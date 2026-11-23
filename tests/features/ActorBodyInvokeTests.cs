using System.Diagnostics;
using System.Security;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class ActorBodyInvokeTests
{
    private static readonly SourceFile[] Sources =
    [
        new("contracts.ds", "module Contracts\ntype Request { value: int }"),
        new("targets.ds", "module Targets\nimport Contracts\nactor Worker { Handle(message: Request): int { 1000 } }"),
        new("actors.ds", """
            module Actors
            import Contracts
            import Targets
            actor Caller { Handle(message: Request): int { await Invoke<Targets.Worker>(message) } }
            actor Plain { Handle(message: Request): int { message.value } }
            """)
    ];

    [Fact]
    public async Task ScopedActorBody()
    {
        var compiled = DSharpCompiler.Compile(Sources);
        Assert.True(compiled.CSharp is not null && compiled.Diagnostics.Count == 0 && compiled.RequiresRuntime,
            $"Expected resource-free actor Handle Invoke: {string.Join("; ", compiled.Diagnostics)}");
        var caller = Assert.Single(compiled.Actors, actor => actor.Name == "Actors.Caller");
        var plain = Assert.Single(compiled.Actors, actor => actor.Name == "Actors.Plain");
        Assert.Equal("Contracts.Request", caller.InputType);
        Assert.Equal("int", caller.ResultType);
        Assert.Empty(caller.Dependencies);
        Assert.True(caller.RequiresDispatcher);
        Assert.False(plain.RequiresDispatcher);
        var root = RepositoryRoot();
        var runtime = Path.Combine(root, "src/DSharp.Runtime/bin/Release/net11.0/DSharp.Runtime.dll");
        Assert.True(File.Exists(runtime), $"Built runtime ABI missing: {runtime}");
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-body-invoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "Generated.cs"), compiled.CSharp!);
            var host = HostProgram.Replace("CALLER_METHOD", caller.ImplementationName, StringComparison.Ordinal)
                .Replace("PLAIN_METHOD", plain.ImplementationName, StringComparison.Ordinal);
            await File.WriteAllTextAsync(Path.Combine(directory, "Program.cs"), host);
            var escaped = SecurityElement.Escape(runtime);
            await File.WriteAllTextAsync(Path.Combine(directory, "Generated.csproj"), $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net11.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup><ItemGroup><Reference Include=\"DSharp.Runtime\"><HintPath>{escaped}</HintPath></Reference></ItemGroup></Project>");
            await File.WriteAllTextAsync(Path.Combine(directory, "global.json"), "{\"sdk\":{\"version\":\"11.0.100\",\"rollForward\":\"disable\"}}");
            await File.WriteAllTextAsync(Path.Combine(directory, "NuGet.Config"), "<configuration><packageSources><clear/></packageSources></configuration>");
            var build = await Run(directory, null, "build", "Generated.csproj", "--configuration", "Release", "--nologo");
            Assert.True(build.ExitCode == 0, $"Actor body host build failed: {build.Output}");
            var executed = await Run(directory, null, Path.Combine(directory, "bin/Release/net11.0/Generated.dll"));
            Assert.True(executed.ExitCode == 0, $"Actor body host failed: {executed.Output}");
            Assert.Contains("actor body passed", executed.Output);
            CheckRejectedForms();
            await CheckCli(root, runtime, directory);
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void CheckRejectedForms()
    {
        const string common = "module Demo\ntype Request { value: int }\nactor Worker { Handle(message: Request): int { message.value } }\n";
        string[] invalid =
        [
            "actor Caller { Handle(message: Request): int { await Invoke<Missing>(message) } }",
            "actor Caller { Handle(message: Request): int { await Invoke<Worker>(1) } }",
            "interface Store { read(): int }\nactor Caller(store: Store) { Handle(message: Request): int { await Invoke<Worker>(message) } }",
            "resource Reader { read(): int { 1 } release(): Unit { () } }\nactor Caller { Handle(message: Request): int { using reader = Reader() { await Invoke<Worker>(message) } } }",
            "actor Caller { async helper(message: Request): int { await Invoke<Worker>(message) } Handle(message: Request): int { 1 } }",
            "actor Caller { Handle(message: Request): int { let callback = async () => { await Invoke<Worker>(message) }\n await callback() } }",
            "actor Caller { Handle(message: Request): int { Invoke<Worker>(message) } }",
            "actor Caller { Handle(message: Request): int { let pending = Invoke<Worker>(message)\n 1 } }"
        ];
        foreach (var scenario in invalid)
        {
            var result = DSharpCompiler.Compile(common + scenario, "invalid-actor-invoke.ds");
            Assert.True(result.CSharp is null && result.Diagnostics.Any(d => d.Code == "DS2047" && d.Path == "invalid-actor-invoke.ds"),
                $"Expected bounded Handle Invoke rejection: {scenario}; diagnostics: {string.Join("; ", result.Diagnostics)}");
        }
        var transformed = DSharpCompiler.Compile(common + "actor Caller { Handle(message: Request): int { await Invoke<Worker>(Request { value = message.value }) } }", "transformed-actor-invoke.ds");
        Assert.True(transformed.CSharp is not null && transformed.Diagnostics.Count == 0 && transformed.RequiresRuntime,
            $"Expected immutable record transformation in Handle Invoke: {string.Join("; ", transformed.Diagnostics)}");
        var localAlias = DSharpCompiler.Compile(common + "actor Caller { Handle(message: Request): int { let saved = message\n await Invoke<Worker>(saved) } }", "actor-local-alias.ds");
        Assert.True(localAlias.CSharp is not null && localAlias.Diagnostics.Count == 0 && localAlias.RequiresRuntime,
            $"Expected pure actor input alias before Handle Invoke: {string.Join("; ", localAlias.Diagnostics)}");
        foreach (var bypass in new[]
        {
            "fn bad(): Unit { Caller() }",
            "fn bad(): int { Caller().Handle(Request { value = 1 }) }",
            "fn bad(): int { let callback = Caller().Handle\n 1 }"
        })
        {
            var result = DSharpCompiler.Compile(common + "actor Caller { Handle(message: Request): int { await Invoke<Worker>(message) } }\n" + bypass, "bypass-actor-invoke.ds");
            Assert.True(result.CSharp is null && result.Diagnostics.Any(d => d.Path == "bypass-actor-invoke.ds"),
                $"Expected actor bypass rejection: {bypass}; diagnostics: {string.Join("; ", result.Diagnostics)}");
        }
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
        Assert.True(success.ExitCode == 0, $"Actor body CLI compile failed: {success.Output}");
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
        catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Actor body test process timed out."); }
        return (process.ExitCode, await stdout + await stderr);
    }

    private const string HostProgram = """
        using System.Reflection;
        using DSharp.Runtime;

        static class Program
        {
            private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
            private static async Task Main()
            {
                var methods = typeof(Program).Assembly.GetTypes().SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)).ToArray();
                var caller = methods.Single(method => method.Name == "CALLER_METHOD");
                var plain = methods.Single(method => method.Name == "PLAIN_METHOD");
                Require(!caller.IsPublic && !plain.IsPublic, "actor bodies must remain internal");
                var args = caller.GetParameters();
                Require(args.Length == 2 && args[0].ParameterType == typeof(IActorDispatcher), "effectful Handle receives the host dispatcher and message");
                Require(plain.GetParameters().Length == 1 && plain.GetParameters()[0].ParameterType == args[1].ParameterType, "ordinary Handle signature must stay unchanged");
                var firstMessage = Activator.CreateInstance(args[1].ParameterType, 7)!;
                var secondMessage = Activator.CreateInstance(args[1].ParameterType, 8)!;
                var firstHost = new Fake();
                var secondHost = new Fake();
                var first = (Task<int>)caller.Invoke(null, new[] { (object)firstHost, firstMessage })!;
                var second = (Task<int>)caller.Invoke(null, new[] { (object)secondHost, secondMessage })!;
                Require(firstHost.Calls.Count == 1 && secondHost.Calls.Count == 1, "each Handle dispatches exactly once");
                Require(ReferenceEquals(firstHost.Calls[0].Route, secondHost.Calls[0].Route), "both host contexts use the same target route");
                Require((string)firstHost.Calls[0].Route.GetType().GetProperty("Key")!.GetValue(firstHost.Calls[0].Route)! == "Targets.Worker", "route identifies the qualified target");
                Require(ReferenceEquals(firstMessage, firstHost.Calls[0].Message) && ReferenceEquals(secondMessage, secondHost.Calls[0].Message), "each message is forwarded exactly once without copying");
                Require(!first.IsCompleted && !second.IsCompleted, "Handle tasks remain pending until their dispatcher completes");
                var fault = new InvalidOperationException("host fault");
                secondHost.Calls[0].Completion.SetException(fault);
                try { await second.WaitAsync(TimeSpan.FromSeconds(10)); throw new Exception("expected host fault"); }
                catch (InvalidOperationException observed) { Require(ReferenceEquals(fault, observed), "host fault identity must propagate"); }
                Require(!first.IsCompleted, "one host completion cannot affect the other");
                firstHost.Calls[0].Completion.SetResult(42);
                Require(await first.WaitAsync(TimeSpan.FromSeconds(10)) == 42, "Invoke result must be returned exactly");
                var plainTask = (Task<int>)plain.Invoke(null, new[] { firstMessage })!;
                Require(await plainTask.WaitAsync(TimeSpan.FromSeconds(10)) == 7, "non-effectful Handle remains callable with the prior signature");
                Console.WriteLine("actor body passed");
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
