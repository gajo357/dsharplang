# Implementation plan: #25 — Immutable records and copy updates

Issue: https://github.com/gajo357/dsharplang/issues/25
Base: `b64f166` on main. Prerequisites #23 and #24 were merged in #110 and
#111; DEC-02 and #55 were merged in #113.

## Scope and implementation

Implement the issue's frozen syntax for public record declarations, named
construction, field access and `with` updates. Records have immutable supported
scalar or nested-record fields and structural value equality. Preserve lexical
scopes, evaluation order, source spans and all existing public constructors.
Collect record signatures before binding bodies, type-check field names and
values and reject mutation, duplicate declarations and namespace conflicts.
Emit independent, immutable values using the existing managed C# backend.
Do not add collections, foreign mutable values, validation, unions or generics.

Files: `src/DSharp.Compiler/Compiler.cs` (syntax, binder, emission), a new
`tests/features/RecordTests.cs`, `examples/records.ds`, implementation status
and this plan. No dependencies, workflows, trusted tests or project changes.

## Acceptance evidence

- AC1: parse and execute declarations, construction and field access for
  supported primitive and nested record fields, including parameters and returns.
- AC2: test diagnostics and source locations for missing, unknown, duplicate or
  incompatible fields, reassignment and duplicate declarations.
- AC3: execute copy updates and structural equality, check the original remains
  unchanged, nested values compare by value, and initializers evaluate in order.
- Regressions: preserve existing tests, generated-name safety, control flow and
  behavior of unrelated grammar. Build generated code with warnings as errors.

The issue freezes the relevant syntax and DEC-02 specifies public declarations
and value-namespace collision rules; no unresolved decisions currently block
this scoped slice. Any behavior not covered by those contracts must be bounded
explicitly rather than silently expanding the language.

## Verification

With the workspace SDK and cache configured, run:

```bash
bash .github/ai/bootstrap.sh
bash scripts/verify-compiler.sh
dotnet run --project src/DSharp.Cli -- check examples/records.ds
dotnet run --project src/DSharp.Cli -- compile examples/records.ds artifacts/records-demo
AI_REPORT_DIR=/workspace/work/issue25-verification bash .github/ai/verify.sh
git diff --check
```

Run GitHub Actions on the single draft PR; fix any failing checks on that branch.

## Progress

- Implemented record declarations, named construction, access, copy updates,
  structural equality and type documentation. The binder collects forward
  signatures and diagnoses invalid fields, types, namespace collisions and
  attempted mutation before emission. Generated immutable CLR classes use
  D# structural equality helpers; external CLR equality is not a promised API.
- Added feature tests for all ACs, nested values, source spans, forward types,
  source order and receiver evaluation, C# name collisions, early returns,
  documentation and record literals in conditions. Existing tests are unchanged.
- Bootstrap, Release build with zero warnings, 18 baseline tests, 168 feature
  tests, CLI example check/compile, 20 automation tests and documentation
  validation passed. The local full verifier could not pull its pinned SDK image
  (`Forbidden`); GitHub Actions runs the unchanged containerized verification.

## PR

Draft PR pending; review and merge remain with the user. Fix any failures on
the same branch before advancing.
