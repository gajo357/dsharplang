# Issue 35: value pipes and return inference

Base: `347c521` on main. Dependencies [#33](https://github.com/gajo357/dsharplang/issues/33),
[#20](https://github.com/gajo357/dsharplang/issues/20), and adopted
[DEC-02](DEC-02.md) are merged. DEC-01 remains unadopted and is not required
for this issue.

## Scope and approach

Implement `|>` as a left-associative value pipe at DEC-02's lowest precedence.
It passes the left value exactly once to an ordinary one-argument function,
function value or lambda, with normal lexical lookup, type checking and
evaluation order. It does not curry, await, unwrap Result, destructure tuples or
perform failure-aware composition. Preserve early returns, exceptions and
short-circuiting.

Permit omitted return annotations for ordinary non-contract helpers only when
their own dependency component is provably acyclic and the checked body and all
reachable explicit returns determine a unique compatible result. Infer from
callees in dependency order, never from callers; reject unannotated recursive
components, indirect calls and unresolved dependencies that prevent proof of
acyclicity. Explicit annotations retain current behavior. Handler/interface and
async forms remain unsupported and cannot be inferred as a side effect.

Preserve existing compiler APIs, feature tests, evaluation semantics, the
capture/data boundary, pinned SDK and trusted verification. Avoid making new
library, async, tuple or numeric contract decisions. If an old negative test
asserts syntax or behavior that this issue explicitly requires, replace only
that contradictory assertion with positive execution coverage and document it.

## Acceptance map

- AC1: execute named, function-valued and lambda pipes, chains, precedence and
  nested expressions; verify left operand evaluated once, normal argument
  order, short circuit and nearest return behavior.
- AC2: execute inferred primitive/nullable/Result/function results and forward
  acyclic dependencies, checking all reachable exits. Diagnose underconstrained,
  recursive, mutually recursive, indirect-call and invalid inference cases.
- AC3: diagnose incompatible input, arity and non-callable pipe targets; verify
  Result values are passed as values and not unwrapped; async/tasks and tuples
  remain unsupported rather than implicitly transformed.
- Preserve existing source spans, generic and closure behavior, inference with
  no caller evidence, capture restrictions and generated C# warnings-as-errors.

## Verification

Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`,
`AI_REPORT_DIR=/workspace/work/issue-35-verification bash .github/ai/verify.sh`,
CLI check/compile on an example, documentation and diff checks, and GitHub
Actions. Record actual results and fix CI on the same PR branch.

## Progress

- AC1: generated execution covers static and dynamic targets, lambdas, chains,
  generic/nullable values, precedence, single evaluation, source order and
  short-circuit/early-return behavior.
- AC2: inferred primitive, generic, nullable and Result-returning helpers run
  with forward dependencies and reachable explicit returns. Diagnostics cover
  cycles, indirect calls, missing evidence and distinct explicit exit types.
  A separate checked-type dependency graph prevents declaration-order-dependent
  inference; cyclic checked types require an annotation. Match reachability
  treats uncertain coverage conservatively and requires an annotation when
  uncertain explicit exits would otherwise affect inference.
- AC3: incompatible pipe targets and input types are diagnosed; generated
  execution passes Result values unchanged. Tuple/task syntax remains outside
  the implemented language. Existing tests remain unchanged.
- Bootstrap, `bash scripts/verify-compiler.sh` (18 baseline and 357 feature
  tests), CLI check/compile of the example, documentation and diff checks
  passed locally. Generated C# tests enable nullable warnings as errors.
- `bash .github/ai/verify.sh` passed documentation and 20 automation tests;
  the isolated compiler stage could not download its pinned image locally
  (`Forbidden`). GitHub Actions runs the unchanged isolated verification.
- The first GitHub compiler run passed all tests but exceeded the test-report
  size limit. The new test's reporting identifiers were shortened without
  removing assertions or changing existing tests or the verifier.
