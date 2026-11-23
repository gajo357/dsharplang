# Issue #241 — pure local bindings before actor Invoke

Base: `ee0562e245b58079b8337474df9255402714a126` (`origin/main`).
Issue: https://github.com/gajo357/dsharplang/issues/241

## Scope and prerequisites

Advance #241 after merged resource-free actor Invoke and immutable message
transformation slices #279/#283 and the #45 owned-resource boundary. Permit a
no-dependency actor Handle to make simple immutable local bindings before its
final directly awaited targeted Invoke, only when each initializer and the
message are proven from the Handle input, earlier proven locals, declared record
fields/construction and literal nodes. Preserve scoped dispatch, exact target
typing, the #45 resource boundary and all unsupported forms. Keep #241, #45 and
#52 open; no production executor, registration or durability is implied.

## Steps and files

1. Allow only simple let statements in the checked resource-free Handle shape.
   Check their bound expressions in order against a closed pure-data whitelist,
   allowing references only to the input and earlier proven bindings. Check the
   final message with the same data-only rule. Reject calls, operators, mutable
   builders, resources, lambdas, using, control-flow statements and other forms.
2. Exercise generated actor bodies against the real typed runtime ABI and two
   scoped recording hosts: chained locals and transformed message, qualified
   target identity, independent pending results, faults and unchanged plain ABI.
3. Migrate only old negative fixtures that reject the exact newly supported pure
   let form; preserve all unrelated assertions and add negatives for effectful
   initializers, forward names, using/dependencies, helpers and escaping calls.
4. Update implementation status and record actual evidence; run focused/CLI and
   required verification. Do not claim general purity, cleanup or actor delivery.

Files: compiler, one new feature test, exact obsolete fixture migration(s),
status and this plan. No runtime API, dependency or verifier change.

## Acceptance map

| Requirement | Evidence |
| --- | --- |
| Pure scoped locals | Generated host executes ordered chained input-derived locals and target record, preserving exact values and scoped dispatcher. |
| Host isolation | Independent pending fake dispatchers, exact result and fault, stable target route and unchanged unaffected Handle signature. |
| Fail closed | Negative tests for effectful/lambda/resource initializers, direct Handle bypass, forward references, non-let statements, dependent actors and unawaited calls; preserve #45. |
| Verification | Bootstrap, full verifier, compiler/runtime verifiers, focused feature and CLI tests, docs and `git diff --check`; record actual results and image limitation. |

## Executed evidence

- Focused pure-local, actor-body and transformed-message groups passed. Generated
  hosts exercised chained immutable locals, distinct imported input/target types,
  scoped interleaved dispatchers, exact message values, results, faults,
  unsafe-initializer rejection and CLI check/compile. Two obsolete alias
  negatives were narrowly migrated to positives; unrelated assertions remain.
- Bootstrap succeeded. The full verifier passed documentation (132 files,
  308 local links) and all 31 automation tests, then could not pull the pinned
  MCR SDK image (`Forbidden`); hosted exact-head isolated evidence is required.
- The unchanged compiler verifier passed 18 baseline and 406 feature tests
  without skips on a short source snapshot; changed compiler and test files were
  byte-compared. Complete raw feature TRX was 506,836 bytes under the unchanged
  512,000-byte cap. Runtime verifier passed 2 baseline and 5 feature tests.
  Documentation validation and `git diff --check` passed.
- The host dispatcher is a recording fake, not a production actor executor;
  resource scopes, arbitrary effects and other statement forms remain rejected.
