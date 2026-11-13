using System.Reflection;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class ClosureCaptureAcceptanceTests
{
    [Fact(DisplayName = "Closures/immutable captures and safety")]
    public async Task ReturnedClosuresPreserveValuesAndCannotExposeMutableCaptureState()
    {
        const string source = """
            module Demo
            type Item { number: int }
            type Choice { | Number(int)
                          | Empty }
            type Fault { | Bad }

            fn multiply(factor: int): (int) -> int { value => factor * value }
            fn local(seed: int): () -> int {
                let stored = seed + 1
                () => stored
            }
            fn makeNested(seed: int): (int) -> () -> int {
                value => () => seed + value
            }
            fn captureArgument(): (int) -> () -> int { value => () => value }
            fn captureItem(item: Item): () -> int { () => item.number }
            fn captureChoice(value: Choice): () -> int {
                match value {
                    Choice.Number(number) => () => number
                    Choice.Empty => () => 0
                }
            }
            fn captureNullable(value: int?): () -> int? { () => value }
            fn captureGeneric<T>(value: T): () -> T { () => value }
            fn carry<T>(value: T): () -> T { captureGeneric<T>(value) }
            fn maybe<T>(value: T?): () -> T? { () => value }
            fn source(): int { 1 }
            fn lexicalShadow(): int {
                let source = 42
                let callback: () -> int = () => source
                callback()
            }
            fn optionShadow(): int {
                let None: Option<int> = Some(42)
                let callback: () -> Option<int> = () => None
                match callback() {
                    Some(number) => number
                    Option.None => 0
                }
            }
            fn fromNullable(value: int?): () -> int {
                match value {
                    null => () => 0
                    present => () => present
                }
            }
            fn deferred(result: Result<int, Fault>): () -> Result<int, Fault> {
                () => {
                    let number = result?
                    Success(number + 1)
                }
            }
            fn lambdaReturn(seed: int): () -> int {
                () => { if seed == 41 { return seed + 1 } else { 0 } }
            }
            fn boundary(): Result<int, Fault> {
                let failed = deferred(Error(Fault.Bad))
                let success = deferred(Success(41))
                let outcome = failed()
                match outcome {
                    Error(Fault.Bad) => success()
                    Success(value) => Success(value)
                }
            }
            fn boundaryCode(): int {
                match boundary() {
                    Success(number) => number
                    Error(Fault.Bad) => 0
                }
            }
            fn unusedFault(): () -> int {
                let seed = 41
                () => if false { 1 / 0 } else { seed + 1 }
            }
            fn run(): bool {
                let triple = multiply(3)
                let double = multiply(2)
                let localFirst = local(40)
                let localSecond = local(41)
                let nested = makeNested(10)
                let innerFirst = nested(20)
                let innerSecond = nested(31)
                let otherNested = makeNested(1)
                let otherInner = otherNested(2)
                let capturedArgument = captureArgument()(42)
                let original = Item { number = 41 }
                let snapshot = captureItem(original)
                let changed = original with { number = 99 }
                let chosen = captureChoice(Choice.Number(42))
                let noChoice = captureChoice(Choice.Empty)
                let present = captureNullable(42)
                let absent = captureNullable(null)
                let generic = carry<int>(42)
                let nullableGeneric = carry<int?>(null)
                let genericItem = carry<Item>(original)
                let genericOption = carry<Option<int?>>(Some(null))
                let nullableText = carry<string?>(null)
                let openIntPresent = maybe<int>(42)
                let openIntMissing = maybe<int>(null)
                let nullableIntPresent = maybe<int?>(42)
                let nullableIntMissing = maybe<int?>(null)
                let openTextPresent = maybe<string>("Alice")
                let openTextMissing = maybe<string>(null)
                let nullableTextPresent = maybe<string?>("Alice")
                let nullableTextMissing = maybe<string?>(null)
                let nullOptionKept = match genericOption() {
                    None => false
                    Some(null) => true
                    Some(number) => false
                }
                let presentMatch = fromNullable(42)
                let nullMatch = fromNullable(null)
                let returned = lambdaReturn(41)
                let lazy = unusedFault()
                (triple(14) == 42) && (double(21) == 42) && (triple(2) == 6) &&
                (localFirst() == 41) && (localSecond() == 42) &&
                (innerFirst() == 30) && (innerSecond() == 41) && (otherInner() == 3) &&
                (capturedArgument() == 42) && (snapshot() == 41) && (changed.number == 99) &&
                (chosen() == 42) && (noChoice() == 0) &&
                (present() == 42) && (absent() == null) &&
                (generic() == 42) && (nullableGeneric() == null) && (genericItem().number == 41) &&
                nullOptionKept && (nullableText() == null) &&
                (openIntPresent() == 42) && (openIntMissing() == null) &&
                (nullableIntPresent() == 42) && (nullableIntMissing() == null) &&
                (openTextPresent() == "Alice") && (openTextMissing() == null) &&
                (nullableTextPresent() == "Alice") && (nullableTextMissing() == null) &&
                (presentMatch() == 42) && (nullMatch() == 0) &&
                (lexicalShadow() == 42) && (optionShadow() == 42) &&
                (returned() == 42) && (boundaryCode() == 42) && (lazy() == 42)
            }
            """;

        await NullableAcceptanceRuntime.WithGenerated(source, type =>
        {
            Assert.Equal(true, NullableAcceptanceRuntime.Invoke(type, "run"));

            var maker = type.GetMethod("multiply", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
            var first = Assert.IsAssignableFrom<Delegate>(maker.Invoke(null, new object[] { 3 }));
            var second = Assert.IsAssignableFrom<Delegate>(maker.Invoke(null, new object[] { 2 }));
            Assert.Equal(42, first.DynamicInvoke(14));
            Assert.Equal(42, second.DynamicInvoke(21));
            Assert.Equal(6, first.DynamicInvoke(2));
            Assert.NotSame(first.Target, second.Target);

            foreach (var closure in new[] { first, second })
            {
                Assert.NotNull(closure.Target);
                var targetType = closure.Target!.GetType();
                var fields = targetType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotEmpty(fields);
                Assert.All(fields, field =>
                {
                    Assert.False(field.IsPublic, $"Capture field {field.Name} is public.");
                    Assert.True(field.IsInitOnly, $"Capture field {field.Name} is mutable.");
                });
                Assert.DoesNotContain(targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance),
                    property => property.SetMethod is { IsPublic: true });
            }
        });

        var invalidCases = new[]
        {
            "fn f(): int { let callback: (int) -> int = value => value\n let nested: () -> int = () => callback(42)\n nested() }",
            "fn hold<T>(value: T): () -> T { () => value }\nfn f(): int { let callback: (int) -> int = value => value\n let nested = hold<(int) -> int>(callback)\n 42 }",
            "fn hold<T>(value: T): () -> T { () => value }\nfn forward<T>(value: T): () -> T { hold<T>(value) }\nfn f(): int { let callback: (int) -> int = value => value\n let nested = forward<(int) -> int>(callback)\n 42 }",
            "fn maybe<T>(value: T?): () -> T? { () => value }\nfn forward<T>(value: T?): () -> T? { maybe<T>(value) }\nfn f(): int { let callback: (int) -> int = value => value\n let nested = forward<(int) -> int>(callback)\n 42 }",
            "type Box<T> { value: T }\nfn f(): int { let callback: (int) -> int = value => value\n let bad = Box { value = callback }\n 42 }",
            "type Choice { | Callback((int) -> int) }\nfn f(): int { 42 }",
            "fn f(): int { let outer = 42\n let callback: (int) -> int = outer => outer\n callback(1) }"
        };
        for (var index = 0; index < invalidCases.Length; index++)
        {
            var result = DSharpCompiler.Compile("module Demo\n" + invalidCases[index], "closure-capture.ds");
            Assert.True(result.CSharp is null, $"Closure safety scenario {index} emitted C#.");
            var error = Assert.Single(result.Diagnostics);
            Assert.StartsWith("DS", error.Code);
            Assert.Equal("closure-capture.ds", error.Path);
        }
    }
}
