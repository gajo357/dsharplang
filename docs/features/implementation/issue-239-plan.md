# Issue #239 — actor runtime specification reconciliation

Issue: https://github.com/gajo357/dsharplang/issues/239  
Base: `eba3b22ca23b9bff418e2d4565e9aadb2ae7c508` (`origin/main`)

## Scope and dependencies

Import the 21 November handoff as the canonical actor direction and reconcile the active language, syntax, architecture, implementation instructions, examples, sequence and task queue. #101 already maps all 21 verification checks to owners. This is documentation only: #13 and #14 subsequently develop reviewable dispatcher/fault and durable-store contracts, and #51 and #54 define declaration and registration spelling. Existing executable service examples and fixtures remain executable until #240 migrates them; classify rather than falsely claim actor support. Preserve interface defaults, functional pipes, unrelated triggers and logging. No new runtime, syntax or compatibility promise is implemented.

## Steps and affected interfaces

1. Import the complete handoff under `docs/features/` and explicitly identify illustrative syntax and precedence over conflicting historical actor/service design.
2. Inventory active documents and examples containing superseded worker, queue-agent, dispatch and service claims. Reconcile them to the handoff while retaining historical decisions with clear labels and identifying currently executable service behavior.
3. Update the sequence and task queue to require durable acceptance before a public completion milestone and to link #239, #13/#14, cutover #240, dispatcher #241, selection #242, diagnostic chain #243 and resource children #244–#247. Link the check ownership table in #101.
4. Review links, classifications, ordering, and cross-document consistency; run the prescribed checks and record their actual outcomes.

## Acceptance evidence map

| Criterion | Evidence |
| --- | --- |
| Import and reconcile active specifications, examples and precedence | Imported handoff; targeted cross-document search and example classification. |
| Replace superseded actor model; preserve unrelated features; remove pre-durability milestone | Review of active docs and sequence against handoff sections 2–3 and #101 order. |
| Distinguish executable features and classify services | Status and example classifications cross-checked against source and #240. |
| Record semantics and bounded spelling/fault decisions; map 21 checks | Handoff, subsequent #13/#14/#51/#54 gates, and #101's numbered ownership table. |
| Verify | `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`, documentation/link validation, `git diff --check`. Documentation checks do not establish runtime behavior. |

## Unresolved decisions

Exact declaration and registration grammar and runtime fault surface await the bounded reviewed #51/#54 and #13 contracts. Do not settle these here. Record any further blocker discovered during reconciliation.

## Results

- Imported the source handoff and documented that the issue excludes legacy aliases; reconciled active actor/dispatch descriptions and historical precedence, including #101 delivery order and the existing 21-check owner map.
- Classified five service-bearing executable examples and retained them for #240. No actor/runtime implementation is claimed.
- `PATH=/tmp/dsharp-dotnet:$PATH npm_config_cache=/tmp/dsharp-npm-cache bash .github/ai/bootstrap.sh`: passed with pinned SDK 11.0.100.
- `PATH=/tmp/dsharp-dotnet:$PATH npm_config_cache=/tmp/dsharp-npm-cache DOTNET_CLI_HOME=/tmp/dsharp-dotnet-home NUGET_PACKAGES=/tmp/dsharp-nuget XDG_RUNTIME_DIR=/tmp bash scripts/verify-compiler.sh`: passed locked restore/build, 18 baseline and 378 feature tests, zero skips.
- Same environment, `bash .github/ai/verify.sh`: documentation and 25 automation tests passed; isolated compiler stage could not pull its pinned Docker image (registry layer download returned Forbidden). CI must provide that result. An initial run lacked XDG_RUNTIME_DIR and failed against the read-only home; rerunning with the writable runtime directory passed the affected packaged-resource test.
- Documentation, link and whitespace checks were rerun after final edits; see PR evidence.
- Follow-up review corrected the remaining active CallError instruction and obsolete worker/queue adapter briefs, restored HTTP preprocessing requirements, clarified interface defaults, and repaired the phase/dependency table. The #101 order remains authoritative for cutover tasks.
- Follow-up bootstrap passed. Full verifier again passed documentation (108 files, 312 links) and all 25 automation tests; its isolated compiler stage again failed to download the pinned Docker image (Forbidden). At the original head `ea15b9e`, the GitHub Compiler job ran all 18 baseline and 378 feature tests without failures or skips but failed final TRX validation with `Oversized report`; the retry passed. See the PR for current-head CI results.
- Follow-up `scripts/verify-compiler.sh`, with the same environment above, passed locked restore/build and all 18 baseline plus 378 feature tests, without skips.
