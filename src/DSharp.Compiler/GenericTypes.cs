namespace DSharp.Compiler;

// Internal structural view of the compiler's canonical type strings. Public
// syntax and bound model constructors continue to expose their existing strings.
internal sealed record TypeShape(string Name, IReadOnlyList<TypeShape> Arguments, bool IsNullable)
{
    public override string ToString() => Name +
        (Arguments.Count == 0 ? "" : "<" + string.Join(",", Arguments) + ">") +
        (IsNullable ? "?" : "");
}

internal static class GenericTypes
{
    // Parse a canonical supported nominal type. A syntactically valid tuple or
    // function type may still be rejected here until those features are added.
    public static bool TryParse(string text, out TypeShape shape)
    {
        var position = 0;
        TypeShape? Parse()
        {
            if (position >= text.Length) return null;
            var start = position;
            if (text[position] == '$')
            {
                // The source lexer rejects '$'; these identifiers exist only
                // inside the binder to distinguish lexical type parameters.
                position++;
                var digitStart = position;
                while (position < text.Length && char.IsAsciiDigit(text[position])) position++;
                if (position == digitStart) return null;
            }
            else
            {
                if (!(char.IsAsciiLetter(text[position]) || text[position] == '_')) return null;
                position++;
                while (position < text.Length)
                {
                    if (char.IsAsciiLetterOrDigit(text[position]) || text[position] == '_') { position++; continue; }
                    if (text[position] == '.' && position + 1 < text.Length &&
                        (char.IsAsciiLetter(text[position + 1]) || text[position + 1] == '_')) { position += 2; continue; }
                    break;
                }
            }
            var name = text[start..position];
            var arguments = new List<TypeShape>();
            if (position < text.Length && text[position] == '<')
            {
                position++;
                while (true)
                {
                    var argument = Parse();
                    if (argument is null) return null;
                    arguments.Add(argument);
                    if (position >= text.Length) return null;
                    if (text[position] == '>') { position++; break; }
                    if (text[position++] != ',') return null;
                }
            }
            var nullable = position < text.Length && text[position] == '?';
            if (nullable) position++;
            return new TypeShape(name, arguments, nullable);
        }
        var parsed = Parse();
        shape = parsed!;
        return parsed is not null && position == text.Length;
    }

    // Validate recursively against declared nominal arities and the current
    // lexical set of open type-parameter names (which cannot take arguments).
    public static bool TryValidate(string text, IReadOnlyDictionary<string, int> arities,
        IReadOnlySet<string> parameters)
    {
        if (!TryParse(text, out var shape)) return false;
        bool Valid(TypeShape value)
        {
            if (parameters.Contains(value.Name)) return value.Arguments.Count == 0;
            return arities.TryGetValue(value.Name, out var arity) && arity == value.Arguments.Count &&
                value.Arguments.All(Valid);
        }
        return Valid(shape);
    }

    // Substitute one lexical parameter scope. Nullability is idempotent:
    // substituting an already-nullable type for T in T? retains one nullable
    // layer. This does not make repeated nullable suffixes valid source syntax.
    public static bool TrySubstitute(string text, IReadOnlyDictionary<string, string> substitutions, out string result)
    {
        result = "";
        if (!TryParse(text, out var shape)) return false;
        TypeShape? Replace(TypeShape value)
        {
            if (value.Arguments.Count == 0 && substitutions.TryGetValue(value.Name, out var replacement))
            {
                if (!TryParse(replacement, out var resolved)) return null;
                return resolved with { IsNullable = value.IsNullable || resolved.IsNullable };
            }
            var arguments = new List<TypeShape>();
            foreach (var argument in value.Arguments)
            {
                var replaced = Replace(argument);
                if (replaced is null) return null;
                arguments.Add(replaced);
            }
            return value with { Arguments = arguments };
        }
        var replaced = Replace(shape);
        if (replaced is null) return false;
        result = replaced.ToString();
        return true;
    }

    public static string Substitute(string text, IReadOnlyDictionary<string, string> substitutions) =>
        TrySubstitute(text, substitutions, out var result) ? result :
            throw new ArgumentException("Invalid type or nullable substitution.", nameof(text));

    // Infer parameters from one argument without partially mutating the caller's
    // substitution map on failure. Null and noncompleting expressions provide
    // no evidence. Callers must recheck all arguments after inference: a nullable
    // join can invalidate a previously encountered invariant type argument.
    public static bool TryUnify(string pattern, string actual, IReadOnlySet<string> parameters,
        IDictionary<string, string> substitutions, IReadOnlySet<string>? fixedParameters = null)
    {
        if (!TryParse(pattern, out var formal)) return false;
        if (actual is "<null>" or "<never>") return true;
        if (!TryParse(actual, out var given)) return false;
        var pending = new Dictionary<string, string>(substitutions, StringComparer.Ordinal);
        bool Unify(TypeShape expected, TypeShape received, bool allowLift)
        {
            if (parameters.Contains(expected.Name) && expected.Arguments.Count == 0)
            {
                if (expected.IsNullable)
                {
                    // nullable(T) is idempotent, so a nullable argument alone
                    // does not distinguish T=int from T=int?. Defer binding T
                    // until another argument, an explicit type argument, or the
                    // result context provides unambiguous evidence. The caller
                    // revalidates all arguments after inference.
                    if (!pending.TryGetValue(expected.Name, out var priorNullable))
                        return allowLift || received.IsNullable;
                    if (!TryParse(priorNullable, out var priorShape)) return false;
                    var required = (priorShape with { IsNullable = true }).ToString();
                    return required == received.ToString() ||
                        (allowLift && !received.IsNullable && required == (received with { IsNullable = true }).ToString());
                }
                var name = received.ToString();
                if (pending.TryGetValue(expected.Name, out var prior))
                {
                    if (prior == name) return true;
                    if (fixedParameters?.Contains(expected.Name) == true)
                        return allowLift && prior == name + "?";
                    if (name == prior + "?") { pending[expected.Name] = name; return true; }
                    return allowLift && prior == name + "?";
                }
                pending.Add(expected.Name, name);
                return true;
            }
            if ((!expected.IsNullable && received.IsNullable) ||
                (expected.IsNullable && !received.IsNullable && !allowLift) || expected.Name != received.Name ||
                expected.Arguments.Count != received.Arguments.Count) return false;
            for (var i = 0; i < expected.Arguments.Count; i++)
                if (!Unify(expected.Arguments[i], received.Arguments[i], false)) return false;
            return true;
        }
        if (!Unify(formal, given, true)) return false;
        foreach (var pair in pending) substitutions[pair.Key] = pair.Value;
        return true;
    }
}
