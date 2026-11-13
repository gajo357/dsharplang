# Issue 36: Tuples and immutable destructuring

Base: `8b4cbd60f4dae9d42f9cb2d1d84f7a296e835eb5` on main. Dependencies
[#25](https://github.com/gajo357/dsharplang/issues/25) and
[#33](https://github.com/gajo357/dsharplang/issues/33) are merged. The adopted
DEC-02 grammar distinguishes tuple types from function parameter lists and Unit.

## Scope and implementation

- Parse tuple values and types with at least two elements and immutable `let`
  tuple destructuring, preserving grouping, Unit, lambda and function-call syntax.
- Bind and type-check tuple construction and destructuring, including nested
  values, arity, scoping and correct distinctions between tuple and multiple
  function arguments. Preserve ordinary evaluation order and evaluate a
  destructured expression once.
- Emit executable C# using an immutable representation consistent with D#'s
  existing value semantics. Do not add mutable elements, record positional
  shortcuts, dependencies, or collection/runtime APIs.
- Update the implementation status and add a focused feature test file. Primary
  implementation touches `src/DSharp.Compiler/Compiler.cs` and its existing
  type/coverage helpers where needed; generated C# remains the backend.

## Acceptance and verification

- Exercise typed and nested tuples through function arguments, return values,
  records and local bindings; execute generated C# and verify evaluation order.
- Test immutable tuple destructuring, nested tuples, arity/type/name errors,
  Unit, grouping and tuple-argument versus multiple-argument distinctions.
- Preserve existing tests and trusted verification. Run
  `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`,
  `AI_REPORT_DIR=/workspace/work/issue-36-verification bash .github/ai/verify.sh`,
  documentation checks and `git diff --check`; record actual results here.
- No unresolved syntax decision is assumed; report any newly discovered
  contract dependency instead of silently adopting it.

## Verification results

- `bash .github/ai/bootstrap.sh` passed. `bash scripts/verify-compiler.sh`
  passed the locked restore/build, 18 baseline and 358 feature tests.
- The new grouped feature test executes generated C# for nested and wide tuples,
  typed and generic functions, closures, records, nullable values, Option and
  Result. It checks ordered element evaluation and a single initializer
  evaluation, as well as arity, assignment, nullability and type diagnostics.
- CLI `check` and `compile` passed for `examples/tuples.ds`.
- `bash .github/ai/verify.sh` passed documentation and all 20 automation tests.
  Its isolated compiler stage could not pull the pinned Docker image locally
  (`Forbidden`); GitHub Actions runs the unchanged isolated verifier.
- The C# backend represents tuples as value tuples: generated code works with
  value copies, and D# does not expose tuple-field mutation.
