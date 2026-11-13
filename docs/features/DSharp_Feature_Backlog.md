# D# feature implementation backlog

Prepared 11 November 2026 for [gajo357/dsharplang](https://github.com/gajo357/dsharplang). This is a historical planning snapshot; its issue counts, bootstrap status, task order and permissions may be stale. Consult current implementation status, issue state, repository instructions and implementation sequence before use. The nullable/Option and [pipes/logging](DSharp_Pipes_and_Logging.md) requirements below incorporate later target contracts.

**Original snapshot: 94 issue briefs:** 1 existing issue, 38 compiler candidates, 9 design tasks, 3 environment/infrastructure templates, and 43 later runtime/library/tooling briefs. Later additions are listed separately below. This is a planning catalog, not a list of approved implementation jobs. Several later briefs explicitly require smaller child issues before dispatch.

No GitHub issues were created by producing this document. IDs such as LANG-02 are backlog identifiers, not GitHub issue numbers. Replace dependency IDs with real issue/PR links as issues are created.

### 13 November 2026 reconciliation: pipes and logging

The [pipes, partial application and logging specification](DSharp_Pipes_and_Logging.md)
adds LANG-41, LIB-04 and LOG-01–04 and revises HTTP-03/#83 and OPS-01/#91.
The earlier statement about issue creation describes only the original snapshot.
The new contracts are implementation targets, not evidence that the features run.
LANG-18/#35 remains the completed plain-pipe foundation; partial application is
tracked separately. Existing nullable-generic #30 and Option/LANG-40/#121 retain
their own completion criteria.

Calls gain positional-prefix partial application; result-aware composition uses
ordinary library functions. HTTP preprocessing uses `pipeline = callable`, not a
`pipeline {}` block. Logging uses host-scoped `Logger.log`, with a bounded
transient actor and console-only default. Database logging is opt-in and uses the
existing RuntimeStore; it does not make log admission durable. This reconciliation
does not enable runtime projects, start handlers or change the compiler.

| Backlog ID | GitHub tracking | First bounded slice |
| --- | --- | --- |
| LANG-41 | [#129](https://github.com/gajo357/dsharplang/issues/129) | [#135](https://github.com/gajo357/dsharplang/issues/135): ordinary functions and pipes |
| LIB-04 | [#130](https://github.com/gajo357/dsharplang/issues/130) | [#136](https://github.com/gajo357/dsharplang/issues/136): synchronous Result helpers |
| LOG-01 | [#131](https://github.com/gajo357/dsharplang/issues/131) | LogEvent, templates and scope capture |
| LOG-02 | [#132](https://github.com/gajo357/dsharplang/issues/132) | Actor, console and output lifecycle |
| LOG-03 | [#133](https://github.com/gajo357/dsharplang/issues/133) | Shared-store database output and retention |
| LOG-04 | [#134](https://github.com/gajo357/dsharplang/issues/134) | LogReader and observer search |

Core #135 depends on merged #30/#33/#34/#35/#36, and sync #136 depends on #31
and #135. Existing tests intentionally reject underapplied calls (including
`1 |> add(2)`). Before dispatching #135, scope a narrowly reviewed migration of
those obsolete expectations; this documentation task changes no tests. Do not
weaken unrelated tests or treat this as blanket permission to edit verification.

## What is already present

Main was inspected at tree `23fd22c65af7410ce87536b829232139462ef774`. The repository has the pinned .NET 10 toolchain, a compiler that generates C#, parameterless int-literal functions, single-line comments, source diagnostics, CLI check/compile and execution tests. These are completed foundations, not new backlog tasks.

[Issue #7 — immutable local let bindings](https://github.com/gajo357/dsharplang/issues/7) is open and labelled `ai:approved`. Reuse it as LANG-01. Do not mark it implemented until its PR is integrated and its checks pass. Its existing approved issue body is authoritative for that run.

The current workflow allows compiler/CLI C# files, new feature tests, examples, bounded implementation notes and implementation status. It does **not** yet allow runtime projects, dependency files or verifier changes. SET tasks belong in the normal repository environment; later runtime issues depend on explicit infrastructure enablement.

The older `DSharp_Implementation_Instructions.md` still contains historical documentation-only/bootstrap guidance. Current `AGENTS.md`, policy, implemented bootstrap and status take precedence. SET-01 reconciles those documents.

## How to use this backlog

1. Keep this as the overall roadmap. Create only a small batch of concrete issues, beginning with the sequence below.
2. A feature issue should deliver one observable behavior through parsing, type checking and generated execution. Avoid separate giant “write the parser” or “implement actors” tickets.
3. Dependencies must be integrated into the branch used by the coordinator. An open dependency PR is insufficient.
4. Resolve the relevant design gate, freeze exact examples/API/state transitions, and fill exact allowed paths/verification before marking an issue approved. “Proposed” syntax is not automatically a frozen language contract.
5. Each implementation issue uses the six required sections below. Keep AC identifiers on individual lines. A contract decision issue produces a proposal; adoption of a new contract is a separate explicit maintainer decision.
6. For a frozen, bounded issue: use the approved issue form, add `ai:approved`, then run **Actions → AI coordinate → Run workflow** on `main` with its issue number. Let the coordinator manage implementation, verification, repair and review.
7. Run one issue at a time under the current serialized workflow. Check exact-candidate test evidence, review the PR, integrate it, then advance.
8. Do not edit a frozen issue to steer an active run. Follow the repository's restart/reconciliation process instead.

The coordinator has a 60-minute overall deadline and a 20-minute agent execution budget. A task also has a 20-file / 128-KiB patch boundary. Size for one focused change with verification. If a brief is too large, create children with their own complete contracts; do not partially implement a safety feature and expose an unsafe public surface.

## Recommended first batch

| Order | Backlog ID | Concrete outcome |
| --- | --- | --- |
| 1 | LANG-01 / existing #7 | Immutable locals; generated program returns 42. |
| 2 | LANG-02 | Integer expressions with precedence, parentheses and checked arithmetic. |
| 3 | LANG-03 | Typed parameters and same-module calls. |
| 4 | LANG-04 | Booleans, comparisons and short-circuit logic. |
| 5 | LANG-05 | Typed if/else and nested local scopes. |

These are issue candidates, not already approved semantics. LANG-02 and LANG-03 are independent after #7; the workflow should still run them sequentially. SET-01 can be done separately in the repository environment. Broad runtime decisions do not need to block this compiler batch.

Afterward, build immutable data, matching, nullability and Result; then lambdas/interfaces and worker contracts. Deliver one callable-worker example before durable queue execution. Build SQLite queue execution/recovery, followed by events/cron and HTTP composition. Numeric extensions, extra providers, WebSockets and editor tooling can follow their own dependency paths.

## Source-of-truth map

- [Current implementation status](https://github.com/gajo357/dsharplang/blob/main/docs/features/DSharp_Implementation_Status.md): tested implementation.
- [Syntax reference](https://github.com/gajo357/dsharplang/blob/main/docs/features/DSharp_Syntax_Reference.md): established choices plus explicitly provisional grammar/APIs.
- [Pipes and logging target](DSharp_Pipes_and_Logging.md): partial application, Result helpers, HTTP callable preprocessing and the Logger/output/query contracts.
- [Feature specification](https://github.com/gajo357/dsharplang/blob/main/docs/design/FEATURES.md): backend behavior and open design topics.
- [Repository agent instructions](https://github.com/gajo357/dsharplang/blob/main/AGENTS.md), [automation policy](https://github.com/gajo357/dsharplang/blob/main/.github/ai/policy.json), [workflow instructions](https://github.com/gajo357/dsharplang/blob/main/docs/AI_WORKFLOWS.md): actual execution permissions and checks.

This document proposes order and issue scope. It does not override those sources, finalize all draft APIs or authorize workflow changes.

## Non-negotiable design constraints

- Immutable data and nested collections; local mutable builders never escape their creating invocation.
- Public-only declarations; no class/service implementation inheritance.
- Native `T?` and `null`, separate from explicit `Option<T>` with `None` and `Some(T)`. Nullable application is idempotent. Track nullable generics in [#30](https://github.com/gajo357/dsharplang/issues/30) and Option in [#121](https://github.com/gajo357/dsharplang/issues/121).
- Every normal application handler path returns `Result`; queue success carries Unit under the current draft. Logger's typed Unit-returning submission API and internal system-actor outcomes are the explicit runtime-facility exception.
- Only workers expose awaited handler results. Queue acceptance is different from completion; direct `.handle` access is forbidden.
- Queue/event/scheduled payload roots are declared data types or Unit, with recursively immutable serializable fields.
- Individual interface methods may have default bodies; this does not select a default service implementation.
- First-class lambdas obey data/capture/lifetime restrictions.
- Code-based explicit registration; no automatic discovery or application YAML. The standard Logger is registered once by each host; arbitrary packages do not gain handler auto-activation.
- At-least-once durable execution; recovery/retries do not undo external side effects.
- gRPC stays out of this implementation plan.

## Coverage index

| Source area | Backlog coverage |
| --- | --- |
| Lexical syntax, primitives, comments | DEC-01, DEC-02; LANG-02/04/06/07/31/32/33/38 |
| Bindings, records, unions, nullable, Result | LANG-01, LANG-05, LANG-08–15 |
| Lambdas, generic functions, pipes, tuples | LANG-13, LANG-16–19, LANG-25, LANG-41; LIB-04 |
| Immutable collections and local builders | LANG-20–23 |
| Async, scoped resources, services/default methods | LANG-24–28; DEC-07; APP-04 |
| Modules and extensions | LANG-29/30; DEC-02 |
| Agent discovery/contracts, policies, application wiring | LANG-34–37; DEC-03–05; TOOL-01–05 |
| Worker execution, supervision and isolation | RUN-01–03 |
| Queue durability, retries, ordering, controls | DEC-04; RUN-04–13, RUN-16 |
| Events and cron | RUN-14/15; DEC-08 scheduling section |
| Runtime store providers | RUN-04/05/17/18 |
| Configuration, secrets and startup | DEC-07; APP-01–04; RUN-03; OPS-03 |
| JSON, immutable standard values, outbound HTTP | LIB-01–03 |
| HTTP, middleware, authentication, WebSockets | DEC-05/08; HTTP-01–06 |
| C#/F# NuGet interoperability | DEC-06; INT-01–04 |
| Observability, observer, testing, outbox | LOG-01–04; OPS-01–05 |
| Packaging/project boundaries and tests | SET-02/03; INT-01 |

## Issue index

Status is planning readiness, not implementation completion. “Split / freeze first” entries are parent briefs and must not be sent unchanged to AI implement. “Runtime/tooling setup required” entries also need exact paths and test commands inserted after setup.

| ID | Title | Depends on | Status |
| --- | --- | --- | --- |
| [SET-01](#set-01-reconcile-planning-documents-with-the-implemented-bootstrap) | Reconcile planning documents with the implemented bootstrap | Current bootstrap | Environment task |
| [SET-02](#set-02-enable-bounded-runtime-projects-and-trusted-runtime-tests) | Enable bounded runtime projects and trusted runtime tests | DEC-03, DEC-04 | Environment task |
| [SET-03](#set-03-enable-optional-package-and-language-server-test-projects) | Enable optional package and language-server test projects | SET-02 | Split / freeze first |
| [DEC-01](#dec-01-freeze-extended-primitives-and-standard-value-semantics) | Freeze extended primitives and standard-value semantics | Current bootstrap | Design only |
| [DEC-02](#dec-02-freeze-advanced-grammar-and-symbol-resolution-rules) | Freeze advanced grammar and symbol-resolution rules | Current bootstrap | Design only |
| [DEC-03](#dec-03-freeze-worker-invocation-and-failure-contracts) | Freeze worker invocation and failure contracts | Current bootstrap | Design only |
| [DEC-04](#dec-04-freeze-durable-queue-and-store-protocols) | Freeze durable queue and store protocols | Current bootstrap | Design only |
| [DEC-05](#dec-05-freeze-http-composition-and-authorization-contracts) | Freeze HTTP composition and authorization contracts | Current bootstrap | Design only |
| [DEC-06](#dec-06-freeze-safe-clr-and-nuget-interoperability) | Freeze safe CLR and NuGet interoperability | Current bootstrap | Design only |
| [DEC-07](#dec-07-freeze-resource-configuration-and-startup-lifecycle) | Freeze resource, configuration and startup lifecycle | Current bootstrap | Design only |
| [DEC-08](#dec-08-freeze-scheduling-and-websocket-lifecycle-contracts) | Freeze scheduling and WebSocket lifecycle contracts | Current bootstrap | Design only |
| [LANG-01](#lang-01-implement-immutable-local-let-bindings) | Implement immutable local let bindings | Current bootstrap | Existing #7 |
| [LANG-02](#lang-02-implement-int-arithmetic-and-parentheses) | Implement int arithmetic and parentheses | LANG-01 | Candidate; dependencies first |
| [LANG-03](#lang-03-implement-typed-parameters-and-same-module-function-calls) | Implement typed parameters and same-module function calls | LANG-01 | Candidate; dependencies first |
| [LANG-04](#lang-04-implement-bool-values-comparisons-and-short-circuit-logic) | Implement bool values, comparisons and short-circuit logic | LANG-02, LANG-03 | Candidate; dependencies first |
| [LANG-05](#lang-05-implement-ifelse-expressions-and-nested-lexical-scopes) | Implement if/else expressions and nested lexical scopes | LANG-04 | Candidate; dependencies first |
| [LANG-06](#lang-06-implement-string-and-char-literals) | Implement string and char literals | LANG-03 | Candidate; dependencies first |
| [LANG-07](#lang-07-implement-unit-and-explicit-return) | Implement Unit and explicit return | LANG-05 | Candidate; dependencies first |
| [LANG-08](#lang-08-implement-immutable-record-types-and-copy-updates) | Implement immutable record types and copy updates | LANG-06, LANG-07 | Candidate; dependencies first |
| [LANG-09](#lang-09-implement-discriminated-unions-and-nominal-domain-wrappers) | Implement discriminated unions and nominal domain wrappers | LANG-08 | Candidate; dependencies first |
| [LANG-10](#lang-10-implement-exhaustive-match-and-guards) | Implement exhaustive match and guards | LANG-09, LANG-04 | Candidate; dependencies first |
| [LANG-11](#lang-11-implement-native-nullable-types-and-direct-matching) | Implement native nullable types and direct matching | LANG-10 | Candidate; dependencies first |
| [LANG-12](#lang-12-implement-nullable-fallback-and-conditional-field-access) | Implement nullable fallback and conditional field access | LANG-11 | Candidate; dependencies first |
| [LANG-13](#lang-13-implement-generic-data-and-generic-functions) | Implement generic data and generic functions | LANG-09, LANG-03, DEC-02 | Candidate; dependencies first |
| [LANG-14](#lang-14-implement-result-and-successerror-constructors) | Implement Result and Success/Error constructors | LANG-13, LANG-11, LANG-07 | Candidate; dependencies first |
| [LANG-15](#lang-15-implement-postfix-result-propagation) | Implement postfix Result propagation | LANG-14, LANG-07 | Candidate; dependencies first |
| [LANG-16](#lang-16-implement-function-types-and-capture-free-lambdas) | Implement function types and capture-free lambdas | LANG-03, LANG-07, DEC-02 | Candidate; dependencies first |
| [LANG-17](#lang-17-implement-immutable-closure-capture-and-returned-closures) | Implement immutable closure capture and returned closures | LANG-16, LANG-08 | Candidate; dependencies first |
| [LANG-18](#lang-18-implement-value-piping-and-simple-return-type-inference) | Implement value piping and simple return-type inference | LANG-16, LANG-03, DEC-02 | Candidate; dependencies first |
| [LANG-19](#lang-19-implement-tuples-and-tuple-destructuring) | Implement tuples and tuple destructuring | LANG-08, LANG-16 | Candidate; dependencies first |
| [LANG-41](#lang-41-implement-automatic-partial-application) | Implement automatic partial application (#129; core #135) | LANG-13, LANG-16–19; staged method/async/lifetime prerequisites | Core test migration before dispatch; later children required |
| [LANG-20](#lang-20-implement-immutable-list-values-and-core-operations) | Implement immutable List values and core operations | LANG-13, LANG-16, DEC-01 | Candidate; dependencies first |
| [LANG-21](#lang-21-implement-immutable-map-and-set-values) | Implement immutable Map and Set values | LANG-20, DEC-01 | Candidate; dependencies first |
| [LANG-22](#lang-22-implement-sequential-forin-iteration) | Implement sequential for/in iteration | LANG-20, LANG-07 | Candidate; dependencies first |
| [LANG-23](#lang-23-implement-method-local-mutable-builders-and-escape-checks) | Implement method-local mutable builders and escape checks | LANG-21, LANG-17, LANG-22 | Candidate; dependencies first |
| [LANG-24](#lang-24-implement-async-functions-task-and-await) | Implement async functions, Task and await | LANG-15, LANG-03, DEC-02, LANG-23 | Candidate; dependencies first |
| [LANG-25](#lang-25-implement-async-lambdas) | Implement async lambdas | LANG-24, LANG-17 | Candidate; dependencies first |
| [LANG-26](#lang-26-implement-interfaces-and-explicitly-implemented-services) | Implement interfaces and explicitly implemented services | LANG-13, LANG-24 | Candidate; dependencies first |
| [LANG-27](#lang-27-implement-default-interface-method-bodies) | Implement default interface method bodies | LANG-26 | Candidate; dependencies first |
| [LANG-28](#lang-28-implement-scoped-using-and-resource-non-escape) | Implement scoped using and resource non-escape | LANG-24, LANG-26, DEC-07 | Candidate; dependencies first |
| [LANG-29](#lang-29-implement-multi-file-modules-and-imports) | Implement multi-file modules and imports | LANG-03, DEC-02 | Candidate; dependencies first |
| [LANG-30](#lang-30-implement-statically-resolved-extensions) | Implement statically resolved extensions | LANG-29, LANG-26 | Candidate; dependencies first |
| [LANG-31](#lang-31-implement-integer-widths-and-extended-integer-literals) | Implement integer widths and extended integer literals | LANG-02, DEC-01 | Candidate; dependencies first |
| [LANG-32](#lang-32-implement-float32-float-and-decimal) | Implement float32, float and decimal | LANG-31, DEC-01 | Candidate; dependencies first |
| [LANG-33](#lang-33-implement-string-interpolation) | Implement string interpolation | LANG-06, DEC-01 | Candidate; dependencies first |
| [LANG-34](#lang-34-implement-worker-declaration-contracts) | Implement worker declaration contracts | LANG-26, LANG-14, LANG-24, DEC-03 | Candidate; dependencies first |
| [LANG-35](#lang-35-implement-triggered-agent-contracts-and-message-restrictions) | Implement triggered-agent contracts and message restrictions | LANG-34, LANG-23, DEC-04 | Split / freeze first |
| [LANG-36](#lang-36-implement-typed-execution-policy-declarations) | Implement typed execution-policy declarations | LANG-35, DEC-04 | Candidate; dependencies first |
| [LANG-37](#lang-37-implement-code-based-application-composition) | Implement code-based application composition | LANG-29, LANG-35, LANG-36, DEC-07 | Candidate; dependencies first |
| [LANG-38](#lang-38-implement-block-comments-and-attached-documentation-comments) | Implement block comments and attached documentation comments | DEC-02 | Candidate; dependencies first |
| [RUN-01](#run-01-execute-isolated-callable-worker-invocations) | Execute isolated callable worker invocations | SET-02, LANG-34 | Runtime/tooling setup required |
| [RUN-02](#run-02-handle-worker-faults-deadlines-and-cancellation) | Handle worker faults, deadlines and cancellation | RUN-01, DEC-03 | Runtime/tooling setup required |
| [RUN-03](#run-03-start-and-stop-explicitly-registered-applications) | Start and stop explicitly registered applications | SET-02, LANG-37, DEC-07 | Runtime/tooling setup required |
| [RUN-04](#run-04-implement-runtimestore-contracts-and-conformance-harness) | Implement RuntimeStore contracts and conformance harness | SET-02, DEC-04 | Runtime/tooling setup required |
| [RUN-05](#run-05-implement-sqlite-runtime-schema-and-migrations) | Implement SQLite runtime schema and migrations | RUN-04 | Runtime/tooling setup required |
| [RUN-06](#run-06-implement-durable-enqueue-and-acceptance-receipts) | Implement durable enqueue and acceptance receipts | RUN-05, LIB-01, LANG-35 | Runtime/tooling setup required |
| [RUN-07](#run-07-implement-atomic-claims-and-expiring-ownership) | Implement atomic claims and expiring ownership | RUN-06 | Runtime/tooling setup required |
| [RUN-08](#run-08-implement-completion-and-stale-ack-rejection) | Implement completion and stale-ack rejection | RUN-07 | Runtime/tooling setup required |
| [RUN-09](#run-09-connect-queue-execution-to-typed-handler-outcomes) | Connect queue execution to typed handler outcomes | RUN-08, RUN-01, LANG-36 | Runtime/tooling setup required |
| [RUN-10](#run-10-implement-retries-delays-and-retained-failures) | Implement retries, delays and retained failures | RUN-09 | Runtime/tooling setup required |
| [RUN-11](#run-11-implement-per-key-sequential-ordering-and-concurrency) | Implement per-key sequential ordering and concurrency | RUN-10 | Runtime/tooling setup required |
| [RUN-12](#run-12-implement-queue-capacity-and-cooperative-cancellation) | Implement queue capacity and cooperative cancellation | RUN-11 | Runtime/tooling setup required |
| [RUN-13](#run-13-implement-atomic-completion-with-follow-up-enqueue) | Implement atomic completion with follow-up enqueue | RUN-08 | Runtime/tooling setup required |
| [RUN-14](#run-14-implement-recoverable-event-fan-out) | Implement recoverable event fan-out | RUN-09, RUN-13, LANG-35 | Runtime/tooling setup required |
| [RUN-15](#run-15-implement-durable-cron-occurrence-creation) | Implement durable cron occurrence creation | RUN-13, DEC-08, LIB-02 | Runtime/tooling setup required |
| [RUN-16](#run-16-implement-queue-inspection-and-operator-controls) | Implement queue inspection and operator controls | RUN-12, DEC-05 | Runtime/tooling setup required |
| [RUN-17](#run-17-implement-postgresql-runtimestore-provider) | Implement PostgreSQL RuntimeStore provider | SET-03, RUN-04, RUN-16 | Split / freeze first |
| [RUN-18](#run-18-implement-mysql-runtimestore-provider) | Implement MySQL RuntimeStore provider | SET-03, RUN-04, RUN-16 | Split / freeze first |
| [APP-01](#app-01-implement-typed-configuration-loading) | Implement typed configuration loading | RUN-03, LIB-01, DEC-07 | Runtime/tooling setup required |
| [APP-02](#app-02-implement-asynchronous-secret-provider-initialization) | Implement asynchronous secret-provider initialization | APP-01, DEC-07 | Runtime/tooling setup required |
| [APP-03](#app-03-implement-lazy-resource-initialization-and-replacement) | Implement lazy resource initialization and replacement | APP-02 | Runtime/tooling setup required |
| [APP-04](#app-04-implement-scoped-database-and-filesystem-capabilities) | Implement scoped database and filesystem capabilities | SET-03, LANG-28, APP-02, DEC-06 | Split / freeze first |
| [LIB-01](#lib-01-implement-typed-json-encoding-and-decoding) | Implement typed JSON encoding and decoding | SET-02, LANG-21, LANG-11, LANG-14, DEC-04 | Runtime/tooling setup required |
| [LIB-02](#lib-02-implement-standard-immutable-time-identity-and-binary-values) | Implement standard immutable time, identity and binary values | SET-02, DEC-01, LIB-01 | Runtime/tooling setup required |
| [LIB-03](#lib-03-design-and-implement-an-outbound-http-client-adapter) | Design and implement an outbound HTTP client adapter | SET-03, DEC-06, DEC-07, LANG-28 | Split / freeze first |
| [HTTP-01](#http-01-host-explicitly-registered-http-agents) | Host explicitly registered HTTP agents | SET-03, RUN-03, LANG-35, DEC-05 | Runtime/tooling setup required |
| [HTTP-02](#http-02-implement-typed-path-query-and-json-body-binding) | Implement typed path, query and JSON body binding | HTTP-01, LIB-01 | Runtime/tooling setup required |
| [HTTP-03](#http-03-implement-typed-asynchronous-transformation-pipelines) | Implement HTTP callable preprocessing | HTTP-02, LANG-16, LANG-26, LANG-41, LIB-04 | Runtime/tooling setup required |
| [HTTP-04](#http-04-implement-wrapping-middleware-and-cleanup) | Implement wrapping middleware and cleanup | HTTP-03, LANG-28 | Runtime/tooling setup required |
| [HTTP-05](#http-05-implement-runtime-authentication-and-authorization-checks) | Implement runtime authentication and authorization checks | HTTP-04, DEC-05 | Runtime/tooling setup required |
| [HTTP-06](#http-06-implement-typed-websocket-connection-handling) | Implement typed WebSocket connection handling | HTTP-01, DEC-08 | Split / freeze first |
| [INT-01](#int-01-implement-explicit-nuget-reference-and-package-resolution) | Implement explicit NuGet reference and package resolution | SET-03, LANG-29, DEC-06 | Runtime/tooling setup required |
| [INT-02](#int-02-implement-the-safe-c-value-and-method-adapter-subset) | Implement the safe C# value and method adapter subset | INT-01, LANG-26, LANG-28 | Runtime/tooling setup required |
| [INT-03](#int-03-implement-clr-async-error-and-delegate-adapters) | Implement CLR async, error and delegate adapters | INT-02, LANG-25 | Runtime/tooling setup required |
| [INT-04](#int-04-implement-the-initial-f-nuget-adapter-subset) | Implement the initial F# NuGet adapter subset | INT-03 | Runtime/tooling setup required |
| [LIB-04](#lib-04-implement-result-composition-helpers) | Implement Result composition helpers (#130; sync #136) | LANG-14, core #135; LANG-24/25 for async coverage | New target; staged sync/async work |
| [LOG-01](#log-01-implement-logevent-templates-and-scope-capture) | Implement LogEvent, templates and scope capture | Relevant setup, collections, async, scoped resources and standard encodings | Runtime setup and bounded slices required |
| [LOG-02](#log-02-implement-logger-actor-console-and-output-lifecycle) | Implement Logger actor, console and output lifecycle | LOG-01, RUN-01, RUN-03, DEC-07 | Runtime setup required |
| [LOG-03](#log-03-implement-database-log-output-and-retention) | Implement database log output and retention | LOG-02, RUN-04/05/09, LIB-01 | Real-provider tests required |
| [LOG-04](#log-04-implement-logreader-and-observer-search) | Implement LogReader and observer search | LOG-03, OPS-02, HTTP-05 | Real-provider/auth tests required |
| [OPS-01](#ops-01-implement-correlated-structured-logs-metrics-and-tracing) | Implement runtime instrumentation, metrics and tracing through Logger | SET-03, RUN-09, HTTP-01, LOG-02 | Split / freeze first |
| [OPS-02](#ops-02-implement-protected-runtime-observer-dashboard) | Implement protected runtime observer dashboard | RUN-16, HTTP-05, OPS-01 | Split / freeze first |
| [OPS-03](#ops-03-implement-health-checks-and-bounded-graceful-draining) | Implement health checks and bounded graceful draining | RUN-03, RUN-12, HTTP-01 | Runtime/tooling setup required |
| [OPS-04](#ops-04-provide-handler-and-application-testing-harnesses) | Provide handler and application testing harnesses | SET-03, RUN-09, HTTP-03 | Split / freeze first |
| [OPS-05](#ops-05-design-business-data-transactions-and-outbox-integration) | Design business-data transactions and outbox integration | DEC-04, DEC-07, DEC-06 | Design only |
| [TOOL-01](#tool-01-expose-consistent-compiler-diagnostics-and-status) | Expose consistent compiler diagnostics and status | LANG-35 | Candidate; dependencies first |
| [TOOL-02](#tool-02-implement-language-server-document-diagnostics) | Implement language-server document diagnostics | SET-03, TOOL-01 | Runtime/tooling setup required |
| [TOOL-03](#tool-03-implement-semantic-completion-hover-and-signature-help) | Implement semantic completion, hover and signature help | TOOL-02, LANG-36, LANG-16 | Runtime/tooling setup required |
| [TOOL-04](#tool-04-implement-required-members-code-actions) | Implement required-members code actions | TOOL-03 | Runtime/tooling setup required |
| [TOOL-05](#tool-05-generate-application-inventory-from-registration-metadata) | Generate application inventory from registration metadata | LANG-37, RUN-03 | Runtime/tooling setup required |

## Issue briefs

For a compiler candidate, copy its six sections into the approved issue form only after dependencies are complete and its concrete contract is accepted. The “Decisions already made” section is the proposed frozen contract of that new issue; merely listing it here does not approve it.

For runtime/tooling and parent briefs, the explicit blockers in Constraints and any extra gates must be resolved first. Keep unready issues unapproved. Do not ask an implementation worker to choose new public APIs implicitly.

## Repository and decision work

## SET-01: Reconcile planning documents with the implemented bootstrap

**Planning status:** Environment task.

### Goal

Update the repository handoff so new agents start from the current compiler and automation boundary.

Dependencies: current bootstrap. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Remove obsolete claims that compiler decisions are open or automation is documentation-only; preserve genuinely provisional language APIs.

AC2: Link the implemented bootstrap and existing issue #7; record the current compiler-code allowlist and verification commands.

AC3: Keep feature status evidence-based; do not mark draft syntax or an open issue implemented.

### Constraints

Use the normal repository environment; these paths exceed current worker permissions. Define exact files/dependencies, preserve tests/protections and use a separate reviewed PR.

### Non-goals

No new compiler feature; no changes to workflow permissions.

### Verification

Run existing bootstrap/verifier/compiler checks. For infrastructure, demonstrate failure detection, remove the deliberate failure, then pass all checks. Record exact commands for dependent issues.

### Decisions already made

The repository now uses C# generation, the pinned .NET 10 SDK and compiler-code automation. Older implementation instructions are historical where they conflict with current repository policy.

## SET-02: Enable bounded runtime projects and trusted runtime tests

**Planning status:** Environment task.

### Goal

Prepare the repository for separately scoped runtime implementation tasks.

Dependencies: DEC-03, DEC-04. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Choose and record runtime project boundaries, references, exact dependency versions and file allowlists before adding projects.

AC2: Add locked restore/build/test coverage and a runtime feature-test location to the trusted verifier; prove an intentionally failing runtime test fails verification.

AC3: Permit only the approved runtime paths while preserving immutable prior tests, independent verification and existing compiler checks.

AC4: Add a minimal runtime test harness with a controllable clock and deterministic randomness; record the commands future issues must run.

### Constraints

Use the normal repository environment; these paths exceed current worker permissions. Define exact files/dependencies, preserve tests/protections and use a separate reviewed PR.

### Non-goals

No worker execution, queue persistence or blanket expansion of automation permissions.

### Verification

Run existing bootstrap/verifier/compiler checks. For infrastructure, demonstrate failure detection, remove the deliberate failure, then pass all checks. Record exact commands for dependent issues.

### Decisions already made

This is a maintainer/environment task. The present compiler workflow cannot edit project files, dependency locks, verification scripts or runtime project paths.

## SET-03: Enable optional package and language-server test projects

**Planning status:** Split / freeze first.

**Before dispatch:** Create one issue per optional subsystem with exact paths, pinned dependencies and trusted verification commands.

### Goal

Prepare separate optional transport, provider, interop and tooling projects as each subsystem becomes ready.

Dependencies: SET-02. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Record the exact projects, package versions and permitted paths for the next selected subsystem; split this task by subsystem rather than enabling everything at once.

AC2: Extend trusted verification with its integration or protocol tests and prove failure propagation.

AC3: Keep compiler-only and worker-only builds usable without optional HTTP, database-provider or editor dependencies.

### Constraints

Use the normal repository environment; these paths exceed current worker permissions. Define exact files/dependencies, preserve tests/protections and use a separate reviewed PR.

### Non-goals

No implementation of the optional feature; no implicit service discovery.

### Verification

Run existing bootstrap/verifier/compiler checks. For infrastructure, demonstrate failure detection, remove the deliberate failure, then pass all checks. Record exact commands for dependent issues.

### Decisions already made

Treat this as a repeatable infrastructure template, instantiated separately for HTTP, providers, interop and tooling after their design gates. Each instance is a distinct bounded issue.

## DEC-01: Freeze extended primitives and standard-value semantics

**Planning status:** Design only.

### Goal

Write an approved contract for numeric and standard values beyond the bootstrap int.

Dependencies: current bootstrap. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Specify integer widths, literal ranges/suffixes/separators, conversions, checked arithmetic and division edge cases.

AC2: Resolve float versus float32 naming, decimal rules, NaN/equality and culture-independent formatting.

AC3: Specify uuid, Instant, LocalDate, LocalTime, Duration and Bytes representations and minimum APIs; distinguish values from serialized encodings.

AC4: Include exact valid/invalid examples and separate optional later APIs from the first slices.

AC5: Freeze immutable collection construction/core APIs, lookup failure behavior, duplicate-key policy, equality/hash eligibility and deterministic formatting for supported values.

### Constraints

Write only `docs/features/implementation/DEC-01.md`. Preserve settled choices; label proposals. No implementation/dependency/baseline edits. Explicitly adopt the reviewed contract before dependent implementation.

### Non-goals

No compiler implementation or currency domain policy.

### Verification

Run `bash .github/ai/bootstrap.sh` and `bash .github/ai/verify.sh`. Check examples/invariants against current specifications and link dependents. Passing documentation checks does not approve a contract or demonstrate runtime behavior.

### Decisions already made

int is signed 32-bit. Extra scalar spellings and standard-library APIs in the syntax reference are proposals until approved. No general implicit mixed-numeric conversions.

## DEC-02: Freeze advanced grammar and symbol-resolution rules

**Planning status:** Design only.

### Goal

Resolve the remaining grammar needed by generics, function values, modules and extensions.

Dependencies: current bootstrap. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Specify function-type/lambda grammar, generic call disambiguation and return-inference limits including recursive functions.

AC2: Specify module merging, import lookup, duplicate/ambiguous symbols and extension conflicts.

AC3: Record operator precedence, contextual versus reserved words and supported comment/escape syntax with parser fixtures.

AC4: Preserve public-only declarations, braces, no semicolons and no general reassignment.

AC5: Define newline continuation inside delimiters/after operators, non-nested block comments and declaration documentation attachment.

### Constraints

Write only `docs/features/implementation/DEC-02.md`. Preserve settled choices; label proposals. No implementation/dependency/baseline edits. Explicitly adopt the reviewed contract before dependent implementation.

### Non-goals

No runtime protocols, package manifest or editor implementation.

### Verification

Run `bash .github/ai/bootstrap.sh` and `bash .github/ai/verify.sh`. Check examples/invariants against current specifications and link dependents. Passing documentation checks does not approve a contract or demonstrate runtime behavior.

### Decisions already made

Use the syntax reference as the proposal; this issue approves a bounded grammar record, not every illustrative API.

## DEC-03: Freeze worker invocation and failure contracts

**Planning status:** Design only.

### Goal

Define the caller-visible and supervisor-visible outcomes for callable workers.

Dependencies: current bootstrap. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Choose the exact result shape for application errors, faults, timeout and cancellation; explicitly settle or replace the proposed CallError wrapper.

AC2: Define caller cancellation versus continued remote work, late completion and bounded waiting/call cycles.

AC3: Define fresh invocation ownership, dependency lifetimes and supported worker policies.

AC4: Provide sample calls and failure tests; retain the distinction between ordinary service calls and supervised worker calls.

### Constraints

Write only `docs/features/implementation/DEC-03.md`. Preserve settled choices; label proposals. No implementation/dependency/baseline edits. Explicitly adopt the reviewed contract before dependent implementation.

### Non-goals

No durable queue protocol or persistent actor/cache semantics.

### Verification

Run `bash .github/ai/bootstrap.sh` and `bash .github/ai/verify.sh`. Check examples/invariants against current specifications and link dependents. Passing documentation checks does not approve a contract or demonstrate runtime behavior.

### Decisions already made

Only workers support awaited handler results. Every normal handler completion reports Result. Invocation isolation is within a CLR process, not an OS-process or BEAM guarantee.

## DEC-04: Freeze durable queue and store protocols

**Planning status:** Design only.

### Goal

Specify queue acceptance, attempts, serialization and atomic store operations before implementing durability.

Dependencies: current bootstrap. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Define acceptance receipt and persistence point, stable identities, payload versions, eligibility, attempts, lease tokens and terminal states.

AC2: Resolve how Result<Unit,E> outcomes request retry, rejection or rescheduling without introducing awaited queue replies.

AC3: Specify delay/backoff/jitter, per-key ordering, capacity/concurrency scopes, deduplication, cancellation and failure retention.

AC4: Define atomic follow-up enqueue, event fan-out recovery and provider capabilities; include crash-point/stale-owner test cases.

### Constraints

Write only `docs/features/implementation/DEC-04.md`. Preserve settled choices; label proposals. No implementation/dependency/baseline edits. Explicitly adopt the reviewed contract before dependent implementation.

### Non-goals

No implementation; no exactly-once claim or implicit cross-database transaction.

### Verification

Run `bash .github/ai/bootstrap.sh` and `bash .github/ai/verify.sh`. Check examples/invariants against current specifications and link dependents. Passing documentation checks does not approve a contract or demonstrate runtime behavior.

### Decisions already made

At least once; one SQLite runtime file per application initially. Queue roots are declared immutable data types or Unit. External side effects are not rolled back by retry.

## DEC-05: Freeze HTTP composition and authorization contracts

**Planning status:** Design only.

### Goal

Approve typed transformation and wrapping middleware contracts before transport integration.

Dependencies: current bootstrap. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Specify request/path/query/body and response types, pipeline error composition and explicit status mappings.

AC2: Specify asynchronous composition, context preservation, short-circuiting and wrapper cleanup under failure/cancellation.

AC3: Define authentication versus authorization and how public-only construction interacts with runtime-valid evidence.

AC4: Provide one protected route example and negative tests for mismatched inputs, erased errors and fabricated authorization context.

### Constraints

Write only `docs/features/implementation/DEC-05.md`. Preserve settled choices; label proposals. No implementation/dependency/baseline edits. Explicitly adopt the reviewed contract before dependent implementation.

### Non-goals

No provider-specific identity integration, WebSockets or gRPC.

### Verification

Run `bash .github/ai/bootstrap.sh` and `bash .github/ai/verify.sh`. Check examples/invariants against current specifications and link dependents. Passing documentation checks does not approve a contract or demonstrate runtime behavior.

### Decisions already made

Typed pipelines deliver the exact handler input. A public record named Authorized is not proof of permission. Handlers declare routes and are explicitly registered.

## DEC-06: Freeze safe CLR and NuGet interoperability

**Planning status:** Design only.

### Goal

Define the first supported C#/F# library boundary and build integration.

Dependencies: current bootstrap. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Specify eligible CLR values/methods, null/exception mapping, ownership of foreign mutable objects and safe copying/wrapping.

AC2: Specify Task/cancellation/disposal and delegate adaptation without allowing local builders or scoped resources to escape.

AC3: Define package resolution/locking and initial support limits for native assets, source generators and framework compatibility.

AC4: Define explicit F# option/union/function/async adapters that preserve D# Option distinctions, including None versus Some(null), where applicable. The precise mapping remains for the interop decision.

### Constraints

Write only `docs/features/implementation/DEC-06.md`. Preserve settled choices; label proposals. No implementation/dependency/baseline edits. Explicitly adopt the reviewed contract before dependent implementation.

### Non-goals

No unrestricted CLR escape hatch, trimming or Native AOT.

### Verification

Run `bash .github/ai/bootstrap.sh` and `bash .github/ai/verify.sh`. Check examples/invariants against current specifications and link dependents. Passing documentation checks does not approve a contract or demonstrate runtime behavior.

### Decisions already made

NuGet interoperability is intended, not blanket compatibility. D# data immutability and agent restrictions remain enforced across foreign calls.

## DEC-07: Freeze resource, configuration and startup lifecycle

**Planning status:** Design only.

### Goal

Define explicit dependency ownership and asynchronous host initialization.

Dependencies: current bootstrap. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Specify typed configuration bindings and precedence, validation and redaction; approve secret-provider APIs.

AC2: Define eager/lazy initialization, one shared lazy attempt, failure retry, dependency cycles and failed-start cleanup.

AC3: Define scoped resource lifetime/disposal plus database transaction commit/rollback and filesystem capability rules.

AC4: Specify secret refresh/replacement behavior and graceful shutdown deadlines without mutating immutable settings.

### Constraints

Write only `docs/features/implementation/DEC-07.md`. Preserve settled choices; label proposals. No implementation/dependency/baseline edits. Explicitly adopt the reviewed contract before dependent implementation.

### Non-goals

No application YAML or persistent actor lifetime model.

### Verification

Run `bash .github/ai/bootstrap.sh` and `bash .github/ai/verify.sh`. Check examples/invariants against current specifications and link dependents. Passing documentation checks does not approve a contract or demonstrate runtime behavior.

### Decisions already made

Application wiring is code; values can come from JSON/environment/provider sources. Resource acquisition follows lifecycle rules and must not happen merely from metadata inspection.

## DEC-08: Freeze scheduling and WebSocket lifecycle contracts

**Planning status:** Design only.

### Goal

Record the two remaining transport/time protocols as separately approvable sections.

Dependencies: current bootstrap. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: For schedules, choose cron dialect, timezone data, DST behavior, occurrence identity and Skip/RunOnce/CatchUp limits.

AC2: Define atomic schedule advancement plus queued occurrence creation and restart recovery examples.

AC3: For WebSockets, define connection ownership, typed frames/messages, bounded ordered output, disconnect/cancellation and reconnect expectations.

AC4: Allow each section to be approved independently; create separate issues if either section exceeds a small review.

### Constraints

Write only `docs/features/implementation/DEC-08.md`. Preserve settled choices; label proposals. No implementation/dependency/baseline edits. Explicitly adopt the reviewed contract before dependent implementation.

### Non-goals

No implementation, general streaming protocol or gRPC.

### Verification

Run `bash .github/ai/bootstrap.sh` and `bash .github/ai/verify.sh`. Check examples/invariants against current specifications and link dependents. Passing documentation checks does not approve a contract or demonstrate runtime behavior.

### Decisions already made

Cron delivers through queues. Sockets are live resources; durable messages do not preserve connections across restart.

## Language and compiler

## LANG-01: Implement immutable local let bindings

**Planning status:** Existing #7.

### Goal

Reuse existing issue #7: immutable local int bindings, annotations and final local expressions.

Dependencies: current bootstrap. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Complete https://github.com/gajo357/dsharplang/issues/7 without creating a duplicate.

AC2: Require its positive/negative feature tests and executed generated-C# result of 42 before recording implementation.

### Constraints

Use issue #7's exact approved/frozen scope. Do not create another issue or change an active frozen run using this document.

### Non-goals

All features excluded by issue #7.

### Verification

Use all verification and AC evidence required by issue #7; an open/approved issue is not proof of implementation.

### Decisions already made

The open issue is already labelled ai:approved. Its frozen body controls execution; this backlog does not amend an active run.

## LANG-02: Implement int arithmetic and parentheses

**Planning status:** Candidate; dependencies first.

### Goal

Compile integer expressions such as `(20 + 1) * 2` to executable C# returning 42.

Example:
```text
module Demo

fn answer(): int {
    let base = 20
    (base + 1) * 2
}
```

Dependencies: LANG-01. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Support unary minus and +, -, *, /, % with conventional multiplicative precedence and parentheses; apply them to literals and locals.

AC2: Use checked int arithmetic; division truncates toward zero, with overflow and division by zero treated as execution faults.

AC3: Reject invalid operands/malformed expressions with D# spans; execute precedence, negative-division, overflow and zero-division tests.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No additional numeric types, comparisons or user-defined operators.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

Approval of this issue freezes this int-only slice. Preserve current positive literal range; do not add the special -2147483648 literal case here. Evaluate operands left to right.

## LANG-03: Implement typed parameters and same-module function calls

**Planning status:** Candidate; dependencies first.

### Goal

Support `fn identity(value: int): int { value }` and calls from another function.

Example:
```text
module Demo

fn identity(value: int): int {
    value
}

fn answer(): int {
    identity(42)
}
```

Dependencies: LANG-01. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Parse typed parameters and argument lists; resolve same-module declarations independently of source order.

AC2: Check duplicate parameters, arity and argument/return types; parameters are immutable and function-local.

AC3: Generate and execute nested calls including forward calls; reject unknown names and conflicting local/parameter names.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No overloads, recursion inference, imports, lambdas or named/default arguments.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

Use explicit int parameter and return types; uncurried positional calls. Preserve existing compiler entry points and generated public surface.

## LANG-04: Implement bool values, comparisons and short-circuit logic

**Planning status:** Candidate; dependencies first.

### Goal

Add bool expressions usable in functions, parameters and locals.

Dependencies: LANG-02, LANG-03. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Support true/false, bool annotations, int equality/order comparisons, bool equality, !, && and ||.

AC2: Require compatible operand types and reject chained comparisons.

AC3: Prove short-circuit evaluation prevents a failing right operand from executing; test operator precedence and emitted execution.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No nullable flow analysis, structural data equality or bitwise operators.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

Comparisons return bool. Logical operators accept bool only; there is no truthiness.

## LANG-05: Implement if/else expressions and nested lexical scopes

**Planning status:** Candidate; dependencies first.

### Goal

Return typed values from conditional branches with ordinary block-local bindings.

Dependencies: LANG-04. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Require bool conditions and else when if supplies a non-Unit value.

AC2: Require compatible branch types and keep bindings local to their blocks.

AC3: Execute both branches and nested conditionals; reject mismatched branches and references outside scope.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No nullable narrowing from comparisons, loops or pattern matching.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

For this first slice require exact branch-type equality and reject redeclaring an already visible local name. Later approved nullability rules may add contextual conversions.

## LANG-06: Implement string and char literals

**Planning status:** Candidate; dependencies first.

### Goal

Add immutable UTF-16 string and char values.

Dependencies: LANG-03. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Support literals, annotations, parameters, returns and the documented basic escapes; reject malformed escapes and invalid char lengths.

AC2: Preserve non-ASCII text through C# emission; handle quotes/backslashes without generated-code injection.

AC3: Execute round trips and string/char equality with ordinal semantics.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No interpolation, raw/multiline literals, concatenation or culture-sensitive text APIs.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

A char is one UTF-16 code unit. Literal escaping is a compiler responsibility.

## LANG-07: Implement Unit and explicit return

**Planning status:** Candidate; dependencies first.

### Goal

Support no-payload functions and early exit from the nearest function.

Dependencies: LANG-05. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Represent Unit by () and check explicit return values against the function result type.

AC2: Allow if without else only in a Unit context; reject a missing required result on reachable completion paths.

AC3: Execute early returns from nested blocks and ensure the remainder of that invocation is not evaluated.

AC4: Treat a block with no final value as Unit, while never converting it into implicit Success().

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No loop control, implicit handler success or exceptions syntax.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

Unit is a value, not null. The final expression still returns implicitly; return targets the nearest function body.

## LANG-08: Implement immutable record types and copy updates

**Planning status:** Candidate; dependencies first.

### Goal

Construct named data records, read fields and create modified copies.

Example:
```text
type Invoice {
    total: int
}

fn make(): Invoice {
    Invoice { total = 42 }
}
```

Dependencies: LANG-06, LANG-07. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Support `type Name { field: T }`, named construction and field access for supported immutable scalar/nested record fields.

AC2: Reject missing/unknown/duplicate fields, incompatible values and field reassignment; all fields are public.

AC3: Implement copy-with updates without modifying the original, plus structural equality for the supported fields; execute independent-copy tests.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No mutable foreign fields, callbacks, collections or automatic domain validation.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

Approval freezes `type Invoice { total: int }`, `Invoice { total = 42 }`, `invoice.total`, and `invoice with { total = 43 }` for this slice. No class inheritance; constructors/fields are public.

## LANG-09: Implement discriminated unions and nominal domain wrappers

**Planning status:** Candidate; dependencies first.

### Goal

Represent alternatives with zero or multiple typed payloads.

Dependencies: LANG-08. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Parse leading-| cases, resolve qualified/expected-type case construction and reject ambiguous bare cases.

AC2: Check payload arity/types and preserve declared union identity; distinct single-case wrappers are not interchangeable.

AC3: Generate executable construction and structural equality for eligible payloads; reject illegal mutation.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No pattern matching, generic constraints or function-valued data fields.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

Cases carry immutable data; there is no implementation inheritance. Explicit qualification resolves ambiguity.

## LANG-10: Implement exhaustive match and guards

**Planning status:** Candidate; dependencies first.

### Goal

Match unions, booleans and primitive values with checked coverage.

Dependencies: LANG-09, LANG-04. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Support case patterns, payload binding, nesting, _, and bool when guards with lexical arm scopes.

AC2: Require compatible arm result types and coverage of union/bool cases; open primitive domains need a catch-all.

AC3: Treat guarded arms as incomplete coverage unless an unguarded covering arm exists; evaluate the scrutinee once.

AC4: Test missing cases, wrong payload patterns, first matching arm and compile errors after adding a union case without wildcard coverage.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No arbitrary theorem proving for guards or runtime reflection matching.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

Match is an expression; _ intentionally absorbs future alternatives. Null patterns are added in LANG-11.

## LANG-11: Implement native nullable types and direct matching

**Planning status:** Candidate; dependencies first.

### Goal

Use T? and null directly, without implicitly wrapping nullable values in Option or Some. Explicit Option is tracked separately in [#121](https://github.com/gajo357/dsharplang/issues/121).

Dependencies: LANG-10. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Allow T values or null where T? is expected and reject null for T; cover both value/reference representations in code generation.

AC2: Support match arms for null and a named present value, narrowing the latter to T even if the named arm is first.

AC3: Require coverage and reject unsafe access to nullable data; test nested nullable record/union payloads.

AC4: Do not conflate a present value with null or insert source-visible wrappers.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No null-forgiving operator or comparison-based flow narrowing.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

A named value pattern for a nullable scrutinee matches present values only; _ also matches null. Non-null is explicit through matching.

## LANG-12: Implement nullable fallback and conditional field access

**Planning status:** Candidate; dependencies first.

### Goal

Evaluate `value ?? fallback` and nullable field access safely.

Dependencies: LANG-11. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Make ?? lazy and right-associative with compatible fallback types.

AC2: Support ?. for record field access with a nullable result; preserve null without evaluating downstream access.

AC3: Test nested chains, fallback short-circuiting and errors for incompatible operands.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No ternary operator or implicit unchecked nullable dereference.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

The first slice covers fields only; optional method-call access gets a later follow-up if required. ? postfix remains reserved for Result propagation.

## LANG-13: Implement generic data and generic functions

**Planning status:** Candidate; dependencies first.

### Goal

Type-check and emit parameterized immutable data/functions.

Dependencies: LANG-09, LANG-03, DEC-02. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Support declared type parameters, explicit substitutions and unambiguous call inference.

AC2: Reject arity/type mismatches and unbound parameters; validate data fields after substitution.

AC3: Execute identity<T> and nested generic record/union tests without losing nominal identities.

AC4: Apply nullable normalization idempotently during resolution, substitution, inference and generation. Unconstrained T? instantiated with int, int?, string and string? admits null and normalizes to the appropriate nullable type. Execute generic calls, returns and record fields, and preserve closed Box<int?> support. Generated C# must preserve these semantics rather than rely on unconstrained C# T? alone.

AC5: When alias declarations and collections become supported, test normalization through aliases and null values through collections. These remain future work where unimplemented. Keep [#30](https://github.com/gajo357/dsharplang/issues/30) open until generated behavior across calls and data is correct; parsing T? alone is insufficient.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No unconstrained operators on arbitrary T or advanced generic constraints.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

Use the approved DEC-02 inference/disambiguation rules. Keep generic constraints, variance and type aliases unsupported.

## Explicit Option<T>: [#121](https://github.com/gajo357/dsharplang/issues/121)

Option<T> is a tagged union with None and Some(T). None is not null; Some(null) is allowed only if T is nullable. Construction, exhaustive matching and equality preserve None, Some(null) and nested options as distinct values. Access to a payload requires matching or an explicit operation; None never silently becomes default(T). T? and Option<T> have no implicit conversion erasing these distinctions. Once collections are available, exercise these values in collections. JSON serialization and HTTP binding remain undecided.

## LANG-14: Implement Result and Success/Error constructors

**Planning status:** Candidate; dependencies first.

### Goal

Provide typed success/error values including Unit and nullable success.

Dependencies: LANG-13, LANG-11, LANG-07. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Support Result<T,E>, Success(value), Error(error) and Success() for Unit with expected-type checking.

AC2: Compile Success(invoice) and Success(null) into distinct Result<Invoice?,E> values and match them directly.

AC3: Require both outcomes in exhaustive matching and reject incompatible error/success payloads or ambiguous inference.

AC4: Reject an unused Result expression statement; require matching, propagation, return or an explicitly typed consuming call.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No exception translation, worker error wrapper or implicit propagation.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

Result is a discriminated union with typed cases. No implicit Some/Option layer is added to Result; explicit Option is a separate type.

## LANG-15: Implement postfix Result propagation

**Planning status:** Candidate; dependencies first.

### Goal

Unwrap success and return a compatible error from the enclosing function.

Dependencies: LANG-14, LANG-07. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: For result?, continue with the success value or return its Error once without evaluating later expressions.

AC2: Require the enclosing function to return a compatible Result; reject propagation from plain-returning functions.

AC3: Test nested expressions, error-type mismatch, nullable success and nearest-function propagation.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No automatic error conversion or outer-function return from a lambda.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

Postfix ? applies to Result, not nullable values. Async/lambda handling must reuse these rules when those constructs land.

## LANG-16: Implement function types and capture-free lambdas

**Planning status:** Candidate; dependencies first.

### Goal

Assign, pass and return first-class function values with => syntax.

Dependencies: LANG-03, LANG-07, DEC-02. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Support zero/single/multiple parameters, explicit or expected parameter types, and expression/block lambda bodies.

AC2: Check function arity/return types and invoke a function-valued parameter; execute higher-order examples.

AC3: Ensure explicit return and Result propagation target the lambda; reject underconstrained parameters.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No closure capture, async lambda or overload resolution.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

Use approved (T, U) -> R grammar. Function values are execution objects and cannot be fields/payloads in data type declarations.

## LANG-17: Implement immutable closure capture and returned closures

**Planning status:** Candidate; dependencies first.

### Goal

Return a closure such as multiplier(factor) while preserving lexical values.

Dependencies: LANG-16, LANG-08. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Capture immutable lexical data and keep closures from separate invocations independent.

AC2: Reject callback fields in records/unions and avoid synthesizing publicly accessible mutable closure state.

AC3: Execute returned closures after the creating call completes; test nested scope and nearest-return behavior.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No mutable-builder/resource capture or durable callback serialization.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

Only immutable data captures in this first slice. Resource/service/worker capability captures remain rejected until their lifetime rules are implemented.

## LANG-18: Implement value piping and simple return-type inference

**Planning status:** Historical foundation, implemented by [#35](https://github.com/gajo357/dsharplang/issues/35). See current implementation status for tested limits. LANG-41 tracks the newly specified partial application separately.

### Goal

Compose ordinary helpers using |> and omit return annotations where inference is unambiguous.

Dependencies: LANG-16, LANG-03, DEC-02. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Compile value |> function as one call, checking the function input and evaluating the left side once.

AC2: Infer nonrecursive helper return types from checked bodies; require annotations where approved inference rules cannot resolve them.

AC3: Reject incompatible pipes; prove pipes do not await tasks, unwrap Result or destructure tuples implicitly.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No result-aware pipe operator, automatic partial application in this original slice, or recursive inference. LIB-04 supplies ordinary Result helpers; LANG-41 supplies partial application.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

Use DEC-02 precedence and inference limits. Handler/interface result annotations remain required.

## LANG-19: Implement tuples and tuple destructuring

**Planning status:** Candidate; dependencies first.

### Goal

Represent fixed typed value groups and destructure immutable bindings.

Dependencies: LANG-08, LANG-16. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Support tuple expressions/types and `let (left, right) = value` with checked arity.

AC2: Distinguish a single tuple argument from multiple function arguments.

AC3: Execute nested tuples and reject assignment/destructuring mismatches; keep tuple contents immutable.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No mutable tuple elements or record-field positional shortcuts.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

Unit () remains distinct from nonempty tuples. Bare tuples are not valid queue message roots.

## LANG-20: Implement immutable List values and core operations

**Planning status:** Candidate; dependencies first.

### Goal

Create and transform immutable ordered collections.

Dependencies: LANG-13, LANG-16, DEC-01. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Support typed/inferred list literals and the approved minimum map/filter/fold/length APIs.

AC2: Ensure updates/transforms create new values; never expose a mutable array/list or shared writable backing storage.

AC3: Check element and callback types, execute order/empty-list tests and reject mutation methods.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No async parallel mapping or mutable builders.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

Freeze exact collection API names and index/out-of-range behavior in the issue before dispatch, using DEC-01/DEC-02 where relevant. List means immutable in D#.

## LANG-21: Implement immutable Map and Set values

**Planning status:** Candidate; dependencies first.

### Goal

Add immutable lookup and membership collections.

Dependencies: LANG-20, DEC-01. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Freeze and implement construction, persistent updates, lookup/membership and count APIs with explicit duplicate-key rules.

AC2: Define eligible equality/hash keys and ensure immutable nested values cannot expose mutation.

AC3: Test missing keys, duplicate entries, structural key behavior and original-value preservation.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No MutableMap, sorted collections or arbitrary foreign keys.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

Exact key/equality and failure APIs must be approved before implementation. Read-only wrappers over externally mutable storage are insufficient.

## LANG-22: Implement sequential for/in iteration

**Planning status:** Candidate; dependencies first.

### Goal

Visit immutable collection elements in order with a Unit-valued body.

Dependencies: LANG-20, LANG-07. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Scope the element binding to each iteration and require a Unit body/result.

AC2: Execute each iteration sequentially, including awaits once async exists; test empty and ordered inputs.

AC3: Reject incompatible iteration sources and non-Unit bodies.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No while, break, continue or parallel iteration API.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

No hidden parallelism. Early return exits the containing function; collection contents remain immutable.

## LANG-23: Implement method-local mutable builders and escape checks

**Planning status:** Candidate; dependencies first.

### Goal

Allow efficient local collection construction without exporting mutable state.

Dependencies: LANG-21, LANG-17, LANG-22. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Support MutableList and MutableMap recognized construction/mutation/freeze operations, with Unit-returning mutations.

AC2: Reject builders in fields, parameters, returns, wrappers, containers, aliases that escape, foreign calls and lambda captures including immediately invoked lambdas.

AC3: Freeze into an independent immutable snapshot; later builder mutations cannot change it.

AC4: Add adversarial nested/capture tests and positive local aliases/operations consistent with one creating invocation.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No mutable fields, helper-call borrowing or borrowed captures.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

Only recognized builder operations may receive a builder. Do not require a whole ownership language or permit borrowed builder parameters. If this slice exceeds one run, split runtime operations from escape checking without exposing unsafe builders.

## LANG-24: Implement async functions, Task and await

**Planning status:** Candidate; dependencies first.

### Goal

Lower explicit async functions to correct .NET asynchronous completion.

Dependencies: LANG-15, LANG-03, DEC-02, LANG-23. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Apply approved async signatures and Task<T> rules; reject await in disallowed contexts.

AC2: Preserve typed results, evaluation order and locals across suspension; execute delayed and failing asynchronous fixtures.

AC3: Allow a builder to survive an await only within its owning invocation and retain all capture/escape restrictions.

AC4: Ensure ordinary call sites receive a task and must explicitly await it.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No worker scheduling, source-level catch syntax or implicit task flattening beyond the approved rules.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

Use DEC-02 spelling. Async does not imply parallel iteration or retry. Compiler async tests may use controlled generated-C# host fixtures.

## LANG-25: Implement async lambdas

**Planning status:** Candidate; dependencies first.

### Goal

Create asynchronous function values with checked captures and result types.

Dependencies: LANG-24, LANG-17. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Support approved async lambda grammar and expected Task result types.

AC2: Keep return and postfix ? local to the lambda; retain all builder/resource capture restrictions.

AC3: Execute awaited closures and show ordinary map with an async callback returns tasks where its types permit.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No parallel map implementation or durable lambda payloads.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

Async collection callbacks are not implicitly awaited. Bounded async iteration needs an explicit separate API.

## LANG-26: Implement interfaces and explicitly implemented services

**Planning status:** Candidate; dependencies first.

### Goal

Define service contracts, explicit constructor dependencies and checked implementations.

Dependencies: LANG-13, LANG-24. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Support interface signatures and services implementing one or more interfaces without implementation inheritance.

AC2: Check required methods, duplicate/conflicting signatures and inferred implementation signatures against contracts.

AC3: Type-check constructor dependencies and direct service calls; execute polymorphic substitution with two services.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No default method bodies or application registration in this slice.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

Ordinary service calls are ordinary calls, not queued or supervised invocations. All methods are public; no automatic DI discovery.

## LANG-27: Implement default interface method bodies

**Planning status:** Candidate; dependencies first.

### Goal

Use per-method default implementations when a selected service omits those methods.

Dependencies: LANG-26. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Allow signatures with and without bodies; omitted bodyless methods remain errors.

AC2: Dispatch calls from default bodies to the selected service implementation and prefer a matching explicit service method.

AC3: Require an explicit implementation for conflicting defaults across interfaces; execute sync and async examples.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No interface state or class/service inheritance.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

The body defines the default. No default service selection, override keyword or visibility modifiers.

## LANG-28: Implement scoped using and resource non-escape

**Planning status:** Candidate; dependencies first.

### Goal

Enforce bounded resource lifetime and deterministic disposal.

Dependencies: LANG-24, LANG-26, DEC-07. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Support approved using syntax for synchronous/asynchronous resources and dispose on success, early return, typed failure and fault/cancellation.

AC2: Reject resource escape through return, fields, containers, capture, tasks or agent messages using approved ownership rules.

AC3: Verify exactly-once disposal and defined behavior when both body and cleanup fail.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No general ownership borrowing or implicit database transaction.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

Scoped handles cannot outlive their operation. Disposal does not undo external effects or imply transaction rollback.

## LANG-29: Implement multi-file modules and imports

**Planning status:** Candidate; dependencies first.

### Goal

Compile multiple source files with deterministic module/name resolution.

Dependencies: LANG-03, DEC-02. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Allow files to contribute to one module; resolve explicit module imports and qualified public names.

AC2: Reject duplicate/ambiguous/unresolved declarations and report the originating file/span.

AC3: Extend the CLI/source-input API compatibly and execute a cross-file program; preserve single-file usage.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No package manifest, aliases or NuGet resolution.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

Use DEC-02 lookup rules. Imports only affect names and never discover or register handlers.

## LANG-30: Implement statically resolved extensions

**Planning status:** Candidate; dependencies first.

### Goal

Add public functions through extend without adding state or inheritance.

Dependencies: LANG-29, LANG-26. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Support the approved self receiver syntax and type-check receiver/arguments/results.

AC2: Apply DEC-02 conflict and lookup rules; report ambiguous extensions before emission.

AC3: Execute extension calls across imports and reject added fields or implementation inheritance.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No runtime extension registry or overload rules beyond the approved subset.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

Extensions are ordinary statically resolved functions. No dynamic mutation of foreign or declared types.

## LANG-31: Implement integer widths and extended integer literals

**Planning status:** Candidate; dependencies first.

### Goal

Add the approved integer types and literal forms beyond int.

Dependencies: LANG-02, DEC-01. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Implement byte/sbyte/short/ushort/uint/long/ulong plus approved contextual literal typing, separators, bases and suffixes.

AC2: Test every boundary including minimum signed literals, out-of-range values and checked arithmetic.

AC3: Reject implicit mixed-variable numeric operations and expose only approved explicit conversion behavior.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No binary floating point, decimal or unchecked arithmetic syntax.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

DEC-01 freezes the complete width/conversion matrix; bootstrap int behavior must remain compatible except explicitly approved corrections.

## LANG-32: Implement float32, float and decimal

**Planning status:** Candidate; dependencies first.

### Goal

Support approved fractional numeric representations and conversions.

Dependencies: LANG-31, DEC-01. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Implement fractional literals/suffixes and the approved type names with culture-independent parsing/emission.

AC2: Keep binary and decimal operations distinct; require explicit mixed-type conversion.

AC3: Execute range, rounding, NaN/infinity and decimal-overflow cases according to DEC-01.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No currency type or implicit decimal/binary mixing.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

float naming and IEEE/decimal edge semantics must be approved first. Monetary rounding remains application logic.

## LANG-33: Implement string interpolation

**Planning status:** Candidate; dependencies first.

### Goal

Emit interpolated strings with correct escaping and evaluation order.

Dependencies: LANG-06, DEC-01. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Support documented interpolation and escaped braces with checked embedded expressions.

AC2: Evaluate each expression once and use the approved deterministic formatting rules.

AC3: Test Unicode, nested expressions, literal braces/quotes and malformed input with source diagnostics.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No raw strings or a full formatting mini-language.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

Formatting supported values must be explicit in DEC-01 or this approved issue; do not fall back to arbitrary unsafe foreign ToString behavior.

## LANG-34: Implement worker declaration contracts

**Planning status:** Candidate; dependencies first.

### Goal

Resolve callable worker definitions into a shared semantic contract.

Dependencies: LANG-26, LANG-14, LANG-24, DEC-03. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Require one typed handle returning Result and validate dependencies, helper members and supported worker policies.

AC2: Reject queue/event/cron/HTTP triggers on workers and missing or duplicate handlers.

AC3: Expose checked input/success/error/dependency/source metadata to generation and tooling; require a result on every normal path.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No queue declaration or functional worker-runtime claim.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

Workers may await inside handle without an extra async marker per the approved contract. This issue validates declarations; runtime calls arrive in RUN-01/02.

## LANG-35: Implement triggered-agent contracts and message restrictions

**Planning status:** Split / freeze first.

**Before dispatch:** Event: DEC-04. Cron: DEC-08 scheduling section. HTTP: DEC-05. Start with Queue; instantiate each trigger category as a separate issue.

### Goal

Validate trigger/handle relationships through one shared semantic model.

Dependencies: LANG-34, LANG-23, DEC-04. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Support one approved typed trigger per agent and infer its required handler input/result category; report missing members with expected signatures.

AC2: Enforce declared type/Unit queue roots and recursively serializable immutable payloads after generic substitution.

AC3: Reject queue handler business-value returns, worker-like calls, direct .handle access/method groups and capability casts.

AC4: Reject misspelled/duplicate reserved members; do not guess the application error type.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No actual transport execution, package discovery or invented default error type.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

Instantiate this template separately for Queue first, then Event/Cron/HTTP as their decision gates close. Non-queue gates need not block the Queue instance.

## LANG-36: Implement typed execution-policy declarations

**Planning status:** Candidate; dependencies first.

### Goal

Check supported policies and retry variants against each resolved agent kind.

Dependencies: LANG-35, DEC-04. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Validate supported policy fields/types and reject unknown fields or policies belonging to another declaration kind.

AC2: Resolve retry variants against the handler's error union; maxAttempts includes the first attempt.

AC3: Preserve static policy metadata plus runtime/store capability requirements for startup validation.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No silent policy weakening or runtime scheduling.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

Only approved policy fields and defaults are accepted. Runtime faults have separate policy from application Error variants.

## LANG-37: Implement code-based application composition

**Planning status:** Candidate; dependencies first.

### Goal

Generate typed explicit registration and dependency recipes from app declarations.

Dependencies: LANG-29, LANG-35, LANG-36, DEC-07. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Support approved bind, workers.register, api.register and agents.register forms using definition references.

AC2: Reject statically known wrong kinds, missing/duplicate bindings and duplicate identities/routes where decidable.

AC3: Emit metadata/recipes without executing handlers or acquiring resources; retain dynamic checks for host startup.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No application YAML, global service locator or runtime initialization.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

Registration is explicit and all-public. Application code is the source of wiring; imports and installed packages do not enable handlers.

## LANG-38: Implement block comments and attached documentation comments

**Planning status:** Candidate; dependencies first.

### Goal

Accept documented comment forms while preserving accurate source spans and declaration documentation.

Dependencies: DEC-02. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Support non-nested /* ... */ block comments and report an unterminated comment at its opening span.

AC2: Attach /// documentation to the next eligible declaration according to DEC-02 without changing execution.

AC3: Preserve line/column locations through comments and multiline expressions; test that comment-like text inside strings remains literal.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No documentation website generator, formatter or semantic editor features.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

Single-line // comments already exist. Block-comment nesting is not supported; no comment form changes lexical scope.

## Worker execution and durable messaging

## RUN-01: Execute isolated callable worker invocations

**Planning status:** Runtime/tooling setup required.

### Goal

Run a compiled worker through the approved runtime reference.

Dependencies: SET-02, LANG-34. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Create a fresh invocation and supply explicit dependencies; return the checked success/application error result.

AC2: Keep concurrent invocation locals isolated and release execution capacity across async waits without blocking worker threads.

AC3: Execute a generated D# worker from an integration host and substitute a fake service dependency.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No durability, network distribution or persistent actor state.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

Apply DEC-03. Registration/reference access is runtime-owned; direct handle bypass is rejected by the compiler.

## RUN-02: Handle worker faults, deadlines and cancellation

**Planning status:** Runtime/tooling setup required.

### Goal

Return the approved worker-call outcome for non-application failures.

Dependencies: RUN-01, DEC-03. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Map fault, timeout and cancellation according to DEC-03 while preserving normal Result errors.

AC2: Test late completions, cancellation races and nested/bounded calls using controlled time; avoid leaking detailed fault data through public payloads.

AC3: Prove an ordinary handler exception does not terminate unrelated invocations and that resources are released.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No queue retry policies or resilience against fatal CLR/native-process failure.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

Timeout does not imply rollback or guaranteed termination of external effects. Do not invent automatic retry.

## RUN-03: Start and stop explicitly registered applications

**Planning status:** Runtime/tooling setup required.

### Goal

Build the dependency graph and admit work only after required startup completes.

Dependencies: SET-02, LANG-37, DEC-07. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Reject dynamic duplicate identities/routes, missing bindings, cycles and unsupported store policies before admission.

AC2: Initialize independent ready dependencies concurrently and roll back acquired resources after failed startup.

AC3: Dispatch registered handlers only; release resources in dependency order at shutdown.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No lazy initialization, secret rotation or automatic handler discovery.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

Metadata inspection and registration do not execute business code. Host startup is asynchronous.

## RUN-04: Implement RuntimeStore contracts and conformance harness

**Planning status:** Runtime/tooling setup required.

### Goal

Make atomic persistence operations and provider guarantees executable contracts.

Dependencies: SET-02, DEC-04. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Define the approved atomic operations/capability descriptor and implement a deterministic in-memory reference store for tests.

AC2: Add reusable conformance scenarios for transitions, identity, attempts, stale owners and atomic boundaries.

AC3: Reject unsupported guarantees explicitly; record each operation's transactional boundary.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No SQLite provider or distributed coordination claim.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

Store operations are domain operations, not generic get/save. The in-memory store makes no durability promise.

## RUN-05: Implement SQLite runtime schema and migrations

**Planning status:** Runtime/tooling setup required.

### Goal

Create and upgrade one runtime database file per application.

Dependencies: RUN-04. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Implement versioned schema and transaction helpers for approved runtime entities; record stable identities separately from source symbol names.

AC2: Test fresh creation, compatible upgrade, failed upgrade rollback and rejection of newer unsupported schema.

AC3: Support persistent local storage with clear application isolation and approved SQLite settings.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No per-queue files by default, claims or multi-host SQLite coordination.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

SQLite is the initial single-host provider. Exact dependency/version and project permissions come from SET-02.

## RUN-06: Implement durable enqueue and acceptance receipts

**Planning status:** Runtime/tooling setup required.

### Goal

Accept typed queue data only after persistence succeeds.

Dependencies: RUN-05, LIB-01, LANG-35. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Validate/encode the approved message contract/version and atomically persist identity, payload and eligibility.

AC2: Return the approved acceptance receipt after commit; distinguish rejection from accepted handler work.

AC3: Test restart persistence, stable deduplication semantics and a crash before/after commit; no receipt exposes handler completion.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No executing handlers, retries or exactly-once external effects.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

Apply DEC-04 acceptance semantics. Enqueue may be awaited for acceptance; queue execution remains unawaitable.

## RUN-07: Implement atomic claims and expiring ownership

**Planning status:** Runtime/tooling setup required.

### Goal

Claim eligible work with recoverable attempt tokens.

Dependencies: RUN-06. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Atomically select and claim eligible work with lease expiry and a fresh attempt token under competing claimers.

AC2: Renew valid ownership and reclaim expired claims; reject stale renewals.

AC3: Run handlers outside claim transactions; use crash/restart and contention tests with controlled time.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No handler completion or ordering policy beyond approved eligibility.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

Ownership expiry can cause duplicate execution. Claims are bounded database transactions, not locks held across handler awaits.

## RUN-08: Implement completion and stale-ack rejection

**Planning status:** Runtime/tooling setup required.

### Goal

Commit approved attempt outcomes only for the current owner.

Dependencies: RUN-07. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Atomically complete, fail or reschedule an owned attempt under DEC-04 transitions.

AC2: Reject late/stale completion after lease takeover without changing the current attempt.

AC3: Test crashes around commit and ensure terminal work is not normally reclaimed; record outcome evidence.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No retry policy calculation, fan-out or business-database transaction.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

Token validation is required on every ownership-sensitive mutation. Rescheduling releases the active execution slot.

## RUN-09: Connect queue execution to typed handler outcomes

**Planning status:** Runtime/tooling setup required.

### Goal

Execute persisted messages in fresh invocations and report outcomes to the supervisor.

Dependencies: RUN-08, RUN-01, LANG-36. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Decode and dispatch the correct registered handler with its dependencies; apply approved Result<Unit,E>/fault outcome mapping.

AC2: Complete through ownership-checked store operations and release resources/capacity on every path.

AC3: Run an enqueue-restart-execute integration case and a poisoned-payload case; isolate one failing handler from other work.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No automatic retry until RUN-10; no shared per-message mutable state.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

Business outcomes go to the supervisor, never to the enqueuing caller. Runtime faults are distinct from application errors.

## RUN-10: Implement retries, delays and retained failures

**Planning status:** Runtime/tooling setup required.

### Goal

Apply declared retry rules without sleeping inside handler instances.

Dependencies: RUN-09. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Retry only eligible errors/faults using approved attempts, backoff and jitter; maxAttempts includes attempt one.

AC2: Persist next eligibility and retain exhausted/permanent failures with diagnostic references.

AC3: Use deterministic clock/random tests for attempts, delay, restart and terminal outcomes; release execution capacity between attempts.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No observer UI, manual retry controls or ordering changes.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

Policies come from DEC-04/LANG-36. Retry does not undo effects and cannot bypass ownership tokens.

## RUN-11: Implement per-key sequential ordering and concurrency

**Planning status:** Runtime/tooling setup required.

### Goal

Preserve completion ordering within a key while other keys can progress.

Dependencies: RUN-10. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Enforce configured queue/key/host concurrency within provider-supported scope.

AC2: Keep a failed or delayed head blocking its key across retries until completion or approved skip/abandonment.

AC3: Test competing claimers, several keys, recovery and concurrency counts; reject deployment-wide guarantees the provider cannot supply.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No general distributed worker routing or hidden weakening of requested policy.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

FIFO claim order alone is insufficient. SQLite starts with single-host guarantees.

## RUN-12: Implement queue capacity and cooperative cancellation

**Planning status:** Runtime/tooling setup required.

### Goal

Bound pending/active work and apply approved cancellation semantics.

Dependencies: RUN-11. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Enforce approved capacity behavior atomically and distinguish wait/reject/timeout outcomes.

AC2: Cancel pending work and signal running work according to policy without accepting stale completions.

AC3: Test overloaded producers, cancellation races and shutdown interaction; bound waiter/resource growth.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No user-defined queue priorities or implicit cancellation of unrelated messages.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

Cancellation is cooperative where execution is already running and does not roll back completed effects.

## RUN-13: Implement atomic completion with follow-up enqueue

**Planning status:** Runtime/tooling setup required.

### Goal

Commit a completed attempt and its same-store follow-up messages together.

Dependencies: RUN-08. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Expose the approved atomic store operation with message eligibility/identity checks.

AC2: Test rollback if any follow-up insert fails and crash recovery around the commit.

AC3: Require valid attempt ownership and prevent stale attempts from publishing follow-ups.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No cross-database atomicity or automatic business-data outbox.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

Atomicity applies within one RuntimeStore transaction. Separate business databases need an outbox protocol.

## RUN-14: Implement recoverable event fan-out

**Planning status:** Runtime/tooling setup required.

### Goal

Deliver one independent queued item per registered subscription.

Dependencies: RUN-09, RUN-13, LANG-35. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Persist publication and delivery progress so a crash midway through fan-out does not silently omit subscriptions.

AC2: Give each subscription independent outcomes/retries; workers on one subscription compete for the same delivery.

AC3: Test zero/multiple subscriptions, restart, stable subscription identity and one failed subscription.

AC4: If approved filters/mappings are included, enforce deterministic data-only behavior and test their failure contract.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No cross-store exactly-once publication or wildcard dynamic discovery.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

Use DEC-04 fan-out transaction/recovery rules. Event payloads obey queue data restrictions.

## RUN-15: Implement durable cron occurrence creation

**Planning status:** Runtime/tooling setup required.

### Goal

Persist schedules and atomically enqueue their due occurrences.

Dependencies: RUN-13, DEC-08, LIB-02. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Apply the approved cron dialect/timezone/DST semantics and stable occurrence identities.

AC2: Advance schedule progress and create queued occurrences atomically.

AC3: Test restart at each boundary and Skip/RunOnce/bounded CatchUp after downtime; retries use normal queue machinery.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No long-lived sleeping handler or live-socket scheduling.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

The approved scheduling section of DEC-08 is required; its WebSocket section is unrelated. Use a controllable clock.

## RUN-16: Implement queue inspection and operator controls

**Planning status:** Runtime/tooling setup required.

### Goal

Expose checked inspection, pause/resume and failure retry/abandonment operations.

Dependencies: RUN-12, DEC-05. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Return typed paginated queue/attempt/failure views without leaking secrets or unbounded payloads.

AC2: Implement authorized control operations with concurrency/token checks and auditable outcomes.

AC3: Test pause races, retry of retained failures, cancellation and ordering consequences of explicit abandon/skip.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No dashboard UI or unauthenticated administrative endpoints.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

Operator APIs need approved access-control and transition rules from DEC-04/DEC-05. Manual retry still has at-least-once effects.

## RUN-17: Implement PostgreSQL RuntimeStore provider

**Planning status:** Split / freeze first.

**Before dispatch:** Split into schema/migrations, enqueue/claims, transitions, and multi-host conformance before implementation; preserve the frozen RuntimeStore contract.

### Goal

Provide the approved shared-store guarantees on PostgreSQL.

Dependencies: SET-03, RUN-04, RUN-16. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Implement schema/migrations and the frozen atomic operations without generic read-modify-write races.

AC2: Pass provider conformance tests plus multi-host claim, stale-token, ordering and global concurrency tests.

AC3: Document deployment capability limits, permissions, retention and recovery procedures.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No generic ORM layer or automatic business database ownership.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

Instantiate provider-specific SET-03 first. Split implementation into migrations, claims, transitions and conformance child issues before automation if needed.

## RUN-18: Implement MySQL RuntimeStore provider

**Planning status:** Split / freeze first.

**Before dispatch:** Split into schema/migrations, enqueue/claims, transitions, and multi-host conformance before implementation; preserve the frozen RuntimeStore contract.

### Goal

Provide the approved shared-store guarantees on MySQL.

Dependencies: SET-03, RUN-04, RUN-16. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Implement schema/migrations and atomic operations with verified locking/isolation behavior.

AC2: Pass the same provider conformance suite and multi-host contention/recovery tests.

AC3: Reject unsupported policies and document permissions, version requirements and operational recovery.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No weakened semantics to match another provider or business-data ORM.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

Instantiate provider-specific SET-03 first. Split by migration/claim/transition slices before dispatch if the implementation exceeds one run.

## Configuration and resources

## APP-01: Implement typed configuration loading

**Planning status:** Runtime/tooling setup required.

### Goal

Read external configuration into validated immutable values before startup admission.

Dependencies: RUN-03, LIB-01, DEC-07. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Apply approved defaults/JSON/environment precedence deterministically and bind typed values.

AC2: Report missing/invalid values with paths while redacting designated secrets.

AC3: Test competing sources, malformed values and failed startup; keep wiring in code.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No app YAML or live mutation of configuration records.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

DEC-07 freezes source APIs and precedence. Configuration is immutable after resolution.

## APP-02: Implement asynchronous secret-provider initialization

**Planning status:** Runtime/tooling setup required.

### Goal

Resolve secrets before dependent resources accept work.

Dependencies: APP-01, DEC-07. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Use the approved provider interface and independent bootstrap credentials/host identity.

AC2: Initialize dependent clients/pools only after required secret resolution; handle failure and cleanup.

AC3: Use a fake provider to test concurrency, failure and redaction from logs/diagnostics.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No provider-specific cloud integration or rotation protocol beyond the approved basic contract.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

Secret values are runtime inputs, not checked-in source or normal diagnostic payloads.

## APP-03: Implement lazy resource initialization and replacement

**Planning status:** Runtime/tooling setup required.

### Goal

Coordinate shared lazy startup and approved resource refresh.

Dependencies: APP-02. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Allow one initialization attempt for concurrent first users and define retry after failure.

AC2: Refresh/replace resources through DEC-07 ownership rules while in-flight users finish safely.

AC3: Test competing callers, failed replacement, disposal and shutdown races with deterministic barriers.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No warm actor caches or automatic secret polling without explicit configuration.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

Refreshing a resource does not mutate an already issued immutable configuration value.

## APP-04: Implement scoped database and filesystem capabilities

**Planning status:** Split / freeze first.

**Before dispatch:** The database and filesystem slices are separate issues. Choose a concrete driver only for the database instance.

### Goal

Provide safe resource adapters for a selected database driver and filesystem scope.

Dependencies: SET-03, LANG-28, APP-02, DEC-06. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: For the database slice, share a pool, borrow a connection per operation and define explicit transaction commit/rollback with disposal.

AC2: For the filesystem slice, restrict permitted paths/operations and scope streams; distinguish partial writes from atomic replacement.

AC3: Reject handle escape and test cleanup/failure/concurrency behavior through generated D# calls.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No universal ORM, permanent per-actor connection or rollback of arbitrary external effects.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

Instantiate database and filesystem as separate issues using approved adapters and dependencies. Shared external state still requires database/file coordination.

## Standard backend library

## LIB-01: Implement typed JSON encoding and decoding

**Planning status:** Runtime/tooling setup required.

### Goal

Encode immutable data and decode it with structured typed errors.

Dependencies: SET-02, LANG-21, LANG-11, LANG-14, DEC-04. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Support the approved primitive/nullable/record/union/collection encodings and stable contract/version information where durable messaging needs it.

AC2: Handle missing nullable fields, required-field absence, explicit null, unknown fields and invalid values according to a frozen encoding contract.

AC3: Round-trip nested eligible data and reject functions, tasks, resources and mutable foreign objects.

AC4: Report structured path-aware errors without leaking secret values or executing application constructors with side effects.

AC5: Option in #121 does not itself define a JSON or HTTP representation. Decide
wire encoding explicitly in the applicable contract and preserve `None` versus
`Some(null)` where options are supported. Logger fields require explicit mapping;
logging does not adopt a core Option wire schema. Preserve large integer/decimal
precision and report provider limitations rather than silently rounding.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No arbitrary polymorphic CLR deserialization or unsupported formats.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

DEC-04 freezes durable encoding/evolution rules. Preserve public construction and validation distinctions.

## LIB-02: Implement standard immutable time, identity and binary values

**Planning status:** Runtime/tooling setup required.

### Goal

Expose approved uuid/time/Duration/Bytes values and minimal operations.

Dependencies: SET-02, DEC-01, LIB-01. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Implement representations and explicit parse/format/convert APIs selected by DEC-01.

AC2: Provide immutable Bytes ownership with safe copying at mutable-array boundaries.

AC3: Test Unicode-independent encodings, time/zone distinctions, round trips and invalid inputs.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No generic DateTime primitive, business calendar or mutable byte buffers.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

Instant differs from LocalDate/LocalTime; Duration is elapsed time. Split individual value families if the approved API exceeds one run.

## LIB-03: Design and implement an outbound HTTP client adapter

**Planning status:** Split / freeze first.

**Before dispatch:** Design first; then instantiate a minimal implementation issue. Circuit breakers remain a separate design follow-up.

### Goal

Create a bounded typed outbound HTTP integration through an explicitly approved adapter contract.

Dependencies: SET-03, DEC-06, DEC-07, LANG-28. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: First freeze connection reuse, response/resource ownership, deadline/cancellation and error mapping in a design-only issue.

AC2: Implement a minimal adapter with fake-server tests for success, failure, cancellation and disposal.

AC3: Keep retries opt-in and constrained by operation/idempotency semantics; expose a later separate circuit-breaker proposal.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No automatic unsafe-method retry, general streaming or universal resilience framework.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

This backend area is not yet designed. Create the design child first; only then approve the implementation child.

## HTTP, middleware and WebSockets

## HTTP-01: Host explicitly registered HTTP agents

**Planning status:** Runtime/tooling setup required.

### Goal

Route method/path requests to fresh typed handler invocations.

Dependencies: SET-03, RUN-03, LANG-35, DEC-05. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Register approved HTTP definitions and reject conflicting dynamic routes before accepting connections.

AC2: Dispatch concurrent requests without global serialization and map the approved handler outcomes to responses.

AC3: Run an in-process host test with two routes, concurrent requests, a failing handler and cancellation.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No middleware framework, automatic controllers or queue retry on requests.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

HTTP is optional and registration is explicit. Transport-specific SET-03 must freeze the ASP.NET integration/dependencies.

## HTTP-02: Implement typed path, query and JSON body binding

**Planning status:** Runtime/tooling setup required.

### Goal

Transform transport inputs into the declared immutable request type.

Dependencies: HTTP-01, LIB-01. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Decode path/query/body using DEC-05 rules with structured errors and explicit missing/null behavior.

AC2: Preserve previously established identity/context in the resulting envelope.

AC3: Test invalid/oversized inputs, route parameter mismatches and response encoding without invoking business handlers on decode failure.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No arbitrary request property bags or unapproved custom serialization.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

Typed envelope shape and boundary error/status mappings come from DEC-05.

## HTTP-03: Implement typed asynchronous transformation pipelines

**Planning status:** Runtime/tooling setup required.

### Goal

Bind HTTP preprocessing to an ordinary typed callable with `pipeline = expression`, using normal pipes and the LIB-04 Result helpers.

Dependencies: HTTP-02/#82, LANG-16/#33, LANG-26/#43, LANG-41 and LIB-04, plus the DEC-05 transport/error contract and the relevant HTTP setup child.

### Acceptance criteria

AC1: Check callable input, successful output against the exact handle input, and explicit HTTP error mappings; reject legacy `pipeline {}` composition.

AC2: Invoke preprocessing once and call handle only on success. Use ordinary synchronous/asynchronous Result helpers; absent preprocessing lets binding supply the handler input directly.

AC3: Prove identity/context survives decoding and that incompatible inputs or erased errors are compile errors.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No general pipeline block, result-aware pipe operator, wrapping continuations or implicit error-to-string conversion. Wrapping middleware remains HTTP-04.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

The contextual pipeline member is a callable-valued declaration slot, not a separate composition language. See [the target specification](DSharp_Pipes_and_Logging.md#5-services-workers-and-http). Runtime authorization and DEC-05 transport contracts remain separate requirements.

## HTTP-04: Implement wrapping middleware and cleanup

**Planning status:** Runtime/tooling setup required.

### Goal

Run tracing/resource middleware around downstream execution.

Dependencies: HTTP-03, LANG-28. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Implement the approved wrapper/continuation contract and declared nesting order.

AC2: Clean up on success, typed error, fault and cancellation; define repeated or omitted continuation behavior.

AC3: Test nested wrappers and early failures without duplicate handler invocation or leaked resources.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No implicit database rollback or user-defined scheduler.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

DEC-05 defines continuation/lifetime semantics. Transforming input alone does not implement wrapping.

## HTTP-05: Implement runtime authentication and authorization checks

**Planning status:** Runtime/tooling setup required.

### Goal

Supply verified identity and enforce policy for protected handler operations.

Dependencies: HTTP-04, DEC-05. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Implement the approved provider/policy interfaces with a fake identity provider and explicit permission/resource checks.

AC2: Require compatible typed contexts in the pipeline while independently checking runtime validity where sensitive operations occur.

AC3: Test missing/invalid identity, denied permission, resource scope and fabricated public Authorized-like data.

AC4: Specify independent policies for queue/scheduled entry points rather than treating persisted HTTP identity as permanent authorization.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No hardcoded identity vendor or authorization based only on record construction.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

Type shape alone is insufficient proof. Provider integrations such as a concrete JWT/OIDC adapter require separately approved issues.

## HTTP-06: Implement typed WebSocket connection handling

**Planning status:** Split / freeze first.

**Before dispatch:** Approve DEC-08's socket section first. Split connection lifecycle and bounded output buffering before dispatch.

### Goal

Handle typed socket messages with bounded ordered outbound writes.

Dependencies: HTTP-01, DEC-08. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Implement explicit connect/message/disconnect lifecycle and isolated per-message handler execution.

AC2: Enforce approved buffer limits/order/cancellation and clean up on disconnect or host shutdown.

AC3: Test slow clients, concurrent sends, malformed messages and reconnection behavior against the documented protocol.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No claim that durable notifications preserve sockets or automatically replay missed messages.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

Requires the approved WebSocket section of DEC-08 and its exact transport test setup. Split lifecycle and buffering slices if needed.

## .NET and F# interoperability

## INT-01: Implement explicit NuGet reference and package resolution

**Planning status:** Runtime/tooling setup required.

### Goal

Resolve approved external package references reproducibly for generated C# builds.

Dependencies: SET-03, LANG-29, DEC-06. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Implement the frozen package/reference input with exact restore/lock behavior and deterministic compile diagnostics.

AC2: Check target-framework compatibility and reject unsupported integration requirements explicitly.

AC3: Build a pinned pure-managed fixture package without accepting arbitrary source generators or native loading.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No blanket NuGet compatibility or full package ecosystem.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

DEC-06 defines supported package/build syntax and limits; current compiler automation cannot edit dependency manifests.

## INT-02: Implement the safe C# value and method adapter subset

**Planning status:** Runtime/tooling setup required.

### Goal

Call approved CLR constructors/methods/properties while preserving D# type safety.

Dependencies: INT-01, LANG-26, LANG-28. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Map eligible types/generics and expose only the frozen safe API subset.

AC2: Copy or wrap mutable foreign data under approved ownership rules; reject builders/resources at forbidden boundaries.

AC3: Test foreign nulls, incompatible signatures, mutation aliasing and immutable D# results.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No unrestricted reflection or arbitrary foreign-object data fields.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

Use DEC-06 adapters; successful CLR compilation alone is not proof of D# immutability.

## INT-03: Implement CLR async, error and delegate adapters

**Planning status:** Runtime/tooling setup required.

### Goal

Integrate tasks, cancellation, disposal and callbacks through the safe boundary.

Dependencies: INT-02, LANG-25. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Apply explicit null/exception-to-error mapping and preserve cancellation without losing fault diagnostics.

AC2: Adapt eligible delegates with checked capture lifetime; block builder/resource escape through callbacks.

AC3: Test Task completion/failure, synchronous foreign throws, late callbacks and deterministic disposal.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No arbitrary unmanaged callbacks or implicit retry.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

Interop must obey DEC-06 and D# resource rules, not silently weaken them.

## INT-04: Implement the initial F# NuGet adapter subset

**Planning status:** Runtime/tooling setup required.

### Goal

Consume selected F# APIs through explicit D#-safe adapters.

Dependencies: INT-03. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Use a pinned F# fixture exposing supported options, unions, function values and asynchronous computations.

AC2: Map optional values and outcomes through the approved D# interop contract, preserving distinctions such as None versus Some(null) where applicable. Exact F# mappings remain undecided.

AC3: Test null/exception/cancellation, union cases and returned functions; document unsupported F# surfaces.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No automatic support for every F# feature or computation expression.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

Start with DEC-06's explicitly selected subset. Package implementation language does not guarantee automatic compatibility.

## Operations and application testing

## OPS-01: Implement correlated structured logs, metrics and tracing

**Planning status:** Split / freeze first.

**Before dispatch:** Reuse LOG-01/02 and approve the remaining metric/trace contracts. Instantiate host/request instrumentation and queue correlation/export separately if needed.

### Goal

Connect request, worker and queue attempts through approved diagnostic context, reusing the standard Logger rather than building another logging stack.

Dependencies: SET-03/#10, RUN-09/#64, HTTP-01/#81 and LOG-02.

### Acceptance criteria

AC1: Reuse LOG-01's LogEvent and scope contract; freeze remaining metric/trace schema, cardinality and redaction rules without redefining Logger fields or delivery.

AC2: Emit lifecycle/outcome/duration events and queue depth/retry metrics without serializing secrets or full message payloads by default.

AC3: Test propagation across async/worker/enqueue boundaries and isolate exporter failure from business handling. Logger/output-internal diagnostics bypass the failed delivery path and are rate-limited.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No unbounded metric labels or arbitrary sensitive-payload logging.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

Logger is the host-scoped, bounded transient system actor from LOG-02; console is the default and database/cloud outputs are opt-in. Metrics/tracing and boundary instrumentation reuse it. LOG-03 owns database delivery, and LOG-04 adds search after the core observer. Do not make OPS-02 depend on LOG-04 and create a dependency cycle.

## OPS-02: Implement protected runtime observer dashboard

**Planning status:** Split / freeze first.

**Before dispatch:** Approve the API/page contract first. Split read-only views from operator controls before dispatch.

### Goal

Show queues, retained failures, schedules and execution diagnostics with controlled actions.

Dependencies: RUN-16, HTTP-05, OPS-01. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: First approve a small page/API contract; provide paginated views backed by typed inspection endpoints.

AC2: Protect access and distinguish read permissions from retry/pause/cancel permissions.

AC3: Test authentication, action authorization, stale views, empty states and unavailable stores; audit mutations.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No public administrative UI or unrelated business-data dashboard.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

Use RUN-16 operations; never modify persisted state through ad hoc dashboard database writes.

## OPS-03: Implement health checks and bounded graceful draining

**Planning status:** Runtime/tooling setup required.

### Goal

Report readiness/liveness and stop accepting work before shutdown deadlines.

Dependencies: RUN-03, RUN-12, HTTP-01. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Derive readiness from required initialized dependencies and admission state; keep liveness semantics distinct.

AC2: Stop new HTTP/queue admission, drain active work within the approved deadline and release leases/resources safely.

AC3: Test rolling restart, stuck handler, failed dependency and forced deadline without falsely completing work.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No deployment-platform provisioning.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

Shutdown cannot guarantee rollback or termination of arbitrary foreign code. Freeze exact health/status contract before dispatch.

## OPS-04: Provide handler and application testing harnesses

**Planning status:** Split / freeze first.

**Before dispatch:** Approve the application-facing test API first. Split worker/queue helpers from HTTP helpers.

### Goal

Let application authors test handlers and composed apps with fake services and time.

Dependencies: SET-03, RUN-09, HTTP-03. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Expose approved test host helpers for dependency replacement, worker invocation and queue/HTTP scenarios.

AC2: Provide deterministic clocks/randomness and captured outcomes without allowing production dispatch bypass.

AC3: Ship one D# application test example covering success, failure, retry and cancellation.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No alternative production runtime or mocks that redefine queue acceptance.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

Define the public test API in a small design child first; reuse runtime conformance primitives rather than duplicate their semantics.

## OPS-05: Design business-data transactions and outbox integration

**Planning status:** Design only.

### Goal

Specify reliable publication when business data and runtime data use separate stores.

Dependencies: DEC-04, DEC-07, DEC-06. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Describe commit ownership, outbox storage, relay identity, deduplication and crash recovery with a concrete transaction example.

AC2: Define adapters/driver responsibilities and how migrations are owned by the application.

AC3: Create bounded follow-up implementation issues for one selected driver and relay after approval.

### Constraints

Write only `docs/features/implementation/OPS-05.md`. Preserve settled choices; label proposals. No implementation/dependency/baseline edits. Explicitly adopt the reviewed contract before dependent implementation.

### Non-goals

No automatic cross-database transaction or universal ORM.

### Verification

Run `bash .github/ai/bootstrap.sh` and `bash .github/ai/verify.sh`. Check examples/invariants against current specifications and link dependents. Passing documentation checks does not approve a contract or demonstrate runtime behavior.

### Decisions already made

Same-store follow-up atomicity does not span an arbitrary business database. External effects remain at least once.

## Compiler and editor tooling

## TOOL-01: Expose consistent compiler diagnostics and status

**Planning status:** Candidate; dependencies first.

### Goal

Make CLI and programmatic diagnostics describe missing/invalid contracts clearly.

Dependencies: LANG-35. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Use stable diagnostic codes, source ranges and expected signatures from the shared semantic model.

AC2: Distinguish unsupported draft syntax from malformed implemented syntax and avoid misleading raw generated-C# errors.

AC3: Add fixture coverage for missing handle/trigger, mismatched payloads, wrong invocation kind and misspelled policy members.

### Constraints

Allowed: `src/DSharp.Compiler/**/*.cs`, necessary `src/DSharp.Cli/**/*.cs`, new `tests/features/**/*.cs`, `examples/**/*.ds`, `docs/features/implementation/**/*.md`, and `docs/features/DSharp_Implementation_Status.md`. Preserve APIs, existing tests, SDK/dependencies and C# generation. No workflow/project/policy/verifier edits.

### Non-goals

No language-server transport or silently changing existing public diagnostics.

### Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`. Add positive/negative feature tests; execute generated C# for behavioral claims. Map ACs to evidence and update implementation status.

### Decisions already made

CLI diagnostics are useful before an editor exists. Do not invent an application error type in a suggested signature.

## TOOL-02: Implement language-server document diagnostics

**Planning status:** Runtime/tooling setup required.

### Goal

Publish compiler-backed diagnostics for open and changed documents.

Dependencies: SET-03, TOOL-01. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Create the approved protocol host/document lifecycle with cancellation and stale-result suppression.

AC2: Reuse compiler parsing/binding/contracts rather than a second semantic implementation.

AC3: Run protocol tests for edit, invalid/incomplete source, close and cross-file changes.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No completion, formatting or full IDE extension.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

Tooling-specific SET-03 freezes LSP package and protocol versions. Do not execute application/package startup code during analysis.

## TOOL-03: Implement semantic completion, hover and signature help

**Planning status:** Runtime/tooling setup required.

### Goal

Show valid members and inferred contracts while editing agents and lambdas.

Dependencies: TOOL-02, LANG-36, LANG-16. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Complete only applicable handle/trigger/pipeline/policy members and approved policy fields.

AC2: Show resolved input/result/error/invocation types on hover and parameters for lambdas/service/worker calls.

AC3: Provide go-to-definition and test incomplete declarations without executing metadata code.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No separate hard-coded agent signature tables.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

Use the same semantic contracts as the compiler. Before a trigger exists, do not guess a message or error type.

## TOOL-04: Implement required-members code actions

**Planning status:** Runtime/tooling setup required.

### Goal

Insert missing agent members without modifying existing business code.

Dependencies: TOOL-03. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Derive the handler signature from the selected trigger/pipeline and preserve existing members/imports/formatting where possible.

AC2: Leave an explicit editor placeholder when the application error type is unknown; do not claim unfinished code compiles.

AC3: Test edits for queue/cron/worker/HTTP declarations, duplicate action invocation and cancellation.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No automatic business logic or guessed error types.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

A generated skeleton is a starting point, not an implicit successful handler.

## TOOL-05: Generate application inventory from registration metadata

**Planning status:** Runtime/tooling setup required.

### Goal

Describe routes, queues, subscriptions, schedules and dependencies without executing handlers.

Dependencies: LANG-37, RUN-03. Convert these IDs to merged PR/issue links before dispatch.

### Acceptance criteria

AC1: Export an approved structured inventory plus readable documentation from resolved explicit registrations.

AC2: Include stable identities, typed contracts, policy scopes and dynamic startup-validation requirements.

AC3: Test deterministic output, disabled handlers, renamed symbols with stable IDs and redaction of configuration/secrets.

### Constraints

BLOCKED until the relevant SET-02/03 setup. Before approval, insert exact enabled source/test paths and pinned dependencies. Preserve existing tests, compiler behavior, invocation and immutability rules; no blanket workflow permissions.

### Non-goals

No infrastructure deployment or automatic handler discovery.

### Verification

Before dispatch, insert trusted subsystem commands from setup plus compiler regression checks. Test every AC, including failures; use controlled time/barriers. Durable changes require relevant crash/restart and stale-owner tests.

### Decisions already made

Derive the overview from code metadata; it is not a second YAML source of truth.

## Deferred and follow-up register

These are recorded so “every feature” does not silently expand the first implementation. Create a design issue only when promoting an item into scope.

| Area | Treatment |
| --- | --- |
| gRPC | Explicitly skipped at your request. No implementation issue now. |
| Persistent/warm actors and caches | Deferred decision; baseline is isolated invocations. |
| Distributed worker location/routing | Deferred protocol; shared database queue coordination does not imply remote worker RPC. |
| General async streams and parallel collection operators | Design before implementation: buffering, cancellation, lifetime and bounded concurrency. |
| Provider-specific authentication and secret integrations | Add one issue per chosen provider after HTTP-05 / APP-02 contracts exist. |
| Circuit breakers and advanced outbound retry policy | Follow-up design after the minimal LIB-03 adapter; no automatic retries for unsafe operations. |
| Business ORM, migration tooling, concrete outbox adapters | OPS-05 defines the contract; select one driver and split implementation afterward. |
| Native dependencies, source generators, full F# coverage | Explicit interoperability expansion after INT-01–04; no blanket support claim. |
| Advanced generic constraints/variance/type aliases/import aliases | Deferred grammar/type-system work. |
| Raw strings, extra literal suffixes, general local reassignment | Deferred syntax. |
| Borrowed mutable builder parameters/captures | Deferred; non-escape rules stay strict. |
| Alternative wire formats and arbitrary payload migrations | Extend the approved serialization/evolution protocol with separate format/version issues. |
| Cloud provisioning, deployment manifests and packaging distribution | Separate infrastructure/product work; declarations do not automatically provision cloud resources. |
| Final CLI branding/package manifest/file extension | Current prototype conventions are usable; permanent public contract remains a decision. |
| Native AOT | Deferred; current backend is managed .NET. |

## Pipes and logging additions — 13 November 2026

These briefs implement [the target specification](DSharp_Pipes_and_Logging.md).
Their creation does not enable runtime projects or mark language/runtime support
implemented. Preserve one bounded plan and PR per implementation slice; split a
brief before dispatch when its staged prerequisites require it.

## LANG-41: Implement automatic partial application

**Planning status:** Parent [#129](https://github.com/gajo357/dsharplang/issues/129); first child [#135](https://github.com/gajo357/dsharplang/issues/135) stages ordinary functions before later callable kinds.

### Goal

For fixed-arity `(A, B, C) -> R`, make `f(a)` a `(B, C) -> R` and `f(a,b)` a
`(C) -> R`, retaining the declared multi-argument function type.

Dependencies: LANG-13/#30, LANG-16/#33, LANG-17/#34, LANG-18/#35 and LANG-19/#36 for the
ordinary-function slice. Async/method/default/extension and lifetime coverage
waits for LANG-24–28/#41–45 and LANG-30/#47 as applicable; do not claim those
kinds complete before their implementation exists.

### Acceptance criteria

AC1: Support positional prefixes, grouped calls and pipes supplying the first remaining argument; a newline before `|>` continues the expression. Reject excess arguments and discarded function-valued statements.

AC2: Evaluate the callable receiver and supplied arguments once in source order when forming the partial function. Invoke its body only once all arguments arrive; `f()` supplies an empty prefix unless the function has zero parameters.

AC3: Infer generic arguments from supplied values and expected/rest-of-pipe context without declaration-order overload selection. Preserve async task deferral, receiver dispatch and resource/builder lifetime restrictions as those callable kinds become available.

### Constraints

Use compiler/CLI sources, new feature tests, examples and feature documentation.
Preserve existing tests, SDK, dependencies and C# generation. Method/async work
must have its prerequisites and scoped tests; no workflow/project/verifier edits.
The obsolete underapplication-rejection tests need a narrowly reviewed migration
before child #135 dispatch; unrelated tests and verifier checks remain intact.

### Non-goals

No argument holes, automatic type-level currying, partially named calls, guessed
foreign overload/default/variadic semantics or general variadic partial application.

### Verification

Run the required repository checks and executed generated-C# tests for grouped
calls, receiver/argument effects, generic ambiguity and delayed task creation.
Include negative escape checks and distinguish one tuple argument from several.

### Decisions already made

The pipe stays ordinary application. Configuration/dependencies conventionally
precede the flowing data. Fully supplied supported calls retain their behavior.

## LIB-04: Implement Result composition helpers

**Planning status:** Parent [#130](https://github.com/gajo357/dsharplang/issues/130); synchronous child [#136](https://github.com/gajo357/dsharplang/issues/136) may precede async overloads.

### Goal

Provide `andThen`, `map`, `mapError`, `tap` and `tapError` as ordinary standard
functions with callback first and flowing Result last.

Dependencies: LANG-14/#31 and core partial application #135 for synchronous child #136;
LANG-24/#41 and LANG-25/#42 for the complete async matrix. Library placement must
use the enabled project boundary or a separately approved setup child.

### Acceptance criteria

AC1: Implement the success/error callback behavior in section 4 of the target. Preserve errors, invoke only the applicable callback once, and require a shared andThen error type or an explicit mapError.

AC2: Cover all four Result/Task<Result> × Result/Task<Result>-returning andThen combinations. Other helpers accept Result or Task<Result> with synchronous callbacks; tap observers return Unit.

AC3: Await task sources once; preserve ordinary eager callback-expression evaluation, exceptions and cancellation. `map` wraps without flattening; Success(null) and Success(None) remain successes.

### Constraints

Preserve plain pipe semantics, Option tags and nullable normalization. Add no
runtime scheduling, retry, queue, transaction or implicit exception-to-Error layer.
Use exact enabled library/compiler paths and checks in each bounded plan.

### Non-goals

No async tap callbacks, automatic error unions, retries, parallelism or durability.

### Verification

Execute sync/async matrix tests with callback counters and controlled tasks;
exercise typed errors, cancellation/faults, nested Result/Option/Task and null.

### Decisions already made

These helpers are library symbols, not keywords or special Result-aware pipes.
Worker/queue adapters preserve their separate dispatch and acceptance contracts.

## LOG-01: Implement LogEvent, templates and scope capture

**Planning status:** [#131](https://github.com/gajo357/dsharplang/issues/131); runtime setup and bounded compiler/runtime slices required.

### Goal

Define typed immutable LogEvent data and the `Logger.log`/`Logger.scope` capture
boundary from sections 6–8 and 11 of the target.

Dependencies: relevant SET-02/#9 and SET-03/#10 setup; LANG-20/21/#37/#38,
LANG-24/#41, LANG-28/#45, standard values #79 and JSON codec #78. This contract
must not depend on LOG-02 delivery, which consumes its captured events.

### Acceptance criteria

AC1: Check literal/constant templates, matching named fields, duplicates, escaped braces and interpolation diagnostics. Evaluate every supplied expression once in source order even for disabled levels.

AC2: Freeze supported JSON-compatible field values without implicit record/union/Option serialization or numeric precision loss. Preserve template, message, origin, stable identity and runtime metadata separately; validate size/depth and nonfinite values through independent drop diagnostics.

AC3: Implement host-isolated immutable scope chains and capture snapshots; merge outer, inner and event fields in that order. Dispose scopes on normal/early/Result/fault/cancellation exits and explicitly transfer allowed context across runtime boundaries.

### Constraints

Use enabled compiler/runtime paths only. Logger named-field syntax is a designated
API, not general variadic partial application. Source expression faults retain
ordinary semantics; invalid event data must not fault business handling.

### Non-goals

No automatic Option wire schema, hidden template dependencies on scopes, or
scope/correlation fields as authorization evidence. Delivery belongs to LOG-02.

### Verification

Execute compiler diagnostics and generated calls; test await/concurrent host scope
isolation, cleanup and frozen snapshots with controlled barriers.

### Decisions already made

`Logger.log` returns Unit after synchronous capture and a non-waiting submission
attempt. It promises neither durable acceptance nor eventual processing.

## LOG-02: Implement Logger actor, console and output lifecycle

**Planning status:** [#132](https://github.com/gajo357/dsharplang/issues/132); runtime setup required.

### Goal

Run one host-scoped standard Logger actor using existing actor infrastructure,
with bounded transient admission and independent output delivery.

Dependencies: LOG-01/#131, RUN-01/#56, RUN-03/#58, DEC-07/#17 and the enabled
runtime/output project boundary. Runtime instrumentation in OPS-01 depends on
this facility, not the reverse.

### Acceptance criteria

AC1: Default to Info and one console JSON-lines output, with no database. Validate startup settings; explicit outputs replace the default and an empty list disables delivery. Bootstrap output is not replayed; host/test instances stay isolated.

AC2: Implement the section 10 event/byte/batch/time budgets, independent per-output backlogs, three attempts, deadline/quarantine rules, stable event IDs and overflow counters. At most one write per output may run, including after timeout.

AC3: Return producer calls without waiting for output I/O. Keep healthy outputs progressing when another blocks; bypass Logger for rate-limited internal diagnostics. Drain under the host deadline after producers, then release outputs before the shared store.

### Constraints

Use the existing actor scheduler/supervision. LogOutput accepts immutable batches
and reports whole-batch success or a typed failure. Keep state actor-owned and
all active batches/tasks/retries within the specified bounds.

### Non-goals

No second actor framework, durable mailbox, exactly-once/audit guarantee, default
database/cloud exporter or arbitrary package auto-activation.

### Verification

Use fake outputs, controlled clocks/barriers, blocked-output progress tests and
shutdown/fault/retry tests. Demonstrate documented transient crash loss.

### Decisions already made

Console-only defaults and the runtime system-actor registration exception do not
change explicit registration rules for application handlers.

## LOG-03: Implement database log output and retention

**Planning status:** [#133](https://github.com/gajo357/dsharplang/issues/133); runtime setup and real-provider tests required.

### Goal

Provide opt-in DatabaseLogOutput on the application's existing RuntimeStore,
with separate logging schema/migrations and bounded retention maintenance.

Dependencies: LOG-02/#132, RUN-04/#59, RUN-05/#60, RUN-09/#64 and LIB-01/#78.
Other database providers follow their own setup/conformance issues.

### Acceptance criteria

AC1: Commit immutable event batches atomically, deduplicating stable event IDs, preserving typed JSON and the section 12 indexes. Do not create a database per queue or a durable job per log event.

AC2: Default to seven-day visibility measured from received_at, with startup/periodic bounded cleanup and explicit keep-forever. Enforce the default 256 MiB logical retained-log budget; count capacity eviction separately from expiration.

AC3: Exclude expired events from normal queries before deletion. Test coexistence with queue claims and shared migrations; log failures cannot roll back handler/business transactions or remove queue rows.

### Constraints

Logging remains outside business/queue transactions. No remote work under an
open storage transaction, aggressive whole-file compaction or implicit promise
about disk/backups deletion. The mailbox remains transient.

### Non-goals

No mandatory separate logging database, default activation or remote-provider
retention promise beyond that provider's documented capability.

### Verification

Use the real initial provider for commit, retry deduplication, retention, capacity,
restart and queue-contention integration tests; mocks alone are insufficient.

### Decisions already made

Database output success means commit. Delivery failure is isolated from business
handling; retained bytes do not describe the whole database file or its WAL.

## LOG-04: Implement LogReader and observer search

**Planning status:** [#134](https://github.com/gajo357/dsharplang/issues/134); real-provider and authenticated observer integration required.

### Goal

Expose a separate read-only LogReader service and integrate typed, bounded log
search into the existing observer.

Dependencies: LOG-03/#133, OPS-02/#92 and HTTP-05/#85. Core OPS-02 does not depend on
LOG-04; this is its subsequent log-search integration.

### Acceptance criteria

AC1: Implement time bounds/choice, level/category/correlation and AND-combined typed field predicates, with default page size 100 and maximum 500. Bind queries as parameters and validate field paths; no executable predicates or SQL inputs.

AC2: Preserve missing versus explicit null and numeric comparison semantics. Opaque cursors bind the query and first-page high-water sequence; tolerate retention deletion and reject unsupported provider semantics or timeouts explicitly.

AC3: Require observer read authorization, show history unavailable without a query-capable output, and isolate failures from queue work. Tenant filter values are not access control; Logger does not execute queries.

### Constraints

Use the existing observer/service contract and real-provider integration tests.
No implicit public HTTP route, silently truncated successful query or long-lived
snapshot guarantee.

### Non-goals

No arbitrary boolean/full-text language, cross-cloud search, automatic index for
every field, or unbounded console history buffer.

### Verification

Test JSON missing/null/precision, cursor stability under insert/retention,
authorization, empty/no-store states, provider limits and bounded timeouts.

### Decisions already made

The async reader returns `Result<LogPage, LogQueryError>`; LogPage has immutable
events and a nullable next cursor. Query state never changes Logger delivery.

## Completion rule

An implementation issue is complete only when the specified behavior works, intended invalid programs fail for the correct reason, relevant trusted checks pass at the reviewed commit, and the status document reflects the actual scope. Parsing a construct, producing a stub, or compiling unrelated generated C# is insufficient.

A feature marked partially implemented stays partial. A design issue closes when its decision record is reviewed; it does not establish executable support. A later feature may require integration tests against earlier features, but it must not weaken the earlier tests.

## Suggested instruction for the repository agent

> Work from DSharp_Feature_Backlog.md and the repository's current AGENTS.md, policy and implementation status. First reconcile the backlog with current main and existing GitHub issues. Reuse issue #7 for LANG-01. Prepare only the next three dependency-ready issue bodies, with the six required sections, exact grammar/API examples, allowed paths, AC identifiers and existing verification commands. Mark unresolved contracts and infrastructure prerequisites explicitly. Do not implement the entire backlog, duplicate existing issues, create live issues without an explicit request, or approve/merge your own changes. For an implementation run, complete only the single frozen issue selected by AI coordinate and provide AC-to-test evidence.

