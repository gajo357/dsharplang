# Implementation plan: #29 — Nullable fallback and conditional field access

Issue: https://github.com/gajo357/dsharplang/issues/29
Base: `c223492` on main; prerequisite #28 was merged in #117. The selected
contract is syntax reference §5.3 and the adopted DEC-02 precedence rules.

## Scope and implementation

Implement right-associative `??` below `||`: evaluate the left operand once,
and evaluate its compatible fallback only if the left is absent. Support
nonnullable or nullable fallback results, contextual null inference where a
contained type is known, and reject incompatible or uninferrable operands.
Preserve source-order evaluation, short circuiting and early returns.

Implement `?.` for record fields. It evaluates the receiver once, yields null
when absent, and otherwise accesses the field, flattening any already-nullable
field to a single nullable layer. Nested chains retain absence and do not access
later fields on a null path. Ordinary nullable dereference remains invalid.
Conditional method calls, postfix Result propagation, `!`, and ternary syntax
remain separate work. Retain existing APIs, tests, dependencies, project files,
workflows and trusted verifiers. No new decision gate blocks this bounded slice.

Change compiler parser, bound model, type checking and managed emission, add
new feature tests and `examples/nullable-operators.ds`, update status and this
plan. Cover supported scalar, Unit, record and union values without source-visible
wrappers. Resolve contextual `null` without silently assigning `T?` to `T`.

## Acceptance evidence

- AC1: execute present/null fallback for value/reference/Unit types; assert
  laziness, one left evaluation, compatible result type and right associativity.
- AC2: execute absent/present record field access and nested chains, nullable
  fields, flattening and single receiver evaluation; reject invalid access.
- AC3: exercise combined chains, precedence, null inference, nullable fallback,
  early returns, type/unknown-field diagnostics, exact source spans, C# naming
  and generated code with warnings as errors. Existing regression tests pass.

## Verification

With the workspace SDK/cache configured:

```bash
bash .github/ai/bootstrap.sh
bash scripts/verify-compiler.sh
dotnet run --project src/DSharp.Cli -- check examples/nullable-operators.ds
dotnet run --project src/DSharp.Cli -- compile examples/nullable-operators.ds artifacts/nullable-operators-demo
AI_REPORT_DIR=/workspace/work/issue29-verification bash .github/ai/verify.sh
git diff --check
```

Open one draft PR; fix any CI failure on its branch and leave merge to the user.
Do not begin another issue before review.

## Progress

- Implemented right-associative coalescing with contextual fallback typing and
  single, lazy operand evaluation. Added conditional record-field access with
  flattened nullable results and preserved early-return control flow.
- Added execution and diagnostic tests for the ACs, operator precedence,
  nullable/value/reference/Unit cases, nested chains, contextual null, source
  spans, and instrumented single evaluation. Generated C# also builds with nullable
  analysis and warnings as errors; existing tests are unchanged.
- Bootstrap, Release build with zero warnings, 18 baseline and 276 feature tests,
  CLI example check/compile, 20 automation tests and documentation checks passed.
  The local full verifier could not download its pinned SDK image (`Forbidden`);
  GitHub Actions runs the unchanged containerized verifier.

## PR

Draft PR pending user review and merge. Fix any CI failure on this branch.
