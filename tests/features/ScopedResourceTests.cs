using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class ScopedResourceTests
{
    [Fact(DisplayName = "Scoped resources follow C# disposal semantics")]
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
                    if fails { let fault = 2147483647 + 1
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

            fn braced(mark: Tracker): int {
                using item = Cursor(mark.mark(1), mark, false) { item.read() }
            }
            fn declared(mark: Tracker): int {
                using item = Cursor(mark.mark(1), mark, false)
                item.read()
            }
            fn declaredEarly(mark: Tracker): int {
                using item = Cursor(1, mark, false)
                return item.read()
            }
            fn boundary(mark: Tracker): int {
                let value = if true {
                    using item = Cursor(1, mark, false)
                    item.read()
                } else { 0 }
                let ignored = mark.mark(99)
                value
            }
            fn nested(mark: Tracker): int {
                using outer = Cursor(1, mark, false)
                using inner = Cursor(2, mark, false)
                inner.read()
            }
            fn mixed(mark: Tracker): int {
                using outer = Cursor(1, mark, false)
                using inner = Cursor(2, mark, false) { inner.read() }
            }
            fn emptyRemainder(mark: Tracker): Unit {
                using item = Cursor(1, mark, false)
            }
            fn iterations(mark: Tracker): int {
                for number in [1, 2] {
                    using item = Cursor(number, mark, false)
                    let ignored = item.read()
                    ()
                }
                42
            }
            fn laterAcquireFails(mark: Tracker): int {
                using outer = Cursor(1, mark, false)
                using inner = Cursor(1 / 0, mark, false)
                inner.read()
            }
            fn generic<T>(mark: Tracker, value: T): T {
                using item = Cursor(1, mark, false)
                value
            }
            fn genericAnswer(mark: Tracker): int { generic<int>(mark, 42) }
            fn inferred(mark: Tracker) {
                using item = Cursor(1, mark, false)
                42
            }
            fn failAcquire(mark: Tracker): int {
                using item = Cursor(1 / 0, mark, false)
                item.read()
            }
            fn failCleanup(mark: Tracker): int {
                using item = Cursor(1, mark, true) { item.read() }
            }
            fn bodyFailure(mark: Tracker): int {
                using item = Cursor(1, mark, false)
                1 / 0
            }
            fn nestedMarkerFailures(mark: Tracker): int {
                using outer = Cursor(1, mark, false)
                using inner = Cursor(2, mark, false)
                1 / 0
            }
            fn failBody(mark: Tracker): int {
                using item = Cursor(1, mark, true)
                1 / 0
            }
            fn nestedFailures(mark: Tracker): int {
                using outer = Cursor(1, mark, true)
                using inner = Cursor(2, mark, true)
                1 / 0
            }
            fn failEarly(mark: Tracker): int {
                using item = Cursor(1, mark, true)
                return 42
            }
            fn nullable(mark: Tracker): string? {
                using item = Cursor(1, mark, false) { null }
            }
            fn nullableError(mark: Tracker): Result<int, string>? {
                using item = Cursor(1, mark, true) { Error<int, string>("nullable") }
            }
            fn nullableErrorEarly(mark: Tracker): Result<int, string>? {
                using item = Cursor(1, mark, true)
                return Error<int, string>("early")
            }
            fn nullableNullFail(mark: Tracker): Result<int, string>? {
                using item = Cursor(1, mark, true) { null }
            }
            fn nullableSuccessFail(mark: Tracker): Result<int, string>? {
                using item = Cursor(1, mark, true) { Success<int, string>(42) }
            }
            fn propagate(mark: Tracker, fails: bool): Result<int, string> {
                using item = Cursor(1, mark, fails)
                let value = Error<int, string>("primary")?
                Success(value)
            }
            fn resultCode(mark: Tracker): int {
                match propagate(mark, false) {
                    Success(value) => value
                    Error(error) => if error == "primary" { 42 } else { 0 }
                }
            }
            fn finalError(mark: Tracker): Result<int, string> {
                using item = Cursor(1, mark, true) { Error("final") }
            }
            fn handled(mark: Tracker): Result<int, string> {
                using item = Cursor(1, mark, true) {
                    let value = match Error<int, string>("handled") {
                        Error(_) => 42
                        Success(number) => number
                    }
                    Success(value)
                }
            }
            fn nestedError(mark: Tracker): Result<Result<int, string>, string> {
                using item = Cursor(1, mark, true) { Success(Error<int, string>("nested")) }
            }
            async fn delayed(mark: Tracker): int {
                using item = AsyncCursor(mark)
                await item.read()
            }
            async fn delayedBraced(mark: Tracker): int {
                using item = AsyncCursor(mark) { await item.read() }
            }
            """;
        var result = DSharpCompiler.Compile(source, "scoped-resources.ds");
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        await WithGenerated(result.CSharp!, async type =>
        {
            var order = new ConcurrentQueue<int>();
            var tracker = (TrackerProxy)DispatchProxy.Create(Method(type, "braced").GetParameters()[0].ParameterType, typeof(TrackerProxy));
            tracker.Mark = n => { order.Enqueue(n); return n; };
            object?[] Args(params object?[] extra) => new object?[] { tracker }.Concat(extra).ToArray();
            void Seen(params int[] expected) { Assert.Equal(expected, order); order.Clear(); }
            void CleanupFault(string name, params object?[] extra)
            {
                var thrown = Assert.Throws<TargetInvocationException>(() => Method(type, name).Invoke(null, Args(extra)));
                Assert.IsType<OverflowException>(thrown.InnerException);
            }

            Assert.Equal(42, Invoke<int>(type, "braced", Args())); Seen(1, 2, 10);
            Assert.Equal(42, Invoke<int>(type, "declared", Args())); Seen(1, 2, 10);
            Assert.Equal(42, Invoke<int>(type, "declaredEarly", Args())); Seen(2, 10);
            Assert.Equal(42, Invoke<int>(type, "boundary", Args())); Seen(2, 10, 99);
            Assert.Equal(42, Invoke<int>(type, "nested", Args())); Seen(2, 20, 10);
            Assert.Equal(42, Invoke<int>(type, "mixed", Args())); Seen(2, 20, 10);
            _ = Method(type, "emptyRemainder").Invoke(null, Args()); Seen(10);
            Assert.Equal(42, Invoke<int>(type, "iterations", Args())); Seen(2, 10, 2, 20);
            Assert.Equal(42, Invoke<int>(type, "genericAnswer", Args())); Seen(10);
            Assert.Equal(42, Invoke<int>(type, "inferred", Args())); Seen(10);
            var laterAcquisition = Assert.Throws<TargetInvocationException>(() => Method(type, "laterAcquireFails").Invoke(null, Args()));
            Assert.IsType<DivideByZeroException>(laterAcquisition.InnerException); Seen(10);
            Assert.Null(Invoke<string?>(type, "nullable", Args())); Seen(10);
            var acquisition = Assert.Throws<TargetInvocationException>(() => Method(type, "failAcquire").Invoke(null, Args()));
            Assert.IsType<DivideByZeroException>(acquisition.InnerException); Seen();

            var bodyFailure = Assert.Throws<TargetInvocationException>(() => Method(type, "bodyFailure").Invoke(null, Args()));
            Assert.IsType<DivideByZeroException>(bodyFailure.InnerException); Seen(10);
            tracker.Mark = n => {
                order.Enqueue(n);
                if (n == 20) throw new ArgumentException("inner cleanup");
                if (n == 10) throw new InvalidOperationException("outer cleanup");
                return n;
            };
            var multipleFailures = Assert.Throws<TargetInvocationException>(() => Method(type, "nestedMarkerFailures").Invoke(null, Args()));
            Assert.Equal("outer cleanup", Assert.IsType<InvalidOperationException>(multipleFailures.InnerException).Message);
            Seen(20, 10);
            tracker.Mark = n => { order.Enqueue(n); return n; };
            CleanupFault("failCleanup"); Seen(2, 10);
            CleanupFault("failBody"); Seen(10); // cleanup masks DivideByZeroException
            CleanupFault("nestedFailures"); Seen(20, 10);
            CleanupFault("failEarly"); Seen(10);
            foreach (var name in new[] { "nullableError", "nullableErrorEarly", "nullableNullFail", "nullableSuccessFail", "finalError", "handled", "nestedError" })
            {
                CleanupFault(name); Seen(10);
            }
            Assert.Equal(42, Invoke<int>(type, "resultCode", Args())); Seen(10);
            CleanupFault("propagate", true); Seen(10); // cleanup masks propagated Result.Error

            foreach (var name in new[] { "delayed", "delayedBraced" })
            {
                var ready = Gate();
                var released = Gate();
                tracker.Ready = () => ready.Task;
                tracker.Released = () => released.Task;
                var pending = Invoke<Task<int>>(type, name, Args());
                Assert.False(pending.IsCompleted);
                ready.SetResult(42);
                await WaitUntil(() => order.Count > 0);
                Assert.Equal(new[] { 2 }, order);
                Assert.False(pending.IsCompleted);
                released.SetResult(0);
                Assert.Equal(42, await pending.WaitAsync(TimeSpan.FromSeconds(10)));
                Seen(2, 3);

                ready = Gate(); released = Gate();
                pending = Invoke<Task<int>>(type, name, Args());
                var bodyException = new InvalidOperationException("body failed");
                ready.SetException(bodyException);
                released.SetResult(0);
                Assert.Same(bodyException, await Assert.ThrowsAsync<InvalidOperationException>(async () => await pending));
                Seen(3);

                ready = Gate(); released = Gate();
                pending = Invoke<Task<int>>(type, name, Args());
                ready.SetException(new InvalidOperationException("body failed"));
                released.SetException(new ArgumentException("cleanup failed"));
                Assert.Equal("cleanup failed", (await Assert.ThrowsAsync<ArgumentException>(async () => await pending)).Message);
                Seen();

                ready = Gate(); released = Gate();
                pending = Invoke<Task<int>>(type, name, Args());
                ready.SetCanceled();
                Assert.False(pending.IsCompleted);
                released.SetResult(0);
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
                Seen(3);

                ready = Gate(); released = Gate();
                pending = Invoke<Task<int>>(type, name, Args());
                ready.SetCanceled();
                Assert.False(pending.IsCompleted); // async cleanup still awaited after cancellation
                released.SetException(new InvalidOperationException("cleanup failed"));
                var cleanup = await Assert.ThrowsAsync<InvalidOperationException>(async () => await pending);
                Assert.Equal("cleanup failed", cleanup.Message); // cleanup masks cancellation
                Seen();
            }
        });

        RejectResourceEscapes();
        await ImportedResourceUsesItsDeclaringModule();
        await new OwnedResourceFactoryTests().FactoriesTransferAndRejectEscapes();
        await new OwnedServiceFactoryTests().ServiceFactoriesTransferAndRejectEscapes();
        await new OwnedResultResourceTests().OwnedResultsTransferAndRejectEscapes();
        await new ExtensionAcceptanceTests().ExtensionsResolveAndExecute();
        await new IntegerWidthsTests().IntegerWidthsExecuteAndRejectInvalidPrograms();
        await new InterpolationAcceptanceTests().InterpolatedStringsExecuteAndRejectInvalidPrograms();
        await new ExtensionPartialAcceptanceTests().ExtensionPartialsCaptureAndExecute();
        await new MapPartialAcceptanceTests().MapPartialsCaptureAndExecute();
        await new ListFoldPartialAcceptanceTests().FoldPartialsCaptureAndExecute();
        await new PartialInferenceAcceptanceTests().StaticPartialCallsInferAndIndirectCallsRemainChecked();
        await new EmptyCollectionPartialAcceptanceTests().EmptyCollectionPartialsExecuteAndRejectInvalidPrograms();
        await new GenericListEmptyPartialAcceptanceTests().GenericListEmptyPartialsExecuteAndRejectInvalidPrograms();
        await new EmptyCollectionInferenceAcceptanceTests().EmptyCollectionPartialCallsInferSafely();
        await new ConditionalCallTests().ConditionalCallsPreserveNullAndEvaluationOrder();
        new NullableOperatorTests().CoalescingIsRightAssociativeAndConditionalAccessParsesInsideConditions();
        new CommentTests().UnterminatedBlockCommentPointsAtOpeningDelimiter();
        new CommentTests().NestedBlockCommentIsRejectedAtTheNestedOpener();
        await new CollectionMethodValueTests().CollectionMethodValuesExecuteAndRejectInvalidPrograms();
        await new CollectionMethodPipeTests().CollectionMethodPipesExecuteAndRejectInvalidPrograms();
        new CommentTests().MultilineCommentsAndUnicodePreserveUtf16Location();
        await new BareCollectionPipeInferenceTests().BareCollectionPipesInferAndRejectInvalidPrograms();
        await new BareExtensionPipeTests().PipesExecuteWithOrderAndDeferredWork();
        await new MultilineBindingHeaderTests().ParenthesizedHeadersContinueAcrossLinesOnlyInsideTheirDelimiters();
        await new DecimalTests().DecimalValuesExecuteAndInvalidProgramsAreDiagnosed();
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
                fn answer(): int {
                    using item = Lib.Cursor(40)
                    item.read()
                }
                fn braced(): int {
                    using item = Lib.Cursor(40) { item.read() }
                }
                fn shadow(): int {
                    using Lib = Lib.Cursor(40)
                    Lib.read()
                }
                """)
        };
        var result = DSharpCompiler.Compile(sources);
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        await WithGenerated(result.CSharp!, type =>
        {
            Assert.Equal(42, Invoke<int>(type, "answer"));
            Assert.Equal(42, Invoke<int>(type, "braced"));
            Assert.Equal(42, Invoke<int>(type, "shadow"));
            return Task.CompletedTask;
        }, "App");
        var outside = DSharpCompiler.Compile(new[]
        {
            sources[0],
            new SourceFile("outside.ds", "module App\nimport Lib\nfn bad(): int { let value = using item = Lib.Cursor(40) { item.read() }\n item.read() }")
        });
        Assert.Null(outside.CSharp);
        Assert.NotEmpty(outside.Diagnostics);
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
            "fn f(): int { using item = Item() { item.release()\n 42 } }",
            "fn f(): Item { using item = Item() { item } }",
            "fn f(): int { using item = Item()\n let alias = item\n alias.read() }",
            "fn id<T>(x: T): T { x }\nfn f(): int { using item = Item() { id(item).read() } }",
            "fn f(): int { using item = Item() { let later: () -> int = () => item.read()\n later() } }",
            "fn use(value: Item): int { value.read() }\nfn f(): int { using item = Item() { use(item) } }",
            "fn f(): int { using item = 42 { item } }",
            "fn f(): int { let x = using item = Item() { item.read() }\n item.read() }",
            "fn f(): int { let value = using item = Item() { null }\n 42 }",
            "async fn f(input: AsyncSource): int { using item = AsyncItem(input) { let task = item.read()\n await task } }",
            "fn f(): int { using item = AsyncRelease() { 42 } }",
            "fn f(): int { using item = Item() report missing { item.read() } }"
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
            async fn bad(src: Source): int {
                using outer = Outer()
                using inner = Inner(src)
                await outer.use(inner.read())
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
