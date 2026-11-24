using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

// Test-local adapter for the volatile reference. It does not define a public
// provider API or establish authorization, delivery, durability or real time.
internal sealed class VolatileOutcomeScenarioStore : IOutcomeScenarioStore
{
    private const string Application = "outcome-conformance";
    private static readonly DateTimeOffset InitialTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly VolatileTargetedStore store;

    public VolatileOutcomeScenarioStore(CRequirement requirements)
    {
        const CRequirement known = CRequirement.Durable | CRequirement.Canonical | CRequirement.FollowUps;
        if ((requirements & ~known) != 0) throw new NotSupportedException("Unknown conformance requirement.");
        var requested = VolatileStoreRequirements.None;
        if ((requirements & CRequirement.Durable) != 0) requested |= VolatileStoreRequirements.Durability;
        if ((requirements & CRequirement.Canonical) != 0) requested |= VolatileStoreRequirements.CanonicalSemanticEquality;
        if ((requirements & CRequirement.FollowUps) != 0) requested |= VolatileStoreRequirements.AtomicFollowUps;
        store = new VolatileTargetedStore(TimeSpan.FromMinutes(1), requested);
        store.InitializeClock(InitialTime);
    }

    public CCapabilities Capabilities => new(ByteExact: true, InProcessAtomic: true, Durable: false,
        Canonical: false, FollowUps: false, ControlledTime: true);
    private static TargetedRequest Request(string key, byte[] payload) =>
        new(Application, key, TargetedOperation.Dispatch, "target", payload, [1], []);

    public OAdmission Accept(string key, byte[] payload, Guid id, string selected)
    {
        var result = store.Accept(Request(key, payload), id, selected);
        var status = result.Kind switch
        {
            TargetedAcceptKind.Created => OStatus.Created,
            TargetedAcceptKind.Existing => OStatus.Existing,
            TargetedAcceptKind.Conflict => OStatus.Conflict,
            _ => throw new InvalidOperationException($"Unexpected acceptance: {result.Kind}")
        };
        return new(status, result.Record?.InvocationId, result.Record?.SelectedImplementation, result.Record?.Request.Payload);
    }

    public OAdmission Probe(string key, byte[] payload)
    {
        var result = store.ProbeTargeted(Request(key, payload));
        var status = result.Kind switch
        {
            SubmissionProbeKind.Absent => OStatus.Absent,
            SubmissionProbeKind.Existing => OStatus.Existing,
            SubmissionProbeKind.Conflict => OStatus.Conflict,
            _ => throw new InvalidOperationException($"Unexpected probe: {result.Kind}")
        };
        return new(status, result.Record?.InvocationId, result.Record?.SelectedImplementation, result.Record?.Request.Payload);
    }

    public OSnapshot Claim(string key, Guid id, Guid attempt, Guid token, TimeSpan lease) =>
        Transition(store.Claim(Application, key, id, attempt, token, lease));

    public OSnapshot Cancel(string key, Guid id)
    {
        var result = store.RequestCancellation(Application, key, id);
        var status = result.Status switch
        {
            CancellationRequestStatus.Applied => OStatus.Applied,
            CancellationRequestStatus.AlreadyRequested => OStatus.AlreadyRequested,
            CancellationRequestStatus.AlreadyTerminal => OStatus.AlreadyTerminal,
            CancellationRequestStatus.NotFound => OStatus.Absent,
            _ => throw new InvalidOperationException($"Unexpected cancellation: {result.Status}")
        };
        return Snapshot(status, result.Ownership);
    }

    public OSnapshot Complete(string key, Guid id, Guid attempt, Guid token, OData result) =>
        Transition(store.Complete(Application, key, id, attempt, token, new TargetedCompletion(result.Payload, result.Metadata)));

    public OSnapshot Fail(string key, Guid id, Guid attempt, Guid token, OFault fault) =>
        Transition(store.FailAfterFault(Application, key, id, attempt, token,
            new ReportedTerminalFault(new TargetedFault(fault.Payload, fault.Metadata), fault.Origin)));

    public OSnapshot RecoverRetry(string key, Guid id, Guid attempt, Guid token, DateTimeOffset observedDeadline, TimeSpan delay) =>
        Transition(store.RecoverExpiredAsRetry(Application, key, id, attempt, token, observedDeadline, delay));

    public OSnapshot Inspect(string key, Guid id)
    {
        var value = store.GetOwnership(Application, key, id);
        return Snapshot(value is null ? OStatus.Absent : OStatus.Applied, value);
    }

    public void Advance(TimeSpan delta) => store.AdvanceClock(store.CurrentTime.Add(delta));

    private static OSnapshot Transition(AttemptTransitionResult result) => Snapshot(result.Status switch
    {
        AttemptTransitionStatus.Applied => OStatus.Applied,
        AttemptTransitionStatus.NotFound => OStatus.Absent,
        AttemptTransitionStatus.NotEligible => OStatus.NotEligible,
        AttemptTransitionStatus.InvalidOwner => OStatus.InvalidOwner,
        AttemptTransitionStatus.Expired => OStatus.Expired,
        AttemptTransitionStatus.NotExpired => OStatus.NotExpired,
        AttemptTransitionStatus.ObservationMismatch => OStatus.ObservationMismatch,
        AttemptTransitionStatus.CancellationFinalized => OStatus.CancellationFinalized,
        _ => throw new InvalidOperationException($"Unexpected transition: {result.Status}")
    }, result.Ownership);

    private static OData? Normal(TargetedCompletion? completion) => completion is null
        ? null : new OData(completion.Payload, completion.ContractMetadata);
    private static OFault? Fault(ReportedTerminalFault? fault) => fault is null
        ? null : new OFault(fault.Fault.Payload, fault.Fault.ContractMetadata, fault.OriginInvocationId);

    private static OSnapshot Snapshot(OStatus status, TargetedOwnership? value)
    {
        var state = value?.State switch
        {
            null => OState.None,
            TargetedAttemptState.Eligible => OState.Eligible,
            TargetedAttemptState.Running => OState.Running,
            TargetedAttemptState.Completed => OState.Completed,
            TargetedAttemptState.FailedReportedFault or TargetedAttemptState.FailedLeaseExpired => OState.Failed,
            TargetedAttemptState.Cancelled => OState.Cancelled,
            _ => throw new InvalidOperationException($"Unexpected outcome: {value.State}")
        };
        OExit? exit = value?.ObservedCancellationExit is { } observed
            ? new OExit(observed.Kind switch
            {
                CancellationExitKind.Normal => OExitKind.Normal,
                CancellationExitKind.ReportedFault => OExitKind.ReportedFault,
                _ => throw new InvalidOperationException($"Unexpected cancellation exit: {observed.Kind}")
            }, Normal(observed.Completion), Fault(observed.Fault)) : null;
        return new(status, state, value?.CancellationRequested ?? false, value?.LeaseUntil,
            Normal(value?.Completion), Fault(value?.TerminalFault), exit);
    }
}

// This mutant routes a cancellation to a real decoy, returning the decoy's
// genuine status, while the actual target remains uncancelled and can complete.
internal sealed class MisroutedCancellationDecorator(VolatileOutcomeScenarioStore inner) : IOutcomeScenarioStore
{
    public string? TargetKey { get; private set; }
    public Guid TargetId { get; private set; }
    public string DecoyKey { get; } = $"decoy-{Guid.NewGuid()}";
    public Guid DecoyId { get; } = Guid.NewGuid();
    public CCapabilities Capabilities => inner.Capabilities;
    public OAdmission Accept(string key, byte[] payload, Guid id, string selected) => inner.Accept(key, payload, id, selected);
    public OAdmission Probe(string key, byte[] payload) => inner.Probe(key, payload);
    public OSnapshot Claim(string key, Guid id, Guid attempt, Guid token, TimeSpan lease) => inner.Claim(key, id, attempt, token, lease);
    public OSnapshot Cancel(string key, Guid id)
    {
        TargetKey = key; TargetId = id;
        if (inner.Accept(DecoyKey, [77], DecoyId, "decoy").Status != OStatus.Created)
            throw new InvalidOperationException("Decoy setup failed.");
        return inner.Cancel(DecoyKey, DecoyId);
    }
    public OSnapshot Complete(string key, Guid id, Guid attempt, Guid token, OData result) => inner.Complete(key, id, attempt, token, result);
    public OSnapshot Fail(string key, Guid id, Guid attempt, Guid token, OFault fault) => inner.Fail(key, id, attempt, token, fault);
    public OSnapshot RecoverRetry(string key, Guid id, Guid attempt, Guid token, DateTimeOffset observedDeadline, TimeSpan delay) => inner.RecoverRetry(key, id, attempt, token, observedDeadline, delay);
    public OSnapshot Inspect(string key, Guid id) => inner.Inspect(key, id);
    public void Advance(TimeSpan delta) => inner.Advance(delta);
}

public sealed class OutcomeConformanceTests
{
    [Fact]
    public void VolatileReferencePassesTheScopedOutcomeScenarios() =>
        OutcomeScenarios.RunAll(requirements => new VolatileOutcomeScenarioStore(requirements));

    [Fact]
    public void VolatileReferenceRejectsUnsupportedRequirements()
    {
        var store = new VolatileOutcomeScenarioStore(CRequirement.None);
        Assert.True(store.Capabilities.ByteExact);
        Assert.True(store.Capabilities.InProcessAtomic);
        Assert.True(store.Capabilities.ControlledTime);
        Assert.False(store.Capabilities.Durable);
        Assert.False(store.Capabilities.Canonical);
        Assert.False(store.Capabilities.FollowUps);
        Assert.Throws<NotSupportedException>(() => new VolatileOutcomeScenarioStore(CRequirement.Durable));
        Assert.Throws<NotSupportedException>(() => new VolatileOutcomeScenarioStore(CRequirement.Canonical));
        Assert.Throws<NotSupportedException>(() => new VolatileOutcomeScenarioStore(CRequirement.FollowUps));
        Assert.Throws<NotSupportedException>(() => new VolatileOutcomeScenarioStore((CRequirement)128));
    }

    [Fact]
    public void SharedScenarioDetectsRealCompletionAfterMisroutedCancellation()
    {
        VolatileOutcomeScenarioStore? actual = null;
        MisroutedCancellationDecorator? broken = null;
        var violation = Assert.Throws<OutcomeConformanceViolation>(() => OutcomeScenarios.CancellationNormal(requirements =>
        {
            actual = new VolatileOutcomeScenarioStore(requirements);
            broken = new MisroutedCancellationDecorator(actual);
            return broken;
        }));
        Assert.Equal("cancellation.request-lost-delivered", violation.Invariant);
        Assert.Equal(OState.Completed, violation.Observation!.State);
        Assert.NotNull(actual);
        Assert.NotNull(broken);
        Assert.NotNull(broken.TargetKey);
        var target = actual.Inspect(broken.TargetKey, broken.TargetId);
        Assert.Equal(OState.Completed, target.State);
        Assert.NotNull(target.Delivered);
        Assert.Equal(new byte[] { 0, 69, 82, 82 }, target.Delivered.Payload);
        Assert.Equal(new byte[] { 3, 4 }, target.Delivered.Metadata);
        var decoy = actual.Inspect(broken.DecoyKey, broken.DecoyId);
        Assert.Equal(OState.Cancelled, decoy.State);
        Assert.True(decoy.CancellationRequested);
        Assert.Null(decoy.Delivered);
    }
}
