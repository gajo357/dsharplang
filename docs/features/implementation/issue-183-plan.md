# Issue 183: Partial application of immutable List.fold

**Base:** `91eaee7` (merged #182).
**Issue:** [#183](https://github.com/gajo357/dsharplang/issues/183), a bounded
child of [#129](https://github.com/gajo357/dsharplang/issues/129).
**Dependencies:** List #37, ordinary partial application #135, extension
partials #179 and Map.set partial #181 are merged; DEC-01 and the
[positional-prefix contract](../DSharp_Pipes_and_Logging.md#3-automatic-partial-application)
are adopted.
**Status:** Implemented; ready for review.

## Scope and decisions

For immutable `List<T>`, `items.fold(initial)` produces a function taking the
remaining `(A,T) -> A` callback and returning `A`. Evaluate receiver and initial
once, in order, at construction. Each completed call folds the captured list
left to right; an empty list returns the captured initial. Preserve full fold,
real-member precedence and existing type and lifetime rules. Accumulator `A`
may be inferred from the initial, an explicit fold type argument, or a compatible
expected function context; unresolved or incompatible types are diagnosed.

A partial retains the receiver and initial, so reject builder/resource/service
or interface capability capture, including through generic substitution and
indirect closure capture. This must not change fully applied fold behavior.
Other collection methods, empty prefixes, method values, service/interface
methods, foreign interop and remaining #129 integrations are separate work.
No new public API or language rule is assumed. An inferred-return function
immediately invoking or piping into this partial may still need an explicit
return type under existing conservative dependency analysis.

## Steps and files

1. Extend List.fold binding in `src/DSharp.Compiler/`, preserving fully supplied
   calls, typed arguments and real-member precedence. Infer or validate `A`.
2. Lower using capture-safe closure machinery, or a bound form integrated with
   generic capability checks, async, return/propagation and nullable projection.
3. Add executable D# acceptance tests and a runnable `examples/` program; update
   implementation status and this plan. Retain all assertions and verifier rules.

## Acceptance-to-test map

- Capture/deferral: receiver then initial once, no early callbacks, repeated
  calls, ordered iteration, empty list, faults and original value preserved.
- Types/composition: grouped/pipe calls, inferred, explicit and contextual `A`,
  nullable/Option/generic data, modules, async and early return.
- Safety/lookup: wrong types, unresolved null, excess args, discarded function,
  direct or generic/indirect captured capabilities and real-member precedence.
- Regression: existing compiler/features and full List.fold, generated C# with
  warnings as errors.

## Verification

- `bash .github/ai/bootstrap.sh`: passed.
- `bash scripts/verify-compiler.sh`: passed (18 compiler tests and 377 feature
  tests, including the aggregated List.fold partial suite).
- `bash .github/ai/verify.sh`: documentation checks and all 25 automation
  tests passed. The isolated Docker stage could not download the pinned SDK
  image locally because the registry returned `Forbidden`; GitHub CI runs it.
- Focused generated-C# execution passed with warnings as errors, covering
  evaluation order, nullable and generic values, async and capability safety.
- `examples/list_fold_partial.ds`: checked, compiled without warnings and
  executed; `Demo.answer()` returned `16`.

Fix any CI failures on this PR branch.
