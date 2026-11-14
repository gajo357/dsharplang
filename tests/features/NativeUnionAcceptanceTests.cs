using DSharp.Compiler;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace DSharp.Tests;

public sealed class NativeUnionAcceptanceTests
{
    [Fact(DisplayName = "Native unions preserve D# cases")]
    public async Task NativeUnionsPreserveTagsPayloadsEqualityAndDiagnostics()
    {
        // Mirrors examples/native_union_cases.ds. The isolated verifier stages
        // feature test sources, but intentionally does not stage examples.
        const string source = """
            module Demo

            // Cases stay distinct even when their payloads have the same type.
            type Choice<T> { | Left(T) | Right(T) }
            type Item { number: int }
            type Event { | Added(Item, string?) | Skipped }

            // Source names can resemble the compiler's internal helper names.
            type __dsharp_native_case_0 { number: int }
            type __dsharp_native_union { | Wrap(__dsharp_native_case_0) }

            fn keep<T>(value: T): T { value }
            fn firstChoice(): Choice<int> { Choice.Left(42) }

            fn choiceCode(value: Choice<int>): int {
                match value {
                    Left(n) => n
                    Right(n) => n + 1
                }
            }

            fn resultCode(value: Result<int, int>): int {
                match value {
                    Success(n) => n
                    Error(n) => n + 1
                }
            }

            fn optionCode(value: Option<string?>): int {
                match value {
                    None => 1
                    Some(null) => 2
                    Some(name) => if name == "Alice" { 3 } else { 0 }
                }
            }

            fn nestedCode(value: Option<Option<int>>): int {
                match value {
                    None => 1
                    Some(None) => 2
                    Some(Some(number)) => number
                }
            }

            fn answer(): bool {
                let left: Choice<int> = Choice.Left(42)
                let right: Choice<int> = Choice.Right(42)
                let success: Result<int, int> = Success(42)
                let error: Result<int, int> = Error(42)
                let missing: Option<string?> = None
                let cleared: Option<string?> = Some(null)
                let given: Option<string?> = Some("Alice")
                let nullableMissing: Option<string?>? = null
                let nullableNone: Option<string?>? = None
                let nullableCleared: Option<string?>? = Some(null)
                let outerNone: Option<Option<int>> = None
                let innerNone: Option<Option<int>> = Some(None)
                let innerSome: Option<Option<int>> = Some(Some(42))
                let first = Event.Added(Item { number = 42 }, null)
                let same = Event.Added(Item { number = 42 }, null)
                let different = Event.Added(Item { number = 43 }, null)
                let named = __dsharp_native_union.Wrap(__dsharp_native_case_0 { number = 42 })
                let namedAgain = __dsharp_native_union.Wrap(__dsharp_native_case_0 { number = 42 })
                (left != right) && (choiceCode(keep(left)) == 42) && (choiceCode(right) == 43) &&
                (success != error) && (resultCode(success) == 42) && (resultCode(keep(error)) == 43) &&
                (missing != cleared) && (cleared != given) &&
                (nullableMissing == null) && (nullableNone != null) &&
                (nullableCleared != null) && (nullableNone != nullableCleared) &&
                (optionCode(missing) == 1) && (optionCode(cleared) == 2) && (optionCode(given) == 3) &&
                (outerNone != innerNone) && (innerNone != innerSome) &&
                (nestedCode(outerNone) == 1) && (nestedCode(innerNone) == 2) && (nestedCode(innerSome) == 42) &&
                (first == same) && (first != different) && (first != Event.Skipped) &&
                (named == namedAgain)
            }
            """;
        var compilation = DSharpCompiler.Compile(source, "native-union.ds");
        Assert.Empty(compilation.Diagnostics);
        Assert.NotNull(compilation.CSharp);
        Assert.Matches(@"\bprivate\s+union\s+", compilation.CSharp!);
        Assert.DoesNotContain("private readonly object[] __dsharp_payload", compilation.CSharp!);
        await NullableAcceptanceRuntime.WithGenerated(source, type =>
        {
            Assert.Equal(true, NullableAcceptanceRuntime.Invoke(type, "answer"));

            // Foreign reflection can manufacture an otherwise unreachable default
            // storage value. It must fail instead of masquerading as a D# case.
            var valid = NullableAcceptanceRuntime.Invoke(type, "firstChoice")!;
            var wrapperType = valid.GetType();
            var payload = wrapperType.GetField("__dsharp_payload", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(payload);
            Assert.True(payload.IsPrivate);
            Assert.True(payload.FieldType.IsValueType);
            var invalid = RuntimeHelpers.GetUninitializedObject(wrapperType);
            var tag = wrapperType.GetProperty("__dsharp_tag", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(tag);
            var failure = Assert.Throws<TargetInvocationException>(() => tag.GetValue(invalid));
            Assert.IsType<InvalidOperationException>(failure.InnerException);
        });

        foreach (var invalid in new[]
        {
            "type U { | Left(int) | Right(int) }\nfn f(value: U): int { match value { Left(n) => n } }",
            "fn f(): Option<string> { Some(null) }"
        })
        {
            var rejected = DSharpCompiler.Compile("module Demo\n" + invalid, "native-union.ds");
            Assert.Null(rejected.CSharp);
            Assert.NotEmpty(rejected.Diagnostics);
            Assert.All(rejected.Diagnostics, error => Assert.Equal("native-union.ds", error.Path));
        }
    }
}
