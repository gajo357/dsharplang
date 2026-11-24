using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

// A test-local mapping, not a production provider contract or a claim about
// real clocks, restarts, authorization, durability or multi-host fencing.
internal sealed class VolatileLeaseScenarioStore : ILeaseScenarioStore
{
    private const string Application = "lease-conformance";
    private static readonly DateTimeOffset InitialTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly VolatileTargetedStore store;

    public VolatileLeaseScenarioStore(CRequirement requirements)
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

    public LAdmission Accept(string key, byte[] payload, Guid invocation, string selected)
    {
        var result = store.Accept(Request(key, payload), invocation, selected);
        var status = result.Kind switch
        {
            TargetedAcceptKind.Created => LStatus.Created,
            TargetedAcceptKind.Existing => LStatus.Existing,
            TargetedAcceptKind.Conflict => LStatus.Conflict,
            TargetedAcceptKind.InvocationIdCollision => LStatus.IdentityCollision,
            _ => throw new InvalidOperationException($"Unexpected admission: {result.Kind}")
        };
        return new(status, result.Record?.InvocationId, result.Record?.SelectedImplementation, result.Record?.Request.Payload);
    }

    public LAdmission Probe(string key, byte[] payload)
    {
        var result = store.ProbeTargeted(Request(key, payload));
        var status = result.Kind switch
        {
            SubmissionProbeKind.Absent => LStatus.Absent,
            SubmissionProbeKind.Existing => LStatus.Existing,
            SubmissionProbeKind.Conflict => LStatus.Conflict,
            _ => throw new InvalidOperationException($"Unexpected probe: {result.Kind}")
        };
        return new(status, result.Record?.InvocationId, result.Record?.SelectedImplementation, result.Record?.Request.Payload);
    }

    public LSnapshot Claim(string key, Guid invocation, Guid attempt, Guid token, TimeSpan lease) =>
        Transition(store.Claim(Application, key, invocation, attempt, token, lease));
    public LSnapshot Renew(string key, Guid invocation, Guid attempt, Guid token, TimeSpan lease) =>
        Transition(store.Renew(Application, key, invocation, attempt, token, lease));
    public LSnapshot Recover(string key, Guid invocation, Guid attempt, Guid token, DateTimeOffset observedDeadline, TimeSpan delay) =>
        Transition(store.RecoverExpiredAsRetry(Application, key, invocation, attempt, token, observedDeadline, delay));
    public LSnapshot Complete(string key, Guid invocation, Guid attempt, Guid token, byte[] result) =>
        Transition(store.Complete(Application, key, invocation, attempt, token, new TargetedCompletion(result, [1])));
    public LSnapshot Inspect(string key, Guid invocation)
    {
        var current = store.GetOwnership(Application, key, invocation);
        return Snapshot(current is null ? LStatus.Absent : LStatus.Applied, current);
    }
    public void Advance(TimeSpan delta) => store.AdvanceClock(store.CurrentTime.Add(delta));
    internal DateTimeOffset CurrentTime => store.CurrentTime;

    private static LSnapshot Transition(AttemptTransitionResult result) => Snapshot(result.Status switch
    {
        AttemptTransitionStatus.Applied => LStatus.Applied,
        AttemptTransitionStatus.NotFound => LStatus.Absent,
        AttemptTransitionStatus.NotEligible => LStatus.NotEligible,
        AttemptTransitionStatus.NotYetEligible => LStatus.NotYetEligible,
        AttemptTransitionStatus.IdentityCollision => LStatus.IdentityCollision,
        AttemptTransitionStatus.InvalidOwner => LStatus.InvalidOwner,
        AttemptTransitionStatus.Expired => LStatus.Expired,
        AttemptTransitionStatus.NotExpired => LStatus.NotExpired,
        AttemptTransitionStatus.ObservationMismatch => LStatus.ObservationMismatch,
        _ => throw new InvalidOperationException($"Unexpected ownership transition: {result.Status}")
    }, result.Ownership);

    private static LSnapshot Snapshot(LStatus status, TargetedOwnership? value)
    {
        var state = value?.State switch
        {
            null => LState.None,
            TargetedAttemptState.Eligible => LState.Eligible,
            TargetedAttemptState.Running => LState.Running,
            TargetedAttemptState.Completed => LState.Completed,
            TargetedAttemptState.Cancelled => LState.Cancelled,
            _ => throw new InvalidOperationException($"Unexpected ownership state: {value.State}")
        };
        var history = value?.RecoveryHistory.Select(item => new LRecovery(
            item.Kind switch
            {
                AttemptRecoveryKind.LeaseExpired => LRecoveryKind.LeaseExpired,
                AttemptRecoveryKind.ReportedFault => LRecoveryKind.ReportedFault,
                _ => throw new InvalidOperationException($"Unexpected recovery kind: {item.Kind}")
            }, item.AttemptId, item.Token, item.LeaseUntil, item.EligibleAt, item.Fault?.Payload)).ToArray() ?? [];
        return new(status, state, value?.AttemptId, value?.Token, value?.LeaseUntil,
            value?.EligibleAt, history, value?.Completion?.Payload);
    }
}

// This mutant replaces the stale observed deadline with the current one. It
// delegates the actual transition to the real store and therefore creates a
// real forbidden recovery when the newer lease has also expired.
internal sealed class StaleDeadlineBypassDecorator(VolatileLeaseScenarioStore inner) : ILeaseScenarioStore
{
    public string? MutatedKey { get; private set; }
    public Guid MutatedInvocation { get; private set; }
    public CCapabilities Capabilities => inner.Capabilities;
    public LAdmission Accept(string key, byte[] payload, Guid invocation, string selected) => inner.Accept(key, payload, invocation, selected);
    public LAdmission Probe(string key, byte[] payload) => inner.Probe(key, payload);
    public LSnapshot Claim(string key, Guid invocation, Guid attempt, Guid token, TimeSpan lease) => inner.Claim(key, invocation, attempt, token, lease);
    public LSnapshot Renew(string key, Guid invocation, Guid attempt, Guid token, TimeSpan lease) => inner.Renew(key, invocation, attempt, token, lease);
    public LSnapshot Recover(string key, Guid invocation, Guid attempt, Guid token, DateTimeOffset observedDeadline, TimeSpan delay)
    {
        var current = inner.Inspect(key, invocation);
        var submittedDeadline = current.Deadline is { } deadline && inner.CurrentTime >= deadline
            ? deadline : observedDeadline;
        var result = inner.Recover(key, invocation, attempt, token, submittedDeadline, delay);
        if (result.Status == LStatus.Applied) { MutatedKey = key; MutatedInvocation = invocation; }
        return result;
    }
    public LSnapshot Complete(string key, Guid invocation, Guid attempt, Guid token, byte[] result) => inner.Complete(key, invocation, attempt, token, result);
    public LSnapshot Inspect(string key, Guid invocation) => inner.Inspect(key, invocation);
    public void Advance(TimeSpan delta) => inner.Advance(delta);
}

public sealed class LeaseRetryConformanceTests
{
    [Fact]
    public async Task VolatileReferencePassesTheScopedLeaseAndRetryScenarios()
    {
        await LeaseRetryScenarios.RunAll(requirements => new VolatileLeaseScenarioStore(requirements));
    }

    [Fact]
    public void VolatileReferenceRejectsUnsupportedRequirements()
    {
        var store = new VolatileLeaseScenarioStore(CRequirement.None);
        Assert.True(store.Capabilities.ByteExact);
        Assert.True(store.Capabilities.InProcessAtomic);
        Assert.True(store.Capabilities.ControlledTime);
        Assert.False(store.Capabilities.Durable);
        Assert.False(store.Capabilities.Canonical);
        Assert.False(store.Capabilities.FollowUps);
        Assert.Throws<NotSupportedException>(() => new VolatileLeaseScenarioStore(CRequirement.Durable));
        Assert.Throws<NotSupportedException>(() => new VolatileLeaseScenarioStore(CRequirement.Canonical));
        Assert.Throws<NotSupportedException>(() => new VolatileLeaseScenarioStore(CRequirement.FollowUps));
        Assert.Throws<NotSupportedException>(() => new VolatileLeaseScenarioStore((CRequirement)128));
    }

    [Fact]
    public void SharedScenarioDetectsARealStaleDeadlineRecovery()
    {
        VolatileLeaseScenarioStore? actual = null;
        StaleDeadlineBypassDecorator? broken = null;
        var violation = Assert.Throws<LeaseConformanceViolation>(() => LeaseRetryScenarios.StaleDeadline(requirements =>
        {
            actual = new VolatileLeaseScenarioStore(requirements);
            broken = new StaleDeadlineBypassDecorator(actual);
            return broken;
        }));
        Assert.Equal("lease.stale-observation-mutated", violation.Invariant);
        Assert.Equal(LState.Eligible, violation.Observation!.State);
        Assert.NotNull(actual);
        Assert.NotNull(broken);
        Assert.NotNull(broken.MutatedKey);
        var backing = actual.Inspect(broken.MutatedKey, broken.MutatedInvocation);
        Assert.Equal(LState.Eligible, backing.State);
        var recovery = Assert.Single(backing.History);
        Assert.Equal(LRecoveryKind.LeaseExpired, recovery.Kind);
        Assert.Null(recovery.Fault);
    }
}
