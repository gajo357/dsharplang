# Implementation plan: #104 — Isolate compiler verifier pilots

Issue: https://github.com/gajo357/dsharplang/issues/104
Base: `21769d9` on main. The failure was reported for feature PR #103.

## Scope and expected behavior

The trusted verifier stages existing feature tests before overlaying candidate
files. The pilot currently supplies only PilotTests.cs, so existing feature tests
survive even after that fixture is emptied. The zero-test scenario therefore
passes on a checkout containing real feature tests.

This separately scoped automation repair masks each existing feature C# file with
an empty file in the disposable candidate. Actual checkout tests and the trusted
verifier remain unchanged. The success fixture then has only its own feature test;
zero-tests has none while trusted compiler acceptance tests still run.

## Steps and files

1. Write this plan before changes.
2. Update `.github/ai/pilot.mjs` to shadow existing feature files before creating
   the pilot fixture, including nested paths.
3. Add `.github/ai/tests/pilot.test.mjs` exercising the real pilot driver against
   a deterministic verifier double that models trusted-plus-candidate overlays.
   Include pre-existing root/nested feature tests and verify source preservation.
4. Run JavaScript tests, doc/whitespace checks and real pilots if image access
   permits. Open a separate draft PR; do not merge or alter #7 feature scope.

## Acceptance evidence

- AC1: pilot overlays all existing feature C# paths in the temporary candidate.
- AC2: regression asserts success has exactly one feature fixture, zero-tests
  fails with zero features, and original trusted files remain unchanged.
- AC3: fixture contains existing root and nested tests. Run the regression against
  old driver behavior to prove it detects the reported failure.

## Decisions and verification

No new dependencies, workflow permissions, image pin, existing test or verifier
changes. The mock exercises fixture isolation and is not evidence of real Docker
execution. Real Docker pilots remain required in CI.

## Progress

- Fixed the disposable candidate overlay without changing the trusted verifier.
- The new regression test fails against the original driver with the reported
  zero-tests incorrectly passed assertion, then passes with the fixed driver.
- All 19 JavaScript automation tests pass, no skips.
- Documentation validation passes (15 files, 47 local links); git diff --check passes.
- Real pilot execution attempted with the pinned SDK on PATH. It is blocked in
  the initial success scenario because Docker cannot pull the pinned SDK image:
  download failed after six attempts, Forbidden. Real pilots must finish in CI;
  the deterministic verifier double is not a substitute for that check.

Commands:

```bash
node --test .github/ai/tests/pilot.test.mjs
node --test .github/ai/tests/*.test.mjs
node .github/ai/check-docs.mjs .
git diff --check
# Existing local .NET SDK/cache selectors supplied:
node .github/ai/pilot.mjs /workspace/work/issue104-pilots
```

## PR

Pending. Merge the repair first, then update #103 from main and rerun CI.
