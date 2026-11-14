# Implementation plan: #30 — Generic data and functions

Issue: https://github.com/gajo357/dsharplang/issues/30
Base: `aa049df` on main. Prerequisites DEC-02 #12, functions #20 and union
types #26 are merged; DEC-02 was explicitly adopted in #113.

## Scope and implementation

Support declared type parameters on immutable records, unions and functions,
explicit type arguments and unambiguous call inference. Parse complete generic
call shapes syntactically as specified by DEC-02, including nested type arguments;
retain comparison parsing when that shape is absent. Resolve and substitute
types recursively, check field/case/call arguments, enforce generic arity and
reject unbound or conflicting parameters before emission. Preserve nominal
identity across different declarations and substitutions.

Infer only substitutions uniquely determined by supported argument and expected
types through exact structural matching; never guess from ambiguous/conflicting
evidence. Keep generic constraints, variance, aliases and arbitrary operations
on unconstrained type parameters unsupported. Preserve current immutable,
nullable, match, evaluation-order and diagnostic behavior. Where a combination
depends on separately unimplemented syntax or semantics, report it explicitly
instead of silently selecting a new contract.

Update compiler syntax/types, binder, substitution/inference, match coverage and
managed code generation as needed, add new feature tests and an example, plus
this plan and implementation status. Preserve existing APIs, all existing tests,
dependencies, project files and trusted verification. DEC-02 leaves nullable
eligibility to separate semantic checking: an open, unconstrained `T?` remains
unsupported until its eligibility and representation across generic forwarding
are resolved. Closed nullable type arguments, such as `Box<int?>`, are supported.

## Acceptance evidence

- AC1: parse declarations/explicit applications and execute identity<T> with
  explicit and inferred types; verify DEC-02 syntactic disambiguation and nested
  closers without altering comparisons.
- AC2: reject wrong generic/value arity, incompatible substitutions, unbound or
  ambiguous parameters, duplicate declarations and invalid substituted fields,
  with original D# locations.
- AC3: execute nested generic records/unions, constructor and function inference,
  structural equality, matching and distinct nominal instantiations.
- Regressions: lexical scopes, nullable data, evaluation/early return, safe names,
  generated C# warnings-as-errors and unchanged existing test suites.

## Verification

With the workspace SDK/cache configured:

```bash
bash .github/ai/bootstrap.sh
bash scripts/verify-compiler.sh
dotnet run --project src/DSharp.Cli -- check examples/generics.ds
dotnet run --project src/DSharp.Cli -- compile examples/generics.ds artifacts/generics-demo
AI_REPORT_DIR=/workspace/work/issue30-verification bash .github/ai/verify.sh
git diff --check
```

Open one draft PR; fix CI on its branch and leave merge to the user. Do not start
another issue before review.

## Progress

- Implemented generic declarations, type substitution and inference, nested
  generic records/unions, syntactic call disambiguation, pattern coverage and
  structural data equality. Internal type-parameter identities preserve lexical
  shadowing without changing source syntax or exposing internal symbols in
  diagnostics. Existing tests remain unchanged.
- Added execution and diagnostic tests for the ACs, inference order, nullable
  concrete type arguments, generic name shadowing, nested data, evaluation
  order, original spans, comparison disambiguation and generated C# with warnings
  as errors. Open `T?` and operations synthesizing it remain diagnosed; deciding
  how nullable eligibility propagates through generic parameters is outstanding.
- Bootstrap, Release build with zero warnings, 18 baseline and 321 feature tests,
  CLI example check/compile, 20 automation tests and documentation checks passed.
  The local full verifier could not download its pinned SDK image (`Forbidden`);
  GitHub Actions runs the unchanged containerized verifier.

## PR

Draft PR for user review. Keep #30 open while the open-`T?` boundary remains
unresolved; fix any CI failures on this branch.

## Completion plan after collections and async

**Base:** `e4f23c60e31701f7e81b0e7ff5f6d62ef2aca5cd`. Earlier entries above are
historical: subsequent PRs implemented the associated nullable CLR projection
and immutable collections. Issue #30 stays open until all acceptance criteria are
verified with executed generated code; acceptance of `T?` syntax is not enough.

1. Audit current generic calls, returns, fields, unions and List/Map collections
   against AC1–AC6, including null and present values for `int`, `int?`, `string`
   and `string?`, nested optional values, and closed `Box<int?>` arguments.
2. Add a real D# example and new feature tests that compile and execute the
   complete matrix with warnings treated as errors. Include multi-file calls,
   generic inference/substitution, and negative diagnostics where appropriate.
3. Fix any demonstrated in-scope compiler defects; preserve existing tests and
   avoid changing nullable-vs-Option semantics. Aliases are currently unsupported;
   AC4 requires alias normalization only if aliases are added.
4. Update implementation status and the representation explanation precisely.
   Use `Closes #30` only if all applicable criteria pass, including calls and data.

Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`, and
`bash .github/ai/verify.sh`; check, compile and execute the D# example. Record
actual results and fix any CI failure directly on this issue's PR branch.

### Completion evidence

- AC1–AC3: the existing `GenericTests` execute declared generic functions,
  explicit and inferred calls, nested records/unions and nominal identity, and
  reject incorrect arity, unbound types and incompatible substitutions.
- AC4–AC6: the new `GenericNullableMatrixTests` execute null and present values
  for all four requested substitutions through generic calls and returns,
  record fields, union payloads, `List<T?>`, `Map<string,T?>`, and async
  forwarding. They distinguish `None`, `Some(null)` and nested options, retain
  closed `Box<int?>`, and reject non-nullable/null and invariant-type mismatches.
  Compiling generated C# with warnings as errors also rules out an invalid
  `Nullable<Nullable<int>>` for the exercised paths. Type aliases remain
  unsupported, so the conditional alias rule is not yet applicable.
- The backend's associated nullable CLR parameter and its fixed-point propagation
  through generic functions and nominal data are documented in the implementation
  status. The D# matrix example checks and compiles; its generated `answer()`
  executes and returns 42.
- `bash .github/ai/bootstrap.sh` passed. `bash scripts/verify-compiler.sh`
  passed 18 compiler and 374 feature tests. `bash .github/ai/verify.sh` passed
  documentation and all 25 automation tests; its isolated compiler step could
  not download the pinned SDK image locally (`Forbidden`). GitHub CI must run
  that isolated step.

Local commands used the pinned 11.0.100 SDK on `PATH`,
`XDG_RUNTIME_DIR=/tmp/dsharp-xdg-verify`,
`DOTNET_CLI_HOME=/tmp/dsharp-dotnet11-home`,
`NUGET_PACKAGES=/tmp/dsharp-dotnet11-packages`, and
`npm_config_cache=/tmp/dsharp-npm-cache` for writable sandbox caches.
