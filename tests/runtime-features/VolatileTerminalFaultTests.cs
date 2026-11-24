using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class VolatileTerminalFaultTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static (VolatileTargetedStore Store, Guid Invocation, Guid Attempt, Guid Token) Running()
    {
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        store.InitializeClock(Start);
        var invocation = Guid.NewGuid(); var attempt = Guid.NewGuid(); var token = Guid.NewGuid();
        var request = new TargetedRequest("app", "submission", TargetedOperation.Dispatch, "target", [1, 2], [3], [4]);
        Assert.Equal(TargetedAcceptKind.Created, store.Accept(request, invocation, "selected-v1").Kind);
        Assert.Equal(AttemptTransitionStatus.Applied, store.Claim("app", "submission", invocation, attempt, token, TimeSpan.FromSeconds(10)).Status);
        return (store, invocation, attempt, token);
    }

    [Fact]
    public void CallerOriginAndOpaqueFaultAreRetainedWithoutInferenceOrMutableBuffers()
    {
        foreach (var suppliedOrigin in new Guid?[] { null, Guid.NewGuid() })
        {
            var (store, invocation, attempt, token) = Running();
            byte[] data = [10, 11], metadata = [20, 21];
            var opaque = new TargetedFault(data, metadata);
            data[0] = 90; metadata[0] = 91;
            opaque.Payload[1] = 80; opaque.ContractMetadata[1] = 81;
            var reported = new ReportedTerminalFault(opaque, suppliedOrigin);
            var accepted = store.FailAfterFault("app", "submission", invocation, attempt, token, reported);
            Assert.Equal(AttemptTransitionStatus.Applied, accepted.Status);
            var snapshot = accepted.Ownership!;
            Assert.Equal(TargetedAttemptState.FailedReportedFault, snapshot.State);
            Assert.Equal(invocation, snapshot.Invocation.InvocationId);
            Assert.Equal("selected-v1", snapshot.Invocation.SelectedImplementation);
            Assert.Equal(new byte[] { 1, 2 }, snapshot.Invocation.Request.Payload);
            Assert.Equal(attempt, snapshot.AttemptId); Assert.Equal(token, snapshot.Token);
            Assert.Equal(suppliedOrigin, snapshot.TerminalFault!.OriginInvocationId);
            Assert.Equal(new byte[] { 10, 11 }, snapshot.TerminalFault.Fault.Payload);
            Assert.Equal(new byte[] { 20, 21 }, snapshot.TerminalFault.Fault.ContractMetadata);
            snapshot.TerminalFault.Fault.Payload[0] = 70;
            snapshot.TerminalFault.Fault.ContractMetadata[0] = 71;
            var later = store.GetOwnership("app", "submission", invocation)!;
            Assert.Equal(suppliedOrigin, later.TerminalFault!.OriginInvocationId);
            Assert.Equal(new byte[] { 10, 11 }, later.TerminalFault.Fault.Payload);
            Assert.Equal(new byte[] { 20, 21 }, later.TerminalFault.Fault.ContractMetadata);
            Assert.Null(later.Completion);
        }
        Assert.Throws<ArgumentNullException>(() => new ReportedTerminalFault(null!));
        Assert.ThrowsAny<ArgumentException>(() => new ReportedTerminalFault(new TargetedFault([], [1]), Guid.Empty));
    }

    [Fact]
    public void ExactOwnerAndLeaseFenceFaultAndAllTerminalStatesRemainDistinct()
    {
        var (store, invocation, attempt, token) = Running();
        var fault = new ReportedTerminalFault(new TargetedFault([10], [20]));
        Assert.Throws<ArgumentNullException>(() => store.FailAfterFault("app", "submission", invocation, attempt, token, null!));
        Assert.Equal(AttemptTransitionStatus.NotFound, store.FailAfterFault("other", "submission", invocation, attempt, token, fault).Status);
        Assert.Equal(AttemptTransitionStatus.NotFound, store.FailAfterFault("app", "other", invocation, attempt, token, fault).Status);
        Assert.Equal(AttemptTransitionStatus.NotFound, store.FailAfterFault("app", "submission", Guid.NewGuid(), attempt, token, fault).Status);
        Assert.Equal(AttemptTransitionStatus.InvalidOwner, store.FailAfterFault("app", "submission", invocation, Guid.NewGuid(), token, fault).Status);
        Assert.Equal(AttemptTransitionStatus.InvalidOwner, store.FailAfterFault("app", "submission", invocation, attempt, Guid.NewGuid(), fault).Status);
        store.AdvanceClock(Start.AddSeconds(10).AddTicks(-1));
        Assert.Equal(AttemptTransitionStatus.Applied, store.FailAfterFault("app", "submission", invocation, attempt, token, fault).Status);
        Assert.Equal(AttemptTransitionStatus.NotEligible, store.FailAfterFault("app", "submission", invocation, attempt, token, fault).Status);
        Assert.Equal(AttemptTransitionStatus.NotEligible, store.FailAfterFault("app", "submission", invocation, attempt, token, new ReportedTerminalFault(new TargetedFault([99], [99]))).Status);
        Assert.Equal(AttemptTransitionStatus.NotEligible, store.Complete("app", "submission", invocation, attempt, token, new TargetedCompletion([], [1])).Status);
        Assert.Equal(AttemptTransitionStatus.NotEligible, store.RescheduleAfterFault("app", "submission", invocation, attempt, token, TimeSpan.Zero, new TargetedFault([], [1])).Status);
        Assert.Equal(AttemptTransitionStatus.NotEligible, store.Renew("app", "submission", invocation, attempt, token, TimeSpan.FromSeconds(1)).Status);
        Assert.Equal(AttemptTransitionStatus.NotEligible, store.Claim("app", "submission", invocation, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromSeconds(1)).Status);
        store.AdvanceClock(Start.AddSeconds(10));
        Assert.Equal(AttemptTransitionStatus.NotEligible, store.RecoverExpiredAsRetry("app", "submission", invocation, attempt, token, Start.AddSeconds(10), TimeSpan.Zero).Status);
        Assert.Equal(AttemptTransitionStatus.NotEligible, store.RecoverExpiredAsFailed("app", "submission", invocation, attempt, token, Start.AddSeconds(10)).Status);
        Assert.Equal(new byte[] { 10 }, store.GetOwnership("app", "submission", invocation)!.TerminalFault!.Fault.Payload);

        var (expiredStore, expiredId, expiredAttempt, expiredToken) = Running();
        expiredStore.AdvanceClock(Start.AddSeconds(10));
        Assert.Equal(AttemptTransitionStatus.Expired, expiredStore.FailAfterFault("app", "submission", expiredId, expiredAttempt, expiredToken, fault).Status);
        Assert.Equal(AttemptTransitionStatus.Applied, expiredStore.RecoverExpiredAsFailed("app", "submission", expiredId, expiredAttempt, expiredToken, Start.AddSeconds(10)).Status);
        Assert.Equal(AttemptTransitionStatus.NotEligible, expiredStore.FailAfterFault("app", "submission", expiredId, expiredAttempt, expiredToken, fault).Status);
        var lost = expiredStore.GetOwnership("app", "submission", expiredId)!;
        Assert.Equal(TargetedAttemptState.FailedLeaseExpired, lost.State);
        Assert.Null(lost.TerminalFault);
        var (completedStore, completedId, completedAttempt, completedToken) = Running();
        Assert.Equal(AttemptTransitionStatus.Applied, completedStore.Complete("app", "submission", completedId, completedAttempt, completedToken, new TargetedCompletion([0, 69, 82, 82], [1])).Status);
        Assert.Equal(AttemptTransitionStatus.NotEligible, completedStore.FailAfterFault("app", "submission", completedId, completedAttempt, completedToken, fault).Status);
        var normal = completedStore.GetOwnership("app", "submission", completedId)!;
        Assert.Equal(TargetedAttemptState.Completed, normal.State);
        Assert.Null(normal.TerminalFault);
        Assert.Equal(new byte[] { 0, 69, 82, 82 }, normal.Completion!.Payload);
    }

    [Fact]
    public void PriorFaultAndLeaseLossHistorySurviveTerminalReportedFault()
    {
        var (store, invocation, attempt1, token1) = Running();
        Assert.Equal(AttemptTransitionStatus.Applied, store.RescheduleAfterFault("app", "submission", invocation, attempt1, token1, TimeSpan.Zero, new TargetedFault([5], [6])).Status);
        var attempt2 = Guid.NewGuid(); var token2 = Guid.NewGuid();
        Assert.Equal(AttemptTransitionStatus.Applied, store.Claim("app", "submission", invocation, attempt2, token2, TimeSpan.FromSeconds(1)).Status);
        store.AdvanceClock(Start.AddSeconds(1));
        Assert.Equal(AttemptTransitionStatus.Applied, store.RecoverExpiredAsRetry("app", "submission", invocation, attempt2, token2, Start.AddSeconds(1), TimeSpan.Zero).Status);
        var attempt3 = Guid.NewGuid(); var token3 = Guid.NewGuid();
        Assert.Equal(AttemptTransitionStatus.Applied, store.Claim("app", "submission", invocation, attempt3, token3, TimeSpan.FromSeconds(1)).Status);
        var fault = new ReportedTerminalFault(new TargetedFault([8], [9]));
        Assert.Equal(AttemptTransitionStatus.InvalidOwner, store.FailAfterFault("app", "submission", invocation, attempt1, token1, fault).Status);
        var final = store.FailAfterFault("app", "submission", invocation, attempt3, token3, fault);
        Assert.Equal(AttemptTransitionStatus.Applied, final.Status);
        Assert.Equal(new[] { AttemptRecoveryKind.ReportedFault, AttemptRecoveryKind.LeaseExpired }, final.Ownership!.RecoveryHistory.Select(item => item.Kind));
        Assert.Equal(new[] { attempt1, attempt2 }, final.Ownership.RecoveryHistory.Select(item => item.AttemptId));
        Assert.Equal(new byte[] { 5 }, final.Ownership.RecoveryHistory[0].Fault!.Payload);
        Assert.Null(final.Ownership.RecoveryHistory[1].Fault);
        Assert.Null(final.Ownership.TerminalFault!.OriginInvocationId);
        Assert.Equal(new byte[] { 8 }, final.Ownership.TerminalFault.Fault.Payload);
    }

    [Fact]
    public async Task ConcurrentTerminalFaultsChooseExactlyOneOpaqueResult()
    {
        const int count = 12;
        var (store, invocation, attempt, token) = Running();
        using var barrier = new Barrier(count);
        var tasks = Enumerable.Range(0, count).Select(i => Task.Factory.StartNew(() =>
        {
            if (!barrier.SignalAndWait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Fault barrier timed out.");
            return (Value: (byte)i, Result: store.FailAfterFault("app", "submission", invocation, attempt, token, new ReportedTerminalFault(new TargetedFault([(byte)i], [1]))));
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
        var results = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(30));
        var winner = Assert.Single(results, item => item.Result.Status == AttemptTransitionStatus.Applied);
        Assert.Equal(count - 1, results.Count(item => item.Result.Status == AttemptTransitionStatus.NotEligible));
        Assert.Equal(new[] { winner.Value }, store.GetOwnership("app", "submission", invocation)!.TerminalFault!.Fault.Payload);
    }

    [Fact]
    public async Task FaultCompletionRescheduleRenewalClockAndRecoveryRaceCoherently()
    {
        var (store, invocation, attempt, token) = Running();
        var deadline = Start.AddSeconds(10);
        store.AdvanceClock(deadline.AddTicks(-1));
        using var barrier = new Barrier(6);
        void Signal() { if (!barrier.SignalAndWait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Fault race barrier timed out."); }
        var clock = Task.Factory.StartNew(() => { Signal(); store.AdvanceClock(deadline); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var fail = Task.Factory.StartNew(() => { Signal(); return store.FailAfterFault("app", "submission", invocation, attempt, token, new ReportedTerminalFault(new TargetedFault([1], [1]))); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var complete = Task.Factory.StartNew(() => { Signal(); return store.Complete("app", "submission", invocation, attempt, token, new TargetedCompletion([2], [1])); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var retry = Task.Factory.StartNew(() => { Signal(); return store.RescheduleAfterFault("app", "submission", invocation, attempt, token, TimeSpan.Zero, new TargetedFault([3], [1])); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var renew = Task.Factory.StartNew(() => { Signal(); return store.Renew("app", "submission", invocation, attempt, token, TimeSpan.FromSeconds(10)); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var recover = Task.Factory.StartNew(() => { Signal(); return store.RecoverExpiredAsRetry("app", "submission", invocation, attempt, token, deadline, TimeSpan.Zero); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        await Task.WhenAll(clock, fail, complete, retry, renew, recover).WaitAsync(TimeSpan.FromSeconds(30));
        var f = await fail; var c = await complete; var r = await retry; var x = await recover;
        Assert.True(new[] { f, c, r, x }.Count(item => item.Status == AttemptTransitionStatus.Applied) <= 1);
        var state = store.GetOwnership("app", "submission", invocation)!;
        if (f.Status == AttemptTransitionStatus.Applied)
        {
            Assert.Equal(TargetedAttemptState.FailedReportedFault, state.State);
            Assert.Equal(new byte[] { 1 }, state.TerminalFault!.Fault.Payload);
        }
        else if (c.Status == AttemptTransitionStatus.Applied)
        {
            Assert.Equal(TargetedAttemptState.Completed, state.State);
            Assert.Equal(new byte[] { 2 }, state.Completion!.Payload);
        }
        else if (r.Status == AttemptTransitionStatus.Applied || x.Status == AttemptTransitionStatus.Applied)
        {
            Assert.Equal(TargetedAttemptState.Eligible, state.State);
            Assert.Single(state.RecoveryHistory);
            Assert.Equal(r.Status == AttemptTransitionStatus.Applied ? AttemptRecoveryKind.ReportedFault : AttemptRecoveryKind.LeaseExpired, state.RecoveryHistory[0].Kind);
        }
        else
        {
            Assert.Equal(TargetedAttemptState.Running, state.State);
            Assert.Equal(AttemptTransitionStatus.Expired, f.Status);
        }
        Assert.Equal(invocation, state.Invocation.InvocationId);
        Assert.Equal("selected-v1", state.Invocation.SelectedImplementation);
    }
}
