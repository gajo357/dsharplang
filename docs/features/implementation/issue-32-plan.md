# Issue 32: postfix Result propagation

Base: `dcbd069` on `main`. Dependencies [#31](https://github.com/gajo357/dsharplang/issues/31)
and [#24](https://github.com/gajo357/dsharplang/issues/24) are merged. The
remaining alias and collection coverage in #30 is outside this issue.

## Scope and design

Parse postfix `?` with call/member precedence and distinguish it from `??`,
`?.`, and nullable type syntax. Bind it only for the intrinsic standard
`Result<T,E>` when the nearest enclosing function returns `Result<U,E>` with
the same error type. The success expression has type `T`, retaining nullable,
generic and Unit payloads. A user-declared `Result` does not acquire intrinsic
semantics. No error conversion or lambda/async syntax is introduced.

Lower through typed bound nodes and the existing nearest-function return
mechanism: evaluate the operand once, yield its Success payload or return the
same Error payload from the enclosing function. Preserve evaluation order,
short-circuiting, and handling of nested expressions and explicit returns.

## Acceptance map

- AC1: generated execution for Success/Error, single evaluation, nested
  arguments/operators/branches and skipped later side effects or faults.
- AC2: source diagnostics for non-Result operands, enclosing non-Result types,
  mismatched errors, nullable/Option operands and user-defined Result names.
- AC3: execute nullable, Unit, generic and nested successes; verify errors do
  not escape past the nearest function, with no silent error conversion.
- Check parse precedence and source spans, existing Result/nullable behavior,
  name collisions, warnings-as-errors, and evaluation order.

## Files and verification

Compiler syntax, binder and emitter under `src/DSharp.Compiler/`; new feature
tests under `tests/features/`; a small example and the implementation status.
Do not alter existing tests, trusted checks, dependencies or workflow files.

Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`,
`bash .github/ai/verify.sh`, CLI check/compile of the example, documentation
and diff checks, and GitHub Actions. Record results and any limitations here.
Fix CI failures on the same PR branch.

## Progress

- AC1: generated execution confirms Success values, single operand evaluation,
  short-circuiting, nested expressions and skipped later faulting arguments.
- AC2: grouped source-diagnostic tests reject non-Result and nullable operands,
  non-Result enclosing returns, user-defined Result, and differing error types.
- AC3: generated execution covers nearest-function handling, generic, nullable,
  Unit and Option payloads, nested Result values, matches and guards. The same
  error type is required; no automatic error conversion is performed. Async and
  lambda forms remain unsupported.
- `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh` (18 baseline
  and 353 feature tests), CLI check/compile of `examples/result-propagation.ds`,
  documentation validation and `git diff --check` passed locally. Generated C#
  tests enable nullable warnings and treat warnings as errors.
- `bash .github/ai/verify.sh` passed documentation and 20 automation tests, but
  its isolated compiler phase could not download the pinned image locally
  (`Forbidden`). GitHub Actions runs that isolated verification.
