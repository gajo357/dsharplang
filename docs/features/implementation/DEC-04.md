# DEC-04: Durable queue and RuntimeStore contract

**Status:** Draft proposal for review; not adopted or implemented.  
**Issue:** [#14](https://github.com/gajo357/dsharplang/issues/14).  
**Base:** `53599ca920d522cb91076a276052ea86af51a968`.

## Work plan

Reconcile existing queue, runtime, serialization and logging direction; prepare
a coherent reviewable acceptance, lease, retry and atomicity contract with D#
illustrations and crash-point tests. Identify unresolved decisions and dependent
gates. This design-only slice changes this document and no runtime behavior.

## Status, scope and sources

This is a concrete **proposal for review**, not an adopted protocol, a running
queue, or authorization to implement provider/runtime projects. Merging this
record and passing repository checks do not adopt it. A maintainer must explicitly
adopt a reviewed version before dependent implementation. The unadopted
[worker-call proposal](DEC-03.md) has a different request/reply boundary; none
of its proposed deadlines, cancellation results or defaults are imported here.

The [syntax reference](../DSharp_Syntax_Reference.md) §§13–15,
[architecture](../../ARCHITECTURE.md) and [feature specification](../../design/FEATURES.md)
settle the boundaries used below. The [pipes/logging target](../DSharp_Pipes_and_Logging.md)
§§5, 8 and 12 and [implementation sequence](DSharp_Implementation_Sequence.md)
provide integration constraints. Dependent slices include #9 runtime setup,
#59–#68 store/queue execution, #69–#71 event/schedule/operations, #78 encoding,
#133 database logging and #72/#73 additional providers. Those issues remain
separately gated; this proposal does not approve a project layout, package, SQL
driver or external storage API.

Existing direction retained: enqueue is durable acceptance, not an awaited handler
reply; queue/event/cron handlers report `Result<Unit,E>` to supervision; attempts
are at least once, may duplicate effects and normally have fresh invocation state.
A queued reference is not a worker or directly callable handler. One SQLite runtime
file per application on persistent local storage is the initial single-host model.
The runtime store and business data are logically distinct, with no implicit
cross-store transaction. Immutable eligible messages have a declared record or
union root, or Unit; bare primitives, tuples, lists, nullable roots, functions,
tasks and runtime capabilities are not queue roots.

## Proposed identities, envelope and admission

A host validates the stable application and queue identities and the registered
handler, type and policy before admitting work. A new accepted message gets a
stable `MessageId` unique within the configured store and a stable queue identity;
retries do not change either. A durable envelope records message and queue IDs,
contract identity and schema version, encoded payload, creation/eligibility time,
configured diagnostic correlation fields, partition key when used, attempt
count, policy version and state. Subscription identity and publication identity
are included for event deliveries. Every started attempt has a distinct attempt
identity, ownership token and invocation ID. These are runtime identities, not
permissions, and cannot be manufactured into a live agent capability by a public
record or a persisted payload.

`enqueue` and the `enqueue(agent)` adapter asynchronously return a typed
acceptance result; `Success(receipt)` identifies the committed message and does
not contain a handler result or completion task. Before commit, the provider
validates the message root, recursive eligibility and versioned encoding, queue
existence and explicit delay. New insertions also require pending capacity. An invalid message,
unsupported version, stopping host or capacity failure produces an explicit
error; it never produces a receipt for work that has not committed. Receipt
fields, error case names, admission configuration syntax and concrete message-ID
representation below are contract sketches pending review, not implemented APIs.

The acceptance transaction atomically writes the envelope and any deduplication
record and commits before reporting success. A caller cancelled before submission
starts no store operation. Once submitted, cancellation cannot establish that a
commit did not occur. A timeout, lost connection or process crash after commit but
before receipt is an **ambiguous outcome**, not an ordinary rejection promising
non-acceptance. No timeout path fabricates a success receipt. A caller that needs
safe retry supplies the proposed explicit idempotency key; it must not assume a
new unkeyed retry reuses a prior identity.

The proposed deduplication scope is `(application, queue, caller key)`, using an
exact canonical key value, not a process-local hash. In a single transaction, a
first request records its stable message identity and a fingerprint of canonical
contract identity/version, payload bytes and caller-supplied options that affect
delivery, including delay request and partition selection. For a relative delay,
fingerprint the original requested duration, not an absolute time recomputed on
retry; return the original receipt and eligibility on a match. Volatile tracing
metadata is excluded from the fingerprint and the first committed correlation
snapshot wins. A repeated key with identical
fingerprint returns the same receipt without new work. A reused key with different
content returns an explicit conflict, never a receipt for the wrong payload.
Concurrent duplicates are serialized by the provider's atomic uniqueness rule.
After authorization and while the store is available, the admission transaction
checks the existing key and fingerprint before charging capacity for new work.
A matching replay returns its prior receipt even when the pending queue is full;
a conflicting reuse returns conflict. Only a genuinely new acceptance consumes
capacity. The key decision, capacity check and insert must be atomic under races.
The application configures a positive finite deduplication horizon; key expiry is
measured from the original committed acceptance and is not extended on read. After
expiry the same key may create a new message, so this is not a permanent
exactly-once guarantee. Configuration must state an explicit retention horizon;
this proposal does not select one silently. A stored dedup record is not removed
while the associated work is pending, running or retained `Failed` and still
actionable, even after the nominal horizon. Once that work is completed,
cancelled or abandoned, the record may expire if its horizon has elapsed. If a
terminal envelope is compacted while the deduplication record remains valid,
the record must retain enough immutable receipt information to return the
original receipt on replay; alternatively keep the envelope until expiry.

The encoding contract in #78 must explicitly specify deterministic canonical
bytes and migration behavior before such fingerprints can be compared across
versions. Different encoded payload or delivery-option bytes are a conflict even
when a later decoder would consider their values equivalent. Idempotency keys are optional, not inferred
from payload or diagnostic correlation. Payloads are immutable serializable data
at acceptance; callback/function/task values, service/worker capabilities,
connections and mutable foreign values are rejected recursively after generic
substitution. An approved encoder must distinguish missing from explicit null
where relevant. `Option<T>` has no automatic JSON/HTTP/queue wire representation:
if a later adopted encoding supports it, `None`, `Some(null)` and nested Options
must remain distinct; otherwise reject it explicitly. Logger's JSON envelope does
not decide the queue format.

## Proposed durable states and transitions

The names here describe durable meaning, not new D# keywords or an API promise:

| State | Durable meaning | Allowed next states |
| --- | --- | --- |
| `Pending` | Accepted, unowned; `eligibleAt` may be future | `Running`, `Cancelled` |
| `Running` | One current ownership token with expiry; handler may be active | `Completed`, `Pending`, `Failed`, `Cancelled` |
| `Completed` | Successful terminal completion recorded | none (subject to retention) |
| `Failed` | Rejected or exhausted work retained for inspection; blocks its ordered head | `Pending` by authorized manual retry, `Cancelled` by authorized cancellation, `Abandoned` by explicit authorized skip |
| `Cancelled` | Cancelled terminal work retained for inspection | none unless a future policy explicitly permits replay |
| `Abandoned` | Explicitly skipped work retained for inspection | none unless a future replay policy is adopted |

A proposed cancellation-request flag is separate from state. The atomic claim
changes eligible `Pending` to `Running`, increments the persisted attempt number
(starting at 1), assigns a fresh unpredictable ownership token and `leaseUntil`,
and creates an invocation identity. This attempt ordinal is monotonic across the
message's entire lifetime, including manual retries. Each retry generation also
has a separate persisted attempt count, reset atomically when manual retry starts
a new generation. `maxAttempts` limits the current generation and includes its
first claim. Token
identity is never reused for a later attempt even after process restart. The
handler runs **outside** the claim transaction; no database transaction is held
across handler work or remote I/O. Renewal, completion, reschedule and failure
updates each atomically verify queue/message, `Running` state, exact token and
an unexpired lease against the authoritative store clock. A stale/expired owner
gets a stale-ownership outcome and makes no state change, even before a replacement
claims the message. It cannot revive itself merely by submitting a late renewal.

Lease expiry indicates lost ownership, not proven termination or rollback. An
atomic recovery operation fences the old token and applies the configured
runtime-fault retry policy or records a retained failure when attempts are
exhausted. A retry transition sets `Pending` with a future `eligibleAt`, clears
the owner, releases durable claim/concurrency bookkeeping and permits a later
new attempt. The old process may still be running, so it may duplicate external
side effects; all later acknowledgements from it remain stale. A provider must
use one authoritative time source for lease eligibility/expiry and compare it
inside the same transaction as ownership changes. For the initial SQLite
single-host provider this is a host-controlled store clock; a multi-host provider
must use its shared authoritative clock, not independently trusted host clocks.
Exact precision, clock-skew tolerance and lease-duration/renewal defaults must be
specified per approved provider before production use. An expiry boundary uses
`now >= leaseUntil` as expired. Recovery, completion and renewal are serialized
so no two owners can both commit an ownership-sensitive transition.

The provider must declare its durable-commit assumptions, including supported
SQLite journal/synchronization settings, persistent-storage requirements and
recovery behavior. A provider cannot acknowledge a durability guarantee its
configured storage cannot provide. The single-host clock must handle wall-clock
adjustments and restart explicitly; an in-memory monotonic timestamp alone is
not a persisted cross-restart clock. Exact clock-jump behavior remains a provider
conformance decision before adoption.

A normally completed handler reports `Success()` or `Error(error)` to its
supervisor. Success completes only with a valid token and after required invocation
cleanup. An application `Error` is matched against explicitly configured variants:
only a selected retry policy can reschedule it; an unselected variant goes to
retained `Failed` with its safe diagnostic classification. Unexpected faults,
lease expiry, runtime cancellation and timeouts have separately configured policy;
they are not silently treated as an application-error variant or retried merely
because an error exists. `maxAttempts` limits executions, not admission calls:
a retryable outcome at the limit becomes retained `Failed`. Safe/redacted
inspection detail and authorization are separate observability concerns.

Rescheduling is supervisor/runtime control, **not** a third `Result` variant and
not a fabricated business error. The proposed operation takes the current owned
attempt, a validated future eligibility time and a bounded reason; it atomically
records the retry/schedule transition with token fencing. Explicit handler-driven
rescheduling, if exposed later, needs its own typed capability and lifetime rules;
this proposal does not invent its D# declaration. A retry policy can select that
operation from an eligible application failure. Invalid time/policy/capability
must not result in a silent success or an untracked sleeping handler.

## Retry, ordering, capacity and cancellation

Per-policy retryable error variants, maximum attempts, backoff and jitter are
validated before registration. The proposed initial retry delay formula is
`min(maxDelay, baseDelay * 2^(failedAttempt - 1))` with arithmetic saturated
before overflow. `failedAttempt` is the 1-based attempt count in the current
retry generation, not the lifetime-global ordinal; a manual retry starts the
new generation's backoff schedule from its first attempt. Proposed jitter is a
nonnegative sampled offset, capped so the
final delay cannot exceed `maxDelay`; it is selected once per transition, not
recomputed on restart. The sampled `eligibleAt` is stored in the same transaction.
Policy supplies explicit positive base/max delays, a bounded jitter range and
attempt limit; the design does not assume an undocumented global production
default. A provider must never shorten a requested delay silently. A
controlled random source and clock make the policy testable. Manual retry is an
authorized operation on retained `Failed` work, records operator/audit metadata,
reuses the message ID/payload and starts a new attempt generation with a fresh
manual-retry budget; the original attempt history remains distinguishable. A
manual retry must not mutate completed work or bypass provider capacity checks.
Whether and when manual retry is permitted for a particular failure remains
explicit application policy, rather than an automatic action by this proposal.

Concurrency limit scope is explicit: per host, whole deployment, queue or key as
supported by the provider and selected policy. Pending capacity and in-flight
execution capacity are separately bounded; limits count the claimed/running work
until a fenced terminal/reschedule transition or expiry recovery commits. A
producer may receive an explicit capacity rejection or use a separately designed
bounded wait; this proposal does not introduce an unbounded producer wait. If a
provider cannot implement the requested deployment-wide atomic scope, startup
fails rather than silently degrading to per-host guarantees. An expired but still
running process is not a current owner: recovery permits another attempt, and the
number of physically running noncooperative processes cannot be guaranteed by a
lease alone. Hosts must track/cap their own local work; they must not describe a
store claim limit as a global bound on stale external processes.

For sequential queues and per-key partitions, the head is the earliest accepted
message in the ordering scope that has not yet been unblocked by completion,
cancellation or explicit skip/abandonment. A retained failed message remains a
head even though that attempt is terminal. Acceptance assigns a stable
store sequence atomically; simultaneous acceptances are ordered by that committed
sequence, not by wall-clock or producer intent. A delayed retry of the head blocks
later messages in the same queue/key, including when another key can continue.
An exhausted retained `Failed` head continues to block that ordering scope until
successful authorized manual retry, explicit skip/abandonment or an authorized
cancellation resolves it. Manual retry preserves its original position while it
blocks; after an explicit skip/abandonment later work may have advanced, and a
future replay cannot retroactively restore external-effect ordering. Mere FIFO
dispatch without completion fencing is not sufficient. Partition keys are stable immutable data derived once at admission
under an explicitly approved key/equality contract. DEC-01 has since been
adopted; durable key encoding remains a separate gate, not a decision here.

This ordering guarantees a single **valid store owner** and ordered durable
transitions, not physical non-overlap after lease expiry. A noncooperative old
handler may still perform an external side effect after a replacement or later
key item starts. Strict external-effect ordering requires external fencing,
idempotency or stronger coordination; it is not provided by the lease alone.

For a pending message, an authorized cancel operation atomically transitions it to
`Cancelled` before a claim can commit. For a running message, it durably records
a cancellation request and signals the current invocation cooperatively; it does
not revoke a lease while handler code still runs. Proposed winner rule: if the
request commits before the fenced terminal outcome, the subsequent fenced exit
or expiry recovery records `Cancelled` without scheduling another attempt; if a
terminal outcome committed first, cancellation returns its already-terminal
status. The cancellation request and outcome are serialized atomically. The
handler may not stop promptly, and cancellation says nothing about external side
effects already performed. A lost owner with a persisted cancel request resolves
to `Cancelled` when its lease is recovered, not an unbounded retry. Completion,
cancel and lease-expiry races need explicit outcomes and audit records; an
unauthorized actor cannot cancel, skip, inspect protected details or retry.
A `Failed` message can also be explicitly cancelled by an authorized operation;
the atomic `Failed`-to-`Cancelled` transition records the operator and reason,
preserves its failure/attempt history and unblocks its ordering scope. Cancellation
of a failed head is not the same as an implicit retry or successful completion.
A cancelled/failed/abandoned row remains inspectable under retention and authorization;
retention policy and cleanup must never remove a live claim or its dedup record.

## Atomic operations and provider conformance

`RuntimeStore` exposes named atomic runtime operations—not arbitrary get/save
plus caller-implemented races—including accept/dedup, claim, renew, complete,
retry/reschedule, fail, cancellation request, expiry recovery, inspection and
manual retry. Provider capability declarations describe transactional guarantees,
ordering/concurrency scope, time and serialization limitations. The runtime
checks them against requested policies at startup. No unsupported guarantee is
silently treated as supported; SQLite single-host is not evidence of multi-host
coordination. PostgreSQL, MySQL and third-party providers must pass the same
conformance behavior for the capabilities they advertise.

When a handler requests completion with follow-up enqueue **in the same
RuntimeStore**, commit the fenced original completion and all validated follow-up
acceptances/dedup records in one transaction. If ownership has expired, any
follow-up conflicts, encoding/capacity validation fails, or the transaction fails,
commit neither completion nor follow-ups; a retry must not leak partial children.
Stable deterministic or explicit child idempotency keys let recovery distinguish
an already accepted child from a new one. This is a runtime-store transition,
not an automatic cross-database or external side-effect transaction. A business
database change and publication need a separately designed outbox/equivalent
protocol (#95).

Persist each subscription's stable identity and independent outcome. Event
publication captures a stable intended subscription/version set and durably records
fan-out progress. For each intended subscription, creation of its independently
addressed queued delivery and marking that subscription as delivered must be
atomic or otherwise recoverable through a stable unique delivery key. Recovery
must not omit a matching subscription or multiply its logical delivery record;
zero subscribers is an accepted empty fan-out. Runtime handler attempts can still
duplicate side effects. Dynamic subscription changes, registration policy and
filter/mapping purity must be resolved by #69; the snapshot boundary and error
policy must be explicitly approved rather than inferred from current process
memory.

Schedule advancement and creation of each due occurrence are atomic within one
store transaction; a stable schedule/occurrence identity prevents duplicate
logical occurrence records after restart. Persist timezone and progress; skip,
run-once and catch-up policies are explicit and bounded. Detailed timezone/DST,
misfire and restart policy belongs to #18/#70; this record states the atomicity
boundary without adopting their algorithms.

The optional database log output can use the same configured RuntimeStore but
owns separately versioned logging tables and retention, and runs outside queue
and business transactions. A log submission remains a bounded, transient,
nonwaiting attempt, not a durable enqueue or receipt. Logging cleanup must never
delete or compact queue/business rows; log delivery failure cannot roll back a
completed queue transition. Persist only configured, eligible correlation fields
with the message; never persist a live scope or treat trace/tenant metadata as
identity or authorization. Reconstruct per-attempt context with a stable message
ID and fresh attempt/invocation IDs. Logger/output diagnostics bypass the failed
Logger path.

## Illustrative D# examples

These examples illustrate the target and proposed control shape; **the current
compiler does not implement queue agents, async runtime capabilities or these
provisional receipt/error APIs**. The producer awaits acceptance only:

```text
type SendInvoice { invoiceId: string }
type DeliveryError { | Temporary | InvalidAddress }

agent DeliverInvoice(mail: MailService) {
    trigger Queue<SendInvoice>("invoices")
    policy {
        concurrency = 4
        scope = PerHost
        retry DeliveryError.Temporary { maxAttempts = 5 }
    }
    handle(message: SendInvoice): Result<Unit, DeliveryError> {
        await mail.send(message.invoiceId)
    }
}

async fn submit(queue: DeliverInvoice, message: SendInvoice)
    : Result<EnqueueReceipt, EnqueueError> {
    await enqueue(queue)(message)
}
```

`Success(receipt)` indicates acceptance only; it must never be interpreted as mail
sent. A logged diagnostic is independent of that durable acceptance. Direct
`queue(message)`, `queue.handle(message)`, `call(queue)` or awaiting a receipt's
handler outcome are invalid. Exact syntax, configuration and security capabilities
remain subject to the relevant language/runtime decisions.

## Required evidence after adoption

Implement provider conformance with controlled clock/randomness, barriers and
real-provider integration; a passing document check does not establish these
behaviors. At minimum:

1. Exercise immutable root/recursive payload rejection, schema/version migration,
   null/missing and an explicitly approved Option encoding if one exists. Confirm
   `None`, `Some(null)` and nested options do not collapse.
2. Test capacity rejection, duplicate concurrent keyed requests, mismatched key
   reuse, horizon expiry and lost post-commit response. With a full queue, matching
   key replay returns the original receipt and conflicting reuse fails; neither
   consumes capacity. Unkeyed ambiguity is not a false reject.
3. Crash immediately before/after acceptance, claim, renewal, retry, terminal
   commit and recovery. Restart a real SQLite provider, reclaim expired ownership
   and reject stale renew/complete/reschedule even if no replacement has claimed.
4. Race cancellation, expiry and acknowledgement; prove one committed terminal
   result, cooperative signaling and no claim of external-effect rollback.
5. Show per-key ordering, delayed head blocking, independent-key progress and
   provider rejection for unsupported multi-host/deployment-wide scope. Competing
   claims cannot both commit ownership; tests must not assume FIFO dispatch alone.
   Hold an expired noncooperative owner alive while a replacement and later key
   item run; reject stale store writes without claiming external effects stopped.
6. Test named application error policy, separate unexpected fault/cancellation,
   first-attempt accounting, persisted jitter/delay and saturation, exhaustion,
   authorized manual retry and retained/audited history. Verify global attempt
   ordinals remain monotonic while generation-local retry budgets and backoff
   schedules reset, and test authorized cancellation of a retained failed head.
7. Crash every point around completion + follow-up, schedule advance + occurrence,
   and event fan-out progress; prove same-store atomicity and recovery without
   missing or duplicated logical entries. Explicitly test unrelated business-DB
   effects are not rolled back and no implicit cross-store transaction exists.
8. Test transient Logger failure/overflow independently of queue commit; database
   log retention/capacity cleanup cannot delete live queue records. Verify allowed
   correlation snapshot per attempt without treating it as authorization.

## Review decisions and adoption gate

Review and explicitly adopt or amend the proposed deduplication scope and horizon,
capacity/ordering scopes, authoritative clock and lease-fencing boundary,
cancellation winner rule, retry/backoff/jitter/manual-retry accounting, event
subscriber snapshot and store transaction boundaries. No universal numeric queue,
lease, timeout or retention default is adopted here. #78 must separately resolve
canonical payload/Option encoding, #17 lifetime/shutdown, #18/#70 schedule details,
#69 subscription registration and #95 outbox behavior. Runtime setup #9 and
provider children need an adopted version, exact project boundaries and trusted
integration tests before implementation. A merged design proposal alone is not
adoption and does not close #14.

## Verification of this proposal

Bootstrap, documentation/link checks and compiler verification passed (18
baseline and 362 feature tests). All 23 automation tests passed. The local
isolated verifier could not pull its pinned Docker image (`Forbidden`); GitHub
Actions runs the unchanged verifier. These checks validate the repository, not
durable queue behavior or adoption of this proposal.

The first GitHub compiler run passed all tests but rejected its feature report
for exceeding the existing size limit. A shorter internal test assembly name
reduces repeated report paths; test cases, assertions and the verifier remain
unchanged. This is a CI repair on the same PR, not a queue implementation.
