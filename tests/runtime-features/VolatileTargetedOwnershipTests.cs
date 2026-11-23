using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class VolatileTargetedOwnershipTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static Guid Accept(VolatileTargetedStore store, string submission = "submission")
    {
        var id = Guid.NewGuid();
        var request = new TargetedRequest("app", submission, TargetedOperation.Dispatch, "target", [1], [2], [3]);
        Assert.Equal(TargetedAcceptKind.Created, store.Accept(request, id, "implementation-v1").Kind);
        return id;
    }

    private static void AssertRunning(TargetedOwnership? ownership, Guid invocation, Guid attempt, Guid token, DateTimeOffset deadline)
    {
        Assert.NotNull(ownership);
        Assert.Equal(TargetedAttemptState.Running, ownership.State);
        Assert.Equal(invocation, ownership.Invocation.InvocationId);
        Assert.Equal("implementation-v1", ownership.Invocation.SelectedImplementation);
        Assert.Equal(attempt, ownership.AttemptId);
        Assert.Equal(token, ownership.Token);
        Assert.Equal(deadline, ownership.LeaseUntil);
    }

    [Fact]
    public void ExplicitClockIsMonotonicAndInvalidDurationsAndOverflowLeaveOwnershipUnchanged()
    {
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        var id = Accept(store);
        Assert.Throws<InvalidOperationException>(() => store.GetOwnership("app", "submission", id));
        Assert.Throws<InvalidOperationException>(() => store.Claim("app", "submission", id, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromSeconds(1)));
        Assert.Throws<InvalidOperationException>(() => _ = store.CurrentTime);
        store.InitializeClock(Start);
        Assert.Equal(Start, store.CurrentTime);
        Assert.Throws<InvalidOperationException>(() => store.InitializeClock(Start));
        Assert.ThrowsAny<ArgumentException>(() => store.AdvanceClock(Start.AddTicks(-1)));
        Assert.Equal(Start, store.CurrentTime);
        store.AdvanceClock(Start.AddSeconds(1));
        Assert.Equal(Start.AddSeconds(1), store.CurrentTime);

        var before = store.GetOwnership("app", "submission", id)!;
        Assert.Equal(TargetedAttemptState.Eligible, before.State);
        Assert.ThrowsAny<ArgumentException>(() => store.Claim("app", "submission", id, Guid.Empty, Guid.NewGuid(), TimeSpan.FromSeconds(1)));
        Assert.ThrowsAny<ArgumentException>(() => store.Claim("app", "submission", id, Guid.NewGuid(), Guid.Empty, TimeSpan.FromSeconds(1)));
        Assert.ThrowsAny<ArgumentException>(() => store.Claim("app", "submission", id, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.Zero));
        Assert.ThrowsAny<ArgumentException>(() => store.Claim("app", "submission", id, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromTicks(-1)));
        Assert.ThrowsAny<ArgumentException>(() => store.Claim("app", "submission", id, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.MaxValue));
        Assert.Equal(before, store.GetOwnership("app", "submission", id));
        store.AdvanceClock(DateTimeOffset.MaxValue.AddTicks(-1));
        Assert.ThrowsAny<ArgumentException>(() => store.Claim("app", "submission", id, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromTicks(2)));
        Assert.Equal(before, store.GetOwnership("app", "submission", id));
    }

    [Fact]
    public void OnlyAcceptedTargetedInvocationsCanBeClaimedAndSelectionStaysPinned()
    {
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        store.InitializeClock(Start);
        var id = Accept(store);
        // Publication and invocation identities occupy separate domains and may
        // have the same raw GUID. The key must identify a targeted submission.
        var publicationId = id;
        Assert.Equal(PublicationAcceptKind.Created, store.AcceptPublication(new PublicationRequest("app", "publication", [1], [2], []), publicationId, []).Kind);
        Assert.Null(store.GetOwnership("app", "publication", publicationId));
        Assert.Null(store.GetOwnership("app", "submission", Guid.NewGuid()));
        Assert.Equal(AttemptTransitionStatus.NotFound, store.Claim("app", "publication", publicationId, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromSeconds(1)).Status);
        Assert.Equal(AttemptTransitionStatus.NotFound, store.Claim("app", "submission", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromSeconds(1)).Status);
        var attempt = Guid.NewGuid();
        var token = Guid.NewGuid();
        var result = store.Claim("app", "submission", id, attempt, token, TimeSpan.FromSeconds(10));
        Assert.Equal(AttemptTransitionStatus.Applied, result.Status);
        AssertRunning(result.Ownership, id, attempt, token, Start.AddSeconds(10));
        Assert.Equal("target", result.Ownership!.Invocation.Request.RequestedTarget);
        Assert.Equal(new byte[] { 1 }, result.Ownership.Invocation.Request.Payload);
        Assert.Equal(AttemptTransitionStatus.NotEligible, store.Claim("app", "submission", id, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromSeconds(10)).Status);
        AssertRunning(store.GetOwnership("app", "submission", id), id, attempt, token, Start.AddSeconds(10));
    }

    [Fact]
    public void RenewalFencesOldObservationsAndExactExpiryRequiresExplicitTerminalFailure()
    {
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        store.InitializeClock(Start);
        var id = Accept(store);
        var attempt = Guid.NewGuid();
        var token = Guid.NewGuid();
        var oldDeadline = Start.AddSeconds(10);
        Assert.Equal(AttemptTransitionStatus.Applied, store.Claim("app", "submission", id, attempt, token, TimeSpan.FromSeconds(10)).Status);
        Assert.Equal(AttemptTransitionStatus.InvalidOwner, store.Renew("app", "submission", id, attempt, Guid.NewGuid(), TimeSpan.FromSeconds(20)).Status);
        Assert.Equal(AttemptTransitionStatus.InvalidOwner, store.Renew("app", "submission", id, Guid.NewGuid(), token, TimeSpan.FromSeconds(20)).Status);
        Assert.Equal(AttemptTransitionStatus.NotExtended, store.Renew("app", "submission", id, attempt, token, TimeSpan.FromSeconds(10)).Status);
        Assert.ThrowsAny<ArgumentException>(() => store.Renew("app", "submission", id, attempt, token, TimeSpan.Zero));
        AssertRunning(store.GetOwnership("app", "submission", id), id, attempt, token, oldDeadline);
        store.AdvanceClock(oldDeadline.AddTicks(-1));
        Assert.Equal(AttemptTransitionStatus.NotExpired, store.RecoverExpiredAsFailed("app", "submission", id, attempt, token, oldDeadline).Status);
        var renewed = store.Renew("app", "submission", id, attempt, token, TimeSpan.FromSeconds(10));
        var newDeadline = oldDeadline.AddTicks(-1).AddSeconds(10);
        Assert.Equal(AttemptTransitionStatus.Applied, renewed.Status);
        AssertRunning(renewed.Ownership, id, attempt, token, newDeadline);
        store.AdvanceClock(oldDeadline);
        Assert.Equal(AttemptTransitionStatus.ObservationMismatch, store.RecoverExpiredAsFailed("app", "submission", id, attempt, token, oldDeadline).Status);
        AssertRunning(store.GetOwnership("app", "submission", id), id, attempt, token, newDeadline);
        store.AdvanceClock(newDeadline.AddTicks(-1));
        Assert.Equal(AttemptTransitionStatus.NotExpired, store.RecoverExpiredAsFailed("app", "submission", id, attempt, token, newDeadline).Status);
        store.AdvanceClock(newDeadline);
        Assert.Equal(AttemptTransitionStatus.Expired, store.Renew("app", "submission", id, attempt, token, TimeSpan.FromSeconds(10)).Status);
        Assert.Equal(AttemptTransitionStatus.NotEligible, store.Claim("app", "submission", id, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromSeconds(10)).Status);
        Assert.Equal(AttemptTransitionStatus.ObservationMismatch, store.RecoverExpiredAsFailed("app", "submission", id, attempt, token, oldDeadline).Status);
        AssertRunning(store.GetOwnership("app", "submission", id), id, attempt, token, newDeadline);
        Assert.Equal(AttemptTransitionStatus.ObservationMismatch, store.RecoverExpiredAsFailed("app", "submission", id, attempt, Guid.NewGuid(), newDeadline).Status);
        var failed = store.RecoverExpiredAsFailed("app", "submission", id, attempt, token, newDeadline);
        Assert.Equal(AttemptTransitionStatus.Applied, failed.Status);
        Assert.Equal(TargetedAttemptState.FailedLeaseExpired, failed.Ownership!.State);
        Assert.Equal(id, failed.Ownership.Invocation.InvocationId);
        Assert.Equal("implementation-v1", failed.Ownership.Invocation.SelectedImplementation);
        Assert.Equal(attempt, failed.Ownership.AttemptId);
        Assert.Equal(token, failed.Ownership.Token);
        Assert.Equal(AttemptTransitionStatus.NotEligible, store.Claim("app", "submission", id, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromSeconds(10)).Status);
        Assert.Equal(TargetedAttemptState.FailedLeaseExpired, store.GetOwnership("app", "submission", id)!.State);
    }

    [Fact]
    public void AttemptAndOwnershipIdsCannotBeReusedAcrossInvocationsOrRoles()
    {
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        store.InitializeClock(Start);
        var first = Accept(store);
        var second = Accept(store, "second");
        var attempt = Guid.NewGuid();
        var token = Guid.NewGuid();
        Assert.Equal(AttemptTransitionStatus.Applied, store.Claim("app", "submission", first, attempt, token, TimeSpan.FromSeconds(1)).Status);
        Assert.Equal(AttemptTransitionStatus.IdentityCollision, store.Claim("app", "second", second, attempt, Guid.NewGuid(), TimeSpan.FromSeconds(1)).Status);
        Assert.Equal(AttemptTransitionStatus.IdentityCollision, store.Claim("app", "second", second, Guid.NewGuid(), token, TimeSpan.FromSeconds(1)).Status);
        Assert.Equal(AttemptTransitionStatus.IdentityCollision, store.Claim("app", "second", second, token, Guid.NewGuid(), TimeSpan.FromSeconds(1)).Status);
        Assert.Equal(AttemptTransitionStatus.IdentityCollision, store.Claim("app", "second", second, Guid.NewGuid(), attempt, TimeSpan.FromSeconds(1)).Status);
        var same = Guid.NewGuid();
        Assert.Equal(AttemptTransitionStatus.IdentityCollision, store.Claim("app", "second", second, same, same, TimeSpan.FromSeconds(1)).Status);
        Assert.Equal(TargetedAttemptState.Eligible, store.GetOwnership("app", "second", second)!.State);
        store.AdvanceClock(Start.AddSeconds(1));
        Assert.Equal(AttemptTransitionStatus.Applied, store.RecoverExpiredAsFailed("app", "submission", first, attempt, token, Start.AddSeconds(1)).Status);
        Assert.Equal(AttemptTransitionStatus.IdentityCollision, store.Claim("app", "second", second, attempt, Guid.NewGuid(), TimeSpan.FromSeconds(1)).Status);
        var secondAttempt = Guid.NewGuid();
        var secondToken = Guid.NewGuid();
        Assert.Equal(AttemptTransitionStatus.Applied, store.Claim("app", "second", second, secondAttempt, secondToken, TimeSpan.FromSeconds(1)).Status);
        AssertRunning(store.GetOwnership("app", "second", second), second, secondAttempt, secondToken, Start.AddSeconds(2));
    }

    [Fact]
    public void InvocationAttemptAndTokenIdentitiesCannotCollideAcrossApplicationsInEitherOrder()
    {
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        store.InitializeClock(Start);
        var first = Accept(store);
        var otherRequest = new TargetedRequest("other-app", "other", TargetedOperation.Dispatch, "target", [1], [2], [3]);
        var other = Guid.NewGuid();
        Assert.Equal(TargetedAcceptKind.Created, store.Accept(otherRequest, other, "implementation-v1").Kind);
        Assert.Equal(AttemptTransitionStatus.IdentityCollision,
            store.Claim("other-app", "other", other, first, Guid.NewGuid(), TimeSpan.FromSeconds(1)).Status);
        Assert.Equal(AttemptTransitionStatus.IdentityCollision,
            store.Claim("other-app", "other", other, Guid.NewGuid(), first, TimeSpan.FromSeconds(1)).Status);
        Assert.Equal(TargetedAttemptState.Eligible, store.GetOwnership("other-app", "other", other)!.State);

        var attempt = Guid.NewGuid();
        var token = Guid.NewGuid();
        Assert.Equal(AttemptTransitionStatus.Applied, store.Claim("app", "submission", first, attempt, token, TimeSpan.FromSeconds(1)).Status);
        var candidate = new TargetedRequest("third-app", "new", TargetedOperation.Dispatch, "target", [1], [2], [3]);
        Assert.Equal(TargetedAcceptKind.InvocationIdCollision, store.Accept(candidate, attempt, "implementation-v1").Kind);
        Assert.Equal(TargetedAcceptKind.InvocationIdCollision, store.Accept(candidate, token, "implementation-v1").Kind);
        Assert.Null(store.Lookup("third-app", "new"));
        store.AdvanceClock(Start.AddSeconds(1));
        Assert.Equal(AttemptTransitionStatus.Applied, store.RecoverExpiredAsFailed("app", "submission", first, attempt, token, Start.AddSeconds(1)).Status);
        Assert.Equal(TargetedAcceptKind.InvocationIdCollision, store.Accept(candidate, attempt, "implementation-v1").Kind);
        Assert.Equal(TargetedAcceptKind.InvocationIdCollision, store.Accept(candidate, token, "implementation-v1").Kind);
        Assert.Null(store.Lookup("third-app", "new"));
        Assert.Equal(AttemptTransitionStatus.IdentityCollision,
            store.Claim("other-app", "other", other, attempt, Guid.NewGuid(), TimeSpan.FromSeconds(1)).Status);
        Assert.Equal(AttemptTransitionStatus.IdentityCollision,
            store.Claim("other-app", "other", other, Guid.NewGuid(), token, TimeSpan.FromSeconds(1)).Status);
        Assert.Equal(TargetedAttemptState.Eligible, store.GetOwnership("other-app", "other", other)!.State);

        // Publication identity is a separate domain, even for an existing token.
        var publication = new PublicationRequest("fourth-app", "publication", [1], [2], []);
        Assert.Equal(PublicationAcceptKind.Created, store.AcceptPublication(publication, token, []).Kind);
        Assert.Equal(token, store.LookupPublication("fourth-app", "publication")!.PublicationId);
    }

    [Fact]
    public async Task ConcurrentClaimsAndRecoveriesEachHaveExactlyOneWinner()
    {
        const int count = 12;
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        store.InitializeClock(Start);
        var id = Accept(store);
        using var claimBarrier = new Barrier(count);
        var claims = Enumerable.Range(0, count).Select(_ => Task.Factory.StartNew(() =>
        {
            if (!claimBarrier.SignalAndWait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Claim barrier timed out.");
            return store.Claim("app", "submission", id, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromSeconds(1));
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
        var results = await Task.WhenAll(claims).WaitAsync(TimeSpan.FromSeconds(30));
        var winner = Assert.Single(results, item => item.Status == AttemptTransitionStatus.Applied).Ownership!;
        Assert.Equal(count - 1, results.Count(item => item.Status == AttemptTransitionStatus.NotEligible));
        store.AdvanceClock(winner.LeaseUntil!.Value);
        using var recoveryBarrier = new Barrier(count);
        var recoveries = Enumerable.Range(0, count).Select(_ => Task.Factory.StartNew(() =>
        {
            if (!recoveryBarrier.SignalAndWait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Recovery barrier timed out.");
            return store.RecoverExpiredAsFailed("app", "submission", id, winner.AttemptId!.Value, winner.Token!.Value, winner.LeaseUntil.Value);
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
        var completed = await Task.WhenAll(recoveries).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Single(completed, item => item.Status == AttemptTransitionStatus.Applied);
        Assert.Equal(TargetedAttemptState.FailedLeaseExpired, store.GetOwnership("app", "submission", id)!.State);
    }

    [Fact]
    public async Task RenewalExpiryAndRecoveryRaceToOneCoherentState()
    {
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        store.InitializeClock(Start);
        var id = Accept(store);
        var attempt = Guid.NewGuid();
        var token = Guid.NewGuid();
        var deadline = Start.AddSeconds(10);
        Assert.Equal(AttemptTransitionStatus.Applied, store.Claim("app", "submission", id, attempt, token, TimeSpan.FromSeconds(10)).Status);
        store.AdvanceClock(deadline.AddTicks(-1));
        using var barrier = new Barrier(3);
        void Signal()
        {
            if (!barrier.SignalAndWait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Ownership race barrier timed out.");
        }
        var advance = Task.Factory.StartNew(() => { Signal(); store.AdvanceClock(deadline); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var renew = Task.Factory.StartNew(() => { Signal(); return store.Renew("app", "submission", id, attempt, token, TimeSpan.FromSeconds(20)); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var recover = Task.Factory.StartNew(() => { Signal(); return store.RecoverExpiredAsFailed("app", "submission", id, attempt, token, deadline); }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        await Task.WhenAll(advance, renew, recover).WaitAsync(TimeSpan.FromSeconds(30));
        var renewed = await renew;
        var recovered = await recover;
        var current = store.GetOwnership("app", "submission", id)!;
        if (renewed.Status == AttemptTransitionStatus.Applied)
        {
            Assert.NotEqual(AttemptTransitionStatus.Applied, recovered.Status);
            Assert.Equal(TargetedAttemptState.Running, current.State);
            Assert.True(current.LeaseUntil > deadline);
        }
        else if (recovered.Status == AttemptTransitionStatus.Applied)
        {
            Assert.Equal(TargetedAttemptState.FailedLeaseExpired, current.State);
            Assert.Equal(deadline, current.LeaseUntil);
        }
        else
        {
            Assert.Equal(AttemptTransitionStatus.Expired, renewed.Status);
            Assert.Equal(AttemptTransitionStatus.NotExpired, recovered.Status);
            Assert.Equal(TargetedAttemptState.Running, current.State);
            Assert.Equal(deadline, current.LeaseUntil);
        }
        Assert.Equal(attempt, current.AttemptId);
        Assert.Equal(token, current.Token);
        Assert.Equal(id, current.Invocation.InvocationId);
    }
}
