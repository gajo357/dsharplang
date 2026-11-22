# Issue #264 — HTTP contracts for plain and Unit results

Issue: https://github.com/gajo357/dsharplang/issues/264
Parent: https://github.com/gajo357/dsharplang/issues/52
Base: `dc956f5ca4446c6ce37eaf57eae2a90a69326fd5` (`origin/main`).

## Scope and decisions

Extend the checked direct HTTP binder/mapper contract from #262 to ordinary
immutable Handle results and Unit. #239, #51, #15 and #262 are merged/closed.
The parent #52 stays open for remaining HTTP preprocessing/Dispatch and its
codec and dispatcher acceptance evidence. This compiler-only slice does not
register routes, execute callbacks, choose statuses, encode data or implement
transport, authentication, dispatch or runtime behavior. #54, #78, #81–#85
and #241 retain those responsibilities.

For a plain/Unit result choose the existing contextual descriptor with three
function references: `trigger Http<Response>(decode, mapBindingError, mapValue)`.
`decode` remains an actual visible ordinary non-generic one-argument function
with an explicit logical intrinsic `Result<HandleInput, BindingError>` return;
it may be synchronous or async. Both explicit non-generic synchronous mappers
take exactly one argument and return the selected immutable Response:
`BindingError -> Response` and `HandleResult -> Response`. No error type,
business status, callback or transport value is inferred. Callable names obey
the #262 source-scope/import/actor-shadowing rules. No descriptor creates a
route or invokes a mapper.

Intrinsic nonnullable `Result<Value, BusinessError>` Handle results retain
#262's four-reference contract and both value/business-error mappings. Reject
the three-reference form for intrinsic Result and the four-reference form for
plain/Unit. A nullable intrinsic Result remains unsupported rather than being
reinterpreted as plain. A user-declared nominal type named Result is ordinary
data and may be mapped as one plain value; a binder's Result must still be the
compiler intrinsic. Represent the plain form with an explicit metadata variant
that has no business-error type, mapper, identity or span, never a fabricated
value. Preserve the existing Result-only public metadata and source behavior.

## Steps, files and interfaces

1. Extend `src/DSharp.Compiler/Compiler.cs` to parse three- or four-reference
   variants and validate their arity against the scoped Handle identity; the
   new plain metadata has no business-error fields. Keep the prior Result-only
   public records unchanged. Update `src/DSharp.Compiler/MultiFileResolver.cs`
   to resolve the plain variant's references with the same origin and shadowing
   rules.
2. Add only new `tests/features/HttpPlainContractTests.cs` and
   `examples/http-plain-contract.ds`. Preserve all old tests and update status
   only for observed compiler checking, not HTTP response behavior.
3. Run the checks below, fix any in-scope failures on this branch and publish a
   single draft PR closing this child only. CI repairs stay on the same PR.

## Acceptance-to-test mapping

| Criterion | Planned evidence |
| --- | --- |
| Plain/Unit contracts and metadata | Actual sync/async binder plus typed binding-error and plain/Unit value mappers; response, actor result, function identity and source spans retained in the plain variant with no business-error fields. |
| Preserve Result arity and identity | Intrinsic Result still requires four references; plain/Unit rejects four; nullable intrinsic Result rejects; a distinct user Result is treated as plain. Binder Result must remain intrinsic. |
| Errors and isolation | Wrong binder, mapper types/returns/arity, unknown/ambiguous/inaccessible and actor-shadowed names; imported nominal mismatch, no implicit bindings and direct-actor-call rejection. |
| CLI and example | Check/compile the example and imported sources, reject an invalid descriptor without output and build generated C# only; no callback execution or HTTP transport claim. |

## Verification commands

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-compiler.sh`, `bash scripts/verify-runtime.sh`, focused
feature tests, CLI checks and `git diff --check`. Record actual results and the
local image-pull limitation; exact-head hosted isolated verification is required
before merge. If the long local checkout path exceeds the existing TRX guard,
rerun the unchanged verifier from a byte-checked short snapshot. Preserve all
existing tests and verification limits.

## Results

- Bootstrap succeeded. Documentation validation passed for 120 files and 308
  links, and all 31 automation tests passed. The local isolated verifier could
  not pull the pinned MCR SDK image because the registry returned `Forbidden`.
- The unchanged compiler verifier passed 18 compiler tests and 396 feature
  tests from a byte-checked short snapshot; its feature TRX was 509,900 bytes,
  below the existing 512,000-byte limit. Runtime verification passed 2 baseline
  and 1 feature test from that snapshot. Three duplicate-descriptor cases were
  subsequently added to an existing grouped test; the final focused run passed
  all 14 HTTP, Queue, Event and Cron test groups including those cases.
- CLI checks and compilation passed for the new example and imported sources;
  generated C# built successfully. A missing mapper was rejected with DS2046
  and produced no output directory.
- Hosted checks on the draft's exact head are needed before merge. No HTTP
  execution or runtime transport behavior was exercised or claimed.
