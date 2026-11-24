using DSharp.Runtime;

namespace DSharp.Runtime.Tests.Features;

// Clock control is fixture-only, never part of the provider contract.
internal sealed record ContractFixture(IRuntimeStore Store, Action<TimeSpan> Advance);

internal sealed class ContractScenarioViolation : Exception
{
    public ContractScenarioViolation(string invariant, TargetedOwnership? observation = null)
        : base($"RuntimeStore contract invariant failed: {invariant}")
    {
        Invariant = invariant;
        Observation = observation;
    }
    public string Invariant { get; }
    public TargetedOwnership? Observation { get; }
}

internal static class RuntimeStoreContractScenarios
{
    private const string App = "contract-app";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static void Check(bool condition, string invariant, TargetedOwnership? observation = null)
    {
        if (!condition) throw new ContractScenarioViolation(invariant, observation);
    }

    private static TargetedRequest Target(string key, byte[]? payload = null) =>
        new(App, key, TargetedOperation.Dispatch, "target", payload ?? [1, 2], [3], [4]);
    private static PublicationRequest Publication(string key, byte[]? payload = null) =>
        new(App, key, payload ?? [1, 2], [3], [4]);

    public static async Task RunAll(Func<ContractFixture> factory)
    {
        Capabilities(factory);
        AdmissionAndPublication(factory);
        ParentAdmission(factory);
        await ConcurrentClaims(factory);
        StaleObservation(factory);
        Cancellation(factory);
    }

    private static void Capabilities(Func<ContractFixture> factory)
    {
        var store = factory().Store;
        var supported = store.Capabilities.Guarantees;
        var required = RuntimeStoreGuarantees.ByteExactComparison | RuntimeStoreGuarantees.InProcessAtomicity |
                       RuntimeStoreGuarantees.SimulatedTime;
        Check((supported & required) == required, "contract.capabilities.fixture");
        store.ValidateRequirements(required);
        foreach (var bit in new[] { RuntimeStoreGuarantees.Durable, RuntimeStoreGuarantees.CanonicalSemanticEquality,
                     RuntimeStoreGuarantees.AtomicCompletionFollowUps, RuntimeStoreGuarantees.RestartSafe,
                     RuntimeStoreGuarantees.MultiHost, RuntimeStoreGuarantees.RealTimeClock })
        {
            if ((supported & bit) != 0) continue;
            var rejected = false;
            try { store.ValidateRequirements(bit); }
            catch (NotSupportedException) { rejected = true; }
            Check(rejected, $"contract.capabilities.rejected-{bit}");
        }
        var unknownRejected = false;
        try { store.ValidateRequirements((RuntimeStoreGuarantees)(1 << 29)); }
        catch (NotSupportedException) { unknownRejected = true; }
        Check(unknownRejected, "contract.capabilities.unknown");
    }

    private static void AdmissionAndPublication(Func<ContractFixture> factory)
    {
        var store = factory().Store;
        var invocation = Guid.NewGuid();
        var first = store.AcceptUnvalidated(Target("target"), invocation, "first-impl");
        Check(first.Kind == TargetedAcceptKind.Created && first.Record?.InvocationId == invocation, "contract.target.created");
        var again = store.AcceptUnvalidated(Target("target"), Guid.Empty, null);
        Check(again.Kind == TargetedAcceptKind.Existing && again.Record?.InvocationId == invocation &&
              again.Record.SelectedImplementation == "first-impl", "contract.target.retry");
        Check(store.AcceptUnvalidated(Target("target", [1, 9]), Guid.NewGuid(), "other").Kind == TargetedAcceptKind.Conflict,
            "contract.target.conflict");
        Check(store.ProbePublication(Publication("target")).Kind == SubmissionProbeKind.Conflict, "contract.target.shared-key");
        var publicationId = Guid.NewGuid();
        var intents = new[] { new PublicationIntent("sub-a", "actor-a", "impl-a"), new PublicationIntent("sub-b", "actor-b", "impl-b") };
        var pub = store.AcceptPublication(Publication("publication"), publicationId, intents);
        Check(pub.Kind == PublicationAcceptKind.Created && pub.Record?.PublicationId == publicationId &&
              pub.Record.Intents.SequenceEqual(intents), "contract.publication.created");
        var retry = store.AcceptPublication(Publication("publication"), Guid.Empty,
            [new PublicationIntent("changed", "actor-c", "impl-c")]);
        Check(retry.Kind == PublicationAcceptKind.Existing && retry.Record?.PublicationId == publicationId &&
              retry.Record.Intents.SequenceEqual(intents), "contract.publication.retry");
        Check(store.ProbeTargeted(Target("publication")).Kind == SubmissionProbeKind.Conflict, "contract.publication.shared-key");
        var empty = store.AcceptPublication(Publication("empty"), Guid.NewGuid(), []);
        Check(empty.Kind == PublicationAcceptKind.Created && empty.Record?.Intents.Count == 0, "contract.publication.empty");
    }

    private static void ParentAdmission(Func<ContractFixture> factory)
    {
        var fixture = factory(); var store = fixture.Store;
        var parent = Guid.NewGuid(); var attempt = Guid.NewGuid(); var token = Guid.NewGuid();
        Check(store.AcceptUnvalidated(Target("parent"), parent, "parent-impl").Kind == TargetedAcceptKind.Created,
            "contract.parent.accept");
        Check(store.Claim(App, "parent", parent, attempt, token, TimeSpan.FromSeconds(10)).Status == AttemptTransitionStatus.Applied,
            "contract.parent.claim");
        var child = new TargetedRequest(App, "child", TargetedOperation.Invoke, "child-target", [6], [7], [],
            parent.ToString("D"), attempt.ToString("D"));
        var childId = Guid.NewGuid();
        var admitted = store.AcceptOwnedInvoke(child, childId, "child-impl", "parent", parent, attempt, token);
        Check(admitted.Status == OwnedInvokeStatus.Created && admitted.Record?.InvocationId == childId,
            "contract.parent.child-created");
        var retry = store.AcceptOwnedInvoke(child, Guid.Empty, null, "parent", parent, attempt, token);
        Check(retry.Status == OwnedInvokeStatus.Existing && retry.Record?.InvocationId == childId &&
              retry.Record.SelectedImplementation == "child-impl", "contract.parent.child-pinned");
        Check(store.AcceptOwnedInvoke(child, Guid.Empty, null, "parent", parent, attempt, Guid.NewGuid()).Status == OwnedInvokeStatus.ParentUnavailable,
            "contract.parent.wrong-token");
        fixture.Advance(TimeSpan.FromSeconds(10));
        Check(store.AcceptOwnedInvoke(child, Guid.Empty, null, "parent", parent, attempt, token).Status == OwnedInvokeStatus.ParentUnavailable,
            "contract.parent.expired");
        Check(store.ProbeTargeted(child).Record?.InvocationId == childId, "contract.parent.probe-after-expiry");
    }

    private static async Task ConcurrentClaims(Func<ContractFixture> factory)
    {
        var store = factory().Store; var id = Guid.NewGuid();
        Check(store.AcceptUnvalidated(Target("claim-race"), id, "selected").Kind == TargetedAcceptKind.Created,
            "contract.claim.accept");
        using var barrier = new Barrier(2);
        var tasks = Enumerable.Range(0, 2).Select(_ => Task.Factory.StartNew(() =>
        {
            if (!barrier.SignalAndWait(Timeout)) throw new ContractScenarioViolation("contract.claim.timeout");
            return store.Claim(App, "claim-race", id, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromSeconds(10));
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
        var results = await Task.WhenAll(tasks).WaitAsync(Timeout);
        Check(results.Count(x => x.Status == AttemptTransitionStatus.Applied) == 1 &&
              results.Count(x => x.Status == AttemptTransitionStatus.NotEligible) == 1, "contract.claim.single-winner");
        var winner = results.Single(x => x.Status == AttemptTransitionStatus.Applied).Ownership!;
        var observed = store.GetOwnership(App, "claim-race", id);
        Check(observed?.State == TargetedAttemptState.Running && observed.AttemptId == winner.AttemptId && observed.Token == winner.Token,
            "contract.claim.retained-winner", observed);
    }

    public static void StaleObservation(Func<ContractFixture> factory)
    {
        var fixture = factory(); var store = fixture.Store;
        const string key = "stale-observation";
        var id = Guid.NewGuid(); var attempt = Guid.NewGuid(); var token = Guid.NewGuid();
        Check(store.AcceptUnvalidated(Target(key), id, "pinned").Kind == TargetedAcceptKind.Created, "contract.stale.accept");
        var first = store.Claim(App, key, id, attempt, token, TimeSpan.FromSeconds(10));
        Check(first.Status == AttemptTransitionStatus.Applied && first.Ownership?.LeaseUntil is not null, "contract.stale.claim", first.Ownership);
        var oldDeadline = first.Ownership!.LeaseUntil!.Value;
        fixture.Advance(TimeSpan.FromSeconds(9));
        var renewed = store.Renew(App, key, id, attempt, token, TimeSpan.FromSeconds(10));
        Check(renewed.Status == AttemptTransitionStatus.Applied && renewed.Ownership?.LeaseUntil == oldDeadline.AddSeconds(9),
            "contract.stale.renew", renewed.Ownership);
        var newDeadline = renewed.Ownership!.LeaseUntil!.Value;
        fixture.Advance(TimeSpan.FromSeconds(1));
        var before = store.RecoverExpiredAsRetry(App, key, id, attempt, token, oldDeadline, TimeSpan.Zero);
        var beforeSnapshot = store.GetOwnership(App, key, id);
        Check(beforeSnapshot?.State == TargetedAttemptState.Running && beforeSnapshot.LeaseUntil == newDeadline &&
              beforeSnapshot.RecoveryHistory.Count == 0, "contract.stale-before-mutated", beforeSnapshot);
        fixture.Advance(TimeSpan.FromSeconds(9));
        var after = store.RecoverExpiredAsRetry(App, key, id, attempt, token, oldDeadline, TimeSpan.Zero);
        var observed = store.GetOwnership(App, key, id);
        Check(observed?.State == TargetedAttemptState.Running && observed.AttemptId == attempt && observed.Token == token &&
              observed.LeaseUntil == newDeadline && observed.RecoveryHistory.Count == 0,
            "contract.stale-observation-mutated", observed);
        Check(before.Status == AttemptTransitionStatus.ObservationMismatch && after.Status == AttemptTransitionStatus.ObservationMismatch,
            "contract.stale-observation-status", observed);
        var recovery = store.RecoverExpiredAsRetry(App, key, id, attempt, token, newDeadline, TimeSpan.Zero);
        Check(recovery.Status == AttemptTransitionStatus.Applied && recovery.Ownership?.State == TargetedAttemptState.Eligible &&
              recovery.Ownership.RecoveryHistory.Count == 1 && recovery.Ownership.RecoveryHistory[0].Kind == AttemptRecoveryKind.LeaseExpired,
            "contract.stale.valid-recovery", recovery.Ownership);
        var fresh = store.Claim(App, key, id, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromSeconds(10));
        Check(fresh.Status == AttemptTransitionStatus.Applied, "contract.stale.reclaim", fresh.Ownership);
        var stale = store.Complete(App, key, id, attempt, token, new TargetedCompletion([8], [1]));
        Check(stale.Status == AttemptTransitionStatus.InvalidOwner, "contract.stale.old-owner-fenced", stale.Ownership);
    }

    private static void Cancellation(Func<ContractFixture> factory)
    {
        var store = factory().Store;
        var id = Guid.NewGuid(); var attempt = Guid.NewGuid(); var token = Guid.NewGuid();
        Check(store.AcceptUnvalidated(Target("cancel"), id, "pinned").Kind == TargetedAcceptKind.Created, "contract.cancel.accept");
        Check(store.Claim(App, "cancel", id, attempt, token, TimeSpan.FromSeconds(10)).Status == AttemptTransitionStatus.Applied,
            "contract.cancel.claim");
        Check(store.RequestCancellation(App, "cancel", id).Status == CancellationRequestStatus.Applied, "contract.cancel.request");
        byte[] bytes = [0, 69, 82, 82]; byte[] metadata = [2];
        var result = store.Complete(App, "cancel", id, attempt, token, new TargetedCompletion(bytes, metadata));
        bytes[0] = 99; metadata[0] = 99;
        var observed = store.GetOwnership(App, "cancel", id);
        Check(result.Status == AttemptTransitionStatus.CancellationFinalized && observed?.State == TargetedAttemptState.Cancelled &&
              observed.Completion is null && observed.ObservedCancellationExit?.Kind == CancellationExitKind.Normal &&
              observed.ObservedCancellationExit.Completion!.Payload.SequenceEqual(new byte[] { 0, 69, 82, 82 }) &&
              observed.ObservedCancellationExit.Completion.ContractMetadata.SequenceEqual(new byte[] { 2 }),
            "contract.cancel.observed-not-delivered", observed);
        Check(store.RequestCancellation(App, "cancel", id).Status == CancellationRequestStatus.AlreadyRequested,
            "contract.cancel.idempotent");
    }
}
