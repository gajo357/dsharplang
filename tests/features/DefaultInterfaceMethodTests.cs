using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class DefaultInterfaceMethodTests
{
    [Fact(DisplayName = "Default interface method bodies")]
    public async Task DefaultsDispatchAreCheckedAndRespectBoundaries()
    {
        const string source = """
            module Demo
            fn read(): int { 999 }
            interface Counter {
                read(): int
                next(): int { read() + 1 }
                final(): int { next() + 1 }
            }
            service Ordinary(amount: int): Counter {
                read() { amount }
            }
            service Replace: Counter {
                read() { 999 }
                next() { 40 }
            }
            interface NullableEcho { echo(value: int?): int? { value } }
            service Echo: NullableEcho { }
            interface Called { run(value: int): int { value + 2 } }
            service Caller: Called { }
            interface Factorial {
                calculate(value: int): int {
                    if value == 0 { 1 } else { value * calculate(value - 1) }
                }
            }
            service Factor: Factorial { }
            interface Left { value(): int { 11 } }
            interface Right { value(): int { 22 } }
            service Resolved: Left, Right { value() { 42 } }
            interface Optional { shared(): int { 7 } }
            interface Required { shared(): int }
            service Explicit: Optional, Required { shared() { 42 } }
            interface Shadows {
                count(): int { 1 }
                parameter(count: int): int { count + 2 }
                local(): int { let count = 40
                    count + 2 }
            }
            service Shadowed: Shadows { }
            interface Pending {
                async readValue(input: Task<int>): int
                async constant(): int { 42 }
                async answerValue(input: Task<int>): int {
                    let value = await readValue(input)
                    value + 2
                }
            }
            service Awaiter: Pending {
                readValue(input) { await input }
            }
            service AsyncOverride: Pending {
                readValue(input) { await input }
                answerValue(input) { await input }
            }
            fn answer(): int {
                let first: Counter = Ordinary(40)
                let second: Counter = Replace()
                let factor: Factorial = Factor()
                let left: Left = Resolved()
                let right: Right = Resolved()
                let optional: Optional = Explicit()
                let required: Required = Explicit()
                let shadow: Shadows = Shadowed()
                let echo: NullableEcho = Echo()
                if first.final() == 42 && second.final() == 41 && factor.calculate(5) == 120 &&
                   left.value() == 42 && right.value() == 42 && optional.shared() == 42 &&
                   required.shared() == 42 && shadow.parameter(40) == 42 && shadow.local() == 42 &&
                   echo.echo(null) == null && echo.echo(42) == 42 && Ordinary(40).final() == 42 {
                    42
                } else { 0 }
            }
            fn make(mark: (int) -> int): Called {
                let ignored = mark(1)
                Caller()
            }
            fn order(mark: (int) -> int): int {
                make(mark).run(mark(2)) + 38
            }
            async fn delayed(input: Task<int>): int {
                let value: Pending = Awaiter()
                await value.answerValue(input)
            }
            async fn constantDefault(): int {
                let value: Pending = Awaiter()
                await value.constant()
            }
            async fn overrideAsync(input: Task<int>): int {
                let value: Pending = AsyncOverride()
                await value.answerValue(input)
            }
            """;
        var result = DSharpCompiler.Compile(source, "default-methods.ds");
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        await WithGenerated(result.CSharp!, "Demo", async type =>
        {
            Assert.Equal(42, Call<int>(type, "answer"));
            var seen = new List<int>();
            var method = Method(type, "order");
            var marker = Adapt(method.GetParameters()[0].ParameterType, value => { seen.Add(value); return value; });
            Assert.Equal(42, method.Invoke(null, new object[] { marker }));
            Assert.Equal(new[] { 1, 2 }, seen);

            Assert.Equal(42, await Call<Task<int>>(type, "constantDefault").WaitAsync(TimeSpan.FromSeconds(10)));
            var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = Call<Task<int>>(type, "delayed", gate.Task);
            Assert.False(pending.IsCompleted);
            gate.SetResult(40);
            Assert.Equal(42, await pending.WaitAsync(TimeSpan.FromSeconds(10)));
            var failed = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var failure = Call<Task<int>>(type, "delayed", failed.Task);
            failed.SetException(new InvalidOperationException("default failed"));
            Assert.Equal("default failed", (await Assert.ThrowsAsync<InvalidOperationException>(async () => await failure)).Message);
            var overridden = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var overriding = Call<Task<int>>(type, "overrideAsync", overridden.Task);
            Assert.False(overriding.IsCompleted);
            overridden.SetResult(42);
            Assert.Equal(42, await overriding.WaitAsync(TimeSpan.FromSeconds(10)));
        });

        await CrossModuleBodiesResolveInTheirDeclaringFile();
        RejectInvalidDefaults();
    }

    private static async Task CrossModuleBodiesResolveInTheirDeclaringFile()
    {
        var sources = new[]
        {
            new SourceFile("support.ds", "module Support\nfn increment(value: int): int { value + 1 }"),
            new SourceFile("contract.ds", """
                module Contracts
                import Support
                interface Calculation {
                    read(value: int): int
                    result(value: int): int { increment(read(value)) }
                    fromParameter(read: (int) -> int): int { read(41) }
                    fromLocal(): int {
                        let read: (int) -> int = value => value + 2
                        read(40)
                    }
                }
                """),
            new SourceFile("service.ds", """
                module Services
                import Contracts
                fn increment(value: int): int { 1000 }
                service Fixed(amount: int): Calculation { read(value) { amount + value } }
                """),
            new SourceFile("app.ds", """
                module App
                import Contracts
                import Services
                fn answer(): int {
                    let item: Calculation = Fixed(40)
                    if item.result(1) == 42 && item.fromParameter(value => value + 1) == 42 && item.fromLocal() == 42 { 42 } else { 0 }
                }
                """)
        };
        var result = DSharpCompiler.Compile(sources);
        Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
        Assert.Empty(result.Diagnostics);
        await WithGenerated(result.CSharp!, "App", type =>
        {
            Assert.Equal(42, Call<int>(type, "answer"));
            return Task.CompletedTask;
        });
    }

    private static void RejectInvalidDefaults()
    {
        var invalid = new[]
        {
            // Defaults do not satisfy a missing required method on the same interface.
            "interface A { required(): int\n provided(): int { 42 } }\nservice S: A { }",
            // Check default bodies even when no service implements the interface.
            "interface A { provided(): int { true } }",
            "interface A { provided(): int { missing() } }",
            "interface A { provided(): int { await later() } }\nasync fn later(): int { 42 }",
            "interface A { required(value: int): int\n provided(): int { required(true) } }",
            "interface A { provided(): int { 1 }\n provided(): int { 2 } }",
            // Neither declaration order nor interface view silently selects a default.
            "interface A { value(): int { 1 } }\ninterface B { value(): int { 2 } }\nservice S: A, B { }",
            "interface A { value(): int { 1 } }\ninterface B { value(): int }\nservice S: A, B { }",
            "interface A { value(): int { 1 } }\ninterface B { value(): bool { true } }\nservice S: A, B { value() { 1 } }",
            // Interface state and service inheritance remain unsupported.
            "interface A { amount: int\n value(): int { amount } }",
            "interface A { value(): int { 1 } }\nservice S: A { }\nservice Child: S { }",
            // An implicit receiver is a capability and cannot escape in a closure.
            "interface A { get(): int\n closure(): () -> int { () => get() } }\nservice S: A { get() { 42 } }",
            "interface A { get(): int\n closure(): () -> () -> int { () => () => get() } }\nservice S: A { get() { 42 } }",
            // Method values and partial application of methods are not part of #44.
            "interface A { add(x: int, y: int): int { x + y }\n capture(): (int) -> int { add(1) } }\nservice S: A { }",
        };
        foreach (var body in invalid)
        {
            var result = DSharpCompiler.Compile("module Invalid\n" + body, "invalid-default.ds");
            Assert.True(result.CSharp is null, $"Invalid default program compiled: {body}");
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, diagnostic => Assert.Equal("invalid-default.ds", diagnostic.Path));
        }
    }

    private static MethodInfo Method(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
    private static T Call<T>(Type type, string name, params object?[] args) => (T)Method(type, name).Invoke(null, args)!;

    private static Delegate Adapt(Type type, Func<int, int> action)
    {
        var value = Expression.Parameter(typeof(int), "value");
        return Expression.Lambda(type, Expression.Invoke(Expression.Constant(action), value), value).Compile();
    }

    private static async Task WithGenerated(string csharp, string module, Func<Type, Task> verify)
    {
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-default-methods-" + Guid.NewGuid());
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
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated default-method build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("default-methods-" + Guid.NewGuid(), isCollectible: true);
            try
            {
                var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0/Generated.dll"));
                var type = Assert.Single(assembly.GetTypes(), candidate => candidate.Name == module);
                await verify(type);
            }
            finally { context.Unload(); }
        }
        finally { Directory.Delete(directory, true); }
    }
}
