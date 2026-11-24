using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

internal interface IPublicationFailureSubject
{
    void InjectAfterReservation(string key, byte[] payload, Guid id, IReadOnlyList<PublicationIntent> intents);
    PublicationAcceptResult Accept(string key, byte[] payload, Guid id, IEnumerable<PublicationIntent> intents);
    PublicationProbeResult Probe(string key, byte[] payload);
    TargetedProbeResult ProbeTarget(string key, byte[] payload);
    PublicationRecord? Lookup(string key);
}

internal sealed class PublicationFailureSubject : IPublicationFailureSubject
{
    private const string App = "failure-conformance";
    internal VolatileTargetedStore Store { get; } = new(TimeSpan.FromMinutes(1));
    internal static PublicationRequest Request(string key, byte[] payload) => new(App, key, payload, [1], []);
    public void InjectAfterReservation(string key, byte[] payload, Guid id, IReadOnlyList<PublicationIntent> intents) =>
        Store.AcceptPublicationForConformance(Request(key, payload), id, intents, PublicationFailurePoint.AfterIdentityReservation);
    public PublicationAcceptResult Accept(string key, byte[] payload, Guid id, IEnumerable<PublicationIntent> intents) =>
        Store.AcceptPublication(Request(key, payload), id, intents);
    public PublicationProbeResult Probe(string key, byte[] payload) => Store.ProbePublication(Request(key, payload));
    public TargetedProbeResult ProbeTarget(string key, byte[] payload) =>
        Store.ProbeTargeted(new TargetedRequest(App, key, TargetedOperation.Dispatch, "target", payload, [1], []));
    public PublicationRecord? Lookup(string key) => Store.LookupPublication(App, key);
}

// This deliberately broken adapter commits a prefix before claiming the same
// injected failure. It uses the real store so the partial record is observable.
internal sealed class PrefixCommitFailureSubject(PublicationFailureSubject inner) : IPublicationFailureSubject
{
    public void InjectAfterReservation(string key, byte[] payload, Guid id, IReadOnlyList<PublicationIntent> intents)
    {
        var result = inner.Accept(key, payload, id, intents.Take(1));
        if (result.Kind != PublicationAcceptKind.Created) throw new InvalidOperationException("Negative-control setup failed.");
        throw new PublicationInjectedFailureException();
    }
    public PublicationAcceptResult Accept(string key, byte[] payload, Guid id, IEnumerable<PublicationIntent> intents) => inner.Accept(key, payload, id, intents);
    public PublicationProbeResult Probe(string key, byte[] payload) => inner.Probe(key, payload);
    public TargetedProbeResult ProbeTarget(string key, byte[] payload) => inner.ProbeTarget(key, payload);
    public PublicationRecord? Lookup(string key) => inner.Lookup(key);
}

internal sealed class PublicationFailureInvariant(string name) : Exception(name)
{
    public string Invariant { get; } = name;
}

internal sealed class SimulatedLostAcknowledgementException : Exception { }

internal static class PublicationFailureScenarios
{
    internal static readonly PublicationIntent[] Intents =
    [new("sub-a", "actor-a", "impl-a"), new("sub-b", "actor-b", "impl-b")];

    private static void Check(bool condition, string invariant)
    {
        if (!condition) throw new PublicationFailureInvariant(invariant);
    }

    public static void Rollback(Func<IPublicationFailureSubject> factory)
    {
        var subject = factory();
        var id = Guid.NewGuid();
        var threw = false;
        try { subject.InjectAfterReservation("rollback", [1, 2], id, Intents); }
        catch (PublicationInjectedFailureException) { threw = true; }
        Check(threw, "rollback.expected-sentinel");
        Check(subject.Lookup("rollback") is null && subject.Probe("rollback", [1, 2]).Kind == SubmissionProbeKind.Absent,
            "rollback.partial-record");
        Check(subject.ProbeTarget("rollback", [1, 2]).Kind == SubmissionProbeKind.Absent, "rollback.shared-key");
        var inserted = subject.Accept("rollback", [1, 2], id, Intents);
        Check(inserted.Kind == PublicationAcceptKind.Created && inserted.Record?.PublicationId == id, "rollback.id-reusable");
        Check(inserted.Record!.Intents.SequenceEqual(Intents), "rollback.full-intents");
    }

    public static void LostAcknowledgement(Func<IPublicationFailureSubject> factory)
    {
        var subject = factory();
        var id = Guid.NewGuid();
        var lost = false;
        try { CommitThenLoseAcknowledgement(subject, id); }
        catch (SimulatedLostAcknowledgementException) { lost = true; }
        Check(lost, "lost-ack.expected-sentinel");
        var retry = subject.Accept("lost-ack", [3, 4], Guid.NewGuid(), [new PublicationIntent("other", "other", "other")]);
        Check(retry.Kind == PublicationAcceptKind.Existing && retry.Record?.PublicationId == id, "lost-ack.pinned-id");
        Check(retry.Record!.Intents.SequenceEqual(Intents), "lost-ack.frozen-intents");
        Check(retry.Record.Intents.Select(intent => (retry.Record.PublicationId, intent.SubscriptionId))
            .SequenceEqual(Intents.Select(intent => (id, intent.SubscriptionId))), "lost-ack.delivery-identities");
        Check(subject.Accept("lost-ack", [3, 9], Guid.NewGuid(), []).Kind == PublicationAcceptKind.Conflict,
            "lost-ack.changed-request");
    }

    private static void CommitThenLoseAcknowledgement(IPublicationFailureSubject subject, Guid id)
    {
        // The actual store has returned from its gate before this simulated
        // response loss; this is neither a crash nor durable-recovery evidence.
        var committed = subject.Accept("lost-ack", [3, 4], id, Intents);
        Check(committed.Kind == PublicationAcceptKind.Created, "lost-ack.commit");
        throw new SimulatedLostAcknowledgementException();
    }
}

public sealed class PublicationFailureConformanceTests
{
    [Fact]
    public void ReservationRollbackAndLostAcknowledgementPassAgainstReferenceStore()
    {
        PublicationFailureScenarios.Rollback(() => new PublicationFailureSubject());
        PublicationFailureScenarios.LostAcknowledgement(() => new PublicationFailureSubject());
    }

    [Fact]
    public void SharedRollbackScenarioDetectsARealRetainedPartialCommit()
    {
        PublicationFailureSubject? actual = null;
        var error = Assert.Throws<PublicationFailureInvariant>(() => PublicationFailureScenarios.Rollback(() =>
        {
            actual = new PublicationFailureSubject();
            return new PrefixCommitFailureSubject(actual);
        }));
        Assert.Equal("rollback.partial-record", error.Invariant);
        Assert.NotNull(actual);
        var partial = actual.Lookup("rollback");
        Assert.NotNull(partial);
        Assert.Equal(PublicationFailureScenarios.Intents[0], Assert.Single(partial.Intents));
        Assert.Equal(SubmissionProbeKind.Existing, actual.Probe("rollback", [1, 2]).Kind);
        Assert.Equal(SubmissionProbeKind.Conflict, actual.ProbeTarget("rollback", [1, 2]).Kind);
    }

    [Fact]
    public void ValidationAndThrowingEnumerationLeaveNoRecordOrReservedId()
    {
        var subject = new PublicationFailureSubject();
        var id = Guid.NewGuid();
        Assert.Throws<ArgumentOutOfRangeException>(() => subject.Store.AcceptPublicationForConformance(
            PublicationFailureSubject.Request("invalid", [1]), id, PublicationFailureScenarios.Intents, (PublicationFailurePoint)100));
        Assert.Null(subject.Lookup("invalid"));
        Assert.Equal(SubmissionProbeKind.Absent, subject.ProbeTarget("invalid", [1]).Kind);
        Assert.Throws<InvalidOperationException>(() => subject.Accept("invalid", [1], id, Throwing()));
        Assert.Null(subject.Lookup("invalid"));
        Assert.Equal(PublicationAcceptKind.Created, subject.Accept("invalid", [1], id, PublicationFailureScenarios.Intents).Kind);
        // Retained matching requests resolve before either proposed enumeration or injection.
        Assert.Equal(PublicationAcceptKind.Existing, subject.Accept("invalid", [1], Guid.Empty, Throwing()).Kind);
        var existing = subject.Store.AcceptPublicationForConformance(PublicationFailureSubject.Request("invalid", [1]),
            Guid.Empty, Throwing(), PublicationFailurePoint.AfterIdentityReservation);
        Assert.Equal(PublicationAcceptKind.Existing, existing.Kind);
        Assert.Equal(id, existing.Record!.PublicationId);
    }

    private static IEnumerable<PublicationIntent> Throwing()
    {
        throw new InvalidOperationException("Intent enumeration failed.");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }

    [Fact]
    public async Task ConcurrentMatchingPublicationsRetainOneWholeSet()
    {
        const int count = 12;
        var subject = new PublicationFailureSubject();
        using var barrier = new Barrier(count);
        var tasks = Enumerable.Range(0, count).Select(i => Task.Factory.StartNew(() =>
        {
            if (!barrier.SignalAndWait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Publication race timed out.");
            var intents = new[] { new PublicationIntent("a", "target", $"selection-{i}"), new PublicationIntent("b", "other", $"selection-{i}") };
            return subject.Accept("race", [1], Guid.NewGuid(), intents);
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
        var outcomes = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(30));
        var winner = Assert.Single(outcomes, item => item.Kind == PublicationAcceptKind.Created);
        Assert.Equal(count - 1, outcomes.Count(item => item.Kind == PublicationAcceptKind.Existing));
        Assert.All(outcomes, item =>
        {
            Assert.Equal(winner.Record!.PublicationId, item.Record!.PublicationId);
            Assert.Equal(winner.Record.Intents, item.Record.Intents);
        });
        Assert.Equal(2, subject.Lookup("race")!.Intents.Count);
    }
}
