using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class VolatileExpiryRetryTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static (VolatileTargetedStore Store, Guid Invocation, Guid Attempt, Guid Token) Running(DateTimeOffset? start = null, TimeSpan? lease = null)
    {
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        store.InitializeClock(start ?? Start);
        var invocation = Guid.NewGuid(); var attempt = Guid.NewGuid(); var token = Guid.NewGuid();
        var request = new TargetedRequest("app", "submission", TargetedOperation.Dispatch, "target", [1, 2], [3], [4]);
        Assert.Equal(TargetedAcceptKind.Created, store.Accept(request, invocation, "selected-v1").Kind);
        Assert.Equal(AttemptTransitionStatus.Applied, store.Claim("app", "submission", invocation, attempt, token, lease ?? TimeSpan.FromSeconds(10)).Status);
        return (store, invocation, attempt, token);
    }

    [Fact]
    public void ExactExpiryPersistsDelayAndRequiresFreshAttemptIdentity()
    {
        var (store, invocation, attempt, token) = Running();
        var deadline = Start.AddSeconds(10);
        store.AdvanceClock(deadline.AddTicks(-1));
        Assert.Equal(AttemptTransitionStatus.NotExpired, store.RecoverExpiredAsRetry("app", "submission", invocation, attempt, token, deadline, TimeSpan.FromSeconds(3)).Status);
        store.AdvanceClock(deadline);
        var result = store.RecoverExpiredAsRetry("app", "submission", invocation, attempt, token, deadline, TimeSpan.FromSeconds(3));
        Assert.Equal(AttemptTransitionStatus.Applied, result.Status);
        Assert.Equal(TargetedAttemptState.Eligible, result.Ownership!.State);
        Assert.Equal(deadline.AddSeconds(3), result.Ownership.EligibleAt);
        var lost = Assert.Single(result.Ownership.RecoveryHistory);
        Assert.Equal(AttemptRecoveryKind.LeaseExpired, lost.Kind);
        Assert.Null(lost.Fault);
        Assert.Equal(attempt, lost.AttemptId);
        Assert.Equal(token, lost.Token);
        Assert.Equal(deadline, lost.LeaseUntil);
        Assert.Equal(deadline.AddSeconds(3), lost.EligibleAt);
        Assert.Empty(result.Ownership.History);
        Assert.Equal(AttemptTransitionStatus.NotEligible, store.RecoverExpiredAsRetry("app", "submission", invocation, attempt, token, deadline, TimeSpan.Zero).Status);
        var newAttempt = Guid.NewGuid(); var newToken = Guid.NewGuid();
        Assert.Equal(AttemptTransitionStatus.NotYetEligible, store.Claim("app", "submission", invocation, newAttempt, newToken, TimeSpan.FromSeconds(1)).Status);
        store.AdvanceClock(deadline.AddSeconds(3).AddTicks(-1));
        Assert.Equal(AttemptTransitionStatus.NotYetEligible, store.Claim("app", "submission", invocation, newAttempt, newToken, TimeSpan.FromSeconds(1)).Status);
        store.AdvanceClock(deadline.AddSeconds(3));
        Assert.Equal(AttemptTransitionStatus.IdentityCollision, store.Claim("app", "submission", invocation, attempt, newToken, TimeSpan.FromSeconds(1)).Status);
        Assert.Equal(AttemptTransitionStatus.IdentityCollision, store.Claim("app", "submission", invocation, newAttempt, token, TimeSpan.FromSeconds(1)).Status);
        var claimed = store.Claim("app", "submission", invocation, newAttempt, newToken, TimeSpan.FromSeconds(1));
        Assert.Equal(AttemptTransitionStatus.Applied, claimed.Status);
        Assert.Equal(invocation, claimed.Ownership!.Invocation.InvocationId);
        Assert.Equal("selected-v1", claimed.Ownership.Invocation.SelectedImplementation);
        Assert.Equal(new byte[] { 1, 2 }, claimed.Ownership.Invocation.Request.Payload);
        Assert.Equal(newAttempt, claimed.Ownership.AttemptId);
        Assert.Equal(newToken, claimed.Ownership.Token);
        Assert.Equal(AttemptTransitionStatus.InvalidOwner, store.Renew("app", "submission", invocation, attempt, token, TimeSpan.FromSeconds(1)).Status);
        Assert.Equal(AttemptTransitionStatus.InvalidOwner, store.Complete("app", "submission", invocation, attempt, token, new TargetedCompletion([], [1])).Status);
        Assert.Equal(AttemptTransitionStatus.InvalidOwner, store.RescheduleAfterFault("app", "submission", invocation, attempt, token, TimeSpan.Zero, new TargetedFault([], [1])).Status);
        Assert.Equal(AttemptTransitionStatus.ObservationMismatch, store.RecoverExpiredAsRetry("app", "submission", invocation, attempt, token, deadline, TimeSpan.Zero).Status);
        Assert.Equal(AttemptTransitionStatus.ObservationMismatch, store.RecoverExpiredAsRetry("app", "submission", invocation, attempt, token, claimed.Ownership.LeaseUntil!.Value, TimeSpan.Zero).Status);
    }

    [Fact]
    public void RenewedAndInvalidObservationsNeverFenceCurrentOwner()
    {
        var (store, invocation, attempt, token) = Running();
        var oldDeadline = Start.AddSeconds(10);
        store.AdvanceClock(Start.AddSeconds(9));
        Assert.Equal(AttemptTransitionStatus.Applied, store.Renew("app", "submission", invocation, attempt, token, TimeSpan.FromSeconds(10)).Status);
        var newDeadline = Start.AddSeconds(19);
        store.AdvanceClock(oldDeadline);
        Assert.Equal(AttemptTransitionStatus.ObservationMismatch, store.RecoverExpiredAsRetry("app", "submission", invocation, attempt, token, oldDeadline, TimeSpan.Zero).Status);
        Assert.Equal(AttemptTransitionStatus.ObservationMismatch, store.RecoverExpiredAsRetry("app", "submission", invocation, Guid.NewGuid(), token, newDeadline, TimeSpan.Zero).Status);
        Assert.Equal(AttemptTransitionStatus.ObservationMismatch, store.RecoverExpiredAsRetry("app", "submission", invocation, attempt, Guid.NewGuid(), newDeadline, TimeSpan.Zero).Status);
        Assert.Equal(AttemptTransitionStatus.NotFound, store.RecoverExpiredAsRetry("other", "submission", invocation, attempt, token, newDeadline, TimeSpan.Zero).Status);
        Assert.ThrowsAny<ArgumentException>(() => store.RecoverExpiredAsRetry("app", "submission", invocation, attempt, token, newDeadline, TimeSpan.FromTicks(-1)));
        Assert.ThrowsAny<ArgumentException>(() => store.RecoverExpiredAsRetry("app", "submission", invocation, attempt, token, newDeadline, TimeSpan.MaxValue));
        store.AdvanceClock(newDeadline);
        Assert.Equal(AttemptTransitionStatus.ObservationMismatch, store.RecoverExpiredAsRetry("app", "submission", invocation, attempt, token, oldDeadline, TimeSpan.Zero).Status);
        var unchanged = store.GetOwnership("app", "submission", invocation)!;
        Assert.Equal(TargetedAttemptState.Running, unchanged.State);
        Assert.Equal(newDeadline, unchanged.LeaseUntil);
        Assert.Empty(unchanged.RecoveryHistory);
        Assert.Equal(AttemptTransitionStatus.Applied, store.RecoverExpiredAsRetry("app", "submission", invocation, attempt, token, newDeadline, TimeSpan.Zero).Status);
        Assert.Equal(AttemptTransitionStatus.Applied, store.Claim("app", "submission", invocation, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromSeconds(1)).Status);

        var nearMax = DateTimeOffset.MaxValue.AddTicks(-2);
        var (overflow, overflowId, overflowAttempt, overflowToken) = Running(nearMax, TimeSpan.FromTicks(1));
        var end = nearMax.AddTicks(1);
        overflow.AdvanceClock(end);
        Assert.ThrowsAny<ArgumentException>(() => overflow.RecoverExpiredAsRetry("app", "submission", overflowId, overflowAttempt, overflowToken, end, TimeSpan.FromTicks(2)));
        Assert.Equal(TargetedAttemptState.Running, overflow.GetOwnership("app", "submission", overflowId)!.State);
        Assert.Empty(overflow.GetOwnership("app", "submission", overflowId)!.RecoveryHistory);
    }

    [Fact]
    public void MixedFaultAndLeaseRecoveryHistoryIsOrderedCopiedAndSnapshotSafe()
    {
        var (store, invocation, attempt1, token1) = Running();
        byte[] data = [5, 6], metadata = [7, 8];
        var fault = new TargetedFault(data, metadata);
        data[0] = 99; metadata[0] = 99;
        var first = store.RescheduleAfterFault("app", "submission", invocation, attempt1, token1, TimeSpan.Zero, fault).Ownership!;
        var attempt2 = Guid.NewGuid(); var token2 = Guid.NewGuid();
        Assert.Equal(AttemptTransitionStatus.Applied, store.Claim("app", "submission", invocation, attempt2, token2, TimeSpan.FromSeconds(1)).Status);
        store.AdvanceClock(Start.AddSeconds(1));
        var second = store.RecoverExpiredAsRetry("app", "submission", invocation, attempt2, token2, Start.AddSeconds(1), TimeSpan.Zero).Ownership!;
        var attempt3 = Guid.NewGuid(); var token3 = Guid.NewGuid();
        Assert.Equal(AttemptTransitionStatus.Applied, store.Claim("app", "submission", invocation, attempt3, token3, TimeSpan.FromSeconds(1)).Status);
        var third = store.RescheduleAfterFault("app", "submission", invocation, attempt3, token3, TimeSpan.Zero, new TargetedFault([9], [10])).Ownership!;
        Assert.Single(first.RecoveryHistory);
        Assert.Equal(2, second.RecoveryHistory.Count);
        Assert.Equal(3, third.RecoveryHistory.Count);
        Assert.Equal(new[] { AttemptRecoveryKind.ReportedFault, AttemptRecoveryKind.LeaseExpired, AttemptRecoveryKind.ReportedFault }, third.RecoveryHistory.Select(item => item.Kind));
        Assert.Equal(new[] { attempt1, attempt2, attempt3 }, third.RecoveryHistory.Select(item => item.AttemptId));
        Assert.Equal(new byte[] { 5, 6 }, third.RecoveryHistory[0].Fault!.Payload);
        Assert.Equal(new byte[] { 7, 8 }, third.RecoveryHistory[0].Fault!.ContractMetadata);
        Assert.Null(third.RecoveryHistory[1].Fault);
        Assert.Equal(new byte[] { 9 }, third.RecoveryHistory[2].Fault!.Payload);
        third.RecoveryHistory[0].Fault!.Payload[0] = 88;
        if (third.RecoveryHistory is IList<AttemptRecovery> list && !list.IsReadOnly) list.Clear();
        var retained = store.GetOwnership("app", "submission", invocation)!;
        Assert.Equal(3, retained.RecoveryHistory.Count);
        Assert.Equal(new byte[] { 5, 6 }, retained.RecoveryHistory[0].Fault!.Payload);
        Assert.Equal(2, retained.History.Count);
        Assert.Equal(new[] { attempt1, attempt3 }, retained.History.Select(item => item.AttemptId));
    }

    [Fact]
    public void CompletedAndTerminalLeaseFailureCannotBecomeRetries()
    {
        var (completedStore, completed, attempt, token) = Running();
        Assert.Equal(AttemptTransitionStatus.Applied, completedStore.Complete("app", "submission", completed, attempt, token, new TargetedCompletion([0, 69, 82, 82], [1])).Status);
        completedStore.AdvanceClock(Start.AddSeconds(10));
        Assert.Equal(AttemptTransitionStatus.NotEligible, completedStore.RecoverExpiredAsRetry("app", "submission", completed, attempt, token, Start.AddSeconds(10), TimeSpan.Zero).Status);
        Assert.Equal(new byte[] { 0, 69, 82, 82 }, completedStore.GetOwnership("app", "submission", completed)!.Completion!.Payload);
        var (failedStore, failed, failedAttempt, failedToken) = Running();
        failedStore.AdvanceClock(Start.AddSeconds(10));
        Assert.Equal(AttemptTransitionStatus.Applied, failedStore.RecoverExpiredAsFailed("app", "submission", failed, failedAttempt, failedToken, Start.AddSeconds(10)).Status);
        Assert.Equal(AttemptTransitionStatus.NotEligible, failedStore.RecoverExpiredAsRetry("app", "submission", failed, failedAttempt, failedToken, Start.AddSeconds(10), TimeSpan.Zero).Status);
        Assert.Equal(TargetedAttemptState.FailedLeaseExpired, failedStore.GetOwnership("app", "submission", failed)!.State);
    }

    [Fact]
    public async Task ConcurrentRecoveryAndSubsequentClaimsHaveOneWinner()
    {
        const int count = 12;
        var (store, invocation, attempt, token) = Running();
        var deadline = Start.AddSeconds(10);
        store.AdvanceClock(deadline);
        using var recoveryBarrier = new Barrier(count);
        var recoveries = Enumerable.Range(0, count).Select(_ => Task.Factory.StartNew(() =>
        {
            if (!recoveryBarrier.SignalAndWait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Recovery barrier timed out.");
            return store.RecoverExpiredAsRetry("app", "submission", invocation, attempt, token, deadline, TimeSpan.Zero);
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
        var recovered = await Task.WhenAll(recoveries).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Single(recovered, item => item.Status == AttemptTransitionStatus.Applied);
        Assert.Equal(count - 1, recovered.Count(item => item.Status == AttemptTransitionStatus.NotEligible));
        Assert.Single(store.GetOwnership("app", "submission", invocation)!.RecoveryHistory);
        using var claimBarrier = new Barrier(count);
        var claims = Enumerable.Range(0, count).Select(_ => Task.Factory.StartNew(() =>
        {
            if (!claimBarrier.SignalAndWait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Claim barrier timed out.");
            return store.Claim("app", "submission", invocation, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromSeconds(1));
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
        var claimed = await Task.WhenAll(claims).WaitAsync(TimeSpan.FromSeconds(30));
        var winner = Assert.Single(claimed, item => item.Status == AttemptTransitionStatus.Applied);
        Assert.Equal(count - 1, claimed.Count(item => item.Status == AttemptTransitionStatus.NotEligible));
        Assert.Single(winner.Ownership!.RecoveryHistory);
    }

    [Fact]
    public async Task CompetingExpiryFailureAndRetryChooseOneTerminalTransition()
    {
        var (store, invocation, attempt, token) = Running();
        var deadline = Start.AddSeconds(10);
        store.AdvanceClock(deadline);
        using var barrier = new Barrier(2);
        void Signal() { if (!barrier.SignalAndWait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Recovery race timed out."); }
        var retry = Task.Factory.StartNew(() => { Signal(); return store.RecoverExpiredAsRetry("app", "submission", invocation, attempt, token, deadline, TimeSpan.Zero); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var failure = Task.Factory.StartNew(() => { Signal(); return store.RecoverExpiredAsFailed("app", "submission", invocation, attempt, token, deadline); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        await Task.WhenAll(retry, failure).WaitAsync(TimeSpan.FromSeconds(30));
        var r = await retry; var f = await failure;
        Assert.NotEqual(r.Status == AttemptTransitionStatus.Applied, f.Status == AttemptTransitionStatus.Applied);
        var retained = store.GetOwnership("app", "submission", invocation)!;
        if (r.Status == AttemptTransitionStatus.Applied)
        {
            Assert.Equal(AttemptTransitionStatus.NotEligible, f.Status);
            Assert.Equal(TargetedAttemptState.Eligible, retained.State);
            Assert.Null(Assert.Single(retained.RecoveryHistory).Fault);
        }
        else
        {
            Assert.Equal(AttemptTransitionStatus.NotEligible, r.Status);
            Assert.Equal(TargetedAttemptState.FailedLeaseExpired, retained.State);
        }
    }

    [Fact]
    public async Task RecoveryCompletionAndClockRaceAndZeroDelayReclaimStayCoherent()
    {
        var (store, invocation, attempt, token) = Running();
        var deadline = Start.AddSeconds(10);
        store.AdvanceClock(deadline.AddTicks(-1));
        using var barrier = new Barrier(3);
        void Signal() { if (!barrier.SignalAndWait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Transition barrier timed out."); }
        var clock = Task.Factory.StartNew(() => { Signal(); store.AdvanceClock(deadline); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var complete = Task.Factory.StartNew(() => { Signal(); return store.Complete("app", "submission", invocation, attempt, token, new TargetedCompletion([42], [1])); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var recover = Task.Factory.StartNew(() => { Signal(); return store.RecoverExpiredAsRetry("app", "submission", invocation, attempt, token, deadline, TimeSpan.Zero); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        await Task.WhenAll(clock, complete, recover).WaitAsync(TimeSpan.FromSeconds(30));
        var completion = await complete; var recovery = await recover;
        Assert.False(completion.Status == AttemptTransitionStatus.Applied && recovery.Status == AttemptTransitionStatus.Applied);
        var state = store.GetOwnership("app", "submission", invocation)!;
        if (completion.Status == AttemptTransitionStatus.Applied)
        {
            Assert.Equal(TargetedAttemptState.Completed, state.State);
            Assert.Equal(new byte[] { 42 }, state.Completion!.Payload);
        }
        else if (recovery.Status == AttemptTransitionStatus.Applied)
        {
            Assert.Equal(TargetedAttemptState.Eligible, state.State);
            Assert.Null(Assert.Single(state.RecoveryHistory).Fault);
        }
        else
        {
            Assert.Equal(AttemptTransitionStatus.Expired, completion.Status);
            Assert.Equal(AttemptTransitionStatus.NotExpired, recovery.Status);
            Assert.Equal(TargetedAttemptState.Running, state.State);
        }

        var (other, otherId, otherAttempt, otherToken) = Running();
        other.AdvanceClock(deadline);
        using var retryBarrier = new Barrier(2);
        void StartBoth() { if (!retryBarrier.SignalAndWait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Reclaim barrier timed out."); }
        var oldRecovery = Task.Factory.StartNew(() => { StartBoth(); return other.RecoverExpiredAsRetry("app", "submission", otherId, otherAttempt, otherToken, deadline, TimeSpan.Zero); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var newClaim = Task.Factory.StartNew(() => { StartBoth(); return other.Claim("app", "submission", otherId, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromSeconds(1)); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        await Task.WhenAll(oldRecovery, newClaim).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(AttemptTransitionStatus.Applied, (await oldRecovery).Status);
        var claim = await newClaim;
        var observed = other.GetOwnership("app", "submission", otherId)!;
        if (claim.Status == AttemptTransitionStatus.Applied)
        {
            Assert.Equal(TargetedAttemptState.Running, observed.State);
            Assert.Equal(claim.Ownership!.AttemptId, observed.AttemptId);
        }
        else
        {
            Assert.Equal(AttemptTransitionStatus.NotEligible, claim.Status);
            Assert.Equal(TargetedAttemptState.Eligible, observed.State);
        }
        Assert.Null(Assert.Single(observed.RecoveryHistory).Fault);
    }
}
