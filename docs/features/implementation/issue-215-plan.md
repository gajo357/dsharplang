# Issue 215: Decimal literals and arithmetic

**Issue:** [#215](https://github.com/gajo357/dsharplang/issues/215); partial child of [#49](https://github.com/gajo357/dsharplang/issues/49).
**Base:** `4d69e7e` (merged PR #214).
**Dependencies:** Adopted [DEC-01](DEC-01.md), existing checked numeric and integer-width implementation (#48). The remaining #48 conversion and numeric-name questions are independent of same-type decimal operations and remain open.

## Scope and approach

Implement the adopted decimal type and exact `m`-suffixed literals across the existing safe value contexts, with same-type unary, arithmetic and comparison operations, nullable/generic/data handling, and map/set keys. Check literal coefficient and scale using integer arithmetic without silent rounding; apply the adopted exponent and underscore grammar and preserve integer/member tokenization. Reuse the pinned .NET decimal operations for runtime checked behavior and scale-insensitive equality. Do not implement binary floating point, mixed numeric conversions, text formatting, or currency semantics; keep #49 open.

1. Update lexer, literal analysis, type resolution and binder/emitter in `src/DSharp.Compiler/Compiler.cs`, `MultiFileResolver.cs` and `MatchCoverage.cs` as required. Preserve all existing tests and diagnostics.
2. Add executable D# example and feature tests for exact literal boundaries, exponent/underscore forms, context and member tokenization; malformed, mixed numeric and overprecision rejection; arithmetic, comparison, overflow, division-by-zero, equality and map/set keys; nullable/Option, generic and nested values, multi-file and culture invariance.
3. Record precisely tested scope in `docs/features/DSharp_Implementation_Status.md`.

## Verification and decisions

Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`, `.github/ai/verify.sh`, and D# CLI example checks using the pinned .NET 11 SDK. Generated C# execution must verify runtime behavior, not syntax alone. Record actual results and limitations. Open one draft PR and fix any CI failure on that same branch. No new public contract is needed: DEC-01 is adopted; conversions, formatting and floating point are explicitly deferred.

## Results

- Bootstrap and `scripts/verify-compiler.sh` passed: 18 compiler tests and 379 feature tests, with zero build warnings or errors. The decimal acceptance test executed generated C# under `fr-FR`, including exact literal boundaries, nested nullable/Option and collection behavior, numeric match reachability and runtime arithmetic faults.
- The D# CLI accepted `examples/decimal_arithmetic.ds`.
- `.github/ai/verify.sh` passed documentation and all 25 automation tests. Its final isolated compiler stage could not run locally because the pinned .NET SDK Docker image download returned `Forbidden`; GitHub CI must provide the isolated result.
- GitHub's first compiler run passed all 379 tests but rejected the TRX as oversized. The decimal test now runs through the existing feature aggregate to reduce report entries; all assertions and the trusted report-size verifier remain intact.
