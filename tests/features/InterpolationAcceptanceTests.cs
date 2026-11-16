using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

// Aggregated with the existing feature test to keep the bounded TRX report stable.
public sealed class InterpolationAcceptanceTests
{
    public async Task InterpolatedStringsExecuteAndRejectInvalidPrograms()
    {
        const string source = """
            module Demo
            type Box { value: int }
            type Fault { | Bad }
            resource Item { read(): int { 42 }
                            release(): Unit { () } }
            fn greet(name: string): string { $"Hello, {name}!" }
            fn text(): string { $"雪 ☃ {{a}} \"quote\" \\ {"hi"} {'λ'}" }
            fn escapedBraces(): string { $"{{{42}}} {{}}" }
            fn empty(): string { $"" }
            fn resourceValue(): string { using item = Item() { $"value {item.read()}" } }
            fn blocks(flag: bool): string { $"{if flag { Box { value = 42 }.value } else { 0 }}" }
            fn matching(value: bool): string { $"{match value { true => 42
                                                                   false => 0 }}" }
            fn nested(): string { $"outer {$"inner {42}"} end" }
            fn commented(): string { $"{1 /* comment with } and quote \" */ + 2}" }
            fn values(a: sbyte, b: byte, c: short, d: ushort, e: int, f: uint, g: long, h: ulong): string {
                $"{a}|{b}|{c}|{d}|{e}|{f}|{g}|{h}|{true}|{false}|{()}"
            }
            fn markOrder(mark: (int) -> int): string { $"[{mark(1)}|{mark(2)}|{mark(3)}]" }
            fn helperOrder(mark: (int) -> int): string { $"{mark(1)}{if mark(2) == 2 { 3 } else { 4 }}{mark(5)}" }
            fn inferred() { $"value {42}" }
            fn early(): string { $"start {if true { return "early" } else { 1 }} end" }
            fn earlyWithEffects(mark: (int) -> int): string { $"{mark(1)}{if true { return "early" } else { 1 }}{mark(2)}" }
            fn tick(mark: (int) -> int): Unit { let ignored = mark(1)
                                                  () }
            fn unitEffect(mark: (int) -> int): string { $"{tick(mark)}:{mark(2)}" }
            fn fail(): Result<int, Fault> { Error(Fault.Bad) }
            fn propagate(mark: (int) -> int): Result<string, Fault> { Success($"{mark(1)}:{fail()?}:{mark(2)}") }
            fn handled(mark: (int) -> int): bool { match propagate(mark) { Error(Fault.Bad) => true
                                                                               Success(_) => false } }
            async fn asyncValue(pending: Task<int>): string { $"value {await pending}!" }
            async fn asyncOrder(mark: (int) -> int, pending: Task<int>): string { $"{mark(1)}:{await pending}:{mark(2)}" }
            """;
        var compiled = DSharpCompiler.Compile(source, "interpolation.ds");
        Assert.True(compiled.CSharp is not null, string.Join("\n", compiled.Diagnostics));
        Assert.Empty(compiled.Diagnostics);
        await WithGenerated(compiled.CSharp!, "Demo", async type =>
        {
            Assert.Equal("Hello, Ada!", Call<string>(type, "greet", "Ada"));
            Assert.Equal("雪 ☃ {a} \"quote\" \\ hi λ", Call<string>(type, "text"));
            Assert.Equal("{42} {}", Call<string>(type, "escapedBraces"));
            Assert.Equal("", Call<string>(type, "empty"));
            Assert.Equal("value 42", Call<string>(type, "resourceValue"));
            Assert.Equal("42", Call<string>(type, "blocks", true));
            Assert.Equal("0", Call<string>(type, "blocks", false));
            Assert.Equal("42", Call<string>(type, "matching", true));
            Assert.Equal("outer inner 42 end", Call<string>(type, "nested"));
            Assert.Equal("3", Call<string>(type, "commented"));
            Assert.Equal("value 42", Call<string>(type, "inferred"));
            Assert.Equal("early", Call<string>(type, "early"));
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-EG");
                Assert.Equal("-128|255|-32768|65535|-2147483648|4294967295|-9223372036854775808|18446744073709551615|true|false|()",
                    Call<string>(type, "values", sbyte.MinValue, byte.MaxValue, short.MinValue, ushort.MaxValue,
                        int.MinValue, uint.MaxValue, long.MinValue, ulong.MaxValue));
            }
            finally { CultureInfo.CurrentCulture = previous; }

            var seen = new List<int>();
            var callbackType = Method(type, "markOrder").GetParameters()[0].ParameterType;
            var callback = Adapt(callbackType, n => { seen.Add(n); return n; });
            Assert.Equal("[1|2|3]", Call<string>(type, "markOrder", callback));
            Assert.Equal(new[] { 1, 2, 3 }, seen);
            seen.Clear();
            Assert.Equal("135", Call<string>(type, "helperOrder", callback));
            Assert.Equal(new[] { 1, 2, 5 }, seen);
            seen.Clear();
            Assert.Equal("early", Call<string>(type, "earlyWithEffects", callback));
            Assert.Equal(new[] { 1 }, seen);
            seen.Clear();
            Assert.Equal("():2", Call<string>(type, "unitEffect", callback));
            Assert.Equal(new[] { 1, 2 }, seen);
            seen.Clear();
            Assert.True(Call<bool>(type, "handled", callback));
            Assert.Equal(new[] { 1 }, seen);
            seen.Clear();
            var fault = new InvalidOperationException("failure");
            callback = Adapt(callbackType, n => { seen.Add(n); if (n == 2) throw fault; return n; });
            var thrown = Assert.Throws<TargetInvocationException>(() => Call<string>(type, "markOrder", callback));
            Assert.Same(fault, thrown.InnerException);
            Assert.Equal(new[] { 1, 2 }, seen);

            var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = Call<Task<string>>(type, "asyncValue", gate.Task);
            Assert.False(pending.IsCompleted);
            gate.SetResult(42);
            Assert.Equal("value 42!", await pending.WaitAsync(TimeSpan.FromSeconds(10)));
            var failed = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending = Call<Task<string>>(type, "asyncValue", failed.Task);
            var asyncFault = new InvalidOperationException("async failure");
            failed.SetException(asyncFault);
            Assert.Same(asyncFault, await Assert.ThrowsAsync<InvalidOperationException>(async () => await pending));

            seen.Clear();
            callback = Adapt(callbackType, n => { seen.Add(n); return n; });
            gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending = Call<Task<string>>(type, "asyncOrder", callback, gate.Task);
            Assert.Equal(new[] { 1 }, seen);
            Assert.False(pending.IsCompleted);
            gate.SetResult(42);
            Assert.Equal("1:42:2", await pending.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(new[] { 1, 2 }, seen);
        });

        RejectInvalidPrograms();
        await Imports();
    }

    private static void RejectInvalidPrograms()
    {
        string[] invalid =
        [
            "fn bad(): string { $\"a {\" }", "fn bad(): string { $\"a }\" }",
            "fn bad(): string { $\"a {}\" }", "fn bad(): string { $\"{1 +}\" }",
            "fn bad(): string { $\"{1:hex}\" }", "fn bad(): string { $\"{1,3}\" }",
            "fn bad(): string { $\"bad \\q\" }", "fn bad(): string { $\"{[1, 2]}\" }",
            "fn bad(f: (int) -> int): string { $\"{f}\" }",
            "fn bad(value: int?): string { $\"{value}\" }",
            "fn bad(value: Task<int>): string { $\"{value}\" }",
            "fn bad(): string { let value = MutableList<int>()\n $\"{value}\" }",
            "resource Item { release(): Unit { () } }\nfn bad(): string { using item = Item() { $\"{item}\" } }",
        ];
        var accepted = new List<string>();
        foreach (var item in invalid)
        {
            var result = DSharpCompiler.Compile("module Demo\n" + item, "bad.ds");
            if (result.CSharp is not null || result.Diagnostics.Count == 0) accepted.Add(item);
            Assert.All(result.Diagnostics, d => Assert.StartsWith("DS", d.Code));
        }
        Assert.True(accepted.Count == 0, "Expected D# diagnostics for:\n" + string.Join("\n", accepted));

        const string badName = "module Demo\nfn bad(): string { $\"a {missing}\" }";
        var diagnostic = DSharpCompiler.Compile(badName, "specific.ds").Diagnostics;
        Assert.Contains(diagnostic, d => d.Path == "specific.ds" && d.Span.Start == badName.IndexOf("missing", StringComparison.Ordinal) && d.Span.Length == "missing".Length);

        const string crlf = "module Demo\r\nfn bad(): string { $\"a {missing}\" }";
        var crlfDiagnostics = DSharpCompiler.Compile(crlf, "crlf.ds").Diagnostics;
        Assert.Contains(crlfDiagnostics, d => d.Path == "crlf.ds" && d.Span.Start == crlf.IndexOf("missing", StringComparison.Ordinal) && d.Span.Line == 2);
    }

    private static async Task Imports()
    {
        var files = new[]
        {
            new SourceFile("library.ds", "module Library\nfn number(): int { 42 }"),
            new SourceFile("app.ds", "module App\nimport Library\nfn answer(): string { $\"from import: {number()}\" }")
        };
        var result = DSharpCompiler.Compile(files);
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        await WithGenerated(result.CSharp!, "App", type =>
        {
            Assert.Equal("from import: 42", Call<string>(type, "answer"));
            return Task.CompletedTask;
        });
        var invalid = new[] { files[0], new SourceFile("wrong-app.ds", "module App\nfn bad(): string { $\"{number()}\" }") };
        var failure = DSharpCompiler.Compile(invalid);
        Assert.Null(failure.CSharp);
        Assert.Contains(failure.Diagnostics, d => d.Path == "wrong-app.ds");
    }

    private static MethodInfo Method(Type type, string name) => type.GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
    private static T Call<T>(Type type, string name, params object?[] args) => (T)Method(type, name).Invoke(null, args)!;
    private static Delegate Adapt(Type type, Func<int, int> action)
    {
        var parameter = Expression.Parameter(typeof(int), "value");
        return Expression.Lambda(type, Expression.Invoke(Expression.Constant(action), parameter), parameter).Compile();
    }

    private static async Task WithGenerated(string csharp, string module, Func<Type, Task> verify)
    {
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-interpolation-" + Guid.NewGuid());
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
            var context = new AssemblyLoadContext("interpolation-" + Guid.NewGuid(), isCollectible: true);
            try
            {
                var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0/Generated.dll"));
                await verify(assembly.GetType("DSharp.Generated." + module)!);
            }
            finally { context.Unload(); }
        }
        finally { Directory.Delete(directory, true); }
    }
}
