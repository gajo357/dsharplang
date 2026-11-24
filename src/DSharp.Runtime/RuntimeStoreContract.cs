namespace DSharp.Runtime;

// Provisional internal vocabulary for explicit provider guarantees. A flag is
// a statement by the provider, not evidence that a conformance test passed.
[Flags]
internal enum RuntimeStoreGuarantees
{
    None = 0,
    ByteExactComparison = 1 << 0,
    InProcessAtomicity = 1 << 1,
    SimulatedTime = 1 << 2,
    Durable = 1 << 3,
    CanonicalSemanticEquality = 1 << 4,
    AtomicCompletionFollowUps = 1 << 5,
    RestartSafe = 1 << 6,
    MultiHost = 1 << 7,
    RealTimeClock = 1 << 8,
    BlockedPinnedImplementation = 1 << 9
}

internal readonly record struct RuntimeStoreCapabilities(RuntimeStoreGuarantees Guarantees);

/// <summary>
/// Provisional internal storage boundary. Its synchronous signatures and DTOs
/// are not a finalized provider protocol, public API, dispatcher or authorization
/// boundary. Providers must reject requested guarantees they do not provide.
/// </summary>
/// <remarks>
/// Each operation is an atomic provider boundary. A request-aware probe must
/// distinguish absence, an exact retained match and a shared-key conflict before
/// host reselection. Acceptance rechecks atomically; a probe reserves nothing.
/// Targeted and publication submissions share the application/submission key.
/// The accepted payload, metadata, target, selection and publication intents are
/// retained as immutable snapshots. Callers and readers do not own store buffers.
///
/// <see cref="AcceptUnvalidated"/> retains and compares parent strings but does
/// not validate an actor parent, authorize a caller or prove a canonical codec.
/// <see cref="AcceptOwnedInvoke"/> checks its exact current parent attempt and
/// inserts the child in one boundary; it is not a completion/follow-up commit.
/// Claim, renewal, outcome, retry and recovery serialize against ownership and
/// the provider's declared time source. An expired or superseded owner cannot
/// write; a cancellation request that wins precedes later fenced exit/recovery,
/// while an earlier terminal outcome remains terminal. These guarantees never
/// imply that stale external code has stopped or its effects were rolled back.
///
/// Host validation, authorization, selection, actor execution and other external
/// work must not run inside a provider transaction. Enumerating caller-provided
/// publication intents may execute host code: materialize outside a provider's
/// critical section and recheck admission when committing. An acceptance result
/// from a volatile provider is not a durable acknowledgement; a durable provider
/// may acknowledge only after the commit required by its declared guarantee.
/// </remarks>
internal interface IRuntimeStore
{
    RuntimeStoreCapabilities Capabilities { get; }
    TimeSpan RetentionHorizon { get; }
    void ValidateRequirements(RuntimeStoreGuarantees required);

    TargetedProbeResult ProbeTargeted(TargetedRequest request);
    PublicationProbeResult ProbePublication(PublicationRequest request);
    TargetedRecord? Lookup(string applicationId, string submissionId);
    PublicationRecord? LookupPublication(string applicationId, string submissionId);
    TargetedAcceptResult AcceptUnvalidated(TargetedRequest request, Guid invocationId, string? selectedImplementation);
    PublicationAcceptResult AcceptPublication(PublicationRequest request, Guid publicationId, IEnumerable<PublicationIntent>? intents);
    OwnedInvokeResult AcceptOwnedInvoke(TargetedRequest request, Guid childInvocationId, string? selectedImplementation,
        string parentSubmissionId, Guid parentInvocationId, Guid parentAttemptId, Guid parentToken);

    TargetedOwnership? GetOwnership(string applicationId, string submissionId, Guid invocationId);
    // Providers that do not advertise BlockedPinnedImplementation must reject
    // these optional operations rather than silently ignoring a block request.
    AttemptTransitionResult BlockEligible(string applicationId, string submissionId, Guid invocationId,
        long expectedEligibilityVersion, string expectedSelectedImplementation, Guid blockId, BlockReason reason) =>
        throw new NotSupportedException("Blocked pinned implementations are not supported.");
    AttemptTransitionResult Unblock(string applicationId, string submissionId, Guid invocationId,
        Guid expectedBlockId, string expectedSelectedImplementation) =>
        throw new NotSupportedException("Blocked pinned implementations are not supported.");
    AttemptTransitionResult Claim(string applicationId, string submissionId, Guid invocationId, Guid attemptId, Guid token, TimeSpan duration);
    AttemptTransitionResult Renew(string applicationId, string submissionId, Guid invocationId, Guid attemptId, Guid token, TimeSpan duration);
    AttemptTransitionResult Complete(string applicationId, string submissionId, Guid invocationId, Guid attemptId, Guid token, TargetedCompletion completion);
    AttemptTransitionResult FailAfterFault(string applicationId, string submissionId, Guid invocationId, Guid attemptId, Guid token, ReportedTerminalFault fault);
    AttemptTransitionResult RescheduleAfterFault(string applicationId, string submissionId, Guid invocationId, Guid attemptId, Guid token, TimeSpan delay, TargetedFault fault);
    AttemptTransitionResult RecoverExpiredAsRetry(string applicationId, string submissionId, Guid invocationId, Guid observedAttemptId,
        Guid observedToken, DateTimeOffset observedLeaseUntil, TimeSpan delay);
    AttemptTransitionResult RecoverExpiredAsFailed(string applicationId, string submissionId, Guid invocationId, Guid observedAttemptId,
        Guid observedToken, DateTimeOffset observedLeaseUntil);
    CancellationRequestResult RequestCancellation(string applicationId, string submissionId, Guid invocationId);
}

internal sealed partial class VolatileTargetedStore : IRuntimeStore
{
    private const RuntimeStoreGuarantees SupportedContractGuarantees =
        RuntimeStoreGuarantees.ByteExactComparison | RuntimeStoreGuarantees.InProcessAtomicity | RuntimeStoreGuarantees.SimulatedTime |
        RuntimeStoreGuarantees.BlockedPinnedImplementation;
    private const RuntimeStoreGuarantees KnownContractGuarantees = SupportedContractGuarantees |
        RuntimeStoreGuarantees.Durable | RuntimeStoreGuarantees.CanonicalSemanticEquality |
        RuntimeStoreGuarantees.AtomicCompletionFollowUps | RuntimeStoreGuarantees.RestartSafe |
        RuntimeStoreGuarantees.MultiHost | RuntimeStoreGuarantees.RealTimeClock;

    public RuntimeStoreCapabilities Capabilities => new(SupportedContractGuarantees);

    public void ValidateRequirements(RuntimeStoreGuarantees required)
    {
        if ((required & ~KnownContractGuarantees) != 0 || (required & ~SupportedContractGuarantees) != 0)
            throw new NotSupportedException("The volatile reference does not support the requested RuntimeStore guarantees.");
    }

    TargetedAcceptResult IRuntimeStore.AcceptUnvalidated(TargetedRequest request, Guid invocationId, string? selectedImplementation) =>
        Accept(request, invocationId, selectedImplementation);
}
