# Issue 205: Sequential async for-loop integration

**Base:** `461f297` (merged #204).
**Issue:** [#205](https://github.com/gajo357/dsharplang/issues/205), a follow-up to #39 AC2.
**Dependencies:** List iteration #39, async functions #41, implemented async lambdas #42, local builders #40 and scoped resources #45.
**Status:** Verified; ready for review.

## Scope and decisions

Complete and verify sequential awaiting in the body of an ordinary immutable List `for` loop. The existing backend detects awaits in loop bodies; do not claim new lowering if execution evidence shows it already works. Use controlled incomplete tasks to prove no iteration starts before the previous await finishes, source and element order are preserved, and errors, cancellation, early return, propagation and lifetime boundaries work. Implement any compiler fixes needed without new syntax, APIs, async enumerables, implicit parallelism or changes to existing tests and trusted verification. Format every D# match case on its own line, separately from braces.

## Steps and files

1. Inspect parser, binder, emitter and current #39/#41 coverage. Add compiler fixes in `src/DSharp.Compiler/` only if integration cases expose gaps.
2. Add new feature execution tests using host-controlled gates and D# programs, covering source evaluation, empty/multiple lists, nested loops, lambdas, nullability, Result and lifetime behavior.
3. Add runnable D# example and update implementation status and this plan with exact verified scope.

## Acceptance-to-test map

- Controlled incomplete Tasks prove ordered non-overlapping iteration, source once, empty input, local state and no hidden parallelism.
- Fault, cancellation, early return and Result propagation stop later iterations and preserve outcomes; nested/lambda cases stay within the appropriate boundary.
- Generic/nullable elements, invocation-local builders/resources and invalid escaping/sync-await/non-Unit bodies retain existing rules.
- Generated C# builds with warnings as errors; run existing compiler and feature suites.

## Verification

- `bash .github/ai/bootstrap.sh`: passed.
- `bash scripts/verify-compiler.sh`: passed (18 compiler and 378 feature tests).
- `bash .github/ai/verify.sh`: documentation and all 25 automation tests passed; the isolated compiler stage could not pull the pinned SDK image locally because the registry returned `Forbidden`. GitHub CI runs that stage.
- Focused generated-code tests with genuinely incomplete Tasks passed for awaited source and sequential iterations, empty input, fault/cancellation, nested loops, lambda-local returns, Result propagation, nullable generic local builders, and synchronous/asynchronous resource cleanup.
- Independent execution probes verified async cleanup before later iterations/returns and fault precedence. Existing lowering passed; no compiler modification was necessary.
- `examples/async_for.ds` checked and compiled without warnings; generated `Demo.answer()` returned `42`. D# match cases use one case per line.

Fix CI failures on this PR branch.
