using System.Globalization;
using System.Text;

namespace DSharp.Compiler;

public sealed record SourceSpan(int Start, int Length, int Line, int Column);
public sealed record Diagnostic(string Code, string Path, SourceSpan Span, string Message);
public sealed record Compilation(string? CSharp, IReadOnlyList<Diagnostic> Diagnostics);
public sealed record Token(string Text, SourceSpan Span);
public sealed record LetBindingSyntax(Token Name, Token? Annotation, Token Initializer);
public sealed record FunctionSyntax(Token Name, Token ReturnType, Token Literal)
{
    // Literal retains its original name for bootstrap API compatibility; it is now
    // the final expression token, which may also refer to a local.
    public IReadOnlyList<LetBindingSyntax> Bindings { get; init; } = Array.Empty<LetBindingSyntax>();
}
public sealed record ModuleSyntax(Token Name, IReadOnlyList<FunctionSyntax> Functions);
public sealed record BoundLocal(string Name, int Value, string? InitializerLocal);
public sealed record BoundFunction(string Name, int Value)
{
    public IReadOnlyList<BoundLocal> Locals { get; init; } = Array.Empty<BoundLocal>();
    public string? ResultLocal { get; init; }
}
public sealed record BoundModule(string Name, IReadOnlyList<BoundFunction> Functions);

public sealed class SourceError(Diagnostic diagnostic) : Exception(diagnostic.Message)
{
    public Diagnostic Diagnostic { get; } = diagnostic;
}

public static class Lexer
{
    public static IReadOnlyList<Token> Lex(string source, string path = "<source>")
    {
        var tokens = new List<Token>();
        int i = 0, line = 1, column = 1;
        void Advance() { if (source[i++] == '\n') { line++; column = 1; } else column++; }
        while (i < source.Length)
        {
            if (char.IsWhiteSpace(source[i])) { Advance(); continue; }
            if (source[i] == '/' && i + 1 < source.Length && source[i + 1] == '/')
            { while (i < source.Length && source[i] != '\n') Advance(); continue; }
            int start = i, startLine = line, startColumn = column;
            char c = source[i];
            if (char.IsAsciiLetter(c) || c == '_')
                do { Advance(); } while (i < source.Length && (char.IsAsciiLetterOrDigit(source[i]) || source[i] == '_'));
            else if (char.IsAsciiDigit(c))
                do { Advance(); } while (i < source.Length && char.IsAsciiDigit(source[i]));
            else if ("():{}=".Contains(c)) Advance();
            else throw new SourceError(new("DS1001", path, new(i, 1, line, column), $"Unsupported character '{c}'."));
            tokens.Add(new(source[start..i], new(start, i - start, startLine, startColumn)));
        }
        tokens.Add(new("", new(i, 0, line, column)));
        return tokens;
    }
}

public sealed class Parser(IReadOnlyList<Token> tokens, string path = "<source>")
{
    private int position;
    private Token Current => tokens[position];
    private Token Take() => tokens[position++];
    private SourceError Error(string message) => new(new("DS1002", path, Current.Span, message));
    private Token Expect(string text)
    {
        if (Current.Text != text) throw Error($"Expected '{text}', found '{Current.Text}'. Only int literals and local let bindings are supported.");
        return Take();
    }
    private Token Identifier()
    {
        var text = Current.Text;
        if (text.Length == 0 || !(char.IsAsciiLetter(text[0]) || text[0] == '_') || Reserved.Contains(text))
            throw Error("Expected an identifier.");
        return Take();
    }
    // Draft language words cannot accidentally become identifiers in this subset.
    private static readonly HashSet<string> Reserved = new("module import type interface service agent worker fn async await let return if else match when for in with extend using app true false null int public private protected internal".Split(' '));
    private Token Expression()
    {
        var text = Current.Text;
        if (text.Length > 0 && text.All(char.IsAsciiDigit)) return Take();
        if (text.Length > 0 && (char.IsAsciiLetter(text[0]) || text[0] == '_') && !Reserved.Contains(text))
            return Take();
        throw new SourceError(new("DS2002", path, Current.Span, "Expected an int literal or local identifier."));
    }
    public ModuleSyntax Parse()
    {
        Expect("module"); var name = Identifier();
        var functions = new List<FunctionSyntax>();
        while (Current.Text != "")
        {
            Expect("fn"); var function = Identifier(); Expect("("); Expect(")"); Expect(":");
            var type = Take();
            if (type.Text == "") throw new SourceError(new("DS1002", path, type.Span, "Expected a return type."));
            Expect("{");
            var bindings = new List<LetBindingSyntax>();
            while (Current.Text == "let")
            {
                var keyword = Take();
                var local = Identifier();
                Token? annotation = null;
                if (Current.Text == ":")
                {
                    Take();
                    if (Current.Text == "") throw Error("Expected a local type annotation.");
                    annotation = Take();
                }
                Expect("=");
                var initializer = Expression();
                if (initializer.Span.Line != keyword.Span.Line)
                    throw new SourceError(new("DS1002", path, initializer.Span, "Write each let binding on one line."));
                if (Current.Span.Line == initializer.Span.Line)
                    throw Error("A let binding must be followed by a newline and a final expression or another binding.");
                bindings.Add(new(local, annotation, initializer));
            }
            var result = Expression();
            if (Current.Text == "=")
                throw new SourceError(new("DS2005", path, result.Span, "Local bindings cannot be reassigned."));
            Expect("}");
            functions.Add(new(function, type, result) { Bindings = bindings.Count == 0 ? Array.Empty<LetBindingSyntax>() : bindings });
        }
        if (functions.Count == 0) throw Error("Expected at least one function.");
        return new(name, functions);
    }
}

public static class Binder
{
    public static BoundModule Bind(ModuleSyntax syntax, string path = "<source>")
    {
        var names = new HashSet<string>();
        var functions = new List<BoundFunction>();
        foreach (var function in syntax.Functions)
        {
            void Fail(string code, Token token, string message) => throw new SourceError(new(code, path, token.Span, message));
            if (!names.Add(function.Name.Text)) Fail("DS2001", function.Name, "Duplicate function declaration.");
            if (function.ReturnType.Text != "int") Fail("DS2003", function.ReturnType, "Only the int return type is supported.");
            var locals = new Dictionary<string, int>(StringComparer.Ordinal);
            var boundLocals = new List<BoundLocal>();
            (int Value, string? Local) BindExpression(Token token)
            {
                if (token.Text.Length > 0 && token.Text.All(char.IsAsciiDigit))
                {
                    if (!int.TryParse(token.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var literal))
                        Fail("DS2004", token, "Integer literal is outside the int range.");
                    return (literal, null);
                }
                if (!locals.TryGetValue(token.Text, out var value))
                    Fail("DS2007", token, "Unknown local; bindings are visible only after their initializer and within their function.");
                return (value, token.Text);
            }
            foreach (var binding in function.Bindings)
            {
                if (locals.ContainsKey(binding.Name.Text))
                    Fail("DS2006", binding.Name, "Duplicate local binding.");
                if (binding.Annotation is { } annotation && annotation.Text != "int")
                    Fail("DS2008", annotation, "Only int local annotations are supported.");
                var initializer = BindExpression(binding.Initializer);
                locals.Add(binding.Name.Text, initializer.Value);
                boundLocals.Add(new(binding.Name.Text, initializer.Value, initializer.Local));
            }
            var result = BindExpression(function.Literal);
            functions.Add(new(function.Name.Text, result.Value)
            {
                Locals = boundLocals.Count == 0 ? Array.Empty<BoundLocal>() : boundLocals,
                ResultLocal = result.Local
            });
        }
        return new(syntax.Name.Text, functions);
    }
}

public static class CSharpEmitter
{
    public static string Emit(BoundModule module)
    {
        // A generated namespace avoids a C# member/type name collision without restricting D# names.
        var output = new StringBuilder("// Generated by DSharp.\nnamespace DSharp.Generated;\n\n");
        var container = "__DSharp_Functions";
        while (container == module.Name || module.Functions.Any(f => f.Name == container)) container += "_";
        output.Append("public sealed class @").Append(module.Name).Append(" : ").Append(container)
            .Append("\n{\n    private @").Append(module.Name).Append("() {}\n}\n\npublic abstract class ")
            .Append(container).Append("\n{\n");
        foreach (var function in module.Functions)
        {
            output.Append("    public static int @").Append(function.Name).Append("()");
            if (function.Locals.Count == 0)
            {
                output.Append(" => ").Append(function.Value.ToString(CultureInfo.InvariantCulture)).Append(";\n");
                continue;
            }
            output.Append("\n    {\n");
            var localNames = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var local in function.Locals)
            {
                var emittedName = "__dsharp_local_" + localNames.Count.ToString(CultureInfo.InvariantCulture);
                var initializer = local.InitializerLocal is { } reference
                    ? localNames[reference] : local.Value.ToString(CultureInfo.InvariantCulture);
                output.Append("        int ").Append(emittedName).Append(" = ").Append(initializer).Append(";\n");
                localNames.Add(local.Name, emittedName);
            }
            var result = function.ResultLocal is { } resultLocal
                ? localNames[resultLocal] : function.Value.ToString(CultureInfo.InvariantCulture);
            output.Append("        return ").Append(result).Append(";\n    }\n");
        }
        return output.Append("}\n").ToString();
    }
}

public static class DSharpCompiler
{
    public static Compilation Compile(string source, string path = "<source>")
    {
        try { return new(CSharpEmitter.Emit(Binder.Bind(new Parser(Lexer.Lex(source, path), path).Parse(), path)), []); }
        catch (SourceError error) { return new(null, [error.Diagnostic]); }
    }
}
