# Issue 170: Immediately consumed owned-resource function factories

**Base:** `bd02501` (merged #169).
**Issue:** [#170](https://github.com/gajo357/dsharplang/issues/170), a bounded child of [#45](https://github.com/gajo357/dsharplang/issues/45).
**Status:** Implemented for review; #45 remains open for the remaining ownership work.

## Scope and ownership rules

Support ordinary explicitly typed, non-generic top-level functions returning a
fresh nominal resource. A factory can transfer the resource directly from a
constructor or another checked factory in a return position, including through
branches, without introducing an `Owned<T>` spelling. A `using` initializer can
consume a fully applied synchronous factory call, or directly `await` an async
factory call. The resource and, for async factories, its task must never become
an ordinary first-class value. Resource parameters, locals, data storage,
closures, partial application, dynamic calls and generic forwarding remain
prohibited. Scope handles themselves cannot be returned.

Reject `using` inside an owned-returning factory in this first slice: cleanup
could fail after the resource is produced but before it transfers to its caller,
leaking the produced resource. Reject other expressions that can perform work
after owned creation; each successful factory exit must transfer exactly one
fresh resource immediately. Preserve C#-style cleanup precedence for consumers.

Service/interface method factories and transfers through `Result` or stored
`Task` values remain separate #45 work. This slice does not introduce general
borrowing, CLR interop, implicit registration or a resource provider runtime.

## Steps and affected files

1. Extend binding and type validation to recognize explicit owned-returning
   function signatures while preserving restrictions everywhere else. Check
   resource-return positions, direct call/await and safe forwarding; reject
   indirect escape paths, partials and operations after resource production.
2. Extend source resolution/emission only as needed for checked owned factories,
   cross-module calls and async acquisition; retain source diagnostics and APIs.
3. Add a runnable D# example and new feature tests, update implementation status
   and the resource contract with exact tested boundaries. Keep #45 open.

## Acceptance-to-test map

- Execute synchronous and async factories, both using forms, explicit return,
  branches, same-module and cross-module forwarding, acquisition order and
  cleanup once. Control async suspension, failures and cancellation.
- Reject arbitrary resource values/aliases, factory values/partials, unawaited or
  stored factory tasks, generic and data/container escapes, unproven return
  paths and any resource transfer crossing factory-owned cleanup. Confirm failed
  acquisition does not release a nonexistent resource.
- Preserve existing immutable-data and resource non-escape tests; generated C#
  must build with warnings as errors. No old tests are changed.

## Verification

Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh` and
`bash .github/ai/verify.sh`, then check, compile and execute the D# example.
Record results and fix CI failures on the same PR.

### Verification results

- `bash .github/ai/bootstrap.sh` passed.
- `bash scripts/verify-compiler.sh` passed 18 compiler tests and 378 feature
  tests. The new suite executes generated C# with warnings as errors. An earlier
  full run exposed a generated-test assembly identity collision; the new test
  harness now uses unique assembly names, and the two resource suites passed
  together four times before the successful full rerun.
- The D# example checks, compiles and runs; its synchronous and asynchronous
  answers both return 42.
- `bash .github/ai/verify.sh` passed documentation and 25 automation tests;
  the local isolated compiler stage could not download its pinned SDK image
  because the registry returned `Forbidden`. GitHub CI runs that stage.

Commands used SDK 11.0.100 via `/workspace/work/dotnet11`,
`XDG_RUNTIME_DIR=/tmp/dsharp-xdg-verify`,
`DOTNET_CLI_HOME=/tmp/dsharp-dotnet11-home`,
`NUGET_PACKAGES=/tmp/dsharp-dotnet11-packages`, and
`npm_config_cache=/tmp/dsharp-npm-cache` for writable caches.
