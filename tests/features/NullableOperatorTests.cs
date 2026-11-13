using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class NullableOperatorTests
{
    [Fact]
    public void CoalescingIsRightAssociativeAndConditionalAccessParsesInsideConditions()
    {
        const string source = """
            module Demo
            type Item { enabled: bool }
            fn f(a: int?, b: int?, c: int): int { a ?? b ?? c }
            fn condition(): int { if Item { enabled = true }?.enabled ?? false { 42 } else { 0 } }
            """;
        var module = new Parser(Lexer.Lex(source, "operators.ds"), "operators.ds").Parse();
        var first = Assert.IsType<CoalesceSyntax>(module.Functions[0].Expression);
        Assert.Equal("a", Assert.IsType<ValueSyntax>(first.Left).Value.Text);
        var nested = Assert.IsType<CoalesceSyntax>(first.Right);
        Assert.Equal("b", Assert.IsType<ValueSyntax>(nested.Left).Value.Text);
        Assert.Equal("c", Assert.IsType<ValueSyntax>(nested.Right).Value.Text);
        var conditional = Assert.IsType<IfSyntax>(module.Functions[1].Expression);
        var condition = Assert.IsType<CoalesceSyntax>(conditional.Condition);
        var access = Assert.IsType<ConditionalFieldAccessSyntax>(condition.Left);
        Assert.Equal("enabled", access.Field.Text);
        Assert.IsType<RecordConstructionSyntax>(access.Receiver);
    }

    [Fact]
    public async Task FallbackIsLazyRightAssociativeAndRespectsPrecedence()
    {
        const string source = """
            module Demo
            fn present(): int {
                let value: int? = 42
                value ?? (1 / 0)
            }
            fn absent(): int {
                let value: int? = null
                value ?? 42
            }
            fn chain(): int {
                let first: int? = null
                let second: int? = null
                let third: int? = 42
                first ?? second ?? third ?? (1 / 0)
            }
            fn boolPrecedence(): bool {
                let value: bool? = false
                value ?? true || true
            }
            fn arithmeticPrecedence(): int {
                let value: int? = 1
                value ?? 2 + 3
            }
            fn arithmeticFallback(): int {
                let value: int? = null
                value ?? 20 + 22
            }
            fn missingFallback(): int {
                let value: int? = null
                value ?? (1 / 0)
            }
            fn nullableFallback(first: int?, second: int?): int? { first ?? second }
            fn resultType(): bool {
                let missing: int? = nullableFallback(null, null)
                let present: int? = nullableFallback(null, 42)
                (missing == null) && (present == 42)
            }
            fn unit(): Unit {
                let value: Unit? = null
                value ?? ()
            }
            fn unitPresent(): Unit {
                let value: Unit? = ()
                value ?? ()
            }
            fn text(): string {
                let value: string? = null
                value ?? "answer"
            }
            """;
        await WithGenerated(source, type =>
        {
            Assert.Equal(42, Invoke(type, "present"));
            Assert.Equal(42, Invoke(type, "absent"));
            Assert.Equal(42, Invoke(type, "chain"));
            Assert.Equal(false, Invoke(type, "boolPrecedence"));
            Assert.Equal(1, Invoke(type, "arithmeticPrecedence"));
            Assert.Equal(42, Invoke(type, "arithmeticFallback"));
            Assert.Equal(true, Invoke(type, "resultType"));
            Assert.Equal("answer", Invoke(type, "text"));
            Assert.True(Invoke(type, "unit")!.GetType().IsValueType);
            Assert.True(Invoke(type, "unitPresent")!.GetType().IsValueType);
            Assert.IsType<DivideByZeroException>(Assert.Throws<TargetInvocationException>(() => Invoke(type, "missingFallback")).InnerException);
        });
    }

    [Fact]
    public async Task ConditionalAccessChainsFlattenNullableFieldsAndPreserveAbsence()
    {
        const string source = """
            module Demo
            type Detail { number: int?
                          title: string?
                          flag: bool
                          GetHashCode: int
                          unit: Unit }
            type Holder { detail: Detail? }
            fn number(holder: Holder?): int? { holder?.detail?.number }
            fn title(holder: Holder?): string? { holder?.detail?.title }
            fn flag(holder: Holder?): bool? { holder?.detail?.flag }
            fn unit(holder: Holder?): Unit? { holder?.detail?.unit }
            fn regularRecord(): int? {
                let detail = Detail { number = 42
                                      title = "ready"
                                      flag = true
                                      GetHashCode = 42
                                      unit = () }
                detail?.number
            }
            fn present(): int {
                let holder = Holder { detail = Detail { number = 42
                                                        title = "ready"
                                                        flag = true
                                                        GetHashCode = 42
                                                        unit = () } }
                let number: int? = holder?.detail?.number
                let label = title(holder) ?? "missing"
                if (number == 42) && (label == "ready") && (flag(holder) == true) &&
                   (unit(holder) != null) && (regularRecord() == 42) &&
                   (holder?.detail?.GetHashCode == 42) { 42 } else { 0 }
            }
            fn absentReceiver(): int { number(null) ?? 42 }
            fn absentInner(): int {
                let holder = Holder { detail = null }
                number(holder) ?? 42
            }
            fn absentField(): int {
                let holder = Holder { detail = Detail { number = null
                                                        title = null
                                                        flag = false
                                                        GetHashCode = 0
                                                        unit = () } }
                if (title(holder) == null) && (flag(holder) == false) {
                    number(holder) ?? 42
                } else { 0 }
            }
            fn leadingAccess(): int {
                let holder = Holder { detail = Detail { number = 42
                                                        title = null
                                                        flag = true
                                                        GetHashCode = 42
                                                        unit = () } }
                holder
                    ?.detail
                    ?.number ?? 0
            }
            fn constructionInCondition(): int {
                if Detail { number = 1
                            title = null
                            flag = true
                            GetHashCode = 42
                            unit = () }?.flag ?? false { 42 } else { 0 }
            }
            """;
        await WithGenerated(source, type =>
        {
            foreach (var name in new[] { "present", "absentReceiver", "absentInner", "absentField", "leadingAccess", "constructionInCondition" })
                Assert.Equal(42, Invoke(type, name));
        });
    }

    [Fact]
    public async Task FallbackInferenceAndEarlyReturnRespectContextAndLaziness()
    {
        const string source = """
            module Demo
            type Item { number: int }
            type Choice { | Value(int)
                          | Empty }
            fn inferred(): int { null ?? 42 }
            fn bothNull(): int? { null ?? null }
            fn optional(flag: bool): int? {
                let first: int? = null
                first ?? (if flag { null } else { 42 })
            }
            fn matching(flag: bool): int? {
                let first: int? = null
                first ?? (match flag { true => null
                                       false => 42 })
            }
            fn inferredBranches(flag: bool): int {
                let first: int? = null
                let value = first ?? (if flag { null } else { 42 })
                match value { null => 42
                              number => number }
            }
            fn earlyFallback(): int {
                let first: int? = null
                first ?? (if true { return 42 } else { 1 / 0 })
            }
            fn skippedReturn(): int {
                let first: int? = 42
                first ?? (if true { return 7 } else { 1 / 0 })
            }
            fn earlyLeft(): int {
                let first: int? = null
                (if true { return 42 } else { first }) ?? (1 / 0)
            }
            fn earlyReceiver(): int {
                let item: Item? = null
                (if true { return 42 } else { item })?.number ?? (1 / 0)
            }
            fn referenceFallback(): int {
                let item: Item? = null
                (item ?? Item { number = 42 }).number
            }
            fn unionFallback(): bool {
                let choice: Choice? = null
                (choice ?? Choice.Value(42)) == Choice.Value(42)
            }
            fn skipNullableFallback(): int {
                let first: int? = 42
                let second: int? = first ?? (if true { return 7 } else { null })
                second ?? 0
            }
            fn run(): int {
                if (inferred() == 42) && (bothNull() == null) &&
                   (optional(true) == null) && (optional(false) == 42) &&
                   (matching(true) == null) && (matching(false) == 42) &&
                   (inferredBranches(true) == 42) && (inferredBranches(false) == 42) &&
                   (earlyFallback() == 42) && (skippedReturn() == 42) &&
                   (earlyLeft() == 42) && (earlyReceiver() == 42) &&
                   (referenceFallback() == 42) && unionFallback() &&
                   (skipNullableFallback() == 42) { 42 } else { 0 }
            }
            """;
        await WithGenerated(source, type => Assert.Equal(42, Invoke(type, "run")));
    }

    [Fact]
    public async Task FallbackLeftAndConditionalReceiverAreEachEvaluatedExactlyOnce()
    {
        const string source = """
            module Demo
            type Item { number: int }
            fn tick(): int? { 42 }
            fn probe(): Item? { Item { number = 42 } }
            fn probeMissing(): Item? { null }
            fn fallback(): int { tick() ?? 0 }
            fn access(): int { probe()?.number ?? 0 }
            fn accessMissing(): int { probeMissing()?.number ?? 42 }
            """;
        await WithGenerated(source, type =>
        {
            Assert.Equal(42, Invoke(type, "fallback"));
            Assert.Equal(1, Invoke(type, "readTickCount"));
            Assert.Equal(42, Invoke(type, "access"));
            Assert.Equal(1, Invoke(type, "readProbeCount"));
            Assert.Equal(42, Invoke(type, "accessMissing"));
            Assert.Equal(1, Invoke(type, "readMissingCount"));
        }, csharp => Instrument(Instrument(Instrument(csharp, "tick", "Tick"), "probe", "Probe"), "probeMissing", "Missing"));
    }

    [Theory]
    [InlineData("fn f(): int { 1 ?? 2 }")]
    [InlineData("fn f(value: int?): int { value ?? true }")]
    [InlineData("fn f(): int { let value = null ?? null\n 42 }")]
    [InlineData("fn f(): int { null ?? null }")]
    [InlineData("type Item { number: int }\nfn f(value: Item?): int { value.number }")]
    [InlineData("type Item { number: int }\nfn f(value: Item?): int { value?.missing ?? 0 }")]
    [InlineData("type Item { number: int }\nfn f(value: Item?): int { value?.number() ?? 0 }")]
    [InlineData("fn f(value: int?): int { value?.number ?? 0 }")]
    [InlineData("type Item { number: int }\nfn f(value: Item?): int { value?.number + 1 }")]
    [InlineData("type Item { number: int }\nfn f(): int { null?.number ?? 0 }")]
    [InlineData("type Item { number: int }\nfn f(value: Item?): int { let number: int = value?.number\n number }")]
    [InlineData("fn f(value: int?): int { value ?? \"wrong\" }")]
    [InlineData("fn f(): int { let value: int? = null\n let fallback: bool? = null\n value ?? fallback\n 0 }")]
    public void InvalidOperatorsAreDiagnosed(string body)
    {
        var source = "module Demo\n" + body;
        var result = DSharpCompiler.Compile(source, "operators.ds");
        Assert.Null(result.CSharp);
        var error = Assert.Single(result.Diagnostics);
        Assert.StartsWith("DS", error.Code);
        Assert.Equal("operators.ds", error.Path);
        Assert.InRange(error.Span.Start, 0, source.Length);
        Assert.InRange(error.Span.Length, 0, source.Length - error.Span.Start);
        Assert.True(error.Span.Line >= 1);
        Assert.True(error.Span.Column >= 1);
    }

    [Theory]
    [InlineData("fn f(): int { ~1 ?? 2 }", "1")]
    [InlineData("fn f(value: int?): int { value ?? ~true }", "true")]
    [InlineData("type Item { number: int }\nfn f(value: Item?): int { value?.~missing ?? 0 }", "missing")]
    public void InvalidOperatorDiagnosticsPreserveSourceSpans(string marked, string token)
    {
        var marker = marked.IndexOf('~');
        var source = "module Demo\n" + marked.Remove(marker, 1);
        var start = "module Demo\n".Length + marker;
        var result = DSharpCompiler.Compile(source, "operators.ds");
        Assert.Null(result.CSharp);
        var error = Assert.Single(result.Diagnostics);
        Assert.Equal("operators.ds", error.Path);
        var prefix = source[..start];
        Assert.Equal(new SourceSpan(start, token.Length, prefix.Count(c => c == '\n') + 1,
            start - prefix.LastIndexOf('\n')), error.Span);
    }

    private static string Instrument(string csharp, string name, string suffix)
    {
        var pattern = @"    public static ([^\r\n(]+) @" + name + @"\(\)";
        var match = Assert.Single(Regex.Matches(csharp, pattern).Cast<Match>());
        var resultType = match.Groups[1].Value;
        return csharp.Replace(match.Value,
            "    private static int __test" + suffix + "Count;\n" +
            "    public static int @read" + suffix + "Count() => __test" + suffix + "Count;\n" +
            "    public static " + resultType + " @" + name + "() { __test" + suffix + "Count++; return @" + name + "_original(); }\n" +
            "    public static " + resultType + " @" + name + "_original()", StringComparison.Ordinal);
    }

    private static object? Invoke(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.Invoke(null, null);

    private static async Task WithGenerated(string source, Action<Type> verify, Func<string, string>? transform = null)
    {
        var compilation = DSharpCompiler.Compile(source, "operators.ds");
        Assert.Empty(compilation.Diagnostics);
        Assert.Equal(compilation.CSharp, DSharpCompiler.Compile(source).CSharp);
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-nullable-operators-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var csharp = compilation.CSharp!;
            File.WriteAllText(Path.Combine(directory, "Generated.cs"), transform is null ? csharp : transform(csharp));
            File.WriteAllText(Path.Combine(directory, "Generated.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net11.0</TargetFramework><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>");
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
            var context = new AssemblyLoadContext("nullable-operators-" + Guid.NewGuid(), isCollectible: true);
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
