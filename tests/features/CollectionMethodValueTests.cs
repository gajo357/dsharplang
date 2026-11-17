using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

public sealed class C201
{
    [Fact]
    public async Task Run()
    {
        const string source = """
            module Demo
            type Entry { get: int }
            extend List<int> { fn length(self): int { 99 } }
            extend Map<int,string?> { fn count(self): int { 99 } }
            extend Set<int> { fn count(self): int { 99 } }
            fn make(mark: (int)->int): List<int> {
                let ignored = mark(1)
                [1,2,3]
            }
            fn appendFactory(mark: (int)->int): (int)->List<int> { make(mark).append }
            fn mapFactory(mark: (int)->int): ((int)->int)->List<int> { make(mark).map }
            fn length(xs: List<int>): int { xs.length() }
            fn plusOne(x: int): int { x + 1 }
            fn sum(a: int, b: int): int { a + b }
            fn useGet(get: (int)->Option<int>): Option<int> { get(1) }
            fn generic<T>(xs: List<T?>): (int)->Option<T?> { xs.get }
            async fn load(gate: Task<int>): List<int> { [await gate] }
            async fn fromAsync(gate: Task<int>): int {
                let append: (int)->List<int> = (await load(gate)).append
                append(42).length()
            }
            fn earlyReceiver(): int {
                let append: (int)->List<int> = (if true { return 42 } else { [1] }).append
                append(2).length()
            }
            fn answer(): bool {
                let xs = [1,2,3]
                let get: (int)->Option<int> = xs.get
                let append: (int)->List<int> = xs.append
                let len: ()->int = xs.length
                let filter: ((int)->bool)->List<int> = xs.filter
                let map: ((int)->int)->List<int> = xs.map
                let fold: (int,(int,int)->int)->int = xs.fold
                let table = Map.empty<int,string?>()
                let set: (int,string?)->Map<int,string?> = table.set
                let inserted = set(1,null)
                let mapGet: (int)->Option<string?> = inserted.get
                let contains: (int)->bool = inserted.contains
                let mapCount: ()->int = inserted.count
                let values = Set.from([1,2])
                let add: (int)->Set<int> = values.add
                let setContains: (int)->bool = values.contains
                let setCount: ()->int = values.count
                let nullable = generic<int>([null,42])
                let nullableAgain = generic<int?>([null,42])
                let options: List<Option<int?>> = [None,Some(null),Some(42)]
                let optionMap: ((Option<int?>)->Option<int?>)->List<Option<int?>> = options.map
                let mappedOptions = optionMap((value:Option<int?>) => value)
                let entry = Entry { get = 42 }
                len() == 3 && get(0) == Some(1) && get(8) == None &&
                append(4) == [1,2,3,4] && xs == [1,2,3] &&
                filter((x:int) => x > 1) == [2,3] && map(plusOne) == [2,3,4] &&
                fold(10,sum) == 16 && useGet(xs.get) == Some(2) &&
                mapCount() == 1 && mapGet(1) == Some(null) && mapGet(2) == None && contains(1) && !contains(2) &&
                setCount() == 2 && setContains(1) && !setContains(3) && add(3).contains(3) && !values.contains(3) &&
                nullable(0) == Some(null) && nullable(1) == Some(42) && nullable(8) == None &&
                nullableAgain(0) == Some(null) && mappedOptions.get(0) == Some(None) &&
                mappedOptions.get(1) == Some(Some(null)) && mappedOptions.get(2) == Some(Some(42)) && entry.get == 42
            }
            """;
        var compilation = DSharpCompiler.Compile(source, "collection-method-value.ds");
        Assert.True(compilation.CSharp is not null, string.Join("\n", compilation.Diagnostics));
        Assert.Empty(compilation.Diagnostics);
        await WithGenerated(compilation.CSharp!, "Demo", async type =>
        {
            Assert.Equal(true, Method(type, "answer").Invoke(null, null));
            Assert.Equal(42, Method(type, "earlyReceiver").Invoke(null, null));
            var events = new List<int>();
            var factory = Method(type, "appendFactory");
            var mark = AdaptInt(factory.GetParameters()[0].ParameterType, value => { events.Add(value); return value; });
            var append = Assert.IsAssignableFrom<Delegate>(factory.Invoke(null, new object[] { mark }));
            Assert.Equal(new[] { 1 }, events);
            var first = append.DynamicInvoke(4)!;
            var second = append.DynamicInvoke(5)!;
            Assert.Equal(4, Method(type, "length").Invoke(null, new[] { first }));
            Assert.Equal(4, Method(type, "length").Invoke(null, new[] { second }));
            Assert.Equal(new[] { 1 }, events);
            var mapFactory = Method(type, "mapFactory");
            var mapMark = AdaptInt(mapFactory.GetParameters()[0].ParameterType, value => { events.Add(value); return value; });
            var map = Assert.IsAssignableFrom<Delegate>(mapFactory.Invoke(null, new object[] { mapMark }));
            Assert.Equal(new[] { 1, 1 }, events);
            var callbackType = map.GetType().GetMethod("Invoke")!.GetParameters()[0].ParameterType;
            var callback = AdaptInt(callbackType, value => { events.Add(value + 1); return value + 1; });
            Assert.Equal(new[] { 1, 1 }, events);
            _ = map.DynamicInvoke(callback);
            Assert.Equal(new[] { 1, 1, 2, 3, 4 }, events);
            var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = (Task<int>)Method(type, "fromAsync").Invoke(null, new object[] { gate.Task })!;
            Assert.False(pending.IsCompleted);
            gate.SetResult(1);
            Assert.Equal(2, await pending.WaitAsync(TimeSpan.FromSeconds(10)));
        });
        await Imports();
        RejectInvalid();
        RejectInference();
    }

    private static async Task Imports()
    {
        var files = new[]
        {
            new SourceFile("library.ds", "module Library\nfn values(): List<int> { [40,42] }"),
            new SourceFile("app.ds", "module Demo\nimport Library\nfn answer(): bool { let get: (int)->Option<int> = values().get\n get(1) == Some(42) }")
        };
        var result = DSharpCompiler.Compile(files);
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        await WithGenerated(result.CSharp!, "Demo", type => { Assert.Equal(true, Method(type, "answer").Invoke(null, null)); return Task.CompletedTask; });
    }

    private static void RejectInvalid()
    {
        string[] invalid =
        [
            "fn bad(xs: List<int>): Unit { let value = xs.get\n () }",
            "fn bad(xs: List<int>): Unit { let value: (string)->Option<int> = xs.get\n () }",
            "fn bad(xs: List<int>?): Unit { let value: (int)->Option<int> = xs.get\n () }",
            "fn bad(): Unit { let b = MutableList<int>()\n let f: (int)->Unit = b.add\n () }",
            "interface A { get(): int }\nfn bad(a: A): Unit { let f: ()->int = a.get\n () }",
            "resource Cursor { read(value:int): int { 42 }\n release(): Unit { () } }\nfn bad(): Unit { using c = Cursor() { let f: (int)->int = c.read\n () } }",
            "extend List<int> { fn get(self, name:string): int { 42 } }\nfn bad(xs: List<int>): Unit { let f: (string)->int = xs.get\n () }",
            "type Item { get: int }\nfn bad(item: Item): Unit { let f: (int)->int = item.get\n () }",
            "fn bad(xs: List<int>): Unit { let map: ((string)->int)->List<int> = xs.map\n () }",
            "fn bad(xs: List<int>): Unit { let fold: (int,(int,string)->int)->int = xs.fold\n () }",
            "fn bad(xs: List<int>): Unit { let map: ((int)->Task<int>)->List<Task<int>> = xs.map\n () }",
            "fn bad(xs: List<int>): Unit { let map: ((int)->((int)->int))->List<(int)->int> = xs.map\n () }",
            "interface A { get(): int }\nfn bad(values: List<A>): Unit { let get: (int)->Option<A> = values.get\n () }",
            "interface A { get(): int }\nfn bad(value: A): Unit { let fold: (A,(A,int)->A)->A = [1].fold\n let pending = fold(value)\n () }",
            "interface A { get(): int }\nfn make<T>(xs: List<int>): (T,(T,int)->T)->T { xs.fold }\nfn bad(value: A): Unit { let fold = make<A>([1])\n let pending = fold(value)\n () }"
        ];
        foreach (var body in invalid)
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + body, "collection-method-invalid.ds");
            Assert.True(result.CSharp is null, "Expected rejection: " + body);
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, diagnostic => Assert.Equal("collection-method-invalid.ds", diagnostic.Path));
        }
    }

    private static void RejectInference()
    {
        const string callback = "fn bad(callback: (int)->int) { let map: ((int)->int)->List<int> = [1].map\n map(callback) }";
        const string step = "fn step(x:int): int { bad().length() }";
        const string recursive = "fn bad() { let map: ((int)->int)->List<int> = [1].map\n map(step) }";
        foreach (var body in new[] { callback, step + "\n" + recursive, recursive + "\n" + step })
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + body, "collection-method-inference.ds");
            Assert.Null(result.CSharp);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DS2027");
        }
    }

    private static MethodInfo Method(Type type, string name) => type.GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
    private static Delegate AdaptInt(Type delegateType, Func<int,int> callback)
    {
        var value = Expression.Parameter(typeof(int), "value");
        return Expression.Lambda(delegateType, Expression.Invoke(Expression.Constant(callback), value), value).Compile();
    }

    private static async Task WithGenerated(string csharp, string module, Func<Type,Task> verify)
    {
        var assemblyName = "DSharpCollectionValue_" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-collection-value-" + Guid.NewGuid());
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
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated collection method value build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("collection-method-value-" + Guid.NewGuid(), isCollectible: false);
            var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0", assemblyName + ".dll"));
            await verify(Assert.Single(assembly.GetTypes(), type => type.Name == module));
        }
        finally { Directory.Delete(directory, true); }
    }
}
