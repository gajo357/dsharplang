using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class NullableTests
{
    [Fact]
    public async Task NullableValuesFlowThroughLocalsCallsReturnsFieldsAndCopies()
    {
        const string source = """
            module Demo
            type Item { number: int }
            type Switch { | Off
                          | On }
            type Container { number: int?
                             text: string?
                             item: Item?
                             choice: Switch?
                             empty: Unit? }
            fn identity(value: int?): int? { value }
            fn getItem(value: Item?): int {
                match value {
                    null => 0
                    item => item.number
                }
            }
            fn describe(value: string?): string {
                match value {
                    name => name
                    null => "absent"
                }
            }
            fn getChoice(value: Switch?): int {
                match value {
                    null => 0
                    Switch.Off => 1
                    Switch.On => 42
                }
            }
            fn getUnit(value: Unit?): int {
                match value {
                    null => 0
                    () => 42
                }
            }
            fn getChar(value: char?): int {
                match value {
                    null => 0
                    ch => if ch == '/' { 42 } else { 1 }
                }
            }
            fn present(): int {
                let number: int? = 42
                match identity(number) {
                    n => n
                    null => 0
                }
            }
            fn nullFirst(): int {
                let number: int? = null
                match number {
                    null => 42
                    n => n
                }
            }
            fn presentFirstNull(): int {
                let number: int? = null
                match number {
                    n => n
                    null => 42
                }
            }
            fn records(): int {
                let item: Item? = Item { number = 42 }
                getItem(item)
            }
            fn absentRecord(): int { getItem(null) }
            fn strings(): bool { describe("hello") == "hello" && describe(null) == "absent" }
            fn unions(): int { getChoice(Switch.On) }
            fn unit(): int { getUnit(()) }
            fn character(): int { getChar('/') }
            fn absentCharacter(): int { getChar(null) }
            fn throughFields(): int {
                let original = Container { number = null
                                           text = null
                                           item = null
                                           choice = null
                                           empty = null }
                let updated = original with { number = 42
                                              text = "ok"
                                              item = Item { number = 42 }
                                              choice = Switch.On
                                              empty = () }
                let cleared = updated with { number = null
                                             item = null }
                let originalValue = match original.number { null => 1
                                                            n => n }
                let newValue = match updated.number { n => n
                                                       null => 0 }
                if (getItem(original.item) == 0) && (getItem(updated.item) == 42) &&
                   (getItem(cleared.item) == 0) && (cleared.number == null) &&
                   (getChoice(updated.choice) == 42) && (getUnit(updated.empty) == 42) &&
                   (describe(updated.text) == "ok") {
                    originalValue + newValue
                } else { 0 }
            }
            """;
        await WithGenerated(source, type =>
        {
            foreach (var name in new[] { "present", "nullFirst", "presentFirstNull", "records", "unions", "unit", "character" })
                Assert.Equal(42, Invoke(type, name));
            Assert.Equal(0, Invoke(type, "absentRecord"));
            Assert.Equal(0, Invoke(type, "absentCharacter"));
            Assert.Equal(true, Invoke(type, "strings"));
            Assert.Equal(43, Invoke(type, "throughFields"));
        });
    }

    [Fact]
    public async Task NullableIfAndMatchInferOrReceiveExpectedTypes()
    {
        const string source = """
            module Demo
            fn optional(flag: bool): int? { if flag { 42 } else { null } }
            fn nullOnly(flag: bool): int? { if flag { null } else { null } }
            fn matchNullOnly(flag: bool): int? {
                match flag { true => null
                             false => null }
            }
            fn neverCondition(): int? {
                match true {
                    true => if (if true { return null } else { return null }) { null } else { null }
                    false => null
                }
            }
            fn inferredIf(flag: bool): int {
                let value = if flag { null } else { 42 }
                match value { number => number
                              null => 42 }
            }
            fn inferredMatch(flag: bool): int {
                let value = match flag { true => null
                                         false => 42 }
                match value { null => 42
                              number => number }
            }
            fn annotated(flag: bool): int {
                let value: int? = if flag { null } else { null }
                match value { null => 42
                              n => n }
            }
            fn contextualArgument(value: int?): int {
                match value { n => n
                              null => 42 }
            }
            fn nested(): int { contextualArgument(if true { null } else { 0 }) }
            fn run(): int {
                let one = match optional(true) { n => n
                                                  null => 0 }
                let two = match optional(false) { n => n
                                                   null => 0 }
                if (one == 42) && (two == 0) && (inferredIf(true) == 42) &&
                   (inferredIf(false) == 42) && (inferredMatch(true) == 42) &&
                   (inferredMatch(false) == 42) && (annotated(true) == 42) &&
                   (nested() == 42) && (nullOnly(true) == null) &&
                   (matchNullOnly(false) == null) && (neverCondition() == null) { 42 } else { 0 }
            }
            """;
        await WithGenerated(source, type => Assert.Equal(42, Invoke(type, "run")));
    }

    [Fact]
    public async Task NestedNullablePatternsGuardsAndWildcardCoverAbsenceAndPresence()
    {
        const string source = """
            module Demo
            type Item { number: int }
            type Switch { | Off
                          | On }
            type Event { | Flag(bool?)
                         | Payload(Item?)
                         | Mode(Switch?)
                         | EmptyUnit(Unit?) }
            fn score(value: Event): int {
                match value {
                    Event.Flag(null) => 1
                    Event.Flag(true) => 42
                    Event.Flag(false) => 2
                    Event.Payload(null) => 3
                    Event.Payload(item) when item.number > 10 => item.number
                    Event.Payload(item) => item.number + 1
                    Event.Mode(null) => 4
                    Event.Mode(Switch.Off) => 5
                    Event.Mode(Switch.On) => 42
                    Event.EmptyUnit(null) => 6
                    Event.EmptyUnit(()) => 42
                }
            }
            fn wildcard(value: int?): int {
                match value {
                    n when n > 0 => n
                    n => n + 1
                    _ => 42
                }
            }
            fn wildcardPayload(value: Event): int {
                match value {
                    Event.Flag(_) => 42
                    _ => 0
                }
            }
            fn run(): int {
                if (score(Event.Flag(true)) == 42) && (score(Event.Flag(null)) == 1) &&
                   (score(Event.Payload(Item { number = 42 })) == 42) &&
                   (score(Event.Payload(Item { number = 4 })) == 5) &&
                   (score(Event.Mode(Switch.On)) == 42) &&
                   (score(Event.EmptyUnit(())) == 42) &&
                   (wildcard(null) == 42) && (wildcard(42) == 42) &&
                   (wildcardPayload(Event.Flag(null)) == 42) { 42 } else { 0 }
            }
            """;
        await WithGenerated(source, type => Assert.Equal(42, Invoke(type, "run")));
    }

    [Fact]
    public async Task NullableEqualityDistinguishesAbsenceAndUsesValueEquality()
    {
        const string source = """
            module Demo
            type Item { number: int }
            type Choice { | Value(int)
                          | Empty }
            type Bag { number: int?
                       item: Item?
                       choice: Choice? }
            type Payload { | Packed(int?, Item?, Choice?) }
            fn run(): bool {
                let absent: int? = null
                let anotherAbsent: int? = null
                let present: int? = 42
                let zero: int? = 0
                let same: int? = 42
                let other: int? = 43
                let text: string? = "hello"
                let noText: string? = null
                let flag: bool? = false
                let character: char? = '/'
                let unit: Unit? = ()
                let noUnit: Unit? = null
                let item: Item? = Item { number = 42 }
                let sameItem: Item? = Item { number = 42 }
                let noItem: Item? = null
                let choice: Choice? = Choice.Value(42)
                let sameChoice: Choice? = Choice.Value(42)
                let noChoice: Choice? = null
                let bag1 = Bag { number = 42
                                 item = Item { number = 42 }
                                 choice = Choice.Value(42) }
                let bag2 = Bag { number = 42
                                 item = Item { number = 42 }
                                 choice = Choice.Value(42) }
                let emptyBag1 = Bag { number = null
                                      item = null
                                      choice = null }
                let emptyBag2 = Bag { number = null
                                      item = null
                                      choice = null }
                let payload1 = Payload.Packed(42, Item { number = 42 }, Choice.Value(42))
                let payload2 = Payload.Packed(42, Item { number = 42 }, Choice.Value(42))
                let emptyPayload1 = Payload.Packed(null, null, null)
                let emptyPayload2 = Payload.Packed(null, null, null)
                (absent == null) && (null == absent) && (absent == anotherAbsent) &&
                (present == 42) && (42 == present) && (present == same) &&
                (present != other) && (present != null) && (zero != null) &&
                (text == "hello") && (text != noText) && (noText == null) &&
                (flag == false) && (flag != null) && (character == '/') && (unit == ()) &&
                (unit != noUnit) && (noUnit == null) && (item == sameItem) &&
                (item == Item { number = 42 }) && (item != noItem) &&
                (noItem == null) && (choice == sameChoice) &&
                (choice == Choice.Value(42)) && (choice != noChoice) && (noChoice == null) &&
                (bag1 == bag2) && (emptyBag1 == emptyBag2) && (bag1 != emptyBag1) &&
                (payload1 == payload2) && (emptyPayload1 == emptyPayload2) && (payload1 != emptyPayload1)
            }
            """;
        await WithGenerated(source, type => Assert.Equal(true, Invoke(type, "run")));
    }

    [Fact]
    public async Task NullableScrutineeIsEvaluatedExactlyOnceAndEarlyReturnsSkipArms()
    {
        const string source = """
            module Demo
            fn tick(): int? { 42 }
            fn absent(): int? { null }
            fn answer(): int {
                match tick() {
                    null => 0
                    number => number
                }
            }
            fn earlyScrutinee(): int {
                match (if true { return 42 } else { absent() }) {
                    null => 1 / 0
                    number => number
                }
            }
            fn earlyArm(): int {
                let value: int? = null
                match value {
                    null => if true { return 42 } else { 0 }
                    number => 1 / 0
                }
            }
            """;
        await WithGenerated(source, type =>
        {
            Assert.Equal(42, Invoke(type, "answer"));
            Assert.Equal(1, Invoke(type, "readTickCount"));
            Assert.Equal(42, Invoke(type, "earlyScrutinee"));
            Assert.Equal(42, Invoke(type, "earlyArm"));
        }, csharp =>
        {
            var match = Assert.Single(Regex.Matches(csharp, @"    public static ([^\r\n(]+) @tick\(\)").Cast<Match>());
            var resultType = match.Groups[1].Value;
            var signature = match.Value;
            return csharp.Replace(signature,
                "    private static int __testTickCount;\n" +
                "    public static int @readTickCount() => __testTickCount;\n" +
                "    public static " + resultType + " @tick() { __testTickCount++; return @tick_original(); }\n" +
                "    public static " + resultType + " @tick_original()", StringComparison.Ordinal);
        });
    }

    [Theory]
    [InlineData("fn f(): int { let value = null\n 42 }")]
    [InlineData("fn f(): int { let value: int = null\n value }")]
    [InlineData("fn f(): int { null }")]
    [InlineData("fn f(value: int?): int { value }")]
    [InlineData("fn required(value: int): int { value }\nfn f(value: int?): int { required(value) }")]
    [InlineData("fn f(value: int??): int { 0 }")]
    [InlineData("fn f(): int { let value: int?? = null\n 42 }")]
    [InlineData("fn f(value: int?): int { match value { n => n } }")]
    [InlineData("fn f(value: int?): int { match value { null => 0 } }")]
    [InlineData("fn f(value: int?): int { match value { null => 0\n n when n > 0 => n } }")]
    [InlineData("type Event { | Flag(bool?) }\nfn f(value: Event): int { match value { Event.Flag(null) => 0\n Event.Flag(true) => 1 } }")]
    [InlineData("type Event { | Flag(bool?) }\nfn f(value: Event): int { match value { Event.Flag(null) => 0\n Event.Flag(flag) when flag => 1 } }")]
    [InlineData("type Item { number: int }\nfn f(value: Item?): int { value.number }")]
    [InlineData("type Item { number: int }\nfn f(value: Item?): int { if value != null { value.number } else { 0 } }")]
    [InlineData("type Item { number: int? }\nfn f(): Item { Item { } }")]
    [InlineData("type Item { number: int? }\nfn f(): Item { Item { number = true } }")]
    [InlineData("type Event { | Number(int?) }\nfn f(): Event { Event.Number(true) }")]
    [InlineData("fn f(value: bool?): int { match value { null => 0\n 1 => 1\n _ => 2 } }")]
    [InlineData("fn f(): bool { null == null }")]
    public void InvalidNullableProgramsAreRejected(string body)
    {
        var source = "module Demo\n" + body;
        var result = DSharpCompiler.Compile(source, "nullable.ds");
        Assert.Null(result.CSharp);
        var error = Assert.Single(result.Diagnostics);
        Assert.StartsWith("DS", error.Code);
        Assert.Equal("nullable.ds", error.Path);
        Assert.InRange(error.Span.Start, 0, source.Length);
        Assert.InRange(error.Span.Length, 0, source.Length - error.Span.Start);
        Assert.True(error.Span.Line >= 1);
        Assert.True(error.Span.Column >= 1);
    }

    [Theory]
    [InlineData("fn f(): int { let value = ~null\n 42 }", "null")]
    [InlineData("fn f(value: int?): int { ~value }", "value")]
    [InlineData("type Item { number: int }\nfn f(value: Item?): int { value.~number }", "number")]
    public void NullableDiagnosticsPreserveSourceSpans(string marked, string token)
    {
        var marker = marked.IndexOf('~');
        var source = "module Demo\n" + marked.Remove(marker, 1);
        var start = "module Demo\n".Length + marker;
        var result = DSharpCompiler.Compile(source, "nullable.ds");
        Assert.Null(result.CSharp);
        var error = Assert.Single(result.Diagnostics);
        Assert.Equal("nullable.ds", error.Path);
        var prefix = source[..start];
        Assert.Equal(new SourceSpan(start, token.Length, prefix.Count(c => c == '\n') + 1,
            start - prefix.LastIndexOf('\n')), error.Span);
    }

    private static object? Invoke(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.Invoke(null, null);

    private static async Task WithGenerated(string source, Action<Type> verify, Func<string, string>? transform = null)
    {
        var compilation = DSharpCompiler.Compile(source, "nullable.ds");
        Assert.Empty(compilation.Diagnostics);
        Assert.Equal(compilation.CSharp, DSharpCompiler.Compile(source).CSharp);
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-nullable-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var csharp = compilation.CSharp!;
            File.WriteAllText(Path.Combine(directory, "Generated.cs"), transform is null ? csharp : transform(csharp));
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
            var context = new AssemblyLoadContext("nullable-" + Guid.NewGuid(), isCollectible: true);
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
