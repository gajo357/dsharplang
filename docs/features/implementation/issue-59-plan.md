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
