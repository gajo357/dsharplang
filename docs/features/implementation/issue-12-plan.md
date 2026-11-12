# Implementation plan: #12 — Advanced grammar and symbol resolution

Issue: https://github.com/gajo357/dsharplang/issues/12
Base: `7d487df` on main; the P01 compiler and #111 are merged. No open PRs were found at task start.

## Scope and approach

Produce a bounded design proposal in `docs/features/implementation/DEC-02.md`,
using syntax-reference revision 0.3 and the existing compiler as evidence. The
record is not a claim of implementation or approval. Maintainers must explicitly
adopt the reviewed contract before dependent implementation starts.

The issue permits only the decision record. This plan is the additional artifact
required by the repository's per-issue workflow; no compiler, baseline, policy,
workflow, dependency or trusted-test edits are in scope.

1. Inventory existing implemented grammar and marked proposals; identify exact
   ambiguities and dependencies.
2. Specify a coherent proposed bounded contract for function types, lambdas,
   generic calls, return inference, module/import/extension lookup and diagnostics.
3. Specify proposed precedence, keywords, newline/comment/escape handling and
   parser fixtures, retaining settled invariants. Distinguish current limitations
   from future intended behavior and explicitly flag adoption gates.
4. Validate examples and references against repository documents, run available
   required checks and open one draft PR. User reviews/merges; no automatic adoption
   or dependent work.

## Acceptance evidence

- AC1: explicit grammar and positive/negative fixtures for lambdas, function types,
  generic disambiguation and return inference/recursion.
- AC2: deterministic namespace/import and extension resolution, ambiguity and
  duplicate diagnostics with fixtures.
- AC3: precedence/associativity, reserved/contextual words, lexical/comment/escape
  rules and example fixtures.
- AC4: public-only, braces, no semicolons or general reassignment retained.
- AC5: newline continuation, non-nested block comments and documentation attachment
  rules, with explicit supported/rejected cases.

## Verification and blockers

Run `bash .github/ai/bootstrap.sh` and `bash .github/ai/verify.sh`; also run
`bash scripts/verify-compiler.sh` per AGENTS.md where available. Run the local link
validator and `git diff --check`. Record actual results, including any environment
blocker. Passing tests does not adopt a proposal. Explicit maintainer adoption is a
separate gate and cannot be inferred from creating or merging a design PR.

## Progress

- Added a bounded, explicitly unadopted DEC-02 proposal with grammar, parser
  fixtures, inference/recursion limits, qualified and unqualified name lookup,
  declaration namespaces, union-case ambiguity, extension conflicts, precedence,
  lexical/newline/comment rules and dependent issue links.
- Cross-checked selected/provisional rules against syntax reference revision 0.3
  and current implementation status. Independent reviews led to explicit
  handling of contextual receivers, primitive names, recursive/indirect inference,
  module-prefix collisions, extension fallback and ambiguous union case names.
- Bootstrap passed with the pinned .NET 10.0.100 SDK. Compiler verification passed
  locked restore, Release build with zero warnings/errors, 18/18 compiler tests
  and 131/131 feature tests. Documentation check passed (24 files, 65 local
  links); all 20 automation tests passed, no skips.
- The full local verifier reached its offline compiler stage but its pinned SDK
  image pull returned Forbidden after six attempts. GitHub Actions must complete
  the unchanged trusted verifier and real pilots. Passing checks does not adopt
  the decision contract or implement proposed language features.

Commands with workspace-local SDK/cache selectors:

```bash
export PATH="/workspace/work/dotnet:$PATH"
export DOTNET_ROOT=/workspace/work/dotnet
export DOTNET_CLI_HOME=/workspace/work/dotnet-cli
export NUGET_PACKAGES=/workspace/work/nuget-packages
export npm_config_cache=/workspace/work/npm-cache
bash .github/ai/bootstrap.sh
bash scripts/verify-compiler.sh
AI_REPORT_DIR=/workspace/work/issue12-verification bash .github/ai/verify.sh
node .github/ai/check-docs.mjs .
git diff --check
```

## PR

Pending review and merge.
