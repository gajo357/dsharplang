# Issue #262 — typed direct HTTP binder and mapper contract

Issue: https://github.com/gajo357/dsharplang/issues/262
Parent: https://github.com/gajo357/dsharplang/issues/52
Base: `53ab2ffec1b0c7f0e75457f9368cad86e6904cc0` (`origin/main`).

## Scope and decisions

Implement the first, bounded HTTP compiler-contract child in the merged
[parent plan](issue-52-plan.md). #239, #51, #15, #256, #258 and #260 are
merged/closed. #15's DEC-05 selects typed, explicit error boundaries but leaves
transport/library names and mapping syntax provisional. This slice checks real
function signatures; it is not an HTTP server, route registration, decoder,
authorization mechanism or response encoder.

Choose an actor-local contextual descriptor
`trigger Http<Response>(decode, mapBindingError, mapSuccess, mapBusinessError)`.
Each argument names an existing ordinary module function; qualified/imported
names resolve in the actor's source file. `Http` and `trigger` are not globally
reserved. The descriptor is inert metadata and does not invoke functions or
register a route. A single actor may declare at most one HTTP descriptor in this
bounded slice. Preserve source spans, resolved identities and actor result.

This first slice supports only a Handle returning the intrinsic
`Result<Value, BusinessError>` and an explicitly typed, non-generic, one-argument
direct binder with logical result `Result<HandleInput, BindingError>`; a normal
synchronous binder or `async fn` with that logical result is supported. Each
of the three explicitly named, non-generic, one-argument synchronous mapping
functions must accept respectively BindingError, Value and BusinessError, and
return exactly the selected `Response` type. Functions may be reused for roles
whose types actually match. Require supported immutable data shapes for the
selected response and signature types under existing compiler rules; never
invent an error type or compare source display strings as type identity. The
selected Response is application data, not a declared transport HttpResponse.

Plain/Unit Handle results, Dispatch receipts, async mappers, generic or inferred
callables, arbitrary `pipeline = expression`, routes, request/response adapter
types, individual status/body legality, transport/runtime failure mappings,
authentication, encoding, registration and execution remain outside this child.
No claim is made that a mapper's function body handles every possible case or
that its result is a valid HTTP response. These limitations remain explicit
integration work for #54, #78, #81–#85 and #241; parent #52 remains open until
its other trigger, codec and dispatch gates are actually met.

## Steps, files and interfaces

1. Extend `src/DSharp.Compiler/Compiler.cs` with contextual HTTP syntax and
   checked actor metadata. Validate the actual scoped named functions after
   their signatures exist, using canonical nominal identities and the intrinsic
   Result identity. Preserve all prior Queue/Event/Cron checks and emit no HTTP
   invocation. Extend `src/DSharp.Compiler/MultiFileResolver.cs` for explicit
   type and function references in the actor's file, preserving spans, import
   errors and useful expected Handle diagnostics.
2. Add only new feature tests `tests/features/HttpContractTests.cs` and an
   example `examples/http-contract.ds`; preserve existing tests. Update the
   status only for verified compiler checking, not HTTP behavior.
3. Run required checks, fix real issues on this branch, record actual evidence
   and open one draft PR closing this bounded child only. Fix CI on this PR.

## Acceptance-to-test mapping

| Criterion | Planned evidence |
| --- | --- |
| Actual binder and mappings | Sync/async binder Result success matches Handle input; explicit BindingError/Value/BusinessError mappers return selected Response. Metadata retains source locations and meaningful business result. |
| Type and reference rejection | Missing/malformed/duplicate descriptors, unknown/inaccessible/ambiguous functions, wrong arity, generics/inferred results, mismatched success/error/response types and user-defined Result homonyms are rejected with located diagnostics. |
| Identity and isolation | Same short names in separate modules, imported/qualified functions, hidden synthetic-name collision, no-trigger actor, coexistence with Queue/Event/Cron and direct actor-call rejection. No registration or callback invocation is generated. |
| CLI and example | Check/compile the example and imported files; reject invalid descriptors without output; build generated C# only. No host or response mapping execution is claimed. |

## Verification commands

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-compiler.sh`, `bash scripts/verify-runtime.sh`, focused
feature tests, CLI checks and `git diff --check`. Record actual results and local
image-pull limits; exact-head hosted isolated verification must pass before
merge. If the long local checkout path exceeds the existing TRX size guard, run
the unchanged verifier from a byte-checked short snapshot and retain both
results. Preserve all existing tests and verification limits.

## Results

- The HTTP descriptor checks actual scoped ordinary binder and mapper function
  signatures, intrinsic Result identity and selected response type; it retains
  inert actor metadata and emits no HTTP registration or callback invocation.
  Local value shadowing and imported ambiguity follow ordinary resolver rules.
- The focused HTTP, Cron, Event and Queue tests passed 12 grouped facts,
  including sync/async binders, nominal and Result-shadow mismatch, missing or
  incompatible mappers, qualified imports, shadow/ambiguity and actor
  isolation. CLI checked/compiled the example, built the generated project,
  checked imported sources and rejected a missing mapper without output.
- Bootstrap, documentation (119 files, 308 local links) and all 31 automation
  tests passed. The unchanged compiler verifier passed 18 baseline and 394
  feature tests without skips from a byte-checked short-path source snapshot;
  its validated feature report was 509,776 bytes. Runtime verification passed
  2 baseline and 1 feature test. `git diff --check` passed.
- The local isolated verifier could not pull pinned MCR SDK layers (`Forbidden`);
  exact-head hosted compiler/runtime evidence remains required. No existing
  tests or verification limits were weakened. General HTTP preprocessing,
  non-Result/Dispatch mappings and actual transport behavior remain pending.
