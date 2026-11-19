# Issue 223: Async Result error mapping and taps

**Issue:** [#223](https://github.com/gajo357/dsharplang/issues/223), a partial child of [#130](https://github.com/gajo357/dsharplang/issues/130).
**Base:** `2502b4d` (merged PR #222).
**Dependencies:** Adopted [pipeline and Result helper contract](../DSharp_Pipes_and_Logging.md), synchronous helpers #136, embedded D# prelude #142, async functions/lambdas #41/#42, task-input `map` #219 and async `andThen` #221.

## Scope and steps

Implement `Task<Result<A, E>>` input overloads of `mapError`, `tap` and `tapError` using synchronous callbacks and D# prelude bodies. Extend the existing bounded private prelude overload registration and selector, keeping normal shadowing, type inference, ambiguity diagnostics, direct/pipe/typed/grouped calls and used-only transitive prelude emission. Await input once; invoke the applicable callback once on the relevant branch, preserving eagerly evaluated callback expressions, value/error tags, faults and cancellation. Do not implicitly await the pipe, flatten values, allow async Unit observers or introduce new public names. Keep #130 open until all parent criteria are verified.

1. Add three private helper implementations to `src/DSharp.Compiler/Prelude/Result.ds` with separate match-arm lines.
2. Register the helper variants in `src/DSharp.Compiler/Compiler.cs` through the existing overload framework, preserving all previous functionality and trusted tests.
3. Add `tests/features/AsyncResultObserversTests.cs` and executable `examples/async_result_observers.ds` for all helpers and branches, direct/pipe/typed/grouped calls, delay and ordering, skip behavior, callback/source faults and cancellation, null/Option/nested Results, mismatch and async-observer rejection, name shadowing and transitive prelude dependencies.
4. Update `docs/features/DSharp_Implementation_Status.md` with precise tested behavior and remaining parent work.

## Verification

Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`, `.github/ai/verify.sh` and the D# CLI example check using pinned .NET 11. Execute generated C# with controlled tasks and callbacks. Record actual results and limitations; open one draft PR and fix any CI failure on that same branch. This implements an existing adopted contract and introduces no new public syntax or runtime contract.

The new feature tests are registered through the existing aggregate to avoid the previously observed GitHub test-report size limit; all assertions and the trusted verifier remain intact.

## Results

- Bootstrap and the corrected `scripts/verify-compiler.sh` passed: 18 compiler tests and 378 feature registrations, including both new generated-code suites. The Release build had zero warnings or errors.
- The first full run exposed a duplicate source marker used by an existing packaged-prelude test. The new async tap binding was renamed, preserving its behavior; the existing test, new copied-prelude test and full rerun all passed without changing existing assertions.
- The D# CLI accepted `examples/async_result_observers.ds`. Tests cover all three helpers, both Result branches, conversion to a different error type, delayed tasks, evaluation and skip behavior, faults/cancellation, nullable/Option/nested values, shadowing and transitive prelude calls.
- `.github/ai/verify.sh` passed documentation and all 25 automation tests. Its isolated compiler stage could not run locally because the pinned SDK Docker image download returned `Forbidden`; GitHub CI must provide that result.
