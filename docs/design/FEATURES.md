# Backend Language Feature Specification

**Version:** 0.2  
**Date:** 10 November 2026  
**Status:** Design specification; not a claim of implemented functionality.

A strongly typed language for backend services, API integrations and reliable background work on .NET. The design combines immutable data, explicit dependencies, isolated execution and typed composition with optional first-party packages for HTTP, WebSockets, queues and events.

This document consolidates the original feature baseline and subsequent decisions about middleware and application composition. Behaviour and package boundaries are specified here; exact grammar, keywords and syntax belong in a separate syntax document. Proposed defaults and unresolved choices are identified explicitly.

## 1. Language and type system

- Static, strong typing with immutable record-like data, value equality and immutable nested collections. Updates produce new values.
- No implementation inheritance in application code. Use composition, service interfaces and extension methods.
- Discriminated unions represent known alternatives. Pattern matching must be exhaustive; adding a variant exposes unhandled cases at compile time.
- Typed results represent success and failure. Optional values, generics and explicit error propagation are proposed supporting features; their exact rules remain open.
- Interfaces define contracts without inherited implementation or state. Multiple services may implement an interface; a service may implement multiple interfaces.
- Functions and service methods can be composed through typed inputs and outputs. Composition must account for asynchronous completion and typed failures.
- JSON serialization and typed decoding are available out of the box. Invalid input produces structured errors. Other formats can be provided through packages.
- The runtime uses .NET garbage collection. Deep immutability and isolation must be enforced at language boundaries, including interoperation with mutable .NET libraries.

## 2. Services and isolated execution

- The default service model is a fresh isolated invocation for each trigger, subject to an execution policy. Concurrent triggers may create concurrent instances. An invocation can await another service result without occupying a worker thread.
- Each invocation owns its state. Communication uses typed messages and references; another service cannot directly access that state.
- Ordinary handler failures are contained. Callers receive a failure or timeout. Dependency relationships do not automatically propagate crashes or establish supervision.
- Supervision controls restart and recovery. Retrying an operation is separate from restarting an execution instance, and neither undoes external side effects.
- Direct request/reply, durable submission and event publication remain distinct operations with different completion guarantees.
- Persistent actors remain optional and unresolved. Sequential queue processing alone does not preserve actor state. A proposed persistent-actor default processes one handler to completion, including awaits; call cycles require bounded waits and careful design.
- Isolation covers application errors within the host. Fatal CLR or native failures can terminate the entire OS process. BEAM-style per-actor garbage collection and execution preemption are not promised on .NET.

## 3. Dependencies and resource ownership

Dependencies are explicit and supplied through application composition. There is no global service locator or automatic handler discovery. Service contracts support substituting implementations and test doubles.

| Dependency | Ownership rule |
| --- | --- |
| Immutable values | Safe to share without mutation. |
| Service references | Grant access through a contract without exposing internal state. |
| Resource capabilities | Expose safe operations over runtime-managed infrastructure. |
| Connections and streams | Exclusively owned within a bounded operation scope. |

Database pools are shared infrastructure. Operations borrow connections and return them afterward; actors do not keep a permanent connection by default. A transaction owns one connection for its scope. Transaction handles cannot escape that scope or be sent to another actor.

Filesystem capabilities can restrict access to a directory and permitted operations. Simple operations manage handles automatically; streams are scoped and closed on completion or failure. File handles do not normally need pooling.

Private actor memory does not prevent races in shared external data. Coordinate conflicting file updates through an owning actor or storage-level mechanisms. Closing a failed write does not undo partial output; atomic replacement is a separate operation.

## 4. Configuration, secrets and startup

- Load configuration from JSON files and runtime environment variables into validated immutable types. Proposed precedence: defaults, base JSON, environment-specific JSON, then environment variables.
- Resolve secrets asynchronously from a provider using an independent bootstrap credential or host identity. Redact secret values in diagnostics. Initialize database pools and API clients from resolved configuration and secrets.
- **Eager initialization:** required resources become ready before dependent consumers or endpoints accept work.
- **Lazy initialization:** first use starts one shared initialization attempt; concurrent callers await it. Failure permits controlled retry.
- Startup validates registrations, initializes independent resources concurrently, then starts ready components. Shutdown and failed startup release acquired resources in dependency order.
- Registration declares dependencies; the host performs asynchronous initialization. Secret rotation requires an explicit resource refresh or replacement lifecycle, separate from immutable configuration.

The exact API for configuration-source bindings and custom startup resolution remains open. The earlier proposal to bind these through an application YAML file is not the selected direction. Configuration values may still come from external files and environment variables independently of code-based application registration.

## 5. Handler declarations and application composition

**Application wiring is defined in strongly typed code.** Each handler declares its trigger alongside its typed implementation. A central application explicitly registers the handlers it enables.

Trigger declarations are inspectable before handler execution. Registering a handler creates an invocation recipe; it does not create shared mutable handler state or execute its business logic.

| Handler kind | Declaration responsibilities |
| --- | --- |
| HTTP | Method, route, typed middleware pipeline and handler contract. |
| Queue | Stable queue identity, payload contract and default execution policies. |
| Event | Event contract, stable subscription identity and default delivery policies. |
| Scheduled job | Stable schedule identity, cron expression, timezone and missed-run policy. |

Handlers define default retry, ordering and concurrency policies where applicable. Application composition supplies dependencies, selects stores and enables handlers. Application overrides may constrain resource budgets but must not violate correctness requirements, such as required sequential processing.

The central API component is a router/host. It dispatches requests concurrently according to execution policies; central registration does not serialize all requests through a single sequential actor.

### Validation

- Check statically known pipeline connections, handler signatures and referenced contracts at compile time.
- Validate duplicate routes or identities, missing dependencies, incompatible registrations and unsupported store policies before accepting work.
- Clearly distinguish compile-time checks from startup checks that depend on runtime configuration.
- Installing a package must not start services or discover handlers automatically. Registration groups are permitted when explicitly invoked.
- Runtime location tracking in distributed deployments must not become automatic feature discovery.

### System visibility

Structured declarations should support tooling that produces route lists, queue and schedule inventories, and event/subscription maps. This preserves a readable overview of the application without maintaining a separate YAML source of truth.

Exact declaration syntax and compiler-generated versus library-defined registration remain open.

## 6. Typed pipelines, middleware and authentication

HTTP request handling uses composable typed steps. A step receives a known input and produces the input required by the next step. The final handler receives exactly the typed data and identity context it declares.

A typical pipeline authenticates a request, checks permission, decodes the body, and invokes the business handler. Resource-specific authorization may instead run after decoding an identifier and loading the relevant resource.

### Composition rules

- The compiler checks compatible inputs and outputs, including asynchronous and failure behaviour.
- Steps explicitly preserve or transform context. Decoding a body must not silently discard identity or authorization information needed downstream.
- A step can stop execution with a typed failure. The HTTP boundary maps authentication, authorization, decoding and application failures into responses.
- No untyped request-property bag, casting or global request context is required to connect steps.
- Composition supports ordinary functions and service methods. Service calls continue to respect isolation and resource ownership rules.
- Middleware must support wrapping downstream execution for tracing, logging and scoped resources, including cleanup on completion, failure or cancellation. Input-to-output transformations alone do not cover these cases.

### Authentication and authorization

- Authentication establishes a verified identity; authorization checks whether that identity may perform an operation.
- Handlers can require typed identity or authorization context. A pipeline must supply the required context before it can connect to the handler.
- Authorization evidence, where represented as a type, must have controlled construction and an appropriate operation/resource scope. Arbitrary application values must not masquerade as a successful authorization check.
- Type checking verifies the pipeline contract; credentials and access policies are still evaluated at runtime.
- The exact identity types, authorization-evidence representation and provider APIs remain open.
- Queue and scheduled entry points must establish their own identity/permission semantics. Persisting an authenticated HTTP context is not an implicit authorization policy for future work.

Typed composition is a general capability. HTTP middleware and authentication packages build on it; reusable composition may also serve queue and scheduled handlers.

## 7. Queues and execution policies

A first-party queue library provides Oban-inspired durable execution. Entries carry typed immutable payloads; each attempt normally runs in a fresh invocation.

| Policy | Intended behaviour |
| --- | --- |
| Concurrent | Multiple eligible messages may execute up to a configured limit. |
| Sequential | One message completes before the next begins for the queue or partition key. |
| Partitioned | Preserve sequential processing within a key while other keys progress. |
| Delayed | A message becomes eligible at or after a specified time. |
| Bounded | Pending capacity and active concurrency are limited; overload waits or rejects explicitly. |

- Sequential processing preserves order across retries: a delayed head message blocks its key until it completes or is explicitly skipped or abandoned. FIFO dispatch alone does not guarantee completion order.
- Concurrency scope is explicit: per host, across the deployment, or per key. Deployment-wide enforcement requires coordination through a capable shared store.
- Long handlers execute outside database claim transactions.
- Retry policies specify eligible failures, maximum attempts, backoff and jitter. Exhausted messages remain available for inspection and manual retry.
- A handler can complete, reject or reschedule a message. Rescheduling releases its execution slot rather than keeping an instance asleep.
- Durable processing is at least once. Support stable message identities and deduplication; handlers remain responsible for idempotent external effects.
- Provide inspection, pause, resume, cancellation and retry controls. Cancellation is cooperative where required and never implies rollback of completed effects.

## 8. Events and scheduled jobs

- An event can have zero, one or many subscriptions. Each matching subscription receives an independent queued delivery with its own outcome. Multiple workers on one subscription compete for its work; they are not separate subscribers.
- A subscription declares a trigger and handler. The runtime manages its backing queue using normal retry, ordering and concurrency policies. A separate queue reference or message mapping is not required.
- Optional filters and mappings should be pure and deterministic.
- Cron is another queue trigger. Persist schedules, timezone and delivery progress. Missed-run behaviour is explicit: skip, run once, or catch up.
- Delayed jobs and cron share the queue execution machinery. Durable fan-out must recover without silently omitting subscribers.

## 9. Runtime persistence

`RuntimeStore` is a provider contract for queues, schedules, retries and execution bookkeeping. It is logically separate from business data and exposes atomic runtime operations rather than generic get/save methods.

| Provider | Proposed deployment and isolation |
| --- | --- |
| SQLite default | One runtime file per application on persistent local storage; single-host starting point. |
| PostgreSQL | An application schema suitable for shared coordination across hosts. |
| MySQL | An application database with equivalent required runtime semantics. |
| Third-party stores | Implement the provider contract and declare supported capabilities. |

One SQLite file per application keeps related changes transactional. Separate files remain an advanced option for independent workloads. Schema or database naming does not replace access permissions.

Required persistence behaviour:

- Atomically enqueue, claim, renew ownership, complete, reschedule and move exhausted work to retained failure storage.
- Record schedule advancement with occurrence creation, and completion with follow-up enqueueing, atomically within the same store.
- Use recoverable claims with expiry and attempt tokens. Reject stale acknowledgements after ownership changes; recovery can still produce duplicate execution or external effects.
- Use stable queue, subscription and message-contract identities across deployments. Support payload versioning and migrations for stored work.
- Include store migrations, retention and recovery procedures in the provider/runtime lifecycle.
- Reject unsupported guarantees rather than silently weakening them.
- Use an explicit outbox or equivalent coordination for reliable publication of business changes when business data resides in a separate database.

## 10. HTTP and WebSockets

HTTP and WebSockets are optional first-party packages. Applications can host APIs without queues, run workers without HTTP, or combine both.

HTTP supports explicit handler registration, handler-defined methods and routes, typed request/response contracts, JSON encoding/decoding, typed middleware composition and isolated request execution.

WebSockets support an explicit connection lifecycle and typed messages. Runtime-managed connections are long-lived resources; message handlers can use isolated invocations. Outbound writes require bounded buffering and defined ordering.

Durable notifications do not preserve live sockets across restart. Reconnection and missed-message recovery require an explicit protocol.

**gRPC is deferred.** It is not part of the current implementation scope.

## 11. .NET and NuGet interoperability

The intended platform is .NET, with access to compatible NuGet libraries written in C# and F#. Compatibility is a design goal requiring explicit compiler and build support; not every package is automatically supported merely because it targets .NET.

The interoperation design must address:

- CLR types, constructors, methods, properties, interfaces, generics and delegates.
- Async interoperability, including tasks, cancellation and deterministic disposal of resources.
- Mapping foreign nulls and exceptions into the language's optional-value and error models.
- Ownership of mutable foreign objects and conversion or wrapping at isolated service boundaries.
- F#-specific values and APIs, including options, unions, function values and asynchronous computations.
- Packages that depend on generated source, source generators or other build integration.
- Runtime/framework compatibility, native dependencies and any future trimming or AOT restrictions.

Foreign libraries must not silently invalidate immutability or isolation guarantees. The exact safe boundary, permitted escape hatches and adapter requirements remain unresolved. No blanket compatibility guarantee is made in this version.

## 12. Package boundaries

Capabilities are distributed as composable first-party NuGet packages. Names are provisional.

| Package area | Responsibility |
| --- | --- |
| Runtime and Hosting | Execution, supervision, composition, registration, startup and shutdown. |
| HTTP and WebSockets | Optional inbound transports, typed pipelines and connection handling. |
| Authentication and Authorization | Identity providers, access checks and typed pipeline integration; exact split remains open. |
| Messaging and Queues | Typed messages and durable execution policies. |
| Events and Scheduling | Event subscriptions and cron triggers backed by queues. |
| Storage abstractions and providers | RuntimeStore plus SQLite, PostgreSQL and MySQL adapters. |
| Configuration and Secrets | Typed settings and asynchronous secret providers. |

Application declarations describe runtime wiring. Cloud infrastructure provisioning is a separate possible capability; an application queue declaration does not inherently create a cloud-provider queue.

## 13. Open decisions and deferred backend areas

### Language and runtime decisions

- Language name, grammar and syntax; exact result, optional-value and propagation rules.
- Typed composition mechanics, context preservation, wrapping middleware and authorization-evidence types.
- Compiler-generated versus library-defined registration and the limits of static metadata.
- Persistent actors, restart-state restoration, lifetime scopes and coexistence with fresh invocations.
- Actor/runtime framework selection. Generating C# over .NET, potentially using Akka.NET, remains a prototype direction rather than a committed dependency.
- Precise distributed ordering, lease recovery, cancellation, cron catch-up and secret-rotation protocols.
- Serialization evolution, additional formats and the supported .NET interop surface.
- Configuration-source binding APIs and custom startup hooks.

### Backend areas identified but not yet designed

- Outbound HTTP clients: connection reuse, deadlines, cancellation, safe retries and circuit breakers.
- Observability: structured logs, metrics, tracing and correlation across requests and queued work.
- Business-data access: drivers/ORM integration, transaction APIs, migrations and outbox ergonomics.
- General streaming: async streams, bounded buffering and resource lifetime across transports.
- Operations: readiness/liveness checks, graceful draining, shutdown deadlines and deployment models.
- Testing: handler test harnesses, replaceable dependencies, controllable time and integration hosting.

These are follow-up design topics, not finalized language guarantees. gRPC remains explicitly deferred.

## 14. Suggested implementation sequence

1. Typed immutable messages, explicit code-based registration and SQLite-backed enqueue, execute, retry and crash recovery.
2. Cron and event fan-out using the same durable execution machinery.
3. HTTP handlers and typed middleware composition, including authentication integration.
4. WebSockets and additional RuntimeStore providers.

Define the minimum .NET interoperability boundary early enough to support these stages. Validate the runtime with a controllable clock and provider conformance tests, including retry recovery, stale claims and supported atomic operations.

## 15. Revision notes

Version 0.2 preserves the original runtime and persistence baseline and incorporates the subsequent design discussion:

- Code-based application composition is the selected direction; application YAML is no longer the proposed source of truth.
- HTTP handlers declare routes and methods alongside their implementation and are explicitly registered with a central router/host.
- Typed middleware composition supports authentication, authorization, decoding and wrapping downstream execution.
- C# and F# NuGet interoperability is an explicit design goal with unresolved boundary rules.
- gRPC is deferred, and the remaining backend gaps are recorded as follow-up topics.

