# Issue #326: application agent guidance and example

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

Claude Code is not installed here and the repository does not identify a supported
Claude Code version. Official documentation can establish relative import syntax,
not an execution test against an unspecified version; retain that acceptance
item as outstanding and keep #326 open until verified. The new automation test
runs in the candidate verifier; the existing base verifier remains untouched.

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
  describes `@` imports relative to the containing file. No installed or
  repository-supported Claude Code version was available for an actual import
  execution check; that item and the AI-01/02/04 coordination remain open.
