# Issue 197: Contextual empty-prefix extension partials

**Base:** `841862c` (merged #196).
**Issue:** [#197](https://github.com/gajo357/dsharplang/issues/197), a bounded child of #129.
**Dependencies:** ordinary partials #135, static extensions #47, nonempty extension partials #179, and contextual collection partials #187/#189 are merged.
**Status:** Implemented; ready for review.

## Scope and decisions

Enable empty-prefix immutable extension partials when an expected callable, pipe, or immediate grouped invocation provides enough type evidence to select one candidate. Preserve full zero-argument calls, callable-returning full methods, real-member precedence and visibility; a previously valid full call keeps its meaning. Uncontextualized empty extension partials and bare methods remain rejected. Capture the receiver once and defer execution until saturation, including async extensions; maintain builder/resource/service/interface and capability restrictions. Preserve existing assertions and trusted verification; leave #129 open.

## Steps and files

1. Extend `src/DSharp.Compiler/Compiler.cs` contextual extension applicability, overload selection and invocation evidence; reuse the checked partial representation and evaluate the receiver once.
2. Preserve full-call/real-member precedence, overloaded candidates, generic/nullability arguments, direct imports, async timing and lifetime checks. Keep omitted-return inference conservative where it cannot prove a unique static target.
3. Add new `tests/features/` coverage and an executable D# example; update implementation status and this plan.

## Acceptance-to-test map

- Execute typed callable, pipe, grouped and staged invocations, repeated calls, nullable/Option payloads and side-effect ordering; verify receiver capture and deferred async execution.
- Preserve existing valid full zero-argument methods, full callable results and real-member precedence; verify visibility, ambiguity, overload mismatch, generic context and errors.
- Reject unsupported uncontextualized/ambiguous calls, unsafe builder/resource/service/interface captures, invalid partial arguments, recursion and indirect dependency inference.
- Build generated C# with warnings as errors; run existing compiler and feature suites.

## Verification

- `bash .github/ai/bootstrap.sh`: passed.
- `bash scripts/verify-compiler.sh`: passed (18 compiler and 377 feature tests).
- `bash .github/ai/verify.sh`: documentation and 25 automation tests passed. The isolated compiler stage could not pull the pinned SDK image locally because the registry returned `Forbidden`; GitHub CI runs that stage.
- Focused execution tests passed with generated C# compiled as warnings-as-errors, covering contextual and grouped calls, overload precedence, null and Option values, imports, deferred async work, lifetime rejection, and receiver/argument order.
- Independent generated-code checks covered null and ambiguous pipes, full callable-returning methods, collection intrinsic collisions, early returns and genuinely suspended async receivers.
- `examples/extension_empty_partial.ds` checked and compiled without warnings; generated `Demo.answer()` returned `42`.

Fix CI failures on this PR branch.
