# Issue 185: Return inference through statically resolved partial calls

**Base:** `3d5811d` (merged #184).
**Issue:** [#185](https://github.com/gajo357/dsharplang/issues/185), a bounded
child of [#129](https://github.com/gajo357/dsharplang/issues/129).
**Dependencies:** return inference #35, ordinary partials #135, extension
partials #179, Map.set partial #181 and List.fold partial #183 are merged.
**Status:** Implemented; ready for review.

## Scope and decisions

Allow checked, acyclic omitted-return functions to immediately invoke or pipe
into a provably statically resolved Map.set, List.fold or immutable extension
partial. Establish dependencies using actual static targets and callbacks; retain
DS2027 for unresolved callable values, indirect dispatch and recursive/cyclic
checked-type dependencies. Preserve overload resolution, real-member
precedence, local scopes, nullability and evaluation order.

Close the existing resource-method lookalike hole: an unannotated function must
not infer through a scoped-resource method merely because its name or argument
resembles a collection operation. Fully checked explicit-return resource methods
and cleanup behavior remain supported. Where static receiver/callback identity
cannot be proven, preserve the conservative annotation requirement. No language
or public API decision is introduced; broader #129 integration remains open.

## Steps and affected files

1. Adjust dependency/return-inference analysis in `src/DSharp.Compiler/` to
   recognize only proven partial-call targets and their invoked callbacks,
   respecting lexical/type scopes, imports, precedence and inference cycles.
2. Guard indirect scoped-resource dispatch during inference without changing
   explicit-return resource behavior or the capability/ownership model.
3. Add executable D# feature coverage and a runnable example; update the status
   and plan. Preserve existing assertions and trusted verification.

## Acceptance-to-test map

- Positive: acyclic grouped/piped static Map.set, List.fold and extension
  partials, typed locals/parameters, imports, nullable data and order.
- Safety: unknown callback/function values, service/interface/resource and
  user-type lookalikes require annotations; detect cycles in either declaration
  order, including resource callback-shaped recursive bodies.
- Lookup: real-member priority, ambiguous overloads, full calls and generic
  constraints remain checked; generated C# executes with warnings as errors.
- Regression: existing compiler and feature suites, using cleanup and faults.

## Verification

- `bash .github/ai/bootstrap.sh`: passed.
- `bash scripts/verify-compiler.sh`: passed (18 compiler tests and 377 feature
  tests, including the aggregated partial-inference suite).
- `bash .github/ai/verify.sh`: documentation checks and all 25 automation
  tests passed. The isolated Docker stage could not download the pinned SDK
  image locally because the registry returned `Forbidden`; GitHub CI runs it.
- Focused generated-C# tests passed with warnings as errors, including static
  partials, nullable values, indirect/cyclic rejection and resource cleanup.
- `examples/partial_inference.ds`: checked, compiled without warnings and
  executed; `Demo.answer()` returned `42`.

Fix any CI failures on this PR branch.
