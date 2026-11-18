# Issue 209: Immediately invoked bare method values

**Base:** `826def4` (merged #208).
**Issue:** [#209](https://github.com/gajo357/dsharplang/issues/209), a bounded child of #129.
**Dependencies:** contextual empty extension partials #197, contextual extension values #199, collection values #201 and bare collection pipes #203 are merged.
**Status:** Implemented; ready for review.

## Scope and decisions

Let immediate grouped invocation provide enough argument and expected-result context to select a unique supported immutable collection or extension method value, including zero-argument, multiple-argument and staged calls. Reuse existing callable/partial semantics: evaluate receiver once before supplied arguments, defer remaining work until saturation, and preserve async, nullable and Option behavior. Preserve actual record fields, qualified union cases, built-in/extension precedence, direct-import visibility, old uncontextualized bare-method and bare-extension-pipe rejections, and all capability/resource/builder restrictions. Keep indirect, ambiguous, recursive and unresolved return inference conservative; no new syntax, public APIs or generic/foreign/service method groups. Keep #129 open. D# match cases must each be on their own line separate from braces.

## Steps and files

1. Extend `src/DSharp.Compiler/Compiler.cs` grouped invocation/member binding with isolated argument/result type evidence and unique candidate selection. Reuse existing method-value and partial representations.
2. Preserve record/union/intrinsic/extension precedence, lexical/import scope, zero-arity and nullable handling, callback typing, inference graph safety, and receiver/argument order across early exits and suspension.
3. Add new feature tests and runnable D# example; update status and this plan. Preserve existing tests and trusted verification.

## Acceptance-to-test map

- Execute grouped List/Map/Set and extension methods, zero/multiple arguments, staged partials, generic callbacks, nullable and nested Option values and deferred async work.
- Check overload uniqueness, imported/hidden names, real-field/union/builtin precedence, nullability, ambiguity and old untyped/bare-pipe negatives.
- Check receiver/argument once and order, early return and suspension; reject capability/resource/builder escape, dynamic callbacks, recursion and invalid types.
- Build generated C# warnings as errors and run compiler/feature suites.

## Verification

- `bash .github/ai/bootstrap.sh`: passed.
- `bash scripts/verify-compiler.sh`: passed (18 compiler and 378 feature tests).
- `bash .github/ai/verify.sh`: documentation and 25 automation tests passed; the isolated compiler stage could not pull the pinned SDK image locally because the registry returned `Forbidden`. GitHub CI runs that stage.
- Focused generated-code execution passed with warnings as errors for List/Map/Set and extension methods, contextual generic map/fold, nullable/nested Option values, staged and zero-argument calls, overloads, imports, evaluation order, early returns, async deferral and capability rejection.
- Independent adversarial checks covered field and union precedence, shadowing, ambiguous overloads, recursion, dynamic service/resource dispatch and cancellation.
- `examples/grouped_method_values.ds` checked and compiled without warnings; generated `Demo.answer()` returned `42`. D# match cases use one case per line.

Fix CI failures on this PR branch.
