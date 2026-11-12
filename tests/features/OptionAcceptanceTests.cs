using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class OptionAcceptance
{
    [Fact(DisplayName = "Option/distinct values and matching")]
    public async Task NoneSomeNullAndNestedOptionsRemainDistinct()
    {
        const string source = """
            module Demo
            type Item { number: int }
            fn missing(): Option<int?> { None }
            fn nullValue(): Option<int?> { Some(null) }
            fn zero(): Option<int?> { Some(0) }
            fn number(): Option<int?> { Some(42) }
            fn classify(value: Option<int?>): int {
                match value {
                    None => 1
                    Some(null) => 2
                    Some(n) => n
                }
            }
            fn nullableText(): Option<string?> { Some(null) }
            fn absentText(): Option<string?> { None }
            fn presentText(): Option<string?> { Some("hello") }
            fn textCode(value: Option<string?>): int {
                match value {
                    Some(text) => if text == "hello" { 42 } else { 0 }
                    Some(null) => 2
                    None => 1
                }
            }
            fn outerNone(): Option<Option<int>> { None }
            fn innerNone(): Option<Option<int>> { Some(None) }
            fn innerSome(): Option<Option<int>> { Some(Some(42)) }
            fn qualifiedNone(): Option<Option<int>> { Option.None }
            fn qualifiedInnerNone(): Option<Option<int>> { Option.Some(Option.None) }
            fn qualifiedSome(): Option<Option<int>> { Option.Some(Option.Some(42)) }
            fn nestedCode(value: Option<Option<int>>): int {
                match value {
                    None => 1
                    Some(None) => 2
                    Some(Some(n)) => n
                }
            }
            fn optionalEmpty(): Option<int>? { None }
            fn optionalMissing(): Option<int>? { null }
            fn itemValue(): Option<Item> { Some(Item { number = 42 }) }
            fn resultValue(): Result<Option<int?>, int> { Success(Some(null)) }
            fn resultCode(value: Result<Option<int?>, int>): int {
                match value {
                    Success(None) => 1
                    Success(Some(null)) => 2
                    Success(Some(n)) => n
                    Error(n) => n
                }
            }
            fn run(): bool {
                (classify(missing()) == 1) && (classify(nullValue()) == 2) &&
                (classify(zero()) == 0) && (classify(number()) == 42) &&
                (missing() != nullValue()) && (nullValue() != zero()) &&
                (nullValue() == nullValue()) && (zero() == zero()) &&
                (textCode(nullableText()) == 2) && (textCode(absentText()) == 1) &&
                (textCode(presentText()) == 42) && (nullableText() != absentText()) &&
                (nestedCode(outerNone()) == 1) && (nestedCode(innerNone()) == 2) &&
                (nestedCode(innerSome()) == 42) && (outerNone() == qualifiedNone()) &&
                (innerNone() == qualifiedInnerNone()) && (innerSome() == qualifiedSome()) &&
                (outerNone() != innerNone()) && (innerNone() != innerSome()) &&
                (optionalEmpty() != null) && (optionalMissing() == null) &&
                (itemValue() == Some(Item { number = 42 })) && (resultCode(resultValue()) == 2)
            }
            """;
        await NullableAcceptanceRuntime.WithGenerated(source, type => Assert.Equal(true, NullableAcceptanceRuntime.Invoke(type, "run")));
    }

    [Fact(DisplayName = "Option/Unit and generic payload")]
    public async Task OptionWorksWithUnitAndGenericNullablePayloads()
    {
        const string source = """
            module Demo
            fn maybe<T>(seed: T, value: T?): Option<T?> { Some(value) }
            fn noValue<T>(seed: T): Option<T?> { None }
            fn unit(): Option<Unit> { Some(()) }
            fn noUnit(): Option<Unit> { None }
            fn intCode(value: Option<int?>): int {
                match value {
                    None => 1
                    Some(null) => 2
                    Some(number) => number
                }
            }
            fn run(): bool {
                let nullInt = maybe<int>(0, null)
                let zeroInt = maybe<int>(0, 0)
                let nestedNull = maybe<int?>(null, null)
                let none = noValue<int>(0)
                (unit() != noUnit()) && (intCode(nullInt) == 2) &&
                (intCode(zeroInt) == 0) && (intCode(nestedNull) == 2) &&
                (intCode(none) == 1) && (none != nullInt) && (nullInt == nestedNull)
            }
            """;
        await NullableAcceptanceRuntime.WithGenerated(source, type => Assert.Equal(true, NullableAcceptanceRuntime.Invoke(type, "run")));
    }

    [Fact(DisplayName = "Option/name lookup")]
    public async Task UserNamesAndLexicalBindingsTakePrecedence()
    {
        const string source = """
            module Demo
            type U { | Some(int) }
            type Holder { Some: int }
            fn standard(): Option<int> { Option.Some(42) }
            fn userCase(): int {
                let value = Some(42)
                match value { U.Some(n) => n }
            }
            fn shadowLocal(): int {
                let Some = 42
                Some
            }
            fn shadowQualifier(): int {
                let Option = Holder { Some = 42 }
                Option.Some
            }
            fn extract(value: Option<int>): int {
                match value {
                    Option.None => 0
                    Option.Some(n) => n
                }
            }
            fn run(): int {
                if (extract(standard()) == 42) && (userCase() == 42) &&
                   (shadowLocal() == 42) && (shadowQualifier() == 42) { 42 } else { 0 }
            }
            """;
        await NullableAcceptanceRuntime.WithGenerated(source, type => Assert.Equal(42, NullableAcceptanceRuntime.Invoke(type, "run")));
    }

    [Fact(DisplayName = "Option/user type name")]
    public async Task UserDefinedOptionTypeIsNotReserved()
    {
        const string source = """
            module Demo
            type Option { | Named(int) }
            fn Some(value: int): int { value }
            fn None(value: int): int { value }
            fn run(): int {
                match Option.Named(Some(40) + None(2)) {
                    Option.Named(value) => value
                }
            }
            """;
        await NullableAcceptanceRuntime.WithGenerated(source, type => Assert.Equal(42, NullableAcceptanceRuntime.Invoke(type, "run")));
    }

    [Theory(DisplayName = "Option/invalid")]
    [InlineData("null and inference", 0, 4)]
    [InlineData("coverage and conversions", 5, 9)]
    public void Invalid(string group, int first, int last)
    {
        for (var id = first; id <= last; id++)
        {
            var failure = Record.Exception(() =>
            {
                string body = id switch
                {
                    0 => "fn f(): Option<int> { Some(null) }",
                    1 => "fn f(): Option<string> { Some(null) }",
                    2 => "fn f(): int { let value = None\n 42 }",
                    3 => "fn f(): int { let value = Some(null)\n 42 }",
                    4 => "fn f(): Option<int> { Some() }",
                    5 => "fn f(value: Option<int>): int { match value { Some(n) => n } }",
                    6 => "fn f(value: Option<int>): int { match value { None => 0 } }",
                    7 => "fn f(): Option<int> { null }",
                    8 => "fn takes(value: int?): int { 42 }\nfn f(): int { let value: Option<int> = None\n takes(value) }",
                    9 => "fn f(value: int?): Option<int> { value }",
                    _ => throw new ArgumentOutOfRangeException(nameof(id))
                };
                var result = DSharpCompiler.Compile("module Demo\n" + body, "option.ds");
                Assert.Null(result.CSharp);
                var error = Assert.Single(result.Diagnostics);
                Assert.StartsWith("DS", error.Code);
                Assert.Equal("option.ds", error.Path);
            });
            Assert.True(failure is null, $"Option {group} scenario {id}: {failure}");
        }
    }

    [Theory(DisplayName = "Option/source spans")]
    [InlineData("payload", 0)]
    [InlineData("inference", 1)]
    public void DiagnosticSpans(string group, int id)
    {
        (string marked, string token) = id switch
        {
            0 => ("fn f(): Option<int> { Some(~null) }", "null"),
            1 => ("fn f(): int { let value = ~None\n 42 }", "None"),
            _ => throw new ArgumentOutOfRangeException(nameof(id))
        };
        var marker = marked.IndexOf('~');
        var source = "module Demo\n" + marked.Remove(marker, 1);
        var result = DSharpCompiler.Compile(source, "option.ds");
        Assert.Null(result.CSharp);
        var error = Assert.Single(result.Diagnostics);
        Assert.Equal("option.ds", error.Path);
        var start = "module Demo\n".Length + marker;
        var prefix = source[..start];
        Assert.True(error.Span == new SourceSpan(start, token.Length, prefix.Count(c => c == '\n') + 1,
            start - prefix.LastIndexOf('\n')), $"Option {group} span: {error.Span}");
    }
}
