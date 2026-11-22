using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using DSharp.Compiler;
using CompilerBinder = DSharp.Compiler.Binder;
using Xunit;

namespace DSharp.Tests;

public sealed class ActorContractTests
{
    private const string Message = "type Request { value: int }";

    [Fact]
    public void ActorDeclarationAndMetadataContracts()
    {
        OneTypedHandleIsAcceptedWithoutAnExplicitAsyncMarker();
        InvalidHandleContractsAreRejected();
        UserActorNameCannotCollideWithSyntheticResourceContract();
        ActorMessageTypesResolveAcrossSourceFiles();
        ActorIdentityAndDuplicateNamesRespectModuleBoundaries();
        ActorCannotShadowUnit();
    }

    private void ActorCannotShadowUnit()
    {
        const string declaration = "actor Unit { Handle(message: Unit): Unit { () } }";
        var single = DSharpCompiler.Compile("module Demo\n" + declaration, "unit-actor.ds");
        Assert.Null(single.CSharp);
        Assert.Contains(single.Diagnostics, diagnostic => diagnostic.Code == "DS2042" && diagnostic.Path == "unit-actor.ds");
        var multi = DSharpCompiler.Compile(new[]
        {
            new SourceFile("types.ds", "module Types\ntype Request { value: int }"),
            new SourceFile("unit-actor.ds", "module Workers\n" + declaration)
        });
        Assert.Null(multi.CSharp);
        Assert.Contains(multi.Diagnostics, diagnostic => diagnostic.Code == "DS2042" && diagnostic.Path == "unit-actor.ds");
    }

    [Fact]
    public void ActorInstancesAndHandleCannotBypassDispatch()
    {
        ActorsCannotBeConstructedInjectedOrCalledAsValues();
        ActorBodiesCannotBypassDispatchByCallingHandle();
        QualifiedActorConstructionCannotBypassTheDispatcher();
    }

    [Fact]
    public void ActorDependencyOwnershipAndAsyncLifetimeAreEnforced()
    {
        BorrowedActorResourceCannotBeReleasedOrTransferred();
        ActorCanReadFromAnInterfaceDependencyWithoutOwningIt();
        ActorMayDirectlyAwaitAnAsyncHelperThatBorrowsItsDependency();
        AsyncActorHelperCannotLetItsResourceBorrowOutliveTheCall();
        ActorCannotDisposeOrEscapeAnInterfaceDependency();
        ActorHelperCannotUseABorrowedInterfaceToReleaseHostResource();
        ImportedInterfaceDependencyCannotEscapeOrRelease();
    }

    [Fact]
    public async Task ActorBodiesExecuteWithHostOwnedResources()
    {
        await ActorCanUsePrivateHelperAndDeclaredResource();
        ActorHandleMayAwaitAnAsyncResourceWithoutAnAsyncModifier();
        await GeneratedInternalActorBodiesExecuteValueUnitResultAndAwait();
    }

    private void OneTypedHandleIsAcceptedWithoutAnExplicitAsyncMarker()
    {
        const string source = """
            module Actors
            type Request { value: int }
            actor Echo {
                Handle(message: Request): int { message.value }
            }
            actor Nothing {
                Handle(message: Unit): Unit { () }
            }
            actor Outcome {
                Handle(message: Request): Result<int, string> { Error("declined") }
            }
            """;

        var syntax = new Parser(Lexer.Lex(source, "actors.ds"), "actors.ds").Parse();
        Assert.Equal(new[] { "Echo", "Nothing", "Outcome" }, syntax.Actors.Select(actor => actor.Name.Text));
        foreach (var actor in syntax.Actors)
        {
            var handle = Assert.Single(actor.Methods, method => method.Name.Text == "Handle");
            Assert.Single(handle.Parameters);
        }

        var bound = CompilerBinder.Bind(syntax, "actors.ds");
        var echo = Assert.Single(bound.Actors, actor => actor.Name == "Echo");
        Assert.Equal("Request", echo.InputType);
        Assert.Equal("int", echo.ResultType);
        Assert.Empty(echo.Dependencies);
        Assert.Equal("actors.ds", echo.SourcePath);
        Assert.Equal(source.IndexOf("Echo", StringComparison.Ordinal), echo.NameSpan.Start);
        Assert.Equal(source.IndexOf("Handle", StringComparison.Ordinal), echo.HandleSpan.Start);
        Assert.False(string.IsNullOrWhiteSpace(echo.ImplementationName));
        var nothing = Assert.Single(bound.Actors, actor => actor.Name == "Nothing");
        Assert.Equal("Unit", nothing.InputType);
        Assert.Equal("Unit", nothing.ResultType);

        var compiled = DSharpCompiler.Compile(source, "actors.ds");
        Assert.Empty(compiled.Diagnostics);
        Assert.NotNull(compiled.CSharp);
    }

    private void InvalidHandleContractsAreRejected()
    {
        string[] cases =
        [
            "actor Worker { helper(value: int): int { value } }",
            "actor Worker { Handle(message: Request): int { 1 } Handle(other: Request): int { 2 } }",
            "actor Worker { Handle(): int { 1 } }",
            "actor Worker { Handle(left: Request, right: Request): int { 1 } }",
            "actor Worker { Handle(message): int { 1 } }",
            "actor Worker { Handle(message: Request) { 1 } }",
            "actor Worker { Handle(message: Request): int { \"not an integer\" } }",
        ];
        foreach (var declaration in cases)
        {
            var result = DSharpCompiler.Compile($"module Demo\n{Message}\n{declaration}", "invalid-actor.ds");
            Assert.True(result.CSharp is null && result.Diagnostics.Count > 0,
                $"Expected an actor diagnostic for: {declaration}");
        }
    }

    private void ActorsCannotBeConstructedInjectedOrCalledAsValues()
    {
        string[] cases =
        [
            "fn bad(): Unit { Worker() }",
            "fn bad(worker: Worker): Unit { () }",
            "type Box { value: Worker }",
            "type Box<T> { value: T }\nfn bad(worker: Box<Worker>): Unit { () }",
            "fn bad(workers: List<Worker>): Unit { () }",
            "fn bad(worker: Option<Worker>): Unit { () }",
            "type Wrapped { | Item(Worker) }",
            "fn bad(): Worker { Worker() }",
            "actor Other(peer: Worker) { Handle(message: Request): Unit { () } }",
            "fn bad(): int { Worker().Handle(Request { value: 1 }) }",
            "fn bad(): int { let callback = Worker().Handle\n 42 }",
        ];
        foreach (var declaration in cases)
        {
            var result = DSharpCompiler.Compile($"module Demo\n{Message}\nactor Worker {{ Handle(message: Request): int {{ message.value }} }}\n{declaration}", "actor-bypass.ds");
            Assert.True(result.CSharp is null && result.Diagnostics.Count > 0,
                $"Expected an actor diagnostic for: {declaration}");
        }
    }

    private void ActorBodiesCannotBypassDispatchByCallingHandle()
    {
        string[] cases =
        [
            "Handle(message)",
            "let callback = Handle\n callback(message)",
            "Other().Handle(message)",
        ];
        foreach (var expression in cases)
        {
            var source = $$"""
                module Demo
                {{Message}}
                actor Other { Handle(message: Request): int { message.value } }
                actor Worker { Handle(message: Request): int { {{expression}} } }
                """;
            var result = DSharpCompiler.Compile(source, "actor-self-bypass.ds");
            Assert.True(result.CSharp is null && result.Diagnostics.Count > 0,
                $"Expected an actor diagnostic for: {expression}");
        }
    }

    private void BorrowedActorResourceCannotBeReleasedOrTransferred()
    {
        string[] cases =
        [
            "reader.release()",
            "let alias = reader\n alias.release()",
            "using alias = reader { alias.read() }",
        ];
        foreach (var expression in cases)
        {
            var source = $$"""
                module Demo
                {{Message}}
                resource Reader { read(): int { 42 } release(): Unit { () } }
                actor Worker(reader: Reader) { Handle(message: Request): int { {{expression}}
                    42 } }
                """;
            var result = DSharpCompiler.Compile(source, "actor-owned-resource.ds");
            Assert.True(result.CSharp is null && result.Diagnostics.Count > 0,
                $"Expected an actor diagnostic for: {expression}");
        }
    }

    private void ActorCanReadFromAnInterfaceDependencyWithoutOwningIt()
    {
        const string source = """
            module Demo
            type Request { value: int }
            interface Store { read(): int
                              release(): Unit }
            actor Worker(store: Store) {
                Handle(message: Request): int { store.read() }
            }
            """;
        var result = DSharpCompiler.Compile(source, "actor-interface-dependency.ds");
        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.CSharp);
        var actor = Assert.Single(result.Actors);
        var dependency = Assert.Single(actor.Dependencies);
        Assert.Equal("store", dependency.Name);
        Assert.Equal("Store", dependency.Type);
    }

    private void ActorMayDirectlyAwaitAnAsyncHelperThatBorrowsItsDependency()
    {
        const string source = """
            module Demo
            type Request { value: int }
            interface Store { read(): int }
            actor Worker(store: Store) {
                async helper(value: int): int { value + store.read() }
                Handle(message: Request): int { await helper(message.value) }
            }
            """;
        var result = DSharpCompiler.Compile(source, "actor-async-helper.ds");
        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.CSharp);
    }

    private void AsyncActorHelperCannotLetItsResourceBorrowOutliveTheCall()
    {
        string[] cases =
        [
            "let pending = helper(message.value)\n await pending",
            "let pending: Task<int> = helper(message.value)\n 42",
        ];
        foreach (var body in cases)
        {
            var source = $$"""
                module Demo
                {{Message}}
                interface Store { read(): int }
                actor Worker(store: Store) {
                    async helper(value: int): int { value + store.read() }
                    Handle(message: Request): int { {{body}} }
                }
                """;
            var result = DSharpCompiler.Compile(source, "actor-async-helper-escape.ds");
            Assert.True(result.CSharp is null && result.Diagnostics.Count > 0,
                $"Expected an actor diagnostic for: {body}");
        }
    }

    private void ActorCannotDisposeOrEscapeAnInterfaceDependency()
    {
        string[] cases =
        [
            "store.release()",
            "let alias = store\n alias.release()",
            "let cleanup: () -> Unit = store.release\n cleanup()",
            "releaseStore(store)",
            "let callback: () -> Unit = () => store.release()\n callback()",
            "let escaped = keepStore(store)\n escaped.release()",
        ];
        foreach (var expression in cases)
        {
            var source = $$"""
                module Demo
                {{Message}}
                interface Store { read(): int
                                  release(): Unit }
                fn releaseStore(value: Store): Unit { value.release() }
                fn keepStore(value: Store): Store { value }
                actor Worker(store: Store) { Handle(message: Request): int { {{expression}}
                    42 } }
                """;
            var result = DSharpCompiler.Compile(source, "actor-interface-ownership.ds");
            Assert.True(result.CSharp is null && result.Diagnostics.Count > 0,
                $"Expected an actor diagnostic for: {expression}");
        }
    }

    private void ActorHelperCannotUseABorrowedInterfaceToReleaseHostResource()
    {
        const string source = """
            module Demo
            type Request { value: int }
            interface Store { read(): int
                              release(): Unit }
            actor Worker(store: Store) {
                cleanup(value: Store): Unit { value.release() }
                Handle(message: Request): int {
                    cleanup(store)
                    42
                }
            }
            """;
        var result = DSharpCompiler.Compile(source, "actor-helper-ownership.ds");
        Assert.Null(result.CSharp);
        Assert.NotEmpty(result.Diagnostics);
    }

    private void ImportedInterfaceDependencyCannotEscapeOrRelease()
    {
        string[] cases =
        [
            "store.release()",
            "let cleanup: () -> Unit = store.release\n cleanup()",
        ];
        foreach (var expression in cases)
        {
            SourceFile[] sources =
            [
                new("contracts.ds", "module Contracts\ninterface Store { read(): int\n release(): Unit }"),
                new("actor.ds", $"module Workers\nimport Contracts\ntype Request {{ value: int }}\nactor Worker(store: Store) {{ Handle(message: Request): int {{ {expression}\n 42 }} }}")
            ];
            var result = DSharpCompiler.Compile(sources);
            Assert.True(result.CSharp is null && result.Diagnostics.Count > 0,
                $"Expected an actor diagnostic for: {expression}");
            Assert.All(result.Diagnostics, diagnostic => Assert.Equal("actor.ds", diagnostic.Path));
        }
    }

    private void UserActorNameCannotCollideWithSyntheticResourceContract()
    {
        const string source = """
            module Demo
            resource Reader { read(): int { 42 } release(): Unit { () } }
            actor __dsharp_resource_contract_0 {
                Handle(message: Unit): int { 42 }
            }
            """;
        var result = DSharpCompiler.Compile(source, "actor-resource-name.ds");
        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.CSharp);
        Assert.Equal("__dsharp_resource_contract_0", Assert.Single(result.Actors).Name);
    }

    private void ActorMessageTypesResolveAcrossSourceFiles()
    {
        SourceFile[] sources =
        [
            new("request.ds", "module Contracts\ntype Request { value: int }"),
            new("actor.ds", "module Workers\nimport Contracts\nactor Worker { Handle(message: Request): int { message.value } }")
        ];

        var result = DSharpCompiler.Compile(sources);
        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.CSharp);
        var actor = Assert.Single(result.Actors);
        Assert.Equal("Workers.Worker", actor.Name);
        Assert.Equal("Contracts.Request", actor.InputType);
        Assert.Equal("int", actor.ResultType);
        Assert.Equal("actor.ds", actor.SourcePath);
        Assert.Equal(sources[1].Text.IndexOf("actor Worker", StringComparison.Ordinal) + "actor ".Length, actor.NameSpan.Start);
        Assert.Equal(sources[1].Text.IndexOf("Handle", StringComparison.Ordinal), actor.HandleSpan.Start);
    }

    private void QualifiedActorConstructionCannotBypassTheDispatcher()
    {
        SourceFile[] sources =
        [
            new("workers.ds", "module Workers\ntype Request { value: int }\nactor Worker { Handle(message: Request): int { message.value } }"),
            new("app.ds", "module App\nimport Workers\nfn bad(): Unit { Workers.Worker() }")
        ];

        var result = DSharpCompiler.Compile(sources);
        Assert.Null(result.CSharp);
        Assert.NotEmpty(result.Diagnostics);
    }

    private void ActorIdentityAndDuplicateNamesRespectModuleBoundaries()
    {
        var distinct = DSharpCompiler.Compile(new[]
        {
            new SourceFile("left.ds", "module Left\nactor Worker { Handle(message: Unit): int { 1 } }"),
            new SourceFile("right.ds", "module Right\nactor Worker { Handle(message: Unit): int { 2 } }")
        });
        Assert.Empty(distinct.Diagnostics);
        Assert.Equal(new[] { "Left.Worker", "Right.Worker" }, distinct.Actors.Select(actor => actor.Name).OrderBy(name => name));
        Assert.Equal(new[] { "left.ds", "right.ds" }, distinct.Actors.Select(actor => actor.SourcePath).OrderBy(path => path));

        var collision = DSharpCompiler.Compile(new[]
        {
            new SourceFile("actor.ds", "module Shared\nactor Worker { Handle(message: Unit): int { 1 } }"),
            new SourceFile("function.ds", "module Shared\nfn Worker(): int { 1 }")
        });
        Assert.Null(collision.CSharp);
        Assert.NotEmpty(collision.Diagnostics);
    }

    private async Task ActorCanUsePrivateHelperAndDeclaredResource()
    {
        const string source = """
            module Demo
            type Request { value: int }
            interface Tracker { mark(value: int): int }
            resource Offset(tracker: Tracker) {
                read(): int { tracker.mark(2) }
                release(): Unit {
                    let ignored = tracker.mark(9)
                    ()
                }
            }
            actor Worker(offset: Offset) {
                double(value: int): int { value * 2 }
                Handle(message: Request): int { double(message.value) + offset.read() }
            }
            """;

        var result = DSharpCompiler.Compile(source, "actor-resource.ds");
        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.CSharp);
        var syntax = new Parser(Lexer.Lex(source, "actor-resource.ds"), "actor-resource.ds").Parse();
        var actor = Assert.Single(CompilerBinder.Bind(syntax, "actor-resource.ds").Actors);
        var dependency = Assert.Single(actor.Dependencies);
        Assert.Equal("offset", dependency.Name);
        Assert.Equal("Offset", dependency.Type);

        // The shim is host-side test code, compiled into the generated assembly.
        // D# callers still have no actor constructor or Handle-call path.
        var trackerInterface = Regex.Match(result.CSharp!,
            @"public interface (?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{[^}]*__dsharp_method_mark\(",
            RegexOptions.Singleline);
        Assert.True(trackerInterface.Success, "Generated tracker interface was not found.");
        var shim = $"\npublic sealed class ActorTestTracker : {trackerInterface.Groups["name"].Value} {{ public int Calls; public int __dsharp_method_mark(int value) {{ Calls = Calls * 10 + value; return value; }} }}\n";
        await WithGenerated(result.CSharp! + shim, async module =>
        {
            var implementation = Assert.Single(module.Assembly.GetTypes()
                .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)),
                method => method.Name == actor.ImplementationName);
            Assert.False(implementation.IsPublic);
            var parameters = implementation.GetParameters();
            Assert.Equal(2, parameters.Length);
            var resourceType = Assert.Single(module.Assembly.GetTypes(), type =>
                type.IsClass && !type.IsAbstract && parameters[0].ParameterType.IsAssignableFrom(type));
            var trackerType = Assert.Single(module.Assembly.GetTypes(), type => type.Name == "ActorTestTracker");
            var tracker = Activator.CreateInstance(trackerType);
            Assert.NotNull(tracker);
            var resource = Activator.CreateInstance(resourceType, tracker);
            Assert.NotNull(resource);
            var message = Activator.CreateInstance(parameters[1].ParameterType, 20);
            Assert.NotNull(message);

            var task = Assert.IsAssignableFrom<Task<int>>(implementation.Invoke(null, new[] { resource, message }));
            Assert.Equal(42, await task.WaitAsync(TimeSpan.FromSeconds(10)));
            var calls = trackerType.GetField("Calls");
            Assert.NotNull(calls);
            Assert.Equal(2, calls!.GetValue(tracker)); // Actor only used the borrowed resource.
            var release = resourceType.GetMethod("__dsharp_method_release", BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(release);
            release!.Invoke(resource, null); // The host retains cleanup ownership.
            Assert.Equal(29, calls.GetValue(tracker));
        });
    }

    private void ActorHandleMayAwaitAnAsyncResourceWithoutAnAsyncModifier()
    {
        const string source = """
            module Demo
            type Request { value: int }
            resource Reader {
                async read(): int { 42 }
                release(): Unit { () }
            }
            actor Worker(reader: Reader) {
                Handle(message: Request): int { await reader.read() }
            }
            """;

        var result = DSharpCompiler.Compile(source, "actor-await.ds");
        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.CSharp);
    }

    private async Task GeneratedInternalActorBodiesExecuteValueUnitResultAndAwait()
    {
        const string source = """
            module Demo
            async fn compute(): int { 42 }
            fn isDeclined(value: Result<int, string>): bool {
                match value {
                    Success(_) => false
                    Error(error) => error == "declined"
                }
            }
            actor Number {
                Handle(message: Unit): int { 42 }
            }
            actor Empty {
                Handle(message: Unit): Unit { () }
            }
            actor Declined {
                Handle(message: Unit): Result<int, string> { Error("declined") }
            }
            actor Waiting {
                Handle(message: Unit): int { await compute() }
            }
            """;

        var syntax = new Parser(Lexer.Lex(source, "actor-execution.ds"), "actor-execution.ds").Parse();
        var bound = CompilerBinder.Bind(syntax, "actor-execution.ds");
        var compiled = DSharpCompiler.Compile(source, "actor-execution.ds");
        Assert.Empty(compiled.Diagnostics);
        Assert.NotNull(compiled.CSharp);

        await WithGenerated(compiled.CSharp!, async module =>
        {
            async Task<object?> InvokeBody(string actorName)
            {
                var actor = Assert.Single(bound.Actors, item => item.Name == actorName);
                var method = Assert.Single(module.Assembly.GetTypes()
                    .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)),
                    candidate => candidate.Name == actor.ImplementationName);
                Assert.False(method.IsPublic);
                var parameter = Assert.Single(method.GetParameters());
                var unit = Activator.CreateInstance(parameter.ParameterType);
                Assert.NotNull(unit);
                var task = Assert.IsAssignableFrom<Task>(method.Invoke(null, new[] { unit }));
                await task.WaitAsync(TimeSpan.FromSeconds(10));
                return task.GetType().GetProperty("Result")!.GetValue(task);
            }

            Assert.Equal(42, await InvokeBody("Number"));
            Assert.NotNull(await InvokeBody("Empty"));
            Assert.Equal(42, await InvokeBody("Waiting"));
            var businessResult = await InvokeBody("Declined");
            Assert.NotNull(businessResult);
            var check = module.GetMethod("isDeclined", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            Assert.NotNull(check);
            Assert.Equal(true, check!.Invoke(null, new[] { businessResult }));
        });
    }

    private static async Task WithGenerated(string csharp, Func<Type, Task> verify)
    {
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-actor-contract-" + Guid.NewGuid());
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
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated actor build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("actor-contract-" + Guid.NewGuid(), isCollectible: true);
            try
            {
                var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0/Generated.dll"));
                var module = Assert.Single(assembly.GetTypes(), type => type.Name == "Demo");
                await verify(module);
            }
            finally { context.Unload(); }
        }
        finally { Directory.Delete(directory, true); }
    }
}
