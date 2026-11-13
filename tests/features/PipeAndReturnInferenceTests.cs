using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class PipeAndReturnInferenceTests
{
    [Fact(DisplayName = "Pipes and inferred returns/execution")]
    public async Task PipesEvaluateOnceInOrderAndInferenceUsesCheckedBodies()
    {
        const string source = """
            module Demo
            type Fault { | Failed }
            type Pair { first: int
                        second: int }
            fn increment(value: int): int { value + 1 }
            fn double(value: int): int { value * 2 }
            fn pick(first: int, second: int): int { first }
            fn identity<T>(value: T): T { value }
            fn optional(value: int?): int? { value }
            fn keep(value: Result<int, Fault>): Result<int, Fault> { value }
            fn resultCode(value: Result<int, Fault>): int {
                match value {
                    Success(number) => number
                    Error(Fault.Failed) => 7
                }
            }
            fn source(): int { 41 }
            fn factory(): (int) -> int { value => value + 1 }
            fn runOrder(): int { source() |> factory() }
            fn early(): int {
                let unused = (if true { return 42 } else { 41 }) |> factory()
                1 / 0
            }
            fn fails(value: int): bool { value / 0 > 0 }
            fn skip(): bool { false && (41 |> fails) }
            fn localShadow(): int {
                let increment: (int) -> int = value => value + 2
                40 |> increment
            }
            fn capturedPipe(seed: int): int { 40 |> (value => value + seed) }
            fn forward(value: int) { later(value) }
            fn later(value: int) { value + 1 }
            fn inferredGeneric<T>(value: T) { value }
            fn inferOptional(flag: bool) { if flag { 42 } else { null } }
            fn inferNullableReturns(flag: bool) {
                if flag { return null } else { return 42 }
            }
            fn inferResult() { typedResult() }
            fn typedResult(): Result<int, Fault> { Success(42) }
            fn constructResult(): Result<int, Fault> { 42 |> Result.Success }
            fn inferBranch(flag: bool) {
                if flag { return 42 } else { 41 }
            }
            fn inferAllReturn(flag: bool) {
                if flag { return 42 } else { return 41 }
            }
            fn inferEarly() {
                return 42
                let unreachable = true
            }
            fn inferArgumentEarly() {
                pick(if true { return 42 } else { return 41 }, 1 / 0)
            }
            fn inferArgumentReturn() {
                pick(if true { return 42 } else { return 41 }, if true { return 0 } else { 0 })
            }
            fn inferShortCircuit() { false && (1 / 0 > 0) }
            fn deadForward() { deadTarget(if true { return 42 } else { return 41 }) }
            fn deadTarget(value: int) { 1 }
            fn deadCycleFirst() {
                return 42
                deadCycleSecond()
            }
            fn deadCycleSecond() { deadCycleFirst() }
            fn deadChain() {
                return 42
                let x = deadChain()
                let y = x + 1
                y
            }
            fn inferConstantNullable() { if true { 42 } else { null } }
            fn inferNullableFromDeadCall() { if true { 42 } else { nullableLater() } }
            fn nullableLater() { let missing: int? = null
                missing }
            fn literalNever() { if true { return 42 } else { nullableLater() } }
            fn typedNullable(): int? { null }
            fn literalNeverTyped() { if true { return 42 } else { typedNullable() } }
            fn coveredNeverMatch(flag: bool) {
                match flag {
                    _ => if flag { return 42 } else { return 41 }
                    true => coveredNeverMatch(flag)
                }
            }
            fn coveredNullable(flag: bool) {
                match flag {
                    _ => if flag { return 42 } else { return 41 }
                    true => typedNullable()
                }
            }
            fn deadPropIf(): Result<int, string> {
                Success((if true { return Error("x") } else { 42 })?)
            }
            fn deadPropMatch(flag: bool): Result<int, string> {
                Success((match flag {
                    _ => if flag { return Error("x") } else { return Error("y") }
                    true => 42
                })?)
            }
            fn textResultCode(value: Result<int, string>): int {
                match value {
                    Success(number) => number
                    Error(message) => if message == "x" { 42 } else { 41 }
                }
            }
            fn deadRecord() {
                Pair { first = if true { return 42 } else { return 41 }
                       second = deadRecord() }
            }
            fn pairFrom(value: int): Pair { Pair { first = value
                                                  second = 0 } }
            fn deadUpdate() {
                pairFrom(if true { return 42 } else { return 41 }) with { first = deadUpdate() }
            }
            fn genericIdentity<T>(value: T): T { value }
            fn deadGenericArgument() {
                pick(if true { return 42 } else { return 41 }, genericIdentity(deadGenericArgument()) + 1)
            }
            fn deadConditional() {
                if false { let x = deadConditional()
                           x + 1 } else { return 42 }
            }
            fn deadPipe() {
                (if true { return 42 } else { return 41 }) |> deadFactory()
            }
            fn deadFactory() { (x: int) => x + deadPipe() }
            fn guardedCoverage(flag: bool) {
                match flag {
                    true when false => 42
                    true => null
                    false => 0
                }
            }
            fn recursiveKnown(value: int): int {
                if value == 0 { 42 } else { recursiveKnown(value - 1) }
            }
            fn inferUsingKnownRecursion() { recursiveKnown(2) }
            fn run(): bool {
                let local: (int) -> int = value => value + 1
                let absent: int? = null
                let present: int? = 42
                let failure: Result<int, Fault> = Error(Fault.Failed)
                let success: Result<int, Fault> = Success(42)
                let nonNull = absent ?? 40 |> increment
                let chained = 20 |> double |> increment
                let fromLocal = 41 |> local
                let fromLambda = 41 |> (value => value + 1)
                let fromGeneric = 42 |> identity
                let widened = 42 |> ((value: int?) => value ?? 0)
                let glued = 41|>increment
                let precedence = 1 + 19 |> double
                let absentAgain = absent |> optional
                let presentAgain = present |> optional
                (nonNull == 41) && (chained == 41) && (fromLocal == 42) &&
                (fromLambda == 42) && (fromGeneric == 42) && (widened == 42) && (glued == 42) && (precedence == 40) &&
                (localShadow() == 42) && (capturedPipe(2) == 42) && !skip() &&
                (absentAgain == null) && (presentAgain == 42) &&
                (resultCode(failure |> keep) == 7) && (resultCode(success |> keep) == 42) &&
                (forward(41) == 42) && (inferredGeneric<int>(42) == 42) &&
                (inferOptional(true) == 42) && (inferOptional(false) == null) &&
                (inferNullableReturns(true) == null) && (inferNullableReturns(false) == 42) &&
                (resultCode(inferResult()) == 42) && (resultCode(constructResult()) == 42) && (inferBranch(true) == 42) &&
                (inferBranch(false) == 41) && (inferAllReturn(true) == 42) &&
                (inferAllReturn(false) == 41) && (inferEarly() == 42) &&
                (inferArgumentEarly() == 42) && (inferArgumentReturn() == 42) && !inferShortCircuit() &&
                (deadForward() == 42) && (deadCycleFirst() == 42) && (deadCycleSecond() == 42) &&
                (deadChain() == 42) && (inferConstantNullable() == 42) && (inferNullableFromDeadCall() == 42) &&
                (literalNever() == 42) && (literalNeverTyped() == 42) &&
                (coveredNeverMatch(true) == 42) && (coveredNeverMatch(false) == 41) &&
                (coveredNullable(true) == 42) && (coveredNullable(false) == 41) &&
                (textResultCode(deadPropIf()) == 42) && (textResultCode(deadPropMatch(true)) == 42) &&
                (textResultCode(deadPropMatch(false)) == 41) &&
                (deadRecord() == 42) && (deadUpdate() == 42) && (deadGenericArgument() == 42) &&
                (deadConditional() == 42) && (deadPipe() == 42) &&
                (guardedCoverage(true) == null) && (guardedCoverage(false) == 0) &&
                (inferUsingKnownRecursion() == 42)
            }
            """;

        await WithGenerated(source, type =>
        {
            Assert.Equal(true, Invoke(type, "run"));
            Assert.Equal(42, Invoke(type, "runOrder"));
            Assert.Equal(1, Invoke(type, "readSourceCalls"));
            Assert.Equal(1, Invoke(type, "readFactoryCalls"));
            const BindingFlags publicStatic = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            Assert.Equal(typeof(int?), type.GetMethod("inferConstantNullable", publicStatic)!.ReturnType);
            Assert.Equal(typeof(int?), type.GetMethod("inferNullableFromDeadCall", publicStatic)!.ReturnType);
            Assert.Equal(typeof(int), type.GetMethod("literalNever", publicStatic)!.ReturnType);
            Assert.Equal(1, Invoke(type, "readSourceOrder"));
            Assert.Equal(2, Invoke(type, "readFactoryOrder"));
            Assert.Equal(42, Invoke(type, "early"));
            Assert.Equal(1, Invoke(type, "readFactoryCalls"));
        });
        InvalidProgramsAreDiagnosedWithoutGuessing();
    }

    private void InvalidProgramsAreDiagnosedWithoutGuessing()
    {
        var cases = new[]
        {
            "fn convert(value: string): int { 42 }\nfn f(): int { 42 |> convert }",
            "fn f(): int { 42 |> 1 }",
            "fn takes(value: int): int { value }\nfn f(): int { let r: Result<int,int> = Success(42)\n r |> takes }",
            "fn takes(a: int, b: int): int { a + b }\nfn f(): int { 42 |> takes }",
            "fn add(a: int, b: int): int { a + b }\nfn f(): int { 1 |> add(2) }",
            "fn f(): int { 42 |> missing }",
            "fn f(): int { null |> (value => 42) }",
            "fn f() { null }\nfn use(): int? { f() }",
            "fn f() { Success(42) }\nfn use(): Result<int,int> { f() }",
            "fn f() { f() }",
            "fn f() { g() }\nfn g() { f() }",
            "fn f(): int { g() }\nfn g() { f() }",
            "fn f(callback: (int) -> int) { callback(42) }",
            "fn f(callback: (int) -> int) { 42 |> callback }",
            "fn f(flag: bool) { if flag { return 1 } else { return true } }",
            "fn f(flag: bool, value: int?) { if flag { return 1 } else { return value } }",
            "fn pick(first: int, second: int): int { first }\nfn f() { pick(if true { return 42 } else { return 41 }, if true { return true } else { 0 }) }",
            "fn pick(first: int, second: int): int { first }\nfn f(flag: bool) { pick(if flag { return 42 } else { return 41 }, if flag { return null } else { 0 }) }",
            "fn f(flag: bool) { match flag { _ => 42\n true => if flag { return null } else { return null } } }",
            "fn f(flag: bool) { match flag { true => 42\n true => if flag { return null } else { return null }\n false => 0 } }",
            "fn f(value: int?) { match value { null => 42\n null => if true { return null } else { return null }\n present => present } }",
            "type Choice { | Some(bool)\n | None }\nfn f(value: Choice) { match value { Choice.Some(true) => 42\n Choice.Some(false) => 41\n Choice.Some(_) => if true { return null } else { return null }\n Choice.None => 0 } }",
            "fn f() { return 42\n let invalid = true + 1\n invalid }",
            "fn take(a: int, b: int): int { a }\nfn f() { take(if true { return 42 } else { return 41 }, missing()) }",
            "type Pair { first: int\n second: int }\nfn f() { Pair { first = if true { return 42 } else { return 41 }\n second = true + 1 } }",
            "fn f() { (if true { return 42 } else { return 41 }) |> 1 }",
            "fn id(value: int): int { value }\nfn f(): Result<int,string> { Success(id(if true { return Error(\"x\") } else { return Error(\"y\") })?) }",
            "fn f() { if true { return 42 } else { return null } }",
            "fn f() { if false { return null } else { return 42 } }",
            "fn f() { false && (if true { return null } else { true }) }",
            "fn f() { true || (if true { return null } else { false }) }",
            "fn f(): int { let tuple = (1, 2)\n tuple |> (value => value) }",
            "fn use(value: Task<int>): int { 42 }\nfn f(task: Task<int>): int { task |> use }"
        };
        for (var index = 0; index < cases.Length; index++)
        {
            var source = "module Demo\n" + cases[index];
            var failure = Record.Exception(() =>
            {
                var result = DSharpCompiler.Compile(source, "pipe-inference.ds");
                Assert.Null(result.CSharp);
                var diagnostic = Assert.Single(result.Diagnostics);
                Assert.StartsWith("DS", diagnostic.Code);
                Assert.Equal("pipe-inference.ds", diagnostic.Path);
                Assert.InRange(diagnostic.Span.Start, 0, source.Length);
                Assert.InRange(diagnostic.Span.Length, 1, source.Length - diagnostic.Span.Start);
            });
            Assert.True(failure is null, $"Pipe and inference scenario {index}: {failure}");
        }

        // A dead checked-type dependency cycle must not acquire a different
        // meaning merely because its declarations appear in another order.
        const string first = "fn f() { if true { 42 } else { g() ?? 42 } }";
        const string second = "fn g() { if true { null } else { f() } }";
        var forward = DSharpCompiler.Compile("module Demo\n" + first + "\n" + second, "order.ds");
        var reverse = DSharpCompiler.Compile("module Demo\n" + second + "\n" + first, "order.ds");
        Assert.Null(forward.CSharp);
        Assert.Null(reverse.CSharp);
        Assert.Equal("DS2027", Assert.Single(forward.Diagnostics).Code);
        Assert.Equal("DS2027", Assert.Single(reverse.Diagnostics).Code);
    }

    private static object? Invoke(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.Invoke(null, null);

    private static string Instrument(string csharp)
    {
        var source = Assert.Single(Regex.Matches(csharp, @"    public static ([^\r\n(]+) @source\(\)").Cast<Match>());
        csharp = csharp.Replace(source.Value,
            "    private static int __testClock, __testSourceCalls, __testFactoryCalls, __testSourceOrder, __testFactoryOrder;\n" +
            "    public static int @readSourceCalls() => __testSourceCalls;\n" +
            "    public static int @readFactoryCalls() => __testFactoryCalls;\n" +
            "    public static int @readSourceOrder() => __testSourceOrder;\n" +
            "    public static int @readFactoryOrder() => __testFactoryOrder;\n" +
            "    public static " + source.Groups[1].Value + " @source() { __testSourceCalls++; __testSourceOrder = ++__testClock; return @source_original(); }\n" +
            "    public static " + source.Groups[1].Value + " @source_original()", StringComparison.Ordinal);
        var factory = Assert.Single(Regex.Matches(csharp, @"    public static ([^\r\n(]+) @factory\(\)").Cast<Match>());
        return csharp.Replace(factory.Value,
            "    public static " + factory.Groups[1].Value + " @factory() { __testFactoryCalls++; __testFactoryOrder = ++__testClock; return @factory_original(); }\n" +
            "    public static " + factory.Groups[1].Value + " @factory_original()", StringComparison.Ordinal);
    }

    private static async Task WithGenerated(string source, Action<Type> verify)
    {
        var compilation = DSharpCompiler.Compile(source, "pipe-inference.ds");
        Assert.Empty(compilation.Diagnostics);
        Assert.Equal(compilation.CSharp, DSharpCompiler.Compile(source).CSharp);
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-pipe-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "Generated.cs"), Instrument(compilation.CSharp!));
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
            catch (OperationCanceledException) { build.Kill(true); throw new TimeoutException("Generated build timed out."); }
            Assert.True(build.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("pipe-" + Guid.NewGuid(), isCollectible: true);
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
