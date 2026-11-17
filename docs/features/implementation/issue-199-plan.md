# Issue 199: Contextual extension method values

**Base:** `bd5a319` (merged #198).
**Issue:** [#199](https://github.com/gajo357/dsharplang/issues/199), a bounded child of #129.
**Dependencies:** function values #33/#34, static extensions #47, extension partials #179 and contextual empty prefixes #197 are merged.
**Status:** Implemented; ready for review.

## Scope and decisions

In a non-nullable expected callable context, resolve a uniquely matching visible immutable-value extension used through existing bare member syntax, e.g. `let add: (int)->int = 40.add`. Capture its receiver once and defer its body until invocation, including zero-argument and async methods. Preserve record-field and qualified union-case precedence, existing uncontextualized rejection, import visibility, ambiguous overload diagnostics and builder/resource/capability restrictions. Bare service/interface/foreign methods, generic extension declarations, uncontextualized member values and bare-method pipe inference are outside this slice. No new syntax, public API or runtime protocol; keep #129 open and all existing tests and trusted verification unchanged.

## Steps and files

1. Extend source member-access resolution in `src/DSharp.Compiler/Compiler.cs` and `MultiFileResolver.cs` with file-scoped extension visibility when an expected callable type selects one extension; keep actual field and qualified case precedence.
2. Reuse safe receiver-capturing callable lowering, including correct zero-arity and async timing, nullable/generic value substitution, and prohibitions on capability/resource/builder capture.
3. Add new `tests/features/` coverage and runnable D# example; update implementation status and this plan.

## Acceptance-to-test map

- Execute typed locals, parameters and return contexts, zero/multiple arguments, async deferral, repeat calls and nullable/Option values; observe receiver once and invocation ordering.
- Preserve real field/qualified-case precedence, imports, overload ambiguity, lexical shadowing, expected-type mismatch and the old uncontextualized rejection.
- Reject unsafe builder/resource/service/interface receivers or captured capabilities and illegal escapes. Keep indirect/recursive inferred-return analysis conservative.
- Build generated C# with warnings as errors and run existing compiler and feature tests.

## Verification

- `bash .github/ai/bootstrap.sh`: passed.
- `bash scripts/verify-compiler.sh`: passed (18 compiler and 378 feature tests).
- `bash .github/ai/verify.sh`: documentation and 25 automation tests passed; the isolated compiler stage could not pull the pinned SDK image locally because the registry returned `Forbidden`. GitHub CI runs that stage.
- Focused execution tests passed with generated C# built with warnings as errors. They cover typed method values, zero/multiple arguments, async deferral, exact nullable receiver targets, Option payloads, imports, field/collection precedence, lifetime rejection and evaluation order.
- Independent adversarial checks covered nested and overloaded callback cycles, generic inference, indirect dispatch, nullable receivers, and existing inferred record fields.
- `examples/extension_method_values.ds` checked and compiled without warnings; generated `Demo.answer()` returned `42`.

Fix CI failures on this PR branch.
