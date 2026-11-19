using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

public sealed class Float64Tests
{
    public async Task FloatValuesFollowIeeeSemantics()
    {
        // These exact integers define the binary64 maximum and half of the
        // smallest subnormal. They also exercise long, bounded source tokens.
        var maximum = ((BigInteger.One << 1024) - (BigInteger.One << 971)).ToString(CultureInfo.InvariantCulture);
        var halfSubnormal = BigInteger.Pow(5, 1075);
        var half = halfSubnormal.ToString(CultureInfo.InvariantCulture) + "e-1075";
        var below = (halfSubnormal - 1).ToString(CultureInfo.InvariantCulture) + "e-1075";
        var above = (halfSubnormal + 1).ToString(CultureInfo.InvariantCulture) + "e-1075";
        var tie12 = (3 * halfSubnormal).ToString(CultureInfo.InvariantCulture) + "e-1075";
        var tie34 = (7 * halfSubnormal).ToString(CultureInfo.InvariantCulture) + "e-1075";
        const string template = """
            module Demo

            type Reading { value: float }
            type Box<T> { value: T }
            type Label { float: int }
            type Number { | Measured(float) | Missing }
            extend int { fn add(self, amount: int): int { self + amount } }
            extend float { fn add(self, amount: float): float { self + amount } }

            fn equalNullable<T>(left: T?, right: T?): bool { left == right }
            fn maybe<T>(value: T?): T? { value }
            fn bitsMaximum(): float { MAXIMUM.0 }
            fn bitsTieEvenLow(): float { 1.00000000000000011102230246251565404236316680908203125 }
            fn bitsTieEvenHigh(): float { 1.00000000000000033306690738754696212708950042724609375 }
            fn bitsUnderflow(): float { 1e-999999 }
            fn bitsNegativeUnderflow(): float { -1e-999999 }
            fn bitsNegativeZero(): float { -0.0 }
            fn bitsSubnormalBelowHalf(): float { BELOW }
            fn bitsSubnormalHalf(): float { HALF }
            fn bitsSubnormalAboveHalf(): float { ABOVE }
            fn bitsSubnormalTieOneTwo(): float { TIE12 }
            fn bitsSubnormalTieThreeFour(): float { TIE34 }
            fn nan(): float { 0.0 / 0.0 }
            fn infinity(): float { 1.0 / 0.0 }
            fn negativeInfinity(): float { -1.0 / 0.0 }
            fn remainder(): float { 5.0 % 3.0 }
            fn signedRemainder(): float { -4.0 % 2.0 }
            fn from(mark: (float) -> float): float { mark(1.5) + mark(2.0) }
            fn classify(value: float): int {
                match value
                {
                    1.0 => 42
                    1.00000000000000011102230246251565404236316680908203125 => 0
                    -0.0 => 7
                    0.0 => 0
                    -2.0 => 9
                    _ => 1
                }
            }
            fn deadDuplicate(value: float) {
                match value
                {
                    1.0 => if true { return 42 } else { return 41 }
                    1.00000000000000011102230246251565404236316680908203125 => null
                    _ => if true { return 0 } else { return 1 }
                }
            }
            fn deadZero(value: float) {
                match value
                {
                    0.0 => if true { return 42 } else { return 41 }
                    -0.0 => null
                    _ => if true { return 0 } else { return 1 }
                }
            }
            fn answer(): bool {
                let nanValue = 0.0 / 0.0
                let zero: float? = 0.0
                let nullValue: float? = null
                let nullable: float? = nanValue
                let values: List<float> = [nanValue]
                let anotherValues: List<float> = [nanValue]
                let reading = Reading { value = nanValue }
                let anotherReading = Reading { value = nanValue }
                let boxed: Box<float> = Box { value = nanValue }
                let union: Number = Measured(nanValue)
                let option: Option<float?> = Some(nanValue)
                let result: Result<float, int> = Success(nanValue)
                let tuple: (float, int) = (nanValue, 1)
                let nested: List<Option<float?>> = [option]
                let map = Map.empty<int, float>().set(1, nanValue)
                let withValue = Map.empty<int, float>().set(1, 2.0)
                let float = 42
                !(nanValue == nanValue) && nanValue != nanValue &&
                !(nanValue < 1.0) && !(nanValue <= 1.0) && !(nanValue > 1.0) && !(nanValue >= 1.0) &&
                !(nanValue < nanValue) && !(nanValue <= nanValue) && !(nanValue > nanValue) && !(nanValue >= nanValue) &&
                !(nullable == nullable) && !(values == values) && !(values == anotherValues) &&
                !(reading == reading) && !(reading == anotherReading) && !(boxed == boxed) &&
                !(union == union) && !(option == option) && !(result == result) && !(tuple == tuple) &&
                !(nested == nested) && !(map == map) && !(equalNullable<float>(nullable, nullable)) &&
                !(equalNullable<List<float>>(values, values)) &&
                equalNullable<float>(nullValue, null) && zero == -0.0 && maybe<float>(null) == null &&
                maybe<float?>(zero) == zero && withValue.get(1) == Some(2.0) &&
                1.5 + 2.0 == 3.5 && 3.0 - 1.0 == 2.0 && 1.5 * 2.0 == 3.0 && 7.5 / 2.5 == 3.0 &&
                1_2.5e-1 == 1.25 && 1e3 == 1000.0 && 0e+9999999999999999999999 == 0.0 &&
                0x1e3 == 483 && 0x1f == 31 && 40.add(2) == float && 40.0.add(2.0) == 42.0 && Label { float = 42 }.float == 42
            }
            """;
        var source = template.Replace("MAXIMUM", maximum, StringComparison.Ordinal)
            .Replace("BELOW", below, StringComparison.Ordinal).Replace("HALF", half, StringComparison.Ordinal)
            .Replace("ABOVE", above, StringComparison.Ordinal).Replace("TIE12", tie12, StringComparison.Ordinal)
            .Replace("TIE34", tie34, StringComparison.Ordinal);
        var oldCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var compiled = DSharpCompiler.Compile(source, "float64.ds");
            Assert.True(compiled.CSharp is not null, string.Join("\n", compiled.Diagnostics));
            Assert.Empty(compiled.Diagnostics);
            await WithGenerated(compiled.CSharp!, "Demo", type =>
            {
                Assert.True(Call<bool>(type, "answer"));
                Assert.Equal(0x7fefffffffffffffL, Bits(Call<double>(type, "bitsMaximum")));
                Assert.Equal(0x3ff0000000000000L, Bits(Call<double>(type, "bitsTieEvenLow")));
                Assert.Equal(0x3ff0000000000002L, Bits(Call<double>(type, "bitsTieEvenHigh")));
                Assert.Equal(0L, Bits(Call<double>(type, "bitsUnderflow")));
                Assert.Equal(long.MinValue, Bits(Call<double>(type, "bitsNegativeUnderflow")));
                Assert.Equal(long.MinValue, Bits(Call<double>(type, "bitsNegativeZero")));
                Assert.Equal(0L, Bits(Call<double>(type, "bitsSubnormalBelowHalf")));
                Assert.Equal(0L, Bits(Call<double>(type, "bitsSubnormalHalf")));
                Assert.Equal(1L, Bits(Call<double>(type, "bitsSubnormalAboveHalf")));
                Assert.Equal(2L, Bits(Call<double>(type, "bitsSubnormalTieOneTwo")));
                Assert.Equal(4L, Bits(Call<double>(type, "bitsSubnormalTieThreeFour")));
                Assert.True(double.IsNaN(Call<double>(type, "nan")));
                Assert.True(double.IsPositiveInfinity(Call<double>(type, "infinity")));
                Assert.True(double.IsNegativeInfinity(Call<double>(type, "negativeInfinity")));
                Assert.Equal(2.0, Call<double>(type, "remainder"));
                Assert.Equal(long.MinValue, Bits(Call<double>(type, "signedRemainder")));
                Assert.Equal(42, Call<int>(type, "classify", 1.0));
                Assert.Equal(7, Call<int>(type, "classify", 0.0));
                Assert.Equal(9, Call<int>(type, "classify", -2.0));
                Assert.Equal(42, Call<int>(type, "deadDuplicate", 1.0));
                Assert.Equal(42, Call<int>(type, "deadZero", 0.0));
                var events = new List<double>();
                var from = Method(type, "from");
                var mark = Adapt(from.GetParameters()[0].ParameterType, n => { events.Add(n); return n; });
                Assert.Equal(3.5, from.Invoke(null, new object[] { mark }));
                Assert.Equal(new[] { 1.5, 2.0 }, events);
            });
            await MultiFile();
            await StandaloneEquality();
            RejectInvalid(maximum);
        }
        finally { CultureInfo.CurrentCulture = oldCulture; }
    }

    private static long Bits(double value) => BitConverter.DoubleToInt64Bits(value);
    private static MethodInfo Method(Type type, string name) => type.GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
    private static T Call<T>(Type type, string name, params object?[] args) => (T)Method(type, name).Invoke(null, args)!;
    private static Delegate Adapt(Type delegateType, Func<double, double> callback)
    {
        var value = Expression.Parameter(typeof(double), "value");
        return Expression.Lambda(delegateType, Expression.Invoke(Expression.Constant(callback), value), value).Compile();
    }

    private static async Task MultiFile()
    {
        var model = new SourceFile("model.ds", """
            module Model
            type Reading { value: float }
            fn identity<T>(value: T): T { value }
            fn reading(value: float): Reading { Reading { value = value } }
            """);
        var app = new SourceFile("app.ds", """
            module App
            import Model
            fn answer(): bool {
                let item = reading(identity<float>(1.25))
                let nested: Option<float?> = Some(item.value)
                item.value == 1.25 && nested == Some(1.25)
            }
            """);
        var result = DSharpCompiler.Compile(new[] { model, app });
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        await WithGenerated(result.CSharp!, "App", type => Assert.True(Call<bool>(type, "answer")));
    }

    private static async Task StandaloneEquality()
    {
        const string source = """
            module Standalone
            fn nullableSelf<T>(value: T?): bool { value == value }
            fn tupleSelf(value: float): bool {
                let pair = (value, 1)
                pair == pair
            }
            fn answer(): bool {
                let nanValue = 0.0 / 0.0
                let nullable: float? = nanValue
                let nothing: float? = null
                !(nullableSelf<float>(nullable)) &&
                !(tupleSelf(nanValue)) && nullableSelf<float>(nothing) && nullableSelf<float>(0.0)
            }
            """;
        var result = DSharpCompiler.Compile(source, "float64-standalone.ds");
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        await WithGenerated(result.CSharp!, "Standalone", type => Assert.True(Call<bool>(type, "answer")));
    }

    private static void RejectInvalid(string maximum)
    {
        var maximumPlusOne = (BigInteger.Parse(maximum, CultureInfo.InvariantCulture) + BigInteger.One).ToString(CultureInfo.InvariantCulture);
        string[] invalid =
        [
            "fn bad(): float { " + maximumPlusOne + ".0 }",
            "fn bad(): float { 1e+9999999999999999999999 }",
            "fn bad(): float { 1__0.0 }",
            "fn bad(): float { 1_.0 }",
            "fn bad(): float { 1._0 }",
            "fn bad(): float { 1.0_ }",
            "fn bad(): float { 1e+ }",
            "fn bad(): float { .5 }",
            "fn bad(): float { 1. }",
            "fn bad(): float { 1.0f }",
            "fn bad(): float { 1e3f }",
            "fn bad(): float { 1.0m }",
            "fn bad(): float { 1 }",
            "fn bad(): float { 0x1e3 }",
            "fn bad(): float { 1.0 + 1 }",
            "fn bad(): float { 1 + 1.0 }",
            "fn bad(): float { 1f + 1.0 }",
            "fn bad(): float { 1m + 1.0 }",
            "fn bad(): int { 1.0 }",
            "type float { value: int }\nfn bad(): int { 1 }",
            "fn bad<float>(value: float): float { value }",
            "fn bad(): Unit { let value = Map.empty<float, int>()\n () }",
            "fn bad(): Unit { let value = Set.empty<float?>()\n () }",
            "type Key { value: float }\nfn bad(): Unit { let value = Map.empty<Key, int>()\n () }",
            "type Box<T> { value: T }\nfn bad(): Unit { let value = Set.empty<Box<float>>()\n () }",
            "type Key { value: Option<float?> }\nfn bad(): Unit { let value = Map.empty<Key, int>()\n () }",
            "type Holder<T> { value: T }\nfn wrap<T>(value: T): Holder<T> { Holder { value = value } }\nfn bad(): Unit { let value = Set.from([wrap<float>(1.0)])\n () }"
        ];
        foreach (var body in invalid)
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + body, "invalid-float64.ds");
            Assert.True(result.CSharp is null, "Expected rejection: " + body);
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, d => Assert.Equal("invalid-float64.ds", d.Path));
        }
    }

    private static async Task WithGenerated(string csharp, string module, Action<Type> verify)
    {
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-float64-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "Generated.cs"), csharp);
            await File.WriteAllTextAsync(Path.Combine(directory, "Generated.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net11.0</TargetFramework><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>");
            await File.WriteAllTextAsync(Path.Combine(directory, "global.json"), "{\"sdk\":{\"version\":\"11.0.100\",\"rollForward\":\"disable\"}}");
            await File.WriteAllTextAsync(Path.Combine(directory, "NuGet.Config"), "<configuration><packageSources><clear/></packageSources></configuration>");
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "build", "Generated.csproj", "--nologo", "--verbosity", "quiet" }) start.ArgumentList.Add(arg);
            using var build = Process.Start(start)!;
            var stdout = build.StandardOutput.ReadToEndAsync();
            var stderr = build.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try { await build.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { build.Kill(true); throw new TimeoutException("Generated float build timed out."); }
            Assert.True(build.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("float64-" + Guid.NewGuid(), isCollectible: true);
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
