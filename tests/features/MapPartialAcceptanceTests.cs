using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class MapPartialAcceptanceTests
{
    // Invoked from the established feature aggregator to keep CI reports bounded.
    public async Task MapPartialsCaptureAndExecute()
    {
        const string source = """
            module Demo
            type Error { | Failed }
            fn make(mark: (int) -> int): Map<int,int> {
                let ignored = mark(1)
                Map.empty<int,int>().set(9, 90)
            }
            extend Map<int,int> {
                fn set(self, key: int): int { 999 }
                fn set(self, key: int, text: string, other: int): int { 999 }
            }
            fn partial(mark: (int) -> int): (int) -> Map<int,int> {
                make(mark).set(mark(2))
            }
            fn read(value: Map<int,int>, key: int): int {
                match value.get(key) { None => -1
                                       Some(number) => number }
            }
            fn generic<T>(value: T): Map<string,T> {
                let setter = Map.empty<string,T>().set("x")
                setter(value)
            }
            fn genericNullable<T>(value: T?): Map<string,T?> {
                let setter = Map.empty<string,T?>().set("x")
                setter(value)
            }
            fn integer(value: Option<int>): int {
                match value { None => -1
                              Some(number) => number }
            }
            fn partialResult(): bool {
                let original = Map.empty<string,int>()
                let put = original.set("score")
                let first = put(42)
                let second = 7 |> put
                let grouped = (original.set("x"))(42)
                original.count() == 0 && first.count() == 1 && second.count() == 1 &&
                first.get("score") == Some(42) && second.get("score") == Some(7) &&
                first.get("score") == Some(42) && grouped.get("x") == Some(42) &&
                generic<int>(42).get("x") == Some(42) &&
                genericNullable<int>(null).get("x") == Some(null) &&
                genericNullable<int>(42).get("x") == Some(42) &&
                original.set("full", 42).get("full") == Some(42)
            }
            fn piped(): Map<string,int> { 42 |> Map.empty<string,int>().set("answer") }
            fn pipedResult(): bool { piped().get("answer") == Some(42) }
            fn nullable(): bool {
                let empty = Map.empty<string?, string?>()
                let putNull = empty.set(null)
                let cleared = putNull(null)
                let replaced = putNull("Alice")
                empty.get(null) == None && cleared.get(null) == Some(null) &&
                replaced.get(null) == Some("Alice")
            }
            fn options(): bool {
                let empty = Map.empty<Option<string?>, Option<int?>>()
                let put = empty.set(Some(null))
                let first = put(None)
                let second = put(Some(null))
                empty.get(Some(null)) == None && first.get(Some(null)) == Some(None) &&
                second.get(Some(null)) == Some(Some(null))
            }
            fn precedence(): bool {
                let values = Map.empty<int,int>()
                let put: (int) -> Map<int,int> = values.set(2)
                put(42).get(2) == Some(42) && values.set(2, 7).get(2) == Some(7)
            }
            fn receiverExit(): int {
                let put = (if true { return 42 } else { Map.empty<int,int>() }).set(1)
                integer(put(2).get(1))
            }
            fn keyExit(): int {
                let put = Map.empty<int,int>().set(if true { return 42 } else { 1 })
                integer(put(2).get(1))
            }
            fn propagate(key: Result<int,Error>): Result<int,Error> {
                let put = Map.empty<int,int>().set(key?)
                Success(integer(put(42).get(1)))
            }
            fn propagateError(): bool {
                match propagate(Error(Failed)) { Error(Failed) => true
                                                 Success(value) => false }
            }
            async fn makeAsync(mark: (int) -> int, gate: Task<int>): Map<int,int> {
                let ignored = await gate
                make(mark)
            }
            async fn asyncReceiver(mark: (int) -> int, gate: Task<int>): int {
                let put = (await makeAsync(mark, gate)).set(mark(2))
                integer(put(42).get(2))
            }
            async fn asyncKey(mark: (int) -> int, gate: Task<int>): int {
                let put = make(mark).set(await gate)
                integer(put(42).get(2))
            }
            """;
        var result = DSharpCompiler.Compile(source, "map-partial.ds");
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        await WithGenerated(result.CSharp!, "Demo", async type =>
        {
            Assert.True(Call<bool>(type, "partialResult"));
            Assert.True(Call<bool>(type, "pipedResult"));
            Assert.True(Call<bool>(type, "nullable"));
            Assert.True(Call<bool>(type, "options"));
            Assert.True(Call<bool>(type, "precedence"));
            Assert.Equal(42, Call<int>(type, "receiverExit"));
            Assert.Equal(42, Call<int>(type, "keyExit"));
            Assert.True(Call<bool>(type, "propagateError"));
            var events = new List<int>();
            var factory = Method(type, "partial");
            var mark = Adapt(factory.GetParameters()[0].ParameterType, n => { events.Add(n); return n; });
            var put = Assert.IsAssignableFrom<Delegate>(factory.Invoke(null, new object[] { mark }));
            Assert.Equal(new[] { 1, 2 }, events);
            var first = put.DynamicInvoke(42)!;
            var second = put.DynamicInvoke(7)!;
            Assert.Equal(new[] { 1, 2 }, events);
            Assert.Equal(42, Call<int>(type, "read", first, 2));
            Assert.Equal(7, Call<int>(type, "read", second, 2));
            Assert.Equal(90, Call<int>(type, "read", first, 9));
            Assert.Equal(42, Call<int>(type, "read", first, 2));
            var fails = new List<int>();
            var failReceiver = Adapt(factory.GetParameters()[0].ParameterType, n => { fails.Add(n); throw new InvalidOperationException("receiver"); });
            var firstFailure = Assert.Throws<TargetInvocationException>(() => factory.Invoke(null, new object[] { failReceiver }));
            Assert.IsType<InvalidOperationException>(firstFailure.InnerException);
            Assert.Equal(new[] { 1 }, fails);
            fails.Clear();
            var failKey = Adapt(factory.GetParameters()[0].ParameterType, n => { fails.Add(n); if (n == 2) throw new InvalidOperationException("key"); return n; });
            var secondFailure = Assert.Throws<TargetInvocationException>(() => factory.Invoke(null, new object[] { failKey }));
            Assert.IsType<InvalidOperationException>(secondFailure.InnerException);
            Assert.Equal(new[] { 1, 2 }, fails);
            var asyncMethod = Method(type, "asyncKey");
            var asyncEvents = new List<int>();
            var asyncMark = Adapt(asyncMethod.GetParameters()[0].ParameterType, n => { asyncEvents.Add(n); return n; });
            var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = Call<Task<int>>(type, "asyncKey", asyncMark, gate.Task);
            Assert.Equal(new[] { 1 }, asyncEvents);
            Assert.False(pending.IsCompleted);
            gate.SetResult(2);
            Assert.Equal(42, await pending.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(new[] { 1 }, asyncEvents);
            var receiverEvents = new List<int>();
            var receiverMark = Adapt(Method(type, "asyncReceiver").GetParameters()[0].ParameterType, n => { receiverEvents.Add(n); return n; });
            var receiverGate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var receiverPending = Call<Task<int>>(type, "asyncReceiver", receiverMark, receiverGate.Task);
            Assert.Empty(receiverEvents);
            Assert.False(receiverPending.IsCompleted);
            receiverGate.SetResult(0);
            Assert.Equal(42, await receiverPending.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(new[] { 1, 2 }, receiverEvents);
            var failedGate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var failing = Call<Task<int>>(type, "asyncKey", asyncMark, failedGate.Task);
            var expected = new InvalidOperationException("key failure");
            failedGate.SetException(expected);
            Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(async () => await failing));
        });
        RejectInvalidPrograms();
        await Imports();
    }

    private static void RejectInvalidPrograms()
    {
        string[] invalid =
        [
            "fn bad(): Unit { let p = Map.empty<int,int>().set(\"wrong\")\n () }",
            "fn bad(): Unit { let p = Map.empty<int,int>().set(1)\n let m = p(\"wrong\")\n () }",
            "fn bad(): Unit { let p = Map.empty<int,int>().set(1,2,3)\n () }",
            "fn bad(): Unit { Map.empty<int,int>().set(1)\n () }",
            "fn bad(): Unit { let b = MutableMap<int,int>()\n let p = b.set(1)\n () }",
            "interface A { get(): int }\nfn bad(a: A): Unit { let m = Map.empty<int,A>()\n let p = m.set(1)\n () }",
            "resource Cursor { read(): int { 42 }\n release(): Unit { () } }\nfn bad(): Unit { using c = Cursor() { let m = Map.empty<int, Cursor>()\n let p = m.set(1)\n () } }",
            "fn bad(): Unit { let m = Map.empty<int,MutableList<int>>()\n let p = m.set(1)\n () }",
            "interface A { get(): int }\nfn make<V>(m: Map<int,V>, key: int): (V) -> Map<int,V> { m.set(key) }\nfn bad(a: A): Unit { let m = Map.empty<int,A>()\n let p = make<A>(m, 1)\n () }",
            "resource Cursor { release(): Unit { () } }\nfn bad(): Unit { using c = Cursor() { let p = Map.empty<int,int>().set(c)\n () } }",
            "fn bad(): Unit { let b = MutableList<int>()\n let p = Map.empty<int,int>().set(b)\n () }",
            "interface A { get(): int }\nfn bad(a: A): Unit { let p = Map.empty<int,int>().set(a)\n () }",
            "interface A { set(value: int): int }\nfn inferred(a: A) { a.set(1) }"
        ];
        foreach (var declaration in invalid)
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + declaration, "invalid-map-partial.ds");
            Assert.True(result.CSharp is null, "Invalid map partial compiled: " + declaration);
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, diagnostic => Assert.StartsWith("DS2", diagnostic.Code));
        }
    }

    private static async Task Imports()
    {
        var sources = new[]
        {
            new SourceFile("lib.ds", "module Lib\nfn key(): string { \"answer\" }\nfn value(): int { 42 }"),
            new SourceFile("app.ds", "module Demo\nimport Lib\nfn answer(): bool { let put = Map.empty<string,int>().set(key())\n let result = value() |> put\n result.get(\"answer\") == Some(42) }")
        };
        var result = DSharpCompiler.Compile(sources);
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        await WithGenerated(result.CSharp!, "Demo", type => { Assert.True(Call<bool>(type, "answer")); return Task.CompletedTask; });
    }

    private static MethodInfo Method(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
    private static T Call<T>(Type type, string name, params object?[] args) => (T)Method(type, name).Invoke(null, args)!;
    private static Delegate Adapt(Type type, Func<int,int> action)
    {
        var value = Expression.Parameter(typeof(int), "value");
        return Expression.Lambda(type, Expression.Invoke(Expression.Constant(action), value), value).Compile();
    }
    private static async Task WithGenerated(string csharp, string module, Func<Type,Task> verify)
    {
        var assemblyName = "DSharpMapPartial_" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-map-partial-" + Guid.NewGuid());
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
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated map partial build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("map-partial-" + Guid.NewGuid(), isCollectible: false);
            var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0", assemblyName + ".dll"));
            var type = Assert.Single(assembly.GetTypes(), candidate => candidate.Name == module);
            await verify(type);
        }
        finally { Directory.Delete(directory, true); }
    }
}
