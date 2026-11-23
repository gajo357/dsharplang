# Issue #45 — owned resource scopes and actor waits

Base: `fa912a80018e104b19451d5d9eec88d1c67ca46e` (`origin/main`).
Issue: https://github.com/gajo357/dsharplang/issues/45

## Scope and prerequisites

Advance the remaining actor-boundary portion of #45 after the merged async,
resource and dispatcher slices #17/#41/#43/#44/#170/#172/#174/#269/#275.
Current source resource declarations do not identify which owned handles are
transactions. A real transaction can currently remain open across a direct or
transitive actor Invoke. Until a transaction capability is distinguished, reject
actor dispatcher operations while a locally owned `using` scope is active as a
conservative compiler boundary. This does not classify all resources as
transactions or forbid normal awaited resource operations. It does not equate a
host-owned dependency with an active transaction, and it preserves actor calls
after local disposal. Keep #45 and #241 open; #244 owns database/provider
semantics and #240 owns service fixture migration.

## Steps and files

1. Detect direct actor operations with a live locally owned resource in the
   binder, preserving precise source diagnostic location. Track the lexical
   using body, not acquisition before a scope begins or code after it ends.
2. After computing the existing resolved dispatcher call graph, reject calls to
   dispatcher-dependent functions under `BoundUsing` bodies; traverse nested
   scopes, branches and modules without mistaking ordinary resource awaits for
   actor dispatch. Preserve resource cleanup and existing function signatures.
3. Add grouped feature tests for direct/transitive cross-actor awaits inside
   braced, declaration and nested scopes, diagnostics and CLI paths. Verify
   positive ordinary awaited resource operations, invocation after disposal,
   unaffected host-owned dependency calls, and cleanup/fault behavior. Preserve
   all prior tests and checks.
4. Update implementation status with the explicit conservative restriction and
   remaining transaction capability/provider work.

Files: `src/DSharp.Compiler/Compiler.cs`, one new feature test, this plan and
implementation status. No new transaction syntax, provider, runtime ABI or
storage guarantee is introduced.

## Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-compiler.sh`, `bash scripts/verify-runtime.sh`, focused
feature/CLI checks, docs and `git diff --check`. Record actual evidence and any
local isolated-image limitation.

## Executed evidence

- Focused resource-boundary and existing actor-body Invoke groups passed. The
  new generated host exercises an ordinary awaited resource operation and
  asynchronous cleanup before a later Invoke, fault ordering and a host-owned
  dependency; negative cases cover direct/transitive Invoke, Dispatch and
  publication, nested scopes/acquisitions, source locations and CLI checks.
- An initial full run exposed a changed diagnostic for an already unsupported
  actor-body form. The guard was reordered to retain the existing DS2047; both
  focused groups and the complete compiler verifier then passed without changing
  any prior test.
- Bootstrap succeeded. The final full verifier passed documentation (130 files,
  308 links) and all 31 automation tests, then could not pull the pinned MCR SDK
  image (`Forbidden`); hosted exact-head isolated evidence is required.
- The unchanged compiler verifier passed 18 baseline and 404 feature tests
  without skips on a short source snapshot; changed compiler and new test were
  byte-compared. The complete raw feature TRX was 504,663 bytes under the
  unchanged 512,000-byte cap. Runtime verifier passed 2 baseline and 5 feature
  tests. Documentation validation and `git diff --check` passed.
- This is a conservative restriction on locally owned resources, not proof of
  transaction classification or provider behavior. #45 remains open.
