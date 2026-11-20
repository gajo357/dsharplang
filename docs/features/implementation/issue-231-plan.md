# Issue 231: Multiline declaration boundaries

**Issue:** [#231](https://github.com/gajo357/dsharplang/issues/231).
**Base:** `3ef1d83` (merged PR #230).
**Dependencies:** Adopted [DEC-02](DEC-02.md), record and interface syntax, comments #55, and multiline local-binding headers #213.

## Scope and approach

Apply the adopted newline boundary after completed multiline record field types and bodyless interface method signatures. A following declaration must begin on a later line than the preceding completed type or signature; newlines inside matching parentheses or other permitted type delimiters remain valid. Preserve diagnostics, comments and existing syntax elsewhere. No new syntax or public API is selected.

1. Correct the field and interface signature boundary checks in `src/DSharp.Compiler/Compiler.cs`, using the final consumed token instead of the opening token of a multiline type.
2. Add executable D# examples and feature tests for multiline tuple, function and generic types, line endings, comments and declaration boundary errors with source locations. Update implementation status.
3. Verify valid multi-line declarations execute correctly, and adjacent declarations on the same closing line are rejected. Cover LF, CRLF and CR, comments with and without line breaks, and existing single-line compatibility. Preserve all prior tests and trusted verification.

## Verification

Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`, `bash .github/ai/verify.sh`, and the D# CLI example check on pinned .NET 11. Record actual results and limitations. Open a single draft PR and fix CI failures on its branch.

## Results

- Bootstrap, the 18 core compiler tests and the D# CLI check of `examples/multiline_declarations.ds` passed. An independent set of 36 CLI probes covered multiline and nullable types, comments, closing braces and line endings.
- The initial feature run passed 377 registrations but the new test failed when reflection did not search inherited generated module methods. After correcting the lookup, the complete new test passed in a focused harness for LF, CRLF and CR; the full compiler suite was rerun.
- `.github/ai/verify.sh` passed documentation and all 25 automation tests. The isolated compiler stage could not run locally because the pinned Docker image download returned `Forbidden`; GitHub CI supplies that verification.
