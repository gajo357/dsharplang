using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

public sealed class AsyncExtensionPipeInferenceTests
{
    public async Task AsyncExtensionPipesInferTasksAndPreserveSafety()
    {
        const string source = """
            module Demo
            type Invoice { amount: int }

            extend Invoice {
                async fn withFee(self, fee: int): int { self.amount + fee }
                async fn fail(self, fee: int): int { self.amount / fee }
                async fn maybe(self, flag: int): int? { if flag == 0 { null } else { self.amount } }
            }
            extend List<int> {
                async fn withFee(self, fee: int): int { self.length() + fee }
            }
            fn total(invoice: Invoice, fee: int) { fee |> invoice.withFee }
            fn grouped(invoice: Invoice) { 2 |> (invoice.withFee) }
            fn collection(items: List<int>) { 40 |> items.withFee }
            fn annotated(invoice: Invoice): Task<int> { 2 |> invoice.withFee }
            fn fault(invoice: Invoice) { 0 |> invoice.fail }
            fn maybe(invoice: Invoice, flag: int) { flag |> invoice.maybe }
            fn directlyFaulted() { 0 |> Invoice { amount = 40 }.fail }
            fn input(): int { 2 }
            fn receiver(): Invoice { Invoice { amount = 40 } }
            fn fromCalls() { input() |> receiver().withFee }
            fn failingInput(): int { 1 / 0 }
            fn failingReceiver(): Invoice {
                let number = 2147483647 + 1
                Invoice { amount = number }
            }
            fn faultOrder() { failingInput() |> failingReceiver().withFee }
            fn receiverFault() { 2 |> failingReceiver().withFee }
            async fn answer(): int { await total(Invoice { amount = 40 }, 2) }
            async fn collectionAnswer(): int { await collection([10, 20]) }
            async fn groupedAnswer(): int { await grouped(Invoice { amount = 40 }) }
            async fn annotatedAnswer(): int { await annotated(Invoice { amount = 40 }) }
            async fn faultAnswer(): int { await fault(Invoice { amount = 40 }) }
            async fn nullableAnswer(): int {
                let value = await maybe(Invoice { amount = 40 }, 0)
                value ?? 42
            }
            """;
        var compiled = DSharpCompiler.Compile(source, "async-extension-inference.ds");
        Assert.True(compiled.CSharp is not null, string.Join("\n", compiled.Diagnostics));
        Assert.Empty(compiled.Diagnostics);
        await WithGenerated(compiled.CSharp!, "Demo", async type =>
        {
            Assert.Equal(typeof(Task<int>), Method(type, "fromCalls").ReturnType);
            Assert.Equal(typeof(Task<int>), Method(type, "total").ReturnType);
            Assert.Equal(typeof(Task<int>), Method(type, "collection").ReturnType);
            Assert.Equal(42, await CallTask(type, "fromCalls"));
            Assert.Equal(42, await CallTask(type, "answer"));
            Assert.Equal(42, await CallTask(type, "collectionAnswer"));
            Assert.Equal(42, await CallTask(type, "groupedAnswer"));
            Assert.Equal(42, await CallTask(type, "annotatedAnswer"));
            Assert.Equal(42, await CallTask(type, "nullableAnswer"));
            var task = CallTask(type, "faultAnswer");
            await Assert.ThrowsAsync<DivideByZeroException>(async () => await task);
            var directFault = CallTask(type, "directlyFaulted");
            await Assert.ThrowsAsync<DivideByZeroException>(async () => await directFault);
            var inputFault = Assert.Throws<TargetInvocationException>(() => Method(type, "faultOrder").Invoke(null, null));
            Assert.IsType<DivideByZeroException>(inputFault.InnerException);
            var receiverFault = Assert.Throws<TargetInvocationException>(() => Method(type, "receiverFault").Invoke(null, null));
            Assert.IsType<OverflowException>(receiverFault.InnerException);
        });

        await ImportedExtensions();
        RejectUnprovenOrUnsafe();
    }

    private static async Task ImportedExtensions()
    {
        var pricing = new SourceFile("pricing.ds", """
            module Pricing
            extend int {
                async fn withFee(self, fee: int): int { self + fee }
            }
            """);
        var app = new SourceFile("app.ds", """
            module App
            import Pricing
            fn total(amount: int, fee: int) { fee |> amount.withFee }
            async fn answer(): int { await total(40, 2) }
            """);
        var compiled = DSharpCompiler.Compile(new[] { pricing, app });
        Assert.True(compiled.CSharp is not null, string.Join("\n", compiled.Diagnostics));
        Assert.Empty(compiled.Diagnostics);
        await WithGenerated(compiled.CSharp!, "App", async type =>
            Assert.Equal(42, await CallTask(type, "answer")));

        var hidden = new SourceFile("hidden.ds", "module App\nfn bad() { 2 |> 40.withFee }");
        Assert.Null(DSharpCompiler.Compile(new[] { pricing, hidden }).CSharp);
        var other = new SourceFile("other.ds", "module Other\nextend int { async fn withFee(self, fee: int): int { self + fee } }");
        foreach (var imports in new[] { "import Pricing\nimport Other", "import Other\nimport Pricing" })
        {
            var ambiguous = new SourceFile("ambiguous.ds", "module App\n" + imports + "\nfn bad() { 2 |> 40.withFee }");
            Assert.Null(DSharpCompiler.Compile(new[] { pricing, other, ambiguous }).CSharp);
        }
    }

    private static void RejectUnprovenOrUnsafe()
    {
        string[] invalid =
        [
            // Cycles through target, input or receiver remain conservative.
            "extend int { async fn rec(self, x: int): int { await bad() } }\nfn bad() { 2 |> 40.rec }",
            "extend int { async fn add(self, x: int): int { self + x } }\nfn input(): int { let pending = bad()\n 2 }\nfn bad() { input() |> 40.add }",
            "extend int { async fn add(self, x: int): int { self + x } }\nfn receiver(): int { let pending = bad()\n 40 }\nfn bad() { 2 |> receiver().add }",
            // No unchecked overload selection, intrinsic shadowing or double call.
            "extend int { async fn add(self, x: int): int { x }\n async fn add(self, x: int, y: int): int { x + y } }\nfn bad() { 2 |> 40.add }",
            "extend int { async fn add(self, x: int): int { x } }\nfn bad() { (2 |> 40.add)() }",
            "extend int { async fn add(self, x: int): int { x } }\nfn bad(f: (int)->int) { f(2) |> 40.add }",
            "extend int { async fn add(self, x: int): int { x } }\nfn bad(f: (int)->int) { 2 |> f(40).add }",
            "extend List<int> { async fn get(self, key: string): int { 42 } }\nfn bad(xs: List<int>) { \"x\" |> xs.get }",
            // Tasks cannot be hidden inside immutable data or lifetime-limited values.
            "extend int { async fn add(self, x: int): int { x } }\nfn bad(): List<Task<int>> { [2 |> 40.add] }",
            "extend int { async fn add(self, x: int): int { x } }\nfn bad(): Option<Task<int>> { Some(2 |> 40.add) }",
            "interface A { get(): int }\nservice S: A { get() { 42 } }\nextend int { async fn add(self, x: int): int { x } }\nfn input(): int { S().get() }\nfn bad() { input() |> 40.add }",
            "resource Cursor { get(value: int): int { value }\n release(): Unit { () } }\nfn bad() { using cursor = Cursor() { 2 |> cursor.get } }",
            "extend MutableList<int> { async fn addValue(self, x: int): int { x } }\nfn bad() { let xs = MutableList<int>()\n 2 |> xs.addValue }",
            // Explicit logical return annotations remain required on async functions.
            "async fn bad() { 42 }"
        ];
        foreach (var body in invalid)
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + body, "async-extension-inference-invalid.ds");
            Assert.True(result.CSharp is null, "Expected rejection: " + body);
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, diagnostic => Assert.Equal("async-extension-inference-invalid.ds", diagnostic.Path));
        }
    }

    private static MethodInfo Method(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
    private static Task<int> CallTask(Type type, string name) => (Task<int>)Method(type, name).Invoke(null, null)!;

    private static async Task WithGenerated(string csharp, string module, Func<Type, Task> verify)
    {
        var assemblyName = "DSharpAsyncExtensionInference_" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-async-extension-inference-" + Guid.NewGuid());
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
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated async extension inference build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("async-extension-inference-" + Guid.NewGuid(), isCollectible: true);
            try
            {
                var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0", assemblyName + ".dll"));
                await verify(Assert.Single(assembly.GetTypes(), type => type.Name == module));
            }
            finally { context.Unload(); }
        }
        finally { Directory.Delete(directory, true); }
    }
}
