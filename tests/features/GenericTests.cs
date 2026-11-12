using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class GenericTests
{
    [Fact]
    public void GenericCallShapeIsSyntacticAndNestedClosersRemainPartOfTypes()
    {
        const string source = """
            module Demo
            type Box<T> { value: T }
            fn identity<T>(value: T): T { value }
            fn nested(value: Box<Box<int>>): Box<Box<int>> { identity<Box<Box<int>>>(value) }
            fn comparison(a: int, b: int): bool { a < b }
            fn shape(a: int, b: int, c: int): int { a < b > (c) }
            """;
        var module = new Parser(Lexer.Lex(source, "generics.ds"), "generics.ds").Parse();
        var nested = Assert.IsType<CallSyntax>(module.Functions[1].Expression);
        Assert.Equal("identity", nested.Name.Text);
        Assert.Equal("Box<Box<int>>", Assert.Single(nested.TypeArguments).Text);
        var comparison = Assert.IsType<BinarySyntax>(module.Functions[2].Expression);
        Assert.Equal("<", comparison.Operator.Text);
        var syntacticCall = Assert.IsType<CallSyntax>(module.Functions[3].Expression);
        Assert.Equal("a", syntacticCall.Name.Text);
        Assert.Equal("b", Assert.Single(syntacticCall.TypeArguments).Text);
        Assert.Equal("c", Assert.IsType<ValueSyntax>(Assert.Single(syntacticCall.Arguments)).Value.Text);

        var result = DSharpCompiler.Compile(source, "generics.ds");
        Assert.Null(result.CSharp);
        Assert.Single(result.Diagnostics); // It must not fall back to a comparison.
    }

    [Fact]
    public async Task GenericTypeClosersCanTouchEqualsWithoutChangingGreaterThanOrEqual()
    {
        const string source = """
            module Demo
            type Box<T> { value: T }
            type Inner<T> { value: T }
            type Outer<T> { value: T }
            fn adjacent(): int {
                let value: Box<int>=Box { value = 42 }
                value.value
            }
            fn nested(): int {
                let value: Outer<Inner<int>>=Outer { value = Inner { value = 42 } }
                value.value.value
            }
            fn comparison(): int { if 42>=42 { 42 } else { 0 } }
            fn falseComparison(): int { if 41>=42 { 0 } else { 42 } }
            """;
        var module = new Parser(Lexer.Lex(source, "generics.ds"), "generics.ds").Parse();
        var boxType = Assert.Single(module.Functions[0].Bindings).Annotation!;
        var outerType = Assert.Single(module.Functions[1].Bindings).Annotation!;
        Assert.Equal("Box<int>", boxType.Text);
        Assert.Equal("Outer<Inner<int>>", outerType.Text);
        Assert.Equal(source.IndexOf("Box<int>=", StringComparison.Ordinal), boxType.Span.Start);
        Assert.Equal("Box<int>".Length, boxType.Span.Length);
        Assert.Equal(source.IndexOf("Outer<Inner<int>>=", StringComparison.Ordinal), outerType.Span.Start);
        Assert.Equal("Outer<Inner<int>>".Length, outerType.Span.Length);
        await WithGenerated(source, type =>
        {
            foreach (var name in new[] { "adjacent", "nested", "comparison", "falseComparison" })
                Assert.Equal(42, Invoke(type, name));
        });
    }

    [Fact]
    public async Task NullableGenericInferenceDoesNotDependOnArgumentOrder()
    {
        const string source = """
            module Demo
            type Pair<T> { first: T
                           second: T }
            type Box<T> { value: T }
            fn identity<T>(value: T): T { value }
            fn choose<T>(first: T, second: T): T { second }
            fn run(): bool {
                let value: int? = 42
                let left = choose(42, value)
                let right = choose(value, 42)
                let nestedLeft = choose(42, identity(value))
                let nestedRight = choose(identity(value), 42)
                let firstNull = choose(null, 42)
                let lastNull = choose(42, null)
                let firstCoalesce = choose(null ?? null, 42)
                let lastCoalesce = choose(42, null ?? null)
                let firstBox = choose(Box { value = null ?? null }, Box { value = 42 })
                let lastBox = choose(Box { value = 42 }, Box { value = null ?? null })
                let pairFirst = Pair { first = null
                                       second = 42 }
                let pairLast = Pair { first = 42
                                      second = null }
                (left == 42) && (right == 42) && (left != null) && (right != null) &&
                (nestedLeft == 42) && (nestedRight == 42) &&
                (firstNull == 42) && (lastNull == null) &&
                (firstCoalesce == 42) && (lastCoalesce == null) &&
                (firstBox.value == 42) && (firstBox.value != null) && (lastBox.value == null) &&
                (pairFirst.first == null) && (pairFirst.second == 42) &&
                (pairLast.first == 42) && (pairLast.second == null)
            }
            """;
        await WithGenerated(source, type => Assert.Equal(true, Invoke(type, "run")));
    }

    [Fact]
    public async Task GenericInferenceCombinesNestedNullEvidenceInEitherOrder()
    {
        const string source = """
            module Demo
            type Box<T> { value: T }
            fn choose<T>(box: Box<T>, value: T): Box<T> { box }
            fn chooseReverse<T>(value: T, box: Box<T>): Box<T> { box }
            fn inspect(box: Box<int?>): int {
                match box.value { null => 42
                                  number => number }
            }
            fn faultFirst(): int {
                let box = choose(Box { value = if 1 / 0 == 0 { null } else { null } }, 2147483647 + 1)
                inspect(box)
            }
            fn overflowFirst(): int {
                let box = chooseReverse(2147483647 + 1, Box { value = if 1 / 0 == 0 { null } else { null } })
                inspect(box)
            }
            fn run(): bool {
                let first = choose(Box { value = null }, 42)
                let second = chooseReverse(42, Box { value = null })
                let conditional = choose(Box { value = if true { null } else { null } }, 42)
                let matched = chooseReverse(42, Box { value = match true { true => null
                                                                            false => null } })
                (inspect(first) == 42) && (inspect(second) == 42) &&
                (inspect(conditional) == 42) && (inspect(matched) == 42)
            }
            """;
        await WithGenerated(source, type =>
        {
            Assert.Equal(true, Invoke(type, "run"));
            Assert.IsType<DivideByZeroException>(Assert.Throws<TargetInvocationException>(() => Invoke(type, "faultFirst")).InnerException);
            Assert.IsType<OverflowException>(Assert.Throws<TargetInvocationException>(() => Invoke(type, "overflowFirst")).InnerException);
        });
    }

    [Fact]
    public async Task ExplicitInferredAndForwardGenericFunctionsExecute()
    {
        const string source = """
            module Demo
            fn identity<T>(value: T): T { value }
            fn keep<T>(value: T): T {
                let same: T = value
                same
            }
            fn useForward(): int { later(42) }
            fn later<T>(value: T): T { identity<T>(value) }
            fn unitValue(value: Unit): int { 42 }
            fn explicit(): int { identity<int>(42) }
            fn inferred(): int { identity(42) }
            fn genericLocal(): int { keep(42) }
            fn text(): string { identity<string>("hello") }
            fn boolean(): bool { identity(true) }
            fn unit(): int { unitValue(identity(())) }
            fn typedNullable(): int {
                let value: int? = identity<int?>(null)
                match value { null => 42
                              n => n }
            }
            """;
        await WithGenerated(source, type =>
        {
            foreach (var name in new[] { "useForward", "explicit", "inferred", "genericLocal", "unit", "typedNullable" })
                Assert.Equal(42, Invoke(type, name));
            Assert.Equal("hello", Invoke(type, "text"));
            Assert.Equal(true, Invoke(type, "boolean"));
        });
    }

    [Fact]
    public async Task GenericRecordsAndUnionsSubstituteNestedTypesAndKeepNominalIdentity()
    {
        const string source = """
            module Demo
            type Box<T> { value: T }
            type Item { number: int }
            type Choice { | Mark(int) }
            type Option<T> { | Empty
                             | Present(T) }
            type Holder<T> { item: Box<T>
                             maybe: Option<T> }
            fn identity<T>(value: T): T { value }
            fn unbox<T>(box: Box<T>): T { box.value }
            fn wrap<T>(value: T): Box<T> { Box { value = value } }
            fn makeNested(): Box<Box<int>> { Box { value = Box { value = 42 } } }
            fn unwrap<T>(value: Option<T>, fallback: T): T {
                match value {
                    Option.Empty => fallback
                    Option.Present(item) => item
                }
            }
            fn empty(): Option<int> { Option.Empty }
            fn genericEmpty<T>(): Option<T> { Option.Empty }
            fn expectedOnly(): Option<int> { genericEmpty() }
            fn explicitEmpty(): Option<int> { genericEmpty<int>() }
            fn nested(): int {
                let inner = Box { value = 42 }
                let outer = Box { value = inner }
                let annotated: Box<Box<int>> = identity<Box<Box<int>>>(outer)
                unbox(unbox(annotated))
            }
            fn fields(): int {
                let holder: Holder<int> = Holder { item = wrap(42)
                                                   maybe = Option.Present(42) }
                unbox(holder.item)
            }
            fn union(): int {
                let value: Option<int> = Option.Present(42)
                let explicit: Option<int> = Option.Present<int>(42)
                if value == explicit { unwrap(value, 0) } else { 0 }
            }
            fn emptyCase(): int { unwrap(empty(), 42) }
            fn inferredFromReturn(): int { unwrap(expectedOnly(), 42) }
            fn explicitNoArguments(): int { unwrap(explicitEmpty(), 42) }
            fn nestedCall(): int { unbox(unbox(makeNested())) }
            fn equality(): bool {
                let first = Box { value = Box { value = 42 } }
                let second = Box { value = Box { value = 42 } }
                let different = Box { value = Box { value = 43 } }
                (first == second) && (first != different)
            }
            fn nestedStructuralEquality(): bool {
                let item1 = Box { value = Item { number = 42 } }
                let item2 = Box { value = Item { number = 42 } }
                let item3 = Box { value = Item { number = 43 } }
                let choice1 = Box { value = Choice.Mark(42) }
                let choice2 = Box { value = Choice.Mark(42) }
                let choice3 = Box { value = Choice.Mark(43) }
                (item1 == item2) && (item1 != item3) &&
                (choice1 == choice2) && (choice1 != choice3)
            }
            fn nullable(): int {
                let box: Box<int?> = Box { value = null }
                match box.value { null => 42
                                  value => value }
            }
            fn safeAccess(box: Box<int>?): int { box?.value ?? 42 }
            fn safePresent(): int { safeAccess(Box { value = 42 }) }
            fn safeMissing(): int { safeAccess(null) }
            """;
        await WithGenerated(source, type =>
        {
            foreach (var name in new[] { "nested", "fields", "union", "emptyCase", "inferredFromReturn", "explicitNoArguments", "nestedCall", "nullable", "safePresent", "safeMissing" })
                Assert.Equal(42, Invoke(type, name));
            Assert.Equal(true, Invoke(type, "equality"));
            Assert.Equal(true, Invoke(type, "nestedStructuralEquality"));
        });
    }

    [Fact]
    public async Task GenericCallsPreserveArgumentOrderEarlyReturnAndSingleEvaluation()
    {
        const string source = """
            module Demo
            fn identity<T>(value: T): T { value }
            fn second<T>(first: T, second: T): T { second }
            fn tick(): int { 42 }
            fn once(): int { identity(tick()) }
            fn faultFirst(): int { second<int>(1 / 0, 2147483647 + 1) }
            fn overflowFirst(): int { second<int>(2147483647 + 1, 1 / 0) }
            fn early(): int { second<int>(if true { return 42 } else { 1 }, 1 / 0) }
            """;
        await WithGenerated(source, type =>
        {
            Assert.Equal(42, Invoke(type, "once"));
            Assert.Equal(1, Invoke(type, "readTickCount"));
            Assert.IsType<DivideByZeroException>(Assert.Throws<TargetInvocationException>(() => Invoke(type, "faultFirst")).InnerException);
            Assert.IsType<OverflowException>(Assert.Throws<TargetInvocationException>(() => Invoke(type, "overflowFirst")).InnerException);
            Assert.Equal(42, Invoke(type, "early"));
        }, csharp =>
        {
            var match = Assert.Single(Regex.Matches(csharp, @"    public static ([^\r\n(]+) @tick\(\)").Cast<Match>());
            var resultType = match.Groups[1].Value;
            return csharp.Replace(match.Value,
                "    private static int __testTickCount;\n" +
                "    public static int @readTickCount() => __testTickCount;\n" +
                "    public static " + resultType + " @tick() { __testTickCount++; return @tick_original(); }\n" +
                "    public static " + resultType + " @tick_original()", StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task GenericNamesAvoidCSharpKeywordsAndGeneratedMemberCollisions()
    {
        const string source = """
            module Demo
            type Equals<T> { class: T
                             GetHashCode: int }
            type Keyword<class> { value: class }
            fn class<T>(value: T): T { value }
            fn keyword<event>(value: event): event { value }
            fn answer(): int {
                let value = Equals { class = 42
                                     GetHashCode = 0 }
                let item = Keyword { value = 42 }
                if (class(value.class) == 42) && (keyword(item.value) == 42) &&
                   (value.GetHashCode == 0) { 42 } else { 0 }
            }
            """;
        await WithGenerated(source, type => Assert.Equal(42, Invoke(type, "answer")));
    }

    [Fact]
    public async Task TypeParametersShadowNominalNamesWithoutChangingExistingFieldTypes()
    {
        const string source = """
            module Demo
            type Item { number: int }
            type Kind { | One }
            type Wrapper { item: Item }
            type Tagged<Item> { value: Item
                                wrapper: Wrapper }
            type TaggedKind<Kind> { value: Kind }
            type Choice<Item> { | Entry(Item)
                                | Wrapped(Wrapper) }
            fn pick<Item>(wrapper: Wrapper, value: Item): Item {
                if wrapper.item.number == 42 { value } else { value }
            }
            fn useUnit<Unit>(value: Unit): Unit {
                let actual = ()
                let checked = consume(actual)
                let same: Unit = value
                if checked == 42 { same } else { same }
            }
            fn consume(value: Unit): int { 42 }
            fn tagged(value: Tagged<int>): int {
                value.wrapper.item.number + value.value - 42
            }
            fn choice(value: Choice<int>): int {
                match value {
                    Choice.Entry(item) => item
                    Choice.Wrapped(wrapper) => wrapper.item.number
                }
            }
            fn run(): int {
                let wrapper = Wrapper { item = Item { number = 42 } }
                let taggedValue = Tagged { value = 42
                                           wrapper = wrapper }
                let taggedKind = TaggedKind { value = 42 }
                let entry: Choice<int> = Choice.Entry(42)
                let wrapped: Choice<int> = Choice.Wrapped(wrapper)
                if (pick(wrapper, 42) == 42) && (useUnit(42) == 42) &&
                   (tagged(taggedValue) == 42) && (taggedKind.value == 42) && (choice(entry) == 42) &&
                   (choice(wrapped) == 42) { 42 } else { 0 }
            }
            """;
        await WithGenerated(source, type => Assert.Equal(42, Invoke(type, "run")));
    }

    [Theory]
    [InlineData("fn identity<T>(value: T): T { value }\nfn f(): int { identity<int, bool>(42) }")]
    [InlineData("fn identity<T>(value: T): T { value }\nfn f(): int { identity<int>() }")]
    [InlineData("fn identity<T>(value: T): T { value }\nfn f(): int { identity<int>(true) }")]
    [InlineData("fn choose<T>(a: T, b: T): T { a }\nfn f(): int { choose(1, true) }")]
    [InlineData("fn unresolved<T>(): int { 42 }\nfn f(): int { unresolved() }")]
    [InlineData("fn identity<T>(value: T): T { value }\nfn f(): int { identity<Missing>(42) }")]
    [InlineData("fn nongeneric(value: int): int { value }\nfn f(): int { nongeneric<int>(42) }")]
    [InlineData("fn duplicate<T, T>(value: T): T { value }\nfn f(): int { 0 }")]
    [InlineData("type Box<T, T> { value: T }\nfn f(): int { 0 }")]
    [InlineData("type Item { number: int }\nfn bad<Item>(value: Item): Item { Item { number = 42 } }\nfn f(): int { 0 }")]
    [InlineData("fn bad<Unit>(): Unit { () }\nfn f(): int { 0 }")]
    [InlineData("type Box<T> { value: T }\nfn f(value: Box<int, bool>): int { 0 }")]
    [InlineData("type Box<T> { value: T }\nfn f(value: Box): int { 0 }")]
    [InlineData("type Box<T> { value: T }\nfn f(): Box<bool> { Box { value = 42 } }")]
    [InlineData("type Box<T> { value: T }\nfn f(box: Box<int>?): int { box.value }")]
    [InlineData("type Box<T> { value: T }\nfn f(box: Box<int>?): Box<int> { box with { value = 1 } }")]
    [InlineData("type Box<T> { value: T }\nfn f(): bool { let a = Box { value = 42 }\n let b = Box { value = true }\n a == b }")]
    [InlineData("type Box<T> { value: T }\ntype Wrapper<T> { value: T }\nfn f(): bool { let a = Box { value = 42 }\n let b = Wrapper { value = 42 }\n a == b }")]
    [InlineData("type Option<T> { | Present(T) }\nfn f(): Option<bool> { Option.Present(42) }")]
    [InlineData("type Option<T> { | Present(T) }\nfn f(): bool { let first: Option<int> = Option.Present(1)\n let second: Option<bool> = Option.Present(true)\n first == second }")]
    [InlineData("type Option<T> { | Empty\n | Present(T) }\nfn f(): int { let value = Option.Empty\n 42 }")]
    [InlineData("fn equal<T>(a: T, b: T): bool { a == b }\nfn f(): int { 0 }")]
    [InlineData("fn isNull<T>(value: T): bool { value == null }\nfn f(): int { 0 }")]
    [InlineData("fn add<T>(a: T, b: T): T { a + b }\nfn f(): int { 0 }")]
    [InlineData("fn choose<T>(a: T, b: T): T { b }\nfn f(value: int?): int { choose<int>(value, 42) }")]
    [InlineData("fn choose<T>(a: T, b: T): T { b }\nfn f(): int { choose<int>(null, 42) }")]
    [InlineData("fn choose<T>(a: T, b: T): T { b }\nfn f(): int { let value = choose(null, null)\n 42 }")]
    [InlineData("type Box<T> { value: T }\nfn choose<T>(box: Box<T>, value: T): Box<T> { box }\nfn f(): Box<int> { choose<int>(Box { value = null }, 42) }")]
    [InlineData("type Box<T> { value: T }\nfn accept(value: Box<int?>): int { 42 }\nfn f(): int { let value: Box<int> = Box { value = 42 }\n accept(value) }")]
    [InlineData("fn f(a: int, b: int, c: int): bool { a < b > c }")]
    [InlineData("fn f(a: int, b: int, c: int): bool { a < b > (c) }")]
    public void InvalidGenericProgramsAreDiagnosed(string body)
    {
        var source = "module Demo\n" + body;
        var result = DSharpCompiler.Compile(source, "generics.ds");
        Assert.Null(result.CSharp);
        var error = Assert.Single(result.Diagnostics);
        Assert.StartsWith("DS", error.Code);
        Assert.Equal("generics.ds", error.Path);
        Assert.InRange(error.Span.Start, 0, source.Length);
        Assert.InRange(error.Span.Length, 0, source.Length - error.Span.Start);
        Assert.True(error.Span.Line >= 1);
        Assert.True(error.Span.Column >= 1);
    }

    [Theory]
    [InlineData("fn identity<T>(value: T): T { value }\nfn f(): int { identity<~Missing>(42) }", "Missing")]
    [InlineData("fn choose<T>(a: T, b: T): T { a }\nfn f(): int { choose(1, ~true) }", "true")]
    public void GenericDiagnosticsReferToOriginalSource(string marked, string token)
    {
        var marker = marked.IndexOf('~');
        var source = "module Demo\n" + marked.Remove(marker, 1);
        var start = "module Demo\n".Length + marker;
        var result = DSharpCompiler.Compile(source, "generics.ds");
        Assert.Null(result.CSharp);
        var error = Assert.Single(result.Diagnostics);
        Assert.Equal("generics.ds", error.Path);
        var prefix = source[..start];
        Assert.Equal(new SourceSpan(start, token.Length, prefix.Count(c => c == '\n') + 1,
            start - prefix.LastIndexOf('\n')), error.Span);
    }

    [Fact]
    public void ShadowingDiagnosticDistinguishesTypeParameterWithoutInternalSymbols()
    {
        const string source = "module Demo\ntype Item { number: int }\ntype Wrapper { item: Item }\n" +
            "fn bad<Item>(value: Wrapper): Item { value.item }";
        var result = DSharpCompiler.Compile(source, "generics.ds");
        Assert.Null(result.CSharp);
        var error = Assert.Single(result.Diagnostics);
        Assert.Equal("generics.ds", error.Path);
        Assert.Contains("Item (type parameter)", error.Message);
        Assert.Contains("found Item", error.Message);
        Assert.DoesNotContain("$", error.Message);
        var start = source.LastIndexOf("item", StringComparison.Ordinal);
        var prefix = source[..start];
        Assert.Equal(new SourceSpan(start, "item".Length, prefix.Count(c => c == '\n') + 1,
            start - prefix.LastIndexOf('\n')), error.Span);
    }

    private static object? Invoke(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.Invoke(null, null);

    private static async Task WithGenerated(string source, Action<Type> verify, Func<string, string>? transform = null)
    {
        var compilation = DSharpCompiler.Compile(source, "generics.ds");
        Assert.Empty(compilation.Diagnostics);
        Assert.Equal(compilation.CSharp, DSharpCompiler.Compile(source).CSharp);
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-generics-" + Guid.NewGuid());
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
            var context = new AssemblyLoadContext("generics-" + Guid.NewGuid(), isCollectible: true);
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
