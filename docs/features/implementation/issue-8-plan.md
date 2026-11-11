# Implementation plan: #8 — Reconcile planning documents

## Scope and baseline

Issue: https://github.com/gajo357/dsharplang/issues/8

Base: `567fbf1c88d8a329339f1e11a92e4ee71bf90e03` on main.
No open PRs were returned by the repository search at task start. The executable
bootstrap and compiler-code automation are present; immutable locals (#7) are not
reported implemented. This task is documentation and instruction reconciliation,
not compiler implementation or a workflow-permission change.

Follow [the implementation sequence](DSharp_Implementation_Sequence.md), phase P00.

## Expected behavior

A contributor can start a scoped Codex web task without configuring the earlier
API-backed coordinator, find the existing compiler commands and actual scope,
and produce one plan and draft PR. Provisional APIs remain provisional.

## Implementation steps

1. Record this plan before changing repository guidance.
2. Reconcile AGENTS.md and the old handoff with the implemented bootstrap and
   Codex web lifecycle; retain the selected language requirements.
3. Link the sequence/task queue from README and distinguish the optional
   coordinator in AI_WORKFLOWS.md without changing its policy or executable files.
4. Correct stale bootstrap/hand-off status text; keep #7 and runtime work pending.
5. Run existing checks, record actual results, inspect the final diff and publish
   one draft PR. Do not merge or start #7.

## Files and interfaces

Allowed documentation changes for this task:

- `AGENTS.md`
- `README.md`
- `docs/AI_WORKFLOWS.md`
- `docs/COMPILER_BOOTSTRAP.md`
- `docs/features/DSharp_Implementation_Instructions.md`
- `docs/features/DSharp_Syntax_Reference.md` (status introduction only)
- `docs/features/DSharp_Implementation_Status.md`
- `docs/features/implementation/issue-8-plan.md`

No dependencies, compiler/runtime code, tests, workflow sources/locks, policy,
trusted verifiers, architecture or language baseline changes.

## Acceptance evidence

| Criterion | Evidence |
| --- | --- |
| AC1 | AGENTS, README, handoff and bootstrap distinguish existing C#/.NET implementation from provisional future APIs; optional coordinator has its own documented scope. |
| AC2 | Handoff links bootstrap and #7, lists current policy globs and exact verification commands. |
| AC3 | Status preserves literal-function-only implementation; #7 remains pending and sequence completion requires tested behavior. |

## Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh` and
`bash scripts/verify-compiler.sh`. Run `git diff --check` and inspect changed paths.
This task does not modify infrastructure, so no deliberate verifier failure is
introduced; existing automation regression tests exercise rejection behavior.

## Decisions and blockers

C# compiler, generated C#, .NET SDK 10.0.100, managed execution and JavaScript
validation are existing choices. Runtime contracts and remaining draft grammar
are not adopted by this PR. Explicit setup issues may propose narrowly scoped
infrastructure/dependency changes through reviewed PRs; ordinary feature tasks
cannot assume that authority. Coordinator policy continues to apply unchanged
when that workflow is selected.

## Progress and deviations

- Baseline inspected; plan written before edits.
- Reconciled all seven guidance/status files; preserved the selected language
  requirements and linked the historical handoff in Git history.
- Installed the existing pinned .NET SDK 10.0.100 into a local workspace directory
  because it was absent from the execution environment; no repository dependency
  or pin changed. Used workspace-local npm, NuGet and CLI caches.
- Bootstrap passed with the pinned SDK on PATH.
- Documentation validator passed: 14 documents, 47 local links.
- Automation regression tests passed: 18/18, no skips. These include build-failure,
  zero-test, stale-verification and forbidden-edit rejection coverage.
- Compiler script passed: locked restore, Release build with zero warnings/errors,
  18/18 real compiler/generated-code tests and strict TRX validation, no skips.
- Full verifier reached its offline container stage but could not pull the pinned
  mcr.microsoft.com/dotnet/sdk image: Docker reported download failed after six
  attempts, Forbidden. Full verifier is blocked, not passed; no image override,
  harness edit or weaker replacement was used.
- git diff --check passed. All changed paths are the eight planned Markdown files;
  no source, test, dependency, workflow or policy files changed.

Commands used with local tooling/cache selectors:

```bash
export PATH="/workspace/work/dotnet:$PATH"
export DOTNET_ROOT=/workspace/work/dotnet
export DOTNET_CLI_HOME=/workspace/work/dotnet-cli
export npm_config_cache=/workspace/work/npm-cache
bash .github/ai/bootstrap.sh
NUGET_PACKAGES=/workspace/work/nuget-packages bash scripts/verify-compiler.sh
AI_REPORT_DIR=/workspace/work/issue8-verification bash .github/ai/verify.sh
node .github/ai/check-docs.mjs .
git diff --check
```

The full verifier must pass in CI or an environment that can pull its pinned
image before this PR is treated as fully verified.

## PR

Draft PR: https://github.com/gajo357/dsharplang/pull/102

The full verifier is blocked locally as recorded above. The user reviews and
merges after required checks pass, before the next dependent task.
