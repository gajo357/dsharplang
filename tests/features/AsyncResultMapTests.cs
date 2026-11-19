using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class AsyncResultMapTests
{
    public async Task TaskResultMapPreservesCompositionAndEffects()
    {
        const string source = """
            module Demo

            fn good(): Result<int, string> { Success(41) }
            fn bad(): Result<int, string> { Error("bad") }
            fn nullValue(): Result<string?, string> { Success(null) }
            fn optionNone(): Result<Option<int?>, string> { Success(None) }
            fn optionNull(): Result<Option<int?>, string> { Success(Some(null)) }
            fn optionValue(): Result<Option<int?>, string> { Success(Some(41)) }

            fn source(mark: (int) -> int, value: Task<Result<int, string>>): Task<Result<int, string>> {
                let ignored = mark(1)
                value
            }
            fn direct(mark: (int) -> int, factory: () -> (int) -> int, pending: Task<Result<int, string>>): Task<Result<int, string>> {
                map(factory(), source(mark, pending))
            }
            fn piped(mark: (int) -> int, factory: () -> (int) -> int, pending: Task<Result<int, string>>): Task<Result<int, string>> {
                source(mark, pending) |> map(factory())
            }
            fn lambda(pending: Task<Result<int, string>>): Task<Result<int, string>> {
                pending |> map(value => value + 1)
            }
            fn grouped(pending: Task<Result<int, string>>): Task<Result<int, string>> {
                map(value => value + 1)(pending)
            }
            fn explicitArgs(pending: Task<Result<int, string>>): Task<Result<int, string>> {
                map<int, int, string>(value => value + 1, pending)
            }
            fn typedPartial(factory: () -> (int) -> int): (Task<Result<int, string>>) -> Task<Result<int, string>> {
                let f: (Task<Result<int, string>>) -> Task<Result<int, string>> = map(factory())
                f
            }
            fn typedBare(mark: (int) -> int, pending: Task<Result<int, string>>): Task<Result<int, string>> {
                let f: ((int) -> int, Task<Result<int, string>>) -> Task<Result<int, string>> = map
                f(mark, pending)
            }
            fn typedEmpty(mark: (int) -> int, pending: Task<Result<int, string>>): Task<Result<int, string>> {
                let f: ((int) -> int, Task<Result<int, string>>) -> Task<Result<int, string>> = map()
                f(mark, pending)
            }
            fn generic<T>(pending: Task<Result<T, string>>, transform: (T) -> int): Task<Result<int, string>> {
                pending |> map(transform)
            }
            fn nullToInt(value: string?): int { if value == null { 42 } else { 0 } }
            fn mapNull(pending: Task<Result<string?, string>>): Task<Result<int, string>> {
                pending |> map(nullToInt)
            }
            fn classify(value: Option<int?>): int {
                match value
                {
                    None => 1
                    Some(null) => 2
                    Some(number) => number + 1
                }
            }
            fn mapOption(pending: Task<Result<Option<int?>, string>>): Task<Result<int, string>> {
                pending |> map(classify)
            }
            fn wrap(value: int): Result<int, string> { Success(value) }
            fn nested(pending: Task<Result<int, string>>): Task<Result<Result<int, string>, string>> {
                pending |> map(wrap)
            }
            async fn code(pending: Task<Result<int, string>>): int {
                match (await pending)
                {
                    Success(value) => value
                    Error(error) => -1
                }
            }
            async fn nestedCode(pending: Task<Result<Result<int, string>, string>>): int {
                match (await pending)
                {
                    Success(Success(value)) => value
                    Success(Error(error)) => -2
                    Error(error) => -1
                }
            }
            """;
        var compiled = DSharpCompiler.Compile(source, "async-result-map.ds");
        Assert.True(compiled.CSharp is not null, string.Join("\n", compiled.Diagnostics));
        Assert.Empty(compiled.Diagnostics);
        await WithGenerated(compiled.CSharp!, "Demo", async type =>
        {
            var events = new List<int>();
            var direct = Method(type, "direct");
            var mark = Adapt(direct.GetParameters()[0].ParameterType, value => { events.Add(value); return value + 1; });
            var factory = AdaptFactory(direct.GetParameters()[1].ParameterType, () => { events.Add(2); return mark; });
            var gate = Gate(direct.GetParameters()[2].ParameterType);
            var mapped = (Task)direct.Invoke(null, new object[] { mark, factory, gate.Task })!;
            Assert.False(mapped.IsCompleted);
            Assert.Equal(new[] { 2, 1 }, events);
            gate.Complete(Method(type, "good").Invoke(null, null)!);
            Assert.Equal(42, await Decode(type, mapped));

            foreach (var method in new[] { "grouped", "explicitArgs" })
            {
                var action = Method(type, method);
                gate = Gate(action.GetParameters()[0].ParameterType);
                mapped = (Task)action.Invoke(null, new[] { gate.Task })!;
                gate.Complete(Method(type, "good").Invoke(null, null)!);
                Assert.Equal(42, await Decode(type, mapped));
            }
            var generic = Method(type, "generic").MakeGenericMethod(typeof(int));
            gate = Gate(generic.GetParameters()[0].ParameterType);
            var genericMark = Adapt(generic.GetParameters()[1].ParameterType, value => value + 1);
            mapped = (Task)generic.Invoke(null, new object[] { gate.Task, genericMark })!;
            gate.Complete(Method(type, "good").Invoke(null, null)!);
            Assert.Equal(42, await Decode(type, mapped));
            Assert.Equal(new[] { 2, 1, 41 }, events);

            events.Clear();
            gate = Gate(Method(type, "piped").GetParameters()[2].ParameterType);
            mapped = (Task)Method(type, "piped").Invoke(null, new object[] { mark, factory, gate.Task })!;
            Assert.False(mapped.IsCompleted);
            Assert.Equal(new[] { 1, 2 }, events);
            gate.Complete(Method(type, "bad").Invoke(null, null)!);
            Assert.Equal(-1, await Decode(type, mapped));
            Assert.Equal(new[] { 1, 2 }, events);

            events.Clear();
            gate = Gate(Method(type, "typedPartial").ReturnType.GetMethod("Invoke")!.GetParameters()[0].ParameterType);
            var partial = Assert.IsAssignableFrom<Delegate>(Method(type, "typedPartial").Invoke(null, new object[] { factory }));
            Assert.Equal(new[] { 2 }, events);
            mapped = (Task)partial.DynamicInvoke(gate.Task)!;
            Assert.False(mapped.IsCompleted);
            gate.Complete(Method(type, "good").Invoke(null, null)!);
            Assert.Equal(42, await Decode(type, mapped));
            Assert.Equal(new[] { 2, 41 }, events);

            events.Clear();
            gate = Gate(Method(type, "typedBare").GetParameters()[1].ParameterType);
            mapped = (Task)Method(type, "typedBare").Invoke(null, new object[] { mark, gate.Task })!;
            gate.Complete(Method(type, "good").Invoke(null, null)!);
            Assert.Equal(42, await Decode(type, mapped));
            Assert.Equal(new[] { 41 }, events);

            events.Clear();
            gate = Gate(Method(type, "typedEmpty").GetParameters()[1].ParameterType);
            mapped = (Task)Method(type, "typedEmpty").Invoke(null, new object[] { mark, gate.Task })!;
            gate.Complete(Method(type, "good").Invoke(null, null)!);
            Assert.Equal(42, await Decode(type, mapped));
            Assert.Equal(new[] { 41 }, events);

            gate = Gate(Method(type, "lambda").GetParameters()[0].ParameterType);
            mapped = (Task)Method(type, "lambda").Invoke(null, new[] { gate.Task })!;
            gate.Complete(Method(type, "good").Invoke(null, null)!);
            Assert.Equal(42, await Decode(type, mapped));

            foreach (var (method, makeValue, expected) in new[]
            {
                ("mapNull", "nullValue", 42),
                ("mapOption", "optionNone", 1),
                ("mapOption", "optionNull", 2),
                ("mapOption", "optionValue", 42)
            })
            {
                var action = Method(type, method);
                gate = Gate(action.GetParameters()[0].ParameterType);
                mapped = (Task)action.Invoke(null, new[] { gate.Task })!;
                gate.Complete(Method(type, makeValue).Invoke(null, null)!);
                Assert.Equal(expected, await Decode(type, mapped));
            }
            gate = Gate(Method(type, "nested").GetParameters()[0].ParameterType);
            mapped = (Task)Method(type, "nested").Invoke(null, new[] { gate.Task })!;
            gate.Complete(Method(type, "good").Invoke(null, null)!);
            Assert.Equal(41, await ((Task<int>)Method(type, "nestedCode").Invoke(null, new[] { mapped })!).WaitAsync(TimeSpan.FromSeconds(10)));

            events.Clear();
            gate = Gate(direct.GetParameters()[2].ParameterType);
            mapped = (Task)direct.Invoke(null, new object[] { mark, factory, gate.Task })!;
            var fault = new InvalidOperationException("source failed");
            gate.Fail(fault);
            Assert.Same(fault, await Assert.ThrowsAsync<InvalidOperationException>(async () => await mapped));
            Assert.Equal(new[] { 2, 1 }, events);
            events.Clear();
            gate = Gate(direct.GetParameters()[2].ParameterType);
            mapped = (Task)direct.Invoke(null, new object[] { mark, factory, gate.Task })!;
            gate.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await mapped);
            Assert.True(mapped.IsCanceled);
            Assert.Equal(new[] { 2, 1 }, events);

            events.Clear();
            var callbackFault = new InvalidOperationException("callback failed");
            var throwing = Adapt(direct.GetParameters()[0].ParameterType, value =>
            {
                events.Add(value);
                if (value == 41) throw callbackFault;
                return value;
            });
            var faultFactory = AdaptFactory(direct.GetParameters()[1].ParameterType, () => { events.Add(2); return throwing; });
            gate = Gate(direct.GetParameters()[2].ParameterType);
            mapped = (Task)direct.Invoke(null, new object[] { throwing, faultFactory, gate.Task })!;
            gate.Complete(Method(type, "good").Invoke(null, null)!);
            Assert.Same(callbackFault, await Assert.ThrowsAsync<InvalidOperationException>(async () => await mapped));
            Assert.Equal(new[] { 2, 1, 41 }, events);
        });

        var invalid = new[]
        {
            "fn wrong(value: string): int { 42 }\nfn bad(input: Task<Result<int, string>>): Task<Result<int, string>> { map(wrong, input) }",
            "async fn callback(value: int): int { value }\nfn bad(input: Task<Result<int, string>>): Task<Result<int, string>> { map(callback, input) }",
            "fn bad(input: Task<Result<int, string>>): int { input |> map(value => value + 1) }",
            "fn bad(input: Task<Result<int, string>>): Task<Result<int, string>> { __dsharp_result_map_task(value => value + 1, input) }"
        };
        foreach (var body in invalid)
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + body, "invalid-async-map.ds");
            Assert.True(result.CSharp is null, "Expected rejection: " + body);
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, diagnostic => Assert.Equal("invalid-async-map.ds", diagnostic.Path));
        }
        const string shadow = """
            module Shadow
            fn map(value: int): int { value + 1 }
            fn answer(): int { 41 |> map }
            """;
        var shadowed = DSharpCompiler.Compile(shadow, "shadow-async-map.ds");
        Assert.True(shadowed.CSharp is not null, string.Join("\n", shadowed.Diagnostics));
        await WithGenerated(shadowed.CSharp!, "Shadow", type =>
        {
            Assert.Equal(42, Method(type, "answer").Invoke(null, null));
            return Task.CompletedTask;
        });
        const string localShadow = """
            module Shadow
            fn answer(): int {
                let map: (int) -> int = value => value + 1
                41 |> map
            }
            """;
        shadowed = DSharpCompiler.Compile(localShadow, "shadow-local-map.ds");
        Assert.True(shadowed.CSharp is not null, string.Join("\n", shadowed.Diagnostics));
        await WithGenerated(shadowed.CSharp!, "Shadow", type =>
        {
            Assert.Equal(42, Method(type, "answer").Invoke(null, null));
            return Task.CompletedTask;
        });

        var custom = new SourceFile("custom.ds", "module Custom\nfn map(value: int): int { value + 1 }");
        var imports = new SourceFile("imports.ds", "module App\nimport Custom\nfn answer(): int { 41 |> map }");
        var imported = DSharpCompiler.Compile(new[] { custom, imports });
        Assert.True(imported.CSharp is not null, string.Join("\n", imported.Diagnostics));
        await WithGenerated(imported.CSharp!, "App", type =>
        {
            Assert.Equal(42, Method(type, "answer").Invoke(null, null));
            return Task.CompletedTask;
        });

        var provider = new SourceFile("provider.ds", """
            module Provider
            async fn source(): Result<int, string> { Success(41) }
            """);
        var app = new SourceFile("app.ds", """
            module App
            import Provider
            type Result { | Local(int) }
            type Task { local: int }
            fn __dsharp_result_map_task(): int { 42 }
            async fn answer(): int {
                let result = await (source() |> map(value => value + 1))
                match result
                {
                    Success(value) => value
                    Error(error) => 0
                }
            }
            fn collision(): int { __dsharp_result_map_task() }
            """);
        var shadowTypes = DSharpCompiler.Compile(new[] { provider, app });
        Assert.True(shadowTypes.CSharp is not null, string.Join("\n", shadowTypes.Diagnostics));
        await WithGenerated(shadowTypes.CSharp!, "App", async type =>
        {
            Assert.Equal(42, await ((Task<int>)Method(type, "answer").Invoke(null, null)!).WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(42, Method(type, "collision").Invoke(null, null));
        });

        await PreludeTaskMapDependency();
    }

    private static async Task PreludeTaskMapDependency()
    {
        // Test a changed copy of the embedded D# source, not the checked-in
        // prelude or current compiler. A later helper delegates through the
        // task-input map overload and a separately declared async function.
        const string original = """
            fn tap<A, E>(observe: (A) -> Unit, value: Result<A, E>): Result<A, E> {
                match value {
                    // Match arms cannot yet contain a bare block. This branch keeps the
                    // observation and the unchanged result in the same lexical scope.
                    Result.Success(item) => if true {
                        observe(item)
                        value
                    } else { value }
                    Result.Error(error) => value
                }
            }
            """;
        const string replacement = """
            async fn tap<A,E>(observe:(A)->Unit,value:Result<A,E>):Result<A,E>{
                await map((v:A)=>v, taskOf(value))
            }
            async fn taskOf<A,E>(value:Result<A,E>):Result<A,E>{value}
            """;
        Assert.True(Encoding.UTF8.GetByteCount(replacement) <= Encoding.UTF8.GetByteCount(original));
        var bytes = File.ReadAllBytes(typeof(DSharpCompiler).Assembly.Location);
        var needle = Encoding.UTF8.GetBytes(original);
        var changed = Encoding.UTF8.GetBytes(replacement.PadRight(original.Length));
        Assert.Equal(needle.Length, changed.Length);
        var found = -1;
        for (var index = 0; index <= bytes.Length - needle.Length; index++)
        {
            if (!bytes.AsSpan(index, needle.Length).SequenceEqual(needle)) continue;
            Assert.Equal(-1, found);
            found = index;
        }
        Assert.True(found >= 0, "Expected embedded tap body was not found.");
        changed.CopyTo(bytes.AsSpan(found, changed.Length));

        const string source = """
            module Demo
            type Task { value: int }
            fn map(value: int): int { value + 1 }
            fn ignore(value: int): Unit { () }
            async fn answer(): int {
                let observed = await tap(ignore, Success<int, string>(41))
                match observed
                {
                    Success(value) => map(value)
                    Error(error) => 0
                }
            }
            """;
        var context = new AssemblyLoadContext("mutated-task-map-" + Guid.NewGuid(), isCollectible: true);
        try
        {
            using var stream = new MemoryStream(bytes);
            var compiler = context.LoadFromStream(stream).GetType("DSharp.Compiler.DSharpCompiler", throwOnError: true)!;
            var compile = compiler.GetMethod("Compile", new[] { typeof(string), typeof(string) })!;
            var result = compile.Invoke(null, new object[] { source, "prelude-task-dependency.ds" })!;
            var resultType = result.GetType();
            var csharp = (string?)resultType.GetProperty("CSharp")!.GetValue(result);
            var diagnostics = ((System.Collections.IEnumerable)resultType.GetProperty("Diagnostics")!.GetValue(result)!).Cast<object>().ToArray();
            Assert.True(csharp is not null, string.Join("\n", diagnostics.Select(diagnostic => diagnostic.ToString())));
            await WithGenerated(csharp!, "Demo", async type =>
                Assert.Equal(42, await ((Task<int>)Method(type, "answer").Invoke(null, null)!).WaitAsync(TimeSpan.FromSeconds(10))));
        }
        finally { context.Unload(); }
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
    private static async Task<int> Decode(Type type, Task mapped) =>
        await ((Task<int>)Method(type, "code").Invoke(null, new[] { mapped })!).WaitAsync(TimeSpan.FromSeconds(10));
    private static MethodInfo Method(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
    private static Delegate Adapt(Type delegateType, Func<int, int> callback)
    {
        var parameter = Expression.Parameter(typeof(int), "value");
        return Expression.Lambda(delegateType, Expression.Invoke(Expression.Constant(callback), parameter), parameter).Compile();
    }
    private static Delegate AdaptFactory(Type delegateType, Func<Delegate> callback)
    {
        var returnType = delegateType.GetMethod("Invoke")!.ReturnType;
        return Expression.Lambda(delegateType, Expression.Convert(Expression.Invoke(Expression.Constant(callback)), returnType)).Compile();
    }
    private static async Task WithGenerated(string csharp, string module, Func<Type, Task> verify)
    {
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-async-result-map-" + Guid.NewGuid());
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
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated async Result map build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("async-result-map-" + Guid.NewGuid(), isCollectible: true);
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
