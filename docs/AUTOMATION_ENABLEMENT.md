# Automation enablement validation

This is the separate trusted-infrastructure change stacked on the executable
compiler bootstrap. Review/merge the bootstrap first, retarget this PR to main,
then review the policy/publisher/controller/verifier changes together. Neither PR
is automatically merged. The complete language and agent runtime remain out of scope.

## Local checks

The existing documentation checker and automation suite remain required. The
suite now includes real Git diff policy tests, a real TRX parser test, publisher
allowlist parity and controller/API pilots. It verifies that source plus new
feature tests is allowed; old tests, manifests/locks and workflow edits are not.
A successful mocked code-task controller pilot reaches READY only after fresh
CI evidence and independent review. Build failure exhausts exactly two repairs;
zero tests, stale evidence and forbidden edits cannot reach readiness.

`python3 .github/ai/pilot.py` is a reproducible operator-run compiler pilot. It
constructs disposable candidates without GitHub/model calls: an implementation
comment plus a meaningful new literal-binding test, then a deliberately broken
C# source, then a feature file containing no tests. It runs the actual offline
verifier and asserts that only the first candidate succeeds. It does not claim
to implement a new language feature or exercise the live model worker.

## Observed results

- Full bootstrap/verify entrypoint: documentation links passed, 15 automation tests
  passed with no skips, locked compiler restore and Release build passed, and 18
  protected acceptance tests passed in the offline container.
- Real compiler pilot: success exited 0 with 18 acceptance tests and 1 feature test;
  deliberate build failure exited 1; zero discovered feature tests exited 1.
- Real Git policy tests rejected a workflow edit, package lock/project edits,
  existing-test edits, symlinks, deletions and oversized patches.
- Mocked controller pilots: success became READY only after independent review;
  build failure never became ready and exhausted two repairs; zero/stale/forbidden
  evidence stayed BLOCKED. These are local API fixtures, not live model runs.
- All three agent workflow locks regenerated with pinned gh-aw v1.2.2, with zero
  compiler warnings. Authored ordinary workflow YAML passed actionlint 1.7.12.

## Environment and rollout

The cloud proxy allows MCR manifests but blocks the SDK image layer redirect at
`westus.data.mcr.microsoft.com`. Local container validation therefore uses an
operator-supplied image assembled from the installed exact .NET 10.0.100 SDK and
system libraries. This is explicitly different from the default digest-pinned
MCR image. Workflows use the default; they do not set the local override.
The bootstrap PR's ordinary CI and Compiler workflows passed on GitHub.

The exact-image CI result and final local pilot outcomes are recorded in the PR
verification summary. No live GitHub/model task has been dispatched, no App/API
settings have been changed, and no merge/deployment has occurred. After human
review/merge, run a live pilot through AI coordinate with an approved small issue
and existing configured App/API settings before production code tasks.
