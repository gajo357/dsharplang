using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class VolatilePublicationStoreTests
{
    private static PublicationRequest Request(string app = "app", string submission = "submission",
        byte[]? payload = null, byte[]? contract = null, byte[]? options = null) =>
        new(app, submission, payload ?? [1, 2], contract ?? [3, 4], options ?? [5, 6]);

    private static PublicationIntent Intent(string subscription = "subscription", string target = "target", string selected = "selected") =>
        new(subscription, target, selected);

    private static IEnumerable<PublicationIntent> ThrowingProposals()
    {
        throw new InvalidOperationException("retained requests must not enumerate proposals");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }

    private static IEnumerable<PublicationIntent> BlockingFailure(ManualResetEventSlim started, ManualResetEventSlim release)
    {
        started.Set();
        if (!release.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException("Writer did not release proposal enumeration.");
        throw new InvalidOperationException("proposal enumeration failed");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }

    [Fact]
    public void EmptySingleAndMultipleIntentSetsAreFrozenAndRetriesIgnoreProposals()
    {
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        var emptyId = Guid.NewGuid();
        var empty = store.AcceptPublication(Request(submission: "empty"), emptyId, []);
        Assert.Equal(PublicationAcceptKind.Created, empty.Kind);
        Assert.Empty(empty.Record!.Intents);
        Assert.Empty(store.LookupPublication("app", "empty")!.Intents);

        var oneId = Guid.NewGuid();
        var one = store.AcceptPublication(Request(submission: "one"), oneId, [Intent()]);
        Assert.Equal(PublicationAcceptKind.Created, one.Kind);
        Assert.Equal(Intent(), Assert.Single(one.Record!.Intents));

        var multipleId = Guid.NewGuid();
        var proposed = new List<PublicationIntent> { Intent("a", "actor-a", "v1"), Intent("b", "actor-b", "v2") };
        var multiple = store.AcceptPublication(Request(submission: "multiple"), multipleId, proposed);
        Assert.Equal(PublicationAcceptKind.Created, multiple.Kind);
        proposed[0] = Intent("a", "changed", "changed");
        proposed.Clear();
        var changed = store.AcceptPublication(Request(submission: "multiple"), Guid.NewGuid(), [Intent("c")]);
        var invalid = store.AcceptPublication(Request(submission: "multiple"), Guid.Empty, null);
        var throwing = store.AcceptPublication(Request(submission: "multiple"), Guid.Empty, ThrowingProposals());
        foreach (var retry in new[] { changed, invalid, throwing })
        {
            Assert.Equal(PublicationAcceptKind.Existing, retry.Kind);
            Assert.Equal(multipleId, retry.Record!.PublicationId);
            Assert.Equal(new[] { Intent("a", "actor-a", "v1"), Intent("b", "actor-b", "v2") }, retry.Record.Intents);
        }
        var output = store.LookupPublication("app", "multiple")!.Intents;
        if (output is IList<PublicationIntent> list && !list.IsReadOnly)
        {
            list[0] = Intent("tampered");
            list.Clear();
        }
        var retained = store.LookupPublication("app", "multiple")!;
        Assert.Equal(multipleId, retained.PublicationId);
        Assert.Equal(new[] { Intent("a", "actor-a", "v1"), Intent("b", "actor-b", "v2") }, retained.Intents);
        Assert.Equal(new[] { (multipleId, "a"), (multipleId, "b") },
            retained.Intents.Select(intent => (retained.PublicationId, intent.SubscriptionId)));
    }

    [Fact]
    public void ByteRequestsAreCopiedAndAllChangedFieldsConflictWithoutChangingRetainedRecord()
    {
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        byte[] payload = [1, 2], contract = [3, 4], options = [5, 6];
        var original = Request(payload: payload, contract: contract, options: options);
        payload[0] = 90; contract[0] = 91; options[0] = 92;
        original.Payload[1] = 80; original.ContractMetadata[1] = 81; original.BehaviorOptions[1] = 82;
        var id = Guid.NewGuid();
        Assert.Equal(PublicationAcceptKind.Created, store.AcceptPublication(original, id, [Intent()]).Kind);
        Assert.Equal(PublicationAcceptKind.Existing, store.AcceptPublication(Request(), Guid.Empty, null).Kind);
        PublicationRequest[] changed = [Request(payload: [1, 9]), Request(contract: [3, 9]), Request(options: [5, 9])];
        foreach (var request in changed)
        {
            var conflict = store.AcceptPublication(request, Guid.Empty, ThrowingProposals());
            Assert.Equal(PublicationAcceptKind.Conflict, conflict.Kind);
            Assert.Null(conflict.Record);
        }
        var snapshot = store.LookupPublication("app", "submission")!;
        snapshot.Request.Payload[0] = 10;
        snapshot.Request.ContractMetadata[0] = 11;
        snapshot.Request.BehaviorOptions[0] = 12;
        var retained = store.LookupPublication("app", "submission")!;
        Assert.Equal(new byte[] { 1, 2 }, retained.Request.Payload);
        Assert.Equal(new byte[] { 3, 4 }, retained.Request.ContractMetadata);
        Assert.Equal(new byte[] { 5, 6 }, retained.Request.BehaviorOptions);
        Assert.Equal(id, retained.PublicationId);
        Assert.Equal(Intent(), Assert.Single(retained.Intents));

        Assert.Equal(PublicationAcceptKind.Created, store.AcceptPublication(Request(app: "other"), Guid.NewGuid(), []).Kind);
        Assert.Equal(PublicationAcceptKind.Created, store.AcceptPublication(Request(submission: "other"), Guid.NewGuid(), []).Kind);
    }

    [Fact]
    public void InvalidIntentsAndPublicationIdCollisionsNeverPartiallyInsert()
    {
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        var id = Guid.NewGuid();
        Assert.Equal(PublicationAcceptKind.Created, store.AcceptPublication(Request(), id, [Intent("original")]).Kind);
        var collision = store.AcceptPublication(Request(submission: "collision"), id, [Intent("new")]);
        Assert.Equal(PublicationAcceptKind.PublicationIdCollision, collision.Kind);
        Assert.Null(collision.Record);
        Assert.Null(store.LookupPublication("app", "collision"));
        var otherApp = store.AcceptPublication(Request(app: "other"), id, []);
        Assert.Equal(PublicationAcceptKind.PublicationIdCollision, otherApp.Kind);
        Assert.Null(otherApp.Record);
        Assert.Null(store.LookupPublication("other", "submission"));
        Assert.ThrowsAny<ArgumentException>(() => store.AcceptPublication(Request(submission: "duplicate"), Guid.NewGuid(), [Intent("same"), Intent("same", "other", "other")]));
        Assert.Null(store.LookupPublication("app", "duplicate"));
        Assert.ThrowsAny<ArgumentException>(() => store.AcceptPublication(Request(submission: "blank"), Guid.NewGuid(), [Intent("valid"), Intent("")]));
        Assert.Null(store.LookupPublication("app", "blank"));
        Assert.ThrowsAny<ArgumentException>(() => store.AcceptPublication(Request(submission: "empty-id"), Guid.Empty, []));
        Assert.Null(store.LookupPublication("app", "empty-id"));
        Assert.Equal(Intent("original"), Assert.Single(store.LookupPublication("app", "submission")!.Intents));
        var freshId = Guid.NewGuid();
        Assert.Equal(PublicationAcceptKind.Created, store.AcceptPublication(Request(submission: "collision"), freshId, [Intent("new")]).Kind);
        Assert.Equal(freshId, store.LookupPublication("app", "collision")!.PublicationId);
        Assert.Equal(PublicationAcceptKind.PublicationIdCollision, store.AcceptPublication(Request(submission: "another"), id, []).Kind);
        Assert.Null(store.LookupPublication("app", "another"));
    }

    [Fact]
    public async Task AWinningConcurrentCommitTakesPrecedenceOverFailedProposalEnumeration()
    {
        foreach (var conflict in new[] { false, true })
        {
            var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
            using var started = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            var pending = Task.Factory.StartNew(() => store.AcceptPublication(
                Request(payload: conflict ? [9] : [1, 2]), Guid.NewGuid(), BlockingFailure(started, release)),
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            try
            {
                Assert.True(started.Wait(TimeSpan.FromSeconds(30)));
                var id = Guid.NewGuid();
                Assert.Equal(PublicationAcceptKind.Created, store.AcceptPublication(Request(), id, [Intent()]).Kind);
                release.Set();
                var observed = await pending;
                Assert.Equal(conflict ? PublicationAcceptKind.Conflict : PublicationAcceptKind.Existing, observed.Kind);
                if (conflict) Assert.Null(observed.Record);
                else Assert.Equal(id, observed.Record!.PublicationId);
            }
            finally { release.Set(); }
        }
    }

    [Fact]
    public void BothCrossOperationOrdersRejectWithoutTouchingTheRetainedRecord()
    {
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        var publicationId = Guid.NewGuid();
        Assert.Equal(PublicationAcceptKind.Created, store.AcceptPublication(Request(), publicationId, [Intent()]).Kind);
        var target = new TargetedRequest("app", "submission", TargetedOperation.Dispatch, "target", [1], [3], []);
        var rejectedTarget = store.Accept(target, Guid.Empty, null);
        Assert.Equal(TargetedAcceptKind.Conflict, rejectedTarget.Kind);
        Assert.Null(rejectedTarget.Record);
        Assert.Equal(publicationId, store.LookupPublication("app", "submission")!.PublicationId);
        Assert.Null(store.Lookup("app", "submission"));

        var reverse = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        var invocationId = Guid.NewGuid();
        Assert.Equal(TargetedAcceptKind.Created, reverse.Accept(target, invocationId, "selected").Kind);
        var rejectedPublication = reverse.AcceptPublication(Request(), Guid.Empty, ThrowingProposals());
        Assert.Equal(PublicationAcceptKind.Conflict, rejectedPublication.Kind);
        Assert.Null(rejectedPublication.Record);
        Assert.Equal(invocationId, reverse.Lookup("app", "submission")!.InvocationId);
        Assert.Null(reverse.LookupPublication("app", "submission"));
    }

    [Fact]
    public void RequestAwareProbesAvoidSelectionForMatchesAndConflictsOfEitherKind()
    {
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        var target = new TargetedRequest("app", "targeted", TargetedOperation.Dispatch, "target", [1], [3], [5]);
        var publication = Request(submission: "publication");
        var targetId = Guid.NewGuid();
        var publicationId = Guid.NewGuid();
        Assert.Equal(SubmissionProbeKind.Absent, store.ProbeTargeted(target).Kind);
        Assert.Null(store.ProbeTargeted(target).Record);
        Assert.Equal(SubmissionProbeKind.Absent, store.ProbePublication(publication).Kind);
        Assert.Null(store.ProbePublication(publication).Record);
        Assert.Equal(TargetedAcceptKind.Created, store.Accept(target, targetId, "target-v1").Kind);
        Assert.Equal(PublicationAcceptKind.Created, store.AcceptPublication(publication, publicationId, [Intent()]).Kind);

        var targetMatch = store.ProbeTargeted(new TargetedRequest("app", "targeted", TargetedOperation.Dispatch, "target", [1], [3], [5]));
        Assert.Equal(SubmissionProbeKind.Existing, targetMatch.Kind);
        Assert.Equal(targetId, targetMatch.Record!.InvocationId);
        Assert.Equal("target-v1", targetMatch.Record.SelectedImplementation);
        var publicationMatch = store.ProbePublication(Request(submission: "publication"));
        Assert.Equal(SubmissionProbeKind.Existing, publicationMatch.Kind);
        Assert.Equal(publicationId, publicationMatch.Record!.PublicationId);
        Assert.Equal(Intent(), Assert.Single(publicationMatch.Record.Intents));

        var targetConflicts = new[]
        {
            new TargetedRequest("app", "targeted", TargetedOperation.Dispatch, "other", [1], [3], [5]),
            new TargetedRequest("app", "targeted", TargetedOperation.Dispatch, "target", [2], [3], [5]),
            new TargetedRequest("app", "targeted", TargetedOperation.Dispatch, "target", [1], [4], [5]),
            new TargetedRequest("app", "targeted", TargetedOperation.Dispatch, "target", [1], [3], [6]),
            new TargetedRequest("app", "targeted", TargetedOperation.Invoke, "target", [1], [3], [5]),
            new TargetedRequest("app", "publication", TargetedOperation.Dispatch, "target", [1], [3], [5])
        };
        foreach (var request in targetConflicts)
        {
            var probe = store.ProbeTargeted(request);
            Assert.Equal(SubmissionProbeKind.Conflict, probe.Kind);
            Assert.Null(probe.Record);
        }
        var publicationConflicts = new[]
        {
            Request(submission: "publication", payload: [9]),
            Request(submission: "publication", contract: [9]),
            Request(submission: "publication", options: [9]),
            Request(submission: "targeted")
        };
        foreach (var request in publicationConflicts)
        {
            var probe = store.ProbePublication(request);
            Assert.Equal(SubmissionProbeKind.Conflict, probe.Kind);
            Assert.Null(probe.Record);
        }
        Assert.Equal(SubmissionProbeKind.Absent, store.ProbeTargeted(new TargetedRequest("other", "targeted", TargetedOperation.Dispatch, "target", [1], [3], [5])).Kind);
        Assert.Equal(SubmissionProbeKind.Absent, store.ProbePublication(Request(app: "other", submission: "publication")).Kind);
        Assert.Throws<ArgumentNullException>(() => store.ProbeTargeted(null!));
        Assert.Throws<ArgumentNullException>(() => store.ProbePublication(null!));

        // A host can inspect the complete request before selecting. A later
        // Accept still arbitrates races; a probe does not reserve the key.
        var selectionCalls = 0;
        void TargetedHost(TargetedRequest request)
        {
            if (store.ProbeTargeted(request).Kind == SubmissionProbeKind.Absent) selectionCalls++;
        }
        void PublicationHost(PublicationRequest request)
        {
            if (store.ProbePublication(request).Kind == SubmissionProbeKind.Absent) selectionCalls++;
        }
        TargetedHost(target);
        foreach (var request in targetConflicts) TargetedHost(request);
        PublicationHost(publication);
        foreach (var request in publicationConflicts) PublicationHost(request);
        Assert.Equal(0, selectionCalls);
        TargetedHost(new TargetedRequest("app", "new-targeted", TargetedOperation.Dispatch, "target", [1], [3], [5]));
        PublicationHost(Request(submission: "new-publication"));
        Assert.Equal(2, selectionCalls);
    }

    [Fact]
    public async Task CompetingPublicationsAndTargetedSubmissionsShareOneAtomicNamespace()
    {
        const int count = 16;
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        using var start = new Barrier(count);
        var tasks = Enumerable.Range(0, count).Select(i => Task.Factory.StartNew(() =>
        {
            Assert.True(start.SignalAndWait(TimeSpan.FromSeconds(30)));
            return (Value: i % 2, Result: store.AcceptPublication(Request(payload: [(byte)(i % 2)]), Guid.NewGuid(), [Intent($"candidate-{i}")]));
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
        var results = await Task.WhenAll(tasks);
        var winner = Assert.Single(results, result => result.Result.Kind == PublicationAcceptKind.Created);
        foreach (var result in results)
        {
            if (result.Value == winner.Value)
            {
                Assert.True(result.Result.Kind is PublicationAcceptKind.Created or PublicationAcceptKind.Existing);
                Assert.Equal(winner.Result.Record!.PublicationId, result.Result.Record!.PublicationId);
                Assert.Equal(winner.Result.Record.Intents, result.Result.Record.Intents);
            }
            else
            {
                Assert.Equal(PublicationAcceptKind.Conflict, result.Result.Kind);
                Assert.Null(result.Result.Record);
            }
        }
        var retained = store.LookupPublication("app", "submission")!;
        Assert.Equal(winner.Result.Record!.PublicationId, retained.PublicationId);
        Assert.Equal(new[] { (byte)winner.Value }, retained.Request.Payload);

        var cross = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        using var crossStart = new Barrier(count);
        using var afterProbe = new Barrier(count);
        var contenders = Enumerable.Range(0, count).Select(i => Task.Factory.StartNew(() =>
        {
            Assert.True(crossStart.SignalAndWait(TimeSpan.FromSeconds(30)));
            if (i % 2 == 0)
            {
                var request = Request();
                var probed = cross.ProbePublication(request).Kind;
                Assert.True(afterProbe.SignalAndWait(TimeSpan.FromSeconds(30)));
                var result = cross.AcceptPublication(request, Guid.NewGuid(), [Intent()]);
                return (Publication: true, Created: result.Kind == PublicationAcceptKind.Created,
                    Conflict: result.Kind == PublicationAcceptKind.Conflict, HasRecord: result.Record is not null, Probed: probed);
            }
            var targeted = new TargetedRequest("app", "submission", TargetedOperation.Dispatch, "target", [1, 2], [3, 4], [5, 6]);
            var targetedProbe = cross.ProbeTargeted(targeted).Kind;
            Assert.True(afterProbe.SignalAndWait(TimeSpan.FromSeconds(30)));
            var accepted = cross.Accept(targeted, Guid.NewGuid(), "selected");
            return (Publication: false, Created: accepted.Kind == TargetedAcceptKind.Created,
                Conflict: accepted.Kind == TargetedAcceptKind.Conflict, HasRecord: accepted.Record is not null, Probed: targetedProbe);
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
        var outcome = await Task.WhenAll(contenders);
        Assert.All(outcome, result => Assert.Equal(SubmissionProbeKind.Absent, result.Probed));
        var crossWinner = Assert.Single(outcome, result => result.Created);
        Assert.All(outcome.Where(result => result.Publication != crossWinner.Publication), result =>
        {
            Assert.True(result.Conflict);
            Assert.False(result.HasRecord);
        });
        Assert.All(outcome.Where(result => result.Publication == crossWinner.Publication), result =>
        {
            Assert.False(result.Conflict);
            Assert.True(result.HasRecord);
        });
        Assert.Equal(crossWinner.Publication, cross.LookupPublication("app", "submission") is not null);
        Assert.Equal(!crossWinner.Publication, cross.Lookup("app", "submission") is not null);
    }
}
