# Issue 41: Async functions, Task and await

**Base:** `c9b96384473b20b9f19aa9cecc46a07877a38513` (merged #40).
**Decision:** The maintainer explicitly adopted DEC-07 as proposed and requested
this implementation. Its adoption record is in [PR #162](https://github.com/gajo357/dsharplang/pull/162),
which must be merged before this implementation PR.

## Existing contracts and dependencies

The issue's listed dependencies #32, #20, #12 and #40 are merged. DEC-02 adopts
`async fn` and `await` grammar and precedence; the syntax reference §11.1 gives
the logical result annotation and Task-wrapped call result. DEC-02 does not adopt
async return-type inference, so require explicit annotations in this slice. Async
lambdas, worker scheduling, source-level catch and implicit task flattening remain
separate work. This plan does not adopt resource or host lifecycle decisions.

## Implementation outline

1. Parse explicit async declarations and `await`, retaining existing public APIs
   and checked diagnostics for invalid contexts and missing async annotations.
2. Bind `Task<T>` calls and await results with the specified types and precedence;
   preserve Result propagation, generic/nullability behavior and task values
   without silently inventing unsupported storage or implicit conversions.
3. Lower functions and every affected expression helper so async suspension keeps
   evaluation order, early return and typed propagation intact; avoid async
   helpers when they would not suspend. Keep synchronous behavior unchanged.
4. Permit same-invocation builders across await while preserving all current
   non-escape and lambda-capture checks. Add runnable D# examples and feature
   tests that execute generated code with controlled incomplete and faulted tasks.

## Acceptance-to-test map

- AC1: explicit async signatures, logical return types, Task calls, await typing,
  precedence and rejection in synchronous or unsupported contexts.
- AC2: delayed, completed and faulted task fixtures; evaluation order and locals
  across suspension, including nested conditionals, loops and Result propagation.
- AC3: builder before/after suspension with independent snapshots; reject capture,
  returns, wrappers and helper-call escapes before and after await.
- AC4: synchronous callers receive a Task and cannot use its result without an
  explicit await; async calls must not implicitly await or flatten nested tasks.

## Verification

After implementing, run `bash .github/ai/bootstrap.sh`,
`bash scripts/verify-compiler.sh` and `bash .github/ai/verify.sh`, compile and
execute the D# example, record actual results and fix any CI failure on the same PR.

## Current verification and test conflict

- Bootstrap passed. The CLI checked and compiled `examples/async-functions.ds`;
  running its generated async `Demo.answer()` returned 42.
- The focused generated-code async acceptance test passed, including controlled
  suspension, failures, cancellation, builder lifetime, nested tasks, generic
  nullable results, multi-file type shadowing and explicit task-valued pipes.
- `bash scripts/verify-compiler.sh`: the 18 compiler tests passed; the feature
  suite reported 371 passed and one failure. The pre-existing
  `PipeAndReturnInferenceTests` scenario 33 expects a `Task<int>` piped to a
  function accepting `Task<int>` to fail because Task types were unsupported.
  With this feature the program is valid under ordinary pipe semantics. The new
  acceptance test executes this exact case without implicit await. Replacing that
  old scenario's parameter type with `int` would continue to verify that a pipe
  cannot implicitly await, but #41's scope and AGENTS.md prohibit changing old
  tests. Resolve that explicit conflict before claiming complete verification.
- `bash .github/ai/verify.sh`: documentation and all 25 automation tests passed;
  the isolated compiler step could not download its pinned SDK container image
  locally because the registry returned `Forbidden`. GitHub CI must run it.
