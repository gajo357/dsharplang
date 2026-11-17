# Issue 195: Inference through conditional calls

**Base:** `ce43e8c` (merged #194).
**Issue:** [#195](https://github.com/gajo357/dsharplang/issues/195).
**Dependencies:** omitted-return inference #35, static call inference #185/#191, conditional immutable calls #193 and static extensions #47 are merged.
**Status:** Implemented; ready for review.

## Scope and decisions

Infer omitted return types through checked acyclic fully supplied conditional immutable calls when receiver and call target are provably static. Support applicable intrinsic collections and visible static extensions, chaining, generic/nullability, Option values and fallback. Preserve target precedence, type checking, conditional evaluation and existing lifetime rules. Account for every potentially executed argument, callback and extension body. Do not infer through unknown callable values, unresolved/ambiguous targets, indirect capability/resource/service dispatch, invalid conditional partials or recursive/cyclic dependencies. Existing tests and trusted verification stay unchanged; no new syntax, API, runtime behavior or relaxation of #193 restrictions.

## Steps and affected files

1. Extend compiler dependency analysis to classify proven conditional intrinsic and static-extension calls conservatively, reusing existing lexical type evidence and callable analysis where safe.
2. Include receiver, possible arguments, statically invoked callback and applicable extension dependency edges; preserve normal resolution, lexical shadowing, cycles and false positives for unavailable targets.
3. Add new feature tests with real D# and a runnable D# example, including runtime execution and negative cases; update status and this plan.

## Acceptance-to-test map

- Infer executed List/Map/Set and extension calls with present/null receivers, chaining and `??`; exercise generic and nullable/Option values, imports and precedence.
- Prove no skipped side effects or duplicate evaluation; cover safe statically known callback dependencies.
- Reject unknown callbacks, ambiguous/unknown targets, indirect service/resource dispatch, lexical lookalikes, cycles and declaration-order changes; preserve #193 restrictions and existing tests.
- Build generated C# warnings as errors and run existing compiler and feature tests.

## Verification

- `bash .github/ai/bootstrap.sh`: passed.
- `bash scripts/verify-compiler.sh`: passed (18 compiler and 376 feature tests).
- `bash .github/ai/verify.sh`: documentation and 25 automation tests passed. Its isolated compiler stage could not pull the pinned SDK image locally because the registry returned `Forbidden`; GitHub CI runs that stage.
- Focused execution tests passed with generated C# built as warnings-as-errors. They cover nullable List/Map/Set, generic record chains, Option distinctions, extension imports and precedence, callback evaluation, and conservative dynamic/cyclic dependency diagnostics.
- Independent adversarial tests reject service/resource dispatch hidden through callbacks, inferred aliases, factories, branches and generic identity calls, including both declaration orders.
- `examples/conditional_inference.ds` checked and compiled without warnings; generated `Demo.answer()` returned `2`.

Fix any CI failure on the same PR branch.
