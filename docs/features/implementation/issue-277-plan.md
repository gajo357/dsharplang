# Issue #277 — target triggered actors with Invoke and Dispatch

Base: `67f96b9d352dc333dfa550e49df51c899adedc64` (`origin/main`).
Issue: https://github.com/gajo357/dsharplang/issues/277

## Scope and prerequisites

Prove the #52 AC3 integration boundary using the merged Queue/Event contract
children #256/#258, runtime seam #267 and targeted Invoke/Dispatch children
#269/#271. Compile a triggered actor with a business Result and explicitly
invoke and dispatch to the same resolved target. This is test evidence, not a
transport, subscription, codec or production dispatcher. Keep #52 and #241
open. The #78 codec, #54 registration and #61/#69 runtime remain separate.

## Steps and files

1. Add a single grouped feature test under `tests/features/` using imported
   actor/message modules, retained trigger metadata, two typed host entries and
   a controlled dispatcher against the real `DSharp.Runtime` ABI.
2. Execute generated code and assert the same route identity, once-only
   argument evaluation, exact business Error propagation, independently pending
   fake Dispatch acceptance and exact distinct invocation receipt.
3. Add wrong input/target and direct Handle bypass negative cases and exercise
   supported CLI check/compile. Preserve existing tests and implementation;
   fix only a demonstrated in-scope defect if one appears.
4. Update implementation status and this plan with actual evidence, without
   claiming parent completion or real delivery.

## Acceptance map

| Requirement | Evidence |
| --- | --- |
| Triggered actor and import identity | Compile checked Queue/Event target; inspect retained metadata and execute qualified/unqualified host entries. |
| Typed Invoke and Dispatch | Real ABI recording host observes the same route and once-only messages; the exact business Error and fake acceptance receipt propagate independently. |
| Invalid and bypass cases | New negative tests for wrong message/actor and direct Handle, plus CLI check/compile. |
| Scope | Explicitly retain separate codec, registration and runtime-delivery work. |

## Verification

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-compiler.sh`, `bash scripts/verify-runtime.sh`, the focused
feature test and `git diff --check`. Record actual outcomes, including any
isolated image pull limitation and hosted exact-head checks.

## Executed evidence

- The focused Release feature test passed: it compiled the imported
  Queue/Event actor, inspected its metadata, executed the generated host using
  the real runtime ABI and a recording dispatcher, and exercised invalid
  contracts and multi-file CLI check/compile.
- Bootstrap passed. The full verifier passed documentation (127 files, 308
  local links) and all 31 automation tests, then could not pull the pinned MCR
  SDK image (`Forbidden`); isolated compiler/runtime verification requires
  hosted exact-head evidence.
- The unchanged compiler verifier passed all 18 baseline and 401 feature tests
  without skips on a source snapshot; the new test was byte-compared with the
  branch version. Its complete raw feature TRX was 509,407 bytes under the
  unchanged 512,000-byte limit. Runtime verification passed 2 baseline and 5
  feature tests. `git diff --check` and documentation validation passed.
- The dispatcher is a controlled fake; these results do not establish durable
  acceptance, actual publication, subscription registration or codec support.
