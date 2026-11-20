using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

public sealed class FloatInterpolationTests
{
    public async Task FloatInterpolationPreservesValuesOrderAndCulture()
    {
        const string source = """
            module Demo
            type Reading { value: float }

            fn format32(value: float32): string { $"{value}" }
            fn format64(value: float): string { $"{value}" }
            fn mixed(value: float32, reading: Reading): string { $"{42}|{true}|{value}|{reading.value}|{'X'}" }
            fn mixedAnswer(): string { mixed(1.25f, Reading { value = 1000.0 }) }
            fn computed(): string { $"{1.5 + 2.0}|{1.5f + 2.0f}" }
            fn ordered(mark: (float) -> float): string {
                $"{mark(1.25)}|{mark(2.5)}|{mark(3.0)}"
            }
            fn early(mark: (float) -> float): string {
                $"{mark(1.0)}|{if true { return "early" } else { 2.0 }}|{mark(3.0)}"
            }
            async fn delayed(mark: (float) -> float, pending: Task<float>): string {
                $"{mark(1.25)}|{await pending}|{mark(3.5)}"
            }
            """;
        var result = DSharpCompiler.Compile(source, "float-interpolation.ds");
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        await WithGenerated(result.CSharp!, async type =>
        {
            var previousCulture = CultureInfo.CurrentCulture;
            try
            {
                foreach (var culture in new[] { "en-US", "fr-FR", "ar-EG" })
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                    foreach (var (value, expected) in new (float, string)[]
                    {
                        (0f, "0.0"), (BitConverter.Int32BitsToSingle(unchecked((int)0x80000000)), "-0.0"),
                        (1f, "1.0"), (-1.5f, "-1.5"), (100f, "100.0"), (1000f, "1.0e3"),
                        (0.001f, "0.001"), (0.0001f, "0.0001"), (0.00001f, "1.0e-5"),
                        (1.2345678f, "1.2345678"), (float.Epsilon, "1.0e-45"),
                        (float.MaxValue, "3.4028235e38"), (-float.MaxValue, "-3.4028235e38"),
                        (float.NaN, "NaN"), (float.PositiveInfinity, "Infinity"), (float.NegativeInfinity, "-Infinity")
                    })
                        Assert.Equal(expected, Call<string>(type, "format32", value));
                    foreach (var (value, expected) in new (double, string)[]
                    {
                        (0d, "0.0"), (BitConverter.Int64BitsToDouble(long.MinValue), "-0.0"),
                        (1d, "1.0"), (-1.5d, "-1.5"), (100d, "100.0"), (1000d, "1.0e3"),
                        (0.001d, "0.001"), (0.0001d, "0.0001"), (0.00001d, "1.0e-5"),
                        (1.2345678901234567d, "1.2345678901234567"), (double.Epsilon, "5.0e-324"),
                        (double.MaxValue, "1.7976931348623157e308"), (-double.MaxValue, "-1.7976931348623157e308"),
                        (double.NaN, "NaN"), (double.PositiveInfinity, "Infinity"), (double.NegativeInfinity, "-Infinity")
                    })
                        Assert.Equal(expected, Call<string>(type, "format64", value));
                    Assert.Equal("3.5|3.5", Call<string>(type, "computed"));
                    Assert.Equal("42|true|1.25|1.0e3|X", Call<string>(type, "mixedAnswer"));
                }
            }
            finally { CultureInfo.CurrentCulture = previousCulture; }

            var events = new List<double>();
            var callbackType = Method(type, "ordered").GetParameters()[0].ParameterType;
            var mark = Adapt(callbackType, value => { events.Add(value); return value; });
            Assert.Equal("1.25|2.5|3.0", Call<string>(type, "ordered", mark));
            Assert.Equal(new[] { 1.25, 2.5, 3.0 }, events);
            events.Clear();
            Assert.Equal("early", Call<string>(type, "early", mark));
            Assert.Equal(new[] { 1.0 }, events);

            events.Clear();
            var expectedFault = new InvalidOperationException("float callback failure");
            var faulting = Adapt(callbackType, value => { events.Add(value); if (value == 2.5) throw expectedFault; return value; });
            var thrown = Assert.Throws<TargetInvocationException>(() => Call<string>(type, "ordered", faulting));
            Assert.Same(expectedFault, thrown.InnerException);
            Assert.Equal(new[] { 1.25, 2.5 }, events);

            events.Clear();
            var gate = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = Call<Task<string>>(type, "delayed", mark, gate.Task);
            Assert.False(pending.IsCompleted);
            Assert.Equal(new[] { 1.25 }, events);
            gate.SetResult(2.5);
            Assert.Equal("1.25|2.5|3.5", await pending.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(new[] { 1.25, 3.5 }, events);

            events.Clear();
            var failed = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending = Call<Task<string>>(type, "delayed", mark, failed.Task);
            var asyncFault = new InvalidOperationException("float task failure");
            failed.SetException(asyncFault);
            Assert.Same(asyncFault, await Assert.ThrowsAsync<InvalidOperationException>(async () => await pending));
            Assert.Equal(new[] { 1.25 }, events);

            events.Clear();
            var cancelled = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending = Call<Task<string>>(type, "delayed", mark, cancelled.Task);
            cancelled.SetCanceled();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
            Assert.Equal(new[] { 1.25 }, events);
        });

        string[] invalid =
        [
            "fn bad(value: float?): string { $\"{value}\" }",
            "fn bad(value: float32?): string { $\"{value}\" }",
            "type Reading { value: float }\nfn bad(value: Reading): string { $\"{value}\" }",
            "fn bad(value: Option<float>): string { $\"{value}\" }",
            "fn bad(value: List<float32>): string { $\"{value}\" }",
            "fn bad(value: Task<float>): string { $\"{value}\" }"
        ];
        foreach (var body in invalid)
        {
            var rejected = DSharpCompiler.Compile("module Invalid\n" + body, "invalid-float-interpolation.ds");
            Assert.True(rejected.CSharp is null, "Expected rejection: " + body);
            Assert.NotEmpty(rejected.Diagnostics);
            Assert.All(rejected.Diagnostics, diagnostic => Assert.Equal("invalid-float-interpolation.ds", diagnostic.Path));
        }

        var library = new SourceFile("library.ds", """
            module Library
            fn small(value: float32): string { $"{value}" }
            fn wide(value: float): string { $"{value}" }
            """);
        var application = new SourceFile("application.ds", """
            module Demo
            import Library
            fn answer(): string { $"{small(0.00001f)}|{wide(1000.0)}" }
            """);
        var multiFile = DSharpCompiler.Compile(new[] { library, application });
        Assert.True(multiFile.CSharp is not null, string.Join("\n", multiFile.Diagnostics));
        Assert.Empty(multiFile.Diagnostics);
        await WithGenerated(multiFile.CSharp!, type =>
        {
            Assert.Equal("1.0e-5|1.0e3", Call<string>(type, "answer"));
            return Task.CompletedTask;
        });
    }

    private static MethodInfo Method(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
    private static T Call<T>(Type type, string name, params object?[] args) => (T)Method(type, name).Invoke(null, args)!;
    private static Delegate Adapt(Type type, Func<double, double> callback)
    {
        var parameter = Expression.Parameter(typeof(double), "value");
        return Expression.Lambda(type, Expression.Invoke(Expression.Constant(callback), parameter), parameter).Compile();
    }

    private static async Task WithGenerated(string csharp, Func<Type, Task> verify)
    {
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-float-interpolation-" + Guid.NewGuid());
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
            catch (OperationCanceledException) { build.Kill(true); throw new TimeoutException("Generated float interpolation build timed out."); }
            Assert.True(build.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("float-interpolation-" + Guid.NewGuid(), isCollectible: true);
            try
            {
                var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0/Generated.dll"));
                await verify(assembly.GetType("DSharp.Generated.Demo")!);
            }
            finally { context.Unload(); }
        }
        finally { Directory.Delete(directory, true); }
    }
}
