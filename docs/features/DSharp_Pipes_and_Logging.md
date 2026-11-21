# D# pipes, partial application and logging

**Revision:** 1.0  
**Date:** 13 November 2026  
**Status:** Target specification and implementation handoff; not implemented-status evidence.  
**Companions:** [Syntax](DSharp_Syntax_Reference.md), [agent instructions](DSharp_Implementation_Instructions.md), [backlog](DSharp_Feature_Backlog.md), [sequence](implementation/DSharp_Implementation_Sequence.md).

This document defines the decisions from the composition and logging discussion. It supersedes the older `pipeline {}` composition syntax, calls requiring all arguments, `Log.write` naming, database logging enabled by default, and a separate logging database as the prescribed default. Other language and runtime contracts remain in force. The exact APIs below are the initial implementation target; additional provider features are separate work.

## 1. Everyday code

```text
let preview = text |> trim |> replace("old", "new") |> take(80)

using scope = Logger.scope(tenantId: request.tenantId) {
    let result = await (
        request
        |> validate
        |> andThen(invoices.save)
        |> tap(invoice => Logger.log(
            Info,
            "Invoice {invoiceId} created",
            invoiceId: invoice.id,
            total: invoice.total
        ))
    )

    result
}
```

The caller uses normal functions, pipes and `Logger.log`. It does not construct log messages for a queue, inject an output, serialize JSON, open a database, await log storage, manage retries or start the Logger actor. Logger scopes are optional.

## 2. Value pipes

`value |> function` applies `function` to `value`. Pipes associate left to right:

```text
value |> first |> second
// Equivalent calls: second(first(value))
```

Rules:

1. Evaluate the left expression once, then the right callable expression once, then apply it. Lower through temporaries when necessary to preserve this order.
2. Use ordinary call checking, including partial application. If several arguments remain, the pipe supplies the first remaining argument and returns a function for the rest.
3. Call/member access binds more tightly than `|>`. Pipe chains have the low precedence already specified by the syntax reference. A newline before `|>` continues the expression; it does not begin a new statement.
4. Do not implicitly await, unwrap `Result` or `Option`, remove null, unpack a tuple, retry, parallelize or invoke a queue handler.
5. Piping a function value passes that function as a value. It does not silently mean function composition. Use a lambda to define a reusable chain.

```text
let clean = text => text |> trim |> take(80)
let pair = (3, 4)
pair |> consumePair         // One tuple argument, not two arguments
```

## 3. Automatic partial application

For a fixed-arity callable `(A, B, C) -> R`, supplying a positional prefix produces a function for the remaining parameters:

| Expression | Type / behavior |
| --- | --- |
| `f` | `(A, B, C) -> R` |
| `f(a)` | `(B, C) -> R` |
| `f(a, b)` | `(C) -> R` |
| `f(a, b, c)` | Execute and produce `R` |
| `f(a)(b, c)` | Execute and produce `R` |
| `f(a)(b)(c)` | Execute and produce `R` |

```text
fn calculate(a: int, b: int, c: int): int {
    a * b + c
}

let withA = calculate(2)
let withAB = withA(3)
let answer = 4 |> withAB      // 10
```

This is automatic partial application of ordinary multi-argument functions. Their declared type remains `(A, B, C) -> R`; it is not silently rewritten to `A -> B -> C -> R` in every type position.

### Argument order and execution

- Bind arguments left to right. Prefer configuration/dependencies first and flowing data last: `replace(old, replacement, text)` and `take(count, text)`.
- Evaluate the callable receiver and supplied argument expressions exactly once when forming the partial function. Capture their values. Execute the target body only when all arguments have arrived, once for each completed invocation.
- `f()` invokes a zero-argument function. For a function still requiring arguments, it supplies an empty prefix and yields a callable with those arguments remaining; it does not execute the body. Prefer bare `f` when no binding is needed.
- Too many arguments are an error. To bind a middle or final argument, use a lambda; placeholder/hole syntax is outside this version.
- Discarding a function-valued expression as a statement is an error. This catches accidentally omitting arguments. Returning a function, binding it, or passing it to another function is valid.

### Methods, generics and async

The partial-application target covers D# functions, function values, supported resource and interface methods (including defaults), and supported extension methods. Capturing a receiver must retain its dispatch semantics and obey its lifetime. Mutable builders and scoped resources cannot escape by being partially applied instead of captured in an explicit lambda. Existing fully supplied service-method calls remain executable until the #240 cutover; this target does not claim method partial application is implemented. Actors use the dispatcher rather than direct method calls.

For an async function, partial application produces a function whose eventual return is the same `Task<R>`. It starts no task until the remaining arguments arrive. It never blocks or awaits for the caller.

Infer generic arguments from supplied arguments, expected function type and the rest of the pipe expression. If several valid overloads or unresolved types remain, require an annotation; do not choose by declaration order. First resolve a unique callable and parameter order, then construct the partial application.

The initial feature supports positional prefixes. Fully supplied named calls retain their existing rules; partially supplied named calls require a lambda. Default parameters, variadic calls and overloaded foreign APIs need their separate interop rules and must not acquire guessed partial-application behavior. Existing supported full foreign calls continue to work.

## 4. Result composition through ordinary functions

Use five standard functions with the callback first and the flowing value last. They are library symbols, not keywords:

| Helper | On `Success(value)` | On `Error(error)` |
| --- | --- | --- |
| `andThen(next, result)` | Invoke `next(value)` and return its result | Preserve the error; do not invoke `next` |
| `map(transform, result)` | Return `Success(transform(value))` | Preserve the error |
| `mapError(convert, result)` | Preserve the success | Return `Error(convert(error))` |
| `tap(observe, result)` | Invoke the Unit-returning observer and preserve the success | Preserve the error |
| `tapError(observe, result)` | Preserve the success | Invoke the Unit-returning observer and preserve the error |

`map` wraps the callback's return value. It does not flatten a returned Result, Option or Task. Use `andThen` for a result-returning next step. `tap` and `tapError` have synchronous Unit-returning callbacks in this version; an async callback requires an explicit awaited function instead.

Partial application makes these normal calls composable:

```text
let outcome = request
    |> validate
    |> andThen(price(EUR))
    |> map(toReceipt)
    |> mapError(toCheckoutError)
```

All `andThen` steps share an error type. Map different error types explicitly. Do not erase them to strings or a universal error type.

### Async overloads

For `andThen`, support exactly these combinations, preserving the same success/error behavior:

| Input | Callback return | Combined return |
| --- | --- | --- |
| `Result<A, E>` | `Result<B, E>` | `Result<B, E>` |
| `Result<A, E>` | `Task<Result<B, E>>` | `Task<Result<B, E>>` |
| `Task<Result<A, E>>` | `Result<B, E>` | `Task<Result<B, E>>` |
| `Task<Result<A, E>>` | `Task<Result<B, E>>` | `Task<Result<B, E>>` |

`map`, `mapError`, `tap` and `tapError` accept either a Result or a Task of Result; the task input produces a task output. Their callbacks remain synchronous. Await the source once before inspecting it; invoke the applicable callback at most once. A task-producing step makes the composed result awaitable without changing the pipe operator.

```text
let createInvoice = async request => {
    await (
        request
        |> validate
        |> andThen(invoices.save)
        |> map(toReceipt)
    )
}
```

A typed error skips subsequent success callbacks. Creating the callback argument itself still follows normal eager expression evaluation; the helper only skips invoking that callback. Exceptions and cancellation propagate through the normal fault/cancellation contract; helpers do not automatically catch them into `Error`. No helper retries, starts parallel work, creates a transaction or makes a chain durable. A task supplied as an input may already be running; short-circuiting only prevents invoking later callbacks.

`Success(null)` and `Success(None)` are still successes. Nullable/optional payloads remain explicitly handled by subsequent code. Native nullability stays idempotent; Option tags are never erased by composition.

## 5. Actor dispatch and HTTP

Callable HTTP preprocessing uses ordinary pipes and Result helpers; wrapping middleware remains separate. Actor execution uses the three dispatcher operations in the [actor handoff](DSharp_Actor_Runtime_Instructions.md), not worker/queue adapters. Invoke waits for the exact Handle result through durable scheduling. Targeted Dispatch acknowledges one accepted invocation; untargeted Dispatch acknowledges a publication. Neither Dispatch waits for recipient completion. The exact declaration and dispatcher syntax remain subject to reviewed contracts.

The contextual `pipeline` member is a declaration slot containing an ordinary typed callable, not a `pipeline {}` expression. The compiler checks its input, successful output against the handler input, and explicit HTTP error mappings. The host invokes preprocessing once and invokes the handler only on success; without preprocessing the binding contract supplies the handler input directly. Pipes do not implicitly unwrap results. Wrapping middleware owns before/after behavior and cleanup, and authorization is checked at runtime rather than inferred from an Authorized-shaped record.

## 6. What Logger is

`Logger` is the standard system actor, registered once per application host. It owns a bounded, transient mailbox and output delivery state. It is provided by the runtime and does not need a declaration under `eventHandlers/`. This exception is explicit for runtime facilities; installing arbitrary packages still cannot auto-activate their handlers.

`Logger.log(...)` is its typed submission API, not a direct call to its handler. It returns Unit immediately after synchronous capture and a non-waiting mailbox submission attempt. It does not return a processing task or acceptance receipt. The actor receives immutable `LogEvent` messages and reports its own internal outcomes to runtime supervision.

The actor's mailbox is in memory. Logs may be lost on overflow, abrupt process termination or exhausted delivery retries. Selecting a database output persists events after processing; it does not make the preceding mailbox durable. Business queues retain commit-before-acknowledgement semantics. Durable auditing, if later needed, is a separately specified facility.

The API is host-scoped, despite its convenient global spelling. Compiler/runtime binding selects the current host. Concurrent tests and hosts have independent settings, scopes and outputs. `Logger` is not a mutable process-global service locator. A bootstrap console output is available during resource initialization; configured outputs take over before traffic is accepted. Bootstrap events are not replayed into newly configured outputs. Close Logger after producers drain.

## 7. Caller API and templates

```text
Logger.log(
    Info,
    "Invoice {invoiceId} created for {total}",
    invoiceId: invoice.id,
    total: invoice.total,
    customerId: invoice.customerId
)
```

The order is level, message template, then user-defined named fields. Levels in ascending severity are `Trace`, `Debug`, `Info`, `Warning`, `Error`, `Critical`. Resolve them as standard `LogLevel` cases, including disambiguation from Result.Error; they are not new keywords.

- Initial templates are compile-time string literals or constants. Placeholders are `{fieldName}` with identifier names; `{{` and `}}` escape literal braces. Format specifiers and arbitrary expressions inside placeholders are deferred.
- Each placeholder must have a matching event field in the call. Extra fields are allowed for filtering. Duplicate field names are errors. Repeated use of the same placeholder is allowed and evaluates its value once.
- Scope fields are attached automatically, but a template cannot depend on a hidden scope field. Supply an event field when it appears in the template.
- Preserve the template and structured values independently of the rendered message. Ordinary `$"..."` string interpolation does not recover structured fields; diagnose its use as the structured template with guidance to use named placeholders.
- Standard call evaluation remains eager: evaluate supplied expressions once in source order even if the level is disabled. Filtering avoids event allocation, formatting and serialization work where possible. For expensive field computation, the caller may explicitly guard with `Logger.isEnabled(Debug)`; do not introduce hidden lazy argument semantics.
- No automatic serialization of request bodies, service objects, exceptions, builders or closures. Values admitted as fields are immutable JSON-compatible scalars (including nullable scalars), lists and string-keyed maps of such values. UUID/time values use the documented standard textual encoding. Records, unions and Option require explicit field selection/mapping; `None` must not silently become null.
- Binary floating-point NaN/infinity and excessive event size/depth are invalid event values. They cause a dropped-event counter and independent diagnostic, not an exception thrown by Logger into business handling. A supplied field expression that itself faults still follows ordinary expression semantics.

The compiler lowers the convenient named fields into a checked field collection. Logger.log is a designated standard API with two required arguments and a named-field tail; a level/template call with no fields is complete. This does not introduce general variadic partial application. Callers do not assemble a heterogeneous dictionary themselves. Frozen runtime data preserves numeric/boolean/null values; it does not call `ToString` on everything. Formatting the message must not mutate or reevaluate fields.

## 8. Scopes and correlation

```text
using tenantScope = Logger.scope(tenantId: request.tenantId) {
    Logger.log(Info, "Creating invoice")

    using invoiceScope = Logger.scope(invoiceId: invoice.id) {
        await delivery.prepare(invoice)
        Logger.log(Info, "Invoice ready")
    }
}
```

`Logger.scope` is a synchronous caller-context helper associated with Logger. It is not a queued request to change the actor's own scope. It returns a scoped resource disposed by `using` on success, early return, postfix-? propagation, fault and cancellation.

Scopes flow through supported awaited calls and are isolated between concurrent logical invocations. Child scopes form immutable context chains. Merge outer fields, then inner fields, then event fields; the more specific value wins. Capture the final snapshot at log submission, so disposing/changing the current scope cannot alter buffered events. Runtime metadata is separate and cannot be overwritten by a user field with the same spelling.

Actors create a new invocation scope with parent correlation. Runtime boundary adapters explicitly transfer allowed context; do not rely on thread-local state or accidental execution-context flow through Channels. Queues persist configured correlation fields and reconstruct context for each attempt; they do not persist a live scope or automatically copy all user fields. Message ID stays stable across retries, while attempt and invocation ID identify each execution. Log context is diagnostic data, never authorization evidence.

## 9. Startup configuration and output plugins

No logging configuration means `Info` minimum with one `ConsoleLogOutput`. No log database, cloud exporter or retention job starts by default.

```text
app Billing {
    logging {
        minimumLevel = Info
        retention = 7d

        outputs = [
            ConsoleLogOutput(),
            DatabaseLogOutput(store: runtimeStore),
            CustomLogOutput(settings)
        ]
    }
}
```

`runtimeStore` refers to the application's existing queue database resource. Outputs are startup service recipes/capabilities, not serialized fields inside a data `type`. All components resolve before traffic is accepted. An explicitly supplied output list replaces the default; `outputs = []` intentionally disables delivery. No implicit duplicate console output is appended.

The application minimum is an admission threshold. An optional output minimum may further restrict it; it cannot recover events excluded at admission. Settings are validated at startup. Initial configuration is fixed for that host lifetime; live reconfiguration is later work.

The app retention value defaults to seven days and supplies the default for DatabaseLogOutput unless that output overrides it. Console has no retained history. Third-party storage outputs configure retention through their documented startup settings or external infrastructure; the minimal LogOutput.write interface itself does not promise retention management. Such providers must state which mechanism owns retention, validate supported periods when managing it, and surface failures; never imply a local setting changed a remote policy when it did not.

Every output implements this service interface:

```text
interface LogOutput {
    async write(events: List<LogEvent>)
        : Result<Unit, LogOutputError>
}
```

The list and events are immutable. `Success()` means the whole batch was accepted under that output's documented destination guarantee. A database output succeeds after commit. An output must not return success after launching an untracked background write. The minimal interface has no partial-batch acknowledgement; retrying a failed batch may repeat events already accepted remotely.

`LogOutputError` distinguishes a retryable delivery failure from permanent rejection, with a diagnostic code/message. Foreign exceptions are caught at the output boundary and recorded as output faults. Runtime-owned diagnostics keep stack information; business callers do not receive these outcomes. Third parties implement the same public interface and use normal package/DI mechanisms. Outputs may be native D# services or supported .NET adapters; cloud-provider implementations are separate packages/tasks.

## 10. What the actor does

1. Receive already-captured LogEvents; keep bounded buffering and per-output delivery state.
2. Form batches and dispatch them to outputs. Track success independently per output.
3. Retry only the failed output, with bounded attempts and backoff. Keep event IDs stable across retries.
4. Surface drops, output failures and backlog measurements through independent runtime diagnostics/inspection.
5. During shutdown, stop new admission after producers drain, finish pending writes up to the host deadline, report remaining drops, then release outputs before closing the shared RuntimeStore.

Use the actor runtime's scheduling/supervision infrastructure, not a second homemade actor framework. Bookkeeping is execution-owned state; public source declarations do not expose it as shared data. Output services run with at most one in-flight write per configured output. Completion reports return to the actor; mutable delivery state is not shared with output tasks. A slow output has its own bounded backlog and does not hold up healthy outputs.

Initial overridable operational defaults:

| Setting | Default |
| --- | --- |
| Admission and each output backlog | At most 4,096 events and 16 MiB of canonical event bytes, whichever is reached first |
| Maximum event | 64 KiB encoded; maximum field nesting depth 16 |
| Batch | Up to 128 events; flush nonempty batch within 250 ms |
| Overflow | Drop the incoming event for the affected full buffer; increment a counter |
| Output attempts | Three total attempts, with 1 s then 2 s delays |
| Output write deadline | 5 s |
| Shutdown drain deadline | 5 s, bounded by the host's remaining shutdown deadline |

Never create an unbounded backlog of tasks or retries. Count active batches toward budgets. A timed-out output might still be running: do not overlap a retry with that operation. Quarantine a non-cooperating output until it returns; its backlog remains bounded. A late/ambiguous remote completion can cause duplicates, so no exactly-once promise is made. Explicit terminal failures are dropped for that output, with counters.

The fallback diagnostic path must bypass Logger and be rate-limited; use runtime counters and best-effort emergency stderr reporting. The caller must never wait for this reporting. Logger/output-internal logs cannot be fed recursively into the same failed delivery path. Host-fatal failures remain outside normal logging guarantees.

## 11. Event data and JSON

In memory, `LogEvent` is typed immutable data. JSON is its standard export/storage envelope. The schema contains a version, stable event ID, capture timestamp in UTC, level, source category, template, rendered message, user fields and runtime context.

```json
{
  "schemaVersion": 1,
  "eventId": "a65eb374-4c31-4be9-949b-c98e0dc21952",
  "timestamp": "2026-11-13T08:00:00Z",
  "level": "Info",
  "category": "Billing.CreateInvoice",
  "template": "Invoice {invoiceId} created for {total}",
  "message": "Invoice INV-42 created for 125",
  "fields": {
    "tenantId": "TEN-5",
    "invoiceId": "INV-42",
    "total": 125,
    "paid": false,
    "note": null
  },
  "context": {
    "invocationId": "b1bc112c-053d-40aa-a9c6-5b747377658d"
  }
}
```

Category comes from the originating D# module/service/handler, not the Logger actor. Context can additionally include trace/span IDs, parent invocation, handler, queue/message identity and attempt when available. Missing context fields are omitted; explicitly supplied null user fields remain present as null.

JSON encoding preserves numbers as numbers and their written precision, strings as strings, booleans as booleans, lists/maps as arrays/objects. Preserve large integers and decimals in the codec; do not round-trip through a binary floating-point intermediate. A provider with narrower value/query capabilities must document and validate those limits. Core Option has no new automatic JSON encoding as a side effect of logging.

Console emits one compact JSON event per line by default. A developer-friendly text formatter may be configured; it changes rendering only. All outputs receive the same captured event identity and fields.

## 12. Database storage and retention

`DatabaseLogOutput(store: runtimeStore)` uses the same configured database/provider as queues. It owns a separately versioned logging table/migration area and indexes. It does not create one file per queue, another mandatory database or a persisted job for each log event.

Initial logical table:

| Column | Purpose |
| --- | --- |
| `sequence` | Store-assigned increasing key for stable pagination |
| `event_id` | Unique within the application; deduplicate output retries |
| `captured_at`, `received_at` | Event time and database acceptance time in UTC |
| `level`, `category` | Structured severity and originating component |
| `trace_id`, `invocation_id`, `message_id` | Nullable common correlation columns |
| `schema_version`, `template`, `message` | Envelope version and display data |
| `fields_json`, `context_json` | Remaining typed structured data encoded as JSON |

Use short, atomic batch transactions with duplicate event IDs ignored. Initial indexes cover event ID, time/sequence, level/time, category/time and common correlation IDs. Custom fields remain queryable from JSON; optional expression/projection indexes can accelerate selected fields later. Do not index every key automatically.

Retention is measured from database `received_at`, so clock skew in a producer timestamp cannot silently delete a newly accepted event. The default is seven days. Cleanup runs on startup and periodically in small batches; expired records are omitted from normal queries even before physical cleanup. Retention is a maximum visibility period, not a guarantee of exact-time disk reclamation or backups deletion. Allow an explicit keep-forever setting rather than overloading null/zero.

When enabled, the database output has a configurable logical retained-data budget (initial default 256 MiB). Evict oldest retained events when necessary, in bounded batches, and count capacity eviction separately from expiration. This limit concerns retained log data, not the whole shared database file or its WAL. Never remove queue/business rows or run aggressive whole-file compaction as a logging cleanup action. Shared-store migrations and maintenance follow the normal provider ownership rules.

Database logging remains outside business/queue transactions. Log delivery failure cannot roll back a completed handler. Test contention between batching, retention and queue claims; avoid holding a transaction while performing remote output or handler work.

## 13. Searching stored logs

The database plugin exposes a separate read-only `LogReader` service. The observer uses this service; it does not ask the Logger actor to run queries or execute user SQL. With no database/query-capable output configured, show that history is unavailable; console output is not secretly buffered forever for search.

The initial query model supports:

- Required time range (default UI range: last hour), with explicit capture-time or receive-time selection.
- Exact/minimum level, category, trace/invocation/message IDs.
- AND-combined user-field predicates: equality, numeric comparison, field existence and explicit null.
- A page size (default 100, maximum 500) and opaque cursor; newest received records first using `sequence` as a stable tie-breaker.

For example, these are filter descriptions, not a new D# expression language:

```text
last 24 hours
level >= Warning
fields.tenantId == "TEN-5"
fields.total > 100
```

The reader contract is `async query(query: LogQuery): Result<LogPage, LogQueryError>`. LogQuery carries the time bounds/time-field choice, typed filter list, page size and nullable cursor; it contains no executable predicates or SQL. The reader returns `Result<LogPage, LogQueryError>` asynchronously. `LogPage` contains immutable events and a nullable next cursor. Cursor contents bind the query and a first-page high-water sequence; newly inserted events do not shift subsequent pages. Retention may remove records during pagination, so fewer results are possible. Do not promise a long-lived database snapshot.

Queries bind typed values as parameters and validate field paths. Missing and explicit null are distinct: `exists(note)` matches a present null, `isNull(note)` matches only present null, and `eq(note, "x")` excludes both missing and null. Missing/type-incompatible fields do not satisfy numeric comparisons. Never compare numeric JSON lexically. Preserve supported numeric semantics or return a clear unsupported-query error for a provider limitation; do not silently round or change comparison meaning.

Use native indexed/JSON operations where supported and bounded execution time. Unindexed arbitrary-field queries may be slower; a timeout is an explicit query failure, not a silently incomplete successful page. Initial work need not implement full-text search, arbitrary boolean query languages, cross-cloud search or indexing every custom parameter. Provider-specific query translation belongs behind LogReader.

The observer requires its existing authenticated read permission; filter fields such as tenantId are not themselves an access-control mechanism. Query services are not public HTTP routes merely because D# declarations are public. Retention and query failures are isolated from queue operations.

## 14. Implementation slices and acceptance

Keep one bounded issue, plan and PR per slice. Preserve existing issue mappings and reconcile any work already merged.

| Backlog ID | Deliverable | Essential evidence |
| --- | --- | --- |
| LANG-18 / #35 | Existing plain pipe feature | Evaluation order, precedence and negative implicit-await/unwrap tests |
| LANG-41 / [#129](https://github.com/gajo357/dsharplang/issues/129) | Automatic partial application; ordinary-function child [#135](https://github.com/gajo357/dsharplang/issues/135) | Grouped calls, receiver/argument evaluation once, generic inference, async deferral, lifetime checks |
| LIB-04 / [#130](https://github.com/gajo357/dsharplang/issues/130) | Result composition helpers; synchronous child [#136](https://github.com/gajo357/dsharplang/issues/136) | Sync/async matrix, errors skipped, callbacks once, no retry/flattening, nullable/Option payloads |
| HTTP-03 / #83 | HTTP callable preprocessing | `pipeline = ...` plus ordinary pipes; precise handler input, short-circuit and error mapping |
| LOG-01 / [#131](https://github.com/gajo357/dsharplang/issues/131) | LogEvent, templates and scope capture | Typed fields, compile diagnostics, concurrent/await scope isolation and cleanup |
| LOG-02 / [#132](https://github.com/gajo357/dsharplang/issues/132) | Logger actor, console and plugin lifecycle | No caller I/O wait, real actor dispatch, bounded delivery, failed-output isolation, shutdown |
| LOG-03 / [#133](https://github.com/gajo357/dsharplang/issues/133) | Database output and retention | Same RuntimeStore, committed batches, deduplication, expiry/capacity, queue coexistence |
| LOG-04 / [#134](https://github.com/gajo357/dsharplang/issues/134) | LogReader and observer search | Typed filters, JSON null/missing/numbers, pagination, auth, absent store and timeout cases |
| OPS-01 / #91 | Runtime instrumentation/metrics/tracing | Reuse Logger; correlate HTTP, actor invocations and queue attempts without recursive delivery |

Run executed generated-C# tests for language claims; syntax acceptance is insufficient. Use fake outputs and controlled clocks/barriers for delivery tests. Include a blocked-output test proving producer calls and healthy outputs progress; test crash loss as documented, not as a false durability guarantee. The database/query slices require real-provider integration tests, not only mocks. Option remains LANG-40 and nullable-generic issue #30 keeps its own completion criteria.

### Repository integration notes

This document was imported from the user-supplied revision 1.0 on 13 November
2026. It is the target for future implementation, not a claim that partial
application, async composition or Logger already executes. The merged #35 pipe
and #36 tuple milestones keep their historical scope.

Core #135 requires an inventory and narrowly reviewed migration of obsolete
under-application rejection assertions before implementation; unrelated tests
and the trusted verifier remain protected. No test migration is performed by
this specification update. #129 remains open for the later async, method,
extension and lifetime matrix, and #130 remains open for async helpers.

Runtime dispatcher and admission work belong to #241, #57 and #61 or
their bounded integration children. LOG-04 depends on the core observer #92;
the observer core must not depend on LOG-04. Runtime/setup and remaining design
gates still apply. Slice plans must define concrete logging data/error/query
types, numeric codec limits and operational-setting declarations while
preserving this document's semantics and defaults.

## 15. What stays outside this change

No new general pipeline block, magical pipe unwrapping, arbitrary argument holes, automatic error unions, implicit actor retries, durable distributed workflows, guaranteed audit delivery, automatic Option wire schema, or mandatory database/cloud logger. The startup handler folder rules, Channels/database architecture, C# backend and Minimal API hosting remain unchanged. Third-party output packages and advanced search can be implemented independently once the output/event contracts pass their conformance tests.
