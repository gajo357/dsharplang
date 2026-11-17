# Issue 201: Contextual immutable collection method values

**Base:** `4c6edc1` (merged #200).
**Issue:** [#201](https://github.com/gajo357/dsharplang/issues/201), a bounded child of #129.
**Dependencies:** immutable lists/maps/sets #37/#38, collection partials #187/#189 and contextual extension method values #199 are merged.
**Status:** Implemented; ready for review.

## Scope and decisions

Allow an expected non-nullable callable type to select an applicable bare immutable List/Map/Set method, including zero-argument length/count and contextual generic List map/fold. Capture the receiver once; invoke the operation and its callback only when called. Preserve immutable snapshots, nullable/Option distinctions, field and union precedence, existing uncontextualized rejection and existing same-name extension collision behavior. Do not introduce bare-pipe behavior, untyped method values, service/interface or resource method values, or generic extension declarations. Keep dynamic dependency inference conservative and preserve all existing tests and trusted verification; #129 remains open.

## Steps and files

1. Extend `src/DSharp.Compiler/Compiler.cs` field-access binding to recognize exact applicable built-in collection callable types in expected-function contexts, reusing existing bound partials and generic type inference where safe.
2. Preserve nullability, callback/lifetime checks, receiver evaluation and invocation timing; maintain real-field, union, visible extension and diagnostics precedence. Track dependencies conservatively.
3. Add new `tests/features/` execution tests and D# example; update status and this plan.

## Acceptance-to-test map

- Execute List/Map/Set method values, zero-argument, contextual map/fold, generic and nullable/Option cases, typed locals/arguments/returns, receiver once and repeat calls.
- Verify deferred callback execution and argument order; reject mismatched/untyped methods, illegal resource/builder/service captures and old extension collision cases.
- Preserve existing inference, shadowing, nullability, field/union handling and recursive/dynamic dependency diagnostics.
- Build generated C# warnings as errors; run full compiler and feature tests.

## Verification

- `bash .github/ai/bootstrap.sh`: passed.
- `bash scripts/verify-compiler.sh`: passed (18 compiler and 379 feature tests).
- `bash .github/ai/verify.sh`: documentation and 25 automation tests passed; the isolated compiler stage could not pull the pinned SDK image locally because the registry returned `Forbidden`. GitHub CI runs that stage.
- Focused generated-code tests built with warnings as errors and executed List, Map and Set method values, nullable/Option distinctions, generic map/fold, zero-argument calls, callback and receiver order, and async receiver behavior.
- Independent probes covered field and extension precedence, recursive and dynamic inference, shadowing and rejected resource/builder/service captures.
- `examples/collection_method_values.ds` checked and compiled without warnings; generated `Demo.answer()` returned `42`.

Fix CI failures on this PR branch.
