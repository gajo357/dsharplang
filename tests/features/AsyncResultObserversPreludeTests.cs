using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class AsyncResultObserversPreludeTests
{
    public async Task TransitiveObserversExecuteAndOmitUnusedHelpers()
    {
        const string originalMapError = """
            async fn __dsharp_result_maperror_task<A, B, E>(convert: (E) -> B, value: Task<Result<A, E>>): Result<A, B> {
                let resolved = await value
                match resolved
                {
                    Result.Success(item) => Result.Success(item)
                    Result.Error(error) => Result.Error(convert(error))
                }
            }
            """;
        const string originalTap = """
            async fn __dsharp_result_tap_task<A, E>(observe: (A) -> Unit, value: Task<Result<A, E>>): Result<A, E> {
                let resolved = await value
                match resolved
                {
                    Result.Success(present) => if true {
                        observe(present)
                        resolved
                    } else { resolved }
                    Result.Error(error) => resolved
                }
            }
            """;
        // mapError calls tap, which is declared later in the bundled prelude.
        const string forward = """
            async fn __dsharp_result_maperror_task<A,B,E>(convert:(E)->B,value:Task<Result<A,E>>):Result<A,B>{
             let r=await tap((v:A)=>(),value)
             match r{
              Result.Success(v)=>Result.Success(v)
              Result.Error(e)=>Result.Error(convert(e))
             }
            }
            """;
        // tap calls mapError, which has already been considered by the binder.
        const string backward = """
            async fn __dsharp_result_tap_task<A,E>(observe:(A)->Unit,value:Task<Result<A,E>>):Result<A,E>{
             let r=await mapError((e:E)=>e,value)
             match r{
              Result.Success(v)=>if true{observe(v)
               r}else{r}
              Result.Error(e)=>r
             }
            }
            """;
        // A dead branch is emitted and must have its helper dependency bound.
        const string unreachable = """
            async fn __dsharp_result_tap_task<A,E>(observe:(A)->Unit,value:Task<Result<A,E>>):Result<A,E>{
             let r=if true{await value}else{await mapError((e:E)=>e,value)}
             match r{
              Result.Success(v)=>if true{observe(v)
               r}else{r}
              Result.Error(e)=>r
             }
            }
            """;
        const string mapErrorSource = """
            module Demo
            type Task { number: int }
            fn tap(value: int): int { value + 1 }
            async fn initial(): Result<int, string> { Success(42) }
            fn convert(error: string): int { 0 }
            async fn answer(): int {
                let result = await (initial() |> mapError(convert))
                match result
                {
                    Success(value) => value
                    Error(error) => 0
                }
            }
            """;
        const string tapSource = """
            module Demo
            type Task { number: int }
            fn mapError(value: int): int { value + 1 }
            async fn initial(): Result<int, string> { Success(42) }
            fn observe(value: int): Unit { () }
            async fn answer(): int {
                let result = await (initial() |> tap(observe))
                match result
                {
                    Success(value) => value
                    Error(error) => 0
                }
            }
            """;
        await CompilePatchAndExecute(originalMapError, forward, mapErrorSource);
        await CompilePatchAndExecute(originalTap, backward, tapSource);
        await CompilePatchAndExecute(originalTap, unreachable, tapSource);

        // Synchronous-only programs should not emit any task overload.
        const string synchronous = """
            module Demo
            fn observe(value: int): Unit { () }
            fn answer(): int {
                let result = Success<int, string>(42) |> tap(observe)
                match result
                {
                    Success(value) => value
                    Error(error) => 0
                }
            }
            """;
        var normal = DSharpCompiler.Compile(synchronous, "observers-prelude-unused.ds");
        Assert.True(normal.CSharp is not null, string.Join("\n", normal.Diagnostics));
        Assert.DoesNotContain("__dsharp_result_maperror_task", normal.CSharp!);
        Assert.DoesNotContain("__dsharp_result_tap_task", normal.CSharp!);
        Assert.DoesNotContain("__dsharp_result_taperror_task", normal.CSharp!);
        await WithGenerated(normal.CSharp!, false);
    }

    private static async Task CompilePatchAndExecute(string original, string replacement, string source)
    {
        var originalBytes = Encoding.UTF8.GetBytes(original);
        var replacementBytes = Encoding.UTF8.GetBytes(replacement);
        Assert.True(replacementBytes.Length <= originalBytes.Length,
            $"Replacement prelude body ({replacementBytes.Length}) exceeds reserved space ({originalBytes.Length}).");
        var bytes = File.ReadAllBytes(typeof(DSharpCompiler).Assembly.Location);
        var found = -1;
        for (var index = 0; index <= bytes.Length - originalBytes.Length; index++)
        {
            if (!bytes.AsSpan(index, originalBytes.Length).SequenceEqual(originalBytes)) continue;
            Assert.Equal(-1, found);
            found = index;
        }
        Assert.True(found >= 0, "Expected embedded observer helper body was not found.");
        Array.Fill(bytes, (byte)' ', found, originalBytes.Length);
        replacementBytes.CopyTo(bytes.AsSpan(found));
        var context = new AssemblyLoadContext("observers-prelude-copy-" + Guid.NewGuid(), isCollectible: true);
        try
        {
            using var stream = new MemoryStream(bytes);
            var compiler = context.LoadFromStream(stream).GetType("DSharp.Compiler.DSharpCompiler", throwOnError: true)!;
            var compile = compiler.GetMethod("Compile", new[] { typeof(string), typeof(string) })!;
            var result = compile.Invoke(null, new object[] { source, "observers-prelude-dependency.ds" })!;
            var type = result.GetType();
            var csharp = (string?)type.GetProperty("CSharp")!.GetValue(result);
            var diagnostics = ((System.Collections.IEnumerable)type.GetProperty("Diagnostics")!.GetValue(result)!).Cast<object>().ToArray();
            Assert.True(csharp is not null, string.Join("\n", diagnostics.Select(diagnostic => diagnostic.ToString())));
            Assert.Contains("__dsharp_result_maperror_task", csharp!);
            Assert.Contains("__dsharp_result_tap_task", csharp!);
            Assert.DoesNotContain("__dsharp_result_taperror_task", csharp!);
            await WithGenerated(csharp!, true);
        }
        finally { context.Unload(); }
    }

    private static async Task WithGenerated(string csharp, bool isAsync)
    {
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-observers-prelude-" + Guid.NewGuid());
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
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated observer prelude build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("observers-generated-" + Guid.NewGuid(), isCollectible: true);
            try
            {
                var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0/Generated.dll"));
                var module = assembly.GetType("DSharp.Generated.Demo", throwOnError: true)!;
                var value = module.GetMethod("answer", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.Invoke(null, null);
                if (isAsync) Assert.Equal(42, await ((Task<int>)value!).WaitAsync(TimeSpan.FromSeconds(10)));
                else Assert.Equal(42, value);
            }
            finally { context.Unload(); }
        }
        finally { Directory.Delete(directory, true); }
    }
}
