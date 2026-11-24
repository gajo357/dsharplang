namespace DSharp.Runtime.Tests.Features;

// A test-only normalized contract, not a production provider API.
internal enum LStatus
{
    Created, Existing, Conflict, Absent, Applied, NotEligible, NotYetEligible,
    IdentityCollision, InvalidOwner, Expired, NotExpired, ObservationMismatch
}
internal enum LState { None, Eligible, Running, Completed, Cancelled }
internal enum LRecoveryKind { LeaseExpired, ReportedFault }
internal sealed record LRecovery(LRecoveryKind Kind, Guid Attempt, Guid Token,
    DateTimeOffset Deadline, DateTimeOffset Due, byte[]? Fault);
internal sealed record LAdmission(LStatus Status, Guid? Invocation, string? Selected, byte[]? Payload);
internal sealed record LSnapshot(LStatus Status, LState State, Guid? Attempt, Guid? Token,
    DateTimeOffset? Deadline, DateTimeOffset? Due, IReadOnlyList<LRecovery> History, byte[]? Completion);

internal interface ILeaseScenarioStore
{
    CCapabilities Capabilities { get; }
    LAdmission Accept(string key, byte[] payload, Guid invocation, string selected);
    LAdmission Probe(string key, byte[] payload);
    LSnapshot Claim(string key, Guid invocation, Guid attempt, Guid token, TimeSpan lease);
    LSnapshot Renew(string key, Guid invocation, Guid attempt, Guid token, TimeSpan lease);
    LSnapshot Recover(string key, Guid invocation, Guid attempt, Guid token, DateTimeOffset observedDeadline, TimeSpan delay);
    LSnapshot Complete(string key, Guid invocation, Guid attempt, Guid token, byte[] result);
    LSnapshot Inspect(string key, Guid invocation);
    void Advance(TimeSpan delta);
}

internal sealed class LeaseConformanceViolation : Exception
{
    public LeaseConformanceViolation(string invariant, LSnapshot? observation = null)
        : base($"Lease conformance invariant failed: {invariant}")
    {
        Invariant = invariant;
        Observation = observation;
    }
    public string Invariant { get; }
    public LSnapshot? Observation { get; }
}

internal static class LeaseRetryScenarios
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static void Check(bool condition, string invariant, LSnapshot? observation = null)
    {
        if (!condition) throw new LeaseConformanceViolation(invariant, observation);
    }

    public static async Task RunAll(Func<CRequirement, ILeaseScenarioStore> factory)
    {
        var caps = factory(CRequirement.None).Capabilities;
        Check(caps.ByteExact && caps.InProcessAtomic && caps.ControlledTime, "lease.capabilities.subset");
        foreach (var (requirement, supported) in new[]
        {
            (CRequirement.Durable, caps.Durable),
            (CRequirement.Canonical, caps.Canonical),
            (CRequirement.FollowUps, caps.FollowUps)
        })
        {
            if (supported) continue;
            var rejected = false;
            try { _ = factory(requirement); }
            catch (NotSupportedException) { rejected = true; }
            Check(rejected, $"lease.capabilities.rejected-{requirement}");
        }
        StaleDeadline(factory);
        await LiveRace(factory);
        await ExpiredRace(factory);
    }

    public static void StaleDeadline(Func<CRequirement, ILeaseScenarioStore> factory)
    {
        var store = factory(CRequirement.None);
        const string key = "lease-stale";
        var id = Guid.NewGuid(); var attempt = Guid.NewGuid(); var token = Guid.NewGuid();
        Check(store.Accept(key, [1, 2], id, "pinned").Status == LStatus.Created, "lease.accept");
        var first = store.Claim(key, id, attempt, token, TimeSpan.FromSeconds(10));
        Check(first.Status == LStatus.Applied && first.Deadline.HasValue, "lease.claim", first);
        var oldDeadline = first.Deadline!.Value;
        store.Advance(TimeSpan.FromSeconds(9));
        var renewed = store.Renew(key, id, attempt, token, TimeSpan.FromSeconds(10));
        Check(renewed.Status == LStatus.Applied && renewed.Deadline == oldDeadline.AddSeconds(9), "lease.renew", renewed);
        var newDeadline = renewed.Deadline!.Value;
        store.Advance(TimeSpan.FromSeconds(1));
        var before = store.Recover(key, id, attempt, token, oldDeadline, TimeSpan.FromSeconds(3));
        var preSnapshot = store.Inspect(key, id);
        Check(preSnapshot.State == LState.Running && preSnapshot.Attempt == attempt && preSnapshot.Token == token &&
              preSnapshot.Deadline == newDeadline && preSnapshot.History.Count == 0, "lease.old-boundary-mutated", preSnapshot);
        // Defer the first status assertion so the negative control can reach the
        // renewed deadline and demonstrate a real forbidden mutation.
        store.Advance(TimeSpan.FromSeconds(9));
        var after = store.Recover(key, id, attempt, token, oldDeadline, TimeSpan.FromSeconds(3));
        var postSnapshot = store.Inspect(key, id);
        Check(postSnapshot.State == LState.Running && postSnapshot.Attempt == attempt && postSnapshot.Token == token &&
              postSnapshot.Deadline == newDeadline && postSnapshot.History.Count == 0,
              "lease.stale-observation-mutated", postSnapshot);
        Check(before.Status == LStatus.ObservationMismatch && after.Status == LStatus.ObservationMismatch,
            "lease.stale-observation-status", postSnapshot);

        var recovered = store.Recover(key, id, attempt, token, newDeadline, TimeSpan.FromSeconds(3));
        Check(recovered.Status == LStatus.Applied && recovered.State == LState.Eligible && recovered.Due == newDeadline.AddSeconds(3),
            "lease.recovery-due", recovered);
        Check(recovered.History.Count == 1 && recovered.History[0].Kind == LRecoveryKind.LeaseExpired &&
              recovered.History[0].Attempt == attempt && recovered.History[0].Token == token &&
              recovered.History[0].Deadline == newDeadline && recovered.History[0].Due == recovered.Due && recovered.History[0].Fault is null,
            "lease.recovery-history", recovered);
        var freshAttempt = Guid.NewGuid(); var freshToken = Guid.NewGuid();
        store.Advance(TimeSpan.FromSeconds(3).Subtract(TimeSpan.FromTicks(1)));
        Check(store.Claim(key, id, freshAttempt, freshToken, TimeSpan.FromSeconds(1)).Status == LStatus.NotYetEligible,
            "lease.before-due");
        store.Advance(TimeSpan.FromTicks(1));
        Check(store.Claim(key, id, attempt, freshToken, TimeSpan.FromSeconds(1)).Status == LStatus.IdentityCollision,
            "lease.attempt-reused");
        Check(store.Claim(key, id, freshAttempt, token, TimeSpan.FromSeconds(1)).Status == LStatus.IdentityCollision,
            "lease.token-reused");
        var next = store.Claim(key, id, freshAttempt, freshToken, TimeSpan.FromSeconds(1));
        Check(next.Status == LStatus.Applied && next.Attempt == freshAttempt && next.Token == freshToken && next.History.Count == 1,
            "lease.due-fresh-claim", next);
        Check(store.Complete(key, id, attempt, token, [9]).Status == LStatus.InvalidOwner, "lease.old-owner-fenced");
        var retained = store.Probe(key, [1, 2]);
        Check(retained.Status == LStatus.Existing && retained.Invocation == id && retained.Selected == "pinned" &&
              retained.Payload is not null && retained.Payload.SequenceEqual(new byte[] { 1, 2 }), "lease.accepted-identity");
        Check(recovered.State == LState.Eligible && recovered.History.Count == 1 && recovered.History[0].Attempt == attempt,
            "lease.earlier-snapshot", recovered);
    }

    private static Task<LSnapshot> Participant(Barrier barrier, Func<LSnapshot> operation) => Task.Factory.StartNew(() =>
    {
        if (!barrier.SignalAndWait(Timeout)) throw new LeaseConformanceViolation("lease.race-barrier-timeout");
        return operation();
    }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    private static async Task LiveRace(Func<CRequirement, ILeaseScenarioStore> factory)
    {
        var store = factory(CRequirement.None);
        const string key = "lease-live-race";
        var id = Guid.NewGuid(); var attempt = Guid.NewGuid(); var token = Guid.NewGuid();
        Check(store.Accept(key, [1], id, "pinned").Status == LStatus.Created, "lease.live-accept");
        var claim = store.Claim(key, id, attempt, token, TimeSpan.FromSeconds(10));
        Check(claim.Status == LStatus.Applied && claim.Deadline.HasValue, "lease.live-claim", claim);
        using var barrier = new Barrier(4);
        var renewal = Participant(barrier, () => store.Renew(key, id, attempt, token, TimeSpan.FromSeconds(20)));
        var completion = Participant(barrier, () => store.Complete(key, id, attempt, token, [8]));
        var recovery = Participant(barrier, () => store.Recover(key, id, attempt, token, claim.Deadline!.Value, TimeSpan.Zero));
        var competingClaim = Participant(barrier, () => store.Claim(key, id, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromSeconds(10)));
        await Task.WhenAll(renewal, completion, recovery, competingClaim).WaitAsync(Timeout);
        var final = store.Inspect(key, id);
        Check(completion.Result.Status == LStatus.Applied && final.State == LState.Completed &&
              final.Completion is not null && final.Completion.SequenceEqual(new byte[] { 8 }) && final.History.Count == 0,
            "lease.live-completion", final);
        Check(renewal.Result.Status is LStatus.Applied or LStatus.NotEligible, "lease.live-renew", final);
        Check(recovery.Result.Status is LStatus.NotExpired or LStatus.ObservationMismatch or LStatus.NotEligible,
            "lease.live-recovery", final);
        Check(competingClaim.Result.Status == LStatus.NotEligible, "lease.live-second-claim", final);
    }

    private static async Task ExpiredRace(Func<CRequirement, ILeaseScenarioStore> factory)
    {
        var store = factory(CRequirement.None);
        const string key = "lease-expired-race";
        var id = Guid.NewGuid(); var attempt = Guid.NewGuid(); var token = Guid.NewGuid();
        Check(store.Accept(key, [1], id, "pinned").Status == LStatus.Created, "lease.expired-accept");
        var claim = store.Claim(key, id, attempt, token, TimeSpan.FromSeconds(10));
        Check(claim.Status == LStatus.Applied && claim.Deadline.HasValue, "lease.expired-claim", claim);
        store.Advance(TimeSpan.FromSeconds(10));
        var newAttempt = Guid.NewGuid(); var newToken = Guid.NewGuid();
        using var barrier = new Barrier(3);
        var recovery1 = Participant(barrier, () => store.Recover(key, id, attempt, token, claim.Deadline!.Value, TimeSpan.Zero));
        var recovery2 = Participant(barrier, () => store.Recover(key, id, attempt, token, claim.Deadline!.Value, TimeSpan.Zero));
        var nextClaim = Participant(barrier, () => store.Claim(key, id, newAttempt, newToken, TimeSpan.FromSeconds(10)));
        await Task.WhenAll(recovery1, recovery2, nextClaim).WaitAsync(Timeout);
        var final = store.Inspect(key, id);
        Check(new[] { recovery1.Result, recovery2.Result }.Count(x => x.Status == LStatus.Applied) == 1,
            "lease.expired-single-recovery", final);
        Check(final.History.Count == 1 && final.History[0].Kind == LRecoveryKind.LeaseExpired &&
              final.History[0].Attempt == attempt && final.History[0].Token == token, "lease.expired-history", final);
        Check(nextClaim.Result.Status == LStatus.Applied
                ? final.State == LState.Running && final.Attempt == newAttempt && final.Token == newToken
                : nextClaim.Result.Status == LStatus.NotEligible && final.State == LState.Eligible,
            "lease.expired-claim-order", final);
    }
}
