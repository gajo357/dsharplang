namespace DSharp.Runtime;

// Provisional single-process state only; failure means the lease expired, not
// that the actor threw or that a stale process stopped executing.
internal enum TargetedAttemptState { Eligible, Running, FailedLeaseExpired, Completed }

// Opaque bytes only. Supplying metadata is not a codec or schema compatibility check.
internal sealed class TargetedCompletion
{
    private readonly byte[] payload;
    private readonly byte[] contractMetadata;

    public TargetedCompletion(byte[] payload, byte[] contractMetadata)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(contractMetadata);
        if (contractMetadata.Length == 0)
            throw new ArgumentException("Result contract metadata must be supplied.", nameof(contractMetadata));
        this.payload = (byte[])payload.Clone();
        this.contractMetadata = (byte[])contractMetadata.Clone();
    }

    public byte[] Payload => (byte[])payload.Clone();
    public byte[] ContractMetadata => (byte[])contractMetadata.Clone();
}

// Caller-reported opaque data; this is neither a normalized diagnostic nor
// evidence of an actor fault origin. The caller selects any retry policy.
internal sealed class TargetedFault
{
    private readonly byte[] payload;
    private readonly byte[] contractMetadata;

    public TargetedFault(byte[] payload, byte[] contractMetadata)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(contractMetadata);
        if (contractMetadata.Length == 0)
            throw new ArgumentException("Fault contract metadata must be supplied.", nameof(contractMetadata));
        this.payload = (byte[])payload.Clone();
        this.contractMetadata = (byte[])contractMetadata.Clone();
    }

    public byte[] Payload => (byte[])payload.Clone();
    public byte[] ContractMetadata => (byte[])contractMetadata.Clone();
}

internal sealed record RescheduledAttempt(Guid AttemptId, Guid Token, DateTimeOffset LeaseUntil,
    DateTimeOffset EligibleAt, TargetedFault Fault);

internal sealed record TargetedOwnership(TargetedRecord Invocation, TargetedAttemptState State,
    Guid? AttemptId, Guid? Token, DateTimeOffset? LeaseUntil, TargetedCompletion? Completion = null)
{
    public DateTimeOffset? EligibleAt { get; init; }
    // Only the store supplies this collection: it never exposes its mutable backing array.
    public IReadOnlyList<RescheduledAttempt> History { get; init; } = Array.Empty<RescheduledAttempt>();
}
internal enum AttemptTransitionStatus
{
    Applied, NotFound, NotEligible, InvalidOwner, Expired, ObservationMismatch,
    NotExpired, IdentityCollision, NotExtended, NotYetEligible
}
internal sealed record AttemptTransitionResult(AttemptTransitionStatus Status, TargetedOwnership? Ownership);

internal sealed partial class VolatileTargetedStore
{
    private DateTimeOffset? simulatedTime;
    private readonly Dictionary<Guid, TargetedOwnership> ownerships = new();
    // Shared across both roles, so no prior attempt ID can later become a token or vice versa.
    private readonly HashSet<Guid> usedOwnershipIds = new();

    // This clock exists only for deterministic reference bookkeeping. It has no
    // real-time, persistence, restart or provider clock-jump guarantee.
    public void InitializeClock(DateTimeOffset initial)
    {
        lock (gate)
        {
            if (simulatedTime.HasValue)
                throw new InvalidOperationException("The simulated clock is already initialized.");
            simulatedTime = initial;
        }
    }

    public void AdvanceClock(DateTimeOffset now)
    {
        lock (gate)
        {
            var previous = GetClock();
            if (now < previous)
                throw new ArgumentOutOfRangeException(nameof(now), "The simulated clock cannot move backwards.");
            simulatedTime = now;
        }
    }

    public DateTimeOffset CurrentTime
    {
        get { lock (gate) return GetClock(); }
    }

    public TargetedOwnership? GetOwnership(string applicationId, string submissionId, Guid invocationId)
    {
        ValidateSubmissionKey(applicationId, submissionId);
        ValidateIdentity(invocationId, nameof(invocationId));
        lock (gate)
        {
            _ = GetClock();
            return FindOwnership(applicationId, submissionId, invocationId);
        }
    }

    public AttemptTransitionResult Claim(string applicationId, string submissionId, Guid invocationId, Guid attemptId, Guid token, TimeSpan duration)
    {
        ValidateSubmissionKey(applicationId, submissionId);
        ValidateIdentity(invocationId, nameof(invocationId));
        ValidateIdentity(attemptId, nameof(attemptId));
        ValidateIdentity(token, nameof(token));
        ValidateDuration(duration);
        lock (gate)
        {
            var now = GetClock();
            var current = FindOwnership(applicationId, submissionId, invocationId);
            if (current is null)
                return new(AttemptTransitionStatus.NotFound, null);
            if (current.State != TargetedAttemptState.Eligible)
                return new(AttemptTransitionStatus.NotEligible, current);
            if (current.EligibleAt is { } due && now < due)
                return new(AttemptTransitionStatus.NotYetEligible, current);
            if (attemptId == token || invocationIds.Contains(attemptId) || invocationIds.Contains(token) ||
                usedOwnershipIds.Contains(attemptId) || usedOwnershipIds.Contains(token))
                return new(AttemptTransitionStatus.IdentityCollision, current);
            var deadline = AddLease(now, duration);
            var next = new TargetedOwnership(current.Invocation, TargetedAttemptState.Running, attemptId, token, deadline)
            {
                History = current.History
            };
            // Roll back both identity reservations if any subsequent insertion fails.
            usedOwnershipIds.Add(attemptId);
            try
            {
                usedOwnershipIds.Add(token);
                try { ownerships[invocationId] = next; }
                catch { usedOwnershipIds.Remove(token); throw; }
            }
            catch { usedOwnershipIds.Remove(attemptId); throw; }
            return new(AttemptTransitionStatus.Applied, next);
        }
    }

    public AttemptTransitionResult Renew(string applicationId, string submissionId, Guid invocationId, Guid attemptId, Guid token, TimeSpan duration)
    {
        ValidateSubmissionKey(applicationId, submissionId);
        ValidateIdentity(invocationId, nameof(invocationId));
        ValidateIdentity(attemptId, nameof(attemptId));
        ValidateIdentity(token, nameof(token));
        ValidateDuration(duration);
        lock (gate)
        {
            var now = GetClock();
            var current = FindOwnership(applicationId, submissionId, invocationId);
            if (current is null)
                return new(AttemptTransitionStatus.NotFound, null);
            if (current.State != TargetedAttemptState.Running)
                return new(AttemptTransitionStatus.NotEligible, current);
            if (current.AttemptId != attemptId || current.Token != token)
                return new(AttemptTransitionStatus.InvalidOwner, current);
            if (now >= current.LeaseUntil!.Value)
                return new(AttemptTransitionStatus.Expired, current);
            var deadline = AddLease(now, duration);
            if (deadline <= current.LeaseUntil.Value)
                return new(AttemptTransitionStatus.NotExtended, current);
            var next = current with { LeaseUntil = deadline };
            ownerships[invocationId] = next;
            return new(AttemptTransitionStatus.Applied, next);
        }
    }

    public AttemptTransitionResult Complete(string applicationId, string submissionId, Guid invocationId,
        Guid attemptId, Guid token, TargetedCompletion completion)
    {
        ValidateSubmissionKey(applicationId, submissionId);
        ValidateIdentity(invocationId, nameof(invocationId));
        ValidateIdentity(attemptId, nameof(attemptId));
        ValidateIdentity(token, nameof(token));
        ArgumentNullException.ThrowIfNull(completion);
        lock (gate)
        {
            var now = GetClock();
            var current = FindOwnership(applicationId, submissionId, invocationId);
            if (current is null)
                return new(AttemptTransitionStatus.NotFound, null);
            if (current.State != TargetedAttemptState.Running)
                return new(AttemptTransitionStatus.NotEligible, current);
            if (current.AttemptId != attemptId || current.Token != token)
                return new(AttemptTransitionStatus.InvalidOwner, current);
            if (now >= current.LeaseUntil!.Value)
                return new(AttemptTransitionStatus.Expired, current);
            var next = current with { State = TargetedAttemptState.Completed, Completion = completion };
            ownerships[invocationId] = next;
            return new(AttemptTransitionStatus.Applied, next);
        }
    }

    public AttemptTransitionResult RescheduleAfterFault(string applicationId, string submissionId, Guid invocationId,
        Guid attemptId, Guid token, TimeSpan delay, TargetedFault fault)
    {
        ValidateSubmissionKey(applicationId, submissionId);
        ValidateIdentity(invocationId, nameof(invocationId));
        ValidateIdentity(attemptId, nameof(attemptId));
        ValidateIdentity(token, nameof(token));
        if (delay < TimeSpan.Zero || delay == TimeSpan.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(delay), "A finite nonnegative delay is required.");
        ArgumentNullException.ThrowIfNull(fault);
        lock (gate)
        {
            var now = GetClock();
            var current = FindOwnership(applicationId, submissionId, invocationId);
            if (current is null)
                return new(AttemptTransitionStatus.NotFound, null);
            if (current.State != TargetedAttemptState.Running)
                return new(AttemptTransitionStatus.NotEligible, current);
            if (current.AttemptId != attemptId || current.Token != token)
                return new(AttemptTransitionStatus.InvalidOwner, current);
            if (now >= current.LeaseUntil!.Value)
                return new(AttemptTransitionStatus.Expired, current);

            DateTimeOffset due;
            try { due = now.Add(delay); }
            catch (ArgumentOutOfRangeException)
            {
                throw new ArgumentOutOfRangeException(nameof(delay), "Eligibility exceeds the clock range.");
            }
            var history = new RescheduledAttempt[current.History.Count + 1];
            for (var i = 0; i < current.History.Count; i++)
                history[i] = current.History[i];
            history[^1] = new RescheduledAttempt(attemptId, token, current.LeaseUntil.Value, due, fault);
            var next = new TargetedOwnership(current.Invocation, TargetedAttemptState.Eligible, null, null, null)
            {
                EligibleAt = due,
                History = Array.AsReadOnly(history)
            };
            ownerships[invocationId] = next;
            return new(AttemptTransitionStatus.Applied, next);
        }
    }

    public AttemptTransitionResult RecoverExpiredAsFailed(string applicationId, string submissionId, Guid invocationId, Guid observedAttemptId,
        Guid observedToken, DateTimeOffset observedLeaseUntil)
    {
        ValidateSubmissionKey(applicationId, submissionId);
        ValidateIdentity(invocationId, nameof(invocationId));
        ValidateIdentity(observedAttemptId, nameof(observedAttemptId));
        ValidateIdentity(observedToken, nameof(observedToken));
        lock (gate)
        {
            var now = GetClock();
            var current = FindOwnership(applicationId, submissionId, invocationId);
            if (current is null)
                return new(AttemptTransitionStatus.NotFound, null);
            if (current.State != TargetedAttemptState.Running)
                return new(AttemptTransitionStatus.NotEligible, current);
            if (current.AttemptId != observedAttemptId || current.Token != observedToken || current.LeaseUntil != observedLeaseUntil)
                return new(AttemptTransitionStatus.ObservationMismatch, current);
            if (now < current.LeaseUntil!.Value)
                return new(AttemptTransitionStatus.NotExpired, current);
            var next = current with { State = TargetedAttemptState.FailedLeaseExpired };
            ownerships[invocationId] = next;
            return new(AttemptTransitionStatus.Applied, next);
        }
    }

    // Helpers are invoked only under gate, and never invoke host code.
    private DateTimeOffset GetClock() => simulatedTime ??
        throw new InvalidOperationException("The simulated clock must be initialized first.");

    private TargetedOwnership? FindOwnership(string applicationId, string submissionId, Guid invocationId)
    {
        if (!submissions.TryGetValue((applicationId, submissionId), out var accepted) ||
            accepted.InvocationId != invocationId)
            return null;
        if (ownerships.TryGetValue(invocationId, out var current))
            return current;
        return new(accepted, TargetedAttemptState.Eligible, null, null, null);
    }

    private static void ValidateSubmissionKey(string applicationId, string submissionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(submissionId);
    }

    private static void ValidateIdentity(Guid value, string name)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("A nonempty identity is required.", name);
    }

    private static void ValidateDuration(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero || duration == TimeSpan.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(duration), "A finite positive lease is required.");
    }

    private static DateTimeOffset AddLease(DateTimeOffset now, TimeSpan duration)
    {
        try { return now.Add(duration); }
        catch (ArgumentOutOfRangeException)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Lease deadline exceeds the clock range.");
        }
    }
}
