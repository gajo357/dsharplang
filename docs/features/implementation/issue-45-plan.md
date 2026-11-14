# Issue 45: Scoped resource lifetime

**Base:** `a9fe0b5` (merged #44 / PR #168).
**Issue:** [#45](https://github.com/gajo357/dsharplang/issues/45).
**Status:** Needs decision; this branch records a contract proposal only.

## Prerequisites and scope

Async functions (#41), interfaces and explicit services (#43), and lifecycle
[DEC-07](DEC-07.md) are merged. DEC-07 adopts bounded ownership, exactly-once
cleanup, awaiting async disposal, and reporting cleanup failures separately
without replacing the primary outcome. It intentionally leaves the exact public
API and compiler ownership enforcement open. The syntax reference's `using`
example is proposed rather than adopted, and foreign disposal adapters are part
of the separately unadopted DEC-06. No resource type or cleanup protocol exists
in the current compiler.

Before code, define how scoped resources are declared and acquired, their allowed
uses and transfers, how sync versus async cleanup is selected, and how an earlier
typed failure, fault or cancellation is preserved and a cleanup failure is
observable. These affect public syntax, ownership and runtime behavior. The
[contract proposal](issue-45-contract.md) makes the decisions reviewable and
identifies any choices requiring the maintainer. This proposal does not implement
or close #45.

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
