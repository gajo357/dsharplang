using System.Globalization;
using System.Text;

namespace DSharp.Compiler;

public sealed record SourceSpan(int Start, int Length, int Line, int Column);
public sealed record Diagnostic(string Code, string Path, SourceSpan Span, string Message);
public sealed record Compilation(string? CSharp, IReadOnlyList<Diagnostic> Diagnostics);
public sealed record Token(string Text, SourceSpan Span);
public abstract record ExpressionSyntax(Token Token);
public sealed record ValueSyntax(Token Value) : ExpressionSyntax(Value);
public sealed record UnarySyntax(Token Operator, ExpressionSyntax Operand) : ExpressionSyntax(Operator);
public sealed record BinarySyntax(ExpressionSyntax Left, Token Operator, ExpressionSyntax Right) : ExpressionSyntax(Operator);
public abstract record BoundExpression;
public sealed record BoundInt(int Value) : BoundExpression;
public sealed record BoundReference(string Name) : BoundExpression;
public sealed record BoundUnary(string Operator, BoundExpression Operand) : BoundExpression;
public sealed record BoundBinary(BoundExpression Left, string Operator, BoundExpression Right) : BoundExpression;
public sealed record LetBindingSyntax(Token Name, Token? Annotation, Token Initializer)
{
    public ExpressionSyntax? Expression { get; init; }
}
public sealed record FunctionSyntax(Token Name, Token ReturnType, Token Literal)
{
    // Literal retains its original name for bootstrap API compatibility; it is now
    // the first token of the final expression. Expression carries the full tree.
    public ExpressionSyntax? Expression { get; init; }
    public IReadOnlyList<LetBindingSyntax> Bindings { get; init; } = Array.Empty<LetBindingSyntax>();
}
public sealed record ModuleSyntax(Token Name, IReadOnlyList<FunctionSyntax> Functions);
public sealed record BoundLocal(string Name, int Value, string? InitializerLocal)
{
    public BoundExpression? Expression { get; init; }
}
public sealed record BoundFunction(string Name, int Value)
{
    public BoundExpression? Expression { get; init; }
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
            else if ("():{}=+-*/%".Contains(c)) Advance();
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
        if (Current.Text != text) throw Error($"Expected '{text}', found '{Current.Text}'. Only supported int expressions and local let bindings are accepted.");
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
    private ExpressionSyntax Expression(int? line = null, int minimumPrecedence = 0)
    {
        ExpressionSyntax left;
        if (line is { } expectedLine && Current.Span.Line != expectedLine)
            throw new SourceError(new("DS1002", path, Current.Span, "Expected an operand on the binding line."));
        if (Current.Text == "-")
        {
            var op = Take();
            left = new UnarySyntax(op, Expression(line, 3));
        }
        else if (Current.Text == "(")
        {
            Take(); left = Expression(line); Expect(")");
        }
        else
        {
            var text = Current.Text;
            if (text.Length == 0 || !(text.All(char.IsAsciiDigit) ||
                ((char.IsAsciiLetter(text[0]) || text[0] == '_') && !Reserved.Contains(text))))
                throw new SourceError(new("DS2002", path, Current.Span, "Expected an int literal, local or parenthesized expression."));
            left = new ValueSyntax(Take());
        }
        while (line is null || Current.Span.Line == line)
        {
            var precedence = Current.Text switch { "+" or "-" => 1, "*" or "/" or "%" => 2, _ => 0 };
            if (precedence == 0 || precedence < minimumPrecedence) break;
            var op = Take();
            left = new BinarySyntax(left, op, Expression(line, precedence + 1));
        }
        return left;
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
                var initializerToken = Current;
                var initializer = Expression(keyword.Span.Line);
                var initializerEnd = tokens[position - 1];
                if (initializerEnd.Span.Line != keyword.Span.Line)
                    throw new SourceError(new("DS1002", path, initializerEnd.Span, "Write each let binding on one line."));
                if (Current.Span.Line == initializerEnd.Span.Line)
                    throw Error("A let binding must be followed by a newline and a final expression or another binding.");
                bindings.Add(new(local, annotation, initializerToken) { Expression = initializer });
            }
            var resultToken = Current;
            var result = Expression();
            if (Current.Text == "=")
                throw new SourceError(new("DS2005", path, resultToken.Span, "Local bindings cannot be reassigned."));
            Expect("}");
            functions.Add(new(function, type, resultToken) { Expression = result, Bindings = bindings.Count == 0 ? Array.Empty<LetBindingSyntax>() : bindings });
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
            BoundExpression BindExpression(ExpressionSyntax expression)
            {
                switch (expression)
                {
                    case ValueSyntax valueSyntax:
                        var token = valueSyntax.Value;
                        if (token.Text.Length > 0 && token.Text.All(char.IsAsciiDigit))
                        {
                            if (!int.TryParse(token.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var literal))
                                Fail("DS2004", token, "Integer literal is outside the int range.");
                            return new BoundInt(literal);
                        }
                        if (!locals.ContainsKey(token.Text))
                            Fail("DS2007", token, "Unknown local; bindings are visible only after their initializer and within their function.");
                        return new BoundReference(token.Text);
                    case UnarySyntax unary:
                        return new BoundUnary(unary.Operator.Text, BindExpression(unary.Operand));
                    case BinarySyntax binary:
                        var left = BindExpression(binary.Left);
                        var right = BindExpression(binary.Right);
                        return new BoundBinary(left, binary.Operator.Text, right);
                    default: throw new InvalidOperationException("Unknown expression syntax.");
                }
            }
            // Preserve bootstrap values for literal/local-only clients. Arithmetic
            // has no compile-time value: evaluating it here would move faults out
            // of execution. The bound expression is authoritative for emission.
            int LegacyValue(BoundExpression expression) => expression switch
            {
                BoundInt literal => literal.Value,
                BoundReference reference => locals[reference.Name],
                _ => 0
            };
            foreach (var binding in function.Bindings)
            {
                if (locals.ContainsKey(binding.Name.Text))
                    Fail("DS2006", binding.Name, "Duplicate local binding.");
                if (binding.Annotation is { } annotation && annotation.Text != "int")
                    Fail("DS2008", annotation, "Only int local annotations are supported.");
                var initializer = BindExpression(binding.Expression ?? new ValueSyntax(binding.Initializer));
                var value = LegacyValue(initializer);
                locals.Add(binding.Name.Text, value);
                boundLocals.Add(new(binding.Name.Text, value, (initializer as BoundReference)?.Name) { Expression = initializer });
            }
            var result = BindExpression(function.Expression ?? new ValueSyntax(function.Literal));
            functions.Add(new(function.Name.Text, LegacyValue(result))
            {
                Locals = boundLocals.Count == 0 ? Array.Empty<BoundLocal>() : boundLocals,
                ResultLocal = (result as BoundReference)?.Name,
                Expression = boundLocals.Count == 0 && result is BoundInt ? null : result
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
            if (function.Expression is null && function.Locals.Count == 0)
            {
                output.Append(" => ").Append(function.Value.ToString(CultureInfo.InvariantCulture)).Append(";\n");
                continue;
            }
            output.Append("\n    {\n        checked\n        {\n");
            var localNames = new Dictionary<string, string>(StringComparer.Ordinal);
            var literalCount = 0;
            string EmitExpression(BoundExpression expression)
            {
                switch (expression)
                {
                    case BoundInt literal:
                        // C# constant overflow/division errors must instead be D#
                        // execution faults. Lift literals to ordinary int locals.
                        var name = "__dsharp_int_" + (literalCount++).ToString(CultureInfo.InvariantCulture);
                        output.Append("            int ").Append(name).Append(" = ")
                            .Append(literal.Value.ToString(CultureInfo.InvariantCulture)).Append(";\n");
                        return name;
                    case BoundReference reference: return localNames[reference.Name];
                    case BoundUnary unary: return "(-" + EmitExpression(unary.Operand) + ")";
                    case BoundBinary binary:
                        var left = EmitExpression(binary.Left);
                        var right = EmitExpression(binary.Right);
                        return "(" + left + " " + binary.Operator + " " + right + ")";
                    default: throw new InvalidOperationException("Unknown bound expression.");
                }
            }
            foreach (var local in function.Locals)
            {
                var emittedName = "__dsharp_local_" + localNames.Count.ToString(CultureInfo.InvariantCulture);
                var expression = local.Expression ?? (local.InitializerLocal is { } reference
                    ? (BoundExpression)new BoundReference(reference) : new BoundInt(local.Value));
                var initializer = EmitExpression(expression);
                output.Append("            int ").Append(emittedName).Append(" = ").Append(initializer).Append(";\n");
                localNames.Add(local.Name, emittedName);
            }
            var result = EmitExpression(function.Expression ?? (function.ResultLocal is { } resultLocal
                ? (BoundExpression)new BoundReference(resultLocal) : new BoundInt(function.Value)));
            output.Append("            return ").Append(result).Append(";\n        }\n    }\n");
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
