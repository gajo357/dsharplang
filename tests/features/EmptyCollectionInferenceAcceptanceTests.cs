using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class EmptyCollectionInferenceAcceptanceTests
{
    // Run through the feature aggregate to keep the CI report bounded.
    public async Task EmptyCollectionPartialCallsInferSafely()
    {
        const string source = """
            module Demo
            fn plusOne(value: int): int { value + 1 }
            fn sum(acc: int, value: int): int { acc + value }
            fn listAppend() { 42 |> [1].append() }
            fn listGet() { ([41,42].get())(1) }
            fn listFilter() { ((x: int) => x > 1) |> [1,2,3].filter() }
            fn listMap() { plusOne |> [1,2].map<int>() }
            fn listMapContext() { ((x: int) => x + 1) |> [1,2].map() }
            fn listMapGrouped() { ([1,2].map<int>())(plusOne) }
            fn listFold() { ([19,23].fold<int>())(0, sum) }
            fn listFoldContext() { ([19,23].fold())(0, (a:int,b:int) => a + b) }
            fn listFoldStaged() { (0 |> [19,23].fold<int>())(sum) }
            fn listFoldNested() { sum |> (0 |> [19,23].fold<int>()) }
            fn listFoldGroupedStage() { (([19,23].fold<int>())(0))(sum) }
            fn listFoldStage() { 0 |> [19,23].fold<int>() }
            fn listParameter(xs: List<int>) { 0 |> xs.get() }
            fn listLocal() { let xs: List<int> = [42]
                0 |> xs.get() }
            fn mapSet() { (Map.empty<int,int>().set())(1,42) }
            fn mapSetStaged() { (1 |> Map.empty<int,int>().set())(42) }
            fn mapStage() { 1 |> Map.empty<int,int>().set() }
            fn mapGet(values: Map<int,int>) { 1 |> values.get() }
            fn mapContains(values: Map<int,int>) { (values.contains())(1) }
            fn setAdd() { 42 |> Set.empty<int>().add() }
            fn setFrom() { 42 |> Set.from([1]).add() }
            fn setContains(values: Set<int>) { (values.contains())(42) }
            fn nullable() { (Map.empty<int,Option<string?>>().set())(1, Some(null)) }
            fn optionList() { ((v: Option<int?>) => v) |> [None, Some(null), Some(42)].map<Option<int?>>() }
            fn answer(): bool {
                let map = mapSet()
                let nullableMap = nullable()
                let options = optionList()
                listAppend() == [1,42] && listGet() == Some(42) && listFilter() == [2,3] &&
                listMap() == [2,3] && listMapContext() == [2,3] && listMapGrouped() == [2,3] &&
                listFold() == 42 && listFoldContext() == 42 && listFoldStaged() == 42 &&
                listFoldNested() == 42 && listFoldGroupedStage() == 42 &&
                (listFoldStage())(sum) == 42 && listParameter([42]) == Some(42) && listLocal() == Some(42) &&
                map.get(1) == Some(42) && mapSetStaged().get(1) == Some(42) &&
                (mapStage())(42).get(1) == Some(42) && mapGet(map) == Some(42) && mapContains(map) &&
                setContains(setAdd()) && setContains(setFrom()) && nullableMap.get(1) == Some(Some(null)) && nullableMap.get(2) == None &&
                options.get(0) == Some(None) && options.get(1) == Some(Some(null)) && options.get(2) == Some(Some(42))
            }
            fn values(mark: (int)->int): List<int> { let ignored = mark(1)
                [2,3] }
            fn effects(mark: (int)->int): int {
                (values(mark).fold<int>())(mark(4), (acc:int, x:int) => acc + x)
            }
            fn factory(mark: (int)->int): ((int)->int)->List<int> { values(mark).map<int>() }
            fn length(values: List<int>): int { values.length() }
            """;
        var compilation = DSharpCompiler.Compile(source, "empty-inference.ds");
        Assert.True(compilation.CSharp is not null, string.Join("\n", compilation.Diagnostics));
        Assert.Empty(compilation.Diagnostics);
        await WithGenerated(compilation.CSharp!, "Demo", type =>
        {
            Assert.Equal(true, Call(type, "answer"));
            var events = new List<int>();
            var effects = Method(type, "effects");
            var mark = AdaptInt(effects.GetParameters()[0].ParameterType, n => { events.Add(n); return n; });
            Assert.Equal(9, effects.Invoke(null, new object[] { mark }));
            Assert.Equal(new[] { 1, 4 }, events);
            events.Clear();
            var factory = Method(type, "factory");
            var factoryMark = AdaptInt(factory.GetParameters()[0].ParameterType, n => { events.Add(n); return n; });
            var map = Assert.IsAssignableFrom<Delegate>(factory.Invoke(null, new object[] { factoryMark }));
            Assert.Equal(new[] { 1 }, events);
            var callbackType = map.GetType().GetMethod("Invoke")!.GetParameters()[0].ParameterType;
            var callback = AdaptInt(callbackType, n => { events.Add(n); return n + 1; });
            Assert.Equal(2, Method(type, "length").Invoke(null, new[] { map.DynamicInvoke(callback) }));
            Assert.Equal(new[] { 1, 2, 3 }, events);
        });
        RejectIndirectAndRecursive();
        await ImportedCallbacks();
        RejectImportedExtensionFallback();
    }

    private static void RejectIndirectAndRecursive()
    {
        string[] invalid =
        [
            "fn bad(callback: (int)->bool) { callback |> [1].filter() }",
            "fn bad(callback: (int)->int) { callback |> [1].map<int>() }",
            "fn bad(callback: (int,int)->int) { ([1].fold<int>())(0,callback) }",
            "fn bad(callback: (int,int)->int) { (0 |> [1].fold<int>())(callback) }",
            "interface A { get(): int }\nfn bad(value: A) { value.get() }",
            "resource Cursor { get(): int { 42 }\n release(): Unit { () } }\nfn bad() { using r = Cursor() { r.get() } }",
            "interface Map { get(): (int)->int }\nfn bad(value: Map) { 1 |> value.get() }",
            "interface A { get(): int }\nservice S: A { get() { bad() } }\nextend List<int> { fn get(self): (int)->int { (value:int) => S().get() } }\nfn bad() { 1 |> [1].get() }",
            "extend List<int> { fn get(self): (int)->int { (value:int) => bad() } }\nfn bad() { ([1].get())(1) }",
            "fn bad() { ([1].get())(1) }\nextend List<int> { fn get(self): (int)->int { (value:int) => bad() } }",
            "fn step(acc:int, value:int) { bad() }\nfn bad() { ([1].fold<int>())(0,step) }",
            "fn bad() { ([1].fold<int>())(0,step) }\nfn step(acc:int, value:int) { bad() }",
            "fn bad() { ([1].fold<int>())(0,(acc:int,value:int) => bad()) }"
        ];
        foreach (var body in invalid)
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + body, "empty-inference-invalid.ds");
            Assert.True(result.CSharp is null, "Expected rejection for " + body);
            Assert.True(result.Diagnostics.Any(d => d.Code == "DS2027"),
                "Expected indirect/cyclic inference diagnostic for " + body + ": " + string.Join("; ", result.Diagnostics));
            Assert.All(result.Diagnostics, d => Assert.Equal("empty-inference-invalid.ds", d.Path));
        }
        var shadow = DSharpCompiler.Compile("module Invalid\ntype Set<T> { value: T }\nfn bad() { 1 |> Set.empty<int>().add() }", "set-shadow.ds");
        Assert.Null(shadow.CSharp);
        Assert.NotEmpty(shadow.Diagnostics);
    }

    private static async Task ImportedCallbacks()
    {
        var files = new[]
        {
            new SourceFile("math.ds", "module Math\nfn sum(a:int, b:int): int { a + b }\nfn increment(a:int): int { a + 1 }"),
            new SourceFile("app.ds", """
                module Demo
                import Math
                fn mapped(values: List<int>) { increment |> values.map<int>() }
                fn folded(values: List<int>) { (values.fold<int>())(0, sum) }
                fn answer(): bool { mapped([1,2]) == [2,3] && folded([19,23]) == 42 }
                """)
        };
        var result = DSharpCompiler.Compile(files);
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        await WithGenerated(result.CSharp!, "Demo", type => Assert.Equal(true, Call(type, "answer")));
    }

    private static void RejectImportedExtensionFallback()
    {
        var files = new[]
        {
            new SourceFile("extensions.ds", """
                module Extensions
                interface A { get(): int }
                service S: A { get() { 42 } }
                extend List<int> { fn get(self): (int)->int { (value:int) => S().get() } }
                """),
            new SourceFile("app.ds", "module Demo\nimport Extensions\nfn bad() { 1 |> [1].get() }")
        };
        var result = DSharpCompiler.Compile(files);
        Assert.True(result.CSharp is null, "An imported full extension returning a callable must not be treated as an intrinsic partial.");
        Assert.Contains(result.Diagnostics, d => d.Code == "DS2027");
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
        var assemblyName = "DSharpEmptyInference_" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-empty-inference-" + Guid.NewGuid());
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
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated empty inference build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("empty-inference-" + Guid.NewGuid(), isCollectible: false);
            var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0", assemblyName + ".dll"));
            verify(Assert.Single(assembly.GetTypes(), candidate => candidate.Name == module));
        }
        finally { Directory.Delete(directory, true); }
    }
}
