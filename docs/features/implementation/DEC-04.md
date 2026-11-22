# DEC-04: Durable actor acceptance, attempts and publication recovery

**Status:** Draft proposal for review; not adopted or implemented.  
**Issue:** [#14](https://github.com/gajo357/dsharplang/issues/14).  
**Base:** `a10e9d70bd5239ff100f704f289e10d8f7335f9e`.

## Scope and review boundary

This proposal replaces the historical queue-agent and callable-worker split,
`enqueue(agent)` adapter and rules that retry or reject a normal business
`Result.Error`. The [actor handoff](../DSharp_Actor_Runtime_Instructions.md)
selects the target semantics. [DEC-03](DEC-03.md) describes caller-visible
Invoke/Dispatch outcomes and remains a draft requiring expressed maintainer
acceptance; this storage proposal aligns with its semantics without adopting its
illustrative fault or language spelling. A reviewed merge may carry acceptance
when the maintainer expresses it. Documentation checks do not implement or adopt
either contract. The [implementation status](../DSharp_Implementation_Status.md)
records what runs today.

#59–#69 own the provider, intake, execution, policies and publication; #78 owns
schema/codec decisions, #57 waits and control, #243 diagnostic chains, and #95
business-store coordination. The initial provider is one SQLite RuntimeStore file
per application. Messages are immutable declared data or `Unit`, schema checked
at acceptance; no compatibility migration is implied by the actor cutover.
Runtime capabilities, tasks, functions, mutable foreign values, connections and
streams are not message payloads. Exact encoding, including Option/null handling,
requires #78; no new public API, fault type, keyword, numeric default or package
is selected here.

## Targeted acceptance and stable identity

For targeted `Invoke` and `Dispatch`, validate the public target, immutable input,
resource bindings and routing against a frozen host registry. Evaluate ordered
pure specialization predicates once and select the first match or the default.
Predicate failure is a selection error; it must not silently choose another
implementation. Both operations use the same route and target-wide policies.
Before execution or a successful acceptance acknowledgement, atomically commit:

- a stable submission identity and logical invocation identity; accepted payload
  and schema/compatibility metadata;
- requested public target, selected implementation and the routing/policy metadata
  needed to preserve that decision;
- an awaited parent invocation and **parent attempt** for an Invoke from an actor;
  targeted Dispatch is an independent root; and
- admission/order information and the durable initial state required for recovery.

A failed validation or failed commit produces no success receipt and cannot start
an actor body. A receipt acknowledges durable responsibility under the configured
provider's declared durability, not completion or exactly-once external effects.
The logical invocation and selected implementation remain the same across
attempts. An unavailable or incompatible selected implementation after deployment
is retained as an explicit blocked/failed operational state for inspection and
recovery; never select a new fallback during recovery.

A caller retains a stable submission identity for acknowledgement loss. On retry,
look up that identity **before** re-evaluating a changed registry, predicates or
capacity: a matching retained submission returns its original identity/status or
receipt, and conflicting reuse is rejected. Compare the full semantic request,
including operation and payload/target identity, under a reviewed canonical
representation; a hash alone must not silently accept a conflict. Concurrent
identical retries cannot create two logical invocations. Authorized status lookup
can resolve an uncertain acknowledgement. A pre-commit rejection and an unknown
post-commit response are distinct; never claim a lost response proves rejection.
Retention must cover the advertised deduplication horizon; after expiry the
runtime cannot promise a retry will deduplicate. The horizon and exact API remain
review decisions, not implicit infinite retention.

## Attempts, fencing and outcomes

The names below describe durable meaning, not D# declarations or a finalized
schema. All ownership-sensitive transitions are atomic RuntimeStore operations,
not independent get/save calls assembled by actor code.

| State | Durable meaning | Representative transition |
| --- | --- | --- |
| Accepted / eligible | Committed logical invocation without a current owner, possibly delayed | Atomic claim creates an attempt and owner |
| Running | One current fenced owner for an attempt; body and cleanup run outside the store transaction | Owned completion, fault/retry, cancellation handling or lease recovery |
| Completed | Fenced terminal normal `Handle` value, including `Unit` or business `Result.Error` | Retained for status/inspection until policy permits deletion |
| Failed / blocked | Terminal execution fault or unavailable/incompatible selected implementation | Explicit authorized recovery, cancellation or retention policy; never automatic reselection |
| Cancelled | A durable terminal control outcome under the reviewed cancellation policy | Retained for inspection until policy permits deletion |

A claim atomically creates a distinct attempt identity and fresh ownership token,
sets eligibility/lease data, and retains the same logical invocation and pinned
selection. It must never reuse a token for a later attempt. Claim atomically checks
unowned eligibility; owner renewal, completion, failure and reschedule each check
the current state, exact token and unexpired lease against the provider's
authoritative clock. Expiry recovery atomically compares the observed current
state and token and requires that lease to be expired before fencing it. Renewal,
claim and recovery serialize so an old observation cannot fence a renewed lease
or a replacement owner. An expired or superseded token cannot renew, complete, enqueue a
follow-up or acknowledge work, even before a replacement claims it. Attempt
history, normalized faults and any origin failing invocation remain inspectable
subject to authorization and retention. #243 preserves logical ancestry without
adding duplicate frames across attempts or propagated faults.

Lease expiry means ownership was lost, not that an old process stopped or that
its external writes were rolled back. Recovery fences the old token and applies
the configured runtime-fault retry policy or records terminal failure. A stale,
noncooperative body may still execute outside the store; idempotency, external
fencing or reconciliation is necessary for repeat-sensitive effects. Store
ownership limits are not a promise that physically stale processes cease running.
The handler and scoped cleanup run outside the store transaction; do not hold a
database transaction across handler work or remote I/O.

A fenced normal completion records the exact declared value, including a normal
`Result.Error`, as a completed business answer. It does not become a retry,
rejection or runtime fault merely because the result is an error. A terminal
execution fault is separate, retains its original failing invocation and may be
propagated through unhandled Invoke waits according to #13/#243. Submission or
selection failure before acceptance has no executed target frame. An accepted
attempt's cleanup fault is recorded without erasing an earlier primary fault.
Protected diagnostics and correlation do not grant inspection authority.

## Ordering, policies, waits and cancellation

Policy applies to the public target across all its selected implementations.
The store assigns acceptance order atomically; wall-clock arrival or predicate
execution order is not a committed FIFO sequence. For a configured target-wide
FIFO or per-key exclusive policy, later eligible work in the same scope cannot
pass its unresolved head merely because it selected a different implementation.
A retry delay does not reorder that head. Independent keys may progress. A
retained failed/blocked head requires an explicit reviewed recovery, cancellation
or authorized skip policy; a skip must be auditable and cannot retroactively
restore the ordering of external effects.

An exclusive target/key permit remains held across an awaited Invoke. A
would-be descendant that needs an ancestor-held permit is rejected explicitly;
other cycles use bounded waits. Suspending an await releases runnable scheduler
capacity, not the logical permit. Provider claims fence durable transitions, not
the external actions of an expired process: strict external-effect ordering needs
an external fencing or coordination contract. Capacity and concurrency scopes
must be explicit and bounded. Startup rejects a policy the selected provider
cannot enforce rather than silently degrading deployment-wide guarantees to
per-host behavior.

Policies govern retries of execution faults and specify finite attempts,
backoff and jitter where enabled. Persist the chosen next eligibility time so a
restart does not reroll the delay; ensure arithmetic is bounded. Do not set a
universal attempt count, timeout, capacity, delay or retention default here.
A caller's wait timeout does not itself cancel accepted work or prove it stopped;
a later completion can be inspected. Explicit authorized cancellation is a
separate cooperative request. A pending cancellation and claim, or a running
cancellation and fenced completion/expiry, must serialize to one documented
durable outcome without double completion or hidden retry; cancellation cannot
undo committed business or external effects. Exact cancellation winner rules,
control surface and provider clock-jump behavior require review with #13/#57.

Retain deduplication records and completed/failure results for their documented
horizons. Preserve an ancestor while retained child/failure records depend on it,
and never delete a live ownership record to satisfy ordinary retention. Recovery
may restart an invocation from its input according to the reviewed policy; the
stored diagnostic chain does not restore a suspended CLR continuation or prove
that repeated child submissions and external effects were avoided.

## Publication and detached delivery

Untargeted `Dispatch(message)` is publication, including with zero subscribers;
it does not infer a concrete actor target or await recipient results. Validate
all explicit subscriptions and select one implementation per public target
before accepting. Freeze subscription identities, public targets, selected
implementations, message/schema and routing metadata at the acceptance boundary.
Commit either all intended delivery records or a complete durable fan-out record
from which every intent can be recovered before acknowledging publication.
Invalid routing or a failed acceptance commit produces no successful receipt.
A publication also has a stable submission identity. After a lost acknowledgement,
matching retry is resolved before consulting changed subscriptions, selections or
capacity; it returns the original publication receipt/status and frozen set.
Conflicting identity reuse fails, and concurrent retries cannot create a second
logical publication. Retention and the deduplication horizon follow the same
explicit boundary as targeted submissions.

A stable `(publication ID, subscription ID)` identifies each logical delivery.
Create each delivery and record progress atomically, or use an equivalent unique,
recoverable operation: after a crash, repeated expansion must neither omit an
intended subscription nor create a second logical delivery. Zero subscribers
commits an accepted empty publication. Later registration changes do not alter
an accepted frozen set or its pinned selections. Instances of the same
subscription compete for a delivery; they are not extra subscribers. Attempts
and faults for one delivery remain independent of peers and the publisher's
awaited chain. Optional correlation is diagnostic, not an awaited-parent link.
Execution remains at least once even when logical delivery identities deduplicate.

## Atomicity and provider capabilities

A fenced original completion and validated follow-up acceptances in the **same**
RuntimeStore must commit as one transaction. Invalid/stale ownership, a follow-up
identity conflict or failed validation/commit leaves neither a committed original
completion nor partial follow-ups. Stable follow-up identities make retries
recognizable; they do not make arbitrary external writes transactional.

A business database or external API is not part of the RuntimeStore transaction.
Use the separately reviewed outbox or equivalent coordination (#95) when business
changes and publication must be reconciled. Do not promise atomic cross-store
commit, compensation or exactly-once side effects. Provider capabilities must
state supported atomic operations, clock/lease assumptions, storage durability,
ordering/concurrency scope, encoding and recovery limitations; unsupported policy
must fail validation. SQLite's one-file single-application guarantee is not proof
of multi-host or distributed provider behavior. File location, journal/sync mode,
storage persistence, authoritative time and clock adjustment behavior require
provider-specific review before a durability claim.

## Crash and conformance examples

These are proposed future provider/runtime tests, not implemented behavior. Use
controlled clocks, barriers, concurrent clients and a real SQLite process restart;
a documentation pass is not durability evidence.

1. **Before and after acceptance:** fail before commit and observe no receipt or
   actor execution; crash after commit but before acknowledgement, retry the same
   identity and obtain the original receipt/status without a second invocation.
   Reuse with different input fails; registry changes and capacity exhaustion do
   not reroute an already accepted matching retry.
2. **Ownership and restart:** race claims, renewals, completion and expiry;
   at most one token can commit each transition. After expiry or replacement,
   reject stale completion and stale follow-up even if the old body continues.
   Restart retains logical invocation, parent attempt, selected implementation,
   attempt history and failure origin; missing code becomes blocked/failed, not a
   new fallback. Verify lease loss does not imply the old external effect stopped.
3. **Outcomes and control:** persist `Unit`, a successful value and a business
   `Result.Error` as normal completion; fault retries preserve identity/selection,
   then record terminal origin. Race cancellation with claim, completion and
   expiry under the adopted winner rule. A timed-out waiter can inspect a later
   result; no result, cancellation or timeout implies rollback.
4. **Ordering and permits:** hold one target/key permit across an Invoke, reject
   descendant re-entry, exercise a bounded other cycle and let an independent key
   progress. Show retry delay blocks the same ordered head across specializations;
   test configured capacity and rejection of unsupported provider scope.
5. **Publication:** accept zero subscribers; freeze several explicit subscriptions
   with different selections. Crash before/after acceptance and at each fan-out
   step, lose the acknowledgement, retry the same publication identity, change
   registrations and restart repeatedly. Assert the original frozen set and one logical delivery
   per frozen subscription, no missing intent and independent recipient failures;
   repeated attempts may repeat external side effects.
6. **Atomic follow-ups and retention:** crash at both sides of same-store
   completion/follow-up commit, test stale owner and identity conflicts, and
   demonstrate all-or-nothing records. Show separate business-store effects need
   outbox/equivalent coordination. Retain ancestors, deduplication and results
   while references/horizons require them and protect inspection authorization.

## Review decisions

Review acceptance/deduplication scope and horizon, canonical identity comparison,
provider durability and clock/lease assumptions, retention and ancestor rules,
cancellation winner and authorized recovery/skip policy, and publication/follow-up
atomicity. Coordinate caller-visible outcomes and the unresolved public fault
surface with #13; encoding with #78; and cross-store coordination with #95.
Exact language/API spellings and provider defaults remain separate reviewed
choices. No runtime behavior is claimed until its owning implementation and
conformance evidence exist.
