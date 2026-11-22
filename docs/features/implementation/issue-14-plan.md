# Issue #14 — durable actor acceptance and recovery contract

Issue: https://github.com/gajo357/dsharplang/issues/14
Base: `a10e9d70bd5239ff100f704f289e10d8f7335f9e` (`origin/main`)

## Scope and dependencies

Replace the superseded DEC-04 queue-agent proposal with a durable actor intake, attempt and publication contract from the merged #239 handoff. The decision takes effect on merge, not as a claim of implementation. #13 was merged and closed by the maintainer; this decision accepts its relevant semantic boundaries under the user's delegated technical authority without claiming that its public fault spelling is adopted. #59–#69 own storage and execution implementation; #78 owns encoding, and #95 owns cross-store business coordination.

The issue confines decision content to `DEC-04.md`; this separate plan is required by `AGENTS.md` and the scheduled workflow. No other product or design files are in scope.

## Steps and affected interfaces

1. Compare the handoff, issue criteria and DEC-03 with the historical queue record; remove superseded agent/worker and business-error retry rules.
2. Specify durable acceptance and deduplication, pinned routing and parent context, fenced attempts and results, policies and cancellation, publication recovery, same-store follow-ups and cross-store limits.
3. Add crash-point and conformance examples and map acceptance to evidence; identify review decisions and #13 alignment without selecting a new public API.
4. Run required verification and record results. Only DEC-04 and this plan change.

## Acceptance-to-evidence map

| #14 criterion | Planned evidence |
| --- | --- |
| 1: targeted durable acceptance and lost acknowledgement | Acceptance transaction, stable identity and crash cases |
| 2: attempts, fencing, business results and pinned selection | State and ownership transitions, outcome table, restart cases |
| 3: policy, ordering, waits, cancellation and retention | Target-wide policy section and controlled race cases |
| 4: frozen publication and recovery | Snapshot/fan-out transaction, independent deliveries and crash cases |
| 5: atomic follow-ups, capabilities and cross-store coordination | Atomicity section and real-provider conformance matrix |
| Verification | `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`, `git diff --check`; documentation checks do not establish runtime durability |

## Unresolved decisions

Exact public fault spelling, provider-specific encoding and clock implementation remain with their owners. Providers must validate a configured finite deduplication horizon and demonstrate the decided clock and cancellation invariants; no default duration or unsupported guarantee is implied.

## Results

- Replaced the historical queue-agent proposal with durable actor intake, fenced attempts, target-wide policies, recoverable publication and transaction boundaries. Cross-checked all five criteria against the handoff and DEC-03 semantics. Independent review corrected expiry recovery fencing and added explicit lost-ack deduplication for publication. Under the user's delegated technical approval, the decision is effective on merge; it resolves cancellation races and configured deduplication/clock boundaries without selecting DEC-03's public fault spelling.
- With `PATH=/tmp/dsharp-dotnet:$PATH`, `npm_config_cache=/tmp/dsharp-npm-cache`, `DOTNET_CLI_HOME=/tmp/dsharp-dotnet-home`, `NUGET_PACKAGES=/tmp/dsharp-nuget` and `XDG_RUNTIME_DIR=/tmp`, bootstrap passed. The full verifier passed documentation and all 25 automation tests but its isolated compiler stage could not pull pinned SDK image layers (`Forbidden`).
- With the same environment, `bash scripts/verify-compiler.sh` passed locked restore/build and all 18 baseline and 378 feature tests without skips. After the final review edits, `node .github/ai/check-docs.mjs .` passed (110 files, 305 local links), and `git diff --check` passed. These checks do not establish runtime durability.
- After the decision refinements, the full verifier again passed documentation and all 25 automation tests but could not pull the pinned SDK layers (`Forbidden`). The compiler command above ran on the preceding documentation-only head with identical compiler and tests; exact-head GitHub checks provide the isolated verification.
