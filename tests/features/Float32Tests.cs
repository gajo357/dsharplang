using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

public sealed class Float32Tests
{
    public async Task Float32ValuesFollowIeeeSemantics()
    {
        const string source = """
            module Demo

            type Reading { value: float32 }
            type Box<T> { value: T }
            type Label { float32: int }
            type Number { | Measured(float32) | Missing }
            extend int { fn add(self, amount: int): int { self + amount } }
            extend float32 { fn add(self, amount: float32): float32 { self + amount } }

            fn equalNullable<T>(left: T?, right: T?): bool { left == right }
            fn maybe<T>(value: T?): T? { value }
            fn bitsMaximum(): float32 { 340282346638528859811704183484516925440f }
            fn bitsTieEvenLow(): float32 { 1.000000059604644775390625f }
            fn bitsTieEvenHigh(): float32 { 1.000000178813934326171875f }
            fn bitsUnderflow(): float32 { 1e-999999f }
            fn bitsNegativeUnderflow(): float32 { -1e-999999f }
            fn bitsNegativeZero(): float32 { -0f }
            fn bitsSubnormalBelowHalf(): float32 { 0.000000000000000000000000000000000000000000000700649232162408535461864791644958065640130970938257885878534141944895541342930300743319094181060791015624f }
            fn bitsSubnormalHalf(): float32 { 0.000000000000000000000000000000000000000000000700649232162408535461864791644958065640130970938257885878534141944895541342930300743319094181060791015625f }
            fn bitsSubnormalAboveHalf(): float32 { 0.000000000000000000000000000000000000000000000700649232162408535461864791644958065640130970938257885878534141944895541342930300743319094181060791015626f }
            fn bitsSubnormalTieOneTwo(): float32 { 0.000000000000000000000000000000000000000000002101947696487225606385594374934874196920392912814773657635602425834686624028790902229957282543182373046875f }
            fn bitsSubnormalTieThreeFour(): float32 { 0.000000000000000000000000000000000000000000004904544625136859748233053541514706459480916796567805201149738993614268789400512105203233659267425537109375f }
            fn nan(): float32 { 0f / 0f }
            fn infinity(): float32 { 1f / 0f }
            fn negativeInfinity(): float32 { -1f / 0f }
            fn remainder(): float32 { 5f % 3f }
            fn signedRemainder(): float32 { -4f % 2f }
            fn from(mark: (float32) -> float32): float32 { mark(1.5f) + mark(2f) }
            fn classify(value: float32): int {
                match value
                {
                    1.0f => 42
                    1.000000059604644775390625f => 0
                    -0f => 7
                    0.0f => 0
                    -2f => 9
                    _ => 1
                }
            }
            fn deadDuplicate(value: float32) {
                match value
                {
                    1f => if true { return 42 } else { return 41 }
                    1.000000059604644775390625f => null
                    _ => if true { return 0 } else { return 1 }
                }
            }
            fn deadZero(value: float32) {
                match value
                {
                    0f => if true { return 42 } else { return 41 }
                    -0f => null
                    _ => if true { return 0 } else { return 1 }
                }
            }
            fn answer(): bool {
                let nanValue = 0f / 0f
                let zero: float32? = 0f
                let nullValue: float32? = null
                let nullable: float32? = nanValue
                let values: List<float32> = [nanValue]
                let anotherValues: List<float32> = [nanValue]
                let reading = Reading { value = nanValue }
                let anotherReading = Reading { value = nanValue }
                let boxed: Box<float32> = Box { value = nanValue }
                let union: Number = Measured(nanValue)
                let option: Option<float32?> = Some(nanValue)
                let result: Result<float32, int> = Success(nanValue)
                let tuple: (float32, int) = (nanValue, 1)
                let nested: List<Option<float32?>> = [option]
                let map = Map.empty<int, float32>().set(1, nanValue)
                let withValue = Map.empty<int, float32>().set(1, 2f)
                let float32 = 42
                !(nanValue == nanValue) && nanValue != nanValue &&
                !(nanValue < 1f) && !(nanValue <= 1f) && !(nanValue > 1f) && !(nanValue >= 1f) &&
                !(nanValue < nanValue) && !(nanValue <= nanValue) && !(nanValue > nanValue) && !(nanValue >= nanValue) &&
                !(nullable == nullable) && !(values == values) && !(values == anotherValues) &&
                !(reading == reading) && !(reading == anotherReading) && !(boxed == boxed) &&
                !(union == union) && !(option == option) && !(result == result) && !(tuple == tuple) &&
                !(nested == nested) && !(map == map) && !(equalNullable<float32>(nullable, nullable)) &&
                !(equalNullable<List<float32>>(values, values)) &&
                equalNullable<float32>(nullValue, null) && zero == -0f && maybe<float32>(null) == null &&
                maybe<float32?>(zero) == zero && withValue.get(1) == Some(2f) &&
                1.5f + 2f == 3.5f && 3f - 1f == 2f && 1.5f * 2f == 3f && 7.5f / 2.5f == 3f &&
                1_2.5e-1f == 1.25f && 0e+9999999999999999999999f == 0f &&
                0x1f == 31 && 40.add(2) == float32 && 40f.add(2f) == 42f && Label { float32 = 42 }.float32 == 42
            }
            """;
        var oldCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var compiled = DSharpCompiler.Compile(source, "float32.ds");
            Assert.True(compiled.CSharp is not null, string.Join("\n", compiled.Diagnostics));
            Assert.Empty(compiled.Diagnostics);
            await WithGenerated(compiled.CSharp!, "Demo", type =>
            {
                Assert.Equal(true, Method(type, "answer").Invoke(null, null));
                Assert.Equal(0x7f7fffff, Bits(Call<float>(type, "bitsMaximum")));
                Assert.Equal(0x3f800000, Bits(Call<float>(type, "bitsTieEvenLow")));
                Assert.Equal(0x3f800002, Bits(Call<float>(type, "bitsTieEvenHigh")));
                Assert.Equal(0, Bits(Call<float>(type, "bitsUnderflow")));
                Assert.Equal(unchecked((int)0x80000000), Bits(Call<float>(type, "bitsNegativeUnderflow")));
                Assert.Equal(unchecked((int)0x80000000), Bits(Call<float>(type, "bitsNegativeZero")));
                // Exact 2^-150 is the midpoint between zero and the first subnormal.
                // Nearest-even selects 0, 2 and 4 for the three exact midpoint cases.
                Assert.Equal(0, Bits(Call<float>(type, "bitsSubnormalBelowHalf")));
                Assert.Equal(0, Bits(Call<float>(type, "bitsSubnormalHalf")));
                Assert.Equal(1, Bits(Call<float>(type, "bitsSubnormalAboveHalf")));
                Assert.Equal(2, Bits(Call<float>(type, "bitsSubnormalTieOneTwo")));
                Assert.Equal(4, Bits(Call<float>(type, "bitsSubnormalTieThreeFour")));
                Assert.True(float.IsNaN(Call<float>(type, "nan")));
                Assert.True(float.IsPositiveInfinity(Call<float>(type, "infinity")));
                Assert.True(float.IsNegativeInfinity(Call<float>(type, "negativeInfinity")));
                Assert.Equal(2f, Call<float>(type, "remainder"));
                Assert.Equal(unchecked((int)0x80000000), Bits(Call<float>(type, "signedRemainder")));
                Assert.Equal(42, Call<int>(type, "classify", 1f));
                Assert.Equal(7, Call<int>(type, "classify", 0f));
                Assert.Equal(9, Call<int>(type, "classify", -2f));
                Assert.Equal(42, Call<int>(type, "deadDuplicate", 1f));
                Assert.Equal(42, Call<int>(type, "deadZero", 0f));
                var events = new List<float>();
                var from = Method(type, "from");
                var mark = Adapt(from.GetParameters()[0].ParameterType, n => { events.Add(n); return n; });
                Assert.Equal(3.5f, from.Invoke(null, new object[] { mark }));
                Assert.Equal(new[] { 1.5f, 2f }, events);
            });
            await MultiFile();
            await StandaloneEquality();
            RejectInvalid();
        }
        finally { CultureInfo.CurrentCulture = oldCulture; }
    }

    private static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
    private static MethodInfo Method(Type type, string name) => type.GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
    private static T Call<T>(Type type, string name, params object?[] args) => (T)Method(type, name).Invoke(null, args)!;
    private static Delegate Adapt(Type delegateType, Func<float, float> callback)
    {
        var value = Expression.Parameter(typeof(float), "value");
        return Expression.Lambda(delegateType, Expression.Invoke(Expression.Constant(callback), value), value).Compile();
    }

    private static async Task MultiFile()
    {
        var model = new SourceFile("model.ds", """
            module Model
            type Reading { value: float32 }
            fn identity<T>(value: T): T { value }
            fn reading(value: float32): Reading { Reading { value = value } }
            """);
        var app = new SourceFile("app.ds", """
            module App
            import Model
            fn answer(): bool {
                let item = reading(identity<float32>(1.25f))
                let nested: Option<float32?> = Some(item.value)
                item.value == 1.25f && nested == Some(1.25f)
            }
            """);
        var result = DSharpCompiler.Compile(new[] { model, app });
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        await WithGenerated(result.CSharp!, "App", type => Assert.True(Call<bool>(type, "answer")));
    }

    private static async Task StandaloneEquality()
    {
        // No records, unions, lists or maps: generic and tuple equality still
        // has to respect IEEE semantics even when no nominal dispatcher was otherwise needed.
        const string source = """
            module Standalone
            fn nullableSelf<T>(value: T?): bool { value == value }
            fn tupleSelf(value: float32): bool {
                let pair = (value, 1)
                pair == pair
            }
            fn answer(): bool {
                let nanValue = 0f / 0f
                let nullable: float32? = nanValue
                let nothing: float32? = null
                !(nullableSelf<float32>(nullable)) &&
                !(tupleSelf(nanValue)) && nullableSelf<float32>(nothing) && nullableSelf<float32>(0f)
            }
            """;
        var result = DSharpCompiler.Compile(source, "float32-standalone.ds");
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        await WithGenerated(result.CSharp!, "Standalone", type => Assert.True(Call<bool>(type, "answer")));
    }

    private static void RejectInvalid()
    {
        string[] invalid =
        [
            "fn bad(): float32 { 340282346638528859811704183484516925441f }",
            "fn bad(): float32 { 3.40282346638528859811704183484516925441e38f }",
            "fn bad(): float32 { 1e+9999999999999999999999f }",
            "fn bad(): float32 { 1__0f }",
            "fn bad(): float32 { 1_.0f }",
            "fn bad(): float32 { 1._0f }",
            "fn bad(): float32 { 1.0_f }",
            "fn bad(): float32 { 1e+f }",
            "fn bad(): float32 { .5f }",
            "fn bad(): float32 { 1.f }",
            "fn bad(): float32 { 1fL }",
            "fn bad(): float32 { 1.0 }",
            "fn bad(): float32 { 1m }",
            "fn bad(): float32 { 1f + 1 }",
            "fn bad(): float32 { 1 + 1f }",
            "fn bad(): int { 1f }",
            "fn bad(value: float): int { 1 }",
            "type float32 { value: int }\nfn bad(): int { 1 }",
            "fn bad<float32>(value: float32): float32 { value }",
            "fn bad(): Unit { let value = Map.empty<float32, int>()\n () }",
            "fn bad(): Unit { let value = Set.empty<float32?>()\n () }",
            "type Key { value: float32 }\nfn bad(): Unit { let value = Map.empty<Key, int>()\n () }",
            "type Box<T> { value: T }\nfn bad(): Unit { let value = Set.empty<Box<float32>>()\n () }",
            "type Key { value: Option<float32?> }\nfn bad(): Unit { let value = Map.empty<Key, int>()\n () }",
            "type Holder<T> { value: T }\nfn wrap<T>(value: T): Holder<T> { Holder { value = value } }\nfn bad(): Unit { let value = Set.from([wrap<float32>(1f)])\n () }"
        ];
        foreach (var body in invalid)
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + body, "invalid-float32.ds");
            Assert.True(result.CSharp is null, "Expected rejection: " + body);
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, d => Assert.Equal("invalid-float32.ds", d.Path));
        }
    }

    private static async Task WithGenerated(string csharp, string module, Action<Type> verify)
    {
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-float32-" + Guid.NewGuid());
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
            catch (OperationCanceledException) { build.Kill(true); throw new TimeoutException("Generated float32 build timed out."); }
            Assert.True(build.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("float32-" + Guid.NewGuid(), isCollectible: true);
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
