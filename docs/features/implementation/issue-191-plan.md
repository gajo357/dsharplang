# Issue 191: Return inference through empty-prefix collection partials

**Base:** `85661fc` (merged #190).
**Issue:** [#191](https://github.com/gajo357/dsharplang/issues/191), a bounded
child of [#129](https://github.com/gajo357/dsharplang/issues/129).
**Dependencies:** return inference #35, ordinary partials #135, partial
inference #185, collection empty prefixes #187 and generic List prefixes #189
are merged.
**Status:** Implemented; ready for review.

## Scope and decisions

Infer checked, acyclic omitted returns through immediate grouped and piped calls
to provably intrinsic empty-prefix immutable List/Map/Set partials, including
supported contextual generic `List.map`/`fold` and staged calls. Retain real
member/extension resolution, normal nullability, evaluation order and ownership.
Track whether a callback is actually invoked: only statically known lambdas or
unshadowed named functions may establish its dependencies. Unknown callbacks,
dynamic service/interface/resource dispatch, ambiguous extension fallbacks and
recursive/cyclic checked-type dependencies remain conservative.

Do not change uncontextualized generic `map()`/`fold()` rejection, existing
full extension calls, mutable builders, other #129 method families or any public
API or runtime behavior. Keep #129 open.

## Steps and affected files

1. Extend compiler dependency/inference analysis to recognize only proven
   intrinsic empty-prefix partial targets and safe staged/grouped/pipe completion;
   preserve lexical scopes, callback invocation edges and overload precedence.
2. Keep real extension fallback, nullable/generic type evidence, indirect calls,
   capabilities, resource lifetimes and inference cycles checked conservatively.
3. Add runnable D# example and feature tests, update status and this plan;
   preserve existing assertions and trusted verification.

## Acceptance-to-test map

- Execute static monomorphic/generic collection partials in pipes, grouped and
  staged calls; verify inferred types, nullable values and evaluation order.
- Reject unknown dynamic callbacks, service/resource lookalikes, recursion,
  shadowed types and ambiguous extension fallback; preserve existing valid
  extension and zero-arity calls.
- Check builder/resource/capability non-escape, imports and full operations;
  generated C# builds with warnings as errors.
- Run the existing compiler and feature suites.

## Verification

- `bash .github/ai/bootstrap.sh`: passed.
- `bash scripts/verify-compiler.sh`: passed (18 compiler and 377 feature tests).
- `bash .github/ai/verify.sh`: documentation and all 25 automation tests passed; the isolated compiler stage could not pull the pinned .NET SDK image locally because the registry returned `Forbidden`. GitHub CI runs that stage.
- The D# example checked and compiled with no C# warnings, and its generated `answer()` returned `42`.
- Independent adversarial checks covered callback cycles, unknown callbacks, lexical shadowing, service/resource dispatch and extension fallback.
