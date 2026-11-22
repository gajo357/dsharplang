# Issue #256 — checked queue trigger contract

Issue: https://github.com/gajo357/dsharplang/issues/256
Parent: https://github.com/gajo357/dsharplang/issues/52
Base: `535ea1b1d013398e2ddec67b2dabbe09498c1ec5` (`origin/main`).

## Scope and decisions

Implement the queue-only compiler child from the merged [parent plan](issue-52-plan.md).
The checked actor model #51, #239, #40 and #14 are merged prerequisites. No
event/cron/HTTP binding, application registration (#54), dispatcher (#241),
codec (#78), queue transport, host startup or durable acceptance (#61) is
implemented. Parent #52 stays open for the other children and its codec/dispatch
acceptance evidence.

For this bounded compiler slice, choose the contextual actor member spelling
`trigger Queue<Request>("orders")`; it declares checked metadata only. `trigger`
and `Queue` are not globally reserved and a method named `trigger` continues to
work. A binding names one existing actor Handle input and a nonempty queue name;
duplicate decoded names within an actor are rejected. Cross-actor registration
and uniqueness are owned by #54/#58. The chosen spelling does not assert a wire
format or a stable host API.

The root must be `Unit` or a nonnullable declared record/union. Compare resolved
nominal/generic types, not display strings. Recursively substitute generic
parameters in every record field and union payload, rejecting functions, tasks,
builders, actor/resource/interface/service capabilities and unsupported shapes.
Allow existing scalar data and recursively declared records/unions as fields;
conservatively reject nullable, tuple, collection, `Option` and other unfrozen
wire shapes in this first structural slice. A regular recursive type may be
checked with a finite visited graph; expanding generic recursion must terminate
and fail closed. A conservative limit of 128 nested type/member steps fails
closed on deeper shapes; it does not limit the number of sibling fields. This
proves only structural eligibility, not encoding. Future
#78 compatibility and exact supported codec shapes remain parent closure gates.

## Steps, files and interfaces

1. Extend `src/DSharp.Compiler/Compiler.cs` with source-preserving queue syntax,
   contextual parsing, exact checked actor/type comparison, bounded recursive
   shape validation, diagnostics and immutable checked queue metadata. Extend
   `src/DSharp.Compiler/MultiFileResolver.cs` to resolve binding types in the
   actor's source context without losing locations. Do not emit queue execution.
2. Add only new tests in `tests/features/QueueContractTests.cs` and an example
   in `examples/queue-contract.ds`. Preserve all existing tests, SDK, trusted
   verification and unrelated semantics. Update the implementation status with
   only the tested compiler contract, not runtime claims.
3. Run the checks below, fix in-scope failures on this branch, record actual
   results and open one draft PR closing this child only. If CI fails, repair it
   on the same PR while preserving tests, verification and protections.

## Acceptance-to-test mapping

| Criterion | Planned evidence |
| --- | --- |
| Explicit queue, matching Handle, diagnostics and locations | Parse/type-check positive/negative bindings, expected signature, duplicates and malformed members; inspect queue metadata and source spans in single and multi-file/CLI paths, including same-named types from different modules. |
| Data roots and recursive safety | Valid declared message and Unit, primitive/nullable/container roots, nested unsupported or capability shapes, instantiated generics and recursive/expanding graphs; verify finite, fail-closed diagnostics. |
| Preserve result and actor isolation | Queue actor with non-Unit or `Result` result and a zero-binding actor; retain direct-construction/Handle and capability-escape rejection without claiming dispatch or registration. |
| Example and executable evidence | `dsharp check` and `compile` for the example; execute generated behavior only where actually emitted. Queue delivery and codec claims remain out of scope. |

## Verification commands

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-compiler.sh`, `bash scripts/verify-runtime.sh`, targeted new
feature tests and CLI checks for the example and invalid cases. Run
`git diff --check`. Record actual results and local image-pull limitations; a
hosted exact-head isolated verifier must pass before merge.

## Results

- The parser and binder retain explicit queue name, resolved message and source
  locations on the existing checked actor model. Exact Handle matching uses
  scoped type identity. Duplicate decoded names, malformed direct-Binder
  literals and unsafe/unsupported nested shapes fail closed; there is no queue
  runtime, codec or registration implementation in this slice.
- The four grouped `QueueContractTests` pass, including Unit and meaningful
  business-result actors, normal and expanding recursion, generic substitution,
  unsupported shapes and capability-bearing payload rejection, decoded duplicate
  names, cross-file identity and source spans, malformed and forged syntax,
  actor isolation and a 300-field record. The compiler verifier passed locked
  restore/build, 18 baseline and 386 feature tests without skips. The standalone
  runtime verifier passed 2 baseline and 1 feature test.
- The CLI checked and compiled `examples/queue-contract.ds`, checked an imported
  message across files, and rejected an incompatible Handle input with DS2043,
  expected signature and source location without creating output. The generated
  project built successfully; no queue delivery was claimed or exercised.
- Bootstrap passed, documentation passed (116 files, 305 local links), and all
  31 automation tests passed. The local isolated verifier could not pull the
  pinned MCR SDK image layers (`Forbidden`); exact-head hosted verification is
  still required. `git diff --check` passed.
- Follow-up review found an unimported generic source name could collide with a
  synthetic multi-file symbol. Fresh-name allocation now reserves names inside
  composite type tokens, and regressions cover direct and nested type uses with
  and without a queue binding. Queue type/name diagnostics also retain the full
  expected Handle signature and source location, including import errors. The
  focused queue tests passed after these fixes; full verification is rerun for
  the updated head, with hosted isolated evidence still required.
