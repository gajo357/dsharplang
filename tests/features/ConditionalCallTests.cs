using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

public sealed class A
{
    [Fact(DisplayName = "x")]
    public async Task B()
    {
        const string source = """
            module Demo
            type Customer { name: string? }
            type Holder { customer: Customer? }
            extend Customer {
                fn display(self, fallback: string): string { self.name ?? fallback }
                fn nameOrNull(self, absent: bool): string? { if absent { null } else { self.name } }
            }
            extend List<int> { fn length(self): int { 99 } }
            extend int {
                fn add(self, first: int, second: int): int { self + first + second }
            }
            fn inc(x: int): int { x + 1 }
            fn length(values: List<int>?): int? { values?.length() }
            fn lookup(values: List<int>?): Option<int>? { values?.get(0) }
            fn mapped(values: List<int>?): List<int>? { values?.map<int>(inc) }
            fn generic<T>(values: List<T>?): Option<T>? { values?.get(0) }
            fn chained(values: List<int>?): int { values?.append(42)?.length() ?? 0 }
            fn display(value: Holder?): string { value?.customer?.display("anonymous") ?? "missing" }
            fn nullableResult(value: Customer?, absent: bool): string? { value?.nameOrNull(absent) }
            fn receiver(mark: (int)->int, present: bool): int? {
                let ignored = mark(1)
                if present { 40 } else { null }
            }
            fn order(mark: (int)->int, present: bool): int {
                receiver(mark, present)?.add(mark(2), mark(3)) ?? 0
            }
            fn skipFailure(): int { let missing: int? = null
                missing?.add(1 / 0, 1 / 0) ?? 42 }
            fn returns(): int { let missing: int? = null
                missing?.add(if true { return 1 } else { 2 }, 3) ?? 42 }
            fn propagate(values: List<int>?, index: Result<int,string>): Result<int,string> {
                let ignored = values?.get(index?)
                Success(42)
            }
            async fn asyncIndex(values: List<int>?, index: Task<int>): int {
                let result = values?.get(await index)
                if result == Some(42) { 42 } else { 0 }
            }
            async fn asyncMissing(index: Task<int>): int { await asyncIndex(null, index) }
            async fn asyncPresent(index: Task<int>): int { await asyncIndex([42], index) }
            fn answer(): bool {
                let present: Customer? = Customer { name = "Ada" }
                let named: Holder? = Holder { customer = present }
                let missing: Holder? = null
                let unnamed: Customer? = Customer { name = null }
                let noValues: List<int>? = null
                length(noValues) == null && length([42]) == 1 &&
                lookup(noValues) == null && lookup([42]) == Some(42) && lookup([]) == None &&
                generic<int?>([null]) == Some(null) && generic<int?>([]) == None && generic<int?>(null) == null &&
                mapped([1,2]) == [2,3] && mapped(null) == null &&
                chained(null) == 0 && chained([1]) == 2 &&
                display(named) == "Ada" && display(missing) == "missing" &&
                nullableResult(present, false) == "Ada" && nullableResult(present, true) == null &&
                nullableResult(unnamed, false) == null && nullableResult(null, false) == null &&
                skipFailure() == 42 && returns() == 42 &&
                propagate(null, Error("bad")) == Success(42) &&
                propagate([1], Error("bad")) == Error("bad") &&
                propagate([1], Success(0)) == Success(42)
            }
            """;
        var compiled = DSharpCompiler.Compile(source, "conditional-calls.ds");
        Assert.True(compiled.CSharp is not null, string.Join("\n", compiled.Diagnostics));
        Assert.Empty(compiled.Diagnostics);
        await WithGenerated(compiled.CSharp!, type =>
        {
            Assert.Equal(true, Method(type, "answer").Invoke(null, null));
            var events = new List<int>();
            var order = Method(type, "order");
            var callbackType = order.GetParameters()[0].ParameterType;
            var arg = Expression.Parameter(typeof(int), "value");
            Func<int,int> mark = value => { events.Add(value); return value; };
            var callback = Expression.Lambda(callbackType, Expression.Invoke(Expression.Constant(mark), arg), arg).Compile();
            Assert.Equal(45, order.Invoke(null, new object[] { callback, true }));
            Assert.Equal(new[] { 1, 2, 3 }, events);
            events.Clear();
            Assert.Equal(0, order.Invoke(null, new object[] { callback, false }));
            Assert.Equal(new[] { 1 }, events);
            var pending = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var missing = Assert.IsAssignableFrom<Task<int>>(Method(type, "asyncMissing").Invoke(null, new object[] { pending.Task }));
            Assert.True(missing.IsCompletedSuccessfully);
            Assert.Equal(0, missing.Result);
            var present = Assert.IsAssignableFrom<Task<int>>(Method(type, "asyncPresent").Invoke(null, new object[] { pending.Task }));
            Assert.False(present.IsCompleted);
            pending.SetResult(0);
            Assert.Equal(42, present.GetAwaiter().GetResult());
        });
        await ImportedExtension();
        InvalidCalls();
    }

    private static async Task ImportedExtension()
    {
        var files = new[]
        {
            new SourceFile("extensions.ds", "module Extensions\nextend int { fn plus(self, value: int): int { self + value } }"),
            new SourceFile("app.ds", "module Demo\nimport Extensions\nfn answer(): int { let value: int? = 40\n value?.plus(2) ?? 0 }")
        };
        var compiled = DSharpCompiler.Compile(files);
        Assert.True(compiled.CSharp is not null, string.Join("\n", compiled.Diagnostics));
        Assert.Empty(compiled.Diagnostics);
        await WithGenerated(compiled.CSharp!, type => Assert.Equal(42, Method(type, "answer").Invoke(null, null)));
        var hidden = DSharpCompiler.Compile(new[] { files[0], new SourceFile("hidden.ds", "module Demo\nfn bad(value: int?): int? { value?.plus(1) }") });
        Assert.Null(hidden.CSharp);
        Assert.NotEmpty(hidden.Diagnostics);
        var ambiguous = DSharpCompiler.Compile(new[] { files[0],
            new SourceFile("other.ds", "module Other\nextend int { fn plus(self, value: int): int { self + value } }"),
            new SourceFile("ambiguous.ds", "module Demo\nimport Extensions\nimport Other\nfn bad(value: int?): int? { value?.plus(1) }") });
        Assert.Null(ambiguous.CSharp);
        Assert.NotEmpty(ambiguous.Diagnostics);
    }

    private static void InvalidCalls()
    {
        string[] invalid =
        [
            "fn bad(value: List<int>?): int? { value?.missing() }",
            "fn bad(value: List<int>?): int? { value?.length(1) }",
            "fn bad(value: List<int>?): int { value?.length() }",
            "fn bad(value: List<int>?): int? { value.length() }",
            "fn bad(value: List<int>?): (int)->List<int> { value?.append() }",
            "fn bad(value: List<int>?): Option<int>? { value?.get(\"wrong\") }",
            "type Item { number: int }\nfn bad(value: Item?): int? { value?.number() }",
            "extend int { async fn work(self): int { 1 } }\nfn bad(value: int?): Task<int>? { value?.work() }"
        ];
        foreach (var body in invalid)
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + body, "invalid-conditional.ds");
            Assert.True(result.CSharp is null, "Expected rejection: " + body);
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, d => Assert.Equal("invalid-conditional.ds", d.Path));
        }
    }

    private static MethodInfo Method(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;

    private static async Task WithGenerated(string csharp, Action<Type> verify)
    {
        var assemblyName = "DSharpConditional_" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-conditional-" + Guid.NewGuid());
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
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated conditional call build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("conditional-" + Guid.NewGuid(), isCollectible: false);
            var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0", assemblyName + ".dll"));
            verify(Assert.Single(assembly.GetTypes(), type => type.Name == "Demo"));
        }
        finally { Directory.Delete(directory, true); }
    }
}
