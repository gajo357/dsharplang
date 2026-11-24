namespace DSharp.Runtime.Tests.Features;

// Test-only normalized boundary; no production provider interface is selected.
[Flags]
internal enum CRequirement { None = 0, Durable = 1, Canonical = 2, FollowUps = 4 }
internal enum CStatus
{
    Created, Existing, Conflict, Absent, Applied, NotEligible, InvalidOwner, Expired,
    NotExpired, AlreadyRequested, AlreadyTerminal, CancellationFinalized
}
internal enum CState { None, Eligible, Running, Completed, Cancelled }
internal sealed record CIntent(string SubscriptionId, string Target, string Selected);
internal sealed record CAdmission(CStatus Status, Guid? Id, string? Selected, IReadOnlyList<CIntent> Intents);
internal sealed record COwnership(CStatus Status, CState State, Guid? Attempt, Guid? Token,
    DateTimeOffset? Deadline, byte[]? Delivered, byte[]? ObservedExit, bool CancellationRequested);
internal sealed record CCapabilities(bool ByteExact, bool InProcessAtomic, bool Durable,
    bool Canonical, bool FollowUps, bool ControlledTime);

internal interface IConformanceStore
{
    CCapabilities Capabilities { get; }
    CAdmission Accept(string key, byte[] payload, Guid proposedId, string? selected);
    CAdmission Publish(string key, byte[] payload, Guid proposedId, IReadOnlyList<CIntent> intents);
    CAdmission ProbeTargeted(string key, byte[] payload);
    COwnership Claim(string key, Guid invocation, Guid attempt, Guid token, TimeSpan lease);
    COwnership RecoverAsRetry(string key, Guid invocation, Guid attempt, Guid token, DateTimeOffset observedDeadline);
    COwnership Complete(string key, Guid invocation, Guid attempt, Guid token, byte[] result);
    COwnership Cancel(string key, Guid invocation);
    COwnership Inspect(string key, Guid invocation);
    void Advance(TimeSpan delta);
}

internal sealed class ConformanceViolation : Exception
{
    public ConformanceViolation(string invariant, COwnership? observation = null)
        : base($"Conformance invariant failed: {invariant}")
    {
        Invariant = invariant;
        Observation = observation;
    }
    public string Invariant { get; }
    public COwnership? Observation { get; }
}

internal static class ConformanceScenarios
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public static async Task RunAll(Func<CRequirement, IConformanceStore> factory)
    {
        Capabilities(factory);
        Admission(factory);
        Publication(factory);
        await Claims(factory);
        StaleOwner(factory);
        await Cancellation(factory);
    }

    private static void Check(bool condition, string invariant, COwnership? observation = null)
    {
        if (!condition) throw new ConformanceViolation(invariant, observation);
    }

    private static void Capabilities(Func<CRequirement, IConformanceStore> factory)
    {
        var caps = factory(CRequirement.None).Capabilities;
        Check(caps.ByteExact && caps.InProcessAtomic && caps.ControlledTime, "capabilities.reference-subset");
        // The subset exercises no optional guarantee, so a claimed supported
        // capability is neither tested nor certified here. Unsupported ones
        // must reject explicit requests rather than silently degrade.
        foreach (var (requirement, supported) in new[]
        {
            (CRequirement.Durable, caps.Durable),
            (CRequirement.Canonical, caps.Canonical),
            (CRequirement.FollowUps, caps.FollowUps)
        })
        {
            if (supported) continue;
            var rejected = false;
            try { _ = factory(requirement); }
            catch (NotSupportedException) { rejected = true; }
            Check(rejected, $"capabilities.rejected-{requirement}");
        }
    }

    private static void Admission(Func<CRequirement, IConformanceStore> factory)
    {
        var store = factory(CRequirement.None);
        var id = Guid.NewGuid();
        var first = store.Accept("target", [1, 2], id, "first");
        Check(first.Status == CStatus.Created && first.Id == id && first.Selected == "first", "admission.created");
        var retry = store.Accept("target", [1, 2], Guid.NewGuid(), "changed");
        Check(retry.Status == CStatus.Existing && retry.Id == id && retry.Selected == "first", "admission.pinned-retry");
        var conflict = store.Accept("target", [1, 3], Guid.NewGuid(), "changed");
        Check(conflict.Status == CStatus.Conflict && conflict.Id is null, "admission.conflict");
        var probe = store.ProbeTargeted("target", [1, 2]);
        Check(probe.Status == CStatus.Existing && probe.Id == id && probe.Selected == "first", "admission.conflict-no-mutation");
    }

    private static void Publication(Func<CRequirement, IConformanceStore> factory)
    {
        var store = factory(CRequirement.None);
        var id = Guid.NewGuid();
        var intents = new[] { new CIntent("sub-a", "actor-a", "impl-a"), new CIntent("sub-b", "actor-b", "impl-b") };
        var first = store.Publish("pub", [4], id, intents);
        Check(first.Status == CStatus.Created && first.Id == id && first.Intents.SequenceEqual(intents), "publication.created-frozen");
        var retry = store.Publish("pub", [4], Guid.NewGuid(), [new CIntent("sub-c", "actor-c", "changed")]);
        Check(retry.Status == CStatus.Existing && retry.Id == id && retry.Intents.SequenceEqual(intents), "publication.pinned-retry");
        Check(store.Publish("pub", [5], Guid.NewGuid(), []).Status == CStatus.Conflict, "publication.conflict");
        Check(store.ProbeTargeted("pub", [4]).Status == CStatus.Conflict, "publication.shared-key");
        var empty = store.Publish("empty", [4], Guid.NewGuid(), []);
        Check(empty.Status == CStatus.Created && empty.Intents.Count == 0, "publication.empty");
    }

    private static async Task Claims(Func<CRequirement, IConformanceStore> factory)
    {
        var store = factory(CRequirement.None);
        var id = Guid.NewGuid();
        Check(store.Accept("claims", [1], id, "selected").Status == CStatus.Created, "claims.accept");
        using var gate = new Barrier(2);
        var calls = Enumerable.Range(0, 2).Select(_ => Task.Factory.StartNew(() =>
        {
            if (!gate.SignalAndWait(Timeout)) throw new ConformanceViolation("claims.barrier-timeout");
            return store.Claim("claims", id, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromSeconds(10));
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
        var results = await Task.WhenAll(calls).WaitAsync(Timeout);
        Check(results.Count(x => x.Status == CStatus.Applied) == 1 && results.Count(x => x.Status == CStatus.NotEligible) == 1, "claims.single-winner");
        var winner = results.Single(x => x.Status == CStatus.Applied);
        var observed = store.Inspect("claims", id);
        Check(observed.State == CState.Running && observed.Attempt == winner.Attempt && observed.Token == winner.Token, "claims.retained-winner", observed);
    }

    public static void StaleOwner(Func<CRequirement, IConformanceStore> factory)
    {
        var store = factory(CRequirement.None);
        var id = Guid.NewGuid();
        Check(store.Accept("stale", [1], id, "pinned").Status == CStatus.Created, "stale-owner.accept");
        var oldAttempt = Guid.NewGuid(); var oldToken = Guid.NewGuid();
        var old = store.Claim("stale", id, oldAttempt, oldToken, TimeSpan.FromSeconds(10));
        Check(old.Status == CStatus.Applied && old.Deadline.HasValue, "stale-owner.initial-claim", old);
        store.Advance(TimeSpan.FromSeconds(10));
        var recovery = store.RecoverAsRetry("stale", id, oldAttempt, oldToken, old.Deadline!.Value);
        Check(recovery.Status == CStatus.Applied, "stale-owner.recovery", recovery);
        var freshAttempt = Guid.NewGuid(); var freshToken = Guid.NewGuid();
        var fresh = store.Claim("stale", id, freshAttempt, freshToken, TimeSpan.FromSeconds(10));
        Check(fresh.Status == CStatus.Applied, "stale-owner.reclaim", fresh);
        var rejected = store.Complete("stale", id, oldAttempt, oldToken, [42]);
        // Inspect even if the returned status claims rejection: a broken adapter may mutate first.
        var observed = store.Inspect("stale", id);
        Check(observed.State == CState.Running && observed.Attempt == freshAttempt && observed.Token == freshToken && observed.Delivered is null,
            "stale-owner.mutated", observed);
        Check(rejected.Status == CStatus.InvalidOwner, "stale-owner.accepted", observed);
        var original = store.ProbeTargeted("stale", [1]);
        Check(original.Id == id && original.Selected == "pinned", "stale-owner.identity");
    }

    private static async Task Cancellation(Func<CRequirement, IConformanceStore> factory)
    {
        var store = factory(CRequirement.None);
        var id = Guid.NewGuid(); var attempt = Guid.NewGuid(); var token = Guid.NewGuid();
        Check(store.Accept("cancel", [1], id, "selected").Status == CStatus.Created, "cancellation.accept");
        Check(store.Claim("cancel", id, attempt, token, TimeSpan.FromSeconds(10)).Status == CStatus.Applied, "cancellation.claim");
        using var gate = new Barrier(2);
        var cancel = Task.Factory.StartNew(() =>
        {
            if (!gate.SignalAndWait(Timeout)) throw new ConformanceViolation("cancellation.barrier-timeout");
            return store.Cancel("cancel", id);
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var finish = Task.Factory.StartNew(() =>
        {
            if (!gate.SignalAndWait(Timeout)) throw new ConformanceViolation("cancellation.barrier-timeout");
            return store.Complete("cancel", id, attempt, token, [7]);
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        await Task.WhenAll(cancel, finish).WaitAsync(Timeout);
        var result = store.Inspect("cancel", id);
        if (cancel.Result.Status == CStatus.Applied)
            Check(finish.Result.Status == CStatus.CancellationFinalized && result.State == CState.Cancelled && result.Delivered is null && result.ObservedExit is not null && result.ObservedExit.SequenceEqual(new byte[] { 7 }), "cancellation.request-won", result);
        else
            Check(cancel.Result.Status == CStatus.AlreadyTerminal && finish.Result.Status == CStatus.Applied && result.State == CState.Completed && result.Delivered is not null && result.Delivered.SequenceEqual(new byte[] { 7 }), "cancellation.completion-won", result);
    }
}
