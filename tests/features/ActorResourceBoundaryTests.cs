using System.Diagnostics;
using System.Security;
using System.Text.RegularExpressions;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class ActorResourceBoundaryTests
{
    private const string Source = """
        module Demo
        type Request { value: int }
        interface Tracker {
            mark(value: int): int
            async ready(): int
            async released(): int
        }
        resource Transaction(tracker: Tracker) {
            async read(): int {
                let value = await tracker.ready()
                let ignored = tracker.mark(2)
                value
            }
            async release(): Unit {
                let ignored = await tracker.released()
                let seen = tracker.mark(3)
                ()
            }
        }
        actor Worker { Handle(message: Request): int { message.value } }
        async fn twice(value: int): int { value * 2 }
        async fn after(tracker: Tracker): int {
            let value = using tx = Transaction(tracker) { await tx.read() }
            await Invoke<Worker>(Request { value = value })
        }
        async fn ordinary(tracker: Tracker): int {
            using tx = Transaction(tracker) {
                let value = await tx.read()
                await twice(value)
            }
        }
        async fn hostAfter(tracker: Tracker): int {
            let value = tracker.mark(4)
            await Invoke<Worker>(Request { value = value })
        }
        """;

    [Fact]
    public async Task ResourceWaitBoundary()
    {
        var compiled = DSharpCompiler.Compile(Source, "resource-boundary.ds");
        Assert.True(compiled.CSharp is not null && compiled.Diagnostics.Count == 0 && compiled.RequiresRuntime,
            $"Expected legal resource and dispatcher boundaries: {string.Join("; ", compiled.Diagnostics)}");
        var tracker = Regex.Match(compiled.CSharp!, @"public interface (?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{[^}]*__dsharp_method_ready\(", RegexOptions.Singleline);
        Assert.True(tracker.Success, "Generated tracker interface not found.");
        var root = RepositoryRoot();
        var runtime = Path.Combine(root, "src/DSharp.Runtime/bin/Release/net11.0/DSharp.Runtime.dll");
        Assert.True(File.Exists(runtime), $"Built runtime ABI missing: {runtime}");
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-resource-wait-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "Generated.cs"), compiled.CSharp!);
            await File.WriteAllTextAsync(Path.Combine(directory, "Program.cs"), HostProgram.Replace("TRACKER_INTERFACE", tracker.Groups["name"].Value, StringComparison.Ordinal));
            var escaped = SecurityElement.Escape(runtime);
            await File.WriteAllTextAsync(Path.Combine(directory, "Generated.csproj"), $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net11.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup><ItemGroup><Reference Include=\"DSharp.Runtime\"><HintPath>{escaped}</HintPath></Reference></ItemGroup></Project>");
            await File.WriteAllTextAsync(Path.Combine(directory, "global.json"), "{\"sdk\":{\"version\":\"11.0.100\",\"rollForward\":\"disable\"}}");
            await File.WriteAllTextAsync(Path.Combine(directory, "NuGet.Config"), "<configuration><packageSources><clear/></packageSources></configuration>");
            var build = await Run(directory, null, "build", "Generated.csproj", "--configuration", "Release", "--nologo");
            Assert.True(build.ExitCode == 0, $"Resource-boundary host build failed: {build.Output}");
            var host = await Run(directory, null, Path.Combine(directory, "bin/Release/net11.0/Generated.dll"));
            Assert.True(host.ExitCode == 0, $"Resource-boundary host failed: {host.Output}");
            Assert.Contains("resource boundary passed", host.Output);
            CheckRejections();
            await CheckCli(root, runtime, directory);
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void CheckRejections()
    {
        const string common = """
            module Demo
            type Request { value: int }
            resource Tx(value: int) { read(): int { value } release(): Unit { () } }
            actor Worker { Handle(message: Request): int { message.value } }
            async fn inner(message: Request): int { await Invoke<Worker>(message) }
            async fn target(message: Request) { await Dispatch<Worker>(message) }
            async fn publish(message: Request) { await Dispatch(message) }
            """;
        (string Label, string Body, string Token)[] cases =
        [
            ("direct braced used", "async fn bad(): int { using tx = Tx(1) { await Invoke<Worker>(Request { value = tx.read() }) } }", "Invoke<Worker>"),
            ("direct braced unused", "async fn bad(): int { using tx = Tx(1) { await Invoke<Worker>(Request { value = 1 }) } }", "Invoke<Worker>"),
            ("direct declaration unused", "async fn bad(): int { using tx = Tx(1)\n await Invoke<Worker>(Request { value = 1 }) }", "Invoke<Worker>"),
            ("direct nested", "async fn bad(): int { using outer = Tx(1) { using innerTx = Tx(2) { await Invoke<Worker>(Request { value = 1 }) } } }", "Invoke<Worker>"),
            ("nested acquisition", "async fn bad(): int { using outer = Tx(1) { using innerTx = Tx(await Invoke<Worker>(Request { value = 1 })) { innerTx.read() } } }", "Invoke<Worker>"),
            ("direct targeted Dispatch", "async fn bad(): Unit { using tx = Tx(1) { let receipt = await Dispatch<Worker>(Request { value = 1 })\n () } }", "Dispatch<Worker>"),
            ("direct publication", "async fn bad(): Unit { using tx = Tx(1) { let receipt = await Dispatch(Request { value = 1 })\n () } }", "Dispatch(Request"),
            ("transitive braced", "async fn bad(): int { using tx = Tx(1) { await inner(Request { value = tx.read() }) } }", "inner(Request"),
            ("transitive declaration unused", "async fn bad(): int { using tx = Tx(1)\n await inner(Request { value = 1 }) }", "inner(Request"),
            ("transitive branch", "async fn bad(): int { using tx = Tx(1) { if true { await inner(Request { value = 1 }) } else { 0 } } }", "inner(Request"),
            ("nested transitive unused", "async fn bad(): int { using outer = Tx(1) { using innerTx = Tx(2) { await inner(Request { value = 1 }) } } }", "inner(Request"),
            ("transitive targeted Dispatch", "async fn bad(): Unit { using tx = Tx(1) { let receipt = await target(Request { value = 1 })\n () } }", "target(Request"),
            ("transitive publication", "async fn bad(): Unit { using tx = Tx(1) { let receipt = await publish(Request { value = 1 })\n () } }", "publish(Request")
        ];
        foreach (var (label, body, token) in cases)
        {
            var source = common + "\n" + body;
            var result = DSharpCompiler.Compile(source, "invalid-resource-wait.ds");
            var start = source.LastIndexOf(token, StringComparison.Ordinal);
            Assert.True(result.CSharp is null && result.Diagnostics.Any(d => d.Code == "DS2041" && d.Path == "invalid-resource-wait.ds" && d.Span.Start == start),
                $"Expected located DS2041 for {label}: {string.Join("; ", result.Diagnostics)}");
        }
        const string beforeAcquisition = "async fn safe(): int { using tx = Tx(await Invoke<Worker>(Request { value = 1 })) { tx.read() } }";
        var before = DSharpCompiler.Compile(common + "\n" + beforeAcquisition, "before-resource.ds");
        Assert.True(before.CSharp is not null && before.Diagnostics.Count == 0 && before.RequiresRuntime,
            $"Actor wait in the acquisition before the owned scope starts should compile: {string.Join("; ", before.Diagnostics)}");

        SourceFile[] multi =
        [
            new("contracts.ds", "module Contracts\ntype Request { value: int }"),
            new("calls.ds", "module Calls\nimport Contracts\nactor Worker { Handle(message: Request): int { message.value } }\nasync fn inner(message: Request): int { await Invoke<Worker>(message) }"),
            new("user.ds", "module User\nimport Contracts\nimport Calls\nresource Tx { read(): int { 1 } release(): Unit { () } }\nasync fn bad(): int { using tx = Tx() { await Calls.inner(Request { value = 1 }) } }")
        ];
        var imported = DSharpCompiler.Compile(multi);
        Assert.True(imported.CSharp is null && imported.Diagnostics.Any(d => d.Code == "DS2041" && d.Path == "user.ds" && d.Span.Start == multi[2].Text.IndexOf("Calls.inner", StringComparison.Ordinal) + "Calls.".Length),
            $"Expected located qualified transitive wait rejection: {string.Join("; ", imported.Diagnostics)}");
    }

    private static async Task CheckCli(string root, string runtime, string directory)
    {
        var cli = Path.Combine(root, "src/DSharp.Cli/bin/Release/net11.0/dsharp.dll");
        Assert.True(File.Exists(cli), $"Built CLI missing: {cli}");
        var source = Path.Combine(directory, "resource-boundary.ds");
        await File.WriteAllTextAsync(source, Source);
        var check = await Run(root, null, cli, "check", source);
        Assert.True(check.ExitCode == 0, $"Positive CLI check failed: {check.Output}");
        var output = Path.Combine(directory, "cli-output");
        var compiled = await Run(root, runtime, cli, "compile", source, output);
        Assert.True(compiled.ExitCode == 0, $"Positive CLI compile failed: {compiled.Output}");
        var invalidPath = Path.Combine(directory, "invalid.ds");
        await File.WriteAllTextAsync(invalidPath, "module Demo\ntype Request { value: int }\nresource Tx { release(): Unit { () } }\nactor Worker { Handle(message: Request): int { message.value } }\nasync fn bad(): int { using tx = Tx() { await Invoke<Worker>(Request { value = 1 }) } }");
        var invalid = await Run(root, null, cli, "check", invalidPath);
        Assert.True(invalid.ExitCode == 1 && invalid.Output.Contains("DS2041") && invalid.Output.Contains("invalid.ds"),
            $"CLI must report the resource boundary: {invalid.Output}");
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
        catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Resource boundary test process timed out."); }
        return (process.ExitCode, await stdout + await stderr);
    }

    private const string HostProgram = """
        using System.Collections.Concurrent;
        using System.Reflection;
        using DSharp.Generated;
        using DSharp.Runtime;

        static class Program
        {
            private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
            private static async Task Main()
            {
                var demo = typeof(Program).Assembly.GetTypes().Single(type => type.Name == "Demo");
                MethodInfo Entry(string name) => demo.GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy) ?? throw new Exception("missing " + name);
                Task<int> Start(string name, params object[] args) => (Task<int>)Entry(name).Invoke(null, args)!;
                var host = new Fake();
                var tracker = new HostTracker();
                var after = Start("after", host, tracker);
                await tracker.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Require(host.Calls.Count == 0 && !after.IsCompleted, "resource read must precede actor invocation");
                tracker.Ready.SetResult(40);
                await tracker.ReleaseStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Require(host.Calls.Count == 0 && !after.IsCompleted, "actor invocation must wait for async cleanup");
                tracker.Released.SetResult(0);
                await host.Called.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Require(tracker.Marks.SequenceEqual(new[] { 2, 3 }), "read and cleanup must each complete once before Invoke");
                Require((int)host.Calls[0].Message!.GetType().GetProperty("value")!.GetValue(host.Calls[0].Message)! == 40, "post-cleanup message value");
                Require(!after.IsCompleted, "actor result remains pending on dispatcher");
                host.Calls[0].Completion.SetResult(42);
                Require(await after.WaitAsync(TimeSpan.FromSeconds(10)) == 42, "post-cleanup Invoke result");

                var failedHost = new Fake();
                var failing = new HostTracker();
                var failed = Start("after", failedHost, failing);
                await failing.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
                failing.Ready.SetResult(9);
                await failing.ReleaseStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
                var cleanupFault = new InvalidOperationException("cleanup fault");
                failing.Released.SetException(cleanupFault);
                try { await failed.WaitAsync(TimeSpan.FromSeconds(10)); throw new Exception("expected cleanup fault"); }
                catch (InvalidOperationException observed) { Require(ReferenceEquals(cleanupFault, observed), "cleanup fault must propagate unchanged"); }
                Require(failedHost.Calls.Count == 0 && failing.Marks.SequenceEqual(new[] { 2 }), "failed cleanup must not start actor invocation");

                var ordinary = new HostTracker();
                var normal = Start("ordinary", ordinary);
                await ordinary.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
                ordinary.Ready.SetResult(20);
                await ordinary.ReleaseStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
                ordinary.Released.SetResult(0);
                Require(await normal.WaitAsync(TimeSpan.FromSeconds(10)) == 40 && ordinary.Marks.SequenceEqual(new[] { 2, 3 }), "ordinary awaited resource/function calls remain legal and clean up");

                var borrowed = new HostTracker();
                var borrowedHost = new Fake();
                var borrowCall = Start("hostAfter", borrowedHost, borrowed);
                await borrowedHost.Called.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Require(borrowed.Marks.SequenceEqual(new[] { 4 }) && !borrowed.ReleaseStarted.Task.IsCompleted, "ordinary host interface is not an owned transaction");
                borrowedHost.Calls[0].Completion.SetResult(4);
                Require(await borrowCall.WaitAsync(TimeSpan.FromSeconds(10)) == 4, "host dependency path remains usable");
                Console.WriteLine("resource boundary passed");
            }
        }
        sealed class HostTracker : TRACKER_INTERFACE
        {
            public readonly ConcurrentQueue<int> Marks = new();
            public readonly TaskCompletionSource<int> Ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public readonly TaskCompletionSource<int> Released = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public readonly TaskCompletionSource<bool> ReadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public readonly TaskCompletionSource<bool> ReleaseStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public int __dsharp_method_mark(int value) { Marks.Enqueue(value); return value; }
            public Task<int> __dsharp_method_ready() { ReadStarted.TrySetResult(true); return Ready.Task; }
            public Task<int> __dsharp_method_released() { ReleaseStarted.TrySetResult(true); return Released.Task; }
        }
        sealed class Fake : IActorDispatcher
        {
            public readonly List<Call> Calls = new();
            public readonly TaskCompletionSource<bool> Called = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public Task<TResult> Invoke<TMessage, TResult>(ActorRoute<TMessage, TResult> route, TMessage message)
            {
                if (typeof(TResult) != typeof(int)) throw new Exception("unexpected result type");
                var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                Calls.Add(new Call(route, message, completion));
                Called.TrySetResult(true);
                return (Task<TResult>)(object)completion.Task;
            }
            public Task<InvocationReceipt> Dispatch<TMessage, TResult>(ActorRoute<TMessage, TResult> route, TMessage message) => throw new Exception("unexpected Dispatch");
            public Task<PublicationReceipt> Publish<TMessage>(MessageContract<TMessage> contract, TMessage message) => throw new Exception("unexpected Publish");
        }
        sealed record Call(object Route, object? Message, TaskCompletionSource<int> Completion);
        """;
}
