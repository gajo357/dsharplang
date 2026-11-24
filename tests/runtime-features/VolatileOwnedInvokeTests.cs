using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class VolatileOwnedInvokeTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private const string App = "owned-app";
    private const string ParentKey = "parent";

    private sealed class Fixture
    {
        public VolatileTargetedStore Store { get; } = new(TimeSpan.FromMinutes(1));
        public Guid ParentId { get; } = Guid.Parse("abcdef01-2345-6789-abcd-ef0123456789");
        public Guid Attempt { get; } = Guid.Parse("abcdef11-2345-6789-abcd-ef0123456789");
        public Guid Token { get; } = Guid.Parse("abcdef21-2345-6789-abcd-ef0123456789");

        public Fixture()
        {
            Store.InitializeClock(Start);
            var parent = new TargetedRequest(App, ParentKey, TargetedOperation.Dispatch, "parent-target", [1], [2], []);
            Assert.Equal(TargetedAcceptKind.Created, Store.Accept(parent, ParentId, "parent-impl").Kind);
            Assert.Equal(AttemptTransitionStatus.Applied,
                Store.Claim(App, ParentKey, ParentId, Attempt, Token, TimeSpan.FromSeconds(10)).Status);
        }

        public TargetedRequest Child(string key = "child", byte[]? payload = null, string app = App,
            string? parent = null, string? attempt = null) =>
            new(app, key, TargetedOperation.Invoke, "child-target", payload ?? [5, 6], [7], [8],
                parent ?? ParentId.ToString("D"), attempt ?? Attempt.ToString("D"));

        public OwnedInvokeResult Admit(TargetedRequest request, Guid id, string? selected = "child-impl") =>
            Store.AcceptOwnedInvoke(request, id, selected, ParentKey, ParentId, Attempt, Token);
    }

    [Fact]
    public void LiveOwnerPinsChildAndUsesSharedSubmissionNamespace()
    {
        var f = new Fixture();
        var id = Guid.NewGuid();
        byte[] input = [5, 6];
        var request = f.Child(payload: input);
        input[0] = 99;
        var created = f.Admit(request, id);
        Assert.Equal(OwnedInvokeStatus.Created, created.Status);
        Assert.NotNull(created.Record);
        Assert.Equal(id, created.Record.InvocationId);
        Assert.Equal("child-impl", created.Record.SelectedImplementation);
        Assert.Equal(f.ParentId.ToString("D"), created.Record.Request.ParentInvocationId);
        Assert.Equal(f.Attempt.ToString("D"), created.Record.Request.ParentAttemptId);
        Assert.Equal(new byte[] { 5, 6 }, created.Record.Request.Payload);
        created.Record.Request.Payload[0] = 77;

        var retry = f.Admit(f.Child(), Guid.Empty, null);
        Assert.Equal(OwnedInvokeStatus.Existing, retry.Status);
        Assert.Equal(id, retry.Record!.InvocationId);
        Assert.Equal("child-impl", retry.Record.SelectedImplementation);
        Assert.Equal(new byte[] { 5, 6 }, retry.Record.Request.Payload);
        var conflict = f.Admit(f.Child(payload: [5, 9]), Guid.NewGuid(), "other");
        Assert.Equal(OwnedInvokeStatus.Conflict, conflict.Status);
        Assert.Null(conflict.Record);
        Assert.Equal(id, f.Store.ProbeTargeted(f.Child()).Record!.InvocationId);

        var publication = new PublicationRequest(App, "publication", [3], [4], []);
        Assert.Equal(PublicationAcceptKind.Created, f.Store.AcceptPublication(publication, Guid.NewGuid(), []).Kind);
        var crossKind = f.Admit(f.Child(key: "publication"), Guid.NewGuid());
        Assert.Equal(OwnedInvokeStatus.Conflict, crossKind.Status);
        Assert.Null(crossKind.Record);
        Assert.Equal(SubmissionProbeKind.Existing, f.Store.ProbePublication(publication).Kind);

        var colliding = f.Admit(f.Child(key: "collision"), f.ParentId);
        Assert.Equal(OwnedInvokeStatus.InvocationIdCollision, colliding.Status);
        Assert.Null(colliding.Record);
        Assert.Equal(SubmissionProbeKind.Absent, f.Store.ProbeTargeted(f.Child(key: "collision")).Kind);
    }

    [Fact]
    public void InvalidParentIdentityAndMetadataAreRejectedWithoutChildMutation()
    {
        var f = new Fixture();
        var id = Guid.NewGuid();
        Assert.Throws<ArgumentNullException>(() => f.Store.AcceptOwnedInvoke(null!, id, "impl", ParentKey, f.ParentId, f.Attempt, f.Token));
        Assert.ThrowsAny<ArgumentException>(() => f.Store.AcceptOwnedInvoke(f.Child(), id, "impl", " ", f.ParentId, f.Attempt, f.Token));
        Assert.ThrowsAny<ArgumentException>(() => f.Store.AcceptOwnedInvoke(f.Child(), id, "impl", ParentKey, Guid.Empty, f.Attempt, f.Token));
        Assert.ThrowsAny<ArgumentException>(() => f.Store.AcceptOwnedInvoke(f.Child(), id, "impl", ParentKey, f.ParentId, Guid.Empty, f.Token));
        Assert.ThrowsAny<ArgumentException>(() => f.Store.AcceptOwnedInvoke(f.Child(), id, "impl", ParentKey, f.ParentId, f.Attempt, Guid.Empty));
        Assert.ThrowsAny<ArgumentException>(() => f.Admit(f.Child(parent: Guid.NewGuid().ToString("D")), id));
        Assert.ThrowsAny<ArgumentException>(() => f.Admit(f.Child(attempt: Guid.NewGuid().ToString("D")), id));
        Assert.ThrowsAny<ArgumentException>(() => f.Admit(f.Child(parent: f.ParentId.ToString("D").ToUpperInvariant()), id));
        Assert.ThrowsAny<ArgumentException>(() => f.Admit(f.Child(parent: f.ParentId.ToString("N")), id));
        var root = new TargetedRequest(App, "child", TargetedOperation.Invoke, "child-target", [1], [2], []);
        var dispatch = new TargetedRequest(App, "child", TargetedOperation.Dispatch, "child-target", [1], [2], []);
        Assert.ThrowsAny<ArgumentException>(() => f.Admit(root, id));
        Assert.ThrowsAny<ArgumentException>(() => f.Admit(dispatch, id));
        Assert.Equal(SubmissionProbeKind.Absent, f.Store.ProbeTargeted(f.Child()).Kind);
    }

    [Fact]
    public void ParentFenceRequiresSameApplicationAndCurrentUnexpiredOwner()
    {
        var f = new Fixture();
        var id = Guid.NewGuid();
        Assert.Equal(OwnedInvokeStatus.ParentUnavailable,
            f.Store.AcceptOwnedInvoke(f.Child(), id, "impl", "wrong", f.ParentId, f.Attempt, f.Token).Status);
        Assert.Equal(OwnedInvokeStatus.ParentUnavailable,
            f.Store.AcceptOwnedInvoke(f.Child(app: "other"), id, "impl", ParentKey, f.ParentId, f.Attempt, f.Token).Status);
        Assert.Equal(OwnedInvokeStatus.ParentUnavailable,
            f.Store.AcceptOwnedInvoke(f.Child(), id, "impl", ParentKey, f.ParentId, f.Attempt, Guid.NewGuid()).Status);
        var unknownParent = Guid.NewGuid();
        Assert.Equal(OwnedInvokeStatus.ParentUnavailable,
            f.Store.AcceptOwnedInvoke(f.Child(parent: unknownParent.ToString("D")), id, "impl", ParentKey, unknownParent, f.Attempt, f.Token).Status);
        Assert.Equal(SubmissionProbeKind.Absent, f.Store.ProbeTargeted(f.Child()).Kind);
        Assert.Equal(CancellationRequestStatus.Applied, f.Store.RequestCancellation(App, ParentKey, f.ParentId).Status);
        Assert.Equal(OwnedInvokeStatus.Created, f.Admit(f.Child(key: "during-pending"), Guid.NewGuid()).Status);
        f.Store.AdvanceClock(Start.AddSeconds(10));
        Assert.Equal(OwnedInvokeStatus.ParentUnavailable, f.Admit(f.Child(), id).Status);
        Assert.Equal(OwnedInvokeStatus.ParentUnavailable, f.Admit(f.Child(key: "during-pending"), Guid.Empty, null).Status);
        Assert.Equal(SubmissionProbeKind.Existing, f.Store.ProbeTargeted(f.Child(key: "during-pending")).Kind);
        Assert.Equal(AttemptTransitionStatus.CancellationFinalized,
            f.Store.RecoverExpiredAsRetry(App, ParentKey, f.ParentId, f.Attempt, f.Token, Start.AddSeconds(10), TimeSpan.Zero).Status);
        Assert.Equal(OwnedInvokeStatus.ParentUnavailable, f.Admit(f.Child(), id).Status);
        Assert.Equal(SubmissionProbeKind.Absent, f.Store.ProbeTargeted(f.Child()).Kind);

        var terminal = new Fixture();
        Assert.Equal(AttemptTransitionStatus.Applied, terminal.Store.Complete(App, ParentKey, terminal.ParentId,
            terminal.Attempt, terminal.Token, new TargetedCompletion([], [1])).Status);
        Assert.Equal(OwnedInvokeStatus.ParentUnavailable, terminal.Admit(terminal.Child(), id).Status);
        Assert.Equal(SubmissionProbeKind.Absent, terminal.Store.ProbeTargeted(terminal.Child()).Kind);

        var replaced = new Fixture();
        var oldChildId = Guid.NewGuid();
        Assert.Equal(OwnedInvokeStatus.Created, replaced.Admit(replaced.Child(), oldChildId).Status);
        replaced.Store.AdvanceClock(Start.AddSeconds(10));
        Assert.Equal(AttemptTransitionStatus.Applied, replaced.Store.RecoverExpiredAsRetry(App, ParentKey, replaced.ParentId,
            replaced.Attempt, replaced.Token, Start.AddSeconds(10), TimeSpan.Zero).Status);
        var newAttempt = Guid.NewGuid(); var newToken = Guid.NewGuid();
        Assert.Equal(AttemptTransitionStatus.Applied, replaced.Store.Claim(App, ParentKey, replaced.ParentId,
            newAttempt, newToken, TimeSpan.FromSeconds(10)).Status);
        Assert.Equal(OwnedInvokeStatus.ParentUnavailable, replaced.Admit(replaced.Child(), id).Status);
        var later = replaced.Child(attempt: newAttempt.ToString("D"));
        Assert.Equal(OwnedInvokeStatus.Conflict, replaced.Store.AcceptOwnedInvoke(later, id, "impl", ParentKey,
            replaced.ParentId, newAttempt, newToken).Status);
        var freshKey = replaced.Child(key: "next-child", attempt: newAttempt.ToString("D"));
        Assert.Equal(OwnedInvokeStatus.Created, replaced.Store.AcceptOwnedInvoke(freshKey, id, "impl", ParentKey,
            replaced.ParentId, newAttempt, newToken).Status);
        var wrongAttempt = replaced.Store.AcceptOwnedInvoke(replaced.Child(), Guid.NewGuid(), "impl", ParentKey,
            replaced.ParentId, replaced.Attempt, replaced.Token);
        Assert.Equal(OwnedInvokeStatus.ParentUnavailable, wrongAttempt.Status);
        Assert.Equal(SubmissionProbeKind.Conflict, replaced.Store.ProbeTargeted(later).Kind);
        Assert.Equal(oldChildId, replaced.Store.ProbeTargeted(replaced.Child()).Record!.InvocationId);
        Assert.Equal(id, replaced.Store.ProbeTargeted(freshKey).Record!.InvocationId);
    }

    [Fact]
    public async Task ContendingChildrenAndParentCompletionHaveOneCoherentOutcome()
    {
        var f = new Fixture();
        const int contenders = 8;
        using var barrier = new Barrier(contenders + 1);
        var children = Enumerable.Range(0, contenders).Select(i => Task.Factory.StartNew(() =>
        {
            if (!barrier.SignalAndWait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Owned Invoke race barrier timed out.");
            return f.Admit(f.Child(), Guid.NewGuid(), $"choice-{i}");
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
        var finish = Task.Factory.StartNew(() =>
        {
            if (!barrier.SignalAndWait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Owned Invoke race barrier timed out.");
            return f.Store.Complete(App, ParentKey, f.ParentId, f.Attempt, f.Token, new TargetedCompletion([], [1]));
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        await Task.WhenAll(children.Cast<Task>().Append(finish)).WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(AttemptTransitionStatus.Applied, (await finish).Status);
        var results = await Task.WhenAll(children);
        var created = results.Where(item => item.Status == OwnedInvokeStatus.Created).ToArray();
        Assert.True(created.Length <= 1);
        Assert.All(results, result => Assert.True(result.Status is OwnedInvokeStatus.Created or OwnedInvokeStatus.Existing or OwnedInvokeStatus.ParentUnavailable));
        var probe = f.Store.ProbeTargeted(f.Child());
        if (created.Length == 0)
        {
            Assert.All(results, item => Assert.Equal(OwnedInvokeStatus.ParentUnavailable, item.Status));
            Assert.Equal(SubmissionProbeKind.Absent, probe.Kind);
        }
        else
        {
            var winner = created[0].Record!;
            Assert.Equal(SubmissionProbeKind.Existing, probe.Kind);
            Assert.Equal(winner.InvocationId, probe.Record!.InvocationId);
            Assert.Equal(winner.SelectedImplementation, probe.Record.SelectedImplementation);
            foreach (var item in results.Where(item => item.Status == OwnedInvokeStatus.Existing))
            {
                Assert.Equal(winner.InvocationId, item.Record!.InvocationId);
                Assert.Equal(winner.SelectedImplementation, item.Record.SelectedImplementation);
            }
            Assert.All(results.Where(item => item.Status == OwnedInvokeStatus.ParentUnavailable), item => Assert.Null(item.Record));
        }
    }
}
