# Issue 193: Conditional calls to immutable value methods

**Base:** `d87d510` (merged #192).
**Issue:** [#193](https://github.com/gajo357/dsharplang/issues/193), a follow-up to #29.
**Dependencies:** conditional fields #29, nullable generics #30, immutable collections #37/#38, modules #46, and static extensions #47 are implemented.
**Status:** Implemented; ready for review.

## Scope and decisions

Support `?.` with fully supplied synchronous immutable-value method calls, using the existing nullability, collection and extension semantics. Evaluate the receiver once; evaluate arguments and invoke the method only if present. Lift results to nullable idempotently, preserving Option/null distinctions. Preserve ordinary resolution and import visibility. Reject conditional partials, task results, builders, resources and service/interface dispatch pending their separate contracts. Existing syntax and behavior, capability/lifetime checks and existing test assertions remain unchanged.

## Steps and affected files

1. Extend syntax and module resolution to preserve conditional-call receiver, type arguments, arguments and visible extension candidates.
2. Reuse checked member-call resolution for the non-null receiver; conservatively analyze dependencies and control flow; enforce immutable receiver/result and full synchronous invocation.
3. Lower via the existing nullable representation, ensuring receiver is evaluated once and all arguments/calls occur only on the present path. Preserve generic and idempotent nullable substitution and early-exit boundaries.
4. Add new feature tests and runnable D# example. Update implementation status and this plan; keep trusted verification and existing test assertions unchanged.

## Acceptance-to-test map

- Runtime: nullable and present receivers; collection intrinsic and extension methods; reference/value/nullable/Option results, chaining and fallback; receiver once, arguments in order, skipped failures and method effects.
- Checking: wrong types/arity, missing or ambiguous methods, nullable direct access, source locations, imports and precedence; generic nullable arguments and result flattening.
- Safety: prevent indirect service/interface/resource dispatch, builder capture, partial application, async/Task results, indirect/cyclic inferred dependencies and incorrect early exits.
- Regression: all existing compiler and feature tests; generated C# builds with warnings as errors.

## Verification

- `bash .github/ai/bootstrap.sh`: passed.
- `bash scripts/verify-compiler.sh`: passed initially (18 compiler and 378 feature tests). The GitHub runner then exceeded the existing test-report size cap despite every test passing. The new suite now runs through the established feature aggregate to keep the report bounded; existing assertions and trusted verification are unchanged. The full rerun passed (18 compiler and 377 feature tests, including the aggregated conditional-call suite).
- `bash .github/ai/verify.sh`: documentation and all 25 automation tests passed; the isolated compiler stage could not pull the pinned SDK image locally because the registry returned `Forbidden`. GitHub CI runs this stage.
- Focused execution tests passed with generated C# built as warnings-as-errors, including lazy argument evaluation, early return, Result propagation, await suspension, null and Option distinctions, generics, imports, precedence and rejected calls.
- The D# example checked and compiled without warnings; its generated `Demo.answer()` returned `Ada`.
- Independent safety checks covered generic value/reference nullable substitutions and rejected partial, resource, builder, service, Task and callable cases. Omitted-return functions still require an explicit annotation for conditional calls.

Fix CI failures on the same PR branch.
