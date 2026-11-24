# Issue #60: SQLite actor schema and migrations

## Completion audit plan

Base `c44cac8936a5b9c7156ecfef4435920aa4bdf0bc` includes all twelve #60
slices and the real-provider reference-model round trip. Reconcile the live
#60 acceptance criteria against the merged schema and named tests below. Update
this plan and the implementation status only; make no production, test,
dependency or verification changes. Run bootstrap, full verification, SQLite,
runtime and compiler scripts; record exact results and request hosted exact-head
checks. The completion draft may close #60 only on a subsequent eligible merge.

The earlier per-slice statements that #60 remains open describe their historical
partial scope. In particular, actual durable admission and acknowledgement are
owned by #61, while resolving a recorded implementation and reporting a missing
or incompatible one are owned by #64. Both are downstream of #60: requiring
their completed implementations to close the schema issue would reverse the
documented dependency order. This audit does not declare either downstream issue
or the shared #101 integration checks complete.

### Acceptance-to-evidence map

1. **Invocation, attempt, publication and delivery records, opaque contracts,
   outcomes and ancestry.** `ActorSqliteSchema` defines application-scoped
   submission, invocation, attempt, publication and delivery tables, v2 attempt
   exits and ordered fault observations, and v3 lifecycle/recovery records.
   `ActorSqliteSchemaTests.SchemaIsIdempotentAndPreservesOpaqueBytesAndSelectionOnReopen`,
   `AttemptsAncestryAndFrozenDeliveryRelationshipsRemainDistinct`,
   `ActorSqliteOutcomeTests.AttemptExitsPreserveNormalFaultAndDistinctOriginsWithOrderedObservations`
   and `ActorSqliteLifecycleTests.RecoveryHistoryKeepsFaultAndLeaseLossDistinctAndOrdered`
   exercise those stored shapes with real SQLite. Requested and selected IDs
   are stored as caller-supplied identities; no inference from source/display
   names or host registry behavior is claimed.
2. **Uniqueness and transaction/version helpers.** `SharedCaseSensitiveKeysAndKindsEnforceOwnership`
   and `AttemptsAncestryAndFrozenDeliveryRelationshipsRemainDistinct` verify
   shared submission keys, distinct attempts and publication/subscription
   delivery identities and relationships. `ActorSqliteWriteBatchTests` covers
   committed linked writes, rollback on later constraint or row-count failure,
   and an actual deferred-foreign-key commit failure. The version coordinator
   rejects unsupported versions; its caller-owned transaction is covered by
   `ActorSchemaMigrationTransactionTests` and the real SQLite inactive-transaction
   test. These helpers are substrate, not #59 provider conformance.
3. **Fresh setup, reopen, atomic failure and upgrades.**
   `ActorSqliteApplicationUpgradeTests.FreshFileIsInitializedAndBoundAndInvalidIdCreatesNothing`,
   `PopulatedV1AndV2UpgradeAndBindWithoutLosingRecords`,
   `LaterDdlConflictRollsBackIntermediateUpgradeAndBinding` and
   `UnversionedNonemptyNewerAndCorruptFilesFailWithoutMutation`, along with the
   v2/v3 migration-failure tests, exercise supported setup and upgrades with
   real files and prove rollback and unsupported-version rejection.
4. **Application isolation, selected settings and preserved records.**
   `ActorSqliteDatabaseTests` checks the file settings; the v4 binding tests
   verify wrong/mixed IDs, guard enforcement and competing binders.
   `ActorSqliteProcessRestartTests.CommittedActorRecordsSurviveWriterProcessExit`
   verifies two separate application files, settings, selected IDs, ancestry
   and stored fault/lifecycle data after a writer process exits.
   `ActorSqliteReferenceRoundTripTests.ActualAcceptedReferenceRecordsCanBeStoredAndReadAfterWriterExit`
   uses accepted #59 reference records and a **test-owned** SQLite mapping,
   then reads their pinned identities and encoded data after the writer exits.
   This proves the schema can preserve those records, not that an SQLite
   admission path exists. #61 owns that path; #64 owns actual compatibility
   decisions and blocked/failed reporting, and #243/#71 own diagnostic chains
   and inspection. The host remains responsible for application-to-path mapping
   and file provenance. The current bound path rejects NUL and unpaired UTF-16
   application IDs and does not defend against deliberate trusted-schema edits.

This is normal committed-file persistence and atomic setup evidence, not a
claim of power-loss recovery, at-least-once execution, codec correctness,
authorization or a complete durable `RuntimeStore`. Those remain downstream.
Hosted CI #353 and Compiler #346 passed on the merged #313 head; the trusted
SQLite run executed the baseline and 52 feature tests and rejected its named
deliberate failure before a clean rerun. A new hosted run is still needed for
the completion draft itself. #60 remains open until that draft is merged.

### Completion-audit verification

- Bootstrap and documentation checks (135 files, 309 local links) passed, as
  did all 46 automation tests. No implementation or test files changed.
- Real SQLite passed 1 baseline and 52 feature tests, including both process
  tests. Runtime passed 2 baseline and 96 feature tests.
- Compiler passed 18 baseline and 406 feature tests from a private short-path
  snapshot, with the unchanged report guard enforced (506,836-byte feature TRX).
- The full isolated verifier and exact-base isolated SQLite verifier could not
  complete locally because the pinned MCR image pull returned `Forbidden`.
  Hosted exact-head checks, including the SQLite failure/clean pilot, remain
  required for this draft.

## Twelfth slice: round-trip actual reference-model records across processes

Base `9aabb290a9939bafb3ba05debf343e65e2d435d1` contains the application-bound
SQLite schema and readers. Continue the #60 branch and add a real-provider
feature test in `tests/sqlite-features/ActorSqliteReferenceRoundTripTests.cs`.
Its separate writer process accepts targeted and publication requests using the
merged #59 volatile reference model, then explicitly maps the returned records
into SQLite in a test-owned transaction and exits. A second process opens the
same file, uses the existing bound readers and verifies the stored invocation,
publication and each frozen delivery identity and all opaque request data.
Use reflection in the test only to reach the internal #59 reference from the
separate SQLite test assembly; do not change friend assemblies or test projects.
The helper maps data for this test and is not a SQLite admission implementation.

This verifies that actual accepted #59 reference records can be represented and
survive normal writer-process exit, complementing earlier direct SQL and
cross-process tests. It does not prove atomic admission, deduplication, host
selection/availability, power-loss recovery or a durable RuntimeStore provider;
#61, #64 and #69 retain those boundaries and #60 stays open until its full
acceptance. Preserve all previous tests, schema and trusted verification. Run
bootstrap, full verification, SQLite, runtime and compiler scripts, and the
exact-base isolated SQLite verifier where the pinned image is available. Record
actual results and status without claiming untested runtime behavior.

### Twelfth-slice verification

- Bootstrap and documentation checks (135 files, 308 local links) passed, as
  did all 46 automation tests.
- Real SQLite passed 1 baseline and 52 feature tests, including the new test
  that accepts targeted/publication records in the reference writer process,
  maps them in a test-owned transaction and reads them after that process exits.
  Runtime passed 2 baseline and 96 feature tests.
- Compiler passed 18 baseline and 406 feature tests from a private short-path
  snapshot, with the unchanged report guard enforced (506,836-byte feature TRX).
- The full isolated verifier and exact-base isolated SQLite verifier could not
  complete locally because the pinned MCR image pull returned `Forbidden`.
  Hosted exact-head verification, including the SQLite failure/clean pilot,
  remains required before merge. This evidence does not implement admission,
  host availability reporting, provider conformance or power-loss recovery;
  #60 remains open.

## Eleventh slice: read preserved publication and delivery identities

Base `376490463e85e3252b2bff86a2ebed085d79167a` includes the application-bound
v4 schema, pinned invocation reader and normal cross-process persistence test.
Continue the #60 branch. Add an internal, parameterized bound-file reader for
one saved publication and one exact `(publication, subscription)` delivery.
The publication snapshot retains its submission identity and copied opaque
contract, payload and options even when no deliveries exist. The delivery
snapshot retains its frozen requested and selected implementation IDs; absence
of a specific delivery is distinct from an existing empty publication. Each
query is a separate SQLite snapshot, not a multi-query atomic view or an
enumeration/fanout API. Keep the host's path/provenance responsibilities and
the bound opener's existing behavior; never reselect an implementation.

Add `src/DSharp.Runtime.Sqlite/ActorSqlitePublicationReader.cs` and real-provider
tests in `tests/sqlite-features/ActorSqlitePublicationReaderTests.cs` covering
reopen, opaque bytes and defensive copies, multiple pinned identities, a real
zero-delivery publication, missing IDs, parameterized apostrophes, wrong
application and missing-file rejection. These are schema/reopen reads, not
publication acceptance, registry selection, delivery execution, a codec or
durable acknowledgement; #69 owns fanout and #60 remains open pending its full
acceptance. Preserve prior tests, schema, trusted verification and protections.

Run bootstrap, the full verifier, SQLite, runtime and compiler scripts, and the
exact-base SQLite verifier when its pinned image is available. Record actual
results and precisely tested behavior in this plan and the status document.

### Eleventh-slice verification

- Bootstrap and documentation checks (135 files, 308 local links) passed, as
  did all 46 automation tests.
- Real SQLite passed 1 baseline and 51 feature tests, including the new
  publication and delivery reopen test and the previous cross-process test.
  Runtime passed 2 baseline and 96 feature tests.
- Compiler passed 18 baseline and 406 feature tests from a private short-path
  snapshot, with the unchanged report guard enforced (506,836-byte feature TRX).
- The full isolated verifier and exact-base isolated SQLite verifier could not
  complete locally because the pinned MCR image pull returned `Forbidden`.
  Hosted exact-head verification, including the SQLite failure/clean pilot,
  remains required before merge. These reads do not establish admission, host
  availability, fanout or power-loss recovery; #60 remains open.

## Tenth slice: verify file persistence across independent processes

Base `661282f31be397916b34133ab65f889f9ccf5636` provides the application-bound
v4 schema and pinned-work reader. Continue the #60 branch. Add a real SQLite
feature test, `tests/sqlite-features/ActorSqliteProcessRestartTests.cs`, whose
parent test starts a writer process, waits for its successful exit and then starts
a separate reader process against the same local files. Each child runs just the
same test in an explicitly selected phase, with bounded time and an unambiguous
completion marker. Keep all existing tests, project files, trusted verification,
schema and production behavior unchanged.

The writer creates two separately bound application files and commits model-shaped
targeted invocation, attempt, lifecycle/fault and publication/delivery records.
The reader verifies selected IDs and opaque encoded data using the bound reader,
and verifies the remaining persisted records and settings with real SQLite after
the writer process has exited. It checks cross-application rejection and an empty
publication as distinct from a missing one. This proves normal committed-file
persistence across processes, not an actual accepted SQLite admission protocol,
power-loss recovery, host availability reporting or durable acknowledgement;
those boundaries remain with #61–#64 and #60 stays open until full acceptance.

Run bootstrap, the full verifier, SQLite, runtime and compiler scripts, and the
base-owned SQLite verifier when its pinned image is available. Record actual
results and update the implementation status only for the observed behavior.

### Tenth-slice verification

- Bootstrap and documentation checks (135 files, 308 local links) passed, as
  did all 46 automation tests.
- Real SQLite passed 1 baseline and 50 feature tests. The separate writer and
  reader process test also passed when rerun with `DOTNET_PROCESSOR_COUNT=2`.
  Runtime passed 2 baseline and 96 feature tests.
- Compiler passed 18 baseline and 406 feature tests from a private short-path
  snapshot, with the unchanged report guard enforced (506,836-byte feature TRX).
- The full isolated verifier and exact-base isolated SQLite verifier could not
  complete locally: the pinned MCR image pull returned `Forbidden`. Hosted
  exact-head isolated checks, including the SQLite failure/clean pilot, remain
  required. This evidence covers orderly process exit, not abrupt failure or
  SQLite admission; #60 remains open.

## Ninth slice: read preserved pinned work after reopen

Base `c45734fe0dc92be9cfadf6b6fe3b42e51fb18d4b` contains the application-bound
v4 schema. Continue the #60 branch and add an internal read-only SQLite
projection of saved invocation identities and their original encoded inputs, with
the requested and selected actor IDs, target key, operation and optional awaited
parent/attempt. Targeted invocations resolve their submission directly; delivery
invocations resolve it through their saved publication. Open the existing bound
file for the specified application and read one caller-specified invocation through
a parameterized, application-scoped query. Materialize immutable copies before returning, keeping the host responsible
for file provenance and using only stored IDs, never reselecting an implementation.
This is a schema/reopen substrate, not an availability decision, execution,
admission, claim, codec, authorization, or complete RuntimeStore provider.

Add `src/DSharp.Runtime.Sqlite/ActorSqlitePinnedWorkReader.cs` and a real-provider
test in `tests/sqlite-features/ActorSqlitePinnedWorkReaderTests.cs`. Verify that
targeted and publication-derived rows retain their distinct identities, encoded
contracts/payload/options, target keys and ancestry after all original connections
close; a different or missing application/file must be rejected, a zero-delivery
publication must not create work, and callers cannot mutate later reads by changing
returned bytes. Keep #60 open until its full acceptance, including actual host
availability reporting, is proven; #61–#64 own operational work.

Run bootstrap, the full verifier, SQLite, runtime and compiler scripts, and the
base-owned SQLite verifier when its pinned image is available. Keep trusted tests,
verification and the established application-bound opener unchanged. Record exact
results and limitations in this plan and implementation status.

### Ninth-slice verification

- Bootstrap and documentation checks (135 files, 308 local links) passed, as
  did all 46 automation tests.
- The real SQLite suite passed 1 baseline and 49 feature tests; runtime passed
  2 baseline and 96 feature tests. The new SQLite test covers bound reopen,
  original targeted and publication data, null/empty target distinction,
  case-sensitive wrong-application rejection, absent files and defensive copies.
- Compiler passed 18 baseline and 406 feature tests from a private short-path
  snapshot, with the unchanged report guard enforced (506,836-byte feature TRX).
- The full isolated verifier and the unchanged base-owned isolated SQLite
  verifier could not complete locally because the pinned MCR image pull returned
  `Forbidden`. Hosted exact-head isolated checks, including the SQLite named
  failure/clean pilot, remain required before merge. This slice does not prove
  actual availability reporting, execution or crash durability; #60 stays open.

## Eighth slice: atomic fresh/older-file upgrade and binding

Base `423e75b7222fcde77414a5474807f17faac97df5` includes explicit v1–v3
schemas and opt-in v3-to-v4 application binding. Continue the existing #60
branch. Add an internal caller-owned-transaction migration entry point to
`src/DSharp.Runtime/ActorSchemaMigrationCoordinator.cs`, with additive core tests
in `tests/runtime-features/ActorSchemaMigrationTransactionTests.cs`. It requires
a live transaction on the supplied open connection, checks connection identity,
and relies on the provider to reject inactive transactions. It performs the same
version checks/migrations inside the transaction and never commits, rolls back or
disposes it. The existing coordinator entry point retains its current behavior
and trusted tests.

Use that entry point from an explicit new fresh/older-file binding path in
`src/DSharp.Runtime.Sqlite/ActorSqliteSchema.cs`,
`ActorSqliteApplicationBinding.cs` and `ActorSqliteDatabase.cs`; add real-provider
tests in `tests/sqlite-features/ActorSqliteApplicationUpgradeTests.cs`. Under a
single immediate SQLite transaction, validate existing v1/v2/v3 actor identities
and foreign keys, upgrade their schema to v3, install the v4 application binding
and guards, and commit. An empty unversioned database can follow the same path;
a nonempty unversioned database, wrong/mixed IDs, corruption or unsupported newer
version must fail without partial schema/version changes. Existing v3-only
binding and v1–v3/default opener contracts remain unchanged. Validate an app ID
before file creation. Keep the v4 identity limitations and host responsibility
for path/provenance from the preceding slice; do not claim authentication,
malicious-schema resistance, actual host availability, admission or crash
durability. #60 remains open pending its full acceptance criteria.

Real SQLite tests cover fresh creation, populated v1 and v2 upgrades retaining
records, same/different-app reopen, wrong and mixed identities without intermediate
upgrade, invalid IDs before file creation, nonempty unversioned/newer/corrupt files,
same/different concurrent first binders, and a real later-DDL conflict that rolls
back both intermediate migrations and binding. Core tests verify caller-owned
transaction behavior, connection mismatch and no implicit commit/rollback/dispose.
Preserve all prior tests, old entry points, trusted verifiers and safeguards.

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-sqlite.sh`, `bash scripts/verify-runtime.sh`,
`bash scripts/verify-compiler.sh`, the base-owned isolated SQLite verifier where
the pinned image is available, and `git diff --check`. Record actual results.

### Eighth-slice verification

- Bootstrap, documentation checks (135 files and 308 local links), and all 46
  automation tests passed.
- The real SQLite suite passed 1 baseline and 48 feature tests, including the
  inactive-transaction rejection; the competing-upgrade test also passed five
  additional runs. Runtime passed 2 baseline and 96 feature tests.
- Compiler passed 18 baseline and 406 feature tests from a private short-path
  snapshot, with the existing report guard enforced (506,836-byte feature TRX).
- The full isolated verifier and base-owned isolated SQLite verifier could not
  complete locally: the pinned MCR image pull returned `Forbidden`. Hosted
  exact-head isolated verification, including the SQLite failure-detection pilot,
  remains required before merge. These local results do not prove durability or
  host availability; #60 remains open.

## Seventh slice: atomic per-file application binding

Base `b9afd21458d0e6dbf5a324e66ee1f08b5d13bc46` includes the v1–v3 schemas
and internal transaction helper. Continue the existing #60 branch. Add an
explicit, internal version-four binding for an already-version-three file in
`src/DSharp.Runtime.Sqlite/ActorSqliteApplicationBinding.cs`, with an internal
bound-file opener in `ActorSqliteDatabase.cs` and additive real-provider tests in
`tests/sqlite-features/ActorSqliteApplicationBindingTests.cs`. Existing v1–v3
openers and trusted tests retain their exact behavior and reject newer files.
Earlier versions remain upgradeable to v3 through their existing explicit
path; binding does not claim their upgrade and v4 binding are one transaction.

On a caller-owned, exclusively used open v3 connection with foreign keys enabled,
DELETE journal and FULL synchronous settings, take an immediate write transaction.
Recheck these settings after acquiring the lock, then validate the schema version, every existing
actor-table application identity and foreign-key integrity before installing a
singleton application binding and fixed, trusted insert/update guards on the
actor tables. The binding is immutable through ordinary row operations. Advance
`user_version` to four and commit as one transaction; any mismatch or failure
rolls back the binding, guards and version. Rebinding the same application is
idempotent; a different or mixed application is rejected. A bound-file opener
validates the application ID before opening an existing file, uses the same
connection settings as the existing opener, and rejects missing, mismatched or
unsupported bindings without creating a new file. Caller IDs are parameterized
data, compared case-sensitively. This opt-in slice rejects IDs containing NUL or
unpaired UTF-16 rather than risk SQLite TEXT changing their identity; it does not
claim to support every string accepted by the in-memory reference. Validation
checks stored text and cannot reconstruct an identity already changed before it
was stored; the host remains responsible for existing-file provenance. Bound
reopen takes an immediate lock while it scans stored actor IDs, guards and foreign
keys, so it can contend with writers. Preserve errors across rollback/disposal and
treat commit exceptions as uncertain.

Guards apply even if a later connection disables foreign keys. They protect
ordinary row changes, not malicious changes to the trusted schema, triggers or
filesystem. The host still selects local paths and owns process-level policy;
this is not admission, host registration, cross-host coordination, power-loss
recovery or durable acknowledgement. #60 stays open until its full acceptance,
including actual unavailable/incompatible implementation reporting, is proven;
#61–#64 retain operational behavior.

Real SQLite tests cover matching populated v3 binding and v4 reopen, case-sensitive
mismatch and mixed-application rejection without a version/data change, an empty
v3 file, idempotent rebinding, unsupported versions and missing paths, and
contention between two first binders. Exercise wrong-application inserts and
updates with foreign keys disabled, singleton replacement/update/delete attempts,
preserved prior v1/v2/v3 records, rejection of unsupported connection settings,
and a deliberate v4 DDL conflict proving rollback.
Keep all previous tests, version targets and the trusted verifier unchanged.

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-sqlite.sh`, `bash scripts/verify-runtime.sh`,
`bash scripts/verify-compiler.sh`, the base-owned isolated SQLite verifier where
the pinned image is available, and `git diff --check`. Record actual results.

### Seventh-slice verification

Bootstrap, documentation (135 files and 308 local links) and all 46 automation
tests passed. Local real-provider SQLite verification passed one baseline and
41 feature tests, including mixed-ID rejection, missing guards, unsupported
settings, all actor-table row guards with foreign keys disabled, and transactional
DDL rollback. The competing-binder test passed five additional runs. Runtime
verification passed two baseline and 93 features from a snapshot matching runtime
source and tests. Compiler verification passed 18 baseline and 406 feature tests
from a short-path snapshot matching the final SQLite source and tests; its
506,836-byte feature TRX passed the unchanged size guard.

The full isolated verifier and base-owned SQLite verifier could not pull the
pinned MCR image locally (`Forbidden`), so neither completed offline tests here.
Hosted exact-head verification must provide that evidence; prior tests and the
trusted verifier remain unchanged. These tests do not establish power-loss
durability, host path selection or protection against trusted-schema tampering.

## Sixth slice: atomic SQLite write batches

Base `c45d612c037bab68ab40c13ae96b39f03ff05c66` contains the versioned
identity, outcome and lifecycle tables. Continue the existing #60 branch. Add
an internal batch helper in `src/DSharp.Runtime.Sqlite/ActorSqliteWriteBatch.cs`
and additive real-provider tests in
`tests/sqlite-features/ActorSqliteWriteBatchTests.cs`. It accepts an already-open
caller-owned SQLite connection and a pre-materialized batch of trusted,
transaction-compatible single SQL statements with named parameters. Before the
transaction begins, validate and copy the batch, names, expected affected-row
counts and supported null/string/integer/BLOB values, including cloning BLOBs.
Require foreign-key enforcement on the open connection, then bind each command
and all parameters to one transaction, execute in order,
and return counts only after successful commit. An expected-count mismatch is a
failure and rolls back the batch. The helper does not invoke host callbacks or
enumerate caller objects while the transaction is active, and it does not close
the connection. The caller must exclusively use the non-thread-safe connection
throughout the operation.

The SQL is trusted internal input. Statements must not control transactions,
attach databases, modify transaction-affecting settings or perform external
effects. Connection-registered functions and schema triggers are also trusted;
SQLite can invoke them when executing a statement. These preconditions are not
enforced by an incomplete SQL parser and the helper is
not an application SQL interface. On execution or count failure, attempt
rollback, preserve the original error and any rollback/disposal errors, and never
report success when commit or disposal fails. Commit or post-commit disposal
exceptions may leave the outcome uncertain, so a caller must re-read before
retrying. This helper does not implement read-check-write admission, ownership
fencing, host callbacks, a public business transaction or durable acknowledgement.
Host routing still owns choosing a distinct local file per application; no
in-file application binding is claimed.

Real SQLite tests cover a committed linked multi-row batch and reopen, rollback
on a later constraint failure and expected-count mismatch, a usable caller
connection after failure, parameterized SQL-shaped text and null/empty BLOB
distinctions, immutable batch snapshots, duplicate/invalid input before a
transaction, independent files, and a deferred foreign-key failure at commit
that verifies rollback and connection usability. Preserve old schema/test behavior
and the trusted verifier. Real SQLite proves the tested atomic behavior, not hardware
power-loss or commit-acknowledgement semantics. #60 remains open until its
remaining host/availability and other acceptance criteria are demonstrated;
#61–#64 own admission, claims and execution.

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-sqlite.sh`, `bash scripts/verify-runtime.sh`,
`bash scripts/verify-compiler.sh`, the base-owned isolated SQLite verifier where
the pinned image is available, and `git diff --check`. Record actual results.

### Sixth-slice verification

Bootstrap, documentation (135 files and 308 local links) and all 46 automation
tests passed. Local real-provider SQLite verification passed one baseline and
31 feature tests, including rollback after an actual deferred-foreign-key failure
at commit and subsequent connection reuse. Runtime verification passed two
baseline and 93 features from a snapshot matching runtime source and tests.
Independent review checked input snapshots, trusted-connection boundaries and
cleanup; the final implementation attempts transaction disposal in `finally`
before allocating any aggregate exception.
Compiler verification passed 18 baseline and 406 feature tests from a
short-path snapshot matching the final SQLite source and tests; its 506,836-byte
feature TRX passed the unchanged size guard.
The full isolated verifier and base-owned SQLite verifier could not pull the
pinned MCR image locally (`Forbidden`), so neither completed offline tests here.
Hosted exact-head verification must provide that evidence; prior tests and
the trusted verifier remain unchanged.

## Fifth slice: opt-in version-three lifecycle storage

Base `3ae335353beb8f9142236f60ea7c5c5279e6f53e` includes the explicit v1/v2
schema targets and attempt-exit tables. Continue the existing #60 branch. Add a
caller-selected version-three migration through the same coordinator and opener,
preserving the existing v1 and v2 targets, defaults, rejection behavior, prior
tests and trusted verification. This is an internal storage substrate, not a
provider, transition API or change to the default host schema.

Add raw invocation lifecycle and ordered attempt-recovery tables in
`src/DSharp.Runtime.Sqlite/ActorSqliteSchema.cs`, with an additive
`tests/sqlite-features/ActorSqliteLifecycleTests.cs`. Persist the documented
eligible, running, blocked, completed, reported-fault, lease-expired and cancelled
states, eligibility revision/time, cancellation marker, an optional paired
attempt/token/lease tuple, and optional block identity/reason. Use the pinned
selected actor already stored on the invocation; do not reselect or authenticate
it. Cancellation before claim has no tuple; after claim, completed, faulted,
lease-expired or cancelled states can retain one. A cancellation of a blocked
invocation can retain the block. Represent recovery history separately with
ordered reported-fault versus lease-expiry tags and the observed ownership/time
data; lease expiry alone must not manufacture an actor fault. Store time as UTC
ticks in integer fields and document that choice as internal SQLite representation.
Keep checks limited to structural facts supported by the #59 model. The future
writer must enforce token/attempt/block freshness, valid transitions and
monotonic revisions, reporting a pinned implementation's actual availability,
fencing, history linkage, timestamp interpretation and durable operations.
The raw ordinal permits gaps; a future writer must enforce contiguous history.

Real SQLite tests cover populated v1/v2-to-v3 upgrades, selected identities and
prior outcome preservation, explicit target rejection and v1/v2 fail-closed
behavior on v3, and idempotent reopen. Exercise cancellation before and after an
attempt, terminal rows retaining the ownership tuple, blocked cancellation,
lease-expiry without a fault, ordered recovery tags, malformed structural shapes
and cross-invocation foreign keys. Force a real second-v3-statement conflict and
prove the earlier statement and schema version roll back while existing records
survive. #60 stays open for actual host/availability reporting, transaction and
durability criteria; #61–#64 own admission, claim and execution behavior.

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-sqlite.sh`, `bash scripts/verify-runtime.sh`,
`bash scripts/verify-compiler.sh`, the base-owned isolated SQLite verifier where
the pinned image is available, and `git diff --check`. Record actual results.

### Fifth-slice verification

Bootstrap, documentation (135 files and 308 local links), and all 46 automation
tests passed. Local real-provider SQLite verification passed one baseline and
23 feature tests, covering the upgrade, rollback, cancellation/terminal/block
shapes, distinct recovery tags and UTC tick boundaries. Runtime verification
passed two baseline and 93 feature tests from a snapshot matching runtime source
and tests. Independent review found no further scoped defect.
Compiler verification passed 18 baseline and 406 feature tests from a
short-path snapshot matching the final SQLite source and tests; its 506,836-byte
feature TRX passed the unchanged size guard.
The full isolated verifier and base-owned SQLite verifier could not pull the
pinned MCR image locally (`Forbidden`), so neither completed offline tests here.
Hosted exact-head verification must provide that evidence; prior tests and
the trusted verifier remain unchanged.

## Fourth slice: opt-in version-two attempt-exit storage

Base `236911a9cc90063cbc67907238506aad1d2f7040` includes the version-one
identity schema, its file opener and trusted SQLite tests. Continue on the
existing #60 branch. Preserve `ActorSqliteSchema.Apply(connection)` and
`ActorSqliteDatabase.Open(path)` as explicit version-one compatibility paths:
they reject newer files. Add an internal caller-selected version target for
version two, using the same transactional coordinator and the same file
settings; never select a target based on tests or environment. Validate an
unsupported target before opening or creating a file. Version two is opt-in and
does not change the default host integration or any existing trusted test.

Add a version-two migration in `src/DSharp.Runtime.Sqlite/ActorSqliteSchema.cs`
and target selection in `src/DSharp.Runtime.Sqlite/ActorSqliteDatabase.cs`, with
new real-provider tests in `tests/sqlite-features/ActorSqliteOutcomeTests.cs`.
Store per-attempt normal versus reported-fault exits separately, as opaque payload
and nonempty contract metadata, with an optional unverified reported fault origin.
Retain optional caller-normalized reports separately from an absent report,
including their own optional unverified origin, an optional primary observation
and ordered cleanup observations. Diagnostic and observation metadata bytes may
be empty. A cleanup-only report is valid. An attempt exit is not automatically an
invocation terminal outcome: cancellation can win while a later exit is recorded,
and retry history and lease loss must remain distinct. Raw rows do not provide
fencing, atomic acceptance, normalization, redaction, typed origin parsing and
equality, nonblank classification, contiguous cleanup ordering, report-child
existence or fault-chain
propagation; future writers must
enforce the relevant invariants. Do not implement those downstream operations or
claim a codec, production execution or crash durability.

Real SQLite tests cover fresh v0-to-v2, populated v1-to-v2 with preserved
identities and opaque bytes, both explicit targets and idempotent reopen, v1
rejection of v2 and v2 rejection of v3, invalid target before file creation,
normal versus reported-fault shapes, empty payloads, both optional origins,
primary/ordered cleanup and cleanup-only reports, malformed shapes and
foreign-key relationships. Force a real conflict in the second v2 DDL statement
on an existing populated v1 file and prove the first statement and version update
roll back while old records remain. Preserve all existing tests and the trusted
verifier. #60 stays open pending its full outcome/host/availability and other
acceptance criteria; #61–#64 own durable execution.

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-sqlite.sh`, `bash scripts/verify-runtime.sh`,
`bash scripts/verify-compiler.sh`, the base-owned isolated SQLite verifier where
the pinned image is available, and `git diff --check`. Record actual results.

### Fourth-slice verification

Bootstrap and the documentation/automation tests passed (135 files, 308 links,
46 automation tests). Local real-provider SQLite verification passed one
baseline and 17 feature tests; runtime verification passed two baseline and 93
features from a snapshot matching the runtime source and tests. Independent
review caught a text-equality constraint that could reject equivalent Guid
spellings; it was removed, with typed equality left to the future writer and
both preserved spellings covered by the final SQLite run.
Compiler verification passed 18 baseline and 406 feature tests from a
short-path snapshot matching the final SQLite source and tests; its 506,836-byte
feature TRX passed the unchanged size guard.
The full isolated verifier and base-owned SQLite verifier could not pull the
pinned MCR image locally (`Forbidden`), so neither completed offline tests here.
Hosted exact-head verification must provide that evidence; all prior tests and
the trusted verifier remain unchanged.

## Third slice: file-backed single-host connection settings

Base `5568706794414744c8d9e1282270aacf28b604d1` includes the version-one
identity schema and the merged trusted SQLite verifier. Continue on the existing
#60 branch. Add an internal `src/DSharp.Runtime.Sqlite/ActorSqliteDatabase.cs`
opener and additive `tests/sqlite-features/ActorSqliteDatabaseTests.cs`. It takes
an explicit absolute file path, opens a non-pooled private-cache SQLite
connection with foreign keys enabled, validates that its journal mode is DELETE,
sets and reads back connection-local synchronous FULL and a bounded busy timeout,
and calls the unchanged version-one schema coordinator. Reject memory/URI/relative
paths and unsupported settings; dispose the connection on initialization failure
without deleting the caller's file. Inspect rather than change persistent journal
mode before schema validation, so an unsupported or newer file is not silently
converted. Path syntax does not establish file type or storage locality. The host
remains responsible for selecting a distinct local file per application and for
process ownership; this does not prove cross-process exclusion,
filesystem durability, power-loss recovery, actor admission or a public API.
Settings are checked at open time; callers control the returned connection and
may change them or override a command timeout. This is not a lifetime bound.

Real-provider tests map the scoped criteria to evidence: fresh open and reopen,
actual per-connection setting readback and enforced foreign keys, persistent
version-one records, distinct application files, rejected relative/memory paths,
and failure/disposal with existing data and journal mode preserved for unsupported
files. Preserve all existing tests, the existing schema version and trusted
verification. Cross-kind invocation/attempt/token identity freshness is an
operation-level obligation for the future admission/claim provider, not something
these raw tables enforce. #60 remains open for outcome storage, migration,
host/availability and other outstanding criteria; #61–#64 own durable behavior.
The currently trusted SQLite feature tests explicitly assert version one and
reject version two. A later version needs a scoped transition that preserves
unsupported-version coverage and trusted verification; this slice leaves both
the existing tests and version behavior intact.

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-sqlite.sh`, `bash scripts/verify-runtime.sh`,
`bash scripts/verify-compiler.sh`, the base-owned isolated SQLite verifier where
the pinned image is available, and `git diff --check`. Record actual results.

### Third-slice verification

Bootstrap, documentation (135 files and 308 local links) and all 46 automation
tests passed. Local real-provider SQLite verification passed one baseline and
12 feature tests, including setting readback after reopen, foreign-key enforcement,
independent files, a semicolon in the filename and rejected WAL/newer files with
logical records and journal mode retained. Runtime verification passed two
baseline and 93 feature tests from a snapshot matching the runtime source/tests.
Independent review checked the scoped connection policy and its limits.
Compiler verification passed 18 baseline and 406 feature tests from a
short-path snapshot matching the final opener source and tests; its 506,836-byte
TRX passed the unchanged size guard.

The full isolated verifier and base-owned SQLite verifier could not pull the
pinned MCR image locally (`Forbidden`); neither completed offline tests here.
Hosted exact-head verification must provide that evidence. Existing tests and
the base-owned verifier remain unchanged.

## Second slice: initial actor identity schema

Base `aa0832e5d3a076f7b65182113d2d37be85629386` includes the merged #10
optional SQLite setup and its real offline verifier. #59 is closed; this reuses
the existing #60 branch. Add a version-one, internal SQLite migration in
`src/DSharp.Runtime.Sqlite/ActorSqliteSchema.cs`, applied through the existing
coordinator to a caller-owned open connection. Exercise it with an additive
`tests/sqlite-features/ActorSqliteSchemaTests.cs` against real SQLite. Preserve
trusted base projects, locks, existing tests and verifier. Version one is an
initial development schema; additional fields require an explicit later migration.

The bounded schema records a shared `(application_id, submission_id)` identity
and kind for targeted and publication submissions, opaque contract and payload
bytes, distinct invocation and attempt identities, requested and selected actor
identities, optional opaque target key, paired awaited-parent identities,
publications and frozen delivery identities unique per publication/subscription.
These are internal relational storage shapes, not an admission API, dispatch,
provider implementation, codec or durability acknowledgement. Avoid assuming
display names are stable identities. Test foreign keys with explicitly enabled
connection settings; reject mixed kinds and partial awaited ancestry in the
scope where the schema can check them. Keep publication with zero deliveries
and multiple attempts per invocation valid. A delivery-sourced invocation must
remain a root and preserve the delivery's frozen requested and selected actor
identities. Do not claim features not enforced.

Acceptance mapping: real-provider tests create a fresh file, apply twice and
reopen, prove exact bytes and selected IDs survive, reject shared-key and
delivery duplicates, verify distinct attempt IDs and required relationships,
test parent pairing and invalid kinds, and prove separate application files do
not share rows. Exercise the existing coordinator with a deliberately invalid
second migration statement on a fresh database and verify transactional schema
rollback and unchanged version; reject unsupported versions without mutation.
This slice does not yet cover the complete fault/cleanup model, single-host
settings policy, all restart semantics or provider admission and recovery; #60
remains open. Raw SQL can leave incomplete submissions or change delivery rows;
these tables do not prove atomic admission or immutable accepted records.
#61–#64 own durable operational behavior, #69 owns fan-out and #78 owns codecs.

Run bootstrap, documentation/automation, SQLite, runtime and compiler checks,
including the trusted isolated SQLite verifier and its failure pilot where the
pinned image is available. Record exact results and local environment limits.
The commands are `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-sqlite.sh`, `bash scripts/verify-runtime.sh`,
`bash scripts/verify-compiler.sh`, and `git diff --check`. For the base-owned
SQLite check use `node <trusted>/.github/ai/verify-sqlite.mjs <candidate> <reports>`;
the merged CI job also runs its named-failure pilot.

### Second-slice verification

Bootstrap, documentation (135 files and 308 local links) and all 46 automation
tests passed. Local SQLite verification passed one baseline and six feature
tests against the pinned real provider. Runtime verification passed two baseline
and 93 feature tests from a snapshot whose runtime source and tests match this
branch. Independent review found and corrected a delivery identity/root
constraint; the final SQLite run includes its positive and negative cases.
Compiler verification passed 18 baseline and 406 feature tests from a
short-path snapshot matching the final SQLite source and feature tests; its
506,836-byte TRX passed the unchanged size guard.

The full isolated verifier and the base-owned SQLite verifier reached the pinned
MCR image pull, which returned `Forbidden` in this environment. They did not
complete offline tests locally; hosted exact-head checks must provide that
evidence. The earlier merged #10 setup's hosted SQLite failure pilot did prove its
named failure was rejected and a clean rerun passed.


## First-slice historical context

Base: `c01adaf65a83b638b808dcd3601ba65cf419537d`. The issue's sole stated
prerequisite, #59, was completed by merged #300. #14 and #9 provide the accepted
store semantics and the enabled core runtime test path. The runtime project has
no SQLite package, and its trusted verifier uses base-owned project manifests and
lock files. Optional provider setup is owned by #10; no SQLite-specific setup
child or branch currently exists. The #60 issue authorizes an actor SQLite schema,
but it does not itself change the separate setup ownership or make a new package
available to trusted verification.

## Scope and dependency boundary

Deliver an independently useful first implementation slice under #60: an
internal, provider-neutral migration coordinator using the pinned framework's
`System.Data.Common` abstractions. It can operate only on a caller-owned, already
open connection and trusted, pre-materialized migration descriptions. It is not a
SQLite provider, actual actor schema, public API, dependency setup, file opener or
durable acknowledgement. No dependency, project, lockfile, workflow or trusted
verifier change was included. The later provider setup under #10 needed to pin its
managed SQLite provider and transitive native/RID assets, enable exact source and
test paths and locked offline verification, demonstrate a real failing test is
rejected, and preserve core/compiler-only paths. Real SQLite schema execution,
settings, reopen and restart proof follow only after that setup is available.

The initial actor schema must ultimately retain stable invocation, attempt,
publication and subscription-delivery identities; the application/submission
namespace and publication/subscription pairs need uniqueness. A shared submission
record must distinguish targeted and publication kinds; invocation records retain
requested and selected identities, payload and compatibility metadata, public
target/key and awaited parent/attempt, and attempts retain fenced ownership and
outcome data. Publication records retain the complete frozen subscriber/selection
set. These are requirements to implement and verify, not a claim that this slice
chooses a final SQL layout, JSON encoding, migration for legacy workers, or an
external business-data transaction. #78 owns wire encoding; #61–#64 own durable
intake/claim/outcome execution and #69 delivery materialization.

## First slice: transactional version coordinator

Affected files: `src/DSharp.Runtime/ActorSchemaMigrationCoordinator.cs`, new
`tests/runtime-features/ActorSchemaMigrationCoordinatorTests.cs`, this plan and
implementation status. Before starting a transaction, validate and copy a trusted
ordered migration list: positive contiguous versions starting at one, nonempty
statement sequences, nonblank SQL, no live enumerable or host callback retained.
Each trusted statement must be a single transaction-compatible migration statement:
it must not control the transaction, alter `user_version`, attach databases,
run `VACUUM` or perform external effects. This is a trusted-input precondition,
not a claim that the coordinator parses or secures arbitrary SQL.
The runner uses fixed SQLite `PRAGMA user_version` reads/updates, not user-supplied
version SQL. It begins a provider-supplied transaction, binds every command to that
same transaction, reads/validates the stored version inside it, rejects unsupported
or corrupt versions before writes, executes all needed trusted statements, updates
the version only after they succeed, then commits. Version zero is accepted as a
fresh database only after a transaction-bound `sqlite_master` check shows no
non-internal objects; existing nonempty unversioned files fail closed. A current
version does not rerun statements. On error attempt rollback and preserve both the
primary error and any rollback error; do not report success after commit failure.
A commit exception or later disposal failure may occur after the commit took
effect: rollback after a commit exception is best effort, and the caller must
treat the outcome as uncertain and re-read before retrying.
Dispose commands and transaction, but never close or dispose the caller's
connection. Do not claim that a generic `DbTransaction` provides SQLite locking,
atomicity or crash behavior until tested against the approved real provider.

AC-to-test mapping: a deterministic BCL fake records transaction identity,
command order and staged versus committed state to exercise ordered version
checks, no-op, upgrades, unsupported/corrupt/nonempty-unversioned rejection,
rollback after injected read/statement/version/commit failure, error causality,
pre- and post-commit acknowledgement loss, disposal and caller connection
ownership. It does not prove SQLite SQL validity,
physical rollback, application isolation or restart. Future real-provider tests
must validate fresh/reopen schema, entity and shared-key constraints, migration
rollback and unsupported versions, explicit single-host settings, independent
application files and preserved selected implementations after restart. #60 stays
open until these actual acceptance criteria pass against real SQLite.

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-compiler.sh`, `bash scripts/verify-runtime.sh`, and
`git diff --check`. Record actual results and any isolated-image limits. Do not
add a package or modify trusted checks as a substitute for the #10 setup.

## Verification on the first slice

Bootstrap, documentation checks and all 31 automation tests passed. The first
runtime run exposed two fake-helper member names that hid inherited members; the
names were corrected, and runtime verification then passed 2 baseline and 92
feature tests. These are fake-provider orchestration tests, not SQLite evidence.
Compiler verification passed 18 baseline and 406 feature tests in a private
short-path snapshot matching the final coordinator source; the test helper was
finalized later and covered by the final runtime run. The unchanged 512,000-byte
TRX guard accepted the 506,836-byte feature report. The local isolated verifier
could not pull its pinned MCR image (`Forbidden`); hosted exact-head verification
is still required.
