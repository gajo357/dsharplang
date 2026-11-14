using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class GenericNullableMatrixTests
{
    [Fact(DisplayName = "Generic nullable matrix")]
    public async Task NullAndPresentValuesSurviveGenericCallsAndStoredData()
    {
        // Each invocation tests a different substitution for T. The generated
        // C# is compiled with warnings as errors and run by the shared harness.
        const string source = """
            module Demo

            type Box<T> { value: T }
            type Packet<T> { | Payload(T?) }
            type Snapshot<T> {
                missing: T?
                present: T?
                items: List<T?>
                named: Map<string, T?>
                optional: Option<T?>
                packet: Packet<T>
            }

            fn forward<T>(value: T?): T? { value }
            fn forwardAgain<T>(value: T?): T? { forward<T>(value) }
            fn choose<T>(seed: T, value: T?): T? { forwardAgain<T>(value) }
            fn message<T>(seed: T, value: T?): Packet<T> { Packet.Payload(value) }
            fn payload<T>(packet: Packet<T>): T? {
                match packet {
                    Packet.Payload(null) => null
                    Packet.Payload(value) => value
                }
            }
            fn optionCode<T>(value: Option<T?>): int {
                match value {
                    None => 0
                    Some(null) => 1
                    Some(present) => 2
                }
            }
            fn snapshot<T>(seed: T, value: T?): Snapshot<T> {
                Snapshot {
                    missing = forward<T>(null)
                    present = choose<T>(seed, value)
                    items = [forward<T>(null), forwardAgain<T>(value)]
                    named = Map.empty<string, T?>().set("missing", null).set("present", value)
                    optional = Some(value)
                    packet = message<T>(seed, value)
                }
            }
            async fn forwardAsync<T>(value: T?): T? { forwardAgain<T>(value) }
            async fn asyncMatrix(): bool {
                let intNull = await forwardAsync<int>(null)
                let intValue = await forwardAsync<int>(0)
                let nullableIntNull = await forwardAsync<int?>(null)
                let nullableIntValue = await forwardAsync<int?>(42)
                let textNull = await forwardAsync<string>(null)
                let textValue = await forwardAsync<string>("Alice")
                let nullableTextNull = await forwardAsync<string?>(null)
                let nullableTextValue = await forwardAsync<string?>("Alice")
                (intNull == null) && (intValue == 0) && (intValue != null) &&
                (nullableIntNull == null) && (nullableIntValue == 42) &&
                (textNull == null) && (textValue == "Alice") &&
                (nullableTextNull == null) && (nullableTextValue == "Alice")
            }
            fn verify<T>(seed: T, value: T?): bool {
                let data: Snapshot<T> = snapshot<T>(seed, value)
                let absent: Option<T?> = None
                let suppliedNull: Option<T?> = Some(null)
                let nestedAbsent: Option<Option<T?>> = Some(None)
                let nestedNull: Option<Option<T?>> = Some(Some(null))
                let outerAbsent: Option<Option<T?>> = None
                (forward<T>(null) == null) && (forwardAgain<T>(value) == value) &&
                (choose<T>(seed, value) == value) && (data.missing == null) &&
                (data.present == value) && (optionCode<T>(data.items.get(0)) == 1) &&
                (optionCode<T>(data.items.get(1)) == 2) &&
                (optionCode<T>(data.items.get(2)) == 0) &&
                (optionCode<T>(data.named.get("missing")) == 1) &&
                (optionCode<T>(data.named.get("present")) == 2) &&
                (optionCode<T>(data.named.get("absent")) == 0) &&
                (optionCode<T>(data.optional) == 2) &&
                (payload<T>(data.packet) == value) &&
                (payload<T>(message<T>(seed, null)) == null) &&
                (absent != suppliedNull) && (outerAbsent != nestedAbsent) &&
                (nestedAbsent != nestedNull)
            }
            fn run(): bool {
                let closedMissing: Box<int?> = Box { value = null }
                let closedPresent: Box<int?> = Box { value = 42 }
                let inferredValue = choose(42, 42)
                let inferredNull = choose(42, null)
                let inferredText = choose("seed", "Alice")
                verify<int>(0, 42) && verify<int>(0, 0) &&
                verify<int?>(null, 42) && verify<int?>(null, 0) &&
                verify<string>("seed", "Alice") && verify<string?>(null, "Alice") &&
                (closedMissing.value == null) && (closedPresent.value == 42) &&
                (inferredValue == 42) && (inferredNull == null) && (inferredText == "Alice")
            }
            """;

        await NullableAcceptanceRuntime.WithGenerated(source, type =>
        {
            Assert.Equal(true, NullableAcceptanceRuntime.Invoke(type, "run"));
            var asyncResult = Assert.IsType<Task<bool>>(NullableAcceptanceRuntime.Invoke(type, "asyncMatrix"));
            Assert.True(asyncResult.GetAwaiter().GetResult());
        });

        // The two representations must not be silently interchanged. A
        // non-nullable payload also cannot receive null by generic substitution.
        var invalid = new[]
        {
            "fn f(): Option<string> { Some(null) }",
            "fn forward<T>(value: T?): T? { value }\nfn f(): int { forward<int>(null) }",
            "type Box<T> { value: T }\nfn f(): Box<int?> { let item: Box<int> = Box { value = 1 }\n item }",
            "fn f(): List<int> { [null] }",
            "fn f(value: Option<int?>): int? { value }"
        };
        foreach (var body in invalid)
        {
            var result = DSharpCompiler.Compile("module Demo\n" + body, "nullable-matrix-invalid.ds");
            Assert.Null(result.CSharp);
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, diagnostic => Assert.Equal("nullable-matrix-invalid.ds", diagnostic.Path));
            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code.StartsWith("DS1", StringComparison.Ordinal));
        }
    }
}
