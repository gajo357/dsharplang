using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class MapSetTests
{
    [Fact(DisplayName = "MapSet")]
    public async Task Run()
    {
        await Cases();
        Diagnostics();
        await ModulesAndShadowing();
    }

    private static async Task Cases()
    {
        // Mirrors examples/map_set.ds. The isolated verifier stages test sources,
        // not the examples folder.
        const string source = """
            module Demo

            // A missing name differs from a name explicitly cleared to null.
            type AccountKey {
                region: string
                id: int
            }
            type CompositeKey { part: Option<string?> }
            type Kind { | Personal(int) | Business(int) }
            type Chain { | End | Link(int, Chain) }
            type Snapshot<T> { values: Map<string, T?> }

            fn keep<T>(value: T): T { value }
            fn snapshot<T>(seed: T, value: T?): Snapshot<T> {
                Snapshot { values = Map.empty<string, T?>().set("name", value) }
            }
            fn recordKey(): AccountKey {
                AccountKey {
                    region = "north"
                    id = 7
                }
            }
            fn personalKind(): Kind { Kind.Personal(7) }
            fn businessKind(): Kind { Kind.Business(7) }
            fn noOption(): Option<string?> { None }
            fn nullOption(): Option<string?> { Some(null) }
            fn optionRecord(): CompositeKey { CompositeKey { part = Some(null) } }
            fn chain(): Chain { Chain.Link(1, Chain.Link(2, Chain.End)) }
            fn representativeMap(first: AccountKey, second: AccountKey): Map<AccountKey, int> {
                Map.empty<AccountKey, int>().set(first, 1).set(second, 2)
            }
            fn representativeSet(first: AccountKey, second: AccountKey): Set<AccountKey> {
                Set.empty<AccountKey>().add(first).add(second)
            }
            fn inferred() { Map.empty<int, int>().set(1, 2).count() }
            fn inferredSet() { Set.from([1, 1]).count() }

            fn number(value: Option<int>): int {
                match value { None => -1
                              Some(n) => n }
            }

            fn name(value: Option<string?>): int {
                match value { None => 0
                              Some(null) => 1
                              Some(text) => if text == "Alice" { 2 } else { 3 } }
            }

            fn nestedName(value: Option<Option<string?>>): int {
                match value { None => 0
                              Some(None) => 1
                              Some(Some(null)) => 2
                              Some(Some(text)) => if text == "Alice" { 3 } else { 4 } }
            }

            fn nullableNumber(value: Option<int?>): int {
                match value { None => 0
                              Some(null) => 1
                              Some(n) => n + 2 }
            }

            fn answer(): bool {
                let empty = Map.empty<string, string?>()
                let cleared = empty.set("name", null)
                let renamed = cleared.set("name", "Alice")
                let additions = renamed.set("city", "London")
                let reverse = empty.set("city", "London").set("name", "Alice")
                let changed = additions.set("name", "Other")

                let names = Map.empty<string, Option<string?>>()
                    .set("unchanged", None)
                    .set("clear", Some(null))
                    .set("rename", Some("Alice"))

                let options: List<Option<string?>> = [None, Some(null), Some("Alice"), Some(null)]
                let distinct = Set.from(options)
                let emptySet: Set<int> = Set.from([])
                let explicitEmpty = Set.from<int>([])
                let distinctReverse = Set.empty<Option<string?>>()
                    .add(Some("Alice")).add(Some(null)).add(None)
                let extended = distinct.add(Some("Other"))
                let byOption = Map.empty<Option<string?>, int>()
                    .set(None, 1).set(Some(null), 2).set(Some("Alice"), 3)

                let nullKeys = Map.empty<string?, int>().set(null, 1).set("", 2)
                let nullableSet = Set.empty<int?>().add(null).add(0).add(null)
                let key = recordKey()
                let sameKey = recordKey()
                let recordKeys = Map.empty<AccountKey, int>().set(key, 10).set(sameKey, 42)
                let optionRecords = Map.empty<CompositeKey, int>().set(optionRecord(), 42)
                let kinds = Set.from([Kind.Personal(7), Kind.Business(7), Kind.Personal(7)])
                let collisionMap = Map.empty<Kind, int>().set(Kind.Personal(31), 1).set(Kind.Business(0), 2)
                let collisionSet = Set.from([Kind.Personal(31), Kind.Business(0)])
                let chains = Set.from([chain(), chain()])
                let recursiveMap = Map.empty<Chain, int>().set(chain(), 42)
                let intNull = snapshot<int>(0, null)
                let intAlreadyNullable = snapshot<int?>(null, null)
                let intValue = snapshot<int>(0, 42)
                let textNull = snapshot<string>("", null)
                let grouped = Map.empty<string, List<int>>().set("ids", [1, 2]).set("more", [3])
                let groupedReverse = Map.empty<string, List<int>>().set("more", [3]).set("ids", [1, 2])
                let pairValues = Map.empty<string, (int, int)>().set("a", (1, 2)).set("b", (3, 4))
                let pairValuesReverse = Map.empty<string, (int, int)>().set("b", (3, 4)).set("a", (1, 2))

                (inferred() == 1) && (inferredSet() == 1) &&
                (empty.count() == 0) && (name(empty.get("name")) == 0) &&
                (name(cleared.get("name")) == 1) && (name(renamed.get("name")) == 2) &&
                (cleared.count() == 1) && (name(cleared.get("name")) == 1) &&
                (additions.count() == 2) && (additions == reverse) && (additions != changed) &&
                additions.contains("name") && !additions.contains("missing") &&
                (nestedName(names.get("missing")) == 0) &&
                (nestedName(names.get("unchanged")) == 1) &&
                (nestedName(names.get("clear")) == 2) &&
                (nestedName(names.get("rename")) == 3) &&
                (emptySet.count() == 0) && (explicitEmpty == emptySet) &&
                (distinct.count() == 3) && (distinct == distinctReverse) &&
                (extended.count() == 4) && (distinct != extended) &&
                distinct.contains(None) && distinct.contains(Some(null)) &&
                distinct.contains(Some("Alice")) && !distinct.contains(Some("Other")) &&
                (number(byOption.get(None)) == 1) && (number(byOption.get(Some(null))) == 2) &&
                (number(byOption.get(Some("Alice"))) == 3) &&
                (nullKeys.count() == 2) && (number(nullKeys.get(null)) == 1) &&
                (number(nullKeys.get("")) == 2) &&
                (nullableSet.count() == 2) && nullableSet.contains(null) && nullableSet.contains(0) &&
                (recordKeys.count() == 1) && (number(recordKeys.get(sameKey)) == 42) &&
                (number(optionRecords.get(optionRecord())) == 42) &&
                (kinds.count() == 2) && kinds.contains(Kind.Personal(7)) && kinds.contains(Kind.Business(7)) &&
                (collisionMap.count() == 2) && (number(collisionMap.get(Kind.Personal(31))) == 1) &&
                (number(collisionMap.get(Kind.Business(0))) == 2) && (collisionSet.count() == 2) &&
                collisionSet.contains(Kind.Personal(31)) && collisionSet.contains(Kind.Business(0)) &&
                (chains.count() == 1) && chains.contains(chain()) && (number(recursiveMap.get(chain())) == 42) &&
                (nullableNumber(intNull.values.get("name")) == 1) &&
                (nullableNumber(intValue.values.get("name")) == 44) &&
                (intNull.values == intAlreadyNullable.values) && (name(textNull.values.get("name")) == 1) &&
                grouped.contains("ids") && (grouped == groupedReverse) &&
                (pairValues == pairValuesReverse)
            }
            fn failKey(): int { 1 / 0 }
            fn failValue(): int { 2147483647 + 1 }
            fn mapOrder(): Map<int, int> { Map.empty<int, int>().set(failKey(), failValue()) }
            fn failSet(): Set<int> { let x = 1 / 0
                Set.empty<int>() }
            fn setOrder(): Set<int> { failSet().add(failValue()) }
            """;
        await NullableAcceptanceRuntime.WithGenerated(source, type =>
        {
            Assert.Equal(true, NullableAcceptanceRuntime.Invoke(type, "answer"));

            // Independently created structurally equal keys must hash equally;
            // Option tags and nominal union cases contribute to the hash.
            int Hash(object value)
            {
                var hashInterface = Assert.Single(value.GetType().GetInterfaces(),
                    candidate => candidate.Name.StartsWith("__DSharp_Hashable", StringComparison.Ordinal));
                var method = hashInterface.GetMethod("__dsharp_hash");
                Assert.NotNull(method);
                return Assert.IsType<int>(method.Invoke(value, null));
            }
            object Value(string function) => NullableAcceptanceRuntime.Invoke(type, function)!;
            Assert.Equal(Hash(Value("recordKey")), Hash(Value("recordKey")));
            Assert.Equal(Hash(Value("optionRecord")), Hash(Value("optionRecord")));
            Assert.Equal(Hash(Value("personalKind")), Hash(Value("personalKind")));
            Assert.Equal(Hash(Value("nullOption")), Hash(Value("nullOption")));
            Assert.NotEqual(Hash(Value("noOption")), Hash(Value("nullOption")));

            // Equal keys are separate immutable objects; replacement/deduplication
            // must retain the original representative, which D# cannot observe.
            var first = Value("recordKey");
            var second = Value("recordKey");
            Assert.NotSame(first, second);
            object Representative(string function, string field)
            {
                var method = type.GetMethod(function, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                Assert.NotNull(method);
                var result = method.Invoke(null, new[] { first, second });
                Assert.NotNull(result);
                var storage = result.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(storage);
                var keys = Assert.IsAssignableFrom<Array>(storage.GetValue(result));
                Assert.Single(keys.Cast<object>());
                return keys.GetValue(0)!;
            }
            Assert.Same(first, Representative("representativeMap", "__keys"));
            Assert.Same(first, Representative("representativeSet", "__items"));

            foreach (var function in new[] { "mapOrder", "setOrder" })
            {
                var error = Assert.Throws<TargetInvocationException>(
                    () => NullableAcceptanceRuntime.Invoke(type, function));
                Assert.IsType<DivideByZeroException>(error.InnerException);
            }
        });
    }

    private static void Diagnostics()
    {
        var cases = new[]
        {
            "fn f(): Map<int, int> { Map.empty<int>() }",
            "fn f(): Set<int> { Set.empty<int, int>() }",
            "fn f(): int { let s = Set.from([])\n s.count() }",
            "fn f(): Map<int, int> { Map.empty<int, int>().set(1, true) }",
            "fn f(): Option<int> { Map.empty<int, int>().get(\"bad\") }",
            "fn f(): Set<int> { Set.from([1, 2]).add(\"bad\") }",
            "fn f(): Map<List<int>, int> { Map.empty<List<int>, int>() }",
            "fn f(): Set<(int, int)> { Set.empty<(int, int)>() }",
            "fn f(): Map<Option<List<int>>, int> { Map.empty<Option<List<int>>, int>() }",
            "type K { data: List<int> }\nfn f(): Map<K, int> { Map.empty<K, int>() }",
            "type K { | Case((int, int)) }\nfn f(): Set<K> { Set.empty<K>() }",
            "type Phantom<T> { id: int }\nfn f(): Map<Phantom<List<int>>, int> { Map.empty<Phantom<List<int>>, int>() }",
            "type Phantom<T> { | Case(int) }\nfn f(): Set<Phantom<(int, int)>> { Set.empty<Phantom<(int, int)>>() }",
            "fn f<T>(key: T): Map<T, int> { Map.empty<T, int>().set(key, 1) }",
            "fn f(): Map<(int) -> int, int> { Map.empty<(int) -> int, int>() }",
            "fn f(): Unit { Map.empty<int, int>().remove(1) }"
        };
        for (var i = 0; i < cases.Length; i++)
        {
            var result = DSharpCompiler.Compile("module Demo\n" + cases[i], "map-set.ds");
            Assert.True(result.CSharp is null, $"Invalid map/set scenario {i} unexpectedly compiled.");
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, error => Assert.Equal("map-set.ds", error.Path));
        }
    }

    private static async Task ModulesAndShadowing()
    {
        var files = new[]
        {
            new SourceFile("lib.ds", "module Lib\nfn initial(): int { 40 }"),
            new SourceFile("app.ds", """
                module App
                import Lib
                fn answer(): int {
                    let values = Map.empty<string, int>().set("key", Lib.initial())
                    match values.get("key") { None => 0
                                               Some(value) => value + 2 }
                }
                """)
        };
        var compilation = DSharpCompiler.Compile(files);
        Assert.Empty(compilation.Diagnostics);
        Assert.NotNull(compilation.CSharp);
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-map-set-multifile-" + Guid.NewGuid());
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
            catch (OperationCanceledException) { build.Kill(true); throw new TimeoutException("Multi-file map/set build timed out."); }
            Assert.True(build.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("map-set-multifile-" + Guid.NewGuid(), isCollectible: true);
            try
            {
                var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0/Generated.dll"));
                var type = Assert.Single(assembly.GetTypes(), candidate => candidate.Name == "App");
                Assert.Equal(42, type.GetMethod("answer", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.Invoke(null, null));
            }
            finally { context.Unload(); }
        }
        finally { Directory.Delete(directory, true); }

        const string shadow = """
            module Demo
            type Map<T> { value: T }
            type Set<T> { value: T }
            fn answer(): int {
                let map: Map<int> = Map { value = 40 }
                let set: Set<int> = Set { value = 2 }
                map.value + set.value
            }
            """;
        await NullableAcceptanceRuntime.WithGenerated(shadow,
            type => Assert.Equal(42, NullableAcceptanceRuntime.Invoke(type, "answer")));

        foreach (var invalid in new[]
        {
            "type Map<T> { value: T }\nfn f(): int { Map.empty<int, int>().count() }",
            "type Set<T> { value: T }\nfn f(): int { Set.empty<int>().count() }"
        })
        {
            var result = DSharpCompiler.Compile("module Demo\n" + invalid, "map-shadow.ds");
            Assert.Null(result.CSharp);
            Assert.NotEmpty(result.Diagnostics);
        }
    }
}
