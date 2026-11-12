using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class FunctionTypeGenericAcceptanceTests
{
    public async Task GenericSubstitutionPreservesNullableFunctionsAndRejectsDataEscapes()
    {
        const string source = """
            module Demo
            fn lift<T>(value: T): T? { value }
            fn forward<T>(value: T?): T? { value }
            fn isMissing<T>(value: T?): bool { value == null }
            fn run(): bool {
                let callback: (int) -> int = value => value + 1
                let present: ((int) -> int)? = lift<(int) -> int>(callback)
                let absent: ((int) -> int)? = forward<((int) -> int)?>(null)
                let liftedAbsent: ((int) -> int)? = lift<((int) -> int)?>(null)
                let presentResult = match present {
                    null => 0
                    value => value(41)
                }
                let absentResult = match absent {
                    null => true
                    value => false
                }
                let liftedAbsentResult = match liftedAbsent {
                    null => true
                    value => false
                }
                (presentResult == 42) && absentResult && liftedAbsentResult &&
                (present != null) && (absent == null) &&
                isMissing<(int) -> int>(null) && !isMissing<(int) -> int>(callback)
            }
            """;
        await NullableAcceptanceRuntime.WithGenerated(source,
            type => Assert.Equal(true, NullableAcceptanceRuntime.Invoke(type, "run")));

        var invalidCases = new[]
        {
            "type Box<T> { value: T }\nfn make<T>(value: T): Box<T> { Box { value = value } }\nfn f(): int { let result = make<(int) -> int>((value: int) => value)\n 42 }",
            "type Box<T> { value: T }\nfn make<T>(value: T): Box<T> { Box { value = value } }\nfn outer<T>(value: T): Box<T> { make<T>(value) }\nfn f(): int { let result = outer<(int) -> int>((value: int) => value)\n 42 }",
            "fn accepts(callback: (int?) -> int): int { callback(null) }\nfn f(): int { let callback: (int) -> int = value => value\n accepts(callback) }",
            "fn accepts(callback: (int) -> int?): int? { callback(1) }\nfn f(): int? { let callback: (int) -> int = value => value\n accepts(callback) }",
            "fn f(a: (int) -> int, b: (int) -> int): bool { a == b }",
            "fn f(a: ((int) -> int)?, b: ((int) -> int)?): bool { a != b }",
            "fn equal<T>(a: T?, b: T?): bool { a == b }\nfn f(): bool { let callback: (int) -> int = value => value\n equal<(int) -> int>(callback, callback) }",
            "fn equal<T>(a: T?, b: T?): bool { a == b }\nfn forward<T>(a: T?, b: T?): bool { equal<T>(a, b) }\nfn f(): bool { let callback: (int) -> int = value => value\n forward<(int) -> int>(callback, callback) }"
        };
        for (var index = 0; index < invalidCases.Length; index++)
        {
            var result = DSharpCompiler.Compile("module Demo\n" + invalidCases[index], "function-generics.ds");
            Assert.True(result.CSharp is null && result.Diagnostics.Count > 0,
                $"Function type generic scenario {index} was not diagnosed.");
        }
    }
}
