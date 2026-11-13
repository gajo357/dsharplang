# Issue 136: synchronous Result composition helpers

Base: `1edfda8` on main, including merged partial-application #135. Standard
Result #31 and the ordinary-function slice are available. This implements only
the synchronous child of #130; async overloads remain in the parent.

## Scope and implementation

- Add ordinary first-class `andThen`, `map`, `mapError`, `tap` and `tapError`
  symbols using the existing compiler prelude/standard Result boundary, with no
  new project, dependency or implicit pipe behavior. Preserve current-module
  and local shadowing precedence and use the canonical standard Result type.
- Follow callback-first/result-last generic signatures in the
  [composition specification](../DSharp_Pipes_and_Logging.md#4-result-composition-through-ordinary-functions).
  Preserve explicit error types, normal eager evaluation of callback expressions,
  single callback invocation and normal exception propagation.
- Preserve `Success(null)`, `Success(None)`, `Some(null)` and nested Results
  without silent flattening or Option/null conversion. `tap` observers return
  Unit; only `andThen` sequences a Result-returning callback.
- Extend compiler/emitter and new feature tests; update status and this plan.
  Keep existing tests, pinned dependencies and trusted verifier unchanged.

## Acceptance and verification

- Generated execution tests cover all five helpers on both outcomes, callback
  creation vs invocation, partial application/pipes, first-class functions,
  matching error types, null/Option and nested results. Negative cases cover
  mismatches, non-Unit observers, incompatible callbacks and missing context.
- Verify ordinary name lookup, generic/nullable projection and no implicit
  retry, flattening or error conversion. No Task/async behavior is added.
- Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`,
  `AI_REPORT_DIR=/workspace/work/issue-136-verification bash .github/ai/verify.sh`,
  documentation checks and `git diff --check`. Record actual results and keep
  any CI fixes on the same PR branch.
- No unresolved language decision is assumed for the bounded synchronous slice;
  retain the existing prelude conventions and report any newly required contract.

## Results

- Bootstrap and compiler verification passed (18 baseline, 360 feature tests).
  The final focused test also passed with explicit-return and mismatched-error
  lambda regressions added after that full run.
- Generated C# tests cover all helpers and both outcomes, callback timing and
  counts, generic/nullable arguments, nested Result/Option, lookup precedence,
  error diagnostics and pipe/partial calls.
- Documentation and all 20 automation tests passed. The local isolated
  compiler verifier could not pull its pinned Docker image (`Forbidden`);
  GitHub Actions runs that unchanged stage.
- Existing tests, project files, dependencies and trusted verification were not
  modified. Async overloads remain in #130.
