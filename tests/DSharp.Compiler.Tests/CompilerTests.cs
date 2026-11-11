using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class CompilerTests
{
    private const string Source = "module Demo\n\nfn answer(): int {\n    42\n}\n";
    [Fact]
    public void LexerTracksCommentsAndLocations()
    {
        var tokens = Lexer.Lex("// comment\nmodule Demo");
        Assert.Equal("module", tokens[0].Text);
        Assert.Equal(new SourceSpan(11, 6, 2, 1), tokens[0].Span);
        Assert.Equal("", tokens[^1].Text);
    }
    [Fact]
    public void ParserPreservesFunctionAndLiteral()
    {
        var module = new Parser(Lexer.Lex(Source)).Parse();
        Assert.Equal("Demo", module.Name.Text);
        var function = Assert.Single(module.Functions);
        Assert.Equal("answer", function.Name.Text);
        Assert.Equal("42", function.Literal.Text);
    }
    [Theory]
    [InlineData("module Demo fn answer(): int { 42", "DS1002")]
    [InlineData("module Demo fn answer(): int { @ }", "DS1001")]
    [InlineData("module Demo fn answer(): int { true }", "DS2002")]
    [InlineData("module Demo fn answer(): int { 2147483648 }", "DS2004")]
    [InlineData("module Demo fn answer(): string { 42 }", "DS2003")]
    [InlineData("module Demo fn answer(): int { 1 } fn answer(): int { 2 }", "DS2001")]
    [InlineData("module Demo import Other", "DS1002")]
    [InlineData("module Demo fn answer(x: int): int { 42 }", "DS1002")]
    [InlineData("module Demo fn answer(): int { 42 } extra", "DS1002")]
    [InlineData("module", "DS1002")]
    public void InvalidSourceHasDSharpDiagnostic(string source, string code)
    {
        var result = DSharpCompiler.Compile(source, "invalid.ds");
        Assert.Null(result.CSharp);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(code, diagnostic.Code);
        Assert.Equal("invalid.ds", diagnostic.Path);
        Assert.InRange(diagnostic.Span.Start, 0, source.Length);
        Assert.True(diagnostic.Span.Line > 0 && diagnostic.Span.Column > 0);
    }
    [Fact]
    public void DiagnosticPointsAtOriginalToken()
    {
        var error = Assert.Single(DSharpCompiler.Compile("module Demo\nfn answer(): int {\n true\n}").Diagnostics);
        Assert.Equal(3, error.Span.Line);
        Assert.Equal(2, error.Span.Column);
        Assert.Equal(4, error.Span.Length);
    }
    [Fact]
    public void SemanticBindingResolvesIntBeforeEmission()
    {
        var syntax = new Parser(Lexer.Lex(Source)).Parse();
        Assert.Equal(new BoundFunction("answer", 42), Assert.Single(DSharp.Compiler.Binder.Bind(syntax).Functions));
        var result = DSharpCompiler.Compile(Source);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(result.CSharp, DSharpCompiler.Compile(Source).CSharp);
    }
    [Theory]
    [InlineData("Demo", "answer")]
    [InlineData("class", "event")]
    [InlineData("Demo", "Demo")]
    [InlineData("__DSharp_Functions", "__DSharp_Functions_")]
    public async Task GeneratedCodeBuildsAndReturns42(string module, string function)
    {
        var source = Source.Replace("Demo", module).Replace("answer", function);
        var result = DSharpCompiler.Compile(source);
        Assert.Empty(result.Diagnostics);
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-execution-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "Generated.cs"), result.CSharp);
            File.WriteAllText(Path.Combine(directory, "Generated.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
            File.WriteAllText(Path.Combine(directory, "global.json"), "{\"sdk\":{\"version\":\"10.0.100\",\"rollForward\":\"disable\"}}");
            File.WriteAllText(Path.Combine(directory, "NuGet.Config"), "<configuration><packageSources><clear/></packageSources></configuration>");
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "build", "Generated.csproj", "--nologo", "--verbosity", "quiet" }) start.ArgumentList.Add(arg);
            using var build = Process.Start(start)!;
            var stdout = build.StandardOutput.ReadToEndAsync();
            var stderr = build.StandardError.ReadToEndAsync();
            if (!build.WaitForExit(60000)) { build.Kill(true); throw new TimeoutException("Generated build timed out"); }
            Assert.True(build.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("generated-" + Guid.NewGuid(), isCollectible: true);
            try
            {
                var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net10.0/Generated.dll"));
                var method = assembly.GetType("DSharp.Generated." + module)!.GetMethod(function, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
                var observed = method.Invoke(null, null);
                Assert.Equal(42, observed);
                Console.WriteLine($"Observed {module}.{function}() = {observed}");
            }
            finally { context.Unload(); }
        }
        finally { Directory.Delete(directory, true); }
    }
}
