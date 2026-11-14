# DEC-08: Scheduling and WebSocket lifecycle contracts

**Status:** Draft proposal for review; neither section is adopted or implemented.
**Issue:** [#18](https://github.com/gajo357/dsharplang/issues/18).
**Base:** `bb2da38280ab694e34120d0cd71290aac97dc2c6`.

## Scope and settled principles

Cron delivers through durable queues. WebSockets are live resources and do not
survive a process restart. These are settled requirements from the
[backlog](../DSharp_Feature_Backlog.md#dec-08-freeze-scheduling-and-websocket-lifecycle-contracts)
and [architecture](../../ARCHITECTURE.md). All defaults below are proposals.
The [work plan](issue-18-plan.md) records scope and verification.

The [queue/store](DEC-04.md) and [HTTP](DEC-05.md) records remain separately
gated proposals. This document specifies required integration behavior without
adopting their provisional representations. No runtime, transport, cron library,
serialization format or D# registration syntax is implemented or selected here.

## 1. Proposed scheduling contract

A schedule produces messages for an existing durable queue. Execution,
concurrency, retries and dead letters belong to that queue; scheduling introduces
no separate execution model. One-time delayed work uses delayed enqueue.

### Definition and calendar

Schedules have stable application-scoped names. A definition contains its name,
revision, cron expression, timezone, missed-occurrence policy, target queue and
typed serializable immutable payload. Payload roots follow the existing declared
data type or Unit rule; handles, services and closures cannot be persisted.
Startup rejects invalid definitions, missing queues and incompatible payloads.

| Concern | Proposed contract |
| --- | --- |
| Cron | Five fields: minute, hour, day of month, month, day of week. Numeric values, `*`, lists, inclusive ranges and positive steps only. |
| Field bounds | Minute 0–59, hour 0–23, day 1–31, month 1–12, weekday 0–6. Sunday is 0; 7 is rejected. Ranges do not wrap. |
| Exclusions | No seconds, year, names, macros or special extensions such as `L`, `W`, `#` or `?`. Reject invalid syntax and out-of-range values at startup. |
| Day matching | At least one of day of month or day of week must be the literal `*`; both may be `*`. Equivalent expressions such as `*/1` do not satisfy this restriction. |
| Calendar | Match valid calendar dates only; a nonexistent date such as February 30 creates no occurrence. Steps select values within their field/range, not durations after the previous run. |
| Timezone | UTC by default; explicit IANA names supported. Invalid names fail startup validation. |
| Timezone data | All instances participating in a schedule use the same versioned timezone database; record its version for diagnostics and reject mismatched participants. A rule-data upgrade is a definition revision activated at a boundary below. |
| Spring DST | A nonexistent local time creates no occurrence. |
| Autumn DST | A repeated local time creates one occurrence at its first UTC instant. Starting between the two instants does not make the second eligible. |
| Identity | Stable tuple of application identity, schedule name, revision and scheduled UTC instant. Unique within the store, independent of scheduler instance or execution time. |
| Delivery | Occurrence creation is deduplicated; queue execution remains at least once. Handlers must tolerate retries and duplicate effects. |
| Overlap | Follow the target queue's concurrency rules; a schedule does not wait for earlier executions. |

For `30 2 * * *` in `Europe/Berlin`, 2026-03-29 has no occurrence;
2026-10-25 produces only 00:30 UTC, not 01:30 UTC. The resolved timezone-data
version accompanies diagnostics so these expectations can be reproduced.

### Progress, activation and missed occurrences

Progress is a persisted UTC evaluation boundary, not the last successful handler
execution. A new revision starts at activation instant A with no historical
catch-up. Its first eligible occurrence is strictly after A. Recovery captures a
fixed UTC cutoff C and evaluates occurrences in `(progress, C]`, never moving
progress backward when the clock moves backward. An empty interval enqueues
nothing under every policy. Normal due processing uses the same boundary rule.

| Policy | Proposed behavior for unmaterialized elapsed occurrences |
| --- | --- |
| Skip | Enqueue none; advance progress through C. |
| RunOnce (default) | Enqueue the latest elapsed occurrence, if any; advance through C. |
| CatchUp(maxCount, maxAge) | Select occurrences with age `C - scheduledInstant <= maxAge`, retain the most recent maxCount, and enqueue them chronologically; advance through C. |

CatchUp requires explicit positive finite maxCount and maxAge. There is no
unlimited mode. Discarded older occurrences are counted with the progress
transaction so retrying recovery cannot double-count committed discards. Queue
ordering policies govern execution; chronological insertion does not promise
chronological completion. Already queued work follows its normal queue rules.

Example: a minute schedule has progress 10:00 UTC and recovery cutoff 10:05.
Skip produces nothing; RunOnce produces 10:05; CatchUp(2, 3 minutes) produces
10:04 and 10:05, discarding three occurrences. The age boundary includes 10:02
before the count limit is applied. A second recovery through 10:05 creates none.

### Atomicity and recovery

Schedule and target queue must share a transactional runtime store in v1.
Reject configurations that cannot atomically create queue messages and advance
progress. No external broker publication is treated as an atomic store write.

1. Read the active revision, persisted progress and coordination version; capture
   C once for the recovery attempt.
2. Evaluate eligible occurrences through C using that revision and timezone data;
   select the policy's messages with their original scheduled instants.
3. In one store transaction, conditionally verify active revision and progress,
   insert selected queue messages with unique occurrence identities, record
   discard counts, and advance progress through C.
4. On a coordination conflict, roll back and recalculate from current persisted
   state. Never commit progress after only part of the selected batch is stored.

Selection must respect configured bounds without creating an unbounded in-memory
catch-up list. Exact provider query and coordination mechanisms remain separate
implementation work; unique identities supplement conditional updates.

| Failure or race | Required outcome |
| --- | --- |
| Crash before commit | Neither messages nor progress commit; recovery recalculates. |
| Crash after commit, before acknowledgement | Messages remain queued; persisted progress prevents recreation. |
| Competing instances read the same progress | Only one conditional transaction wins; the loser rereads. No duplicate occurrence messages. |
| Handler failure | Queue retries the existing message; scheduler creates no replacement. |
| Queue insertion fails | Whole transaction rolls back, including progress and discard accounting. |

Definition changes create new revisions effective at activation. Activation is
coordinated in the store with progress updates: the old revision owns scheduled
instants at or before A; the new revision owns instants strictly after A. Any
unmaterialized old interval through A is finalized under its old policy in the
activation transaction before replacement. Stale producers fail their revision
check. A committed old batch through C requires activation at A >= C; no revision
reinterprets committed history. Previously queued messages are retained.

Removal is a coordinated deactivation: after it commits, stale producers cannot
enqueue further occurrences. Unmaterialized occurrences are abandoned and already
queued messages remain. Reusing a removed name must allocate a new revision,
never reuse occurrence identities.

## 2. Proposed WebSocket contract

A socket is owned by one runtime process. Its connection scope may span many
handler invocations and does not require a persistent actor. Application handlers
receive a scoped connection capability that cannot escape into durable payloads.

### Transport, execution and buffering

| Concern | Proposed contract |
| --- | --- |
| Ownership | Runtime owns I/O, framing, heartbeat, cancellation, close handshake and cleanup. |
| Authentication | Authenticate and authorize before accepting; rejection creates no accepted connection. |
| Types | Endpoint declares inbound/outbound types. Initial typed transport uses JSON text messages; exact envelope and type mapping need separate review. |
| Reassembly | Reassemble fragments into one complete message before decoding/dispatch. Enforce the byte limit while accumulating, before allocating an oversized message. |
| Execution | Process incoming messages sequentially per connection, including awaits; different connections execute concurrently. No automatic incoming-message retry. |
| Incoming buffering | Bound complete-message count and bytes as well as individual message size. Pause application-data reads when full; if capacity cannot be obtained within the configured inbound timeout, close. Control-frame handling remains runtime-owned. |
| Outgoing buffering | One bounded queue and one writer per connection. Count and byte accounting include the in-flight write so a blocked writer cannot bypass limits. |
| Ordering | Preserve queue acceptance order. Concurrent senders have no additional ordering guarantee. |
| Sending | Validate/encode against the outbound type and size limit, then await capacity with cancellation and a finite configured timeout. Bound pending admission count/bytes too; reject excess pending admissions immediately. |
| Send success | Success means local queue acceptance, never client receipt or processing. Acceptance is the linearization point; a cancellation race resolves according to whether acceptance already happened. |
| Slow clients | The first outbound capacity or write timeout closes the connection and fails sends still waiting for acceptance. No silent dropping while the connection remains open. |

All size, buffer, pending-admission, heartbeat, write, admission, inbound-wait and
close-handshake limits are finite validated endpoint/host configuration. Defaults
and numeric values require transport review before implementation, rather than
depending on an operating system's incidental buffering. Encoding and pending
admission must obey the byte budget; waiting senders cannot retain unbounded
encoded payloads. A locally invalid or oversized outbound value fails its send
before acceptance without entering the queue.

Accepted sends cannot later be changed to failed results: closing may discard
their undelivered data, and diagnostics record that loss without implying a
client acknowledgement. A send waiting for admission is pending and may fail;
an accepted send is complete even if its bytes have not yet reached the wire.
Cancelling an individual admission wait does not alone close the connection.

Protocol control frames never reach application handlers. Proposed close codes:
1002 for framing/protocol violations, 1003 for binary application messages in the
text-only transport, 1007 for invalid UTF-8/JSON or typed payload decoding, 1009
for oversized incoming messages, 1008 for inbound saturation/outbound timeouts,
and 1011 for handler failures. Normal closure uses 1000; graceful host shutdown
uses 1001 where possible. Sanitize reasons and never expose exception details.
A broken transport may prevent sending a close frame; 1006 is observational
and is never transmitted. Exact typed codecs remain an implementation gate.

### Disconnect, cancellation and failure

Once disconnect/closure is detected, the runtime atomically marks the connection
closing: stop admitting incoming work and new sends, cancel the connection scope
and pending operations, and discard buffered unprocessed input. A handler already
running receives cooperative cancellation; committed side effects are not undone.

After the active handler exits, invoke best-effort disconnect cleanup once in the
surviving process, using a separate bounded cleanup scope. Configure a finite
handler cancellation grace period: if the handler does not exit, invalidate its
connection capability, log the timeout and omit application cleanup rather than
run it concurrently against that handler's state. Runtime socket/resource cleanup
still proceeds. Cleanup itself has a finite timeout. No cleanup is guaranteed
after a process crash, and no thread termination or rollback is implied.

A handler failure is logged and closes the connection by default. Durable work
must explicitly enter a queue; socket message dispatch neither persists nor
automatically retries work. Connection-local ordering ends at disconnect and
does not impose ordering on separately queued durable work.

### Reconnect and restart

Reconnect creates a new connection ID and repeats authentication/authorization.
Old handles remain invalid. There is no automatic replay, subscription restoration
or session resumption. Applications needing recovery use application-level message
IDs, acknowledgements and durable state. A queued job may carry a user or
correlation ID but cannot preserve or resurrect a connection.

Cross-instance routing, rooms, durable replay, general streaming and gRPC are
outside this initial contract.

## Acceptance matrix for future implementation

These are required runtime demonstrations, not tests implemented by this record.
Use a controllable clock, versioned timezone fixtures, store fault injection,
competing scheduler instances, and a controllable socket peer.

| AC | Cases and observable assertions |
| --- | --- |
| AC1 | Valid/invalid dialect and zones; literal day wildcard; calendar dates; DST gap/fold (including startup between fold instants); stable identities across instances; mismatch rejection for timezone versions. |
| AC1 | Empty recovery interval, Skip, default RunOnce, positive bounded CatchUp, inclusive maxAge edge, count truncation, chronological insertion and discarded counts. |
| AC2 | Crash immediately before/after commit, queue insertion failure and competing producers: selected messages and progress commit together without duplicate identities. Handler retry keeps its occurrence identity. |
| AC2 | Activation at a due instant, stale producer versus revision/removal, retained queued work, reused names and backward clock: no history reinterpretation or identity reuse. |
| AC3 | Reject unauthenticated upgrade; fragmented input dispatches once only after completion; malformed/binary/oversized input gets the specified close outcome. |
| AC3 | Sequential handlers, concurrent connections, ordered output, count/byte/in-flight/pending limits, stalled writer and inbound saturation: bounded memory and deterministic closure. |
| AC3 | Admission/cancellation races, accepted-but-undelivered messages, pending-send cancellation, disconnect during handler, uncooperative handler grace timeout and bounded cleanup at most once. |
| AC3 | Handler fault has no retry; reconnect after restart has new identity/authentication and invalid old handles, with no replay or restoration. |
| AC4 | Scheduling and WebSocket adoption recorded independently; neither section authorizes the other's implementation. |

## Independent adoption and dependent work

Scheduling status: **proposed, needs explicit adoption**. Its implementation is
[#70 / RUN-15](https://github.com/gajo357/dsharplang/issues/70), after its durable
queue/store, time-value and setup prerequisites. Provider tests must establish
atomicity; passing compiler tests cannot establish durability.

WebSocket status: **proposed, needs explicit adoption**. Its implementation is
[#86 / HTTP-06](https://github.com/gajo357/dsharplang/issues/86), after HTTP/setup
prerequisites and transport/codec/limit review. Split lifecycle and bounded-output
implementation as required by the backlog. If either review grows too broad,
split decision children without blocking independent adoption of the other.

Merging this proposal does not adopt either section. Explicit adoption must name
the reviewed section/version. Exact D# syntax should follow existing registration
and handler conventions in a separately reviewed implementation contract.

## Repository verification

Documentation checks passed (59 documents, 177 local links), as did
`git diff --check` and bootstrap with the pinned SDK installed locally.
`bash .github/ai/verify.sh` passed 24 of 25 automation tests; its compiler resource
test and `bash scripts/verify-compiler.sh` were blocked by .NET shared-memory
initialization attempting to create `/home/agent/.dotnet` on a read-only
filesystem. Compiler execution is not verified in this environment. See the
[work plan](issue-18-plan.md) for evidence and limits. These checks do not
demonstrate schedule execution, transport behavior or adoption.
