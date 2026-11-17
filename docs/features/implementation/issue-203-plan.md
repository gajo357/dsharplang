# Issue 203: Bare immutable collection methods in pipes

**Base:** `53b53cd` (merged #202).
**Issue:** [#203](https://github.com/gajo357/dsharplang/issues/203), a bounded child of #129.
**Dependencies:** pipes #35, ordinary partials #135, immutable collection partials #187/#189, and contextual collection method values #201 are merged.
**Status:** Implemented; ready for review.

## Scope and decisions

Allow pipes to supply the first argument to a provably applicable bare immutable List/Map/Set method, including staged calls and contextual generic map/fold where existing type rules suffice. Preserve left-value-before-receiver evaluation, single evaluation, partial-application timing, nullability, Option and immutable data. Preserve actual field/union precedence, shadowing, nullable receiver checks, callback inference, and resource/builder/service/interface restrictions. Bare extension-method pipes and uncontextualized bare methods remain unsupported. Keep #129 open and existing tests and trusted verification unchanged.

The user also requested one match case per line for future D# examples. Update the prior collection-method example and record this preference in the repository instructions; no test expectations change.

## Steps and files

1. Extend compiler pipe/member binding to resolve only applicable intrinsic collection method values from the pipe input and expected result, including multi-argument partials and supported generic inference.
2. Reuse existing pipe, bound method and partial representations; retain source-order execution, nullable and Option semantics, intrinsic/field/union precedence, inference/callback and lifetime checks.
3. Add a new feature suite and D# example, with every match case on a separate line; correct the earlier example and update status, instructions and plan.

## Acceptance-to-test map

- Execute List/Map/Set bare method pipes, staged calls and contextual generic map/fold; cover nullable and nested Option values, receiver capture, repeated calls and left-before-right evaluation.
- Preserve real field/union and extension behavior, untyped method/extension pipe rejections, shadowing, invalid types and nullable receiver diagnostics.
- Reject unsafe resource, builder and service/interface captures and indirect/cyclic inferred dependencies; execute generated C# with warnings as errors.
- Run the existing compiler and feature suites.

## Verification

- `bash .github/ai/bootstrap.sh`: passed.
- `bash scripts/verify-compiler.sh`: passed initially (18 compiler and 378 feature tests). GitHub then passed all tests but rejected its oversized report. The new suite was moved into the established aggregate without changing its assertions; the final rerun passed (18 compiler and 377 feature results).
- `bash .github/ai/verify.sh`: documentation and 25 automation tests passed; the isolated compiler stage could not pull the pinned SDK image locally because the registry returned `Forbidden`. GitHub CI runs that stage.
- Focused generated-code tests passed with warnings as errors for List/Map/Set pipes, staged calls, nullable and Option distinctions, generic callbacks, left-before-receiver order, async suspension and capability rejection.
- Independent probes covered lexical/field/union precedence, dynamic and recursive inference, null keys, nullable generic fold and service/resource/builder capture.
- `examples/collection_method_pipes.ds` checked and compiled without warnings, and generated `Demo.answer()` returned `42`; the reformatted collection-method example also checks successfully. All new D# match cases occupy separate lines.

Fix any CI failure on the same PR branch.
