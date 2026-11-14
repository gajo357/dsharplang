# Issue 42: Async lambdas

**Base:** `6bc164926dda9ea0278b0ab275aeb875cf12519b` (merged #41).
**Dependencies:** async functions and Task #41; immutable closures #34; builder
non-escape #40. DEC-02 adopts the async lambda grammar; syntax reference §9.5
specifies explicit async and task-valued results.

## Scope and open boundary

Implement explicit `async` lambdas as function values returning `Task<R>`, with
typed/contextual parameters, immutable captures, actual suspension, and local
`return` and Result `?` boundaries. Do not allow builders to be captured; builders
created inside the lambda remain local to that invocation. Async functions,
ordinary lambdas and APIs retain their existing behavior.

The #41 compiler rejects task values in immutable lists and other immutable data.
The #42 map criterion is conditional on types permitting `List<Task<T>>`; no
approved relaxation of the existing immutable-data boundary is made here. Test
that ordinary `map` does not implicitly await or silently store tasks, and record
the conditional gap. Do not claim that map-of-tasks executes until a separately
approved execution-collection representation exists. Async parallel operators,
resource implementations and durable function payloads are out of scope.

## Steps

1. Parse the adopted explicit forms (`async x => ...`, `async (...) => ...`)
   and retain async metadata across source and bound lambda nodes and resolvers.
2. Check contextual `(parameters) -> Task<R>` against the lambda body as `R`,
   including generics, nullable types, omitted parameter annotations and nested
   lambdas. Infer a logical result only when supported by the adopted rules; do
   not add implicit task flattening or implicit await.
3. Lower async lambdas to Task-returning delegates with proper closure state and
   nonblocking awaits; ensure helper lowering, lambda return and `?` boundaries
   work across suspension. Preserve sync lambda and enclosing function behavior.
4. Add executable D# examples and generated-code feature tests; update status.

## Acceptance-to-test map

- AC1: typed and contextually inferred parameters, expression and block forms,
  Task-valued callback signatures, generic/nullable values, suspension and
  diagnostics for mismatched contexts or illegal await.
- AC2: early return and Result propagation stay inside the lambda, nested sync
  and async lambda boundaries are respected, mutable builder capture is rejected,
  and an invocation-local builder can survive its own await.
- AC3: run awaited closures with genuinely incomplete, failed and cancelled
  tasks. Check ordinary map's async-callback rejection under the current
  immutable-data type rule, with no implicit await. List-of-tasks execution is
  contingent on a future supported execution-collection type boundary.

## Verification

Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`, and
`bash .github/ai/verify.sh`; check, compile and execute the D# example. Record
actual results. If CI fails, repair this same PR branch.

## Verification results

- `bash .github/ai/bootstrap.sh` passed with the pinned SDK and writable caches.
- `bash scripts/verify-compiler.sh` passed 18 compiler tests and 373 feature
  tests. After adding delayed lambda-local Result/return and nullable generic
  capture cases, the focused async-lambda test passed again.
- `bash .github/ai/verify.sh` passed documentation and 25 automation tests; its
  isolated compiler stage could not download the pinned SDK image locally because
  the registry returned `Forbidden`. GitHub CI must run that isolated stage.
- The CLI checked and compiled `examples/async-lambdas.ds`, and its generated
  `Demo.answer()` returned 42. Acceptance tests execute controlled suspension,
  failure/cancellation, local early return and Result propagation, captures,
  nested tasks, generic/nullability and invocation-local builders; illegal
  captures, sync-lambda await and task-valued immutable-list mapping are rejected.

The local commands used the pinned 11.0.100 SDK on `PATH`,
`XDG_RUNTIME_DIR=/tmp/dsharp-xdg-verify`,
`DOTNET_CLI_HOME=/tmp/dsharp-dotnet11-home`,
`NUGET_PACKAGES=/tmp/dsharp-dotnet11-packages`, and
`npm_config_cache=/tmp/dsharp-npm-cache` for sandbox-writable caches.
