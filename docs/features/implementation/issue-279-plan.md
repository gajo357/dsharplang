# Issue #279 — resource-free actor Handle Invoke

Base: `324ef2c8d2083efe4bc21064568a637424d78bc2` (`origin/main`).
Issue: https://github.com/gajo357/dsharplang/issues/279

## Scope and dependencies

Advance #241 after the merged dispatcher seam #267 and compiler Invoke slices
#269/#275. Permit only a no-dependency actor Handle whose sole result directly
awaits targeted Invoke with its own Handle input. Its generated internal body
receives the invocation host's typed dispatcher explicitly, preserving the
public route and exact result. This is an internal compiler seam, not an actor
executor, registration, delivery or durability implementation. Keep #241/#52
open. General live-resource/transaction boundary integration remains #45.

## Steps and affected files

1. Before binding, identify only the checked Handle synthetic body with zero
   dependencies and an otherwise empty body whose final expression is a directly
   awaited targeted Invoke of the unshadowed intrinsic using the input parameter.
   Keep all other actor methods, lambda/indirect calls, local statements,
   resource scopes and direct actor calls fail-closed.
2. Reuse the typed scoped dispatcher parameter and route emission for the
   permitted body, preserving every unaffected body signature and diagnostics.
   Record whether the checked actor's internal body needs that parameter in
   actor metadata so a host can select the signature without reflection.
3. Add a new generated-host feature test against the real runtime ABI covering
   multi-module target identity, two independently interleaved dispatchers,
   pending results, exact message/route/result, fault propagation, and unsafe
   forms. Exercise CLI and keep resource/transaction cases rejected.
4. Migrate exactly the obsolete negative that asserts this newly supported
   resource-free Handle form fails; retain helper, lambda and unsafe negatives.
   Update status and record exact verification.

Files: `src/DSharp.Compiler/Compiler.cs`, a new `tests/features/` test,
the one authorized negative fixture in `ActorInvokeEntryTests.cs`, this plan,
and implementation status. No runtime interface or dependency is changed.

## Acceptance map

| Requirement | Evidence |
| --- | --- |
| Explicit scoped Invoke | Execute internal Handle bodies with two recording dispatchers, precise route and input, independently pending completions, exact value and fault. |
| Isolation and unchanged ABI | Assert host contexts do not leak, module target is correct, per-actor metadata identifies the dispatcher requirement, and an unaffected Handle keeps its old signature. |
| Resource and escape limits | Reject actor dependencies, local statements, using/transaction scopes, helpers, lambdas, unawaited calls and direct Handle/construction bypass. |
| Verification | Run bootstrap, full verifier, compiler/runtime verifiers, focused test and CLI; record actual evidence and any MCR pull limit. |

## Decisions

No durable acceptance, scheduler ABI, public actor-construction surface or
resource-crossing policy is selected here. Those remain with #45/#56/#61.

## Executed evidence

- The focused actor-body and existing Invoke entry tests passed after rebuilding
  the compiler and CLI. The new generated host executes with two interleaved
  fake dispatchers, checks exact route/message/value/fault propagation and
  unaffected Handle ABI. Single- and multi-file checked actor metadata identify
  whether the Handle needs the dispatcher. Negative and CLI cases passed.
- Bootstrap succeeded. The full verifier passed documentation (128 files,
  308 local links) and all 31 automation tests, then could not pull the pinned
  MCR SDK image (`Forbidden`); hosted exact-head isolated evidence is required.
- The unchanged compiler verifier passed all 18 baseline and 402 feature tests
  without skips on a source snapshot; changed compiler and test files were
  byte-compared. The complete feature TRX was 510,521 bytes under the unchanged
  512,000-byte cap. Runtime verifier passed 2 baseline and 5 feature tests.
  Documentation validation and `git diff --check` passed.
- The host dispatcher is a recording fake, not an actor executor or durable
  admission. Resource-dependent Handles and other body forms remain rejected.
