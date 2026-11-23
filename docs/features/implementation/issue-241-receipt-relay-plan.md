# Issue #241 — direct opaque receipt relays

Base: `00c03c5c99b59a1234a4d072cc644e0108fd5fa7` (`origin/main`).
Issue: https://github.com/gajo357/dsharplang/issues/241

## Scope and dependencies

Advance #241 after the merged runtime seam #267 and Dispatch/compiler slices
#271/#273/#275. Preserve the private distinction between invocation and
publication receipts. Permit an otherwise empty inferred async ordinary module
function whose final expression directly awaits one fully applied nongeneric
function already proven to produce one of these receipts. Propagate the exact
kind and host dispatcher over finite resolved-call chains, including qualified
modules and reordered declarations. Keep #241 and #52 open; production admission,
publication, registration, delivery, and receipt lookup remain separate work.

## Steps and affected interfaces

1. Extend the existing direct-Dispatch normalization with a conservative fixed
   point seeded by its established receipt entries. Allow only a root directly
   awaited resolved call in an otherwise empty async body, with no parameter or
   type-name shadowing, full arity and nongeneric caller/callee. Forward only
   direct data parameters or Unit, excluding capability/execution parameters
   and resource-bearing argument expressions. Unseeded cycles and ordinary
   omitted async results retain their current diagnostics.
2. Extend bound effect validation only for those proven root relay edges of the
   same private receipt kind. Preserve exact hidden scoped dispatcher forwarding;
   reject stored/discarded receipts, captures, partials, unawaited/indirect
   calls, lambdas, actors, helpers, HTTP callbacks and resource scopes.
3. Add generated-host feature evidence against the real runtime ABI. Exercise
   both receipt kinds through reordered and qualified relay chains, independently
   pending dispatchers, exact receipt/route/contract identity, once-only input and
   faults. Add negatives for unsafe uses, cycles, shadowing and ambiguity.
4. Update implementation status and this plan. Preserve existing tests, runtime
   interfaces, report guard and verification code.

Files: `src/DSharp.Compiler/Compiler.cs`, one new feature test, implementation
status and this plan. No new public receipt spelling or runtime protocol.

## Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-compiler.sh`, `bash scripts/verify-runtime.sh`, focused
feature/CLI checks, docs and `git diff --check`. Record actual outcomes and
any local isolated image limitation.

## Executed evidence

- Focused receipt relay, targeted Dispatch and publication groups passed. The
  generated host exercises exact receipt identities, qualified and reordered
  chains, Unit, independent pending host contexts, fault propagation, unsafe
  uses and multi-file CLI check/compile. Existing feature tests were preserved.
- Bootstrap succeeded. The full verifier passed documentation (129 files,
  308 local links) and all 31 automation tests, then could not pull the pinned
  MCR SDK image (`Forbidden`); hosted exact-head isolated evidence is required.
- The unchanged compiler verifier passed all 18 baseline and 403 feature tests
  without skips on a short source snapshot; changed source and feature test
  were byte-compared. The complete raw feature TRX was 503,536 bytes under the
  unchanged 512,000-byte limit. Runtime verifier passed 2 baseline and 5 feature
  tests. Documentation validation and `git diff --check` passed.
- Recording fakes do not prove actual acceptance, publication, delivery or
  durability. The parent #241 remains open.
