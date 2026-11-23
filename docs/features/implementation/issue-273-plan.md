# Issue #273 — targetless Dispatch publication entries

Issue: https://github.com/gajo357/dsharplang/issues/273
Parent: https://github.com/gajo357/dsharplang/issues/241
Base: `13755fa8a0e4b0e67310e77e4d7f964b014e1f24` (`origin/main`).

## Scope and decisions

Extend #271's explicit host-scoped async entry with directly awaited targetless
`Dispatch(message)`. The compiler infers the exact message contract without
choosing an actor or considering subscriber counts, and emits one call to
`IActorDispatcher.Publish<TMessage>(contract, message)`. It evaluates the
argument once and returns the same opaque `PublicationReceipt` provided by the
dispatcher after fake acceptance. The receipt is a distinct non-spellable,
nonconstructible compiler nominal type, separate from targeted Dispatch's
`InvocationReceipt`; support it only as the inferred final host-entry result.
Reject source annotation, local storage, nullable/container/data embedding,
equality, forging and other escapes, as in #271. #69 owns actual publication,
delivery and durable acceptance; #54 owns subscriptions and #78 owns the codec.
Neither #241 nor #52 closes with this slice.

Admit a nonnullable declared record/union or Unit root with #52's conservative
structural restrictions recursively applied after generic substitution. Reject
unknown, ambiguous, null, primitive/container root, live resource, builder,
function, task, interface/capability and unsupported shapes; structural
eligibility is not proof that #78 can encode the message. Preserve ordinary
source/local/module names `Dispatch` before contextual lookup and keep the
existing targeted Dispatch/Invoke behavior. Reject unsupported actor/helper/
lambda contexts, D# calls and captures of effectful entries, unobserved tasks,
targetless Invoke, and dispatcher-dependent HTTP callbacks.

Emit a shared immutable `MessageContract<TMessage>` descriptor per exact scoped
message identity. The host-scoped key recursively represents fully qualified
source nominal names, including generic arguments; same type uses share one
descriptor and distinct same-named types do not. The key is not a durable wire
or schema identity. `Compilation.RequiresRuntime` must include publication-only
programs; the existing explicit host dispatcher argument and CLI runtime DLL
input remain in use. No subscription, actor selection or handler call is emitted.

## Steps and acceptance evidence

1. Extend `src/DSharp.Compiler/Compiler.cs` and resolver only as necessary for
   message identity, a distinct private publication receipt, normal name
   resolution, structural message checking, context restrictions and typed
   generated Publish calls. Preserve the prior ABI, tests and trusted checks.
2. Add a new `tests/features/PublicationTests.cs` with grouped tests and a D#
   example. Build generated C# against the actual runtime ABI and execute its
   managed DLL via `dotnet` on the no-exec-safe temporary mount. A controlled
   recording dispatcher checks zero, one and multiple simulated subscribers,
   pending fake acceptance, exact distinct receipt, once-only evaluation,
   same/different nominal and generic message contracts and independent hosts.
   Test invalid data/targets/scope/receipt escapes, ordinary name shadowing,
   import ambiguity and CLI check/compile. Fake subscriber lists do not prove
   registration, actual delivery or durable acceptance.
3. Update implementation status only for tested behavior. Run bootstrap, full
   verifier, compiler/runtime verification, focused tests, CLI checks and
   `git diff --check`. Keep the existing 512,000-byte report guard, all prior
   tests and artifacts. Fix any actual CI failure on this PR branch.

## Verification

- Bootstrap passed. Documentation validation passed for 125 files and 308
  local links; all 31 automation tests passed. The local isolated verifier
  could not pull the pinned MCR SDK image (`Forbidden`).
- The new publication and existing Invoke integration groups passed. The
  existing targeted Dispatch group failed one assertion: its invalid list in
  `tests/features/TargetedDispatchEntryTests.cs:81` requires DS2048 for
  `async fn use() { await Dispatch(Request { value = 1 }) }`. That is the
  valid targetless publication required here, including with a matching actor
  in scope. No principled semantic distinction can preserve that rejection.
- The unchanged-baseline standalone compiler run passed all 18 baseline
  tests and 398 of 399 feature tests; the sole failure is that prior
  assertion. Runtime verification passed 2 baseline and 5 feature tests.
- The user's standing instruction to repair CI on the same PR authorizes the
  necessary covered behavior transition despite the repository's default
  immutable-test rule. The one obsolete list item is replaced with
  `async fn use() { await Dispatch<Worker, Worker>(Request { value = 1 }) }`,
  retaining DS2048 coverage for invalid target arity and all other assertions.
  The new publication test covers the formerly rejected program positively.
  A disposable-copy preview passed the three focused groups and the unchanged
  compiler verifier (18 baseline and 399 feature tests). Its 511,931-byte TRX
  was within the 512,000-byte limit by only 69 bytes. Actual current-head and
  hosted verification are required; no verifier, guard or protection is bypassed.

## Same-PR report-size repair

- After the fixture migration, hosted CI 279 passed its trusted base harness,
  but Compiler 272's 18 baseline and all 399 feature tests passed before its
  raw TRX exceeded the unchanged size guard. The local unchanged verifier
  passed those tests and runtime 2+5 from `/tmp/a`; its 511,931-byte report
  illustrates the original narrow margin.
- The standalone Compiler workflow now uses a temporary, test-project-only
  MSBuild output-path setting with the same five directory levels required by
  existing tests. It shortens only the absolute assembly path repeated in the
  raw TRX. Exact-SHA and clean-checkout checks, test discovery/execution,
  unmodified verifier, 512,000-byte guard and artifact upload remain intact.
  The unchanged compiler verifier passed 18 baseline and 399 feature tests at
  the shorter path with a 494,375-byte TRX (17,625 bytes below the cap).
- A disposable pilot added one deliberately failing new feature test and ran
  the real standalone verifier with the same output-path setting. It reported
  399 passing and one failing test, exited nonzero, and the unchanged TRX
  validator independently rejected the 497,526-byte report as failed. The
  temporary test was not added to the branch. Hosted exact-head verification
  of this workflow repair remains required.
