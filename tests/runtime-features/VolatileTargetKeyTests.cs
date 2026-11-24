using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class VolatileTargetKeyTests
{
    private const string App = "routing-key-app";
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static TargetedRequest Request(string submission, byte[]? key, string target = "actor", byte[]? payload = null) =>
        new(App, submission, TargetedOperation.Dispatch, target, payload ?? [1, 2], [3], [4],
            parentInvocationId: null, parentAttemptId: null, publicTargetKey: key);

    [Fact]
    public void NullEmptyAndDifferentKeysAreDistinctAndCopiesAreRetained()
    {
        IRuntimeStore store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        byte[] source = [5, 6];
        var original = Request("keyed", source);
        source[0] = 99;
        original.PublicTargetKey![1] = 88;
        var id = Guid.NewGuid();
        var first = store.AcceptUnvalidated(original, id, "selected-v1");
        Assert.Equal(TargetedAcceptKind.Created, first.Kind);
        Assert.Equal(new byte[] { 5, 6 }, first.Record!.Request.PublicTargetKey);
        first.Record.Request.PublicTargetKey![0] = 77;
        var retry = store.AcceptUnvalidated(Request("keyed", [5, 6]), Guid.Empty, null);
        Assert.Equal(TargetedAcceptKind.Existing, retry.Kind);
        Assert.Equal(id, retry.Record!.InvocationId);
        Assert.Equal("selected-v1", retry.Record.SelectedImplementation);
        Assert.Equal(new byte[] { 5, 6 }, retry.Record.Request.PublicTargetKey);
        foreach (var changed in new[]
        {
            Request("keyed", null), Request("keyed", []), Request("keyed", [5, 7]),
            Request("keyed", [5, 6], target: "other"), Request("keyed", [5, 6], payload: [1, 9])
        })
        {
            var conflict = store.AcceptUnvalidated(changed, Guid.NewGuid(), "other");
            Assert.Equal(TargetedAcceptKind.Conflict, conflict.Kind);
            Assert.Null(conflict.Record);
        }
        Assert.Equal(new byte[] { 5, 6 }, store.ProbeTargeted(Request("keyed", [5, 6])).Record!.Request.PublicTargetKey);
        var publication = new PublicationRequest(App, "keyed", [1, 2], [3], [4]);
        Assert.Equal(SubmissionProbeKind.Conflict, store.ProbePublication(publication).Kind);

        Assert.Equal(TargetedAcceptKind.Created, store.AcceptUnvalidated(Request("null", null), Guid.NewGuid(), "selected").Kind);
        Assert.Equal(SubmissionProbeKind.Conflict, store.ProbeTargeted(Request("null", [])).Kind);
        Assert.Null(store.ProbeTargeted(Request("null", null)).Record!.Request.PublicTargetKey);
        Assert.Equal(TargetedAcceptKind.Created, store.AcceptUnvalidated(Request("empty", []), Guid.NewGuid(), "selected").Kind);
        Assert.Equal(SubmissionProbeKind.Conflict, store.ProbeTargeted(Request("empty", null)).Kind);
        Assert.Empty(store.ProbeTargeted(Request("empty", [])).Record!.Request.PublicTargetKey!);
    }

    [Fact]
    public void OwnedInvokeAndFreshRetryKeepOriginalOpaqueTargetKey()
    {
        var concrete = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        concrete.InitializeClock(Start);
        IRuntimeStore store = concrete;
        var parentId = Guid.NewGuid(); var parentAttempt = Guid.NewGuid(); var parentToken = Guid.NewGuid();
        var parent = Request("parent", [10]);
        Assert.Equal(TargetedAcceptKind.Created, store.AcceptUnvalidated(parent, parentId, "parent-impl").Kind);
        Assert.Equal(AttemptTransitionStatus.Applied,
            store.Claim(App, "parent", parentId, parentAttempt, parentToken, TimeSpan.FromSeconds(10)).Status);
        byte[] childKey = [20, 21];
        var child = new TargetedRequest(App, "child", TargetedOperation.Invoke, "child-target", [11], [12], [13],
            parentId.ToString("D"), parentAttempt.ToString("D"), publicTargetKey: childKey);
        childKey[0] = 99;
        var childId = Guid.NewGuid();
        var accepted = store.AcceptOwnedInvoke(child, childId, "pinned-child", "parent", parentId, parentAttempt, parentToken);
        Assert.Equal(OwnedInvokeStatus.Created, accepted.Status);
        Assert.Equal(new byte[] { 20, 21 }, accepted.Record!.Request.PublicTargetKey);
        var matching = store.AcceptOwnedInvoke(child, Guid.Empty, null, "parent", parentId, parentAttempt, parentToken);
        Assert.Equal(OwnedInvokeStatus.Existing, matching.Status);
        Assert.Equal(childId, matching.Record!.InvocationId);
        Assert.Equal("pinned-child", matching.Record.SelectedImplementation);
        var changed = new TargetedRequest(App, "child", TargetedOperation.Invoke, "child-target", [11], [12], [13],
            parentId.ToString("D"), parentAttempt.ToString("D"), publicTargetKey: [20, 22]);
        Assert.Equal(OwnedInvokeStatus.Conflict,
            store.AcceptOwnedInvoke(changed, Guid.NewGuid(), "other", "parent", parentId, parentAttempt, parentToken).Status);
        var childAttempt = Guid.NewGuid(); var childToken = Guid.NewGuid();
        var claimed = store.Claim(App, "child", childId, childAttempt, childToken, TimeSpan.FromSeconds(10));
        Assert.Equal(AttemptTransitionStatus.Applied, claimed.Status);
        Assert.Equal(new byte[] { 20, 21 }, claimed.Ownership!.Invocation.Request.PublicTargetKey);
        claimed.Ownership.Invocation.Request.PublicTargetKey![1] = 77;
        var retry = store.RescheduleAfterFault(App, "child", childId, childAttempt, childToken,
            TimeSpan.FromSeconds(2), new TargetedFault([1], [2]));
        Assert.Equal(AttemptTransitionStatus.Applied, retry.Status);
        Assert.Equal(new byte[] { 20, 21 }, retry.Ownership!.Invocation.Request.PublicTargetKey);
        concrete.AdvanceClock(Start.AddSeconds(2));
        var fresh = store.Claim(App, "child", childId, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromSeconds(10));
        Assert.Equal(AttemptTransitionStatus.Applied, fresh.Status);
        Assert.Equal(childId, fresh.Ownership!.Invocation.InvocationId);
        Assert.Equal("pinned-child", fresh.Ownership.Invocation.SelectedImplementation);
        Assert.Equal(new byte[] { 20, 21 }, fresh.Ownership.Invocation.Request.PublicTargetKey);
        Assert.Equal(new byte[] { 10 }, store.GetOwnership(App, "parent", parentId)!.Invocation.Request.PublicTargetKey);
    }
}
