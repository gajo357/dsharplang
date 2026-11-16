using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class ExtensionAcceptanceTests
{
    // Run from the existing resource feature Fact to keep the bounded TRX report stable.
    public async Task ExtensionsResolveAndExecute()
    {
        const string source = """
            module Demo

            type Invoice { value: int }
            interface Number { score(value: int): int }
            service Fixed(amount: int): Number { score(value) { amount + value } }

            extend Invoice {
                fn add(self, value: int): int { self.value + value }
                fn add(self: Invoice, value: string): int { self.value + 2 }
                async fn later(self, input: Task<int>): int { self.value + await input }
            }
            extend Number {
                fn score(self, value: int): int { 0 }
                fn score(self, text: string): int { 42 }
                fn score(self, first: int, second: int): int { first + second }
            }
            extend Fixed {
                fn concreteOnly(self): int { self.score(2) }
            }
            extend List<int> {
                fn length(self, delta: int): int { self.length() + delta }
            }
            extend Map<string, int> {
                fn count(self, delta: int): int { self.count() + delta }
            }
            extend int {
                fn twice(self): int { self * 2 }
            }
            extend string? {
                fn absent(self): bool { self == null }
            }
            fn ordinary(self: int): int { self }
            fn make(mark: (int) -> int): Invoice {
                let ignored = mark(1)
                Invoice { value = 40 }
            }
            fn answer(): int {
                let invoice = Invoice { value = 40 }
                if invoice.add(2) == 42 && invoice.add("fee") == 42 && ordinary(42) == 42 { 42 } else { 0 }
            }
            fn memberPrecedence(): int {
                let number: Number = Fixed(40)
                number.score(2)
            }
            fn memberFallback(): int {
                let number: Number = Fixed(999)
                number.score("fallback")
            }
            fn staticReceiver(): int { Fixed(40).concreteOnly() }
            fn arityFallback(): int {
                let number: Number = Fixed(0)
                number.score(20, 22)
            }
            fn primitiveAndNullable(): int {
                let value = 21
                let text: string? = null
                if text.absent() { value.twice() } else { 0 }
            }
            fn mapFallback(): int {
                let items: Map<string, int> = Map.empty<string, int>()
                items.count(42)
            }
            fn collectionFallback(): int {
                let items: List<int> = [1, 2]
                items.length(40)
            }
            fn order(mark: (int) -> int): int {
                make(mark).add(mark(2))
            }
            async fn asyncAnswer(input: Task<int>): int {
                let invoice = Invoice { value = 40 }
                await invoice.later(input)
            }
            """;
        var result = DSharpCompiler.Compile(source, "extensions.ds");
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        await WithGenerated(result.CSharp!, "Demo", async type =>
        {
            Assert.Equal(42, Call<int>(type, "answer"));
            Assert.Equal(42, Call<int>(type, "memberPrecedence"));
            Assert.Equal(42, Call<int>(type, "memberFallback"));
            Assert.Equal(42, Call<int>(type, "staticReceiver"));
            Assert.Equal(42, Call<int>(type, "arityFallback"));
            Assert.Equal(42, Call<int>(type, "primitiveAndNullable"));
            Assert.Equal(42, Call<int>(type, "mapFallback"));
            Assert.Equal(42, Call<int>(type, "collectionFallback"));
            var seen = new List<int>();
            var order = Method(type, "order");
            var callback = Adapt(order.GetParameters()[0].ParameterType, value => { seen.Add(value); return value; });
            Assert.Equal(42, order.Invoke(null, new object[] { callback }));
            Assert.Equal(new[] { 1, 2 }, seen);

            var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = Call<Task<int>>(type, "asyncAnswer", gate.Task);
            Assert.False(pending.IsCompleted);
            gate.SetResult(2);
            Assert.Equal(42, await pending.WaitAsync(TimeSpan.FromSeconds(10)));
            var failure = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending = Call<Task<int>>(type, "asyncAnswer", failure.Task);
            var expected = new InvalidOperationException("extension failed");
            failure.SetException(expected);
            Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(async () => await pending));
            var cancelled = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending = Call<Task<int>>(type, "asyncAnswer", cancelled.Task);
            cancelled.SetCanceled();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
        });

        await ImportsAndVisibility();
        RejectInvalidExtensions();
    }

    private static async Task ImportsAndVisibility()
    {
        var model = new SourceFile("model.ds", "module Model\ntype Invoice { value: int }");
        var extension = new SourceFile("extension.ds", """
            module Pricing
            import Model
            extend Invoice {
                fn withFee(self, fee: int): int { self.value + fee }
            }
            """);
        var app = new SourceFile("app.ds", """
            module App
            import Model
            import Pricing
            import Pricing
            fn answer(): int {
                let value = Invoice { value = 40 }
                value.withFee(2)
            }
            """);
        var result = DSharpCompiler.Compile(new[] { model, extension, app });
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        await WithGenerated(result.CSharp!, "App", type =>
        {
            Assert.Equal(42, Call<int>(type, "answer"));
            return Task.CompletedTask;
        });

        var transit = new SourceFile("transit.ds", "module Transit\nimport Pricing\nfn noop(): int { 0 }");
        var appThroughTransit = new SourceFile("transitive.ds", """
            module App
            import Model
            import Transit
            fn answer(): int {
                let value = Invoice { value = 40 }
                value.withFee(2)
            }
            """);
        AssertRejected(new[] { model, extension, transit, appThroughTransit }, "transitive import exposed an extension");

        var other = new SourceFile("other.ds", """
            module Other
            import Model
            extend Invoice { fn withFee(self, fee: int): int { self.value + fee + 1 } }
            """);
        SourceFile AmbiguousApp(bool reverse) => new("ambiguous.ds",
            "module App\nimport Model\nimport " + (reverse ? "Other" : "Pricing") +
            "\nimport " + (reverse ? "Pricing" : "Other") +
            "\nfn answer(): int { let value = Invoice { value = 40 }\n value.withFee(2) }");
        foreach (var reverse in new[] { false, true })
        {
            var ambiguous = DSharpCompiler.Compile(new[] { model, extension, other, AmbiguousApp(reverse) });
            Assert.Null(ambiguous.CSharp);
            Assert.NotEmpty(ambiguous.Diagnostics);
            var messages = string.Join("\n", ambiguous.Diagnostics.Select(d => d.Message));
            Assert.Contains("Pricing", messages, StringComparison.Ordinal);
            Assert.Contains("Other", messages, StringComparison.Ordinal);
        }

        // Imports are file-scoped even among files contributing to the same module.
        var visibleInOne = new SourceFile("visible.ds", """
            module App
            import Model
            import Pricing
            fn visible(): int { let value = Invoice { value = 40 }
                value.withFee(2) }
            """);
        var missingInOther = new SourceFile("missing.ds", """
            module App
            import Model
            fn missing(): int { let value = Invoice { value = 40 }
                value.withFee(2) }
            """);
        AssertRejected(new[] { model, extension, visibleInOne, missingInOther }, "an import leaked between files");

        // Two declarations in the same module remain distinct and identify both origins.
        var sameOne = new SourceFile("same-one.ds", """
            module App
            import Model
            extend Invoice { fn withFee(self, fee: int): int { self.value + fee } }
            """);
        var sameTwo = new SourceFile("same-two.ds", """
            module App
            import Model
            extend Invoice { fn withFee(self, fee: int): int { self.value + fee + 1 } }
            """);
        var sameCall = new SourceFile("same-call.ds", """
            module App
            import Model
            fn answer(): int { let value = Invoice { value = 40 }
                value.withFee(2) }
            """);
        var sameAmbiguous = DSharpCompiler.Compile(new[] { model, sameOne, sameTwo, sameCall });
        Assert.Null(sameAmbiguous.CSharp);
        Assert.NotEmpty(sameAmbiguous.Diagnostics);
        var sameMessages = string.Join("\n", sameAmbiguous.Diagnostics.Select(d => d.Message));
        Assert.Contains("same-one.ds", sameMessages, StringComparison.Ordinal);
        Assert.Contains("same-two.ds", sameMessages, StringComparison.Ordinal);

        var localConflict = new SourceFile("local.ds", """
            module App
            import Model
            import Pricing
            extend Invoice { fn withFee(self, fee: int): int { self.value + fee } }
            fn answer(): int {
                let value = Invoice { value = 40 }
                value.withFee(2)
            }
            """);
        AssertRejected(new[] { model, extension, localConflict }, "local/import ambiguity silently selected a winner");
    }

    private static void RejectInvalidExtensions()
    {
        var invalid = new[]
        {
            // The contextual receiver must be first, unique and correctly typed.
            "extend Invoice { fn bad(value: int): int { value } }",
            "extend Invoice { fn bad(value: int, self): int { value } }",
            "extend Invoice { fn bad(self, self: int): int { 1 } }",
            "extend Invoice { fn bad(self: int): int { self } }",
            "extend Invoice { fn bad(self) { 1 } }",
            "extend Invoice { value: int }",
            "extend Invoice: Other { fn bad(self): int { 1 } }",
            // Calls are exact, not implicit conversions or method values/partials.
            "extend Invoice { fn add(self, value: int): int { self.value + value } }\nfn bad(): int { let item = Invoice { value = 40 }\n item.add(\"two\") }",
            "extend Invoice { fn add(self, value: int): int { self.value + value } }\nfn bad(): Unit { let item = Invoice { value = 40 }\n let callable = item.add\n () }",
            "extend Invoice { fn add(self, value: int): int { self.value + value } }\nfn bad(): Unit { let item = Invoice { value = 40 }\n let callable = item.add()\n () }",
            "extend Invoice { fn add<T>(self, value: T): int { self.value } }",
            // Compiler-generated extension helpers are not source-callable or first-class.
            "extend Invoice { fn add(self, value: int): int { self.value + value } }\nfn bad(): int { __dsharp_extension_0(Invoice { value = 40 }, 2) }",
            "extend Invoice { fn add(self, value: int): int { self.value + value } }\nfn bad(): Unit { let f = __dsharp_extension_0\n () }",
            "extend Invoice { fn add(self, value: int): int { self.value + value } }\nfn bad(): Unit { let f = __dsharp_extension_0(Invoice { value = 40 })\n () }",
            // Static receiver type decides applicability and capabilities cannot be captured.
            "interface Number { score(value: int): int }\nservice Fixed: Number { score(value) { value } }\nextend Fixed { fn only(self): int { 42 } }\nfn bad(n: Number): int { n.only() }",
            "interface Number { score(value: int): int }\nextend Number { fn leak(self): () -> int { () => self.score(1) } }",
            // A matching real factory must not fall back to an extension based on result context.
            "resource Cursor { read(): int { 42 }\n release(): Unit { () } }\ninterface Store { open(): Cursor }\nextend Store { fn open(self): int { 42 } }\nfn bad(s: Store): int { s.open() }",
            // Untyped null cannot silently select one of multiple exact candidates.
            "extend Invoice { fn pick(self, value: int?): int { 1 }\n fn pick(self, value: string?): int { 2 } }\nfn bad(): int { let item = Invoice { value = 40 }\n item.pick(null) }",
            // Resource handles cannot be passed as first-class receiver values.
            "resource Cursor { read(): int { 42 }\n release(): Unit { () } }\nextend Cursor { fn copied(self): int { self.read() } }\nfn bad(): int { using c = Cursor() { c.copied() } }"
        };
        foreach (var body in invalid)
        {
            var result = DSharpCompiler.Compile("module Invalid\ntype Invoice { value: int }\n" + body, "invalid-extension.ds");
            Assert.True(result.CSharp is null, $"Invalid extension program compiled: {body}");
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, d => Assert.Equal("invalid-extension.ds", d.Path));
        }
    }

    private static void AssertRejected(IReadOnlyList<SourceFile> sources, string reason)
    {
        var result = DSharpCompiler.Compile(sources);
        Assert.True(result.CSharp is null, reason);
        Assert.NotEmpty(result.Diagnostics);
    }

    private static MethodInfo Method(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
    private static T Call<T>(Type type, string name, params object?[] args) => (T)Method(type, name).Invoke(null, args)!;

    private static Delegate Adapt(Type type, Func<int, int> action)
    {
        var value = Expression.Parameter(typeof(int), "value");
        return Expression.Lambda(type, Expression.Invoke(Expression.Constant(action), value), value).Compile();
    }

    private static async Task WithGenerated(string csharp, string module, Func<Type, Task> verify)
    {
        var assemblyName = "DSharpExtensions_" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-extensions-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "Generated.cs"), csharp);
            File.WriteAllText(Path.Combine(directory, "Generated.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net11.0</TargetFramework><AssemblyName>" + assemblyName + "</AssemblyName><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>");
            File.WriteAllText(Path.Combine(directory, "global.json"), "{\"sdk\":{\"version\":\"11.0.100\",\"rollForward\":\"disable\"}}");
            File.WriteAllText(Path.Combine(directory, "NuGet.Config"), "<configuration><packageSources><clear/></packageSources></configuration>");
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "build", "Generated.csproj", "--nologo", "--verbosity", "quiet" }) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated extension build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("extensions-" + Guid.NewGuid(), isCollectible: false);
            var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0", assemblyName + ".dll"));
            var type = Assert.Single(assembly.GetTypes(), candidate => candidate.Name == module);
            await verify(type);
        }
        finally { Directory.Delete(directory, true); }
    }
}
