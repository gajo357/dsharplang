using System.Diagnostics;
using System.Security;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class ScopedInvokePropagationTests
{
    private static readonly SourceFile[] Sources =
    [
        new("contracts.ds", "module Contracts\ntype Request { value: int }"),
        new("service.ds", """
            module Service
            import Contracts
            actor Worker { Handle(message: Request): int { 1000 } }
            async fn middle(value: int): int {
                let next = await tick(value)
                await leaf(next)
            }
            async fn recur(count: int, value: int): int {
                if count == 0 { await leaf(value) } else { await recur(count - 1, value) }
            }
            async fn tick(value: int): int { value + 1 }
            async fn leaf(value: int): int { await Invoke<Worker>(Request { value = value }) }
            """),
        new("app.ds", """
            module App
            import Service
            async fn outer(next: (int) -> int): int { await Service.middle(next(7)) }
            async fn repeat(value: int): int { await Service.recur(3, value) }
            fn plain(value: int): int { value + 1 }
            """)
    ];

    [Fact]
    public async Task ScopedCalls()
    {
        var compiled = DSharpCompiler.Compile(Sources);
        Assert.True(compiled.CSharp is not null && compiled.Diagnostics.Count == 0 && compiled.RequiresRuntime,
            $"Expected scoped async chain: {string.Join("; ", compiled.Diagnostics)}");
        var root = RepositoryRoot();
        var runtime = Path.Combine(root, "src/DSharp.Runtime/bin/Release/net11.0/DSharp.Runtime.dll");
        Assert.True(File.Exists(runtime), $"Built runtime ABI missing: {runtime}");
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-scope-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "Generated.cs"), compiled.CSharp!);
            await File.WriteAllTextAsync(Path.Combine(directory, "Program.cs"), HostProgram);
            var escaped = SecurityElement.Escape(runtime);
            await File.WriteAllTextAsync(Path.Combine(directory, "Generated.csproj"), $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net11.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup><ItemGroup><Reference Include=\"DSharp.Runtime\"><HintPath>{escaped}</HintPath></Reference></ItemGroup></Project>");
            await File.WriteAllTextAsync(Path.Combine(directory, "global.json"), "{\"sdk\":{\"version\":\"11.0.100\",\"rollForward\":\"disable\"}}");
            await File.WriteAllTextAsync(Path.Combine(directory, "NuGet.Config"), "<configuration><packageSources><clear/></packageSources></configuration>");
            var build = await Run(directory, "build", "Generated.csproj", "--configuration", "Release", "--nologo");
            Assert.True(build.ExitCode == 0, $"Scoped host build failed: {build.Output}");
            var host = await Run(directory, Path.Combine(directory, "bin/Release/net11.0/Generated.dll"));
            Assert.True(host.ExitCode == 0, $"Scoped host failed: {host.Output}");
            Assert.Contains("scoped host passed", host.Output);
            CheckUnsafePaths();
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void CheckUnsafePaths()
    {
        const string common = """
            module Demo
            type Request { value: int }
            actor Worker { Handle(message: Request): int { message.value } }
            async fn leaf(value: int): int { await Invoke<Worker>(Request { value = value }) }
            async fn middle(value: int): int { await leaf(value) }
            """;
        string[] invalid =
        [
            "fn bad(): Unit { let pending = middle(1)\n () }",
            "async fn bad(): int { let pending = middle(1)\n await pending }",
            "fn bad(): Unit { let callback = middle\n () }",
            "async fn bad(): int { let callback = async () => { await middle(1) }\n await callback() }",
            "async fn bad(): int { await (async () => { await middle(1) })() }",
            "actor Caller { Handle(message: Request): int { await middle(message.value) } }",
            "actor Caller { async helper(message: Request): int { await middle(message.value) } Handle(message: Request): int { 1 } }",
            "extend Request { async fn call(self): int { await middle(self.value) } }",
            "async fn pair(first: int, second: int): int { await middle(first) }\nfn bad(): Unit { let callback = pair(1)\n () }"
        ];
        foreach (var scenario in invalid)
        {
            var result = DSharpCompiler.Compile(common + "\n" + scenario, "unsafe-scope.ds");
            Assert.True(result.CSharp is null && result.Diagnostics.Any(d => d.Code == "DS2047" && d.Path == "unsafe-scope.ds"),
                $"Expected transitive scoped call rejection: {scenario}; diagnostics: {string.Join("; ", result.Diagnostics)}");
        }

        const string http = """
            module Demo
            type Raw { value: int }
            type Request { value: int }
            type Reply { code: int }
            actor Decoder { Handle(raw: Raw): Result<Request, string> { Success(Request { value = raw.value }) } }
            async fn inner(raw: Raw): Result<Request, string> { await Invoke<Decoder>(raw) }
            async fn decode(raw: Raw): Result<Request, string> { await inner(raw) }
            fn mapBinding(error: string): Reply { Reply { code = 400 } }
            fn mapValue(value: int): Reply { Reply { code = value } }
            actor Worker { trigger Http<Reply>(decode, mapBinding, mapValue) Handle(message: Request): int { message.value } }
            """;
        var callback = DSharpCompiler.Compile(http, "http-scope.ds");
        Assert.True(callback.CSharp is null && callback.Diagnostics.Any(d => d.Code == "DS2047" && d.Path == "http-scope.ds" && d.Span.Start == http.LastIndexOf("decode,", StringComparison.Ordinal)),
            $"Expected transitive HTTP callback rejection: {string.Join("; ", callback.Diagnostics)}");

        const string receipt = """
            module Demo
            actor Worker { Handle(message: Unit): int { 1 } }
            async fn send() { await Dispatch<Worker>(()) }
            async fn caller(): Unit { let receipt = await send()
                () }
            """;
        var receiptResult = DSharpCompiler.Compile(receipt, "receipt-scope.ds");
        Assert.True(receiptResult.CSharp is null && receiptResult.Diagnostics.Any(d => d.Code == "DS2047" && d.Path == "receipt-scope.ds"),
            $"Receipt propagation must remain unsupported: {string.Join("; ", receiptResult.Diagnostics)}");

        var ordinary = DSharpCompiler.Compile("module Demo\nfn Invoke<T>(value: T): T { value }\nfn ordinary(): int { Invoke<int>(7) }", "ordinary-scope.ds");
        Assert.True(ordinary.CSharp is not null && ordinary.Diagnostics.Count == 0 && !ordinary.RequiresRuntime,
            $"Ordinary Invoke shadow must remain unaffected: {string.Join("; ", ordinary.Diagnostics)}");
    }

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "DSharp.slnx"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }

    private static async Task<(int ExitCode, string Output)> Run(string directory, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Scoped Invoke host timed out."); }
        return (process.ExitCode, await stdout + await stderr);
    }

    private const string HostProgram = """
        using System.Reflection;
        using DSharp.Runtime;

        static class Program
        {
            private static int evaluations;
            private static int Next(int value) { evaluations++; return value; }
            private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
            private static async Task<object?> Result(Task task)
            {
                await task.WaitAsync(TimeSpan.FromSeconds(10));
                return task.GetType().GetProperty("Result")!.GetValue(task);
            }
            private static async Task Main()
            {
                var app = typeof(Program).Assembly.GetTypes().Single(type => type.Name == "App");
                MethodInfo Entry(string name) => app.GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy) ?? throw new Exception("missing " + name);
                Task Start(string name, Fake host, object argument)
                {
                    var method = Entry(name);
                    var parameters = method.GetParameters();
                    Require(parameters.Length == 2 && parameters[0].ParameterType == typeof(IActorDispatcher), "scoped dispatcher must be explicit first argument");
                    if (argument is Delegate callback)
                        argument = Delegate.CreateDelegate(parameters[1].ParameterType, callback.Target, callback.Method);
                    return (Task)method.Invoke(null, new[] { (object)host, argument })!;
                }
                var firstHost = new Fake();
                var secondHost = new Fake();
                var first = Start("outer", firstHost, (Func<int, int>)Next);
                var second = Start("outer", secondHost, (Func<int, int>)Next);
                Require(evaluations == 2, "caller arguments must be evaluated once");
                Require(firstHost.Calls.Count == 1 && secondHost.Calls.Count == 1, "each caller must invoke its own dispatcher once");
                Require(ReferenceEquals(firstHost.Calls[0].Route, secondHost.Calls[0].Route), "qualified transitive calls must preserve the actor route");
                Require((int)firstHost.Calls[0].Message!.GetType().GetProperty("value")!.GetValue(firstHost.Calls[0].Message)! == 8, "awaited helper must forward the exact message");
                Require(!first.IsCompleted && !second.IsCompleted, "calls must remain pending until their own dispatcher completes");
                secondHost.Calls[0].Completion.SetResult(23);
                Require((int)(await Result(second))! == 23 && !first.IsCompleted, "host completion must remain isolated");
                firstHost.Calls[0].Completion.SetResult(17);
                Require((int)(await Result(first))! == 17, "caller must receive the exact Invoke result");
                var recursive = Start("repeat", firstHost, 5);
                Require(firstHost.Calls.Count == 2 && ReferenceEquals(firstHost.Calls[0].Route, firstHost.Calls[1].Route), "recursive call graph must preserve route and issue one Invoke");
                Require((int)firstHost.Calls[1].Message!.GetType().GetProperty("value")!.GetValue(firstHost.Calls[1].Message)! == 5, "recursive value forwarded");
                firstHost.Calls[1].Completion.SetResult(29);
                Require((int)(await Result(recursive))! == 29, "recursive caller receives exact result");
                var faultTask = Start("repeat", secondHost, 3);
                var fault = new InvalidOperationException("host fault");
                secondHost.Calls[1].Completion.SetException(fault);
                try { await Result(faultTask); throw new Exception("expected host fault"); }
                catch (InvalidOperationException observed) { Require(ReferenceEquals(fault, observed), "host fault identity must propagate"); }
                var plain = Entry("plain");
                Require(plain.GetParameters().Length == 1 && plain.GetParameters()[0].ParameterType == typeof(int), "unaffected export signature must stay unchanged");
                Require((int)plain.Invoke(null, new object[] { 41 })! == 42, "unaffected export behavior");
                Require(evaluations == 2, "arguments evaluated once across the chain");
                Console.WriteLine("scoped host passed");
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
