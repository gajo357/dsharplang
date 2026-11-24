using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class VolatileCancellationTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static (VolatileTargetedStore Store, Guid Invocation) Accepted()
    {
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        store.InitializeClock(Start);
        var id = Guid.NewGuid();
        var request = new TargetedRequest("app", "submission", TargetedOperation.Dispatch, "target", [1, 2], [3], [4]);
        Assert.Equal(TargetedAcceptKind.Created, store.Accept(request, id, "selected-v1").Kind);
        return (store, id);
    }

    private static (Guid Attempt, Guid Token) Claim(VolatileTargetedStore store, Guid invocation, TimeSpan? lease = null)
    {
        var attempt = Guid.NewGuid(); var token = Guid.NewGuid();
        Assert.Equal(AttemptTransitionStatus.Applied, store.Claim("app", "submission", invocation, attempt, token, lease ?? TimeSpan.FromSeconds(10)).Status);
        return (attempt, token);
    }

    [Fact]
    public void EligibleAndDelayedCancellationAreIdempotentAndPreventClaim()
    {
        var (store, id) = Accepted();
        Assert.Equal(CancellationRequestStatus.NotFound, store.RequestCancellation("other", "submission", id).Status);
        var cancelled = store.RequestCancellation("app", "submission", id);
        Assert.Equal(CancellationRequestStatus.Applied, cancelled.Status);
        Assert.Equal(TargetedAttemptState.Cancelled, cancelled.Ownership!.State);
        Assert.Null(cancelled.Ownership.ObservedCancellationExit);
        Assert.Equal(CancellationRequestStatus.AlreadyRequested, store.RequestCancellation("app", "submission", id).Status);
        Assert.Equal(AttemptTransitionStatus.NotEligible, store.Claim("app", "submission", id, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromSeconds(1)).Status);
        Assert.Equal("selected-v1", store.GetOwnership("app", "submission", id)!.Invocation.SelectedImplementation);

        var (delayed, delayedId) = Accepted();
        var (attempt, token) = Claim(delayed, delayedId);
        Assert.Equal(AttemptTransitionStatus.Applied, delayed.RescheduleAfterFault("app", "submission", delayedId, attempt, token, TimeSpan.FromSeconds(10), new TargetedFault([5], [6])).Status);
        var pending = delayed.RequestCancellation("app", "submission", delayedId);
        Assert.Equal(CancellationRequestStatus.Applied, pending.Status);
        Assert.Equal(TargetedAttemptState.Cancelled, pending.Ownership!.State);
        Assert.Equal(AttemptRecoveryKind.ReportedFault, Assert.Single(pending.Ownership.RecoveryHistory).Kind);
        Assert.Equal(new byte[] { 5 }, pending.Ownership.RecoveryHistory[0].Fault!.Payload);
        delayed.AdvanceClock(Start.AddSeconds(10));
        Assert.Equal(AttemptTransitionStatus.NotEligible, delayed.Claim("app", "submission", delayedId, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromSeconds(1)).Status);
    }

    [Fact]
    public void RunningRequestRetainsSeparateCopiedNormalOrReportedFaultExit()
    {
        foreach (var bytes in new byte[][] { [], [0, 69, 82, 82] })
        {
            var (store, id) = Accepted(); var (attempt, token) = Claim(store, id);
            Assert.Equal(CancellationRequestStatus.Applied, store.RequestCancellation("app", "submission", id).Status);
            Assert.Equal(CancellationRequestStatus.AlreadyRequested, store.RequestCancellation("app", "submission", id).Status);
            var pending = store.GetOwnership("app", "submission", id)!;
            Assert.Equal(TargetedAttemptState.Running, pending.State);
            Assert.True(pending.CancellationRequested);
            Assert.Null(pending.ObservedCancellationExit);
            Assert.Equal(AttemptTransitionStatus.InvalidOwner, store.Complete("app", "submission", id, Guid.NewGuid(), token, new TargetedCompletion([], [1])).Status);
            var expected = bytes.ToArray(); byte[] metadata = [8, 9];
            var completion = new TargetedCompletion(bytes, metadata);
            if (bytes.Length != 0) bytes[0] = 99;
            metadata[0] = 99;
            var exit = store.Complete("app", "submission", id, attempt, token, completion);
            Assert.Equal(AttemptTransitionStatus.CancellationFinalized, exit.Status);
            Assert.Equal(TargetedAttemptState.Cancelled, exit.Ownership!.State);
            Assert.Null(exit.Ownership.Completion);
            Assert.Equal(CancellationExitKind.Normal, exit.Ownership.ObservedCancellationExit!.Kind);
            Assert.Equal(expected, exit.Ownership.ObservedCancellationExit.Completion!.Payload);
            Assert.Equal(new byte[] { 8, 9 }, exit.Ownership.ObservedCancellationExit.Completion.ContractMetadata);
            exit.Ownership.ObservedCancellationExit.Completion.ContractMetadata[0] = 77;
            Assert.Equal(new byte[] { 8, 9 }, store.GetOwnership("app", "submission", id)!.ObservedCancellationExit!.Completion!.ContractMetadata);
            Assert.Equal(AttemptTransitionStatus.NotEligible, store.Complete("app", "submission", id, attempt, token, completion).Status);
            Assert.Equal(CancellationRequestStatus.AlreadyRequested, store.RequestCancellation("app", "submission", id).Status);
        }

        var (faultStore, faultId) = Accepted(); var (faultAttempt, faultToken) = Claim(faultStore, faultId);
        var origin = Guid.NewGuid(); byte[] faultBytes = [5, 6];
        var fault = new ReportedTerminalFault(new TargetedFault(faultBytes, [7]), origin);
        faultBytes[0] = 90;
        Assert.Equal(CancellationRequestStatus.Applied, faultStore.RequestCancellation("app", "submission", faultId).Status);
        var observed = faultStore.FailAfterFault("app", "submission", faultId, faultAttempt, faultToken, fault);
        Assert.Equal(AttemptTransitionStatus.CancellationFinalized, observed.Status);
        Assert.Equal(TargetedAttemptState.Cancelled, observed.Ownership!.State);
        Assert.Null(observed.Ownership.TerminalFault);
        Assert.Equal(CancellationExitKind.ReportedFault, observed.Ownership.ObservedCancellationExit!.Kind);
        Assert.Equal(origin, observed.Ownership.ObservedCancellationExit.Fault!.OriginInvocationId);
        Assert.Equal(new byte[] { 5, 6 }, observed.Ownership.ObservedCancellationExit.Fault.Fault.Payload);
        observed.Ownership.ObservedCancellationExit.Fault.Fault.Payload[0] = 22;
        Assert.Equal(new byte[] { 5, 6 }, faultStore.GetOwnership("app", "submission", faultId)!.ObservedCancellationExit!.Fault!.Fault.Payload);
    }

    [Fact]
    public void CancellationPreventsRescheduleAndBothExpiryRecoveryChoicesFromRetrying()
    {
        var (store, id) = Accepted(); var (attempt, token) = Claim(store, id);
        Assert.Equal(CancellationRequestStatus.Applied, store.RequestCancellation("app", "submission", id).Status);
        Assert.Equal(AttemptTransitionStatus.Applied, store.Renew("app", "submission", id, attempt, token, TimeSpan.FromSeconds(20)).Status);
        var observed = store.RescheduleAfterFault("app", "submission", id, attempt, token, TimeSpan.FromSeconds(5), new TargetedFault([7], [8]));
        Assert.Equal(AttemptTransitionStatus.CancellationFinalized, observed.Status);
        Assert.Equal(TargetedAttemptState.Cancelled, observed.Ownership!.State);
        Assert.Equal(CancellationExitKind.ReportedFault, observed.Ownership.ObservedCancellationExit!.Kind);
        Assert.Null(observed.Ownership.ObservedCancellationExit.Fault!.OriginInvocationId);
        Assert.Equal(new byte[] { 7 }, observed.Ownership.ObservedCancellationExit.Fault.Fault.Payload);
        Assert.Empty(observed.Ownership.RecoveryHistory);
        Assert.Equal(AttemptTransitionStatus.NotEligible, store.Claim("app", "submission", id, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromSeconds(1)).Status);

        foreach (var retry in new[] { false, true })
        {
            var (expired, expiredId) = Accepted(); var (oldAttempt, oldToken) = Claim(expired, expiredId);
            var deadline = Start.AddSeconds(10);
            if (!retry) Assert.Equal(CancellationRequestStatus.Applied, expired.RequestCancellation("app", "submission", expiredId).Status);
            expired.AdvanceClock(deadline);
            if (retry) Assert.Equal(CancellationRequestStatus.Applied, expired.RequestCancellation("app", "submission", expiredId).Status);
            Assert.Equal(AttemptTransitionStatus.Expired, expired.Complete("app", "submission", expiredId, oldAttempt, oldToken, new TargetedCompletion([], [1])).Status);
            Assert.Equal(AttemptTransitionStatus.Expired, expired.FailAfterFault("app", "submission", expiredId, oldAttempt, oldToken, new ReportedTerminalFault(new TargetedFault([], [1]))).Status);
            Assert.Equal(AttemptTransitionStatus.Expired, expired.RescheduleAfterFault("app", "submission", expiredId, oldAttempt, oldToken, TimeSpan.Zero, new TargetedFault([], [1])).Status);
            Assert.Equal(AttemptTransitionStatus.ObservationMismatch, expired.RecoverExpiredAsRetry("app", "submission", expiredId, oldAttempt, oldToken, deadline.AddTicks(-1), TimeSpan.Zero).Status);
            var recovery = retry
                ? expired.RecoverExpiredAsRetry("app", "submission", expiredId, oldAttempt, oldToken, deadline, TimeSpan.FromSeconds(1))
                : expired.RecoverExpiredAsFailed("app", "submission", expiredId, oldAttempt, oldToken, deadline);
            Assert.Equal(AttemptTransitionStatus.CancellationFinalized, recovery.Status);
            Assert.Equal(TargetedAttemptState.Cancelled, recovery.Ownership!.State);
            Assert.Null(recovery.Ownership.ObservedCancellationExit);
            Assert.Equal(AttemptTransitionStatus.NotEligible, expired.Claim("app", "submission", expiredId, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromSeconds(1)).Status);
        }

        var (renewed, renewedId) = Accepted(); var (renewedAttempt, renewedToken) = Claim(renewed, renewedId);
        renewed.AdvanceClock(Start.AddSeconds(9));
        Assert.Equal(CancellationRequestStatus.Applied, renewed.RequestCancellation("app", "submission", renewedId).Status);
        Assert.Equal(AttemptTransitionStatus.Applied, renewed.Renew("app", "submission", renewedId, renewedAttempt, renewedToken, TimeSpan.FromSeconds(10)).Status);
        renewed.AdvanceClock(Start.AddSeconds(19));
        Assert.Equal(AttemptTransitionStatus.ObservationMismatch, renewed.RecoverExpiredAsRetry("app", "submission", renewedId, renewedAttempt, renewedToken, Start.AddSeconds(10), TimeSpan.Zero).Status);
        Assert.Equal(AttemptTransitionStatus.ObservationMismatch, renewed.RecoverExpiredAsFailed("app", "submission", renewedId, renewedAttempt, renewedToken, Start.AddSeconds(10)).Status);
        Assert.True(renewed.GetOwnership("app", "submission", renewedId)!.CancellationRequested);
        Assert.Equal(AttemptTransitionStatus.CancellationFinalized, renewed.RecoverExpiredAsRetry("app", "submission", renewedId, renewedAttempt, renewedToken, Start.AddSeconds(19), TimeSpan.Zero).Status);
    }

    [Fact]
    public void TerminalFirstStaysTerminalAndMixedRecoveryHistorySurvivesCancellation()
    {
        foreach (var reportedFault in new[] { false, true })
        {
            var (store, id) = Accepted(); var (attempt, token) = Claim(store, id);
            var terminal = reportedFault
                ? store.FailAfterFault("app", "submission", id, attempt, token, new ReportedTerminalFault(new TargetedFault([1], [2])))
                : store.Complete("app", "submission", id, attempt, token, new TargetedCompletion([0, 69, 82, 82], [2]));
            Assert.Equal(AttemptTransitionStatus.Applied, terminal.Status);
            Assert.Equal(CancellationRequestStatus.AlreadyTerminal, store.RequestCancellation("app", "submission", id).Status);
            Assert.Equal(reportedFault ? TargetedAttemptState.FailedReportedFault : TargetedAttemptState.Completed, store.GetOwnership("app", "submission", id)!.State);
        }
        var (mixed, mixedId) = Accepted(); var (firstAttempt, firstToken) = Claim(mixed, mixedId);
        Assert.Equal(AttemptTransitionStatus.Applied, mixed.RescheduleAfterFault("app", "submission", mixedId, firstAttempt, firstToken, TimeSpan.Zero, new TargetedFault([5], [6])).Status);
        var (secondAttempt, secondToken) = Claim(mixed, mixedId, TimeSpan.FromSeconds(1));
        mixed.AdvanceClock(Start.AddSeconds(1));
        Assert.Equal(AttemptTransitionStatus.Applied, mixed.RecoverExpiredAsRetry("app", "submission", mixedId, secondAttempt, secondToken, Start.AddSeconds(1), TimeSpan.Zero).Status);
        var (thirdAttempt, thirdToken) = Claim(mixed, mixedId);
        Assert.Equal(CancellationRequestStatus.Applied, mixed.RequestCancellation("app", "submission", mixedId).Status);
        var result = mixed.Complete("app", "submission", mixedId, thirdAttempt, thirdToken, new TargetedCompletion([], [1]));
        Assert.Equal(AttemptTransitionStatus.CancellationFinalized, result.Status);
        Assert.Equal(new[] { AttemptRecoveryKind.ReportedFault, AttemptRecoveryKind.LeaseExpired }, result.Ownership!.RecoveryHistory.Select(item => item.Kind));
        Assert.Equal(new byte[] { 5 }, result.Ownership.RecoveryHistory[0].Fault!.Payload);
        Assert.Null(result.Ownership.RecoveryHistory[1].Fault);
        Assert.Equal("selected-v1", result.Ownership.Invocation.SelectedImplementation);
    }

    [Fact]
    public async Task CancellationAndClaimRaceWithoutLosingTheRequest()
    {
        var (store, id) = Accepted();
        using var barrier = new Barrier(2);
        void Signal() { if (!barrier.SignalAndWait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Claim race timed out."); }
        var cancel = Task.Factory.StartNew(() => { Signal(); return store.RequestCancellation("app", "submission", id); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var claim = Task.Factory.StartNew(() => { Signal(); return store.Claim("app", "submission", id, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromSeconds(1)); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        await Task.WhenAll(cancel, claim).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(CancellationRequestStatus.Applied, (await cancel).Status);
        var claimed = await claim;
        var state = store.GetOwnership("app", "submission", id)!;
        if (claimed.Status == AttemptTransitionStatus.Applied)
        {
            Assert.Equal(TargetedAttemptState.Running, state.State);
            Assert.True(state.CancellationRequested);
        }
        else
        {
            Assert.Equal(AttemptTransitionStatus.NotEligible, claimed.Status);
            Assert.Equal(TargetedAttemptState.Cancelled, state.State);
        }
    }

    [Fact]
    public async Task CancellationCompetesWithExitRescheduleClockAndRecoveryWithoutTwoOutcomes()
    {
        var (store, id) = Accepted(); var (attempt, token) = Claim(store, id);
        var deadline = Start.AddSeconds(10);
        store.AdvanceClock(deadline.AddTicks(-1));
        using var barrier = new Barrier(6);
        void Signal() { if (!barrier.SignalAndWait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Cancellation race timed out."); }
        var clock = Task.Factory.StartNew(() => { Signal(); store.AdvanceClock(deadline); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var cancel = Task.Factory.StartNew(() => { Signal(); return store.RequestCancellation("app", "submission", id); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var complete = Task.Factory.StartNew(() => { Signal(); return store.Complete("app", "submission", id, attempt, token, new TargetedCompletion([1], [1])); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var fail = Task.Factory.StartNew(() => { Signal(); return store.FailAfterFault("app", "submission", id, attempt, token, new ReportedTerminalFault(new TargetedFault([2], [1]))); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var retry = Task.Factory.StartNew(() => { Signal(); return store.RescheduleAfterFault("app", "submission", id, attempt, token, TimeSpan.Zero, new TargetedFault([3], [1])); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var recover = Task.Factory.StartNew(() => { Signal(); return store.RecoverExpiredAsRetry("app", "submission", id, attempt, token, deadline, TimeSpan.Zero); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        await Task.WhenAll(clock, cancel, complete, fail, retry, recover).WaitAsync(TimeSpan.FromSeconds(30));
        var cancellation = await cancel; var completion = await complete; var failure = await fail; var reschedule = await retry; var recovery = await recover;
        var transitions = new[] { completion, failure, reschedule, recovery };
        Assert.True(transitions.Count(item => item.Status is AttemptTransitionStatus.Applied or AttemptTransitionStatus.CancellationFinalized) <= 1);
        var state = store.GetOwnership("app", "submission", id)!;
        if (state.State == TargetedAttemptState.Cancelled)
        {
            Assert.Equal(CancellationRequestStatus.Applied, cancellation.Status);
            Assert.Null(state.Completion); Assert.Null(state.TerminalFault);
            Assert.Equal(AttemptTransitionStatus.NotEligible, store.Claim("app", "submission", id, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromSeconds(1)).Status);
        }
        else if (state.State == TargetedAttemptState.Running)
        {
            Assert.Equal(CancellationRequestStatus.Applied, cancellation.Status);
            Assert.True(state.CancellationRequested);
        }
        else
        {
            Assert.True(state.State is TargetedAttemptState.Completed or TargetedAttemptState.FailedReportedFault);
            Assert.Equal(CancellationRequestStatus.AlreadyTerminal, cancellation.Status);
        }
        Assert.Equal(id, state.Invocation.InvocationId);
        Assert.Equal("selected-v1", state.Invocation.SelectedImplementation);
    }
}
