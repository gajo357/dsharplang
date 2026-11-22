using System.Reflection;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class ActorDispatcherContractTests
{
    [Fact]
    public void TypedRouteAndMessageKeysAreImmutableNonemptyOpaqueValues()
    {
        string?[] invalid = [null, "", " ", "\t\r\n"];
        foreach (var key in invalid)
        {
            Assert.ThrowsAny<ArgumentException>(() => new ActorRoute<Request, int>(key!));
            Assert.ThrowsAny<ArgumentException>(() => new MessageContract<Request>(key!));
        }

        var route = new ActorRoute<Request, int>("  host/target  ");
        var contract = new MessageContract<Request>("  host/message  ");
        Assert.Equal("  host/target  ", route.Key);
        Assert.Equal("  host/message  ", contract.Key);
        Assert.Null(typeof(ActorRoute<Request, int>).GetProperty(nameof(route.Key))!.GetSetMethod());
        Assert.Null(typeof(MessageContract<Request>).GetProperty(nameof(contract.Key))!.GetSetMethod());
        Assert.NotEqual(typeof(InvocationReceipt), typeof(PublicationReceipt));
        Assert.Empty(typeof(InvocationReceipt).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Empty(typeof(PublicationReceipt).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.NotNull(new InvocationReceipt());
        Assert.NotNull(new PublicationReceipt());
    }

    [Fact]
    public async Task TargetedOperationsForwardTheSameRouteAndCompleteIndependently()
    {
        var dispatcher = new RecordingDispatcher();
        IActorDispatcher api = dispatcher;
        var route = new ActorRoute<Request, int>("target");
        var message = new Request(42);
        var invocation = api.Invoke(route, message);
        var acceptance = api.Dispatch(route, message);
        var invoked = Assert.Single(dispatcher.Invocations);
        var dispatched = Assert.Single(dispatcher.Dispatches);
        Assert.Same(route, invoked.Route);
        Assert.Same(route, dispatched.Route);
        Assert.Same(message, invoked.Message);
        Assert.Same(message, dispatched.Message);
        Assert.Empty(dispatcher.Publications);
        Assert.False(invocation.IsCompleted);
        Assert.False(acceptance.IsCompleted);

        Assert.IsType<TaskCompletionSource<int>>(invoked.Completion).SetResult(42);
        Assert.Equal(42, await invocation);
        Assert.False(acceptance.IsCompleted);
        var receipt = new InvocationReceipt();
        dispatched.Completion.SetResult(receipt);
        Assert.Same(receipt, await acceptance);

        var reverse = new RecordingDispatcher();
        var pendingResult = reverse.Invoke(route, message);
        var pendingReceipt = reverse.Dispatch(route, message);
        var reverseReceipt = new InvocationReceipt();
        Assert.Single(reverse.Dispatches).Completion.SetResult(reverseReceipt);
        Assert.Same(reverseReceipt, await pendingReceipt);
        Assert.False(pendingResult.IsCompleted);
        Assert.IsType<TaskCompletionSource<int>>(Assert.Single(reverse.Invocations).Completion).SetResult(7);
        Assert.Equal(7, await pendingResult);
    }

    [Fact]
    public async Task TypedUnitLikeAndBusinessResultsRemainExactValues()
    {
        var dispatcher = new RecordingDispatcher();
        var unitRoute = new ActorRoute<Request, UnitLike>("unit");
        var businessRoute = new ActorRoute<Request, BusinessResult>("business");
        var message = new Request(1);
        var unitTask = dispatcher.Invoke(unitRoute, message);
        var businessTask = dispatcher.Invoke(businessRoute, message);
        Assert.Equal(2, dispatcher.Invocations.Count);
        Assert.Same(unitRoute, dispatcher.Invocations[0].Route);
        Assert.Same(businessRoute, dispatcher.Invocations[1].Route);
        Assert.Same(message, dispatcher.Invocations[0].Message);
        Assert.Same(message, dispatcher.Invocations[1].Message);
        var failureValue = new BusinessResult(false, "declined");
        Assert.IsType<TaskCompletionSource<BusinessResult>>(dispatcher.Invocations[1].Completion).SetResult(failureValue);
        Assert.Same(failureValue, await businessTask);
        Assert.False(unitTask.IsCompleted);
        Assert.IsType<TaskCompletionSource<UnitLike>>(dispatcher.Invocations[0].Completion).SetResult(new UnitLike());
        Assert.Equal(new UnitLike(), await unitTask);
    }

    [Fact]
    public async Task PublicationAndFailuresRemainIndependentAcrossDispatcherInstances()
    {
        var first = new RecordingDispatcher();
        var second = new RecordingDispatcher();
        var route = new ActorRoute<Request, int>("same-target");
        var contract = new MessageContract<Request>("same-message");
        var message = new Request(7);

        var zeroSubscribers = first.Publish(contract, message);
        var firstPublication = Assert.Single(first.Publications);
        Assert.Same(contract, firstPublication.Contract);
        Assert.Same(message, firstPublication.Message);
        Assert.Empty(firstPublication.SimulatedSubscribers);
        first.AddSimulatedSubscriber(contract, "first");
        first.AddSimulatedSubscriber(contract, "second");
        var twoSubscribers = first.Publish(contract, message);
        Assert.Equal(new[] { "first", "second" }, first.Publications[1].SimulatedSubscribers);
        Assert.Empty(firstPublication.SimulatedSubscribers);
        Assert.Empty(first.Invocations);
        Assert.Empty(first.Dispatches);
        Assert.False(zeroSubscribers.IsCompleted);
        Assert.False(twoSubscribers.IsCompleted);
        var publicationReceipt = new PublicationReceipt();
        first.Publications[1].Completion.SetResult(publicationReceipt);
        Assert.Same(publicationReceipt, await twoSubscribers);
        Assert.False(zeroSubscribers.IsCompleted);
        var emptyReceipt = new PublicationReceipt();
        firstPublication.Completion.SetResult(emptyReceipt);
        Assert.Same(emptyReceipt, await zeroSubscribers);
        var faultPublication = first.Publish(contract, message);
        var faultPublicationRecord = first.Publications[2];

        var firstInvoke = first.Invoke(route, message);
        var secondInvoke = second.Invoke(route, message);
        var secondDispatch = second.Dispatch(route, message);
        var secondPublication = second.Publish(contract, message);
        Assert.Single(first.Invocations);
        Assert.Single(second.Invocations);
        Assert.Empty(second.Publications[0].SimulatedSubscribers);
        Assert.Same(route, first.Invocations[0].Route);
        Assert.Same(route, second.Invocations[0].Route);
        Assert.Same(message, second.Invocations[0].Message);
        Assert.IsType<TaskCompletionSource<int>>(second.Invocations[0].Completion).SetResult(99);
        Assert.Equal(99, await secondInvoke);
        Assert.False(firstInvoke.IsCompleted);
        Assert.False(secondDispatch.IsCompleted);
        Assert.False(secondPublication.IsCompleted);

        var invokeFault = new InvalidOperationException("invoke failed");
        var dispatchFault = new InvalidOperationException("dispatch failed");
        var publishFault = new InvalidOperationException("publish failed");
        Assert.IsType<TaskCompletionSource<int>>(first.Invocations[0].Completion).SetException(invokeFault);
        second.Dispatches[0].Completion.SetException(dispatchFault);
        faultPublicationRecord.Completion.SetException(publishFault);
        Assert.Same(invokeFault, await Assert.ThrowsAsync<InvalidOperationException>(async () => await firstInvoke));
        Assert.Same(dispatchFault, await Assert.ThrowsAsync<InvalidOperationException>(async () => await secondDispatch));
        Assert.Same(publishFault, await Assert.ThrowsAsync<InvalidOperationException>(async () => await faultPublication));
        Assert.False(secondPublication.IsCompleted);
        var secondReceipt = new PublicationReceipt();
        second.Publications[0].Completion.SetResult(secondReceipt);
        Assert.Same(secondReceipt, await secondPublication);
    }

    private sealed record Request(int Value);
    private readonly record struct UnitLike;
    private sealed record BusinessResult(bool Success, string? Error);

    // A deterministic test fake records calls and lets each test finish its own
    // tasks. Its subscriber list and receipts do not implement runtime delivery
    // or prove durable acceptance.
    private sealed class RecordingDispatcher : IActorDispatcher
    {
        private readonly Dictionary<object, List<string>> subscribers = new(ReferenceEqualityComparer.Instance);
        public List<Invocation> Invocations { get; } = [];
        public List<TargetedDispatch> Dispatches { get; } = [];
        public List<Publication> Publications { get; } = [];

        public Task<TResult> Invoke<TMessage, TResult>(ActorRoute<TMessage, TResult> route, TMessage message)
        {
            var completion = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            Invocations.Add(new Invocation(route, message, completion));
            return completion.Task;
        }

        public Task<InvocationReceipt> Dispatch<TMessage, TResult>(ActorRoute<TMessage, TResult> route, TMessage message)
        {
            var completion = new TaskCompletionSource<InvocationReceipt>(TaskCreationOptions.RunContinuationsAsynchronously);
            Dispatches.Add(new TargetedDispatch(route, message, completion));
            return completion.Task;
        }

        public Task<PublicationReceipt> Publish<TMessage>(MessageContract<TMessage> contract, TMessage message)
        {
            var completion = new TaskCompletionSource<PublicationReceipt>(TaskCreationOptions.RunContinuationsAsynchronously);
            var selected = subscribers.TryGetValue(contract, out var values) ? values.ToArray() : Array.Empty<string>();
            Publications.Add(new Publication(contract, message, selected, completion));
            return completion.Task;
        }

        public void AddSimulatedSubscriber<TMessage>(MessageContract<TMessage> contract, string name)
        {
            if (!subscribers.TryGetValue(contract, out var list)) subscribers[contract] = list = [];
            list.Add(name);
        }
    }

    private sealed record Invocation(object Route, object? Message, object Completion);
    private sealed record TargetedDispatch(object Route, object? Message, TaskCompletionSource<InvocationReceipt> Completion);
    private sealed record Publication(object Contract, object? Message, IReadOnlyList<string> SimulatedSubscribers,
        TaskCompletionSource<PublicationReceipt> Completion);
}
