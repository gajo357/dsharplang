# Issue 46: D# multi-file modules and imports

Base: `f81b837591a2fd6629d1109b82fce8772b0a3317` on main. [#46](https://github.com/gajo357/dsharplang/issues/46)
depends on implemented #20 and adopted DEC-02/#12. Its dependencies are met;
the roadmap's phase order is conservative and does not require adoption of the
independent DEC-01 collections contract.

## Scope

- Support real D# source files contributing to the same module, file-local
  explicit imports, dotted module names and qualified public declarations in
  accordance with [DEC-02](DEC-02.md). Preserve deterministic precedence,
  shadowing, ambiguity and diagnostic location rules.
- Extend compiler source-input APIs and CLI compatibly with existing single-file
  usage. Check and generate cross-file programs without changing the pinned
  backend, dependencies, project manifests or trusted verification.
- Keep source path/span attribution for errors, including duplicates, ambiguous
  names and unresolved imports. Preserve existing generic, nullable, Result,
  Option, closure and immutability behavior.
- Add new feature tests and real `.ds` examples to exercise the cross-file
  compiler and CLI. Imports resolve names; they do not register handlers. This
  does not implement package manifests, aliases, NuGet, or a bundled stdlib.

## Acceptance and verification

- AC1: execute generated C# for a multi-file D# program that merges one module,
  imports another and uses qualified/unqualified public names, types and calls;
  exercise local/file-scoped shadowing and non-transitive imports.
- AC2: reject duplicate, unresolved and ambiguous declarations/imports, with
  the source path and relevant span of the offending file. Verify order
  independence and avoid accidental cross-module name collisions.
- AC3: exercise multiple-file compiler and CLI input and preserve legacy
  single-file behavior and public APIs. Include genuine D# files, not C#-encoded
  substitutes, in examples.
- Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`,
  `AI_REPORT_DIR=/workspace/work/issue-46-verification bash .github/ai/verify.sh`,
  documentation/link checks and `git diff --check`. Record actual results; fix
  CI on the same PR branch.

## Design boundary

DEC-02 defines import/name lookup; no new external module or packaging contract
is adopted here. A bundled D# standard library and migration of the existing
Result helpers to D# source are a separate integration once source packaging
and trusted verifier staging are explicitly scoped.

## Results

- Bootstrap and compiler verification passed (18 baseline and 361 feature
  tests). The multi-file generated-code and CLI tests cover both input orders,
  dotted qualification, file-local imports, generic data, union cases,
  collisions, origin paths and token equality compatibility.
- The four checked-in D# files in `examples/multifile/` pass CLI `check` and
  `compile` together. Existing single-file entry points remain covered.
- Documentation and all 20 automation tests passed. The local isolated compiler
  verifier could not download its pinned Docker image (`Forbidden`); GitHub
  Actions runs the unchanged verifier. No existing tests, project/dependency
  files or verifier scripts were modified.
