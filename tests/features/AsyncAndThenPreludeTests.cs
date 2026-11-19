using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class AsyncAndThenPreludeTests
{
    public async Task TransitiveDependenciesExecuteAndOmitUnusedHelpers()
    {
        const string taskAsync = """
            async fn __dsharp_result_andthen_task_async<A, B, E>(next: (A) -> Task<Result<B, E>>, value: Task<Result<A, E>>): Result<B, E> {
                let resolved = await value
                match resolved
                {
                    Result.Success(item) => await next(item)
                    Result.Error(error) => Result.Error(error)
                }
            }
            """;
        const string taskSync = """
            async fn __dsharp_result_andthen_task<A, B, E>(next: (A) -> Result<B, E>, value: Task<Result<A, E>>): Result<B, E> {
                let resolved = await value
                match resolved
                {
                    Result.Success(item) => next(item)
                    Result.Error(error) => Result.Error(error)
                }
            }
            """;
        // The task+async helper calls the task+sync helper that appears earlier
        // in the packaged prelude; the worklist must revisit that dependency.
        const string backward = """
            async fn __dsharp_result_andthen_task_async<A,B,E>(next:(A)->Task<Result<B,E>>,value:Task<Result<A,E>>):Result<B,E>{
             let r=await andThen((v:A)=>Success<A,E>(v),value)
             match r{
              Result.Success(v)=>await next(v)
              Result.Error(e)=>Result.Error(e)
             }
            }
            """;
        // The task+sync helper calls the task+async helper later in the source.
        // This test intentionally uses identity semantics in the copied source.
        const string forward = """
            async fn __dsharp_result_andthen_task<A,E>(next:(A)->Result<A,E>,value:Task<Result<A,E>>):Result<A,E>{
             await andThen(async (v:A)=>Success<A,E>(v),value)
            }
            """;
        // Even unreachable branches are type-checked and emitted; they must
        // still cause any referenced helper body to be available to C#.
        const string unreachable = """
            async fn __dsharp_result_andthen_task_async<A,B,E>(next:(A)->Task<Result<B,E>>,value:Task<Result<A,E>>):Result<B,E>{
             let r=if true {await value}else{await andThen((v:A)=>Success<A,E>(v),value)}
             match r{
              Result.Success(v)=>await next(v)
              Result.Error(e)=>Result.Error(e)
             }
            }
            """;
        const string taskAsyncSource = """
            module Demo
            type Task { number: int }
            fn map(value: int): int { value + 1 }
            async fn initial(): Result<int, string> { Success(41) }
            async fn increment(value: int): Result<int, string> { Success(map(value)) }
            async fn answer(): int {
                let outcome = await (initial() |> andThen(increment))
                match outcome
                {
                    Success(value) => value
                    Error(error) => 0
                }
            }
            """;
        const string taskSyncSource = """
            module Demo
            type Task { number: int }
            fn map(value: int): int { value }
            async fn initial(): Result<int, string> { Success(42) }
            fn identity(value: int): Result<int, string> { Success(map(value)) }
            async fn answer(): int {
                let outcome = await (initial() |> andThen(identity))
                match outcome
                {
                    Success(value) => value
                    Error(error) => 0
                }
            }
            """;
        await CompilePatchAndExecute(taskAsync, backward, taskAsyncSource);
        await CompilePatchAndExecute(taskSync, forward, taskSyncSource);
        await CompilePatchAndExecute(taskAsync, unreachable, taskAsyncSource);

        // A normal sync use must not force any async overload into the output.
        const string synchronous = """
            module Demo
            fn next(value: int): Result<int, string> { Success(value + 1) }
            fn answer(): int {
                let outcome = Success<int, string>(41) |> andThen(next)
                match outcome
                {
                    Success(value) => value
                    Error(error) => 0
                }
            }
            """;
        var normal = DSharpCompiler.Compile(synchronous, "andthen-prelude-unused.ds");
        Assert.True(normal.CSharp is not null, string.Join("\n", normal.Diagnostics));
        Assert.DoesNotContain("__dsharp_result_andthen_task", normal.CSharp!);
        Assert.DoesNotContain("__dsharp_result_andthen_async", normal.CSharp!);
        await WithGenerated(normal.CSharp!, false);
    }

    private static async Task CompilePatchAndExecute(string original, string replacement, string source)
    {
        // Patch a copy of the embedded D# source in an isolated compiler assembly.
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
        Assert.True(found >= 0, "Expected embedded andThen body was not found.");
        Array.Fill(bytes, (byte)' ', found, originalBytes.Length);
        replacementBytes.CopyTo(bytes.AsSpan(found));
        var context = new AssemblyLoadContext("andthen-prelude-copy-" + Guid.NewGuid(), isCollectible: true);
        try
        {
            using var stream = new MemoryStream(bytes);
            var compiler = context.LoadFromStream(stream).GetType("DSharp.Compiler.DSharpCompiler", throwOnError: true)!;
            var compile = compiler.GetMethod("Compile", new[] { typeof(string), typeof(string) })!;
            var result = compile.Invoke(null, new object[] { source, "andthen-prelude-dependency.ds" })!;
            var type = result.GetType();
            var csharp = (string?)type.GetProperty("CSharp")!.GetValue(result);
            var diagnostics = ((System.Collections.IEnumerable)type.GetProperty("Diagnostics")!.GetValue(result)!).Cast<object>().ToArray();
            Assert.True(csharp is not null, string.Join("\n", diagnostics.Select(diagnostic => diagnostic.ToString())));
            Assert.Matches("__dsharp_result_andthen_task(?!_async)", csharp!);
            Assert.Contains("__dsharp_result_andthen_task_async", csharp!);
            Assert.DoesNotContain("__dsharp_result_andthen_async", csharp!);
            await WithGenerated(csharp!, true);
        }
        finally { context.Unload(); }
    }

    private static async Task WithGenerated(string csharp, bool isAsync)
    {
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-andthen-prelude-" + Guid.NewGuid());
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
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated async andThen prelude build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("andthen-generated-" + Guid.NewGuid(), isCollectible: true);
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
