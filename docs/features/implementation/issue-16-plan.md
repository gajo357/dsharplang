# Issue #16 — CLR and NuGet interoperability contract

Issue: https://github.com/gajo357/dsharplang/issues/16
Base: `0759d0b7778e3649ae8fef54c89891699f9d790e` (`origin/main`)

## Scope and dependencies

Review and reconcile the existing DEC-06 C#/F# boundary with the merged actor handoff, current .NET 11 managed baseline and decision records. Adopt a bounded contract on reviewed merge without claiming implementation. #87–#90 own package integration and CLR/F# implementation; #121 owns D# Option; #45 and #17 own scoped-resource behavior. No package, compiler, dependency, trusted verifier or baseline change is in scope.

Issue #16 confines decision content to `DEC-06.md`; this separate plan is required by repository instructions and the scheduled workflow. No other product or design files are in scope.

## Steps and affected interfaces

1. Cross-check the original proposal's allowed CLR shapes, null and exception mapping, mutable copying/capabilities and lifecycle rules against #16, #121 and current decisions.
2. Reconcile obsolete agent/service and worker/queue language with isolated actor attempts, resource capabilities and immutable message restrictions. Update SDK context to the actual pinned .NET 11 managed target; retain the explicit asset, package and trusted-code limitations.
3. Verify F# option, union, function and async mappings preserve nullable/Option distinctions and lifecycle constraints; retain future implementation evidence and independent scoped owner issues.
4. Record any real unresolved implementation boundary, run required verification and update results. Only DEC-06 and this plan change.

## Acceptance-to-evidence map

| #16 criterion | Evidence |
| --- | --- |
| AC1: CLR eligibility, null, exceptions and ownership | Explicit adapter, recursive value and snapshot/capability sections and future tests |
| AC2: tasks, cancellation, disposal, delegates and non-escape | Async/callback lifecycle sections and controlled tests |
| AC3: NuGet lock and supported assets/framework | Locked restore and build boundary, fail-closed package inspection tests |
| AC4: F# adapters and Option/null distinctions | F# mapping table, case-aware boundary, round-trip tests |
| Verification | `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`, `bash scripts/verify-compiler.sh`, `git diff --check`; these do not demonstrate foreign execution |

## Decisions and boundaries

Preserve the narrow explicit adapter subset and pinned managed target. Public syntax, package declaration format, actual F# package/version and provider-specific shutdown remain scoped implementation choices; unsupported shapes fail closed. Never claim untrusted foreign code is sandboxed or settle actor fault representation by this decision.

## Results

- Reconciled DEC-06 with the current actor/resource boundaries and pinned 11.0.100/net11.0 managed target. The contract retains F# Option/null distinctions, lock and package-hook restrictions, and the explicit trusted-code boundary. It permits only a tracked bounded async resource borrow while preserving non-escape and transaction limits. Independent review found no substantive contradiction.
- With `PATH=/tmp/dsharp-dotnet:$PATH`, `npm_config_cache=/tmp/dsharp-npm-cache`, `DOTNET_CLI_HOME=/tmp/dsharp-dotnet-home`, `NUGET_PACKAGES=/tmp/dsharp-nuget` and `XDG_RUNTIME_DIR=/tmp`, bootstrap passed. The full verifier passed documentation and 25 automation tests but could not download the pinned SDK image layers (`Forbidden`).
- With the same environment, `bash scripts/verify-compiler.sh` passed locked restore/build and all 18 baseline and 378 feature tests without skips. `node .github/ai/check-docs.mjs .` passed (112 files, 303 local links), and `git diff --check` passed. These checks do not establish safe foreign execution.
