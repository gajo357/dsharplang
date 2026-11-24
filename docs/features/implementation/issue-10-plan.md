# Issue #10: optional SQLite provider setup

Base: `e804b222f0c83cfd834a4344eea0e78056136395`. #9 runtime setup is merged;
#59 is closed, and #60 has a merged BCL-only migration coordinator. #10 remains
the existing owner of optional provider/package/test setup. This is one SQLite
setup slice under that existing issue; no new issue or feature implementation is
created and #10 remains open for its other subsystems. No SQLite-specific setup
issue, branch or PR exists. The #60 schema and durable-provider behavior are out
of scope until this setup is merged and their own real tests pass.

## Scope and exact paths

Use a separate optional `src/DSharp.Runtime.Sqlite/` project and
`tests/DSharp.Runtime.Sqlite.Tests/` project, each with a committed NuGet lock
file, plus additive `tests/sqlite-features/` test sources. These projects are not
added to the core solution or core runtime dependencies. Pin the managed
`Microsoft.Data.Sqlite` package exactly to `[11.0.0]`; use the existing exact
xUnit/test SDK pins and lock the resolved transitive/native SQLitePCLRaw graph.
Record the actual resolved versions and native runtime assets, not just dependency
range minima. The optional project references the package-free core runtime; the
core gains only `src/DSharp.Runtime/SqliteSetupAccess.cs` and a new
`tests/runtime-features/SqliteSetupAccessTests.cs` for internal access by the
optional assembly only. SQLite tests reach the coordinator through an internal
wrapper in that optional assembly. The optional project is an
internal setup probe, not an actor store or public application API. No actor
schema, production dispatch, compatibility codec or durable acknowledgement is
implemented here.

The generated lock graph resolves `Microsoft.Data.Sqlite` and its Core package
to 11.0.0, `SQLite` to 3.53.4, and the SQLitePCLRaw bundle, config, core and
provider packages to 3.0.5. Locked content hashes are recorded in both project
lockfiles. The SQLite package includes Linux x64, Linux musl x64 and Linux arm64
native assets; asset presence alone is not an isolated-container load result.

Add `scripts/verify-sqlite.sh` for local locked restore/build, nonzero baseline
and feature test execution with bounded TRX validation. Add a separate trusted
`.github/ai/verify-sqlite.mjs` staged verifier and a pilot
`.github/ai/sqlite-pilot.mjs`, with focused automation tests in
`.github/ai/tests/`; wire a separate narrowly scoped step and pilot into
`.github/workflows/ci.yml` without removing or weakening existing checks. The
verifier must take project files, lockfiles, pinned configuration and baseline
from the trusted base, overlay scoped candidate core and SQLite source and only new
SQLite feature tests,
reject symlinks and modifications/deletions of existing feature tests, and demand
new discovered SQLite tests for changed SQLite source or the existing core
`ActorSchemaMigrationCoordinator.cs`; other core changes retain their existing
runtime-verifier coverage and are still compiled/tested against the SQLite smoke.
Restore exact locked packages before a
network-disabled, nonprivileged, read-only isolated test container; retain bounded
TRX evidence and require nonzero passing baseline and feature results. Validate a
real SQLite open, parameterized SQL and transaction rollback in a temporary file,
close and reopen it, and verify the native library loads. This is setup evidence,
including the actual core migration coordinator against real SQLite; it is not an
actor schema or crash-durability claim.

The first setup PR has no base SQLite verifier. Permit this bootstrap fallback
only for the exact base `e804b222f0c83cfd834a4344eea0e78056136395`; any later
base missing the merged verifier fails closed. Its CI still runs all existing
base checks; explicitly run the proposed verifier and a disposable failure pilot
that inserts a genuinely failing new feature, confirms the exact verifier rejects
it, removes it and proves the clean case succeeds. On later PRs use the merged base
SQLite verifier against the candidate plus the proposed candidate verifier on
setup changes. Never copy candidate manifests over trusted manifests or let
candidate code replace baseline tests. Preserve all existing guardrails, pinned
images and failure checks; no allow-failure flag or protection bypass.

## Acceptance-to-test map

- Exact managed and transitive versions, lockfiles and paths: locked restore,
  package/native asset inspection and project isolation checks.
- Trusted verification: stage-tamper/symlink/old-test protections, nonzero TRX
  parsing, actual offline SQLite baseline and feature runs, named failing-test
  pilot and clean rerun.
- Core usability: existing bootstrap, documentation/automation, compiler and
  runtime verifiers run unchanged; compiler-only and background/core builds do
  not require the optional package.
- Real provider smoke: parameterized file-backed operation, transaction rollback,
  dispose/reopen and native load. Schema migrations, real actor durability and
  crash/restart acceptance remain #60–#64, not evidence claimed by this setup.

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-compiler.sh`, `bash scripts/verify-runtime.sh`,
`bash scripts/verify-sqlite.sh`, the SQLite verifier/pilot and focused automation
tests, plus `git diff --check`. Record actual results and any MCR image limit.

## Verification evidence

- Bootstrap and documentation passed (135 files, 308 local links). The full
  automation suite passed all 46 tests, including SQLite staging and pilot tests.
- The local SQLite verifier passed one baseline and one feature test against
  real file-backed SQLite. The runtime verifier passed 2 baseline and 93 feature
  tests. The compiler verifier passed 18 baseline and 406 feature tests, with a
  506,836-byte feature TRX under the unchanged size guard, from a short-path
  snapshot whose SQLite and runtime source and test files match this branch.
- The local full isolated verifier and the separate actual SQLite verifier could
  not pull the pinned MCR image: the registry returned `Forbidden`. The actual
  failure pilot correctly rejected that infrastructure failure before the
  baseline passed; it did not demonstrate a named test failure or clean rerun.
  Hosted exact-head CI must provide both results before merge. The first setup
  run tests the proposed verifier at the exact bootstrap base; it is not an
  independent base-owned SQLite verifier until this setup is merged.
