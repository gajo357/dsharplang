# Issue 48: Integer widths and literals

**Base:** `597c122` (merged #176).
**Issue:** [#48](https://github.com/gajo357/dsharplang/issues/48).
**Dependencies:** [#19](https://github.com/gajo357/dsharplang/issues/19) and adopted [DEC-01](DEC-01.md).
**Status:** Integer implementation slice ready for review; #48 remains open.

## Scope and decisions

Add the adopted eight integer types, contextually typed unsuffixed literals,
`L` suffix, hexadecimal/binary bases and digit separators; check numeric ranges
before unary negation. Same-type integer arithmetic and comparisons are checked
and preserve the operand type. Context does not convert already-typed values;
no implicit mixed-width operations. DEC-01 explicitly preserves rejection of
`-2147483648` and other directly spelled signed-minimum magnitudes; compute the
minimum by checked arithmetic instead. It leaves conversion entry-point names
beyond its examples to a later library slice; do not invent a conversion API.
If that means any acceptance criterion remains incomplete, keep #48 open and
record the remaining work rather than claiming completion.

DEC-02 also reserves numeric type names globally, but the existing immutable
feature suite contains a function named `short`. Until that conflict is resolved,
this slice preserves existing value identifiers and treats the new integer names
as built-ins in type positions; user-defined types and type parameters cannot
shadow them. This is a compatibility deviation, not a change to DEC-02.

## Steps and affected files

1. Extend the compiler lexer/parser, contextual binding, primitive typing and
   C# emission in `src/DSharp.Compiler/`, retaining source diagnostics and old
   int behavior. Preserve existing APIs and resource/generic safety.
2. Add executable D# acceptance tests under `tests/features/` covering the
   specified matrix and a runnable `examples/` program; do not edit existing
   tests or verification infrastructure.
3. Update the implementation status and this plan with exact evidence and any
   deferred conversion work.

## Acceptance-to-test map

- AC1: each width's limits; decimal, hex and binary forms; legal/illegal
  separators and `L`; expected types through arguments, returns and fields.
- AC2: positive/negative boundaries, rejected out-of-range and directly spelled
  minimum magnitudes, computed minimums, checked arithmetic and division faults.
- AC3: reject mixed typed operands and implicit conversions; demonstrate only
  adopted explicitly named conversion operations, if available.
- Regression: existing int semantics, inference, records, nullable and
  collections; run generated C# and verify positive/negative diagnostics.

## Verification

- `bash .github/ai/bootstrap.sh`: passed.
- `bash scripts/verify-compiler.sh`: passed (18 compiler tests, 378 feature tests).
- `bash .github/ai/verify.sh`: documentation checks and 25 automation tests passed;
  the isolated Docker stage was blocked locally when the pinned SDK image download
  returned `Forbidden`. GitHub CI runs the isolated verifier.
- The focused integer acceptance test passed with generated C# warnings as errors.
- `examples/integer_widths.ds`: checked, compiled without warnings and executed;
  `Demo.answer()` returned `42` as a `uint`.

Fix any CI failures on the same PR branch.
