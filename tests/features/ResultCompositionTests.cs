using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class ResultCompositionTests
{
    [Fact(DisplayName = "Synchronous Result composition")]
    public async Task HelpersPreserveOutcomesAndCallbackEvaluation()
    {
        const string source = """
            module Demo
            fn transform(value: int): int { value + 1 }
            fn factory(): (int) -> int { transform }
            fn source(): Result<int, string> { Error("bad") }
            fn good(): Result<int, string> { Success(41) }
            fn zero(): Result<int, string> { Success(0) }
            fn bad(): Result<int, string> { Error("bad") }
            fn next(value: int): Result<int, string> {
                if value == 41 { Success(42) } else { Error("next") }
            }
            fn convert(value: string): int { 7 }
            fn observe(value: int): Unit { () }
            fn observeError(value: string): Unit { () }
            fn explode(value: int): int { value / 0 }
            fn badFactory(): (int) -> int {
                let unused = 1 / 0
                transform
            }
            fn failCreating(): Result<int, string> { map(badFactory(), bad()) }
            fn failInvoking(): Result<int, string> { map(explode, good()) }
            fn wrap(value: int): Result<int, string> { Success(value) }
            fn optional(value: int): Option<int?> { Some(null) }
            fn nullable(value: int?): int { if value == null { 42 } else { 0 } }
            fn identityNullable<T>(value: T?): T? { value }
            fn genericNullable<T>(input: Result<T?, string>): Result<T?, string> {
                map(identityNullable<T>(), input)
            }
            fn isNull(value: Option<int?>): int {
                match value { None => 1
                              Some(null) => 42
                              Some(n) => n }
            }
            fn valueCode(value: Result<int, string>): int {
                match value { Success(n) => n
                              Error(e) => 0 }
            }
            fn errorCode(value: Result<int, int>): int {
                match value { Success(n) => n
                              Error(e) => e }
            }
            fn nestedCode(value: Result<Result<int, string>, string>): int {
                match value { Success(Success(n)) => n
                              Success(Error(e)) => 1
                              Error(e) => 2 }
            }
            fn optionCode(value: Result<Option<int?>, string>): int {
                match value { Success(None) => 1
                              Success(Some(null)) => 42
                              Success(Some(n)) => n
                              Error(e) => 0 }
            }
            fn mapped(flag: bool): Result<int, string> {
                map(factory(), if flag { good() } else { source() })
            }
            fn partiallyMapped(): Result<int, string> { source() |> map(factory()) }
            fn piped(): int {
                let result = good() |> andThen(next) |> map(transform)
                valueCode(result)
            }
            fn directLambda(): Result<int, string> { map(x => x + 1, good()) }
            fn inferredLambda() { good() |> map(x => x + 1) }
            fn inferredAndThen() { good() |> andThen(x => Success(x + 1)) }
            fn typedAndThen(): Result<int, string> { good() |> andThen(x => Success(x + 1)) }
            fn typedError(): Result<int, string> { good() |> andThen(x => Error("stop")) }
            fn earlyReturn() { good() |> andThen(x => { return Success(x + 1) }) }
            fn forward<T>(value: Result<T, string>, callback: (T) -> int): Result<int, string> {
                map(callback, value)
            }
            fn run(): bool {
                let absent: Result<Option<int?>, string> = Success(None)
                let presentNull: Result<Option<int?>, string> = Success(Some(null))
                let nullValue: Result<int?, string> = Success(null)
                let nullText: Result<string?, string> = Success(null)
                let mapper: ((int) -> int, Result<int, string>) -> Result<int, string> = map
                (valueCode(andThen(next, good())) == 42) &&
                (valueCode(andThen(next, bad())) == 0) &&
                (andThen(next, zero()) == Error<int, string>("next")) &&
                (valueCode(map(transform, good())) == 42) &&
                (valueCode(mapper(transform, good())) == 42) &&
                (valueCode(map(transform, bad())) == 0) &&
                (errorCode(mapError(convert, bad())) == 7) &&
                (errorCode(mapError(convert, good())) == 41) &&
                (tap(observe, good()) == good()) && (tap(observe, bad()) == bad()) &&
                (tapError(observeError, good()) == good()) && (tapError(observeError, bad()) == bad()) &&
                (nestedCode(map(wrap, good())) == 41) &&
                (valueCode(directLambda()) == 42) && (valueCode(inferredLambda()) == 42) &&
                (valueCode(inferredAndThen()) == 42) && (valueCode(typedAndThen()) == 42) &&
                (typedError() == Error<int, string>("stop")) &&
                (valueCode(earlyReturn()) == 42) &&
                (valueCode(forward<int>(good(), transform)) == 42) &&
                (optionCode(map(optional, good())) == 42) &&
                (valueCode(map(nullable, nullValue)) == 42) &&
                (genericNullable<int>(nullValue) == nullValue) &&
                (genericNullable<int?>(nullValue) == nullValue) &&
                (genericNullable<string>(nullText) == nullText) &&
                (valueCode(map(isNull, absent)) == 1) &&
                (valueCode(map(isNull, presentNull)) == 42) &&
                (absent != presentNull) && (piped() == 43)
            }
            """;

        await WithInstrumentedGenerated(source, type =>
        {
            Assert.Equal(true, NullableAcceptanceRuntime.Invoke(type, "run"));
            Assert.Equal(3, NullableAcceptanceRuntime.Invoke(type, "readNextCount"));
            Assert.Equal(1, NullableAcceptanceRuntime.Invoke(type, "readConvertCount"));
            Assert.Equal(1, NullableAcceptanceRuntime.Invoke(type, "readObserveCount"));
            Assert.Equal(1, NullableAcceptanceRuntime.Invoke(type, "readObserveErrorCount"));
            NullableAcceptanceRuntime.Invoke(type, "resetCounters");
            type.GetMethod("mapped", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.Invoke(null, new object[] { false });
            Assert.Equal(1, NullableAcceptanceRuntime.Invoke(type, "readFactoryCount"));
            Assert.Equal(1, NullableAcceptanceRuntime.Invoke(type, "readFactoryOrder"));
            Assert.Equal(2, NullableAcceptanceRuntime.Invoke(type, "readSourceOrder"));
            Assert.Equal(0, NullableAcceptanceRuntime.Invoke(type, "readTransformCount"));
            NullableAcceptanceRuntime.Invoke(type, "resetCounters");
            type.GetMethod("mapped", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.Invoke(null, new object[] { true });
            Assert.Equal(1, NullableAcceptanceRuntime.Invoke(type, "readFactoryCount"));
            Assert.Equal(1, NullableAcceptanceRuntime.Invoke(type, "readTransformCount"));
            NullableAcceptanceRuntime.Invoke(type, "resetCounters");
            NullableAcceptanceRuntime.Invoke(type, "partiallyMapped");
            Assert.Equal(1, NullableAcceptanceRuntime.Invoke(type, "readFactoryCount"));
            Assert.Equal(1, NullableAcceptanceRuntime.Invoke(type, "readSourceOrder"));
            Assert.Equal(2, NullableAcceptanceRuntime.Invoke(type, "readFactoryOrder"));
            Assert.Equal(0, NullableAcceptanceRuntime.Invoke(type, "readTransformCount"));
            foreach (var name in new[] { "failCreating", "failInvoking" })
            {
                var exception = Assert.Throws<TargetInvocationException>(() => NullableAcceptanceRuntime.Invoke(type, name));
                Assert.IsType<DivideByZeroException>(exception.InnerException);
            }
        });

        var invalidCases = new[]
        {
            "fn next(x: int): Result<int, int> { Success(x) }\nfn f(value: Result<int, string>): Result<int, string> { andThen(next, value) }",
            "fn good(): Result<int, string> { Success(41) }\nfn f() { good() |> andThen(x => Error<int, int>(x)) }",
            "fn observe(x: int): int { x }\nfn f(value: Result<int, string>): Result<int, string> { tap(observe, value) }",
            "fn observe(x: string): int { 1 }\nfn f(value: Result<int, string>): Result<int, string> { tapError(observe, value) }",
            "fn transform(x: string): int { 1 }\nfn f(value: Result<int, string>): Result<int, string> { map(transform, value) }",
            "fn f(): int { map(1, 2) }"
        };
        for (var index = 0; index < invalidCases.Length; index++)
        {
            var input = "module Demo\n" + invalidCases[index];
            var result = DSharpCompiler.Compile(input, "result-composition.ds");
            Assert.True(result.CSharp is null, $"Invalid composition scenario {index} unexpectedly compiled.");
            var error = Assert.Single(result.Diagnostics);
            Assert.StartsWith("DS", error.Code);
            Assert.Equal("result-composition.ds", error.Path);
            Assert.InRange(error.Span.Start, 0, input.Length - 1);
        }

        const string shadowing = """
            module Demo
            fn map(value: int): int { value + 1 }
            fn run(): int {
                let tap: (int) -> int = value => value + 1
                tap(map(40))
            }
            """;
        await NullableAcceptanceRuntime.WithGenerated(shadowing,
            type => Assert.Equal(42, NullableAcceptanceRuntime.Invoke(type, "run")));

        const string typeShadowing = """
            module Demo
            type Result { | Local(int) }
            fn map(value: Result): int {
                match value { Result.Local(n) => n }
            }
            fn run(): int { map(Result.Local(42)) }
            """;
        await NullableAcceptanceRuntime.WithGenerated(typeShadowing,
            type => Assert.Equal(42, NullableAcceptanceRuntime.Invoke(type, "run")));

        const string typeShadowWithPrelude = """
            module Demo
            type Result { | Local(int) }
            fn run(): int {
                let value = Success<int, string>(41) |> map(n => n + 1)
                match value { Success(n) => n
                              Error(e) => 0 }
            }
            """;
        await NullableAcceptanceRuntime.WithGenerated(typeShadowWithPrelude,
            type => Assert.Equal(42, NullableAcceptanceRuntime.Invoke(type, "run")));
    }

    private static async Task WithInstrumentedGenerated(string source, Action<Type> verify)
    {
        var compilation = DSharpCompiler.Compile(source, "result-composition.ds");
        Assert.Empty(compilation.Diagnostics);
        var csharp = compilation.CSharp!;
        var factory = Assert.Single(Regex.Matches(csharp, @"    public static ([^\r\n]+) @factory\(\)").Cast<Match>());
        csharp = csharp.Replace(factory.Value,
            "    private static int __clock, __factoryOrder, __sourceOrder, __factoryCount, __transformCount, __nextCount, __convertCount, __observeCount, __observeErrorCount;\n" +
            "    public static void @resetCounters() { __clock = __factoryOrder = __sourceOrder = __factoryCount = __transformCount = 0; }\n" +
            "    public static int @readFactoryOrder() => __factoryOrder;\n" +
            "    public static int @readSourceOrder() => __sourceOrder;\n" +
            "    public static int @readFactoryCount() => __factoryCount;\n" +
            "    public static int @readTransformCount() => __transformCount;\n" +
            "    public static int @readNextCount() => __nextCount;\n" +
            "    public static int @readConvertCount() => __convertCount;\n" +
            "    public static int @readObserveCount() => __observeCount;\n" +
            "    public static int @readObserveErrorCount() => __observeErrorCount;\n" +
            "    public static " + factory.Groups[1].Value + " @factory() { __factoryCount++; __factoryOrder = ++__clock; return @factoryOriginal(); }\n" +
            "    public static " + factory.Groups[1].Value + " @factoryOriginal()", StringComparison.Ordinal);
        var sourceDecl = Assert.Single(Regex.Matches(csharp, @"    public static ([^\r\n]+) @source\(\)").Cast<Match>());
        csharp = csharp.Replace(sourceDecl.Value,
            "    public static " + sourceDecl.Groups[1].Value + " @source() { __sourceOrder = ++__clock; return @sourceOriginal(); }\n" +
            "    public static " + sourceDecl.Groups[1].Value + " @sourceOriginal()", StringComparison.Ordinal);
        var transform = Assert.Single(Regex.Matches(csharp, @"    public static int @transform\(int [^\r\n]+\)").Cast<Match>());
        csharp = csharp.Replace(transform.Value,
            "    public static int @transform(int value) { __transformCount++; return @transformOriginal(value); }\n" +
            transform.Value.Replace("@transform(", "@transformOriginal(", StringComparison.Ordinal), StringComparison.Ordinal);
        foreach (var (name, parameterType, counter) in new[]
        {
            ("next", "int", "__nextCount"), ("convert", "string", "__convertCount"),
            ("observe", "int", "__observeCount"), ("observeError", "string", "__observeErrorCount")
        })
        {
            var declaration = Assert.Single(Regex.Matches(csharp, @"    public static ([^\r\n]+) @" + name + @"\(" + parameterType + @" [^\r\n]+\)").Cast<Match>());
            csharp = csharp.Replace(declaration.Value,
                "    public static " + declaration.Groups[1].Value + " @" + name + "(" + parameterType + " value) { " + counter + "++; return @" + name + "Original(value); }\n" +
                declaration.Value.Replace("@" + name + "(", "@" + name + "Original(", StringComparison.Ordinal), StringComparison.Ordinal);
        }
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-result-composition-" + Guid.NewGuid());
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
            var stdout = build.StandardOutput.ReadToEndAsync(); var stderr = build.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try { await build.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { build.Kill(true); throw new TimeoutException("Generated Result composition build timed out."); }
            Assert.True(build.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("result-composition-" + Guid.NewGuid(), isCollectible: true);
            try { verify(context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0/Generated.dll")).GetType("DSharp.Generated.Demo")!); }
            finally { context.Unload(); }
        }
        finally { Directory.Delete(directory, true); }
    }
}
