using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class HttpPlainContractTests
{
    private const string Contract = """
        module Demo
        type Request { value: int }
        type Other { value: int }
        type Raw { value: int }
        type Reply { code: int }
        type Output { value: int }
        fn decode(raw: Raw): Result<Request, string> { Success(Request { value = raw.value }) }
        fn mapBinding(error: string): Reply { Reply { code = 400 } }
        fn mapValue(value: int): Reply { Reply { code = value } }
        actor Worker {
            trigger Http<Reply>(decode, mapBinding, mapValue)
            Handle(message: Request): int { message.value }
        }
        """;

    [Fact]
    public void PlainAndUnitHttpCandidatesRetainExplicitMetadata()
    {
        var syntax = new Parser(Lexer.Lex(Contract, "plain-http.ds"), "plain-http.ds").Parse();
        var syntaxBinding = Assert.Single(Assert.Single(syntax.Actors).HttpPlainBindings);
        Assert.Equal("mapValue", syntaxBinding.SuccessMapper.Text);
        Assert.Empty(Assert.Single(syntax.Actors).HttpBindings);
        var compiled = DSharpCompiler.Compile(Contract, "plain-http.ds");
        Assert.Empty(compiled.Diagnostics);
        Assert.NotNull(compiled.CSharp);
        var actor = Assert.Single(compiled.Actors);
        Assert.Equal("Request", actor.InputType);
        Assert.Equal("int", actor.ResultType);
        var http = Assert.Single(actor.HttpPlainBindings);
        Assert.Equal("Reply", http.ResponseType);
        Assert.Equal("decode", http.Binder);
        Assert.Equal("mapBinding", http.BindingMapper);
        Assert.Equal("mapValue", http.SuccessMapper);
        Assert.Equal("string", http.BindingErrorType);
        Assert.Equal("int", http.ValueType);
        Assert.False(http.IsAsyncBinder);
        Assert.Empty(actor.HttpBindings);
        Assert.Null(typeof(BoundHttpPlainBinding).GetProperty("BusinessMapper"));
        Assert.Null(typeof(BoundHttpPlainBinding).GetProperty("BusinessErrorType"));
        Assert.Null(typeof(BoundHttpPlainBinding).GetProperty("BusinessMapperSpan"));
        Assert.Equal("plain-http.ds", http.SourcePath);
        Assert.Equal(Contract.IndexOf("trigger Http<Reply>", StringComparison.Ordinal), http.TriggerSpan.Start);
        Assert.Equal(Contract.IndexOf("Reply>(decode", StringComparison.Ordinal), http.ResponseSpan.Start);
        Assert.Equal(Contract.IndexOf("decode, mapBinding", StringComparison.Ordinal), http.BinderSpan.Start);
        Assert.Equal(Contract.IndexOf("mapBinding, mapValue", StringComparison.Ordinal), http.BindingMapperSpan.Start);
        Assert.Equal(Contract.IndexOf("mapValue)", StringComparison.Ordinal), http.SuccessMapperSpan.Start);

        var asyncContract = Contract.Replace("fn decode(raw: Raw)", "async fn decode(raw: Raw)", StringComparison.Ordinal);
        var asyncResult = DSharpCompiler.Compile(asyncContract, "async-plain-http.ds");
        Assert.Empty(asyncResult.Diagnostics);
        Assert.True(Assert.Single(Assert.Single(asyncResult.Actors).HttpPlainBindings).IsAsyncBinder);

        var recordContract = Contract.Replace("mapValue(value: int): Reply { Reply { code = value } }", "mapValue(value: Output): Reply { Reply { code = value.value } }", StringComparison.Ordinal)
            .Replace("Handle(message: Request): int { message.value }", "Handle(message: Request): Output { Output { value = message.value } }", StringComparison.Ordinal);
        var recordResult = DSharpCompiler.Compile(recordContract, "record-http.ds");
        Assert.Empty(recordResult.Diagnostics);
        Assert.Equal("Output", Assert.Single(Assert.Single(recordResult.Actors).HttpPlainBindings).ValueType);

        var unitContract = Contract.Replace("mapValue(value: int): Reply { Reply { code = value } }", "mapValue(value: Unit): Reply { Reply { code = 204 } }", StringComparison.Ordinal)
            .Replace("Handle(message: Request): int { message.value }", "Handle(message: Request): Unit { () }", StringComparison.Ordinal);
        var unitResult = DSharpCompiler.Compile(unitContract, "unit-http.ds");
        Assert.Empty(unitResult.Diagnostics);
        Assert.Equal("Unit", Assert.Single(Assert.Single(unitResult.Actors).HttpPlainBindings).ValueType);
        Assert.Empty(Assert.Single(unitResult.Actors).HttpBindings);

        var coexistence = Contract.Replace("trigger Http<Reply>(decode, mapBinding, mapValue)",
            "trigger Queue<Request>(\"jobs\")\n    trigger Event<Request>()\n    trigger Cron<Request>(\"nightly\", \"opaque\")\n    trigger Http<Reply>(decode, mapBinding, mapValue)", StringComparison.Ordinal) +
            "\nactor Plain { Handle(message: Request): int { message.value } }";
        var mixed = DSharpCompiler.Compile(coexistence, "mixed-plain-http.ds");
        Assert.Empty(mixed.Diagnostics);
        var worker = Assert.Single(mixed.Actors, item => item.Name == "Worker");
        Assert.Single(worker.QueueBindings);
        Assert.Single(worker.EventBindings);
        Assert.Single(worker.CronBindings);
        Assert.Single(worker.HttpPlainBindings);
        Assert.Empty(worker.HttpBindings);
        var plain = Assert.Single(mixed.Actors, item => item.Name == "Plain");
        Assert.Empty(plain.HttpPlainBindings);
        Assert.Empty(plain.HttpBindings);
        Assert.Empty(plain.QueueBindings);
        Assert.Empty(plain.EventBindings);
        Assert.Empty(plain.CronBindings);

        AssertCrossModuleResultHomonym();
        AssertImportsAndShadowing();
    }

    [Fact]
    public void HttpArityAndCallableTypesFollowTheActualHandleContract()
    {
        (string Label, string Before, string After)[] bad =
        [
            ("binder wrong success", "Result<Request, string> { Success(Request { value = raw.value }) }", "Result<Other, string> { Success(Other { value = raw.value }) }"),
            ("binder wrong return", "Result<Request, string> { Success(Request { value = raw.value }) }", "Request { Request { value = raw.value } }"),
            ("binder wrong arity", "decode(raw: Raw)", "decode(raw: Raw, extra: int)"),
            ("binder generic", "decode(raw: Raw)", "decode<T>(raw: Raw)"),
            ("binder inferred", "decode(raw: Raw): Result<Request, string>", "decode(raw: Raw)"),
            ("unknown binder", "(decode, mapBinding, mapValue)", "(missing, mapBinding, mapValue)"),
            ("binding mapper input", "mapBinding(error: string)", "mapBinding(error: bool)"),
            ("value mapper input", "mapValue(value: int): Reply { Reply { code = value } }", "mapValue(value: string): Reply { Reply { code = 0 } }"),
            ("binding mapper return", "mapBinding(error: string): Reply { Reply { code = 400 } }", "mapBinding(error: string): int { 400 }"),
            ("value mapper return", "mapValue(value: int): Reply { Reply { code = value } }", "mapValue(value: int): int { value }"),
            ("mapper arity", "mapBinding(error: string)", "mapBinding(error: string, extra: int)"),
            ("mapper generic", "mapBinding(error: string)", "mapBinding<T>(error: string)"),
            ("mapper inferred", "mapBinding(error: string): Reply", "mapBinding(error: string)"),
            ("mapper async", "fn mapBinding(error: string)", "async fn mapBinding(error: string)"),
            ("unknown binding mapper", "(decode, mapBinding, mapValue)", "(decode, missing, mapValue)"),
            ("unknown value mapper", "(decode, mapBinding, mapValue)", "(decode, mapBinding, missing)"),
            ("plain with four references", "(decode, mapBinding, mapValue)", "(decode, mapBinding, mapValue, mapValue)"),
            ("Result with three references", "Handle(message: Request): int { message.value }", "Handle(message: Request): Result<int, bool> { Success(message.value) }"),
            ("nullable intrinsic Result", "Handle(message: Request): int { message.value }", "Handle(message: Request): Result<int, bool>? { null }")
        ];
        foreach (var (label, before, after) in bad)
        {
            Assert.Contains(before, Contract);
            var source = Contract.Replace(before, after, StringComparison.Ordinal);
            var result = DSharpCompiler.Compile(source, "invalid-plain-http.ds");
            Assert.True(result.CSharp is null && result.Diagnostics.Any(d => d.Code == "DS2046" && d.Path == "invalid-plain-http.ds"),
                $"Expected HTTP contract rejection for {label}; diagnostics: {string.Join("; ", result.Diagnostics)}");
            var error = Assert.Single(result.Diagnostics, d => d.Code == "DS2046");
            var handle = source.IndexOf("Handle(message", StringComparison.Ordinal);
            var line = source[..handle].Count(c => c == '\n') + 1;
            var column = handle - source.LastIndexOf('\n', handle);
            Assert.Contains("Handle(Request):", error.Message);
            Assert.Contains($"invalid-plain-http.ds({line},{column})", error.Message);
            Assert.True(error.Span.Start >= source.IndexOf("trigger Http", StringComparison.Ordinal) && error.Span.Start < handle,
                $"Expected descriptor token location for {label}");
            if (label.StartsWith("unknown", StringComparison.Ordinal))
                Assert.Equal(source.IndexOf("missing", source.IndexOf("trigger Http", StringComparison.Ordinal), StringComparison.Ordinal), error.Span.Start);
        }

        var resultContract = Contract.Replace("actor Worker {", "fn mapBusiness(error: bool): Reply { Reply { code = 500 } }\nactor Worker {", StringComparison.Ordinal)
            .Replace("(decode, mapBinding, mapValue)", "(decode, mapBinding, mapValue, mapBusiness)", StringComparison.Ordinal)
            .Replace("Handle(message: Request): int { message.value }", "Handle(message: Request): Result<int, bool> { Success(message.value) }", StringComparison.Ordinal);
        var resultPositive = DSharpCompiler.Compile(resultContract, "four-http.ds");
        Assert.Empty(resultPositive.Diagnostics);
        var resultActor = Assert.Single(resultPositive.Actors);
        Assert.Empty(resultActor.HttpPlainBindings);
        var four = Assert.Single(resultActor.HttpBindings);
        Assert.Equal("mapBusiness", four.BusinessMapper);
        Assert.Equal("bool", four.BusinessErrorType);
        Assert.NotNull(four.BusinessMapperSpan);

        const string threeReference = "trigger Http<Reply>(decode, mapBinding, mapValue)";
        const string fourReference = "trigger Http<Reply>(decode, mapBinding, mapValue, mapBusiness)";
        (string Label, string First, string Second)[] duplicates =
        [
            ("three then three", threeReference, threeReference),
            ("three then four", threeReference, fourReference),
            ("four then three", fourReference, threeReference)
        ];
        foreach (var (label, first, second) in duplicates)
        {
            var source = Contract.Replace("actor Worker {", "fn mapBusiness(error: bool): Reply { Reply { code = 500 } }\nactor Worker {", StringComparison.Ordinal)
                .Replace(threeReference, first + "\n    " + second, StringComparison.Ordinal);
            if (first == fourReference)
                source = source.Replace("Handle(message: Request): int { message.value }", "Handle(message: Request): Result<int, bool> { Success(message.value) }", StringComparison.Ordinal);
            var result = DSharpCompiler.Compile(source, "duplicate-plain-http.ds");
            Assert.Null(result.CSharp);
            var error = Assert.Single(result.Diagnostics, d => d.Code == "DS2046");
            Assert.Equal("duplicate-plain-http.ds", error.Path);
            var firstStart = source.IndexOf("trigger Http", StringComparison.Ordinal);
            var secondStart = source.IndexOf("trigger Http", firstStart + 1, StringComparison.Ordinal);
            Assert.True(error.Span.Start == secondStart,
                $"Expected the second descriptor location for {label}; actual offset {error.Span.Start}.");
        }

        string[] malformed =
        [
            "trigger Http<Reply>()",
            "trigger Http<Reply>(decode, mapBinding)",
            "trigger Http<Reply>(decode, mapBinding, mapValue, mapValue, decode)",
            "trigger Http<Reply>(123, mapBinding, mapValue)",
            "triger Http<Reply>(decode, mapBinding, mapValue)"
        ];
        foreach (var descriptor in malformed)
        {
            var source = Contract.Replace("trigger Http<Reply>(decode, mapBinding, mapValue)", descriptor, StringComparison.Ordinal);
            var result = DSharpCompiler.Compile(source, "malformed-plain-http.ds");
            Assert.True(result.CSharp is null && result.Diagnostics.Any(d => d.Path == "malformed-plain-http.ds"),
                $"Expected malformed descriptor rejection: {descriptor}");
        }
        AssertActorBypassRejected();
    }

    private static void AssertCrossModuleResultHomonym()
    {
        const string contracts = """
            module Contracts
            type Request { value: int }
            type Raw { value: int }
            type Reply { code: int }
            fn decode(raw: Raw): Result<Request, string> { Success(Request { value = raw.value }) }
            fn mapBinding(error: string): Reply { Reply { code = 400 } }
            """;
        const string shadow = "module Shadow\ntype Result<T, E> { | Box(T) }";
        const string mapping = """
            module Contracts
            import Shadow
            fn mapValue(value: Shadow.Result<int, bool>): Reply { Reply { code = 200 } }
            """;
        const string actorSource = """
            module Workers
            import Contracts
            import Shadow
            actor Worker {
                trigger Http<Reply>(decode, mapBinding, mapValue)
                Handle(message: Request): Shadow.Result<int, bool> { Shadow.Result.Box(message.value) }
            }
            """;
        SourceFile[] sources = [new("contracts.ds", contracts), new("shadow.ds", shadow), new("mapping.ds", mapping), new("actor.ds", actorSource)];
        var result = DSharpCompiler.Compile(sources);
        Assert.True(result.CSharp is not null && result.Diagnostics.Count == 0,
            $"Expected a user Result homonym as a plain Handle value: {string.Join("; ", result.Diagnostics)}");
        var actor = Assert.Single(result.Actors);
        Assert.Equal("Shadow.Result<int, bool>", actor.ResultType);
        var candidate = Assert.Single(actor.HttpPlainBindings);
        Assert.Equal("Shadow.Result<int, bool>", candidate.ValueType);
        Assert.Empty(actor.HttpBindings);

        var wrongArity = actorSource.Replace("(decode, mapBinding, mapValue)", "(decode, mapBinding, mapValue, mapBinding)", StringComparison.Ordinal);
        SourceFile[] fourSources = [new("contracts.ds", contracts), new("shadow.ds", shadow), new("mapping.ds", mapping), new("actor.ds", wrongArity)];
        var rejected = DSharpCompiler.Compile(fourSources);
        Assert.True(rejected.CSharp is null && rejected.Diagnostics.Any(d => d.Code == "DS2046" && d.Path == "actor.ds"),
            $"Expected user Result to reject the intrinsic Result descriptor shape: {string.Join("; ", rejected.Diagnostics)}");

        const string badBinder = """
            module Workers
            import Contracts
            import Shadow
            fn shadowDecode(raw: Raw): Shadow.Result<Request, string> { Shadow.Result.Box(Request { value = raw.value }) }
            actor Worker {
                trigger Http<Reply>(shadowDecode, mapBinding, mapValue)
                Handle(message: Request): Shadow.Result<int, bool> { Shadow.Result.Box(message.value) }
            }
            """;
        SourceFile[] wrongBinderSources = [new("contracts.ds", contracts), new("shadow.ds", shadow), new("mapping.ds", mapping), new("actor.ds", badBinder)];
        var wrongBinder = DSharpCompiler.Compile(wrongBinderSources);
        Assert.Null(wrongBinder.CSharp);
        var error = Assert.Single(wrongBinder.Diagnostics, d => d.Code == "DS2046");
        Assert.Equal("actor.ds", error.Path);
        Assert.Equal(badBinder.IndexOf("shadowDecode,", StringComparison.Ordinal), error.Span.Start);
    }

    private static void AssertImportsAndShadowing()
    {
        const string contracts = """
            module Contracts
            type Request { value: int }
            type Raw { value: int }
            type Reply { code: int }
            fn mapBinding(error: string): Reply { Reply { code = 400 } }
            fn mapValue(value: int): Reply { Reply { code = value } }
            """;
        const string binding = "module Binding\nimport Contracts\nfn decode(raw: Raw): Result<Request, string> { Success(Request { value = raw.value }) }";
        const string worker = "module Workers\nimport Contracts\nimport Binding\nactor Worker { trigger Http<Reply>(Binding.decode, mapBinding, mapValue) Handle(message: Request): int { message.value } }";
        SourceFile[] sources = [new("contracts.ds", contracts), new("binding.ds", binding), new("worker.ds", worker)];
        var result = DSharpCompiler.Compile(sources);
        Assert.Empty(result.Diagnostics);
        var actor = Assert.Single(result.Actors);
        Assert.Equal("Workers.Worker", actor.Name);
        Assert.Equal("Contracts.Request", actor.InputType);
        var http = Assert.Single(actor.HttpPlainBindings);
        Assert.Equal("Contracts.Reply", http.ResponseType);
        Assert.Equal("Binding.decode", http.Binder);
        Assert.Equal("worker.ds", http.SourcePath);
        Assert.Equal(worker.IndexOf("Binding.decode", StringComparison.Ordinal), http.BinderSpan.Start);
        Assert.Empty(actor.HttpBindings);

        const string unimported = "module Workers\nimport Contracts\nactor Worker { trigger Http<Reply>(Binding.decode, mapBinding, mapValue) Handle(message: Request): int { message.value } }";
        SourceFile[] missing = [new("contracts.ds", contracts), new("binding.ds", binding), new("missing.ds", unimported)];
        var missingResult = DSharpCompiler.Compile(missing);
        Assert.Null(missingResult.CSharp);
        Assert.Contains(missingResult.Diagnostics, d => d.Code == "DS2032" && d.Path == "missing.ds" && d.Span.Start == unimported.IndexOf("Binding.decode", StringComparison.Ordinal) && d.Message.Contains("Handle(Request): int"));

        const string wrongNominal = "module Workers\nimport Contracts\nimport Binding\nimport Other\nactor Worker { trigger Http<Reply>(Binding.decode, mapBinding, mapValue) Handle(message: Other.Request): int { message.value } }";
        SourceFile[] mismatch = [new("contracts.ds", contracts), new("binding.ds", binding), new("other.ds", "module Other\ntype Request { value: int }"), new("mismatch.ds", wrongNominal)];
        var mismatchResult = DSharpCompiler.Compile(mismatch);
        Assert.Null(mismatchResult.CSharp);
        Assert.Contains(mismatchResult.Diagnostics, d => d.Code == "DS2046" && d.Path == "mismatch.ds" && d.Span.Start == wrongNominal.IndexOf("Binding.decode", StringComparison.Ordinal));

        const string shadow = "module Workers\nimport Contracts\nimport Binding\ninterface Store { read(): int }\nactor Worker(Binding: Store) { trigger Http<Reply>(Binding.decode, mapBinding, mapValue) Handle(message: Request): int { message.value } }";
        SourceFile[] shadowSources = [new("contracts.ds", contracts), new("binding.ds", binding), new("shadow.ds", shadow)];
        var shadowResult = DSharpCompiler.Compile(shadowSources);
        Assert.Null(shadowResult.CSharp);
        Assert.Contains(shadowResult.Diagnostics, d => d.Code == "DS2046" && d.Path == "shadow.ds" && d.Span.Start == shadow.IndexOf("Binding.decode", StringComparison.Ordinal));

        const string ambiguous = "module Workers\nimport Contracts\nimport Binding\nimport Other\nactor Worker { trigger Http<Reply>(decode, mapBinding, mapValue) Handle(message: Request): int { message.value } }";
        SourceFile[] ambiguousSources = [new("contracts.ds", contracts), new("binding.ds", binding), new("other.ds", "module Other\nfn decode(raw: int): int { raw }"), new("ambiguous.ds", ambiguous)];
        var ambiguousResult = DSharpCompiler.Compile(ambiguousSources);
        Assert.Null(ambiguousResult.CSharp);
        Assert.Contains(ambiguousResult.Diagnostics, d => d.Code == "DS2031" && d.Path == "ambiguous.ds" && d.Span.Start == ambiguous.IndexOf("decode,", StringComparison.Ordinal));
    }

    private static void AssertActorBypassRejected()
    {
        string[] cases =
        [
            "fn bad(): Unit { Worker() }",
            "fn bad(): int { Worker().Handle(Request { value = 1 }) }",
            "fn bad(): int { let callback = Worker().Handle\n 42 }",
            "fn bad(worker: Worker): int { 1 }"
        ];
        foreach (var bypass in cases)
        {
            var result = DSharpCompiler.Compile(Contract + "\n" + bypass, "plain-http-bypass.ds");
            Assert.True(result.CSharp is null && result.Diagnostics.Count > 0, $"Expected actor isolation rejection for: {bypass}");
        }
    }
}
