# Implementation plan: #28 — Nullable types and direct matching

Issue: https://github.com/gajo357/dsharplang/issues/28
Base: `5174ac5` on main; prerequisite match #27 was merged in #116.
The selected nullable rules are in syntax reference §5 and adopted DEC-02.

## Scope and implementation

Support a single `?` suffix on supported scalar, Unit, record and union types
in function parameters/results, local annotations, record fields and union
payloads. A `T` or `null` may flow into an expected `T?`; `T?` may not flow
into `T` without explicit matching. Bare null with no inferred contained type
and repeated nullable suffixes are diagnostics. Propagate expected types into
conditional and match expressions and preserve source spans and evaluation order.

Extend matching to distinguish `null` from a present value. A named nullable
pattern binds a present value as `T` even when it appears first; `_` includes
absence. Coverage checks both possibilities and composes nested nullable, union
and bool payload patterns; guarded arms do not establish coverage. Direct
access to nullable data is rejected. Generated code must preserve absence for
value and reference types without a source-visible Option/Some wrapper.
Nullable equality, including comparison with `null`, does not narrow a value.

Update compiler parsing, binding, coverage and emission, add new feature tests,
an `examples/nullable.ds` program, implementation status and this plan. Preserve
existing public constructors/APIs, tests, dependencies and trusted verification.
Null-forgiving, `?.`, `??`, nullable comparison-based narrowing, nullable
defaults, Result and generics remain separate issues. No unresolved decision
gate blocks this slice.

## Acceptance evidence

- AC1: execute present/null flows through typed locals, parameters, returns,
  record fields, updates and union payloads for both value/reference types;
  reject non-null targets, untyped null and repeated `?` with source spans.
- AC2: execute null-first and present-first matching and prove named patterns
  bind nonnullable values with no source-visible wrapping.
- AC3: check exhaustive/null-missing/guarded patterns and unsafe nullable
  access, including nested record/union payloads and bool/union combinations.
- AC4: prove present and null remain distinct through construction, calls,
  updates and matching; test contextual if/match expressions, single evaluation,
  arm ordering, early returns and generated C# with warnings as errors.

## Verification

With the workspace SDK/cache configured:

```bash
bash .github/ai/bootstrap.sh
bash scripts/verify-compiler.sh
dotnet run --project src/DSharp.Cli -- check examples/nullable.ds
dotnet run --project src/DSharp.Cli -- compile examples/nullable.ds artifacts/nullable-demo
AI_REPORT_DIR=/workspace/work/issue28-verification bash .github/ai/verify.sh
git diff --check
```

Open one draft PR; fix any CI problems on the same branch and leave merge to the
user. Do not start the next issue before review.

## Progress

- Implemented nullable signatures, values, contextual coercion, direct match
  narrowing, null-safe coverage and nullable equality without comparison-based
  flow narrowing. Generated values use CLR nullable value types or references;
  no source-visible Option/Some representation is introduced.
- Added execution and diagnostic tests for the ACs, nested union/record values,
  bool/Unit patterns, contextual conditionals and matches, early returns,
  structural equality, source spans and single evaluation. Generated C# was
  compiled with nullable analysis and warnings-as-errors enabled. Existing tests
  remain unchanged, including the previous untyped-null diagnostic code.
- Bootstrap, Release build with zero warnings, 18 baseline and 255 feature tests,
  CLI example check/compile, 20 automation tests and documentation checks passed.
  The local full verifier could not download its pinned SDK image (`Forbidden`);
  GitHub Actions runs the unchanged containerized verifier.

## PR

Draft PR pending user review and merge. Fix any CI failure on this branch.
