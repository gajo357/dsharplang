using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class TupleTests
{
    [Fact(DisplayName = "Tuples and destructuring")]
    public async Task TuplesAreTypedImmutableValuesAndCallsKeepTheirArity()
    {
        const string source = """
            module Demo
            type Box { pair: (int, string) }
            type Item { number: int }
            type Envelope<T> { pair: (T, int) }

            fn sumPair(pair: (int, int)): int {
                let (left, right) = pair
                left + right
            }
            fn sumTwo(left: int, right: int): int { left + right }
            fn typedPair(): (int, string) { (42, "answer") }
            fn nestedPair(): ((int, string), bool) { (typedPair(), true) }
            fn genericPair<T>(value: T): (T, T) { (value, value) }
            fn fromTuple<T>(pair: (T, string?), value: T): T { value }
            fn fromNullableTuple<T>(pair: (T?, int?), value: T): T { value }
            fn fromNullableWrapper<T>(pair: (T?, int?)?, value: T): T { value }
            fn first<T>(pair: (T, T)): T { let (a, b) = pair
                a }
            fn nestedFirst<T>(value: ((T, T), T)): T { let ((a, b), c) = value
                c }
            fn optionFirst<T>(value: (Option<T>, T)): T { let (a, b) = value
                b }
            fn optionLeft<T>(value: (Option<T>, T)): Option<T> { let (a, b) = value
                a }
            fn empty<T>(): Option<T> { None }
            fn consume<T>(pair: (Option<T>, T)): int { 42 }
            fn relay<T>(pair: (Option<T>, T)): T { let (a, b) = pair
                b }
            fn definitelyReturn(flag: bool) {
                relay((if true { return 42 } else { empty() }, if flag { return 42 } else { 1 }))
            }
            fn deferredReturn(flag: bool) {
                consume((if flag { return 7 } else { empty() }, 1))
            }
            fn maybe<T>(value: T?): T? { value }
            fn inferredForward() { inferredPair() }
            fn inferredPair() { (inferredElement(), 22) }
            fn inferredElement() { 20 }
            fn earlyInfer() {
                let dead = (if true { return 42 } else { return 41 }, laterInfer())
                0
            }
            fn laterInfer() { 2 }
            fn tupleSource(): (int, int) { (20, 22) }
            fn firstElement(): int { 20 }
            fn secondElement(): int { 22 }
            fn applyPair(callback: ((int, int)) -> int): int { callback((20, 22)) }
            fn applyTwo(callback: (int, int) -> int): int { callback(20, 22) }
            fn optionPair(): Option<(int, int)> { Some((20, 22)) }
            fn resultPair(): Result<(int, int), string> { Success((20, 22)) }
            fn optionCode(value: Option<(int, int)>): int {
                match value {
                    Some(pair) => sumPair(pair)
                    None => 0
                }
            }
            fn resultCode(value: Result<(int, int), string>): int {
                match value {
                    Success(pair) => sumPair(pair)
                    Error(message) => 0
                }
            }
            fn nullablePairCode(value: (int, int)?): int {
                match value {
                    null => 0
                    present => sumPair(present)
                }
            }
            fn capturePair(pair: (int, int)): () -> int {
                () => { let (left, right) = pair
                        left + right }
            }
            fn tupleBeforeReturn(): int {
                let pair = (if true { return 42 } else { 1 }, 1 / 0)
                let (left, right) = pair
                left + right
            }
            fn run(): bool {
                let (sourceLeft, sourceRight) = tupleSource()
                let (orderedLeft, orderedRight) = (firstElement(), secondElement())
                let pair = (20, 22)
                let (left, right) = pair
                let sameTuple: (int, int) = pair
                let (againLeft, againRight) = sameTuple
                let (nested, marker) = nestedPair()
                let (answer, label) = nested
                let (g1, g2) = genericPair<int>(21)
                let (inferredLeft, inferredRight) = inferredForward()
                let ((nestedLeft, nestedRight), nestedTail) = ((19, 20), 3)
                let (w1, w2, w3, w4, w5, w6, w7, w8) = (1, 2, 3, 4, 5, 6, 7, 14)
                let (unit, unitNumber) = ((), 42)
                let nullable: (int?, string?) = (null, null)
                let (missingNumber, missingText) = nullable
                let nullableInt: int? = null
                let missingPair: (int, int)? = null
                let presentPair: (int, int)? = (20, 22)
                let (fallbackLeft, fallbackRight) = missingPair ?? (20, 22)
                let (presentLeft, presentRight) = presentPair ?? (0, 0)
                let missingNullableElements: (int?, string?)? = null
                let (fallbackNumber, fallbackText) = missingNullableElements ?? (null, null)
                let genericMissing = maybe<(int, int)>(null)
                let genericPresent = maybe<(int, int)>((20, 22))
                let (genericLeft, genericRight) = genericPresent ?? (0, 0)
                let contextual: (int?, Option<string?>) = (null, Some(null))
                let (contextNumber, contextOption) = contextual
                let contextSomeNull = match contextOption {
                    None => false
                    Some(null) => true
                    Some(value) => false
                }
                let someNull = optionLeft((Some(null), 1))
                let inferredNone = optionLeft((None, 42))
                let inferredEmpty = optionLeft((empty(), 42))
                let contextualEmpty: (Option<int>, int) = (empty(), 42)
                let (contextEmpty, contextInt) = contextualEmpty
                let preservedSomeNull = match someNull {
                    None => false
                    Some(null) => true
                    Some(value) => false
                }
                let preservedNone = match inferredNone {
                    None => true
                    Some(value) => false
                }
                let preservedEmpty = match inferredEmpty {
                    None => true
                    Some(value) => false
                }
                let contextualIsNone = match contextEmpty {
                    None => true
                    Some(value) => false
                }
                let nestedOption: Option<Option<(int, int)>> = Some(Some((20, 22)))
                let sameNestedOption: Option<Option<(int, int)>> = Some(Some((20, 22)))
                let missingNestedOption: Option<Option<(int, int)>> = Some(None)
                let boxed = Box { pair = typedPair() }
                let equalBox = Box { pair = (42, "answer") }
                let differentBox = Box { pair = (41, "answer") }
                let (boxNumber, boxText) = boxed.pair
                let captured = capturePair((20, 22))
                let objectPair = (Item { number = 42 }, 1)
                let equalObjectPair = (Item { number = 42 }, 1)
                let differentObjectPair = (Item { number = 41 }, 1)
                let envelope = Envelope { pair = objectPair }
                let equalEnvelope = Envelope { pair = equalObjectPair }
                let differentEnvelope = Envelope { pair = differentObjectPair }
                let single: ((int, int)) -> int = inputPair => {
                    let (a, b) = inputPair
                    a + b
                }
                let multi: (int, int) -> int = (a, b) => a + b
                let passed: (int, int) = (20, 22)
                let viaPipe = passed |> sumPair
                (sourceLeft + sourceRight == 42) && (orderedLeft + orderedRight == 42) &&
                (left + right == 42) && (againLeft + againRight == 42) &&
                marker && (answer == 42) && (label == "answer") &&
                (nestedLeft + nestedRight + nestedTail == 42) &&
                (w1 + w2 + w3 + w4 + w5 + w6 + w7 + w8 == 42) &&
                (g1 + g2 == 42) && (unitNumber == 42) &&
                (inferredLeft + inferredRight == 42) && (earlyInfer() == 42) &&
                (fromTuple((1, null), 42) == 42) &&
                (fromTuple(((1, null)), 42) == 42) &&
                (fromNullableTuple((null, null), 42) == 42) &&
                (fromNullableWrapper((null, null), 42) == 42) &&
                (first((1, null)) == 1) && (first((null, 1)) == null) &&
                (first((1, nullableInt)) == 1) && (first((nullableInt, 1)) == null) &&
                (first<int?>((null, null)) == null) &&
                (nestedFirst(((null, null), 1)) == 1) &&
                (nestedFirst(((1, null), 1)) == 1) &&
                (optionFirst((Some(null), 1)) == 1) &&
                (optionFirst((Some(1), null)) == null) && preservedSomeNull &&
                (optionFirst((None, 42)) == 42) && preservedNone &&
                (optionFirst((empty(), 42)) == 42) && preservedEmpty &&
                contextualIsNone && (contextInt == 42) &&
                (deferredReturn(true) == 7) && (deferredReturn(false) == 42) &&
                (definitelyReturn(true) == 42) && (definitelyReturn(false) == 42) &&
                (missingNumber == null) && (missingText == null) &&
                (genericMissing == null) && (genericLeft + genericRight == 42) &&
                (contextNumber == null) && contextSomeNull &&
                (fallbackNumber == null) && (fallbackText == null) &&
                (nestedOption == sameNestedOption) && (nestedOption != missingNestedOption) &&
                (fallbackLeft + fallbackRight == 42) && (presentLeft + presentRight == 42) &&
                (nullablePairCode(missingPair) == 0) && (nullablePairCode(presentPair) == 42) &&
                (pair == (20, 22)) && (pair != (20, 23)) &&
                (objectPair == equalObjectPair) && (objectPair != differentObjectPair) &&
                (envelope == equalEnvelope) && (envelope != differentEnvelope) &&
                (boxNumber == 42) && (boxText == "answer") &&
                (boxed == equalBox) && (boxed != differentBox) &&
                (optionCode(optionPair()) == 42) && (resultCode(resultPair()) == 42) &&
                (captured() == 42) &&
                (sumPair((20, 22)) == 42) && (sumTwo(20, 22) == 42) &&
                (single((20, 22)) == 42) && (multi(20, 22) == 42) &&
                (applyPair(single) == 42) && (applyTwo(multi) == 42) &&
                (viaPipe == 42) && (tupleBeforeReturn() == 42)
            }
            """;

        await WithGenerated(source, type =>
        {
            Assert.Equal(true, NullableAcceptanceRuntime.Invoke(type, "run"));
            Assert.Equal(1, NullableAcceptanceRuntime.Invoke(type, "readTupleCalls"));
            Assert.Equal(1, NullableAcceptanceRuntime.Invoke(type, "readFirstOrder"));
            Assert.Equal(2, NullableAcceptanceRuntime.Invoke(type, "readSecondOrder"));
        });

        var invalidCases = new[]
        {
            "fn f(): int { let (a, b) = 42\n a + b }",
            "fn f(): int { let (a, b) = (1, 2, 3)\n a + b }",
            "fn f(): int { let (a, b, c) = (1, 2)\n a + b + c }",
            "fn f(): int { let (a, a) = (1, 2)\n a }",
            "fn f(): int { let (a, b) = ()\n a + b }",
            "fn f(): int { let pair: (int, string) = (1, true)\n 42 }",
            "fn f(): int { let pair: (int, int) = (1, 2, 3)\n 42 }",
            "fn f(): int { let pair = (1, 2)\n pair = (3, 4)\n 42 }",
            "fn f(): int { let (a, b) = (1, 2)\n a = 3\n b }",
            "fn sum(value: (int, int)): int { let (a, b) = value\n a + b }\nfn f(): int { sum(20, 22) }",
            "fn sum(a: int, b: int): int { a + b }\nfn f(): int { sum((20, 22)) }",
            "fn f(): int { let one: ((int, int)) -> int = value => 42\n one(20, 22) }",
            "fn f(): int { let two: (int, int) -> int = (a, b) => a + b\n two((20, 22)) }",
            "fn f(): int { let one: ((int, int)) -> int = (a, b) => a + b\n 42 }",
            "fn f(): int { let two: (int, int) -> int = value => 42\n 42 }",
            "fn f(): int { let pair = (1, 2)\n pair.Item1 = 3\n 42 }",
            "fn f(): int { let callback: (int) -> int = x => x\n let pair = (callback, 42)\n 42 }",
            "fn sink<T>(value: T): Unit { let pair = (value, 42)\n () }\nfn f(): int { sink((x: int) => x)\n 42 }",
            "fn f(): int { let ((a, b), c) = ((1, 2, 3), 4)\n a + b + c }",
            "fn recursive() { (recursive(), 42) }",
            "fn bad() { let dead = (if true { return 42 } else { return 41 }, missing())\n 0 }",
            "fn first<T>(pair: (T, T)): T { let (a, b) = pair\n a }\nfn f(): int { let missing = first((null, null))\n 42 }",
            "fn optionFirst<T>(p: (Option<T>, T)): T { let (a, b) = p\n b }\nfn f(): int { optionFirst<int>((Some(null), 1)) }"
        };
        for (var index = 0; index < invalidCases.Length; index++)
        {
            var input = "module Demo\n" + invalidCases[index];
            var result = DSharpCompiler.Compile(input, "tuples.ds");
            Assert.True(result.CSharp is null, $"Tuple scenario {index} unexpectedly emitted C#.");
            var error = Assert.Single(result.Diagnostics);
            Assert.StartsWith("DS", error.Code);
            Assert.Equal("tuples.ds", error.Path);
            Assert.InRange(error.Span.Start, 0, input.Length);
            Assert.InRange(error.Span.Length, 1, input.Length - error.Span.Start);
        }

        const string unresolvedGeneric = """
            module Demo
            fn empty<T>(): Option<T> { None }
            fn consume<T>(pair: (Option<T>, int)): int { 42 }
            fn f(): int { consume((empty(), 1)) }
            """;
        var unresolved = DSharpCompiler.Compile(unresolvedGeneric, "tuples.ds");
        Assert.Null(unresolved.CSharp);
        var unresolvedError = Assert.Single(unresolved.Diagnostics);
        Assert.Equal("DS2023", unresolvedError.Code);
        Assert.DoesNotContain("null", unresolvedError.Message, StringComparison.OrdinalIgnoreCase);

        const string incompatibleReturn = """
            module Demo
            fn empty<T>(): Option<T> { None }
            fn consume<T>(pair: (Option<T>, T)): int { 42 }
            fn go(flag: bool) { consume((if flag { return "bad" } else { empty() }, 1)) }
            """;
        var incompatible = DSharpCompiler.Compile(incompatibleReturn, "tuples.ds");
        Assert.Null(incompatible.CSharp);
        Assert.Equal("DS2027", Assert.Single(incompatible.Diagnostics).Code);
    }

    private static async Task WithGenerated(string source, Action<Type> verify)
    {
        var compilation = DSharpCompiler.Compile(source, "tuples.ds");
        Assert.Empty(compilation.Diagnostics);
        Assert.Equal(compilation.CSharp, DSharpCompiler.Compile(source).CSharp);
        var csharp = compilation.CSharp!;
        var declaration = Assert.Single(Regex.Matches(csharp, @"    public static ([^\r\n]+) @tupleSource\(\)").Cast<Match>());
        csharp = csharp.Replace(declaration.Value,
            "    private static int __tupleCalls;\n" +
            "    public static int @readTupleCalls() => __tupleCalls;\n" +
            "    public static " + declaration.Groups[1].Value + " @tupleSource() { __tupleCalls++; return @tupleSourceOriginal(); }\n" +
            "    public static " + declaration.Groups[1].Value + " @tupleSourceOriginal()", StringComparison.Ordinal);
        var first = Assert.Single(Regex.Matches(csharp, @"    public static ([^\r\n]+) @firstElement\(\)").Cast<Match>());
        csharp = csharp.Replace(first.Value,
            "    private static int __tupleClock, __firstOrder, __secondOrder;\n" +
            "    public static int @readFirstOrder() => __firstOrder;\n" +
            "    public static int @readSecondOrder() => __secondOrder;\n" +
            "    public static " + first.Groups[1].Value + " @firstElement() { __firstOrder = ++__tupleClock; return @firstElementOriginal(); }\n" +
            "    public static " + first.Groups[1].Value + " @firstElementOriginal()", StringComparison.Ordinal);
        var second = Assert.Single(Regex.Matches(csharp, @"    public static ([^\r\n]+) @secondElement\(\)").Cast<Match>());
        csharp = csharp.Replace(second.Value,
            "    public static " + second.Groups[1].Value + " @secondElement() { __secondOrder = ++__tupleClock; return @secondElementOriginal(); }\n" +
            "    public static " + second.Groups[1].Value + " @secondElementOriginal()", StringComparison.Ordinal);
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-tuple-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "Generated.cs"), csharp);
            File.WriteAllText(Path.Combine(directory, "Generated.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>");
            File.WriteAllText(Path.Combine(directory, "global.json"), "{\"sdk\":{\"version\":\"10.0.100\",\"rollForward\":\"disable\"}}");
            File.WriteAllText(Path.Combine(directory, "NuGet.Config"), "<configuration><packageSources><clear/></packageSources></configuration>");
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "build", "Generated.csproj", "--nologo", "--verbosity", "quiet" }) start.ArgumentList.Add(arg);
            using var build = Process.Start(start)!;
            var stdout = build.StandardOutput.ReadToEndAsync();
            var stderr = build.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try { await build.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { build.Kill(true); throw new TimeoutException("Generated tuple build timed out."); }
            Assert.True(build.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("tuple-" + Guid.NewGuid(), isCollectible: true);
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
