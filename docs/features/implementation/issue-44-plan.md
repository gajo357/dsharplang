# Issue 44: Default interface method bodies

**Base:** `e636155` (merged #43, PR #167).
**Issue:** [#44](https://github.com/gajo357/dsharplang/issues/44).
**Status:** Implemented for review.

## Scope and dependencies

Implement the documented default method behavior in syntax reference §12 and
issue #44 using the interfaces, services, async and imports already merged in
#43, #41 and #46. A body supplies a default; an omitted signature without a body
is still required. Calls inside a default dispatch to the selected service's
implementation; an explicit service method takes precedence. A service must
explicitly resolve conflicting defaults from multiple interfaces. A bodyless
signature remains required even if another interface supplies a matching default. Interfaces
still have no state; there is no service inheritance, implicit selection,
registration, method value, or change to capability escape rules.

## Steps and affected interfaces

1. Extend interface parsing and binding to retain optional method bodies and
   check each against its declared parameter, return and async contract, including
   references to other methods on that interface. Preserve spans and diagnostics.
2. Resolve omitted service methods against applicable defaults, reject ambiguous
   defaults and incompatible contracts, and lower defaults and calls with virtual
   dispatch without changing evaluation, async or safety semantics.
3. Update multi-file name resolution for default body scopes. Add a runnable D#
   example and feature tests, and update implementation status.
4. Migrate only the #43-era assertion in `ServiceAcceptanceTests.cs` that rejects
   a default method body: it directly contradicts the requested #44 feature.
   Keep all unrelated existing tests and negative coverage intact.

## Acceptance-to-test map

- AC1: interface with required and default methods; missing required members and
  malformed/default bodies rejected; mixed declarations and imports.
- AC2: execute defaults calling required and other default methods, service
  overrides, distinct service instances, evaluation order and failures.
- AC3: reject conflicting defaults without an explicit implementation, execute
  its resolution and exercise async suspension, return values and faults.
- Regression/safety: no state/inheritance; dependency and capability boundaries;
  generated C# builds with warnings as errors; existing features remain intact.

## Verification

Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`,
`bash .github/ai/verify.sh`; check, compile and execute the D# example. Record
actual results and fix any CI failure on the same PR branch.

## Unresolved decisions

None needed for the documented bounded feature. Reject unsupported method values
and unrelated interface conversions as in #43; avoid adding surface syntax.

### Actual results

- `bash .github/ai/bootstrap.sh` passed.
- `bash scripts/verify-compiler.sh` passed 18 compiler tests and 376 feature
  tests. The combined #43/#44 focused tests passed again after two additional
  name-shadowing and nested-capture cases were added.
- The D# example checks, compiles and runs: `Demo.answer()` returns 42.
- `bash .github/ai/verify.sh` passed documentation and 25 automation tests.
  The local isolated compiler stage could not download its pinned SDK image
  because the image registry returned `Forbidden`; GitHub CI runs that stage.

Commands used pinned SDK 11.0.100 via `PATH=/workspace/work/dotnet11:$PATH`,
`XDG_RUNTIME_DIR=/tmp/dsharp-xdg-verify`,
`DOTNET_CLI_HOME=/tmp/dsharp-dotnet11-home`,
`NUGET_PACKAGES=/tmp/dsharp-dotnet11-packages` and
`npm_config_cache=/tmp/dsharp-npm-cache` for writable caches.
