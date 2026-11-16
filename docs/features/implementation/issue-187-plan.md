# Issue 187: Empty-prefix immutable collection partials

**Base:** `05b8dd1` (merged #186).
**Issue:** [#187](https://github.com/gajo357/dsharplang/issues/187), a bounded
child of [#129](https://github.com/gajo357/dsharplang/issues/129).
**Dependencies:** List #37, Map/Set #38, ordinary partials #135, extension
partials #179, Map.set #181, List.fold #183 and partial inference #185 are
merged. DEC-01 and the [partial-application contract](../DSharp_Pipes_and_Logging.md#3-automatic-partial-application)
are adopted.
**Status:** Implemented; ready for review.

## Scope and decisions

An empty call on an immutable collection method that requires arguments returns
its ordinary function type: List `append`, `get`, `filter`; Map `get`, `contains`,
`set`; and Set `add`, `contains`. Capture the immutable receiver once when the
function is formed; defer the operation until its required arguments arrive.
Existing zero-argument `length()` and `count()` are full calls. Real intrinsic
members retain precedence when applicable. Existing fully supplied zero-argument
extension calls must keep their behavior; an expected callable may select the
intrinsic partial when the full extension result is incompatible. Full calls,
static factories, result/Option semantics and ownership rules stay unchanged.

Polymorphic List `map`/`fold` empty prefixes, extension empty prefixes, bare
method values, mutable builders, services/interfaces and interop are separate
work. No new syntax or public API is introduced; #129 remains open. Immediate
grouped or piped use in an inferred-return function may still need an explicit
return type under existing conservative dependency analysis.

## Steps and affected files

1. Extend intrinsic member selection and type checking in
   `src/DSharp.Compiler/` for these empty positional prefixes; preserve
   zero-arity methods and real-member precedence.
2. Lower a captured receiver to a function through safe closure machinery,
   respecting generic/nullability, async, early return, capability checks and
   further ordinary partial/pipe calls.
3. Add runnable D# example and feature tests; update implementation status and
   this plan. Preserve all previous assertions and trusted verification.

## Acceptance-to-test map

- Execute every scoped method, repeated and grouped invocation, Map.set's
  multi-argument function, value pipes, Option/null and immutable updates.
- Verify receiver evaluated once, deferred work and callback order, faults,
  async suspension/early exits and zero-argument full-call behavior.
- Verify matching real-member priority, direct imports, generic data and type
  inference. Reject wrong/excess arguments, function discards, builder,
  resource and capability capture, including generic substitution.
- Run existing compiler/features and build generated C# with warnings as errors.

## Verification

- `bash .github/ai/bootstrap.sh`: passed.
- `bash scripts/verify-compiler.sh`: passed (18 compiler tests and 377 feature
  tests, including the aggregated empty-collection-partial suite).
- `bash .github/ai/verify.sh`: documentation checks and all 25 automation
  tests passed. The isolated Docker stage could not download the pinned SDK
  image locally because the registry returned `Forbidden`; GitHub CI runs it.
- Focused generated-C# execution passed with warnings as errors for all
  scoped methods, nullable/generic values, async, lookup and capture safety.
- `examples/collection_empty_partials.ds`: checked, compiled without warnings
  and executed; `Demo.answer()` returned `42`.

Fix any CI failures on this PR branch.
