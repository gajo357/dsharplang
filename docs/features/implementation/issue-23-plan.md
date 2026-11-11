# Implementation plan: #23 — String and char literals

Issue: https://github.com/gajo357/dsharplang/issues/23
Base: `d84dd31` on main, including merged #109.

## Scope

Support immutable UTF-16 string and char literals, annotations, parameters,
locals, function/branch returns and ordinal equality. A char is exactly one
UTF-16 code unit. Support documented basic escapes (newline, carriage return,
tab, backslash, quote and four-digit Unicode escapes); malformed, unfinished,
multiline and invalid-length literals produce D# source diagnostics. No
interpolation, raw/multiline strings, concatenation or culture-sensitive APIs.

## Approach

1. Write this plan before code changes.
2. Lex and decode quoted text safely while preserving source spans and enforcing
   UTF-16 char length; represent decoded text in typed bound expressions.
3. Extend type checking for string/char annotations, calls, returns and equality;
   use explicitly ordinal string comparison and safe numeric UTF-16 C# emission
   to prevent escaping, Unicode or source-injection problems.
4. Add feature execution/negative tests, example and implementation status.
5. Run bootstrap, compiler script, trusted verifier and example checks; open one
   draft PR and fix any failures on its branch. Do not merge or start #24.

AC1: execute literal/annotation/parameter/return and escape cases; reject malformed
escapes and invalid char lengths with source locations. AC2: execute non-ASCII,
quotes, slash, surrogate and hostile-looking source round trips. AC3: execute
ordinal string and char equality/inequality, including non-ASCII cases.

## Verification

Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`,
`bash .github/ai/verify.sh`, the CLI example and diff checks. If local Docker
image pull is blocked, report it and rely on complete GitHub Actions results.
Preserve existing APIs, tests, dependencies and trusted verification; only adapt
an obsolete negative fixture if the newly supported syntax directly contradicts it.

## Progress

- Implemented UTF-16 quoted string/char lexing, basic escapes, source diagnostics,
  type checking in locals/parameters/returns/calls and ordinal equality.
- C# output encodes individual UTF-16 code units numerically, avoiding source
  injection and preserving non-ASCII, embedded NUL and isolated surrogates.
  Character literals are lifted to nonconstant locals to avoid unreachable-code
  warnings when char comparisons are used as conditions.
- Added 20 feature tests: actual generated-code round trips, escape/Unicode/char
  boundaries, malformed literal/type rejection, ordinal equality, warnings as
  errors and the public binder's empty-token diagnostic.
- One obsolete FunctionCallTests input expecting a string parameter to be invalid
  now uses unsupported float; its diagnostic/location check remains intact.
  Trusted compiler tests, including string-return type mismatch, are unchanged.
- Bootstrap, CLI check/compile in the repository, locked restore, Release build
  with zero warnings/errors, 18/18 compiler tests, 116/116 feature tests, 20/20
  automation tests, documentation (21 files/59 links), and diff checks passed.
- The full local verifier reached its offline compiler stage but its pinned SDK
  image pull returned Forbidden. GitHub Actions must run the trusted verifier and
  real pilots before merge.

Commands with workspace-local SDK/cache selectors:

```bash
export PATH="/workspace/work/dotnet:$PATH"
export DOTNET_ROOT=/workspace/work/dotnet
export DOTNET_CLI_HOME=/workspace/work/dotnet-cli
export NUGET_PACKAGES=/workspace/work/nuget-packages
export npm_config_cache=/workspace/work/npm-cache
bash .github/ai/bootstrap.sh
bash scripts/verify-compiler.sh
dotnet run --project src/DSharp.Cli -- check examples/text-literals.ds
dotnet run --project src/DSharp.Cli -- compile examples/text-literals.ds artifacts/text-literals-demo
AI_REPORT_DIR=/workspace/work/issue23-verification bash .github/ai/verify.sh
node .github/ai/check-docs.mjs .
git diff --check
```

## PR

Pending user review and merge.
