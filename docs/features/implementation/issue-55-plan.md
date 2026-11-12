# Implementation plan: #55 — Block and documentation comments

Issue: https://github.com/gajo357/dsharplang/issues/55
Base: `2589304` on main; DEC-02 was merged in #112 and explicitly adopted by the
user on 12 November 2026. Record that adoption in the decision file in this PR.

## Scope and evidence

Implement non-nested block comments with opening-span errors for nested/unterminated
comments; preserve UTF-16 offsets and line/column through comments and literals.
Attach contiguous `///` groups immediately before eligible module/function
declarations; blank lines, ordinary comments, misplaced/trailing documentation and
EOF cause an unattached-doc diagnostic. Keep comment text/spans in syntax metadata;
comments do not change generated execution or lexical scopes. Existing module and
function constructors and ordinary `//` token behavior remain compatible.

Honor adopted DEC-02 newline continuation in delimiters/after operators where
this comment slice touches multiline expressions; do not introduce unrelated
language features. Comment markers inside string/char literals remain text.

- AC1: execute block-comment fixtures; assert opening spans for unterminated and
  nested comments and newline/CRLF locations.
- AC2: inspect module/function syntax metadata and doc spans; reject unattached
  and misplaced docs; run equivalent code with and without docs.
- AC3: assert exact source locations after comments and multiline expressions,
  and execute strings containing comment-like text.

Add compiler code, new feature tests, example, status, this plan and the explicit
adoption record. Preserve existing tests, dependencies, workflows and verifiers;
adapt an obsolete feature fixture only if adopted syntax makes it valid. Run
bootstrap, compiler script, full verifier, CLI example and GitHub Actions. Open
one PR; fix any CI failures on that branch. No docs-site, formatter or editor work.

## Progress

Implemented block/doc comments, their source spans and syntax metadata, and
multiline expression continuation within the currently supported grammar. The
adopted contract covers more than this slice: declaration-header newline handling
and unsupported syntax such as member access need later implementation. Two
existing feature fixtures were updated because the adopted continuation rules
make their old inputs valid.

Bootstrap and compiler verification passed: 18 baseline and 148 feature tests.
The CLI checked and compiled the comments example. The full verifier passed the
documentation and 20 automation checks, but its containerized compiler stage
could not run locally because the pinned image download returned `Forbidden`.
GitHub Actions provides the remaining containerized verification.

## PR

Pending user review and merge.
