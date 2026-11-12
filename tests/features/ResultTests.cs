using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class ResultTests
{
    [Fact]
    public async Task SuccessNullPresentAndErrorRemainDistinctInEitherPatternOrder()
    {
        const string source = """
            module Demo
            type Invoice { total: int }
            type Fault { | Missing
                         | Invalid(int) }
            fn find(mode: int): Result<Invoice?, Fault> {
                if mode == 0 { Success(null) } else {
                    if mode == 1 { Success(Invoice { total = 42 }) } else {
                        Error(Fault.Missing)
                    }
                }
            }
            fn nullFirst(value: Result<Invoice?, Fault>): int {
                match value {
                    Success(null) => 1
                    Success(invoice) => invoice.total
                    Error(Fault.Missing) => 3
                    Error(Fault.Invalid(code)) => code
                }
            }
            fn presentFirst(value: Result<Invoice?, Fault>): int {
                match value {
                    Success(invoice) => invoice.total
                    Success(null) => 1
                    Error(Fault.Missing) => 3
                    Error(Fault.Invalid(code)) => code
                }
            }
            fn run(): bool {
                (nullFirst(find(0)) == 1) && (presentFirst(find(0)) == 1) &&
                (nullFirst(find(1)) == 42) && (presentFirst(find(1)) == 42) &&
                (nullFirst(find(2)) == 3) && (presentFirst(find(2)) == 3) &&
                (find(0) != find(1)) && (find(0) != find(2))
            }
            """;
        await WithGenerated(source, type => Assert.Equal(true, Invoke(type, "run")));
    }

    [Fact]
    public async Task UnitShorthandMatchesExplicitUnitAndNullableSuccessRemainsDistinct()
    {
        const string source = """
            module Demo
            type Fault { | Failed }
            fn short(): Result<Unit, Fault> { Success() }
            fn full(): Result<Unit, Fault> { Success(()) }
            fn nullablePresent(): Result<Unit?, Fault> { Success(()) }
            fn nullableAbsent(): Result<Unit?, Fault> { Success(null) }
            fn failed(): Result<Unit?, Fault> { Error(Fault.Failed) }
            fn classify(value: Result<Unit?, Fault>): int {
                match value {
                    Success(()) => 42
                    Success(null) => 1
                    Error(Fault.Failed) => 2
                }
            }
            fn run(): bool {
                (short() == full()) && (classify(nullablePresent()) == 42) &&
                (classify(nullableAbsent()) == 1) && (classify(failed()) == 2) &&
                (nullablePresent() != nullableAbsent()) && (nullableAbsent() != failed())
            }
            """;
        await WithGenerated(source, type => Assert.Equal(true, Invoke(type, "run")));
    }

    [Fact]
    public async Task ContextualResultsCanBeReturnedStoredMatchedAndExplicitlyConsumed()
    {
        const string source = """
            module Demo
            type Fault { | Failed }
            fn choose(flag: bool): Result<int, Fault> {
                if flag { Success(42) } else { Error(Fault.Failed) }
            }
            fn chooseMatch(flag: bool): Result<int, Fault> {
                match flag {
                    true => Success(42)
                    false => Error(Fault.Failed)
                }
            }
            fn explicitSuccess(): Result<int, Fault> { Result.Success<int, Fault>(42) }
            fn explicitError(): Result<int, Fault> { Error<int, Fault>(Fault.Failed) }
            fn explicitUnit(): Result<Unit, Fault> { Success<Unit, Fault>() }
            fn unitOk(value: Result<Unit, Fault>): bool {
                match value {
                    Success(()) => true
                    Error(e) => false
                }
            }
            fn consume(value: Result<int, Fault>): Unit {
                match value {
                    Success(n) => ()
                    Error(e) => ()
                }
            }
            fn extract(value: Result<int, Fault>): int {
                match value {
                    Success(n) => n
                    Error(e) => 0
                }
            }
            fn run(): int {
                let saved: Result<int, Fault> = choose(true)
                consume(saved)
                consume(Success(42))
                if (extract(saved) == 42) && (extract(chooseMatch(true)) == 42) &&
                   (extract(choose(false)) == 0) && (extract(chooseMatch(false)) == 0) &&
                   (extract(explicitSuccess()) == 42) && (extract(explicitError()) == 0) &&
                   unitOk(explicitUnit()) { 42 } else { 0 }
            }
            """;
        await WithGenerated(source, type => Assert.Equal(42, Invoke(type, "run")));
    }

    [Fact]
    public async Task ConstructorsAreLazyInContextAndPayloadEvaluatesOnce()
    {
        const string source = """
            module Demo
            type Fault { | Failed }
            fn tick(): int { 42 }
            fn make(): Result<int, Fault> { Success(tick()) }
            fn early(): Result<int, Fault> {
                Success(if true { return Success(42) } else { 1 / 0 })
            }
            fn skipped(flag: bool): Result<int, Fault> {
                if flag { Success(42) } else { Success(1 / 0) }
            }
            fn extract(value: Result<int, Fault>): int {
                match value {
                    Success(n) => n
                    Error(e) => 0
                }
            }
            fn run(): int { extract(make()) }
            fn runEarly(): int { extract(early()) }
            fn runSkipped(): int { extract(skipped(true)) }
            fn runFault(): int { extract(skipped(false)) }
            """;
        await WithGenerated(source, type =>
        {
            Assert.Equal(42, Invoke(type, "run"));
            Assert.Equal(1, Invoke(type, "readTickCount"));
            Assert.Equal(42, Invoke(type, "runEarly"));
            Assert.Equal(42, Invoke(type, "runSkipped"));
            Assert.IsType<DivideByZeroException>(Assert.Throws<TargetInvocationException>(() => Invoke(type, "runFault")).InnerException);
        }, csharp =>
        {
            var match = Assert.Single(Regex.Matches(csharp, @"    public static ([^\r\n(]+) @tick\(\)").Cast<Match>());
            var resultType = match.Groups[1].Value;
            return csharp.Replace(match.Value,
                "    private static int __testTickCount;\n" +
                "    public static int @readTickCount() => __testTickCount;\n" +
                "    public static " + resultType + " @tick() { __testTickCount++; return @tick_original(); }\n" +
                "    public static " + resultType + " @tick_original()", StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task UnrelatedUserResultAndConstructorNamesRemainOrdinaryIdentifiers()
    {
        const string source = """
            module Demo
            type Result { | Named(int) }
            fn Success(value: int): int { value }
            fn Error(value: int): int { value }
            fn read(value: Result): int {
                match value { Result.Named(n) => n }
            }
            fn run(): int {
                let Success = 40
                let Error = 2
                read(Result.Named(Success + Error))
            }
            fn functions(): int { Success(40) + Error(2) }
            """;
        await WithGenerated(source, type =>
        {
            Assert.Equal(42, Invoke(type, "run"));
            Assert.Equal(42, Invoke(type, "functions"));
        });
    }

    [Fact]
    public async Task CurrentModuleCasesAndFunctionsTakePrecedenceOverImplicitResultCases()
    {
        const string source = """
            module Demo
            type Other { | Success(int) }
            type Holder { Success: int }
            fn Error(value: int): int { value }
            fn make(): Result<int, int> { Result.Success(42) }
            fn extract(value: Result<int, int>): int {
                match value {
                    Result.Success(n) => n
                    Result.Error(e) => e
                }
            }
            fn run(): int { extract(make()) }
            fn userCase(): int {
                let value = Success(42)
                match value { Other.Success(n) => n }
            }
            fn userFunction(): int { Error(42) }
            fn localCase(): int { let Success = 42
                                  Success }
            fn localResult(): int {
                let Result = Holder { Success = 42 }
                Result.Success
            }
            """;
        await WithGenerated(source, type =>
        {
            Assert.Equal(42, Invoke(type, "run"));
            Assert.Equal(42, Invoke(type, "userCase"));
            Assert.Equal(42, Invoke(type, "userFunction"));
            Assert.Equal(42, Invoke(type, "localCase"));
            Assert.Equal(42, Invoke(type, "localResult"));
        });
    }

    [Fact]
    public async Task PreludeCaseNamesRemainAvailableAsBindingsForUnrelatedUnions()
    {
        const string source = """
            module Demo
            type U { | A }
            fn successBinding(): int {
                match U.A {
                    Success => if Success == U.A { 42 } else { 0 }
                }
            }
            fn errorBinding(): int {
                match U.A {
                    Error => if Error == U.A { 42 } else { 0 }
                }
            }
            """;
        await WithGenerated(source, type =>
        {
            Assert.Equal(42, Invoke(type, "successBinding"));
            Assert.Equal(42, Invoke(type, "errorBinding"));
        });
    }

    [Fact]
    public void UnusedStandardResultDoesNotChangeThePublicBoundUnionList()
    {
        const string ordinary = """
            module Demo
            type U { | A }
            fn first(): int {
                match U.A { Success => if Success == U.A { 42 } else { 0 } }
            }
            fn second(): int {
                match U.A { Error => if Error == U.A { 42 } else { 0 } }
            }
            """;
        var syntax = new Parser(Lexer.Lex(ordinary, "ordinary.ds"), "ordinary.ds").Parse();
        var bound = DSharp.Compiler.Binder.Bind(syntax, "ordinary.ds");
        Assert.Equal("U", Assert.Single(bound.Unions).Name);

        const string withResult = "module Demo\nfn answer(): Result<int, int> { Success(42) }";
        Assert.Empty(DSharpCompiler.Compile(withResult, "result.ds").Diagnostics);
    }

    [Theory]
    [InlineData("fn f(): Result<int, int> { Success() }")]
    [InlineData("fn f(): Result<Unit?, int> { Success() }")]
    [InlineData("fn f(): Result<Unit, int> { Success(null) }")]
    [InlineData("fn f(): Result<int, int> { Success(true) }")]
    [InlineData("fn f(): Result<int, int> { Error(true) }")]
    [InlineData("fn f(): Result<int, int> { Success(1, 2) }")]
    [InlineData("fn f(): Result<int, int> { Error() }")]
    [InlineData("fn f(): int { let value = Success(42)\n 42 }")]
    [InlineData("fn f(): int { let value = Success()\n 42 }")]
    [InlineData("fn f(): int { let value = Error(42)\n 42 }")]
    [InlineData("fn f(): int { let value = Success(null)\n 42 }")]
    [InlineData("fn f(value: Result<int, int>): int { match value { Success(n) => n } }")]
    [InlineData("fn f(value: Result<int, int>): int { match value { Error(n) => n } }")]
    [InlineData("fn f(value: Result<int?, int>): int { match value { Success(n) => n\n Error(e) => e } }")]
    [InlineData("fn make(): Result<int, int> { Success(42) }\nfn f(): int { make()\n 42 }")]
    [InlineData("fn make(): Result<int, int> { Success(42) }\nfn f(): Unit { if true { make()\n () } }")]
    [InlineData("fn f(): int { let saved: Result<int, int> = Success(42)\n saved\n 42 }")]
    [InlineData("type Other { | Success(int) }\nfn f(): Result<int, int> { Success(42) }")]
    [InlineData("type Other { | Success(int) }\nfn Success(value: int): int { value }\nfn f(): int { Success(42) }")]
    [InlineData("fn f(): Result<int, int> { Success(\"wrong\") }")]
    [InlineData("fn f(): Result<int, int> { Error(null) }")]
    public void InvalidResultProgramsAreRejected(string body)
    {
        var source = "module Demo\n" + body;
        var result = DSharpCompiler.Compile(source, "result.ds");
        Assert.Null(result.CSharp);
        var error = Assert.Single(result.Diagnostics);
        Assert.StartsWith("DS", error.Code);
        Assert.Equal("result.ds", error.Path);
        Assert.InRange(error.Span.Start, 0, source.Length);
        Assert.InRange(error.Span.Length, 0, source.Length - error.Span.Start);
        Assert.True(error.Span.Line >= 1);
        Assert.True(error.Span.Column >= 1);
    }

    [Theory]
    [InlineData("fn f(): Result<int, int> { ~Success() }", "Success")]
    [InlineData("fn f(): Result<int, int> { Success(~true) }", "true")]
    [InlineData("fn f(): Result<int, int> { Error(~true) }", "true")]
    [InlineData("fn f(): int { let value = ~Success(42)\n 42 }", "Success")]
    [InlineData("fn make(): Result<int, int> { Success(42) }\nfn f(): int { ~make()\n 42 }", "make")]
    public void ResultDiagnosticsPointAtOriginalDSharpSource(string marked, string token)
    {
        var marker = marked.IndexOf('~');
        var source = "module Demo\n" + marked.Remove(marker, 1);
        var start = "module Demo\n".Length + marker;
        var result = DSharpCompiler.Compile(source, "result.ds");
        Assert.Null(result.CSharp);
        var error = Assert.Single(result.Diagnostics);
        Assert.Equal("result.ds", error.Path);
        var prefix = source[..start];
        Assert.Equal(new SourceSpan(start, token.Length, prefix.Count(c => c == '\n') + 1,
            start - prefix.LastIndexOf('\n')), error.Span);
    }

    [Fact]
    public void StandardCasePatternDiagnosticDistinguishesAUserDefinedResult()
    {
        const string source = "module Demo\ntype Result { | Named(int) }\n" +
            "fn f(value: Result): int { match value { Success(n) => n\n _ => 0 } }";
        var result = DSharpCompiler.Compile(source, "result.ds");
        Assert.Null(result.CSharp);
        var error = Assert.Single(result.Diagnostics);
        Assert.Equal("result.ds", error.Path);
        Assert.Contains("standard Result", error.Message);
        Assert.Contains("not Result", error.Message);
        Assert.DoesNotContain("$", error.Message);
        var start = source.IndexOf("Success", StringComparison.Ordinal);
        var prefix = source[..start];
        Assert.Equal(new SourceSpan(start, "Success".Length, prefix.Count(c => c == '\n') + 1,
            start - prefix.LastIndexOf('\n')), error.Span);
    }

    private static object? Invoke(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.Invoke(null, null);

    private static async Task WithGenerated(string source, Action<Type> verify, Func<string, string>? transform = null)
    {
        var compilation = DSharpCompiler.Compile(source, "result.ds");
        Assert.Empty(compilation.Diagnostics);
        Assert.Equal(compilation.CSharp, DSharpCompiler.Compile(source).CSharp);
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-result-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var csharp = compilation.CSharp!;
            File.WriteAllText(Path.Combine(directory, "Generated.cs"), transform is null ? csharp : transform(csharp));
            File.WriteAllText(Path.Combine(directory, "Generated.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>");
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
            var context = new AssemblyLoadContext("result-" + Guid.NewGuid(), isCollectible: true);
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
