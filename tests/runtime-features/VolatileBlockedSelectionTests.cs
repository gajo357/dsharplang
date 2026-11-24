using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class VolatileBlockedSelectionTests
{
    private const string App = "blocked-app";
    private const string Key = "submission";
    private const string Selected = "pinned-v1";
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private sealed class Fixture
    {
        public VolatileTargetedStore Concrete { get; } = new(TimeSpan.FromMinutes(1));
        public IRuntimeStore Store => Concrete;
        public Guid Invocation { get; } = Guid.NewGuid();
        public Guid Attempt { get; } = Guid.NewGuid();
        public Guid Token { get; } = Guid.NewGuid();
        public TargetedRequest Request { get; } = new(App, Key, TargetedOperation.Dispatch, "public-target", [1, 2], [3], [4]);

        public Fixture()
        {
            Concrete.InitializeClock(Start);
            Assert.Equal(TargetedAcceptKind.Created, Store.AcceptUnvalidated(Request, Invocation, Selected).Kind);
        }

        public AttemptTransitionResult Block(long version, Guid blockId, string selected = Selected,
            BlockReason reason = BlockReason.ImplementationUnavailable) =>
            Store.BlockEligible(App, Key, Invocation, version, selected, blockId, reason);
        public AttemptTransitionResult Unblock(Guid blockId, string selected = Selected) =>
            Store.Unblock(App, Key, Invocation, blockId, selected);
        public TargetedOwnership Snapshot() => Assert.IsType<TargetedOwnership>(Store.GetOwnership(App, Key, Invocation));
        public AttemptTransitionResult Claim(Guid? attempt = null, Guid? token = null) =>
            Store.Claim(App, Key, Invocation, attempt ?? Attempt, token ?? Token, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void PinnedDelayedRetryAndHistorySurviveBlockingAndUnblocking()
    {
        var f = new Fixture();
        Assert.True((f.Store.Capabilities.Guarantees & RuntimeStoreGuarantees.BlockedPinnedImplementation) != 0);
        f.Store.ValidateRequirements(RuntimeStoreGuarantees.BlockedPinnedImplementation);
        Assert.Equal(0, f.Snapshot().EligibilityVersion);
        Assert.Equal(AttemptTransitionStatus.Applied, f.Claim().Status);
        var retry = f.Store.RescheduleAfterFault(App, Key, f.Invocation, f.Attempt, f.Token,
            TimeSpan.FromSeconds(10), new TargetedFault([5, 6], [7]));
        Assert.Equal(AttemptTransitionStatus.Applied, retry.Status);
        var eligible = Assert.IsType<TargetedOwnership>(retry.Ownership);
        Assert.Equal(1, eligible.EligibilityVersion);
        Assert.Equal(Start.AddSeconds(10), eligible.EligibleAt);
        Assert.Single(eligible.RecoveryHistory);
        Assert.Equal(AttemptTransitionStatus.ObservationMismatch, f.Block(0, Guid.NewGuid()).Status);
        Assert.Equal(AttemptTransitionStatus.IdentityCollision, f.Block(1, f.Attempt).Status);
        Assert.Equal(AttemptTransitionStatus.IdentityCollision, f.Block(1, f.Token).Status);
        var blockA = Guid.NewGuid();
        var blocked = f.Block(eligible.EligibilityVersion, blockA, reason: BlockReason.Incompatible);
        Assert.Equal(AttemptTransitionStatus.Applied, blocked.Status);
        Assert.Equal(TargetedAttemptState.Blocked, blocked.Ownership!.State);
        Assert.Equal(blockA, blocked.Ownership.Block!.BlockId);
        Assert.Equal(Selected, blocked.Ownership.Block.SelectedImplementation);
        Assert.Equal(BlockReason.Incompatible, blocked.Ownership.Block.Reason);
        Assert.Equal(Start.AddSeconds(10), blocked.Ownership.EligibleAt);
        Assert.Equal(new byte[] { 5, 6 }, blocked.Ownership.RecoveryHistory[0].Fault!.Payload);
        blocked.Ownership.RecoveryHistory[0].Fault!.Payload[0] = 99;
        Assert.Equal(AttemptTransitionStatus.NotEligible, f.Claim(Guid.NewGuid(), Guid.NewGuid()).Status);
        var probe = f.Store.ProbeTargeted(f.Request);
        Assert.Equal(SubmissionProbeKind.Existing, probe.Kind);
        Assert.Equal(f.Invocation, probe.Record!.InvocationId);
        Assert.Equal(Selected, probe.Record.SelectedImplementation);
        Assert.Equal(new byte[] { 1, 2 }, probe.Record.Request.Payload);
        var matching = f.Store.AcceptUnvalidated(f.Request, Guid.Empty, null);
        Assert.Equal(TargetedAcceptKind.Existing, matching.Kind);
        Assert.Equal(Selected, matching.Record!.SelectedImplementation);

        var unblocked = f.Unblock(blockA);
        Assert.Equal(AttemptTransitionStatus.Applied, unblocked.Status);
        Assert.Equal(TargetedAttemptState.Eligible, unblocked.Ownership!.State);
        Assert.Equal(2, unblocked.Ownership.EligibilityVersion);
        Assert.Equal(Start.AddSeconds(10), unblocked.Ownership.EligibleAt);
        Assert.Equal(new byte[] { 5, 6 }, unblocked.Ownership.RecoveryHistory[0].Fault!.Payload);
        Assert.Equal(AttemptTransitionStatus.NotYetEligible, f.Claim(Guid.NewGuid(), Guid.NewGuid()).Status);
        var blockB = Guid.NewGuid();
        Assert.Equal(AttemptTransitionStatus.Applied, f.Block(2, blockB).Status);
        f.Concrete.AdvanceClock(Start.AddSeconds(10));
        Assert.Equal(AttemptTransitionStatus.NotEligible, f.Claim(Guid.NewGuid(), Guid.NewGuid()).Status);
        Assert.Equal(AttemptTransitionStatus.Applied, f.Unblock(blockB).Status);
        Assert.Equal(3, f.Snapshot().EligibilityVersion);
        Assert.Equal(AttemptTransitionStatus.IdentityCollision, f.Claim(f.Attempt, Guid.NewGuid()).Status);
        Assert.Equal(AttemptTransitionStatus.IdentityCollision, f.Claim(Guid.NewGuid(), f.Token).Status);
        var fresh = f.Claim(Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(AttemptTransitionStatus.Applied, fresh.Status);
        Assert.Equal(f.Invocation, fresh.Ownership!.Invocation.InvocationId);
        Assert.Equal(Selected, fresh.Ownership.Invocation.SelectedImplementation);
        Assert.Single(fresh.Ownership.RecoveryHistory);
        Assert.Equal(Start.AddSeconds(10), eligible.EligibleAt);
        Assert.Equal(1, eligible.EligibilityVersion);
    }

    [Fact]
    public void ObservationsAndIdentifiersFenceStaleBlockingAndUnblocking()
    {
        var f = new Fixture();
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        Assert.ThrowsAny<ArgumentException>(() => f.Block(0, Guid.Empty));
        Assert.ThrowsAny<ArgumentException>(() => f.Block(0, Guid.NewGuid(), " "));
        Assert.ThrowsAny<ArgumentException>(() => f.Block(0, Guid.NewGuid(), reason: (BlockReason)999));
        Assert.Equal(AttemptTransitionStatus.ObservationMismatch, f.Block(0, Guid.NewGuid(), "other").Status);
        Assert.Equal(AttemptTransitionStatus.ObservationMismatch, f.Block(1, Guid.NewGuid()).Status);
        Assert.Equal(AttemptTransitionStatus.IdentityCollision, f.Block(0, f.Invocation).Status);
        Assert.Equal(TargetedAttemptState.Eligible, f.Snapshot().State);
        Assert.Equal(AttemptTransitionStatus.Applied, f.Block(0, a).Status);
        Assert.Equal(AttemptTransitionStatus.ObservationMismatch, f.Unblock(Guid.NewGuid()).Status);
        Assert.Equal(AttemptTransitionStatus.ObservationMismatch, f.Unblock(a, "other").Status);
        Assert.Equal(AttemptTransitionStatus.Applied, f.Unblock(a).Status);
        Assert.Equal(1, f.Snapshot().EligibilityVersion);
        Assert.Equal(AttemptTransitionStatus.ObservationMismatch, f.Block(0, b).Status);
        Assert.Equal(AttemptTransitionStatus.IdentityCollision, f.Block(1, a).Status);
        Assert.Equal(AttemptTransitionStatus.Applied, f.Block(1, b).Status);
        Assert.Equal(AttemptTransitionStatus.ObservationMismatch, f.Unblock(a).Status);
        Assert.Equal(b, f.Snapshot().Block!.BlockId);
        Assert.Equal(AttemptTransitionStatus.Applied, f.Unblock(b).Status);
        Assert.Equal(2, f.Snapshot().EligibilityVersion);
        Assert.Equal(AttemptTransitionStatus.IdentityCollision, f.Claim(a, Guid.NewGuid()).Status);
        Assert.Equal(AttemptTransitionStatus.IdentityCollision, f.Claim(Guid.NewGuid(), b).Status);
        Assert.Equal(AttemptTransitionStatus.Applied, f.Claim().Status);
        Assert.Equal(AttemptTransitionStatus.NotEligible, f.Block(2, Guid.NewGuid()).Status);
        var fault = new ReportedTerminalFault(new TargetedFault([8], [9]));
        Assert.Equal(AttemptTransitionStatus.Applied, f.Store.FailAfterFault(App, Key, f.Invocation, f.Attempt, f.Token, fault).Status);
        Assert.Equal(AttemptTransitionStatus.NotEligible, f.Block(2, Guid.NewGuid()).Status);
        Assert.Equal(TargetedAttemptState.FailedReportedFault, f.Snapshot().State);
    }

    [Fact]
    public void BlockedCancellationIsTerminalAndPreservesPinnedIdentity()
    {
        var f = new Fixture(); var blockId = Guid.NewGuid();
        Assert.Equal(AttemptTransitionStatus.Applied, f.Block(0, blockId).Status);
        Assert.Equal(CancellationRequestStatus.Applied, f.Store.RequestCancellation(App, Key, f.Invocation).Status);
        Assert.Equal(TargetedAttemptState.Cancelled, f.Snapshot().State);
        Assert.Equal(CancellationRequestStatus.AlreadyRequested, f.Store.RequestCancellation(App, Key, f.Invocation).Status);
        Assert.Equal(AttemptTransitionStatus.NotEligible, f.Unblock(blockId).Status);
        Assert.Equal(AttemptTransitionStatus.NotEligible, f.Claim().Status);
        var record = f.Store.ProbeTargeted(f.Request).Record!;
        Assert.Equal(f.Invocation, record.InvocationId);
        Assert.Equal(Selected, record.SelectedImplementation);
    }

    [Fact]
    public async Task BlockingClaimAndCancellationRacesHaveCoherentWinners()
    {
        var f = new Fixture(); var blockId = Guid.NewGuid();
        using (var barrier = new Barrier(2))
        {
            var block = Task.Factory.StartNew(() =>
            {
                if (!barrier.SignalAndWait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Block race barrier timed out.");
                return f.Block(0, blockId);
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            var claim = Task.Factory.StartNew(() =>
            {
                if (!barrier.SignalAndWait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Block race barrier timed out.");
                return f.Claim();
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            var results = await Task.WhenAll(block, claim).WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal(1, results.Count(item => item.Status == AttemptTransitionStatus.Applied));
            Assert.Equal(1, results.Count(item => item.Status == AttemptTransitionStatus.NotEligible));
            var snapshot = f.Snapshot();
            if (results[0].Status == AttemptTransitionStatus.Applied)
            {
                Assert.Equal(TargetedAttemptState.Blocked, snapshot.State);
                Assert.Equal(blockId, snapshot.Block!.BlockId);
            }
            else
            {
                Assert.Equal(TargetedAttemptState.Running, snapshot.State);
                Assert.Equal(f.Attempt, snapshot.AttemptId);
            }
        }

        var cancelled = new Fixture(); var id = Guid.NewGuid();
        Assert.Equal(AttemptTransitionStatus.Applied, cancelled.Block(0, id).Status);
        using var secondBarrier = new Barrier(2);
        var cancel = Task.Factory.StartNew(() =>
        {
            if (!secondBarrier.SignalAndWait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Cancel race barrier timed out.");
            return cancelled.Store.RequestCancellation(App, Key, cancelled.Invocation);
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var unblock = Task.Factory.StartNew(() =>
        {
            if (!secondBarrier.SignalAndWait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Unblock race barrier timed out.");
            return cancelled.Unblock(id);
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        await Task.WhenAll((Task)cancel, unblock).WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(CancellationRequestStatus.Applied, (await cancel).Status);
        Assert.True((await unblock).Status is AttemptTransitionStatus.Applied or AttemptTransitionStatus.NotEligible);
        Assert.Equal(TargetedAttemptState.Cancelled, cancelled.Snapshot().State);
        Assert.Equal(AttemptTransitionStatus.NotEligible, cancelled.Claim().Status);
        Assert.Equal(AttemptTransitionStatus.NotEligible, cancelled.Unblock(id).Status);
    }
}
