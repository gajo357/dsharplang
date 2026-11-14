namespace DSharp.Compiler;

// Resolve each file in its own import/lexical context before passing a single
// collision-free program to the existing binder. Names are collected first so
// forward and cross-file references do not depend on input order.
internal static class MultiFileResolver
{
    private sealed record Symbol(string Module, string Name, string Internal, string Kind, Token Location, string? Union = null);

    public static ModuleResolution Resolve(IReadOnlyList<ModuleSyntax> files)
    {
        if (files.Count == 0) throw new ArgumentException("At least one source file is required.", nameof(files));
        var modules = files.Select(f => f.Name.Text).ToHashSet(StringComparer.Ordinal);
        var symbols = new List<Symbol>();
        var serial = 0;
        var occupied = new HashSet<string>(StringComparer.Ordinal);
        void CollectNames(object? node)
        {
            if (node is null || node is string) return;
            if (node is Token token) { occupied.Add(token.Text); return; }
            if (node is System.Collections.IEnumerable list)
            {
                foreach (var item in list) CollectNames(item);
                return;
            }
            var type = node.GetType();
            if (!type.Name.EndsWith("Syntax", StringComparison.Ordinal)) return;
            foreach (var property in type.GetProperties())
                if (property.GetIndexParameters().Length == 0) CollectNames(property.GetValue(node));
        }
        foreach (var file in files) CollectNames(file);
        string Fresh()
        {
            string name;
            do name = "__dsharp_module_symbol_" + serial++.ToString(System.Globalization.CultureInfo.InvariantCulture);
            while (!occupied.Add(name));
            return name;
        }
        string Path(Token token) => token.OriginPath ?? "<source>";
        void Fail(Token token, string code, string message) => throw new SourceError(new(code, Path(token), token.Span, message));
        void Add(string module, string name, string kind, Token location, string? union = null, string? nameOverride = null)
        {
            var same = symbols.FirstOrDefault(s => s.Module == module && s.Name == name &&
                ((kind == "case" && s.Kind == "case" && s.Union == union) ||
                 (kind != "case" && s.Kind != "case" && (kind == s.Kind || kind == "record" || s.Kind == "record"))));
            if (same is not null)
                Fail(location, "DS2001", $"Duplicate declaration '{module}.{name}'; first declared at {Path(same.Location)}({same.Location.Span.Line},{same.Location.Span.Column}).");
            symbols.Add(new(module, name, nameOverride ?? Fresh(), kind, location, union));
        }
        // Stable identities across file order, important for deterministic output.
        foreach (var file in files.OrderBy(f => f.Name.Text, StringComparer.Ordinal).ThenBy(f => Path(f.Name), StringComparer.Ordinal))
        {
            foreach (var function in file.Functions) Add(file.Name.Text, function.Name.Text, "function", function.Name);
            foreach (var record in file.Records) Add(file.Name.Text, record.Name.Text, "record", record.Name);
            foreach (var union in file.Unions) Add(file.Name.Text, union.Name.Text, "union", union.Name);
        }
        foreach (var file in files.OrderBy(f => f.Name.Text, StringComparer.Ordinal).ThenBy(f => Path(f.Name), StringComparer.Ordinal))
            foreach (var union in file.Unions)
            {
                var declared = symbols.Single(s => s.Module == file.Name.Text && s.Kind == "union" && s.Name == union.Name.Text);
                foreach (var item in union.Cases) Add(file.Name.Text, item.Name.Text, "case", item.Name, declared.Internal);
            }
        var exports = symbols.Where(s => s.Kind == "function").Select(s => new ModuleExport(s.Module, s.Name, s.Internal)).ToArray();
        var functions = new List<FunctionSyntax>();
        var records = new List<RecordDeclarationSyntax>();
        var unions = new List<UnionDeclarationSyntax>();
        foreach (var file in files)
        {
            var current = file.Name.Text;
            var imports = file.Imports.Select(i => i.Text).Distinct(StringComparer.Ordinal).ToArray();
            foreach (var import in file.Imports)
                if (!modules.Contains(import.Text)) Fail(import, "DS2030", $"Unknown imported module '{import.Text}'.");
            bool Visible(string module) => module == current || imports.Contains(module, StringComparer.Ordinal);
            Symbol? Select(Token location, IEnumerable<Symbol> choices)
            {
                var candidates = choices.Distinct().ToArray();
                if (candidates.Length > 1)
                    Fail(location, "DS2031", $"Ambiguous name '{location.Text}': " + string.Join(", ", candidates.OrderBy(s => s.Module, StringComparer.Ordinal).ThenBy(s => s.Union, StringComparer.Ordinal).Select(s => $"{s.Module}.{s.Name} at {Path(s.Location)}({s.Location.Span.Line},{s.Location.Span.Column})")) + ".");
                return candidates.SingleOrDefault();
            }
            Symbol? Lookup(Token location, string name, Func<Symbol, bool> filter)
            {
                var local = symbols.Where(s => s.Module == current && s.Name == name && filter(s)).ToArray();
                if (local.Length != 0) return Select(location, local);
                return Select(location, symbols.Where(s => imports.Contains(s.Module, StringComparer.Ordinal) && s.Name == name && filter(s)));
            }
            Symbol? Qualified(Token location, string module, string name, Func<Symbol, bool> filter)
            {
                if (!Visible(module)) { Fail(location, "DS2032", $"Module '{module}' is not imported in this file."); return null; }
                return Select(location, symbols.Where(s => s.Module == module && s.Name == name && filter(s)));
            }
            Token Rename(Token token, string name) => token with { Text = name };
            Token ResolveType(Token token, IReadOnlySet<string> parameters)
            {
                if (!GenericTypes.TryParse(token.Text, out var type)) return token;
                TypeShape Visit(TypeShape shape)
                {
                    var name = shape.Name;
                    if (!shape.IsFunction && !shape.IsTuple && !parameters.Contains(name) && name is not ("int" or "bool" or "char" or "string" or "Unit"))
                    {
                        var dot = name.LastIndexOf('.');
                        var found = dot < 0 ? Lookup(token, name, s => s.Kind is "record" or "union") :
                            Qualified(token, name[..dot], name[(dot + 1)..], s => s.Kind is "record" or "union");
                        if (found is not null) name = found.Internal;
                    }
                    return shape with { Name = name, Arguments = shape.Arguments.Select(Visit).ToArray() };
                }
                return token with { Text = Visit(type).ToString() };
            }
            Token[] Types(IReadOnlyList<Token> values, IReadOnlySet<string> typeParameters) => values.Select(t => ResolveType(t, typeParameters)).ToArray();
            Symbol? ValueName(Token location) => Lookup(location, location.Text, s => s.Kind is "function" or "record" or "case");
            ExpressionSyntax CaseValue(Symbol value, Token name) => new FieldAccessSyntax(new ValueSyntax(Rename(name, value.Union!)), Rename(name, value.Internal));
            ExpressionSyntax CaseCall(Symbol value, Token name, Token open, IReadOnlyList<ExpressionSyntax> args, IReadOnlyList<Token> typeArgs) =>
                new MemberCallSyntax(new ValueSyntax(Rename(name, value.Union!)), Rename(name, value.Internal), open, args) { TypeArguments = typeArgs };
            Token CaseName(Symbol union, Token name)
            {
                var item = symbols.FirstOrDefault(s => s.Kind == "case" && s.Union == union.Internal && s.Name == name.Text);
                return item is null ? name : Rename(name, item.Internal);
            }
            bool PathParts(ExpressionSyntax expression, out List<Token> parts)
            {
                parts = new List<Token>();
                var buffer = new List<Token>();
                bool Flatten(ExpressionSyntax item)
                {
                    if (item is ValueSyntax value && value.Value.Text.Length > 0 && (char.IsAsciiLetter(value.Value.Text[0]) || value.Value.Text[0] == '_')) { buffer.Add(value.Value); return true; }
                    if (item is FieldAccessSyntax access && Flatten(access.Receiver)) { buffer.Add(access.Field); return true; }
                    return false;
                }
                if (!Flatten(expression)) return false;
                parts = buffer; return true;
            }
            Symbol? ResolvePath(IReadOnlyList<Token> parts, IReadOnlySet<string> lexical, Func<Symbol, bool> filter)
            {
                if (parts.Count < 2 || lexical.Contains(parts[0].Text)) return null;
                var module = string.Join(".", parts.Take(parts.Count - 1).Select(p => p.Text));
                // Recognize only exact module prefixes; normal value/member lookup
                // takes precedence and unknown paths are left for binder diagnostics.
                if (!modules.Contains(module)) return null;
                return Qualified(parts[^1], module, parts[^1].Text, filter);
            }
            PatternSyntax Pattern(PatternSyntax pattern, HashSet<string> scope, IReadOnlySet<string> typeParameters)
            {
                if (pattern is NamePatternSyntax name)
                {
                    if (scope.Contains(name.Name.Text)) return pattern;
                    var found = ValueName(name.Name);
                    if (found?.Kind == "case") return new CasePatternSyntax(Rename(name.Name, found.Union!), Rename(name.Name, found.Internal), Array.Empty<PatternSyntax>());
                    scope.Add(name.Name.Text); return pattern;
                }
                if (pattern is CasePatternSyntax c)
                {
                    Token? qualifier = c.Qualifier;
                    var caseName = c.Name;
                    if (qualifier is null)
                    {
                        var found = ValueName(c.Name);
                        if (found?.Kind == "case") { qualifier = Rename(c.Name, found.Union!); caseName = Rename(c.Name, found.Internal); }
                    }
                    else
                    {
                        var firstPart = qualifier.Text.Split('.')[0];
                        if (scope.Contains(firstPart) || typeParameters.Contains(firstPart))
                            return c with { Arguments = c.Arguments.Select(a => Pattern(a, scope, typeParameters)).ToArray() };
                        var dot = qualifier.Text.LastIndexOf('.');
                        var found = dot < 0 ? Lookup(qualifier, qualifier.Text, s => s.Kind == "union") : Qualified(qualifier, qualifier.Text[..dot], qualifier.Text[(dot + 1)..], s => s.Kind == "union");
                        if (found is not null) { qualifier = Rename(qualifier, found.Internal); caseName = CaseName(found, c.Name); }
                    }
                    return c with { Qualifier = qualifier, Name = caseName, Arguments = c.Arguments.Select(a => Pattern(a, scope, typeParameters)).ToArray() };
                }
                return pattern;
            }
            BlockSyntax Block(BlockSyntax block, HashSet<string> outer, IReadOnlySet<string> typeParameters)
            {
                var scope = new HashSet<string>(outer, StringComparer.Ordinal);
                var statements = new List<StatementSyntax>(); var bindings = new List<LetBindingSyntax>();
                foreach (var statement in block.Statements)
                {
                    switch (statement)
                    {
                        case LetStatementSyntax let:
                            var binding = let.Binding with { Annotation = let.Binding.Annotation is null ? null : ResolveType(let.Binding.Annotation, typeParameters), Expression = Expression(let.Binding.Expression ?? new ValueSyntax(let.Binding.Initializer), scope, typeParameters) };
                            bindings.Add(binding); statements.Add(new LetStatementSyntax(binding)); scope.Add(binding.Name.Text); break;
                        case TupleLetStatementSyntax tuple:
                            statements.Add(tuple with { Annotation = tuple.Annotation is null ? null : ResolveType(tuple.Annotation, typeParameters), Value = Expression(tuple.Value, scope, typeParameters) });
                            foreach (var name in tuple.Pattern.Names) scope.Add(name.Text); break;
                        case ReturnStatementSyntax returned: statements.Add(returned with { Value = Expression(returned.Value, scope, typeParameters) }); break;
                        case ExpressionStatementSyntax expression: statements.Add(expression with { Expression = Expression(expression.Expression, scope, typeParameters) }); break;
                    }
                }
                return new BlockSyntax(bindings, Expression(block.Result, scope, typeParameters)) { Statements = statements };
            }
            ExpressionSyntax Expression(ExpressionSyntax expression, HashSet<string> scope, IReadOnlySet<string> typeParameters)
            {
                switch (expression)
                {
                    case ValueSyntax value:
                        if (scope.Contains(value.Value.Text)) return value;
                        var foundValue = ValueName(value.Value);
                        return foundValue?.Kind switch { "function" => new ValueSyntax(Rename(value.Value, foundValue.Internal)), "case" => CaseValue(foundValue, value.Value), _ => value };
                    case CallSyntax call:
                        var callArguments = call.Arguments.Select(a => Expression(a, scope, typeParameters)).ToArray();
                        var callTypes = Types(call.TypeArguments, typeParameters);
                        if (scope.Contains(call.Name.Text)) return call with { Arguments = callArguments, TypeArguments = callTypes };
                        var foundCall = ValueName(call.Name);
                        // A visible nominal type shadows a standard builder
                        // constructor. Preserve its identity so the binder
                        // cannot mistake a user record or union for the
                        // intrinsic merely because their source names agree.
                        if ((call.Name.Text is "MutableList" or "MutableMap") && !typeParameters.Contains(call.Name.Text))
                        {
                            var builderType = foundCall?.Kind == "record" ? foundCall :
                                foundCall is null ? Lookup(call.Name, call.Name.Text, s => s.Kind == "union") : null;
                            if (builderType is not null)
                                return call with { Name = Rename(call.Name, builderType.Internal), Arguments = callArguments, TypeArguments = callTypes };
                        }
                        return foundCall?.Kind switch { "function" => call with { Name = Rename(call.Name, foundCall.Internal), Arguments = callArguments, TypeArguments = callTypes }, "case" => CaseCall(foundCall, call.Name, call.OpenParen, callArguments, callTypes), _ => call with { Arguments = callArguments, TypeArguments = callTypes } };
                    case MemberCallSyntax member:
                        var memberArgs = member.Arguments.Select(a => Expression(a, scope, typeParameters)).ToArray();
                        var memberTypes = Types(member.TypeArguments, typeParameters);
                        if (PathParts(member.Receiver, out var prefix))
                        {
                            var path = prefix.Append(member.Member).ToArray();
                            // Module qualification selects a module function;
                            // a case uses its declaring union as an additional
                            // qualifier, even when it shares the function name.
                            var qualified = ResolvePath(path, scope, s => s.Kind == "function");
                            if (qualified?.Kind == "function") return new CallSyntax(Rename(member.Member, qualified.Internal), member.OpenParen, memberArgs) { TypeArguments = memberTypes };
                            if (prefix.Count >= 2 && !scope.Contains(prefix[0].Text))
                            {
                                var module = string.Join(".", prefix.Take(prefix.Count - 1).Select(p => p.Text));
                                if (modules.Contains(module))
                                {
                                    var union = Qualified(prefix[^1], module, prefix[^1].Text, s => s.Kind == "union");
                                    if (union is not null) return member with { Receiver = new ValueSyntax(Rename(prefix[^1], union.Internal)), Member = CaseName(union, member.Member), Arguments = memberArgs, TypeArguments = memberTypes };
                                }
                            }
                            if (prefix.Count == 1 && !scope.Contains(prefix[0].Text))
                            {
                                // A type parameter shadows both the standard
                                // collection qualifier and visible nominal types.
                                if ((prefix[0].Text is "Map" or "Set") && typeParameters.Contains(prefix[0].Text))
                                    return member with { Arguments = memberArgs, TypeArguments = memberTypes };
                                var union = Lookup(prefix[0], prefix[0].Text, s => s.Kind == "union");
                                if (union is not null) return member with { Receiver = new ValueSyntax(Rename(prefix[0], union.Internal)), Member = CaseName(union, member.Member), Arguments = memberArgs, TypeArguments = memberTypes };
                                // A visible source record also shadows an intrinsic
                                // factory qualifier; preserve its identity so the
                                // binder diagnoses a non-callable member instead of
                                // silently selecting standard Map or Set.
                                if ((prefix[0].Text is "Map" or "Set") && !typeParameters.Contains(prefix[0].Text))
                                {
                                    var record = ValueName(prefix[0]);
                                    if (record?.Kind == "record") return member with { Receiver = new ValueSyntax(Rename(prefix[0], record.Internal)), Arguments = memberArgs, TypeArguments = memberTypes };
                                }
                            }
                        }
                        return member with { Receiver = Expression(member.Receiver, scope, typeParameters), Arguments = memberArgs, TypeArguments = memberTypes };
                    case FieldAccessSyntax field:
                        if (PathParts(field, out var fieldPath))
                        {
                            var qualified = ResolvePath(fieldPath, scope, s => s.Kind == "function");
                            if (qualified?.Kind == "function") return new ValueSyntax(Rename(field.Field, qualified.Internal));
                            if (fieldPath.Count >= 3 && !scope.Contains(fieldPath[0].Text))
                            {
                                var module = string.Join(".", fieldPath.Take(fieldPath.Count - 2).Select(p => p.Text));
                                if (modules.Contains(module))
                                {
                                    var union = Qualified(fieldPath[^2], module, fieldPath[^2].Text, s => s.Kind == "union");
                                    if (union is not null) return field with { Receiver = new ValueSyntax(Rename(fieldPath[^2], union.Internal)), Field = CaseName(union, field.Field) };
                                }
                            }
                            if (field.Receiver is ValueSyntax qualifier && !scope.Contains(qualifier.Value.Text))
                            {
                                var union = Lookup(qualifier.Value, qualifier.Value.Text, s => s.Kind == "union");
                                if (union is not null) return field with { Receiver = new ValueSyntax(Rename(qualifier.Value, union.Internal)), Field = CaseName(union, field.Field) };
                            }
                        }
                        return field with { Receiver = Expression(field.Receiver, scope, typeParameters) };
                    case RecordConstructionSyntax record:
                        var recordType = Lookup(record.Name, record.Name.Text, s => s.Kind == "record");
                        return record with { Name = recordType is null ? record.Name : Rename(record.Name, recordType.Internal), Fields = record.Fields.Select(f => f with { Value = Expression(f.Value, scope, typeParameters) }).ToArray() };
                    case QualifiedRecordConstructionSyntax record:
                        if (!PathParts(record.Receiver, out var recordPrefix) || scope.Contains(recordPrefix[0].Text))
                            Fail(record.Name, "DS2032", "Record construction requires a visible imported module.");
                        var recordModule = string.Join(".", recordPrefix.Select(p => p.Text));
                        var qualifiedRecord = Qualified(record.Name, recordModule, record.Name.Text, s => s.Kind == "record");
                        if (qualifiedRecord is null) Fail(record.Name, "DS2030", "Unknown record type in imported module.");
                        return new RecordConstructionSyntax(Rename(record.Name, qualifiedRecord!.Internal), record.Fields.Select(f => f with { Value = Expression(f.Value, scope, typeParameters) }).ToArray());
                    case GroupSyntax group: return group with { Inner = Expression(group.Inner, scope, typeParameters) };
                    case TupleSyntax tuple: return tuple with { Elements = tuple.Elements.Select(a => Expression(a, scope, typeParameters)).ToArray() };
                    case ListSyntax list: return list with { Elements = list.Elements.Select(a => Expression(a, scope, typeParameters)).ToArray() };
                    case InvokeSyntax invocation: return invocation with { Receiver = Expression(invocation.Receiver, scope, typeParameters), Arguments = invocation.Arguments.Select(a => Expression(a, scope, typeParameters)).ToArray() };
                    case UnarySyntax unary: return unary with { Operand = Expression(unary.Operand, scope, typeParameters) };
                    case AwaitSyntax awaited: return awaited with { Operand = Expression(awaited.Operand, scope, typeParameters) };
                    case BinarySyntax binary: return binary with { Left = Expression(binary.Left, scope, typeParameters), Right = Expression(binary.Right, scope, typeParameters) };
                    case CoalesceSyntax coalesce: return coalesce with { Left = Expression(coalesce.Left, scope, typeParameters), Right = Expression(coalesce.Right, scope, typeParameters) };
                    case PipeSyntax pipe: return pipe with { Left = Expression(pipe.Left, scope, typeParameters), Right = Expression(pipe.Right, scope, typeParameters) };
                    case PropagateSyntax propagation: return propagation with { Operand = Expression(propagation.Operand, scope, typeParameters) };
                    case ConditionalFieldAccessSyntax optional: return optional with { Receiver = Expression(optional.Receiver, scope, typeParameters) };
                    case RecordUpdateSyntax update: return update with { Receiver = Expression(update.Receiver, scope, typeParameters), Fields = update.Fields.Select(f => f with { Value = Expression(f.Value, scope, typeParameters) }).ToArray() };
                    case IfSyntax conditional: return conditional with { Condition = Expression(conditional.Condition, scope, typeParameters), Then = Block(conditional.Then, scope, typeParameters), Else = Block(conditional.Else, scope, typeParameters) };
                    case ForSyntax loop:
                        var loopScope = new HashSet<string>(scope, StringComparer.Ordinal) { loop.Name.Text };
                        return loop with { Source = Expression(loop.Source, scope, typeParameters), Body = Block(loop.Body, loopScope, typeParameters) };
                    case LambdaSyntax lambda:
                        var lambdaScope = new HashSet<string>(scope, StringComparer.Ordinal);
                        foreach (var p in lambda.Parameters) lambdaScope.Add(p.Name.Text);
                        return lambda with { Parameters = lambda.Parameters.Select(p => p with { Type = p.Type is null ? null : ResolveType(p.Type, typeParameters) }).ToArray(), Body = Block(lambda.Body, lambdaScope, typeParameters) };
                    case MatchSyntax match:
                        return match with { Scrutinee = Expression(match.Scrutinee, scope, typeParameters), Arms = match.Arms.Select(arm => { var inner = new HashSet<string>(scope, StringComparer.Ordinal); var pattern = Pattern(arm.Pattern, inner, typeParameters); return arm with { Pattern = pattern, Guard = arm.Guard is null ? null : Expression(arm.Guard, inner, typeParameters), Value = Expression(arm.Value, inner, typeParameters) }; }).ToArray() };
                    default: return expression;
                }
            }
            foreach (var record in file.Records)
            {
                var symbol = symbols.Single(s => s.Module == current && s.Kind == "record" && s.Name == record.Name.Text);
                var parameters = record.TypeParameters.Select(t => t.Text).ToHashSet(StringComparer.Ordinal);
                records.Add(record with { Name = Rename(record.Name, symbol.Internal), Fields = record.Fields.Select(f => f with { Type = ResolveType(f.Type, parameters) }).ToArray() });
            }
            foreach (var union in file.Unions)
            {
                var symbol = symbols.Single(s => s.Module == current && s.Kind == "union" && s.Name == union.Name.Text);
                var parameters = union.TypeParameters.Select(t => t.Text).ToHashSet(StringComparer.Ordinal);
                unions.Add(union with { Name = Rename(union.Name, symbol.Internal), Cases = union.Cases.Select(c => c with { Name = CaseName(symbol, c.Name), PayloadTypes = Types(c.PayloadTypes, parameters) }).ToArray() });
            }
            foreach (var function in file.Functions)
            {
                var symbol = symbols.Single(s => s.Module == current && s.Kind == "function" && s.Name == function.Name.Text);
                var parameters = function.TypeParameters.Select(t => t.Text).ToHashSet(StringComparer.Ordinal);
                var scope = function.Parameters.Select(p => p.Name.Text).ToHashSet(StringComparer.Ordinal);
                var body = function.Body is null ? null : Block(function.Body, scope, parameters);
                functions.Add(function with { Name = Rename(function.Name, symbol.Internal), ReturnType = ResolveType(function.ReturnType, parameters), Parameters = function.Parameters.Select(p => p with { Type = ResolveType(p.Type, parameters) }).ToArray(), Body = body, Expression = body?.Result ?? (function.Expression is null ? null : Expression(function.Expression, scope, parameters)), Bindings = body?.Bindings ?? function.Bindings });
            }
        }
        return new ModuleResolution(new ModuleSyntax(files[0].Name, functions) { Records = records, Unions = unions }, exports);
    }
}
