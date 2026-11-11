using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class FunctionCallTests
{
    [Fact]
    public async Task TypedParametersAndForwardNestedCallsExecute()
    {
        const string source = """
            module Demo
            fn answer(): int { twice(add(identity(19), 2)) }
            fn local(): int {
                let item = identity(40)
                add(item, 2)
            }
            fn literal(): int { 42 }
            fn callZero(): int { literal() }
            fn class(): int { event(42) }
            fn useExpressions(): int { add(4 * 5, -(1 - 2)) * 2 }
            fn collision(): int { __dsharp_int_0(42) }
            fn __dsharp_int_0(x: int): int { x }
            fn event(event: int): int { event }
            fn twice(n: int): int { n * 2 }
            fn identity(value: int): int { value }
            fn add(first: int, second: int): int { first + second }
            fn reuse(value: int): int {
                let other = value
                other
            }
            """;
        await WithGenerated(source, type =>
        {
            Assert.Equal(42, Call(type, "answer"));
            Assert.Equal(42, Call(type, "local"));
            Assert.Equal(42, Call(type, "callZero"));
            Assert.Equal(42, Call(type, "class"));
            Assert.Equal(42, Call(type, "useExpressions"));
            Assert.Equal(42, Call(type, "collision"));
            Assert.Equal(7, Call(type, "identity", 7));
            Assert.Equal(30, Call(type, "add", 13, 17));
            Assert.Equal(-7, Call(type, "reuse", -7));
            Assert.Equal(0, Call(type, "identity", 0));
        });
    }

    [Fact]
    public async Task CallsEvaluateArgumentsLeftToRightAndOnlyOnInvocation()
    {
        const string source = """
            module Demo
            fn first(): int { ignore(1 / 0, 2147483647 + 1) }
            fn second(): int { ignore(2147483647 + 1, 1 / 0) }
            fn ignore(a: int, b: int): int { 42 }
            """;
        await WithGenerated(source, type =>
        {
            var first = Assert.Throws<TargetInvocationException>(() => Call(type, "first"));
            Assert.IsType<DivideByZeroException>(first.InnerException);
            var second = Assert.Throws<TargetInvocationException>(() => Call(type, "second"));
            Assert.IsType<OverflowException>(second.InnerException);
        });
    }

    [Theory]
    [InlineData("fn test(a: int, |a: int): int { a }")]
    [InlineData("fn test(a: int): int {\n let |a = 1\n a\n}")]
    [InlineData("fn test(a: int): int {\n |a = 2\n a\n}")]
    [InlineData("fn test(a: int): int { a }\nfn other(): int { |a }")]
    [InlineData("fn test(): int { |missing(1) }")]
    [InlineData("fn identity(a: int): int { a }\nfn test(): int { |identity() }")]
    [InlineData("fn identity(a: int): int { a }\nfn test(): int { |identity(1, 2) }")]
    [InlineData("fn test(value: |float): int { 1 }")]
    [InlineData("fn identity(a: int): int { a }\nfn test(): int { identity(|true) }")]
    [InlineData("fn identity(a: int): int { a }\nfn test(): int { identity(|missing) }")]
    [InlineData("fn test(value: int): |string { value }")]
    [InlineData("fn identity(a: int): int { a }\nfn test(): int { identity(1, |) }")]
    [InlineData("fn identity(a: int): int { a }\nfn test(): int { identity(1 |}")]
    public void InvalidCallsAndParametersReturnDSharpSourceDiagnostics(string marked)
    {
        var markedSource = "module Demo\n" + marked;
        var start = markedSource.IndexOf('|');
        var source = markedSource.Remove(start, 1);
        var result = DSharpCompiler.Compile(source, "calls.ds");
        Assert.Null(result.CSharp);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.StartsWith("DS", diagnostic.Code);
        Assert.Equal("calls.ds", diagnostic.Path);
        Assert.True(diagnostic.Span.Start <= start && diagnostic.Span.Start + diagnostic.Span.Length >= start,
            $"Expected diagnostic at offset {start}, got {diagnostic.Span} ({diagnostic.Message})");
        var prefix = source[..diagnostic.Span.Start];
        Assert.Equal(prefix.Count(c => c == '\n') + 1, diagnostic.Span.Line);
        Assert.Equal(diagnostic.Span.Start - prefix.LastIndexOf('\n'), diagnostic.Span.Column);
    }

    private static object? Call(Type type, string name, params object[] arguments) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.Invoke(null, arguments);

    private static async Task WithGenerated(string source, Action<Type> verify)
    {
        var compilation = DSharpCompiler.Compile(source, "calls.ds");
        Assert.Empty(compilation.Diagnostics);
        Assert.Equal(compilation.CSharp, DSharpCompiler.Compile(source).CSharp);
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-calls-" + Guid.NewGuid());
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
            var context = new AssemblyLoadContext("calls-" + Guid.NewGuid(), isCollectible: true);
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
