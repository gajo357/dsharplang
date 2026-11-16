using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class PartialInferenceAcceptanceTests
{
    // Run inside the established feature aggregate to keep the CI report bounded.
    public async Task StaticPartialCallsInferAndIndirectCallsRemainChecked()
    {
        const string source = """
            module Demo
            fn sum(acc: int, value: int): int { acc + value }
            extend int {
                fn add(self, first: int, second: int): int { self + first + second }
                fn get(self, first: int, second: int): int { self + first + second }
                fn fold(self, first: int, second: int): int { self + first + second }
            }
            // Applicable intrinsic partials win over same-name full extensions.
            extend Map<int,int> { fn set(self, key: int): int { 999 } }
            fn mapPipe(map: Map<int,int>) { 42 |> map.set(1) }
            fn mapGrouped(map: Map<int,int>) { (map.set(1))(42) }
            fn mapLocal() { let map: Map<int,int> = Map.empty<int,int>()
                42 |> map.set(1) }
            fn foldPipe(values: List<int>) { sum |> values.fold(1) }
            fn foldGrouped(values: List<int>) { (values.fold(1))(sum) }
            fn foldLambda(values: List<int>) { ((acc: int, item: int) => acc + item) |> values.fold(1) }
            fn foldLiteral() { sum |> [20, 21].fold(1) }
            fn foldLocal() { let values: List<int> = [20, 21]
                sum |> values.fold(1) }
            fn extensionAdd() { 2 |> 39.add(1) }
            fn extensionGet() { 2 |> 39.get(1) }
            fn extensionFold() { 2 |> 39.fold(1) }
            fn extensionGrouped() { (39.add(1))(2) }
            fn tagged(mark: (int)->int, value: int): int { mark(value) }
            fn makeMap(mark: (int)->int): Map<int,int> { let seen = mark(2)
                Map.empty<int,int>() }
            fn order(mark: (int)->int): bool { let result = tagged(mark,1) |> makeMap(mark).set(tagged(mark,3))
                result.get(3) == Some(1) }
            fn nullableMap() { let value: int? = null
                value |> Map.empty<int,int?>().set(1) }
            fn answer(): bool {
                let empty: Map<int,int> = Map.empty<int,int>()
                let result1 = mapPipe(empty)
                let result2 = mapGrouped(empty)
                let result3 = mapLocal()
                let nullable = nullableMap()
                empty.count() == 0 && result1.get(1) == Some(42) &&
                result2.get(1) == Some(42) && result3.get(1) == Some(42) &&
                foldPipe([20,21]) == 42 && foldGrouped([20,21]) == 42 &&
                foldLambda([20,21]) == 42 && foldLiteral() == 42 && foldLocal() == 42 &&
                extensionAdd() == 42 && extensionGet() == 42 && extensionFold() == 42 &&
                extensionGrouped() == 42 && nullable.get(1) == Some(null)
            }
            """;
        var compilation = DSharpCompiler.Compile(source, "partial-inference.ds");
        Assert.True(compilation.CSharp is not null, string.Join("\n", compilation.Diagnostics));
        Assert.Empty(compilation.Diagnostics);
        await WithGenerated(compilation.CSharp!, "Demo", type =>
        {
            Assert.Equal(true, Call(type, "answer"));
            var events = new List<int>();
            var method = type.GetMethod("order", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
            var parameterType = method.GetParameters()[0].ParameterType;
            var arg = Expression.Parameter(typeof(int), "value");
            Func<int,int> record = n => { events.Add(n); return n; };
            var callback = Expression.Lambda(parameterType, Expression.Invoke(Expression.Constant(record), arg), arg).Compile();
            Assert.Equal(true, method.Invoke(null, new object[] { callback }));
            Assert.Equal(new[] { 1, 2, 3 }, events);
        });

        RejectIndirectAndRecursive();
        await UnrelatedExtensionDoesNotCreateCycle();
        await ImportedTargets();
        await NominalListShadow();
        await ExplicitResourceStillWorks();
    }

    private static void RejectIndirectAndRecursive()
    {
        string[] indirect =
        [
            // The fold callback is actually invoked; unknown callable values remain indirect.
            "fn bad(xs: List<int>, step: (int,int)->int) { step |> xs.fold(0) }",
            "fn bad(xs: List<int>, step: (int,int)->int) { (xs.fold(0))(step) }",
            "fn bad(f: (int)->int) { 42 |> f }",
            "fn bad(f: (int)->int) { f(42) }",
            // Same-named service/resource methods are not statically dispatched List/Map operations.
            "interface A { fold(step: (int)->int): int }\nfn bad(a: A) { a.fold(x => x) }",
            "interface A { set(key: int): (int)->int }\nfn bad(a: A) { 2 |> a.set(1) }",
            "resource Cursor { fold(step: (int)->int): int { step(1) }\n release(): Unit { () } }\nfn bad() { using r = Cursor() { r.fold(x => x) } }",
            "resource Cursor { fold(step: (int)->int): int { bad() }\n release(): Unit { () } }\nfn bad() { using r = Cursor() { r.fold(x => x) } }",
            // Shadowed user types must not be mistaken for the built-in List/Map.
            "interface Map { set(key: int): (int)->int }\nfn bad(x: Map) { 2 |> x.set(1) }",
            // An extension selected after a mismatching intrinsic key may itself return an unknown callback.
            "extend Map<int,int> { fn set(self, f: (int)->int): (int)->int { f } }\nfn bad(m:Map<int,int>, f:(int)->int) { 2 |> m.set(f) }",
            "extend Map<int,int> { fn set(self, f: (int)->int): (int)->int { f } }\nfn bad(m:Map<int,int>, f:(int)->int) { (m.set(f))(2) }",
            // An out-of-range intrinsic key can select a full long overload; its callback is still indirect.
            "extend Map<int,int> { fn set(self, key: long): (int)->int { (value:int) => value } }\nfn bad(m:Map<int,int>) { 2 |> m.set(2147483648) }",
            "extend Map<int,int> { fn set(self, key: long): (int)->int { (value:int) => value } }\nfn bad(m:Map<int,int>) { (m.set(2147483648))(2) }",
            // An inapplicable intrinsic key can instead choose a longer extension; retain its call edge.
            "extend Map<int,int> { fn set(self, key:string, extra:int):int { bad(Map.empty<int,int>()) } }\nfn bad(m:Map<int,int>) { 2 |> m.set(\"key\") }",
            "fn bad(m:Map<int,int>) { 2 |> m.set(\"key\") }\nextend Map<int,int> { fn set(self, key:string, extra:int):int { bad(Map.empty<int,int>()) } }",
            "extend List<int> { fn fold(self, text: string?): (int)->int { (value:int) => value } }\nfn bad(xs:List<int>) { 2 |> xs.fold(null) }",
            "extend List<int> { fn fold(self, text: string?): (int)->int { (value:int) => value } }\nfn bad(xs:List<int>) { (xs.fold(null))(2) }",
            "extend List<int> { fn fold(self, text:string?, extra:int):int { bad() } }\nfn bad() { 2 |> [1].fold(null) }",
            "fn bad() { 2 |> [1].fold(null) }\nextend List<int> { fn fold(self, text:string?, extra:int):int { bad() } }",
            // A full extension returning a callback cannot hide a recursive service call.
            "interface A { get(): int }\nservice S: A { get() { bad() } }\nextend List<int> { fn fold(self, text: string?): ((int,int)->int)->int { (step:(int,int)->int) => S().get() } }\nfn bad() { ([1].fold(null))((a:int,b:int)=>a+b) }",
            // A list-shaped local in an earlier lexical block cannot leak proof.
            "interface A { fold(step: (int)->int): int }\nfn bad(svc: A) { let unused = if true { let value: List<int> = [1]\n value.length() } else { 0 }\n let value: A = svc\n value.fold(x => x) }",
            // Recursion must be rejected regardless of declaration order, including callback bodies.
            "fn first() { (Map.empty<int,int>().set(1))(second()) }\nfn second() { first().count() }",
            "fn second() { first().count() }\nfn first() { (Map.empty<int,int>().set(1))(second()) }",
            "fn first() { step |> [1].fold(0) }\nfn step(acc:int, item:int) { first() }",
            "fn first() { ((acc:int, item:int) => first()) |> [1].fold(0) }"
        ];
        foreach (var body in indirect)
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + body, "partial-inference-invalid.ds");
            Assert.True(result.CSharp is null, "Expected rejection for " + body);
            Assert.True(result.Diagnostics.Any(d => d.Code == "DS2027"),
                "Expected indirect/cyclic inference diagnostic for " + body + ": " + string.Join("; ", result.Diagnostics));
            Assert.All(result.Diagnostics, d => Assert.Equal("partial-inference-invalid.ds", d.Path));
        }
    }

    private static async Task ImportedTargets()
    {
        var files = new[]
        {
            new SourceFile("model.ds", "module Model\ntype Item { amount: int }"),
            new SourceFile("math.ds", """
                module Math
                import Model
                fn sum(acc: int, item: int): int { acc + item }
                extend Item { fn add(self, first: int, second: int): int { self.amount + first + second } }
                """),
            new SourceFile("app.ds", """
                module Demo
                import Model
                import Math
                import Math
                fn viaExtension() { 2 |> Item { amount = 39 }.add(1) }
                fn viaFold() { sum |> [20,21].fold(1) }
                fn answer(): bool { viaExtension() == 42 && viaFold() == 42 }
                """)
        };
        var result = DSharpCompiler.Compile(files);
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        await WithGenerated(result.CSharp!, "Demo", type => Assert.Equal(true, Call(type, "answer")));

        var transitive = new SourceFile("bridge.ds", "module Bridge\nimport Math\nfn marker(): int { 1 }");
        var throughBridge = new SourceFile("hidden.ds", "module Demo\nimport Model\nimport Bridge\nfn answer(): int { 2 |> Item { amount = 39 }.add(1) }");
        var hidden = DSharpCompiler.Compile(new[] { files[0], files[1], transitive, throughBridge });
        Assert.True(hidden.CSharp is null, "A transitive import must not expose an extension.");
        Assert.NotEmpty(hidden.Diagnostics);
    }

    private static async Task UnrelatedExtensionDoesNotCreateCycle()
    {
        const string source = """
            module Demo
            extend int { fn set(self, a:int, b:int): Map<int,int> { answer() } }
            fn answer() { 42 |> Map.empty<int,int>().set(1) }
            fn check(): bool { answer().get(1) == Some(42) }
            """;
        var result = DSharpCompiler.Compile(source, "unrelated-extension.ds");
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        await WithGenerated(result.CSharp!, "Demo", type => Assert.Equal(true, Call(type, "check")));
    }

    private static async Task ExplicitResourceStillWorks()
    {
        const string source = """
            module Demo
            resource Cursor {
                fold(step: (int)->int): int { step(40) }
                release(): Unit { () }
            }
            resource FailingCursor {
                fold(step: (int)->int): int { step(40) }
                release(): Unit { let fault = 1 / 0
                    () }
            }
            fn answer(): int { using cursor = Cursor() { cursor.fold(value => value + 2) } }
            fn deadBranch() { using cursor = Cursor() { if true { 42 } else { cursor.fold(value => value + 2) } } }
            fn failingRelease(): int { using cursor = FailingCursor() { cursor.fold(value => value + 2) } }
            """;
        var result = DSharpCompiler.Compile(source, "resource-explicit.ds");
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        await WithGenerated(result.CSharp!, "Demo", type =>
        {
            Assert.Equal(42, Call(type, "answer"));
            Assert.Equal(42, Call(type, "deadBranch"));
            var error = Assert.Throws<TargetInvocationException>(() => Call(type, "failingRelease"));
            Assert.IsType<DivideByZeroException>(error.InnerException);
        });
    }

    private static async Task NominalListShadow()
    {
        const string source = """
            module Demo
            type List<T> { value: T }
            extend List<int> { fn fold(self, first: int, second: int): int { self.value + first + second } }
            fn answer() { let value: List<int> = List { value = 39 }
                2 |> value.fold(1) }
            """;
        var result = DSharpCompiler.Compile(source, "shadow-list.ds");
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        await WithGenerated(result.CSharp!, "Demo", type => Assert.Equal(42, Call(type, "answer")));
    }

    private static object? Call(Type type, string method) => type.GetMethod(method,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.Invoke(null, null);

    private static async Task WithGenerated(string csharp, string module, Action<Type> verify)
    {
        var assemblyName = "DSharpPartialInference_" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-partial-inference-" + Guid.NewGuid());
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
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated partial-inference build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("partial-inference-" + Guid.NewGuid(), isCollectible: false);
            var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0", assemblyName + ".dll"));
            var type = Assert.Single(assembly.GetTypes(), candidate => candidate.Name == module);
            verify(type);
        }
        finally { Directory.Delete(directory, true); }
    }
}
