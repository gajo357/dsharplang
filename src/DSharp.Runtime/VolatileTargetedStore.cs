using System.Runtime.CompilerServices;

// The provisional reference model is exercised by the existing runtime test
// assembly. This is not a production provider or an authorization boundary.
[assembly: InternalsVisibleTo("DSR")]

namespace DSharp.Runtime;

internal enum TargetedOperation
{
    Invoke,
    Dispatch
}

[Flags]
internal enum VolatileStoreRequirements
{
    None = 0,
    Durability = 1,
    AtomicFollowUps = 2,
    CanonicalSemanticEquality = 4
}

internal enum TargetedAcceptKind
{
    Created,
    Existing,
    Conflict,
    InvocationIdCollision
}

/// <summary>
/// A copied, byte-comparable reference request. Byte equality is not proof of
/// canonical semantic equality; callers supply the complete comparable request
/// representation, including encoding and schema identity in contract metadata
/// and all acceptance-affecting options. Encoding, authorization and host
/// validation remain separate. The optional public target key is caller-supplied
/// opaque data; its presence does not define a codec or an ordering policy.
/// </summary>
internal sealed class TargetedRequest
{
    private readonly byte[] payload;
    private readonly byte[] contractMetadata;
    private readonly byte[] behaviorOptions;
    private readonly byte[]? publicTargetKey;

    public TargetedRequest(string applicationId, string submissionId, TargetedOperation operation,
        string requestedTarget, byte[] payload, byte[] contractMetadata, byte[] behaviorOptions,
        string? parentInvocationId = null, string? parentAttemptId = null)
        : this(applicationId, submissionId, operation, requestedTarget, payload, contractMetadata,
            behaviorOptions, parentInvocationId, parentAttemptId, null) { }

    public TargetedRequest(string applicationId, string submissionId, TargetedOperation operation,
        string requestedTarget, byte[] payload, byte[] contractMetadata, byte[] behaviorOptions,
        string? parentInvocationId, string? parentAttemptId, byte[]? publicTargetKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(submissionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedTarget);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(contractMetadata);
        ArgumentNullException.ThrowIfNull(behaviorOptions);
        if (operation is not (TargetedOperation.Invoke or TargetedOperation.Dispatch))
            throw new ArgumentOutOfRangeException(nameof(operation));
        if (contractMetadata.Length == 0)
            throw new ArgumentException("Contract metadata must be supplied.", nameof(contractMetadata));
        if ((parentInvocationId is null) != (parentAttemptId is null))
            throw new ArgumentException("Parent invocation and parent attempt must be supplied together.");
        if (parentInvocationId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(parentInvocationId);
            ArgumentException.ThrowIfNullOrWhiteSpace(parentAttemptId);
            if (operation == TargetedOperation.Dispatch)
                throw new ArgumentException("Targeted Dispatch is an independent root and cannot have an awaited parent.");
        }
        ApplicationId = applicationId;
        SubmissionId = submissionId;
        Operation = operation;
        RequestedTarget = requestedTarget;
        ParentInvocationId = parentInvocationId;
        ParentAttemptId = parentAttemptId;
        this.payload = (byte[])payload.Clone();
        this.contractMetadata = (byte[])contractMetadata.Clone();
        this.behaviorOptions = (byte[])behaviorOptions.Clone();
        this.publicTargetKey = publicTargetKey is null ? null : (byte[])publicTargetKey.Clone();
    }

    public string ApplicationId { get; }
    public string SubmissionId { get; }
    public TargetedOperation Operation { get; }
    public string RequestedTarget { get; }
    public string? ParentInvocationId { get; }
    public string? ParentAttemptId { get; }
    public byte[] Payload => (byte[])payload.Clone();
    public byte[] ContractMetadata => (byte[])contractMetadata.Clone();
    public byte[] BehaviorOptions => (byte[])behaviorOptions.Clone();
    public byte[]? PublicTargetKey => publicTargetKey is null ? null : (byte[])publicTargetKey.Clone();

    internal bool SameRequest(TargetedRequest other) =>
        string.Equals(ApplicationId, other.ApplicationId, StringComparison.Ordinal) &&
        string.Equals(SubmissionId, other.SubmissionId, StringComparison.Ordinal) &&
        Operation == other.Operation &&
        string.Equals(RequestedTarget, other.RequestedTarget, StringComparison.Ordinal) &&
        string.Equals(ParentInvocationId, other.ParentInvocationId, StringComparison.Ordinal) &&
        string.Equals(ParentAttemptId, other.ParentAttemptId, StringComparison.Ordinal) &&
        ((publicTargetKey is null && other.publicTargetKey is null) ||
         (publicTargetKey is not null && other.publicTargetKey is not null && publicTargetKey.AsSpan().SequenceEqual(other.publicTargetKey))) &&
        payload.AsSpan().SequenceEqual(other.payload) &&
        contractMetadata.AsSpan().SequenceEqual(other.contractMetadata) &&
        behaviorOptions.AsSpan().SequenceEqual(other.behaviorOptions);
}

internal sealed record TargetedRecord(TargetedRequest Request, Guid InvocationId, string SelectedImplementation);
internal sealed record TargetedAcceptResult(TargetedAcceptKind Kind, TargetedRecord? Record);
internal enum SubmissionProbeKind { Absent, Existing, Conflict }
internal sealed record TargetedProbeResult(SubmissionProbeKind Kind, TargetedRecord? Record);

/// <summary>
/// Provisional single-process reference bookkeeping; records live only until
/// this object is discarded. No receipt, durable acknowledgement, selection,
/// authorization, codec, retention expiry or multi-record transaction is supplied.
/// </summary>
internal sealed partial class VolatileTargetedStore
{
    private readonly object gate = new();
    private readonly Dictionary<(string ApplicationId, string SubmissionId), TargetedRecord> submissions = new();
    private readonly HashSet<Guid> invocationIds = new();

    public VolatileTargetedStore(TimeSpan retentionHorizon, VolatileStoreRequirements requirements = VolatileStoreRequirements.None)
    {
        if (retentionHorizon <= TimeSpan.Zero || retentionHorizon == TimeSpan.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(retentionHorizon), "A finite positive horizon is required.");
        if (requirements != VolatileStoreRequirements.None)
            throw new NotSupportedException("This volatile reference does not support requested provider capabilities.");
        RetentionHorizon = retentionHorizon;
    }

    public TimeSpan RetentionHorizon { get; }
    public bool IsDurable => false;
    public bool SupportsAtomicFollowUps => false;

    // This request-aware probe shares the entire submission namespace, so a
    // host can discover a cross-operation conflict before doing any selection.
    // Authorization and complete host orchestration remain outside this model.
    public TargetedProbeResult ProbeTargeted(TargetedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (gate)
        {
            var key = (request.ApplicationId, request.SubmissionId);
            if (publications.ContainsKey(key))
                return new(SubmissionProbeKind.Conflict, null);
            if (!submissions.TryGetValue(key, out var existing))
                return new(SubmissionProbeKind.Absent, null);
            return request.SameRequest(existing.Request)
                ? new(SubmissionProbeKind.Existing, existing)
                : new(SubmissionProbeKind.Conflict, null);
        }
    }

    // Kind-specific record lookup; use ProbeTargeted for shared-key conflict
    // detection before selection. Authorization and orchestration remain outside.
    public TargetedRecord? Lookup(string applicationId, string submissionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(submissionId);
        lock (gate)
            return submissions.GetValueOrDefault((applicationId, submissionId));
    }

    public TargetedAcceptResult Accept(TargetedRequest request, Guid invocationId, string? selectedImplementation)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (gate)
        {
            var key = (request.ApplicationId, request.SubmissionId);
            if (publications.ContainsKey(key))
                return new(TargetedAcceptKind.Conflict, null);
            if (submissions.TryGetValue(key, out var existing))
                return request.SameRequest(existing.Request)
                    ? new(TargetedAcceptKind.Existing, existing)
                    : new(TargetedAcceptKind.Conflict, null);

            if (invocationId == Guid.Empty)
                throw new ArgumentException("A nonempty invocation identity is required for a new record.", nameof(invocationId));
            ArgumentException.ThrowIfNullOrWhiteSpace(selectedImplementation);
            if (invocationIds.Contains(invocationId) || usedOwnershipIds.Contains(invocationId))
                return new(TargetedAcceptKind.InvocationIdCollision, null);

            var record = new TargetedRecord(request, invocationId, selectedImplementation);
            invocationIds.Add(invocationId);
            try
            {
                submissions.Add(key, record);
            }
            catch
            {
                invocationIds.Remove(invocationId);
                throw;
            }
            return new(TargetedAcceptKind.Created, record);
        }
    }
}
