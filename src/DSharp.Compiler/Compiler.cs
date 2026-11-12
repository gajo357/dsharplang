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
public sealed record CallSyntax(Token Name, Token OpenParen, IReadOnlyList<ExpressionSyntax> Arguments) : ExpressionSyntax(Name);
public sealed record MemberCallSyntax(ExpressionSyntax Receiver, Token Member, Token OpenParen, IReadOnlyList<ExpressionSyntax> Arguments) : ExpressionSyntax(Member);
public sealed record RecordFieldSyntax(Token Name, Token Type);
public sealed record RecordDeclarationSyntax(Token Name, IReadOnlyList<RecordFieldSyntax> Fields)
{
    public IReadOnlyList<Token> DocumentationComments { get; init; } = Array.Empty<Token>();
}
public sealed record RecordInitializerSyntax(Token Name, ExpressionSyntax Value);
public sealed record RecordConstructionSyntax(Token Name, IReadOnlyList<RecordInitializerSyntax> Fields) : ExpressionSyntax(Name);
public sealed record FieldAccessSyntax(ExpressionSyntax Receiver, Token Field) : ExpressionSyntax(Field);
public sealed record RecordUpdateSyntax(ExpressionSyntax Receiver, Token With, IReadOnlyList<RecordInitializerSyntax> Fields) : ExpressionSyntax(With);
public sealed record UnionCaseSyntax(Token Name, IReadOnlyList<Token> PayloadTypes);
public sealed record UnionDeclarationSyntax(Token Name, IReadOnlyList<UnionCaseSyntax> Cases)
{
    public IReadOnlyList<Token> DocumentationComments { get; init; } = Array.Empty<Token>();
}
public sealed record ParameterSyntax(Token Name, Token Type);
public sealed record UnarySyntax(Token Operator, ExpressionSyntax Operand) : ExpressionSyntax(Operator);
public sealed record BinarySyntax(ExpressionSyntax Left, Token Operator, ExpressionSyntax Right) : ExpressionSyntax(Operator);
public abstract record PatternSyntax(Token Token);
public sealed record WildcardPatternSyntax(Token Wildcard) : PatternSyntax(Wildcard);
public sealed record LiteralPatternSyntax(Token Literal, Token? Minus = null) : PatternSyntax(Minus ?? Literal);
public sealed record UnitPatternSyntax(Token Open) : PatternSyntax(Open);
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
public sealed record BoundCall(string Name, IReadOnlyList<BoundExpression> Arguments) : BoundExpression;
public sealed record BoundRecordField(string Name, string Type);
public sealed record BoundRecordDeclaration(string Name, IReadOnlyList<BoundRecordField> Fields);
public sealed record BoundRecordInitializer(string Name, BoundExpression Value);
public sealed record BoundRecordConstruction(string Type, IReadOnlyList<BoundRecordInitializer> Fields) : BoundExpression;
public sealed record BoundFieldAccess(BoundExpression Receiver, string ReceiverType, string Field) : BoundExpression;
public sealed record BoundRecordUpdate(BoundExpression Receiver, string Type, IReadOnlyList<BoundRecordInitializer> Fields) : BoundExpression;
public sealed record BoundUnionCase(string Name, IReadOnlyList<string> PayloadTypes);
public sealed record BoundUnionDeclaration(string Name, IReadOnlyList<BoundUnionCase> Cases);
public sealed record BoundUnionConstruction(string Type, string Case, IReadOnlyList<BoundExpression> Arguments) : BoundExpression;
public abstract record BoundPattern;
public sealed record BoundWildcardPattern : BoundPattern;
public sealed record BoundBindingPattern(string Name, string Type) : BoundPattern;
public sealed record BoundLiteralPattern(BoundExpression Value, string Type) : BoundPattern;
public sealed record BoundCasePattern(string Union, string Case, IReadOnlyList<BoundPattern> Arguments) : BoundPattern;
public sealed record BoundMatchArm(BoundPattern Pattern, BoundExpression? Guard, BoundExpression Value, bool GuardNever);
public sealed record BoundMatch(BoundExpression Scrutinee, string ScrutineeType,
    IReadOnlyList<BoundMatchArm> Arms, string Type) : BoundExpression;
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
            else if (i + 1 < source.Length && (source.Substring(i, 2) is "==" or "!=" or "<=" or ">=" or "&&" or "||" or "=>")) { Advance(); Advance(); }
            else if ("(),.:{}=+-*/%!<>|".Contains(c)) Advance();
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
                return tokens[index + 1].Text is "." or "with" or "{" or
                    "||" or "&&" or "==" or "!=" or "<" or "<=" or ">" or ">=" or "+" or "-" or "*" or "/" or "%";
            }
        }
        return false;
    }
    private ExpressionSyntax Expression(int? line = null, int minimumPrecedence = 0, bool stopAtBlock = false)
    {
        ExpressionSyntax left;
        if (Current.IsDocumentation) throw Error("Expected an expression.");
        if (line is { } expectedLine && Current.Span.Line != expectedLine)
            throw new SourceError(new("DS1002", path, Current.Span, "Expected an operand on the binding line."));
        if (Current.Text == "match")
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
                    guard = Expression(Current.Span.Line);
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
            left = new UnarySyntax(op, Expression(line is null ? null : Current.Span.Line, 7, stopAtBlock));
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
            if (name.Text[0] is not ('"' or '\'') && name.Text is not ("true" or "false") && Current.Text == "(" && (line is null || Current.Span.Line == name.Span.Line))
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
                left = new CallSyntax(name, open, args);
            }
            else if ((!stopAtBlock || ConditionRecordConstruction()) && name.Text[0] is not ('"' or '\'') && name.Text is not ("true" or "false") &&
                !char.IsAsciiDigit(name.Text[0]) && Current.Text == "{" && (line is null || Current.Span.Line == name.Span.Line))
                left = new RecordConstructionSyntax(name, RecordInitializers());
            else left = new ValueSyntax(name);
        }
        while (true)
        {
            // DEC-02 permits a leading dot across a newline, even outside grouping.
            if (Current.Text == ".")
            {
                Take();
                var member = Identifier();
                if (Current.Text == "(" && (line is null || Current.Span.Line == member.Span.Line))
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
                    left = new MemberCallSyntax(left, member, open, args);
                }
                else left = new FieldAccessSyntax(left, member);
                continue;
            }
            if (line is not null && Current.Span.Line != tokens[position - 1].Span.Line) break;
            if (Current.Text == "with")
            {
                var with = Take();
                left = new RecordUpdateSyntax(left, with, RecordInitializers());
                continue;
            }
            var precedence = Current.Text switch { "||" => 1, "&&" => 2, "==" or "!=" => 3,
                "<" or "<=" or ">" or ">=" => 4, "+" or "-" => 5, "*" or "/" or "%" => 6, _ => 0 };
            if (precedence == 0 || precedence < minimumPrecedence) break;
            var op = Take();
            var rightLine = line is not null ? Current.Span.Line : line;
            var right = Expression(rightLine, precedence + 1, stopAtBlock);
            static bool Comparison(ExpressionSyntax expr) => expr is BinarySyntax binary &&
                binary.Operator.Text is "==" or "!=" or "<" or "<=" or ">" or ">=";
            if (precedence is 3 or 4 && (Comparison(left) || Comparison(right)))
                throw new SourceError(new("DS2013", path, op.Span, "Chained comparisons require explicit grouping."));
            left = new BinarySyntax(left, op, right);
        }
        return left;
    }
    private static bool AllowsContinuation(ExpressionSyntax expression) => expression switch
    {
        IfSyntax or MatchSyntax or GroupSyntax or CallSyntax or MemberCallSyntax or BinarySyntax or RecordConstructionSyntax or FieldAccessSyntax or RecordUpdateSyntax => true,
        UnarySyntax unary => AllowsContinuation(unary.Operand),
        _ => false
    };
    private PatternSyntax Pattern()
    {
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
                if (Current.Text == ":") { Take(); if (Current.Text == "") throw Error("Expected a local type annotation."); annotation = Take(); }
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
                Take(); var recordName = Identifier(); Expect("{");
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
                                    payloads.Add(Take());
                                    if (Current.Text != ",") break;
                                    Take();
                                } while (true);
                            }
                            Expect(")");
                        }
                        cases.Add(new(caseName, payloads));
                    }
                    Expect("}");
                    unions.Add(new(recordName, cases) { DocumentationComments = declarationDocs });
                    continue;
                }
                var fields = new List<RecordFieldSyntax>();
                while (Current.Text is not ("}" or ""))
                {
                    var fieldName = Identifier(); Expect(":");
                    if (Current.Text == "") throw Error("Expected a field type.");
                    var fieldType = Take();
                    fields.Add(new(fieldName, fieldType));
                    if (Current.Text != "}" && Current.Span.Line == fieldType.Span.Line)
                        throw Error("Expected a newline after a record field declaration.");
                }
                Expect("}");
                records.Add(new(recordName, fields) { DocumentationComments = declarationDocs });
                continue;
            }
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
            var body = Block();
            // Prefer the missing-result span over a following stray closing brace.
            if (Current.Text == "}" && type.Text == "int" && body.Result is UnitSyntax missing &&
                body.Statements.All(statement => statement is LetStatementSyntax))
                throw new SourceError(new("DS2002", path, missing.Token.Span, "A reachable int result is required."));
            functions.Add(new(function, type, body.Result.Token) { Expression = body.Result, Body = body, DocumentationComments = declarationDocs, Parameters = parameters.Count == 0 ? Array.Empty<ParameterSyntax>() : parameters, Bindings = body.Bindings });
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
        bool SupportedType(string type) => type is "int" or "bool" or "string" or "char" or "Unit" || records.ContainsKey(type) || unions.ContainsKey(type);
        foreach (var record in syntax.Records)
        {
            var fields = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in record.Fields)
            {
                if (!fields.Add(field.Name.Text)) Fail("DS2015", field.Name, "Duplicate record field.");
                if (!SupportedType(field.Type.Text)) Fail("DS2015", field.Type, "Unsupported record field type.");
            }
        }
        var cases = new Dictionary<string, List<(UnionDeclarationSyntax Union, UnionCaseSyntax Case)>>(StringComparer.Ordinal);
        foreach (var union in syntax.Unions)
        {
            var caseNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var unionCase in union.Cases)
            {
                if (!caseNames.Add(unionCase.Name.Text)) Fail("DS2017", unionCase.Name, "Duplicate union case.");
                foreach (var type in unionCase.PayloadTypes)
                    if (!SupportedType(type.Text)) Fail("DS2017", type, "Unsupported union payload type.");
                if (!cases.TryGetValue(unionCase.Name.Text, out var candidates))
                    cases.Add(unionCase.Name.Text, candidates = new());
                candidates.Add((union, unionCase));
            }
        }
        var signatures = new Dictionary<string, FunctionSyntax>(StringComparer.Ordinal);
        foreach (var function in syntax.Functions)
        {
            if (!signatures.TryAdd(function.Name.Text, function)) Fail("DS2001", function.Name, "Duplicate function declaration.");
            if (records.TryGetValue(function.Name.Text, out var conflictingRecord))
                Fail("DS2015", function.Name.Span.Start > conflictingRecord.Name.Span.Start ? function.Name : conflictingRecord.Name,
                    "A function and record constructor cannot share a name.");
            if (!SupportedType(function.ReturnType.Text)) Fail("DS2003", function.ReturnType, "Unsupported return type.");
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var parameter in function.Parameters)
            {
                if (!names.Add(parameter.Name.Text)) Fail("DS2010", parameter.Name, "Duplicate parameter.");
                if (!SupportedType(parameter.Type.Text)) Fail("DS2011", parameter.Type, "Unsupported parameter type.");
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
                if (actual != expected && actual != "<never>") Fail("DS2002", syntaxNode.Token, $"Expected {expected}, found {actual}.");
            }
            // Some noncompleting expressions still carry a known nominal type.
            // Preserve it when checking unreachable code and chained record operations.
            string? KnownType(BoundExpression expression) => expression switch
            {
                BoundRecordConstruction construction => construction.Type,
                BoundRecordUpdate update => update.Type,
                BoundUnionConstruction construction => construction.Type,
                BoundFieldAccess access when records.TryGetValue(access.ReceiverType, out var record) =>
                    record.Fields.FirstOrDefault(field => field.Name.Text == access.Field)?.Type.Text,
                BoundCall call when signatures.TryGetValue(call.Name, out var target) => target.ReturnType.Text,
                BoundNever never => never.Type,
                BoundIf conditional => conditional.Type,
                BoundMatch match => match.Type,
                _ => null
            };
            (IReadOnlyList<BoundRecordInitializer> Fields, bool Never) BindRecordFields(
                RecordDeclarationSyntax record, IReadOnlyList<RecordInitializerSyntax> initializers, Token location, bool requireAll)
            {
                var known = record.Fields.ToDictionary(field => field.Name.Text, StringComparer.Ordinal);
                var used = new HashSet<string>(StringComparer.Ordinal);
                var bound = new List<BoundRecordInitializer>();
                var never = false;
                foreach (var initializer in initializers)
                {
                    if (!known.TryGetValue(initializer.Name.Text, out var field))
                        Fail("DS2016", initializer.Name, "Unknown record field.");
                    if (!used.Add(initializer.Name.Text))
                        Fail("DS2016", initializer.Name, "Duplicate record field initializer.");
                    var value = BindExpression(initializer.Value);
                    Require(initializer.Value, value.Type, field!.Type.Text);
                    never |= value.Type == "<never>";
                    bound.Add(new(initializer.Name.Text, Coerce(value.Expression, field.Type.Text, value.Type)));
                }
                if (requireAll && used.Count != known.Count)
                    Fail("DS2016", location, "Record construction requires every field.");
                return (bound, never);
            }
            (UnionDeclarationSyntax Union, UnionCaseSyntax Case) BareCase(Token name)
            {
                var candidates = cases[name.Text];
                if (candidates.Count != 1 || signatures.ContainsKey(name.Text) || records.ContainsKey(name.Text))
                    Fail("DS2018", name, "Ambiguous union case; qualify it with its union name.");
                return candidates[0];
            }
            (BoundExpression Expression, string Type) BindCase(UnionDeclarationSyntax union,
                UnionCaseSyntax unionCase, Token name, IReadOnlyList<ExpressionSyntax> arguments)
            {
                if (unionCase.PayloadTypes.Count != arguments.Count)
                    Fail("DS2018", name, "Incorrect number of union case payloads.");
                var payloads = new List<BoundExpression>();
                var never = false;
                for (var i = 0; i < arguments.Count; i++)
                {
                    var payload = BindExpression(arguments[i]);
                    var expected = unionCase.PayloadTypes[i].Text;
                    Require(arguments[i], payload.Type, expected);
                    never |= payload.Type == "<never>";
                    payloads.Add(Coerce(payload.Expression, expected, payload.Type));
                }
                return (new BoundUnionConstruction(union.Name.Text, unionCase.Name.Text, payloads),
                    never ? "<never>" : union.Name.Text);
            }
            (BoundExpression Expression, string Type) BindQualifiedCase(UnionDeclarationSyntax union,
                Token name, IReadOnlyList<ExpressionSyntax> arguments)
            {
                var unionCase = union.Cases.FirstOrDefault(item => item.Name.Text == name.Text);
                if (unionCase is null) Fail("DS2018", name, "Unknown union case.");
                return BindCase(union, unionCase!, name, arguments);
            }
            BoundPattern BindPattern(PatternSyntax pattern, string expected, List<string> introduced)
            {
                switch (pattern)
                {
                    case WildcardPatternSyntax:
                        return new BoundWildcardPattern();
                    case UnitPatternSyntax unit:
                        if (expected != "Unit") Fail("DS2019", unit.Token, $"Expected a {expected} pattern.");
                        return new BoundLiteralPattern(new BoundUnit(), "Unit");
                    case LiteralPatternSyntax literal:
                        var literalValue = BindExpression(new ValueSyntax(literal.Literal));
                        if (literal.Minus is not null && literalValue.Type != "int")
                            Fail("DS2019", literal.Token, "Only an int literal can be negative.");
                        if (literalValue.Type != expected)
                            Fail("DS2019", literal.Token, $"Expected a {expected} pattern.");
                        return new BoundLiteralPattern(literal.Minus is null ? literalValue.Expression :
                            new BoundUnary("-", literalValue.Expression), literalValue.Type);
                    case NamePatternSyntax name:
                        if (locals.ContainsKey(name.Name.Text))
                            Fail("DS2006", name.Name, "A pattern binding cannot shadow a visible local or parameter.");
                        if (unions.ContainsKey(expected) && cases.ContainsKey(name.Name.Text))
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
                            if (locals.ContainsKey(qualifier.Text))
                                Fail("DS2019", qualifier, "A local value cannot qualify a union case pattern.");
                            if (!unions.TryGetValue(qualifier.Text, out var qualified))
                                Fail("DS2019", qualifier, "Unknown union type in pattern.");
                            union = qualified!;
                            unionCase = union.Cases.FirstOrDefault(item => item.Name.Text == casePattern.Name.Text)!;
                            if (unionCase is null) Fail("DS2019", casePattern.Name, "Unknown union case in pattern.");
                        }
                        else
                        {
                            if (locals.ContainsKey(casePattern.Name.Text))
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
                if (union.Name.Text != expected) Fail("DS2019", name, $"Case belongs to {union.Name.Text}, not {expected}.");
                if (unionCase.PayloadTypes.Count != arguments.Count) Fail("DS2019", name, "Incorrect number of case patterns.");
                var argumentsBound = new List<BoundPattern>();
                for (var i = 0; i < arguments.Count; i++)
                    argumentsBound.Add(BindPattern(arguments[i], unionCase.PayloadTypes[i].Text, introduced));
                return new BoundCasePattern(union.Name.Text, unionCase.Name.Text, argumentsBound);
            }
            (BoundExpression Expression, string Type) BindExpression(ExpressionSyntax expression)
            {
                switch (expression)
                {
                    case UnitSyntax: return (new BoundUnit(), "Unit");
                    case MatchSyntax match:
                        var scrutinee = BindExpression(match.Scrutinee);
                        var scrutineeType = scrutinee.Type == "<never>" ? KnownType(scrutinee.Expression) : scrutinee.Type;
                        if (scrutineeType is null || !SupportedType(scrutineeType))
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
                                var value = BindExpression(arm.Value);
                                if (!guardNever && value.Type != "<never>")
                                {
                                    if (matchResultType is null) matchResultType = value.Type;
                                    else Require(arm.Value, value.Type, matchResultType);
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
                                unionCase.PayloadTypes.Select(type => type.Text).ToArray())).ToArray())).ToArray();
                        if (!MatchCoverage.IsExhaustive(scrutineeType!,
                            boundArms.Where(arm => arm.Guard is null).Select(arm => arm.Pattern).ToArray(), coverageUnions))
                            Fail("DS2020", match.Match, "Match patterns are not exhaustive.");
                        var emittedMatchType = matchResultType ?? "Unit";
                        var arms = boundArms.Select(arm => new BoundMatchArm(arm.Pattern, arm.Guard,
                            arm.GuardNever ? arm.Value : Coerce(arm.Value, emittedMatchType, arm.Type), arm.GuardNever)).ToArray();
                        return (new BoundMatch(Coerce(scrutinee.Expression, scrutineeType!, scrutinee.Type), scrutineeType!, arms, emittedMatchType),
                            scrutinee.Type == "<never>" || boundArms.All(arm => arm.GuardNever || arm.Type == "<never>") ? "<never>" : emittedMatchType);
                    case IfSyntax conditional:
                        var condition = BindExpression(conditional.Condition);
                        Require(conditional.Condition, condition.Type, "bool");
                        var thenBlock = BindBlock(conditional.Then);
                        var elseBlock = BindBlock(conditional.Else);
                        if (!conditional.HasElse && thenBlock.Type is not ("Unit" or "<never>"))
                            Fail("DS1002", conditional.MissingElseLocation ?? conditional.If, "An if without else requires a Unit branch.");
                        if (thenBlock.Type != "<never>" && elseBlock.Type != "<never>" && thenBlock.Type != elseBlock.Type)
                        {
                            var location = conditional.Then.Result is UnitSyntax ? conditional.Then.Result : conditional.Else.Result;
                            Fail("DS2002", location.Token, "Conditional branches must have the same type.");
                        }
                        var branchType = thenBlock.Type == "<never>" ? elseBlock.Type : thenBlock.Type;
                        var emittedType = branchType == "<never>" ? "Unit" : branchType;
                        if (condition.Type == "<never>") branchType = "<never>";
                        return (new BoundIf(Coerce(condition.Expression, "bool", condition.Type),
                            CoerceBlock(thenBlock.Block, emittedType), CoerceBlock(elseBlock.Block, emittedType), emittedType), branchType);
                    case GroupSyntax group: return BindExpression(group.Inner);
                    case RecordConstructionSyntax construction:
                        if (locals.ContainsKey(construction.Name.Text)) Fail("DS2016", construction.Name, "A local value is not a record constructor.");
                        if (records.ContainsKey(construction.Name.Text) && cases.ContainsKey(construction.Name.Text))
                            Fail("DS2018", construction.Name, "Ambiguous record constructor and union case.");
                        if (!records.TryGetValue(construction.Name.Text, out var record)) Fail("DS2016", construction.Name, "Unknown record type.");
                        var constructed = BindRecordFields(record!, construction.Fields, construction.Name, true);
                        return (new BoundRecordConstruction(construction.Name.Text, constructed.Fields), constructed.Never ? "<never>" : construction.Name.Text);
                    case FieldAccessSyntax access:
                        if (access.Receiver is ValueSyntax qualifier && !locals.ContainsKey(qualifier.Value.Text) &&
                            unions.TryGetValue(qualifier.Value.Text, out var qualifiedUnion))
                            return BindQualifiedCase(qualifiedUnion, access.Field, Array.Empty<ExpressionSyntax>());
                        var receiver = BindExpression(access.Receiver);
                        var receiverType = receiver.Type == "<never>" ? KnownType(receiver.Expression) : receiver.Type;
                        if (!records.TryGetValue(receiverType ?? "", out var accessedRecord))
                            Fail("DS2016", access.Field, "Field access requires a record value.");
                        var accessedField = accessedRecord!.Fields.FirstOrDefault(field => field.Name.Text == access.Field.Text);
                        if (accessedField is null) Fail("DS2016", access.Field, "Unknown record field.");
                        return (new BoundFieldAccess(Coerce(receiver.Expression, receiverType!, receiver.Type), receiverType!, access.Field.Text),
                            receiver.Type == "<never>" ? "<never>" : accessedField!.Type.Text);
                    case RecordUpdateSyntax update:
                        var original = BindExpression(update.Receiver);
                        var originalType = original.Type == "<never>" ? KnownType(original.Expression) : original.Type;
                        if (!records.TryGetValue(originalType ?? "", out var updatedRecord))
                            Fail("DS2016", update.With, "A copy update requires a record value.");
                        var changes = BindRecordFields(updatedRecord!, update.Fields, update.With, false);
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
                        if (!locals.TryGetValue(token.Text, out var local) && cases.ContainsKey(token.Text))
                        {
                            var selected = BareCase(token);
                            return BindCase(selected.Union, selected.Case, token, Array.Empty<ExpressionSyntax>());
                        }
                        if (!locals.TryGetValue(token.Text, out local))
                            Fail("DS2007", token, "Unknown local; bindings are visible only after their initializer and within their function.");
                        return (new BoundReference(token.Text), local.Type);
                    case CallSyntax call:
                        if (locals.ContainsKey(call.Name.Text)) Fail("DS1002", call.OpenParen, "Local values are not callable.");
                        if (cases.ContainsKey(call.Name.Text))
                        {
                            var selected = BareCase(call.Name);
                            return BindCase(selected.Union, selected.Case, call.Name, call.Arguments);
                        }
                        if (!signatures.TryGetValue(call.Name.Text, out var target)) Fail("DS2009", call.Name, "Unknown function.");
                        if (target!.Parameters.Count != call.Arguments.Count) Fail("DS2012", call.Name, "Incorrect number of arguments.");
                        var args = new List<BoundExpression>();
                        var callNever = false;
                        for (var i = 0; i < call.Arguments.Count; i++)
                        {
                            var arg = BindExpression(call.Arguments[i]);
                            Require(call.Arguments[i], arg.Type, target.Parameters[i].Type.Text);
                            callNever |= arg.Type == "<never>";
                            args.Add(Coerce(arg.Expression, target.Parameters[i].Type.Text, arg.Type));
                        }
                        return (new BoundCall(call.Name.Text, args), callNever ? "<never>" : target.ReturnType.Text);
                    case MemberCallSyntax member:
                        if (member.Receiver is ValueSyntax memberQualifier && !locals.ContainsKey(memberQualifier.Value.Text) &&
                            unions.TryGetValue(memberQualifier.Value.Text, out var memberUnion))
                            return BindQualifiedCase(memberUnion, member.Member, member.Arguments);
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
                            var operandType = left.Type == "<never>" ? (right.Type == "<never>" ? "int" : right.Type) : left.Type;
                            if (operandType == "Unit") Fail("DS2002", binary.Operator, "Unit equality is not supported.");
                            Require(binary.Right, right.Type, operandType);
                            return (new BoundBinary(Coerce(left.Expression, operandType, left.Type), op, Coerce(right.Expression, operandType, right.Type)) { OperandType = operandType }, left.Type == "<never>" || right.Type == "<never>" ? "<never>" : "bool");
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
                if (actual == "<never>") return new BoundNever(expression, expected);
                if (expression is BoundIf conditional)
                    return conditional with { Type = expected, Then = CoerceBlock(conditional.Then, expected), Else = CoerceBlock(conditional.Else, expected) };
                return expression;
            }
            BoundBlock CoerceBlock(BoundBlock block, string expected) => block.EvaluateResult
                ? block with { Result = Coerce(block.Result, expected, block.ResultType) }
                : block;
            bool hasReturn = false;
            (BoundBlock Block, string Type) BindBlock(BlockSyntax block, bool keepLocals = false)
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
                                if (locals.ContainsKey(binding.Name.Text)) Fail("DS2006", binding.Name, "Duplicate local binding.");
                                if (binding.Annotation is { } annotation && !SupportedType(annotation.Text)) Fail("DS2008", annotation, "Unsupported local annotation.");
                                var initSyntax = binding.Expression ?? new ValueSyntax(binding.Initializer);
                                var init = BindExpression(initSyntax);
                                // A record expression retains its nominal type even when an
                                // initializer cannot complete. Later unreachable statements
                                // still need that type to be checked consistently.
                                var localType = binding.Annotation?.Text ?? (init.Type == "<never>" ? KnownType(init.Expression) ?? "Unit" : init.Type);
                                if (binding.Annotation is { } annotationType && init.Type != "<never>" && init.Type != localType)
                                {
                                    if (localType is "string" or "char") Fail("DS2008", annotationType, "Local annotation does not match initializer.");
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
                                var returned = BindExpression(ret.Value);
                                Require(ret.Value, returned.Type, function.ReturnType.Text);
                                boundStatements.Add(new BoundReturnStatement(Coerce(returned.Expression, function.ReturnType.Text, returned.Type)));
                                hasReturn = true; reachable = false; break;
                            case ExpressionStatementSyntax exprStatement:
                                var expr = BindExpression(exprStatement.Expression);
                                Require(exprStatement.Expression, expr.Type, "Unit");
                                boundStatements.Add(new BoundExpressionStatement(Coerce(expr.Expression, "Unit", expr.Type)));
                                if (reachable && expr.Type == "<never>") reachable = false;
                                break;
                        }
                    }
                    var final = BindExpression(block.Result);
                    var type = reachable ? final.Type : "<never>";
                    return (new BoundBlock(boundLocalsInBlock, final.Expression) { Statements = boundStatements, CanComplete = type != "<never>", EvaluateResult = reachable, ResultType = final.Type }, type);
                }
                finally { if (!keepLocals) foreach (var name in introduced) locals.Remove(name); }
            }
            var sourceBody = function.Body ?? new BlockSyntax(function.Bindings, function.Expression ?? new ValueSyntax(function.Literal));
            var body = BindBlock(sourceBody, true);
            if (body.Type != "<never>" && body.Type != function.ReturnType.Text && (function.ReturnType.Text is "string" or "char"))
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
                Expression = boundLocals.Count == 0 && result.Expression is BoundInt ? null : result.Expression
            });
        }
        return new(syntax.Name.Text, functions)
        {
            Records = syntax.Records.Select(record => new BoundRecordDeclaration(record.Name.Text,
                record.Fields.Select(field => new BoundRecordField(field.Name.Text, field.Type.Text)).ToArray())).ToArray(),
            Unions = syntax.Unions.Select(union => new BoundUnionDeclaration(union.Name.Text,
                union.Cases.Select(unionCase => new BoundUnionCase(unionCase.Name.Text,
                    unionCase.PayloadTypes.Select(type => type.Text).ToArray())).ToArray())).ToArray()
        };
    }
}

public static class CSharpEmitter
{
    public static string Emit(BoundModule module)
    {
        // A generated namespace avoids a C# member/type name collision without restricting D# names.
        var output = new StringBuilder("// Generated by DSharp.\nnamespace DSharp.Generated;\n\n");
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
        var recordTypes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var record in module.Records)
            recordTypes.Add(record.Name, UniqueGlobal("__DSharp_Record_" + recordTypes.Count.ToString(CultureInfo.InvariantCulture)));
        foreach (var union in module.Unions)
            recordTypes.Add(union.Name, UniqueGlobal("__DSharp_Union_" + recordTypes.Count.ToString(CultureInfo.InvariantCulture)));
        var container = "__DSharp_Functions";
        while (!globalNames.Add(container)) container += "_";
        var unitType = "__DSharp_Unit";
        while (!globalNames.Add(unitType)) unitType += "_";
        var signalType = "__DSharp_ReturnSignal";
        while (signalType == container || module.Functions.Any(f => f.Name == signalType)) signalType += "_";
        var raiseMethod = "__DSharp_Raise";
        while (raiseMethod == container || raiseMethod == signalType || module.Functions.Any(f => f.Name == raiseMethod)) raiseMethod += "_";
        var equalityMethods = new Dictionary<string, string>(StringComparer.Ordinal);
        var memberNames = new HashSet<string>(module.Functions.Select(f => f.Name), StringComparer.Ordinal) { signalType, raiseMethod };
        foreach (var record in module.Records)
        {
            var name = "__DSharp_RecordEqual_" + equalityMethods.Count.ToString(CultureInfo.InvariantCulture);
            while (!memberNames.Add(name)) name += "_";
            equalityMethods.Add(record.Name, name);
        }
        foreach (var union in module.Unions)
        {
            var name = "__DSharp_UnionEqual_" + equalityMethods.Count.ToString(CultureInfo.InvariantCulture);
            while (!memberNames.Add(name)) name += "_";
            equalityMethods.Add(union.Name, name);
        }
        string CType(string type) => type == "Unit" ? unitType : recordTypes.TryGetValue(type, out var recordType) ? recordType : type;
        output.Append("public readonly struct ").Append(unitType).Append(" {}\n\n");
        foreach (var record in module.Records)
        {
            output.Append("public sealed class ").Append(CType(record.Name)).Append("\n{\n");
            foreach (var field in record.Fields)
            {
                // C# object members can also be ordinary D# field names.
                var hidesObjectMember = field.Name is "Equals" or "GetHashCode" or "ToString" or "GetType" or "MemberwiseClone";
                output.Append("    public ").Append(hidesObjectMember ? "new " : "").Append(CType(field.Type)).Append(" @").Append(field.Name).Append(" { get; }\n");
            }
            output.Append("    public ").Append(CType(record.Name)).Append("(");
            for (var i = 0; i < record.Fields.Count; i++)
            {
                if (i != 0) output.Append(", ");
                output.Append(CType(record.Fields[i].Type)).Append(" __dsharp_field_").Append(i.ToString(CultureInfo.InvariantCulture));
            }
            output.Append(")\n    {\n");
            for (var i = 0; i < record.Fields.Count; i++)
                output.Append("        this.@").Append(record.Fields[i].Name).Append(" = __dsharp_field_").Append(i.ToString(CultureInfo.InvariantCulture)).Append(";\n");
            output.Append("    }\n}\n\n");
        }
        foreach (var union in module.Unions)
        {
            var type = CType(union.Name);
            output.Append("public sealed class ").Append(type).Append("\n{\n")
                .Append("    internal int __dsharp_tag { get; }\n")
                .Append("    private readonly object[] __dsharp_payload;\n")
                .Append("    private ").Append(type).Append("(int tag, object[] payload) { __dsharp_tag = tag; __dsharp_payload = payload; }\n");
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
            output.Append("}\n\n");
        }
        output.Append("public sealed class @").Append(module.Name).Append(" : ").Append(container)
            .Append("\n{\n    private @").Append(module.Name).Append("() {}\n}\n\npublic abstract class ")
            .Append(container).Append("\n{\n");
        if (module.Functions.Any(f => f.HasReturn))
        {
            output.Append("    private sealed class ").Append(signalType).Append("<T> : global::System.Exception\n")
                .Append("    {\n        public T Value { get; }\n        public ").Append(signalType).Append("(T value) { Value = value; }\n    }\n")
                .Append("    private static T ").Append(raiseMethod).Append("<T>(T value) => throw new ").Append(signalType).Append("<T>(value);\n");
        }
        foreach (var record in module.Records)
        {
            output.Append("    private static bool ").Append(equalityMethods[record.Name]).Append("(").Append(CType(record.Name)).Append(" __left, ")
                .Append(CType(record.Name)).Append(" __right) =>\n        global::System.Object.ReferenceEquals(__left, __right) ||\n        (!global::System.Object.ReferenceEquals(__left, null) && !global::System.Object.ReferenceEquals(__right, null)");
            foreach (var field in record.Fields)
            {
                var left = "__left.@" + field.Name; var right = "__right.@" + field.Name;
                var equals = field.Type == "Unit" ? "true" :
                    field.Type == "string" ? "global::System.String.Equals(" + left + ", " + right + ", global::System.StringComparison.Ordinal)" :
                    equalityMethods.TryGetValue(field.Type, out var nested) ? nested + "(" + left + ", " + right + ")" : left + " == " + right;
                output.Append(" && ").Append(equals);
            }
            output.Append(");\n");
        }
        foreach (var union in module.Unions)
        {
            output.Append("    private static bool ").Append(equalityMethods[union.Name]).Append("(").Append(CType(union.Name)).Append(" __left, ")
                .Append(CType(union.Name)).Append(" __right)\n    {\n")
                .Append("        if (global::System.Object.ReferenceEquals(__left, __right)) return true;\n")
                .Append("        if (global::System.Object.ReferenceEquals(__left, null) || global::System.Object.ReferenceEquals(__right, null) || __left.__dsharp_tag != __right.__dsharp_tag) return false;\n")
                .Append("        switch (__left.__dsharp_tag)\n        {\n");
            for (var caseIndex = 0; caseIndex < union.Cases.Count; caseIndex++)
            {
                var unionCase = union.Cases[caseIndex];
                var caseSuffix = caseIndex.ToString(CultureInfo.InvariantCulture);
                output.Append("            case ").Append(caseSuffix).Append(": return true");
                for (var i = 0; i < unionCase.PayloadTypes.Count; i++)
                {
                    var payloadType = unionCase.PayloadTypes[i];
                    var accessor = ".__dsharp_case_" + caseSuffix + "_item_" + i.ToString(CultureInfo.InvariantCulture);
                    var left = "__left" + accessor; var right = "__right" + accessor;
                    var equals = payloadType == "Unit" ? "true" :
                        payloadType == "string" ? "global::System.String.Equals(" + left + ", " + right + ", global::System.StringComparison.Ordinal)" :
                        equalityMethods.TryGetValue(payloadType, out var nested) ? nested + "(" + left + ", " + right + ")" : left + " == " + right;
                    output.Append(" && ").Append(equals);
                }
                output.Append(";\n");
            }
            output.Append("            default: return false;\n        }\n    }\n");
        }
        foreach (var function in module.Functions)
        {
            output.Append("    public static ").Append(CType(function.ReturnType)).Append(" @").Append(function.Name).Append("(");
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
            string PatternItem(BoundCasePattern pattern, int item, string value)
            {
                var union = module.Unions.Single(candidate => candidate.Name == pattern.Union);
                var caseIndex = union.Cases.ToList().FindIndex(candidate => candidate.Name == pattern.Case);
                return "(" + value + ").__dsharp_case_" + caseIndex.ToString(CultureInfo.InvariantCulture) +
                    "_item_" + item.ToString(CultureInfo.InvariantCulture);
            }
            string PatternCondition(BoundPattern pattern, string value)
            {
                switch (pattern)
                {
                    case BoundWildcardPattern or BoundBindingPattern:
                        // A nonconstant true expression keeps later arms and the
                        // defensive fallback reachable under C# warnings-as-errors.
                        return "global::System.Object.ReferenceEquals(null, null)";
                    case BoundLiteralPattern literal:
                        if (literal.Type == "Unit") return "global::System.Object.ReferenceEquals(null, null)";
                        var expected = EmitExpression(literal.Value);
                        return literal.Type == "string"
                            ? "global::System.String.Equals(" + value + ", " + expected + ", global::System.StringComparison.Ordinal)"
                            : "(" + value + " == " + expected + ")";
                    case BoundCasePattern unionCase:
                        var union = module.Unions.Single(candidate => candidate.Name == unionCase.Union);
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
                    case BoundRecordConstruction construction:
                        var definition = module.Records.Single(record => record.Name == construction.Type);
                        var fields = construction.Fields.Select(field =>
                        {
                            var index = definition.Fields.ToList().FindIndex(item => item.Name == field.Name);
                            return "__dsharp_field_" + index.ToString(CultureInfo.InvariantCulture) + ": " + EmitExpression(field.Value);
                        }).ToArray();
                        return "new " + CType(construction.Type) + "(" + string.Join(", ", fields) + ")";
                    case BoundFieldAccess access:
                        return "(" + EmitExpression(access.Receiver) + ").@" + access.Field;
                    case BoundRecordUpdate update:
                        var updateDefinition = module.Records.Single(record => record.Name == update.Type);
                        var updateHelper = "__dsharp_update_" + (updateCount++).ToString(CultureInfo.InvariantCulture);
                        output.Append("            ").Append(CType(update.Type)).Append(" ").Append(updateHelper).Append("(").Append(CType(update.Type)).Append(" __original");
                        for (var i = 0; i < update.Fields.Count; i++)
                        {
                            var field = updateDefinition.Fields.Single(item => item.Name == update.Fields[i].Name);
                            output.Append(", ").Append(CType(field.Type)).Append(" __update_").Append(i.ToString(CultureInfo.InvariantCulture));
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
                        var unionDefinition = module.Unions.Single(union => union.Name == unionConstruction.Type);
                        var unionCaseIndex = unionDefinition.Cases.ToList().FindIndex(unionCase => unionCase.Name == unionConstruction.Case);
                        var unionArguments = unionConstruction.Arguments.Select(EmitExpression).ToArray();
                        return CType(unionConstruction.Type) + ".__dsharp_case_" + unionCaseIndex.ToString(CultureInfo.InvariantCulture) + "(" + string.Join(", ", unionArguments) + ")";
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
                        if (equalityMethods.TryGetValue(binary.OperandType, out var equality) && (binary.Operator is "==" or "!="))
                        {
                            var equals = equality + "(" + left + ", " + right + ")";
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
                            output.Append("            _ = ").Append(raiseMethod).Append("<").Append(CType(function.ReturnType)).Append(">(").Append(returned).Append(");\n");
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
