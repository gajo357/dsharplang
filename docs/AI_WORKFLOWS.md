# Approved issue to reviewed draft PR

This pilot uses GitHub as coordinator, GitHub Agentic Workflows with Codex as the
implementation and review workers, and ordinary Actions as the verifier.
Automation never merges or deploys. The repository currently contains designs,
so the enforced scope is Markdown under `docs/features/` only. It cannot build or
test a DSharp compiler or runtime that does not exist.

## Enable in GitHub

1. Review and merge the workflow files, their generated `.lock.yml` files, and
   the trusted `.github/ai/` harness into `main`. No files have been pushed by
   this setup task. Actions must allow the pinned GitHub and OpenAI tooling.
   GitHub Agentic Workflows is a preview; confirm availability for the repository.
2. Install a dedicated GitHub App on **this repository only**. Give it repository
   Contents, Pull requests, Issues and Actions read/write, and Checks read.
   Do not grant Administration, Workflows, deployment permissions, or protection
   bypass. The coordinator token requests Actions write, Contents read and Pull
   requests write; only safe-output jobs receive publishing authority.
3. In repository Actions settings, add variable `AI_APP_CLIENT_ID` and secret
   `AI_APP_PRIVATE_KEY`. Add secret `CODEX_API_KEY` (or `OPENAI_API_KEY`) for an
   OpenAI API account, and variable `AI_MODEL` with an explicitly selected model
   supported by that account. A ChatGPT subscription login is not this API
   credential. Enter secrets through GitHub settings, never issues or chat.
   These are **GitHub Actions settings**, separate from cloud environment secrets.
4. Create label `ai:approved`. Restrict its use to maintainers. Protect `main`:
   require `CI / documentation-and-automation`, human approval, dismissal of stale
   approvals, and no automation App bypass. Protect `ai/issue-*` against force
   pushes and deletion during active runs. Automation must not count as the
   required human approval.
5. Use the approved automation issue form. Every criterion must have an `AC1:`
   style identifier on its own line. A maintainer adds `ai:approved`, then runs
   **AI coordinate** on `main`, supplying the issue number. This is the scope
   approval; editing the issue later does not change the frozen task.

Do not launch the worker workflows directly: they require an active coordinator
and its immutable approved-manifest artifact. Rerunning old runs is rejected.
If a blocked task already has a branch or PR, reconcile it manually before
starting another run; the controller deliberately refuses to guess ownership.

## What runs

`ai-coordinate.yml` authorizes the launcher, freezes the issue body/hash, base
SHA, instruction hashes, selected model and deadline, then checks the baseline.
Workers use the frozen model selection for this task. One task runs per
repository; another dispatch waits through Actions concurrency. GitHub may
replace an older pending dispatch when another one is queued.

`ai-implement.md` starts a read-only authenticated worker. A separate safe-output
job creates one draft PR on `ai/issue-N`. Publishing allows only feature Markdown,
20 changed files and 128 KiB. It blocks protected files and duplicate task branches.
The worker saves a plan artifact with acceptance-criterion evidence mapping.

For every candidate revision, `ai-verify.yml` runs on a fresh runner using the
checker from the frozen trusted base. It rejects scope violations, deletions,
symlinks and executable files before checking documentation. It uploads exact
revision metadata, documentation results and JUnit automation reports.
`ai-review.md` starts a fresh Codex context with the frozen specification, diff
and CI evidence. Its structured report must contain evidence for every criterion
and the exact head SHA. Malformed, missing or stale output blocks the task.

The TypeScript controller mechanically combines those results. Only complete,
successful current evidence makes the PR ready for human review. Fixable check
failures or reviewer blockers trigger `ai-repair.md` on the same PR, at most
twice. Both verification and review run again after each repair. Missing or
cancelled checks, policy failures, infrastructure failures, conflicting edits,
changed main, and architectural decisions block instead of initiating repairs.
The overall deadline is 60 minutes, with 20 minutes per agent execution; setup
and queue time also consume the overall deadline. The controller cancels tracked
unfinished workers when it detects a blocker. GitHub runner failure or forcibly
cancelling the controller may prevent cleanup: cancel its child runs manually.

Download `approved-manifest` and `coordinator-state` from the coordinator run for
the frozen inputs, phase, repair count, child run IDs and evidence. Worker runs
retain plan/review artifacts and framework usage/log artifacts. The ready summary
names the checked SHA and verification/review runs. Failed tasks remain visibly
failed with a draft PR. There is no monetary spend cap; time and attempts are
bounded, and API billing limits should be configured separately.

PR and coordinator status are mutable GitHub metadata. Human branch edits after
readiness require new checks; branch protection is the final merge boundary.
Actions cannot atomically mark ready conditional on a head SHA: the controller
checks immediately before and after the mutation and withdraws readiness on a
detected concurrent change. A human can always change a draft's status manually.

## Local development and regeneration

Requirements: Linux amd64, Git, curl, unzip, Python 3.11+ and Node 24+. No runtime
services or external credentials are needed for local validation.

```bash
bash .github/ai/bootstrap.sh
bash .github/ai/install-tools.sh
bash .github/ai/verify.sh
/workspace/.tools/gh-aw compile --no-check-update --no-logo \
  --action-mode action --action-tag 5c7541d098b1fd0b039865a2e69849b0fe3c679c
```

The tool installer verifies the official v1.2.2 binary against its release SHA256.
Commit the Markdown and regenerated lock files together. On an intentional
secret/action change, review the compiler's safe-update report before using
`--approve`; do not bypass signature, checksum or TLS verification. The generated
`.gitattributes` entry marks locks as generated for GitHub review.
`aw.json` disables the unrelated scheduled maintenance workflow.

The tests exercise the real Git diff policy and the controller through a mocked
GitHub API, including readiness, stale reports and exhausting two repairs.
Documentation checks verify nonempty design files and local file links. They do
not validate language semantics, external URLs or Markdown anchor correctness.
JUnit discovery rejects missing/skipped required automation tests.

The ordinary workflow YAML passes actionlint 1.7.12. That version reports newer
framework-generated `queue`, `cache-mode` and `job.workflow_*` syntax as unknown;
the agent workflow sources/locks are validated by the v1.2.2 compiler. Do not edit
generated locks to silence an older linter. GitHub execution remains unverified
until the first configured pilot runs.

## Compilation credential/action review

The initial compiler report added `AI_APP_PRIVATE_KEY`, `CODEX_API_KEY` and
`OPENAI_API_KEY` references. The App key is confined to restricted output jobs;
the model credential is handled by the framework's isolated inference proxy.
No credential values are stored in these files. Other optional framework token
references should remain unset unless separately reviewed. All authored actions
and the framework setup action use full commit pins; compiler-managed actions
and container images are also pinned. New authored actions are checkout,
setup-node, upload-artifact, download-artifact and create-github-app-token. No
custom redirect or external reporting destination was added. Generated shell,
token scopes and third-party code still need human review before enabling the
App or API spend; local compilation is not an audit or a live execution test.

## Enable implementation later

.NET is the established target platform. Compiler implementation language,
emission strategy and runtime protocols remain open in the architecture docs.
First approve a minimal executable skeleton with pinned dependencies and a real
build/test entry point. In a separately reviewed automation change, replace the
documentation-only verifier, require nonzero runtime test discovery and AC test
mapping, and update policy, safe-output allowlists and controller preflight
together. Protect that trusted acceptance harness from worker modifications.
Do not simply add `src/` to an allowlist and describe documentation checks as
runtime verification.
