# D# implementation instructions

**Updated:** 13 November 2026
**Repository:** https://github.com/gajo357/dsharplang

## Current source of truth

The compiler bootstrap and compiler-code automation are implemented. The old
version 0.1 handoff described the earlier documentation-only state; its bootstrap
and automation-enablement briefs are historical, not tasks to repeat. That version
remains available in Git history at commit
[567fbf1](https://github.com/gajo357/dsharplang/blob/567fbf1c88d8a329339f1e11a92e4ee71bf90e03/docs/features/DSharp_Implementation_Instructions.md).

Read [AGENTS.md](../../AGENTS.md), [bootstrap scope](../COMPILER_BOOTSTRAP.md),
[implementation status](DSharp_Implementation_Status.md) and the
[syntax reference](DSharp_Syntax_Reference.md), revision 0.4, and the
[pipes, partial application and logging target](DSharp_Pipes_and_Logging.md).
Selected requirements below, syntax section 21.2 and that companion supersede
conflicting older examples. Other proposed syntax and APIs still need their
relevant contract decision; this update does not approve unrelated draft choices
or establish implementation completion.

The compiler is C#, with a hand-written lexer/parser, typed binding and generated
C# on .NET SDK 10.0.100. The implementation status records the tested subset,
including plain pipes and tuples; do not infer implementation support for partial
application, async composition or Logger from the new target specification.

## Codex web lifecycle

Follow the [implementation sequence](implementation/DSharp_Implementation_Sequence.md)
and [task queue](implementation/DSharp_Task_Queue.md).
Recheck current main and existing tasks/PRs to avoid duplicating completed or active
work. Dependencies must be merged before dependent implementation starts.

Use one issue, one dedicated branch, one implementation plan and one PR. Write
`docs/features/implementation/issue-N-plan.md` before implementation. Include base
commit, exact scope, behavior/examples, steps, files/interfaces, dependencies,
AC-to-test mapping, commands and unresolved decisions. Proceed through routine
implementation/testing without another approval pause. Report a missing prerequisite
or unresolved public contract rather than choosing it implicitly.

Keep foundational compiler work sequential. Split broad parent issues into their
planning/contract slice and separate implementation children as the sequence
requires. A plan merged for a parent is not completion of its children.

Implement semantic checks before lowering, add positive generated-execution tests
and negative diagnostic tests, preserve existing tests, and update the plan/status
with actual results. Open a draft PR containing plan, code, tests and acceptance
evidence. Use Closes #N only for a completed issue; use Refs #N for coordination
work. The user reviews and merges; do not merge, deploy or start another issue.

## Scope and the optional coordinator

Normal Codex web work does not require AI coordinate, ai:approved or an API key.
The separate [API-backed workflow](../AI_WORKFLOWS.md) is available if explicitly
selected; retain its frozen-input and trusted verification/publishing rules.

Current compiler-code policy allows only:

- `docs/features/implementation/**/*.md`
- `docs/features/DSharp_Implementation_Status.md`
- `src/DSharp.Compiler/**/*.cs`
- `src/DSharp.Cli/**/*.cs`
- `tests/features/**/*.cs` (new tests; existing tests are protected)
- `examples/**/*.ds`

Read the actual [policy](../../.github/ai/policy.json) before coordinator dispatch.
Its 20-file / 128-KiB patch boundary, repair budgets and deadlines apply to that
workflow, not to the Codex web product. Do not silently expand the current
feature scope. Separately scoped setup issues may propose exact projects,
dependencies and verification changes through reviewed PRs; they do not grant
blanket authority to ordinary feature tasks. This documentation task changes no
workflow source, lockfile, policy, dependency or trusted verifier.

## Verification and completion

Run the existing commands:

```bash
bash .github/ai/bootstrap.sh
bash .github/ai/verify.sh
bash scripts/verify-compiler.sh
```

Bootstrap checks pinned prerequisites and installs locked JavaScript validation
dependencies. The verifier checks documentation/automation and runs the trusted
compiler harness. The compiler script performs locked restore, Release build,
real compiler/generated-code tests and strict nonzero/all-passed TRX validation.
Use JavaScript for repository validation; do not introduce Python. Preserve
managed .NET execution; Native AOT remains deferred.

Report actual commands/results and map every AC to evidence. A documentation pass,
parsed syntax, stub or unrelated C# build is not feature completion. Keep partial,
unsupported, implemented and tested behavior distinct. Unresolved runtime fault,
queue/store, serialization, middleware/auth and interop contracts still require
scoped decision issues before dependent implementation.

## 4. Language requirements agents must preserve

Treat these as requirements once incorporated into the approved repository design. Do not reintroduce older alternatives from superseded examples.

1. **Immutable data:** `type` declares records or discriminated unions. Fields and nested data are immutable. `List`, `Map` and `Set` are immutable values.
2. **Local builders:** mutable lists/maps may exist only in their creating function/method/handler or lambda. They cannot escape through fields, returns, captures, wrappers, tasks, views, foreign calls or agent messages. `freeze()` creates an independent immutable snapshot.
3. **Nullable values and explicit options:** `T?` means `T` or `null`, with idempotent nullability after substitution or alias resolution. An unconstrained `T?` must admit null even for `T = int`; generated C# must preserve this across calls and stored data. Match directly against a present value or null. Separately, `Option<T>` has `None` and `Some(T)`: `Some(null)` requires nullable `T`, and nested options remain distinct. Do not implicitly convert between these concepts. Track generic nullable correctness in [#30](https://github.com/gajo357/dsharplang/issues/30) and explicit options in [#121](https://github.com/gajo357/dsharplang/issues/121); do not close #30 for syntax support alone. Alias syntax and collection support remain future work where not implemented; their eventual behavior must preserve these rules. JSON and HTTP mapping remain undecided.
4. **Results:** use `Result<T, E>`, `Success(value)`, `Success(null)`, `Success()` for Unit and `Error(error)`. There is no nested present-value wrapper in ordinary syntax.
5. **Error propagation:** postfix `?` applies to a compatible `Result`, not an unchecked nullable unwrap. `?.` and `??` handle nullable access/fallback.
6. **Public declarations:** no visibility modifiers. Public visibility does not defeat lexical scope, immutable fields, resource lifetimes or runtime dispatch restrictions.
7. **Interface defaults:** interface methods may have bodies; methods without bodies require implementation. Explicit service implementations take precedence. No `default service` selection feature.
8. **Callable workers:** a worker may be called and its result awaited through the runtime. It remains an isolated invocation, not a direct method call on shared mutable state.
9. **Message-only queues:** queued agents cannot be called as workers, directly invoke `handle`, expose completion tasks or disguise the handler as a lambda. Only enqueue acceptance may be awaited by a producer.
10. **Queue contracts:** a queue message root is a declared data `type` or Unit. Nested fields must be immutable and serializable; runtime capabilities, tasks, functions and connections are not payloads.
11. **Handler outcomes:** every normal handler path reports success or error to the runtime/supervisor. Queue retry decisions are independent of returning a result to a caller.
12. **First-class lambdas:** inferred/typed parameters, multiple parameters, block bodies, function values, immutable captures, returned lambdas and async lambdas. Captures must respect lifetime and non-escape rules.
13. **Explicit composition:** handlers declare triggers; application code registers definitions and dependencies. No implicit application-handler discovery and no YAML application source of truth. Logger is an explicit runtime-owned system-actor exception, not permission for packages to auto-activate handlers.
14. **Discoverable contracts:** compiler diagnostics identify required `handle`/`trigger` members. Trigger types determine handler input and valid policies; editor features use the same semantic contract model.
15. **Small language:** preserve braces, ordinary calls, inference, expressions and explicit failure handling. Do not add class inheritance, mutable record fields or undocumented convenience syntax.

Detailed syntax, examples and provisional choices live in the companion reference. A list here is not a replacement for reading it.

## 5. Composition and logging implementation targets

The [pipes and logging specification](DSharp_Pipes_and_Logging.md) is the detailed
target contract. Reconcile existing issue mappings before dispatch: LANG-18/#35
remains the merged plain-pipe feature, LANG-41 adds partial application, LIB-04
adds Result helpers, HTTP-03/#83 becomes callable preprocessing, LOG-01 through
LOG-04 separate capture, delivery, storage and queries, and OPS-01/#91 reuses
Logger for runtime instrumentation. Plan bounded children where a slice spans
unimplemented language/runtime prerequisites. A documentation merge completes
none of those implementation slices.

- **Calls and pipes:** evaluate left/receiver/supplied argument expressions once
  in source order. Partial application binds positional prefixes and delays the
  body/task until all arguments arrive; retain ordinary multi-argument types.
  Reject excess arguments, discarded function expressions and escaping builders
  or resources. Resolve a unique callable before partial application; ambiguous
  generics need context/annotations. Named partials, holes and guessed foreign
  overload/default/variadic behavior are outside the initial feature.
- **Result composition:** implement callback-first/value-last `andThen`, `map`,
  `mapError`, `tap` and `tapError` as library functions, with the exact sync/async
  matrix in the companion. Preserve errors, eager callback-expression creation,
  at-most-once applicable callback invocation, faults/cancellation and distinct
  null/Option payloads. Do not add implicit pipe await/unwrap, retry, flattening,
  parallelism or durability.
- **HTTP and runtime dispatch:** use `pipeline = callable` with ordinary pipes
  and Result helpers, not `pipeline {}`. Check input/output and explicit HTTP
  error mappings; the host invokes preprocessing once and `handle` only on
  success. `call(worker)` and `enqueue(agent)` must preserve their existing
  runtime contracts and lifetimes, with durable acceptance receipts distinct
  from queued handler results. Wrapping middleware remains separate.
- **Capture and scopes:** `Logger.log` captures structured immutable events and
  performs a non-waiting transient submission, returning Unit. Validate literal/
  constant templates and named fields; retain eager argument evaluation even at
  disabled levels. Scope cleanup covers return, `?`, fault and cancellation;
  concurrent invocations/hosts stay isolated. Capture a snapshot rather than a
  mutable context reference. Never infer an Option JSON schema from logging.
- **Delivery:** use the actor runtime, bounded per-host admission and per-output
  state, immutable batches, bounded retries and one in-flight write per output.
  Failed/blocked outputs must not block producer calls or healthy outputs.
  Timeouts cannot cause overlapping retries; internal diagnostics bypass Logger.
  Console at Info is the only unconfigured default. Explicit output lists
  replace it, including an empty list, and lifecycle ordering protects the shared
  store. Preserve every operational budget in the companion.
- **Storage and search:** opt-in DatabaseLogOutput shares the queue RuntimeStore
  with separately versioned tables, committed batches and event-ID deduplication.
  Retention uses receive time and bounded expiry/capacity cleanup; it never
  deletes business rows or shares handler transactions. LogReader is separate,
  read-only, authenticated through the observer boundary, typed and parameterized.
  Preserve numeric precision and missing/null distinctions, bind cursors to the
  query/high-water sequence, and report timeouts/unsupported queries explicitly.

Require executed generated-C# tests for language behavior, controlled clocks and
blocked/failing outputs for delivery, and real-provider integration tests for
database retention, queries and queue contention. Test documented log loss rather
than claiming durable or exactly-once logging. Unrelated async, runtime, transport,
serialization and interop prerequisites retain their existing decision gates.
