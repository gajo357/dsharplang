using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

public sealed class CI195
{
    [Fact]
    public async Task Run()
    {
        const string source = """
            module Demo
            type Customer { name: string? }
            type Wrapper<T> { items: List<T>? }
            extend Customer { fn label(self, fallback: string): string { self.name ?? fallback } }
            extend List<int> { fn length(self): int { 99 } }
            fn inc(value: int): int { value + 1 }
            fn total(acc: int, value: int): int { acc + value }
            fn count(xs: List<int>?) { xs?.length() ?? 0 }
            fn first(xs: List<int>?) { xs?.get(0) }
            fn filter(xs: List<int>?) { xs?.filter((n: int) => n > 1) }
            fn mapped(xs: List<int>?) { xs?.map<int>(inc) }
            fn folded(xs: List<int>?) { xs?.fold<int>(0, total) }
            fn chain(xs: List<int>?) { xs?.append(42)?.length() ?? 0 }
            fn has(xs: Set<int>?) { xs?.contains(42) }
            fn lookup(xs: Map<int, string?>?) { xs?.get(1) }
            fn generic<T>(xs: List<T>?) { xs?.get(0) }
            fn nested<T>(wrapper: Wrapper<T>?) { wrapper?.items?.get(0) }
            fn label(person: Customer?) { person?.label("Anonymous") ?? "None" }
            fn source(mark: (int)->int, present: bool): List<int>? {
                let ignored = mark(1)
                if present { [42] } else { null }
            }
            fn index(mark: (int)->int): int { mark(2) - 2 }
            fn tracked(mark: (int)->int, present: bool): Option<int>? { source(mark, present)?.get(index(mark)) }
            fn answer(): bool {
                let empty: List<int>? = null
                let words = Map.empty<int,string?>().set(1, null)
                let withNull: Wrapper<int?> = Wrapper { items = [null] }
                let noItems: Wrapper<int?> = Wrapper { items = [] }
                count(empty) == 0 && count([1,2]) == 2 &&
                first(empty) == null && first([42]) == Some(42) && first([]) == None &&
                filter([1,2,3]) == [2,3] && mapped([1,2]) == [2,3] && folded([19,23]) == 42 &&
                chain(empty) == 0 && chain([1]) == 2 &&
                has(Set.from([42])) == true && has(null) == null &&
                lookup(words) == Some(null) && lookup(Map.empty<int,string?>()) == None && lookup(null) == null &&
                generic<int?>([null]) == Some(null) && generic<int?>([]) == None && generic<int?>(null) == null &&
                nested<int?>(withNull) == Some(null) && nested<int?>(noItems) == None && nested<int?>(null) == null &&
                label(Customer { name = "Ada" }) == "Ada" && label(Customer { name = null }) == "Anonymous" && label(null) == "None"
            }
            """;
        var compiled = DSharpCompiler.Compile(source, "conditional-inference.ds");
        Assert.True(compiled.CSharp is not null, string.Join("\n", compiled.Diagnostics));
        Assert.Empty(compiled.Diagnostics);
        await WithGenerated(compiled.CSharp!, type =>
        {
            Assert.Equal(true, Method(type, "answer").Invoke(null, null));
            var events = new List<int>();
            var tracked = Method(type, "tracked");
            var argument = Expression.Parameter(typeof(int), "value");
            Func<int,int> mark = value => { events.Add(value); return value; };
            var callback = Expression.Lambda(tracked.GetParameters()[0].ParameterType, Expression.Invoke(Expression.Constant(mark), argument), argument).Compile();
            tracked.Invoke(null, new object[] { callback, true });
            Assert.Equal(new[] { 1, 2 }, events);
            events.Clear();
            tracked.Invoke(null, new object[] { callback, false });
            Assert.Equal(new[] { 1 }, events);
        });
        RejectUnprovenCalls();
        await ImportedExtensions();
    }

    private static void RejectUnprovenCalls()
    {
        string[] invalid =
        [
            "fn bad(xs: List<int>?, callback: (int)->bool) { xs?.filter(callback) }",
            "fn bad(xs: List<int>?, callback: (int)->int) { xs?.map<int>(callback) }",
            "fn bad(xs: List<int>?, callback: (int,int)->int) { xs?.fold<int>(0,callback) }",
            "fn step(value: int) { bad() ?? 0 }\nfn bad() { [1]?.fold<int>(0,(acc:int,value:int) => step(value)) }",
            "fn bad() { [1]?.fold<int>(0,(acc:int,value:int) => step(value)) }\nfn step(value: int) { bad() ?? 0 }",
            "extend int { fn twice(self): int { bad(null) } }\nfn bad(value: int?) { value?.twice() ?? 0 }",
            "fn bad(value: int?) { value?.twice() ?? 0 }\nextend int { fn twice(self): int { bad(null) } }",
            "interface A { get(): int }\nservice S: A { get() { 42 } }\nfn step(value: int): int { S().get() }\nfn bad(xs: List<int>?) { xs?.map<int>(step) }",
            "fn bad(xs: List<int>?) { xs?.map<int>(step) }\nfn step(value: int): int { S().get() }\ninterface A { get(): int }\nservice S: A { get() { 42 } }",
            "interface A { get(): int }\nservice S: A { get() { 42 } }\nfn bad(xs: List<int>?) { xs?.map<int>((value:int) => S().get()) }",
            "fn bad(xs: List<int>?) { xs?.map<int>((value:int) => S().get()) }\ninterface A { get(): int }\nservice S: A { get() { 42 } }",
            "resource Cursor { get(): int { 42 }\n release(): Unit { () } }\nfn step(value: int): int { using r = Cursor() { r.get() } }\nfn bad(xs: List<int>?) { xs?.map<int>(step) }",
            "resource Cursor { get(): int { 42 }\n release(): Unit { () } }\nfn bad(xs: List<int>?) { xs?.map<int>((value:int) => { using r = Cursor() { r.get() } }) }",
            "fn bad(xs: List<int>?) { xs?.map<int>(step) }\nfn step(value: int): int { using r = Cursor() { r.get() } }\nresource Cursor { get(): int { 42 }\n release(): Unit { () } }",
            "fn bad(xs: List<int>?) { xs?.map<int>((value:int) => { using r = Cursor() { r.get() } }) }\nresource Cursor { get(): int { 42 }\n release(): Unit { () } }",
            "interface A { get(): int }\nservice S: A { get() { 42 } }\nfn bad(xs: List<int>?) { xs?.map<int>((value:int) => { let s = S()\n s.get() }) }",
            "interface A { get(): int }\nservice S: A { get() { 42 } }\nfn bad(xs: List<int>?) { xs?.map<int>((value:int) => (if true { S() } else { S() }).get()) }",
            "interface A { get(): int }\nservice S: A { get() { 42 } }\nfn bad(xs: List<int>?) { xs?.map<int>((value:int) => { let s = if true { S() } else { S() }\n s.get() }) }",
            "interface A { get(): int }\nservice S: A { get() { 42 } }\nfn provide() { S() }\nfn cb(value:int): int { provide().get() }\nfn bad(xs: List<int>?) { xs?.map<int>(cb) }",
            "interface A { get(): int }\nservice S: A { get() { 42 } }\nfn id<T>(value:T): T { value }\nfn cb(value:int): int { let s = id(S())\n s.get() }\nfn bad(xs: List<int>?) { xs?.map<int>(cb) }",
            "interface A { get(): int }\nservice S: A { get() { 42 } }\nfn cb(value:int): int { let s = if true { let a = S()\n a } else { let b = S()\n b }\n s.get() }\nfn bad(xs: List<int>?) { xs?.map<int>(cb) }",
            "interface A { get(): int }\nfn bad(value: A) { value.get() }",
            "resource Cursor { get(): int { 42 }\n release(): Unit { () } }\nfn bad() { using r = Cursor() { r.get() } }",
            "fn bad(xs: List<int>?) { xs?.missing() }",
            "fn bad(xs: List<int>?) { xs?.append() }",
            "type List { value: int }\nfn bad(xs: List?) { xs?.length() }"
        ];
        for (var i = 0; i < invalid.Length; i++)
        {
            var body = invalid[i];
            var result = DSharpCompiler.Compile("module Invalid\n" + body, "conditional-inference-invalid.ds");
            Assert.True(result.CSharp is null, "Expected rejection: " + body);
            Assert.NotEmpty(result.Diagnostics);
            if (i < 21) Assert.Contains(result.Diagnostics, d => d.Code == "DS2027");
            Assert.All(result.Diagnostics, d => Assert.Equal("conditional-inference-invalid.ds", d.Path));
        }
    }

    private static async Task ImportedExtensions()
    {
        var files = new[]
        {
            new SourceFile("extensions.ds", "module Extensions\nextend int { fn plus(self, value: int): int { self + value } }"),
            new SourceFile("app.ds", "module Demo\nimport Extensions\nfn add(value: int?) { value?.plus(2) ?? 0 }\nfn answer(): int { add(40) }")
        };
        var compiled = DSharpCompiler.Compile(files);
        Assert.True(compiled.CSharp is not null, string.Join("\n", compiled.Diagnostics));
        Assert.Empty(compiled.Diagnostics);
        await WithGenerated(compiled.CSharp!, type => Assert.Equal(42, Method(type, "answer").Invoke(null, null)));
        var hidden = DSharpCompiler.Compile(new[] { files[0], new SourceFile("hidden.ds", "module Demo\nfn bad(value: int?) { value?.plus(1) }") });
        Assert.Null(hidden.CSharp);
        Assert.NotEmpty(hidden.Diagnostics);
        var ambiguous = DSharpCompiler.Compile(new[] { files[0],
            new SourceFile("other.ds", "module Other\nextend int { fn plus(self, value: int): int { self + value } }"),
            new SourceFile("ambiguous.ds", "module Demo\nimport Extensions\nimport Other\nfn bad(value: int?) { value?.plus(1) }") });
        Assert.Null(ambiguous.CSharp);
        Assert.NotEmpty(ambiguous.Diagnostics);
    }

    private static MethodInfo Method(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;

    private static async Task WithGenerated(string csharp, Action<Type> verify)
    {
        var assemblyName = "DSharpConditionalInference_" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-cond-infer-" + Guid.NewGuid());
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
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated conditional inference build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("conditional-inference-" + Guid.NewGuid(), isCollectible: false);
            var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0", assemblyName + ".dll"));
            verify(Assert.Single(assembly.GetTypes(), type => type.Name == "Demo"));
        }
        finally { Directory.Delete(directory, true); }
    }
}
