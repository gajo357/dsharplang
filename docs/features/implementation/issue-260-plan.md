# Issue #260 — checked Cron trigger contract

Issue: https://github.com/gajo357/dsharplang/issues/260
Parent: https://github.com/gajo357/dsharplang/issues/52
Base: `111f685dab8c41a5cea8c7fd862fd2ef9f36311f` (`origin/main`).

## Scope and decisions

Implement the Cron-only compiler child in the merged [parent plan](issue-52-plan.md),
reusing checked actor and trigger contracts from Queue #256 and Event #258.
#239, #51, #14, #18, #256 and #258 are merged/closed. #18's DEC-08 schedule
details still explicitly call themselves draft; issue closure is not adoption
or evidence of implemented schedule behavior. #52 remains open for HTTP and
its separate codec/dispatch acceptance gates.

Choose the bounded actor member `trigger Cron<Request>("nightly", "0 0 * * *")`.
It records only a candidate: a nonempty local descriptor name, nonempty opaque
expression text, declared message type and source spans. Neither literal is
registration or an activated schedule. Compare decoded descriptor names
ordinally and reject duplicates within one actor. `trigger`, `Cron` and ordinary
helpers remain usable elsewhere. The expression is retained for later owners;
the compiler does not parse or validate its calendar syntax, timezone, revision,
missed-run policy or defaults. The example expression is illustrative, not an
adopted cron dialect. #54 owns registration and typed payload provision, #70
owns schedule/calendar/recovery, #78 encoding and #241 dispatch.

The descriptor message must match the actor's resolved Handle input using the
same nonnullable declared record/union or Unit root and conservative recursively
substituted data rules as Queue/Event. Preserve their depth bound and reject
unfrozen nullable/tuple/collection/Option wire shapes. This is structural
eligibility only, not a serialization guarantee. Preserve actor result, source
identity, existing bindings and direct-actor-call restrictions. Cron semantic
failures use DS2045, while existing import/ambiguity codes retain their source
paths/spans and show the expected Handle where available.

## Steps, files and interfaces

1. Extend `src/DSharp.Compiler/Compiler.cs` with contextual Cron syntax and
   checked actor Cron metadata; reuse the existing structural and Handle
   checks and literal validation without weakening Queue/Event behavior. Extend
   `src/DSharp.Compiler/MultiFileResolver.cs` for source-context Cron type
   resolution and import diagnostics. Emit no scheduler, registration or codec.
2. Add only new tests in `tests/features/CronContractTests.cs` and an example
   `examples/cron-contract.ds`. Update implementation status only for observed
   compiler metadata; preserve all old tests and verification safeguards.
3. Run required checks, correct real failures on this branch, record evidence and
   publish one draft PR closing this child only. CI repairs stay on this PR.

## Acceptance-to-test mapping

| Criterion | Planned evidence |
| --- | --- |
| Typed descriptor and metadata | Valid declared and Unit contracts with non-Unit result; decoded name, opaque expression and source spans retained; incorrect Handle input, malformed/empty or duplicate descriptors, invalid shapes and inaccessible/ambiguous imports produce located errors. |
| No scheduler or implicit registration | No-binding and Queue/Event-only actors expose no Cron metadata; one actor can hold Queue/Event/Cron descriptors without activating them. No payload creation, occurrence or runtime delivery is asserted. |
| Shared data/isolation rules | Generic substitution and regular/expanding recursion; nested capabilities and unsupported shapes; cross-file identity and synthetic-name collision; ordinary `trigger` helper and user `Cron` identifiers; actor construction/direct-call regressions. |
| Example and CLI | Check/compile example and imported sources; reject invalid contracts without output; build generated C# only. No cron grammar or scheduling claim. |

## Verification commands

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-compiler.sh`, `bash scripts/verify-runtime.sh`, focused
feature tests, CLI checks and `git diff --check`. Record actual results and
local image-pull limits; exact-head hosted isolated verification is required
before merge. If the long local path overflows the existing TRX size guard,
rerun the unchanged verifier from a byte-checked short snapshot and retain both
results; do not weaken the guard or existing tests.

## Results

- Cron metadata records decoded local descriptor name, opaque expression,
  resolved message type and source locations on the checked actor. Queue, Event
  and Cron share structural Handle validation; no schedule is registered,
  payload created or cron expression parsed by this slice.
- Focused Cron, Event and Queue tests passed all 10 grouped facts. The new
  cases cover meaningful results, Unit, coexistence, intentionally unvalidated
  expression text, decoded duplicates, forged literals, generic recursion,
  capability rejection, multi-file identity and actor isolation. The CLI
  checked and compiled the example, checked an imported contract and rejected
  a mismatch with DS2045 and the expected Handle location without output.
- Bootstrap, documentation (118 files, 307 local links) and all 31 automation
  tests passed. From a byte-checked short-path source snapshot, the unchanged
  compiler verifier passed 18 baseline and 392 feature tests without skips;
  its validated feature report was 509,607 bytes. Runtime verification passed
  2 baseline and 1 feature test. `git diff --check` passed.
- The local isolated verifier could not pull pinned MCR SDK layers (`Forbidden`);
  exact-head hosted compiler/runtime verification remains required. No tests or
  verification limits were weakened.
