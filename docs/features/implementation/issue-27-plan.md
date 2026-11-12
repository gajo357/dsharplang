# Implementation plan: #27 — Exhaustive match and guards

Issue: https://github.com/gajo357/dsharplang/issues/27
Base: `3cff2f0` on main. Prerequisites #21 and #26 were merged in #108 and
#115; adopted DEC-02 remains the lookup contract.

## Scope and implementation

Implement `match` as an expression over supported unions, bools and primitive
values, with case patterns, nested payload patterns, literal patterns, named
bindings and `_`. Arms may have bool `when` guards; bindings are scoped to the
guard and arm body. Evaluate the scrutinee once and take the first arm whose
pattern and guard match. Preserve source-order evaluation and early returns.
Check arm result types and reject invalid patterns before C# emission. Nullable
patterns, Result-specific semantics, generics and reflection matching are out of
scope for their later issues.

Coverage considers unguarded patterns only. Require every union and bool value
to be covered, including nested payload combinations; open primitive domains need
an unguarded binding or wildcard covering their remaining values. The wildcard
deliberately covers future union cases. Respect DEC-02 ambiguity rules for
unqualified cases; do not infer away ambiguous names from an expected type.

Implementation touches compiler lexer/parser, syntax and bound models, binder,
coverage analysis and C# emitter. Add new feature tests, `examples/match.ds`,
implementation status and this plan. Preserve existing public APIs, all trusted
and feature tests, dependencies, project files, workflows and verifiers. No
unresolved decisions currently block the frozen match contract.

## Acceptance evidence

- AC1: execute nested case/literal/wildcard/binding patterns, qualified cases,
  bool guards and lexical scopes; reject malformed or wrongly typed payloads.
- AC2: enforce compatible arm values and exhaustive bool/union coverage,
  including nested and multi-payload combinations; require open-domain catch-all.
- AC3: guarded arms do not count for coverage, scrutinee runs once, guards and
  bodies evaluate lazily in first-match order; early returns target the function.
- AC4: reject missing cases and changed unions, but accept an explicit wildcard;
  verify original source diagnostics and generated execution with warnings as errors.

## Verification

With the workspace SDK/cache configured:

```bash
bash .github/ai/bootstrap.sh
bash scripts/verify-compiler.sh
dotnet run --project src/DSharp.Cli -- check examples/match.ds
dotnet run --project src/DSharp.Cli -- compile examples/match.ds artifacts/match-demo
AI_REPORT_DIR=/workspace/work/issue27-verification bash .github/ai/verify.sh
git diff --check
```

Open one draft PR and repair any CI failure on that same branch. Leave merge to
the user and do not start another issue before review.

## Progress

- Implemented typed match expressions, nested case/literal patterns, lexical
  bindings, wildcard and bool guards. Matrix coverage considers combinations of
  unguarded union/bool patterns, recursive unions, Unit and open domains.
  Generated code evaluates the scrutinee once and applies patterns and guards in
  order; early returns and noncompleting guards preserve function control flow.
- Added execution and diagnostic tests covering the ACs, source spans,
  malformed and ambiguous patterns, arm scope, guard fallback, future cases,
  nested coverage and a directly instrumented single-evaluation check. Existing
  tests remain unchanged.
- Bootstrap, Release build with zero warnings, 18 baseline and 228 feature tests,
  CLI example check/compile, 20 automation tests and documentation checks passed.
  The local full verifier could not download the pinned SDK image (`Forbidden`);
  GitHub Actions runs the unchanged containerized verifier.

## PR

Draft PR pending user review and merge. Fix any CI failure on the same branch.
