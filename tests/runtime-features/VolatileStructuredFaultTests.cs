using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class VolatileStructuredFaultTests
{
    private const string App = "structured-fault-app";
    private const string Key = "invocation";
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private sealed class Fixture
    {
        public VolatileTargetedStore Concrete { get; } = new(TimeSpan.FromMinutes(1));
        public IRuntimeStore Store => Concrete;
        public Guid Invocation { get; } = Guid.NewGuid();
        public Guid Attempt { get; } = Guid.NewGuid();
        public Guid Token { get; } = Guid.NewGuid();
        public TargetedRequest Request { get; } = new(App, Key, TargetedOperation.Dispatch, "actor", [1, 2], [3], []);

        public Fixture()
        {
            Concrete.InitializeClock(Start);
            Assert.Equal(TargetedAcceptKind.Created, Store.AcceptUnvalidated(Request, Invocation, "pinned").Kind);
            Assert.Equal(AttemptTransitionStatus.Applied,
                Store.Claim(App, Key, Invocation, Attempt, Token, TimeSpan.FromSeconds(10)).Status);
        }

        public TargetedOwnership Snapshot() => Assert.IsType<TargetedOwnership>(Store.GetOwnership(App, Key, Invocation));
        public AttemptTransitionResult Fail(ReportedTerminalFault fault, Guid? attempt = null, Guid? token = null) =>
            Store.FailAfterFault(App, Key, Invocation, attempt ?? Attempt, token ?? Token, fault);
    }

    private static NormalizedFaultObservation Observation(string code, byte[]? data = null, byte[]? metadata = null) =>
        new(code, data ?? [1, 2], metadata ?? [3, 4]);

    [Fact]
    public void ReportsValidateInputsAndPreserveExplicitOriginWithoutInference()
    {
        var f = new Fixture();
        Assert.True((f.Store.Capabilities.Guarantees & RuntimeStoreGuarantees.StructuredFaultReports) != 0);
        f.Store.ValidateRequirements(RuntimeStoreGuarantees.StructuredFaultReports);
        Assert.ThrowsAny<ArgumentException>(() => new NormalizedFaultObservation(" ", [1], [2]));
        Assert.Throws<ArgumentNullException>(() => new NormalizedFaultObservation("kind", null!, [2]));
        Assert.Throws<ArgumentNullException>(() => new NormalizedFaultObservation("kind", [1], null!));
        var emptyOpaque = new NormalizedFaultObservation("kind", [], []);
        Assert.Empty(emptyOpaque.Diagnostic);
        Assert.Empty(emptyOpaque.Metadata);
        Assert.Throws<ArgumentNullException>(() => new NormalizedFaultReport(Observation("p"), null!));
        Assert.ThrowsAny<ArgumentException>(() => new NormalizedFaultReport(null, []));
        Assert.ThrowsAny<ArgumentException>(() => new NormalizedFaultReport(null, [null!]));
        Assert.ThrowsAny<ArgumentException>(() => new NormalizedFaultReport(Observation("p"), [], Guid.Empty));
        var origin = Guid.NewGuid();
        var report = new NormalizedFaultReport(Observation("p"), [], origin);
        var fault = new TargetedFault([5], [6], report);
        Assert.Equal(origin, new ReportedTerminalFault(fault).OriginInvocationId);
        Assert.Equal(origin, new ReportedTerminalFault(fault, origin).OriginInvocationId);
        Assert.ThrowsAny<ArgumentException>(() => new ReportedTerminalFault(fault, Guid.NewGuid()));
        var unknownOrigin = new TargetedFault([5], [6], new NormalizedFaultReport(Observation("unknown"), []));
        Assert.Equal(origin, new ReportedTerminalFault(unknownOrigin, origin).OriginInvocationId);
        Assert.Null(new ReportedTerminalFault(new TargetedFault([1], [2])).Fault.Report);
        Assert.Null(new ReportedTerminalFault(new TargetedFault([1], [2])).OriginInvocationId);
    }

    [Fact]
    public void TerminalAndCleanupOnlyReportsRetainCopiedStructuredFacts()
    {
        var f = new Fixture(); var origin = Guid.NewGuid();
        byte[] primaryBytes = [10, 11], primaryMetadata = [12, 13], secondaryBytes = [20, 21], secondaryMetadata = [22];
        var primary = Observation("handler", primaryBytes, primaryMetadata);
        var cleanup = new List<NormalizedFaultObservation>
        {
            Observation("cleanup-a", secondaryBytes, secondaryMetadata),
            Observation("cleanup-b", [30], [31])
        };
        var report = new NormalizedFaultReport(primary, cleanup, origin);
        var fault = new TargetedFault([40], [41], report);
        primaryBytes[0] = 90; primaryMetadata[0] = 90; secondaryBytes[0] = 90; secondaryMetadata[0] = 90;
        cleanup.Clear();
        var terminal = f.Fail(new ReportedTerminalFault(fault));
        Assert.Equal(AttemptTransitionStatus.Applied, terminal.Status);
        Assert.Equal(TargetedAttemptState.FailedReportedFault, terminal.Ownership!.State);
        Assert.Equal(origin, terminal.Ownership.TerminalFault!.OriginInvocationId);
        var retained = Assert.IsType<NormalizedFaultReport>(terminal.Ownership.TerminalFault.Fault.Report);
        Assert.Equal("handler", retained.Primary!.Classification);
        Assert.Equal(new byte[] { 10, 11 }, retained.Primary.Diagnostic);
        Assert.Equal(new byte[] { 12, 13 }, retained.Primary.Metadata);
        Assert.Equal(new[] { "cleanup-a", "cleanup-b" }, retained.Cleanup.Select(item => item.Classification));
        Assert.Equal(new byte[] { 20, 21 }, retained.Cleanup[0].Diagnostic);
        Assert.Equal(new byte[] { 22 }, retained.Cleanup[0].Metadata);
        retained.Primary.Diagnostic[0] = 80; retained.Primary.Metadata[0] = 80;
        retained.Cleanup[0].Diagnostic[0] = 80; retained.Cleanup[0].Metadata[0] = 80;
        var reread = Assert.IsType<NormalizedFaultReport>(f.Snapshot().TerminalFault!.Fault.Report);
        Assert.Equal(new byte[] { 10, 11 }, reread.Primary!.Diagnostic);
        Assert.Equal(new byte[] { 12, 13 }, reread.Primary.Metadata);
        Assert.Equal(new byte[] { 20, 21 }, reread.Cleanup[0].Diagnostic);
        Assert.Equal(new byte[] { 22 }, reread.Cleanup[0].Metadata);
        Assert.Equal(AttemptTransitionStatus.NotEligible, f.Fail(new ReportedTerminalFault(new TargetedFault([9], [1]))).Status);
        Assert.Equal(origin, f.Snapshot().TerminalFault!.OriginInvocationId);
        Assert.Equal("pinned", f.Snapshot().Invocation.SelectedImplementation);

        var cleanupOnly = new Fixture();
        var only = new NormalizedFaultReport(null, [Observation("cleanup-only")]);
        var failed = cleanupOnly.Fail(new ReportedTerminalFault(new TargetedFault([], [1], only)));
        Assert.Equal(AttemptTransitionStatus.Applied, failed.Status);
        Assert.Equal(TargetedAttemptState.FailedReportedFault, failed.Ownership!.State);
        Assert.Null(failed.Ownership.TerminalFault!.Fault.Report!.Primary);
        Assert.Equal("cleanup-only", Assert.Single(failed.Ownership.TerminalFault.Fault.Report.Cleanup).Classification);
        Assert.Null(failed.Ownership.TerminalFault.OriginInvocationId);
    }

    [Fact]
    public void RetryAndCancellationRetainReportAcrossFencedAttempts()
    {
        var f = new Fixture(); var origin = Guid.NewGuid();
        var report = new NormalizedFaultReport(Observation("primary", [7], [8]), [Observation("cleanup", [9], [10])], origin);
        var fault = new TargetedFault([11], [12], report);
        var rescheduled = f.Store.RescheduleAfterFault(App, Key, f.Invocation, f.Attempt, f.Token, TimeSpan.FromSeconds(2), fault);
        Assert.Equal(AttemptTransitionStatus.Applied, rescheduled.Status);
        Assert.Equal(TargetedAttemptState.Eligible, rescheduled.Ownership!.State);
        var saved = Assert.Single(rescheduled.Ownership.RecoveryHistory).Fault!.Report!;
        Assert.Equal(origin, saved.OriginInvocationId);
        Assert.Equal("primary", saved.Primary!.Classification);
        Assert.Equal("cleanup", Assert.Single(saved.Cleanup).Classification);
        f.Concrete.AdvanceClock(Start.AddSeconds(2));
        var nextAttempt = Guid.NewGuid(); var nextToken = Guid.NewGuid();
        var next = f.Store.Claim(App, Key, f.Invocation, nextAttempt, nextToken, TimeSpan.FromSeconds(10));
        Assert.Equal(AttemptTransitionStatus.Applied, next.Status);
        Assert.Equal(f.Invocation, next.Ownership!.Invocation.InvocationId);
        Assert.Equal("pinned", next.Ownership.Invocation.SelectedImplementation);
        Assert.Equal(origin, Assert.Single(next.Ownership.RecoveryHistory).Fault!.Report!.OriginInvocationId);
        Assert.Equal(AttemptTransitionStatus.InvalidOwner, f.Fail(new ReportedTerminalFault(fault)).Status);
        Assert.Equal(CancellationRequestStatus.Applied, f.Store.RequestCancellation(App, Key, f.Invocation).Status);
        var exit = f.Store.FailAfterFault(App, Key, f.Invocation, nextAttempt, nextToken, new ReportedTerminalFault(fault));
        Assert.Equal(AttemptTransitionStatus.CancellationFinalized, exit.Status);
        Assert.Equal(TargetedAttemptState.Cancelled, exit.Ownership!.State);
        Assert.Null(exit.Ownership.TerminalFault);
        Assert.Null(exit.Ownership.Completion);
        Assert.Equal(CancellationExitKind.ReportedFault, exit.Ownership.ObservedCancellationExit!.Kind);
        var observed = exit.Ownership.ObservedCancellationExit.Fault!;
        Assert.Equal(origin, observed.OriginInvocationId);
        Assert.Equal("primary", observed.Fault.Report!.Primary!.Classification);
        Assert.Equal("cleanup", Assert.Single(observed.Fault.Report.Cleanup).Classification);
        observed.Fault.Report.Primary.Diagnostic[0] = 99;
        Assert.Equal(new byte[] { 7 }, f.Snapshot().ObservedCancellationExit!.Fault!.Fault.Report!.Primary!.Diagnostic);
        Assert.Equal(origin, Assert.Single(f.Snapshot().RecoveryHistory).Fault!.Report!.OriginInvocationId);

        var duringCancellation = new Fixture();
        Assert.Equal(CancellationRequestStatus.Applied,
            duringCancellation.Store.RequestCancellation(App, Key, duringCancellation.Invocation).Status);
        var rescheduleExit = duringCancellation.Store.RescheduleAfterFault(App, Key, duringCancellation.Invocation,
            duringCancellation.Attempt, duringCancellation.Token, TimeSpan.Zero, fault);
        Assert.Equal(AttemptTransitionStatus.CancellationFinalized, rescheduleExit.Status);
        Assert.Equal(origin, rescheduleExit.Ownership!.ObservedCancellationExit!.Fault!.OriginInvocationId);
        Assert.Empty(rescheduleExit.Ownership.RecoveryHistory);
    }

    [Fact]
    public async Task FencedOwnersAndCompetingNormalOutcomeCannotOverwriteEachOther()
    {
        var expired = new Fixture();
        var report = new NormalizedFaultReport(Observation("runtime"), []);
        var fault = new ReportedTerminalFault(new TargetedFault([1], [2], report));
        Assert.Equal(AttemptTransitionStatus.InvalidOwner, expired.Fail(fault, token: Guid.NewGuid()).Status);
        Assert.Equal(AttemptTransitionStatus.InvalidOwner, expired.Fail(fault, attempt: Guid.NewGuid()).Status);
        expired.Concrete.AdvanceClock(Start.AddSeconds(10));
        Assert.Equal(AttemptTransitionStatus.Expired, expired.Fail(fault).Status);
        Assert.Null(expired.Snapshot().TerminalFault);

        var normal = new Fixture();
        Assert.Equal(AttemptTransitionStatus.Applied, normal.Store.Complete(App, Key, normal.Invocation, normal.Attempt, normal.Token,
            new TargetedCompletion([0, 69, 82, 82], [1])).Status);
        Assert.Equal(TargetedAttemptState.Completed, normal.Snapshot().State);
        Assert.Null(normal.Snapshot().TerminalFault);

        var race = new Fixture();
        using var barrier = new Barrier(2);
        var failed = Task.Factory.StartNew(() =>
        {
            if (!barrier.SignalAndWait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Fault race barrier timed out.");
            return race.Fail(fault);
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var completed = Task.Factory.StartNew(() =>
        {
            if (!barrier.SignalAndWait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Fault race barrier timed out.");
            return race.Store.Complete(App, Key, race.Invocation, race.Attempt, race.Token,
                new TargetedCompletion([0, 69, 82, 82], [1]));
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var results = await Task.WhenAll(failed, completed).WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(1, results.Count(item => item.Status == AttemptTransitionStatus.Applied));
        Assert.Equal(1, results.Count(item => item.Status == AttemptTransitionStatus.NotEligible));
        var final = race.Snapshot();
        if (results[0].Status == AttemptTransitionStatus.Applied)
        {
            Assert.Equal(TargetedAttemptState.FailedReportedFault, final.State);
            Assert.Equal("runtime", final.TerminalFault!.Fault.Report!.Primary!.Classification);
            Assert.Null(final.Completion);
        }
        else
        {
            Assert.Equal(TargetedAttemptState.Completed, final.State);
            Assert.Equal(new byte[] { 0, 69, 82, 82 }, final.Completion!.Payload);
            Assert.Null(final.TerminalFault);
        }
    }
}
