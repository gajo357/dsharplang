namespace DSharp.Compiler;

// Checks whether the unguarded rows cover every value of the scrutinee type.
// A row can contain several columns after a union case is specialized, which is
// necessary to account for combinations of nested cases and bool payloads.
internal static class MatchCoverage
{
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
