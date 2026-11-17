using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

public sealed class E197
{
    [Fact]
    public async Task Run()
    {
        const string source = """
            module Demo
            type Invoice { value: int }
            fn make(mark: (int)->int): Invoice {
                let ignored = mark(1)
                Invoice { value = 40 }
            }
            extend Invoice {
                fn add(self, fee: int): int { self.value + fee }
                fn apply(self, transform: (int)->int): int { transform(self.value) }
                fn sum(self, first: int, second: int): int { self.value + first + second }
                fn kind(self, first: int, second: int): int { self.value + first + second }
                fn kind(self, first: int, second: string): int { self.value + first + 1 }
                fn exact(self): int { 42 }
                fn exact(self, value: int): int { self.value + value }
                fn fun(self): (int)->int { (value: int) => self.value + value }
                fn fun(self, value: int): int { 1000 + value }
                fn route(self): (string)->int { (text: string) => 0 }
                fn route(self, value: int): int { self.value + value }
                fn nullable(self, text: string?): int { if text == null { self.value + 2 } else { 0 } }
                fn inspect(self, value: Option<string?>): int {
                    match value { None => self.value
                                  Some(null) => self.value + 1
                                  Some(text) => self.value + 2 }
                }
                async fn watch(self, mark: (int)->int, gate: Task<int>): int {
                    let ignored = mark(2)
                    self.value + await gate
                }
            }
            extend List<int> {
                fn get(self, name: string): int { self.length() + 40 }
            }
            fn factory(mark: (int)->int): (int)->int { make(mark).add() }
            fn groupedOrder(mark: (int)->int): int { (make(mark).sum())(mark(2), mark(3)) }
            fn asyncFactory(mark: (int)->int): ((int)->int, Task<int>)->Task<int> {
                make(mark).watch()
            }
            fn result(): bool {
                let invoice = Invoice { value = 40 }
                let add: (int)->int = invoice.add()
                let sum: (int,int)->int = invoice.sum()
                let exact: (int)->int = invoice.exact()
                let callable: (int)->int = invoice.fun()
                let routed: (int)->int = invoice.route()
                let option: (Option<string?>)->int = invoice.inspect()
                let nullArg: string? = null
                let special: (string)->int = [1,2].get()
                add(2) == 42 && add(3) == 43 && sum(1,1) == 42 &&
                (2 |> invoice.add()) == 42 && ((invoice.apply())(x => x + 2)) == 42 && ((invoice.sum())(1))(1) == 42 &&
                ((1 |> invoice.sum())(1)) == 42 && ((invoice.kind())(1,1)) == 42 && ((invoice.kind())(1,"x")) == 42 &&
                invoice.exact() == 42 && exact(2) == 42 && callable(2) == 42 &&
                ((invoice.fun())(2)) == 42 && (2 |> invoice.fun()) == 42 &&
                routed(2) == 42 && ((invoice.route())(2)) == 42 && (2 |> invoice.route()) == 42 &&
                (null |> invoice.nullable()) == 42 && (nullArg |> invoice.nullable()) == 42 &&
                option(None) == 40 && option(Some(null)) == 41 && option(Some("A")) == 42 &&
                [1,2].length() == 2 && special("x") == 42 && ("x" |> [1,2].get()) == 42 && (1 |> [1,2].get()) == Some(2)
            }
            """;
        var compiled = DSharpCompiler.Compile(source, "empty-extension.ds");
        Assert.True(compiled.CSharp is not null, string.Join("\n", compiled.Diagnostics));
        Assert.Empty(compiled.Diagnostics);
        await WithGenerated(compiled.CSharp!, "Demo", async type =>
        {
            Assert.Equal(true, Method(type, "result").Invoke(null, null));
            var groupedEvents = new List<int>();
            var groupedOrder = Method(type, "groupedOrder");
            var groupedMark = Adapt(groupedOrder.GetParameters()[0].ParameterType, value => { groupedEvents.Add(value); return value; });
            Assert.Equal(45, groupedOrder.Invoke(null, new object[] { groupedMark }));
            Assert.Equal(new[] { 1, 2, 3 }, groupedEvents);
            var events = new List<int>();
            var factory = Method(type, "factory");
            var mark = Adapt(factory.GetParameters()[0].ParameterType, value => { events.Add(value); return value; });
            var add = Assert.IsAssignableFrom<Delegate>(factory.Invoke(null, new object[] { mark }));
            Assert.Equal(new[] { 1 }, events);
            Assert.Equal(42, add.DynamicInvoke(2));
            Assert.Equal(43, add.DynamicInvoke(3));
            Assert.Equal(new[] { 1 }, events);
            var asyncFactory = Method(type, "asyncFactory");
            var asyncMark = Adapt(asyncFactory.GetParameters()[0].ParameterType, value => { events.Add(value); return value; });
            var call = Assert.IsAssignableFrom<Delegate>(asyncFactory.Invoke(null, new object[] { asyncMark }));
            Assert.Equal(new[] { 1, 1 }, events);
            var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var callbackType = call.GetType().GetMethod("Invoke")!.GetParameters()[0].ParameterType;
            var callback = Adapt(callbackType, value => { events.Add(value); return value; });
            var pending = Assert.IsAssignableFrom<Task<int>>(call.DynamicInvoke(callback, gate.Task));
            Assert.Equal(new[] { 1, 1, 2 }, events);
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
        var pricing = new SourceFile("pricing.ds", "module Pricing\nimport Model\nextend Invoice { fn add(self, fee: int): int { self.value + fee } }");
        var app = new SourceFile("app.ds", "module App\nimport Model\nimport Pricing\nfn answer(): int { let f: (int)->int = Invoice { value = 40 }.add()\n f(2) }");
        var compiled = DSharpCompiler.Compile(new[] { model, pricing, app });
        Assert.True(compiled.CSharp is not null, string.Join("\n", compiled.Diagnostics));
        await WithGenerated(compiled.CSharp!, "App", type => { Assert.Equal(42, Method(type, "answer").Invoke(null, null)); return Task.CompletedTask; });
        var hidden = new SourceFile("hidden.ds", "module App\nimport Model\nfn bad(): int { let f: (int)->int = Invoice { value = 40 }.add()\n f(2) }");
        Assert.Null(DSharpCompiler.Compile(new[] { model, pricing, hidden }).CSharp);
        var duplicate = new SourceFile("other.ds", "module Other\nimport Model\nextend Invoice { fn add(self, fee: int): int { self.value + fee + 1 } }");
        foreach (var imports in new[] { "import Pricing\nimport Other", "import Other\nimport Pricing" })
        {
            var ambiguous = new SourceFile("ambiguous.ds", "module App\nimport Model\n" + imports + "\nfn bad(): int { let f: (int)->int = Invoice { value = 40 }.add()\n f(2) }");
            var rejected = DSharpCompiler.Compile(new[] { model, pricing, duplicate, ambiguous });
            Assert.Null(rejected.CSharp);
            Assert.Contains(rejected.Diagnostics, d => d.Code == "DS2018");
        }
        var nullableOverloads = new SourceFile("nullable-overloads.ds", """
            module App
            extend int {
                fn check(self, value: string?): int { 42 }
                fn check(self, value: int): int { 0 }
            }
            fn answer(): int { null |> 1.check() }
            """);
        var overloads = DSharpCompiler.Compile(new[] { nullableOverloads });
        Assert.True(overloads.CSharp is not null, string.Join("\n", overloads.Diagnostics));
        await WithGenerated(overloads.CSharp!, "App", type => { Assert.Equal(42, Method(type, "answer").Invoke(null, null)); return Task.CompletedTask; });
    }

    private static void RejectInvalid()
    {
        string[] invalid =
        [
            "extend int { fn add(self, value: int): int { self + value } }\nfn bad(): Unit { let p = 1.add()\n () }",
            "extend int { fn add(self, value: int): int { self + value } }\nfn bad(): Unit { let p: (bool)->int = 1.add()\n () }",
            "extend int { fn add(self, value: int): int { self + value } }\nfn bad(): int { true |> 1.add() }",
            "extend int { fn add(self, value: int): int { self + value } }\nfn bad(): int { (1.add())(2,3) }",
            "extend int { fn choose(self, value: string?): int { 1 }\n fn choose(self, value: int?): int { 2 } }\nfn bad(): int { null |> 1.choose() }",
            "extend int { fn apply(self, f: (int)->int): int { f(self) }\n fn apply(self, f: (bool)->int): int { f(true) } }\nfn bad(): int { ((40.apply())(x => 42)) }",
            "interface A { get(): int }\nservice S: A { get() { 42 } }\nextend A { fn add(self, value: int): int { self.get() + value } }\nfn bad(s: A): Unit { let p: (int)->int = s.add()\n () }",
            "resource Cursor { read(): int { 42 }\n release(): Unit { () } }\nextend Cursor { fn add(self, value: int): int { self.read() + value } }\nfn bad(): Unit { using c = Cursor() { let p: (int)->int = c.add()\n () } }",
            "extend MutableList<int> { fn addSome(self, value: int): int { value } }\nfn bad(): Unit { let b = MutableList<int>()\n let p: (int)->int = b.addSome()\n () }"
        ];
        foreach (var body in invalid)
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + body, "empty-extension-invalid.ds");
            Assert.True(result.CSharp is null, "Expected rejection: " + body);
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, diagnostic => Assert.Equal("empty-extension-invalid.ds", diagnostic.Path));
        }
    }

    private static MethodInfo Method(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
    private static Delegate Adapt(Type delegateType, Func<int,int> callback)
    {
        var value = Expression.Parameter(typeof(int), "value");
        return Expression.Lambda(delegateType, Expression.Invoke(Expression.Constant(callback), value), value).Compile();
    }
    private static async Task WithGenerated(string csharp, string module, Func<Type, Task> verify)
    {
        var assemblyName = "DSharpEmptyExtension_" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-empty-extension-" + Guid.NewGuid());
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
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated empty extension build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("empty-extension-" + Guid.NewGuid(), isCollectible: false);
            var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0", assemblyName + ".dll"));
            await verify(Assert.Single(assembly.GetTypes(), candidate => candidate.Name == module));
        }
        finally { Directory.Delete(directory, true); }
    }
}
