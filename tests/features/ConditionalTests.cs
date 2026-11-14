using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class ConditionalTests
{
    [Fact]
    public async Task ConditionalExpressionsExecuteWithIndependentNestedScopes()
    {
        const string source = """
            module Demo
            fn choose(flag: bool): int {
                if flag {
                    let value = 42
                    value
                } else {
                    let value = 7
                    value
                }
            }
            fn nested(a: bool, b: bool): int {
                let outer = 40
                if a {
                    let left = 2
                    if b { outer + left } else { outer - left }
                } else {
                    let right = 3
                    outer + right
                }
            }
            fn boolResult(a: int): bool {
                if a > 0 { true } else { false }
            }
            fn localInitializer(flag: bool): int {
                let picked = if flag {
                    let n = 40
                    n + 2
                } else {
                    7
                }
                picked
            }
            fn identity(value: int): int { value }
            fn asArgument(flag: bool): int { identity(if flag { 42 } else { 7 }) }
            fn inArithmetic(flag: bool): int { (if flag { 20 } else { 1 }) * 2 + 2 }
            fn afterBlock(flag: bool): int {
                let chosen = if flag {
                    let temporary = 2
                    temporary
                } else { 7 }
                let temporary = 40
                temporary + chosen
            }
            fn sum(a: int, b: int): int { a + b }
            fn multilineArithmetic(flag: bool): int {
                let result = (if flag {
                    39
                } else {
                    4
                }) + 3
                result
            }
            fn multilineArguments(flag: bool): int {
                let result = sum(if flag {
                    40
                } else {
                    5
                }, 2)
                result
            }
            fn parameterNameCollision(flag: bool): int {
                if flag {
                    let __dsharp_if_0 = 42
                    __dsharp_if_0
                } else { 7 }
            }
            """;
        await WithGenerated(source, type =>
        {
            Assert.Equal(42, Invoke(type, "choose", true));
            Assert.Equal(7, Invoke(type, "choose", false));
            Assert.Equal(42, Invoke(type, "nested", true, true));
            Assert.Equal(38, Invoke(type, "nested", true, false));
            Assert.Equal(43, Invoke(type, "nested", false, true));
            Assert.Equal(true, Invoke(type, "boolResult", 1));
            Assert.Equal(false, Invoke(type, "boolResult", 0));
            Assert.Equal(42, Invoke(type, "localInitializer", true));
            Assert.Equal(7, Invoke(type, "localInitializer", false));
            Assert.Equal(42, Invoke(type, "asArgument", true));
            Assert.Equal(7, Invoke(type, "asArgument", false));
            Assert.Equal(42, Invoke(type, "inArithmetic", true));
            Assert.Equal(4, Invoke(type, "inArithmetic", false));
            Assert.Equal(42, Invoke(type, "afterBlock", true));
            Assert.Equal(47, Invoke(type, "afterBlock", false));
            Assert.Equal(42, Invoke(type, "multilineArithmetic", true));
            Assert.Equal(7, Invoke(type, "multilineArithmetic", false));
            Assert.Equal(42, Invoke(type, "multilineArguments", true));
            Assert.Equal(7, Invoke(type, "multilineArguments", false));
            Assert.Equal(42, Invoke(type, "parameterNameCollision", true));
        });
    }

    [Fact]
    public async Task ConditionalAndLogicalBranchesAreLazy()
    {
        const string source = """
            module Demo
            fn dangerous(): bool { 1 / 0 == 0 }
            fn takeFirst(a: int, b: int): int { a }
            fn leftFirst(): int { takeFirst(1 / 0, if (2147483647 + 1) > 0 { 1 } else { 2 }) }
            fn overflowFirst(): int { takeFirst(2147483647 + 1, if (1 / 0) > 0 { 1 } else { 2 }) }
            fn branch(flag: bool): int { if flag { 42 } else { 1 / 0 } }
            fn reverse(flag: bool): int { if flag { 1 / 0 } else { 42 } }
            fn andSkipped(): bool { false && (if dangerous() { true } else { false }) }
            fn orSkipped(): bool { true || (if dangerous() { true } else { false }) }
            fn andRuns(): bool { true && (if dangerous() { true } else { false }) }
            fn orRuns(): bool { false || (if dangerous() { true } else { false }) }
            fn nestedSkipped(): bool {
                if false { dangerous() } else { if true { true } else { dangerous() } }
            }
            fn checkedBranch(flag: bool): int {
                if flag { 42 } else {
                    let overflow = 2147483647 + 1
                    overflow
                }
            }
            fn branchWithLocal(flag: bool): int {
                if flag { 42 } else {
                    let bad = 1 / 0
                    bad
                }
            }
            """;
        await WithGenerated(source, type =>
        {
            Assert.Equal(42, Invoke(type, "branch", true));
            Assert.Equal(42, Invoke(type, "reverse", false));
            Assert.Equal(false, Invoke(type, "andSkipped"));
            Assert.Equal(true, Invoke(type, "orSkipped"));
            Assert.Equal(true, Invoke(type, "nestedSkipped"));
            Assert.Equal(42, Invoke(type, "branchWithLocal", true));
            Assert.IsType<DivideByZeroException>(Assert.Throws<TargetInvocationException>(() => Invoke(type, "leftFirst")).InnerException);
            Assert.IsType<OverflowException>(Assert.Throws<TargetInvocationException>(() => Invoke(type, "overflowFirst")).InnerException);
            Assert.Equal(42, Invoke(type, "checkedBranch", true));
            Assert.IsType<OverflowException>(Assert.Throws<TargetInvocationException>(() => Invoke(type, "checkedBranch", false)).InnerException);
            foreach (var (name, args) in new (string, object[])[] {
                ("branch", [false]), ("reverse", [true]), ("andRuns", []),
                ("orRuns", []), ("branchWithLocal", [false])
            })
                Assert.IsType<DivideByZeroException>(Assert.Throws<TargetInvocationException>(() => Invoke(type, name, args)).InnerException);
        });
    }

    [Theory]
    [InlineData("fn answer(): int { if |1 { 2 } else { 3 } }")]
    [InlineData("fn answer(): int { if true { 2 } |}")]
    [InlineData("fn answer(): int { if true { 2 } else { |true } }")]
    [InlineData("fn answer(): bool { if true { true } else { |1 } }")]
    [InlineData("fn answer(): int { if true { |} else { 3 } }")]
    [InlineData("fn answer(): int { if true { 2 } else { |} }")]
    [InlineData("fn answer(): int { if true {\n let hidden = 42\n hidden\n} else { |hidden } }")]
    [InlineData("fn answer(flag: bool): int {\n let outer = 1\n if flag {\n  let |outer = 2\n  outer\n } else { 0 }\n}")]
    [InlineData("fn answer(flag: bool): int { if flag {\n let |flag = 1\n flag\n} else { 0 } }")]
    [InlineData("fn answer(flag: bool): int {\n let x = if flag {\n  let hidden = 1\n  hidden\n } else { 2 }\n |hidden\n}")]
    [InlineData("fn answer(): int { if true {\n let x = 1\n if true {\n  let |x = 2\n  x\n } else { 0 }\n} else { 0 } }")]
    public void InvalidConditionalsHaveDSharpSourceDiagnostics(string marked)
    {
        var markedSource = "module Demo\n" + marked;
        var start = markedSource.IndexOf('|');
        var source = markedSource.Remove(start, 1);
        var result = DSharpCompiler.Compile(source, "conditional-invalid.ds");
        Assert.Null(result.CSharp);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.StartsWith("DS", diagnostic.Code);
        Assert.Equal("conditional-invalid.ds", diagnostic.Path);
        Assert.True(diagnostic.Span.Start <= start && diagnostic.Span.Start + diagnostic.Span.Length >= start,
            $"Expected diagnostic near offset {start}, got {diagnostic.Span} ({diagnostic.Message})");
        var prefix = source[..diagnostic.Span.Start];
        Assert.Equal(prefix.Count(c => c == '\n') + 1, diagnostic.Span.Line);
        Assert.Equal(diagnostic.Span.Start - prefix.LastIndexOf('\n'), diagnostic.Span.Column);
    }

    private static object? Invoke(Type type, string name, params object[] arguments) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.Invoke(null, arguments);

    private static async Task WithGenerated(string source, Action<Type> verify)
    {
        var compilation = DSharpCompiler.Compile(source, "conditional.ds");
        Assert.Empty(compilation.Diagnostics);
        Assert.Equal(compilation.CSharp, DSharpCompiler.Compile(source).CSharp);
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-conditional-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "Generated.cs"), compilation.CSharp);
            File.WriteAllText(Path.Combine(directory, "Generated.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net11.0</TargetFramework><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>");
            File.WriteAllText(Path.Combine(directory, "global.json"), "{\"sdk\":{\"version\":\"11.0.100\",\"rollForward\":\"disable\"}}");
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
            var context = new AssemblyLoadContext("conditional-" + Guid.NewGuid(), isCollectible: true);
            try
            {
                var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0/Generated.dll"));
                verify(assembly.GetType("DSharp.Generated.Demo")!);
            }
            finally { context.Unload(); }
        }
        finally { Directory.Delete(directory, true); }
    }
}
