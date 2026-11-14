# Issue 121: Option values in records and generic calls

**Base:** `32e2047a4b1cdc5f6feb57ed6591c67ecba81e7c`.

## Scope and dependencies

Complete the remaining supported acceptance evidence for the existing explicit
`Option<T>` implementation. Generic data, nullable payloads and exhaustive
matching already work in the supported compiler subset. Collections, aliases
and serialization are future features and remain separately gated; this issue
does not adopt a wire encoding or close nullable-generic issue #30.

## Steps and affected interfaces

Add an executable D# example and feature tests for `Option<T>` in record fields,
nested data and generic functions. Cover absence, nullable payloads, nested
options, structural equality and negative source diagnostics. Repair only
compiler defects revealed by these cases, retaining existing APIs, tests and
verifier configuration. Record precise behavior and test outcomes in the
implementation status and this plan.

## Acceptance and verification

Map the issue's constructors, exhaustive matching, non-null rejection, nested
cases, no implicit conversions, generic/record storage, lookup, diagnostics and
equality criteria to the existing acceptance suite and the new executed D#
example. Compile and execute generated C#, then run `bash .github/ai/bootstrap.sh`,
`bash scripts/verify-compiler.sh`, and `bash .github/ai/verify.sh`. Record any
local limitations and verify GitHub Actions on the same PR.

## Acceptance mapping

- Construction, qualified lookup, exhaustive matching, null restrictions,
  nested cases, equality and conversion diagnostics are exercised by
  `OptionAcceptanceTests.cs` and the new record acceptance tests.
- The new D# example and generated-code tests exercise update fields, generic
  functions, generic records, nullable value/reference payloads and nested
  options; tests also reject a non-nullable `Some(null)`, an incomplete field
  match and direct payload-field extraction.
- Collection and serialization behavior is explicitly deferred by #121; #30
  remains open for its separate nullable-generic acceptance criteria.

## Results

The existing compiler passed the new cases without code changes. Bootstrap,
the CLI `check` and `compile` commands on `examples/option_records.ds`, and
execution of its generated `answer()` passed. Compiler verification passed
18 baseline and 366 feature tests. Documentation and 23 automation tests passed.
The local isolated verifier could not download the pinned Docker image
(`Forbidden`); GitHub Actions runs the unchanged isolated verifier.
