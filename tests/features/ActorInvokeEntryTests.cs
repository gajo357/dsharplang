using System.Diagnostics;
using System.Security;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class ActorInvokeEntryTests
{
    private static readonly SourceFile[] Sources =
    [
        new("contracts.ds", "module Contracts\ntype Request { value: int }"),
        new("alpha.ds", """
            module Alpha
            import Contracts
            actor Worker { Handle(message: Request): int { 1000 } }
            actor Empty { Handle(message: Request): Unit { () } }
            actor Declined { Handle(message: Request): Result<int, string> { Error("actor-body") } }
            """),
        new("beta.ds", "module Beta\nimport Contracts\nactor Worker { Handle(message: Request): int { 2000 } }"),
        new("app-a.ds", """
            module App
            import Contracts
            import Alpha
            async fn first(next: (int) -> Request): int { await Invoke<Worker>(next(7)) }
            async fn same(next: (int) -> Request): int { await Invoke<Alpha.Worker>(next(8)) }
            async fn empty(next: (int) -> Request): Unit { await Invoke<Empty>(next(9)) }
            async fn failed(next: (int) -> Request): Result<int, string> { await Invoke<Declined>(next(10)) }
            fn denied(): Result<int, string> { Error("host-denied") }
            fn isDenied(value: Result<int, string>): bool {
                match value { Success(_) => false
                    Error(error) => error == "host-denied" }
            }
            """),
        new("app-b.ds", """
            module App
            import Contracts
            import Beta
            async fn other(next: (int) -> Request): int { await Invoke<Beta.Worker>(next(11)) }
            """)
    ];

    [Fact]
    public async Task GeneratedEntriesUseActualRuntimeAbiAndHostScopedDispatcher()
    {
        var compiled = DSharpCompiler.Compile(Sources);
        Assert.True(compiled.CSharp is not null && compiled.Diagnostics.Count == 0,
            $"Expected multi-file Invoke entries to compile: {string.Join("; ", compiled.Diagnostics)}");
        Assert.True(compiled.RequiresRuntime);
        var root = RepositoryRoot();
        var runtime = RuntimeAssembly(root);
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-invoke-host-" + Guid.NewGuid().ToString("N"));
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
            Assert.True(build.ExitCode == 0, $"Generated host build failed: {build.Output}");
            var host = await Run(directory, null, Path.Combine(directory, "bin/Release/net11.0/Generated.dll"));
            Assert.True(host.ExitCode == 0, $"Generated host failed: {host.Output}");
            Assert.Contains("host checks passed", host.Output);
            await CheckCli(root, runtime, directory);
            InvokeIsTypedScopedAndCannotEscapeTheSupportedEntryBoundary();
            HttpDescriptorsCannotCaptureDispatcherEntries();
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void HttpDescriptorsCannotCaptureDispatcherEntries()
    {
        const string common = """
            module Demo
            type Raw { value: int }
            type Request { value: int }
            type Reply { code: int }
            actor Decoder { Handle(raw: Raw): Result<Request, string> { Success(Request { value = raw.value }) } }
            async fn decode(raw: Raw): Result<Request, string> { await Invoke<Decoder>(raw) }
            fn mapBinding(error: string): Reply { Reply { code = 400 } }
            fn mapSuccess(value: int): Reply { Reply { code = value } }
            fn mapBusiness(error: bool): Reply { Reply { code = 500 } }
            """;
        foreach (var worker in new[]
        {
            "actor Worker { trigger Http<Reply>(decode, mapBinding, mapSuccess, mapBusiness) Handle(message: Request): Result<int, bool> { Success(message.value) } }",
            "actor Worker { trigger Http<Reply>(decode, mapBinding, mapSuccess) Handle(message: Request): int { message.value } }"
        })
        {
            var source = common + "\n" + worker;
            var result = DSharpCompiler.Compile(source, "http-invoke.ds");
            Assert.True(result.CSharp is null && result.Diagnostics.Any(d => d.Code == "DS2047" && d.Path == "http-invoke.ds" && d.Span.Start == source.LastIndexOf("decode,", StringComparison.Ordinal)),
                $"Expected dispatcher-dependent HTTP binder rejection: {string.Join("; ", result.Diagnostics)}");
        }

        SourceFile[] sources =
        [
            new("contracts.ds", "module Contracts\ntype Raw { value: int }\ntype Request { value: int }\ntype Reply { code: int }"),
            new("binding.ds", "module Binding\nimport Contracts\nactor Decoder { Handle(raw: Raw): Result<Request, string> { Success(Request { value = raw.value }) } }\nasync fn decode(raw: Raw): Result<Request, string> { await Invoke<Decoder>(raw) }"),
            new("worker.ds", "module App\nimport Contracts\nimport Binding\nfn mapBinding(error: string): Reply { Reply { code = 400 } }\nfn mapSuccess(value: int): Reply { Reply { code = value } }\nfn mapBusiness(error: bool): Reply { Reply { code = 500 } }\nactor Worker { trigger Http<Reply>(Binding.decode, mapBinding, mapSuccess, mapBusiness) Handle(message: Request): Result<int, bool> { Success(message.value) } }")
        ];
        var qualified = DSharpCompiler.Compile(sources);
        Assert.True(qualified.CSharp is null && qualified.Diagnostics.Any(d => d.Code == "DS2047" && d.Path == "worker.ds" && d.Span.Start == sources[2].Text.IndexOf("Binding.decode,", StringComparison.Ordinal)),
            $"Expected imported dispatcher-dependent HTTP binder rejection: {string.Join("; ", qualified.Diagnostics)}");
        sources[2] = sources[2] with { Text = sources[2].Text.Replace("Binding.decode, mapBinding, mapSuccess, mapBusiness", "Binding.decode, mapBinding, mapSuccess", StringComparison.Ordinal).Replace("Handle(message: Request): Result<int, bool> { Success(message.value) }", "Handle(message: Request): int { message.value }", StringComparison.Ordinal) };
        var qualifiedPlain = DSharpCompiler.Compile(sources);
        Assert.True(qualifiedPlain.CSharp is null && qualifiedPlain.Diagnostics.Any(d => d.Code == "DS2047" && d.Path == "worker.ds" && d.Span.Start == sources[2].Text.IndexOf("Binding.decode,", StringComparison.Ordinal)),
            $"Expected imported dispatcher-dependent plain HTTP binder rejection: {string.Join("; ", qualifiedPlain.Diagnostics)}");
    }

    private static void InvokeIsTypedScopedAndCannotEscapeTheSupportedEntryBoundary()
    {
        const string declarations = "module Demo\ntype Request { value: int }\nactor Worker { Handle(message: Request): int { message.value } }\n";
        string[] invalid =
        [
            "async fn use(): int { await Invoke<Missing>(Request { value = 1 }) }",
            "async fn use(): int { await Invoke<Worker>(1) }",
            "async fn use(): int { await Invoke(Request { value = 1 }) }",
            "async fn use(): int { let pending = Invoke<Worker>(Request { value = 1 })\n 1 }",
            "fn use(): int { Invoke<Worker>(Request { value = 1 }) }",
            "async fn use(): int { let callback = async () => { await Invoke<Worker>(Request { value = 1 }) }\n await callback() }",
            "async fn use(): int { await (async () => { await Invoke<Worker>(Request { value = 1 }) })() }",
            "actor Caller { async helper(message: Request): int { await Invoke<Worker>(message) } Handle(message: Request): int { 1 } }",
            "extend Request { async fn call(self): int { await Invoke<Worker>(self) } }",
            "async fn entry(): int { await Invoke<Worker>(Request { value = 1 }) }\nfn leak(): Unit { let callback = entry\n () }",
            "async fn entry(value: int): int { await Invoke<Worker>(Request { value = value }) }\nfn leak(): Unit { let callback = entry\n () }",
            "async fn entry(first: int, second: int): int { await Invoke<Worker>(Request { value = first }) }\nfn leak(): Unit { let callback = entry(1)\n () }"
        ];
        foreach (var scenario in invalid)
        {
            var result = DSharpCompiler.Compile(declarations + scenario, "invalid-invoke.ds");
            Assert.True(result.CSharp is null && result.Diagnostics.Any(d => d.Code == "DS2047" && d.Path == "invalid-invoke.ds"),
                $"Expected invalid or unsupported Invoke rejection: {scenario}; diagnostics: {string.Join("; ", result.Diagnostics)}");
        }
        var actorBody = DSharpCompiler.Compile(declarations + "actor Caller { Handle(message: Request): int { await Invoke<Worker>(message) } }", "actor-body-invoke.ds");
        Assert.True(actorBody.CSharp is not null && actorBody.Diagnostics.Count == 0 && actorBody.RequiresRuntime,
            $"Expected resource-free direct actor Handle Invoke: {string.Join("; ", actorBody.Diagnostics)}");
        Assert.True(Assert.Single(actorBody.Actors, actor => actor.Name == "Caller").RequiresDispatcher);
        Assert.False(Assert.Single(actorBody.Actors, actor => actor.Name == "Worker").RequiresDispatcher);
        var nested = DSharpCompiler.Compile(declarations + "async fn entry(): int { await Invoke<Worker>(Request { value = 1 }) }\nasync fn caller(): int { await entry() }", "nested-invoke.ds");
        Assert.True(nested.CSharp is not null && nested.Diagnostics.Count == 0 && nested.RequiresRuntime,
            $"Expected directly awaited scoped Invoke chain: {string.Join("; ", nested.Diagnostics)}");
        var direct = DSharpCompiler.Compile(declarations + "fn use(): int { Worker().Handle(Request { value = 1 }) }", "direct-actor.ds");
        Assert.True(direct.CSharp is null && direct.Diagnostics.Any(d => d.Path == "direct-actor.ds"), "Expected direct actor-call rejection.");
        var shadow = DSharpCompiler.Compile("module Demo\nfn Invoke<T>(value: T): T { value }\nfn ordinary(): int { Invoke<int>(7) }", "shadow-invoke.ds");
        Assert.Empty(shadow.Diagnostics);
        Assert.False(shadow.RequiresRuntime);
        Assert.NotNull(shadow.CSharp);
        SourceFile[] importedShadow =
        [
            new("library.ds", "module Library\nfn Invoke<T>(value: T): T { value }"),
            new("consumer.ds", "module Consumer\nimport Library\nfn ordinary(): int { Invoke<int>(7) }")
        ];
        var importedOrdinary = DSharpCompiler.Compile(importedShadow);
        Assert.Empty(importedOrdinary.Diagnostics);
        Assert.False(importedOrdinary.RequiresRuntime);
        Assert.NotNull(importedOrdinary.CSharp);

        SourceFile[] hidden =
        [
            new("hidden.ds", "module Hidden\nactor Worker { Handle(message: Unit): int { 1 } }"),
            new("caller.ds", "module Caller\nasync fn run(): int { await Invoke<Worker>(()) }")
        ];
        var hiddenTarget = DSharpCompiler.Compile(hidden);
        Assert.True(hiddenTarget.CSharp is null && hiddenTarget.Diagnostics.Any(d => d.Path == "caller.ds"),
            $"Expected unimported actor target rejection: {string.Join("; ", hiddenTarget.Diagnostics)}");
        var localShadow = DSharpCompiler.Compile("module Demo\nfn ordinary(): int { let Invoke: (int) -> int = (value: int) => value\n Invoke(7) }", "local-invoke.ds");
        Assert.Empty(localShadow.Diagnostics);
        Assert.False(localShadow.RequiresRuntime);

        SourceFile[] ambiguous =
        [
            new("a.ds", "module A\nactor Worker { Handle(message: Unit): int { 1 } }"),
            new("b.ds", "module B\nactor Worker { Handle(message: Unit): int { 2 } }"),
            new("use.ds", "module Use\nimport A\nimport B\nasync fn run(): int { await Invoke<Worker>(()) }")
        ];
        var ambiguity = DSharpCompiler.Compile(ambiguous);
        Assert.True(ambiguity.CSharp is null && ambiguity.Diagnostics.Any(d => d.Code == "DS2031" && d.Path == "use.ds"),
            $"Expected ambiguous actor target rejection: {string.Join("; ", ambiguity.Diagnostics)}");

        SourceFile[] qualifiedCall =
        [
            new("actor.ds", "module Actors\nactor Worker { Handle(message: Unit): int { 1 } }"),
            new("entry.ds", "module Entries\nimport Actors\nasync fn entry(): int { await Invoke<Worker>(()) }"),
            new("caller.ds", "module Callers\nimport Entries\nasync fn caller(): int { await Entries.entry() }")
        ];
        var supportedCall = DSharpCompiler.Compile(qualifiedCall);
        Assert.True(supportedCall.CSharp is not null && supportedCall.Diagnostics.Count == 0 && supportedCall.RequiresRuntime,
            $"Expected qualified directly awaited scoped Invoke chain: {string.Join("; ", supportedCall.Diagnostics)}");

        SourceFile[] qualifiedCapture =
        [
            new("actor.ds", "module Actors\nactor Worker { Handle(message: Unit): int { 1 } }"),
            new("entry.ds", "module Entries\nimport Actors\nasync fn entry(first: int, second: int): int { await Invoke<Worker>(()) }"),
            new("caller.ds", "module Callers\nimport Entries\nfn leak(): Unit { let callback = Entries.entry\n () }")
        ];
        var forbiddenCapture = DSharpCompiler.Compile(qualifiedCapture);
        Assert.True(forbiddenCapture.CSharp is null && forbiddenCapture.Diagnostics.Any(d => d.Code == "DS2047" && d.Path == "caller.ds"),
            $"Expected qualified effectful entry capture rejection: {string.Join("; ", forbiddenCapture.Diagnostics)}");
        qualifiedCapture[2] = new("caller.ds", "module Callers\nimport Entries\nfn leak(): Unit { let callback = Entries.entry(1)\n () }");
        var forbiddenPartial = DSharpCompiler.Compile(qualifiedCapture);
        Assert.True(forbiddenPartial.CSharp is null && forbiddenPartial.Diagnostics.Any(d => d.Code == "DS2047" && d.Path == "caller.ds"),
            $"Expected qualified effectful entry partial application rejection: {string.Join("; ", forbiddenPartial.Diagnostics)}");
    }

    private static async Task CheckCli(string root, string runtime, string directory)
    {
        var cli = Path.Combine(root, "src/DSharp.Cli/bin/Release/net11.0/dsharp.dll");
        Assert.True(File.Exists(cli), $"Built CLI missing: {cli}");
        var sourcePath = Path.Combine(directory, "cli-invoke.ds");
        await File.WriteAllTextAsync(sourcePath, "module Demo\nactor Worker { Handle(message: Unit): int { 1 } }\nasync fn run(): int { await Invoke<Worker>(()) }");
        var check = await Run(root, null, cli, "check", sourcePath);
        Assert.True(check.ExitCode == 0, $"CLI check should not need a runtime assembly: {check.Output}");
        var missingDirectory = Path.Combine(directory, "missing-runtime-output");
        var missing = await Run(root, null, cli, "compile", sourcePath, missingDirectory);
        Assert.True(missing.ExitCode == 1 && missing.Output.Contains("DS0001:") && !Directory.Exists(missingDirectory), $"CLI should reject absent runtime input before output: {missing.Output}");
        var invalidDirectory = Path.Combine(directory, "invalid-runtime-output");
        var invalid = await Run(root, Path.Combine(directory, "missing-runtime.dll"), cli, "compile", sourcePath, invalidDirectory);
        Assert.True(invalid.ExitCode == 1 && invalid.Output.Contains("DS0001:") && !Directory.Exists(invalidDirectory), $"CLI should reject an invalid runtime path before output: {invalid.Output}");
        var wrongIdentityDirectory = Path.Combine(directory, "wrong-assembly-output");
        var wrongIdentity = await Run(root, cli, cli, "compile", sourcePath, wrongIdentityDirectory);
        Assert.True(wrongIdentity.ExitCode == 1 && wrongIdentity.Output.Contains("DS0001:") && !Directory.Exists(wrongIdentityDirectory), $"CLI should reject a different managed assembly: {wrongIdentity.Output}");
        var corruptPath = Path.Combine(directory, "corrupt-runtime.dll");
        await File.WriteAllTextAsync(corruptPath, "not an assembly");
        var corruptDirectory = Path.Combine(directory, "corrupt-assembly-output");
        var corrupt = await Run(root, corruptPath, cli, "compile", sourcePath, corruptDirectory);
        Assert.True(corrupt.ExitCode == 1 && corrupt.Output.Contains("DS0001:") && !Directory.Exists(corruptDirectory), $"CLI should reject a corrupt runtime assembly: {corrupt.Output}");
        var goodDirectory = Path.Combine(directory, "cli-output");
        var good = await Run(root, runtime, cli, "compile", sourcePath, goodDirectory);
        Assert.True(good.ExitCode == 0, $"CLI compile with runtime ABI failed: {good.Output}");
        Assert.True(File.Exists(Path.Combine(goodDirectory, "DSharp.Runtime.dll")));
        Assert.Contains("DSharp.Runtime", await File.ReadAllTextAsync(Path.Combine(goodDirectory, "Generated.csproj")));
        var ordinarySource = Path.Combine(directory, "ordinary.ds");
        await File.WriteAllTextAsync(ordinarySource, "module Ordinary\nfn answer(): int { 42 }");
        var ordinaryDir = Path.Combine(directory, "ordinary-output");
        var ordinary = await Run(root, null, cli, "compile", ordinarySource, ordinaryDir);
        Assert.True(ordinary.ExitCode == 0, $"Ordinary CLI compile failed: {ordinary.Output}");
        Assert.Equal("<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net11.0</TargetFramework></PropertyGroup></Project>\n", await File.ReadAllTextAsync(Path.Combine(ordinaryDir, "Generated.csproj")));
    }

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "DSharp.slnx"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }

    private static string RuntimeAssembly(string root)
    {
        var path = Path.Combine(root, "src/DSharp.Runtime/bin/Release/net11.0/DSharp.Runtime.dll");
        Assert.True(File.Exists(path), $"Built runtime ABI missing: {path}");
        return path;
    }

    private static async Task<(int ExitCode, string Output)> Run(string workingDirectory, string? runtime, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = workingDirectory, RedirectStandardOutput = true, RedirectStandardError = true };
        start.Environment.Remove("DSHARP_RUNTIME_ASSEMBLY");
        if (runtime is not null) start.Environment["DSHARP_RUNTIME_ASSEMBLY"] = runtime;
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated Invoke test process timed out."); }
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
                var method = delegateType.GetMethod("Invoke")!;
                var value = Expression.Parameter(typeof(int), "value");
                var make = typeof(Program).GetMethod(nameof(Message), BindingFlags.Static | BindingFlags.NonPublic)!;
                var call = Expression.Call(make, Expression.Constant(method.ReturnType), value);
                return Expression.Lambda(delegateType, Expression.Convert(call, method.ReturnType), value).Compile();
            }
            private static void Require(bool value, string message)
            {
                if (!value) throw new Exception(message);
            }
            private static async Task<object?> Result(Task task)
            {
                await task.WaitAsync(TimeSpan.FromSeconds(10));
                return task.GetType().GetProperty("Result")!.GetValue(task);
            }
            private static async Task Main()
            {
                var assembly = typeof(Program).Assembly;
                var app = assembly.GetTypes().Single(t => t.Name == "App");
                MethodInfo Entry(string name) => app.GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy) ?? throw new Exception("missing entry " + name);
                Task Start(string name, Fake host)
                {
                    var entry = Entry(name);
                    var args = entry.GetParameters();
                    Require(args.Length == 2 && args[0].ParameterType == typeof(IActorDispatcher), "dispatcher must be explicit first argument");
                    return (Task)entry.Invoke(null, new object?[] { host, Factory(args[1].ParameterType) })!;
                }
                var firstHost = new Fake();
                var secondHost = new Fake();
                var first = Start("first", firstHost);
                var same = Start("same", secondHost);
                Require(evaluations == 2, "each argument evaluated exactly once");
                Require(firstHost.Calls.Count == 1 && secondHost.Calls.Count == 1, "one call per host");
                Require((int)firstHost.Calls[0].Message!.GetType().GetProperty("value")!.GetValue(firstHost.Calls[0].Message)! == 7, "first message must be forwarded");
                Require((int)secondHost.Calls[0].Message!.GetType().GetProperty("value")!.GetValue(secondHost.Calls[0].Message)! == 8, "second message must be forwarded");
                Require(ReferenceEquals(firstHost.Calls[0].Route, secondHost.Calls[0].Route), "same canonical target shares route");
                Require(!first.IsCompleted && !same.IsCompleted, "awaits must preserve pending tasks");
                secondHost.Calls[0].Complete(23);
                Require((int)(await Result(same))! == 23 && !first.IsCompleted, "second host completion isolated");
                firstHost.Calls[0].Complete(17);
                Require((int)(await Result(first))! == 17, "first host gets its own result");
                var other = Start("other", firstHost);
                Require(!ReferenceEquals(firstHost.Calls[0].Route, firstHost.Calls[1].Route), "same-named actors in distinct modules need different routes");
                firstHost.Calls[1].Complete(29);
                Require((int)(await Result(other))! == 29, "other actor result");
                var empty = Start("empty", firstHost);
                var emptyCall = firstHost.Calls[2];
                emptyCall.Complete(Activator.CreateInstance(emptyCall.ResultType));
                Require(await Result(empty) is not null, "Unit value should survive Invoke");
                var failed = Start("failed", firstHost);
                var denied = app.GetMethod("denied", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.Invoke(null, null);
                firstHost.Calls[3].Complete(denied);
                var response = await Result(failed);
                var isDenied = app.GetMethod("isDenied", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
                Require((bool)isDenied.Invoke(null, new[] { response })!, "business error must remain the host's result");
                Require(evaluations == 5, "all argument expressions run once");
                Console.WriteLine("host checks passed");
            }
        }
        sealed class Fake : IActorDispatcher
        {
            public readonly List<Call> Calls = new();
            public Task<TResult> Invoke<TMessage, TResult>(ActorRoute<TMessage, TResult> route, TMessage message)
            {
                var completion = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                Calls.Add(new Call(route, message, typeof(TResult), value => completion.SetResult((TResult)value!)));
                return completion.Task;
            }
            public Task<InvocationReceipt> Dispatch<TMessage, TResult>(ActorRoute<TMessage, TResult> route, TMessage message) => throw new Exception("unexpected Dispatch");
            public Task<PublicationReceipt> Publish<TMessage>(MessageContract<TMessage> contract, TMessage message) => throw new Exception("unexpected Publish");
        }
        sealed record Call(object Route, object? Message, Type ResultType, Action<object?> Complete);
        """;
}
