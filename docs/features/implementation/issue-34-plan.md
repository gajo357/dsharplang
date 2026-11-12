# Issue 34: immutable closure captures

Base: `2f358bc` on main. Dependencies [#33](https://github.com/gajo357/dsharplang/issues/33)
and [#25](https://github.com/gajo357/dsharplang/issues/25) are merged. DEC-01
is still unadopted and is not required by this issue.

## Scope and approach

Enable lexical capture of supported immutable data values by lambdas, including
parameters, earlier locals and pattern bindings. A returned closure must retain
the values from the invocation that created it; multiple invocations and nested
closures must remain independent. Keep the existing no-shadow and definite-scope
rules, function/data boundary and function equality restrictions. Capturing
function values, mutable builders, scoped resources, services or worker
capabilities is outside this first slice and must be rejected, including through
unconstrained generic arguments and nested nominal wrappers. Do not add closure
serialization, async lambdas or an interop contract.

Represent captures explicitly in typed binding, preserving lexical identity,
transitive nested captures and the nearest lambda return/Result boundary. Enforce
immutable-data eligibility through generic calls and forwarding. Generate a
per-creation closure environment whose captured state cannot be modified through
a public API; preserve evaluation order and existing capture-free behavior.

The #33 feature suite contains negative cases that explicitly rejected immutable
captures; the selected issue now requires those cases to succeed. Replace only
the directly contradictory negative cases with positive execution coverage,
retaining all unrelated restrictions and existing tests.

## Acceptance map

- AC1: execute captured parameters, locals, nullable values and immutable
  record/union/Option data; create multiple closures from separate calls and
  verify independent values. Validate generic data restrictions.
- AC2: reject function/capability/builder/resource capture and function-valued
  record/union payloads, including through generic forwarding. Inspect emitted
  state to ensure no publicly accessible mutable closure fields.
- AC3: invoke closures after creators return, including nested/transitive and
  match scopes; exercise early return and Result propagation within the nearest
  lambda and preserve laziness/evaluation order.
- Preserve name resolution, no-shadow rules, error spans, generated C# with
  warnings as errors, preexisting nullable/Result/generic behavior and all
  unrelated tests.

## Verification

Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`,
`AI_REPORT_DIR=/workspace/work/issue-34-verification bash .github/ai/verify.sh`,
CLI check/compile of an example, documentation and diff checks, and GitHub
Actions. Record actual results; fix any CI failure on the same PR branch.

## Progress

- AC1: generated execution covers independent closures, parameter/local,
  record/union/Option and nested captures, plus generic `T?` captures for
  value, reference and already-nullable types.
- AC2: diagnostics reject function captures directly and through generic
  forwarders, as well as function-valued data. Reflection checks confirm the
  generated closure targets have private readonly fields and no public setters.
- AC3: returned closures execute after their creator returns, including nested
  and match-bound captures; tests cover lexical lookup, nearest-lambda return
  and Result propagation. Three obsolete negative cases were replaced by
  positive execution coverage; other existing restrictions were retained.
- Bootstrap, `bash scripts/verify-compiler.sh` (18 baseline and 356 feature
  tests), CLI check/compile of the example, documentation checks and diff checks
  passed locally. Generated tests enable nullable warnings as errors.
- `bash .github/ai/verify.sh` passed documentation and 20 automation tests;
  its isolated compiler stage could not download the pinned image locally
  (`Forbidden`). GitHub Actions runs that unchanged isolated verification.
