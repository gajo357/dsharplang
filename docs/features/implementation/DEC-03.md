# DEC-03: Callable worker invocation and failure contract

**Status:** Draft proposal for review; not adopted or implemented.  
**Issue:** [#13](https://github.com/gajo357/dsharplang/issues/13).  
**Base:** `5759752af1cac9de82fce4284d31ce8287b288a4`.

## Work plan

Compare existing syntax, runtime direction and pipes/logging contracts; propose
one precise caller and supervisor outcome model with positive/negative D# sketches
and adversarial tests; identify unresolved decisions and downstream gates. This
decision-only issue changes this document, not compiler or runtime behavior.

## Sources and adoption boundary

This record proposes one bounded contract for review; **none of its proposed
rules are adopted or implemented by this PR**. The maintainer must explicitly
adopt a reviewed version before #51, #56, #57, or runtime setup #9 treats it as
an approved contract. Merging it, passing checks or implementing a different
slice is not adoption. The [implementation status](../DSharp_Implementation_Status.md)
records what the current compiler can actually execute.

The [syntax reference](../DSharp_Syntax_Reference.md) §§13–14.3 supplies the
provisional `CallError` shape; the [pipes and logging target](../DSharp_Pipes_and_Logging.md)
§§4–5, 8 fixes ordinary pipe/helper semantics and the `call(worker)` adapter.
The [architecture](../../ARCHITECTURE.md) describes isolation and shared
resources, and the [sequence](DSharp_Implementation_Sequence.md) identifies
[#51](https://github.com/gajo357/dsharplang/issues/51),
[#56](https://github.com/gajo357/dsharplang/issues/56) and
[#57](https://github.com/gajo357/dsharplang/issues/57) as dependent slices.
Resource initialization/disposal in [#17](https://github.com/gajo357/dsharplang/issues/17),
durable queues in [#14](https://github.com/gajo357/dsharplang/issues/14), and
foreign async interop in [#16](https://github.com/gajo357/dsharplang/issues/16)
remain separate gates. This record neither adopts those decisions nor selects
an actor package.

Retained decisions: worker references are runtime capabilities and may be called
and awaited; queue/event/cron agents are message-only and expose no handler
completion to their producer. A direct `.handle` call, callback or cast cannot
bypass dispatch. Every **normal** handler completion returns its declared Result;
a fault or execution-control interruption is not an implicit successful return.
Each normal worker invocation has fresh isolated state in a CLR process. This is
not an OS-process, BEAM-style failure-isolation, preemption or rollback promise.
Imports and installed packages do not register handlers or start services.

## Proposed caller-visible outcomes

Adopt the syntax reference's wrapper as the proposed initial contract:

```text
// Contract sketch; runtime standard types, not implemented declarations.
type FaultId { value: string }
type CallError<E> {
    | Application(E)
    | Unavailable
    | TimedOut
    | Cancelled
    | Faulted(FaultId)
}
```

For a worker with `handle(input: A): Result<T, E>`, `worker(input)` produces
`Task<Result<T, CallError<E>>>`. A no-input handler receives Unit. Handler
`Success(value)` maps to `Success(value)` unchanged; handler `Error(error)`
maps to `Error(CallError.Application(error))`, preserving the *entire* E value,
including nested Result, Option and nullable payloads. No null/Option flattening
or automatic conversion to strings is implied. An application-level cancellation
case returned as E remains `Application(E)`; it is not automatically a runtime
`Cancelled`. Nested worker calls can produce nested `CallError` types and callers
map them explicitly.

| Event at the call boundary | Proposed caller result |
| --- | --- |
| Handler and required invocation cleanup complete normally with `Success(value)` | `Success(value)` |
| Handler and required cleanup complete normally with `Error(error)` | `Error(CallError.Application(error))` |
| No admission: unregistered/stopping host, capacity unavailable, rejected call depth or failure before ownership transfer | `Error(CallError.Unavailable)` |
| Effective call deadline wins before a result is committed, including before admission | `Error(CallError.TimedOut)` |
| Caller cancellation wins before a result is committed, including before admission; or host shutdown cancellation wins after admission | `Error(CallError.Cancelled)` |
| Unhandled ordinary fault, or required cleanup fault before a result is committed | `Error(CallError.Faulted(id))` |

`FaultId` is a public, immutable diagnostic correlation value, not authority or
proof of a fault. Its suggested string field and the failure case names above
are part of this proposal. A runtime generates an ID for an ordinary fault and
records protected diagnostic details separately; the caller receives no stack,
exception object, secret, mutable CLR object or unrestricted diagnostic data.
A forged `FaultId` grants no access. Authorization, storage and retention of
fault details are deferred to observability/host policy.

The call task completes normally with exactly one of the typed outcomes above;
worker-boundary caller cancellation does not itself cancel that task or throw an
exception to the caller. Failures *while evaluating the call expression's
arguments*, before the runtime receives the call, retain ordinary expression
fault/cancellation semantics. Host-fatal CLR/native failures or process loss may
prevent any result; this record does not promise process survival.

## Proposed dispatch and capacity

Evaluate the worker reference and supplied argument expressions once in source
order. The call expression submits at most once when its task is created; awaiting
or observing that same task multiple times does not redispatch it. The runtime
validates that the reference belongs to a live, registered host and that the
input satisfies the worker's checked contract. A bare or forged value cannot
invoke `handle` directly or establish a valid runtime capability merely by
having a public data shape.

An admitted call starts one fresh invocation with independent local state and
explicitly resolved dependencies. The runtime may reuse scheduling infrastructure
but must not leak one invocation's locals, mutable builders or scoped resources to
another. This is an in-process boundary, not a separate process or a general
shared-memory safety proof. Immutable data may be shared; a service reference or
resource capability grants its checked operations, not access to private state.
Shared service dependencies must obey their own concurrency contract. A connection,
stream or transaction belongs to its approved scope and cannot be smuggled across
an invocation boundary. Precise dependency construction, scope and disposal APIs
are subject to #17 and #16; this proposal does not approve them by implication.

Admission is bounded and host-owned. The initial proposal is **no hidden waiting
queue awaiting admission**: if the configured per-host worker capacity is unavailable when an
otherwise live call is admitted, return `Unavailable` without invoking the
handler. The initial proposed default is 64 active worker invocations across all
worker definitions in that host, configurable to another positive finite value
at startup. Scope and value are validated before traffic; this is not a promise
of fairness or of a per-worker limit. No global limit across multiple processes
or hosts is implied. A slot is a **logical admission/in-flight limit**: it remains
occupied across `await` and until the invocation and its required cleanup have
actually ended, even if the caller has timed out or cancelled. This is distinct
from execution capacity: an awaiting invocation releases its scheduler/runnable
execution permit and OS worker thread so other already-admitted work can run;
its continuation reacquires execution capacity before running. This interpretation
is intended to satisfy
[#56](https://github.com/gajo357/dsharplang/issues/56). That issue's phrase
"release execution capacity" is ambiguous about admission slots; this is a
proposed interpretation, not a settled meaning. Reconcile it during review
before #56 implementation. Already-admitted runnable continuations may be queued
by the scheduler within the in-flight bound. Infrastructure may reuse threads and
tasks; it may not evade that bound by detaching untracked work.

An accepted call dispatches the handler at most once. There is no automatic
retry, queueing as a durable message, transaction, rollback or exactly-once
promise. An unavailable result before admission means no handler invocation was
started for that call; it says nothing about side effects of argument expressions.
At an accepted boundary, later timeout/cancellation/fault does not establish
whether business side effects happened.

## Proposed deadlines, cancellation and races

Every call has a finite positive monotonic-time wait budget. For the initial
proposal, the host default and maximum wait are **30 seconds**, configurable to
another finite positive value at startup; exact declaration syntax is deferred to
#17. A caller or enclosing operation can request a shorter deadline, never extend
the host maximum or an inherited parent deadline. The effective deadline is the
earliest applicable deadline and covers admission, handler work and required
cleanup. An already elapsed deadline returns `TimedOut` without dispatch. Clock
reads use a monotonic elapsed-time source; wall-clock corrections must not extend
the budget. These are proposed initial bounds, not claims of existing behavior.

An invocation can request cancellation of a child call. The caller's cancellation
signal is linked into child execution as a **cooperative request**; expiration also
requests cancellation. Before admission, a winning caller cancellation returns
`Cancelled` without starting a handler. After admission, cancellation may win the
caller-facing result while the callee continues until it observes the request and
actually exits. No thread abort, forced disposal, preemption or rollback is implied.
A host that is already stopping rejects admission with `Unavailable`; after
admission, a host-shutdown cancellation request competes as a `Cancelled`
candidate and cooperatively signals the callee. If a caller result has already
committed, shutdown cannot replace it. Deadline-driven cancellation is tagged as
deadline expiry and competes as `TimedOut`, not as `Cancelled`; signal linkage
must preserve that distinction.
A handler may observe the runtime's linked cancellation and exit cooperatively;
the eventual execution observation is distinguished from an application `Error`.
An unrelated cancellation exception, including one not attributable to the linked
signal, is an ordinary fault. Adapter details for foreign cancellation remain with
#16; an arbitrary exception type or message is not sufficient evidence of runtime
cancellation.

One atomic caller-result state arbitrates completion, deadline, caller
cancellation and admitted host-shutdown cancellation. A successful compare-and-set is the sole linearization point: once a
result wins, it cannot change or generate a second response. First read that state:
if it is already terminal, including a committed cancellation, do not attempt any
replacement. While it remains pending, a completion candidate may commit only if
the effective monotonic deadline is unexpired. If the deadline is observed expired
while still pending, attempt to commit `TimedOut`; a concurrent terminal winner
always prevails. If both cancellation and deadline are observed before either commits,
the first successful runtime transition wins; implementations must not claim a
deterministic ordering for physically simultaneous events. Before admission,
first resolve already committed cancellation or expired deadline, then capacity.
The runtime records which event won rather than inferring it from a later fault.

When a call has already returned `TimedOut` or `Cancelled`, a later handler
success, application error, cooperative cancellation or fault is **observed** by
runtime supervision, not delivered as a second caller result. A late fault gets a
protected diagnostic correlation ID; a late success or error is reported as the
actual eventual execution outcome with a late marker. Observe the task to avoid
unobserved faults. Capacity and invocation-owned resources remain held until the
actual exit and required cleanup; a non-cooperating handler can therefore occupy a
slot indefinitely even though the caller's wait was bounded. No strict wall-clock
completion is promised when the process/runtime is stalled or has terminated.

## Proposed nested calls and bounded cycles

Child calls inherit the earlier of the parent deadline, an explicitly shorter
child deadline and the host maximum. Cancellation is propagated as a cooperative
request; child work already submitted may outlive its caller. To bound recursive
call chains, runtime-owned call context also carries a maximum worker-call depth;
the initial proposal is **32 active call boundaries** per root invocation, with a
finite positive host-configured override validated at startup. The initial root
worker call counts as depth 1; each nested worker call adds one. At the default,
depth 32 can be admitted and an attempt to enter depth 33 is rejected. Exceeding
the limit returns `Unavailable` before starting another handler and emits a bounded
diagnostic. Application data cannot forge or reset that runtime budget. These
values and any source-level override syntax require review before adoption.

Self-calls and mutual calls are not statically prohibited and do not imply
reentrancy or deadlock detection. With capacity one, a nested call that cannot
acquire a slot fails `Unavailable`; any other wait is bounded by the inherited
finite deadline, not a promise of progress. Limiting the caller's wait does not
prove a non-cooperative callee terminated, released capacity or undid external
work. Cross-host/distributed cycle detection and guaranteed termination are out
of scope.

## Proposed cleanup, supervision and context

On actual handler exit the runtime observes its outcome, runs required invocation
cleanup once, and then records the final execution outcome. A normal handler
success or application error is eligible for caller completion only after required
cleanup succeeds. An ordinary cleanup fault before caller completion maps to
`Faulted(id)`; if a handler already faulted, preserve its primary fault ID and
record cleanup failures as secondary protected diagnostics rather than silently
losing them. If caller completion already timed out/cancelled, cleanup still runs
on actual exit and any later fault is observed only by supervision. Cleanup must
not dispose a still-running invocation's dependencies merely because the caller
stopped waiting. Exact ordering and combined-failure APIs shared with scoped
resources remain a #17 review gate; this record proposes the worker boundary only.

While the runtime survives, the supervisor/runtime observes each accepted
invocation's actual terminal outcome once **if that invocation terminates**,
separately from whether a caller received it. A non-cooperating invocation may
never terminate, and process loss may prevent any observation. A typed application
error is not an unexpected runtime fault; cancellation and timeout requests are
not automatic retry instructions. A handler fault does not automatically crash the
caller or sibling invocations. Supervision and restart of execution infrastructure
are separate from retrying the business operation. Exceptions may still be fatal to
the CLR process, and no per-actor GC or process isolation is promised.

Runtime adapters explicitly transfer permitted immutable diagnostic correlation
into a fresh invocation scope. Logging context is diagnostic data, not authority;
never infer permissions from it. Do not rely on accidental thread-local or Channel
execution-context flow. Queue propagation/durable correlation remains #14/#91;
this decision neither makes worker calls durable nor changes queue acceptance.

## D# contract sketches

These snippets are **illustrative future D#**, not fixtures accepted or executed
by the current compiler; worker declarations, async and runtime capabilities are
not implemented. The handler's declared E remains a domain error, while the
runtime wraps it only at a worker-call boundary:

```text
type StoreError { | Missing | Offline }
type SummaryError {
    | Storage(StoreError)
    | UpstreamUnavailable
    | UpstreamTimedOut
    | UpstreamCancelled
    | UpstreamFault(FaultId)
}

worker Summary(getInvoice: GetInvoice) {
    handle(id: InvoiceId): Result<string, SummaryError> {
        let outcome = await getInvoice(id)
        match outcome {
            Success(null) => Success("No invoice")
            Success(invoice) => Success(invoice.label)
            Error(CallError.Application(error)) => Error(SummaryError.Storage(error))
            Error(CallError.Unavailable) => Error(SummaryError.UpstreamUnavailable)
            Error(CallError.TimedOut) => Error(SummaryError.UpstreamTimedOut)
            Error(CallError.Cancelled) => Error(SummaryError.UpstreamCancelled)
            Error(CallError.Faulted(id)) => Error(SummaryError.UpstreamFault(id))
        }
    }
}
```

`call(getInvoice)` adapts the same reference into a callable step; it retains the
same `Task<Result<Invoice?, CallError<StoreError>>>`, deadline, admission and
supervision behavior. Ordinary `|>` still passes a value without implicit await
or Result unwrapping. Result helpers do not catch ordinary expression exceptions
and do not retry. Ordinary service calls are not worker calls and do not acquire
this wrapper merely because they are asynchronous.

Invalid examples to diagnose, not alternate dispatch paths:

```text
await queueAgent(message)          // queued reference is not callable
queueAgent.handle(message)         // cannot bypass queue/supervisor
workerRef.handle(request)          // worker handler is runtime-only
call(queueAgent)                   // callable adapter requires a worker
```

A queued producer may await the separate enqueue acceptance Result; its receipt
is not the handler outcome, even if a worker later calls a queue or vice versa.

## Required implementation evidence after adoption

Use controlled clocks, cancellation signals and barriers, not timing sleeps.
These are future acceptance tests; no runtime behavior is claimed by this record.

1. Execute generated D# worker calls for Success, application Error (including
   nested/null/Option values), explicit full error mapping and `call(worker)`;
   reject direct handle access, queued-reference calls and incorrect types.
2. Show one evaluation/submission and repeatable task observation; verify fresh
   state across concurrent invocations and explicit service dependency substitution.
   Trace parent/child correlation without treating it as authentication.
3. Suspend one admitted invocation and show another already-admitted invocation
   progresses on released scheduler capacity. Exhaust pre-admission capacity and depth; validate configured bounds and test
   self/mutual calls at capacity one. Verify no unbounded task creation and that
   logical capacity stays occupied through `await` and until actual cleanup.
4. Race caller cancellation, admitted host shutdown, deadline and completion with a deterministic clock
   and barriers. Assert exactly one outcome and distinguish expired-before-dispatch
   from unavailable admission. Exercise late Success, Error, cooperative cancel
   and fault, observing each without a second reply.
5. Hold a non-cooperative callee beyond its caller deadline. Verify the caller
   returns a typed outcome, no new call exceeds capacity, no still-used resource
   is disposed early, and eventual exit triggers exactly-once cleanup and releases
   capacity. Cover process/host shutdown separately without promising forced stop.
6. Throw ordinary handler and cleanup faults, including an unrelated cancellation
   exception; confirm `Faulted(id)`, protected/redacted details, primary versus
   secondary failure handling and independent sibling invocations. Test
   cancellation attributed to the actual runtime signal separately.
7. Verify side effects are not undone by timeout/cancellation or inferred retry;
   no code starts a retry or reports durable queue acceptance from a worker call.
   Test host stop, unavailable registrations and startup failure explicitly.

## Decisions requiring review before adoption

This proposal chooses the wrapper cases, typed cancellation, immediate bounded
admission, host-wide capacity-64 default, 30-second host wait default/maximum and
depth-32 default so reviewers can approve or amend concrete behavior. In particular, the maintainer should
review those defaults, cleanup-fault precedence and whether any bounded waiting
queue is desired, and resolve the #56 distinction between scheduler execution
capacity and logical in-flight admission. Exact policy declaration syntax, resource lifecycle details,
diagnostic access, cross-host transport/correlation and queue transitions remain
separate contracts. A reviewed/merged proposal is not itself maintainer adoption.

## Verification of this proposal

Bootstrap, documentation/link validation and compiler verification passed
(18 baseline and 362 feature tests). All 23 automation tests passed. The local
isolated verifier could not download its pinned Docker image (`Forbidden`);
GitHub Actions runs the unchanged verifier. These checks validate the repository,
not the unimplemented worker behavior or the adoption of this proposal.
