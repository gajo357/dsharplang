# DEC-07: Resource, configuration, and startup lifecycle

**Status:** Draft proposal for review; not adopted or implemented.

**Issue:** [#17](https://github.com/gajo357/dsharplang/issues/17).

**Base:** `5e0c1d366a9f434501ec3c5dbe32cbe65def90b3`.

## Work plan and boundaries

Reconcile configuration, secrets, resource ownership, startup, shutdown and
host-scoped logging. Propose concrete lifecycle and failure behavior with D#
illustrations and an acceptance-test matrix. The only changed file is this
proposal. It introduces no compiler, runtime, provider, or dependency behavior.

This document is **not adopted**. Merging it or passing documentation checks
neither adopts it nor implements it. Explicit adoption of a reviewed version is
required before dependent implementation. Names, signatures and syntax below are
illustrative proposals, not working D# APIs. This proposal does not adopt or rely
on provisional worker, queue, HTTP or interop protocols in
[DEC-03](DEC-03.md), [DEC-04](DEC-04.md), [DEC-05](DEC-05.md), or
[DEC-06](DEC-06.md). Database drivers, secret providers, public configuration
syntax and compiler enforcement of lifetimes remain separately scoped decisions.

Existing architecture requires explicit code-based composition and registration;
metadata inspection does not acquire resources, run business logic or enable
handlers. Configuration becomes validated immutable values. The architecture
proposes defaults, base JSON, environment-specific JSON, then environment
variables in increasing precedence. Pools are shared infrastructure, borrowed
connections and streams belong to bounded scopes, and transaction handles do
not escape or cross agent boundaries. See the
[architecture](../../ARCHITECTURE.md#composition-and-lifecycle),
[language boundaries](../../LANGUAGE.md#resource-and-interop-boundaries), and
[syntax reference](../DSharp_Syntax_Reference.md#11-asynchronous-operations-and-resources).
Resource disposal does not undo arbitrary side effects. No application YAML,
service locator, persistent actor model, new serialization contract or general
cross-database transaction is selected here.

## Proposed configuration and secret resolution

An explicitly registered typed binding defines field names, approved source keys,
required/optional status, conversions and domain validation. As a proposal, the
four source layers are applied in this order, with later *present* values
winning: defaults, base JSON, environment-specific JSON, environment variables.
The environment name and permitted source locations are explicit host inputs;
metadata inspection never reads or resolves them. A missing value leaves the lower
layer intact; an invalid present value is an error, not a reason to fall back.
An unknown key, ambiguous mapping, duplicate key within one source or duplicate
binding fails validation, with its source identified and values redacted. For
object-shaped settings, merge declared fields recursively; replace an explicitly
supplied array as a whole. A missing required field or an invalid or out-of-range
conversion fails before any consumer accepts work. Conversions use explicitly
selected invariant formats, not machine culture. An absent optional source is
skipped; an absent required source, or any unreadable, malformed or access-denied
source, fails startup. Optional absence does not excuse access errors.

Configuration layers provide raw source values; a registered decoder performs
explicit typed conversion, validation and immutable snapshot construction. No
implicit JSON representation is assigned to D# `Option<T>`, nullable values, or
nested options by this proposal. Such fields require a separately selected
codec that preserves their meaning, or binding must reject them. A candidate
snapshot is published only after the *whole* binding and cross-field validation
succeeds. Consumers see one immutable, host-owned snapshot, not a mutable source
view. Diagnostics may identify a field, source, failure category and safe
correlation identifier; they never include secret values, credentials, tokens,
connection strings or source contents by default. Redaction applies to exception
messages, nested validation errors and logging, including failed startup.
Sensitive status is part of a binding's schema; do not assume a field is safe
solely because its name does not contain `secret`.

Proposed explicit, asynchronous provider boundary, in conceptual signatures:

```text
SecretRef       // opaque configured reference, not the secret value
SecretValue     // host-controlled sensitive value; not ordinary loggable data
SecretError     // typed unavailable, denied, missing, invalid or cancelled cases
SecretProvider.resolve : (SecretRef, Cancellation) -> Task<Result<SecretValue, SecretError>>
```

A host explicitly registers a provider recipe and obtains its bootstrap credential
from host identity or an independently configured bootstrap mechanism. Secret
resolution cannot depend on a resource that itself requires that secret. The
provider is started only after its own prerequisites succeed, and provider
unavailability or denied/missing/invalid secrets fail the dependent recipe closed;
no empty string, stale secret or implicit permission is substituted as a successful
resolution. Retaining an already healthy generation after failed replacement is
governed separately below and does not override expiry or revocation. If a
provider error is cancellable, cancellation remains distinct from denial and
invalid input. Provider/adapter code is trusted, not sandboxed. Access to the
resolved secret is limited to the explicitly configured consumer/recipe; never
place it in Logger fields, URLs, public diagnostics, message data or long-lived
settings snapshots merely for convenience. A typed settings snapshot may retain a
`SecretRef` rather than the resolved value. Exact secure storage, memory erasure,
provider-specific credentials and external secret format are not specified here.

A startup attempt owns its provider calls. Cancellation signals them and waits
for them to stop before releasing resources they use; cancellation alone is not
proof a call stopped. Credentials and provider state must not leak between hosts.
Explicit refresh is described below; resolving a reference does not silently
subscribe to a provider's changes or mutate an existing settings object.

## Proposed dependency graph and startup

The host builds an explicit graph of registered recipes and their declared typed
dependencies, including configuration, secret providers, resources, outputs,
services and consumers. Validate duplicate/missing bindings and cycles before
acquiring resources or accepting traffic; include a safe dependency path in a
cycle diagnostic. A cycle cannot be hidden by marking a resource lazy. Factories
must declare dependencies; reject reentrant/dynamic resolution that would bypass
the graph. Importing a package, inspecting a declaration or enumerating routes
is not registration, initialization or a reason to acquire a connection.

A host transitions `Created -> Starting -> Ready -> Stopping -> Stopped`, with a
failed startup ending in `Failed` after cleanup; these are conceptual states, not
public syntax. Validate the graph first, then run ready configuration, provider,
secret-resolution and eager-resource nodes in topological order: a provider must
start after its prerequisites and before its dependent secret resolutions.
Independent graph nodes may
initialize concurrently, but a dependent cannot start until its dependencies are
ready. Do not accept work until every required eager dependency and output is
ready and startup has completed. A recipe acquires resources only when explicitly
started; a lazy recipe starts upon first permitted use after the host is ready.

On startup failure or cancellation, stop launching dependents, signal and await
all in-flight startup work, record every resource whose acquisition completed,
and release each successfully acquired resource exactly once in reverse
dependency order. An initialization that acquires a partial resource but fails
before returning it must release that partial acquisition inside its own factory.
Do not dispose a dependency while an initializing consumer still uses it. Report
the primary startup failure and separately report cleanup failures without secret
contents. A failed start does not become `Ready`; restart is a new explicit host
start, with a fresh graph and no leaked resources or background work from the
old attempt. A hung foreign initializer cannot be made safe by claiming it stopped:
shutdown escalation and the deadline rules below apply.

## Proposed lazy initialization

Each host has independent lazy-resource state: `NotStarted`, `Initializing`,
`Ready`, or `Failed`. The first caller starts one attempt; concurrent callers
join the same attempt and observe its result. A caller's cancellation stops that
caller waiting but does not cancel the shared attempt or other waiters. The host
owns the attempt and may cancel it when stopping. Publish a resource only after
complete successful initialization; failed partial acquisition is cleaned before
failure is published. No caller sees a partially initialized resource.

On failure, the result is retained until an explicitly configured retry policy
permits a later attempt. The policy declares retryable error categories, delay,
maximum attempts and a clock; there is no implicit immediate retry loop. A
single subsequent caller atomically starts the next permitted attempt and other
callers join it. Cancellation, denial and invalid configuration are not silently
classified as transient. A stopped host never starts a new attempt. Completion
racing shutdown cannot publish a new usable resource: the host waits for that
attempt and releases any successfully acquired result. These guarantees apply
per host and per resource generation, not globally across deployments.

## Proposed scoped operations and resources

A resource recipe is a host-owned capability with an explicit dependency set,
concurrency policy, acquisition and asynchronous release. A scope owns each
borrowed connection, stream and transaction until its work and cleanup finish.
Use-after-scope and escape via fields, returned values, closures, tasks, callbacks,
foreign objects or agent messages are rejected or guarded at the boundary; exact
compiler/runtime enforcement remains implementation work. Cleanup runs exactly
once on normal completion, early return, typed failure, exception and cooperative
cancellation. Await asynchronous disposal. A cleanup fault is reported without
masking an earlier primary failure; if there is no earlier failure it is surfaced.
Cleanup is not skipped just because the operation's cancellation token is set.
Do not promise completion or disposal while foreign code remains in flight.

For databases, the host owns a shared pool and each operation borrows a
connection. A transaction exclusively owns one borrowed connection for its
scope. Under this proposal, an explicit `commit` requests commit. On scope exit
without a successful commit, including cancellation or typed failure, attempt
rollback when the driver knows the transaction is still active and uncommitted.
Failure before commit is not silently committed. A commit error or lost
connection can leave the outcome **unknown**: report it distinctly and do not
claim rollback or automatically replay the transaction; dispose or quarantine
the connection according to the driver contract. Rollback or disposal can
also fail and must be reported. A known successful commit is not undone by later
cleanup failure. Only effects covered by the selected database transaction are
atomic; filesystem, HTTP, another database and external messages are not rolled
back by this scope. Driver-specific isolation, savepoints, idempotency and outbox
coordination require separate contracts.

A filesystem capability explicitly identifies an allowed root and operations;
requesting a capability does not grant arbitrary filesystem access. Resolution
must prevent traversal and symlink or rename races at the actual operation, using
platform-appropriate handle-relative or equivalent checks rather than only a
string-prefix check. Reject when that guarantee cannot be provided. Open streams
are bounded scoped resources, closed on every exit. Ordinary writes may be partial
on failure or cancellation; atomic replacement is a separate explicit operation
with a documented same-filesystem and durability contract. No implicit rollback,
symlink-following permission, or cross-process synchronization is promised.

## Proposed refresh, replacement and shutdown

Immutable configuration snapshots never change under consumers. A refresh is an
explicit host action producing a separately validated candidate snapshot and
resolving the necessary new secrets. A dependent resource changes only through an
explicit refresh/replacement recipe; rotating a secret or editing a file does not
mutate an existing client. Candidate initialization must finish successfully
before atomically publishing a new generation to *new* acquisitions. Existing
operations keep their old generation and its dependencies until their leases
complete; then the old generation is released exactly once in reverse dependency
order. Failed validation or replacement leaves the prior healthy generation
available, cleans partial new resources and reports the failure without secrets.
Concurrent replacement requests are serialized per resource; host shutdown bars
new replacement and waits for in-flight attempts or escalates as described below.
A revoked credential may make the old resource unusable: retaining its object is
not a guarantee of continued successful service. Multi-resource atomic swaps,
provider notification semantics and credential revocation policy require explicit
future contracts; a generation never mixes partially constructed dependencies.
A refresh does not reconfigure the host's fixed Logger output recipes; a change
to those recipes requires a separately created host.

Shutdown has an explicitly configured host deadline and a monotonic clock. First
stop admission and producers, then request cancellation and await in-flight work
and scopes. Stop Logger admission after producers have drained, then drain Logger
for at most the lesser of five seconds and the host's remaining shutdown budget.
Drain/close its outputs, and only then
close any shared RuntimeStore and other dependencies in reverse dependency order.
Release other resources after their dependents and outstanding generation leases
have completed. Cleanup failures are collected and reported safely while other
eligible cleanup continues. An elapsed deadline is reported as incomplete shutdown;
escalation may terminate the process according to host policy. It does **not**
assert that uncooperative work stopped, that cleanup ran, that pending log events
were delivered, or that external effects rolled back. Do not dispose a resource
still in active use merely to claim graceful shutdown. Exact timeout values,
process escalation and deployment-level draining remain host policy.

The settled [logging target](../DSharp_Pipes_and_Logging.md) requires one
host-scoped Logger, non-waiting transient admission and independently bounded
outputs. No logging configuration means Info and one ConsoleLogOutput; no database,
exporter or retention job is created. An explicit output list replaces that
default, including `[]` disabling delivery. Resolve configured output recipes
before traffic; they remain fixed for that host lifetime. Bootstrap console
events during initialization are not replayed into later outputs. Output failures and internal diagnostic
reporting must not recurse through Logger or change application transaction or
response outcomes. Logger is not a durable audit mechanism, and host settings,
scopes, mailboxes and output resources remain isolated across hosts.

## Illustrative D# composition

The following is a target sketch only. Provider, decoder, resource and lifecycle
APIs are proposals, not implemented syntax. An application supplies explicit
recipes; the host owns startup and cleanup rather than opening a connection while
reading declarations.

```text
app Billing {
    let settings = Config.bind<BillingSettings>(sources)
    let secrets = SecretProvider.fromHostIdentity()
    let database = SqlDatabase.fromConfig(settings.database, secrets)
    bind InvoiceStore = SqlInvoiceStore(database)

    agents.register(SendInvoice)
}

service SqlInvoiceStore(database: SqlDatabase) {
    async save(invoice: Invoice): Result<Unit, StoreError> {
        using connection = await database.borrow() {
            using transaction = await connection.beginTransaction() {
                do await transaction.save(invoice)?
                do await transaction.commit()?
                Unit
            }
        }
    }
}
```

The host validates configuration, resolves required secrets and starts required
dependencies before accepting work. An explicit commit covers only the selected
database transaction; a failed or cancelled operation is not proof that an
external effect was undone. No example asserts that this syntax is currently
accepted by the compiler.

## Acceptance evidence required after adoption

Implementation requires appropriately scoped setup and executable D# tests,
controlled clocks/barriers and real selected providers where claims depend on
external behavior. This list is a test plan, not evidence that a runtime exists.

1. **AC1 — typed settings and secrets:** exercise all four precedence layers,
   per-field object merging and array replacement, missing/duplicate/unknown
   keys, optional source absence versus unreadability, invalid highest-priority
   input, culture independence, cross-field validation and atomic snapshot
   publication. Test unavailable, denied, missing, invalid and cancelled secret
   resolution, independent bootstrap credentials, concurrent host isolation,
   dependency cycles and redaction in all failures and logs. Nullable/Option
   values need an explicitly approved encoding or are rejected.
2. **AC2 — startup and lazy resources:** reject missing dependencies and cycles
   before side effects; use barriers to prove independent starts may run in
   parallel while dependencies and readiness ordering hold, including provider
   startup before its dependent secret resolutions. Race many lazy
   callers, cancelled waiters, failure/retry timing and shutdown against a
   completing initializer. Verify one attempt at a time, no partial publication,
   host isolation, and reverse-order exactly-once cleanup for all successful and
   partially acquired resources, with primary and cleanup failures retained.
3. **AC3 — scope, database, filesystem:** execute D# scope exits for success,
   early return, typed failure, fault and cancellation; reject escapes through
   messages, closures and asynchronous/foreign boundaries. Test pool borrowing,
   exclusive transaction connection, explicit commit, rollback only when the
   driver knows the transaction is active, commit-unknown and connection quarantine,
   rollback/disposal failure and effects outside the transaction with a real
   selected database. Test traversal, symlink/rename races, scoped stream cleanup,
   partial writes and separately specified atomic replacement on supported filesystems.
4. **AC4 — refresh and shutdown:** hold an old-generation lease while replacing,
   race replacements and shutdown, fail new validation/secret/resource creation,
   verify old-generation behavior and delayed exactly-once release. Use a fake
   clock and blocked producer/output/foreign operation to prove bounded deadline
   reporting and absence of false cleanup/delivery claims. Check fixed host-scoped
   output recipes, console default, explicit empty outputs, no bootstrap replay,
   producer -> Logger -> outputs -> shared RuntimeStore ordering and isolation
   between two hosts.

## Review and adoption gate

Review source precedence and decoder syntax, sensitive-value handling, provider
and bootstrap identity, graph and lazy retry rules, database/filesystem adapters,
generation replacement, timeout escalation and logging startup/shutdown order.
Exact public APIs, provider choices, wire encodings and timeout values remain open.
Even after adoption, [#45](https://github.com/gajo357/dsharplang/issues/45),
[#58](https://github.com/gajo357/dsharplang/issues/58),
[#74](https://github.com/gajo357/dsharplang/issues/74),
[#75](https://github.com/gajo357/dsharplang/issues/75),
[#76](https://github.com/gajo357/dsharplang/issues/76) and
[#77](https://github.com/gajo357/dsharplang/issues/77) remain subject to their
other prerequisites and separately scoped implementations.

## Verification of this proposal

Bootstrap, documentation and link checks, and compiler verification passed
(18 baseline and 362 feature tests). All 23 automation tests passed. The local
isolated verifier could not download its pinned Docker image (`Forbidden`);
GitHub Actions runs the unchanged verifier. These checks validate the repository,
not resource lifecycle behavior or adoption.
