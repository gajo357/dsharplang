using System.Diagnostics;
using System.Security;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class ReceiptRelayTests
{
    private static readonly SourceFile[] Sources =
    [
        new("contracts.ds", "module Contracts\ntype Event { value: int }"),
        new("workers.ds", """
            module Workers
            import Contracts
            actor Worker { Handle(message: Event): int { 999 } }
            actor Pulse { Handle(message: Unit): int { 1 } }
            async fn target(message: Event) { await Dispatch<Worker>(message) }
            async fn publish(message: Event) { await Dispatch(message) }
            async fn unitLeaf(message: Unit) { await Dispatch<Pulse>(message) }
            """),
        new("bridges.ds", """
            module Bridges
            import Contracts
            import Workers
            async fn targeted(message: Event) { await Workers.target(message) }
            async fn published(message: Event) { await Workers.publish(message) }
            async fn unitRelay() { await Workers.unitLeaf(()) }
            """),
        new("app.ds", """
            module App
            import Contracts
            import Bridges
            async fn targetOne(message: Event) { await Bridges.targeted(message) }
            async fn targetTwo(message: Event) { await targetOne(message) }
            async fn publishOne(message: Event) { await Bridges.published(message) }
            async fn publishTwo(message: Event) { await publishOne(message) }
            async fn unit() { await Bridges.unitRelay() }
            fn plain(value: int): int { value + 1 }
            """)
    ];

    [Fact]
    public async Task Relay()
    {
        var compiled = DSharpCompiler.Compile(Sources);
        Assert.True(compiled.CSharp is not null && compiled.Diagnostics.Count == 0 && compiled.RequiresRuntime,
            $"Expected scoped receipt relay chains: {string.Join("; ", compiled.Diagnostics)}");
        var root = RepositoryRoot();
        var runtime = Path.Combine(root, "src/DSharp.Runtime/bin/Release/net11.0/DSharp.Runtime.dll");
        Assert.True(File.Exists(runtime), $"Built runtime ABI missing: {runtime}");
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-receipt-relay-" + Guid.NewGuid().ToString("N"));
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
            Assert.True(build.ExitCode == 0, $"Receipt relay host build failed: {build.Output}");
            var host = await Run(directory, null, Path.Combine(directory, "bin/Release/net11.0/Generated.dll"));
            Assert.True(host.ExitCode == 0, $"Receipt relay host failed: {host.Output}");
            Assert.Contains("receipt relay passed", host.Output);
            CheckRejections();
            await CheckCli(root, runtime, directory);
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void CheckRejections()
    {
        const string common = """
            module Demo
            type Event { value: int }
            actor Worker { Handle(message: Event): int { message.value } }
            async fn target(message: Event) { await Dispatch<Worker>(message) }
            async fn publish(message: Event) { await Dispatch(message) }
            """;
        string[] unsafeCases =
        [
            "async fn bad(message: Event): Unit { let receipt = await target(message)\n () }",
            "async fn bad(message: Event): Unit { let receipt = await publish(message)\n () }",
            "fn bad(): Unit { let callback = target\n () }",
            "fn bad(): Unit { let callback = publish\n () }",
            "async fn bad(message: Event): Unit { let pending = target(message)\n () }",
            "async fn bad(message: Event): Unit { let pending = publish(message)\n () }",
            "async fn bad(message: Event): Unit { let callback = async () => { await target(message) }\n let ignored = await callback()\n () }",
            "actor Caller { Handle(message: Event): Unit { let receipt = await target(message)\n () } }",
            "actor Caller { async helper(message: Event): Unit { let receipt = await publish(message)\n () } Handle(message: Event): int { 1 } }",
            "extend Event { async fn send(self): Unit { let receipt = await target(self)\n () } }"
        ];
        foreach (var scenario in unsafeCases)
        {
            var result = DSharpCompiler.Compile(common + "\n" + scenario, "unsafe-relay.ds");
            Assert.True(result.CSharp is null && result.Diagnostics.Any(d => d.Code == "DS2047" && d.Path == "unsafe-relay.ds"),
                $"Expected receipt escape rejection: {scenario}; diagnostics: {string.Join("; ", result.Diagnostics)}");
        }
        string[] cannotInfer =
        [
            "async fn first(message: Event) { await second(message) }\nasync fn second(message: Event) { await first(message) }",
            "async fn ordinary(value: int): int { value }\nasync fn bad(value: int) { await ordinary(value) }",
            "async fn bad(message: Event) { let saved = message\n await target(saved) }",
            "async fn bad(message: Event) { await target(Event { value = message.value }) }",
            "async fn generic<T>(message: Event) { await Dispatch<Worker>(message) }\nasync fn bad(message: Event) { await generic<int>(message) }",
            "async fn bad(target: (Event) -> Task<int>, message: Event) { await target(message) }"
        ];
        foreach (var scenario in cannotInfer)
        {
            var result = DSharpCompiler.Compile(common + "\n" + scenario, "nonrelay.ds");
            Assert.True(result.CSharp is null && result.Diagnostics.Any(d => d.Code == "DS2027" && d.Path == "nonrelay.ds"),
                $"Expected unsupported inferred relay rejection: {scenario}; diagnostics: {string.Join("; ", result.Diagnostics)}");
        }
        var capability = DSharpCompiler.Compile(common + "\ninterface Store { read(): int }\nasync fn bad(message: Event, store: Store) { await target(message) }", "capability-relay.ds");
        Assert.True(capability.CSharp is null && capability.Diagnostics.Any(d => d.Code == "DS2041" && d.Path == "capability-relay.ds"),
            $"Expected capability lifetime rejection for receipt relay: {string.Join("; ", capability.Diagnostics)}");
        var ordinaryShadow = DSharpCompiler.Compile("module Demo\nasync fn Dispatch(value: int): int { value }\nasync fn ordinary(value: int): int { await Dispatch(value) }", "shadow-relay.ds");
        Assert.True(ordinaryShadow.CSharp is not null && ordinaryShadow.Diagnostics.Count == 0 && !ordinaryShadow.RequiresRuntime,
            $"Ordinary Dispatch lookup must remain unchanged: {string.Join("; ", ordinaryShadow.Diagnostics)}");
        var inferredShadow = DSharpCompiler.Compile("module Demo\nasync fn Dispatch(value: int): int { value }\nasync fn ordinary(value: int) { await Dispatch(value) }", "shadow-inferred.ds");
        Assert.True(inferredShadow.CSharp is null && inferredShadow.Diagnostics.Any(d => d.Code == "DS2027" && d.Path == "shadow-inferred.ds"),
            $"Ordinary inferred async return must remain unsupported: {string.Join("; ", inferredShadow.Diagnostics)}");

        SourceFile[] ambiguous =
        [
            new("a.ds", "module A\nasync fn target(value: int): int { value }"),
            new("b.ds", "module B\nasync fn target(value: int): int { value }"),
            new("use.ds", "module Use\nimport A\nimport B\nasync fn bad(value: int) { await target(value) }")
        ];
        var ambiguity = DSharpCompiler.Compile(ambiguous);
        Assert.True(ambiguity.CSharp is null && ambiguity.Diagnostics.Any(d => d.Code == "DS2031" && d.Path == "use.ds"),
            $"Expected ambiguous ordinary callee rejection: {string.Join("; ", ambiguity.Diagnostics)}");
        const string http = """
            module Demo
            type Raw { value: int }
            type Request { value: int }
            type Reply { code: int }
            async fn publish(raw: Raw) { await Dispatch(raw) }
            async fn relay(raw: Raw) { await publish(raw) }
            fn mapBinding(error: string): Reply { Reply { code = 400 } }
            fn mapValue(value: int): Reply { Reply { code = value } }
            actor Worker { trigger Http<Reply>(relay, mapBinding, mapValue) Handle(message: Request): int { message.value } }
            """;
        var callback = DSharpCompiler.Compile(http, "relay-http.ds");
        Assert.True(callback.CSharp is null && callback.Diagnostics.Any(d => d.Code == "DS2046" && d.Path == "relay-http.ds" && d.Span.Start == http.LastIndexOf("relay,", StringComparison.Ordinal)),
            $"Expected receipt-returning HTTP binder rejection: {string.Join("; ", callback.Diagnostics)}");
    }

    private static async Task CheckCli(string root, string runtime, string directory)
    {
        var cli = Path.Combine(root, "src/DSharp.Cli/bin/Release/net11.0/dsharp.dll");
        Assert.True(File.Exists(cli), $"Built CLI missing: {cli}");
        var files = new List<string>();
        foreach (var source in Sources)
        {
            var path = Path.Combine(directory, source.Path);
            await File.WriteAllTextAsync(path, source.Text);
            files.Add(path);
        }
        var check = await Run(root, null, new[] { cli, "check" }.Concat(files).ToArray());
        Assert.True(check.ExitCode == 0, $"CLI check should not require runtime ABI: {check.Output}");
        var missingOutput = Path.Combine(directory, "missing-runtime-output");
        var missing = await Run(root, null, new[] { cli, "compile" }.Concat(files).Append(missingOutput).ToArray());
        Assert.True(missing.ExitCode == 1 && missing.Output.Contains("DS0001:") && !Directory.Exists(missingOutput),
            $"CLI must reject missing runtime ABI before output: {missing.Output}");
        var output = Path.Combine(directory, "cli-output");
        var success = await Run(root, runtime, new[] { cli, "compile" }.Concat(files).Append(output).ToArray());
        Assert.True(success.ExitCode == 0, $"Receipt relay CLI compile failed: {success.Output}");
        Assert.True(File.Exists(Path.Combine(output, "DSharp.Runtime.dll")));
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
        catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Receipt relay test process timed out."); }
        return (process.ExitCode, await stdout + await stderr);
    }

    private const string HostProgram = """
        using System.Reflection;
        using DSharp.Runtime;

        static class Program
        {
            private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
            private static async Task<object?> Result(Task task)
            {
                await task.WaitAsync(TimeSpan.FromSeconds(10));
                return task.GetType().GetProperty("Result")!.GetValue(task);
            }
            private static async Task Main()
            {
                var app = typeof(Program).Assembly.GetTypes().Single(type => type.Name == "App");
                MethodInfo Entry(string name) => app.GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy) ?? throw new Exception("missing entry " + name);
                (Task Task, object Message) Start(string name, Fake host, int value, Type receiptType)
                {
                    var entry = Entry(name);
                    var parameters = entry.GetParameters();
                    Require(parameters.Length == 2 && parameters[0].ParameterType == typeof(IActorDispatcher), "dispatcher must be explicit first argument");
                    Require(entry.ReturnType == typeof(Task<>).MakeGenericType(receiptType), "relay must retain the exact receipt type");
                    var message = Activator.CreateInstance(parameters[1].ParameterType, value)!;
                    return ((Task)entry.Invoke(null, new[] { (object)host, message })!, message);
                }
                var first = new Fake();
                var second = new Fake();
                var targetA = Start("targetOne", first, 7, typeof(InvocationReceipt));
                var targetB = Start("targetTwo", second, 8, typeof(InvocationReceipt));
                var pubA = Start("publishOne", first, 9, typeof(PublicationReceipt));
                var pubB = Start("publishTwo", second, 10, typeof(PublicationReceipt));
                Require(first.Targets.Count == 1 && second.Targets.Count == 1 && first.Publications.Count == 1 && second.Publications.Count == 1, "each relay performs one matching operation");
                Require(ReferenceEquals(first.Targets[0].Route, second.Targets[0].Route), "targeted relays share route identity");
                Require((string)first.Targets[0].Route.GetType().GetProperty("Key")!.GetValue(first.Targets[0].Route)! == "Workers.Worker", "target route identifies actor");
                Require(ReferenceEquals(first.Publications[0].Contract, second.Publications[0].Contract), "publication relays share message contract identity");
                Require((string)first.Publications[0].Contract.GetType().GetProperty("Key")!.GetValue(first.Publications[0].Contract)! == "Contracts.Event", "publication contract identifies message");
                Require(ReferenceEquals(targetA.Message, first.Targets[0].Message) && ReferenceEquals(targetB.Message, second.Targets[0].Message) && ReferenceEquals(pubA.Message, first.Publications[0].Message) && ReferenceEquals(pubB.Message, second.Publications[0].Message), "each input must pass through each relay unchanged and exactly once");
                Require(!targetA.Task.IsCompleted && !targetB.Task.IsCompleted && !pubA.Task.IsCompleted && !pubB.Task.IsCompleted, "all fake acceptance tasks start pending");
                var receiptTargetB = new InvocationReceipt();
                second.Targets[0].Completion.SetResult(receiptTargetB);
                Require(ReferenceEquals(receiptTargetB, await Result(targetB.Task)), "targeted receipt reference must be preserved");
                Require(!targetA.Task.IsCompleted && !pubA.Task.IsCompleted && !pubB.Task.IsCompleted, "completing one host does not complete other calls");
                var receiptPubA = new PublicationReceipt();
                first.Publications[0].Completion.SetResult(receiptPubA);
                Require(ReferenceEquals(receiptPubA, await Result(pubA.Task)), "publication receipt reference must be preserved");
                Require(!targetA.Task.IsCompleted && !pubB.Task.IsCompleted, "publication completion remains independent");
                var receiptTargetA = new InvocationReceipt();
                var receiptPubB = new PublicationReceipt();
                first.Targets[0].Completion.SetResult(receiptTargetA);
                second.Publications[0].Completion.SetResult(receiptPubB);
                Require(ReferenceEquals(receiptTargetA, await Result(targetA.Task)) && ReferenceEquals(receiptPubB, await Result(pubB.Task)), "each relay receives its own exact receipt");
                Require(receiptTargetA.GetType() != receiptPubA.GetType(), "receipt categories remain distinct");
                var faultCall = Start("targetTwo", first, 11, typeof(InvocationReceipt));
                var fault = new InvalidOperationException("fake acceptance failed");
                first.Targets[1].Completion.SetException(fault);
                try { await Result(faultCall.Task); throw new Exception("expected fault"); }
                catch (InvalidOperationException observed) { Require(ReferenceEquals(fault, observed), "original fault must propagate through every relay"); }
                var unit = Entry("unit");
                Require(unit.GetParameters().Length == 1 && unit.GetParameters()[0].ParameterType == typeof(IActorDispatcher) && unit.ReturnType == typeof(Task<InvocationReceipt>), "Unit relay keeps explicit dispatcher and receipt");
                var unitTask = (Task)unit.Invoke(null, new object[] { first })!;
                Require(first.Targets.Count == 3 && !unitTask.IsCompleted, "Unit relay reaches fake acceptance once and stays pending");
                var unitReceipt = new InvocationReceipt();
                first.Targets[2].Completion.SetResult(unitReceipt);
                Require(ReferenceEquals(unitReceipt, await Result(unitTask)), "Unit relay preserves exact receipt");
                var plain = Entry("plain");
                Require(plain.GetParameters().Length == 1 && (int)plain.Invoke(null, new object[] { 41 })! == 42, "unaffected entry ABI remains unchanged");
                Console.WriteLine("receipt relay passed");
            }
        }
        sealed class Fake : IActorDispatcher
        {
            public readonly List<TargetCall> Targets = new();
            public readonly List<PublicationCall> Publications = new();
            public Task<TResult> Invoke<TMessage, TResult>(ActorRoute<TMessage, TResult> route, TMessage message) => throw new Exception("unexpected Invoke");
            public Task<InvocationReceipt> Dispatch<TMessage, TResult>(ActorRoute<TMessage, TResult> route, TMessage message)
            {
                var completion = new TaskCompletionSource<InvocationReceipt>(TaskCreationOptions.RunContinuationsAsynchronously);
                Targets.Add(new TargetCall(route, message, completion));
                return completion.Task;
            }
            public Task<PublicationReceipt> Publish<TMessage>(MessageContract<TMessage> contract, TMessage message)
            {
                var completion = new TaskCompletionSource<PublicationReceipt>(TaskCreationOptions.RunContinuationsAsynchronously);
                Publications.Add(new PublicationCall(contract, message, completion));
                return completion.Task;
            }
        }
        sealed record TargetCall(object Route, object? Message, TaskCompletionSource<InvocationReceipt> Completion);
        sealed record PublicationCall(object Contract, object? Message, TaskCompletionSource<PublicationReceipt> Completion);
        """;
}
