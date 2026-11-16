using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class EmptyCollectionPartialAcceptanceTests
{
    // Runs through the established aggregate Fact so the bounded CI report remains stable.
    public async Task EmptyCollectionPartialsExecuteAndRejectInvalidPrograms()
    {
        const string source = """
            module Demo
            type Failure { | Failed }
            fn make(mark: (int) -> int): List<int> {
                let ignored = mark(1)
                [1, 2, 3]
            }
            fn makeAppend(mark: (int) -> int): (int) -> List<int> { make(mark).append() }
            fn makeFilter(mark: (int) -> int): ((int) -> bool) -> List<int> { make(mark).filter() }
            fn lengthOf(value: List<int>): int { value.length() }
            fn firstOf(value: List<int>): int {
                match value.get(0) { None => -1
                                     Some(number) => number }
            }
            fn generic<T>(value: T): bool {
                let empty: List<T> = []
                let append = empty.append()
                let get = append(value).get()
                get(0) == Some(value) && get(1) == None
            }
            fn inferredGet(values: List<int>) { values.get() }
            fn getNullable<T>(values: List<T?>): (int) -> Option<T?> { values.get() }
            fn setNullable<T>(values: Map<int,T?>): (int,T?) -> Map<int,T?> { values.set() }
            fn answer(): bool {
                let numbers = [1, 2, 3]
                let append = numbers.append()
                let get = numbers.get()
                let filter = numbers.filter()
                let changed = append(4)
                let filtered = filter(value => value > 1)
                let values = Map.empty<int,string?>()
                let put = values.set()
                let byKey = put(1)
                let one = byKey(null)
                let two = 2 |> put
                let other = "Alice" |> two
                let mapGet = one.get()
                let contains = one.contains()
                let set = Set.from([1, 2])
                let add = set.add()
                let has = set.contains()
                let altered = add(3)
                let grouped = (Map.empty<int,int>().set())(5, 42)
                let nested: Map<Option<string?>,Option<int?>> = Map.empty<Option<string?>,Option<int?>>()
                let nestedPut = nested.set()
                let nestedResult = nestedPut(Some(null), Some(null))
                let nullableGet = getNullable<int>([null, 42])
                let nullableAgain = getNullable<int?>([null, 42])
                let nullableStrings = getNullable<string>([null, "hi"])
                let nullablePut = setNullable<int>(Map.empty<int,int?>())
                let againPut = setNullable<int?>(Map.empty<int,int?>())
                let nullableMap = nullablePut(1, null)
                let nullableMapAgain = againPut(1, 42)
                let byNull = null |> Map.empty<string?,int>().set()
                let nullKey = byNull(42)
                numbers.length() == 3 && changed.length() == 4 && numbers.get(3) == None &&
                get(0) == Some(1) && get(8) == None && filtered == [2, 3] &&
                values.count() == 0 && one.count() == 1 && one.get(1) == Some(null) &&
                other.get(2) == Some("Alice") && mapGet(1) == Some(null) && mapGet(2) == None &&
                contains(1) && !contains(2) && set.count() == 2 && has(1) && !has(3) &&
                altered.count() == 3 && altered.contains(3) && grouped.get(5) == Some(42) &&
                nested.get(Some(null)) == None && nestedResult.get(Some(null)) == Some(Some(null)) &&
                generic<int>(42) && generic<string?>(null) &&
                nullableGet(0) == Some(null) && nullableGet(1) == Some(42) && nullableGet(9) == None &&
                nullableAgain(0) == Some(null) && nullableAgain(1) == Some(42) &&
                nullableStrings(0) == Some(null) && nullableStrings(1) == Some("hi") &&
                nullableMap.get(1) == Some(null) && nullableMap.get(2) == None && nullableMapAgain.get(1) == Some(42) &&
                nullKey.get(null) == Some(42) && (inferredGet([42]))(0) == Some(42)
            }
            fn receiverExit(): int {
                let f = (if true { return 42 } else { [1] }).append()
                f(2).length()
            }
            async fn values(gate: Task<int>): List<int> { [await gate] }
            async fn asyncReceiver(gate: Task<int>): int {
                let append = (await values(gate)).append()
                append(42).length()
            }
            fn propagated(value: Result<List<int>,Failure>): Result<int,Failure> {
                let append = (value?).append()
                Success(append(1).length())
            }
            fn propagatedError(): bool {
                match propagated(Error(Failed)) { Error(Failed) => true
                                                 Success(value) => false }
            }
            """;
        var compilation = DSharpCompiler.Compile(source, "empty-collection-partials.ds");
        Assert.True(compilation.CSharp is not null, string.Join("\n", compilation.Diagnostics));
        Assert.Empty(compilation.Diagnostics);
        await WithGenerated(compilation.CSharp!, "Demo", async type =>
        {
            Assert.True(Call<bool>(type, "answer"));
            Assert.Equal(42, Call<int>(type, "receiverExit"));
            Assert.True(Call<bool>(type, "propagatedError"));

            var events = new List<int>();
            var factory = Method(type, "makeAppend");
            var mark = AdaptInt(factory.GetParameters()[0].ParameterType, value => { events.Add(value); return value; });
            var append = Assert.IsAssignableFrom<Delegate>(factory.Invoke(null, new object[] { mark }));
            Assert.Equal(new[] { 1 }, events);
            var first = append.DynamicInvoke(4)!;
            var second = append.DynamicInvoke(5)!;
            Assert.Equal(4, Call<int>(type, "lengthOf", first));
            Assert.Equal(4, Call<int>(type, "lengthOf", second));
            Assert.Equal(1, Call<int>(type, "firstOf", first));
            Assert.Equal(new[] { 1 }, events);

            var failingEvents = new List<int>();
            var fail = AdaptInt(factory.GetParameters()[0].ParameterType, value => { failingEvents.Add(value); throw new InvalidOperationException("receiver failure"); });
            var receiverFailure = Assert.Throws<TargetInvocationException>(() => factory.Invoke(null, new object[] { fail }));
            Assert.IsType<InvalidOperationException>(receiverFailure.InnerException);
            Assert.Equal(new[] { 1 }, failingEvents);

            var filterFactory = Method(type, "makeFilter");
            var filterMark = AdaptInt(filterFactory.GetParameters()[0].ParameterType, value => { events.Add(value); return value; });
            var filter = Assert.IsAssignableFrom<Delegate>(filterFactory.Invoke(null, new object[] { filterMark }));
            Assert.Equal(new[] { 1, 1 }, events);
            var callbackType = filter.GetType().GetMethod("Invoke")!.GetParameters()[0].ParameterType;
            var callbackOrder = new List<int>();
            var predicate = AdaptBool(callbackType, value => { callbackOrder.Add(value); return value > 1; });
            Assert.Empty(callbackOrder);
            var filtered = filter.DynamicInvoke(predicate)!;
            Assert.Equal(new[] { 1, 2, 3 }, callbackOrder);
            Assert.Equal(2, Call<int>(type, "lengthOf", filtered));
            Assert.Equal(2, Call<int>(type, "firstOf", filtered));
            _ = filter.DynamicInvoke(predicate);
            Assert.Equal(new[] { 1, 2, 3, 1, 2, 3 }, callbackOrder);
            var failPredicate = AdaptBool(callbackType, _ => throw new InvalidOperationException("callback failure"));
            var callbackFailure = Assert.Throws<TargetInvocationException>(() => filter.DynamicInvoke(failPredicate));
            Assert.IsType<InvalidOperationException>(callbackFailure.InnerException);

            var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = Call<Task<int>>(type, "asyncReceiver", gate.Task);
            Assert.False(pending.IsCompleted);
            gate.SetResult(1);
            Assert.Equal(2, await pending.WaitAsync(TimeSpan.FromSeconds(10)));
            var failedGate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending = Call<Task<int>>(type, "asyncReceiver", failedGate.Task);
            var expected = new InvalidOperationException("async receiver failure");
            failedGate.SetException(expected);
            Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(async () => await pending));
        });
        RejectInvalidPrograms();
        await ExtensionPrecedence();
        await Imports();
    }

    private static void RejectInvalidPrograms()
    {
        string[] cases =
        [
            "fn bad(): Unit { let f = [1].append()\n let x = f(\"wrong\")\n () }",
            "fn bad(): Unit { let f = [1].get()\n let x = f(\"wrong\")\n () }",
            "fn bad(): Unit { let f = [1].filter()\n let x = f(value => value + 1)\n () }",
            "fn bad(): Unit { let f = Map.empty<int,int>().set()\n let x = f(1, 2, 3)\n () }",
            "fn bad(): Unit { [1].append()\n () }",
            "fn bad(): Unit { let b = MutableList<int>()\n let f = b.add()\n () }",
            "fn bad(): Unit { let b = MutableMap<int,int>()\n let f = b.set()\n () }",
            "interface A { get(): int }\nfn bad(a: A): Unit { let m = Map.empty<int,A>()\n let f = m.get()\n () }",
            "interface A { get(): int }\nfn make<T>(m: Map<int,T>): (int) -> Option<T> { m.get() }\nfn bad(m: Map<int,A>): Unit { let f = make<A>(m)\n () }",
            "resource Cursor { read(value: int): int { value }\n release(): Unit { () } }\nfn bad(): Unit { using c = Cursor() { let f = c.read()\n () } }",
            "fn bad(): Unit { let f = [1].map()\n () }",
            "fn bad(): Unit { let f = [1].fold()\n () }",
            "fn bad(callback: (int)->bool) { callback |> [1].filter() }"
        ];
        foreach (var declaration in cases)
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + declaration, "invalid-empty-collection.ds");
            Assert.True(result.CSharp is null, "Invalid collection partial compiled: " + declaration);
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, diagnostic => Assert.StartsWith("DS2", diagnostic.Code));
        }
    }

    private static async Task ExtensionPrecedence()
    {
        const string source = """
            module Demo
            extend List<int> {
                fn get(self): int { 77 }
            }
            fn direct(): int { [42].get() }
            fn answer(): bool {
                let values = [42]
                let full = values.get()
                let partial: (int) -> Option<int> = values.get()
                let fromPipe: Option<int> = 0 |> values.get()
                full == 77 && direct() == 77 && partial(0) == Some(42) && fromPipe == Some(42)
            }
            """;
        var result = DSharpCompiler.Compile(source, "empty-collection-precedence.ds");
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        await WithGenerated(result.CSharp!, "Demo", type => { Assert.True(Call<bool>(type, "answer")); return Task.CompletedTask; });

        const string matching = """
            module Demo
            extend List<int> { fn get(self): (int) -> Option<int> { (index: int) => Some(999) } }
            fn answer(): bool { ([42].get())(0) == Some(999) }
            """;
        var same = DSharpCompiler.Compile(matching, "empty-collection-matching-extension.ds");
        Assert.True(same.CSharp is not null, string.Join("\n", same.Diagnostics));
        Assert.Empty(same.Diagnostics);
        await WithGenerated(same.CSharp!, "Demo", type => { Assert.True(Call<bool>(type, "answer")); return Task.CompletedTask; });

        const string nullableKey = """
            module Demo
            extend Map<string?, int> { fn get(self): int { 77 } }
            fn answer(): bool {
                let table = Map.empty<string?,int>().set(null, 42)
                let previous = table.get()
                let found: Option<int> = null |> table.get()
                previous == 77 && found == Some(42)
            }
            """;
        var nullable = DSharpCompiler.Compile(nullableKey, "empty-collection-null-pipe.ds");
        Assert.True(nullable.CSharp is not null, string.Join("\n", nullable.Diagnostics));
        Assert.Empty(nullable.Diagnostics);
        await WithGenerated(nullable.CSharp!, "Demo", type => { Assert.True(Call<bool>(type, "answer")); return Task.CompletedTask; });

        const string incompatibleCallback = """
            module Demo
            extend Map<string?, int> { fn get(self): (int) -> int { (value: int) => value } }
            fn answer(): bool {
                let table = Map.empty<string?,int>().set(null, 42)
                let found: Option<int> = null |> table.get()
                found == Some(42)
            }
            """;
        var incompatible = DSharpCompiler.Compile(incompatibleCallback, "empty-collection-null-incompatible.ds");
        Assert.True(incompatible.CSharp is not null, string.Join("\n", incompatible.Diagnostics));
        Assert.Empty(incompatible.Diagnostics);
        await WithGenerated(incompatible.CSharp!, "Demo", type => { Assert.True(Call<bool>(type, "answer")); return Task.CompletedTask; });

        const string compatibleCallback = """
            module Demo
            extend Map<string?, int> { fn get(self): (string?) -> int { (value: string?) => 77 } }
            fn answer(): int { null |> Map.empty<string?,int>().get() }
            """;
        var compatible = DSharpCompiler.Compile(compatibleCallback, "empty-collection-null-compatible.ds");
        Assert.True(compatible.CSharp is not null, string.Join("\n", compatible.Diagnostics));
        Assert.Empty(compatible.Diagnostics);
        await WithGenerated(compatible.CSharp!, "Demo", type => { Assert.Equal(77, Call<int>(type, "answer")); return Task.CompletedTask; });
    }

    private static async Task Imports()
    {
        var sources = new[]
        {
            new SourceFile("lib.ds", "module Lib\nfn values(): List<int> { [40] }\nfn value(): int { 2 }"),
            new SourceFile("app.ds", "module Demo\nimport Lib\nfn answer(): bool { let append = values().append()\n let values = value() |> append\n values.get(1) == Some(2) && values.get(0) == Some(40) }")
        };
        var result = DSharpCompiler.Compile(sources);
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        await WithGenerated(result.CSharp!, "Demo", type => { Assert.True(Call<bool>(type, "answer")); return Task.CompletedTask; });
    }

    private static MethodInfo Method(Type type, string name) => type.GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
    private static T Call<T>(Type type, string name, params object?[] args) => (T)Method(type, name).Invoke(null, args)!;
    private static Delegate AdaptInt(Type type, Func<int,int> callback)
    {
        var value = Expression.Parameter(typeof(int), "value");
        return Expression.Lambda(type, Expression.Invoke(Expression.Constant(callback), value), value).Compile();
    }
    private static Delegate AdaptBool(Type type, Func<int,bool> callback)
    {
        var value = Expression.Parameter(typeof(int), "value");
        return Expression.Lambda(type, Expression.Invoke(Expression.Constant(callback), value), value).Compile();
    }
    private static async Task WithGenerated(string csharp, string module, Func<Type,Task> verify)
    {
        var assemblyName = "DSharpEmptyCollectionPartial_" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-empty-collection-partial-" + Guid.NewGuid());
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
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated empty collection partial build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("empty-collection-partial-" + Guid.NewGuid(), isCollectible: false);
            var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0", assemblyName + ".dll"));
            var type = Assert.Single(assembly.GetTypes(), candidate => candidate.Name == module);
            await verify(type);
        }
        finally { Directory.Delete(directory, true); }
    }
}
