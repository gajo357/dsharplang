using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class ListFoldPartialAcceptanceTests
{
    // Invoked by the established feature aggregator to keep CI reports bounded.
    public async Task FoldPartialsCaptureAndExecute()
    {
        const string source = """
            module Demo
            type Failure { | Failed }
            interface Box { get(): int }
            service Fixed: Box { get() { 42 } }
            fn make(mark: (int) -> int): List<int> {
                let ignored = mark(1)
                [2, 3]
            }
            extend List<int> {
                fn fold(self, start: int): int { 999 }
                fn fold(self, start: string?): int { 998 }
                fn fold(self, start: int, text: string, extra: int): int { 999 }
            }
            fn partial(mark: (int) -> int): ((int, int) -> int) -> int {
                make(mark).fold(mark(2))
            }
            fn generic<T>(initial: T): T {
                let partial = [1, 2].fold<T>(initial)
                partial((acc, value) => acc)
            }
            fn genericNullable<T>(initial: T?): T? {
                let partial = [1, 2].fold<T?>(initial)
                partial((acc, value) => acc)
            }
            fn inferred() { [2, 3].fold(1) }
            fn inferredParam(values: List<int>) { values.fold(1) }
            fn fullService(value: Box): int { [1].fold(value, (acc, item) => acc).get() }
            fn contextualPipe(): string? { ((acc, item) => acc) |> [1].fold(null) }
            fn answer(): bool {
                let numbers = [1, 2, 3]
                let fold = numbers.fold(10)
                let plus = (acc: int, value: int) => acc + value
                let first = fold(plus)
                let second = plus |> fold
                let grouped = (numbers.fold(36))((acc, value) => acc + value)
                let empty: List<int> = []
                let emptyFold = empty.fold(42)
                let explicitNull = numbers.fold<int?>(null)
                let contextual: ((int?, int) -> int?) -> int? = numbers.fold(null)
                let stringNull: ((string?, int) -> string?) -> string? = numbers.fold(null)
                let explicitStringNull = numbers.fold<string?>(null)
                let liftLiteral: ((int?, int) -> int?) -> int? = numbers.fold(1)
                let ordinary = 1
                let liftVariable: ((int?, int) -> int?) -> int? = numbers.fold(ordinary)
                let noneFold = numbers.fold<Option<int>>(None)
                first == 16 && second == 16 && grouped == 42 &&
                emptyFold((acc, value) => acc + value) == 42 &&
                explicitNull((acc, value) => acc) == null &&
                contextual((acc, value) => acc) == null &&
                liftLiteral((acc, value) => acc) == 1 && liftVariable((acc, value) => acc) == 1 &&
                stringNull((acc, value) => acc) == null && explicitStringNull((acc, value) => acc) == null && contextualPipe() == null &&
                fullService(Fixed()) == 42 && safeFunction() == 42 &&
                noneFold((acc, value) => acc) == None &&
                generic<int>(42) == 42 && generic<string?>(null) == null &&
                genericNullable<int>(42) == 42 && genericNullable<int>(null) == null &&
                genericNullable<int?>(42) == 42 && genericNullable<int?>(null) == null &&
                genericNullable<string>("hello") == "hello" && genericNullable<string>(null) == null &&
                numbers.fold(0, (acc, value) => acc + value) == 6 &&
                numbers.length() == 3 && (inferred())((acc, value) => acc + value) == 6 &&
                (inferredParam([2,3]))((acc, value) => acc + value) == 6
            }
            fn makeEmpty(): ((int, int) -> int) -> int {
                let values: List<int> = []
                values.fold(42)
            }
            fn safeFunction(): int {
                let initial = (value: int) => value + 1
                let fold = [1].fold(initial)
                let next = fold((acc, item) => acc)
                next(41)
            }
            fn receiverExit(): int {
                let fold = (if true { return 42 } else { [1] }).fold(0)
                fold((acc, value) => acc + value)
            }
            fn initialExit(): int {
                let fold = [1].fold(if true { return 42 } else { 0 })
                fold((acc, value) => acc + value)
            }
            fn propagated(initial: Result<int, Failure>): Result<int, Failure> {
                let fold = [1].fold(initial?)
                Success(fold((acc, value) => acc + value))
            }
            fn propagatedError(): bool {
                match propagated(Error(Failed)) { Error(Failed) => true
                                                   Success(value) => false }
            }
            async fn makeAsync(mark: (int) -> int, gate: Task<int>): List<int> {
                let ignored = await gate
                make(mark)
            }
            async fn asyncReceiver(mark: (int) -> int, gate: Task<int>): int {
                let fold = (await makeAsync(mark, gate)).fold(mark(2))
                fold((acc, value) => acc + value)
            }
            async fn asyncInitial(mark: (int) -> int, gate: Task<int>): int {
                let fold = make(mark).fold(await gate)
                fold((acc, value) => acc + value)
            }
            """;
        var compilation = DSharpCompiler.Compile(source, "list-fold-partial.ds");
        Assert.True(compilation.CSharp is not null, string.Join("\n", compilation.Diagnostics));
        Assert.Empty(compilation.Diagnostics);
        await WithGenerated(compilation.CSharp!, "Demo", async type =>
        {
            Assert.True(Call<bool>(type, "answer"));
            Assert.Equal(42, Call<int>(type, "receiverExit"));
            Assert.Equal(42, Call<int>(type, "initialExit"));
            Assert.True(Call<bool>(type, "propagatedError"));

            var events = new List<int>();
            var factory = Method(type, "partial");
            var mark = AdaptUnary(factory.GetParameters()[0].ParameterType, n => { events.Add(n); return n; });
            var partial = Assert.IsAssignableFrom<Delegate>(factory.Invoke(null, new object[] { mark }));
            Assert.Equal(new[] { 1, 2 }, events);
            var callbackType = partial.GetType().GetMethod("Invoke")!.GetParameters()[0].ParameterType;
            var calls = new List<int>();
            var callback = AdaptBinary(callbackType, (acc, value) => { calls.Add(value); return acc * 10 + value; });
            Assert.Empty(calls);
            Assert.Equal(223, partial.DynamicInvoke(callback));
            Assert.Equal(new[] { 2, 3 }, calls);
            Assert.Equal(223, partial.DynamicInvoke(callback));
            Assert.Equal(new[] { 2, 3, 2, 3 }, calls);
            Assert.Equal(new[] { 1, 2 }, events);
            var faults = new List<int>();
            var failReceiver = AdaptUnary(factory.GetParameters()[0].ParameterType, n => { faults.Add(n); throw new InvalidOperationException("receiver"); });
            var receiverFailure = Assert.Throws<TargetInvocationException>(() => factory.Invoke(null, new object[] { failReceiver }));
            Assert.IsType<InvalidOperationException>(receiverFailure.InnerException);
            Assert.Equal(new[] { 1 }, faults);
            faults.Clear();
            var failInitial = AdaptUnary(factory.GetParameters()[0].ParameterType, n => { faults.Add(n); if (n == 2) throw new InvalidOperationException("initial"); return n; });
            var initialFailure = Assert.Throws<TargetInvocationException>(() => factory.Invoke(null, new object[] { failInitial }));
            Assert.IsType<InvalidOperationException>(initialFailure.InnerException);
            Assert.Equal(new[] { 1, 2 }, faults);
            var empty = Call<Delegate>(type, "makeEmpty");
            var emptyCallbackType = empty.GetType().GetMethod("Invoke")!.GetParameters()[0].ParameterType;
            var emptyCalls = 0;
            var emptyCallback = AdaptBinary(emptyCallbackType, (acc, value) => { emptyCalls++; return acc + value; });
            Assert.Equal(42, empty.DynamicInvoke(emptyCallback));
            Assert.Equal(0, emptyCalls);
            var failCallback = AdaptBinary(callbackType, (acc, value) => throw new InvalidOperationException("callback"));
            var callbackFailure = Assert.Throws<TargetInvocationException>(() => partial.DynamicInvoke(failCallback));
            Assert.IsType<InvalidOperationException>(callbackFailure.InnerException);

            var asyncMethod = Method(type, "asyncInitial");
            var asyncEvents = new List<int>();
            var asyncMark = AdaptUnary(asyncMethod.GetParameters()[0].ParameterType, n => { asyncEvents.Add(n); return n; });
            var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = Call<Task<int>>(type, "asyncInitial", asyncMark, gate.Task);
            Assert.Equal(new[] { 1 }, asyncEvents);
            Assert.False(pending.IsCompleted);
            gate.SetResult(2);
            Assert.Equal(7, await pending.WaitAsync(TimeSpan.FromSeconds(10)));
            var receiverEvents = new List<int>();
            var receiverMark = AdaptUnary(Method(type, "asyncReceiver").GetParameters()[0].ParameterType, n => { receiverEvents.Add(n); return n; });
            var receiverGate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var receiverPending = Call<Task<int>>(type, "asyncReceiver", receiverMark, receiverGate.Task);
            Assert.Empty(receiverEvents);
            Assert.False(receiverPending.IsCompleted);
            receiverGate.SetResult(0);
            Assert.Equal(7, await receiverPending.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(new[] { 1, 2 }, receiverEvents);
            var failedGate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var failing = Call<Task<int>>(type, "asyncInitial", asyncMark, failedGate.Task);
            var expected = new InvalidOperationException("initial failure");
            failedGate.SetException(expected);
            Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(async () => await failing));
        });
        RejectInvalidPrograms();
        await Imports();
    }

    private static void RejectInvalidPrograms()
    {
        string[] invalid =
        [
            "fn bad(): Unit { let f = [1].fold(null)\n () }",
            "fn bad(): Unit { let f = [1].fold<int>(null)\n () }",
            "fn bad(): Unit { let f = [1].fold(0)\n let n = f((acc, value) => true)\n () }",
            "fn bad(): Unit { let f = [1].fold(0, (acc, value) => acc, 3)\n () }",
            "fn bad(): Unit { [1].fold(0)\n () }",
            "fn bad(): Unit { let b = MutableList<int>()\n let f = [1].fold(b)\n () }",
            "resource Cursor { release(): Unit { () } }\nfn bad(): Unit { using c = Cursor() { let f = [1].fold(c)\n () } }",
            "interface A { get(): int }\nfn bad(a: A): Unit { let f = [1].fold(a)\n () }",
            "interface A { get(): int }\nfn bad(a: Task<A>): Unit { let f = [1].fold(a)\n () }",
            "interface A { get(): int }\nfn wrap<T>(value: T): ((T,int)->T)->T { [1].fold<T>(value) }\nfn bad(a: A): Unit { let f = wrap<A>(a)\n () }",
            "interface A { get(): int }\nfn make<T>(value: T): ((T,int)->T)->T { [1].fold<T>(value) }\nfn pass<T>(value:T): ((T,int)->T)->T { make<T>(value) }\nfn bad(a: A): Unit { let f = pass<A>(a)\n () }",
            "interface A { get(): int }\nfn make<T>(value: T): ((T,int)->T)->T { [1].fold<T>(value) }\nfn bad(a: A): Unit { let factory: (A) -> (((A,int)->A)->A) = make<A>\n let f = factory(a)\n () }",
            "fn bad(): Unit { let f: ((string?, int)->string?) -> string? = [1].fold(0)\n () }",
            "interface A { fold(value: int): int }\nfn inferred(a: A) { a.fold(1) }",
            "interface A { fold(value: int, other: int): int }\nfn inferred(a: A) { 2 |> a.fold(1) }",
            "fn cyclic() { [1].fold(seed()) }\nfn seed() { cyclic() }",
            "type List<T> { value: T }\nfn inferred(values: List<int>) { values.fold(1) }",
            "fn bad(values: List<int>?): Unit { let f = values.fold(1)\n () }"
        ];
        foreach (var declaration in invalid)
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + declaration, "invalid-list-fold-partial.ds");
            Assert.True(result.CSharp is null, "Invalid List.fold partial compiled: " + declaration);
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, diagnostic => Assert.StartsWith("DS2", diagnostic.Code));
        }
    }

    private static async Task Imports()
    {
        var sources = new[]
        {
            new SourceFile("lib.ds", "module Lib\nfn initial(): int { 39 }\nfn step(acc: int, item: int): int { acc + item }"),
            new SourceFile("app.ds", "module Demo\nimport Lib\nfn answer(): int { let folder = [1,2].fold(initial())\n step |> folder }")
        };
        var result = DSharpCompiler.Compile(sources);
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        await WithGenerated(result.CSharp!, "Demo", type => { Assert.Equal(42, Call<int>(type, "answer")); return Task.CompletedTask; });
    }

    private static MethodInfo Method(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
    private static T Call<T>(Type type, string name, params object?[] args) => (T)Method(type, name).Invoke(null, args)!;
    private static Delegate AdaptUnary(Type type, Func<int,int> action)
    {
        var value = Expression.Parameter(typeof(int), "value");
        return Expression.Lambda(type, Expression.Invoke(Expression.Constant(action), value), value).Compile();
    }
    private static Delegate AdaptBinary(Type type, Func<int,int,int> action)
    {
        var acc = Expression.Parameter(typeof(int), "acc");
        var value = Expression.Parameter(typeof(int), "value");
        return Expression.Lambda(type, Expression.Invoke(Expression.Constant(action), acc, value), acc, value).Compile();
    }
    private static async Task WithGenerated(string csharp, string module, Func<Type,Task> verify)
    {
        var assemblyName = "DSharpFoldPartial_" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-fold-partial-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "Generated.cs"), csharp);
            File.WriteAllText(Path.Combine(directory, "Generated.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net11.0</TargetFramework><AssemblyName>" + assemblyName + "</AssemblyName><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>");
            File.WriteAllText(Path.Combine(directory, "global.json"), "{\"sdk\":{\"version\":\"11.0.100\",\"rollForward\":\"disable\"}}");
            File.WriteAllText(Path.Combine(directory, "NuGet.Config"), "<configuration><packageSources><clear/></packageSources></configuration>");
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "build", "Generated.csproj", "--nologo", "--verbosity", "quiet" }) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated fold partial build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("fold-partial-" + Guid.NewGuid(), isCollectible: false);
            var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0", assemblyName + ".dll"));
            var type = Assert.Single(assembly.GetTypes(), candidate => candidate.Name == module);
            await verify(type);
        }
        finally { Directory.Delete(directory, true); }
    }
}
