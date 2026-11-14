# Issue 43: Interfaces and explicit services

**Base:** `28589c857a80f6fcf7f3b07ed01a5b8f715cbb39` (merged #30, #41 and #166).
**Issue:** [#43](https://github.com/gajo357/dsharplang/issues/43).
**Status:** The maintainer explicitly adopted the contract; implementation complete for review.

## Why a contract is needed

The syntax reference §12 illustrates a service implementing one interface, while
#43 requires multiple interfaces and checked polymorphic calls. `docs/LANGUAGE.md`
leaves service declaration, construction, call and dispatch syntax open, and
DEC-02 does not adopt those rules. The task queue and AGENTS.md prohibit silently
choosing public grammar or semantics. The accompanying
[contract](issue-43-contract.md) makes a bounded choice reviewable. The maintainer
explicitly adopted it in conversation. #42's task-valued list gap is independently
tracked and is not a listed #43 dependency.

## Implementation scope

Support typed interface signatures and services implementing one or more
interfaces, checked method conformance, explicit constructor dependencies,
service construction, interface-typed values, and direct polymorphic calls.
Preserve async semantics, ordinary call behavior, existing immutable-data and
builder/resource boundaries, and file-scoped import resolution. No interface
default bodies (#44), application registration, implicit service discovery,
implementation inheritance, new runtime host, or general CLR interop.

## Implementation steps and affected interfaces

1. Extend source syntax and the parser for the adopted contract; preserve source
   spans, documentation and existing public compiler model compatibility.
2. Resolve interface/service type, constructor, method and imported names without
   order-dependent winners. Extend binding/type checking for contracts, inferred
   implementation signatures, explicit dependencies, conflicts and dispatch.
3. Lower the checked subset to C# without exposing mutable data or silently
   introducing lifetime or registration behavior. Keep async calls task-valued.
4. Add executable D# examples and new feature tests; update implementation status.

Service and interface references are execution capabilities, not immutable data.
The existing data and generic-substitution checks will need to reject them
recursively in records, unions and collections, including through generic calls;
map/set keys and builder/resource non-escape checks must retain their guarantees.
Lowering must preserve receiver and constructor argument evaluation order and
must not expose internal dependency fields as mutable source data.

## Acceptance-to-test map

- AC1: declarations and checked implementation of one and multiple interfaces;
  reject state/default bodies on interfaces and implementation inheritance.
- AC2: reject missing or duplicate members, incompatible contracts and wrong
  implementations; test inferred signature/async markers and exact diagnostics.
- AC3: execute two implementations passed through the same interface, explicit
  dependency construction and member dispatch, including imported declarations,
  asynchronous results and errors. Reject wrong dependencies and invalid calls.
- Safety/regressions: preserve generic/nullability, task and builder boundaries,
  evaluation order, name shadowing and existing tests. Exercise emitted C# with
  warnings as errors; do not claim unimplemented resource or runtime guarantees.

## Verification

For the documentation proposal run bootstrap, documentation/link checks and the
repository verifier; document any unavailable isolated step. After adoption and
implementation also run `bash scripts/verify-compiler.sh`, compile and execute
the D# example, record actual results, and fix any CI failure on the same PR.

### Proposal verification

- `bash .github/ai/bootstrap.sh` passed with the pinned SDK and writable caches.
- `bash scripts/verify-compiler.sh` passed 18 compiler tests and 374 feature tests;
  these are regression checks, not implementation evidence for interfaces.
- `bash .github/ai/verify.sh` passed documentation and all 25 automation tests.
  Its isolated compiler stage could not download the pinned SDK image locally
  (`Forbidden`); GitHub CI must run that stage.

The local commands used the pinned 11.0.100 SDK on `PATH`,
`XDG_RUNTIME_DIR=/tmp/dsharp-xdg-verify`,
`DOTNET_CLI_HOME=/tmp/dsharp-dotnet11-home`,
`NUGET_PACKAGES=/tmp/dsharp-dotnet11-packages`, and
`npm_config_cache=/tmp/dsharp-npm-cache` for writable sandbox caches.

### Implementation verification

- `bash scripts/verify-compiler.sh` passed all 18 compiler tests and 375
  feature tests, including the new service acceptance coverage.
- The acceptance tests compile generated C# with warnings treated as errors and
  exercise explicit dependencies, polymorphic and async dispatch, imports,
  evaluation order, diagnostics, and capability non-escape.
- `examples/interfaces-services.ds` is checked, compiled and run as a D#
  example; its `Demo.answer()` returns 42.
- `bash .github/ai/verify.sh` passed documentation and all 25 automation tests;
  its isolated compiler stage is checked by GitHub CI because the local image
  registry returns `Forbidden`.
