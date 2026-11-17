using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class CollectionMethodPipeTests
{
    public async Task CollectionMethodPipesExecuteAndRejectInvalidPrograms()
    {
        const string source = """
            module Demo
            type Box { value: int }
            fn plusOne(value: int): int { value + 1 }
            fn sum(total: int, value: int): int { total + value }
            fn generic<T>(xs: List<T?>, index: int): Option<T?> { index |> xs.get }
            fn count(values: List<int>): int { values.length() }
            fn make(mark: (int)->int): List<int> {
                let ignored = mark(2)
                [40,42]
            }
            fn ordered(mark: (int)->int): Option<int> {
                (mark(1)) |> make(mark).get
            }
            fn list(values: List<int>): Option<int> { 1 |> values.get }
            fn append(values: List<int>): List<int> { 3 |> values.append }
            fn filter(values: List<int>): List<int> { ((value:int) => value > 1) |> values.filter }
            fn map(values: List<int>): List<int> { plusOne |> values.map }
            fn fold(values: List<int>): int { sum |> (0 |> values.fold) }
            fn add(values: Set<int>): Set<int> { 42 |> values.add }
            fn contains(values: Set<int>): bool { 42 |> values.contains }
            fn lookup(values: Map<int,string?>): Option<string?> { 1 |> values.get }
            fn lookupNull(values: Map<string?,int>): Option<int> { null |> values.get }
            fn update(values: Map<int,string?>): Map<int,string?> {
                let withKey: (string?)->Map<int,string?> = 1 |> values.set
                withKey(null)
            }
            fn updateFactory(mark: (int)->int): (string?)->Map<int,string?> {
                let values = Map.empty<int,string?>()
                mark(1) |> values.set
            }
            fn mapEvents(mark: (int)->int): List<int> { mark |> [1,2].map }
            async fn asyncValues(gate: Task<int>): List<int> {
                let first = await gate
                [first,42]
            }
            async fn fromAsync(gate: Task<int>): int {
                let found = 1 |> (await asyncValues(gate)).get
                match found
                {
                    Some(value) => value
                    None => 0
                }
            }
            fn answer(): bool {
                let values = [1,2]
                let empty = Map.empty<int,string?>()
                let updated = update(empty)
                let nullKey = Map.empty<string?,int>().set(null,42)
                let foldNull: ((string?,int)->string?)->string? = null |> [1].fold
                let set = add(Set.empty<int>())
                list([40,42]) == Some(42) && append(values) == [1,2,3] && values == [1,2] &&
                filter([1,2,3]) == [2,3] && map(values) == [2,3] && fold([19,23]) == 42 &&
                contains(set) && !contains(Set.empty<int>()) && lookup(updated) == Some(null) && lookup(empty) == None &&
                lookupNull(nullKey) == Some(42) && lookupNull(Map.empty<string?,int>()) == None &&
                foldNull((text:string?, value:int) => "ready") == "ready" &&
                generic<int>([null,42],0) == Some(null) && generic<int?>([null,42],1) == Some(42) &&
                generic<int?>([],0) == None
            }
            fn matchAnswer(): int {
                let found = 1 |> [40,42].get
                match found
                {
                    Some(value) => value
                    None => 0
                }
            }
            """;
        var compilation = DSharpCompiler.Compile(source, "collection-method-pipe.ds");
        Assert.True(compilation.CSharp is not null, string.Join("\n", compilation.Diagnostics));
        Assert.Empty(compilation.Diagnostics);
        await WithGenerated(compilation.CSharp!, "Demo", type =>
        {
            Assert.Equal(true, Call(type, "answer"));
            Assert.Equal(42, Call(type, "matchAnswer"));
            var events = new List<int>();
            var ordered = Method(type, "ordered");
            var mark = AdaptInt(ordered.GetParameters()[0].ParameterType, n => { events.Add(n); return n; });
            Assert.NotNull(ordered.Invoke(null, new object[] { mark }));
            Assert.Equal(new[] { 1, 2 }, events);
            events.Clear();
            var factory = Method(type, "updateFactory");
            var factoryMark = AdaptInt(factory.GetParameters()[0].ParameterType, n => { events.Add(n); return n; });
            var update = Assert.IsAssignableFrom<Delegate>(factory.Invoke(null, new object[] { factoryMark }));
            Assert.Equal(new[] { 1 }, events);
            Assert.NotNull(update.DynamicInvoke(new object?[] { null }));
            Assert.NotNull(update.DynamicInvoke(new object?[] { "Ada" }));
            Assert.Equal(new[] { 1 }, events);
            events.Clear();
            var mapEvents = Method(type, "mapEvents");
            var mapMark = AdaptInt(mapEvents.GetParameters()[0].ParameterType, n => { events.Add(n); return n; });
            Assert.NotNull(mapEvents.Invoke(null, new object[] { mapMark }));
            Assert.Equal(new[] { 1, 2 }, events);
            var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = Assert.IsAssignableFrom<Task<int>>(Method(type, "fromAsync").Invoke(null, new object[] { gate.Task }));
            Assert.False(pending.IsCompleted);
            gate.SetResult(1);
            Assert.True(pending.Wait(TimeSpan.FromSeconds(10)));
            Assert.Equal(42, pending.Result);
        });
        await Imports();
        RejectInvalid();
    }

    private static async Task Imports()
    {
        var files = new[]
        {
            new SourceFile("library.ds", "module Library\nfn plusOne(value:int): int { value + 1 }"),
            new SourceFile("app.ds", "module Demo\nimport Library\nfn answer(): List<int> { plusOne |> [1,2].map }")
        };
        var result = DSharpCompiler.Compile(files);
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        await WithGenerated(result.CSharp!, "Demo", type => Assert.NotNull(Call(type, "answer")));
    }

    private static void RejectInvalid()
    {
        string[] invalid =
        [
            "fn bad(values: List<int>): Unit { let n = 1 |> values.length\n () }",
            "fn bad(values: List<int>?): Unit { let n = 0 |> values.get\n () }",
            "fn bad(values: List<int>): Unit { let n = \"wrong\" |> values.get\n () }",
            "fn bad(values: List<int>): Unit { let f = values.get\n () }",
            "extend int { fn add(self, value:int): int { self + value } }\nfn bad(): int { 2 |> 40.add }",
            "fn bad(): Unit { let builder = MutableList<int>()\n let f = 1 |> builder.add\n () }",
            "interface A { get(value: int): int }\nservice S: A { get(value) { value } }\nfn bad(s:A): int { 1 |> s.get }",
            "resource Cursor { get(value:int): int { value }\n release(): Unit { () } }\nfn bad(): Unit { using c = Cursor() { let f = 1 |> c.get\n () } }",
            "interface A { get(): int }\nfn bad(value:A): Unit { let f = value |> [1].fold\n () }",
            "interface A { get(): int }\nfn make<T>(value:T): ((T,int)->T)->T { value |> [1].fold }\nfn bad(value:A): Unit { let f = make<A>(value)\n () }",
            "type List { value: int }\nfn bad(xs: List): Unit { let x = 1 |> xs.get\n () }",
            "type Box { get: int }\nfn bad(xs: Box): Unit { let x = 1 |> xs.get\n () }"
        ];
        foreach (var body in invalid)
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + body, "collection-method-pipe-invalid.ds");
            Assert.True(result.CSharp is null, "Expected rejection: " + body);
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, d => Assert.Equal("collection-method-pipe-invalid.ds", d.Path));
        }
        string[] indirect =
        [
            "fn bad(xs: List<int>, cb: (int)->bool) { cb |> xs.filter }",
            "fn bad(xs: List<int>, cb: (int)->int) { cb |> xs.map }",
            "fn bad(xs: List<int>, cb: (int,int)->int) { cb |> (0 |> xs.fold) }",
            "fn step(x:int) { bad() }\nfn bad() { step |> [1].map }",
            "fn bad() { step |> [1].map }\nfn step(x:int) { bad() }"
        ];
        foreach (var body in indirect)
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + body, "collection-method-pipe-indirect.ds");
            Assert.Null(result.CSharp);
            Assert.Contains(result.Diagnostics, d => d.Code == "DS2027");
        }
    }

    private static MethodInfo Method(Type type, string method) => type.GetMethod(method,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
    private static object? Call(Type type, string method) => Method(type, method).Invoke(null, null);
    private static Delegate AdaptInt(Type delegateType, Func<int,int> callback)
    {
        var value = Expression.Parameter(typeof(int), "value");
        return Expression.Lambda(delegateType, Expression.Invoke(Expression.Constant(callback), value), value).Compile();
    }

    private static async Task WithGenerated(string csharp, string module, Action<Type> verify)
    {
        var assemblyName = "DSharpCollectionPipe_" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-collection-pipe-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "Generated.cs"), csharp);
            await File.WriteAllTextAsync(Path.Combine(directory, "Generated.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net11.0</TargetFramework><AssemblyName>" + assemblyName + "</AssemblyName><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>");
            await File.WriteAllTextAsync(Path.Combine(directory, "global.json"), "{\"sdk\":{\"version\":\"11.0.100\",\"rollForward\":\"disable\"}}");
            await File.WriteAllTextAsync(Path.Combine(directory, "NuGet.Config"), "<configuration><packageSources><clear/></packageSources></configuration>");
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "build", "Generated.csproj", "--nologo", "--verbosity", "quiet" }) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated collection method pipe build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("collection-pipe-" + Guid.NewGuid(), isCollectible: false);
            var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0", assemblyName + ".dll"));
            verify(Assert.Single(assembly.GetTypes(), candidate => candidate.Name == module));
        }
        finally { Directory.Delete(directory, true); }
    }
}
