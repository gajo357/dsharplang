using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class UnionTests
{
    [Fact]
    public void DocumentationAttachesToUnionDeclarations()
    {
        const string source = "module Demo\n/// A result of processing.\ntype Outcome {\n | Done\n | Amount(int)\n}\nfn result(): Outcome { Outcome.Done }";
        var module = new Parser(Lexer.Lex(source, "unions.ds"), "unions.ds").Parse();
        var union = Assert.Single(module.Unions);
        Assert.Equal("Outcome", union.Name.Text);
        Assert.Equal(new[] { "Done", "Amount" }, union.Cases.Select(c => c.Name.Text));
        var doc = Assert.Single(union.DocumentationComments);
        Assert.Equal("/// A result of processing.", doc.Text);
        Assert.Equal(new SourceSpan(source.IndexOf("///", StringComparison.Ordinal), doc.Text.Length, 2, 1), doc.Span);
    }

    [Fact]
    public async Task CasesWrappersAndNestedValuesExecuteWithStructuralEquality()
    {
        const string source = """
            module Demo
            fn forwardValue(): Later { Later.Message("later") }
            type Later { | Message(string) }
            /// A status with named alternatives.
            type Status {
                | Ready
                | Idle()
                | Value(int)
                | Details(int, string, bool, char, Unit)
            }
            type Outer { | Wrap(Status) }
            type Envelope { status: Status
                            label: string }
            type WrappedEnvelope { | Box(Envelope) }
            type InvoiceId { | InvoiceId(int) }
            type CustomerId { | CustomerId(int) }
            fn nullary(): bool { Ready == Status.Ready }
            fn nullaryCall(): bool { Status.Ready() == Ready() }
            fn declaredEmptyParens(): bool { Idle == Status.Idle() }
            fn oneValue(): bool { Value(42) == Status.Value(42) }
            fn multiValue(): bool {
                Details(42, "héllo", true, '/', ()) == Status.Details(42, "héllo", true, '/', ())
            }
            fn differentTag(): bool { Status.Ready != Status.Value(42) }
            fn differentPayload(): bool { Status.Value(42) != Status.Value(43) }
            fn nestedUnion(): bool { Outer.Wrap(Value(42)) == Outer.Wrap(Status.Value(42)) }
            fn recordPayload(): bool {
                let first = WrappedEnvelope.Box(Envelope { status = Value(42)
                                                            label = "ok" })
                let second = WrappedEnvelope.Box(Envelope { label = "ok"
                                                             status = Status.Value(42) })
                first == second
            }
            fn recordWithUnion(): bool {
                let first = Envelope { status = OuterReady()
                                       label = "ok" }
                let second = first with { status = Status.Ready }
                first == second
            }
            fn OuterReady(): Status { Ready }
            fn wrapper(): bool { InvoiceId(42) == InvoiceId.InvoiceId(42) }
            fn forward(): bool { forwardValue() == Later.Message("later") }
            """;
        await WithGenerated(source, type =>
        {
            foreach (var name in new[] { "nullary", "nullaryCall", "declaredEmptyParens", "oneValue", "multiValue", "differentTag", "differentPayload", "nestedUnion", "recordPayload", "recordWithUnion", "wrapper", "forward" })
                Assert.Equal(true, Invoke(type, name));
        });
    }

    [Fact]
    public async Task QualifiedCasesResolveAmbiguityAndLocalsTakePrecedence()
    {
        const string source = """
            module Demo
            type InvoiceError { | NotFound(int) }
            type PaymentError { | NotFound(int) }
            type State { | Done }
            type Holder { Done: int }
            type Result { | Named(int) }
            type Foo { number: int }
            type Example { | Foo }
            fn Named(value: int): int { value }
            fn useInvoice(value: InvoiceError): bool { value == InvoiceError.NotFound(42) }
            fn qualified(): bool { useInvoice(InvoiceError.NotFound(42)) }
            fn otherQualified(): bool { PaymentError.NotFound(42) == PaymentError.NotFound(42) }
            fn qualifiedOverFunction(): bool { Result.Named(42) == Result.Named(42) }
            fn qualifiedOverRecord(): bool { Example.Foo == Example.Foo }
            fn localShadowsUnionName(): int {
                let State = Holder { Done = 42 }
                State.Done
            }
            fn localShadowsCaseName(): int {
                let Done = 42
                Done
            }
            """;
        await WithGenerated(source, type =>
        {
            Assert.Equal(true, Invoke(type, "qualified"));
            Assert.Equal(true, Invoke(type, "otherQualified"));
            Assert.Equal(true, Invoke(type, "qualifiedOverFunction"));
            Assert.Equal(true, Invoke(type, "qualifiedOverRecord"));
            Assert.Equal(42, Invoke(type, "localShadowsUnionName"));
            Assert.Equal(42, Invoke(type, "localShadowsCaseName"));
        });
    }

    [Fact]
    public async Task PayloadsEvaluateLeftToRightAndReturnsSkipRemainingPayloads()
    {
        const string source = """
            module Demo
            type Outcome { | Finished
                           | Pair(int, int) }
            fn faultFirst(): Outcome { Outcome.Pair(1 / 0, 2147483647 + 1) }
            fn overflowFirst(): Outcome { Outcome.Pair(2147483647 + 1, 1 / 0) }
            fn early(): Outcome { Outcome.Pair(if true { return Outcome.Finished } else { 1 }, 1 / 0) }
            fn earlyResult(): bool { early() == Outcome.Finished }
            fn never(): Outcome { Outcome.Pair(if true { return Outcome.Finished } else { return Outcome.Finished }, 1 / 0) }
            fn neverResult(): bool { never() == Outcome.Finished }
            """;
        await WithGenerated(source, type =>
        {
            Assert.IsType<DivideByZeroException>(Assert.Throws<TargetInvocationException>(() => Invoke(type, "faultFirst")).InnerException);
            Assert.IsType<OverflowException>(Assert.Throws<TargetInvocationException>(() => Invoke(type, "overflowFirst")).InnerException);
            Assert.Equal(true, Invoke(type, "earlyResult"));
            Assert.Equal(true, Invoke(type, "neverResult"));
        });
    }

    [Fact]
    public async Task CaseAndTypeNamesAvoidGeneratedAndCSharpNameCollisions()
    {
        const string source = """
            module Demo
            type class { | event(int)
                         | Equals
                         | GetHashCode
                         | ToString }
            fn keywordCase(): bool { class.event(42) == class.event(42) }
            fn names(): bool { class.Equals != class.GetHashCode && class.ToString == class.ToString }
            """;
        await WithGenerated(source, type =>
        {
            Assert.Equal(true, Invoke(type, "keywordCase"));
            Assert.Equal(true, Invoke(type, "names"));
        });
    }

    [Theory]
    [InlineData("type U { | Item(int) }\nfn f(): U { Item() }")]
    [InlineData("type U { | Item(int) }\nfn f(): U { U.Item(1, 2) }")]
    [InlineData("type U { | Item(int) }\nfn f(): U { U.Item(true) }")]
    [InlineData("type U { | Ready }\nfn f(): U { U.Ready(1) }")]
    [InlineData("type U { | Item(Missing) }\nfn f(): int { 0 }")]
    [InlineData("type U { | Item\n | Item(int) }\nfn f(): int { 0 }")]
    [InlineData("type U { | Item }\nfn f(): U { U.Missing }")]
    [InlineData("type U { | Item }\ntype U { | Other }\nfn f(): int { 0 }")]
    [InlineData("type U { | Item }\ntype V { | Item }\nfn f(): U { Item }")]
    [InlineData("type U { | Item(int) }\ntype V { | Item(int) }\nfn f(): U { Item(42) }")]
    [InlineData("type U { | Item(int) }\nfn Item(value: int): int { value }\nfn f(): U { Item(42) }")]
    [InlineData("type U { | Item(int) }\nfn Item(value: int): int { value }\nfn f(): int { Item(42) }")]
    [InlineData("type InvoiceId { | InvoiceId(int) }\ntype CustomerId { | CustomerId(int) }\nfn use(value: InvoiceId): int { 1 }\nfn f(): int { use(CustomerId(42)) }")]
    [InlineData("type InvoiceId { | InvoiceId(int) }\ntype CustomerId { | CustomerId(int) }\nfn f(): bool { InvoiceId(42) == CustomerId(42) }")]
    [InlineData("type U { | Item(int) }\nfn f(): U { let value = U.Item(42)\n value.Item = 2\n value }")]
    [InlineData("type U { | Item }\ntype Item { number: int }\nfn f(): U { Item }")]
    [InlineData("type U { | Item(int) }\ntype Item { number: int }\nfn f(): U { Item(42) }")]
    [InlineData("type U { | Item }\ntype Item { number: int }\nfn f(): Item { Item { number = 42 } }")]
    public void InvalidUnionsProduceSourceDiagnostics(string body)
    {
        var source = "module Demo\n" + body;
        var result = DSharpCompiler.Compile(source, "unions.ds");
        Assert.Null(result.CSharp);
        var error = Assert.Single(result.Diagnostics);
        Assert.StartsWith("DS", error.Code);
        Assert.Equal("unions.ds", error.Path);
        Assert.InRange(error.Span.Start, 0, source.Length);
        Assert.InRange(error.Span.Length, 0, source.Length - error.Span.Start);
        Assert.True(error.Span.Line >= 1);
        Assert.True(error.Span.Column >= 1);
    }

    [Theory]
    [InlineData("type U { | Item(int) }\nfn f(): U { U.Item(~true) }", "true")]
    [InlineData("type U { | Item(int) }\nfn f(): U { U.~Missing(1) }", "Missing")]
    [InlineData("type U { | Item }\ntype Item { number: int }\nfn f(): Item { ~Item { number = 42 } }", "Item")]
    public void UnionDiagnosticsReferToOriginalSource(string marked, string token)
    {
        // Mark the erroneous token rather than parsing the diagnostic message.
        var marker = marked.IndexOf('~');
        var unmarked = marked.Remove(marker, 1);
        var start = "module Demo\n".Length + marker;
        var source = "module Demo\n" + unmarked;
        var result = DSharpCompiler.Compile(source, "unions.ds");
        var error = Assert.Single(result.Diagnostics);
        Assert.Null(result.CSharp);
        Assert.Equal("unions.ds", error.Path);
        var prefix = source[..start];
        Assert.Equal(new SourceSpan(start, token.Length, prefix.Count(c => c == '\n') + 1,
            start - prefix.LastIndexOf('\n')), error.Span);
    }

    private static object? Invoke(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.Invoke(null, null);

    private static async Task WithGenerated(string source, Action<Type> verify)
    {
        var compilation = DSharpCompiler.Compile(source, "unions.ds");
        Assert.Empty(compilation.Diagnostics);
        Assert.Equal(compilation.CSharp, DSharpCompiler.Compile(source).CSharp);
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-unions-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "Generated.cs"), compilation.CSharp);
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
            var context = new AssemblyLoadContext("unions-" + Guid.NewGuid(), isCollectible: true);
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
