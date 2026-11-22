using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class QueueContractTests
{
    [Fact]
    public void QueueBindingsRetainCheckedActorContractsAndLocations()
    {
        const string source = """
            module QueueContract
            type Request { value: int }
            type Queue { value: int }
            actor Worker {
                trigger Queue<Request>("orders")
                trigger(value: Queue): int { value.value + 1 }
                Handle(message: Request): Result<int, string> { Success(trigger(Queue { value = message.value })) }
            }
            actor Ping {
                trigger Queue<Unit>("ping")
                Handle(message: Unit): Unit { () }
            }
            actor Plain { Handle(message: Request): int { message.value } }
            """;
        var parsed = new Parser(Lexer.Lex(source, "queue.ds"), "queue.ds").Parse();
        var syntax = Assert.Single(parsed.Actors, actor => actor.Name.Text == "Worker");
        var trigger = Assert.Single(syntax.QueueBindings);
        Assert.Equal("trigger", trigger.Keyword.Text);
        Assert.Equal("Request", trigger.MessageType.Text);
        Assert.Contains(syntax.Methods, method => method.Name.Text == "trigger");

        var compiled = DSharpCompiler.Compile(source, "queue.ds");
        Assert.Empty(compiled.Diagnostics);
        Assert.NotNull(compiled.CSharp);
        var worker = Assert.Single(compiled.Actors, actor => actor.Name == "Worker");
        Assert.Equal("Request", worker.InputType);
        Assert.Equal("Result<int, string>", worker.ResultType);
        var queue = Assert.Single(worker.QueueBindings);
        Assert.Equal("orders", queue.QueueName);
        Assert.Equal("Request", queue.MessageType);
        Assert.Equal("queue.ds", queue.SourcePath);
        Assert.Equal(source.IndexOf("trigger Queue<Request>", StringComparison.Ordinal), queue.TriggerSpan.Start);
        Assert.Equal(source.IndexOf("Request>(", StringComparison.Ordinal), queue.TypeSpan.Start);
        Assert.Equal(source.IndexOf("\"orders\"", StringComparison.Ordinal), queue.QueueNameSpan.Start);
        var ping = Assert.Single(compiled.Actors, actor => actor.Name == "Ping");
        Assert.Equal("Unit", Assert.Single(ping.QueueBindings).MessageType);
        Assert.Empty(Assert.Single(compiled.Actors, actor => actor.Name == "Plain").QueueBindings);
    }

    [Fact]
    public void QueueMessageShapesAreCheckedAfterGenericSubstitution()
    {
        string[] accepted =
        [
            "type Request { value: int }",
            "type Part { number: int }\ntype Request { part: Part }",
            "type Box<T> { value: T }\ntype Request { value: Box<int> }",
            "type Choice { | Empty | Number(int) }\ntype Request { value: Choice }",
            "type Request { | End | Next(Request) }"
        ];
        foreach (var declarations in accepted)
        {
            var source = $"module Demo\n{declarations}\nactor Worker {{ trigger Queue<Request>(\"orders\")\n Handle(message: Request): int {{ 42 }} }}";
            var result = DSharpCompiler.Compile(source, "valid-queue.ds");
            Assert.True(result.Diagnostics.Count == 0 && result.CSharp is not null,
                $"Expected queue shape to compile: {declarations}; diagnostics: {string.Join("; ", result.Diagnostics)}");
        }

        var wideFields = string.Join("\n", Enumerable.Range(0, 300).Select(i => $"field{i}: int"));
        var wideSource = $"module Demo\ntype Request {{\n{wideFields}\n}}\nactor Worker {{ trigger Queue<Request>(\"orders\")\n Handle(message: Request): int {{ 42 }} }}";
        var wide = DSharpCompiler.Compile(wideSource, "wide-queue.ds");
        Assert.True(wide.Diagnostics.Count == 0 && wide.CSharp is not null,
            $"Expected a queue record with 300 scalar fields to compile; diagnostics: {string.Join("; ", wide.Diagnostics)}");

        string[] rejected =
        [
            "type Request { value: int? }",
            "type Request { value: List<int> }",
            "type Request { value: Option<int> }",
            "type Request { value: (int, string) }",
            "type Box<T> { value: T }\ntype Request { value: Box<List<int>> }",
            "type Box<T> { value: T }\ntype Request { value: Box<int?> }",
            "type Box<T> { | Item(T) }\ntype Request { value: Box<Option<int>> }",
            "type Nested { value: List<int> }\ntype Request { child: Nested }",
            "type Request { | End | Next(Request) | Bad(List<int>) }",
            "type Node<T> { | End | Next(Node<T>) | Bad(T) }\ntype Request { value: Node<List<int>> }",
            "type Expand<T> { | End | Next(Expand<List<T>>) }\ntype Request { value: Expand<int> }"
        ];
        foreach (var declarations in rejected)
        {
            var source = $"module Demo\n{declarations}\nactor Worker {{ trigger Queue<Request>(\"orders\")\n Handle(message: Request): int {{ 42 }} }}";
            var result = DSharpCompiler.Compile(source, "invalid-queue-shape.ds");
            Assert.True(result.CSharp is null && result.Diagnostics.Any(d => d.Code == "DS2043" && d.Path == "invalid-queue-shape.ds"),
                $"Expected queue shape diagnostic for: {declarations}; diagnostics: {string.Join("; ", result.Diagnostics)}");
        }

        // These field types are rejected by the existing immutable-data check
        // before queue binding; do not attribute that earlier error to DS2043.
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
            var source = $"module Demo\n{setup}\ntype Request {{ value: {fieldType} }}\nactor Worker {{ trigger Queue<Request>(\"orders\")\n Handle(message: Request): int {{ 42 }} }}";
            var result = DSharpCompiler.Compile(source, "queue-capability.ds");
            var typeStart = source.IndexOf($"value: {fieldType}", StringComparison.Ordinal) + "value: ".Length;
            Assert.True(result.CSharp is null && result.Diagnostics.Any(d => d.Code == "DS2015" &&
                d.Path == "queue-capability.ds" && d.Span.Start == typeStart),
                $"Expected immutable-data field diagnostic for queue payload {fieldType}; diagnostics: {string.Join("; ", result.Diagnostics)}");
        }
    }

    [Fact]
    public void QueueBindingsRejectInvalidRootsMismatchAndDuplicateNames()
    {
        string[] cases =
        [
            "actor Worker { trigger Queue<int>(\"q\") Handle(message: int): int { message } }",
            "actor Worker { trigger Queue<string>(\"q\") Handle(message: string): int { 1 } }",
            "actor Worker { trigger Queue<Request?>(\"q\") Handle(message: Request?): int { 1 } }",
            "actor Worker { trigger Queue<List<Request>>(\"q\") Handle(message: List<Request>): int { 1 } }",
            "actor Worker { trigger Queue<(int,string)>(\"q\") Handle(message: (int,string)): int { 1 } }",
            "actor Worker { trigger Queue<Request>(\"q\") Handle(message: Other): int { 1 } }",
            "actor Worker { trigger Queue<Request>(\"\") Handle(message: Request): int { 1 } }",
            "actor Worker { trigger Queue<Request>(\"orders\") trigger Queue<Request>(\"orders\") Handle(message: Request): int { 1 } }",
            "actor Worker { trigger Queue<Request>(\"orders\") trigger Queue<Request>(\"order\\u0073\") Handle(message: Request): int { 1 } }"
        ];
        foreach (var declaration in cases)
        {
            var source = $"module Demo\ntype Request {{ value: int }}\ntype Other {{ value: int }}\n{declaration}";
            var result = DSharpCompiler.Compile(source, "invalid-queue.ds");
            Assert.True(result.CSharp is null && result.Diagnostics.Any(d => d.Code == "DS2043" && d.Path == "invalid-queue.ds"),
                $"Expected queue diagnostic for: {declaration}; diagnostics: {string.Join("; ", result.Diagnostics)}");
        }

        const string mismatch = "module Demo\ntype Request { value: int }\ntype Other { value: int }\nactor Worker { trigger Queue<Request>(\"q\") Handle(message: Other): Result<int, string> { Success(1) } }";
        var rejected = DSharpCompiler.Compile(mismatch, "mismatch.ds");
        var diagnostic = Assert.Single(rejected.Diagnostics, d => d.Code == "DS2043");
        Assert.Equal("mismatch.ds", diagnostic.Path);
        Assert.Contains("Other", diagnostic.Message);
        Assert.Contains("Result<int, string>", diagnostic.Message);
        var mismatchHandle = mismatch.IndexOf("Handle(message", StringComparison.Ordinal);
        var mismatchColumn = mismatchHandle - mismatch.LastIndexOf('\n', mismatchHandle);
        Assert.Contains($"mismatch.ds(4,{mismatchColumn})", diagnostic.Message);
        Assert.True(diagnostic.Span.Start >= mismatch.IndexOf("trigger Queue", StringComparison.Ordinal));
        Assert.True(diagnostic.Span.Start < mismatch.IndexOf("Handle(message", StringComparison.Ordinal));

        string[] contractErrors =
        [
            "actor Worker { trigger Queue<Requset>(\"q\") Handle(message: Request): int { 1 } }",
            "actor Worker { trigger Queue<int>(\"q\") Handle(message: int): int { message } }",
            "actor Worker { trigger Queue<Request>(\"\") Handle(message: Request): int { 1 } }",
            "actor Worker { trigger Queue<Request>(\"q\") trigger Queue<Request>(\"q\") Handle(message: Request): int { 1 } }"
        ];
        foreach (var declaration in contractErrors)
        {
            var source = $"module Demo\ntype Request {{ value: int }}\n{declaration}";
            var result = DSharpCompiler.Compile(source, "queue-signature.ds");
            var error = Assert.Single(result.Diagnostics, d => d.Code == "DS2043");
            var expectedInput = declaration.Contains("Queue<int>", StringComparison.Ordinal) ? "int" : "Request";
            Assert.Contains($"Handle({expectedInput}): int", error.Message);
            var handleStart = source.IndexOf("Handle(message", StringComparison.Ordinal);
            var handleColumn = handleStart - source.LastIndexOf('\n', handleStart);
            Assert.Contains($"queue-signature.ds(3,{handleColumn})", error.Message);
            Assert.True(error.Span.Start >= source.IndexOf("trigger Queue", StringComparison.Ordinal) &&
                error.Span.Start < source.IndexOf("Handle(message", StringComparison.Ordinal),
                $"Expected trigger source location for: {declaration}");
        }

        string[] malformed =
        [
            "triger Queue<Request>(\"orders\")",
            "trigger Queue<Request>()",
            "trigger Queue<Request>(123)",
            "trigger Queue(\"orders\")"
        ];
        foreach (var member in malformed)
        {
            var source = $"module Demo\ntype Request {{ value: int }}\nactor Worker {{ {member} Handle(message: Request): int {{ 1 }} }}";
            var result = DSharpCompiler.Compile(source, "malformed-queue.ds");
            Assert.True(result.CSharp is null && result.Diagnostics.Count > 0 && result.Diagnostics.Any(d => d.Path == "malformed-queue.ds"),
                $"Expected malformed queue diagnostic for: {member}");
        }

        const string valid = "module Demo\ntype Request { value: int }\nactor Worker { trigger Queue<Request>(\"q\") Handle(message: Request): int { 1 } }";
        var syntax = new Parser(Lexer.Lex(valid, "forged.ds"), "forged.ds").Parse();
        var actor = Assert.Single(syntax.Actors);
        var binding = Assert.Single(actor.QueueBindings);
        string[] forgedLiterals = ["abc", "$\"queue\"", "\"unfinished", "\"first\" \"second\"", "\"line\nnext\""];
        foreach (var forged in forgedLiterals)
        {
            var token = binding.QueueName with { Text = forged };
            var alteredActor = actor with { QueueBindings = [binding with { QueueName = token }] };
            var altered = syntax with { Actors = [alteredActor] };
            var error = Assert.Throws<SourceError>(() => DSharp.Compiler.Binder.Bind(altered, "forged.ds"));
            Assert.True(error.Diagnostic.Code == "DS2043" && error.Diagnostic.Path == "forged.ds" &&
                error.Diagnostic.Span == token.Span, $"Expected a queue literal diagnostic for forged token: {forged}");
        }
    }

    [Fact]
    public void QueueBindingsUseResolvedMultiFileIdentityAndPreserveIsolation()
    {
        const string worker = "module Workers\nimport Contracts\nactor Worker { trigger Queue<Request>(\"orders\") Handle(message: Request): int { message.value } }";
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
        var queue = Assert.Single(actor.QueueBindings);
        Assert.Equal("Contracts.Request", queue.MessageType);
        Assert.Equal("worker.ds", queue.SourcePath);
        Assert.Equal(worker.IndexOf("trigger Queue", StringComparison.Ordinal), queue.TriggerSpan.Start);
        Assert.Equal(worker.IndexOf("Request>(", StringComparison.Ordinal), queue.TypeSpan.Start);

        SourceFile[] mismatch =
        [
            new("left.ds", "module Left\ntype Request { value: int }"),
            new("right.ds", "module Right\ntype Request { value: int }"),
            new("worker.ds", "module Workers\nimport Left\nimport Right\nactor Worker { trigger Queue<Left.Request>(\"orders\") Handle(message: Right.Request): int { 1 } }")
        ];
        var invalid = DSharpCompiler.Compile(mismatch);
        Assert.Null(invalid.CSharp);
        Assert.Contains(invalid.Diagnostics, d => d.Code == "DS2043" && d.Path == "worker.ds");

        const string unimportedWorker = "module Workers\nactor Worker { trigger Queue<Contracts.Request>(\"orders\") Handle(message: Unit): int { 1 } }";
        SourceFile[] missingImport =
        [
            new("contracts.ds", "module Contracts\ntype Request { value: int }"),
            new("unimported-worker.ds", unimportedWorker)
        ];
        var missingImportResult = DSharpCompiler.Compile(missingImport);
        Assert.Null(missingImportResult.CSharp);
        var missingImportError = Assert.Single(missingImportResult.Diagnostics, d => d.Code == "DS2032");
        Assert.Equal("unimported-worker.ds", missingImportError.Path);
        Assert.Equal(unimportedWorker.IndexOf("Contracts.Request", StringComparison.Ordinal), missingImportError.Span.Start);
        Assert.Contains("Handle(Unit): int", missingImportError.Message);
        var unimportedHandle = unimportedWorker.IndexOf("Handle(message", StringComparison.Ordinal);
        var unimportedHandleColumn = unimportedHandle - unimportedWorker.LastIndexOf('\n', unimportedHandle);
        Assert.Contains($"unimported-worker.ds(2,{unimportedHandleColumn})", missingImportError.Message);

        foreach (var trigger in new[] { "trigger Queue<__dsharp_module_symbol_0<int>>(\"q\")", "" })
        {
            SourceFile[] hidden =
            [
                new("secret.ds", "module A\ntype Secret<T> { data: T }"),
                new("unimported.ds", $"module B\nactor Worker {{ {trigger} Handle(message: __dsharp_module_symbol_0<int>): int {{ 1 }} }}")
            ];
            var inaccessible = DSharpCompiler.Compile(hidden);
            Assert.True(inaccessible.CSharp is null && inaccessible.Diagnostics.Any(d => d.Path == "unimported.ds"),
                $"Expected source spelling of an internal type symbol to be rejected; trigger: {trigger}");
        }

        SourceFile[] nestedHidden =
        [
            new("secret.ds", "module A\ntype Secret<T> { data: T }"),
            new("unimported.ds", "module B\ntype Outer<T> { value: T }\nactor Worker { trigger Queue<Outer<__dsharp_module_symbol_0<int>>>(\"q\") Handle(message: Outer<__dsharp_module_symbol_0<int>>): int { 1 } }")
        ];
        var nestedInaccessible = DSharpCompiler.Compile(nestedHidden);
        Assert.True(nestedInaccessible.CSharp is null && nestedInaccessible.Diagnostics.Any(d => d.Path == "unimported.ds"),
            "Expected a nested source spelling of an internal type symbol to be rejected.");

        string[] bypasses =
        [
            "fn bad(): Unit { Worker() }",
            "fn bad(): int { Worker().Handle(Request { value = 1 }) }",
            "fn bad(): int { let callback = Worker().Handle\n 42 }",
            "fn bad(worker: Worker): int { 1 }"
        ];
        foreach (var bypass in bypasses)
        {
            var source = $"module Demo\ntype Request {{ value: int }}\nactor Worker {{ trigger Queue<Request>(\"orders\") Handle(message: Request): int {{ message.value }} }}\n{bypass}";
            var rejected = DSharpCompiler.Compile(source, "queue-bypass.ds");
            Assert.True(rejected.CSharp is null && rejected.Diagnostics.Count > 0,
                $"Expected actor isolation diagnostic for: {bypass}");
        }
    }
}
