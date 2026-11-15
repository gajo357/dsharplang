# Issue 174: Immediate Result transfer of owned resources

**Base:** `954670a` (merged #173).
**Issue:** [#174](https://github.com/gajo357/dsharplang/issues/174), a bounded child of [#45](https://github.com/gajo357/dsharplang/issues/45).
**Status:** Implemented for review; #45 remains open for remaining ownership boundaries.

## Scope and safety

Support explicitly declared, nonnullable standard `Result<Resource,E>` as a
transfer-only result from checked nongeneric functions and service/interface
methods, including supported defaults and async forms. Use ordinary D# syntax:
`Success(Cursor(...))`, `Error(error)` and
`using cursor = (await store.open())?`. `E` must be supported immutable data.
The success payload must be freshly constructed or directly transferred from a
checked producer; the entire owned result must be directly returned/forwarded
or immediately consumed by `?` in a `using` initializer. Preserve exact standard
Result identity, existing error-type matching, evaluation order and C# cleanup.

Do not make resources or their Results ordinary immutable data. Reject aliases,
locals, normal matching/equality, arguments, containers, nullable/nested or
user-defined Result wrappers, generic forwarding, closure/function/method values,
partial applications and unawaited/stored tasks. Restrict `Success` ownership to
checked return positions; bind arguments in ordinary context. Every owned-return
body must be checked, and must not contain `using`, which could abandon a pending
resource on a cleanup fault. On `Error` no inner resource is acquired; outer
scopes still unwind normally. An async producer must be directly awaited before
`?`; cancellation or fault before acquisition performs no inner release.

General first-class owned values, general borrowing, stored tasks/results, CLR
interop and runtime providers remain out of scope. Do not close #45 unless its
full scope and ownership boundaries are independently complete.

## Implementation steps and files

1. Extend exact owned-transfer signature and expression checks in
   `src/DSharp.Compiler/Compiler.cs` for standard Result<Resource,E>, without
   relaxing generic/data validation. Handle checked functions, methods and
   default bodies, forwarding and the existing C# backend representation.
2. Bind immediate `?` acquisition and direct async await safely. Preserve
   source/nominal identity, propagation, evaluation and lifetime on every exit.
3. Add executable D# example, new feature coverage, and update status and
   contract. Reuse the existing resource test aggregation to respect the bounded CI report.
   Migrate the single #170 assertion that rejected the now-valid Error-only
   owned-Result producer; all unrelated existing assertions remain.

## Acceptance-to-test map

- Sync/async functions and polymorphic methods/defaults; Success and Error,
  direct/forwarded returns, receiver/argument order, imports and nominal errors.
- Controlled async gates, acquisition fault/cancellation and outer cleanup;
  ensure exactly-once release and C#-style cleanup fault precedence.
- Reject alias/match/storage/capture/partial/generic/nullable/nested/foreign
  Result escapes, unawaited tasks, mismatched E, resource parameters, unsafe
  nested producers and `using` in producers. Test user-defined Result shadowing.
- Preserve existing behavior; build generated C# with warnings as errors and
  run both existing and new feature coverage.

## Verification

Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`,
`bash .github/ai/verify.sh`, then check, compile and execute the D# example.
Record results and fix CI failures on the same PR branch.

### Verification results

- `bash .github/ai/bootstrap.sh` passed.
- `bash scripts/verify-compiler.sh` passed 18 compiler tests and 377 feature
  test results, including the new owned-Result coverage run from the existing
  resource test. Generated C# builds with warnings as errors. One obsolete
  #170 negative asserting that an Error-only producer must fail was removed;
  the newly valid case is covered positively and other assertions are retained.
- The D# example checks, compiles and runs; sync and async answers return 42.
  Independent probes covered 57 ownership/shadowing cases plus generated
  execution for propagation, cancellation and cleanup precedence.
- `bash .github/ai/verify.sh` passed documentation and 25 automation tests.
  The local isolated compiler stage could not download the pinned SDK image
  because the registry returned `Forbidden`; GitHub CI runs that stage.

Commands used SDK 11.0.100 via `/workspace/work/dotnet11`,
`XDG_RUNTIME_DIR=/tmp/dsharp-xdg-verify`,
`DOTNET_CLI_HOME=/tmp/dsharp-dotnet11-home`,
`NUGET_PACKAGES=/tmp/dsharp-dotnet11-packages`, and
`npm_config_cache=/tmp/dsharp-npm-cache` for writable caches.
