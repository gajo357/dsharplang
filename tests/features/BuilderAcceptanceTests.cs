using System.Reflection;
using System.Diagnostics;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class BuilderAcceptanceTests
{
    [Fact(DisplayName = "Local builders")]
    public async Task BuildersRemainLocalAndFreezeIndependentSnapshots()
    {
        // Exercise D# code, then compile and execute the generated C#; the
        // example file is not staged by the isolated feature-test verifier.
        const string source = """
            module Demo
            type Key { region: string
                       id: int }
            type Problem { | Bad(int) }
            type Kind { | Personal(int) | Business(int) }

            fn positiveValues(values: List<int>): List<int> {
                let result = MutableList<int>()
                for value in values {
                    if value > 0 { result.add(value) }
                }
                result.freeze()
            }
            fn listSnapshots(): bool {
                let values: MutableList<int> = MutableList()
                let alias: MutableList<int> = (values)
                let empty = values.freeze()
                let unit: Unit = alias.add(1)
                let first = values.freeze()
                alias.add(2)
                let second = alias.freeze()
                values.add(3)
                let third = values.freeze()
                let nothing: List<int> = []
                (empty == nothing) && (first == [1]) && (second == [1, 2]) &&
                (third == [1, 2, 3]) && (positiveValues([-1, 42, 3]) == [42, 3])
            }
            fn singleton<T>(value: T): List<T> {
                let builder = MutableList<T>()
                builder.add(value)
                builder.freeze()
            }
            fn nullableSingleton<T>(value: T?): List<T?> {
                let builder = MutableList<T?>()
                builder.add(value)
                builder.freeze()
            }
            fn mapGeneric<T>(value: T): Map<int, T> {
                let builder = MutableMap<int, T>()
                builder.set(1, value)
                builder.freeze()
            }
            fn makeInferred() {
                let builder = MutableList<int>()
                builder.add(42)
                builder.freeze()
            }
            fn nullableValues(): bool {
                let values = MutableList<int?>()
                values.add(null)
                values.add(42)
                let expected: List<int?> = [null, 42]
                let onlyNull: List<int?> = [null]
                let textNull: List<string?> = [null]
                let names = MutableMap<string, string?>()
                names.set("name", null)
                let nullName = names.freeze()
                names.set("name", "Alice")
                let alice = names.freeze()
                (values.freeze() == expected) &&
                (singleton<int?>(null) == onlyNull) && (makeInferred() == [42]) &&
                (nullableSingleton<int>(null) == onlyNull) &&
                (nullableSingleton<int?>(null) == onlyNull) &&
                (nullableSingleton<string>(null) == textNull) &&
                (nullableSingleton<string?>(null) == textNull) &&
                (mapGeneric<int?>(null) == Map.empty<int, int?>().set(1, null)) &&
                (nullName == Map.empty<string, string?>().set("name", null)) &&
                (alice == Map.empty<string, string?>().set("name", "Alice"))
            }
            fn mapSnapshots(): bool {
                let builder: MutableMap<Key, int> = MutableMap<Key, int>()
                let alias = builder
                let empty = alias.freeze()
                let first = Key { region = "north"
                                  id = 1 }
                let equal = Key { region = "north"
                                  id = 1 }
                let unit: Unit = builder.set(first, 10)
                let before = builder.freeze()
                alias.set(equal, 42)
                let after = alias.freeze()
                builder.set(Key { region = "south"
                                  id = 2 }, 7)
                let last = builder.freeze()
                (empty.count() == 0) && (before.count() == 1) &&
                (after.count() == 1) && (last.count() == 2) &&
                (before == Map.empty<Key, int>().set(first, 10)) &&
                (after == Map.empty<Key, int>().set(equal, 42))
            }
            fn lambdaLocal(): bool {
                let make: (int) -> List<int> = (value: int) => {
                    let local = MutableList<int>()
                    let alias = local
                    alias.add(value)
                    local.freeze()
                }
                (make(7) == [7]) && (make(42) == [42])
            }
            fn collidingKeys(): bool {
                let builder = MutableMap<Kind, int>()
                builder.set(Kind.Personal(31), 1)
                let first = builder.freeze()
                builder.set(Kind.Business(0), 2)
                let second = builder.freeze()
                builder.set(Kind.Personal(31), 42)
                let third = builder.freeze()
                (first == Map.empty<Kind, int>().set(Kind.Personal(31), 1)) &&
                (second == Map.empty<Kind, int>().set(Kind.Personal(31), 1).set(Kind.Business(0), 2)) &&
                (third == Map.empty<Kind, int>().set(Kind.Personal(31), 42).set(Kind.Business(0), 2))
            }
            fn mapRepresentative(first: Key, second: Key): Map<Key, int> {
                let builder = MutableMap<Key, int>()
                builder.set(first, 1)
                builder.set(second, 2)
                builder.freeze()
            }
            fn key(): Key { Key { region = "north"
                                  id = 1 } }
            fn failKey(): int { 1 / 0 }
            fn failValue(): int { 2147483647 + 1 }
            fn mutationOrder(): Unit {
                let builder = MutableMap<int, int>()
                builder.set(failKey(), failValue())
            }
            fn run(): bool {
                listSnapshots() && nullableValues() && mapSnapshots() && lambdaLocal() && collidingKeys()
            }
            """;
        await NullableAcceptanceRuntime.WithGenerated(source, type =>
        {
            Assert.Equal(true, NullableAcceptanceRuntime.Invoke(type, "run"));

            // Equal keys are distinct records; update must retain the first
            // representative exactly as immutable Map.set does.
            var first = NullableAcceptanceRuntime.Invoke(type, "key")!;
            var second = NullableAcceptanceRuntime.Invoke(type, "key")!;
            Assert.NotSame(first, second);
            var method = type.GetMethod("mapRepresentative", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
            var map = method.Invoke(null, new[] { first, second })!;
            var storage = map.GetType().GetField("__keys", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var keys = Assert.IsAssignableFrom<Array>(storage.GetValue(map));
            Assert.Single(keys.Cast<object>());
            Assert.Same(first, keys.GetValue(0));

            var fault = Assert.Throws<TargetInvocationException>(
                () => NullableAcceptanceRuntime.Invoke(type, "mutationOrder"));
            Assert.IsType<DivideByZeroException>(fault.InnerException);
        });

        RejectEscapesAndBadOperations();
        await NamesAndModules();
    }

    private static void RejectEscapesAndBadOperations()
    {
        var cases = new[]
        {
            // No exposed builders in fields, parameters, returns, or through
            // nullable, tuple, collection or generic data shapes.
            "type Box { value: MutableList<int> }\nfn f(): Unit { () }",
            "type Box { value: Option<MutableList<int>> }\nfn f(): Unit { () }",
            "type Box { value: List<MutableMap<int, int>> }\nfn f(): Unit { () }",
            "type Box { | Wrap(MutableList<int>) }\nfn f(): Unit { () }",
            "fn f(value: MutableList<int>): Unit { () }",
            "fn f(value: MutableMap<int, int>?): Unit { () }",
            "fn f(): MutableList<int> { MutableList<int>() }",
            "fn f(): Option<MutableList<int>> { None }",
            "fn f(): List<MutableList<int>> { [] }",
            // Escape via returns, generic calls and local aliases.
            "fn f() { let b = MutableList<int>()\n b }",
            "fn f() { let b = MutableList<int>()\n let a = b\n a }",
            "fn id<T>(value: T): T { value }\nfn f(): Unit { let b = MutableList<int>()\n let x = id(b)\n () }",
            "fn pick<T>(value: T, number: int): T { value }\nfn f(): Unit { let b = MutableList<int>()\n let later = pick(b)\n () }",
            "fn f(): Unit { let b = MutableList<int>()\n let pair = (b, 1)\n () }",
            "fn f(): Unit { let b = MutableList<int>()\n let values = [b]\n () }",
            "fn f(): Unit { let b = MutableList<int>()\n let wrapped = Some(b)\n () }",
            "fn f(): Unit { let b = MutableMap<int, int>()\n let wrapped = Success(b)\n () }",
            // Captures are forbidden even for immediately invoked lambdas and
            // when a same-invocation alias is the captured name.
            "fn f(): Unit { let b = MutableList<int>()\n let g: () -> Unit = () => b.add(1)\n g() }",
            "fn f(): Unit { let b = MutableList<int>()\n let a = b\n let g: () -> Unit = () => a.add(1)\n g() }",
            "fn f(): Unit { let b = MutableList<int>()\n (() => b.add(1))() }",
            "fn f(): Unit { let b = MutableList<int>()\n let g: () -> () -> Unit = () => () => b.add(1)\n () }",
            // Builder contents must be immutable and each operation typechecks.
            "fn f(): Unit { let b = MutableList<MutableList<int>>()\n () }",
            "fn sink<T>(value: T): Unit { let b = MutableList<T>()\n b.add(value) }\nfn f(): Unit { let g: (int) -> int = x => x\n sink<(int) -> int>(g) }",
            "fn sink<T>(value: T): Unit { let b = MutableList<T>()\n b.add(value) }\nfn forward<T>(value: T): Unit { sink<T>(value) }\nfn f(): Unit { let g: (int) -> int = x => x\n forward<(int) -> int>(g) }",
            "fn f(): Unit { let b = MutableMap<List<int>, int>()\n () }",
            "fn f(): Unit { let b = MutableList<int>()\n b.add(true) }",
            "fn f(): Unit { let b = MutableMap<int, string>()\n b.set(1, 2) }",
            "fn f(): Unit { let b = MutableList<int>()\n b.remove(1) }"
        };
        for (var i = 0; i < cases.Length; i++)
        {
            var result = DSharpCompiler.Compile("module Demo\n" + cases[i], "builder-invalid.ds");
            Assert.True(result.CSharp is null, $"Unsafe builder scenario {i} unexpectedly compiled: {cases[i]}");
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, diagnostic => Assert.Equal("builder-invalid.ds", diagnostic.Path));
            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code.StartsWith("DS1", StringComparison.Ordinal));
        }
    }

    private static async Task NamesAndModules()
    {
        const string nominal = """
            module Demo
            type MutableList<T> { value: T }
            type MutableMap<T> { value: T }
            fn run(): int {
                let first: MutableList<int> = MutableList { value = 40 }
                let second: MutableMap<int> = MutableMap { value = 2 }
                first.value + second.value
            }
            """;
        await NullableAcceptanceRuntime.WithGenerated(nominal,
            type => Assert.Equal(42, NullableAcceptanceRuntime.Invoke(type, "run")));
        const string lexical = """
            module Demo
            fn run(): int {
                let MutableList: (int) -> int = (x: int) => x + 1
                MutableList(41)
            }
            """;
        await NullableAcceptanceRuntime.WithGenerated(lexical,
            type => Assert.Equal(42, NullableAcceptanceRuntime.Invoke(type, "run")));
        var controls = new[]
        {
            "type MutableList<T> { value: T }\nfn f(): Unit { let x = MutableList<int>()\n () }",
            "type MutableMap<T> { value: T }\nfn f(): Unit { let x = MutableMap<int, int>()\n () }",
            "fn f<MutableList>(): Unit { let x = MutableList<int>()\n () }",
            "fn f<MutableMap>(): Unit { let x = MutableMap<int, int>()\n () }"
        };
        foreach (var control in controls)
        {
            var result = DSharpCompiler.Compile("module Demo\n" + control, "builder-shadow.ds");
            Assert.Null(result.CSharp);
            Assert.NotEmpty(result.Diagnostics);
            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code.StartsWith("DS1", StringComparison.Ordinal));
        }

        var importedTypes = DSharpCompiler.Compile(new[]
        {
            new SourceFile("lib.ds", "module Lib\ntype Item { value: int }"),
            new SourceFile("app.ds", """
                module App
                import Lib
                fn answer(): int {
                    let builder: MutableList<Item> = MutableList<Item>()
                    builder.add(Item { value = 42 })
                    match builder.freeze().get(0) { None => 0
                                                    Some(item) => item.value }
                }
                """)
        });
        Assert.Empty(importedTypes.Diagnostics);
        Assert.NotNull(importedTypes.CSharp);
        await ExecuteApp(importedTypes.CSharp!);

        var importedFunction = DSharpCompiler.Compile(new[]
        {
            new SourceFile("lib.ds", "module Lib\nfn MutableList<T>(): int { 42 }"),
            new SourceFile("app.ds", "module App\nimport Lib\nfn answer(): int { MutableList<int>() }")
        });
        Assert.Empty(importedFunction.Diagnostics);
        Assert.NotNull(importedFunction.CSharp);
        await ExecuteApp(importedFunction.CSharp!);

        var importedTypeShadow = DSharpCompiler.Compile(new[]
        {
            new SourceFile("lib.ds", "module Lib\ntype MutableList<T> { value: T }"),
            new SourceFile("app.ds", "module App\nimport Lib\nfn answer(): Unit { let x = MutableList<int>()\n () }")
        });
        Assert.Null(importedTypeShadow.CSharp);
        Assert.NotEmpty(importedTypeShadow.Diagnostics);
        Assert.DoesNotContain(importedTypeShadow.Diagnostics, diagnostic => diagnostic.Code.StartsWith("DS1", StringComparison.Ordinal));
    }

    private static async Task ExecuteApp(string csharp)
    {
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-builder-modules-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "Generated.cs"), csharp);
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
            catch (OperationCanceledException) { build.Kill(true); throw new TimeoutException("Multi-file builder build timed out."); }
            Assert.True(build.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("builder-multifile-" + Guid.NewGuid(), isCollectible: true);
            try
            {
                var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0/Generated.dll"));
                var app = Assert.Single(assembly.GetTypes(), candidate => candidate.Name == "App");
                Assert.Equal(42, app.GetMethod("answer", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.Invoke(null, null));
            }
            finally { context.Unload(); }
        }
        finally { Directory.Delete(directory, true); }
    }
}
