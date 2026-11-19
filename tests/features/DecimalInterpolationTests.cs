using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

public sealed class DecimalInterpolationTests
{
    public async Task DecimalInterpolationPreservesValueOrderAndCultureIndependence()
    {
        const string source = """
            module Demo
            type Invoice { amount: decimal }

            fn format(value: decimal): string { $"{value}" }
            fn invoice(invoice: Invoice): string { $"Total: {invoice.amount}!" }
            fn invoiceAnswer(): string { invoice(Invoice { amount = 19.9500m }) }
            fn mixed(value: decimal): string { $"{42}|{true}|{value}|{'X'}" }
            fn computed(): string { $"{19.9500m + 2.0500m}" }
            fn ordered(mark: (decimal) -> decimal): string {
                $"{mark(1.20m)}|{mark(2.50m)}|{mark(3.00m)}"
            }
            fn early(mark: (decimal) -> decimal): string {
                $"{mark(1m)}|{if true { return "early" } else { 2m }}|{mark(3m)}"
            }
            async fn delayed(mark: (decimal) -> decimal, pending: Task<decimal>): string {
                $"{mark(1.20m)}|{await pending}|{mark(3.40m)}"
            }
            """;
        var result = DSharpCompiler.Compile(source, "decimal-interpolation.ds");
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
                    Assert.Equal("0", Call<string>(type, "format", 0m));
                    Assert.Equal("0", Call<string>(type, "format", new decimal(0, 0, 0, true, 28)));
                    Assert.Equal("19.95", Call<string>(type, "format", 19.9500m));
                    Assert.Equal("-19.95", Call<string>(type, "format", -19.9500m));
                    Assert.Equal("1000", Call<string>(type, "format", 1000.0000m));
                    Assert.Equal("0.0000000000000000000000000001", Call<string>(type, "format", 0.0000000000000000000000000001m));
                    Assert.Equal("-0.0000000000000000000000000001", Call<string>(type, "format", -0.0000000000000000000000000001m));
                    Assert.Equal("79228162514264337593543950335", Call<string>(type, "format", decimal.MaxValue));
                    Assert.Equal("-79228162514264337593543950335", Call<string>(type, "format", decimal.MinValue));
                    Assert.Equal("1234567890123456789012345678.9", Call<string>(type, "format", 1234567890123456789012345678.9m));
                    Assert.Equal("7.9228162514264337593543950335", Call<string>(type, "format", 7.9228162514264337593543950335m));
                    Assert.Equal("Total: 19.95!", Call<string>(type, "invoiceAnswer"));
                    Assert.Equal("42|true|19.95|X", Call<string>(type, "mixed", 19.9500m));
                    Assert.Equal("22", Call<string>(type, "computed"));
                }
            }
            finally { CultureInfo.CurrentCulture = previousCulture; }

            var events = new List<decimal>();
            var callbackType = Method(type, "ordered").GetParameters()[0].ParameterType;
            var mark = Adapt(callbackType, value => { events.Add(value); return value; });
            Assert.Equal("1.2|2.5|3", Call<string>(type, "ordered", mark));
            Assert.Equal(new[] { 1.20m, 2.50m, 3.00m }, events);
            events.Clear();
            Assert.Equal("early", Call<string>(type, "early", mark));
            Assert.Equal(new[] { 1m }, events);

            events.Clear();
            var expected = new InvalidOperationException("decimal callback failure");
            var faulting = Adapt(callbackType, value => { events.Add(value); if (value == 2.50m) throw expected; return value; });
            var thrown = Assert.Throws<TargetInvocationException>(() => Call<string>(type, "ordered", faulting));
            Assert.Same(expected, thrown.InnerException);
            Assert.Equal(new[] { 1.20m, 2.50m }, events);

            events.Clear();
            var gate = new TaskCompletionSource<decimal>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = Call<Task<string>>(type, "delayed", mark, gate.Task);
            Assert.False(pending.IsCompleted);
            Assert.Equal(new[] { 1.20m }, events);
            gate.SetResult(2.5000m);
            Assert.Equal("1.2|2.5|3.4", await pending.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(new[] { 1.20m, 3.40m }, events);

            events.Clear();
            var failed = new TaskCompletionSource<decimal>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending = Call<Task<string>>(type, "delayed", mark, failed.Task);
            var asyncFault = new InvalidOperationException("decimal task failure");
            failed.SetException(asyncFault);
            Assert.Same(asyncFault, await Assert.ThrowsAsync<InvalidOperationException>(async () => await pending));
            Assert.Equal(new[] { 1.20m }, events);

            events.Clear();
            var cancelled = new TaskCompletionSource<decimal>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending = Call<Task<string>>(type, "delayed", mark, cancelled.Task);
            cancelled.SetCanceled();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
            Assert.Equal(new[] { 1.20m }, events);
        });

        string[] invalid =
        [
            "fn bad(value: decimal?): string { $\"{value}\" }",
            "type Invoice { amount: decimal }\nfn bad(value: Invoice): string { $\"{value}\" }",
            "fn bad(value: Option<decimal>): string { $\"{value}\" }",
            "fn bad(value: List<decimal>): string { $\"{value}\" }",
            "fn bad(value: Task<decimal>): string { $\"{value}\" }"
        ];
        foreach (var body in invalid)
        {
            var rejected = DSharpCompiler.Compile("module Invalid\n" + body, "invalid-decimal-interpolation.ds");
            Assert.True(rejected.CSharp is null, "Expected rejection: " + body);
            Assert.NotEmpty(rejected.Diagnostics);
            Assert.All(rejected.Diagnostics, diagnostic => Assert.Equal("invalid-decimal-interpolation.ds", diagnostic.Path));
        }
    }

    private static MethodInfo Method(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
    private static T Call<T>(Type type, string name, params object?[] args) => (T)Method(type, name).Invoke(null, args)!;
    private static Delegate Adapt(Type type, Func<decimal, decimal> callback)
    {
        var parameter = Expression.Parameter(typeof(decimal), "value");
        return Expression.Lambda(type, Expression.Invoke(Expression.Constant(callback), parameter), parameter).Compile();
    }

    private static async Task WithGenerated(string csharp, Func<Type, Task> verify)
    {
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-decimal-interpolation-" + Guid.NewGuid());
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
            catch (OperationCanceledException) { build.Kill(true); throw new TimeoutException("Generated decimal interpolation build timed out."); }
            Assert.True(build.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("decimal-interpolation-" + Guid.NewGuid(), isCollectible: true);
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
