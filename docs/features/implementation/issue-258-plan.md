# Issue #258 — checked event trigger contract

Issue: https://github.com/gajo357/dsharplang/issues/258
Parent: https://github.com/gajo357/dsharplang/issues/52
Base: `39bef18a47a771c0ad2d591e8a474e89669ff51c` (`origin/main`).

## Scope and decisions

Implement the Event-only compiler child of the merged [parent plan](issue-52-plan.md)
using the checked actor and message model established by Queue child #256.
#239, #51, #14 and #256 are merged prerequisites. #52 remains open for the
other trigger children and its remaining codec/dispatch acceptance evidence.
Application subscription identities and registration belong to #54; #241 owns
dispatch, #69 publication/fan-out, and #78 wire encoding. None is implemented
or represented as implemented by this slice.

Choose the bounded contextual actor member `trigger Event<Request>()`. It
records a checked candidate contract only: it carries no subscription ID,
registers nothing and does not activate delivery. Later #54 registrations may
explicitly associate this actor with independently identified subscriptions;
one descriptor is not a runtime subscription. Duplicate Event declarations
for the same resolved message on one actor are invalid. Queue and Event
descriptors coexist without creating one another. `trigger` and `Event` remain
ordinary names elsewhere, including an actor helper named `trigger`.

Reuse the Queue slice's exact resolved Handle-input identity and conservative
structural eligibility checks: a nonnullable declared record/union or Unit root,
scalars and declared data nested inside, no capabilities/functions/tasks/builders
or unfrozen nullable, tuple, collection and Option wire shapes. Preserve its
128-level nesting guard and fail closed for expanding generic recursion. These
checks do not claim a codec or actual serialization. Preserve the declared
business result, original source paths/spans, and existing direct actor-call
and ownership restrictions. Event-specific semantic failures use DS2044;
existing import/ambiguity codes retain their source locations and show the
expected Handle when the source declaration supplies one.

## Steps, files and interfaces

1. Extend `src/DSharp.Compiler/Compiler.cs` with contextual event syntax and
   checked actor Event metadata; factor the shared Queue/Event structural and
   Handle validation without weakening existing queue rules. Extend
   `src/DSharp.Compiler/MultiFileResolver.cs` for event message resolution and
   its source-facing diagnostics. Emit no transport or registration code.
2. Add new `tests/features/EventContractTests.cs` and
   `examples/event-contract.ds`; preserve old tests. Update implementation
   status only with verified compiler behavior.
3. Run the checks below, fix in-scope issues on this branch, record actual
   evidence and open one draft PR closing this child only. CI repairs belong on
   this same branch and must preserve all safeguards.

## Acceptance-to-test mapping

| Criterion | Planned evidence |
| --- | --- |
| Explicit checked Event contract | Valid declared/Unit message and meaningful `Result` Handle; verify metadata, retained target/result and origin spans. Reject mismatch, duplicates, malformed members, invalid shapes and inaccessible/ambiguous multi-file types with expected Handle and location. |
| No implicit subscription | No-binding, queue-only and Event-only actors expose only their declared metadata. A test-only explicit association pairs an event candidate with its checked public/default actor, without a host registration API or delivery claim. |
| Shared safety and isolation | Queue+Event coexistence; generic substitution, regular/expanding recursion, forbidden nested data and previous actor-construction/Handle bypass rejection. Preserve existing queue tests unchanged. |
| CLI and example | Check/compile the example and imported files, and reject invalid input without output. Generated code is built only; no event delivery is claimed. |

## Verification commands

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-compiler.sh`, `bash scripts/verify-runtime.sh`, focused
feature tests, CLI checks and `git diff --check`. Record actual results and
local image-pull limitations; exact-head hosted isolated verification is
required before merge.

## Results

- Checked Event candidate metadata retains the owning actor's input/result and
  source spans. Queue and Event use the same resolved-type and structural-data
  validation. No subscription identity, registration, codec, transport or
  publication was added. The test-only association is an illustrative metadata
  fixture, not validation or execution of #54 registration.
- Focused Event and existing Queue tests passed 8 grouped facts, including
  valid/invalid contracts, generic substitution, regular and expanding recursion
  using an otherwise eligible nested type, source identity and locations,
  queue/event coexistence and actor isolation. The CLI checked and compiled the
  example, checked an imported multi-file contract, and rejected an incompatible
  Handle with DS2044 and its expected signature/location without emitting output.
- Bootstrap, documentation validation (117 files, 306 local links) and all 31
  automation tests passed. Runtime verification passed 2 baseline and 1 feature
  test. The compiler verifier passed 18 baseline and 390 feature tests without
  skips from a byte-checked short-path source snapshot; its validated feature TRX
  was 507,137 bytes. In the longer workspace path all 390 feature tests passed,
  but their 512,597-byte TRX exceeded the unchanged 512,000-byte guard.
- The full local isolated verifier could not pull the pinned MCR SDK layers
  (`Forbidden`). Exact-head hosted CI must establish its isolated compiler and
  runtime evidence; no tests or verification limits were weakened.
