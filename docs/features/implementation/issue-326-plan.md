# Issue #326: application agent guidance and example

## Follow-on: verify inventory behavior beyond the advertised example

Base `bb3cca7ee0d50693917daa5f269c2e2d86788db2` checks, compiles and runs
`available()` as 42 from a clean source export, but a `remaining` function that
always returns 42 would pass. The quick reference advertises that function with
two inputs and subtraction. Strengthen the explicitly scoped application
verifier to execute the actual generated `remaining` with positive, zero and
negative outcomes, in addition to the existing `available()` assertion. Keep the
existing output and all prior checks unchanged. Add an independent candidate
automation test that exports committed source to a temporary checkout, replaces
only the example function body with a constant 42, and verifies the real command
fails on a named `remaining` assertion even though `available()` still yields 42.
No project, dependency or trusted harness changes are proposed.

Map the additional behavioral checks and negative control to the issue's
advertised-example and actual-command acceptance criteria. Update example
instructions and quick reference to describe exactly the tests performed, while
leaving SDK packaging and broader AI-01/02/04 coordination outside this slice.
Run the example command, new negative-control test on the committed candidate,
bootstrap, full verifier and compiler regression; record actual results. Preserve
all existing tests, pinned SDK and trusted verifier. Keep #326 open.

### Behavior verification results

- `bash scripts/verify-application-example.sh` checked and compiled both
  actual D# files, verified `available() == 42` and executed `remaining`
  with positive, zero and negative results.
- `node --test .github/ai/tests/application-behavior-negative.test.mjs`
  exported committed candidate `6fd125e9f37631743d33a85e74979e863e67c07b`,
  changed only the temporary example function to return 42, and confirmed the
  real documented command failed with the named remaining-calculation assertion
  rather than the summary assertion. Existing fresh-source and example tests
  also passed in the candidate automation suite.
- `bash .github/ai/bootstrap.sh` passed with .NET 11.0.100. Full
  `bash .github/ai/verify.sh` passed documentation (137 files, 309 links) and
  all 54 automation tests; its isolated compiler stage could not pull the pinned
  MCR image (`Forbidden`). Hosted exact-head checks remain necessary.
- `bash scripts/verify-compiler.sh` passed 18 baseline and 406 feature tests
  in a short-path committed source snapshot; the unchanged TRX guard accepted
  511,708 bytes. #326 remains open for broader coordination.

## Follow-on: verify a clean source snapshot

Base `ff249fa57e681b3a3b7f35b2240de14d0ba2dac9` contains the application
example, version-specific Claude import evidence and bounded reference claims.
The existing automation runs the example in its working checkout; it does not
prove the issue's fresh-checkout criterion or that the documented command matches
the command being run. Add an independent automation test that exports the
committed source tree to a private temporary directory without Git metadata,
checks both instruction/import pairs and every local example-guidance link there,
asserts the documented shell block is the verifier command, and runs that command
from the example root against the exported compiler and pinned SDK. The verifier
must compile and execute the actual D# sources and assert 42. Clean up the export
and keep the existing tests and trusted verifier unchanged.

This directly tests fresh-source discoverability, links, commands and behavioral
claims for #326 without asserting SDK packaging, upgrade behavior or runtime
features. The test belongs to the candidate automation suite and will exercise
the actual committed head in CI. Run the new test, bootstrap, full verifier and
compiler regression, record results and leave #326 open for broader AI-01/02/04
coordination. Preserve existing tests, dependencies and the pinned SDK.

### Clean-source verification

- `node --test .github/ai/tests/application-fresh-source.test.mjs` passed:
  a committed source export without `.git` contained the instruction/import
  pairs and links, and the documented command compiled and executed the
  example as 42 from its root. CI will repeat this against its exact checkout.
- `bash .github/ai/bootstrap.sh` passed using .NET 11.0.100. Full
  `bash .github/ai/verify.sh` passed documentation (137 files, 309 links) and
  all 53 automation tests, then could not pull the pinned MCR image (`Forbidden`)
  for its isolated compiler stage. Hosted exact-head verification remains
  necessary.
- `bash scripts/verify-compiler.sh` passed 18 baseline and 406 feature tests in
  a short-path snapshot; the unchanged TRX guard accepted 510,084 bytes.

## Follow-on: bound verified-example claims

Base `8f2bd25501a2f302b9ecc4523f79e45a19c02b41` includes the inventory
quick reference and its source comparison. The example guidance currently calls
the entire examples directory verified, although its verifier executes only the
two inventory sources. Link those two explicitly instead. The reference test
compares marked D# blocks with source, but an extra unmarked D# block could evade
that comparison. Add an independent automation guard for all D# fences, with
negative examples showing that unmarked samples fail. Keep the existing test and
behavior verifier unchanged, and preserve other examples and trusted checks.

This addresses the issue's claims and automatically verified-example criteria
for the inventory application; it does not imply verification of other examples,
SDK packaging or runtime behavior. Run the guard and existing example verifier,
bootstrap, full verifier and compiler regression; record exact results. Broader
AI-01/02/04 coordination remains outside this slice and #326 remains open.

### Verified-claim results

- The new guard passed on the reference and rejected unmarked `dsharp`, `DS`
  and `d#` fenced examples. The existing source/link comparison passed as well.
- The documented example verifier, run from its application root, checked and
  compiled the two actual files and executed the calculation as 42.
- `bash .github/ai/bootstrap.sh` passed with .NET 11.0.100 on `PATH`.
  `bash .github/ai/verify.sh` passed documentation (137 files, 309 links) and
  all 52 automation tests, then could not pull the pinned MCR image (`Forbidden`)
  for the isolated compiler stage. Hosted exact-head verification remains needed.
- `bash scripts/verify-compiler.sh` passed 18 baseline and 406 feature tests in
  a short-path snapshot; the unchanged TRX guard accepted 509,272 bytes.

## Follow-on: example-matched quick reference

Base `dd10fb9ac8835f9f3f467ee3e927dba231a1d300` contains the verified
application example and Claude import check. The requested version-matched
cheatsheet has no AI-01 artifact on this base. Add a deliberately narrow
`examples/inventory-app/CHEATSHEET.md` describing only the example's actual
checked compiler subset and linking the current pinned SDK, implementation
status, source files and verifier. Link it from the example's project-owned
`AGENTS.md` and direct scaffold users to a version-matched reference when one
exists. Do not claim a packaged SDK, initialization, actor runtime, or general
syntax compatibility; the broader AI-01/02/04 work remains separate.

Map source and executable claims to the existing example check/build/execute
verifier. Add a small automation test that compares every displayed D# block
with its actual source file and checks local links so the reference cannot
silently drift. Preserve existing tests, trusted verification, dependencies and
the pinned SDK. Run the new test, example verifier, bootstrap, full verifier and
compiler regression, then record exact results. #326 remains open while broader
documentation coordination is outstanding.

### Quick-reference results

- The new source/link automation test passed; the existing example verifier,
  run from `examples/inventory-app`, checked and compiled both D# files and
  executed the generated calculation as 42.
- `bash .github/ai/bootstrap.sh` passed with .NET 11.0.100 on `PATH`.
  `bash .github/ai/verify.sh` passed documentation (137 files, 309 links) and
  all 51 automation tests. Its isolated compiler stage could not pull the pinned
  MCR image (`Forbidden`); hosted exact-head verification remains necessary.
- `bash scripts/verify-compiler.sh` passed 18 baseline and 406 feature tests in
  a short-path snapshot; the unchanged TRX guard accepted 509,272 bytes.

## Follow-on: version-specific Claude import evidence

Base `b64e7c60fd98d287a1dc1908257d6360510e283c` contains the application
and scaffold guidance from #327. Verify the existing relative imports using the
published Linux x64 Claude Code 2.1.332 release as a concrete compatibility
baseline, without adopting a package dependency or claiming other versions.
Add `scripts/verify-claude-import.mjs`: copy the actual guidance/import files to
private temporary fixtures, run the specified version with a dummy key against
an in-process loopback mock, and verify the request context contains the
guidance. A negative control with a non-importing CLAUDE.md must omit it. No
real service, account or private project data is used. Check both the example and
scaffold imports, and document the exact command, version, binary digest,
results and limitation. The source's CLI startup and mock-context evidence
check import loading, not model behavior or SDK generation.

AC mapping: version-specific positive/negative import checks cover the requested
relative import and root delegation; the earlier example execution and link
checks remain unchanged. The current issue retains its AI-01/02/04 coordination
and version-matched cheatsheet work. Run the new verifier, bootstrap, full
verifier and compiler regression; record actual results. Preserve existing
tests, the pinned SDK, dependencies and trusted checks.

### Follow-on results

- The official [Claude Code 2.1.332 release](https://github.com/anthropics/claude-code/releases/tag/v2.1.332)
  supplies the version used for this compatibility baseline. Installed with
  `npm install --prefix /tmp/dsharp-claude-check --ignore-scripts --no-audit --no-fund --no-save @anthropic-ai/claude-code@2.1.332`;
  the Linux x64 executable reported `2.1.332 (Claude Code)` and SHA-256
  `a2df1f41bb00250992926420d0063f89b127cb81abdd284dbefb80c50ff1d026`.
- `node scripts/verify-claude-import.mjs /tmp/dsharp-claude-check/node_modules/@anthropic-ai/claude-code-linux-x64/claude`
  passed for both actual instruction files. Their guidance appeared in the
  local mock request with `@AGENTS.md`, and was absent in both controls without
  the import. The subprocess used an isolated config, dummy key and local mock;
  it did not use a real account or call an AI service. This verifies this
  version and environment, not every Claude version or model behavior.
  The configured endpoint and proxies are not a network sandbox. Both the
  version probe and test runs exclude caller credentials; failure output omits
  subprocess diagnostics and captured request content.
- `bash .github/ai/bootstrap.sh` passed with .NET 11.0.100 on `PATH`.
  `bash .github/ai/verify.sh` passed documentation (137 files, 309 links) and
  all 47 automation tests; its isolated compiler stage could not pull the pinned
  MCR image (`Forbidden`). Hosted exact-head verification remains necessary.
- `bash scripts/verify-compiler.sh` passed 18 baseline and 406 feature tests in
  a short-path snapshot; the unchanged TRX guard accepted 509,272 bytes.
- Review repair: the version probe now receives only `PATH`, version and test
  subprocess diagnostics are suppressed on failure, and the local endpoint and
  proxy configuration is explicitly described as no network sandbox. The three
  `claude-import-safety.test.mjs` regressions passed for synthetic caller-secret
  exclusion, failed-run stderr redaction, and version stdout/stderr redaction.
  The real 2.1.332 positive/negative checks also passed through a wrapper that
  rejects a synthetic caller secret. An initial regression exposed version
  stderr; it was fixed before the passing rerun. Bootstrap passed, documentation
  validation passed (137 files, 309 links), and all 50 automation tests passed.
  The full verifier again stopped on the pinned MCR image pull (`Forbidden`).
  The compiler regression passed 18 baseline and 406 features before the final
  JavaScript-only redaction change, with a 509,272-byte guarded TRX. Hosted
  exact-head checks remain necessary for the repaired branch.

Base: `838aa91dd054abe9977efdc91915be76e58293cb`. #326 is P0 and
independent of the still-open JSON codec issue. It calls for a supported-compiler
example now; SDK packaging and initialization remain AI-04 work. No AI-01/02
artifacts or version-matched cheatsheet are present on this base, so use the
checked-in implementation status, syntax reference and existing examples as
version-matched references rather than inventing a cheatsheet.

## Scope and steps

1. Add a scaffold-source `templates/application/AGENTS.md` and a one-line
   `CLAUDE.md`; do not claim it is installed or automatically upgraded. Guidance
   points users to their own project files and actual supported commands.
2. Add a small `examples/inventory-app` multi-file pure D# example arranged by
   capability, with project-owned root instructions and a one-line Claude import.
   Explain the repository-pinned SDK and compiler version, relative docs and
   commands. Do not present planned actor/runtime features as implemented.
3. Add a narrowly scoped JavaScript automation test and verification script to
   check the real example files, compile them and execute generated behavior,
   as explicitly required by #326. Preserve existing tests and trusted verifier.
4. Document actual results here. No language syntax, public API, package,
   dependency or SDK generation change is proposed.

## Acceptance mapping and verification

- Root instructions/import and template: inspect files, check exact import and
  relative target; verify Claude import semantics against official documentation.
- Real commands/links/supported syntax: run documentation validation, the
  example verifier and its automatic test from a clean pinned SDK checkout.
- Executable claim: compile actual `.ds` files and invoke the generated method,
  asserting the expected result. No runtime behavior is claimed.
- Public syntax/API change: none; SDK upgrade behavior remains AI-04.
- Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
  `bash scripts/verify-compiler.sh`, and `bash scripts/verify-application-example.sh`.

At this slice's original wrap, no version-specific execution evidence was
available. The follow-on above supplies evidence for 2.1.332. The new example
automation test runs in the candidate verifier; the existing base verifier is
untouched. AI-01/02/04 coordination remains outside this bounded follow-on.

## Results

- `bash .github/ai/bootstrap.sh` passed with .NET 11.0.100 on `PATH`.
- `bash .github/ai/verify.sh` passed documentation (137 files, 309 links) and
  47 automation tests, including the real example. The isolated compiler stage
  could not start because the pinned MCR image pull returned `Forbidden`.
  Hosted exact-head verification remains necessary.
- From `examples/inventory-app`, `bash ../../scripts/verify-application-example.sh`
  checked and compiled the two D# files and executed `available()` as 42.
  `node --test .github/ai/tests/application-example.test.mjs` passed after
  adding checks for each example documentation link and the exact import files.
- `bash scripts/verify-compiler.sh` passed in a short-path snapshot: 18 baseline
  and 406 feature tests; the unchanged TRX guard accepted 509,272 bytes.
- [Claude Code's import documentation](https://code.claude.com/docs/en/memory#import-additional-files)
  describes `@` imports relative to the containing file. At this original wrap,
  no version-specific import execution was available; see the follow-on above.
