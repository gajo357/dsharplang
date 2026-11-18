using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

public sealed class DecimalTests
{
    [Fact]
    public async Task DecimalValuesExecuteAndInvalidProgramsAreDiagnosed()
    {
        const string source = """
            module Demo
            type Price { amount: decimal }
            type Box<T> { value: T }
            type Label { decimal: int }
            type Choice { | Exact(decimal) | Missing }
            extend int { fn add(self, amount: int): int { self + amount } }
            extend decimal { fn add(self, amount: decimal): decimal { self + amount } }
            fn identity<T>(value: T): T { value }
            fn maybe<T>(value: T?): T? { value }
            fn sum(mark: (decimal)->decimal): decimal { mark(1.25m) + mark(2.75m) }
            fn overflow(value: decimal): decimal { value + 1m }
            fn divide(left: decimal, right: decimal): decimal { left / right }
            fn remainder(left: decimal, right: decimal): decimal { left % right }
            fn constantOverflow(): decimal { 79228162514264337593543950335m + 1m }
            fn constantDivide(): decimal { 1m / 0m }
            fn classify(value: decimal): int {
                match value
                {
                    1.0m => 42
                    1.00m => 0
                    -0.0m => 7
                    0m => 0
                    -2m => 9
                    2m => 10
                    _ => 1
                }
            }
            fn deadDuplicate(value: decimal) {
                match value
                {
                    1.00m => if true { return 42 } else { return 41 }
                    1.0m => null
                    _ => if true { return 0 } else { return 1 }
                }
            }
            fn deadZero(value: decimal) {
                match value
                {
                    0.00m => if true { return 42 } else { return 41 }
                    -0.0m => null
                    _ => if true { return 0 } else { return 1 }
                }
            }
            fn singleton() {
                match 1.00m
                {
                    1m => if true { return 42 } else { return 41 }
                    _ => null
                }
            }
            fn singletonZero() {
                match -0.0m
                {
                    0m => if true { return 42 } else { return 41 }
                    _ => null
                }
            }
            fn answer(): bool {
                let max: decimal = 79228162514264337593543950335m
                let maxWithZero: decimal = 79228162514264337593543950335.0m
                let smallest: decimal = 0.0000000000000000000000000001m
                let smallestWithZero: decimal = 0.00000000000000000000000000010m
                let nullValue: decimal? = null
                let present: decimal? = 1.0m
                let someNull: Option<decimal?> = Some(null)
                let someValue: Option<decimal?> = Some(1.00m)
                let missing: Option<decimal?> = None
                let box: Box<decimal?> = Box { value = null }
                let price = Price { amount = 1.0m }
                let equalPrice = Price { amount = 1.00m }
                let table = Map.empty<decimal, string>().set(1.0m, "found")
                let prices = Map.empty<Price, int>().set(price, 42)
                let values = Set.empty<decimal>().add(1.0m).add(1.00m)
                let decimal = 42
                let items: List<decimal> = [1.0m, 2.00m]
                let choices: List<Option<decimal?>> = [missing, someNull, someValue]
                let result = match choices.get(2)
                {
                    Some(Some(null)) => false
                    Some(Some(value)) => value == 1m
                    Some(None) => false
                    None => false
                }
                max == maxWithZero && smallest == smallestWithZero &&
                1e-28m == smallest && 1_2.3_4e-1m == 1.234m && 1.0E+3m == 1000m &&
                0e+9999999999999999999999m == 0m && 0e-9999999999999999999999m == 0m &&
                -1.5m + 3.0m == 1.5m && 3.5m - 1.5m == 2m && 1.5m * 2m == 3m &&
                7.5m / 2.5m == 3m && 7.5m % 2m == 1.5m && 1m / 6m == 0.1666666666666666666666666667m &&
                1.0m == 1.00m && 1m != 2m && 1m < 2m && 2m > 1m && 1m <= 1m && 2m >= 2m &&
                identity<decimal>(1.25m) == 1.25m && maybe<decimal>(null) == null &&
                maybe<decimal?>(present) == present && nullValue == null && box.value == null && someNull != missing &&
                someValue == Some(1m) && price == equalPrice && Exact(1m) == Exact(1.0m) &&
                items == [1.00m, 2m] && table.get(1.00m) == Some("found") &&
                prices.get(equalPrice) == Some(42) && values.contains(1m) && values.count() == 1 &&
                result && 40.add(2) == decimal && 40m.add(2m) == 42m && Label { decimal = 42 }.decimal == 42
            }
            """;
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var compiled = DSharpCompiler.Compile(source, "decimal.ds");
            Assert.True(compiled.CSharp is not null, string.Join("\n", compiled.Diagnostics));
            Assert.Empty(compiled.Diagnostics);
            await WithGenerated(compiled.CSharp!, "Demo", type =>
            {
                Assert.Equal(true, Method(type, "answer").Invoke(null, null));
                Assert.Equal(42, Method(type, "classify").Invoke(null, new object[] { 1.00m }));
                Assert.Equal(7, Method(type, "classify").Invoke(null, new object[] { 0m }));
                Assert.Equal(9, Method(type, "classify").Invoke(null, new object[] { -2m }));
                Assert.Equal(10, Method(type, "classify").Invoke(null, new object[] { 2m }));
                Assert.Equal(42, Method(type, "deadDuplicate").Invoke(null, new object[] { 1m }));
                Assert.Equal(42, Method(type, "deadZero").Invoke(null, new object[] { 0m }));
                Assert.Equal(42, Method(type, "singleton").Invoke(null, null));
                Assert.Equal(42, Method(type, "singletonZero").Invoke(null, null));
                var events = new List<decimal>();
                var sum = Method(type, "sum");
                var mark = Adapt(sum.GetParameters()[0].ParameterType, n => { events.Add(n); return n; });
                Assert.Equal(4m, sum.Invoke(null, new object[] { mark }));
                Assert.Equal(new[] { 1.25m, 2.75m }, events);
                Assert.IsType<OverflowException>(Assert.Throws<TargetInvocationException>(() => Method(type, "overflow").Invoke(null, new object[] { decimal.MaxValue })).InnerException);
                Assert.IsType<DivideByZeroException>(Assert.Throws<TargetInvocationException>(() => Method(type, "divide").Invoke(null, new object[] { 1m, 0m })).InnerException);
                Assert.IsType<DivideByZeroException>(Assert.Throws<TargetInvocationException>(() => Method(type, "remainder").Invoke(null, new object[] { 1m, 0m })).InnerException);
                Assert.IsType<OverflowException>(Assert.Throws<TargetInvocationException>(() => Method(type, "constantOverflow").Invoke(null, null)).InnerException);
                Assert.IsType<DivideByZeroException>(Assert.Throws<TargetInvocationException>(() => Method(type, "constantDivide").Invoke(null, null)).InnerException);
            });
            await MultiFile();
            RejectInvalid();
        }
        finally { CultureInfo.CurrentCulture = originalCulture; }
    }

    private static async Task MultiFile()
    {
        var model = new SourceFile("model.ds", """
            module Model
            type Price { amount: decimal }
            fn identity<T>(value: T): T { value }
            fn price(value: decimal): Price { Price { amount = value } }
            fn nested(value: decimal?): Option<decimal?> { Some(value) }
            """);
        var app = new SourceFile("app.ds", """
            module App
            import Model
            fn answer(): bool {
                let price: Price = price(1.0m)
                let list: List<Option<decimal?>> = [nested(null), nested(identity<decimal?>(1.00m))]
                price.amount == 1m && list.get(0) == Some(Some(null)) && list.get(1) == Some(Some(1m))
            }
            """);
        var result = DSharpCompiler.Compile(new[] { model, app });
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        await WithGenerated(result.CSharp!, "App", type => Assert.Equal(true, Method(type, "answer").Invoke(null, null)));
    }

    private static void RejectInvalid()
    {
        string[] invalid =
        [
            "fn bad(): decimal { 79228162514264337593543950336m }",
            "fn bad(): decimal { -79228162514264337593543950336m }",
            "fn bad(): decimal { 79228162514264337593543950335.1m }",
            "fn bad(): decimal { 1e-29m }",
            "fn bad(): decimal { 1.2e-28m }",
            "fn bad(): decimal { 1e+9999999999999999999999m }",
            "fn bad(): decimal { 1e-9999999999999999999999m }",
            "fn bad(): decimal { 1__0m }",
            "fn bad(): decimal { 1_.0m }",
            "fn bad(): decimal { 1._0m }",
            "fn bad(): decimal { 1.0_m }",
            "fn bad(): decimal { 1m_ }",
            "fn bad(): decimal { 1.0e_m }",
            "fn bad(): decimal { 1e+m }",
            "fn bad(): decimal { 1mL }",
            "fn bad(): decimal { 0x1m }",
            "fn bad(): decimal { .5m }",
            "fn bad(): decimal { 1.m }",
            "fn bad(): decimal { 1.0 }",
            "fn bad(): decimal { 1.0f }",
            "fn bad(): decimal { 1m + 1 }",
            "fn bad(): bool { 1m == 1 }",
            "fn bad(): decimal { 1 }",
            "fn bad(): int { 1m }",
            "fn bad(): long { 1m }",
            "type decimal { value: int }\nfn bad(): int { 1 }",
            "fn bad<decimal>(value: decimal): decimal { value }"
        ];
        foreach (var body in invalid)
        {
            var input = "module Invalid\n" + body;
            var result = DSharpCompiler.Compile(input, "invalid-decimal.ds");
            Assert.True(result.CSharp is null, "Expected rejection: " + body);
            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.StartsWith("DS", diagnostic.Code);
            Assert.Equal("invalid-decimal.ds", diagnostic.Path);
            Assert.InRange(diagnostic.Span.Start, 0, input.Length - 1);
            Assert.InRange(diagnostic.Span.Length, 1, input.Length - diagnostic.Span.Start);
        }
    }

    private static MethodInfo Method(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;

    private static Delegate Adapt(Type delegateType, Func<decimal,decimal> callback)
    {
        var value = Expression.Parameter(typeof(decimal), "value");
        return Expression.Lambda(delegateType, Expression.Invoke(Expression.Constant(callback), value), value).Compile();
    }

    private static async Task WithGenerated(string csharp, string module, Action<Type> verify)
    {
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-decimal-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "Generated.cs"), csharp);
            await File.WriteAllTextAsync(Path.Combine(directory, "Generated.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net11.0</TargetFramework><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>");
            await File.WriteAllTextAsync(Path.Combine(directory, "global.json"), "{\"sdk\":{\"version\":\"11.0.100\",\"rollForward\":\"disable\"}}");
            await File.WriteAllTextAsync(Path.Combine(directory, "NuGet.Config"), "<configuration><packageSources><clear/></packageSources></configuration>");
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "build", "Generated.csproj", "--nologo", "--verbosity", "quiet" }) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated decimal build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("decimal-" + Guid.NewGuid(), isCollectible: true);
            try
            {
                var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0/Generated.dll"));
                verify(Assert.Single(assembly.GetTypes(), candidate => candidate.Name == module));
            }
            finally { context.Unload(); }
        }
        finally { Directory.Delete(directory, true); }
    }
}
