# Issue #51 — checked actor declaration contract

Issue: https://github.com/gajo357/dsharplang/issues/51
Base: `a731895701a1102f151c66765538fb3c97c3a9ee` (`origin/main`)

## Scope and dependencies

Introduce a distinct compiler actor declaration and checked single-Handle contract. The merged actor handoff #239 and #13 semantics, plus #31 Result, #41 async and #43 interfaces, are prerequisites; #9 enables separately scoped runtime tests but this issue does not implement a dispatcher or durable execution. Preserve existing service fixtures until #240, all existing tests, the pinned SDK and trusted verification. #52, #54, #241 and #242 own triggers, registration, dispatch and selection respectively.

Use the handoff's bounded spelling `actor Name(optional: Resource) { Handle(message: Input): Output { ... } }`, implicitly async Handle bodies, and typed helper methods using existing async conventions. This issue owns checking declaration/Handle spelling; actor instances are not source-constructible values. Any generated body entry is an internal compiler/host boundary, not permission for D# callers to construct actors or invoke Handle directly. Resource dependency types must be explicit; no actor-instance dependencies or implicit service locator.

## Steps and affected interfaces

1. Add distinct actor syntax and reserved keyword to `src/DSharp.Compiler/Compiler.cs`; retain source spans, typed dependencies, methods and one unambiguous Handle.
2. Bind checked actor metadata (name, source location, input/result, dependencies and implementation) and body/async semantics using existing type and ownership checks. Reject missing/duplicate/overloaded Handle, malformed signatures, actor injection, construction and direct/method-group/cast bypasses. Helpers remain actor-private.
3. Update `src/DSharp.Compiler/MultiFileResolver.cs` for supported module layouts, names and source locations, and generate only internal host-facing actor execution entry points using existing backend machinery. Do not implement actor dispatch, registration or scheduling.
4. Add new feature tests under `tests/features/` and D# examples; map all criteria below, update tested implementation status, and run required verification. One issue, one branch and one draft PR; no edits to old tests or verifier.

## Acceptance-to-evidence map

| #51 criterion | Planned evidence |
| --- | --- |
| AC1: one typed Handle, Unit/Result/await | Positive/negative parser and binder feature tests; execute generated value, Unit, Result and async examples |
| AC2: helpers, resources and no bypass | Positive helper/resource tests and negative duplicate/missing/overloaded Handle, construction, injection, direct call, group/cast tests |
| AC3: checked metadata and source layouts | Assert metadata names/types/dependencies/locations across single and multi-file sources; generated C# execution |
| AC4: single declaration model | One bound actor record used regardless of result type/async; no dispatch, trigger or registration shortcuts |
| Verification | `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`, `bash scripts/verify-runtime.sh`, targeted feature tests, `git diff --check`; report hosted isolated results separately |

## Boundaries and decisions

The precise dispatcher/public fault API remains with #241/#13; actor registration and specialization remain with #54/#242. Source spelling above follows the selected handoff as this issue's bounded compiler contract, not a new application runtime protocol. Do not weaken old service, immutable payload or resource ownership checks. If full safe generation or source layout cannot be achieved in this slice, keep the draft explicitly partial and do not claim the unmet acceptance criteria.

## Results

- Added separate parsed and bound actor metadata, typed single Handle, compiler-private bodies, actor-local helpers, source-facing multi-file identities and resource dependencies. Source construction, actor injection, direct Handle calls, dependency release/alias/forwarding, and escaping async borrowed operations fail closed. #241 and later runtime issues still own dispatch and execution.
- New feature coverage groups the actor contract cases into four facts to preserve all assertions while respecting the existing strict report-size limit. Generated C# host-side tests execute value, `Unit`, business `Result.Error`, await, helper and borrowed-resource examples. `dotnet src/DSharp.Cli/bin/Release/net11.0/dsharp.dll check examples/actor-contract.ds` passed. Independent probes confirmed grouped/conditional/indirect dependency-release and task-lifetime attempts are rejected.
- With `PATH=/tmp/dsharp-dotnet:$PATH`, `npm_config_cache=/tmp/dsharp-npm-cache`, `DOTNET_CLI_HOME=/tmp/dsharp-dotnet-home`, `NUGET_PACKAGES=/tmp/dsharp-nuget` and `XDG_RUNTIME_DIR=/tmp`, bootstrap passed; `bash scripts/verify-runtime.sh` passed 2 baseline and 1 feature test; and `bash scripts/verify-compiler.sh` passed locked restore/build, 18 baseline and 382 feature tests without skips. An earlier run executed 396 passing tests but its 520,118-byte TRX exceeded the unchanged 512,000-byte trusted limit; grouping only the new cases produced the passing report.
- The full local verifier passed documentation and all 31 automation tests but could not pull the pinned container layers (`Forbidden`) before its isolated compiler and runtime stages. GitHub exact-head checks must establish those isolated results. `node .github/ai/check-docs.mjs .` passed (114 files, 304 local links), and `git diff --check` passed. No runtime dispatch or durability is claimed.
