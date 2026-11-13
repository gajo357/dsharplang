using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class DSharpPreludeExecutionTests
{
    [Fact(DisplayName = "Embedded D# prelude is executable")]
    public async Task ImplicitHelpersUseTheBundledDSharpSource()
    {
        const string source = """
            module Demo
            fn fail(value: int): Unit {
                let zero = value - value
                let unused = 1 / zero
                ()
            }
            fn run(): int {
                let input: Result<int, string> = Success(42)
                let observed = tap(fail, input)
                match observed {
                    Success(value) => value
                    Error(error) => 0
                }
            }
            """;

        // The real source calls the observer, so invoking this program faults.
        await NullableAcceptanceRuntime.WithGenerated(source, type =>
        {
            var failure = Assert.Throws<TargetInvocationException>(() => NullableAcceptanceRuntime.Invoke(type, "run"));
            Assert.IsType<DivideByZeroException>(failure.InnerException);
        });

        // Mutate a copy of the compiler assembly, never the checked-in source or
        // the running compiler. The embedded tap success branch now deliberately
        // skips the observer. Its changed runtime behavior proves the implicit
        // helper is compiled from the resource, not another equivalent body.
        const string anchor = "Result.Success(item) => if true {\n            observe(item)";
        const string altered = "Result.Success(item) => if false{\n            observe(item)";
        var alteredAssembly = PatchResource(anchor, altered);
        var changed = CompileWith(alteredAssembly, source);
        Assert.Null(changed.DiagnosticPath);
        Assert.NotNull(changed.CSharp);
        await ExecuteGenerated(changed.CSharp!, type => Assert.Equal(42, NullableAcceptanceRuntime.Invoke(type, "run")));

        // A later prelude helper can itself call an earlier one, even if the
        // user's program mentions only tap. The compiler must include and bind
        // that dependency rather than relying on the original helper bodies.
        const string successBranch = "Result.Success(item) => if true {\n            observe(item)\n            value\n        } else { value }";
        const string delegatedBranch = "Result.Success(item) => andThen(x => Result.Success(x), value)";
        Assert.True(delegatedBranch.Length <= successBranch.Length);
        var delegated = CompileWith(PatchResource(successBranch, delegatedBranch.PadRight(successBranch.Length)), source);
        Assert.Null(delegated.DiagnosticPath);
        Assert.NotNull(delegated.CSharp);
        await ExecuteGenerated(delegated.CSharp!, type => Assert.Equal(42, NullableAcceptanceRuntime.Invoke(type, "run")));

        // A user declaration shadows the prelude at the user's call site, but
        // the prelude's own dependency must still resolve to the standard
        // helper. The two declarations intentionally have incompatible arity.
        const string shadowedSource = """
            module Demo
            fn andThen(value: int): int { value + 1 }
            fn fail(value: int): Unit {
                let zero = value - value
                let unused = 1 / zero
                ()
            }
            fn run(): int {
                let input: Result<int, string> = Success(42)
                let observed = tap(fail, input)
                match observed {
                    Success(value) => andThen(value - 1)
                    Error(error) => 0
                }
            }
            """;
        var shadowed = CompileWith(PatchResource(successBranch, delegatedBranch.PadRight(successBranch.Length)), shadowedSource);
        Assert.Null(shadowed.DiagnosticPath);
        Assert.NotNull(shadowed.CSharp);
        await ExecuteGenerated(shadowed.CSharp!, type => Assert.Equal(42, NullableAcceptanceRuntime.Invoke(type, "run")));

        // A helper-name-like callback parameter is still a lexical function
        // value. A nested lambda may not capture it through prelude name
        // resolution; the ordinary function-capture restriction must apply.
        const string renamedTap = "fn tap<A, E>(andThen:";
        const string originalTap = "fn tap<A, E>(observe:";
        const string forbiddenCapture = "Result.Success(item) => if true { let f: (A) -> Unit = x => andThen(x)\n value } else { value }";
        Assert.True(forbiddenCapture.Length <= successBranch.Length);
        var captureAssembly = PatchResource(originalTap, renamedTap);
        captureAssembly = PatchResource(captureAssembly, successBranch, forbiddenCapture.PadRight(successBranch.Length));
        var captureRejected = CompileWith(captureAssembly, source);
        Assert.Null(captureRejected.CSharp);
        Assert.Equal("<standard:Result.ds>", captureRejected.DiagnosticPath);
        Assert.Equal("DS2026", captureRejected.DiagnosticCode);

        // The helper body is still checked and emitted for an unreachable arm.
        // Its dependency must therefore be present even when the D# condition
        // makes that arm unreachable at runtime.
        const string deadDependency = "Result.Success(item) => if true { value } else { andThen(x => Result.Success(x), value) }";
        Assert.True(deadDependency.Length <= successBranch.Length);
        var dead = CompileWith(PatchResource(successBranch, deadDependency.PadRight(successBranch.Length)), source);
        Assert.Null(dead.DiagnosticPath);
        Assert.NotNull(dead.CSharp);
        await ExecuteGenerated(dead.CSharp!, type => Assert.Equal(42, NullableAcceptanceRuntime.Invoke(type, "run")));

        // A broken helper body must be checked as D# source and produce a useful
        // diagnostic at the bundled source, not silently fall back to old IR.
        const string invalid = "Result.Success(item) => if true {\n            badcall(item)";
        var invalidAssembly = PatchResource(anchor, invalid);
        var rejected = CompileWith(invalidAssembly, source);
        Assert.Null(rejected.CSharp);
        Assert.Equal("<standard:Result.ds>", rejected.DiagnosticPath);
        Assert.False(string.IsNullOrEmpty(rejected.DiagnosticCode));

        // The compiler must be self-contained when copied and invoked from an
        // unrelated working directory, without the source checkout present.
        await CompileOutsideCheckout();
    }

    private static byte[] PatchResource(string original, string replacement)
    {
        return PatchResource(File.ReadAllBytes(typeof(DSharpCompiler).Assembly.Location), original, replacement);
    }

    private static byte[] PatchResource(byte[] bytes, string original, string replacement)
    {
        var needle = Encoding.UTF8.GetBytes(original);
        var substitute = Encoding.UTF8.GetBytes(replacement);
        Assert.Equal(needle.Length, substitute.Length);
        var found = -1;
        for (var index = 0; index <= bytes.Length - needle.Length; index++)
        {
            if (!bytes.AsSpan(index, needle.Length).SequenceEqual(needle)) continue;
            Assert.Equal(-1, found);
            found = index;
        }
        Assert.True(found >= 0, "Expected D# source was not found in the compiler resource.");
        substitute.CopyTo(bytes.AsSpan(found, substitute.Length));
        return bytes;
    }

    private static (string? CSharp, string? DiagnosticPath, string? DiagnosticCode) CompileWith(byte[] assembly, string source)
    {
        var context = new AssemblyLoadContext("mutated-prelude-" + Guid.NewGuid(), isCollectible: true);
        try
        {
            using var stream = new MemoryStream(assembly);
            var compiler = context.LoadFromStream(stream).GetType("DSharp.Compiler.DSharpCompiler", throwOnError: true)!;
            var compile = compiler.GetMethod("Compile", new[] { typeof(string), typeof(string) })!;
            var result = compile.Invoke(null, new object[] { source, "consumer.ds" })!;
            var type = result.GetType();
            var csharp = (string?)type.GetProperty("CSharp")!.GetValue(result);
            var diagnostics = (System.Collections.IEnumerable)type.GetProperty("Diagnostics")!.GetValue(result)!;
            var first = diagnostics.Cast<object>().FirstOrDefault();
            return (csharp,
                first is null ? null : (string?)first.GetType().GetProperty("Path")!.GetValue(first),
                first is null ? null : (string?)first.GetType().GetProperty("Code")!.GetValue(first));
        }
        finally { context.Unload(); }
    }

    private static async Task ExecuteGenerated(string csharp, Action<Type> verify)
    {
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-prelude-execution-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "Generated.cs"), csharp);
            File.WriteAllText(Path.Combine(directory, "Generated.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>");
            File.WriteAllText(Path.Combine(directory, "global.json"), "{\"sdk\":{\"version\":\"10.0.100\",\"rollForward\":\"disable\"}}");
            File.WriteAllText(Path.Combine(directory, "NuGet.Config"), "<configuration><packageSources><clear/></packageSources></configuration>");
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "build", "Generated.csproj", "--nologo", "--verbosity", "quiet" }) start.ArgumentList.Add(argument);
            using var build = Process.Start(start)!;
            var stdout = build.StandardOutput.ReadToEndAsync();
            var stderr = build.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try { await build.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { build.Kill(true); throw new TimeoutException("Generated prelude build timed out."); }
            Assert.True(build.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("generated-prelude-" + Guid.NewGuid(), isCollectible: true);
            try
            {
                var generated = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net10.0/Generated.dll"));
                verify(generated.GetType("DSharp.Generated.Demo", throwOnError: true)!);
            }
            finally { context.Unload(); }
        }
        finally { Directory.Delete(directory, true); }
    }

    private static async Task CompileOutsideCheckout()
    {
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-prelude-copy-" + Guid.NewGuid());
        var isolated = Path.Combine(directory, "unrelated-working-directory");
        Directory.CreateDirectory(isolated);
        try
        {
            File.Copy(typeof(DSharpCompiler).Assembly.Location, Path.Combine(directory, "DSharp.Compiler.dll"));
            File.WriteAllText(Path.Combine(directory, "Probe.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><Reference Include=\"DSharp.Compiler\"><HintPath>DSharp.Compiler.dll</HintPath></Reference></ItemGroup></Project>");
            File.WriteAllText(Path.Combine(directory, "global.json"), "{\"sdk\":{\"version\":\"10.0.100\",\"rollForward\":\"disable\"}}");
            File.WriteAllText(Path.Combine(directory, "NuGet.Config"), "<configuration><packageSources><clear/></packageSources></configuration>");
            File.WriteAllText(Path.Combine(directory, "Program.cs"), "using System; using System.IO; using DSharp.Compiler; var result = DSharpCompiler.Compile(File.ReadAllText(args[0]), args[0]); if (result.CSharp == null) throw new Exception(string.Join(\"; \", result.Diagnostics)); Console.Write(\"compiled\");");
            var consumer = Path.Combine(directory, "consumer.ds");
            File.WriteAllText(consumer, "module Demo\nfn observe(value: int): Unit { () }\nfn run(): Result<int, string> { tap(observe, Success<int, string>(42)) }");
            await RunDotnet(directory, "build", "Probe.csproj", "--nologo", "--verbosity", "quiet");
            var output = await RunDotnet(isolated, Path.Combine(directory, "bin/Debug/net10.0/Probe.dll"), consumer);
            Assert.Equal("compiled", output.Trim());
        }
        finally { Directory.Delete(directory, true); }
    }

    private static async Task<string> RunDotnet(string workingDirectory, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = workingDirectory, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Prelude probe timed out."); }
        var output = await stdout;
        var errors = await stderr;
        Assert.True(process.ExitCode == 0, output + errors);
        return output;
    }
}
