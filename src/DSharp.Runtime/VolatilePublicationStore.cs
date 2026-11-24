using System.Runtime.ExceptionServices;

namespace DSharp.Runtime;

/// <summary>
/// Copied, byte-exact reference input. Contract metadata must include encoding
/// and schema identity, and options must include every acceptance-affecting
/// option. This is not a canonical codec or host validation.
/// </summary>
internal sealed class PublicationRequest
{
    private readonly byte[] payload;
    private readonly byte[] contractMetadata;
    private readonly byte[] behaviorOptions;

    public PublicationRequest(string applicationId, string submissionId, byte[] payload,
        byte[] contractMetadata, byte[] behaviorOptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(submissionId);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(contractMetadata);
        ArgumentNullException.ThrowIfNull(behaviorOptions);
        if (contractMetadata.Length == 0)
            throw new ArgumentException("Contract metadata must be supplied.", nameof(contractMetadata));
        ApplicationId = applicationId;
        SubmissionId = submissionId;
        this.payload = (byte[])payload.Clone();
        this.contractMetadata = (byte[])contractMetadata.Clone();
        this.behaviorOptions = (byte[])behaviorOptions.Clone();
    }

    public string ApplicationId { get; }
    public string SubmissionId { get; }
    public byte[] Payload => (byte[])payload.Clone();
    public byte[] ContractMetadata => (byte[])contractMetadata.Clone();
    public byte[] BehaviorOptions => (byte[])behaviorOptions.Clone();

    internal bool SameRequest(PublicationRequest other) =>
        string.Equals(ApplicationId, other.ApplicationId, StringComparison.Ordinal) &&
        string.Equals(SubmissionId, other.SubmissionId, StringComparison.Ordinal) &&
        payload.AsSpan().SequenceEqual(other.payload) &&
        contractMetadata.AsSpan().SequenceEqual(other.contractMetadata) &&
        behaviorOptions.AsSpan().SequenceEqual(other.behaviorOptions);
}

internal sealed record PublicationIntent(string SubscriptionId, string PublicTarget, string SelectedImplementation);

internal sealed class PublicationRecord
{
    internal PublicationRecord(PublicationRequest request, Guid publicationId, PublicationIntent[] intents)
    {
        Request = request;
        PublicationId = publicationId;
        Intents = Array.AsReadOnly((PublicationIntent[])intents.Clone());
    }

    public PublicationRequest Request { get; }
    public Guid PublicationId { get; }
    // Each immutable intent has stable delivery identity (PublicationId, SubscriptionId).
    public IReadOnlyList<PublicationIntent> Intents { get; }
}

internal enum PublicationAcceptKind { Created, Existing, Conflict, PublicationIdCollision }
internal sealed record PublicationAcceptResult(PublicationAcceptKind Kind, PublicationRecord? Record);
internal sealed record PublicationProbeResult(SubmissionProbeKind Kind, PublicationRecord? Record);

// An explicitly invoked conformance seam for this in-memory reference only.
internal enum PublicationFailurePoint { None, AfterIdentityReservation }
internal sealed class PublicationInjectedFailureException : Exception
{
    public PublicationInjectedFailureException() : base("Injected publication failure after identity reservation.") { }
}

internal sealed partial class VolatileTargetedStore
{
    private readonly Dictionary<(string ApplicationId, string SubmissionId), PublicationRecord> publications = new();
    private readonly HashSet<Guid> publicationIds = new();

    public PublicationProbeResult ProbePublication(PublicationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (gate)
        {
            var key = (request.ApplicationId, request.SubmissionId);
            var result = FindPublicationResult(key, request);
            return result is null
                ? new(SubmissionProbeKind.Absent, null)
                : result.Kind == PublicationAcceptKind.Existing
                    ? new(SubmissionProbeKind.Existing, result.Record)
                    : new(SubmissionProbeKind.Conflict, null);
        }
    }

    // A kind-specific lookup only: full host authorization and orchestration are outside this model.
    public PublicationRecord? LookupPublication(string applicationId, string submissionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(submissionId);
        lock (gate)
            return publications.GetValueOrDefault((applicationId, submissionId));
    }

    public PublicationAcceptResult AcceptPublication(PublicationRequest request, Guid publicationId,
        IEnumerable<PublicationIntent>? intents) =>
        AcceptPublicationCore(request, publicationId, intents, PublicationFailurePoint.None);

    // This entry point is a deterministic conformance probe, not a production failure policy.
    public PublicationAcceptResult AcceptPublicationForConformance(PublicationRequest request, Guid publicationId,
        IEnumerable<PublicationIntent>? intents, PublicationFailurePoint failurePoint)
    {
        if (failurePoint is not (PublicationFailurePoint.None or PublicationFailurePoint.AfterIdentityReservation))
            throw new ArgumentOutOfRangeException(nameof(failurePoint));
        return AcceptPublicationCore(request, publicationId, intents, failurePoint);
    }

    private PublicationAcceptResult AcceptPublicationCore(PublicationRequest request, Guid publicationId,
        IEnumerable<PublicationIntent>? intents, PublicationFailurePoint failurePoint)
    {
        ArgumentNullException.ThrowIfNull(request);
        var key = (request.ApplicationId, request.SubmissionId);
        lock (gate)
        {
            var existing = FindPublicationResult(key, request);
            if (existing is not null)
                return existing;
        }

        // Enumerating a host-supplied sequence may run arbitrary host code. Never
        // do that under the gate. Check the key again after making a snapshot.
        PublicationIntent[]? snapshot = null;
        ExceptionDispatchInfo? failure = null;
        try
        {
            if (publicationId == Guid.Empty)
                throw new ArgumentException("A nonempty publication identity is required for a new record.", nameof(publicationId));
            ArgumentNullException.ThrowIfNull(intents);
            var items = new List<PublicationIntent>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var intent in intents)
            {
                if (intent is null)
                    throw new ArgumentException("Publication intents cannot contain null.", nameof(intents));
                ArgumentException.ThrowIfNullOrWhiteSpace(intent.SubscriptionId);
                ArgumentException.ThrowIfNullOrWhiteSpace(intent.PublicTarget);
                ArgumentException.ThrowIfNullOrWhiteSpace(intent.SelectedImplementation);
                if (!seen.Add(intent.SubscriptionId))
                    throw new ArgumentException("Subscription identities must be unique.", nameof(intents));
                items.Add(intent);
            }
            snapshot = items.ToArray();
        }
        catch (Exception error)
        {
            failure = ExceptionDispatchInfo.Capture(error);
        }

        lock (gate)
        {
            var existing = FindPublicationResult(key, request);
            if (existing is not null)
                return existing;
            if (failure is null)
            {
                if (publicationIds.Contains(publicationId))
                    return new(PublicationAcceptKind.PublicationIdCollision, null);
                var record = new PublicationRecord(request, publicationId, snapshot!);
                publicationIds.Add(publicationId);
                try
                {
                    if (failurePoint == PublicationFailurePoint.AfterIdentityReservation)
                        throw new PublicationInjectedFailureException();
                    publications.Add(key, record);
                }
                catch
                {
                    publicationIds.Remove(publicationId);
                    throw;
                }
                return new(PublicationAcceptKind.Created, record);
            }
        }
        failure.Throw();
        throw new InvalidOperationException("Unreachable");
    }

    // Called only while holding gate; neither this method nor stored records run host callbacks.
    private PublicationAcceptResult? FindPublicationResult(
        (string ApplicationId, string SubmissionId) key, PublicationRequest request)
    {
        if (submissions.ContainsKey(key))
            return new(PublicationAcceptKind.Conflict, null);
        if (!publications.TryGetValue(key, out var existing))
            return null;
        return request.SameRequest(existing.Request)
            ? new(PublicationAcceptKind.Existing, existing)
            : new(PublicationAcceptKind.Conflict, null);
    }
}
