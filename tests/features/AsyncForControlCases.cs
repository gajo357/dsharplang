using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

public sealed class AsyncForControlCases
{
    public async Task Run()
    {
        const string source = """
            module Demo

            interface Tracker {
                mark(value: int): int
                async released(value: int): int
            }

            resource Lease(id: int, tracker: Tracker) {
                read(): int { id }
                release(): Unit {
                    let ignored = tracker.mark(id * 10)
                    ()
                }
            }


            resource AsyncLease(id: int, tracker: Tracker) {
                async release(): Unit {
                    let ignored = await tracker.released(id)
                    ()
                }
            }

            async fn asyncResourceLoop(tracker: Tracker, gate: Task<int>): int {
                for item in [1, 2] {
                    using lease = AsyncLease(item, tracker)
                    let before = tracker.mark(item)
                    let ignored = await gate
                }
                42
            }

            async fn asyncResourceEarly(tracker: Tracker, gate: Task<int>): int {
                for item in [1, 2] {
                    using lease = AsyncLease(item, tracker)
                    let before = tracker.mark(item)
                    let ignored = await gate
                    return 42
                }
                0
            }

            async fn nested(tick: (int)->Task<int>): int {
                let values = MutableList<int>()
                for row in [[1, 2], [3, 4]] {
                    for item in row {
                        let actual = await tick(item)
                        values.add(actual)
                    }
                }
                values.freeze().length()
            }

            fn makeLambda(): ((int)->Task<int>)->Task<int> {
                async (tick: (int)->Task<int>) => {
                    for item in [1, 2, 3] {
                        let value = await tick(item)
                        if value == 2 { return 40 }
                    }
                    0
                }
            }

            async fn lambdaOuter(tick: (int)->Task<int>): int {
                let run = makeLambda()
                let result = await run(tick)
                result + 2
            }

            async fn check(value: int, gate: Task<int>): Result<int,string> {
                let ignored = await gate
                if value == 2 { Error("stop") } else { Success(value) }
            }

            async fn propagated(mark: (int)->int, gate: Task<int>): Result<int,string> {
                for item in [1, 2, 3] {
                    let seen = mark(item)
                    let checked = (await check(seen, gate))?
                    ()
                }
                Success(42)
            }

            async fn propagatedOutput(mark: (int)->int, gate: Task<int>): int {
                let result = await propagated(mark, gate)
                match result
                {
                    Success(value) => value
                    Error(_) => -1
                }
            }

            async fn preserve<T>(values: List<T?>, gate: Task<int>): List<T?> {
                let builder = MutableList<T?>()
                for item in values {
                    let ignored = await gate
                    builder.add(item)
                }
                builder.freeze()
            }

            async fn nullableAnswer(gate: Task<int>): int {
                let values = await preserve<int?>([null, 42], gate)
                if values.get(0) == Some(null) {
                    match values.get(1)
                    {
                        Some(null) => 0
                        Some(value) => value
                        None => 0
                    }
                } else { 0 }
            }

            async fn resourceLoop(tracker: Tracker, gate: Task<int>): int {
                for item in [1, 2] {
                    using lease = Lease(item, tracker)
                    let before = tracker.mark(item)
                    let ignored = await gate
                    let value = lease.read()
                }
                42
            }
            """;
        var compiled = DSharpCompiler.Compile(source, "async-for-control.ds");
        Assert.True(compiled.CSharp is not null, string.Join("\n", compiled.Diagnostics));
        Assert.Empty(compiled.Diagnostics);
        await WithGenerated(compiled.CSharp!, async type =>
        {
            var events = new List<int>();
            var nestedMethod = Method(type, "nested");
            var firstGate = Gate();
            Func<int, Task<int>> firstTick = n =>
            {
                events.Add(n);
                return n == 1 ? firstGate.Task : Task.FromResult(n);
            };
            var tick = Adapt(nestedMethod.GetParameters()[0].ParameterType, firstTick);
            var nested = (Task<int>)nestedMethod.Invoke(null, new object[] { tick })!;
            Assert.False(nested.IsCompleted);
            Assert.Equal(new[] { 1 }, events);
            firstGate.SetResult(1);
            Assert.Equal(4, await nested.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(new[] { 1, 2, 3, 4 }, events);

            events.Clear();
            var lambdaMethod = Method(type, "lambdaOuter");
            Func<int, Task<int>> lambdaTick = n => { events.Add(n); return Task.FromResult(n); };
            var lambdaCallback = Adapt(lambdaMethod.GetParameters()[0].ParameterType, lambdaTick);
            var lambda = (Task<int>)lambdaMethod.Invoke(null, new object[] { lambdaCallback })!;
            Assert.Equal(42, await lambda.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(new[] { 1, 2 }, events);

            events.Clear();
            var propagateMethod = Method(type, "propagatedOutput");
            Func<int, int> mark = n => { events.Add(n); return n; };
            var markCallback = Adapt(propagateMethod.GetParameters()[0].ParameterType, mark);
            var propagationGate = Gate();
            var propagated = (Task<int>)propagateMethod.Invoke(null, new object[] { markCallback, propagationGate.Task })!;
            Assert.False(propagated.IsCompleted);
            Assert.Equal(new[] { 1 }, events);
            propagationGate.SetResult(0);
            Assert.Equal(-1, await propagated.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(new[] { 1, 2 }, events);

            var nullableGate = Gate();
            var nullable = (Task<int>)Method(type, "nullableAnswer").Invoke(null, new object[] { nullableGate.Task })!;
            Assert.False(nullable.IsCompleted);
            nullableGate.SetResult(0);
            Assert.Equal(42, await nullable.WaitAsync(TimeSpan.FromSeconds(10)));

            events.Clear();
            var resourceMethod = Method(type, "resourceLoop");
            var tracker = (TrackerProxy)DispatchProxy.Create(resourceMethod.GetParameters()[0].ParameterType, typeof(TrackerProxy));
            tracker.Mark = n => { events.Add(n); return n; };
            var resourceGate = Gate();
            var resource = (Task<int>)resourceMethod.Invoke(null, new object[] { tracker, resourceGate.Task })!;
            Assert.False(resource.IsCompleted);
            Assert.Equal(new[] { 1 }, events);
            resourceGate.SetResult(0);
            Assert.Equal(42, await resource.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(new[] { 1, 10, 2, 20 }, events);

            events.Clear();
            var canceledGate = Gate();
            var canceled = (Task<int>)resourceMethod.Invoke(null, new object[] { tracker, canceledGate.Task })!;
            Assert.Equal(new[] { 1 }, events);
            canceledGate.SetCanceled();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await canceled.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(new[] { 1, 10 }, events);

            events.Clear();
            var faultGate = Gate();
            var fault = (Task<int>)resourceMethod.Invoke(null, new object[] { tracker, faultGate.Task })!;
            Assert.Equal(new[] { 1 }, events);
            faultGate.SetException(new InvalidOperationException("loop fault"));
            var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await fault.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal("loop fault", error.Message);
            Assert.Equal(new[] { 1, 10 }, events);

            // Async release must finish before the next iteration or exit.
            foreach (var mode in new[] { "success", "early", "fault", "cancel", "cleanup-fault" })
            {
                events.Clear();
                var bodyGate = Gate();
                var cleanupGate = Gate();
                var cleanupStarted = Gate();
                tracker.Released = n =>
                {
                    events.Add(n * 10);
                    if (n == 1)
                    {
                        cleanupStarted.TrySetResult(n);
                        return cleanupGate.Task;
                    }
                    return Task.FromResult(0);
                };
                var name = mode == "early" ? "asyncResourceEarly" : "asyncResourceLoop";
                var pending = (Task<int>)Method(type, name).Invoke(null, new object[] { tracker, bodyGate.Task })!;
                Assert.False(pending.IsCompleted);
                Assert.Equal(new[] { 1 }, events);
                if (mode == "fault" || mode == "cleanup-fault") bodyGate.SetException(new InvalidOperationException("body fault"));
                else if (mode == "cancel") bodyGate.SetCanceled();
                else bodyGate.SetResult(0);
                Assert.Equal(1, await cleanupStarted.Task.WaitAsync(TimeSpan.FromSeconds(10)));
                Assert.False(pending.IsCompleted);
                Assert.Equal(new[] { 1, 10 }, events);
                if (mode == "cleanup-fault") cleanupGate.SetException(new ArgumentException("cleanup fault"));
                else cleanupGate.SetResult(0);
                if (mode == "success")
                {
                    Assert.Equal(42, await pending.WaitAsync(TimeSpan.FromSeconds(10)));
                    Assert.Equal(new[] { 1, 10, 2, 20 }, events);
                }
                else if (mode == "early")
                {
                    Assert.Equal(42, await pending.WaitAsync(TimeSpan.FromSeconds(10)));
                    Assert.Equal(new[] { 1, 10 }, events);
                }
                else if (mode == "fault")
                {
                    var bodyError = await Assert.ThrowsAsync<InvalidOperationException>(async () => await pending.WaitAsync(TimeSpan.FromSeconds(10)));
                    Assert.Equal("body fault", bodyError.Message);
                    Assert.Equal(new[] { 1, 10 }, events);
                }
                else if (mode == "cancel")
                {
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending.WaitAsync(TimeSpan.FromSeconds(10)));
                    Assert.Equal(new[] { 1, 10 }, events);
                }
                else
                {
                    var cleanupError = await Assert.ThrowsAsync<ArgumentException>(async () => await pending.WaitAsync(TimeSpan.FromSeconds(10)));
                    Assert.Equal("cleanup fault", cleanupError.Message);
                    Assert.Equal(new[] { 1, 10 }, events);
                }
            }
        });
        RejectInvalid();
    }

    private static void RejectInvalid()
    {
        string[] invalid =
        [
            "fn bad(gate: Task<int>): Unit { for item in [1] { let value = await gate\n () }\n () }",
            "async fn bad(gate: Task<int>): Unit { for item in [1] { let value = await gate\n value }\n () }",
            "async fn bad(gate: Task<int>): (int)->Unit { let values = MutableList<int>()\n for item in [1] { let ignored = await gate\n values.add(item) }\n (value:int) => values.add(value) }",
            "resource Lease { read(): int { 42 }\n release(): Unit { () } }\nasync fn bad(gate: Task<int>): Unit { using lease = Lease()\n for item in [1] { let ignored = await gate\n let escape: ()->int = () => lease.read()\n () }\n () }"
        ];
        foreach (var body in invalid)
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + body, "async-for-invalid.ds");
            Assert.True(result.CSharp is null, "Expected rejection: " + body);
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, diagnostic => Assert.Equal("async-for-invalid.ds", diagnostic.Path));
        }
    }

    public class TrackerProxy : DispatchProxy
    {
        public Func<int, int> Mark { get; set; } = _ => 0;
        public Func<int, Task<int>> Released { get; set; } = _ => Task.FromResult(0);
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? arguments) => targetMethod!.Name switch
        {
            "__dsharp_method_mark" => Mark((int)arguments![0]!),
            "__dsharp_method_released" => Released((int)arguments![0]!),
            _ => throw new InvalidOperationException(targetMethod.Name)
        };
    }

    private static TaskCompletionSource<int> Gate() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static MethodInfo Method(Type type, string name) => type.GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
    private static Delegate Adapt(Type delegateType, Delegate callback)
    {
        var value = Expression.Parameter(typeof(int), "value");
        return Expression.Lambda(delegateType, Expression.Invoke(Expression.Constant(callback), value), value).Compile();
    }

    private static async Task WithGenerated(string csharp, Func<Type, Task> verify)
    {
        var assemblyName = "DSharpAsyncForControl_" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-async-for-control-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "Generated.cs"), csharp);
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
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated async loop build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("async-for-control-" + Guid.NewGuid(), isCollectible: false);
            var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0", assemblyName + ".dll"));
            await verify(Assert.Single(assembly.GetTypes(), type => type.Name == "Demo"));
        }
        finally { Directory.Delete(directory, true); }
    }
}
