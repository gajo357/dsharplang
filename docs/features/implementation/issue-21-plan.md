# Implementation plan: #21 — Boolean values and short-circuit logic

Issue: https://github.com/gajo357/dsharplang/issues/21
Base: `397fa1e` on main. Prerequisite #19 and #20 are merged.

## Scope

Add bool literals, function/parameter/local bool types, int ordering/equality,
bool equality, logical !, &&, ||; require compatible types and reject chained
comparisons. Keep int arithmetic and same-module calls working. No truthiness,
nullable flow, data equality or bitwise operators.

## Steps and evidence

1. Write this plan before implementation.
2. Extend lexical/syntax nodes and precedence parsing; bind typed expressions,
   parameters, locals, return values and calls with source diagnostics.
3. Emit typed C# values and preserve true short-circuit behavior for all RHS
   expressions and calls. Existing public compiler APIs and tests stay supported
   except obsolete negative fixtures for newly valid syntax, if any; adapt only
   those exact inputs and document a scoped CI migration if required.
4. Add feature tests and example, update status; verify and open one draft PR.
   Fix CI on this same branch; do not merge or start #22.

AC1: generated execution of bool values, functions, parameters, locals and each
operator, with precedence and grouping. AC2: diagnostics for incompatible types,
wrong arguments/returns and chained comparisons with D# spans. AC3: execute short
circuit cases whose RHS would otherwise divide by zero or throw, including calls
and nesting, and tests proving RHS executes when needed.

Run bootstrap, compiler verification, full trusted verifier, CLI example and diff
checks. Report actual results; GitHub Actions supplies offline Docker verification
if the local pinned image remains inaccessible.

## Progress

- Implemented bool literals/types, typed locals/parameters/results/calls, int
  ordering and int/bool equality, logical negation, conjunction/disjunction,
  operator precedence and grouped-comparison handling. Unparenthesized chained
  comparisons and incompatible operands are rejected with D# diagnostics.
- Generated code preserves short-circuit evaluation and checked int arithmetic.
  Execution tests prove faulting calls and division on the RHS are skipped or
  executed as appropriate.
- One obsolete feature-test input formerly rejected inferred true; it now uses
  unsupported else and retains the same diagnostic/span assertion. Trusted
  compiler tests, including true-as-int rejection, remain unchanged.
- Bootstrap passed; locked restore/Release build (zero warnings/errors), 18/18
  compiler tests and 83/83 feature tests passed. The CLI example checked and
  compiled. Documentation (19 files, 55 local links) and 20/20 automation tests
  passed; git diff --check passed.
- The full local verifier reached its offline compiler stage but the pinned SDK
  image pull returned Forbidden after six attempts. GitHub Actions must provide
  the end-to-end verifier and pilot results.

Commands with workspace-local SDK/cache selectors:

```bash
export PATH="/workspace/work/dotnet:$PATH"
export DOTNET_ROOT=/workspace/work/dotnet
export DOTNET_CLI_HOME=/workspace/work/dotnet-cli
export NUGET_PACKAGES=/workspace/work/nuget-packages
export npm_config_cache=/workspace/work/npm-cache
bash .github/ai/bootstrap.sh
bash scripts/verify-compiler.sh
dotnet run --project src/DSharp.Cli -- check examples/bool-logic.ds
dotnet run --project src/DSharp.Cli -- compile examples/bool-logic.ds /workspace/work/bool-logic-example
AI_REPORT_DIR=/workspace/work/issue21-verification bash .github/ai/verify.sh
node .github/ai/check-docs.mjs .
git diff --check
```

## PR

Draft PR: https://github.com/gajo357/dsharplang/pull/108

User reviews and merges after required checks pass, before #22. Fix any PR/CI
issues on this same branch.
