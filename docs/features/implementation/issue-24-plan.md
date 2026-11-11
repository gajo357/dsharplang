# Implementation plan: #24 — Unit and explicit return

Issue: https://github.com/gajo357/dsharplang/issues/24
Base: `a7c712a` on main; #109/#110 and conditional prerequisites are merged.

## Scope and behavior

Support a distinct Unit value `()` and explicit early `return` targeting the
nearest function. Empty blocks evaluate to Unit; a non-Unit function must have
a correctly typed result on every reachable completion path. An if without else
is Unit-valued and its completing then branch must be Unit. Conditional branches
that return do not constrain the type of completing branches. Check return
values against the declared function result; a bare return supplies Unit.

Parse ordered local declarations, Unit expression statements and explicit return
statements in function and branch blocks. Bind lexical scopes and completion
flow, preserving existing grammar and API entry points. Allow code after a return
but do not evaluate it. No loops, exceptions syntax, implicit Success or new
other types.

Because conditionals lower to C# local helpers, early returns need to cross those
helpers without evaluating remaining operands or arguments. Use a private,
typed return signal caught only by the nearest generated function, preserving
other runtime exceptions and call isolation. Unit is a distinct generated value
type; emit names that avoid collisions with source declarations. Retain checked
arithmetic and short-circuit evaluation.

## Evidence

- AC1: Unit values in locals, arguments, returns and explicit return; reject wrong
  return types with source spans.
- AC2: if-without-else requires Unit and has reachable Unit fallthrough; reject
  reachable missing non-Unit results, mismatched branches and non-Unit statements.
- AC3: execute early return from nested branches, operands and call arguments;
  prove subsequent faulting expressions are skipped, left-to-right ordering and
  callee isolation hold.
- AC4: execute empty blocks and no-final-value Unit functions/branches; never
  synthesize Result/Success.

Add compiler code, new feature tests, example and status. Preserve APIs, trusted
verification and existing tests; only adapt obsolete feature fixtures where
newly valid syntax directly contradicts them. Run bootstrap, compiler script,
full verifier, CLI example, diff checks and GitHub Actions. Fix CI in this PR.

## Progress

- Added distinct non-null Unit values/types, ordered Unit expression statements,
  optional else for Unit-valued conditionals, explicit returns and reachable-path
  result checks. Noncompleting expressions retain their control-flow and type
  context through nested conditions, arithmetic, calls and short circuit.
- Generated typed return signals are caught only by the owning D# function. Pure
  helper calls avoid unreachable C# warnings; normal arithmetic faults remain
  untouched. Generated Unit and helper names avoid source-name collisions.
- Added 15 Unit/return feature test cases and generated execution coverage for
  nested returns, both-branch returns, never conditions/composite expressions,
  Unit values/parameters, skipped faults, checked arithmetic, argument order,
  name collisions and warnings-as-errors. Existing tests remain unchanged.
- Bootstrap, CLI example check/compile inside repository, locked restore, Release
  build with zero warnings, 18/18 compiler tests, 131/131 feature tests, 20/20
  automation tests, documentation (22 files/61 links) and diff checks passed.
- The full local verifier reached its offline compiler stage but the pinned SDK
  image pull returned Forbidden after six attempts. GitHub Actions must run the
  unchanged trusted verifier and pilots before merge.

Commands with workspace-local SDK/cache selectors:

```bash
export PATH="/workspace/work/dotnet:$PATH"
export DOTNET_ROOT=/workspace/work/dotnet
export DOTNET_CLI_HOME=/workspace/work/dotnet-cli
export NUGET_PACKAGES=/workspace/work/nuget-packages
export npm_config_cache=/workspace/work/npm-cache
bash .github/ai/bootstrap.sh
bash scripts/verify-compiler.sh
dotnet run --project src/DSharp.Cli -- check examples/unit-return.ds
dotnet run --project src/DSharp.Cli -- compile examples/unit-return.ds artifacts/unit-return-demo
AI_REPORT_DIR=/workspace/work/issue24-verification bash .github/ai/verify.sh
node .github/ai/check-docs.mjs .
git diff --check
```

## PR

Pending user review and merge.
