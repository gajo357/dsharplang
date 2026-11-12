# Implementation plan: #26 — Discriminated unions and nominal wrappers

Issue: https://github.com/gajo357/dsharplang/issues/26
Base: `728bbb8` on main; prerequisite records #25 merged in #114, and DEC-02
was adopted and merged in #113.

## Scope and dependencies

Add public, immutable union declarations with leading `|` cases and zero or
more positional payloads; construct cases qualified or by an unambiguous bare
name. Preserve union identity for single-case wrappers and allow supported
scalar, Unit, record and nested-union payloads. Case values compare structurally
within the same union and reject mutation. Declarations and their constructors
support forward references and the existing expression/control-flow features.

Follow adopted DEC-02 lookup: same-name cases across unions, and a function and
case with the same bare name, are ambiguous even with an expected type; explicit
`Union.Case` resolves the case. Local values retain precedence over qualification.
Expected type can validate or supply context for an otherwise unambiguous case;
it does not silently resolve ambiguous lookup. Cases do not imply pattern
matching; #27 covers match and guards. Generics, nullable types, unimplemented
primitives, mutable/foreign or function-valued payloads remain out of scope.

Implementation: extend lexer/parser syntax and documentation metadata, collect
case and type signatures, resolve and type-check constructions with source spans,
and emit immutable public values with typed payloads and structural equality.
Preserve existing public constructors/APIs, evaluation order and source diagnostics.
Update `src/DSharp.Compiler/Compiler.cs`, add new `tests/features/` cases and an
`examples/` program, plus this plan and implementation status. No dependencies,
existing tests, project, workflow or verifier edits. No unresolved decision gates
remain for this bounded slice.

## Acceptance evidence

- AC1: parse zero/one/multiple-payload cases and test qualified and bare lookup,
  forward declarations, case ambiguity, function collisions and local precedence.
- AC2: reject incorrect arity/types, unknown and duplicate cases, unsupported
  payloads, and assignment between distinct nominal wrappers with source spans.
- AC3: execute constructions and structural equality across nested record/union
  payloads, verify distinct tags compare unequal, preserve left-to-right and
  single evaluation, and diagnose attempted mutation before emission.
- Regressions: comments/docs, name collisions, control flow, early returns,
  baseline compiler behavior and generated C# under warnings-as-errors.

## Verification

Configure the workspace SDK/cache, then run:

```bash
bash .github/ai/bootstrap.sh
bash scripts/verify-compiler.sh
dotnet run --project src/DSharp.Cli -- check examples/unions.ds
dotnet run --project src/DSharp.Cli -- compile examples/unions.ds artifacts/unions-demo
AI_REPORT_DIR=/workspace/work/issue26-verification bash .github/ai/verify.sh
git diff --check
```

Open one draft PR and fix any CI failures on that branch; do not merge or start
the next issue before user review.

## Progress

- Implemented immutable union cases, nominal wrappers, positional payloads,
  qualified/bare resolution, source diagnostics and structural D# equality,
  including record/union payload nesting and documentation on union declarations.
  Ambiguous record-constructor/case names require renaming the record constructor
  in this subset because module-qualified record construction is not implemented.
- Added execution and rejection tests covering the ACs, forward references,
  ambiguous names despite contextual types, lexical precedence, C# names,
  evaluation order and early returns. Existing tests remain unchanged.
- Bootstrap, Release build with zero warnings, 18 baseline and 194 feature tests,
  CLI example check/compile, 20 automation tests and documentation checks passed.
  The full local verifier could not download its pinned SDK image (`Forbidden`);
  GitHub Actions runs the unchanged containerized verifier.

## PR

Draft PR pending user review and merge. Fix any CI failures on this branch.
