using System.Linq.Expressions;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class ForAcceptanceTests
{
    [Fact(DisplayName = "Sequential for/in")]
    public async Task IteratesAndEnforcesScopeAndTypes()
    {
        // This mirrors the runnable example, and adds callbacks so the host can
        // observe iteration order, source evaluation and escaped closures.
        const string source = """
            module Demo
            type Fault { | Bad(int) }
            fn numbers(): List<int> { [1, 2, 3] }
            fn empty(): List<int> { [] }
            fn visit(factory: () -> List<int>, action: (int) -> Unit): Unit {
                for number in factory() { action(number) }
            }
            fn closures(seed: int, keep: (() -> int) -> Unit): Unit {
                for number in [1, 2, 3] { keep(() => number + seed) }
            }
            fn firstPositive(values: List<int>): Option<int> {
                for value in values {
                    if value > 0 { return Some(value) }
                }
                None
            }
            fn first<T>(values: List<T>): Option<T> {
                for value in values { return Some(value) }
                None
            }
            fn classify(value: Option<int>): int {
                match value { None => 0
                              Some(number) => number }
            }
            fn nested(): int {
                for row in [[1, 2], [3, 4]] {
                    for value in row {
                        if value == 3 { return value }
                    }
                }
                0
            }
            fn inferred() {
                for value in [1, 42] {
                    if value == 42 { return value }
                }
                0
            }
            fn lambdaBoundary(): int {
                let find: () -> int = () => {
                    for value in [1, 41] {
                        if value == 41 { return value }
                    }
                    0
                }
                find() + 1
            }
            fn check(value: int): Result<Unit, Fault> {
                if value == 2 { Error(Fault.Bad(value)) } else { Success() }
            }
            fn propagate(values: List<int>): Result<int, Fault> {
                for value in values {
                    let checked = check(value)?
                    ()
                }
                Success(42)
            }
            fn resultCode(value: Result<int, Fault>): int {
                match value { Success(number) => number
                              Error(Fault.Bad(number)) => number }
            }
            fn unitValue(): Unit {
                let value: Unit = for number in [1] { () }
                value
            }
            fn run(): bool {
                let empty: List<int> = []
                (classify(firstPositive(empty)) == 0) &&
                (classify(first<int>(empty)) == 0) &&
                (classify(first<int>([42, 7])) == 42) &&
                (classify(firstPositive([-2, 7, 8])) == 7) &&
                (classify(firstPositive([-2, -1])) == 0) &&
                (nested() == 3) &&
                (inferred() == 42) && (lambdaBoundary() == 42) &&
                (resultCode(propagate([1, 2, 3])) == 2) &&
                (resultCode(propagate([1, 3])) == 42)
            }
            """;
        await NullableAcceptanceRuntime.WithGenerated(source, type =>
        {
            Assert.Equal(true, NullableAcceptanceRuntime.Invoke(type, "run"));
            Assert.NotNull(NullableAcceptanceRuntime.Invoke(type, "unitValue"));

            var visit = Method(type, "visit");
            var list = NullableAcceptanceRuntime.Invoke(type, "numbers")!;
            var empty = NullableAcceptanceRuntime.Invoke(type, "empty")!;
            var calls = 0;
            var visited = new List<int>();
            var factoryType = visit.GetParameters()[0].ParameterType;
            var actionType = visit.GetParameters()[1].ParameterType;
            var factory = Factory(factoryType, () => { calls++; return list; });
            var action = Callback(actionType, value => visited.Add((int)value));
            visit.Invoke(null, new object[] { factory, action });
            Assert.Equal(1, calls);
            Assert.Equal(new[] { 1, 2, 3 }, visited);

            visited.Clear();
            var emptyFactory = Factory(factoryType, () => { calls++; return empty; });
            visit.Invoke(null, new object[] { emptyFactory, action });
            Assert.Equal(2, calls);
            Assert.Empty(visited);

            var throwingFactory = Factory(factoryType, () => throw new DivideByZeroException());
            var error = Assert.Throws<TargetInvocationException>(() =>
                visit.Invoke(null, new object[] { throwingFactory, action }));
            Assert.IsType<DivideByZeroException>(error.InnerException);
            Assert.Empty(visited);

            var closures = new List<Delegate>();
            var collect = Method(type, "closures");
            var capture = Callback(collect.GetParameters()[1].ParameterType,
                value => closures.Add((Delegate)value));
            collect.Invoke(null, new object[] { 10, capture });
            Assert.Equal(3, closures.Count);
            Assert.Equal(new object?[] { 11, 12, 13 }, closures.Select(closure => closure.DynamicInvoke()).ToArray());
        });

        var invalid = new[]
        {
            "fn f(): Unit { for value in 42 { () } }",
            "fn f(values: List<int>?): Unit { for value in values { () } }",
            "fn f(): Unit { for value in [1] { value } }",
            "fn f(): int { for value in [1] { () }\n value }",
            "fn f(value: int): Unit { for value in [1] { () } }",
            "fn f(): Unit { for value in [1] { break } }",
            "fn f(): Unit { for value in [1] { continue } }"
        };
        foreach (var body in invalid)
        {
            var result = DSharpCompiler.Compile("module Demo\n" + body, "for-invalid.ds");
            Assert.True(result.CSharp is null, $"Invalid loop unexpectedly compiled: {body}");
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, error => Assert.Equal("for-invalid.ds", error.Path));
        }

        var modules = DSharpCompiler.Compile(new[]
        {
            new SourceFile("lib.ds", "module Lib\nfn item(): int { 42 }"),
            new SourceFile("app.ds", """
                module App
                import Lib
                fn answer(): int {
                    for item in [item()] {
                        if item == 42 { return item }
                    }
                    0
                }
                """)
        });
        Assert.Empty(modules.Diagnostics);
        Assert.NotNull(modules.CSharp);
        await ExecuteModules(modules.CSharp!);
    }

    private static MethodInfo Method(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;

    private static Delegate Factory(Type type, Func<object> create)
    {
        var body = Expression.Convert(Expression.Invoke(Expression.Constant(create)), type.GetMethod("Invoke")!.ReturnType);
        return Expression.Lambda(type, body).Compile();
    }

    private static Delegate Callback(Type type, Action<object> action)
    {
        var invoke = type.GetMethod("Invoke")!;
        var value = Expression.Parameter(invoke.GetParameters()[0].ParameterType, "value");
        var body = Expression.Block(
            Expression.Invoke(Expression.Constant(action), Expression.Convert(value, typeof(object))),
            Expression.Default(invoke.ReturnType));
        return Expression.Lambda(type, body, value).Compile();
    }

    private static async Task ExecuteModules(string csharp)
    {
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-for-modules-" + Guid.NewGuid());
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
            catch (OperationCanceledException) { build.Kill(true); throw new TimeoutException("Multi-file loop build timed out."); }
            Assert.True(build.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("for-multifile-" + Guid.NewGuid(), isCollectible: true);
            try
            {
                var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0/Generated.dll"));
                var app = Assert.Single(assembly.GetTypes(), candidate => candidate.Name == "App");
                Assert.Equal(42, Method(app, "answer").Invoke(null, null));
            }
            finally { context.Unload(); }
        }
        finally { Directory.Delete(directory, true); }
    }
}
