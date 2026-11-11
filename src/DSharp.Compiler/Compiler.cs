using System.Globalization;
using System.Text;

namespace DSharp.Compiler;

public sealed record SourceSpan(int Start, int Length, int Line, int Column);
public sealed record Diagnostic(string Code, string Path, SourceSpan Span, string Message);
public sealed record Compilation(string? CSharp, IReadOnlyList<Diagnostic> Diagnostics);
public sealed record Token(string Text, SourceSpan Span);
public abstract record ExpressionSyntax(Token Token);
public sealed record BlockSyntax(IReadOnlyList<LetBindingSyntax> Bindings, ExpressionSyntax Result);
public sealed record IfSyntax(Token If, ExpressionSyntax Condition, BlockSyntax Then, BlockSyntax Else) : ExpressionSyntax(If);
public sealed record GroupSyntax(Token Open, ExpressionSyntax Inner) : ExpressionSyntax(Open);
public sealed record ValueSyntax(Token Value) : ExpressionSyntax(Value);
public sealed record CallSyntax(Token Name, Token OpenParen, IReadOnlyList<ExpressionSyntax> Arguments) : ExpressionSyntax(Name);
public sealed record ParameterSyntax(Token Name, Token Type);
public sealed record UnarySyntax(Token Operator, ExpressionSyntax Operand) : ExpressionSyntax(Operator);
public sealed record BinarySyntax(ExpressionSyntax Left, Token Operator, ExpressionSyntax Right) : ExpressionSyntax(Operator);
public abstract record BoundExpression;
public sealed record BoundBlock(IReadOnlyList<BoundLocal> Locals, BoundExpression Result);
public sealed record BoundIf(BoundExpression Condition, BoundBlock Then, BoundBlock Else, string Type) : BoundExpression;
public sealed record BoundInt(int Value) : BoundExpression;
public sealed record BoundBool(bool Value) : BoundExpression;
public sealed record BoundText(string Value, bool IsChar) : BoundExpression;
public sealed record BoundReference(string Name) : BoundExpression;
public sealed record BoundCall(string Name, IReadOnlyList<BoundExpression> Arguments) : BoundExpression;
public sealed record BoundUnary(string Operator, BoundExpression Operand) : BoundExpression;
public sealed record BoundBinary(BoundExpression Left, string Operator, BoundExpression Right) : BoundExpression
{
    public string OperandType { get; init; } = "int";
}
public sealed record LetBindingSyntax(Token Name, Token? Annotation, Token Initializer)
{
    public ExpressionSyntax? Expression { get; init; }
}
public sealed record FunctionSyntax(Token Name, Token ReturnType, Token Literal)
{
    // Literal retains its original name for bootstrap API compatibility; it is now
    // the first token of the final expression. Expression carries the full tree.
    public ExpressionSyntax? Expression { get; init; }
    public IReadOnlyList<ParameterSyntax> Parameters { get; init; } = Array.Empty<ParameterSyntax>();
    public IReadOnlyList<LetBindingSyntax> Bindings { get; init; } = Array.Empty<LetBindingSyntax>();
}
public sealed record ModuleSyntax(Token Name, IReadOnlyList<FunctionSyntax> Functions);
public sealed record BoundLocal(string Name, int Value, string? InitializerLocal)
{
    public string Type { get; init; } = "int";
    public BoundExpression? Expression { get; init; }
}
public sealed record BoundFunction(string Name, int Value)
{
    public string ReturnType { get; init; } = "int";
    public IReadOnlyList<string> ParameterTypes { get; init; } = Array.Empty<string>();
    public BoundExpression? Expression { get; init; }
    public IReadOnlyList<string> Parameters { get; init; } = Array.Empty<string>();
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
            if (c is '\"' or '\'')
            {
                var quote = c; Advance();
                while (i < source.Length && source[i] != quote)
                {
                    if (source[i] is '\n' or '\r')
                        throw new SourceError(new("DS1001", path, new(i, 1, line, column), "Text literals cannot span lines."));
                    if (source[i] == '\\')
                    {
                        Advance();
                        if (i >= source.Length || source[i] is '\n' or '\r')
                            throw new SourceError(new("DS1001", path, new(i, 0, line, column), "Unterminated text escape."));
                    }
                    Advance();
                }
                if (i == source.Length)
                    throw new SourceError(new("DS1001", path, new(start, i - start, startLine, startColumn), "Unterminated text literal."));
                Advance();
            }
            else if (char.IsAsciiLetter(c) || c == '_')
                do { Advance(); } while (i < source.Length && (char.IsAsciiLetterOrDigit(source[i]) || source[i] == '_'));
            else if (char.IsAsciiDigit(c))
                do { Advance(); } while (i < source.Length && char.IsAsciiDigit(source[i]));
            else if (i + 1 < source.Length && (source.Substring(i, 2) is "==" or "!=" or "<=" or ">=" or "&&" or "||")) { Advance(); Advance(); }
            else if ("(),:{}=+-*/%!<>".Contains(c)) Advance();
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
        if (Current.Text != text) throw Error($"Expected '{text}', found '{Current.Text}'. Only supported expressions and local let bindings are accepted.");
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
    private static readonly HashSet<string> Reserved = new("module import type interface service agent worker fn async await let return if else match when for in with extend using app true false null int bool string char public private protected internal".Split(' '));
    private ExpressionSyntax Expression(int? line = null, int minimumPrecedence = 0)
    {
        ExpressionSyntax left;
        if (line is { } expectedLine && Current.Span.Line != expectedLine)
            throw new SourceError(new("DS1002", path, Current.Span, "Expected an operand on the binding line."));
        if (Current.Text == "if")
        {
            var keyword = Take();
            var condition = Expression();
            var thenBlock = Block();
            Expect("else");
            var elseBlock = Block();
            left = new IfSyntax(keyword, condition, thenBlock, elseBlock);
        }
        else if (Current.Text is "-" or "!")
        {
            var op = Take();
            left = new UnarySyntax(op, Expression(line, 7));
        }
        else if (Current.Text == "(")
        {
            var open = Take(); var inner = Expression(line); Expect(")"); left = new GroupSyntax(open, inner);
        }
        else
        {
            var text = Current.Text;
            if (text.Length == 0 || !(text[0] is '"' or '\'' || text is "true" or "false" || text.All(char.IsAsciiDigit) ||
                ((char.IsAsciiLetter(text[0]) || text[0] == '_') && !Reserved.Contains(text))))
                throw new SourceError(new("DS2002", path, Current.Span, "Expected a supported literal, local or parenthesized expression."));
            var name = Take();
            if (name.Text[0] is not ('"' or '\'') && name.Text is not ("true" or "false") && Current.Text == "(" && (line is null || Current.Span.Line == line))
            {
                var open = Take();
                var args = new List<ExpressionSyntax>();
                var argumentLine = line;
                if (Current.Text != ")")
                {
                    do
                    {
                        var argument = Expression(argumentLine);
                        args.Add(argument);
                        if (Current.Text != ",") break;
                        if (argumentLine is not null && HasIf(argument) && Current.Span.Line == tokens[position - 1].Span.Line)
                            argumentLine = Current.Span.Line;
                        Take();
                    } while (true);
                }
                Expect(")");
                left = new CallSyntax(name, open, args);
            }
            else left = new ValueSyntax(name);
        }
        while (line is null || Current.Span.Line == line ||
            (HasIf(left) && Current.Span.Line == tokens[position - 1].Span.Line))
        {
            var precedence = Current.Text switch { "||" => 1, "&&" => 2, "==" or "!=" => 3,
                "<" or "<=" or ">" or ">=" => 4, "+" or "-" => 5, "*" or "/" or "%" => 6, _ => 0 };
            if (precedence == 0 || precedence < minimumPrecedence) break;
            var op = Take();
            var rightLine = line is not null && HasIf(left) ? op.Span.Line : line;
            var right = Expression(rightLine, precedence + 1);
            static bool Comparison(ExpressionSyntax expr) => expr is BinarySyntax binary &&
                binary.Operator.Text is "==" or "!=" or "<" or "<=" or ">" or ">=";
            if (precedence is 3 or 4 && (Comparison(left) || Comparison(right)))
                throw new SourceError(new("DS2013", path, op.Span, "Chained comparisons require explicit grouping."));
            left = new BinarySyntax(left, op, right);
        }
        return left;
    }
    private static bool HasIf(ExpressionSyntax expression) => expression switch
    {
        IfSyntax => true,
        GroupSyntax group => HasIf(group.Inner),
        UnarySyntax unary => HasIf(unary.Operand),
        BinarySyntax binary => HasIf(binary.Left) || HasIf(binary.Right),
        CallSyntax call => call.Arguments.Any(HasIf),
        _ => false
    };
    private List<LetBindingSyntax> Bindings()
    {
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
            if (initializerEnd.Span.Line != keyword.Span.Line && !HasIf(initializer))
                throw new SourceError(new("DS1002", path, initializerEnd.Span, "Write each let binding on one line."));
            if (Current.Span.Line == initializerEnd.Span.Line)
                throw Error("A let binding must be followed by a newline and a final expression or another binding.");
            bindings.Add(new(local, annotation, initializerToken) { Expression = initializer });
        }
        return bindings;
    }
    private BlockSyntax Block()
    {
        Expect("{");
        var bindings = Bindings();
        var result = Expression();
        if (Current.Text == "=")
            throw new SourceError(new("DS2005", path, result.Token.Span, "Local bindings cannot be reassigned."));
        Expect("}");
        return new(bindings, result);
    }
    public ModuleSyntax Parse()
    {
        Expect("module"); var name = Identifier();
        var functions = new List<FunctionSyntax>();
        while (Current.Text != "")
        {
            Expect("fn"); var function = Identifier(); Expect("(");
            var parameters = new List<ParameterSyntax>();
            if (Current.Text != ")")
            {
                do
                {
                    var parameter = Identifier(); Expect(":");
                    if (Current.Text == "") throw Error("Expected a parameter type.");
                    parameters.Add(new(parameter, Take()));
                    if (Current.Text != ",") break;
                    Take();
                } while (true);
            }
            Expect(")"); Expect(":");
            var type = Take();
            if (type.Text == "") throw new SourceError(new("DS1002", path, type.Span, "Expected a return type."));
            Expect("{");
            var bindings = Bindings();
            var resultToken = Current;
            var result = Expression();
            if (Current.Text == "=")
                throw new SourceError(new("DS2005", path, resultToken.Span, "Local bindings cannot be reassigned."));
            Expect("}");
            functions.Add(new(function, type, resultToken) { Expression = result, Parameters = parameters.Count == 0 ? Array.Empty<ParameterSyntax>() : parameters, Bindings = bindings.Count == 0 ? Array.Empty<LetBindingSyntax>() : bindings });
        }
        if (functions.Count == 0) throw Error("Expected at least one function.");
        return new(name, functions);
    }
}

public static class Binder
{
    private static string DecodeText(Token token, string path)
    {
        var raw = token.Text;
        var value = new StringBuilder();
        for (var i = 1; i < raw.Length - 1; i++)
        {
            var c = raw[i];
            if (c != '\\') { value.Append(c); continue; }
            var escape = ++i;
            if (i >= raw.Length - 1) throw new SourceError(new("DS2014", path, token.Span, "Incomplete text escape."));
            switch (raw[i])
            {
                case 'n': value.Append('\n'); break;
                case 'r': value.Append('\r'); break;
                case 't': value.Append('\t'); break;
                case '\\': value.Append('\\'); break;
                case '\"': value.Append('\"'); break;
                case '\'': value.Append('\''); break;
                case 'u':
                    if (i + 4 >= raw.Length - 1 || !ushort.TryParse(raw.AsSpan(i + 1, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var code))
                        throw new SourceError(new("DS2014", path, token.Span, "Unicode escapes require exactly four hexadecimal digits."));
                    value.Append((char)code); i += 4; break;
                default: throw new SourceError(new("DS2014", path, token.Span, $"Unsupported text escape at offset {escape}."));
            }
        }
        return value.ToString();
    }
    public static BoundModule Bind(ModuleSyntax syntax, string path = "<source>")
    {
        void Fail(string code, Token token, string message) => throw new SourceError(new(code, path, token.Span, message));
        var signatures = new Dictionary<string, FunctionSyntax>(StringComparer.Ordinal);
        foreach (var function in syntax.Functions)
        {
            if (!signatures.TryAdd(function.Name.Text, function)) Fail("DS2001", function.Name, "Duplicate function declaration.");
            if (function.ReturnType.Text is not ("int" or "bool" or "string" or "char")) Fail("DS2003", function.ReturnType, "Only int, bool, string and char return types are supported.");
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var parameter in function.Parameters)
            {
                if (!names.Add(parameter.Name.Text)) Fail("DS2010", parameter.Name, "Duplicate parameter.");
                if (parameter.Type.Text is not ("int" or "bool" or "string" or "char")) Fail("DS2011", parameter.Type, "Only int, bool, string and char parameters are supported.");
            }
        }
        var functions = new List<BoundFunction>();
        foreach (var function in syntax.Functions)
        {
            var locals = new Dictionary<string, (int Value, string Type)>(StringComparer.Ordinal);
            foreach (var parameter in function.Parameters) locals.Add(parameter.Name.Text, (0, parameter.Type.Text));
            var boundLocals = new List<BoundLocal>();
            void Require(ExpressionSyntax syntaxNode, string actual, string expected)
            {
                if (actual != expected) Fail("DS2002", syntaxNode.Token, $"Expected {expected}, found {actual}.");
            }
            (BoundExpression Expression, string Type) BindExpression(ExpressionSyntax expression)
            {
                switch (expression)
                {
                    case IfSyntax conditional:
                        var condition = BindExpression(conditional.Condition);
                        Require(conditional.Condition, condition.Type, "bool");
                        var thenBlock = BindBlock(conditional.Then);
                        var elseBlock = BindBlock(conditional.Else);
                        Require(conditional.Else.Result, elseBlock.Type, thenBlock.Type);
                        return (new BoundIf(condition.Expression, thenBlock.Block, elseBlock.Block, thenBlock.Type), thenBlock.Type);
                    case GroupSyntax group: return BindExpression(group.Inner);
                    case ValueSyntax valueSyntax:
                        var token = valueSyntax.Value;
                        if (token.Text.Length > 0 && (token.Text[0] is '\"' or '\''))
                        {
                            var text = DecodeText(token, path);
                            var isChar = token.Text[0] == '\'';
                            if (isChar && text.Length != 1) Fail("DS2014", token, "A char literal must contain exactly one UTF-16 code unit.");
                            return (new BoundText(text, isChar), isChar ? "char" : "string");
                        }
                        if (token.Text is "true" or "false") return (new BoundBool(token.Text == "true"), "bool");
                        if (token.Text.Length > 0 && token.Text.All(char.IsAsciiDigit))
                        {
                            if (!int.TryParse(token.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var literal))
                                Fail("DS2004", token, "Integer literal is outside the int range.");
                            return (new BoundInt(literal), "int");
                        }
                        if (!locals.TryGetValue(token.Text, out var local))
                            Fail("DS2007", token, "Unknown local; bindings are visible only after their initializer and within their function.");
                        return (new BoundReference(token.Text), local.Type);
                    case CallSyntax call:
                        if (locals.ContainsKey(call.Name.Text)) Fail("DS1002", call.OpenParen, "Local values are not callable.");
                        if (!signatures.TryGetValue(call.Name.Text, out var target)) Fail("DS2009", call.Name, "Unknown function.");
                        if (target!.Parameters.Count != call.Arguments.Count) Fail("DS2012", call.Name, "Incorrect number of arguments.");
                        var args = new List<BoundExpression>();
                        for (var i = 0; i < call.Arguments.Count; i++)
                        {
                            var arg = BindExpression(call.Arguments[i]);
                            Require(call.Arguments[i], arg.Type, target.Parameters[i].Type.Text);
                            args.Add(arg.Expression);
                        }
                        return (new BoundCall(call.Name.Text, args), target.ReturnType.Text);
                    case UnarySyntax unary:
                        var operand = BindExpression(unary.Operand);
                        var expected = unary.Operator.Text == "!" ? "bool" : "int";
                        Require(unary.Operand, operand.Type, expected);
                        return (new BoundUnary(unary.Operator.Text, operand.Expression), expected);
                    case BinarySyntax binary:
                        var left = BindExpression(binary.Left);
                        var right = BindExpression(binary.Right);
                        var op = binary.Operator.Text;
                        if (op is "&&" or "||")
                        {
                            Require(binary.Left, left.Type, "bool"); Require(binary.Right, right.Type, "bool");
                            return (new BoundBinary(left.Expression, op, right.Expression), "bool");
                        }
                        if (op is "==" or "!=")
                        {
                            Require(binary.Right, right.Type, left.Type);
                            return (new BoundBinary(left.Expression, op, right.Expression) { OperandType = left.Type }, "bool");
                        }
                        Require(binary.Left, left.Type, "int"); Require(binary.Right, right.Type, "int");
                        return (new BoundBinary(left.Expression, op, right.Expression), op is "<" or "<=" or ">" or ">=" ? "bool" : "int");
                    default: throw new InvalidOperationException("Unknown expression syntax.");
                }
            }
            // Preserve bootstrap values for literal/local-only clients. Arithmetic
            // has no compile-time value: evaluating it here would move faults out
            // of execution. The bound expression is authoritative for emission.
            int LegacyValue(BoundExpression expression) => expression switch
            {
                BoundInt literal => literal.Value,
                BoundReference reference => locals[reference.Name].Value,
                _ => 0
            };
            List<BoundLocal> BindLocals(IReadOnlyList<LetBindingSyntax> bindings)
            {
                var resultLocals = new List<BoundLocal>();
                foreach (var binding in bindings)
                {
                    if (locals.ContainsKey(binding.Name.Text))
                        Fail("DS2006", binding.Name, "Duplicate local binding.");
                    if (binding.Annotation is { } annotation && annotation.Text is not ("int" or "bool" or "string" or "char"))
                        Fail("DS2008", annotation, "Only int, bool, string and char local annotations are supported.");
                    var initializer = BindExpression(binding.Expression ?? new ValueSyntax(binding.Initializer));
                    if (binding.Annotation is { } annotationType && initializer.Type != annotationType.Text)
                    {
                        if (annotationType.Text is "string" or "char") Fail("DS2008", annotationType, "Local annotation does not match initializer.");
                        Require(binding.Expression ?? new ValueSyntax(binding.Initializer), initializer.Type, annotationType.Text);
                    }
                    var value = LegacyValue(initializer.Expression);
                    locals.Add(binding.Name.Text, (value, initializer.Type));
                    resultLocals.Add(new(binding.Name.Text, value, (initializer.Expression as BoundReference)?.Name) { Expression = initializer.Expression, Type = initializer.Type });
                }
                return resultLocals;
            }
            (BoundBlock Block, string Type) BindBlock(BlockSyntax block)
            {
                var introduced = new List<string>();
                try
                {
                    // Existing names remain visible; new names are removed at block exit.
                    var scopedLocals = new List<BoundLocal>();
                    foreach (var binding in block.Bindings)
                    {
                        var bound = BindLocals(new[] { binding });
                        scopedLocals.AddRange(bound);
                        introduced.Add(binding.Name.Text);
                    }
                    var blockResult = BindExpression(block.Result);
                    return (new BoundBlock(scopedLocals, blockResult.Expression), blockResult.Type);
                }
                finally { foreach (var name in introduced) locals.Remove(name); }
            }
            boundLocals.AddRange(BindLocals(function.Bindings));
            var result = BindExpression(function.Expression ?? new ValueSyntax(function.Literal));
            if (result.Type != function.ReturnType.Text && (function.ReturnType.Text is "string" or "char"))
                Fail("DS2003", function.ReturnType, "Return type does not match the function body.");
            Require(function.Expression ?? new ValueSyntax(function.Literal), result.Type, function.ReturnType.Text);
            functions.Add(new(function.Name.Text, LegacyValue(result.Expression))
            {
                Locals = boundLocals.Count == 0 ? Array.Empty<BoundLocal>() : boundLocals,
                ResultLocal = (result.Expression as BoundReference)?.Name,
                ReturnType = function.ReturnType.Text,
                ParameterTypes = function.Parameters.Select(p => p.Type.Text).ToArray(),
                Parameters = function.Parameters.Count == 0 ? Array.Empty<string>() : function.Parameters.Select(p => p.Name.Text).ToArray(),
                Expression = boundLocals.Count == 0 && result.Expression is BoundInt ? null : result.Expression
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
            output.Append("    public static ").Append(function.ReturnType).Append(" @").Append(function.Name).Append("(");
            for (var i = 0; i < function.Parameters.Count; i++)
            {
                if (i != 0) output.Append(", ");
                output.Append(function.ParameterTypes.Count == 0 ? "int" : function.ParameterTypes[i]).Append(" __dsharp_param_").Append(i.ToString(CultureInfo.InvariantCulture));
            }
            output.Append(")");
            if (function.Expression is null && function.Locals.Count == 0)
            {
                output.Append(" => ").Append(function.Value.ToString(CultureInfo.InvariantCulture)).Append(";\n");
                continue;
            }
            output.Append("\n    {\n        checked\n        {\n");
            var localNames = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < function.Parameters.Count; i++)
                localNames.Add(function.Parameters[i], "__dsharp_param_" + i.ToString(CultureInfo.InvariantCulture));
            var literalCount = 0;
            var localCount = 0;
            var conditionalCount = 0;
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
                    case BoundIf conditional:
                        var helper = "__dsharp_if_" + (conditionalCount++).ToString(CultureInfo.InvariantCulture);
                        output.Append("            ").Append(conditional.Type).Append(" ").Append(helper).Append("()\n            {\n");
                        var condition = EmitExpression(conditional.Condition);
                        output.Append("            if (").Append(condition).Append(")\n            {\n");
                        EmitBlock(conditional.Then);
                        output.Append("            }\n            else\n            {\n");
                        EmitBlock(conditional.Else);
                        output.Append("            }\n            }\n");
                        return helper + "()";
                    case BoundText text:
                        if (text.IsChar)
                        {
                            // Keep comparisons nonconstant under warnings-as-errors.
                            var charName = "__dsharp_char_" + (literalCount++).ToString(CultureInfo.InvariantCulture);
                            output.Append("            char ").Append(charName).Append(" = (char)0x")
                                .Append(((int)text.Value[0]).ToString("X4", CultureInfo.InvariantCulture)).Append(";\n");
                            return charName;
                        }
                        return "new string(new char[] { " + string.Join(", ", text.Value.Select(c => "(char)0x" + ((int)c).ToString("X4", CultureInfo.InvariantCulture))) + " })";
                    case BoundBool boolean:
                        // Avoid C# constant-condition/unreachable warnings while
                        // retaining pure literal evaluation in short-circuit paths.
                        var boolName = "__dsharp_bool_" + (literalCount++).ToString(CultureInfo.InvariantCulture);
                        output.Append("            bool ").Append(boolName).Append(" = ")
                            .Append(boolean.Value ? "true" : "false").Append(";\n");
                        return boolName;
                    case BoundReference reference: return localNames[reference.Name];
                    case BoundCall call:
                        var args = call.Arguments.Select(EmitExpression).ToArray();
                        return container + ".@" + call.Name + "(" + string.Join(", ", args) + ")";
                    case BoundUnary unary: return "(" + unary.Operator + EmitExpression(unary.Operand) + ")";
                    case BoundBinary binary:
                        var left = EmitExpression(binary.Left);
                        var right = EmitExpression(binary.Right);
                        if (binary.OperandType == "string" && (binary.Operator is "==" or "!="))
                        {
                            var equals = "global::System.String.Equals(" + left + ", " + right + ", global::System.StringComparison.Ordinal)";
                            return binary.Operator == "!=" ? "(!" + equals + ")" : equals;
                        }
                        return "(" + left + " " + binary.Operator + " " + right + ")";
                    default: throw new InvalidOperationException("Unknown bound expression.");
                }
            }
            void EmitLocals(IReadOnlyList<BoundLocal> blockLocals)
            {
                foreach (var local in blockLocals)
                {
                    var emittedName = "__dsharp_local_" + (localCount++).ToString(CultureInfo.InvariantCulture);
                    var expression = local.Expression ?? (local.InitializerLocal is { } reference
                        ? (BoundExpression)new BoundReference(reference) : new BoundInt(local.Value));
                    var initializer = EmitExpression(expression);
                    output.Append("            ").Append(local.Type).Append(" ").Append(emittedName).Append(" = ").Append(initializer).Append(";\n");
                    localNames.Add(local.Name, emittedName);
                }
            }
            void EmitBlock(BoundBlock block)
            {
                EmitLocals(block.Locals);
                var expression = EmitExpression(block.Result);
                output.Append("            return ").Append(expression).Append(";\n");
                foreach (var local in block.Locals) localNames.Remove(local.Name);
            }
            EmitLocals(function.Locals);
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
