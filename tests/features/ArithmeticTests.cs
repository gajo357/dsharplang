using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class ArithmeticTests
{
    [Fact]
    public async Task OperatorsPrecedenceAssociativityAndLocalsExecute()
    {
        (string Body, int Value)[] cases = [
            ("(20 + 1) * 2", 42), ("20 + 1 * 2", 22), ("40 - 10 - 5", 25),
            ("48 / 4 / 3", 4), ("20 % 6 * 3", 6), ("-2 * -3 + 4", 10),
            ("--42", 42), ("-(20 + 22)", -42), ("7 / -3", -2), ("-7 / 3", -2),
            ("-7 % 3", -1), ("7 % -3", 1), ("0 - 1", -1),
            ("let base = 20\n(base + 1) * 2", 42),
            ("let original: int = (10 + 11) * 2\nlet copy = original / 2\ncopy * 2", 42),
            ("let value = 42\n-value", -42),
            ("let a = 1\nlet b = a + 2 * 3\nb", 7),
            ("((20\n + 1) * 2)", 42), ("20 + // comment\n22", 42),
            ("2147483647", int.MaxValue), ("-2147483647 - 1", int.MinValue)
        ];
        await WithGenerated(cases.Select((c, i) => ("value" + i, c.Body)).ToArray(), type =>
        {
            for (var i = 0; i < cases.Length; i++) Assert.Equal(cases[i].Value, Invoke(type, "value" + i));
        });
    }

    [Fact]
    public async Task CheckedOverflowAndDivisionFaultOnlyWhenInvoked()
    {
        (string Body, Type Error)[] cases = [
            ("2147483647 + 1", typeof(OverflowException)),
            ("(-2147483647 - 1) - 1", typeof(OverflowException)),
            ("2147483647 * 2", typeof(OverflowException)),
            ("-(-2147483647 - 1)", typeof(OverflowException)),
            ("(-2147483647 - 1) / -1", typeof(OverflowException)),
            ("(-2147483647 - 1) % -1", typeof(OverflowException)),
            ("1 / 0", typeof(DivideByZeroException)), ("1 % 0", typeof(DivideByZeroException)),
            ("let value = 0\n42 / value", typeof(DivideByZeroException)),
            ("let value = 2147483647\nlet next = value + 1\nnext", typeof(OverflowException)),
            ("(2147483647 + 1) + (1 / 0)", typeof(OverflowException)),
            ("(1 / 0) + (2147483647 + 1)", typeof(DivideByZeroException)),
            ("let unused = 1 / 0\n42", typeof(DivideByZeroException))
        ];
        await WithGenerated(cases.Select((c, i) => ("fault" + i, c.Body)).ToArray(), type =>
        {
            for (var i = 0; i < cases.Length; i++)
            {
                var error = Assert.Throws<TargetInvocationException>(() => Invoke(type, "fault" + i));
                Assert.IsType(cases[i].Error, error.InnerException);
            }
        });
    }

    [Theory]
    [InlineData("1 + |}", "DS2002", 1)]
    [InlineData("1 + |true", "DS2002", 4)]
    [InlineData("|false * 2", "DS2002", 5)]
    [InlineData("1 + |missing", "DS2007", 7)]
    [InlineData("(|)", "DS2002", 1)]
    [InlineData("(1 + 2 |}", "DS1002", 1)]
    [InlineData("1 |2", "DS1002", 1)]
    [InlineData("1 + |* 2", "DS2002", 1)]
    [InlineData("1 |^ 2", "DS1001", 1)]
    [InlineData("-|2147483648", "DS2004", 10)]
    [InlineData("1 + |2147483648", "DS2004", 10)]
    [InlineData("let value = 1 +\n|2\nvalue", "DS1002", 1)]
    [InlineData("let value = 1\nlet next = value + |later\nlet later = 2\nnext", "DS2007", 5)]
    public void MalformedAndInvalidOperandsHaveDSharpSpans(string markedBody, string code, int length)
    {
        var marked = "module Demo\nfn answer(): int {\n" + markedBody + "\n}";
        var start = marked.IndexOf('|');
        var source = marked.Remove(start, 1);
        var result = DSharpCompiler.Compile(source, "arithmetic.ds");
        Assert.Null(result.CSharp);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(code, diagnostic.Code);
        Assert.Equal("arithmetic.ds", diagnostic.Path);
        var prefix = source[..start];
        Assert.Equal(new SourceSpan(start, length, prefix.Count(c => c == '\n') + 1,
            start - prefix.LastIndexOf('\n')), diagnostic.Span);
    }

    private static object? Invoke(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.Invoke(null, null);

    private static async Task WithGenerated((string Name, string Body)[] functions, Action<Type> verify)
    {
        var source = "module Demo\n" + string.Join("\n", functions.Select(f => $"fn {f.Name}(): int {{\n{f.Body}\n}}"));
        var compilation = DSharpCompiler.Compile(source, "execution.ds");
        Assert.Empty(compilation.Diagnostics);
        Assert.Equal(compilation.CSharp, DSharpCompiler.Compile(source).CSharp);
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-arithmetic-" + Guid.NewGuid());
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
            var context = new AssemblyLoadContext("arithmetic-" + Guid.NewGuid(), isCollectible: true);
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
