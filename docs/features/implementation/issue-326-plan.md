# Issue #326: application agent guidance and example

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
