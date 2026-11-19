# Issue 219: Map an asynchronous Result

**Issue:** [#219](https://github.com/gajo357/dsharplang/issues/219), a partial child of [#130](https://github.com/gajo357/dsharplang/issues/130).
**Base:** `9c1d1f4` (merged PR #218).
**Dependencies:** Adopted [pipeline and Result helper contract](../DSharp_Pipes_and_Logging.md), synchronous Result helpers #136, the embedded D# prelude #142, async functions and lambdas #41/#42, and ordinary partial application #135 have merged implementation slices.

## Scope and steps

Implement the task-input `map` overload for a synchronous callback and `Task<Result<A, E>>`, returning `Task<Result<B, E>>`, including direct calls, pipes and typed callable contexts where the task form is determined. Keep its implementation in the existing packaged D# prelude. Add bounded type-directed resolution for this library symbol while preserving source/local shadowing, existing synchronous helpers, generic inference and ambiguity diagnostics. Await the source once and invoke the callback once only for success; preserve eager callback expression evaluation, errors, exceptions and cancellation. Do not change pipe semantics, admit stored task payloads, or add a public alternate helper name. Leave the rest of #130 open.

1. Add the async helper body in `src/DSharp.Compiler/Prelude/Result.ds`; use the compiler's private prelude registration and `src/DSharp.Compiler/Compiler.cs` for bounded overload selection and generated binding. Preserve existing code and tests.
2. Add executable D# example `examples/async_result_map.ds` and `tests/features/AsyncResultMapTests.cs`: direct/pipe/typed partial, success/error/Option/nullable/nested Result, delayed completion, invocation/order/eager capture, fault/cancellation, mismatched callback, ambiguity and shadowing. Use separate match arm lines.
3. Update `docs/features/DSharp_Implementation_Status.md` with the tested boundary and leave remaining async variants unclaimed. Ensure ordinary standalone or other helpers behave as before.

## Verification

Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`, `.github/ai/verify.sh` and the D# CLI example check using pinned .NET 11. Execute generated C# including delayed and faulted/cancelled tasks, and retain all trusted tests. Record actual results and any environment limitations. Open one draft PR and fix any CI failures on that branch. No new public contract is adopted: this is a bounded implementation of #130; unresolved overload contexts must be diagnosed or retain supported existing semantics without order-based guessing.

The new feature test is registered through the existing feature aggregate, retaining every assertion and the trusted verifier. Earlier GitHub compiler runs failed solely because 379 independently registered test results exceeded the report-size limit.

## Results

- Bootstrap and `scripts/verify-compiler.sh` passed: 18 compiler tests and 378 feature registrations, including the new generated-code acceptance and copied-prelude regression. The Release build has zero warnings or errors.
- The D# CLI accepted `examples/async_result_map.ds`. Tests executed success/error, task suspension, callback creation and invocation order, faults, cancellation, generics, nullable/Option/nested Result values, source shadowing and prelude-internal calls under shadowing.
- `.github/ai/verify.sh` passed documentation and all 25 automation tests. The final isolated compiler stage could not run locally because the pinned .NET SDK Docker image download returned `Forbidden`; GitHub CI must provide that result.
