# Issue 179: Partial application of immutable extension receivers

**Base:** `68fa2df` (merged #178).
**Issue:** [#179](https://github.com/gajo357/dsharplang/issues/179), a bounded
child of [#129](https://github.com/gajo357/dsharplang/issues/129).
**Dependencies:** ordinary partial application #135, static extensions #47,
modules #46 and capability rules #43 are merged. The callable target is the
[partial application specification](../DSharp_Pipes_and_Logging.md#3-automatic-partial-application).
**Status:** Implemented; ready for review.

## Scope and decisions

Allow a unique visible extension on an immutable-data receiver to bind one or
more supplied positional arguments when some remain. The resulting function
captures receiver and prefix once in order and invokes the extension on each
saturated call; async results remain tasks and start only when invoked. Use the
existing exact extension lookup and ordinary partial-function representation;
preserve real-member precedence, direct imports, ambiguity diagnostics and
all immutable-data, resource, builder and capability rules. Do not bind service,
interface, scoped-resource or builder receivers or capture prohibited arguments.

The existing tests reject empty-prefix extension calls and bare method values;
those forms, generic extension declarations, service/interface/default methods,
foreign interop and remaining #129 behavior are separate work. This slice does
not close #129. No public API or language rule beyond the existing target is
assumed. Inferred-return functions piping into an extension partial whose name
overlaps a built-in member may still require an explicit return type; the existing
dependency analysis remains conservative.

## Steps and files

1. Extend extension candidate applicability and call binding in
   `src/DSharp.Compiler/` for nonempty positional prefixes and contextual
   remaining function types. Retain source locations and ordering.
2. Lower to the existing checked partial-call mechanism, with receiver first,
   a uniquely selected target and lifetime-safe capture. Preserve all existing
   accepted and rejected cases.
3. Add executable D# feature coverage and a runnable `examples/` program; update
   implementation status and this plan. Keep existing assertions and trusted
   verification unchanged.

## Acceptance-to-test map

- Capture and invocation: receiver and prefix evaluate once, in source order;
  target remains deferred through grouped calls, pipes, repeat calls and async.
- Resolution: current-module/direct imports, duplicates and nontransitive
  visibility, typed prefix/expected result, real-member precedence, ambiguous
  overloads, invalid types and excessive arguments.
- Safety: reject captured capabilities/resources/builders and unsafe data;
  support allowed nullable/generic immutable values and early return/fault paths.
- Regression: full existing compiler/feature suites including old empty-prefix
  and method-value rejections; emitted C# builds with warnings as errors.

## Verification

- `bash .github/ai/bootstrap.sh`: passed.
- `bash scripts/verify-compiler.sh`: passed (18 compiler tests and 377 feature
  tests, including the aggregated extension-partial suite).
- `bash .github/ai/verify.sh`: documentation checks and all 25 automation
  tests passed. The isolated Docker stage could not download the pinned SDK
  image locally because the registry returned `Forbidden`; GitHub CI runs it.
- Focused execution tests passed with generated C# warnings as errors, covering
  order, async, imports, resolution, pipes, nullable data and lifetime safety.
- `examples/extension_partials.ds`: checked, compiled without warnings and
  executed; `Demo.answer()` returned `42`.

Fix any CI failures on this PR branch.
