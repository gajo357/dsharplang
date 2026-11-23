using System.Diagnostics;
using System.Security;
using System.Text.RegularExpressions;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class PublicationTests
{
    private static readonly SourceFile[] Sources =
    [
        new("shared.ds", "module Shared\ntype Box<T> { value: T }"),
        new("a.ds", "module A\ntype Event { value: int }\nactor Worker { Handle(message: Event): int { 111 } }"),
        new("b.ds", "module B\ntype Event { value: int }"),
        new("app-a.ds", """
            module App
            import Shared
            import A
            async fn first(next: (int) -> Event) { await Dispatch(next(7)) }
            async fn same(next: (int) -> A.Event) { await Dispatch(next(8)) }
            async fn boxA(next: (int) -> Box<Event>) { await Dispatch(next(9)) }
            async fn target(next: (int) -> Event) { await Dispatch<Worker>(next(10)) }
            async fn unit() { await Dispatch(()) }
            """),
        new("app-b.ds", """
            module App
            import Shared
            import B
            async fn other(next: (int) -> B.Event) { await Dispatch(next(11)) }
            async fn boxB(next: (int) -> Box<B.Event>) { await Dispatch(next(12)) }
            """)
    ];

    [Fact]
    public async Task Publish()
    {
        var compiled = DSharpCompiler.Compile(Sources);
        Assert.True(compiled.CSharp is not null && compiled.Diagnostics.Count == 0,
            $"Expected publication entries: {string.Join("; ", compiled.Diagnostics)}");
        Assert.True(compiled.RequiresRuntime);
        var root = RepositoryRoot();
        var runtime = Path.Combine(root, "src/DSharp.Runtime/bin/Release/net11.0/DSharp.Runtime.dll");
        Assert.True(File.Exists(runtime), $"Built runtime ABI missing: {runtime}");
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-pub-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "Generated.cs"), compiled.CSharp!);
            await File.WriteAllTextAsync(Path.Combine(directory, "Program.cs"), HostProgram);
            var escaped = SecurityElement.Escape(runtime);
            await File.WriteAllTextAsync(Path.Combine(directory, "Generated.csproj"), $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net11.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup><ItemGroup><Reference Include=\"DSharp.Runtime\"><HintPath>{escaped}</HintPath></Reference></ItemGroup></Project>");
            await File.WriteAllTextAsync(Path.Combine(directory, "global.json"), "{\"sdk\":{\"version\":\"11.0.100\",\"rollForward\":\"disable\"}}");
            await File.WriteAllTextAsync(Path.Combine(directory, "NuGet.Config"), "<configuration><packageSources><clear/></packageSources></configuration>");
            var build = await Run(directory, null, "build", "Generated.csproj", "--configuration", "Release", "--nologo");
            Assert.True(build.ExitCode == 0, $"Publication host build failed: {build.Output}");
            var host = await Run(directory, null, Path.Combine(directory, "bin/Release/net11.0/Generated.dll"));
            Assert.True(host.ExitCode == 0, $"Publication host failed: {host.Output}");
            Assert.Contains("publication host passed", host.Output);
            CheckSafety();
            await CheckCli(root, runtime, directory);
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void CheckSafety()
    {
        const string priorNegative = "module Demo\ntype Request { value: int }\nactor Worker { Handle(message: Request): int { message.value } }\nasync fn use() { await Dispatch(Request { value = 1 }) }";
        var publication = DSharpCompiler.Compile(priorNegative, "publication-prior-negative.ds");
        Assert.True(publication.CSharp is not null && publication.Diagnostics.Count == 0 && publication.RequiresRuntime,
            $"Targetless Dispatch remains publication even with a matching actor: {string.Join("; ", publication.Diagnostics)}");
        const string declarations = "module Demo\ntype Event { value: int }\nactor Worker { Handle(message: Event): int { message.value } }\n";
        string[] invalid =
        [
            "async fn pub() { await Dispatch(1) }",
            "async fn pub() { await Dispatch(null) }",
            "async fn pub() { await Dispatch([1]) }",
            "async fn pub(value: Event?) { await Dispatch(value) }",
            "type Box<T> { value: T }\nasync fn pub(value: Box<List<int>>) { await Dispatch(value) }",
            "type Box<T> { value: T }\ntype Nested { value: Box<int?> }\nasync fn pub(value: Nested) { await Dispatch(value) }",
            "type EventTree { | End | Next(EventTree) | Bad(List<int>) }\nasync fn pub(value: EventTree) { await Dispatch(value) }",
            "type Box<T> { value: T }\ntype Expand<T> { | End | Next(Expand<Box<T>>) }\nasync fn pub(value: Expand<int>) { await Dispatch(value) }",
            "async fn pub(value: (int) -> int) { await Dispatch(value) }",
            "async fn pub(value: Task<int>) { await Dispatch(value) }",
            "async fn pub<T>(value: T) { await Dispatch(value) }",
            "async fn pub(): Unit { let pending = Dispatch(Event { value = 1 })\n () }",
            "fn pub(): Unit { let pending = Dispatch(Event { value = 1 })\n () }",
            "async fn pub(): Unit { let callback = async () => { await Dispatch(Event { value = 1 }) }\n await callback() }",
            "async fn pub(): Unit { await (async () => { await Dispatch(Event { value = 1 }) })() }",
            "actor Caller { Handle(message: Event): Unit { let ignored = await Dispatch(message)\n () } }",
            "actor Caller { async helper(message: Event): Unit { let ignored = await Dispatch(message)\n () } Handle(message: Event): int { 1 } }",
            "extend Event { async fn send(self): Unit { let ignored = await Dispatch(self)\n () } }",
            "async fn pub(): Unit { let receipt = await Dispatch(Event { value = 1 })\n () }",
            "async fn pub(): bool { (await Dispatch(Event { value = 1 })) == (await Dispatch(Event { value = 1 })) }",
            "type PublicationReceipt { value: int }\nasync fn pub(): PublicationReceipt { await Dispatch(Event { value = 1 }) }"
        ];
        foreach (var scenario in invalid)
        {
            var result = DSharpCompiler.Compile(declarations + scenario, "invalid-pub.ds");
            Assert.True(result.CSharp is null && result.Diagnostics.Any(d => d.Code == "DS2048" && d.Path == "invalid-pub.ds"),
                $"Expected invalid publication rejection: {scenario}; diagnostics: {string.Join("; ", result.Diagnostics)}");
        }
        string[] escapes =
        [
            "async fn pub() { await Dispatch(Event { value = 1 }) }\nasync fn caller(): Unit { let receipt = await pub()\n () }",
            "async fn pub(value: Event) { await Dispatch(value) }\nfn leak(): Unit { let callback = pub\n () }",
            "async fn pub(value: Event, other: int) { await Dispatch(value) }\nfn leak(): Unit { let callback = pub(Event { value = 1 })\n () }"
        ];
        foreach (var scenario in escapes)
        {
            var result = DSharpCompiler.Compile(declarations + scenario, "escaped-pub.ds");
            Assert.True(result.CSharp is null && result.Diagnostics.Any(d => d.Code == "DS2047" && d.Path == "escaped-pub.ds"),
                $"Expected publication entry escape rejection: {scenario}; diagnostics: {string.Join("; ", result.Diagnostics)}");
        }
        var unavailable = DSharpCompiler.Compile(declarations + "async fn pub(): PublicationReceipt { await Dispatch(Event { value = 1 }) }", "pub-receipt.ds");
        Assert.True(unavailable.CSharp is null && unavailable.Diagnostics.Any(d => d.Code == "DS2003" && d.Path == "pub-receipt.ds"),
            $"Publication receipt must not be source-spellable: {string.Join("; ", unavailable.Diagnostics)}");
        var embedded = DSharpCompiler.Compile("module Demo\ntype Box { value: PublicationReceipt }", "pub-field.ds");
        Assert.True(embedded.CSharp is null && embedded.Diagnostics.Any(d => d.Code == "DS2015" && d.Path == "pub-field.ds"),
            $"Publication receipt must not be embeddable in data: {string.Join("; ", embedded.Diagnostics)}");
        var ordinary = DSharpCompiler.Compile("module Demo\nfn Dispatch<T>(value: T): T { value }\nfn ordinary(): int { Dispatch<int>(7) }", "ordinary-dispatch.ds");
        Assert.Empty(ordinary.Diagnostics);
        Assert.False(ordinary.RequiresRuntime);
        var local = DSharpCompiler.Compile("module Demo\nfn ordinary(): int { let Dispatch: (int) -> int = (value: int) => value\n Dispatch(7) }", "local-dispatch.ds");
        Assert.Empty(local.Diagnostics);
        Assert.False(local.RequiresRuntime);
        var homonym = DSharpCompiler.Compile("module Demo\ntype PublicationReceipt { value: int }\nfn ordinary(): PublicationReceipt { PublicationReceipt { value = 7 } }", "receipt-homonym.ds");
        Assert.Empty(homonym.Diagnostics);
        Assert.False(homonym.RequiresRuntime);
        var field = DSharpCompiler.Compile("module Demo\ntype Event { Dispatch: int }\nasync fn pub() { await Dispatch(Event { Dispatch = 1 }) }", "dispatch-field.ds");
        Assert.True(field.CSharp is not null && field.Diagnostics.Count == 0 && field.RequiresRuntime,
            $"A message field name must not shadow publication: {string.Join("; ", field.Diagnostics)}");
        const string sameModule = "module Same\ntype Event { value: int }\nasync fn pub(value: Event) { await Dispatch(value) }";
        var fast = DSharpCompiler.Compile(sameModule, "same.ds");
        var resolved = DSharpCompiler.Compile(new[] { new SourceFile("same.ds", sameModule), new SourceFile("other.ds", "module Other\nfn value(): int { 1 }") });
        Assert.True(fast.CSharp is not null && fast.Diagnostics.Count == 0 && fast.RequiresRuntime,
            $"Expected single-file publication: {string.Join("; ", fast.Diagnostics)}");
        Assert.True(resolved.CSharp is not null && resolved.Diagnostics.Count == 0 && resolved.RequiresRuntime,
            $"Expected resolved publication: {string.Join("; ", resolved.Diagnostics)}");
        static string ContractKey(string csharp)
        {
            var match = Regex.Match(csharp, "MessageContract<[^\\r\\n]* = new\\(\"(?<key>[^\"]+)\"\\)");
            Assert.True(match.Success, "Generated message-contract descriptor was not found.");
            return match.Groups["key"].Value;
        }
        Assert.Equal("Same.Event", ContractKey(fast.CSharp!));
        Assert.Equal(ContractKey(fast.CSharp!), ContractKey(resolved.CSharp!));

        const string forgedSource = "module Demo\nfn id(value: int): int { value }";
        var syntax = new Parser(Lexer.Lex(forgedSource, "forged-pub.ds"), "forged-pub.ds").Parse();
        var function = Assert.Single(syntax.Functions);
        var parameter = Assert.Single(function.Parameters);
        var forgedParameter = parameter with { Type = parameter.Type with { Text = "$9" } };
        var forgedReturn = function.ReturnType with { Text = "$9" };
        var forged = syntax with { Functions = [function with { Parameters = [forgedParameter], ReturnType = forgedReturn }] };
        var forgedError = Assert.Throws<SourceError>(() => DSharp.Compiler.Binder.Bind(forged, "forged-pub.ds"));
        Assert.Equal("DS2048", forgedError.Diagnostic.Code);
        Assert.Equal("forged-pub.ds", forgedError.Diagnostic.Path);
        Assert.True(forgedError.Diagnostic.Span == forgedParameter.Type.Span || forgedError.Diagnostic.Span == forgedReturn.Span,
            "A forged publication receipt must fail at the forged source token.");

        foreach (var imported in new[] { false, true })
        {
            var nominal = $"module {(imported ? "Library" : "Use")}\ntype Dispatch {{ value: int }}";
            var imports = imported ? "import Library\n" : "";
            var source = $"module Use\n{imports}type Event {{ value: int }}\nasync fn pub(value: Event) {{ await Dispatch(value) }}";
            SourceFile[] sources = [new("nominal.ds", nominal), new("use.ds", source)];
            var shadowed = DSharpCompiler.Compile(sources);
            Assert.True(shadowed.CSharp is null && shadowed.Diagnostics.Any(d => d.Code == "DS2027" && d.Path == "use.ds"),
                $"Visible {(imported ? "imported" : "local")} Dispatch nominal must retain ordinary lookup: {string.Join("; ", shadowed.Diagnostics)}");
            sources[1] = new("use.ds", $"module Use\n{imports}type Event {{ value: int }}\nfn control(): int {{ 1 }}");
            Assert.Empty(DSharpCompiler.Compile(sources).Diagnostics);
        }

        SourceFile[] ambiguousDispatch =
        [
            new("a.ds", "module A\ntype Dispatch { value: int }"),
            new("b.ds", "module B\ntype Dispatch { | Mark }"),
            new("use.ds", "module Use\nimport A\nimport B\ntype Event { value: int }\nasync fn pub(value: Event) { await Dispatch(value) }")
        ];
        var dispatchAmbiguity = DSharpCompiler.Compile(ambiguousDispatch);
        Assert.True(dispatchAmbiguity.CSharp is null && dispatchAmbiguity.Diagnostics.Any(d => d.Code == "DS2031" && d.Path == "use.ds"),
            $"Expected ambiguous ordinary Dispatch names: {string.Join("; ", dispatchAmbiguity.Diagnostics)}");
        SourceFile[] hiddenDispatch =
        [
            new("hidden.ds", "module Hidden\ntype Dispatch { value: int }"),
            new("use.ds", "module Use\ntype Event { value: int }\nasync fn pub(value: Event) { await Dispatch(value) }")
        ];
        var unshadowed = DSharpCompiler.Compile(hiddenDispatch);
        Assert.True(unshadowed.CSharp is not null && unshadowed.Diagnostics.Count == 0 && unshadowed.RequiresRuntime,
            $"An unimported Dispatch type must not shadow publication: {string.Join("; ", unshadowed.Diagnostics)}");

        SourceFile[] ambiguous =
        [
            new("a.ds", "module A\ntype Event { value: int }"),
            new("b.ds", "module B\ntype Event { value: int }"),
            new("use.ds", "module Use\nimport A\nimport B\nasync fn pub(value: Event) { await Dispatch(value) }")
        ];
        var ambiguity = DSharpCompiler.Compile(ambiguous);
        Assert.True(ambiguity.CSharp is null && ambiguity.Diagnostics.Any(d => d.Code == "DS2031" && d.Path == "use.ds"),
            $"Expected ambiguous publication type rejection: {string.Join("; ", ambiguity.Diagnostics)}");
        SourceFile[] hidden =
        [
            new("hidden.ds", "module Hidden\ntype Event { value: int }"),
            new("use.ds", "module Use\nasync fn pub(value: Event) { await Dispatch(value) }")
        ];
        var hiddenMessage = DSharpCompiler.Compile(hidden);
        Assert.True(hiddenMessage.CSharp is null && hiddenMessage.Diagnostics.Any(d => d.Path == "use.ds"),
            $"Expected inaccessible publication type rejection: {string.Join("; ", hiddenMessage.Diagnostics)}");
        SourceFile[] qualifiedCapture =
        [
            new("event.ds", "module Events\ntype Event { value: int }"),
            new("entry.ds", "module Entries\nimport Events\nasync fn pub(value: Event) { await Dispatch(value) }"),
            new("caller.ds", "module Callers\nimport Entries\nfn leak(): Unit { let callback = Entries.pub\n () }")
        ];
        var capture = DSharpCompiler.Compile(qualifiedCapture);
        Assert.True(capture.CSharp is null && capture.Diagnostics.Any(d => d.Code == "DS2047" && d.Path == "caller.ds"),
            $"Expected qualified publication entry capture rejection: {string.Join("; ", capture.Diagnostics)}");
        const string callback = """
            module Demo
            type Raw { value: int }
            type Request { value: int }
            type Reply { code: int }
            async fn decode(raw: Raw) { await Dispatch(raw) }
            fn mapBinding(error: string): Reply { Reply { code = 400 } }
            fn mapValue(value: int): Reply { Reply { code = value } }
            actor Worker { trigger Http<Reply>(decode, mapBinding, mapValue) Handle(message: Request): int { message.value } }
            """;
        var forbiddenHttp = DSharpCompiler.Compile(callback, "http-pub.ds");
        Assert.True(forbiddenHttp.CSharp is null && forbiddenHttp.Diagnostics.Any(d => d.Code == "DS2046" && d.Path == "http-pub.ds" && d.Span.Start == callback.LastIndexOf("decode,", StringComparison.Ordinal)),
            $"Expected receipt-returning HTTP binder rejection: {string.Join("; ", forbiddenHttp.Diagnostics)}");
    }

    private static async Task CheckCli(string root, string runtime, string directory)
    {
        var cli = Path.Combine(root, "src/DSharp.Cli/bin/Release/net11.0/dsharp.dll");
        Assert.True(File.Exists(cli), $"Built CLI missing: {cli}");
        var source = Path.Combine(directory, "cli-pub.ds");
        await File.WriteAllTextAsync(source, "module Demo\ntype Event { value: int }\nasync fn pub(value: Event) { await Dispatch(value) }");
        var check = await Run(root, null, cli, "check", source);
        Assert.True(check.ExitCode == 0, $"CLI publication check should not need runtime ABI: {check.Output}");
        var absentDirectory = Path.Combine(directory, "missing-runtime-output");
        var absent = await Run(root, null, cli, "compile", source, absentDirectory);
        Assert.True(absent.ExitCode == 1 && absent.Output.Contains("DS0001:") && !Directory.Exists(absentDirectory),
            $"CLI must reject missing runtime ABI before output: {absent.Output}");
        var output = Path.Combine(directory, "cli-output");
        var compiled = await Run(root, runtime, cli, "compile", source, output);
        Assert.True(compiled.ExitCode == 0, $"CLI publication compile with runtime ABI failed: {compiled.Output}");
        Assert.True(File.Exists(Path.Combine(output, "DSharp.Runtime.dll")));
        Assert.Contains("DSharp.Runtime", await File.ReadAllTextAsync(Path.Combine(output, "Generated.csproj")));
    }

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "DSharp.slnx"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }

    private static async Task<(int ExitCode, string Output)> Run(string directory, string? runtime, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
        start.Environment.Remove("DSHARP_RUNTIME_ASSEMBLY");
        if (runtime is not null) start.Environment["DSHARP_RUNTIME_ASSEMBLY"] = runtime;
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Publication test process timed out."); }
        return (process.ExitCode, await stdout + await stderr);
    }

    private const string HostProgram = """
        using System.Linq.Expressions;
        using System.Reflection;
        using DSharp.Runtime;

        static class Program
        {
            private static int evaluations;
            private static object Build(Type type, int value)
            {
                var parameter = type.GetConstructors().Single().GetParameters().Single().ParameterType;
                var field = parameter == typeof(int) ? (object)value : Build(parameter, value);
                return Activator.CreateInstance(type, field)!;
            }
            private static object Message(Type type, int value)
            {
                evaluations++;
                return Build(type, value);
            }
            private static Delegate Factory(Type delegateType)
            {
                var invoke = delegateType.GetMethod("Invoke")!;
                var value = Expression.Parameter(typeof(int), "value");
                var make = typeof(Program).GetMethod(nameof(Message), BindingFlags.Static | BindingFlags.NonPublic)!;
                var call = Expression.Call(make, Expression.Constant(invoke.ReturnType), value);
                return Expression.Lambda(delegateType, Expression.Convert(call, invoke.ReturnType), value).Compile();
            }
            private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
            private static async Task<object?> Result(Task task)
            {
                await task.WaitAsync(TimeSpan.FromSeconds(10));
                return task.GetType().GetProperty("Result")!.GetValue(task);
            }
            private static async Task Main()
            {
                var app = typeof(Program).Assembly.GetTypes().Single(t => t.Name == "App");
                MethodInfo Entry(string name) => app.GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy) ?? throw new Exception("missing entry " + name);
                Task Start(string name, Fake host, Type receiptType, bool unit = false)
                {
                    var entry = Entry(name);
                    var args = entry.GetParameters();
                    Require(args.Length == (unit ? 1 : 2) && args[0].ParameterType == typeof(IActorDispatcher), "dispatcher must be explicit first argument");
                    Require(entry.ReturnType == typeof(Task<>).MakeGenericType(receiptType), "entry must expose the correct opaque receipt type");
                    var values = unit ? new object?[] { host } : new object?[] { host, Factory(args[1].ParameterType) };
                    return (Task)entry.Invoke(null, values)!;
                }
                var zero = new Fake(0);
                var one = new Fake(1);
                var many = new Fake(3);
                var zeroTask = Start("first", zero, typeof(PublicationReceipt));
                var oneTask = Start("same", one, typeof(PublicationReceipt));
                var manyTask = Start("first", many, typeof(PublicationReceipt));
                Require(evaluations == 3, "each publication message must be evaluated once");
                Require(zero.Publishes.Count == 1 && one.Publishes.Count == 1 && many.Publishes.Count == 1, "each host receives one Publish");
                Require(zero.Publishes[0].SubscriberCount == 0 && one.Publishes[0].SubscriberCount == 1 && many.Publishes[0].SubscriberCount == 3, "test-only subscriber counts");
                Require(zero.Targets.Count == 0 && one.Targets.Count == 0 && many.Targets.Count == 0, "publication never selects a target");
                Require(ReferenceEquals(zero.Publishes[0].Contract, one.Publishes[0].Contract), "same scoped type shares a message contract");
                Require((int)zero.Publishes[0].Message!.GetType().GetProperty("value")!.GetValue(zero.Publishes[0].Message)! == 7, "message forwarded unchanged");
                Require(!zeroTask.IsCompleted && !oneTask.IsCompleted && !manyTask.IsCompleted, "publication remains pending before fake acceptance");
                var oneReceipt = new PublicationReceipt();
                one.Publishes[0].Completion.SetResult(oneReceipt);
                Require(ReferenceEquals(oneReceipt, await Result(oneTask)), "publication returns the fake's exact receipt");
                Require(one.SubscriberWork.All(work => !work.Task.IsCompleted), "fake acceptance need not wait for simulated subscriber work");
                Require(!zeroTask.IsCompleted && !manyTask.IsCompleted, "host completions are isolated");
                var zeroReceipt = new PublicationReceipt();
                var manyReceipt = new PublicationReceipt();
                zero.Publishes[0].Completion.SetResult(zeroReceipt);
                many.Publishes[0].Completion.SetResult(manyReceipt);
                Require(ReferenceEquals(zeroReceipt, await Result(zeroTask)) && ReferenceEquals(manyReceipt, await Result(manyTask)), "zero and many return their own receipts");
                var other = Start("other", zero, typeof(PublicationReceipt));
                Require(!ReferenceEquals(zero.Publishes[0].Contract, zero.Publishes[1].Contract), "same-short-name message types from different modules differ");
                Require(zero.Publishes[0].Key == "A.Event" && zero.Publishes[1].Key == "B.Event", "contract keys preserve nominal module identity");
                var otherReceipt = new PublicationReceipt();
                zero.Publishes[1].Completion.SetResult(otherReceipt);
                Require(ReferenceEquals(otherReceipt, await Result(other)), "other nominal result");
                var boxA = Start("boxA", zero, typeof(PublicationReceipt));
                var boxB = Start("boxB", zero, typeof(PublicationReceipt));
                Require(zero.Publishes[2].Key == "Shared.Box<A.Event>" && zero.Publishes[3].Key == "Shared.Box<B.Event>", "generic contract keys preserve argument identity");
                Require(!ReferenceEquals(zero.Publishes[2].Contract, zero.Publishes[3].Contract), "generic message identities differ");
                zero.Publishes[2].Completion.SetResult(new PublicationReceipt());
                zero.Publishes[3].Completion.SetResult(new PublicationReceipt());
                await Result(boxA);
                await Result(boxB);
                var target = Start("target", zero, typeof(InvocationReceipt));
                Require(zero.Targets.Count == 1 && zero.Publishes.Count == 4, "targeted Dispatch is a separate operation");
                var targetedReceipt = new InvocationReceipt();
                zero.Targets[0].Completion.SetResult(targetedReceipt);
                Require(ReferenceEquals(targetedReceipt, await Result(target)), "targeted operation returns its own distinct receipt");
                Require(targetedReceipt.GetType() != oneReceipt.GetType(), "receipt types are distinct");
                var unit = Start("unit", zero, typeof(PublicationReceipt), unit: true);
                Require(zero.Publishes.Count == 5, "Unit publishes without an actor target");
                var unitReceipt = new PublicationReceipt();
                zero.Publishes[4].Completion.SetResult(unitReceipt);
                Require(ReferenceEquals(unitReceipt, await Result(unit)), "Unit publication receipt");
                Require(evaluations == 7, "all callback arguments evaluated exactly once");
                Console.WriteLine("publication host passed");
            }
        }
        sealed class Fake(int subscribers) : IActorDispatcher
        {
            public readonly List<Publication> Publishes = new();
            public readonly List<Target> Targets = new();
            public readonly TaskCompletionSource<int>[] SubscriberWork = Enumerable.Range(0, subscribers).Select(_ => new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously)).ToArray();
            public Task<TResult> Invoke<TMessage, TResult>(ActorRoute<TMessage, TResult> route, TMessage message) => throw new Exception("unexpected Invoke");
            public Task<InvocationReceipt> Dispatch<TMessage, TResult>(ActorRoute<TMessage, TResult> route, TMessage message)
            {
                var completion = new TaskCompletionSource<InvocationReceipt>(TaskCreationOptions.RunContinuationsAsynchronously);
                Targets.Add(new Target(completion));
                return completion.Task;
            }
            public Task<PublicationReceipt> Publish<TMessage>(MessageContract<TMessage> contract, TMessage message)
            {
                var completion = new TaskCompletionSource<PublicationReceipt>(TaskCreationOptions.RunContinuationsAsynchronously);
                Publishes.Add(new Publication(contract, contract.Key, message, subscribers, completion));
                return completion.Task;
            }
        }
        sealed record Publication(object Contract, string Key, object? Message, int SubscriberCount, TaskCompletionSource<PublicationReceipt> Completion);
        sealed record Target(TaskCompletionSource<InvocationReceipt> Completion);
        """;
}
