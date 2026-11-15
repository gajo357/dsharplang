# Issue 45: Scoped resource lifetime

**Base:** `a9fe0b5` (merged #44 / PR #168).
**Issue:** [#45](https://github.com/gajo357/dsharplang/issues/45).
**Status:** The direct-construction slice on PR #169 follows C# `using` semantics, as directed by the maintainer.

## Scope and decisions

The maintainer approved scoped owned resources, then explicitly directed D# to
follow C# cleanup behavior and remove the extra reporting clause. This supersedes
DEC-07's prior rule requiring separate reporting of a secondary cleanup fault for
this feature: if body and cleanup both fail, the cleanup exception is observable,
as with C# `using`. If cleanup succeeds, the original return, typed failure,
exception or cancellation is preserved.

Implement fresh `resource` declarations with `using handle = Resource(args)`
lasting through the remainder of its enclosing lexical block, and
`using handle = Resource(args) { ... }` for an explicit nested scope. Cleanup is
synchronous or awaited according to the declared release operation and runs once
for every successful acquisition. Nested resources release in reverse acquisition
order. Preserve receiver-only handles, immediate awaiting of async resource
operations, capability restrictions, constructor argument evaluation order,
lexical binding and existing source compatibility. No explicit cleanup callback.

The direct-construction slice does not support provider-returned owned tokens or
transfers through `Task` or `Result`; keep #45 open. No general borrowing,
application host, foreign disposable adapters or transaction behavior is claimed.

## Implementation and files

1. Adjust syntax, binder, emitter and multi-file resolver in `src/DSharp.Compiler`
   for declaration and braced forms, safe lexical lifetime and C#-like cleanup.
2. Adjust the still-unmerged scoped-resource feature tests and D# example for the
   explicitly requested change; preserve all unrelated established tests.
3. Update the resource contract and implementation status with precise behavior.

## Acceptance-to-test map

- Braced and enclosing-block scopes: execute both, nested blocks and loops;
  verify acquisition order, no use before binding or after scope, no shadowing,
  and inner-to-outer release including failure while acquiring a later resource.
- Cleanup: test exactly once on normal completion, early return, `Result` `?`,
  typed errors, faults and cancellation. If cleanup also faults, the cleanup
  failure replaces the in-flight outcome; if cleanup succeeds, preserve it.
- Async: use controlled tasks to verify release is awaited, including on
  cancellation; reject unawaited resource operations or escaping tasks.
- Safety: reject handle aliases, arguments, data, return, lambda captures,
  partial applications, direct release and non-fresh resources. Verify source
  paths and cross-module name resolution; compile generated C# with warnings as
  errors. Provider-returned resources remain outside this slice.

## Verification

Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`, and
`bash .github/ai/verify.sh`. Check, compile and execute the D# example. Record
actual results; fix any CI failures on the existing PR branch.

### Verification results

- `bash .github/ai/bootstrap.sh` passed.
- `bash scripts/verify-compiler.sh` passed 18 compiler tests and 377 feature
  tests, including generated C# built with warnings as errors for both using
  forms, lifetime boundaries and cleanup precedence.
- The D# example checks, compiles and runs; `Demo.run()` returns 42.
- Independent generated-code probes passed for nested scopes, async cleanup,
  cancellation, generic and nullable results, import resolution and escapes.
- `bash .github/ai/verify.sh` passed documentation and 25 automation tests. Its
  local isolated compiler stage could not download the pinned SDK image because
  the registry returned `Forbidden`; GitHub CI runs that stage.

Commands used SDK 11.0.100 via `/workspace/work/dotnet11`,
`XDG_RUNTIME_DIR=/tmp/dsharp-xdg-verify`,
`DOTNET_CLI_HOME=/tmp/dsharp-dotnet11-home`,
`NUGET_PACKAGES=/tmp/dsharp-dotnet11-packages`, and
`npm_config_cache=/tmp/dsharp-npm-cache` for writable caches.
