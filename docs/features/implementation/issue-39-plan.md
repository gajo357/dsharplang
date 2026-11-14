# Issue 39: Sequential D# for/in

**Base:** `28b76b95847d02f67f7153b041055af7db83e50e`.

## Scope and dependencies

Implement the adopted `for item in values { ... }` form for immutable `List<T>`
values after merged #37 and Unit/return #24. Evaluate the collection once,
visit elements sequentially in list order, give each iteration a fresh lexical
binding, require a Unit body, and evaluate the loop itself to Unit. Empty lists
execute no body. Early `return` exits the nearest function or lambda; typed Result
propagation retains its existing boundary. Map/Set iteration order is not adopted
here. Async/await, builders, break, continue and parallel iteration remain separate.

## Steps and affected interfaces

Add syntax and bound nodes, parse and resolve the collection and nested body,
extend binding and capture/reachability analysis, and lower to a single source
evaluation followed by an ordered loop over private immutable list storage. Keep
all existing APIs, tests and safety checks intact. Add an executable D# example
and generated-code acceptance tests; update implementation status.

## Acceptance-to-test map

- AC1: Unit-valued body and expression, lexical scope/shadowing and per-iteration
  closure capture, with out-of-scope and non-Unit diagnostics.
- AC2: Empty and ordered lists, source evaluated once, nested loops, early return
  and Result propagation; async behavior waits for the separate async feature.
- AC3: Reject non-list or nullable sources, wrong body types and unsupported loop
  controls, while preserving list values and existing compiler diagnostics.

Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`, and
`bash .github/ai/verify.sh`; check, compile and execute the D# example. Record
actual results and repair any CI failure on the same PR.

## Verification results

- `bash .github/ai/bootstrap.sh`: passed with the pinned SDK and writable npm cache.
- `bash scripts/verify-compiler.sh`: passed, with 18 compiler tests and 370
  feature tests discovered and passed (including `Sequential for/in`).
- `bash .github/ai/verify.sh`: documentation and all 25 automation tests passed;
  its isolated compiler step could not start because the pinned SDK container
  image download returned `Forbidden` in this environment. The independently
  run compiler verifier above passed; CI must verify the isolated step.
- The CLI checked and compiled `examples/for-in.ds`, and executing its generated
  `Demo.answer` returned 42. The acceptance test also compiled and executed
  generic and nested loops, Result propagation, escaped closures, and multi-file
  source, and checked the negative scenarios listed above.

The local .NET and npm commands used the pinned 11.0.100 SDK on `PATH`,
`XDG_RUNTIME_DIR=/tmp/dsharp-xdg-verify`,
`DOTNET_CLI_HOME=/tmp/dsharp-dotnet11-home`,
`NUGET_PACKAGES=/tmp/dsharp-dotnet11-packages`, and
`npm_config_cache=/tmp/dsharp-npm-cache` so the sandbox could write caches.
