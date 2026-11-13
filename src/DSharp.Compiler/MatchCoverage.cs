namespace DSharp.Compiler;

// Checks whether the unguarded rows cover every value of the scrutinee type.
// A row can contain several columns after a union case is specialized, which is
// necessary to account for combinations of nested cases and bool payloads.
internal static class MatchCoverage
{
    // Whether the candidate can match a value not already covered by previous
    // unguarded arms. Unlike whole-domain exhaustiveness, this detects a
    // redundant pattern even when prior rows cover only part of the domain.
    // If an unsupported shape or resource limit prevents proof, return true
    // conservatively and report that reachability is not definite. Inference
    // can then require an annotation instead of treating uncertainty as proof.
    public static bool IsUseful(string scrutineeType, IReadOnlyList<BoundPattern> previous,
        BoundPattern candidate, IReadOnlyList<BoundUnionDeclaration> unions, out bool definite)
    {
        var definitions = unions.ToDictionary(union => union.Name, StringComparer.Ordinal);
        var rows = previous.Select(pattern => new[] { pattern }).ToList();
        var budget = 20000;
        var certain = true;
        var result = Useful(new[] { scrutineeType }, rows, new[] { candidate }, 0);
        definite = certain;
        return result;

        bool Unknown()
        {
            certain = false;
            return true;
        }

        static bool Any(BoundPattern pattern, string type) => pattern is BoundWildcardPattern ||
            (!type.EndsWith('?') && pattern is BoundBindingPattern);

        static bool EqualLiteral(BoundLiteralPattern left, BoundLiteralPattern right)
        {
            if (left.Type != right.Type) return false;
            static int? Integer(BoundExpression value) => value switch
            {
                BoundInt number => number.Value,
                BoundUnary { Operator: "-", Operand: BoundInt number } => -number.Value,
                _ => null
            };
            if (left.Type == "int")
            {
                var a = Integer(left.Value);
                var b = Integer(right.Value);
                return a is not null && b is not null && a == b;
            }
            return object.Equals(left.Value, right.Value);
        }

        bool Useful(IReadOnlyList<string> types, List<BoundPattern[]> candidates,
            IReadOnlyList<BoundPattern> query, int depth)
        {
            if (depth > 256 || --budget < 0) return Unknown();
            if (candidates.Count == 0) return true;
            if (types.Count == 0) return false;
            if (candidates.Any(row => row.Select((pattern, index) => Any(pattern, types[index])).All(any => any)))
                return false;

            var type = types[0];
            var head = query[0];
            var tailTypes = types.Skip(1).ToArray();
            var tailQuery = query.Skip(1).ToArray();
            // If every previous row accepts this whole column, the target's
            // constraint on it cannot affect coverage of the remaining columns.
            if (candidates.All(row => Any(row[0], type)))
                return Useful(tailTypes, candidates.Select(row => row.Skip(1).ToArray()).ToList(), tailQuery, depth + 1);

            if (type.EndsWith('?'))
            {
                bool CheckNull()
                {
                    var matching = candidates.Where(row => row[0] is BoundWildcardPattern or BoundNullPattern)
                        .Select(row => row.Skip(1).ToArray()).ToList();
                    return Useful(tailTypes, matching, tailQuery, depth + 1);
                }
                bool CheckPresent(BoundPattern target)
                {
                    var matching = new List<BoundPattern[]>();
                    foreach (var row in candidates)
                    {
                        if (row[0] is BoundWildcardPattern)
                            matching.Add(new BoundPattern[] { new BoundWildcardPattern() }.Concat(row.Skip(1)).ToArray());
                        else if (row[0] is BoundPresentPattern present)
                            matching.Add(new[] { present.Inner }.Concat(row.Skip(1)).ToArray());
                    }
                    return Useful(new[] { type[..^1] }.Concat(tailTypes).ToArray(), matching,
                        new[] { target }.Concat(tailQuery).ToArray(), depth + 1);
                }
                if (head is BoundNullPattern) return CheckNull();
                if (head is BoundPresentPattern present) return CheckPresent(present.Inner);
                if (head is BoundWildcardPattern) return CheckNull() || CheckPresent(new BoundWildcardPattern());
                return Unknown();
            }

            if (type == "bool")
            {
                bool Check(bool value)
                {
                    var matching = candidates.Where(row => Any(row[0], type) ||
                        row[0] is BoundLiteralPattern { Value: BoundBool literal } && literal.Value == value)
                        .Select(row => row.Skip(1).ToArray()).ToList();
                    return Useful(tailTypes, matching, tailQuery, depth + 1);
                }
                if (Any(head, type)) return Check(false) || Check(true);
                return head is BoundLiteralPattern { Value: BoundBool literal } ? Check(literal.Value) : Unknown();
            }

            if (type == "Unit")
            {
                if (!Any(head, type) && head is not BoundLiteralPattern { Value: BoundUnit }) return Unknown();
                var matching = candidates.Where(row => Any(row[0], type) || row[0] is BoundLiteralPattern { Value: BoundUnit })
                    .Select(row => row.Skip(1).ToArray()).ToList();
                return Useful(tailTypes, matching, tailQuery, depth + 1);
            }

            if (GenericTypes.TryParse(type, out var unionType) && definitions.TryGetValue(unionType.Name, out var union) &&
                union.TypeParameters.Count == unionType.Arguments.Count)
            {
                var substitutions = new Dictionary<string, string>(StringComparer.Ordinal);
                for (var i = 0; i < union.TypeParameters.Count; i++) substitutions.Add(union.TypeParameters[i], unionType.Arguments[i].ToString());
                var targetCase = head as BoundCasePattern;
                if (!Any(head, type) && (targetCase is null || targetCase.Union != type)) return Unknown();
                var found = false;
                foreach (var unionCase in union.Cases)
                {
                    if (targetCase is not null && targetCase.Case != unionCase.Name) continue;
                    found = true;
                    var payloadTypes = new List<string>();
                    foreach (var payload in unionCase.PayloadTypes)
                    {
                        if (!GenericTypes.TrySubstitute(payload, substitutions, out var substituted)) return Unknown();
                        payloadTypes.Add(substituted);
                    }
                    if (targetCase is not null && targetCase.Arguments.Count != payloadTypes.Count) return Unknown();
                    var targetArguments = targetCase is null
                        ? payloadTypes.Select(_ => (BoundPattern)new BoundWildcardPattern()).ToArray()
                        : targetCase.Arguments.ToArray();
                    var matching = new List<BoundPattern[]>();
                    foreach (var row in candidates)
                    {
                        if (Any(row[0], type))
                            matching.Add(payloadTypes.Select(_ => (BoundPattern)new BoundWildcardPattern()).Concat(row.Skip(1)).ToArray());
                        else if (row[0] is BoundCasePattern pattern && pattern.Union == type && pattern.Case == unionCase.Name &&
                            pattern.Arguments.Count == payloadTypes.Count)
                            matching.Add(pattern.Arguments.Concat(row.Skip(1)).ToArray());
                    }
                    if (Useful(payloadTypes.Concat(tailTypes).ToArray(), matching,
                        targetArguments.Concat(tailQuery).ToArray(), depth + 1)) return true;
                }
                return found ? false : Unknown();
            }

            // For an open-domain literal target, only the same literal and
            // wildcard rows can cover it. For a wildcard target, only wildcard
            // rows can prove it except for the finite-char caveat below.
            if (head is BoundLiteralPattern wanted)
            {
                var matching = candidates.Where(row => Any(row[0], type) ||
                    row[0] is BoundLiteralPattern literal && EqualLiteral(literal, wanted))
                    .Select(row => row.Skip(1).ToArray()).ToList();
                return Useful(tailTypes, matching, tailQuery, depth + 1);
            }
            if (!Any(head, type)) return Unknown();
            var defaultUseful = Useful(tailTypes, candidates.Where(row => Any(row[0], type))
                .Select(row => row.Skip(1).ToArray()).ToList(), tailQuery, depth + 1);
            if (!defaultUseful) return false;
            // UTF-16 char is finite. If every code unit has appeared as a
            // literal, open-domain reasoning cannot prove an unmentioned value
            // exists; leave the answer uncertain rather than claiming one does.
            if (type == "char")
            {
                var seen = new HashSet<char>();
                foreach (var row in candidates)
                    if (row[0] is BoundLiteralPattern { Value: BoundText { IsChar: true } text } && text.Value.Length == 1)
                        seen.Add(text.Value[0]);
                if (seen.Count == char.MaxValue + 1) return Unknown();
            }
            return true;
        }
    }

    public static bool IsExhaustive(string scrutineeType, IReadOnlyList<BoundPattern> unguardedPatterns,
        IReadOnlyList<BoundUnionDeclaration> unions)
    {
        var definitions = unions.ToDictionary(union => union.Name, StringComparer.Ordinal);
        var rows = unguardedPatterns.Select(pattern => new BoundPattern[] { pattern }).ToList();
        return Covers(new[] { scrutineeType }, rows);

        // A binding on a nullable input narrows to its present value and must
        // never be treated as a wildcard that also covers null. The binder wraps
        // such patterns in BoundPresentPattern; keep the check type-aware too.
        static bool Any(BoundPattern pattern, string type) => pattern is BoundWildcardPattern ||
            (!type.EndsWith('?') && pattern is BoundBindingPattern);

        bool Covers(IReadOnlyList<string> types, List<BoundPattern[]> candidates)
        {
            if (candidates.Count == 0) return false;
            if (types.Count == 0) return true;
            if (candidates.Any(row => row.Select((pattern, index) => Any(pattern, types[index])).All(any => any))) return true;

            var tailTypes = types.Skip(1).ToArray();
            // Avoid expanding recursive unions when no pattern constrains this
            // column. It contributes no restriction, even for nested values.
            if (candidates.All(row => Any(row[0], types[0])))
                return Covers(tailTypes, candidates.Select(row => row.Skip(1).ToArray()).ToList());

            if (types[0].EndsWith('?'))
            {
                // `null` is one alternative; a present value is the other and
                // can itself contain union, bool, Unit or open-domain patterns.
                var nullRows = candidates.Where(row => row[0] is BoundWildcardPattern or BoundNullPattern)
                    .Select(row => row.Skip(1).ToArray()).ToList();
                if (!Covers(tailTypes, nullRows)) return false;

                var presentRows = new List<BoundPattern[]>();
                foreach (var row in candidates)
                {
                    if (row[0] is BoundWildcardPattern)
                        presentRows.Add(new BoundPattern[] { new BoundWildcardPattern() }.Concat(row.Skip(1)).ToArray());
                    else if (row[0] is BoundPresentPattern present)
                        presentRows.Add(new[] { present.Inner }.Concat(row.Skip(1)).ToArray());
                }
                return Covers(new[] { types[0][..^1] }.Concat(tailTypes).ToArray(), presentRows);
            }

            if (types[0] == "bool")
            {
                foreach (var value in new[] { false, true })
                {
                    var matching = candidates.Where(row => Any(row[0], types[0]) ||
                        row[0] is BoundLiteralPattern { Value: BoundBool literal } && literal.Value == value)
                        .Select(row => row.Skip(1).ToArray()).ToList();
                    if (!Covers(tailTypes, matching)) return false;
                }
                return true;
            }

            if (types[0] == "Unit")
            {
                var matching = candidates.Where(row => Any(row[0], types[0]) ||
                    row[0] is BoundLiteralPattern { Value: BoundUnit })
                    .Select(row => row.Skip(1).ToArray()).ToList();
                return Covers(tailTypes, matching);
            }

            if (GenericTypes.TryParse(types[0], out var unionType) &&
                definitions.TryGetValue(unionType.Name, out var union) &&
                union.TypeParameters.Count == unionType.Arguments.Count)
            {
                var substitutions = new Dictionary<string, string>(StringComparer.Ordinal);
                for (var index = 0; index < union.TypeParameters.Count; index++)
                    substitutions.Add(union.TypeParameters[index], unionType.Arguments[index].ToString());
                foreach (var unionCase in union.Cases)
                {
                    var payloadTypes = new List<string>();
                    foreach (var payload in unionCase.PayloadTypes)
                    {
                        // An invalid substitution is not evidence of coverage;
                        // the binder diagnoses it at the source type location.
                        if (!GenericTypes.TrySubstitute(payload, substitutions, out var substituted)) return false;
                        payloadTypes.Add(substituted);
                    }
                    var caseTypes = payloadTypes.Concat(tailTypes).ToArray();
                    var matching = new List<BoundPattern[]>();
                    foreach (var row in candidates)
                    {
                        if (Any(row[0], types[0]))
                        {
                            var wildcards = payloadTypes.Select(_ => (BoundPattern)new BoundWildcardPattern());
                            matching.Add(wildcards.Concat(row.Skip(1)).ToArray());
                        }
                        else if (row[0] is BoundCasePattern pattern && pattern.Union == types[0] && pattern.Case == unionCase.Name)
                            matching.Add(pattern.Arguments.Concat(row.Skip(1)).ToArray());
                    }
                    if (!Covers(caseTypes, matching)) return false;
                }
                return true;
            }

            // Other primitive values and record payloads form open domains for this
            // slice: finitely many literal rows cannot replace a catch-all.
            return Covers(tailTypes, candidates.Where(row => Any(row[0], types[0]))
                .Select(row => row.Skip(1).ToArray()).ToList());
        }
    }
}
