# Issue 225: float32 literals and IEEE arithmetic

**Issue:** [#225](https://github.com/gajo357/dsharplang/issues/225), a partial child of [#49](https://github.com/gajo357/dsharplang/issues/49).
**Base:** `2328518` (merged PR #224).
**Dependencies:** Adopted [DEC-01](DEC-01.md), extended integer and decimal implementations #48/#215. Their remaining conversions/name questions are separate; #49 stays open for binary64 and conversions.

## Scope and implementation

Implement IEEE 754 binary32 `float32` and `f`-suffixed literals, with exact finite-range checks before round-to-nearest-even and bounded processing of extreme inputs. Preserve numeric tokenization and no implicit mixed-type conversion. Emit nonconstant float values so arithmetic, infinity, NaN and signed zero follow runtime IEEE semantics. Permit float32 in supported immutable data, generic and nullable contexts but recursively reject float-bearing Map/Set keys. Preserve IEEE equality through nullable, generic, tuple, list, record, union, Option and Result values, including identical-reference containers containing NaN; do not use CLR NaN equality or identity shortcuts to silently equate values.

1. Add bounded exact float32 literal parsing, invariant nearest-even conversion and source diagnostics; integrate lexer/parser/binder and C# emission in `src/DSharp.Compiler/Compiler.cs` and a focused literal helper file if needed. Preserve hexadecimal `0x1f` as integer and member access.
2. Update `MultiFileResolver.cs`, `MatchCoverage.cs` and structural equality/nullable/generic lowering as needed. Reject float-bearing collection keys transitively, preserving other keys and safety.
3. Add `tests/features/Float32Tests.cs` plus `examples/float32_arithmetic.ds`: exact maxima/max+1/underflow/tie-even, separators/exponents, culture, arithmetic, NaN/infinity/signed zero, recursive equality/keys, generic and nullable values, diagnostics and multifile code. Put match arms on separate lines.
4. Update `docs/features/DSharp_Implementation_Status.md`; explicitly retain binary64, conversions and formatting as future work.

## Verification

Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`, `.github/ai/verify.sh` and the D# CLI example check using pinned .NET 11. Execute generated C# and exact-boundary/IEEE tests rather than relying on parsing or default CLR equality. Record actual results and limitations. Open one draft PR and fix any CI failure on the same branch. No new public contract is introduced; DEC-01 already adopts this behavior.

The new feature suite is registered through the existing aggregate to avoid the previously observed GitHub test-report size limit; existing assertions and the trusted verifier remain intact.

## Results

- Bootstrap and `scripts/verify-compiler.sh` passed: 18 compiler tests and 378 feature registrations, with zero Release build warnings or errors. The focused feature aggregate passed again after adding exact subnormal midpoint and adjacent-value cases.
- An independent exact-rational IEEE binary32 oracle checked 1,536 deterministic random, extreme and invalid spellings, including range, rounding ties and subnormal boundaries, with zero mismatches. Generated-code tests cover IEEE NaN and signed zero, nested/nullable equality, key rejection, type safety, culture and cross-module use.
- The D# CLI accepted `examples/float32_arithmetic.ds`.
- `.github/ai/verify.sh` passed documentation and all 25 automation tests. Its isolated compiler stage could not run locally because downloading the pinned SDK Docker image returned `Forbidden`; GitHub CI must provide that result.
