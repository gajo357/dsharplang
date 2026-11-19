using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class AsyncResultObserversTests
{
    public async Task AsyncObserversPreserveValuesTimingAndErrors()
    {
        const string source = """
            module Demo

            fn good(): Result<int, int> { Success(41) }
            fn bad(): Result<int, int> { Error(7) }
            fn nullValue(): Result<string?, int> { Success(null) }
            fn optionNone(): Result<Option<int?>, int> { Success(None) }
            fn optionNull(): Result<Option<int?>, int> { Success(Some(null)) }
            fn optionValue(): Result<Option<int?>, int> { Success(Some(41)) }
            fn nestedValue(): Result<Result<int, int>, int> { Success(Error(8)) }
            fn source(mark: (int) -> int, pending: Task<Result<int, int>>): Task<Result<int, int>> {
                let ignored = mark(1)
                pending
            }
            fn plusOne(value: int): int { value + 1 }
            fn ignore(value: int): Unit { () }
            fn errorText(value: int): string { "changed" }
            fn converted(pending: Task<Result<int, int>>): Task<Result<int, string>> { pending |> mapError(errorText) }
            async fn convertedCode(pending: Task<Result<int, string>>): string {
                match (await pending)
                {
                    Success(value) => "success"
                    Error(error) => error
                }
            }
            fn ignoreNullable(value: string?): Unit { () }
            fn ignoreOption(value: Option<int?>): Unit { () }
            fn ignoreNested(value: Result<int, int>): Unit { () }
            fn direct(mark: (int) -> int, factory: () -> (int) -> int, pending: Task<Result<int, int>>): Task<Result<int, int>> {
                mapError(factory(), source(mark, pending))
            }
            fn piped(mark: (int) -> int, factory: () -> (int) -> int, pending: Task<Result<int, int>>): Task<Result<int, int>> {
                source(mark, pending) |> mapError(factory())
            }
            fn observe(mark: (int) -> int, factory: () -> (int) -> Unit, pending: Task<Result<int, int>>): Task<Result<int, int>> {
                source(mark, pending) |> tap(factory())
            }
            fn observeError(mark: (int) -> int, factory: () -> (int) -> Unit, pending: Task<Result<int, int>>): Task<Result<int, int>> {
                source(mark, pending) |> tapError(factory())
            }
            fn groupedMap(pending: Task<Result<int, int>>): Task<Result<int, int>> { mapError(plusOne)(pending) }
            fn groupedTap(pending: Task<Result<int, int>>): Task<Result<int, int>> { tap(ignore)(pending) }
            fn groupedTapError(pending: Task<Result<int, int>>): Task<Result<int, int>> { tapError(ignore)(pending) }
            fn directTap(pending: Task<Result<int, int>>): Task<Result<int, int>> { tap(ignore, pending) }
            fn directTapError(pending: Task<Result<int, int>>): Task<Result<int, int>> { tapError(ignore, pending) }
            fn explicitMap(pending: Task<Result<int, int>>): Task<Result<int, int>> { mapError<int, int, int>(plusOne, pending) }
            fn typedPartial(callback: (int) -> int): (Task<Result<int, int>>) -> Task<Result<int, int>> {
                let f: (Task<Result<int, int>>) -> Task<Result<int, int>> = mapError(callback)
                f
            }
            fn typedBare(callback: (int) -> Unit, pending: Task<Result<int, int>>): Task<Result<int, int>> {
                let f: ((int) -> Unit, Task<Result<int, int>>) -> Task<Result<int, int>> = tap
                f(callback, pending)
            }
            fn typedEmpty(callback: (int) -> Unit, pending: Task<Result<int, int>>): Task<Result<int, int>> {
                let f: ((int) -> Unit, Task<Result<int, int>>) -> Task<Result<int, int>> = tapError()
                f(callback, pending)
            }
            fn generic<T>(pending: Task<Result<T, int>>, callback: (int) -> int): Task<Result<T, int>> {
                pending |> mapError(callback)
            }
            fn nullable(pending: Task<Result<string?, int>>): Task<Result<string?, int>> { pending |> tap(ignoreNullable) }
            fn convertNull(pending: Task<Result<string?, int>>): Task<Result<string?, string>> { pending |> mapError(errorText) }
            async fn convertNullCode(pending: Task<Result<string?, string>>): bool {
                match (await pending)
                {
                    Success(null) => true
                    Success(value) => false
                    Error(error) => false
                }
            }
            fn option(pending: Task<Result<Option<int?>, int>>): Task<Result<Option<int?>, int>> { pending |> tap(ignoreOption) }
            fn nested(pending: Task<Result<Result<int, int>, int>>): Task<Result<Result<int, int>, int>> { pending |> tap(ignoreNested) }
            async fn code(pending: Task<Result<int, int>>): int {
                match (await pending)
                {
                    Success(value) => value
                    Error(error) => 100 + error
                }
            }
            async fn nullCode(pending: Task<Result<string?, int>>): bool {
                match (await pending)
                {
                    Success(null) => true
                    Success(value) => false
                    Error(error) => false
                }
            }
            async fn optionCode(pending: Task<Result<Option<int?>, int>>): int {
                match (await pending)
                {
                    Success(None) => 1
                    Success(Some(null)) => 2
                    Success(Some(value)) => value + 1
                    Error(error) => 0
                }
            }
            async fn nestedCode(pending: Task<Result<Result<int, int>, int>>): int {
                match (await pending)
                {
                    Success(Success(value)) => value
                    Success(Error(error)) => error
                    Error(error) => 0
                }
            }
            fn sync(): int {
                let value = Success<int, int>(41) |> tap(ignore) |> mapError(plusOne) |> tapError(ignore)
                match value
                {
                    Success(item) => item + 1
                    Error(error) => error
                }
            }
            """;
        var compiled = DSharpCompiler.Compile(source, "async-result-observers.ds");
        Assert.True(compiled.CSharp is not null, string.Join("\n", compiled.Diagnostics));
        Assert.Empty(compiled.Diagnostics);
        await WithGenerated(compiled.CSharp!, "Demo", async type =>
        {
            Assert.Equal(42, Method(type, "sync").Invoke(null, null));
            var events = new ConcurrentQueue<int>();
            var direct = Method(type, "direct");
            var mark = AdaptInt(direct.GetParameters()[0].ParameterType, value => { events.Enqueue(value); return value; });
            var transform = AdaptInt(direct.GetParameters()[1].ParameterType.GetMethod("Invoke")!.ReturnType,
                value => { events.Enqueue(value); return value + 1; });
            var factory = AdaptFactory(direct.GetParameters()[1].ParameterType, () => { events.Enqueue(2); return transform; });
            var good = Method(type, "good").Invoke(null, null)!;
            var bad = Method(type, "bad").Invoke(null, null)!;
            var gate = Gate(direct.GetParameters()[2].ParameterType);
            var mapped = (Task)direct.Invoke(null, new object[] { mark, factory, gate.Task })!;
            Assert.False(mapped.IsCompleted);
            Assert.Equal(new[] { 2, 1 }, events.ToArray());
            gate.Complete(bad);
            Assert.Equal(108, await Decode(type, "code", mapped));
            Assert.Equal(new[] { 2, 1, 7 }, events.ToArray());

            events.Clear();
            gate = Gate(direct.GetParameters()[2].ParameterType);
            mapped = (Task)Method(type, "piped").Invoke(null, new object[] { mark, factory, gate.Task })!;
            Assert.Equal(new[] { 1, 2 }, events.ToArray());
            gate.Complete(good);
            Assert.Equal(41, await Decode(type, "code", mapped));
            Assert.Equal(new[] { 1, 2 }, events.ToArray());

            var observe = Method(type, "observe");
            var observer = AdaptUnit(observe.GetParameters()[1].ParameterType.GetMethod("Invoke")!.ReturnType,
                value => events.Enqueue(value));
            var observeFactory = AdaptFactory(observe.GetParameters()[1].ParameterType, () => { events.Enqueue(2); return observer; });
            foreach (var (name, input, expected, invokes) in new[]
            {
                ("observe", good, 41, true), ("observe", bad, 107, false),
                ("observeError", good, 41, false), ("observeError", bad, 107, true)
            })
            {
                events.Clear();
                gate = Gate(observe.GetParameters()[2].ParameterType);
                mapped = (Task)Method(type, name).Invoke(null, new object[] { mark, observeFactory, gate.Task })!;
                Assert.Equal(new[] { 1, 2 }, events.ToArray());
                Assert.False(mapped.IsCompleted);
                gate.Complete(input);
                Assert.Equal(expected, await Decode(type, "code", mapped));
                Assert.Equal(invokes ? new[] { 1, 2, name == "observe" ? 41 : 7 } : new[] { 1, 2 }, events.ToArray());
            }

            foreach (var name in new[] { "groupedMap", "explicitMap", "groupedTap", "groupedTapError", "directTap", "directTapError" })
            {
                var action = Method(type, name);
                gate = Gate(action.GetParameters()[0].ParameterType);
                mapped = (Task)action.Invoke(null, new[] { gate.Task })!;
                gate.Complete(name is "groupedMap" or "explicitMap" ? bad : good);
                Assert.Equal(name is "groupedMap" or "explicitMap" ? 108 : 41, await Decode(type, "code", mapped));
            }
            events.Clear();
            var partial = Assert.IsAssignableFrom<Delegate>(Method(type, "typedPartial").Invoke(null, new object[] { transform }));
            gate = Gate(partial.GetType().GetMethod("Invoke")!.GetParameters()[0].ParameterType);
            mapped = (Task)partial.DynamicInvoke(gate.Task)!;
            gate.Complete(bad);
            Assert.Equal(108, await Decode(type, "code", mapped));
            Assert.Equal(new[] { 7 }, events.ToArray());
            foreach (var (name, input, expected) in new[] { ("typedBare", good, 41), ("typedEmpty", bad, 107) })
            {
                events.Clear();
                var action = Method(type, name);
                gate = Gate(action.GetParameters()[1].ParameterType);
                mapped = (Task)action.Invoke(null, new object[] { observer, gate.Task })!;
                gate.Complete(input);
                Assert.Equal(expected, await Decode(type, "code", mapped));
                Assert.Equal(new[] { name == "typedBare" ? 41 : 7 }, events.ToArray());
            }
            gate = Gate(Method(type, "converted").GetParameters()[0].ParameterType);
            mapped = (Task)Method(type, "converted").Invoke(null, new[] { gate.Task })!;
            gate.Complete(bad);
            Assert.Equal("changed", await ((Task<string>)Method(type, "convertedCode").Invoke(null, new[] { mapped })!).WaitAsync(TimeSpan.FromSeconds(10)));
            gate = Gate(Method(type, "convertNull").GetParameters()[0].ParameterType);
            mapped = (Task)Method(type, "convertNull").Invoke(null, new[] { gate.Task })!;
            gate.Complete(Method(type, "nullValue").Invoke(null, null)!);
            Assert.True(await ((Task<bool>)Method(type, "convertNullCode").Invoke(null, new[] { mapped })!).WaitAsync(TimeSpan.FromSeconds(10)));
            var generic = Method(type, "generic").MakeGenericMethod(typeof(int));
            gate = Gate(generic.GetParameters()[0].ParameterType);
            var genericTransform = AdaptInt(generic.GetParameters()[1].ParameterType, value => value + 1);
            mapped = (Task)generic.Invoke(null, new object[] { gate.Task, genericTransform })!;
            gate.Complete(bad);
            Assert.Equal(108, await Decode(type, "code", mapped));

            foreach (var (name, producer, decoder, expected) in new[]
            {
                ("nullable", "nullValue", "nullCode", (object)true),
                ("option", "optionNone", "optionCode", (object)1),
                ("option", "optionNull", "optionCode", (object)2),
                ("option", "optionValue", "optionCode", (object)42),
                ("nested", "nestedValue", "nestedCode", (object)8)
            })
            {
                var action = Method(type, name);
                gate = Gate(action.GetParameters()[0].ParameterType);
                mapped = (Task)action.Invoke(null, new[] { gate.Task })!;
                gate.Complete(Method(type, producer).Invoke(null, null)!);
                var decoded = (Task)Method(type, decoder).Invoke(null, new[] { mapped })!;
                await decoded.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(expected, decoded.GetType().GetProperty("Result")!.GetValue(decoded));
            }

            foreach (var name in new[] { "piped", "observe", "observeError" })
            {
                events.Clear();
                var action = Method(type, name);
                gate = Gate(action.GetParameters()[2].ParameterType);
                var callbackFactory = name == "piped" ? factory : observeFactory;
                mapped = (Task)action.Invoke(null, new object[] { mark, callbackFactory, gate.Task })!;
                var fault = new InvalidOperationException("source failed");
                gate.Fail(fault);
                Assert.Same(fault, await Assert.ThrowsAsync<InvalidOperationException>(async () => await mapped));
                Assert.Equal(new[] { 1, 2 }, events.ToArray());
                events.Clear();
                gate = Gate(action.GetParameters()[2].ParameterType);
                mapped = (Task)action.Invoke(null, new object[] { mark, callbackFactory, gate.Task })!;
                gate.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await mapped);
                Assert.True(mapped.IsCanceled);
                Assert.Equal(new[] { 1, 2 }, events.ToArray());
            }
            foreach (var name in new[] { "piped", "observe", "observeError" })
            {
                events.Clear();
                var action = Method(type, name);
                var failure = new InvalidOperationException("callback failed");
                Delegate callback = name == "piped" ? AdaptInt(transform.GetType(), value => throw failure) : AdaptUnit(observer.GetType(), value => throw failure);
                var badFactory = AdaptFactory(action.GetParameters()[1].ParameterType, () => callback);
                gate = Gate(action.GetParameters()[2].ParameterType);
                mapped = (Task)action.Invoke(null, new object[] { mark, badFactory, gate.Task })!;
                gate.Complete(name == "observe" ? good : bad);
                Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(async () => await mapped));
            }
        });
        await RejectInvalidAndVerifyShadowing();
    }

    private static async Task RejectInvalidAndVerifyShadowing()
    {
        string[] invalid =
        [
            "fn wrong(value: string): int { 1 }\nfn bad(input: Task<Result<int, int>>): Task<Result<int, int>> { input |> mapError(wrong) }",
            "fn wrong(value: int): int { value }\nfn bad(input: Task<Result<int, int>>): Task<Result<int, int>> { input |> tap(wrong) }",
            "fn wrong(value: int): int { value }\nfn bad(input: Task<Result<int, int>>): Task<Result<int, int>> { input |> tapError(wrong) }",
            "async fn wrong(value: int): Unit { () }\nfn bad(input: Task<Result<int, int>>): Task<Result<int, int>> { input |> tap(wrong) }",
            "async fn wrong(value: int): Unit { () }\nfn bad(input: Task<Result<int, int>>): Task<Result<int, int>> { input |> tapError(wrong) }",
            "async fn wrong(value: int): int { value }\nfn bad(input: Task<Result<int, int>>): Task<Result<int, int>> { input |> mapError(wrong) }",
            "fn bad(input: Task<Result<int, int>>): int { input |> tap(value => ()) }",
            "fn bad(input: Task<Result<int, int>>): Task<Result<int, int>> { __dsharp_result_maperror_task(value => value, input) }",
            "fn bad(input: Task<Result<int, int>>): Task<Result<int, int>> { __dsharp_result_tap_task(value => (), input) }",
            "fn bad(input: Task<Result<int, int>>): Task<Result<int, int>> { __dsharp_result_taperror_task(value => (), input) }"
        ];
        foreach (var body in invalid)
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + body, "invalid-async-observers.ds");
            Assert.True(result.CSharp is null, "Expected rejection: " + body);
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, diagnostic => Assert.Equal("invalid-async-observers.ds", diagnostic.Path));
        }
        const string shadow = """
            module Shadow
            fn mapError(value: int): int { value + 1 }
            fn tap(value: int): int { value + 1 }
            fn tapError(value: int): int { value + 1 }
            fn answer(): int { 39 |> mapError |> tap |> tapError }
            """;
        var resultShadow = DSharpCompiler.Compile(shadow, "shadow-async-observers.ds");
        Assert.True(resultShadow.CSharp is not null, string.Join("\n", resultShadow.Diagnostics));
        await WithGenerated(resultShadow.CSharp!, "Shadow", type =>
        {
            Assert.Equal(42, Method(type, "answer").Invoke(null, null));
            return Task.CompletedTask;
        });

        const string localShadow = """
            module Shadow
            fn answer(): int {
                let mapError: (int) -> int = value => value + 1
                let tap: (int) -> int = value => value + 1
                let tapError: (int) -> int = value => value + 1
                39 |> mapError |> tap |> tapError
            }
            """;
        resultShadow = DSharpCompiler.Compile(localShadow, "shadow-local-observers.ds");
        Assert.True(resultShadow.CSharp is not null, string.Join("\n", resultShadow.Diagnostics));
        await WithGenerated(resultShadow.CSharp!, "Shadow", type =>
        {
            Assert.Equal(42, Method(type, "answer").Invoke(null, null));
            return Task.CompletedTask;
        });

        var custom = new SourceFile("custom.ds", "module Custom\nfn mapError(value: int): int { value + 1 }\nfn tap(value: int): int { value + 1 }\nfn tapError(value: int): int { value + 1 }");
        var app = new SourceFile("app.ds", "module App\nimport Custom\nfn answer(): int { 39 |> mapError |> tap |> tapError }");
        var imported = DSharpCompiler.Compile(new[] { custom, app });
        Assert.True(imported.CSharp is not null, string.Join("\n", imported.Diagnostics));
        await WithGenerated(imported.CSharp!, "App", type =>
        {
            Assert.Equal(42, Method(type, "answer").Invoke(null, null));
            return Task.CompletedTask;
        });

        var provider = new SourceFile("provider.ds", "module Provider\nasync fn source(): Result<int, int> { Error(40) }");
        var shadowTypes = new SourceFile("types.ds", """
            module App
            import Provider
            type Task { value: int }
            type Result { | Local(int) }
            fn __dsharp_result_maperror_task(): int { 1 }
            fn __dsharp_result_tap_task(): int { 2 }
            fn __dsharp_result_taperror_task(): int { 3 }
            fn increment(value: int): int { value + 1 }
            fn ignore(value: int): Unit { () }
            async fn answer(): int {
                let outcome = await (source() |> mapError(increment) |> tapError(ignore))
                match outcome
                {
                    Success(value) => value
                    Error(error) => error + 1
                }
            }
            fn collision(): int { __dsharp_result_maperror_task() + __dsharp_result_tap_task() + __dsharp_result_taperror_task() }
            """);
        imported = DSharpCompiler.Compile(new[] { provider, shadowTypes });
        Assert.True(imported.CSharp is not null, string.Join("\n", imported.Diagnostics));
        await WithGenerated(imported.CSharp!, "App", async type =>
        {
            Assert.Equal(42, await ((Task<int>)Method(type, "answer").Invoke(null, null)!).WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(6, Method(type, "collision").Invoke(null, null));
        });
    }

    private sealed record Pending(object Source, Task Task)
    {
        public void Complete(object value) => Source.GetType().GetMethod("SetResult")!.Invoke(Source, new[] { value });
        public void Fail(Exception error) => Source.GetType().GetMethod("SetException", new[] { typeof(Exception) })!.Invoke(Source, new object[] { error });
        public void Cancel() => Source.GetType().GetMethod("SetCanceled", Type.EmptyTypes)!.Invoke(Source, null);
    }
    private static Pending Gate(Type taskType)
    {
        var valueType = taskType.GetGenericArguments().Single();
        var sourceType = typeof(TaskCompletionSource<>).MakeGenericType(valueType);
        var source = Activator.CreateInstance(sourceType, TaskCreationOptions.RunContinuationsAsynchronously)!;
        return new(source, (Task)sourceType.GetProperty("Task")!.GetValue(source)!);
    }
    private static async Task<int> Decode(Type type, string method, Task mapped) =>
        await ((Task<int>)Method(type, method).Invoke(null, new[] { mapped })!).WaitAsync(TimeSpan.FromSeconds(10));
    private static MethodInfo Method(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
    private static Delegate AdaptInt(Type type, Func<int, int> callback)
    {
        var value = Expression.Parameter(typeof(int), "value");
        return Expression.Lambda(type, Expression.Invoke(Expression.Constant(callback), value), value).Compile();
    }
    private static Delegate AdaptUnit(Type type, Action<int> callback)
    {
        var value = Expression.Parameter(typeof(int), "value");
        var unit = type.GetMethod("Invoke")!.ReturnType;
        var body = Expression.Block(Expression.Invoke(Expression.Constant(callback), value), Expression.Default(unit));
        return Expression.Lambda(type, body, value).Compile();
    }
    private static Delegate AdaptFactory(Type type, Func<Delegate> callback)
    {
        var result = type.GetMethod("Invoke")!.ReturnType;
        return Expression.Lambda(type, Expression.Convert(Expression.Invoke(Expression.Constant(callback)), result)).Compile();
    }
    private static async Task WithGenerated(string csharp, string module, Func<Type, Task> verify)
    {
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-async-result-observers-" + Guid.NewGuid());
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
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated async Result observers build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("async-result-observers-" + Guid.NewGuid(), isCollectible: true);
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
