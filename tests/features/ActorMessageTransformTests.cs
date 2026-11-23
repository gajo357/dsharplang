using System.Diagnostics;
using System.Security;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class ActorMessageTransformTests
{
    private static readonly SourceFile[] Sources =
    [
        new("contracts.ds", """
            module Contracts
            type Detail { value: int }
            type Incoming { detail: Detail }
            type Outgoing {
                detail: Detail
                number: int
                text: string
                flag: bool
                empty: Unit
            }
            """),
        new("targets.ds", "module Targets\nimport Contracts\nactor Worker { Handle(message: Outgoing): int { 1000 } }"),
        new("actors.ds", """
            module Actors
            import Contracts
            import Targets
            actor Caller {
                Handle(message: Incoming): int {
                    await Invoke<Targets.Worker>(Outgoing {
                        detail = Detail { value = message.detail.value }
                        number = 42
                        text = "literal"
                        flag = true
                        empty = ()
                    })
                }
            }
            actor Plain { Handle(message: Incoming): int { message.detail.value } }
            """)
    ];

    [Fact]
    public async Task Transform()
    {
        var compiled = DSharpCompiler.Compile(Sources);
        Assert.True(compiled.CSharp is not null && compiled.Diagnostics.Count == 0 && compiled.RequiresRuntime,
            $"Expected immutable actor message transformation: {string.Join("; ", compiled.Diagnostics)}");
        var caller = Assert.Single(compiled.Actors, actor => actor.Name == "Actors.Caller");
        var plain = Assert.Single(compiled.Actors, actor => actor.Name == "Actors.Plain");
        var target = Assert.Single(compiled.Actors, actor => actor.Name == "Targets.Worker");
        Assert.Equal("Contracts.Incoming", caller.InputType);
        Assert.Equal("Contracts.Outgoing", target.InputType);
        Assert.True(caller.RequiresDispatcher);
        Assert.False(plain.RequiresDispatcher);
        Assert.False(target.RequiresDispatcher);
        Assert.Empty(caller.Dependencies);
        var root = RepositoryRoot();
        var runtime = Path.Combine(root, "src/DSharp.Runtime/bin/Release/net11.0/DSharp.Runtime.dll");
        Assert.True(File.Exists(runtime), $"Built runtime ABI missing: {runtime}");
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-actor-transform-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "Generated.cs"), compiled.CSharp!);
            var hostSource = HostProgram.Replace("CALLER_METHOD", caller.ImplementationName, StringComparison.Ordinal)
                .Replace("TARGET_METHOD", target.ImplementationName, StringComparison.Ordinal)
                .Replace("PLAIN_METHOD", plain.ImplementationName, StringComparison.Ordinal);
            await File.WriteAllTextAsync(Path.Combine(directory, "Program.cs"), hostSource);
            var escaped = SecurityElement.Escape(runtime);
            await File.WriteAllTextAsync(Path.Combine(directory, "Generated.csproj"), $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net11.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup><ItemGroup><Reference Include=\"DSharp.Runtime\"><HintPath>{escaped}</HintPath></Reference></ItemGroup></Project>");
            await File.WriteAllTextAsync(Path.Combine(directory, "global.json"), "{\"sdk\":{\"version\":\"11.0.100\",\"rollForward\":\"disable\"}}");
            await File.WriteAllTextAsync(Path.Combine(directory, "NuGet.Config"), "<configuration><packageSources><clear/></packageSources></configuration>");
            var build = await Run(directory, null, "build", "Generated.csproj", "--configuration", "Release", "--nologo");
            Assert.True(build.ExitCode == 0, $"Transformed actor host build failed: {build.Output}");
            var host = await Run(directory, null, Path.Combine(directory, "bin/Release/net11.0/Generated.dll"));
            Assert.True(host.ExitCode == 0, $"Transformed actor host failed: {host.Output}");
            Assert.Contains("actor transform passed", host.Output);
            CheckRejectedForms();
            await CheckCli(root, runtime, directory);
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void CheckRejectedForms()
    {
        const string common = """
            module Demo
            type Input { value: int }
            type Output { value: int }
            resource Reader { read(): int { 1 } release(): Unit { () } }
            actor Worker { Handle(message: Output): int { message.value } }
            fn identity(value: int): int { value }
            fn make(message: Input): Output { Output { value = message.value } }
            """;
        string[] invalid =
        [
            "actor Caller { Handle(message: Input): int { await Invoke<Missing>(Output { value = message.value }) } }",
            "actor Caller { Handle(message: Input): int { await Invoke<Worker>(message) } }",
            "actor Caller { Handle(message: Input): int { await Invoke<Worker>(make(message)) } }",
            "actor Caller { Handle(message: Input): int { await Invoke<Worker>(Output { value = identity(message.value) }) } }",
            "actor Caller { Handle(message: Input): int { await Invoke<Worker>(Output { value = message.value + 1 }) } }",
            "actor Caller { Handle(message: Input): int { await Invoke<Worker>(Output { value = -1 }) } }",
            "actor Caller { Handle(message: Input): int { await Invoke<Worker>(Output { value = ((value: int) => value)(message.value) }) } }",
            "actor Caller { Handle(message: Input): int { let saved = message\n await Invoke<Worker>(Output { value = saved.value }) } }",
            "interface Store { read(): int }\nactor Caller(store: Store) { Handle(message: Input): int { await Invoke<Worker>(Output { value = message.value }) } }",
            "actor Caller { Handle(message: Input): int { using reader = Reader() { await Invoke<Worker>(Output { value = reader.read() }) } } }",
            "actor Caller { async helper(message: Input): int { await Invoke<Worker>(Output { value = message.value }) } Handle(message: Input): int { 1 } }",
            "actor Caller { Handle(message: Input): int { let callback = async () => { await Invoke<Worker>(Output { value = message.value }) }\n await callback() } }",
            "actor Caller { Handle(message: Input): int { Invoke<Worker>(Output { value = message.value }) } }"
        ];
        foreach (var scenario in invalid)
        {
            var result = DSharpCompiler.Compile(common + "\n" + scenario, "invalid-transform.ds");
            Assert.True(result.CSharp is null && result.Diagnostics.Any(d => d.Code == "DS2047" && d.Path == "invalid-transform.ds"),
                $"Expected bounded transformation rejection: {scenario}; diagnostics: {string.Join("; ", result.Diagnostics)}");
        }
        var direct = DSharpCompiler.Compile(common + "\nactor Caller { Handle(message: Input): int { await Invoke<Worker>(Output { value = message.value }) } }\nfn bad(): Unit { Caller() }", "transform-bypass.ds");
        Assert.True(direct.CSharp is null && direct.Diagnostics.Any(d => d.Path == "transform-bypass.ds"),
            $"Expected direct actor construction rejection: {string.Join("; ", direct.Diagnostics)}");
    }

    private static async Task CheckCli(string root, string runtime, string directory)
    {
        var cli = Path.Combine(root, "src/DSharp.Cli/bin/Release/net11.0/dsharp.dll");
        Assert.True(File.Exists(cli), $"Built CLI missing: {cli}");
        var paths = new List<string>();
        foreach (var file in Sources)
        {
            var path = Path.Combine(directory, file.Path);
            await File.WriteAllTextAsync(path, file.Text);
            paths.Add(path);
        }
        var check = await Run(root, null, new[] { cli, "check" }.Concat(paths).ToArray());
        Assert.True(check.ExitCode == 0, $"CLI check should not require runtime ABI: {check.Output}");
        var missingOutput = Path.Combine(directory, "missing-runtime-output");
        var missing = await Run(root, null, new[] { cli, "compile" }.Concat(paths).Append(missingOutput).ToArray());
        Assert.True(missing.ExitCode == 1 && missing.Output.Contains("DS0001:") && !Directory.Exists(missingOutput),
            $"CLI must reject missing runtime ABI before output: {missing.Output}");
        var output = Path.Combine(directory, "cli-output");
        var success = await Run(root, runtime, new[] { cli, "compile" }.Concat(paths).Append(output).ToArray());
        Assert.True(success.ExitCode == 0, $"Transformed actor CLI compile failed: {success.Output}");
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
        catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Transformed actor test timed out."); }
        return (process.ExitCode, await stdout + await stderr);
    }

    private const string HostProgram = """
        using System.Reflection;
        using DSharp.Runtime;

        static class Program
        {
            private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
            private static object Input(Type inputType, int value)
            {
                var detailType = inputType.GetConstructors().Single().GetParameters().Single().ParameterType;
                var detail = Activator.CreateInstance(detailType, value)!;
                return Activator.CreateInstance(inputType, detail)!;
            }
            private static async Task Main()
            {
                var methods = typeof(Program).Assembly.GetTypes().SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)).ToArray();
                var caller = methods.Single(method => method.Name == "CALLER_METHOD");
                var target = methods.Single(method => method.Name == "TARGET_METHOD");
                var plain = methods.Single(method => method.Name == "PLAIN_METHOD");
                Require(!caller.IsPublic && !target.IsPublic && !plain.IsPublic, "actor bodies must remain internal");
                var args = caller.GetParameters();
                Require(args.Length == 2 && args[0].ParameterType == typeof(IActorDispatcher), "effectful Handle receives the scoped dispatcher");
                Require(plain.GetParameters().Length == 1 && plain.GetParameters()[0].ParameterType == args[1].ParameterType, "plain Handle signature is unchanged");
                var targetInput = target.GetParameters().Single().ParameterType;
                Require(targetInput != args[1].ParameterType, "caller and target use distinct nominal message types");
                var firstMessage = Input(args[1].ParameterType, 7);
                var secondMessage = Input(args[1].ParameterType, 8);
                var firstHost = new Fake();
                var secondHost = new Fake();
                var first = (Task<int>)caller.Invoke(null, new[] { (object)firstHost, firstMessage })!;
                var second = (Task<int>)caller.Invoke(null, new[] { (object)secondHost, secondMessage })!;
                Require(firstHost.Calls.Count == 1 && secondHost.Calls.Count == 1, "each Handle invokes exactly once");
                Require(ReferenceEquals(firstHost.Calls[0].Route, secondHost.Calls[0].Route), "hosts use the same target route");
                Require((string)firstHost.Calls[0].Route.GetType().GetProperty("Key")!.GetValue(firstHost.Calls[0].Route)! == "Targets.Worker", "route identifies the qualified actor");
                foreach (var (call, original, expected) in new[] { (firstHost.Calls[0], firstMessage, 7), (secondHost.Calls[0], secondMessage, 8) })
                {
                    Require(call.Message is not null && call.Message.GetType() == targetInput && !ReferenceEquals(call.Message, original), "a distinct correctly typed record is sent");
                    var detail = call.Message!.GetType().GetProperty("detail")!.GetValue(call.Message)!;
                    Require((int)detail.GetType().GetProperty("value")!.GetValue(detail)! == expected, "nested input field is preserved");
                    Require((int)call.Message.GetType().GetProperty("number")!.GetValue(call.Message)! == 42, "integer literal preserved");
                    Require((string)call.Message.GetType().GetProperty("text")!.GetValue(call.Message)! == "literal", "text literal preserved");
                    Require((bool)call.Message.GetType().GetProperty("flag")!.GetValue(call.Message)!, "bool literal preserved");
                    Require(call.Message.GetType().GetProperty("empty")!.GetValue(call.Message) is not null, "Unit literal preserved");
                }
                Require(!first.IsCompleted && !second.IsCompleted, "host results remain independently pending");
                var fault = new InvalidOperationException("host fault");
                secondHost.Calls[0].Completion.SetException(fault);
                try { await second.WaitAsync(TimeSpan.FromSeconds(10)); throw new Exception("expected fault"); }
                catch (InvalidOperationException observed) { Require(ReferenceEquals(fault, observed), "original fault identity preserved"); }
                Require(!first.IsCompleted, "one host completion cannot complete the other");
                firstHost.Calls[0].Completion.SetResult(42);
                Require(await first.WaitAsync(TimeSpan.FromSeconds(10)) == 42, "Invoke result preserved");
                var plainTask = (Task<int>)plain.Invoke(null, new[] { firstMessage })!;
                Require(await plainTask.WaitAsync(TimeSpan.FromSeconds(10)) == 7, "unaffected Handle keeps its original behavior");
                Console.WriteLine("actor transform passed");
            }
        }
        sealed class Fake : IActorDispatcher
        {
            public readonly List<Call> Calls = new();
            public Task<TResult> Invoke<TMessage, TResult>(ActorRoute<TMessage, TResult> route, TMessage message)
            {
                if (typeof(TResult) != typeof(int)) throw new Exception("unexpected result type");
                var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                Calls.Add(new Call(route, message, completion));
                return (Task<TResult>)(object)completion.Task;
            }
            public Task<InvocationReceipt> Dispatch<TMessage, TResult>(ActorRoute<TMessage, TResult> route, TMessage message) => throw new Exception("unexpected Dispatch");
            public Task<PublicationReceipt> Publish<TMessage>(MessageContract<TMessage> contract, TMessage message) => throw new Exception("unexpected Publish");
        }
        sealed record Call(object Route, object? Message, TaskCompletionSource<int> Completion);
        """;
}
