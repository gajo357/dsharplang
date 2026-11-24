using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

// A reusable test profile for the optional, caller-normalized report capability.
// It does not certify normalization, authorization, durability or a public API.
internal static class StructuredFaultContractScenarios
{
    private const string App = "structured-contract";

    private static void Check(bool condition, string invariant, TargetedOwnership? observation = null)
    {
        if (!condition) throw new ContractScenarioViolation(invariant, observation);
    }

    private static void RequireCapability(IRuntimeStore store)
    {
        Check((store.Capabilities.Guarantees & RuntimeStoreGuarantees.StructuredFaultReports) != 0,
            "structured.capability-required");
        store.ValidateRequirements(RuntimeStoreGuarantees.StructuredFaultReports);
    }

    private static (Guid Invocation, Guid Attempt, Guid Token) Start(IRuntimeStore store, string key)
    {
        var id = Guid.NewGuid(); var attempt = Guid.NewGuid(); var token = Guid.NewGuid();
        var request = new TargetedRequest(App, key, TargetedOperation.Dispatch, "actor", [1], [2], []);
        Check(store.AcceptUnvalidated(request, id, "pinned").Kind == TargetedAcceptKind.Created, "structured.accept");
        Check(store.Claim(App, key, id, attempt, token, TimeSpan.FromSeconds(10)).Status == AttemptTransitionStatus.Applied,
            "structured.claim");
        return (id, attempt, token);
    }

    private static NormalizedFaultReport Report(Guid? origin) =>
        new(new NormalizedFaultObservation("primary", [1, 2], [3]),
            [new NormalizedFaultObservation("cleanup-a", [4, 5], [6]),
             new NormalizedFaultObservation("cleanup-b", [7], [8])], origin);

    private static void CheckReport(NormalizedFaultReport? report, Guid? origin, string invariant, TargetedOwnership? state)
    {
        Check(report is not null && report.OriginInvocationId == origin && report.Primary?.Classification == "primary" &&
              report.Primary.Diagnostic.SequenceEqual(new byte[] { 1, 2 }) &&
              report.Primary.Metadata.SequenceEqual(new byte[] { 3 }) && report.Cleanup.Count == 2 &&
              report.Cleanup[0].Classification == "cleanup-a" &&
              report.Cleanup[0].Diagnostic.SequenceEqual(new byte[] { 4, 5 }) &&
              report.Cleanup[0].Metadata.SequenceEqual(new byte[] { 6 }) &&
              report.Cleanup[1].Classification == "cleanup-b" &&
              report.Cleanup[1].Diagnostic.SequenceEqual(new byte[] { 7 }) &&
              report.Cleanup[1].Metadata.SequenceEqual(new byte[] { 8 }), invariant, state);
    }

    public static void Terminal(Func<ContractFixture> factory, Guid? origin)
    {
        var store = factory().Store;
        RequireCapability(store);
        var ids = Start(store, "terminal");
        var report = Report(origin);
        var fault = new ReportedTerminalFault(new TargetedFault([9], [10], report));
        Check(store.FailAfterFault(App, "terminal", ids.Invocation, ids.Attempt, ids.Token, fault).Status == AttemptTransitionStatus.Applied,
            "structured.terminal.transition");
        // Inspect persisted state rather than trusting the transition's return.
        var actual = store.GetOwnership(App, "terminal", ids.Invocation);
        Check(actual?.State == TargetedAttemptState.FailedReportedFault && actual.TerminalFault is not null &&
              actual.TerminalFault.OriginInvocationId == origin &&
              actual.Invocation.InvocationId == ids.Invocation && actual.Invocation.SelectedImplementation == "pinned",
            "structured.terminal.identity", actual);
        CheckReport(actual!.TerminalFault!.Fault.Report, origin, "structured.terminal.report", actual);
        actual.TerminalFault.Fault.Report!.Primary!.Diagnostic[0] = 99;
        var reread = store.GetOwnership(App, "terminal", ids.Invocation);
        CheckReport(reread!.TerminalFault!.Fault.Report, origin, "structured.terminal.copied", reread);
    }

    public static void Retry(Func<ContractFixture> factory)
    {
        var fixture = factory(); var store = fixture.Store;
        RequireCapability(store);
        var ids = Start(store, "retry"); var origin = Guid.NewGuid();
        var report = Report(origin);
        Check(store.RescheduleAfterFault(App, "retry", ids.Invocation, ids.Attempt, ids.Token, TimeSpan.FromSeconds(2),
            new TargetedFault([9], [10], report)).Status == AttemptTransitionStatus.Applied, "structured.retry.transition");
        var eligible = store.GetOwnership(App, "retry", ids.Invocation);
        Check(eligible?.State == TargetedAttemptState.Eligible && eligible.RecoveryHistory.Count == 1,
            "structured.retry.history", eligible);
        var prior = eligible!.RecoveryHistory[0];
        Check(prior.Kind == AttemptRecoveryKind.ReportedFault && prior.AttemptId == ids.Attempt && prior.Token == ids.Token,
            "structured.retry.identity", eligible);
        CheckReport(prior.Fault?.Report, origin, "structured.retry.report", eligible);
        fixture.Advance(TimeSpan.FromSeconds(2));
        Check(store.Claim(App, "retry", ids.Invocation, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromSeconds(10)).Status == AttemptTransitionStatus.Applied,
            "structured.retry.reclaim");
        var current = store.GetOwnership(App, "retry", ids.Invocation);
        Check(current?.Invocation.InvocationId == ids.Invocation && current.Invocation.SelectedImplementation == "pinned" &&
              current.RecoveryHistory.Count == 1, "structured.retry.retained", current);
        CheckReport(current!.RecoveryHistory[0].Fault?.Report, origin, "structured.retry.retained-report", current);
    }

    public static void Cancellation(Func<ContractFixture> factory)
    {
        var store = factory().Store;
        RequireCapability(store);
        var ids = Start(store, "cancel"); var origin = Guid.NewGuid();
        Check(store.RequestCancellation(App, "cancel", ids.Invocation).Status == CancellationRequestStatus.Applied,
            "structured.cancel.request");
        var fault = new ReportedTerminalFault(new TargetedFault([9], [10], Report(origin)));
        Check(store.FailAfterFault(App, "cancel", ids.Invocation, ids.Attempt, ids.Token, fault).Status == AttemptTransitionStatus.CancellationFinalized,
            "structured.cancel.transition");
        var actual = store.GetOwnership(App, "cancel", ids.Invocation);
        Check(actual?.State == TargetedAttemptState.Cancelled && actual.TerminalFault is null && actual.Completion is null &&
              actual.ObservedCancellationExit?.Kind == CancellationExitKind.ReportedFault &&
              actual.ObservedCancellationExit.Fault?.OriginInvocationId == origin, "structured.cancel.outcome", actual);
        CheckReport(actual!.ObservedCancellationExit!.Fault!.Fault.Report, origin, "structured.cancel.report", actual);
    }

    public static void RunAll(Func<ContractFixture> factory)
    {
        Terminal(factory, Guid.NewGuid());
        Terminal(factory, null);
        Retry(factory);
        Cancellation(factory);
    }
}

// Test-only negative control: lie about retaining structured reports, but make
// a real terminal commit to the backing store with the report removed.
internal sealed class StripStructuredFaultDecorator(IRuntimeStore inner, bool advertiseCapability = true) : IRuntimeStore
{
    public string? MutatedApplication { get; private set; }
    public string? MutatedSubmission { get; private set; }
    public Guid MutatedInvocation { get; private set; }

    public RuntimeStoreCapabilities Capabilities => advertiseCapability ? inner.Capabilities :
        new(inner.Capabilities.Guarantees & ~RuntimeStoreGuarantees.StructuredFaultReports);
    public TimeSpan RetentionHorizon => inner.RetentionHorizon;
    public void ValidateRequirements(RuntimeStoreGuarantees required)
    {
        if (!advertiseCapability && (required & RuntimeStoreGuarantees.StructuredFaultReports) != 0)
            throw new NotSupportedException("Structured reports are not supported by this test provider.");
        inner.ValidateRequirements(required);
    }
    public TargetedProbeResult ProbeTargeted(TargetedRequest request) => inner.ProbeTargeted(request);
    public PublicationProbeResult ProbePublication(PublicationRequest request) => inner.ProbePublication(request);
    public TargetedRecord? Lookup(string applicationId, string submissionId) => inner.Lookup(applicationId, submissionId);
    public PublicationRecord? LookupPublication(string applicationId, string submissionId) => inner.LookupPublication(applicationId, submissionId);
    public TargetedAcceptResult AcceptUnvalidated(TargetedRequest request, Guid invocationId, string? selectedImplementation) =>
        inner.AcceptUnvalidated(request, invocationId, selectedImplementation);
    public PublicationAcceptResult AcceptPublication(PublicationRequest request, Guid publicationId, IEnumerable<PublicationIntent>? intents) =>
        inner.AcceptPublication(request, publicationId, intents);
    public OwnedInvokeResult AcceptOwnedInvoke(TargetedRequest request, Guid childInvocationId, string? selectedImplementation,
        string parentSubmissionId, Guid parentInvocationId, Guid parentAttemptId, Guid parentToken) =>
        inner.AcceptOwnedInvoke(request, childInvocationId, selectedImplementation, parentSubmissionId, parentInvocationId, parentAttemptId, parentToken);
    public TargetedOwnership? GetOwnership(string applicationId, string submissionId, Guid invocationId) =>
        inner.GetOwnership(applicationId, submissionId, invocationId);
    public AttemptTransitionResult BlockEligible(string applicationId, string submissionId, Guid invocationId,
        long expectedEligibilityVersion, string expectedSelectedImplementation, Guid blockId, BlockReason reason) =>
        inner.BlockEligible(applicationId, submissionId, invocationId, expectedEligibilityVersion, expectedSelectedImplementation, blockId, reason);
    public AttemptTransitionResult Unblock(string applicationId, string submissionId, Guid invocationId, Guid expectedBlockId,
        string expectedSelectedImplementation) => inner.Unblock(applicationId, submissionId, invocationId, expectedBlockId, expectedSelectedImplementation);
    public AttemptTransitionResult Claim(string applicationId, string submissionId, Guid invocationId, Guid attemptId, Guid token, TimeSpan duration) =>
        inner.Claim(applicationId, submissionId, invocationId, attemptId, token, duration);
    public AttemptTransitionResult Renew(string applicationId, string submissionId, Guid invocationId, Guid attemptId, Guid token, TimeSpan duration) =>
        inner.Renew(applicationId, submissionId, invocationId, attemptId, token, duration);
    public AttemptTransitionResult Complete(string applicationId, string submissionId, Guid invocationId, Guid attemptId, Guid token, TargetedCompletion completion) =>
        inner.Complete(applicationId, submissionId, invocationId, attemptId, token, completion);
    public AttemptTransitionResult FailAfterFault(string applicationId, string submissionId, Guid invocationId, Guid attemptId, Guid token, ReportedTerminalFault fault)
    {
        var stripped = new ReportedTerminalFault(new TargetedFault(fault.Fault.Payload, fault.Fault.ContractMetadata), fault.OriginInvocationId);
        var result = inner.FailAfterFault(applicationId, submissionId, invocationId, attemptId, token, stripped);
        if (result.Status == AttemptTransitionStatus.Applied)
        {
            MutatedApplication = applicationId;
            MutatedSubmission = submissionId;
            MutatedInvocation = invocationId;
        }
        return result;
    }
    public AttemptTransitionResult RescheduleAfterFault(string applicationId, string submissionId, Guid invocationId, Guid attemptId, Guid token, TimeSpan delay, TargetedFault fault) =>
        inner.RescheduleAfterFault(applicationId, submissionId, invocationId, attemptId, token, delay, fault);
    public AttemptTransitionResult RecoverExpiredAsRetry(string applicationId, string submissionId, Guid invocationId,
        Guid observedAttemptId, Guid observedToken, DateTimeOffset observedLeaseUntil, TimeSpan delay) =>
        inner.RecoverExpiredAsRetry(applicationId, submissionId, invocationId, observedAttemptId, observedToken, observedLeaseUntil, delay);
    public AttemptTransitionResult RecoverExpiredAsFailed(string applicationId, string submissionId, Guid invocationId,
        Guid observedAttemptId, Guid observedToken, DateTimeOffset observedLeaseUntil) =>
        inner.RecoverExpiredAsFailed(applicationId, submissionId, invocationId, observedAttemptId, observedToken, observedLeaseUntil);
    public CancellationRequestResult RequestCancellation(string applicationId, string submissionId, Guid invocationId) =>
        inner.RequestCancellation(applicationId, submissionId, invocationId);
}

public sealed class StructuredFaultContractTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static VolatileTargetedStore NewStore()
    {
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        store.InitializeClock(Start);
        return store;
    }

    private static ContractFixture Fixture(VolatileTargetedStore backing, IRuntimeStore provider) =>
        new(provider, delta => backing.AdvanceClock(backing.CurrentTime.Add(delta)));

    [Fact]
    public void ReferencePassesTheStructuredFaultProfile()
    {
        StructuredFaultContractScenarios.RunAll(() =>
        {
            var store = NewStore();
            return Fixture(store, store);
        });
    }

    [Fact]
    public void ProfileRequiresTheAdvertisedCapability()
    {
        var store = NewStore();
        var unsupported = new StripStructuredFaultDecorator(store, advertiseCapability: false);
        Assert.Throws<NotSupportedException>(() => unsupported.ValidateRequirements(RuntimeStoreGuarantees.StructuredFaultReports));
        var error = Assert.Throws<ContractScenarioViolation>(() =>
            StructuredFaultContractScenarios.Terminal(() => Fixture(store, unsupported), Guid.NewGuid()));
        Assert.Equal("structured.capability-required", error.Invariant);
        Assert.Null(store.Lookup("structured-contract", "terminal"));
    }

    [Fact]
    public void SharedTerminalScenarioDetectsARealDroppedReport()
    {
        var backing = NewStore();
        var broken = new StripStructuredFaultDecorator(backing);
        var origin = Guid.NewGuid();
        var error = Assert.Throws<ContractScenarioViolation>(() =>
            StructuredFaultContractScenarios.Terminal(() => Fixture(backing, broken), origin));
        Assert.Equal("structured.terminal.report", error.Invariant);
        Assert.Equal(TargetedAttemptState.FailedReportedFault, error.Observation!.State);
        Assert.NotNull(broken.MutatedApplication);
        Assert.NotNull(broken.MutatedSubmission);
        var actual = backing.GetOwnership(broken.MutatedApplication, broken.MutatedSubmission, broken.MutatedInvocation);
        Assert.NotNull(actual);
        Assert.Equal(TargetedAttemptState.FailedReportedFault, actual.State);
        Assert.NotNull(actual.TerminalFault);
        Assert.Equal(origin, actual.TerminalFault.OriginInvocationId);
        Assert.Equal(new byte[] { 9 }, actual.TerminalFault.Fault.Payload);
        Assert.Null(actual.TerminalFault.Fault.Report);
    }
}
