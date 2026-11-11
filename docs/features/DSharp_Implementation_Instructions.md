# D# implementation instructions for GitHub agents

**Version:** 0.1  
**Date:** 11 November 2026  
**Repository:** https://github.com/gajo357/dsharplang  
**Syntax source:** [DSharp_Syntax_Reference.md](DSharp_Syntax_Reference.md), revision 0.3.

This is an implementation handoff, with a bounded first task, a bootstrap proposal and staged acceptance criteria. It does not claim that the compiler, runtime or language server exists. It does not itself change workflow permissions or authorize agents to bypass repository policy.

## 1. How to use these files

1. Add these two files to the repository, keeping their filenames and placing both under `docs/features/`:
   - `docs/features/DSharp_Syntax_Reference.md`
   - `docs/features/DSharp_Implementation_Instructions.md`
2. Use the planning issue in section 3 with your existing approved-issue workflow. It creates a concrete gap/approval plan within the current documentation-only scope.
3. Use the bootstrap brief in section 5 in a normal repository coding session after accepting its explicit architecture choices. This is separate from the restricted automated implementation worker.
4. Review and merge the executable bootstrap, then review the automation-enablement change in section 6. Only then submit compiler/runtime tasks through the automated code pipeline.
5. Submit one small milestone slice at a time using the issue template in section 9. Do not assign the entire language to one timed run.

These files should be present in the checked-out repository at the approved base revision. A path on a ChatGPT workspace is not accessible to a GitHub runner. Do not assume an attachment or a mutable external URL becomes a frozen workflow input merely because it is mentioned in an issue.

No repository files or settings were changed when this handoff was prepared.

## 2. What your current workflow actually permits

Repository files inspected on 11 November 2026 establish the following:

| Evidence | Observed rule |
| --- | --- |
| `.github/ai/policy.json` | `mode: documentation-only`; only `docs/features/` is allowed. |
| `.github/workflows/ai-implement.md` | Publishing permits `docs/features/**/*.md`; worker cannot add code or dependencies. |
| `AGENTS.md` | Compiler choices remain open; tasks must preserve frozen scope and report architectural decisions. |
| `docs/AI_WORKFLOWS.md` | A real executable skeleton and trusted build/test harness must be approved before enabling code implementation. |
| `.github/ISSUE_TEMPLATE/implementation.yml` | Six sections are required; acceptance criteria use identifiers such as `AC1:`. |

The inspected policy limits a task to 20 files, 128 KiB of patch data, two repair attempts and an overall 60-minute deadline. Agent executions are individually limited to 20 minutes. These are observations of the inspected revision, not promises about future configuration.

The existing procedure is: create the approved automation issue, have a maintainer apply `ai:approved`, and run **AI coordinate** on `main` with the issue number. Do not launch **AI implement** or **AI repair** directly; they require the coordinator's frozen inputs.

Sources:

- [Current workflow documentation](https://github.com/gajo357/dsharplang/blob/main/docs/AI_WORKFLOWS.md)
- [Current policy](https://github.com/gajo357/dsharplang/blob/main/.github/ai/policy.json)
- [Current worker instructions](https://github.com/gajo357/dsharplang/blob/main/.github/workflows/ai-implement.md)
- [Current repository instructions](https://github.com/gajo357/dsharplang/blob/main/AGENTS.md)

Observed content SHAs: policy `4698a32ccd1fa488397dbf020b422cdbfecd84d4`; workflow documentation `4fec845507510e1f3e6531c81456904a0f96ad10`; implementation-worker source `88e1e85a675d0c132187821b9966cd46c1d217b8`; repository instructions `2a3af4658426a635bd48249618b900011116632a`. Re-read the checked-out files before launching a task.

**Do not merely add `src/` to an allowlist.** The publisher, verifier, controller preflight and evidence requirements must agree on the executable scope. Passing the present documentation checker is not evidence that a compiler works.

## 3. First issue: ready for the current documentation workflow

Prerequisite: the two supplied files have been committed to `docs/features/` and are available at the coordinator's base revision. This task records implementation readiness; it does not adopt unresolved architecture decisions or modify the design baseline.

Use the title: **[spec] Plan the first executable D# compiler slice**.

Copy the following sections into the corresponding issue-form fields:

### Goal

Create `docs/features/DSharp_Compiler_Bootstrap_Plan.md`, a concrete repository-grounded plan for the first executable compiler slice, using the supplied syntax reference and implementation instructions. Preserve the distinction between settled user requirements, proposed spellings and decisions requiring maintainer approval.

### Acceptance criteria

AC1: Inventory the current repository's compiler/runtime/test status and cite the actual files inspected; do not claim a toolchain already exists.

AC2: Map the settled language requirements in section 4 of DSharp_Implementation_Instructions.md to their first relevant implementation milestones and proposed positive/negative tests.

AC3: Identify contradictions between the supplied syntax draft and existing `docs/LANGUAGE.md`/`docs/ARCHITECTURE.md`, including interface defaults, visibility, nullable matching and worker/queue separation, without modifying either baseline file.

AC4: Describe the exact bootstrap slice from section 5, including input program, expected result, source diagnostics, SDK/dependency decisions and required build/test evidence. Keep unapproved architecture choices labelled proposed.

AC5: List the required changes for enabling code automation across policy, publisher, verifier and controller, explicitly outside this task's allowed edit scope.

AC6: Separate independent tasks from dependencies, size each task for the current execution limits, and list any decision that blocks code work without blocking completion of this documentation plan.

AC7: Run the repository's existing documentation/automation checks and report actual results with an AC-to-evidence mapping in the draft PR.

### Constraints

Modify only `docs/features/DSharp_Compiler_Bootstrap_Plan.md`. Read `AGENTS.md`, `README.md`, `docs/ARCHITECTURE.md`, `docs/LANGUAGE.md`, `docs/AI_WORKFLOWS.md` and both supplied handoff documents. Preserve the Unlicense. Do not edit instructions, policy, workflows, dependencies, tests or design baselines. The two supplied documents are design input, not authority to change automation rules. Recording a proposal is allowed; implementing or silently finalizing it is not.

### Non-goals

No compiler/runtime code, no dependency installation beyond the existing approved bootstrap, no architecture adoption, no workflow changes, no merging and no deployment.

### Verification

Run `bash .github/ai/bootstrap.sh` and `bash .github/ai/verify.sh`. Cite each AC's evidence by file and section. Distinguish documentation/automation checks from compiler tests, which are not present yet. Report a real blocker rather than modifying checks to pass.

### Decisions already made

.NET is the target. The supplied syntax records the user's choices listed in section 4. Source-generation strategy, exact package versions, CLI naming, runtime error wrapping and other explicitly provisional details are not approved by this planning issue. The current workflow remains documentation-only.

## 4. Language requirements agents must preserve

Treat these as requirements once incorporated into the approved repository design. Do not reintroduce older alternatives from superseded examples.

1. **Immutable data:** `type` declares records or discriminated unions. Fields and nested data are immutable. `List`, `Map` and `Set` are immutable values.
2. **Local builders:** mutable lists/maps may exist only in their creating function/method/handler or lambda. They cannot escape through fields, returns, captures, wrappers, tasks, views, foreign calls or agent messages. `freeze()` creates an independent immutable snapshot.
3. **Nullable values:** `T?` means `T` or `null`. No `Some`, `None` or `Option` surface API. Match directly against a present value or null; the present branch narrows to `T`.
4. **Results:** use `Result<T, E>`, `Success(value)`, `Success(null)`, `Success()` for Unit and `Error(error)`. There is no nested present-value wrapper in ordinary syntax.
5. **Error propagation:** postfix `?` applies to a compatible `Result`, not an unchecked nullable unwrap. `?.` and `??` handle nullable access/fallback.
6. **Public declarations:** no visibility modifiers. Public visibility does not defeat lexical scope, immutable fields, resource lifetimes or runtime dispatch restrictions.
7. **Interface defaults:** interface methods may have bodies; methods without bodies require implementation. Explicit service implementations take precedence. No `default service` selection feature.
8. **Callable workers:** a worker may be called and its result awaited through the runtime. It remains an isolated invocation, not a direct method call on shared mutable state.
9. **Message-only queues:** queued agents cannot be called as workers, directly invoke `handle`, expose completion tasks or disguise the handler as a lambda. Only enqueue acceptance may be awaited by a producer.
10. **Queue contracts:** a queue message root is a declared data `type` or Unit. Nested fields must be immutable and serializable; runtime capabilities, tasks, functions and connections are not payloads.
11. **Handler outcomes:** every normal handler path reports success or error to the runtime/supervisor. Queue retry decisions are independent of returning a result to a caller.
12. **First-class lambdas:** inferred/typed parameters, multiple parameters, block bodies, function values, immutable captures, returned lambdas and async lambdas. Captures must respect lifetime and non-escape rules.
13. **Explicit composition:** handlers declare triggers; application code registers definitions and dependencies. No implicit handler discovery and no YAML application source of truth.
14. **Discoverable contracts:** compiler diagnostics identify required `handle`/`trigger` members. Trigger types determine handler input and valid policies; editor features use the same semantic contract model.
15. **Small language:** preserve braces, ordinary calls, inference, expressions and explicit failure handling. Do not add class inheritance, mutable record fields or undocumented convenience syntax.

Detailed syntax, examples and provisional choices live in the companion reference. A list here is not a replacement for reading it.

## 5. Executable bootstrap: separate coding-session brief

Your restricted workflow cannot perform this step in its present form. Use a normal repository coding session or a separately authorized maintainer-led bootstrap PR. The existing `AGENTS.md` explicitly blocks dependency and architecture changes for automated tasks; an agent must not interpret an ordinary docs-only issue as an exception.

### 5.1 Concrete implementation choices proposed for bootstrap approval

These are recommendations for a small prototype, not decisions already made by the user:

| Area | Proposed bootstrap choice |
| --- | --- |
| Compiler implementation | C# on .NET. |
| Backend | Emit readable C# and compile through the pinned .NET SDK; no direct IL backend initially. |
| Parser | Hand-written lexer and recursive-descent parser; add an expression-precedence parser when expressions expand. |
| Compiler pipeline | Source text → tokens/AST → binding/type checks → typed representation → C# emission. |
| SDK | A supported .NET 10 SDK patch, resolved and pinned during bootstrap review; retain a compatible existing approved pin if present. |
| Test harness | Prefer an approved existing test framework; otherwise propose and pin one framework plus the required .NET test adapter/SDK. |
| Prototype naming | Project/tool name `DSharp`; `.ds` fixture extension and `dsharp` command name are provisional development conventions only. |
| Runtime framework | None during bootstrap. Do not introduce Akka.NET or an actor-framework dependency by inference. |

Assignment of the brief below should explicitly accept or replace these recommendations. Exact dependency versions and scope must be recorded in the bootstrap PR before merge. The later unattended pipeline consumes that reviewed, pinned baseline rather than selecting dependencies itself.

### 5.2 Copyable bootstrap task for a normal coding session

> Bootstrap the first executable D# compiler slice in this repository. Read `AGENTS.md`, the architecture/language documents and both files under `docs/features/` first. For this bootstrap task, I approve the implementation direction in section 5.1: C# compiler, generated C# backend, a pinned .NET SDK, a hand-written parser and a real test harness. Treat the proposed fixture/CLI names as provisional. Record exact SDK/package choices and rationale in the PR. This authorization is for a normal coding session, not permission to bypass the documentation-only automation worker.
>
> Create a minimal compiler library, command-line entry point and tests using the existing repository conventions where available. Implement only the literal-function subset below. Make the compiler emit a diagnostic rather than silently accept language features outside that subset. Add reproducible build/test scripts and the tests described below. Preserve the Unlicense.
>
> In this bootstrap PR, update `README.md`, `docs/ARCHITECTURE.md`, `docs/LANGUAGE.md` and the relevant repository instructions only as necessary to accurately record the approved implementation choice, source-of-truth relationship and actual commands. Preserve the frozen language requirements and mark unimplemented features honestly. Do not rewrite existing workflow/controller/policy files; automation enablement is a separate reviewed change.
>
> Produce a draft PR with the generated program's observed output, build/test commands and results, dependency pins, and a criterion-to-test mapping. Do not merge or deploy. If the environment cannot restore approved dependencies, report that specific blocker and retain completed source work; do not claim tests passed.

### 5.3 Bootstrap feature boundary and acceptance criteria

The initial source fixture is deliberately small:

```text
module Demo

fn answer(): int {
    42
}
```

Support only a module declaration, a parameterless `fn`, `int` return annotation, braces, integer literal final expression, whitespace and single-line comments. Define that subset precisely in its implementation issue before parsing it. Larger syntax examples remain design examples until their milestones are implemented.

AC1: A clean checkout restores and builds with the approved pinned SDK and dependencies through documented commands.

AC2: The compiler parses this fixture and resolves its declared return type without delegating D# semantics to C# diagnostics.

AC3: It emits deterministic readable C# with an accessible `Demo.answer()` returning `42`. A generated-code test harness builds and invokes that method and observes `42`.

AC4: The CLI can check/compile a source file with zero exit status on success and nonzero status on failure; argument spellings are documented as provisional.

AC5: Missing braces, unknown tokens, duplicate declarations and a non-integer return expression fail with a diagnostic code and D# source span. An unsupported construct fails explicitly rather than being skipped.

AC6: Tests include a lexer test, a parser/diagnostic test, a semantic test and an execution test against generated code. Tests are discovered and run; zero tests or all-skipped tests fail the verification entry point.

AC7: Preserve the existing documentation/automation checks and record which checks validate what. No change claims queue durability, agent isolation or full syntax support.

AC8: Document compiler/backend/SDK decisions and baseline file precedence. Do not falsely relabel all draft syntax as finalized.

This task produces an executable foundation, not the full language. Dependencies and new infrastructure files are allowed only within this separately authorized bootstrap scope.

## 6. Enable code-writing automation after bootstrap

Prepare this as a separate maintainer-reviewed change using a normal coding session. It changes trusted automation and cannot be performed by a task governed by the current docs-only allowlist.

Required changes:

- Inspect all controller preflight, diff-scope checks, publisher safe-output restrictions, verifier logic, policy files, worker prompts and generated workflow locks. Update them consistently for a bounded code-task mode.
- Allow only the implementation/test/example paths needed for approved milestones. Continue protecting instruction files, workflow sources/locks, dependency manifests/locks and acceptance infrastructure from ordinary worker edits. Dependency additions use a separate reviewed route.
- Retain verification of the exact candidate SHA, frozen source specification/base revision, independent review and bounded repair attempts.
- Run the pinned .NET restore/build/test commands and retain machine-readable test evidence. Require meaningful nonzero discovery and fail on missing, skipped-required or failed tests.
- Execute candidate code/tests without publishing credentials. Preserve the separate publishing boundary and don't weaken checks to accommodate candidate code.
- Preserve the existing documentation and automation regression checks. Add policy tests proving allowed code changes succeed and forbidden harness/workflow changes are rejected.
- Distinguish worker-authored feature tests from trusted acceptance checks. Ordinary workers may add relevant tests in approved paths, but may not weaken or replace the trusted verifier or acceptance suite.
- Regenerate workflow locks using the repository's pinned tooling whenever their sources change; do not hand-edit generated files. Follow the repository's established compilation/review procedure.
- Keep draft PRs and human review/merge boundaries. Do not enable automatic merge or deployment as a side effect.

Before dispatching a real implementation task, run one positive pilot and one intentional policy-failure test. Demonstrate that a build failure, no tests discovered, stale evidence and a forbidden workflow edit each block readiness.

## 7. Implementation milestones

Each row is a milestone that may require multiple small issues. Freeze the relevant syntax/library contracts before assigning code work. Complete only the assigned slice; do not continue into a new milestone because time remains.

| Stage | Outcome | Required evidence |
| --- | --- | --- |
| M0 | Approved source-of-truth map, bootstrap choices and task boundaries. | Repository-grounded plan and explicit decisions. |
| M1 | Executable literal-function compiler/bootstrap. | Clean build, real tests, generated program returns 42. |
| M2 | Expressions, variables, declared scalar types, records, unions and immutable values. | Positive execution tests plus type/immutability diagnostics. |
| M3 | Native nullability, result propagation, exhaustive patterns and lambdas. | Correct runtime behaviour and rejection of unsafe/invalid programs. |
| M4 | Interfaces/default methods, services, binding and callable worker contracts. | Dispatch, substitution and missing-member tests; worker failure protocol frozen first. |
| M5 | Method-local mutable builders and compiler-enforced non-escape rules. | Aliasing/capture/return/interop escape rejection; snapshot independence. |
| M6 | Queue contracts, runtime dispatch and single-host durable SQLite execution. | Crash/restart, retry, claim-token, acceptance and ordering integration tests. |
| M7 | Events and scheduling over the durable machinery. | Independent deliveries, recovered fan-out, persisted schedules and fake-clock tests. |
| M8 | HTTP handlers and typed middleware. | Valid/invalid composition, failure mapping, concurrency and cleanup tests. |
| M9 | Shared contract model and initial language-server experience. | Correct completions, signature help, missing-member fixes and CLI/editor diagnostic parity. |

M2/M3 compiler foundations precede executable agents even though the original feature roadmap starts from durable work. This is an implementation dependency order, not a change to the product's runtime priorities. Develop the contract model during M2/M4; M9 exposes it in the editor.

WebSockets, additional databases, broad .NET/F# interop, AOT, distributed multi-host coordination, persistent actors, cache semantics and gRPC are not part of these first slices. Add separate approved tasks when their contracts are ready.

### M2: data and expression acceptance

- `let` bindings cannot be reassigned; immutable record fields and nested native collections cannot be mutated.
- Record construction/update preserve value semantics; a copied outer record cannot expose a mutable foreign object.
- Union cases and payload types are checked. Non-exhaustive matches fail unless an explicit catch-all covers remaining cases.
- Numeric names, overflow rules, conversions and literal suffixes follow the frozen subset. Unsupported primitives are diagnosed, not mapped arbitrarily.
- Public declarations do not require visibility modifiers; unsupported modifiers produce useful diagnostics.

### M3: nullable/result/lambda acceptance

- `string = null` fails; `string? = null` and `string? = "text"` succeed.
- Nullable matching covers `null` and a present-value arm; the value arm is non-nullable and does not swallow null, even when written first. Nested `Success(value)` follows the same rule.
- Direct optional member access fails. `?.` preserves absence and `??` evaluates its fallback lazily.
- `Success(invoice)`, `Success(null)` and `Error(error)` retain distinct behaviour without present-value wrappers.
- Postfix result `?` evaluates its operand once, returns from the nearest compatible function/lambda/handler and never crosses a lambda boundary. Incompatible error types fail.
- Function values support inferred/typed parameters, zero/multiple parameters, block bodies, immutable captures and returned lambdas. Async lambdas have the documented task result and do not imply parallel collection execution.

### M4: contracts and worker acceptance

- Required interface methods must be implemented; default bodies work without duplication; service implementations override defaults; calls from defaults reach the selected implementation.
- Conflicting defaults require explicit resolution. No class/service inheritance or default-service registration is introduced.
- Worker and queued-agent references are distinct types, including when passed through generics or stored in local variables.
- Missing `handle`/`trigger`, mismatched inputs, duplicate reserved members and unsupported policy fields produce source diagnostics based on one shared contract model.
- Worker execution is runtime-mediated with isolated invocation state. Do not implement agent calls as unrestricted access to a mutable C# instance.
- Freeze the runtime fault/timeout representation before worker-call code generation. The proposed `CallError` union is not automatically approved by this milestone list.

### M5: non-escape acceptance

Reject returning a builder directly or through a record/tuple/union/generic/nullable/result; storing it in fields; passing it to helpers/foreign APIs; capturing it in lambdas/tasks; exporting a lazy view; and sending it to another agent. Check aliases and inferred generic arguments, not just literal type names in annotations.

Allow the creating method's recognized builder operations and immutable snapshot creation. Mutating a builder after `freeze()` must not change the snapshot. Snapshot elements must also be immutable. Await suspension may retain a builder within its invocation without making it shared; concurrent capture remains invalid.

### M6: queue/runtime acceptance

- Queue roots accept declared data types or Unit; bare strings/lists/functions and runtime capabilities fail. Nested payload validation includes serialization eligibility.
- `await queuedAgent(message)`, direct `handle` access, method-group conversion and generic casts cannot bypass enqueue-only semantics.
- Enqueue returns durable acceptance only, never the handler result. Demonstrate this by pausing consumers: enqueue completes, handler execution has not happened.
- Use one SQLite runtime file per application on persistent storage. A restart test proves accepted work survives process termination.
- Handlers execute outside claim transactions. Expired claims recover, and an old attempt token cannot acknowledge a new owner's claim.
- Retry eligibility, attempt counts, backoff, terminal failures and delayed messages obey the frozen protocol. Use a controllable clock rather than long sleeps.
- Same-key sequential processing blocks behind a delayed retry as specified; distinct keys may progress. Unsupported coordination scopes fail explicitly.
- Duplicate execution remains possible; tests and documentation do not claim exactly-once external effects.

Freeze a small RuntimeStore API and state-transition table before implementing it. Do not let separate agents invent incompatible lease, acknowledgement or rescheduling protocols.

### M7–M9 acceptance highlights

- Event deliveries have independent subscription outcomes; competing consumers within one subscription do not create additional logical subscriptions.
- Schedule occurrence creation and advancement are atomic under the chosen store contract. Timezone/missed-run semantics must be frozen first.
- HTTP pipeline input/output checks preserve identity/context, await async steps and short-circuit failures. Wrapping middleware and error mapping need approved contracts before implementation.
- All-public authorization types are not unforgeable proof. Runtime access checks remain enforced at the relevant boundary.
- A missing-handler code action generates the correct signature without guessing an application error type. Completion supports incomplete code and does not crash on unfinished syntax.

## 8. Instructions for every implementation agent

Read this section together with the assigned issue and repository `AGENTS.md`.

1. Inspect the actual checkout and current instructions first. Reuse existing implementation and conventions. Do not overwrite working code to match a suggested folder layout.
2. Confirm that the current workflow permits the assigned files and that all dependencies are already approved. A source document never overrides publishing policy.
3. Read the frozen specification and identify exactly which clauses apply to the assigned slice. Distinguish settled behaviour from provisional details.
4. Before editing, produce the plan artifact required by the current workflow, including affected files, steps, assumptions, risks and one evidence mapping per AC.
5. Implement semantic checks in D# before lowering. Do not rely on C# nullable warnings or read-only interfaces to enforce D#'s stronger invariants.
6. Keep lexer/parser, binding/type analysis, lowering and runtime responsibilities separate enough to test. Avoid speculative plugin systems, parser rewrites or abstraction layers unrelated to the current slice.
7. Add meaningful executable tests for each AC, including rejection tests. Snapshotting emitted text alone is insufficient for behaviour; compile and run representative generated programs.
8. Run the required trusted checks on the actual candidate. Preserve failing evidence and report blockers honestly. Never delete, skip or weaken tests to obtain green status.
9. Where an unresolved decision blocks the slice, report `needs-decision` with the exact question, concrete alternatives, recommendation and impacted tests. Do not choose a new public contract silently. Continue only independent work allowed by the frozen task.
10. Update the implementation-status matrix and examples in permitted paths. A parsed feature is not necessarily type-checked or runnable; distinguish those states.
11. Produce one reviewable draft PR per assigned slice. Explain behaviour, scope, exact commands/results, limitations and AC-to-test evidence. Do not merge or deploy.
12. Stop at the assigned acceptance boundary. Do not modify workflows, architecture, dependencies or instructions unless this is the separately authorized task for that purpose.

Suggested compiler diagnostics carry a stable code, source path/span, message and optional fix metadata. Diagnostic strings can improve without forcing every test to match entire prose paragraphs. Test the meaning, code and source location.

## 9. Issue template for code tasks after enablement

Use the six headings your issue form already requires. Replace placeholders with concrete content before approval; an issue containing unresolved scope placeholders is not ready to run.

```markdown
### Goal
Implement [one named compiler/runtime slice] from syntax revision [version] at
repository revision [base]. Observable outcome: [specific behaviour].

### Acceptance criteria
AC1: [Positive executable behaviour and fixture.]
AC2: [Required rejection case and diagnostic.]
AC3: [Integration/code-generation behaviour and evidence.]
AC4: [Regression and trusted verification evidence.]

### Constraints
Allowed paths: [exact approved directories/files].
Dependencies: [already-approved dependencies only].
Preserve all established syntax and invocation/ownership rules.
Do not edit workflow, policy, instruction or trusted acceptance files.
Only [named features] are in scope; other syntax remains explicitly unsupported.

### Non-goals
[List excluded neighbouring features and unresolved public API decisions.]

### Verification
[Exact existing restore/build/test/verification commands.]
Map every AC to test names, fixtures and executed results.
Compile and run generated code for behavioural claims.
Use the existing plan and evidence artifacts required by the coordinator.

### Decisions already made
[Relevant frozen grammar/types/API/state transitions and approved decision links.]
No unresolved design choice is delegated implicitly to this implementation task.
```

Do not change the issue body after the coordinator freezes it and expect that change to steer the existing run. Follow the repository's restart/reconciliation procedure when scope changes.

## 10. Coordination, review and completion

The current workflow serializes repository tasks. Keep that behaviour unless a separate automation change approves parallel tasks. If later enabled, split ownership by explicit compiler/runtime interfaces and avoid concurrent writes to the same files. Parallel work must not invent different ASTs, type representations or queue protocols.

Implementation review should independently check:

- The candidate uses the frozen language contract and approved scope.
- Every AC has current evidence at the exact candidate SHA.
- Negative cases fail for the intended reason, not an unrelated parse/setup error.
- No mutable or queue-call escape was introduced through lambdas, generics or generated C#.
- Existing tests and trusted verification remain intact.
- Status documents separate implemented, tested, partial and deferred features.

A finished slice means a clean build, meaningful passing tests, accurate scope/status and a draft PR ready for the configured human review process. A documentation-only check, a successful C# build of an unrelated program or a skeleton with unimplemented methods does not satisfy language-behaviour criteria.

## 11. Decisions that must not be guessed

The following are explicit gates for later stages, not reasons to block the small bootstrap program:

- Worker runtime faults/timeouts/cancellation and the proposed `CallError` representation.
- Queue rescheduling/rejection APIs and RuntimeStore state transitions.
- Serialization versioning, migration and durable identity rules.
- Middleware wrapping, HTTP status mappings and permission context semantics.
- .NET foreign object/null/exception handling and supported package capabilities.
- Persistent/warm actor cache lifetime and distributed execution guarantees.

For each gate, prepare a short decision record with the proposed contract, one worked example, alternatives, compatibility implications and acceptance tests. Get the contract incorporated into the approved baseline before assigning dependent code work.
