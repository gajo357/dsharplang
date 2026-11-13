# Issue 138: Migrate the obsolete pipe arity fixture

Base: `bc66245` on main. This is the narrowly scoped baseline setup task
[#138](https://github.com/gajo357/dsharplang/issues/138) required before
ordinary-function partial application [#135](https://github.com/gajo357/dsharplang/issues/135).
The merged [specification](../DSharp_Pipes_and_Logging.md) makes `1 |> add(2)`
valid once implemented; the existing grouped negative fixture currently rejects it.

## Scope and implementation

- Replace only that fixture input with `1 |> add(2, 3)`, where the completed
  call is an integer and therefore not a callable pipe target under either
  current or target semantics. Retain the grouped assertion and diagnostic
  checks and all other cases. Do not alter the compiler, dependencies, project
  manifests, workflow, verifier or unrelated tests.
- This setup issue explicitly scopes the single baseline migration; it does not
  allow general edits to immutable tests. Positive execution of the original
  expression belongs to #135 and cannot pass before its implementation.
- The trusted compiler harness stages its baseline and overlays candidate feature
  `.cs` files; this one reviewed change is visible without changing the harness.

## Inventory and acceptance evidence

- `PipeAndReturnInferenceTests.cs` contains the conflicting `1 |> add(2)` case.
  The adjacent `42 |> takes` remains invalid because the enclosing function
  requires an integer while a partial pipe would yield a function.
- Other zero-argument rejection examples in `FunctionCallTests.cs` and
  `GenericTests.cs` retain integer return expectations and remain negative
  under the new rules; preserve them and verify diagnostic spans in #135.
- Run the existing grouped pipe feature test, then `bash .github/ai/bootstrap.sh`,
  `bash scripts/verify-compiler.sh`, `AI_REPORT_DIR=/workspace/work/issue-138-verification bash .github/ai/verify.sh`,
  documentation validation and `git diff --check`. Record results below and
  use the same PR branch for any CI repair.

## Results

- The focused pipe test passed, as did bootstrap and the compiler verifier
  (18 baseline and 358 feature tests).
- Documentation checks and all 20 automation tests passed. The full verifier's
  isolated compiler stage could not pull its pinned Docker image locally
  (`Forbidden`); GitHub Actions runs the unchanged stage.
- Diff checks passed. The only fixture change is the one specified input; the
  compiler and trusted verification scripts are unchanged.
