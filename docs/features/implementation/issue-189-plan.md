# Issue 189: Contextual empty-prefix List.map and List.fold

**Base:** `aa8db2b` (merged #188).
**Issue:** [#189](https://github.com/gajo357/dsharplang/issues/189), a bounded
child of [#129](https://github.com/gajo357/dsharplang/issues/129).
**Dependencies:** List #37, ordinary partials #135, List.fold partial #183,
partial inference #185 and monomorphic collection empty prefixes #187 are
merged. DEC-01 and the [partial-application contract](../DSharp_Pipes_and_Logging.md#3-automatic-partial-application)
are adopted.
**Status:** Implemented; ready for review.

## Scope and decisions

Permit `List<T>.map<U>()` and `List<T>.fold<A>()` to return the corresponding
function for their required arguments when `U` or `A` is established by an
explicit type argument or uniquely compatible expected callable/pipe context.
Capture the immutable receiver once; defer iteration and callbacks until fully
supplied. Diagnose absent or conflicting type evidence. Preserve the existing
rejection of uncontextualized `map()` and `fold()`, full calls, real-member and
previously valid zero-argument extension behavior, and all nullable/Option and
capability restrictions.

`map<U>` results must remain immutable data, including under generic
substitution. A later partial capturing the `fold<A>` accumulator must obey the
existing generic capability and resource/builder non-escape checks. Unknown
callbacks/indirect dispatch remain conservative in return inference. Other #129
method families, bare values and extension empty prefixes remain separate work.
An inferred-return function immediately invoking or piping into these partials
may still need a return annotation under the existing conservative analysis.

## Steps and affected files

1. Extend compiler intrinsic resolution and type inference for the two generic
   empty prefixes, retaining overload/extension precedence and diagnostics.
2. Reuse safe empty-collection closure lowering; emit generic method type
   arguments and retain type/capability requirements across staged calls.
3. Add runnable D# example and feature tests; update status and this plan.
   Preserve all existing assertions and trusted verification.

## Acceptance-to-test map

- Execute explicit/contextual map/fold partials, grouped and pipe invocation,
  repeated calls and empty lists; receiver once and callbacks only when saturated.
- Validate generic, nullable/Option and multi-file behavior, inference failure,
  same-name extensions and unchanged fully supplied methods.
- Reject invalid result/accumulator types, builders/resources/capabilities and
  generic/indirect capture escapes; retain dynamic-callback inference safety.
- Run full compiler/features; build generated C# with warnings as errors.

## Verification

- `bash .github/ai/bootstrap.sh`: passed.
- `bash scripts/verify-compiler.sh`: passed (18 compiler tests and 377 feature
  tests, including the aggregated generic List partial suite).
- `bash .github/ai/verify.sh`: documentation checks and all 25 automation
  tests passed. The isolated Docker stage could not download the pinned SDK
  image locally because the registry returned `Forbidden`; GitHub CI runs it.
- Focused generated-C# execution passed with warnings as errors, covering
  type inference, ordering, empty lists, nullable generics and safety.
- `examples/generic_list_empty_partials.ds`: checked, compiled without warnings
  and executed; `Demo.answer()` returned `42`.

Fix any CI failures on this PR branch.
