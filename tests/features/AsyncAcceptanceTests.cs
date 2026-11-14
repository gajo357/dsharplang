using System.Diagnostics;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class AsyncAcceptanceTests
{
    [Fact(DisplayName = "Async functions")]
    public async Task FunctionsSuspendAndPreserveDSharpSemantics()
    {
        // Host-controlled incomplete tasks make suspension and sequencing observable.
        const string source = """
            module Demo
            async fn increment(input: Task<int>): int {
                let value = await input
                value + 1
            }
            async fn awaitOnce(factory: () -> Task<int>): int { await factory() }
            fn launch(input: Task<int>): Task<int> { increment(input) }
            fn sameTask(input: Task<int>): Task<int> { input }
            fn pipeTask(input: Task<int>): Task<int> { input |> sameTask }
            fn useTask(input: Task<int>): int { 42 }
            fn pipeImmediate(input: Task<int>): int { input |> useTask }
            async fn immediate(): int { 42 }
            async fn empty(): Unit { () }
            async fn raise(): int { 1 / 0 }
            async fn identity<T>(value: T): T { value }
            async fn genericAnswer(): int { await identity<int>(42) }
            async fn genericFunction(): int {
                let original: () -> int = () => 42
                let function = await identity<() -> int>(original)
                function()
            }
            async fn maybe<T>(value: T?): T? { value }
            async fn genericNullable(): bool {
                let a: int? = await maybe<int>(null)
                let b: int? = await maybe<int?>(null)
                let c: string? = await maybe<string>(null)
                let d: string? = await maybe<string?>(null)
                let value: int? = await maybe<int>(42)
                (a == null) && (b == null) && (c == null) && (d == null) &&
                ((value ?? 0) == 42)
            }
            async fn forwardTask(input: Task<int>): int {
                let forwarded = await identity<Task<int>>(input)
                await forwarded
            }
            async fn add(left: int, right: int): int { left + right }
            fn functionRef(): (int, int) -> Task<int> { add }
            async fn partialAnswer(): int {
                let f: (int) -> Task<int> = add(40)
                await f(2)
            }
            async fn referenceAnswer(): int {
                let f = functionRef()
                await f(20, 22)
            }
            fn sum(left: int, right: int): int { left + right }
            async fn beforeAwait(mark: (int) -> int, input: Task<int>): int {
                sum(mark(1), await input)
            }
            async fn afterAwait(mark: (int) -> int, input: Task<int>): int {
                sum(await input, mark(2))
            }
            async fn nullable(input: Task<string?>): string? { await input }
            async fn nested(input: Task<Task<int>>): Task<int> { await input }
            async fn switchValue(input: Task<int>): int {
                if (await input) > 0 { return 42 } else { 0 }
            }
            async fn unreachableAwait(input: Task<int>): int {
                if true {
                    return 42
                    await input
                } else { 0 }
            }
            async fn pair(first: Task<int>, second: Task<int>, mark: (int) -> Unit): int {
                let a = await first
                mark(a)
                let b = await second
                mark(b)
                a + b
            }
            async fn listValues(first: Task<int>, second: Task<int>): List<int> {
                [await first, await second]
            }
            async fn listAnswer(first: Task<int>, second: Task<int>): int {
                let values = await listValues(first, second)
                match values.get(1) { None => 0
                                      Some(value) => value }
            }
            async fn loop(next: (int) -> Task<int>, mark: (int) -> Unit): int {
                let values = MutableList<int>()
                for value in [1, 2, 3] {
                    let observed = await next(value)
                    values.add(observed)
                    mark(value)
                }
                match values.freeze().get(2) { None => 0
                                               Some(value) => value }
            }
            async fn build(input: Task<int>): List<int> {
                let builder = MutableList<int>()
                builder.add(1)
                let value = await input
                builder.add(value)
                builder.freeze()
            }
            async fn buildAnswer(input: Task<int>): int {
                let items = await build(input)
                match items.get(1) { None => 0
                                     Some(value) => value }
            }
            async fn checkedValue(value: int): Result<int, string> {
                if value > 0 { Success(value) } else { Error("bad") }
            }
            async fn process(value: int): Result<int, string> {
                let number = (await checkedValue(value))?
                Success(number + 1)
            }
            async fn processCode(value: int): int {
                match (await process(value)) { Success(number) => number
                                               Error(_) => -1 }
            }
            async fn optional(value: int): Option<int> {
                if value > 0 { Some(value) } else { None }
            }
            async fn matchAnswer(value: int): int {
                match (await optional(value)) { Some(number) => number
                                               None => 0 }
            }
            async fn makeFunction(): () -> int { () => 42 }
            async fn functionAnswer(): int {
                let f = await makeFunction()
                f()
            }
            """;

        await WithGenerated(source, async type =>
        {
            var first = Gate<int>();
            var pending = Call<Task<int>>(type, "launch", first.Task);
            Assert.False(pending.IsCompleted);
            first.SetResult(41);
            Assert.Equal(42, await pending.WaitAsync(TimeSpan.FromSeconds(10)));
            var pipedGate = Gate<int>();
            var pipedTask = Call<Task<int>>(type, "pipeTask", pipedGate.Task);
            Assert.Same(pipedGate.Task, pipedTask);
            Assert.False(pipedTask.IsCompleted);
            Assert.Equal(42, Call<int>(type, "pipeImmediate", pipedGate.Task));
            Assert.False(pipedGate.Task.IsCompleted);
            pipedGate.SetResult(42);
            Assert.Equal(42, await pipedTask.WaitAsync(TimeSpan.FromSeconds(10)));
            var factoryGate = Gate<int>();
            var factoryCalls = 0;
            Func<Task<int>> factory = () => { Interlocked.Increment(ref factoryCalls); return factoryGate.Task; };
            var awaitOnce = Method(type, "awaitOnce");
            var factoryDelegate = Adapt(awaitOnce.GetParameters()[0].ParameterType, factory);
            var once = (Task<int>)awaitOnce.Invoke(null, new object[] { factoryDelegate })!;
            Assert.Equal(1, Volatile.Read(ref factoryCalls));
            Assert.False(once.IsCompleted);
            factoryGate.SetResult(42);
            Assert.Equal(42, await once.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(1, Volatile.Read(ref factoryCalls));
            Assert.Equal(42, await Call<Task<int>>(type, "immediate"));
            Assert.Equal(42, await Call<Task<int>>(type, "genericAnswer"));
            Assert.Equal(42, await Call<Task<int>>(type, "genericFunction"));
            Assert.True(await Call<Task<bool>>(type, "genericNullable"));
            Assert.Equal(42, await Call<Task<int>>(type, "partialAnswer"));
            Assert.Equal(42, await Call<Task<int>>(type, "referenceAnswer"));
            var forwardGate = Gate<int>();
            var forward = Call<Task<int>>(type, "forwardTask", forwardGate.Task);
            Assert.False(forward.IsCompleted);
            forwardGate.SetResult(42);
            Assert.Equal(42, await forward.WaitAsync(TimeSpan.FromSeconds(10)));
            var ordered = new ConcurrentQueue<int>();
            Func<int, int> observe = value => { ordered.Enqueue(value); return 20; };
            var beforeMethod = Method(type, "beforeAwait");
            var orderedDelegate = Adapt(beforeMethod.GetParameters()[0].ParameterType, observe);
            var beforeGate = Gate<int>();
            var before = (Task<int>)beforeMethod.Invoke(null, new object[] { orderedDelegate, beforeGate.Task })!;
            Assert.Equal(new[] { 1 }, ordered.ToArray());
            Assert.False(before.IsCompleted);
            beforeGate.SetResult(22);
            Assert.Equal(42, await before.WaitAsync(TimeSpan.FromSeconds(10)));
            var afterMethod = Method(type, "afterAwait");
            var afterGate = Gate<int>();
            var after = (Task<int>)afterMethod.Invoke(null, new object[] { orderedDelegate, afterGate.Task })!;
            Assert.Equal(new[] { 1 }, ordered.ToArray());
            afterGate.SetResult(22);
            Assert.Equal(42, await after.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(new[] { 1, 2 }, ordered.ToArray());
            var never = Gate<int>();
            Assert.Equal(42, await Call<Task<int>>(type, "unreachableAwait", never.Task).WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.False(never.Task.IsCompleted);

            var unitTask = Call<Task>(type, "empty");
            await unitTask.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.NotNull(unitTask.GetType().GetProperty("Result")!.GetValue(unitTask));
            await Assert.ThrowsAsync<DivideByZeroException>(async () => await Call<Task<int>>(type, "raise"));

            var failed = Gate<int>();
            var faulted = Call<Task<int>>(type, "increment", failed.Task);
            failed.SetException(new InvalidOperationException("controlled failure"));
            var fault = await Assert.ThrowsAsync<InvalidOperationException>(async () => await faulted);
            Assert.Equal("controlled failure", fault.Message);
            var cancelled = Gate<int>();
            var cancelledResult = Call<Task<int>>(type, "increment", cancelled.Task);
            cancelled.SetCanceled();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await cancelledResult);
            Assert.True(cancelledResult.IsCanceled);

            var nullable = Gate<string?>();
            var nullResult = Call<Task<string?>>(type, "nullable", nullable.Task);
            Assert.False(nullResult.IsCompleted);
            nullable.SetResult(null);
            Assert.Null(await nullResult.WaitAsync(TimeSpan.FromSeconds(10)));

            var inner = Gate<int>();
            var outer = Gate<Task<int>>();
            var nested = Call<Task<Task<int>>>(type, "nested", outer.Task);
            Assert.False(nested.IsCompleted);
            outer.SetResult(inner.Task);
            Assert.Same(inner.Task, await nested.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.False(inner.Task.IsCompleted);
            inner.SetResult(42);
            Assert.Equal(42, await inner.Task);

            var branch = Gate<int>();
            var branchResult = Call<Task<int>>(type, "switchValue", branch.Task);
            Assert.False(branchResult.IsCompleted);
            branch.SetResult(1);
            Assert.Equal(42, await branchResult.WaitAsync(TimeSpan.FromSeconds(10)));

            var pairMethod = Method(type, "pair");
            var seen = new ConcurrentQueue<int>();
            var mark = Adapt(pairMethod.GetParameters()[2].ParameterType, (Action<int>)seen.Enqueue);
            var left = Gate<int>();
            var right = Gate<int>();
            var pair = (Task<int>)pairMethod.Invoke(null, new object[] { left.Task, right.Task, mark })!;
            Assert.Empty(seen);
            left.SetResult(20);
            await Eventually(() => seen.Count == 1);
            Assert.Equal(new[] { 20 }, seen.ToArray());
            Assert.False(pair.IsCompleted);
            right.SetResult(22);
            Assert.Equal(42, await pair.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(new[] { 20, 22 }, seen.ToArray());

            var listFirst = Gate<int>();
            var listSecond = Gate<int>();
            var list = Call<Task<int>>(type, "listAnswer", listFirst.Task, listSecond.Task);
            listSecond.SetResult(42);
            Assert.False(list.IsCompleted);
            listFirst.SetResult(1);
            Assert.Equal(42, await list.WaitAsync(TimeSpan.FromSeconds(10)));

            var loopMethod = Method(type, "loop");
            var requests = new ConcurrentQueue<int>();
            var visits = new ConcurrentQueue<int>();
            var gates = new[] { Gate<int>(), Gate<int>(), Gate<int>() };
            Func<int, Task<int>> next = value => { requests.Enqueue(value); return gates[value - 1].Task; };
            var nextDelegate = Adapt(loopMethod.GetParameters()[0].ParameterType, next);
            var visitDelegate = Adapt(loopMethod.GetParameters()[1].ParameterType, (Action<int>)visits.Enqueue);
            var loop = (Task<int>)loopMethod.Invoke(null, new object[] { nextDelegate, visitDelegate })!;
            Assert.Equal(new[] { 1 }, requests.ToArray());
            Assert.Empty(visits);
            gates[0].SetResult(10);
            await Eventually(() => requests.Count == 2);
            Assert.Equal(new[] { 1 }, visits.ToArray());
            gates[1].SetResult(20);
            await Eventually(() => requests.Count == 3);
            Assert.Equal(new[] { 1, 2 }, visits.ToArray());
            gates[2].SetResult(42);
            Assert.Equal(42, await loop.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(new[] { 1, 2, 3 }, visits.ToArray());

            var a = Gate<int>();
            var b = Gate<int>();
            var buildA = Call<Task<int>>(type, "buildAnswer", a.Task);
            var buildB = Call<Task<int>>(type, "buildAnswer", b.Task);
            b.SetResult(7);
            Assert.Equal(7, await buildB.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.False(buildA.IsCompleted);
            a.SetResult(42);
            Assert.Equal(42, await buildA.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(42, await Call<Task<int>>(type, "processCode", 41));
            Assert.Equal(-1, await Call<Task<int>>(type, "processCode", 0));
            Assert.Equal(42, await Call<Task<int>>(type, "matchAnswer", 42));
            Assert.Equal(0, await Call<Task<int>>(type, "matchAnswer", -1));
            Assert.Equal(42, await Call<Task<int>>(type, "functionAnswer"));
        });

        var invalid = new[]
        {
            "fn f(input: Task<int>): int { await input }",
            "async fn f(input: int): int { await input }",
            "fn f(input: Task<int>): int { input }",
            "async fn work(): int { 1 }\nfn f(): Unit { work()\n () }",
            "fn f(input: Task<int>): bool { input == input }",
            "fn f(input: Task<int>): Unit { let items = [input]\n () }",
            "fn f(input: Task<int>): Unit { let tuple = (input, 1)\n () }",
            "type Box { value: Task<int> }\nfn f(): Unit { () }",
            "type Box { | Wrap(Task<int>) }\nfn f(): Unit { () }",
            "fn box<T>(value: T): Option<T> { Some(value) }\nfn f(input: Task<int>): Unit { let hidden = box(input)\n () }",
            "fn box<T>(value: T): Option<T> { Some(value) }\nfn f(input: Task<int>): Unit { let hidden = box<Task<int>>(input)\n () }",
            "fn box<T>(value: T): Option<T> { Some(value) }\nfn forward<T>(value: T): Option<T> { box<T>(value) }\nfn f(input: Task<int>): Unit { let hidden = forward(input)\n () }",
            "fn box<T>(value: T, ignored: int): Option<T> { Some(value) }\nfn f(input: Task<int>): Unit { let hidden = box(input)\n () }",
            "async fn f() { 42 }",
            "async fn f(input: Task<int>): int { let nested: () -> int = () => await input\n nested() }",
            "async fn f(input: Task<int>): Unit { let builder = MutableList<int>()\n let capture: () -> Unit = () => builder.add(1)\n let value = await input\n capture() }",
            "async fn make(): Result<int, string> { Success(1) }\nasync fn f(): int { await make()? }"
        };
        foreach (var body in invalid)
        {
            var result = DSharpCompiler.Compile("module Demo\n" + body, "async-invalid.ds");
            Assert.True(result.CSharp is null, $"Invalid async scenario compiled: {body}");
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, diagnostic => Assert.Equal("async-invalid.ds", diagnostic.Path));
        }

        // An imported nominal type named Task shadows the source name, while
        // async calls and await still use the compiler's distinct Task type.
        var modules = DSharpCompiler.Compile(new[]
        {
            new SourceFile("lib.ds", """
                module Lib
                type Task<T> { value: T }
                fn stored(): Task<int> { Task { value = 40 } }
                async fn real(): int { 2 }
                """),
            new SourceFile("app.ds", """
                module App
                import Lib
                async fn answer(): int {
                    let task: Task<int> = stored()
                    let value: int = await real()
                    task.value + value
                }
                """)
        });
        Assert.Empty(modules.Diagnostics);
        Assert.NotNull(modules.CSharp);
        await WithGeneratedCSharp(modules.CSharp!, "App", async type =>
            Assert.Equal(42, await Call<Task<int>>(type, "answer")));
    }

    private static TaskCompletionSource<T> Gate<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static MethodInfo Method(Type type, string name) => type.GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
    private static T Call<T>(Type type, string name, params object?[] args) => (T)Method(type, name).Invoke(null, args)!;

    private static Delegate Adapt(Type delegateType, Delegate implementation)
    {
        var target = delegateType.GetMethod("Invoke")!;
        var args = target.GetParameters().Select(parameter => Expression.Parameter(parameter.ParameterType)).ToArray();
        var invocation = Expression.Invoke(Expression.Constant(implementation), args);
        var body = implementation.Method.ReturnType == typeof(void)
            ? Expression.Block(invocation, Expression.Default(target.ReturnType)) : (Expression)invocation;
        return Expression.Lambda(delegateType, body, args).Compile();
    }

    private static async Task Eventually(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition()) { timeout.Token.ThrowIfCancellationRequested(); await Task.Delay(1, timeout.Token); }
    }

    private static async Task WithGenerated(string source, Func<Type, Task> verify)
    {
        var compiled = DSharpCompiler.Compile(source, "async-acceptance.ds");
        Assert.Empty(compiled.Diagnostics);
        Assert.NotNull(compiled.CSharp);
        await WithGeneratedCSharp(compiled.CSharp!, "Demo", verify);
    }

    private static async Task WithGeneratedCSharp(string csharp, string module, Func<Type, Task> verify)
    {
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-async-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "Generated.cs"), csharp);
            File.WriteAllText(Path.Combine(directory, "Generated.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net11.0</TargetFramework><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>");
            File.WriteAllText(Path.Combine(directory, "global.json"), "{\"sdk\":{\"version\":\"11.0.100\",\"rollForward\":\"disable\"}}");
            File.WriteAllText(Path.Combine(directory, "NuGet.Config"), "<configuration><packageSources><clear/></packageSources></configuration>");
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "build", "Generated.csproj", "--nologo", "--verbosity", "quiet" }) start.ArgumentList.Add(argument);
            using var build = Process.Start(start)!;
            var stdout = build.StandardOutput.ReadToEndAsync();
            var stderr = build.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try { await build.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { build.Kill(true); throw new TimeoutException("Generated async build timed out."); }
            Assert.True(build.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("async-" + Guid.NewGuid(), isCollectible: true);
            try
            {
                var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0/Generated.dll"));
                await verify(assembly.GetType("DSharp.Generated." + module)!);
            }
            finally { context.Unload(); }
        }
        finally { Directory.Delete(directory, true); }
    }
}
