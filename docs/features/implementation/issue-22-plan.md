# Implementation plan: #22 — Typed if/else and lexical scopes

Issue: https://github.com/gajo357/dsharplang/issues/22
Base: `b9ae493` on main; #108 and the bool prerequisite are merged.

## Scope and approach

Implement value-producing `if condition { ... } else { ... }` expressions for
existing int/bool values. Conditions must be bool; branches must produce the
same type and each require a final expression. Unit is not implemented yet, so
this slice requires else. Branch blocks can contain local let bindings and nested
conditionals. Reject redeclaring any visible parameter/local; sibling branch
names may be reused and no branch local leaks outside its block.

Extend syntax and bound expression/block trees, parse expressions and nested
blocks, bind branch-local scopes with restoration, and emit typed local helper
functions so branch code executes lazily, including when nested inside &&/||,
function arguments, arithmetic and let initializers. Preserve checked arithmetic,
left-to-right evaluation, existing public compiler entry points and tests.

## Evidence and verification

AC1: execute true/false branches and reject non-bool/missing-else conditions.
AC2: reject branch type mismatches, visible-name redeclaration and out-of-scope
references; prove independently scoped sibling and nested locals.
AC3: execute nested conditionals and nonselected faulting branches; add feature
tests under tests/features, an example and status update. Run bootstrap, compiler
script, full verifier, CLI example and diff checks. GitHub CI supplies Docker
verification if the pinned image remains locally inaccessible.

No loops, nullable narrowing, pattern matching, Unit or additional types.
Do not merge or start the next issue. Fix PR/CI failures on the same branch.

## Progress

- Added typed conditional/block syntax and binding, separate branch-local scopes,
  no-shadow checks and typed generated local helper functions. Helpers keep
  conditions, local initializers and branches lazy even inside short-circuit
  expressions and argument lists.
- Added 13 new feature test cases covering both/nested branches, lexical scope,
  diagnostics, multiline conditional initializers/argument continuations,
  checked overflow, left-to-right fault ordering and skipped faulting branches.
  Generated projects are built with warnings treated as errors.
- Bool literals are emitted via pure local values so a valid constant condition
  does not trigger a C# unreachable-code warning under warnings-as-errors.
- Bootstrap, CLI example check/compile (inside the repository with warnings as
  errors), locked restore, Release build with zero warnings, 18/18 compiler tests,
  96/96 feature tests, 20/20 automation tests, documentation (20 files, 57 links)
  and diff checks passed. Existing tests were not changed.
- The full local verifier reached its offline compiler stage but the pinned SDK
  image pull returned Forbidden after six attempts. GitHub Actions must complete
  the unchanged trusted verifier and pilots.

Commands with workspace-local SDK/cache selectors:

```bash
export PATH="/workspace/work/dotnet:$PATH"
export DOTNET_ROOT=/workspace/work/dotnet
export DOTNET_CLI_HOME=/workspace/work/dotnet-cli
export NUGET_PACKAGES=/workspace/work/nuget-packages
export npm_config_cache=/workspace/work/npm-cache
bash .github/ai/bootstrap.sh
bash scripts/verify-compiler.sh
dotnet run --project src/DSharp.Cli -- check examples/if-scopes.ds
dotnet run --project src/DSharp.Cli -- compile examples/if-scopes.ds artifacts/if-scopes-demo
AI_REPORT_DIR=/workspace/work/issue22-verification bash .github/ai/verify.sh
node .github/ai/check-docs.mjs .
git diff --check
```

## PR

Pending user review and merge.
