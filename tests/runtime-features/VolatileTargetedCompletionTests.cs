using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class VolatileTargetedCompletionTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static (VolatileTargetedStore Store, Guid Invocation, Guid Attempt, Guid Token) Running()
    {
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        store.InitializeClock(Start);
        var invocation = Guid.NewGuid();
        var attempt = Guid.NewGuid();
        var token = Guid.NewGuid();
        var request = new TargetedRequest("app", "submission", TargetedOperation.Dispatch, "target", [1, 2], [3], [4]);
        Assert.Equal(TargetedAcceptKind.Created, store.Accept(request, invocation, "selected-v1").Kind);
        Assert.Equal(AttemptTransitionStatus.Applied, store.Claim("app", "submission", invocation, attempt, token, TimeSpan.FromSeconds(10)).Status);
        return (store, invocation, attempt, token);
    }

    [Fact]
    public void OpaqueNormalValuesAndCopiedBuffersRemainExact()
    {
        // These opaque bytes stand for plain, Unit-like and business-error-like
        // values; the reference does not claim to decode a D# result.
        byte[][] values = [[42], [], [0, 69, 82, 82]];
        foreach (var value in values)
        {
            var (store, invocation, attempt, token) = Running();
            var expected = value.ToArray();
            byte[] metadata = [7, 8];
            var result = new TargetedCompletion(value, metadata);
            if (value.Length != 0) value[0] ^= 0xff;
            metadata[0] = 99;
            if (result.Payload.Length != 0) result.Payload[0] = 88;
            result.ContractMetadata[0] = 77;
            var completed = store.Complete("app", "submission", invocation, attempt, token, result);
            Assert.Equal(AttemptTransitionStatus.Applied, completed.Status);
            Assert.Equal(TargetedAttemptState.Completed, completed.Ownership!.State);
            Assert.Equal(invocation, completed.Ownership.Invocation.InvocationId);
            Assert.Equal("selected-v1", completed.Ownership.Invocation.SelectedImplementation);
            Assert.Equal(new byte[] { 1, 2 }, completed.Ownership.Invocation.Request.Payload);
            Assert.Equal(attempt, completed.Ownership.AttemptId);
            Assert.Equal(token, completed.Ownership.Token);
            Assert.Equal(expected, completed.Ownership.Completion!.Payload);
            Assert.Equal(new byte[] { 7, 8 }, completed.Ownership.Completion.ContractMetadata);
            if (completed.Ownership.Completion.Payload.Length != 0) completed.Ownership.Completion.Payload[0] = 66;
            completed.Ownership.Completion.ContractMetadata[0] = 55;
            var inspected = store.GetOwnership("app", "submission", invocation)!;
            Assert.Equal(TargetedAttemptState.Completed, inspected.State);
            Assert.Equal(expected, inspected.Completion!.Payload);
            Assert.Equal(new byte[] { 7, 8 }, inspected.Completion.ContractMetadata);
        }
        Assert.Throws<ArgumentNullException>(() => new TargetedCompletion(null!, [1]));
        Assert.Throws<ArgumentNullException>(() => new TargetedCompletion([], null!));
        Assert.ThrowsAny<ArgumentException>(() => new TargetedCompletion([], []));
    }

    [Fact]
    public void IdentityAndStateFencingPreserveOneTerminalResult()
    {
        var waiting = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        waiting.InitializeClock(Start);
        var waitingId = Guid.NewGuid();
        var request = new TargetedRequest("app", "waiting", TargetedOperation.Dispatch, "target", [1], [2], [3]);
        Assert.Equal(TargetedAcceptKind.Created, waiting.Accept(request, waitingId, "selected").Kind);
        Assert.Equal(AttemptTransitionStatus.NotEligible,
            waiting.Complete("app", "waiting", waitingId, Guid.NewGuid(), Guid.NewGuid(), new TargetedCompletion([10], [20])).Status);
        Assert.Equal(TargetedAttemptState.Eligible, waiting.GetOwnership("app", "waiting", waitingId)!.State);
        Assert.Equal(PublicationAcceptKind.Created,
            waiting.AcceptPublication(new PublicationRequest("app", "publication", [1], [2], []), waitingId, []).Kind);
        Assert.Equal(AttemptTransitionStatus.NotFound,
            waiting.Complete("app", "publication", waitingId, Guid.NewGuid(), Guid.NewGuid(), new TargetedCompletion([10], [20])).Status);

        var (store, invocation, attempt, token) = Running();
        var result = new TargetedCompletion([10], [20]);
        Assert.Throws<ArgumentNullException>(() => store.Complete("app", "submission", invocation, attempt, token, null!));
        Assert.Equal(AttemptTransitionStatus.NotFound, store.Complete("other", "submission", invocation, attempt, token, result).Status);
        Assert.Equal(AttemptTransitionStatus.NotFound, store.Complete("app", "other", invocation, attempt, token, result).Status);
        Assert.Equal(AttemptTransitionStatus.NotFound, store.Complete("app", "submission", Guid.NewGuid(), attempt, token, result).Status);
        Assert.Equal(AttemptTransitionStatus.InvalidOwner, store.Complete("app", "submission", invocation, Guid.NewGuid(), token, result).Status);
        Assert.Equal(AttemptTransitionStatus.InvalidOwner, store.Complete("app", "submission", invocation, attempt, Guid.NewGuid(), result).Status);
        Assert.Equal(TargetedAttemptState.Running, store.GetOwnership("app", "submission", invocation)!.State);
        store.AdvanceClock(Start.AddSeconds(10).AddTicks(-1));
        Assert.Equal(AttemptTransitionStatus.Applied, store.Complete("app", "submission", invocation, attempt, token, result).Status);
        Assert.Equal(AttemptTransitionStatus.NotEligible, store.Complete("app", "submission", invocation, attempt, token, result).Status);
        Assert.Equal(AttemptTransitionStatus.NotEligible, store.Complete("app", "submission", invocation, attempt, token, new TargetedCompletion([99], [99])).Status);
        store.AdvanceClock(Start.AddSeconds(10));
        Assert.Equal(AttemptTransitionStatus.NotEligible, store.RecoverExpiredAsFailed("app", "submission", invocation, attempt, token, Start.AddSeconds(10)).Status);
        Assert.Equal(new byte[] { 10 }, store.GetOwnership("app", "submission", invocation)!.Completion!.Payload);

        var (failedStore, failedId, failedAttempt, failedToken) = Running();
        failedStore.AdvanceClock(Start.AddSeconds(10));
        Assert.Equal(AttemptTransitionStatus.Expired, failedStore.Complete("app", "submission", failedId, failedAttempt, failedToken, result).Status);
        Assert.Equal(AttemptTransitionStatus.Applied, failedStore.RecoverExpiredAsFailed("app", "submission", failedId, failedAttempt, failedToken, Start.AddSeconds(10)).Status);
        Assert.Equal(AttemptTransitionStatus.NotEligible, failedStore.Complete("app", "submission", failedId, failedAttempt, failedToken, result).Status);
        var failed = failedStore.GetOwnership("app", "submission", failedId)!;
        Assert.Equal(TargetedAttemptState.FailedLeaseExpired, failed.State);
        Assert.Null(failed.Completion);
    }

    [Fact]
    public void RenewalAllowsCompletionBeforeNewDeadlineButNotAtIt()
    {
        var (store, invocation, attempt, token) = Running();
        store.AdvanceClock(Start.AddSeconds(9));
        Assert.Equal(AttemptTransitionStatus.Applied, store.Renew("app", "submission", invocation, attempt, token, TimeSpan.FromSeconds(10)).Status);
        store.AdvanceClock(Start.AddSeconds(10));
        Assert.Equal(AttemptTransitionStatus.ObservationMismatch, store.RecoverExpiredAsFailed("app", "submission", invocation, attempt, token, Start.AddSeconds(10)).Status);
        Assert.Equal(AttemptTransitionStatus.Applied, store.Complete("app", "submission", invocation, attempt, token, new TargetedCompletion([], [1])).Status);
        var (expiredStore, expiredId, expiredAttempt, expiredToken) = Running();
        expiredStore.AdvanceClock(Start.AddSeconds(9));
        Assert.Equal(AttemptTransitionStatus.Applied, expiredStore.Renew("app", "submission", expiredId, expiredAttempt, expiredToken, TimeSpan.FromSeconds(10)).Status);
        expiredStore.AdvanceClock(Start.AddSeconds(19));
        Assert.Equal(AttemptTransitionStatus.Expired, expiredStore.Complete("app", "submission", expiredId, expiredAttempt, expiredToken, new TargetedCompletion([], [1])).Status);
        Assert.Equal(TargetedAttemptState.Running, expiredStore.GetOwnership("app", "submission", expiredId)!.State);
    }

    [Fact]
    public async Task ConcurrentCompletionsChooseExactlyOneResult()
    {
        const int count = 12;
        var (store, invocation, attempt, token) = Running();
        using var barrier = new Barrier(count);
        var calls = Enumerable.Range(0, count).Select(i => Task.Factory.StartNew(() =>
        {
            if (!barrier.SignalAndWait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Completion barrier timed out.");
            return (Value: (byte)i, Result: store.Complete("app", "submission", invocation, attempt, token, new TargetedCompletion([(byte)i], [1])));
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
        var results = await Task.WhenAll(calls).WaitAsync(TimeSpan.FromSeconds(30));
        var winner = Assert.Single(results, item => item.Result.Status == AttemptTransitionStatus.Applied);
        Assert.Equal(count - 1, results.Count(item => item.Result.Status == AttemptTransitionStatus.NotEligible));
        Assert.Equal(new[] { winner.Value }, store.GetOwnership("app", "submission", invocation)!.Completion!.Payload);
    }

    [Fact]
    public async Task CompletionRenewalExpiryAndRecoveryRaceToOneCoherentOutcome()
    {
        var (store, invocation, attempt, token) = Running();
        var deadline = Start.AddSeconds(10);
        store.AdvanceClock(deadline.AddTicks(-1));
        using var barrier = new Barrier(4);
        void Signal() { if (!barrier.SignalAndWait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Completion race barrier timed out."); }
        var advance = Task.Factory.StartNew(() => { Signal(); store.AdvanceClock(deadline); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var complete = Task.Factory.StartNew(() => { Signal(); return store.Complete("app", "submission", invocation, attempt, token, new TargetedCompletion([42], [1])); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var renew = Task.Factory.StartNew(() => { Signal(); return store.Renew("app", "submission", invocation, attempt, token, TimeSpan.FromSeconds(20)); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var recover = Task.Factory.StartNew(() => { Signal(); return store.RecoverExpiredAsFailed("app", "submission", invocation, attempt, token, deadline); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        await Task.WhenAll(advance, complete, renew, recover).WaitAsync(TimeSpan.FromSeconds(30));
        var completion = await complete;
        var renewal = await renew;
        var recovery = await recover;
        var current = store.GetOwnership("app", "submission", invocation)!;
        Assert.False(completion.Status == AttemptTransitionStatus.Applied && recovery.Status == AttemptTransitionStatus.Applied);
        if (completion.Status == AttemptTransitionStatus.Applied)
        {
            Assert.Equal(TargetedAttemptState.Completed, current.State);
            Assert.Equal(new byte[] { 42 }, current.Completion!.Payload);
        }
        else if (recovery.Status == AttemptTransitionStatus.Applied)
        {
            Assert.Equal(TargetedAttemptState.FailedLeaseExpired, current.State);
            Assert.Null(current.Completion);
        }
        else
        {
            Assert.Equal(TargetedAttemptState.Running, current.State);
            Assert.Null(current.Completion);
            Assert.True(completion.Status is AttemptTransitionStatus.Expired);
            if (renewal.Status == AttemptTransitionStatus.Applied) Assert.True(current.LeaseUntil > deadline);
        }
        Assert.Equal(invocation, current.Invocation.InvocationId);
        Assert.Equal(attempt, current.AttemptId);
        Assert.Equal(token, current.Token);
    }
}
