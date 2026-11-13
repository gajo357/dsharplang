# Issue 142: bind Result helpers from D# source

Base: `3b1d715d9ca6a4aaabe32528f90beaaad51b00c2` on main. Multi-file #46 and
packaging/verification setup #144 are merged. The five helper bodies now live in
the embedded `src/DSharp.Compiler/Prelude/Result.ds`; the current compiler still
constructs equivalent bound bodies by hand in C#.

## Scope

- Load the embedded source from the compiler assembly, parse and bind the actual
  D# helper definitions through the normal frontend. Remove the hand-built C#
  helper-body implementation rather than maintaining a second implementation.
- Preserve ordinary source/local name precedence, standard Result identity under
  user shadowing, used-only helper emission, explicit generic parameter order,
  generic/nullable projections, callback timing and function-data safety.
- Do not add a user-facing import/package contract, async overloads, projects,
  dependencies, or edits to trusted verification and existing tests. Keep the
  resource working outside the checkout and report internal source errors clearly.
- Add focused feature tests and update implementation status. #130 remains open
  for async composition.

## Acceptance and verification

- Execute all five helpers through normal source-level calls, partials and pipes;
  preserve nullable/Option/nested Result distinctions, error types, eager callback
  construction and short-circuiting. Existing #136 regression tests remain active.
- Prove normal compiler execution uses the embedded D# implementation, including
  changes to helper source affecting compiled results and invalid helper source
  being diagnosed. Exercise shadowed `Result` and helper names, generic data safety,
  compiler use outside the checkout and deterministic resource lookup.
- Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`,
  `AI_REPORT_DIR=/workspace/work/issue-142-verification bash .github/ai/verify.sh`,
  packaging tests, docs/link checks and `git diff --check`; record actual results.
  CI fixes stay on the same branch.
- No new language contract is assumed. Use the #144 resource as the single source
  of truth; report any genuine unresolved semantic dependency.

## Results

- Bootstrap and compiler verification passed: 18 baseline and 362 feature tests.
  New generated-code tests mutate the embedded D# in an isolated compiler copy
  and prove its changed behavior, normal diagnostics for invalid D# source,
  transitive helper calls, shadowing and capture safety, and use outside checkout.
- Documentation and all 23 automation tests passed. The local isolated verifier
  could not pull its pinned Docker image (`Forbidden`); GitHub Actions runs the
  unchanged trusted verifier. Existing tests, projects and verifier are unchanged.
