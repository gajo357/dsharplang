# Issue 237: Task return inference through async extension pipes

**Issue:** [#237](https://github.com/gajo357/dsharplang/issues/237), a bounded child of [#129](https://github.com/gajo357/dsharplang/issues/129).
**Base:** `0730dcf` (merged PR #236).
**Dependencies:** Adopted #129 async pipe semantics, async functions #41, bare extension pipes #211 and conservative return inference #233/#235.

## Scope and steps

Infer the return of an ordinary non-async function through a statically proven bare pipe into a unique unary async extension whose explicit logical result is supported immutable non-function data. The function returns the same `Task<R>` without an implicit await or flattening. Preserve exact receiver/input matching, collection intrinsic precedence, imports, dependency and cycle checks, evaluation order and capability, resource, builder and task-storage restrictions. Do not infer an `async fn`'s own logical result, accept unsupported callback/staged/overloaded targets or change public syntax/API. #129 stays open.

1. Extend the proven-extension pipe analysis in `src/DSharp.Compiler/Compiler.cs` to allow a safe async target and its logical data result while retaining its strict dependency edges and exact binder-target proof.
2. Add executable D# examples and new feature tests for returned tasks and async failures, imports, collection/non-collection receivers, grouping, evaluation order, recursive/indirect dependencies, collisions and lifetime restrictions. Keep each match arm on its own line.
3. Update implementation status with tested behavior. Preserve existing assertions and trusted verification.

## Acceptance and verification

- Execute inferred and annotated task-returning equivalents, including direct/imported extensions, typed collection receivers, task result and fault propagation. Test pending/cancellation only where a valid controlled fixture proves it.
- Reject unsafe or unproven dependencies, cycles, collisions, task storage and capability/resource/builder escapes; preserve ordinary async annotations and absence of implicit await.
- Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`, `bash .github/ai/verify.sh`, and the D# CLI example check on pinned .NET 11. Record actual results and limitations. Open one draft PR; fix CI on that branch. No public decision is needed.

## Results

- Bootstrap and `scripts/verify-compiler.sh` passed: 18 compiler tests and 378 feature registrations, with no build warnings. The D# CLI accepted `examples/async_extension_pipe_inference.ds`.
- Generated-code tests execute inferred and annotated task returns, imports, immutable collection receivers, `await`, nullable results, fault propagation and input/receiver fault order. They reject cycles, task storage, dynamic dispatch, collisions and unsafe captures. Independent review passed 36 adversarial CLI probes and a generated C# build. The tests do not claim controlled suspension or cancellation coverage.
- `.github/ai/verify.sh` passed documentation and all 25 automation tests. Its isolated compiler stage could not run locally because the pinned Docker image download returned `Forbidden`; GitHub CI supplies that verification.
