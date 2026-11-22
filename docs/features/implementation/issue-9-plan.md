# Issue #9 — bounded runtime project and trusted test infrastructure

Issue: https://github.com/gajo357/dsharplang/issues/9
Base: `9c765ada7165168a6876008b49f7e00599b2dea2` (`origin/main`)

## Scope and prerequisite boundary

Enable a minimal managed runtime assembly and a separate runtime test project; no actor execution, queue persistence, public runtime protocol or provider is implemented. #13 and #14 were merged and closed; DEC-04 accepts the actor outcome distinctions for its scope. The precise public fault spelling remains outside this setup and is not needed to build a test harness. The existing compiler verifier, immutable tests and compiler-only optional coordinator policy remain intact. This setup is not blanket authorization for runtime automation paths.

## Project, dependency and ownership plan

Before editing implementation, freeze these paths and references:

- `src/DSharp.Runtime/DSharp.Runtime.csproj`, its `packages.lock.json`, and minimal assembly marker `RuntimeAssembly.cs`: standalone `net11.0` managed assembly, no package or project references, no public execution API.
- `tests/DSharp.Runtime.Tests/DSharp.Runtime.Tests.csproj`, its `packages.lock.json`, trusted baseline test and deterministic manual clock/scripted random harness files: references only the runtime project and the existing exact managed pins `Microsoft.NET.Test.Sdk` 17.14.1, `xunit` 2.9.3 and `xunit.runner.visualstudio` 3.1.5. `tests/runtime-features/**/*.cs` is the sole candidate runtime-feature location.
- Add those two projects to `DSharp.slnx`; retain SDK 11.0.100, `net11.0`, locked restore and existing compiler projects, tests and dependencies unchanged.
- Add `scripts/verify-runtime.sh` for locked standalone restore/build, baseline and runtime-feature tests with separate nonzero unskipped TRX validation. Extend trusted verification only with a dedicated `.github/ai/verify-runtime.mjs` and narrow `verify.sh` invocation. Host restore and container staging use trusted project manifests and baseline tests, stage candidate runtime source only under `src/DSharp.Runtime/**/*.cs` and candidate test additions under `tests/runtime-features/**/*.cs`; reject unsafe paths, retain offline nonroot constrained execution and validate reports outside candidate code. Existing compiler checks remain unchanged.
- Add only targeted runtime verifier staging/negative regression/pilot support under `.github/ai/` and its new tests, with workflow wiring only if needed. Do not expand the existing compiler coordinator allowlist or change old tests. Candidate automation is exercised by the existing separate candidate-validation CI step; before this PR merges, the old base cannot independently verify a new runtime project, so record this bootstrap limitation honestly.
- Update implementation status for exactly the tested harness and commands, not actor behavior. Runtime feature tasks may add only their approved source and feature files through reviewed Codex web PRs; trusted test and manifest changes require separately scoped authorization.

## Steps and acceptance-to-test map

1. Create projects, locked manifests, deterministic manual time/random harness and baseline/feature tests (AC1, AC4).
2. Add isolated staging and strict runtime TRX checks, preserving compiler checks and immutable trusted baseline (AC2, AC3). Exercise missing/zero/skipped/failed results, unsafe paths, candidate baseline/manifest tampering and the explicit overlay boundaries.
3. Demonstrate a deliberately failing runtime feature test makes the **real isolated runtime verifier** fail, then remove the failure and pass it; record exact commands and evidence. If the container image is unavailable locally, run the proof on the proposed GitHub harness and explicitly report any remaining gap rather than claiming host-only evidence.
4. Run bootstrap, full verifier, compiler and runtime scripts, with exact results and future commands recorded. Keep all work in this branch and one draft PR.

## Verification commands

`bash .github/ai/bootstrap.sh`; `bash .github/ai/verify.sh`; `bash scripts/verify-compiler.sh`; `bash scripts/verify-runtime.sh`; dedicated isolated negative pilot and targeted Node tests; `git diff --check`. Runtime tests require actual nonzero unskipped execution. Documentation and host-only tests do not establish isolated verifier behavior.

## Unresolved decisions

No new public runtime protocol, provider, package or fault representation is selected. The exact scoped project/test files above are the complete approved infrastructure surface; any need to alter unrelated safety invariants or trusted tests requires a separate issue.

## Results

- Created the bounded runtime assembly, separate trusted baseline test project, manual clock and scripted randomness, and runtime feature-test location; no actor or store behavior was added. Added locked standalone and isolated runtime verification, immutable feature staging, a discovered-new-test gate for runtime source edits, separate baseline/feature containers, strict host-side TRX validation, and a disposable failing-test pilot. The compiler-only coordinator policy and prior tests remain unchanged.
- `bash scripts/verify-runtime.sh` passed twice consecutively (2 baseline and 1 feature test), and again after a temporary `Issue9TemporaryFailure.DeliberateFailure` caused the standalone script to fail as intended; the temporary file was removed. This host-side probe does not establish isolated verifier behavior.
- Bootstrap passed. `bash .github/ai/verify.sh` passed documentation (113 files, 304 links) and 31 automation tests, but stopped at the existing compiler verifier because the pinned container image pull returned `Forbidden`. The real isolated runtime negative pilot also failed closed before baseline execution for the same pull restriction; it did not falsely report a successful failure proof. The proposed CI step must demonstrate the real pilot and clean isolated runtime verification before acceptance.
- `bash scripts/verify-compiler.sh` passed locked restore/build, 18 baseline and 378 feature tests without skips. Commands used `PATH=/tmp/dsharp-dotnet:$PATH`, `npm_config_cache=/tmp/dsharp-npm-cache`, `DOTNET_CLI_HOME=/tmp/dsharp-dotnet-home`, `NUGET_PACKAGES=/tmp/dsharp-nuget` and `XDG_RUNTIME_DIR=/tmp`. Focused staging/count tests and `git diff --check` passed. These checks do not establish actor or store behavior.
