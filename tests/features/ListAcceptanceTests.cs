using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class ListAcceptanceTests
{
    [Fact(DisplayName = "Immutable lists preserve values and order")]
    public async Task ListsPreserveDataAndOrderThroughOperations()
    {
        // Mirrors examples/list.ds; the isolated verifier stages feature tests
        // but deliberately does not stage the examples directory.
        const string source = """
            module Demo
            type Item { id: int }
            type Group<T> { values: List<T> }
            fn keep<T>(values: List<T>): List<T> { values }
            fn single<T>(seed: T, value: T?): List<T?> { [value] }
            fn choices<T>(seed: T, value: T?): List<Option<T?>> { [None, Some(value)] }
            fn addOne(value: int): int { value + 1 }
            fn odd(value: int): bool { value % 2 != 0 }
            fn digits(acc: int, value: int): int { acc * 10 + value }
            fn isMissing(value: Option<int>): bool {
                match value { None => true
                              Some(number) => false }
            }
            fn explode(value: int): int { value / 0 }
            fn explodeInOrder(value: int): int {
                if value == 1 { 1 / 0 } else { 2147483647 + 1 }
            }
            fn predicateInOrder(value: int): bool {
                if value == 1 { 1 / 0 == 0 } else { 2147483647 + 1 == 0 }
            }
            fn first(): int { 1 / 0 }
            fn second(): int { 2147483647 + 1 }
            fn literalOrder(): List<int> { [first(), second()] }
            fn callbackFailure(): List<int> { [1].map(explode) }
            fn mapOrder(): List<int> { [1, 2].map(explodeInOrder) }
            fn filterOrder(): List<int> { [1, 2].filter(predicateInOrder) }
            fn emptyCallback(): List<int> { let values: List<int> = []
                values.map(explode) }
            fn nullableCode(value: Option<int?>): int {
                match value { None => -1
                              Some(null) => 0
                              Some(number) => number + 1 }
            }
            fn nestedCode(value: Option<Option<string?>>): int {
                match value { None => 0
                              Some(None) => 1
                              Some(Some(null)) => 2
                              Some(Some(name)) => if name == "Alice" { 3 } else { 4 } }
            }
            fn textCode(value: Option<string?>): int {
                match value { None => 0
                              Some(null) => 1
                              Some(text) => if text == "Alice" { 2 } else { 3 } }
            }
            fn nestedIntCode(value: Option<Option<int?>>): int {
                match value { None => 0
                              Some(None) => 1
                              Some(Some(null)) => 2
                              Some(Some(number)) => number + 3 }
            }
            fn answer(): bool {
                let empty: List<int> = []
                let numbers = [1, 2, 3]
                let extended = numbers.append(4)
                let changed = numbers.map(addOne)
                let selected = numbers.filter(odd)
                let delta = 1
                let captured = numbers.map(value => value + delta)
                let nullable: List<int?> = [null, 0, 42]
                let optional: List<Option<string?>> = [None, Some(null), Some("Alice")]
                let records: List<Item> = [Item { id = 1 }, Item { id = 2 }]
                let group: Group<int> = Group { values = keep(numbers) }
                let nested: List<List<int>> = [[1, 2], [3]]
                let genericNull = single<int>(0, null)
                let alreadyNullable = single<int?>(null, null)
                let text: List<string?> = [null, "Alice"]
                let nullFirst: List<int?> = [null, 1]
                let nullLast: List<int?> = [1, null]
                let wrapped: List<Option<string?>> = [(Some(null)), Some("Alice")]
                let inferredNullFirst = [null, 1]
                let inferredNullLast = [1, null]
                let inferredNested = [[null, 1], [1, null]]
                let mixedNested = [[1], [2, null]]
                let reverseMixedNested = [[2, null], [1]]
                let expectedMixedNested: List<List<int?>> = [[1], [2, null]]
                let expectedReverseMixedNested: List<List<int?>> = [[2, null], [1]]
                let inferredWrapped = [(Some(null)), Some("Alice")]
                let inferredWrappedReverse = [Some("Alice"), (Some(null))]
                let intChoices = choices<int>(0, null)
                let alreadyNullableChoices = choices<int?>(null, null)
                let numberChoices = choices<int>(0, 42)
                let textChoices = choices<string>("", null)
                let presentTextChoices = choices<string>("", "Alice")
                (empty.length() == 0) && isMissing(empty.get(0)) &&
                (numbers.length() == 3) && (extended.length() == 4) &&
                (numbers == [1, 2, 3]) && (extended == [1, 2, 3, 4]) &&
                (changed == [2, 3, 4]) && (selected == [1, 3]) &&
                (captured == changed) &&
                (numbers.filter(value => value % 2 == 1) == selected) &&
                (numbers.fold(0, (acc, value) => acc * 10 + value) == 123) &&
                (numbers.fold(0, digits) == 123) && (changed.fold(0, digits) == 234) &&
                (selected.fold(0, digits) == 13) && (empty.fold(42, digits) == 42) &&
                (nullableCode(nullable.get(-1)) == -1) && (nullableCode(nullable.get(3)) == -1) &&
                (nullableCode(nullable.get(0)) == 0) && (nullableCode(nullable.get(1)) == 1) &&
                (nullableCode(nullable.get(2)) == 43) &&
                (nullableCode(genericNull.get(0)) == 0) &&
                (nullableCode(genericNull.get(1)) == -1) && (genericNull == alreadyNullable) &&
                (textCode(text.get(0)) == 1) && (textCode(text.get(1)) == 2) &&
                (textCode(text.get(2)) == 0) &&
                (nullableCode(nullFirst.get(0)) == 0) && (nullableCode(nullLast.get(1)) == 0) &&
                (inferredNullFirst == nullFirst) && (inferredNullLast == nullLast) &&
                (inferredNested == [nullFirst, nullLast]) &&
                (mixedNested == expectedMixedNested) && (reverseMixedNested == expectedReverseMixedNested) &&
                (inferredWrapped == wrapped) && (nestedCode(inferredWrappedReverse.get(1)) == 2) &&
                (nestedIntCode(intChoices.get(-1)) == 0) &&
                (nestedIntCode(intChoices.get(0)) == 1) && (nestedIntCode(intChoices.get(1)) == 2) &&
                (nestedIntCode(numberChoices.get(1)) == 45) && (intChoices == alreadyNullableChoices) &&
                (nestedCode(textChoices.get(-1)) == 0) && (nestedCode(textChoices.get(0)) == 1) &&
                (nestedCode(textChoices.get(1)) == 2) && (nestedCode(presentTextChoices.get(1)) == 3) &&
                (nestedCode(wrapped.get(0)) == 2) && (nestedCode(wrapped.get(1)) == 3) &&
                (nestedCode(optional.get(-1)) == 0) && (nestedCode(optional.get(0)) == 1) &&
                (nestedCode(optional.get(1)) == 2) && (nestedCode(optional.get(2)) == 3) &&
                (records == [Item { id = 1 }, Item { id = 2 }]) &&
                (records != [Item { id = 2 }, Item { id = 1 }]) &&
                (group.values == numbers) && (nested == [[1, 2], [3]]) &&
                (emptyCallback() == empty)
            }
            """;
        await NullableAcceptanceRuntime.WithGenerated(source, type =>
        {
            Assert.Equal(true, NullableAcceptanceRuntime.Invoke(type, "answer"));
            foreach (var name in new[] { "literalOrder", "callbackFailure", "mapOrder", "filterOrder" })
            {
                var error = Assert.Throws<TargetInvocationException>(
                    () => NullableAcceptanceRuntime.Invoke(type, name));
                Assert.IsType<DivideByZeroException>(error.InnerException);
            }
        });
    }

    [Fact(DisplayName = "Immutable list diagnostics")]
    public void InvalidListsAndOperationsAreRejected()
    {
        var cases = new[]
        {
            "fn f(): int { let value = []\n 42 }",
            "fn f(): List<int> { [1, true] }",
            "fn f(): List<int> { [1].append(\"wrong\") }",
            "fn f(): Option<int> { [1].get(\"wrong\") }",
            "fn f(): List<int> { [1].map(value => value == 1) }",
            "fn f(): List<int> { [1].filter(value => value + 1) }",
            "fn f(): int { [1].fold(0, (acc, value) => true) }",
            "fn f(): Unit { [1].add(2) }",
            "fn f(value: List<int?>): int? { value.get(0) }",
            "fn f(): List<int?> { let bound: List<int> = [1]\n bound }",
            "fn f(): List<(int) -> int> { [value => value] }",
            "fn f(): List<int> { [1,] }",
            "fn f(): List<int> { [1, 2 }"
        };
        for (var i = 0; i < cases.Length; i++)
        {
            var result = DSharpCompiler.Compile("module Demo\n" + cases[i], "list.ds");
            Assert.True(result.CSharp is null, $"Invalid list scenario {i} unexpectedly compiled.");
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, error => Assert.Equal("list.ds", error.Path));
        }
    }

    [Fact(DisplayName = "Immutable lists in multiple modules")]
    public async Task ListElementsResolveImportedFunctions()
    {
        var files = new[]
        {
            new SourceFile("lib.ds", "module Lib\nfn initial(): int { 40 }"),
            new SourceFile("app.ds", """
                module App
                import Lib
                fn values(): List<int> { [Lib.initial(), 2] }
                fn answer(): int { values().fold(0, (acc, value) => acc + value) }
                """)
        };
        var compilation = DSharpCompiler.Compile(files);
        Assert.Empty(compilation.Diagnostics);
        Assert.NotNull(compilation.CSharp);

        var directory = Path.Combine(Path.GetTempPath(), "dsharp-list-multifile-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "Generated.cs"), compilation.CSharp);
            File.WriteAllText(Path.Combine(directory, "Generated.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net11.0</TargetFramework><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>");
            File.WriteAllText(Path.Combine(directory, "global.json"), "{\"sdk\":{\"version\":\"11.0.100\",\"rollForward\":\"disable\"}}");
            File.WriteAllText(Path.Combine(directory, "NuGet.Config"), "<configuration><packageSources><clear/></packageSources></configuration>");
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "build", "Generated.csproj", "--nologo", "--verbosity", "quiet" }) start.ArgumentList.Add(argument);
            using var build = Process.Start(start)!;
            var stdout = build.StandardOutput.ReadToEndAsync();
            var stderr = build.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try { await build.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { build.Kill(true); throw new TimeoutException("Multi-file list build timed out."); }
            Assert.True(build.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("list-multifile-" + Guid.NewGuid(), isCollectible: true);
            try
            {
                var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0/Generated.dll"));
                var type = Assert.Single(assembly.GetTypes(), candidate => candidate.Name == "App");
                Assert.Equal(42, type.GetMethod("answer", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.Invoke(null, null));
            }
            finally { context.Unload(); }
        }
        finally { Directory.Delete(directory, true); }

        // The current module's nominal type wins over the standard List name.
        const string shadow = """
            module Demo
            type List<T> { value: T }
            fn answer(): int {
                let box: List<int> = List { value = 42 }
                box.value
            }
            """;
        await NullableAcceptanceRuntime.WithGenerated(shadow,
            type => Assert.Equal(42, NullableAcceptanceRuntime.Invoke(type, "answer")));
    }
}
