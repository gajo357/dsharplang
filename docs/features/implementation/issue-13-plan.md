# Issue #13 — actor invocation and failure contract

Issue: https://github.com/gajo357/dsharplang/issues/13
Base: `c41b11f21d3ee0f66f994de1ea3bcd0ca61698d1` (`origin/main`)

## Scope and dependencies

Replace the superseded callable-worker proposal in DEC-03 with a reviewable actor dispatcher and failure contract from the merged #239 handoff. This is documentation, not an implementation or adoption claim. Coordinate the persistence boundary with open #14; detailed store transitions belong there. #51 and #241 own checked declarations and dispatcher typing, #56 the isolated attempt, #57 public waits, and #243 diagnostic chains. Exact syntax and public fault types remain subject to review.

The issue limits the decision content to `DEC-03.md`; this separate plan is required by the repository's `AGENTS.md` and the scheduled task. No other product or design files are in scope.

## Steps and affected interfaces

1. Compare the handoff with #13 and its dependent issues; retain selected semantics and remove the incompatible worker/queue split and `CallError<E>` proposal.
2. Specify Invoke, targeted Dispatch and publication boundaries; distinguish acceptance, business completion, faults, timeout, cancellation and uncertain acknowledgement without prematurely fixing API spelling or storage mechanics.
3. Describe attempt isolation, pinned routing, target-wide permits, awaited ancestry and detached roots. Add illustrative examples and future evidence requirements.
4. Cross-check acceptance, examples and #14 boundary; run verification and record results below. The plan and DEC-03 are the only affected files.

## Acceptance-to-evidence map

| #13 criterion | Planned evidence |
| --- | --- |
| 1: three operations and exact results | Operation table, examples and type-checking evidence list |
| 2: fresh invocation, resources, scoped dispatcher and common route | Invocation and routing sections |
| 3: failures, timeouts, cancellation, deduplication | Outcome table, lost-ack and timeout examples; #14 boundary |
| 4: waits, permits and ancestry | Wait, re-entry and diagnostic-chain sections and examples |
| 5: examples and supersession | Illustrative D# and expected observations; explicit replacement of older model |
| Verification | `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`, `git diff --check`; document checks do not prove runtime behavior |

## Unresolved decisions

The exact language spelling and public runtime fault surface need bounded review; sketches are illustrative. Store protocols and races require #14. Do not assume a fixed numeric timeout, retry count or capacity. Any further missing public contract will be recorded instead of silently selected.

## Results

- Replaced the historical worker proposal with the actor operation, isolation, acceptance, outcome, wait, permit and ancestry contract. Examples and future evidence map to all five issue criteria; cross-checked the #14 and dependent-issue boundaries. Exact public fault and syntax spelling remains a review decision, not an implemented API.
- With `PATH=/tmp/dsharp-dotnet:$PATH`, `npm_config_cache=/tmp/dsharp-npm-cache`, `DOTNET_CLI_HOME=/tmp/dsharp-dotnet-home`, `NUGET_PACKAGES=/tmp/dsharp-nuget` and `XDG_RUNTIME_DIR=/tmp`, `bash .github/ai/bootstrap.sh` passed. `bash .github/ai/verify.sh` passed documentation and all 25 automation tests but its isolated compiler stage could not pull the pinned SDK image layers (`Forbidden`).
- With the same environment, `bash scripts/verify-compiler.sh` passed locked restore/build, 18 baseline tests and 378 feature tests, without skips. After the final example edits, `node .github/ai/check-docs.mjs .` passed (109 files, 309 local links), and `git diff --check` passed. These checks do not establish runtime behavior.
