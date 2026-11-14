using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class OptionRecordAcceptance
{
    [Fact(DisplayName = "Option/records and generic calls")]
    public async Task RecordFieldsPreserveOptionCasesThroughGenericCalls()
    {
        // Mirrors examples/option_records.ds. The isolated verifier stages
        // feature sources but deliberately does not stage the examples folder.
        const string source = """
            module Demo

            type NameUpdate { name: Option<string?> }
            type Envelope<T> { payload: T }
            type Box<T> { value: Option<T?> }
            fn keep<T>(value: T): T { value }
            fn box<T>(seed: T, value: T?): Box<T> { Box { value = Some(value) } }
            fn empty<T>(seed: T): Box<T> { Box { value = None } }
            fn unchanged(): NameUpdate { NameUpdate { name = None } }
            fn clear(): NameUpdate { NameUpdate { name = Some(null) } }
            fn rename(): NameUpdate { NameUpdate { name = Some("Alice") } }
            fn apply(update: NameUpdate, current: string?): string? {
                match update.name {
                    None => current
                    Some(null) => null
                    Some(name) => name
                }
            }
            fn answer(): bool {
                let original = "Existing"
                let unchangedValue = keep(unchanged())
                let clearValue = keep(clear())
                let renameValue = keep(rename())
                let outerNone: Envelope<Option<Option<int>>> = Envelope { payload = None }
                let innerNone: Envelope<Option<Option<int>>> = Envelope { payload = Some(None) }
                let innerSome: Envelope<Option<Option<int>>> = Envelope { payload = Some(Some(42)) }
                let emptyInt = keep(empty<int>(0))
                let nullInt = keep(box<int>(0, null))
                let someInt = keep(box<int>(0, 42))
                let emptyText = keep(empty<string>(""))
                let nullText = keep(box<string>("", null))
                let someText = keep(box<string>("", "Alice"))
                (apply(unchangedValue, original) == original) &&
                (apply(clearValue, original) == null) &&
                (apply(renameValue, original) == "Alice") &&
                (unchangedValue != clearValue) && (clearValue != renameValue) &&
                (clearValue == NameUpdate { name = Some(null) }) &&
                (outerNone != innerNone) && (innerNone != innerSome) &&
                (keep(innerNone) == innerNone) &&
                (emptyInt != nullInt) && (nullInt != someInt) &&
                (emptyText != nullText) && (nullText != someText) &&
                (nullInt == box<int>(0, null)) && (someInt == box<int>(0, 42)) &&
                (nullText == box<string>("", null)) && (someText == box<string>("", "Alice"))
            }
            """;
        await NullableAcceptanceRuntime.WithGenerated(source,
            type => Assert.Equal(true, NullableAcceptanceRuntime.Invoke(type, "answer")));
    }

    [Fact(DisplayName = "Option/record field nullability")]
    public void SomeNullCannotPopulateANonNullableRecordField()
    {
        const string source = """
            module Demo
            type NameUpdate { name: Option<string> }
            fn clear(): NameUpdate { NameUpdate { name = Some(null) } }
            """;
        var result = DSharpCompiler.Compile(source, "option-record.ds");
        Assert.Null(result.CSharp);
        Assert.NotEmpty(result.Diagnostics);
        Assert.All(result.Diagnostics, error => Assert.Equal("option-record.ds", error.Path));
    }

    [Fact(DisplayName = "Option/record field exhaustive match")]
    public void RecordFieldMatchMustHandleNoneAndSome()
    {
        const string source = """
            module Demo
            type NameUpdate { name: Option<string?> }
            fn apply(update: NameUpdate): int {
                match update.name { Some(value) => 1 }
            }
            """;
        var result = DSharpCompiler.Compile(source, "option-record.ds");
        Assert.Null(result.CSharp);
        Assert.NotEmpty(result.Diagnostics);
        Assert.All(result.Diagnostics, error => Assert.Equal("option-record.ds", error.Path));
    }

    [Fact(DisplayName = "Option/no implicit value extraction")]
    public void OptionValueCannotBeReadAsAField()
    {
        const string source = """
            module Demo
            fn extract(value: Option<int>): int { value.value }
            """;
        var result = DSharpCompiler.Compile(source, "option-record.ds");
        Assert.Null(result.CSharp);
        Assert.NotEmpty(result.Diagnostics);
        Assert.All(result.Diagnostics, error => Assert.Equal("option-record.ds", error.Path));
    }
}
