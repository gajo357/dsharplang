# Issue 45: Scoped resource lifetime

**Base:** `a9fe0b5` (merged #44 / PR #168).
**Issue:** [#45](https://github.com/gajo357/dsharplang/issues/45).
**Status:** The maintainer approved the owned-resource and explicit-reporter direction; the bounded direct-construction slice is implemented for review.

## Prerequisites and scope

Async functions (#41), interfaces and explicit services (#43), and lifecycle
[DEC-07](DEC-07.md) are merged. DEC-07 adopts bounded ownership, exactly-once
cleanup, awaiting async disposal, and reporting cleanup failures separately
without replacing the primary outcome. It intentionally leaves the exact public
API and compiler ownership enforcement open. The syntax reference's `using`
example is proposed rather than adopted, and foreign disposal adapters are part
of the separately unadopted DEC-06. No resource type or cleanup protocol exists
in the current compiler.

The maintainer approved the owned-resource and explicit-reporter direction.
The [contract](issue-45-contract.md) records the broader design and remaining
provider-transfer choices. This PR implements the bounded slice below; #45 stays
open until provider-returned owned resources and their acceptance tests land.

## Adopted first implementation slice

The maintainer approved proceeding with owned resources and an explicit cleanup
reporter. Implement fresh D# `resource Name(dependencies) { ... }` declarations
and `using handle = Name(args) report reporter { ... }`; the compiler alone
invokes exactly one `release(): Unit` or `async release(): Unit`. The reporter
is an already-bound non-null `(string) -> Unit` function, passed by identifier;
it receives a safe resource-name diagnostic for each secondary cleanup fault.
A function reference or arbitrary expression is not evaluated as a reporter.
The callable may fault: a generated combined fault must retain the primary,
cleanup and reporter failures. Source handles are receiver-only, cannot be
aliased or passed/returned/captured/stored, and async operations are immediately
awaited. Only direct fresh resource construction is accepted; ordinary services
are never implicitly owned. Provider-returned owned tokens, including acquisition
through `Task` or `Result`, remain separate work, so this slice must not close
#45. Do not claim general resource borrowing or foreign adapter behavior.

For an escaping top-level Result Error, an exception, or cancellation plus a
cleanup fault, report that cleanup once and preserve the primary outcome when
reporting succeeds. For normal success or successful early return, surface a
cleanup fault. Reporter faults surface a combined failure retaining all causes.
Nested scopes release in reverse order. Neither a handled nor nested Error value
is treated as an escaping failure. The compiler's internal return signals must
retain their semantics across cleanup.

## Intended implementation steps

1. After adoption, parse and resolve the selected source forms while preserving
   spans, module/import behavior, and existing APIs.
2. Track resource ownership and lifetimes across acquisition, local scopes,
   control flow, generic calls, async suspension, data and closures; reject escape.
3. Generate exactly-once cleanup for normal completion, early return, typed
   failure, fault and cancellation, with the adopted dual-failure behavior.
4. Add executable D# examples and new feature tests; update implementation status.

## Acceptance-to-test map

- AC1: sync and async acquisition/cleanup; success, early return, typed error,
  fault, cancellation, failed acquisition and nested cleanup. Verify awaiting.
- AC2: reject escapes through returns, immutable data, closures, partial calls,
  tasks and indirect/generic routes; verify legitimate in-scope use.
- AC3: exactly-once cleanup, acquisition evaluation order, and observable primary
  and cleanup failures together, including typed errors and cancellation.

Use generated execution, controlled async gates, and counters; C# compiles with
warnings as errors. No implicit transaction rollback, general borrowing, CLR
interop, application host, or resource-provider behavior is claimed.

## Verification

For this proposal run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`
and `bash scripts/verify-compiler.sh`, recording local limitations. After adoption
run them again with feature tests and execute the D# example. Fix any PR CI failure
on its existing branch.

### Proposal verification

- `bash .github/ai/bootstrap.sh` passed.
- `bash scripts/verify-compiler.sh` passed 18 compiler and 376 feature tests;
  this is baseline evidence and does not demonstrate resource behavior.
- `bash .github/ai/verify.sh` passed documentation and all 25 automation
  tests. Its isolated compiler stage could not pull the pinned SDK image locally
  because the registry returned `Forbidden`; GitHub CI runs that stage.

Commands used SDK 11.0.100 from `/workspace/work/dotnet11`,
`XDG_RUNTIME_DIR=/tmp/dsharp-xdg-verify`,
`DOTNET_CLI_HOME=/tmp/dsharp-dotnet11-home`,
`NUGET_PACKAGES=/tmp/dsharp-dotnet11-packages` and
`npm_config_cache=/tmp/dsharp-npm-cache` for writable caches.

### Direct-construction slice verification

- `bash .github/ai/bootstrap.sh` passed.
- `bash scripts/verify-compiler.sh` passed 18 compiler and 377 feature tests,
  including the new scoped-resource execution and rejection suite. Generated C#
  in that suite builds with warnings treated as errors.
- The scoped-resource D# example checks, compiles and runs; `Demo.run()`
  returns 42.
- Independent generated-code probes covered nested cleanup faults, cancellation,
  callback faults, imported resource names and the nullable Result matrix.
- `bash .github/ai/verify.sh` passed documentation and 25 automation tests;
  its local isolated compiler stage could not pull its pinned SDK image because
  the registry returned `Forbidden`. GitHub CI runs that stage.

The #45 issue remains open for provider-returned ownership and its integration
coverage. The approved direction does not imply general borrowing or interop.
