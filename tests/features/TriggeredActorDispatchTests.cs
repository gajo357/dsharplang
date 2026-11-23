using System.Diagnostics;
using System.Security;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class TriggeredActorDispatchTests
{
    private static readonly SourceFile[] Sources =
    [
        new("contracts.ds", "module Contracts\ntype Request { value: int }"),
        new("actors.ds", """
            module Processing
            import Contracts
            actor Worker {
                trigger Queue<Request>("jobs")
                trigger Event<Request>()
                Handle(message: Request): Result<int, string> { Error("actor-body-ran") }
            }
            """),
        new("app.ds", """
            module App
            import Contracts
            import Processing
            async fn get(next: (int) -> Request): Result<int, string> { await Invoke<Worker>(next(7)) }
            async fn submit(next: (int) -> Request) { await Dispatch<Processing.Worker>(next(8)) }
            fn denied(): Result<int, string> { Error("host-denied") }
            fn isDenied(value: Result<int, string>): bool {
                match value { Success(_) => false
                    Error(error) => error == "host-denied" }
            }
            """)
    ];

    [Fact]
    public async Task TriggeredTargets()
    {
        var compiled = DSharpCompiler.Compile(Sources);
        Assert.True(compiled.CSharp is not null && compiled.Diagnostics.Count == 0 && compiled.RequiresRuntime,
            $"Expected triggered actor entries to compile: {string.Join("; ", compiled.Diagnostics)}");
        var actor = Assert.Single(compiled.Actors);
        Assert.Equal("Processing.Worker", actor.Name);
        Assert.Equal("Contracts.Request", actor.InputType);
        Assert.Equal("Result<int, string>", actor.ResultType);
        Assert.Equal("actors.ds", actor.SourcePath);
        var queue = Assert.Single(actor.QueueBindings);
        Assert.Equal("jobs", queue.QueueName);
        Assert.Equal("Contracts.Request", queue.MessageType);
        Assert.Equal("actors.ds", queue.SourcePath);
        Assert.Equal(Sources[1].Text.IndexOf("trigger Queue", StringComparison.Ordinal), queue.TriggerSpan.Start);
        var eventBinding = Assert.Single(actor.EventBindings);
        Assert.Equal("Contracts.Request", eventBinding.MessageType);
        Assert.Equal("actors.ds", eventBinding.SourcePath);
        Assert.Equal(Sources[1].Text.IndexOf("trigger Event", StringComparison.Ordinal), eventBinding.TriggerSpan.Start);

        var root = RepositoryRoot();
        var runtime = Path.Combine(root, "src/DSharp.Runtime/bin/Release/net11.0/DSharp.Runtime.dll");
        Assert.True(File.Exists(runtime), $"Built runtime ABI missing: {runtime}");
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-trigger-target-" + Guid.NewGuid().ToString("N"));
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
            Assert.True(build.ExitCode == 0, $"Triggered actor host build failed: {build.Output}");
            var host = await Run(directory, null, Path.Combine(directory, "bin/Release/net11.0/Generated.dll"));
            Assert.True(host.ExitCode == 0, $"Triggered actor host failed: {host.Output}");
            Assert.Contains("triggered target passed", host.Output);
            CheckRejections();
            await CheckCli(root, runtime, directory);
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void CheckRejections()
    {
        (string Label, string Before, string After, string Code)[] cases =
        [
            ("Invoke wrong message", "Invoke<Worker>(next(7))", "Invoke<Worker>(7)", "DS2047"),
            ("Dispatch wrong message", "Dispatch<Processing.Worker>(next(8))", "Dispatch<Processing.Worker>(8)", "DS2048"),
            ("Invoke unknown target", "Invoke<Worker>(next(7))", "Invoke<Missing>(next(7))", "DS2047"),
            ("Dispatch unknown target", "Dispatch<Processing.Worker>(next(8))", "Dispatch<Missing>(next(8))", "DS2048")
        ];
        foreach (var (label, before, after, code) in cases)
        {
            Assert.Contains(before, Sources[2].Text);
            var files = Sources.ToArray();
            files[2] = files[2] with { Text = files[2].Text.Replace(before, after, StringComparison.Ordinal) };
            var result = DSharpCompiler.Compile(files);
            Assert.True(result.CSharp is null && result.Diagnostics.Any(d => d.Code == code && d.Path == "app.ds"),
                $"Expected {label} diagnostic: {string.Join("; ", result.Diagnostics)}");
        }
        var bypass = Sources.ToArray();
        bypass[2] = bypass[2] with { Text = bypass[2].Text + "\nfn bypass(): int { Worker().Handle(Request { value = 1 }) }" };
        var direct = DSharpCompiler.Compile(bypass);
        Assert.True(direct.CSharp is null && direct.Diagnostics.Any(d => d.Path == "app.ds"),
            $"Expected direct actor Handle bypass rejection: {string.Join("; ", direct.Diagnostics)}");
    }

    private static async Task CheckCli(string root, string runtime, string directory)
    {
        var cli = Path.Combine(root, "src/DSharp.Cli/bin/Release/net11.0/dsharp.dll");
        Assert.True(File.Exists(cli), $"Built CLI missing: {cli}");
        var inputs = new List<string>();
        foreach (var file in Sources)
        {
            var path = Path.Combine(directory, file.Path);
            await File.WriteAllTextAsync(path, file.Text);
            inputs.Add(path);
        }
        var check = await Run(root, null, new[] { cli, "check" }.Concat(inputs).ToArray());
        Assert.True(check.ExitCode == 0, $"Multi-file CLI check should not need runtime ABI: {check.Output}");
        var missingOutput = Path.Combine(directory, "missing-runtime-output");
        var missing = await Run(root, null, new[] { cli, "compile" }.Concat(inputs).Append(missingOutput).ToArray());
        Assert.True(missing.ExitCode == 1 && missing.Output.Contains("DS0001:") && !Directory.Exists(missingOutput),
            $"CLI must reject missing runtime before output: {missing.Output}");
        var output = Path.Combine(directory, "cli-output");
        var success = await Run(root, runtime, new[] { cli, "compile" }.Concat(inputs).Append(output).ToArray());
        Assert.True(success.ExitCode == 0, $"Multi-file CLI compile failed: {success.Output}");
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
        catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Triggered target test timed out."); }
        return (process.ExitCode, await stdout + await stderr);
    }

    private const string HostProgram = """
        using System.Linq.Expressions;
        using System.Reflection;
        using DSharp.Runtime;

        static class Program
        {
            private static int evaluations;
            private static object Message(Type type, int value)
            {
                evaluations++;
                return Activator.CreateInstance(type, value)!;
            }
            private static Delegate Factory(Type delegateType)
            {
                var invoke = delegateType.GetMethod("Invoke")!;
                var value = Expression.Parameter(typeof(int), "value");
                var make = typeof(Program).GetMethod(nameof(Message), BindingFlags.Static | BindingFlags.NonPublic)!;
                var call = Expression.Call(make, Expression.Constant(invoke.ReturnType), value);
                return Expression.Lambda(delegateType, Expression.Convert(call, invoke.ReturnType), value).Compile();
            }
            private static void Require(bool condition, string message)
            {
                if (!condition) throw new Exception(message);
            }
            private static async Task<object?> Result(Task task)
            {
                await task.WaitAsync(TimeSpan.FromSeconds(10));
                return task.GetType().GetProperty("Result")!.GetValue(task);
            }
            private static async Task Main()
            {
                var app = typeof(Program).Assembly.GetTypes().Single(type => type.Name == "App");
                MethodInfo Entry(string name) => app.GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy) ?? throw new Exception("missing entry " + name);
                Task Start(string name, Fake host, Type? expectedResult = null)
                {
                    var entry = Entry(name);
                    var args = entry.GetParameters();
                    Require(args.Length == 2 && args[0].ParameterType == typeof(IActorDispatcher), "dispatcher must be the scoped first argument");
                    if (expectedResult is not null) Require(entry.ReturnType == typeof(Task<>).MakeGenericType(expectedResult), "unexpected host receipt type");
                    return (Task)entry.Invoke(null, new object?[] { host, Factory(args[1].ParameterType) })!;
                }
                var dispatcher = new Fake();
                var invocation = Start("get", dispatcher);
                var acceptance = Start("submit", dispatcher, typeof(InvocationReceipt));
                Require(evaluations == 2, "each argument must be evaluated once");
                Require(dispatcher.Invocations.Count == 1 && dispatcher.Dispatches.Count == 1, "exactly one operation of each kind");
                var invokeCall = dispatcher.Invocations[0];
                var dispatchCall = dispatcher.Dispatches[0];
                Require(ReferenceEquals(invokeCall.Route, dispatchCall.Route), "Invoke and Dispatch must share the exact trigger-bearing actor route");
                Require((string)invokeCall.Route.GetType().GetProperty("Key")!.GetValue(invokeCall.Route)! == "Processing.Worker", "route must identify the source actor");
                Require((int)invokeCall.Message!.GetType().GetProperty("value")!.GetValue(invokeCall.Message)! == 7, "Invoke message forwarded");
                Require((int)dispatchCall.Message!.GetType().GetProperty("value")!.GetValue(dispatchCall.Message)! == 8, "Dispatch message forwarded");
                Require(!invocation.IsCompleted && !acceptance.IsCompleted, "independent fake operations begin pending");
                var denied = Entry("denied").Invoke(null, null);
                invokeCall.Complete(denied);
                var result = await Result(invocation);
                Require((bool)Entry("isDenied").Invoke(null, new[] { result })!, "the host's business Error must reach Invoke unchanged");
                Require(!acceptance.IsCompleted, "Invoke completion must not imply Dispatch acceptance");
                var receipt = new InvocationReceipt();
                dispatchCall.Completion.SetResult(receipt);
                Require(ReferenceEquals(receipt, await Result(acceptance)), "Dispatch returns the exact fake acceptance receipt");
                Require(evaluations == 2, "no argument is evaluated twice");
                Console.WriteLine("triggered target passed");
            }
        }
        sealed class Fake : IActorDispatcher
        {
            public readonly List<InvokeCall> Invocations = new();
            public readonly List<DispatchCall> Dispatches = new();
            public Task<TResult> Invoke<TMessage, TResult>(ActorRoute<TMessage, TResult> route, TMessage message)
            {
                var completion = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                Invocations.Add(new InvokeCall(route, message, value => completion.SetResult((TResult)value!)));
                return completion.Task;
            }
            public Task<InvocationReceipt> Dispatch<TMessage, TResult>(ActorRoute<TMessage, TResult> route, TMessage message)
            {
                var completion = new TaskCompletionSource<InvocationReceipt>(TaskCreationOptions.RunContinuationsAsynchronously);
                Dispatches.Add(new DispatchCall(route, message, completion));
                return completion.Task;
            }
            public Task<PublicationReceipt> Publish<TMessage>(MessageContract<TMessage> contract, TMessage message) => throw new Exception("unexpected implicit publication");
        }
        sealed record InvokeCall(object Route, object? Message, Action<object?> Complete);
        sealed record DispatchCall(object Route, object? Message, TaskCompletionSource<InvocationReceipt> Completion);
        """;
}
