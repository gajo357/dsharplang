# Issue #52 — trigger and message contract plan

Issue: https://github.com/gajo357/dsharplang/issues/52
Base: `a096a78dd3d745df11b7095ed7cefcea90cdb258` (`origin/main`)

## Scope and current state

This is the parent planning/contract slice only. It does not implement trigger
syntax, alter compiler behavior, add a transport or close #52. The sequence
requires separate Queue, Event, Cron and HTTP implementation children, each with
its own issue, plan, branch, PR and evidence. Create those bounded child issues
after this plan is merged; do not combine the children or mark their work done
here. Search live issues, branches and PRs before creating each child and reuse
any existing owner. The queue child is first. The parent remains open until all children and
their shared compiler-contract acceptance checks are complete. Actual codec
compatibility and `Invoke`/`Dispatch` typing remain pending until the separately
eligible #78 and #241 supply the contracts needed to prove them. Application
registration and runtime integration remain downstream work; neither #54 nor
#69 can be made a prerequisite of #52 because each already depends on it.

The actor handoff #239, checked actor compiler contract #51, builder escape
checks #40 and durable-acceptance decision #14 are closed. Scheduling decision
#18 and HTTP decision #15 are closed, but their documents still identify
provisional spellings and unimplemented runtime behavior. Their closure is not
evidence of a working scheduler or HTTP host. The JSON codec #78, application
registration #54, dispatcher #241 and durable admission #61 are still open.
Dependent child work must recheck these states and the exact relevant merged
implementation prerequisites when dispatched.

The compiler currently exposes #51's checked `BoundActorDeclaration`, including
input, result, dependencies and source spans. Its existing `DataType` checks
reject several capability shapes; they are not a recursive proof that a message
has a supported wire representation. No trigger bindings or transport checks
are claimed by #51.

## Shared semantic contract

- A trigger is an explicit way to supply a message to the existing public actor
  contract. Resolve the target and compare the trigger's declared output/input
  to the checked `Handle` input using resolved type identity and generic
  substitution, not display-name equality. Retain target, resolved input and
  result, transport-specific declaration data and source locations for
  diagnostics and later registration. The representation is internal until its
  consumer and syntax are decided; there is no second actor category.
- Diagnose missing or incompatible bindings and duplicate or misspelled
  reserved members at their source locations, including the expected Handle
  signature. Preserve single- and multi-file resolution and reject ambiguous
  names rather than choosing a target. Children must document their exact
  diagnostic and metadata shape and test it through the supported CLI paths.
- Queue message roots are a nonnullable declared immutable record/union `type`
  or `Unit`, not a primitive, nullable, tuple or collection root. Examine nested record, union and generic
  members after substitution; reject builders, live resources, interfaces used as
  capabilities, tasks, functions and other non-transportable values. Ensure a
  cyclic type graph terminates without treating an unexamined capability as
  safe. Distinguish a structurally eligible immutable type from a codec-supported
  wire representation: #78 owns encoding, including null/Option distinctions.
  A child must not claim serialization or accept an unsupported wire format by
  assumption. Transport-specific restrictions may be stricter.
- The actor's declared business result remains available to a future explicit
  `Invoke`; targeted `Dispatch` and publication acknowledge acceptance rather
  than returning a handler's value. A target registration does not implicitly
  subscribe it. Preserve the existing direct-construction, direct-Handle,
  method-group and capability-cast restrictions. #241 owns dispatch code, #54
  owns explicit application registrations and subscriptions, and #61/#69 own
  durable targeted acceptance/publication. A trigger binding alone implements
  none of those runtime behaviors.

The existing references illustrate `Queue<string>(...)` as invalid but do not
freeze a complete trigger grammar. Each child must record its narrowly chosen
spelling and interface in its plan before implementation, reconcile it with #54,
and avoid silently selecting a transport protocol, wire encoding or public
runtime API. If a required contract cannot be derived from merged decisions,
preserve the draft and identify the exact unresolved dependency. HTTP binding
and response mappings remain governed by #15; schedule metadata and policies
remain governed by #18. No new application error type is inferred.

## Child boundaries and dependencies

| Child | Bounded compiler work and required evidence | Dependencies and exclusions |
| --- | --- | --- |
| Queue | Check an explicit typed queue binding against one checked actor, preserve its transport metadata and meaningful Handle result, enforce message-root and recursive data restrictions, and report mismatches with locations. Test valid `Unit` and declared messages, generic nested invalid payloads, duplicate/misspelled members, mismatched input and multi-file identity. Execute generated code only for behavior actually emitted. | #239, #51, #40 and #14 merged. No queue provider, codec, startup, dispatch or durable acceptance; integrate codec-supported shapes with #78 only when available. |
| Event | Check explicit event bindings and message compatibility using the same semantic model; cover no implicit subscriptions and an explicit subscription-contract fixture for a public/default target. Retain a meaningful Handle result. | #14 and the shared queue model; coordinate the contract with #54 without waiting on its implementation. #54 owns actual application registration and #69 owns fan-out. Do not claim delivery or auto-subscription. |
| Cron | Check schedule binding payload/target compatibility and retain the declared schedule metadata needed by later validation. Test valid payload/target, invalid payload and mismatch without claiming time-zone calculations or occurrence recovery. | #18 and the shared model; scheduling implementation #70 owns calendar, durable occurrence and recovery behavior. |
| HTTP | Check the declared direct binder or successful preprocessing output against Handle input and retain enough transport and response-mapping information to reject statically knowable invalid contracts. Cover incompatible pipeline results and declared mappings without inventing error types. | #15 and the shared model; route hosting/binding are #81/#82 and callable preprocessing is #83. Respect explicit mappings and leave wire encoding to its owners. |

Before opening each child PR, its plan must list the exact approved paths and
interfaces, identify any unmerged implementation dependency, map its own ACs
to positive and negative tests, and record commands and actual results. Review
new syntax as part of that bounded implementation, not as a promise of a stable
runtime API. Preserve existing tests and trusted verification.

## Parent acceptance and integration evidence

| #52 criterion | Evidence needed before the parent can close |
| --- | --- |
| AC1: explicit bindings and diagnostics | Each child tests target/input compatibility, invalid or duplicate declarations and source locations; HTTP also checks its applicable response mapping rules. |
| AC2: immutable serializable messages and transport metadata | Queue child tests declared/Unit roots and recursively substituted nested shapes, capability rejection and retained metadata. Before closing #52, an approved #78 codec/schema contract must support the accepted shapes and compiler checks must enforce that contract; structural eligibility alone is insufficient. |
| AC3: meaningful result, dispatch and no implicit subscription or bypass | Queue/event compiler tests preserve the checked result and existing direct-call restrictions, and show that checked targets do not create implicit subscription metadata. Before closing #52, use the independently eligible #241 compiler surface to prove a triggered actor can be targeted by both `Invoke` and `Dispatch` with the correct types. #54 later verifies application registration, while #61/#69 verify actual runtime receipt behavior. Do not present those downstream runtime checks as already completed or require them as prerequisites of #52. |
| AC4: queue and event coverage | Queue child covers typed binding, business result, invalid payload and mismatch; event child checks an explicit public-target subscription contract without claiming host registration. Cross-child compiler tests use the same resolved actor model; #54 separately verifies the registration recipe. |

## This slice: files and verification

Only this plan is changed. No compiler, tests, workflow, dependency, issue
completion state or implementation-status claim changes. Review the plan against
#52, #239, #51, #14, #15, #18, #54, #78, #241, the sequence and the task queue.
Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-compiler.sh`, `bash scripts/verify-runtime.sh`, and
`git diff --check`. Record actual results and any local environment limitation
in the draft PR. A documentation pass alone is not evidence that a child or
the parent is implemented.

## Planning verification results

- Bootstrap passed. The documentation validator passed (115 files and 304 local
  links), as did all 31 automation tests. The isolated verifier could not pull
  the pinned MCR SDK image layers (`Forbidden`), so its compiler/runtime stages
  require the exact-head hosted CI result.
- The standalone compiler verifier passed locked restore/build and all 18
  baseline plus 382 feature tests without skips. The standalone runtime verifier
  passed 2 baseline and 1 feature test. `git diff --check` passed.
- Live GitHub confirmed #239, #51, #40, #14, #15 and #18 closed, with no open
  PR or existing #52 owner branch at dispatch. #54, #78, #241 and #61 remained
  open. The plan explicitly avoids making downstream #54/#69 prerequisites of
  #52, since they depend on #52.
