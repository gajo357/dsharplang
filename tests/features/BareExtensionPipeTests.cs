using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

public sealed class BareExtensionPipeTests
{
    public async Task PipesExecuteWithOrderAndDeferredWork()
    {
        await ExecutePipes();
        await VisibilityAmbiguityAndSafety();
    }

    private static async Task ExecutePipes()
    {
        const string source = """
            module Demo
            type Invoice { value: int }

            fn make(mark: (int)->int): Invoice {
                let ignored = mark(2)
                Invoice { value = 40 }
            }

            extend Invoice {
                fn add(self, fee: int): int { self.value + fee }
                fn sum(self, first: int, second: int): int { self.value + first + second }
                fn fail(self, first: int, second: int): int { 1 / 0 }
                fn choose(self, value: int): int { self.value + value }
                fn choose(self, value: string): int { self.value + 1 }
                fn choose(self, fee: int, extra: int): int { self.value + fee + extra }
                fn choose(self, fee: int, extra: string): int { self.value + fee + 1 }
                fn apply(self, callback: (int)->int): int { callback(self.value) }
                async fn wait(self, gate: Task<int>): int { self.value + await gate }
            }
            extend string? {
                fn orElse(self, fallback: string): string { self ?? fallback }
            }
            extend Option<string?> {
                fn score(self, value: int): int {
                    match self
                    {
                        None => value
                        Some(null) => value + 1
                        Some(text) => value + 2
                    }
                }
            }
            extend int {
                fn nullChoice(self, value: string?): int { self + 2 }
                fn nullChoice(self, value: int): int { self }
            }
            fn ordered(mark: (int)->int): int {
                let partial: (int)->int = mark(1) |> make(mark).sum
                partial(mark(3))
            }
            fn deferred(): (int)->int { 1 |> Invoice { value = 40 }.fail }
            fn waiting(gate: Task<int>): Task<int> { gate |> Invoice { value = 40 }.wait }
            fn generic<T>(value: T, invoice: Invoice): int { 2 |> invoice.add }
            fn answer(): bool {
                let invoice = Invoice { value = 40 }
                let partial: (int)->int = 1 |> invoice.sum
                let selected: (string)->int = 1 |> invoice.choose
                let missing: string? = null
                let present: string? = "Ada"
                let none: Option<string?> = None
                let someNull: Option<string?> = Some(null)
                let someName: Option<string?> = Some("Ada")
                (2 |> invoice.add) == 42 && (2 |> (invoice.add)) == 42 && partial(1) == 42 && selected("x") == 42 &&
                ((x => x + 2) |> invoice.apply) == 42 && generic<string>("value", invoice) == 42 &&
                (2 |> invoice.choose) == 42 && ("x" |> invoice.choose) == 41 &&
                ("Unknown" |> missing.orElse) == "Unknown" && ("Unknown" |> present.orElse) == "Ada" &&
                (40 |> none.score) == 40 && (40 |> someNull.score) == 41 && (40 |> someName.score) == 42 &&
                (null |> 40.nullChoice) == 42
            }
            """;
        var compilation = DSharpCompiler.Compile(source, "bare-extension-pipe.ds");
        Assert.True(compilation.CSharp is not null, string.Join("\n", compilation.Diagnostics));
        Assert.Empty(compilation.Diagnostics);
        await WithGenerated(compilation.CSharp!, "Demo", async type =>
        {
            Assert.Equal(true, Method(type, "answer").Invoke(null, null));
            var events = new List<int>();
            var ordered = Method(type, "ordered");
            var mark = Adapt(ordered.GetParameters()[0].ParameterType, n => { events.Add(n); return n; });
            Assert.Equal(44, ordered.Invoke(null, new object[] { mark }));
            Assert.Equal(new[] { 1, 2, 3 }, events);
            var deferred = Assert.IsAssignableFrom<Delegate>(Method(type, "deferred").Invoke(null, null));
            var failure = Assert.Throws<TargetInvocationException>(() => deferred.DynamicInvoke(1));
            Assert.IsType<DivideByZeroException>(failure.InnerException);
            var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = Assert.IsAssignableFrom<Task<int>>(Method(type, "waiting").Invoke(null, new object[] { gate.Task }));
            Assert.False(pending.IsCompleted);
            gate.SetResult(2);
            Assert.Equal(42, await pending.WaitAsync(TimeSpan.FromSeconds(10)));
        });
    }

    private static async Task VisibilityAmbiguityAndSafety()
    {
        var model = new SourceFile("model.ds", "module Model\ntype Invoice { value: int }");
        var pricing = new SourceFile("pricing.ds", "module Pricing\nimport Model\nextend Invoice { fn add(self, fee: int): int { self.value + fee } }");
        var app = new SourceFile("app.ds", "module App\nimport Model\nimport Pricing\nfn answer(): int { 2 |> Invoice { value = 40 }.add }");
        var compiled = DSharpCompiler.Compile(new[] { model, pricing, app });
        Assert.True(compiled.CSharp is not null, string.Join("\n", compiled.Diagnostics));
        await WithGenerated(compiled.CSharp!, "App", type =>
        {
            Assert.Equal(42, Method(type, "answer").Invoke(null, null));
            return Task.CompletedTask;
        });
        var hidden = new SourceFile("hidden.ds", "module App\nimport Model\nfn bad(): int { 2 |> Invoice { value = 40 }.add }");
        Assert.Null(DSharpCompiler.Compile(new[] { model, pricing, hidden }).CSharp);
        var other = new SourceFile("other.ds", "module Other\nimport Model\nextend Invoice { fn add(self, fee: int): int { self.value + fee + 1 } }");
        foreach (var imports in new[] { "import Pricing\nimport Other", "import Other\nimport Pricing" })
        {
            var ambiguous = new SourceFile("ambiguous.ds", "module App\nimport Model\n" + imports + "\nfn bad(): int { 2 |> Invoice { value = 40 }.add }");
            var result = DSharpCompiler.Compile(new[] { model, pricing, other, ambiguous });
            Assert.Null(result.CSharp);
            Assert.Contains(result.Diagnostics, d => d.Code == "DS2018");
        }
        string[] invalid =
        [
            "extend int { fn add(self, value: int): int { self + value } }\nfn bad(): int { true |> 40.add }",
            "extend int { fn choose(self, value: string?): int { 1 }\n fn choose(self, value: int?): int { 2 } }\nfn bad(): int { null |> 1.choose }",
            "extend int { fn apply(self, f: (int)->int): int { f(self) }\n fn apply(self, f: (bool)->int): int { f(true) } }\nfn bad(): int { (x => 42) |> 40.apply }",
            "extend int { fn factory(self): (int)->int { (v: int) => self + v } }\nfn bad(): int { 2 |> 40.factory }",
            "extend List<int> { fn get(self, name: string): int { 42 } }\nfn bad(): int { \"x\" |> [1].get }",
            "extend int { fn rec(self, value: int): int { answer() } }\nfn answer() { 2 |> 40.rec }",
            "type Item { add: int }\nextend Item { fn add(self, value: int): int { self.add + value } }\nfn bad(item: Item): int { 2 |> item.add }",
            "type Item { value: int }\nextend Item { fn add(self, value: int): int { self.value + value } }\nfn bad(item: Item?): int { 2 |> item.add }",
            "interface A { get(): int }\nextend A { fn add(self, value: int): int { self.get() + value } }\nfn bad(a: A): int { 2 |> a.add }",
            "resource Cursor { read(): int { 42 }\n release(): Unit { () } }\nextend Cursor { fn add(self, value: int): int { self.read() + value } }\nfn bad(): int { using c = Cursor() { 2 |> c.add } }",
            "extend MutableList<int> { fn addSome(self, value: int): int { value } }\nfn bad(): int { let b = MutableList<int>()\n 2 |> b.addSome }"
        ];
        foreach (var body in invalid)
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + body, "bare-extension-invalid.ds");
            Assert.True(result.CSharp is null, "Expected rejection: " + body);
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, d => Assert.Equal("bare-extension-invalid.ds", d.Path));
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
        var assemblyName = "DSharpBareExtension_" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-bare-extension-" + Guid.NewGuid());
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
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated bare extension pipe build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("bare-extension-" + Guid.NewGuid(), isCollectible: false);
            var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0", assemblyName + ".dll"));
            await verify(Assert.Single(assembly.GetTypes(), candidate => candidate.Name == module));
        }
        finally { Directory.Delete(directory, true); }
    }
}
