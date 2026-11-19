# Issue 217: Invariant decimal interpolation

**Issue:** [#217](https://github.com/gajo357/dsharplang/issues/217); partial child of [#50](https://github.com/gajo357/dsharplang/issues/50).
**Base:** `c6e13cc` (merged PR #216).
**Dependencies:** Adopted [DEC-01](DEC-01.md), existing interpolation #50 and decimal #215.

## Scope and approach

Allow non-nullable `decimal` interpolation holes using DEC-01's plain invariant formatting: no grouping or exponent, trim fractional trailing zeros, and render all zeros as `0`. Preserve the full 96-bit coefficient and scale range, interpolation's single left-to-right evaluation, early return, async and fault behavior, and all existing supported scalar formatting. Nullable/composite formatting, binary floats, named general formatting APIs and currency policies remain outside this slice; #49 and #50 stay open.

1. Extend scalar-hole binding and emitted decimal formatting in `src/DSharp.Compiler/Compiler.cs`, preserving existing lowering/evaluation order and using invariant pinned .NET behavior.
2. Add executable D# tests in `tests/features/DecimalInterpolationTests.cs` and a D# example under `examples/`; update `docs/features/DSharp_Implementation_Status.md`.
3. Cover zero and negative zero, positive and negative scale, trailing zeros, maximum/minimum magnitude, `1e-28m`, numbers with many significant digits, non-English culture, expression order/once, suspension/fault/early return and invalid nullable/composite holes.

## Verification

Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`, `.github/ai/verify.sh` and the D# CLI check with the pinned .NET 11 SDK. Build and execute generated code, including culture and async cases, with warnings as errors. Record actual results and limitations. Open one draft PR and fix any CI failure on that same branch. There is no unresolved public contract: DEC-01 specifies decimal rendering.

The new test is invoked by the existing feature aggregate: the prior three GitHub compiler runs independently passed all 379 tests but failed the report-size limit. This preserves every assertion and the trusted verifier while avoiding another oversized report; existing test assertions are unchanged.

## Results

- Bootstrap and `scripts/verify-compiler.sh` passed: 18 core compiler tests and 378 feature registrations, including all new assertions. The focused generated-code test passed under three cultures with decimal boundary values, negative zero, ordering, async and failure paths.
- The D# CLI accepted `examples/decimal_interpolation.ds`. An independent oracle checked 100,000 decimal values plus positive and negative zero at every scale against the invariant format on the pinned .NET 11 SDK.
- `.github/ai/verify.sh` passed documentation and all 25 automation tests. Its final isolated compiler stage could not run locally because downloading the pinned SDK Docker image returned `Forbidden`; GitHub CI must provide that result.
