using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class BareCollectionPipeInferenceTests
{
    public async Task BareCollectionPipesInferAndRejectInvalidPrograms()
    {
        const string source = """
            module Demo

            type Box { values: List<int> }
            extend List<int> { fn get(self, value:string): int { trap() } }
            fn trap(): int {
                match first([40,42])
                {
                    Some(value) => value
                    None => 0
                }
            }
            fn increment(value: int): int { value + 1 }
            fn sum(total: int, value: int): int { total + value }
            fn index(): int { 1 }
            fn source(): List<int> { [40,42] }
            fn first(values: List<int>) { 1 |> values.get }
            fn literal() { 0 |> [42].get }
            fn fromCall() { index() |> source().get }
            fn fromField(box: Box) { 1 |> box.values.get }
            fn append(values: List<int>) { 42 |> values.append }
            fn appendNull(values: List<string?>) { null |> values.append }
            fn filtered(values: List<int>) { ((value:int) => value > 1) |> values.filter }
            fn mapped(values: List<int>) { increment |> values.map }
            fn mappedLambda(values: List<int>) { ((value:int) => value + 1) |> values.map }
            fn folded(values: List<int>) { sum |> (0 |> values.fold) }
            fn foldStage(values: List<int>) { 0 |> values.fold }
            fn add(values: Set<int>) { 42 |> values.add }
            fn contains(values: Set<int>) { 42 |> values.contains }
            fn lookup(values: Map<int,string?>) { 1 |> values.get }
            fn update(values: Map<int,string?>) { null |> (1 |> values.set) }
            fn updateStage(values: Map<int,string?>) { 1 |> values.set }
            fn generic<T>(values: List<T?>) { 0 |> values.get }
            fn options(values: List<Option<int?>>) { 0 |> values.get }
            fn measuredSource(mark: (int)->int): List<int> { let ignored = mark(2)
                [42] }
            fn measured(mark: (int)->int): Option<int> { (mark(1)-1) |> measuredSource(mark).get }
            fn answer(): bool {
                let values = [1,2]
                let map = Map.empty<int,string?>()
                let updated = update(map)
                let staged = updateStage(map)
                let set = add(Set.empty<int>())
                let box = Box { values = [40,42] }
                first([40,42]) == Some(42) && literal() == Some(42) && fromCall() == Some(42) &&
                fromField(box) == Some(42) && append(values) == [1,2,42] && values == [1,2] &&
                appendNull(["Ada"]) == ["Ada",null] &&
                filtered([1,2,3]) == [2,3] && mapped(values) == [2,3] && mappedLambda(values) == [2,3] &&
                folded([19,23]) == 42 && (foldStage([19,23]))(sum) == 42 &&
                contains(set) && !contains(Set.empty<int>()) && lookup(updated) == Some(null) &&
                lookup(map) == None && lookup(staged("Ada")) == Some("Ada") &&
                generic<int>([null]) == Some(null) && generic<int?>([42]) == Some(42) && generic<string?>([null]) == Some(null) &&
                generic<string?>(["Ada"]) == Some("Ada") && generic<string?>([]) == None &&
                options([None]) == Some(None) && options([Some(null)]) == Some(Some(null)) && options([]) == None
            }
            fn matchAnswer(): int {
                match literal()
                {
                    Some(value) => value
                    None => 0
                }
            }
            """;
        var compilation = DSharpCompiler.Compile(source, "bare-pipe-inference.ds");
        Assert.True(compilation.CSharp is not null, string.Join("\n", compilation.Diagnostics));
        Assert.Empty(compilation.Diagnostics);
        await WithGenerated(compilation.CSharp!, type =>
        {
            Assert.Equal(true, Call(type, "answer"));
            Assert.Equal(42, Call(type, "matchAnswer"));
            var events = new List<int>();
            var method = Method(type, "measured");
            var mark = AdaptInt(method.GetParameters()[0].ParameterType, n => { events.Add(n); return n; });
            Assert.NotNull(method.Invoke(null, new object[] { mark }));
            Assert.Equal(new[] { 1, 2 }, events);
        });
        await ImportedCallbacks();
        RejectUnproven();
    }

    private static async Task ImportedCallbacks()
    {
        var files = new[]
        {
            new SourceFile("math.ds", "module Math\nfn increment(value:int): int { value + 1 }\nfn sum(a:int,b:int): int { a + b }"),
            new SourceFile("app.ds", """
                module Demo
                import Math
                fn mapped(values: List<int>) { increment |> values.map }
                fn folded(values: List<int>) { sum |> (0 |> values.fold) }
                fn answer(): bool { mapped([1,2]) == [2,3] && folded([19,23]) == 42 }
                """)
        };
        var result = DSharpCompiler.Compile(files);
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        await WithGenerated(result.CSharp!, type => Assert.Equal(true, Call(type, "answer")));
    }

    private static void RejectUnproven()
    {
        string[] indirect =
        [
            "fn bad(xs: List<int>, cb: (int)->bool) { cb |> xs.filter }",
            "fn bad(xs: List<int>, cb: (int)->int) { cb |> xs.map }",
            "fn inc(value:int): int { value + 1 }\nfn bad(xs:List<int>, inc:(int)->int) { inc |> xs.map }",
            "fn bad(xs: List<int>, cb: (int,int)->int) { cb |> (0 |> xs.fold) }",
            "fn step(value:int) { bad() }\nfn bad() { step |> [1].map }",
            "fn bad() { step |> [1].map }\nfn step(value:int) { bad() }",
            "fn bad() { ((x:int) => bad()) |> [1].map }",
            "fn bad(xs:List<int>) { (((a:int,x:int) => a + x) |> (0 |> xs.fold))() }",
            "interface A { get(): int }\nservice S: A { get() { 42 } }\nfn step(value:int):int { S().get() }\nfn bad() { step |> [1].map }",
            "fn bad() { step |> [1].map }\nfn step(value:int):int { S().get() }\ninterface A { get(): int }\nservice S: A { get() { 42 } }",
            "resource Cursor { get(): int { 42 }\n release(): Unit { () } }\nfn step(value:int):int { using c = Cursor() { c.get() } }\nfn bad() { step |> [1].map }",
            "interface A { get(): int }\nservice S: A { get() { 42 } }\nfn provide() { S() }\nfn step(value:int): int { provide().get() }\nfn bad() { step |> [1].map }",
            "interface A { get(value:int): int }\nfn bad(a:A) { 1 |> a.get }",
            "resource Cursor { get(value:int): int { value }\n release(): Unit { () } }\nfn bad() { using c = Cursor() { 1 |> c.get } }"
        ];
        foreach (var body in indirect)
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + body, "bare-pipe-indirect.ds");
            Assert.Null(result.CSharp);
            Assert.Contains(result.Diagnostics, d => d.Code == "DS2027");
            Assert.All(result.Diagnostics, d => Assert.Equal("bare-pipe-indirect.ds", d.Path));
        }
        string[] invalid =
        [
            "fn bad(xs:List<int>) { 1 |> xs.length }",
            "fn bad(xs:List<int>) { \"bad\" |> xs.get }",
            "fn bad(xs:List<int>?) { 0 |> xs.get }",
            "type Box { get:int }\nfn bad(box:Box) { 1 |> box.get }",
            "type List { value:int }\nfn bad(xs:List) { 1 |> xs.get }",
            "interface A { get(): int }\nfn bad(value:A): Unit { let pending = value |> [1].fold\n () }",
            "interface A { get(): int }\nfn make<T>(value:T): ((T,int)->T)->T { value |> [1].fold }\nfn bad(value:A): Unit { let pending = make<A>(value)\n () }",
            "extend int { fn add(self, value:int):int { self + value } }\nfn bad():int { true |> 40.add }"
        ];
        foreach (var body in invalid)
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + body, "bare-pipe-invalid.ds");
            Assert.Null(result.CSharp);
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, d => Assert.Equal("bare-pipe-invalid.ds", d.Path));
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

    private static async Task WithGenerated(string csharp, Action<Type> verify)
    {
        var assemblyName = "DSharpBarePipeInference_" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-bare-pipe-" + Guid.NewGuid());
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
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated bare-pipe inference build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("bare-pipe-" + Guid.NewGuid(), isCollectible: false);
            var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0", assemblyName + ".dll"));
            verify(Assert.Single(assembly.GetTypes(), type => type.Name == "Demo"));
        }
        finally { Directory.Delete(directory, true); }
    }
}
