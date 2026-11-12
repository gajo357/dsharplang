# Issue 33: function types and capture-free lambdas

Base: `e5f4e42` on main. Dependencies [#20](https://github.com/gajo357/dsharplang/issues/20),
[#24](https://github.com/gajo357/dsharplang/issues/24) and adopted
[DEC-02](DEC-02.md) are merged. DEC-01 remains unadopted and this work does
not implement its proposed primitives or collections.

## Scope and approach

Implement the adopted right-associative `(T, U) -> R` function type grammar
and capture-free lambda expressions with zero, one or multiple parameters,
explicit or contextually inferred parameter types and expression or block
bodies. Support function-valued locals, parameters and return values, passing
and invoking them with arity and type checking. Check typed generic/nullable
components consistently with existing type semantics. Function values remain
execution objects: disallow their storage in records or union payloads, directly
or through generic wrappers, and reject captures of outer locals/parameters.
Lambdas may call ordinary module functions without capturing locals. Do not add
closures, async lambdas, tuples, overloads or new value APIs.

Bind lambda bodies with a distinct lexical return target, scoped parameters,
locals and diagnostics, ensuring both explicit `return` and Result postfix `?`
exit that lambda, never its enclosing function. Emit typed delegates with a
nearest-function return boundary, preserve evaluation order, and ensure that
rejected captures cannot leak into generated C# closures. Reuse the existing
typed compiler model rather than source rewriting. Preserve previous language
semantics, tests, dependencies and trusted checks.

## Acceptance map

- AC1: execute zero/single/multiple parameter lambdas, inferred/annotated and
  mixed parameters, nested function signatures and expression/block bodies;
  reject missing/contradictory expected types with source diagnostics.
- AC2: execute passing, returning, assigning and invoking functions and generic
  higher-order examples; diagnose arity/type mismatch and storing function
  values in immutable data or wrappers.
- AC3: exercise explicit lambda return and Result `?` success/error through
  nested expressions, verifying nearest-lambda scope, order, non-evaluation and
  capture rejection for outer locals/parameters.
- Validate syntax precedence/disambiguation, nullable values, name lookup,
  generated C# with warnings as errors, independent lambda parameters and
  pre-existing generic/Result/nullable behavior.

## Verification

Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`,
`AI_REPORT_DIR=/workspace/work/issue-33-verification bash .github/ai/verify.sh`,
CLI check/compile for an example, documentation and `git diff --check`, plus
GitHub Actions. Record real evidence; fix CI on the same PR branch.

## Progress

- AC1: generated execution covers contextual and explicitly typed zero-, one-
  and multi-parameter lambdas, expression/block bodies and nested function types;
  grouped diagnostics reject missing context and incompatible annotations.
- AC2: generated execution covers passing, returning and invoking functions,
  inferred generic calls and nullable function types. Diagnostics reject arity
  mismatches and storing functions through generic data and helper call chains.
- AC3: lambda-local explicit return and Result propagation are exercised in
  generated programs, with separate caller boundaries and rejected captures.
  Match guards and the existing nonfunction-call diagnostic are preserved.
- Functions do not acquire C# delegate equality semantics: direct and generic
  function-value comparisons are diagnosed, while null checks remain valid.
  Generated execution also covers a 17-parameter delegate and nested functions
  with identical return types.
- Bootstrap, `bash scripts/verify-compiler.sh` (18 baseline and 355 feature
  tests), CLI check/compile of the example, documentation checks and diff checks
  passed locally. Generated test projects enable nullable warnings as errors.
- `bash .github/ai/verify.sh` passed documentation and all 20 automation tests,
  but its isolated compiler stage could not download the pinned image locally
  (`Forbidden`). GitHub Actions runs that unchanged isolated verification.
