using System.Globalization;
using System.Text;

namespace DSharp.Compiler;

public sealed record SourceSpan(int Start, int Length, int Line, int Column);
public sealed record Diagnostic(string Code, string Path, SourceSpan Span, string Message);
public sealed record Compilation(string? CSharp, IReadOnlyList<Diagnostic> Diagnostics)
{
    public IReadOnlyList<BoundActorDeclaration> Actors { get; init; } = Array.Empty<BoundActorDeclaration>();
    public bool RequiresRuntime { get; init; }
}
public sealed record SourceFile(string Path, string Text);
public sealed record ModuleExport(string Module, string Name, string InternalName);
public sealed record ModuleResolution(ModuleSyntax Unified, IReadOnlyList<ModuleExport> Exports);
public sealed record Token(string Text, SourceSpan Span)
{
    // Source origin is diagnostic metadata. Preserve the public token value
    // equality used by existing lexer callers when recording file paths.
    public string? OriginPath { get; init; }
    public bool IsDocumentation { get; init; }
    public bool HasInterveningComment { get; init; }
    public bool HasCodeBeforeOnLine { get; init; }
    public bool Equals(Token? other) => other is not null && Text == other.Text &&
        Span == other.Span && IsDocumentation == other.IsDocumentation &&
        HasInterveningComment == other.HasInterveningComment &&
        HasCodeBeforeOnLine == other.HasCodeBeforeOnLine;
    public override int GetHashCode() => HashCode.Combine(Text, Span, IsDocumentation,
        HasInterveningComment, HasCodeBeforeOnLine);
}
public abstract record ExpressionSyntax(Token Token);
public abstract record StatementSyntax;
public sealed record LetStatementSyntax(LetBindingSyntax Binding) : StatementSyntax;
public sealed record TupleBindingSyntax(Token Token, IReadOnlyList<TupleBindingSyntax> Elements)
{
    public IEnumerable<Token> Names => Elements.Count == 0 ? new[] { Token } : Elements.SelectMany(element => element.Names);
}
public sealed record TupleLetStatementSyntax(TupleBindingSyntax Pattern, Token? Annotation, ExpressionSyntax Value) : StatementSyntax;
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
public sealed record ForSyntax(Token Keyword, Token Name, ExpressionSyntax Source, BlockSyntax Body) : ExpressionSyntax(Keyword);
public sealed record UsingSyntax(Token Keyword, Token Name, ExpressionSyntax Acquisition, BlockSyntax Body) : ExpressionSyntax(Keyword);
public sealed record GroupSyntax(Token Open, ExpressionSyntax Inner) : ExpressionSyntax(Open);
public sealed record TupleSyntax(Token Open, IReadOnlyList<ExpressionSyntax> Elements) : ExpressionSyntax(Open);
public sealed record ListSyntax(Token Open, IReadOnlyList<ExpressionSyntax> Elements) : ExpressionSyntax(Open);
public sealed record ValueSyntax(Token Value) : ExpressionSyntax(Value);
public sealed record InterpolationSyntax(Token Start, IReadOnlyList<Token> Texts, IReadOnlyList<ExpressionSyntax> Holes) : ExpressionSyntax(Start);
public sealed record LambdaParameterSyntax(Token Name, Token? Type);
public sealed record LambdaSyntax(Token Start, IReadOnlyList<LambdaParameterSyntax> Parameters, BlockSyntax Body) : ExpressionSyntax(Start)
{
    public bool IsAsync { get; init; }
}
public sealed record InvokeSyntax(ExpressionSyntax Receiver, Token OpenParen, IReadOnlyList<ExpressionSyntax> Arguments) : ExpressionSyntax(OpenParen);
public sealed record CallSyntax(Token Name, Token OpenParen, IReadOnlyList<ExpressionSyntax> Arguments) : ExpressionSyntax(Name)
{
    public IReadOnlyList<Token> TypeArguments { get; init; } = Array.Empty<Token>();
}
public sealed record MemberCallSyntax(ExpressionSyntax Receiver, Token Member, Token OpenParen, IReadOnlyList<ExpressionSyntax> Arguments) : ExpressionSyntax(Member)
{
    public IReadOnlyList<Token> TypeArguments { get; init; } = Array.Empty<Token>();
    public IReadOnlyList<string>? ExtensionCandidates { get; init; }
}
public sealed record ConditionalMemberCallSyntax(ExpressionSyntax Receiver, Token Operator, Token Member, Token OpenParen, IReadOnlyList<ExpressionSyntax> Arguments) : ExpressionSyntax(Operator)
{
    public IReadOnlyList<Token> TypeArguments { get; init; } = Array.Empty<Token>();
    public IReadOnlyList<string>? ExtensionCandidates { get; init; }
}
public sealed record RecordFieldSyntax(Token Name, Token Type);
public sealed record RecordDeclarationSyntax(Token Name, IReadOnlyList<RecordFieldSyntax> Fields)
{
    public IReadOnlyList<Token> DocumentationComments { get; init; } = Array.Empty<Token>();
    public IReadOnlyList<Token> TypeParameters { get; init; } = Array.Empty<Token>();
}
public sealed record RecordInitializerSyntax(Token Name, ExpressionSyntax Value);
public sealed record RecordConstructionSyntax(Token Name, IReadOnlyList<RecordInitializerSyntax> Fields) : ExpressionSyntax(Name);
public sealed record QualifiedRecordConstructionSyntax(ExpressionSyntax Receiver, Token Name, IReadOnlyList<RecordInitializerSyntax> Fields) : ExpressionSyntax(Name);
public sealed record FieldAccessSyntax(ExpressionSyntax Receiver, Token Field) : ExpressionSyntax(Field)
{
    public IReadOnlyList<string>? ExtensionCandidates { get; init; }
}
public sealed record ConditionalFieldAccessSyntax(ExpressionSyntax Receiver, Token Operator, Token Field) : ExpressionSyntax(Operator);
public sealed record RecordUpdateSyntax(ExpressionSyntax Receiver, Token With, IReadOnlyList<RecordInitializerSyntax> Fields) : ExpressionSyntax(With);
public sealed record UnionCaseSyntax(Token Name, IReadOnlyList<Token> PayloadTypes);
public sealed record UnionDeclarationSyntax(Token Name, IReadOnlyList<UnionCaseSyntax> Cases)
{
    public IReadOnlyList<Token> DocumentationComments { get; init; } = Array.Empty<Token>();
    public IReadOnlyList<Token> TypeParameters { get; init; } = Array.Empty<Token>();
}
public sealed record ParameterSyntax(Token Name, Token Type);
public sealed record InterfaceMethodSyntax(Token Name, IReadOnlyList<ParameterSyntax> Parameters, Token ReturnType, bool IsAsync)
{
    public IReadOnlyList<Token> DocumentationComments { get; init; } = Array.Empty<Token>();
    public BlockSyntax? Body { get; init; }
}
public sealed record InterfaceDeclarationSyntax(Token Name, IReadOnlyList<InterfaceMethodSyntax> Methods)
{
    public IReadOnlyList<Token> DocumentationComments { get; init; } = Array.Empty<Token>();
}
public sealed record ServiceMethodSyntax(Token Name, IReadOnlyList<LambdaParameterSyntax> Parameters, Token? ReturnType, bool IsAsync, BlockSyntax Body)
{
    public IReadOnlyList<Token> DocumentationComments { get; init; } = Array.Empty<Token>();
}
public sealed record ServiceDeclarationSyntax(Token Name, IReadOnlyList<ParameterSyntax> Dependencies, IReadOnlyList<Token> Interfaces, IReadOnlyList<ServiceMethodSyntax> Methods)
{
    public IReadOnlyList<Token> DocumentationComments { get; init; } = Array.Empty<Token>();
}
public sealed record ResourceDeclarationSyntax(Token Name, IReadOnlyList<ParameterSyntax> Dependencies, IReadOnlyList<ServiceMethodSyntax> Methods)
{
    public IReadOnlyList<Token> DocumentationComments { get; init; } = Array.Empty<Token>();
}
public sealed record ActorMethodSyntax(Token Name, IReadOnlyList<ParameterSyntax> Parameters, Token ReturnType, bool IsAsync, BlockSyntax Body)
{
    public IReadOnlyList<Token> DocumentationComments { get; init; } = Array.Empty<Token>();
}
public sealed record ActorQueueBindingSyntax(Token Keyword, Token MessageType, Token QueueName)
{
    public IReadOnlyList<Token> DocumentationComments { get; init; } = Array.Empty<Token>();
}
public sealed record ActorEventBindingSyntax(Token Keyword, Token MessageType)
{
    public IReadOnlyList<Token> DocumentationComments { get; init; } = Array.Empty<Token>();
}
public sealed record ActorCronBindingSyntax(Token Keyword, Token MessageType, Token ScheduleName, Token Expression)
{
    public IReadOnlyList<Token> DocumentationComments { get; init; } = Array.Empty<Token>();
}
public sealed record ActorHttpBindingSyntax(Token Keyword, Token ResponseType, Token Binder, Token BindingMapper, Token SuccessMapper, Token BusinessMapper)
{
    public IReadOnlyList<Token> DocumentationComments { get; init; } = Array.Empty<Token>();
    internal string? BinderIdentity { get; init; }
    internal string? BindingMapperIdentity { get; init; }
    internal string? SuccessMapperIdentity { get; init; }
    internal string? BusinessMapperIdentity { get; init; }
    internal bool ReferencesResolved { get; init; }
}
public sealed record ActorHttpPlainBindingSyntax(Token Keyword, Token ResponseType, Token Binder, Token BindingMapper, Token SuccessMapper)
{
    public IReadOnlyList<Token> DocumentationComments { get; init; } = Array.Empty<Token>();
    internal string? BinderIdentity { get; init; }
    internal string? BindingMapperIdentity { get; init; }
    internal string? SuccessMapperIdentity { get; init; }
    internal bool ReferencesResolved { get; init; }
}
public sealed record ActorDeclarationSyntax(Token Name, IReadOnlyList<ParameterSyntax> Dependencies, IReadOnlyList<ActorMethodSyntax> Methods)
{
    public IReadOnlyList<Token> DocumentationComments { get; init; } = Array.Empty<Token>();
    public IReadOnlyList<ActorQueueBindingSyntax> QueueBindings { get; init; } = Array.Empty<ActorQueueBindingSyntax>();
    public IReadOnlyList<ActorEventBindingSyntax> EventBindings { get; init; } = Array.Empty<ActorEventBindingSyntax>();
    public IReadOnlyList<ActorCronBindingSyntax> CronBindings { get; init; } = Array.Empty<ActorCronBindingSyntax>();
    public IReadOnlyList<ActorHttpBindingSyntax> HttpBindings { get; init; } = Array.Empty<ActorHttpBindingSyntax>();
    public IReadOnlyList<ActorHttpPlainBindingSyntax> HttpPlainBindings { get; init; } = Array.Empty<ActorHttpPlainBindingSyntax>();
}
public sealed record ExtensionMethodSyntax(Token Name, IReadOnlyList<ParameterSyntax> Parameters, Token ReturnType, bool IsAsync, BlockSyntax Body)
{
    public IReadOnlyList<Token> DocumentationComments { get; init; } = Array.Empty<Token>();
    public Token? SelfAnnotation { get; init; }
    public string? InternalName { get; init; }
    public string? OriginModule { get; init; }
}
public sealed record ExtensionDeclarationSyntax(Token Target, IReadOnlyList<ExtensionMethodSyntax> Methods)
{
    public IReadOnlyList<Token> DocumentationComments { get; init; } = Array.Empty<Token>();
}
public sealed record UnarySyntax(Token Operator, ExpressionSyntax Operand) : ExpressionSyntax(Operator);
public sealed record AwaitSyntax(Token Keyword, ExpressionSyntax Operand) : ExpressionSyntax(Keyword);
public sealed record BinarySyntax(ExpressionSyntax Left, Token Operator, ExpressionSyntax Right) : ExpressionSyntax(Operator);
public sealed record CoalesceSyntax(ExpressionSyntax Left, Token Operator, ExpressionSyntax Right) : ExpressionSyntax(Operator);
public sealed record PipeSyntax(ExpressionSyntax Left, Token Operator, ExpressionSyntax Right) : ExpressionSyntax(Operator);
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
public sealed record BoundTuple(IReadOnlyList<BoundExpression> Elements, string Type) : BoundExpression;
public sealed record BoundTupleItem(BoundExpression Tuple, int Index) : BoundExpression;
public sealed record BoundList(IReadOnlyList<BoundExpression> Elements, string Type) : BoundExpression;
public sealed record BoundListOperation(BoundExpression Receiver, string ReceiverType, string Operation, IReadOnlyList<BoundExpression> Arguments, string Type) : BoundExpression;
public sealed record BoundCollectionMethodPartial(BoundExpression Receiver, string ReceiverType, string Operation, IReadOnlyList<string> ParameterTypes, string ReturnType, string Type) : BoundExpression
{
    public IReadOnlyList<string> TypeArguments { get; init; } = Array.Empty<string>();
}
public sealed record BoundListFoldPartial(BoundExpression Receiver, string ReceiverType, BoundExpression Initial, string AccumulatorType, string ElementType, string Type) : BoundExpression;
public sealed record BoundMapSetFactory(string Type, string Operation, IReadOnlyList<BoundExpression> Arguments) : BoundExpression;
public sealed record BoundMapSetOperation(BoundExpression Receiver, string ReceiverType, string Operation, IReadOnlyList<BoundExpression> Arguments, string Type) : BoundExpression;
public sealed record BoundMapSetPartial(BoundExpression Receiver, string ReceiverType, BoundExpression Key, string KeyType, string ValueType, string Type) : BoundExpression;
public sealed record BoundBuilderFactory(string Type, string CollectionType) : BoundExpression;
public sealed record BoundBuilderOperation(BoundExpression Receiver, string ReceiverType, string Operation, IReadOnlyList<BoundExpression> Arguments, string Type) : BoundExpression;
public sealed record BoundServiceConstruction(string Type, IReadOnlyList<BoundExpression> Arguments) : BoundExpression;
public sealed record BoundServiceCall(BoundExpression Receiver, string ReceiverType, string Method, IReadOnlyList<BoundExpression> Arguments, string Type) : BoundExpression;
public sealed record BoundServiceConversion(BoundExpression Expression, string Type) : BoundExpression;
public sealed record BoundNull(string Type) : BoundExpression;
public sealed record BoundAwait(BoundExpression Operand, string Type) : BoundExpression;
public sealed record BoundActorInvocation(string ActorIdentity, BoundExpression Message, string InputType, string ResultType) : BoundExpression;
public sealed record BoundActorDispatch(string ActorIdentity, BoundExpression Message, string InputType, string ResultType) : BoundExpression;
public sealed record BoundPublication(BoundExpression Message, string MessageType) : BoundExpression;
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
public sealed record BoundFor(string Name, BoundExpression Source, string SourceType, string ElementType, BoundBlock Body) : BoundExpression;
public sealed record BoundUsing(string Name, BoundExpression Acquisition, string ResourceType, BoundBlock Body, string Type, bool AsyncRelease) : BoundExpression;
public sealed record BoundInt(int Value) : BoundExpression;
public sealed record BoundInteger(ulong Magnitude, string Type) : BoundExpression;
public sealed record BoundDecimal(uint Low, uint Middle, uint High, byte Scale) : BoundExpression;
public sealed record BoundFloat32(uint Bits) : BoundExpression;
public sealed record BoundFloat64(ulong Bits) : BoundExpression;
public sealed record BoundBool(bool Value) : BoundExpression;
public sealed record BoundText(string Value, bool IsChar) : BoundExpression;
public abstract record BoundInterpolationPart;
public sealed record BoundInterpolationText(string Value) : BoundInterpolationPart;
public sealed record BoundInterpolationHole(BoundExpression Value, string Type) : BoundInterpolationPart;
public sealed record BoundInterpolation(IReadOnlyList<BoundInterpolationPart> Parts) : BoundExpression;
public sealed record BoundReference(string Name) : BoundExpression;
public sealed record BoundCapture(string Name, string Type);
public sealed record BoundLambda(IReadOnlyList<string> Parameters, IReadOnlyList<string> ParameterTypes, string Type, string ReturnType, BoundBlock Body, bool HasReturn) : BoundExpression
{
    public IReadOnlyList<BoundCapture> Captures { get; init; } = Array.Empty<BoundCapture>();
    public bool IsAsync { get; init; }
}
public sealed record BoundInvoke(BoundExpression Receiver, IReadOnlyList<BoundExpression> Arguments, string Type) : BoundExpression;
public sealed record BoundPipe(BoundExpression Value, string ValueType, BoundExpression Receiver, string Type) : BoundExpression
{
    public string ReceiverType { get; init; } = "";
}
public sealed record BoundFunctionReference(string Name, string Type, IReadOnlyList<string> TypeArguments) : BoundExpression
{
    public Token? Location { get; init; }
}
public sealed record BoundPartial(BoundExpression Receiver, string ReceiverType, IReadOnlyList<BoundExpression> Arguments,
    IReadOnlyList<string> ArgumentTypes, IReadOnlyList<string> RemainingTypes, string ReturnType, string Type) : BoundExpression;
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
public sealed record BoundConditionalCall(BoundExpression Receiver, string ReceiverType, string ReceiverName,
    BoundExpression Call, string Type) : BoundExpression;
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
public sealed record BoundUnary(string Operator, BoundExpression Operand) : BoundExpression
{
    public string OperandType { get; init; } = "int";
}
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
    public bool InferReturnType { get; init; }
    public bool IsAsync { get; init; }
}
public sealed record ModuleSyntax(Token Name, IReadOnlyList<FunctionSyntax> Functions)
{
    public IReadOnlyList<Token> Imports { get; init; } = Array.Empty<Token>();
    public IReadOnlyList<Token> DocumentationComments { get; init; } = Array.Empty<Token>();
    public IReadOnlyList<RecordDeclarationSyntax> Records { get; init; } = Array.Empty<RecordDeclarationSyntax>();
    public IReadOnlyList<UnionDeclarationSyntax> Unions { get; init; } = Array.Empty<UnionDeclarationSyntax>();
    public IReadOnlyList<InterfaceDeclarationSyntax> Interfaces { get; init; } = Array.Empty<InterfaceDeclarationSyntax>();
    public IReadOnlyList<ServiceDeclarationSyntax> Services { get; init; } = Array.Empty<ServiceDeclarationSyntax>();
    public IReadOnlyList<ResourceDeclarationSyntax> Resources { get; init; } = Array.Empty<ResourceDeclarationSyntax>();
    public IReadOnlyList<ActorDeclarationSyntax> Actors { get; init; } = Array.Empty<ActorDeclarationSyntax>();
    public IReadOnlyList<ExtensionDeclarationSyntax> Extensions { get; init; } = Array.Empty<ExtensionDeclarationSyntax>();
    // Resolver-only metadata for source-facing diagnostics. Bound nominal
    // identities stay opaque and distinct even when source names overlap. A
    // shared empty map preserves existing equality for directly parsed modules.
    private static readonly IReadOnlyDictionary<string, string> EmptyTypeDisplayNames = new Dictionary<string, string>(StringComparer.Ordinal);
    internal IReadOnlyDictionary<string, string> TypeDisplayNames { get; init; } = EmptyTypeDisplayNames;
    internal IReadOnlyDictionary<string, string> ActorTypeDisplayNames { get; init; } = EmptyTypeDisplayNames;
}
public sealed record BoundLocal(string Name, int Value, string? InitializerLocal)
{
    public string Type { get; init; } = "int";
    public BoundExpression? Expression { get; init; }
}
public sealed record BoundFunction(string Name, int Value)
{
    public bool RequiresDispatcher { get; init; }
    public bool IsOwnedFactory { get; init; }
    public bool IsActorBody { get; init; }
    public IReadOnlyList<string> TypeParameters { get; init; } = Array.Empty<string>();
    public string ReturnType { get; init; } = "int";
    public BoundBlock? Body { get; init; }
    public bool HasReturn { get; init; }
    public IReadOnlyList<string> ParameterTypes { get; init; } = Array.Empty<string>();
    public BoundExpression? Expression { get; init; }
    public IReadOnlyList<string> Parameters { get; init; } = Array.Empty<string>();
    public IReadOnlyList<BoundLocal> Locals { get; init; } = Array.Empty<BoundLocal>();
    public string? ResultLocal { get; init; }
    public bool IsAsync { get; init; }
}
public sealed record BoundInterfaceMethod(string Name, IReadOnlyList<string> ParameterTypes, string ReturnType, bool IsAsync)
{
    public bool IsOwnedFactory { get; init; }
}
public sealed record BoundInterfaceDeclaration(string Name, IReadOnlyList<BoundInterfaceMethod> Methods);
public sealed record BoundServiceMethod(string Name, string FunctionName, IReadOnlyList<string> ParameterTypes, string ReturnType, bool IsAsync)
{
    public string? DefaultInterface { get; init; }
    public bool IsOwnedFactory { get; init; }
}
public sealed record BoundServiceDeclaration(string Name, IReadOnlyList<string> DependencyTypes, IReadOnlyList<string> Interfaces, IReadOnlyList<BoundServiceMethod> Methods);
public sealed record BoundActorDependency(string Name, string Type);
public sealed record BoundQueueBinding(string QueueName, string MessageType, string SourcePath,
    SourceSpan TriggerSpan, SourceSpan TypeSpan, SourceSpan QueueNameSpan)
{
    // This nominal identity is local to one resolved compilation, not a wire schema.
    internal string TypeIdentity { get; init; } = "";
}
public sealed record BoundEventBinding(string MessageType, string SourcePath, SourceSpan TriggerSpan, SourceSpan TypeSpan)
{
    // This identity is scoped to the compilation; it is not a subscription or wire schema.
    internal string TypeIdentity { get; init; } = "";
}
public sealed record BoundCronBinding(string ScheduleName, string Expression, string MessageType, string SourcePath,
    SourceSpan TriggerSpan, SourceSpan TypeSpan, SourceSpan NameSpan, SourceSpan ExpressionSpan)
{
    // This compile-local identity is neither a registration nor a wire schema.
    internal string TypeIdentity { get; init; } = "";
}
public sealed record BoundHttpBinding(string ResponseType, string Binder, string BindingMapper, string SuccessMapper, string BusinessMapper,
    string BindingErrorType, string ValueType, string BusinessErrorType, bool IsAsyncBinder, string SourcePath,
    SourceSpan TriggerSpan, SourceSpan ResponseSpan, SourceSpan BinderSpan, SourceSpan BindingMapperSpan,
    SourceSpan SuccessMapperSpan, SourceSpan BusinessMapperSpan)
{
    internal string BinderIdentity { get; init; } = "";
    internal string BindingMapperIdentity { get; init; } = "";
    internal string SuccessMapperIdentity { get; init; } = "";
    internal string BusinessMapperIdentity { get; init; } = "";
}
public sealed record BoundHttpPlainBinding(string ResponseType, string Binder, string BindingMapper, string SuccessMapper,
    string BindingErrorType, string ValueType, bool IsAsyncBinder, string SourcePath,
    SourceSpan TriggerSpan, SourceSpan ResponseSpan, SourceSpan BinderSpan, SourceSpan BindingMapperSpan, SourceSpan SuccessMapperSpan)
{
    internal string BinderIdentity { get; init; } = "";
    internal string BindingMapperIdentity { get; init; } = "";
    internal string SuccessMapperIdentity { get; init; } = "";
}
public sealed record BoundActorDeclaration(string Name, string InputType, string ResultType,
    IReadOnlyList<BoundActorDependency> Dependencies, string SourcePath, SourceSpan NameSpan,
    SourceSpan HandleSpan, string ImplementationName)
{
    // Describes the internal Handle-body signature; this is not registration
    // or a production actor-executor contract.
    public bool RequiresDispatcher { get; init; }
    public IReadOnlyList<BoundQueueBinding> QueueBindings { get; init; } = Array.Empty<BoundQueueBinding>();
    public IReadOnlyList<BoundEventBinding> EventBindings { get; init; } = Array.Empty<BoundEventBinding>();
    public IReadOnlyList<BoundCronBinding> CronBindings { get; init; } = Array.Empty<BoundCronBinding>();
    public IReadOnlyList<BoundHttpBinding> HttpBindings { get; init; } = Array.Empty<BoundHttpBinding>();
    public IReadOnlyList<BoundHttpPlainBinding> HttpPlainBindings { get; init; } = Array.Empty<BoundHttpPlainBinding>();
}
public sealed record BoundModule(string Name, IReadOnlyList<BoundFunction> Functions)
{
    public IReadOnlyList<BoundActorRoute> ActorRoutes { get; init; } = Array.Empty<BoundActorRoute>();
    public IReadOnlyList<BoundMessageContract> MessageContracts { get; init; } = Array.Empty<BoundMessageContract>();
    internal string? InvocationReceiptSymbol { get; init; }
    internal string? PublicationReceiptSymbol { get; init; }
    public IReadOnlyList<BoundRecordDeclaration> Records { get; init; } = Array.Empty<BoundRecordDeclaration>();
    public IReadOnlyList<BoundUnionDeclaration> Unions { get; init; } = Array.Empty<BoundUnionDeclaration>();
    public string? ListSymbol { get; init; }
    public string? OptionSymbol { get; init; }
    public string? MapSymbol { get; init; }
    public string? SetSymbol { get; init; }
    public string? MutableListSymbol { get; init; }
    public string? MutableMapSymbol { get; init; }
    public string? TaskSymbol { get; init; }
    public IReadOnlyList<BoundInterfaceDeclaration> Interfaces { get; init; } = Array.Empty<BoundInterfaceDeclaration>();
    public IReadOnlyList<BoundServiceDeclaration> Services { get; init; } = Array.Empty<BoundServiceDeclaration>();
    public IReadOnlyList<BoundActorDeclaration> Actors { get; init; } = Array.Empty<BoundActorDeclaration>();
}
public sealed record BoundActorRoute(string ActorIdentity, string Key, string InputType, string ResultType);
public sealed record BoundMessageContract(string MessageType, string Key);

public sealed class SourceError(Diagnostic diagnostic) : Exception(diagnostic.Message)
{
    public Diagnostic Diagnostic { get; } = diagnostic;
}

// Integer tokens represent a magnitude, not a signed or two's-complement bit
// pattern. Validate separators against digits in the selected base and avoid
// overflowing while accumulating even for an adversarially long token.
internal static class IntegerLiterals
{
    internal static bool TryParse(string text, out ulong magnitude, out bool isLong)
    {
        magnitude = 0;
        isLong = text.EndsWith('L');
        var end = isLong ? text.Length - 1 : text.Length;
        var start = 0;
        uint radix = 10;
        if (end >= 2 && text[0] == '0' && text[1] is 'x' or 'b')
        {
            radix = text[1] == 'x' ? 16U : 2U;
            start = 2;
        }
        if (start == end) return false;
        var previousDigit = false;
        for (var i = start; i < end; i++)
        {
            var c = text[i];
            if (c == '_')
            {
                if (!previousDigit || i + 1 == end) return false;
                previousDigit = false;
                continue;
            }
            var digit = c is >= '0' and <= '9' ? c - '0' :
                c is >= 'a' and <= 'f' ? c - 'a' + 10 :
                c is >= 'A' and <= 'F' ? c - 'A' + 10 : -1;
            if (digit < 0 || digit >= radix || magnitude > (ulong.MaxValue - (uint)digit) / radix) return false;
            magnitude = magnitude * radix + (uint)digit;
            previousDigit = true;
        }
        return previousDigit;
    }
}

// A decimal token denotes an exact 96-bit coefficient and a scale from 0 to
// 28. Work with digits and a bounded exponent before constructing a BigInteger,
// so neither very long tokens nor exponents can cause unbounded powers of ten.
internal static class DecimalLiterals
{
    internal static bool TryParse(string text, out BoundDecimal value)
    {
        value = new(0, 0, 0, 0);
        if (text.Length < 2 || text[^1] != 'm') return false;
        var end = text.Length - 1;
        var index = 0;
        var digits = new StringBuilder();
        bool ReadDigits(StringBuilder output, out int count)
        {
            count = 0;
            var previousDigit = false;
            while (index < end && (char.IsAsciiDigit(text[index]) || text[index] == '_'))
            {
                var c = text[index++];
                if (c == '_')
                {
                    if (!previousDigit || index == end || !char.IsAsciiDigit(text[index])) return false;
                    previousDigit = false;
                }
                else { output.Append(c); count++; previousDigit = true; }
            }
            return previousDigit;
        }
        if (!ReadDigits(digits, out _)) return false;
        var fractionLength = 0;
        if (index < end && text[index] == '.')
        {
            index++;
            if (!ReadDigits(digits, out fractionLength)) return false;
        }
        long exponent = 0;
        if (index < end && text[index] is 'e' or 'E')
        {
            index++;
            var negative = index < end && text[index] == '-';
            if (index < end && text[index] is '+' or '-') index++;
            var exponentDigits = new StringBuilder();
            if (!ReadDigits(exponentDigits, out _)) return false;
            foreach (var digit in exponentDigits.ToString())
                exponent = Math.Min(1_000_000_000L, exponent * 10 + digit - '0');
            if (negative) exponent = -exponent;
        }
        if (index != end) return false;
        var coefficient = digits.ToString().TrimStart('0');
        if (coefficient.Length == 0) return true;
        long scale = (long)fractionLength - exponent;
        var last = coefficient.Length;
        while (scale > 0 && last > 0 && coefficient[last - 1] == '0') { last--; scale--; }
        coefficient = coefficient[..last];
        if (scale > 28) return false;
        if (scale < 0)
        {
            if (-scale > 29 || coefficient.Length - scale > 29) return false;
            coefficient += new string('0', (int)-scale);
            scale = 0;
        }
        if (coefficient.Length > 29 || !System.Numerics.BigInteger.TryParse(coefficient,
            NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number > (System.Numerics.BigInteger.One << 96) - 1) return false;
        value = new((uint)(number & uint.MaxValue), (uint)((number >> 32) & uint.MaxValue),
            (uint)((number >> 64) & uint.MaxValue), (byte)scale);
        return true;
    }
}

// Scan an interpolation as one lexical token while still recognizing nested
// expressions, comments and interpolated strings inside its holes. Offsets are
// relative to the supplied source; OriginSpan maps them back to the real file.
internal static class InterpolationScanner
{
    internal readonly record struct Part(int TextStart, int TextEnd, int HoleStart, int HoleEnd);
    internal readonly record struct ScanResult(int End, IReadOnlyList<Part> Parts);

    internal static SourceSpan OriginSpan(string source, int offset, int length, int originStart, int originLine, int originColumn)
    {
        var line = originLine; var column = originColumn;
        for (var i = 0; i < offset; i++)
        {
            if (source[i] == '\n' || (source[i] == '\r' && (i + 1 == source.Length || source[i + 1] != '\n'))) { line++; column = 1; }
            else column++;
        }
        return new(originStart + offset, length, line, column);
    }

    internal static ScanResult Scan(string source, int start, string path, int originStart, int originLine, int originColumn)
    {
        SourceError Error(int at, string message) => new(new("DS1001", path,
            OriginSpan(source, at, at < source.Length ? 1 : 0, originStart, originLine, originColumn), message));

        int Quoted(int cursor)
        {
            var quote = source[cursor++];
            while (cursor < source.Length)
            {
                if (source[cursor] == quote) return cursor + 1;
                if (source[cursor] is '\r' or '\n') throw Error(cursor, "Text literals cannot span lines.");
                if (source[cursor] == '\\')
                {
                    cursor++;
                    if (cursor >= source.Length || source[cursor] is '\r' or '\n') throw Error(cursor, "Unterminated text escape.");
                }
                cursor++;
            }
            throw Error(cursor, "Unterminated text literal in interpolation.");
        }

        ScanResult Interpolated(int begin)
        {
            var parts = new List<Part>();
            var cursor = begin + 2;
            var textStart = cursor;
            while (cursor < source.Length)
            {
                var c = source[cursor];
                if (c == '"')
                {
                    parts.Add(new(textStart, cursor, -1, -1));
                    return new(cursor + 1, parts);
                }
                if (c is '\r' or '\n') throw Error(cursor, "Text literals cannot span lines.");
                if (c == '\\')
                {
                    cursor++;
                    if (cursor >= source.Length || source[cursor] is '\r' or '\n') throw Error(cursor, "Unterminated text escape.");
                    cursor++; continue;
                }
                if (c == '}' && cursor + 1 < source.Length && source[cursor + 1] == '}') { cursor += 2; continue; }
                if (c == '}') throw Error(cursor, "A literal '}' in an interpolated string must be escaped as '}}'.");
                if (c != '{') { cursor++; continue; }
                if (cursor + 1 < source.Length && source[cursor + 1] == '{') { cursor += 2; continue; }
                var textEnd = cursor;
                var holeStart = ++cursor;
                var depth = 0;
                while (cursor < source.Length)
                {
                    c = source[cursor];
                    if (c == '$' && cursor + 1 < source.Length && source[cursor + 1] == '"')
                    { cursor = Interpolated(cursor).End; continue; }
                    if (c is '"' or '\'') { cursor = Quoted(cursor); continue; }
                    if (c == '/' && cursor + 1 < source.Length && source[cursor + 1] == '/')
                    {
                        cursor += 2;
                        while (cursor < source.Length && source[cursor] is not ('\r' or '\n')) cursor++;
                        continue;
                    }
                    if (c == '/' && cursor + 1 < source.Length && source[cursor + 1] == '*')
                    {
                        var comment = cursor; cursor += 2;
                        while (cursor < source.Length && !(source[cursor] == '*' && cursor + 1 < source.Length && source[cursor + 1] == '/'))
                        {
                            if (source[cursor] == '/' && cursor + 1 < source.Length && source[cursor + 1] == '*')
                                throw Error(cursor, "Nested block comments are not supported.");
                            cursor++;
                        }
                        if (cursor == source.Length) throw Error(comment, "Unterminated block comment in interpolation.");
                        cursor += 2; continue;
                    }
                    if (c == '{') { depth++; cursor++; continue; }
                    if (c == '}')
                    {
                        if (depth == 0) break;
                        depth--; cursor++; continue;
                    }
                    cursor++;
                }
                if (cursor == source.Length) throw Error(cursor, "Unterminated interpolation expression.");
                parts.Add(new(textStart, textEnd, holeStart, cursor));
                textStart = ++cursor;
            }
            throw Error(cursor, "Unterminated interpolated string.");
        }
        return Interpolated(start);
    }
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
            if (c == '$' && i + 1 < source.Length && source[i + 1] == '\"')
            {
                var scanned = InterpolationScanner.Scan(source, start, path, 0, 1, 1);
                var end = scanned.End;
                while (i < end) Advance();
            }
            else if (c is '\"' or '\'')
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
            {
                if (c == '0' && i + 1 < source.Length && source[i + 1] is 'x' or 'b')
                    do { Advance(); } while (i < source.Length && (char.IsAsciiLetterOrDigit(source[i]) || source[i] == '_'));
                else
                {
                    do { Advance(); } while (i < source.Length && (char.IsAsciiDigit(source[i]) || source[i] == '_'));
                    // A dot begins a fractional literal only when followed by
                    // a digit; 40.add remains integer member access.
                    if (i + 1 < source.Length && source[i] == '.' && char.IsAsciiDigit(source[i + 1]))
                    {
                        Advance();
                        do { Advance(); } while (i < source.Length && (char.IsAsciiDigit(source[i]) || source[i] == '_'));
                    }
                    if (i < source.Length && source[i] is 'e' or 'E')
                    {
                        Advance();
                        if (i < source.Length && source[i] is '+' or '-') Advance();
                    }
                    while (i < source.Length && (char.IsAsciiLetterOrDigit(source[i]) || source[i] == '_')) Advance();
                }
            }
            else if (i + 1 < source.Length && (source.Substring(i, 2) is "==" or "!=" or "<=" or ">=" or "&&" or "||" or "=>" or "->" or "??" or "?." or "|>")) { Advance(); Advance(); }
            else if ("(),.:{}[]=+-*/%!<>|?".Contains(c)) Advance();
            else throw new SourceError(new("DS1001", path, new(i, 1, line, column), $"Unsupported character '{c}'."));
            tokens.Add(new(source[start..i], new(start, i - start, startLine, startColumn)) { HasInterveningComment = hasInterveningComment });
            hasInterveningComment = false;
            pendingDocumentation = false;
        }
        tokens.Add(new("", new(i, 0, line, column)) { HasInterveningComment = hasInterveningComment });
        return tokens.Select(token => token with { OriginPath = path }).ToArray();
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
    private Token DeclaredTypeIdentifier()
    {
        var name = Identifier();
        if (name.Text is "sbyte" or "byte" or "short" or "ushort" or "uint" or "long" or "ulong" or "decimal" or "float32" or "float")
            throw new SourceError(new("DS1002", path, name.Span, "A built-in numeric type name cannot be declared as a type or type parameter."));
        return name;
    }
    private Token QualifiedIdentifier()
    {
        var first = Identifier();
        var text = first.Text;
        var last = first;
        while (Current.Text == ".")
        {
            Take();
            last = Identifier();
            text += "." + last.Text;
        }
        if (ReferenceEquals(first, last)) return first;
        var span = first.Span;
        return new Token(text, new SourceSpan(span.Start, last.Span.Start + last.Span.Length - span.Start, span.Line, span.Column)) { OriginPath = path };
    }
    private InterpolationSyntax Interpolation()
    {
        var start = Take();
        var source = start.Text;
        var originPath = start.OriginPath ?? path;
        var scan = InterpolationScanner.Scan(source, 0, originPath, start.Span.Start, start.Span.Line, start.Span.Column);
        var texts = new List<Token>();
        var holes = new List<ExpressionSyntax>();
        foreach (var part in scan.Parts)
        {
            var literal = source[part.TextStart..part.TextEnd].Replace("{{", "{", StringComparison.Ordinal).Replace("}}", "}", StringComparison.Ordinal);
            var span = InterpolationScanner.OriginSpan(source, part.TextStart, part.TextEnd - part.TextStart,
                start.Span.Start, start.Span.Line, start.Span.Column);
            texts.Add(new Token("\"" + literal + "\"", span) { OriginPath = originPath });
            if (part.HoleStart < 0) continue;
            var holeSource = source[part.HoleStart..part.HoleEnd];
            IReadOnlyList<Token> holeTokens;
            try { holeTokens = Lexer.Lex(holeSource, originPath); }
            catch (SourceError error)
            {
                var diagnostic = error.Diagnostic;
                throw new SourceError(diagnostic with { Span = InterpolationScanner.OriginSpan(source,
                    part.HoleStart + diagnostic.Span.Start, diagnostic.Span.Length,
                    start.Span.Start, start.Span.Line, start.Span.Column) });
            }
            var mapped = holeTokens.Select(token => token with
            {
                Span = InterpolationScanner.OriginSpan(source, part.HoleStart + token.Span.Start,
                    token.Span.Length, start.Span.Start, start.Span.Line, start.Span.Column), OriginPath = originPath
            }).ToArray();
            var parser = new Parser(mapped, originPath);
            var expression = parser.Expression();
            if (parser.Current.Text != "") throw parser.Error("Expected the end of the interpolation expression.");
            holes.Add(expression);
        }
        return new InterpolationSyntax(start, texts, holes);
    }
    private static bool TypeName(string text) => text.Length > 0 &&
        (char.IsAsciiLetter(text[0]) || text[0] == '_') &&
        (!Reserved.Contains(text) || text is "sbyte" or "byte" or "short" or "ushort" or "int" or "uint" or "long" or "ulong" or "decimal" or "float32" or "float" or "bool" or "string" or "char");

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
        return new Token(text, new SourceSpan(first.Start, last.Start + last.Length - first.Start, first.Line, first.Column)) { OriginPath = path };
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
            parameters.Add(DeclaredTypeIdentifier());
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
    private static readonly HashSet<string> Reserved = new("module import type interface service actor agent worker fn async await let return if else match when for in with extend using app true false null int bool string char public private protected internal".Split(' '));
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
                    "|>" or "??" or "||" or "&&" or "==" or "!=" or "<" or "<=" or ">" or ">=" or "+" or "-" or "*" or "/" or "%";
            }
        }
        return false;
    }
    private bool HasLambdaHead(int start)
    {
        if (start >= tokens.Count) return false;
        var first = tokens[start].Text;
        if (first != "(")
            return start + 1 < tokens.Count && tokens[start + 1].Text == "=>" &&
                first.Length > 0 && (char.IsAsciiLetter(first[0]) || first[0] == '_') && !Reserved.Contains(first);
        var depth = 0;
        for (var cursor = start; cursor < tokens.Count; cursor++)
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
        if (!stopAtMatchArrow && (HasLambdaHead(position) || Current.Text == "async" && HasLambdaHead(position + 1)))
        {
            var start = Current;
            var isAsync = Current.Text == "async";
            if (isAsync) Take();
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
            left = new LambdaSyntax(start, lambdaParameters, body) { IsAsync = isAsync };
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
        else if (Current.Text == "for")
        {
            var keyword = Take();
            var name = Identifier();
            Expect("in");
            var source = Expression(Current.Span.Line, stopAtBlock: true);
            left = new ForSyntax(keyword, name, source, Block());
        }
        else if (Current.Text == "using")
        {
            var keyword = Take();
            var name = Identifier();
            Expect("=");
            var acquisition = Expression(Current.Span.Line, stopAtBlock: true);
            left = new UsingSyntax(keyword, name, acquisition, Block());
        }
        else if (Current.Text is "-" or "!")
        {
            var op = Take();
            left = new UnarySyntax(op, Expression(line is null ? null : Current.Span.Line, 8, stopAtBlock, stopAtMatchArrow));
        }
        else if (Current.Text == "await")
        {
            var keyword = Take();
            left = new AwaitSyntax(keyword, Expression(line is null ? null : Current.Span.Line, 8, stopAtBlock, stopAtMatchArrow));
        }
        else if (Current.Text.StartsWith("$\"", StringComparison.Ordinal))
            left = Interpolation();
        else if (Current.Text == "[")
        {
            var open = Take();
            var elements = new List<ExpressionSyntax>();
            if (Current.Text != "]")
            {
                do
                {
                    elements.Add(Expression());
                    if (Current.Text != ",") break;
                    Take();
                } while (true);
            }
            Expect("]");
            left = new ListSyntax(open, elements);
        }
        else if (Current.Text == "(")
        {
            var open = Take();
            if (Current.Text == ")") { var close = Take(); left = new UnitSyntax(close); }
            else
            {
                var inner = Expression();
                if (Current.Text != ",") { Expect(")"); left = new GroupSyntax(open, inner); }
                else
                {
                    var elements = new List<ExpressionSyntax> { inner };
                    while (Current.Text == ",") { Take(); elements.Add(Expression()); }
                    Expect(")"); left = new TupleSyntax(open, elements);
                }
            }
        }
        else
        {
            var text = Current.Text;
            if (text.Length == 0 || !(text[0] is '"' or '\'' || text is "true" or "false" || char.IsAsciiDigit(text[0]) ||
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
                var genericCall = HasGenericCallShape();
                if (genericCall || (Current.Text == "(" && (line is null || Current.Span.Line == field.Span.Line)))
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
                    left = new ConditionalMemberCallSyntax(left, optionalOperator, field, open, args) { TypeArguments = typeArguments };
                }
                else left = new ConditionalFieldAccessSyntax(left, optionalOperator, field);
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
                else if (Current.Text == "{" && (!stopAtBlock || ConditionRecordConstruction()))
                    left = new QualifiedRecordConstructionSyntax(left, member, RecordInitializers());
                else left = new FieldAccessSyntax(left, member);
                continue;
            }
            if (line is not null && Current.Span.Line != tokens[position - 1].Span.Line && Current.Text != "|>") break;
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
            var precedence = Current.Text switch { "|>" => 0, "??" => 1, "||" => 2, "&&" => 3, "==" or "!=" => 4,
                "<" or "<=" or ">" or ">=" => 5, "+" or "-" => 6, "*" or "/" or "%" => 7, _ => -1 };
            if (precedence < minimumPrecedence) break;
            var op = Take();
            var rightLine = line is not null ? Current.Span.Line : line;
            var right = Expression(rightLine, op.Text == "??" ? precedence : precedence + 1, stopAtBlock, stopAtMatchArrow);
            static bool Comparison(ExpressionSyntax expr) => expr is BinarySyntax binary &&
                binary.Operator.Text is "==" or "!=" or "<" or "<=" or ">" or ">=";
            if (precedence is 4 or 5 && (Comparison(left) || Comparison(right)))
                throw new SourceError(new("DS2013", path, op.Span, "Chained comparisons require explicit grouping."));
            left = op.Text == "??" ? new CoalesceSyntax(left, op, right) : op.Text == "|>"
                ? new PipeSyntax(left, op, right) : new BinarySyntax(left, op, right);
        }
        return left;
    }
    private static bool AllowsContinuation(ExpressionSyntax expression) => expression switch
    {
        IfSyntax or ForSyntax or MatchSyntax or GroupSyntax or TupleSyntax or ListSyntax or CallSyntax or MemberCallSyntax or ConditionalMemberCallSyntax or InvokeSyntax or LambdaSyntax or BinarySyntax or CoalesceSyntax or PipeSyntax or PropagateSyntax or RecordConstructionSyntax or FieldAccessSyntax or ConditionalFieldAccessSyntax or RecordUpdateSyntax => true,
        UnarySyntax unary => AllowsContinuation(unary.Operand),
        AwaitSyntax awaited => AllowsContinuation(awaited.Operand),
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
        if (text.Length > 0 && (text[0] is '"' or '\'' || text is "true" or "false" || char.IsAsciiDigit(text[0])))
        {
            if (minus is not null && !char.IsAsciiDigit(text[0])) throw Error("Expected an integer after '-'.");
            return new LiteralPatternSyntax(Take(), minus);
        }
        if (minus is not null) throw Error("Expected an integer after '-'.");
        var first = Identifier();
        Token? qualifier = null;
        var name = first;
        while (Current.Text == ".")
        {
            Take();
            qualifier = qualifier is null ? name : qualifier with { Text = qualifier.Text + "." + name.Text };
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
        var methodName = declarations.Contains("<method>") && Current.Text.Length > 0 &&
            (char.IsAsciiLetter(Current.Text[0]) || Current.Text[0] == '_') && !Reserved.Contains(Current.Text);
        if (Current.HasInterveningComment || Current.Span.Line != comments[^1].Span.Line + 1 ||
            !declarations.Contains(Current.Text) && !methodName)
            throw new SourceError(new("DS1004", path, comments[^1].Span, "Documentation must immediately precede a declaration."));
        return comments;
    }
    private BlockSyntax Block()
    {
        Expect("{");
        return BlockContents();
    }

    private BlockSyntax BlockContents()
    {
        var bindings = new List<LetBindingSyntax>();
        var statements = new List<StatementSyntax>();
        ExpressionSyntax? result = null;
        while (Current.Text != "}" && Current.Text != "")
        {
            if (Current.Text == "using")
            {
                // A declaration owns the rest of its enclosing lexical block.
                // Desugaring to a nested scope also gives multiple declarations
                // their normal reverse-order cleanup, including on early exits.
                var start = position;
                var keyword = Take();
                var name = Identifier();
                Expect("=");
                var acquisition = Expression(Current.Span.Line, stopAtBlock: true);
                if (Current.Text != "{")
                {
                    var end = tokens[position - 1];
                    if (Current.Text != "}" && Current.Span.Line == end.Span.Line)
                        throw Error("Expected a newline after a using declaration.");
                    var remainder = BlockContents();
                    result = new UsingSyntax(keyword, name, acquisition, remainder);
                    return new(bindings, result) { Statements = statements };
                }
                // A braced using remains an expression; let the normal parser
                // handle subsequent operators and block-statement separation.
                position = start;
            }
            if (Current.Text == "let")
            {
                var headerStart = position;
                Take();
                TupleBindingSyntax? tuplePattern = null; Token? local = null;
                if (Current.Text == "(")
                {
                    TupleBindingSyntax ParseTupleBinding()
                    {
                        if (Current.Text != "(") return new(Identifier(), Array.Empty<TupleBindingSyntax>());
                        var open = Take(); var elements = new List<TupleBindingSyntax> { ParseTupleBinding() };
                        while (Current.Text == ",") { Take(); elements.Add(ParseTupleBinding()); }
                        Expect(")");
                        if (elements.Count < 2) throw Error("Tuple destructuring requires at least two bindings.");
                        return new(open, elements);
                    }
                    tuplePattern = ParseTupleBinding();
                }
                else local = Identifier();
                Token? annotation = null;
                if (Current.Text == ":") { Take(); annotation = TypeToken(); }
                var equals = Expect("=");
                // Header tokens may cross lines inside a tuple pattern or a
                // parenthesized type, but the surrounding binding still ends
                // at a newline. Inspect the original tokens: TypeToken may
                // combine a multiline type into a single synthetic token.
                var headerDepth = 0;
                for (var index = headerStart; index < position; index++)
                {
                    var token = tokens[index];
                    if (index > headerStart && token.Span.Line != tokens[index - 1].Span.Line && headerDepth == 0)
                        throw new SourceError(new("DS1002", path, token.Span, "A binding header may span lines only inside parentheses."));
                    if (token.Text == "(") headerDepth++;
                    else if (token.Text == ")") headerDepth--;
                }
                var first = Current; var value = Expression(first.Span.Line);
                var end = tokens[position - 1];
                if (end.Span.Line != equals.Span.Line && first.Span.Line == equals.Span.Line && !AllowsContinuation(value)) throw new SourceError(new("DS1002", path, end.Span, "Write each let binding on one line."));
                if (Current.Span.Line == end.Span.Line) throw Error("A let binding must be followed by a newline and a final expression or another binding.");
                if (tuplePattern is not null) statements.Add(new TupleLetStatementSyntax(tuplePattern, annotation, value));
                else
                {
                    var binding = new LetBindingSyntax(local!, annotation, first) { Expression = value };
                    bindings.Add(binding); statements.Add(new LetStatementSyntax(binding));
                }
                continue;
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
        while (Current.Text == ".") { Take(); var part = Identifier(); name = name with { Text = name.Text + "." + part.Text }; }
        var imports = new List<Token>();
        while (Current.Text == "import")
        {
            Take(); var imported = Identifier();
            while (Current.Text == ".") { Take(); var part = Identifier(); imported = imported with { Text = imported.Text + "." + part.Text }; }
            imports.Add(imported);
        }
        var functions = new List<FunctionSyntax>();
        var records = new List<RecordDeclarationSyntax>();
        var unions = new List<UnionDeclarationSyntax>();
        var interfaces = new List<InterfaceDeclarationSyntax>();
        var services = new List<ServiceDeclarationSyntax>();
        var resources = new List<ResourceDeclarationSyntax>();
        var actors = new List<ActorDeclarationSyntax>();
        var extensions = new List<ExtensionDeclarationSyntax>();
        IReadOnlyList<ParameterSyntax> TypedParameters()
        {
            Expect("(");
            var parameters = new List<ParameterSyntax>();
            if (Current.Text != ")")
            {
                do
                {
                    var parameter = Identifier();
                    Expect(":");
                    parameters.Add(new(parameter, TypeToken()));
                    if (Current.Text != ",") break;
                    Take();
                } while (true);
            }
            Expect(")");
            return parameters;
        }
        IReadOnlyList<LambdaParameterSyntax> ServiceParameters()
        {
            Expect("(");
            var parameters = new List<LambdaParameterSyntax>();
            if (Current.Text != ")")
            {
                do
                {
                    var parameter = Identifier();
                    Token? type = null;
                    if (Current.Text == ":") { Take(); type = TypeToken(); }
                    parameters.Add(new(parameter, type));
                    if (Current.Text != ",") break;
                    Take();
                } while (true);
            }
            Expect(")");
            return parameters;
        }
        while (Current.Text != "")
        {
            var declarationDocs = Documentation("fn", "async", "type", "interface", "service", "resource", "actor", "extend");
            if (Current.Text == "actor")
            {
                Take();
                var actorName = DeclaredTypeIdentifier();
                if (actorName.Text == "Unit")
                    throw new SourceError(new("DS2042", path, actorName.Span, "Unit is reserved and cannot name an actor."));
                var dependencies = Current.Text == "(" ? TypedParameters() : Array.Empty<ParameterSyntax>();
                Expect("{");
                var methods = new List<ActorMethodSyntax>();
                var queueBindings = new List<ActorQueueBindingSyntax>();
                var eventBindings = new List<ActorEventBindingSyntax>();
                var cronBindings = new List<ActorCronBindingSyntax>();
                var httpBindings = new List<ActorHttpBindingSyntax>();
                var httpPlainBindings = new List<ActorHttpPlainBindingSyntax>();
                while (Current.Text is not ("}" or ""))
                {
                    var methodDocs = Documentation("async", "<method>");
                    // `trigger()` remains an ordinary helper; only the actor member
                    // forms beginning with `trigger Queue`, `Event`, `Cron` or `Http` have binding meaning.
                    if (Current.Text == "trigger" && position + 1 < tokens.Count && tokens[position + 1].Text != "(")
                    {
                        var keyword = Take();
                        var isEvent = Current.Text == "Event";
                        var isCron = Current.Text == "Cron";
                        var isHttp = Current.Text == "Http";
                        Expect(isEvent ? "Event" : isCron ? "Cron" : isHttp ? "Http" : "Queue");
                        Expect("<");
                        var messageType = TypeToken();
                        Expect(">");
                        Expect("(");
                        if (isHttp)
                        {
                            var binder = QualifiedIdentifier(); Expect(",");
                            var bindingMapper = QualifiedIdentifier(); Expect(",");
                            var successMapper = QualifiedIdentifier();
                            Token? businessMapper = null;
                            if (Current.Text == ",") { Take(); businessMapper = QualifiedIdentifier(); }
                            Expect(")");
                            if (businessMapper is not null)
                                httpBindings.Add(new(keyword, messageType, binder, bindingMapper, successMapper, businessMapper) { DocumentationComments = methodDocs });
                            else
                                httpPlainBindings.Add(new(keyword, messageType, binder, bindingMapper, successMapper) { DocumentationComments = methodDocs });
                        }
                        else if (isEvent)
                        {
                            Expect(")");
                            eventBindings.Add(new(keyword, messageType) { DocumentationComments = methodDocs });
                        }
                        else if (isCron)
                        {
                            if (!Current.Text.StartsWith('"') || !Current.Text.EndsWith('"') || Current.Text.Length < 2)
                                throw Error("A cron binding requires a string-literal schedule name.");
                            var scheduleName = Take();
                            Expect(",");
                            if (!Current.Text.StartsWith('"') || !Current.Text.EndsWith('"') || Current.Text.Length < 2)
                                throw Error("A cron binding requires a string-literal expression.");
                            var expression = Take();
                            Expect(")");
                            cronBindings.Add(new(keyword, messageType, scheduleName, expression) { DocumentationComments = methodDocs });
                        }
                        else
                        {
                            if (!Current.Text.StartsWith('"') || !Current.Text.EndsWith('"') || Current.Text.Length < 2)
                                throw Error("A queue binding requires a string-literal queue name.");
                            var queueName = Take();
                            Expect(")");
                            queueBindings.Add(new(keyword, messageType, queueName) { DocumentationComments = methodDocs });
                        }
                        continue;
                    }
                    var isAsyncMethod = Current.Text == "async";
                    if (isAsyncMethod) Take();
                    var methodName = Identifier();
                    var actorParameters = TypedParameters();
                    Expect(":");
                    var returnType = TypeToken();
                    methods.Add(new(methodName, actorParameters, returnType, isAsyncMethod, Block()) { DocumentationComments = methodDocs });
                }
                Expect("}");
                actors.Add(new(actorName, dependencies, methods) { DocumentationComments = declarationDocs, QueueBindings = queueBindings, EventBindings = eventBindings, CronBindings = cronBindings, HttpBindings = httpBindings, HttpPlainBindings = httpPlainBindings });
                continue;
            }
            if (Current.Text == "extend")
            {
                Take();
                var target = TypeToken();
                Expect("{");
                var methods = new List<ExtensionMethodSyntax>();
                while (Current.Text is not ("}" or ""))
                {
                    var methodDocs = Documentation("fn", "async");
                    var extensionAsync = Current.Text == "async";
                    if (extensionAsync) Take();
                    Expect("fn");
                    var methodName = Identifier();
                    if (Current.Text == "<") throw Error("Generic extension methods are not supported.");
                    Expect("(");
                    if (Current.Text != "self") throw Error("An extension method requires self as its first parameter.");
                    var self = Identifier();
                    Token? selfAnnotation = null;
                    if (Current.Text == ":") { Take(); selfAnnotation = TypeToken(); }
                    var extensionParameters = new List<ParameterSyntax> { new(self, target) };
                    while (Current.Text == ",")
                    {
                        Take();
                        var parameter = Identifier();
                        if (parameter.Text == "self") throw new SourceError(new("DS1002", path, parameter.Span, "self may appear only as the receiver."));
                        Expect(":");
                        extensionParameters.Add(new(parameter, TypeToken()));
                    }
                    Expect(")");
                    Expect(":");
                    var returnType = TypeToken();
                    methods.Add(new(methodName, extensionParameters, returnType, extensionAsync, Block()) { DocumentationComments = methodDocs, SelfAnnotation = selfAnnotation });
                }
                Expect("}");
                extensions.Add(new(target, methods) { DocumentationComments = declarationDocs });
                continue;
            }
            if (Current.Text == "resource")
            {
                Take();
                var resourceName = DeclaredTypeIdentifier();
                var dependencies = Current.Text == "(" ? TypedParameters() : Array.Empty<ParameterSyntax>();
                Expect("{");
                var methods = new List<ServiceMethodSyntax>();
                while (Current.Text is not ("}" or ""))
                {
                    var methodDocs = Documentation("async", "<method>");
                    var isAsyncMethod = Current.Text == "async";
                    if (isAsyncMethod) Take();
                    var methodName = Identifier();
                    var resourceParameters = ServiceParameters();
                    Expect(":");
                    var returnType = TypeToken();
                    methods.Add(new(methodName, resourceParameters, returnType, isAsyncMethod, Block()) { DocumentationComments = methodDocs });
                }
                Expect("}");
                resources.Add(new(resourceName, dependencies, methods) { DocumentationComments = declarationDocs });
                continue;
            }
            if (Current.Text == "interface")
            {
                Take();
                var interfaceName = DeclaredTypeIdentifier();
                Expect("{");
                var methods = new List<InterfaceMethodSyntax>();
                while (Current.Text is not ("}" or ""))
                {
                    var methodDocs = Documentation("async", "<method>");
                    var isAsyncMethod = Current.Text == "async";
                    if (isAsyncMethod) Take();
                    var methodName = Identifier();
                    var methodParameters = TypedParameters();
                    Expect(":");
                    var returnType = TypeToken();
                    var signatureEnd = tokens[position - 1];
                    var methodBody = Current.Text == "{" ? Block() : null;
                    methods.Add(new(methodName, methodParameters, returnType, isAsyncMethod) { DocumentationComments = methodDocs, Body = methodBody });
                    if (methodBody is null && Current.Text != "}" && Current.Span.Line == signatureEnd.Span.Line)
                        throw Error("Expected a newline after an interface method signature.");
                }
                Expect("}");
                interfaces.Add(new(interfaceName, methods) { DocumentationComments = declarationDocs });
                continue;
            }
            if (Current.Text == "service")
            {
                Take();
                var serviceName = DeclaredTypeIdentifier();
                var dependencies = Current.Text == "(" ? TypedParameters() : Array.Empty<ParameterSyntax>();
                Expect(":");
                var contracts = new List<Token>();
                do
                {
                    contracts.Add(TypeToken());
                    if (Current.Text != ",") break;
                    Take();
                } while (true);
                Expect("{");
                var methods = new List<ServiceMethodSyntax>();
                while (Current.Text is not ("}" or ""))
                {
                    var methodDocs = Documentation("async", "<method>");
                    var isAsyncMethod = Current.Text == "async";
                    if (isAsyncMethod) Take();
                    var methodName = Identifier();
                    var methodParameters = ServiceParameters();
                    Token? returnType = null;
                    if (Current.Text == ":") { Take(); returnType = TypeToken(); }
                    methods.Add(new(methodName, methodParameters, returnType, isAsyncMethod, Block()) { DocumentationComments = methodDocs });
                }
                Expect("}");
                services.Add(new(serviceName, dependencies, contracts, methods) { DocumentationComments = declarationDocs });
                continue;
            }
            if (Current.Text == "type")
            {
                Take(); var recordName = DeclaredTypeIdentifier(); var typeParameters = TypeParameters(); Expect("{");
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
                    var fieldEnd = tokens[position - 1];
                    fields.Add(new(fieldName, fieldType));
                    if (Current.Text != "}" && Current.Span.Line == fieldEnd.Span.Line)
                        throw Error("Expected a newline after a record field declaration.");
                }
                Expect("}");
                records.Add(new(recordName, fields) { DocumentationComments = declarationDocs, TypeParameters = typeParameters });
                continue;
            }
            var isAsync = Current.Text == "async";
            if (isAsync) Take();
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
            Expect(")");
            var inferReturnType = Current.Text != ":";
            Token type;
            if (inferReturnType) type = new Token("<inferred>", Current.Span);
            else { Take(); type = TypeToken(); }
            var body = Block();
            // Prefer the missing-result span over a following stray closing brace.
            if (Current.Text == "}" && type.Text == "int" && body.Result is UnitSyntax missing &&
                body.Statements.All(statement => statement is LetStatementSyntax or TupleLetStatementSyntax))
                throw new SourceError(new("DS2002", path, missing.Token.Span, "A reachable int result is required."));
            functions.Add(new(function, type, body.Result.Token) { Expression = body.Result, Body = body, DocumentationComments = declarationDocs, TypeParameters = functionTypeParameters, Parameters = parameters.Count == 0 ? Array.Empty<ParameterSyntax>() : parameters, Bindings = body.Bindings, InferReturnType = inferReturnType, IsAsync = isAsync });
        }
        if (functions.Count == 0 && records.Count == 0 && unions.Count == 0 && interfaces.Count == 0 && services.Count == 0 && resources.Count == 0 && actors.Count == 0 && extensions.Count == 0) throw Error("Expected at least one declaration.");
        return new(name, functions) { DocumentationComments = moduleDocs, Records = records, Unions = unions, Interfaces = interfaces, Services = services, Resources = resources, Actors = actors, Extensions = extensions, Imports = imports };
    }
}

public static class Binder
{
    private static bool IntegerType(string? type) => type is "sbyte" or "byte" or "short" or "ushort" or "int" or "uint" or "long" or "ulong";
    private static ulong IntegerMaximum(string type) => type switch
    {
        "sbyte" => (ulong)sbyte.MaxValue, "byte" => byte.MaxValue,
        "short" => (ulong)short.MaxValue, "ushort" => ushort.MaxValue,
        "int" => int.MaxValue, "uint" => uint.MaxValue,
        "long" => long.MaxValue, "ulong" => ulong.MaxValue,
        _ => throw new InvalidOperationException("Not an integer type.")
    };
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
        void Fail(string code, Token token, string message) => throw new SourceError(new(code, token.OriginPath ?? path, token.Span, message));
        // Resource declarations are private nominal capabilities, lowered to
        // service machinery only after their source-level ownership is checked.
        var resourceNames = new HashSet<string>(StringComparer.Ordinal);
        var resourceDeclarations = syntax.Resources.GroupBy(item => item.Name.Text, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var resourceInterfaces = new List<InterfaceDeclarationSyntax>();
        var resourceContracts = new Dictionary<string, string>(StringComparer.Ordinal);
        var privateResourceContracts = new HashSet<string>(StringComparer.Ordinal);
        var resourceServices = new List<ServiceDeclarationSyntax>();
        var occupiedTypes = syntax.Records.Select(item => item.Name.Text).Concat(syntax.Unions.Select(item => item.Name.Text))
            .Concat(syntax.Interfaces.Select(item => item.Name.Text)).Concat(syntax.Services.Select(item => item.Name.Text))
            .Concat(syntax.Resources.Select(item => item.Name.Text)).Concat(syntax.Actors.Select(item => item.Name.Text)).ToHashSet(StringComparer.Ordinal);
        var resourceIndex = 0;
        foreach (var resource in syntax.Resources)
        {
            if (!resourceNames.Add(resource.Name.Text)) Fail("DS2041", resource.Name, "Duplicate resource declaration.");
            var releases = resource.Methods.Where(method => method.Name.Text == "release").ToArray();
            if (releases.Length != 1 || releases[0].Parameters.Count != 0 || releases[0].ReturnType?.Text != "Unit")
                Fail("DS2041", resource.Name, "A resource requires exactly one release(): Unit method.");
            var methodNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var method in resource.Methods)
            {
                if (!methodNames.Add(method.Name.Text)) Fail("DS2041", method.Name, "Duplicate resource operation.");
                if (method.ReturnType is null || method.Parameters.Any(parameter => parameter.Type is null))
                    Fail("DS2041", method.Name, "Resource operations require explicit parameter and result types.");
            }
            string contractName;
            do contractName = "__dsharp_resource_contract_" + (resourceIndex++).ToString(CultureInfo.InvariantCulture);
            while (!occupiedTypes.Add(contractName));
            privateResourceContracts.Add(contractName);
            resourceContracts.Add(resource.Name.Text, contractName);
            var contractToken = resource.Name with { Text = contractName };
            var methods = resource.Methods.Select(method => new InterfaceMethodSyntax(method.Name,
                method.Parameters.Select(parameter => new ParameterSyntax(parameter.Name, parameter.Type!)).ToArray(),
                method.ReturnType!, method.IsAsync)).ToArray();
            resourceInterfaces.Add(new(contractToken, methods));
            resourceServices.Add(new(resource.Name, resource.Dependencies, new[] { contractToken }, resource.Methods));
        }
        syntax = syntax with { Interfaces = syntax.Interfaces.Concat(resourceInterfaces).ToArray(),
            Services = syntax.Services.Concat(resourceServices).ToArray() };
        // Give every declared type parameter a distinct internal identity. The
        // lexer cannot produce '$', so these symbols cannot capture a source
        // type, even when a type parameter shadows a nominal type or Unit.
        var nextTypeSymbol = 0;
        var userTypeSymbolCount = syntax.Records.Sum(record => record.TypeParameters.Count) +
            syntax.Unions.Sum(union => union.TypeParameters.Count) + syntax.Functions.Sum(function => function.TypeParameters.Count);
        var resultSymbol = "$" + userTypeSymbolCount.ToString(CultureInfo.InvariantCulture);
        var optionSymbol = "$" + (userTypeSymbolCount + 1).ToString(CultureInfo.InvariantCulture);
        var listSymbol = "$" + (userTypeSymbolCount + 2).ToString(CultureInfo.InvariantCulture);
        var mapSymbol = "$" + (userTypeSymbolCount + 3).ToString(CultureInfo.InvariantCulture);
        var setSymbol = "$" + (userTypeSymbolCount + 4).ToString(CultureInfo.InvariantCulture);
        var mutableListSymbol = "$" + (userTypeSymbolCount + 5).ToString(CultureInfo.InvariantCulture);
        var mutableMapSymbol = "$" + (userTypeSymbolCount + 6).ToString(CultureInfo.InvariantCulture);
        var taskSymbol = "$" + (userTypeSymbolCount + 7).ToString(CultureInfo.InvariantCulture);
        var invocationReceiptSymbol = "$" + (userTypeSymbolCount + 8).ToString(CultureInfo.InvariantCulture);
        var publicationReceiptSymbol = "$" + (userTypeSymbolCount + 9).ToString(CultureInfo.InvariantCulture);
        var resultUsed = false;
        var optionUsed = false;
        var listUsed = false;
        var mapUsed = false;
        var setUsed = false;
        var mutableListUsed = false;
        var mutableMapUsed = false;
        var taskUsed = syntax.Functions.Any(function => function.IsAsync) || syntax.Actors.Count > 0;
        bool UserType(string name) => syntax.Records.Any(record => record.Name.Text == name) ||
            syntax.Unions.Any(union => union.Name.Text == name) || syntax.Interfaces.Any(item => item.Name.Text == name) ||
            syntax.Services.Any(item => item.Name.Text == name) || syntax.Actors.Any(item => item.Name.Text == name);
        var userResultType = UserType("Result");
        var userOptionType = UserType("Option");
        var userListType = UserType("List");
        var userMapType = UserType("Map");
        var userSetType = UserType("Set");
        var userMutableListType = UserType("MutableList");
        var userMutableMapType = UserType("MutableMap");
        var userTaskType = UserType("Task");
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
            bool ForgedReceipt(TypeShape value) => value.Name == invocationReceiptSymbol || value.Name == publicationReceiptSymbol || value.Arguments.Any(ForgedReceipt);
            if (ForgedReceipt(shape)) Fail("DS2048", token, "Actor receipts are internal compiler types.");
            TypeShape Rebind(TypeShape value)
            {
                var name = value.Name;
                if (scope.TryGetValue(name, out var symbol)) name = symbol;
                else if (name == "Result" && !userResultType) { name = resultSymbol; resultUsed = true; }
                else if (name == "Option" && !userOptionType) { name = optionSymbol; optionUsed = true; }
                else if (name == "List" && !userListType) { name = listSymbol; listUsed = true; }
                else if (name == "Map" && !userMapType) { name = mapSymbol; mapUsed = true; }
                else if (name == "Set" && !userSetType) { name = setSymbol; setUsed = true; }
                else if (name == "MutableList" && !userMutableListType) name = mutableListSymbol;
                else if (name == "MutableMap" && !userMutableMapType) name = mutableMapSymbol;
                else if (name == "Task" && !userTaskType) { name = taskSymbol; taskUsed = true; }
                return value with { Name = name, Arguments = value.Arguments.Select(Rebind).ToArray() };
            }
            return token with { Text = Rebind(shape).ToString() };
        }
        Token ScopedParameter(Token token, IReadOnlyDictionary<string, string> scope) => token with { Text = scope[token.Text] };
        var sourceHttpFunctions = syntax.Functions.Select(function => function.Name.Text).ToHashSet(StringComparer.Ordinal);
        var explicitAsyncSourceFunctions = syntax.Functions.Where(function => function.IsAsync && !function.InferReturnType)
            .Select(function => function.Name.Text).ToHashSet(StringComparer.Ordinal);
        // Extensions are ordinary private functions after resolution. Their
        // receiver is the first parameter, so calls preserve evaluation order.
        var extensionDeclarations = new List<(string Id, ExtensionDeclarationSyntax Declaration, ExtensionMethodSyntax Method)>();
        var occupiedFunctions = new HashSet<string>(StringComparer.Ordinal);
        void CollectExtensionNames(object? node)
        {
            if (node is null or string) return;
            if (node is Token token) { occupiedFunctions.Add(token.Text); return; }
            if (node is System.Collections.IEnumerable sequence)
            {
                foreach (var item in sequence) CollectExtensionNames(item);
                return;
            }
            var type = node.GetType();
            if (!type.Name.EndsWith("Syntax", StringComparison.Ordinal)) return;
            foreach (var property in type.GetProperties().Where(property => property.GetIndexParameters().Length == 0))
                CollectExtensionNames(property.GetValue(node));
        }
        CollectExtensionNames(syntax);
        var extensionOrdinal = 0;
        foreach (var declaration in syntax.Extensions)
        foreach (var method in declaration.Methods)
        {
            var id = method.InternalName;
            if (id is null)
            {
                do { id = "__dsharp_extension_" + (extensionOrdinal++).ToString(CultureInfo.InvariantCulture); }
                while (occupiedFunctions.Contains(id));
            }
            occupiedFunctions.Add(id);
            extensionDeclarations.Add((id, declaration, method));
        }
        syntax = syntax with { Functions = syntax.Functions.Concat(extensionDeclarations.Select(extension =>
            new FunctionSyntax(extension.Method.Name with { Text = extension.Id }, extension.Method.ReturnType, extension.Method.Body.Result.Token)
            {
                Parameters = extension.Method.Parameters, Body = extension.Method.Body, Expression = extension.Method.Body.Result,
                IsAsync = extension.Method.IsAsync, DocumentationComments = extension.Method.DocumentationComments
            })).ToArray() };
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
        var noTypeScope = new Dictionary<string, string>(StringComparer.Ordinal);
        var scopedInterfaces = syntax.Interfaces.Select(item => item with
        {
            Methods = item.Methods.Select(method => method with
            {
                Parameters = method.Parameters.Select(parameter => parameter with { Type = ScopedType(parameter.Type, noTypeScope) }).ToArray(),
                ReturnType = ScopedType(method.ReturnType, noTypeScope)
            }).ToArray()
        }).ToArray();
        var scopedServices = syntax.Services.Select(item => item with
        {
            Dependencies = item.Dependencies.Select(parameter => parameter with { Type = ScopedType(parameter.Type, noTypeScope) }).ToArray(),
            Methods = item.Methods.Select(method => method with
            {
                Parameters = method.Parameters.Select(parameter => parameter with { Type = parameter.Type is null ? null : ScopedType(parameter.Type, noTypeScope) }).ToArray(),
                ReturnType = method.ReturnType is null ? null : ScopedType(method.ReturnType, noTypeScope)
            }).ToArray()
        }).ToArray();
        var scopedActors = syntax.Actors.Select(item => item with
        {
            Dependencies = item.Dependencies.Select(parameter => parameter with { Type = ScopedType(parameter.Type, noTypeScope) }).ToArray(),
            QueueBindings = item.QueueBindings.Select(binding => binding with { MessageType = ScopedType(binding.MessageType, noTypeScope) }).ToArray(),
            EventBindings = item.EventBindings.Select(binding => binding with { MessageType = ScopedType(binding.MessageType, noTypeScope) }).ToArray(),
            CronBindings = item.CronBindings.Select(binding => binding with { MessageType = ScopedType(binding.MessageType, noTypeScope) }).ToArray(),
            HttpBindings = item.HttpBindings.Select(binding => binding with { ResponseType = ScopedType(binding.ResponseType, noTypeScope) }).ToArray(),
            HttpPlainBindings = item.HttpPlainBindings.Select(binding => binding with { ResponseType = ScopedType(binding.ResponseType, noTypeScope) }).ToArray(),
            Methods = item.Methods.Select(method => method with
            {
                Parameters = method.Parameters.Select(parameter => parameter with { Type = ScopedType(parameter.Type, noTypeScope) }).ToArray(),
                ReturnType = ScopedType(method.ReturnType, noTypeScope)
            }).ToArray()
        }).ToArray();
        nextTypeSymbol += 10; // Reserve the intrinsic nominal identities after source symbols.
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
        // This one omitted async result is a compiler-owned, non-spellable
        // receipt. Other omitted async results retain the existing rejection.
        CallSyntax? AwaitedResultCall(FunctionSyntax function)
        {
            ExpressionSyntax? result = function.Body?.Result ?? function.Expression;
            while (result is GroupSyntax group) result = group.Inner;
            if (result is not AwaitSyntax awaited) return null;
            ExpressionSyntax operand = awaited.Operand;
            while (operand is GroupSyntax group) operand = group.Inner;
            return operand as CallSyntax;
        }
        CallSyntax? DispatchResultSite(FunctionSyntax function) =>
            AwaitedResultCall(function) is { Name.Text: "Dispatch" } call ? call : null;
        bool HasDispatchLocal(FunctionSyntax function)
        {
            if (function.Parameters.Any(parameter => parameter.Name.Text == "Dispatch") ||
                functionTypeScopes[function.Name.Text].ContainsKey("Dispatch")) return true;
            var body = function.Body;
            return (body?.Bindings ?? function.Bindings).Any(binding => binding.Name.Text == "Dispatch") ||
                body is not null && (body.Statements.OfType<LetStatementSyntax>()
                    .Any(binding => binding.Binding.Name.Text == "Dispatch") ||
                    body.Statements.OfType<TupleLetStatementSyntax>()
                    .Any(binding => binding.Pattern.Names.Any(name => name.Text == "Dispatch")));
        }
        var normalizedFunctions = scopedFunctions.Select(function => function.IsAsync && function.InferReturnType &&
            sourceHttpFunctions.Contains(function.Name.Text) && DispatchResultSite(function) is { } dispatchSite &&
            !HasDispatchLocal(function) &&
            !scopedFunctions.Any(other => other.Name.Text == "Dispatch") &&
            !scopedRecords.Any(item => item.Name.Text == "Dispatch") && !scopedUnions.Any(item => item.Name.Text == "Dispatch") &&
            !scopedUnions.Any(item => item.Cases.Any(unionCase => unionCase.Name.Text == "Dispatch")) &&
            !scopedInterfaces.Any(item => item.Name.Text == "Dispatch") && !scopedServices.Any(item => item.Name.Text == "Dispatch") &&
            !scopedActors.Any(item => item.Name.Text == "Dispatch")
                ? function with { ReturnType = function.ReturnType with { Text = dispatchSite.TypeArguments.Count == 0 ? publicationReceiptSymbol : invocationReceiptSymbol }, InferReturnType = false }
                : function).ToArray();
        // Only a finite chain rooted in an existing Dispatch entry can infer an
        // opaque receipt. Restrict relay arguments to already-bound parameters
        // (or Unit) and the entire body to the final await, so no resource scope
        // or stored receipt can cross the acceptance boundary here.
        var receiptRelayEntries = new HashSet<string>(StringComparer.Ordinal);
        bool IsReceipt(string name) => name == invocationReceiptSymbol || name == publicationReceiptSymbol;
        bool relayChanged;
        do
        {
            relayChanged = false;
            for (var i = 0; i < normalizedFunctions.Length; i++)
            {
                var function = normalizedFunctions[i];
                if (!function.IsAsync || !function.InferReturnType || !sourceHttpFunctions.Contains(function.Name.Text) ||
                    function.TypeParameters.Count != 0 || function.Body is null || function.Body.Bindings.Count != 0 ||
                    function.Body.Statements.Count != 0 || AwaitedResultCall(function) is not { } call ||
                    call.TypeArguments.Count != 0 || function.Parameters.Any(parameter => parameter.Name.Text == call.Name.Text) ||
                    functionTypeScopes[function.Name.Text].ContainsKey(call.Name.Text) ||
                    call.Arguments.Any(argument => argument is not UnitSyntax &&
                        (argument is not ValueSyntax value || !function.Parameters.Any(parameter => parameter.Name.Text == value.Value.Text))))
                    continue;
                var matches = normalizedFunctions.Where(candidate => candidate.Name.Text == call.Name.Text).ToArray();
                if (matches.Length != 1 || matches[0].InferReturnType || matches[0].TypeParameters.Count != 0 ||
                    matches[0].Parameters.Count != call.Arguments.Count || !IsReceipt(matches[0].ReturnType.Text)) continue;
                normalizedFunctions[i] = function with { ReturnType = function.ReturnType with { Text = matches[0].ReturnType.Text }, InferReturnType = false };
                receiptRelayEntries.Add(function.Name.Text);
                relayChanged = true;
            }
        } while (relayChanged);
        syntax = syntax with { Records = scopedRecords, Unions = scopedUnions, Functions = normalizedFunctions, Interfaces = scopedInterfaces, Services = scopedServices, Actors = scopedActors };
        string DisplayType(string type, bool labelParameters = false)
        {
            if (!GenericTypes.TryParse(type, out var shape)) return type;
            TypeShape Display(TypeShape value) => value with
            {
                Name = value.Name == resultSymbol ? "Result" : value.Name == optionSymbol ? "Option" : value.Name == listSymbol ? "List" : value.Name == mapSymbol ? "Map" : value.Name == setSymbol ? "Set" : value.Name == mutableListSymbol ? "MutableList" : value.Name == mutableMapSymbol ? "MutableMap" : value.Name == taskSymbol ? "Task" : value.Name == invocationReceiptSymbol ? "invocation receipt" : value.Name == publicationReceiptSymbol ? "publication receipt" : syntax.TypeDisplayNames.TryGetValue(value.Name, out var displayName) ? displayName : typeSymbolNames.TryGetValue(value.Name, out var name)
                    ? name + (labelParameters ? " (type parameter)" : "") : value.Name,
                Arguments = value.Arguments.Select(Display).ToArray()
            };
            string Format(TypeShape value)
            {
                var suffix = value.IsNullable ? "?" : "";
                if (value.IsTuple) return "(" + string.Join(", ", value.Arguments.Select(Format)) + ")" + suffix;
                return value.Name + (value.Arguments.Count == 0 ? "" : "<" + string.Join(", ", value.Arguments.Select(Format)) + ">") + suffix;
            }
            return Format(Display(shape));
        }
        string ActorDisplayType(string type)
        {
            if (!GenericTypes.TryParse(type, out var shape)) return type;
            TypeShape Restore(TypeShape value) => value with
            {
                Name = syntax.ActorTypeDisplayNames.TryGetValue(value.Name, out var original) ? original : value.Name,
                Arguments = value.Arguments.Select(Restore).ToArray()
            };
            return DisplayType(Restore(shape).ToString());
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
        var interfaces = new Dictionary<string, InterfaceDeclarationSyntax>(StringComparer.Ordinal);
        foreach (var item in syntax.Interfaces)
            if (!interfaces.TryAdd(item.Name.Text, item) || records.ContainsKey(item.Name.Text) || unions.ContainsKey(item.Name.Text))
                Fail("DS2015", item.Name, "Duplicate interface or type declaration.");
        var services = new Dictionary<string, ServiceDeclarationSyntax>(StringComparer.Ordinal);
        foreach (var item in syntax.Services)
            if (!services.TryAdd(item.Name.Text, item) || interfaces.ContainsKey(item.Name.Text) || records.ContainsKey(item.Name.Text) || unions.ContainsKey(item.Name.Text))
                Fail("DS2015", item.Name, "Duplicate service or type declaration.");
        var actors = new Dictionary<string, ActorDeclarationSyntax>(StringComparer.Ordinal);
        foreach (var actor in syntax.Actors)
            if (actor.Name.Text == "Unit" || !actors.TryAdd(actor.Name.Text, actor) || records.ContainsKey(actor.Name.Text) || unions.ContainsKey(actor.Name.Text) ||
                interfaces.ContainsKey(actor.Name.Text) || services.ContainsKey(actor.Name.Text) ||
                syntax.Functions.Any(function => function.Name.Text == actor.Name.Text))
                Fail("DS2042", actor.Name, "Duplicate actor or conflicting declaration.");
        bool IsNullable(string type) => type.EndsWith("?", StringComparison.Ordinal);
        var arities = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["int"] = 0, ["sbyte"] = 0, ["byte"] = 0, ["short"] = 0, ["ushort"] = 0,
            ["uint"] = 0, ["long"] = 0, ["ulong"] = 0, ["decimal"] = 0, ["float32"] = 0, ["float"] = 0,
            ["bool"] = 0, ["string"] = 0, ["char"] = 0, ["Unit"] = 0,
            [listSymbol] = 1, [mapSymbol] = 2, [setSymbol] = 1, [taskSymbol] = 1, [invocationReceiptSymbol] = 0, [publicationReceiptSymbol] = 0
        };
        foreach (var record in syntax.Records) arities.Add(record.Name.Text, record.TypeParameters.Count);
        foreach (var union in syntax.Unions) arities.Add(union.Name.Text, union.TypeParameters.Count);
        foreach (var item in interfaces.Values)
            if (!arities.TryAdd(item.Name.Text, 0)) Fail("DS2015", item.Name, "Reserved interface type name.");
        foreach (var item in services.Values)
            if (!arities.TryAdd(item.Name.Text, 0)) Fail("DS2015", item.Name, "Reserved service type name.");
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
            // Unconstrained source parameters cannot prove the adopted key
            // eligibility rule. Values remain generic, but Map/Set keys must
            // be fully known and recursively eligible in this first slice.
            bool EligibleKey(TypeShape candidate, HashSet<string> visiting)
            {
                if (candidate.IsFunction || candidate.IsTuple || candidate.Name == listSymbol ||
                    candidate.Name == mapSymbol || candidate.Name == setSymbol || scope.Contains(candidate.Name)) return false;
                if (candidate.Name is "sbyte" or "byte" or "short" or "ushort" or "int" or "uint" or "long" or "ulong" or "decimal" or "bool" or "string" or "char" or "Unit") return candidate.Arguments.Count == 0;
                if (candidate.Arguments.Any(argument => argument.ContainsFunction)) return false;
                if (!records.TryGetValue(candidate.Name, out var record) && !unions.TryGetValue(candidate.Name, out _)) return false;
                if (!candidate.Arguments.All(argument => EligibleKey(argument, new HashSet<string>(visiting, StringComparer.Ordinal)))) return false;
                if (!visiting.Add(candidate.Name)) return true;
                try
                {
                    var typeParameters = record is not null ? record.TypeParameters : unions[candidate.Name].TypeParameters;
                    if (candidate.Arguments.Count != typeParameters.Count) return false;
                    var substitutions = typeParameters.Select((parameter, i) => (parameter.Text, Value: candidate.Arguments[i].ToString()))
                        .ToDictionary(pair => pair.Text, pair => pair.Value, StringComparer.Ordinal);
                    var fields = record is not null ? record.Fields.Select(field => field.Type.Text) :
                        unions[candidate.Name].Cases.SelectMany(unionCase => unionCase.PayloadTypes.Select(payload => payload.Text));
                    foreach (var field in fields)
                        if (!GenericTypes.TrySubstitute(field, substitutions, out var substituted) ||
                            !GenericTypes.TryParse(substituted, out var fieldShape) || !EligibleKey(fieldShape, visiting)) return false;
                    return true;
                }
                finally { visiting.Remove(candidate.Name); }
            }
            bool ExecutionTypesOnly(TypeShape value)
            {
                bool ContainsTask(TypeShape item) => item.Name == taskSymbol || item.Arguments.Any(ContainsTask);
                bool ContainsCapability(TypeShape item) => interfaces.ContainsKey(item.Name) || services.ContainsKey(item.Name) || item.Arguments.Any(ContainsCapability);
                if (resourceNames.Contains(value.Name) || privateResourceContracts.Contains(value.Name)) return false;
                if ((interfaces.ContainsKey(value.Name) || services.ContainsKey(value.Name)) && value.IsNullable) return false;
                if (!value.IsFunction && value.Name != taskSymbol &&
                    value.Arguments.Any(argument => argument.ContainsFunction || ContainsTask(argument) || ContainsCapability(argument))) return false;
                if ((value.Name == mapSymbol || value.Name == setSymbol) &&
                    (value.Arguments.Count == 0 || !EligibleKey(value.Arguments[0], new HashSet<string>(StringComparer.Ordinal)))) return false;
                return value.Arguments.All(ExecutionTypesOnly);
            }
            return ExecutionTypesOnly(shape);
        }
        bool IsBuilder(TypeShape shape) => shape.Name == mutableListSymbol || shape.Name == mutableMapSymbol;
        bool ContainsBuilder(TypeShape shape) => IsBuilder(shape) || shape.Arguments.Any(ContainsBuilder);
        bool ContainsTask(TypeShape shape) => shape.Name == taskSymbol || shape.Arguments.Any(ContainsTask);
        bool ContainsCapability(TypeShape shape) => interfaces.ContainsKey(shape.Name) || services.ContainsKey(shape.Name) || shape.Arguments.Any(ContainsCapability);
        bool ContainsReceipt(TypeShape shape) => shape.Name == invocationReceiptSymbol || shape.Name == publicationReceiptSymbol || shape.Arguments.Any(ContainsReceipt);
        bool DataType(string type) => GenericTypes.TryParse(type, out var shape) && !shape.ContainsFunction && !ContainsBuilder(shape) && !ContainsTask(shape) && !ContainsCapability(shape) && !ContainsReceipt(shape);
        // Resource-bearing Results are affine transfer envelopes, never normal
        // data. Recognize only the intrinsic Result with a direct, nonnullable
        // resource success type and an independently valid immutable error.
        bool OwnedResult(string type) => GenericTypes.TryParse(type, out var shape) &&
            shape.Name == resultSymbol && !shape.IsNullable && shape.Arguments.Count == 2 &&
            !shape.Arguments[0].IsNullable && resourceNames.Contains(shape.Arguments[0].ToString()) &&
            SupportedType(shape.Arguments[1].ToString()) && DataType(shape.Arguments[1].ToString());
        bool OwnedTransfer(string type) => resourceNames.Contains(type) || OwnedResult(type);
        foreach (var extension in extensionDeclarations)
        {
            var scoped = syntax.Functions.First(item => item.Name.Text == extension.Id);
            var target = scoped.Parameters[0].Type;
            if (extension.Method.SelfAnnotation is { } annotation && ScopedType(annotation, noTypeScope).Text != target.Text)
                Fail("DS2011", annotation, "The self annotation must match the extension target.");
            if (!SupportedType(target.Text) || GenericTypes.TryParse(target.Text, out var targetShape) && ContainsBuilder(targetShape))
                Fail("DS2011", target, "Extension targets must be supported non-resource, non-builder types.");
            if (OwnedTransfer(scoped.ReturnType.Text))
                Fail("DS2041", scoped.ReturnType, "Extensions cannot transfer owned resources.");
        }
        // A callable's signature can mention an interface without holding an
        // interface value. Its own captures/partials are checked separately.
        bool RetainsCapability(TypeShape shape) => !shape.IsFunction &&
            (interfaces.ContainsKey(shape.Name) || services.ContainsKey(shape.Name) || shape.Arguments.Any(RetainsCapability));
        bool CapabilityType(string type) => GenericTypes.TryParse(type, out var shape) && RetainsCapability(shape);
        string CallableResult(FunctionSyntax function) => function.IsAsync ? taskSymbol + "<" + function.ReturnType.Text + ">" : function.ReturnType.Text;
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
        // Methods are checked by the same function binder as ordinary bodies.
        // Explicit dependencies become leading synthetic parameters; the
        // emitter's service wrappers supply each instance's own values.
        var interfaceMethods = new Dictionary<string, Dictionary<string, InterfaceMethodSyntax>>(StringComparer.Ordinal);
        foreach (var item in syntax.Interfaces)
        {
            var methods = new Dictionary<string, InterfaceMethodSyntax>(StringComparer.Ordinal);
            foreach (var method in item.Methods)
            {
                if (!methods.TryAdd(method.Name.Text, method)) Fail("DS2040", method.Name, "Duplicate interface method.");
                if (!SupportedType(method.ReturnType.Text) && !OwnedTransfer(method.ReturnType.Text))
                    Fail("DS2040", method.ReturnType, "Unsupported interface result type.");
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var parameter in method.Parameters)
                {
                    if (!names.Add(parameter.Name.Text)) Fail("DS2040", parameter.Name, "Duplicate method parameter.");
                    if (!SupportedType(parameter.Type.Text)) Fail("DS2040", parameter.Type, "Unsupported interface parameter type.");
                }
                if (method.IsAsync) taskUsed = true;
            }
            interfaceMethods.Add(item.Name.Text, methods);
        }
        var serviceMethods = new Dictionary<string, Dictionary<string, InterfaceMethodSyntax>>(StringComparer.Ordinal);
        var boundServiceDeclarations = new List<BoundServiceDeclaration>();
        var syntheticFunctions = new List<FunctionSyntax>();
        var occupiedNames = new HashSet<string>(StringComparer.Ordinal);
        void CollectOccupied(object? node)
        {
            if (node is null || node is string) return;
            if (node is Token token) { occupiedNames.Add(token.Text); return; }
            if (node is System.Collections.IEnumerable sequence) { foreach (var value in sequence) CollectOccupied(value); return; }
            var nodeType = node.GetType();
            if (!nodeType.Name.EndsWith("Syntax", StringComparison.Ordinal)) return;
            foreach (var property in nodeType.GetProperties())
                if (property.GetIndexParameters().Length == 0) CollectOccupied(property.GetValue(node));
        }
        CollectOccupied(syntax);
        var syntheticNumber = 0;
        string FreshSynthetic(string prefix)
        {
            string name;
            do name = prefix + (syntheticNumber++).ToString(CultureInfo.InvariantCulture);
            while (!occupiedNames.Add(name));
            return name;
        }
        // Check every default body even when no service selects it. The
        // implicit receiver is a capability, so the ordinary closure and
        // partial-application escape checks also apply to calls through it.
        var defaultBodies = new Dictionary<(string Interface, string Method), string>();
        var defaultReceivers = new Dictionary<string, (Token Receiver, string Interface)>(StringComparer.Ordinal);
        foreach (var item in syntax.Interfaces)
        foreach (var method in item.Methods)
        {
            if (method.Body is null) continue;
            var syntheticName = FreshSynthetic("__dsharp_default_body_");
            var receiverName = FreshSynthetic("__dsharp_default_receiver_");
            var receiver = method.Name with { Text = receiverName };
            var synthetic = new FunctionSyntax(method.Name with { Text = syntheticName }, method.ReturnType, method.Body.Result.Token)
            {
                Parameters = new[] { new ParameterSyntax(receiver, item.Name) }.Concat(method.Parameters).ToArray(),
                Body = method.Body, Expression = method.Body.Result, Bindings = method.Body.Bindings,
                IsAsync = method.IsAsync
            };
            functionTypeScopes.Add(syntheticName, noTypeScope);
            syntheticFunctions.Add(synthetic);
            defaultBodies.Add((item.Name.Text, method.Name.Text), syntheticName);
            defaultReceivers.Add(syntheticName, (receiver, item.Name.Text));
        }
        foreach (var service in syntax.Services)
        {
            if (service.Interfaces.Count == 0) Fail("DS2040", service.Name, "A service must implement an interface.");
            if (!resourceNames.Contains(service.Name.Text) && service.Interfaces.Any(item => privateResourceContracts.Contains(item.Text)))
                Fail("DS2041", service.Name, "Resource contracts are not source-visible.");
            if (resourceNames.Contains(service.Name.Text))
                foreach (var operation in service.Methods)
                    if (operation.ReturnType is null || !DataType(operation.ReturnType.Text))
                        Fail("DS2041", operation.Name, "A resource operation must return immutable data.");
            var implemented = new HashSet<string>(StringComparer.Ordinal);
            var required = new Dictionary<string, InterfaceMethodSyntax>(StringComparer.Ordinal);
            var declarations = new Dictionary<string, List<(string Interface, InterfaceMethodSyntax Method)>>(StringComparer.Ordinal);
            foreach (var iface in service.Interfaces)
            {
                if (!implemented.Add(iface.Text)) Fail("DS2040", iface, "Duplicate implemented interface.");
                if (!interfaceMethods.TryGetValue(iface.Text, out var methods)) Fail("DS2040", iface, "Unknown implemented interface.");
                foreach (var method in methods!.Values)
                {
                    if (required.TryGetValue(method.Name.Text, out var prior) &&
                        (prior.IsAsync != method.IsAsync || prior.ReturnType.Text != method.ReturnType.Text ||
                         !prior.Parameters.Select(p => p.Type.Text).SequenceEqual(method.Parameters.Select(p => p.Type.Text))))
                        Fail("DS2040", iface, "Implemented interfaces have conflicting method signatures.");
                    required[method.Name.Text] = method;
                    if (!declarations.TryGetValue(method.Name.Text, out var candidates))
                        declarations.Add(method.Name.Text, candidates = new());
                    candidates.Add((iface.Text, method));
                }
            }
            var dependencyNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var dependency in service.Dependencies)
            {
                if (!dependencyNames.Add(dependency.Name.Text)) Fail("DS2040", dependency.Name, "Duplicate service dependency.");
                if (required.ContainsKey(dependency.Name.Text)) Fail("DS2040", dependency.Name, "A dependency cannot share a service method name.");
                if (!SupportedType(dependency.Type.Text) || (!DataType(dependency.Type.Text) &&
                    !interfaces.ContainsKey(dependency.Type.Text) && !services.ContainsKey(dependency.Type.Text)))
                    Fail("DS2040", dependency.Type, "Dependencies require immutable data or a service or interface reference.");
            }
            var implementations = new HashSet<string>(StringComparer.Ordinal);
            var boundMethods = new List<BoundServiceMethod>();
            foreach (var method in service.Methods)
            {
                if (!implementations.Add(method.Name.Text)) Fail("DS2040", method.Name, "Duplicate service method.");
                if (!required.TryGetValue(method.Name.Text, out var contract)) Fail("DS2040", method.Name, "Service method does not implement an interface method.");
                if (method.Parameters.Count != contract!.Parameters.Count) Fail("DS2040", method.Name, "Service method parameter count does not match the interface.");
                if (method.IsAsync && !contract.IsAsync) Fail("DS2040", method.Name, "Service method async marker does not match the interface.");
                if (method.ReturnType is not null && method.ReturnType.Text != contract.ReturnType.Text)
                    Fail("DS2040", method.ReturnType, "Service result type does not match the interface.");
                var names = new HashSet<string>(dependencyNames, StringComparer.Ordinal);
                var methodParameters = new List<ParameterSyntax>();
                for (var i = 0; i < method.Parameters.Count; i++)
                {
                    var parameter = method.Parameters[i];
                    if (!names.Add(parameter.Name.Text)) Fail("DS2040", parameter.Name, "Duplicate or shadowed service method parameter.");
                    if (parameter.Type is not null && parameter.Type.Text != contract.Parameters[i].Type.Text)
                        Fail("DS2040", parameter.Type, "Service parameter type does not match the interface.");
                    methodParameters.Add(new ParameterSyntax(parameter.Name, contract.Parameters[i].Type));
                }
                var syntheticName = FreshSynthetic("__dsharp_service_body_");
                var synthetic = new FunctionSyntax(method.Name with { Text = syntheticName }, contract.ReturnType, method.Body.Result.Token)
                {
                    Parameters = service.Dependencies.Concat(methodParameters).ToArray(),
                    Body = method.Body, Expression = method.Body.Result, Bindings = method.Body.Bindings,
                    IsAsync = contract.IsAsync
                };
                functionTypeScopes.Add(syntheticName, noTypeScope);
                syntheticFunctions.Add(synthetic);
                boundMethods.Add(new BoundServiceMethod(method.Name.Text, syntheticName,
                    contract.Parameters.Select(parameter => parameter.Type.Text).ToArray(), contract.ReturnType.Text, contract.IsAsync)
                    { IsOwnedFactory = OwnedTransfer(contract.ReturnType.Text) });
            }
            foreach (var (name, candidates) in declarations)
            {
                if (implementations.Contains(name)) continue;
                if (candidates.Count(candidate => candidate.Method.Body is not null) > 1)
                    Fail("DS2040", service.Name, "Conflicting interface defaults require an explicit service implementation.");
                if (candidates.Any(candidate => candidate.Method.Body is null))
                    Fail("DS2040", service.Name, "A required interface method is missing.");
                var selected = candidates[0];
                var method = selected.Method;
                boundMethods.Add(new BoundServiceMethod(name, defaultBodies[(selected.Interface, name)],
                    method.Parameters.Select(parameter => parameter.Type.Text).ToArray(), method.ReturnType.Text, method.IsAsync)
                    { DefaultInterface = selected.Interface, IsOwnedFactory = OwnedTransfer(method.ReturnType.Text) });
            }
            serviceMethods.Add(service.Name.Text, required);
            boundServiceDeclarations.Add(new BoundServiceDeclaration(service.Name.Text,
                service.Dependencies.Select(parameter => parameter.Type.Text).ToArray(), service.Interfaces.Select(iface => iface.Text).ToArray(), boundMethods));
        }
        // Trigger contracts intentionally admit only a structurally checked subset. Check
        // instantiated members, including unused generic arguments, rather than
        // assuming that any otherwise valid immutable type is serializable.
        bool TriggerData(TypeShape root)
        {
            var active = new Dictionary<string, string>(StringComparer.Ordinal);
            var complete = new HashSet<string>(StringComparer.Ordinal);
            bool Visit(TypeShape shape, int depth)
            {
                if (depth > 128 || shape.IsNullable || shape.IsFunction || shape.IsTuple) return false;
                if (shape.Name is "sbyte" or "byte" or "short" or "ushort" or "int" or "uint" or "long" or "ulong" or
                    "decimal" or "float32" or "float" or "bool" or "string" or "char" or "Unit")
                    return shape.Arguments.Count == 0;
                if (shape.Name == resultSymbol || shape.Name == optionSymbol) return false;
                records.TryGetValue(shape.Name, out var record);
                unions.TryGetValue(shape.Name, out var union);
                if (record is null && union is null) return false;
                var parameters = record is not null ? record.TypeParameters : union!.TypeParameters;
                if (shape.Arguments.Count != parameters.Count || !shape.Arguments.All(argument => Visit(argument, depth + 1))) return false;
                var key = shape.ToString();
                if (complete.Contains(key)) return true;
                if (active.TryGetValue(shape.Name, out var previous)) return previous == key;
                active.Add(shape.Name, key);
                try
                {
                    var substitutions = parameters.Select((parameter, i) => (parameter.Text, Value: shape.Arguments[i].ToString()))
                        .ToDictionary(pair => pair.Text, pair => pair.Value, StringComparer.Ordinal);
                    var fields = record is not null ? record.Fields.Select(field => field.Type.Text) :
                        union!.Cases.SelectMany(item => item.PayloadTypes.Select(payload => payload.Text));
                    foreach (var field in fields)
                        if (!GenericTypes.TrySubstitute(field, substitutions, out var instantiated) ||
                            !GenericTypes.TryParse(instantiated, out var nested) || !Visit(nested, depth + 1)) return false;
                    complete.Add(key);
                    return true;
                }
                finally { active.Remove(shape.Name); }
            }
            return Visit(root, 0);
        }
        var boundActors = new List<BoundActorDeclaration>();
        var actorMethodsByBody = new Dictionary<string, (ActorDeclarationSyntax Actor, IReadOnlyDictionary<string, (ActorMethodSyntax Method, string Id)> Methods)>(StringComparer.Ordinal);
        foreach (var actor in syntax.Actors)
        {
            var actorMethods = new Dictionary<string, (ActorMethodSyntax Method, string Id)>(StringComparer.Ordinal);
            foreach (var method in actor.Methods)
                if (!actorMethods.TryAdd(method.Name.Text, (method, FreshSynthetic("__dsharp_actor_body_"))))
                    Fail("DS2042", method.Name, "Actor methods cannot be duplicated or overloaded.");
            if (!actorMethods.TryGetValue("Handle", out var handle) || handle.Method.Parameters.Count != 1)
                Fail("DS2042", actor.Name, "An actor requires exactly one Handle with one typed message parameter.");
            var expected = handle.Method.Parameters[0].Type.Text;
            var expectedHandle = $"Expected Handle({ActorDisplayType(expected)}): {ActorDisplayType(handle.Method.ReturnType.Text)} declared at {handle.Method.Name.OriginPath ?? path}({handle.Method.Name.Span.Line},{handle.Method.Name.Span.Column}).";
            var httpKeywords = actor.HttpBindings.Select(binding => binding.Keyword)
                .Concat(actor.HttpPlainBindings.Select(binding => binding.Keyword)).OrderBy(token => token.Span.Start).ToArray();
            if (httpKeywords.Length > 1)
                Fail("DS2046", httpKeywords[1], $"Only one HTTP descriptor is allowed per actor. {expectedHandle}");
            string ValidateTriggerType(Token messageType, string kind, string diagnostic)
            {
                var identity = messageType.Text;
                if (!SupportedType(identity) || !GenericTypes.TryParse(identity, out var messageShape) ||
                    messageShape.IsNullable || (messageShape.Name != "Unit" &&
                    (!records.ContainsKey(messageShape.Name) && !unions.ContainsKey(messageShape.Name))) ||
                    !TriggerData(messageShape))
                    Fail(diagnostic, messageType, $"{kind} input must be Unit or a supported record or union with only supported data fields. {expectedHandle}");
                if (identity != expected)
                    Fail(diagnostic, messageType, $"{kind} input must match Handle. {expectedHandle}");
                return identity;
            }
            string DecodeTriggerLiteral(Token token, string diagnostic, string invalidMessage, bool preserveDecodeCode)
            {
                // Public Bind callers can supply a syntax tree without parsing it.
                var literal = token.Text;
                var validLiteral = literal.Length >= 2 && literal[0] == '"' && literal[^1] == '"';
                for (var i = 1; validLiteral && i < literal.Length - 1; i++)
                {
                    if (literal[i] is '"' or '\n' or '\r') validLiteral = false;
                    else if (literal[i] == '\\' && (++i >= literal.Length - 1 || literal[i] is '\n' or '\r')) validLiteral = false;
                }
                if (!validLiteral) Fail(diagnostic, token, $"{invalidMessage} {expectedHandle}");
                try { return DecodeText(token, token.OriginPath ?? path); }
                catch (SourceError error)
                {
                    throw new SourceError(error.Diagnostic with
                    {
                        Code = preserveDecodeCode ? error.Diagnostic.Code : diagnostic,
                        Message = $"{error.Diagnostic.Message} {expectedHandle}"
                    });
                }
            }
            var queueBindings = new List<BoundQueueBinding>();
            var queueNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var binding in actor.QueueBindings)
            {
                var queueName = DecodeTriggerLiteral(binding.QueueName, "DS2043", "Queue names require an ordinary string literal.", true);
                if (queueName.Length == 0 || !queueNames.Add(queueName))
                    Fail("DS2043", binding.QueueName, $"Queue names must be nonempty and unique within an actor. {expectedHandle}");
                var identity = ValidateTriggerType(binding.MessageType, "Queue", "DS2043");
                queueBindings.Add(new BoundQueueBinding(queueName, ActorDisplayType(identity), binding.Keyword.OriginPath ?? path,
                    binding.Keyword.Span, binding.MessageType.Span, binding.QueueName.Span) { TypeIdentity = identity });
            }
            var eventBindings = new List<BoundEventBinding>();
            var eventTypes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var binding in actor.EventBindings)
            {
                var identity = ValidateTriggerType(binding.MessageType, "Event", "DS2044");
                if (!eventTypes.Add(identity))
                    Fail("DS2044", binding.MessageType, $"An event message contract cannot be duplicated within an actor. {expectedHandle}");
                eventBindings.Add(new BoundEventBinding(ActorDisplayType(identity), binding.Keyword.OriginPath ?? path,
                    binding.Keyword.Span, binding.MessageType.Span) { TypeIdentity = identity });
            }
            var cronBindings = new List<BoundCronBinding>();
            var cronNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var binding in actor.CronBindings)
            {
                var scheduleName = DecodeTriggerLiteral(binding.ScheduleName, "DS2045", "Cron names require an ordinary string literal.", false);
                var expression = DecodeTriggerLiteral(binding.Expression, "DS2045", "Cron expressions require an ordinary string literal.", false);
                if (scheduleName.Length == 0 || !cronNames.Add(scheduleName))
                    Fail("DS2045", binding.ScheduleName, $"Cron names must be nonempty and unique within an actor. {expectedHandle}");
                if (expression.Length == 0)
                    Fail("DS2045", binding.Expression, $"Cron expressions must be nonempty. {expectedHandle}");
                var identity = ValidateTriggerType(binding.MessageType, "Cron", "DS2045");
                cronBindings.Add(new BoundCronBinding(scheduleName, expression, ActorDisplayType(identity), binding.Keyword.OriginPath ?? path,
                    binding.Keyword.Span, binding.MessageType.Span, binding.ScheduleName.Span, binding.Expression.Span) { TypeIdentity = identity });
            }
            var dependencies = new HashSet<string>(StringComparer.Ordinal);
            foreach (var dependency in actor.Dependencies)
            {
                if (!dependencies.Add(dependency.Name.Text) || actorMethods.ContainsKey(dependency.Name.Text))
                    Fail("DS2042", dependency.Name, "Duplicate or conflicting actor dependency name.");
                if (!interfaces.ContainsKey(dependency.Type.Text) && !resourceContracts.ContainsKey(dependency.Type.Text) ||
                    privateResourceContracts.Contains(dependency.Type.Text))
                    Fail("DS2042", dependency.Type, "Actor dependencies require a declared resource or interface.");
            }
            if (!SupportedType(handle.Method.Parameters[0].Type.Text) || !DataType(handle.Method.Parameters[0].Type.Text))
                Fail("DS2042", handle.Method.Parameters[0].Type, "Actor Handle input must be immutable data or Unit.");
            if (!SupportedType(handle.Method.ReturnType.Text) || !DataType(handle.Method.ReturnType.Text))
                Fail("DS2042", handle.Method.ReturnType, "Actor Handle result must be immutable data or Unit.");
            var loweredDependencies = actor.Dependencies.Select(dependency => resourceContracts.TryGetValue(dependency.Type.Text, out var contract)
                ? dependency with { Type = dependency.Type with { Text = contract } } : dependency).ToArray();
            foreach (var (_, pair) in actorMethods)
            {
                var names = new HashSet<string>(dependencies, StringComparer.Ordinal);
                foreach (var parameter in pair.Method.Parameters)
                {
                    if (!names.Add(parameter.Name.Text) || actorMethods.ContainsKey(parameter.Name.Text))
                        Fail("DS2042", parameter.Name, "Actor parameters cannot shadow dependencies or methods.");
                    if (!SupportedType(parameter.Type.Text)) Fail("DS2042", parameter.Type, "Unsupported actor method parameter.");
                }
                if (!SupportedType(pair.Method.ReturnType.Text)) Fail("DS2042", pair.Method.ReturnType, "Unsupported actor method result.");
                var synthetic = new FunctionSyntax(pair.Method.Name with { Text = pair.Id }, pair.Method.ReturnType, pair.Method.Body.Result.Token)
                {
                    Parameters = loweredDependencies.Concat(pair.Method.Parameters).ToArray(),
                    Body = pair.Method.Body, Expression = pair.Method.Body.Result, Bindings = pair.Method.Body.Bindings,
                    IsAsync = pair.Method.Name.Text == "Handle" || pair.Method.IsAsync
                };
                functionTypeScopes.Add(pair.Id, noTypeScope);
                syntheticFunctions.Add(synthetic);
                actorMethodsByBody.Add(pair.Id, (actor, actorMethods));
            }
            var displayName = syntax.TypeDisplayNames.TryGetValue(actor.Name.Text, out var display) ? display : actor.Name.Text;
            boundActors.Add(new BoundActorDeclaration(displayName, ActorDisplayType(handle.Method.Parameters[0].Type.Text),
                ActorDisplayType(handle.Method.ReturnType.Text), actor.Dependencies.Select(dependency =>
                    new BoundActorDependency(dependency.Name.Text, ActorDisplayType(dependency.Type.Text))).ToArray(),
                actor.Name.OriginPath ?? path, actor.Name.Span, handle.Method.Name.Span, handle.Id)
                { QueueBindings = queueBindings, EventBindings = eventBindings, CronBindings = cronBindings });
        }
        syntax = syntax with { Functions = syntax.Functions.Concat(syntheticFunctions).ToArray() };
        var signatures = new Dictionary<string, FunctionSyntax>(StringComparer.Ordinal);
        var ownedFactories = new HashSet<string>(StringComparer.Ordinal);
        foreach (var function in syntax.Functions)
        {
            var typeParameters = Parameters(function.TypeParameters);
            if (receiptRelayEntries.Contains(function.Name.Text) && function.Parameters.Any(parameter => !DataType(parameter.Type.Text)))
                Fail("DS2041", function.Name, "A receipt relay cannot carry a capability or execution value across acceptance.");
            if (function.IsAsync && function.InferReturnType) Fail("DS2027", function.Name, "Async functions require an explicit logical result type.");
            if (!signatures.TryAdd(function.Name.Text, function)) Fail("DS2001", function.Name, "Duplicate function declaration.");
            if (services.ContainsKey(function.Name.Text)) Fail("DS2001", function.Name, "A function and service constructor cannot share a name.");
            if (records.TryGetValue(function.Name.Text, out var conflictingRecord))
                Fail("DS2015", function.Name.Span.Start > conflictingRecord.Name.Span.Start ? function.Name : conflictingRecord.Name,
                    "A function and record constructor cannot share a name.");
            var isOwnedFactory = !function.InferReturnType && OwnedTransfer(function.ReturnType.Text);
            if (isOwnedFactory)
            {
                if (function.TypeParameters.Count != 0)
                    Fail("DS2041", function.ReturnType, "An owned resource factory must be nongeneric.");
                ownedFactories.Add(function.Name.Text);
            }
            else if (!function.InferReturnType && !SupportedType(function.ReturnType.Text, typeParameters)) Fail("DS2003", function.ReturnType, "Unsupported return type or unconstrained nullable type parameter.");
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var parameter in function.Parameters)
            {
                if (!names.Add(parameter.Name.Text)) Fail("DS2010", parameter.Name, "Duplicate parameter.");
                if (!SupportedType(parameter.Type.Text, typeParameters) &&
                    !(actorMethodsByBody.ContainsKey(function.Name.Text) && privateResourceContracts.Contains(parameter.Type.Text)))
                    Fail("DS2011", parameter.Type, "Unsupported parameter type or unconstrained nullable type parameter.");
            }
        }
        // Resolve HTTP contracts only after scoped source signatures are known.
        // The source-function allowlist excludes lowered extensions and generated bodies.
        for (var actorIndex = 0; actorIndex < syntax.Actors.Count; actorIndex++)
        {
            var actor = syntax.Actors[actorIndex];
            if (actor.HttpBindings.Count == 0 && actor.HttpPlainBindings.Count == 0) continue;
            var binding = actor.HttpBindings.FirstOrDefault();
            var plain = actor.HttpPlainBindings.FirstOrDefault();
            var keyword = binding?.Keyword ?? plain!.Keyword;
            var responseToken = binding?.ResponseType ?? plain!.ResponseType;
            var binderToken = binding?.Binder ?? plain!.Binder;
            var bindingMapperToken = binding?.BindingMapper ?? plain!.BindingMapper;
            var successMapperToken = binding?.SuccessMapper ?? plain!.SuccessMapper;
            var binderIdentity = binding?.BinderIdentity ?? plain?.BinderIdentity;
            var bindingMapperIdentity = binding?.BindingMapperIdentity ?? plain?.BindingMapperIdentity;
            var successMapperIdentity = binding?.SuccessMapperIdentity ?? plain?.SuccessMapperIdentity;
            var referencesResolved = binding?.ReferencesResolved ?? plain!.ReferencesResolved;
            var handle = actor.Methods.Single(method => method.Name.Text == "Handle");
            var input = handle.Parameters[0].Type.Text;
            var expectedHandle = $"Expected Handle({ActorDisplayType(input)}): {ActorDisplayType(handle.ReturnType.Text)} declared at {handle.Name.OriginPath ?? path}({handle.Name.Span.Line},{handle.Name.Span.Column}).";
            void HttpError(Token token, string message) => Fail("DS2046", token, $"{message} {expectedHandle}");
            bool ImmutableData(string type) => SupportedType(type) && DataType(type);
            var response = responseToken.Text;
            if (!ImmutableData(response)) HttpError(responseToken, "HTTP response type must be supported immutable data.");
            if (!GenericTypes.TryParse(handle.ReturnType.Text, out var resultShape))
                HttpError(keyword, "HTTP Invoke requires a supported Handle result.");
            var isResult = resultShape.Name == resultSymbol;
            if (isResult && (resultShape.IsNullable || resultShape.Arguments.Count != 2))
                HttpError(keyword, "HTTP Invoke requires a nonnullable intrinsic Result.");
            if (isResult && binding is null)
                HttpError(keyword, "HTTP Invoke requires a business-error mapper for an intrinsic Result.");
            if (!isResult && binding is not null)
                HttpError(binding.BusinessMapper, "HTTP Invoke cannot have a business-error mapper for a plain Handle result.");
            var valueType = isResult ? resultShape.Arguments[0].ToString() : handle.ReturnType.Text;
            var businessErrorType = isResult ? resultShape.Arguments[1].ToString() : null;
            FunctionSyntax SourceFunction(Token token, string? resolved, bool mapper)
            {
                // Actor-local names take precedence over module functions in
                // ordinary actor expressions. A descriptor cannot reference an
                // actor helper or dependency as a host callback.
                var separator = token.Text.IndexOf('.');
                var rootName = separator < 0 ? token.Text : token.Text[..separator];
                if (actor.Methods.Any(method => method.Name.Text == rootName) ||
                    actor.Dependencies.Any(dependency => dependency.Name.Text == rootName))
                    HttpError(token, "HTTP functions cannot name an actor-local helper or dependency.");
                var identity = referencesResolved ? resolved : token.Text;
                if (identity is null || !sourceHttpFunctions.Contains(identity) || !signatures.TryGetValue(identity, out var function))
                {
                    HttpError(token, "HTTP descriptors require a visible ordinary module function.");
                    return null!;
                }
                if (function.TypeParameters.Count != 0 || function.InferReturnType || function.Parameters.Count != 1 ||
                    mapper && function.IsAsync || !ImmutableData(function.Parameters[0].Type.Text) || !ImmutableData(function.ReturnType.Text))
                    HttpError(token, "HTTP functions require an explicit, non-generic, one-argument immutable-data signature; mappers must be synchronous.");
                return function;
            }
            var decoder = SourceFunction(binderToken, binderIdentity, false);
            if (!GenericTypes.TryParse(decoder.ReturnType.Text, out var decoded) || decoded.IsNullable ||
                decoded.Name != resultSymbol || decoded.Arguments.Count != 2 || decoded.Arguments[0].ToString() != input)
                HttpError(binderToken, "HTTP binder must return intrinsic Result<HandleInput, BindingError>.");
            var bindingErrorType = decoded.Arguments[1].ToString();
            void CheckMapper(Token token, string? identity, string argumentType)
            {
                var mapper = SourceFunction(token, identity, true);
                if (mapper.Parameters[0].Type.Text != argumentType || mapper.ReturnType.Text != response)
                    HttpError(token, $"HTTP mapper must accept {ActorDisplayType(argumentType)} and return {ActorDisplayType(response)}.");
            }
            CheckMapper(bindingMapperToken, bindingMapperIdentity, bindingErrorType);
            CheckMapper(successMapperToken, successMapperIdentity, valueType);
            string Identity(Token token, string? resolved) => referencesResolved ? resolved! : token.Text;
            if (binding is not null && businessErrorType is not null)
            {
                CheckMapper(binding.BusinessMapper, binding.BusinessMapperIdentity, businessErrorType);
                var http = new BoundHttpBinding(ActorDisplayType(response), binderToken.Text, bindingMapperToken.Text,
                    successMapperToken.Text, binding.BusinessMapper.Text, ActorDisplayType(bindingErrorType),
                    ActorDisplayType(valueType), ActorDisplayType(businessErrorType), decoder.IsAsync, keyword.OriginPath ?? path,
                    keyword.Span, responseToken.Span, binderToken.Span, bindingMapperToken.Span,
                    successMapperToken.Span, binding.BusinessMapper.Span)
                {
                    BinderIdentity = Identity(binderToken, binderIdentity),
                    BindingMapperIdentity = Identity(bindingMapperToken, bindingMapperIdentity),
                    SuccessMapperIdentity = Identity(successMapperToken, successMapperIdentity),
                    BusinessMapperIdentity = Identity(binding.BusinessMapper, binding.BusinessMapperIdentity)
                };
                boundActors[actorIndex] = boundActors[actorIndex] with { HttpBindings = new[] { http } };
            }
            else
            {
                var http = new BoundHttpPlainBinding(ActorDisplayType(response), binderToken.Text, bindingMapperToken.Text,
                    successMapperToken.Text, ActorDisplayType(bindingErrorType), ActorDisplayType(valueType), decoder.IsAsync,
                    keyword.OriginPath ?? path, keyword.Span, responseToken.Span, binderToken.Span, bindingMapperToken.Span, successMapperToken.Span)
                {
                    BinderIdentity = Identity(binderToken, binderIdentity),
                    BindingMapperIdentity = Identity(bindingMapperToken, bindingMapperIdentity),
                    SuccessMapperIdentity = Identity(successMapperToken, successMapperIdentity)
                };
                boundActors[actorIndex] = boundActors[actorIndex] with { HttpPlainBindings = new[] { http } };
            }
        }
        foreach (var unionCase in standardResult.Cases)
            if (!cases.ContainsKey(unionCase.Name.Text) && !signatures.ContainsKey(unionCase.Name.Text) && !records.ContainsKey(unionCase.Name.Text) && !services.ContainsKey(unionCase.Name.Text))
                cases.Add(unionCase.Name.Text, new() { (standardResult, unionCase) });
        foreach (var unionCase in standardOption.Cases)
            if (!cases.ContainsKey(unionCase.Name.Text) && !signatures.ContainsKey(unionCase.Name.Text) && !records.ContainsKey(unionCase.Name.Text) && !services.ContainsKey(unionCase.Name.Text))
                cases.Add(unionCase.Name.Text, new() { (standardOption, unionCase) });
        // Read the shipped D# source and register its declarations as ordinary
        // prelude functions. Source declarations and locals retain precedence;
        // within this compiler-owned source, Result means the intrinsic union.
        var resultHelpers = new Dictionary<string, FunctionSyntax>(StringComparer.Ordinal);
        var usedResultHelpers = new HashSet<string>(StringComparer.Ordinal);
        const string preludePath = "<standard:Result.ds>";
        using var preludeStream = typeof(Binder).Assembly.GetManifestResourceStream("DSharp.Compiler.Prelude.Result.ds")
            ?? throw new InvalidOperationException("The compiler's embedded Result prelude is missing.");
        using var preludeReader = new StreamReader(preludeStream, Encoding.UTF8);
        var preludeSource = preludeReader.ReadToEnd();
        var preludeTokens = Lexer.Lex(preludeSource, preludePath);
        var prelude = new Parser(preludeTokens, preludePath).Parse();
        // Compiler-owned helpers need distinct internal identities when a user
        // declaration shadows their public name. Reserve every source token so
        // the generated identity cannot be captured by a source local or name.
        var reservedPreludeNames = new HashSet<string>(preludeTokens.Select(token => token.Text), StringComparer.Ordinal);
        void ReserveTokens(object? node)
        {
            if (node is null || node is string) return;
            if (node is Token token) { reservedPreludeNames.Add(token.Text); return; }
            if (node is System.Collections.IEnumerable sequence)
            {
                foreach (var item in sequence) ReserveTokens(item);
                return;
            }
            var type = node.GetType();
            if (!type.Name.EndsWith("Syntax", StringComparison.Ordinal)) return;
            foreach (var property in type.GetProperties())
                if (property.GetIndexParameters().Length == 0) ReserveTokens(property.GetValue(node));
        }
        ReserveTokens(syntax);
        var preludeNames = new Dictionary<string, string>(StringComparer.Ordinal);
        // Compiler-private implementations share an existing public prelude
        // name but cannot be named from user source.
        var resultOverloadSources = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["__dsharp_result_map_task"] = "map",
            ["__dsharp_result_maperror_task"] = "mapError",
            ["__dsharp_result_tap_task"] = "tap",
            ["__dsharp_result_taperror_task"] = "tapError",
            ["__dsharp_result_andthen_async"] = "andThen",
            ["__dsharp_result_andthen_task"] = "andThen",
            ["__dsharp_result_andthen_task_async"] = "andThen"
        };
        var resultOverloads = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var declaration in prelude.Functions)
        {
            var sourceName = declaration.Name.Text;
            if (preludeNames.ContainsKey(sourceName)) Fail("DS2001", declaration.Name, "Duplicate prelude function declaration.");
            var helperName = sourceName;
            // This implementation is a compiler-private overload, not a new
            // source-level name. Pick an identity absent from all source tokens.
            if (resultOverloadSources.ContainsKey(sourceName) || signatures.ContainsKey(helperName) || cases.ContainsKey(helperName) || records.ContainsKey(helperName) || unions.ContainsKey(helperName) || services.ContainsKey(helperName))
            {
                helperName = "__dsharp_prelude_" + sourceName;
                while (!reservedPreludeNames.Add(helperName)) helperName += "_";
            }
            if (resultOverloadSources.TryGetValue(sourceName, out var publicName))
            {
                if (!resultOverloads.TryGetValue(publicName, out var overloads))
                    resultOverloads.Add(publicName, overloads = new List<string>());
                overloads.Add(helperName);
            }
            preludeNames.Add(sourceName, helperName);
            var scope = TypeScope(declaration.TypeParameters);
            functionTypeScopes.Add(helperName, scope);
            Token PreludeType(Token token)
            {
                if (!GenericTypes.TryParse(token.Text, out var shape)) return token;
                TypeShape Rebind(TypeShape value) => value with
                {
                    Name = scope.TryGetValue(value.Name, out var symbol) ? symbol : value.Name == "Result" ? resultSymbol : value.Name == "Task" ? taskSymbol : value.Name,
                    Arguments = value.Arguments.Select(Rebind).ToArray()
                };
                return token with { Text = Rebind(shape).ToString() };
            }
            var helper = declaration with
            {
                Name = declaration.Name with { Text = helperName },
                TypeParameters = declaration.TypeParameters.Select(parameter => ScopedParameter(parameter, scope)).ToArray(),
                Parameters = declaration.Parameters.Select(parameter => parameter with { Type = PreludeType(parameter.Type) }).ToArray(),
                ReturnType = PreludeType(declaration.ReturnType)
            };
            var helperParameters = Parameters(helper.TypeParameters);
            if (helper.InferReturnType || !SupportedType(helper.ReturnType.Text, helperParameters))
                Fail("DS2003", helper.ReturnType, "Unsupported prelude return type.");
            var helperNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var parameter in helper.Parameters)
            {
                if (!helperNames.Add(parameter.Name.Text)) Fail("DS2010", parameter.Name, "Duplicate parameter.");
                if (!SupportedType(parameter.Type.Text, helperParameters)) Fail("DS2011", parameter.Type, "Unsupported prelude parameter type.");
            }
            signatures.Add(helperName, helper);
            resultHelpers.Add(helperName, helper);
        }
        // Intrinsic collection method names take precedence over extensions,
        // even if available context cannot establish an intrinsic method type.
        // Share the rule between inference proof and ordinary member binding.
        bool CollectionMethodCollision(TypeShape shape, string member) => !shape.IsNullable &&
            (shape.Name == listSymbol && member is "length" or "append" or "get" or "filter" or "map" or "fold" ||
             shape.Name == mapSymbol && member is "count" or "contains" or "get" or "set" ||
             shape.Name == setSymbol && member is "count" or "contains" or "add");
        // Return inference is deliberately local to a checked, acyclic call
        // graph. Distinguish calls to lexical function values from calls to
        // module functions; the former cannot establish a static call graph.
        // Keep the proof tied to the actual syntax node and selected extension.
        // A later inference pass must not turn unrelated method values into
        // statically known calls merely because they have a matching type.
        var provenExtensionPipes = new Dictionary<FieldAccessSyntax, string>(ReferenceEqualityComparer.Instance);
        (HashSet<string> Calls, bool Indirect, HashSet<string> StrictCalls, bool UnknownReceiver) AnalyzeDependencies(FunctionSyntax source, bool includeUnreachable = false, bool includeDeadBranches = false)
        {
            var calls = new HashSet<string>(StringComparer.Ordinal);
            var strictCalls = new HashSet<string>(StringComparer.Ordinal);
            var indirect = false;
            var strictContext = false;
            var unknownReceiver = false;
            void AddCall(string name)
            {
                calls.Add(name);
                if (strictContext) strictCalls.Add(name);
            }
            var visible = source.Parameters.Select(parameter => parameter.Name.Text).ToHashSet(StringComparer.Ordinal);
            // Keep only facts proved from declarations or syntax. These facts
            // follow lexical blocks, never guessed from a bare identifier.
            var knownTypes = source.Parameters.ToDictionary(parameter => parameter.Name.Text,
                parameter => parameter.Type.Text, StringComparer.Ordinal);
            var scopedResources = new HashSet<string>(StringComparer.Ordinal);
            string DependencyName(string name, HashSet<string> names)
            {
                if (!names.Contains(name) && actorMethodsByBody.TryGetValue(source.Name.Text, out var actorContext) &&
                    actorContext.Methods.TryGetValue(name, out var actorMethod)) return actorMethod.Id;
                return resultHelpers.ContainsKey(source.Name.Text) && !names.Contains(name) && preludeNames.TryGetValue(name, out var resolved) ? resolved : name;
            }
            void PatternNames(PatternSyntax pattern, HashSet<string> names)
            {
                if (pattern is NamePatternSyntax name) names.Add(name.Name.Text);
                else if (pattern is CasePatternSyntax unionCase)
                    foreach (var argument in unionCase.Arguments) PatternNames(argument, names);
            }
            // Conservative syntactic subsumption is sufficient to avoid
            // inventing call edges from obvious duplicate/dead match arms;
            // typed pattern usefulness is checked later during binding.
            bool IntegerPatternValue(LiteralPatternSyntax literal, out System.Numerics.BigInteger value)
            {
                value = default;
                if (!IntegerLiterals.TryParse(literal.Literal.Text, out var magnitude, out _)) return false;
                value = new System.Numerics.BigInteger(magnitude);
                if (literal.Minus is not null) value = -value;
                return true;
            }
            bool DecimalPatternValue(LiteralPatternSyntax literal, out decimal value)
            {
                value = default;
                if (!DecimalLiterals.TryParse(literal.Literal.Text, out var magnitude)) return false;
                value = new decimal(unchecked((int)magnitude.Low), unchecked((int)magnitude.Middle),
                    unchecked((int)magnitude.High), literal.Minus is not null, magnitude.Scale);
                return true;
            }
            bool Float32PatternValue(LiteralPatternSyntax literal, out float value)
            {
                value = default;
                if (!Float32Literals.TryParse(literal.Literal.Text, out var bits)) return false;
                value = BitConverter.Int32BitsToSingle(unchecked((int)bits));
                if (literal.Minus is not null) value = -value;
                return true;
            }
            bool Float64PatternValue(LiteralPatternSyntax literal, out double value)
            {
                value = default;
                if (!Float64Literals.TryParse(literal.Literal.Text, out var bits)) return false;
                value = BitConverter.Int64BitsToDouble(unchecked((long)bits));
                if (literal.Minus is not null) value = -value;
                return true;
            }
            bool SameLiteral(LiteralPatternSyntax a, LiteralPatternSyntax b) =>
                IntegerPatternValue(a, out var first) && IntegerPatternValue(b, out var second) ? first == second :
                DecimalPatternValue(a, out var decimalFirst) && DecimalPatternValue(b, out var decimalSecond) ? decimalFirst == decimalSecond :
                Float32PatternValue(a, out var floatFirst) && Float32PatternValue(b, out var floatSecond) ? floatFirst == floatSecond :
                Float64PatternValue(a, out var doubleFirst) && Float64PatternValue(b, out var doubleSecond) ? doubleFirst == doubleSecond :
                a.Literal.Text == b.Literal.Text && (a.Minus is null) == (b.Minus is null);
            bool Subsumes(PatternSyntax prior, PatternSyntax next) => (prior, next) switch
            {
                (WildcardPatternSyntax, _) => true,
                (NullPatternSyntax, NullPatternSyntax) => true,
                (UnitPatternSyntax, UnitPatternSyntax) => true,
                (NamePatternSyntax a, NamePatternSyntax b) => a.Name.Text == b.Name.Text,
                (LiteralPatternSyntax a, LiteralPatternSyntax b) => SameLiteral(a, b),
                (CasePatternSyntax a, CasePatternSyntax b) => a.Qualifier?.Text == b.Qualifier?.Text && a.Name.Text == b.Name.Text &&
                    a.Arguments.Count == b.Arguments.Count && a.Arguments.Zip(b.Arguments).All(pair => Subsumes(pair.First, pair.Second)),
                _ => false
            };
            bool NeverBlock(BlockSyntax block)
            {
                var statements = block.Statements.Count > 0 ? block.Statements :
                    block.Bindings.Select(binding => (StatementSyntax)new LetStatementSyntax(binding)).ToArray();
                foreach (var statement in statements)
                {
                    if (statement is ReturnStatementSyntax) return true;
                    if (statement is LetStatementSyntax declaration &&
                        NeverExpression(declaration.Binding.Expression ?? new ValueSyntax(declaration.Binding.Initializer))) return true;
                    if (statement is TupleLetStatementSyntax tupleDeclaration && NeverExpression(tupleDeclaration.Value)) return true;
                    if (statement is ExpressionStatementSyntax evaluated && NeverExpression(evaluated.Expression)) return true;
                }
                return NeverExpression(block.Result);
            }
            bool NeverExpression(ExpressionSyntax expression) => expression switch
            {
                GroupSyntax group => NeverExpression(group.Inner),
                TupleSyntax tuple => tuple.Elements.Any(NeverExpression),
                ListSyntax list => list.Elements.Any(NeverExpression),
                InterpolationSyntax interpolation => interpolation.Holes.Any(NeverExpression),
                ForSyntax loop => NeverExpression(loop.Source),
                UsingSyntax owned => NeverExpression(owned.Acquisition) || NeverBlock(owned.Body),
                AwaitSyntax awaited => NeverExpression(awaited.Operand),
                PropagateSyntax propagation => NeverExpression(propagation.Operand),
                IfSyntax conditional => NeverExpression(conditional.Condition) ||
                    (LiteralBool(conditional.Condition) == true ? NeverBlock(conditional.Then) :
                     LiteralBool(conditional.Condition) == false ? NeverBlock(conditional.Else) :
                     conditional.HasElse && NeverBlock(conditional.Then) && NeverBlock(conditional.Else)),
                MatchSyntax match => NeverExpression(match.Scrutinee) || match.Arms.All(arm =>
                    arm.Guard is not null && NeverExpression(arm.Guard) || NeverExpression(arm.Value)),
                CallSyntax call => call.Arguments.Any(NeverExpression),
                InvokeSyntax invoke => NeverExpression(invoke.Receiver) || invoke.Arguments.Any(NeverExpression),
                MemberCallSyntax member => NeverExpression(member.Receiver) || member.Arguments.Any(NeverExpression),
                // Arguments of a conditional call run only on the present path.
                // Even an unconditionally returning argument cannot make the
                // entire expression non-completing when the receiver is null.
                ConditionalMemberCallSyntax member => NeverExpression(member.Receiver),
                RecordConstructionSyntax record => record.Fields.Any(field => NeverExpression(field.Value)),
                RecordUpdateSyntax update => NeverExpression(update.Receiver) || update.Fields.Any(field => NeverExpression(field.Value)),
                FieldAccessSyntax access => NeverExpression(access.Receiver),
                ConditionalFieldAccessSyntax access => NeverExpression(access.Receiver),
                UnarySyntax unary => NeverExpression(unary.Operand),
                BinarySyntax binary => NeverExpression(binary.Left) ||
                    (binary.Operator.Text is not ("&&" or "||") ||
                     binary.Operator.Text == "&&" && LiteralBool(binary.Left) == true ||
                     binary.Operator.Text == "||" && LiteralBool(binary.Left) == false) && NeverExpression(binary.Right),
                CoalesceSyntax coalesce => NeverExpression(coalesce.Left),
                PipeSyntax pipe => NeverExpression(pipe.Left) || NeverExpression(pipe.Right),
                _ => false
            };
            bool? LiteralBool(ExpressionSyntax expression) => expression switch
            {
                GroupSyntax group => LiteralBool(group.Inner),
                ValueSyntax { Value.Text: "true" } => true,
                ValueSyntax { Value.Text: "false" } => false,
                _ => null
            };
            void Block(BlockSyntax block, HashSet<string> outer, string? expectedBlockType = null)
            {
                var names = new HashSet<string>(outer, StringComparer.Ordinal);
                var previousTypes = new Dictionary<string, string>(knownTypes, StringComparer.Ordinal);
                var statements = block.Statements.Count > 0 ? block.Statements :
                    block.Bindings.Select(binding => (StatementSyntax)new LetStatementSyntax(binding)).ToArray();
                try
                {
                    foreach (var statement in statements)
                    {
                        switch (statement)
                        {
                            case LetStatementSyntax declaration:
                                var initializer = declaration.Binding.Expression ?? new ValueSyntax(declaration.Binding.Initializer);
                                Expression(initializer, names, declaration.Binding.Annotation?.Text);
                                var fact = declaration.Binding.Annotation is { } annotation
                                    ? ScopedType(annotation, functionTypeScopes[source.Name.Text]).Text
                                    : StaticType(initializer, names);
                                if (fact is not null) knownTypes[declaration.Binding.Name.Text] = fact;
                                else knownTypes.Remove(declaration.Binding.Name.Text);
                                names.Add(declaration.Binding.Name.Text);
                                if (!includeUnreachable && NeverExpression(initializer)) return;
                                break;
                            case TupleLetStatementSyntax tupleDeclaration:
                                Expression(tupleDeclaration.Value, names);
                                foreach (var name in tupleDeclaration.Pattern.Names) { names.Add(name.Text); knownTypes.Remove(name.Text); }
                                if (!includeUnreachable && NeverExpression(tupleDeclaration.Value)) return;
                                break;
                            case ReturnStatementSyntax returned: Expression(returned.Value, names, expectedBlockType); if (!includeUnreachable) return; break;
                            case ExpressionStatementSyntax evaluated:
                                Expression(evaluated.Expression, names);
                                if (!includeUnreachable && NeverExpression(evaluated.Expression)) return;
                                break;
                        }
                    }
                    Expression(block.Result, names, expectedBlockType);
                }
                finally { knownTypes = previousTypes; }
            }
            const string unknownListType = "<known-list>";
            const string unknownMapType = "<known-map>";
            const string unknownSetType = "<known-set>";
            bool IsImplicitDefault(string name) => defaultReceivers.TryGetValue(source.Name.Text, out var receiver) &&
                interfaceMethods[receiver.Interface].ContainsKey(name);
            string? StaticType(ExpressionSyntax expression, HashSet<string> names)
            {
                while (expression is GroupSyntax group) expression = group.Inner;
                if (expression is ConditionalMemberCallSyntax conditional &&
                    StaticConditionalCall(conditional, names) is { } conditionalCall && conditionalCall.Result is { } callResult &&
                    SupportedType(callResult, Parameters(source.TypeParameters)))
                    return IsNullable(callResult) ? callResult : callResult + "?";
                if (expression is ConditionalFieldAccessSyntax conditionalField)
                {
                    var fieldType = StaticRecordField(conditionalField.Receiver, conditionalField.Field.Text, names, true);
                    return fieldType is null || IsNullable(fieldType) ? fieldType : fieldType + "?";
                }
                if (expression is FieldAccessSyntax fieldAccess)
                    return StaticRecordField(fieldAccess.Receiver, fieldAccess.Field.Text, names, false);
                if (expression is ListSyntax) return unknownListType;
                if (expression is RecordConstructionSyntax record && records.TryGetValue(record.Name.Text, out var declaration) &&
                    declaration.TypeParameters.Count == 0 && !names.Contains(record.Name.Text) &&
                    !functionTypeScopes[source.Name.Text].ContainsKey(record.Name.Text) && !cases.ContainsKey(record.Name.Text)) return record.Name.Text;
                if (expression is CallSyntax constructor && !names.Contains(constructor.Name.Text) &&
                    !functionTypeScopes[source.Name.Text].ContainsKey(constructor.Name.Text) &&
                    services.ContainsKey(constructor.Name.Text)) return constructor.Name.Text;
                if (expression is IfSyntax conditionalBranch && conditionalBranch.HasElse &&
                    conditionalBranch.Then.Statements.Count == 0 && conditionalBranch.Else.Statements.Count == 0 &&
                    conditionalBranch.Then.Bindings.Count == 0 && conditionalBranch.Else.Bindings.Count == 0)
                {
                    var first = StaticType(conditionalBranch.Then.Result, names);
                    var second = StaticType(conditionalBranch.Else.Result, names);
                    if (first is not null && first == second) return first;
                }
                if (expression is CallSyntax call && !names.Contains(call.Name.Text) &&
                    !IsImplicitDefault(call.Name.Text) && !cases.ContainsKey(DependencyName(call.Name.Text, names)) &&
                    !services.ContainsKey(call.Name.Text) && call.TypeArguments.Count == 0 &&
                    signatures.TryGetValue(DependencyName(call.Name.Text, names), out var target) &&
                    !target.InferReturnType && target.TypeParameters.Count == 0 && call.Arguments.Count == target.Parameters.Count)
                    return CallableResult(target);
                if (expression is MemberCallSyntax { Receiver: ValueSyntax qualifier, Member.Text: "empty", Arguments.Count: 0 } factory &&
                    qualifier.Value.Text == "Map" && !userMapType && !names.Contains("Map") &&
                    !functionTypeScopes[source.Name.Text].ContainsKey("Map") && !signatures.ContainsKey("Map") &&
                    !cases.ContainsKey("Map") && !records.ContainsKey("Map") && !unions.ContainsKey("Map") &&
                    !interfaces.ContainsKey("Map") && !services.ContainsKey("Map") && factory.TypeArguments.Count == 2)
                    return mapSymbol + "<" + string.Join(",", factory.TypeArguments.Select(type =>
                        ScopedType(type, functionTypeScopes[source.Name.Text]).Text)) + ">";
                if (expression is MemberCallSyntax { Receiver: ValueSyntax setQualifier } setFactory &&
                    setQualifier.Value.Text == "Set" && !userSetType && !names.Contains("Set") &&
                    !functionTypeScopes[source.Name.Text].ContainsKey("Set") && !signatures.ContainsKey("Set") &&
                    !cases.ContainsKey("Set") && !records.ContainsKey("Set") && !unions.ContainsKey("Set") &&
                    !interfaces.ContainsKey("Set") && !services.ContainsKey("Set") &&
                    (setFactory.Member.Text == "empty" && setFactory.Arguments.Count == 0 && setFactory.TypeArguments.Count == 1 ||
                     setFactory.Member.Text == "from" && setFactory.Arguments.Count == 1 && setFactory.TypeArguments.Count <= 1))
                    return setFactory.TypeArguments.Count == 1
                        ? setSymbol + "<" + ScopedType(setFactory.TypeArguments[0], functionTypeScopes[source.Name.Text]).Text + ">"
                        : unknownSetType;
                if (expression is NullSyntax) return "<null>";
                if (expression is not ValueSyntax value) return null;
                if (names.Contains(value.Value.Text)) return knownTypes.TryGetValue(value.Value.Text, out var known) ? known : null;
                var text = value.Value.Text;
                if (text is "true" or "false") return "bool";
                if (text.StartsWith('"')) return "string";
                if (text.StartsWith('\'')) return "char";
                if (DecimalLiterals.TryParse(text, out _)) return "decimal";
                if (Float32Literals.TryParse(text, out _)) return "float32";
                if (Float64Literals.TryParse(text, out _)) return "float";
                if (IntegerLiterals.TryParse(text, out var magnitude, out var longSuffix))
                {
                    if (longSuffix) return magnitude <= long.MaxValue ? "long" : null;
                    return magnitude <= int.MaxValue ? "int" : null;
                }
                return null;
            }
            string? StaticRecordField(ExpressionSyntax receiver, string field, HashSet<string> names, bool conditional)
            {
                var type = StaticType(receiver, names);
                if (type is null || !conditional && IsNullable(type)) return null;
                if (IsNullable(type)) type = type[..^1];
                if (!GenericTypes.TryParse(type, out var shape) || !records.TryGetValue(shape.Name, out var record) ||
                    shape.Arguments.Count != record.TypeParameters.Count) return null;
                var declaration = record.Fields.FirstOrDefault(candidate => candidate.Name.Text == field);
                if (declaration is null) return null;
                var substitutions = new Dictionary<string, string>(StringComparer.Ordinal);
                for (var i = 0; i < shape.Arguments.Count; i++)
                    substitutions.Add(record.TypeParameters[i].Text, shape.Arguments[i].ToString());
                return GenericTypes.TrySubstitute(declaration.Type.Text, substitutions, out var result) ? result : null;
            }
            bool IsIntrinsic(string? type, string symbol, string unknown, int arity) => type == unknown ||
                type is not null && GenericTypes.TryParse(type, out var shape) && !shape.IsNullable &&
                shape.Name == symbol && shape.Arguments.Count == arity;
            bool CapabilityFact(string? type)
            {
                if (type is null || !GenericTypes.TryParse(type, out var shape)) return false;
                bool CapabilityShape(TypeShape value) => interfaces.ContainsKey(value.Name) || services.ContainsKey(value.Name) ||
                    resourceNames.Contains(value.Name) || value.Arguments.Any(CapabilityShape);
                return CapabilityShape(shape);
            }
            bool HasFullExtension(MemberCallSyntax member, string? receiver)
            {
                if (member.TypeArguments.Count != 0) return false;
                return extensionDeclarations.Any(extension => extension.Method.Name.Text == member.Member.Text &&
                    (member.ExtensionCandidates is null || member.ExtensionCandidates.Contains(extension.Id)) &&
                    signatures[extension.Id].Parameters.Count == member.Arguments.Count + 1 &&
                    (receiver is null or unknownListType or unknownMapType or unknownSetType || signatures[extension.Id].Parameters[0].Type.Text == receiver));
            }
            bool ProvenMapKey(MemberCallSyntax member, string? receiver, HashSet<string> names)
            {
                if (receiver is null || !GenericTypes.TryParse(receiver, out var shape) || shape.Arguments.Count != 2) return false;
                var key = shape.Arguments[0].ToString();
                var supplied = StaticType(member.Arguments[0], names);
                return supplied == key || supplied == "<null>" && IsNullable(key);
            }
            bool ProvenFoldInitial(MemberCallSyntax member, HashSet<string> names)
            {
                var initial = StaticType(member.Arguments[0], names);
                return initial is not (null or "<null>" or unknownListType or unknownMapType or unknownSetType) &&
                    SupportedType(initial, Parameters(source.TypeParameters));
            }
            bool StaticCallback(ExpressionSyntax expression, HashSet<string> names)
            {
                while (expression is GroupSyntax group) expression = group.Inner;
                if (expression is LambdaSyntax) return true;
                if (expression is not ValueSyntax value || names.Contains(value.Value.Text)) return false;
                var resolved = DependencyName(value.Value.Text, names);
                return !IsImplicitDefault(value.Value.Text) && !cases.ContainsKey(resolved) &&
                    !services.ContainsKey(value.Value.Text) && signatures.ContainsKey(resolved);
            }
            IReadOnlyList<string> MemberArgumentExpected(string name, IReadOnlyList<string>? visible, ExpressionSyntax receiver,
                int argumentIndex, HashSet<string> names, bool conditional = false)
            {
                var receiverType = StaticType(receiver, names);
                if (conditional && receiverType is not null && IsNullable(receiverType)) receiverType = receiverType[..^1];
                var functionTypes = extensionDeclarations.Where(extension => extension.Method.Name.Text == name &&
                    (visible is null || visible.Contains(extension.Id)) &&
                    signatures[extension.Id].Parameters.Count > argumentIndex + 1 &&
                    (receiverType is null || signatures[extension.Id].Parameters[0].Type.Text == receiverType))
                    .Select(extension => signatures[extension.Id].Parameters[argumentIndex + 1].Type.Text)
                    .Where(type => GenericTypes.TryParse(type, out var shape) && shape.IsFunction)
                    .Distinct(StringComparer.Ordinal).ToArray();
                return functionTypes;
            }
            void MemberArgument(ExpressionSyntax argument, string name, IReadOnlyList<string>? visible,
                ExpressionSyntax receiver, int argumentIndex, HashSet<string> names, bool conditional = false)
            {
                var expected = MemberArgumentExpected(name, visible, receiver, argumentIndex, names, conditional);
                if (expected.Count == 0) Expression(argument, names);
                else foreach (var type in expected) Expression(argument, names, type);
            }
            // Return a proof only when the eventual call is a full immutable
            // intrinsic or one uniquely visible extension. Unknown receiver
            // types, callbacks, overloads and dynamic dispatch stay indirect.
            // Result may be unknown even when the call itself is proven (for
            // example, an inferred generic map result). It is used only as a
            // lexical fact for checking a following conditional access.
            (string? Result, string? Extension)? StaticConditionalCall(ConditionalMemberCallSyntax member, HashSet<string> names)
            {
                var receiver = StaticType(member.Receiver, names);
                if (receiver is null or "<null>" or unknownListType or unknownMapType or unknownSetType) return null;
                if (IsNullable(receiver)) receiver = receiver[..^1];
                if (!SupportedType(receiver, Parameters(source.TypeParameters)) || !DataType(receiver) ||
                    !GenericTypes.TryParse(receiver, out var shape) || shape.IsNullable) return null;
                var visible = extensionDeclarations.Where(extension => extension.Method.Name.Text == member.Member.Text &&
                    (member.ExtensionCandidates is null || member.ExtensionCandidates.Contains(extension.Id)) &&
                    signatures[extension.Id].Parameters[0].Type.Text == receiver &&
                    signatures[extension.Id].Parameters.Count == member.Arguments.Count + 1 && member.TypeArguments.Count == 0).ToArray();
                bool Exact(ExpressionSyntax argument, string formal)
                {
                    var actual = StaticType(argument, names);
                    return actual == formal || actual == "<null>" && IsNullable(formal);
                }
                // Without an applicable full extension, an intrinsic-shaped
                // call either binds to that intrinsic or is diagnosed. When a
                // fallback exists, suppress its edge only if the real member's
                // applicability is proved from exact argument types.
                bool IntrinsicArguments(params string[] formals) => member.Arguments.Count == formals.Length &&
                    (visible.Length == 0 || member.Arguments.Zip(formals).All(pair => Exact(pair.First, pair.Second)));
                var explicitTypes = member.TypeArguments.Select(type => ScopedType(type, functionTypeScopes[source.Name.Text]).Text).ToArray();
                if (shape.Name == listSymbol && shape.Arguments.Count == 1)
                {
                    var element = shape.Arguments[0].ToString();
                    if (explicitTypes.Length == 0 && member.Member.Text == "length" && IntrinsicArguments()) return ("int", null);
                    if (explicitTypes.Length == 0 && member.Member.Text == "get" && IntrinsicArguments("int")) return (optionSymbol + "<" + element + ">", null);
                    if (explicitTypes.Length == 0 && member.Member.Text == "append" && IntrinsicArguments(element)) return (receiver, null);
                    if (member.Member.Text == "filter" && explicitTypes.Length == 0 && member.Arguments.Count == 1 &&
                        visible.Length == 0 && StaticCallback(member.Arguments[0], names)) return (receiver, null);
                    if (member.Member.Text == "map" && explicitTypes.Length <= 1 && member.Arguments.Count == 1 &&
                        visible.Length == 0 && StaticCallback(member.Arguments[0], names))
                        return (explicitTypes.Length == 1 ? listSymbol + "<" + explicitTypes[0] + ">" : null, null);
                    if (member.Member.Text == "fold" && explicitTypes.Length <= 1 && member.Arguments.Count == 2 &&
                        visible.Length == 0 && StaticCallback(member.Arguments[1], names))
                        return (explicitTypes.Length == 1 ? explicitTypes[0] : StaticType(member.Arguments[0], names), null);
                }
                if (explicitTypes.Length == 0 && shape.Name == mapSymbol && shape.Arguments.Count == 2)
                {
                    var key = shape.Arguments[0].ToString();
                    if (member.Member.Text == "count" && IntrinsicArguments()) return ("int", null);
                    if (member.Member.Text == "contains" && IntrinsicArguments(key)) return ("bool", null);
                    if (member.Member.Text == "get" && IntrinsicArguments(key)) return (optionSymbol + "<" + shape.Arguments[1] + ">", null);
                    if (member.Member.Text == "set" && IntrinsicArguments(key, shape.Arguments[1].ToString())) return (receiver, null);
                }
                if (explicitTypes.Length == 0 && shape.Name == setSymbol && shape.Arguments.Count == 1)
                {
                    var element = shape.Arguments[0].ToString();
                    if (member.Member.Text == "count" && IntrinsicArguments()) return ("int", null);
                    if (member.Member.Text == "contains" && IntrinsicArguments(element)) return ("bool", null);
                    if (member.Member.Text == "add" && IntrinsicArguments(element)) return (receiver, null);
                }
                // If a real collection operation may take precedence, do not
                // guess that a same-name extension is the selected target.
                if (shape.Name == listSymbol && member.Member.Text is "length" or "get" or "append" or "filter" or "map" or "fold" ||
                    shape.Name == mapSymbol && member.Member.Text is "count" or "contains" or "get" or "set" ||
                    shape.Name == setSymbol && member.Member.Text is "count" or "contains" or "add") return null;
                if (visible.Length != 1) return null;
                var target = signatures[visible[0].Id];
                var result = CallableResult(target);
                if (target.IsAsync || !SupportedType(result, Parameters(source.TypeParameters)) || !DataType(result)) return null;
                return (result, visible[0].Id);
            }
            bool StaticallyPartialExtension(MemberCallSyntax member, HashSet<string> names)
            {
                if (member.Arguments.Count == 0) return false;
                var visible = extensionDeclarations.Where(extension => extension.Method.Name.Text == member.Member.Text &&
                    (member.ExtensionCandidates is null || member.ExtensionCandidates.Contains(extension.Id))).ToArray();
                if (member.Member.Text is "length" or "append" or "get" or "map" or "filter" or "fold" or
                    "empty" or "from" or "count" or "contains" or "set" or "add" or "freeze")
                {
                    var receiver = StaticType(member.Receiver, names);
                    // A real collection member may win. Other immutable
                    // receiver types have no intrinsic or capability dispatch.
                    if (receiver is null || !DataType(receiver) || !GenericTypes.TryParse(receiver, out var shape) ||
                        shape.Name == listSymbol || shape.Name == mapSymbol || shape.Name == setSymbol) return false;
                    visible = visible.Where(extension => signatures[extension.Id].Parameters[0].Type.Text == receiver).ToArray();
                }
                return visible.Length > 0 && visible.All(extension =>
                    signatures[extension.Id].Parameters.Count > member.Arguments.Count + 1);
            }
            // In a one-argument fold call, the argument is the initial value,
            // not the callback. Only suppress indirect-call classification when
            // the receiver is provably the built-in List; service and resource
            // methods named fold must retain the conservative inference rule.
            bool StaticallyListFoldPrefix(MemberCallSyntax member, HashSet<string> names)
            {
                if (member.Member.Text != "fold" || member.Arguments.Count != 1) return false;
                var receiver = StaticType(member.Receiver, names);
                if (!IsIntrinsic(receiver, listSymbol, unknownListType, 1)) return false;
                if (!HasFullExtension(member, receiver)) return true;
                // When the intrinsic cannot infer an accumulator, normal
                // lookup may choose a fully supplied extension returning an
                // arbitrary function. Only classify it as intrinsic if its
                // first argument has a known supported type.
                return ProvenFoldInitial(member, names);
            }
            bool StaticallyMapSetPrefix(MemberCallSyntax member, HashSet<string> names)
            {
                if (member.Member.Text != "set" || member.Arguments.Count != 1 || member.TypeArguments.Count != 0) return false;
                var receiver = StaticType(member.Receiver, names);
                if (!IsIntrinsic(receiver, mapSymbol, unknownMapType, 2)) return false;
                if (!HasFullExtension(member, receiver)) return true;
                return ProvenMapKey(member, receiver, names);
            }
            // Only recognize the built-in empty prefixes when ordinary lookup
            // cannot select a fully supplied extension instead. In particular,
            // a zero-argument extension may return an arbitrary function.
            (int Arity, int Callback)? StaticallyEmptyCollectionPrefix(MemberCallSyntax member, HashSet<string> names)
            {
                if (member.Arguments.Count != 0) return null;
                var receiver = StaticType(member.Receiver, names);
                if (HasFullExtension(member, receiver)) return null;
                if (IsIntrinsic(receiver, listSymbol, unknownListType, 1))
                {
                    if (member.TypeArguments.Count == 0 && member.Member.Text is "append" or "get") return (1, -1);
                    if (member.TypeArguments.Count == 0 && member.Member.Text == "filter") return (1, 0);
                    if (member.TypeArguments.Count <= 1 && member.Member.Text == "map") return (1, 0);
                    if (member.TypeArguments.Count <= 1 && member.Member.Text == "fold") return (2, 1);
                }
                if (member.TypeArguments.Count != 0) return null;
                if (IsIntrinsic(receiver, mapSymbol, unknownMapType, 2))
                {
                    if (member.Member.Text is "get" or "contains") return (1, -1);
                    if (member.Member.Text == "set") return (2, -1);
                }
                if (IsIntrinsic(receiver, setSymbol, unknownSetType, 1) && member.Member.Text is "contains" or "add") return (1, -1);
                return null;
            }
            bool StaticallyCompletedPartial(ExpressionSyntax target, IReadOnlyList<ExpressionSyntax> supplied, HashSet<string> names)
            {
                // Follow grouped invocations and pipes back to their original
                // partial, retaining argument order across every stage.
                var arguments = new List<ExpressionSyntax>(supplied);
                while (true)
                {
                    if (target is GroupSyntax group) { target = group.Inner; continue; }
                    if (target is InvokeSyntax invocation)
                    {
                        arguments.InsertRange(0, invocation.Arguments);
                        target = invocation.Receiver;
                        continue;
                    }
                    if (target is PipeSyntax pipe)
                    {
                        arguments.Insert(0, pipe.Left);
                        target = pipe.Right;
                        continue;
                    }
                    break;
                }
                if (target is not MemberCallSyntax member) return false;
                if (StaticallyEmptyCollectionPrefix(member, names) is { } empty)
                    return arguments.Count <= empty.Arity &&
                        (empty.Callback < 0 || arguments.Count <= empty.Callback || StaticCallback(arguments[empty.Callback], names));
                if (arguments.Count > 1) return false;
                if (StaticallyMapSetPrefix(member, names)) return true;
                if (StaticallyListFoldPrefix(member, names)) return arguments.Count == 0 || StaticCallback(arguments[0], names);
                return StaticallyPartialExtension(member, names);
            }
            // A bare collection member can be resolved by the first pipe
            // value. Retain each subsequent application boundary: once the
            // intrinsic is saturated, another invocation targets its result
            // and is not a statically known collection call.
            bool StaticallyBareCollectionPartial(ExpressionSyntax target, IReadOnlyList<ExpressionSyntax> supplied, HashSet<string> names)
            {
                var stages = new List<IReadOnlyList<ExpressionSyntax>> { supplied };
                while (true)
                {
                    if (target is GroupSyntax group) { target = group.Inner; continue; }
                    if (target is InvokeSyntax invocation)
                    {
                        stages.Insert(0, invocation.Arguments);
                        target = invocation.Receiver;
                        continue;
                    }
                    if (target is PipeSyntax pipe)
                    {
                        stages.Insert(0, new[] { pipe.Left });
                        target = pipe.Right;
                        continue;
                    }
                    break;
                }
                if (target is not FieldAccessSyntax member) return false;
                var receiver = StaticType(member.Receiver, names);
                int arity;
                int callback = -1;
                if (IsIntrinsic(receiver, listSymbol, unknownListType, 1))
                {
                    switch (member.Field.Text)
                    {
                        case "append": case "get": arity = 1; break;
                        case "filter": case "map": arity = 1; callback = 0; break;
                        case "fold": arity = 2; callback = 1; break;
                        default: return false;
                    }
                }
                else if (IsIntrinsic(receiver, mapSymbol, unknownMapType, 2))
                {
                    switch (member.Field.Text)
                    {
                        case "get": case "contains": arity = 1; break;
                        case "set": arity = 2; break;
                        default: return false;
                    }
                }
                else if (IsIntrinsic(receiver, setSymbol, unknownSetType, 1))
                {
                    if (member.Field.Text is not ("contains" or "add")) return false;
                    arity = 1;
                }
                else return false;
                var arguments = new List<ExpressionSyntax>();
                foreach (var stage in stages)
                {
                    if (arguments.Count >= arity || stage.Count > arity - arguments.Count) return false;
                    arguments.AddRange(stage);
                }
                return callback < 0 || arguments.Count <= callback || StaticCallback(arguments[callback], names);
            }
            // Prove a direct, saturated pipe to exactly one visible extension.
            // All potentially competing extensions on that receiver make the
            // proof conservative; ordinary binding still decides applicability.
            // In particular, this does not prove a staged or dynamic invocation.
            (FieldAccessSyntax Access, string Id)? StaticallyBareExtensionPipe(PipeSyntax pipe, HashSet<string> names)
            {
                ExpressionSyntax target = pipe.Right;
                while (target is GroupSyntax group) target = group.Inner;
                if (target is not FieldAccessSyntax access || NeverExpression(pipe.Left) || NeverExpression(access.Receiver)) return null;
                var receiver = StaticType(access.Receiver, names);
                var input = StaticType(pipe.Left, names);
                var parameters = Parameters(source.TypeParameters);
                if (receiver is null || input is null || !SupportedType(receiver, parameters) || !SupportedType(input, parameters) ||
                    !DataType(receiver) || !DataType(input) || !GenericTypes.TryParse(receiver, out var shape) ||
                    shape.IsFunction || !GenericTypes.TryParse(input, out var inputShape) || inputShape.IsFunction) return null;
                // A real field or collection operation can take precedence.
                // The proof does not widen the existing nullable collection
                // behavior; only known non-nullable collection types qualify.
                if (shape.IsNullable && (shape.Name == listSymbol || shape.Name == mapSymbol || shape.Name == setSymbol) ||
                    CollectionMethodCollision(shape, access.Field.Text) ||
                    StaticRecordField(access.Receiver, access.Field.Text, names, false) is not null) return null;
                var candidates = extensionDeclarations.Where(extension => extension.Method.Name.Text == access.Field.Text &&
                    (access.ExtensionCandidates is null || access.ExtensionCandidates.Contains(extension.Id)) &&
                    signatures[extension.Id].Parameters[0].Type.Text == receiver).ToArray();
                if (candidates.Length != 1) return null;
                var method = signatures[candidates[0].Id];
                if (method.Parameters.Count != 2 || method.Parameters[1].Type.Text != input) return null;
                // An async extension's logical result must still be safe data.
                // Binding its invocation produces Task<result>, as for an
                // ordinary async call; this proof does not admit tasks into
                // immutable data or infer an async function's own annotation.
                var result = method.ReturnType.Text;
                if (!SupportedType(result, parameters) || !DataType(result) ||
                    !GenericTypes.TryParse(result, out var resultShape) || resultShape.IsFunction) return null;
                return (access, candidates[0].Id);
            }
            void WithUnknownNames(IEnumerable<string> hidden, Action action)
            {
                var previousTypes = knownTypes;
                knownTypes = new Dictionary<string, string>(knownTypes, StringComparer.Ordinal);
                var shadowedResources = new List<string>();
                foreach (var name in hidden)
                {
                    knownTypes.Remove(name);
                    if (scopedResources.Remove(name)) shadowedResources.Add(name);
                }
                try { action(); }
                finally
                {
                    knownTypes = previousTypes;
                    foreach (var name in shadowedResources) scopedResources.Add(name);
                }
            }
            void Expression(ExpressionSyntax expression, HashSet<string> names, string? expectedType = null)
            {
                switch (expression)
                {
                    case ValueSyntax value when !names.Contains(value.Value.Text) && !cases.ContainsKey(DependencyName(value.Value.Text, names)) && signatures.ContainsKey(DependencyName(value.Value.Text, names)):
                        AddCall(DependencyName(value.Value.Text, names)); break;
                    case CallSyntax call:
                        var callCompletesArguments = true;
                        var callSignature = !names.Contains(call.Name.Text) && signatures.TryGetValue(DependencyName(call.Name.Text, names), out var knownCall) ? knownCall : null;
                        var callTypeSubstitutions = new Dictionary<string, string>(StringComparer.Ordinal);
                        if (callSignature is not null && call.TypeArguments.Count == callSignature.TypeParameters.Count)
                            for (var typeIndex = 0; typeIndex < call.TypeArguments.Count; typeIndex++)
                                callTypeSubstitutions[callSignature.TypeParameters[typeIndex].Text] =
                                    ScopedType(call.TypeArguments[typeIndex], functionTypeScopes[source.Name.Text]).Text;
                        for (var argumentIndex = 0; argumentIndex < call.Arguments.Count; argumentIndex++)
                        {
                            var argument = call.Arguments[argumentIndex];
                            var argumentExpected = callSignature is not null && argumentIndex < callSignature.Parameters.Count
                                ? callSignature.Parameters[argumentIndex].Type.Text : null;
                            if (argumentExpected is not null && callTypeSubstitutions.Count > 0 &&
                                GenericTypes.TrySubstitute(argumentExpected, callTypeSubstitutions, out var resolvedExpected))
                                argumentExpected = resolvedExpected;
                            Expression(argument, names, argumentExpected);
                            if (!includeUnreachable && NeverExpression(argument)) { callCompletesArguments = false; break; }
                        }
                        if (!callCompletesArguments) break;
                        if (names.Contains(call.Name.Text)) indirect = true;
                        else if (cases.ContainsKey(DependencyName(call.Name.Text, names))) { /* A union case is not a function dependency. */ }
                        else if (signatures.ContainsKey(DependencyName(call.Name.Text, names))) AddCall(DependencyName(call.Name.Text, names));
                        else if (services.ContainsKey(call.Name.Text) && !functionTypeScopes[source.Name.Text].ContainsKey(call.Name.Text))
                        { /* Checked service construction has no user body call. */ }
                        else if ((call.Name.Text == "MutableList" && !userMutableListType || call.Name.Text == "MutableMap" && !userMutableMapType) &&
                            !functionTypeScopes[source.Name.Text].ContainsKey(call.Name.Text) && !records.ContainsKey(call.Name.Text) && !unions.ContainsKey(call.Name.Text))
                        { /* A checked builder constructor has no function dependency. */ }
                        else indirect = true;
                        break;
                    case ConditionalMemberCallSyntax member:
                        // Receiver and arguments can contain dependencies even
                        // though the arguments are skipped on the null path.
                        // Only a proven intrinsic or uniquely resolved extension
                        // supplies a static call target; everything else remains
                        // an indirect dependency.
                        var previousStrictContext = strictContext;
                        strictContext = true;
                        try
                        {
                            Expression(member.Receiver, names);
                            if (includeUnreachable || !NeverExpression(member.Receiver))
                            {
                                for (var argumentIndex = 0; argumentIndex < member.Arguments.Count; argumentIndex++)
                                {
                                    var argument = member.Arguments[argumentIndex];
                                    MemberArgument(argument, member.Member.Text, member.ExtensionCandidates,
                                        member.Receiver, argumentIndex, names, conditional: true);
                                    if (!includeUnreachable && NeverExpression(argument)) break;
                                }
                                var conditionalTarget = StaticConditionalCall(member, names);
                                if (conditionalTarget is null) indirect = true;
                                else if (conditionalTarget.Value.Extension is { } extension) AddCall(extension);
                            }
                        }
                        finally { strictContext = previousStrictContext; }
                        break;
                    case MemberCallSyntax member:
                        var qualifier = member.Receiver as ValueSyntax;
                        var unionName = qualifier?.Value.Text == "Result" && !userResultType ? resultSymbol :
                            qualifier?.Value.Text == "Option" && !userOptionType ? optionSymbol : qualifier?.Value.Text;
                        if (qualifier is null || names.Contains(qualifier.Value.Text) || unionName is null || !unions.ContainsKey(unionName))
                        {
                            var provenReceiver = StaticType(member.Receiver, names);
                            // A member name alone cannot prove that dispatch is
                            // an immutable collection operation: an inferred
                            // factory, branch or generic identity can return a
                            // capability whose method happens to be named get,
                            // count, map, etc. Apply this conservative rule on
                            // paths actually evaluated by conditional calls;
                            // preserve legacy inference on unrelated paths.
                            var knownFactory = member.Receiver is ValueSyntax && member.Member.Text is "empty" or "from" &&
                                StaticType(member, names) is not null;
                            if (provenReceiver is null && !knownFactory)
                            {
                                unknownReceiver = true;
                                if (strictContext) indirect = true;
                            }
                            // A collection-like method name must not hide dynamic
                            // service or scoped-resource dispatch in a callback
                            // or in the body of an otherwise statically known
                            // callee. The callable graph includes these bodies
                            // even when their return annotations are explicit.
                            var dispatchReceiver = member.Receiver;
                            while (dispatchReceiver is GroupSyntax groupedReceiver) dispatchReceiver = groupedReceiver.Inner;
                            if (CapabilityFact(provenReceiver) ||
                                dispatchReceiver is ValueSyntax resourceValue && scopedResources.Contains(resourceValue.Value.Text))
                                indirect = true;
                            var namedExtensions = extensionDeclarations.Where(extension => extension.Method.Name.Text == member.Member.Text &&
                                (member.ExtensionCandidates is null || member.ExtensionCandidates.Contains(extension.Id))).ToArray();
                            if (provenReceiver is not null)
                                namedExtensions = namedExtensions.Where(extension =>
                                {
                                    var target = signatures[extension.Id].Parameters[0].Type.Text;
                                    if (provenReceiver == unknownListType) return IsIntrinsic(target, listSymbol, unknownListType, 1);
                                    if (provenReceiver == unknownMapType) return IsIntrinsic(target, mapSymbol, unknownMapType, 2);
                                    if (provenReceiver == unknownSetType) return IsIntrinsic(target, setSymbol, unknownSetType, 1);
                                    return target == provenReceiver;
                                }).ToArray();
                            // The intrinsic wins over extensions only when its
                            // prefix is actually applicable. Retain every
                            // plausible fallback edge if that cannot be proved.
                            var provenRealPartial = StaticallyEmptyCollectionPrefix(member, names) is not null || member.Arguments.Count == 1 &&
                                (member.Member.Text == "set" && member.TypeArguments.Count == 0 &&
                                 IsIntrinsic(provenReceiver, mapSymbol, unknownMapType, 2) && ProvenMapKey(member, provenReceiver, names) ||
                                 member.Member.Text == "fold" && IsIntrinsic(provenReceiver, listSymbol, unknownListType, 1) &&
                                 ProvenFoldInitial(member, names));
                            if (provenRealPartial) namedExtensions = Array.Empty<(string Id, ExtensionDeclarationSyntax Declaration, ExtensionMethodSyntax Method)>();
                            foreach (var extension in namedExtensions) AddCall(extension.Id);
                            if (namedExtensions.Length == 0 && member.Member.Text is not ("length" or "append" or "get" or "map" or "filter" or "fold" or
                                "empty" or "from" or "count" or "contains" or "set" or "add" or "freeze")) indirect = true;
                            if (member.Member.Text is "map" or "filter" or "fold" && member.Arguments.Count > 0 &&
                                !StaticallyListFoldPrefix(member, names) && !StaticallyPartialExtension(member, names))
                            {
                                var callback = member.Arguments[^1];
                                while (callback is GroupSyntax group) callback = group.Inner;
                                if (callback is not LambdaSyntax &&
                                    (callback is not ValueSyntax callbackName || names.Contains(callbackName.Value.Text) ||
                                     !signatures.ContainsKey(DependencyName(callbackName.Value.Text, names))))
                                    indirect = true;
                            }
                            Expression(member.Receiver, names);
                        }
                        if (includeUnreachable || !NeverExpression(member.Receiver))
                            for (var argumentIndex = 0; argumentIndex < member.Arguments.Count; argumentIndex++)
                            {
                                var argument = member.Arguments[argumentIndex];
                                MemberArgument(argument, member.Member.Text, member.ExtensionCandidates,
                                    member.Receiver, argumentIndex, names);
                                if (!includeUnreachable && NeverExpression(argument)) break;
                            }
                        break;
                    case InvokeSyntax invocation:
                    {
                        var provenBare = StaticallyBareCollectionPartial(invocation.Receiver, invocation.Arguments, names);
                        var previousStrict = strictContext;
                        if (provenBare) strictContext = true;
                        try
                        {
                            Expression(invocation.Receiver, names);
                            var invokeCompletes = includeUnreachable || !NeverExpression(invocation.Receiver);
                            if (invokeCompletes)
                                foreach (var argument in invocation.Arguments)
                                {
                                    Expression(argument, names);
                                    if (!includeUnreachable && NeverExpression(argument)) { invokeCompletes = false; break; }
                                }
                            if (invokeCompletes && !provenBare && !StaticallyCompletedPartial(invocation.Receiver, invocation.Arguments, names)) indirect = true;
                        }
                        finally { strictContext = previousStrict; }
                        break;
                    }
                    case PipeSyntax pipe:
                    {
                        var provenBare = StaticallyBareCollectionPartial(pipe.Right, new[] { pipe.Left }, names);
                        var provenExtension = StaticallyBareExtensionPipe(pipe, names);
                        var previousStrict = strictContext;
                        if (provenBare || provenExtension is not null) strictContext = true;
                        try
                        {
                        Expression(pipe.Left, names);
                        if (!includeUnreachable && NeverExpression(pipe.Left)) break;
                        var pipeTarget = pipe.Right;
                        while (pipeTarget is GroupSyntax groupedTarget) pipeTarget = groupedTarget.Inner;
                        if (pipeTarget is ValueSyntax pipeName && !names.Contains(pipeName.Value.Text))
                        {
                            if (cases.ContainsKey(DependencyName(pipeName.Value.Text, names))) { /* Union construction is statically known. */ }
                            else if (signatures.ContainsKey(DependencyName(pipeName.Value.Text, names))) AddCall(DependencyName(pipeName.Value.Text, names));
                            else indirect = true;
                        }
                        else if (pipeTarget is FieldAccessSyntax { Receiver: ValueSyntax pipeQualifier } &&
                            !names.Contains(pipeQualifier.Value.Text) && unions.ContainsKey(
                                pipeQualifier.Value.Text == "Result" && !userResultType ? resultSymbol :
                                pipeQualifier.Value.Text == "Option" && !userOptionType ? optionSymbol : pipeQualifier.Value.Text))
                        { /* Qualified union case construction is statically known. */ }
                        else if (pipeTarget is CallSyntax partialCall && !names.Contains(partialCall.Name.Text) &&
                            signatures.TryGetValue(DependencyName(partialCall.Name.Text, names), out var partialTarget) && partialCall.Arguments.Count < partialTarget.Parameters.Count)
                            Expression(pipeTarget, names);
                        else if (provenExtension is { } extension)
                        {
                            // The receiver and input are evaluated before the
                            // call, and the selected body is a strict graph edge.
                            Expression(extension.Access.Receiver, names);
                            if (includeUnreachable || !NeverExpression(extension.Access.Receiver))
                            {
                                AddCall(extension.Id);
                                provenExtensionPipes[extension.Access] = extension.Id;
                            }
                        }
                        else if (provenBare || StaticallyCompletedPartial(pipeTarget, new[] { pipe.Left }, names))
                            Expression(pipeTarget, names);
                        else
                        {
                            indirect = true;
                            Expression(pipe.Right, names);
                        }
                        }
                        finally { strictContext = previousStrict; }
                        break;
                    }
                    case LambdaSyntax lambda:
                        var lambdaNames = new HashSet<string>(names, StringComparer.Ordinal);
                        foreach (var parameter in lambda.Parameters) lambdaNames.Add(parameter.Name.Text);
                        WithUnknownNames(lambda.Parameters.Select(parameter => parameter.Name.Text), () =>
                        {
                            foreach (var parameter in lambda.Parameters)
                                if (parameter.Type is { } annotation)
                                    knownTypes[parameter.Name.Text] = ScopedType(annotation, functionTypeScopes[source.Name.Text]).Text;
                            var lambdaExpected = expectedType is not null && GenericTypes.TryParse(expectedType, out var lambdaShape) && lambdaShape.IsFunction
                                ? lambdaShape.Arguments[^1].ToString() : null;
                            Block(lambda.Body, lambdaNames, lambdaExpected);
                        });
                        break;
                    case ForSyntax loop:
                        Expression(loop.Source, names);
                        if (includeUnreachable || !NeverExpression(loop.Source))
                        {
                            var loopNames = new HashSet<string>(names, StringComparer.Ordinal) { loop.Name.Text };
                            WithUnknownNames(new[] { loop.Name.Text }, () => Block(loop.Body, loopNames));
                        }
                        break;
                    case UsingSyntax owned:
                        Expression(owned.Acquisition, names);
                        if (includeUnreachable || !NeverExpression(owned.Acquisition))
                        {
                            var scopedNames = new HashSet<string>(names, StringComparer.Ordinal) { owned.Name.Text };
                            WithUnknownNames(new[] { owned.Name.Text }, () =>
                            {
                                scopedResources.Add(owned.Name.Text);
                                try { Block(owned.Body, scopedNames); }
                                finally { scopedResources.Remove(owned.Name.Text); }
                            });
                        }
                        break;
                    case IfSyntax conditional:
                        Expression(conditional.Condition, names);
                        if (includeUnreachable || !NeverExpression(conditional.Condition))
                        {
                            var condition = includeUnreachable ? null : LiteralBool(conditional.Condition);
                            // A dead branch that can complete still contributes to
                            // the checked conditional's common type. A dead branch
                            // that cannot complete contributes only unreachable exits.
                            if (condition != false || includeDeadBranches && !NeverBlock(conditional.Then) && !NeverBlock(conditional.Else)) Block(conditional.Then, names, expectedType);
                            if (condition != true || includeDeadBranches && !NeverBlock(conditional.Else) && !NeverBlock(conditional.Then)) Block(conditional.Else, names, expectedType);
                        }
                        break;
                    case MatchSyntax match:
                        Expression(match.Scrutinee, names);
                        if (!includeUnreachable && NeverExpression(match.Scrutinee)) break;
                        var earlierPatterns = new List<PatternSyntax>();
                        // Covered arms can influence the checked match type only
                        // if some reachable arm can actually complete.
                        var coverageForCompletion = new List<PatternSyntax>();
                        var hasCompletingReachableArm = false;
                        ExpressionSyntax literalSource = match.Scrutinee;
                        while (literalSource is GroupSyntax literalGroup) literalSource = literalGroup.Inner;
                        PatternSyntax? singleton = literalSource switch
                        {
                            ValueSyntax value when value.Value.Text is "true" or "false" ||
                                IntegerLiterals.TryParse(value.Value.Text, out _, out _) ||
                                DecimalLiterals.TryParse(value.Value.Text, out _) ||
                                Float32Literals.TryParse(value.Value.Text, out _) ||
                                Float64Literals.TryParse(value.Value.Text, out _) ||
                                value.Value.Text.StartsWith('"') || value.Value.Text.StartsWith('\'') => new LiteralPatternSyntax(value.Value),
                            UnarySyntax { Operator.Text: "-", Operand: ValueSyntax value } unary when
                                IntegerLiterals.TryParse(value.Value.Text, out _, out _) || DecimalLiterals.TryParse(value.Value.Text, out _) || Float32Literals.TryParse(value.Value.Text, out _) || Float64Literals.TryParse(value.Value.Text, out _) => new LiteralPatternSyntax(value.Value, unary.Operator),
                            UnitSyntax unit => new UnitPatternSyntax(unit.Unit),
                            NullSyntax nullValue => new NullPatternSyntax(nullValue.Null),
                            _ => null
                        };
                        bool LiteralArmPossible(PatternSyntax pattern)
                        {
                            if (singleton is not LiteralPatternSyntax point || pattern is not LiteralPatternSyntax literal) return true;
                            if (point.Literal.Text is "true" or "false" && literal.Literal.Text is "true" or "false")
                                return point.Literal.Text == literal.Literal.Text;
                            if (IntegerPatternValue(point, out var a) && IntegerPatternValue(literal, out var b)) return a == b;
                            if (DecimalPatternValue(point, out var decimalPoint) && DecimalPatternValue(literal, out var decimalLiteral)) return decimalPoint == decimalLiteral;
                            if (Float32PatternValue(point, out var floatPoint) && Float32PatternValue(literal, out var floatLiteral)) return floatPoint == floatLiteral;
                            if (Float64PatternValue(point, out var doublePoint) && Float64PatternValue(literal, out var doubleLiteral)) return doublePoint == doubleLiteral;
                            return true; // Different spellings of text literals can denote the same value.
                        }
                        bool Covered(List<PatternSyntax> previous, PatternSyntax target) =>
                            previous.Any(pattern => Subsumes(pattern, target) || singleton is not null && Subsumes(pattern, singleton));
                        foreach (var candidate in match.Arms)
                        {
                            if (!LiteralArmPossible(candidate.Pattern) || Covered(coverageForCompletion, candidate.Pattern)) continue;
                            if ((candidate.Guard is null || !NeverExpression(candidate.Guard) && LiteralBool(candidate.Guard) != false) && !NeverExpression(candidate.Value))
                                hasCompletingReachableArm = true;
                            if (candidate.Guard is null) coverageForCompletion.Add(candidate.Pattern);
                        }
                        foreach (var arm in match.Arms)
                        {
                            if (!includeUnreachable && (!LiteralArmPossible(arm.Pattern) || Covered(earlierPatterns, arm.Pattern)) &&
                                (!includeDeadBranches || !hasCompletingReachableArm || NeverExpression(arm.Value))) continue;
                            var armNames = new HashSet<string>(names, StringComparer.Ordinal);
                            PatternNames(arm.Pattern, armNames);
                            WithUnknownNames(armNames.Except(names), () =>
                            {
                                if (arm.Guard is not null) Expression(arm.Guard, armNames);
                                if (includeUnreachable || arm.Guard is null || !NeverExpression(arm.Guard) &&
                                    (LiteralBool(arm.Guard) != false || includeDeadBranches && hasCompletingReachableArm)) Expression(arm.Value, armNames, expectedType);
                            });
                            if (arm.Guard is null) earlierPatterns.Add(arm.Pattern);
                            if (!includeUnreachable && !includeDeadBranches && arm.Guard is null && arm.Pattern is WildcardPatternSyntax) break;
                        }
                        break;
                    case GroupSyntax group: Expression(group.Inner, names, expectedType); break;
                    case TupleSyntax tuple:
                        foreach (var element in tuple.Elements)
                        {
                            Expression(element, names);
                            if (!includeUnreachable && NeverExpression(element)) break;
                        }
                        break;
                    case ListSyntax list:
                        foreach (var element in list.Elements)
                        {
                            Expression(element, names);
                            if (!includeUnreachable && NeverExpression(element)) break;
                        }
                        break;
                    case InterpolationSyntax interpolation:
                        foreach (var hole in interpolation.Holes)
                        {
                            Expression(hole, names);
                            if (!includeUnreachable && NeverExpression(hole)) break;
                        }
                        break;
                    case UnarySyntax unary: Expression(unary.Operand, names); break;
                    case BinarySyntax binary:
                        Expression(binary.Left, names);
                        if (includeUnreachable || !NeverExpression(binary.Left) &&
                            (includeDeadBranches || !(binary.Operator.Text == "&&" && LiteralBool(binary.Left) == false) &&
                            !(binary.Operator.Text == "||" && LiteralBool(binary.Left) == true))) Expression(binary.Right, names);
                        break;
                    case CoalesceSyntax coalesce:
                        Expression(coalesce.Left, names);
                        if (includeUnreachable || !NeverExpression(coalesce.Left)) Expression(coalesce.Right, names);
                        break;
                    case AwaitSyntax awaited: Expression(awaited.Operand, names); break;
                    case PropagateSyntax propagation: Expression(propagation.Operand, names); break;
                    case FieldAccessSyntax access:
                        Expression(access.Receiver, names);
                        // A bare member may be an extension method value when
                        // later binding supplies an expected function type.
                        // Such a value must not hide an indirect or recursive
                        // dependency from omitted-return inference. Preserve
                        // actual record fields and qualified union cases.
                        var fieldQualifier = access.Receiver as ValueSyntax;
                        var fieldUnion = fieldQualifier?.Value.Text == "Result" && !userResultType ? resultSymbol :
                            fieldQualifier?.Value.Text == "Option" && !userOptionType ? optionSymbol : fieldQualifier?.Value.Text;
                        var qualifiedCase = fieldQualifier is not null && !names.Contains(fieldQualifier.Value.Text) &&
                            fieldUnion is not null && unions.ContainsKey(fieldUnion);
                        if (!qualifiedCase && expectedType is not null && GenericTypes.TryParse(expectedType, out var fieldExpected) &&
                            (fieldExpected.IsFunction || fieldExpected.Arguments.Count == 0 && signatures.Values.Any(signature =>
                                signature.TypeParameters.Any(parameter => parameter.Text == fieldExpected.Name))) &&
                            StaticRecordField(access.Receiver, access.Field.Text, names, false) is null)
                        {
                            var fieldReceiver = StaticType(access.Receiver, names);
                            // Unknown receiver types can still be ordinary
                            // record fields. Do not let a merely possible
                            // extension change their established inference.
                            // Actual method-value binding in an inferred body
                            // is checked below after lookup resolves the target.
                            var couldBeField = fieldReceiver is null && records.Values.Any(record =>
                                record.Fields.Any(field => field.Name.Text == access.Field.Text));
                            var possibleMethods = extensionDeclarations.Where(extension => extension.Method.Name.Text == access.Field.Text &&
                                (access.ExtensionCandidates is null || access.ExtensionCandidates.Contains(extension.Id)) &&
                                (fieldReceiver is null || signatures[extension.Id].Parameters[0].Type.Text == fieldReceiver)).ToArray();
                            if (!couldBeField && possibleMethods.Length > 0)
                            {
                                foreach (var extension in possibleMethods) AddCall(extension.Id);
                                indirect = true;
                            }
                        }
                        break;
                    case ConditionalFieldAccessSyntax access: Expression(access.Receiver, names); break;
                    case RecordConstructionSyntax construction:
                        foreach (var field in construction.Fields)
                        {
                            Expression(field.Value, names);
                            if (!includeUnreachable && NeverExpression(field.Value)) break;
                        }
                        break;
                    case RecordUpdateSyntax update:
                        Expression(update.Receiver, names);
                        if (includeUnreachable || !NeverExpression(update.Receiver))
                            foreach (var field in update.Fields)
                            {
                                Expression(field.Value, names);
                                if (!includeUnreachable && NeverExpression(field.Value)) break;
                            }
                        break;
                }
            }
            Block(source.Body ?? new BlockSyntax(source.Bindings, source.Expression ?? new ValueSyntax(source.Literal)), visible,
                source.InferReturnType ? null : source.ReturnType.Text);
            return (calls, indirect, strictCalls, unknownReceiver);
        }
        var dependencyGraph = syntax.Functions.ToDictionary(function => function.Name.Text, function => AnalyzeDependencies(function), StringComparer.Ordinal);
        foreach (var helper in resultHelpers.Keys) dependencyGraph.Add(helper,
            (new HashSet<string>(StringComparer.Ordinal), false, new HashSet<string>(StringComparer.Ordinal), false));
        bool InRecursiveComponent(string root)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            bool ReachesRoot(string current)
            {
                if (!seen.Add(current)) return false;
                foreach (var next in dependencyGraph[current].Calls)
                    if (next == root || ReachesRoot(next)) return true;
                return false;
            }
            return ReachesRoot(root);
        }
        bool HasIndirectDependency(string root)
        {
            var seen = new HashSet<(string Name, bool Strict)>();
            bool Visit(string current, bool strict) => seen.Add((current, strict)) &&
                (dependencyGraph[current].Indirect || strict && dependencyGraph[current].UnknownReceiver ||
                 dependencyGraph[current].Calls.Any(next => Visit(next, strict || dependencyGraph[current].StrictCalls.Contains(next))));
            return Visit(root, false);
        }
        foreach (var function in syntax.Functions.Where(function => function.InferReturnType))
            if (InRecursiveComponent(function.Name.Text) || HasIndirectDependency(function.Name.Text))
                Fail("DS2027", function.Name, "An explicit return type is required for recursive or unresolved function dependencies.");
        // Dead branches can contribute to the checked type of a completing
        // expression. Infer those type dependencies in an acyclic order too;
        // a cycle needs an annotation instead of relying on declaration order.
        // Statements after an unconditional exit and impossible calls remain
        // excluded, and annotated callees already have known return types.
        var typeDependencies = syntax.Functions.ToDictionary(function => function.Name.Text,
            function => AnalyzeDependencies(function, includeDeadBranches: true).Calls, StringComparer.Ordinal);
        bool HasCyclicTypeDependencies(string root)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            bool Visit(string current)
            {
                if (!seen.Add(current)) return false;
                foreach (var next in typeDependencies[current])
                    if (signatures[next].InferReturnType && (next == root || Visit(next))) return true;
                return false;
            }
            return Visit(root);
        }
        foreach (var function in syntax.Functions.Where(function => function.InferReturnType))
            if (HasCyclicTypeDependencies(function.Name.Text))
                Fail("DS2027", function.Name, "An explicit return type is required for cyclic checked-type dependencies.");
        // A statically dead call may still affect the checked type of an if or
        // match expression. Prefer resolving its callee first when doing so
        // does not conflict with the reachable-call inference order. Dead-only
        // cycles use the provisional bottom fallback and are checked in pass 2.
        var schedulingGraph = typeDependencies.ToDictionary(entry => entry.Key,
            entry => new HashSet<string>(entry.Value, StringComparer.Ordinal), StringComparer.Ordinal);
        bool SchedulingPath(string from, string to)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            bool Visit(string current) => current == to || seen.Add(current) && schedulingGraph[current].Any(Visit);
            return Visit(from);
        }
        foreach (var owner in syntax.Functions)
            foreach (var callee in AnalyzeDependencies(owner, includeUnreachable: true).Calls)
                if (signatures[callee].InferReturnType && !schedulingGraph[owner.Name.Text].Contains(callee) &&
                    !SchedulingPath(callee, owner.Name.Text))
                    schedulingGraph[owner.Name.Text].Add(callee);
        var inferenceOrder = new List<FunctionSyntax>();
        var scheduled = new HashSet<string>(StringComparer.Ordinal);
        void Schedule(FunctionSyntax function)
        {
            if (!scheduled.Add(function.Name.Text)) return;
            foreach (var name in schedulingGraph[function.Name.Text])
                if (signatures[name].InferReturnType) Schedule(signatures[name]);
            inferenceOrder.Add(function);
        }
        foreach (var function in syntax.Functions.Where(function => function.InferReturnType)) Schedule(function);
        foreach (var function in syntax.Functions.Where(function => !function.InferReturnType)) Schedule(function);
        // User bodies determine which prelude declarations are needed. Their
        // explicit signatures permit binding their bodies after the user code.
        inferenceOrder.AddRange(resultHelpers.Values);
        var functions = new List<BoundFunction>();
        var actorRoutes = new Dictionary<string, BoundActorRoute>(StringComparer.Ordinal);
        var messageContracts = new Dictionary<string, BoundMessageContract>(StringComparer.Ordinal);
        string MessageContractKey(TypeShape shape)
        {
            var name = syntax.ActorTypeDisplayNames.TryGetValue(shape.Name, out var qualified) ? qualified :
                records.ContainsKey(shape.Name) || unions.ContainsKey(shape.Name) ? syntax.Name.Text + "." + shape.Name : shape.Name;
            return name + (shape.Arguments.Count == 0 ? "" : "<" + string.Join(",", shape.Arguments.Select(MessageContractKey)) + ">");
        }
        // Infer every omitted signature before the final body-checking pass.
        // Runtime-unreachable calls may still need a callee's inferred type
        // during final checking, even when they are not call-graph edges.
        var boundPreludeHelpers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var inferencePass in new[] { true, false })
        for (var declarationIndex = 0; declarationIndex < inferenceOrder.Count; declarationIndex++)
        {
            var declaration = inferenceOrder[declarationIndex];
            if (inferencePass && !declaration.InferReturnType) continue;
            if (resultHelpers.ContainsKey(declaration.Name.Text))
            {
                // Used helpers are bound once. Newly discovered dependencies
                // are enqueued after binding, so overloads selected in either
                // direction are emitted without relying on declaration order.
                if (!usedResultHelpers.Contains(declaration.Name.Text) || !boundPreludeHelpers.Add(declaration.Name.Text)) continue;
            }
            var function = inferencePass ? declaration : signatures[declaration.Name.Text];
            var functionTypeScope = functionTypeScopes[function.Name.Text];
            var activeTypeParameters = Parameters(function.TypeParameters);
            var locals = new Dictionary<string, (int Value, string Type)>(StringComparer.Ordinal);
            var conditionalReceiverCounter = 0;
            foreach (var parameter in function.Parameters) locals.Add(parameter.Name.Text, (0, parameter.Type.Text));
            bool IsDefaultMethodName(string name) => defaultReceivers.TryGetValue(function.Name.Text, out var receiver) &&
                interfaceMethods[receiver.Interface].ContainsKey(name);
            bool IsActorMethodName(string name) => actorMethodsByBody.TryGetValue(function.Name.Text, out var actorContext) &&
                actorContext.Methods.ContainsKey(name);
            bool IsActorDependency(string name) => actorMethodsByBody.TryGetValue(function.Name.Text, out var actorContext) &&
                actorContext.Actor.Dependencies.Any(dependency => dependency.Name.Text == name);
            var actorBorrowArguments = new HashSet<ExpressionSyntax>(ReferenceEqualityComparer.Instance);
            var inaccessibleCaptures = new HashSet<string>(StringComparer.Ordinal);
            var resourceHandles = new HashSet<string>(StringComparer.Ordinal);
            var directResourceAwait = false;
            CallSyntax? directlyAwaitedActorHelper = null;
            CallSyntax? directlyAwaitedInvoke = null;
            var lambdaDepth = 0;
            var requiresDispatcher = false;
            // Only individual producer call nodes explicitly marked at a
            // transfer boundary may produce an owned resource. Arguments are
            // never marked, so nesting an owned value into a different call
            // cannot smuggle it across the lifetime boundary.
            var ownedCalls = new HashSet<CallSyntax>(ReferenceEqualityComparer.Instance);
            var ownedMemberCalls = new HashSet<MemberCallSyntax>(ReferenceEqualityComparer.Instance);
            var awaitedOwnedMembers = new HashSet<MemberCallSyntax>(ReferenceEqualityComparer.Instance);
            var awaitedOwnedCalls = new HashSet<CallSyntax>(ReferenceEqualityComparer.Instance);
            var ownedResultCases = new HashSet<ExpressionSyntax>(ReferenceEqualityComparer.Instance);
            var ownedPropagations = new HashSet<PropagateSyntax>(ReferenceEqualityComparer.Instance);
            bool IntrinsicCase(CallSyntax call) => (call.Name.Text is "Success" or "Error") &&
                !locals.ContainsKey(call.Name.Text) && !signatures.ContainsKey(call.Name.Text) &&
                !records.ContainsKey(call.Name.Text) && !services.ContainsKey(call.Name.Text) &&
                !IsDefaultMethodName(call.Name.Text) && !IsActorMethodName(call.Name.Text) && cases.TryGetValue(call.Name.Text, out var candidates) &&
                candidates.Count == 1 && candidates[0].Union.Name.Text == resultSymbol;
            bool IntrinsicMemberCase(MemberCallSyntax member) => member.Receiver is ValueSyntax qualifier &&
                (member.Member.Text is "Success" or "Error") && !locals.ContainsKey(qualifier.Value.Text) &&
                !functionTypeScope.ContainsKey(qualifier.Value.Text) && UnionQualifier(qualifier.Value.Text) == resultSymbol;
            void MarkOwnedResult(ExpressionSyntax expression)
            {
                switch (expression)
                {
                    case GroupSyntax group: MarkOwnedResult(group.Inner); break;
                    case CallSyntax resultCase when OwnedResult(function.ReturnType.Text) && IntrinsicCase(resultCase):
                        ownedResultCases.Add(resultCase);
                        if (resultCase.Name.Text == "Success" && resultCase.Arguments.Count == 1)
                            MarkOwnedResult(resultCase.Arguments[0]);
                        break;
                    case MemberCallSyntax resultCase when OwnedResult(function.ReturnType.Text) && IntrinsicMemberCase(resultCase):
                        ownedResultCases.Add(resultCase);
                        if (resultCase.Member.Text == "Success" && resultCase.Arguments.Count == 1)
                            MarkOwnedResult(resultCase.Arguments[0]);
                        break;
                    case MemberCallSyntax member:
                        ownedMemberCalls.Add(member); break;
                    case CallSyntax call when resourceNames.Contains(call.Name.Text) ||
                                              ownedFactories.Contains(call.Name.Text) && !signatures[call.Name.Text].IsAsync ||
                                              IsDefaultMethodName(call.Name.Text):
                        ownedCalls.Add(call); break;
                    case AwaitSyntax awaited:
                        var operand = awaited.Operand;
                        while (operand is GroupSyntax awaitedGroup) operand = awaitedGroup.Inner;
                        if (operand is CallSyntax asyncCall && ownedFactories.Contains(asyncCall.Name.Text) && signatures[asyncCall.Name.Text].IsAsync)
                            ownedCalls.Add(asyncCall);
                        else if (operand is CallSyntax implicitCall && IsDefaultMethodName(implicitCall.Name.Text))
                        { ownedCalls.Add(implicitCall); awaitedOwnedCalls.Add(implicitCall); }
                        else if (operand is MemberCallSyntax asyncMember)
                        { ownedMemberCalls.Add(asyncMember); awaitedOwnedMembers.Add(asyncMember); }
                        break;
                    case IfSyntax conditional:
                        MarkOwnedBlock(conditional.Then); MarkOwnedBlock(conditional.Else); break;
                    case MatchSyntax match:
                        foreach (var arm in match.Arms) MarkOwnedResult(arm.Value);
                        break;
                }
            }
            void MarkOwnedBlock(BlockSyntax block) => MarkOwnedResult(block.Result);
            void ValidateFactoryBody(object? node, bool insideLambda = false)
            {
                if (node is null or string || node is Token) return;
                if (node is UsingSyntax owned)
                    Fail("DS2041", owned.Keyword, "Owned factories cannot contain using scopes before ownership transfers.");
                if (node is ReturnStatementSyntax returned && !insideLambda) MarkOwnedResult(returned.Value);
                if (node is System.Collections.IEnumerable sequence)
                {
                    foreach (var item in sequence) ValidateFactoryBody(item, insideLambda);
                    return;
                }
                var type = node.GetType();
                if (!type.Name.EndsWith("Syntax", StringComparison.Ordinal)) return;
                var nestedLambda = insideLambda || node is LambdaSyntax;
                foreach (var property in type.GetProperties().Where(property => property.GetIndexParameters().Length == 0))
                    ValidateFactoryBody(property.GetValue(node), nestedLambda);
            }
            var boundLocals = new List<BoundLocal>();
            var tupleBindingCount = 0;
            bool hasReturn = false;
            bool awaitAllowed = function.IsAsync;
            string? currentReturnType = function.ReturnType.Text;
            List<string>? inferredReturns = null;
            bool inferenceReachable = true;
            bool? LiteralCondition(ExpressionSyntax node) => node switch
            {
                GroupSyntax group => LiteralCondition(group.Inner),
                ValueSyntax { Value.Text: "true" } => true,
                ValueSyntax { Value.Text: "false" } => false,
                _ => null
            };
            bool ServiceConversion(string actual, string expected) => services.TryGetValue(actual, out var service) &&
                interfaces.ContainsKey(expected) && service.Interfaces.Any(iface => iface.Text == expected);
            bool Convertible(string actual, string expected) => actual == "<never>" || actual == expected || ServiceConversion(actual, expected) ||
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
            string UnionQualifier(string name) => name == "Result" && (!userResultType || resultHelpers.ContainsKey(function.Name.Text)) ? resultSymbol :
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
                    string? returnPattern = null, string? expectedResult = null,
                    string? contextualFormal = null, string? contextualActual = null)
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
                if (contextualFormal is not null && contextualActual is not null && typeParameters.Count > 0 &&
                    !GenericTypes.TryUnify(contextualFormal, contextualActual, parameters, substitutions, fixedParameters))
                    Fail("DS2023", location, "Conflicting generic type arguments.");
                if (returnPattern is not null && expectedResult is not null && typeParameters.Count > 0)
                {
                    var inferred = new Dictionary<string, string>(substitutions, StringComparer.Ordinal);
                    bool UnifyExpected(string pattern, string actual)
                    {
                        if (SupportedType(actual, activeTypeParameters))
                            return GenericTypes.TryUnify(pattern, actual, parameters, inferred, fixedParameters);
                        if (!GenericTypes.TryParse(pattern, out var formal) || !GenericTypes.TryParse(actual, out var given)) return false;
                        // A contextual type may contain an outer unresolved type
                        // variable. It provides no evidence yet; known siblings
                        // can still constrain this constructor's parameters.
                        if (given.Arguments.Count == 0) return true;
                        if (formal.Arguments.Count == 0 && parameters.Contains(formal.Name)) return true;
                        if (formal.Name != given.Name || formal.IsNullable != given.IsNullable || formal.Arguments.Count != given.Arguments.Count) return false;
                        for (var i = 0; i < formal.Arguments.Count; i++)
                            if (!UnifyExpected(formal.Arguments[i].ToString(), given.Arguments[i].ToString())) return false;
                        return true;
                    }
                    if (UnifyExpected(returnPattern, expectedResult))
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
                    TupleLetStatementSyntax binding => NeverExpression(binding.Value),
                    ExpressionStatementSyntax statementExpression => NeverExpression(statementExpression.Expression),
                    _ => false
                }) || NeverExpression(block.Result);
                bool NeverExpression(ExpressionSyntax expression) => expression switch
                {
                    GroupSyntax group => NeverExpression(group.Inner),
                    TupleSyntax tuple => tuple.Elements.Any(NeverExpression),
                    ListSyntax list => list.Elements.Any(NeverExpression),
                    InterpolationSyntax interpolation => interpolation.Holes.Any(NeverExpression),
                    ForSyntax loop => NeverExpression(loop.Source),
                    UsingSyntax owned => NeverExpression(owned.Acquisition) || NeverBlock(owned.Body),
                    AwaitSyntax awaited => NeverExpression(awaited.Operand),
                    PropagateSyntax propagation => NeverExpression(propagation.Operand),
                    IfSyntax conditional => NeverExpression(conditional.Condition) ||
                        (LiteralCondition(conditional.Condition) == true ? NeverBlock(conditional.Then) :
                         LiteralCondition(conditional.Condition) == false ? NeverBlock(conditional.Else) :
                         conditional.HasElse && NeverBlock(conditional.Then) && NeverBlock(conditional.Else)),
                    MatchSyntax match => NeverExpression(match.Scrutinee) || match.Arms.All(arm =>
                        arm.Guard is not null && NeverExpression(arm.Guard) || NeverExpression(arm.Value)),
                    CallSyntax call => call.Arguments.Any(NeverExpression),
                    InvokeSyntax invocation => NeverExpression(invocation.Receiver) || invocation.Arguments.Any(NeverExpression),
                    MemberCallSyntax member => NeverExpression(member.Receiver) || member.Arguments.Any(NeverExpression),
                    ConditionalMemberCallSyntax member => NeverExpression(member.Receiver),
                    RecordConstructionSyntax record => record.Fields.Any(field => NeverExpression(field.Value)),
                    RecordUpdateSyntax update => NeverExpression(update.Receiver) || update.Fields.Any(field => NeverExpression(field.Value)),
                    FieldAccessSyntax access => NeverExpression(access.Receiver),
                    ConditionalFieldAccessSyntax access => NeverExpression(access.Receiver),
                    UnarySyntax unary => NeverExpression(unary.Operand),
                    BinarySyntax binary => NeverExpression(binary.Left) ||
                        (binary.Operator.Text is not ("&&" or "||") ||
                         binary.Operator.Text == "&&" && LiteralCondition(binary.Left) == true ||
                         binary.Operator.Text == "||" && LiteralCondition(binary.Left) == false) && NeverExpression(binary.Right),
                    CoalesceSyntax coalesce => NeverExpression(coalesce.Left),
                    PipeSyntax pipe => NeverExpression(pipe.Left) || NeverExpression(pipe.Right),
                    _ => false
                };
                void CollectNullConstraints(ExpressionSyntax expression, string formal)
                {
                    if (NeverExpression(expression) || !GenericTypes.TryParse(formal, out var shape)) return;
                    if (expression is GroupSyntax group) { CollectNullConstraints(group.Inner, formal); return; }
                    if (expression is ListSyntax list && shape.Name == listSymbol && shape.Arguments.Count == 1)
                    {
                        foreach (var element in list.Elements) CollectNullConstraints(element, shape.Arguments[0].ToString());
                        return;
                    }
                    if (expression is TupleSyntax tuple && shape.IsTuple && tuple.Elements.Count == shape.Arguments.Count)
                    {
                        for (var i = 0; i < tuple.Elements.Count; i++) CollectNullConstraints(tuple.Elements[i], shape.Arguments[i].ToString());
                        return;
                    }
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
                    if (expression is ListSyntax list && type.Name == listSymbol && type.Arguments.Count == 1)
                    {
                        var element = type.Arguments[0];
                        foreach (var item in list.Elements) element = RefineNullEvidence(item, element);
                        return type with { Arguments = new[] { element } };
                    }
                    if (expression is TupleSyntax tuple && type.IsTuple && tuple.Elements.Count == type.Arguments.Count)
                        return type with { Arguments = tuple.Elements.Select((element, i) => RefineNullEvidence(element, type.Arguments[i])).ToArray() };
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
                // When the checked parameter types are already fixed, a
                // definitely noncompleting argument makes all later arguments
                // irrelevant to return inference. Defer those expressions to
                // the final checking pass, where all signatures are known.
                if (inferencePass && arguments.Any(NeverExpression))
                {
                    var inferenceValues = new List<BoundExpression>();
                    var inferenceTypes = new List<string>();
                    var never = false;
                    foreach (var (argument, index) in arguments.Select((argument, index) => (argument, index)))
                    {
                        var formal = Substitute(formalTypes[index].Text, substitutions, formalTypes[index]);
                        if (never)
                        {
                            inferenceValues.Add(new BoundUnit());
                            inferenceTypes.Add("<never>");
                            continue;
                        }
                        var argumentHint = SupportedType(formal, activeTypeParameters) ? formal :
                            GenericTypes.TryParse(formal, out var partialTupleShape) && partialTupleShape.IsTuple ? formal : null;
                        var argumentValue = BindExpression(argument, argumentHint);
                        if (typeParameters.Count > 0 && !GenericTypes.TryUnify(formalTypes[index].Text, argumentValue.Type, parameters, substitutions, fixedParameters))
                            Fail("DS2023", argument.Token, "Conflicting generic type arguments.");
                        formal = Substitute(formalTypes[index].Text, substitutions, formalTypes[index]);
                        if (SupportedType(formal, activeTypeParameters))
                        {
                            Require(argument, argumentValue.Type, formal);
                            inferenceValues.Add(Coerce(argumentValue.Expression, formal, argumentValue.Type));
                        }
                        else inferenceValues.Add(argumentValue.Expression);
                        inferenceTypes.Add(argumentValue.Type);
                        never = argumentValue.Type == "<never>" || NeverExpression(argument);
                    }
                    return (inferenceValues, inferenceTypes, substitutions, never);
                }
                for (var i = 0; i < arguments.Count; i++) CollectNullConstraints(arguments[i], formalTypes[i].Text);
                var bound = new (BoundExpression Expression, string Type)?[arguments.Count];
                var remaining = arguments.Count;
                var allowInferredHints = false;
                // Inference can bind arguments several times and out of source
                // order. Provisional attempts must not contribute return exits.
                (BoundExpression Expression, string Type) ProbeArgument(ExpressionSyntax argument, string? hint)
                {
                    if (inferredReturns is null) return BindExpression(argument, hint);
                    var saved = inferredReturns;
                    inferredReturns = new List<string>();
                    try { return BindExpression(argument, hint); }
                    finally { inferredReturns = saved; }
                }
                while (remaining > 0)
                {
                    var progress = false;
                    SourceError? deferred = null;
                    for (var i = 0; i < arguments.Count; i++)
                    {
                        if (bound[i] is not null) continue;
                        var argumentCore = arguments[i];
                        while (argumentCore is GroupSyntax group) argumentCore = group.Inner;
                        var hint = substitutions.Count == typeParameters.Count &&
                            (fixedParameters.Count == typeParameters.Count || allowInferredHints)
                            ? Substitute(formalTypes[i].Text, substitutions, formalTypes[i]) : null;
                        if (arguments[i] is LambdaSyntax && GenericTypes.TrySubstitute(formalTypes[i].Text, substitutions, out var partial) &&
                            GenericTypes.TryParse(partial, out var partialShape) && partialShape.IsFunction)
                            hint = partial;
                        if (argumentCore is TupleSyntax && GenericTypes.TrySubstitute(formalTypes[i].Text, substitutions, out var partialTuple) &&
                            GenericTypes.TryParse(partialTuple, out var partialTupleShape) && partialTupleShape.IsTuple)
                            hint = partialTuple;
                        (BoundExpression Expression, string Type) value;
                        try { value = ProbeArgument(arguments[i], hint); }
                        catch (SourceError error) when (typeParameters.Count > 0 &&
                            (((hint is null || argumentCore is TupleSyntax) && error.Diagnostic.Code == "DS2023" && error.Diagnostic.Message == "Cannot infer all generic type arguments.") ||
                             (argumentCore is TupleSyntax && error.Diagnostic.Code == "DS2002" && error.Diagnostic.Message == "Cannot infer the type of a null tuple element.") ||
                             (arguments[i] is LambdaSyntax && error.Diagnostic.Code == "DS2026")))
                        {
                            deferred ??= error;
                            continue;
                        }
                        if (typeParameters.Count > 0 && !GenericTypes.TryUnify(formalTypes[i].Text, value.Type, parameters, substitutions, fixedParameters) &&
                            !(GenericTypes.TrySubstitute(formalTypes[i].Text, substitutions, out var fixedFormal) &&
                              SupportedType(fixedFormal, activeTypeParameters) && Convertible(value.Type, fixedFormal)))
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
                                    var rebound = ProbeArgument(arguments[i], expected);
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
                        throw deferred ?? new SourceError(new("DS2023", location.OriginPath ?? path, location.Span, "Cannot infer all generic type arguments."));
                    }
                }
                if (substitutions.Count != typeParameters.Count)
                    Fail("DS2023", location, "Cannot infer all generic type arguments.");
                var values = new List<BoundExpression>();
                var finalTypes = new List<string>();
                var entryReachable = inferenceReachable;
                var earlierNever = false;
                try
                {
                    for (var i = 0; i < bound.Length; i++)
                    {
                        var expected = Substitute(formalTypes[i].Text, substitutions, formalTypes[i]);
                        var actorBorrow = privateResourceContracts.Contains(expected) &&
                            actorMethodsByBody.TryGetValue(function.Name.Text, out var callerActor) &&
                            actorMethodsByBody.TryGetValue(location.Text, out var calleeActor) &&
                            ReferenceEquals(callerActor.Actor, calleeActor.Actor) &&
                            arguments[i] is ValueSyntax borrowed && locals.TryGetValue(borrowed.Value.Text, out var borrowedLocal) &&
                            borrowedLocal.Type == expected && callerActor.Actor.Dependencies.Any(dependency =>
                                dependency.Name.Text == borrowed.Value.Text && resourceContracts.TryGetValue(dependency.Type.Text, out var contract) && contract == expected);
                        if (!SupportedType(expected, activeTypeParameters) && !actorBorrow)
                            Fail("DS2026", arguments[i].Token, "Function values cannot be stored in data types.");
                        // Commit exits exactly once in runtime argument order.
                        // Later unreachable arguments are still type checked.
                        inferenceReachable = entryReachable && !earlierNever;
                        var value = typeParameters.Count == 0 && inferredReturns is null
                            ? bound[i]!.Value : BindExpression(arguments[i], expected);
                        Require(arguments[i], value.Type, expected);
                        values.Add(Coerce(value.Expression, expected, value.Type));
                        finalTypes.Add(value.Type);
                        earlierNever |= value.Type == "<never>";
                    }
                }
                finally { inferenceReachable = entryReachable; }
                return (values, finalTypes, substitutions, earlierNever);
            }
            // Some noncompleting expressions still carry a known nominal type.
            // Preserve it when checking unreachable code and chained record operations.
            string? KnownType(BoundExpression expression) => expression switch
            {
                BoundTuple tuple => tuple.Type,
                BoundList list => list.Type,
                BoundInterpolation => "string",
                BoundFor => "Unit",
                BoundUsing owned => owned.Type,
                BoundListOperation list => list.Type,
                BoundCollectionMethodPartial collection => collection.Type,
                BoundMapSetFactory collection => collection.Type,
                BoundMapSetOperation collection => collection.Type,
                BoundMapSetPartial collection => collection.Type,
                BoundListFoldPartial fold => fold.Type,
                BoundBuilderFactory builder => builder.Type,
                BoundBuilderOperation builder => builder.Type,
                BoundServiceConstruction service => service.Type,
                BoundServiceCall service => service.Type,
                BoundServiceConversion service => service.Type,
                BoundAwait awaited => awaited.Type,
                BoundLambda lambda => lambda.Type,
                BoundFunctionReference reference => reference.Type,
                BoundPartial partial => partial.Type,
                BoundInvoke invocation => invocation.Type,
                BoundPipe pipe => pipe.Type,
                BoundRecordConstruction construction => construction.Type,
                BoundRecordUpdate update => update.Type,
                BoundUnionConstruction construction => construction.Type,
                BoundNull value => value.Type,
                BoundPropagation propagation => propagation.Type,
                BoundCoalesce coalesce => coalesce.Type,
                BoundConditionalFieldAccess access => access.Type,
                BoundConditionalCall call => call.Type,
                BoundFieldAccess access when records.TryGetValue(Nominal(access.ReceiverType), out var record) =>
                    record.Fields.FirstOrDefault(field => field.Name.Text == access.Field) is { } field &&
                    SubstitutionsFor(access.ReceiverType, record.Name.Text, record.TypeParameters) is { } substitutions
                        ? Substitute(field.Type.Text, substitutions, field.Type) : null,
                BoundCall call when signatures.TryGetValue(call.Name, out var target) && !target.InferReturnType =>
                    Substitute(CallableResult(target), target.TypeParameters.Select((parameter, index) => (parameter.Text, call.TypeArguments[index]))
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
                IReadOnlyList<Token>? explicitTypes = null, string? expectedResult = null, bool called = false,
                bool ownedCase = false)
            {
                if (union.Name.Text == resultSymbol) resultUsed = true;
                if (union.Name.Text == optionSymbol) optionUsed = true;
                if (ownedCase && union.Name.Text == resultSymbol && expectedResult is not null && OwnedResult(expectedResult))
                {
                    if (arguments.Count != 1) Fail("DS2018", name, "An owned Result case requires one payload.");
                    GenericTypes.TryParse(expectedResult, out var ownedShape);
                    if (explicitTypes is { Count: > 0 } && (explicitTypes.Count != 2 ||
                        explicitTypes[0].Text != ownedShape!.Arguments[0].ToString() ||
                        explicitTypes[1].Text != ownedShape.Arguments[1].ToString()))
                        Fail("DS2023", name, "Owned Result case type arguments must match the factory result.");
                    var payloadType = ownedShape!.Arguments[unionCase.Name.Text == "Success" ? 0 : 1].ToString();
                    var payload = BindExpression(arguments[0], payloadType);
                    Require(arguments[0], payload.Type, payloadType);
                    return (new BoundUnionConstruction(expectedResult, unionCase.Name.Text,
                        new[] { Coerce(payload.Expression, payloadType, payload.Type) }),
                        payload.Type == "<never>" ? "<never>" : expectedResult);
                }
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
                string? expectedResult = null, bool called = false, bool ownedCase = false)
            {
                var unionCase = union.Cases.FirstOrDefault(item => item.Name.Text == name.Text);
                if (unionCase is null) Fail("DS2018", name, "Unknown union case.");
                return BindCase(union, unionCase!, name, arguments, explicitTypes, expectedResult, called, ownedCase);
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
                        var literalValue = BindExpression(new ValueSyntax(literal.Literal), expected);
                        if (literal.Minus is not null && !IntegerType(literalValue.Type) && literalValue.Type is not ("decimal" or "float32" or "float"))
                            Fail("DS2019", literal.Token, "Only a numeric literal can be negative.");
                        if (literalValue.Type != expected)
                            Fail("DS2019", literal.Token, $"Expected a {DisplayType(expected)} pattern.");
                        return new BoundLiteralPattern(literal.Minus is null ? literalValue.Expression :
                            new BoundUnary("-", literalValue.Expression) { OperandType = literalValue.Type }, literalValue.Type);
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
            // Probe each applicable prelude overload with its actual context;
            // choose only a unique candidate, never by declaration order. The
            // syntax-node guard permits independent nested helper calls.
            var resolvingResultOverloads = new HashSet<ExpressionSyntax>(ReferenceEqualityComparer.Instance);
            bool TryResultOverloadFamily(string name, out string family)
            {
                foreach (var candidate in resultOverloads.Keys)
                    if (preludeNames.TryGetValue(candidate, out var identity) && identity == name)
                    { family = candidate; return true; }
                family = "";
                return false;
            }
            (BoundExpression Expression, string Type) BindResultOverload(ExpressionSyntax syntaxNode, Token location,
                string family, string? expected, string? pipeInput, string? pipeOutput, bool pipeNull,
                IReadOnlyList<string?>? groupedTypes, string? groupedExpected, bool pipeTarget)
            {
                var savedReturns = inferredReturns;
                var savedReachable = inferenceReachable;
                var savedHasReturn = hasReturn;
                var savedTupleCount = tupleBindingCount;
                var savedConditionalCounter = conditionalReceiverCounter;
                var savedDirectAwait = directResourceAwait;
                var savedUsedHelpers = usedResultHelpers.ToArray();
                var savedOwnedCalls = ownedCalls.ToArray();
                var savedOwnedMembers = ownedMemberCalls.ToArray();
                var savedAwaitedMembers = awaitedOwnedMembers.ToArray();
                var savedAwaitedCalls = awaitedOwnedCalls.ToArray();
                var savedOwnedCases = ownedResultCases.ToArray();
                var savedOwnedPropagations = ownedPropagations.ToArray();
                var savedFlags = (resultUsed, optionUsed, listUsed, mapUsed, setUsed, mutableListUsed, mutableMapUsed, taskUsed);
                void Restore<T>(HashSet<T> target, T[] original)
                {
                    target.Clear();
                    target.UnionWith(original);
                }
                ExpressionSyntax Candidate(string identity) => syntaxNode switch
                {
                    ValueSyntax value => value with { Value = value.Value with { Text = identity } },
                    CallSyntax call => call with { Name = call.Name with { Text = identity } },
                    _ => throw new InvalidOperationException("Invalid Result helper candidate.")
                };
                bool Probe(ExpressionSyntax candidate)
                {
                    if (savedReturns is not null) inferredReturns = new List<string>();
                    inferenceReachable = false;
                    try
                    {
                        var bound = BindExpression(candidate, expected, pipeInput, pipeOutput, pipeNull, groupedTypes, groupedExpected, pipeTarget);
                        return expected is null || !SupportedType(expected, activeTypeParameters) || Convertible(bound.Type, expected);
                    }
                    catch (SourceError) { return false; }
                    finally
                    {
                        inferredReturns = savedReturns;
                        inferenceReachable = savedReachable;
                        hasReturn = savedHasReturn;
                        tupleBindingCount = savedTupleCount;
                        conditionalReceiverCounter = savedConditionalCounter;
                        directResourceAwait = savedDirectAwait;
                        Restore(usedResultHelpers, savedUsedHelpers);
                        Restore(ownedCalls, savedOwnedCalls);
                        Restore(ownedMemberCalls, savedOwnedMembers);
                        Restore(awaitedOwnedMembers, savedAwaitedMembers);
                        Restore(awaitedOwnedCalls, savedAwaitedCalls);
                        Restore(ownedResultCases, savedOwnedCases);
                        Restore(ownedPropagations, savedOwnedPropagations);
                        (resultUsed, optionUsed, listUsed, mapUsed, setUsed, mutableListUsed, mutableMapUsed, taskUsed) = savedFlags;
                    }
                }
                resolvingResultOverloads.Add(syntaxNode);
                try
                {
                    var choices = new List<ExpressionSyntax> { syntaxNode };
                    choices.AddRange(resultOverloads[family].Select(Candidate));
                    var applicable = choices.Where(Probe).ToArray();
                    if (applicable.Length > 1)
                        Fail("DS2026", location, "Result." + family + " is ambiguous; supply an expected callable type.");
                    // When no candidate fits, the original binding supplies
                    // the established diagnostic and D# source location.
                    var selected = applicable.Length == 1 ? applicable[0] : syntaxNode;
                    if (!ReferenceEquals(selected, syntaxNode)) taskUsed = true;
                    return BindExpression(selected, expected, pipeInput, pipeOutput, pipeNull, groupedTypes, groupedExpected, pipeTarget);
                }
                finally { resolvingResultOverloads.Remove(syntaxNode); }
            }
            (BoundExpression Expression, string Type) BindExpression(ExpressionSyntax expression, string? expectedResult = null, string? pipeInputType = null, string? pipeOutputType = null, bool pipeNullInput = false,
                IReadOnlyList<string?>? groupedArgumentTypes = null, string? groupedExpectedResult = null, bool isPipeTarget = false)
            {
                switch (expression)
                {
                    case UnitSyntax: return (new BoundUnit(), "Unit");
                    case NullSyntax: return (new BoundNull("<null>"), "<null>");
                    case InterpolationSyntax interpolation:
                    {
                        var parts = new List<BoundInterpolationPart>();
                        var entryReachable = inferenceReachable;
                        var never = false;
                        try
                        {
                            for (var i = 0; i < interpolation.Holes.Count; i++)
                            {
                                var text = interpolation.Texts[i];
                                parts.Add(new BoundInterpolationText(DecodeText(text, text.OriginPath ?? path)));
                                inferenceReachable = entryReachable && !never;
                                var hole = interpolation.Holes[i];
                                var bound = BindExpression(hole);
                                if (bound.Type == "<never>")
                                {
                                    parts.Add(new BoundInterpolationHole(Coerce(bound.Expression, "string", bound.Type), "string"));
                                    never = true;
                                }
                                else
                                {
                                    if (!(IntegerType(bound.Type) || bound.Type is "decimal" or "float32" or "float" or "string" or "char" or "bool" or "Unit"))
                                        Fail("DS2026", hole.Token, "Interpolation requires a supported non-nullable scalar value.");
                                    parts.Add(new BoundInterpolationHole(bound.Expression, bound.Type));
                                }
                            }
                            var final = interpolation.Texts[^1];
                            parts.Add(new BoundInterpolationText(DecodeText(final, final.OriginPath ?? path)));
                        }
                        finally { inferenceReachable = entryReachable; }
                        return (new BoundInterpolation(parts), never ? "<never>" : "string");
                    }
                    case ListSyntax list:
                    {
                        listUsed = true;
                        string? elementType = null;
                        if (expectedResult is not null && GenericTypes.TryParse(expectedResult, out var expectedList) &&
                            expectedList.Name == listSymbol && expectedList.Arguments.Count == 1 &&
                            SupportedType(expectedList.Arguments[0].ToString(), activeTypeParameters))
                            elementType = expectedList.Arguments[0].ToString();

                        // Probe without committing return evidence; constructor and
                        // null-only elements may need a sibling's type first.
                        if (elementType is null)
                        {
                            var savedReturns = inferredReturns;
                            var savedReturn = hasReturn;
                            var savedReachable = inferenceReachable;
                            if (savedReturns is not null) inferredReturns = new List<string>();
                            SourceError? deferred = null;
                            var hasFreshEvidence = false;
                            bool IsFreshElement(ExpressionSyntax node)
                            {
                                while (node is GroupSyntax group) node = group.Inner;
                                if (node is ListSyntax or TupleSyntax or RecordConstructionSyntax) return true;
                                if (node is CallSyntax call && !locals.ContainsKey(call.Name.Text) &&
                                    cases.TryGetValue(call.Name.Text, out var candidates) && candidates.Count == 1 &&
                                    !signatures.ContainsKey(call.Name.Text) && !records.ContainsKey(call.Name.Text)) return true;
                                return node is MemberCallSyntax { Receiver: ValueSyntax qualifier } member &&
                                    !locals.ContainsKey(qualifier.Value.Text) && unions.ContainsKey(UnionQualifier(qualifier.Value.Text)) &&
                                    unions[UnionQualifier(qualifier.Value.Text)].Cases.Any(item => item.Name.Text == member.Member.Text);
                            }
                            TypeShape? JoinFresh(TypeShape left, TypeShape right)
                            {
                                if (left.Name != right.Name || left.Arguments.Count != right.Arguments.Count) return null;
                                var arguments = new List<TypeShape>();
                                for (var i = 0; i < left.Arguments.Count; i++)
                                {
                                    var child = JoinFresh(left.Arguments[i], right.Arguments[i]);
                                    if (child is null) return null;
                                    arguments.Add(child);
                                }
                                return left with { IsNullable = left.IsNullable || right.IsNullable, Arguments = arguments };
                            }
                            try
                            {
                                foreach (var element in list.Elements)
                                {
                                    try
                                    {
                                        var value = BindExpression(element);
                                        var type = value.Type == "<never>" ? KnownType(value.Expression) : value.Type;
                                        if (type is null || type == "<never>") continue;
                                        var fresh = IsFreshElement(element);
                                        var joined = elementType is null ? type : CommonType(elementType, type);
                                        if (joined is null && (hasFreshEvidence || fresh) &&
                                            GenericTypes.TryParse(elementType!, out var priorShape) && GenericTypes.TryParse(type, out var nextShape))
                                            joined = JoinFresh(priorShape, nextShape)?.ToString();
                                        if (joined is null) Fail("DS2002", element.Token, "List elements must have compatible types.");
                                        elementType = joined;
                                        hasFreshEvidence |= fresh;
                                    }
                                    catch (SourceError error) when (error.Diagnostic.Code is "DS2023" or "DS2002")
                                    { deferred ??= error; }
                                }
                            }
                            finally
                            {
                                inferredReturns = savedReturns;
                                hasReturn = savedReturn;
                                inferenceReachable = savedReachable;
                            }
                            if (elementType is null or "<null>" or "<never>")
                                throw deferred ?? new SourceError(new("DS2023", list.Open.OriginPath ?? path, list.Open.Span, "Cannot infer all generic type arguments."));

                            // Fresh literals and constructors may add nullable
                            // evidence within nested List, record or union shapes.
                            // Existing bound values remain invariant on rebind.
                            TypeShape RefineElement(ExpressionSyntax node, TypeShape shape)
                            {
                                while (node is GroupSyntax group) node = group.Inner;
                                if (node is NullSyntax) return shape with { IsNullable = true };
                                if (node is ListSyntax nested && shape.Name == listSymbol && shape.Arguments.Count == 1)
                                {
                                    var child = shape.Arguments[0];
                                    foreach (var item in nested.Elements) child = RefineElement(item, child);
                                    return shape with { Arguments = new[] { child } };
                                }
                                if (node is TupleSyntax tuple && shape.IsTuple && tuple.Elements.Count == shape.Arguments.Count)
                                    return shape with { Arguments = tuple.Elements.Select((item, i) => RefineElement(item, shape.Arguments[i])).ToArray() };
                                if (node is RecordConstructionSyntax construction && construction.Name.Text == shape.Name &&
                                    records.TryGetValue(shape.Name, out var record) && record.TypeParameters.Count == shape.Arguments.Count)
                                {
                                    var map = record.TypeParameters.Select((p, i) => (p.Text, Type: shape.Arguments[i].ToString())).ToDictionary(p => p.Text, p => p.Type, StringComparer.Ordinal);
                                    var parameters = record.TypeParameters.Select(p => p.Text).ToHashSet(StringComparer.Ordinal);
                                    foreach (var field in construction.Fields)
                                    {
                                        var definition = record.Fields.FirstOrDefault(f => f.Name.Text == field.Name.Text);
                                        if (definition is not null && GenericTypes.TrySubstitute(definition.Type.Text, map, out var current) && GenericTypes.TryParse(current, out var child))
                                            GenericTypes.TryUnify(definition.Type.Text, RefineElement(field.Value, child).ToString(), parameters, map);
                                    }
                                    return shape with { Arguments = record.TypeParameters.Select((p, i) => GenericTypes.TryParse(map[p.Text], out var child) ? child : shape.Arguments[i]).ToArray() };
                                }
                                if (!unions.TryGetValue(shape.Name, out var union) || union.TypeParameters.Count != shape.Arguments.Count) return shape;
                                Token? name = null;
                                IReadOnlyList<ExpressionSyntax>? payloads = null;
                                if (node is CallSyntax call && !locals.ContainsKey(call.Name.Text) && cases.TryGetValue(call.Name.Text, out var candidates) &&
                                    candidates.Count == 1 && candidates[0].Union.Name.Text == shape.Name && !signatures.ContainsKey(call.Name.Text) && !records.ContainsKey(call.Name.Text))
                                { name = call.Name; payloads = call.Arguments; }
                                else if (node is MemberCallSyntax member && member.Receiver is ValueSyntax qualifier &&
                                    !locals.ContainsKey(qualifier.Value.Text) && UnionQualifier(qualifier.Value.Text) == shape.Name)
                                { name = member.Member; payloads = member.Arguments; }
                                var unionCase = name is null ? null : union.Cases.FirstOrDefault(c => c.Name.Text == name.Text);
                                if (unionCase is null || payloads is null || payloads.Count != unionCase.PayloadTypes.Count) return shape;
                                var unionMap = union.TypeParameters.Select((p, i) => (p.Text, Type: shape.Arguments[i].ToString())).ToDictionary(p => p.Text, p => p.Type, StringComparer.Ordinal);
                                var unionParameters = union.TypeParameters.Select(p => p.Text).ToHashSet(StringComparer.Ordinal);
                                for (var i = 0; i < payloads.Count; i++)
                                    if (GenericTypes.TrySubstitute(unionCase.PayloadTypes[i].Text, unionMap, out var current) && GenericTypes.TryParse(current, out var child))
                                        GenericTypes.TryUnify(unionCase.PayloadTypes[i].Text, RefineElement(payloads[i], child).ToString(), unionParameters, unionMap);
                                return shape with { Arguments = union.TypeParameters.Select((p, i) => GenericTypes.TryParse(unionMap[p.Text], out var child) ? child : shape.Arguments[i]).ToArray() };
                            }
                            if (GenericTypes.TryParse(elementType, out var inferredShape))
                            {
                                foreach (var item in list.Elements) inferredShape = RefineElement(item, inferredShape);
                                elementType = inferredShape.ToString();
                            }
                        }
                        if (!SupportedType(elementType!, activeTypeParameters) || !DataType(elementType!))
                            Fail("DS2026", list.Open, "List elements must have a supported immutable data type.");
                        var listType = listSymbol + "<" + elementType + ">";
                        var values = new List<BoundExpression>();
                        var entryReachable = inferenceReachable;
                        var never = false;
                        try
                        {
                            foreach (var item in list.Elements)
                            {
                                inferenceReachable = entryReachable && !never;
                                var value = BindExpression(item, elementType);
                                Require(item, value.Type, elementType!);
                                values.Add(Coerce(value.Expression, elementType!, value.Type));
                                never |= value.Type == "<never>";
                            }
                        }
                        finally { inferenceReachable = entryReachable; }
                        return (new BoundList(values, listType), never ? "<never>" : listType);
                    }
                    case TupleSyntax tuple:
                        TypeShape? expectedTuple = null;
                        if (expectedResult is not null && GenericTypes.TryParse(expectedResult, out var tupleHint) && tupleHint.IsTuple && tupleHint.Arguments.Count == tuple.Elements.Count)
                            expectedTuple = tupleHint;
                        var tupleElements = new BoundExpression?[tuple.Elements.Count];
                        var tupleTypes = new TypeShape?[tuple.Elements.Count];
                        var tupleValues = new (BoundExpression Expression, string Type)?[tuple.Elements.Count];
                        var tupleDeferredErrors = new SourceError?[tuple.Elements.Count];
                        var tupleParameters = new HashSet<string>(StringComparer.Ordinal);
                        var tupleSubstitutions = new Dictionary<string, string>(StringComparer.Ordinal);
                        var tupleNullableRequired = new HashSet<string>(StringComparer.Ordinal);
                        var tupleReturnTarget = inferredReturns;
                        var tupleReturnEvidence = new List<string>?[tuple.Elements.Count];
                        (BoundExpression Expression, string Type) BindTupleElement(int index, ExpressionSyntax element, string? hint)
                        {
                            if (tupleReturnTarget is null) return BindExpression(element, hint);
                            var saved = inferredReturns;
                            inferredReturns = new List<string>();
                            try
                            {
                                var value = BindExpression(element, hint);
                                tupleReturnEvidence[index] = inferredReturns;
                                return value;
                            }
                            finally { inferredReturns = saved; }
                        }
                        void FindTupleParameters(TypeShape shape)
                        {
                            if (shape.Arguments.Count == 0 && !SupportedType(shape.ToString(), activeTypeParameters)) tupleParameters.Add(shape.Name);
                            foreach (var argument in shape.Arguments) FindTupleParameters(argument);
                        }
                        if (expectedTuple is not null) foreach (var item in expectedTuple.Arguments) FindTupleParameters(item);
                        void FindTupleNulls(ExpressionSyntax element, TypeShape shape)
                        {
                            while (element is GroupSyntax group) element = group.Inner;
                            if (element is NullSyntax && !shape.IsNullable && shape.Arguments.Count == 0 && tupleParameters.Contains(shape.Name))
                                tupleNullableRequired.Add(shape.Name);
                            else if (element is TupleSyntax nested && shape.IsTuple && nested.Elements.Count == shape.Arguments.Count)
                                for (var i = 0; i < nested.Elements.Count; i++) FindTupleNulls(nested.Elements[i], shape.Arguments[i]);
                            else if (unions.TryGetValue(shape.Name, out var tupleUnion) && tupleUnion.TypeParameters.Count == shape.Arguments.Count)
                            {
                                Token? name = null; IReadOnlyList<ExpressionSyntax>? payloads = null;
                                if (element is CallSyntax call && !locals.ContainsKey(call.Name.Text) && cases.TryGetValue(call.Name.Text, out var candidates) &&
                                    candidates.Count == 1 && candidates[0].Union.Name.Text == shape.Name)
                                { name = call.Name; payloads = call.Arguments; }
                                else if (element is MemberCallSyntax member && member.Receiver is ValueSyntax qualifier &&
                                    !locals.ContainsKey(qualifier.Value.Text) && UnionQualifier(qualifier.Value.Text) == shape.Name)
                                { name = member.Member; payloads = member.Arguments; }
                                var unionCase = name is null ? null : tupleUnion.Cases.FirstOrDefault(item => item.Name.Text == name.Text);
                                if (unionCase is null || payloads is null || unionCase.PayloadTypes.Count != payloads.Count) return;
                                var map = tupleUnion.TypeParameters.Select((parameter, i) => (parameter.Text, Type: shape.Arguments[i].ToString()))
                                    .ToDictionary(pair => pair.Text, pair => pair.Type, StringComparer.Ordinal);
                                for (var i = 0; i < payloads.Count; i++)
                                    if (GenericTypes.TrySubstitute(unionCase.PayloadTypes[i].Text, map, out var payloadType) &&
                                        GenericTypes.TryParse(payloadType, out var payloadShape)) FindTupleNulls(payloads[i], payloadShape);
                            }
                        }
                        if (expectedTuple is not null)
                            for (var i = 0; i < tuple.Elements.Count; i++) FindTupleNulls(tuple.Elements[i], expectedTuple.Arguments[i]);
                        var tupleReachable = inferenceReachable; var tupleNever = false;
                        try
                        {
                            for (var i = 0; i < tuple.Elements.Count; i++)
                            {
                                inferenceReachable = tupleReachable && !tupleNever;
                                var element = tuple.Elements[i]; var formal = expectedTuple?.Arguments[i].ToString();
                                var hint = formal is null ? null : GenericTypes.Substitute(formal, tupleSubstitutions);
                                if (hint is not null && !SupportedType(hint, activeTypeParameters) &&
                                    (!GenericTypes.TryParse(hint, out var nestedHint) || !nestedHint.IsTuple)) hint = null;
                                if (inferencePass && tupleNever)
                                {
                                    GenericTypes.TryParse(hint ?? "Unit", out var placeholder);
                                    tupleTypes[i] = placeholder; tupleElements[i] = new BoundUnit();
                                    continue;
                                }
                                var elementCore = element;
                                while (elementCore is GroupSyntax elementGroup) elementCore = elementGroup.Inner;
                                if (elementCore is NullSyntax && formal is not null && !SupportedType(formal, activeTypeParameters))
                                {
                                    tupleValues[i] = (new BoundNull("<null>"), "<null>");
                                    continue;
                                }
                                (BoundExpression Expression, string Type) value;
                                var savedHasReturn = hasReturn;
                                try { value = BindTupleElement(i, element, hint); }
                                catch (SourceError error) when (formal is not null && !SupportedType(formal, activeTypeParameters) &&
                                    (error.Diagnostic.Code == "DS2002" && error.Diagnostic.Message == "Cannot infer the type of a null tuple element." ||
                                     error.Diagnostic.Code == "DS2023" && error.Diagnostic.Message == "Cannot infer all generic type arguments."))
                                { hasReturn = savedHasReturn; tupleDeferredErrors[i] = error; continue; }
                                if (value.Type == "<null>" && hint is null) Fail("DS2002", element.Token, "Cannot infer the type of a null tuple element.");
                                var elementType = hint ?? (value.Type == "<never>" ? KnownType(value.Expression) ?? "Unit" : value.Type);
                                if (hint is not null && !Convertible(value.Type, hint))
                                {
                                    if (formal is null || SupportedType(formal, activeTypeParameters)) Require(element, value.Type, hint);
                                    elementType = value.Type;
                                }
                                if (!GenericTypes.TryParse(elementType, out var elementShape)) Fail("DS2002", element.Token, "Cannot infer the tuple element type.");
                                tupleValues[i] = value;
                                tupleTypes[i] = elementShape;
                                tupleElements[i] = Coerce(value.Expression, elementType, value.Type);
                                if (formal is not null && value.Type != "<never>") GenericTypes.TryUnify(formal, value.Type, tupleParameters, tupleSubstitutions);
                                if (value.Type == "<never>") tupleNever = true;
                            }
                            if (expectedTuple is not null)
                            {
                                foreach (var parameter in tupleNullableRequired)
                                    if (tupleSubstitutions.TryGetValue(parameter, out var inferred) && GenericTypes.TryParse(inferred, out var inferredShape))
                                        tupleSubstitutions[parameter] = (inferredShape with { IsNullable = true }).ToString();
                                for (var i = 0; i < tuple.Elements.Count; i++)
                                {
                                    var formal = expectedTuple.Arguments[i];
                                    if (tupleValues[i]?.Type == "<null>" && tupleTypes[i] is null && !formal.IsNullable && formal.Arguments.Count == 0 &&
                                        tupleParameters.Contains(formal.Name) && tupleSubstitutions.TryGetValue(formal.Name, out var prior) &&
                                        GenericTypes.TryParse(prior, out var priorShape))
                                        tupleSubstitutions[formal.Name] = (priorShape with { IsNullable = true }).ToString();
                                }
                                for (var i = 0; i < tuple.Elements.Count; i++)
                                {
                                    var resolved = GenericTypes.Substitute(expectedTuple.Arguments[i].ToString(), tupleSubstitutions);
                                    var value = tupleValues[i];
                                    if (value is null && tupleTypes[i] is null)
                                    {
                                        if (!SupportedType(resolved, activeTypeParameters))
                                        {
                                            if (tupleDeferredErrors[i] is { } deferredError) throw deferredError;
                                            Fail("DS2002", tuple.Elements[i].Token, "Cannot infer the type of a null tuple element.");
                                        }
                                        inferenceReachable = tupleReachable && !tupleValues.Take(i).Any(item => item?.Type == "<never>");
                                        value = BindTupleElement(i, tuple.Elements[i], resolved);
                                        tupleValues[i] = value;
                                        if (value.Value.Type == "<never>") tupleNever = true;
                                    }
                                    if (value is null) continue;
                                    if (!SupportedType(resolved, activeTypeParameters))
                                    {
                                        if (tupleTypes[i] is null) Fail("DS2002", tuple.Elements[i].Token, "Cannot infer the type of a null tuple element.");
                                        continue;
                                    }
                                    if (tupleTypes[i] is null || Convertible(value.Value.Type, resolved))
                                    {
                                        Require(tuple.Elements[i], value.Value.Type, resolved);
                                        GenericTypes.TryParse(resolved, out var resolvedShape);
                                        tupleTypes[i] = resolvedShape;
                                        tupleElements[i] = Coerce(value.Value.Expression, resolved, value.Value.Type);
                                    }
                                }
                            }
                            if (tupleReturnTarget is not null)
                            {
                                var reachableElement = tupleReachable;
                                for (var i = 0; i < tuple.Elements.Count; i++)
                                {
                                    if (reachableElement && tupleReturnEvidence[i] is { } evidence) tupleReturnTarget.AddRange(evidence);
                                    if (tupleValues[i]?.Type == "<never>") reachableElement = false;
                                }
                            }
                        }
                        finally { inferenceReachable = tupleReachable; }
                        var tupleType = new TypeShape(TypeShape.TupleName, tupleTypes.Select(type => type!).ToArray(), false).ToString();
                        if (!SupportedType(tupleType, activeTypeParameters)) Fail("DS2026", tuple.Open, "Tuple elements must be immutable data values.");
                        return (new BoundTuple(tupleElements.Select(element => element!).ToArray(), tupleType), tupleNever ? "<never>" : tupleType);
                    case AwaitSyntax awaited:
                        if (!awaitAllowed) Fail("DS2026", awaited.Keyword, "Await is allowed only inside an async function.");
                        var awaitHint = expectedResult is not null && SupportedType(expectedResult, activeTypeParameters)
                            ? taskSymbol + "<" + expectedResult + ">" : null;
                        ExpressionSyntax directOperand = awaited.Operand;
                        while (directOperand is GroupSyntax awaitedGroup) directOperand = awaitedGroup.Inner;
                        var savedDirectAwait = directResourceAwait;
                        var savedActorHelperAwait = directlyAwaitedActorHelper;
                        var savedInvokeAwait = directlyAwaitedInvoke;
                        directlyAwaitedActorHelper = directOperand as CallSyntax;
                        directlyAwaitedInvoke = directOperand as CallSyntax;
                        if (directOperand is MemberCallSyntax directMember)
                        {
                            ExpressionSyntax directReceiver = directMember.Receiver;
                            while (directReceiver is GroupSyntax receiverGroup) directReceiver = receiverGroup.Inner;
                            directResourceAwait = directReceiver is ValueSyntax directHandle &&
                                (resourceHandles.Contains(directHandle.Value.Text) || IsActorDependency(directHandle.Value.Text));
                        }
                        else directResourceAwait = false;
                        (BoundExpression Expression, string Type) awaitOperand;
                        try { awaitOperand = BindExpression(awaited.Operand, awaitHint); }
                        finally { directResourceAwait = savedDirectAwait; directlyAwaitedActorHelper = savedActorHelperAwait; directlyAwaitedInvoke = savedInvokeAwait; }
                        var awaitedType = awaitOperand.Type == "<never>" ? KnownType(awaitOperand.Expression) : awaitOperand.Type;
                        if (awaitedType is null || !GenericTypes.TryParse(awaitedType, out var taskShape) ||
                            taskShape.Name != taskSymbol || taskShape.IsNullable || taskShape.Arguments.Count != 1)
                            Fail("DS2026", awaited.Keyword, "Await requires a non-nullable Task value.");
                        return (new BoundAwait(awaitOperand.Expression, taskShape!.Arguments[0].ToString()),
                            awaitOperand.Type == "<never>" ? "<never>" : taskShape.Arguments[0].ToString());
                    case LambdaSyntax lambda:
                        return BindLambda(lambda, expectedResult);
                    case InvokeSyntax invocation:
                        return BindInvocation(invocation.Receiver, invocation.OpenParen, invocation.Arguments, expectedResult);
                    case PipeSyntax pipe:
                        return BindPipe(pipe, expectedResult);
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
                        // A bottom-typed conditional or match has no operand
                        // value to inspect, even if a dead arm can complete. It
                        // can inhabit the required Result context, but must still
                        // execute to deliver its enclosing-function return. A
                        // typed call with a noncompleting argument retains its
                        // declared type and must still be checked below.
                        if (propagated.Type == "<never>" && propagated.Expression is BoundIf or BoundMatch)
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
                        if (resourceNames.Contains(successType) &&
                            (!ownedPropagations.Contains(propagation) || !OwnedResult(propagatedType!)))
                            Fail("DS2041", propagation.Operator, "An owned Result must be propagated directly into a using acquisition.");
                        return (new BoundPropagation(propagated.Expression, propagatedType!, successType, currentReturnType!),
                            propagated.Type == "<never>" ? "<never>" : successType);
                    case MatchSyntax match:
                        var scrutinee = BindExpression(match.Scrutinee);
                        var savedMatchReachable = inferenceReachable;
                        inferenceReachable = savedMatchReachable && scrutinee.Type != "<never>";
                        var scrutineeType = scrutinee.Type == "<never>" ? KnownType(scrutinee.Expression) : scrutinee.Type;
                        if (scrutineeType is null || !SupportedType(scrutineeType, activeTypeParameters))
                            Fail("DS2019", match.Scrutinee.Token, "Cannot determine the type being matched.");
                        var coverageUnions = syntax.Unions.Select(union => new BoundUnionDeclaration(union.Name.Text,
                            union.Cases.Select(unionCase => new BoundUnionCase(unionCase.Name.Text,
                                unionCase.PayloadTypes.Select(type => type.Text).ToArray())).ToArray())
                            { TypeParameters = union.TypeParameters.Select(parameter => parameter.Text).ToArray() }).ToArray();
                        var boundArms = new List<(BoundPattern Pattern, BoundExpression? Guard, BoundExpression Value, string Type, bool GuardNever)>();
                        BoundPattern? singletonPattern = scrutinee.Expression switch
                        {
                            BoundBool or BoundInt or BoundInteger or BoundDecimal or BoundFloat32 or BoundFloat64 or BoundText or BoundUnit => new BoundLiteralPattern(scrutinee.Expression, scrutineeType!),
                            BoundUnary { Operator: "-", Operand: BoundInt or BoundInteger or BoundDecimal or BoundFloat32 or BoundFloat64 } => new BoundLiteralPattern(scrutinee.Expression, scrutineeType!),
                            BoundNull => new BoundNullPattern(),
                            _ => null
                        };
                        string? matchResultType = null;
                        var hasReachableCompletingArm = false;
                        var hasDefinitelyCompletingArm = false;
                        var hasUncertainCompletingArm = false;
                        foreach (var arm in match.Arms)
                        {
                            var introduced = new List<string>();
                            var armReachable = savedMatchReachable && scrutinee.Type != "<never>";
                            var evidenceBeforeArm = inferredReturns?.Count ?? 0;
                            var uncertainArm = false;
                            try
                            {
                                inferenceReachable = armReachable;
                                var pattern = BindPattern(arm.Pattern, scrutineeType!, introduced);
                                var previousPatterns = boundArms.Where(item => item.Guard is null).Select(item => item.Pattern).ToArray();
                                var armPossible = MatchCoverage.IsUseful(scrutineeType!, previousPatterns, pattern, coverageUnions, out var definite);
                                if (armPossible && singletonPattern is not null)
                                {
                                    var pointUncovered = MatchCoverage.IsUseful(scrutineeType!, previousPatterns, singletonPattern, coverageUnions, out var pointDefinite);
                                    var pointOutsideArm = MatchCoverage.IsUseful(scrutineeType!, new[] { pattern }, singletonPattern, coverageUnions, out var armDefinite);
                                    if (pointDefinite && !pointUncovered || armDefinite && pointOutsideArm) armPossible = false;
                                    else definite &= pointDefinite && armDefinite;
                                }
                                armReachable &= armPossible;
                                uncertainArm = armReachable && !definite;
                                inferenceReachable = armReachable;
                                BoundExpression? guard = null;
                                var guardNever = false;
                                if (arm.Guard is not null)
                                {
                                    var boundGuard = BindExpression(arm.Guard);
                                    Require(arm.Guard, boundGuard.Type, "bool");
                                    guard = Coerce(boundGuard.Expression, "bool", boundGuard.Type);
                                    guardNever = boundGuard.Type == "<never>";
                                }
                                var guardFalse = arm.Guard is not null && LiteralCondition(arm.Guard) == false;
                                inferenceReachable = armReachable && !guardNever && !guardFalse;
                                var value = BindExpression(arm.Value, expectedResult);
                                if (uncertainArm && inferredReturns is not null && inferredReturns.Count > evidenceBeforeArm)
                                    Fail("DS2027", arm.Value.Token, "An explicit return type is required when match-arm reachability cannot be proven.");
                                if (armPossible && !guardNever && !guardFalse && value.Type != "<never>")
                                {
                                    hasReachableCompletingArm = true;
                                    if (definite) hasDefinitelyCompletingArm = true;
                                    else hasUncertainCompletingArm = true;
                                }
                                if (!guardNever && value.Type != "<never>")
                                {
                                    var branchValueType = expectedResult is not null && interfaces.ContainsKey(expectedResult) && Convertible(value.Type, expectedResult)
                                        ? expectedResult : value.Type;
                                    if (matchResultType is null) matchResultType = branchValueType;
                                    else
                                    {
                                        var common = CommonType(matchResultType, branchValueType);
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
                                inferenceReachable = armReachable;
                                foreach (var name in introduced) locals.Remove(name);
                            }
                        }
                        inferenceReachable = savedMatchReachable;
                        if (inferredReturns is not null && hasUncertainCompletingArm && !hasDefinitelyCompletingArm)
                            Fail("DS2027", match.Match, "An explicit return type is required when match completion cannot be proven.");
                        if (!MatchCoverage.IsExhaustive(scrutineeType!,
                            boundArms.Where(arm => arm.Guard is null).Select(arm => arm.Pattern).ToArray(), coverageUnions))
                            Fail("DS2020", match.Match, "Match patterns are not exhaustive.");
                        var emittedMatchType = matchResultType ?? "Unit";
                        var arms = boundArms.Select(arm => new BoundMatchArm(arm.Pattern, arm.Guard,
                            arm.GuardNever ? arm.Value : emittedMatchType == "<null>" && arm.Type == "<never>"
                                ? new BoundNever(arm.Value, "<null>") : Coerce(arm.Value, emittedMatchType, arm.Type), arm.GuardNever)).ToArray();
                        return (new BoundMatch(Coerce(scrutinee.Expression, scrutineeType!, scrutinee.Type), scrutineeType!, arms, emittedMatchType)
                            { HasCompletingArm = boundArms.Any(arm => !arm.GuardNever && arm.Type != "<never>") },
                            scrutinee.Type == "<never>" || !hasReachableCompletingArm ? "<never>" : emittedMatchType);
                    case ForSyntax loop:
                        var loopSource = BindExpression(loop.Source);
                        var loopSourceType = loopSource.Type == "<never>" ? KnownType(loopSource.Expression) : loopSource.Type;
                        if (loopSourceType is null || !GenericTypes.TryParse(loopSourceType, out var loopShape) ||
                            loopShape.Name != listSymbol || loopShape.IsNullable || loopShape.Arguments.Count != 1)
                            Fail("DS2026", loop.Source.Token, "For iteration requires a non-nullable List value.");
                        if (locals.ContainsKey(loop.Name.Text) || inaccessibleCaptures.Contains(loop.Name.Text))
                            Fail("DS2006", loop.Name, "Duplicate or shadowed local binding.");
                        var loopElementType = loopShape!.Arguments[0].ToString();
                        var loopEntryReachable = inferenceReachable;
                        (BoundBlock Block, string Type) loopBody;
                        locals.Add(loop.Name.Text, (0, loopElementType));
                        try
                        {
                            inferenceReachable = loopEntryReachable && loopSource.Type != "<never>";
                            loopBody = BindBlock(loop.Body, expectedResultType: "Unit");
                        }
                        finally
                        {
                            inferenceReachable = loopEntryReachable;
                            locals.Remove(loop.Name.Text);
                        }
                        Require(loop.Body.Result, loopBody.Type, "Unit");
                        return (new BoundFor(loop.Name.Text, Coerce(loopSource.Expression, loopSourceType!, loopSource.Type),
                            loopSourceType!, loopElementType, CoerceBlock(loopBody.Block, "Unit")),
                            loopSource.Type == "<never>" ? "<never>" : "Unit");
                    case UsingSyntax owned:
                    {
                        if (locals.ContainsKey(owned.Name.Text) || inaccessibleCaptures.Contains(owned.Name.Text))
                            Fail("DS2006", owned.Name, "Duplicate or shadowed local binding.");
                        ExpressionSyntax acquisition = owned.Acquisition;
                        while (acquisition is GroupSyntax acquisitionGroup) acquisition = acquisitionGroup.Inner;
                        if (acquisition is PropagateSyntax ownedPropagation)
                        {
                            ownedPropagations.Add(ownedPropagation);
                            MarkOwnedResult(ownedPropagation.Operand);
                            acquisition = ownedPropagation.Operand;
                            while (acquisition is GroupSyntax propagatedGroup) acquisition = propagatedGroup.Inner;
                        }
                        if (acquisition is AwaitSyntax acquisitionAwait)
                        {
                            acquisition = acquisitionAwait.Operand;
                            while (acquisition is GroupSyntax awaitedGroup) acquisition = awaitedGroup.Inner;
                        }
                        if (acquisition is not (CallSyntax or MemberCallSyntax))
                            Fail("DS2041", owned.Keyword, "Acquire a fresh resource or directly await an owned factory in the using initializer.");
                        if (owned.Acquisition is not PropagateSyntax) MarkOwnedResult(owned.Acquisition);
                        var acquired = BindExpression(owned.Acquisition);
                        var resourceName = acquired.Type == "<never>" ? KnownType(acquired.Expression) : acquired.Type;
                        if (resourceName is null || !resourceNames.Contains(resourceName))
                            Fail("DS2041", owned.Keyword, "A using initializer must produce an owned resource directly.");
                        var resource = services[resourceName!];
                        var releaseAsync = resourceDeclarations[resource.Name.Text].Methods.Single(method => method.Name.Text == "release").IsAsync;
                        if (releaseAsync && !awaitAllowed) Fail("DS2041", owned.Keyword, "Asynchronous resource cleanup requires an async function.");
                        var beforeScope = inferenceReachable;
                        (BoundBlock Block, string Type) scopeBody;
                        locals.Add(owned.Name.Text, (0, resource.Name.Text));
                        resourceHandles.Add(owned.Name.Text);
                        try
                        {
                            inferenceReachable = beforeScope && acquired.Type != "<never>";
                            scopeBody = BindBlock(owned.Body, expectedResultType: expectedResult);
                        }
                        finally
                        {
                            inferenceReachable = beforeScope;
                            resourceHandles.Remove(owned.Name.Text);
                            locals.Remove(owned.Name.Text);
                        }
                        var actualType = scopeBody.Type == "<never>" ? "Unit" : scopeBody.Type;
                        if (actualType == "<null>")
                        {
                            if (expectedResult is null || !IsNullable(expectedResult))
                                Fail("DS2002", owned.Keyword, "Cannot infer the contained type of a null resource scope result.");
                            actualType = expectedResult!;
                            scopeBody = (CoerceBlock(scopeBody.Block, actualType), actualType);
                        }
                        return (new BoundUsing(owned.Name.Text, acquired.Expression,
                            resource.Name.Text, scopeBody.Block, actualType, releaseAsync),
                            acquired.Type == "<never>" || scopeBody.Type == "<never>" ? "<never>" : actualType);
                    }
                    case IfSyntax conditional:
                        var condition = BindExpression(conditional.Condition);
                        Require(conditional.Condition, condition.Type, "bool");
                        var savedIfReachable = inferenceReachable;
                        var literalCondition = LiteralCondition(conditional.Condition);
                        inferenceReachable = savedIfReachable && condition.Type != "<never>" && literalCondition != false;
                        var thenBlock = BindBlock(conditional.Then, expectedResultType: expectedResult);
                        inferenceReachable = savedIfReachable && condition.Type != "<never>" && literalCondition != true;
                        var elseBlock = BindBlock(conditional.Else, expectedResultType: expectedResult);
                        inferenceReachable = savedIfReachable;
                        if (!conditional.HasElse && thenBlock.Type is not ("Unit" or "<never>"))
                            Fail("DS1002", conditional.MissingElseLocation ?? conditional.If, "An if without else requires a Unit branch.");
                        var branchType = expectedResult is not null && interfaces.ContainsKey(expectedResult) &&
                            Convertible(thenBlock.Type, expectedResult) && Convertible(elseBlock.Type, expectedResult)
                            ? expectedResult : CommonType(thenBlock.Type, elseBlock.Type);
                        if (branchType is null)
                        {
                            var location = conditional.Then.Result is UnitSyntax ? conditional.Then.Result : conditional.Else.Result;
                            Fail("DS2002", location.Token, "Conditional branches must have the same type.");
                        }
                        if (branchType is not null && IsNullable(branchType) && !SupportedType(branchType, activeTypeParameters))
                            Fail("DS2023", conditional.If, "Nullable unconstrained type parameters are not supported.");
                        var emittedType = branchType == "<never>" ? "Unit" : branchType;
                        if (condition.Type == "<never>" || literalCondition == true && thenBlock.Type == "<never>" ||
                            literalCondition == false && elseBlock.Type == "<never>") branchType = "<never>";
                        return (new BoundIf(Coerce(condition.Expression, "bool", condition.Type),
                            CoerceBlock(thenBlock.Block, emittedType!), CoerceBlock(elseBlock.Block, emittedType!), emittedType!), branchType!);
                    case GroupSyntax group: return BindExpression(group.Inner, expectedResult, pipeInputType, pipeOutputType, pipeNullInput, groupedArgumentTypes, groupedExpectedResult, isPipeTarget);
                    case CoalesceSyntax coalesce:
                        var nullableLeft = BindExpression(coalesce.Left);
                        var savedCoalesceReachable = inferenceReachable;
                        inferenceReachable = savedCoalesceReachable && nullableLeft.Type != "<never>";
                        var leftType = nullableLeft.Type == "<never>" ? KnownType(nullableLeft.Expression) : nullableLeft.Type;
                        if (nullableLeft.Type == "<never>" && (leftType is null || !IsNullable(leftType))) leftType = "<null>";
                        string? fallbackHint = null;
                        if (leftType is not null && IsNullable(leftType) && GenericTypes.TryParse(leftType[..^1], out var fallbackShape) && fallbackShape.IsTuple)
                            fallbackHint = leftType[..^1];
                        var fallback = BindExpression(coalesce.Right, fallbackHint);
                        inferenceReachable = savedCoalesceReachable;
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
                    case ConditionalMemberCallSyntax optionalCall:
                    {
                        var conditionalReceiver = BindExpression(optionalCall.Receiver);
                        var conditionalReceiverType = conditionalReceiver.Type == "<never>"
                            ? KnownType(conditionalReceiver.Expression) : conditionalReceiver.Type;
                        var presentReceiverType = conditionalReceiverType is not null && IsNullable(conditionalReceiverType)
                            ? conditionalReceiverType[..^1] : conditionalReceiverType;
                        if (presentReceiverType is null or "<null>" || !SupportedType(presentReceiverType, activeTypeParameters) || !DataType(presentReceiverType))
                            Fail("DS2026", optionalCall.Member, "Conditional calls require an immutable value receiver.");

                        // Bind the normal member call against a synthetic present
                        // value. This keeps overload selection, imports, argument
                        // checks and generic inference identical to a direct call.
                        var receiverName = "$conditional_receiver_" + (conditionalReceiverCounter++).ToString(CultureInfo.InvariantCulture);
                        var receiverToken = new Token(receiverName, optionalCall.Receiver.Token.Span) { OriginPath = optionalCall.Receiver.Token.OriginPath };
                        var directCall = new MemberCallSyntax(new ValueSyntax(receiverToken), optionalCall.Member, optionalCall.OpenParen, optionalCall.Arguments)
                        {
                            TypeArguments = optionalCall.TypeArguments,
                            ExtensionCandidates = optionalCall.ExtensionCandidates
                        };
                        locals.Add(receiverName, (0, presentReceiverType!));
                        var wasReachable = inferenceReachable;
                        inferenceReachable = wasReachable && conditionalReceiver.Type != "<never>";
                        (BoundExpression Expression, string Type) called;
                        try
                        {
                            var hint = expectedResult is not null && IsNullable(expectedResult) ? expectedResult[..^1] : expectedResult;
                            called = BindExpression(directCall, hint);
                        }
                        finally { inferenceReachable = wasReachable; locals.Remove(receiverName); }
                        var callType = called.Type == "<never>" ? KnownType(called.Expression) : called.Type;
                        if (callType is null || !SupportedType(callType, activeTypeParameters) || !DataType(callType) ||
                            called.Expression is BoundPartial or BoundCollectionMethodPartial or BoundListFoldPartial or BoundMapSetPartial or BoundServiceCall or BoundBuilderOperation)
                            Fail("DS2026", optionalCall.Member, "Conditional calls require a fully supplied synchronous immutable-value method.");
                        var liftedType = IsNullable(callType!) ? callType! : callType! + "?";
                        if (!SupportedType(liftedType, activeTypeParameters))
                            Fail("DS2023", optionalCall.Member, "Unsupported nullable conditional call result.");
                        var liftedCall = Coerce(called.Expression, liftedType, called.Type);
                        return (new BoundConditionalCall(Coerce(conditionalReceiver.Expression, presentReceiverType! + "?", conditionalReceiver.Type),
                            presentReceiverType!, receiverName, liftedCall, liftedType), conditionalReceiver.Type == "<never>" ? "<never>" : liftedType);
                    }
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
                        if (access.Receiver is ValueSyntax capturedFieldQualifier && !locals.ContainsKey(capturedFieldQualifier.Value.Text) && inaccessibleCaptures.Contains(capturedFieldQualifier.Value.Text))
                            Fail("DS2026", capturedFieldQualifier.Value, "Lambda capture is not supported.");
                        if (access.Receiver is ValueSyntax typeQualifier && !locals.ContainsKey(typeQualifier.Value.Text) &&
                            functionTypeScope.ContainsKey(typeQualifier.Value.Text) && unions.ContainsKey(UnionQualifier(typeQualifier.Value.Text)))
                            Fail("DS2018", typeQualifier.Value, "An unconstrained type parameter cannot qualify a union case.");
                        if (access.Receiver is ValueSyntax qualifier && !locals.ContainsKey(qualifier.Value.Text) &&
                            unions.TryGetValue(UnionQualifier(qualifier.Value.Text), out var qualifiedUnion))
                            return BindQualifiedCase(qualifiedUnion, access.Field, Array.Empty<ExpressionSyntax>(), expectedResult: expectedResult);
                        var receiver = BindExpression(access.Receiver);
                        var receiverType = receiver.Type == "<never>" ? KnownType(receiver.Expression) : receiver.Type;
                        if (receiverType is not null && privateResourceContracts.Contains(receiverType) && access.Field.Text == "release")
                            Fail("DS2041", access.Field, "Actor resource cleanup is owned by the host.");
                        var nullableMethodReceiver = receiverType is not null && IsNullable(receiverType);
                        // An extension may explicitly target a nullable type.
                        // Actual fields still require a non-null receiver.
                        records.TryGetValue(nullableMethodReceiver ? "" : Nominal(receiverType ?? ""), out var accessedRecord);
                        var accessedField = accessedRecord?.Fields.FirstOrDefault(field => field.Name.Text == access.Field.Text);
                        // Real fields retain precedence even if their type does
                        // not match the expected callable context.
                        if (accessedField is not null)
                        {
                            var fieldSubstitutions = SubstitutionsFor(receiverType, accessedRecord!.Name.Text, accessedRecord.TypeParameters);
                            if (fieldSubstitutions is null) Fail("DS2023", access.Field, "Invalid generic record type.");
                            var accessedFieldType = Substitute(accessedField.Type.Text, fieldSubstitutions!, accessedField.Type);
                            return (new BoundFieldAccess(Coerce(receiver.Expression, receiverType!, receiver.Type), receiverType!, access.Field.Text),
                                receiver.Type == "<never>" ? "<never>" : accessedFieldType);
                        }
                        // A bare intrinsic in a pipe receives the same first-
                        // argument and remaining-result evidence as its empty-
                        // prefix call. Derive a complete callable type here so
                        // the normal method-value checks and BoundPipe lowering
                        // retain left-before-receiver evaluation. Do not use
                        // this context to select extension method values. Bare
                        // extensions use the separate candidate check below.
                        var fieldMethodContext = expectedResult;
                        var hasFieldPipeContext = pipeInputType is not null || pipeNullInput || pipeOutputType is not null || groupedArgumentTypes is not null;
                        if (fieldMethodContext is null && hasFieldPipeContext && receiverType is not null &&
                            GenericTypes.TryParse(receiverType, out var fieldPipeReceiver) && !fieldPipeReceiver.IsNullable)
                        {
                            // Applying an empty group to a nonzero-arity
                            // collection method keeps its whole callable type.
                            // That expected type can establish map/fold's type
                            // parameter just as an explicitly typed value can.
                            if (groupedArgumentTypes is { Count: 0 } && groupedExpectedResult is not null &&
                                GenericTypes.TryParse(groupedExpectedResult, out var groupedCallable) &&
                                groupedCallable.IsFunction && !groupedCallable.IsNullable &&
                                (fieldPipeReceiver.Name == listSymbol || fieldPipeReceiver.Name == mapSymbol || fieldPipeReceiver.Name == setSymbol))
                                fieldMethodContext = groupedExpectedResult;
                            string[]? fieldPipeParameters = null;
                            string? fieldPipeResult = null;
                            if (fieldPipeReceiver.Name == listSymbol && fieldPipeReceiver.Arguments.Count == 1)
                            {
                                var element = fieldPipeReceiver.Arguments[0].ToString();
                                switch (access.Field.Text)
                                {
                                    case "length" when groupedArgumentTypes is { Count: 0 }: fieldPipeParameters = Array.Empty<string>(); fieldPipeResult = "int"; break;
                                    case "append": fieldPipeParameters = new[] { element }; fieldPipeResult = receiverType; break;
                                    case "get": fieldPipeParameters = new[] { "int" }; fieldPipeResult = optionSymbol + "<" + element + ">"; break;
                                    case "filter": fieldPipeParameters = new[] { TypeShape.FunctionName + "<" + element + ",bool>" }; fieldPipeResult = receiverType; break;
                                    case "map":
                                    {
                                        string? mapped = null;
                                        var mappedOutput = groupedArgumentTypes is { Count: > 0 } ? groupedExpectedResult : pipeOutputType;
                                        if (mappedOutput is not null && GenericTypes.TryParse(mappedOutput, out var expectedList) &&
                                            !expectedList.IsNullable && expectedList.Name == listSymbol && expectedList.Arguments.Count == 1)
                                            mapped = expectedList.Arguments[0].ToString();
                                        if (mapped is null && pipeInputType is not null && GenericTypes.TryParse(pipeInputType, out var callback) &&
                                            callback.IsFunction && !callback.IsNullable && callback.Arguments.Count == 2 &&
                                            callback.Arguments[0].ToString() == element)
                                            mapped = callback.Arguments[1].ToString();
                                        if (mapped is not null)
                                        {
                                            fieldPipeParameters = new[] { TypeShape.FunctionName + "<" + element + "," + mapped + ">" };
                                            fieldPipeResult = listSymbol + "<" + mapped + ">";
                                        }
                                        break;
                                    }
                                    case "fold":
                                    {
                                        string? accumulator = null;
                                        if (pipeOutputType is not null && GenericTypes.TryParse(pipeOutputType, out var remaining) &&
                                            remaining.IsFunction && !remaining.IsNullable && remaining.Arguments.Count == 2)
                                        {
                                            var callback = remaining.Arguments[0];
                                            if (callback.IsFunction && !callback.IsNullable && callback.Arguments.Count == 3 &&
                                                callback.Arguments[1].ToString() == element &&
                                                callback.Arguments[0].ToString() == callback.Arguments[2].ToString() &&
                                                callback.Arguments[0].ToString() == remaining.Arguments[1].ToString())
                                                accumulator = remaining.Arguments[1].ToString();
                                        }
                                        if (accumulator is null && groupedArgumentTypes is { Count: > 1 } &&
                                            groupedExpectedResult is not null && SupportedType(groupedExpectedResult, activeTypeParameters))
                                            accumulator = groupedExpectedResult;
                                        accumulator ??= pipeInputType;
                                        if (accumulator is not null)
                                        {
                                            fieldPipeParameters = new[] { accumulator, TypeShape.FunctionName + "<" + accumulator + "," + element + "," + accumulator + ">" };
                                            fieldPipeResult = accumulator;
                                        }
                                        break;
                                    }
                                }
                            }
                            else if (fieldPipeReceiver.Name == mapSymbol && fieldPipeReceiver.Arguments.Count == 2)
                            {
                                var key = fieldPipeReceiver.Arguments[0].ToString();
                                var value = fieldPipeReceiver.Arguments[1].ToString();
                                switch (access.Field.Text)
                                {
                                    case "count" when groupedArgumentTypes is { Count: 0 }: fieldPipeParameters = Array.Empty<string>(); fieldPipeResult = "int"; break;
                                    case "get": fieldPipeParameters = new[] { key }; fieldPipeResult = optionSymbol + "<" + value + ">"; break;
                                    case "contains": fieldPipeParameters = new[] { key }; fieldPipeResult = "bool"; break;
                                    case "set": fieldPipeParameters = new[] { key, value }; fieldPipeResult = receiverType; break;
                                }
                            }
                            else if (fieldPipeReceiver.Name == setSymbol && fieldPipeReceiver.Arguments.Count == 1)
                            {
                                var element = fieldPipeReceiver.Arguments[0].ToString();
                                switch (access.Field.Text)
                                {
                                    case "count" when groupedArgumentTypes is { Count: 0 }: fieldPipeParameters = Array.Empty<string>(); fieldPipeResult = "int"; break;
                                    case "contains": fieldPipeParameters = new[] { element }; fieldPipeResult = "bool"; break;
                                    case "add": fieldPipeParameters = new[] { element }; fieldPipeResult = receiverType; break;
                                }
                            }
                            if (fieldPipeParameters is not null && fieldPipeResult is not null)
                                fieldMethodContext = TypeShape.FunctionName + "<" + string.Join(",", fieldPipeParameters.Append(fieldPipeResult)) + ">";
                        }
                        // An immediate grouped invocation supplies argument and
                        // result evidence even when the bare extension has no
                        // separately annotated function type. This path is not
                        // used by uncontextualized reads.
                        if (fieldMethodContext is null && groupedArgumentTypes is not null && receiverType is not null &&
                            SupportedType(receiverType, activeTypeParameters) && DataType(receiverType))
                        {
                            var groupedMethods = extensionDeclarations.Where(extension => extension.Method.Name.Text == access.Field.Text &&
                                (access.ExtensionCandidates is null || access.ExtensionCandidates.Contains(extension.Id)) &&
                                signatures[extension.Id].Parameters[0].Type.Text == receiverType).Where(extension =>
                            {
                                var target = signatures[extension.Id];
                                var formal = target.Parameters.Skip(1).Select(parameter => parameter.Type.Text).ToArray();
                                if (groupedArgumentTypes.Count > formal.Length) return false;
                                for (var i = 0; i < groupedArgumentTypes.Count; i++)
                                    if (groupedArgumentTypes[i] is { } actual && !Convertible(actual, formal[i])) return false;
                                var result = CallableResult(target);
                                var after = groupedArgumentTypes.Count == formal.Length ? result :
                                    TypeShape.FunctionName + "<" + string.Join(",", formal.Skip(groupedArgumentTypes.Count).Append(result)) + ">";
                                return groupedExpectedResult is null || !SupportedType(groupedExpectedResult, activeTypeParameters) ||
                                    Convertible(after, groupedExpectedResult);
                            }).ToArray();
                            // A fully supplied overload retains the existing
                            // precedence over a partial one when both fit.
                            var completeMethods = groupedMethods.Where(extension =>
                                signatures[extension.Id].Parameters.Count == groupedArgumentTypes.Count + 1).ToArray();
                            if (completeMethods.Length > 0) groupedMethods = completeMethods;
                            if (groupedMethods.Length > 1)
                                Fail("DS2018", access.Field, "Ambiguous extension method value; candidates: " + string.Join(", ", groupedMethods.Select(extension =>
                                    (extension.Method.OriginModule ?? syntax.Name.Text) + " (" + (extension.Method.Name.OriginPath ?? path) + ":" +
                                    extension.Method.Name.Span.Line.ToString(CultureInfo.InvariantCulture) + ":" + extension.Method.Name.Span.Column.ToString(CultureInfo.InvariantCulture) + ")")
                                    .OrderBy(name => name, StringComparer.Ordinal)) + ".");
                            if (groupedMethods.Length == 1)
                            {
                                var method = signatures[groupedMethods[0].Id];
                                fieldMethodContext = TypeShape.FunctionName + "<" + string.Join(",", method.Parameters.Skip(1)
                                    .Select(parameter => parameter.Type.Text).Append(CallableResult(method))) + ">";
                            }
                        }
                        // Intrinsic names keep priority even when their method
                        // type cannot be established from the available context.
                        var collectionCollision = GenericTypes.TryParse(receiverType ?? "", out var methodReceiverShape) &&
                            CollectionMethodCollision(methodReceiverShape, access.Field.Text);
                        // A bare extension on the right of a pipe can be chosen
                        // by the incoming argument and the remaining result. If
                        // the input needs context (for example, a lambda), a
                        // unique candidate may supply it; ambiguity is never
                        // resolved by declaration order. A zero-argument method
                        // is not implicitly invoked to obtain a pipe target.
                        if (fieldMethodContext is null && isPipeTarget && groupedArgumentTypes is null && !collectionCollision && receiverType is not null &&
                            SupportedType(receiverType, activeTypeParameters) && DataType(receiverType))
                        {
                            var pipeMethods = extensionDeclarations.Where(extension => extension.Method.Name.Text == access.Field.Text &&
                                (access.ExtensionCandidates is null || access.ExtensionCandidates.Contains(extension.Id)) &&
                                signatures[extension.Id].Parameters[0].Type.Text == receiverType).Where(extension =>
                            {
                                var method = signatures[extension.Id];
                                var formal = method.Parameters.Skip(1).Select(parameter => parameter.Type.Text).ToArray();
                                if (formal.Length == 0 || (pipeInputType is not null || pipeNullInput) &&
                                    !Convertible(pipeInputType ?? "<null>", formal[0])) return false;
                                var result = CallableResult(method);
                                var after = formal.Length == 1 ? result :
                                    TypeShape.FunctionName + "<" + string.Join(",", formal.Skip(1).Append(result)) + ">";
                                return pipeOutputType is null || !SupportedType(pipeOutputType, activeTypeParameters) ||
                                    Convertible(after, pipeOutputType);
                            }).ToArray();
                            // Match ordinary grouped calls: a fully supplied
                            // applicable overload takes precedence over a
                            // partial overload, but ties remain ambiguous. Do
                            // not apply that preference when the input needs
                            // context: applicability has not been established.
                            if (pipeInputType is not null || pipeNullInput)
                            {
                                var completePipeMethods = pipeMethods.Where(extension =>
                                    signatures[extension.Id].Parameters.Count == 2).ToArray();
                                if (completePipeMethods.Length > 0) pipeMethods = completePipeMethods;
                            }
                            if (pipeMethods.Length > 1)
                                Fail("DS2018", access.Field, "Ambiguous extension method value; candidates: " + string.Join(", ", pipeMethods.Select(extension =>
                                    (extension.Method.OriginModule ?? syntax.Name.Text) + " (" + (extension.Method.Name.OriginPath ?? path) + ":" +
                                    extension.Method.Name.Span.Line.ToString(CultureInfo.InvariantCulture) + ":" + extension.Method.Name.Span.Column.ToString(CultureInfo.InvariantCulture) + ")")
                                    .OrderBy(name => name, StringComparer.Ordinal)) + ".");
                            if (pipeMethods.Length == 1)
                            {
                                var method = signatures[pipeMethods[0].Id];
                                fieldMethodContext = TypeShape.FunctionName + "<" + string.Join(",", method.Parameters.Skip(1)
                                    .Select(parameter => parameter.Type.Text).Append(CallableResult(method))) + ">";
                            }
                        }
                        if (receiverType is not null && SupportedType(receiverType, activeTypeParameters) && DataType(receiverType) &&
                            fieldMethodContext is not null && GenericTypes.TryParse(fieldMethodContext, out var expectedMethod) &&
                            expectedMethod.IsFunction && !expectedMethod.IsNullable)
                        {
                            // Capture only a real immutable collection method
                            // whose complete callable type is fixed by context.
                            // As for empty-prefix calls, retain the receiver once
                            // and defer the operation until invocation.
                            if (collectionCollision)
                            {
                                string[]? parameters = null;
                                string? returnType = null;
                                string? methodTypeArgument = null;
                                var methodName = access.Field.Text;
                                if (methodReceiverShape!.Name == listSymbol && methodReceiverShape.Arguments.Count == 1)
                                {
                                    var element = methodReceiverShape.Arguments[0].ToString();
                                    switch (methodName)
                                    {
                                        case "length": parameters = Array.Empty<string>(); returnType = "int"; break;
                                        case "append": parameters = new[] { element }; returnType = receiverType; break;
                                        case "get": parameters = new[] { "int" }; returnType = optionSymbol + "<" + element + ">"; break;
                                        case "filter": parameters = new[] { TypeShape.FunctionName + "<" + element + ",bool>" }; returnType = receiverType; break;
                                        case "map" when expectedMethod.Arguments.Count == 2:
                                        {
                                            var callback = expectedMethod.Arguments[0];
                                            var result = expectedMethod.Arguments[1];
                                            if (callback.IsFunction && !callback.IsNullable && callback.Arguments.Count == 2 &&
                                                callback.Arguments[0].ToString() == element && result.Name == listSymbol && !result.IsNullable &&
                                                result.Arguments.Count == 1 && callback.Arguments[1].ToString() == result.Arguments[0].ToString())
                                            {
                                                methodTypeArgument = callback.Arguments[1].ToString();
                                                parameters = new[] { callback.ToString() };
                                                returnType = result.ToString();
                                            }
                                            break;
                                        }
                                        case "fold" when expectedMethod.Arguments.Count == 3:
                                        {
                                            var accumulator = expectedMethod.Arguments[0].ToString();
                                            var callback = expectedMethod.Arguments[1];
                                            if (callback.IsFunction && !callback.IsNullable && callback.Arguments.Count == 3 &&
                                                callback.Arguments[0].ToString() == accumulator && callback.Arguments[1].ToString() == element &&
                                                callback.Arguments[2].ToString() == accumulator && expectedMethod.Arguments[2].ToString() == accumulator)
                                            {
                                                methodTypeArgument = accumulator;
                                                parameters = new[] { accumulator, callback.ToString() };
                                                returnType = accumulator;
                                            }
                                            break;
                                        }
                                    }
                                }
                                else if (methodReceiverShape.Name == mapSymbol && methodReceiverShape.Arguments.Count == 2)
                                {
                                    var key = methodReceiverShape.Arguments[0].ToString();
                                    var value = methodReceiverShape.Arguments[1].ToString();
                                    switch (methodName)
                                    {
                                        case "count": parameters = Array.Empty<string>(); returnType = "int"; break;
                                        case "get": parameters = new[] { key }; returnType = optionSymbol + "<" + value + ">"; break;
                                        case "contains": parameters = new[] { key }; returnType = "bool"; break;
                                        case "set": parameters = new[] { key, value }; returnType = receiverType; break;
                                    }
                                }
                                else if (methodReceiverShape.Name == setSymbol && methodReceiverShape.Arguments.Count == 1)
                                {
                                    var element = methodReceiverShape.Arguments[0].ToString();
                                    switch (methodName)
                                    {
                                        case "count": parameters = Array.Empty<string>(); returnType = "int"; break;
                                        case "contains": parameters = new[] { element }; returnType = "bool"; break;
                                        case "add": parameters = new[] { element }; returnType = receiverType; break;
                                    }
                                }
                                if (parameters is not null && returnType is not null &&
                                    TypeShape.FunctionName + "<" + string.Join(",", parameters.Append(returnType)) + ">" == fieldMethodContext &&
                                    (methodTypeArgument is null || SupportedType(methodTypeArgument, activeTypeParameters) &&
                                        (methodName != "map" || DataType(methodTypeArgument))))
                                {
                                    if (methodName == "get") optionUsed = true;
                                    var partial = new BoundCollectionMethodPartial(receiver.Expression, receiverType, methodName,
                                        parameters, returnType, fieldMethodContext);
                                    if (methodTypeArgument is not null) partial = partial with { TypeArguments = new[] { methodTypeArgument } };
                                    return (partial, receiver.Type == "<never>" ? "<never>" : fieldMethodContext);
                                }
                            }
                            if (!collectionCollision && (expectedResult is not null || groupedArgumentTypes is not null || isPipeTarget))
                            {
                                var methodCandidates = extensionDeclarations.Where(extension => extension.Method.Name.Text == access.Field.Text &&
                                    (access.ExtensionCandidates is null || access.ExtensionCandidates.Contains(extension.Id)) &&
                                    signatures[extension.Id].Parameters[0].Type.Text == receiverType &&
                                    TypeShape.FunctionName + "<" + string.Join(",", signatures[extension.Id].Parameters.Skip(1)
                                        .Select(parameter => parameter.Type.Text).Append(CallableResult(signatures[extension.Id]))) + ">" == fieldMethodContext).ToArray();
                                if (methodCandidates.Length > 1)
                                    Fail("DS2018", access.Field, "Ambiguous extension method value; candidates: " + string.Join(", ", methodCandidates.Select(extension =>
                                        (extension.Method.OriginModule ?? syntax.Name.Text) + " (" + (extension.Method.Name.OriginPath ?? path) + ":" +
                                        extension.Method.Name.Span.Line.ToString(CultureInfo.InvariantCulture) + ":" + extension.Method.Name.Span.Column.ToString(CultureInfo.InvariantCulture) + ")")
                                        .OrderBy(name => name, StringComparer.Ordinal)) + ".");
                                if (methodCandidates.Length == 1)
                                {
                                    if (inferencePass && inferenceReachable && inferredReturns is not null &&
                                        !(isPipeTarget && provenExtensionPipes.TryGetValue(access, out var provenExtension) && provenExtension == methodCandidates[0].Id))
                                        Fail("DS2027", access.Field, "An explicit return type is required for extension method values.");
                                    var extension = methodCandidates[0];
                                    var methodTarget = signatures[extension.Id];
                                    var formalTypes = methodTarget.Parameters.Select(parameter => parameter.Type.Text).ToArray();
                                    if (CapabilityType(formalTypes[0]))
                                        Fail("DS2026", access.Field, "A method value cannot capture a service or interface reference.");
                                    var result = CallableResult(methodTarget);
                                    var fullType = TypeShape.FunctionName + "<" + string.Join(",", formalTypes.Append(result)) + ">";
                                    if (methodTarget.IsAsync) taskUsed = true;
                                    return (new BoundPartial(new BoundFunctionReference(extension.Id, fullType, Array.Empty<string>()) { Location = access.Field }, fullType,
                                        new[] { Coerce(receiver.Expression, formalTypes[0], receiver.Type) }, new[] { formalTypes[0] },
                                        formalTypes.Skip(1).ToArray(), result, fieldMethodContext), receiver.Type == "<never>" ? "<never>" : fieldMethodContext);
                                }
                            }
                        }
                        if (nullableMethodReceiver) Fail("DS2016", access.Field, "Nullable records require conditional access or matching.");
                        if (accessedRecord is null) Fail("DS2016", access.Field, "Field access requires a record value.");
                        Fail("DS2016", access.Field, "Unknown record field.");
                        throw new InvalidOperationException("Unreachable field access binding.");
                    case RecordUpdateSyntax update:
                        var original = BindExpression(update.Receiver);
                        var originalType = original.Type == "<never>" ? KnownType(original.Expression) : original.Type;
                        if (originalType is not null && IsNullable(originalType))
                            Fail("DS2016", update.With, "A nullable record cannot be copied without matching.");
                        if (!records.TryGetValue(Nominal(originalType ?? ""), out var updatedRecord))
                            Fail("DS2016", update.With, "A copy update requires a record value.");
                        var savedUpdateReachable = inferenceReachable;
                        inferenceReachable = savedUpdateReachable && original.Type != "<never>";
                        (IReadOnlyList<BoundRecordInitializer> Fields, bool Never, string Type) changes;
                        try { changes = BindRecordFields(updatedRecord!, update.Fields, update.With, false, originalType); }
                        finally { inferenceReachable = savedUpdateReachable; }
                        return (new BoundRecordUpdate(Coerce(original.Expression, originalType!, original.Type), originalType!, changes.Fields),
                            original.Type == "<never>" || changes.Never ? "<never>" : originalType!);
                    case ValueSyntax valueSyntax:
                        var token = valueSyntax.Value;
                        if (IsActorDependency(token.Text) && !actorBorrowArguments.Contains(valueSyntax))
                            Fail("DS2041", token, "An actor dependency may only be used as the direct receiver of a host-owned operation.");
                        if (!locals.ContainsKey(token.Text) && actorMethodsByBody.TryGetValue(function.Name.Text, out var actorValueContext) &&
                            actorValueContext.Methods.ContainsKey(token.Text))
                            Fail("DS2042", token, "Actor methods cannot be used as function values.");
                        if (resourceHandles.Contains(token.Text))
                            Fail("DS2041", token, "A resource handle can only be used as the direct receiver of an operation.");
                        if (resultHelpers.ContainsKey(function.Name.Text) && !locals.ContainsKey(token.Text) && !inaccessibleCaptures.Contains(token.Text) &&
                            preludeNames.TryGetValue(token.Text, out var preludeValueName))
                            token = token with { Text = preludeValueName };
                        if (token.Text.Length > 0 && (token.Text[0] is '\"' or '\''))
                        {
                            var text = DecodeText(token, token.OriginPath ?? path);
                            var isChar = token.Text[0] == '\'';
                            if (isChar && text.Length != 1) Fail("DS2014", token, "A char literal must contain exactly one UTF-16 code unit.");
                            return (new BoundText(text, isChar), isChar ? "char" : "string");
                        }
                        if (token.Text is "true" or "false") return (new BoundBool(token.Text == "true"), "bool");
                        if (token.Text.Length > 0 && char.IsAsciiDigit(token.Text[0]))
                        {
                            if (token.Text.EndsWith('m'))
                            {
                                if (!DecimalLiterals.TryParse(token.Text, out var decimalValue))
                                    Fail("DS2004", token, "Invalid decimal literal or value not exactly representable.");
                                return (decimalValue, "decimal");
                            }
                            // Hexadecimal digits may end in f, which is not a float suffix.
                            if (token.Text.EndsWith('f') && !token.Text.StartsWith("0x", StringComparison.Ordinal) && !token.Text.StartsWith("0b", StringComparison.Ordinal))
                            {
                                if (!Float32Literals.TryParse(token.Text, out var bits))
                                    Fail("DS2004", token, "Invalid float32 literal or value outside the finite range.");
                                return (new BoundFloat32(bits), "float32");
                            }
                            // A suffix-free decimal point or exponent selects binary64.
                            // Hexadecimal digits (including e/f) remain integer tokens.
                            if (!token.Text.StartsWith("0x", StringComparison.Ordinal) && !token.Text.StartsWith("0b", StringComparison.Ordinal) &&
                                token.Text.IndexOfAny(new[] { '.', 'e', 'E' }) >= 0)
                            {
                                if (!Float64Literals.TryParse(token.Text, out var bits))
                                    Fail("DS2004", token, "Invalid float literal or value outside the finite range.");
                                return (new BoundFloat64(bits), "float");
                            }
                            if (!IntegerLiterals.TryParse(token.Text, out var magnitude, out var longSuffix))
                                Fail("DS2004", token, "Invalid integer literal or magnitude outside the supported range.");
                            var hinted = expectedResult is not null && IsNullable(expectedResult) ? expectedResult[..^1] : expectedResult;
                            var integerType = longSuffix ? "long" : IntegerType(hinted) ? hinted! : "int";
                            if (magnitude > IntegerMaximum(integerType))
                                Fail("DS2004", token, $"Integer literal is outside the {integerType} range.");
                            return integerType == "int" ? (new BoundInt((int)magnitude), integerType) : (new BoundInteger(magnitude, integerType), integerType);
                        }
                        if (!locals.ContainsKey(token.Text) && inaccessibleCaptures.Contains(token.Text))
                            Fail("DS2026", token, "Lambda capture is not supported.");
                        if (!locals.ContainsKey(token.Text) && defaultReceivers.TryGetValue(function.Name.Text, out var valueDefault) &&
                            interfaceMethods[valueDefault.Interface].ContainsKey(token.Text))
                            Fail("DS2040", token, "Interface methods cannot be used as values.");
                        if (!locals.TryGetValue(token.Text, out var local) && cases.ContainsKey(token.Text))
                        {
                            var selected = BareCase(token);
                            return BindCase(selected.Union, selected.Case, token, Array.Empty<ExpressionSyntax>(), expectedResult: expectedResult);
                        }
                        if (!locals.ContainsKey(token.Text) && !inaccessibleCaptures.Contains(token.Text) && !IsDefaultMethodName(token.Text) &&
                            TryResultOverloadFamily(token.Text, out var valueFamily) && !resolvingResultOverloads.Contains(valueSyntax))
                            return BindResultOverload(valueSyntax, token, valueFamily, expectedResult, pipeInputType, pipeOutputType,
                                pipeNullInput, groupedArgumentTypes, groupedExpectedResult, isPipeTarget);
                        if (!locals.ContainsKey(token.Text) && signatures.TryGetValue(token.Text, out var namedFunction))
                        {
                            if (ownedFactories.Contains(token.Text))
                                Fail("DS2041", token, "An owned resource factory cannot be used as a function value.");
                            if (resultHelpers.ContainsKey(token.Text)) { usedResultHelpers.Add(token.Text); resultUsed = true; }
                            var fullPattern = TypeShape.FunctionName + "<" + string.Join(",", namedFunction.Parameters.Select(p => p.Type.Text).Append(CallableResult(namedFunction))) + ">";
                            var inferred = BindGenericArguments(namedFunction.TypeParameters, Array.Empty<Token>(), Array.Empty<Token>(), Array.Empty<ExpressionSyntax>(), token, fullPattern, expectedResult);
                            var fullType = Substitute(fullPattern, inferred.Substitutions, token);
                            return (new BoundFunctionReference(token.Text, fullType, namedFunction.TypeParameters.Select(p => inferred.Substitutions[p.Text]).ToArray()) { Location = token }, fullType);
                        }
                        if (!locals.TryGetValue(token.Text, out local))
                            Fail("DS2007", token, "Unknown local; bindings are visible only after their initializer and within their function.");
                        if (GenericTypes.TryParse(local.Type, out var localShape) && IsBuilder(localShape))
                            Fail("DS2026", token, "A mutable builder can only be used as a local alias or as the receiver of a builder operation.");
                        return (new BoundReference(token.Text), local.Type);
                    case CallSyntax call:
                        if (!locals.ContainsKey(call.Name.Text) && actorMethodsByBody.TryGetValue(function.Name.Text, out var actorCallContext) &&
                            actorCallContext.Methods.TryGetValue(call.Name.Text, out var actorMethod))
                        {
                            if (call.Name.Text == "Handle") Fail("DS2042", call.Name, "Actor Handle cannot be called directly.");
                            if (call.TypeArguments.Count != 0 || call.Arguments.Count != actorMethod.Method.Parameters.Count)
                                Fail("DS2042", call.Name, "Actor helpers must be fully applied with the declared arguments.");
                            if (actorMethod.Method.IsAsync && actorCallContext.Actor.Dependencies.Count > 0 &&
                                !ReferenceEquals(call, directlyAwaitedActorHelper))
                                Fail("DS2041", call.Name, "An async actor helper borrowing dependencies must be immediately awaited.");
                            var borrowedArguments = actorCallContext.Actor.Dependencies.Select(dependency => (ExpressionSyntax)new ValueSyntax(dependency.Name)).ToArray();
                            foreach (var borrowedArgument in borrowedArguments) actorBorrowArguments.Add(borrowedArgument);
                            var helperCall = call with
                            {
                                Name = call.Name with { Text = actorMethod.Id },
                                Arguments = borrowedArguments.Concat(call.Arguments).ToArray()
                            };
                            try { return BindExpression(helperCall, expectedResult, pipeInputType, pipeOutputType, pipeNullInput, groupedArgumentTypes, groupedExpectedResult, isPipeTarget); }
                            finally { foreach (var borrowedArgument in borrowedArguments) actorBorrowArguments.Remove(borrowedArgument); }
                        }
                        if (resultHelpers.ContainsKey(function.Name.Text) && !locals.ContainsKey(call.Name.Text) && !inaccessibleCaptures.Contains(call.Name.Text) &&
                            preludeNames.TryGetValue(call.Name.Text, out var preludeCallName) && call.Name.Text != preludeCallName)
                            call = call with { Name = call.Name with { Text = preludeCallName } };
                        if (!locals.ContainsKey(call.Name.Text) && inaccessibleCaptures.Contains(call.Name.Text))
                            Fail("DS2026", call.Name, "Lambda capture is not supported.");
                        if (locals.ContainsKey(call.Name.Text))
                        {
                            if (call.TypeArguments.Count > 0) Fail("DS2026", call.Name, "Function values do not accept explicit type arguments.");
                            return BindInvocation(new ValueSyntax(call.Name), call.OpenParen, call.Arguments, expectedResult);
                        }
                        if (defaultReceivers.TryGetValue(function.Name.Text, out var callDefault) &&
                            interfaceMethods[callDefault.Interface].ContainsKey(call.Name.Text))
                        {
                            var implicitMember = new MemberCallSyntax(new ValueSyntax(callDefault.Receiver), call.Name, call.OpenParen, call.Arguments)
                                { TypeArguments = call.TypeArguments, ExtensionCandidates = Array.Empty<string>() };
                            if (ownedCalls.Contains(call)) ownedMemberCalls.Add(implicitMember);
                            if (awaitedOwnedCalls.Contains(call)) awaitedOwnedMembers.Add(implicitMember);
                            return BindExpression(implicitMember, expectedResult);
                        }
                        if (call.Name.Text is "Invoke" or "Dispatch" && !signatures.ContainsKey(call.Name.Text) &&
                            !records.ContainsKey(call.Name.Text) && !unions.ContainsKey(call.Name.Text) &&
                            !interfaces.ContainsKey(call.Name.Text) && !services.ContainsKey(call.Name.Text) &&
                            !actors.ContainsKey(call.Name.Text) && !cases.ContainsKey(call.Name.Text) &&
                            !functionTypeScope.ContainsKey(call.Name.Text))
                        {
                            var isDispatch = call.Name.Text == "Dispatch";
                            var isPublication = isDispatch && call.TypeArguments.Count == 0;
                            var operation = isDispatch ? "Dispatch" : "Invoke";
                            var diagnostic = isDispatch ? "DS2048" : "DS2047";
                            // This deliberately narrow actor seam carries no live
                            // dependency or local resource across the invocation.
                            // The general resource boundary remains with #45.
                            bool ResourceFreeActorHandle()
                            {
                                if (isDispatch || !actorMethodsByBody.TryGetValue(function.Name.Text, out var context) ||
                                    context.Actor.Dependencies.Count != 0 ||
                                    !context.Methods.TryGetValue("Handle", out var handle) || handle.Id != function.Name.Text ||
                                    handle.Method.Body.Bindings.Count != 0 || handle.Method.Body.Statements.Count != 0 ||
                                    call.Arguments.Count != 1 || call.Arguments[0] is not ValueSyntax input ||
                                    input.Value.Text != handle.Method.Parameters[0].Name.Text) return false;
                                ExpressionSyntax result = handle.Method.Body.Result;
                                while (result is GroupSyntax group) result = group.Inner;
                                if (result is not AwaitSyntax awaited) return false;
                                ExpressionSyntax operand = awaited.Operand;
                                while (operand is GroupSyntax operandGroup) operand = operandGroup.Inner;
                                return ReferenceEquals(operand, call);
                            }
                            if (!ReferenceEquals(call, directlyAwaitedInvoke) || !function.IsAsync || lambdaDepth != 0 ||
                                (!sourceHttpFunctions.Contains(function.Name.Text) && !ResourceFreeActorHandle()))
                                Fail(diagnostic, call.Name, $"Actor {operation} requires a supported directly awaited scoped entry.");
                            if (isDispatch && (function.ReturnType.Text != (isPublication ? publicationReceiptSymbol : invocationReceiptSymbol) ||
                                !ReferenceEquals(call, DispatchResultSite(function))))
                                Fail(diagnostic, call.Name, "Dispatch is supported only as the directly awaited inferred entry result.");
                            if (isPublication)
                            {
                                if (call.Arguments.Count != 1)
                                    Fail(diagnostic, call.Name, "Publication requires exactly one message.");
                                var published = BindExpression(call.Arguments[0]);
                                if (!GenericTypes.TryParse(published.Type, out var messageShape) || !SupportedType(published.Type) ||
                                    messageShape.IsNullable || (messageShape.Name != "Unit" &&
                                    (!records.ContainsKey(messageShape.Name) && !unions.ContainsKey(messageShape.Name))) ||
                                    !TriggerData(messageShape))
                                    Fail(diagnostic, call.Arguments[0].Token, "Publication requires Unit or a supported declared record or union message.");
                                messageContracts.TryAdd(published.Type, new BoundMessageContract(published.Type, MessageContractKey(messageShape)));
                                requiresDispatcher = true;
                                taskUsed = true;
                                return (new BoundPublication(published.Expression, published.Type), taskSymbol + "<" + publicationReceiptSymbol + ">");
                            }
                            if (call.TypeArguments.Count != 1 || call.Arguments.Count != 1)
                                Fail(diagnostic, call.Name, $"Actor {operation} requires one explicit target and one message.");
                            var actorIdentity = ScopedType(call.TypeArguments[0], functionTypeScope).Text;
                            if (!actors.TryGetValue(actorIdentity, out var targetActor))
                                Fail(diagnostic, call.TypeArguments[0], $"Actor {operation} requires a visible actor target.");
                            var targetHandle = targetActor!.Methods.Single(method => method.Name.Text == "Handle");
                            var inputType = targetHandle.Parameters[0].Type.Text;
                            var actorResultType = targetHandle.ReturnType.Text;
                            var message = BindExpression(call.Arguments[0], inputType);
                            if (message.Type != inputType && message.Type != "<never>")
                                Fail(diagnostic, call.Arguments[0].Token, $"Actor {operation} requires message type {ActorDisplayType(inputType)}.");
                            var routeKey = syntax.TypeDisplayNames.TryGetValue(targetActor.Name.Text, out var actorDisplay)
                                ? actorDisplay : syntax.Name.Text + "." + targetActor.Name.Text;
                            actorRoutes.TryAdd(actorIdentity, new BoundActorRoute(actorIdentity, routeKey, inputType, actorResultType));
                            requiresDispatcher = true;
                            taskUsed = true;
                            return isDispatch
                                ? (new BoundActorDispatch(actorIdentity, message.Expression, inputType, actorResultType),
                                    message.Type == "<never>" ? "<never>" : taskSymbol + "<" + invocationReceiptSymbol + ">")
                                : (new BoundActorInvocation(actorIdentity, message.Expression, inputType, actorResultType),
                                    message.Type == "<never>" ? "<never>" : taskSymbol + "<" + actorResultType + ">");
                        }
                        if (services.TryGetValue(call.Name.Text, out var serviceConstructor))
                        {
                            if (resourceNames.Contains(call.Name.Text) && !ownedCalls.Contains(call))
                                Fail("DS2041", call.Name, "A resource must transfer directly to a using scope or owned factory result.");
                            // A checked owned transfer already identifies a
                            // resource constructor, including when a union
                            // case has the same source name.
                            if (cases.ContainsKey(call.Name.Text) && !resourceNames.Contains(call.Name.Text))
                                Fail("DS2018", call.Name, "Ambiguous service constructor and union case.");
                            if (functionTypeScope.ContainsKey(call.Name.Text)) Fail("DS2026", call.Name, "A type parameter is not a service constructor.");
                            if (call.TypeArguments.Count != 0) Fail("DS2023", call.Name, "Service constructors do not accept type arguments.");
                            if (call.Arguments.Count != serviceConstructor.Dependencies.Count) Fail("DS2012", call.Name, "Incorrect number of service constructor arguments.");
                            var values = BindGenericArguments(Array.Empty<Token>(), Array.Empty<Token>(),
                                serviceConstructor.Dependencies.Select(parameter => parameter.Type).ToArray(), call.Arguments, call.Name);
                            return (new BoundServiceConstruction(serviceConstructor.Name.Text, values.Values), values.Never ? "<never>" : serviceConstructor.Name.Text);
                        }
                        if (cases.ContainsKey(call.Name.Text))
                        {
                            var selected = BareCase(call.Name);
                            return BindCase(selected.Union, selected.Case, call.Name, call.Arguments,
                                call.TypeArguments.Select(type => ScopedType(type, functionTypeScope)).ToArray(), expectedResult, true,
                                ownedResultCases.Contains(call));
                        }
                        if (TryResultOverloadFamily(call.Name.Text, out var callFamily) && !resolvingResultOverloads.Contains(call))
                            return BindResultOverload(call, call.Name, callFamily, expectedResult, pipeInputType, pipeOutputType,
                                pipeNullInput, groupedArgumentTypes, groupedExpectedResult, isPipeTarget);
                        if (!signatures.TryGetValue(call.Name.Text, out var target)) Fail("DS2009", call.Name, "Unknown function.");
                        if (ownedFactories.Contains(call.Name.Text))
                        {
                            if (!ownedCalls.Contains(call) || call.TypeArguments.Count != 0 || call.Arguments.Count != target!.Parameters.Count)
                                Fail("DS2041", call.Name, "An owned factory must be fully applied and transferred directly.");
                            var transferred = BindGenericArguments(Array.Empty<Token>(), Array.Empty<Token>(),
                                target!.Parameters.Select(parameter => parameter.Type).ToArray(), call.Arguments, call.Name);
                            return (new BoundCall(call.Name.Text, transferred.Values) { Location = call.Name },
                                transferred.Never ? "<never>" : CallableResult(target));
                        }
                        if (resultHelpers.ContainsKey(call.Name.Text)) { usedResultHelpers.Add(call.Name.Text); resultUsed = true; }
                        // A dead call cannot supply return evidence, and its
                        // callee may not yet have an inferred signature. The
                        // final pass will bind and validate the entire call.
                        if (inferencePass && !inferenceReachable && target!.InferReturnType)
                            return (new BoundUnit(), "<never>");
                        if (call.Arguments.Count > target!.Parameters.Count) Fail("DS2012", call.Name, "Incorrect number of arguments.");
                        var isPartialCall = call.Arguments.Count < target.Parameters.Count;
                        var callableResult = CallableResult(target);
                        var callPattern = isPartialCall ? TypeShape.FunctionName + "<" + string.Join(",", target.Parameters.Skip(call.Arguments.Count).Select(p => p.Type.Text).Append(callableResult)) + ">" : callableResult;
                        var resolvedCall = BindGenericArguments(target.TypeParameters,
                            call.TypeArguments.Select(type => ScopedType(type, functionTypeScope)).ToArray(),
                            target.Parameters.Take(call.Arguments.Count).Select(parameter => parameter.Type).ToArray(), call.Arguments, call.Name,
                            callPattern, expectedResult, isPartialCall ? target.Parameters[call.Arguments.Count].Type.Text : null, pipeInputType);
                        if (inferencePass && resolvedCall.Never && resolvedCall.Substitutions.Count != target.TypeParameters.Count)
                            return (new BoundUnit(), "<never>");
                        if (resolvedCall.Never && target.InferReturnType)
                            return (new BoundCall(call.Name.Text, resolvedCall.Values)
                            {
                                TypeArguments = target.TypeParameters.Select(parameter => resolvedCall.Substitutions[parameter.Text]).ToArray(),
                                Location = call.Name
                            }, "<never>");
                        var callResultType = Substitute(callableResult, resolvedCall.Substitutions, target.ReturnType);
                        if (!SupportedType(callResultType, activeTypeParameters)) Fail("DS2026", call.Name, "Function values cannot be stored in data types.");
                        if (isPartialCall)
                        {
                            var arguments = target.TypeParameters.Select(p => resolvedCall.Substitutions[p.Text]).ToArray();
                            var formalTypes = target.Parameters.Select(p => Substitute(p.Type.Text, resolvedCall.Substitutions, p.Type)).ToArray();
                            if (formalTypes.Take(call.Arguments.Count).Any(CapabilityType) || resolvedCall.Types.Any(CapabilityType))
                                Fail("DS2026", call.Name, "A partial application cannot capture a service or interface reference.");
                            var fullType = TypeShape.FunctionName + "<" + string.Join(",", formalTypes.Append(callResultType)) + ">";
                            var partialType = TypeShape.FunctionName + "<" + string.Join(",", formalTypes.Skip(call.Arguments.Count).Append(callResultType)) + ">";
                            return (new BoundPartial(new BoundFunctionReference(call.Name.Text, fullType, arguments) { Location = call.Name }, fullType,
                                resolvedCall.Values, formalTypes.Take(call.Arguments.Count).ToArray(), formalTypes.Skip(call.Arguments.Count).ToArray(), callResultType, partialType),
                                resolvedCall.Never ? "<never>" : partialType);
                        }
                        return (new BoundCall(call.Name.Text, resolvedCall.Values)
                        {
                            TypeArguments = target.TypeParameters.Select(parameter => resolvedCall.Substitutions[parameter.Text]).ToArray(),
                            Location = call.Name
                        }, resolvedCall.Never ? "<never>" : callResultType);
                    case MemberCallSyntax member:
                    {
                        ExpressionSyntax actorReceiver = member.Receiver;
                        while (actorReceiver is GroupSyntax actorReceiverGroup) actorReceiver = actorReceiverGroup.Inner;
                        if (actorReceiver is ValueSyntax actorDependency && IsActorDependency(actorDependency.Value.Text))
                        {
                            if (member.Member.Text == "release") Fail("DS2041", member.Member, "Actor resource cleanup is owned by the host.");
                            if (!locals.TryGetValue(actorDependency.Value.Text, out var actorLocal) ||
                                !interfaceMethods.TryGetValue(actorLocal.Type, out var actorOperations) ||
                                !actorOperations.TryGetValue(member.Member.Text, out var actorOperation))
                                Fail("DS2041", member.Member, "Unknown actor resource operation.");
                            if (actorOperation!.IsAsync && !directResourceAwait)
                                Fail("DS2041", member.Member, "An async actor resource operation must be immediately awaited.");
                            if (!SupportedType(actorOperation.ReturnType.Text) || !DataType(actorOperation.ReturnType.Text))
                                Fail("DS2041", member.Member, "An actor resource operation must return immutable data.");
                            if (member.TypeArguments.Count != 0 || member.Arguments.Count != actorOperation.Parameters.Count)
                                Fail("DS2041", member.Member, "Incorrect actor resource operation arguments.");
                            var previousDirectAwait = directResourceAwait;
                            directResourceAwait = false;
                            (IReadOnlyList<BoundExpression> Values, IReadOnlyList<string> Types, Dictionary<string, string> Substitutions, bool Never) actorArguments;
                            try { actorArguments = BindGenericArguments(Array.Empty<Token>(), Array.Empty<Token>(), actorOperation.Parameters.Select(parameter => parameter.Type).ToArray(), member.Arguments, member.Member); }
                            finally { directResourceAwait = previousDirectAwait; }
                            var actorOperationType = actorOperation.IsAsync ? taskSymbol + "<" + actorOperation.ReturnType.Text + ">" : actorOperation.ReturnType.Text;
                            if (actorOperation.IsAsync) taskUsed = true;
                            return (new BoundServiceCall(new BoundReference(actorDependency.Value.Text), actorLocal.Type, member.Member.Text, actorArguments.Values, actorOperationType),
                                actorArguments.Never ? "<never>" : actorOperationType);
                        }
                        if (member.Receiver is ValueSyntax scopedReceiver && resourceHandles.Contains(scopedReceiver.Value.Text) &&
                            locals.TryGetValue(scopedReceiver.Value.Text, out var scopedLocal))
                        {
                            if (member.Member.Text == "release") Fail("DS2041", member.Member, "Cleanup is managed by the using scope.");
                            if (!serviceMethods[scopedLocal.Type].TryGetValue(member.Member.Text, out var operation))
                                Fail("DS2041", member.Member, "Unknown resource operation.");
                            if (member.TypeArguments.Count != 0 || member.Arguments.Count != operation!.Parameters.Count)
                                Fail("DS2041", member.Member, "Incorrect resource operation arguments.");
                            if (operation!.IsAsync && !directResourceAwait)
                                Fail("DS2041", member.Member, "An async resource operation must be immediately awaited.");
                            // Resource operations are dynamically dispatched like
                            // service methods. A collection-like name or callback
                            // argument cannot make that dispatch a static dependency
                            // for omitted-return inference.
                            if (inferencePass && inferenceReachable && inferredReturns is not null)
                                Fail("DS2027", member.Member, "An explicit return type is required for indirect resource calls.");
                            var previousDirectAwait = directResourceAwait;
                            directResourceAwait = false;
                            (IReadOnlyList<BoundExpression> Values, IReadOnlyList<string> Types, Dictionary<string, string> Substitutions, bool Never) args;
                            try { args = BindGenericArguments(Array.Empty<Token>(), Array.Empty<Token>(), operation.Parameters.Select(parameter => parameter.Type).ToArray(), member.Arguments, member.Member); }
                            finally { directResourceAwait = previousDirectAwait; }
                            var operationType = operation.IsAsync ? taskSymbol + "<" + operation.ReturnType.Text + ">" : operation.ReturnType.Text;
                            if (operation.IsAsync) taskUsed = true;
                            return (new BoundServiceCall(new BoundReference(scopedReceiver.Value.Text), scopedLocal.Type, member.Member.Text, args.Values, operationType),
                                args.Never ? "<never>" : operationType);
                        }
                        var builderReceiverSyntax = member.Receiver;
                        while (builderReceiverSyntax is GroupSyntax builderGroup) builderReceiverSyntax = builderGroup.Inner;
                        if (builderReceiverSyntax is ValueSyntax builderReceiver &&
                            locals.TryGetValue(builderReceiver.Value.Text, out var builderLocal) &&
                            GenericTypes.TryParse(builderLocal.Type, out var builderShape) && IsBuilder(builderShape))
                        {
                            var isListBuilder = builderShape.Name == mutableListSymbol;
                            var operation = member.Member.Text;
                            var arity = operation == "freeze" ? 0 : operation == (isListBuilder ? "add" : "set") ? (isListBuilder ? 1 : 2) : -1;
                            if (arity < 0) Fail("DS2018", member.Member, "Unknown mutable builder operation.");
                            if (member.TypeArguments.Count != 0) Fail("DS2023", member.Member, "Builder operations do not accept type arguments.");
                            if (member.Arguments.Count != arity) Fail("DS2012", member.Member, "Incorrect number of builder operation arguments.");
                            var arguments = new List<BoundExpression>();
                            var savedReachable = inferenceReachable;
                            var never = false;
                            try
                            {
                                for (var i = 0; i < arity; i++)
                                {
                                    inferenceReachable = savedReachable && !never;
                                    var formal = builderShape.Arguments[i].ToString();
                                    var value = BindExpression(member.Arguments[i], formal);
                                    Require(member.Arguments[i], value.Type, formal);
                                    arguments.Add(Coerce(value.Expression, formal, value.Type));
                                    never |= value.Type == "<never>";
                                }
                            }
                            finally { inferenceReachable = savedReachable; }
                            var immutableType = (isListBuilder ? listSymbol : mapSymbol) + "<" + string.Join(",", builderShape.Arguments) + ">";
                            var builderResultType = operation == "freeze" ? immutableType : "Unit";
                            return (new BoundBuilderOperation(new BoundReference(builderReceiver.Value.Text), builderLocal.Type, operation, arguments, builderResultType),
                                never ? "<never>" : builderResultType);
                        }
                        if (member.Receiver is ValueSyntax capturedMemberQualifier && !locals.ContainsKey(capturedMemberQualifier.Value.Text) && inaccessibleCaptures.Contains(capturedMemberQualifier.Value.Text))
                            Fail("DS2026", capturedMemberQualifier.Value, "Lambda capture is not supported.");
                        // Intrinsic factories are available only when the
                        // qualifier is not a lexical/user declaration.
                        if (member.Receiver is ValueSyntax factory && !locals.ContainsKey(factory.Value.Text) &&
                            !functionTypeScope.ContainsKey(factory.Value.Text) && !signatures.ContainsKey(factory.Value.Text) &&
                            !cases.ContainsKey(factory.Value.Text) && !records.ContainsKey(factory.Value.Text) &&
                            (factory.Value.Text == "Map" && !userMapType || factory.Value.Text == "Set" && !userSetType))
                        {
                            var isMap = factory.Value.Text == "Map";
                            var factoryName = member.Member.Text;
                            if (factoryName != "empty" && !(factoryName == "from" && !isMap))
                                Fail("DS2018", member.Member, "Unknown immutable collection factory.");
                            var isFrom = factoryName == "from";
                            var expectedCount = isFrom ? 1 : 0;
                            if (member.Arguments.Count != expectedCount)
                                Fail("DS2012", member.Member, "Incorrect number of collection factory arguments.");
                            var typeCount = isMap ? 2 : 1;
                            if (member.TypeArguments.Count != 0 && member.TypeArguments.Count != typeCount)
                                Fail("DS2023", member.Member, "Incorrect number of collection factory type arguments.");
                            var symbol = isMap ? mapSymbol : setSymbol;
                            var types = member.TypeArguments.Select(type => ScopedType(type, functionTypeScope).Text).ToArray();
                            if (types.Length == 0 && expectedResult is not null && GenericTypes.TryParse(expectedResult, out var expectedCollection) &&
                                expectedCollection.Name == symbol && expectedCollection.Arguments.Count == typeCount)
                                types = expectedCollection.Arguments.Select(argument => argument.ToString()).ToArray();
                            var arguments = new List<BoundExpression>();
                            var never = false;
                            if (isFrom)
                            {
                                var inputHint = types.Length == 1 ? listSymbol + "<" + types[0] + ">" : null;
                                var input = BindExpression(member.Arguments[0], inputHint);
                                var inputType = input.Type == "<never>" ? KnownType(input.Expression) : input.Type;
                                if (inputType is null || !GenericTypes.TryParse(inputType, out var sourceList) || sourceList.Name != listSymbol ||
                                    sourceList.IsNullable || sourceList.Arguments.Count != 1)
                                    Fail("DS2026", member.Arguments[0].Token, "Set.from requires a non-nullable List value.");
                                if (types.Length == 0) types = new[] { sourceList!.Arguments[0].ToString() };
                                var listType = listSymbol + "<" + types[0] + ">";
                                Require(member.Arguments[0], input.Type, listType);
                                arguments.Add(Coerce(input.Expression, listType, input.Type));
                                listUsed = true;
                                never = input.Type == "<never>";
                            }
                            if (types.Length != typeCount)
                                Fail("DS2023", member.Member, "Cannot infer all collection type arguments.");
                            var collectionType = symbol + "<" + string.Join(",", types) + ">";
                            if (!SupportedType(collectionType, activeTypeParameters) || !DataType(collectionType))
                                Fail("DS2026", member.Member, "Collection types require an eligible immutable key and supported data values.");
                            if (isMap) mapUsed = true; else setUsed = true;
                            return (new BoundMapSetFactory(collectionType, (isMap ? "map." : "set.") + factoryName, arguments),
                                never ? "<never>" : collectionType);
                        }
                        if (member.Receiver is ValueSyntax typeMemberQualifier && !locals.ContainsKey(typeMemberQualifier.Value.Text) &&
                            functionTypeScope.ContainsKey(typeMemberQualifier.Value.Text) && unions.ContainsKey(UnionQualifier(typeMemberQualifier.Value.Text)))
                            Fail("DS2018", typeMemberQualifier.Value, "An unconstrained type parameter cannot qualify a union case.");
                        if (member.Receiver is ValueSyntax memberQualifier && !locals.ContainsKey(memberQualifier.Value.Text) &&
                            unions.TryGetValue(UnionQualifier(memberQualifier.Value.Text), out var memberUnion))
                            return BindQualifiedCase(memberUnion, member.Member, member.Arguments,
                                member.TypeArguments.Select(type => ScopedType(type, functionTypeScope)).ToArray(), expectedResult, true,
                                ownedResultCases.Contains(member));
                        var listReceiver = BindExpression(member.Receiver);
                        var listReceiverType = listReceiver.Type == "<never>" ? KnownType(listReceiver.Expression) : listReceiver.Type;
                        SourceError? extensionSafetyError = null;
                        // Probe without contributing return exits to inference. The selected
                        // argument expressions are bound again for the actual call.
                        bool ExactArguments(IReadOnlyList<string> formals)
                        {
                            if (member.Arguments.Count != formals.Count) return false;
                            var savedReturns = inferredReturns;
                            if (savedReturns is not null) inferredReturns = new List<string>();
                            var savedReachability = inferenceReachable;
                            var savedHasReturn = hasReturn;
                            var savedTupleCount = tupleBindingCount;
                            try
                            {
                                for (var i = 0; i < formals.Count; i++)
                                {
                                    var value = BindExpression(member.Arguments[i], formals[i]);
                                    if (value.Type != formals[i] && value.Type != "<never>" &&
                                        !(value.Type == "<null>" && IsNullable(formals[i]))) return false;
                                    inferenceReachable &= value.Type != "<never>";
                                }
                                return true;
                            }
                            catch (SourceError error)
                            {
                                if (error.Diagnostic.Code == "DS2041" || error.Diagnostic.Code == "DS2026" &&
                                    (error.Diagnostic.Message.Contains("capture", StringComparison.OrdinalIgnoreCase) || error.Diagnostic.Message.Contains("builder", StringComparison.OrdinalIgnoreCase)))
                                    extensionSafetyError ??= error;
                                return false;
                            }
                            finally { inferredReturns = savedReturns; inferenceReachable = savedReachability; hasReturn = savedHasReturn; tupleBindingCount = savedTupleCount; }
                        }
                        bool InferredListMemberApplicable(string element, bool fold)
                        {
                            if (member.Arguments.Count != (fold ? 2 : 1)) return false;
                            var savedReturns = inferredReturns;
                            if (savedReturns is not null) inferredReturns = new List<string>();
                            var savedReachability = inferenceReachable;
                            var savedHasReturn = hasReturn;
                            var savedTupleCount = tupleBindingCount;
                            try
                            {
                                var marker = int.MaxValue;
                                while (activeTypeParameters.Contains("$" + marker.ToString(CultureInfo.InvariantCulture)) || arities.ContainsKey("$" + marker.ToString(CultureInfo.InvariantCulture))) marker--;
                                if (!fold)
                                {
                                    var mapCallback = BindExpression(member.Arguments[0], TypeShape.FunctionName + "<" + element + ",$" + marker.ToString(CultureInfo.InvariantCulture) + ">");
                                    var callbackType = mapCallback.Type == "<never>" ? KnownType(mapCallback.Expression) : mapCallback.Type;
                                    return callbackType is not null && GenericTypes.TryParse(callbackType, out var callbackShape) &&
                                        callbackShape.IsFunction && !callbackShape.IsNullable && callbackShape.Arguments.Count == 2 &&
                                        callbackShape.Arguments[0].ToString() == element &&
                                        SupportedType(callbackShape.Arguments[1].ToString(), activeTypeParameters) && DataType(callbackShape.Arguments[1].ToString());
                                }
                                var initial = BindExpression(member.Arguments[0]);
                                var accumulator = initial.Type == "<never>" ? KnownType(initial.Expression) : initial.Type;
                                if (accumulator is null or "<null>" or "<never>" || !SupportedType(accumulator, activeTypeParameters)) return false;
                                var callbackFormal = TypeShape.FunctionName + "<" + accumulator + "," + element + "," + accumulator + ">";
                                var callback = BindExpression(member.Arguments[1], callbackFormal);
                                return callback.Type == callbackFormal || callback.Type == "<never>";
                            }
                            catch (SourceError) { return false; }
                            finally { inferredReturns = savedReturns; inferenceReachable = savedReachability; hasReturn = savedHasReturn; tupleBindingCount = savedTupleCount; }
                        }
                        // A fold prefix leaves one callback argument. Infer its
                        // accumulator from an expected partial function or from
                        // the callback supplied by the left side of a pipe.
                        string? FoldPartialHint(string element)
                        {
                            string? FromCallback(TypeShape shape)
                            {
                                if (!shape.IsFunction || shape.IsNullable || shape.Arguments.Count != 3 ||
                                    shape.Arguments[1].ToString() != element ||
                                    shape.Arguments[0].ToString() != shape.Arguments[2].ToString()) return null;
                                var accumulator = shape.Arguments[0].ToString();
                                return SupportedType(accumulator, activeTypeParameters) ? accumulator : null;
                            }
                            if (expectedResult is not null && GenericTypes.TryParse(expectedResult, out var expected) &&
                                expected.IsFunction && !expected.IsNullable && expected.Arguments.Count == 2)
                            {
                                var accumulator = FromCallback(expected.Arguments[0]);
                                if (accumulator is not null && expected.Arguments[1].ToString() == accumulator) return accumulator;
                            }
                            if (pipeInputType is not null && GenericTypes.TryParse(pipeInputType, out var callback))
                            {
                                var accumulator = FromCallback(callback);
                                if (accumulator is not null) return accumulator;
                            }
                            return pipeOutputType is not null && SupportedType(pipeOutputType, activeTypeParameters)
                                ? pipeOutputType : null;
                        }
                        bool InferredFoldPartialApplicable(string element)
                        {
                            if (member.Arguments.Count != 1) return false;
                            var savedReturns = inferredReturns;
                            if (savedReturns is not null) inferredReturns = new List<string>();
                            var savedReachability = inferenceReachable;
                            var savedHasReturn = hasReturn;
                            var savedTupleCount = tupleBindingCount;
                            try
                            {
                                var hint = FoldPartialHint(element);
                                var initial = BindExpression(member.Arguments[0], hint);
                                var accumulator = initial.Type == "<never>" ? KnownType(initial.Expression) : initial.Type;
                                if (accumulator == "<null>") accumulator = hint is not null && IsNullable(hint) ? hint : null;
                                return accumulator is not (null or "<null>" or "<never>") && SupportedType(accumulator, activeTypeParameters);
                            }
                            catch (SourceError error)
                            {
                                if (error.Diagnostic.Code == "DS2041" || error.Diagnostic.Code == "DS2026" &&
                                    (error.Diagnostic.Message.Contains("capture", StringComparison.OrdinalIgnoreCase) || error.Diagnostic.Message.Contains("builder", StringComparison.OrdinalIgnoreCase)))
                                    extensionSafetyError ??= error;
                                return false;
                            }
                            finally { inferredReturns = savedReturns; inferenceReachable = savedReachability; hasReturn = savedHasReturn; tupleBindingCount = savedTupleCount; }
                        }
                        bool RealMemberApplicable()
                        {
                            if (listReceiverType is null || member.TypeArguments.Count != 0) return false;
                            if (interfaceMethods.TryGetValue(listReceiverType, out var methods) || serviceMethods.TryGetValue(listReceiverType, out methods))
                                return methods!.TryGetValue(member.Member.Text, out var method) && ExactArguments(method.Parameters.Select(p => p.Type.Text).ToArray());
                            if (!GenericTypes.TryParse(listReceiverType, out var shape) || shape.IsNullable) return false;
                            if (shape.Name == listSymbol && shape.Arguments.Count == 1)
                            {
                                var element = shape.Arguments[0].ToString();
                                return member.Member.Text switch
                                {
                                    "length" => ExactArguments(Array.Empty<string>()),
                                    "get" => ExactArguments(new[] { "int" }),
                                    "append" => ExactArguments(new[] { element }),
                                    "filter" => ExactArguments(new[] { TypeShape.FunctionName + "<" + element + ",bool>" }),
                                    "map" => InferredListMemberApplicable(element, false),
                                    "fold" => member.Arguments.Count == 1
                                        ? InferredFoldPartialApplicable(element)
                                        : InferredListMemberApplicable(element, true),
                                    _ => false
                                };
                            }
                            if (shape.Name == mapSymbol && shape.Arguments.Count == 2)
                            {
                                var key = shape.Arguments[0].ToString();
                                return member.Member.Text switch
                                {
                                    "count" => ExactArguments(Array.Empty<string>()),
                                    "contains" or "get" => ExactArguments(new[] { key }),
                                    "set" => member.Arguments.Count == 1
                                        ? ExactArguments(new[] { key })
                                        : ExactArguments(new[] { key, shape.Arguments[1].ToString() }),
                                    _ => false
                                };
                            }
                            if (shape.Name == setSymbol && shape.Arguments.Count == 1)
                                return member.Member.Text switch
                                {
                                    "count" => ExactArguments(Array.Empty<string>()),
                                    "contains" or "add" => ExactArguments(new[] { shape.Arguments[0].ToString() }),
                                    _ => false
                                };
                            return false;
                        }
                        // Empty generic List methods need an explicit type or a
                        // structurally compatible callable/pipe context. Do not
                        // invent a type for an unconstrained map()/fold().
                        (string[] Parameters, string Result, string Type, string TypeArgument)? GenericEmptyListPartial()
                        {
                            if (listReceiverType is null || member.Arguments.Count != 0 ||
                                member.Member.Text is not ("map" or "fold") || member.TypeArguments.Count > 1 ||
                                !GenericTypes.TryParse(listReceiverType, out var list) || list.IsNullable ||
                                list.Name != listSymbol || list.Arguments.Count != 1) return null;
                            var element = list.Arguments[0].ToString();
                            var isMap = member.Member.Text == "map";
                            string? inferred = member.TypeArguments.Count == 1
                                ? ScopedType(member.TypeArguments[0], functionTypeScope).Text : null;
                            string? FromCallback(TypeShape callback, bool fold)
                            {
                                if (!callback.IsFunction || callback.IsNullable || callback.Arguments.Count != (fold ? 3 : 2)) return null;
                                if (fold)
                                    return callback.Arguments[1].ToString() == element && callback.Arguments[0].ToString() == callback.Arguments[2].ToString()
                                        ? callback.Arguments[0].ToString() : null;
                                return callback.Arguments[0].ToString() == element ? callback.Arguments[1].ToString() : null;
                            }
                            string? FromExpected(string? expected, bool remainingAfterPipe)
                            {
                                if (expected is null || !GenericTypes.TryParse(expected, out var shape) || shape.IsNullable) return null;
                                if (isMap && remainingAfterPipe)
                                    return shape.Name == listSymbol && shape.Arguments.Count == 1 ? shape.Arguments[0].ToString() : null;
                                if (!shape.IsFunction || shape.Arguments.Count != (isMap || remainingAfterPipe ? 2 : 3)) return null;
                                if (isMap)
                                {
                                    var argument = FromCallback(shape.Arguments[0], false);
                                    var result = shape.Arguments[1];
                                    return argument is not null && result.Name == listSymbol && !result.IsNullable &&
                                        result.Arguments.Count == 1 && result.Arguments[0].ToString() == argument ? argument : null;
                                }
                                var initial = remainingAfterPipe ? null : shape.Arguments[0].ToString();
                                var accumulator = FromCallback(shape.Arguments[remainingAfterPipe ? 0 : 1], true);
                                return accumulator is not null && shape.Arguments[^1].ToString() == accumulator &&
                                    (initial is null || initial == accumulator) ? accumulator : null;
                            }
                            var expectedHint = FromExpected(expectedResult, false) ?? FromExpected(pipeOutputType, true);
                            if (inferred is null) inferred = expectedHint;
                            if (inferred is null && pipeInputType is not null && GenericTypes.TryParse(pipeInputType, out var input))
                                inferred = isMap ? FromCallback(input, false) : pipeInputType;
                            if (inferred is null) return null;
                            var callbackType = isMap ? TypeShape.FunctionName + "<" + element + "," + inferred + ">" :
                                TypeShape.FunctionName + "<" + inferred + "," + element + "," + inferred + ">";
                            var parameters = isMap ? new[] { callbackType } : new[] { inferred, callbackType };
                            var result = isMap ? listSymbol + "<" + inferred + ">" : inferred;
                            return (parameters, result, TypeShape.FunctionName + "<" + string.Join(",", parameters.Append(result)) + ">", inferred);
                        }
                        var genericEmptyListPartial = GenericEmptyListPartial();
                        if (member.TypeArguments.Count > 0 && genericEmptyListPartial is { } explicitlyTypedPartial)
                        {
                            if (!SupportedType(explicitlyTypedPartial.TypeArgument, activeTypeParameters))
                                Fail("DS2023", member.TypeArguments[0], "Unsupported type argument.");
                            if (member.Member.Text == "map" && !DataType(explicitlyTypedPartial.TypeArgument))
                                Fail("DS2026", member.Member, "List.map must produce immutable data values.");
                            return (new BoundCollectionMethodPartial(listReceiver.Expression, listReceiverType!, member.Member.Text,
                                explicitlyTypedPartial.Parameters, explicitlyTypedPartial.Result, explicitlyTypedPartial.Type)
                                { TypeArguments = new[] { explicitlyTypedPartial.TypeArgument } },
                                listReceiver.Type == "<never>" ? "<never>" : explicitlyTypedPartial.Type);
                        }
                        if (listReceiverType is not null && member.TypeArguments.Count == 0)
                        {
                            // Monomorphic collection methods with required parameters
                            // can be captured with an empty positional prefix. Keep
                            // zero-arity members and polymorphic map/fold out of this
                            // initial slice.
                            (string[] Parameters, string Result, string Type)? EmptyCollectionPartial()
                            {
                                if (member.Arguments.Count != 0 || !GenericTypes.TryParse(listReceiverType, out var shape) || shape.IsNullable)
                                    return null;
                                string[]? parameters = null;
                                string? result = null;
                                if (shape.Name == listSymbol && shape.Arguments.Count == 1)
                                {
                                    var element = shape.Arguments[0].ToString();
                                    switch (member.Member.Text)
                                    {
                                        case "append": parameters = new[] { element }; result = listReceiverType; break;
                                        case "get": parameters = new[] { "int" }; result = optionSymbol + "<" + element + ">"; break;
                                        case "filter": parameters = new[] { TypeShape.FunctionName + "<" + element + ",bool>" }; result = listReceiverType; break;
                                    }
                                }
                                else if (shape.Name == mapSymbol && shape.Arguments.Count == 2)
                                {
                                    var key = shape.Arguments[0].ToString();
                                    var value = shape.Arguments[1].ToString();
                                    switch (member.Member.Text)
                                    {
                                        case "get": parameters = new[] { key }; result = optionSymbol + "<" + value + ">"; break;
                                        case "contains": parameters = new[] { key }; result = "bool"; break;
                                        case "set": parameters = new[] { key, value }; result = listReceiverType; break;
                                    }
                                }
                                else if (shape.Name == setSymbol && shape.Arguments.Count == 1)
                                {
                                    var element = shape.Arguments[0].ToString();
                                    switch (member.Member.Text)
                                    {
                                        case "contains": parameters = new[] { element }; result = "bool"; break;
                                        case "add": parameters = new[] { element }; result = listReceiverType; break;
                                    }
                                }
                                if (parameters is null || result is null) return null;
                                var function = TypeShape.FunctionName + "<" + string.Join(",", parameters.Append(result)) + ">";
                                return (parameters, result, function);
                            }
                            var ordinaryEmptyPartial = EmptyCollectionPartial();
                            (string[] Parameters, string Result, string Type)? emptyCollectionPartial = ordinaryEmptyPartial ??
                                (genericEmptyListPartial is { } inferredGeneric ? (inferredGeneric.Parameters, inferredGeneric.Result, inferredGeneric.Type) : null);
                            bool PipeCompatibleCallable(string callable)
                            {
                                if (pipeInputType is null && !pipeNullInput && groupedArgumentTypes is null ||
                                    !GenericTypes.TryParse(callable, out var shape) || !shape.IsFunction || shape.IsNullable || shape.Arguments.Count < 2 ||
                                    (pipeInputType is not null || pipeNullInput) && !Convertible(pipeInputType ?? "<null>", shape.Arguments[0].ToString())) return false;
                                if (groupedArgumentTypes is not null)
                                {
                                    if (groupedArgumentTypes.Count >= shape.Arguments.Count) return false;
                                    for (var i = 0; i < groupedArgumentTypes.Count; i++)
                                        if (groupedArgumentTypes[i] is { } supplied && !Convertible(supplied, shape.Arguments[i].ToString())) return false;
                                    var groupedOutput = groupedArgumentTypes.Count == shape.Arguments.Count - 1
                                        ? shape.Arguments[^1].ToString()
                                        : TypeShape.FunctionName + "<" + string.Join(",", shape.Arguments.Skip(groupedArgumentTypes.Count)) + ">";
                                    return groupedExpectedResult is null || !SupportedType(groupedExpectedResult, activeTypeParameters) ||
                                        Convertible(groupedOutput, groupedExpectedResult);
                                }
                                var output = shape.Arguments.Count == 2 ? shape.Arguments[1].ToString() :
                                    TypeShape.FunctionName + "<" + string.Join(",", shape.Arguments.Skip(1)) + ">";
                                return pipeOutputType is null || !SupportedType(pipeOutputType, activeTypeParameters) || Convertible(output, pipeOutputType);
                            }
                            string ExtensionPartialType(FunctionSyntax extension) => TypeShape.FunctionName + "<" + string.Join(",",
                                extension.Parameters.Skip(member.Arguments.Count + 1).Select(p => p.Type.Text).Append(CallableResult(extension))) + ">";
                            var availableExtensions = extensionDeclarations.Where(extension => extension.Method.Name.Text == member.Member.Text &&
                                (member.ExtensionCandidates is null || member.ExtensionCandidates.Contains(extension.Id)) &&
                                signatures[extension.Id].Parameters[0].Type.Text == listReceiverType &&
                                signatures[extension.Id].Parameters.Count == member.Arguments.Count + 1).ToArray();
                            if (availableExtensions.Length > 0 && !RealMemberApplicable())
                            {
                                var applicable = availableExtensions.Where(extension => ExactArguments(signatures[extension.Id].Parameters.Skip(1).Select(p => p.Type.Text).ToArray())).ToArray();
                                // Previously accepted saturated extensions keep
                                // winning unless a callable context uniquely fits
                                // the new intrinsic partial and not their result.
                                if (emptyCollectionPartial is { } intrinsic && expectedResult == intrinsic.Type)
                                    applicable = applicable.Where(extension => CallableResult(signatures[extension.Id]) == expectedResult).ToArray();
                                if (emptyCollectionPartial is { } pipedIntrinsic && PipeCompatibleCallable(pipedIntrinsic.Type))
                                    applicable = applicable.Where(extension => PipeCompatibleCallable(CallableResult(signatures[extension.Id]))).ToArray();
                                // A contextual callable can make a previously
                                // invalid exact-arity result yield to a longer
                                // overload. Preserve all already valid full calls.
                                if (DataType(listReceiverType) && expectedResult is not null &&
                                    GenericTypes.TryParse(expectedResult, out var expectedCallable) && expectedCallable.IsFunction && !expectedCallable.IsNullable &&
                                    extensionDeclarations.Any(extension => extension.Method.Name.Text == member.Member.Text &&
                                        (member.ExtensionCandidates is null || member.ExtensionCandidates.Contains(extension.Id)) &&
                                        signatures[extension.Id].Parameters[0].Type.Text == listReceiverType &&
                                        signatures[extension.Id].Parameters.Count > member.Arguments.Count + 1 &&
                                        ExtensionPartialType(signatures[extension.Id]) == expectedResult &&
                                        ExactArguments(signatures[extension.Id].Parameters.Skip(1).Take(member.Arguments.Count).Select(p => p.Type.Text).ToArray())))
                                    applicable = applicable.Where(extension => CallableResult(signatures[extension.Id]) == expectedResult).ToArray();
                                if (DataType(listReceiverType) && (pipeInputType is not null || pipeNullInput || groupedArgumentTypes is not null))
                                {
                                    if (extensionDeclarations.Any(extension => extension.Method.Name.Text == member.Member.Text &&
                                        (member.ExtensionCandidates is null || member.ExtensionCandidates.Contains(extension.Id)) &&
                                        signatures[extension.Id].Parameters[0].Type.Text == listReceiverType &&
                                        signatures[extension.Id].Parameters.Count > member.Arguments.Count + 1 &&
                                        PipeCompatibleCallable(ExtensionPartialType(signatures[extension.Id])) &&
                                        ExactArguments(signatures[extension.Id].Parameters.Skip(1).Take(member.Arguments.Count).Select(p => p.Type.Text).ToArray())))
                                        applicable = applicable.Where(extension => PipeCompatibleCallable(CallableResult(signatures[extension.Id]))).ToArray();
                                }
                                if (applicable.Length > 1)
                                    Fail("DS2018", member.Member, "Ambiguous extension call; candidates: " + string.Join(", ", applicable.Select(extension =>
                                        (extension.Method.OriginModule ?? syntax.Name.Text) + " (" + (extension.Method.Name.OriginPath ?? path) + ":" +
                                        extension.Method.Name.Span.Line.ToString(CultureInfo.InvariantCulture) + ":" + extension.Method.Name.Span.Column.ToString(CultureInfo.InvariantCulture) + ")")
                                        .OrderBy(name => name, StringComparer.Ordinal)) + ".");
                                if (applicable.Length == 1)
                                {
                                    var extension = applicable[0];
                                    var extensionTarget = signatures[extension.Id];
                                    var savedExtensionReachable = inferenceReachable;
                                    inferenceReachable = savedExtensionReachable && listReceiver.Type != "<never>";
                                    (IReadOnlyList<BoundExpression> Values, IReadOnlyList<string> Types, Dictionary<string, string> Substitutions, bool Never) extensionArguments;
                                    try { extensionArguments = BindGenericArguments(Array.Empty<Token>(), Array.Empty<Token>(),
                                        extensionTarget.Parameters.Skip(1).Select(parameter => parameter.Type).ToArray(), member.Arguments, member.Member); }
                                    finally { inferenceReachable = savedExtensionReachable; }
                                    if (extensionTarget.IsAsync) taskUsed = true;
                                    return (new BoundCall(extension.Id, new[] { listReceiver.Expression }.Concat(extensionArguments.Values).ToArray()) { Location = member.Member },
                                        listReceiver.Type == "<never>" || extensionArguments.Never ? "<never>" : CallableResult(extensionTarget));
                                }
                            }
                            // A real empty-prefix collection partial wins when
                            // it fits the callable context. If it cannot fit,
                            // allow a uniquely compatible extension partial in
                            // the same way as the existing contextual full-call
                            // fallback. Without such an extension retain the
                            // intrinsic so ordinary type errors stay unchanged.
                            var expectedEmptyCallable = expectedResult is not null &&
                                GenericTypes.TryParse(expectedResult, out var expectedEmptyShape) &&
                                expectedEmptyShape.IsFunction && !expectedEmptyShape.IsNullable;
                            var hasEmptyContext = expectedEmptyCallable || pipeInputType is not null || pipeNullInput || groupedArgumentTypes is not null;
                            bool FitsEmptyContext(string callable) =>
                                (!expectedEmptyCallable || callable == expectedResult) &&
                                (pipeInputType is null && !pipeNullInput && groupedArgumentTypes is null || PipeCompatibleCallable(callable));
                            var useExtensionInstead = emptyCollectionPartial is { } contextualIntrinsic && member.Arguments.Count == 0 &&
                                hasEmptyContext && !FitsEmptyContext(contextualIntrinsic.Type) && DataType(listReceiverType) &&
                                extensionDeclarations.Any(extension => extension.Method.Name.Text == member.Member.Text &&
                                    (member.ExtensionCandidates is null || member.ExtensionCandidates.Contains(extension.Id)) &&
                                    signatures[extension.Id].Parameters[0].Type.Text == listReceiverType &&
                                    signatures[extension.Id].Parameters.Count > 1 && FitsEmptyContext(ExtensionPartialType(signatures[extension.Id])));
                            if (emptyCollectionPartial is { } emptyPartial && !useExtensionInstead)
                            {
                                if (member.Member.Text == "get") optionUsed = true;
                                if (genericEmptyListPartial is { } generic && !SupportedType(generic.TypeArgument, activeTypeParameters))
                                    Fail("DS2023", member.Member, "Unsupported type argument.");
                                if (genericEmptyListPartial is { } mappedGeneric && member.Member.Text == "map" && !DataType(mappedGeneric.TypeArgument))
                                    Fail("DS2026", member.Member, "List.map must produce immutable data values.");
                                var partial = new BoundCollectionMethodPartial(listReceiver.Expression, listReceiverType, member.Member.Text,
                                    emptyPartial.Parameters, emptyPartial.Result, emptyPartial.Type);
                                if (genericEmptyListPartial is { } genericType) partial = partial with { TypeArguments = new[] { genericType.TypeArgument } };
                                return (partial,
                                    listReceiver.Type == "<never>" ? "<never>" : emptyPartial.Type);
                            }
                            // Fully supplied extension calls above retain their
                            // existing precedence. Keep only extensions with
                            // remaining parameters and resolve their callable
                            // type before creating a closure. An empty prefix
                            // needs callable, pipe or grouped-call context.
                            var availablePartials = extensionDeclarations.Where(extension => extension.Method.Name.Text == member.Member.Text &&
                                (member.ExtensionCandidates is null || member.ExtensionCandidates.Contains(extension.Id)) &&
                                signatures[extension.Id].Parameters[0].Type.Text == listReceiverType &&
                                signatures[extension.Id].Parameters.Count > member.Arguments.Count + 1).ToArray();
                            var contextualEmpty = member.Arguments.Count == 0 &&
                                (expectedResult is not null && GenericTypes.TryParse(expectedResult, out var expectedEmpty) && expectedEmpty.IsFunction && !expectedEmpty.IsNullable ||
                                 pipeInputType is not null || pipeNullInput || groupedArgumentTypes is not null);
                            if ((member.Arguments.Count > 0 || contextualEmpty) && availablePartials.Length > 0 && DataType(listReceiverType) && !RealMemberApplicable())
                            {
                                bool RealPartialApplicable()
                                {
                                    if (interfaceMethods.TryGetValue(listReceiverType, out var partialMethods) ||
                                        serviceMethods.TryGetValue(listReceiverType, out partialMethods))
                                        return partialMethods!.TryGetValue(member.Member.Text, out var partialMethod) &&
                                            partialMethod.Parameters.Count > member.Arguments.Count &&
                                            ExactArguments(partialMethod.Parameters.Take(member.Arguments.Count).Select(parameter => parameter.Type.Text).ToArray());
                                    if (!GenericTypes.TryParse(listReceiverType, out var partialShape) || partialShape.IsNullable) return false;
                                    if (partialShape.Name == mapSymbol && partialShape.Arguments.Count == 2 && member.Member.Text == "set" && member.Arguments.Count == 1)
                                        return ExactArguments(new[] { partialShape.Arguments[0].ToString() });
                                    if (partialShape.Name == listSymbol && partialShape.Arguments.Count == 1 && member.Member.Text == "fold" && member.Arguments.Count == 1)
                                    {
                                        // The accumulator is generic. Any supported
                                        // non-null first argument supplies its type.
                                        var savedReturns = inferredReturns;
                                        if (savedReturns is not null) inferredReturns = new List<string>();
                                        var savedReachability = inferenceReachable;
                                        var savedHasReturn = hasReturn;
                                        var savedTupleCount = tupleBindingCount;
                                        try
                                        {
                                            var initial = BindExpression(member.Arguments[0]);
                                            var type = initial.Type == "<never>" ? KnownType(initial.Expression) : initial.Type;
                                            return type is not (null or "<null>" or "<never>") && SupportedType(type, activeTypeParameters);
                                        }
                                        catch (SourceError error)
                                        {
                                            if (error.Diagnostic.Code == "DS2041" || error.Diagnostic.Code == "DS2026" &&
                                                (error.Diagnostic.Message.Contains("capture", StringComparison.OrdinalIgnoreCase) || error.Diagnostic.Message.Contains("builder", StringComparison.OrdinalIgnoreCase)))
                                                extensionSafetyError ??= error;
                                            return false;
                                        }
                                        finally { inferredReturns = savedReturns; inferenceReachable = savedReachability; hasReturn = savedHasReturn; tupleBindingCount = savedTupleCount; }
                                    }
                                    return false;
                                }
                                if (!RealPartialApplicable())
                                {
                                    var partialCandidates = availablePartials.Where(extension =>
                                        ExactArguments(signatures[extension.Id].Parameters.Skip(1).Take(member.Arguments.Count).Select(p => p.Type.Text).ToArray())).ToArray();
                                    if (expectedResult is not null && GenericTypes.TryParse(expectedResult, out var expectedPartial) && expectedPartial.IsFunction && !expectedPartial.IsNullable)
                                        partialCandidates = partialCandidates.Where(extension => ExtensionPartialType(signatures[extension.Id]) == expectedResult).ToArray();
                                    if (pipeInputType is not null || pipeNullInput)
                                        partialCandidates = partialCandidates.Where(extension => Convertible(pipeInputType ?? "<null>",
                                            signatures[extension.Id].Parameters[member.Arguments.Count + 1].Type.Text)).ToArray();
                                    if (groupedArgumentTypes is not null)
                                        partialCandidates = partialCandidates.Where(extension => PipeCompatibleCallable(ExtensionPartialType(signatures[extension.Id]))).ToArray();
                                    if (pipeOutputType is not null && SupportedType(pipeOutputType, activeTypeParameters))
                                        partialCandidates = partialCandidates.Where(extension =>
                                        {
                                            var candidate = signatures[extension.Id];
                                            var remaining = candidate.Parameters.Skip(member.Arguments.Count + 1).Select(p => p.Type.Text).ToArray();
                                            var outputType = remaining.Length == 1 ? CallableResult(candidate) :
                                                TypeShape.FunctionName + "<" + string.Join(",", remaining.Skip(1).Append(CallableResult(candidate))) + ">";
                                            return Convertible(outputType, pipeOutputType);
                                        }).ToArray();
                                    if (partialCandidates.Length > 1)
                                        Fail("DS2018", member.Member, "Ambiguous extension call; candidates: " + string.Join(", ", partialCandidates.Select(extension =>
                                            (extension.Method.OriginModule ?? syntax.Name.Text) + " (" + (extension.Method.Name.OriginPath ?? path) + ":" +
                                            extension.Method.Name.Span.Line.ToString(CultureInfo.InvariantCulture) + ":" + extension.Method.Name.Span.Column.ToString(CultureInfo.InvariantCulture) + ")")
                                            .OrderBy(name => name, StringComparer.Ordinal)) + ".");
                                    if (partialCandidates.Length == 1)
                                    {
                                        var extension = partialCandidates[0];
                                        var partialTarget = signatures[extension.Id];
                                        var formalTypes = partialTarget.Parameters.Select(parameter => parameter.Type.Text).ToArray();
                                        var savedExtensionReachable = inferenceReachable;
                                        inferenceReachable = savedExtensionReachable && listReceiver.Type != "<never>";
                                        (IReadOnlyList<BoundExpression> Values, IReadOnlyList<string> Types, Dictionary<string, string> Substitutions, bool Never) arguments;
                                        try { arguments = BindGenericArguments(Array.Empty<Token>(), Array.Empty<Token>(),
                                            partialTarget.Parameters.Skip(1).Take(member.Arguments.Count).Select(parameter => parameter.Type).ToArray(), member.Arguments, member.Member); }
                                        finally { inferenceReachable = savedExtensionReachable; }
                                        var capturedTypes = formalTypes.Take(member.Arguments.Count + 1).ToArray();
                                        if (capturedTypes.Any(CapabilityType) || arguments.Types.Any(CapabilityType))
                                            Fail("DS2026", member.Member, "A partial application cannot capture a service or interface reference.");
                                        var partialResultType = CallableResult(partialTarget);
                                        var fullType = TypeShape.FunctionName + "<" + string.Join(",", formalTypes.Append(partialResultType)) + ">";
                                        var partialType = ExtensionPartialType(partialTarget);
                                        if (partialTarget.IsAsync) taskUsed = true;
                                        return (new BoundPartial(new BoundFunctionReference(extension.Id, fullType, Array.Empty<string>()) { Location = member.Member }, fullType,
                                            new[] { listReceiver.Expression }.Concat(arguments.Values).ToArray(), capturedTypes,
                                            formalTypes.Skip(member.Arguments.Count + 1).ToArray(), partialResultType, partialType),
                                            listReceiver.Type == "<never>" || arguments.Never ? "<never>" : partialType);
                                    }
                                }
                            }
                            if (extensionSafetyError is not null) throw extensionSafetyError;
                        }
                        if (listReceiverType is not null &&
                            (interfaceMethods.TryGetValue(listReceiverType, out var dispatchMethods) || serviceMethods.TryGetValue(listReceiverType, out dispatchMethods)))
                        {
                            if (privateResourceContracts.Contains(listReceiverType) && member.Member.Text == "release")
                                Fail("DS2041", member.Member, "Actor resource cleanup is owned by the host.");
                            // Virtual dispatch is an indirect dependency. Check
                            // this after binding the receiver, so a service method
                            // named `get` does not make unrelated List.get calls
                            // require a return annotation.
                            if (inferencePass && inferenceReachable && inferredReturns is not null)
                                Fail("DS2027", member.Member, "An explicit return type is required for indirect service calls.");
                            if (!dispatchMethods!.TryGetValue(member.Member.Text, out var dispatchMethod))
                                Fail("DS2040", member.Member, "Unknown service or interface method.");
                            if (OwnedTransfer(dispatchMethod!.ReturnType.Text) &&
                                (!ownedMemberCalls.Contains(member) || dispatchMethod.IsAsync != awaitedOwnedMembers.Contains(member)))
                                Fail("DS2041", member.Member, "An owned factory method must be fully applied and transferred directly; async factories must be directly awaited.");
                            if (member.TypeArguments.Count != 0) Fail("DS2023", member.Member, "Service methods do not accept type arguments.");
                            if (member.Arguments.Count != dispatchMethod!.Parameters.Count)
                                Fail("DS2012", member.Member, "Incorrect number of service method arguments.");
                            var savedDispatchReachable = inferenceReachable;
                            inferenceReachable = savedDispatchReachable && listReceiver.Type != "<never>";
                            (IReadOnlyList<BoundExpression> Values, IReadOnlyList<string> Types, Dictionary<string, string> Substitutions, bool Never) dispatchArguments;
                            try { dispatchArguments = BindGenericArguments(Array.Empty<Token>(), Array.Empty<Token>(),
                                dispatchMethod.Parameters.Select(parameter => parameter.Type).ToArray(), member.Arguments, member.Member); }
                            finally { inferenceReachable = savedDispatchReachable; }
                            var dispatchResult = dispatchMethod.IsAsync ? taskSymbol + "<" + dispatchMethod.ReturnType.Text + ">" : dispatchMethod.ReturnType.Text;
                            if (dispatchMethod.IsAsync) taskUsed = true;
                            return (new BoundServiceCall(listReceiver.Expression, listReceiverType, member.Member.Text, dispatchArguments.Values, dispatchResult),
                                listReceiver.Type == "<never>" || dispatchArguments.Never ? "<never>" : dispatchResult);
                        }
                        if (listReceiverType is not null && GenericTypes.TryParse(listReceiverType, out var collectionShape) &&
                            (collectionShape.Name == mapSymbol && collectionShape.Arguments.Count == 2 ||
                             collectionShape.Name == setSymbol && collectionShape.Arguments.Count == 1))
                        {
                            if (collectionShape.IsNullable) Fail("DS2018", member.Member, "Collection operations require a non-nullable receiver.");
                            var isMap = collectionShape.Name == mapSymbol;
                            var operation = member.Member.Text;
                            var arity = operation switch
                            {
                                "count" => 0,
                                "contains" => 1,
                                "get" when isMap => 1,
                                "set" when isMap => 2,
                                "add" when !isMap => 1,
                                _ => -1
                            };
                            if (arity < 0) Fail("DS2018", member.Member, "Unknown immutable collection operation.");
                            if (member.TypeArguments.Count != 0) Fail("DS2023", member.Member, "Collection operations do not accept type arguments.");
                            var partialSet = isMap && operation == "set" && member.Arguments.Count == 1;
                            if (member.Arguments.Count != arity && !partialSet) Fail("DS2012", member.Member, "Incorrect number of collection operation arguments.");
                            var keyType = collectionShape.Arguments[0].ToString();
                            var valueType = isMap ? collectionShape.Arguments[1].ToString() : null;
                            var arguments = new List<BoundExpression>();
                            var savedReachable = inferenceReachable;
                            var never = listReceiver.Type == "<never>";
                            try
                            {
                                for (var i = 0; i < member.Arguments.Count; i++)
                                {
                                    inferenceReachable = savedReachable && !never;
                                    var formal = i == 0 ? keyType : valueType!;
                                    var argument = BindExpression(member.Arguments[i], formal);
                                    Require(member.Arguments[i], argument.Type, formal);
                                    arguments.Add(Coerce(argument.Expression, formal, argument.Type));
                                    never |= argument.Type == "<never>";
                                }
                            }
                            finally { inferenceReachable = savedReachable; }
                            if (partialSet)
                            {
                                var partialType = TypeShape.FunctionName + "<" + valueType + "," + listReceiverType + ">";
                                return (new BoundMapSetPartial(listReceiver.Expression, listReceiverType, arguments[0], keyType, valueType!, partialType),
                                    never ? "<never>" : partialType);
                            }
                            var returnType = operation switch
                            {
                                "count" => "int",
                                "contains" => "bool",
                                "get" => optionSymbol + "<" + valueType + ">",
                                _ => listReceiverType
                            };
                            if (operation == "get") optionUsed = true;
                            return (new BoundMapSetOperation(listReceiver.Expression, listReceiverType, operation, arguments, returnType),
                                never ? "<never>" : returnType);
                        }
                        if (listReceiverType is not null && GenericTypes.TryParse(listReceiverType, out var listShape) &&
                            listShape.Name == listSymbol && listShape.Arguments.Count == 1)
                        {
                            if (listShape.IsNullable) Fail("DS2018", member.Member, "List operations require a non-nullable receiver.");
                            var listOperation = member.Member.Text;
                            var expectedCount = listOperation switch { "length" => 0, "append" or "get" or "map" or "filter" => 1, "fold" => 2, _ => -1 };
                            if (expectedCount < 0) Fail("DS2018", member.Member, "Unknown immutable List operation.");
                            var partialFold = listOperation == "fold" && member.Arguments.Count == 1;
                            if (member.Arguments.Count != expectedCount && !partialFold) Fail("DS2012", member.Member, "Incorrect number of List operation arguments.");
                            if (member.TypeArguments.Count > (listOperation is "map" or "fold" ? 1 : 0))
                                Fail("DS2023", member.Member, "Incorrect number of List operation type arguments.");
                            var elementType = listShape.Arguments[0].ToString();
                            var boundArguments = new List<BoundExpression>();
                            var savedListReachable = inferenceReachable;
                            var never = listReceiver.Type == "<never>";
                            string listResultType;
                            string? specified = member.TypeArguments.Count == 0 ? null : ScopedType(member.TypeArguments[0], functionTypeScope).Text;
                            if (specified is not null && !SupportedType(specified, activeTypeParameters))
                                Fail("DS2023", member.TypeArguments[0], "Unsupported type argument.");
                            (BoundExpression Expression, string Type) Argument(int index, string? hint)
                            {
                                inferenceReachable = savedListReachable && !never;
                                var argument = BindExpression(member.Arguments[index], hint);
                                never |= argument.Type == "<never>";
                                return argument;
                            }
                            string FunctionType(params string[] pieces) => TypeShape.FunctionName + "<" + string.Join(",", pieces) + ">";
                            string Marker()
                            {
                                var marker = int.MaxValue;
                                while (activeTypeParameters.Contains("$" + marker.ToString(CultureInfo.InvariantCulture)) || arities.ContainsKey("$" + marker.ToString(CultureInfo.InvariantCulture))) marker--;
                                return "$" + marker.ToString(CultureInfo.InvariantCulture);
                            }
                            try
                            {
                                if (listOperation == "length") listResultType = "int";
                                else if (listOperation is "append" or "get")
                                {
                                    var formal = listOperation == "append" ? elementType : "int";
                                    var argument = Argument(0, formal);
                                    Require(member.Arguments[0], argument.Type, formal);
                                    boundArguments.Add(Coerce(argument.Expression, formal, argument.Type));
                                    if (listOperation == "get") { optionUsed = true; listResultType = optionSymbol + "<" + elementType + ">"; }
                                    else listResultType = listReceiverType!;
                                }
                                else if (listOperation == "filter")
                                {
                                    var formal = FunctionType(elementType, "bool");
                                    var callback = Argument(0, formal);
                                    Require(member.Arguments[0], callback.Type, formal);
                                    boundArguments.Add(Coerce(callback.Expression, formal, callback.Type));
                                    listResultType = listReceiverType!;
                                }
                                else if (listOperation == "map")
                                {
                                    var resultHint = specified;
                                    if (resultHint is null && expectedResult is not null && GenericTypes.TryParse(expectedResult, out var expectedMap) &&
                                        expectedMap.Name == listSymbol && expectedMap.Arguments.Count == 1 && SupportedType(expectedMap.Arguments[0].ToString(), activeTypeParameters))
                                        resultHint = expectedMap.Arguments[0].ToString();
                                    var callback = Argument(0, FunctionType(elementType, resultHint ?? Marker()));
                                    var callbackType = callback.Type == "<never>" ? KnownType(callback.Expression) : callback.Type;
                                    TypeShape callbackShape = null!;
                                    if (callbackType is null || !GenericTypes.TryParse(callbackType, out callbackShape) || !callbackShape.IsFunction ||
                                        callbackShape.IsNullable || callbackShape.Arguments.Count != 2 || callbackShape.Arguments[0].ToString() != elementType)
                                        Fail("DS2026", member.Arguments[0].Token, "List.map requires a function accepting the element type.");
                                    var mappedType = callbackShape!.Arguments[1].ToString();
                                    if (resultHint is not null && mappedType != resultHint) Fail("DS2002", member.Arguments[0].Token, "List.map callback result type does not match its expected type.");
                                    if (!SupportedType(mappedType, activeTypeParameters) || !DataType(mappedType))
                                        Fail("DS2026", member.Arguments[0].Token, "List.map must produce immutable data values.");
                                    boundArguments.Add(callback.Expression);
                                    listResultType = listSymbol + "<" + mappedType + ">";
                                }
                                else // fold
                                {
                                    var accumulatorHint = specified ?? (partialFold ? FoldPartialHint(elementType) : expectedResult);
                                    var initial = Argument(0, accumulatorHint);
                                    var accumulator = specified ?? (initial.Type == "<never>" ? KnownType(initial.Expression) : initial.Type);
                                    if (partialFold && specified is null && accumulatorHint is not null && Convertible(initial.Type, accumulatorHint))
                                        accumulator = accumulatorHint;
                                    if (accumulator is null or "<null>" or "<never>" || !SupportedType(accumulator, activeTypeParameters))
                                        Fail("DS2023", member.Arguments[0].Token, "Cannot infer the List.fold accumulator type.");
                                    Require(member.Arguments[0], initial.Type, accumulator!);
                                    boundArguments.Add(Coerce(initial.Expression, accumulator!, initial.Type));
                                    var formal = FunctionType(accumulator!, elementType, accumulator!);
                                    if (partialFold)
                                    {
                                        if (CapabilityType(accumulator!) || CapabilityType(initial.Type))
                                            Fail("DS2026", member.Member, "A partial application cannot capture a service or interface reference.");
                                        var partialType = FunctionType(formal, accumulator!);
                                        return (new BoundListFoldPartial(listReceiver.Expression, listReceiverType!, boundArguments[0], accumulator!, elementType, partialType),
                                            never ? "<never>" : partialType);
                                    }
                                    var callback = Argument(1, formal);
                                    Require(member.Arguments[1], callback.Type, formal);
                                    boundArguments.Add(Coerce(callback.Expression, formal, callback.Type));
                                    listResultType = accumulator!;
                                }
                            }
                            finally { inferenceReachable = savedListReachable; }
                            return (new BoundListOperation(listReceiver.Expression, listReceiverType!, listOperation, boundArguments, listResultType), never ? "<never>" : listResultType);
                        }
                        Fail("DS2018", member.Member, "Only qualified union cases are callable through a member.");
                        throw new InvalidOperationException("Unreachable after source error.");
                    }
                    case UnarySyntax unary:
                        var operand = BindExpression(unary.Operand, unary.Operator.Text == "!" ? "bool" : expectedResult);
                        var expected = unary.Operator.Text == "!" ? "bool" : operand.Type == "<never>" ? "int" : operand.Type;
                        if (unary.Operator.Text != "!" && !IntegerType(expected) && expected is not ("decimal" or "float32" or "float"))
                            Fail("DS2002", unary.Operand.Token, "Unary arithmetic requires a numeric operand.");
                        Require(unary.Operand, operand.Type, expected);
                        return (new BoundUnary(unary.Operator.Text, Coerce(operand.Expression, expected, operand.Type)) { OperandType = expected }, operand.Type == "<never>" ? "<never>" : expected);
                    case BinarySyntax binary:
                        // A literal is contextual even on the left of a typed operand.
                        // Probe the other operand to obtain its type without recording
                        // return inference or changing runtime evaluation order.
                        bool NumericLiteral(ExpressionSyntax expression)
                        {
                            while (expression is GroupSyntax group) expression = group.Inner;
                            if (expression is UnarySyntax { Operator.Text: "-" } unaryLiteral) return NumericLiteral(unaryLiteral.Operand);
                            if (expression is BinarySyntax { Operator.Text: "+" or "-" or "*" or "/" or "%" } numericBinary)
                                return NumericLiteral(numericBinary.Left) && NumericLiteral(numericBinary.Right);
                            return expression is ValueSyntax value && value.Value.Text.Length > 0 && char.IsAsciiDigit(value.Value.Text[0]);
                        }
                        bool FixedLongLiteral(ExpressionSyntax expression)
                        {
                            while (expression is GroupSyntax group) expression = group.Inner;
                            if (expression is UnarySyntax { Operator.Text: "-" } unaryLiteral) return FixedLongLiteral(unaryLiteral.Operand);
                            if (expression is BinarySyntax { Operator.Text: "+" or "-" or "*" or "/" or "%" } numericBinary)
                                return FixedLongLiteral(numericBinary.Left) || FixedLongLiteral(numericBinary.Right);
                            return expression is ValueSyntax value && value.Value.Text.Length > 0 && char.IsAsciiDigit(value.Value.Text[0]) && value.Value.Text.EndsWith("L", StringComparison.Ordinal);
                        }
                        bool FreshUnionCase(ExpressionSyntax expression)
                        {
                            while (expression is GroupSyntax group) expression = group.Inner;
                            if (expression is CallSyntax call)
                                return !locals.ContainsKey(call.Name.Text) && cases.TryGetValue(call.Name.Text, out var candidates) &&
                                    candidates.Count == 1 && !signatures.ContainsKey(call.Name.Text) && !records.ContainsKey(call.Name.Text);
                            return expression is MemberCallSyntax { Receiver: ValueSyntax qualifier } member &&
                                !locals.ContainsKey(qualifier.Value.Text) && unions.ContainsKey(UnionQualifier(qualifier.Value.Text)) &&
                                unions[UnionQualifier(qualifier.Value.Text)].Cases.Any(item => item.Name.Text == member.Member.Text);
                        }
                        var op = binary.Operator.Text;
                        string? IntegerHint(string? type) => IntegerType(type) ? type :
                            type is not null && IsNullable(type) && IntegerType(type[..^1]) ? type[..^1] : null;
                        string? binaryHint = op is not ("==" or "!=" or "<" or "<=" or ">" or ">=" or "&&" or "||") ? IntegerHint(expectedResult) : null;
                        var numericLeftNeedsHint = NumericLiteral(binary.Left) && (!NumericLiteral(binary.Right) || FixedLongLiteral(binary.Right));
                        var caseLeftNeedsHint = op is "==" or "!=" && FreshUnionCase(binary.Left);
                        if (binaryHint is null && (numericLeftNeedsHint || caseLeftNeedsHint))
                        {
                            var savedReturns = inferredReturns;
                            if (savedReturns is not null) inferredReturns = new List<string>();
                            var savedReachability = inferenceReachable;
                            var savedReturn = hasReturn;
                            var savedTupleCount = tupleBindingCount;
                            try
                            {
                                var probe = BindExpression(binary.Right);
                                if (caseLeftNeedsHint && SupportedType(probe.Type, activeTypeParameters)) binaryHint = probe.Type;
                                else if (IntegerType(probe.Type)) binaryHint = probe.Type;
                                else if (op is "==" or "!=") binaryHint = IntegerHint(probe.Type);
                            }
                            catch (SourceError) { }
                            finally { inferredReturns = savedReturns; inferenceReachable = savedReachability; hasReturn = savedReturn; tupleBindingCount = savedTupleCount; }
                        }
                        var left = BindExpression(binary.Left, binaryHint);
                        var savedBinaryReachable = inferenceReachable;
                        var literalLeft = LiteralCondition(binary.Left);
                        inferenceReachable = savedBinaryReachable && left.Type != "<never>" &&
                            !(binary.Operator.Text == "&&" && literalLeft == false) &&
                            !(binary.Operator.Text == "||" && literalLeft == true);
                        var rightHint = IntegerType(left.Type) ? left.Type :
                            op is "==" or "!=" && SupportedType(left.Type, activeTypeParameters) ? left.Type : null;
                        var right = BindExpression(binary.Right, rightHint);
                        inferenceReachable = savedBinaryReachable;
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
                            if (operandType is not null && GenericTypes.TryParse(operandType, out var taskEquality) && taskEquality.Name == taskSymbol &&
                                !(taskEquality.IsNullable && (left.Type == "<null>" || right.Type == "<null>")))
                                Fail("DS2026", binary.Operator, "Task equality is not supported; nullable tasks may be compared with null.");
                            if (operandType is not null && GenericTypes.TryParse(operandType, out var capabilityEquality) && ContainsCapability(capabilityEquality))
                                Fail("DS2026", binary.Operator, "Service and interface reference equality is not supported.");
                            if (operandType is not null && IsNullable(operandType) && !SupportedType(operandType, activeTypeParameters))
                                Fail("DS2023", binary.Operator, "Nullable unconstrained type parameters are not supported.");
                            if (operandType == "Unit") Fail("DS2002", binary.Operator, "Unit equality is not supported.");
                            if (operandType is not null && activeTypeParameters.Contains(operandType))
                                Fail("DS2023", binary.Operator, "Equality on an unconstrained type parameter is not supported.");
                            return (new BoundBinary(Coerce(left.Expression, operandType!, left.Type), op, Coerce(right.Expression, operandType!, right.Type)) { OperandType = operandType! }, left.Type == "<never>" || right.Type == "<never>" ? "<never>" : "bool");
                        }
                        var integerOperand = left.Type == "<never>" ? right.Type == "<never>" ? "int" : right.Type : left.Type;
                        if (!IntegerType(integerOperand) && integerOperand is not ("decimal" or "float32" or "float")) Fail("DS2002", (left.Type == "<never>" ? binary.Right : binary.Left).Token, "Arithmetic and ordered comparisons require numeric operands.");
                        Require(binary.Left, left.Type, integerOperand); Require(binary.Right, right.Type, integerOperand);
                        return (new BoundBinary(Coerce(left.Expression, integerOperand, left.Type), op, Coerce(right.Expression, integerOperand, right.Type)) { OperandType = integerOperand }, left.Type == "<never>" || right.Type == "<never>" ? "<never>" : op is "<" or "<=" or ">" or ">=" ? "bool" : integerOperand);
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
                if (ServiceConversion(actual, expected)) return new BoundServiceConversion(expression, expected);
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
                var enclosingReachable = inferenceReachable;
                try
                {
                    var statements = block.Statements.Count > 0 ? block.Statements : block.Bindings.Select(b => (StatementSyntax)new LetStatementSyntax(b)).ToArray();
                    foreach (var statement in statements)
                    {
                        // Once this block itself cannot complete, remaining
                        // statements cannot contribute inference evidence.
                        // The final pass still binds and checks all of them.
                        if (inferencePass && !reachable) break;
                        inferenceReachable = enclosingReachable && reachable;
                        switch (statement)
                        {
                            case LetStatementSyntax declaration:
                                var binding = declaration.Binding;
                                if (locals.ContainsKey(binding.Name.Text) || inaccessibleCaptures.Contains(binding.Name.Text)) Fail("DS2006", binding.Name, "Duplicate or shadowed local binding.");
                                var annotation = binding.Annotation is null ? null : ScopedType(binding.Annotation, functionTypeScope);
                                bool ValidBuilderType(string type)
                                {
                                    if (!GenericTypes.TryParse(type, out var shape) || !IsBuilder(shape) || shape.IsNullable ||
                                        shape.Arguments.Count != (shape.Name == mutableListSymbol ? 1 : 2)) return false;
                                    var immutable = (shape.Name == mutableListSymbol ? listSymbol : mapSymbol) + "<" + string.Join(",", shape.Arguments) + ">";
                                    return SupportedType(immutable, activeTypeParameters) && DataType(immutable);
                                }
                                if (annotation is not null && !SupportedType(annotation.Text, activeTypeParameters) && !ValidBuilderType(annotation.Text))
                                    Fail("DS2008", annotation, "Unsupported local annotation.");
                                var initSyntax = binding.Expression ?? new ValueSyntax(binding.Initializer);
                                ExpressionSyntax builderInitSyntax = initSyntax;
                                while (builderInitSyntax is GroupSyntax builderGroup) builderInitSyntax = builderGroup.Inner;
                                (BoundExpression Expression, string Type) init;
                                if (builderInitSyntax is ValueSyntax alias && locals.TryGetValue(alias.Value.Text, out var aliasLocal) &&
                                    GenericTypes.TryParse(aliasLocal.Type, out var aliasShape) && IsBuilder(aliasShape))
                                    init = (new BoundReference(alias.Value.Text), aliasLocal.Type);
                                else if (builderInitSyntax is CallSyntax builderCall &&
                                    !locals.ContainsKey(builderCall.Name.Text) && !inaccessibleCaptures.Contains(builderCall.Name.Text) &&
                                    !functionTypeScope.ContainsKey(builderCall.Name.Text) && !signatures.ContainsKey(builderCall.Name.Text) &&
                                    !cases.ContainsKey(builderCall.Name.Text) && !records.ContainsKey(builderCall.Name.Text) && !unions.ContainsKey(builderCall.Name.Text) &&
                                    (builderCall.Name.Text == "MutableList" && !userMutableListType || builderCall.Name.Text == "MutableMap" && !userMutableMapType))
                                {
                                    var isListBuilder = builderCall.Name.Text == "MutableList";
                                    var count = isListBuilder ? 1 : 2;
                                    if (builderCall.Arguments.Count != 0) Fail("DS2012", builderCall.Name, "Builder constructors take no value arguments.");
                                    var typeArguments = builderCall.TypeArguments.Select(type => ScopedType(type, functionTypeScope).Text).ToArray();
                                    if (typeArguments.Length == 0 && annotation is not null && GenericTypes.TryParse(annotation.Text, out var expectedBuilder) &&
                                        expectedBuilder.Name == (isListBuilder ? mutableListSymbol : mutableMapSymbol))
                                        typeArguments = expectedBuilder.Arguments.Select(type => type.ToString()).ToArray();
                                    if (typeArguments.Length != count) Fail("DS2023", builderCall.Name, "Cannot infer builder type arguments.");
                                    var builderType = (isListBuilder ? mutableListSymbol : mutableMapSymbol) + "<" + string.Join(",", typeArguments) + ">";
                                    if (!ValidBuilderType(builderType)) Fail("DS2026", builderCall.Name, "Builders require supported immutable data and eligible map keys.");
                                    var immutableType = (isListBuilder ? listSymbol : mapSymbol) + "<" + string.Join(",", typeArguments) + ">";
                                    if (isListBuilder) { mutableListUsed = true; listUsed = true; }
                                    else { mutableMapUsed = true; mapUsed = true; }
                                    init = (new BoundBuilderFactory(builderType, immutableType), builderType);
                                }
                                else init = BindExpression(initSyntax, annotation?.Text);
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
                            case TupleLetStatementSyntax declaration:
                            {
                                var names = new HashSet<string>(StringComparer.Ordinal);
                                foreach (var name in declaration.Pattern.Names)
                                    if (!names.Add(name.Text) || locals.ContainsKey(name.Text) || inaccessibleCaptures.Contains(name.Text))
                                        Fail("DS2006", name, "Duplicate or shadowed local binding.");
                                var tupleAnnotation = declaration.Annotation is null ? null : ScopedType(declaration.Annotation, functionTypeScope);
                                if (tupleAnnotation is not null && !SupportedType(tupleAnnotation.Text, activeTypeParameters))
                                    Fail("DS2008", tupleAnnotation, "Unsupported local annotation.");
                                var tupleInit = BindExpression(declaration.Value, tupleAnnotation?.Text);
                                var tupleType = tupleAnnotation?.Text ?? (tupleInit.Type == "<never>" ? KnownType(tupleInit.Expression) ?? "Unit" : tupleInit.Type);
                                if (tupleAnnotation is not null) Require(declaration.Value, tupleInit.Type, tupleType);
                                if (!GenericTypes.TryParse(tupleType, out var tupleShape) || !tupleShape.IsTuple || tupleShape.IsNullable)
                                    Fail("DS2008", declaration.Pattern.Token, "Tuple destructuring requires a non-null tuple value.");
                                var hiddenName = "$tupleLocal" + (tupleBindingCount++).ToString(CultureInfo.InvariantCulture);
                                var temporary = new BoundLocal(hiddenName, 0, null) { Type = tupleType, Expression = Coerce(tupleInit.Expression, tupleType, tupleInit.Type) };
                                boundLocalsInBlock.Add(temporary); boundStatements.Add(new BoundLetStatement(temporary));
                                void Introduce(TupleBindingSyntax pattern, TypeShape shape, BoundExpression source)
                                {
                                    if (pattern.Elements.Count == 0)
                                    {
                                        var type = shape.ToString(); locals.Add(pattern.Token.Text, (0, type)); introduced.Add(pattern.Token.Text);
                                        var local = new BoundLocal(pattern.Token.Text, 0, null) { Type = type, Expression = source };
                                        boundLocalsInBlock.Add(local); boundStatements.Add(new BoundLetStatement(local));
                                        return;
                                    }
                                    if (!shape.IsTuple || shape.IsNullable || shape.Arguments.Count != pattern.Elements.Count)
                                        Fail("DS2008", pattern.Token, "Tuple destructuring arity or nested tuple type does not match the value.");
                                    for (var i = 0; i < pattern.Elements.Count; i++)
                                        Introduce(pattern.Elements[i], shape.Arguments[i], new BoundTupleItem(source, i));
                                }
                                Introduce(declaration.Pattern, tupleShape, new BoundReference(hiddenName));
                                if (reachable && tupleInit.Type == "<never>") reachable = false;
                                break;
                            }
                            case ReturnStatementSyntax ret:
                                var returned = BindExpression(ret.Value, currentReturnType ?? expectedResultType);
                                if (currentReturnType is null)
                                {
                                    if (inferenceReachable && returned.Type != "<never>") inferredReturns!.Add(returned.Type);
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
                                if (GenericTypes.TryParse(expr.Type, out var discardedFunction) && discardedFunction.IsFunction)
                                    Fail("DS2026", exprStatement.Expression.Token, "A function value cannot be discarded.");
                                Require(exprStatement.Expression, expr.Type, "Unit");
                                boundStatements.Add(new BoundExpressionStatement(Coerce(expr.Expression, "Unit", expr.Type)));
                                if (reachable && expr.Type == "<never>") reachable = false;
                                break;
                        }
                    }
                    if (inferencePass && !reachable)
                        return (new BoundBlock(boundLocalsInBlock, new BoundUnit())
                            { Statements = boundStatements, CanComplete = false, EvaluateResult = false, ResultType = "<never>" }, "<never>");
                    inferenceReachable = enclosingReachable && reachable;
                    var final = BindExpression(block.Result, expectedResultType);
                    var type = reachable ? final.Type : "<never>";
                    return (new BoundBlock(boundLocalsInBlock, final.Expression) { Statements = boundStatements, CanComplete = type != "<never>", EvaluateResult = reachable, ResultType = final.Type }, type);
                }
                finally
                {
                    inferenceReachable = enclosingReachable;
                    if (!keepLocals) foreach (var name in introduced) locals.Remove(name);
                }
            }
            (BoundExpression Expression, string Type) BindPipe(PipeSyntax pipe, string? expectedResult)
            {
                // Module functions and union constructors already have typed
                // call binding, including inference and lexical lookup. They
                // have no effectful receiver, so a direct call evaluates the
                // piped expression once in the proper order.
                var target = pipe.Right;
                while (target is GroupSyntax group) target = group.Inner;
                if (resultHelpers.ContainsKey(function.Name.Text))
                {
                    if (target is ValueSyntax preludeValue && !locals.ContainsKey(preludeValue.Value.Text) && !inaccessibleCaptures.Contains(preludeValue.Value.Text) &&
                        preludeNames.TryGetValue(preludeValue.Value.Text, out var valueName))
                        target = preludeValue with { Value = preludeValue.Value with { Text = valueName } };
                    else if (target is CallSyntax preludeCall && !locals.ContainsKey(preludeCall.Name.Text) && !inaccessibleCaptures.Contains(preludeCall.Name.Text) &&
                        preludeNames.TryGetValue(preludeCall.Name.Text, out var callName))
                        target = preludeCall with { Name = preludeCall.Name with { Text = callName } };
                }
                if (target is ValueSyntax actorName && !locals.ContainsKey(actorName.Value.Text) && IsActorMethodName(actorName.Value.Text))
                    return BindExpression(new CallSyntax(actorName.Value, pipe.Operator, new[] { pipe.Left }), expectedResult);
                if (target is ValueSyntax name && !locals.ContainsKey(name.Value.Text) && !IsDefaultMethodName(name.Value.Text) &&
                    !inaccessibleCaptures.Contains(name.Value.Text) &&
                    (signatures.ContainsKey(name.Value.Text) || cases.ContainsKey(name.Value.Text)))
                    return BindExpression(new CallSyntax(name.Value, pipe.Operator, new[] { pipe.Left }), expectedResult);
                if (target is FieldAccessSyntax { Receiver: ValueSyntax qualifier } member &&
                    !locals.ContainsKey(qualifier.Value.Text) && !inaccessibleCaptures.Contains(qualifier.Value.Text) &&
                    unions.ContainsKey(UnionQualifier(qualifier.Value.Text)))
                    return BindExpression(new MemberCallSyntax(member.Receiver, member.Field, pipe.Operator, new[] { pipe.Left }), expectedResult);

                // A noncompleting left side never evaluates a dynamic target.
                // Inference can collect its exits without knowing an as-yet
                // unresolved target signature; the final pass checks both.
                bool NeverBlock(BlockSyntax block)
                {
                    var statements = block.Statements.Count > 0 ? block.Statements :
                        block.Bindings.Select(binding => (StatementSyntax)new LetStatementSyntax(binding)).ToArray();
                    return statements.Any(statement => statement switch
                    {
                        ReturnStatementSyntax => true,
                        LetStatementSyntax binding => Never(binding.Binding.Expression ?? new ValueSyntax(binding.Binding.Initializer)),
                        TupleLetStatementSyntax binding => Never(binding.Value),
                        ExpressionStatementSyntax evaluated => Never(evaluated.Expression),
                        _ => false
                    }) || Never(block.Result);
                }
                bool Never(ExpressionSyntax expression) => expression switch
                {
                    GroupSyntax group => Never(group.Inner),
                    TupleSyntax tuple => tuple.Elements.Any(Never),
                    ListSyntax list => list.Elements.Any(Never),
                    InterpolationSyntax interpolation => interpolation.Holes.Any(Never),
                    ForSyntax loop => Never(loop.Source),
                    UsingSyntax owned => Never(owned.Acquisition) || NeverBlock(owned.Body),
                    AwaitSyntax awaited => Never(awaited.Operand),
                    IfSyntax conditional => Never(conditional.Condition) ||
                        (LiteralCondition(conditional.Condition) == true ? NeverBlock(conditional.Then) :
                         LiteralCondition(conditional.Condition) == false ? NeverBlock(conditional.Else) :
                         conditional.HasElse && NeverBlock(conditional.Then) && NeverBlock(conditional.Else)),
                    MatchSyntax match => Never(match.Scrutinee) || match.Arms.All(arm =>
                        arm.Guard is not null && Never(arm.Guard) || Never(arm.Value)),
                    CallSyntax call => call.Arguments.Any(Never),
                    InvokeSyntax invoke => Never(invoke.Receiver) || invoke.Arguments.Any(Never),
                    MemberCallSyntax call => Never(call.Receiver) || call.Arguments.Any(Never),
                    ConditionalMemberCallSyntax call => Never(call.Receiver),
                    PropagateSyntax propagation => Never(propagation.Operand),
                    UnarySyntax unary => Never(unary.Operand),
                    BinarySyntax binary => Never(binary.Left) ||
                        (binary.Operator.Text is not ("&&" or "||") ||
                         binary.Operator.Text == "&&" && LiteralCondition(binary.Left) == true ||
                         binary.Operator.Text == "||" && LiteralCondition(binary.Left) == false) && Never(binary.Right),
                    CoalesceSyntax coalesce => Never(coalesce.Left),
                    PipeSyntax nested => Never(nested.Left) || Never(nested.Right),
                    RecordConstructionSyntax record => record.Fields.Any(field => Never(field.Value)),
                    RecordUpdateSyntax update => Never(update.Receiver) || update.Fields.Any(field => Never(field.Value)),
                    FieldAccessSyntax access => Never(access.Receiver),
                    ConditionalFieldAccessSyntax access => Never(access.Receiver),
                    _ => false
                };
                if (inferencePass && Never(pipe.Left))
                {
                    var savedExits = inferredReturns;
                    if (savedExits is not null) inferredReturns = new List<string>();
                    var canBind = false;
                    try { BindExpression(pipe.Left); canBind = true; }
                    catch (SourceError) { /* The callable may supply required context. */ }
                    finally { inferredReturns = savedExits; }
                    if (canBind)
                    {
                        BindExpression(pipe.Left);
                        return (new BoundUnit(), "<never>");
                    }
                }

                // Determine a callable's parameter type without collecting
                // speculative function-return evidence. Runtime evaluation is
                // emitted in the opposite order: value first, receiver second.
                (BoundExpression Expression, string Type)? receiverProbe = null;
                var savedReachable = inferenceReachable;
                string? pipeProbeType = null;
                string? pipeReceiverHint = null;
                var pipeNullInput = false;
                try
                {
                    inferenceReachable = false;
                    try
                    {
                        var leftProbe = BindExpression(pipe.Left);
                        pipeProbeType = leftProbe.Type == "<never>" ? KnownType(leftProbe.Expression) : leftProbe.Type;
                        pipeNullInput = pipeProbeType == "<null>";
                        if (pipeProbeType is "<null>" or "<never>") pipeProbeType = null;
                    }
                    catch (SourceError) { /* The target may provide the required context. */ }
                    if (pipeProbeType is not null && expectedResult is not null && target is CallSyntax targetCall &&
                        !locals.ContainsKey(targetCall.Name.Text) && signatures.TryGetValue(targetCall.Name.Text, out var pipeTarget))
                    {
                        var remaining = pipeTarget.Parameters.Count - targetCall.Arguments.Count;
                        if (remaining == 1)
                            pipeReceiverHint = TypeShape.FunctionName + "<" + pipeProbeType + "," + expectedResult + ">";
                        else if (remaining > 1 && GenericTypes.TryParse(expectedResult, out var rest) && rest.IsFunction && rest.Arguments.Count == remaining)
                            pipeReceiverHint = TypeShape.FunctionName + "<" + pipeProbeType + "," + string.Join(",", rest.Arguments) + ">";
                    }
                    try { receiverProbe = BindExpression(pipe.Right, pipeReceiverHint, pipeProbeType, expectedResult, pipeNullInput, isPipeTarget: true); }
                    catch (SourceError error) when (target is LambdaSyntax && error.Diagnostic.Code == "DS2026") { }
                }
                finally { inferenceReachable = savedReachable; }

                string? inputType = null;
                string? lambdaHint = null;
                if (receiverProbe is { } probe)
                {
                    var probeType = probe.Type == "<never>" ? KnownType(probe.Expression) : probe.Type;
                    if (probeType is null || !GenericTypes.TryParse(probeType, out var functionShape) || !functionShape.IsFunction || functionShape.IsNullable)
                        Fail("DS2026", pipe.Operator, "The right side of a pipe must be a non-nullable function value.");
                    if (functionShape!.Arguments.Count < 2)
                        Fail("DS2012", pipe.Operator, "A pipe target must accept at least one argument.");
                    inputType = functionShape.Arguments[0].ToString();
                }
                else if (target is LambdaSyntax lambda)
                {
                    if (lambda.Parameters.Count != 1) Fail("DS2012", pipe.Operator, "A pipe target must accept exactly one argument.");
                    if (lambda.Parameters[0].Type is { } annotation)
                        inputType = ScopedType(annotation, functionTypeScope).Text;
                }
                else Fail("DS2026", pipe.Operator, "Cannot infer the pipe target.");

                var value = BindExpression(pipe.Left, inputType);
                if (inputType is null)
                {
                    inputType = value.Type == "<never>" ? KnownType(value.Expression) : value.Type;
                    if (inputType is null || !SupportedType(inputType, activeTypeParameters))
                        Fail("DS2026", pipe.Left.Token, "Cannot infer a pipe parameter type from this value.");
                }
                if (receiverProbe is null)
                {
                    var resultHint = expectedResult is not null && SupportedType(expectedResult, activeTypeParameters)
                        ? expectedResult : null;
                    // A hygienic, unbound symbol asks BindLambda to infer its
                    // result while its input is determined by the pipe value.
                    if (resultHint is null)
                    {
                        var marker = int.MaxValue;
                        while (activeTypeParameters.Contains("$" + marker.ToString(CultureInfo.InvariantCulture))) marker--;
                        resultHint = "$" + marker.ToString(CultureInfo.InvariantCulture);
                    }
                    lambdaHint = TypeShape.FunctionName + "<" + inputType + "," + resultHint + ">";
                }
                inferenceReachable = savedReachable && value.Type != "<never>";
                (BoundExpression Expression, string Type) receiver;
                try { receiver = BindExpression(pipe.Right, lambdaHint ?? pipeReceiverHint, inputType, expectedResult, pipeNullInput, isPipeTarget: true); }
                finally { inferenceReachable = savedReachable; }
                var receiverType = receiver.Type == "<never>" ? KnownType(receiver.Expression) : receiver.Type;
                if (receiverType is null || !GenericTypes.TryParse(receiverType, out var shape) || !shape.IsFunction || shape.IsNullable || shape.Arguments.Count < 2)
                    Fail("DS2026", pipe.Operator, "The right side of a pipe must be a non-nullable function with an argument.");
                var formal = shape!.Arguments[0].ToString();
                Require(pipe.Left, value.Type, formal);
                if (shape.Arguments.Count > 2 && (CapabilityType(formal) || CapabilityType(value.Type)))
                    Fail("DS2026", pipe.Operator, "A partial application cannot capture a service or interface reference.");
                var result = shape.Arguments.Count == 2 ? shape.Arguments[1].ToString() : TypeShape.FunctionName + "<" + string.Join(",", shape.Arguments.Skip(1)) + ">";
                return (new BoundPipe(Coerce(value.Expression, formal, value.Type), formal,
                    Coerce(receiver.Expression, receiverType!, receiver.Type), result) { ReceiverType = receiverType! },
                    value.Type == "<never>" || receiver.Type == "<never>" ? "<never>" : result);
            }
            (BoundExpression Expression, string Type) BindInvocation(ExpressionSyntax receiverSyntax, Token location, IReadOnlyList<ExpressionSyntax> arguments, string? expectedResult = null)
            {
                var unwrappedReceiver = receiverSyntax;
                while (unwrappedReceiver is GroupSyntax group) unwrappedReceiver = group.Inner;
                // An immediately saturated standard helper prefix evaluates
                // callback and input once in order, just as a positional call.
                // The final input can therefore resolve the overload.
                CallSyntax? resultPrefix = null;
                var prefixArguments = new List<ExpressionSyntax>();
                bool CollectResultPrefix(ExpressionSyntax node)
                {
                    while (node is GroupSyntax grouped) node = grouped.Inner;
                    if (node is InvokeSyntax invoke)
                    {
                        if (!CollectResultPrefix(invoke.Receiver)) return false;
                        prefixArguments.AddRange(invoke.Arguments);
                        return true;
                    }
                    if (node is not CallSyntax prefix || locals.ContainsKey(prefix.Name.Text) ||
                        inaccessibleCaptures.Contains(prefix.Name.Text) || IsDefaultMethodName(prefix.Name.Text) || IsActorMethodName(prefix.Name.Text))
                        return false;
                    var prefixName = resultHelpers.ContainsKey(function.Name.Text) &&
                        preludeNames.TryGetValue(prefix.Name.Text, out var resolvedPreludeName)
                            ? resolvedPreludeName : prefix.Name.Text;
                    if (!TryResultOverloadFamily(prefixName, out _)) return false;
                    resultPrefix = prefixName == prefix.Name.Text ? prefix : prefix with { Name = prefix.Name with { Text = prefixName } };
                    prefixArguments.AddRange(prefix.Arguments);
                    return true;
                }
                if (CollectResultPrefix(unwrappedReceiver) && prefixArguments.Count < 2 && prefixArguments.Count + arguments.Count == 2)
                {
                    prefixArguments.AddRange(arguments);
                    return BindExpression(resultPrefix! with { Arguments = prefixArguments }, expectedResult);
                }
                if (unwrappedReceiver is ValueSyntax actorName && !locals.ContainsKey(actorName.Value.Text) && IsActorMethodName(actorName.Value.Text))
                    return BindExpression(new CallSyntax(actorName.Value, location, arguments), expectedResult);
                if (unwrappedReceiver is ValueSyntax name && !locals.ContainsKey(name.Value.Text) && !IsDefaultMethodName(name.Value.Text) &&
                    !inaccessibleCaptures.Contains(name.Value.Text) && !cases.ContainsKey(name.Value.Text) && signatures.ContainsKey(name.Value.Text))
                    return BindExpression(new CallSyntax(name.Value, location, arguments), expectedResult);
                (BoundExpression Expression, string Type) receiver;
                if (unwrappedReceiver is MemberCallSyntax { Member.Text: "map" or "fold", Arguments.Count: 0 } emptyGeneric && arguments.Count > 0)
                {
                    // The first grouped argument is evidence for the otherwise
                    // unbound generic type, just as the left side of a pipe is.
                    // Probe only for type information; the actual arguments are
                    // bound and evaluated in their original order below.
                    var savedReturns = inferredReturns;
                    if (savedReturns is not null) inferredReturns = new List<string>();
                    var savedReachability = inferenceReachable;
                    var savedHasReturn = hasReturn;
                    var savedTupleCount = tupleBindingCount;
                    string? argumentType = null;
                    var argumentIsNull = false;
                    try
                    {
                        inferenceReachable = false;
                        var probe = BindExpression(arguments[0]);
                        argumentType = probe.Type == "<never>" ? KnownType(probe.Expression) : probe.Type;
                        argumentIsNull = argumentType == "<null>";
                        if (argumentType is "<null>" or "<never>") argumentType = null;
                    }
                    catch (SourceError) { /* The expected result may provide context instead. */ }
                    finally { inferredReturns = savedReturns; inferenceReachable = savedReachability; hasReturn = savedHasReturn; tupleBindingCount = savedTupleCount; }
                    if (emptyGeneric.Member.Text == "fold" && arguments.Count > 1 && argumentType is null && argumentIsNull &&
                        expectedResult is not null && IsNullable(expectedResult)) argumentType = expectedResult;
                    var remainingHint = emptyGeneric.Member.Text == "map" || arguments.Count == 1 ? expectedResult : null;
                    receiver = BindExpression(receiverSyntax, null, argumentType, remainingHint, argumentIsNull);
                }
                else if (unwrappedReceiver is MemberCallSyntax { Arguments.Count: 0 } && arguments.Count > 0)
                {
                    // Prefer an already callable full zero-argument member.
                    // Only when that call cannot supply a callable do grouped
                    // invocation arguments provide evidence for an empty
                    // extension prefix. Probes never execute expressions.
                    (BoundExpression Expression, string Type)? full = null;
                    var savedReturns = inferredReturns;
                    var savedReachability = inferenceReachable;
                    var savedHasReturn = hasReturn;
                    var savedTupleCount = tupleBindingCount;
                    if (savedReturns is not null) inferredReturns = new List<string>();
                    try
                    {
                        inferenceReachable = false;
                        full = BindExpression(receiverSyntax);
                    }
                    catch (SourceError error) when (error.Diagnostic.Code == "DS2018") { }
                    finally
                    {
                        inferredReturns = savedReturns;
                        inferenceReachable = savedReachability;
                        hasReturn = savedHasReturn;
                        tupleBindingCount = savedTupleCount;
                    }
                    var fullType = full is { } resolved && resolved.Type == "<never>" ? KnownType(resolved.Expression) : full?.Type;
                    var fullFits = false;
                    if (fullType is not null && GenericTypes.TryParse(fullType, out var fullShape) && fullShape.IsFunction &&
                        !fullShape.IsNullable && arguments.Count < fullShape.Arguments.Count)
                    {
                        // A full callable keeps precedence when it accepts the
                        // grouped invocation. An incompatible callable must
                        // not hide an applicable longer extension overload.
                        var afterFullReturns = inferredReturns;
                        var afterFullReachability = inferenceReachable;
                        var afterFullHasReturn = hasReturn;
                        var afterFullTupleCount = tupleBindingCount;
                        if (afterFullReturns is not null) inferredReturns = new List<string>();
                        try
                        {
                            inferenceReachable = false;
                            fullFits = true;
                            for (var i = 0; i < arguments.Count; i++)
                            {
                                var formalType = fullShape.Arguments[i].ToString();
                                var probe = BindExpression(arguments[i], formalType);
                                if (probe.Type != "<never>" && !Convertible(probe.Type, formalType)) { fullFits = false; break; }
                            }
                            var afterCall = arguments.Count == fullShape.Arguments.Count - 1
                                ? fullShape.Arguments[^1].ToString()
                                : TypeShape.FunctionName + "<" + string.Join(",", fullShape.Arguments.Skip(arguments.Count)) + ">";
                            if (fullFits && expectedResult is not null && SupportedType(expectedResult, activeTypeParameters) &&
                                !Convertible(afterCall, expectedResult)) fullFits = false;
                        }
                        catch (SourceError) { fullFits = false; }
                        finally
                        {
                            inferredReturns = afterFullReturns;
                            inferenceReachable = afterFullReachability;
                            hasReturn = afterFullHasReturn;
                            tupleBindingCount = afterFullTupleCount;
                        }
                    }
                    if (fullFits)
                        receiver = BindExpression(receiverSyntax);
                    else
                    {
                        inferredReturns = savedReturns;
                        inferenceReachable = savedReachability;
                        hasReturn = savedHasReturn;
                        tupleBindingCount = savedTupleCount;
                        if (savedReturns is not null) inferredReturns = new List<string>();
                        var groupedTypes = new List<string?>();
                        try
                        {
                            inferenceReachable = false;
                            foreach (var argument in arguments)
                            {
                                try
                                {
                                    var probe = BindExpression(argument);
                                    var argumentType = probe.Type == "<never>" ? KnownType(probe.Expression) : probe.Type;
                                    groupedTypes.Add(argumentType == "<never>" ? null : argumentType);
                                }
                                catch (SourceError) { groupedTypes.Add(null); }
                            }
                        }
                        finally
                        {
                            inferredReturns = savedReturns;
                            inferenceReachable = savedReachability;
                            hasReturn = savedHasReturn;
                            tupleBindingCount = savedTupleCount;
                        }
                        var firstType = groupedTypes[0];
                        receiver = BindExpression(receiverSyntax, null, firstType == "<null>" ? null : firstType,
                            arguments.Count == 1 ? expectedResult : null, firstType == "<null>", groupedTypes, expectedResult);
                    }
                }
                else if (unwrappedReceiver is FieldAccessSyntax)
                {
                    // A grouped bare method value receives evidence from its
                    // invocation without executing or committing any probe.
                    // Normal field and qualified-case lookup still happens
                    // first when the receiver is bound below.
                    var savedReturns = inferredReturns;
                    var savedReachability = inferenceReachable;
                    var savedHasReturn = hasReturn;
                    var savedTupleCount = tupleBindingCount;
                    if (savedReturns is not null) inferredReturns = new List<string>();
                    var groupedTypes = new List<string?>();
                    try
                    {
                        inferenceReachable = false;
                        foreach (var argument in arguments)
                        {
                            try
                            {
                                var probe = BindExpression(argument);
                                var argumentType = probe.Type == "<never>" ? KnownType(probe.Expression) : probe.Type;
                                groupedTypes.Add(argumentType == "<never>" ? null : argumentType);
                            }
                            catch (SourceError) { groupedTypes.Add(null); }
                        }
                    }
                    finally
                    {
                        inferredReturns = savedReturns;
                        inferenceReachable = savedReachability;
                        hasReturn = savedHasReturn;
                        tupleBindingCount = savedTupleCount;
                    }
                    var firstType = groupedTypes.Count > 0 ? groupedTypes[0] : null;
                    receiver = BindExpression(receiverSyntax, null, firstType == "<null>" ? null : firstType,
                        expectedResult, firstType == "<null>", groupedTypes, expectedResult);
                }
                else receiver = BindExpression(receiverSyntax);
                var type = receiver.Type == "<never>" ? KnownType(receiver.Expression) : receiver.Type;
                if (type is null || !GenericTypes.TryParse(type, out var shape) || !shape.IsFunction)
                    Fail("DS1002", location, "Only function values are callable.");
                if (shape!.IsNullable)
                    Fail("DS2026", location, "Invocation requires a non-nullable function value.");
                var functionShape = shape!;
                var formal = functionShape.Arguments.Take(functionShape.Arguments.Count - 1)
                    .Select(argument => new Token(argument.ToString(), location.Span)).ToArray();
                if (arguments.Count > formal.Length) Fail("DS2012", location, "Incorrect number of arguments.");
                var savedInvokeReachable = inferenceReachable;
                inferenceReachable = savedInvokeReachable && receiver.Type != "<never>";
                (IReadOnlyList<BoundExpression> Values, IReadOnlyList<string> Types, Dictionary<string, string> Substitutions, bool Never) values;
                try { values = BindGenericArguments(Array.Empty<Token>(), Array.Empty<Token>(), formal.Take(arguments.Count).ToArray(), arguments, location); }
                finally { inferenceReachable = savedInvokeReachable; }
                var resultType = functionShape.Arguments[^1].ToString();
                if (arguments.Count < formal.Length)
                {
                    if (formal.Take(arguments.Count).Any(parameter => CapabilityType(parameter.Text)) || values.Types.Any(CapabilityType))
                        Fail("DS2026", location, "A partial application cannot capture a service or interface reference.");
                    var partialType = TypeShape.FunctionName + "<" + string.Join(",", formal.Skip(arguments.Count).Select(t => t.Text).Append(resultType)) + ">";
                    return (new BoundPartial(receiver.Expression, type!, values.Values, formal.Take(arguments.Count).Select(t => t.Text).ToArray(),
                        formal.Skip(arguments.Count).Select(t => t.Text).ToArray(), resultType, partialType),
                        receiver.Type == "<never>" || values.Never ? "<never>" : partialType);
                }
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
                var savedReachable = inferenceReachable;
                var savedAwaitAllowed = awaitAllowed;
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
                var partialReturnHint = expected?.Arguments[^1].ToString();
                if (lambda.IsAsync && partialReturnHint is not null)
                {
                    // Async lambdas have a Task-valued delegate result, while
                    // the body, explicit return and Result propagation use its
                    // logical payload type. A not-yet-inferred generic result is
                    // left open so the checked body can provide the evidence.
                    if (GenericTypes.TryParse(partialReturnHint, out var expectedTask) &&
                        expectedTask.Name == taskSymbol && !expectedTask.IsNullable && expectedTask.Arguments.Count == 1)
                        partialReturnHint = expectedTask.Arguments[0].ToString();
                    else if (SupportedType(partialReturnHint, activeTypeParameters))
                        Fail("DS2026", lambda.Start, "An async lambda requires a Task result in its expected function type.");
                    else partialReturnHint = null;
                }
                var expectedReturn = partialReturnHint;
                if (expectedReturn is not null && !SupportedType(expectedReturn, activeTypeParameters)) expectedReturn = null;
                lambdaDepth++;
                try
                {
                    // Each source lambda establishes its own await boundary.
                    awaitAllowed = lambda.IsAsync;
                    inaccessibleCaptures = new HashSet<string>(savedCaptures, StringComparer.Ordinal);
                    inaccessibleCaptures.UnionWith(savedLocals.Keys);
                    (BoundBlock Block, string Type) BindBody(string? resultType, List<string>? returns, string? bodyHint = null)
                    {
                        // The lambda sees its lexical ancestors while binding. Its
                        // actual immutable captures are computed from the final
                        // bound body after any contextual inference/rebinding.
                        locals = new Dictionary<string, (int Value, string Type)>(savedLocals, StringComparer.Ordinal);
                        for (var i = 0; i < lambda.Parameters.Count; i++) locals.Add(lambda.Parameters[i].Name.Text, (0, parameterTypes[i]));
                        currentReturnType = resultType;
                        inferredReturns = returns;
                        hasReturn = false;
                        inferenceReachable = true;
                        return BindBlock(lambda.Body, true, bodyHint ?? resultType);
                    }
                    var returns = new List<string>();
                    var body = BindBody(expectedReturn, expectedReturn is null ? returns : null, partialReturnHint);
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
                    var delegateReturn = lambda.IsAsync ? taskSymbol + "<" + returnType + ">" : returnType!;
                    if (lambda.IsAsync) taskUsed = true;
                    var shapes = parameterTypes.Append(delegateReturn).Select(type => { GenericTypes.TryParse(type, out var shape); return shape; }).ToArray();
                    var lambdaType = new TypeShape(TypeShape.FunctionName, shapes, false).ToString();
                    // Include references made solely by nested lambdas: the
                    // intermediate environment must carry them to the point at
                    // which the nested closure is created. No-shadowing makes
                    // name identity unambiguous within this lexical ancestry.
                    var referenced = new HashSet<string>(StringComparer.Ordinal);
                    void CollectReferences(object? node)
                    {
                        if (node is null || node is string) return;
                        if (node is BoundReference reference) referenced.Add(reference.Name);
                        if (node is System.Collections.IEnumerable sequence)
                        {
                            foreach (var item in sequence) CollectReferences(item);
                            return;
                        }
                        var nodeType = node.GetType();
                        if (!nodeType.Name.StartsWith("Bound", StringComparison.Ordinal)) return;
                        foreach (var property in nodeType.GetProperties())
                            if (property.GetIndexParameters().Length == 0) CollectReferences(property.GetValue(node));
                    }
                    CollectReferences(boundBody);
                    var captures = savedLocals.Where(entry => referenced.Contains(entry.Key))
                        .Select(entry => new BoundCapture(entry.Key, entry.Value.Type)).ToArray();
                    foreach (var capture in captures)
                        if (!DataType(capture.Type)) Fail("DS2026", lambda.Start, "Only immutable data values can be captured.");
                    return (new BoundLambda(lambda.Parameters.Select(parameter => parameter.Name.Text).ToArray(), parameterTypes,
                        lambdaType, returnType!, boundBody, hasReturn) { Captures = captures, IsAsync = lambda.IsAsync }, lambdaType);
                }
                finally
                {
                    lambdaDepth--;
                    locals = savedLocals;
                    inaccessibleCaptures = savedCaptures;
                    currentReturnType = savedReturnType;
                    inferredReturns = savedReturns;
                    hasReturn = savedHasReturn;
                    inferenceReachable = savedReachable;
                    awaitAllowed = savedAwaitAllowed;
                }
            }
            var sourceBody = function.Body ?? new BlockSyntax(function.Bindings, function.Expression ?? new ValueSyntax(function.Literal));
            if (ownedFactories.Contains(function.Name.Text))
            {
                ValidateFactoryBody(sourceBody);
                MarkOwnedBlock(sourceBody);
            }
            if (function.InferReturnType)
            {
                // Infer from the function's own reachable exits only, never
                // from a caller's expected type. Bind again with the resulting
                // type to validate and lower the complete body normally.
                currentReturnType = null;
                var exits = new List<string>();
                inferredReturns = exits;
                var inferredBody = BindBlock(sourceBody, true);
                if (inferredBody.Type != "<never>") exits.Add(inferredBody.Type);
                var candidate = exits.FirstOrDefault(type => type != "<null>");
                if (candidate is null || candidate is "<never>" or "<invalid>" ||
                    exits.Any(type => type != "<null>" && type != candidate))
                    Fail("DS2027", function.Name, "Cannot infer a unique return type without widening or caller context.");
                if (exits.Contains("<null>") && !IsNullable(candidate!)) candidate += "?";
                if (!SupportedType(candidate!, activeTypeParameters))
                    Fail("DS2027", function.Name, "Cannot infer a supported return type.");
                function = function with { ReturnType = new Token(candidate!, function.ReturnType.Span), InferReturnType = false };
                signatures[function.Name.Text] = function;
                continue;
            }
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
                IsAsync = function.IsAsync,
                RequiresDispatcher = requiresDispatcher,
                IsOwnedFactory = ownedFactories.Contains(function.Name.Text),
                IsActorBody = actorMethodsByBody.ContainsKey(function.Name.Text),
                ResultLocal = (result.Expression as BoundReference)?.Name,
                ReturnType = function.ReturnType.Text,
                ParameterTypes = function.Parameters.Select(p => p.Type.Text).ToArray(),
                Parameters = function.Parameters.Count == 0 ? Array.Empty<string>() : function.Parameters.Select(p => p.Name.Text).ToArray(),
                Expression = boundLocals.Count == 0 && result.Expression is BoundInt ? null : result.Expression,
                TypeParameters = function.TypeParameters.Select(parameter => parameter.Text).ToArray()
            });
            if (resultHelpers.ContainsKey(function.Name.Text))
                foreach (var dependency in usedResultHelpers)
                    if (!boundPreludeHelpers.Contains(dependency)) inferenceOrder.Add(resultHelpers[dependency]);
        }
        // Propagate only value-returning Invoke entries through directly awaited
        // ordinary async functions. Resolve effects before validating uses so
        // recursion, forward calls and transitive callback escapes fail closed.
        var dispatcherEntries = functions.Where(item => item.RequiresDispatcher).Select(item => item.Name).ToHashSet(StringComparer.Ordinal);
        var functionByName = functions.ToDictionary(item => item.Name, StringComparer.Ordinal);
        bool CanForward(string name) => functionByName[name].ReturnType != invocationReceiptSymbol &&
            functionByName[name].ReturnType != publicationReceiptSymbol;
        bool IsReceiptRelay(BoundFunction owner, BoundCall call) =>
            receiptRelayEntries.Contains(owner.Name) && owner.ReturnType == functionByName[call.Name].ReturnType &&
            owner.Expression is BoundAwait awaited && ReferenceEquals(awaited.Operand, call);
        void WalkBound(object? node, Action<object, bool, bool> visit, bool immediateAwait = false, bool inLambda = false)
        {
            if (node is null or string) return;
            if (node is System.Collections.IEnumerable sequence)
            {
                foreach (var item in sequence) WalkBound(item, visit, false, inLambda);
                return;
            }
            var type = node.GetType();
            if (!type.Name.StartsWith("Bound", StringComparison.Ordinal)) return;
            visit(node, immediateAwait, inLambda);
            if (node is BoundAwait awaited)
            {
                WalkBound(awaited.Operand, visit, true, inLambda);
                return;
            }
            var nestedLambda = inLambda || node is BoundLambda;
            foreach (var property in type.GetProperties())
                if (property.GetIndexParameters().Length == 0)
                    WalkBound(property.GetValue(node), visit, false, nestedLambda);
        }
        bool dispatcherChanged;
        do
        {
            dispatcherChanged = false;
            foreach (var owner in functions)
                WalkBound(owner, (node, _, _) =>
                {
                    if (node is BoundCall call && dispatcherEntries.Contains(call.Name) && (CanForward(call.Name) || IsReceiptRelay(owner, call)) &&
                        dispatcherEntries.Add(owner.Name)) dispatcherChanged = true;
                });
        } while (dispatcherChanged);
        for (var i = 0; i < functions.Count; i++)
            if (dispatcherEntries.Contains(functions[i].Name) && !functions[i].RequiresDispatcher)
                functions[i] = functions[i] with { RequiresDispatcher = true };
        for (var i = 0; i < boundActors.Count; i++)
            if (dispatcherEntries.Contains(boundActors[i].ImplementationName))
                boundActors[i] = boundActors[i] with { RequiresDispatcher = true };
        foreach (var actor in syntax.Actors)
        {
            void CheckHttpReference(Token token, string? resolved, bool referencesResolved)
            {
                var identity = referencesResolved ? resolved : token.Text;
                if (identity is not null && dispatcherEntries.Contains(identity))
                    Fail("DS2047", token, "A dispatcher host entry cannot be used as an HTTP callback yet.");
            }
            foreach (var binding in actor.HttpBindings)
            {
                CheckHttpReference(binding.Binder, binding.BinderIdentity, binding.ReferencesResolved);
                CheckHttpReference(binding.BindingMapper, binding.BindingMapperIdentity, binding.ReferencesResolved);
                CheckHttpReference(binding.SuccessMapper, binding.SuccessMapperIdentity, binding.ReferencesResolved);
                CheckHttpReference(binding.BusinessMapper, binding.BusinessMapperIdentity, binding.ReferencesResolved);
            }
            foreach (var binding in actor.HttpPlainBindings)
            {
                CheckHttpReference(binding.Binder, binding.BinderIdentity, binding.ReferencesResolved);
                CheckHttpReference(binding.BindingMapper, binding.BindingMapperIdentity, binding.ReferencesResolved);
                CheckHttpReference(binding.SuccessMapper, binding.SuccessMapperIdentity, binding.ReferencesResolved);
            }
        }
        foreach (var owner in functions)
        {
            WalkBound(owner, (node, immediatelyAwaited, inLambda) =>
            {
                if (node is BoundCall call && dispatcherEntries.Contains(call.Name))
                {
                    if ((!CanForward(call.Name) || !explicitAsyncSourceFunctions.Contains(owner.Name)) && !IsReceiptRelay(owner, call) ||
                        !immediatelyAwaited || inLambda)
                        Fail("DS2047", call.Location ?? signatures[owner.Name].Name, "A dispatcher entry requires a directly awaited call from an ordinary async function.");
                }
                if (node is BoundFunctionReference reference && dispatcherEntries.Contains(reference.Name))
                    Fail("DS2047", reference.Location ?? signatures[owner.Name].Name, "A dispatcher host entry cannot be captured from D# yet.");
            });
        }
        // Generic parameters used in data positions must remain data-safe at
        // every call, even when the function returns Unit or forwards through
        // several generic helpers. Discover those requirements to a fixed point.
        var dataRequirements = functions.ToDictionary(item => item.Name,
            _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        // A generic partial application may retain a value whose eventual type
        // is not known at its declaration. Track this separately from immutable
        // data requirements: tasks/functions remain permitted execution values,
        // while service and interface references cannot be retained.
        var capabilityRequirements = functions.ToDictionary(item => item.Name,
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
                var capabilityRequired = capabilityRequirements[owner.Name];
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
                    var childData = inData || (!shape.IsFunction && shape.Name != taskSymbol && shape.Arguments.Count > 0);
                    foreach (var argument in shape.Arguments) Scan(argument, childData);
                }
                void NeedCapabilitySafe(TypeShape shape)
                {
                    if (shape.IsFunction) return;
                    if (shape.Arguments.Count == 0 && ownerParameters.Contains(shape.Name) && capabilityRequired.Add(shape.Name))
                        requirementsChanged = true;
                    foreach (var argument in shape.Arguments) NeedCapabilitySafe(argument);
                }
                void Visit(object? node)
                {
                    if (node is null) return;
                    if (node is BoundLambda closure)
                        foreach (var capture in closure.Captures)
                            if (GenericTypes.TryParse(capture.Type, out var captureType)) Need(captureType);
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
                    if (node is BoundPartial partial)
                        foreach (var type in partial.ArgumentTypes)
                            if (GenericTypes.TryParse(type, out var captured))
                            {
                                if (RetainsCapability(captured)) Fail("DS2026", new Token(owner.Name, syntheticSpan), "A partial application cannot capture a service or interface reference.");
                                NeedCapabilitySafe(captured);
                            }
                    if (node is BoundListFoldPartial fold && GenericTypes.TryParse(fold.AccumulatorType, out var foldCaptured))
                    {
                        if (RetainsCapability(foldCaptured)) Fail("DS2026", new Token(owner.Name, syntheticSpan), "A partial application cannot capture a service or interface reference.");
                        NeedCapabilitySafe(foldCaptured);
                    }
                    if (node is BoundPipe pipe && GenericTypes.TryParse(pipe.ReceiverType, out var pipeReceiver) &&
                        pipeReceiver.IsFunction && pipeReceiver.Arguments.Count > 2 && GenericTypes.TryParse(pipe.ValueType, out var pipeValue))
                    {
                        if (RetainsCapability(pipeValue)) Fail("DS2026", new Token(owner.Name, syntheticSpan), "A partial application cannot capture a service or interface reference.");
                        NeedCapabilitySafe(pipeValue);
                    }
                    if (node is BoundCall call && boundFunctions.TryGetValue(call.Name, out var callee))
                    {
                        for (var i = 0; i < Math.Min(call.TypeArguments.Count, callee.TypeParameters.Count); i++)
                        {
                            if (capabilityRequirements[callee.Name].Contains(callee.TypeParameters[i]) &&
                                GenericTypes.TryParse(call.TypeArguments[i], out var capabilityArgument))
                            {
                                if (RetainsCapability(capabilityArgument))
                                    Fail("DS2026", call.Location ?? new Token(call.Name, syntheticSpan), "A partial application cannot capture a service or interface reference.");
                                NeedCapabilitySafe(capabilityArgument);
                            }
                            if (!dataRequirements[callee.Name].Contains(callee.TypeParameters[i]) ||
                                !GenericTypes.TryParse(call.TypeArguments[i], out var argument)) continue;
                            if (argument.ContainsFunction || ContainsTask(argument) || ContainsCapability(argument))
                                Fail("DS2026", call.Location ?? new Token(call.Name, syntheticSpan), "Execution type is not valid for this generic data operation.");
                            Need(argument);
                        }
                    }
                    if (node is BoundFunctionReference reference && boundFunctions.TryGetValue(reference.Name, out var referenced))
                    {
                        for (var i = 0; i < Math.Min(reference.TypeArguments.Count, referenced.TypeParameters.Count); i++)
                        {
                            if (capabilityRequirements[referenced.Name].Contains(referenced.TypeParameters[i]) &&
                                GenericTypes.TryParse(reference.TypeArguments[i], out var capabilityArgument))
                            {
                                if (RetainsCapability(capabilityArgument))
                                    Fail("DS2026", reference.Location ?? new Token(reference.Name, syntheticSpan), "A partial application cannot capture a service or interface reference.");
                                NeedCapabilitySafe(capabilityArgument);
                            }
                            if (!dataRequirements[referenced.Name].Contains(referenced.TypeParameters[i]) ||
                                !GenericTypes.TryParse(reference.TypeArguments[i], out var argument)) continue;
                            if (argument.ContainsFunction || ContainsTask(argument) || ContainsCapability(argument))
                                Fail("DS2026", reference.Location ?? new Token(reference.Name, syntheticSpan), "Execution type is not valid for this generic data operation.");
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
        var byName = functions.ToDictionary(function => function.Name, StringComparer.Ordinal);
        return new(syntax.Name.Text, syntax.Functions.Select(function => byName[function.Name.Text]).Concat(usedResultHelpers.Select(name => byName[name])).ToArray())
        {
            ActorRoutes = actorRoutes.Values.ToArray(),
            MessageContracts = messageContracts.Values.ToArray(),
            InvocationReceiptSymbol = functions.Any(function => function.ReturnType == invocationReceiptSymbol) ? invocationReceiptSymbol : null,
            PublicationReceiptSymbol = functions.Any(function => function.ReturnType == publicationReceiptSymbol) ? publicationReceiptSymbol : null,
            ListSymbol = listUsed ? listSymbol : null,
            OptionSymbol = optionUsed ? optionSymbol : null,
            MapSymbol = mapUsed ? mapSymbol : null,
            SetSymbol = setUsed ? setSymbol : null,
            MutableListSymbol = mutableListUsed ? mutableListSymbol : null,
            MutableMapSymbol = mutableMapUsed ? mutableMapSymbol : null,
            TaskSymbol = taskUsed ? taskSymbol : null,
            Interfaces = syntax.Interfaces.Select(item => new BoundInterfaceDeclaration(item.Name.Text,
                item.Methods.Select(method => new BoundInterfaceMethod(method.Name.Text,
                    method.Parameters.Select(parameter => parameter.Type.Text).ToArray(), method.ReturnType.Text, method.IsAsync)
                    { IsOwnedFactory = OwnedTransfer(method.ReturnType.Text) }).ToArray())).ToArray(),
            Services = boundServiceDeclarations,
            Actors = boundActors,
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
    public static string Emit(BoundModule module, IReadOnlyList<ModuleExport>? exports = null)
    {
        // A generated namespace avoids a C# member/type name collision without restricting D# names.
        var output = new StringBuilder("// Generated by DSharp.\n#nullable disable\nnamespace DSharp.Generated;\n\n");
        var globalNames = new HashSet<string>(StringComparer.Ordinal);
        if (exports is null) globalNames.Add(module.Name);
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
        foreach (var contract in module.Interfaces)
        {
            globalNames.Add(contract.Name);
            foreach (var method in contract.Methods) globalNames.Add(method.Name);
        }
        foreach (var service in module.Services)
        {
            globalNames.Add(service.Name);
            foreach (var method in service.Methods) globalNames.Add(method.Name);
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
        // Interface/service names are separate from immutable record/union
        // names. Their members use a compiler-owned prefix so even source names
        // such as ToString, Equals, or GetType cannot collide with CLR members.
        var capabilityTypes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var contract in module.Interfaces)
            capabilityTypes.Add(contract.Name, UniqueGlobal("__DSharp_Interface_" + capabilityTypes.Count.ToString(CultureInfo.InvariantCulture)));
        foreach (var service in module.Services)
            capabilityTypes.Add(service.Name, UniqueGlobal("__DSharp_Service_" + capabilityTypes.Count.ToString(CultureInfo.InvariantCulture)));
        static string CapabilityMethod(string name) => "__dsharp_method_" + name;
        var structuralInterface = UniqueGlobal("__DSharp_Structural");
        var listTypeName = module.ListSymbol is null ? null : UniqueGlobal("__DSharp_List");
        var mapTypeName = module.MapSymbol is null ? null : UniqueGlobal("__DSharp_Map");
        var setTypeName = module.SetSymbol is null ? null : UniqueGlobal("__DSharp_Set");
        var mutableListTypeName = module.MutableListSymbol is null ? null : UniqueGlobal("__DSharp_MutableList");
        var mutableMapTypeName = module.MutableMapSymbol is null ? null : UniqueGlobal("__DSharp_MutableMap");
        var hasKeyedCollections = mapTypeName is not null || setTypeName is not null;
        var hashInterface = hasKeyedCollections ? UniqueGlobal("__DSharp_Hashable") : null;
        var container = "__DSharp_Functions";
        while (!globalNames.Add(container)) container += "_";
        var routeNames = module.ActorRoutes.Select((route, index) => (route.ActorIdentity,
                Name: UniqueGlobal("__dsharp_actor_route_" + index.ToString(CultureInfo.InvariantCulture))))
            .ToDictionary(item => item.ActorIdentity, item => item.Name, StringComparer.Ordinal);
        var contractNames = module.MessageContracts.Select((contract, index) => (contract.MessageType,
                Name: UniqueGlobal("__dsharp_message_contract_" + index.ToString(CultureInfo.InvariantCulture))))
            .ToDictionary(item => item.MessageType, item => item.Name, StringComparer.Ordinal);
        var unitType = "__DSharp_Unit";
        while (!globalNames.Add(unitType)) unitType += "_";
        var signalType = "__DSharp_ReturnSignal";
        while (signalType == container || module.Functions.Any(f => f.Name == signalType)) signalType += "_";
        var raiseMethod = "__DSharp_Raise";
        while (raiseMethod == container || raiseMethod == signalType || module.Functions.Any(f => f.Name == raiseMethod)) raiseMethod += "_";
        var memberNames = new HashSet<string>(module.Functions.Select(f => f.Name), StringComparer.Ordinal) { signalType, raiseMethod };
        var equalityDispatcher = "__DSharp_StructuralEquals";
        while (!memberNames.Add(equalityDispatcher)) equalityDispatcher += "_";
        var hashDispatcher = "__DSharp_StructuralHash";
        while (!memberNames.Add(hashDispatcher)) hashDispatcher += "_";
        var closureDefinitions = new List<string>();
        var closureCount = 0;
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
                if (node is BoundFunctionReference reference && functionProjections.TryGetValue(reference.Name, out var referenceSlots))
                    for (var i = 0; i < Math.Min(referenceSlots.Length, reference.TypeArguments.Count); i++)
                        if (referenceSlots[i] && GenericTypes.TryParse(reference.TypeArguments[i], out var argument)) Need(argument);
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
            !(shape.Arguments.Count == 0 && parameterNames.ContainsKey(shape.Name)) &&
            (recordTypes.ContainsKey(shape.Name) || shape.Name == module.ListSymbol ||
                shape.Name == module.MapSymbol || shape.Name == module.SetSymbol);
        bool IsTypeParameter(string type) => GenericTypes.TryParse(type, out var shape) &&
            shape.Arguments.Count == 0 && parameterNames.ContainsKey(shape.Name);
        static bool IsValueType(string type) => type is "sbyte" or "byte" or "short" or "ushort" or "int" or "uint" or "long" or "ulong" or "decimal" or "float32" or "float" or "bool" or "char" or "Unit" ||
            GenericTypes.TryParse(type, out var shape) && shape.IsTuple && !shape.IsNullable;
        string CType(string type)
        {
            if (!GenericTypes.TryParse(type, out var shape)) return type;
            string ConvertShape(TypeShape current)
            {
                if (current.Arguments.Count == 0 && current.IsNullable && nullableParameterNames.TryGetValue(current.Name, out var nullableParameter))
                    return nullableParameter;
                if (current.IsFunction)
                    return DelegateName(current.Arguments.Count - 1) + "<" + string.Join(", ", current.Arguments.Select(ConvertShape)) + ">";
                if (current.IsTuple)
                {
                    var tuple = "(" + string.Join(", ", current.Arguments.Select(ConvertShape)) + ")";
                    return current.IsNullable ? "global::System.Nullable<" + tuple + ">" : tuple;
                }
                var name = current.Arguments.Count == 0 && parameterNames.TryGetValue(current.Name, out var parameter) ? parameter :
                    current.Name == "Unit" ? unitType : current.Name == module.InvocationReceiptSymbol ? "global::DSharp.Runtime.InvocationReceipt" : current.Name == module.PublicationReceiptSymbol ? "global::DSharp.Runtime.PublicationReceipt" : current.Name == "float32" ? "float" : current.Name == "float" ? "double" : current.Name == module.ListSymbol ? listTypeName! :
                    current.Name == module.MapSymbol ? mapTypeName! : current.Name == module.SetSymbol ? setTypeName! :
                    current.Name == module.MutableListSymbol ? mutableListTypeName! : current.Name == module.MutableMapSymbol ? mutableMapTypeName! :
                    current.Name == module.TaskSymbol ? "global::System.Threading.Tasks.Task" :
                    recordTypes.TryGetValue(current.Name, out var recordType) ? recordType :
                    capabilityTypes.TryGetValue(current.Name, out var capabilityType) ? capabilityType : current.Name;
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
            // Generic and nullable values can contain a boxed float even when
            // the source itself does not name float32. Route compound values
            // through the IEEE-aware dispatcher in every module.
            if (GenericTypes.TryParse(type, out var tupleShape) && tupleShape.IsTuple)
                return container + "." + equalityDispatcher + "<" + CType(type) + ">(" + left + ", " + right + ")";
            if (type.EndsWith("?", StringComparison.Ordinal))
            {
                var inner = type[..^1];
                if (IsTypeParameter(inner) || IsValueType(inner))
                    return container + "." + equalityDispatcher + "<" + CType(type) + ">(" + left + ", " + right + ")";
                type = inner;
            }
            return type == "Unit" ? "true" :
                type == "string" ? "global::System.String.Equals(" + left + ", " + right + ", global::System.StringComparison.Ordinal)" :
                type is "float32" or "float" || IsNominal(type) || IsTypeParameter(type) ? container + "." + equalityDispatcher + "<" + CType(type) + ">(" + left + ", " + right + ")" : left + " == " + right;
        }
        output.Append("public readonly struct ").Append(unitType).Append(" {}\n\n");
        output.Append("public interface ").Append(structuralInterface).Append(" { bool __dsharp_equals(object other); }\n\n");
        if (hasKeyedCollections)
            output.Append("public interface ").Append(hashInterface).Append(" { int __dsharp_hash(); }\n\n");
        if (listTypeName is not null)
        {
            // The array is never exposed, and each construction takes its own
            // snapshot. Only typed immutable D# data can be list elements.
            var unaryDelegate = DelegateName(1);
            var binaryDelegate = DelegateName(2);
            output.Append("public sealed class ").Append(listTypeName).Append("<__DSharp_Item> : ").Append(structuralInterface).Append("\n{\n")
                .Append("    private readonly __DSharp_Item[] __items;\n")
                .Append("    private ").Append(listTypeName).Append("(__DSharp_Item[] items) { __items = (__DSharp_Item[])items.Clone(); }\n")
                .Append("    internal static ").Append(listTypeName).Append("<__DSharp_Item> __dsharp_create(__DSharp_Item[] items) => new ").Append(listTypeName).Append("<__DSharp_Item>(items);\n")
                .Append("    internal int __dsharp_length() => __items.Length;\n")
                .Append("    internal __DSharp_Item __dsharp_item(int index) => __items[index];\n")
                .Append("    internal ").Append(listTypeName).Append("<__DSharp_Item> __dsharp_append(__DSharp_Item item)\n    {\n")
                .Append("        var copy = new __DSharp_Item[checked(__items.Length + 1)];\n")
                .Append("        global::System.Array.Copy(__items, copy, __items.Length);\n")
                .Append("        copy[__items.Length] = item;\n")
                .Append("        return __dsharp_create(copy);\n    }\n");
            if (module.OptionSymbol is not null)
            {
                var optionName = recordTypes[module.OptionSymbol];
                var optionCases = module.Unions.Single(union => union.Name == module.OptionSymbol).Cases;
                var noneIndex = optionCases.ToList().FindIndex(item => item.Name == "None");
                var someIndex = optionCases.ToList().FindIndex(item => item.Name == "Some");
                if (noneIndex < 0 || someIndex < 0) throw new InvalidOperationException("Standard Option cases are missing.");
                var optionType = optionName + "<__DSharp_Item>";
                output.Append("    internal ").Append(optionType).Append(" __dsharp_get(int index) => index < 0 || index >= __items.Length ? ")
                    .Append(optionType).Append(".__dsharp_case_").Append(noneIndex.ToString(CultureInfo.InvariantCulture)).Append("() : ")
                    .Append(optionType).Append(".__dsharp_case_").Append(someIndex.ToString(CultureInfo.InvariantCulture)).Append("(__items[index]);\n");
            }
            output.Append("    internal ").Append(listTypeName).Append("<__DSharp_Result> __dsharp_map<__DSharp_Result>(")
                .Append(unaryDelegate).Append("<__DSharp_Item, __DSharp_Result> callback)\n    {\n")
                .Append("        var result = new __DSharp_Result[__items.Length];\n")
                .Append("        for (int i = 0; i < __items.Length; i++) result[i] = callback(__items[i]);\n")
                .Append("        return ").Append(listTypeName).Append("<__DSharp_Result>.__dsharp_create(result);\n    }\n")
                .Append("    internal ").Append(listTypeName).Append("<__DSharp_Item> __dsharp_filter(").Append(unaryDelegate).Append("<__DSharp_Item, bool> callback)\n    {\n")
                .Append("        var result = new global::System.Collections.Generic.List<__DSharp_Item>();\n")
                .Append("        for (int i = 0; i < __items.Length; i++) if (callback(__items[i])) result.Add(__items[i]);\n")
                .Append("        return __dsharp_create(result.ToArray());\n    }\n")
                .Append("    internal __DSharp_Acc __dsharp_fold<__DSharp_Acc>(__DSharp_Acc initial, ")
                .Append(binaryDelegate).Append("<__DSharp_Acc, __DSharp_Item, __DSharp_Acc> callback)\n    {\n")
                .Append("        var current = initial;\n")
                .Append("        for (int i = 0; i < __items.Length; i++) current = callback(current, __items[i]);\n")
                .Append("        return current;\n    }\n")
                .Append("    bool ").Append(structuralInterface).Append(".__dsharp_equals(object other)\n    {\n")
                .Append("        if (other is not ").Append(listTypeName).Append("<__DSharp_Item> value || __items.Length != value.__items.Length) return false;\n")
                .Append("        for (int i = 0; i < __items.Length; i++) if (!").Append(container).Append(".")
                .Append(equalityDispatcher).Append("<__DSharp_Item>(__items[i], value.__items[i])) return false;\n")
                .Append("        return true;\n    }\n}\n\n");
        }
        if (mapTypeName is not null)
        {
            // These arrays are built only inside the generated type and are
            // never exposed; updates copy changed storage and may share the rest.
            output.Append("public sealed class ").Append(mapTypeName).Append("<__DSharp_Key, __DSharp_Value> : ").Append(structuralInterface).Append("\n{\n")
                .Append("    private readonly __DSharp_Key[] __keys;\n    private readonly __DSharp_Value[] __values;\n    private readonly int[] __hashes;\n")
                .Append("    private ").Append(mapTypeName).Append("(__DSharp_Key[] keys, __DSharp_Value[] values, int[] hashes) { __keys = keys; __values = values; __hashes = hashes; }\n")
                .Append("    internal static ").Append(mapTypeName).Append("<__DSharp_Key, __DSharp_Value> __dsharp_empty() => new ").Append(mapTypeName)
                .Append("<__DSharp_Key, __DSharp_Value>(global::System.Array.Empty<__DSharp_Key>(), global::System.Array.Empty<__DSharp_Value>(), global::System.Array.Empty<int>());\n")
                .Append("    internal static ").Append(mapTypeName).Append("<__DSharp_Key, __DSharp_Value> __dsharp_create(__DSharp_Key[] keys, __DSharp_Value[] values, int[] hashes) => new ").Append(mapTypeName)
                .Append("<__DSharp_Key, __DSharp_Value>((__DSharp_Key[])keys.Clone(), (__DSharp_Value[])values.Clone(), (int[])hashes.Clone());\n")
                .Append("    private int __dsharp_find(__DSharp_Key key, int hash)\n    {\n")
                .Append("        for (int i = 0; i < __keys.Length; i++) if (__hashes[i] == hash && ").Append(container).Append(".")
                .Append(equalityDispatcher).Append("<__DSharp_Key>(__keys[i], key)) return i;\n        return -1;\n    }\n")
                .Append("    internal int __dsharp_count() => __keys.Length;\n")
                .Append("    internal bool __dsharp_contains(__DSharp_Key key) => __dsharp_find(key, ").Append(container).Append(".")
                .Append(hashDispatcher).Append("<__DSharp_Key>(key)) >= 0;\n")
                .Append("    internal ").Append(mapTypeName).Append("<__DSharp_Key, __DSharp_Value> __dsharp_set(__DSharp_Key key, __DSharp_Value value)\n    {\n")
                .Append("        int hash = ").Append(container).Append(".").Append(hashDispatcher).Append("<__DSharp_Key>(key);\n")
                .Append("        int index = __dsharp_find(key, hash);\n")
                .Append("        if (index >= 0) { var values = (__DSharp_Value[])__values.Clone(); values[index] = value; return new ")
                .Append(mapTypeName).Append("<__DSharp_Key, __DSharp_Value>(__keys, values, __hashes); }\n")
                .Append("        int length = checked(__keys.Length + 1);\n")
                .Append("        var keys = new __DSharp_Key[length]; var addedValues = new __DSharp_Value[length]; var hashes = new int[length];\n")
                .Append("        global::System.Array.Copy(__keys, keys, __keys.Length); global::System.Array.Copy(__values, addedValues, __values.Length); global::System.Array.Copy(__hashes, hashes, __hashes.Length);\n")
                .Append("        keys[length - 1] = key; addedValues[length - 1] = value; hashes[length - 1] = hash;\n")
                .Append("        return new ").Append(mapTypeName).Append("<__DSharp_Key, __DSharp_Value>(keys, addedValues, hashes);\n    }\n");
            if (module.OptionSymbol is not null)
            {
                var optionName = recordTypes[module.OptionSymbol];
                var optionCases = module.Unions.Single(union => union.Name == module.OptionSymbol).Cases;
                var noneIndex = optionCases.ToList().FindIndex(item => item.Name == "None");
                var someIndex = optionCases.ToList().FindIndex(item => item.Name == "Some");
                if (noneIndex < 0 || someIndex < 0) throw new InvalidOperationException("Standard Option cases are missing.");
                var optionType = optionName + "<__DSharp_Value>";
                output.Append("    internal ").Append(optionType).Append(" __dsharp_get(__DSharp_Key key)\n    {\n")
                    .Append("        int index = __dsharp_find(key, ").Append(container).Append(".").Append(hashDispatcher).Append("<__DSharp_Key>(key));\n")
                    .Append("        return index < 0 ? ").Append(optionType).Append(".__dsharp_case_").Append(noneIndex.ToString(CultureInfo.InvariantCulture))
                    .Append("() : ").Append(optionType).Append(".__dsharp_case_").Append(someIndex.ToString(CultureInfo.InvariantCulture)).Append("(__values[index]);\n    }\n");
            }
            output.Append("    bool ").Append(structuralInterface).Append(".__dsharp_equals(object other)\n    {\n")
                .Append("        if (other is not ").Append(mapTypeName).Append("<__DSharp_Key, __DSharp_Value> value || __keys.Length != value.__keys.Length) return false;\n")
                .Append("        for (int i = 0; i < __keys.Length; i++) { int index = value.__dsharp_find(__keys[i], __hashes[i]); if (index < 0 || !")
                .Append(container).Append(".").Append(equalityDispatcher).Append("<__DSharp_Value>(__values[i], value.__values[index])) return false; }\n")
                .Append("        return true;\n    }\n}\n\n");
        }
        if (setTypeName is not null)
        {
            output.Append("public sealed class ").Append(setTypeName).Append("<__DSharp_Item> : ").Append(structuralInterface).Append("\n{\n")
                .Append("    private readonly __DSharp_Item[] __items;\n    private readonly int[] __hashes;\n")
                .Append("    private ").Append(setTypeName).Append("(__DSharp_Item[] items, int[] hashes) { __items = items; __hashes = hashes; }\n")
                .Append("    internal static ").Append(setTypeName).Append("<__DSharp_Item> __dsharp_empty() => new ")
                .Append(setTypeName).Append("<__DSharp_Item>(global::System.Array.Empty<__DSharp_Item>(), global::System.Array.Empty<int>());\n")
                .Append("    private int __dsharp_find(__DSharp_Item item, int hash)\n    {\n")
                .Append("        for (int i = 0; i < __items.Length; i++) if (__hashes[i] == hash && ").Append(container).Append(".")
                .Append(equalityDispatcher).Append("<__DSharp_Item>(__items[i], item)) return i;\n        return -1;\n    }\n")
                .Append("    internal int __dsharp_count() => __items.Length;\n")
                .Append("    internal bool __dsharp_contains(__DSharp_Item item) => __dsharp_find(item, ").Append(container).Append(".")
                .Append(hashDispatcher).Append("<__DSharp_Item>(item)) >= 0;\n")
                .Append("    internal ").Append(setTypeName).Append("<__DSharp_Item> __dsharp_add(__DSharp_Item item)\n    {\n")
                .Append("        int hash = ").Append(container).Append(".").Append(hashDispatcher).Append("<__DSharp_Item>(item);\n")
                .Append("        if (__dsharp_find(item, hash) >= 0) return new ").Append(setTypeName).Append("<__DSharp_Item>(__items, __hashes);\n")
                .Append("        int length = checked(__items.Length + 1); var items = new __DSharp_Item[length]; var hashes = new int[length];\n")
                .Append("        global::System.Array.Copy(__items, items, __items.Length); global::System.Array.Copy(__hashes, hashes, __hashes.Length);\n")
                .Append("        items[length - 1] = item; hashes[length - 1] = hash; return new ").Append(setTypeName).Append("<__DSharp_Item>(items, hashes);\n    }\n");
            if (listTypeName is not null)
                output.Append("    internal static ").Append(setTypeName).Append("<__DSharp_Item> __dsharp_from(").Append(listTypeName).Append("<__DSharp_Item> source)\n    {\n")
                    .Append("        var result = __dsharp_empty(); for (int i = 0; i < source.__dsharp_length(); i++) result = result.__dsharp_add(source.__dsharp_item(i)); return result;\n    }\n");
            output.Append("    bool ").Append(structuralInterface).Append(".__dsharp_equals(object other)\n    {\n")
                .Append("        if (other is not ").Append(setTypeName).Append("<__DSharp_Item> value || __items.Length != value.__items.Length) return false;\n")
                .Append("        for (int i = 0; i < __items.Length; i++) if (value.__dsharp_find(__items[i], __hashes[i]) < 0) return false;\n")
                .Append("        return true;\n    }\n}\n\n");
        }
        if (mutableListTypeName is not null)
        {
            if (listTypeName is null) throw new InvalidOperationException("MutableList requires the immutable List runtime.");
            output.Append("internal sealed class ").Append(mutableListTypeName).Append("<__DSharp_Item>\n{\n")
                .Append("    private readonly global::System.Collections.Generic.List<__DSharp_Item> __items = new();\n")
                .Append("    internal ").Append(unitType).Append(" __dsharp_add(__DSharp_Item value) { __items.Add(value); return default; }\n")
                .Append("    internal ").Append(listTypeName).Append("<__DSharp_Item> __dsharp_freeze() => ")
                .Append(listTypeName).Append("<__DSharp_Item>.__dsharp_create(__items.ToArray());\n}\n\n");
        }
        if (mutableMapTypeName is not null)
        {
            if (mapTypeName is null) throw new InvalidOperationException("MutableMap requires the immutable Map runtime.");
            // The dictionary indexes integer hash buckets only; actual key
            // identity remains D# structural equality, including collisions.
            // Each freeze obtains fresh arrays before the immutable map owns them.
            output.Append("internal sealed class ").Append(mutableMapTypeName).Append("<__DSharp_Key, __DSharp_Value>\n{\n")
                .Append("    private readonly global::System.Collections.Generic.List<__DSharp_Key> __keys = new();\n")
                .Append("    private readonly global::System.Collections.Generic.List<__DSharp_Value> __values = new();\n")
                .Append("    private readonly global::System.Collections.Generic.List<int> __hashes = new();\n")
                .Append("    private readonly global::System.Collections.Generic.Dictionary<int, global::System.Collections.Generic.List<int>> __buckets = new();\n")
                .Append("    internal ").Append(unitType).Append(" __dsharp_set(__DSharp_Key key, __DSharp_Value value)\n    {\n")
                .Append("        int hash = ").Append(container).Append(".").Append(hashDispatcher).Append("<__DSharp_Key>(key);\n")
                .Append("        if (!__buckets.TryGetValue(hash, out var bucket)) { bucket = new(); __buckets.Add(hash, bucket); }\n")
                .Append("        foreach (int index in bucket) if (").Append(container).Append(".").Append(equalityDispatcher)
                .Append("<__DSharp_Key>(__keys[index], key)) { __values[index] = value; return default; }\n")
                .Append("        bucket.Add(__keys.Count); __keys.Add(key); __values.Add(value); __hashes.Add(hash);\n")
                .Append("        return default;\n    }\n")
                .Append("    internal ").Append(mapTypeName).Append("<__DSharp_Key, __DSharp_Value> __dsharp_freeze() => ")
                .Append(mapTypeName).Append("<__DSharp_Key, __DSharp_Value>.__dsharp_create(__keys.ToArray(), __values.ToArray(), __hashes.ToArray());\n}\n\n");
        }
        foreach (var record in module.Records)
        {
            SetTypeParameters(record.TypeParameters, nominalProjections[record.Name]);
            var recordName = recordTypes[record.Name];
            var recordType = recordName + TypeParameterList(record.TypeParameters);
            output.Append("public sealed class ").Append(recordType).Append(" : ").Append(structuralInterface);
            if (hasKeyedCollections) output.Append(", ").Append(hashInterface);
            output.Append("\n{\n");
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
            output.Append(";\n    }\n");
            if (hasKeyedCollections)
            {
                output.Append("    int ").Append(hashInterface).Append(".__dsharp_hash()\n    {\n        int hash = 17;\n");
                foreach (var field in record.Fields)
                    output.Append("        hash = unchecked(hash * 31 + ").Append(container).Append(".").Append(hashDispatcher)
                        .Append("<").Append(CType(field.Type)).Append(">(this.@").Append(field.Name).Append("));\n");
                output.Append("        return hash;\n    }\n");
            }
            output.Append("}\n\n");
        }
        foreach (var union in module.Unions)
        {
            SetTypeParameters(union.TypeParameters, nominalProjections[union.Name]);
            var unionName = recordTypes[union.Name];
            var type = unionName + TypeParameterList(union.TypeParameters);
            // C# unions discriminate by case *type*. A separate wrapper for each
            // D# case preserves case identity even when payload types coincide,
            // and holds null as a payload rather than as the union's default.
            // Reserve names globally: generated payload types may mention other
            // nominal D# types whose C# names are unqualified in this scope.
            var nativeUnion = UniqueGlobal("__dsharp_native_union");
            var variants = union.Cases.Select((_, i) =>
                UniqueGlobal("__dsharp_native_case_" + i.ToString(CultureInfo.InvariantCulture))).ToArray();
            output.Append("public sealed class ").Append(type).Append(" : ").Append(structuralInterface);
            if (hasKeyedCollections) output.Append(", ").Append(hashInterface);
            output.Append("\n{\n");
            for (var caseIndex = 0; caseIndex < union.Cases.Count; caseIndex++)
            {
                var unionCase = union.Cases[caseIndex];
                var variant = variants[caseIndex];
                output.Append("    private sealed class ").Append(variant).Append("\n    {\n");
                for (var i = 0; i < unionCase.PayloadTypes.Count; i++)
                    output.Append("        internal readonly ").Append(CType(unionCase.PayloadTypes[i])).Append(" __item_")
                        .Append(i.ToString(CultureInfo.InvariantCulture)).Append(";\n");
                output.Append("        internal ").Append(variant).Append("(");
                for (var i = 0; i < unionCase.PayloadTypes.Count; i++)
                {
                    if (i != 0) output.Append(", ");
                    output.Append(CType(unionCase.PayloadTypes[i])).Append(" __value_").Append(i.ToString(CultureInfo.InvariantCulture));
                }
                output.Append(")\n        {\n");
                for (var i = 0; i < unionCase.PayloadTypes.Count; i++)
                {
                    var index = i.ToString(CultureInfo.InvariantCulture);
                    output.Append("            __item_").Append(index).Append(" = __value_").Append(index).Append(";\n");
                }
                output.Append("        }\n    }\n");
            }
            output.Append("    private union ").Append(nativeUnion).Append("(").Append(string.Join(", ", variants)).Append(");\n")
                .Append("    private readonly ").Append(nativeUnion).Append(" __dsharp_payload;\n")
                .Append("    private ").Append(unionName).Append("(").Append(nativeUnion).Append(" payload) { __dsharp_payload = payload; }\n")
                .Append("    internal int __dsharp_tag => __dsharp_payload switch\n    {\n");
            for (var caseIndex = 0; caseIndex < union.Cases.Count; caseIndex++)
                output.Append("        ").Append(variants[caseIndex]).Append(" => ").Append(caseIndex.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            // The native struct's default is not a D# case. The only constructor
            // is private and factories always supply a wrapper; fail closed if an
            // invalid native state is nevertheless observed via foreign code.
            output.Append("        _ => throw new global::System.InvalidOperationException(\"Invalid D# union state.\")\n    };\n");
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
                output.Append(") => new ").Append(type).Append("(new ").Append(variants[caseIndex]).Append("(");
                for (var i = 0; i < unionCase.PayloadTypes.Count; i++)
                {
                    if (i != 0) output.Append(", ");
                    output.Append("__value_").Append(i.ToString(CultureInfo.InvariantCulture));
                }
                output.Append("));\n");
                for (var i = 0; i < unionCase.PayloadTypes.Count; i++)
                {
                    var index = i.ToString(CultureInfo.InvariantCulture);
                    output.Append("    internal ").Append(CType(unionCase.PayloadTypes[i])).Append(" __dsharp_case_").Append(caseSuffix)
                        .Append("_item_").Append(index).Append(" => __dsharp_payload is ").Append(variants[caseIndex])
                        .Append(" __case ? __case.__item_").Append(index)
                        .Append(" : throw new global::System.InvalidOperationException(\"Invalid D# union case.\");\n");
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
            output.Append("            default: return false;\n        }\n    }\n");
            if (hasKeyedCollections)
            {
                output.Append("    int ").Append(hashInterface).Append(".__dsharp_hash()\n    {\n")
                    .Append("        int hash = unchecked(17 * 31 + __dsharp_tag);\n")
                    .Append("        switch (__dsharp_tag)\n        {\n");
                for (var caseIndex = 0; caseIndex < union.Cases.Count; caseIndex++)
                {
                    var unionCase = union.Cases[caseIndex];
                    var caseSuffix = caseIndex.ToString(CultureInfo.InvariantCulture);
                    output.Append("            case ").Append(caseSuffix).Append(":\n");
                    for (var i = 0; i < unionCase.PayloadTypes.Count; i++)
                    {
                        var accessor = ".__dsharp_case_" + caseSuffix + "_item_" + i.ToString(CultureInfo.InvariantCulture);
                        output.Append("                hash = unchecked(hash * 31 + ").Append(container).Append(".")
                            .Append(hashDispatcher).Append("<").Append(CType(unionCase.PayloadTypes[i])).Append(">(this")
                            .Append(accessor).Append("));\n");
                    }
                    output.Append("                return hash;\n");
                }
                output.Append("            default: throw new global::System.InvalidOperationException(\"Invalid D# union state.\");\n        }\n    }\n");
            }
            output.Append("}\n\n");
        }
        // Services are execution capabilities. Their private dependency fields
        // hold exactly the values explicitly passed at construction; method
        // bodies live in the same checked function emitter as ordinary D# code.
        // Wrappers pass dependencies before method parameters and therefore
        // preserve the creating instance's dependency bindings on every call.
        SetTypeParameters(Array.Empty<string>(), Array.Empty<bool>());
        foreach (var contract in module.Interfaces)
        {
            output.Append("public interface ").Append(capabilityTypes[contract.Name]).Append("\n{\n");
            foreach (var method in contract.Methods)
            {
                output.Append(method.IsOwnedFactory ? "    internal abstract " : "    ").Append(method.IsAsync ? "global::System.Threading.Tasks.Task<" + CType(method.ReturnType) + ">" : CType(method.ReturnType))
                    .Append(" ").Append(CapabilityMethod(method.Name)).Append("(");
                for (var i = 0; i < method.ParameterTypes.Count; i++)
                {
                    if (i != 0) output.Append(", ");
                    output.Append(CType(method.ParameterTypes[i])).Append(" __dsharp_arg_").Append(i.ToString(CultureInfo.InvariantCulture));
                }
                output.Append(");\n");
            }
            output.Append("}\n\n");
        }
        foreach (var service in module.Services)
        {
            var serviceName = capabilityTypes[service.Name];
            output.Append("public sealed class ").Append(serviceName);
            if (service.Interfaces.Count > 0)
                output.Append(" : ").Append(string.Join(", ", service.Interfaces.Select(type => CType(type))));
            output.Append("\n{\n");
            for (var i = 0; i < service.DependencyTypes.Count; i++)
                output.Append("    private readonly ").Append(CType(service.DependencyTypes[i])).Append(" __dsharp_dependency_")
                    .Append(i.ToString(CultureInfo.InvariantCulture)).Append(";\n");
            output.Append("    public ").Append(serviceName).Append("(");
            for (var i = 0; i < service.DependencyTypes.Count; i++)
            {
                if (i != 0) output.Append(", ");
                output.Append(CType(service.DependencyTypes[i])).Append(" __dsharp_arg_").Append(i.ToString(CultureInfo.InvariantCulture));
            }
            output.Append(")\n    {\n");
            for (var i = 0; i < service.DependencyTypes.Count; i++)
            {
                var index = i.ToString(CultureInfo.InvariantCulture);
                output.Append("        __dsharp_dependency_").Append(index).Append(" = __dsharp_arg_").Append(index)
                    .Append(";\n        _ = __dsharp_dependency_").Append(index).Append(";\n");
            }
            output.Append("    }\n");
            foreach (var method in service.Methods)
            {
                output.Append(method.IsOwnedFactory ? "    internal " : "    public ").Append(method.IsAsync ? "global::System.Threading.Tasks.Task<" + CType(method.ReturnType) + ">" : CType(method.ReturnType))
                    .Append(" ").Append(CapabilityMethod(method.Name)).Append("(");
                var arguments = new List<string>();
                if (method.DefaultInterface is not null) arguments.Add("this");
                else for (var i = 0; i < service.DependencyTypes.Count; i++)
                    arguments.Add("__dsharp_dependency_" + i.ToString(CultureInfo.InvariantCulture));
                for (var i = 0; i < method.ParameterTypes.Count; i++)
                {
                    if (i != 0) output.Append(", ");
                    var name = "__dsharp_arg_" + i.ToString(CultureInfo.InvariantCulture);
                    output.Append(CType(method.ParameterTypes[i])).Append(" ").Append(name);
                    arguments.Add(name);
                }
                output.Append(") => ").Append(container).Append(".@").Append(method.FunctionName)
                    .Append("(").Append(string.Join(", ", arguments)).Append(");\n");
                if (method.IsOwnedFactory)
                {
                    // Internal interface members cannot be satisfied by an
                    // ordinary internal method. Emit explicit forwarding for
                    // every interface sharing this checked source signature.
                    // D# calls on a concrete service still use the internal
                    // method above, while interface calls dispatch virtually.
                    foreach (var interfaceName in service.Interfaces)
                    {
                        var contract = module.Interfaces.Single(item => item.Name == interfaceName);
                        if (!contract.Methods.Any(item => item.Name == method.Name && item.IsOwnedFactory)) continue;
                        output.Append("    ").Append(method.IsAsync ? "global::System.Threading.Tasks.Task<" + CType(method.ReturnType) + ">" : CType(method.ReturnType))
                            .Append(" ").Append(CType(interfaceName)).Append(".").Append(CapabilityMethod(method.Name)).Append("(");
                        for (var i = 0; i < method.ParameterTypes.Count; i++)
                        {
                            if (i != 0) output.Append(", ");
                            output.Append(CType(method.ParameterTypes[i])).Append(" __dsharp_arg_").Append(i.ToString(CultureInfo.InvariantCulture));
                        }
                        output.Append(") => ").Append(CapabilityMethod(method.Name)).Append("(")
                            .Append(string.Join(", ", Enumerable.Range(0, method.ParameterTypes.Count).Select(i => "__dsharp_arg_" + i.ToString(CultureInfo.InvariantCulture))))
                            .Append(");\n");
                    }
                }
            }
            output.Append("}\n\n");
        }
        if (exports is null)
            output.Append("public sealed class @").Append(module.Name).Append(" : ").Append(container)
                .Append("\n{\n    private @").Append(module.Name).Append("() {}\n}\n\n");
        else
        {
            var moduleIndex = 0;
            foreach (var group in exports.GroupBy(item => item.Module, StringComparer.Ordinal))
            {
                var className = UniqueGlobal(group.Key.Replace('.', '_'));
                var baseName = UniqueGlobal("__DSharp_ModuleExports_" + (moduleIndex++).ToString(CultureInfo.InvariantCulture));
                while (group.Any(export => export.Name == baseName)) baseName = UniqueGlobal(baseName + "_");
                output.Append("public sealed class @").Append(className).Append(" : ").Append(baseName)
                    .Append("\n{\n    private @").Append(className).Append("() {}\n}\n\npublic abstract class ")
                    .Append(baseName).Append("\n{\n");
                foreach (var export in group)
                {
                    var function = module.Functions.Single(item => item.Name == export.InternalName);
                    if (function.IsOwnedFactory) continue;
                    SetTypeParameters(function.TypeParameters, functionProjections[function.Name]);
                    output.Append("    public static ").Append(function.IsAsync ? "global::System.Threading.Tasks.Task<" + CType(function.ReturnType) + ">" : CType(function.ReturnType)).Append(" @").Append(export.Name)
                        .Append(TypeParameterList(function.TypeParameters)).Append("(");
                    var arguments = new List<string>();
                    if (function.RequiresDispatcher)
                    {
                        output.Append("global::DSharp.Runtime.IActorDispatcher __dsharp_dispatcher");
                        arguments.Add("__dsharp_dispatcher");
                    }
                    for (var i = 0; i < function.Parameters.Count; i++)
                    {
                        if (i != 0 || function.RequiresDispatcher) output.Append(", ");
                        var argument = "__arg" + i.ToString(CultureInfo.InvariantCulture);
                        arguments.Add(argument);
                        output.Append(CType(function.ParameterTypes[i])).Append(" ").Append(argument);
                    }
                    output.Append(") => ").Append(container).Append(".@").Append(function.Name)
                        .Append(TypeParameterList(function.TypeParameters)).Append("(").Append(string.Join(", ", arguments)).Append(");\n");
                }
                output.Append("}\n\n");
            }
        }
        output.Append("public abstract class ").Append(container).Append("\n{\n");
        SetTypeParameters(Array.Empty<string>(), Array.Empty<bool>());
        foreach (var route in module.ActorRoutes)
            output.Append("    private static readonly global::DSharp.Runtime.ActorRoute<").Append(CType(route.InputType)).Append(", ")
                .Append(CType(route.ResultType)).Append("> ").Append(routeNames[route.ActorIdentity]).Append(" = new(\"")
                .Append(route.Key).Append("\");\n");
        foreach (var contract in module.MessageContracts)
            output.Append("    private static readonly global::DSharp.Runtime.MessageContract<").Append(CType(contract.MessageType))
                .Append("> ").Append(contractNames[contract.MessageType]).Append(" = new(\"").Append(contract.Key).Append("\");\n");
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
        bool ContainsUsing(object? node)
        {
            if (node is null or string) return false;
            if (node is BoundUsing) return true;
            if (node is System.Collections.IEnumerable sequence)
            {
                foreach (var item in sequence) if (ContainsUsing(item)) return true;
                return false;
            }
            var type = node.GetType();
            return type.Name.StartsWith("Bound", StringComparison.Ordinal) && type.GetProperties()
                .Where(property => property.GetIndexParameters().Length == 0)
                .Any(property => ContainsUsing(property.GetValue(node)));
        }
        // Generated expression helpers form real C# function boundaries.
        // Propagate suspension through each one that contains an await; a
        // source lambda is its own boundary, whether synchronous or async.
        bool ContainsAwait(object? node)
        {
            if (node is null or string || node is BoundLambda) return false;
            if (node is BoundAwait or BoundUsing { AsyncRelease: true }) return true;
            if (node is System.Collections.IEnumerable sequence)
            {
                foreach (var item in sequence) if (ContainsAwait(item)) return true;
                return false;
            }
            var type = node.GetType();
            return type.Name.StartsWith("Bound", StringComparison.Ordinal) && type.GetProperties()
                .Where(property => property.GetIndexParameters().Length == 0)
                .Any(property => ContainsAwait(property.GetValue(node)));
        }
        string HelperResult(string type, object node) => ContainsAwait(node)
            ? "async global::System.Threading.Tasks.Task<" + CType(type) + ">" : CType(type);
        string HelperCall(string name, object node) => ContainsAwait(node) ? "(await " + name + "())" : name + "()";
        // The conservative walk can see an await in a block's unreachable
        // result, which EmitBlock correctly omits. Ensure that an async helper
        // still contains an await in the generated code in that case.
        string HelperBody(object node) => ContainsAwait(node)
            ? "()\n            {\n            await global::System.Threading.Tasks.Task.CompletedTask;\n"
            : "()\n            {\n";
        var hasUsing = module.Functions.Any(ContainsUsing);
        if (module.Functions.Any(f => f.HasReturn || NeedsLambdaReturn(f)) || hasUsing)
        {
            output.Append("    private sealed class ").Append(signalType).Append("<T> : global::System.Exception\n")
                .Append("    {\n        public T Value { get; }\n        public ").Append(signalType).Append("(T value) { Value = value; }\n    }\n")
                .Append("    private static T ").Append(raiseMethod).Append("<T>(T value) => throw new ").Append(signalType).Append("<T>(value);\n");
        }
        {
            output.Append("    internal static bool ").Append(equalityDispatcher).Append("<__DSharp_Value>(__DSharp_Value __left, __DSharp_Value __right)\n    {\n")
                .Append("        if (__left is null || __right is null) return __left is null && __right is null;\n")
                .Append("        if (__left is float __leftFloat && __right is float __rightFloat) return __leftFloat == __rightFloat;\n")
                .Append("        if (__left is double __leftDouble && __right is double __rightDouble) return __leftDouble == __rightDouble;\n")
                .Append("        if (__left is ").Append(structuralInterface).Append(" __data) return __data.__dsharp_equals(__right);\n")
                .Append("        if (__left is global::System.Runtime.CompilerServices.ITuple __leftTuple && __right is global::System.Runtime.CompilerServices.ITuple __rightTuple)\n")
                .Append("        {\n            if (__leftTuple.Length != __rightTuple.Length) return false;\n")
                .Append("            for (int __i = 0; __i < __leftTuple.Length; __i++) if (!").Append(equalityDispatcher).Append("<object>(__leftTuple[__i], __rightTuple[__i])) return false;\n")
                .Append("            return true;\n        }\n")
                .Append("        return global::System.Collections.Generic.EqualityComparer<__DSharp_Value>.Default.Equals(__left, __right);\n    }\n");
        }
        if (hasKeyedCollections)
        {
            output.Append("    internal static int ").Append(hashDispatcher).Append("<__DSharp_Value>(__DSharp_Value value)\n    {\n")
                .Append("        if (global::System.Object.ReferenceEquals(value, null)) return unchecked((int)0x9E3779B9);\n")
                .Append("        if (value is ").Append(hashInterface).Append(" data) return data.__dsharp_hash();\n")
                .Append("        if (value is ").Append(structuralInterface).Append(") throw new global::System.InvalidOperationException(\"Ineligible D# key.\");\n")
                .Append("        if (value is string text) return global::System.StringComparer.Ordinal.GetHashCode(text);\n")
                .Append("        return global::System.Collections.Generic.EqualityComparer<__DSharp_Value>.Default.GetHashCode(value);\n    }\n");
        }
        foreach (var function in module.Functions)
        {
            SetTypeParameters(function.TypeParameters, functionProjections[function.Name]);
            output.Append(function.IsOwnedFactory || function.IsActorBody ? "    internal static " : "    public static ").Append(function.IsAsync ? "async global::System.Threading.Tasks.Task<" + CType(function.ReturnType) + ">" : CType(function.ReturnType)).Append(" @").Append(function.Name)
                .Append(TypeParameterList(function.TypeParameters)).Append("(");
            if (function.RequiresDispatcher) output.Append("global::DSharp.Runtime.IActorDispatcher __dsharp_dispatcher");
            for (var i = 0; i < function.Parameters.Count; i++)
            {
                if (i != 0 || function.RequiresDispatcher) output.Append(", ");
                output.Append(CType(function.ParameterTypes.Count == 0 ? "int" : function.ParameterTypes[i])).Append(" __dsharp_param_").Append(i.ToString(CultureInfo.InvariantCulture));
            }
            output.Append(")");
            if (!function.IsAsync && function.Expression is null && function.Locals.Count == 0 && function.Body is null && !function.HasReturn)
            {
                output.Append(" => ").Append(function.Value.ToString(CultureInfo.InvariantCulture)).Append(";\n");
                continue;
            }
            output.Append("\n    {\n");
            // Keep even an async function with no source await asynchronous in
            // CLR exception/completion semantics without a CS1998 warning.
            if (function.IsAsync) output.Append("        await global::System.Threading.Tasks.Task.CompletedTask;\n");
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
            var pipeCount = 0;
            var partialCount = 0;
            var lambdaCount = 0;
            var listCount = 0;
            var forCount = 0;
            var usingCount = 0;
            var integerHelperCount = 0;
            var interpolationCount = 0;
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
                    case BoundAwait awaited: return "(await (" + EmitExpression(awaited.Operand) + "))";
                    case BoundInterpolation interpolation:
                        // Emit each hole into a typed local and format it before
                        // lowering the next hole. EmitExpression may itself add
                        // statements, so merely concatenating returned strings
                        // would allow later setup to run before earlier calls.
                        var interpolationId = (interpolationCount++).ToString(CultureInfo.InvariantCulture);
                        var interpolationHelper = "__dsharp_interpolation_" + interpolationId;
                        output.Append("            ").Append(HelperResult("string", interpolation)).Append(" ")
                            .Append(interpolationHelper).Append(HelperBody(interpolation));
                        // Round-trip formatting chooses the significant digits for
                        // the original IEEE width. DEC-01 has its own choice of
                        // fixed versus scientific notation, so reconstruct both
                        // spellings from those digits instead of relying on CLR's
                        // notation thresholds. Keep this local to interpolation so
                        // programs without floating holes emit no formatting helper.
                        var floatFormatHelper = "__dsharp_format_float_" + interpolationId;
                        if (interpolation.Parts.OfType<BoundInterpolationHole>().Any(hole => hole.Type is "float32" or "float"))
                        {
                            output.Append($$"""
            string {{floatFormatHelper}}(string candidate)
            {
                if (candidate == "NaN" || candidate == "Infinity" || candidate == "-Infinity") return candidate;
                var negative = candidate[0] == '-';
                var start = negative ? 1 : 0;
                var exponentIndex = candidate.IndexOfAny(new char[] { 'E', 'e' });
                var end = exponentIndex < 0 ? candidate.Length : exponentIndex;
                var exponent = exponentIndex < 0 ? 0 : global::System.Int32.Parse(candidate.Substring(exponentIndex + 1), global::System.Globalization.CultureInfo.InvariantCulture);
                var mantissa = candidate.Substring(start, end - start);
                var dot = mantissa.IndexOf('.');
                var point = (dot < 0 ? mantissa.Length : dot) + exponent;
                var digits = mantissa.Replace(".", "");
                var first = 0;
                while (first < digits.Length - 1 && digits[first] == '0') first++;
                point -= first;
                digits = digits.Substring(first).TrimEnd('0');
                var fixedText = point <= 0 ? "0." + new string('0', -point) + digits :
                    point >= digits.Length ? digits + new string('0', point - digits.Length) + ".0" :
                    digits.Substring(0, point) + "." + digits.Substring(point);
                var scientific = digits.Substring(0, 1) + "." + (digits.Length == 1 ? "0" : digits.Substring(1)) +
                    "e" + (point - 1).ToString(global::System.Globalization.CultureInfo.InvariantCulture);
                return (negative ? "-" : "") + (fixedText.Length <= scientific.Length ? fixedText : scientific);
            }

""");
                        }
                        var interpolationParts = new List<string>();
                        foreach (var part in interpolation.Parts)
                        {
                            if (part is BoundInterpolationText interpolationText)
                            {
                                interpolationParts.Add("new string(new char[] { " + string.Join(", ", interpolationText.Value.Select(c => "(char)0x" + ((int)c).ToString("X4", CultureInfo.InvariantCulture))) + " })");
                                continue;
                            }
                            var hole = (BoundInterpolationHole)part;
                            var holeId = interpolationParts.Count.ToString(CultureInfo.InvariantCulture);
                            var holeValue = "__dsharp_interpolation_value_" + interpolationId + "_" + holeId;
                            var holeText = "__dsharp_interpolation_text_" + interpolationId + "_" + holeId;
                            var holeExpression = EmitExpression(hole.Value);
                            output.Append("            ").Append(CType(hole.Type)).Append(" ").Append(holeValue)
                                .Append(" = ").Append(holeExpression).Append(";\n");
                            var formatted = hole.Type switch
                            {
                                "string" => holeValue,
                                "char" => "new string(" + holeValue + ", 1)",
                                "bool" => "(" + holeValue + " ? \"true\" : \"false\")",
                                "Unit" => "\"()\"",
                                // All decimal values have a scale of at most 28.
                                // Optional fractional digits retain the exact value
                                // without an exponent or trailing zeroes. Force
                                // either sign of zero to the adopted spelling.
                                "decimal" => "(" + holeValue + " == 0m ? \"0\" : " + holeValue +
                                    ".ToString(\"0.############################\", global::System.Globalization.CultureInfo.InvariantCulture))",
                                "float32" => "(" + holeValue + " == 0f ? (global::System.BitConverter.SingleToInt32Bits(" + holeValue +
                                    ") < 0 ? \"-0.0\" : \"0.0\") : " + floatFormatHelper + "(" + holeValue +
                                    ".ToString(\"R\", global::System.Globalization.CultureInfo.InvariantCulture)))",
                                "float" => "(" + holeValue + " == 0d ? (global::System.BitConverter.DoubleToInt64Bits(" + holeValue +
                                    ") < 0 ? \"-0.0\" : \"0.0\") : " + floatFormatHelper + "(" + holeValue +
                                    ".ToString(\"R\", global::System.Globalization.CultureInfo.InvariantCulture)))",
                                "sbyte" or "byte" or "short" or "ushort" or "int" or "uint" or "long" or "ulong" =>
                                    holeValue + ".ToString(global::System.Globalization.CultureInfo.InvariantCulture)",
                                _ => throw new InvalidOperationException("Unsupported bound interpolation type.")
                            };
                            if (hole.Type == "Unit") output.Append("            _ = ").Append(holeValue).Append(";\n");
                            output.Append("            string ").Append(holeText).Append(" = ").Append(formatted).Append(";\n");
                            interpolationParts.Add(holeText);
                        }
                        output.Append("            return global::System.String.Concat(new string[] { ")
                            .Append(string.Join(", ", interpolationParts)).Append(" });\n            }\n");
                        return HelperCall(interpolationHelper, interpolation);
                    case BoundTuple tuple: return "(" + string.Join(", ", tuple.Elements.Select(EmitExpression)) + ")";
                    case BoundTupleItem item: return "(" + EmitExpression(item.Tuple) + ").Item" + (item.Index + 1).ToString(CultureInfo.InvariantCulture);
                    case BoundList list:
                        if (!GenericTypes.TryParse(list.Type, out var literalShape) || literalShape.Arguments.Count != 1)
                            throw new InvalidOperationException("Invalid bound list literal type.");
                        var literalElementType = literalShape.Arguments[0].ToString();
                        var literalId = (listCount++).ToString(CultureInfo.InvariantCulture);
                        var literalHelper = "__dsharp_list_literal_" + literalId;
                        output.Append("            ").Append(HelperResult(list.Type, list)).Append(" ").Append(literalHelper).Append(HelperBody(list));
                        var elementNames = new List<string>();
                        for (var i = 0; i < list.Elements.Count; i++)
                        {
                            var value = EmitExpression(list.Elements[i]);
                            var elementName = "__dsharp_list_item_" + literalId + "_" + i.ToString(CultureInfo.InvariantCulture);
                            output.Append("            ").Append(CType(literalElementType)).Append(" ").Append(elementName).Append(" = ").Append(value).Append(";\n");
                            elementNames.Add(elementName);
                        }
                        output.Append("            return ").Append(CType(list.Type)).Append(".__dsharp_create(new ")
                            .Append(CType(literalElementType)).Append("[] { ").Append(string.Join(", ", elementNames)).Append(" });\n            }\n");
                        return HelperCall(literalHelper, list);
                    case BoundBuilderFactory builder:
                        return "new " + CType(builder.Type) + "()";
                    case BoundBuilderOperation builder:
                        // The checked receiver is a same-invocation local. C# evaluates
                        // receiver and arguments in source order, once each.
                        var builderReceiver = EmitExpression(builder.Receiver);
                        var builderArguments = builder.Arguments.Select(EmitExpression).ToArray();
                        return "(" + builderReceiver + ").__dsharp_" + builder.Operation + "(" + string.Join(", ", builderArguments) + ")";
                    case BoundCollectionMethodPartial collectionPartial:
                        // Bind the immutable receiver once. The intrinsic method
                        // itself must not run until its arguments are supplied.
                        var collectionPartialId = (listCount++).ToString(CultureInfo.InvariantCulture);
                        var collectionPartialHelper = "__dsharp_collection_partial_" + collectionPartialId;
                        var collectionPartialReceiver = "__dsharp_collection_partial_receiver_" + collectionPartialId;
                        var collectionPartialArguments = collectionPartial.ParameterTypes.Select((_, i) =>
                            "__dsharp_collection_partial_argument_" + collectionPartialId + "_" + i.ToString(CultureInfo.InvariantCulture)).ToArray();
                        output.Append("            ").Append(HelperResult(collectionPartial.Type, collectionPartial)).Append(" ")
                            .Append(collectionPartialHelper).Append(HelperBody(collectionPartial));
                        var collectionPartialSource = EmitExpression(collectionPartial.Receiver);
                        output.Append("            ").Append(CType(collectionPartial.ReceiverType)).Append(" ")
                            .Append(collectionPartialReceiver).Append(" = ").Append(collectionPartialSource).Append(";\n")
                            .Append("            return ((").Append(CType(collectionPartial.Type)).Append(")((")
                            .Append(string.Join(", ", collectionPartialArguments)).Append(") => ")
                            .Append(collectionPartialReceiver).Append(".__dsharp_").Append(collectionPartial.Operation);
                        if (collectionPartial.TypeArguments.Count > 0)
                            output.Append("<").Append(string.Join(", ", collectionPartial.TypeArguments.Select(CType))).Append(">");
                        output.Append("(").Append(string.Join(", ", collectionPartialArguments)).Append(")));\n            }\n");
                        return HelperCall(collectionPartialHelper, collectionPartial);
                    case BoundListFoldPartial foldPartial:
                        // Capture in source order. The callback and iteration run
                        // only when the returned function is invoked.
                        var foldPartialId = (listCount++).ToString(CultureInfo.InvariantCulture);
                        var foldPartialHelper = "__dsharp_fold_partial_" + foldPartialId;
                        var foldPartialReceiver = "__dsharp_fold_partial_receiver_" + foldPartialId;
                        var foldPartialInitial = "__dsharp_fold_partial_initial_" + foldPartialId;
                        var foldPartialCallback = "__dsharp_fold_partial_callback_" + foldPartialId;
                        output.Append("            ").Append(HelperResult(foldPartial.Type, foldPartial)).Append(" ")
                            .Append(foldPartialHelper).Append(HelperBody(foldPartial));
                        var foldPartialSource = EmitExpression(foldPartial.Receiver);
                        output.Append("            ").Append(CType(foldPartial.ReceiverType)).Append(" ")
                            .Append(foldPartialReceiver).Append(" = ").Append(foldPartialSource).Append(";\n");
                        var foldPartialValue = EmitExpression(foldPartial.Initial);
                        output.Append("            ").Append(CType(foldPartial.AccumulatorType)).Append(" ")
                            .Append(foldPartialInitial).Append(" = ").Append(foldPartialValue).Append(";\n")
                            .Append("            return ((").Append(CType(foldPartial.Type)).Append(")((")
                            .Append(foldPartialCallback).Append(") => ").Append(foldPartialReceiver)
                            .Append(".__dsharp_fold<").Append(CType(foldPartial.AccumulatorType)).Append(">(")
                            .Append(foldPartialInitial).Append(", ").Append(foldPartialCallback)
                            .Append(")));\n            }\n");
                        return HelperCall(foldPartialHelper, foldPartial);
                    case BoundListOperation listOperation:
                        if (!GenericTypes.TryParse(listOperation.ReceiverType, out var receiverShape) || receiverShape.Arguments.Count != 1)
                            throw new InvalidOperationException("Invalid bound list operation receiver type.");
                        var operationElementType = receiverShape.Arguments[0].ToString();
                        var operationId = (listCount++).ToString(CultureInfo.InvariantCulture);
                        var operationHelper = "__dsharp_list_operation_" + operationId;
                        var operationReceiver = "__dsharp_list_receiver_" + operationId;
                        output.Append("            ").Append(HelperResult(listOperation.Type, listOperation)).Append(" ").Append(operationHelper).Append(HelperBody(listOperation));
                        var operationValue = EmitExpression(listOperation.Receiver);
                        output.Append("            ").Append(CType(listOperation.ReceiverType)).Append(" ").Append(operationReceiver).Append(" = ").Append(operationValue).Append(";\n");
                        var operationArgumentTypes = listOperation.Operation switch
                        {
                            "length" => Array.Empty<string>(),
                            "append" => new[] { operationElementType },
                            "get" => new[] { "int" },
                            "filter" => new[] { TypeShape.FunctionName + "<" + operationElementType + ",bool>" },
                            "map" when GenericTypes.TryParse(listOperation.Type, out var mapShape) && mapShape.Arguments.Count == 1 =>
                                new[] { TypeShape.FunctionName + "<" + operationElementType + "," + mapShape.Arguments[0] + ">" },
                            "fold" => new[] { listOperation.Type, TypeShape.FunctionName + "<" + listOperation.Type + "," + operationElementType + "," + listOperation.Type + ">" },
                            _ => throw new InvalidOperationException("Unknown bound list operation.")
                        };
                        if (operationArgumentTypes.Length != listOperation.Arguments.Count)
                            throw new InvalidOperationException("Invalid bound list operation arguments.");
                        var operationArgumentNames = new List<string>();
                        for (var i = 0; i < listOperation.Arguments.Count; i++)
                        {
                            var value = EmitExpression(listOperation.Arguments[i]);
                            var argumentName = "__dsharp_list_argument_" + operationId + "_" + i.ToString(CultureInfo.InvariantCulture);
                            output.Append("            ").Append(CType(operationArgumentTypes[i])).Append(" ").Append(argumentName).Append(" = ").Append(value).Append(";\n");
                            operationArgumentNames.Add(argumentName);
                        }
                        var operationTypeArguments = listOperation.Operation switch
                        {
                            "map" => "<" + CType(GenericTypes.TryParse(listOperation.Type, out var resultShape) ? resultShape.Arguments[0].ToString() : throw new InvalidOperationException("Invalid map result.")) + ">",
                            "fold" => "<" + CType(listOperation.Type) + ">",
                            _ => ""
                        };
                        output.Append("            return ").Append(operationReceiver).Append(".__dsharp_").Append(listOperation.Operation)
                            .Append(operationTypeArguments).Append("(").Append(string.Join(", ", operationArgumentNames)).Append(");\n            }\n");
                        return HelperCall(operationHelper, listOperation);
                    case BoundMapSetFactory factory:
                        if (factory.Operation is "map.empty" or "set.empty")
                        {
                            if (factory.Arguments.Count != 0) throw new InvalidOperationException("Invalid empty collection factory.");
                            return CType(factory.Type) + ".__dsharp_empty()";
                        }
                        if (factory.Operation != "set.from" || factory.Arguments.Count != 1 || module.ListSymbol is null ||
                            !GenericTypes.TryParse(factory.Type, out var factoryShape) || factoryShape.Arguments.Count != 1)
                            throw new InvalidOperationException("Invalid bound collection factory.");
                        var factoryId = (listCount++).ToString(CultureInfo.InvariantCulture);
                        var factoryHelper = "__dsharp_collection_factory_" + factoryId;
                        var factoryArg = "__dsharp_collection_argument_" + factoryId;
                        output.Append("            ").Append(HelperResult(factory.Type, factory)).Append(" ").Append(factoryHelper).Append(HelperBody(factory));
                        var factoryValue = EmitExpression(factory.Arguments[0]);
                        output.Append("            ").Append(CType(module.ListSymbol + "<" + factoryShape.Arguments[0] + ">"))
                            .Append(" ").Append(factoryArg).Append(" = ").Append(factoryValue).Append(";\n")
                            .Append("            return ").Append(CType(factory.Type)).Append(".__dsharp_from(").Append(factoryArg).Append(");\n            }\n");
                        return HelperCall(factoryHelper, factory);
                    case BoundMapSetPartial mapPartial:
                        // Construct the function only after capturing the map and
                        // key in source order. Each completed call then performs
                        // the ordinary immutable update on that captured map.
                        var mapPartialId = (listCount++).ToString(CultureInfo.InvariantCulture);
                        var mapPartialHelper = "__dsharp_map_partial_" + mapPartialId;
                        var mapPartialReceiver = "__dsharp_map_partial_receiver_" + mapPartialId;
                        var mapPartialKey = "__dsharp_map_partial_key_" + mapPartialId;
                        var mapPartialValue = "__dsharp_map_partial_value_" + mapPartialId;
                        output.Append("            ").Append(HelperResult(mapPartial.Type, mapPartial)).Append(" ")
                            .Append(mapPartialHelper).Append(HelperBody(mapPartial));
                        var mapPartialSource = EmitExpression(mapPartial.Receiver);
                        output.Append("            ").Append(CType(mapPartial.ReceiverType)).Append(" ")
                            .Append(mapPartialReceiver).Append(" = ").Append(mapPartialSource).Append(";\n");
                        var mapPartialKeyValue = EmitExpression(mapPartial.Key);
                        output.Append("            ").Append(CType(mapPartial.KeyType)).Append(" ")
                            .Append(mapPartialKey).Append(" = ").Append(mapPartialKeyValue).Append(";\n")
                            .Append("            return ((").Append(CType(mapPartial.Type)).Append(")((")
                            .Append(mapPartialValue).Append(") => ").Append(mapPartialReceiver)
                            .Append(".__dsharp_set(").Append(mapPartialKey).Append(", ").Append(mapPartialValue)
                            .Append(")));\n            }\n");
                        return HelperCall(mapPartialHelper, mapPartial);
                    case BoundMapSetOperation collectionOperation:
                        if (!GenericTypes.TryParse(collectionOperation.ReceiverType, out var collectionShape) ||
                            (collectionShape.Name != module.MapSymbol && collectionShape.Name != module.SetSymbol))
                            throw new InvalidOperationException("Invalid bound collection operation receiver.");
                        var collectionIsMap = collectionShape.Name == module.MapSymbol;
                        if (collectionShape.Arguments.Count != (collectionIsMap ? 2 : 1))
                            throw new InvalidOperationException("Invalid bound collection type arity.");
                        var collectionArgTypes = collectionOperation.Operation switch
                        {
                            "count" => Array.Empty<string>(),
                            "get" or "contains" or "add" => new[] { collectionShape.Arguments[0].ToString() },
                            "set" when collectionIsMap => new[] { collectionShape.Arguments[0].ToString(), collectionShape.Arguments[1].ToString() },
                            _ => throw new InvalidOperationException("Unknown bound collection operation.")
                        };
                        if (collectionArgTypes.Length != collectionOperation.Arguments.Count)
                            throw new InvalidOperationException("Invalid bound collection operation arguments.");
                        var collectionId = (listCount++).ToString(CultureInfo.InvariantCulture);
                        var collectionHelper = "__dsharp_collection_operation_" + collectionId;
                        var collectionReceiver = "__dsharp_collection_receiver_" + collectionId;
                        output.Append("            ").Append(HelperResult(collectionOperation.Type, collectionOperation)).Append(" ").Append(collectionHelper).Append(HelperBody(collectionOperation));
                        var collectionValue = EmitExpression(collectionOperation.Receiver);
                        output.Append("            ").Append(CType(collectionOperation.ReceiverType)).Append(" ").Append(collectionReceiver).Append(" = ").Append(collectionValue).Append(";\n");
                        var collectionArguments = new List<string>();
                        for (var i = 0; i < collectionOperation.Arguments.Count; i++)
                        {
                            var value = EmitExpression(collectionOperation.Arguments[i]);
                            var argument = "__dsharp_collection_argument_" + collectionId + "_" + i.ToString(CultureInfo.InvariantCulture);
                            output.Append("            ").Append(CType(collectionArgTypes[i])).Append(" ").Append(argument).Append(" = ").Append(value).Append(";\n");
                            collectionArguments.Add(argument);
                        }
                        output.Append("            return ").Append(collectionReceiver).Append(".__dsharp_").Append(collectionOperation.Operation)
                            .Append("(").Append(string.Join(", ", collectionArguments)).Append(");\n            }\n");
                        return HelperCall(collectionHelper, collectionOperation);
                    case BoundNull nullable: return "default(" + CType(nullable.Type) + ")";
                    case BoundLambda lambda:
                        var lambdaId = (lambdaCount++).ToString(CultureInfo.InvariantCulture);
                        var lambdaName = "__dsharp_lambda_" + lambdaId;
                        if (lambda.Captures.Count > 0)
                        {
                            // Explicit environments avoid Roslyn's mutable
                            // display-class fields. Construct at the expression
                            // site so conditional and short-circuit behavior is
                            // preserved and each evaluation owns its snapshot.
                            var closureName = "__DSharp_Closure_" + (closureCount++).ToString(CultureInfo.InvariantCulture);
                            while (!memberNames.Add(closureName)) closureName += "_";
                            var genericArguments = TypeParameterList(function.TypeParameters);
                            var captureValues = lambda.Captures.Select(capture => localNames[capture.Name]).ToArray();
                            var outerOutput = output;
                            output = new StringBuilder();
                            output.Append("    private sealed class ").Append(closureName).Append(genericArguments).Append("\n    {\n");
                            for (var i = 0; i < lambda.Captures.Count; i++)
                                output.Append("        private readonly ").Append(CType(lambda.Captures[i].Type)).Append(" __capture_").Append(i.ToString(CultureInfo.InvariantCulture)).Append(";\n");
                            output.Append("        public ").Append(closureName).Append("(");
                            for (var i = 0; i < lambda.Captures.Count; i++)
                            {
                                if (i != 0) output.Append(", ");
                                output.Append(CType(lambda.Captures[i].Type)).Append(" value").Append(i.ToString(CultureInfo.InvariantCulture));
                            }
                            output.Append(")\n        {\n");
                            for (var i = 0; i < lambda.Captures.Count; i++)
                                output.Append("            __capture_").Append(i.ToString(CultureInfo.InvariantCulture)).Append(" = value").Append(i.ToString(CultureInfo.InvariantCulture)).Append(";\n");
                            output.Append("        }\n        public ")
                                .Append(lambda.IsAsync ? "async global::System.Threading.Tasks.Task<" + CType(lambda.ReturnType) + ">" : CType(lambda.ReturnType))
                                .Append(" Invoke(");
                            for (var i = 0; i < lambda.Parameters.Count; i++)
                            {
                                if (i != 0) output.Append(", ");
                                output.Append(CType(lambda.ParameterTypes[i])).Append(" __dsharp_lambda_param_").Append(lambdaId).Append("_").Append(i.ToString(CultureInfo.InvariantCulture));
                            }
                            output.Append(")\n        {\n");
                            var capturedNames = localNames;
                            var capturedReturnType = emitterReturnType;
                            localNames = new Dictionary<string, string>(StringComparer.Ordinal);
                            for (var i = 0; i < lambda.Captures.Count; i++)
                                localNames.Add(lambda.Captures[i].Name, "this.__capture_" + i.ToString(CultureInfo.InvariantCulture));
                            for (var i = 0; i < lambda.Parameters.Count; i++)
                                localNames.Add(lambda.Parameters[i], "__dsharp_lambda_param_" + lambdaId + "_" + i.ToString(CultureInfo.InvariantCulture));
                            emitterReturnType = lambda.ReturnType;
                            if (lambda.IsAsync) output.Append("            await global::System.Threading.Tasks.Task.CompletedTask;\n");
                            if (lambda.HasReturn) output.Append("            try\n            {\n");
                            EmitBlock(lambda.Body, lambda.ReturnType);
                            if (lambda.HasReturn)
                                output.Append("            }\n            catch (").Append(signalType).Append("<").Append(CType(lambda.ReturnType)).Append("> __dsharp_lambda_return_").Append(lambdaId).Append(")\n            {\n                return __dsharp_lambda_return_").Append(lambdaId).Append(".Value;\n            }\n");
                            output.Append("        }\n    }\n");
                            localNames = capturedNames;
                            emitterReturnType = capturedReturnType;
                            var closureDefinition = output.ToString();
                            output = outerOutput;
                            closureDefinitions.Add(closureDefinition);
                            return "((" + CType(lambda.Type) + ")new " + closureName + genericArguments + "(" + string.Join(", ", captureValues) + ").Invoke)";
                        }
                        output.Append("            static ")
                            .Append(lambda.IsAsync ? "async global::System.Threading.Tasks.Task<" + CType(lambda.ReturnType) + ">" : CType(lambda.ReturnType))
                            .Append(" ").Append(lambdaName).Append("(");
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
                        if (lambda.IsAsync) output.Append("            await global::System.Threading.Tasks.Task.CompletedTask;\n");
                        if (lambda.HasReturn) output.Append("            try\n            {\n");
                        EmitBlock(lambda.Body, lambda.ReturnType);
                        if (lambda.HasReturn)
                            output.Append("            }\n            catch (").Append(signalType).Append("<").Append(CType(lambda.ReturnType)).Append("> __dsharp_lambda_return_").Append(lambdaId).Append(")\n            {\n                return __dsharp_lambda_return_").Append(lambdaId).Append(".Value;\n            }\n");
                        output.Append("            }\n");
                        localNames = savedNames;
                        emitterReturnType = savedEmitterReturnType;
                        return "((" + CType(lambda.Type) + ")" + lambdaName + ")";
                    case BoundFunctionReference functionReference:
                        var referenceArguments = new List<string>();
                        var referenceSlots = functionProjections[functionReference.Name];
                        for (var i = 0; i < functionReference.TypeArguments.Count; i++)
                        {
                            var argument = functionReference.TypeArguments[i];
                            referenceArguments.Add(CType(argument));
                            if (referenceSlots[i] && GenericTypes.TryParse(argument, out var shape))
                                referenceArguments.Add(CType((shape with { IsNullable = true }).ToString()));
                        }
                        return "((" + CType(functionReference.Type) + ")" + container + ".@" + functionReference.Name +
                            (referenceArguments.Count == 0 ? "" : "<" + string.Join(", ", referenceArguments) + ">") + ")";
                    case BoundPartial partial:
                        var partialId = (partialCount++).ToString(CultureInfo.InvariantCulture);
                        var partialHelper = "__dsharp_partial_" + partialId;
                        var partialReceiver = "__dsharp_partial_receiver_" + partialId;
                        output.Append("            ").Append(HelperResult(partial.Type, partial)).Append(" ").Append(partialHelper).Append(HelperBody(partial));
                        var capturedReceiver = EmitExpression(partial.Receiver);
                        output.Append("            ").Append(CType(partial.ReceiverType)).Append(" ").Append(partialReceiver).Append(" = ").Append(capturedReceiver).Append(";\n");
                        var capturedArguments = new List<string>();
                        for (var i = 0; i < partial.Arguments.Count; i++)
                        {
                            var captured = EmitExpression(partial.Arguments[i]);
                            var captureName = "__dsharp_partial_arg_" + partialId + "_" + i.ToString(CultureInfo.InvariantCulture);
                            output.Append("            ").Append(CType(partial.ArgumentTypes[i])).Append(" ").Append(captureName).Append(" = ").Append(captured).Append(";\n");
                            capturedArguments.Add(captureName);
                        }
                        var remainingNames = partial.RemainingTypes.Select((_, i) => "__dsharp_partial_rest_" + partialId + "_" + i.ToString(CultureInfo.InvariantCulture)).ToArray();
                        output.Append("            return ((").Append(CType(partial.Type)).Append(")((")
                            .Append(string.Join(", ", remainingNames)).Append(") => ").Append(partialReceiver).Append(".Invoke(")
                            .Append(string.Join(", ", capturedArguments.Concat(remainingNames))).Append(")));\n            }\n");
                        return HelperCall(partialHelper, partial);
                    case BoundInvoke invocation:
                        var callable = EmitExpression(invocation.Receiver);
                        var invocationArguments = invocation.Arguments.Select(EmitExpression).ToArray();
                        return "(" + callable + ").Invoke(" + string.Join(", ", invocationArguments) + ")";
                    case BoundPipe pipe:
                        var pipeId = (pipeCount++).ToString(CultureInfo.InvariantCulture);
                        var pipeHelper = "__dsharp_pipe_" + pipeId;
                        var pipeValue = "__dsharp_pipe_value_" + pipeId;
                        output.Append("            ").Append(HelperResult(pipe.Type, pipe)).Append(" ").Append(pipeHelper).Append(HelperBody(pipe));
                        var pipedValue = EmitExpression(pipe.Value);
                        output.Append("            ").Append(CType(pipe.ValueType)).Append(" ").Append(pipeValue).Append(" = ").Append(pipedValue).Append(";\n");
                        var pipeReceiver = EmitExpression(pipe.Receiver);
                        if (GenericTypes.TryParse(pipe.ReceiverType, out var pipeShape) && pipeShape.IsFunction && pipeShape.Arguments.Count > 2)
                        {
                            var pipeCallable = "__dsharp_pipe_callable_" + pipeId;
                            var names = pipeShape.Arguments.Skip(1).SkipLast(1).Select((_, i) => "__dsharp_pipe_rest_" + pipeId + "_" + i.ToString(CultureInfo.InvariantCulture)).ToArray();
                            output.Append("            ").Append(CType(pipe.ReceiverType)).Append(" ").Append(pipeCallable).Append(" = ").Append(pipeReceiver).Append(";\n")
                                .Append("            return ((").Append(CType(pipe.Type)).Append(")((").Append(string.Join(", ", names)).Append(") => ")
                                .Append(pipeCallable).Append(".Invoke(").Append(pipeValue).Append(", ").Append(string.Join(", ", names)).Append(")));\n            }\n");
                        }
                        else output.Append("            return (").Append(pipeReceiver).Append(").Invoke(").Append(pipeValue).Append(");\n            }\n");
                        return HelperCall(pipeHelper, pipe);
                    case BoundPropagation propagation:
                        var propagationId = (propagationCount++).ToString(CultureInfo.InvariantCulture);
                        var propagationHelper = "__dsharp_propagate_" + propagationId;
                        var propagationValue = "__dsharp_propagate_value_" + propagationId;
                        output.Append("            ").Append(HelperResult(propagation.Type, propagation)).Append(" ").Append(propagationHelper).Append(HelperBody(propagation));
                        var propagationOperand = EmitExpression(propagation.Operand);
                        output.Append("            ").Append(CType(propagation.OperandType)).Append(" ").Append(propagationValue).Append(" = ").Append(propagationOperand).Append(";\n")
                            .Append("            if (").Append(propagationValue).Append(".__dsharp_tag == 1)\n")
                            .Append("                _ = ").Append(raiseMethod).Append("<").Append(CType(propagation.ReturnType)).Append(">(")
                            .Append(CType(propagation.ReturnType)).Append(".__dsharp_case_1(").Append(propagationValue).Append(".__dsharp_case_1_item_0));\n")
                            .Append("            return ").Append(propagationValue).Append(".__dsharp_case_0_item_0;\n            }\n");
                        return HelperCall(propagationHelper, propagation);
                    case BoundNullableLift lift: return "(" + CType(lift.Type) + ")(object)(" + EmitExpression(lift.Expression) + ")";
                    case BoundInt literal:
                        // C# constant overflow/division errors must instead be D#
                        // execution faults. Lift literals to ordinary int locals.
                        var name = "__dsharp_int_" + (literalCount++).ToString(CultureInfo.InvariantCulture);
                        output.Append("            int ").Append(name).Append(" = ")
                            .Append(literal.Value.ToString(CultureInfo.InvariantCulture)).Append(";\n");
                        return name;
                    case BoundInteger literal:
                        // Emit typed locals so C# cannot turn an otherwise valid
                        // D# expression into a compile-time overflow diagnostic.
                        var integerName = "__dsharp_int_" + (literalCount++).ToString(CultureInfo.InvariantCulture);
                        output.Append("            ").Append(CType(literal.Type)).Append(" ").Append(integerName)
                            .Append(" = checked((").Append(CType(literal.Type)).Append(")")
                            .Append(literal.Magnitude.ToString(CultureInfo.InvariantCulture)).Append("UL);\n");
                        return integerName;
                    case BoundDecimal literal:
                        // Construct an exact, nonconstant decimal. C# must not
                        // evaluate D# overflow or division-by-zero at compile time.
                        var decimalName = "__dsharp_decimal_" + (literalCount++).ToString(CultureInfo.InvariantCulture);
                        output.Append("            decimal ").Append(decimalName).Append(" = new global::System.Decimal(")
                            .Append("unchecked((int)").Append(literal.Low.ToString(CultureInfo.InvariantCulture)).Append("U), ")
                            .Append("unchecked((int)").Append(literal.Middle.ToString(CultureInfo.InvariantCulture)).Append("U), ")
                            .Append("unchecked((int)").Append(literal.High.ToString(CultureInfo.InvariantCulture)).Append("U), false, ")
                            .Append(literal.Scale.ToString(CultureInfo.InvariantCulture)).Append(");\n");
                        return decimalName;
                    case BoundFloat32 literal:
                        // Materialize bits at runtime: constant C# would fold IEEE arithmetic.
                        var floatName = "__dsharp_float32_" + (literalCount++).ToString(CultureInfo.InvariantCulture);
                        output.Append("            float ").Append(floatName).Append(" = global::System.BitConverter.Int32BitsToSingle(unchecked((int)")
                            .Append(literal.Bits.ToString(CultureInfo.InvariantCulture)).Append("U));\n");
                        return floatName;
                    case BoundFloat64 literal:
                        // Preserve exact binary64 bits and runtime IEEE behavior.
                        var doubleName = "__dsharp_float_" + (literalCount++).ToString(CultureInfo.InvariantCulture);
                        output.Append("            double ").Append(doubleName).Append(" = global::System.BitConverter.Int64BitsToDouble(unchecked((long)")
                            .Append(literal.Bits.ToString(CultureInfo.InvariantCulture)).Append("UL));\n");
                        return doubleName;
                    case BoundNever never:
                        var neverHelper = "__dsharp_if_" + (conditionalCount++).ToString(CultureInfo.InvariantCulture);
                        output.Append("            ").Append(HelperResult(never.Type, never)).Append(" ").Append(neverHelper).Append(HelperBody(never));
                        var neverValue = EmitExpression(never.Expression);
                        output.Append("            _ = ").Append(neverValue).Append(";\n            throw new global::System.InvalidOperationException(\"Noncompleting expression completed.\");\n            }\n");
                        return HelperCall(neverHelper, never);
                    case BoundUsing owned:
                        var usingId = (usingCount++).ToString(CultureInfo.InvariantCulture);
                        var usingHelper = "__dsharp_using_" + usingId;
                        var usingResource = "__dsharp_using_resource_" + usingId;
                        output.Append("            ").Append(HelperResult(owned.Type, owned)).Append(" ").Append(usingHelper).Append(HelperBody(owned));
                        var usingAcquisition = EmitExpression(owned.Acquisition);
                        output.Append("            ").Append(CType(owned.ResourceType)).Append(" ").Append(usingResource).Append(" = ").Append(usingAcquisition).Append(";\n")
                            .Append("            try\n            {\n");
                        localNames.Add(owned.Name, usingResource);
                        EmitBlock(owned.Body, owned.Type);
                        localNames.Remove(owned.Name);
                        output.Append("            }\n            finally\n            {\n            _ = ");
                        if (owned.AsyncRelease) output.Append("await ");
                        output.Append(usingResource).Append(".").Append(CapabilityMethod("release")).Append("();\n            }\n            }\n");
                        return HelperCall(usingHelper, owned);
                    case BoundFor loop:
                        var forId = (forCount++).ToString(CultureInfo.InvariantCulture);
                        var forHelper = "__dsharp_for_" + forId;
                        var forSource = "__dsharp_for_source_" + forId;
                        var forIndex = "__dsharp_for_index_" + forId;
                        var forItem = "__dsharp_for_item_" + forId;
                        output.Append("            ").Append(HelperResult("Unit", loop)).Append(" ").Append(forHelper).Append(HelperBody(loop));
                        var sourceValue = EmitExpression(loop.Source);
                        output.Append("            ").Append(CType(loop.SourceType)).Append(" ").Append(forSource).Append(" = ").Append(sourceValue).Append(";\n")
                            .Append("            int ").Append(forIndex).Append(" = 0;\n")
                            .Append("            while (").Append(forIndex).Append(" < ").Append(forSource)
                            .Append(".__dsharp_length())\n            {\n")
                            .Append("            ").Append(CType(loop.ElementType)).Append(" ").Append(forItem).Append(" = ").Append(forSource)
                            .Append(".__dsharp_item(").Append(forIndex).Append("++);\n")
                            .Append("            _ = ").Append(forItem).Append(";\n");
                        localNames.Add(loop.Name, forItem);
                        if (loop.Body.Statements.Count == 0) EmitLocals(loop.Body.Locals);
                        else EmitStatements(loop.Body.Statements);
                        if (loop.Body.CanComplete)
                        {
                            var forResult = EmitExpression(loop.Body.Result);
                            output.Append("            _ = ").Append(forResult).Append(";\n");
                        }
                        else
                        {
                            if (loop.Body.EvaluateResult)
                            {
                                var forNever = EmitExpression(loop.Body.Result);
                                output.Append("            _ = ").Append(forNever).Append(";\n");
                            }
                            output.Append("            throw new global::System.InvalidOperationException(\"Noncompleting loop body completed.\");\n");
                        }
                        foreach (var local in loop.Body.Locals) localNames.Remove(local.Name);
                        localNames.Remove(loop.Name);
                        output.Append("            }\n            return default(").Append(unitType).Append(");\n            }\n");
                        return HelperCall(forHelper, loop);
                    case BoundIf conditional:
                        var helper = "__dsharp_if_" + (conditionalCount++).ToString(CultureInfo.InvariantCulture);
                        output.Append("            ").Append(HelperResult(conditional.Type, conditional)).Append(" ").Append(helper).Append(HelperBody(conditional));
                        var condition = EmitExpression(conditional.Condition);
                        output.Append("            if (").Append(condition).Append(")\n            {\n");
                        EmitBlock(conditional.Then, conditional.Type);
                        output.Append("            }\n            else\n            {\n");
                        EmitBlock(conditional.Else, conditional.Type);
                        output.Append("            }\n            }\n");
                        return HelperCall(helper, conditional);
                    case BoundMatch match:
                        var matchId = (matchCount++).ToString(CultureInfo.InvariantCulture);
                        var matchHelper = "__dsharp_match_" + matchId;
                        var matchValue = "__dsharp_match_value_" + matchId;
                        output.Append("            ").Append(HelperResult(match.Type, match)).Append(" ").Append(matchHelper).Append(HelperBody(match));
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
                        return HelperCall(matchHelper, match);
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
                        output.Append("            ").Append(HelperResult(coalesce.Type, coalesce)).Append(" ").Append(coalesceHelper).Append(HelperBody(coalesce));
                        var coalesceLeft = EmitExpression(coalesce.Left);
                        output.Append("            ").Append(CType(coalesce.ContainedType + "?")).Append(" ").Append(coalesceValue).Append(" = ").Append(coalesceLeft).Append(";\n");
                        var coalescePresent = IsValueType(coalesce.ContainedType) ? coalesceValue + ".HasValue" :
                            "!global::System.Object.ReferenceEquals(" + coalesceValue + ", null)";
                        var coalescePresentValue = coalesce.Type == coalesce.ContainedType + "?"
                            ? coalesceValue : PresentValue(coalesce.ContainedType, coalesceValue);
                        output.Append("            if (").Append(coalescePresent).Append(") return ").Append(coalescePresentValue).Append(";\n");
                        var coalesceFallback = EmitExpression(coalesce.Right);
                        output.Append("            return ").Append(coalesceFallback).Append(";\n            }\n");
                        return HelperCall(coalesceHelper, coalesce);
                    case BoundConditionalCall conditionalCall:
                        var conditionalCallId = (nullableOperationCount++).ToString(CultureInfo.InvariantCulture);
                        var conditionalCallHelper = "__dsharp_nullable_" + conditionalCallId;
                        var conditionalCallValue = "__dsharp_nullable_value_" + conditionalCallId;
                        var conditionalCallPresent = "__dsharp_nullable_present_" + conditionalCallId;
                        output.Append("            ").Append(HelperResult(conditionalCall.Type, conditionalCall)).Append(" ")
                            .Append(conditionalCallHelper).Append(HelperBody(conditionalCall));
                        var conditionalCallReceiver = EmitExpression(conditionalCall.Receiver);
                        output.Append("            ").Append(CType(conditionalCall.ReceiverType + "?")).Append(" ").Append(conditionalCallValue)
                            .Append(" = ").Append(conditionalCallReceiver).Append(";\n")
                            .Append("            if (").Append(conditionalCallValue).Append(" is null) return default(")
                            .Append(CType(conditionalCall.Type)).Append(");\n")
                            .Append("            ").Append(CType(conditionalCall.ReceiverType)).Append(" ").Append(conditionalCallPresent)
                            .Append(" = ").Append(PresentValue(conditionalCall.ReceiverType, conditionalCallValue)).Append(";\n");
                        localNames.Add(conditionalCall.ReceiverName, conditionalCallPresent);
                        string conditionalCallResult;
                        try { conditionalCallResult = EmitExpression(conditionalCall.Call); }
                        finally { localNames.Remove(conditionalCall.ReceiverName); }
                        output.Append("            return ").Append(conditionalCallResult).Append(";\n            }\n");
                        return HelperCall(conditionalCallHelper, conditionalCall);
                    case BoundConditionalFieldAccess optionalField:
                        var optionalId = (nullableOperationCount++).ToString(CultureInfo.InvariantCulture);
                        var optionalHelper = "__dsharp_nullable_" + optionalId;
                        var optionalValue = "__dsharp_nullable_value_" + optionalId;
                        output.Append("            ").Append(HelperResult(optionalField.Type, optionalField)).Append(" ").Append(optionalHelper).Append(HelperBody(optionalField));
                        var optionalReceiver = EmitExpression(optionalField.Receiver);
                        var optionalResult = optionalValue + ".@" + optionalField.Field;
                        if (IsTypeParameter(optionalField.FieldType) && optionalField.Type == optionalField.FieldType + "?")
                            optionalResult = "(" + CType(optionalField.Type) + ")(object)(" + optionalResult + ")";
                        output.Append("            ").Append(CType(optionalField.ReceiverType)).Append(" ").Append(optionalValue).Append(" = ").Append(optionalReceiver).Append(";\n")
                            .Append("            if (global::System.Object.ReferenceEquals(").Append(optionalValue).Append(", null)) return default(").Append(CType(optionalField.Type)).Append(");\n")
                            .Append("            return ").Append(optionalResult).Append(";\n            }\n");
                        return HelperCall(optionalHelper, optionalField);
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
                    case BoundServiceConstruction serviceConstruction:
                        var dependencyArguments = serviceConstruction.Arguments.Select(EmitExpression).ToArray();
                        return "new " + CType(serviceConstruction.Type) + "(" + string.Join(", ", dependencyArguments) + ")";
                    case BoundServiceCall serviceCall:
                        var serviceReceiver = EmitExpression(serviceCall.Receiver);
                        var serviceArguments = serviceCall.Arguments.Select(EmitExpression).ToArray();
                        return "(" + serviceReceiver + ")." + CapabilityMethod(serviceCall.Method) + "(" + string.Join(", ", serviceArguments) + ")";
                    case BoundServiceConversion conversion:
                        return "((" + CType(conversion.Type) + ")(" + EmitExpression(conversion.Expression) + "))";
                    case BoundActorInvocation invocation:
                        return "__dsharp_dispatcher.Invoke<" + CType(invocation.InputType) + ", " + CType(invocation.ResultType) + ">(" +
                            routeNames[invocation.ActorIdentity] + ", " + EmitExpression(invocation.Message) + ")";
                    case BoundActorDispatch dispatch:
                        return "__dsharp_dispatcher.Dispatch<" + CType(dispatch.InputType) + ", " + CType(dispatch.ResultType) + ">(" +
                            routeNames[dispatch.ActorIdentity] + ", " + EmitExpression(dispatch.Message) + ")";
                    case BoundPublication publication:
                        return "__dsharp_dispatcher.Publish<" + CType(publication.MessageType) + ">(" +
                            contractNames[publication.MessageType] + ", " + EmitExpression(publication.Message) + ")";
                    case BoundCall call:
                        var args = call.Arguments.Select(EmitExpression).ToList();
                        if (module.Functions.Single(item => item.Name == call.Name).RequiresDispatcher)
                            args.Insert(0, "__dsharp_dispatcher");
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
                    case BoundUnary unary:
                        var unaryOperand = EmitExpression(unary.Operand);
                        if (unary.Operator != "-") return "(" + unary.Operator + unaryOperand + ")";
                        // C# promotes narrow values and has no unary minus for
                        // ulong. A checked subtraction retains D#'s unsigned
                        // negation rule, including zero.
                        if (unary.OperandType == "ulong") return "checked(0UL - (" + unaryOperand + "))";
                        return unary.OperandType == "int" ? "(-" + unaryOperand + ")" :
                            "checked((" + CType(unary.OperandType) + ")(-(" + unaryOperand + ")))";
                    case BoundBinary binary:
                        var left = EmitExpression(binary.Left);
                        var right = EmitExpression(binary.Right);
                        if ((binary.Operator is "==" or "!=") && (binary.OperandType.EndsWith("?", StringComparison.Ordinal) ||
                            binary.OperandType is "string" or "float32" or "float" || IsNominal(binary.OperandType) || IsTypeParameter(binary.OperandType) ||
                            GenericTypes.TryParse(binary.OperandType, out var binaryShape) && binaryShape.IsTuple))
                        {
                            var equals = EqualValue(binary.OperandType, left, right);
                            return binary.Operator == "!=" ? "(!(" + equals + "))" : equals;
                        }
                        if (binary.OperandType is "float32" or "float" && binary.Operator is "<" or "<=" or ">" or ">=")
                        {
                            // Distinct operands avoid C# self-comparison warnings
                            // while retaining IEEE NaN ordering and source order.
                            var floatComparison = "__dsharp_float_comparison_" + (integerHelperCount++).ToString(CultureInfo.InvariantCulture);
                            output.Append("            bool ").Append(floatComparison).Append("(").Append(CType(binary.OperandType)).Append(" a, ").Append(CType(binary.OperandType)).Append(" b) => a ").Append(binary.Operator).Append(" b;\n");
                            return floatComparison + "(" + left + ", " + right + ")";
                        }
                        if (binary.Operator == "%" && binary.OperandType is "sbyte" or "short")
                        {
                            // CLR promotes these operands to int before %, so
                            // explicitly preserve D#'s minimum / -1 fault.
                            var integerRemainderHelper = "__dsharp_integer_remainder_" + (integerHelperCount++).ToString(CultureInfo.InvariantCulture);
                            var type = CType(binary.OperandType);
                            output.Append("            ").Append(type).Append(" ").Append(integerRemainderHelper).Append("(").Append(type)
                                .Append(" a, ").Append(type).Append(" b) { if (a == ").Append(type)
                                .Append(".MinValue && b == -1) throw new global::System.OverflowException(); return checked((")
                                .Append(type).Append(")(a % b)); }\n");
                            return integerRemainderHelper + "(" + left + ", " + right + ")";
                        }
                        var operation = "(" + left + " " + binary.Operator + " " + right + ")";
                        return binary.Operator is "+" or "-" or "*" or "/" or "%" && binary.OperandType is "sbyte" or "byte" or "short" or "ushort"
                            ? "checked((" + CType(binary.OperandType) + ")" + operation + ")" : operation;
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
        foreach (var definition in closureDefinitions) output.Append(definition);
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
    private static bool NeedsModuleResolution(ModuleSyntax module, IReadOnlyList<Token> tokens)
    {
        if (module.Imports.Count != 0 || module.Name.Text.Contains('.')) return true;
        for (var i = 0; i + 1 < tokens.Count; i++)
            if (tokens[i].Text == module.Name.Text && tokens[i + 1].Text == ".") return true;
        return false;
    }
    public static Compilation Compile(string source, string path = "<source>")
    {
        try
        {
            var tokens = Lexer.Lex(source, path);
            var parsed = new Parser(tokens, path).Parse();
            if (!NeedsModuleResolution(parsed, tokens))
            {
                var bound = Binder.Bind(parsed, path);
                return new(CSharpEmitter.Emit(bound), []) { Actors = bound.Actors, RequiresRuntime = bound.ActorRoutes.Count > 0 || bound.MessageContracts.Count > 0 };
            }
            var resolved = MultiFileResolver.Resolve(new[] { parsed });
            var resolvedBound = Binder.Bind(resolved.Unified, path);
            return new(CSharpEmitter.Emit(resolvedBound, resolved.Exports), []) { Actors = resolvedBound.Actors, RequiresRuntime = resolvedBound.ActorRoutes.Count > 0 || resolvedBound.MessageContracts.Count > 0 };
        }
        catch (SourceError error) { return new(null, [error.Diagnostic]); }
    }
    public static Compilation Compile(IReadOnlyList<SourceFile> sources)
    {
        if (sources is null || sources.Count == 0)
            return new(null, new[] { new Diagnostic("DS1002", "<source>", new SourceSpan(0, 0, 1, 1), "Expected at least one source file.") });
        try
        {
            var tokenSets = sources.Select(source => Lexer.Lex(source.Text, source.Path)).ToArray();
            var parsed = sources.Select((source, i) => new Parser(tokenSets[i], source.Path).Parse()).ToArray();
            if (parsed.Length == 1 && !NeedsModuleResolution(parsed[0], tokenSets[0]))
            {
                var bound = Binder.Bind(parsed[0], sources[0].Path);
                return new(CSharpEmitter.Emit(bound), []) { Actors = bound.Actors, RequiresRuntime = bound.ActorRoutes.Count > 0 || bound.MessageContracts.Count > 0 };
            }
            var resolved = MultiFileResolver.Resolve(parsed);
            var resolvedBound = Binder.Bind(resolved.Unified, sources[0].Path);
            return new(CSharpEmitter.Emit(resolvedBound, resolved.Exports), []) { Actors = resolvedBound.Actors, RequiresRuntime = resolvedBound.ActorRoutes.Count > 0 || resolvedBound.MessageContracts.Count > 0 };
        }
        catch (SourceError error) { return new(null, new[] { error.Diagnostic }); }
    }
}
