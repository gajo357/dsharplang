# Issue #275 — scoped Invoke through awaited functions

Base: `5995c2c9605f474a944933f5d41ffb152600541f` (`origin/main`).
Issue: https://github.com/gajo357/dsharplang/issues/275

## Scope and steps

Advance #241's scoped dispatcher propagation for directly awaited, fully applied
ordinary async functions with explicit return types. Determine the transitive
set from resolved bound-call identities using a fixed point, including recursion
and modules. Pass the same host-supplied dispatcher through every affected call
and existing export wrapper. Unaffected signatures remain unchanged. Reject
unsafe paths, escaping function values, callbacks, actor helpers, lambdas and
unobserved tasks; receipt-returning Dispatch and publication propagation remain
for later slices. Do not claim delivery, durability or complete #241/#52.

1. Inspect checked call/await representations and function origins; compute
   effects after binding and validate uses with their lexical context.
2. Emit the hidden dispatcher argument for validated affected calls.
3. Add generated-code tests with interleaved scoped dispatchers, transitive and
   multi-module calls, recursion, route/return/evaluation checks and negatives.
   Migrate only the two obsolete Invoke call-rejection fixtures that this issue
   intentionally enables, preserving their other rejection coverage.
4. Update status and this plan with actual evidence.

## Files and dependencies

`src/DSharp.Compiler/Compiler.cs`, a new `tests/features/` test, the two narrow
fixture updates in `ActorInvokeEntryTests.cs`, this plan and implementation
status. Prerequisites #267, #269, #271 and #273 are merged; #241 and #52 remain
open. No new runtime API, dependency, syntax or wire protocol is introduced.

## Acceptance evidence

- Explicit typed async chain, transitive/reordered/recursive and qualified calls:
  compile and execute against a recording dispatcher; verify route, exact value,
  argument evaluation and isolation across interleaved hosts.
- Existing unaffected exports and ordinary shadowed calls: compile and retain
  their signatures/behavior.
- Captures, partials, lambdas, unsupported actor/extension/callback paths and
  unawaited calls: negative compiler diagnostics, including transitive effects.
- Existing Invoke, Dispatch and publication suites retain all other assertions.

## Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-compiler.sh`, `bash scripts/verify-runtime.sh`, focused
feature tests and `git diff --check`. Record limitations and exact outcomes.

## Decisions

No additional public contract decision is required for this bounded internal
propagation. Inferred receipt forwarding and production runtime behavior are
explicitly excluded.

## Executed evidence

- Bootstrap passed. The full verifier passed documentation and all 31 automation
  tests, then could not pull the pinned MCR image (`Forbidden`); its isolated
  compiler/runtime stages require hosted evidence.
- The unchanged compiler verifier passed 18 baseline and 400 feature tests with
  no skips. It ran on a source snapshot byte-compared to the changed compiler and
  test files, using the same short test-output layout as hosted Compiler; the
  complete feature TRX was 503,469 bytes under the unchanged 512,000-byte cap.
- Runtime verifier passed 2 baseline and 5 feature tests. Focused existing
  Invoke/Dispatch/publication groups and the new scoped test passed; the new
  generated host exercises qualified transitive calls, recursion, interleaved
  fake dispatchers, pending completions, exact routes, faults and evaluation.
  Unsafe captures, partials, lambda/actor/extension uses, unawaited calls,
  transitive HTTP callback use and receipt forwarding remain rejected.
- Documentation link validation and `git diff --check` passed. Earlier local
  attempts encountered sandbox socket and .NET home permissions; rerunning with
  the necessary bounded permissions produced the successful results above.
