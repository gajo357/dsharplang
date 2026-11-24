using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

// This adapter describes the current in-process reference only. It is not a
// public RuntimeStore protocol or evidence of durable provider conformance.
internal sealed class VolatileConformanceStore : IConformanceStore
{
    private const string Application = "conformance-app";
    private static readonly DateTimeOffset InitialTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly VolatileTargetedStore store;

    public VolatileConformanceStore(CRequirement requirements)
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

    private static TargetedRequest Target(string key, byte[] payload) =>
        new(Application, key, TargetedOperation.Dispatch, "target", payload, [1], []);
    private static PublicationRequest Publication(string key, byte[] payload) =>
        new(Application, key, payload, [1], []);

    public CAdmission Accept(string key, byte[] payload, Guid proposedId, string? selected)
    {
        var result = store.Accept(Target(key, payload), proposedId, selected);
        return Admission(result.Kind switch
        {
            TargetedAcceptKind.Created => CStatus.Created,
            TargetedAcceptKind.Existing => CStatus.Existing,
            TargetedAcceptKind.Conflict => CStatus.Conflict,
            _ => throw new InvalidOperationException($"Unexpected targeted acceptance: {result.Kind}")
        }, result.Record);
    }

    public CAdmission ProbeTargeted(string key, byte[] payload)
    {
        var result = store.ProbeTargeted(Target(key, payload));
        return Admission(result.Kind switch
        {
            SubmissionProbeKind.Absent => CStatus.Absent,
            SubmissionProbeKind.Existing => CStatus.Existing,
            SubmissionProbeKind.Conflict => CStatus.Conflict,
            _ => throw new InvalidOperationException($"Unexpected targeted probe: {result.Kind}")
        }, result.Record);
    }

    private static CAdmission Admission(CStatus status, TargetedRecord? record) =>
        new(status, record?.InvocationId, record?.SelectedImplementation, Array.Empty<CIntent>());

    public CAdmission Publish(string key, byte[] payload, Guid proposedId, IReadOnlyList<CIntent> intents)
    {
        var result = store.AcceptPublication(Publication(key, payload), proposedId,
            intents.Select(intent => new PublicationIntent(intent.SubscriptionId, intent.Target, intent.Selected)));
        var status = result.Kind switch
        {
            PublicationAcceptKind.Created => CStatus.Created,
            PublicationAcceptKind.Existing => CStatus.Existing,
            PublicationAcceptKind.Conflict => CStatus.Conflict,
            _ => throw new InvalidOperationException($"Unexpected publication acceptance: {result.Kind}")
        };
        var frozen = result.Record?.Intents.Select(intent =>
            new CIntent(intent.SubscriptionId, intent.PublicTarget, intent.SelectedImplementation)).ToArray() ?? [];
        return new(status, result.Record?.PublicationId, null, frozen);
    }

    public COwnership Claim(string key, Guid invocation, Guid attempt, Guid token, TimeSpan lease) =>
        Transition(store.Claim(Application, key, invocation, attempt, token, lease));

    public COwnership RecoverAsRetry(string key, Guid invocation, Guid attempt, Guid token, DateTimeOffset observedDeadline) =>
        Transition(store.RecoverExpiredAsRetry(Application, key, invocation, attempt, token, observedDeadline, TimeSpan.Zero));

    public COwnership Complete(string key, Guid invocation, Guid attempt, Guid token, byte[] result) =>
        Transition(store.Complete(Application, key, invocation, attempt, token, new TargetedCompletion(result, [1])));

    public COwnership Cancel(string key, Guid invocation)
    {
        var result = store.RequestCancellation(Application, key, invocation);
        var status = result.Status switch
        {
            CancellationRequestStatus.Applied => CStatus.Applied,
            CancellationRequestStatus.AlreadyRequested => CStatus.AlreadyRequested,
            CancellationRequestStatus.AlreadyTerminal => CStatus.AlreadyTerminal,
            CancellationRequestStatus.NotFound => CStatus.Absent,
            _ => throw new InvalidOperationException($"Unexpected cancellation: {result.Status}")
        };
        return Ownership(status, result.Ownership);
    }

    public COwnership Inspect(string key, Guid invocation)
    {
        var value = store.GetOwnership(Application, key, invocation);
        return Ownership(value is null ? CStatus.Absent : CStatus.Applied, value);
    }

    public void Advance(TimeSpan delta) => store.AdvanceClock(store.CurrentTime.Add(delta));

    private static COwnership Transition(AttemptTransitionResult result) => Ownership(result.Status switch
    {
        AttemptTransitionStatus.Applied => CStatus.Applied,
        AttemptTransitionStatus.NotFound => CStatus.Absent,
        AttemptTransitionStatus.NotEligible => CStatus.NotEligible,
        AttemptTransitionStatus.InvalidOwner => CStatus.InvalidOwner,
        AttemptTransitionStatus.Expired => CStatus.Expired,
        AttemptTransitionStatus.NotExpired => CStatus.NotExpired,
        AttemptTransitionStatus.CancellationFinalized => CStatus.CancellationFinalized,
        _ => throw new InvalidOperationException($"Unexpected ownership transition: {result.Status}")
    }, result.Ownership);

    private static COwnership Ownership(CStatus status, TargetedOwnership? value) => new(status,
        value?.State switch
        {
            null => CState.None,
            TargetedAttemptState.Eligible => CState.Eligible,
            TargetedAttemptState.Running => CState.Running,
            TargetedAttemptState.Completed => CState.Completed,
            TargetedAttemptState.Cancelled => CState.Cancelled,
            _ => throw new InvalidOperationException($"Unexpected ownership state: {value.State}")
        }, value?.AttemptId, value?.Token, value?.LeaseUntil, value?.Completion?.Payload,
        value?.ObservedCancellationExit?.Completion?.Payload, value?.CancellationRequested ?? false);
}

// The negative control deliberately corrupts only stale completion: it sends
// the current attempt and token to the real store and thereby commits the very
// transition that a correctly fenced adapter must reject.
internal sealed class StaleOwnerBypassDecorator(IConformanceStore inner) : IConformanceStore
{
    private Guid liveAttempt;
    private Guid liveToken;
    public string? MutatedKey { get; private set; }
    public Guid MutatedInvocation { get; private set; }
    public CCapabilities Capabilities => inner.Capabilities;
    public CAdmission Accept(string key, byte[] payload, Guid proposedId, string? selected) => inner.Accept(key, payload, proposedId, selected);
    public CAdmission Publish(string key, byte[] payload, Guid proposedId, IReadOnlyList<CIntent> intents) => inner.Publish(key, payload, proposedId, intents);
    public CAdmission ProbeTargeted(string key, byte[] payload) => inner.ProbeTargeted(key, payload);
    public COwnership Claim(string key, Guid invocation, Guid attempt, Guid token, TimeSpan lease)
    {
        var result = inner.Claim(key, invocation, attempt, token, lease);
        if (result.Status == CStatus.Applied) { liveAttempt = attempt; liveToken = token; }
        return result;
    }
    public COwnership RecoverAsRetry(string key, Guid invocation, Guid attempt, Guid token, DateTimeOffset observedDeadline) =>
        inner.RecoverAsRetry(key, invocation, attempt, token, observedDeadline);
    public COwnership Complete(string key, Guid invocation, Guid attempt, Guid token, byte[] result)
    {
        MutatedKey = key;
        MutatedInvocation = invocation;
        return inner.Complete(key, invocation, liveAttempt, liveToken, result);
    }
    public COwnership Cancel(string key, Guid invocation) => inner.Cancel(key, invocation);
    public COwnership Inspect(string key, Guid invocation) => inner.Inspect(key, invocation);
    public void Advance(TimeSpan delta) => inner.Advance(delta);
}

public sealed class RuntimeStoreConformanceTests
{
    [Fact]
    public async Task VolatileReferencePassesItsExplicitlyScopedSharedScenarios()
    {
        await ConformanceScenarios.RunAll(requirements => new VolatileConformanceStore(requirements));
    }

    [Fact]
    public void UnsupportedProviderRequirementsFailClosed()
    {
        var store = new VolatileConformanceStore(CRequirement.None);
        Assert.True(store.Capabilities.ByteExact);
        Assert.True(store.Capabilities.InProcessAtomic);
        Assert.True(store.Capabilities.ControlledTime);
        Assert.False(store.Capabilities.Durable);
        Assert.False(store.Capabilities.Canonical);
        Assert.False(store.Capabilities.FollowUps);
        Assert.Throws<NotSupportedException>(() => new VolatileConformanceStore(CRequirement.Durable));
        Assert.Throws<NotSupportedException>(() => new VolatileConformanceStore(CRequirement.Canonical));
        Assert.Throws<NotSupportedException>(() => new VolatileConformanceStore(CRequirement.FollowUps));
        Assert.Throws<NotSupportedException>(() => new VolatileConformanceStore((CRequirement)128));
    }

    [Fact]
    public void SharedStaleOwnerScenarioDetectsAnActualForbiddenCommit()
    {
        StaleOwnerBypassDecorator? broken = null;
        VolatileConformanceStore? actual = null;
        var violation = Assert.Throws<ConformanceViolation>(() => ConformanceScenarios.StaleOwner(requirements =>
        {
            actual = new VolatileConformanceStore(requirements);
            broken = new StaleOwnerBypassDecorator(actual);
            return broken;
        }));
        Assert.Equal("stale-owner.mutated", violation.Invariant);
        Assert.Equal(CState.Completed, violation.Observation!.State);
        Assert.NotNull(broken);
        Assert.NotNull(actual);
        Assert.NotNull(broken.MutatedKey);
        var actualPostcondition = actual.Inspect(broken.MutatedKey, broken.MutatedInvocation);
        Assert.Equal(CState.Completed, actualPostcondition.State);
        Assert.Equal(new byte[] { 42 }, actualPostcondition.Delivered);
    }
}
