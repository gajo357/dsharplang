using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class BooleanTests
{
    [Fact]
    public async Task BooleansComparisonsAndCallsExecute()
    {
        const string source = """
            module Demo
            fn truth(): bool { true }
            fn falsehood(): bool { false }
            fn identity(value: bool): bool { value }
            fn both(a: bool, b: bool): bool { a && b }
            fn local(a: int): bool {
                let positive: bool = a > 0
                let zero = a == 0
                positive || zero
            }
            fn comparison(): bool { 3 * 2 + 1 == 7 && 1 < 2 && 4 >= 4 }
            fn precedence(): bool { true || false && false }
            fn negation(): bool { !false && !(3 != 3) }
            fn equality(): bool { (1 <= 2) == true && false != true }
            fn ordering(): bool { 1 < 2 && 2 <= 2 && 3 > 2 && 3 >= 3 }
            fn nested(): bool { identity(both(true, identity(true))) }
            fn unequal(): bool { 1 != 2 && 2 == 2 }
            """;
        await WithGenerated(source, type =>
        {
            Assert.Equal(true, Invoke(type, "truth"));
            Assert.Equal(false, Invoke(type, "falsehood"));
            Assert.Equal(false, Invoke(type, "identity", false));
            Assert.Equal(true, Invoke(type, "identity", true));
            Assert.Equal(false, Invoke(type, "both", true, false));
            Assert.Equal(true, Invoke(type, "both", true, true));
            Assert.Equal(true, Invoke(type, "local", 0));
            Assert.Equal(true, Invoke(type, "local", 4));
            Assert.Equal(false, Invoke(type, "local", -2));
            foreach (var name in new[] { "comparison", "precedence", "negation", "equality", "ordering", "nested", "unequal" })
                Assert.Equal(true, Invoke(type, name));
        });
    }

    [Fact]
    public async Task LogicalOperatorsShortCircuitFaultingOperands()
    {
        const string source = """
            module Demo
            fn dangerous(): bool { 1 / 0 == 0 }
            fn skippedAnd(): bool { false && dangerous() }
            fn skippedOr(): bool { true || dangerous() }
            fn nested(): bool { (true || dangerous()) && !(false && dangerous()) }
            fn directAnd(): bool { false && 1 / 0 == 0 }
            fn directOr(): bool { true || 1 / 0 == 0 }
            fn executeAnd(): bool { true && dangerous() }
            fn executeOr(): bool { false || dangerous() }
            fn arg(a: bool): bool { a }
            fn callArgument(): bool { arg(true || dangerous()) }
            """;
        await WithGenerated(source, type =>
        {
            Assert.Equal(false, Invoke(type, "skippedAnd"));
            Assert.Equal(true, Invoke(type, "skippedOr"));
            Assert.Equal(true, Invoke(type, "nested"));
            Assert.Equal(false, Invoke(type, "directAnd"));
            Assert.Equal(true, Invoke(type, "directOr"));
            Assert.Equal(true, Invoke(type, "callArgument"));
            foreach (var name in new[] { "executeAnd", "executeOr" })
                Assert.IsType<DivideByZeroException>(Assert.Throws<TargetInvocationException>(() => Invoke(type, name)).InnerException);
        });
    }

    [Theory]
    [InlineData("fn answer(): int { 1 + true }")]
    [InlineData("fn answer(): bool { !1 }")]
    [InlineData("fn answer(): bool { true < false }")]
    [InlineData("fn answer(): bool { 1 == true }")]
    [InlineData("fn answer(): bool { true && 1 }")]
    [InlineData("fn answer(): bool { false || 0 }")]
    [InlineData("fn answer(): bool { 1 < 2 < 3 }")]
    [InlineData("fn answer(): bool { true == false == true }")]
    [InlineData("fn answer(): int { true }")]
    [InlineData("fn answer(): bool { 1 }")]
    [InlineData("fn answer(): bool {\n let value: int = true\n value == 0\n}")]
    [InlineData("fn answer(): bool {\n let value: bool = 1\n value\n}")]
    [InlineData("fn identity(value: bool): bool { value }\nfn answer(): bool { identity(2) }")]
    [InlineData("fn identity(value: int): int { value }\nfn answer(): int { identity(false) }")]
    [InlineData("fn answer(): bool { true & false }")]
    [InlineData("fn answer(): bool { true | false }")]
    public void InvalidOperandsTypesAndChainedComparisonsHaveSourceDiagnostics(string body)
    {
        var source = "module Demo\n" + body;
        var result = DSharpCompiler.Compile(source, "boolean-invalid.ds");
        Assert.Null(result.CSharp);
        var error = Assert.Single(result.Diagnostics);
        Assert.StartsWith("DS", error.Code);
        Assert.Equal("boolean-invalid.ds", error.Path);
        Assert.InRange(error.Span.Start, 0, source.Length);
        Assert.True(error.Span.Length > 0);
        var prefix = source[..error.Span.Start];
        Assert.Equal(prefix.Count(c => c == '\n') + 1, error.Span.Line);
        Assert.Equal(error.Span.Start - prefix.LastIndexOf('\n'), error.Span.Column);
    }

    private static object? Invoke(Type type, string name, params object[] arguments) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.Invoke(null, arguments);

    private static async Task WithGenerated(string source, Action<Type> verify)
    {
        var compilation = DSharpCompiler.Compile(source, "boolean.ds");
        Assert.Empty(compilation.Diagnostics);
        Assert.Equal(compilation.CSharp, DSharpCompiler.Compile(source).CSharp);
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-bool-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "Generated.cs"), compilation.CSharp);
            File.WriteAllText(Path.Combine(directory, "Generated.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
            File.WriteAllText(Path.Combine(directory, "global.json"), "{\"sdk\":{\"version\":\"10.0.100\",\"rollForward\":\"disable\"}}");
            File.WriteAllText(Path.Combine(directory, "NuGet.Config"), "<configuration><packageSources><clear/></packageSources></configuration>");
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "build", "Generated.csproj", "--nologo", "--verbosity", "quiet" }) start.ArgumentList.Add(arg);
            using var build = Process.Start(start)!;
            var stdout = build.StandardOutput.ReadToEndAsync();
            var stderr = build.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try { await build.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { build.Kill(true); throw new TimeoutException("Generated build timed out."); }
            Assert.True(build.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("boolean-" + Guid.NewGuid(), isCollectible: true);
            try
            {
                var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net10.0/Generated.dll"));
                verify(assembly.GetType("DSharp.Generated.Demo")!);
            }
            finally { context.Unload(); }
        }
        finally { Directory.Delete(directory, true); }
    }
}
