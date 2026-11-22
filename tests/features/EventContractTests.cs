using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class EventContractTests
{
    [Fact]
    public void ExplicitEventCandidatesPreserveActorContractAndDoNotSubscribe()
    {
        const string source = """
            module EventContract
            type Request { value: int }
            type Event { value: int }
            actor Worker {
                trigger Event<Request>()
                trigger(value: Event): int { value.value + 1 }
                Handle(message: Request): Result<int, string> { Success(trigger(Event { value = message.value })) }
            }
            actor Ping {
                trigger Event<Unit>()
                Handle(message: Unit): Unit { () }
            }
            actor Both {
                trigger Queue<Request>("orders")
                trigger Event<Request>()
                Handle(message: Request): int { message.value }
            }
            actor QueueOnly {
                trigger Queue<Request>("queued")
                Handle(message: Request): int { message.value }
            }
            actor Plain { Handle(message: Request): int { message.value } }
            """;
        var parsed = new Parser(Lexer.Lex(source, "event.ds"), "event.ds").Parse();
        var syntax = Assert.Single(parsed.Actors, actor => actor.Name.Text == "Worker");
        var trigger = Assert.Single(syntax.EventBindings);
        Assert.Equal("trigger", trigger.Keyword.Text);
        Assert.Equal("Request", trigger.MessageType.Text);
        Assert.Contains(syntax.Methods, method => method.Name.Text == "trigger");

        var result = DSharpCompiler.Compile(source, "event.ds");
        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.CSharp);
        var worker = Assert.Single(result.Actors, actor => actor.Name == "Worker");
        Assert.Equal("Request", worker.InputType);
        Assert.Equal("Result<int, string>", worker.ResultType);
        var candidate = Assert.Single(worker.EventBindings);
        Assert.Equal("Request", candidate.MessageType);
        Assert.Equal("event.ds", candidate.SourcePath);
        Assert.Equal(source.IndexOf("trigger Event<Request>", StringComparison.Ordinal), candidate.TriggerSpan.Start);
        Assert.Equal(source.IndexOf("Request>()", StringComparison.Ordinal), candidate.TypeSpan.Start);
        Assert.Empty(worker.QueueBindings);
        Assert.Equal("Unit", Assert.Single(Assert.Single(result.Actors, actor => actor.Name == "Ping").EventBindings).MessageType);
        var both = Assert.Single(result.Actors, actor => actor.Name == "Both");
        Assert.Equal("Request", Assert.Single(both.EventBindings).MessageType);
        Assert.Equal("Request", Assert.Single(both.QueueBindings).MessageType);
        var queueOnly = Assert.Single(result.Actors, actor => actor.Name == "QueueOnly");
        Assert.Empty(queueOnly.EventBindings);
        Assert.Single(queueOnly.QueueBindings);
        var plain = Assert.Single(result.Actors, actor => actor.Name == "Plain");
        Assert.Empty(plain.EventBindings);
        Assert.Empty(plain.QueueBindings);

        // This is a test-only explicit association with the public actor contract,
        // not application registration or evidence of runtime subscriptions.
        var explicitAssociation = (Identity: "test-subscriber", Actor: worker, Event: candidate);
        Assert.Equal("test-subscriber", explicitAssociation.Identity);
        Assert.Equal("Worker", explicitAssociation.Actor.Name);
        Assert.Equal(explicitAssociation.Actor.InputType, explicitAssociation.Event.MessageType);
    }

    [Fact]
    public void EventMessageEligibilityUsesTheSharedCheckedDataRules()
    {
        string[] accepted =
        [
            "type Request { value: int }",
            "type Part { number: int }\ntype Request { part: Part }",
            "type Box<T> { value: T }\ntype Request { value: Box<int> }",
            "type Choice { | Empty | Number(int) }\ntype Request { choice: Choice }",
            "type Request { | End | Next(Request) }"
        ];
        foreach (var declarations in accepted)
        {
            var source = $"module Demo\n{declarations}\nactor Worker {{ trigger Event<Request>() Handle(message: Request): int {{ 42 }} }}";
            var result = DSharpCompiler.Compile(source, "valid-event.ds");
            Assert.True(result.CSharp is not null && result.Diagnostics.Count == 0,
                $"Expected event data to compile: {declarations}; diagnostics: {string.Join("; ", result.Diagnostics)}");
        }

        string[] rejected =
        [
            "type Request { value: int? }",
            "type Request { value: List<int> }",
            "type Request { value: Option<int> }",
            "type Request { value: (int, string) }",
            "type Box<T> { value: T }\ntype Request { value: Box<int?> }",
            "type Box<T> { value: T }\ntype Request { value: Box<List<int>> }",
            "type Request { | End | Next(Request) | Bad(List<int>) }",
            "type Node<T> { | End | Next(Node<T>) | Bad(T) }\ntype Request { value: Node<List<int>> }",
            "type Box<T> { value: T }\ntype Expand<T> { | End | Next(Expand<Box<T>>) }\ntype Request { value: Expand<int> }",
            "type Expand<T> { | End | Next(Expand<List<T>>) }\ntype Request { value: Expand<int> }"
        ];
        foreach (var declarations in rejected)
        {
            var source = $"module Demo\n{declarations}\nactor Worker {{ trigger Event<Request>() Handle(message: Request): int {{ 42 }} }}";
            var result = DSharpCompiler.Compile(source, "invalid-event-shape.ds");
            Assert.True(result.CSharp is null && result.Diagnostics.Any(d => d.Code == "DS2044" && d.Path == "invalid-event-shape.ds"),
                $"Expected event shape diagnostic for: {declarations}; diagnostics: {string.Join("; ", result.Diagnostics)}");
        }

        const string expandingQueue = "module Demo\ntype Box<T> { value: T }\ntype Expand<T> { | End | Next(Expand<Box<T>>) }\ntype Request { value: Expand<int> }\nactor Worker { trigger Queue<Request>(\"q\") Handle(message: Request): int { 42 } }";
        var queueResult = DSharpCompiler.Compile(expandingQueue, "expanding-queue.ds");
        Assert.Null(queueResult.CSharp);
        Assert.Contains(queueResult.Diagnostics, d => d.Code == "DS2043" && d.Path == "expanding-queue.ds");

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
            var source = $"module Demo\n{setup}\ntype Request {{ value: {fieldType} }}\nactor Worker {{ trigger Event<Request>() Handle(message: Request): int {{ 42 }} }}";
            var result = DSharpCompiler.Compile(source, "event-capability.ds");
            var typeStart = source.IndexOf($"value: {fieldType}", StringComparison.Ordinal) + "value: ".Length;
            Assert.True(result.CSharp is null && result.Diagnostics.Any(d => d.Code == "DS2015" &&
                d.Path == "event-capability.ds" && d.Span.Start == typeStart),
                $"Expected existing immutable-data diagnostic for event payload {fieldType}; diagnostics: {string.Join("; ", result.Diagnostics)}");
        }
    }

    [Fact]
    public void InvalidEventContractsReportExpectedHandleAndLocation()
    {
        (string Member, string Input)[] cases =
        [
            ("trigger Event<int>()", "int"),
            ("trigger Event<string>()", "string"),
            ("trigger Event<Request?>()", "Request?"),
            ("trigger Event<List<Request>>()", "List<Request>"),
            ("trigger Event<(int,string)>()", "(int,string)"),
            ("trigger Event<Requset>()", "Request"),
            ("trigger Event<Other>()", "Request"),
            ("trigger Event<Request>() trigger Event<Request>()", "Request")
        ];
        foreach (var (member, input) in cases)
        {
            var source = $"module Demo\ntype Request {{ value: int }}\ntype Other {{ value: int }}\nactor Worker {{ {member} Handle(message: {input}): int {{ 1 }} }}";
            var result = DSharpCompiler.Compile(source, "invalid-event.ds");
            Assert.Null(result.CSharp);
            var error = Assert.Single(result.Diagnostics, d => d.Code == "DS2044");
            Assert.Equal("invalid-event.ds", error.Path);
            var handle = source.IndexOf("Handle(message", StringComparison.Ordinal);
            var column = handle - source.LastIndexOf('\n', handle);
            Assert.Contains($"invalid-event.ds(4,{column})", error.Message);
            var inputDisplay = input == "(int,string)" ? "(int, string)" : input;
            Assert.Contains($"Handle({inputDisplay}): int", error.Message);
            Assert.True(error.Span.Start >= source.IndexOf("trigger Event", StringComparison.Ordinal) && error.Span.Start < handle,
                $"Expected event declaration diagnostic for: {member}");
        }

        string[] malformed =
        [
            "triger Event<Request>()",
            "trigger Event<Request>(\"name\")",
            "trigger Event()",
            "trigger Event<Request>"
        ];
        foreach (var member in malformed)
        {
            var source = $"module Demo\ntype Request {{ value: int }}\nactor Worker {{ {member} Handle(message: Request): int {{ 1 }} }}";
            var result = DSharpCompiler.Compile(source, "malformed-event.ds");
            Assert.True(result.CSharp is null && result.Diagnostics.Any(d => d.Path == "malformed-event.ds"),
                $"Expected malformed event diagnostic for: {member}");
        }
    }

    [Fact]
    public void EventBindingsRespectMultiFileIdentityAndActorIsolation()
    {
        const string worker = "module Workers\nimport Contracts\nactor Worker { trigger Event<Request>() Handle(message: Request): int { message.value } }";
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
        var candidate = Assert.Single(actor.EventBindings);
        Assert.Equal("Contracts.Request", candidate.MessageType);
        Assert.Equal("worker.ds", candidate.SourcePath);
        Assert.Equal(worker.IndexOf("trigger Event", StringComparison.Ordinal), candidate.TriggerSpan.Start);
        Assert.Equal(worker.IndexOf("Request>()", StringComparison.Ordinal), candidate.TypeSpan.Start);

        SourceFile[] mismatch =
        [
            new("left.ds", "module Left\ntype Request { value: int }"),
            new("right.ds", "module Right\ntype Request { value: int }"),
            new("worker.ds", "module Workers\nimport Left\nimport Right\nactor Worker { trigger Event<Left.Request>() Handle(message: Right.Request): int { 1 } }")
        ];
        var invalid = DSharpCompiler.Compile(mismatch);
        Assert.Null(invalid.CSharp);
        Assert.Contains(invalid.Diagnostics, d => d.Code == "DS2044" && d.Path == "worker.ds" && d.Message.Contains("Right.Request"));

        const string unimportedWorker = "module Workers\nactor Worker { trigger Event<Contracts.Request>() Handle(message: Unit): int { 1 } }";
        SourceFile[] missingImport =
        [
            new("contracts.ds", "module Contracts\ntype Request { value: int }"),
            new("unimported-worker.ds", unimportedWorker)
        ];
        AssertResolutionError(missingImport, "unimported-worker.ds", unimportedWorker, "Contracts.Request", "DS2032", 2);

        const string ambiguousWorker = "module Workers\nimport Left\nimport Right\nactor Worker { trigger Event<Request>() Handle(message: Unit): int { 1 } }";
        SourceFile[] ambiguous =
        [
            new("left.ds", "module Left\ntype Request { value: int }"),
            new("right.ds", "module Right\ntype Request { value: int }"),
            new("ambiguous-worker.ds", ambiguousWorker)
        ];
        AssertResolutionError(ambiguous, "ambiguous-worker.ds", ambiguousWorker, "Request>()", "DS2031", 4);

        SourceFile[] hidden =
        [
            new("secret.ds", "module A\ntype Secret<T> { data: T }"),
            new("unimported.ds", "module B\ntype Outer<T> { value: T }\nactor Worker { trigger Event<Outer<__dsharp_module_symbol_0<int>>>() Handle(message: Outer<__dsharp_module_symbol_0<int>>): int { 1 } }")
        ];
        var inaccessible = DSharpCompiler.Compile(hidden);
        Assert.True(inaccessible.CSharp is null && inaccessible.Diagnostics.Any(d => d.Path == "unimported.ds"),
            "Expected the source spelling of an internal generic symbol to be rejected.");

        string[] bypasses =
        [
            "fn bad(): Unit { Worker() }",
            "fn bad(): int { Worker().Handle(Request { value = 1 }) }",
            "fn bad(): int { let callback = Worker().Handle\n 42 }",
            "fn bad(worker: Worker): int { 1 }"
        ];
        foreach (var bypass in bypasses)
        {
            var source = $"module Demo\ntype Request {{ value: int }}\nactor Worker {{ trigger Event<Request>() Handle(message: Request): int {{ message.value }} }}\n{bypass}";
            var rejected = DSharpCompiler.Compile(source, "event-bypass.ds");
            Assert.True(rejected.CSharp is null && rejected.Diagnostics.Count > 0,
                $"Expected actor isolation diagnostic for: {bypass}");
        }
    }

    private static void AssertResolutionError(SourceFile[] sources, string path, string source, string typeText, string code, int handleLine)
    {
        var result = DSharpCompiler.Compile(sources);
        Assert.Null(result.CSharp);
        var error = Assert.Single(result.Diagnostics, d => d.Code == code);
        Assert.Equal(path, error.Path);
        Assert.Equal(source.IndexOf(typeText, StringComparison.Ordinal), error.Span.Start);
        Assert.Contains("Handle(Unit): int", error.Message);
        var handle = source.IndexOf("Handle(message", StringComparison.Ordinal);
        var column = handle - source.LastIndexOf('\n', handle);
        Assert.Contains($"{path}({handleLine},{column})", error.Message);
    }
}
