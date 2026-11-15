using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class OwnedServiceFactoryTests
{
    public async Task ServiceFactoriesTransferAndRejectEscapes()
    {
        const string source = """
            module Demo

            interface Tracker { mark(value: int): int }
            resource Cursor(value: int, tracker: Tracker) {
                read(): int {
                    let ignored = tracker.mark(4)
                    value
                }
                release(): Unit {
                    let ignored = tracker.mark(5)
                    ()
                }
            }
            interface Store {
                open(value: int): Cursor
                openDefault(value: int): Cursor { open(value) }
                async openAsync(input: Task<int>): Cursor
                async openAgain(input: Task<int>): Cursor { await openAsync(input) }
            }
            interface StoreAlias { open(value: int): Cursor }
            interface Forwarder { create(store: Store, value: int): Cursor }
            service Relay: Forwarder {
                create(store, value) { store.open(value) }
            }
            service Offset(amount: int, tracker: Tracker): Store, StoreAlias {
                open(value) {
                    let ignored = tracker.mark(3)
                    Cursor(value + amount, tracker)
                }
                openAsync(input) {
                    let value = await input
                    Cursor(value + amount, tracker)
                }
            }
            service Override(tracker: Tracker): Store {
                open(value) { Cursor(value, tracker) }
                openDefault(value) { Cursor(value + 1, tracker) }
                openAsync(input) { Cursor(await input, tracker) }
            }
            fn forwarded(store: Store, value: int): Cursor { store.open(value) }
            fn getStore(tracker: Tracker): Store {
                let ignored = tracker.mark(1)
                Offset(0, tracker)
            }
            fn order(tracker: Tracker): int {
                using cursor = getStore(tracker).open(tracker.mark(2) + 38)
                cursor.read() + 2
            }
            fn direct(tracker: Tracker): int {
                using cursor = Offset(0, tracker).open(40) { cursor.read() + 2 }
            }
            fn inherited(tracker: Tracker): int {
                let store: Store = Offset(0, tracker)
                using cursor = store.openDefault(40)
                cursor.read() + 2
            }
            fn overridden(tracker: Tracker): int {
                let store: Store = Override(tracker)
                using cursor = store.openDefault(39)
                cursor.read() + 2
            }
            fn forwardedFunction(tracker: Tracker): int {
                let store: Store = Offset(0, tracker)
                using cursor = forwarded(store, 40)
                cursor.read() + 2
            }
            fn forwardedMethod(tracker: Tracker): int {
                let store: Store = Offset(0, tracker)
                let relay: Forwarder = Relay()
                using cursor = relay.create(store, 40)
                cursor.read() + 2
            }
            fn alias(tracker: Tracker): int {
                let store: StoreAlias = Offset(0, tracker)
                using cursor = store.open(40)
                cursor.read() + 2
            }
            fn failArgument(tracker: Tracker): int {
                using cursor = getStore(tracker).open(1 / 0)
                cursor.read()
            }
            async fn asyncAnswer(input: Task<int>, tracker: Tracker): int {
                let store: Store = Offset(0, tracker)
                using cursor = await store.openAsync(input)
                cursor.read() + 2
            }
            async fn asyncDefault(input: Task<int>, tracker: Tracker): int {
                let store: Store = Offset(0, tracker)
                using cursor = await store.openAgain(input)
                cursor.read() + 2
            }
            async fn asyncConcrete(input: Task<int>, tracker: Tracker): int {
                using cursor = await Offset(0, tracker).openAsync(input) { cursor.read() + 2 }
            }
            """;
        var compiled = DSharpCompiler.Compile(source, "owned-service-factories.ds");
        Assert.True(compiled.CSharp is not null, string.Join("\n", compiled.Diagnostics));
        Assert.Empty(compiled.Diagnostics);
        await WithGenerated(compiled.CSharp!, "Demo", async type =>
        {
            var observed = new ConcurrentQueue<int>();
            var tracker = (TrackerProxy)DispatchProxy.Create(Method(type, "order").GetParameters()[0].ParameterType, typeof(TrackerProxy));
            tracker.Mark = value => { observed.Enqueue(value); return value; };
            void Seen(params int[] values) { Assert.Equal(values, observed); observed.Clear(); }

            Assert.Equal(42, Invoke<int>(type, "order", tracker)); Seen(1, 2, 3, 4, 5);
            Assert.Equal(42, Invoke<int>(type, "direct", tracker)); Seen(3, 4, 5);
            Assert.Equal(42, Invoke<int>(type, "inherited", tracker)); Seen(3, 4, 5);
            Assert.Equal(42, Invoke<int>(type, "overridden", tracker)); Seen(4, 5);
            Assert.Equal(42, Invoke<int>(type, "forwardedFunction", tracker)); Seen(3, 4, 5);
            Assert.Equal(42, Invoke<int>(type, "forwardedMethod", tracker)); Seen(3, 4, 5);
            Assert.Equal(42, Invoke<int>(type, "alias", tracker)); Seen(3, 4, 5);
            AssertOwnedMethodsAreNotPublic(type.Assembly);
            AssertNoPublicStaticOwnedFactories(type.Assembly);
            Assert.Null(type.GetMethod("forwarded", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy));
            var failure = Assert.Throws<TargetInvocationException>(() => Method(type, "failArgument").Invoke(null, new object[] { tracker }));
            Assert.IsType<DivideByZeroException>(failure.InnerException); Seen(1);

            foreach (var name in new[] { "asyncAnswer", "asyncConcrete", "asyncDefault" })
            {
                var gate = Gate();
                var pending = Invoke<Task<int>>(type, name, gate.Task, tracker);
                Assert.False(pending.IsCompleted); Seen();
                gate.SetResult(40);
                Assert.Equal(42, await pending.WaitAsync(TimeSpan.FromSeconds(10))); Seen(4, 5);

                gate = Gate(); pending = Invoke<Task<int>>(type, name, gate.Task, tracker);
                var expected = new InvalidOperationException("acquisition fault");
                gate.SetException(expected);
                Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(async () => await pending)); Seen();

                gate = Gate(); pending = Invoke<Task<int>>(type, name, gate.Task, tracker);
                gate.SetCanceled();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending); Seen();
            }
        });

        await CrossModuleDispatch();
        RejectOwnershipEscapes();
    }

    private static async Task CrossModuleDispatch()
    {
        var sources = new[]
        {
            new SourceFile("lib.ds", """
                module Lib
                resource Cursor(value: int) { read(): int { value }
                    release(): Unit { () } }
                interface Store {
                    open(value: int): Cursor
                    defaultOpen(value: int): Cursor { open(value) }
                    async later(input: Task<int>): Cursor
                }
                service First: Store {
                    open(value) { Cursor(value) }
                    later(input) { Cursor(await input) }
                }
                service Second: Store {
                    open(value) { Cursor(value + 1) }
                    defaultOpen(value) { Cursor(value + 2) }
                    later(input) { Cursor(await input) }
                }
                fn checked(store: Store, value: int): Cursor { store.open(value) }
                """),
            new SourceFile("app.ds", """
                module App
                import Lib
                fn answer(): int {
                    let first: Store = First()
                    let second: Store = Second()
                    using one = first.defaultOpen(40)
                    using two = second.defaultOpen(38)
                    using three = Lib.checked(first, 40)
                    if one.read() == 40 && two.read() == 40 && three.read() == 40 { 42 } else { 0 }
                }
                async fn later(input: Task<int>): int {
                    let first: Store = First()
                    using item = await first.later(input) { item.read() + 2 }
                }
                """)
        };
        var compiled = DSharpCompiler.Compile(sources);
        Assert.True(compiled.CSharp is not null, string.Join("\n", compiled.Diagnostics));
        Assert.Empty(compiled.Diagnostics);
        await WithGenerated(compiled.CSharp!, "App", async type =>
        {
            Assert.Equal(42, Invoke<int>(type, "answer"));
            AssertOwnedMethodsAreNotPublic(type.Assembly);
            AssertNoPublicStaticOwnedFactories(type.Assembly);
            var gate = Gate();
            var pending = Invoke<Task<int>>(type, "later", gate.Task);
            Assert.False(pending.IsCompleted);
            gate.SetResult(40);
            Assert.Equal(42, await pending.WaitAsync(TimeSpan.FromSeconds(10)));
        });
    }

    private static void RejectOwnershipEscapes()
    {
        const string declarations = """
            resource Cursor(value: int) { read(): int { value }
                release(): Unit { () } }
            resource Other { release(): Unit { () } }
            interface Store {
                open(value: int): Cursor
                async later(input: Task<int>): Cursor
            }
            service Good: Store {
                open(value) { Cursor(value) }
                later(input) { Cursor(await input) }
            }
            fn id<T>(value: T): T { value }
            """;
        var invalid = new[]
        {
            // An owned method result and its method cannot be first-class values.
            "fn bad(store: Store): Unit { let value = store.open(40)\n () }",
            "fn bad(store: Store): Unit { let factory = store.open\n () }",
            "fn bad(store: Store): Unit { let factory = id(store.open)\n () }",
            "fn bad(store: Store): Unit { let factory = store.open()\n () }",
            "fn bad(store: Store): Unit { let value = Some(store.open(40))\n () }",
            "fn bad(store: Store): Unit { let value = (store.open(40), 1)\n () }",
            "fn bad(store: Store): Unit { let value = id(store.open(40))\n () }",
            "fn bad(store: Store): Unit { let f = () => store.open(40)\n () }",
            "fn bad(store: Store): int { using value = id(store.open(40)) { value.read() } }",
            // Async factory tasks may only be awaited directly by a valid transfer.
            "async fn bad(store: Store, input: Task<int>): Unit { let value = store.later(input)\n () }",
            "async fn bad(store: Store, input: Task<int>): Unit { let value = await store.later(input)\n () }",
            "async fn bad(store: Store, input: Task<int>): int { using value = store.later(input) { value.read() } }",
            "async fn bad(store: Store, input: Task<int>): int { using value = await id(store.later(input)) { value.read() } }",
            // Interfaces and bodies cannot hide ownership in data or ordinary parameters.
            "interface Bad { consume(value: Cursor): int }",
            "interface Bad { get(): Option<Cursor> }",
            "interface Bad { get(): Task<Cursor> }",
            "interface Bad { get(): Cursor? }",
            "interface Bad { get(): Cursor }\nservice Wrong: Bad { get() { 42 } }",
            "interface Bad { get(): Cursor }\nservice Wrong: Bad { get() { using other = Other()\n Cursor(40) } }",
            "interface Bad { get(): Cursor { using other = Other()\n Cursor(40) } }",
            "interface Bad { make(): Cursor\n get(): Cursor { let pending = make()\n Cursor(40) } }",
            "interface Bad { make(): Cursor\n get(): Cursor { let pending = make\n Cursor(40) } }",
            "fn bad(store: Store): Cursor { using other = Other()\n store.open(40) }",
            // A method cannot accept a producer through an ordinary argument.
            "fn consume(value: int): int { value }\nfn bad(store: Store): int { consume(store.open(40)) }"
        };
        var accepted = new List<string>();
        foreach (var body in invalid)
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + declarations + "\n" + body, "invalid-owned-service.ds");
            if (result.CSharp is not null) accepted.Add(body);
            else
            {
                Assert.NotEmpty(result.Diagnostics);
                Assert.All(result.Diagnostics, diagnostic => Assert.Equal("invalid-owned-service.ds", diagnostic.Path));
                Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code.StartsWith("DS1", StringComparison.Ordinal));
            }
        }
        Assert.True(accepted.Count == 0, "Invalid owned-service programs compiled:\n" + string.Join("\n", accepted));
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

    private static void AssertOwnedMethodsAreNotPublic(Assembly assembly)
    {
        var names = new[] { "__dsharp_method_open", "__dsharp_method_openDefault",
            "__dsharp_method_openAsync", "__dsharp_method_openAgain", "__dsharp_method_create",
            "__dsharp_method_defaultOpen", "__dsharp_method_later" };
        var methods = assembly.GetExportedTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            .Where(method => names.Any(name => method.Name.EndsWith(name, StringComparison.Ordinal))).ToArray();
        Assert.NotEmpty(methods);
        Assert.Contains(methods, method => method.IsAssembly);
        Assert.All(methods, method => Assert.False(method.IsPublic, method.ToString()));
    }

    private static void AssertNoPublicStaticOwnedFactories(Assembly assembly)
    {
        foreach (var type in assembly.GetExportedTypes())
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            var result = method.ReturnType;
            if (result.IsGenericType && result.GetGenericTypeDefinition() == typeof(Task<>))
                result = result.GetGenericArguments()[0];
            Assert.Null(result.GetMethod("__dsharp_method_release", BindingFlags.Public | BindingFlags.Instance));
        }
    }

    private static TaskCompletionSource<int> Gate() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static MethodInfo Method(Type type, string name) => type.GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
    private static T Invoke<T>(Type type, string name, params object?[] args) => (T)Method(type, name).Invoke(null, args)!;

    private static async Task WithGenerated(string csharp, string module, Func<Type, Task> verify)
    {
        var assemblyName = "DSharpOwnedServices_" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-owned-services-" + Guid.NewGuid());
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
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated service-factory build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("owned-services-" + Guid.NewGuid(), isCollectible: false);
            var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0", assemblyName + ".dll"));
            var type = Assert.Single(assembly.GetTypes(), candidate => candidate.Name == module);
            await verify(type);
        }
        finally { Directory.Delete(directory, true); }
    }
}
