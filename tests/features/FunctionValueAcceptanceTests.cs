using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class FunctionValueAcceptanceTests
{
    [Fact(DisplayName = "Function values/execution")]
    public async Task CaptureFreeLambdasRunAndKeepTheirOwnReturnBoundary()
    {
        const string source = """
            module Demo
            type Fault { | Bad }
            fn query(ok: bool): Result<int, Fault> {
                if ok { Success(41) } else { Error(Fault.Bad) }
            }
            fn apply(callback: (int) -> int, value: int): int { callback(value) }
            fn combine(callback: (int, int) -> int, a: int, b: int): int { callback(a, b) }
            fn generic<T>(value: T, callback: (T) -> T): T { callback(value) }
            fn mapForward<T, U>(value: T, callback: (T) -> U): U { callback(value) }
            fn mapReverse<T, U>(callback: (T) -> U, value: T): U { callback(value) }
            fn identity<T>(value: T): T { value }
            fn nullable(callback: (int?) -> int?, value: int?): int? { callback(value) }
            fn nullableResult(callback: (int) -> string?): string? { callback(0) }
            fn make(): (int) -> int { value => value + 1 }
            fn makeGeneric<T>(): (T) -> T { value => value }
            fn guarded(flag: bool): int {
                match 42 {
                    value when flag => value
                    _ => 0
                }
            }
            fn run(): bool {
                let zero: () -> int = () => 42
                let add: (int, int) -> int = (left, right) => left + right
                let increment = (value: int) => value + 1
                let explicit: (int, int) -> int = (left: int, right: int) => left + right
                let mixed: (int, int) -> int = (left: int, right) => left + right
                let block: (int) -> int = value => {
                    let doubled = value * 2
                    doubled + 2
                }
                let first: (int) -> int = value => value + 1
                let second: (int) -> int = value => value + 2
                let nullableIdentity: (int?) -> int? = value => value
                let returned = make()
                let returnedGeneric = makeGeneric<int>()
                let passed = identity<(int) -> int>(returned)
                let nested: () -> (int) -> int = () => (value: int) => value + 1
                let nestedFunction = nested()
                let wide: (int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int) -> int =
                    (p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, p15, p16) => p0
                (zero() == 42) && (add(20, 22) == 42) &&
                (increment(41) == 42) && (explicit(20, 22) == 42) &&
                (mixed(20, 22) == 42) && (block(20) == 42) &&
                (first(41) == 42) && (second(40) == 42) &&
                (apply(value => value + 1, 41) == 42) &&
                (combine((a, b) => a + b, 20, 22) == 42) &&
                (generic<int>(42, value => value) == 42) &&
                (mapForward(41, value => value + 1) == 42) &&
                (mapReverse(value => value + 1, 41) == 42) &&
                (nullable(nullableIdentity, null) == null) && (nullable(value => value, 42) == 42) &&
                (returned(41) == 42) && (returnedGeneric(42) == 42) &&
                (passed(41) == 42) && (make()(41) == 42) &&
                (nestedFunction(41) == 42) && (nested()(41) == 42) &&
                (wide(42, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0) == 42) &&
                (guarded(true) == 42) && (guarded(false) == 0)
            }
            fn returnsOnlyFromLambda(): int {
                let callback: (int) -> int = value => {
                    if value == 0 { return 41 } else { value }
                }
                callback(0) + 1
            }
            fn distinguishNullableFunction(): bool {
                let present: ((int) -> string)? = (value: int) => "present"
                let absent: ((int) -> string)? = null
                let presentWorked = match present {
                    null => false
                    callback => callback(42) == "present"
                }
                let absentWorked = match absent {
                    null => true
                    callback => false
                }
                let resultWasNull = match nullableResult(value => null) {
                    null => true
                    value => false
                }
                presentWorked && absentWorked && resultWasNull
            }
            fn propagatesOnlyFromLambda(): int {
                let callback: (bool) -> Result<int, Fault> = ok => {
                    let value = query(ok)?
                    Success(value + 1)
                }
                let failed = callback(false)
                match failed {
                    Error(Fault.Bad) => match callback(true) {
                        Success(value) => value
                        Error(Fault.Bad) => 0
                    }
                    Success(_) => 0
                }
            }
            fn sameTypeReturnBoundary(): Result<int, Fault> {
                let callback: () -> Result<int, Fault> = () => { return Error(Fault.Bad) }
                let seen = callback()
                let code = match seen {
                    Error(Fault.Bad) => 42
                    Success(value) => value
                }
                return Success(code)
            }
            fn sameTypeBoundaryCode(): int {
                match sameTypeReturnBoundary() {
                    Success(value) => value
                    Error(Fault.Bad) => 0
                }
            }
            """;

        await NullableAcceptanceRuntime.WithGenerated(source, type =>
        {
            Assert.Equal(true, NullableAcceptanceRuntime.Invoke(type, "run"));
            Assert.Equal(true, NullableAcceptanceRuntime.Invoke(type, "distinguishNullableFunction"));
            Assert.Equal(42, NullableAcceptanceRuntime.Invoke(type, "returnsOnlyFromLambda"));
            Assert.Equal(42, NullableAcceptanceRuntime.Invoke(type, "propagatesOnlyFromLambda"));
            Assert.Equal(42, NullableAcceptanceRuntime.Invoke(type, "sameTypeBoundaryCode"));
        });
        await new FunctionTypeGenericAcceptanceTests().GenericSubstitutionPreservesNullableFunctionsAndRejectsDataEscapes();
    }

    [Fact(DisplayName = "Function values/invalid")]
    public void InvalidProgramsProduceSourceDiagnostics()
    {
        var cases = new[]
        {
            "fn f(): int { let callback = value => value\n 42 }",
            "fn f(): int { let callback = (left, right) => 42\n 42 }",
            "fn f(): int { let callback: (int) -> int = (left, right) => left\n 42 }",
            "fn f(): int { let callback: (int) -> int = (value: string) => 42\n 42 }",
            "fn f(): int { let callback: (int) -> bool = value => value + 1\n 42 }",
            "fn f(): int { let callback: (int) -> int = value => { return true }\n 42 }",
            "fn f(callback: (int) -> int): int { callback() }",
            "fn f(): int { let callback: () -> int = () => 42\n callback(1) }",
            "fn f(): int { let callback: (int) -> int = value => value\n callback(true) }",
            "fn f(): int { let callback: (int, int) -> int = (value, value) => value\n 42 }",
            "fn f(seed: int): int { let callback: (int) -> int = value => value + seed\n 42 }",
            "fn f(): int { let captured = 42\n let callback: () -> int = () => captured\n 42 }",
            "fn f(): int { let outer: (int) -> () -> int = value => () => value\n 42 }",
            "fn calc(): int { 42 }\nfn f(): int { let calc: () -> int = () => 1\n let callback: () -> int = () => calc()\n callback() }",
            "fn f(): int { let None: Option<int> = Some(42)\n let callback: () -> Option<int> = () => None\n match callback() { Some(n) => n\n None => 0 } }",
            "fn f(): int { let value = 42\n let callback: (int) -> int = value => value\n callback(1) }",
            "fn f(): int { let callback: (int) -> int = value => value\n value }",
            "type Box { callback: (int) -> int }\nfn f(): int { 42 }",
            "type Choice { | Callback((int) -> int) }\nfn f(): int { 42 }",
            "type Box<T> { value: T }\nfn f(box: Box<(int) -> int>): int { 42 }",
            "type Choice<T> { | Wrapped(T) }\nfn f(value: Choice<(int) -> int>): int { 42 }",
            "type Box<T> { value: T }\nfn f(): int { let bad = Box { value = (x: int) => x }\n 42 }",
            "type Choice<T> { | Wrapped(T) }\nfn f(): int { let bad = Choice.Wrapped((x: int) => x)\n 42 }",
            "fn f(): int { let bad: Option<(int) -> int> = Some((x: int) => x)\n 42 }",
            "fn load(): Result<int, int> { Success(42) }\nfn f(): Result<int, int> { let callback: () -> int = () => load()?\n Success(callback()) }",
            "fn f(): int { let callback: (int) -> int = value => value\n callback }"
        };

        for (var index = 0; index < cases.Length; index++)
        {
            var source = "module Demo\n" + cases[index];
            var failure = Record.Exception(() =>
            {
                var result = DSharpCompiler.Compile(source, "function-values.ds");
                Assert.Null(result.CSharp);
                var error = Assert.Single(result.Diagnostics);
                Assert.StartsWith("DS", error.Code);
                Assert.Equal("function-values.ds", error.Path);
                Assert.InRange(error.Span.Start, 0, source.Length);
                Assert.InRange(error.Span.Length, 1, source.Length - error.Span.Start);
                Assert.True(error.Span.Line >= 1);
                Assert.True(error.Span.Column >= 1);
            });
            Assert.True(failure is null, $"Function value scenario {index}: {failure}");
        }
        new FunctionDataEscapeTests().GenericCallsCannotHideFunctionsInTemporaryData();
    }
}
