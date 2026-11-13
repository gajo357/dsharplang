# Issue 144: package D# prelude source for the compiler

Base: `488641d` on main, including multi-file support #46. This is the explicitly
bounded setup prerequisite [#144](https://github.com/gajo357/dsharplang/issues/144)
for [#142](https://github.com/gajo357/dsharplang/issues/142), which will replace
the existing C#-constructed Result-helper bodies with normally bound D# source.

## Scope and sequencing

- Write the actual five synchronous Result helper definitions in
  `src/DSharp.Compiler/Prelude/Result.ds`; compile and execute them as D# using
  existing compiler support. Preserve the current generic argument order and
  nullable/Option behavior. No generated duplicate C# source is a second source
  of truth.
- Embed exactly that source in the compiler assembly with a deterministic
  resource name via the existing compiler project. It must be readable from the
  built artifact outside the repository, without a working-directory dependency.
- Extend the trusted compiler verifier only for that exact candidate `.ds`
  resource. Keep trusted project manifests, existing tests, offline container,
  reports and all other restrictions unchanged; reject missing or unsafe
  symlink paths and never silently retain stale trusted resource bytes.
- Add new automation and/or feature tests for resource packaging, source
  behavior and staging: candidate-vs-trusted bytes, missing, symlink and
  containment failures. Exercise positive and negative checks without weakening
  the verifier. No workflow, dependency or existing test changes are authorized.
- This setup does not yet change which helper implementation the compiler binds.
  After review and merge, #142 will consume the embedded source through the D#
  frontend, preserving built-in Result identity and source lookup precedence.

## Verification

Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`,
`AI_REPORT_DIR=/workspace/work/issue-144-verification bash .github/ai/verify.sh`,
focused staging/packaging tests, D# generated-code execution, documentation
validation and `git diff --check`. Record actual results. Fix CI on this branch.
No language/module/package contract is finalized by this infrastructure slice.

## Results

- Bootstrap and compiler verification passed: 18 baseline and 361 feature tests.
- All 23 automation tests passed, including exact-byte staging, missing and
  symlink rejection, and extraction of the embedded resource from the built
  compiler outside the checkout. The extracted D# source was compiled with a
  D# consumer and executed through all five helpers, including nested Results,
  nullable Options and explicit `mapError` generic argument order.
- Documentation and diff checks passed. The local isolated verifier could not
  pull its pinned Docker image (`Forbidden`); GitHub Actions runs the trusted
  base and proposed isolated verifiers. No existing tests or workflows changed.
- The resource is packaged but not yet the compiler's implicit implementation;
  #142 owns that integration and removal of hand-constructed helper bodies.
