using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

public sealed class AF205
{
    [Fact]
    public async Task Run()
    {
        const string source = """
            module Demo

            fn numbers(mark: (int)->int): List<int> {
                let ignored = mark(0)
                [1, 2, 3]
            }

            fn empty(mark: (int)->int): List<int> {
                let ignored = mark(0)
                []
            }

            async fn delayedNumbers(mark: (int)->int, gate: Task<int>): List<int> {
                let ignored = mark(0)
                let released = await gate
                [1, 2, 3]
            }

            async fn visit(mark: (int)->int, step: (int)->Task<int>): int {
                for value in numbers(mark) {
                    let resolved = await step(value)
                    let ignored = mark(resolved)
                    ()
                }
                42
            }

            async fn visitEmpty(mark: (int)->int, step: (int)->Task<int>): int {
                for value in empty(mark) {
                    let resolved = await step(value)
                    let ignored = mark(resolved)
                    ()
                }
                42
            }

            async fn visitAwaitedSource(mark: (int)->int, gate: Task<int>, step: (int)->Task<int>): int {
                for value in await delayedNumbers(mark, gate) {
                    let seen = mark(value)
                    let resolved = await step(seen)
                    ()
                }
                42
            }
            """;
        await WithGenerated(source, async type =>
        {
            await CompletesSequentially(type);
            await HandlesEmpty(type);
            await WaitsForSource(type);
            await StopsAfterFault(type);
            await StopsAfterCancellation(type);
        });
        await new AsyncForControlCases().Run();
    }

    private static async Task CompletesSequentially(Type type)
    {
        var events = new ConcurrentQueue<int>();
        var started = Enumerable.Range(0, 3).Select(_ => Gate<int>()).ToArray();
        var steps = Enumerable.Range(0, 3).Select(_ => Gate<int>()).ToArray();
        var method = Method(type, "visit");
        Func<int, int> mark = value => { events.Enqueue(value); return value; };
        Func<int, Task<int>> step = value =>
        {
            events.Enqueue(100 + value);
            started[value - 1].SetResult(value);
            return steps[value - 1].Task;
        };
        var result = Call(method, mark, step);
        Assert.False(result.IsCompleted);
        Assert.Equal(new[] { 0, 101 }, events.ToArray());
        Assert.False(started[1].Task.IsCompleted);

        steps[0].SetResult(11);
        Assert.Equal(2, await started[1].Task.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(new[] { 0, 101, 11, 102 }, events.ToArray());
        Assert.False(result.IsCompleted);
        Assert.False(started[2].Task.IsCompleted);

        steps[1].SetResult(12);
        Assert.Equal(3, await started[2].Task.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(new[] { 0, 101, 11, 102, 12, 103 }, events.ToArray());
        Assert.False(result.IsCompleted);

        steps[2].SetResult(13);
        Assert.Equal(42, await result.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(new[] { 0, 101, 11, 102, 12, 103, 13 }, events.ToArray());
    }

    private static async Task HandlesEmpty(Type type)
    {
        var events = new ConcurrentQueue<int>();
        Func<int, int> mark = value => { events.Enqueue(value); return value; };
        Func<int, Task<int>> step = _ => throw new InvalidOperationException("An empty loop ran its body.");
        Assert.Equal(42, await Call(Method(type, "visitEmpty"), mark, step).WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(new[] { 0 }, events.ToArray());
    }

    private static async Task WaitsForSource(Type type)
    {
        var events = new ConcurrentQueue<int>();
        var gate = Gate<int>();
        Func<int, int> mark = value => { events.Enqueue(value); return value; };
        Func<int, Task<int>> step = value =>
        {
            events.Enqueue(100 + value);
            return Task.FromResult(value);
        };
        var method = Method(type, "visitAwaitedSource");
        var parameters = method.GetParameters();
        var result = (Task<int>)method.Invoke(null,
            new object[] { Adapt(parameters[0].ParameterType, mark), gate.Task, Adapt(parameters[2].ParameterType, step) })!;
        Assert.False(result.IsCompleted);
        Assert.Equal(new[] { 0 }, events.ToArray());
        gate.SetResult(0);
        Assert.Equal(42, await result.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(new[] { 0, 1, 101, 2, 102, 3, 103 }, events.ToArray());
    }

    private static async Task StopsAfterFault(Type type)
    {
        var events = new ConcurrentQueue<int>();
        var secondStarted = Gate<int>();
        var first = Gate<int>();
        var second = Gate<int>();
        Func<int, int> mark = value => { events.Enqueue(value); return value; };
        Func<int, Task<int>> step = value =>
        {
            events.Enqueue(100 + value);
            if (value == 2) secondStarted.TrySetResult(value);
            return value == 1 ? first.Task : second.Task;
        };
        var result = Call(Method(type, "visit"), mark, step);
        first.SetResult(11);
        Assert.Equal(2, await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(10)));
        var failure = new InvalidOperationException("controlled failure");
        second.SetException(failure);
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(async () => await result.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Same(failure, actual);
        Assert.Equal(new[] { 0, 101, 11, 102 }, events.ToArray());
    }

    private static async Task StopsAfterCancellation(Type type)
    {
        var events = new ConcurrentQueue<int>();
        var secondStarted = Gate<int>();
        var first = Gate<int>();
        var second = Gate<int>();
        Func<int, int> mark = value => { events.Enqueue(value); return value; };
        Func<int, Task<int>> step = value =>
        {
            events.Enqueue(100 + value);
            if (value == 2) secondStarted.TrySetResult(value);
            return value == 1 ? first.Task : second.Task;
        };
        var result = Call(Method(type, "visit"), mark, step);
        first.SetResult(11);
        Assert.Equal(2, await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(10)));
        second.SetCanceled();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await result.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.True(result.IsCanceled);
        Assert.Equal(new[] { 0, 101, 11, 102 }, events.ToArray());
    }

    private static TaskCompletionSource<T> Gate<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static MethodInfo Method(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
    private static Task<int> Call(MethodInfo method, Func<int, int> mark, Func<int, Task<int>> step)
    {
        var types = method.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
        return (Task<int>)method.Invoke(null, new object[] { Adapt(types[0], mark), Adapt(types[1], step) })!;
    }
    private static Delegate Adapt(Type delegateType, Delegate callback)
    {
        var value = Expression.Parameter(typeof(int), "value");
        return Expression.Lambda(delegateType, Expression.Invoke(Expression.Constant(callback), value), value).Compile();
    }

    private static async Task WithGenerated(string source, Func<Type, Task> verify)
    {
        var compilation = DSharpCompiler.Compile(source, "async-for-sequence.ds");
        Assert.True(compilation.CSharp is not null, string.Join("\n", compilation.Diagnostics));
        Assert.Empty(compilation.Diagnostics);
        var assemblyName = "DSharpAsyncFor_" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-async-for-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "Generated.cs"), compilation.CSharp!);
            await File.WriteAllTextAsync(Path.Combine(directory, "Generated.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net11.0</TargetFramework><AssemblyName>" + assemblyName + "</AssemblyName><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>");
            await File.WriteAllTextAsync(Path.Combine(directory, "global.json"), "{\"sdk\":{\"version\":\"11.0.100\",\"rollForward\":\"disable\"}}");
            await File.WriteAllTextAsync(Path.Combine(directory, "NuGet.Config"), "<configuration><packageSources><clear/></packageSources></configuration>");
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "build", "Generated.csproj", "--nologo", "--verbosity", "quiet" }) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated async-for build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("async-for-" + Guid.NewGuid(), isCollectible: false);
            var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0", assemblyName + ".dll"));
            await verify(Assert.Single(assembly.GetTypes(), type => type.Name == "Demo"));
        }
        finally { Directory.Delete(directory, true); }
    }
}
