# DSharp

A strongly typed, actor-first language for APIs, events, and background jobs on
.NET.

**Status:** language and runtime design. This repository contains documentation,
not a compiler, runtime, or installable toolchain. DSharp is the working project
name; final language naming remains open.

## Why this language exists

Backend applications combine request handling, background work, external APIs,
storage, authentication, and recovery. The difficult questions often sit between
those parts: who owns state, when work is durable, how failures reach callers,
and what happens when a process stops halfway through an operation.

DSharp aims to make those boundaries explicit. Immutable typed data describes
what crosses them. Isolated service invocations own their state. Typed pipelines
connect operations, while application code declares dependencies and enables
handlers. Durable execution provides retries and recovery with explicit limits.

The purpose is to make backend behavior easier to understand and check before
deployment while retaining access to the .NET ecosystem. Types do not eliminate
runtime failures, and retries do not undo external effects.

## Inspirations

- **Actor-style isolation:** services communicate through typed messages and
  references instead of accessing one another's private state. The default is a
  fresh invocation per trigger; persistent actors remain an open design.
- **Immutable data and typed functional composition:** records, unions,
  exhaustive matching, and typed results make data flow and alternatives
  explicit. Composition and interfaces replace implementation inheritance.
- **Oban-inspired durable jobs:** queues provide bounded concurrency, retries,
  scheduling, and retained failures. Events and cron reuse this machinery.
- **The .NET ecosystem:** managed execution and garbage collection provide the
  platform foundation. Compatible C# and F# NuGet libraries are an interop goal,
  requiring explicit compiler/build support and safe boundaries.

The design does not promise BEAM-style per-actor garbage collection or execution
preemption on .NET. Generating C# and using Akka.NET are prototype possibilities,
not committed implementation dependencies.

## Purpose and scope

DSharp targets backend APIs, integrations, event consumers, and reliable
background processing. An application may run HTTP endpoints, workers, or both.
Optional first-party packages provide HTTP, WebSockets, queues, events,
scheduling, configuration, secrets, and runtime storage.

Application wiring is strongly typed code. Handlers declare triggers and the
application explicitly registers the handlers it enables. Installing a package
does not discover handlers or start services. Configuration values may still
come from JSON and environment variables; YAML is not the selected application
wiring model. Infrastructure provisioning is a separate possible capability.

## A request and its follow-up work

This is a conceptual flow, **not DSharp syntax**:

```text
application explicitly registers CreateOrder and SendReceipt

POST /orders
  -> authenticate request
  -> check permission
  -> decode body while preserving identity context
  -> invoke CreateOrder with typed input and explicit dependencies
  -> return a typed response or map a typed failure to HTTP

durable receipt submission
  -> store a typed immutable payload
  -> run SendReceipt in a fresh invocation when eligible
  -> complete, retry, reject, or reschedule
```

Request/reply waits for an invocation outcome. Durable submission records work
for later execution. Event publication fans out to independent subscription
deliveries. These operations have different completion guarantees. Durable work
is at least once: handlers must make external effects idempotent. Coordinating a
business change and publication across separate databases requires an explicit
outbox or equivalent mechanism.

## Documentation

| Document | Contents |
| --- | --- |
| [Architecture and implementation](docs/ARCHITECTURE.md) | Components, interactions, ownership, persistence, and implementation stages. |
| [Type system and syntax](docs/LANGUAGE.md) | Established type semantics, syntax boundaries, and unresolved rules. |
| [Feature specification v0.2](docs/design/FEATURES.md) | Preserved source specification dated 10 November 2026. |
| [Documentation tasks](docs/TASKS.md) | Task breakdown, review evidence, and follow-up decisions. |
| [AI development workflows](docs/AI_WORKFLOWS.md) | Approved issue intake, draft PRs, independent verification/review, and bounded repairs. |

The feature specification is the baseline. Provisional choices and open decisions
are labeled; examples do not finalize grammar.

## Implementation status

There are no compiler or runtime build, run, or test commands yet. Documentation
and automation checks are described in [AI development workflows](docs/AI_WORKFLOWS.md).
The proposed first milestone is
typed immutable messages, explicit registration, and SQLite-backed enqueue,
execution, retries, and crash recovery. Events and cron follow, then HTTP and
typed middleware, then WebSockets and additional storage providers.

Grammar, compiler architecture, safe .NET interop, and some runtime protocols
still need design work. gRPC is deferred. Read the detailed design before
building against a proposed capability or contributing a design change.

## License

See [LICENSE](LICENSE).
