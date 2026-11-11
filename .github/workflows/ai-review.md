---
name: AI review
run-name: "${{ inputs.run_key }}"
on:
  workflow_dispatch:
    inputs:
      model:
        type: string
        required: true
      controller_run:
        type: string
        required: true
      run_key:
        type: string
        required: true
      base_sha:
        type: string
        required: true
      head_sha:
        type: string
        required: true
      ci_run:
        type: string
        required: true
permissions:
  contents: read
  actions: read
  issues: read
  pull-requests: read
engine:
  id: codex
  model: "${{ inputs.model }}"
timeout-minutes: 20
concurrency:
  job-discriminator: "${{ github.run_id }}"
network:
  allowed: [defaults, node]
tools:
  github:
    toolsets: [default, actions]
  bash: ["*"]
safe-outputs:
  report-failed-jobs: false
  noop:
steps:
  - uses: actions/setup-node@49933ea5288caeca8642d1e84afbd3f7d6820020
    with:
      node-version: "24"
  - uses: actions/download-artifact@d3f86a106a0bac45b974a628896c90dbdf5c8093
    with:
      name: approved-manifest
      path: /tmp/gh-aw/task
      run-id: ${{ inputs.controller_run }}
      github-token: ${{ github.token }}
  - name: Preserve trusted context loader
    run: |
      mkdir -p /tmp/gh-aw/task
      cp .github/ai/prepare-worker.mjs /tmp/gh-aw/task/prepare-worker.mjs
pre-agent-steps:
  - name: Validate controller and select immutable candidate
    env:
      GH_TOKEN: ${{ github.token }}
    run: node /tmp/gh-aw/task/prepare-worker.mjs review
post-steps:
  - uses: actions/upload-artifact@ea165f8d65b6e75b540449e92b4886f43607fa02
    with:
      name: review-${{ inputs.head_sha }}
      path: /tmp/gh-aw/task/review.json
      if-no-files-found: error
      retention-days: 14
---

Review the candidate in a fresh context. Read /tmp/gh-aw/task/context.json,
the frozen specification, architecture documents, the exact diff from baseSha to
selectedHead, and CI run ${{ inputs.ci_run }} using read-only GitHub tools.
Do not rely on implementation prose. Check each acceptance criterion, correctness,
scope, weak evidence, contradictions, and unnecessary complexity.
Do not edit repository files. Write ONLY the structured result to
/tmp/gh-aw/task/review.json, following .github/ai/schemas/review.schema.json.
headSha must be the exact selectedHead. A pass requires evidence for every AC and
zero blockers. Missing CI, ambiguous requirements, or a new architectural choice
must not pass. Use needs_decision for unresolved architecture or scope.
The controller validates the JSON; your prose is never readiness evidence.


Check actual compiler build and machine-readable acceptance/feature test evidence
from CI, not documentation checks alone. Verify each AC maps to relevant test
names/fixtures, source diagnostics and generated execution where applicable.
Zero/missing/skipped tests, weakening old tests, dependency changes or unsupported
syntax decisions cannot pass. Existing tests, manifests and trusted infrastructure
are protected; workers may add feature tests only. No merge or deployment.
