using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class VolatileTargetedRescheduleTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static (VolatileTargetedStore Store, Guid Invocation, Guid Attempt, Guid Token) Running(DateTimeOffset? start = null)
    {
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        store.InitializeClock(start ?? Start);
        var invocation = Guid.NewGuid();
        var attempt = Guid.NewGuid();
        var token = Guid.NewGuid();
        var request = new TargetedRequest("app", "submission", TargetedOperation.Dispatch, "target", [1, 2], [3], [4]);
        Assert.Equal(TargetedAcceptKind.Created, store.Accept(request, invocation, "selected-v1").Kind);
        Assert.Equal(AttemptTransitionStatus.Applied, store.Claim("app", "submission", invocation, attempt, token, TimeSpan.FromTicks(1)).Status);
        return (store, invocation, attempt, token);
    }

    [Fact]
    public void DelayBoundaryAndRetainedHistoryPreserveTheSameInvocationAndCopiedFault()
    {
        var (store, invocation, attempt, token) = Running();
        byte[] payload = [10, 11], metadata = [20, 21];
        var fault = new TargetedFault(payload, metadata);
        payload[0] = 90; metadata[0] = 91;
        fault.Payload[1] = 80; fault.ContractMetadata[1] = 81;
        var scheduled = store.RescheduleAfterFault("app", "submission", invocation, attempt, token, TimeSpan.FromSeconds(5), fault);
        Assert.Equal(AttemptTransitionStatus.Applied, scheduled.Status);
        var first = scheduled.Ownership!;
        Assert.Equal(TargetedAttemptState.Eligible, first.State);
        Assert.Equal(Start.AddSeconds(5), first.EligibleAt);
        Assert.Null(first.AttemptId); Assert.Null(first.Token); Assert.Null(first.LeaseUntil);
        var history = Assert.Single(first.History);
        Assert.Equal(attempt, history.AttemptId);
        Assert.Equal(token, history.Token);
        Assert.Equal(Start.AddTicks(1), history.LeaseUntil);
        Assert.Equal(Start.AddSeconds(5), history.EligibleAt);
        Assert.Equal(new byte[] { 10, 11 }, history.Fault.Payload);
        Assert.Equal(new byte[] { 20, 21 }, history.Fault.ContractMetadata);
        history.Fault.Payload[0] = 70; history.Fault.ContractMetadata[0] = 71;
        if (first.History is IList<RescheduledAttempt> list && !list.IsReadOnly) list.Clear();
        Assert.Equal(AttemptTransitionStatus.NotEligible, store.RescheduleAfterFault("app", "submission", invocation, attempt, token, TimeSpan.Zero, fault).Status);
        var nextAttempt = Guid.NewGuid();
        var nextToken = Guid.NewGuid();
        Assert.Equal(AttemptTransitionStatus.NotYetEligible, store.Claim("app", "submission", invocation, nextAttempt, nextToken, TimeSpan.FromSeconds(1)).Status);
        store.AdvanceClock(Start.AddSeconds(5).AddTicks(-1));
        Assert.Equal(AttemptTransitionStatus.NotYetEligible, store.Claim("app", "submission", invocation, nextAttempt, nextToken, TimeSpan.FromSeconds(1)).Status);
        store.AdvanceClock(Start.AddSeconds(5));
        Assert.Equal(AttemptTransitionStatus.IdentityCollision, store.Claim("app", "submission", invocation, attempt, nextToken, TimeSpan.FromSeconds(1)).Status);
        Assert.Equal(AttemptTransitionStatus.IdentityCollision, store.Claim("app", "submission", invocation, nextAttempt, token, TimeSpan.FromSeconds(1)).Status);
        var claimed = store.Claim("app", "submission", invocation, nextAttempt, nextToken, TimeSpan.FromSeconds(1));
        Assert.Equal(AttemptTransitionStatus.Applied, claimed.Status);
        Assert.Equal(invocation, claimed.Ownership!.Invocation.InvocationId);
        Assert.Equal("selected-v1", claimed.Ownership.Invocation.SelectedImplementation);
        Assert.Equal(new byte[] { 1, 2 }, claimed.Ownership.Invocation.Request.Payload);
        Assert.Equal(nextAttempt, claimed.Ownership.AttemptId);
        Assert.Equal(nextToken, claimed.Ownership.Token);
        Assert.Equal(AttemptTransitionStatus.InvalidOwner, store.RescheduleAfterFault("app", "submission", invocation, attempt, token, TimeSpan.Zero, fault).Status);
        Assert.Equal(AttemptTransitionStatus.InvalidOwner, store.Complete("app", "submission", invocation, attempt, token, new TargetedCompletion([], [1])).Status);
        Assert.Equal(AttemptTransitionStatus.ObservationMismatch, store.RecoverExpiredAsFailed("app", "submission", invocation, attempt, token, Start.AddTicks(1)).Status);

        var secondFault = new TargetedFault([30], [40]);
        var second = store.RescheduleAfterFault("app", "submission", invocation, nextAttempt, nextToken, TimeSpan.Zero, secondFault);
        Assert.Equal(AttemptTransitionStatus.Applied, second.Status);
        Assert.Equal(Start.AddSeconds(5), second.Ownership!.EligibleAt);
        Assert.Equal(2, second.Ownership.History.Count);
        Assert.Equal(attempt, second.Ownership.History[0].AttemptId);
        Assert.Equal(new byte[] { 10, 11 }, second.Ownership.History[0].Fault.Payload);
        Assert.Equal(new byte[] { 20, 21 }, second.Ownership.History[0].Fault.ContractMetadata);
        Assert.Equal(nextAttempt, second.Ownership.History[1].AttemptId);
        Assert.Equal(nextToken, second.Ownership.History[1].Token);
        Assert.Equal(new byte[] { 30 }, second.Ownership.History[1].Fault.Payload);
        Assert.Single(claimed.Ownership.History);
        var third = store.Claim("app", "submission", invocation, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromSeconds(1));
        Assert.Equal(AttemptTransitionStatus.Applied, third.Status);
        Assert.Equal(2, third.Ownership!.History.Count);
        Assert.Equal(new byte[] { 10, 11 }, third.Ownership.History[0].Fault.Payload);
        Assert.Equal(new byte[] { 30 }, third.Ownership.History[1].Fault.Payload);
    }

    [Fact]
    public void InvalidAndExpiredOwnersCannotMutateOrConsumeIdentities()
    {
        var (store, invocation, attempt, token) = Running();
        var fault = new TargetedFault([1], [2]);
        Assert.Throws<ArgumentNullException>(() => new TargetedFault(null!, [1]));
        Assert.Throws<ArgumentNullException>(() => new TargetedFault([], null!));
        Assert.ThrowsAny<ArgumentException>(() => new TargetedFault([], []));
        Assert.Throws<ArgumentNullException>(() => store.RescheduleAfterFault("app", "submission", invocation, attempt, token, TimeSpan.Zero, null!));
        Assert.ThrowsAny<ArgumentException>(() => store.RescheduleAfterFault("app", "submission", invocation, attempt, token, TimeSpan.FromTicks(-1), fault));
        Assert.ThrowsAny<ArgumentException>(() => store.RescheduleAfterFault("app", "submission", invocation, attempt, token, TimeSpan.MaxValue, fault));
        Assert.Equal(AttemptTransitionStatus.NotFound, store.RescheduleAfterFault("other", "submission", invocation, attempt, token, TimeSpan.Zero, fault).Status);
        Assert.Equal(AttemptTransitionStatus.NotFound, store.RescheduleAfterFault("app", "other", invocation, attempt, token, TimeSpan.Zero, fault).Status);
        Assert.Equal(AttemptTransitionStatus.NotFound, store.RescheduleAfterFault("app", "submission", Guid.NewGuid(), attempt, token, TimeSpan.Zero, fault).Status);
        Assert.Equal(AttemptTransitionStatus.InvalidOwner, store.RescheduleAfterFault("app", "submission", invocation, Guid.NewGuid(), token, TimeSpan.Zero, fault).Status);
        Assert.Equal(AttemptTransitionStatus.InvalidOwner, store.RescheduleAfterFault("app", "submission", invocation, attempt, Guid.NewGuid(), TimeSpan.Zero, fault).Status);
        Assert.Empty(store.GetOwnership("app", "submission", invocation)!.History);
        store.AdvanceClock(Start.AddTicks(1));
        Assert.Equal(AttemptTransitionStatus.Expired, store.RescheduleAfterFault("app", "submission", invocation, attempt, token, TimeSpan.Zero, fault).Status);
        Assert.Equal(TargetedAttemptState.Running, store.GetOwnership("app", "submission", invocation)!.State);
        Assert.Empty(store.GetOwnership("app", "submission", invocation)!.History);

        var nearMax = DateTimeOffset.MaxValue.AddTicks(-1);
        var (overflow, overflowId, overflowAttempt, overflowToken) = Running(nearMax);
        Assert.ThrowsAny<ArgumentException>(() => overflow.RescheduleAfterFault("app", "submission", overflowId, overflowAttempt, overflowToken, TimeSpan.FromTicks(2), fault));
        var unchanged = overflow.GetOwnership("app", "submission", overflowId)!;
        Assert.Equal(TargetedAttemptState.Running, unchanged.State);
        Assert.Equal(overflowAttempt, unchanged.AttemptId);
        Assert.Empty(unchanged.History);
    }

    [Fact]
    public void CompletedBusinessErrorLikeValueAndExplicitLeaseFailureStayTerminal()
    {
        var fault = new TargetedFault([1], [2]);
        var (completedStore, completed, attempt, token) = Running();
        Assert.Equal(AttemptTransitionStatus.Applied, completedStore.Complete("app", "submission", completed, attempt, token, new TargetedCompletion([0, 69, 82, 82], [4])).Status);
        Assert.Equal(AttemptTransitionStatus.NotEligible, completedStore.RescheduleAfterFault("app", "submission", completed, attempt, token, TimeSpan.Zero, fault).Status);
        Assert.Equal(AttemptTransitionStatus.NotEligible, completedStore.Claim("app", "submission", completed, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromTicks(1)).Status);
        Assert.Equal(new byte[] { 0, 69, 82, 82 }, completedStore.GetOwnership("app", "submission", completed)!.Completion!.Payload);
        var (failedStore, failed, failedAttempt, failedToken) = Running();
        failedStore.AdvanceClock(Start.AddTicks(1));
        Assert.Equal(AttemptTransitionStatus.Applied, failedStore.RecoverExpiredAsFailed("app", "submission", failed, failedAttempt, failedToken, Start.AddTicks(1)).Status);
        Assert.Equal(AttemptTransitionStatus.NotEligible, failedStore.RescheduleAfterFault("app", "submission", failed, failedAttempt, failedToken, TimeSpan.Zero, fault).Status);
        Assert.Equal(AttemptTransitionStatus.NotEligible, failedStore.Claim("app", "submission", failed, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromTicks(1)).Status);
        Assert.Equal(TargetedAttemptState.FailedLeaseExpired, failedStore.GetOwnership("app", "submission", failed)!.State);
    }

    [Fact]
    public async Task CompetingClaimsAtEligibilityHaveOneWinner()
    {
        const int count = 12;
        var (store, invocation, attempt, token) = Running();
        Assert.Equal(AttemptTransitionStatus.Applied, store.RescheduleAfterFault("app", "submission", invocation, attempt, token, TimeSpan.FromSeconds(1), new TargetedFault([], [1])).Status);
        store.AdvanceClock(Start.AddSeconds(1));
        using var barrier = new Barrier(count);
        var calls = Enumerable.Range(0, count).Select(_ => Task.Factory.StartNew(() =>
        {
            if (!barrier.SignalAndWait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Claim barrier timed out.");
            return store.Claim("app", "submission", invocation, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromSeconds(1));
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
        var results = await Task.WhenAll(calls).WaitAsync(TimeSpan.FromSeconds(30));
        var winner = Assert.Single(results, item => item.Status == AttemptTransitionStatus.Applied);
        Assert.Equal(count - 1, results.Count(item => item.Status == AttemptTransitionStatus.NotEligible));
        Assert.NotEqual(attempt, winner.Ownership!.AttemptId);
        Assert.NotEqual(token, winner.Ownership.Token);
        Assert.Single(winner.Ownership.History);
    }

    [Fact]
    public async Task CompletionRescheduleRenewalAndClockRaceToOneCoherentState()
    {
        var (store, invocation, attempt, token) = Running();
        var deadline = Start.AddTicks(1);
        using var barrier = new Barrier(4);
        void Signal() { if (!barrier.SignalAndWait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Reschedule race barrier timed out."); }
        var advance = Task.Factory.StartNew(() => { Signal(); store.AdvanceClock(deadline); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var completion = Task.Factory.StartNew(() => { Signal(); return store.Complete("app", "submission", invocation, attempt, token, new TargetedCompletion([42], [1])); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var schedule = Task.Factory.StartNew(() => { Signal(); return store.RescheduleAfterFault("app", "submission", invocation, attempt, token, TimeSpan.FromSeconds(1), new TargetedFault([7], [1])); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var renewal = Task.Factory.StartNew(() => { Signal(); return store.Renew("app", "submission", invocation, attempt, token, TimeSpan.FromSeconds(2)); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        await Task.WhenAll(advance, completion, schedule, renewal).WaitAsync(TimeSpan.FromSeconds(30));
        var completed = await completion;
        var rescheduled = await schedule;
        var renewed = await renewal;
        var current = store.GetOwnership("app", "submission", invocation)!;
        Assert.False(completed.Status == AttemptTransitionStatus.Applied && rescheduled.Status == AttemptTransitionStatus.Applied);
        if (completed.Status == AttemptTransitionStatus.Applied)
        {
            Assert.Equal(TargetedAttemptState.Completed, current.State);
            Assert.Equal(new byte[] { 42 }, current.Completion!.Payload);
            Assert.Empty(current.History);
        }
        else if (rescheduled.Status == AttemptTransitionStatus.Applied)
        {
            Assert.Equal(TargetedAttemptState.Eligible, current.State);
            Assert.Equal(attempt, Assert.Single(current.History).AttemptId);
            Assert.Equal(new byte[] { 7 }, current.History[0].Fault.Payload);
        }
        else
        {
            Assert.Equal(TargetedAttemptState.Running, current.State);
            Assert.Equal(AttemptTransitionStatus.Expired, completed.Status);
            Assert.Equal(AttemptTransitionStatus.Expired, rescheduled.Status);
            Assert.Equal(AttemptTransitionStatus.Expired, renewed.Status);
        }
        Assert.Equal(invocation, current.Invocation.InvocationId);
        Assert.Equal("selected-v1", current.Invocation.SelectedImplementation);
    }
}
