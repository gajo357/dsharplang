# DEC-03: Actor invocation, dispatch and failure contract

**Status:** Draft proposal for review; not adopted or implemented.  
**Issue:** [#13](https://github.com/gajo357/dsharplang/issues/13).  
**Base:** `c41b11f21d3ee0f66f994de1ea3bcd0ca61698d1`.

## Boundary and adoption

This proposal replaces the former callable-worker and queue-agent split, direct
worker calls, `call(worker)` adapter and `CallError<E>` wrapping proposal. The
[actor handoff](../DSharp_Actor_Runtime_Instructions.md) supplies the selected
semantics; this document makes the caller and execution boundaries reviewable.
The [implementation status](../DSharp_Implementation_Status.md) identifies what
actually runs today. Passing documentation checks does not implement or adopt
this proposal. Dependent work requires maintainer acceptance of the reviewed
contract; a reviewed merge may carry that acceptance when the maintainer
expresses it.

[#14](https://github.com/gajo357/dsharplang/issues/14) owns the detailed durable
store transactions and recovery protocol; #51 and #241 own checked actor and
dispatcher typing, #56 the isolated attempt, #57 public waits and control, and
#243 the diagnostic chain. Resource lifetimes remain with #17/#45. Exact actor,
registration, receipt, fault, catch, identity and lookup spelling below is
illustrative and must be reviewed with those owners. No new standard-library
surface or exception representation is silently selected by this document.

## Operations and routing

A registered public/default actor target has one checked `Handle` input and one
declared result. An ordered specialization may supply a compatible implementation;
its pure predicate is evaluated before acceptance. The first match wins, or the
default runs when none matches. Validation or predicate failure is a selection
failure, not a reason to fall through. Persist the requested target and selected
implementation at acceptance; retries use the same selection. Missing or
incompatible selected code after deployment is an explicit operational failure or
blocked state, not permission to reroute.

| Operation (illustrative) | Route and acknowledgement | Caller observes |
| --- | --- | --- |
| `await Invoke<InvoiceActor>(message)` | Explicit target; durable scheduling through the selected route | One completion with the exact declared `Handle` value, including `Unit` or `Result<T, E>` |
| `await Dispatch<InvoiceActor>(message)` | Same target, selection and target-wide policy; durable acceptance of one invocation | An acceptance receipt, not its eventual result |
| `await Dispatch(message)` | Publication to a frozen set of zero or more explicitly registered subscriptions, each with its own selected route | A publication receipt, not subscriber results |

`Invoke` has one awaitable completion; it does not return a second handle that
must be awaited to obtain the answer. `Dispatch` does not infer a target from a
message, turn one subscriber into targeted dispatch, or wait for a recipient's
`Handle`. Zero subscribers is a valid accepted publication. Every explicit
subscription receives an independent delivery; several instances of the same
subscription compete for its delivery rather than creating new subscriptions.
Acceptance must preserve all intended delivery identities and selections or a
recoverable fan-out record before acknowledging publication. #14/#69 define the
store protocol and recovery details.

The compiler derives targeted input and result types from the public target's
checked `Handle`; reject an invalid target, incompatible input or unobserved
awaitable under the applicable language rules. Do not construct an actor or call
its `Handle` directly from application code. Actor dispatch uses runtime-owned,
application-scoped infrastructure, not a global serialized actor or an implicit
service locator. Both targeted operations share the public target's ordering,
concurrency, admission and other execution policies across specializations.

## Acceptance, attempts and isolation

Evaluate the operation's arguments once under ordinary expression rules before
submission; an argument evaluation fault is not a runtime acceptance receipt.
The runtime validates the route and message, binds an application-scoped
dispatcher and declared resources, and commits the invocation identity, payload,
requested target, selection, compatibility and routing metadata before reporting
successful targeted acceptance or executing the actor. An `Invoke` from an actor
also records its awaited parent invocation and parent attempt before the child
runs. External callers have no actor parent. #14 specifies the atomic persistence
and deduplication operations, schema compatibility and retention.

Each execution **attempt** has fresh isolated invocation state and explicitly
bound resources. An accepted logical invocation may have several attempts under
its runtime-fault policy; a lease or retry does not promise a single physical
execution or undo external side effects. Shared adapters must satisfy their
concurrency contracts, and connections, transactions, streams and request state
remain in their approved scopes. A transaction must finish before a durable actor
wait or cross-actor invocation. The isolation boundary is managed CLR execution,
not an OS process, forced preemption, rollback or BEAM-style failure isolation.
Required cleanup is observed before recording an attempt's final outcome;
cleanup faults remain visible without erasing a prior primary execution fault.
A caller timing out does not dispose resources still used by a running attempt.

## Caller outcomes and uncertain acknowledgement

Normal completion returns the declared `Handle` value unchanged. In particular,
`Error(domainError)` is a completed business `Result`, not a runtime fault,
failed acceptance, implicit retry or `CallError.Application`. `Unit`, nullable,
Option and nested values retain their declared meaning. Faults and execution
control are separate from that value; the following names describe semantic
categories, not finalized D# variants or exception syntax.

| Observation | Meaning and required distinction |
| --- | --- |
| Submission or selection failure | Validation, route, capacity or persistence failed before acceptance; no successful receipt and no executed target frame is claimed. Argument-expression failure occurs before submission. |
| Normal completion | The exact declared result, including a business `Result.Error`, is the answer. |
| Terminal execution fault | An accepted invocation exhausted its applicable execution-fault policy or otherwise terminated in a runtime fault. Preserve the original failing invocation identity and protected diagnostic record. |
| Wait timeout | The caller's bounded wait ended without a delivered completion. This alone does not prove the actor stopped or prevent a later committed result. |
| Explicit cancellation | A distinct authorized cooperative request and its observed outcome; it cannot undo committed work or external effects and need not stop noncooperative code immediately. |
| Uncertain acknowledgement | The commit may have succeeded even though the caller did not receive its acknowledgement; this is not proof of rejection or permission to submit a new logical operation. |

A caller supplies or retains a stable submission identity for uncertain outcomes.
It can query authorized status and retry the same logical submission with that
identity. The store deduplicates a matching retry to the original invocation or
publication and rejects conflicting reuse; it must not create a second logical
submission. Retention and deduplication horizon, authorization, and exact status
and conflict APIs are coordinated with #14. If the identity has expired, do not
claim deduplication is still guaranteed. A successful receipt means durable
responsibility under the provider's stated guarantees, not execution success or
exactly-once external effects.

Each individual wait resolves at most once. Completion, deadline and cancellation
races require a single recorded winner for that wait; a timeout or cancellation
already delivered cannot later become a second answer. A later execution outcome
remains observable through authorized status/inspection subject to retention.
Timeout of a wait is not itself a cancellation request. Explicit cancellation is
cooperative, and the durable control and race rules are specified by #14/#57.
No fixed timeout, capacity, retry count, default cancellation policy, or public
fault representation is adopted here. Host-fatal failure or process loss can
prevent a live waiter from receiving any answer.

## Waits, permits and failure ancestry

Waiting for `Invoke` does not block an OS worker thread or retain runnable
scheduler capacity. An exclusive public-target or per-key permit held by the
caller remains held across the wait, including across specializations. Reject a
descendant invocation that requires a permit held by an awaited ancestor before
it can deadlock; do not silently release the permit or weaken ordering. Other
cycles require bounded waits and explicit timeout/failure, not a promise of
universal distributed cycle detection or of eventual termination of a
noncooperative actor. The exact policy configuration is for #53/#57/#66.

Only `Invoke` establishes an awaited parent relationship. Targeted `Dispatch`
and each untargeted publication delivery start independent roots even when the
producer awaits acceptance. Optional correlation may connect detached work for
inspection but does not propagate its later failure into the producer's awaited
chain. A failed submission is attributed to the submission stage with the
requested target where applicable, never to an unexecuted actor body.

The runtime records logical invocation identity, requested and selected actors,
parent invocation and attempt, attempt/failure status and original failing
invocation. A terminal child fault propagates through unhandled waits without
replacing the origin or appending duplicate logical frames. A caller may handle
the fault and finish normally; retain the child failure for authorized inspection
without calling that caller failed. Parallel invokes, where supported, create
siblings rather than a fabricated linear stack. Persist ancestry for diagnosis
after restart and retain ancestors while linked records depend on them. This does
not restore suspended CLR continuations, replay saved steps, or guarantee that a
restarted caller avoids repeating child submissions or external effects.

## Illustrative D# and observations

These sketches describe future behavior; the current compiler does not implement
actor dispatch. Names for dispatcher access, identity, status, fault handling and
receipts are placeholders for reviewed syntax. Each match case is separate; no
sketch establishes a new language feature.

```text
actor InvoiceActor(store: InvoiceStore) {
    Handle(request: CreateInvoice): Result<Invoice, InvoiceError> {
        // A domain rejection is an ordinary result.
        Error(InvoiceError.Invalid)
    }
}

let answer = await Invoke<InvoiceActor>(request)
match answer {
    Success(invoice) => useInvoice(invoice)
    Error(error) => reportBusinessError(error)
}

let accepted = await Dispatch<InvoiceActor>(request)
let publication = await Dispatch(InvoiceCreated(invoiceId))

actor RefreshIndex(index: Index) {
    Handle(message: Refresh): Unit {
        index.refresh(message)
    }
}
let done = await Invoke<RefreshIndex>(refresh)
```

The first wait receives `Error(InvoiceError.Invalid)` unchanged. If `Handle`
returns `Unit`, Invoke completes with `Unit`; it does not wrap it in a receipt.
The targeted receipt can arrive while the selected `Handle` is deliberately
blocked. The publication receipt is valid with zero subscriptions; with two
explicit subscriptions, each gets one independent logical delivery and its own
selected implementation. Neither receipt asserts that a handler finished.

```text
// Provisional fault handling and lookup notation, for review only.
try {
    let invoice = await Invoke<InvoiceActor>(request)
    useResult(invoice)
} catch terminalFault {
    inspectOrigin(terminalFault)
}

let submission = stableSubmissionId()
try {
    await Invoke<InvoiceActor>(request, submission, waitLimit)
} catch waitTimedOut {
    inspectStatus(submission)
}
```

For a terminal nested failure, the caught fault identifies the original failing
invocation; handling it does not erase that child's inspection record or make a
normally completing caller fail. In the timeout example, the same identity can
later reveal completion or support a deduplicated retry. A lost acknowledgement
uses the same identity as well; a fresh identity would be a new submission. The
placeholder catch and lookup forms are not approved D# syntax.

```text
let accepted = await Dispatch<AuditActor>(auditMessage)
// A later AuditActor execution fault is an independent root.

// Provisional error handling; the persistence failure is known to precede commit.
try {
    let receipt = await Dispatch<AuditActor>(auditMessage)
    recordReceipt(receipt)
} catch submissionFailed {
    reportAcceptanceFailure(submissionFailed)
}
```

If route validation or persistence fails before acceptance, the producer observes
submission failure, receives no receipt, and no target body is attributed as
executed. An uncertain acknowledgement instead requires lookup or deduplicated
retry as described above. After accepted
detached dispatch, a later audit fault does not turn its producer's successful
invocation into a fault. If an awaited descendant would reacquire an exclusive
ancestor-held target/key permit, it is rejected; another cycle is bounded by the
configured wait rather than silently breaking the permit.

## Evidence required after adoption

Documentation validation cannot establish runtime durability or language
behavior. The implementing issues must use executed generated D#, controlled
clocks, barriers and real-provider restart tests as applicable:

1. #51/#241 derive exact `Handle` input/result types; exercise value, `Unit`,
   `Result.Error`, invalid target/message and prohibited direct calls. Show
   targeted and untargeted operations are distinct with zero, one and multiple
   subscriptions.
2. #56/#61/#64 demonstrate fresh per-attempt state, bound resources and pinned
   selection through retry; hold `Handle` blocked while a committed targeted
   receipt returns. Verify no acceptance when validation or durable commit fails.
3. #14/#61/#69 test pre/post-commit crashes, lost acknowledgement, matching and
   conflicting identity reuse, authorized status, frozen publication recipients
   and recovery without duplicate logical deliveries. External effects may repeat.
4. #57/#66 use controlled races and barriers to distinguish terminal fault,
   timeout and explicit cancellation, prove one answer per wait and inspect late
   completion. Show a held exclusive permit across waits, ancestor re-entry
   rejection, another bounded cycle and independent runnable progress.
5. #57/#243 prove propagated origin and nonduplicated frames, caught child fault
   with successful parent, detached failure isolation, sibling ancestry and
   restart inspection without restored continuation. Verify cleanup and protected
   diagnostics without exposing secrets or treating correlation as authority.

## Review and verification

Review the proposed caller-visible distinctions and handling surface, permit and
wait semantics, stable identity/status boundary and examples alongside #14.
Resolve exact language and public fault spelling before dependent implementation;
this document does not select it by example. No runtime or provider behavior is
claimed by the documentation checks recorded in the issue plan.
