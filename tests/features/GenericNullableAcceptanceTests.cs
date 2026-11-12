using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class GenericNullableAcceptance
{
    [Fact(DisplayName = "Generic nullable/value and reference flows")]
    public async Task GenericCallsFieldsAndSubstitutionPreserveNull()
    {
        const string source = """
            module Demo
            type Item { number: int }
            type Choice { | Number(int) }
            type Box<T> { value: T }
            type Slot<T> { value: T? }
            type Project<T> { inner: Box<T?> }
            type Shell<T> { project: Project<T> }
            type Message<T> { | Payload(T?) }
            fn lift<T>(value: T): T? { value }
            fn forward<T>(value: T?): T? { value }
            fn forwardAgain<T>(value: T?): T? { forward<T>(value) }
            fn first<T>(seed: T, value: T?): T? { value }
            fn last<T>(value: T?, seed: T): T? { value }
            fn makeSlot<T>(seed: T, value: T?): Slot<T> { Slot { value = value } }
            fn getSlot<T>(slot: Slot<T>): T? { slot.value }
            fn makeBox<T>(seed: T, value: T?): Box<T?> { Box { value = value } }
            fn forwardBox<T>(value: Box<T>): Box<T> { value }
            fn makeShell<T>(seed: T, value: T?): Shell<T> {
                Shell { project = Project { inner = Box { value = value } } }
            }
            fn fallbackNull<T>(value: T?): T? { value ?? null }
            fn readIntBox(box: Box<int?>): int? { box.value }
            fn wrap<T>(seed: T, value: T?): Message<T> { Message.Payload(value) }
            fn unwrap<T>(value: Message<T>): T? {
                match value {
                    Message.Payload(null) => null
                    Message.Payload(present) => present
                }
            }
            fn isMissing<T>(box: Box<T>?): bool {
                let value = box?.value
                value == null
            }
            fn equal<T>(left: T?, right: T?): bool { left == right }
            fn run(): bool {
                let zero: int? = lift<int>(0)
                let absent: int? = forward<int>(null)
                let again: int? = forwardAgain<int>(null)
                let nullableZero: int? = lift<int?>(0)
                let nullableAbsent: int? = forward<int?>(null)
                let inferred: int? = null
                let inferredLift = lift(inferred)
                let firstOrder = first(42, null)
                let reverseOrder = last(null, 42)
                let valueSlot = makeSlot<int>(0, null)
                let nullableSlot = makeSlot<int?>(null, null)
                let presentSlot = makeSlot<int?>(null, 42)
                let projected: Box<int?> = forwardBox(makeBox<int>(0, null))
                let shell = makeShell<int?>(null, 42)
                let message = wrap<int>(0, 42)
                let emptyMessage = wrap<int?>(null, null)
                let refPresent: string? = forward<string>("hello")
                let refAbsent: string? = forward<string>(null)
                let nestedRef: string? = forward<string?>(null)
                let unitPresent: Unit? = forward<Unit>(())
                let unitAbsent: Unit? = forward<Unit?>(null)
                let box: Box<int> = Box { value = 0 }
                let nullableBox: Box<int?> = Box { value = null }
                (zero == 0) && (zero != null) && (absent == null) && (again == null) &&
                (nullableZero == 0) && (nullableAbsent == null) && (inferredLift == null) &&
                (firstOrder == null) && (reverseOrder == null) &&
                (getSlot(valueSlot) == null) && (getSlot(nullableSlot) == null) &&
                (getSlot(presentSlot) == 42) && (unwrap(message) == 42) &&
                (readIntBox(projected) == null) && (shell.project.inner.value == 42) &&
                (fallbackNull<int>(null) == null) && (fallbackNull<int?>(42) == 42) &&
                (unwrap(emptyMessage) == null) && (refPresent == "hello") &&
                (refAbsent == null) && (nestedRef == null) &&
                (unitPresent != null) && (unitAbsent == null) &&
                isMissing<int>(null) && !isMissing<int>(box) && isMissing<int?>(nullableBox) &&
                equal<int>(null, null) && !equal<int>(0, null) && equal<int?>(0, 0) &&
                equal<Item>(Item { number = 42 }, Item { number = 42 }) &&
                !equal<Item>(Item { number = 42 }, Item { number = 43 }) &&
                equal<Choice>(Choice.Number(42), Choice.Number(42)) &&
                !equal<Choice>(Choice.Number(42), Choice.Number(43))
            }
            """;
        await NullableAcceptanceRuntime.WithGenerated(source, type => Assert.Equal(true, NullableAcceptanceRuntime.Invoke(type, "run")));
    }

    [Fact(DisplayName = "Generic nullable/matching and default")]
    public async Task NullableGenericMatchingAndFallbackDistinguishDefaultFromAbsence()
    {
        const string source = """
            module Demo
            type Box<T> { value: T }
            fn forward<T>(value: T?): T? { value }
            fn supplied<T>(value: T?): bool {
                match value {
                    null => false
                    present => true
                }
            }
            fn orElse<T>(value: T?, fallback: T): T { value ?? fallback }
            fn get<T>(value: T?): T? { forward<T>(value) }
            fn run(): bool {
                let existing: Box<int?> = Box { value = null }
                let zero: int? = get<int?>(0)
                let missing: int? = get<int?>(null)
                let stringPresent: string? = get<string?>("present")
                (existing.value == null) && (zero == 0) && (zero != null) &&
                (missing == null) && supplied<int>(0) && !supplied<int>(null) &&
                supplied<string>("x") && !supplied<string>(null) &&
                (orElse<int>(null, 42) == 42) && (orElse<int>(0, 42) == 0) &&
                (orElse<int?>(null, null) == null) && (orElse<int?>(null, 42) == 42) &&
                (orElse<string>(null, "fallback") == "fallback") &&
                (stringPresent == "present")
            }
            """;
        await NullableAcceptanceRuntime.WithGenerated(source, type => Assert.Equal(true, NullableAcceptanceRuntime.Invoke(type, "run")));
    }

    [Theory(DisplayName = "Generic nullable/invalid")]
    [InlineData("unresolved", 0, 1)]
    [InlineData("mismatch", 2, 4)]
    public void Invalid(string group, int first, int last)
    {
        for (var id = first; id <= last; id++)
        {
            var failure = Record.Exception(() =>
            {
                string body = id switch
                {
                    0 => "fn maybe<T>(value: T?): T? { value }\nfn f(): int { let value = maybe(42)\n 42 }",
                    1 => "fn maybe<T>(value: T?): T? { value }\nfn f(): int { let value = maybe(null)\n 42 }",
                    2 => "fn maybe<T>(value: T?): T? { value }\nfn f(): int { let value: int = maybe<int>(null)\n value }",
                    3 => "type Box<T> { value: T }\nfn f(): Box<int> { Box { value = null } }",
                    4 => "fn lift<T>(value: T): T? { value }\nfn f(): string? { lift<int>(42) }",
                    _ => throw new ArgumentOutOfRangeException(nameof(id))
                };
                var result = DSharpCompiler.Compile("module Demo\n" + body, "nullable-generic.ds");
                Assert.Null(result.CSharp);
                var error = Assert.Single(result.Diagnostics);
                Assert.StartsWith("DS", error.Code);
                Assert.Equal("nullable-generic.ds", error.Path);
            });
            Assert.True(failure is null, $"Nullable generic {group} scenario {id}: {failure}");
        }
    }
}

internal static class NullableAcceptanceRuntime
{
    internal static object? Invoke(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.Invoke(null, null);

    internal static async Task WithGenerated(string source, Action<Type> verify)
    {
        var compilation = DSharpCompiler.Compile(source, "nullable-option.ds");
        Assert.Empty(compilation.Diagnostics);
        Assert.Equal(compilation.CSharp, DSharpCompiler.Compile(source).CSharp);
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-nullable-option-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "Generated.cs"), compilation.CSharp);
            File.WriteAllText(Path.Combine(directory, "Generated.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>");
            File.WriteAllText(Path.Combine(directory, "global.json"), "{\"sdk\":{\"version\":\"10.0.100\",\"rollForward\":\"disable\"}}");
            File.WriteAllText(Path.Combine(directory, "NuGet.Config"), "<configuration><packageSources><clear/></packageSources></configuration>");
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "build", "Generated.csproj", "--nologo", "--verbosity", "quiet" }) start.ArgumentList.Add(arg);
            using var build = Process.Start(start)!;
            var stdout = build.StandardOutput.ReadToEndAsync();
            var stderr = build.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try { await build.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { build.Kill(true); throw new TimeoutException("Generated build timed out."); }
            Assert.True(build.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("nullable-option-" + Guid.NewGuid(), isCollectible: true);
            try
            {
                var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net10.0/Generated.dll"));
                verify(assembly.GetType("DSharp.Generated.Demo")!);
            }
            finally { context.Unload(); }
        }
        finally { Directory.Delete(directory, true); }
    }
}
