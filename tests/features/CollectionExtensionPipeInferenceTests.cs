using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

public sealed class CollectionExtensionPipeInferenceTests
{
    public async Task CollectionExtensionPipesInferAndPreserveSafety()
    {
        const string source = """
            module Demo

            extend List<int> {
                fn withFee(self, fee: int): int { self.length() + fee }
                fn get(self, key: string): int { 0 }
            }
            extend List<int?> {
                fn withFee(self, fee: int): int { self.length() + fee }
            }
            extend Map<int, int> {
                fn withFee(self, fee: int): int { self.count() + fee }
            }
            extend Set<int> {
                fn withFee(self, fee: int): int { self.count() + fee }
            }

            fn fee(): int { 40 }
            fn items(): List<int> { [10, 20] }
            fn listTotal(items: List<int>, fee: int) { fee |> items.withFee }
            fn mapTotal(items: Map<int, int>, fee: int) { fee |> items.withFee }
            fn setTotal(items: Set<int>, fee: int) { fee |> items.withFee }
            fn grouped(items: List<int>) { 40 |> (items.withFee) }
            fn first(items: List<int>) { 0 |> items.get }
            fn fromCalls() { fee() |> items().withFee }
            fn annotated(items: List<int>): int { 40 |> items.withFee }
            fn nullable<T>(items: List<int?>, tag: T) { 40 |> items.withFee }

            fn failingFee(): int { 1 / 0 }
            fn failingItems(): List<int> {
                let failed = 2147483647 + 1
                [failed]
            }
            fn faultOrder() { failingFee() |> failingItems().withFee }
            fn receiverFault() { 40 |> failingItems().withFee }

            fn answer(): bool {
                let list: List<int> = [10, 20]
                let nullableItems: List<int?> = [null, 42]
                let map = Map.empty<int, int>().set(1, 10).set(2, 20)
                let set = Set.from([10, 20])
                listTotal(list, 40) == 42 && mapTotal(map, 40) == 42 &&
                setTotal(set, 40) == 42 && grouped(list) == annotated(list) && first(list) == Some(10) &&
                fromCalls() == 42 && nullable<string>(nullableItems, "tag") == 42 &&
                list.length() == 2 && map.count() == 2 && set.count() == 2
            }
            """;
        var compiled = DSharpCompiler.Compile(source, "collection-extension-inference.ds");
        Assert.True(compiled.CSharp is not null, string.Join("\n", compiled.Diagnostics));
        Assert.Empty(compiled.Diagnostics);
        await WithGenerated(compiled.CSharp!, "Demo", type =>
        {
            Assert.Equal(true, Method(type, "answer").Invoke(null, null));
            var inputFailure = Assert.Throws<TargetInvocationException>(() => Method(type, "faultOrder").Invoke(null, null));
            Assert.IsType<DivideByZeroException>(inputFailure.InnerException);
            var receiverFailure = Assert.Throws<TargetInvocationException>(() => Method(type, "receiverFault").Invoke(null, null));
            Assert.IsType<OverflowException>(receiverFailure.InnerException);
        });

        await ImportedExtensions();
        RejectUnprovenOrUnsafe();
    }

    private static async Task ImportedExtensions()
    {
        var library = new SourceFile("pricing.ds", """
            module Pricing
            extend List<int> {
                fn withFee(self, fee: int): int { self.length() + fee }
            }
            """);
        var application = new SourceFile("app.ds", """
            module App
            import Pricing
            fn total(items: List<int>, fee: int) { fee |> items.withFee }
            fn answer(): int { total([10, 20], 40) }
            """);
        var compiled = DSharpCompiler.Compile(new[] { library, application });
        Assert.True(compiled.CSharp is not null, string.Join("\n", compiled.Diagnostics));
        Assert.Empty(compiled.Diagnostics);
        await WithGenerated(compiled.CSharp!, "App", type => Assert.Equal(42, Method(type, "answer").Invoke(null, null)));

        var hidden = new SourceFile("hidden.ds", "module App\nfn bad(items: List<int>) { 40 |> items.withFee }");
        Assert.Null(DSharpCompiler.Compile(new[] { library, hidden }).CSharp);
        var other = new SourceFile("other.ds", "module Other\nextend List<int> { fn withFee(self, fee: int): int { fee } }");
        foreach (var imports in new[] { "import Pricing\nimport Other", "import Other\nimport Pricing" })
        {
            var ambiguous = new SourceFile("ambiguous.ds", "module App\n" + imports + "\nfn bad(items: List<int>) { 40 |> items.withFee }");
            var result = DSharpCompiler.Compile(new[] { library, other, ambiguous });
            Assert.Null(result.CSharp);
            Assert.NotEmpty(result.Diagnostics);
        }
    }

    private static void RejectUnprovenOrUnsafe()
    {
        string[] invalid =
        [
            // Built-in collection member names keep precedence.
            "extend List<int> { fn get(self, value: string): int { 42 } }\nfn bad(items: List<int>) { \"x\" |> items.get }",
            "extend List<int> { fn length(self, value: int): int { 42 } }\nfn bad(items: List<int>) { 2 |> items.length }",
            "extend Map<int,int> { fn get(self, value: string): int { 42 } }\nfn bad(items: Map<int,int>) { \"x\" |> items.get }",
            "extend Map<int,int> { fn set(self, value: string): int { 42 } }\nfn bad(items: Map<int,int>) { \"x\" |> items.set }",
            "extend Set<int> { fn add(self, value: string): int { 42 } }\nfn bad(items: Set<int>) { \"x\" |> items.add }",
            "extend Set<int> { fn count(self, value: int): int { 42 } }\nfn bad(items: Set<int>) { 2 |> items.count }",
            // Ambiguous overloads and unsupported dynamic calls stay conservative.
            "extend List<int> { fn fee(self, x: int): int { x }\n fn fee(self, x: int, y: int): int { x + y } }\nfn bad(xs: List<int>) { 2 |> xs.fee }",
            "extend List<int> { fn fee(self, x: int): int { x } }\nfn bad(xs: List<int>, f: (int)->int) { f(2) |> xs.fee }",
            "extend List<int> { fn fee(self, x: int): int { x } }\nfn bad(xs: List<int>) { (2 |> xs.fee)() }",
            // Track cycles through extension, input and receiver, regardless of annotations.
            "extend List<int> { fn fee(self, x: int): int { bad(self) } }\nfn bad(xs: List<int>) { 2 |> xs.fee }",
            "extend List<int> { fn fee(self, x: int): int { x } }\nfn input(xs: List<int>): int { bad(xs) }\nfn bad(xs: List<int>) { input(xs) |> xs.fee }",
            "extend List<int> { fn fee(self, x: int): int { x } }\nfn receiver(): List<int> { [bad()] }\nfn bad() { 2 |> receiver().fee }",
            // Indirect dispatch and lifetime-restricted receivers cannot be hidden.
            "interface A { get(): int }\nservice S: A { get() { 42 } }\nextend List<int> { fn fee(self, x: int): int { x } }\nfn input(): int { S().get() }\nfn bad(xs: List<int>) { input() |> xs.fee }",
            "interface A { get(): int }\nfn bad(xs: List<A>) { 2 |> xs.fee }",
            "resource Cursor { get(value: int): int { value }\n release(): Unit { () } }\nfn bad() { using c = Cursor() { 2 |> c.get } }",
            "extend MutableList<int> { fn fee(self, x: int): int { x } }\nfn bad() { let xs = MutableList<int>()\n 2 |> xs.fee }"
        ];
        foreach (var body in invalid)
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + body, "collection-extension-inference-invalid.ds");
            Assert.True(result.CSharp is null, "Expected rejection: " + body);
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, diagnostic => Assert.Equal("collection-extension-inference-invalid.ds", diagnostic.Path));
        }
    }

    private static MethodInfo Method(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;

    private static async Task WithGenerated(string csharp, string module, Action<Type> verify)
    {
        var assemblyName = "DSharpCollectionExtensionInference_" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-collection-extension-inference-" + Guid.NewGuid());
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
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated collection extension inference build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("collection-extension-inference-" + Guid.NewGuid(), isCollectible: true);
            try
            {
                var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0", assemblyName + ".dll"));
                verify(Assert.Single(assembly.GetTypes(), type => type.Name == module));
            }
            finally { context.Unload(); }
        }
        finally { Directory.Delete(directory, true); }
    }
}
