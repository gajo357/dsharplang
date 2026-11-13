using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class LetBindingTests
{
    private static string Program(string body) => "module Demo\nfn answer(): int {\n" + body + "\n}\n";

    [Theory]
    [InlineData("let original = 42\nlet result = original\nresult", 42)]
    [InlineData("let original: int = 42\nlet result: int = original\nresult", 42)]
    [InlineData("let unused = 1\n42", 42)]
    [InlineData("let value = 0\nvalue", 0)]
    [InlineData("let value = 2147483647\nvalue", int.MaxValue)]
    [InlineData("let event = 42\nlet __dsharp_local_0 = event\nlet __DSharp_Functions = __dsharp_local_0\n__DSharp_Functions", 42)]
    [InlineData("// comment\r\nlet value = 42 // comment\r\n\r\nlet copy = value\r\ncopy", 42)]
    public async Task GeneratedLocalsExecute(string body, int expected)
    {
        await Execute(Program(body), new Dictionary<string, int> { ["answer"] = expected });
    }

    [Fact]
    public async Task FunctionsHaveIndependentLocalsAndLiteralFunctionsStillExecute()
    {
        const string source = "module Demo\nfn first(): int {\nlet value = 42\nvalue\n}\n" +
            "fn second(): int {\nlet value = 7\nvalue\n}\nfn literal(): int { 42 }";
        await Execute(source, new Dictionary<string, int> { ["first"] = 42, ["second"] = 7, ["literal"] = 42 });
    }

    [Fact]
    public void SyntaxAndBindingPreserveDeclarationsAndReferences()
    {
        var source = Program("let original: int = 42\nlet copy = original\ncopy");
        var syntax = new Parser(Lexer.Lex(source)).Parse();
        var function = Assert.Single(syntax.Functions);
        Assert.Equal("int", function.Bindings[0].Annotation!.Text);
        Assert.Equal("original", function.Bindings[1].Initializer.Text);
        Assert.Equal("copy", function.Literal.Text);
        var bound = Assert.Single(DSharp.Compiler.Binder.Bind(syntax).Functions);
        Assert.Equal(42, bound.Value);
        Assert.Equal("original", bound.Locals[1].InitializerLocal);
        Assert.Equal("copy", bound.ResultLocal);
        var output = CSharpEmitter.Emit(new("Demo", [bound]));
        Assert.Contains("int __dsharp_local_1 = __dsharp_local_0;", output);
        Assert.Contains("return __dsharp_local_1;", output);
        Assert.Equal(output, DSharpCompiler.Compile(source).CSharp);
    }

    [Theory]
    [InlineData("let value = 42\n|value = 7\nvalue", "DS2005", 5)]
    [InlineData("let value = 42\nlet |value = 7\nvalue", "DS2006", 5)]
    [InlineData("|missing", "DS2007", 7)]
    [InlineData("let value = |missing\nvalue", "DS2007", 7)]
    [InlineData("let value = |later\nlet later = 42\nvalue", "DS2007", 5)]
    [InlineData("let value = |value\nvalue", "DS2007", 5)]
    [InlineData("let value: |string = 42\nvalue", "DS2008", 6)]
    [InlineData("let value = |2147483648\nvalue", "DS2004", 10)]
    [InlineData("let value = |else\nvalue", "DS2002", 4)]
    [InlineData("let value = |null\nvalue", "DS2002", 4)]
    [InlineData("let |let = 42\n42", "DS1002", 3)]
    [InlineData("let |@invalid = 42\n42", "DS1001", 1)]
    [InlineData("let value = 42 |let copy = value\ncopy", "DS1002", 3)]
    [InlineData("let value = 42 |value", "DS1002", 5)]
    [InlineData("let value =\n|else\nvalue", "DS2002", 4)]
    [InlineData("let value = 42\n|}", "DS2002", 1)]
    [InlineData("let value = 42|;\nvalue", "DS1001", 1)]
    [InlineData("let value = 1 |^ 2\nvalue", "DS1001", 1)]
    [InlineData("let value = 42\nvalue|()", "DS1002", 1)]
    [InlineData("let value = |{ 42 }\nvalue", "DS2002", 1)]
    public void InvalidBindingsReportExactDSharpLocation(string markedBody, string code, int length)
    {
        var marked = Program(markedBody);
        var start = marked.IndexOf('|');
        var source = marked.Remove(start, 1);
        var result = DSharpCompiler.Compile(source, "bindings.ds");
        Assert.Null(result.CSharp);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(code, diagnostic.Code);
        Assert.Equal("bindings.ds", diagnostic.Path);
        var prefix = source[..start];
        Assert.Equal(new SourceSpan(start, length, prefix.Count(c => c == '\n') + 1,
            start - prefix.LastIndexOf('\n')), diagnostic.Span);
    }

    [Fact]
    public void LocalsCannotLeakIntoAnotherFunction()
    {
        const string source = "module Demo\nfn first(): int {\nlet value = 42\nvalue\n}\nfn second(): int {\nvalue\n}";
        var diagnostic = Assert.Single(DSharpCompiler.Compile(source, "scope.ds").Diagnostics);
        Assert.Equal("DS2007", diagnostic.Code);
        Assert.Equal("scope.ds", diagnostic.Path);
        Assert.Equal(new SourceSpan(source.LastIndexOf("value", StringComparison.Ordinal), 5, 7, 1), diagnostic.Span);
    }

    [Theory]
    [InlineData("let")]
    [InlineData("let value")]
    [InlineData("let value:")]
    [InlineData("let value: int")]
    [InlineData("let value =")]
    public void IncompleteBindingsReturnDiagnosticsInsteadOfThrowing(string body)
    {
        var result = DSharpCompiler.Compile("module Demo fn answer(): int {\n" + body);
        Assert.Null(result.CSharp);
        Assert.Single(result.Diagnostics);
    }

    private static async Task Execute(string source, IReadOnlyDictionary<string, int> expected)
    {
        var compilation = DSharpCompiler.Compile(source);
        Assert.Empty(compilation.Diagnostics);
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-let-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "Generated.cs"), compilation.CSharp);
            File.WriteAllText(Path.Combine(directory, "Generated.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net11.0</TargetFramework></PropertyGroup></Project>");
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
            var context = new AssemblyLoadContext("let-" + Guid.NewGuid(), isCollectible: true);
            try
            {
                var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0/Generated.dll"));
                foreach (var (name, value) in expected)
                {
                    var method = assembly.GetType("DSharp.Generated.Demo")!.GetMethod(name,
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
                    Assert.Equal(value, method.Invoke(null, null));
                }
            }
            finally { context.Unload(); }
        }
        finally { Directory.Delete(directory, true); }
    }
}
