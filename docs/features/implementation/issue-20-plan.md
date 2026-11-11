# Implementation plan: #20 — Typed parameters and same-module calls

Issue: https://github.com/gajo357/dsharplang/issues/20
Base: `4cf7fd1` on main; prerequisite #7 and arithmetic #19 are merged. No open PRs existed at task start.

## Scope and behavior

Support explicit `int` parameters and positional calls to functions in the same module, including zero arguments, forward references and nested calls. Parameters are immutable and scoped to their function; reject duplicates, collisions with locals, undeclared functions, wrong arity and unsupported types with D# locations. Preserve checked arithmetic, argument evaluation order, generated public method names and existing compiler entry points. No overloads, other numeric types, imports, named/default arguments or inferred signatures.

## Steps and files

1. Write this plan before implementation.
2. Extend lexer, expression and function syntax in `src/DSharp.Compiler/Compiler.cs` to parse parameters, argument lists and call expressions.
3. Collect signatures before binding bodies, check arity, types and scopes, and emit symbolic calls and uniquely named C# parameters.
4. Add `tests/features/FunctionCallTests.cs`, `examples/function-calls.ds`, and update implementation status.
5. Run required checks, record actual results and open one draft PR. Fix any PR issues on the same branch. Do not merge or start #21.

## Acceptance evidence

- AC1: generated execution of parameterized, forward, zero-argument and nested same-module calls; parse malformed lists.
- AC2: verify int types, duplicate parameters, collisions, immutable assignments, argument count and local scope diagnostics with source spans.
- AC3: execute nested/forward calls and arithmetic argument expressions; reject unknown functions and shadowed names. Test argument fault order.

## Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`, CLI example and `git diff --check`. Preserve the trusted harness and all relevant test assertions. The existing trusted
negative fixture for `fn answer(x: int): int { 42 }` contradicts this feature;
change only its input to the still-invalid missing-type form `fn answer(x): int
{ 42 }`, retaining DS1002 and the test count. CI uses the base revision of the
trusted test regardless of this branch, so the fixture fix must be on main before
the base verifier can pass. Keep the fixture correction in its own commit for
review. Previous runs could not pull the pinned Docker SDK image; report the result.

## Decisions and blockers

The issue defines the bounded int-only contract. Resolve declarations in a first pass; preserve legacy constructors and fields. Parenthesized calls use positional arguments only. No language/runtime decision beyond this issue is adopted.

## Progress

- Implemented explicit int parameters, positional call expressions, signatures
  collected before body binding, argument arity/type checks, function-local
  parameter scope and generated public C# methods. Names are escaped/qualified
  and generated parameters avoid collisions.
- Added forward/nested execution, keyword/generated-name collision, checked
  arithmetic and argument evaluation-order tests, plus diagnostic coverage.
- The original trusted parameterized-function rejection was run and fails exactly
  because the now-valid example compiles. The one-input fixture correction keeps
  negative missing-type coverage and is isolated in its own commit. CI uses the
  base's old test and remains blocked until that correction is on main; changing
  the candidate test alone does not fix the trusted-base verifier.
- Compiler script passed locally after that fixture correction: locked restore,
  build with zero warnings/errors, 18/18 compiler tests and 65/65 feature tests.
  The final expanded function-call suite also passed 15/15.
- Bootstrap and CLI check/compile passed; generated example library built.
  Documentation passed (18 files, 53 links) and automation 19/19.
- Full verifier blocked pulling its pinned SDK image (Forbidden after six tries).
  Its base-test conflict is a separate confirmed CI blocker, even where Docker works.

Commands with local SDK/cache selectors:

```bash
export PATH="/workspace/work/dotnet:$PATH"
export DOTNET_ROOT=/workspace/work/dotnet
export DOTNET_CLI_HOME=/workspace/work/dotnet-cli
export NUGET_PACKAGES=/workspace/work/nuget-packages
export npm_config_cache=/workspace/work/npm-cache
bash .github/ai/bootstrap.sh
bash scripts/verify-compiler.sh
dotnet test tests/DSharp.Compiler.Tests --no-restore -c Release -p:DSharpTestGroup=Features --filter FullyQualifiedName~FunctionCallTests
dotnet run --project src/DSharp.Cli -- check examples/function-calls.ds
dotnet run --project src/DSharp.Cli -- compile examples/function-calls.ds /workspace/work/function-calls-example
AI_REPORT_DIR=/workspace/work/issue20-verification bash .github/ai/verify.sh
node .github/ai/check-docs.mjs .
git diff --check
```

## PR

Draft PR: https://github.com/gajo357/dsharplang/pull/107

The isolated test-only commit dae5479aeec9e4faffbd57809b3c33e9a0ae881e must be
reviewed and landed on main before the trusted-base verifier can accept this PR.
Then update this branch, rerun checks, and review/merge before #21.
