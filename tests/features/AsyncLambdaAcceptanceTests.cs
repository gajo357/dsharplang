using System.Diagnostics;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class AsyncLambdaAcceptanceTests
{
    [Fact(DisplayName = "Async lambdas")]
    public async Task LambdasSuspendRetainCapturesAndEnforceBoundaries()
    {
        // A host-controlled TaskCompletionSource proves real suspension, rather
        // than only checking that completed tasks happened to return a value.
        const string source = """
            module Demo
            fn adder(offset: int): (Task<int>) -> Task<int> {
                async input => {
                    let value = await input
                    value + offset
                }
            }
            fn sum(a: int, b: int): int { a + b }
            fn twoArguments(): (int, int) -> Task<int> {
                async (a: int, b: int) => sum(a, b)
            }
            fn runner(): ((int) -> Unit, Task<int>) -> Task<int> {
                async (mark, input) => {
                    mark(1)
                    let value = await input
                    mark(2)
                    value
                }
            }
            fn builder(): (Task<int>) -> Task<int> {
                async input => {
                    let values = MutableList<int>()
                    values.add(1)
                    let result = await input
                    values.add(result)
                    match values.freeze().get(1) { Some(value) => value
                                                    None => 0 }
                }
            }
            fn fails(): (Task<int>) -> Task<int> {
                async input => {
                    let value = await input
                    value / 0
                }
            }
            fn immediateFailure(): () -> Task<int> { async () => 1 / 0 }
            fn apply<T, U>(value: T, callback: (T) -> Task<U>): Task<U> {
                callback(value)
            }
            async fn genericAnswer(): int {
                await apply(41, async (value: int) => value + 1)
            }
            async fn inferredAnswer(): int {
                let plus = async (value: int) => value + 1
                await plus(41)
            }
            async fn nullableAnswer(): bool {
                let identity: (int?) -> Task<int?> = async value => value
                let empty: int? = await identity(null)
                let present: int? = await identity(42)
                (empty == null) && ((present ?? 0) == 42)
            }
            fn captureNullable<T>(value: T?): () -> Task<T?> {
                async () => value
            }
            async fn genericNullableAnswer(): bool {
                let first = captureNullable<int>(null)
                let second = captureNullable<int?>(null)
                let third = captureNullable<string>(null)
                let fourth = captureNullable<string?>(null)
                let a: int? = await first()
                let b: int? = await second()
                let c: string? = await third()
                let d: string? = await fourth()
                (a == null) && (b == null) && (c == null) && (d == null)
            }
            async fn check(value: int): Result<int, string> {
                if value > 0 { Success(value) } else { Error("negative") }
            }
            async fn resultBoundary(value: int): int {
                let callback: (int) -> Task<Result<int, string>> = async number => {
                    let checked = (await check(number))?
                    Success(checked + 1)
                }
                let result = await callback(value)
                match result { Success(number) => number
                               Error(_) => 42 }
            }
            async fn returnBoundary(): int {
                let callback: () -> Task<int> = async () => { return 40 }
                let value = await callback()
                value + 2
            }
            async fn delayedResult(input: Task<int>): int {
                let callback: (Task<int>) -> Task<Result<int, string>> = async pending => {
                    let value = await pending
                    let accepted = (await check(value))?
                    Success(accepted)
                }
                let result = await callback(input)
                match result { Success(value) => value
                               Error(_) => 42 }
            }
            async fn delayedReturn(input: Task<int>): int {
                let callback: (Task<int>) -> Task<int> = async pending => {
                    let value = await pending
                    if value > 0 { return value }
                    0
                }
                let result = await callback(input)
                result + 2
            }
            async fn nested(): int {
                let outer: (int) -> Task<int> = async x => {
                    let plus: (int) -> int = y => y + 1
                    let inner: (int) -> Task<int> = async y => x + y
                    await inner(plus(x) + 1)
                }
                await outer(20)
            }
            async fn nestedBuilder(input: Task<int>): int {
                let outer: (Task<int>) -> Task<int> = async task => {
                    let inner: (Task<int>) -> Task<int> = async pending => {
                        let values = MutableList<int>()
                        values.add(1)
                        let value = await pending
                        values.add(value)
                        match values.freeze().get(1) { Some(item) => item
                                                        None => 0 }
                    }
                    await inner(task)
                }
                await outer(input)
            }
            fn nestedTask(): (Task<int>) -> Task<Task<int>> {
                async input => input
            }
            """;

        await WithGenerated(source, async type =>
        {
            var first = (Delegate)Call(type, "adder", 1)!;
            var second = (Delegate)Call(type, "adder", 2)!;
            var firstGate = Gate<int>();
            var secondGate = Gate<int>();
            var firstResult = (Task<int>)first.DynamicInvoke(firstGate.Task)!;
            var secondResult = (Task<int>)second.DynamicInvoke(secondGate.Task)!;
            Assert.False(firstResult.IsCompleted);
            Assert.False(secondResult.IsCompleted);
            secondGate.SetResult(40);
            Assert.Equal(42, await secondResult.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.False(firstResult.IsCompleted);
            firstGate.SetResult(41);
            Assert.Equal(42, await firstResult.WaitAsync(TimeSpan.FromSeconds(10)));

            var sum = (Delegate)Call(type, "twoArguments")!;
            Assert.Equal(42, await ((Task<int>)sum.DynamicInvoke(19, 23)!).WaitAsync(TimeSpan.FromSeconds(10)));

            var runner = (Delegate)Call(type, "runner")!;
            var events = new ConcurrentQueue<int>();
            var markType = runner.GetType().GetMethod("Invoke")!.GetParameters()[0].ParameterType;
            var mark = Adapt(markType, (Action<int>)events.Enqueue);
            Assert.Empty(events); // Forming a closure does not run its body.
            var gate = Gate<int>();
            var running = (Task<int>)runner.DynamicInvoke(mark, gate.Task)!;
            Assert.Equal(new[] { 1 }, events.ToArray());
            Assert.False(running.IsCompleted);
            gate.SetResult(42);
            Assert.Equal(42, await running.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(new[] { 1, 2 }, events.ToArray());

            var builder = (Delegate)Call(type, "builder")!;
            var buildGate = Gate<int>();
            var built = (Task<int>)builder.DynamicInvoke(buildGate.Task)!;
            Assert.False(built.IsCompleted);
            buildGate.SetResult(42);
            Assert.Equal(42, await built.WaitAsync(TimeSpan.FromSeconds(10)));

            var failing = (Delegate)Call(type, "fails")!;
            var failureGate = Gate<int>();
            var failure = (Task<int>)failing.DynamicInvoke(failureGate.Task)!;
            Assert.False(failure.IsCompleted);
            failureGate.SetResult(1);
            await Assert.ThrowsAsync<DivideByZeroException>(async () => await failure);
            var immediate = (Delegate)Call(type, "immediateFailure")!;
            await Assert.ThrowsAsync<DivideByZeroException>(async () => await (Task<int>)immediate.DynamicInvoke()!);
            var cancelledGate = Gate<int>();
            var cancelled = (Task<int>)first.DynamicInvoke(cancelledGate.Task)!;
            cancelledGate.SetCanceled();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await cancelled);
            Assert.True(cancelled.IsCanceled);
            var faultGate = Gate<int>();
            var faulted = (Task<int>)first.DynamicInvoke(faultGate.Task)!;
            faultGate.SetException(new InvalidOperationException("controlled failure"));
            var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await faulted);
            Assert.Equal("controlled failure", error.Message);

            Assert.Equal(42, await AsyncCall<int>(type, "genericAnswer"));
            Assert.Equal(42, await AsyncCall<int>(type, "inferredAnswer"));
            Assert.True(await AsyncCall<bool>(type, "nullableAnswer"));
            Assert.True(await AsyncCall<bool>(type, "genericNullableAnswer"));
            Assert.Equal(42, await AsyncCall<int>(type, "resultBoundary", -1));
            Assert.Equal(42, await AsyncCall<int>(type, "resultBoundary", 41));
            Assert.Equal(42, await AsyncCall<int>(type, "returnBoundary"));
            var resultGate = Gate<int>();
            var delayedResult = AsyncCall<int>(type, "delayedResult", resultGate.Task);
            Assert.False(delayedResult.IsCompleted);
            resultGate.SetResult(-1);
            Assert.Equal(42, await delayedResult.WaitAsync(TimeSpan.FromSeconds(10)));
            var returnGate = Gate<int>();
            var delayedReturn = AsyncCall<int>(type, "delayedReturn", returnGate.Task);
            Assert.False(delayedReturn.IsCompleted);
            returnGate.SetResult(40);
            Assert.Equal(42, await delayedReturn.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(42, await AsyncCall<int>(type, "nested"));
            var nestedGate = Gate<int>();
            var nestedBuilder = AsyncCall<int>(type, "nestedBuilder", nestedGate.Task);
            Assert.False(nestedBuilder.IsCompleted);
            nestedGate.SetResult(42);
            Assert.Equal(42, await nestedBuilder.WaitAsync(TimeSpan.FromSeconds(10)));

            var inner = Gate<int>();
            var nesting = (Delegate)Call(type, "nestedTask")!;
            var result = (Task<Task<int>>)nesting.DynamicInvoke(inner.Task)!;
            Assert.Same(inner.Task, await result.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.False(inner.Task.IsCompleted);
            inner.SetResult(42);
            Assert.Equal(42, await inner.Task);
        });

        var invalid = new[]
        {
            // Await remains forbidden inside a nested synchronous lambda.
            "async fn f(input: Task<int>): int { let nested: (Task<int>) -> int = x => await x\n nested(input) }",
            "fn f(): Unit { let callback: (int) -> Task<int> = async x => x\n let value: int = callback(1)\n () }",
            "async fn work(value: int): int { value }\nfn f(): Unit { let callback: (int) -> Task<int> = async x => work(x)\n () }",
            "fn f(): Unit { let b = MutableList<int>()\n let callback: () -> Task<Unit> = async () => b.add(1)\n () }",
            "fn f(): Unit { let b = MutableList<int>()\n (async () => b.add(1))()\n () }",
            // Immutable list values cannot contain Task in this compiler slice;
            // ordinary map does not implicitly await an async callback.
            "fn f(): Unit { let values = [1, 2]\n let tasks = values.map(async (x: int) => x)\n () }",
            "fn f(): Unit { let callback: (int) -> int = async x => x\n () }",
            "fn f(): Unit { let callback = async x => x\n () }"
        };
        foreach (var body in invalid)
        {
            var result = DSharpCompiler.Compile("module Demo\n" + body, "async-lambda-invalid.ds");
            Assert.True(result.CSharp is null, $"Invalid async lambda compiled: {body}");
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, diagnostic => Assert.Equal("async-lambda-invalid.ds", diagnostic.Path));
            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code.StartsWith("DS1", StringComparison.Ordinal));
        }
    }

    private static TaskCompletionSource<T> Gate<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static MethodInfo Method(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
    private static object? Call(Type type, string name, params object?[] args) => Method(type, name).Invoke(null, args);
    private static Task<T> AsyncCall<T>(Type type, string name, params object?[] args) => (Task<T>)Call(type, name, args)!;

    private static Delegate Adapt(Type type, Action<int> action)
    {
        var method = type.GetMethod("Invoke")!;
        var value = Expression.Parameter(method.GetParameters()[0].ParameterType, "value");
        var body = Expression.Block(Expression.Invoke(Expression.Constant(action), value), Expression.Default(method.ReturnType));
        return Expression.Lambda(type, body, value).Compile();
    }

    private static async Task WithGenerated(string source, Func<Type, Task> verify)
    {
        var compilation = DSharpCompiler.Compile(source, "async-lambda.ds");
        Assert.Empty(compilation.Diagnostics);
        Assert.NotNull(compilation.CSharp);
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-async-lambda-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "Generated.cs"), compilation.CSharp);
            File.WriteAllText(Path.Combine(directory, "Generated.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net11.0</TargetFramework><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>");
            File.WriteAllText(Path.Combine(directory, "global.json"), "{\"sdk\":{\"version\":\"11.0.100\",\"rollForward\":\"disable\"}}");
            File.WriteAllText(Path.Combine(directory, "NuGet.Config"), "<configuration><packageSources><clear/></packageSources></configuration>");
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "build", "Generated.csproj", "--nologo", "--verbosity", "quiet" }) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated async lambda build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("async-lambda-" + Guid.NewGuid(), isCollectible: true);
            try
            {
                var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0/Generated.dll"));
                await verify(assembly.GetType("DSharp.Generated.Demo")!);
            }
            finally { context.Unload(); }
        }
        finally { Directory.Delete(directory, true); }
    }
}
