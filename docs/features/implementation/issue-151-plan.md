# Issue 151: .NET 11 and C# 15 baseline

**Base:** `f22efb53fdedbb17339d798d61c9d25577497cd8`.

## Scope and steps

Update the pinned SDK, target framework, generated project templates, test
fixtures, package locks, CI and isolated verification image together. Preserve
all existing assertions and change only version-specific fixture values. Update
active instructions and documentation, then assess C# 15 union lowering against
existing D# semantics without changing it unless equivalence is established.

## Dependencies and affected interfaces

Requires released .NET 11 SDK, compatible test dependencies and an immutable
official SDK image digest. Affects the CLI's generated projects, the compiler
test harness, pinned SDK selection and trusted verification scripts. Existing D#
source semantics and generated-code behavior remain compatibility requirements.

## Acceptance and verification

- Bootstrap, generated programs and all existing compiler/feature tests run on
  the pinned SDK and `net11.0`; inspect package locks and generated artifacts.
- Automation tests and the isolated verifier remain enabled and enforce the same
  checks, now with an immutable .NET 11 SDK image.
- Documentation and active contributor instructions state the actual baseline.
- Compare native C# unions with named D# variants, duplicate payload types,
  nullable payloads, nested options, equality and pattern matching.

Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`, and
`bash .github/ai/verify.sh`; record actual results and GitHub Actions outcomes.
No new package or D# language semantics are implied by the SDK change.

## Results

SDK 11.0.100, `net11.0`, regenerated lockfiles and the official SDK image
digest are pinned; dependency versions and hashes are unchanged. Bootstrap and
compiler verification passed with 18 baseline and 362 feature tests. The full
verifier passed documentation checks and 24 automation tests, but its local
isolated compiler stage could not download the pinned image (`Forbidden`);
GitHub Actions must exercise that unchanged isolation boundary.

An independent C# 15 execution probe confirmed that distinct case wrappers can
preserve `None`, `Some(null)`, nested options and same-payload result cases. A
direct union of identical generic payload types is ambiguous; a native union
also has an extra default/null state, and its default equality differs from
D# structural equality for generated objects. Keep the existing tagged lowering
until those requirements are addressed and verified; track the evaluation in
[#152](https://github.com/gajo357/dsharplang/issues/152).
