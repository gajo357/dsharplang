# Issue #60: SQLite actor schema and migrations

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
verifier change is included. The future provider setup under #10 must pin its
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
