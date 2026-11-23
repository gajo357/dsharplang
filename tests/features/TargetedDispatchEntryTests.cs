using System.Diagnostics;
using System.Security;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class TargetedDispatchEntryTests
{
    private static readonly SourceFile[] Sources =
    [
        new("contracts.ds", "module Contracts\ntype Request { value: int }"),
        new("alpha.ds", """
            module Alpha
            import Contracts
            actor Worker { Handle(message: Request): Result<int, string> { Error("actor-body") } }
            """),
        new("beta.ds", "module Beta\nimport Contracts\nactor Worker { Handle(message: Request): int { 2000 } }"),
        new("app-a.ds", """
            module App
            import Contracts
            import Alpha
            async fn send(next: (int) -> Request) { await Dispatch<Worker>(next(7)) }
            async fn sendAgain(next: (int) -> Request) { await Dispatch<Alpha.Worker>(next(8)) }
            async fn ask(next: (int) -> Request): Result<int, string> { await Invoke<Worker>(next(9)) }
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
            async fn other(next: (int) -> Request) { await Dispatch<Beta.Worker>(next(10)) }
            """)
    ];

    [Fact]
    public async Task TargetedDispatchUsesTheSharedScopedRouteAndOpaqueReceipt()
    {
        var compiled = DSharpCompiler.Compile(Sources);
        Assert.True(compiled.CSharp is not null && compiled.Diagnostics.Count == 0,
            $"Expected typed targeted Dispatch entries: {string.Join("; ", compiled.Diagnostics)}");
        Assert.True(compiled.RequiresRuntime);
        var root = RepositoryRoot();
        var runtime = RuntimeAssembly(root);
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-dispatch-host-" + Guid.NewGuid().ToString("N"));
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
            Assert.True(build.ExitCode == 0, $"Generated Dispatch host build failed: {build.Output}");
            var host = await Run(directory, null, Path.Combine(directory, "bin/Release/net11.0/Generated.dll"));
            Assert.True(host.ExitCode == 0, $"Generated Dispatch host failed: {host.Output}");
            Assert.Contains("dispatch host checks passed", host.Output);
            CheckUnsupportedForms();
            await CheckCli(root, runtime, directory);
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void CheckUnsupportedForms()
    {
        const string fieldName = "module Demo\ntype Request { Dispatch: int }\nactor Worker { Handle(message: Request): int { message.Dispatch } }\nasync fn run() { await Dispatch<Worker>(Request { Dispatch = 1 }) }";
        var fieldNameResult = DSharpCompiler.Compile(fieldName, "dispatch-field.ds");
        Assert.True(fieldNameResult.CSharp is not null && fieldNameResult.Diagnostics.Count == 0 && fieldNameResult.RequiresRuntime,
            $"A field named Dispatch must not shadow the dispatcher operation: {string.Join("; ", fieldNameResult.Diagnostics)}");
        const string declarations = "module Demo\ntype Request { value: int }\nactor Worker { Handle(message: Request): int { message.value } }\n";
        string[] invalid =
        [
            "async fn use() { await Dispatch<Missing>(Request { value = 1 }) }",
            "async fn use() { await Dispatch<Worker>(1) }",
            "async fn use() { await Dispatch<Worker, Worker>(Request { value = 1 }) }",
            "async fn use(): Unit { let pending = Dispatch<Worker>(Request { value = 1 })\n () }",
            "fn use(): Unit { let pending = Dispatch<Worker>(Request { value = 1 })\n () }",
            "async fn use(): Unit { let callback = async () => { await Dispatch<Worker>(Request { value = 1 }) }\n await callback() }",
            "async fn use(): Unit { await (async () => { await Dispatch<Worker>(Request { value = 1 }) })() }",
            "actor Caller { Handle(message: Request): Unit { let ignored = await Dispatch<Worker>(message)\n () } }",
            "actor Caller { async helper(message: Request): Unit { let ignored = await Dispatch<Worker>(message)\n () } Handle(message: Request): int { 1 } }",
            "extend Request { async fn send(self): Unit { let ignored = await Dispatch<Worker>(self)\n () } }",
            "async fn entry(): Unit { let receipt = await Dispatch<Worker>(Request { value = 1 })\n receipt }",
            "async fn entry(): Unit { let receipt = await Dispatch<Worker>(Request { value = 1 })\n () }",
            "async fn entry(): bool { (await Dispatch<Worker>(Request { value = 1 })) == (await Dispatch<Worker>(Request { value = 1 })) }",
            "type InvocationReceipt { value: int }\nasync fn entry(): InvocationReceipt { await Dispatch<Worker>(Request { value = 1 }) }"
        ];
        foreach (var scenario in invalid)
        {
            var result = DSharpCompiler.Compile(declarations + scenario, "invalid-dispatch.ds");
            Assert.True(result.CSharp is null && result.Diagnostics.Any(d => d.Code == "DS2048" && d.Path == "invalid-dispatch.ds"),
                $"Expected invalid Dispatch rejection: {scenario}; diagnostics: {string.Join("; ", result.Diagnostics)}");
        }
        string[] entryEscapes =
        [
            "async fn entry() { await Dispatch<Worker>(Request { value = 1 }) }\nasync fn caller(): Unit { let receipt = await entry()\n () }",
            "async fn entry(value: int) { await Dispatch<Worker>(Request { value = value }) }\nfn leak(): Unit { let callback = entry\n () }",
            "async fn entry(first: int, second: int) { await Dispatch<Worker>(Request { value = first }) }\nfn leak(): Unit { let callback = entry(1)\n () }"
        ];
        foreach (var scenario in entryEscapes)
        {
            var result = DSharpCompiler.Compile(declarations + scenario, "escaped-dispatch.ds");
            Assert.True(result.CSharp is null && result.Diagnostics.Any(d => d.Code == "DS2047" && d.Path == "escaped-dispatch.ds"),
                $"Expected effectful entry escape rejection: {scenario}; diagnostics: {string.Join("; ", result.Diagnostics)}");
        }
        var receiptAnnotation = DSharpCompiler.Compile(declarations + "async fn entry(): InvocationReceipt { await Dispatch<Worker>(Request { value = 1 }) }", "receipt-annotation.ds");
        Assert.True(receiptAnnotation.CSharp is null && receiptAnnotation.Diagnostics.Any(d => d.Code == "DS2003" && d.Path == "receipt-annotation.ds"),
            $"Expected inaccessible receipt annotation rejection: {string.Join("; ", receiptAnnotation.Diagnostics)}");
        var receiptField = DSharpCompiler.Compile(declarations + "type Box { receipt: InvocationReceipt }", "receipt-field.ds");
        Assert.True(receiptField.CSharp is null && receiptField.Diagnostics.Any(d => d.Code == "DS2015" && d.Path == "receipt-field.ds"),
            $"Expected inaccessible receipt field rejection: {string.Join("; ", receiptField.Diagnostics)}");
        const string forgedSource = "module Demo\nfn id(value: int): int { value }";
        var forgedSyntax = new Parser(Lexer.Lex(forgedSource, "forged-receipt.ds"), "forged-receipt.ds").Parse();
        var function = Assert.Single(forgedSyntax.Functions);
        var parameter = Assert.Single(function.Parameters);
        var forgedParameter = parameter with { Type = parameter.Type with { Text = "$8" } };
        var forgedReturn = function.ReturnType with { Text = "$8" };
        var forgedFunction = function with { Parameters = [forgedParameter], ReturnType = forgedReturn };
        var forgedModule = forgedSyntax with { Functions = [forgedFunction] };
        var forgedError = Assert.Throws<SourceError>(() => DSharp.Compiler.Binder.Bind(forgedModule, "forged-receipt.ds"));
        Assert.Equal("DS2048", forgedError.Diagnostic.Code);
        Assert.Equal("forged-receipt.ds", forgedError.Diagnostic.Path);
        Assert.True(forgedError.Diagnostic.Span == forgedParameter.Type.Span || forgedError.Diagnostic.Span == forgedReturn.Span,
            "A forged internal receipt must be rejected at one of the forged type tokens.");
        var ordinary = DSharpCompiler.Compile("module Demo\nfn Dispatch<T>(value: T): T { value }\nfn ordinary(): int { Dispatch<int>(7) }", "shadow-dispatch.ds");
        Assert.Empty(ordinary.Diagnostics);
        Assert.False(ordinary.RequiresRuntime);
        var local = DSharpCompiler.Compile("module Demo\nfn ordinary(): int { let Dispatch: (int) -> int = (value: int) => value\n Dispatch(7) }", "local-dispatch.ds");
        Assert.Empty(local.Diagnostics);
        Assert.False(local.RequiresRuntime);
        const string forgedShadow = "module Demo\nasync fn run() { let Dispatch: (int) -> int = (value: int) => value\n await Dispatch<int>(1) }";
        var shadowSyntax = new Parser(Lexer.Lex(forgedShadow, "forged-shadow.ds"), "forged-shadow.ds").Parse();
        var shadowFunction = Assert.Single(shadowSyntax.Functions);
        var shadowBody = Assert.IsType<BlockSyntax>(shadowFunction.Body);
        Assert.Contains(shadowBody.Statements, statement => statement is LetStatementSyntax);
        var statementOnlyBody = shadowBody with { Bindings = Array.Empty<LetBindingSyntax>() };
        var statementOnlyFunction = shadowFunction with { Body = statementOnlyBody, Bindings = Array.Empty<LetBindingSyntax>() };
        var statementOnlyModule = shadowSyntax with { Functions = [statementOnlyFunction] };
        var statementOnlyError = Assert.Throws<SourceError>(() => DSharp.Compiler.Binder.Bind(statementOnlyModule, "forged-shadow.ds"));
        Assert.Equal("DS2027", statementOnlyError.Diagnostic.Code);
        Assert.Equal("forged-shadow.ds", statementOnlyError.Diagnostic.Path);
        var homonym = DSharpCompiler.Compile("module Demo\ntype InvocationReceipt { value: int }\nfn ordinary(): InvocationReceipt { InvocationReceipt { value = 7 } }", "receipt-homonym.ds");
        Assert.Empty(homonym.Diagnostics);
        Assert.False(homonym.RequiresRuntime);

        AssertOrdinaryNominalLookup();

        SourceFile[] ambiguous =
        [
            new("a.ds", "module A\nactor Worker { Handle(message: Unit): int { 1 } }"),
            new("b.ds", "module B\nactor Worker { Handle(message: Unit): int { 2 } }"),
            new("use.ds", "module Use\nimport A\nimport B\nasync fn run() { await Dispatch<Worker>(()) }")
        ];
        var ambiguity = DSharpCompiler.Compile(ambiguous);
        Assert.True(ambiguity.CSharp is null && ambiguity.Diagnostics.Any(d => d.Code == "DS2031" && d.Path == "use.ds"),
            $"Expected ambiguous Dispatch target rejection: {string.Join("; ", ambiguity.Diagnostics)}");
        SourceFile[] forbidden =
        [
            new("actor.ds", "module Actors\nactor Worker { Handle(message: Unit): int { 1 } }"),
            new("entry.ds", "module Entries\nimport Actors\nasync fn entry() { await Dispatch<Worker>(()) }"),
            new("caller.ds", "module Callers\nimport Entries\nasync fn caller(): Unit { let receipt = await Entries.entry()\n () }")
        ];
        var direct = DSharpCompiler.Compile(forbidden);
        Assert.True(direct.CSharp is null && direct.Diagnostics.Any(d => d.Code == "DS2047" && d.Path == "caller.ds"),
            $"Expected qualified Dispatch entry call rejection: {string.Join("; ", direct.Diagnostics)}");
        forbidden[2] = new("caller.ds", "module Callers\nimport Entries\nfn leak(): Unit { let callback = Entries.entry\n () }");
        var captured = DSharpCompiler.Compile(forbidden);
        Assert.True(captured.CSharp is null && captured.Diagnostics.Any(d => d.Code == "DS2047" && d.Path == "caller.ds"),
            $"Expected qualified Dispatch entry capture rejection: {string.Join("; ", captured.Diagnostics)}");

        const string callback = """
            module Demo
            type Raw { value: int }
            type Request { value: int }
            type Reply { code: int }
            actor Target { Handle(raw: Raw): int { raw.value } }
            async fn decode(raw: Raw) { await Dispatch<Target>(raw) }
            fn mapBinding(error: string): Reply { Reply { code = 400 } }
            fn mapValue(value: int): Reply { Reply { code = value } }
            actor Worker { trigger Http<Reply>(decode, mapBinding, mapValue) Handle(message: Request): int { message.value } }
            """;
        var forbiddenHttp = DSharpCompiler.Compile(callback, "http-dispatch.ds");
        Assert.True(forbiddenHttp.CSharp is null && forbiddenHttp.Diagnostics.Any(d => d.Code == "DS2046" && d.Path == "http-dispatch.ds" && d.Span.Start == callback.LastIndexOf("decode,", StringComparison.Ordinal)),
            $"Expected receipt-returning HTTP binder rejection: {string.Join("; ", forbiddenHttp.Diagnostics)}");
    }

    private static void AssertOrdinaryNominalLookup()
    {
        (string Label, string Template)[] declarations =
        [
            ("record", "type NAME { value: int }"),
            ("union", "type NAME { | Mark }"),
            ("interface", "interface NAME { read(): int }"),
            ("actor", "actor NAME { Handle(message: Unit): int { 1 } }")
        ];
        foreach (var operation in new[] { "Dispatch", "Invoke" })
        foreach (var (label, template) in declarations)
        foreach (var imported in new[] { false, true })
        {
            var declaration = template.Replace("NAME", operation, StringComparison.Ordinal);
            var library = $"module {(imported ? "Library" : "Use")}\n{declaration}";
            var imports = imported ? "import Library\n" : "";
            var expression = operation == "Dispatch" ?
                "async fn run() { await Dispatch<Worker>(()) }" :
                "async fn run(): int { await Invoke<Worker>(()) }";
            var source = $"module Use\n{imports}actor Worker {{ Handle(message: Unit): int {{ 1 }} }}\n{expression}";
            SourceFile[] sources = [new("nominal.ds", library), new("use.ds", source)];
            var result = DSharpCompiler.Compile(sources);
            Assert.True(result.CSharp is null && result.Diagnostics.Any(d => d.Path == "use.ds") && !result.RequiresRuntime,
                $"Expected visible {label} {operation} ({(imported ? "imported" : "same module")}) to retain ordinary lookup: {string.Join("; ", result.Diagnostics)}");
            if (operation == "Dispatch")
                Assert.Contains(result.Diagnostics, d => d.Code == "DS2027" && d.Path == "use.ds");
            else
                Assert.DoesNotContain(result.Diagnostics, d => d.Code == "DS2047");

            sources[1] = new("use.ds", $"module Use\n{imports}actor Worker {{ Handle(message: Unit): int {{ 1 }} }}\nfn control(): int {{ 1 }}");
            var control = DSharpCompiler.Compile(sources);
            Assert.True(control.CSharp is not null && control.Diagnostics.Count == 0,
                $"Expected valid {label} {operation} declaration in scope: {string.Join("; ", control.Diagnostics)}");
        }

        foreach (var operation in new[] { "Dispatch", "Invoke" })
        {
            var expression = operation == "Dispatch" ?
                "async fn run() { await Dispatch<Worker>(()) }" :
                "async fn run(): int { await Invoke<Worker>(()) }";
            var use = $"module Use\nimport A\nimport B\nactor Worker {{ Handle(message: Unit): int {{ 1 }} }}\n{expression}";
            SourceFile[] ambiguous =
            [
                new("a.ds", $"module A\ntype {operation} {{ value: int }}"),
                new("b.ds", $"module B\ntype {operation} {{ | Mark }}"),
                new("use.ds", use)
            ];
            var result = DSharpCompiler.Compile(ambiguous);
            Assert.True(result.CSharp is null && result.Diagnostics.Any(d => d.Code == "DS2031" && d.Path == "use.ds"),
                $"Expected ambiguous imported {operation} nominal lookup: {string.Join("; ", result.Diagnostics)}");

            SourceFile[] hidden =
            [
                new("hidden.ds", $"module Hidden\ntype {operation} {{ value: int }}"),
                new("use.ds", $"module Use\nactor Worker {{ Handle(message: Unit): int {{ 1 }} }}\n{expression}")
            ];
            var unshadowed = DSharpCompiler.Compile(hidden);
            Assert.True(unshadowed.CSharp is not null && unshadowed.Diagnostics.Count == 0 && unshadowed.RequiresRuntime,
                $"An unimported {operation} nominal must not shadow the contextual operation: {string.Join("; ", unshadowed.Diagnostics)}");
        }
    }

    private static async Task CheckCli(string root, string runtime, string directory)
    {
        var cli = Path.Combine(root, "src/DSharp.Cli/bin/Release/net11.0/dsharp.dll");
        Assert.True(File.Exists(cli), $"Built CLI missing: {cli}");
        var source = Path.Combine(directory, "cli-dispatch.ds");
        await File.WriteAllTextAsync(source, "module Demo\nactor Worker { Handle(message: Unit): int { 1 } }\nasync fn send() { await Dispatch<Worker>(()) }");
        var check = await Run(root, null, cli, "check", source);
        Assert.True(check.ExitCode == 0, $"CLI check should not need runtime ABI: {check.Output}");
        var absentDirectory = Path.Combine(directory, "missing-runtime-output");
        var absent = await Run(root, null, cli, "compile", source, absentDirectory);
        Assert.True(absent.ExitCode == 1 && absent.Output.Contains("DS0001:") && !Directory.Exists(absentDirectory),
            $"CLI must reject missing runtime ABI before output: {absent.Output}");
        var output = Path.Combine(directory, "cli-output");
        var compiled = await Run(root, runtime, cli, "compile", source, output);
        Assert.True(compiled.ExitCode == 0, $"CLI compile with runtime ABI failed: {compiled.Output}");
        Assert.True(File.Exists(Path.Combine(output, "DSharp.Runtime.dll")));
        Assert.Contains("DSharp.Runtime", await File.ReadAllTextAsync(Path.Combine(output, "Generated.csproj")));
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
        catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated Dispatch test process timed out."); }
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
                var app = typeof(Program).Assembly.GetTypes().Single(t => t.Name == "App");
                MethodInfo Entry(string name) => app.GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy) ?? throw new Exception("missing entry " + name);
                Task Start(string name, Fake host)
                {
                    var entry = Entry(name);
                    var args = entry.GetParameters();
                    Require(args.Length == 2 && args[0].ParameterType == typeof(IActorDispatcher), "host dispatcher must be first argument");
                    return (Task)entry.Invoke(null, new object?[] { host, Factory(args[1].ParameterType) })!;
                }
                var firstHost = new Fake();
                var secondHost = new Fake();
                var first = Start("send", firstHost);
                var second = Start("sendAgain", secondHost);
                var invoke = Start("ask", firstHost);
                Require(evaluations == 3, "each message argument must be evaluated once");
                Require(firstHost.Dispatches.Count == 1 && secondHost.Dispatches.Count == 1 && firstHost.Invocations.Count == 1, "targeted operations called exactly once");
                var firstCall = firstHost.Dispatches[0];
                var secondCall = secondHost.Dispatches[0];
                Require(ReferenceEquals(firstCall.Route, secondCall.Route) && ReferenceEquals(firstCall.Route, firstHost.Invocations[0].Route), "Invoke and Dispatch must share the canonical target route");
                Require((int)firstCall.Message!.GetType().GetProperty("value")!.GetValue(firstCall.Message)! == 7, "first Dispatch message forwarded");
                Require((int)secondCall.Message!.GetType().GetProperty("value")!.GetValue(secondCall.Message)! == 8, "second Dispatch message forwarded");
                Require(!first.IsCompleted && !second.IsCompleted && !invoke.IsCompleted, "entries remain pending until the fake completes them");
                var accepted = new InvocationReceipt();
                secondCall.Completion.SetResult(accepted);
                Require(ReferenceEquals(accepted, await Result(second)), "Dispatch must return the exact acceptance receipt");
                Require(!first.IsCompleted && !invoke.IsCompleted, "one host's acceptance cannot complete another call");
                var denied = app.GetMethod("denied", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.Invoke(null, null);
                firstHost.Invocations[0].Complete(denied);
                var business = await Result(invoke);
                var isDenied = app.GetMethod("isDenied", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
                Require((bool)isDenied.Invoke(null, new[] { business })!, "Invoke keeps the declared business result");
                Require(!first.IsCompleted, "Invoke result cannot stand in for acceptance");
                var acceptedFirst = new InvocationReceipt();
                firstCall.Completion.SetResult(acceptedFirst);
                Require(ReferenceEquals(acceptedFirst, await Result(first)), "first host gets its own receipt");
                var other = Start("other", firstHost);
                Require(!ReferenceEquals(firstCall.Route, firstHost.Dispatches[1].Route), "same actor name in another module needs a distinct route");
                var otherReceipt = new InvocationReceipt();
                firstHost.Dispatches[1].Completion.SetResult(otherReceipt);
                Require(ReferenceEquals(otherReceipt, await Result(other)), "other target receipt");
                Require(evaluations == 4, "every message argument evaluated once");
                Console.WriteLine("dispatch host checks passed");
            }
        }
        sealed class Fake : IActorDispatcher
        {
            public readonly List<TargetCall> Dispatches = new();
            public readonly List<InvokeCall> Invocations = new();
            public Task<TResult> Invoke<TMessage, TResult>(ActorRoute<TMessage, TResult> route, TMessage message)
            {
                var completion = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                Invocations.Add(new InvokeCall(route, message, value => completion.SetResult((TResult)value!)));
                return completion.Task;
            }
            public Task<InvocationReceipt> Dispatch<TMessage, TResult>(ActorRoute<TMessage, TResult> route, TMessage message)
            {
                var completion = new TaskCompletionSource<InvocationReceipt>(TaskCreationOptions.RunContinuationsAsynchronously);
                Dispatches.Add(new TargetCall(route, message, completion));
                return completion.Task;
            }
            public Task<PublicationReceipt> Publish<TMessage>(MessageContract<TMessage> contract, TMessage message) => throw new Exception("unexpected Publish");
        }
        sealed record TargetCall(object Route, object? Message, TaskCompletionSource<InvocationReceipt> Completion);
        sealed record InvokeCall(object Route, object? Message, Action<object?> Complete);
        """;
}
