using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

public sealed class G209
{
    [Fact]
    public async Task Run()
    {
        const string source = """
            module Demo

            type Invoice { value: int }
            type Choice { | Present(int) | Missing }

            fn make(mark: (int)->int): Invoice {
                let ignored = mark(1)
                Invoice { value = 40 }
            }

            extend Invoice {
                fn add(self, fee: int): int { self.value + fee }
                fn sum(self, first: int, second: int): int { self.value + first + second }
                fn read(self): int { self.value + 2 }
                fn apply(self, callback: (int)->int): int { callback(self.value) }
                fn choose(self, value: int, second: int): int { self.value + value + second }
                fn choose(self, value: int, second: string): int { self.value + value + 1 }
                fn fail(self, first: int, second: int): int { 1 / 0 }
                fn fun(self): (int)->int { (value: int) => self.value + value }
                fn fun(self, value: int): int { 1000 + value }
                async fn watch(self, mark: (int)->int, gate: Task<int>): int {
                    let ignored = mark(2)
                    self.value + await gate
                }
            }

            extend string? {
                fn orElse(self, fallback: string): string { self ?? fallback }
            }

            fn inc(value: int): int { value + 1 }
            fn total(acc: int, value: int): int { acc + value }
            fn genericFirst<T>(values: List<T?>): Option<T?> { (values.get)(0) }
            fn order(mark: (int)->int): int { (make(mark).sum)(mark(2), mark(3)) }
            fn delayed(): (int)->int { (Invoice { value = 40 }.fail)(1) }
            fn wait(mark: (int)->int, gate: Task<int>): Task<int> { (make(mark).watch)(mark, gate) }
            fn earlyArg(): int { (Invoice { value = 40 }.sum)(if true { return 42 } else { 1 }, 2) }
            fn earlyReceiver(): int { ((if true { return 42 } else { Invoice { value = 40 } }).add)(2) }

            fn grouped(xs: List<int>): bool {
                let invoice = Invoice { value = 40 }
                let keyed = ((Map.empty<int,string?>().set)(1))(null)
                let values = (Set.empty<int>().add)(42)
                let missing: string? = null
                let present: string? = "Ada"
                let get: Option<int> = (xs.get)(1)
                let map: List<int> = (xs.map)((x: int) => x + 1)
                let fold: int = ((xs.fold)(0))(total)
                let mapper: ((int)->int)->List<int> = (xs.map)()
                let folder: (int,(int,int)->int)->int = (xs.fold)()
                let absent: Option<string?> = (keyed.get)(2)
                let presentNull: Option<string?> = (keyed.get)(1)
                let decision: Choice = Choice.Present(42)
                let options: List<Option<int?>> = [None, Some(null), Some(42)]
                (invoice.add)(2) == 42 && (invoice.read)() == 42 &&
                ((invoice.sum)(1))(1) == 42 && (invoice.choose)(1, 1) == 42 &&
                (invoice.choose)(1, "x") == 42 && (invoice.apply)(x => x + 2) == 42 &&
                (invoice.fun)(2) == 1002 && (invoice.fun)()(2) == 42 &&
                (missing.orElse)("Unknown") == "Unknown" && (present.orElse)("Unknown") == "Ada" &&
                (xs.length)() == 2 && get == Some(42) && map == [41,43] && fold == 82 &&
                mapper(inc) == [41,43] && folder(0,total) == 82 &&
                (values.contains)(42) && (values.count)() == 1 && (keyed.count)() == 1 &&
                absent == None && presentNull == Some(null) && decision == Choice.Present(42) &&
                (options.get)(0) == Some(None) && (options.get)(1) == Some(Some(null)) &&
                (options.get)(2) == Some(Some(42)) && genericFirst<int?>([null]) == Some(null) &&
                earlyArg() == 42 && earlyReceiver() == 42
            }

            fn groupedAnswer(): bool { grouped([40,42]) }

            fn answer(): int {
                let result = ([40,42].get)(1)
                match result
                {
                    Some(value) => value
                    None => 0
                }
            }
            """;
        var compilation = DSharpCompiler.Compile(source, "grouped-method-values.ds");
        Assert.True(compilation.CSharp is not null, string.Join("\n", compilation.Diagnostics));
        Assert.Empty(compilation.Diagnostics);
        await WithGenerated(compilation.CSharp!, "Demo", async type =>
        {
            Assert.Equal(42, Method(type, "answer").Invoke(null, null));
            Assert.Equal(true, Method(type, "groupedAnswer").Invoke(null, null));
            var events = new List<int>();
            var order = Method(type, "order");
            var mark = Adapt(order.GetParameters()[0].ParameterType, n => { events.Add(n); return n; });
            Assert.Equal(45, order.Invoke(null, new object[] { mark }));
            Assert.Equal(new[] { 1, 2, 3 }, events);
            var deferred = Assert.IsAssignableFrom<Delegate>(Method(type, "delayed").Invoke(null, null));
            var thrown = Assert.Throws<TargetInvocationException>(() => deferred.DynamicInvoke(1));
            Assert.IsType<DivideByZeroException>(thrown.InnerException);
            events.Clear();
            var wait = Method(type, "wait");
            var asyncMark = Adapt(wait.GetParameters()[0].ParameterType, n => { events.Add(n); return n; });
            var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = Assert.IsAssignableFrom<Task<int>>(wait.Invoke(null, new object[] { asyncMark, gate.Task }));
            Assert.Equal(new[] { 1, 2 }, events);
            Assert.False(pending.IsCompleted);
            gate.SetResult(2);
            Assert.Equal(42, await pending.WaitAsync(TimeSpan.FromSeconds(10)));
        });
        await ImportsAndAmbiguity();
        RejectInvalid();
    }

    private static async Task ImportsAndAmbiguity()
    {
        var model = new SourceFile("model.ds", "module Model\ntype Invoice { value: int }");
        var pricing = new SourceFile("pricing.ds", "module Pricing\nimport Model\nextend Invoice { fn add(self, value:int): int { self.value + value } }");
        var app = new SourceFile("app.ds", "module App\nimport Model\nimport Pricing\nfn answer(): int { (Invoice { value = 40 }.add)(2) }");
        var compiled = DSharpCompiler.Compile(new[] { model, pricing, app });
        Assert.True(compiled.CSharp is not null, string.Join("\n", compiled.Diagnostics));
        await WithGenerated(compiled.CSharp!, "App", type =>
        {
            Assert.Equal(42, Method(type, "answer").Invoke(null, null));
            return Task.CompletedTask;
        });
        var hidden = new SourceFile("hidden.ds", "module App\nimport Model\nfn bad(): int { (Invoice { value = 40 }.add)(2) }");
        Assert.Null(DSharpCompiler.Compile(new[] { model, pricing, hidden }).CSharp);
        var other = new SourceFile("other.ds", "module Other\nimport Model\nextend Invoice { fn add(self, value:int): int { self.value + value + 1 } }");
        foreach (var imports in new[] { "import Pricing\nimport Other", "import Other\nimport Pricing" })
        {
            var ambiguous = new SourceFile("ambiguous.ds", "module App\nimport Model\n" + imports + "\nfn bad(): int { (Invoice { value = 40 }.add)(2) }");
            var result = DSharpCompiler.Compile(new[] { model, pricing, other, ambiguous });
            Assert.Null(result.CSharp);
            Assert.Contains(result.Diagnostics, d => d.Code == "DS2018");
        }
    }

    private static void RejectInvalid()
    {
        string[] invalid =
        [
            "fn bad(): Unit { let f = [1].get\n () }",
            "extend int { fn add(self, value:int): int { self + value } }\nfn bad(): int { true |> 40.add }",
            "extend int { fn add(self, value:int): int { self + value } }\nfn bad(): int { (40.add)(true) }",
            "extend int { fn choose(self, value:string?):int {1}\n fn choose(self, value:int?):int {2} }\nfn bad():int { (1.choose)(null) }",
            "extend int { fn apply(self, f:(int)->int):int {f(self)}\n fn apply(self, f:(bool)->int):int {f(true)} }\nfn bad():int { (40.apply)(x => 42) }",
            "type Item { add: int }\nextend Item { fn add(self, value:int):int {self.add + value} }\nfn bad(item:Item):int { (item.add)(2) }",
            "fn bad(items:List<int>?): Option<int> { (items.get)(0) }",
            "interface A { get():int }\nfn bad(a:A):int { (a.get)() }",
            "resource Cursor { read(value:int):int {value}\n release():Unit {()} }\nfn bad():int { using c = Cursor() { (c.read)(42) } }",
            "fn bad():Unit { let b = MutableList<int>()\n (b.add)(1)\n () }"
        ];
        foreach (var body in invalid)
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + body, "grouped-method-invalid.ds");
            Assert.True(result.CSharp is null, "Expected rejection: " + body);
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, d => Assert.Equal("grouped-method-invalid.ds", d.Path));
        }
    }

    private static MethodInfo Method(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
    private static Delegate Adapt(Type delegateType, Func<int,int> callback)
    {
        var value = Expression.Parameter(typeof(int), "value");
        return Expression.Lambda(delegateType, Expression.Invoke(Expression.Constant(callback), value), value).Compile();
    }

    private static async Task WithGenerated(string csharp, string module, Func<Type,Task> verify)
    {
        var assemblyName = "DSharpGroupedMethod_" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-grouped-method-" + Guid.NewGuid());
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
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated grouped method build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("grouped-method-" + Guid.NewGuid(), isCollectible: false);
            var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0", assemblyName + ".dll"));
            await verify(Assert.Single(assembly.GetTypes(), type => type.Name == module));
        }
        finally { Directory.Delete(directory, true); }
    }
}
