# Issue #271 — targeted Dispatch at scoped host entries

Issue: https://github.com/gajo357/dsharplang/issues/271
Parent: https://github.com/gajo357/dsharplang/issues/241
Base: `1f61ebe1e320c1d738a2d955ddaa30409a9a5170` (`origin/main`).

## Scope and decisions

Extend the bounded, host-supplied dispatcher entry from #269 with directly
awaited `Dispatch<Actor>(message)`. #267 supplies the typed runtime seam.
Both targeted operations resolve the same public actor and exact Handle input,
use the same `ActorRoute<TInput,TResult>` descriptor and supplied dispatcher,
and evaluate the message once. One await of Dispatch yields the actual opaque
`DSharp.Runtime.InvocationReceipt`, not the business result. Only the recording
fake produces receipts in these tests; #61 owns real durable acceptance.

For this first slice the receipt is an internal, nominal compiler type with no
source constructor or ID and no implicit equivalence to a user-defined type
named `InvocationReceipt`. Permit it only as the inferred result of a directly
awaited targeted Dispatch host entry; reject explicit receipt annotations,
nullable or container/data embedding, equality, local storage and any other
unsupported escape until those contracts are deliberately implemented. Generated
host signatures expose `Task<DSharp.Runtime.InvocationReceipt>` and use the
same explicit dispatcher parameter/runtime DLL as #269. This is a narrow
receipt typing rule, not a serialization or public receipt lookup design.

Resolve ordinary local/module/actor names `Dispatch` before contextual syntax.
Fail closed for untargeted Dispatch until the publication child, invalid or
ambiguous target/input, unobserved operation, actor/helper/lambda contexts,
D# calls or captures of effectful entries, and HTTP callback capture. Reuse
#269's scope safety and preserve ordinary `Dispatch` functions and all existing
tests. No production dispatcher, host registration, publication or durability
is implemented; keep #241 and #52 open.

## Steps, interfaces and acceptance evidence

1. Extend `src/DSharp.Compiler/Compiler.cs` with an unforgeable internal receipt
   identity and targeted Dispatch binding/emission, with source-location
   diagnostics and exact actor/message identity. Keep the two route uses shared
   and preserve the existing runtime-reference flag and public non-dispatch
   signatures. Handle multi-file names using existing resolution; do not modify
   the runtime ABI or project manifests.
2. Add `tests/features/TargetedDispatchEntryTests.cs` and a D# example. Compile
   and execute a generated host against the actual runtime ABI and a controlled
   recording dispatcher: Invoke/Dispatch use the identical route, Dispatch
   remains pending until fake acceptance and yields the same receipt, arguments
   execute once, and independent hosts remain isolated. Test nominal imports,
   wrong inputs/targets, targetless/unobserved use, shadowing, unsupported
   context/capture/receipt forms and CLI check/compile. Run the managed test
   host DLL with `dotnet` so the isolated no-exec temporary mount is respected.
3. Update implementation status only for tested behavior. Run bootstrap, full
   verifier, focused tests, compiler/runtime verifiers, CLI checks and
   `git diff --check`. Record actual results and exact-head hosted checks.
   Preserve every existing test, report-size guard and verifier safeguard;
   fix any actual CI failure on this same PR branch.

## Verification

- Bootstrap passed; documentation validation passed for 124 files and 308
  local links, and all 31 automation tests passed. The full local verifier
  could not pull the pinned MCR SDK image (`Forbidden`), so isolated compiler
  and runtime checks need hosted exact-head evidence.
- Focused Dispatch and existing Invoke integration tests passed, including
  generated host execution, shared route, pending fake acceptance, exact
  receipt, separate hosts, source shadowing and rejection cases.
- On a byte-checked `/tmp/XXX` snapshot, 18 baseline and all 398 feature
  tests executed successfully, but the unchanged 512,000-byte TRX guard
  rejected their 512,433-byte report. The Compiler workflow now creates an
  atomic private one-character `/tmp` checkout while keeping exact SHA and
  cleanliness checks, all tests, the same size guard and report artifact.
  This saves repeated path bytes only; it is not a long-term sharding design.
  In a collision/symlink probe the allocator skipped existing entries and
  created a private directory. The unchanged compiler verifier then passed
  18 baseline and 398 feature tests from a byte-checked `/tmp/a` snapshot;
  the 510,841-byte TRX passed the unchanged guard. Runtime verification passed
  2 baseline and 5 feature tests from the same snapshot. The final plan/status
  wording was updated after that snapshot. Hosted exact-head checks are still
  required before merge.

## Same-branch lookup correction

Review found that multi-file resolution could leave a visible source type named
`Dispatch` or `Invoke` in its original spelling and accidentally select the
contextual actor operation. Preserve the resolved nominal identity before
binder dispatch, including ambiguity across visible nominal kinds, while keeping
local, generic and ordinary value lookup precedence. The receipt prepass also
recognizes a top-level let statement in a manually supplied syntax tree even
when the parallel bindings collection is absent. The grouped Dispatch and
Invoke integration tests pass with added multi-file nominal-shadow, ambiguity,
hidden-type and direct Binder regression cases. Hosted checks on the corrected
head remain necessary.
