using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

public sealed class E199
{
    [Fact]
    public async Task Run()
    {
        const string source = """
            module Demo
            type Invoice { value: int }
            type Box { value: int }
            type Holder<T> { value: T }
            type Decision { | Accept | Reject }
            fn make(mark: (int)->int): Invoice {
                let ignored = mark(1)
                Invoice { value = 40 }
            }
            extend Invoice {
                fn add(self, fee: int): int { self.value + fee }
                fn sum(self, first: int, second: int): int { self.value + first + second }
                fn get(self): int { self.value + 2 }
                fn fail(self): int { 1 / 0 }
                fn pick(self, fee: int): int { self.value + fee }
                fn pick(self, name: string): int { self.value + 1 }
                fn value(self, fee: int): int { self.value + fee }
                async fn watch(self, mark: (int)->int, gate: Task<int>): int {
                    let ignored = mark(2)
                    self.value + await gate
                }
            }
            extend Holder<int?> {
                fn orElse(self, fallback: int): int { self.value ?? fallback }
            }
            extend Option<string?> {
                fn score(self, value: int): int {
                    match self { None => value
                                 Some(null) => value + 1
                                 Some(text) => value + 2 }
                }
            }
            extend Decision { fn Accept(self, value: int): int { value } }
            extend string? { fn alternate(self, fallback: string): string { self ?? fallback } }
            extend int { fn value(self): int { self } }
            extend List<int> { fn addFee(self, fee: int): int { self.length() + fee }
                               fn get(self, name: string): int { 99 }
                               fn get(self, callback: ()->int): int { 42 }
                               fn append(self, callback: (int)->int): List<int> { self } }
            fn makeBox() { Box { value = 0 } }
            fn builtinIndex(xs: List<int>) { xs.get(makeBox().value) }
            fn builtinAppend(xs: List<int>) { xs.append(makeBox().value) }
            fn apply(f: (int)->int): int { f(2) }
            fn factory(mark: (int)->int): (int)->int { make(mark).add }
            fn zero(mark: (int)->int): ()->int { make(mark).get }
            fn badFactory(): ()->int { Invoice { value = 40 }.fail }
            fn asyncFactory(mark: (int)->int): ((int)->int, Task<int>)->Task<int> { make(mark).watch }
            fn result(): bool {
                let invoice = Invoice { value = 40 }
                let add: (int)->int = invoice.add
                let sum: (int,int)->int = invoice.sum
                let get: ()->int = invoice.get
                let selected: (int)->int = invoice.pick
                let holder: Holder<int?> = Holder { value = null }
                let absent: Option<string?> = None
                let presentNull: Option<string?> = Some(null)
                let fallback: (int)->int = holder.orElse
                let missing: (int)->int = absent.score
                let present: (int)->int = presentNull.score
                let missingText: string? = null
                let presentText: string? = "Ada"
                let missingName: (string)->string = missingText.alternate
                let presentName: (string)->string = presentText.alternate
                let listFee: (int)->int = [40].addFee
                let mapped: List<int> = [1,2].map<int>(invoice.add)
                let field: int = invoice.value
                let decision: Decision = Decision.Accept
                add(2) == 42 && add(3) == 43 && sum(1,1) == 42 && get() == 42 && selected(2) == 42 &&
                apply(invoice.add) == 42 && fallback(42) == 42 && missing(40) == 40 && present(40) == 41 &&
                missingName("Anonymous") == "Anonymous" && presentName("Anonymous") == "Ada" &&
                listFee(41) == 42 && mapped == [41,42] && [40,42].get(1) == Some(42) &&
                [40,42].get("x") == 99 && builtinIndex([42]) == Some(42) &&
                builtinAppend([42]) == [42,0] && field == 40 && decision == Decision.Accept
            }
            """;
        var compiled = DSharpCompiler.Compile(source, "extension-value.ds");
        Assert.True(compiled.CSharp is not null, string.Join("\n", compiled.Diagnostics));
        Assert.Empty(compiled.Diagnostics);
        await WithGenerated(compiled.CSharp!, "Demo", async type =>
        {
            Assert.Equal(true, Method(type, "result").Invoke(null, null));
            var events = new List<int>();
            var factory = Method(type, "factory");
            var mark = Adapt(factory.GetParameters()[0].ParameterType, value => { events.Add(value); return value; });
            var add = Assert.IsAssignableFrom<Delegate>(factory.Invoke(null, new object[] { mark }));
            Assert.Equal(new[] { 1 }, events);
            Assert.Equal(42, add.DynamicInvoke(2));
            Assert.Equal(43, add.DynamicInvoke(3));
            Assert.Equal(new[] { 1 }, events);
            var zero = Method(type, "zero");
            var zeroMark = Adapt(zero.GetParameters()[0].ParameterType, value => { events.Add(value); return value; });
            var get = Assert.IsAssignableFrom<Delegate>(zero.Invoke(null, new object[] { zeroMark }));
            Assert.Equal(new[] { 1, 1 }, events);
            Assert.Equal(42, get.DynamicInvoke());
            Assert.Equal(42, get.DynamicInvoke());
            Assert.Equal(new[] { 1, 1 }, events);
            var fail = Assert.IsAssignableFrom<Delegate>(Method(type, "badFactory").Invoke(null, null));
            var failure = Assert.Throws<TargetInvocationException>(() => fail.DynamicInvoke());
            Assert.IsType<DivideByZeroException>(failure.InnerException);
            var asyncFactory = Method(type, "asyncFactory");
            var asyncMark = Adapt(asyncFactory.GetParameters()[0].ParameterType, value => { events.Add(value); return value; });
            var watch = Assert.IsAssignableFrom<Delegate>(asyncFactory.Invoke(null, new object[] { asyncMark }));
            Assert.Equal(new[] { 1, 1, 1 }, events);
            var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var callbackType = watch.GetType().GetMethod("Invoke")!.GetParameters()[0].ParameterType;
            var callback = Adapt(callbackType, value => { events.Add(value); return value; });
            var pending = Assert.IsAssignableFrom<Task<int>>(watch.DynamicInvoke(callback, gate.Task));
            Assert.Equal(new[] { 1, 1, 1, 2 }, events);
            Assert.False(pending.IsCompleted);
            gate.SetResult(2);
            Assert.Equal(42, await pending.WaitAsync(TimeSpan.FromSeconds(10)));
        });
        await ImportsAndAmbiguity();
        RejectInvalid();
        RejectRecursiveMethodValues();
    }

    private static async Task ImportsAndAmbiguity()
    {
        var model = new SourceFile("model.ds", "module Model\ntype Invoice { value: int }");
        var pricing = new SourceFile("pricing.ds", "module Pricing\nimport Model\nextend Invoice { fn add(self, fee:int): int { self.value + fee } }");
        var app = new SourceFile("app.ds", "module App\nimport Model\nimport Pricing\nfn answer(): int { let f: (int)->int = Invoice { value = 40 }.add\n f(2) }");
        var result = DSharpCompiler.Compile(new[] { model, pricing, app });
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        await WithGenerated(result.CSharp!, "App", type => { Assert.Equal(42, Method(type, "answer").Invoke(null, null)); return Task.CompletedTask; });
        var hidden = new SourceFile("hidden.ds", "module App\nimport Model\nfn bad(): int { let f: (int)->int = Invoice { value = 40 }.add\n f(2) }");
        Assert.Null(DSharpCompiler.Compile(new[] { model, pricing, hidden }).CSharp);
        var relay = new SourceFile("relay.ds", "module Relay\nimport Pricing");
        var transitive = new SourceFile("transitive.ds", "module App\nimport Model\nimport Relay\nfn bad(): int { let f: (int)->int = Invoice { value = 40 }.add\n f(2) }");
        Assert.Null(DSharpCompiler.Compile(new[] { model, pricing, relay, transitive }).CSharp);
        var duplicate = new SourceFile("duplicate.ds", "module App\nimport Model\nimport Pricing\nimport Pricing\nfn answer(): int { let f: (int)->int = Invoice { value = 40 }.add\n f(2) }");
        var duplicated = DSharpCompiler.Compile(new[] { model, pricing, duplicate });
        Assert.True(duplicated.CSharp is not null, string.Join("\n", duplicated.Diagnostics));
        await WithGenerated(duplicated.CSharp!, "App", type => { Assert.Equal(42, Method(type, "answer").Invoke(null, null)); return Task.CompletedTask; });
        var other = new SourceFile("other.ds", "module Other\nimport Model\nextend Invoice { fn add(self, fee:int): int { self.value + fee + 1 } }");
        foreach (var imports in new[] { "import Pricing\nimport Other", "import Other\nimport Pricing" })
        {
            var ambiguous = new SourceFile("ambiguous.ds", "module App\nimport Model\n" + imports + "\nfn bad(): int { let f: (int)->int = Invoice { value = 40 }.add\n f(2) }");
            var rejected = DSharpCompiler.Compile(new[] { model, pricing, other, ambiguous });
            Assert.Null(rejected.CSharp);
            Assert.Contains(rejected.Diagnostics, diagnostic => diagnostic.Code == "DS2018");
        }
    }

    private static void RejectInvalid()
    {
        string[] invalid =
        [
            "extend int { fn add(self, value:int): int { self + value } }\nfn bad(): Unit { let p = 1.add\n () }",
            "extend int { fn add(self, value:int): int { self + value } }\nfn bad(): Unit { let p: (bool)->int = 1.add\n () }",
            "type Item { value: int }\nextend Item { fn add(self, value:int): int { self.value + value } }\nfn bad(item: Item?): Unit { let p: (int)->int = item.add\n () }",
            "extend List<int> { fn get(self, name:string): int { 42 } }\nfn bad(items: List<int>): Unit { let p: (string)->int = items.get\n () }",
            "type Item { add: int }\nextend Item { fn add(self, value:int): int { self.add + value } }\nfn bad(item: Item): Unit { let p: (int)->int = item.add\n () }",
            "interface A { get(): int }\nservice S: A { get() { 42 } }\nextend A { fn add(self, value:int): int { self.get() + value } }\nfn bad(s:A): Unit { let p: (int)->int = s.add\n () }",
            "resource Cursor { read(): int { 42 }\n release(): Unit { () } }\nextend Cursor { fn add(self, value:int): int { self.read() + value } }\nfn bad(): Unit { using c = Cursor() { let p: (int)->int = c.add\n () } }",
            "extend MutableList<int> { fn addSome(self, value:int): int { value } }\nfn bad(): Unit { let b = MutableList<int>()\n let p: (int)->int = b.addSome\n () }",
            "extend int { fn add(self, value:int): int { self + value } }\nfn bad(): int { 2 |> 40.add }"
        ];
        foreach (var body in invalid)
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + body, "extension-value-invalid.ds");
            Assert.True(result.CSharp is null, "Expected rejection: " + body);
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, diagnostic => Assert.Equal("extension-value-invalid.ds", diagnostic.Path));
        }
    }

    private static void RejectRecursiveMethodValues()
    {
        const string extensions = "extend int { fn rec(self,v:int):int { answer() } fn ignore(self, callback:(int)->int):int { 42 } }";
        var calls = new[] { "fn answer() { 40.ignore(40.rec) }", "fn answer() { let value: int? = 40\n value?.ignore(40.rec) ?? 0 }" };
        foreach (var call in calls)
        foreach (var body in new[] { extensions + "\n" + call, call + "\n" + extensions })
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + body, "recursive-extension-value.ds");
            Assert.Null(result.CSharp);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DS2027");
        }
        const string nullableExtensions = "extend string? { fn rec(self, value:int): int { answer() } fn ignore(self, callback:(int)->int): int { 42 } }";
        foreach (var value in new[] { "null", "\"Ada\"" })
        {
            var nullableCall = "fn answer() { let text: string? = " + value + "\n text.ignore(text.rec) }";
            foreach (var body in new[] { nullableExtensions + "\n" + nullableCall, nullableCall + "\n" + nullableExtensions })
            {
                var result = DSharpCompiler.Compile("module Invalid\n" + body, "recursive-extension-value.ds");
                Assert.Null(result.CSharp);
                Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DS2027");
            }
        }
        const string nestedExtensions = "extend int { fn rec(self,v:int): int { answer() } fn ignore(self, callback:()->()->int): int { 42 } fn ignore(self, callback:()->()->()->((int)->int)): int { 42 } }";
        const string nestedCall = "fn answer() { 40.ignore(() => () => () => 40.rec) }";
        foreach (var body in new[] { nestedExtensions + "\n" + nestedCall, nestedCall + "\n" + nestedExtensions })
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + body, "recursive-extension-value.ds");
            Assert.Null(result.CSharp);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DS2027");
        }
        const string genericExtensions = "extend int { fn rec(self,v:int): int { answer() } } fn ignore<T>(callback:()->T):int { 42 }";
        const string genericCall = "fn answer() { ignore<(int)->int>(() => 40.rec) }";
        foreach (var body in new[] { genericExtensions + "\n" + genericCall, genericCall + "\n" + genericExtensions })
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + body, "recursive-extension-value.ds");
            Assert.Null(result.CSharp);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DS2027");
        }
        const string inferredExtensions = "extend int { fn rec(self,v:int): int { answer() } } fn infer<T>(witness:T, callback:()->T):int { 42 }";
        const string inferredCall = "fn answer() { infer((x:int) => x, () => 40.rec) }";
        foreach (var body in new[] { inferredExtensions + "\n" + inferredCall, inferredCall + "\n" + inferredExtensions })
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + body, "recursive-extension-value.ds");
            Assert.Null(result.CSharp);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DS2027");
        }
        const string indirect = "extend int { fn target(self): int { bad() } fn run(self, f: ()->int): int { f() } }";
        const string indirectCall = "fn bad() { 40.run(40.target) }";
        foreach (var body in new[] { indirect + "\n" + indirectCall, indirectCall + "\n" + indirect })
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + body, "recursive-extension-value.ds");
            Assert.Null(result.CSharp);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DS2027");
        }
    }

    private static MethodInfo Method(Type type, string name) => type.GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
    private static Delegate Adapt(Type delegateType, Func<int,int> callback)
    {
        var value = Expression.Parameter(typeof(int), "value");
        return Expression.Lambda(delegateType, Expression.Invoke(Expression.Constant(callback), value), value).Compile();
    }

    private static async Task WithGenerated(string csharp, string module, Func<Type,Task> verify)
    {
        var assemblyName = "DSharpExtensionValue_" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-extension-value-" + Guid.NewGuid());
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
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated extension method value build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("extension-value-" + Guid.NewGuid(), isCollectible: false);
            var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0", assemblyName + ".dll"));
            await verify(Assert.Single(assembly.GetTypes(), type => type.Name == module));
        }
        finally { Directory.Delete(directory, true); }
    }
}
