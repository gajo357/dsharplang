using System.Globalization;
using System.Text;

namespace DSharp.Compiler;

public sealed record SourceSpan(int Start, int Length, int Line, int Column);
public sealed record Diagnostic(string Code, string Path, SourceSpan Span, string Message);
public sealed record Compilation(string? CSharp, IReadOnlyList<Diagnostic> Diagnostics);
public sealed record Token(string Text, SourceSpan Span)
{
    public bool IsDocumentation { get; init; }
    public bool HasInterveningComment { get; init; }
    public bool HasCodeBeforeOnLine { get; init; }
}
public abstract record ExpressionSyntax(Token Token);
public abstract record StatementSyntax;
public sealed record LetStatementSyntax(LetBindingSyntax Binding) : StatementSyntax;
public sealed record ReturnStatementSyntax(Token Keyword, ExpressionSyntax Value) : StatementSyntax;
public sealed record ExpressionStatementSyntax(ExpressionSyntax Expression) : StatementSyntax;
public sealed record UnitSyntax(Token Unit) : ExpressionSyntax(Unit);
public sealed record NullSyntax(Token Null) : ExpressionSyntax(Null);
public sealed record BlockSyntax(IReadOnlyList<LetBindingSyntax> Bindings, ExpressionSyntax Result)
{
    public IReadOnlyList<StatementSyntax> Statements { get; init; } = Array.Empty<StatementSyntax>();
}
public sealed record IfSyntax(Token If, ExpressionSyntax Condition, BlockSyntax Then, BlockSyntax Else) : ExpressionSyntax(If)
{
    public bool HasElse { get; init; } = true;
    public Token? MissingElseLocation { get; init; }
}
public sealed record GroupSyntax(Token Open, ExpressionSyntax Inner) : ExpressionSyntax(Open);
public sealed record ValueSyntax(Token Value) : ExpressionSyntax(Value);
public sealed record LambdaParameterSyntax(Token Name, Token? Type);
public sealed record LambdaSyntax(Token Start, IReadOnlyList<LambdaParameterSyntax> Parameters, BlockSyntax Body) : ExpressionSyntax(Start);
public sealed record InvokeSyntax(ExpressionSyntax Receiver, Token OpenParen, IReadOnlyList<ExpressionSyntax> Arguments) : ExpressionSyntax(OpenParen);
public sealed record CallSyntax(Token Name, Token OpenParen, IReadOnlyList<ExpressionSyntax> Arguments) : ExpressionSyntax(Name)
{
    public IReadOnlyList<Token> TypeArguments { get; init; } = Array.Empty<Token>();
}
public sealed record MemberCallSyntax(ExpressionSyntax Receiver, Token Member, Token OpenParen, IReadOnlyList<ExpressionSyntax> Arguments) : ExpressionSyntax(Member)
{
    public IReadOnlyList<Token> TypeArguments { get; init; } = Array.Empty<Token>();
}
public sealed record RecordFieldSyntax(Token Name, Token Type);
public sealed record RecordDeclarationSyntax(Token Name, IReadOnlyList<RecordFieldSyntax> Fields)
{
    public IReadOnlyList<Token> DocumentationComments { get; init; } = Array.Empty<Token>();
    public IReadOnlyList<Token> TypeParameters { get; init; } = Array.Empty<Token>();
}
public sealed record RecordInitializerSyntax(Token Name, ExpressionSyntax Value);
public sealed record RecordConstructionSyntax(Token Name, IReadOnlyList<RecordInitializerSyntax> Fields) : ExpressionSyntax(Name);
public sealed record FieldAccessSyntax(ExpressionSyntax Receiver, Token Field) : ExpressionSyntax(Field);
public sealed record ConditionalFieldAccessSyntax(ExpressionSyntax Receiver, Token Operator, Token Field) : ExpressionSyntax(Operator);
public sealed record RecordUpdateSyntax(ExpressionSyntax Receiver, Token With, IReadOnlyList<RecordInitializerSyntax> Fields) : ExpressionSyntax(With);
public sealed record UnionCaseSyntax(Token Name, IReadOnlyList<Token> PayloadTypes);
public sealed record UnionDeclarationSyntax(Token Name, IReadOnlyList<UnionCaseSyntax> Cases)
{
    public IReadOnlyList<Token> DocumentationComments { get; init; } = Array.Empty<Token>();
    public IReadOnlyList<Token> TypeParameters { get; init; } = Array.Empty<Token>();
}
public sealed record ParameterSyntax(Token Name, Token Type);
public sealed record UnarySyntax(Token Operator, ExpressionSyntax Operand) : ExpressionSyntax(Operator);
public sealed record BinarySyntax(ExpressionSyntax Left, Token Operator, ExpressionSyntax Right) : ExpressionSyntax(Operator);
public sealed record CoalesceSyntax(ExpressionSyntax Left, Token Operator, ExpressionSyntax Right) : ExpressionSyntax(Operator);
public sealed record PropagateSyntax(ExpressionSyntax Operand, Token Operator) : ExpressionSyntax(Operator);
public abstract record PatternSyntax(Token Token);
public sealed record WildcardPatternSyntax(Token Wildcard) : PatternSyntax(Wildcard);
public sealed record LiteralPatternSyntax(Token Literal, Token? Minus = null) : PatternSyntax(Minus ?? Literal);
public sealed record UnitPatternSyntax(Token Open) : PatternSyntax(Open);
public sealed record NullPatternSyntax(Token Null) : PatternSyntax(Null);
// A bare name may be either a binding or a zero-payload case; the binder resolves it.
public sealed record NamePatternSyntax(Token Name) : PatternSyntax(Name);
public sealed record CasePatternSyntax(Token? Qualifier, Token Name, IReadOnlyList<PatternSyntax> Arguments) : PatternSyntax(Qualifier ?? Name);
public sealed record MatchArmSyntax(PatternSyntax Pattern, ExpressionSyntax? Guard, ExpressionSyntax Value);
public sealed record MatchSyntax(Token Match, ExpressionSyntax Scrutinee, IReadOnlyList<MatchArmSyntax> Arms) : ExpressionSyntax(Match);
public abstract record BoundExpression;
public abstract record BoundStatement;
public sealed record BoundLetStatement(BoundLocal Local) : BoundStatement;
public sealed record BoundReturnStatement(BoundExpression Value) : BoundStatement;
public sealed record BoundExpressionStatement(BoundExpression Expression) : BoundStatement;
public sealed record BoundUnit : BoundExpression;
public sealed record BoundNull(string Type) : BoundExpression;
public sealed record BoundPropagation(BoundExpression Operand, string OperandType, string Type, string ReturnType) : BoundExpression;
// An unconstrained T and its nullable projection have different CLR types.
// Keep this conversion explicit rather than relying on C#'s generic T? rules.
public sealed record BoundNullableLift(BoundExpression Expression, string Type) : BoundExpression;
public sealed record BoundNever(BoundExpression Expression, string Type) : BoundExpression;
public sealed record BoundBlock(IReadOnlyList<BoundLocal> Locals, BoundExpression Result)
{
    public IReadOnlyList<BoundStatement> Statements { get; init; } = Array.Empty<BoundStatement>();
    public bool CanComplete { get; init; } = true;
    public bool EvaluateResult { get; init; } = true;
    public string ResultType { get; init; } = "";
}
public sealed record BoundIf(BoundExpression Condition, BoundBlock Then, BoundBlock Else, string Type) : BoundExpression;
public sealed record BoundInt(int Value) : BoundExpression;
public sealed record BoundBool(bool Value) : BoundExpression;
public sealed record BoundText(string Value, bool IsChar) : BoundExpression;
public sealed record BoundReference(string Name) : BoundExpression;
public sealed record BoundLambda(IReadOnlyList<string> Parameters, IReadOnlyList<string> ParameterTypes, string Type, string ReturnType, BoundBlock Body, bool HasReturn) : BoundExpression;
public sealed record BoundInvoke(BoundExpression Receiver, IReadOnlyList<BoundExpression> Arguments, string Type) : BoundExpression;
public sealed record BoundCall(string Name, IReadOnlyList<BoundExpression> Arguments) : BoundExpression
{
    public IReadOnlyList<string> TypeArguments { get; init; } = Array.Empty<string>();
    public Token? Location { get; init; }
}
public sealed record BoundRecordField(string Name, string Type);
public sealed record BoundRecordDeclaration(string Name, IReadOnlyList<BoundRecordField> Fields)
{
    public IReadOnlyList<string> TypeParameters { get; init; } = Array.Empty<string>();
}
public sealed record BoundRecordInitializer(string Name, BoundExpression Value);
public sealed record BoundRecordConstruction(string Type, IReadOnlyList<BoundRecordInitializer> Fields) : BoundExpression;
public sealed record BoundFieldAccess(BoundExpression Receiver, string ReceiverType, string Field) : BoundExpression;
public sealed record BoundConditionalFieldAccess(BoundExpression Receiver, string ReceiverType, string Field, string Type) : BoundExpression
{
    public string FieldType { get; init; } = "";
}
public sealed record BoundCoalesce(BoundExpression Left, BoundExpression Right, string ContainedType, string Type) : BoundExpression;
public sealed record BoundRecordUpdate(BoundExpression Receiver, string Type, IReadOnlyList<BoundRecordInitializer> Fields) : BoundExpression;
public sealed record BoundUnionCase(string Name, IReadOnlyList<string> PayloadTypes);
public sealed record BoundUnionDeclaration(string Name, IReadOnlyList<BoundUnionCase> Cases)
{
    public IReadOnlyList<string> TypeParameters { get; init; } = Array.Empty<string>();
}
public sealed record BoundUnionConstruction(string Type, string Case, IReadOnlyList<BoundExpression> Arguments) : BoundExpression;
public abstract record BoundPattern;
public sealed record BoundWildcardPattern : BoundPattern;
public sealed record BoundNullPattern : BoundPattern;
public sealed record BoundPresentPattern(BoundPattern Inner, string Type) : BoundPattern;
public sealed record BoundBindingPattern(string Name, string Type) : BoundPattern;
public sealed record BoundLiteralPattern(BoundExpression Value, string Type) : BoundPattern;
public sealed record BoundCasePattern(string Union, string Case, IReadOnlyList<BoundPattern> Arguments) : BoundPattern;
public sealed record BoundMatchArm(BoundPattern Pattern, BoundExpression? Guard, BoundExpression Value, bool GuardNever);
public sealed record BoundMatch(BoundExpression Scrutinee, string ScrutineeType,
    IReadOnlyList<BoundMatchArm> Arms, string Type) : BoundExpression
{
    public bool HasCompletingArm { get; init; } = true;
}
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
    public BlockSyntax? Body { get; init; }
    public IReadOnlyList<Token> DocumentationComments { get; init; } = Array.Empty<Token>();
    public IReadOnlyList<Token> TypeParameters { get; init; } = Array.Empty<Token>();
    public IReadOnlyList<ParameterSyntax> Parameters { get; init; } = Array.Empty<ParameterSyntax>();
    public IReadOnlyList<LetBindingSyntax> Bindings { get; init; } = Array.Empty<LetBindingSyntax>();
}
public sealed record ModuleSyntax(Token Name, IReadOnlyList<FunctionSyntax> Functions)
{
    public IReadOnlyList<Token> DocumentationComments { get; init; } = Array.Empty<Token>();
    public IReadOnlyList<RecordDeclarationSyntax> Records { get; init; } = Array.Empty<RecordDeclarationSyntax>();
    public IReadOnlyList<UnionDeclarationSyntax> Unions { get; init; } = Array.Empty<UnionDeclarationSyntax>();
}
public sealed record BoundLocal(string Name, int Value, string? InitializerLocal)
{
    public string Type { get; init; } = "int";
    public BoundExpression? Expression { get; init; }
}
public sealed record BoundFunction(string Name, int Value)
{
    public IReadOnlyList<string> TypeParameters { get; init; } = Array.Empty<string>();
    public string ReturnType { get; init; } = "int";
    public BoundBlock? Body { get; init; }
    public bool HasReturn { get; init; }
    public IReadOnlyList<string> ParameterTypes { get; init; } = Array.Empty<string>();
    public BoundExpression? Expression { get; init; }
    public IReadOnlyList<string> Parameters { get; init; } = Array.Empty<string>();
    public IReadOnlyList<BoundLocal> Locals { get; init; } = Array.Empty<BoundLocal>();
    public string? ResultLocal { get; init; }
}
public sealed record BoundModule(string Name, IReadOnlyList<BoundFunction> Functions)
{
    public IReadOnlyList<BoundRecordDeclaration> Records { get; init; } = Array.Empty<BoundRecordDeclaration>();
    public IReadOnlyList<BoundUnionDeclaration> Unions { get; init; } = Array.Empty<BoundUnionDeclaration>();
}

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
        var hasInterveningComment = false;
        var pendingDocumentation = false;
        void Advance()
        {
            var c = source[i++];
            if (c == '\n' || (c == '\r' && (i == source.Length || source[i] != '\n'))) { line++; column = 1; }
            else column++;
        }
        while (i < source.Length)
        {
            if (char.IsWhiteSpace(source[i])) { Advance(); continue; }
            if (source[i] == '/' && i + 1 < source.Length && source[i + 1] == '/')
            {
                var isDoc = i + 2 < source.Length && source[i + 2] == '/';
                var commentStart = i; var commentLine = line; var commentColumn = column;
                var codeBefore = tokens.Count > 0 && tokens[^1].Span.Line == line;
                while (i < source.Length && source[i] is not ('\n' or '\r')) Advance();
                if (isDoc)
                {
                    tokens.Add(new(source[commentStart..i], new(commentStart, i - commentStart, commentLine, commentColumn))
                    {
                        IsDocumentation = true, HasInterveningComment = hasInterveningComment,
                        HasCodeBeforeOnLine = codeBefore
                    });
                    hasInterveningComment = false;
                    pendingDocumentation = true;
                }
                else hasInterveningComment |= pendingDocumentation;
                continue;
            }
            if (source[i] == '/' && i + 1 < source.Length && source[i + 1] == '*')
            {
                var commentStart = i; var commentLine = line; var commentColumn = column;
                Advance(); Advance();
                while (i < source.Length && !(source[i] == '*' && i + 1 < source.Length && source[i + 1] == '/'))
                {
                    if (source[i] == '/' && i + 1 < source.Length && source[i + 1] == '*')
                        throw new SourceError(new("DS1003", path, new(i, 2, line, column), "Nested block comments are not supported."));
                    Advance();
                }
                if (i == source.Length)
                    throw new SourceError(new("DS1003", path, new(commentStart, 2, commentLine, commentColumn), "Unterminated block comment."));
                Advance(); Advance(); hasInterveningComment |= pendingDocumentation; continue;
            }
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
            else if (i + 1 < source.Length && (source.Substring(i, 2) is "==" or "!=" or "<=" or ">=" or "&&" or "||" or "=>" or "->" or "??" or "?.")) { Advance(); Advance(); }
            else if ("(),.:{}=+-*/%!<>|?".Contains(c)) Advance();
            else throw new SourceError(new("DS1001", path, new(i, 1, line, column), $"Unsupported character '{c}'."));
            tokens.Add(new(source[start..i], new(start, i - start, startLine, startColumn)) { HasInterveningComment = hasInterveningComment });
            hasInterveningComment = false;
            pendingDocumentation = false;
        }
        tokens.Add(new("", new(i, 0, line, column)) { HasInterveningComment = hasInterveningComment });
        return tokens;
    }
}

public sealed class Parser(IReadOnlyList<Token> tokens, string path = "<source>")
{
    private int position;
    private Token Current => tokens[position];
    private Token Take() => tokens[position++];
    private SourceError Error(string message) => Current.IsDocumentation
        ? new(new("DS1004", path, Current.Span, "Documentation must immediately precede a declaration."))
        : new(new("DS1002", path, Current.Span, message));
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
    private static bool TypeName(string text) => text.Length > 0 &&
        (char.IsAsciiLetter(text[0]) || text[0] == '_') &&
        (!Reserved.Contains(text) || text is "int" or "bool" or "string" or "char");

    // Recognize the complete adopted type grammar before consulting symbols. This
    // deliberately recognizes function/tuple/qualified types that a later stage
    // may reject as unsupported, so generic-call disambiguation remains syntactic.
    private bool ScanType(ref int index, out string text)
    {
        text = "";
        var cursor = index;
        if (cursor >= tokens.Count) return false;
        if (tokens[cursor].Text == "(")
        {
            cursor++;
            var parts = new List<string>();
            if (cursor < tokens.Count && tokens[cursor].Text != ")")
            {
                do
                {
                    if (!ScanType(ref cursor, out var part)) return false;
                    parts.Add(part);
                    if (cursor >= tokens.Count || tokens[cursor].Text != ",") break;
                    cursor++;
                } while (true);
            }
            if (cursor >= tokens.Count || tokens[cursor].Text != ")") return false;
            cursor++;
            text = "(" + string.Join(",", parts) + ")";
            if (cursor < tokens.Count && tokens[cursor].Text == "->")
            {
                cursor++;
                if (!ScanType(ref cursor, out var result)) return false;
                text += "->" + result;
                index = cursor;
                return true;
            }
            if (parts.Count == 0) return false;
        }
        else
        {
            if (!TypeName(tokens[cursor].Text)) return false;
            text = tokens[cursor++].Text;
            while (cursor < tokens.Count && tokens[cursor].Text == ".")
            {
                cursor++;
                if (cursor >= tokens.Count || !TypeName(tokens[cursor].Text)) return false;
                text += "." + tokens[cursor++].Text;
            }
            if (cursor < tokens.Count && tokens[cursor].Text == "<")
            {
                cursor++;
                var parts = new List<string>();
                do
                {
                    if (!ScanType(ref cursor, out var part)) return false;
                    parts.Add(part);
                    if (cursor >= tokens.Count || tokens[cursor].Text != ",") break;
                    cursor++;
                } while (true);
                if (cursor >= tokens.Count || tokens[cursor].Text != ">") return false;
                cursor++;
                text += "<" + string.Join(",", parts) + ">";
            }
        }
        if (cursor < tokens.Count && tokens[cursor].Text == "?") { cursor++; text += "?"; }
        if (cursor < tokens.Count && tokens[cursor].Text is "?" or "??") return false;
        index = cursor;
        return true;
    }
    private Token TypeToken()
    {
        var start = position;
        var end = position;
        if (!ScanType(ref end, out var text) && !SplitGenericAssignmentClose(out end, out text))
            throw Error("Expected a valid type.");
        position = end;
        if (end == start + 1) return tokens[start];
        var first = tokens[start].Span;
        var last = tokens[end - 1].Span;
        return new Token(text, new SourceSpan(first.Start, last.Start + last.Length - first.Start, first.Line, first.Column));
    }
    private bool SplitGenericAssignmentClose(out int end, out string text)
    {
        // In a type annotation `Box<int>=value` has a closing `>` immediately
        // followed by `=`. The lexer otherwise correctly emits `>=` as one
        // comparison token. Retry on a private token copy only in this context.
        end = position;
        text = "";
        var depth = 0;
        for (var index = position; index < tokens.Count; index++)
        {
            var current = tokens[index];
            if (current.Text == "<") depth++;
            else if (current.Text == ">" && depth > 0) depth--;
            else if (current.Text == ">=")
            {
                if (depth != 1) return false;
                var original = tokens;
                var split = tokens.ToList();
                var span = current.Span;
                split[index] = current with { Text = ">", Span = new(span.Start, 1, span.Line, span.Column) };
                split.Insert(index + 1, new Token("=", new(span.Start + 1, 1, span.Line, span.Column + 1)));
                tokens = split;
                var candidate = position;
                if (ScanType(ref candidate, out var parsed) && candidate == index + 1)
                {
                    end = candidate;
                    text = parsed;
                    return true;
                }
                tokens = original;
                return false;
            }
            else if (current.Text is "" or "=" or "=>" or "{" or "}") return false;
        }
        return false;
    }
    private IReadOnlyList<Token> TypeParameters()
    {
        if (Current.Text != "<") return Array.Empty<Token>();
        Take();
        var parameters = new List<Token>();
        do
        {
            parameters.Add(Identifier());
            if (Current.Text != ",") break;
            Take();
        } while (true);
        Expect(">");
        return parameters;
    }
    private bool HasGenericCallShape()
    {
        if (Current.Text != "<") return false;
        var cursor = position + 1;
        do
        {
            if (!ScanType(ref cursor, out _)) return false;
            if (cursor >= tokens.Count || tokens[cursor].Text != ",") break;
            cursor++;
        } while (true);
        return cursor + 1 < tokens.Count && tokens[cursor].Text == ">" && tokens[cursor + 1].Text == "(";
    }
    private IReadOnlyList<Token> CallTypeArguments()
    {
        Expect("<");
        var arguments = new List<Token>();
        do
        {
            arguments.Add(TypeToken());
            if (Current.Text != ",") break;
            Take();
        } while (true);
        Expect(">");
        return arguments;
    }
    // Draft language words cannot accidentally become identifiers in this subset.
    private static readonly HashSet<string> Reserved = new("module import type interface service agent worker fn async await let return if else match when for in with extend using app true false null int bool string char public private protected internal".Split(' '));
    private bool ConditionRecordConstruction()
    {
        // A condition and its body may begin `name {`. Recognize a record only
        // when its field shape and the token after its matching brace require
        // the condition to continue; otherwise leave the brace for the body.
        if (Current.Text != "{") return false;
        var next = tokens[position + 1].Text;
        if (next != "}" && (next.Length == 0 || !(char.IsAsciiLetter(next[0]) || next[0] == '_') ||
            Reserved.Contains(next) || tokens[position + 2].Text != "=")) return false;
        var depth = 0;
        for (var index = position; index < tokens.Count; index++)
        {
            if (tokens[index].Text == "{") depth++;
            else if (tokens[index].Text == "}" && --depth == 0)
            {
                if (index + 1 == tokens.Count) return false;
                return tokens[index + 1].Text is "." or "?." or "?" or "with" or "{" or
                    "??" or "||" or "&&" or "==" or "!=" or "<" or "<=" or ">" or ">=" or "+" or "-" or "*" or "/" or "%";
            }
        }
        return false;
    }
    private bool HasLambdaHead()
    {
        if (Current.Text != "(")
            return position + 1 < tokens.Count && tokens[position + 1].Text == "=>" &&
                Current.Text.Length > 0 && (char.IsAsciiLetter(Current.Text[0]) || Current.Text[0] == '_') && !Reserved.Contains(Current.Text);
        var depth = 0;
        for (var cursor = position; cursor < tokens.Count; cursor++)
        {
            if (tokens[cursor].Text == "(") depth++;
            else if (tokens[cursor].Text == ")" && --depth == 0)
                return cursor + 1 < tokens.Count && tokens[cursor + 1].Text == "=>";
        }
        return false;
    }
    private ExpressionSyntax Expression(int? line = null, int minimumPrecedence = 0, bool stopAtBlock = false, bool stopAtMatchArrow = false)
    {
        ExpressionSyntax left;
        if (Current.IsDocumentation) throw Error("Expected an expression.");
        if (line is { } expectedLine && Current.Span.Line != expectedLine)
            throw new SourceError(new("DS1002", path, Current.Span, "Expected an operand on the binding line."));
        if (!stopAtMatchArrow && HasLambdaHead())
        {
            var start = Current;
            var lambdaParameters = new List<LambdaParameterSyntax>();
            if (Current.Text == "(")
            {
                Take();
                if (Current.Text != ")")
                {
                    do
                    {
                        var name = Identifier();
                        Token? annotation = null;
                        if (Current.Text == ":") { Take(); annotation = TypeToken(); }
                        lambdaParameters.Add(new(name, annotation));
                        if (Current.Text != ",") break;
                        Take();
                    } while (true);
                }
                Expect(")");
            }
            else lambdaParameters.Add(new(Identifier(), null));
            Expect("=>");
            var body = Current.Text == "{" ? Block() : new BlockSyntax(Array.Empty<LetBindingSyntax>(), Expression(line is null ? null : Current.Span.Line));
            left = new LambdaSyntax(start, lambdaParameters, body);
        }
        else if (Current.Text == "null")
        {
            left = new NullSyntax(Take());
        }
        else if (Current.Text == "match")
        {
            var keyword = Take();
            var scrutinee = Expression(Current.Span.Line, stopAtBlock: true);
            Expect("{");
            var arms = new List<MatchArmSyntax>();
            while (Current.Text is not ("}" or ""))
            {
                var pattern = Pattern();
                ExpressionSyntax? guard = null;
                if (Current.Text == "when")
                {
                    Take();
                    guard = Expression(Current.Span.Line, stopAtMatchArrow: true);
                }
                Expect("=>");
                var value = Expression(Current.Span.Line);
                var end = tokens[position - 1];
                arms.Add(new(pattern, guard, value));
                if (Current.Text != "}" && Current.Span.Line == end.Span.Line)
                    throw Error("Expected a newline after a match arm.");
            }
            if (arms.Count == 0) throw Error("A match expression requires at least one arm.");
            Expect("}");
            left = new MatchSyntax(keyword, scrutinee, arms);
        }
        else if (Current.Text == "if")
        {
            var keyword = Take();
            var condition = Expression(Current.Span.Line, stopAtBlock: true);
            var thenBlock = Block();
            var hasElse = Current.Text == "else";
            var missing = Current;
            if (hasElse) Take();
            var elseBlock = hasElse ? Block() : new BlockSyntax(Array.Empty<LetBindingSyntax>(), new UnitSyntax(missing));
            left = new IfSyntax(keyword, condition, thenBlock, elseBlock) { HasElse = hasElse, MissingElseLocation = missing };
        }
        else if (Current.Text is "-" or "!")
        {
            var op = Take();
            left = new UnarySyntax(op, Expression(line is null ? null : Current.Span.Line, 7, stopAtBlock, stopAtMatchArrow));
        }
        else if (Current.Text == "(")
        {
            var open = Take();
            if (Current.Text == ")") { var close = Take(); left = new UnitSyntax(close); }
            else { var inner = Expression(); Expect(")"); left = new GroupSyntax(open, inner); }
        }
        else
        {
            var text = Current.Text;
            if (text.Length == 0 || !(text[0] is '"' or '\'' || text is "true" or "false" || text.All(char.IsAsciiDigit) ||
                ((char.IsAsciiLetter(text[0]) || text[0] == '_') && !Reserved.Contains(text))))
                throw new SourceError(new("DS2002", path, Current.Span, "Expected a supported literal, local or parenthesized expression."));
            var name = Take();
            var genericCall = HasGenericCallShape();
            if (name.Text[0] is not ('"' or '\'') && name.Text is not ("true" or "false") &&
                (genericCall || (Current.Text == "(" && (line is null || Current.Span.Line == name.Span.Line))))
            {
                var typeArguments = genericCall ? CallTypeArguments() : Array.Empty<Token>();
                var open = Expect("(");
                var args = new List<ExpressionSyntax>();
                if (Current.Text != ")")
                {
                    do
                    {
                        args.Add(Expression());
                        if (Current.Text != ",") break;
                        Take();
                    } while (true);
                }
                Expect(")");
                left = new CallSyntax(name, open, args) { TypeArguments = typeArguments };
            }
            else if ((!stopAtBlock || ConditionRecordConstruction()) && name.Text[0] is not ('"' or '\'') && name.Text is not ("true" or "false") &&
                !char.IsAsciiDigit(name.Text[0]) && Current.Text == "{" && (line is null || Current.Span.Line == name.Span.Line))
                left = new RecordConstructionSyntax(name, RecordInitializers());
            else left = new ValueSyntax(name);
        }
        while (true)
        {
            if (Current.Text == "(" && (line is null || Current.Span.Line == tokens[position - 1].Span.Line))
            {
                var open = Take();
                var args = new List<ExpressionSyntax>();
                if (Current.Text != ")")
                {
                    do
                    {
                        args.Add(Expression());
                        if (Current.Text != ",") break;
                        Take();
                    } while (true);
                }
                Expect(")");
                left = new InvokeSyntax(left, open, args);
                continue;
            }
            // A leading member operator continues across a newline.
            if (Current.Text == "?.")
            {
                var optionalOperator = Take();
                var field = Identifier();
                if (Current.Text == "(" && (line is null || Current.Span.Line == field.Span.Line))
                    throw new SourceError(new("DS1002", path, optionalOperator.Span, "Conditional method calls are not supported."));
                left = new ConditionalFieldAccessSyntax(left, optionalOperator, field);
                continue;
            }
            if (Current.Text == ".")
            {
                Take();
                var member = Identifier();
                var genericCall = HasGenericCallShape();
                if (genericCall || (Current.Text == "(" && (line is null || Current.Span.Line == member.Span.Line)))
                {
                    var typeArguments = genericCall ? CallTypeArguments() : Array.Empty<Token>();
                    var open = Expect("(");
                    var args = new List<ExpressionSyntax>();
                    if (Current.Text != ")")
                    {
                        do
                        {
                            args.Add(Expression());
                            if (Current.Text != ",") break;
                            Take();
                        } while (true);
                    }
                    Expect(")");
                    left = new MemberCallSyntax(left, member, open, args) { TypeArguments = typeArguments };
                }
                else left = new FieldAccessSyntax(left, member);
                continue;
            }
            if (line is not null && Current.Span.Line != tokens[position - 1].Span.Line) break;
            if (Current.Text == "?")
            {
                left = new PropagateSyntax(left, Take());
                continue;
            }
            if (Current.Text == "with")
            {
                var with = Take();
                left = new RecordUpdateSyntax(left, with, RecordInitializers());
                continue;
            }
            var precedence = Current.Text switch { "??" => 0, "||" => 1, "&&" => 2, "==" or "!=" => 3,
                "<" or "<=" or ">" or ">=" => 4, "+" or "-" => 5, "*" or "/" or "%" => 6, _ => -1 };
            if (precedence < minimumPrecedence) break;
            var op = Take();
            var rightLine = line is not null ? Current.Span.Line : line;
            var right = Expression(rightLine, op.Text == "??" ? precedence : precedence + 1, stopAtBlock, stopAtMatchArrow);
            static bool Comparison(ExpressionSyntax expr) => expr is BinarySyntax binary &&
                binary.Operator.Text is "==" or "!=" or "<" or "<=" or ">" or ">=";
            if (precedence is 3 or 4 && (Comparison(left) || Comparison(right)))
                throw new SourceError(new("DS2013", path, op.Span, "Chained comparisons require explicit grouping."));
            left = op.Text == "??" ? new CoalesceSyntax(left, op, right) : new BinarySyntax(left, op, right);
        }
        return left;
    }
    private static bool AllowsContinuation(ExpressionSyntax expression) => expression switch
    {
        IfSyntax or MatchSyntax or GroupSyntax or CallSyntax or MemberCallSyntax or InvokeSyntax or LambdaSyntax or BinarySyntax or CoalesceSyntax or PropagateSyntax or RecordConstructionSyntax or FieldAccessSyntax or ConditionalFieldAccessSyntax or RecordUpdateSyntax => true,
        UnarySyntax unary => AllowsContinuation(unary.Operand),
        _ => false
    };
    private PatternSyntax Pattern()
    {
        if (Current.Text == "null") return new NullPatternSyntax(Take());
        if (Current.Text == "_") return new WildcardPatternSyntax(Take());
        if (Current.Text == "(")
        {
            var open = Take();
            Expect(")");
            return new UnitPatternSyntax(open);
        }
        Token? minus = null;
        if (Current.Text == "-") minus = Take();
        var text = Current.Text;
        if (text.Length > 0 && (text[0] is '"' or '\'' || text is "true" or "false" || text.All(char.IsAsciiDigit)))
        {
            if (minus is not null && !text.All(char.IsAsciiDigit)) throw Error("Expected an integer after '-'.");
            return new LiteralPatternSyntax(Take(), minus);
        }
        if (minus is not null) throw Error("Expected an integer after '-'.");
        var first = Identifier();
        Token? qualifier = null;
        var name = first;
        if (Current.Text == ".")
        {
            Take();
            qualifier = first;
            name = Identifier();
        }
        if (Current.Text == "(")
        {
            Take();
            var arguments = new List<PatternSyntax>();
            if (Current.Text != ")")
            {
                do
                {
                    arguments.Add(Pattern());
                    if (Current.Text != ",") break;
                    Take();
                } while (true);
            }
            Expect(")");
            return new CasePatternSyntax(qualifier, name, arguments);
        }
        return qualifier is null ? new NamePatternSyntax(name) : new CasePatternSyntax(qualifier, name, Array.Empty<PatternSyntax>());
    }
    private IReadOnlyList<RecordInitializerSyntax> RecordInitializers()
    {
        Expect("{");
        var fields = new List<RecordInitializerSyntax>();
        while (Current.Text is not ("}" or ""))
        {
            var name = Identifier();
            Expect("=");
            var value = Expression(Current.Span.Line);
            var end = tokens[position - 1];
            fields.Add(new(name, value));
            if (Current.Text != "}" && Current.Span.Line == end.Span.Line)
                throw Error("Expected a newline after a record field initializer.");
        }
        Expect("}");
        return fields;
    }
    private IReadOnlyList<Token> Documentation(params string[] declarations)
    {
        if (!Current.IsDocumentation) return Array.Empty<Token>();
        var comments = new List<Token>();
        while (Current.IsDocumentation)
        {
            var current = Current;
            if (current.HasCodeBeforeOnLine || (comments.Count > 0 &&
                (current.HasInterveningComment || current.Span.Line != comments[^1].Span.Line + 1)))
                throw new SourceError(new("DS1004", path, comments.Count > 0 ? comments[^1].Span : current.Span, "Documentation must be a contiguous group on separate lines."));
            comments.Add(Take());
        }
        if (Current.HasInterveningComment || Current.Span.Line != comments[^1].Span.Line + 1 || !declarations.Contains(Current.Text))
            throw new SourceError(new("DS1004", path, comments[^1].Span, "Documentation must immediately precede a declaration."));
        return comments;
    }
    private BlockSyntax Block()
    {
        Expect("{");
        var bindings = new List<LetBindingSyntax>();
        var statements = new List<StatementSyntax>();
        ExpressionSyntax? result = null;
        while (Current.Text != "}" && Current.Text != "")
        {
            if (Current.Text == "let")
            {
                var keyword = Take(); var local = Identifier(); Token? annotation = null;
                if (Current.Text == ":") { Take(); annotation = TypeToken(); }
                var equals = Expect("=");
                if (equals.Span.Line != keyword.Span.Line)
                    throw new SourceError(new("DS1002", path, equals.Span, "A binding header must be on one line."));
                var first = Current; var value = Expression(first.Span.Line);
                var end = tokens[position - 1];
                if (end.Span.Line != keyword.Span.Line && first.Span.Line == keyword.Span.Line && !AllowsContinuation(value)) throw new SourceError(new("DS1002", path, end.Span, "Write each let binding on one line."));
                if (Current.Span.Line == end.Span.Line) throw Error("A let binding must be followed by a newline and a final expression or another binding.");
                var binding = new LetBindingSyntax(local, annotation, first) { Expression = value };
                bindings.Add(binding); statements.Add(new LetStatementSyntax(binding)); continue;
            }
            if (Current.Text == "return")
            {
                var keyword = Take();
                var value = Current.Text is "}" or "" || Current.Span.Line != keyword.Span.Line
                    ? (ExpressionSyntax)new UnitSyntax(keyword) : Expression(keyword.Span.Line);
                var end = tokens[position - 1];
                if (Current.Text != "}" && Current.Span.Line == end.Span.Line) throw Error("Expected a newline after return.");
                statements.Add(new ReturnStatementSyntax(keyword, value)); continue;
            }
            var expression = Expression(Current.Span.Line);
            if (Current.Text == "=") throw new SourceError(new("DS2005", path, expression.Token.Span, "Local bindings cannot be reassigned."));
            if (Current.Text == "}") { result = expression; break; }
            var last = tokens[position - 1];
            if (Current.Span.Line == last.Span.Line) throw Error("Expected a newline after the expression.");
            statements.Add(new ExpressionStatementSyntax(expression));
        }
        var close = Expect("}");
        result ??= new UnitSyntax(close);
        return new(bindings, result) { Statements = statements };
    }
    public ModuleSyntax Parse()
    {
        var moduleDocs = Documentation("module");
        Expect("module"); var name = Identifier();
        var functions = new List<FunctionSyntax>();
        var records = new List<RecordDeclarationSyntax>();
        var unions = new List<UnionDeclarationSyntax>();
        while (Current.Text != "")
        {
            var declarationDocs = Documentation("fn", "type");
            if (Current.Text == "type")
            {
                Take(); var recordName = Identifier(); var typeParameters = TypeParameters(); Expect("{");
                if (Current.Text == "|")
                {
                    var cases = new List<UnionCaseSyntax>();
                    while (Current.Text == "|")
                    {
                        Take();
                        var caseName = Identifier();
                        var payloads = new List<Token>();
                        if (Current.Text == "(")
                        {
                            Take();
                            if (Current.Text != ")")
                            {
                                do
                                {
                                    if (Current.Text is "" or ")" or ",") throw Error("Expected a union payload type.");
                                    payloads.Add(TypeToken());
                                    if (Current.Text != ",") break;
                                    Take();
                                } while (true);
                            }
                            Expect(")");
                        }
                        cases.Add(new(caseName, payloads));
                    }
                    Expect("}");
                    unions.Add(new(recordName, cases) { DocumentationComments = declarationDocs, TypeParameters = typeParameters });
                    continue;
                }
                var fields = new List<RecordFieldSyntax>();
                while (Current.Text is not ("}" or ""))
                {
                    var fieldName = Identifier(); Expect(":");
                    if (Current.Text == "") throw Error("Expected a field type.");
                    var fieldType = TypeToken();
                    fields.Add(new(fieldName, fieldType));
                    if (Current.Text != "}" && Current.Span.Line == fieldType.Span.Line)
                        throw Error("Expected a newline after a record field declaration.");
                }
                Expect("}");
                records.Add(new(recordName, fields) { DocumentationComments = declarationDocs, TypeParameters = typeParameters });
                continue;
            }
            Expect("fn"); var function = Identifier(); var functionTypeParameters = TypeParameters(); Expect("(");
            var parameters = new List<ParameterSyntax>();
            if (Current.Text != ")")
            {
                do
                {
                    var parameter = Identifier(); Expect(":");
                    if (Current.Text == "") throw Error("Expected a parameter type.");
                    parameters.Add(new(parameter, TypeToken()));
                    if (Current.Text != ",") break;
                    Take();
                } while (true);
            }
            Expect(")"); Expect(":");
            var type = TypeToken();
            var body = Block();
            // Prefer the missing-result span over a following stray closing brace.
            if (Current.Text == "}" && type.Text == "int" && body.Result is UnitSyntax missing &&
                body.Statements.All(statement => statement is LetStatementSyntax))
                throw new SourceError(new("DS2002", path, missing.Token.Span, "A reachable int result is required."));
            functions.Add(new(function, type, body.Result.Token) { Expression = body.Result, Body = body, DocumentationComments = declarationDocs, TypeParameters = functionTypeParameters, Parameters = parameters.Count == 0 ? Array.Empty<ParameterSyntax>() : parameters, Bindings = body.Bindings });
        }
        if (functions.Count == 0 && records.Count == 0 && unions.Count == 0) throw Error("Expected at least one declaration.");
        return new(name, functions) { DocumentationComments = moduleDocs, Records = records, Unions = unions };
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
        // Give every declared type parameter a distinct internal identity. The
        // lexer cannot produce '$', so these symbols cannot capture a source
        // type, even when a type parameter shadows a nominal type or Unit.
        var nextTypeSymbol = 0;
        var userTypeSymbolCount = syntax.Records.Sum(record => record.TypeParameters.Count) +
            syntax.Unions.Sum(union => union.TypeParameters.Count) + syntax.Functions.Sum(function => function.TypeParameters.Count);
        var resultSymbol = "$" + userTypeSymbolCount.ToString(CultureInfo.InvariantCulture);
        var optionSymbol = "$" + (userTypeSymbolCount + 1).ToString(CultureInfo.InvariantCulture);
        var resultUsed = false;
        var optionUsed = false;
        var userResultType = syntax.Records.Any(record => record.Name.Text == "Result") ||
            syntax.Unions.Any(union => union.Name.Text == "Result");
        var userOptionType = syntax.Records.Any(record => record.Name.Text == "Option") ||
            syntax.Unions.Any(union => union.Name.Text == "Option");
        var typeSymbolNames = new Dictionary<string, string>(StringComparer.Ordinal);
        var functionTypeScopes = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        Dictionary<string, string> TypeScope(IReadOnlyList<Token> parameters)
        {
            var scope = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var parameter in parameters)
            {
                if (scope.ContainsKey(parameter.Text)) Fail("DS2023", parameter, "Duplicate type parameter.");
                var symbol = "$" + (nextTypeSymbol++).ToString(CultureInfo.InvariantCulture);
                scope.Add(parameter.Text, symbol);
                typeSymbolNames.Add(symbol, parameter.Text);
            }
            return scope;
        }
        Token ScopedType(Token token, IReadOnlyDictionary<string, string> scope)
        {
            if (!GenericTypes.TryParse(token.Text, out var shape)) return token;
            TypeShape Rebind(TypeShape value)
            {
                var name = value.Name;
                if (scope.TryGetValue(name, out var symbol)) name = symbol;
                else if (name == "Result" && !userResultType) { name = resultSymbol; resultUsed = true; }
                else if (name == "Option" && !userOptionType) { name = optionSymbol; optionUsed = true; }
                return value with { Name = name, Arguments = value.Arguments.Select(Rebind).ToArray() };
            }
            return token with { Text = Rebind(shape).ToString() };
        }
        Token ScopedParameter(Token token, IReadOnlyDictionary<string, string> scope) => token with { Text = scope[token.Text] };
        var scopedRecords = syntax.Records.Select(record =>
        {
            var scope = TypeScope(record.TypeParameters);
            return record with
            {
                TypeParameters = record.TypeParameters.Select(parameter => ScopedParameter(parameter, scope)).ToArray(),
                Fields = record.Fields.Select(field => field with { Type = ScopedType(field.Type, scope) }).ToArray()
            };
        }).ToArray();
        var scopedUnions = syntax.Unions.Select(union =>
        {
            var scope = TypeScope(union.TypeParameters);
            return union with
            {
                TypeParameters = union.TypeParameters.Select(parameter => ScopedParameter(parameter, scope)).ToArray(),
                Cases = union.Cases.Select(unionCase => unionCase with
                {
                    PayloadTypes = unionCase.PayloadTypes.Select(type => ScopedType(type, scope)).ToArray()
                }).ToArray()
            };
        }).ToArray();
        var scopedFunctions = syntax.Functions.Select(function =>
        {
            var scope = TypeScope(function.TypeParameters);
            functionTypeScopes.TryAdd(function.Name.Text, scope);
            return function with
            {
                TypeParameters = function.TypeParameters.Select(parameter => ScopedParameter(parameter, scope)).ToArray(),
                Parameters = function.Parameters.Select(parameter => parameter with { Type = ScopedType(parameter.Type, scope) }).ToArray(),
                ReturnType = ScopedType(function.ReturnType, scope)
            };
        }).ToArray();
        nextTypeSymbol += 2; // Reserve the intrinsic nominal identities after source symbols.
        var syntheticSpan = new SourceSpan(0, 0, 1, 1);
        var resultTypeTokens = new[] { new Token("T", syntheticSpan), new Token("E", syntheticSpan) };
        var resultScope = TypeScope(resultTypeTokens);
        var standardResult = new UnionDeclarationSyntax(new Token(resultSymbol, syntheticSpan), new[]
        {
            new UnionCaseSyntax(new Token("Success", syntheticSpan), new[] { ScopedType(resultTypeTokens[0], resultScope) }),
            new UnionCaseSyntax(new Token("Error", syntheticSpan), new[] { ScopedType(resultTypeTokens[1], resultScope) })
        }) { TypeParameters = resultTypeTokens.Select(parameter => ScopedParameter(parameter, resultScope)).ToArray() };
        var optionTypeTokens = new[] { new Token("T", syntheticSpan) };
        var optionScope = TypeScope(optionTypeTokens);
        var standardOption = new UnionDeclarationSyntax(new Token(optionSymbol, syntheticSpan), new[]
        {
            new UnionCaseSyntax(new Token("None", syntheticSpan), Array.Empty<Token>()),
            new UnionCaseSyntax(new Token("Some", syntheticSpan), new[] { ScopedType(optionTypeTokens[0], optionScope) })
        }) { TypeParameters = optionTypeTokens.Select(parameter => ScopedParameter(parameter, optionScope)).ToArray() };
        scopedUnions = scopedUnions.Append(standardResult).Append(standardOption).ToArray();
        syntax = syntax with { Records = scopedRecords, Unions = scopedUnions, Functions = scopedFunctions };
        string DisplayType(string type, bool labelParameters = false)
        {
            if (!GenericTypes.TryParse(type, out var shape)) return type;
            TypeShape Display(TypeShape value) => value with
            {
                Name = value.Name == resultSymbol ? "Result" : value.Name == optionSymbol ? "Option" : typeSymbolNames.TryGetValue(value.Name, out var name)
                    ? name + (labelParameters ? " (type parameter)" : "") : value.Name,
                Arguments = value.Arguments.Select(Display).ToArray()
            };
            return Display(shape).ToString();
        }
        var records = new Dictionary<string, RecordDeclarationSyntax>(StringComparer.Ordinal);
        foreach (var record in syntax.Records)
        {
            if (record.Name.Text == "Unit" || !records.TryAdd(record.Name.Text, record))
                Fail("DS2015", record.Name, "Duplicate or reserved record type name.");
        }
        var unions = new Dictionary<string, UnionDeclarationSyntax>(StringComparer.Ordinal);
        foreach (var union in syntax.Unions)
        {
            if (union.Name.Text == "Unit" || !unions.TryAdd(union.Name.Text, union))
                Fail("DS2017", union.Name, "Duplicate or reserved union type name.");
            if (records.TryGetValue(union.Name.Text, out var conflictingRecord))
                Fail("DS2017", union.Name.Span.Start > conflictingRecord.Name.Span.Start ? union.Name : conflictingRecord.Name,
                    "A record and union cannot share a type name.");
        }
        bool IsNullable(string type) => type.EndsWith("?", StringComparison.Ordinal);
        var arities = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["int"] = 0, ["bool"] = 0, ["string"] = 0, ["char"] = 0, ["Unit"] = 0
        };
        foreach (var record in syntax.Records) arities.Add(record.Name.Text, record.TypeParameters.Count);
        foreach (var union in syntax.Unions) arities.Add(union.Name.Text, union.TypeParameters.Count);
        var emptyParameters = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> Parameters(IReadOnlyList<Token> parameters)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var parameter in parameters)
            {
                if (!names.Add(parameter.Text)) Fail("DS2023", parameter, "Duplicate type parameter.");
            }
            return names;
        }
        bool SupportedType(string type, IReadOnlySet<string>? parameters = null)
        {
            var scope = parameters ?? emptyParameters;
            if (!GenericTypes.TryValidate(type, arities, scope) || !GenericTypes.TryParse(type, out var shape)) return false;
            bool ExecutionTypesOnly(TypeShape value)
            {
                if (!value.IsFunction && value.Arguments.Any(argument => argument.ContainsFunction)) return false;
                return value.Arguments.All(ExecutionTypesOnly);
            }
            return ExecutionTypesOnly(shape);
        }
        bool DataType(string type) => GenericTypes.TryParse(type, out var shape) && !shape.ContainsFunction;
        foreach (var record in syntax.Records)
        {
            var typeParameters = Parameters(record.TypeParameters);
            var fields = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in record.Fields)
            {
                if (!fields.Add(field.Name.Text)) Fail("DS2015", field.Name, "Duplicate record field.");
                if (!SupportedType(field.Type.Text, typeParameters) || !DataType(field.Type.Text)) Fail("DS2015", field.Type, "Unsupported record field type or function value in data.");
            }
        }
        var cases = new Dictionary<string, List<(UnionDeclarationSyntax Union, UnionCaseSyntax Case)>>(StringComparer.Ordinal);
        foreach (var union in syntax.Unions)
        {
            var typeParameters = Parameters(union.TypeParameters);
            var caseNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var unionCase in union.Cases)
            {
                if (!caseNames.Add(unionCase.Name.Text)) Fail("DS2017", unionCase.Name, "Duplicate union case.");
                foreach (var type in unionCase.PayloadTypes)
                    if (!SupportedType(type.Text, typeParameters) || !DataType(type.Text)) Fail("DS2017", type, "Unsupported union payload type or function value in data.");
                // Standard cases are a prelude; current-module declarations
                // take precedence before those names are considered.
                if (union.Name.Text == resultSymbol || union.Name.Text == optionSymbol) continue;
                if (!cases.TryGetValue(unionCase.Name.Text, out var candidates))
                    cases.Add(unionCase.Name.Text, candidates = new());
                candidates.Add((union, unionCase));
            }
        }
        var signatures = new Dictionary<string, FunctionSyntax>(StringComparer.Ordinal);
        foreach (var function in syntax.Functions)
        {
            var typeParameters = Parameters(function.TypeParameters);
            if (!signatures.TryAdd(function.Name.Text, function)) Fail("DS2001", function.Name, "Duplicate function declaration.");
            if (records.TryGetValue(function.Name.Text, out var conflictingRecord))
                Fail("DS2015", function.Name.Span.Start > conflictingRecord.Name.Span.Start ? function.Name : conflictingRecord.Name,
                    "A function and record constructor cannot share a name.");
            if (!SupportedType(function.ReturnType.Text, typeParameters)) Fail("DS2003", function.ReturnType, "Unsupported return type or unconstrained nullable type parameter.");
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var parameter in function.Parameters)
            {
                if (!names.Add(parameter.Name.Text)) Fail("DS2010", parameter.Name, "Duplicate parameter.");
                if (!SupportedType(parameter.Type.Text, typeParameters)) Fail("DS2011", parameter.Type, "Unsupported parameter type or unconstrained nullable type parameter.");
            }
        }
        foreach (var unionCase in standardResult.Cases)
            if (!cases.ContainsKey(unionCase.Name.Text) && !signatures.ContainsKey(unionCase.Name.Text) && !records.ContainsKey(unionCase.Name.Text))
                cases.Add(unionCase.Name.Text, new() { (standardResult, unionCase) });
        foreach (var unionCase in standardOption.Cases)
            if (!cases.ContainsKey(unionCase.Name.Text) && !signatures.ContainsKey(unionCase.Name.Text) && !records.ContainsKey(unionCase.Name.Text))
                cases.Add(unionCase.Name.Text, new() { (standardOption, unionCase) });
        var functions = new List<BoundFunction>();
        foreach (var function in syntax.Functions)
        {
            var functionTypeScope = functionTypeScopes[function.Name.Text];
            var activeTypeParameters = Parameters(function.TypeParameters);
            var locals = new Dictionary<string, (int Value, string Type)>(StringComparer.Ordinal);
            foreach (var parameter in function.Parameters) locals.Add(parameter.Name.Text, (0, parameter.Type.Text));
            var inaccessibleCaptures = new HashSet<string>(StringComparer.Ordinal);
            var boundLocals = new List<BoundLocal>();
            bool hasReturn = false;
            string? currentReturnType = function.ReturnType.Text;
            List<string>? inferredReturns = null;
            bool Convertible(string actual, string expected) => actual == "<never>" || actual == expected ||
                (IsNullable(expected) && (actual == "<null>" || actual == expected[..^1]));
            string? CommonType(string left, string right)
            {
                if (left == "<never>") return right;
                if (right == "<never>") return left;
                if (left == right) return left;
                if (left == "<null>") return IsNullable(right) ? right : right + "?";
                if (right == "<null>") return IsNullable(left) ? left : left + "?";
                if (IsNullable(left) && left[..^1] == right) return left;
                if (IsNullable(right) && right[..^1] == left) return right;
                return null;
            }
            void Require(ExpressionSyntax syntaxNode, string actual, string expected)
            {
                if (!Convertible(actual, expected))
                {
                    var expectedName = DisplayType(expected);
                    var actualName = DisplayType(actual);
                    if (expected != actual && expectedName == actualName)
                    {
                        expectedName = DisplayType(expected, true);
                        actualName = DisplayType(actual, true);
                    }
                    Fail("DS2002", syntaxNode.Token, $"Expected {expectedName}, found {actualName}.");
                }
            }
            string Nominal(string type) => GenericTypes.TryParse(type, out var shape) ? shape.Name : type;
            string UnionQualifier(string name) => name == "Result" && !userResultType ? resultSymbol :
                name == "Option" && !userOptionType ? optionSymbol : name;
            string GenericPattern(string name, IReadOnlyList<Token> parameters) => parameters.Count == 0 ? name :
                name + "<" + string.Join(",", parameters.Select(parameter => parameter.Text)) + ">";
            string Substitute(string type, IReadOnlyDictionary<string, string> substitutions, Token location)
            {
                if (!GenericTypes.TrySubstitute(type, substitutions, out var substituted))
                    Fail("DS2023", location, "Invalid generic type substitution.");
                return substituted;
            }
            Dictionary<string, string>? SubstitutionsFor(string? type, string name, IReadOnlyList<Token> parameters)
            {
                if (type is null || !GenericTypes.TryParse(type, out var shape) || shape.Name != name ||
                    shape.Arguments.Count != parameters.Count) return null;
                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                for (var i = 0; i < parameters.Count; i++) values.Add(parameters[i].Text, shape.Arguments[i].ToString());
                return values;
            }
            (IReadOnlyList<BoundExpression> Values, IReadOnlyList<string> Types, Dictionary<string, string> Substitutions, bool Never)
                BindGenericArguments(IReadOnlyList<Token> typeParameters, IReadOnlyList<Token> explicitTypes,
                    IReadOnlyList<Token> formalTypes, IReadOnlyList<ExpressionSyntax> arguments, Token location,
                    string? returnPattern = null, string? expectedResult = null)
            {
                if (formalTypes.Count != arguments.Count) Fail("DS2012", location, "Incorrect number of arguments.");
                if (explicitTypes.Count > 0 && explicitTypes.Count != typeParameters.Count)
                    Fail("DS2023", location, "Incorrect number of type arguments.");
                var parameters = typeParameters.Select(parameter => parameter.Text).ToHashSet(StringComparer.Ordinal);
                var substitutions = new Dictionary<string, string>(StringComparer.Ordinal);
                var fixedParameters = new HashSet<string>(StringComparer.Ordinal);
                for (var i = 0; i < explicitTypes.Count; i++)
                {
                    if (!SupportedType(explicitTypes[i].Text, activeTypeParameters))
                        Fail("DS2023", explicitTypes[i], "Unsupported type argument.");
                    substitutions.Add(typeParameters[i].Text, explicitTypes[i].Text);
                    fixedParameters.Add(typeParameters[i].Text);
                }
                if (returnPattern is not null && expectedResult is not null && typeParameters.Count > 0)
                {
                    var inferred = new Dictionary<string, string>(substitutions, StringComparer.Ordinal);
                    if (GenericTypes.TryUnify(returnPattern, expectedResult, parameters, inferred, fixedParameters))
                    {
                        foreach (var entry in inferred) { substitutions[entry.Key] = entry.Value; fixedParameters.Add(entry.Key); }
                    }
                }
                // Record nullability constraints even when nested constructors
                // need other arguments before they can be bound. This pass only
                // inspects syntax; all arguments are bound and rechecked below.
                var nullableRequired = new HashSet<string>(StringComparer.Ordinal);
                bool NeverBlock(BlockSyntax block) => block.Statements.Any(statement => statement switch
                {
                    ReturnStatementSyntax => true,
                    LetStatementSyntax binding => NeverExpression(binding.Binding.Expression ?? new ValueSyntax(binding.Binding.Initializer)),
                    ExpressionStatementSyntax statementExpression => NeverExpression(statementExpression.Expression),
                    _ => false
                }) || NeverExpression(block.Result);
                bool NeverExpression(ExpressionSyntax expression) => expression switch
                {
                    GroupSyntax group => NeverExpression(group.Inner),
                    PropagateSyntax propagation => NeverExpression(propagation.Operand),
                    IfSyntax conditional => NeverExpression(conditional.Condition) ||
                        conditional.HasElse && NeverBlock(conditional.Then) && NeverBlock(conditional.Else),
                    MatchSyntax match => NeverExpression(match.Scrutinee) || match.Arms.All(arm =>
                        arm.Guard is not null && NeverExpression(arm.Guard) || NeverExpression(arm.Value)),
                    CallSyntax call => call.Arguments.Any(NeverExpression),
                    InvokeSyntax invocation => NeverExpression(invocation.Receiver) || invocation.Arguments.Any(NeverExpression),
                    MemberCallSyntax member => NeverExpression(member.Receiver) || member.Arguments.Any(NeverExpression),
                    RecordConstructionSyntax record => record.Fields.Any(field => NeverExpression(field.Value)),
                    RecordUpdateSyntax update => NeverExpression(update.Receiver) || update.Fields.Any(field => NeverExpression(field.Value)),
                    FieldAccessSyntax access => NeverExpression(access.Receiver),
                    ConditionalFieldAccessSyntax access => NeverExpression(access.Receiver),
                    UnarySyntax unary => NeverExpression(unary.Operand),
                    BinarySyntax binary => NeverExpression(binary.Left) ||
                        binary.Operator.Text is not ("&&" or "||") && NeverExpression(binary.Right),
                    CoalesceSyntax coalesce => NeverExpression(coalesce.Left),
                    _ => false
                };
                void CollectNullConstraints(ExpressionSyntax expression, string formal)
                {
                    if (NeverExpression(expression) || !GenericTypes.TryParse(formal, out var shape)) return;
                    if (expression is GroupSyntax group) { CollectNullConstraints(group.Inner, formal); return; }
                    if (expression is NullSyntax)
                    {
                        if (!shape.IsNullable && shape.Arguments.Count == 0 && parameters.Contains(shape.Name)) nullableRequired.Add(shape.Name);
                        return;
                    }
                    if (expression is IfSyntax conditional)
                    {
                        if (!NeverBlock(conditional.Then)) CollectNullConstraints(conditional.Then.Result, formal);
                        if (conditional.HasElse && !NeverBlock(conditional.Else)) CollectNullConstraints(conditional.Else.Result, formal);
                        return;
                    }
                    if (expression is MatchSyntax match)
                    {
                        foreach (var arm in match.Arms)
                            if (arm.Guard is null || !NeverExpression(arm.Guard)) CollectNullConstraints(arm.Value, formal);
                        return;
                    }
                    if (expression is CoalesceSyntax coalesce)
                    {
                        CollectNullConstraints(coalesce.Right, formal);
                        return;
                    }
                    if (expression is RecordConstructionSyntax construction && construction.Name.Text == shape.Name &&
                        records.TryGetValue(shape.Name, out var record) && record.TypeParameters.Count == shape.Arguments.Count)
                    {
                        var map = record.TypeParameters.Select((parameter, index) => (parameter.Text, Type: shape.Arguments[index].ToString()))
                            .ToDictionary(pair => pair.Text, pair => pair.Type, StringComparer.Ordinal);
                        foreach (var field in construction.Fields)
                        {
                            var declaration = record.Fields.FirstOrDefault(item => item.Name.Text == field.Name.Text);
                            if (declaration is not null && GenericTypes.TrySubstitute(declaration.Type.Text, map, out var fieldType))
                                CollectNullConstraints(field.Value, fieldType);
                        }
                        return;
                    }
                    if (!unions.TryGetValue(shape.Name, out var union) || union.TypeParameters.Count != shape.Arguments.Count) return;
                    Token? caseName = null;
                    IReadOnlyList<ExpressionSyntax>? payloads = null;
                    if (expression is CallSyntax call && !locals.ContainsKey(call.Name.Text) && cases.TryGetValue(call.Name.Text, out var candidates) &&
                        candidates.Count == 1 && !signatures.ContainsKey(call.Name.Text) && !records.ContainsKey(call.Name.Text))
                    { caseName = call.Name; payloads = call.Arguments; }
                    else if (expression is MemberCallSyntax member && member.Receiver is ValueSyntax qualifier &&
                        UnionQualifier(qualifier.Value.Text) == union.Name.Text && !locals.ContainsKey(qualifier.Value.Text))
                    { caseName = member.Member; payloads = member.Arguments; }
                    if (caseName is null || payloads is null) return;
                    var unionCase = union.Cases.FirstOrDefault(item => item.Name.Text == caseName.Text);
                    if (unionCase is null || unionCase.PayloadTypes.Count != payloads.Count) return;
                    var caseMap = union.TypeParameters.Select((parameter, index) => (parameter.Text, Type: shape.Arguments[index].ToString()))
                        .ToDictionary(pair => pair.Text, pair => pair.Type, StringComparer.Ordinal);
                    for (var i = 0; i < payloads.Count; i++)
                        if (GenericTypes.TrySubstitute(unionCase.PayloadTypes[i].Text, caseMap, out var payloadType))
                            CollectNullConstraints(payloads[i], payloadType);
                }
                TypeShape RefineNullEvidence(ExpressionSyntax expression, TypeShape type)
                {
                    if (NeverExpression(expression)) return type;
                    if (expression is NullSyntax) return type with { IsNullable = true };
                    if (expression is GroupSyntax group) return RefineNullEvidence(group.Inner, type);
                    if (expression is CoalesceSyntax coalesce) return RefineNullEvidence(coalesce.Right, type);
                    if (expression is IfSyntax conditional)
                    {
                        if (!NeverBlock(conditional.Then)) type = RefineNullEvidence(conditional.Then.Result, type);
                        if (conditional.HasElse && !NeverBlock(conditional.Else)) type = RefineNullEvidence(conditional.Else.Result, type);
                        return type;
                    }
                    if (expression is MatchSyntax match)
                    {
                        foreach (var arm in match.Arms)
                            if (arm.Guard is null || !NeverExpression(arm.Guard)) type = RefineNullEvidence(arm.Value, type);
                        return type;
                    }
                    if (expression is RecordConstructionSyntax construction && construction.Name.Text == type.Name &&
                        records.TryGetValue(type.Name, out var record) && record.TypeParameters.Count == type.Arguments.Count)
                    {
                        var map = record.TypeParameters.Select((parameter, index) => (parameter.Text, Type: type.Arguments[index].ToString()))
                            .ToDictionary(pair => pair.Text, pair => pair.Type, StringComparer.Ordinal);
                        var recordParameters = record.TypeParameters.Select(parameter => parameter.Text).ToHashSet(StringComparer.Ordinal);
                        foreach (var field in construction.Fields)
                        {
                            var declaration = record.Fields.FirstOrDefault(item => item.Name.Text == field.Name.Text);
                            if (declaration is null || !GenericTypes.TrySubstitute(declaration.Type.Text, map, out var current) ||
                                !GenericTypes.TryParse(current, out var fieldType)) continue;
                            var refined = RefineNullEvidence(field.Value, fieldType).ToString();
                            GenericTypes.TryUnify(declaration.Type.Text, refined, recordParameters, map);
                        }
                        return type with { Arguments = record.TypeParameters.Select(parameter =>
                            GenericTypes.TryParse(map[parameter.Text], out var argument) ? argument : type.Arguments[0]).ToArray() };
                    }
                    if (!unions.TryGetValue(type.Name, out var union) || union.TypeParameters.Count != type.Arguments.Count) return type;
                    Token? caseName = null;
                    IReadOnlyList<ExpressionSyntax>? payloads = null;
                    if (expression is CallSyntax call && !locals.ContainsKey(call.Name.Text) && cases.TryGetValue(call.Name.Text, out var candidates) &&
                        candidates.Count == 1 && !signatures.ContainsKey(call.Name.Text) && !records.ContainsKey(call.Name.Text))
                    { caseName = call.Name; payloads = call.Arguments; }
                    else if (expression is MemberCallSyntax member && member.Receiver is ValueSyntax qualifier &&
                        UnionQualifier(qualifier.Value.Text) == union.Name.Text && !locals.ContainsKey(qualifier.Value.Text))
                    { caseName = member.Member; payloads = member.Arguments; }
                    if (caseName is null || payloads is null) return type;
                    var unionCase = union.Cases.FirstOrDefault(item => item.Name.Text == caseName.Text);
                    if (unionCase is null || unionCase.PayloadTypes.Count != payloads.Count) return type;
                    var caseMap = union.TypeParameters.Select((parameter, index) => (parameter.Text, Type: type.Arguments[index].ToString()))
                        .ToDictionary(pair => pair.Text, pair => pair.Type, StringComparer.Ordinal);
                    var unionParameters = union.TypeParameters.Select(parameter => parameter.Text).ToHashSet(StringComparer.Ordinal);
                    for (var i = 0; i < payloads.Count; i++)
                    {
                        var declaration = unionCase.PayloadTypes[i];
                        if (!GenericTypes.TrySubstitute(declaration.Text, caseMap, out var current) ||
                            !GenericTypes.TryParse(current, out var payloadType)) continue;
                        var refined = RefineNullEvidence(payloads[i], payloadType).ToString();
                        GenericTypes.TryUnify(declaration.Text, refined, unionParameters, caseMap);
                    }
                    return type with { Arguments = union.TypeParameters.Select(parameter =>
                        GenericTypes.TryParse(caseMap[parameter.Text], out var argument) ? argument : type.Arguments[0]).ToArray() };
                }
                for (var i = 0; i < arguments.Count; i++) CollectNullConstraints(arguments[i], formalTypes[i].Text);
                var bound = new (BoundExpression Expression, string Type)?[arguments.Count];
                var remaining = arguments.Count;
                var allowInferredHints = false;
                while (remaining > 0)
                {
                    var progress = false;
                    SourceError? deferred = null;
                    for (var i = 0; i < arguments.Count; i++)
                    {
                        if (bound[i] is not null) continue;
                        var hint = substitutions.Count == typeParameters.Count &&
                            (fixedParameters.Count == typeParameters.Count || allowInferredHints)
                            ? Substitute(formalTypes[i].Text, substitutions, formalTypes[i]) : null;
                        if (arguments[i] is LambdaSyntax && GenericTypes.TrySubstitute(formalTypes[i].Text, substitutions, out var partial) &&
                            GenericTypes.TryParse(partial, out var partialShape) && partialShape.IsFunction)
                            hint = partial;
                        (BoundExpression Expression, string Type) value;
                        try { value = BindExpression(arguments[i], hint); }
                        catch (SourceError error) when (typeParameters.Count > 0 &&
                            ((hint is null && error.Diagnostic.Code == "DS2023" && error.Diagnostic.Message == "Cannot infer all generic type arguments.") ||
                             (arguments[i] is LambdaSyntax && error.Diagnostic.Code == "DS2026")))
                        {
                            deferred ??= error;
                            continue;
                        }
                        if (typeParameters.Count > 0 && !GenericTypes.TryUnify(formalTypes[i].Text, value.Type, parameters, substitutions, fixedParameters))
                        {
                            // Fresh constructors can be rebound with the eventual
                            // context, so join nullable evidence before rejecting a
                            // nominal type mismatch. Existing values are still
                            // checked invariantly in the final rebind below.
                            var candidate = new Dictionary<string, string>(substitutions, StringComparer.Ordinal);
                            if (GenericTypes.TryParse(formalTypes[i].Text, out var formal) &&
                                !formal.IsNullable && formal.Arguments.Count == 0 && parameters.Contains(formal.Name) &&
                                !fixedParameters.Contains(formal.Name) && candidate.TryGetValue(formal.Name, out var prior) &&
                                GenericTypes.TryParse(prior, out var priorShape) && GenericTypes.TryParse(value.Type, out var valueShape))
                            {
                                TypeShape? JoinNullable(TypeShape first, TypeShape second)
                                {
                                    if (first.Name != second.Name || first.Arguments.Count != second.Arguments.Count) return null;
                                    var joined = new List<TypeShape>();
                                    for (var index = 0; index < first.Arguments.Count; index++)
                                    {
                                        var child = JoinNullable(first.Arguments[index], second.Arguments[index]);
                                        if (child is null) return null;
                                        joined.Add(child);
                                    }
                                    return first with { IsNullable = first.IsNullable || second.IsNullable, Arguments = joined };
                                }
                                var joined = JoinNullable(priorShape, valueShape);
                                if (joined is not null) candidate[formal.Name] = joined.ToString();
                            }
                            var contextual = false;
                            if (candidate.Count == typeParameters.Count)
                            {
                                var expected = Substitute(formalTypes[i].Text, candidate, formalTypes[i]);
                                try
                                {
                                    var rebound = BindExpression(arguments[i], expected);
                                    if (Convertible(rebound.Type, expected))
                                    {
                                        value = rebound;
                                        foreach (var entry in candidate) substitutions[entry.Key] = entry.Value;
                                        contextual = true;
                                    }
                                }
                                catch (SourceError) { /* Report the original conflicting evidence below. */ }
                            }
                            if (!contextual) Fail("DS2023", arguments[i].Token, "Conflicting generic type arguments.");
                        }
                        bound[i] = value; remaining--; progress = true;
                    }
                    foreach (var parameter in nullableRequired)
                    {
                        if (fixedParameters.Contains(parameter) || !substitutions.TryGetValue(parameter, out var inferred) || IsNullable(inferred)) continue;
                        if (SupportedType(inferred + "?", activeTypeParameters)) substitutions[parameter] = inferred + "?";
                    }
                    for (var i = 0; i < arguments.Count; i++)
                    {
                        if (!GenericTypes.TryParse(formalTypes[i].Text, out var formal) || formal.IsNullable || formal.Arguments.Count != 0 ||
                            !parameters.Contains(formal.Name) || fixedParameters.Contains(formal.Name) ||
                            !substitutions.TryGetValue(formal.Name, out var inferred) || !GenericTypes.TryParse(inferred, out var current)) continue;
                        var refined = RefineNullEvidence(arguments[i], current).ToString();
                        if (SupportedType(refined, activeTypeParameters)) substitutions[formal.Name] = refined;
                    }
                    if (!progress)
                    {
                        if (!allowInferredHints && substitutions.Count == typeParameters.Count)
                        {
                            allowInferredHints = true;
                            continue;
                        }
                        throw deferred ?? new SourceError(new("DS2023", path, location.Span, "Cannot infer all generic type arguments."));
                    }
                }
                if (substitutions.Count != typeParameters.Count)
                    Fail("DS2023", location, "Cannot infer all generic type arguments.");
                var values = new List<BoundExpression>();
                for (var i = 0; i < bound.Length; i++)
                {
                    var expected = Substitute(formalTypes[i].Text, substitutions, formalTypes[i]);
                    if (!SupportedType(expected, activeTypeParameters))
                        Fail("DS2026", arguments[i].Token, "Function values cannot be stored in data types.");
                    // Rebind with the final context: earlier inferred constructors
                    // may need a nullable type discovered by a later argument.
                    var value = typeParameters.Count == 0 ? bound[i]!.Value : BindExpression(arguments[i], expected);
                    Require(arguments[i], value.Type, expected);
                    values.Add(Coerce(value.Expression, expected, value.Type));
                }
                return (values, bound.Select(item => item!.Value.Type).ToArray(), substitutions, bound.Any(item => item!.Value.Type == "<never>"));
            }
            // Some noncompleting expressions still carry a known nominal type.
            // Preserve it when checking unreachable code and chained record operations.
            string? KnownType(BoundExpression expression) => expression switch
            {
                BoundLambda lambda => lambda.Type,
                BoundInvoke invocation => invocation.Type,
                BoundRecordConstruction construction => construction.Type,
                BoundRecordUpdate update => update.Type,
                BoundUnionConstruction construction => construction.Type,
                BoundNull value => value.Type,
                BoundPropagation propagation => propagation.Type,
                BoundCoalesce coalesce => coalesce.Type,
                BoundConditionalFieldAccess access => access.Type,
                BoundFieldAccess access when records.TryGetValue(Nominal(access.ReceiverType), out var record) =>
                    record.Fields.FirstOrDefault(field => field.Name.Text == access.Field) is { } field &&
                    SubstitutionsFor(access.ReceiverType, record.Name.Text, record.TypeParameters) is { } substitutions
                        ? Substitute(field.Type.Text, substitutions, field.Type) : null,
                BoundCall call when signatures.TryGetValue(call.Name, out var target) =>
                    Substitute(target.ReturnType.Text, target.TypeParameters.Select((parameter, index) => (parameter.Text, call.TypeArguments[index]))
                        .ToDictionary(pair => pair.Text, pair => pair.Item2, StringComparer.Ordinal), target.ReturnType),
                BoundNever never => never.Type,
                BoundIf conditional => conditional.Type,
                BoundMatch match => match.Type,
                _ => null
            };
            (IReadOnlyList<BoundRecordInitializer> Fields, bool Never, string Type) BindRecordFields(
                RecordDeclarationSyntax record, IReadOnlyList<RecordInitializerSyntax> initializers, Token location, bool requireAll,
                string? knownType = null)
            {
                var known = record.Fields.ToDictionary(field => field.Name.Text, StringComparer.Ordinal);
                var used = new HashSet<string>(StringComparer.Ordinal);
                var types = new List<Token>();
                foreach (var initializer in initializers)
                {
                    if (!known.TryGetValue(initializer.Name.Text, out var field))
                        Fail("DS2016", initializer.Name, "Unknown record field.");
                    if (!used.Add(initializer.Name.Text))
                        Fail("DS2016", initializer.Name, "Duplicate record field initializer.");
                    types.Add(field!.Type);
                }
                if (requireAll && used.Count != known.Count)
                    Fail("DS2016", location, "Record construction requires every field.");
                if (knownType is not null && IsNullable(knownType)) knownType = knownType[..^1];
                var resolved = BindGenericArguments(record.TypeParameters, Array.Empty<Token>(), types,
                    initializers.Select(initializer => initializer.Value).ToArray(), location,
                    GenericPattern(record.Name.Text, record.TypeParameters), knownType);
                var type = Substitute(GenericPattern(record.Name.Text, record.TypeParameters), resolved.Substitutions, location);
                if (!SupportedType(type, activeTypeParameters)) Fail("DS2026", location, "Function values cannot be stored in records.");
                var bound = initializers.Select((item, index) => new BoundRecordInitializer(item.Name.Text, resolved.Values[index])).ToArray();
                return (bound, resolved.Never, type);
            }
            (UnionDeclarationSyntax Union, UnionCaseSyntax Case) BareCase(Token name)
            {
                var candidates = cases[name.Text];
                if (candidates.Count != 1 || signatures.ContainsKey(name.Text) || records.ContainsKey(name.Text))
                    Fail("DS2018", name, "Ambiguous union case; qualify it with its union name.");
                return candidates[0];
            }
            (BoundExpression Expression, string Type) BindCase(UnionDeclarationSyntax union,
                UnionCaseSyntax unionCase, Token name, IReadOnlyList<ExpressionSyntax> arguments,
                IReadOnlyList<Token>? explicitTypes = null, string? expectedResult = null, bool called = false)
            {
                if (union.Name.Text == resultSymbol) resultUsed = true;
                if (union.Name.Text == optionSymbol) optionUsed = true;
                if (called && union.Name.Text == resultSymbol && unionCase.Name.Text == "Success" && arguments.Count == 0)
                {
                    var unitExpected = explicitTypes is { Count: > 0 }
                        ? explicitTypes.Count == 2 && explicitTypes[0].Text == "Unit"
                        : expectedResult is not null && GenericTypes.TryParse(expectedResult, out var expectedShape) &&
                            expectedShape.Name == resultSymbol && expectedShape.Arguments.Count == 2 &&
                            expectedShape.Arguments[0].ToString() == "Unit";
                    if (!unitExpected) Fail("DS2018", name, "Success() requires a Unit success type.");
                    arguments = new ExpressionSyntax[] { new UnitSyntax(name) };
                }
                if (unionCase.PayloadTypes.Count != arguments.Count)
                    Fail("DS2018", name, "Incorrect number of union case payloads.");
                if (expectedResult is not null && IsNullable(expectedResult)) expectedResult = expectedResult[..^1];
                var resolved = BindGenericArguments(union.TypeParameters, explicitTypes ?? Array.Empty<Token>(),
                    unionCase.PayloadTypes, arguments, name, GenericPattern(union.Name.Text, union.TypeParameters), expectedResult);
                var type = Substitute(GenericPattern(union.Name.Text, union.TypeParameters), resolved.Substitutions, name);
                if (!SupportedType(type, activeTypeParameters)) Fail("DS2026", name, "Function values cannot be stored in union payloads.");
                return (new BoundUnionConstruction(type, unionCase.Name.Text, resolved.Values), resolved.Never ? "<never>" : type);
            }
            (BoundExpression Expression, string Type) BindQualifiedCase(UnionDeclarationSyntax union,
                Token name, IReadOnlyList<ExpressionSyntax> arguments, IReadOnlyList<Token>? explicitTypes = null,
                string? expectedResult = null, bool called = false)
            {
                var unionCase = union.Cases.FirstOrDefault(item => item.Name.Text == name.Text);
                if (unionCase is null) Fail("DS2018", name, "Unknown union case.");
                return BindCase(union, unionCase!, name, arguments, explicitTypes, expectedResult, called);
            }
            BoundPattern BindPattern(PatternSyntax pattern, string expected, List<string> introduced)
            {
                if (pattern is WildcardPatternSyntax) return new BoundWildcardPattern();
                if (pattern is NullPatternSyntax nullPattern)
                {
                    if (!IsNullable(expected)) Fail("DS2021", nullPattern.Token, "Null requires a nullable matched type.");
                    return new BoundNullPattern();
                }
                if (IsNullable(expected))
                {
                    var contained = expected[..^1];
                    return new BoundPresentPattern(BindPattern(pattern, contained, introduced), contained);
                }
                switch (pattern)
                {
                    case UnitPatternSyntax unit:
                        if (expected != "Unit") Fail("DS2019", unit.Token, $"Expected a {DisplayType(expected)} pattern.");
                        return new BoundLiteralPattern(new BoundUnit(), "Unit");
                    case LiteralPatternSyntax literal:
                        var literalValue = BindExpression(new ValueSyntax(literal.Literal));
                        if (literal.Minus is not null && literalValue.Type != "int")
                            Fail("DS2019", literal.Token, "Only an int literal can be negative.");
                        if (literalValue.Type != expected)
                            Fail("DS2019", literal.Token, $"Expected a {DisplayType(expected)} pattern.");
                        return new BoundLiteralPattern(literal.Minus is null ? literalValue.Expression :
                            new BoundUnary("-", literalValue.Expression), literalValue.Type);
                    case NamePatternSyntax name:
                        if (locals.ContainsKey(name.Name.Text) || inaccessibleCaptures.Contains(name.Name.Text))
                            Fail("DS2006", name.Name, "A pattern binding cannot shadow a visible local or parameter.");
                        if (unions.ContainsKey(Nominal(expected)) && cases.TryGetValue(name.Name.Text, out var patternCases) &&
                            patternCases.Any(candidate => candidate.Union.Name.Text == Nominal(expected) ||
                                (candidate.Union.Name.Text != resultSymbol && candidate.Union.Name.Text != optionSymbol)))
                        {
                            var selected = BareCase(name.Name);
                            return BindCasePattern(selected.Union, selected.Case, name.Name, Array.Empty<PatternSyntax>(), expected, introduced);
                        }
                        locals.Add(name.Name.Text, (0, expected));
                        introduced.Add(name.Name.Text);
                        return new BoundBindingPattern(name.Name.Text, expected);
                    case CasePatternSyntax casePattern:
                        UnionDeclarationSyntax union;
                        UnionCaseSyntax unionCase;
                        if (casePattern.Qualifier is { } qualifier)
                        {
                            if (locals.ContainsKey(qualifier.Text) || inaccessibleCaptures.Contains(qualifier.Text))
                                Fail("DS2019", qualifier, "A local value cannot qualify a union case pattern.");
                            if (functionTypeScope.ContainsKey(qualifier.Text))
                                Fail("DS2019", qualifier, "An unconstrained type parameter cannot qualify a union case pattern.");
                            if (!unions.TryGetValue(UnionQualifier(qualifier.Text), out var qualified))
                                Fail("DS2019", qualifier, "Unknown union type in pattern.");
                            union = qualified!;
                            unionCase = union.Cases.FirstOrDefault(item => item.Name.Text == casePattern.Name.Text)!;
                            if (unionCase is null) Fail("DS2019", casePattern.Name, "Unknown union case in pattern.");
                        }
                        else
                        {
                            if (locals.ContainsKey(casePattern.Name.Text) || inaccessibleCaptures.Contains(casePattern.Name.Text))
                                Fail("DS2019", casePattern.Name, "A local value cannot be used as a case pattern.");
                            if (!cases.ContainsKey(casePattern.Name.Text))
                                Fail("DS2019", casePattern.Name, "Unknown union case in pattern.");
                            (union, unionCase) = BareCase(casePattern.Name);
                        }
                        return BindCasePattern(union, unionCase!, casePattern.Name, casePattern.Arguments, expected, introduced);
                    default: throw new InvalidOperationException("Unknown pattern syntax.");
                }
            }
            BoundPattern BindCasePattern(UnionDeclarationSyntax union, UnionCaseSyntax unionCase,
                Token name, IReadOnlyList<PatternSyntax> arguments, string expected, List<string> introduced)
            {
                if (union.Name.Text == resultSymbol) resultUsed = true;
                if (union.Name.Text == optionSymbol) optionUsed = true;
                var substitutions = SubstitutionsFor(expected, union.Name.Text, union.TypeParameters);
                if (substitutions is null)
                {
                    var expectedName = DisplayType(expected);
                    var unionName = DisplayType(union.Name.Text);
                    if (expected != union.Name.Text && expectedName == unionName)
                    {
                        if (union.Name.Text == resultSymbol) unionName = "standard Result";
                        else if (union.Name.Text == optionSymbol) unionName = "standard Option";
                        else if (Nominal(expected) == resultSymbol) expectedName = "standard Result";
                        else if (Nominal(expected) == optionSymbol) expectedName = "standard Option";
                        else expectedName = DisplayType(expected, true);
                    }
                    Fail("DS2019", name, $"Case belongs to {unionName}, not {expectedName}.");
                }
                if (unionCase.PayloadTypes.Count != arguments.Count) Fail("DS2019", name, "Incorrect number of case patterns.");
                var argumentsBound = new List<BoundPattern>();
                for (var i = 0; i < arguments.Count; i++)
                    argumentsBound.Add(BindPattern(arguments[i], Substitute(unionCase.PayloadTypes[i].Text, substitutions!, unionCase.PayloadTypes[i]), introduced));
                return new BoundCasePattern(expected, unionCase.Name.Text, argumentsBound);
            }
            (BoundExpression Expression, string Type) BindExpression(ExpressionSyntax expression, string? expectedResult = null)
            {
                switch (expression)
                {
                    case UnitSyntax: return (new BoundUnit(), "Unit");
                    case NullSyntax: return (new BoundNull("<null>"), "<null>");
                    case LambdaSyntax lambda:
                        return BindLambda(lambda, expectedResult);
                    case InvokeSyntax invocation:
                        return BindInvocation(invocation.Receiver, invocation.OpenParen, invocation.Arguments);
                    case PropagateSyntax propagation:
                        if (currentReturnType is null || !GenericTypes.TryParse(currentReturnType, out var outerResult) ||
                            outerResult.Name != resultSymbol || outerResult.IsNullable || outerResult.Arguments.Count != 2)
                            Fail("DS2025", propagation.Operator, "Result propagation requires an enclosing function returning Result.");
                        // Preserve the type of an already-bound Result. Supply a
                        // context only if ordinary inference cannot determine it;
                        // otherwise a surrounding nullable expectation could
                        // change an existing Result<int,E> into Result<int?,E>.
                        (BoundExpression Expression, string Type) propagated;
                        try { propagated = BindExpression(propagation.Operand); }
                        catch (SourceError error) when (error.Diagnostic.Code is "DS2023" or "DS2018")
                        {
                            var successHint = expectedResult is not null && SupportedType(expectedResult, activeTypeParameters)
                                ? expectedResult : null;
                            var sourceOperand = propagation.Operand;
                            while (sourceOperand is GroupSyntax grouped) sourceOperand = grouped.Inner;
                            IReadOnlyList<ExpressionSyntax>? successArguments = null;
                            if (sourceOperand is CallSyntax call && call.Name.Text == "Success" &&
                                !locals.ContainsKey(call.Name.Text) && !signatures.ContainsKey(call.Name.Text) &&
                                !records.ContainsKey(call.Name.Text) && cases.TryGetValue(call.Name.Text, out var candidates) &&
                                candidates.Count == 1 && candidates[0].Union.Name.Text == resultSymbol)
                                successArguments = call.Arguments;
                            else if (sourceOperand is MemberCallSyntax member && member.Receiver is ValueSyntax resultQualifier &&
                                resultQualifier.Value.Text == "Result" && member.Member.Text == "Success" && !userResultType &&
                                !locals.ContainsKey(resultQualifier.Value.Text) && !functionTypeScope.ContainsKey(resultQualifier.Value.Text))
                                successArguments = member.Arguments;
                            if (successHint is null && successArguments is { Count: 0 }) successHint = "Unit";
                            if (successHint is null && successArguments is { Count: 1 })
                            {
                                var payload = BindExpression(successArguments[0]);
                                if (SupportedType(payload.Type, activeTypeParameters)) successHint = payload.Type;
                            }
                            if (successHint is null) throw;
                            var operandHint = resultSymbol + "<" + successHint + "," + outerResult!.Arguments[1] + ">";
                            propagated = BindExpression(propagation.Operand, operandHint);
                        }
                        // An if/match with no completing branch has no operand
                        // value to inspect. It is bottom-typed, so it may inhabit
                        // the required Result context. Keep evaluating it, so its
                        // enclosing-function return still occurs. A typed call
                        // whose argument does not complete retains its declared
                        // type and must still be checked below.
                        if (propagated.Type == "<never>" &&
                            (propagated.Expression is BoundIf { Then.CanComplete: false, Else.CanComplete: false } ||
                             propagated.Expression is BoundMatch { HasCompletingArm: false }))
                            return (propagated.Expression, "<never>");
                        var propagatedType = propagated.Type == "<never>" ? KnownType(propagated.Expression) : propagated.Type;
                        if (propagatedType is null || !GenericTypes.TryParse(propagatedType, out var operandShape) ||
                            operandShape.Name != resultSymbol || operandShape.IsNullable || operandShape.Arguments.Count != 2)
                            Fail("DS2025", propagation.Operator, "Postfix ? requires a non-nullable Result value.");
                        if (operandShape!.Arguments[1].ToString() != outerResult!.Arguments[1].ToString())
                            Fail("DS2025", propagation.Operator, "The propagated error type must match the enclosing Result error type.");
                        hasReturn = true;
                        resultUsed = true;
                        var successType = operandShape.Arguments[0].ToString();
                        return (new BoundPropagation(propagated.Expression, propagatedType!, successType, currentReturnType!),
                            propagated.Type == "<never>" ? "<never>" : successType);
                    case MatchSyntax match:
                        var scrutinee = BindExpression(match.Scrutinee);
                        var scrutineeType = scrutinee.Type == "<never>" ? KnownType(scrutinee.Expression) : scrutinee.Type;
                        if (scrutineeType is null || !SupportedType(scrutineeType, activeTypeParameters))
                            Fail("DS2019", match.Scrutinee.Token, "Cannot determine the type being matched.");
                        var boundArms = new List<(BoundPattern Pattern, BoundExpression? Guard, BoundExpression Value, string Type, bool GuardNever)>();
                        string? matchResultType = null;
                        foreach (var arm in match.Arms)
                        {
                            var introduced = new List<string>();
                            try
                            {
                                var pattern = BindPattern(arm.Pattern, scrutineeType!, introduced);
                                BoundExpression? guard = null;
                                var guardNever = false;
                                if (arm.Guard is not null)
                                {
                                    var boundGuard = BindExpression(arm.Guard);
                                    Require(arm.Guard, boundGuard.Type, "bool");
                                    guard = Coerce(boundGuard.Expression, "bool", boundGuard.Type);
                                    guardNever = boundGuard.Type == "<never>";
                                }
                                var value = BindExpression(arm.Value, expectedResult);
                                if (!guardNever && value.Type != "<never>")
                                {
                                    if (matchResultType is null) matchResultType = value.Type;
                                    else
                                    {
                                        var common = CommonType(matchResultType, value.Type);
                                        if (common is null) Fail("DS2002", arm.Value.Token, "Match arms must have compatible types.");
                                        if (common is not null && IsNullable(common) && !SupportedType(common, activeTypeParameters))
                                            Fail("DS2023", arm.Value.Token, "Nullable unconstrained type parameters are not supported.");
                                        matchResultType = common;
                                    }
                                }
                                boundArms.Add((pattern, guard, value.Expression, value.Type, guardNever));
                            }
                            finally
                            {
                                foreach (var name in introduced) locals.Remove(name);
                            }
                        }
                        var coverageUnions = syntax.Unions.Select(union => new BoundUnionDeclaration(union.Name.Text,
                            union.Cases.Select(unionCase => new BoundUnionCase(unionCase.Name.Text,
                                unionCase.PayloadTypes.Select(type => type.Text).ToArray())).ToArray())
                            { TypeParameters = union.TypeParameters.Select(parameter => parameter.Text).ToArray() }).ToArray();
                        if (!MatchCoverage.IsExhaustive(scrutineeType!,
                            boundArms.Where(arm => arm.Guard is null).Select(arm => arm.Pattern).ToArray(), coverageUnions))
                            Fail("DS2020", match.Match, "Match patterns are not exhaustive.");
                        var emittedMatchType = matchResultType ?? "Unit";
                        var arms = boundArms.Select(arm => new BoundMatchArm(arm.Pattern, arm.Guard,
                            arm.GuardNever ? arm.Value : emittedMatchType == "<null>" && arm.Type == "<never>"
                                ? new BoundNever(arm.Value, "<null>") : Coerce(arm.Value, emittedMatchType, arm.Type), arm.GuardNever)).ToArray();
                        return (new BoundMatch(Coerce(scrutinee.Expression, scrutineeType!, scrutinee.Type), scrutineeType!, arms, emittedMatchType)
                            { HasCompletingArm = boundArms.Any(arm => !arm.GuardNever && arm.Type != "<never>") },
                            scrutinee.Type == "<never>" || boundArms.All(arm => arm.GuardNever || arm.Type == "<never>") ? "<never>" : emittedMatchType);
                    case IfSyntax conditional:
                        var condition = BindExpression(conditional.Condition);
                        Require(conditional.Condition, condition.Type, "bool");
                        var thenBlock = BindBlock(conditional.Then, expectedResultType: expectedResult);
                        var elseBlock = BindBlock(conditional.Else, expectedResultType: expectedResult);
                        if (!conditional.HasElse && thenBlock.Type is not ("Unit" or "<never>"))
                            Fail("DS1002", conditional.MissingElseLocation ?? conditional.If, "An if without else requires a Unit branch.");
                        var branchType = CommonType(thenBlock.Type, elseBlock.Type);
                        if (branchType is null)
                        {
                            var location = conditional.Then.Result is UnitSyntax ? conditional.Then.Result : conditional.Else.Result;
                            Fail("DS2002", location.Token, "Conditional branches must have the same type.");
                        }
                        if (branchType is not null && IsNullable(branchType) && !SupportedType(branchType, activeTypeParameters))
                            Fail("DS2023", conditional.If, "Nullable unconstrained type parameters are not supported.");
                        var emittedType = branchType == "<never>" ? "Unit" : branchType;
                        if (condition.Type == "<never>") branchType = "<never>";
                        return (new BoundIf(Coerce(condition.Expression, "bool", condition.Type),
                            CoerceBlock(thenBlock.Block, emittedType!), CoerceBlock(elseBlock.Block, emittedType!), emittedType!), branchType!);
                    case GroupSyntax group: return BindExpression(group.Inner, expectedResult);
                    case CoalesceSyntax coalesce:
                        var nullableLeft = BindExpression(coalesce.Left);
                        var fallback = BindExpression(coalesce.Right);
                        var leftType = nullableLeft.Type == "<never>" ? KnownType(nullableLeft.Expression) : nullableLeft.Type;
                        if (nullableLeft.Type == "<never>" && (leftType is null || !IsNullable(leftType))) leftType = "<null>";
                        if (leftType != "<null>" && (leftType is null || !IsNullable(leftType)))
                            Fail("DS2022", coalesce.Left.Token, "The left operand of ?? must be nullable.");
                        if (leftType == "<null>" && fallback.Type == "<null>" && nullableLeft.Type != "<never>")
                            return (new BoundCoalesce(nullableLeft.Expression, fallback.Expression, "<null>", "<null>"), "<null>");
                        string containedType;
                        string resultType;
                        if (leftType == "<null>")
                        {
                            var inferred = fallback.Type == "<never>" ? KnownType(fallback.Expression) : fallback.Type;
                            if (inferred is null || inferred == "<null>" || inferred == "<never>" || !SupportedType(inferred, activeTypeParameters)) inferred = "Unit";
                            containedType = IsNullable(inferred) ? inferred[..^1] : inferred;
                            resultType = IsNullable(inferred) || fallback.Type == "<null>" ? containedType + "?" : containedType;
                            leftType = containedType + "?";
                        }
                        else
                        {
                            containedType = leftType![..^1];
                            resultType = fallback.Type == "<null>" || fallback.Type == leftType ? leftType! : containedType;
                        }
                        if (!SupportedType(leftType!, activeTypeParameters))
                            Fail("DS2023", coalesce.Left.Token, "Nullable unconstrained type parameters are not supported.");
                        Require(coalesce.Right, fallback.Type, resultType);
                        return (new BoundCoalesce(Coerce(nullableLeft.Expression, leftType!, nullableLeft.Type),
                            Coerce(fallback.Expression, resultType, fallback.Type), containedType, resultType),
                            nullableLeft.Type == "<never>" || nullableLeft.Type == "<null>" && fallback.Type == "<never>" ? "<never>" : resultType);
                    case RecordConstructionSyntax construction:
                        if (inaccessibleCaptures.Contains(construction.Name.Text)) Fail("DS2026", construction.Name, "Lambda capture is not supported.");
                        if (locals.ContainsKey(construction.Name.Text)) Fail("DS2016", construction.Name, "A local value is not a record constructor.");
                        if (functionTypeScope.ContainsKey(construction.Name.Text))
                            Fail("DS2023", construction.Name, "An unconstrained type parameter is not a record constructor.");
                        if (records.ContainsKey(construction.Name.Text) && cases.ContainsKey(construction.Name.Text))
                            Fail("DS2018", construction.Name, "Ambiguous record constructor and union case.");
                        if (!records.TryGetValue(construction.Name.Text, out var record)) Fail("DS2016", construction.Name, "Unknown record type.");
                        var constructed = BindRecordFields(record!, construction.Fields, construction.Name, true, expectedResult);
                        return (new BoundRecordConstruction(constructed.Type, constructed.Fields), constructed.Never ? "<never>" : constructed.Type);
                    case ConditionalFieldAccessSyntax optionalAccess:
                        var optionalReceiver = BindExpression(optionalAccess.Receiver);
                        var optionalType = optionalReceiver.Type == "<never>" ? KnownType(optionalReceiver.Expression) : optionalReceiver.Type;
                        var recordType = optionalType is not null && IsNullable(optionalType) ? optionalType[..^1] : optionalType;
                        if (!records.TryGetValue(Nominal(recordType ?? ""), out var optionalRecord))
                            Fail("DS2022", optionalAccess.Field, "Conditional field access requires a record value.");
                        var optionalField = optionalRecord!.Fields.FirstOrDefault(field => field.Name.Text == optionalAccess.Field.Text);
                        if (optionalField is null) Fail("DS2022", optionalAccess.Field, "Unknown record field.");
                        var optionalSubstitutions = SubstitutionsFor(recordType, optionalRecord.Name.Text, optionalRecord.TypeParameters);
                        if (optionalSubstitutions is null) Fail("DS2023", optionalAccess.Field, "Invalid generic record type.");
                        var optionalFieldType = Substitute(optionalField!.Type.Text, optionalSubstitutions!, optionalField.Type);
                        var optionalResultType = IsNullable(optionalFieldType) ? optionalFieldType : optionalFieldType + "?";
                        if (!SupportedType(optionalResultType, activeTypeParameters))
                            Fail("DS2023", optionalAccess.Field, "Nullable unconstrained type parameters are not supported.");
                        return (new BoundConditionalFieldAccess(Coerce(optionalReceiver.Expression, recordType! + "?", optionalReceiver.Type),
                            recordType!, optionalAccess.Field.Text, optionalResultType) { FieldType = optionalFieldType }, optionalReceiver.Type == "<never>" ? "<never>" : optionalResultType);
                    case FieldAccessSyntax access:
                        if (access.Receiver is ValueSyntax capturedFieldQualifier && inaccessibleCaptures.Contains(capturedFieldQualifier.Value.Text))
                            Fail("DS2026", capturedFieldQualifier.Value, "Lambda capture is not supported.");
                        if (access.Receiver is ValueSyntax typeQualifier && !locals.ContainsKey(typeQualifier.Value.Text) &&
                            functionTypeScope.ContainsKey(typeQualifier.Value.Text) && unions.ContainsKey(UnionQualifier(typeQualifier.Value.Text)))
                            Fail("DS2018", typeQualifier.Value, "An unconstrained type parameter cannot qualify a union case.");
                        if (access.Receiver is ValueSyntax qualifier && !locals.ContainsKey(qualifier.Value.Text) &&
                            unions.TryGetValue(UnionQualifier(qualifier.Value.Text), out var qualifiedUnion))
                            return BindQualifiedCase(qualifiedUnion, access.Field, Array.Empty<ExpressionSyntax>(), expectedResult: expectedResult);
                        var receiver = BindExpression(access.Receiver);
                        var receiverType = receiver.Type == "<never>" ? KnownType(receiver.Expression) : receiver.Type;
                        if (receiverType is not null && IsNullable(receiverType))
                            Fail("DS2016", access.Field, "Nullable records require conditional access or matching.");
                        if (!records.TryGetValue(Nominal(receiverType ?? ""), out var accessedRecord))
                            Fail("DS2016", access.Field, "Field access requires a record value.");
                        var accessedField = accessedRecord!.Fields.FirstOrDefault(field => field.Name.Text == access.Field.Text);
                        if (accessedField is null) Fail("DS2016", access.Field, "Unknown record field.");
                        var fieldSubstitutions = SubstitutionsFor(receiverType, accessedRecord.Name.Text, accessedRecord.TypeParameters);
                        if (fieldSubstitutions is null) Fail("DS2023", access.Field, "Invalid generic record type.");
                        var accessedFieldType = Substitute(accessedField!.Type.Text, fieldSubstitutions!, accessedField.Type);
                        return (new BoundFieldAccess(Coerce(receiver.Expression, receiverType!, receiver.Type), receiverType!, access.Field.Text),
                            receiver.Type == "<never>" ? "<never>" : accessedFieldType);
                    case RecordUpdateSyntax update:
                        var original = BindExpression(update.Receiver);
                        var originalType = original.Type == "<never>" ? KnownType(original.Expression) : original.Type;
                        if (originalType is not null && IsNullable(originalType))
                            Fail("DS2016", update.With, "A nullable record cannot be copied without matching.");
                        if (!records.TryGetValue(Nominal(originalType ?? ""), out var updatedRecord))
                            Fail("DS2016", update.With, "A copy update requires a record value.");
                        var changes = BindRecordFields(updatedRecord!, update.Fields, update.With, false, originalType);
                        return (new BoundRecordUpdate(Coerce(original.Expression, originalType!, original.Type), originalType!, changes.Fields),
                            original.Type == "<never>" || changes.Never ? "<never>" : originalType!);
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
                        if (!locals.ContainsKey(token.Text) && inaccessibleCaptures.Contains(token.Text))
                            Fail("DS2026", token, "Lambda capture is not supported.");
                        if (!locals.TryGetValue(token.Text, out var local) && cases.ContainsKey(token.Text))
                        {
                            var selected = BareCase(token);
                            return BindCase(selected.Union, selected.Case, token, Array.Empty<ExpressionSyntax>(), expectedResult: expectedResult);
                        }
                        if (!locals.TryGetValue(token.Text, out local))
                            Fail("DS2007", token, "Unknown local; bindings are visible only after their initializer and within their function.");
                        return (new BoundReference(token.Text), local.Type);
                    case CallSyntax call:
                        if (!locals.ContainsKey(call.Name.Text) && inaccessibleCaptures.Contains(call.Name.Text))
                            Fail("DS2026", call.Name, "Lambda capture is not supported.");
                        if (locals.ContainsKey(call.Name.Text))
                        {
                            if (call.TypeArguments.Count > 0) Fail("DS2026", call.Name, "Function values do not accept explicit type arguments.");
                            return BindInvocation(new ValueSyntax(call.Name), call.OpenParen, call.Arguments);
                        }
                        if (cases.ContainsKey(call.Name.Text))
                        {
                            var selected = BareCase(call.Name);
                            return BindCase(selected.Union, selected.Case, call.Name, call.Arguments,
                                call.TypeArguments.Select(type => ScopedType(type, functionTypeScope)).ToArray(), expectedResult, true);
                        }
                        if (!signatures.TryGetValue(call.Name.Text, out var target)) Fail("DS2009", call.Name, "Unknown function.");
                        var resolvedCall = BindGenericArguments(target!.TypeParameters,
                            call.TypeArguments.Select(type => ScopedType(type, functionTypeScope)).ToArray(),
                            target.Parameters.Select(parameter => parameter.Type).ToArray(), call.Arguments, call.Name,
                            target.ReturnType.Text, expectedResult);
                        var callResultType = Substitute(target.ReturnType.Text, resolvedCall.Substitutions, target.ReturnType);
                        if (!SupportedType(callResultType, activeTypeParameters)) Fail("DS2026", call.Name, "Function values cannot be stored in data types.");
                        return (new BoundCall(call.Name.Text, resolvedCall.Values)
                        {
                            TypeArguments = target.TypeParameters.Select(parameter => resolvedCall.Substitutions[parameter.Text]).ToArray(),
                            Location = call.Name
                        }, resolvedCall.Never ? "<never>" : callResultType);
                    case MemberCallSyntax member:
                        if (member.Receiver is ValueSyntax capturedMemberQualifier && inaccessibleCaptures.Contains(capturedMemberQualifier.Value.Text))
                            Fail("DS2026", capturedMemberQualifier.Value, "Lambda capture is not supported.");
                        if (member.Receiver is ValueSyntax typeMemberQualifier && !locals.ContainsKey(typeMemberQualifier.Value.Text) &&
                            functionTypeScope.ContainsKey(typeMemberQualifier.Value.Text) && unions.ContainsKey(UnionQualifier(typeMemberQualifier.Value.Text)))
                            Fail("DS2018", typeMemberQualifier.Value, "An unconstrained type parameter cannot qualify a union case.");
                        if (member.Receiver is ValueSyntax memberQualifier && !locals.ContainsKey(memberQualifier.Value.Text) &&
                            unions.TryGetValue(UnionQualifier(memberQualifier.Value.Text), out var memberUnion))
                            return BindQualifiedCase(memberUnion, member.Member, member.Arguments,
                                member.TypeArguments.Select(type => ScopedType(type, functionTypeScope)).ToArray(), expectedResult, true);
                        BindExpression(member.Receiver);
                        Fail("DS2018", member.Member, "Only qualified union cases are callable through a member.");
                        throw new InvalidOperationException("Unreachable after source error.");
                    case UnarySyntax unary:
                        var operand = BindExpression(unary.Operand);
                        var expected = unary.Operator.Text == "!" ? "bool" : "int";
                        Require(unary.Operand, operand.Type, expected);
                        return (new BoundUnary(unary.Operator.Text, Coerce(operand.Expression, expected, operand.Type)), operand.Type == "<never>" ? "<never>" : expected);
                    case BinarySyntax binary:
                        var left = BindExpression(binary.Left);
                        var right = BindExpression(binary.Right);
                        var op = binary.Operator.Text;
                        if (op is "&&" or "||")
                        {
                            Require(binary.Left, left.Type, "bool"); Require(binary.Right, right.Type, "bool");
                            return (new BoundBinary(Coerce(left.Expression, "bool", left.Type), op, Coerce(right.Expression, "bool", right.Type)), left.Type == "<never>" ? "<never>" : "bool");
                        }
                        if (op is "==" or "!=")
                        {
                            // An unconstrained T is not itself known to admit null.
                            // Keep the existing restriction on comparing a bare T
                            // directly to null; an explicit T? has a valid nullable
                            // representation and may be compared normally.
                            if (left.Type == "<null>" && activeTypeParameters.Contains(right.Type) ||
                                right.Type == "<null>" && activeTypeParameters.Contains(left.Type))
                                Fail("DS2023", binary.Operator, "Equality between null and an unconstrained type parameter is not supported.");
                            var operandType = CommonType(left.Type, right.Type);
                            if (operandType == "<never>") operandType = "int";
                            if (operandType == "<null>") Fail("DS2021", binary.Left.Token, "Cannot infer a type for null equality.");
                            if (operandType is null) Fail("DS2002", binary.Right.Token, "Equality operands have incompatible types.");
                            if (operandType is not null && GenericTypes.TryParse(operandType, out var equalityShape) && equalityShape.IsFunction &&
                                !(equalityShape.IsNullable && (left.Type == "<null>" || right.Type == "<null>")))
                                Fail("DS2026", binary.Operator, "Function value equality is not supported; nullable functions may be compared with null.");
                            if (operandType is not null && IsNullable(operandType) && !SupportedType(operandType, activeTypeParameters))
                                Fail("DS2023", binary.Operator, "Nullable unconstrained type parameters are not supported.");
                            if (operandType == "Unit") Fail("DS2002", binary.Operator, "Unit equality is not supported.");
                            if (operandType is not null && activeTypeParameters.Contains(operandType))
                                Fail("DS2023", binary.Operator, "Equality on an unconstrained type parameter is not supported.");
                            return (new BoundBinary(Coerce(left.Expression, operandType!, left.Type), op, Coerce(right.Expression, operandType!, right.Type)) { OperandType = operandType! }, left.Type == "<never>" || right.Type == "<never>" ? "<never>" : "bool");
                        }
                        Require(binary.Left, left.Type, "int"); Require(binary.Right, right.Type, "int");
                        return (new BoundBinary(Coerce(left.Expression, "int", left.Type), op, Coerce(right.Expression, "int", right.Type)), left.Type == "<never>" || right.Type == "<never>" ? "<never>" : op is "<" or "<=" or ">" or ">=" ? "bool" : "int");
                    default: throw new InvalidOperationException("Unknown expression syntax.");
                }
            }
            // Preserve bootstrap values for literal/local-only clients. Arithmetic
            // has no compile-time value: evaluating it here would move faults out
            // of execution. The bound expression is authoritative for emission.
            int LegacyValue(BoundExpression expression) => expression switch
            {
                BoundInt literal => literal.Value,
                BoundReference reference => locals.TryGetValue(reference.Name, out var value) ? value.Value : 0,
                _ => 0
            };
            BoundExpression Coerce(BoundExpression expression, string expected, string actual = "")
            {
                // Delay resolving an all-null conditional/match until its context
                // or another branch supplies the contained type.
                if (expected == "<null>") return expression;
                if (actual == "<never>")
                {
                    if (expression is BoundIf { Type: "<null>" } or BoundMatch { Type: "<null>" } or BoundCoalesce { Type: "<null>" } or BoundNever)
                        expression = Coerce(expression, expected);
                    return new BoundNever(expression, expected);
                }
                if (expression is BoundNever never)
                {
                    var inner = never.Expression;
                    if (inner is BoundIf { Type: "<null>" } or BoundMatch { Type: "<null>" } or BoundCoalesce { Type: "<null>" } or BoundNever)
                        inner = Coerce(inner, expected);
                    return never with { Expression = inner, Type = expected };
                }
                if (expression is BoundNull) return new BoundNull(expected);
                if (expression is BoundCoalesce { Type: "<null>" } pending)
                {
                    var contained = IsNullable(expected) ? expected[..^1] : expected;
                    return pending with { Left = Coerce(pending.Left, contained + "?"), Right = Coerce(pending.Right, expected),
                        ContainedType = contained, Type = expected };
                }
                if (expression is BoundIf conditional)
                    return conditional with { Type = expected, Then = CoerceBlock(conditional.Then, expected), Else = CoerceBlock(conditional.Else, expected) };
                if (expression is BoundMatch match)
                    return match with { Type = expected, Arms = match.Arms.Select(arm => arm.GuardNever ? arm :
                        arm with { Value = Coerce(arm.Value, expected, match.Type) }).ToArray() };
                if (actual.Length > 0 && actual != expected && IsNullable(expected) && expected[..^1] == actual &&
                    activeTypeParameters.Contains(actual))
                    return new BoundNullableLift(expression, expected);
                return expression;
            }
            BoundBlock CoerceBlock(BoundBlock block, string expected) => block.EvaluateResult
                ? block with { Result = Coerce(block.Result, expected, block.ResultType), ResultType = expected }
                : block;
            (BoundBlock Block, string Type) BindBlock(BlockSyntax block, bool keepLocals = false, string? expectedResultType = null)
            {
                var introduced = new List<string>(); var boundStatements = new List<BoundStatement>();
                var boundLocalsInBlock = new List<BoundLocal>(); bool reachable = true;
                try
                {
                    var statements = block.Statements.Count > 0 ? block.Statements : block.Bindings.Select(b => (StatementSyntax)new LetStatementSyntax(b)).ToArray();
                    foreach (var statement in statements)
                    {
                        switch (statement)
                        {
                            case LetStatementSyntax declaration:
                                var binding = declaration.Binding;
                                if (locals.ContainsKey(binding.Name.Text) || inaccessibleCaptures.Contains(binding.Name.Text)) Fail("DS2006", binding.Name, "Duplicate or shadowed local binding.");
                                var annotation = binding.Annotation is null ? null : ScopedType(binding.Annotation, functionTypeScope);
                                if (annotation is not null && !SupportedType(annotation.Text, activeTypeParameters)) Fail("DS2008", annotation, "Unsupported local annotation.");
                                var initSyntax = binding.Expression ?? new ValueSyntax(binding.Initializer);
                                var init = BindExpression(initSyntax, annotation?.Text);
                                if (annotation is null && init.Type == "<null>")
                                    Fail("DS2002", initSyntax.Token, "Cannot infer the contained type of null.");
                                // A record expression retains its nominal type even when an
                                // initializer cannot complete. Later unreachable statements
                                // still need that type to be checked consistently.
                                var localType = annotation?.Text ?? (init.Type == "<never>" ? KnownType(init.Expression) ?? "Unit" : init.Type);
                                if (annotation is { } annotationType && init.Type != "<never>" && init.Type != localType)
                                {
                                    if ((localType is "string" or "char") && init.Type != "<null>" && !IsNullable(init.Type))
                                        Fail("DS2008", annotationType, "Local annotation does not match initializer.");
                                    Require(initSyntax, init.Type, localType);
                                }
                                var boundInit = Coerce(init.Expression, localType, init.Type);
                                var legacy = LegacyValue(boundInit);
                                locals.Add(binding.Name.Text, (legacy, localType)); introduced.Add(binding.Name.Text);
                                var local = new BoundLocal(binding.Name.Text, legacy, (boundInit as BoundReference)?.Name) { Expression = boundInit, Type = localType };
                                boundLocalsInBlock.Add(local); boundStatements.Add(new BoundLetStatement(local));
                                if (reachable && init.Type == "<never>") reachable = false;
                                break;
                            case ReturnStatementSyntax ret:
                                var returned = BindExpression(ret.Value, currentReturnType);
                                if (currentReturnType is null)
                                {
                                    if (returned.Type != "<never>") inferredReturns!.Add(returned.Type);
                                    boundStatements.Add(new BoundReturnStatement(returned.Expression));
                                }
                                else
                                {
                                    Require(ret.Value, returned.Type, currentReturnType);
                                    boundStatements.Add(new BoundReturnStatement(Coerce(returned.Expression, currentReturnType, returned.Type)));
                                }
                                hasReturn = true; reachable = false; break;
                            case ExpressionStatementSyntax exprStatement:
                                var expr = BindExpression(exprStatement.Expression);
                                if (GenericTypes.TryParse(expr.Type, out var discardedType) && discardedType.Name == resultSymbol)
                                    Fail("DS2024", exprStatement.Expression.Token, "A Result value cannot be discarded.");
                                Require(exprStatement.Expression, expr.Type, "Unit");
                                boundStatements.Add(new BoundExpressionStatement(Coerce(expr.Expression, "Unit", expr.Type)));
                                if (reachable && expr.Type == "<never>") reachable = false;
                                break;
                        }
                    }
                    var final = BindExpression(block.Result, expectedResultType);
                    var type = reachable ? final.Type : "<never>";
                    return (new BoundBlock(boundLocalsInBlock, final.Expression) { Statements = boundStatements, CanComplete = type != "<never>", EvaluateResult = reachable, ResultType = final.Type }, type);
                }
                finally { if (!keepLocals) foreach (var name in introduced) locals.Remove(name); }
            }
            (BoundExpression Expression, string Type) BindInvocation(ExpressionSyntax receiverSyntax, Token location, IReadOnlyList<ExpressionSyntax> arguments)
            {
                var receiver = BindExpression(receiverSyntax);
                var type = receiver.Type == "<never>" ? KnownType(receiver.Expression) : receiver.Type;
                if (type is null || !GenericTypes.TryParse(type, out var shape) || !shape.IsFunction)
                    Fail("DS1002", location, "Only function values are callable.");
                if (shape!.IsNullable)
                    Fail("DS2026", location, "Invocation requires a non-nullable function value.");
                var functionShape = shape!;
                var formal = functionShape.Arguments.Take(functionShape.Arguments.Count - 1)
                    .Select(argument => new Token(argument.ToString(), location.Span)).ToArray();
                var values = BindGenericArguments(Array.Empty<Token>(), Array.Empty<Token>(), formal, arguments, location);
                var resultType = functionShape.Arguments[^1].ToString();
                return (new BoundInvoke(receiver.Expression, values.Values, resultType),
                    receiver.Type == "<never>" || values.Never ? "<never>" : resultType);
            }
            (BoundExpression Expression, string Type) BindLambda(LambdaSyntax lambda, string? expectedType)
            {
                TypeShape? expected = null;
                if (expectedType is not null && GenericTypes.TryParse(expectedType, out var parsedExpected) && parsedExpected.IsFunction)
                    expected = parsedExpected;
                if (expected is not null && expected.Arguments.Count != lambda.Parameters.Count + 1)
                    Fail("DS2026", lambda.Start, "Lambda parameter count does not match the expected function type.");
                var savedLocals = locals;
                var savedCaptures = inaccessibleCaptures;
                var savedReturnType = currentReturnType;
                var savedReturns = inferredReturns;
                var savedHasReturn = hasReturn;
                var parameterTypes = new List<string>();
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var (parameter, index) in lambda.Parameters.Select((parameter, index) => (parameter, index)))
                {
                    if (!names.Add(parameter.Name.Text) || savedLocals.ContainsKey(parameter.Name.Text) || savedCaptures.Contains(parameter.Name.Text))
                        Fail("DS2006", parameter.Name, "Lambda parameters cannot duplicate or shadow a visible local.");
                    var expectedParameter = expected is null ? null : expected.Arguments[index].ToString();
                    var annotation = parameter.Type is null ? null : ScopedType(parameter.Type, functionTypeScope);
                    if (annotation is not null && !SupportedType(annotation.Text, activeTypeParameters))
                        Fail("DS2026", annotation, "Unsupported lambda parameter type.");
                    var usableExpected = expectedParameter is not null && SupportedType(expectedParameter, activeTypeParameters) ? expectedParameter : null;
                    if (annotation is not null && usableExpected is not null && annotation.Text != usableExpected)
                        Fail("DS2026", annotation, "Lambda parameter type does not match the expected type.");
                    var parameterType = annotation?.Text ?? usableExpected;
                    if (parameterType is null) Fail("DS2026", parameter.Name, "Cannot infer lambda parameter type.");
                    parameterTypes.Add(parameterType!);
                }
                var expectedReturn = expected?.Arguments[^1].ToString();
                if (expectedReturn is not null && !SupportedType(expectedReturn, activeTypeParameters)) expectedReturn = null;
                try
                {
                    inaccessibleCaptures = new HashSet<string>(savedCaptures, StringComparer.Ordinal);
                    inaccessibleCaptures.UnionWith(savedLocals.Keys);
                    (BoundBlock Block, string Type) BindBody(string? resultType, List<string>? returns)
                    {
                        locals = new Dictionary<string, (int Value, string Type)>(StringComparer.Ordinal);
                        for (var i = 0; i < lambda.Parameters.Count; i++) locals.Add(lambda.Parameters[i].Name.Text, (0, parameterTypes[i]));
                        currentReturnType = resultType;
                        inferredReturns = returns;
                        hasReturn = false;
                        return BindBlock(lambda.Body, true, resultType);
                    }
                    var returns = new List<string>();
                    var body = BindBody(expectedReturn, expectedReturn is null ? returns : null);
                    var returnType = expectedReturn;
                    if (returnType is null)
                    {
                        if (body.Type != "<never>") returnType = body.Type;
                        foreach (var returnedType in returns)
                            returnType = returnType is null ? returnedType : CommonType(returnType, returnedType) ?? "<invalid>";
                        if (returnType is null || returnType is "<null>" or "<never>" or "<invalid>" || !SupportedType(returnType, activeTypeParameters))
                            Fail("DS2026", lambda.Start, "Cannot infer a unique lambda return type.");
                        body = BindBody(returnType, null);
                    }
                    Require(lambda.Body.Result, body.Type, returnType!);
                    var boundBody = body.Block with { Result = Coerce(body.Block.Result, returnType!, body.Type) };
                    var shapes = parameterTypes.Append(returnType!).Select(type => { GenericTypes.TryParse(type, out var shape); return shape; }).ToArray();
                    var lambdaType = new TypeShape(TypeShape.FunctionName, shapes, false).ToString();
                    return (new BoundLambda(lambda.Parameters.Select(parameter => parameter.Name.Text).ToArray(), parameterTypes,
                        lambdaType, returnType!, boundBody, hasReturn), lambdaType);
                }
                finally
                {
                    locals = savedLocals;
                    inaccessibleCaptures = savedCaptures;
                    currentReturnType = savedReturnType;
                    inferredReturns = savedReturns;
                    hasReturn = savedHasReturn;
                }
            }
            var sourceBody = function.Body ?? new BlockSyntax(function.Bindings, function.Expression ?? new ValueSyntax(function.Literal));
            var body = BindBlock(sourceBody, true, function.ReturnType.Text);
            if (body.Type != "<never>" && body.Type != "<null>" && !IsNullable(body.Type) && body.Type != function.ReturnType.Text && (function.ReturnType.Text is "string" or "char"))
                Fail("DS2003", function.ReturnType, "Return type does not match the function body.");
            Require(sourceBody.Result, body.Type, function.ReturnType.Text);
            var resultExpression = Coerce(body.Block.Result, function.ReturnType.Text, body.Type);
            var result = (Expression: resultExpression, Type: body.Type);
            boundLocals.AddRange(body.Block.Locals);
            functions.Add(new(function.Name.Text, LegacyValue(result.Expression))
            {
                Locals = boundLocals.Count == 0 ? Array.Empty<BoundLocal>() : boundLocals,
                Body = body.Block.Statements.Count == 0 ? null : body.Block with { Result = resultExpression },
                HasReturn = hasReturn,
                ResultLocal = (result.Expression as BoundReference)?.Name,
                ReturnType = function.ReturnType.Text,
                ParameterTypes = function.Parameters.Select(p => p.Type.Text).ToArray(),
                Parameters = function.Parameters.Count == 0 ? Array.Empty<string>() : function.Parameters.Select(p => p.Name.Text).ToArray(),
                Expression = boundLocals.Count == 0 && result.Expression is BoundInt ? null : result.Expression,
                TypeParameters = function.TypeParameters.Select(parameter => parameter.Text).ToArray()
            });
        }
        // Generic parameters used in data positions must remain data-safe at
        // every call, even when the function returns Unit or forwards through
        // several generic helpers. Discover those requirements to a fixed point.
        var dataRequirements = functions.ToDictionary(item => item.Name,
            _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        var boundFunctions = functions.ToDictionary(item => item.Name, StringComparer.Ordinal);
        bool requirementsChanged;
        do
        {
            requirementsChanged = false;
            foreach (var owner in functions)
            {
                var ownerParameters = owner.TypeParameters.ToHashSet(StringComparer.Ordinal);
                var required = dataRequirements[owner.Name];
                void Need(TypeShape shape)
                {
                    if (shape.Arguments.Count == 0 && ownerParameters.Contains(shape.Name) && required.Add(shape.Name))
                        requirementsChanged = true;
                    foreach (var argument in shape.Arguments) Need(argument);
                }
                void Scan(TypeShape shape, bool inData = false)
                {
                    if (inData && shape.Arguments.Count == 0 && ownerParameters.Contains(shape.Name) && required.Add(shape.Name))
                        requirementsChanged = true;
                    var childData = inData || (!shape.IsFunction && shape.Arguments.Count > 0);
                    foreach (var argument in shape.Arguments) Scan(argument, childData);
                }
                void Visit(object? node)
                {
                    if (node is null) return;
                    if (node is string text)
                    {
                        if (GenericTypes.TryParse(text, out var shape)) Scan(shape);
                        return;
                    }
                    if (node is BoundBinary { Operator: "==" or "!=" } equality &&
                        equality.Left is not BoundNull && equality.Right is not BoundNull &&
                        GenericTypes.TryParse(equality.OperandType, out var equalityType) && equalityType.IsNullable &&
                        equalityType.Arguments.Count == 0 && ownerParameters.Contains(equalityType.Name) && required.Add(equalityType.Name))
                        requirementsChanged = true;
                    if (node is BoundCall call && boundFunctions.TryGetValue(call.Name, out var callee))
                    {
                        for (var i = 0; i < Math.Min(call.TypeArguments.Count, callee.TypeParameters.Count); i++)
                        {
                            if (!dataRequirements[callee.Name].Contains(callee.TypeParameters[i]) ||
                                !GenericTypes.TryParse(call.TypeArguments[i], out var argument)) continue;
                            if (argument.ContainsFunction)
                                Fail("DS2026", call.Location ?? new Token(call.Name, syntheticSpan), "Function type is not valid for this generic operation.");
                            Need(argument);
                        }
                    }
                    if (node is System.Collections.IEnumerable sequence)
                    {
                        foreach (var item in sequence) Visit(item);
                        return;
                    }
                    var nodeType = node.GetType();
                    if (!nodeType.Name.StartsWith("Bound", StringComparison.Ordinal)) return;
                    foreach (var property in nodeType.GetProperties())
                    {
                        if (property.GetIndexParameters().Length != 0) continue;
                        var value = property.GetValue(node);
                        if (value is string && !property.Name.EndsWith("Type", StringComparison.Ordinal) && property.Name != "Union") continue;
                        Visit(value);
                    }
                }
                Visit(owner);
            }
        } while (requirementsChanged);
        return new(syntax.Name.Text, functions)
        {
            Records = syntax.Records.Select(record => new BoundRecordDeclaration(record.Name.Text,
                record.Fields.Select(field => new BoundRecordField(field.Name.Text, field.Type.Text)).ToArray())
                { TypeParameters = record.TypeParameters.Select(parameter => parameter.Text).ToArray() }).ToArray(),
            Unions = syntax.Unions.Where(union => (union.Name.Text != resultSymbol || resultUsed) &&
                (union.Name.Text != optionSymbol || optionUsed)).Select(union => new BoundUnionDeclaration(union.Name.Text,
                union.Cases.Select(unionCase => new BoundUnionCase(unionCase.Name.Text,
                    unionCase.PayloadTypes.Select(type => type.Text).ToArray())).ToArray())
                { TypeParameters = union.TypeParameters.Select(parameter => parameter.Text).ToArray() }).ToArray()
        };
    }
}

public static class CSharpEmitter
{
    public static string Emit(BoundModule module)
    {
        // A generated namespace avoids a C# member/type name collision without restricting D# names.
        var output = new StringBuilder("// Generated by DSharp.\n#nullable disable\nnamespace DSharp.Generated;\n\n");
        var globalNames = new HashSet<string>(StringComparer.Ordinal) { module.Name };
        foreach (var function in module.Functions) globalNames.Add(function.Name);
        foreach (var record in module.Records)
        {
            globalNames.Add(record.Name);
            foreach (var field in record.Fields) globalNames.Add(field.Name);
        }
        foreach (var union in module.Unions)
        {
            globalNames.Add(union.Name);
            foreach (var unionCase in union.Cases) globalNames.Add(unionCase.Name);
        }
        string UniqueGlobal(string proposal)
        {
            while (!globalNames.Add(proposal)) proposal += "_";
            return proposal;
        }
        var delegateNames = new Dictionary<int, string>();
        string DelegateName(int arity)
        {
            if (!delegateNames.TryGetValue(arity, out var name))
            {
                name = UniqueGlobal("__DSharp_Function_" + arity.ToString(CultureInfo.InvariantCulture));
                delegateNames.Add(arity, name);
            }
            return name;
        }
        var recordTypes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var record in module.Records)
            recordTypes.Add(record.Name, UniqueGlobal("__DSharp_Record_" + recordTypes.Count.ToString(CultureInfo.InvariantCulture)));
        foreach (var union in module.Unions)
            recordTypes.Add(union.Name, UniqueGlobal("__DSharp_Union_" + recordTypes.Count.ToString(CultureInfo.InvariantCulture)));
        var structuralInterface = UniqueGlobal("__DSharp_Structural");
        var container = "__DSharp_Functions";
        while (!globalNames.Add(container)) container += "_";
        var unitType = "__DSharp_Unit";
        while (!globalNames.Add(unitType)) unitType += "_";
        var signalType = "__DSharp_ReturnSignal";
        while (signalType == container || module.Functions.Any(f => f.Name == signalType)) signalType += "_";
        var raiseMethod = "__DSharp_Raise";
        while (raiseMethod == container || raiseMethod == signalType || module.Functions.Any(f => f.Name == raiseMethod)) raiseMethod += "_";
        var memberNames = new HashSet<string>(module.Functions.Select(f => f.Name), StringComparer.Ordinal) { signalType, raiseMethod };
        var equalityDispatcher = "__DSharp_StructuralEquals";
        while (!memberNames.Add(equalityDispatcher)) equalityDispatcher += "_";
        // CLR's unconstrained T? does not lift value-type instantiations. Add an
        // associated CLR type parameter for each nullable projection actually
        // needed by a declaration, propagating requirements through its uses.
        var nominalProjections = module.Records.ToDictionary(x => x.Name, x => new bool[x.TypeParameters.Count], StringComparer.Ordinal);
        foreach (var union in module.Unions) nominalProjections.Add(union.Name, new bool[union.TypeParameters.Count]);
        var functionProjections = module.Functions.ToDictionary(x => x.Name, x => new bool[x.TypeParameters.Count], StringComparer.Ordinal);
        bool changed;
        do
        {
            changed = false;
            void Discover(object? node, IReadOnlyList<string> parameters, bool[] requirements)
            {
                if (node is null) return;
                void Need(TypeShape argument)
                {
                    if (argument.Arguments.Count != 0) return;
                    for (var i = 0; i < parameters.Count; i++)
                        if (parameters[i] == argument.Name && !requirements[i]) { requirements[i] = true; changed = true; }
                }
                void Scan(TypeShape shape)
                {
                    if (shape.IsNullable) Need(shape);
                    if (nominalProjections.TryGetValue(shape.Name, out var slots))
                        for (var i = 0; i < Math.Min(slots.Length, shape.Arguments.Count); i++)
                            if (slots[i]) Need(shape.Arguments[i]);
                    foreach (var argument in shape.Arguments) Scan(argument);
                }
                if (node is string text)
                {
                    if (GenericTypes.TryParse(text, out var type)) Scan(type);
                    return;
                }
                if (node is BoundCall call && functionProjections.TryGetValue(call.Name, out var calleeSlots))
                    for (var i = 0; i < Math.Min(calleeSlots.Length, call.TypeArguments.Count); i++)
                        if (calleeSlots[i] && GenericTypes.TryParse(call.TypeArguments[i], out var argument)) Need(argument);
                if (node is System.Collections.IEnumerable sequence)
                {
                    foreach (var item in sequence) Discover(item, parameters, requirements);
                    return;
                }
                var nodeType = node.GetType();
                if (!nodeType.Name.StartsWith("Bound", StringComparison.Ordinal)) return;
                foreach (var property in nodeType.GetProperties())
                {
                    if (property.GetIndexParameters().Length != 0) continue;
                    var value = property.GetValue(node);
                    // Names and string literals are not type references.
                    if (value is string && !property.Name.EndsWith("Type", StringComparison.Ordinal) && property.Name != "Union") continue;
                    Discover(value, parameters, requirements);
                }
            }
            foreach (var record in module.Records) Discover(record.Fields, record.TypeParameters, nominalProjections[record.Name]);
            foreach (var union in module.Unions) Discover(union.Cases, union.TypeParameters, nominalProjections[union.Name]);
            foreach (var function in module.Functions) Discover(function, function.TypeParameters, functionProjections[function.Name]);
        } while (changed);
        var parameterNames = new Dictionary<string, string>(StringComparer.Ordinal);
        var nullableParameterNames = new Dictionary<string, string>(StringComparer.Ordinal);
        bool[] activeProjections = Array.Empty<bool>();
        void SetTypeParameters(IReadOnlyList<string> parameters, bool[] projections)
        {
            parameterNames.Clear();
            nullableParameterNames.Clear();
            activeProjections = projections;
            for (var i = 0; i < parameters.Count; i++)
            {
                var alias = "__DSharp_TypeParameter_" + i.ToString(CultureInfo.InvariantCulture);
                while (globalNames.Contains(alias)) alias += "_";
                parameterNames.Add(parameters[i], alias);
                if (projections[i]) nullableParameterNames.Add(parameters[i], alias + "_Nullable");
            }
        }
        string TypeParameterList(IReadOnlyList<string> parameters) => parameters.Count == 0 ? "" :
            "<" + string.Join(", ", parameters.SelectMany((parameter, i) => activeProjections[i]
                ? new[] { parameterNames[parameter], nullableParameterNames[parameter] }
                : new[] { parameterNames[parameter] })) + ">";
        static string Nominal(string type) => GenericTypes.TryParse(type, out var shape) ? shape.Name : type;
        bool IsNominal(string type) => GenericTypes.TryParse(type, out var shape) &&
            !(shape.Arguments.Count == 0 && parameterNames.ContainsKey(shape.Name)) && recordTypes.ContainsKey(shape.Name);
        bool IsTypeParameter(string type) => GenericTypes.TryParse(type, out var shape) &&
            shape.Arguments.Count == 0 && parameterNames.ContainsKey(shape.Name);
        static bool IsValueType(string type) => type is "int" or "bool" or "char" or "Unit";
        string CType(string type)
        {
            if (!GenericTypes.TryParse(type, out var shape)) return type;
            string ConvertShape(TypeShape current)
            {
                if (current.Arguments.Count == 0 && current.IsNullable && nullableParameterNames.TryGetValue(current.Name, out var nullableParameter))
                    return nullableParameter;
                if (current.IsFunction)
                    return DelegateName(current.Arguments.Count - 1) + "<" + string.Join(", ", current.Arguments.Select(ConvertShape)) + ">";
                var name = current.Arguments.Count == 0 && parameterNames.TryGetValue(current.Name, out var parameter) ? parameter :
                    current.Name == "Unit" ? unitType : recordTypes.TryGetValue(current.Name, out var recordType) ? recordType : current.Name;
                if (current.Arguments.Count > 0)
                {
                    nominalProjections.TryGetValue(current.Name, out var slots);
                    var arguments = new List<string>();
                    for (var i = 0; i < current.Arguments.Count; i++)
                    {
                        var argument = current.Arguments[i];
                        arguments.Add(ConvertShape(argument));
                        if (slots is not null && slots[i]) arguments.Add(ConvertShape(argument with { IsNullable = true }));
                    }
                    name += "<" + string.Join(", ", arguments) + ">";
                }
                return current.IsNullable && IsValueType(current.Name) && current.Arguments.Count == 0
                    ? "global::System.Nullable<" + name + ">" : name;
            }
            return ConvertShape(shape);
        }
        string EqualValue(string type, string left, string right)
        {
            if (type.EndsWith("?", StringComparison.Ordinal))
            {
                var inner = type[..^1];
                if (IsTypeParameter(inner))
                    return module.Records.Count > 0 || module.Unions.Count > 0
                        ? container + "." + equalityDispatcher + "<" + CType(type) + ">(" + left + ", " + right + ")"
                        : "global::System.Collections.Generic.EqualityComparer<" + CType(type) + ">.Default.Equals(" + left + ", " + right + ")";
                if (IsValueType(inner))
                    return "global::System.Collections.Generic.EqualityComparer<" + CType(type) + ">.Default.Equals(" + left + ", " + right + ")";
                type = inner;
            }
            return type == "Unit" ? "true" :
                type == "string" ? "global::System.String.Equals(" + left + ", " + right + ", global::System.StringComparison.Ordinal)" :
                IsNominal(type) || IsTypeParameter(type) ? container + "." + equalityDispatcher + "<" + CType(type) + ">(" + left + ", " + right + ")" : left + " == " + right;
        }
        output.Append("public readonly struct ").Append(unitType).Append(" {}\n\n");
        if (module.Records.Count > 0 || module.Unions.Count > 0)
            output.Append("public interface ").Append(structuralInterface).Append(" { bool __dsharp_equals(object other); }\n\n");
        foreach (var record in module.Records)
        {
            SetTypeParameters(record.TypeParameters, nominalProjections[record.Name]);
            var recordName = recordTypes[record.Name];
            var recordType = recordName + TypeParameterList(record.TypeParameters);
            output.Append("public sealed class ").Append(recordType).Append(" : ").Append(structuralInterface).Append("\n{\n");
            foreach (var field in record.Fields)
            {
                // C# object members can also be ordinary D# field names.
                var hidesObjectMember = field.Name is "Equals" or "GetHashCode" or "ToString" or "GetType" or "MemberwiseClone";
                output.Append("    public ").Append(hidesObjectMember ? "new " : "").Append(CType(field.Type)).Append(" @").Append(field.Name).Append(" { get; }\n");
            }
            output.Append("    public ").Append(recordName).Append("(");
            for (var i = 0; i < record.Fields.Count; i++)
            {
                if (i != 0) output.Append(", ");
                output.Append(CType(record.Fields[i].Type)).Append(" __dsharp_field_").Append(i.ToString(CultureInfo.InvariantCulture));
            }
            output.Append(")\n    {\n");
            for (var i = 0; i < record.Fields.Count; i++)
                output.Append("        this.@").Append(record.Fields[i].Name).Append(" = __dsharp_field_").Append(i.ToString(CultureInfo.InvariantCulture)).Append(";\n");
            output.Append("    }\n    bool ").Append(structuralInterface).Append(".__dsharp_equals(object __other)\n    {\n")
                .Append("        if (__other is not ").Append(recordType).Append(" __that) return false;\n")
                .Append("        return true");
            foreach (var field in record.Fields)
                output.Append(" && ").Append(EqualValue(field.Type, "this.@" + field.Name, "__that.@" + field.Name));
            output.Append(";\n    }\n}\n\n");
        }
        foreach (var union in module.Unions)
        {
            SetTypeParameters(union.TypeParameters, nominalProjections[union.Name]);
            var unionName = recordTypes[union.Name];
            var type = unionName + TypeParameterList(union.TypeParameters);
            output.Append("public sealed class ").Append(type).Append(" : ").Append(structuralInterface).Append("\n{\n")
                .Append("    internal int __dsharp_tag { get; }\n")
                .Append("    private readonly object[] __dsharp_payload;\n")
                .Append("    private ").Append(unionName).Append("(int tag, object[] payload) { __dsharp_tag = tag; __dsharp_payload = payload; }\n");
            for (var caseIndex = 0; caseIndex < union.Cases.Count; caseIndex++)
            {
                var unionCase = union.Cases[caseIndex];
                var caseSuffix = caseIndex.ToString(CultureInfo.InvariantCulture);
                output.Append("    public static ").Append(type).Append(" __dsharp_case_").Append(caseSuffix).Append("(");
                for (var i = 0; i < unionCase.PayloadTypes.Count; i++)
                {
                    if (i != 0) output.Append(", ");
                    output.Append(CType(unionCase.PayloadTypes[i])).Append(" __value_").Append(i.ToString(CultureInfo.InvariantCulture));
                }
                output.Append(") => new ").Append(type).Append("(").Append(caseSuffix).Append(", new object[] { ");
                for (var i = 0; i < unionCase.PayloadTypes.Count; i++)
                {
                    if (i != 0) output.Append(", ");
                    output.Append("__value_").Append(i.ToString(CultureInfo.InvariantCulture));
                }
                output.Append(" });\n");
                for (var i = 0; i < unionCase.PayloadTypes.Count; i++)
                {
                    var index = i.ToString(CultureInfo.InvariantCulture);
                    output.Append("    internal ").Append(CType(unionCase.PayloadTypes[i])).Append(" __dsharp_case_").Append(caseSuffix)
                        .Append("_item_").Append(index).Append(" => (").Append(CType(unionCase.PayloadTypes[i])).Append(")__dsharp_payload[").Append(index).Append("];\n");
                }
            }
            output.Append("    bool ").Append(structuralInterface).Append(".__dsharp_equals(object __other)\n    {\n")
                .Append("        if (__other is not ").Append(type).Append(" __that || __dsharp_tag != __that.__dsharp_tag) return false;\n")
                .Append("        switch (__dsharp_tag)\n        {\n");
            for (var caseIndex = 0; caseIndex < union.Cases.Count; caseIndex++)
            {
                var unionCase = union.Cases[caseIndex];
                var caseSuffix = caseIndex.ToString(CultureInfo.InvariantCulture);
                output.Append("            case ").Append(caseSuffix).Append(": return true");
                for (var i = 0; i < unionCase.PayloadTypes.Count; i++)
                {
                    var accessor = ".__dsharp_case_" + caseSuffix + "_item_" + i.ToString(CultureInfo.InvariantCulture);
                    output.Append(" && ").Append(EqualValue(unionCase.PayloadTypes[i], "this" + accessor, "__that" + accessor));
                }
                output.Append(";\n");
            }
            output.Append("            default: return false;\n        }\n    }\n}\n\n");
        }
        output.Append("public sealed class @").Append(module.Name).Append(" : ").Append(container)
            .Append("\n{\n    private @").Append(module.Name).Append("() {}\n}\n\npublic abstract class ")
            .Append(container).Append("\n{\n");
        bool NeedsLambdaReturn(object? node)
        {
            if (node is null || node is string) return false;
            if (node is BoundLambda { HasReturn: true }) return true;
            if (node is System.Collections.IEnumerable sequence)
            {
                foreach (var item in sequence) if (NeedsLambdaReturn(item)) return true;
                return false;
            }
            var type = node.GetType();
            return type.Name.StartsWith("Bound", StringComparison.Ordinal) && type.GetProperties()
                .Where(property => property.GetIndexParameters().Length == 0)
                .Any(property => NeedsLambdaReturn(property.GetValue(node)));
        }
        if (module.Functions.Any(f => f.HasReturn || NeedsLambdaReturn(f)))
        {
            output.Append("    private sealed class ").Append(signalType).Append("<T> : global::System.Exception\n")
                .Append("    {\n        public T Value { get; }\n        public ").Append(signalType).Append("(T value) { Value = value; }\n    }\n")
                .Append("    private static T ").Append(raiseMethod).Append("<T>(T value) => throw new ").Append(signalType).Append("<T>(value);\n");
        }
        if (module.Records.Count > 0 || module.Unions.Count > 0)
        {
            output.Append("    internal static bool ").Append(equalityDispatcher).Append("<__DSharp_Value>(__DSharp_Value __left, __DSharp_Value __right)\n    {\n")
                .Append("        if (global::System.Object.ReferenceEquals(__left, __right)) return true;\n")
                .Append("        if (__left is ").Append(structuralInterface).Append(" __data) return __data.__dsharp_equals(__right);\n")
                .Append("        return global::System.Collections.Generic.EqualityComparer<__DSharp_Value>.Default.Equals(__left, __right);\n    }\n");
        }
        foreach (var function in module.Functions)
        {
            SetTypeParameters(function.TypeParameters, functionProjections[function.Name]);
            output.Append("    public static ").Append(CType(function.ReturnType)).Append(" @").Append(function.Name)
                .Append(TypeParameterList(function.TypeParameters)).Append("(");
            for (var i = 0; i < function.Parameters.Count; i++)
            {
                if (i != 0) output.Append(", ");
                output.Append(CType(function.ParameterTypes.Count == 0 ? "int" : function.ParameterTypes[i])).Append(" __dsharp_param_").Append(i.ToString(CultureInfo.InvariantCulture));
            }
            output.Append(")");
            if (function.Expression is null && function.Locals.Count == 0 && function.Body is null && !function.HasReturn)
            {
                output.Append(" => ").Append(function.Value.ToString(CultureInfo.InvariantCulture)).Append(";\n");
                continue;
            }
            output.Append("\n    {\n");
            if (function.HasReturn) output.Append("        try\n        {\n");
            output.Append("        checked\n        {\n");
            var localNames = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < function.Parameters.Count; i++)
                localNames.Add(function.Parameters[i], "__dsharp_param_" + i.ToString(CultureInfo.InvariantCulture));
            var literalCount = 0;
            var localCount = 0;
            var conditionalCount = 0;
            var updateCount = 0;
            var matchCount = 0;
            var nullableOperationCount = 0;
            var propagationCount = 0;
            var lambdaCount = 0;
            var emitterReturnType = function.ReturnType;
            string PatternItem(BoundCasePattern pattern, int item, string value)
            {
                var union = module.Unions.Single(candidate => candidate.Name == Nominal(pattern.Union));
                var caseIndex = union.Cases.ToList().FindIndex(candidate => candidate.Name == pattern.Case);
                return "(" + value + ").__dsharp_case_" + caseIndex.ToString(CultureInfo.InvariantCulture) +
                    "_item_" + item.ToString(CultureInfo.InvariantCulture);
            }
            string PresentValue(string innerType, string value) => IsTypeParameter(innerType)
                ? "(" + CType(innerType) + ")(object)(" + value + ")"
                : IsValueType(innerType) ? "(" + value + ").Value" : value;
            string PatternCondition(BoundPattern pattern, string value)
            {
                switch (pattern)
                {
                    case BoundWildcardPattern or BoundBindingPattern:
                        // A nonconstant true expression keeps later arms and the
                        // defensive fallback reachable under C# warnings-as-errors.
                        return "global::System.Object.ReferenceEquals(null, null)";
                    case BoundNullPattern:
                        return "(" + value + " is null)";
                    case BoundPresentPattern present:
                        var presentTest = IsValueType(present.Type) ? "(" + value + ").HasValue" : "!global::System.Object.ReferenceEquals(" + value + ", null)";
                        return "(" + presentTest + " && " + PatternCondition(present.Inner, PresentValue(present.Type, value)) + ")";
                    case BoundLiteralPattern literal:
                        if (literal.Type == "Unit") return "global::System.Object.ReferenceEquals(null, null)";
                        var expected = EmitExpression(literal.Value);
                        return literal.Type == "string"
                            ? "global::System.String.Equals(" + value + ", " + expected + ", global::System.StringComparison.Ordinal)"
                            : "(" + value + " == " + expected + ")";
                    case BoundCasePattern unionCase:
                        var union = module.Unions.Single(candidate => candidate.Name == Nominal(unionCase.Union));
                        var caseIndex = union.Cases.ToList().FindIndex(candidate => candidate.Name == unionCase.Case);
                        var conditions = new List<string> { "(" + value + ").__dsharp_tag == " + caseIndex.ToString(CultureInfo.InvariantCulture) };
                        for (var i = 0; i < unionCase.Arguments.Count; i++)
                            conditions.Add(PatternCondition(unionCase.Arguments[i], PatternItem(unionCase, i, value)));
                        return "(" + string.Join(" && ", conditions) + ")";
                    default: throw new InvalidOperationException("Unknown bound pattern.");
                }
            }
            void EmitPatternBindings(BoundPattern pattern, string value, List<string> names)
            {
                switch (pattern)
                {
                    case BoundBindingPattern binding:
                        var name = "__dsharp_local_" + (localCount++).ToString(CultureInfo.InvariantCulture);
                        output.Append("            ").Append(CType(binding.Type)).Append(" ").Append(name).Append(" = ").Append(value).Append(";\n")
                            .Append("            _ = ").Append(name).Append(";\n");
                        localNames.Add(binding.Name, name);
                        names.Add(binding.Name);
                        break;
                    case BoundPresentPattern present:
                        EmitPatternBindings(present.Inner, PresentValue(present.Type, value), names);
                        break;
                    case BoundCasePattern unionCase:
                        for (var i = 0; i < unionCase.Arguments.Count; i++)
                            EmitPatternBindings(unionCase.Arguments[i], PatternItem(unionCase, i, value), names);
                        break;
                }
            }
            string EmitExpression(BoundExpression expression)
            {
                switch (expression)
                {
                    case BoundUnit: return "default(" + unitType + ")";
                    case BoundNull nullable: return "default(" + CType(nullable.Type) + ")";
                    case BoundLambda lambda:
                        var lambdaId = (lambdaCount++).ToString(CultureInfo.InvariantCulture);
                        var lambdaName = "__dsharp_lambda_" + lambdaId;
                        output.Append("            static ").Append(CType(lambda.ReturnType)).Append(" ").Append(lambdaName).Append("(");
                        for (var i = 0; i < lambda.Parameters.Count; i++)
                        {
                            if (i != 0) output.Append(", ");
                            output.Append(CType(lambda.ParameterTypes[i])).Append(" __dsharp_lambda_param_").Append(lambdaId).Append("_").Append(i.ToString(CultureInfo.InvariantCulture));
                        }
                        output.Append(")\n            {\n");
                        var savedNames = localNames;
                        var savedEmitterReturnType = emitterReturnType;
                        localNames = new Dictionary<string, string>(StringComparer.Ordinal);
                        for (var i = 0; i < lambda.Parameters.Count; i++)
                            localNames.Add(lambda.Parameters[i], "__dsharp_lambda_param_" + lambdaId + "_" + i.ToString(CultureInfo.InvariantCulture));
                        emitterReturnType = lambda.ReturnType;
                        if (lambda.HasReturn) output.Append("            try\n            {\n");
                        EmitBlock(lambda.Body, lambda.ReturnType);
                        if (lambda.HasReturn)
                            output.Append("            }\n            catch (").Append(signalType).Append("<").Append(CType(lambda.ReturnType)).Append("> __dsharp_lambda_return_").Append(lambdaId).Append(")\n            {\n                return __dsharp_lambda_return_").Append(lambdaId).Append(".Value;\n            }\n");
                        output.Append("            }\n");
                        localNames = savedNames;
                        emitterReturnType = savedEmitterReturnType;
                        return "((" + CType(lambda.Type) + ")" + lambdaName + ")";
                    case BoundInvoke invocation:
                        var callable = EmitExpression(invocation.Receiver);
                        var invocationArguments = invocation.Arguments.Select(EmitExpression).ToArray();
                        return "(" + callable + ").Invoke(" + string.Join(", ", invocationArguments) + ")";
                    case BoundPropagation propagation:
                        var propagationId = (propagationCount++).ToString(CultureInfo.InvariantCulture);
                        var propagationHelper = "__dsharp_propagate_" + propagationId;
                        var propagationValue = "__dsharp_propagate_value_" + propagationId;
                        output.Append("            ").Append(CType(propagation.Type)).Append(" ").Append(propagationHelper).Append("()\n            {\n");
                        var propagationOperand = EmitExpression(propagation.Operand);
                        output.Append("            ").Append(CType(propagation.OperandType)).Append(" ").Append(propagationValue).Append(" = ").Append(propagationOperand).Append(";\n")
                            .Append("            if (").Append(propagationValue).Append(".__dsharp_tag == 1)\n")
                            .Append("                _ = ").Append(raiseMethod).Append("<").Append(CType(propagation.ReturnType)).Append(">(")
                            .Append(CType(propagation.ReturnType)).Append(".__dsharp_case_1(").Append(propagationValue).Append(".__dsharp_case_1_item_0));\n")
                            .Append("            return ").Append(propagationValue).Append(".__dsharp_case_0_item_0;\n            }\n");
                        return propagationHelper + "()";
                    case BoundNullableLift lift: return "(" + CType(lift.Type) + ")(object)(" + EmitExpression(lift.Expression) + ")";
                    case BoundInt literal:
                        // C# constant overflow/division errors must instead be D#
                        // execution faults. Lift literals to ordinary int locals.
                        var name = "__dsharp_int_" + (literalCount++).ToString(CultureInfo.InvariantCulture);
                        output.Append("            int ").Append(name).Append(" = ")
                            .Append(literal.Value.ToString(CultureInfo.InvariantCulture)).Append(";\n");
                        return name;
                    case BoundNever never:
                        var neverHelper = "__dsharp_if_" + (conditionalCount++).ToString(CultureInfo.InvariantCulture);
                        output.Append("            ").Append(CType(never.Type)).Append(" ").Append(neverHelper).Append("()\n            {\n");
                        var neverValue = EmitExpression(never.Expression);
                        output.Append("            _ = ").Append(neverValue).Append(";\n            throw new global::System.InvalidOperationException(\"Noncompleting expression completed.\");\n            }\n");
                        return neverHelper + "()";
                    case BoundIf conditional:
                        var helper = "__dsharp_if_" + (conditionalCount++).ToString(CultureInfo.InvariantCulture);
                        output.Append("            ").Append(CType(conditional.Type)).Append(" ").Append(helper).Append("()\n            {\n");
                        var condition = EmitExpression(conditional.Condition);
                        output.Append("            if (").Append(condition).Append(")\n            {\n");
                        EmitBlock(conditional.Then, conditional.Type);
                        output.Append("            }\n            else\n            {\n");
                        EmitBlock(conditional.Else, conditional.Type);
                        output.Append("            }\n            }\n");
                        return helper + "()";
                    case BoundMatch match:
                        var matchId = (matchCount++).ToString(CultureInfo.InvariantCulture);
                        var matchHelper = "__dsharp_match_" + matchId;
                        var matchValue = "__dsharp_match_value_" + matchId;
                        output.Append("            ").Append(CType(match.Type)).Append(" ").Append(matchHelper).Append("()\n            {\n");
                        var scrutinee = EmitExpression(match.Scrutinee);
                        output.Append("            ").Append(CType(match.ScrutineeType)).Append(" ").Append(matchValue).Append(" = ").Append(scrutinee).Append(";\n")
                            .Append("            _ = ").Append(matchValue).Append(";\n");
                        foreach (var arm in match.Arms)
                        {
                            var patternTest = PatternCondition(arm.Pattern, matchValue);
                            output.Append("            if (").Append(patternTest).Append(")\n            {\n");
                            var armNames = new List<string>();
                            EmitPatternBindings(arm.Pattern, matchValue, armNames);
                            if (arm.GuardNever)
                            {
                                var neverGuard = EmitExpression(arm.Guard!);
                                output.Append("            _ = ").Append(neverGuard).Append(";\n")
                                    .Append("            throw new global::System.InvalidOperationException(\"Noncompleting guard completed.\");\n");
                            }
                            else
                            {
                                if (arm.Guard is not null)
                                {
                                    var guard = EmitExpression(arm.Guard);
                                    output.Append("            if (").Append(guard).Append(")\n            {\n");
                                }
                                var armValue = EmitExpression(arm.Value);
                                output.Append("            return ").Append(armValue).Append(";\n");
                                if (arm.Guard is not null) output.Append("            }\n");
                            }
                            output.Append("            }\n");
                            foreach (var armName in armNames) localNames.Remove(armName);
                        }
                        output.Append("            throw new global::System.InvalidOperationException(\"Exhaustive match had no matching arm.\");\n            }\n");
                        return matchHelper + "()";
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
                    case BoundCoalesce coalesce:
                        var coalesceId = (nullableOperationCount++).ToString(CultureInfo.InvariantCulture);
                        var coalesceHelper = "__dsharp_nullable_" + coalesceId;
                        var coalesceValue = "__dsharp_nullable_value_" + coalesceId;
                        output.Append("            ").Append(CType(coalesce.Type)).Append(" ").Append(coalesceHelper).Append("()\n            {\n");
                        var coalesceLeft = EmitExpression(coalesce.Left);
                        output.Append("            ").Append(CType(coalesce.ContainedType + "?")).Append(" ").Append(coalesceValue).Append(" = ").Append(coalesceLeft).Append(";\n");
                        var coalescePresent = IsValueType(coalesce.ContainedType) ? coalesceValue + ".HasValue" :
                            "!global::System.Object.ReferenceEquals(" + coalesceValue + ", null)";
                        var coalescePresentValue = coalesce.Type == coalesce.ContainedType + "?"
                            ? coalesceValue : PresentValue(coalesce.ContainedType, coalesceValue);
                        output.Append("            if (").Append(coalescePresent).Append(") return ").Append(coalescePresentValue).Append(";\n");
                        var coalesceFallback = EmitExpression(coalesce.Right);
                        output.Append("            return ").Append(coalesceFallback).Append(";\n            }\n");
                        return coalesceHelper + "()";
                    case BoundConditionalFieldAccess optionalField:
                        var optionalId = (nullableOperationCount++).ToString(CultureInfo.InvariantCulture);
                        var optionalHelper = "__dsharp_nullable_" + optionalId;
                        var optionalValue = "__dsharp_nullable_value_" + optionalId;
                        output.Append("            ").Append(CType(optionalField.Type)).Append(" ").Append(optionalHelper).Append("()\n            {\n");
                        var optionalReceiver = EmitExpression(optionalField.Receiver);
                        var optionalResult = optionalValue + ".@" + optionalField.Field;
                        if (IsTypeParameter(optionalField.FieldType) && optionalField.Type == optionalField.FieldType + "?")
                            optionalResult = "(" + CType(optionalField.Type) + ")(object)(" + optionalResult + ")";
                        output.Append("            ").Append(CType(optionalField.ReceiverType)).Append(" ").Append(optionalValue).Append(" = ").Append(optionalReceiver).Append(";\n")
                            .Append("            if (global::System.Object.ReferenceEquals(").Append(optionalValue).Append(", null)) return default(").Append(CType(optionalField.Type)).Append(");\n")
                            .Append("            return ").Append(optionalResult).Append(";\n            }\n");
                        return optionalHelper + "()";
                    case BoundRecordConstruction construction:
                        var definition = module.Records.Single(record => record.Name == Nominal(construction.Type));
                        var fields = construction.Fields.Select(field =>
                        {
                            var index = definition.Fields.ToList().FindIndex(item => item.Name == field.Name);
                            return "__dsharp_field_" + index.ToString(CultureInfo.InvariantCulture) + ": " + EmitExpression(field.Value);
                        }).ToArray();
                        return "new " + CType(construction.Type) + "(" + string.Join(", ", fields) + ")";
                    case BoundFieldAccess access:
                        return "(" + EmitExpression(access.Receiver) + ").@" + access.Field;
                    case BoundRecordUpdate update:
                        var updateDefinition = module.Records.Single(record => record.Name == Nominal(update.Type));
                        if (!GenericTypes.TryParse(update.Type, out var updateShape))
                            throw new InvalidOperationException("Invalid bound record type.");
                        var updateSubstitutions = updateDefinition.TypeParameters.Select((parameter, index) =>
                            (parameter, type: updateShape.Arguments[index].ToString())).ToDictionary(item => item.parameter, item => item.type, StringComparer.Ordinal);
                        var updateHelper = "__dsharp_update_" + (updateCount++).ToString(CultureInfo.InvariantCulture);
                        output.Append("            ").Append(CType(update.Type)).Append(" ").Append(updateHelper).Append("(").Append(CType(update.Type)).Append(" __original");
                        for (var i = 0; i < update.Fields.Count; i++)
                        {
                            var field = updateDefinition.Fields.Single(item => item.Name == update.Fields[i].Name);
                            output.Append(", ").Append(CType(GenericTypes.Substitute(field.Type, updateSubstitutions))).Append(" __update_").Append(i.ToString(CultureInfo.InvariantCulture));
                        }
                        output.Append(") => new ").Append(CType(update.Type)).Append("(");
                        for (var i = 0; i < updateDefinition.Fields.Count; i++)
                        {
                            if (i != 0) output.Append(", ");
                            var field = updateDefinition.Fields[i];
                            var updateIndex = update.Fields.ToList().FindIndex(item => item.Name == field.Name);
                            output.Append(updateIndex < 0 ? "__original.@" + field.Name : "__update_" + updateIndex.ToString(CultureInfo.InvariantCulture));
                        }
                        output.Append(");\n");
                        var receiver = EmitExpression(update.Receiver);
                        var updates = update.Fields.Select(field => EmitExpression(field.Value)).ToArray();
                        return updateHelper + "(" + receiver + (updates.Length == 0 ? "" : ", " + string.Join(", ", updates)) + ")";
                    case BoundUnionConstruction unionConstruction:
                        var unionDefinition = module.Unions.Single(union => union.Name == Nominal(unionConstruction.Type));
                        var unionCaseIndex = unionDefinition.Cases.ToList().FindIndex(unionCase => unionCase.Name == unionConstruction.Case);
                        var unionArguments = unionConstruction.Arguments.Select(EmitExpression).ToArray();
                        return CType(unionConstruction.Type) + ".__dsharp_case_" + unionCaseIndex.ToString(CultureInfo.InvariantCulture) + "(" + string.Join(", ", unionArguments) + ")";
                    case BoundCall call:
                        var args = call.Arguments.Select(EmitExpression).ToArray();
                        var slots = functionProjections[call.Name];
                        var typeArguments = new List<string>();
                        for (var i = 0; i < call.TypeArguments.Count; i++)
                        {
                            var argument = call.TypeArguments[i];
                            typeArguments.Add(CType(argument));
                            if (slots[i] && GenericTypes.TryParse(argument, out var shape))
                                typeArguments.Add(CType((shape with { IsNullable = true }).ToString()));
                        }
                        var callTypes = typeArguments.Count == 0 ? "" : "<" + string.Join(", ", typeArguments) + ">";
                        return container + ".@" + call.Name + callTypes + "(" + string.Join(", ", args) + ")";
                    case BoundUnary unary: return "(" + unary.Operator + EmitExpression(unary.Operand) + ")";
                    case BoundBinary binary:
                        var left = EmitExpression(binary.Left);
                        var right = EmitExpression(binary.Right);
                        if ((binary.Operator is "==" or "!=") && (binary.OperandType.EndsWith("?", StringComparison.Ordinal) ||
                            binary.OperandType == "string" || IsNominal(binary.OperandType) || IsTypeParameter(binary.OperandType)))
                        {
                            var equals = EqualValue(binary.OperandType, left, right);
                            return binary.Operator == "!=" ? "(!(" + equals + "))" : equals;
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
                    output.Append("            ").Append(CType(local.Type)).Append(" ").Append(emittedName).Append(" = ").Append(initializer).Append(";\n");
                    if (local.Type == "Unit") output.Append("            _ = ").Append(emittedName).Append(";\n");
                    localNames.Add(local.Name, emittedName);
                }
            }
            void EmitStatements(IReadOnlyList<BoundStatement> statements)
            {
                foreach (var statement in statements)
                {
                    switch (statement)
                    {
                        case BoundLetStatement declaration:
                            EmitLocals(new[] { declaration.Local }); break;
                        case BoundReturnStatement ret:
                            var returned = EmitExpression(ret.Value);
                            output.Append("            _ = ").Append(raiseMethod).Append("<").Append(CType(emitterReturnType)).Append(">(").Append(returned).Append(");\n");
                            break;
                        case BoundExpressionStatement expr:
                            var value = EmitExpression(expr.Expression);
                            output.Append("            _ = ").Append(value).Append(";\n");
                            break;
                    }
                }
            }
            void EmitBlock(BoundBlock block, string returnType)
            {
                if (block.Statements.Count == 0) EmitLocals(block.Locals);
                else EmitStatements(block.Statements);
                if (block.CanComplete)
                {
                    var expression = EmitExpression(block.Result);
                    output.Append("            return ").Append(expression).Append(";\n");
                }
                else
                {
                    if (block.EvaluateResult)
                    {
                        var neverValue = EmitExpression(block.Result);
                        output.Append("            _ = ").Append(neverValue).Append(";\n");
                    }
                    output.Append("            throw new global::System.InvalidOperationException(\"Noncompleting block completed.\");\n");
                }
                foreach (var local in block.Locals) localNames.Remove(local.Name);
            }
            if (function.Body is { } body) EmitBlock(body, function.ReturnType);
            else
            {
                EmitLocals(function.Locals);
                var result = EmitExpression(function.Expression ?? (function.ResultLocal is { } resultLocal
                    ? (BoundExpression)new BoundReference(resultLocal) : new BoundInt(function.Value)));
                output.Append("            return ").Append(result).Append(";\n");
            }
            output.Append("        }\n");
            if (function.HasReturn)
                output.Append("        }\n        catch (").Append(signalType).Append("<").Append(CType(function.ReturnType)).Append("> __dsharp_return)\n        {\n            return __dsharp_return.Value;\n        }\n");
            output.Append("    }\n");
        }
        output.Append("}\n");
        foreach (var entry in delegateNames.OrderBy(pair => pair.Key))
        {
            var parameters = Enumerable.Range(0, entry.Key).Select(index => "T" + index.ToString(CultureInfo.InvariantCulture)).ToArray();
            output.Append("\npublic delegate TReturn ").Append(entry.Value).Append("<")
                .Append(string.Join(", ", parameters.Append("TReturn"))).Append(">(")
                .Append(string.Join(", ", parameters.Select((parameter, index) => parameter + " value" + index.ToString(CultureInfo.InvariantCulture))))
                .Append(");\n");
        }
        return output.ToString();
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
