# Implementation plan: #19 — Int arithmetic and parentheses

## Scope and baseline

Issue: https://github.com/gajo357/dsharplang/issues/19
Base: `29d129a` on main. Prerequisite #7 is integrated by PR #103; pilot repair
#105 is also integrated. No open PRs were returned at task start.

## Expected behavior

Unary minus and binary +, -, *, /, % operate on int literals, locals and parenthesized
expressions. Multiplicative operators bind before additive operators; binary
operators associate left. Evaluation is left to right. Checked overflow and zero
division occur at execution, including constant-looking expressions. Division
truncates toward zero. Positive literals remain 0..2147483647; the special
-2147483648 literal is not introduced.

## Steps and files

1. Record this plan before edits.
2. Extend Compiler.cs with expression syntax/bound trees, precedence parsing,
   ordered semantic binding and checked C# emission without constant folding.
   Preserve existing compiler entry points and bootstrap constructors/properties.
3. Add tests/features/ArithmeticTests.cs with generated execution/fault tests,
   precedence/associativity and source-span diagnostics.
4. Add examples/int-arithmetic.ds and update implementation status.
5. Run required checks and open one draft PR; do not merge or start #20.

Allowed changes: compiler C#, new feature tests, example, this plan/status.
The requested new behavior necessarily supersedes one previous feature test input
that treated + as unsupported. Change only that input to unsupported ^, keeping
its exact diagnostic/span assertions and all other prior tests. This preserves
unsupported-operator coverage while testing arithmetic positively in the new suite.
No dependency, project, verifier,
workflow or repository instruction changes.

## Acceptance evidence

- AC1: generated execution of all operators, parentheses, unary minus and locals;
  precedence/left-associativity examples and arithmetic local initializers.
- AC2: generated execution of negative division/remainder, checked add/subtract/
  multiply/negation/division overflow and division/remainder by zero. Faults must
  occur during invocation, not C# compilation or D# binding.
- AC3: malformed/unsupported operands and range/unknown-local diagnostics checked
  against D# spans; existing literal/local suites remain relevant regressions.

## Decisions and verification

The requested issue supplies the int-only contract; broader grammar/types remain
unsupported. Use typed expression trees and emit runtime expressions in checked
context, lifting literals to generated locals to avoid C# constant-expression
errors. Preserve declaration line boundaries so a following final expression that
starts with parentheses or unary minus is not consumed into an initializer.

Run bootstrap, full verifier, compiler verification, CLI example and diff checks.
Previously the pinned Docker image was inaccessible locally; retry and report the
actual result. Record test evidence and any obsolete assertion adaptation below.

## Progress

- Expression syntax and bound trees, precedence parser and checked C# emission are
  implemented. Literals are lifted into runtime int locals so C# constant folding
  does not reject programs whose arithmetic must fault at invocation.
- Bootstrap constructors/properties remain available. Bound expression trees are
  authoritative for arithmetic; legacy scalar Value fields retain the original
  literal/local-only behavior and are not arithmetic constant-evaluation APIs.
- The obsolete + rejection input in LetBindingTests changes only to ^. Its code,
  exact-span and path assertions remain intact; all other previous tests are
  unchanged. This is necessary for the newly requested behavior, not a skipped
  regression or relaxed assertion. No trusted acceptance tests are changed.
- Compiler verification passed: locked restore, Release build with zero warnings
  or errors, 18/18 compiler tests and 50/50 feature tests, no skips, strict TRX.
  The 15 new tests execute 21 successful expression fixtures and 13 runtime-fault
  fixtures plus exact-span malformed/invalid-operand cases. The CLR min-int % -1
  overflow case is included alongside signed division/remainder behavior.
- Bootstrap passed. CLI check and compile of examples/int-arithmetic.ds passed;
  the generated library builds with zero warnings/errors.
- Full verifier passed documentation (17 files, 51 local links) and all 19
  automation tests, including the merged pilot-isolation regression. Its offline
  compiler stage is blocked by the pinned Docker image pull: download failed
  after six attempts, Forbidden. Full verification is not claimed passed.
- git diff --check and final documentation checks pass. No dependencies, project,
  workflow, policy, verifier or instruction files change.

Commands used with workspace-local SDK/cache selectors:

```bash
export PATH="/workspace/work/dotnet:$PATH"
export DOTNET_ROOT=/workspace/work/dotnet
export DOTNET_CLI_HOME=/workspace/work/dotnet-cli
export NUGET_PACKAGES=/workspace/work/nuget-packages
export npm_config_cache=/workspace/work/npm-cache
bash .github/ai/bootstrap.sh
bash scripts/verify-compiler.sh
dotnet run --project src/DSharp.Cli -- check examples/int-arithmetic.ds
dotnet run --project src/DSharp.Cli -- compile examples/int-arithmetic.ds /workspace/work/arithmetic-example
AI_REPORT_DIR=/workspace/work/issue19-verification bash .github/ai/verify.sh
node .github/ai/check-docs.mjs .
git diff --check
```

CI must finish the offline verifier and real pilots using the pinned image before
the PR is treated as fully verified.

## PR

Draft PR: https://github.com/gajo357/dsharplang/pull/106

User reviews/merges after required checks pass, before #20. Further fixes stay
on this PR branch.
