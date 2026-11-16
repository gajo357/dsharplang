# Issue 181: Partial application of immutable Map.set

**Base:** `7928224` (merged #180).
**Issue:** [#181](https://github.com/gajo357/dsharplang/issues/181), a bounded
child of [#129](https://github.com/gajo357/dsharplang/issues/129).
**Dependencies:** immutable maps #38, ordinary partial application #135 and
extension-prefix integration #179 are merged; DEC-01 and the positional-prefix
[composition contract](../DSharp_Pipes_and_Logging.md#3-automatic-partial-application)
are adopted.
**Status:** Implemented; ready for review.

## Scope and decisions

For an immutable `Map<K,V>`, `map.set(key)` produces a `(V) -> Map<K,V>`.
Evaluate the receiver and key exactly once, in order, when the function is
formed; each completed call applies the existing immutable map update to those
captured values. Preserve the original map, applicable real-member precedence,
full `set(key,value)` behavior, static typing and all capability, builder and
resource non-escape checks. No new public API or syntax is introduced.

Other collection methods, empty prefixes, method values, service/interface
methods, foreign interop and remaining #129 integrations remain separate work.
Do not close #129 from this slice. Inferred-return analysis retains its existing
conservative rules. A function immediately invoking or piping into this partial
may require an explicit return annotation; unconstrained generic map keys remain
unsupported under the existing key-eligibility policy.

## Steps and affected files

1. Extend `Map.set` call binding and type checking in `src/DSharp.Compiler/`
   for exactly one positional argument and return the correct function type.
2. Lower the partial with existing safe closure infrastructure or a small
   dedicated bound form, capturing receiver/key in source order. Preserve async,
   early return, faults, generic/nullability and capability restrictions.
3. Add runnable D# example and feature tests, update implementation status and
   this plan; retain all old assertions and trusted verification.

## Acceptance-to-test map

- Capture/deferral: receiver then key exactly once, independent maps and
  original unchanged, repeated invocation, delayed fault/side-effect behavior.
- Types/composition: grouped and pipe calls, typed/inferred and generic
  functions, nullable and Option keys/values, modules and async/early exits.
- Safety/lookup: wrong types, extra args, discarded function, builders,
  resources/capabilities and same-name extension precedence; full calls unchanged.
- Regression: existing compiler/features, generated C# warnings as errors.

## Verification

- `bash .github/ai/bootstrap.sh`: passed.
- `bash scripts/verify-compiler.sh`: passed (18 compiler tests and 377 feature
  tests, including the aggregated Map partial suite).
- `bash .github/ai/verify.sh`: documentation checks and all 25 automation
  tests passed. The isolated Docker stage could not download the pinned SDK
  image locally because the registry returned `Forbidden`; GitHub CI runs it.
- Focused generated-C# execution passed with warnings as errors, covering
  order, immutable updates, nullable/generic values, async and lifetime safety.
- `examples/map_partial.ds`: checked, compiled without warnings and executed;
  `Demo.answer()` returned `true`.

Fix any CI failures on this PR branch.
