# Issue 229: Invariant binary floating-point interpolation

**Issue:** [#229](https://github.com/gajo357/dsharplang/issues/229), a partial child of [#50](https://github.com/gajo357/dsharplang/issues/50).
**Base:** `0edf580` (merged PR #228).
**Dependencies:** Adopted [DEC-01](DEC-01.md), existing interpolation, binary32 #225, and binary64 #227.

## Scope and approach

Support non-nullable `float32` and `float` interpolation with DEC-01's invariant shortest round-trip decimal, closest candidate and ties-to-even. Construct both fixed and normalized scientific forms, each with a decimal point and a fractional digit, choose the shorter and prefer fixed on ties. Preserve signed zero and special-value spelling. Leave nullable and composite holes, general format/parse APIs and explicit conversions out of scope. Keep #49 and #50 open.

1. Extend scalar interpolation binding and C# lowering in `src/DSharp.Compiler/Compiler.cs`, using the pinned .NET round-trip formatter separately for each width and canonicalizing notation. Preserve interpolation evaluation and async semantics.
2. Add executable D# feature tests under `tests/features/`, a D# example under `examples/`, and an accurate implementation-status update.
3. Map acceptance to tests: both widths and exact boundary/subnormal values, shortest notation and ties, signed zero and non-finite values; culture independence; single evaluation, order, async and failure behavior; rejection of unsupported holes. Check candidates with an independent exact-rational oracle where practical.

## Verification

Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`, `bash .github/ai/verify.sh`, and the D# CLI example check on the pinned .NET 11 SDK. Execute generated code and record actual results and limitations. Open one draft PR; fix CI failures on that branch. The adopted contract requires no new public decision.

## Results

- Bootstrap and `scripts/verify-compiler.sh` passed, including 18 compiler tests and 378 feature registrations; the Release build had no warnings. The new feature tests execute generated D# code across three cultures, multiple files, edge values and ordering, asynchronous completion, faults and cancellation. The D# CLI accepted `examples/float_interpolation.ds`.
- An independent exact-integer midpoint oracle checked 12,902 positive and negative values, including seeded random values and each binary exponent boundary and its neighbors, against both the pinned runtime's shortest/closest/even candidate and the actual emitted formatting helper. All matched.
- `.github/ai/verify.sh` passed documentation and all 25 automation tests. The isolated compiler stage could not run locally because the pinned Docker image download returned `Forbidden`; GitHub CI must provide that result.
