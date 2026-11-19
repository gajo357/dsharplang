using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class AsyncResultAndThenTests
{
    public async Task AndThenPreservesAllResultAndTaskCombinations()
    {
        const string source = """
            module Demo

            fn good(): Result<int, string> { Success(41) }
            fn bad(): Result<int, string> { Error("failed") }
            fn next(value: int): Result<int, string> { Success(value + 1) }
            async fn asyncNext(value: int): Result<int, string> { Success(value + 1) }
            fn none(): Result<Option<int?>, string> { Success(None) }
            fn nullSome(): Result<Option<int?>, string> { Success(Some(null)) }
            fn numberSome(): Result<Option<int?>, string> { Success(Some(41)) }
            fn classify(value: Option<int?>): Result<int, string> {
                match value
                {
                    None => Success(1)
                    Some(null) => Success(2)
                    Some(number) => Success(number + 1)
                }
            }
            fn markSource(mark: (int) -> int, pending: Task<Result<int, string>>): Task<Result<int, string>> {
                let ignored = mark(1)
                pending
            }
            fn synchronous(): Result<int, string> { good() |> andThen(next) }
            fn resultAsync(): Task<Result<int, string>> { good() |> andThen(asyncNext) }
            fn resultAsyncError(): Task<Result<int, string>> { bad() |> andThen(asyncNext) }
            fn inferred() { good() |> andThen(value => Success(value + 1)) }
            fn resultAsyncCallback(input: Result<int, string>, callback: (int) -> Task<Result<int, string>>): Task<Result<int, string>> {
                input |> andThen(callback)
            }
            fn taskSync(pending: Task<Result<int, string>>): Task<Result<int, string>> { pending |> andThen(next) }
            fn taskAsync(pending: Task<Result<int, string>>, callback: (int) -> Task<Result<int, string>>): Task<Result<int, string>> {
                pending |> andThen(callback)
            }
            fn taskAsyncDirect(mark: (int) -> int, factory: () -> (int) -> Task<Result<int, string>>, pending: Task<Result<int, string>>): Task<Result<int, string>> {
                andThen(factory(), markSource(mark, pending))
            }
            fn taskAsyncPipe(mark: (int) -> int, factory: () -> (int) -> Task<Result<int, string>>, pending: Task<Result<int, string>>): Task<Result<int, string>> {
                markSource(mark, pending) |> andThen(factory())
            }
            fn grouped(pending: Task<Result<int, string>>): Task<Result<int, string>> { andThen(asyncNext)(pending) }
            fn explicitArgs(pending: Task<Result<int, string>>): Task<Result<int, string>> { andThen<int, int, string>(asyncNext, pending) }
            fn lambda(pending: Task<Result<int, string>>): Task<Result<int, string>> {
                pending |> andThen(async value => Success(value + 1))
            }
            fn typedPartial(callback: (int) -> Task<Result<int, string>>): (Task<Result<int, string>>) -> Task<Result<int, string>> {
                let f: (Task<Result<int, string>>) -> Task<Result<int, string>> = andThen(callback)
                f
            }
            fn typedBare(pending: Task<Result<int, string>>, callback: (int) -> Task<Result<int, string>>): Task<Result<int, string>> {
                let f: ((int) -> Task<Result<int, string>>, Task<Result<int, string>>) -> Task<Result<int, string>> = andThen
                f(callback, pending)
            }
            fn generic<T>(pending: Task<Result<T, string>>, callback: (T) -> Result<int, string>): Task<Result<int, string>> {
                pending |> andThen(callback)
            }
            fn option(pending: Task<Result<Option<int?>, string>>): Task<Result<int, string>> { pending |> andThen(classify) }
            fn nullValue(): Result<string?, string> { Success(null) }
            fn nullNext(value: string?): Result<int, string> { if value == null { Success(42) } else { Success(0) } }
            fn nullCase(pending: Task<Result<string?, string>>): Task<Result<int, string>> { pending |> andThen(nullNext) }
            fn wrap(value: int): Result<Result<int, string>, string> { Success(Success(value)) }
            fn nested(pending: Task<Result<int, string>>): Task<Result<Result<int, string>, string>> { pending |> andThen(wrap) }
            fn codeSync(value: Result<int, string>): int {
                match value
                {
                    Success(number) => number
                    Error(error) => -1
                }
            }
            async fn code(pending: Task<Result<int, string>>): int { codeSync(await pending) }
            async fn nestedCode(pending: Task<Result<Result<int, string>, string>>): int {
                match (await pending)
                {
                    Success(Success(number)) => number
                    Success(Error(error)) => -2
                    Error(error) => -1
                }
            }
            """;
        var compiled = DSharpCompiler.Compile(source, "async-andthen.ds");
        Assert.True(compiled.CSharp is not null, string.Join("\n", compiled.Diagnostics));
        Assert.Empty(compiled.Diagnostics);
        await WithGenerated(compiled.CSharp!, "Demo", async type =>
        {
            var good = Method(type, "good").Invoke(null, null)!;
            var bad = Method(type, "bad").Invoke(null, null)!;
            Assert.Equal(42, Method(type, "codeSync").Invoke(null, new[] { Method(type, "synchronous").Invoke(null, null) }));
            Assert.Equal(42, await Decode(type, (Task)Method(type, "resultAsync").Invoke(null, null)!));
            Assert.Equal(-1, await Decode(type, (Task)Method(type, "resultAsyncError").Invoke(null, null)!));
            Assert.Equal(42, Method(type, "codeSync").Invoke(null, new[] { Method(type, "inferred").Invoke(null, null) }));
            foreach (var name in new[] { "taskSync", "grouped", "explicitArgs", "lambda" })
            {
                var method = Method(type, name);
                var input = Gate(method.GetParameters()[0].ParameterType);
                var result = (Task)method.Invoke(null, new[] { input.Task })!;
                Assert.False(result.IsCompleted);
                input.Complete(good);
                Assert.Equal(42, await Decode(type, result));
            }

            {
                var action = Method(type, "resultAsyncCallback");
                var sourceType = Method(type, "taskSync").GetParameters()[0].ParameterType;
                var singleCallbackGate = Gate(sourceType);
                var invoked = new List<int>();
                var singleCallback = Adapt(action.GetParameters()[1].ParameterType, value => { invoked.Add(value); return singleCallbackGate.Task; });
                var absent = (Task)action.Invoke(null, new object[] { bad, singleCallback })!;
                Assert.Equal(-1, await Decode(type, absent));
                Assert.Empty(invoked);
                var present = (Task)action.Invoke(null, new object[] { good, singleCallback })!;
                Assert.Equal(new[] { 41 }, invoked);
                Assert.False(present.IsCompleted);
                singleCallbackGate.Complete(Method(type, "next").Invoke(null, new[] { (object)41 })!);
                Assert.Equal(42, await Decode(type, present));
            }

            var taskAsync = Method(type, "taskAsync");
            var sourceGate = Gate(taskAsync.GetParameters()[0].ParameterType);
            var callbackGate = Gate(taskAsync.GetParameters()[0].ParameterType);
            var events = new ConcurrentQueue<int>();
            var callback = Adapt(taskAsync.GetParameters()[1].ParameterType, value => { events.Enqueue(value); return callbackGate.Task; });
            var mapped = (Task)taskAsync.Invoke(null, new object[] { sourceGate.Task, callback })!;
            Assert.False(mapped.IsCompleted);
            Assert.Empty(events);
            sourceGate.Complete(good);
            await WaitFor(() => events.Count != 0);
            Assert.Equal(new[] { 41 }, events);
            Assert.False(mapped.IsCompleted);
            callbackGate.Complete(Method(type, "next").Invoke(null, new[] { (object)41 })!);
            Assert.Equal(42, await Decode(type, mapped));

            events.Clear();
            sourceGate = Gate(taskAsync.GetParameters()[0].ParameterType);
            callbackGate = Gate(taskAsync.GetParameters()[0].ParameterType);
            callback = Adapt(taskAsync.GetParameters()[1].ParameterType, value => { events.Enqueue(value); return callbackGate.Task; });
            mapped = (Task)taskAsync.Invoke(null, new object[] { sourceGate.Task, callback })!;
            sourceGate.Complete(bad);
            Assert.Equal(-1, await Decode(type, mapped));
            Assert.Empty(events);

            // Typed partial and bare values choose the same async variant.
            foreach (var name in new[] { "typedPartial", "typedBare" })
            {
                events.Clear();
                var method = Method(type, name);
                var parameter = name == "typedPartial" ? method.GetParameters()[0].ParameterType : method.GetParameters()[1].ParameterType;
                var gateType = name == "typedPartial" ? method.ReturnType.GetMethod("Invoke")!.GetParameters()[0].ParameterType : method.GetParameters()[0].ParameterType;
                var source = Gate(gateType);
                var callbackSource = Gate(gateType);
                var next = Adapt(parameter, value => { events.Enqueue(value); return callbackSource.Task; });
                if (name == "typedPartial")
                {
                    var partial = Assert.IsAssignableFrom<Delegate>(method.Invoke(null, new object[] { next }));
                    mapped = (Task)partial.DynamicInvoke(source.Task)!;
                }
                else mapped = (Task)method.Invoke(null, new object[] { source.Task, next })!;
                source.Complete(good);
                await WaitFor(() => events.Count != 0);
                callbackSource.Complete(Method(type, "next").Invoke(null, new[] { (object)41 })!);
                Assert.Equal(42, await Decode(type, mapped));
            }
            {
                var generic = Method(type, "generic").MakeGenericMethod(typeof(int));
                var gate = Gate(generic.GetParameters()[0].ParameterType);
                var next = Adapt(generic.GetParameters()[1].ParameterType, value => Method(type, "next").Invoke(null, new[] { (object)value })!);
                mapped = (Task)generic.Invoke(null, new object[] { gate.Task, next })!;
                gate.Complete(good);
                Assert.Equal(42, await Decode(type, mapped));
            }

            // Errors produced by the next step remain typed errors.
            sourceGate = Gate(taskAsync.GetParameters()[0].ParameterType);
            callbackGate = Gate(taskAsync.GetParameters()[0].ParameterType);
            callback = Adapt(taskAsync.GetParameters()[1].ParameterType, _ => callbackGate.Task);
            mapped = (Task)taskAsync.Invoke(null, new object[] { sourceGate.Task, callback })!;
            sourceGate.Complete(good);
            callbackGate.Complete(bad);
            Assert.Equal(-1, await Decode(type, mapped));

            foreach (var (methodName, expectedOrder) in new[] { ("taskAsyncDirect", new[] { 2, 1 }), ("taskAsyncPipe", new[] { 1, 2 }) })
            {
                events.Clear();
                var method = Method(type, methodName);
                var source = Gate(method.GetParameters()[2].ParameterType);
                var mark = Adapt(method.GetParameters()[0].ParameterType, value => { events.Enqueue(value); return value; });
                var next = Adapt(method.GetParameters()[1].ParameterType.GetMethod("Invoke")!.ReturnType, value => { events.Enqueue(value); return Task.FromResult(good); });
                var factory = AdaptFactory(method.GetParameters()[1].ParameterType, () => { events.Enqueue(2); return next; });
                mapped = (Task)method.Invoke(null, new object[] { mark, factory, source.Task })!;
                Assert.Equal(expectedOrder, events);
                source.Complete(bad);
                Assert.Equal(-1, await Decode(type, mapped));
                Assert.Equal(expectedOrder, events);
            }

            foreach (var faultInput in new[] { true, false })
            {
                events.Clear();
                sourceGate = Gate(taskAsync.GetParameters()[0].ParameterType);
                callbackGate = Gate(taskAsync.GetParameters()[0].ParameterType);
                callback = Adapt(taskAsync.GetParameters()[1].ParameterType, value => { events.Enqueue(value); return callbackGate.Task; });
                mapped = (Task)taskAsync.Invoke(null, new object[] { sourceGate.Task, callback })!;
                var exception = new InvalidOperationException("expected fault");
                if (faultInput) sourceGate.Fail(exception);
                else { sourceGate.Complete(good); await WaitFor(() => events.Count != 0); callbackGate.Fail(exception); }
                Assert.Same(exception, await Assert.ThrowsAsync<InvalidOperationException>(async () => await mapped));
                Assert.Equal(faultInput ? Array.Empty<int>() : new[] { 41 }, events);
            }
            foreach (var cancelInput in new[] { true, false })
            {
                events.Clear();
                sourceGate = Gate(taskAsync.GetParameters()[0].ParameterType);
                callbackGate = Gate(taskAsync.GetParameters()[0].ParameterType);
                callback = Adapt(taskAsync.GetParameters()[1].ParameterType, value => { events.Enqueue(value); return callbackGate.Task; });
                mapped = (Task)taskAsync.Invoke(null, new object[] { sourceGate.Task, callback })!;
                if (cancelInput) sourceGate.Cancel();
                else { sourceGate.Complete(good); await WaitFor(() => events.Count != 0); callbackGate.Cancel(); }
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await mapped);
                Assert.True(mapped.IsCanceled);
                Assert.Equal(cancelInput ? Array.Empty<int>() : new[] { 41 }, events);
            }

            foreach (var (name, factory, expected) in new[] { ("option", "none", 1), ("option", "nullSome", 2), ("option", "numberSome", 42), ("nullCase", "nullValue", 42) })
            {
                var method = Method(type, name);
                var gate = Gate(method.GetParameters()[0].ParameterType);
                mapped = (Task)method.Invoke(null, new[] { gate.Task })!;
                gate.Complete(Method(type, factory).Invoke(null, null)!);
                Assert.Equal(expected, await Decode(type, mapped));
            }
            var nested = Method(type, "nested");
            sourceGate = Gate(nested.GetParameters()[0].ParameterType);
            mapped = (Task)nested.Invoke(null, new[] { sourceGate.Task })!;
            sourceGate.Complete(good);
            Assert.Equal(41, await ((Task<int>)Method(type, "nestedCode").Invoke(null, new[] { mapped })!).WaitAsync(TimeSpan.FromSeconds(10)));
        });
        RejectInvalid();
        await Shadowing();
        await ImportedAndLocalShadowing();
    }

    private static void RejectInvalid()
    {
        string[] invalid =
        [
            "fn wrong(value: string): Result<int, string> { Success(1) }\nfn bad(input: Task<Result<int, string>>): Task<Result<int, string>> { input |> andThen(wrong) }",
            "fn wrong(value: int): Result<int, int> { Success(value) }\nfn bad(input: Task<Result<int, string>>): Task<Result<int, string>> { input |> andThen(wrong) }",
            "async fn next(value: int): Result<int, string> { Success(value) }\nfn bad(input: Task<Result<int, string>>): int { input |> andThen(next) }",
            "fn bad(input: Task<Result<int, string>>): Task<Result<int, string>> { __dsharp_result_andthen_task(value => Success(value), input) }",
            "fn bad(input: Task<Result<int, string>>): Task<Result<int, string>> { __dsharp_result_andthen_async(value => Success(value), input) }",
            "fn bad(input: Task<Result<int, string>>): Task<Result<int, string>> { __dsharp_result_andthen_task_async(value => Success(value), input) }"
        ];
        foreach (var body in invalid)
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + body, "invalid-andthen.ds");
            Assert.True(result.CSharp is null, "Expected rejection: " + body);
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, diagnostic => Assert.Equal("invalid-andthen.ds", diagnostic.Path));
        }
    }

    private static async Task Shadowing()
    {
        const string source = """
            module Shadow
            fn andThen(value: int): int { value + 1 }
            fn __dsharp_result_andthen_async(value: int): int { value + 1 }
            fn __dsharp_result_andthen_task(value: int): int { value + 1 }
            fn __dsharp_result_andthen_task_async(value: int): int { value + 1 }
            fn answer(): int { 41 |> andThen }
            fn collision(): int { __dsharp_result_andthen_async(41) }
            """;
        var compiled = DSharpCompiler.Compile(source, "andthen-shadow.ds");
        Assert.True(compiled.CSharp is not null, string.Join("\n", compiled.Diagnostics));
        await WithGenerated(compiled.CSharp!, "Shadow", type =>
        {
            Assert.Equal(42, Method(type, "answer").Invoke(null, null));
            Assert.Equal(42, Method(type, "collision").Invoke(null, null));
            return Task.CompletedTask;
        });
    }

    private static async Task ImportedAndLocalShadowing()
    {
        const string local = """
            module Local
            fn answer(): int {
                let andThen: (int) -> int = value => value + 1
                41 |> andThen
            }
            """;
        var result = DSharpCompiler.Compile(local, "local-andthen.ds");
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        await WithGenerated(result.CSharp!, "Local", type => { Assert.Equal(42, Method(type, "answer").Invoke(null, null)); return Task.CompletedTask; });
        var lib = new SourceFile("helper.ds", "module Helper\nfn andThen(value: int): int { value + 1 }");
        var app = new SourceFile("app.ds", "module App\nimport Helper\nfn answer(): int { 41 |> andThen }");
        result = DSharpCompiler.Compile(new[] { lib, app });
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        await WithGenerated(result.CSharp!, "App", type => { Assert.Equal(42, Method(type, "answer").Invoke(null, null)); return Task.CompletedTask; });
    }

    private sealed record Pending(object Source, Task Task)
    {
        public void Complete(object value) => Source.GetType().GetMethod("SetResult")!.Invoke(Source, new[] { value });
        public void Fail(Exception error) => Source.GetType().GetMethod("SetException", new[] { typeof(Exception) })!.Invoke(Source, new object[] { error });
        public void Cancel() => Source.GetType().GetMethod("SetCanceled", Type.EmptyTypes)!.Invoke(Source, null);
    }

    private static Pending Gate(Type taskType)
    {
        var sourceType = typeof(TaskCompletionSource<>).MakeGenericType(taskType.GetGenericArguments().Single());
        var source = Activator.CreateInstance(sourceType, TaskCreationOptions.RunContinuationsAsynchronously)!;
        return new(source, (Task)sourceType.GetProperty("Task")!.GetValue(source)!);
    }
    private static async Task WaitFor(Func<bool> predicate)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!predicate())
        {
            if (DateTime.UtcNow >= deadline) throw new TimeoutException("Expected callback was not invoked.");
            await Task.Delay(10);
        }
    }
    private static async Task<int> Decode(Type type, Task mapped) =>
        await ((Task<int>)Method(type, "code").Invoke(null, new[] { mapped })!).WaitAsync(TimeSpan.FromSeconds(10));
    private static MethodInfo Method(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
    private static Delegate Adapt(Type delegateType, Func<int, object> callback)
    {
        var parameter = Expression.Parameter(typeof(int), "value");
        var output = delegateType.GetMethod("Invoke")!.ReturnType;
        return Expression.Lambda(delegateType, Expression.Convert(Expression.Invoke(Expression.Constant(callback), parameter), output), parameter).Compile();
    }
    private static Delegate AdaptFactory(Type delegateType, Func<Delegate> callback)
    {
        var output = delegateType.GetMethod("Invoke")!.ReturnType;
        return Expression.Lambda(delegateType, Expression.Convert(Expression.Invoke(Expression.Constant(callback)), output)).Compile();
    }
    private static async Task WithGenerated(string csharp, string module, Func<Type, Task> verify)
    {
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-async-result-andthen-" + Guid.NewGuid());
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
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated async Result andThen build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("async-result-andthen-" + Guid.NewGuid(), isCollectible: true);
            try
            {
                var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0/Generated.dll"));
                await verify(Assert.Single(assembly.GetTypes(), candidate => candidate.Name == module));
            }
            finally { context.Unload(); }
        }
        finally { Directory.Delete(directory, true); }
    }
}
