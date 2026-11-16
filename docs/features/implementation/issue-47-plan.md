# Issue 47: Statically resolved extensions

**Base:** `d57b4f4` (merged #175).
**Issue:** [#47](https://github.com/gajo357/dsharplang/issues/47).
**Dependencies:** [#43](https://github.com/gajo357/dsharplang/issues/43) and
[#46](https://github.com/gajo357/dsharplang/issues/46) are merged. The maintainer
adopted the grammar and lookup rules in [DEC-02](DEC-02.md).
**Status:** Implemented; ready for review.

## Scope

Implement `extend Target { fn name(self, arg: Type): ResultType { ... } }`
using DEC-02's contextual `self`, exact target/argument applicability, explicit
result type, static resolution and public declarations. A matching real member
wins; if a real member of the same name is inapplicable, consider visible
extensions. Collect extensions from the current module and direct imports, dedupe
the same declaration, and reject multiple applicable candidates with useful
origin diagnostics independent of declaration/import order. Transitive imports
are not visible. Extensions add no fields, state, inheritance or runtime registry.

The initial adopted contract has no generic applicability or conversion ranking,
qualified-call syntax, overload resolution beyond its exact candidate rules, or
extension method values/partial application (#129). Preserve resource, builder,
capability, immutable-data, generic and async safety; reject unsupported forms
rather than silently introducing a new language or lifetime rule.

## Implementation and affected files

1. Parse extension declarations and validate the receiver, parameters, result,
   body and target; retain spans and documentation. Integrate type/name binding
   and static call resolution, including real-member precedence.
2. Resolve current-module and direct-import extensions in multi-file programs;
   preserve ambiguity diagnostics, exact origin and source semantics. Lower
   checked calls to ordinary generated C# functions with receiver evaluated once
   before arguments and no runtime discovery.
3. Add runnable D# examples and new tests/features coverage; update status and
   this plan. Reuse the resource feature test aggregation to respect the existing
   bounded CI report while preserving all established assertions.

## Acceptance-to-test map

- AC1: valid/invalid `self`, typed arguments/results, ordinary and async
  extensions, receiver evaluation, exact target and argument type matching.
- AC2: matching real member wins; inapplicable real member falls back; exact
  unique candidates, duplicate imports, no transitive visibility, conflicting
  local/import candidates and import-order-independent origin diagnostics.
- AC3: execute cross-file/imported extension calls; reject fields, state,
  inheritance and unsafe values. Compile generated C# with warnings as errors.
- Regression/safety: generics/nullability, built-in collections, async,
  owned-resource and capability/closure/partial-application boundaries.

## Verification

- `bash .github/ai/bootstrap.sh`: passed.
- `bash scripts/verify-compiler.sh`: passed (18 compiler tests and 377 feature tests).
- `bash .github/ai/verify.sh`: documentation checks and all 25 automation tests
  passed; the offline Docker stage could not run locally because downloading the
  pinned SDK image returned `Forbidden`. CI provides the isolated verification.
- The focused extension acceptance suite compiled generated C# with warnings as
  errors and passed, including import, resolution and lifetime boundary cases.
- `examples/extensions.ds`: checked, compiled with zero warnings and executed;
  `Demo.answer()` returned `42`.

Fix any CI failures on this same PR branch.
