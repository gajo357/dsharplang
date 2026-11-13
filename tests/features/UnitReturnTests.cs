using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class UnitReturnTests
{
    [Fact]
    public async Task UnitIsAValueAcrossEmptyBodiesLocalsParametersAndCalls()
    {
        const string source = """
            module Demo
            fn __DSharp_Unit(): Unit { () }
            fn __DSharp_ReturnSignal(): int { return 42 }
            fn __DSharp_Raise(): int { return 42 }
            fn empty(): Unit { }
            fn explicitValue(): Unit { () }
            fn bareReturn(): Unit { return }
            fn returnValue(): Unit { return () }
            fn identity(value: Unit): Unit { value }
            fn throughCall(): Unit { identity(()) }
            fn inferredLocal(): Unit {
                let value = ()
                identity(value)
            }
            fn unusedUnit(): Unit {
                let unused = ()
                ()
            }
            fn typedLocal(): Unit {
                let value: Unit = ()
                value
            }
            fn emptyBranch(flag: bool): Unit { if flag { } }
            fn sequenced(flag: bool): Unit {
                if flag { identity(()) }
                empty()
                ()
            }
            """;
        await WithGenerated(source, type =>
        {
            var value = Invoke(type, "empty") ?? throw new InvalidOperationException("Unit must not be null.");
            Assert.True(value.GetType().IsValueType, "Unit must have value semantics.");
            foreach (var name in new[] { "explicitValue", "bareReturn", "returnValue", "throughCall", "inferredLocal", "unusedUnit", "typedLocal", "__DSharp_Unit" })
            {
                var result = Invoke(type, name);
                Assert.NotNull(result);
                Assert.Equal(value.GetType(), result!.GetType());
                Assert.Equal(value, result);
            }
            Assert.Equal(42, Invoke(type, "__DSharp_ReturnSignal"));
            Assert.Equal(42, Invoke(type, "__DSharp_Raise"));
            Assert.Equal(value, Invoke(type, "identity", value));
            Assert.Equal(value, Invoke(type, "emptyBranch", true));
            Assert.Equal(value, Invoke(type, "emptyBranch", false));
            Assert.Equal(value, Invoke(type, "sequenced", true));
            Assert.Equal(value, Invoke(type, "sequenced", false));
        });
    }

    [Fact]
    public async Task ReturnsLeaveTheNearestFunctionAndSkipRemainingEvaluation()
    {
        const string source = """
            module Demo
            fn early(flag: bool): int {
                if flag { return 42 }
                7
            }
            fn both(flag: bool): int {
                if flag { return 42 } else { return 7 }
            }
            fn oneBranch(flag: bool): int {
                if flag { return 42 } else { 7 }
            }
            fn nested(): int {
                let value = if true {
                    if true { return 42 } else { 1 }
                } else { 2 }
                value + (1 / 0)
            }
            fn operand(): int {
                let value = (if true { return 42 } else { 1 }) + (1 / 0)
                value
            }
            fn callArgument(): int { add(if true { return 42 } else { 0 }, 1 / 0) }
            fn add(left: int, right: int): int { left + right }
            fn callee(): int { return 41 }
            fn caller(): int { callee() + 1 }
            fn unitEarly(): Unit {
                return ()
                let never = 1 / 0
                if never == 0 { () } else { () }
            }
            fn overflowBeforeReturn(): int { (2147483647 + 1) + (if true { return 42 } else { 0 }) }
            fn skippedLogicalReturn(): bool {
                if false && (if true { return true } else { false }) { return false }
                true
            }
            """;
        await WithGenerated(source, type =>
        {
            Assert.Equal(42, Invoke(type, "early", true));
            Assert.Equal(7, Invoke(type, "early", false));
            Assert.Equal(42, Invoke(type, "both", true));
            Assert.Equal(7, Invoke(type, "both", false));
            Assert.Equal(42, Invoke(type, "oneBranch", true));
            Assert.Equal(7, Invoke(type, "oneBranch", false));
            Assert.Equal(42, Invoke(type, "nested"));
            Assert.Equal(42, Invoke(type, "operand"));
            Assert.Equal(42, Invoke(type, "callArgument"));
            Assert.Equal(42, Invoke(type, "caller"));
            Assert.NotNull(Invoke(type, "unitEarly"));
            Assert.Equal(true, Invoke(type, "skippedLogicalReturn"));
            var error = Assert.Throws<TargetInvocationException>(() => Invoke(type, "overflowBeforeReturn"));
            Assert.IsType<OverflowException>(error.InnerException);
        });
    }

    [Fact]
    public async Task NonCompletingExpressionsCoerceAcrossNestedContexts()
    {
        const string source = """
            module Demo
            fn intIdentity(value: int): int { value }
            fn textIdentity(value: string): string { value }
            fn boolToText(value: bool): string { "unused" }
            fn unary(): int {
                intIdentity(!(if true { return 42 } else { return 1 }))
            }
            fn binary(): string {
                textIdentity(1 + (if true { return "done" } else { return "bad" }))
            }
            fn nestedCall(): int {
                intIdentity(boolToText(if true { return 42 } else { return 1 }))
            }
            fn equality(): int {
                intIdentity((if true { return 42 } else { return 1 }) == 5)
            }
            fn conditionNever(): string {
                if (if true { return "done" } else { return "bad" }) { 1 } else { 2 }
            }
            fn priorLet(flag: bool): int {
                let unused = 1
                if flag { return 42 } else { return 7 }
            }
            fn nestedFinal(): int {
                if true {
                    if true { return 42 } else { return 1 }
                } else { return 2 }
            }
            """;
        await WithGenerated(source, type =>
        {
            Assert.Equal(42, Invoke(type, "unary"));
            Assert.Equal("done", Invoke(type, "binary"));
            Assert.Equal(42, Invoke(type, "nestedCall"));
            Assert.Equal(42, Invoke(type, "equality"));
            Assert.Equal("done", Invoke(type, "conditionNever"));
            Assert.Equal(42, Invoke(type, "priorLet", true));
            Assert.Equal(7, Invoke(type, "priorLet", false));
            Assert.Equal(42, Invoke(type, "nestedFinal"));
        });
    }

    [Theory]
    [InlineData("fn answer(): int { }")]
    [InlineData("fn answer(flag: bool): int { if flag { return 42 } }")]
    [InlineData("fn answer(): int { return () }")]
    [InlineData("fn answer(): Unit { return 42 }")]
    [InlineData("fn answer(): int { if true { 42 } }")]
    [InlineData("fn answer(): Unit { if 1 { } }")]
    [InlineData("fn answer(flag: bool): int { if flag { return 42 } else { } }")]
    [InlineData("fn answer(): int {\n let value: int = ()\n 42\n}")]
    [InlineData("fn answer(): Unit {\n let value: Unit = 42\n ()\n}")]
    [InlineData("fn answer(): Unit {\n 42\n ()\n}")]
    [InlineData("fn empty(): Unit { }\nfn answer(): int { empty() }")]
    [InlineData("fn answer(): int { return }")]
    public void InvalidUnitAndReturnPathsHaveDSharpDiagnostics(string body)
    {
        var source = "module Demo\n" + body;
        var compilation = DSharpCompiler.Compile(source, "unit-invalid.ds");
        Assert.Null(compilation.CSharp);
        var diagnostic = Assert.Single(compilation.Diagnostics);
        Assert.StartsWith("DS", diagnostic.Code);
        Assert.Equal("unit-invalid.ds", diagnostic.Path);
        Assert.InRange(diagnostic.Span.Start, 0, source.Length);
        Assert.InRange(diagnostic.Span.Length, 0, source.Length - diagnostic.Span.Start);
        var prefix = source[..diagnostic.Span.Start];
        Assert.Equal(prefix.Count(c => c == '\n') + 1, diagnostic.Span.Line);
        Assert.Equal(diagnostic.Span.Start - prefix.LastIndexOf('\n'), diagnostic.Span.Column);
    }

    private static object? Invoke(Type type, string name, params object[] args) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.Invoke(null, args);

    private static async Task WithGenerated(string source, Action<Type> verify)
    {
        var compilation = DSharpCompiler.Compile(source, "unit.ds");
        Assert.Empty(compilation.Diagnostics);
        Assert.Equal(compilation.CSharp, DSharpCompiler.Compile(source).CSharp);
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-unit-" + Guid.NewGuid());
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
            var context = new AssemblyLoadContext("unit-" + Guid.NewGuid(), isCollectible: true);
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
