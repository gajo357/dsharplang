# Issue 233: Return inference through bare extension-method pipes

**Issue:** [#233](https://github.com/gajo357/dsharplang/issues/233), a bounded child of [#129](https://github.com/gajo357/dsharplang/issues/129).
**Base:** `aeb2d25` (merged PR #232).
**Dependencies:** Adopted pipe specification #129, static extensions #47, bare collection pipe inference #207 and bare extension pipes #211.

## Scope and steps

Infer omitted return types for checked, acyclic bare extension-method pipes where a unique, safe target is statically proven. Begin with synchronous single-argument extensions on a known immutable receiver and known non-function input, returning supported non-function data. Preserve existing pipe evaluation order, normal field and intrinsic precedence, import visibility, nullable behavior, overload diagnostics and resource, capability and builder restrictions. Keep unknown, ambiguous, indirect, recursive and unsupported staged or callback cases conservative. Do not add syntax, runtime APIs or relax ownership. Keep #129 open.

1. Extend the dependency analysis in `src/DSharp.Compiler/Compiler.cs` to prove the exact extension target and include its strict dependency, receiver and input edges. Limit any binder inference relaxation to the same proven pipe and selected target.
2. Add runnable D# examples and new feature tests covering inferred calls, imports and receiver/input evaluation order, along with rejected cycles, ambiguity, dynamic dispatch and lifetime escapes. Keep match cases on separate lines.
3. Update `docs/features/DSharp_Implementation_Status.md` with the precisely tested scope and preserve existing assertions and trusted verification.

## Acceptance and verification

- Execute inferred and annotated equivalents, including visible imported extensions, grouping and source/receiver evaluation once and in order.
- Reject recursive dependency cycles, ambiguous or unknown resolution, wrong types and unsafe capability/resource/builder cases; retain intrinsic and field precedence.
- Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`, `bash .github/ai/verify.sh`, and the D# CLI example check on pinned .NET 11. Record results and environment limitations. Open one draft PR and repair its CI on the same branch.

No public contract decision is needed: this slice makes existing supported extension-pipe behavior available to conservative inference.

## Results

- Bootstrap and `scripts/verify-compiler.sh` passed: 18 compiler tests and 378 feature registrations, with no build warnings. The focused generated-code suite passed and the D# CLI accepted `examples/bare_extension_pipe_inference.ds`.
- The new tests execute imported and local calls, grouping and fault-based evaluation order. They reject cycles through the extension, receiver and input, dynamic callbacks, hidden service dispatch, unsupported overloads, collisions and unsafe captures. Thirteen additional independent adversarial CLI probes passed.
- `.github/ai/verify.sh` passed documentation and all 25 automation tests. The isolated compiler stage could not run locally because downloading the pinned Docker image returned `Forbidden`; GitHub CI supplies that result.
