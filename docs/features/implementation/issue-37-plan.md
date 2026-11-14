# Issue 37: Immutable D# lists

**Base:** `bb2da38280ab694e34120d0cd71290aac97dc2c6`.

## Scope and dependencies

Implement the List slice of [DEC-01](DEC-01.md), explicitly adopted by the
maintainer in this session, using the existing generic, lambda, nullable and
Option implementation. Support immutable list literals and `length`, `append`,
`get`, `map`, `filter` and left-to-right `fold`. An absent index returns `None`;
a present nullable null returns `Some(null)`. Do not implement maps, sets,
builders, general instance methods, aliases, async iteration or serialization.

## Steps and affected interfaces

Extend lexer/parser and multi-file resolution for list literals, bind intrinsic
list types, literal inference, typed callbacks and list operations, and emit a
private immutable managed representation with D# structural equality. Preserve
source precedence, diagnostics, once-only left-to-right evaluation and existing
compiler APIs. Keep writable storage private, and reject function values and
other non-data payloads. Add executable D# examples and feature tests; the
compiler's runtime support necessarily uses C# until D# gains iteration and a
bounded list prelude interface. Record the adoption and tested status.

## Acceptance-to-test map

- Literal inference, typed empty lists and `length`/`append`/`get`/`map`/
  `filter`/`fold`: compile and execute D# programs with empty, ordered and
  transformed values, inference and callback type errors.
- Immutability and evaluation: compare original and updated lists, verify
  once-only left-to-right element and callback order, and reject mutation calls
  or escaping function-valued elements.
- Nullable and Option: execute generic records/functions and list operations
  containing nullable value/reference payloads, `None`, `Some(null)` and
  nested options. Verify missing indices remain distinct from present nulls.
- Equality and naming: compare lists and nested data structurally; check normal
  module/name lookup, generic substitution and diagnostics.

Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`, and
`bash .github/ai/verify.sh`; compile and execute the D# example and record actual
results. Preserve the pinned SDK, old tests and trusted verification.

## Results

The compiler now parses and binds list literals and the six adopted operations,
emitting immutable private-backed storage and ordered callbacks. The executable
D# example and feature tests cover records, nested lists, generic nullable
elements, `List<Option<T?>>`, structural equality, diagnostics, callback order
and multi-file references. The CLI checked and compiled the example, and the
generated `answer()` returned true. Bootstrap and compiler verification passed:
18 baseline and 368 feature tests. The new list checks are grouped in one test;
all cases and assertions are retained. The test assembly output path is shorter
to keep CI reports within the existing size limit without changing verification.
Documentation and all 25 automation tests passed. The local isolated verifier could not download its pinned Docker image
(`Forbidden`); GitHub Actions runs the unchanged verifier. Issue #30 remains
open for its complete nullable-generic acceptance criteria.
