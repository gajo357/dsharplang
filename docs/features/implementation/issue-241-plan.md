# Issue #241 — typed actor dispatcher surface

Issue: https://github.com/gajo357/dsharplang/issues/241
Base: `00a87107e66b4775213f2383398592a549f8546d` (`origin/main`).

## This slice and dependency reconciliation

This is a planning slice. It does not implement the three operations or close
#241. The #52 parent remains open until its own codec and dispatcher integration
criteria are proven. The handoff #239, decisions #13/#14, runtime setup #9,
checked actors #51 and trigger children #256/#258/#260/#262/#264 are merged.
The #52 plan identifies #241 as independent of the remaining #52 work and as
the owner of its targeted operation evidence. #53 and #54 remain downstream.

The #241 issue says to use the approved runtime interface from #9. The merged
`issue-9-plan.md` explicitly chose **no public runtime protocol**, and
`src/DSharp.Runtime/RuntimeAssembly.cs` is only an internal marker. It provides
neither a dispatcher nor receipts, and generated projects currently do not
reference that assembly. Treat #9 as enabling the runtime project and verifier,
not as evidence that an ABI exists. DEC-03 sets the operation semantics while
leaving receipt, fault and lookup spelling illustrative. #61, #57 and #69 own
durable acceptance, final waits and publication, respectively; a recording
dispatcher proves compiler lowering, not any of those runtime properties.

## Required contract

- A targeted Invoke and targeted Dispatch resolve the same explicit public
  actor identity and check their single argument against that actor's resolved
  Handle input. One await of Invoke yields the exact Handle result, including
  Unit and a business `Result.Error` unchanged. One await of targeted Dispatch
  yields an invocation-acceptance receipt, not the result.
- Untargeted Dispatch derives a message contract from its argument and yields a
  publication-acceptance receipt, regardless of whether zero, one or several
  subscribers exist. It must never infer a target. No registration creates an
  implicit subscription. Argument evaluation occurs once before submission.
- Reject invalid targets, incompatible or ambiguous message identities,
  targetless Invoke, discarded/unobserved awaitables and existing direct actor
  construction, injection, Handle calls and method groups. Preserve current
  async, resource-lifetime and lexical/name-resolution rules. Trigger bindings
  do not change whether an actor can be explicitly targeted.
- Both operations use the application and invocation's scoped dispatcher;
  neither may use a mutable process-global instance, reflectively invoke
  Handle, construct an actor or bypass the public route for a co-located actor.
  Faults and uncertain acceptance are not converted to business errors.

## Proposed integration boundary and implementation order

1. First implement a bounded typed compiler/runtime seam within #241's own
   scoped child, adding only the necessary runtime source and new runtime
   feature tests under the paths enabled by #9. Define typed route descriptors
   carrying a host-scoped public target identity with input/result type parameters;
   both targeted operations consume the same descriptor. The recording seam
   returns `Task<TResult>` for Invoke, distinct typed invocation and publication
   receipt awaitables for Dispatch, and receives the actual message exactly
   once. Publication uses a typed message contract without an actor target.
   Receipt values in a recording test are tokens only: no durability, public
   wire format, receipt lookup or production implementation is implied.
   Coordinate target/message identities with #54/#58 and #78 without claiming
   a cross-deployment durable identity or their runtime implementations.
2. Lower the three contextual forms using the resolved #51 actor model and
   existing Task/await machinery. Establish an explicit host-supplied scoped
   dispatcher at a generated entry boundary and thread it through all callable
   paths that can execute dispatch, including actor helpers, ordinary async
   functions and closures. Preserve separate application/invocation contexts
   across interleaved awaits and existing generated entry points. If a path
   cannot preserve the scope, reject that path explicitly until implemented;
   do not substitute a process-global value or silently capture another host.
   Generic descriptors must work with generated nominal types and Unit without
   caller-supplied result types, reflection, casts or fabricated actor objects.
   Define and test how contextual `Invoke` and `Dispatch` forms interact with
   ordinary functions of the same name; do not silently capture existing calls.
3. Implement the typed route and receipt seam with runtime feature tests in a
   bounded child, followed by its compiler binding and emission consumer in a
   separate child. Before starting either, search live issues and branches for
   an existing owner and document the concrete C# signatures, generated-project
   reference and host entry mechanism in its plan. Verify emitted type
   visibility, including generated Unit, and how D# recognizes distinct opaque
   receipts without exposing constructors or fixing a storage-ID format. Do not close #241
   until all criteria below and #101 checks 1 and 19 have executed evidence.
   #54 handles registration, #61/#69 actual acceptance and #57 public waits.

These are proposed integration constraints, not a claim of an already shipped
runtime API. They make the missing #9 interface an explicit implementation item
under #241 rather than a new requirement for user sign-off. Choose exact names
and visibility in the implementation plan after inspecting the generated
project boundary; preserve the typed and scoped properties above.

## Acceptance-to-test map for implementation

| Requirement | Evidence to add in new tests |
| --- | --- |
| Exact Invoke result | Execute generated C# with a recording dispatcher for value, Unit and business `Result.Error`; one await yields precisely each value and the same target route used by targeted Dispatch. |
| Both Dispatch forms | Targeted receipt and targetless publication receipt are distinct typed results; publication remains publication with zero, one and multiple simulated subscribers, without waiting for their results. |
| Correct typing and isolation | Wrong actor/message, targetless Invoke, ambiguity, unobserved awaits, actor construction/injection and direct Handle rejected with locations. Exercise single/multiple modules, typed Queue/Event/HTTP trigger actors and CLI check/compile. |
| Scoped evaluation | Record once-only argument evaluation and route identity; interleave separate host/invocation contexts across awaits, ordinary functions, helpers and supported closures. Observe no global leakage or direct actor invocation. |
| Runtime limits | New runtime feature tests cover the selected typed seam and test doubles. No assertion substitutes a recording receipt for real durable acceptance, subscriber isolation, waiting or fault recovery. |

## Files and verification for this planning slice

Only this plan is changed. Review it against #241, #52, #9, DEC-03, the actor
handoff, current compiler and runtime, and the live issue/PR states. Run
`bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-compiler.sh`, `bash scripts/verify-runtime.sh`, and
`git diff --check`. Record results and any environmental limitation below.
Documentation checks do not establish runtime or compiler behavior.

## Results

- Bootstrap succeeded. Documentation validation passed (121 files and 308
  local links), and all 31 automation tests passed. The full local verifier
  could not pull the pinned MCR SDK image layers (`Forbidden`), so its isolated
  compiler and runtime stages need exact-head hosted evidence.
- The unchanged compiler verifier passed 18 baseline and 396 feature tests
  without skips, and the runtime verifier passed 2 baseline and 1 feature test.
  These ran from a byte-checked short source snapshot; the feature TRX was
  509,900 bytes, below the unchanged 512,000-byte guard. The final plan wording
  was updated after the snapshot; no compiler or runtime source changed.
