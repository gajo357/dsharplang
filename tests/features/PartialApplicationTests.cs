using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class PartialApplicationTests
{
    [Fact(DisplayName = "Partial calls and pipes")]
    public async Task PartialCallsCaptureValuesAndExecuteOnlyWhenSaturated()
    {
        const string source = """
            module Demo
            fn calculate(a: int, b: int, c: int): int { a * b + c }
            fn add(a: int, b: int): int { a + b }
            fn inferredPipe() { 1 |> add(2) }
            fn source(): int { 40 }
            fn argument(): int { 2 }
            fn factory(): (int, int) -> int { (a, b) => a + b }
            fn identity<T>(first: T, second: T): T { first }
            fn genericId<T>(value: T): T { value }
            fn nullableFirst<T>(first: T?, second: T): T? { first }
            fn selectSecond<T>(first: int, second: T): T { second }
            fn selectGeneric<T, U>(first: T, second: U): U { second }
            fn typedVia(): (string) -> string { 42 |> selectGeneric() }
            fn applyCallback(callback: (int) -> int, value: int): int { callback(value) }
            fn tupleFirst(pair: (int, int), other: int): int {
                let (a, b) = pair
                a + b + other
            }
            fn dangerous(a: int, b: int): int { a / (b - b) }
            fn earlyPartial(): int {
                let unused = calculate(if true { return 42 } else { 2 })
                1 / 0
            }
            fn make(): (int) -> int { factory()(argument()) }
            fn makeEmpty(): (int, int) -> int { factory()() }
            fn makeGeneric<T>(value: T): (T) -> T { identity(value) }
            fn makeGenericNullable<T>(value: T?): (T) -> T? { nullableFirst<T>(value) }
            fn pipeOrder(): int { source() |> factory()(argument()) }
            fn run(): bool {
                let withA = calculate(2)
                let withAB = withA(3)
                let fromEmpty: (int, int, int) -> int = calculate()
                let bare: (int, int) -> int = add
                let intermediate = 2 |> calculate
                let again: (int, int) -> int = (a, b) => a + b
                let closure = again(20)
                let generic = identity(42)
                let contextual: (int) -> int = identity(42)
                let nullable = nullableFirst<int>(null)
                let nullableText = nullableFirst<string>(null)
                let nullableAgain = nullableFirst<int?>(null)
                let genericFactory = makeGeneric<int>(42)
                let genericNullableFactory = makeGenericNullable<int>(null)
                let fromExpected: (string) -> string = selectSecond(1)
                let fromPipe = "Alice" |> selectSecond(1)
                let fromEmptyPipe = 42 |> genericId()
                let partialFromEmpty: (int) -> int = 42 |> identity()
                let groupedGeneric: (int) -> int = (identity)(42)
                let tuple = tupleFirst((20, 21))
                let safe = dangerous(1)
                let callback: (int) -> int = x => x + 1
                let apply: ((int) -> int, int) -> int = (f, x) => f(x)
                let functionValue = callback |> apply
                let suppliedCallback = applyCallback(callback)
                let multiLine = 40
                    |> add(2)
                (withAB(4) == 10) && (calculate(2)(3, 4) == 10) &&
                (calculate(2)(3)(4) == 10) && ((4 |> withAB) == 10) &&
                (fromEmpty(2, 3, 4) == 10) && (intermediate(3, 4) == 10) &&
                (bare(20, 22) == 42) && ((1 |> add(2)) == 3) && (inferredPipe() == 3) &&
                (closure(22) == 42) && (generic(0) == 42) &&
                (contextual(0) == 42) && (nullable(42) == null) &&
                (nullableText("x") == null) && (nullableAgain(null) == null) &&
                (genericFactory(0) == 42) && (genericNullableFactory(42) == null) &&
                (fromExpected("Alice") == "Alice") && (fromPipe == "Alice") &&
                (typedVia()("Alice") == "Alice") &&
                (fromEmptyPipe == 42) && (partialFromEmpty(0) == 42) &&
                ((genericId)(42) == 42) && (groupedGeneric(0) == 42) && (tuple(1) == 42) &&
                (functionValue(41) == 42) && (suppliedCallback(41) == 42) &&
                (multiLine == 42) && (earlyPartial() == 42)
            }
            """;

        await WithInstrumentedGenerated(source, type =>
        {
            Assert.Equal(true, NullableAcceptanceRuntime.Invoke(type, "run"));
            NullableAcceptanceRuntime.Invoke(type, "resetCounters");
            var partial = Assert.IsAssignableFrom<Delegate>(NullableAcceptanceRuntime.Invoke(type, "make"));
            Assert.Equal(1, NullableAcceptanceRuntime.Invoke(type, "readFactoryCalls"));
            Assert.Equal(1, NullableAcceptanceRuntime.Invoke(type, "readArgumentCalls"));
            Assert.Equal(0, NullableAcceptanceRuntime.Invoke(type, "readTargetCalls"));
            Assert.Equal(42, partial.DynamicInvoke(40));
            Assert.Equal(22, partial.DynamicInvoke(20));
            Assert.Equal(2, NullableAcceptanceRuntime.Invoke(type, "readTargetCalls"));
            Assert.Equal(1, NullableAcceptanceRuntime.Invoke(type, "readFactoryCalls"));
            Assert.Equal(1, NullableAcceptanceRuntime.Invoke(type, "readArgumentCalls"));
            NullableAcceptanceRuntime.Invoke(type, "resetCounters");
            var empty = Assert.IsAssignableFrom<Delegate>(NullableAcceptanceRuntime.Invoke(type, "makeEmpty"));
            Assert.Equal(1, NullableAcceptanceRuntime.Invoke(type, "readFactoryCalls"));
            Assert.Equal(0, NullableAcceptanceRuntime.Invoke(type, "readTargetCalls"));
            Assert.Equal(42, empty.DynamicInvoke(20, 22));
            Assert.Equal(1, NullableAcceptanceRuntime.Invoke(type, "readTargetCalls"));
            NullableAcceptanceRuntime.Invoke(type, "resetCounters");
            Assert.Equal(42, NullableAcceptanceRuntime.Invoke(type, "pipeOrder"));
            Assert.Equal(1, NullableAcceptanceRuntime.Invoke(type, "readSourceOrder"));
            Assert.Equal(2, NullableAcceptanceRuntime.Invoke(type, "readFactoryOrder"));
            Assert.Equal(3, NullableAcceptanceRuntime.Invoke(type, "readArgumentOrder"));
            Assert.Equal(1, NullableAcceptanceRuntime.Invoke(type, "readTargetCalls"));
        });

        var invalidCases = new[]
        {
            "fn add(a: int, b: int): int { a + b }\nfn f(): int { add(1, 2, 3) }",
            "fn add(a: int, b: int): int { a + b }\nfn f(): int { add(1) }",
            "fn add(a: int, b: int): int { a + b }\nfn f(): int { add(true) }",
            "fn add(a: int, b: int): int { a + b }\nfn f(): int { add(1)\n 42 }",
            "fn add(a: int, b: int): int { a + b }\nfn f(): int { 1 |> add(2, 3) }",
            "fn choose<T>(first: int, second: T): int { first }\nfn f(): int { let p = choose(1)\n 42 }",
            "fn add(a: int, b: int): int { a + b }\nfn f(): int { let p: (bool) -> int = add(1)\n 42 }",
            "type Box { value: (int) -> int }\nfn add(a: int, b: int): int { a + b }\nfn f(): int { let box = Box { value = add(1) }\n 42 }",
            "fn hold<T>(value: T, x: int): () -> T { () => value }\nfn f(): int { let cb: (int) -> int = x => x\n let p = hold(cb)\n 42 }"
        };
        for (var index = 0; index < invalidCases.Length; index++)
        {
            var input = "module Demo\n" + invalidCases[index];
            var result = DSharpCompiler.Compile(input, "partial.ds");
            Assert.True(result.CSharp is null, $"Scenario {index} unexpectedly compiled.");
            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.StartsWith("DS", diagnostic.Code);
            Assert.Equal("partial.ds", diagnostic.Path);
            Assert.InRange(diagnostic.Span.Start, 0, input.Length);
        }
    }

    private static async Task WithInstrumentedGenerated(string source, Action<Type> verify)
    {
        var compilation = DSharpCompiler.Compile(source, "partial.ds");
        Assert.Empty(compilation.Diagnostics);
        var csharp = compilation.CSharp!;
        var factory = Assert.Single(Regex.Matches(csharp, @"    public static ([^\r\n]+) @factory\(\)").Cast<Match>());
        csharp = csharp.Replace(factory.Value,
            "    private static int __clock, __sourceOrder, __factoryOrder, __argumentOrder, __factoryCalls, __argumentCalls, __targetCalls;\n" +
            "    public static void @resetCounters() { __clock = __sourceOrder = __factoryOrder = __argumentOrder = __factoryCalls = __argumentCalls = __targetCalls = 0; }\n" +
            "    public static int @readSourceOrder() => __sourceOrder;\n" +
            "    public static int @readFactoryOrder() => __factoryOrder;\n" +
            "    public static int @readArgumentOrder() => __argumentOrder;\n" +
            "    public static int @readFactoryCalls() => __factoryCalls;\n" +
            "    public static int @readArgumentCalls() => __argumentCalls;\n" +
            "    public static int @readTargetCalls() => __targetCalls;\n" +
            "    public static " + factory.Groups[1].Value + " @factory() { __factoryCalls++; __factoryOrder = ++__clock; var f = @factoryOriginal(); return (a, b) => { __targetCalls++; return f(a, b); }; }\n" +
            "    public static " + factory.Groups[1].Value + " @factoryOriginal()", StringComparison.Ordinal);
        foreach (var (name, counter) in new[] { ("source", "__sourceOrder = ++__clock;"), ("argument", "__argumentCalls++; __argumentOrder = ++__clock;") })
        {
            var declaration = Assert.Single(Regex.Matches(csharp, @"    public static ([^\r\n]+) @" + name + @"\(\)").Cast<Match>());
            csharp = csharp.Replace(declaration.Value,
                "    public static " + declaration.Groups[1].Value + " @" + name + "() { " + counter + " return @" + name + "Original(); }\n" +
                "    public static " + declaration.Groups[1].Value + " @" + name + "Original()", StringComparison.Ordinal);
        }

        var directory = Path.Combine(Path.GetTempPath(), "dsharp-partial-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "Generated.cs"), csharp);
            File.WriteAllText(Path.Combine(directory, "Generated.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net11.0</TargetFramework><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>");
            File.WriteAllText(Path.Combine(directory, "global.json"), "{\"sdk\":{\"version\":\"11.0.100\",\"rollForward\":\"disable\"}}");
            File.WriteAllText(Path.Combine(directory, "NuGet.Config"), "<configuration><packageSources><clear/></packageSources></configuration>");
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "build", "Generated.csproj", "--nologo", "--verbosity", "quiet" }) start.ArgumentList.Add(arg);
            using var build = Process.Start(start)!;
            var stdout = build.StandardOutput.ReadToEndAsync();
            var stderr = build.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try { await build.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { build.Kill(true); throw new TimeoutException("Generated partial application build timed out."); }
            Assert.True(build.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("partial-" + Guid.NewGuid(), isCollectible: true);
            try { verify(context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0/Generated.dll")).GetType("DSharp.Generated.Demo")!); }
            finally { context.Unload(); }
        }
        finally { Directory.Delete(directory, true); }
    }
}
