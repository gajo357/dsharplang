using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class GenericListEmptyPartialAcceptanceTests
{
    // Run through the feature aggregator to keep the CI report bounded.
    public async Task GenericListEmptyPartialsExecuteAndRejectInvalidPrograms()
    {
        const string source = """
            module Demo
            type Failure { | Failed }
            type Item { value: int }
            interface Box { get(): int }
            service Fixed: Box { get() { 42 } }
            fn values(mark: (int) -> int): List<int> {
                let ignored = mark(1)
                [1, 2, 3]
            }
            fn makeMap(mark: (int) -> int): ((int) -> int) -> List<int> {
                values(mark).map<int>()
            }
            fn makeFold(mark: (int) -> int): (int, (int,int) -> int) -> int {
                values(mark).fold<int>()
            }
            fn length(xs: List<int>): int { xs.length() }
            fn first(xs: List<int>): int {
                match xs.get(0) { None => -1
                                  Some(value) => value }
            }
            fn add(acc: int, value: int): int { acc + value }
            fn plusOne(value: int): int { value + 1 }
            fn nullableMap<T>(xs: List<T?>): ((T?) -> T?) -> List<T?> { xs.map<T?>() }
            fn nullableFold<T>(xs: List<int>): (T?, (T?,int) -> T?) -> T? { xs.fold<T?>() }
            fn answer(): bool {
                let numbers = [1, 2, 3]
                let mapped = numbers.map<int>()
                let mappedContext: ((int)->int)->List<int> = numbers.map()
                let folded = numbers.fold<int>()
                let foldedContext: (int,(int,int)->int)->int = numbers.fold()
                let byValue = folded(10)
                let byValuePipe: ((int,int)->int)->int = 10 |> numbers.fold()
                let pipeMap: List<int> = plusOne |> numbers.map()
                let typedPipe: List<int> = ((value: int) => value + 1) |> numbers.map()
                let untypedPipe: List<int> = (value => value + 1) |> numbers.map()
                let empty: List<int> = []
                let emptyMap = empty.map<int>()
                let emptyFold = empty.fold<int>()
                let emptyMapNoCall = emptyMap((value: int) => 1 / 0)
                let emptyFoldNoCall = emptyFold(42, (acc: int, value: int) => 1 / 0)
                let nullable = nullableMap<int>([null, 42])((v: int?) => v)
                let nullableAgain = nullableMap<int?>([null, 42])((v: int?) => v)
                let nullableString = nullableMap<string>([null, "hi"])((v: string?) => v)
                let optional: List<Option<int?>> = [None, Some(null), Some(42)]
                let optionMap = optional.map<Option<int?>>()
                let optionResult = optionMap((v: Option<int?>) => v)
                let fNullable = nullableFold<int>(numbers)
                let fNullableAgain = nullableFold<int?>(numbers)
                let pipeNull: ((int?,int)->int?)->int? = null |> numbers.fold()
                let inferredMap = numbers.map<int>()
                let groupedMap: List<int> = (numbers.map())((x: int) => x + 1)
                let groupedFold: int = (numbers.fold())(10, (a: int, b: int) => a + b)
                let fullMap = numbers.map(value => value + 1)
                let fullFold = numbers.fold(10, add)
                mapped(plusOne) == [2, 3, 4] && mappedContext(plusOne) == [2, 3, 4] &&
                folded(10, add) == 16 && foldedContext(10, add) == 16 &&
                byValue(add) == 16 && byValuePipe(add) == 16 && pipeMap == [2, 3, 4] && typedPipe == [2, 3, 4] && untypedPipe == [2, 3, 4] &&
                emptyMap(plusOne) == [] && emptyMapNoCall == [] && emptyFold(42, add) == 42 && emptyFoldNoCall == 42 && numbers == [1, 2, 3] &&
                nullable.get(0) == Some(null) && nullable.get(1) == Some(42) &&
                nullableAgain.get(0) == Some(null) && nullableAgain.get(1) == Some(42) &&
                nullableString.get(0) == Some(null) && nullableString.get(1) == Some("hi") &&
                optionResult.get(0) == Some(None) && optionResult.get(1) == Some(Some(null)) && optionResult.get(2) == Some(Some(42)) &&
                fNullable(null, (acc, value) => acc) == null && fNullableAgain(42, (acc, value) => acc) == 42 &&
                pipeNull((acc, value) => acc) == null && fullService(Fixed()) == 42 && groupedMap == fullMap && groupedFold == 16 && inferredMap(plusOne) == fullMap && fullFold == 16
            }
            fn fullService(value: Box): int { [1].fold(value, (acc, item) => acc).get() }
            fn groupedEffects(mark: (int) -> int): int {
                (values(mark).fold())(mark(2), (acc: int, item: int) => acc + item)
            }
            fn groupedEarly(): int {
                ([1].fold())(if true { return 42 } else { 0 }, (a: int, b: int) => a + b)
            }
            fn earlyMap(): int {
                let partial = (if true { return 42 } else { [1] }).map<int>()
                partial(value => value).length()
            }
            fn earlyFold(): int {
                let partial = (if true { return 42 } else { [1] }).fold<int>()
                partial(0, add)
            }
            fn propagated(input: Result<List<int>,Failure>): Result<int,Failure> {
                let partial = (input?).fold<int>()
                Success(partial(10, add))
            }
            fn propagatedError(): bool {
                match propagated(Error(Failed)) { Error(Failed) => true
                                                  Success(value) => false }
            }
            async fn load(gate: Task<int>): List<int> { [await gate, 2] }
            async fn asyncMap(gate: Task<int>): int {
                let partial = (await load(gate)).map<int>()
                partial(plusOne).length()
            }
            async fn asyncFold(gate: Task<int>): int {
                let partial = (await load(gate)).fold<int>()
                partial(10, add)
            }
            """;
        var compilation = DSharpCompiler.Compile(source, "generic-list-empty-partials.ds");
        Assert.True(compilation.CSharp is not null, string.Join("\n", compilation.Diagnostics));
        Assert.Empty(compilation.Diagnostics);
        await WithGenerated(compilation.CSharp!, "Demo", async type =>
        {
            Assert.True(Call<bool>(type, "answer"));
            Assert.Equal(42, Call<int>(type, "groupedEarly"));
            Assert.Equal(42, Call<int>(type, "earlyMap"));
            Assert.Equal(42, Call<int>(type, "earlyFold"));
            Assert.True(Call<bool>(type, "propagatedError"));

            var ordered = new List<int>();
            var effects = Method(type, "groupedEffects");
            var orderedMark = AdaptUnary(effects.GetParameters()[0].ParameterType, n => { ordered.Add(n); return n; });
            Assert.Equal(8, effects.Invoke(null, new object[] { orderedMark }));
            Assert.Equal(new[] { 1, 2 }, ordered);
            var events = new List<int>();
            var factory = Method(type, "makeMap");
            var mark = AdaptUnary(factory.GetParameters()[0].ParameterType, n => { events.Add(n); return n; });
            var map = Assert.IsAssignableFrom<Delegate>(factory.Invoke(null, new object[] { mark }));
            Assert.Equal(new[] { 1 }, events);
            var callbackType = map.GetType().GetMethod("Invoke")!.GetParameters()[0].ParameterType;
            var calls = new List<int>();
            var transform = AdaptUnary(callbackType, n => { calls.Add(n); return n + 1; });
            Assert.Empty(calls);
            var first = map.DynamicInvoke(transform)!;
            var second = map.DynamicInvoke(transform)!;
            Assert.Equal(new[] { 1, 2, 3, 1, 2, 3 }, calls);
            Assert.Equal(3, Call<int>(type, "length", first));
            Assert.Equal(2, Call<int>(type, "first", second));
            Assert.Equal(new[] { 1 }, events);
            var throwing = AdaptUnary(callbackType, _ => throw new InvalidOperationException("callback"));
            var failure = Assert.Throws<TargetInvocationException>(() => map.DynamicInvoke(throwing));
            Assert.IsType<InvalidOperationException>(failure.InnerException);

            var foldFactory = Method(type, "makeFold");
            var foldMark = AdaptUnary(foldFactory.GetParameters()[0].ParameterType, n => { events.Add(n); return n; });
            var fold = Assert.IsAssignableFrom<Delegate>(foldFactory.Invoke(null, new object[] { foldMark }));
            Assert.Equal(new[] { 1, 1 }, events);
            var foldCallbackType = fold.GetType().GetMethod("Invoke")!.GetParameters()[1].ParameterType;
            var folds = new List<int>();
            var step = AdaptBinary(foldCallbackType, (acc, value) => { folds.Add(value); return acc * 10 + value; });
            Assert.Empty(folds);
            Assert.Equal(10123, fold.DynamicInvoke(10, step));
            Assert.Equal(10123, fold.DynamicInvoke(10, step));
            Assert.Equal(new[] { 1, 2, 3, 1, 2, 3 }, folds);

            foreach (var name in new[] { "asyncMap", "asyncFold" })
            {
                var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                var pending = Call<Task<int>>(type, name, gate.Task);
                Assert.False(pending.IsCompleted);
                gate.SetResult(1);
                Assert.Equal(name == "asyncMap" ? 2 : 13, await pending.WaitAsync(TimeSpan.FromSeconds(10)));
            }
            var failed = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var failing = Call<Task<int>>(type, "asyncMap", failed.Task);
            var expected = new InvalidOperationException("receiver");
            failed.SetException(expected);
            Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(async () => await failing));
        });
        RejectInvalidPrograms();
        await ExtensionPrecedence();
        await Imports();
    }

    private static void RejectInvalidPrograms()
    {
        string[] invalid =
        [
            "fn bad(): Unit { let f = [1].map()\n () }",
            "fn bad(): Unit { let f = [1].fold()\n () }",
            "fn bad(): Unit { let f = [1].map<int>()\n let value = f((x: int) => \"wrong\")\n () }",
            "fn bad(): Unit { let f = [1].fold<int>()\n let value = f(0, (a: int, b: int) => true)\n () }",
            "fn bad(): Unit { let f = [1].map<int,string>()\n () }",
            "fn bad(): Unit { let f = [1].fold<int,string>()\n () }",
            "fn bad(): Unit { let f = [1].map<int>()\n let value = f((x: int) => x, 1)\n () }",
            "fn bad(): Unit { [1].map<int>()\n () }",
            "fn bad(): Unit { [1].fold<int>()\n () }",
            "fn bad(): Unit { let f = [1].fold<int>()(null)\n () }",
            "fn bad(): Unit { let f = [1].map<Task<int>>()\n () }",
            "fn bad(): Unit { let f = [1].map<(int)->int>()\n () }",
            "fn bad(): Unit { let b = MutableList<int>()\n let f = b.map<int>()\n () }",
            "fn bad(): Unit { let b = MutableList<int>()\n let f = [1].fold<MutableList<int>>()(b)\n () }",
            "resource Cursor { release(): Unit { () } }\nfn bad(): Unit { using c = Cursor() { let f = [1].fold<Cursor>()(c)\n () } }",
            "interface A { get(): int }\nfn bad(a: A): Unit { let f = [1].map<A>()\n () }",
            "interface A { get(): int }\nfn bad(a: A): Unit { let f = [1].fold<A>()(a)\n () }",
            "interface A { get(): int }\nfn make<U>(xs: List<int>): ((int)->U)->List<U> { xs.map<U>() }\nfn bad(): Unit { let f = make<A>([1])\n () }",
            "interface A { get(): int }\nfn make<U>(xs: List<int>): ((int)->U)->List<U> { xs.map<U>() }\nfn pass<U>(xs: List<int>): ((int)->U)->List<U> { make<U>(xs) }\nfn bad(): Unit { let f = pass<A>([1])\n () }",
            "interface A { get(): int }\nfn make<U>(xs: List<int>): ((int)->U)->List<U> { xs.map<U>() }\nfn bad(): Unit { let factory: (List<int>)->(((int)->A)->List<A>) = make<A>()\n () }",
            "interface A { get(): int }\nfn make<T>(): (T,(T,int)->T)->T { [1].fold<T>() }\nfn wrap<T>(value:T): ((T,int)->T)->T { make<T>()(value) }\nfn bad(a:A): Unit { let f = wrap<A>(a)\n () }",
            "interface A { get(): int }\nfn make<T>(): (T,(T,int)->T)->T { [1].fold<T>() }\nfn bad(a:A): Unit { let factory: (A,(A,int)->A)->A = make<A>()\n let f = factory(a)\n () }",
            "fn bad(callback: (int)->int) { callback |> [1].map<int>() }",
            "fn bad(callback: (int,int)->int) { let f = [1].fold<int>()(0)\n callback |> f }"
        ];
        foreach (var declaration in invalid)
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + declaration, "invalid-generic-list-empty.ds");
            Assert.True(result.CSharp is null && result.Diagnostics.Count > 0, "Invalid source compiled: " + declaration);
            Assert.True(result.Diagnostics.All(diagnostic => diagnostic.Code.StartsWith("DS2", StringComparison.Ordinal)), declaration + ": " + string.Join("; ", result.Diagnostics));
        }
    }

    private static async Task ExtensionPrecedence()
    {
        const string source = """
            module Demo
            extend List<int> {
                fn map(self): int { 77 }
                fn fold(self): int { 88 }
            }
            fn plusOne(value: int): int { value + 1 }
            fn add(a: int, b: int): int { a + b }
            fn answer(): bool {
                let values = [40]
                let fullMap = values.map()
                let fullFold = values.fold()
                let partialMap: ((int)->int)->List<int> = values.map()
                let partialFold: (int,(int,int)->int)->int = values.fold()
                let explicitMap = values.map<int>()
                let explicitFold = values.fold<int>()
                let pipedMap: List<int> = plusOne |> values.map()
                fullMap == 77 && fullFold == 88 && partialMap(plusOne) == [41] &&
                partialFold(2, add) == 42 && explicitMap(plusOne) == [41] &&
                explicitFold(2, add) == 42 && pipedMap == [41]
            }
            """;
        var result = DSharpCompiler.Compile(source, "generic-list-precedence.ds");
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        await WithGenerated(result.CSharp!, "Demo", type => { Assert.True(Call<bool>(type, "answer")); return Task.CompletedTask; });

        const string sameCallable = """
            module Demo
            extend List<int> { fn map(self): ((int)->int)->List<int> { (f: (int)->int) => [999] } }
            fn answer(): bool { let m: ((int)->int)->List<int> = [1].map()
                m((x:int) => x) == [999] }
            """;
        var same = DSharpCompiler.Compile(sameCallable, "generic-list-matching-extension.ds");
        Assert.True(same.CSharp is not null, string.Join("\n", same.Diagnostics));
        Assert.Empty(same.Diagnostics);
        await WithGenerated(same.CSharp!, "Demo", type => { Assert.True(Call<bool>(type, "answer")); return Task.CompletedTask; });

        var a = new SourceFile("a.ds", "module A\nextend List<int> { fn map(self): ((int)->int)->List<int> { (f: (int)->int) => [1] } }");
        var b = new SourceFile("b.ds", "module B\nextend List<int> { fn map(self): ((int)->int)->List<int> { (f: (int)->int) => [2] } }");
        var app = new SourceFile("app.ds", "module Demo\nimport A\nimport B\nfn answer(): bool { let f: ((int)->int)->List<int> = [1].map()\n f((x:int)=>x) == [1] }");
        var ambiguous = DSharpCompiler.Compile(new[] { a, b, app });
        Assert.Null(ambiguous.CSharp);
        Assert.Contains(ambiguous.Diagnostics, diagnostic => diagnostic.Code == "DS2018" && diagnostic.Message.Contains("Ambiguous", StringComparison.Ordinal));
    }

    private static async Task Imports()
    {
        var sources = new[]
        {
            new SourceFile("lib.ds", "module Lib\nfn values(): List<int> { [40] }\nfn plus(value: int): int { value + 2 }"),
            new SourceFile("app.ds", "module Demo\nimport Lib\nfn answer(): bool { let m: ((int)->int)->List<int> = values().map()\n let f: (int,(int,int)->int)->int = values().fold()\n m(plus) == [42] && f(2, (a, b) => a + b) == 42 }")
        };
        var result = DSharpCompiler.Compile(sources);
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        await WithGenerated(result.CSharp!, "Demo", type => { Assert.True(Call<bool>(type, "answer")); return Task.CompletedTask; });
    }

    private static MethodInfo Method(Type type, string name) => type.GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
    private static T Call<T>(Type type, string name, params object?[] args) => (T)Method(type, name).Invoke(null, args)!;
    private static Delegate AdaptUnary(Type type, Func<int,int> callback)
    {
        var value = Expression.Parameter(typeof(int), "value");
        return Expression.Lambda(type, Expression.Invoke(Expression.Constant(callback), value), value).Compile();
    }
    private static Delegate AdaptBinary(Type type, Func<int,int,int> callback)
    {
        var first = Expression.Parameter(typeof(int), "first");
        var second = Expression.Parameter(typeof(int), "second");
        return Expression.Lambda(type, Expression.Invoke(Expression.Constant(callback), first, second), first, second).Compile();
    }
    private static async Task WithGenerated(string csharp, string module, Func<Type,Task> verify)
    {
        var assemblyName = "DSharpGenericListEmpty_" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-generic-list-empty-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "Generated.cs"), csharp);
            File.WriteAllText(Path.Combine(directory, "Generated.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net11.0</TargetFramework><AssemblyName>" + assemblyName + "</AssemblyName><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>");
            File.WriteAllText(Path.Combine(directory, "global.json"), "{\"sdk\":{\"version\":\"11.0.100\",\"rollForward\":\"disable\"}}");
            File.WriteAllText(Path.Combine(directory, "NuGet.Config"), "<configuration><packageSources><clear/></packageSources></configuration>");
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "build", "Generated.csproj", "--nologo", "--verbosity", "quiet" }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated generic list partial build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("generic-list-empty-" + Guid.NewGuid(), isCollectible: false);
            var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0", assemblyName + ".dll"));
            var type = Assert.Single(assembly.GetTypes(), candidate => candidate.Name == module);
            await verify(type);
        }
        finally { Directory.Delete(directory, true); }
    }
}
