using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class CommentTests
{
    [Fact]
    public void DocumentationAttachesToModuleAndFollowingFunctionsWithOriginalSpans()
    {
        const string source = "/// module docs\r\nmodule Demo\r\n\r\n" +
            "/// first line\r\n  /// second line\r\nfn answer(): int { 42 }\r\n" +
            "/// other function\r\nfn other(): int { 7 }\r\n";
        var tokens = Lexer.Lex(source, "docs.ds");
        var module = new Parser(tokens, "docs.ds").Parse();

        var moduleDoc = Assert.Single(module.DocumentationComments);
        Assert.Equal("/// module docs", moduleDoc.Text);
        AssertTokenAt(source, moduleDoc, "/// module docs");
        var answer = module.Functions[0];
        Assert.Equal("answer", answer.Name.Text);
        Assert.Equal(new[] { "/// first line", "/// second line" }, answer.DocumentationComments.Select(t => t.Text));
        foreach (var token in answer.DocumentationComments) AssertTokenAt(source, token, token.Text);
        var other = module.Functions[1];
        var otherDoc = Assert.Single(other.DocumentationComments);
        Assert.Equal("/// other function", otherDoc.Text);
        AssertTokenAt(source, otherDoc, otherDoc.Text);
    }

    [Fact]
    public void OrdinaryCommentsStayTriviaAndDoNotAlterGeneratedProgram()
    {
        const string clean = "module Demo\nfn answer(): int { 42 }\n";
        const string withComments = "/// Module docs\nmodule Demo\n\n" +
            "/* first\n   second */\n/// Answer docs\nfn answer(): int { /* inside */ 42 }\n";
        var cleanResult = DSharpCompiler.Compile(clean, "clean.ds");
        var commentsResult = DSharpCompiler.Compile(withComments, "comments.ds");
        Assert.Empty(cleanResult.Diagnostics);
        Assert.Empty(commentsResult.Diagnostics);
        Assert.Equal(cleanResult.CSharp, commentsResult.CSharp);
        Assert.DoesNotContain("Answer docs", commentsResult.CSharp!);
        Assert.DoesNotContain("first", commentsResult.CSharp!);
        var ordinary = Lexer.Lex("// comment\nmodule Demo", "plain.ds");
        Assert.Equal("module", ordinary[0].Text);
        Assert.Equal(new SourceSpan(11, 6, 2, 1), ordinary[0].Span);
        Assert.Equal(new Token("module", new SourceSpan(11, 6, 2, 1)), ordinary[0]);
    }

    [Fact]
    public async Task CommentsAndMultilineContinuationsPreserveExecutionAndText()
    {
        const string source = """
            /// Module documentation
            module Demo
            /// Calculates the answer.
            /// Comment markers in quoted text are values.
            fn answer(): int {
                let value = 20 + // continue after the operator
                    /* a multiline
                       block comment */ 22
                value
            }
            fn grouped(): int {
                let value = (
                    20
                    + 22
                )
                value
            }
            fn assigned(): int {
                let value = // continuation after the binding operator
                    42
                value
            }
            fn id(value: int): int { value }
            fn argument(): int {
                let result = id(
                    42
                )
                result
            }
            fn continuedCall(): int { 1 +
                id(41) }
            fn continuedUnary(): int { 43 + -
                1 }
            fn markers(): bool { "/* not a comment */ // text /// docs" == "/* not a comment */ // text /// docs" }
            fn slash(): char { '/' }
            """;
        await WithGenerated(source, type =>
        {
            Assert.Equal(42, Invoke(type, "answer"));
            Assert.Equal(42, Invoke(type, "grouped"));
            Assert.Equal(42, Invoke(type, "argument"));
            Assert.Equal(42, Invoke(type, "assigned"));
            Assert.Equal(42, Invoke(type, "continuedCall"));
            Assert.Equal(42, Invoke(type, "continuedUnary"));
            Assert.Equal(true, Invoke(type, "markers"));
            Assert.Equal('/', Invoke(type, "slash"));
        });
    }

    [Fact]
    public void MultilineCommentsAndUnicodePreserveUtf16Location()
    {
        const string source = "module Demo\r\n/* 🌍\r\ncomment */\r\nfn answer(): int {\r\n  missing\r\n}\r\n";
        var compilation = DSharpCompiler.Compile(source, "unicode.ds");
        Assert.Null(compilation.CSharp);
        var error = Assert.Single(compilation.Diagnostics);
        Assert.Equal("DS2007", error.Code);
        Assert.Equal("unicode.ds", error.Path);
        Assert.Equal(new SourceSpan(source.IndexOf("missing", StringComparison.Ordinal), 7, 5, 3), error.Span);
    }

    [Fact]
    public void UnterminatedBlockCommentPointsAtOpeningDelimiter()
    {
        const string source = "module Demo\r\nfn answer(): int {\r\n  42\r\n}\r\n/* never closed\r\nmore";
        var error = Assert.Single(DSharpCompiler.Compile(source, "unclosed.ds").Diagnostics);
        Assert.Equal("unclosed.ds", error.Path);
        Assert.Equal(new SourceSpan(source.IndexOf("/*", StringComparison.Ordinal), 2, 5, 1), error.Span);
        Assert.StartsWith("DS", error.Code);
    }

    [Fact]
    public void NestedBlockCommentIsRejectedAtTheNestedOpener()
    {
        const string source = "module Demo\n/* outer\n  /* inner */\n*/\nfn answer(): int { 42 }";
        var error = Assert.Single(DSharpCompiler.Compile(source, "nested.ds").Diagnostics);
        Assert.Equal("nested.ds", error.Path);
        Assert.Equal(new SourceSpan(source.IndexOf("/* inner", StringComparison.Ordinal), 2, 3, 3), error.Span);
        Assert.StartsWith("DS", error.Code);
    }

    [Theory]
    [InlineData("module Demo\n/// orphan\n\nfn answer(): int { 42 }")]
    [InlineData("module Demo\n/// orphan\n// barrier\nfn answer(): int { 42 }")]
    [InlineData("module Demo\n/// orphan\n/* barrier */\nfn answer(): int { 42 }")]
    [InlineData("module Demo\n/// orphan\n")]
    [InlineData("module Demo\nfn answer(): int {\n /// orphan\n let value = 42\n value\n}")]
    [InlineData("module Demo\nfn answer(): int { /// orphan\n 42 }")]
    [InlineData("module Demo\nfn answer(): int { 42 } /// orphan\nfn other(): int { 7 }")]
    [InlineData("module Demo\n/// first\n\n/// second\nfn answer(): int { 42 }")]
    public void UnattachedOrMisplacedDocumentationIsRejected(string source)
    {
        var result = DSharpCompiler.Compile(source, "orphan.ds");
        Assert.Null(result.CSharp);
        var error = Assert.Single(result.Diagnostics);
        Assert.Equal("DS1004", error.Code);
        Assert.Equal("orphan.ds", error.Path);
        var opener = source.IndexOf("///", StringComparison.Ordinal);
        Assert.Equal(opener, error.Span.Start);
        Assert.True(error.Span.Length >= 3);
    }

    [Theory]
    [InlineData("module Demo\nfn answer(): int {\n let value = 1\n + 2\n value\n}", "+")]
    [InlineData("module Demo\nfn answer(): int {\n 1\n + 2\n}", "+")]
    [InlineData("module Demo\nfn answer(): int {\n if true\n && false { 1 } else { 2 }\n}", "&&")]
    public void LeadingBinaryOperatorDoesNotContinuePreviousLine(string source, string op)
    {
        var result = DSharpCompiler.Compile(source, "leading.ds");
        Assert.Null(result.CSharp);
        var error = Assert.Single(result.Diagnostics);
        Assert.StartsWith("DS", error.Code);
        Assert.Equal(source.IndexOf(op, StringComparison.Ordinal), error.Span.Start);
    }

    private static void AssertTokenAt(string source, Token token, string text)
    {
        var offset = source.IndexOf(text, StringComparison.Ordinal);
        Assert.True(offset >= 0);
        var prefix = source[..offset];
        Assert.Equal(new SourceSpan(offset, text.Length, prefix.Count(c => c == '\n') + 1,
            offset - prefix.LastIndexOf('\n')), token.Span);
    }

    private static object? Invoke(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.Invoke(null, null);

    private static async Task WithGenerated(string source, Action<Type> verify)
    {
        var compilation = DSharpCompiler.Compile(source, "comments.ds");
        Assert.Empty(compilation.Diagnostics);
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-comments-" + Guid.NewGuid());
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
            var context = new AssemblyLoadContext("comments-" + Guid.NewGuid(), isCollectible: true);
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
