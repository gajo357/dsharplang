using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class OwnedResourceFactoryTests
{
    [Fact(DisplayName = "Owned resource factories transfer directly to using")]
    public async Task FactoriesTransferAndRejectEscapes()
    {
        const string source = """
            module Demo

            interface Tracker { mark(value: int): int }
            resource Cursor(value: int, tracker: Tracker) {
                read(): int {
                    let ignored = tracker.mark(2)
                    value
                }
                release(): Unit {
                    let ignored = tracker.mark(3)
                    ()
                }
            }

            fn open(value: int, tracker: Tracker): Cursor {
                Cursor(value, tracker)
            }
            fn forward(value: int, tracker: Tracker): Cursor {
                open(value, tracker)
            }
            fn choose(flag: bool, tracker: Tracker): Cursor {
                if flag { return forward(40, tracker) } else { Cursor(41, tracker) }
            }
            async fn openAsync(input: Task<int>, tracker: Tracker): Cursor {
                let value = await input
                Cursor(value, tracker)
            }
            async fn forwardAsync(input: Task<int>, tracker: Tracker): Cursor {
                await openAsync(input, tracker)
            }

            fn answer(tracker: Tracker): int {
                using cursor = forward(tracker.mark(1) + 39, tracker)
                cursor.read() + 2
            }
            fn braced(tracker: Tracker): int {
                using cursor = choose(true, tracker) { cursor.read() + 2 }
            }
            fn branch(tracker: Tracker): int {
                using cursor = choose(false, tracker)
                cursor.read() + 1
            }
            fn shadow(): int {
                let open: (int) -> int = (value: int) => value + 1
                open(41)
            }
            fn failBody(tracker: Tracker): int {
                using cursor = open(40, tracker)
                1 / 0
            }
            fn failArgument(tracker: Tracker): int {
                using cursor = open(1 / 0, tracker)
                cursor.read()
            }
            async fn asyncAnswer(input: Task<int>, tracker: Tracker): int {
                using cursor = await forwardAsync(input, tracker)
                cursor.read() + 2
            }
            async fn asyncBraced(input: Task<int>, tracker: Tracker): int {
                using cursor = await openAsync(input, tracker) { cursor.read() + 2 }
            }
            """;
        var result = DSharpCompiler.Compile(source, "owned-factories.ds");
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        await WithGenerated(result.CSharp!, "Demo", async type =>
        {
            var observed = new ConcurrentQueue<int>();
            var tracker = (TrackerProxy)DispatchProxy.Create(Method(type, "answer").GetParameters()[0].ParameterType, typeof(TrackerProxy));
            tracker.Mark = value => { observed.Enqueue(value); return value; };
            void Seen(params int[] values) { Assert.Equal(values, observed); observed.Clear(); }
            Assert.Equal(42, Invoke<int>(type, "answer", tracker)); Seen(1, 2, 3);
            Assert.Equal(42, Invoke<int>(type, "braced", tracker)); Seen(2, 3);
            Assert.Equal(42, Invoke<int>(type, "branch", tracker)); Seen(2, 3);
            Assert.Equal(42, Invoke<int>(type, "shadow"));
            var acquisition = Assert.Throws<TargetInvocationException>(() => Method(type, "failArgument").Invoke(null, new object[] { tracker }));
            Assert.IsType<DivideByZeroException>(acquisition.InnerException); Seen();
            tracker.Mark = value =>
            {
                observed.Enqueue(value);
                if (value == 3) throw new InvalidOperationException("cleanup failed");
                return value;
            };
            var cleanup = Assert.Throws<TargetInvocationException>(() => Method(type, "failBody").Invoke(null, new object[] { tracker }));
            Assert.Equal("cleanup failed", Assert.IsType<InvalidOperationException>(cleanup.InnerException).Message);
            Seen(3);
            tracker.Mark = value => { observed.Enqueue(value); return value; };

            // Ownership-producing functions must not be public CLR entry points.
            foreach (var name in new[] { "open", "forward", "choose", "openAsync", "forwardAsync" })
                Assert.Null(type.GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy));
            AssertNoPublicOwnedFactories(type.Assembly);

            foreach (var name in new[] { "asyncAnswer", "asyncBraced" })
            {
                var gate = Gate();
                var pending = Invoke<Task<int>>(type, name, gate.Task, tracker);
                Assert.False(pending.IsCompleted);
                Seen();
                gate.SetResult(40);
                Assert.Equal(42, await pending.WaitAsync(TimeSpan.FromSeconds(10))); Seen(2, 3);

                gate = Gate();
                pending = Invoke<Task<int>>(type, name, gate.Task, tracker);
                var failure = new InvalidOperationException("acquisition failed");
                gate.SetException(failure);
                Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(async () => await pending));
                Seen();

                gate = Gate();
                pending = Invoke<Task<int>>(type, name, gate.Task, tracker);
                gate.SetCanceled();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
                Seen();
            }
        });

        await CrossModuleForwarding();
        await ConstructorCaseNameCollisions();
        RejectOwnershipEscapes();
    }

    private static async Task CrossModuleForwarding()
    {
        var sources = new[]
        {
            new SourceFile("lib.ds", """
                module Lib
                resource Cursor(value: int) {
                    read(): int { value }
                    release(): Unit { () }
                }
                fn open(value: int): Cursor { Cursor(value) }
                fn forward(value: int): Cursor { return open(value) }
                fn adjust(value: int): int { value + 1 }
                async fn openAsync(input: Task<int>): Cursor {
                    let value = await input
                    Cursor(value)
                }
                """),
            new SourceFile("app.ds", """
                module App
                import Lib
                fn open(value: int): int { 999 }
                fn answer(): int {
                    using cursor = Lib.forward(40)
                    cursor.read() + 2
                }
                async fn asyncAnswer(input: Task<int>): int {
                    using cursor = await Lib.openAsync(input) { cursor.read() + 2 }
                }
                """)
        };
        var result = DSharpCompiler.Compile(sources);
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        await WithGenerated(result.CSharp!, "App", async type =>
        {
            Assert.Equal(42, Invoke<int>(type, "answer"));
            var lib = Assert.Single(type.Assembly.GetTypes(), candidate => candidate.Name == "Lib");
            Assert.Equal(42, Invoke<int>(lib, "adjust", 41));
            foreach (var name in new[] { "open", "forward", "openAsync" })
                Assert.Null(lib.GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy));
            AssertNoPublicOwnedFactories(type.Assembly);
            var input = Gate();
            var pending = Invoke<Task<int>>(type, "asyncAnswer", input.Task);
            Assert.False(pending.IsCompleted);
            input.SetResult(40);
            Assert.Equal(42, await pending.WaitAsync(TimeSpan.FromSeconds(10)));
        });
    }

    private static async Task ConstructorCaseNameCollisions()
    {
        const string source = """
            module Demo
            type Choice { | Ready | Other }
            resource Ready(value: int) {
                read(): int { value }
                release(): Unit { () }
            }
            resource Some(value: int) {
                read(): int { value }
                release(): Unit { () }
            }
            fn readyFactory(): Ready { Ready(40) }
            fn someFactory(): Some { Some(40) }
            fn readyDirect(): int { using value = Ready(40) { value.read() + 2 } }
            fn readyOwned(): int { using value = readyFactory() { value.read() + 2 } }
            fn someDirect(): int { using value = Some(40) { value.read() + 2 } }
            fn someOwned(): int { using value = someFactory() { value.read() + 2 } }
            """;
        var result = DSharpCompiler.Compile(source, "factory-name-collisions.ds");
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        await WithGenerated(result.CSharp!, "Demo", type =>
        {
            foreach (var name in new[] { "readyDirect", "readyOwned", "someDirect", "someOwned" })
                Assert.Equal(42, Invoke<int>(type, name));
            return Task.CompletedTask;
        });
    }

    private static void RejectOwnershipEscapes()
    {
        const string declarations = """
            resource Cursor { read(): int { 42 }
                release(): Unit { () } }
            resource Other { release(): Unit { () } }
            fn open(): Cursor { Cursor() }
            fn sized(value: int): Cursor { Cursor() }
            async fn openAsync(input: Task<int>): Cursor { let ignored = await input
                Cursor() }
            """;
        var invalid = new[]
        {
            // Unowned values cannot become locals, collections, arguments or returns.
            "fn bad(): Unit { let value = open()\n () }",
            "fn bad(): Unit { let value = Cursor()\n () }",
            "fn bad(): Unit { let value = Some(open())\n () }",
            "fn bad(): Unit { let value = (open(), 1)\n () }",
            "fn id<T>(value: T): T { value }\nfn bad(): Unit { let value = id(open())\n () }",
            "fn use(value: Cursor): int { value.read() }\nfn bad(): int { use(open()) }",
            "fn bad(): Unit { open()\n () }",
            "fn bad(): int { using value = open() |> id { value.read() } }\nfn id<T>(x: T): T { x }",
            // Factories and their pending tasks are not first-class values.
            "fn bad(): Unit { let factory = open\n () }",
            "fn bad(): Unit { let factory = sized()\n () }",
            "fn bad(): Unit { let factory = openAsync\n () }",
            "fn bad(): Unit { let factory = open()\n () }",
            "fn bad(): Unit { let factory: () -> Cursor = () => open()\n () }",
            "async fn bad(input: Task<int>): Unit { let pending = openAsync(input)\n () }",
            "async fn bad(input: Task<int>): Unit { let cursor = await openAsync(input)\n () }",
            "async fn bad(input: Task<int>): int { using item = openAsync(input) { item.read() } }",
            "fn id<T>(value: T): T { value }\nasync fn bad(input: Task<int>): int { using item = await id(openAsync(input)) { item.read() } }",
            "async fn bad(input: Task<int>): Unit { let callback = () => openAsync(input)\n () }",
            // Resource-typed parameters, generics and nullable/data wrappers remain forbidden.
            "fn bad(item: Cursor): Cursor { item }",
            "fn bad<T>(value: T): Cursor { Cursor() }",
            "fn bad(): Cursor? { null }",
            "fn bad(): Option<Cursor> { None }",
            "fn bad(): Result<Cursor, string> { Error(\"error\") }",
            // The newly created resource must not cross cleanup of an active scope.
            "fn bad(): Cursor { using other = Other()\n Cursor() }",
            "fn bad(): Cursor { using other = Other() { Cursor() } }",
            "fn bad(): Cursor { using other = Other()\n return Cursor() }",
            "fn bad(flag: bool): Cursor { if flag { using other = Other()\n return Cursor() } else { Cursor() } }",
            "fn bad(): Cursor { let value = using other = Other() { 1 }\n Cursor() }",
            "fn bad(): Cursor { using item = open()\n item }",
            "fn other(value: Cursor): Cursor { value }\nfn bad(): Cursor { other(open()) }"
        };
        var accepted = new List<string>();
        foreach (var body in invalid)
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + declarations + "\n" + body, "invalid-factory.ds");
            if (result.CSharp is not null) accepted.Add(body);
            else
            {
                Assert.NotEmpty(result.Diagnostics);
                Assert.All(result.Diagnostics, diagnostic => Assert.Equal("invalid-factory.ds", diagnostic.Path));
                Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code.StartsWith("DS1", StringComparison.Ordinal));
            }
        }
        Assert.True(accepted.Count == 0, "Invalid owned-factory programs compiled:\n" + string.Join("\n", accepted));
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

    private static void AssertNoPublicOwnedFactories(Assembly assembly)
    {
        foreach (var type in assembly.GetExportedTypes())
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            var result = method.ReturnType;
            if (result.IsGenericType && result.GetGenericTypeDefinition() == typeof(Task<>)) result = result.GetGenericArguments()[0];
            Assert.Null(result.GetMethod("__dsharp_method_release", BindingFlags.Public | BindingFlags.Instance));
        }
    }

    private static TaskCompletionSource<int> Gate() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static MethodInfo Method(Type type, string name) => type.GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
    private static T Invoke<T>(Type type, string name, params object?[] arguments) => (T)Method(type, name).Invoke(null, arguments)!;

    private static async Task WithGenerated(string csharp, string module, Func<Type, Task> verify)
    {
        var assemblyName = "DSharpOwnedFactory_" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-owned-factory-" + Guid.NewGuid());
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
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated factory build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("owned-factories-" + Guid.NewGuid(), isCollectible: false);
            var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0", assemblyName + ".dll"));
            var type = Assert.Single(assembly.GetTypes(), candidate => candidate.Name == module);
            await verify(type);
        }
        finally { Directory.Delete(directory, true); }
    }
}
