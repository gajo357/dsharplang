using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

// Replaces only a stale observed deadline after the renewed lease itself has
// expired. The actual recovery still executes on the real backing store.
internal sealed class StaleContractDeadlineDecorator(VolatileTargetedStore backing) : IRuntimeStore
{
    private IRuntimeStore Inner => backing;
    public string? MutatedApplication { get; private set; }
    public string? MutatedSubmission { get; private set; }
    public Guid MutatedInvocation { get; private set; }
    public Guid MutatedAttempt { get; private set; }
    public Guid MutatedToken { get; private set; }
    public DateTimeOffset? SubmittedDeadline { get; private set; }

    public RuntimeStoreCapabilities Capabilities => Inner.Capabilities;
    public TimeSpan RetentionHorizon => Inner.RetentionHorizon;
    public void ValidateRequirements(RuntimeStoreGuarantees required) => Inner.ValidateRequirements(required);
    public TargetedProbeResult ProbeTargeted(TargetedRequest request) => Inner.ProbeTargeted(request);
    public PublicationProbeResult ProbePublication(PublicationRequest request) => Inner.ProbePublication(request);
    public TargetedRecord? Lookup(string applicationId, string submissionId) => Inner.Lookup(applicationId, submissionId);
    public PublicationRecord? LookupPublication(string applicationId, string submissionId) => Inner.LookupPublication(applicationId, submissionId);
    public TargetedAcceptResult AcceptUnvalidated(TargetedRequest request, Guid invocationId, string? selectedImplementation) =>
        Inner.AcceptUnvalidated(request, invocationId, selectedImplementation);
    public PublicationAcceptResult AcceptPublication(PublicationRequest request, Guid publicationId, IEnumerable<PublicationIntent>? intents) =>
        Inner.AcceptPublication(request, publicationId, intents);
    public OwnedInvokeResult AcceptOwnedInvoke(TargetedRequest request, Guid childInvocationId, string? selectedImplementation,
        string parentSubmissionId, Guid parentInvocationId, Guid parentAttemptId, Guid parentToken) =>
        Inner.AcceptOwnedInvoke(request, childInvocationId, selectedImplementation, parentSubmissionId, parentInvocationId, parentAttemptId, parentToken);
    public TargetedOwnership? GetOwnership(string applicationId, string submissionId, Guid invocationId) =>
        Inner.GetOwnership(applicationId, submissionId, invocationId);
    public AttemptTransitionResult Claim(string applicationId, string submissionId, Guid invocationId, Guid attemptId, Guid token, TimeSpan duration) =>
        Inner.Claim(applicationId, submissionId, invocationId, attemptId, token, duration);
    public AttemptTransitionResult Renew(string applicationId, string submissionId, Guid invocationId, Guid attemptId, Guid token, TimeSpan duration) =>
        Inner.Renew(applicationId, submissionId, invocationId, attemptId, token, duration);
    public AttemptTransitionResult Complete(string applicationId, string submissionId, Guid invocationId, Guid attemptId, Guid token, TargetedCompletion completion) =>
        Inner.Complete(applicationId, submissionId, invocationId, attemptId, token, completion);
    public AttemptTransitionResult FailAfterFault(string applicationId, string submissionId, Guid invocationId, Guid attemptId, Guid token, ReportedTerminalFault fault) =>
        Inner.FailAfterFault(applicationId, submissionId, invocationId, attemptId, token, fault);
    public AttemptTransitionResult RescheduleAfterFault(string applicationId, string submissionId, Guid invocationId, Guid attemptId, Guid token, TimeSpan delay, TargetedFault fault) =>
        Inner.RescheduleAfterFault(applicationId, submissionId, invocationId, attemptId, token, delay, fault);

    public AttemptTransitionResult RecoverExpiredAsRetry(string applicationId, string submissionId, Guid invocationId,
        Guid observedAttemptId, Guid observedToken, DateTimeOffset observedLeaseUntil, TimeSpan delay)
    {
        var current = Inner.GetOwnership(applicationId, submissionId, invocationId);
        var shouldSubstitute = current?.State == TargetedAttemptState.Running &&
            current.AttemptId == observedAttemptId && current.Token == observedToken &&
            current.LeaseUntil is { } deadline && deadline != observedLeaseUntil && backing.CurrentTime >= deadline;
        var submitted = shouldSubstitute ? current!.LeaseUntil!.Value : observedLeaseUntil;
        var result = Inner.RecoverExpiredAsRetry(applicationId, submissionId, invocationId, observedAttemptId, observedToken, submitted, delay);
        if (shouldSubstitute && result.Status == AttemptTransitionStatus.Applied)
        {
            MutatedApplication = applicationId;
            MutatedSubmission = submissionId;
            MutatedInvocation = invocationId;
            MutatedAttempt = observedAttemptId;
            MutatedToken = observedToken;
            SubmittedDeadline = submitted;
        }
        return result;
    }

    public AttemptTransitionResult RecoverExpiredAsFailed(string applicationId, string submissionId, Guid invocationId,
        Guid observedAttemptId, Guid observedToken, DateTimeOffset observedLeaseUntil) =>
        Inner.RecoverExpiredAsFailed(applicationId, submissionId, invocationId, observedAttemptId, observedToken, observedLeaseUntil);
    public CancellationRequestResult RequestCancellation(string applicationId, string submissionId, Guid invocationId) =>
        Inner.RequestCancellation(applicationId, submissionId, invocationId);
}

// This factory exposes a real in-process reference only. Clock control is a
// separate test fixture and is not part of the provisional provider contract.
public sealed class RuntimeStoreContractTests
{
    private static readonly DateTimeOffset InitialTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static VolatileTargetedStore NewStore()
    {
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        store.InitializeClock(InitialTime);
        return store;
    }

    private static ContractFixture Fixture(VolatileTargetedStore store, IRuntimeStore provider) =>
        new(provider, delta => store.AdvanceClock(store.CurrentTime.Add(delta)));

    [Fact]
    public async Task VolatileReferencePassesTheInterfaceDrivenScenarios()
    {
        await RuntimeStoreContractScenarios.RunAll(() =>
        {
            var backing = NewStore();
            return Fixture(backing, backing);
        });
    }

    [Fact]
    public void SharedScenarioDetectsARealStaleDeadlineRecovery()
    {
        VolatileTargetedStore? backing = null;
        StaleContractDeadlineDecorator? broken = null;
        var violation = Assert.Throws<ContractScenarioViolation>(() =>
            RuntimeStoreContractScenarios.StaleObservation(() =>
            {
                backing = NewStore();
                broken = new StaleContractDeadlineDecorator(backing);
                return Fixture(backing, broken);
            }));

        Assert.Equal("contract.stale-observation-mutated", violation.Invariant);
        Assert.Equal(TargetedAttemptState.Eligible, violation.Observation!.State);
        Assert.NotNull(backing);
        Assert.NotNull(broken);
        Assert.NotNull(broken.MutatedApplication);
        Assert.NotNull(broken.MutatedSubmission);
        var actual = backing.GetOwnership(broken.MutatedApplication, broken.MutatedSubmission, broken.MutatedInvocation);
        Assert.NotNull(actual);
        Assert.Equal(TargetedAttemptState.Eligible, actual.State);
        var recovery = Assert.Single(actual.RecoveryHistory);
        Assert.Equal(AttemptRecoveryKind.LeaseExpired, recovery.Kind);
        Assert.Null(recovery.Fault);
        Assert.Equal(broken.MutatedAttempt, recovery.AttemptId);
        Assert.Equal(broken.MutatedToken, recovery.Token);
        Assert.Equal(broken.SubmittedDeadline, recovery.LeaseUntil);
    }
}
