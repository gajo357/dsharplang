using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class ScopedResourceTests
{
    [Fact(DisplayName = "Scoped resources release on each exit")]
    public async Task ScopedResourcesExecuteAndRejectEscapes()
    {
        const string source = """
            module Demo

            interface Tracker {
                mark(value: int): int
                async ready(): int
                async released(): int
            }

            resource Cursor(id: int, mark: Tracker, fails: bool) {
                read(): int {
                    let ignored = mark.mark(2)
                    42
                }
                release(): Unit {
                    let ignored = mark.mark(id * 10)
                    if fails { let fault = 1 / 0
                        () } else { () }
                }
            }

            resource AsyncCursor(mark: Tracker) {
                async read(): int {
                    let value = await mark.ready()
                    let ignored = mark.mark(2)
                    value
                }
                async release(): Unit {
                    let ignored = await mark.released()
                    let seen = mark.mark(3)
                    ()
                }
            }

            fn normal(mark: Tracker, onCleanup: (string) -> Unit): int {
                using item = Cursor(mark.mark(1), mark, false) report onCleanup { item.read() }
            }
            fn early(mark: Tracker, onCleanup: (string) -> Unit): int {
                using item = Cursor(1, mark, false) report onCleanup { return item.read() }
            }
            fn nested(mark: Tracker, onCleanup: (string) -> Unit): int {
                using outer = Cursor(1, mark, false) report onCleanup {
                    using inner = Cursor(2, mark, false) report onCleanup { inner.read() }
                }
            }
            fn failAcquire(mark: Tracker, onCleanup: (string) -> Unit): int {
                using item = Cursor(1 / 0, mark, false) report onCleanup { item.read() }
            }
            fn failCleanup(mark: Tracker, onCleanup: (string) -> Unit): int {
                using item = Cursor(1, mark, true) report onCleanup { item.read() }
            }
            fn failBody(mark: Tracker, onCleanup: (string) -> Unit): int {
                using item = Cursor(1, mark, true) report onCleanup { 1 / 0 }
            }
            fn nestedFailures(mark: Tracker, onCleanup: (string) -> Unit): int {
                using outer = Cursor(1, mark, true) report onCleanup {
                    using inner = Cursor(2, mark, true) report onCleanup { 1 / 0 }
                }
            }
            fn failEarly(mark: Tracker, onCleanup: (string) -> Unit): int {
                using item = Cursor(1, mark, true) report onCleanup { return 42 }
            }
            fn nullable(mark: Tracker, onCleanup: (string) -> Unit): string? {
                using item = Cursor(1, mark, false) report onCleanup { null }
            }
            fn nullableError(mark: Tracker, onCleanup: (string) -> Unit): Result<int, string>? {
                using item = Cursor(1, mark, true) report onCleanup { Error<int, string>("nullable") }
            }
            fn nullableErrorEarly(mark: Tracker, onCleanup: (string) -> Unit): Result<int, string>? {
                using item = Cursor(1, mark, true) report onCleanup { return Error<int, string>("early") }
            }
            fn nullableNullFail(mark: Tracker, onCleanup: (string) -> Unit): Result<int, string>? {
                using item = Cursor(1, mark, true) report onCleanup { null }
            }
            fn nullableSuccessFail(mark: Tracker, onCleanup: (string) -> Unit): Result<int, string>? {
                using item = Cursor(1, mark, true) report onCleanup { Success<int, string>(42) }
            }
            fn propagate(mark: Tracker, onCleanup: (string) -> Unit): Result<int, string> {
                using item = Cursor(1, mark, true) report onCleanup {
                    let value = Error<int, string>("primary")?
                    Success(value)
                }
            }
            fn resultCode(mark: Tracker, onCleanup: (string) -> Unit): int {
                match propagate(mark, onCleanup) {
                    Success(value) => value
                    Error(error) => if error == "primary" { 42 } else { 0 }
                }
            }
            fn finalError(mark: Tracker, onCleanup: (string) -> Unit): Result<int, string> {
                using item = Cursor(1, mark, true) report onCleanup { Error("final") }
            }
            fn finalCode(mark: Tracker, onCleanup: (string) -> Unit): int {
                match finalError(mark, onCleanup) {
                    Success(value) => value
                    Error(error) => if error == "final" { 42 } else { 0 }
                }
            }
            fn handled(mark: Tracker, onCleanup: (string) -> Unit): Result<int, string> {
                using item = Cursor(1, mark, true) report onCleanup {
                    let value = match Error<int, string>("handled") {
                        Error(_) => 42
                        Success(number) => number
                    }
                    Success(value)
                }
            }
            fn nestedError(mark: Tracker, onCleanup: (string) -> Unit): Result<Result<int, string>, string> {
                using item = Cursor(1, mark, true) report onCleanup { Success(Error<int, string>("nested")) }
            }
            async fn delayed(mark: Tracker, onCleanup: (string) -> Unit): int {
                using item = AsyncCursor(mark) report onCleanup { await item.read() }
            }
            """;
        var result = DSharpCompiler.Compile(source, "scoped-resources.ds");
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        await WithGenerated(result.CSharp!, async type =>
        {
            var order = new ConcurrentQueue<int>();
            var reports = new ConcurrentQueue<string>();
            int Mark(int n) { order.Enqueue(n); return n; }
            void Report(string name) => reports.Enqueue(name);
            var tracker = (TrackerProxy)DispatchProxy.Create(Method(type, "normal").GetParameters()[0].ParameterType, typeof(TrackerProxy));
            tracker.Mark = Mark;
            object?[] Args(string name) => AdaptArguments(type, name, tracker, (Action<string>)Report);

            Assert.Equal(42, Invoke<int>(type, "normal", Args("normal")));
            Assert.Equal(new[] { 1, 2, 10 }, order);
            Assert.Empty(reports);
            order.Clear();
            Assert.Equal(42, Invoke<int>(type, "early", Args("early")));
            Assert.Equal(new[] { 2, 10 }, order);
            order.Clear();
            Assert.Null(Invoke<string?>(type, "nullable", Args("nullable")));
            Assert.Equal(new[] { 10 }, order);
            Assert.Empty(reports);
            order.Clear();
            foreach (var (name, payload) in new[] { ("nullableError", "nullable"), ("nullableErrorEarly", "early") })
            {
                var outcome = Method(type, name).Invoke(null, Args(name));
                Assert.NotNull(outcome);
                Assert.Equal(payload, outcome.GetType().GetProperty("__dsharp_case_1_item_0", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(outcome));
                Assert.Equal(new[] { 10 }, order);
                Assert.Equal("Cursor", Assert.Single(reports));
                order.Clear(); reports.Clear();
            }
            foreach (var name in new[] { "nullableNullFail", "nullableSuccessFail" })
            {
                Assert.IsType<DivideByZeroException>(Assert.Throws<TargetInvocationException>(() => Method(type, name).Invoke(null, Args(name))).InnerException);
                Assert.Equal(new[] { 10 }, order);
                Assert.Empty(reports);
                order.Clear();
            }
            Assert.Equal(42, Invoke<int>(type, "nested", Args("nested")));
            Assert.Equal(new[] { 2, 20, 10 }, order);
            order.Clear();
            Assert.IsType<DivideByZeroException>(Assert.Throws<TargetInvocationException>(() => Method(type, "failAcquire").Invoke(null, Args("failAcquire"))).InnerException);
            Assert.Empty(order);
            Assert.Empty(reports);
            Assert.IsType<DivideByZeroException>(Assert.Throws<TargetInvocationException>(() => Method(type, "failCleanup").Invoke(null, Args("failCleanup"))).InnerException);
            Assert.Equal(new[] { 2, 10 }, order);
            Assert.Empty(reports);
            order.Clear();
            Assert.IsType<DivideByZeroException>(Assert.Throws<TargetInvocationException>(() => Method(type, "failEarly").Invoke(null, Args("failEarly"))).InnerException);
            Assert.Equal(new[] { 10 }, order);
            Assert.Empty(reports);
            order.Clear();
            Assert.IsType<DivideByZeroException>(Assert.Throws<TargetInvocationException>(() => Method(type, "failBody").Invoke(null, Args("failBody"))).InnerException);
            Assert.Equal(new[] { 10 }, order);
            Assert.Equal("Cursor", Assert.Single(reports));
            order.Clear(); reports.Clear();
            Assert.IsType<DivideByZeroException>(Assert.Throws<TargetInvocationException>(() => Method(type, "nestedFailures").Invoke(null, Args("nestedFailures"))).InnerException);
            Assert.Equal(new[] { 20, 10 }, order);
            Assert.Equal(new[] { "Cursor", "Cursor" }, reports);
            order.Clear(); reports.Clear();
            Assert.Equal(42, Invoke<int>(type, "resultCode", Args("resultCode")));
            Assert.Equal(new[] { 10 }, order);
            Assert.Single(reports);
            order.Clear(); reports.Clear();
            Assert.Equal(42, Invoke<int>(type, "finalCode", Args("finalCode")));
            Assert.Equal(new[] { 10 }, order);
            Assert.Single(reports);
            order.Clear(); reports.Clear();
            Assert.IsType<DivideByZeroException>(Assert.Throws<TargetInvocationException>(() => Method(type, "handled").Invoke(null, Args("handled"))).InnerException);
            Assert.Empty(reports);
            order.Clear();
            Assert.IsType<DivideByZeroException>(Assert.Throws<TargetInvocationException>(() => Method(type, "nestedError").Invoke(null, Args("nestedError"))).InnerException);
            Assert.Empty(reports);
            order.Clear();

            Action<string> brokenReporter = _ => throw new InvalidOperationException("reporter failed");
            var brokenArgs = AdaptArguments(type, "failBody", tracker, brokenReporter);
            var combined = Assert.Throws<TargetInvocationException>(() => Method(type, "failBody").Invoke(null, brokenArgs)).InnerException!;
            Assert.EndsWith("ScopeCleanupException", combined.GetType().Name);
            Assert.Equal("Fault", combined.GetType().GetProperty("PrimaryKind")!.GetValue(combined));
            Assert.IsType<DivideByZeroException>(combined.GetType().GetProperty("PrimaryOutcome")!.GetValue(combined));
            Assert.IsType<DivideByZeroException>(combined.GetType().GetProperty("CleanupFailure")!.GetValue(combined));
            Assert.IsType<InvalidOperationException>(combined.GetType().GetProperty("ReporterFailure")!.GetValue(combined));
            order.Clear();
            brokenArgs = AdaptArguments(type, "propagate", tracker, brokenReporter);
            combined = Assert.Throws<TargetInvocationException>(() => Method(type, "propagate").Invoke(null, brokenArgs)).InnerException!;
            Assert.EndsWith("ScopeCleanupException", combined.GetType().Name);
            Assert.Equal("TypedError", combined.GetType().GetProperty("PrimaryKind")!.GetValue(combined));
            var primary = combined.GetType().GetProperty("PrimaryOutcome")!.GetValue(combined)!;
            Assert.Equal("primary", primary.GetType().GetProperty("__dsharp_case_1_item_0", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(primary));
            Assert.IsType<DivideByZeroException>(combined.GetType().GetProperty("CleanupFailure")!.GetValue(combined));
            Assert.IsType<InvalidOperationException>(combined.GetType().GetProperty("ReporterFailure")!.GetValue(combined));
            order.Clear();

            var ready = Gate();
            var released = Gate();
            tracker.Ready = () => ready.Task;
            tracker.Released = () => released.Task;
            var delayedArgs = AdaptArguments(type, "delayed", tracker, (Action<string>)Report);
            var pending = Invoke<Task<int>>(type, "delayed", delayedArgs);
            Assert.False(pending.IsCompleted);
            ready.SetResult(42);
            await WaitUntil(() => order.Count > 0);
            Assert.Equal(new[] { 2 }, order);
            Assert.False(pending.IsCompleted);
            released.SetResult(0);
            Assert.Equal(42, await pending.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(new[] { 2, 3 }, order);
            Assert.Empty(reports);

            order.Clear(); reports.Clear();
            ready = Gate(); released = Gate();
            delayedArgs = AdaptArguments(type, "delayed", tracker, (Action<string>)Report);
            pending = Invoke<Task<int>>(type, "delayed", delayedArgs);
            ready.SetCanceled();
            released.SetException(new InvalidOperationException("release failed"));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
            Assert.Equal("AsyncCursor", Assert.Single(reports));

            reports.Clear();
            ready = Gate(); released = Gate();
            delayedArgs = AdaptArguments(type, "delayed", tracker, brokenReporter);
            pending = Invoke<Task<int>>(type, "delayed", delayedArgs);
            ready.SetCanceled();
            released.SetException(new InvalidOperationException("release failed"));
            var cancelledAndUnreported = await Record.ExceptionAsync(async () => await pending);
            Assert.NotNull(cancelledAndUnreported);
            Assert.EndsWith("ScopeCleanupException", cancelledAndUnreported.GetType().Name);
            Assert.Equal("Cancellation", cancelledAndUnreported.GetType().GetProperty("PrimaryKind")!.GetValue(cancelledAndUnreported));
            Assert.IsAssignableFrom<OperationCanceledException>(cancelledAndUnreported.GetType().GetProperty("PrimaryOutcome")!.GetValue(cancelledAndUnreported));
            Assert.Equal("release failed", Assert.IsType<InvalidOperationException>(cancelledAndUnreported.GetType().GetProperty("CleanupFailure")!.GetValue(cancelledAndUnreported)).Message);
            Assert.Equal("reporter failed", Assert.IsType<InvalidOperationException>(cancelledAndUnreported.GetType().GetProperty("ReporterFailure")!.GetValue(cancelledAndUnreported)).Message);
        });

        RejectResourceEscapes();
        await ImportedResourceUsesItsDeclaringModule();
    }

    private static async Task ImportedResourceUsesItsDeclaringModule()
    {
        var sources = new[]
        {
            new SourceFile("lib.ds", """
                module Lib
                fn adjust(value: int): int { value + 2 }
                resource Cursor(value: int) {
                    read(): int { adjust(value) }
                    release(): Unit { () }
                }
                """),
            new SourceFile("app.ds", """
                module App
                import Lib
                fn adjust(value: int): int { 0 }
                fn answer(onCleanup: (string) -> Unit): int {
                    using item = Lib.Cursor(40) report onCleanup { item.read() }
                }
                """)
        };
        var result = DSharpCompiler.Compile(sources);
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        await WithGenerated(result.CSharp!, type =>
        {
            var reports = new List<string>();
            var args = AdaptArguments(type, "answer", (Action<string>)reports.Add);
            Assert.Equal(42, Invoke<int>(type, "answer", args));
            Assert.Empty(reports);
            return Task.CompletedTask;
        }, "App");
    }

    private static void RejectResourceEscapes()
    {
        const string resource = "resource Item { read(): int { 42 }\n release(): Unit { () } }\n" +
            "interface AsyncSource { async read(): int }\n" +
            "resource AsyncItem(input: AsyncSource) { async read(): int { await input.read() }\n release(): Unit { () } }\n" +
            "resource AsyncRelease { async release(): Unit { () } }\n";
        var invalid = new[]
        {
            "fn f(): Unit { let item = Item()\n () }",
            "fn f(onCleanup: (string) -> Unit): int { using item = Item() report onCleanup { item.release()\n 42 } }",
            "fn f(onCleanup: (string) -> Unit): Item { using item = Item() report onCleanup { item } }",
            "fn f(onCleanup: (string) -> Unit): int { using item = Item() report onCleanup { let alias = item\n alias.read() } }",
            "fn id<T>(x: T): T { x }\nfn f(onCleanup: (string) -> Unit): int { using item = Item() report onCleanup { id(item).read() } }",
            "fn f(onCleanup: (string) -> Unit): int { using item = Item() report onCleanup { let later: () -> int = () => item.read()\n later() } }",
            "fn use(value: Item): int { value.read() }\nfn f(onCleanup: (string) -> Unit): int { using item = Item() report onCleanup { use(item) } }",
            "fn f(onCleanup: (string) -> Unit): int { using item = 42 report onCleanup { item } }",
            "fn f(): int { using item = Item() report missing { item.read() } }",
            "fn f(onCleanup: (int) -> Unit): int { using item = Item() report onCleanup { item.read() } }",
            "fn f(onCleanup: (string) -> Unit): int { using item = Item() report onCleanup() { item.read() } }",
            "fn f<T>(value: T, onCleanup: (string) -> Unit): T { using item = Item() report onCleanup { value } }",
            "fn f(onCleanup: (string) -> Unit): int { let value = using item = Item() report onCleanup { null }\n 42 }",
            "async fn f(input: AsyncSource, onCleanup: (string) -> Unit): int { using item = AsyncItem(input) report onCleanup { let task = item.read()\n await task } }",
            "fn f(onCleanup: (string) -> Unit): int { using item = AsyncRelease() report onCleanup { 42 } }"
        };
        foreach (var body in invalid)
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + resource + body, "invalid-resource.ds");
            Assert.True(result.CSharp is null, $"Invalid resource program compiled: {body}");
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, diagnostic => Assert.Equal("invalid-resource.ds", diagnostic.Path));
        }

        const string nestedTaskEscape = """
            module Invalid
            interface Source { async read(): int }
            resource Inner(src: Source) {
                async read(): int { await src.read() }
                release(): Unit { () }
            }
            resource Outer {
                async use(input: Task<int>): int { await input }
                release(): Unit { () }
            }
            async fn bad(src: Source, onCleanup: (string) -> Unit): int {
                using outer = Outer() report onCleanup {
                    using inner = Inner(src) report onCleanup {
                        await outer.use(inner.read())
                    }
                }
            }
            """;
        var escaped = DSharpCompiler.Compile(nestedTaskEscape, "invalid-resource.ds");
        Assert.Null(escaped.CSharp);
        Assert.Contains(escaped.Diagnostics, diagnostic => diagnostic.Code == "DS2041");
    }

    public class TrackerProxy : DispatchProxy
    {
        public Func<int, int> Mark { get; set; } = _ => 0;
        public Func<Task<int>> Ready { get; set; } = () => Task.FromResult(0);
        public Func<Task<int>> Released { get; set; } = () => Task.FromResult(0);

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? arguments) => targetMethod!.Name switch
        {
            "__dsharp_method_mark" => Mark((int)arguments![0]!),
            "__dsharp_method_ready" => Ready(),
            "__dsharp_method_released" => Released(),
            _ => throw new InvalidOperationException(targetMethod.Name)
        };
    }

    private static TaskCompletionSource<int> Gate() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static async Task WaitUntil(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition()) { timeout.Token.ThrowIfCancellationRequested(); await Task.Delay(5, timeout.Token); }
    }

    private static MethodInfo Method(Type type, string name) => type.GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
    private static T Invoke<T>(Type type, string name, params object?[] arguments) => (T)Method(type, name).Invoke(null, arguments)!;
    private static object?[] AdaptArguments(Type type, string name, params object[] values)
    {
        var parameters = Method(type, name).GetParameters();
        return values.Select((value, index) => value switch
        {
            Action<string> report => Adapt(parameters[index].ParameterType, typeof(string), report),
            _ => value
        }).ToArray();
    }
    private static Delegate Adapt(Type delegateType, Type argumentType, Delegate callback)
    {
        var argument = Expression.Parameter(argumentType, "value");
        var call = Expression.Invoke(Expression.Constant(callback), argument);
        var returnType = delegateType.GetMethod("Invoke")!.ReturnType;
        Expression body = callback is Action<string> ? Expression.Block(call, Expression.Default(returnType)) : call;
        return Expression.Lambda(delegateType, body, argument).Compile();
    }

    private static async Task WithGenerated(string csharp, Func<Type, Task> verify, string module = "Demo")
    {
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-scoped-resource-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "Generated.cs"), csharp);
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
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated resource build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("scoped-resources-" + Guid.NewGuid(), isCollectible: false);
            var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0/Generated.dll"));
            var type = Assert.Single(assembly.GetTypes(), candidate => candidate.Name == module);
            await verify(type);
        }
        finally { Directory.Delete(directory, true); }
    }
}
