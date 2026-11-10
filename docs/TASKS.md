# Language documentation tasks

Source: the supplied **Backend Language Feature Specification v0.2**, preserved
verbatim in [design/FEATURES.md](design/FEATURES.md). The original shared
[discussion](https://chatgpt.com/share/6af2e18e-4778-83eb-a8d1-6721ab4e9fa1)
was inaccessible; documentation uses the supplied specification instead.

## 1. Extract the agreed design — complete

Established direction: .NET, immutable typed data, fresh isolated invocations,
explicit code-based wiring, typed composition, and durable queue machinery reused
by events and cron. Proposed defaults and unresolved decisions are identified
in the documents rather than promoted to guarantees.

## 2. Explain the language — complete

[README.md](../README.md) explains motivation, purpose, inspirations, intended
use cases, a conceptual flow, implementation status, and documentation links.
No install or build commands are invented for the documentation-only repository.

## 3. Explain architecture and implementation — complete

[ARCHITECTURE.md](ARCHITECTURE.md) describes components, interactions, startup,
resource ownership, failure boundaries, persistence, transport packages, .NET
interop, and proposed implementation stages. All implementation components are
explicitly planned, not claimed as existing code.

## 4. Document types and syntax boundaries — complete for the supplied baseline

[LANGUAGE.md](LANGUAGE.md) records established type semantics and illustrative
valid/incompatible cases. It inventories the unresolved syntax work. The source
leaves grammar and keywords open, so examples are pseudocode and no executable
grammar is invented. Finalizing syntax remains a separate language-design task.

## 5. Review together — complete

Reviewed terminology and guarantees against the supplied specification. Local
Markdown links, code fence balance, source-copy integrity, and whitespace were
checked. There is no compiler or test suite with which to validate examples.
Changes remain on `docs/language-design`; nothing is merged or deployed.

## Architectural decisions recorded

These decisions come from the supplied baseline, rather than new implementation:

- Use explicit typed application registration, with handler-defined triggers;
  keep configuration values separate from application wiring.
- Default to fresh isolated invocations; do not equate sequential queues with
  persistent actors.
- Reuse durable execution for queues, event subscriptions, and schedules, with
  at-least-once processing and explicit ordering/concurrency scopes.
- Separate runtime persistence from business data, and require explicit outbox
  coordination when changes cross database boundaries.
- Target .NET while retaining immutability, ownership, and failure boundaries;
  leave compiler backend, runtime framework, and safe interop APIs open.

Documentation organization uses three primary documents plus the preserved
source baseline. Concrete syntax is deferred rather than inferred. No source
implementation, package dependencies, or runtime framework is introduced.

## Follow-up design tasks

- Choose grammar, type details, composition mechanics, and scoped-resource APIs.
- Select compiler/backend and registration metadata strategy.
- Define the minimum safe .NET interoperability surface.
- Resolve persistent actors, distributed recovery protocols, and serialization evolution.
- Design the deferred backend areas listed in the source; gRPC remains deferred.
