# Issue 40: Method-local mutable builders

**Base:** `589df6634696a5ce608ac4f78575d2620f3ad6ff` (merged #39).
**Dependencies:** immutable Map/Set #38, immutable captures #34, and sequential
iteration #39. The adopted builder contract is in DEC-01 and syntax reference §8.1.

## Scope

Implement `MutableList<T>()` with `add` and `freeze`, and `MutableMap<K,V>()`
with `set` and `freeze`. Mutations return Unit; each freeze produces an independent
immutable snapshot. Builder bindings and local aliases may be used only in their
creating invocation and as receivers of recognized operations. Reject escape via
returns, fields, parameters, closures (even immediately invoked), ordinary calls,
partial application, aliases that escape, tuples, unions, collections, generics,
and other nested values. Builders created inside lambdas belong to that invocation;
generated compiler helpers do not constitute user lambda capture. Builder element
types must be immutable data, and map keys follow existing structural-key rules.
Borrowed helper arguments/captures, general reassignment, async integration and
foreign interoperability remain outside this issue.

## Implementation

1. Extend binding and local alias tracking with a narrowly scoped builder type
   representation; keep builder types out of ordinary data and function signatures.
2. Recognize constructors only at safe local bindings and allow builder values
   only for local aliases and direct receivers of checked operations. Preserve
   lexical scope and reject captures transitively; check generic and nullable
   paths without weakening existing immutable-data checks.
3. Emit private mutable local storage, using the existing D# map key equality and
   hash semantics; copy into immutable list/map values on every freeze. Preserve
   evaluation order, once-only evaluation and existing public APIs.
4. Add a real D# example and new feature tests executing generated code, covering
   aliases, mutation and snapshots as well as adversarial escape attempts. Update
   the compiler status with precisely verified behavior.

## Acceptance-to-test map

- AC1: construct both builders, mutate through aliases, exercise empty and generic
  builders, Unit operations, loops and nested local scopes.
- AC2: reject direct and indirect escape, nested wrappers/containers, arguments,
  partial application, signatures and data fields, and captures including immediate
  lambdas; distinguish a builder created inside a lambda from an outer capture.
- AC3: execute multiple freeze/mutate cycles, checking earlier snapshots and map
  structural keys, nullable values and duplicate-key updates.
- AC4: run adversarial nested/capture cases alongside positive same-invocation
  aliases. Include generated-code execution, not only emitted text or parsing.

## Verification

Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`, and
`bash .github/ai/verify.sh`; check, compile and execute the D# example. Record
actual results and repair any failing CI on the existing PR branch.

## Verification results

- `bash .github/ai/bootstrap.sh`: passed with the pinned SDK and writable caches.
- `bash scripts/verify-compiler.sh`: passed with 18 compiler tests and 371
  feature tests, including `Local builders`.
- `bash .github/ai/verify.sh`: documentation and all 25 automation tests passed;
  the isolated compiler step could not start because the pinned SDK container
  image download returned `Forbidden` in this environment. The independently
  run compiler verifier passed; GitHub CI must verify the isolated step.
- The CLI checked and compiled `examples/local-builders.ds`; running its generated
  `Demo.answer()` returned 42. Generated-code acceptance tests also execute local
  aliases, repeated snapshots, structural map-key collisions, generic nullable
  elements, lambda-local builders and multi-file name resolution, and reject
  adversarial escapes and captures.

The local commands used the pinned 11.0.100 SDK on `PATH`,
`XDG_RUNTIME_DIR=/tmp/dsharp-xdg-verify`,
`DOTNET_CLI_HOME=/tmp/dsharp-dotnet11-home`,
`NUGET_PACKAGES=/tmp/dsharp-dotnet11-packages`, and
`npm_config_cache=/tmp/dsharp-npm-cache` for writable sandbox caches.
