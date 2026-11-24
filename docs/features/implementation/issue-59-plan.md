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

## Third slice: volatile targeted ownership and expiry

Base: `1c20ff0097571ea316887901889b59693b811173`, with #9/#14 and the
preceding #59 slices merged. Continue on the existing dedicated branch. Add
`src/DSharp.Runtime/VolatileTargetedOwnership.cs` and
`tests/runtime-features/VolatileTargetedOwnershipTests.cs`, extend the targeted
identity check in `src/DSharp.Runtime/VolatileTargetedStore.cs`, and update this
plan and the implementation status.
Existing tests, dependencies, verifier, public ABI and D# syntax remain unchanged.

Use an explicitly initialized store-owned simulated clock, advanced only
monotonically under the store gate, and read it once in each ownership operation.
Do not invoke host callbacks or use caller-supplied transition timestamps. A
claim of an already accepted targeted invocation requires its application,
submission and invocation identity; publication IDs have a separate domain and
may have the same raw value. Claim atomically creates a distinct attempt and
fresh ownership token, records a positive finite lease and retains
the original invocation and selection. Never reuse an attempt identity or token.
Targeted invocation, attempt and token IDs cannot collide in either insertion
order; publication IDs remain a distinct domain.
Renewal checks the exact current owner and unexpired lease and strictly extends
the deadline. All time arithmetic and identities are validated before mutation.

Expiry uses an observed attempt, token and lease deadline, atomically verifies
that all still match and that `now >= leaseUntil`, and fences the expired owner
only when the caller explicitly selects terminal lease-expired failure. Retain
that failure and the attempt; do not claim the handler threw. No automatic retry,
eligibility, scheduling or provider policy is selected. This is only a volatile
fencing reference; retry, rescheduling and normalized execution faults remain
#59 work.
It cannot stop a stale process or guarantee ordering of its external effects.

Tests map to the slice as follows: coordinated claims and renew/expiry races
exercise atomic ownership; exact-boundary expiry, old observations and wrong or
reused tokens exercise fencing; invalid/overflowing durations and backwards
clock movements exercise fail-closed validation; retained invocation and
selection check identity preservation; unknown or publication identities cannot
be claimed. Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-compiler.sh`, `bash scripts/verify-runtime.sh`, and
`git diff --check`; record actual outcomes and isolated-image limits.

This internal, single-process simulated clock is not a provider-authoritative
real-time or restart-safe clock. Durable acceptance, real provider conformance,
completion, cancellation, policy, rescheduling and publication execution remain
open; #59 is not completed by this slice.

Verification: bootstrap and documentation checks passed; all 31 automation
tests passed. Runtime verification passed 2 baseline and 24 feature tests.
Compiler verification passed 18 baseline and 406 feature tests in a private
short-path snapshot. Its runtime source and feature test match this candidate;
the unchanged 512,000-byte TRX guard accepted the 506,836-byte feature report.
The local isolated verifier could not pull its pinned MCR image (`Forbidden`);
hosted verification is still required for that container path.

## Fourth slice: volatile fenced normal completion

Base: `72294c442bd1e924be28bec314d730e94a16ffb2`, with #9/#14 and the
preceding #59 slices merged. Continue the existing branch. Extend the internal
targeted ownership reference in `src/DSharp.Runtime/VolatileTargetedOwnership.cs`
and add `tests/runtime-features/VolatileTargetedCompletionTests.cs`; update this plan and the
implementation status. Existing tests, dependencies, verifier, public ABI and D#
syntax stay unchanged.

Model a copied opaque normal-result envelope containing bytes and nonempty
result contract/encoding metadata. Empty bytes can represent a Unit-like value;
business-error-like bytes remain normal values. The reference does not decode or
validate declared D# types or provide canonical encoding. Copy buffers on input
and output, and preserve the accepted request, selection, invocation and attempt.

Completion reads the store's simulated clock once inside the shared gate and
requires the exact targeted key and invocation, running state, attempt and token,
and `now < leaseUntil`. It atomically records one terminal completed outcome.
Wrong, stale, expired, unknown or terminal owners cannot mutate the result; a
repeat returns a non-mutating terminal status, even for matching bytes. Read-only
inspection can return the retained outcome; this slice does not claim lost-ack
replay semantics. No host callbacks occur under the gate and no follow-ups are
accepted; same-store atomic follow-ups remain explicitly unsupported.

Tests map to acceptance as follows: plain, Unit-like and business-error-like
opaque outcomes and copied buffers exercise preservation; invalid metadata and
wrong identity or state exercise fail-closed behavior; exact deadline, renewal,
stale owners and repeated completions exercise fencing; coordinated competing
completion, renewal and expiry races exercise atomic terminal outcomes. Run
`bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-compiler.sh`, `bash scripts/verify-runtime.sh`, and
`git diff --check`; record actual results and any isolated-image limit.

This remains a volatile simulation, not a production provider, canonical codec,
schema compatibility check, durable acknowledgement, authorization mechanism,
executor, fault/retry policy, cancellation or restart evidence. #59 stays open.

Verification: bootstrap and documentation checks passed; all 31 automation
tests passed. Runtime verification passed 2 baseline and 29 feature tests.
Compiler verification passed 18 baseline and 406 feature tests in a private
short-path snapshot; the unchanged 512,000-byte TRX guard accepted the
506,836-byte feature report. Runtime source matches the final candidate; the
final unclaimed/publication-key assertions were covered by the subsequent full
runtime run.
The local isolated verifier could not pull its pinned MCR image (`Forbidden`);
hosted verification is still required for that container path.

## Fifth slice: volatile owned rescheduling after a reported fault

Base: `faa47ed4ca4acbfc3975acebefd6f6986bebc1cd`, with #9/#14 and the
preceding #59 slices merged. Continue the existing issue branch. Extend the
internal reference in `src/DSharp.Runtime/VolatileTargetedOwnership.cs`, add
`tests/runtime-features/VolatileTargetedRescheduleTests.cs`, and update this plan
and implementation status. Keep old tests, dependencies, verifier, public ABI and
D# syntax intact.

An explicit caller-selected reschedule after a reported runtime fault requires
the full targeted key, Running state, exact attempt/token and `now < leaseUntil`
under the store gate. A nonnegative caller-selected delay is added with checked
time arithmetic to the simulated clock read once inside the gate; persist the
resulting eligibility instant without rerolling or choosing a default policy.
Retain a copied opaque caller-reported fault and an immutable snapshot of each
rescheduled attempt. Atomically fence the old owner and store the eligible time.
The data is neither a canonical codec nor a normalized diagnostic; no actor fault
origin is inferred. A subsequent claim may occur only at or after eligibility,
with fresh never-reused attempt/token identities and the same invocation, input
and selected implementation. No host callback runs inside the gate.

Completed normal values, including business-error-like bytes, and terminal
lease-expiry failure remain terminal. This slice does not add expiry-to-retry,
automatic policy, retries of business results, cancellation or provider
recovery. Unrecognized durability, follow-up or policy guarantees remain
unsupported. The simulated reference cannot stop a stale process or guarantee
external-effect ordering; #59 remains open.

Acceptance-to-test map: zero and positive delay, exact eligibility boundary,
negative/overflow rejection and stored due time exercise scheduling; stale,
expired, wrong and terminal owners exercise fencing; repeated reschedule and
fresh claim exercise identity non-reuse, pinned selection and immutable attempt
history; input/output mutation exercises fault copying; coordinated completion,
renewal, reschedule and claim races exercise atomic transitions. Run
`bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-compiler.sh`, `bash scripts/verify-runtime.sh`, and
`git diff --check`. Record actual outcomes and any isolated-image limit.

Verification: bootstrap and documentation checks passed; all 31 automation
tests passed. Runtime verification passed 2 baseline and 34 feature tests.
Compiler verification passed 18 baseline and 406 feature tests in a private
short-path snapshot matching the final runtime source and tests. The unchanged
512,000-byte TRX guard accepted the 506,836-byte feature report.
The local isolated verifier could not pull its pinned MCR image (`Forbidden`);
hosted verification is still required for that container path.

## Sixth slice: volatile expiry recovery with explicit retry

Base: `d14e8fd516159cb8973ed41ebfc76514b00e5992`, with #9/#14 and the
preceding #59 slices merged. Continue the existing issue branch. Extend the
internal reference in `src/DSharp.Runtime/VolatileTargetedOwnership.cs`, add
`tests/runtime-features/VolatileExpiryRetryTests.cs`, and update this plan and
implementation status. Preserve existing tests, dependencies, verifier, public
ABI and D# syntax.

A caller may explicitly choose retry after lease loss. Under the same gate, read
the simulated clock once; require the full targeted key, Running state, exact
observed attempt, token and lease deadline, and `now >= leaseUntil`. Validate a
caller-selected nonnegative delay and checked due-time arithmetic before any
mutation. Atomically fence the old owner, persist the chosen eligibility time,
and retain a tagged lease-expiry history entry without inventing an actor fault
or origin. Existing terminal expiry failure stays terminal. Reclaim uses the
existing due boundary and fresh never-reused identities, preserving invocation,
request and selection; no default retry policy or host callback is introduced.

Preserve the existing fault-only history view for compatibility, and add one
immutable ordered view covering both reported-fault reschedules and lease-loss
recoveries. Tagged entries must expose fault data only for reported faults; copy
all mutable buffers and retain earlier snapshots. This does not claim canonical
encoding, normalized fault origin, real-time or durable recovery, cancellation,
configured retry policy, scheduler or external-effect ordering; #59 stays open.

Acceptance-to-test map: just-before/exact expiry, zero/positive delay and due
boundary exercise timing; renewed/stale observations, wrong IDs, negative and
overflowing delays and terminal states exercise fencing; mixed multi-attempt
history, fresh IDs and mutated outputs exercise identity and ownership; competing
recovery, renewal, failure and claim races exercise atomic transitions. Run
`bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-compiler.sh`, `bash scripts/verify-runtime.sh`, and
`git diff --check`; record outcomes and any isolated-image limit.

Verification: bootstrap and documentation checks passed; all 31 automation
tests passed. Runtime verification passed 2 baseline and 41 feature tests.
Compiler verification passed 18 baseline and 406 feature tests in a private
short-path snapshot matching the final runtime source and tests. The unchanged
512,000-byte TRX guard accepted the 506,836-byte feature report.
The local isolated verifier could not pull its pinned MCR image (`Forbidden`);
hosted verification is still required for that container path.

## Seventh slice: volatile fenced terminal reported faults

Base: `bcb10806c12c9bf408b58e41b41283da068e1590`, with #9/#14 and the
preceding #59 slices merged. Continue the existing issue branch. Extend
`src/DSharp.Runtime/VolatileTargetedOwnership.cs`, add
`tests/runtime-features/VolatileTerminalFaultTests.cs`, and update this plan and
implementation status. Keep existing tests, dependencies, verifier, public ABI and
syntax intact.

An explicit caller-selected terminal reported fault requires the full targeted
key, Running state, exact attempt/token and `now < leaseUntil` under the shared
simulated-clock gate. Store a distinct terminal state with copied opaque fault
data and an optional, nonempty caller-reported origin invocation ID. Preserve the
exact supplied origin; do not invent one or claim it was validated, authenticated
or normalized. Preserve invocation, selection, attempt and existing recovery
histories. A repeat, stale or expired owner cannot mutate the terminal snapshot.
Completed business-error-like values and lease-expiry failure remain distinct.

Acceptance-to-test map: present, absent and invalid origins plus mutated input and
output buffers exercise faithful copying; wrong key/attempt/token, just-before and
exact expiry, repeated and terminal operations exercise fencing; mixed prior
reschedule history exercises retention; coordinated fault, completion, reschedule,
renewal and expiry races exercise one coherent outcome. Run
`bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-compiler.sh`, `bash scripts/verify-runtime.sh`, and
`git diff --check`; record actual outcomes and isolated-image limits.

This internal simulation is not normalized #243 fault-chain propagation, origin
validation, cleanup-fault aggregation, authorization, durable storage, recovery,
automatic retry policy, execution or provider conformance. #59 stays open.

Verification: bootstrap and documentation checks passed; all 31 automation
tests passed. Runtime verification passed 2 baseline and 46 feature tests.
Compiler verification passed 18 baseline and 406 feature tests in a private
short-path snapshot matching the final runtime source and tests. The unchanged
512,000-byte TRX guard accepted the 506,836-byte feature report.
The local isolated verifier could not pull its pinned MCR image (`Forbidden`);
hosted verification is still required for that container path.

## Eighth slice: volatile cancellation winner bookkeeping

Base: `8ee328f5b2c833e9263896e2902f3a1fca7cda20`, with #9/#14 and the
preceding #59 slices merged. Continue the existing issue branch. Extend the
internal reference in `src/DSharp.Runtime/VolatileTargetedOwnership.cs`, add
`tests/runtime-features/VolatileCancellationTests.cs`, and update this plan and
status. Preserve existing tests, dependencies, verifier, public ABI and D# syntax.

Serialize the reference cancellation request with existing operations under the
same gate. An eligible invocation becomes Cancelled before claim; a running one
retains an idempotent request marker for a host to observe. A terminal result or
failure that committed first remains unchanged. If a request wins first, a
still-fenced normal or reported-fault exit finalizes Cancelled and preserves its
actual copied exit separately for inspection; it is not a delivered result or a
retry. This includes `RescheduleAfterFault`, which must not schedule another
attempt. Both expiry-recovery variants retain their exact observed fence and
finalize Cancelled without a retry or invented exit after a request, including
when the request arrived after lease expiry. Stale or expired owners cannot record
an exit, and renewal remains subject to the normal exact-owner, unexpired fence.
No host callbacks, authorization or signal delivery are added under the gate.

Acceptance-to-test map: pending/delayed requests and competing claim exercise
winner ordering; repeated requests and terminal-first cases exercise idempotence;
normal, business-error-like and reported-fault exits exercise separately retained
copied observations; wrong or expired owners and exact observed expiry exercise
fencing; bounded cancellation versus exit, reschedule, claim and recovery races
exercise single outcomes and preserved prior histories. Run
`bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-compiler.sh`, `bash scripts/verify-runtime.sh`, and
`git diff --check`; record actual results and isolated-image limits.

This is volatile state bookkeeping for #59 conformance, not authorization,
cooperative signaling, provider durability, wait delivery, physical process
termination, rollback of external effects, or complete cleanup-fault aggregation.
Execution and cancellation delivery belong to #67 and waiting to #57; #59 stays open.

Verification: bootstrap and documentation checks passed; all 31 automation
tests passed. Runtime verification passed 2 baseline and 52 feature tests.
Compiler verification passed 18 baseline and 406 feature tests in a private
short-path snapshot matching the final runtime source and tests. The unchanged
512,000-byte TRX guard accepted the 506,836-byte feature report.
The local isolated verifier could not pull its pinned MCR image (`Forbidden`);
hosted verification is still required for that container path.

## Ninth slice: reusable volatile-subset conformance scenarios

Base: `359ae2d066a9f7ef953aeb801cbe9e02d23c0118`, with #9/#14 and the
preceding #59 slices merged. Continue the existing issue branch. Add test-only
`tests/runtime-features/RuntimeStoreConformanceHarness.cs` and
`tests/runtime-features/RuntimeStoreConformanceTests.cs`, and update this plan
and the implementation status. Keep existing tests, dependencies,
verifier, production public ABI and D# syntax unchanged.

Use fresh independent reference stores through a narrow adapter; scenario
assertions must inspect actual returned identities, states and frozen intents,
not a second implementation's internal data. Cover byte-exact targeted retry and
conflict with pinned selection; frozen publication and shared submission keys;
coordinated one-winner claims and stale-owner rejection; and cancellation winner
ordering. Use bounded barriers and controlled simulated time. Explicitly report
the supported in-process/byte-exact scope and assert durability, canonical
equality and atomic-follow-up requirements fail closed. Do not silently count
unsupported capabilities as passed provider conformance.

Run the same reusable stale-owner scenario against both the real adapter and a
deliberately broken test-only decorator that substitutes the live attempt and token;
assert a specific invariant failure from the scenario and inspect the forbidden
state mutation. This verifies the scenario detects a real violation without
changing production behavior or weakening trusted tests. Add no test fixture to
an old file and do not change the verifier.

Acceptance-to-test map: retry/conflict and frozen fan-out exercise identity;
concurrent claim and stale-token scenarios exercise ownership; cancellation
exercises state winner ordering; unsupported requirements exercise fail-closed
capabilities; the negative control demonstrates the same scenario detects a
forbidden mutation. Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-compiler.sh`, `bash scripts/verify-runtime.sh`, and
`git diff --check`. Record actual outcomes, including the local isolated-image
limit and hosted failure-pilot evidence when available.

This is a reusable subset for controlled in-process reference behavior, not
provider certification or evidence of canonical semantic equality, SQLite,
durability, restart, actual delivery, authorization, production clock behavior,
cleanup-fault aggregation or external-effect guarantees. The publication scenario
checks frozen identities and retries; it does not prove all-or-none behavior on a
failed multi-intent commit. #59 stays open.

Verification: bootstrap and documentation checks passed; all 31 automation
tests passed. Runtime verification passed 2 baseline and 55 feature tests,
including the negative control's expected specific violation and independently
observed forbidden commit. Compiler verification passed 18 baseline and 406
feature tests in a private short-path snapshot matching the final conformance
files. The unchanged 512,000-byte TRX guard accepted the 506,836-byte feature
report. The local isolated verifier could not pull its pinned MCR image
(`Forbidden`); hosted verification is still required for that path.

## Tenth slice: publication rollback and lost-ack scenarios

Base: `34cbec564a771d723120e3c04057d0cafe9f8ea0`, with #9/#14 and prior
#59 slices merged. Continue the existing issue branch. Extend the internal
`src/DSharp.Runtime/VolatilePublicationStore.cs` with a fixed, explicitly invoked
test failure point; add `tests/runtime-features/PublicationFailureConformanceTests.cs`
and update this plan and status. Existing tests,
dependencies, trusted verifier, public provider ABI and D# syntax stay unchanged.

The ordinary acceptance path must remain unchanged. A separate internal
conformance entry invokes the same acceptance core with a closed failure point
that throws a dedicated sentinel immediately after reserving a publication ID,
inside the existing rollback region and before publishing the complete frozen
record. No host callback runs under the gate. Validate unknown failure points
before mutation. A reusable scenario must observe the exact sentinel, confirm no
publication or cross-kind key is retained, then successfully reuse the same ID
and key for the full intended set. A test-only broken adapter deliberately
commits only a prefix before reporting the same failure; the same scenario must
reject its specific partial-record invariant and an independent backing-store
inspection must observe that real forbidden state.

Simulate a lost acknowledgement outside the store gate by discarding a
successfully committed response in the test adapter. A retry with changed
proposals must recover the original ID and complete frozen set; a changed request
must conflict. This is a response-loss simulation, not a crash or durable commit.
Test concurrent matching calls, throwing intent enumeration and unchanged normal
semantics without assuming which caller wins. Run `bash .github/ai/bootstrap.sh`,
`bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`,
`bash scripts/verify-runtime.sh`, and `git diff --check`; record actual results.

These tests exercise one in-process reference rollback boundary and simulated
response loss. They do not establish arbitrary mid-transaction failures,
durability, restart, real delivery, canonical equality, provider certification,
authorization or external-effect guarantees. #59 remains open.

Verification: bootstrap and documentation checks passed; all 31 automation
tests passed. Runtime verification passed 2 baseline and 59 feature tests,
including the expected partial-commit negative-control violation and independently
observed forbidden record. Compiler verification passed 18 baseline and 406
feature tests in a private short-path snapshot matching the final runtime source
and tests. The unchanged 512,000-byte TRX guard accepted the 506,836-byte feature
report. The local isolated verifier could not pull its pinned MCR image
(`Forbidden`); hosted verification is still required for that path.

## Eleventh slice: reusable lease and retry conformance

Base: `e904d33a57879bcbb0c51a47dfe1aa45d9654e5e`, with #9/#14 and prior
#59 slices merged. Continue the existing issue branch. Add the test-only
provider-neutral adapter and shared scenarios in
`tests/runtime-features/LeaseRetryConformanceHarness.cs` and the reference and
negative-control facts in `tests/runtime-features/LeaseRetryConformanceTests.cs`;
update this plan and status.
Preserve old tests, dependencies, verifier, production provider ABI and D# syntax.

Use fresh stores and controlled time. Shared scenarios must check an old observed
deadline after renewal, both before and after the renewed deadline; delayed retry
eligibility just before and at its boundary; fresh attempt/token identity, pinned
invocation and selection, and immutable retained lease-loss history. Bounded
concurrent recovery/claim and renewal/completion races must allow all coherent
serial outcomes rather than require a particular winner. Explicitly describe the
reference subset and reject unsupported guarantees instead of certifying them.

Run the same stale-deadline scenario against the real adapter and a deliberately
broken test-only decorator that substitutes the current deadline into the real
store's recovery call. After the renewed deadline has expired this causes a real
forbidden transition to eligible. Assert the specific stale-observation invariant
and independently inspect the backing state and lease-loss history; do not count
an unrelated exception or fabricated status as detection.

Acceptance-to-test map: old versus renewed observations and exact expiry exercise
fencing; delayed eligibility and fresh IDs exercise retry state; copied tagged
history and pinned selection exercise preservation; bounded races exercise
atomicity; the negative control proves the same scenario detects a real stale
recovery. Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-compiler.sh`, `bash scripts/verify-runtime.sh`, and
`git diff --check`; record actual results and isolated-image limits.

These tests cover controlled single-process reference behavior, not real provider
clock changes, configured retry policy, restart, durability, authorization,
multi-host fencing or external-effect guarantees. #59 remains open.

Verification: bootstrap and documentation checks passed; all 31 automation
tests passed. Runtime verification passed 2 baseline and 62 feature tests,
including the expected stale-observation violation and independently observed
forbidden recovery in the negative control. Compiler verification passed 18
baseline and 406 feature tests in a private short-path snapshot matching the
final test files. The unchanged 512,000-byte TRX guard accepted the 506,836-byte
feature report. The local isolated verifier could not pull its pinned MCR image
(`Forbidden`); hosted verification is still required for that path.

## Twelfth slice: reusable cancellation outcome conformance

Base: `85a64af272607fd03008f6f840f541d6b20cbf8d`, with #9/#14 and the
prior #59 slices merged. Continue the existing issue branch. Add a test-local
provider-neutral outcome adapter and shared scenarios in
`tests/runtime-features/OutcomeConformanceHarness.cs`, plus the volatile adapter
and negative-control facts in `tests/runtime-features/OutcomeConformanceTests.cs`.
Update this plan and the implementation status. Preserve existing tests,
production APIs, dependencies, trusted verifier and D# syntax.

Using fresh reference stores and controlled time, verify a cancellation requested
before an owned normal exit keeps the invocation cancelled, retains a copied
observed exit, never delivers it as the terminal completion, preserves accepted
identity, and remains idempotent. An opaque business-error-like payload is still
a normal completion, with no codec interpretation. Check the corresponding owned
reported-fault exit and supplied unverified origin, terminal-first completion and
fault outcomes, and exact expiry after cancellation without inventing an exit or
retry. Inspect retained snapshots and bytes to exercise copy boundaries.

Run the same cancellation/normal-exit scenario through a deliberately broken
test-only adapter that routes the cancellation to a real decoy invocation while
forwarding the target's completion to the real store. Require a named invariant
to catch the target's forbidden completed state and independently inspect both
the actual target completion and the decoy cancellation. No fabricated status or
unrelated failure counts as detection. Shared scenarios test declared capabilities
and unsupported guarantees. Existing shared tests retain bounded concurrency
coverage; this slice does not claim real provider, real-clock, delivery, durable,
authorization, fault normalization or cancellation-signal behavior. #59 remains
open.

Acceptance-to-test map: owned exit versus cancellation checks winner ordering,
observed exit and copied bytes; reported faults and origin check opaque retention;
expiry checks cancellation without a fabricated exit or retry; the real decoy
negative control demonstrates that the reusable scenario catches misrouted
cancellation. Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-compiler.sh`, `bash scripts/verify-runtime.sh`, and
`git diff --check`, recording actual results and isolated-image limits.

Verification: bootstrap and documentation checks passed; all 31 automation
tests passed. Runtime verification passed 2 baseline and 65 feature tests,
including the expected misrouted-cancellation violation and independently
observed target and decoy states. Compiler verification passed 18 baseline and
406 feature tests in a private short-path snapshot matching the final outcome
test files. The unchanged 512,000-byte TRX guard accepted the 506,836-byte feature
report. The local isolated verifier could not pull its pinned MCR image
(`Forbidden`); hosted verification is still required for that path.

## Thirteenth slice: fenced parent-aware Invoke admission

Base: `ad4f2a673f92d01abf6f174c44a823af849bcc93`, with #9/#14 and
prior #59 slices merged. Continue the existing issue branch. Add an internal,
opt-in parent-aware admission operation in a new partial runtime source file,
new feature tests in `tests/runtime-features/VolatileOwnedInvokeTests.cs`, and
update this plan and implementation status. Preserve legacy bookkeeping, existing
tests, dependencies, trusted verifier and public APIs.

For an Invoke only, require a same-application parent submission, nonempty parent
invocation/attempt/token IDs, and a child request whose parent strings equal the
canonical `Guid.ToString("D")` invocation and attempt IDs. Reject malformed or
mismatched input without mutation. Under the existing gate and one simulated clock
read, require the parent record to be Running with that exact unexpired
attempt/token before performing the existing shared-key acceptance atomically.
Pending cancellation alone does not revoke a still-current owner in this reference;
terminal state or `now >= leaseUntil` does. No callbacks, selection or external
work run under the gate. A still-current matching retry retains the first child ID
and selection; mismatches and cross-kind keys conflict. This opt-in operation
checks the owner before returning an existing record or inserting a new one.
A separately authorized host may use the existing request-aware probe to resolve
an uncertain acknowledgement, including after ownership loss; the probe itself
is not authorization. This local conservative API choice does not decide a public
status or retry protocol.

Map valid parent and immutable child metadata, matching and conflicting retry,
shared namespace, GUID collision and argument validation to new tests. Cover
cross-application/wrong-key/wrong-token, expired and superseded owners, and
terminal parent without creating a child. Bounded concurrent child submissions
and a parent terminal transition must permit either coherent serial outcome and
prove no partial child. Run `bash .github/ai/bootstrap.sh`,
`bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`,
`bash scripts/verify-runtime.sh`, and `git diff --check`; record actual results.

The earlier raw `Accept` remains unvalidated bookkeeping for compatibility and
is not an enforcement boundary. This addition proves only its explicit opt-in
path in a volatile single-process model; it is not host authorization, durable
admission, canonical encoding, parent completion plus atomic follow-ups, wait
cycle/permit handling, or fault-chain propagation. #59 stays open.

Verification: bootstrap, documentation and all 31 automation tests passed.
The first runtime run caught an xUnit analyzer error in the new race test;
after replacing blocking task-result access with awaits, runtime verification
passed 2 baseline and 69 feature tests. Compiler verification passed 18 baseline
and 406 feature tests in a private short-path snapshot matching the final new
runtime source; that snapshot predates the test-only await correction, which
was covered by the final runtime run. The unchanged 512,000-byte TRX guard
accepted the 506,836-byte feature report. The local isolated verifier could not
pull its pinned MCR image (`Forbidden`); hosted verification is still required.

## Fourteenth slice: provisional provider contract and conformance entry point

Base: `6514a031240ad458b1b238e1403638b5894baca1`, with #9/#14 and the
prior #59 slices merged. Continue the existing issue branch. Add an internal,
provisional provider-neutral operation contract and capability validation in
`src/DSharp.Runtime/RuntimeStoreContract.cs`; adapt the existing volatile store
without changing its prior concrete operations or tests. Add a new reusable
interface-driven scenario harness and volatile reference/negative-control tests
under `tests/runtime-features`, and update this plan and implementation status.
No public API, new dependency, old-test migration or verifier edit is authorized.

Expose the existing opaque request and snapshot types and atomic targeted and
publication probe/admission, parent-fenced Invoke, ownership, renewal, outcome,
recovery and cancellation operations through the internal contract. Keep the
legacy raw acceptance path explicitly named unvalidated: actor-parent validity
and host authorization cannot be inferred from it. Document transaction boundaries,
request/response snapshot ownership, lookup before reselection, fenced writes and
cancellation winner semantics, with no external work held across a transaction.
Expose an immutable capability descriptor and fail-closed known-bit requirement
validation. The volatile reference declares byte-exact comparison, in-process
atomic operations and controlled simulated time only; it rejects durability,
canonical semantic equality, atomic completion/follow-ups, restart/multi-host and
real-clock guarantees. Clock control and failure injection remain test fixtures,
not production provider operations. Success on this reference is never a durable
acknowledgement; a future durable provider may acknowledge only after its declared
commit. This internal synchronous shape is provisional, not the finalized provider
or D# protocol.

Run reusable scenarios directly against the interface returned by a factory, with
separately controlled reference clock: shared namespace and pinned retries, frozen
publication identity/intents, parent-fenced admission, claims/renewal/stale-token
and expiry boundaries, cancellation/outcome bookkeeping and unsupported capability
requests. Use fresh stores and bounded concurrency. A test-only interface decorator
must cause a real forbidden stale-observation recovery in the backing store; the
same scenario must catch its specific invariant and independent inspection must
confirm the wrong eligible state and retained history. Existing harnesses remain
unchanged. Explicitly record which guarantees the suite does not certify and leave
#59 open unless every acceptance criterion is actually proven. SQLite/restart,
codec and compatibility policy, fault-chain normalization and atomic follow-ups
remain with their respective owners.

Map each surface and advertised guarantee to the reusable tests. Run
`bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-compiler.sh`, `bash scripts/verify-runtime.sh`, and
`git diff --check`; record actual results and isolated-image limits.

Verification: bootstrap, documentation and all 31 automation tests passed.
Runtime verification passed 2 baseline and 71 feature tests, including the
interface-driven scenarios and the expected real stale-recovery negative control.
Compiler verification passed 18 baseline and 406 feature tests in a private
short-path snapshot matching the final contract source; the snapshot predates the
final scenario files, which were covered by the final runtime run. The unchanged
512,000-byte TRX guard accepted the 506,836-byte feature report. The local
isolated verifier could not pull its pinned MCR image (`Forbidden`); hosted
verification is still required for that path.

## Fifteenth slice: explicit blocked pinned implementation

Base: `5713dddbd4d268cd0c2dafe33b5b7ff283b61013`, with #9/#14 and prior
#59 slices merged. Continue the existing branch. Add provisional internal
provider-contract operations and volatile reference support for explicitly
blocking eligible work whose *pinned* implementation the host reports unavailable
or incompatible. Add new runtime feature tests and update this plan and status.
Preserve existing tests, dependencies, verifier and public APIs. This models the
DEC-04 blocked state without implementing host availability checks, authorization,
automatic reselection, compatibility interpretation or scheduling policy.

A caller can block only an eligible invocation after comparing the exact retained
selection and an eligibility-version observation. Assign each successful block a
fresh nonempty block ID that cannot be reused in the invocation/attempt/token ID
domain; roll back its reservation if the state update fails. Record the explicit
host-reported reason. Unblocking requires the active block ID and exact pinned
selection and creates a new eligibility version, so stale observations cannot
block or unblock a later eligible episode (including claim/reschedule ABA).
Propagate and checked-increment eligibility versions on each transition back to
eligible. Preserve original invocation, request, selection, prior history and retry
due time; no callback or selection runs under the gate. Claim rejects blocked
work. Cancellation of blocked work becomes immediately terminal and cannot be
undone by an unblock. Block/unblock, claim and cancellation serialize; running or
terminal work cannot be blocked. These operations are provisional internal
bookkeeping, not a public or durable provider contract.

Acceptance-to-test map: exact retained identity/selection and matching retry;
blocked claim rejection and restored eligibility/due/history; reported reason and
wrong selection; stale versions/blocks and ID collisions; blocked cancellation;
bounded competing block/claim and cancellation/unblock races with both valid
serial outcomes. Exercise the operation through IRuntimeStore as well as the
volatile reference. An explicit capability advertises this optional behavior;
other providers inherit fail-closed methods. No external compatibility check or
provider certification is claimed. Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-compiler.sh`, `bash scripts/verify-runtime.sh`, and
`git diff --check`. Record actual results and isolated-image limits. #59 remains
open until all its criteria are met.

Verification: bootstrap, documentation and all 31 automation tests passed.
Runtime verification passed 2 baseline and 75 feature tests. Its build retried a
transient file-copy contention warning caused by concurrent local verification;
the retry succeeded. Compiler verification passed 18 baseline and 406 feature
tests in a private short-path snapshot matching the final changed runtime sources
and feature test. The unchanged 512,000-byte TRX guard accepted the 506,836-byte
feature report. The local isolated verifier could not pull its pinned MCR image
(`Forbidden`); hosted verification is still required for that path.

## Sixteenth slice: structured fault record retention

Base: `405663965096616e21ffc1e62b059391db88eefa`, with #9/#14 and prior
#59 slices merged. Continue the existing branch. Extend the internal opaque fault
value with an optional immutable caller-normalized report in new runtime source;
adapt the provisional capability declaration and existing terminal-fault value
only as needed for consistent origin retention. Add new runtime feature tests,
and update this plan and status. Existing opaque-only operations/tests remain
compatible; no public fault syntax, codec, dependency or verifier is selected.

Carry an optional opaque public-target routing key as an explicit copied field in
the targeted request, preserving the difference between absent and present-empty;
include it in request identity/conflict comparisons and retain it through attempts.
No key encoding, scheduler, ordering policy or public syntax is selected.

The report contains a caller-supplied optional primary observation, an ordered
immutable list of cleanup observations, and an optional nonempty original
invocation ID. At least one observation is required. Each observation has a
nonblank caller-supplied classification and copied opaque diagnostic bytes and
metadata. Construct and validate/copy the report outside the store gate; the store
never reflects on CLR exceptions or runs caller enumeration under a transaction.
Preserve supplied origin and distinguish unknown origin; reject conflicting
explicit terminal versus report origins. A cleanup-only report is a fault, while
cleanup observations never replace an existing primary. Integrate this immutable
value into the existing fenced terminal, retry-history and cancellation-observed
fault paths without changing their winner, ownership, expiry or retry behavior.
Expose an explicit optional capability for storing structured reports; do not
claim the store has normalized, redacted, authenticated or propagated the caller's
input. Host normalization/authorization, actual cleanup execution, fault-chain
propagation (#243), codec and durable/restart proof remain with their owners.

Acceptance-to-test map: absent, empty and populated routing keys, copied bytes,
conflicts and retention across retry; primary plus multiple cleanup and cleanup-only reports,
missing/invalid components/origin and conflict rejection; copied input/output
buffers and retained snapshots; same invocation/selection and exact report/origin
through reschedule and fresh retry; terminal and cancellation-winner inspection;
wrong/stale/expired owners and bounded competing outcomes. Exercise operations
through the provisional interface and explicitly state legacy opaque-only behavior.
Add a reusable structured-fault provider profile that requires the capability,
checks report/origin retention through terminal, retry and cancellation paths,
and catches a real report-stripping fault adapter by inspecting backing state.
The legacy opaque path remains for compatibility; a caller seeking this profile
must provide a structured report and require the corresponding capability.
Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-compiler.sh`, `bash scripts/verify-runtime.sh`, and
`git diff --check`; record actual outcomes and image limits. Close #59 only when
all four actual issue acceptance criteria are demonstrated and the draft merges;
SQLite implementation is explicitly downstream (#60–#64), not a prerequisite.

### Issue #59 acceptance evidence and closure boundary

1. Atomic contract and distinct invocation/attempt identities: the provisional
   `RuntimeStoreContract.cs` states transaction, capability and acknowledgement
   boundaries; `RuntimeStoreContractScenarios.cs`, `RuntimeStoreConformanceHarness.cs`
   and the ownership, lease/retry, blocked and cancellation tests exercise
   admission, frozen publication, claims, renewal, outcomes and stale fencing.
2. Retained input, target/key, selection, parent attempt, result metadata and
   structured fault/origin: targeted admission/owned Invoke and target-key tests,
   `VolatileTargetedCompletionTests.cs`, and the structured fault reference and
   provider-profile scenarios check copying and retention. Full structured-fault
   conformance requires callers to supply a report and providers to advertise
   `StructuredFaultReports`; opaque-only legacy calls are not that profile.
3. Stable `(publication, subscription)` identity and uncertain acknowledgement:
   `PublicationFailureConformanceTests.cs` tests the frozen set, retry and fixed
   rollback boundary, including a real partial-record negative control. This is
   in-process failure simulation, not crash recovery evidence.
4. Deterministic reference and reusable conformance: the interface, admission,
   lease, outcome, structured-fault and publication scenario suites exercise
   idempotence, concurrent claims, stale owners, frozen fan-out, state transitions
   and explicit unsupported guarantees. The contract states that a durable provider
   can acknowledge only after its declared commit; the volatile provider never
   advertises durability.

These criteria describe an internal reference contract, not actual provider
certification. #60–#64 own SQLite and restart proof; #78 owns wire encoding and
compatibility interpretation; #243 owns fault-chain propagation; #65/#66 own
ordering policy; #68 owns atomic completion/follow-ups. The host owns actual
normalization, authorization, selection and execution. Closing #59 after a verified
merge does not claim any of those downstream behaviors.

Verification: bootstrap, documentation and all 31 automation tests passed.
The first combined runtime run exposed an ambiguous new request-constructor
overload; requiring the three new-overload arguments preserved the old calls.
After that correction runtime verification passed 2 baseline and 84 feature tests,
including the structured-report profile and real report-stripping negative control.
Compiler verification passed 18 baseline and 406 feature tests in a private
short-path snapshot matching all final changed runtime sources and feature tests.
The unchanged 512,000-byte TRX guard accepted the 506,836-byte feature report.
The local isolated verifier could not pull its pinned MCR image (`Forbidden`);
hosted exact-head verification is still required.
