using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class TextLiteralTests
{
    [Fact]
    public async Task StringsAndCharsRoundTripThroughParametersLocalsAndReturns()
    {
        const string source = """
            module Demo
            fn plain(): string { "héllo 🌍" }
            fn empty(): string { "" }
            fn escapes(): string { "line\nreturn\rtab\tslash\\quote\"unicode\u0041zero\u0000" }
            fn apostrophe(): char { '\'' }
            fn slash(): char { '\\' }
            fn newline(): char { '\n' }
            fn unicode(): char { '\u03A9' }
            fn loneHighSurrogate(): char { '\uD800' }
            fn loneSurrogateString(): string { "\uD800" }
            fn dangerous(): string { "\"; throw new System.Exception(); //" }
            fn echo(value: string): string { value }
            fn echoChar(value: char): char { value }
            fn locally(): string {
                let original: string = "résumé"
                let copy = original
                echo(copy)
            }
            fn pick(flag: bool): string { if flag { "yes" } else { "no" } }
            """;
        await WithGenerated(source, type =>
        {
            Assert.Equal("héllo 🌍", Invoke(type, "plain"));
            Assert.Equal("", Invoke(type, "empty"));
            Assert.Equal("line\nreturn\rtab\tslash\\quote\"unicodeAzero\0", Invoke(type, "escapes"));
            Assert.Equal('\'', Invoke(type, "apostrophe"));
            Assert.Equal('\\', Invoke(type, "slash"));
            Assert.Equal('\n', Invoke(type, "newline"));
            Assert.Equal('\u03A9', Invoke(type, "unicode"));
            Assert.Equal('\uD800', Invoke(type, "loneHighSurrogate"));
            Assert.Equal(new string('\uD800', 1), Invoke(type, "loneSurrogateString"));
            Assert.Equal("\"; throw new System.Exception(); //", Invoke(type, "dangerous"));
            Assert.Equal("a\0b", Invoke(type, "echo", "a\0b"));
            Assert.Equal('\uDFFF', Invoke(type, "echoChar", '\uDFFF'));
            Assert.Equal("résumé", Invoke(type, "locally"));
            Assert.Equal("yes", Invoke(type, "pick", true));
            Assert.Equal("no", Invoke(type, "pick", false));
        });
    }

    [Fact]
    public async Task EqualityIsOrdinalAndWorksForStringsAndCodeUnits()
    {
        const string source = """
            module Demo
            fn identical(): bool { "héllo" == "héllo" }
            fn notIdentical(): bool { "A" != "a" }
            fn normalizationMatters(): bool { "é" == "e\u0301" }
            fn embeddedZero(): bool { "a\u0000b" != "a" }
            fn codeUnits(): bool { '\u03A9' == 'Ω' }
            fn codeUnitsDiffer(): bool { '\uD800' != '\uD801' }
            fn sameString(a: string, b: string): bool { a == b }
            fn sameChar(a: char, b: char): bool { a == b }
            fn constantCondition(): int { if 'x' == 'x' { 42 } else { 0 } }
            """;
        await WithGenerated(source, type =>
        {
            Assert.Equal(true, Invoke(type, "identical"));
            Assert.Equal(true, Invoke(type, "notIdentical"));
            Assert.Equal(false, Invoke(type, "normalizationMatters"));
            Assert.Equal(true, Invoke(type, "embeddedZero"));
            Assert.Equal(true, Invoke(type, "codeUnits"));
            Assert.Equal(true, Invoke(type, "codeUnitsDiffer"));
            Assert.Equal(false, Invoke(type, "sameString", "é", "e\u0301"));
            Assert.Equal(true, Invoke(type, "sameChar", '\uD800', '\uD800'));
            Assert.Equal(42, Invoke(type, "constantCondition"));
        });
    }

    [Fact]
    public void DirectBinderHandlesEmptyTokenWithDiagnostic()
    {
        var span = new SourceSpan(0, 0, 1, 1);
        var syntax = new ModuleSyntax(new Token("Demo", span),
            [new FunctionSyntax(new Token("answer", span), new Token("int", span), new Token("", span))]);
        var error = Assert.Throws<SourceError>(() => DSharp.Compiler.Binder.Bind(syntax, "empty.ds"));
        Assert.Equal("DS2007", error.Diagnostic.Code);
        Assert.Equal("empty.ds", error.Diagnostic.Path);
    }

    [Theory]
    [InlineData("fn answer(): string { \"bad\\q\" }")]
    [InlineData("fn answer(): string { \"bad\\u12G4\" }")]
    [InlineData("fn answer(): string { \"bad\\u123\" }")]
    [InlineData("fn answer(): string { \"unfinished }")]
    [InlineData("fn answer(): string { \"first\nsecond\" }")]
    [InlineData("fn answer(): char { '' }")]
    [InlineData("fn answer(): char { 'ab' }")]
    [InlineData("fn answer(): char { '🌍' }")]
    [InlineData("fn answer(): char { '\\u12G4' }")]
    [InlineData("fn answer(): char { '\\q' }")]
    [InlineData("fn answer(): char { 'unfinished }")]
    [InlineData("fn answer(): int { \"not an int\" }")]
    [InlineData("fn answer(): bool { \"x\" == 'x' }")]
    [InlineData("fn answer(): bool { \"x\" < \"y\" }")]
    [InlineData("fn answer(): string { \"a\" + \"b\" }")]
    [InlineData("fn identity(value: char): char { value }\nfn answer(): char { identity(\"x\") }")]
    [InlineData("fn answer(): char {\n let c: char = \"x\"\n c\n}")]
    public void MalformedOrIncompatibleTextHasSourceDiagnostics(string body)
    {
        var source = "module Demo\n" + body;
        var result = DSharpCompiler.Compile(source, "text-invalid.ds");
        Assert.Null(result.CSharp);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.StartsWith("DS", diagnostic.Code);
        Assert.Equal("text-invalid.ds", diagnostic.Path);
        Assert.InRange(diagnostic.Span.Start, 0, source.Length);
        Assert.InRange(diagnostic.Span.Length, 0, source.Length - diagnostic.Span.Start);
        var prefix = source[..diagnostic.Span.Start];
        Assert.Equal(prefix.Count(c => c == '\n') + 1, diagnostic.Span.Line);
        Assert.Equal(diagnostic.Span.Start - prefix.LastIndexOf('\n'), diagnostic.Span.Column);
    }

    private static object? Invoke(Type type, string name, params object[] arguments) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.Invoke(null, arguments);

    private static async Task WithGenerated(string source, Action<Type> verify)
    {
        var compilation = DSharpCompiler.Compile(source, "text.ds");
        Assert.Empty(compilation.Diagnostics);
        Assert.Equal(compilation.CSharp, DSharpCompiler.Compile(source).CSharp);
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-text-" + Guid.NewGuid());
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
            var context = new AssemblyLoadContext("text-" + Guid.NewGuid(), isCollectible: true);
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
