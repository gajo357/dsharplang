using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class MatchTests
{
    [Fact]
    public async Task NestedPatternsBindingsGuardsAndPrimitiveMatchesExecute()
    {
        const string source = """
            module Demo
            type Atom { | Alpha
                        | Beta }
            type Packet { | Empty
                          | Pair(bool, bool)
                          | Inner(Atom)
                          | Number(int) }
            type UnitBox { | Wrap(Unit) }
            type Unrelated { | Name }
            fn classify(value: Packet): int {
                match value {
                    Packet.Empty => 0
                    Packet.Pair(true, true) => 11
                    Packet.Pair(true, false) => 12
                    Packet.Pair(false, true) => 13
                    Packet.Pair(false, false) => 14
                    Packet.Inner(Atom.Alpha) => 21
                    Packet.Inner(Atom.Beta) => 22
                    Packet.Number(n) when n > 10 => n
                    Packet.Number(n) => n + 1
                }
            }
            fn pair(): int { classify(Packet.Pair(false, true)) }
            fn inner(): int { classify(Packet.Inner(Atom.Beta)) }
            fn guard(): int { classify(Packet.Number(42)) }
            fn fallback(): int { classify(Packet.Number(4)) }
            fn bareCase(): int {
                match Packet.Number(42) {
                    Number(n) => n
                    _ => 0
                }
            }
            fn boolean(value: bool): int {
                match value {
                    true => 42
                    false => 0
                }
            }
            fn truth(): int { boolean(true) }
            fn number(): int {
                match -1 {
                    -1 => 42
                    n => n
                }
            }
            fn text(): int {
                match "hello" {
                    "hello" => 42
                    _ => 0
                }
            }
            fn character(): int {
                match '/' {
                    '/' => 42
                    _ => 0
                }
            }
            fn unit(): int { match () { () => 42 } }
            fn unitPayload(): int {
                match UnitBox.Wrap(()) {
                    UnitBox.Wrap(()) => 42
                }
            }
            fn keywords(): int { match 42 { class => class } }
            fn bindingNamedLikeCase(): int { match 42 { Name => Name } }
            fn insideExpression(): int {
                2 + (match true {
                    true => 40
                    false => 0
                })
            }
            """;
        await WithGenerated(source, type =>
        {
            Assert.Equal(13, Invoke(type, "pair"));
            Assert.Equal(22, Invoke(type, "inner"));
            Assert.Equal(42, Invoke(type, "guard"));
            Assert.Equal(5, Invoke(type, "fallback"));
            foreach (var name in new[] { "bareCase", "truth", "number", "text", "character", "unit", "unitPayload", "keywords", "bindingNamedLikeCase", "insideExpression" })
                Assert.Equal(42, Invoke(type, name));
        });
    }

    [Fact]
    public async Task ScrutineeIsEvaluatedExactlyOnce()
    {
        const string source = """
            module Demo
            fn tick(): bool { true }
            fn answer(): int {
                match tick() {
                    false => 0
                    true => 42
                }
            }
            """;
        await WithGenerated(source, type =>
        {
            Assert.Equal(42, Invoke(type, "answer"));
            Assert.Equal(1, Invoke(type, "readTickCount"));
        }, csharp =>
        {
            const string signature = "    public static bool @tick()";
            Assert.Equal(1, csharp.Split(signature, StringSplitOptions.None).Length - 1);
            return csharp.Replace(signature,
                "    private static int __testTickCount;\n" +
                "    public static int @readTickCount() => __testTickCount;\n" +
                "    public static bool @tick() { __testTickCount++; return @tick_original(); }\n" +
                "    public static bool @tick_original()", StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task FirstMatchingArmAndLazyGuardsPreserveEarlyReturns()
    {
        const string source = """
            module Demo
            type Input { | Empty
                         | Number(int) }
            fn first(): int {
                match 5 {
                    x when x == 5 => 42
                    _ => 1 / 0
                }
            }
            fn falseGuard(): int {
                match 5 {
                    x when x < 0 => 1 / 0
                    _ => 42
                }
            }
            fn patternBeforeGuard(): int {
                match Input.Empty {
                    Input.Number(n) when 1 / 0 == 0 => n
                    _ => 42
                }
            }
            fn bodyReturn(): int {
                let selected = match true {
                    true => if true { return 42 } else { 0 }
                    false => 1 / 0
                }
                selected + 1 / 0
            }
            fn guardReturn(): int {
                match 5 {
                    n when if true { return 42 } else { false } => n
                    _ => 1 / 0
                }
            }
            fn guardAlwaysReturns(): int {
                match 5 {
                    5 when if true { return 42 } else { return 7 } => "unreachable"
                    _ => 0
                }
            }
            fn scrutineeReturn(): int {
                match (if true { return 42 } else { false }) {
                    true => 1 / 0
                    false => 1 / 0
                }
            }
            fn scrutineeFault(): int {
                match (1 / 0) {
                    _ => 2147483647 + 1
                }
            }
            """;
        await WithGenerated(source, type =>
        {
            foreach (var name in new[] { "first", "falseGuard", "patternBeforeGuard", "bodyReturn", "guardReturn", "guardAlwaysReturns", "scrutineeReturn" })
                Assert.Equal(42, Invoke(type, name));
            Assert.IsType<DivideByZeroException>(Assert.Throws<TargetInvocationException>(() => Invoke(type, "scrutineeFault")).InnerException);
        });
    }

    [Fact]
    public async Task QualifiedPatternsDisambiguateCasesAndWildcardCoversFutureAlternatives()
    {
        const string source = """
            module Demo
            type First { | Found(int)
                         | Other
                         | AddedLater }
            type Second { | Found(int) }
            fn withExplicit(value: First): int {
                match value {
                    First.Found(x) => x
                    First.Other => 0
                    First.AddedLater => 1
                }
            }
            fn withWildcard(value: First): int {
                match value {
                    First.Found(x) => x
                    _ => 42
                }
            }
            fn explicit(): int { withExplicit(First.Found(42)) }
            fn wildcard(): int { withWildcard(First.AddedLater) }
            """;
        await WithGenerated(source, type =>
        {
            Assert.Equal(42, Invoke(type, "explicit"));
            Assert.Equal(42, Invoke(type, "wildcard"));
        });
    }

    [Fact]
    public async Task DisjointNestedPatternsCoverCompleteBooleanProducts()
    {
        const string source = """
            module Demo
            type Switch { | Off
                          | On }
            type Tree { | Node(Switch, bool, bool)
                        | Empty }
            type Chain { | End
                         | Link(bool, Chain) }
            fn score(value: Tree): int {
                match value {
                    Tree.Empty => 0
                    Tree.Node(Switch.Off, _, _) => 10
                    Tree.Node(Switch.On, true, _) => 20
                    Tree.Node(Switch.On, false, true) => 30
                    Tree.Node(Switch.On, false, false) => 42
                }
            }
            fn answer(): int { score(Tree.Node(Switch.On, false, false)) }
            fn recursive(): int {
                match Chain.Link(true, Chain.End) {
                    Chain.End => 0
                    Chain.Link(_, _) => 42
                }
            }
            """;
        await WithGenerated(source, type =>
        {
            Assert.Equal(42, Invoke(type, "answer"));
            Assert.Equal(42, Invoke(type, "recursive"));
        });
    }

    [Theory]
    [InlineData("fn f(value: bool): int { match value { true => 1 } }")]
    [InlineData("fn f(value: bool): int { match value { true when true => 1\n false => 2 } }")]
    [InlineData("fn f(value: Unit): int { match value { () when true => 1 } }")]
    [InlineData("type U { | Wrap(Unit) }\nfn f(value: U): int { match value { U.Wrap(()) when true => 1 } }")]
    [InlineData("fn f(value: int): int { match value { 1 => 1\n 2 => 2 } }")]
    [InlineData("fn f(value: string): int { match value { \"a\" => 1 } }")]
    [InlineData("type U { | A\n | B\n | Added }\nfn f(value: U): int { match value { U.A => 1\n U.B => 2 } }")]
    [InlineData("type U { | Pair(bool, bool) }\nfn f(value: U): int { match value { U.Pair(true, _) => 1\n U.Pair(_, true) => 2 } }")]
    [InlineData("type V { | A\n | B }\ntype U { | Wrap(V) }\nfn f(value: U): int { match value { U.Wrap(V.A) => 1 } }")]
    [InlineData("type U { | A\n | B }\nfn f(value: U): int { match value { U.A when true => 1\n U.B => 2 } }")]
    [InlineData("fn f(value: bool): int { match value { true => 1\n false => true } }")]
    [InlineData("fn f(value: bool): int { match value { true when 1 => 1\n _ => 2 } }")]
    [InlineData("type U { | Item(int) }\nfn f(value: U): int { match value { U.Item => 1 } }")]
    [InlineData("type U { | Item(int) }\nfn f(value: U): int { match value { U.Item(x, y) => 1\n _ => 2 } }")]
    [InlineData("type U { | Item(int) }\nfn f(value: U): int { match value { U.Item(true) => 1\n _ => 2 } }")]
    [InlineData("type U { | A }\ntype V { | B }\nfn f(value: U): int { match value { V.B => 1\n _ => 2 } }")]
    [InlineData("type U { | A }\ntype V { | B }\nfn f(value: U): int { match value { B => 1\n _ => 2 } }")]
    [InlineData("type U { | A }\ntype V { | B }\ntype W { | B }\nfn f(value: U): int { match value { B => 1\n _ => 2 } }")]
    [InlineData("type U { | Item(int) }\ntype V { | Item(int) }\nfn f(value: U): int { match value { Item(n) => n\n _ => 0 } }")]
    [InlineData("type U { | Item(int) }\nfn Item(n: int): int { n }\nfn f(value: U): int { match value { Item(n) => n\n _ => 0 } }")]
    [InlineData("type U { | Pair(int, int) }\nfn f(value: U): int { match value { U.Pair(x, x) => x } }")]
    [InlineData("type U { | Item(int) }\nfn f(value: U): int { let x = 1\n match value { U.Item(x) => x } }")]
    [InlineData("type U { | Item(int)\n | Empty }\nfn f(value: U): int { let result = match value { U.Item(x) => x\n U.Empty => 0 }\n x }")]
    [InlineData("fn f(value: bool): int { match value { true => 1\n false when x > 0 => 2\n _ => 0 } }")]
    public void InvalidPatternsCoverageAndScopesAreDiagnosed(string body)
    {
        var source = "module Demo\n" + body;
        var result = DSharpCompiler.Compile(source, "match.ds");
        Assert.Null(result.CSharp);
        var error = Assert.Single(result.Diagnostics);
        Assert.StartsWith("DS", error.Code);
        Assert.Equal("match.ds", error.Path);
        Assert.InRange(error.Span.Start, 0, source.Length);
        Assert.InRange(error.Span.Length, 0, source.Length - error.Span.Start);
        Assert.True(error.Span.Line >= 1);
        Assert.True(error.Span.Column >= 1);
    }

    [Theory]
    [InlineData("fn f(value: bool): int { match value { true when ~1 => 1\n _ => 2 } }", "1")]
    [InlineData("fn f(value: bool): int { match value { true => 1\n false => ~true } }", "true")]
    [InlineData("type U { | Item(int) }\nfn f(value: U): int { match value { U.Item(~true) => 1\n _ => 2 } }", "true")]
    [InlineData("type U { | Item(int) }\nfn f(value: U): int { match value { U.~Item(x, y) => 1\n _ => 2 } }", "Item")]
    [InlineData("fn f(value: bool): int { match value { true => ~missing\n false => 1 } }", "missing")]
    public void DiagnosticsPointAtOriginalPatternGuardOrExpression(string marked, string token)
    {
        var marker = marked.IndexOf('~');
        var source = "module Demo\n" + marked.Remove(marker, 1);
        var start = "module Demo\n".Length + marker;
        var result = DSharpCompiler.Compile(source, "match.ds");
        Assert.Null(result.CSharp);
        var error = Assert.Single(result.Diagnostics);
        Assert.Equal("match.ds", error.Path);
        var prefix = source[..start];
        Assert.Equal(new SourceSpan(start, token.Length, prefix.Count(c => c == '\n') + 1,
            start - prefix.LastIndexOf('\n')), error.Span);
    }

    private static object? Invoke(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.Invoke(null, null);

    private static async Task WithGenerated(string source, Action<Type> verify, Func<string, string>? transform = null)
    {
        var compilation = DSharpCompiler.Compile(source, "match.ds");
        Assert.Empty(compilation.Diagnostics);
        Assert.Equal(compilation.CSharp, DSharpCompiler.Compile(source).CSharp);
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-match-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var csharp = compilation.CSharp!;
            File.WriteAllText(Path.Combine(directory, "Generated.cs"), transform is null ? csharp : transform(csharp));
            File.WriteAllText(Path.Combine(directory, "Generated.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>");
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
            var context = new AssemblyLoadContext("match-" + Guid.NewGuid(), isCollectible: true);
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
