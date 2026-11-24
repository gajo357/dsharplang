namespace DSharp.Runtime;

internal enum OwnedInvokeStatus
{
    Created,
    Existing,
    Conflict,
    InvocationIdCollision,
    ParentUnavailable
}

internal sealed record OwnedInvokeResult(OwnedInvokeStatus Status, TargetedRecord? Record);

internal sealed partial class VolatileTargetedStore
{
    // This opt-in volatile reference path fences a parent before accepting an
    // awaited child. Raw Accept remains unvalidated bookkeeping; neither path
    // provides authorization, a codec, durability or atomic follow-up commits.
    public OwnedInvokeResult AcceptOwnedInvoke(TargetedRequest request, Guid childInvocationId,
        string? selectedImplementation, string parentSubmissionId, Guid parentInvocationId,
        Guid parentAttemptId, Guid parentToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSubmissionKey(request.ApplicationId, parentSubmissionId);
        ValidateIdentity(parentInvocationId, nameof(parentInvocationId));
        ValidateIdentity(parentAttemptId, nameof(parentAttemptId));
        ValidateIdentity(parentToken, nameof(parentToken));
        if (request.Operation != TargetedOperation.Invoke ||
            !string.Equals(request.ParentInvocationId, parentInvocationId.ToString("D"), StringComparison.Ordinal) ||
            !string.Equals(request.ParentAttemptId, parentAttemptId.ToString("D"), StringComparison.Ordinal))
            throw new ArgumentException("An owned Invoke must carry the canonical parent invocation and attempt identities.", nameof(request));

        lock (gate)
        {
            var now = GetClock();
            var parent = FindOwnership(request.ApplicationId, parentSubmissionId, parentInvocationId);
            if (parent is null || parent.State != TargetedAttemptState.Running ||
                parent.AttemptId != parentAttemptId || parent.Token != parentToken ||
                now >= parent.LeaseUntil!.Value)
                return new(OwnedInvokeStatus.ParentUnavailable, null);

            // Monitor locks are reentrant. Acceptance and parent fencing remain
            // one atomic operation under the same gate; Accept invokes no host code.
            var accepted = Accept(request, childInvocationId, selectedImplementation);
            var status = accepted.Kind switch
            {
                TargetedAcceptKind.Created => OwnedInvokeStatus.Created,
                TargetedAcceptKind.Existing => OwnedInvokeStatus.Existing,
                TargetedAcceptKind.Conflict => OwnedInvokeStatus.Conflict,
                TargetedAcceptKind.InvocationIdCollision => OwnedInvokeStatus.InvocationIdCollision,
                _ => throw new InvalidOperationException($"Unexpected targeted acceptance: {accepted.Kind}")
            };
            return new(status, accepted.Record);
        }
    }
}
