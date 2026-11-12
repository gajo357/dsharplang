# Implementation plan: generic nullability and explicit Option

Base: `cbd322a` on main. This work follows the maintainer's 12 November 2026
instructions, which supersede older statements excluding Option and requiring
non-idempotent generic nullable substitution. Generic issue #30 remains open
until generated code satisfies the complete nullable contract. Explicit Option
is tracked independently in [#121](https://github.com/gajo357/dsharplang/issues/121).

## Scope

Make semantic nullability idempotent during type resolution, generic
substitution/inference and code generation; `T?` instantiated as either `T=int`
or `T=int?` must faithfully represent an int or null. Preserve closed nullable
arguments and support value, reference and Unit values through generic calls,
returns, records, fields, union payloads, matching and equality. Literal `T??`
syntax need not be accepted. Choose and document an internal representation
that does not expose source-level wrappers or conflate null with a default value.

Add standard `Option<T>` as a distinct typed union `None | Some(T)`; preserve
`None`, `Some(null)` and arbitrarily nested options as different values. Reject
`Some(null)` for a nonnullable T and implicit Option/nullable conversions.
Reuse typed binding, match coverage and structural equality; respect lexical
scope and keep standard names nonreserved. Do not adopt JSON/HTTP wire behavior.

Reconcile the four user-supplied source documents with the current, newer repo
versions, updating the sequence, implementation instructions, syntax reference,
feature backlog and affected task/status references without regressing unrelated
work. Update the related GitHub issue descriptions and track Option separately.
No dependency, workflow, trusted verifier or unrelated tests change.

Aliases and native collections have no implemented syntax/runtime in this
compiler. Exercise normalization through the underlying type/substitution model
and existing generic record/union containers, record their source-level test
coverage as pending, and do not silently invent alias or collection APIs.

## Acceptance evidence

- Nullability: value/reference/Unit T?, already-nullable substitutions,
  calls/returns/fields/nested generics, conditionals, matching, equality,
  null versus default, evaluation order and generated C# warnings-as-errors.
- Option: `None`, `Some(value)`, `Some(null)` and nested cases, exhaustive
  matching, equality, wrong nullability, no implicit conversion/default unwrap,
  standard-name shadowing and original source diagnostics.
- Verify generic coverage and nested nullable combinations; confirm every
  existing test remains unchanged and all relevant documents/tickets agree.
- Preserve evidence limits by batching new diagnostic scenarios rather than
  changing trusted report limits. Do not close #30 on syntax acceptance alone.

## Verification

With the workspace SDK/cache configured: `bash .github/ai/bootstrap.sh`,
`bash scripts/verify-compiler.sh`, CLI check/compile for examples, and
`AI_REPORT_DIR=/workspace/work/nullable-option-verification bash .github/ai/verify.sh`.
Run document link/diff checks and GitHub Actions; fix CI on the same PR.

## Representation

Generated C# carries an associated nullable CLR type parameter for an open
generic parameter only when a declaration needs it, with requirements propagated
through generic calls and nominal data declarations. For `T = int` or `int?`
the associated type is `int?`; for `T = string` or `string?` it is `string`.
Explicit boxing conversions preserve null and avoid nested CLR nullable value
types. Direct external C# callers could supply inconsistent type arguments;
the emitted signature is an implementation detail, not a supported interop
contract. Standard Option is a separate generated union with a tag and payload.

## Progress and evidence

- The original compiler acceptance suite passed 18 tests; the feature suite
  passed 348 tests, including generated C# execution with nullable warnings as
  errors. Two former rejection cases for newly valid nullable generic behavior
  were replaced by positive acceptance tests. Fresh record constructors join
  nullable inference evidence in either argument order, while previously bound
  generic values remain invariant.
- `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`, the CLI
  `check` and `compile` commands on `examples/generics.ds`, documentation
  validation and `git diff --check` passed locally.
- `.github/ai/verify.sh` passed documentation and 20 automation tests, but its
  isolated compiler phase could not download the pinned Docker image locally
  (`Forbidden`). GitHub Actions provides the isolated verification.
- Source-level type aliases and native collections are not yet implemented, so
  the requested end-to-end alias and collection tests remain pending. Keep #30
  open; #121 remains separately tracked. JSON and HTTP representation decisions
  are deferred.
