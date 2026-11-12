# Implementation plan: #11 — extended primitives and standard values

Issue: https://github.com/gajo357/dsharplang/issues/11
Base: `81f71f7` on main, including merged Result propagation #123.

## Scope and approach

Write `DEC-01.md` as a bounded, explicitly unadopted proposal. The issue limits
the decision itself to that file; this separate plan is required by the
repository workflow, following the precedent of issue #12. No compiler,
dependency, baseline, workflow, policy, trusted verifier or other implementation
change is in scope. Maintainer adoption of the reviewed contract is a separate
gate before dependent implementation; a merged proposal or green checks do not
adopt it.

1. Inventory existing implemented and settled behavior versus illustrative and
   provisional syntax and APIs, including the adopted DEC-02 grammar, idempotent
   nullable types and explicit Option semantics.
2. Propose exact numeric, standard-value and immutable-collection semantics,
   with diagnostic examples, edge cases, explicit boundaries and practical
   implementation slices. Identify choices that need maintainer review rather
   than presenting them as already adopted.
3. Cross-check coherence, existing compiler behavior, issue dependencies and
   in-scope examples; inspect the proposal independently for unsafe ambiguities.
4. Run repository checks and record actual results; open one draft PR for review.

## Acceptance evidence

- AC1: integer types/ranges, literal grammar/suffixes/separators, conversions,
  checked arithmetic and division fixtures; preserve the implemented int slice.
- AC2: binary32/binary64 names and decimal rules, NaN, signed zero and equality,
  format/parse behavior and cultures with boundary fixtures.
- AC3: concrete representations, ranges, minimum APIs and value-vs-wire
  distinctions for uuid, Instant, LocalDate, LocalTime, Duration and Bytes.
- AC4: valid and invalid fixtures for each contract with first-slice vs deferred
  APIs and explicit distinctions from today's compiler.
- AC5: immutable List/Map/Set construction, lookup behavior, duplicate policy,
  equality/hash eligibility and deterministic formatting; explain nullable and
  Option distinctions and avoid promising wire formats.

## Verification

Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh` and
`AI_REPORT_DIR=/workspace/work/issue-11-verification bash .github/ai/verify.sh`
with workspace-local SDK/cache settings; run `node .github/ai/check-docs.mjs .`
and `git diff --check`. Record actual results and CI; a documentation pass is
not adoption or evidence of runtime implementation. Fix any failures on the
same PR branch. No dependent implementation starts in this PR.

## Progress

- Added the proposed, explicitly unadopted DEC-01 record. It maps AC1–AC5 to
  concrete rules and valid/invalid fixtures, separates first-slice APIs from
  deferred work, and calls out review choices and dependency gates.
- Independent numeric, standard-value and collection reviews informed precise
  parse/format rules, finite/subnormal behavior, constructor errors, immutable
  lookups returning Option, key equality/hash eligibility and display ordering.
- `bash .github/ai/bootstrap.sh` passed. `bash scripts/verify-compiler.sh`
  passed 18 baseline and 353 feature tests. Documentation and diff checks
  passed. These are regression checks, not proof that the proposed APIs exist.
- `bash .github/ai/verify.sh` passed documentation and all 20 automation tests;
  its isolated compiler phase could not download the pinned Docker image locally
  (`Forbidden`). GitHub Actions runs the unchanged isolated verification.
  The maintainer must explicitly adopt the reviewed proposal before dependent
  implementation; approval is not inferred from a successful PR.
