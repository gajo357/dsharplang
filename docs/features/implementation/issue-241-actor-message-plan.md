# Issue #241 — resource-free actor message transformation

Base: `a379b2880e12a916dd2ad3d81eea48df0a58616e` (`origin/main`).
Issue: https://github.com/gajo357/dsharplang/issues/241

## Scope and dependencies

Advance #241 after the merged scoped dispatcher, actor Handle and owned-resource
boundary slices #267/#269/#279/#45. In a no-dependency actor Handle whose sole
result directly awaits targeted Invoke, allow a message built from its input
using only checked immutable record construction, field access and primitive
literal nodes. Unary negative and other operators are outside this slice.
Validate the already-bound expression against a closed whitelist;
reject arbitrary function/resource calls, lambdas, capabilities, resource
scopes, statements and every unsupported shape. Preserve the exact host-scoped
dispatcher, target and type identity. Keep #241/#45/#52 open; no production
executor, storage, registration or transaction capability is introduced.

## Steps and affected files

1. Keep the existing resource-free Handle and direct-await checks. Bind and type
   check the target message normally, then permit only input references, field
   reads, immutable record construction and primitive/Unit literal nodes. Reject any
   other bound expression with the existing diagnostic before emission.
2. Add a focused feature test executing generated actor bodies with distinct
   input/output record types and scoped recording dispatchers. Check exact route,
   values, type identities, host isolation, faults and no direct actor call.
3. Migrate only the one prior negative fixture asserting the exact now-supported
   record transformation fails; preserve all unrelated negative assertions.
   Add explicit rejected effectful calls, resource/using, helper, lambda and
   unobserved forms. Test multi-file CLI check/compile.
4. Update implementation status and record actual evidence in this plan.

Files: `src/DSharp.Compiler/Compiler.cs`, one new feature test, the one
obsolete negative in `ActorBodyInvokeTests.cs`, status and this plan.

## Acceptance map

| Requirement | Evidence |
| --- | --- |
| Pure transformed input | Generated host executes field reads and record construction using the actual runtime ABI; assert qualified target/input type and exact message values. |
| Context isolation and failure | Interleave fake hosts, preserve pending completion and runtime fault identity; no direct Handle bypass. |
| Safety | Reject function/partial/lambda/resource expressions, dependencies, locals, using, wrong target/message and unawaited calls; retain the #45 boundary. |
| Regression | Existing tests pass except the specifically migrated obsolete expectation, with trusted checks unchanged. |

## Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-compiler.sh`, `bash scripts/verify-runtime.sh`, focused
feature/CLI checks, docs and `git diff --check`. Record actual evidence and
any local isolated-image limitation.

## Executed evidence

- Focused transformed-message and existing actor-body tests passed. The new
  generated host exercises nested immutable records, distinct imported input
  and target types, scoped interleaved dispatchers, exact message fields,
  result/fault propagation, unsafe-expression rejection and CLI check/compile.
  One prior negative was migrated to the now-supported positive case; its
  unrelated rejection assertions remain.
- Bootstrap succeeded. The full verifier passed documentation (131 files,
  308 local links) and all 31 automation tests, then could not pull the pinned
  MCR SDK image (`Forbidden`); hosted exact-head isolated evidence is required.
- The unchanged compiler verifier passed 18 baseline and 405 feature tests
  without skips on a short source snapshot; changed compiler and test files were
  byte-compared. Its complete raw feature TRX was 505,757 bytes under the
  unchanged 512,000-byte cap. Runtime verifier passed 2 baseline and 5 feature
  tests. Documentation validation and `git diff --check` passed.
- Unary negative values and all other operators, calls, resource scopes and
  nontrivial actor bodies remain outside this slice. Fakes do not establish an
  actor executor, delivery, storage or durability; #241 remains open.
