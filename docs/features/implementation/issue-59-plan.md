# Issue 59: RuntimeStore contracts and reference implementation

## First slice: volatile targeted-submission bookkeeping

Implement and exercise an in-memory, single-process reference for the atomic
identity and lookup portion of targeted admission. This is a bounded slice of
#59; it does not close the issue or provide durable acceptance.

Base: `e34c3bf92888e466af4d43fe664838020a16c291`. Dependencies: #9's test
infrastructure and #14's DEC-04 contract are merged. #78 owns the real codec;
this slice uses explicitly comparable opaque input and does not depend on #52.
Files: `src/DSharp.Runtime/VolatileTargetedStore.cs`,
`tests/runtime-features/VolatileTargetedStoreTests.cs`, this plan, and
`docs/features/DSharp_Implementation_Status.md`. The internal provisional
interfaces are a byte-comparable request, record, result and volatile store;
the existing dispatcher ABI is not changed. No D# syntax or public provider
protocol is finalized here.

1. Define explicit application/submission and invocation identities and a
   copied, opaque request envelope: operation, requested target, payload and
   schema identity, behavior-changing options, and parent invocation and attempt.
   Reject partial parent pairs and parents on root Dispatch. Compare all fields
   and bytes exactly; this reference equality does not establish DEC-04 semantic
   equality or a canonical codec. Selection is pinned by the first record and
   excluded from retry equality; #78 owns the actual codec.
2. Make lookup, matching retry, conflict detection and insertion atomic under
   a lock, with operation outside the shared application/submission key. Validate
   a positive finite retention horizon and reject unknown or unsupported
   requirements, including canonical equality, durability and transactions. Keep retained records for the
   lifetime of this volatile store. Expose lookup so the host can check before
   selection; host authorization and selection orchestration remain future work.
3. Exercise retries, conflicts, races, distinct namespaces, immutable snapshots,
   collision handling and rejected capabilities with runtime feature tests. Run
   the repository bootstrap, compiler, runtime and isolated verification; record
   actual outcomes and any environmental limit in the PR.

Acceptance evidence: concurrent identical and conflicting submissions exercise
atomic identity; retry and changed selection exercise pinning; field changes,
including parent attempt, exercise conflict detection; input and output mutation
exercise ownership; cross-application keys and invocation collisions exercise
namespace isolation; invalid horizon and unsupported capability tests exercise
fail-closed setup. These tests cover only the targeted bookkeeping subset of #59.

Verification commands: `bash .github/ai/bootstrap.sh`,
`bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`,
`bash scripts/verify-runtime.sh`, and `git diff --check`. The full verification
requires access to the pinned container image. Record any actual limitation.
No user decision is required for this explicitly volatile, provisional slice;
the actual codec, durable provider and orchestration remain unresolved future work.

## Verification on this slice

- Bootstrap succeeded. Runtime verification passed 2 baseline and 10 feature
  tests, including the barrier-coordinated contention and capability cases.
- Compiler verification passed 18 baseline and 406 feature tests in a private
  short-path source snapshot, with the unchanged TRX size guard (506,836 bytes).
  The runtime source matches the final candidate; the snapshot predates the
  final runtime test-only barrier and capability assertions, which were covered
  by the subsequent full runtime verification.
- Documentation checks and all 31 automation tests passed. The isolated local
  verifier could not pull its pinned MCR image (`Forbidden`), so its container
  build and tests require hosted verification; no local durability is inferred.

This slice does not implement publication, claims, leases, recovery, provider
authorization, result compatibility, external effects, SQLite, or a production
dispatcher. It cannot demonstrate crash durability or promise that competing
callers run selection only once. Parent identities are copied for comparison,
not validated against an invocation or attempt registry. #59 remains open for
its full acceptance gates.

## Second slice: volatile publication identity and frozen intents

Base: `30521eb276387d59ec9a641aa9dd5e36ec38d578`, containing the first
targeted bookkeeping slice and the merged #9/#14 prerequisites. Continue #59
on its dedicated branch. Extend the same internal volatile reference in
`src/DSharp.Runtime/VolatileTargetedStore.cs` and
`src/DSharp.Runtime/VolatilePublicationStore.cs`; add
`tests/runtime-features/VolatilePublicationStoreTests.cs` and update the status.
No existing tests, public ABI, dependencies, trusted verifier or D# syntax change.

Represent a publication's byte-exact reference request and a fully supplied
frozen set of subscription IDs, public targets and selected implementations.
Use a separate publication identity and stable `(publication, subscription)`
delivery identity. Validate all proposed intents and identity collisions before
mutation, permit an empty set, and atomically insert the publication and complete
frozen set. Use the same application/submission namespace and lock as targeted
submissions so the operation kinds cannot reuse one identity. Resolve matching
retained requests before considering changed proposals; return the original set
and selection, reject conflicting requests, and expose request-aware shared-key
probes so callers can distinguish absence, a matching record and cross-kind or
same-kind conflict before selection. Never invoke host code under the store lock.
Snapshot mutable inputs and outputs. Unsupported canonical equality,
durability and atomic follow-up requirements continue to fail closed.

Acceptance-to-test map: zero/one/multiple frozen intents and duplicate subscription
rejection cover all-or-none insertion; retry with changed or invalid proposals and
changed request cover pinned identity and conflicts; concurrent matching and
conflicting submissions and cross-operation races cover the shared namespace;
identity collision and buffer/list mutation cover no partial state and ownership.
Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-compiler.sh`, `bash scripts/verify-runtime.sh`, and
`git diff --check`. Record actual results and any isolated-image limitation.

This is reference-only byte equality and process-local atomic bookkeeping. It
does not validate host subscriptions, authorization or canonical encoding, run
selection, schedule or execute delivery, recover after a crash, or claim a
durable receipt. The actual codec, provider and host orchestration remain future
work. #59 remains open for attempts, provider conformance and its other gates.
The probes do not reserve a key or prevent competing callers from performing
selection concurrently; acceptance must recheck and #61 owns host orchestration.

Verification: bootstrap and documentation checks passed; all 31 automation
tests passed. Runtime verification passed 2 baseline and 17 feature tests. The
isolated local verifier could not pull its pinned MCR image (`Forbidden`); hosted
verification is still required for that container path. Compiler verification
passed 18 baseline and 406 feature tests from a private short-path source snapshot;
the unchanged TRX guard accepted 506,836 bytes. The runtime source matches the
final candidate; the subsequent runtime run covers the final bounded waits in
the new runtime test's race barriers.
