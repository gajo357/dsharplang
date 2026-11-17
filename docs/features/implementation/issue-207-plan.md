# Issue 207: Inference through bare collection-method pipes

**Base:** `2f4b6b4` (merged #206).
**Issue:** [#207](https://github.com/gajo357/dsharplang/issues/207), a bounded #129/#35 follow-up.
**Dependencies:** return inference #35, collection partial inference #185/#191, conditional inference #195, and bare collection pipes #203 are merged.
**Status:** Implemented; ready for review.

## Scope and decisions

Infer omitted returns through checked acyclic pipes to statically proven intrinsic bare List/Map/Set methods, including supported staged set/fold and known callbacks. Preserve normal name/field/extension resolution, nullability/Option tags, evaluation order and lifetime restrictions. Unknown/dynamic callbacks and method values, service/interface/resource dispatch, ambiguous/unresolved targets, cyclic dependencies and unsupported bare extensions remain conservative. Zero-argument length/count are not valid pipe targets. No syntax or runtime behavior changes; preserve all existing tests and trusted verification and keep #129 open. Format every D# match case on its own line, separately from braces.

## Steps and files

1. Extend `src/DSharp.Compiler/Compiler.cs` dependency analysis to prove only real bare collection targets and safe staged completion; include receiver, argument, callback and callee edges without guessing ambiguous resolution.
2. Preserve lexical scopes, checked-type cycles, service/resource shadowing and normal extension/field precedence; do not treat extra invocation of returned functions as static completion.
3. Add new feature execution tests and runnable D# example. Update status and this plan.

## Acceptance-to-test map

- Execute inferred List/Map/Set pipes, generic and nullable/Option values, known callbacks, grouped/staged set/fold, imports, receiver and source evaluation order.
- Reject unknown callbacks/targets, zero-arg pipe targets, invalid types, service/resource lookalikes, illegal captures and cycles in both declaration orders.
- Preserve extension and field precedence, lexical shadowing, unrelated previously valid programs and existing negative tests.
- Build generated C# warnings as errors and run compiler and feature suites.

## Verification

- `bash .github/ai/bootstrap.sh`: passed.
- `bash scripts/verify-compiler.sh`: passed (18 compiler and 379 feature tests).
- `bash .github/ai/verify.sh`: documentation and all 25 automation tests passed; the isolated compiler stage could not pull the pinned SDK image locally because the registry returned `Forbidden`. GitHub CI runs that stage.
- Focused generated-code tests passed with warnings as errors, covering List/Map/Set pipelines, staged set/fold, generic nullable and Option values, static callbacks, imports, source order, and conservative diagnostics.
- Independent adversarial probes covered post-saturation invocation, callback cycles, lexical shadowing, same-name extensions and service/resource dispatch including factories and aliases.
- `examples/bare_collection_pipe_inference.ds` checked and compiled without warnings; generated `Demo.answer()` returned `42`. D# match cases use one case per line.

Fix CI failures on this PR branch.
