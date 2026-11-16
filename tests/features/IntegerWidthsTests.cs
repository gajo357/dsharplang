using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

// A single short-named test keeps the repository's bounded TRX report small.
public sealed class I
{
    private sealed record Width(string Name, string Max, string MinExpression, object Zero, object One, object Two,
        object Maximum, object Minimum, object? MinusOne);

    private static readonly Width[] Widths =
    [
        new("sbyte", "127", "-127 - 1", (sbyte)0, (sbyte)1, (sbyte)2, sbyte.MaxValue, sbyte.MinValue, (sbyte)-1),
        new("byte", "255", "0", (byte)0, (byte)1, (byte)2, byte.MaxValue, byte.MinValue, null),
        new("short", "32767", "-32767 - 1", (short)0, (short)1, (short)2, short.MaxValue, short.MinValue, (short)-1),
        new("ushort", "65535", "0", (ushort)0, (ushort)1, (ushort)2, ushort.MaxValue, ushort.MinValue, null),
        new("int", "2147483647", "-2147483647 - 1", 0, 1, 2, int.MaxValue, int.MinValue, -1),
        new("uint", "4294967295", "0", 0U, 1U, 2U, uint.MaxValue, uint.MinValue, null),
        new("long", "9223372036854775807", "-9223372036854775807 - 1", 0L, 1L, 2L, long.MaxValue, long.MinValue, -1L),
        new("ulong", "18446744073709551615", "0", 0UL, 1UL, 2UL, ulong.MaxValue, ulong.MinValue, null),
    ];

    [Fact(DisplayName = "I")]
    public async Task T()
    {
        var pieces = new List<string> { "module Demo" };
        for (var i = 0; i < Widths.Length; i++)
        {
            var w = Widths[i];
            pieces.Add($$"""
                fn max{{i}}(): {{w.Name}} { {{w.Max}} }
                fn min{{i}}(): {{w.Name}} { {{w.MinExpression}} }
                fn add{{i}}(a: {{w.Name}}, b: {{w.Name}}): {{w.Name}} { a + b }
                fn sub{{i}}(a: {{w.Name}}, b: {{w.Name}}): {{w.Name}} { a - b }
                fn mul{{i}}(a: {{w.Name}}, b: {{w.Name}}): {{w.Name}} { a * b }
                fn div{{i}}(a: {{w.Name}}, b: {{w.Name}}): {{w.Name}} { a / b }
                fn rem{{i}}(a: {{w.Name}}, b: {{w.Name}}): {{w.Name}} { a % b }
                fn neg{{i}}(a: {{w.Name}}): {{w.Name}} { -a }
                fn less{{i}}(a: {{w.Name}}, b: {{w.Name}}): bool { a < b }
                fn literalAdd{{i}}(): {{w.Name}} { 1 + 1 }
                fn constantOverflow{{i}}(): {{w.Name}} { {{w.Max}} + 1 }
                """);
        }
        pieces.Add("""
            type Packet { value: ushort }
            type Label { short: int }
            fn short(): int { 42 }
            fn identifierCompatibility(): int {
                let short = 42
                short
            }
            fn memberCompatibility(): int { Label { short = 42 }.short }
            fn identity<T>(value: T): T { value }
            fn take(value: uint): uint { value }
            fn contexts(): bool {
                let small: byte = 0b1010_1010
                let record = Packet { value = 0xF_F }
                let values: List<ulong> = [0, 18_446_744_073_709_551_615]
                let expected: Option<ulong> = Some(18446744073709551615)
                small == 170 && record.value == 255 && values.get(1) == expected && values.get(1) == Some(18446744073709551615) && Some(18446744073709551615) == values.get(1) && take(0xFFFF_FFFF) == 4294967295 && identity<byte>(42) == 42
            }
            fn hex(): ulong { 0xFFFF_FFFF_FFFF_FFFF }
            fn binary(): uint { 0b1111_1111 }
            fn longSuffix(): long { 8_000_000_000L }
            fn hexLong(): long { 0x7FFF_FFFF_FFFF_FFFFL }
            fn inferredLong() { 123L }
            fn suffixContext() { 1 + 2L }
            fn suffixEquality(): bool { 1 == 2L }
            fn nullableEquality(): bool {
                let n: byte? = 42
                n == 42 && 42 == n && n != 41 && 41 != n
            }
            fn conditional(flag: bool): byte { if flag { 255 } else { 0 } }
            fn mixedLiteral(): bool {
                let n: byte = 41
                n + 1 == 42 && 1 + n == 42 && n == 41 && 41 == n
            }
            fn keys(): bool {
                let table = Map.empty<uint, byte>().set(0xFFFF_FFFF, 42)
                let values = Set.empty<ulong>().add(18446744073709551615).add(1)
                table.get(4294967295) == Some(42) && values.contains(0xFFFF_FFFF_FFFF_FFFF)
            }
            fn byteMatch(value: byte): int { match value { 0xFF => 42
                                                            _ => 0 } }
            fn duplicateMatch(value: byte) { match value { 0xFF => 42
                                                            255 => 0
                                                            _ => 0 } }
            fn signedMatch(value: sbyte): int { match value { -1 => 42
                                                               _ => 0 } }
            fn nullableMatch(value: byte?): int { match value { null => 0
                                                                0xFF => 42
                                                                _ => 1 } }
            fn bigMatch(value: ulong): int { match value { 0xFFFF_FFFF_FFFF_FFFF => 42
                                                           _ => 0 } }
            fn unsignedNegativeMatch(value: uint): int { match value { -1 => 42
                                                                        _ => 0 } }
            fn firstFault(): byte { 255 + 1 }
            fn secondFault(): byte { 1 / 0 }
            fn unsignedNeg(): uint { -1 }
            fn literalRemainderZero(): byte { 1 % 0 }
            fn literalMinimumDivide(): int { (-2147483647 - 1) / -1 }
            fn literalMinimumRemainder(): int { (-2147483647 - 1) % -1 }
            fn firstThenSecond(): byte { firstFault() + secondFault() }
            fn secondThenFirst(): byte { secondFault() + firstFault() }
            fn reachability() {
                match 0x1 { 1 => if true { return 42 } else { return 41 }
                            _ => recursive() }
            }
            fn recursive() { reachability() }
            """);
        var source = string.Join("\n", pieces);
        var compilation = DSharpCompiler.Compile(source, "integers.ds");
        Assert.True(compilation.CSharp is not null, string.Join("\n", compilation.Diagnostics));
        Assert.Empty(compilation.Diagnostics);
        Assert.Equal(compilation.CSharp, DSharpCompiler.Compile(source, "integers.ds").CSharp);
        await RunGenerated(compilation.CSharp!, type =>
        {
            Assert.Equal(true, Call(type, "contexts"));
            Assert.Equal(42, Call(type, "short"));
            Assert.Equal(42, Call(type, "identifierCompatibility"));
            Assert.Equal(42, Call(type, "memberCompatibility"));
            Assert.Equal(ulong.MaxValue, Call(type, "hex"));
            Assert.Equal(255U, Call(type, "binary"));
            Assert.Equal(8_000_000_000L, Call(type, "longSuffix"));
            Assert.Equal(long.MaxValue, Call(type, "hexLong"));
            Assert.Equal(123L, Call(type, "inferredLong"));
            Assert.Equal(3L, Call(type, "suffixContext"));
            Assert.Equal(false, Call(type, "suffixEquality"));
            Assert.Equal(true, Call(type, "nullableEquality"));
            Assert.Equal((byte)255, Call(type, "conditional", true));
            Assert.Equal((byte)0, Call(type, "conditional", false));
            Assert.Equal(true, Call(type, "mixedLiteral"));
            Assert.Equal(true, Call(type, "keys"));
            Assert.Equal(42, Call(type, "byteMatch", (byte)255));
            Assert.Equal(42, Call(type, "duplicateMatch", (byte)255));
            Assert.Equal(42, Call(type, "signedMatch", (sbyte)-1));
            Assert.Equal(42, Call(type, "nullableMatch", (byte)255));
            Assert.Equal(42, Call(type, "bigMatch", ulong.MaxValue));
            Fault<OverflowException>(type, "unsignedNegativeMatch", 1U);
            Fault<OverflowException>(type, "firstThenSecond");
            Fault<DivideByZeroException>(type, "secondThenFirst");
            Fault<OverflowException>(type, "unsignedNeg");
            Fault<DivideByZeroException>(type, "literalRemainderZero");
            Fault<OverflowException>(type, "literalMinimumDivide");
            Fault<OverflowException>(type, "literalMinimumRemainder");
            Assert.Equal(42, Call(type, "reachability"));
            for (var i = 0; i < Widths.Length; i++)
            {
                var w = Widths[i];
                Assert.Equal(w.Maximum, Call(type, "max" + i));
                Assert.Equal(w.Minimum, Call(type, "min" + i));
                Assert.Equal(w.Two, Call(type, "literalAdd" + i));
                Assert.Equal(w.Two, Call(type, "add" + i, w.One, w.One));
                Assert.Equal(w.One, Call(type, "sub" + i, w.Two, w.One));
                Assert.Equal(w.Two, Call(type, "mul" + i, w.One, w.Two));
                Assert.Equal(w.One, Call(type, "div" + i, w.Two, w.Two));
                Assert.Equal(w.Zero, Call(type, "rem" + i, w.Two, w.One));
                Assert.Equal(true, Call(type, "less" + i, w.One, w.Two));
                Fault<OverflowException>(type, "add" + i, w.Maximum, w.One);
                Fault<OverflowException>(type, "sub" + i, w.Minimum, w.One);
                Fault<OverflowException>(type, "mul" + i, w.Maximum, w.Two);
                Fault<OverflowException>(type, "constantOverflow" + i);
                Fault<DivideByZeroException>(type, "div" + i, w.One, w.Zero);
                Fault<DivideByZeroException>(type, "rem" + i, w.One, w.Zero);
                Assert.Equal(w.Zero, Call(type, "neg" + i, w.Zero));
                if (w.MinusOne is not null)
                {
                    Assert.Equal(w.MinusOne, Call(type, "neg" + i, w.One));
                    Fault<OverflowException>(type, "neg" + i, w.Minimum);
                    Fault<OverflowException>(type, "div" + i, w.Minimum, w.MinusOne);
                    Fault<OverflowException>(type, "rem" + i, w.Minimum, w.MinusOne);
                }
                else Fault<OverflowException>(type, "neg" + i, w.One);
            }
        });

        RejectInvalidPrograms();
        await MultiFile();
    }

    private static void RejectInvalidPrograms()
    {
        string[] declarations =
        [
            "fn x(): sbyte { 128 }", "fn x(): byte { 256 }",
            "fn x(): short { 32768 }", "fn x(): ushort { 65536 }",
            "fn x(): int { 2147483648 }", "fn x(): uint { 4294967296 }",
            "fn x(): long { 9223372036854775808 }", "fn x(): ulong { 18446744073709551616 }",
            "fn x(): sbyte { -128 }", "fn x(): short { -32768 }", "fn x(): int { -2147483648 }",
            "fn x(): long { -9223372036854775808 }", "fn x(): int { 0xFFFF_FFFF }",
            "fn x(): int { 1__0 }", "fn x(): int { 0x_FF }", "fn x(): int { 10_ }",
            "fn x(): int { 0b102 }", "fn x(): long { 5_L }", "fn x(): int { 0b_1 }",
            "fn x(): int { 0x }", "fn x(): int { 0b }", "fn x(): int { 1U }",
            "fn x(): byte { 1L }", "fn x(): long { let n = 1\n n }",
            "fn x(a: int, b: long): long { a + b }", "fn x(a: byte, b: short): bool { a < b }",
            "fn x(a: uint, b: ulong): bool { a == b }", "fn x(a: byte): long { a }",
            "fn x() { 2147483648 }",
            "fn x(a: byte): int { match a { 256 => 1\n _ => 0 } }",
            "fn x(): byte { if true { 256 } else { 0 } }",
            "fn x(): List<byte> { [0, 256] }",
        ];
        var accepted = new List<string>();
        foreach (var declaration in declarations)
        {
            var result = DSharpCompiler.Compile("module Demo\n" + declaration, "invalid-integer.ds");
            if (result.CSharp is not null || result.Diagnostics.Count == 0) accepted.Add(declaration);
            Assert.All(result.Diagnostics, d => Assert.StartsWith("DS", d.Code));
        }
        foreach (var width in Widths)
        {
            foreach (var declaration in new[] { $"type {width.Name} {{ value: int }}", $"fn x<{width.Name}>(value: {width.Name}): int {{ 0 }}" })
            {
                var result = DSharpCompiler.Compile("module Demo\n" + declaration, "invalid-integer.ds");
                if (result.CSharp is not null || result.Diagnostics.Count == 0) accepted.Add(declaration);
                Assert.All(result.Diagnostics, d => Assert.StartsWith("DS", d.Code));
            }
        }
        Assert.True(accepted.Count == 0, "Expected D# diagnostics for:\n" + string.Join("\n", accepted));
    }

    private static async Task MultiFile()
    {
        var files = new[]
        {
            new SourceFile("lib.ds", "module Lib\ntype Packet { count: ulong }\nfn count(value: Packet): ulong { value.count }"),
            new SourceFile("app.ds", "module Demo\nimport Lib\nfn run(): ulong { count(Packet { count = 0xFFFF_FFFF_FFFF_FFFF }) }")
        };
        var result = DSharpCompiler.Compile(files);
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        await RunGenerated(result.CSharp!, type => Assert.Equal(ulong.MaxValue, Call(type, "run")));
    }

    private static object? Call(Type type, string name, params object[] args) =>
        type.GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.Invoke(null, args);

    private static void Fault<T>(Type type, string name, params object[] args) where T : Exception
    {
        var failure = Assert.Throws<TargetInvocationException>(() => Call(type, name, args));
        Assert.IsType<T>(failure.InnerException);
    }

    private static async Task RunGenerated(string csharp, Action<Type> verify)
    {
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-integer-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "Generated.cs"), csharp);
            await File.WriteAllTextAsync(Path.Combine(directory, "Generated.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net11.0</TargetFramework><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>");
            await File.WriteAllTextAsync(Path.Combine(directory, "global.json"), "{\"sdk\":{\"version\":\"11.0.100\",\"rollForward\":\"disable\"}}");
            await File.WriteAllTextAsync(Path.Combine(directory, "NuGet.Config"), "<configuration><packageSources><clear/></packageSources></configuration>");
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "build", "Generated.csproj", "--nologo", "--verbosity", "quiet" }) start.ArgumentList.Add(arg);
            using var build = Process.Start(start)!;
            var stdout = build.StandardOutput.ReadToEndAsync();
            var stderr = build.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try { await build.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { build.Kill(true); throw new TimeoutException("Generated build timed out."); }
            Assert.True(build.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("integer-" + Guid.NewGuid(), isCollectible: true);
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
