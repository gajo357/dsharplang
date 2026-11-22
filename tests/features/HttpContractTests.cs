using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class HttpContractTests
{
    private const string Contract = """
        module Demo
        type Request { value: int }
        type Other { value: int }
        type Raw { value: int }
        type Reply { code: int }
        type Http { value: int }
        fn decode(raw: Raw): Result<Request, string> { Success(Request { value = raw.value }) }
        fn mapBinding(error: string): Reply { Reply { code = 400 } }
        fn mapSuccess(value: int): Reply { Reply { code = value } }
        fn mapBusiness(error: bool): Reply { Reply { code = 500 } }
        actor Worker {
            trigger Http<Reply>(decode, mapBinding, mapSuccess, mapBusiness)
            trigger(value: Http): int { value.value }
            Handle(message: Request): Result<int, bool> { Success(trigger(Http { value = message.value })) }
        }
        """;

    [Fact]
    public void HttpCandidatesCheckRealCallablesAndRemainInertMetadata()
    {
        var syntax = new Parser(Lexer.Lex(Contract, "http.ds"), "http.ds").Parse();
        var actorSyntax = Assert.Single(syntax.Actors);
        var bindingSyntax = Assert.Single(actorSyntax.HttpBindings);
        Assert.Equal("trigger", bindingSyntax.Keyword.Text);
        Assert.Equal("Reply", bindingSyntax.ResponseType.Text);
        Assert.Equal("decode", bindingSyntax.Binder.Text);
        Assert.Equal("mapBinding", bindingSyntax.BindingMapper.Text);
        Assert.Equal("mapSuccess", bindingSyntax.SuccessMapper.Text);
        Assert.Equal("mapBusiness", bindingSyntax.BusinessMapper.Text);
        Assert.Contains(actorSyntax.Methods, method => method.Name.Text == "trigger");

        var compiled = DSharpCompiler.Compile(Contract, "http.ds");
        Assert.Empty(compiled.Diagnostics);
        Assert.NotNull(compiled.CSharp);
        var actor = Assert.Single(compiled.Actors);
        Assert.Equal("Request", actor.InputType);
        Assert.Equal("Result<int, bool>", actor.ResultType);
        var http = Assert.Single(actor.HttpBindings);
        Assert.Equal("Reply", http.ResponseType);
        Assert.Equal("decode", http.Binder);
        Assert.Equal("mapBinding", http.BindingMapper);
        Assert.Equal("mapSuccess", http.SuccessMapper);
        Assert.Equal("mapBusiness", http.BusinessMapper);
        Assert.Equal("string", http.BindingErrorType);
        Assert.Equal("int", http.ValueType);
        Assert.Equal("bool", http.BusinessErrorType);
        Assert.False(http.IsAsyncBinder);
        Assert.Equal("http.ds", http.SourcePath);
        Assert.Equal(Contract.IndexOf("trigger Http<Reply>", StringComparison.Ordinal), http.TriggerSpan.Start);
        Assert.Equal(Contract.IndexOf("Reply>(decode", StringComparison.Ordinal), http.ResponseSpan.Start);
        Assert.Equal(Contract.IndexOf("decode, mapBinding", StringComparison.Ordinal), http.BinderSpan.Start);
        Assert.Equal(Contract.IndexOf("mapBinding, mapSuccess", StringComparison.Ordinal), http.BindingMapperSpan.Start);
        Assert.Equal(Contract.IndexOf("mapSuccess, mapBusiness", StringComparison.Ordinal), http.SuccessMapperSpan.Start);
        Assert.Equal(Contract.IndexOf("mapBusiness)", StringComparison.Ordinal), http.BusinessMapperSpan.Start);
        Assert.Empty(actor.QueueBindings);
        Assert.Empty(actor.EventBindings);
        Assert.Empty(actor.CronBindings);

        var asyncContract = Contract.Replace("fn decode(raw: Raw)", "async fn decode(raw: Raw)", StringComparison.Ordinal);
        var asyncResult = DSharpCompiler.Compile(asyncContract, "async-http.ds");
        Assert.Empty(asyncResult.Diagnostics);
        Assert.True(Assert.Single(Assert.Single(asyncResult.Actors).HttpBindings).IsAsyncBinder);

        var coexistence = Contract.Replace("trigger Http<Reply>(decode, mapBinding, mapSuccess, mapBusiness)",
            "trigger Queue<Request>(\"jobs\")\n    trigger Event<Request>()\n    trigger Cron<Request>(\"nightly\", \"opaque\")\n    trigger Http<Reply>(decode, mapBinding, mapSuccess, mapBusiness)", StringComparison.Ordinal) +
            "\nactor Plain { Handle(message: Request): int { message.value } }";
        var mixed = DSharpCompiler.Compile(coexistence, "mixed-http.ds");
        Assert.Empty(mixed.Diagnostics);
        var worker = Assert.Single(mixed.Actors, item => item.Name == "Worker");
        Assert.Single(worker.QueueBindings);
        Assert.Single(worker.EventBindings);
        Assert.Single(worker.CronBindings);
        Assert.Single(worker.HttpBindings);
        var plain = Assert.Single(mixed.Actors, item => item.Name == "Plain");
        Assert.Empty(plain.HttpBindings);
        Assert.Empty(plain.QueueBindings);
        Assert.Empty(plain.EventBindings);
        Assert.Empty(plain.CronBindings);

        AssertMultiFileIdentityAndImports();
    }

    [Fact]
    public void InvalidHttpContractsRejectWrongFunctionsAndPreserveActorIsolation()
    {
        (string Label, string Before, string After)[] bad =
        [
            ("binder wrong success", "Result<Request, string> { Success(Request { value = raw.value }) }", "Result<Other, string> { Success(Other { value = raw.value }) }"),
            ("binder wrong return", "Result<Request, string> { Success(Request { value = raw.value }) }", "Request { Request { value = raw.value } }"),
            ("binder wrong arity", "decode(raw: Raw)", "decode(raw: Raw, extra: int)"),
            ("binder generic", "decode(raw: Raw)", "decode<T>(raw: Raw)"),
            ("binder inferred", "decode(raw: Raw): Result<Request, string>", "decode(raw: Raw)"),
            ("unknown binder", "(decode, mapBinding, mapSuccess, mapBusiness)", "(missing, mapBinding, mapSuccess, mapBusiness)"),
            ("binding mapper input", "mapBinding(error: string)", "mapBinding(error: bool)"),
            ("success mapper input", "mapSuccess(value: int)", "mapSuccess(value: string)"),
            ("business mapper input", "mapBusiness(error: bool)", "mapBusiness(error: int)"),
            ("binding mapper return", "mapBinding(error: string): Reply { Reply { code = 400 } }", "mapBinding(error: string): int { 400 }"),
            ("success mapper return", "mapSuccess(value: int): Reply { Reply { code = value } }", "mapSuccess(value: int): int { value }"),
            ("business mapper return", "mapBusiness(error: bool): Reply { Reply { code = 500 } }", "mapBusiness(error: bool): int { 500 }"),
            ("mapper wrong arity", "mapBinding(error: string)", "mapBinding(error: string, extra: int)"),
            ("mapper generic", "mapBinding(error: string)", "mapBinding<T>(error: string)"),
            ("mapper inferred", "mapBinding(error: string): Reply", "mapBinding(error: string)"),
            ("mapper async", "fn mapBinding(error: string)", "async fn mapBinding(error: string)"),
            ("unknown binding mapper", "(decode, mapBinding, mapSuccess, mapBusiness)", "(decode, missing, mapSuccess, mapBusiness)"),
            ("unknown mapper", "(decode, mapBinding, mapSuccess, mapBusiness)", "(decode, mapBinding, missing, mapBusiness)"),
            ("unknown business mapper", "(decode, mapBinding, mapSuccess, mapBusiness)", "(decode, mapBinding, mapSuccess, missing)"),
            ("plain Handle", "Handle(message: Request): Result<int, bool> { Success(trigger(Http { value = message.value })) }", "Handle(message: Request): int { message.value }")
        ];
        foreach (var (label, before, after) in bad)
        {
            Assert.Contains(before, Contract);
            var source = Contract.Replace(before, after, StringComparison.Ordinal);
            var result = DSharpCompiler.Compile(source, "invalid-http.ds");
            Assert.True(result.CSharp is null && result.Diagnostics.Any(d => d.Code == "DS2046" && d.Path == "invalid-http.ds"),
                $"Expected HTTP contract diagnostic for {label}; diagnostics: {string.Join("; ", result.Diagnostics)}");
            var error = Assert.Single(result.Diagnostics, d => d.Code == "DS2046");
            var handle = source.IndexOf("Handle(message", StringComparison.Ordinal);
            Assert.Contains("Handle(Request):", error.Message);
            Assert.Contains("invalid-http.ds(", error.Message);
            Assert.True(error.Span.Start >= source.IndexOf("trigger Http", StringComparison.Ordinal) && error.Span.Start < handle,
                $"Expected a descriptor reference location for {label}");
            if (label.StartsWith("unknown", StringComparison.Ordinal))
                Assert.True(error.Span.Start == source.IndexOf("missing", source.IndexOf("trigger Http", StringComparison.Ordinal), StringComparison.Ordinal),
                    $"Expected the missing reference token location for {label}");
        }

        string[] malformed =
        [
            "trigger Http<Reply>()",
            "trigger Http<Reply>(decode, mapBinding, mapSuccess)",
            "trigger Http<Reply>(decode, mapBinding, mapSuccess, mapBusiness, decode)",
            "trigger Http<Reply>(123, mapBinding, mapSuccess, mapBusiness)",
            "triger Http<Reply>(decode, mapBinding, mapSuccess, mapBusiness)"
        ];
        foreach (var descriptor in malformed)
        {
            var source = Contract.Replace("trigger Http<Reply>(decode, mapBinding, mapSuccess, mapBusiness)", descriptor, StringComparison.Ordinal);
            var result = DSharpCompiler.Compile(source, "malformed-http.ds");
            Assert.True(result.CSharp is null && result.Diagnostics.Any(d => d.Path == "malformed-http.ds"),
                $"Expected malformed HTTP descriptor rejection: {descriptor}");
        }
        var duplicate = Contract.Replace("trigger Http<Reply>(decode, mapBinding, mapSuccess, mapBusiness)",
            "trigger Http<Reply>(decode, mapBinding, mapSuccess, mapBusiness)\n    trigger Http<Reply>(decode, mapBinding, mapSuccess, mapBusiness)", StringComparison.Ordinal);
        Assert.Contains(DSharpCompiler.Compile(duplicate, "duplicate-http.ds").Diagnostics, d => d.Code == "DS2046" && d.Path == "duplicate-http.ds");

        AssertShadowResultRejected();
        AssertActorBypassRejected();
    }

    private static void AssertMultiFileIdentityAndImports()
    {
        const string contracts = """
            module Contracts
            type Request { value: int }
            type Raw { value: int }
            type Reply { code: int }
            fn decode(raw: Raw): Result<Request, string> { Success(Request { value = raw.value }) }
            fn mapBinding(error: string): Reply { Reply { code = 400 } }
            fn mapSuccess(value: int): Reply { Reply { code = value } }
            fn mapBusiness(error: bool): Reply { Reply { code = 500 } }
            """;
        const string worker = "module Workers\nimport Contracts\nactor Worker { trigger Http<Reply>(decode, Contracts.mapBinding, mapSuccess, mapBusiness) Handle(message: Request): Result<int, bool> { Success(message.value) } }";
        SourceFile[] sources = [new("contracts.ds", contracts), new("worker.ds", worker)];
        var result = DSharpCompiler.Compile(sources);
        Assert.Empty(result.Diagnostics);
        var actor = Assert.Single(result.Actors);
        Assert.Equal("Workers.Worker", actor.Name);
        Assert.Equal("Contracts.Request", actor.InputType);
        var http = Assert.Single(actor.HttpBindings);
        Assert.Equal("Contracts.Reply", http.ResponseType);
        Assert.Equal("worker.ds", http.SourcePath);
        Assert.Equal(worker.IndexOf("trigger Http", StringComparison.Ordinal), http.TriggerSpan.Start);
        Assert.Equal(worker.IndexOf("Reply>(", StringComparison.Ordinal), http.ResponseSpan.Start);
        Assert.Equal(worker.IndexOf("decode,", StringComparison.Ordinal), http.BinderSpan.Start);
        Assert.Equal(worker.IndexOf("Contracts.mapBinding", StringComparison.Ordinal), http.BindingMapperSpan.Start);

        const string missingWorker = "module Workers\nactor Worker { trigger Http<Contracts.Reply>(Contracts.decode, Contracts.mapBinding, Contracts.mapSuccess, Contracts.mapBusiness) Handle(message: Unit): Result<int, bool> { Success(1) } }";
        SourceFile[] missing = [new("contracts.ds", contracts), new("missing-worker.ds", missingWorker)];
        var missingResult = DSharpCompiler.Compile(missing);
        Assert.Null(missingResult.CSharp);
        Assert.Contains(missingResult.Diagnostics, d => d.Code == "DS2032" && d.Path == "missing-worker.ds" && d.Message.Contains("Handle(Unit): Result<int,bool>"));

        const string ambiguousWorker = "module Workers\nimport Contracts\nimport Other\nactor Worker { trigger Http<Reply>(decode, mapBinding, mapSuccess, mapBusiness) Handle(message: Request): Result<int, bool> { Success(message.value) } }";
        SourceFile[] ambiguous = [new("contracts.ds", contracts), new("other.ds", "module Other\nfn decode(raw: int): int { raw }"), new("ambiguous-worker.ds", ambiguousWorker)];
        var ambiguousResult = DSharpCompiler.Compile(ambiguous);
        Assert.Null(ambiguousResult.CSharp);
        Assert.Contains(ambiguousResult.Diagnostics, d => d.Code == "DS2031" && d.Path == "ambiguous-worker.ds" && d.Message.Contains("Handle(Request): Result<int,bool>"));

        const string distinctWorker = "module Workers\nimport Contracts\nimport Other\nactor Worker { trigger Http<Contracts.Reply>(decode, mapBinding, mapSuccess, mapBusiness) Handle(message: Other.Request): Result<int, bool> { Success(message.value) } }";
        SourceFile[] distinct = [new("contracts.ds", contracts), new("other.ds", "module Other\ntype Request { value: int }"), new("distinct-worker.ds", distinctWorker)];
        var distinctResult = DSharpCompiler.Compile(distinct);
        Assert.Null(distinctResult.CSharp);
        Assert.Contains(distinctResult.Diagnostics, d => d.Code == "DS2046" && d.Path == "distinct-worker.ds");

        var mapperContracts = contracts.Replace("fn decode(raw: Raw): Result<Request, string> { Success(Request { value = raw.value }) }\n", "", StringComparison.Ordinal);
        Assert.DoesNotContain("fn decode", mapperContracts);
        const string bindingModule = "module Binding\nimport Contracts\nfn decode(raw: Raw): Result<Request, string> { Success(Request { value = raw.value }) }";
        const string localShadow = "module Workers\nimport Contracts\nimport Binding\ntype decode { value: int }\nactor Worker { trigger Http<Reply>(decode, mapBinding, mapSuccess, mapBusiness) Handle(message: Request): Result<int, bool> { Success(message.value) } }";
        SourceFile[] localShadowSources = [new("contracts.ds", mapperContracts), new("binding.ds", bindingModule), new("shadow-worker.ds", localShadow)];
        var localShadowResult = DSharpCompiler.Compile(localShadowSources);
        Assert.Null(localShadowResult.CSharp);
        var localShadowError = Assert.Single(localShadowResult.Diagnostics, d => d.Code == "DS2046");
        Assert.Equal("shadow-worker.ds", localShadowError.Path);
        Assert.Equal(localShadow.IndexOf("decode, mapBinding", StringComparison.Ordinal), localShadowError.Span.Start);
        Assert.Contains("Handle(Contracts.Request): Result<int, bool>", localShadowError.Message);

        var qualifiedWorker = localShadow.Replace("(decode, mapBinding", "(Binding.decode, mapBinding", StringComparison.Ordinal);
        SourceFile[] qualifiedSources = [new("contracts.ds", mapperContracts), new("binding.ds", bindingModule), new("qualified-worker.ds", qualifiedWorker)];
        var qualifiedResult = DSharpCompiler.Compile(qualifiedSources);
        Assert.True(qualifiedResult.CSharp is not null && qualifiedResult.Diagnostics.Count == 0,
            $"Expected qualified binder to bypass local value shadow: {string.Join("; ", qualifiedResult.Diagnostics)}");
        Assert.Equal("Binding.decode", Assert.Single(Assert.Single(qualifiedResult.Actors).HttpBindings).Binder);

        const string ambiguousValue = "module Workers\nimport Contracts\nimport Binding\nimport Types\nactor Worker { trigger Http<Reply>(decode, mapBinding, mapSuccess, mapBusiness) Handle(message: Request): Result<int, bool> { Success(message.value) } }";
        SourceFile[] ambiguousValueSources = [new("contracts.ds", mapperContracts), new("binding.ds", bindingModule), new("types.ds", "module Types\ntype decode { value: int }"), new("value-worker.ds", ambiguousValue)];
        var ambiguousValueResult = DSharpCompiler.Compile(ambiguousValueSources);
        Assert.Null(ambiguousValueResult.CSharp);
        var ambiguousValueError = Assert.Single(ambiguousValueResult.Diagnostics, d => d.Code == "DS2031");
        Assert.Equal("value-worker.ds", ambiguousValueError.Path);
        Assert.Equal(ambiguousValue.IndexOf("decode, mapBinding", StringComparison.Ordinal), ambiguousValueError.Span.Start);
        Assert.Contains("Handle(Request): Result<int,bool>", ambiguousValueError.Message);

        SourceFile[] hidden =
        [
            new("secret.ds", "module A\ntype Secret<T> { data: T }"),
            new("hidden.ds", "module B\nactor Worker { trigger Http<__dsharp_module_symbol_0<int>>(decode, mb, ms, me) Handle(message: Unit): Result<int, bool> { Success(1) } }")
        ];
        var hiddenResult = DSharpCompiler.Compile(hidden);
        Assert.True(hiddenResult.CSharp is null && hiddenResult.Diagnostics.Any(d => d.Path == "hidden.ds"),
            "Expected an unimported synthetic source type to be rejected.");
    }

    private static void AssertShadowResultRejected()
    {
        const string source = """
            module Demo
            type Request { value: int }
            type Raw { value: int }
            type Reply { value: int }
            type Result<T, E> { | Box(T) }
            fn decode(raw: Raw): Result<Request, string> { Result.Box(Request { value = raw.value }) }
            fn mapBinding(error: string): Reply { Reply { value = 1 } }
            fn mapSuccess(value: int): Reply { Reply { value = value } }
            fn mapBusiness(error: bool): Reply { Reply { value = 1 } }
            actor Worker {
                trigger Http<Reply>(decode, mapBinding, mapSuccess, mapBusiness)
                Handle(message: Request): Result<int, bool> { Result.Box(message.value) }
            }
            """;
        var result = DSharpCompiler.Compile(source, "shadow-result-http.ds");
        Assert.True(result.CSharp is null && result.Diagnostics.Any(d => d.Code == "DS2046" && d.Path == "shadow-result-http.ds"),
            $"Expected user-defined Result to be rejected; diagnostics: {string.Join("; ", result.Diagnostics)}");

        // The actor file does not import Shadow; its Handle still refers to
        // intrinsic Result even though a same-module binder was declared in a
        // different file which imports the user-defined Shadow.Result.
        const string actorSource = """
            module Workers
            import Contracts
            actor Worker {
                trigger Http<Reply>(decode, mapBinding, mapSuccess, mapBusiness)
                Handle(message: Request): Result<int, bool> { Success(message.value) }
            }
            """;
        SourceFile[] sources =
        [
            new("contracts.ds", """
                module Contracts
                type Request { value: int }
                type Raw { value: int }
                type Reply { value: int }
                fn mapBinding(error: string): Reply { Reply { value = 1 } }
                fn mapSuccess(value: int): Reply { Reply { value = value } }
                fn mapBusiness(error: bool): Reply { Reply { value = 1 } }
                """),
            new("shadow.ds", "module Shadow\ntype Result<T, E> { | Box(T) }"),
            new("binder.ds", """
                module Workers
                import Contracts
                import Shadow
                fn decode(raw: Raw): Shadow.Result<Request, string> { Shadow.Result.Box(Request { value = raw.value }) }
                """),
            new("actor.ds", actorSource)
        ];
        var shadowBinder = DSharpCompiler.Compile(sources);
        Assert.Null(shadowBinder.CSharp);
        var error = Assert.Single(shadowBinder.Diagnostics, d => d.Code == "DS2046");
        Assert.Equal("actor.ds", error.Path);
        Assert.Equal(actorSource.IndexOf("decode,", StringComparison.Ordinal), error.Span.Start);
        Assert.Contains("Handle(Contracts.Request): Result<int, bool>", error.Message);
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
            var result = DSharpCompiler.Compile(Contract + "\n" + bypass, "http-bypass.ds");
            Assert.True(result.CSharp is null && result.Diagnostics.Count > 0,
                $"Expected actor isolation rejection for: {bypass}");
        }
    }
}
