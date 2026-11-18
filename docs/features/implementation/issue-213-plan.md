# Issue 213: Multiline parenthesized binding headers

**Issue:** [#213](https://github.com/gajo357/dsharplang/issues/213)
**Base:** `9147845` (merged PR #212)
**Dependencies:** Adopted [DEC-02](DEC-02.md), tuple destructuring #36 and comments #55 are on main.

## Scope and steps

Implement DEC-02's existing newline rule for local `let` headers: newline transitions are permitted inside matching parentheses in tuple binding patterns and tuple or function type annotations. Validate header token transitions without widening parsing of line breaks outside parentheses. Keep initializer continuation and statement boundaries tied to the end of the header rather than the `let` keyword's line. Preserve lexical scopes, type checks, diagnostics and evaluation order. Do not change other declaration forms, syntax decisions, trusted verification or existing tests.

1. Adjust `src/DSharp.Compiler/Compiler.cs` to enforce the existing newline boundary based on token spans and parenthesis nesting.
2. Add executable D# tests in `tests/features/MultilineBindingHeaderTests.cs` and `examples/multiline_binding_headers.ds`; document precisely tested behavior in `docs/features/DSharp_Implementation_Status.md`.
3. Verify nested destructuring, tuple and function type annotations, evaluation once, comments and CRLF; reject line breaks outside matching parentheses and preserve initializer/statement boundaries and diagnostic locations. Include compatible existing single-line programs.

## Acceptance and verification

- Generated C# executes multiline patterns and annotations correctly, with initializer side effects observed once; D# tests cover nested tuples, nested function types, whitespace and comments.
- Reject a break after `let`, before `:`, before `=`, and immediately after a completed closing parenthesis; check source spans and comment-preserved line boundaries. Test initializer continuation and malformed patterns as well as successful cases.
- Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh` and `bash .github/ai/verify.sh` with the pinned .NET 11 SDK and writable temporary caches. Run the example through the D# CLI. Record actual results and environment limitations. Open a single draft PR and fix its CI directly on the same branch.

No unresolved syntax decision: the maintainer adopted the DEC-02 rule. This task does not extend the rule to unrelated headers.

## Results

- Bootstrap passed with the pinned SDK and a writable npm cache.
- `scripts/verify-compiler.sh` passed: 18 compiler tests and 379 feature tests, with zero build warnings or errors. The focused test executed generated C# with warnings as errors under LF, CRLF and CR line endings, including single-evaluation and diagnostic-span checks.
- The D# CLI accepted `examples/multiline_binding_headers.ds`.
- `.github/ai/verify.sh` passed documentation and all 25 automation tests. Its isolated compiler stage could not run locally because downloading the pinned .NET SDK Docker image returned `Forbidden`; GitHub CI must provide that stage's result.
