using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class CronContractTests
{
    [Fact]
    public void CheckedCronCandidatesRetainOnlyDeclaredMetadata()
    {
        const string source = """
            module CronContract
            type Request { value: int }
            type Cron { value: int }
            actor Worker {
                trigger Cron<Request>("night\u006cy", "not a cron expression")
                trigger(value: Cron): int { value.value + 1 }
                Handle(message: Request): Result<int, string> { Success(trigger(Cron { value = message.value })) }
            }
            actor Ping {
                trigger Cron<Unit>("ping", "0 0 * * *")
                Handle(message: Unit): Unit { () }
            }
            actor All {
                trigger Queue<Request>("jobs")
                trigger Event<Request>()
                trigger Cron<Request>("all", "opaque")
                Handle(message: Request): int { message.value }
            }
            actor QueueOnly { trigger Queue<Request>("q") Handle(message: Request): int { message.value } }
            actor EventOnly { trigger Event<Request>() Handle(message: Request): int { message.value } }
            actor Plain { Handle(message: Request): int { message.value } }
            """;
        var syntax = new Parser(Lexer.Lex(source, "cron.ds"), "cron.ds").Parse();
        var actorSyntax = Assert.Single(syntax.Actors, actor => actor.Name.Text == "Worker");
        var trigger = Assert.Single(actorSyntax.CronBindings);
        Assert.Equal("trigger", trigger.Keyword.Text);
        Assert.Equal("Request", trigger.MessageType.Text);
        Assert.Contains(actorSyntax.Methods, method => method.Name.Text == "trigger");

        var result = DSharpCompiler.Compile(source, "cron.ds");
        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.CSharp);
        var worker = Assert.Single(result.Actors, actor => actor.Name == "Worker");
        Assert.Equal("Request", worker.InputType);
        Assert.Equal("Result<int, string>", worker.ResultType);
        var candidate = Assert.Single(worker.CronBindings);
        Assert.Equal("nightly", candidate.ScheduleName);
        Assert.Equal("not a cron expression", candidate.Expression);
        Assert.Equal("Request", candidate.MessageType);
        Assert.Equal("cron.ds", candidate.SourcePath);
        Assert.Equal(source.IndexOf("trigger Cron<Request>", StringComparison.Ordinal), candidate.TriggerSpan.Start);
        Assert.Equal(source.IndexOf("Request>(", StringComparison.Ordinal), candidate.TypeSpan.Start);
        Assert.Equal(source.IndexOf("\"night\\u006cy\"", StringComparison.Ordinal), candidate.NameSpan.Start);
        Assert.Equal(source.IndexOf("\"not a cron expression\"", StringComparison.Ordinal), candidate.ExpressionSpan.Start);
        Assert.Empty(worker.QueueBindings);
        Assert.Empty(worker.EventBindings);
        Assert.Equal("Unit", Assert.Single(Assert.Single(result.Actors, actor => actor.Name == "Ping").CronBindings).MessageType);
        var all = Assert.Single(result.Actors, actor => actor.Name == "All");
        Assert.Equal("Request", Assert.Single(all.QueueBindings).MessageType);
        Assert.Equal("Request", Assert.Single(all.EventBindings).MessageType);
        Assert.Equal("Request", Assert.Single(all.CronBindings).MessageType);
        var queueOnly = Assert.Single(result.Actors, actor => actor.Name == "QueueOnly");
        Assert.Single(queueOnly.QueueBindings);
        Assert.Empty(queueOnly.CronBindings);
        var eventOnly = Assert.Single(result.Actors, actor => actor.Name == "EventOnly");
        Assert.Single(eventOnly.EventBindings);
        Assert.Empty(eventOnly.CronBindings);
        var plain = Assert.Single(result.Actors, actor => actor.Name == "Plain");
        Assert.Empty(plain.CronBindings);
        Assert.Empty(plain.QueueBindings);
        Assert.Empty(plain.EventBindings);

        AssertCronDataRules();
        AssertCronSourceIdentity();
    }

    private static void AssertCronDataRules()
    {
        string[] valid =
        [
            "type Request { value: int }",
            "type Box<T> { value: T }\ntype Request { value: Box<int> }",
            "type Request { | End | Next(Request) }"
        ];
        foreach (var declarations in valid)
        {
            var source = $"module Demo\n{declarations}\nactor Worker {{ trigger Cron<Request>(\"job\", \"opaque\") Handle(message: Request): int {{ 1 }} }}";
            var compiled = DSharpCompiler.Compile(source, "valid-cron-data.ds");
            Assert.True(compiled.CSharp is not null && compiled.Diagnostics.Count == 0,
                $"Expected valid Cron data: {declarations}; diagnostics: {string.Join("; ", compiled.Diagnostics)}");
        }
        string[] invalid =
        [
            "type Request { value: int? }",
            "type Request { value: List<int> }",
            "type Request { value: Option<int> }",
            "type Request { value: (int, string) }",
            "type Box<T> { value: T }\ntype Request { value: Box<int?> }",
            "type Box<T> { value: T }\ntype Request { value: Box<List<int>> }",
            "type Request { | End | Next(Request) | Bad(List<int>) }",
            "type Node<T> { | End | Next(Node<T>) | Bad(T) }\ntype Request { value: Node<List<int>> }",
            "type Box<T> { value: T }\ntype Expand<T> { | End | Next(Expand<Box<T>>) }\ntype Request { value: Expand<int> }"
        ];
        foreach (var declarations in invalid)
        {
            var source = $"module Demo\n{declarations}\nactor Worker {{ trigger Cron<Request>(\"job\", \"opaque\") Handle(message: Request): int {{ 1 }} }}";
            var compiled = DSharpCompiler.Compile(source, "invalid-cron-data.ds");
            Assert.True(compiled.CSharp is null && compiled.Diagnostics.Any(d => d.Code == "DS2045" && d.Path == "invalid-cron-data.ds"),
                $"Expected Cron data diagnostic: {declarations}; diagnostics: {string.Join("; ", compiled.Diagnostics)}");
        }
        (string Setup, string FieldType)[] capabilities =
        [
            ("interface Store { read(): int }", "Store"),
            ("resource Reader { read(): int { 1 } release(): Unit { () } }", "Reader"),
            ("", "(int) -> int"),
            ("", "Task<int>"),
            ("", "MutableList<int>")
        ];
        foreach (var (setup, fieldType) in capabilities)
        {
            var source = $"module Demo\n{setup}\ntype Request {{ value: {fieldType} }}\nactor Worker {{ trigger Cron<Request>(\"job\", \"opaque\") Handle(message: Request): int {{ 1 }} }}";
            var compiled = DSharpCompiler.Compile(source, "cron-capability.ds");
            var typeStart = source.IndexOf($"value: {fieldType}", StringComparison.Ordinal) + "value: ".Length;
            Assert.True(compiled.CSharp is null && compiled.Diagnostics.Any(d => d.Code == "DS2015" && d.Path == "cron-capability.ds" && d.Span.Start == typeStart),
                $"Expected immutable-data rejection before Cron binding for {fieldType}; diagnostics: {string.Join("; ", compiled.Diagnostics)}");
        }
    }

    [Fact]
    public void CronErrorsPreserveExpectedContractAndActorIsolation()
    {
        (string Member, string Input)[] cases =
        [
            ("trigger Cron<int>(\"job\", \"opaque\")", "int"),
            ("trigger Cron<string>(\"job\", \"opaque\")", "string"),
            ("trigger Cron<Request?>(\"job\", \"opaque\")", "Request?"),
            ("trigger Cron<List<Request>>(\"job\", \"opaque\")", "List<Request>"),
            ("trigger Cron<Requset>(\"job\", \"opaque\")", "Request"),
            ("trigger Cron<Other>(\"job\", \"opaque\")", "Request"),
            ("trigger Cron<Request>(\"\", \"opaque\")", "Request"),
            ("trigger Cron<Request>(\"job\", \"\")", "Request"),
            ("trigger Cron<Request>(\"job\", \"opaque\") trigger Cron<Request>(\"job\", \"other\")", "Request"),
            ("trigger Cron<Request>(\"nightly\", \"a\") trigger Cron<Request>(\"night\\u006cy\", \"b\")", "Request"),
            ("trigger Cron<Request>(\"bad\\z\", \"opaque\")", "Request"),
            ("trigger Cron<Request>(\"job\", \"bad\\z\")", "Request")
        ];
        foreach (var (member, input) in cases)
        {
            var source = $"module Demo\ntype Request {{ value: int }}\ntype Other {{ value: int }}\nactor Worker {{ {member} Handle(message: {input}): int {{ 1 }} }}";
            var compiled = DSharpCompiler.Compile(source, "invalid-cron.ds");
            Assert.Null(compiled.CSharp);
            var error = Assert.Single(compiled.Diagnostics, d => d.Code == "DS2045");
            Assert.Equal("invalid-cron.ds", error.Path);
            var handle = source.IndexOf("Handle(message", StringComparison.Ordinal);
            var column = handle - source.LastIndexOf('\n', handle);
            Assert.Contains($"Handle({input}): int", error.Message);
            Assert.Contains($"invalid-cron.ds(4,{column})", error.Message);
            Assert.True(error.Span.Start >= source.IndexOf("trigger Cron", StringComparison.Ordinal) && error.Span.Start < handle,
                $"Expected Cron declaration diagnostic for: {member}");
        }
        string[] malformed =
        [
            "triger Cron<Request>(\"job\", \"x\")",
            "trigger Cron<Request>()",
            "trigger Cron<Request>(\"job\")",
            "trigger Cron<Request>(123, \"x\")",
            "trigger Cron<Request>(\"job\", 123)",
            "trigger Cron(\"job\", \"x\")"
        ];
        foreach (var member in malformed)
        {
            var source = $"module Demo\ntype Request {{ value: int }}\nactor Worker {{ {member} Handle(message: Request): int {{ 1 }} }}";
            var compiled = DSharpCompiler.Compile(source, "malformed-cron.ds");
            Assert.True(compiled.CSharp is null && compiled.Diagnostics.Any(d => d.Path == "malformed-cron.ds"),
                $"Expected malformed Cron diagnostic for: {member}");
        }
        AssertForgedLiteralRejected();
        AssertActorBypassRejected();
    }

    private static void AssertForgedLiteralRejected()
    {
        const string valid = "module Demo\ntype Request { value: int }\nactor Worker { trigger Cron<Request>(\"job\", \"opaque\") Handle(message: Request): int { 1 } }";
        var syntax = new Parser(Lexer.Lex(valid, "forged-cron.ds"), "forged-cron.ds").Parse();
        var actor = Assert.Single(syntax.Actors);
        var binding = Assert.Single(actor.CronBindings);
        string[] forged = ["abc", "$\"value\"", "\"unfinished", "\"first\" \"second\"", "\"line\nnext\""];
        foreach (var literal in forged)
        foreach (var nameField in new[] { true, false })
        {
            var original = nameField ? binding.ScheduleName : binding.Expression;
            var token = original with { Text = literal };
            var alteredBinding = nameField ? binding with { ScheduleName = token } : binding with { Expression = token };
            var alteredActor = actor with { CronBindings = [alteredBinding] };
            var altered = syntax with { Actors = [alteredActor] };
            var error = Assert.Throws<SourceError>(() => DSharp.Compiler.Binder.Bind(altered, "forged-cron.ds"));
            Assert.True(error.Diagnostic.Code == "DS2045" && error.Diagnostic.Path == "forged-cron.ds" && error.Diagnostic.Span == token.Span &&
                error.Diagnostic.Message.Contains("Handle(Request): int"),
                $"Expected located Cron literal diagnostic for {(nameField ? "name" : "expression")}: {literal}");
        }
    }

    private static void AssertCronSourceIdentity()
    {
        const string worker = "module Workers\nimport Contracts\nactor Worker { trigger Cron<Request>(\"job\", \"opaque\") Handle(message: Request): int { message.value } }";
        SourceFile[] sources =
        [
            new("contracts.ds", "module Contracts\ntype Request { value: int }"),
            new("worker.ds", worker)
        ];
        var result = DSharpCompiler.Compile(sources);
        Assert.Empty(result.Diagnostics);
        var actor = Assert.Single(result.Actors);
        Assert.Equal("Workers.Worker", actor.Name);
        Assert.Equal("Contracts.Request", actor.InputType);
        var candidate = Assert.Single(actor.CronBindings);
        Assert.Equal("Contracts.Request", candidate.MessageType);
        Assert.Equal("worker.ds", candidate.SourcePath);
        Assert.Equal(worker.IndexOf("trigger Cron", StringComparison.Ordinal), candidate.TriggerSpan.Start);
        Assert.Equal(worker.IndexOf("Request>(", StringComparison.Ordinal), candidate.TypeSpan.Start);
        Assert.Equal(worker.IndexOf("\"job\"", StringComparison.Ordinal), candidate.NameSpan.Start);
        Assert.Equal(worker.IndexOf("\"opaque\"", StringComparison.Ordinal), candidate.ExpressionSpan.Start);

        SourceFile[] mismatch =
        [
            new("left.ds", "module Left\ntype Request { value: int }"),
            new("right.ds", "module Right\ntype Request { value: int }"),
            new("worker.ds", "module Workers\nimport Left\nimport Right\nactor Worker { trigger Cron<Left.Request>(\"job\", \"x\") Handle(message: Right.Request): int { 1 } }")
        ];
        var invalid = DSharpCompiler.Compile(mismatch);
        Assert.Null(invalid.CSharp);
        Assert.Contains(invalid.Diagnostics, d => d.Code == "DS2045" && d.Path == "worker.ds" && d.Message.Contains("Handle(Right.Request): int"));

        const string missing = "module Workers\nactor Worker { trigger Cron<Contracts.Request>(\"job\", \"x\") Handle(message: Unit): int { 1 } }";
        SourceFile[] missingImport = [new("contracts.ds", "module Contracts\ntype Request { value: int }"), new("missing.ds", missing)];
        AssertResolutionError(missingImport, "missing.ds", missing, "Contracts.Request", "DS2032", 2);
        const string ambiguous = "module Workers\nimport Left\nimport Right\nactor Worker { trigger Cron<Request>(\"job\", \"x\") Handle(message: Unit): int { 1 } }";
        SourceFile[] ambiguousSources = [new("left.ds", "module Left\ntype Request { value: int }"), new("right.ds", "module Right\ntype Request { value: int }"), new("ambiguous.ds", ambiguous)];
        AssertResolutionError(ambiguousSources, "ambiguous.ds", ambiguous, "Request>(", "DS2031", 4);

        SourceFile[] hidden =
        [
            new("secret.ds", "module A\ntype Secret<T> { data: T }"),
            new("hidden.ds", "module B\ntype Outer<T> { value: T }\nactor Worker { trigger Cron<Outer<__dsharp_module_symbol_0<int>>>(\"job\", \"x\") Handle(message: Outer<__dsharp_module_symbol_0<int>>): int { 1 } }")
        ];
        var inaccessible = DSharpCompiler.Compile(hidden);
        Assert.True(inaccessible.CSharp is null && inaccessible.Diagnostics.Any(d => d.Path == "hidden.ds"),
            "Expected a source spelling of the internal generic symbol to be rejected.");
    }

    private static void AssertResolutionError(SourceFile[] sources, string path, string source, string typeText, string code, int line)
    {
        var result = DSharpCompiler.Compile(sources);
        Assert.Null(result.CSharp);
        var error = Assert.Single(result.Diagnostics, d => d.Code == code);
        Assert.Equal(path, error.Path);
        Assert.Equal(source.IndexOf(typeText, StringComparison.Ordinal), error.Span.Start);
        Assert.Contains("Handle(Unit): int", error.Message);
        var handle = source.IndexOf("Handle(message", StringComparison.Ordinal);
        var column = handle - source.LastIndexOf('\n', handle);
        Assert.Contains($"{path}({line},{column})", error.Message);
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
            var source = $"module Demo\ntype Request {{ value: int }}\nactor Worker {{ trigger Cron<Request>(\"job\", \"opaque\") Handle(message: Request): int {{ message.value }} }}\n{bypass}";
            var result = DSharpCompiler.Compile(source, "cron-bypass.ds");
            Assert.True(result.CSharp is null && result.Diagnostics.Count > 0,
                $"Expected actor isolation diagnostic for: {bypass}");
        }
    }
}
