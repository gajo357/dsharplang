namespace DSharp.Runtime;

// Provisional single-process state only. Lease expiry does not imply that the
// actor threw or that a stale process stopped executing.
internal enum TargetedAttemptState { Eligible, Running, FailedLeaseExpired, Completed, FailedReportedFault, Cancelled, Blocked }

// Readiness is reported by the host; this reference does not verify it.
internal enum BlockReason { ImplementationUnavailable, Incompatible }
internal sealed record TargetedBlock(Guid BlockId, string SelectedImplementation, BlockReason Reason);

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

// Origin is optional caller-reported data: it is neither inferred nor validated.
internal sealed class ReportedTerminalFault
{
    public ReportedTerminalFault(TargetedFault fault, Guid? originInvocationId = null)
    {
        ArgumentNullException.ThrowIfNull(fault);
        if (originInvocationId == Guid.Empty)
            throw new ArgumentException("A supplied origin must be nonempty.", nameof(originInvocationId));
        Fault = fault;
        OriginInvocationId = originInvocationId;
    }

    public TargetedFault Fault { get; }
    public Guid? OriginInvocationId { get; }
}

internal enum CancellationExitKind { Normal, ReportedFault }

// An inspected attempt exit is separate from the cancelled caller outcome.
internal sealed class CancellationExit
{
    private CancellationExit(CancellationExitKind kind, TargetedCompletion? completion, ReportedTerminalFault? fault)
    {
        Kind = kind;
        Completion = completion;
        Fault = fault;
    }

    internal static CancellationExit ForNormal(TargetedCompletion completion) => new(CancellationExitKind.Normal, completion, null);
    internal static CancellationExit ForFault(ReportedTerminalFault fault) => new(CancellationExitKind.ReportedFault, null, fault);
    public CancellationExitKind Kind { get; }
    public TargetedCompletion? Completion { get; }
    public ReportedTerminalFault? Fault { get; }
}

internal sealed record RescheduledAttempt(Guid AttemptId, Guid Token, DateTimeOffset LeaseUntil,
    DateTimeOffset EligibleAt, TargetedFault Fault);

internal enum AttemptRecoveryKind { ReportedFault, LeaseExpired }

// No actor fault or origin is inferred from the loss of a lease.
internal sealed class AttemptRecovery
{
    private AttemptRecovery(Guid attemptId, Guid token, DateTimeOffset leaseUntil,
        DateTimeOffset eligibleAt, AttemptRecoveryKind kind, TargetedFault? fault)
    {
        AttemptId = attemptId;
        Token = token;
        LeaseUntil = leaseUntil;
        EligibleAt = eligibleAt;
        Kind = kind;
        Fault = fault;
    }

    internal static AttemptRecovery ForFault(RescheduledAttempt attempt) =>
        new(attempt.AttemptId, attempt.Token, attempt.LeaseUntil, attempt.EligibleAt,
            AttemptRecoveryKind.ReportedFault, attempt.Fault);

    internal static AttemptRecovery ForExpiry(Guid attemptId, Guid token, DateTimeOffset leaseUntil,
        DateTimeOffset eligibleAt) =>
        new(attemptId, token, leaseUntil, eligibleAt, AttemptRecoveryKind.LeaseExpired, null);

    public Guid AttemptId { get; }
    public Guid Token { get; }
    public DateTimeOffset LeaseUntil { get; }
    public DateTimeOffset EligibleAt { get; }
    public AttemptRecoveryKind Kind { get; }
    public TargetedFault? Fault { get; }
}

internal sealed record TargetedOwnership(TargetedRecord Invocation, TargetedAttemptState State,
    Guid? AttemptId, Guid? Token, DateTimeOffset? LeaseUntil, TargetedCompletion? Completion = null)
{
    public DateTimeOffset? EligibleAt { get; init; }
    public long EligibilityVersion { get; init; }
    public TargetedBlock? Block { get; init; }
    public ReportedTerminalFault? TerminalFault { get; init; }
    public bool CancellationRequested { get; init; }
    public CancellationExit? ObservedCancellationExit { get; init; }
    // This pre-existing view contains only explicitly reported faults.
    public IReadOnlyList<RescheduledAttempt> History { get; init; } = Array.Empty<RescheduledAttempt>();
    // Complete ordered rescheduling history, including lease loss; snapshots own their buffers.
    public IReadOnlyList<AttemptRecovery> RecoveryHistory { get; init; } = Array.Empty<AttemptRecovery>();
}
internal enum AttemptTransitionStatus
{
    Applied, NotFound, NotEligible, InvalidOwner, Expired, ObservationMismatch,
    NotExpired, IdentityCollision, NotExtended, NotYetEligible, CancellationFinalized
}
internal sealed record AttemptTransitionResult(AttemptTransitionStatus Status, TargetedOwnership? Ownership);
internal enum CancellationRequestStatus { Applied, AlreadyRequested, AlreadyTerminal, NotFound }
internal sealed record CancellationRequestResult(CancellationRequestStatus Status, TargetedOwnership? Ownership);

internal sealed partial class VolatileTargetedStore
{
    private DateTimeOffset? simulatedTime;
    private readonly Dictionary<Guid, TargetedOwnership> ownerships = new();
    // Shared by attempts, tokens and blocks, so none can be reused in another role.
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

    // Bookkeeping only: the host is responsible for authorization and signalling.
    public CancellationRequestResult RequestCancellation(string applicationId, string submissionId, Guid invocationId)
    {
        ValidateSubmissionKey(applicationId, submissionId);
        ValidateIdentity(invocationId, nameof(invocationId));
        lock (gate)
        {
            _ = GetClock();
            var current = FindOwnership(applicationId, submissionId, invocationId);
            if (current is null)
                return new(CancellationRequestStatus.NotFound, null);
            if (current.State == TargetedAttemptState.Cancelled || current.CancellationRequested)
                return new(CancellationRequestStatus.AlreadyRequested, current);
            if (current.State is not (TargetedAttemptState.Eligible or TargetedAttemptState.Running or TargetedAttemptState.Blocked))
                return new(CancellationRequestStatus.AlreadyTerminal, current);
            var next = current with
            {
                CancellationRequested = true,
                State = current.State is TargetedAttemptState.Eligible or TargetedAttemptState.Blocked ? TargetedAttemptState.Cancelled : current.State
            };
            ownerships[invocationId] = next;
            return new(CancellationRequestStatus.Applied, next);
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
                EligibilityVersion = current.EligibilityVersion,
                History = current.History,
                RecoveryHistory = current.RecoveryHistory
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
            if (current.CancellationRequested)
            {
                var cancelled = current with
                {
                    State = TargetedAttemptState.Cancelled,
                    ObservedCancellationExit = CancellationExit.ForNormal(completion)
                };
                ownerships[invocationId] = cancelled;
                return new(AttemptTransitionStatus.CancellationFinalized, cancelled);
            }
            var next = current with { State = TargetedAttemptState.Completed, Completion = completion };
            ownerships[invocationId] = next;
            return new(AttemptTransitionStatus.Applied, next);
        }
    }

    public AttemptTransitionResult FailAfterFault(string applicationId, string submissionId, Guid invocationId,
        Guid attemptId, Guid token, ReportedTerminalFault fault)
    {
        ValidateSubmissionKey(applicationId, submissionId);
        ValidateIdentity(invocationId, nameof(invocationId));
        ValidateIdentity(attemptId, nameof(attemptId));
        ValidateIdentity(token, nameof(token));
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
            if (current.CancellationRequested)
            {
                var cancelled = current with
                {
                    State = TargetedAttemptState.Cancelled,
                    ObservedCancellationExit = CancellationExit.ForFault(fault)
                };
                ownerships[invocationId] = cancelled;
                return new(AttemptTransitionStatus.CancellationFinalized, cancelled);
            }
            var next = current with { State = TargetedAttemptState.FailedReportedFault, TerminalFault = fault };
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

            if (current.CancellationRequested)
            {
                var cancelled = current with
                {
                    State = TargetedAttemptState.Cancelled,
                    ObservedCancellationExit = CancellationExit.ForFault(new ReportedTerminalFault(fault))
                };
                ownerships[invocationId] = cancelled;
                return new(AttemptTransitionStatus.CancellationFinalized, cancelled);
            }

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
            var recoveryHistory = new AttemptRecovery[current.RecoveryHistory.Count + 1];
            for (var i = 0; i < current.RecoveryHistory.Count; i++)
                recoveryHistory[i] = current.RecoveryHistory[i];
            recoveryHistory[^1] = AttemptRecovery.ForFault(history[^1]);
            var next = new TargetedOwnership(current.Invocation, TargetedAttemptState.Eligible, null, null, null)
            {
                EligibilityVersion = checked(current.EligibilityVersion + 1),
                EligibleAt = due,
                History = Array.AsReadOnly(history),
                RecoveryHistory = Array.AsReadOnly(recoveryHistory)
            };
            ownerships[invocationId] = next;
            return new(AttemptTransitionStatus.Applied, next);
        }
    }

    public AttemptTransitionResult RecoverExpiredAsRetry(string applicationId, string submissionId, Guid invocationId,
        Guid observedAttemptId, Guid observedToken, DateTimeOffset observedLeaseUntil, TimeSpan delay)
    {
        ValidateSubmissionKey(applicationId, submissionId);
        ValidateIdentity(invocationId, nameof(invocationId));
        ValidateIdentity(observedAttemptId, nameof(observedAttemptId));
        ValidateIdentity(observedToken, nameof(observedToken));
        if (delay < TimeSpan.Zero || delay == TimeSpan.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(delay), "A finite nonnegative delay is required.");
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

            if (current.CancellationRequested)
            {
                var cancelled = current with { State = TargetedAttemptState.Cancelled };
                ownerships[invocationId] = cancelled;
                return new(AttemptTransitionStatus.CancellationFinalized, cancelled);
            }

            DateTimeOffset due;
            try { due = now.Add(delay); }
            catch (ArgumentOutOfRangeException)
            {
                throw new ArgumentOutOfRangeException(nameof(delay), "Eligibility exceeds the clock range.");
            }
            var recoveryHistory = new AttemptRecovery[current.RecoveryHistory.Count + 1];
            for (var i = 0; i < current.RecoveryHistory.Count; i++)
                recoveryHistory[i] = current.RecoveryHistory[i];
            recoveryHistory[^1] = AttemptRecovery.ForExpiry(observedAttemptId, observedToken, observedLeaseUntil, due);
            var next = new TargetedOwnership(current.Invocation, TargetedAttemptState.Eligible, null, null, null)
            {
                EligibilityVersion = checked(current.EligibilityVersion + 1),
                EligibleAt = due,
                History = current.History,
                RecoveryHistory = Array.AsReadOnly(recoveryHistory)
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
            if (current.CancellationRequested)
            {
                var cancelled = current with { State = TargetedAttemptState.Cancelled };
                ownerships[invocationId] = cancelled;
                return new(AttemptTransitionStatus.CancellationFinalized, cancelled);
            }
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
