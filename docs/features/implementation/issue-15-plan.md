# Issue #15 — HTTP composition and authorization contract

Issue: https://github.com/gajo357/dsharplang/issues/15
Base: `d6d2ad277caa717639971deb00c7e72aac98c289` (`origin/main`)

## Scope and dependencies

Reconcile DEC-05's HTTP binding, ordinary callable preprocessing, explicit response mappings, wrappers and authorization contract with the actor handoff and accepted durable admission boundary. This is a design decision, not runtime implementation. #13 and #14 semantic boundaries are on main; public fault/registration syntax remains with their owners. #81–#85 own HTTP implementation, #78 owns wire encoding, and #80/#246/#247 own outgoing factory-backed HTTP. Do not introduce a new identity provider or authorize durable actor work by serializing request-scoped proof.

Issue #15 confines decision content to `DEC-05.md`; this separate plan is required by repository instructions and the scheduled workflow. No other product or design files are in scope.

## Steps and affected interfaces

1. Inventory DEC-05's established input/output, errors, wrapper and evidence rules against #15, the pipes target and actor handoff.
2. Replace obsolete agent/service descriptions with explicitly registered HTTP routes feeding targeted actor dispatch, preserving typed preprocessing and authorization checks without leaking request capabilities across durable boundaries.
3. Make the selected evidence and cleanup boundaries reviewable with protected-route and negative examples; distinguish endpoint authorization at admission from separate operation-specific reauthorization and avoid implying deferred execution revalidates automatically.
4. Cross-check acceptance and owners, run verification and record actual results. Only DEC-05 and this plan change.

## Acceptance-to-evidence map

| #15 criterion | Evidence |
| --- | --- |
| AC1: typed transport, errors, mappings | Binding and response sections, typed example and mapping table |
| AC2: async composition and wrapper cleanup | Continuation, context and lifetime sections; failure/cancellation cases |
| AC3: authentication and runtime-valid authorization evidence | Host-issued request-scoped proof and durable dispatch boundary |
| AC4: protected route and negative tests | Example and future executed/transport evidence matrix |
| Verification | `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`, `git diff --check`; documentation checks do not prove runtime behavior |

## Decisions and boundaries

Use ordinary callable pipelines and explicit actor routes. Propose endpoint authorization before durable submission; never persist a request proof as an actor message or claim admission guarantees later revocation checks. Sensitive execution-time operations enforce their own current policy. Exact registration/API syntax, provider details, actor fault representation, and encoding remain owned by their separate issues, not invented here.

## Results

- Reconciled DEC-05 with explicit durable actor dispatch while preserving typed callable preprocessing, response mappings, wrappers and runtime authorization evidence. Request-scoped proof is checked at HTTP admission and is never serialized; protected execution requires independently valid authority and fails closed when no approved mechanism exists. Independent review corrected the Dispatch receipt mapping and example formatting, and added authorization-change and cross-subject cases.
- With `PATH=/tmp/dsharp-dotnet:$PATH`, `npm_config_cache=/tmp/dsharp-npm-cache`, `DOTNET_CLI_HOME=/tmp/dsharp-dotnet-home`, `NUGET_PACKAGES=/tmp/dsharp-nuget` and `XDG_RUNTIME_DIR=/tmp`, bootstrap passed. The full verifier passed documentation and 25 automation tests but could not download the pinned SDK container layers (`Forbidden`).
- With the same environment, `bash scripts/verify-compiler.sh` passed locked restore/build and all 18 baseline and 378 feature tests without skips. After review fixes, `node .github/ai/check-docs.mjs .` passed (111 files, 305 local links), and `git diff --check` passed. These checks do not establish HTTP or authorization behavior.
