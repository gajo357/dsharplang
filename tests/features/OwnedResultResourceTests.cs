using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class OwnedResultResourceTests
{
    // This suite runs from ScopedResourceTests so CI retains its bounded TRX size.
    public async Task OwnedResultsTransferAndRejectEscapes()
    {
        const string source = """
            module Demo

            interface Tracker { mark(value: int): int }
            resource Cursor(value: int, tracker: Tracker) {
                read(): int { let ignored = tracker.mark(4)
                    value }
                release(): Unit { let ignored = tracker.mark(5)
                    () }
            }
            resource Guard(tracker: Tracker, fails: bool) {
                release(): Unit {
                    let ignored = tracker.mark(6)
                    if fails { let fault = 1 / 0
                        () } else { () }
                }
            }
            fn make(value: int, tracker: Tracker): Cursor { Cursor(value, tracker) }
            fn open(value: int, tracker: Tracker): Result<Cursor, string> {
                let ignored = tracker.mark(3)
                if value >= 0 { Success(make(value, tracker)) }
                else { Error("missing") }
            }
            fn forward(value: int, tracker: Tracker): Result<Cursor, string> {
                open(value, tracker)
            }
            async fn openFunctionAsync(input: Task<int>, tracker: Tracker): Result<Cursor, string> {
                let value = await input
                if value >= 0 { Success(Cursor(value, tracker)) } else { Error("missing") }
            }
            async fn forwardFunctionAsync(input: Task<int>, tracker: Tracker): Result<Cursor, string> {
                await openFunctionAsync(input, tracker)
            }
            fn explicitOpen(value: int, tracker: Tracker): Result<Cursor, string> {
                Result.Success<Cursor, string>(Cursor(value, tracker))
            }
            fn validate(value: int): Result<int, string> {
                if value >= 0 { Success(value) } else { Error("invalid") }
            }
            fn validated(value: int, tracker: Tracker): Result<Cursor, string> {
                let checked = validate(value)?
                Success(Cursor(checked, tracker))
            }
            interface Store {
                open(value: int): Result<Cursor, string>
                openDefault(value: int): Result<Cursor, string> { open(value) }
                async openAsync(input: Task<int>): Result<Cursor, string>
                async openAgain(input: Task<int>): Result<Cursor, string> { await openAsync(input) }
            }
            service MemoryStore(tracker: Tracker): Store {
                open(value) {
                    let ignored = tracker.mark(3)
                    if value >= 0 { Success(Cursor(value, tracker)) }
                    else { Error("missing") }
                }
                openAsync(input) {
                    let value = await input
                    if value >= 0 { Success(Cursor(value, tracker)) }
                    else { Error("missing") }
                }
            }
            service Override(tracker: Tracker): Store {
                open(value) { Error("not selected") }
                openDefault(value) { Success(Cursor(value + 1, tracker)) }
                openAsync(input) { Success(Cursor(await input, tracker)) }
            }
            fn getStore(tracker: Tracker): Store {
                let ignored = tracker.mark(1)
                MemoryStore(tracker)
            }
            fn fromFunction(value: int, tracker: Tracker): Result<int, string> {
                using cursor = forward(value, tracker)?
                Success(cursor.read() + 2)
            }
            fn fromExplicit(tracker: Tracker): Result<int, string> {
                using cursor = explicitOpen(40, tracker)? { Success(cursor.read() + 2) }
            }
            fn fromValidated(value: int, tracker: Tracker): Result<int, string> {
                using cursor = validated(value, tracker)?
                Success(cursor.read() + 2)
            }
            fn fromService(value: int, tracker: Tracker): Result<int, string> {
                using cursor = getStore(tracker).openDefault(tracker.mark(2) + value)?
                Success(cursor.read() + 2)
            }
            fn fromOverride(tracker: Tracker): Result<int, string> {
                let store: Store = Override(tracker)
                using cursor = store.openDefault(39)?
                Success(cursor.read() + 2)
            }
            fn outerError(tracker: Tracker, fail: bool): Result<int, string> {
                using outer = Guard(tracker, fail)
                let store: Store = MemoryStore(tracker)
                using cursor = store.open(-1)?
                Success(cursor.read())
            }
            fn resultCode(value: Result<int, string>): int {
                match value { Success(number) => number
                    Error("missing") => 7
                    Error("invalid") => 8
                    Error(_) => 9 }
            }
            fn functionCode(value: int, tracker: Tracker): int { resultCode(fromFunction(value, tracker)) }
            fn explicitCode(tracker: Tracker): int { resultCode(fromExplicit(tracker)) }
            fn validatedCode(value: int, tracker: Tracker): int { resultCode(fromValidated(value, tracker)) }
            fn serviceCode(value: int, tracker: Tracker): int { resultCode(fromService(value, tracker)) }
            fn overrideCode(tracker: Tracker): int { resultCode(fromOverride(tracker)) }
            fn outerCode(tracker: Tracker, fail: bool): int { resultCode(outerError(tracker, fail)) }
            async fn asyncService(input: Task<int>, tracker: Tracker): Result<int, string> {
                let store: Store = MemoryStore(tracker)
                using cursor = (await store.openAgain(input))?
                Success(cursor.read() + 2)
            }
            async fn asyncCode(input: Task<int>, tracker: Tracker): int {
                resultCode(await asyncService(input, tracker))
            }
            async fn asyncFunction(input: Task<int>, tracker: Tracker): Result<int, string> {
                using cursor = (await forwardFunctionAsync(input, tracker))?
                Success(cursor.read() + 2)
            }
            async fn asyncFunctionCode(input: Task<int>, tracker: Tracker): int {
                resultCode(await asyncFunction(input, tracker))
            }
            async fn asyncOuter(input: Task<int>, tracker: Tracker, fail: bool): Result<int, string> {
                using outer = Guard(tracker, fail)
                let store: Store = MemoryStore(tracker)
                using cursor = (await store.openAsync(input))?
                Success(cursor.read() + 2)
            }
            async fn asyncOuterCode(input: Task<int>, tracker: Tracker, fail: bool): int {
                resultCode(await asyncOuter(input, tracker, fail))
            }
            """;
        var compiled = DSharpCompiler.Compile(source, "owned-result-resource.ds");
        Assert.True(compiled.CSharp is not null, string.Join("\n", compiled.Diagnostics));
        Assert.Empty(compiled.Diagnostics);
        await WithGenerated(compiled.CSharp!, "Demo", async type =>
        {
            var observed = new ConcurrentQueue<int>();
            var tracker = (TrackerProxy)DispatchProxy.Create(Method(type, "functionCode").GetParameters()[1].ParameterType, typeof(TrackerProxy));
            tracker.Mark = value => { observed.Enqueue(value); return value; };
            void Seen(params int[] expected) { Assert.Equal(expected, observed); observed.Clear(); }
            Assert.Equal(42, Invoke<int>(type, "functionCode", 40, tracker)); Seen(3, 4, 5);
            Assert.Equal(7, Invoke<int>(type, "functionCode", -1, tracker)); Seen(3);
            Assert.Equal(42, Invoke<int>(type, "explicitCode", tracker)); Seen(4, 5);
            Assert.Equal(42, Invoke<int>(type, "validatedCode", 40, tracker)); Seen(4, 5);
            Assert.Equal(8, Invoke<int>(type, "validatedCode", -1, tracker)); Seen();
            Assert.Equal(42, Invoke<int>(type, "serviceCode", 38, tracker)); Seen(1, 2, 3, 4, 5);
            Assert.Equal(7, Invoke<int>(type, "serviceCode", -3, tracker)); Seen(1, 2, 3);
            Assert.Equal(42, Invoke<int>(type, "overrideCode", tracker)); Seen(4, 5);
            Assert.Equal(7, Invoke<int>(type, "outerCode", tracker, false)); Seen(3, 6);
            var outerFault = Assert.Throws<TargetInvocationException>(() => Method(type, "outerCode").Invoke(null, new object[] { tracker, true }));
            Assert.IsType<DivideByZeroException>(outerFault.InnerException); Seen(3, 6);

            var gate = Gate();
            var pending = Invoke<Task<int>>(type, "asyncCode", gate.Task, tracker);
            Assert.False(pending.IsCompleted); Seen();
            gate.SetResult(40);
            Assert.Equal(42, await pending.WaitAsync(TimeSpan.FromSeconds(10))); Seen(4, 5);
            gate = Gate(); pending = Invoke<Task<int>>(type, "asyncCode", gate.Task, tracker);
            gate.SetResult(-1);
            Assert.Equal(7, await pending.WaitAsync(TimeSpan.FromSeconds(10))); Seen();
            gate = Gate(); pending = Invoke<Task<int>>(type, "asyncCode", gate.Task, tracker);
            var expected = new InvalidOperationException("acquisition failed");
            gate.SetException(expected);
            Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(async () => await pending)); Seen();
            gate = Gate(); pending = Invoke<Task<int>>(type, "asyncCode", gate.Task, tracker);
            gate.SetCanceled();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending); Seen();

            gate = Gate(); pending = Invoke<Task<int>>(type, "asyncFunctionCode", gate.Task, tracker);
            Assert.False(pending.IsCompleted); Seen();
            gate.SetResult(40);
            Assert.Equal(42, await pending.WaitAsync(TimeSpan.FromSeconds(10))); Seen(4, 5);
            gate = Gate(); pending = Invoke<Task<int>>(type, "asyncFunctionCode", gate.Task, tracker);
            gate.SetResult(-1);
            Assert.Equal(7, await pending.WaitAsync(TimeSpan.FromSeconds(10))); Seen();

            gate = Gate(); pending = Invoke<Task<int>>(type, "asyncOuterCode", gate.Task, tracker, false);
            gate.SetResult(-1);
            Assert.Equal(7, await pending.WaitAsync(TimeSpan.FromSeconds(10))); Seen(6);
            gate = Gate(); pending = Invoke<Task<int>>(type, "asyncOuterCode", gate.Task, tracker, false);
            gate.SetCanceled();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending); Seen(6);
            gate = Gate(); pending = Invoke<Task<int>>(type, "asyncOuterCode", gate.Task, tracker, true);
            gate.SetCanceled();
            await Assert.ThrowsAsync<DivideByZeroException>(async () => await pending); Seen(6);

            foreach (var name in new[] { "open", "forward", "openFunctionAsync", "forwardFunctionAsync", "explicitOpen", "validated" })
                Assert.Null(type.GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy));
        });
        await CrossModuleDispatch();
        await ResolveShadowedCases();
        RejectOwnershipEscapes();
    }

    private static async Task CrossModuleDispatch()
    {
        var sources = new[]
        {
            new SourceFile("lib.ds", """
                module Lib
                resource Cursor(value: int) {
                    read(): int { value }
                    release(): Unit { () }
                }
                interface Store {
                    open(value: int): Result<Cursor, string>
                    openDefault(value: int): Result<Cursor, string> { open(value) }
                    async openAsync(input: Task<int>): Result<Cursor, string>
                }
                service MemoryStore: Store {
                    open(value) { if value >= 0 { Success(Cursor(value)) } else { Error("missing") } }
                    openAsync(input) {
                        let value = await input
                        if value >= 0 { Success(Cursor(value)) } else { Error("missing") }
                    }
                }
                fn factory(value: int): Result<Cursor, string> {
                    if value >= 0 { Success(Cursor(value)) } else { Error("missing") }
                }
                """),
            new SourceFile("app.ds", """
                module App
                import Lib
                fn open(value: int): int { 999 }
                fn fromDefault(): Result<int, string> {
                    let store: Lib.Store = Lib.MemoryStore()
                    using cursor = store.openDefault(40)?
                    Success(cursor.read() + 2)
                }
                fn fromQualified(): Result<int, string> {
                    using cursor = Lib.factory(40)? { Success(cursor.read() + 2) }
                }
                fn answer(): int {
                    match fromDefault() {
                        Success(value) => value
                        Error(_) => 0
                    }
                }
                fn qualified(): int {
                    match fromQualified() {
                        Success(value) => value
                        Error(_) => 0
                    }
                }
                async fn laterResult(input: Task<int>): Result<int, string> {
                    let store: Lib.Store = Lib.MemoryStore()
                    using cursor = (await store.openAsync(input))?
                    Success(cursor.read() + 2)
                }
                async fn later(input: Task<int>): int {
                    match await laterResult(input) {
                        Success(value) => value
                        Error(_) => 0
                    }
                }
                """)
        };
        var compiled = DSharpCompiler.Compile(sources);
        Assert.True(compiled.CSharp is not null, string.Join("\n", compiled.Diagnostics));
        Assert.Empty(compiled.Diagnostics);
        await WithGenerated(compiled.CSharp!, "App", async type =>
        {
            Assert.Equal(42, Invoke<int>(type, "answer"));
            Assert.Equal(42, Invoke<int>(type, "qualified"));
            var gate = Gate();
            var pending = Invoke<Task<int>>(type, "later", gate.Task);
            Assert.False(pending.IsCompleted);
            gate.SetResult(40);
            Assert.Equal(42, await pending.WaitAsync(TimeSpan.FromSeconds(10)));
        });

        // Distinct resources of the same source name must not be conflated.
        var mismatch = DSharpCompiler.Compile(new[]
        {
            new SourceFile("one.ds", "module One\nresource Cursor { release(): Unit { () } }\nfn open(): Result<Cursor, string> { Success(Cursor()) }"),
            new SourceFile("two.ds", "module Two\nresource Cursor { release(): Unit { () } }"),
            new SourceFile("bad.ds", "module Bad\nimport One\nimport Two\nfn bad(): Result<Two.Cursor, string> { One.open() }")
        });
        Assert.Null(mismatch.CSharp);
        Assert.NotEmpty(mismatch.Diagnostics);
        Assert.Contains(mismatch.Diagnostics, diagnostic => diagnostic.Path == "bad.ds");
    }

    private static async Task ResolveShadowedCases()
    {
        var sources = new[]
        {
            new SourceFile("shadow.ds", "module Shadow\ntype Outcome { | Success(int)\n | Error(int) }"),
            new SourceFile("app.ds", """
                module App
                import Shadow
                resource Cursor { read(): int { 40 }
                    release(): Unit { () } }
                interface Store {
                    Success(): Result<Cursor, string>
                    open(): Result<Cursor, string> { Success() }
                }
                service Memory: Store {
                    Success() { Result.Success<Cursor, string>(Cursor()) }
                }
                fn load(): Result<int, string> {
                    let store: Store = Memory()
                    using cursor = store.open()?
                    Result.Success<int, string>(cursor.read() + 2)
                }
                fn answer(): int {
                    match load() {
                        Result.Success(value) => value
                        Result.Error(_) => 0
                    }
                }
                """)
        };
        var compiled = DSharpCompiler.Compile(sources);
        Assert.True(compiled.CSharp is not null, string.Join("\n", compiled.Diagnostics));
        Assert.Empty(compiled.Diagnostics);
        await WithGenerated(compiled.CSharp!, "App", type =>
        {
            Assert.Equal(42, Invoke<int>(type, "answer"));
            return Task.CompletedTask;
        });

        var shadowed = DSharpCompiler.Compile(new[]
        {
            sources[0],
            new SourceFile("bad.ds", "module Bad\nimport Shadow\nresource Cursor { release(): Unit { () } }\nfn bad(): Result<Cursor, string> { Success(Cursor()) }")
        });
        Assert.Null(shadowed.CSharp);
        Assert.NotEmpty(shadowed.Diagnostics);
    }

    private static void RejectOwnershipEscapes()
    {
        const string declarations = """
            resource Cursor { read(): int { 42 }
                release(): Unit { () } }
            resource Other { release(): Unit { () } }
            fn make(): Cursor { Cursor() }
            fn open(): Result<Cursor, string> { Success(Cursor()) }
            async fn openAsync(input: Task<int>): Result<Cursor, string> {
                let ignored = await input
                Success(Cursor())
            }
            interface Store {
                open(): Result<Cursor, string>
                async later(input: Task<int>): Result<Cursor, string>
            }
            service Good: Store {
                open() { Success(Cursor()) }
                later(input) { let ignored = await input
                    Success(Cursor()) }
            }
            fn id<T>(value: T): T { value }
            """;
        var invalid = new[]
        {
            // An owned result must be consumed directly by ? in using or forwarded.
            "fn bad(): Unit { let item = open()\n () }",
            "fn bad(): Unit { let item = open()?\n () }",
            "fn bad(): Unit { let factory = open\n () }",
            "fn bad(): Unit { let x = Some(open())\n () }",
            "fn bad(): Unit { let x = (open(), 1)\n () }",
            "fn bad(): Unit { let x = id(open())\n () }",
            "fn bad(): Unit { let x = () => open()\n () }",
            "fn bad(): int { using item = open() { item.read() } }",
            "fn bad(): Result<int, string> { let item = open()?\n Success(item.read()) }",
            "fn bad(): Result<int, bool> { using item = open()?\n Success(item.read()) }",
            "fn bad(): Result<int, string> { using item = id(open())?\n Success(item.read()) }",
            "fn bad(): Result<int, string> { match open() { Success(_) => Success(0)\n Error(_) => Error(\"missing\") } }",
            // Async acquisition must await a producer directly before propagation.
            "async fn bad(input: Task<int>): Unit { let x = openAsync(input)\n () }",
            "async fn bad(input: Task<int>): Unit { let x = await openAsync(input)\n () }",
            "async fn bad(input: Task<int>): Result<int, string> { using item = openAsync(input)?\n Success(item.read()) }",
            "async fn bad(input: Task<int>): Result<int, string> { using item = (await id(openAsync(input)))?\n Success(item.read()) }",
            // Methods cannot be retained, partially applied or escape through data.
            "fn bad(store: Store): Unit { let factory = store.open\n () }",
            "fn bad(store: Store): Unit { let item = store.open()\n () }",
            "fn bad(store: Store): Unit { let item = Some(store.open())\n () }",
            "fn bad(store: Store): Unit { let item = id(store.open())\n () }",
            "async fn bad(store: Store, input: Task<int>): Unit { let x = store.later(input)\n () }",
            "async fn bad(store: Store, input: Task<int>): Unit { let x = await store.later(input)\n () }",
            // Result<Resource,E> remains an affine transfer, never ordinary data.
            "fn bad(value: Result<Cursor, string>): Unit { () }",
            "type Bad { value: Result<Cursor, string> }",
            "type Bad { | Case(Result<Cursor, string>) }",
            "fn bad(): Result<Cursor, string>? { null }",
            "fn bad(): Result<Option<Cursor>, string> { Error(\"no\") }",
            "fn bad(): Result<Cursor, Cursor> { Error(Cursor()) }",
            "fn bad(): Result<Cursor, (int) -> int> { Success(Cursor()) }",
            "fn bad<T>(value: T): Result<Cursor, string> { Success(Cursor()) }",
            "fn bad(): Unit { let item = Success(Cursor())\n () }",
            // An ordinary producer cannot receive or wrap an owned resource.
            "fn consume(value: Cursor): int { value.read() }\nfn bad(): int { consume(make()) }",
            "fn bad(): Result<Cursor, string> { Success(id(make())) }",
            "fn bad(): Result<Cursor, string> { Success(open()) }",
            // Cleanup during a producer return could abandon its pending resource.
            "fn bad(): Result<Cursor, string> { using other = Other()\n Success(Cursor()) }",
            "fn bad(): Result<Cursor, string> { using other = Other() { Success(Cursor()) } }",
            "fn bad(flag: bool): Result<Cursor, string> { if flag { using other = Other()\n return Success(Cursor()) } else { Error(\"missing\") } }",
            "interface Bad { open(): Result<Cursor, string> }\nservice Invalid: Bad { open() { using other = Other()\n Success(Cursor()) } }",
            "interface Bad { open(): Result<Cursor, string> { using other = Other()\n Success(Cursor()) } }"
        };
        var accepted = new List<string>();
        foreach (var body in invalid)
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + declarations + "\n" + body, "invalid-owned-result.ds");
            if (result.CSharp is not null) accepted.Add(body);
            else
            {
                Assert.NotEmpty(result.Diagnostics);
                Assert.All(result.Diagnostics, diagnostic => Assert.Equal("invalid-owned-result.ds", diagnostic.Path));
            }
        }
        Assert.True(accepted.Count == 0, "Invalid owned-result programs compiled:\n" + string.Join("\n", accepted));

        // A source-defined Result is not the compiler's standard Result.
        var shadowResult = DSharpCompiler.Compile("""
            module Shadow
            resource Cursor { release(): Unit { () } }
            type Result<T, E> { | Wrapped(T) }
            fn bad(): Result<Cursor, string> { Wrapped(Cursor()) }
            """, "shadow-result.ds");
        Assert.Null(shadowResult.CSharp);
        Assert.NotEmpty(shadowResult.Diagnostics);

        // A same-named source function must not become an implicit standard case.
        var shadowSuccess = DSharpCompiler.Compile("""
            module Shadow
            resource Cursor { release(): Unit { () } }
            fn Success(value: int): int { value }
            fn bad(): Result<Cursor, string> { Success(Cursor()) }
            """, "shadow-success.ds");
        Assert.Null(shadowSuccess.CSharp);
        Assert.NotEmpty(shadowSuccess.Diagnostics);
    }

    public class TrackerProxy : DispatchProxy
    {
        public Func<int, int> Mark { get; set; } = _ => 0;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? arguments) => targetMethod!.Name switch
        {
            "__dsharp_method_mark" => Mark((int)arguments![0]!),
            _ => throw new InvalidOperationException(targetMethod.Name)
        };
    }

    private static TaskCompletionSource<int> Gate() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static MethodInfo Method(Type type, string name) => type.GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
    private static T Invoke<T>(Type type, string name, params object?[] arguments) => (T)Method(type, name).Invoke(null, arguments)!;

    private static async Task WithGenerated(string csharp, string module, Func<Type, Task> verify)
    {
        var assemblyName = "DSharpOwnedResults_" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-owned-results-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "Generated.cs"), csharp);
            File.WriteAllText(Path.Combine(directory, "Generated.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net11.0</TargetFramework><AssemblyName>" + assemblyName + "</AssemblyName><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>");
            File.WriteAllText(Path.Combine(directory, "global.json"), "{\"sdk\":{\"version\":\"11.0.100\",\"rollForward\":\"disable\"}}");
            File.WriteAllText(Path.Combine(directory, "NuGet.Config"), "<configuration><packageSources><clear/></packageSources></configuration>");
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "build", "Generated.csproj", "--nologo", "--verbosity", "quiet" }) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated owned-result build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("owned-results-" + Guid.NewGuid(), isCollectible: false);
            var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0", assemblyName + ".dll"));
            var type = Assert.Single(assembly.GetTypes(), candidate => candidate.Name == module);
            await verify(type);
        }
        finally { Directory.Delete(directory, true); }
    }
}
