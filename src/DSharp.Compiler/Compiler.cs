using System.Globalization;
using System.Text;

namespace DSharp.Compiler;

public sealed record SourceSpan(int Start, int Length, int Line, int Column);
public sealed record Diagnostic(string Code, string Path, SourceSpan Span, string Message);
public sealed record Compilation(string? CSharp, IReadOnlyList<Diagnostic> Diagnostics);
public sealed record Token(string Text, SourceSpan Span);
public sealed record FunctionSyntax(Token Name, Token ReturnType, Token Literal);
public sealed record ModuleSyntax(Token Name, IReadOnlyList<FunctionSyntax> Functions);
public sealed record BoundFunction(string Name, int Value);
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
            else if ("():{}".Contains(c)) Advance();
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
        if (Current.Text != text) throw Error($"Expected '{text}', found '{Current.Text}'. Only literal functions are supported.");
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
            if (!Current.Text.All(char.IsAsciiDigit) || Current.Text.Length == 0)
                throw new SourceError(new("DS2002", path, Current.Span, "Expected an integer literal final expression; other expressions are unsupported."));
            var literal = Take(); Expect("}");
            functions.Add(new(function, type, literal));
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
            if (!int.TryParse(function.Literal.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                Fail("DS2004", function.Literal, "Integer literal is outside the int range.");
            functions.Add(new(function.Name.Text, value));
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
            output.Append("    public static int @").Append(function.Name).Append("() => ")
                .Append(function.Value.ToString(CultureInfo.InvariantCulture)).Append(";\n");
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
