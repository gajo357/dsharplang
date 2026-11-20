# Issue 235: Extension-pipe inference on immutable collections

**Issue:** [#235](https://github.com/gajo357/dsharplang/issues/235), a bounded child of [#129](https://github.com/gajo357/dsharplang/issues/129).
**Base:** `ce0580b` (merged PR #234).
**Dependencies:** Adopted pipe contract #129, extension methods #47, collection-method pipes #203, bare extension pipes #211 and their conservative return inference #233.

## Scope and steps

Extend #233's checked, acyclic inference to bare pipes selecting a unique synchronous unary extension on a statically typed immutable List, Map or Set receiver. Preserve exact data argument/result matching, intrinsic member precedence, imports, evaluation order, overload conservatism, dependency/cycle analysis, and the existing capability, resource and builder restrictions. Unknown collection types, unsupported staged or callback applications and async extensions remain conservative. No new syntax, API or lifetime rule is introduced; #129 stays open.

1. Update static target proof in `src/DSharp.Compiler/Compiler.cs` for non-colliding typed collection receivers and keep it aligned with ordinary binder precedence.
2. Add executable D# feature tests and an example under `tests/features/` and `examples/`, plus an accurate implementation-status update.
3. Execute List, Map and Set cases and imported extensions; verify evaluation and fault order. Reject intrinsic-name collisions, ambiguous/unknown targets, dependency cycles and invalid capability/resource/builder cases; preserve existing tests and trusted verification.

## Verification

Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`, `bash .github/ai/verify.sh`, and the D# CLI example check with pinned .NET 11. Record actual results and environment limitations. Open one draft PR and repair any CI failures on the same branch. No public contract decision is required.

## Results

- Bootstrap and `scripts/verify-compiler.sh` passed: 18 compiler tests and 378 feature registrations, with no build warnings. The focused generated-code tests passed, and the D# CLI accepted `examples/collection_extension_pipe_inference.ds`.
- The new D# cases execute List, Map and Set pipes, nullable elements, imports, grouping and fault order. They check intrinsic precedence, ambiguity, cycles, hidden dispatch and unsafe captures. Independent reviewers ran additional adversarial CLI probes, including same-name intrinsic calls and their result types.
- `.github/ai/verify.sh` passed documentation and all 25 automation tests. Its isolated compiler stage could not run locally because the pinned Docker image download returned `Forbidden`; GitHub CI supplies that verification.
