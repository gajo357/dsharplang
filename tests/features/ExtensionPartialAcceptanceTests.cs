using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class ExtensionPartialAcceptanceTests
{
    // Called by the existing feature aggregator to keep CI's bounded report small.
    public async Task ExtensionPartialsCaptureAndExecute()
    {
        const string source = """
            module Demo
            type Invoice { value: int }
            type Failure { | Failed }
            fn make(mark: (int) -> int): Invoice {
                let ignored = mark(1)
                Invoice { value = 40 }
            }
            extend Invoice {
                fn add(self, fee: int, extra: int): int { self.value + fee + extra }
                fn apply(self, fee: int, mark: (int) -> int): int { mark(self.value + fee) }
                fn fail(self, fee: int, extra: int): int { self.value / (extra - extra) }
                async fn later(self, fee: int, gate: Task<int>): int { self.value + fee + await gate }
                async fn watch(self, fee: int, mark: (int) -> int, gate: Task<int>): int { let value = mark(self.value + fee)
                    value + await gate }
                fn exact(self, fee: int): int { self.value + fee }
                fn exact(self, fee: int, extra: int): int { self.value + fee + extra }
                fn nullPrefix(self, text: string?, extra: int): int { if text == null { self.value + extra } else { 0 } }
                fn choose(self, fee: int, extra: int): int { self.value + fee + extra }
                fn choose(self, fee: int, suffix: string): int { self.value + fee + 2 }
                fn triple(self, first: int, second: int, third: int): int { self.value + first + second + third }
            }
            extend List<int> {
                fn length(self, first: int, second: int): int { self.length() + first + second }
                fn get(self, first: int, second: int): int { self.length() + first + second }
                fn append(self, first: int, second: int): int { self.length() + first + second }
            }
            extend Option<string?> {
                fn inspect(self, supplied: int, extra: int): int {
                    match self { None => supplied + extra
                                 Some(null) => supplied + extra + 1
                                 Some(value) => supplied + extra + 2 }
                }
            }
            extend int {
                fn plus(self, first: int, second: int): int { self + first + second }
            }
            extend string? {
                fn lengthOr(self, fallback: int, extra: int): int {
                    if self == null { fallback + extra } else { extra }
                }
            }
            fn identity<T>(value: T): T { value }
            fn makePartial(mark: (int) -> int): (int) -> int {
                make(mark).add(mark(2))
            }
            fn makeApply(mark: (int) -> int): ((int) -> int) -> int {
                make(mark).apply(mark(2))
            }
            fn makeFail(): (int) -> int { Invoice { value = 40 }.fail(1) }
            fn makeAsync(): (Task<int>) -> Task<int> { Invoice { value = 40 }.later(1) }
            fn makeAsyncWatch(): ((int) -> int, Task<int>) -> Task<int> { Invoice { value = 40 }.watch(1) }
            fn earlyArgument(): int {
                let partial = Invoice { value = 40 }.add(if true { return 42 } else { 1 })
                partial(0)
            }
            fn earlyReceiver(): int {
                let partial = (if true { return 42 } else { Invoice { value = 40 } }).add(1)
                partial(0)
            }
            fn propagated(value: Result<int, Failure>): Result<int, Failure> {
                let partial = Invoice { value = 40 }.add(value?)
                Success(partial(1))
            }
            fn propagatedError(): bool {
                match propagated(Error(Failure.Failed)) {
                    Error(Failure.Failed) => true
                    Success(value) => false
                }
            }
            fn pipeExpected(): int {
                let invoice = Invoice { value = 35 }
                2 |> invoice.choose(5)
            }
            fn nullablePipe(): int? { 2 |> 39.plus(1) }
            fn inferredPipe() {
                let invoice = Invoice { value = 35 }
                2 |> invoice.choose(5)
            }
            fn generic(value: int): (int) -> int { identity<Invoice>(Invoice { value = value }).add(1) }
            fn result(): bool {
                let invoice = Invoice { value = 35 }
                let chooseText: (string) -> int = invoice.choose(5)
                let chooseInt: (int) -> int = invoice.choose(5)
                let exactPartial: (int) -> int = invoice.exact(5)
                let nullable: string? = null
                let missing = nullable.lengthOr(40)
                let nullPrefix = invoice.nullPrefix(null)
                let absent: Option<string?> = None
                let presentNull: Option<string?> = Some(null)
                let noneCase = absent.inspect(40)
                let someNullCase = presentNull.inspect(39)
                let grouped = invoice.triple(1)(2)
                let values: List<int> = [1, 2]
                let partialLength = values.length(38)
                (2 |> invoice.add(5)) == 42 && grouped(4) == 42 &&
                chooseText("x") == 42 && chooseInt(2) == 42 &&
                missing(2) == 42 && nullPrefix(7) == 42 && noneCase(2) == 42 && someNullCase(2) == 42 && (generic(40))(1) == 42 &&
                partialLength(2) == 42 && values.get(1) == Some(2) &&
                values.get(19, 21) == 42 && values.append(3).length() == 3 &&
                invoice.exact(7) == 42 && exactPartial(2) == 42 && (2 |> invoice.exact(5)) == 42 && earlyArgument() == 42 && earlyReceiver() == 42 &&
                propagatedError() && pipeExpected() == 42 && nullablePipe() == 42 && inferredPipe() == 42
            }
            """;
        var result = DSharpCompiler.Compile(source, "extension-partials.ds");
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        await WithGenerated(result.CSharp!, "Demo", async type =>
        {
            Assert.True(Call<bool>(type, "result"));
            var events = new List<int>();
            var factory = Method(type, "makePartial");
            var mark = Adapt(factory.GetParameters()[0].ParameterType, value => { events.Add(value); return value; });
            var partial = Assert.IsAssignableFrom<Delegate>(factory.Invoke(null, new object[] { mark }));
            Assert.Equal(new[] { 1, 2 }, events);
            Assert.Equal(42, partial.DynamicInvoke(0));
            Assert.Equal(43, partial.DynamicInvoke(1));
            Assert.Equal(new[] { 1, 2 }, events);

            var failedOrder = new List<int>();
            var failsAtReceiver = Adapt(factory.GetParameters()[0].ParameterType, value =>
            {
                failedOrder.Add(value);
                throw new InvalidOperationException("receiver failed");
            });
            var receiverFailure = Assert.Throws<TargetInvocationException>(() => factory.Invoke(null, new object[] { failsAtReceiver }));
            Assert.IsType<InvalidOperationException>(receiverFailure.InnerException);
            Assert.Equal(new[] { 1 }, failedOrder);
            failedOrder.Clear();
            var failsAtArgument = Adapt(factory.GetParameters()[0].ParameterType, value =>
            {
                failedOrder.Add(value);
                if (value == 2) throw new InvalidOperationException("argument failed");
                return value;
            });
            var argumentFailure = Assert.Throws<TargetInvocationException>(() => factory.Invoke(null, new object[] { failsAtArgument }));
            Assert.IsType<InvalidOperationException>(argumentFailure.InnerException);
            Assert.Equal(new[] { 1, 2 }, failedOrder);

            var applyFactory = Method(type, "makeApply");
            var applyMark = Adapt(applyFactory.GetParameters()[0].ParameterType, value => { events.Add(value); return value; });
            var apply = Assert.IsAssignableFrom<Delegate>(applyFactory.Invoke(null, new object[] { applyMark }));
            Assert.Equal(new[] { 1, 2, 1, 2 }, events);
            var invokeParameter = apply.GetType().GetMethod("Invoke")!.GetParameters()[0].ParameterType;
            var targetCalls = 0;
            var callback = Adapt(invokeParameter, value => { targetCalls++; return value; });
            Assert.Equal(0, targetCalls);
            Assert.Equal(42, apply.DynamicInvoke(callback));
            Assert.Equal(42, apply.DynamicInvoke(callback));
            Assert.Equal(2, targetCalls);

            var safeUntilCalled = Call<Delegate>(type, "makeFail");
            var thrown = Assert.Throws<TargetInvocationException>(() => safeUntilCalled.DynamicInvoke(0));
            Assert.IsType<DivideByZeroException>(thrown.InnerException);
            var watch = Call<Delegate>(type, "makeAsyncWatch");
            var watchType = watch.GetType().GetMethod("Invoke")!.GetParameters()[0].ParameterType;
            var watchCalls = 0;
            var watchCallback = Adapt(watchType, value => { watchCalls++; return value; });
            Assert.Equal(0, watchCalls);
            var watchGate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var watching = Assert.IsAssignableFrom<Task<int>>(watch.DynamicInvoke(watchCallback, watchGate.Task));
            Assert.Equal(1, watchCalls);
            Assert.False(watching.IsCompleted);
            watchGate.SetResult(1);
            Assert.Equal(42, await watching.WaitAsync(TimeSpan.FromSeconds(10)));

            var asyncPartial = Call<Delegate>(type, "makeAsync");
            var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = Assert.IsAssignableFrom<Task<int>>(asyncPartial.DynamicInvoke(gate.Task));
            Assert.False(pending.IsCompleted);
            gate.SetResult(1);
            Assert.Equal(42, await pending.WaitAsync(TimeSpan.FromSeconds(10)));
            var failure = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending = Assert.IsAssignableFrom<Task<int>>(asyncPartial.DynamicInvoke(failure.Task));
            var expected = new InvalidOperationException("async extension failed");
            failure.SetException(expected);
            Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(async () => await pending));
            var cancel = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending = Assert.IsAssignableFrom<Task<int>>(asyncPartial.DynamicInvoke(cancel.Task));
            cancel.SetCanceled();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
        });

        await ImportsAndAmbiguity();
        RejectInvalidPrograms();
    }

    private static async Task ImportsAndAmbiguity()
    {
        var model = new SourceFile("model.ds", "module Model\ntype Invoice { value: int }");
        var extensions = new SourceFile("pricing.ds", """
            module Pricing
            import Model
            extend Invoice { fn add(self, fee: int, extra: int): int { self.value + fee + extra } }
            """);
        var app = new SourceFile("app.ds", """
            module App
            import Model
            import Pricing
            import Pricing
            fn answer(): int {
                let invoice = Invoice { value = 35 }
                let fee = invoice.add(5)
                2 |> fee
            }
            """);
        var result = DSharpCompiler.Compile(new[] { model, extensions, app });
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        await WithGenerated(result.CSharp!, "App", type =>
        {
            Assert.Equal(42, Call<int>(type, "answer"));
            return Task.CompletedTask;
        });
        var distinct = new SourceFile("different.ds", """
            module Other
            import Model
            extend Invoice { fn add(self, fee: int, extra: string): int { self.value + fee + 2 } }
            """);
        var selected = new SourceFile("selected.ds", """
            module App
            import Model
            import Pricing
            import Other
            fn answer(): int {
                let invoice = Invoice { value = 35 }
                let chosen: (string) -> int = invoice.add(5)
                chosen("ok")
            }
            """);
        var selectedResult = DSharpCompiler.Compile(new[] { model, extensions, distinct, selected });
        Assert.True(selectedResult.CSharp is not null, string.Join("\n", selectedResult.Diagnostics));
        await WithGenerated(selectedResult.CSharp!, "App", type =>
        {
            Assert.Equal(42, Call<int>(type, "answer"));
            return Task.CompletedTask;
        });
        var transit = new SourceFile("transit.ds", "module Transit\nimport Pricing\nfn ok(): int { 0 }");
        var hidden = new SourceFile("hidden.ds", """
            module App
            import Model
            import Transit
            fn answer(): int { let p = Invoice { value = 35 }.add(5)
                p(2) }
            """);
        Reject(new[] { model, extensions, transit, hidden });
        var other = new SourceFile("other.ds", """
            module Other
            import Model
            extend Invoice { fn add(self, fee: int, extra: int): int { self.value + fee + extra + 1 } }
            """);
        string? message = null;
        foreach (var imports in new[] { "import Pricing\nimport Other", "import Other\nimport Pricing" })
        {
            var source = new SourceFile("ambiguous.ds", "module App\nimport Model\n" + imports +
                "\nfn answer(): int { let p = Invoice { value = 35 }.add(5)\n p(2) }");
            var ambiguous = DSharpCompiler.Compile(new[] { model, extensions, other, source });
            Assert.Null(ambiguous.CSharp);
            Assert.NotEmpty(ambiguous.Diagnostics);
            var actual = string.Join("\n", ambiguous.Diagnostics.Select(d => d.Message));
            Assert.Contains("Pricing", actual, StringComparison.Ordinal);
            Assert.Contains("Other", actual, StringComparison.Ordinal);
            if (message is null) message = actual;
            else Assert.Equal(message, actual);
        }
    }

    private static void RejectInvalidPrograms()
    {
        var invalid = new[]
        {
            "extend Invoice { fn add(self, x: int, y: int): int { x + y } }\nfn bad(): int { let p = Invoice { value = 40 }.add(\"x\")\n p(2) }",
            "extend Invoice { fn add(self, x: int, y: int): int { x + y } }\nfn bad(): int { Invoice { value = 40 }.add(1, 2, 3) }",
            "extend Invoice { fn add(self, x: int, y: int): int { x + y } }\nfn bad(): int { Invoice { value = 40 }.add(1)\n 42 }",
            "extend Invoice { fn add(self, x: int, y: int): int { x + y } }\nfn bad(): int { let p: (bool) -> int = Invoice { value = 40 }.add(1)\n 42 }",
            "extend List<int> { fn append(self, first: int, second: int): int { first + second } }\nfn bad(): Unit { let values: List<int> = [1, 2]\n let p: (int) -> int = values.append(1)\n () }",
            "extend Invoice { fn choose(self, x: int?, y: int): int { y }\n fn choose(self, x: string?, y: int): int { y } }\nfn bad(): int { let p = Invoice { value = 40 }.choose(null)\n 42 }",
            "extend Invoice { fn choose(self, x: int, y: int): int { x }\n fn choose(self, x: int, y: string): int { x } }\nfn bad(): int { let p = Invoice { value = 40 }.choose(1)\n 42 }",
            "interface A { get(): int }\nservice S: A { get() { 42 } }\nextend A { fn add(self, x: int, y: int): int { self.get() + x + y } }\nfn bad(s: A): Unit { let p = s.add(1)\n () }",
            "interface A { get(): int }\nservice S: A { get() { 42 } }\nextend int { fn use(self, s: A, x: int): int { self + s.get() + x } }\nfn bad(s: A): Unit { let p = 1.use(s)\n () }",
            "resource Cursor { read(): int { 42 }\n release(): Unit { () } }\nextend Cursor { fn add(self, x: int, y: int): int { self.read() + x + y } }\nfn bad(): Unit { using c = Cursor() { let p = c.add(1)\n () } }",
            "extend MutableList<int> { fn addSome(self, x: int, y: int): int { x + y } }\nfn bad(): Unit { let b = MutableList<int>()\n let p = b.addSome(1)\n () }"
        };
        foreach (var body in invalid)
        {
            var result = DSharpCompiler.Compile("module Invalid\ntype Invoice { value: int }\n" + body, "invalid-partial-extension.ds");
            Assert.True(result.CSharp is null, "Invalid program compiled: " + body);
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, diagnostic => Assert.StartsWith("DS2", diagnostic.Code));
        }
    }

    private static void Reject(IReadOnlyList<SourceFile> sources)
    {
        var result = DSharpCompiler.Compile(sources);
        Assert.Null(result.CSharp);
        Assert.NotEmpty(result.Diagnostics);
    }

    private static MethodInfo Method(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
    private static T Call<T>(Type type, string name, params object?[] args) => (T)Method(type, name).Invoke(null, args)!;
    private static Delegate Adapt(Type type, Func<int, int> action)
    {
        var value = Expression.Parameter(typeof(int), "value");
        return Expression.Lambda(type, Expression.Invoke(Expression.Constant(action), value), value).Compile();
    }

    private static async Task WithGenerated(string csharp, string module, Func<Type, Task> verify)
    {
        var assemblyName = "DSharpExtensionPartial_" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-extension-partial-" + Guid.NewGuid());
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
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated extension partial build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("extension-partial-" + Guid.NewGuid(), isCollectible: false);
            var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0", assemblyName + ".dll"));
            var type = Assert.Single(assembly.GetTypes(), candidate => candidate.Name == module);
            await verify(type);
        }
        finally { Directory.Delete(directory, true); }
    }
}
