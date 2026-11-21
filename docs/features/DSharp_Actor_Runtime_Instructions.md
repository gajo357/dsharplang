# D# actor runtime: implementation instructions

Date: 21 November 2026  
Status: design handoff; not a claim that these features are implemented. The #239 cutover excludes legacy aliases and compatibility formats even where this source mentions an optional alias. Exact declaration/registration spelling and the runtime fault surface await bounded reviewed contracts (#51/#54 and #13/#14).

## 1. Scope and decisions

Implement this model consistently across the language design, compiler, runtime, examples, and tests. Reconcile conflicting older specifications before implementation. Preserve unrelated language decisions.

The selected model is:

- Actors are the application execution unit and expose one Handle entry point.
- Actor-to-actor requests go through the runtime dispatcher.
- Invoke waits for the actor's final result. Dispatch waits for durable acceptance only.
- Dispatch<Actor>(message) targets one actor route; Dispatch(message) without an actor target publishes to zero-to-many subscribers.
- A default actor defines the callable target, input/output contract, and fallback behavior.
- Ordered specializations provide alternative implementations selected by canHandle.
- Resources replace the D# service concept for HTTP, databases, files, and external adapters.
- Persist the logical chain of awaited actor invocations and identify the actor that failed.
- Built-in HTTP resources must use .NET IHttpClientFactory internally.

Do not introduce a workflow language type. Do not require separate worker and queue actor categories merely to determine whether the caller may await a result.

All D# and registration examples below are illustrative syntax. Preserve these semantics when adapting them to the repository's grammar.

## 2. Actors and typed dispatch

An actor has one unambiguous Handle signature in the initial implementation:

~~~text
actor InvoiceActor {
    Handle(message: CreateInvoice): Result<Invoice, InvoiceError> {
        // Default invoice implementation.
    }
}
~~~

Actors may have private helpers and declared resource dependencies. Callers do not construct actor instances, inject other actor instances, or directly call another actor's Handle.

Provide a dispatcher scoped to the current application runtime and invocation context. It may be available as a built-in facility. It must not depend on process-global mutable application state.

| Operation | Await completes when | Returned value |
| --- | --- | --- |
| Invoke<Actor>(message) | The selected actor's Handle finishes | Handle's declared result |
| Dispatch<Actor>(message) | The invocation and selected implementation are durably accepted | Invocation receipt/ID |
| Dispatch(message) | The publication and its delivery intents are durably accepted | Publication receipt/ID; no combined subscriber result |

~~~text
// Alternative A: wait for the completed operation.
outcome = await dispatcher.Invoke<InvoiceActor>(message)

// Alternative B: submit independent work and continue.
receipt = await dispatcher.Dispatch<InvoiceActor>(message)
~~~

These are alternative ways to initiate work, not two steps required for one invocation. Invoke does not return a handle that must then be awaited again.

Both targeted operations use the same registered actor routing and execution policies. Waiting for a result does not bypass the queue or change delivery into an in-process method call. Each accepted invocation has one selected implementation; delivery attempts may occur more than once.

### Type checking

Define an explicit compiler rule deriving Invoke's input and completed result types from the target actor's Handle. In the example, the input is CreateInvoice and the result is Result<Invoice, InvoiceError>.

Reject invalid targets and incompatible message types at compile time where statically known. Generated C# must preserve the typed return without exposing object casts, reflection-based guessing, or extra response type arguments to D# callers.

Dispatch<Actor> validates the same input contract but returns an acknowledgement instead of the business result. An actor returning no value can still be invoked and awaited for completion.

Dispatch(message) infers the message contract from its argument, never an actor target. The absence of an explicit actor type argument selects publication semantics even if only one subscriber exists. Invoke requires an explicit actor target.

Discarding an unobserved Invoke or Dispatch awaitable must not be presented as reliable submission. Require observing acceptance/completion through the supported asynchronous usage, with a diagnostic for discarded calls.

### Acceptance and outcomes

Before acknowledging targeted Dispatch<Actor>, durably store the payload, invocation identity, requested target, selected implementation, and required routing/chain metadata. Acknowledgement confirms responsibility for the work, not successful execution.

Reject unregistered targets and invalid submissions explicitly. Persistence failure must not produce a successful receipt. If acceptance is uncertain after a lost acknowledgement, retain a stable submission identity for status checks and deduplicated retry.

A normal Result failure is a completed business answer. It is not automatically an execution fault or retry request. Runtime faults and exhausted retry policies use the runtime's failure mechanism.

A caller's wait timeout does not prove that the actor stopped. Cancellation is a separate explicit operation and does not roll back completed external effects.

Waiting on Invoke must not occupy a worker thread. Preserve any exclusive ordering ownership promised by the route. Reject descendant invocations requiring an exclusive target/key permit already held by an awaiting ancestor. Use bounded waits to surface other scheduling cycles; full deadlock detection is not assumed.

Neither form of Dispatch waits for recipient Handle completion.

### Untargeted Dispatch: publication

Omitting the actor target means SNS-style publication:

~~~text
publication = await dispatcher.Dispatch(InvoiceCreated(invoice.id))
~~~

Deliver the message independently to every explicitly registered subscription for its message contract. Zero subscribers is valid and still returns a publication acknowledgement. There is no combined business result; subscriber return values do not change the publication receipt.

Subscription registration is explicit at startup. Illustrative syntax:

~~~text
app.Register<SendInvoiceEmailActor>()
    .SubscribeTo<InvoiceCreated>()

app.Register<UpdateInvoiceIndexActor>()
    .SubscribeTo<InvoiceCreated>()
~~~

Validate that each subscriber's Handle accepts the message contract. Registering an actor for targeted invocation does not automatically subscribe it to every matching publication. Use explicit subscription identities; multiple runtime instances of one subscription compete for its deliveries rather than each receiving another copy.

Each subscription names a public/default actor target. Apply that target's ordered specializations to select one implementation for that subscription. Do not stop at the first matching subscription: all subscriptions receive independent deliveries, while specializations select only within their own target.

Freeze the subscriber set and selected implementations at acceptance. Persist all delivery intents, or a recoverable fan-out record containing them, before acknowledging success. If routing or validation fails before acceptance, reject the publication before scheduling any recipients. Registration changes must not redirect a previously accepted publication.

Give every logical delivery a stable identity using the publication ID and subscription ID. Recovery of partial fan-out must not create duplicate logical deliveries or omit recipients; execution attempts remain at least once.

After acceptance, each delivery has independent execution, retries, and failure status. One subscriber's failure must not stop others or retroactively fail the publisher. Each delivery starts outside the publisher's awaited actor chain; publication IDs provide correlation only.

Use Dispatch(message) as the publication API. No separate Publish method is required. If an existing compatibility alias is retained, it must delegate to precisely these semantics.

## 3. Default actors and specializations

Use the terms **default actor** and **specialization**. This is alternative implementation selection, not inheritance, a decorator chain, or a parent/child actor lifetime.

The default actor supplies the stable public target and its Handle input/output contract. A specialization must provide:

- The same Handle input and declared result types.
- A canHandle method accepting that input and returning bool.
- Its own implementation and any explicitly declared resources.

~~~text
actor CreditInvoiceActor {
    canHandle(message: CreateInvoice): bool =>
        message.kind == Credit

    Handle(message: CreateInvoice): Result<Invoice, InvoiceError> {
        // Specialized invoice implementation.
    }
}
~~~

No interface or extends declaration is required solely for this mechanism. Ordinary interfaces remain available elsewhere in the language. A specialization does not inherit private state, helper methods, or the default implementation.

### Startup registration

Register the public target and ordered specializations explicitly:

~~~text
app.Register<InvoiceActor>()
    .SpecializeWith<CreditInvoiceActor>()
    .SpecializeWith<ForeignInvoiceActor>()
~~~

Registration establishes the relationship. Freeze it before accepting application work.

Selection must:

1. Evaluate CreditInvoiceActor.canHandle.
2. If false, evaluate ForeignInvoiceActor.canHandle.
3. Select the first true candidate.
4. Select InvoiceActor if all candidates return false.
5. Execute exactly one Handle.

The default is automatically the final fallback and requires no canHandle. The runtime must not execute it before or after a selected specialization.

canHandle must be cheap, deterministic, and side-effect-free. It inspects immutable input; it must not query resources, send messages, read ambient time/randomness, or mutate state. Its failure is a routing error, not an implicit false result.

Validate Handle compatibility and predicate signatures at compile time where possible and startup otherwise. Reject duplicate entries and invalid registrations. Specialization registration alone does not expose an independent public target. Any standalone registration must be explicit.

Keep the first implementation flat: one ordered specialization list per public target. Nested specialization graphs and live registration changes are outside this handoff.

### Execution guarantees

Persist the selection before execution. Retries keep the selected actor identity. A failure, timeout, or business rejection must never fall through to another candidate.

Registration changes affect new selections. If an existing invocation's implementation is unavailable or incompatible after deployment, retain it as explicitly blocked/failed for operator action; do not silently reroute it.

Compatible updates to that implementation remain allowed: they must honor the Handle contract and read the stored input/result schemas. Build identifiers may aid diagnosis but do not preserve executable code. This handoff does not require hosting historical binaries or a workflow version-pinning system.

Ordering, FIFO, per-key concurrency, and admission limits apply to the public target across all specializations. Additional implementation limits may constrain execution but cannot weaken those guarantees.

The dispatcher is concurrent infrastructure. Each target retains its own execution policy; application-wide dispatch must not be serialized through one global actor.

## 4. Persisted actor stack: awaited invocation chain

The required "actor stack" is a logical chain of actors awaiting one another. Its purpose is to show the callers and the actual actor in which a failure originated.

Persist only the invocation relationships and actor identities needed for that explanation, alongside existing execution/failure records. Do not capture CLR stack frames, local variables, resource objects, or arbitrary continuations.

For example:

| Depth | Requested target | Selected implementation | Relationship |
| --- | --- | --- | --- |
| 0 | OrderActor | OrderActor | Awaiting the invoice invocation |
| 1 | InvoiceActor | ForeignInvoiceActor | Awaiting the storage invocation |
| 2 | StoreInvoiceActor | StoreInvoiceActor | Origin of the execution failure |

The user should see that StoreInvoiceActor failed while ForeignInvoiceActor was handling an InvoiceActor request made by OrderActor.

### Minimal data

Reuse the runtime's invocation records. Add fields only where the existing schema lacks them.

| Data | Purpose |
| --- | --- |
| Invocation ID | Identify one logical execution independently of actor name |
| Awaited parent invocation ID, nullable | Reconstruct the caller chain |
| Parent attempt identity | Distinguish children created by different attempts of the caller |
| Requested actor identity | Identify the public target named by the caller |
| Selected actor identity | Identify the implementation whose Handle executes |
| Contract/schema compatibility metadata | Validate stored input/results against the selected actor after deployment |
| Status, attempt identity, normalized failure | Reuse ordinary execution records |
| Origin failing invocation ID on propagated faults | Preserve the actual source of failure |

A root ID or rendered stack may be derived or indexed for efficient querying. Do not duplicate message bodies or capture locals just to display this chain.

Each child created through Invoke records its awaited parent context with durable acceptance. Persist the relationship before child execution. Do not rely solely on AsyncLocal, a live Task, or transient logs to retain it.

### Invoke and Dispatch boundaries

- Invoke creates an awaited-parent relationship.
- Targeted Dispatch<Actor> creates an independent execution root, even though the caller awaits its acceptance.
- Untargeted Dispatch(message) creates independent subscriber deliveries outside the publisher's awaited chain.
- Optional correlation metadata may connect detached work for searching, but it must not imply an awaited dependency or propagate its later failures.
- A root caller outside an actor has no parent actor frame.

If A invokes B and B dispatches C, a later failure of C must not mark A or B as failed. If B's Dispatch acceptance itself fails, report an error at B's dispatch call; do not claim that C.Handle failed.

### Failure propagation and retries

While callers are waiting, retry the failing child according to its execution policy. Keep the same logical invocation/selection and record attempts separately. Do not automatically restart every waiting ancestor for an intermediate child failure.

When a child's execution fault becomes terminal, propagate it through unhandled Invoke waits while retaining the origin invocation ID. Do not replace the failing actor with the immediate caller at every boundary or append duplicate frames during rethrow.

A caller may catch the fault and return a normal outcome. In that case, preserve the child's failure for inspection without declaring the successful caller failed.

A declared Result failure remains a business value. It does not automatically trigger the runtime fault chain. Existing logging may describe it separately.

If a resource operation throws inside an actor, that actor is the origin frame. The resource is not another actor frame.

For a submission or selection error before any target Handle starts, report the dispatch stage and requested target with no selected implementation. Do not invent an executed actor frame.

Parallel Invoke calls, if otherwise supported, create sibling records. A failure view shows the ancestry of the failed branch; it must not invent a linear ordering among siblings.

### Durability is diagnostic here

The chain and recorded failure must remain inspectable after a process restart. This does **not** restore a suspended Handle body or its local variables.

Retain the existing explicitly documented execution recovery policy. If recovery restarts an actor invocation from its input, external operations may repeat. Parent links alone do not prevent duplicate child submissions.

Do not claim that retrying a failed leaf automatically resumes lost or already-failed callers. Full replay, saved step results, continuation recovery, and workflow version migration are separate features, not prerequisites for this diagnostic chain.

Keep ancestor records available while retained child/failure records depend on them. The initial observer needs only a readable chain, requested/selected identities, and the failing actor with its error.

## 5. Replace services with resources

Remove services as a competing D# application execution abstraction.

- Move application work expressed as old business services into actors.
- Represent HTTP, database, filesystem, and external-system access as resources.
- Keep pure calculations and formatting as ordinary functions.
- Retain ordinary interfaces where useful for resource providers, test substitutes, and plug-ins.
- Do not rename standard .NET framework APIs such as IServiceCollection; this change concerns the D# application model.

Resources are typed APIs used within an actor invocation. They do not automatically acquire a mailbox, Handle method, actor identity, or independent retry lifecycle.

Actors declare resource dependencies explicitly. Startup registration binds configured resources and validates missing or ambiguous dependencies. Avoid a general Services.Get lookup inside actor code.

~~~text
actor DownloadDocument(http: Http, files: Files) {
    Handle(message: DownloadDocumentRequest): FilePath {
        content = await http.GetBytes(message.url)
        return await files.Write(message.destination, content)
    }
}
~~~

Resource registration and dependency syntax remain illustrative; a new resource keyword is not required to implement the concept. Back resources with suitable .NET libraries and narrow adapters. Allow custom providers without requiring a generic repository layer over every database operation.

### Ownership and lifetimes

| Resource | Runtime-managed shared infrastructure | Scoped ownership |
| --- | --- | --- |
| HTTP | IHttpClientFactory, configuration, managed handlers/pools | Client usage, request, response, response stream |
| Database | Data source and connection pool | Borrowed connection, reader, transaction |
| Files | Adapter and configured filesystem access | Open file and stream |

Shared adapters must support concurrent use without storing mutable per-invocation data in shared fields.

Release resources on success, error, and cancellation. A transaction may contain several ordinary awaited database operations on its connection. It must finish before a durable actor wait, cross-actor invocation, or restart boundary.

Do not place live connections, transactions, resource handles, or streams in actor messages, persisted results, or diagnostic chains. Pass immutable data or durable identifiers instead.

A resource method may encapsulate a complete transaction. The actor should not need to orchestrate individual connection-management calls for routine operations.

Actor retries can repeat HTTP requests, database writes, and file changes. Make relevant operations repeat-safe with stable operation identities, uniqueness constraints, idempotent external APIs, or explicit reconciliation. A persisted actor chain is not an exactly-once execution guarantee.

Keep business database transactions separate from RuntimeStore transactions. Coordinating business changes with queued work requires the existing outbox/equivalent design where they cannot commit together.

## 6. Mandatory HTTP implementation: IHttpClientFactory

All built-in HTTP resources must obtain clients through **System.Net.Http.IHttpClientFactory**, registered using AddHttpClient from Microsoft.Extensions.Http. Use the same integration in ASP.NET hosts and background-only .NET hosts.

This is the selected D# implementation policy. The factory remains an internal detail of the HTTP resource; actor code uses the resource API.

Illustrative host-side C#:

~~~csharp
builder.Services.AddHttpClient("supplier", client =>
{
    client.BaseAddress = new Uri(settings.SupplierBaseUrl);
});
~~~

A shared resource adapter may retain the factory and configured client name. Obtain a factory-created client for a bounded operation or attempt. Do not retain a factory-created client indefinitely inside a singleton adapter. Let the factory manage handler and pool lifetimes.

Do not add a parallel built-in HTTP path that constructs standalone HttpClient instances per request. Do not implement a custom pool beside the factory. Use bounded configured client registrations; never create a new client name from every message or tenant ID.

Use request-local headers and authentication context. Dispose requests, responses, and finished streams. Configure deadlines and cancellation. Avoid shared cookie state across tenants; cookie-based integrations need explicitly isolated handling.

These lifetime and isolation requirements follow Microsoft's factory guidance; see the references below.

Define actor-level versus HTTP-attempt retries explicitly. Keep automatic HTTP retries disabled initially unless configured, and prevent accidental retry multiplication. A timeout must not be treated as proof that an external write did not happen.

Tests may replace the factory or message handler. Normal production HTTP resource execution must still use the factory-backed path.

## 7. Implementation sequence

1. Reconcile specifications and examples: services/resources, dispatcher calls, awaiting, and specialization terms.
2. Define compiler-visible Handle contracts and typed Invoke/Dispatch operations.
3. Add explicit registration, ordered specialization validation, and selection.
4. Integrate durable acceptance, fixed selected implementations, target-wide execution limits, and recoverable untargeted Dispatch fan-out.
5. Persist awaited parent relationships and original failure attribution.
6. Expose the chain through existing inspection/logging or Observer APIs.
7. Add resource binding/lifetimes and factory-backed HTTP; integrate database/file adapters.
8. Validate recovery and failure boundaries before claiming the model complete.

Use small reviewable changes. Follow repository instructions for implementation plans and PRs. This document authorizes the design direction; it does not assert that the runtime, syntax, or recovery mechanisms already exist.

## 8. Required verification

Use meaningful compiler and runtime tests for these guarantees:

1. Invoke derives the Handle result type; wrong inputs and invalid targets are rejected.
2. Targeted Dispatch<Actor> acknowledges durable acceptance without waiting for Handle completion.
3. Every specialization requires canHandle and compatible Handle types.
4. Registration order selects the first match; the default is selected only if every specialization returns false.
5. Only one Handle runs per targeted invocation or subscription delivery; a fault or Result failure never triggers fallback.
6. Retries preserve the selected actor and do not bypass target-wide per-key ordering.
7. A nested awaited failure reports the full chain and original failing actor, including selected specialization.
8. Repeated attempts and fault propagation do not duplicate frames.
9. A handled child failure does not make a successful caller fail.
10. A dispatched child's later failure stays outside its submitter's awaited chain.
11. A failed dispatch acceptance is attributed to the submission stage, not an unexecuted target Handle.
12. Restart preserves diagnostic relationships without claiming restored continuations.
13. Missing selected implementations after deployment produce an explicit recoverable operational state.
14. Resource scopes dispose connections, transactions, responses, and streams correctly.
15. HTTP resources acquire clients through the configured IHttpClientFactory, and per-call headers/context do not leak across concurrent invocations.
16. Awaited re-entry requiring an ancestor's exclusive target/key permit fails explicitly instead of hanging or weakening ordering.
17. Untargeted Dispatch(message) accepts zero subscribers and delivers independently to all registered subscriptions when several exist.
18. A subscribed target selects one specialization without suppressing deliveries to other subscribed targets.
19. Untargeted Dispatch never infers a concrete actor, returns a publication receipt, and does not await subscriber results.
20. Partial fan-out recovery preserves the accepted subscriber set and selected implementations without duplicate logical deliveries or missing recipients.
21. Subscriber execution failures are isolated from other subscribers and from the publisher's awaited chain.

Add sibling-chain tests if parallel Invoke already exists. Retain existing recovery/idempotency tests rather than substituting diagnostic-chain tests for them.

## 9. Deliberately separate follow-up work

Do not expand this handoff into a complete durable-workflow engine. Saved operation results, deterministic body replay, durable signal waits, checkpoint migrations, nested specializations, and automatic compensation require separate proposals.

Exact D# declaration grammar, resource-binding spelling, and the runtime error type should follow current language conventions. They must preserve the semantics above.

## References

These sources inform the design; the decisions above are D# requirements.

- [FCQRS workflows](https://onurgumus.github.io/FCQRS/concepts/workflows.html): inspiration for durable execution identity and recovery considerations. This handoff adopts a smaller diagnostic-chain requirement.
- [Microsoft: IHttpClientFactory with .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory): factory registration, client and handler lifetimes, and integration.
- [Microsoft: HttpClient guidelines](https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines): connection reuse and lifetime considerations.
- [Npgsql basic usage](https://www.npgsql.org/doc/basic-usage.html): an example of a shared data source with scoped pooled connections and transactions; this does not mandate PostgreSQL as the only provider.

