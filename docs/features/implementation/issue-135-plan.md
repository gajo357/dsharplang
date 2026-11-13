# Issue 135: ordinary partial application

Base: `fef685cc2113fcde1b20025cb56eb146a9205d51` on main. The scoped baseline
migration #138 is merged. This implements the ordinary-function child of #129,
using the [composition target](../DSharp_Pipes_and_Logging.md); #129 remains open
for async, service/interface/extension and resource-lifetime integrations.

## Scope and implementation

- Extend call binding, generic inference and C# emission for positional prefix
  application of ordinary functions and function values, preserving declared
  multi-argument types, normal full calls, grouped invocation and empty prefixes.
- Capture callable and supplied arguments once in source order. Invoke the body
  only when saturated; subsequent invocations of a partial function are separate.
- Support pipes supplying the first remaining argument, newline continuation,
  lexical scopes, nullable/generic and tuple values, without implicit await,
  unwrap, retry, tuple unpacking or composition. Preserve existing non-escape
  restrictions and diagnose illegal function discards, excess args and ambiguity.
- Touch compiler source and new feature tests, an example if useful, this plan
  and the implementation status. Preserve existing tests, dependencies and
  trusted verification. Do not implement parent-issue runtime integrations.

## Acceptance and checks

- Execute generated C# for partial/grouped/empty/full calls, pipes, expression
  ordering, capture timing and repeated calls; verify one tuple argument versus
  multiple parameters, null/Option/Result payloads and generic inference.
- Reject excess arguments, mismatched types, unconstrained generics, discarded
  functions, prohibited captures and unsupported named partial calls with
  meaningful diagnostics. Preserve all existing test coverage.
- Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`,
  `AI_REPORT_DIR=/workspace/work/issue-135-verification bash .github/ai/verify.sh`,
  doc/link checks and `git diff --check`; record actual results.
- Unresolved decisions: none assumed for this bounded slice. If a previously
  unapproved public contract is necessary, document it before implementation.

## Results

- Bootstrap and compiler verification passed (18 baseline, 359 feature tests).
  The focused test passed again after adding the supplied-argument early-return
  regression. Generated C# tests check invocation timing, order, generic/nullable
  inference, grouped calls, function values, tuple arguments and diagnostics.
- Documentation and all 20 automation tests passed. The local isolated compiler
  verifier could not pull its pinned Docker image (`Forbidden`); GitHub Actions
  runs the unchanged verifier.
- No existing test, project, dependency or verification file was changed.
