using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class RecordTests
{
    [Fact]
    public void DocumentationAttachesToRecordDeclarations()
    {
        const string source = "module Demo\n/// Invoice documentation\ntype Invoice { total: int }\nfn answer(): int { 42 }";
        var module = new Parser(Lexer.Lex(source, "records.ds"), "records.ds").Parse();
        var record = Assert.Single(module.Records);
        Assert.Equal("Invoice", record.Name.Text);
        var doc = Assert.Single(record.DocumentationComments);
        Assert.Equal("/// Invoice documentation", doc.Text);
        Assert.Equal(new SourceSpan(source.IndexOf("///", StringComparison.Ordinal), doc.Text.Length, 2, 1), doc.Span);
    }

    [Fact]
    public async Task RecordsExecuteAsImmutableValuesWithStructuralEquality()
    {
        const string source = """
            module Demo
            /// A public immutable value.
            type Invoice {
                total: int
                title: string
                paid: bool
                marker: char
                empty: Unit
            }
            type Envelope {
                invoice: Invoice
                id: int
            }
            type Flag { enabled: bool }
            fn forwardValue(): Later { Later { number = 42 } }
            type Later { number: int }
            fn forward(): int { forwardValue().number }
            fn invoice(amount: int): Invoice {
                Invoice {
                    marker = '/'
                    title = "héllo"
                    empty = ()
                    paid = true
                    total = amount
                }
            }
            fn total(value: Envelope): int { value.invoice.total }
            fn answer(): int {
                let value = Envelope {
                    invoice = invoice(42)
                    id = 8
                }
                total(value)
            }
            fn newlineAccess(): int {
                let value = invoice(42)
                value
                    .total
            }
            fn originals(): int {
                let original = invoice(12)
                let revised = original with { total = 42 }
                original.total + revised.total
            }
            fn nestedCopy(): int {
                let original = Envelope { invoice = invoice(12)
                                          id = 8 }
                let updated = original with { invoice = original.invoice with { total = 42 } }
                original.invoice.total + updated.invoice.total + updated.id
            }
            fn equal(): bool {
                let first = Envelope { invoice = invoice(42)
                                       id = 8 }
                let second = Envelope { id = 8
                                        invoice = invoice(42) }
                first == second
            }
            fn unequal(): bool {
                let first = invoice(42)
                let second = first with { total = 43 }
                first != second
            }
            fn flag(): int {
                let value = invoice(42)
                if value.paid { value.total } else { 0 }
            }
            fn choose(value: bool): int { if value { 42 } else { 0 } }
            fn chooseTrue(): int { choose(true) }
            fn constructedCondition(): int {
                if Flag { enabled = true }.enabled { 42 } else { 0 }
            }
            """;
        await WithGenerated(source, type =>
        {
            Assert.Equal(42, Invoke(type, "answer"));
            Assert.Equal(42, Invoke(type, "forward"));
            Assert.Equal(42, Invoke(type, "newlineAccess"));
            Assert.Equal(54, Invoke(type, "originals"));
            Assert.Equal(62, Invoke(type, "nestedCopy"));
            Assert.Equal(true, Invoke(type, "equal"));
            Assert.Equal(true, Invoke(type, "unequal"));
            Assert.Equal(42, Invoke(type, "flag"));
            Assert.Equal(42, Invoke(type, "chooseTrue"));
            Assert.Equal(42, Invoke(type, "constructedCondition"));
        });
    }

    [Fact]
    public async Task ConstructionAndUpdatesEvaluateInWrittenOrder()
    {
        const string source = """
            module Demo
            type Pair { first: int
                        second: int }
            fn constructionOrder(): int {
                let value = Pair { second = 1 / 0
                                   first = 2147483647 + 1 }
                value.first
            }
            fn updateOrder(): int {
                let original = Pair { first = 1
                                      second = 2 }
                let value = original with { second = 1 / 0
                                            first = 2147483647 + 1 }
                value.first
            }
            fn badReceiver(): Pair {
                let bad = 2147483647 + 1
                Pair { first = bad
                       second = 2 }
            }
            fn receiverOrder(): int {
                let value = badReceiver() with { first = 1 / 0 }
                value.first
            }
            """;
        await WithGenerated(source, type =>
        {
            Assert.IsType<DivideByZeroException>(Assert.Throws<TargetInvocationException>(() => Invoke(type, "constructionOrder")).InnerException);
            Assert.IsType<DivideByZeroException>(Assert.Throws<TargetInvocationException>(() => Invoke(type, "updateOrder")).InnerException);
            Assert.IsType<OverflowException>(Assert.Throws<TargetInvocationException>(() => Invoke(type, "receiverOrder")).InnerException);
        });
    }

    [Fact]
    public async Task EarlyReturnsInsideRecordExpressionsSkipLaterFieldsAndUpdates()
    {
        const string source = """
            module Demo
            type Pair { first: int
                        second: int }
            type never { value: int }
            fn makeNever(): never { never { value = 42 } }
            fn readNever(): int { makeNever().value }
            fn constructionReturn(): int {
                let item = Pair { first = if true { return 42 } else { 1 }
                                  second = 1 / 0 }
                item.first
            }
            fn updateReturn(): int {
                let original = Pair { first = 1
                                      second = 2 }
                let item = original with { first = if true { return 42 } else { 1 }
                                           second = 1 / 0 }
                item.first
            }
            fn receiverReturn(): int {
                let original = Pair { first = 1
                                      second = 2 }
                let item = (if true { return 42 } else { original }) with { first = 1 / 0 }
                item.first
            }
            fn neverField(): int {
                let item = Pair { first = if true { return 42 } else { return 7 }
                                  second = 1 / 0 }
                item.first
            }
            fn directAccessAfterReturn(): int {
                Pair { first = if true { return 42 } else { return 7 }
                       second = 1 / 0 }.first
            }
            fn updateAfterReturn(): int {
                let item = Pair { first = if true { return 42 } else { return 7 }
                                  second = 1 / 0 } with { first = 1 / 0 }
                item.first
            }
            """;
        await WithGenerated(source, type =>
        {
            Assert.Equal(42, Invoke(type, "readNever"));
            Assert.Equal(42, Invoke(type, "constructionReturn"));
            Assert.Equal(42, Invoke(type, "updateReturn"));
            Assert.Equal(42, Invoke(type, "receiverReturn"));
            Assert.Equal(42, Invoke(type, "neverField"));
            Assert.Equal(42, Invoke(type, "directAccessAfterReturn"));
            Assert.Equal(42, Invoke(type, "updateAfterReturn"));
        });
    }

    [Fact]
    public async Task SourceNamesDoNotCollideWithGeneratedOrCSharpNames()
    {
        const string source = """
            module Demo
            type Demo { Equals: int
                        ToString: int
                        GetHashCode: int
                        class: int }
            fn answer(): int {
                let item = Demo { Equals = 10
                                  ToString = 12
                                  GetHashCode = 0
                                  class = 20 }
                item.Equals + item.ToString + item.GetHashCode + item.class
            }
            """;
        await WithGenerated(source, type => Assert.Equal(42, Invoke(type, "answer")));
    }

    [Theory]
    [InlineData("type Item { value: int }\nfn f(): Item { |Item { } }", "Item")]
    [InlineData("type Item { value: int }\nfn f(): Item { Item { |other = 1 } }", "other")]
    [InlineData("type Item { value: int }\nfn f(): Item { Item { value = 1\n |value = 2 } }", "value")]
    [InlineData("type Item { value: int }\nfn f(): Item { Item { value = |true } }", "true")]
    [InlineData("type Item { value: int }\nfn f(): int { let item = Item { value = 1 }\n item.|other }", "other")]
    [InlineData("type Item { value: int }\nfn f(): Item { let item = Item { value = 1 }\n item with { |other = 2 } }", "other")]
    [InlineData("type Item { value: int }\nfn f(): Item { let item = Item { value = 1 }\n item with { value = 2\n |value = 3 } }", "value")]
    [InlineData("type Item { value: int }\nfn f(): Item { let item = Item { value = 1 }\n item with { value = |true } }", "true")]
    [InlineData("type Item { value: int\n |value: int }\nfn f(): int { 0 }", "value")]
    [InlineData("type Item { value: int }\ntype |Item { value: int }\nfn f(): int { 0 }", "Item")]
    [InlineData("type Item { value: int }\nfn |Item(): int { 0 }", "Item")]
    [InlineData("type Item { value: |Missing }\nfn f(): int { 0 }", "Missing")]
    public void InvalidFieldsAndDeclarationsProduceSourceDiagnostics(string marked, string token)
    {
        var start = marked.IndexOf('|');
        var source = "module Demo\n" + marked.Remove(start, 1);
        start += "module Demo\n".Length;
        var result = DSharpCompiler.Compile(source, "records.ds");
        Assert.Null(result.CSharp);
        var error = Assert.Single(result.Diagnostics);
        Assert.StartsWith("DS", error.Code);
        Assert.Equal("records.ds", error.Path);
        var prefix = source[..start];
        Assert.Equal(new SourceSpan(start, token.Length, prefix.Count(c => c == '\n') + 1,
            start - prefix.LastIndexOf('\n')), error.Span);
    }

    [Fact]
    public void FieldReassignmentIsRejected()
    {
        const string source = "module Demo\ntype Item { value: int }\nfn f(): int {\n let item = Item { value = 1 }\n item.value = 2\n item.value\n}";
        var result = DSharpCompiler.Compile(source, "records.ds");
        Assert.Null(result.CSharp);
        var error = Assert.Single(result.Diagnostics);
        Assert.Equal("DS2005", error.Code);
        Assert.Equal("records.ds", error.Path);
        Assert.True(error.Span.Start >= source.IndexOf("item.value =", StringComparison.Ordinal));
        Assert.True(error.Span.Start < source.IndexOf(" = 2", StringComparison.Ordinal));
    }

    private static object? Invoke(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.Invoke(null, null);

    private static async Task WithGenerated(string source, Action<Type> verify)
    {
        var compilation = DSharpCompiler.Compile(source, "records.ds");
        Assert.Empty(compilation.Diagnostics);
        Assert.Equal(compilation.CSharp, DSharpCompiler.Compile(source).CSharp);
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-records-" + Guid.NewGuid());
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
            var context = new AssemblyLoadContext("records-" + Guid.NewGuid(), isCollectible: true);
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
