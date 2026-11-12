# Implementation plan: #31 — Result and Success/Error constructors

Issue: https://github.com/gajo357/dsharplang/issues/31
Base: `7611fa5` on main; #28 and #24 are merged, and #30's generic data slice
was merged in #119. #30 remains open for unconstrained `T?`; this issue uses
closed nullable arguments such as `Result<Invoice?, Error>`, which #119 supports.
The user requested proceeding to this next issue after that boundary was disclosed.

## Scope and implementation

Add the standard typed `Result<T,E>` discriminated union with `Success(T)` and
`Error(E)`, expected-type checking and `Success()` shorthand for Unit. A
`Success(null)` with a nullable success type remains distinct from both a
present success and an error; match patterns work directly without Option/Some.
Require exhaustive matching and diagnose incompatible or ambiguous construction.
Reject discarded Result expression statements, including in nested blocks;
returning, matching, storing and explicitly typed consumption remain available.

Integrate with existing generic union binding, coverage and generated execution;
do not implement result behavior via source-text rewriting. `Result`, `Success`
and `Error` are standard type/case names, not globally reserved keywords; preserve
ordinary lexical lookup and explicit ambiguity diagnostics. No postfix `?`,
exception translation, worker error wrapper or implicit propagation in this issue.
Preserve existing APIs, tests, dependencies, project files and trusted verifiers.
Update compiler code, add feature tests and an example, plus status and this plan.

## Acceptance evidence

- AC1: execute typed success/error and Unit shorthand; reject wrong payloads,
  untyped ambiguous constructors and invalid shorthand with source spans.
- AC2: execute distinct `Success(invoice)`, `Success(null)` and `Error(error)`
  for `Result<Invoice?,E>` and match directly, in either nullable arm order.
- AC3: reject incomplete match, wrong error/success payloads and ambiguous
  inference; cover nested patterns, expected-context and nominal identities.
- AC4: reject unused Result statements while accepting return, match, local
  storage and typed argument consumption. Test evaluation order and early returns.
- Regressions: DEC-02 lookup, lexical shadowing, generated-name safety,
  generic/nullable/Unit behavior, warnings-as-errors and all existing tests.

## Verification

With the workspace SDK/cache configured:

```bash
bash .github/ai/bootstrap.sh
bash scripts/verify-compiler.sh
dotnet run --project src/DSharp.Cli -- check examples/result.ds
dotnet run --project src/DSharp.Cli -- compile examples/result.ds artifacts/result-demo
AI_REPORT_DIR=/workspace/work/issue31-verification bash .github/ai/verify.sh
git diff --check
```

Open one draft PR and fix CI on its branch; leave merge to the user. Do not
advance to another issue before review.

## Progress

- Implemented the standard Result as a distinct typed generic union integrated
  with existing constructor binding, matching, coverage and generated execution.
  `Success()` is Unit-only; discarding a standard Result statement is rejected.
  Standard names are an implicit fallback below current-module declarations,
  with lexical locals taking precedence. The intrinsic union is emitted only
  when used so unrelated programs keep their public bound union list and output.
- Added execution and rejection tests for all ACs, direct nullable payload
  matching in either order, nominal identity, original spans, Unit, name
  collisions, contextual construction, early returns and warnings-as-errors.
  Existing tests remain unchanged.
- Bootstrap, Release build with zero warnings, 18 baseline and 356 feature tests,
  CLI example check/compile, 20 automation tests and documentation checks passed.
  The local full verifier could not download its pinned SDK image (`Forbidden`);
  GitHub Actions runs the unchanged containerized verifier.

## PR

Draft PR pending user review and merge. Fix any CI failure on this branch.
