# Issue 221: Async Result andThen combinations

**Issue:** [#221](https://github.com/gajo357/dsharplang/issues/221), a partial child of [#130](https://github.com/gajo357/dsharplang/issues/130).
**Base:** `116d86c` (merged PR #220).
**Dependencies:** Adopted [pipeline and Result helper contract](../DSharp_Pipes_and_Logging.md), synchronous helpers #136, embedded D# prelude #142, async functions/lambdas #41/#42 and task-input `map` #219.

## Scope and approach

Complete `andThen`'s four Result/Task<Result> input and synchronous/asynchronous callback combinations using D# prelude bodies. Generalize the existing bounded prelude-overload selection without changing normal function overload rules, synchronous helpers, visibility, generic inference or pipe semantics. Resolve from actual input, callback and expected types; reject unresolved ambiguities instead of selecting by order. Ensure dynamically selected prelude dependencies are discovered and emitted once independently of declaration order, preserving used-only emission and intrinsic Task/Result identities under user shadowing.

1. Add three async helper bodies to `src/DSharp.Compiler/Prelude/Result.ds`, with separate match-arm lines and only the necessary awaits.
2. Extend `src/DSharp.Compiler/Compiler.cs` with collision-safe private registration, overload probes, direct/pipe/bare/typed/grouped selection, and robust transitive helper scheduling. Preserve all existing assertions and safety checks.
3. Add executable D# example `examples/async_result_andthen.ds` and `tests/features/AsyncResultAndThenTests.cs` covering the entire four-way matrix, actual sequential suspension, success/error and callback skip, eagerly constructed callbacks, exactly-once evaluation, task/callback faults and cancellation, typed and inferred lambdas, generic/nullable/Option/nested payloads, mismatch and ambiguity diagnostics, local/import/module and Task/Result shadowing, and prelude cross-helper dependencies.
4. Update `docs/features/DSharp_Implementation_Status.md`. Other asynchronous Result helper variants remain open under #130.

## Verification

Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`, `.github/ai/verify.sh` and the D# CLI check with pinned .NET 11. Execute generated code including controlled input and callback tasks, cancellation/fault behavior and copied-prelude regression. Record results and environment limitations. Open one draft PR; fix CI failures on that branch. The exact public API and behavior were adopted in #130; this work selects no new language contract.

The two new feature suites are registered through the existing feature aggregate to avoid the previously observed GitHub test-report size limit, retaining all assertions and the trusted verifier unchanged.

## Results

- Bootstrap and `scripts/verify-compiler.sh` passed: 18 core compiler tests and 378 feature registrations, including the new generated-code matrix and cross-prelude tests. The Release build had zero warnings or errors.
- After tightening the distinct-helper assertion, the feature aggregate passed again. Tests cover the four combinations, suspension and callback ordering, errors, faults, cancellation, nullable/Option/nested values, shadowing, overload diagnostics and copied-prelude dependencies in both orders and unreachable branches.
- The D# CLI accepted `examples/async_result_andthen.ds`.
- `.github/ai/verify.sh` passed documentation and all 25 automation tests. Its isolated compiler stage could not run locally because the pinned SDK Docker image download returned `Forbidden`; GitHub CI must provide that result.
