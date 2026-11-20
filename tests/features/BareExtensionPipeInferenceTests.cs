using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

public sealed class BareExtensionPipeInferenceTests
{
    public async Task BareExtensionPipesInferAndPreserveSafety()
    {
        const string source = """
            module Demo
            type Invoice { price: int }

            extend int {
                fn add(self, value: int): int { self + value }
            }
            extend Invoice {
                fn add(self, value: int): int { self.price + value }
            }

            fn input(): int { 2 }
            fn receiver(): int { 40 }
            fn literal() { 2 |> 40.add }
            fn grouped() { 2 |> (40.add) }
            fn fromCalls() { input() |> receiver().add }
            fn fromParameter(value: int) { value |> 40.add }
            fn fromRecord(invoice: Invoice) { 2 |> invoice.add }
            fn forwarding() { literal() }
            fn leftFault(): int { 1 / 0 }
            fn receiverFault(): int { 2147483647 + 1 }
            fn faultOrder() { leftFault() |> receiverFault().add }
            fn receiverFaultOnly() { 2 |> receiverFault().add }

            fn make(mark: (int)->int): Invoice {
                let ignored = mark(2)
                Invoice { price = 40 }
            }
            fn annotatedOrder(mark: (int)->int): int {
                mark(1) |> make(mark).add
            }
            fn answer(): bool {
                literal() == 42 && grouped() == 42 && fromCalls() == 42 &&
                fromParameter(2) == 42 && fromRecord(Invoice { price = 40 }) == 42 &&
                forwarding() == 42
            }
            """;
        var compilation = DSharpCompiler.Compile(source, "bare-extension-inference.ds");
        Assert.True(compilation.CSharp is not null, string.Join("\n", compilation.Diagnostics));
        Assert.Empty(compilation.Diagnostics);
        await WithGenerated(compilation.CSharp!, "Demo", type =>
        {
            Assert.Equal(true, Method(type, "answer").Invoke(null, null));
            var fault = Assert.Throws<TargetInvocationException>(() => Method(type, "faultOrder").Invoke(null, null));
            Assert.IsType<DivideByZeroException>(fault.InnerException);
            var receiverFault = Assert.Throws<TargetInvocationException>(() => Method(type, "receiverFaultOnly").Invoke(null, null));
            Assert.IsType<OverflowException>(receiverFault.InnerException);

            var events = new List<int>();
            var method = Method(type, "annotatedOrder");
            var mark = Adapt(method.GetParameters()[0].ParameterType, value => { events.Add(value); return value; });
            Assert.Equal(41, method.Invoke(null, new object[] { mark }));
            Assert.Equal(new[] { 1, 2 }, events);
        });

        await ImportedExtensions();
        RejectUnprovenOrUnsafe();
    }

    private static async Task ImportedExtensions()
    {
        var model = new SourceFile("model.ds", "module Model\ntype Invoice { price: int }");
        var pricing = new SourceFile("pricing.ds", "module Pricing\nimport Model\nextend Invoice { fn fee(self, amount: int): int { self.price + amount } }");
        var app = new SourceFile("app.ds", """
            module App
            import Model
            import Pricing
            fn total(invoice: Invoice, amount: int) { amount |> invoice.fee }
            fn answer(): int { total(Invoice { price = 40 }, 2) }
            """);
        var compilation = DSharpCompiler.Compile(new[] { model, pricing, app });
        Assert.True(compilation.CSharp is not null, string.Join("\n", compilation.Diagnostics));
        Assert.Empty(compilation.Diagnostics);
        await WithGenerated(compilation.CSharp!, "App", type =>
            Assert.Equal(42, Method(type, "answer").Invoke(null, null)));

        var hidden = new SourceFile("hidden.ds", "module App\nimport Model\nfn bad(invoice: Invoice) { 2 |> invoice.fee }");
        var hiddenResult = DSharpCompiler.Compile(new[] { model, pricing, hidden });
        Assert.Null(hiddenResult.CSharp);
        Assert.NotEmpty(hiddenResult.Diagnostics);
        var other = new SourceFile("other.ds", "module Other\nimport Model\nextend Invoice { fn fee(self, amount: int): int { self.price + amount + 1 } }");
        foreach (var imports in new[] { "import Pricing\nimport Other", "import Other\nimport Pricing" })
        {
            var ambiguous = new SourceFile("ambiguous.ds", "module App\nimport Model\n" + imports + "\nfn bad(invoice: Invoice) { 2 |> invoice.fee }");
            var result = DSharpCompiler.Compile(new[] { model, pricing, other, ambiguous });
            Assert.Null(result.CSharp);
            Assert.NotEmpty(result.Diagnostics);
        }
    }

    private static void RejectUnprovenOrUnsafe()
    {
        string[] invalid =
        [
            // A dependency in the extension body, input, or receiver must not hide a cycle.
            "extend int { fn rec(self, value: int): int { bad() } }\nfn bad() { 2 |> 40.rec }",
            "extend int { fn add(self, value: int): int { self + value } }\nfn input(): int { bad() }\nfn bad() { input() |> 40.add }",
            "extend int { fn add(self, value: int): int { self + value } }\nfn receiver(): int { bad() }\nfn bad() { 2 |> receiver().add }",
            // Unknown callable dependencies and unsupported types remain conservative.
            "extend int { fn add(self, value: int): int { self + value } }\nfn bad(callback: (int)->int) { callback(2) |> 40.add }",
            "extend int { fn add(self, value: int): int { self + value } }\nfn bad() { true |> 40.add }",
            "extend int { fn add(self, value: int): int { self + value } }\nfn bad() { (2 |> 40.add)() }",
            "extend int { fn add(self, value: int): int { self + value }\n fn add(self, first: int, second: int): int { self + first + second } }\nfn bad() { 2 |> 40.add }",
            "extend int { fn pick(self, value: string?): int { 1 }\n fn pick(self, value: int?): int { 2 } }\nfn bad() { null |> 40.pick }",
            "type Invoice { add: int }\nextend Invoice { fn add(self, amount: int): int { amount } }\nfn bad(invoice: Invoice) { 2 |> invoice.add }",
            "extend List<int> { fn get(self, value: string): int { 42 } }\nfn bad() { \"wrong\" |> [1].get }",
            // Capability, resource and mutable builder targets cannot be captured or inferred.
            "interface A { get(value: int): int }\nfn bad(value: A) { 2 |> value.get }",
            "interface A { get(): int }\nservice S: A { get() { 42 } }\nextend int { fn add(self, value: int): int { self + value } }\nfn source(): int { S().get() }\nfn bad() { source() |> 40.add }",
            "resource Cursor { get(value: int): int { value }\n release(): Unit { () } }\nfn bad() { using cursor = Cursor() { 2 |> cursor.get } }",
            "extend MutableList<int> { fn addValue(self, value: int): int { value } }\nfn bad() { let items = MutableList<int>()\n 2 |> items.addValue }"
        ];
        foreach (var body in invalid)
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + body, "bare-extension-inference-invalid.ds");
            Assert.Null(result.CSharp);
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, diagnostic => Assert.Equal("bare-extension-inference-invalid.ds", diagnostic.Path));
        }
    }

    private static MethodInfo Method(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;

    private static Delegate Adapt(Type delegateType, Func<int, int> callback)
    {
        var value = Expression.Parameter(typeof(int), "value");
        return Expression.Lambda(delegateType, Expression.Invoke(Expression.Constant(callback), value), value).Compile();
    }

    private static async Task WithGenerated(string csharp, string module, Action<Type> verify)
    {
        var assemblyName = "DSharpBareExtensionInference_" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-bare-extension-inference-" + Guid.NewGuid());
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
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated bare extension inference build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("bare-extension-inference-" + Guid.NewGuid(), isCollectible: false);
            var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0", assemblyName + ".dll"));
            verify(Assert.Single(assembly.GetTypes(), type => type.Name == module));
        }
        finally { Directory.Delete(directory, true); }
    }
}
