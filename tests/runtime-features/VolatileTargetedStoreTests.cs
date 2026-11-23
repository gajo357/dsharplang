using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class VolatileTargetedStoreTests
{
    private static TargetedRequest Request(
        string app = "app", string submission = "submission",
        TargetedOperation operation = TargetedOperation.Invoke, string target = "actor",
        byte[]? payload = null, byte[]? contract = null, byte[]? options = null,
        string? parent = "parent", string? attempt = "attempt") =>
        new(app, submission, operation, target, payload ?? [1, 2], contract ?? [3, 4],
            options ?? [5, 6], parent, attempt);

    [Fact]
    public void MatchingRetriesPinTheFirstIdentityAndSelectionAndNamespaceIsolated()
    {
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        var firstId = Guid.NewGuid();
        var first = store.Accept(Request(), firstId, "implementation-v1");
        Assert.Equal(TargetedAcceptKind.Created, first.Kind);
        Assert.NotNull(first.Record);
        Assert.Equal(firstId, first.Record.InvocationId);
        Assert.Equal("implementation-v1", first.Record.SelectedImplementation);

        var retry = store.Accept(Request(), Guid.NewGuid(), "implementation-v2");
        Assert.Equal(TargetedAcceptKind.Existing, retry.Kind);
        Assert.NotNull(retry.Record);
        Assert.Equal(firstId, retry.Record.InvocationId);
        Assert.Equal("implementation-v1", retry.Record.SelectedImplementation);
        var retryBeforeSelection = store.Accept(Request(), Guid.Empty, null);
        Assert.Equal(TargetedAcceptKind.Existing, retryBeforeSelection.Kind);
        Assert.Equal(firstId, retryBeforeSelection.Record!.InvocationId);
        Assert.Equal("implementation-v1", retryBeforeSelection.Record.SelectedImplementation);
        var lookup = store.Lookup("app", "submission");
        Assert.NotNull(lookup);
        Assert.Equal(firstId, lookup.InvocationId);
        Assert.Equal("implementation-v1", lookup.SelectedImplementation);

        Assert.Null(store.Lookup("other", "submission"));
        Assert.Null(store.Lookup("app", "other"));
        var otherApp = store.Accept(Request(app: "other"), Guid.NewGuid(), "other-impl");
        var otherKey = store.Accept(Request(submission: "other"), Guid.NewGuid(), "third-impl");
        Assert.Equal(TargetedAcceptKind.Created, otherApp.Kind);
        Assert.Equal(TargetedAcceptKind.Created, otherKey.Kind);
        Assert.NotEqual(firstId, otherApp.Record!.InvocationId);
        Assert.NotEqual(firstId, otherKey.Record!.InvocationId);
        Assert.Equal(firstId, store.Lookup("app", "submission")!.InvocationId);
    }

    [Fact]
    public void FullRequestComparisonRejectsEverySemanticChangeAndSnapshotsAreCopied()
    {
        var store = new VolatileTargetedStore(TimeSpan.FromSeconds(10));
        byte[] payload = [1, 2], contract = [3, 4], options = [5, 6];
        var request = Request(payload: payload, contract: contract, options: options);
        payload[0] = 90;
        contract[0] = 91;
        options[0] = 92;
        var exposedPayload = request.Payload;
        var exposedContract = request.ContractMetadata;
        var exposedOptions = request.BehaviorOptions;
        exposedPayload[1] = 80;
        exposedContract[1] = 81;
        exposedOptions[1] = 82;

        var id = Guid.NewGuid();
        Assert.Equal(TargetedAcceptKind.Created, store.Accept(request, id, "selected").Kind);
        Assert.Equal(TargetedAcceptKind.Existing, store.Accept(Request(), Guid.NewGuid(), "changed").Kind);
        TargetedRequest[] conflicts =
        [
            Request(target: "different"),
            Request(payload: [1, 9]),
            Request(contract: [3, 9]),
            Request(options: [5, 9]),
            Request(parent: "other-parent"),
            Request(attempt: "other-attempt"),
            Request(parent: null, attempt: null),
            Request(operation: TargetedOperation.Dispatch, parent: null, attempt: null)
        ];
        foreach (var conflict in conflicts)
        {
            var rejected = store.Accept(conflict, Guid.NewGuid(), "different");
            Assert.Equal(TargetedAcceptKind.Conflict, rejected.Kind);
            Assert.Null(rejected.Record);
            var unchanged = store.Lookup("app", "submission")!;
            Assert.Equal(id, unchanged.InvocationId);
            Assert.Equal("selected", unchanged.SelectedImplementation);
            Assert.Equal(new byte[] { 1, 2 }, unchanged.Request.Payload);
        }

        var snapshot = store.Lookup("app", "submission")!;
        Assert.Equal(new byte[] { 1, 2 }, snapshot.Request.Payload);
        Assert.Equal(new byte[] { 3, 4 }, snapshot.Request.ContractMetadata);
        Assert.Equal(new byte[] { 5, 6 }, snapshot.Request.BehaviorOptions);
        snapshot.Request.Payload[0] = 70;
        snapshot.Request.ContractMetadata[0] = 71;
        snapshot.Request.BehaviorOptions[0] = 72;
        var later = store.Lookup("app", "submission")!;
        Assert.Equal(new byte[] { 1, 2 }, later.Request.Payload);
        Assert.Equal(new byte[] { 3, 4 }, later.Request.ContractMetadata);
        Assert.Equal(new byte[] { 5, 6 }, later.Request.BehaviorOptions);
        Assert.Equal(id, later.InvocationId);
        Assert.Equal("selected", later.SelectedImplementation);

        var noParent = new VolatileTargetedStore(TimeSpan.FromSeconds(10));
        Assert.Equal(TargetedAcceptKind.Created,
            noParent.Accept(Request(parent: null, attempt: null), Guid.NewGuid(), "selected").Kind);
        Assert.Equal(TargetedAcceptKind.Conflict,
            noParent.Accept(Request(operation: TargetedOperation.Dispatch, parent: null, attempt: null), Guid.NewGuid(), "selected").Kind);
    }

    [Fact]
    public async Task CompetingIdenticalAndConflictingSubmissionsHaveOneWinner()
    {
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        const int contenders = 16;
        using var start = new Barrier(contenders);
        var calls = Enumerable.Range(0, contenders).Select(i => Task.Factory.StartNew(() =>
        {
            start.SignalAndWait();
            return store.Accept(Request(), Guid.NewGuid(), $"candidate-{i}");
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
        var results = await Task.WhenAll(calls);
        var winner = Assert.Single(results, result => result.Kind == TargetedAcceptKind.Created);
        Assert.Equal(contenders - 1, results.Count(result => result.Kind == TargetedAcceptKind.Existing));
        Assert.All(results, result =>
        {
            Assert.NotNull(result.Record);
            Assert.Equal(winner.Record!.InvocationId, result.Record.InvocationId);
            Assert.Equal(winner.Record.SelectedImplementation, result.Record.SelectedImplementation);
        });

        var conflicting = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        using var raceStart = new Barrier(contenders);
        var races = Enumerable.Range(0, contenders).Select(i => Task.Factory.StartNew(() =>
        {
            raceStart.SignalAndWait();
            return (Value: i % 2, Result: conflicting.Accept(Request(payload: [(byte)(i % 2)]), Guid.NewGuid(), $"candidate-{i}"));
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
        var raced = await Task.WhenAll(races);
        var accepted = Assert.Single(raced, result => result.Result.Kind == TargetedAcceptKind.Created);
        foreach (var result in raced)
        {
            Assert.Equal(result.Value == accepted.Value
                ? (result.Result.Kind == TargetedAcceptKind.Created ? TargetedAcceptKind.Created : TargetedAcceptKind.Existing)
                : TargetedAcceptKind.Conflict, result.Result.Kind);
            if (result.Value == accepted.Value)
                Assert.Equal(accepted.Result.Record!.InvocationId, result.Result.Record!.InvocationId);
            else
                Assert.Null(result.Result.Record);
        }
        Assert.Equal(new[] { (byte)accepted.Value }, conflicting.Lookup("app", "submission")!.Request.Payload);
    }

    [Fact]
    public void ConfigurationAndInvocationIdCollisionsFailClosed()
    {
        Assert.ThrowsAny<ArgumentException>(() => new VolatileTargetedStore(TimeSpan.Zero));
        Assert.ThrowsAny<ArgumentException>(() => new VolatileTargetedStore(TimeSpan.FromTicks(-1)));
        Assert.ThrowsAny<ArgumentException>(() => new VolatileTargetedStore(TimeSpan.MaxValue));
        Assert.Throws<NotSupportedException>(() => new VolatileTargetedStore(TimeSpan.FromMinutes(1), VolatileStoreRequirements.Durability));
        Assert.Throws<NotSupportedException>(() => new VolatileTargetedStore(TimeSpan.FromMinutes(1), VolatileStoreRequirements.AtomicFollowUps));
        Assert.Throws<NotSupportedException>(() => new VolatileTargetedStore(TimeSpan.FromMinutes(1), VolatileStoreRequirements.CanonicalSemanticEquality));
        Assert.Throws<NotSupportedException>(() => new VolatileTargetedStore(TimeSpan.FromMinutes(1), (VolatileStoreRequirements)128));
        var store = new VolatileTargetedStore(TimeSpan.FromTicks(1));
        Assert.Equal(TimeSpan.FromTicks(1), store.RetentionHorizon);
        Assert.False(store.IsDurable);
        Assert.False(store.SupportsAtomicFollowUps);
        var id = Guid.NewGuid();
        Assert.Equal(TargetedAcceptKind.Created, store.Accept(Request(), id, "selected").Kind);
        var collision = store.Accept(Request(submission: "other"), id, "selected");
        Assert.Equal(TargetedAcceptKind.InvocationIdCollision, collision.Kind);
        Assert.Null(collision.Record);
        Assert.Null(store.Lookup("app", "other"));
        Assert.Equal(TargetedAcceptKind.InvocationIdCollision, store.Accept(Request(app: "other"), id, "selected").Kind);
        Assert.Null(store.Lookup("other", "submission"));
        Assert.Equal(id, store.Lookup("app", "submission")!.InvocationId);
    }

    [Fact]
    public void InvalidAdmissionInputsCannotCreateARecord()
    {
        Assert.ThrowsAny<ArgumentException>(() => Request(app: " "));
        Assert.ThrowsAny<ArgumentException>(() => Request(submission: ""));
        Assert.ThrowsAny<ArgumentException>(() => Request(target: "\t"));
        Assert.ThrowsAny<ArgumentException>(() => Request(operation: (TargetedOperation)999));
        Assert.ThrowsAny<ArgumentException>(() => Request(contract: []));
        Assert.ThrowsAny<ArgumentException>(() => Request(parent: "parent", attempt: null));
        Assert.ThrowsAny<ArgumentException>(() => Request(parent: null, attempt: "attempt"));
        Assert.ThrowsAny<ArgumentException>(() => Request(parent: "", attempt: "attempt"));
        Assert.ThrowsAny<ArgumentException>(() => Request(operation: TargetedOperation.Dispatch));

        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        Assert.ThrowsAny<ArgumentException>(() => store.Accept(Request(), Guid.Empty, "selection"));
        Assert.Null(store.Lookup("app", "submission"));
        Assert.ThrowsAny<ArgumentException>(() => store.Accept(Request(), Guid.NewGuid(), " "));
        Assert.Null(store.Lookup("app", "submission"));
        Assert.Equal(TargetedAcceptKind.Created, store.Accept(Request(payload: [], options: []), Guid.NewGuid(), "selection").Kind);
    }
}
