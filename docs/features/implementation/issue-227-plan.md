# Issue 227: Binary64 float literals and IEEE arithmetic

**Issue:** [#227](https://github.com/gajo357/dsharplang/issues/227), a partial child of [#49](https://github.com/gajo357/dsharplang/issues/49).
**Base:** `06898f9` (merged PR #226).
**Dependencies:** Adopted [DEC-01](DEC-01.md), extended integer #48, decimal #215 and float32 #225 implementation. Binary64 requires no deferred numeric conversions or formatting; #49 remains open for those.

## Scope and steps

Implement binary64 `float` and unsuffixed fractional/exponent literals with exact pre-round finite range checking, invariant nearest-even conversion, bounded handling of huge exponents and IEEE arithmetic. Map D# `float` to CLR `double`, maintain distinct `float32`/decimal/integer types and existing numeric tokenization. Preserve recursive IEEE NaN inequality for nullable/generic/tuple/list/record/union/Option/Result values, including same-reference containers; reject recursively float-bearing Map/Set keys. Keep conversions and general numeric formatting outside scope.

1. Implement bounded exact binary64 literal validation and nonconstant bit emission in `src/DSharp.Compiler/`, with necessary binder, match reachability, nullable/type and multi-file support. Check exact max/max+1 before rounding and normal/subnormal ties.
2. Add D# execution/diagnostic tests in `tests/features/Float64Tests.cs` and `examples/float_arithmetic.ds` for literal precision/range/culture, all IEEE behavior, nested equality, key safety, generics/nullables/multifile and mixed-type rejection. Preserve existing safety tests.
3. The adopted DEC-01 contract supersedes exactly two old negative fixture inputs that assert the `float` parameter type is unsupported: replace only those type spellings with unknown `float128` in FunctionCallTests.cs and Float32Tests.cs; preserve all assertions and other rows. No other existing negative fixtures change.
4. Update `docs/features/DSharp_Implementation_Status.md` and leave #49 open for conversions and formatting.

## Verification

Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`, `.github/ai/verify.sh`, and the D# CLI example check on pinned .NET 11. Execute generated code, including exact overflow boundaries and IEEE NaN equality. Record actual results and environment limitations; open one draft PR and fix any CI failure on the same branch. DEC-01 already adopts this contract; no new numeric semantics are selected.

The new feature suite is registered through the existing aggregate to avoid the previously observed GitHub test-report size limit; existing assertions and the trusted verifier remain intact.

## Results

- Bootstrap and `scripts/verify-compiler.sh` passed: 18 compiler tests and 378 feature registrations, with a warning-free Release build. After adding final mixed-type negative cases, the feature aggregate passed again.
- The D# CLI accepted `examples/float_arithmetic.ds`. Generated-code tests cover exact maximum/max+1, normal and subnormal ties, signed underflow, NaN/infinity, nested/nullable/generic equality, key rejection, culture and multi-file use. An independent exact-rational oracle checked 2,524 random/extreme/invalid tokens and seven adversarial 10,000-digit inputs with no mismatches.
- The two obsolete `float` rejection inputs were narrowly migrated to unknown `float128`; their surrounding assertions remain unchanged.
- `.github/ai/verify.sh` passed documentation and all 25 automation tests. Its isolated compiler stage could not run locally because downloading the pinned SDK Docker image returned `Forbidden`; GitHub CI must provide that result.
