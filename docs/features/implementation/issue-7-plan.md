# Implementation plan: #7 — Immutable local let bindings

## Scope and baseline

Issue: https://github.com/gajo357/dsharplang/issues/7

Base: `21769d9` (main, including merged instruction PR #102).
No open PRs were returned at task start. Follow P01 of the implementation
sequence; #7's existing issue body is the feature contract.

## Expected behavior

A function may declare inferred or explicitly int-typed immutable locals,
initialized from an existing integer literal or earlier local. Its final
expression is a literal or local. The issue's Demo.answer program returns 42.
Bindings occupy separate lines, become visible after their initializer is
checked, and belong only to their function. Existing literal-only programs work.

## Implementation steps

1. Write this plan before code changes.
2. Extend lexer punctuation and syntax records for let declarations and references,
   keeping existing compiler entry points and literal-function record constructors.
3. Parse line-separated declarations and one final expression; diagnose attempts
   to assign, unsupported expressions, malformed bindings and missing separators.
4. Bind declarations in order with a new environment per function. Check explicit
   annotations, duplicates, unresolved/self/forward references and integer range.
5. Emit actual C# local declarations and their references using generated local
   names so D# names cannot collide with C# keywords or generated container types.
6. Add new feature tests, the requested example and precise status; run existing
   bootstrap, full verifier and compiler script. Publish one draft PR; do not merge
   or start arithmetic (#19).

## Files and interfaces

- `src/DSharp.Compiler/Compiler.cs`: lexer, syntax, parser, binder, C# emitter.
- `tests/features/LetBindingTests.cs`: positive execution and negative diagnostics.
- `examples/let-bindings.ds`: issue's example.
- `docs/features/DSharp_Implementation_Status.md`: tested scope.
- This per-issue plan, required by the merged repository instructions.

No dependencies, existing tests, workflow/policy, trusted verifier or repository
instruction changes. No arithmetic, calls, parameters, nested scopes or new types.

## Acceptance evidence

| AC | Planned evidence |
| --- | --- |
| AC1 | Inferred literal/earlier-local compilation and generated execution tests. |
| AC2 | Explicit int local execution and invalid annotation rejection. |
| AC3 | Literal/local final expressions, including existing unchanged compiler suite. |
| AC4 | Reassignment, duplicate, undeclared, self/forward-reference tests checking code, path and exact source span. |
| AC5 | Separate functions reuse local names; cross-function access is rejected. |
| AC6 | Feature suite builds generated C# and invokes methods, observing 42 and distinct local values. |
| AC7 | Requested example and implementation status matrix. |

## Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-compiler.sh` and `git diff --check` using the existing pinned
SDK and local caches. The previous task's full verifier could not pull its pinned
Docker image; retry and record the actual result without weakening the harness.

## Decisions and blockers

Use only int, the existing identifier/literal range and the existing C# backend.
No new public language decision is required. Token line locations enforce this
slice's separate-line declarations; no general newline grammar is introduced.

## Progress and deviations

- Implemented syntax, ordered per-function binding and C# local/reference emission.
  Existing literal-function constructors and tests remain compatible.
- Added the requested example and updated the precise implementation status.
- Bootstrap passed with .NET SDK 10.0.100 and workspace-local caches.
- Compiler script passed locked restore and Release build (zero warnings/errors),
  unchanged compiler suite 18/18 and new feature suite 35/35, no skipped tests.
  Generated-code tests build and invoke inferred/typed chains, integer boundaries,
  keyword/generated-name locals, CRLF/comments and independent functions.
- Negative tests cover reassignment, duplicate names, unknown/self/forward/cross-
  function references, invalid annotations, overflow, malformed/incomplete input
  and unsupported syntax. They check exact codes, path and source spans.
- CLI check passed for examples/let-bindings.ds. CLI compile built the generated
  .NET library successfully with zero warnings/errors.
- Full verifier passed documentation (15 files, 49 local links) and automation
  regressions (18/18, no skips). Its offline compiler stage remains blocked by
  Docker's pinned SDK-image pull: download failed after six attempts, Forbidden.
  The full verifier did not pass; no harness/image override was used.
- git diff --check passed. Existing tests, dependencies and trusted infrastructure
  remain unchanged. The plan file is the addition required by merged AGENTS.md
  beyond the older issue's explicit feature-file list.

Exact commands (with local selectors to make the existing SDK/caches available):

```bash
export PATH="/workspace/work/dotnet:$PATH"
export DOTNET_ROOT=/workspace/work/dotnet
export DOTNET_CLI_HOME=/workspace/work/dotnet-cli
export NUGET_PACKAGES=/workspace/work/nuget-packages
export npm_config_cache=/workspace/work/npm-cache
bash .github/ai/bootstrap.sh
bash scripts/verify-compiler.sh
dotnet run --project src/DSharp.Cli -- check examples/let-bindings.ds
dotnet run --project src/DSharp.Cli -- compile examples/let-bindings.ds /workspace/work/let-example
AI_REPORT_DIR=/workspace/work/issue7-verification bash .github/ai/verify.sh
node .github/ai/check-docs.mjs .
git diff --check
```

CI or another environment with access to the pinned image must complete the full
verifier before the PR is treated as fully verified.

## PR

Draft PR pending. The user reviews and merges before #19 begins.
