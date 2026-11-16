# Issue 50: String interpolation

**Base:** `097be6a` (merged #177).
**Issue:** [#50](https://github.com/gajo357/dsharplang/issues/50).
**Dependencies:** [#23](https://github.com/gajo357/dsharplang/issues/23) and adopted [DEC-01](DEC-01.md).
**Status:** Scalar interpolation slice ready for review; #50 remains open.

## Scope and decisions

Implement the documented `$"text {expression}"` form, ordinary string escapes,
and `{{`/`}}` for literal braces. Interpolated expressions are checked D#
expressions, evaluated once from left to right. Format supported values
invariantly using their adopted semantics; insert a string as text as shown by
the syntax reference's `$"Hello, {name}"`. Reject unsupported types rather than
calling arbitrary foreign `ToString`. No raw strings, alignment or formatting
mini-language. The grammar decision DEC-02 explicitly defers interpolation and
DEC-01 specifies diagnostic display separately; where the desired interpolation
format or type surface is not established, keep that behavior out of scope and
leave #50 open rather than claiming unsupported cases complete.

## Steps and affected files

1. Extend lexer/parser and syntax nodes in `src/DSharp.Compiler/` for text,
   escaped braces and nested expressions with accurate D# source spans.
2. Bind expression segments and lower to readable C#, preserving evaluation
   order, exception/return/async behavior and existing lifetime safety. Implement
   only safely defined formatting types without foreign dispatch.
3. Add execution/diagnostic coverage under `tests/features/` and a runnable D#
   example. Update implementation status and this plan with exact support and
   results. Preserve existing assertions and trusted verification.

## Acceptance-to-test map

- AC1: interpolation, ordinary escapes, literal braces, nested expressions and
  checked interpolated types; reject malformed braces, escapes and expressions.
- AC2: callback and fault evidence for left-to-right, exactly-once evaluation;
  invariant formatting across cultures for every supported value type.
- AC3: Unicode, quotes, expression nesting and accurate diagnostics, including
  multiple source files; reject unsupported format syntax and unsafe values.
- Regression: existing ordinary strings, comments, generics, async and resources.

## Verification

- `bash .github/ai/bootstrap.sh`: passed.
- `bash scripts/verify-compiler.sh`: passed (18 compiler tests and 377 feature
  tests, including the aggregated interpolation suite).
- `bash .github/ai/verify.sh`: documentation checks and all 25 automation
  tests passed. The isolated Docker stage could not download the pinned SDK
  image locally because the registry returned `Forbidden`; GitHub CI runs it.
- The interpolation suite compiled generated C# with warnings as errors and
  executed scalar formatting, order, early exit, async, imports and diagnostics.
- `examples/interpolation.ds`: checked and compiled without warnings;
  `Demo.answer()` returned `Hello, Ada! You have 42 items.`

Fix any CI failure on the same PR branch.
