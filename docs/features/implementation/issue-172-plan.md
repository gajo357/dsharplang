# Issue 172: Immediately consumed owned-resource service methods

**Base:** `2d8c61c` (merged #171).
**Issue:** [#172](https://github.com/gajo357/dsharplang/issues/172), a bounded child of [#45](https://github.com/gajo357/dsharplang/issues/45).
**Status:** Implemented for review; #45 remains open for Result transfer.

## Scope and ownership rules

Extend #170's fresh owned-resource return rules to nongeneric interface/service
methods and supported default interface bodies. Use existing D# declarations and
calls: `interface Store { open(): Cursor }`, `using cursor = store.open()`, or
`using cursor = await store.openAsync()`. Calls must be fully applied and used
immediately by `using` or by a checked owned-return body; async calls must be
directly awaited. Ordinary immutable data rules remain unchanged.

Validate every service implementation and interface default body, including
forwarding through checked functions or methods. Owned-return bodies cannot
contain `using`, because a cleanup failure could abandon a produced resource
before transfer. The receiver and arguments are bound in ordinary context and
must be evaluated once in source order. Continue rejecting resource aliases,
parameters, data, captures, method values, partial applications, generic
forwarding and unawaited/stored resource tasks. Preserve C# cleanup precedence.

This is a D# source ownership guarantee, not a CLR interop guarantee: generated
interfaces and resource types have public CLR surfaces from #43/#169, and
foreign implementations/callers are not yet an approved ownership boundary.
`Result<Resource,E>` transfer, general borrowing, runtime providers, CLR
interop and implicit registration remain separate work under #45 or later.

## Implementation steps and files

1. Extend signature and method conformance checks for direct owned returns;
   validate checked synthetic service/default bodies without weakening the
   existing data/capability rules or accepting factory method values.
2. Extend binding for immediate method acquisition/await and owned forwarding,
   preserving polymorphism, receiver/argument evaluation and source diagnostics.
   Adjust resolution or C# lowering only as needed.
3. Add executable D# examples and new feature coverage, including malicious
   escape tests; update status and the resource contract. Keep #45 open.

## Acceptance-to-test map

- Sync and async factories through service and interface values; two
  implementations, explicit override and default method, direct forwarding,
  nested/cross-module dispatch, receiver/argument evaluation once, and cleanup.
- Async gates for suspension, fault and cancellation; no disposal before
  acquisition and exactly one release after acquisition.
- Reject method values/partials, detached tasks, indirect/generic/data/closure
  escapes, mismatched returns, ordinary producers hidden in arguments, and
  `using` inside owned-returning functions, service methods and defaults.
- Preserve existing tests; compile generated C# with warnings as errors. Reuse
  the existing resource test's aggregation hook to avoid exceeding CI's bounded
  TRX report size while retaining all assertions.

## Verification

Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`,
`bash .github/ai/verify.sh`, then check, compile and execute the D# example.
Record results and fix CI failures on the same PR branch.

### Verification results

- `bash .github/ai/bootstrap.sh` passed.
- `bash scripts/verify-compiler.sh` passed 18 compiler tests and 377 feature
  test results, including the new factory checks run from the existing resource
  test. Generated C# builds with warnings as errors; no existing assertions were
  removed.
- The D# example checks, compiles and runs; both synchronous and async answers
  return 42. Independent probes covered default/override and multi-interface
  dispatch, ownership escapes and ordinary external CLR member visibility.
- `bash .github/ai/verify.sh` passed documentation and 25 automation tests.
  The local isolated compiler stage could not download the pinned SDK image
  because the registry returned `Forbidden`; GitHub CI runs that stage.

Commands used SDK 11.0.100 via `/workspace/work/dotnet11`,
`XDG_RUNTIME_DIR=/tmp/dsharp-xdg-verify`,
`DOTNET_CLI_HOME=/tmp/dsharp-dotnet11-home`,
`NUGET_PACKAGES=/tmp/dsharp-dotnet11-packages`, and
`npm_config_cache=/tmp/dsharp-npm-cache` for writable caches.
