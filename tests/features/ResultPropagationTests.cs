using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class ResultPropagationTests
{
    [Fact(DisplayName = "Result propagation/evaluation and scope")]
    public async Task EvaluatesOnceAndReturnsOnlyFromNearestFunction()
    {
        const string source = """
            module Demo
            type Fault { | Bad(int) }
            fn successSource(): Result<int, Fault> { Success(40) }
            fn errorSource(): Result<int, Fault> { Error(Fault.Bad(7)) }
            fn add(left: int, right: int): int { left + right }
            fn success(): Result<int, Fault> {
                let number = successSource()?
                Success(number + 2)
            }
            fn failure(): Result<int, Fault> {
                Success(add(errorSource()?, 1 / 0))
            }
            fn inner(): Result<int, Fault> { Success(errorSource()? + 1) }
            fn outer(): Result<int, Fault> {
                let handled = inner()
                match handled {
                    Success(number) => Success(number)
                    Error(Fault.Bad(code)) => Success(code + 35)
                }
            }
            fn code(value: Result<int, Fault>): int {
                match value {
                    Success(number) => number
                    Error(Fault.Bad(error)) => error
                }
            }
            fn runSuccess(): int { code(success()) }
            fn runFailure(): int { code(failure()) }
            fn runOuter(): int { code(outer()) }
            """;
        await WithGenerated(source, type =>
        {
            Assert.Equal(42, Invoke(type, "runSuccess"));
            Assert.Equal(1, Invoke(type, "readSuccessCount"));
            Assert.Equal(7, Invoke(type, "runFailure"));
            Assert.Equal(1, Invoke(type, "readErrorCount"));
            Assert.Equal(42, Invoke(type, "runOuter"));
            Assert.Equal(2, Invoke(type, "readErrorCount"));
        }, csharp =>
        {
            csharp = Instrument(csharp, "successSource", "Success");
            return Instrument(csharp, "errorSource", "Error");
        });
    }

    [Fact(DisplayName = "Result propagation/generic and nullable")]
    public async Task GenericNullableUnitAndOptionPayloadsRemainDistinct()
    {
        const string source = """
            module Demo
            type Fault { | Bad(int) }
            fn copy<T, E>(result: Result<T, E>): Result<T, E> { Success(result?) }
            fn copyNullable<T, E>(result: Result<T?, E>): Result<T?, E> { Success(result?) }
            fn nullableLift<T, E>(result: Result<T, E>): Result<T?, E> { Success(result?) }
            fn get(flag: int): Result<string?, Fault> {
                if flag == 0 { Success(null) } else {
                    if flag == 1 { Success("Alice") } else { Error(Fault.Bad(9)) }
                }
            }
            fn forward(flag: int): Result<string?, Fault> {
                let name = get(flag)?
                Success(name)
            }
            fn classify(value: Result<string?, Fault>): int {
                match value {
                    Success(null) => 1
                    Success(name) => if name == "Alice" { 2 } else { 0 }
                    Error(Fault.Bad(code)) => code
                }
            }
            fn intCode(value: Result<int?, Fault>): int {
                match value {
                    Success(null) => 1
                    Success(number) => number
                    Error(Fault.Bad(code)) => code
                }
            }
            fn nonNullable(): Result<int, Fault> { Success(42) }
            fn genericValue<T>(value: T): Result<T, Fault> { Success(value) }
            fn liftResult(): Result<int?, Fault> { Success(nonNullable()?) }
            fn liftGeneric(): Result<int?, Fault> { Success(genericValue<int>(42)?) }
            fn optionCode(value: Result<Option<string?>, Fault>): int {
                match value {
                    Success(None) => 1
                    Success(Some(null)) => 2
                    Success(Some(name)) => if name == "Alice" { 3 } else { 0 }
                    Error(Fault.Bad(code)) => code
                }
            }
            fn unitCode(value: Result<Unit, Fault>): int {
                match value { Success(()) => 42
                              Error(Fault.Bad(code)) => code }
            }
            fn run(): bool {
                let empty: Result<int?, Fault> = Success(null)
                let zero: Result<int?, Fault> = Success(0)
                let optional: Result<Option<string?>, Fault> = Success(Some(null))
                let noOption: Result<Option<string?>, Fault> = Success(None)
                let unit: Result<Unit, Fault> = Success()
                (classify(forward(0)) == 1) && (classify(forward(1)) == 2) &&
                (classify(forward(2)) == 9) && (intCode(copy(empty)) == 1) &&
                (intCode(copyNullable<int, Fault>(zero)) == 0) &&
                (intCode(copyNullable<int?, Fault>(empty)) == 1) &&
                (intCode(nullableLift<int, Fault>(Success(42))) == 42) &&
                (intCode(nullableLift<int?, Fault>(empty)) == 1) &&
                (intCode(nullableLift<int?, Fault>(zero)) == 0) &&
                (intCode(liftResult()) == 42) && (intCode(liftGeneric()) == 42) &&
                (optionCode(copy(optional)) == 2) && (optionCode(copy(noOption)) == 1) &&
                (unitCode(copy(unit)) == 42)
            }
            """;
        await NullableAcceptanceRuntime.WithGenerated(source, type => Assert.Equal(true, NullableAcceptanceRuntime.Invoke(type, "run")));
    }

    [Fact(DisplayName = "Result propagation/nested expressions")]
    public async Task NestedExpressionsAndShortCircuitRespectPrecedence()
    {
        const string source = """
            module Demo
            type Fault { | Bad(int) }
            type Box { value: int }
            fn make(): Result<int, Fault> { Success(5) }
            fn makeBox(): Result<Box, Fault> { Success(Box { value = 6 }) }
            fn fail(): Result<int, Fault> { Error(Fault.Bad(7)) }
            fn direct(): Result<int, Fault> { Success(Success(42)?) }
            fn directFailure(): Result<int, Fault> { Success(Error(Fault.Bad(7))?) }
            fn directLocal(): Result<int, Fault> {
                let number = Success(42)?
                Success(number)
            }
            fn qualifiedLocal(): Result<int, Fault> {
                let number = Result.Success(42)?
                Success(number)
            }
            fn unitLocal(): Result<Unit, Fault> {
                let value = Success()?
                Success(value)
            }
            fn layered(): Result<Result<int, Fault>, Fault> { Success(make()) }
            fn layeredError(): Result<Result<int, Fault>, Fault> { Success(fail()) }
            fn nestedPropagation(): Result<int, Fault> { Success((layered()?)? + 37) }
            fn nestedPropagationError(): Result<int, Fault> { Success((layeredError()?)?) }
            fn nested(): Result<int, Fault> { Success(make()? + 2 * (makeBox()?).value + 25) }
            fn nullable(flag: bool): Result<int?, Fault> {
                if flag { Success(null) } else { Error(Fault.Bad(7)) }
            }
            fn fallback(flag: bool): Result<int, Fault> { Success(nullable(flag)? ?? 42) }
            fn guarded(): Result<int, Fault> {
                match true {
                    true when fail()? > 0 => Success(42)
                    _ => Success(0)
                }
            }
            fn nestedReturn(): Result<int, Fault> {
                Success((if true { return Error(Fault.Bad(11)) } else { fail() })?)
            }
            fn bottom(flag: bool): Result<int, Fault> {
                Success((if flag { return Error(Fault.Bad(12)) } else { return Error(Fault.Bad(13)) })?)
            }
            fn bottomMatch(flag: bool): Result<int, Fault> {
                Success((match flag {
                    true => if flag { return Error(Fault.Bad(14)) } else { return Error(Fault.Bad(15)) }
                    false => if flag { return Error(Fault.Bad(16)) } else { return Error(Fault.Bad(17)) }
                })?)
            }
            fn skip(flag: bool): Result<bool, Fault> {
                Success(flag && (fail()? > 0))
            }
            fn choose(flag: bool): Result<int, Fault> {
                Success(if flag { (make())? + 37 } else { fail()? })
            }
            fn code(value: Result<int, Fault>): int {
                match value { Success(number) => number
                              Error(Fault.Bad(error)) => error }
            }
            fn boolCode(value: Result<bool, Fault>): int {
                match value { Success(true) => 1
                              Success(false) => 0
                              Error(Fault.Bad(error)) => error }
            }
            fn unitCode(value: Result<Unit, Fault>): int {
                match value { Success(()) => 42
                              Error(Fault.Bad(error)) => error }
            }
            fn run(): bool {
                (code(direct()) == 42) && (code(directFailure()) == 7) && (code(nested()) == 42) &&
                (code(directLocal()) == 42) && (code(qualifiedLocal()) == 42) && (unitCode(unitLocal()) == 42) &&
                (code(nestedPropagation()) == 42) && (code(nestedPropagationError()) == 7) &&
                (code(choose(true)) == 42) && (code(choose(false)) == 7) &&
                (code(fallback(true)) == 42) && (code(fallback(false)) == 7) &&
                (code(guarded()) == 7) && (code(nestedReturn()) == 11) &&
                (code(bottom(true)) == 12) && (code(bottom(false)) == 13) &&
                (code(bottomMatch(true)) == 14) && (code(bottomMatch(false)) == 17) &&
                (boolCode(skip(false)) == 0) && (boolCode(skip(true)) == 7)
            }
            """;
        await NullableAcceptanceRuntime.WithGenerated(source, type => Assert.Equal(true, NullableAcceptanceRuntime.Invoke(type, "run")));
    }

    [Theory(DisplayName = "Result propagation/invalid")]
    [InlineData("operand and return", 0, 4)]
    [InlineData("incompatible error and names", 5, 11)]
    public void InvalidProgramsProduceSourceDiagnostics(string group, int first, int last)
    {
        for (var id = first; id <= last; id++)
        {
            var failure = Record.Exception(() =>
            {
                var body = id switch
                {
                    0 => "fn f(value: int): Result<int, int> { Success(value?) }",
                    1 => "fn f(value: int?): Result<int, int> { Success(value?) }",
                    2 => "fn f(value: Option<int>): Result<int, int> { Success(value?) }",
                    3 => "fn f(value: Result<int, int>): int { value? }",
                    4 => "fn f(value: Result<int, int>): Unit { let n = value?\n () }",
                    5 => "fn f(value: Result<int, string>): Result<int, int> { Success(value?) }",
                    6 => "type First { | Bad }\ntype Second { | Bad }\nfn f(value: Result<int, First>): Result<int, Second> { Success(value?) }",
                    7 => "type Result<T, E> { | Ok(T)\n | Fail(E) }\nfn f(value: Result<int, int>): Result<int, int> { Result.Ok(value?) }",
                    8 => "fn f<T, E, F>(value: Result<T, E>): Result<T, F> { Success(value?) }",
                    9 => "fn f(value: Result<int, int>?): Result<int, int> { Success(value?) }",
                    10 => "fn f(): Result<int, int> { let value = Error(7)?\n Success(42) }",
                    11 => "fn consume(value: int): int { value }\nfn f(flag: bool): Result<int, int> { Success(consume(if flag { return Error(1) } else { return Error(2) })?) }",
                    _ => throw new ArgumentOutOfRangeException(nameof(id))
                };
                var source = "module Demo\n" + body;
                var result = DSharpCompiler.Compile(source, "propagation.ds");
                Assert.Null(result.CSharp);
                var error = Assert.Single(result.Diagnostics);
                Assert.StartsWith("DS", error.Code);
                Assert.Equal("propagation.ds", error.Path);
                Assert.InRange(error.Span.Start, 0, source.Length);
                Assert.InRange(error.Span.Length, 1, source.Length - error.Span.Start);
                Assert.True(error.Span.Line >= 1);
                Assert.True(error.Span.Column >= 1);
            });
            Assert.True(failure is null, $"Result propagation {group} scenario {id}: {failure}");
        }
    }

    private static string Instrument(string csharp, string name, string counter)
    {
        var match = Assert.Single(Regex.Matches(csharp, @"    public static ([^\r\n(]+) @" + name + @"\(\)").Cast<Match>());
        var resultType = match.Groups[1].Value;
        return csharp.Replace(match.Value,
            "    private static int __test" + counter + "Count;\n" +
            "    public static int @read" + counter + "Count() => __test" + counter + "Count;\n" +
            "    public static " + resultType + " @" + name + "() { __test" + counter + "Count++; return @" + name + "_original(); }\n" +
            "    public static " + resultType + " @" + name + "_original()", StringComparison.Ordinal);
    }

    private static object? Invoke(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.Invoke(null, null);

    private static async Task WithGenerated(string source, Action<Type> verify, Func<string, string> transform)
    {
        var compilation = DSharpCompiler.Compile(source, "propagation.ds");
        Assert.Empty(compilation.Diagnostics);
        Assert.Equal(compilation.CSharp, DSharpCompiler.Compile(source).CSharp);
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-propagation-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "Generated.cs"), transform(compilation.CSharp!));
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
            catch (OperationCanceledException) { build.Kill(true); throw new TimeoutException("Generated build timed out."); }
            Assert.True(build.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("propagation-" + Guid.NewGuid(), isCollectible: true);
            try
            {
                var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0/Generated.dll"));
                verify(assembly.GetType("DSharp.Generated.Demo")!);
            }
            finally { context.Unload(); }
        }
        finally { Directory.Delete(directory, true); }
    }
}
