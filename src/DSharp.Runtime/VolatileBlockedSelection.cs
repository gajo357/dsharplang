namespace DSharp.Runtime;

internal sealed partial class VolatileTargetedStore
{
    // The host reports readiness; this provisional reference does not perform
    // authorization, check compatibility or choose a replacement implementation.
    public AttemptTransitionResult BlockEligible(string applicationId, string submissionId, Guid invocationId,
        long expectedEligibilityVersion, string expectedSelectedImplementation, Guid blockId, BlockReason reason)
    {
        ValidateSubmissionKey(applicationId, submissionId);
        ValidateIdentity(invocationId, nameof(invocationId));
        ValidateIdentity(blockId, nameof(blockId));
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSelectedImplementation);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedEligibilityVersion);
        if (reason is not (BlockReason.ImplementationUnavailable or BlockReason.Incompatible))
            throw new ArgumentOutOfRangeException(nameof(reason));

        lock (gate)
        {
            _ = GetClock();
            var current = FindOwnership(applicationId, submissionId, invocationId);
            if (current is null)
                return new(AttemptTransitionStatus.NotFound, null);
            if (current.State != TargetedAttemptState.Eligible)
                return new(AttemptTransitionStatus.NotEligible, current);
            if (current.EligibilityVersion != expectedEligibilityVersion ||
                !string.Equals(current.Invocation.SelectedImplementation, expectedSelectedImplementation, StringComparison.Ordinal))
                return new(AttemptTransitionStatus.ObservationMismatch, current);
            if (invocationIds.Contains(blockId) || usedOwnershipIds.Contains(blockId))
                return new(AttemptTransitionStatus.IdentityCollision, current);

            var next = current with
            {
                State = TargetedAttemptState.Blocked,
                Block = new TargetedBlock(blockId, current.Invocation.SelectedImplementation, reason)
            };
            usedOwnershipIds.Add(blockId);
            try { ownerships[invocationId] = next; }
            catch { usedOwnershipIds.Remove(blockId); throw; }
            return new(AttemptTransitionStatus.Applied, next);
        }
    }

    // Recovery is an explicit host decision for the same pinned selection.
    // The block identity fences stale unblock observations across ABA cycles.
    public AttemptTransitionResult Unblock(string applicationId, string submissionId, Guid invocationId,
        Guid expectedBlockId, string expectedSelectedImplementation)
    {
        ValidateSubmissionKey(applicationId, submissionId);
        ValidateIdentity(invocationId, nameof(invocationId));
        ValidateIdentity(expectedBlockId, nameof(expectedBlockId));
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSelectedImplementation);

        lock (gate)
        {
            _ = GetClock();
            var current = FindOwnership(applicationId, submissionId, invocationId);
            if (current is null)
                return new(AttemptTransitionStatus.NotFound, null);
            if (current.State != TargetedAttemptState.Blocked)
                return new(AttemptTransitionStatus.NotEligible, current);
            if (current.Block?.BlockId != expectedBlockId ||
                !string.Equals(current.Invocation.SelectedImplementation, expectedSelectedImplementation, StringComparison.Ordinal))
                return new(AttemptTransitionStatus.ObservationMismatch, current);
            var next = current with
            {
                State = TargetedAttemptState.Eligible,
                Block = null,
                EligibilityVersion = checked(current.EligibilityVersion + 1)
            };
            ownerships[invocationId] = next;
            return new(AttemptTransitionStatus.Applied, next);
        }
    }
}
