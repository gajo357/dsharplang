namespace DSharp.Runtime.Tests.Features;

// Test-only scenario boundary, not a provider protocol or a codec.
internal enum OStatus
{
    Created, Existing, Conflict, Absent, Applied, NotEligible, InvalidOwner,
    Expired, NotExpired, ObservationMismatch, AlreadyRequested, AlreadyTerminal, CancellationFinalized
}
internal enum OState { None, Eligible, Running, Completed, Failed, Cancelled }
internal enum OExitKind { Normal, ReportedFault }
internal sealed record OData(byte[] Payload, byte[] Metadata);
internal sealed record OFault(byte[] Payload, byte[] Metadata, Guid? Origin);
internal sealed record OExit(OExitKind Kind, OData? Normal, OFault? Fault);
internal sealed record OAdmission(OStatus Status, Guid? Invocation, string? Selected, byte[]? Payload);
internal sealed record OSnapshot(OStatus Status, OState State, bool CancellationRequested,
    DateTimeOffset? Deadline, OData? Delivered, OFault? TerminalFault, OExit? ObservedExit);

internal interface IOutcomeScenarioStore
{
    CCapabilities Capabilities { get; }
    OAdmission Accept(string key, byte[] payload, Guid id, string selected);
    OAdmission Probe(string key, byte[] payload);
    OSnapshot Claim(string key, Guid id, Guid attempt, Guid token, TimeSpan lease);
    OSnapshot Cancel(string key, Guid id);
    OSnapshot Complete(string key, Guid id, Guid attempt, Guid token, OData value);
    OSnapshot Fail(string key, Guid id, Guid attempt, Guid token, OFault fault);
    OSnapshot RecoverRetry(string key, Guid id, Guid attempt, Guid token, DateTimeOffset observedDeadline, TimeSpan delay);
    OSnapshot Inspect(string key, Guid id);
    void Advance(TimeSpan delta);
}

internal sealed class OutcomeConformanceViolation : Exception
{
    public OutcomeConformanceViolation(string invariant, OSnapshot? observation = null)
        : base($"Outcome conformance invariant failed: {invariant}")
    {
        Invariant = invariant;
        Observation = observation;
    }
    public string Invariant { get; }
    public OSnapshot? Observation { get; }
}

internal static class OutcomeScenarios
{
    private static void Check(bool condition, string invariant, OSnapshot? observation = null)
    {
        if (!condition) throw new OutcomeConformanceViolation(invariant, observation);
    }

    private static bool Same(OData? data, byte[] payload, byte[] metadata) =>
        data is not null && data.Payload.SequenceEqual(payload) && data.Metadata.SequenceEqual(metadata);
    private static bool Same(OFault? data, byte[] payload, byte[] metadata, Guid? origin) =>
        data is not null && data.Payload.SequenceEqual(payload) && data.Metadata.SequenceEqual(metadata) && data.Origin == origin;

    public static void RunAll(Func<CRequirement, IOutcomeScenarioStore> factory)
    {
        var caps = factory(CRequirement.None).Capabilities;
        Check(caps.ByteExact && caps.InProcessAtomic && caps.ControlledTime, "outcome.capabilities.subset");
        foreach (var (requirement, supported) in new[]
        {
            (CRequirement.Durable, caps.Durable),
            (CRequirement.Canonical, caps.Canonical),
            (CRequirement.FollowUps, caps.FollowUps)
        })
        {
            if (supported) continue;
            var rejected = false;
            try { _ = factory(requirement); }
            catch (NotSupportedException) { rejected = true; }
            Check(rejected, $"outcome.capabilities.rejected-{requirement}");
        }
        CancellationNormal(factory);
        CancellationFault(factory);
        TerminalFirst(factory, false);
        TerminalFirst(factory, true);
        ExpiredCancellation(factory);
    }

    public static void CancellationNormal(Func<CRequirement, IOutcomeScenarioStore> factory)
    {
        var store = factory(CRequirement.None);
        const string key = "outcome-normal";
        var id = Guid.NewGuid(); var attempt = Guid.NewGuid(); var token = Guid.NewGuid();
        Check(store.Accept(key, [1, 2], id, "pinned").Status == OStatus.Created, "cancellation.accept");
        Check(store.Claim(key, id, attempt, token, TimeSpan.FromSeconds(10)).Status == OStatus.Applied, "cancellation.claim");
        var request = store.Cancel(key, id);
        // Capture pending state, but inspect and check the actual post-exit state
        // first: the negative control must reveal real forbidden delivery.
        var pending = store.Inspect(key, id);
        byte[] payload = [0, 69, 82, 82]; byte[] metadata = [3, 4];
        var expectedPayload = payload.ToArray(); var expectedMetadata = metadata.ToArray();
        var completion = store.Complete(key, id, attempt, token, new OData(payload, metadata));
        payload[0] = 99; metadata[0] = 99;
        var observed = store.Inspect(key, id);
        Check(observed.State == OState.Cancelled && observed.Delivered is null,
            "cancellation.request-lost-delivered", observed);
        Check(request.Status == OStatus.Applied && pending.State == OState.Running && pending.CancellationRequested,
            "cancellation.pending", pending);
        Check(completion.Status == OStatus.CancellationFinalized && observed.CancellationRequested &&
              observed.ObservedExit?.Kind == OExitKind.Normal && observed.ObservedExit.Fault is null &&
              Same(observed.ObservedExit.Normal, expectedPayload, expectedMetadata), "cancellation.normal-observed", observed);
        observed.ObservedExit!.Normal!.Payload[0] = 77;
        observed.ObservedExit.Normal.Metadata[0] = 77;
        var readAgain = store.Inspect(key, id);
        Check(Same(readAgain.ObservedExit?.Normal, expectedPayload, expectedMetadata), "cancellation.normal-copied", readAgain);
        Check(store.Cancel(key, id).Status == OStatus.AlreadyRequested, "cancellation.idempotent");
        Check(store.Complete(key, id, attempt, token, new OData([8], [1])).Status == OStatus.NotEligible,
            "cancellation.repeat-no-delivery");
        var accepted = store.Probe(key, [1, 2]);
        Check(accepted.Status == OStatus.Existing && accepted.Invocation == id && accepted.Selected == "pinned" &&
              accepted.Payload is not null && accepted.Payload.SequenceEqual(new byte[] { 1, 2 }), "cancellation.identity");
    }

    private static void CancellationFault(Func<CRequirement, IOutcomeScenarioStore> factory)
    {
        var store = factory(CRequirement.None);
        const string key = "outcome-fault";
        var id = Guid.NewGuid(); var attempt = Guid.NewGuid(); var token = Guid.NewGuid(); var origin = Guid.NewGuid();
        Check(store.Accept(key, [1], id, "pinned").Status == OStatus.Created, "fault.accept");
        Check(store.Claim(key, id, attempt, token, TimeSpan.FromSeconds(10)).Status == OStatus.Applied, "fault.claim");
        Check(store.Cancel(key, id).Status == OStatus.Applied, "fault.cancel");
        byte[] payload = [5, 6]; byte[] metadata = [7, 8];
        var fault = store.Fail(key, id, attempt, token, new OFault(payload, metadata, origin));
        payload[0] = 99; metadata[0] = 99;
        var observed = store.Inspect(key, id);
        Check(fault.Status == OStatus.CancellationFinalized && observed.State == OState.Cancelled &&
              observed.Delivered is null && observed.TerminalFault is null && observed.ObservedExit?.Kind == OExitKind.ReportedFault &&
              observed.ObservedExit.Normal is null && Same(observed.ObservedExit.Fault, [5, 6], [7, 8], origin),
            "fault.observed-only", observed);
        observed.ObservedExit!.Fault!.Payload[0] = 88;
        observed.ObservedExit.Fault.Metadata[0] = 88;
        var readAgain = store.Inspect(key, id);
        Check(Same(readAgain.ObservedExit?.Fault, [5, 6], [7, 8], origin), "fault.copied", readAgain);
    }

    private static void TerminalFirst(Func<CRequirement, IOutcomeScenarioStore> factory, bool reportedFault)
    {
        var store = factory(CRequirement.None);
        var key = reportedFault ? "fault-first" : "normal-first";
        var id = Guid.NewGuid(); var attempt = Guid.NewGuid(); var token = Guid.NewGuid();
        Check(store.Accept(key, [1], id, "pinned").Status == OStatus.Created, "terminal.accept");
        Check(store.Claim(key, id, attempt, token, TimeSpan.FromSeconds(10)).Status == OStatus.Applied, "terminal.claim");
        var origin = Guid.NewGuid();
        var finish = reportedFault
            ? store.Fail(key, id, attempt, token, new OFault([9], [2], origin))
            : store.Complete(key, id, attempt, token, new OData([0, 69, 82, 82], [2]));
        Check(finish.Status == OStatus.Applied, "terminal.finish", finish);
        var request = store.Cancel(key, id);
        var observed = store.Inspect(key, id);
        Check(request.Status == OStatus.AlreadyTerminal && !observed.CancellationRequested && observed.ObservedExit is null &&
              (reportedFault
                  ? observed.State == OState.Failed && observed.Delivered is null && Same(observed.TerminalFault, [9], [2], origin)
                  : observed.State == OState.Completed && observed.TerminalFault is null && Same(observed.Delivered, [0, 69, 82, 82], [2])),
            "terminal.first-wins", observed);
    }

    private static void ExpiredCancellation(Func<CRequirement, IOutcomeScenarioStore> factory)
    {
        var store = factory(CRequirement.None);
        const string key = "outcome-expired";
        var id = Guid.NewGuid(); var attempt = Guid.NewGuid(); var token = Guid.NewGuid();
        Check(store.Accept(key, [1], id, "pinned").Status == OStatus.Created, "expiry.accept");
        var claimed = store.Claim(key, id, attempt, token, TimeSpan.FromSeconds(10));
        Check(claimed.Status == OStatus.Applied && claimed.Deadline.HasValue, "expiry.claim", claimed);
        Check(store.Cancel(key, id).Status == OStatus.Applied, "expiry.cancel");
        Check(store.RecoverRetry(key, id, attempt, token, claimed.Deadline!.Value, TimeSpan.Zero).Status == OStatus.NotExpired,
            "expiry.not-yet");
        store.Advance(TimeSpan.FromSeconds(10));
        Check(store.Complete(key, id, attempt, token, new OData([7], [1])).Status == OStatus.Expired,
            "expiry.stale-exit");
        var recovered = store.RecoverRetry(key, id, attempt, token, claimed.Deadline!.Value, TimeSpan.Zero);
        var observed = store.Inspect(key, id);
        Check(recovered.Status == OStatus.CancellationFinalized && observed.State == OState.Cancelled &&
              observed.CancellationRequested && observed.Delivered is null && observed.TerminalFault is null && observed.ObservedExit is null,
            "expiry.cancelled-no-exit", observed);
        Check(store.Claim(key, id, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromSeconds(1)).Status == OStatus.NotEligible,
            "expiry.no-retry");
    }
}
