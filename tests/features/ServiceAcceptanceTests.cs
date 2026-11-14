using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class ServiceAcceptanceTests
{
    [Fact(DisplayName = "Interfaces and services")]
    public async Task ServicesDispatchAndEnforceCapabilityBoundaries()
    {
        // The host observes an incomplete Task and constructor argument order;
        // the compiler's generated C# is compiled with warnings as errors.
        const string source = """
            module Demo

            interface Price {
                /// Produce a checked price for a value.
                calculate(value: int): int
            }
            interface Quote { eligible(value: int): bool }
            interface Mirror { calculate(value: int): int }
            interface Empty { }
            interface Getter { get(): int
                               count(): int }
            interface AsyncPrice {
                /// Complete the calculation asynchronously.
                async calculate(input: Task<int>): int
            }
            type Box<T> { value: T? }
            interface ReadBox { read(item: Box<int?>): int? }
            interface AsyncBoundary { async decide(input: Task<int>): Result<int, string> }
            interface Transform { apply(value: int, callback: (int) -> int): Result<int?, string> }
            interface ObjectCalls {
                Equals(value: int): int
                ToString(): string
                GetType(): int
            }

            service Fixed(amount: int): Price, Quote, Mirror {
                /// Return the explicitly supplied amount.
                calculate(value) { amount }
                eligible(value: int): bool { value > 0 }
            }
            service Doubled(amount: int): Price {
                calculate(value) { amount * 2 }
            }
            service WithFee(underlying: Price, fee: int): Price {
                calculate(value) { underlying.calculate(value) + fee }
            }
            service AsyncFixed(offset: int): AsyncPrice {
                /// The async marker and types come from the interface.
                calculate(input) {
                    let value = await input
                    value + offset
                }
            }
            service Reader: ReadBox {
                read(item) { item.value }
            }
            service Unused(amount: int): Empty { }
            service ReadOne: Getter { get() { 42 }
                                      count() { 1 } }
            service Checked: AsyncBoundary {
                decide(input) {
                    let value = (await readResult(input))?
                    if value == 40 { return Success(value + 2) }
                    Success(value)
                }
            }
            service Transformer: Transform {
                apply(value, callback) { Success(callback(value)) }
            }
            service Names: ObjectCalls {
                Equals(value) { value + 1 }
                ToString() { "name" }
                GetType() { 42 }
            }
            service Pair(first: int, second: int): Price {
                calculate(value) { first * 10 + second }
            }
            fn use(pricer: Price): int { pricer.calculate(0) }
            fn identity<T>(value: T): T { value }
            fn applyPrice(callback: (Price) -> int, value: Price): int { callback(value) }
            fn safeFunctionPartial(): int {
                let callback: (Price) -> int = use
                let later: (Price) -> int = applyPrice(callback)
                later(Fixed(42))
            }
            fn choose(flag: bool): Price {
                if flag { Fixed(20) } else { Doubled(11) }
            }
            fn chooseMatch(flag: bool): Price {
                match flag { true => Fixed(20)
                             false => Doubled(11) }
            }
            fn inferredFirst(items: List<int>) { items.get(0) }
            fn inferredLocal() { let items = [1]
                items.get(0) }
            fn inferredCount(items: Map<int, int>) { items.count() }
            fn listAndService(): int {
                let getter: Getter = ReadOne()
                let local = match inferredLocal() { Some(value) => value
                                                    None => 0 }
                match inferredFirst([1]) {
                    Some(value) => getter.get() + value + inferredCount(Map.empty<int, int>().set(1, 42)) + local - 3
                    None => 0
                }
            }
            fn answer(): int {
                let first: Price = Fixed(20)
                let second: Price = Doubled(11)
                let quote: Quote = Fixed(20)
                let mirror: Mirror = Fixed(20)
                let concrete = Fixed(20)
                let forwarded: Price = identity<Price>(Fixed(20))
                let decorated: Price = WithFee(first, 2)
                let boxed: Box<int?> = Box { value = 42 }
                let reader: ReadBox = Reader()
                let names: ObjectCalls = Names()
                let empty: Empty = Unused(1)
                let transform: Transform = Transformer()
                let transformed = match transform.apply(41, value => value + 1) {
                    Success(null) => false
                    Success(value) => value == 42
                    Error(_) => false
                }
                if quote.eligible(1) && mirror.calculate(0) == 20 &&
                   concrete.calculate(0) == 20 && use(forwarded) == 20 &&
                   use(decorated) == 22 && reader.read(boxed) == 42 &&
                   names.Equals(41) == 42 && names.ToString() == "name" && names.GetType() == 42 &&
                   use(choose(true)) == 20 && use(choose(false)) == 22 &&
                   use(chooseMatch(true)) == 20 && use(chooseMatch(false)) == 22 && listAndService() == 42 &&
                   transformed && safeFunctionPartial() == 42 {
                    use(first) + use(second)
                } else { 0 }
            }
            fn dependencies(mark: (int) -> int): int {
                let item: Price = Pair(mark(4), mark(2))
                item.calculate(0)
            }
            fn receiver(mark: (int) -> int): Price {
                let ignored = mark(1)
                Fixed(40)
            }
            fn receiverOrder(mark: (int) -> int): int {
                receiver(mark).calculate(mark(2)) + 2
            }
            async fn makeService(): Price { Fixed(40) }
            async fn readResult(input: Task<int>): Result<int, string> {
                let value = await input
                if value < 0 { Error("negative") } else { Success(value) }
            }
            async fn delayedBoundary(input: Task<int>): int {
                let value: AsyncBoundary = Checked()
                match (await value.decide(input)) { Success(number) => number
                                                   Error(_) => 42 }
            }
            async fn asyncFactory(): int {
                let value = await makeService()
                use(value) + 2
            }
            async fn asyncDispatch(input: Task<int>): int {
                let value: AsyncPrice = AsyncFixed(2)
                await value.calculate(input)
            }
            """;

        var parsed = new Parser(Lexer.Lex(source, "services-acceptance.ds"), "services-acceptance.ds").Parse();
        Assert.Contains("Produce a checked price", Assert.Single(parsed.Interfaces.Single(item => item.Name.Text == "Price").Methods.Single().DocumentationComments).Text);
        Assert.Contains("Complete the calculation", Assert.Single(parsed.Interfaces.Single(item => item.Name.Text == "AsyncPrice").Methods.Single().DocumentationComments).Text);
        Assert.Contains("Return the explicitly", Assert.Single(parsed.Services.Single(item => item.Name.Text == "Fixed").Methods.Single(item => item.Name.Text == "calculate").DocumentationComments).Text);
        Assert.Contains("async marker", Assert.Single(parsed.Services.Single(item => item.Name.Text == "AsyncFixed").Methods.Single().DocumentationComments).Text);

        var compiled = DSharpCompiler.Compile(source, "services-acceptance.ds");
        Assert.Empty(compiled.Diagnostics);
        Assert.NotNull(compiled.CSharp);
        await WithGenerated(compiled.CSharp!, "Demo", async type =>
        {
            Assert.Equal(42, Call<int>(type, "answer"));
            Assert.Equal(42, await Call<Task<int>>(type, "asyncFactory").WaitAsync(TimeSpan.FromSeconds(10)));

            var method = Method(type, "dependencies");
            var seen = new List<int>();
            var marker = Adapt(method.GetParameters()[0].ParameterType, value => { seen.Add(value); return value; });
            Assert.Equal(42, method.Invoke(null, new object[] { marker }));
            Assert.Equal(new[] { 4, 2 }, seen);
            seen.Clear();
            Assert.Equal(42, Method(type, "receiverOrder").Invoke(null, new object[] { marker }));
            Assert.Equal(new[] { 1, 2 }, seen);

            var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = Call<Task<int>>(type, "asyncDispatch", gate.Task);
            Assert.False(pending.IsCompleted);
            gate.SetResult(40);
            Assert.Equal(42, await pending.WaitAsync(TimeSpan.FromSeconds(10)));
            var failed = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var failure = Call<Task<int>>(type, "asyncDispatch", failed.Task);
            failed.SetException(new InvalidOperationException("service failure"));
            Assert.Equal("service failure", (await Assert.ThrowsAsync<InvalidOperationException>(async () => await failure)).Message);

            var returned = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var delayedReturn = Call<Task<int>>(type, "delayedBoundary", returned.Task);
            Assert.False(delayedReturn.IsCompleted);
            returned.SetResult(40);
            Assert.Equal(42, await delayedReturn.WaitAsync(TimeSpan.FromSeconds(10)));
            var propagated = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var delayedError = Call<Task<int>>(type, "delayedBoundary", propagated.Task);
            Assert.False(delayedError.IsCompleted);
            propagated.SetResult(-1);
            Assert.Equal(42, await delayedError.WaitAsync(TimeSpan.FromSeconds(10)));
        });

        RejectInvalidServices();
        await MultiFileDispatch();
        await NominalShadows();
    }

    private static void RejectInvalidServices()
    {
        var invalid = new[]
        {
            // Contract and implementation mismatches, including sync/async.
            "interface A { get(): int }\nservice S: A { }",
            "interface A { get(): int\n get(): int }\nservice S: A { get() { 1 } }",
            "interface A { get(): int }\nservice S: A { get() { 1 }\n get() { 2 } }",
            "interface A { get(value: int): int }\nservice S: A { get(value: bool): int { 1 } }",
            "interface A { get(): int }\nservice S: A { get(): bool { true } }",
            "interface A { async get(): int }\nservice S: A { get(): bool { true } }",
            "interface A { get(): int }\nservice S: A { async get() { 1 } }",
            "interface A { get(): int }\ninterface B { get(): bool }\nservice S: A, B { get() { 1 } }",
            "interface A { get(): int }\nservice S: A, A { get() { 1 } }",
            "interface A { get(): int }\nservice S: A { get() { 1 }\n extra() { 2 } }",
            "interface A { get(): int }\nservice S: A { get() { 1 } }\nservice Child: S { }",
            // Explicit constructor dependencies are checked and immutable.
            "interface A { get(): int }\nservice S(value: int): A { get() { value } }\nfn f(): A { S(true) }",
            "interface A { get(): int }\nservice S(value: int): A { get() { value } }\nfn f(): A { S() }",
            "interface A { get(value: int): int }\nservice S(value: int): A { get(value) { value } }",
            "interface A { get(): int }\nservice S(value: int): A { get() { let value = 1\n value } }",
            "interface A { get(): int }\nservice S(work: Task<int>): A { get() { 1 } }",
            "interface A { get(): int }\nservice S(work: () -> int): A { get() { 1 } }",
            // Capabilities are not immutable data and cannot be captured.
            "interface A { get(): int }\ntype Box { value: A }",
            "interface A { get(): int }\ntype Box<T> { value: T }\nservice S: A { get() { 1 } }\nfn f(): Box<A> { Box { value = S() } }",
            "interface A { get(): int }\nservice S: A { get() { 1 } }\nfn wrap<T>(value: T): Option<T> { Some(value) }\nfn f(): Unit { let x = wrap(S())\n () }",
            "interface A { get(): int }\nservice S: A { get() { 1 } }\nfn wrap<T>(value: T): Option<T> { Some(value) }\nfn forward<T>(value: T): Option<T> { wrap<T>(value) }\nfn f(): Unit { let x = forward(S())\n () }",
            "interface A { get(): int }\nservice S: A { get() { 1 } }\nfn f(): Unit { let value: A = S()\n let callback: () -> int = () => value.get()\n () }",
            "interface A { get(): int }\nservice S: A { get() { 1 } }\nfn f(): Unit { let values = [S()]\n () }",
            "interface A { get(): int }\nservice S: A { get() { 1 } }\nfn f(): Unit { let builder = MutableList<A>()\n () }",
            "interface A { get(): int }\nfn f(value: A?): Unit { () }",
            "interface A { get(): int }\nfn f(): Unit { let values = Map.empty<A, int>()\n () }",
            "interface A { get(): int }\nfn f(): Unit { let values = Map.empty<string, A>()\n () }",
            // A partial application stores supplied values in its closure.
            // This is forbidden for capabilities even when immediately invoked
            // or hidden by generic inference and pipe lowering.
            "interface A { get(): int }\nservice S: A { get() { 42 } }\nfn use(value: A, extra: int): int { value.get() + extra }\nfn f(): (int) -> int { use(S()) }",
            "interface A { get(): int }\nservice S: A { get() { 42 } }\nfn use(value: A, extra: int): int { value.get() + extra }\nfn f(): Unit { let callback = use(S())\n () }",
            "interface A { get(): int }\nservice S: A { get() { 42 } }\nfn use(value: A, extra: int): int { value.get() + extra }\nfn f(): int { (use(S()))(0) }",
            "interface A { get(): int }\nservice S: A { get() { 42 } }\nfn use(value: A, extra: int): int { value.get() + extra }\nfn f(): Unit { let callback = S() |> use\n () }",
            "interface A { get(): int }\nservice S: A { get() { 42 } }\nfn keep<T>(value: T, extra: int): int { extra }\nfn f(): Unit { let callback = keep(S())\n () }",
            "interface A { get(): int }\nservice S: A { get() { 42 } }\nfn use(value: A, extra: int): int { value.get() + extra }\nfn consume(callback: (int) -> int): int { callback(0) }\nfn f(): int { consume(use(S())) }",
            "interface A { get(): int }\nservice S: A { get() { 42 } }\nfn use(value: A, extra: int): int { value.get() + extra }\nfn f(): Unit { let operation: (A, int) -> int = use\n let callback = operation(S())\n () }",
            "interface A { get(): int }\nservice S: A { get() { 42 } }\nfn pair<T>(value: T, extra: int): T { value }\nfn make<T>(value: T): (int) -> T { pair<T>(value) }\nfn f(): Unit { let callback = make<A>(S())\n () }",
            "interface A { get(): int }\nservice S: A { get() { 42 } }\nfn pair<T>(value: T, extra: int): T { value }\nfn make<T>(value: T): (int) -> T { pair<T>(value) }\nfn forward<T>(value: T): (int) -> T { make<T>(value) }\nfn f(): Unit { let callback = forward<A>(S())\n () }",
            // A function reference whose generic body stores T in Option must
            // not bypass the same capability-in-data restriction as a call.
            "interface A { get(): int }\nfn save<T>(value: T): int { let boxed: Option<T> = Some(value)\n 1 }\nfn f(): Unit { let callback: (A) -> int = save<A>()\n () }",
            "interface A { get(): int }\nservice S: A { get() { 1 } }\nfn f(): bool { S() == S() }",
            "interface A { get(): int }\nfn f(left: A, right: A): bool { left == right }",
            "interface A { get(): int }\ninterface B { get(): int }\nservice S: A { get() { 1 } }\nfn f(): B { S() }",
            "interface A { get(): int }\nservice S: A { get() { 1 } }\nfn f(value: A): int { value.missing() }",
            // Interface dispatch is indirect: names also used by collections
            // must not let omitted-return inference guess through a call cycle.
            "interface A { get(): int }\nfn inferred(value: A) { value.get() }",
            "interface A { get(): int }\nservice S: A { get() { inferred(S()) } }\nfn inferred(value: A) { value.get() }",
            "interface A { count(): int }\nfn inferred(value: A) { value.count() }",
        };
        foreach (var body in invalid)
        {
            var result = DSharpCompiler.Compile("module Demo\n" + body, "service-invalid.ds");
            Assert.True(result.CSharp is null, $"Invalid service program compiled: {body}");
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, diagnostic => Assert.Equal("service-invalid.ds", diagnostic.Path));
            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code.StartsWith("DS1", StringComparison.Ordinal));
        }
        // Default interface bodies have their own later issue and must not be
        // silently accepted, whether rejected during parsing or binding.
        var defaultBody = DSharpCompiler.Compile("module Demo\ninterface A { get(): int { 42 } }", "service-invalid.ds");
        Assert.Null(defaultBody.CSharp);
        Assert.NotEmpty(defaultBody.Diagnostics);
        var unattachedDocumentation = DSharpCompiler.Compile("module Demo\ninterface A {\n /// orphan\n }", "service-invalid.ds");
        Assert.Null(unattachedDocumentation.CSharp);
        Assert.NotEmpty(unattachedDocumentation.Diagnostics);
    }

    private static async Task MultiFileDispatch()
    {
        var sources = new[]
        {
            new SourceFile("contracts.ds", "module Contracts\ninterface Price { calculate(value: int): int }"),
            new SourceFile("services.ds", """
                module Pricing
                import Contracts
                service Fixed(amount: int): Price { calculate(value) { amount } }
                service Doubled(amount: int): Price { calculate(value) { amount * 2 } }
                """),
            new SourceFile("main.ds", """
                module App
                import Contracts
                import Pricing
                fn use(value: Price): int { value.calculate(0) }
                fn answer(): int { use(Fixed(20)) + use(Doubled(11)) }
                fn qualified(): int { use(Pricing.Fixed(20)) + use(Pricing.Doubled(11)) }
                fn lexicalShadow(): int {
                    let Pricing: Price = Fixed(42)
                    Pricing.calculate(0)
                }
                """)
        };
        var compilation = DSharpCompiler.Compile(sources);
        Assert.Empty(compilation.Diagnostics);
        Assert.NotNull(compilation.CSharp);
        await WithGenerated(compilation.CSharp!, "App", type =>
        {
            Assert.Equal(42, Call<int>(type, "answer"));
            Assert.Equal(42, Call<int>(type, "qualified"));
            Assert.Equal(42, Call<int>(type, "lexicalShadow"));
            return Task.CompletedTask;
        });

        var ambiguous = DSharpCompiler.Compile(new[]
        {
            new SourceFile("a.ds", "module A\ninterface Price { get(): int }"),
            new SourceFile("b.ds", "module B\ninterface Price { get(): int }"),
            new SourceFile("app.ds", "module App\nimport A\nimport B\nfn f(value: Price): int { value.get() }")
        });
        Assert.Null(ambiguous.CSharp);
        Assert.Contains(ambiguous.Diagnostics, diagnostic => diagnostic.Code == "DS2031");

        var typeParameterShadow = DSharpCompiler.Compile(new[]
        {
            new SourceFile("lib.ds", "module Lib\ninterface I { get(): int }\nservice S: I { get() { 42 } }"),
            new SourceFile("app.ds", "module App\nimport Lib\nfn f<S>(): int { S().get() }")
        });
        Assert.Null(typeParameterShadow.CSharp);
        Assert.NotEmpty(typeParameterShadow.Diagnostics);
        Assert.All(typeParameterShadow.Diagnostics, diagnostic => Assert.Equal("app.ds", diagnostic.Path));

        const string conversionSource = "module App\nimport Lib\nimport Other\nfn f(): I { S() }";
        var mismatch = DSharpCompiler.Compile(new[]
        {
            new SourceFile("lib.ds", "module Lib\ninterface Own { get(): int }\nservice S: Own { get() { 42 } }"),
            new SourceFile("other.ds", "module Other\ninterface I { get(): int }"),
            new SourceFile("app.ds", conversionSource)
        });
        Assert.Null(mismatch.CSharp);
        var error = Assert.Single(mismatch.Diagnostics);
        Assert.Equal("app.ds", error.Path);
        Assert.Equal(conversionSource.IndexOf("S()", StringComparison.Ordinal), error.Span.Start);
        Assert.Contains("Lib.S", error.Message);
        Assert.Contains("Other.I", error.Message);
        Assert.DoesNotContain("__dsharp_module_symbol_", error.Message);

        var sameNames = DSharpCompiler.Compile(new[]
        {
            new SourceFile("one.ds", "module One\ninterface Contract { get(): int }"),
            new SourceFile("two.ds", "module Two\ninterface Contract { get(): int }"),
            new SourceFile("app.ds", "module App\nimport One\nimport Two\nfn f(value: One.Contract): Two.Contract { value }")
        });
        Assert.Null(sameNames.CSharp);
        var namesError = Assert.Single(sameNames.Diagnostics);
        Assert.Equal("app.ds", namesError.Path);
        Assert.Contains("One.Contract", namesError.Message);
        Assert.Contains("Two.Contract", namesError.Message);
        Assert.DoesNotContain("__dsharp_module_symbol_", namesError.Message);
    }

    private static async Task NominalShadows()
    {
        const string source = """
            module Demo
            interface Map { count(): int }
            service MutableList: Map { count() { 42 } }
            fn answer(): int {
                let value: Map = MutableList()
                value.count()
            }
            """;
        var compiled = DSharpCompiler.Compile(source, "service-shadow.ds");
        Assert.Empty(compiled.Diagnostics);
        Assert.NotNull(compiled.CSharp);
        await WithGenerated(compiled.CSharp!, "Demo", type =>
        {
            Assert.Equal(42, Call<int>(type, "answer"));
            return Task.CompletedTask;
        });

        const string preludeCases = """
            module Demo
            interface Amount { value(): int }
            service Some: Amount { value() { 10 } }
            service None: Amount { value() { 11 } }
            service Success: Amount { value() { 10 } }
            service Error: Amount { value() { 11 } }
            fn answer(): int {
                Some().value() + None().value() + Success().value() + Error().value()
            }
            """;
        var preludeShadow = DSharpCompiler.Compile(preludeCases, "service-prelude-shadow.ds");
        Assert.Empty(preludeShadow.Diagnostics);
        Assert.NotNull(preludeShadow.CSharp);
        await WithGenerated(preludeShadow.CSharp!, "Demo", type =>
        {
            Assert.Equal(42, Call<int>(type, "answer"));
            return Task.CompletedTask;
        });

        var modules = DSharpCompiler.Compile(new[]
        {
            new SourceFile("domain.ds", "module Domain\ninterface List { count(): int }\ninterface Task { get(): int }\nservice MutableMap: List { count() { 42 } }\nservice Current: Task { get() { 1 } }"),
            new SourceFile("app.ds", "module App\nimport Domain\nfn answer(): int { let value: List = MutableMap()\n let other: Task = Current()\n value.count() + other.get() - 1 }\nasync fn later(): int { 42 }\nasync fn asyncAnswer(): int { await later() }")
        });
        Assert.Empty(modules.Diagnostics);
        Assert.NotNull(modules.CSharp);
        await WithGenerated(modules.CSharp!, "App", async type =>
        {
            Assert.Equal(42, Call<int>(type, "answer"));
            Assert.Equal(42, await Call<Task<int>>(type, "asyncAnswer").WaitAsync(TimeSpan.FromSeconds(10)));
        });

        // A user nominal shadows an intrinsic with the same source name. It
        // must not silently resolve to an unrelated Map/Set/Mutable factory.
        var invalid = new[]
        {
            "interface Map { count(): int }\nfn f(): Unit { let value = Map.empty<int, int>()\n () }",
            "interface Set { count(): int }\nfn f(): Unit { let value = Set.empty<int>()\n () }",
            "interface MutableList { count(): int }\nfn f(): Unit { let value = MutableList<int>()\n () }",
            "interface MutableMap { count(): int }\nfn f(): Unit { let value = MutableMap<int, int>()\n () }",
            "interface A { get(): int }\nservice Map: A { get() { 1 } }\nfn f(): Unit { let value = Map.empty<int, int>()\n () }"
        };
        foreach (var body in invalid)
        {
            var result = DSharpCompiler.Compile("module Demo\n" + body, "service-shadow.ds");
            Assert.True(result.CSharp is null, $"A user nominal failed to shadow a built-in: {body}");
            Assert.NotEmpty(result.Diagnostics);
            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code.StartsWith("DS1", StringComparison.Ordinal));
        }
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
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-services-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "Generated.cs"), csharp);
            File.WriteAllText(Path.Combine(directory, "Generated.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net11.0</TargetFramework><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>");
            File.WriteAllText(Path.Combine(directory, "global.json"), "{\"sdk\":{\"version\":\"11.0.100\",\"rollForward\":\"disable\"}}");
            File.WriteAllText(Path.Combine(directory, "NuGet.Config"), "<configuration><packageSources><clear/></packageSources></configuration>");
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "build", "Generated.csproj", "--nologo", "--verbosity", "quiet" }) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated service build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("services-" + Guid.NewGuid(), isCollectible: true);
            try
            {
                var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0/Generated.dll"));
                var type = Assert.Single(assembly.GetTypes(), candidate => candidate.Name == module);
                await verify(type);
            }
            finally { context.Unload(); }
        }
        finally { Directory.Delete(directory, true); }
    }
}
