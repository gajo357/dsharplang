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

        static bool Any(BoundPattern pattern) => pattern is BoundWildcardPattern or BoundBindingPattern;

        bool Covers(IReadOnlyList<string> types, List<BoundPattern[]> candidates)
        {
            if (candidates.Count == 0) return false;
            if (types.Count == 0) return true;
            if (candidates.Any(row => row.All(Any))) return true;

            var tailTypes = types.Skip(1).ToArray();
            // Avoid expanding recursive unions when no pattern constrains this
            // column. It contributes no restriction, even for nested values.
            if (candidates.All(row => Any(row[0])))
                return Covers(tailTypes, candidates.Select(row => row.Skip(1).ToArray()).ToList());

            if (types[0] == "bool")
            {
                foreach (var value in new[] { false, true })
                {
                    var matching = candidates.Where(row => Any(row[0]) ||
                        row[0] is BoundLiteralPattern { Value: BoundBool literal } && literal.Value == value)
                        .Select(row => row.Skip(1).ToArray()).ToList();
                    if (!Covers(tailTypes, matching)) return false;
                }
                return true;
            }

            if (types[0] == "Unit")
            {
                var matching = candidates.Where(row => Any(row[0]) ||
                    row[0] is BoundLiteralPattern { Value: BoundUnit })
                    .Select(row => row.Skip(1).ToArray()).ToList();
                return Covers(tailTypes, matching);
            }

            if (definitions.TryGetValue(types[0], out var union))
            {
                foreach (var unionCase in union.Cases)
                {
                    var caseTypes = unionCase.PayloadTypes.Concat(tailTypes).ToArray();
                    var matching = new List<BoundPattern[]>();
                    foreach (var row in candidates)
                    {
                        if (Any(row[0]))
                        {
                            var wildcards = unionCase.PayloadTypes.Select(_ => (BoundPattern)new BoundWildcardPattern());
                            matching.Add(wildcards.Concat(row.Skip(1)).ToArray());
                        }
                        else if (row[0] is BoundCasePattern pattern && pattern.Union == union.Name && pattern.Case == unionCase.Name)
                            matching.Add(pattern.Arguments.Concat(row.Skip(1)).ToArray());
                    }
                    if (!Covers(caseTypes, matching)) return false;
                }
                return true;
            }

            // Other primitive values and record payloads form open domains for this
            // slice: finitely many literal rows cannot replace a catch-all.
            return Covers(tailTypes, candidates.Where(row => Any(row[0]))
                .Select(row => row.Skip(1).ToArray()).ToList());
        }
    }
}
