# D# implementation sequence for Codex web

**Prepared:** 11 November 2026  
**Repository:** [gajo357/dsharplang](https://github.com/gajo357/dsharplang)  
**Inspected main commit:** [23fd22c65af7410ce87536b829232139462ef774](https://github.com/gajo357/dsharplang/commit/23fd22c65af7410ce87536b829232139462ef774)  
**Coverage:** all 94 current feature/design/setup issues, #7 through #100.

## 1. Execution model

Use the existing published environment in **Codex web**.

**One implementation issue → one task → one dedicated branch → one implementation plan → one PR.**

Start with sequential compiler work. Add at most two independent implementation tasks once interfaces and project boundaries have settled. This is a recommended operating limit, not a Codex product limit.

For each issue, write its plan before changing implementation code, then proceed with the scoped implementation and tests. Keep the plan and code in the same PR. Routine planning does not require another approval pause; a missing prerequisite or unresolved language/runtime decision does.

Review and merge each prerequisite PR before starting dependent implementation from updated main. Separate cloud tasks have separate workspaces; they do not automatically share another task's unmerged code.

Launch tasks directly in Codex web. The earlier API-backed AI coordinate/implement/repair workflows are not required for this execution model. Existing GitHub CI/build checks remain useful. Their time limits and file allowlists must not be described as Codex web product limits.

This file is a roadmap, not an automatic GitHub issue scheduler. It does not launch tasks, edit issues, approve new language decisions or merge PRs.

## 2. Starting point and interpretation

The current compiler implements a small C#-generating bootstrap: parameterless int-literal functions, whitespace/single-line comments, diagnostics and CLI check/compile. The inspected implementation status reports those features as tested. This planning task did not rerun the compiler tests.

Issue #7 is open and already has an approval label from the earlier workflow; its implementation is not on inspected main. All 94 tracked issues are open at this snapshot. Recheck current main, issue state and any linked PR before starting work.

Use issue numbers and dependencies, not the numeric order in GitHub's issue list. For example, #9 needs the decisions in #13 and #14 first. A planning PR being merged is not proof that its implementation children are complete.

The sequence below satisfies all explicit dependency IDs currently recorded in the issues. Additional dependencies discovered during a per-issue plan must be recorded and resolved before that issue runs.

The default order is conservative. Independent decisions can be drafted early, and optional work can move earlier when its own prerequisites are complete. Do not treat every phase as a mandatory barrier for unrelated work.

## 3. Start here

| Step | Issue | Deliverable |
| --- | --- | --- |
| 1 | [#8](https://github.com/gajo357/dsharplang/issues/8) — SET-01 | Align working instructions with Codex web, this sequence, per-issue plans and PRs. |
| 2 | [#7](https://github.com/gajo357/dsharplang/issues/7) — LANG-01 | Immutable local let bindings with executed generated-code tests. |
| 3 | [#19](https://github.com/gajo357/dsharplang/issues/19) — LANG-02 | Integer arithmetic, parentheses and checked behavior. |
| 4 | [#20](https://github.com/gajo357/dsharplang/issues/20) — LANG-03 | Typed parameters and same-module calls. |
| 5 | [#21](https://github.com/gajo357/dsharplang/issues/21) — LANG-04 | Booleans, comparisons and short-circuit logic. |
| 6 | [#22](https://github.com/gajo357/dsharplang/issues/22) — LANG-05 | Typed if/else and lexical scopes. |
| 7 | [#23](https://github.com/gajo357/dsharplang/issues/23) — LANG-06 | String and char literals. |
| 8 | [#24](https://github.com/gajo357/dsharplang/issues/24) — LANG-07 | Unit and explicit return. |

If #7 has already started, finish and merge it before applying #8's instruction changes; then continue with #19. Do not restart completed work.

## 4. Milestones and default order

Within a row, proceed left to right. Follow the detailed phase notes for parent/child issues.

| Phase | Outcome | Issue order |
| --- | --- | --- |
| P00 | Align the repository with Codex web | [#8](https://github.com/gajo357/dsharplang/issues/8) |
| P01 | Basic executable language | [#7](https://github.com/gajo357/dsharplang/issues/7) → [#19](https://github.com/gajo357/dsharplang/issues/19) → [#20](https://github.com/gajo357/dsharplang/issues/20) → [#21](https://github.com/gajo357/dsharplang/issues/21) → [#22](https://github.com/gajo357/dsharplang/issues/22) → [#23](https://github.com/gajo357/dsharplang/issues/23) → [#24](https://github.com/gajo357/dsharplang/issues/24) |
| P02 | Typed immutable data and explicit outcomes | [#12](https://github.com/gajo357/dsharplang/issues/12) → [#55](https://github.com/gajo357/dsharplang/issues/55) → [#25](https://github.com/gajo357/dsharplang/issues/25) → [#26](https://github.com/gajo357/dsharplang/issues/26) → [#27](https://github.com/gajo357/dsharplang/issues/27) → [#28](https://github.com/gajo357/dsharplang/issues/28) → [#29](https://github.com/gajo357/dsharplang/issues/29) → [#30](https://github.com/gajo357/dsharplang/issues/30) → [#31](https://github.com/gajo357/dsharplang/issues/31) → [#32](https://github.com/gajo357/dsharplang/issues/32) |
| P03 | Functions, collections and local builders | [#11](https://github.com/gajo357/dsharplang/issues/11) → [#33](https://github.com/gajo357/dsharplang/issues/33) → [#34](https://github.com/gajo357/dsharplang/issues/34) → [#35](https://github.com/gajo357/dsharplang/issues/35) → [#36](https://github.com/gajo357/dsharplang/issues/36) → [#37](https://github.com/gajo357/dsharplang/issues/37) → [#38](https://github.com/gajo357/dsharplang/issues/38) → [#39](https://github.com/gajo357/dsharplang/issues/39) → [#40](https://github.com/gajo357/dsharplang/issues/40) |
| P04 | Async, services and scoped resources | [#17](https://github.com/gajo357/dsharplang/issues/17) → [#41](https://github.com/gajo357/dsharplang/issues/41) → [#42](https://github.com/gajo357/dsharplang/issues/42) → [#43](https://github.com/gajo357/dsharplang/issues/43) → [#44](https://github.com/gajo357/dsharplang/issues/44) → [#45](https://github.com/gajo357/dsharplang/issues/45) → [#46](https://github.com/gajo357/dsharplang/issues/46) |
| P05 | Freeze runtime boundaries and add runtime projects | [#13](https://github.com/gajo357/dsharplang/issues/13) → [#14](https://github.com/gajo357/dsharplang/issues/14) → [#15](https://github.com/gajo357/dsharplang/issues/15) → [#16](https://github.com/gajo357/dsharplang/issues/16) → [#18](https://github.com/gajo357/dsharplang/issues/18) → [#9](https://github.com/gajo357/dsharplang/issues/9) |
| P06 | First callable worker | [#51](https://github.com/gajo357/dsharplang/issues/51) → [#56](https://github.com/gajo357/dsharplang/issues/56) → [#57](https://github.com/gajo357/dsharplang/issues/57) |
| P07 | Agent contracts and explicit application hosting | [#52](https://github.com/gajo357/dsharplang/issues/52) → [#53](https://github.com/gajo357/dsharplang/issues/53) → [#54](https://github.com/gajo357/dsharplang/issues/54) → [#58](https://github.com/gajo357/dsharplang/issues/58) → [#96](https://github.com/gajo357/dsharplang/issues/96) → [#100](https://github.com/gajo357/dsharplang/issues/100) → [#10](https://github.com/gajo357/dsharplang/issues/10) |
| P08 | First durable SQLite queue | [#59](https://github.com/gajo357/dsharplang/issues/59) → [#60](https://github.com/gajo357/dsharplang/issues/60) → [#78](https://github.com/gajo357/dsharplang/issues/78) → [#61](https://github.com/gajo357/dsharplang/issues/61) → [#62](https://github.com/gajo357/dsharplang/issues/62) → [#63](https://github.com/gajo357/dsharplang/issues/63) → [#64](https://github.com/gajo357/dsharplang/issues/64) |
| P09 | Reliable queue policies and operations | [#65](https://github.com/gajo357/dsharplang/issues/65) → [#66](https://github.com/gajo357/dsharplang/issues/66) → [#67](https://github.com/gajo357/dsharplang/issues/67) → [#68](https://github.com/gajo357/dsharplang/issues/68) → [#71](https://github.com/gajo357/dsharplang/issues/71) |
| P10 | Events, schedules and application configuration | [#79](https://github.com/gajo357/dsharplang/issues/79) → [#69](https://github.com/gajo357/dsharplang/issues/69) → [#70](https://github.com/gajo357/dsharplang/issues/70) → [#74](https://github.com/gajo357/dsharplang/issues/74) → [#75](https://github.com/gajo357/dsharplang/issues/75) → [#76](https://github.com/gajo357/dsharplang/issues/76) |
| P11 | Typed HTTP and authentication | [#81](https://github.com/gajo357/dsharplang/issues/81) → [#82](https://github.com/gajo357/dsharplang/issues/82) → [#83](https://github.com/gajo357/dsharplang/issues/83) → [#84](https://github.com/gajo357/dsharplang/issues/84) → [#85](https://github.com/gajo357/dsharplang/issues/85) → [#93](https://github.com/gajo357/dsharplang/issues/93) |
| P12 | Observability, testing and the observer | [#91](https://github.com/gajo357/dsharplang/issues/91) → [#94](https://github.com/gajo357/dsharplang/issues/94) → [#92](https://github.com/gajo357/dsharplang/issues/92) |
| P13 | .NET/F# interop and external resource adapters | [#87](https://github.com/gajo357/dsharplang/issues/87) → [#88](https://github.com/gajo357/dsharplang/issues/88) → [#89](https://github.com/gajo357/dsharplang/issues/89) → [#90](https://github.com/gajo357/dsharplang/issues/90) → [#77](https://github.com/gajo357/dsharplang/issues/77) → [#80](https://github.com/gajo357/dsharplang/issues/80) → [#95](https://github.com/gajo357/dsharplang/issues/95) |
| P14 | Additional language features, tooling and providers | [#48](https://github.com/gajo357/dsharplang/issues/48) → [#49](https://github.com/gajo357/dsharplang/issues/49) → [#50](https://github.com/gajo357/dsharplang/issues/50) → [#47](https://github.com/gajo357/dsharplang/issues/47) → [#97](https://github.com/gajo357/dsharplang/issues/97) → [#98](https://github.com/gajo357/dsharplang/issues/98) → [#99](https://github.com/gajo357/dsharplang/issues/99) → [#86](https://github.com/gajo357/dsharplang/issues/86) → [#72](https://github.com/gajo357/dsharplang/issues/72) → [#73](https://github.com/gajo357/dsharplang/issues/73) |

The main demonstration points are:

- **After P01:** a small program computes and branches.
- **After P02:** domain data, nullable values and Result execute correctly.
- **After P06:** a caller awaits an isolated worker.
- **After P09:** a durable queue recovers and enforces retries/order/limits.
- **After P11:** a protected HTTP route reaches its exact typed handler.
- **After P12:** the sample app can be inspected and tested operationally.

P13 and P14 are expansion tracks. They need not block the earlier demonstrations. This plan does not label a demonstration production-ready merely because one happy path works.

## 5. Detailed phases

### P00 — Align the repository with Codex web

**Order:** [#8](https://github.com/gajo357/dsharplang/issues/8) (SET-01).

**Outcome:** Codex web tasks have a clear working agreement, and the repository documentation describes the compiler that actually exists.

1. Use #8 to distinguish normal Codex web work from the earlier API-backed GitHub coordinator. Keep the build/test scripts and safety rules. Do not require AI coordinate or ai:approved merely to launch a web task.
2. Record this sequence in the repository, ideally as `docs/features/implementation/DSharp_Implementation_Sequence.md`. Add the per-issue plan and PR rules below to the working instructions through the scoped #8 PR.
3. Clarify how explicitly scoped infrastructure issues may add projects/dependencies/test commands. Ordinary feature issues must not silently expand that authority.
4. Retain the existing checkout, pinned .NET SDK and generated-C# backend. Do not restart the compiler bootstrap or create a new architecture.

**Completion check:** One reviewed documentation PR establishes the working agreement. Existing verification still passes. If #7 is already running, finish its PR before changing the instructions it is working under.

**Parallel work:** Run this before new implementation tasks. Changing repository instructions while other tasks implement against older instructions creates avoidable ambiguity.

### P01 — Basic executable language

**Order:** [#7](https://github.com/gajo357/dsharplang/issues/7) (LANG-01) → [#19](https://github.com/gajo357/dsharplang/issues/19) (LANG-02) → [#20](https://github.com/gajo357/dsharplang/issues/20) (LANG-03) → [#21](https://github.com/gajo357/dsharplang/issues/21) (LANG-04) → [#22](https://github.com/gajo357/dsharplang/issues/22) (LANG-05) → [#23](https://github.com/gajo357/dsharplang/issues/23) (LANG-06) → [#24](https://github.com/gajo357/dsharplang/issues/24) (LANG-07).

**Outcome:** A small D# program can calculate values, call functions and select a branch.

1. Implement #7 first. Replace the literal-only function body representation with the smallest statement/expression structure needed for immutable local bindings. Keep source locations throughout.
2. Add arithmetic in #19 with a real precedence-aware parser and checked integer lowering. Then add typed parameters and name-resolved calls in #20.
3. Add boolean/comparison/short-circuit semantics in #21, branch typing and nested scopes in #22, text literals in #23, and Unit/early return in #24.
4. Grow lexer, syntax, binding and emission together for each issue. Move classes into focused files when needed, but avoid a separate wholesale compiler rewrite.

**Completion check:** Generated C# executes an int-returning function using locals, arithmetic, a function call and if/else. Negative tests demonstrate invalid names/types/scopes and immutable bindings. Unit and strings work independently.

**Parallel work:** One compiler implementation at a time. #19 and #20 share a dependency, but both extend the same compiler structures; do not run them together yet.

### P02 — Typed immutable data and explicit outcomes

**Order:** [#12](https://github.com/gajo357/dsharplang/issues/12) (DEC-02) → [#55](https://github.com/gajo357/dsharplang/issues/55) (LANG-38) → [#25](https://github.com/gajo357/dsharplang/issues/25) (LANG-08) → [#26](https://github.com/gajo357/dsharplang/issues/26) (LANG-09) → [#27](https://github.com/gajo357/dsharplang/issues/27) (LANG-10) → [#28](https://github.com/gajo357/dsharplang/issues/28) (LANG-11) → [#29](https://github.com/gajo357/dsharplang/issues/29) (LANG-12) → [#30](https://github.com/gajo357/dsharplang/issues/30) (LANG-13) → [#31](https://github.com/gajo357/dsharplang/issues/31) (LANG-14) → [#32](https://github.com/gajo357/dsharplang/issues/32) (LANG-15).

**Outcome:** Programs express immutable domain data, nullable values and explicit success/error outcomes.

1. Resolve the grammar questions in #12 before generic functions and lambda-related syntax consume them. Add comment support in #55 without changing language semantics.
2. Implement records and copy updates (#25), then unions and nominal wrappers (#26). Add exhaustive matching (#27), nullable matching (#28) and nullable operators (#29).
3. Add generic substitution/inference (#30), Result construction/matching (#31), then postfix propagation (#32). Do not implement Result by string rewriting.
4. Keep the agreed nullable behavior: T? uses value/null patterns, a named nullable-value arm matches only a present value, and Success(value) needs no Some wrapper. Reject ignored Result expression statements.

**Completion check:** A generated program returns Result<Record?, Error>, distinguishes Success(record), Success(null) and Error, and propagates an error correctly. Copy updates preserve the original value. Missing match cases and unsafe null access fail before C# emission.

**Parallel work:** #12 can be researched while a P01 coding task runs, provided it changes only its own decision document. Compiler changes remain sequential. Merge the approved grammar before dependent code starts.

### P03 — Functions, collections and local builders

**Order:** [#11](https://github.com/gajo357/dsharplang/issues/11) (DEC-01) → [#33](https://github.com/gajo357/dsharplang/issues/33) (LANG-16) → [#34](https://github.com/gajo357/dsharplang/issues/34) (LANG-17) → [#35](https://github.com/gajo357/dsharplang/issues/35) (LANG-18) → [#36](https://github.com/gajo357/dsharplang/issues/36) (LANG-19) → [#37](https://github.com/gajo357/dsharplang/issues/37) (LANG-20) → [#38](https://github.com/gajo357/dsharplang/issues/38) (LANG-21) → [#39](https://github.com/gajo357/dsharplang/issues/39) (LANG-22) → [#40](https://github.com/gajo357/dsharplang/issues/40) (LANG-23).

**Outcome:** Functions can be passed and composed; collections remain immutable while local construction can be efficient.

1. Freeze the relevant collection/key/equality contracts in #11. That issue also records numeric and standard-value decisions used later; implementing all those values is not required now.
2. Implement capture-free lambdas (#33), immutable closures (#34), pipes/limited return inference (#35) and tuples (#36). Check callback arity, return scope and capture legality.
3. Implement immutable List (#37), Map/Set (#38), sequential iteration (#39), and method-local builders with non-escape checks (#40).
4. Do not expose a builder before its escape restrictions work. Cover returns, wrappers, nested containers, captures, helper calls and foreign calls; freeze must create an independent immutable snapshot.

**Completion check:** Higher-order functions and returned closures execute correctly. List/map/set transformations preserve prior values. A builder cannot escape its creating invocation, and later mutation cannot affect a frozen snapshot.

**Parallel work:** One compiler writer. A decision-document task can run separately, but must not change shared implemented contracts without review.

### P04 — Async, services and scoped resources

**Order:** [#17](https://github.com/gajo357/dsharplang/issues/17) (DEC-07) → [#41](https://github.com/gajo357/dsharplang/issues/41) (LANG-24) → [#42](https://github.com/gajo357/dsharplang/issues/42) (LANG-25) → [#43](https://github.com/gajo357/dsharplang/issues/43) (LANG-26) → [#44](https://github.com/gajo357/dsharplang/issues/44) (LANG-27) → [#45](https://github.com/gajo357/dsharplang/issues/45) (LANG-28) → [#46](https://github.com/gajo357/dsharplang/issues/46) (LANG-29).

**Outcome:** Application logic can await work, use explicit service contracts and safely scope resources.

1. Approve lifecycle/ownership decisions in #17. Then implement async functions/await (#41) and async lambdas (#42), including typed failures and local state across suspension.
2. Implement interfaces/services (#43), then default bodies for individual interface methods (#44). Test calls from a default body to the selected implementation and conflict resolution across interfaces.
3. Implement scoped using and non-escape (#45), including success, early return, fault and cancellation cleanup. Disposal is not transaction rollback.
4. Implement multi-file modules/imports (#46). Imports resolve names; they do not register handlers or initialize services.

**Completion check:** A multi-file program calls an async service through an interface, uses a default method and disposes a scoped resource on every tested exit. Builder/resource ownership remains enforced across await and callbacks.

**Parallel work:** Keep compiler and shared type-system changes sequential. Default method bodies are an implementation feature, not a default-service registration mechanism.

### P05 — Freeze runtime boundaries and add runtime projects

**Order:** [#13](https://github.com/gajo357/dsharplang/issues/13) (DEC-03) → [#14](https://github.com/gajo357/dsharplang/issues/14) (DEC-04) → [#15](https://github.com/gajo357/dsharplang/issues/15) (DEC-05) → [#16](https://github.com/gajo357/dsharplang/issues/16) (DEC-06) → [#18](https://github.com/gajo357/dsharplang/issues/18) (DEC-08) → [#9](https://github.com/gajo357/dsharplang/issues/9) (SET-02).

**Outcome:** Runtime implementation has approved public contracts, project boundaries and executable test infrastructure.

1. Resolve worker-call outcomes (#13), durable queue/store transitions (#14), HTTP composition/authorization (#15), safe interop boundaries (#16), and cron/WebSocket lifecycle sections (#18). Each design issue produces a focused decision-record PR.
2. Record exact types/state transitions and representative positive/negative cases. Separate application Error from runtime fault, cancellation and timeout; settle the provisional CallError shape before code depends on it.
3. Use #9 to add the approved runtime projects, references, pinned dependencies and trusted tests. Include controlled time/randomness. Prove a deliberately failing test fails verification before removing the deliberate failure.
4. Keep infrastructure changes scoped to #9. Normal runtime features consume the resulting contracts and commands instead of changing project policy as a side effect.

**Completion check:** The needed decision records are accepted and merged. Runtime projects build and their tests run in the environment and CI. There is no dependency on an API-billed coding coordinator.

**Parallel work:** Draft at most two decision records concurrently if their assumptions are explicit. Reconcile cross-cutting contracts before merging. Make #9 the only writer of project files/locks/test infrastructure.

### P06 — First callable worker

**Order:** [#51](https://github.com/gajo357/dsharplang/issues/51) (LANG-34) → [#56](https://github.com/gajo357/dsharplang/issues/56) (RUN-01) → [#57](https://github.com/gajo357/dsharplang/issues/57) (RUN-02).

**Outcome:** The first end-to-end callable worker works.

1. Implement checked worker declarations and the shared semantic contract in #51.
2. Connect compiled worker references/call lowering to fresh runtime invocations in #56. Include the necessary compiler-to-runtime bridge in the issue plan; declaration parsing alone is insufficient.
3. Add fault, timeout and cancellation behavior in #57, using the approved #13 contract. Test concurrent calls and late completions.
4. Use a minimal explicit test host to exercise a worker with a fake service dependency. The full app declaration/hosting flow follows in P07.

**Completion check:** A generated D# caller awaits a worker and receives its typed outcome. Concurrent invocations do not share mutable per-request state, and one ordinary handler failure does not terminate unrelated invocations.

**Parallel work:** Sequence #51, #56, #57. No queue execution or persistent actor state is required for this milestone.

### P07 — Agent contracts and explicit application hosting

**Order:** [#52](https://github.com/gajo357/dsharplang/issues/52) (LANG-35) → [#53](https://github.com/gajo357/dsharplang/issues/53) (LANG-36) → [#54](https://github.com/gajo357/dsharplang/issues/54) (LANG-37) → [#58](https://github.com/gajo357/dsharplang/issues/58) (RUN-03) → [#96](https://github.com/gajo357/dsharplang/issues/96) (TOOL-01) → [#100](https://github.com/gajo357/dsharplang/issues/100) (TOOL-05) → [#10](https://github.com/gajo357/dsharplang/issues/10) (SET-03).

**Outcome:** Explicit application registration can validate and start a host without executing business logic during registration.

1. Split #52 into trigger-specific child issues: Queue, Event, Cron and HTTP. Each shares one compiler contract model but has its own plan/PR and the appropriate approved decision dependencies.
2. Complete the applicable trigger-contract children before policy validation (#53), code-based composition (#54) and host startup/shutdown (#58). For the conservative default sequence, finish all four children before #54; an earlier queue-only composition requires an explicitly narrowed child issue.
3. Add consistent contract diagnostics (#96) and an application inventory generated from metadata (#100). Unknown members/policies are errors; tooling must not invent an error type.
4. Use #10 for a project/setup map and concrete optional-subsystem setup child issues. Merge each setup child immediately before its subsystem starts; do not install every future dependency now.

**Completion check:** An explicit app can register definitions, validate dependencies and identities, initialize resources, invoke a worker and shut down. A queue reference cannot be called or expose .handle as a callback. Inventory generation does not run handlers.

**Parallel work:** Keep contract, policy and composition compiler edits sequential. Runtime host work can overlap a read-only review. Do not run optional setup children concurrently if they edit common solution/lock/CI files.

### P08 — First durable SQLite queue

**Order:** [#59](https://github.com/gajo357/dsharplang/issues/59) (RUN-04) → [#60](https://github.com/gajo357/dsharplang/issues/60) (RUN-05) → [#78](https://github.com/gajo357/dsharplang/issues/78) (LIB-01) → [#61](https://github.com/gajo357/dsharplang/issues/61) (RUN-06) → [#62](https://github.com/gajo357/dsharplang/issues/62) (RUN-07) → [#63](https://github.com/gajo357/dsharplang/issues/63) (RUN-08) → [#64](https://github.com/gajo357/dsharplang/issues/64) (RUN-09).

**Outcome:** A typed message survives a restart and is executed through an ownership-checked durable queue.

1. Define atomic RuntimeStore operations and the provider conformance harness (#59), then SQLite schema/migrations (#60). Implement typed JSON/contract encoding (#78).
2. Add committed enqueue acceptance (#61), atomic claims/lease tokens (#62), and owned completion/rescheduling with stale-ack rejection (#63).
3. Connect messages to fresh handler execution and supervisor outcomes (#64). Execute handlers outside claim transactions.
4. Test persistence and recovery at each transaction boundary. Include malformed/unsupported payloads and crashes before/after commit. Never equate a returned enqueue receipt with successful handler completion.

**Completion check:** Enqueue a declared immutable data message, restart the host, execute the handler and record its outcome. An expired owner cannot acknowledge a newer attempt. No exactly-once external-effect guarantee is claimed.

**Parallel work:** After #59's shared contract is merged, #60 and #78 may run concurrently only with disjoint implementation/test ownership and established project references. Otherwise run the listed order.

### P09 — Reliable queue policies and operations

**Order:** [#65](https://github.com/gajo357/dsharplang/issues/65) (RUN-10) → [#66](https://github.com/gajo357/dsharplang/issues/66) (RUN-11) → [#67](https://github.com/gajo357/dsharplang/issues/67) (RUN-12) → [#68](https://github.com/gajo357/dsharplang/issues/68) (RUN-13) → [#71](https://github.com/gajo357/dsharplang/issues/71) (RUN-16).

**Outcome:** Queue processing has enforceable retries, ordering, limits and operator controls.

1. Add retry/delay/retained failures (#65), then per-key completion ordering and concurrency (#66), then capacity/cancellation (#67).
2. Add atomic completion with same-store follow-up enqueue (#68), then typed inspection/pause/resume/retry/abandon controls (#71).
3. Keep a delayed or retried head blocking its sequential key until an approved terminal/skip outcome. Other keys may progress.
4. Require explicit provider capability checks and test restart/ownership races. Operator controls need the approved access policy; no public admin endpoint is enabled merely because control methods exist.

**Completion check:** Controlled-time tests prove attempts, backoff, failure retention and per-key order across retries/recovery. Capacity is bounded and stale controls cannot corrupt current ownership. Follow-up work commits atomically within the same RuntimeStore.

**Parallel work:** Sequence store transition/claim changes. Documentation or read-only review can overlap. Do not parallelize two tickets that change the same eligibility/ownership invariants.

### P10 — Events, schedules and application configuration

**Order:** [#79](https://github.com/gajo357/dsharplang/issues/79) (LIB-02) → [#69](https://github.com/gajo357/dsharplang/issues/69) (RUN-14) → [#70](https://github.com/gajo357/dsharplang/issues/70) (RUN-15) → [#74](https://github.com/gajo357/dsharplang/issues/74) (APP-01) → [#75](https://github.com/gajo357/dsharplang/issues/75) (APP-02) → [#76](https://github.com/gajo357/dsharplang/issues/76) (APP-03).

**Outcome:** The application can react to durable events, run schedules and initialize from typed configuration/secrets.

1. Implement the needed immutable standard values in #79. Split value families if necessary; scheduling requires the approved time/Duration representations.
2. Implement recoverable event fan-out (#69), then cron occurrence creation (#70). Each subscription has independent delivery; schedule advancement and occurrence enqueue must commit together.
3. Implement typed configuration (#74), asynchronous secret initialization (#75), and single-attempt lazy initialization/resource replacement (#76).
4. Keep configuration precedence and refresh semantics explicit. Reject invalid required settings before admitting dependent work; redact secrets in diagnostics.

**Completion check:** Multiple subscriptions recover independently after restart. Missed cron behavior and DST are deterministic under the chosen contract. Required resources are ready before consumers start, and concurrent lazy users share one initialization attempt.

**Parallel work:** Events and configuration are good candidates for two simultaneous tasks after their dependencies are merged, if they touch separate source/test files. Cron still waits for its time types and store operations.

### P11 — Typed HTTP and authentication

**Order:** [#81](https://github.com/gajo357/dsharplang/issues/81) (HTTP-01) → [#82](https://github.com/gajo357/dsharplang/issues/82) (HTTP-02) → [#83](https://github.com/gajo357/dsharplang/issues/83) (HTTP-03) → [#84](https://github.com/gajo357/dsharplang/issues/84) (HTTP-04) → [#85](https://github.com/gajo357/dsharplang/issues/85) (HTTP-05) → [#93](https://github.com/gajo357/dsharplang/issues/93) (OPS-03).

**Outcome:** A real HTTP request passes through typed authentication/authorization/decoding into the exact handler input.

1. Complete the HTTP-specific #10 setup child, then HTTP hosting (#81), typed path/query/body binding (#82), transformation pipelines (#83), wrapping middleware (#84), and runtime auth checks (#85).
2. Preserve identity/context through transformations. Await async steps and stop on typed failure through explicit error mappings. Wrappers handle downstream cleanup, including faults/cancellation.
3. Enforce access at runtime: a publicly constructible Authorized-shaped record is not sufficient proof that authentication or permission checks occurred.
4. Finish health/readiness and bounded graceful draining (#93), including shutdown during HTTP and queued work.

**Completion check:** An integration host serves a protected typed route, rejects missing/invalid/denied credentials, preserves context through decoding and maps failures explicitly. Cleanup and shutdown complete according to the approved lifecycle.

**Parallel work:** Keep HTTP hosting/pipeline/auth implementation sequential while their shared contracts settle. A mature queue subsystem can be reviewed in parallel.

### P12 — Observability, testing and the observer

**Order:** [#91](https://github.com/gajo357/dsharplang/issues/91) (OPS-01) → [#94](https://github.com/gajo357/dsharplang/issues/94) (OPS-04) → [#92](https://github.com/gajo357/dsharplang/issues/92) (OPS-02).

**Outcome:** The application is observable and testable, with protected operational controls.

1. Split #91 into a telemetry contract and bounded instrumentation/export implementation children. Propagate correlation across request/worker/enqueue boundaries without leaking payloads or secrets.
2. Split #94 into a public testing API decision, worker/queue helpers and HTTP helpers. Reuse the existing deterministic runtime test primitives.
3. Split #92 into the observer contract, read-only views and authorized control views. Reuse #71 APIs; do not write store tables directly from the dashboard.
4. Keep the observer and exporters optional. Their failures must not redefine business outcomes.

**Completion check:** A sample request and its queued work can be correlated, tested with fake dependencies/time, and inspected through a protected observer. Read and control permissions are tested separately.

**Parallel work:** After the shared telemetry/test API decisions merge, independent adapters or read-only UI work can run two at a time. Control UI waits for authorization and typed operation APIs.

### P13 — .NET/F# interop and external resource adapters

**Order:** [#87](https://github.com/gajo357/dsharplang/issues/87) (INT-01) → [#88](https://github.com/gajo357/dsharplang/issues/88) (INT-02) → [#89](https://github.com/gajo357/dsharplang/issues/89) (INT-03) → [#90](https://github.com/gajo357/dsharplang/issues/90) (INT-04) → [#77](https://github.com/gajo357/dsharplang/issues/77) (APP-04) → [#80](https://github.com/gajo357/dsharplang/issues/80) (LIB-03) → [#95](https://github.com/gajo357/dsharplang/issues/95) (OPS-05).

**Outcome:** Selected C# and F# packages can be used through a defined safe boundary.

1. Complete interop-specific project setup, then package resolution (#87), safe C# value/method adapters (#88), task/error/delegate adapters (#89), and the initial F# subset (#90).
2. Use pinned fixture packages to test actual calls, nulls, exceptions, mutable aliases, callbacks and async cancellation. Generated C# compiling is not sufficient proof of D# ownership safety.
3. Split #77 into database and filesystem adapters. Share database pools, scope connections/transactions and enforce filesystem capabilities.
4. Split #80 into an outbound HTTP contract and minimal implementation. Use #95 to design business-data transaction/outbox integration; concrete drivers/relay implementation follow only after that design.
5. Internal runtime libraries may already call C# dependencies from earlier setup issues. This phase adds the user-facing D# interoperability surface.

**Completion check:** A D# program safely calls supported C# and F# APIs. Explicit adapters preserve nullable/result semantics and resource ownership. Database/filesystem/HTTP adapter tests exercise failure and cleanup. No blanket NuGet support is claimed.

**Parallel work:** Database and filesystem adapter children can run concurrently after their contracts and dependencies merge. Package resolution and shared interop mapping changes remain sequential.

### P14 — Additional language features, tooling and providers

**Order:** [#48](https://github.com/gajo357/dsharplang/issues/48) (LANG-31) → [#49](https://github.com/gajo357/dsharplang/issues/49) (LANG-32) → [#50](https://github.com/gajo357/dsharplang/issues/50) (LANG-33) → [#47](https://github.com/gajo357/dsharplang/issues/47) (LANG-30) → [#97](https://github.com/gajo357/dsharplang/issues/97) (TOOL-02) → [#98](https://github.com/gajo357/dsharplang/issues/98) (TOOL-03) → [#99](https://github.com/gajo357/dsharplang/issues/99) (TOOL-04) → [#86](https://github.com/gajo357/dsharplang/issues/86) (HTTP-06) → [#72](https://github.com/gajo357/dsharplang/issues/72) (RUN-17) → [#73](https://github.com/gajo357/dsharplang/issues/73) (RUN-18).

**Outcome:** Additional capabilities expand the working system without delaying the earlier milestones.

1. Finish integer widths/literals (#48), fractional numeric types (#49), interpolation (#50), and extensions (#47) in their listed dependency order.
2. Complete tooling-specific setup and implement LSP diagnostics (#97), semantic completion/hover/signature help (#98), and required-member code actions (#99), reusing compiler contracts.
3. Split WebSockets (#86) into lifecycle and bounded ordered output work after its approved contract and transport setup.
4. Split PostgreSQL (#72) and MySQL (#73) providers into schema, enqueue/claims, transitions and multi-host conformance children. Run the same provider contract tests, plus contention/recovery tests on the actual database.
5. Complete optional setup children before starting each package; no need to wait for unrelated optional features.

**Completion check:** Each selected extension has its own executed evidence. Editor help agrees with compiler diagnostics. Providers preserve requested guarantees under contention and restart. Slow socket clients cannot cause unbounded output buffering.

**Parallel work:** Potentially two independent tracks: language/compiler versus an established provider; LSP versus a provider; PostgreSQL versus MySQL after their shared contract is frozen. Do not parallelize common project/lockfile setup.

## 6. Parent issues and smaller PRs

Several existing issues explicitly ask to be split. Treat them as coordination issues; do not ask one Codex task to implement all their subfeatures.

Preserve the one-PR-per-issue convention as follows:

1. Give the existing parent a focused planning/contract PR containing its implementation plan, child boundaries and dependencies.
2. Create a separate issue for each approved implementation slice. Every child gets its own plan, branch and PR.
3. Use `Refs #PARENT` in a coordination PR. Do not close the parent just because its plan merged.
4. Close the parent only when its required children and integration acceptance checks are complete. No additional catch-all code PR is needed.
5. Dependencies on a parent mean the relevant implementation children are merged. Record those exact child links in the dependent issue.

| Existing issue | Required split or refinement |
| --- | --- |
| [#10](https://github.com/gajo357/dsharplang/issues/10) SET-03 | Separate setup children for HTTP, interop/adapters, tooling and each additional provider. Choose exact projects/dependencies/commands per child. |
| [#18](https://github.com/gajo357/dsharplang/issues/18) DEC-08 | Scheduling and WebSocket contracts can be reviewed independently within the design PR; split into decision children if review becomes too broad. |
| [#52](https://github.com/gajo357/dsharplang/issues/52) LANG-35 | Separate Queue, Event, Cron and HTTP compiler-contract children. All reuse one semantic model. |
| [#72](https://github.com/gajo357/dsharplang/issues/72) RUN-17; [#73](https://github.com/gajo357/dsharplang/issues/73) RUN-18 | Separate migrations/schema, enqueue/claims, transitions and multi-host conformance per provider. |
| [#77](https://github.com/gajo357/dsharplang/issues/77) APP-04 | Separate database and filesystem adapters. Choose one database driver for the first database child. |
| [#79](https://github.com/gajo357/dsharplang/issues/79) LIB-02 | Split uuid, time/duration and Bytes families if the selected APIs exceed a focused implementation. |
| [#80](https://github.com/gajo357/dsharplang/issues/80) LIB-03 | Outbound HTTP contract first; then minimal client adapter. Circuit breaker remains a later design issue. |
| [#86](https://github.com/gajo357/dsharplang/issues/86) HTTP-06 | Socket lifecycle first; bounded output buffering/order next. |
| [#91](https://github.com/gajo357/dsharplang/issues/91) OPS-01 | Telemetry contract first; then scoped instrumentation/correlation and exporter adapters. |
| [#92](https://github.com/gajo357/dsharplang/issues/92) OPS-02 | Observer contract, read-only views, then authorized control views. |
| [#94](https://github.com/gajo357/dsharplang/issues/94) OPS-04 | Testing API contract, worker/queue helpers, then HTTP helpers. |
| [#95](https://github.com/gajo357/dsharplang/issues/95) OPS-05 | Design only; select a concrete driver/outbox relay in later implementation children. |

If any other issue grows beyond one cohesive change, split it during planning. Do not expose an unsafe partial builder, interop boundary or queue transition merely to keep the PR small.

## 7. Rules for parallel work

**Use one compiler implementation task at a time through the foundational phases.** The current lexer, parser, binder and emitter share a small core; separate branches prevent workspace interference but do not prevent incompatible designs or merge conflicts.

Two implementation tasks may run together only when:

- Their prerequisites are merged.
- Neither consumes unmerged work from the other.
- Their source/test ownership and interfaces are clearly separate.
- Neither is changing shared project files, dependency locks or test infrastructure.
- Both plans name any shared files. Minor status-document overlap is resolved explicitly during integration; shared semantic changes require sequencing.

| Combination | Decision |
| --- | --- |
| #19 arithmetic + #20 function calls | Sequential initially; both change compiler expression/binding structures. |
| A compiler feature + a decision-only document in a different file | Usually suitable, provided the code task does not depend on the unapproved decision. |
| #60 SQLite schema + #78 JSON after #59 | Possible after project/contracts are stable and file/test ownership is disjoint. |
| #69 events + #74 configuration | Possible after P09, if their implementation and tests are independent. |
| A read-only PR review + an independent implementation | Suitable; review a recorded PR commit and re-review later fixes. |
| PostgreSQL + MySQL provider children | Possible after shared store contracts/conformance tests and separate project setup are merged. |
| Two jobs changing the same queue claim/ordering state machine | Sequential. |
| Two setup jobs editing solution, lockfiles or CI | Sequential. |

Merge parallel PRs one at a time. Bring the second branch up to date, resolve conflicts, rerun affected checks and review the resulting diff. A passing result from before a conflicting merge is not sufficient evidence for the updated branch.

Do not use stacked PRs by default. If deliberately introduced later, record their base branches and merge order; “separate PR” does not mean independent code.

## 8. Per-issue implementation lifecycle

### A. Select a ready issue

Read current main and the issue, check for an existing PR/task, and confirm merged prerequisites. Read the applicable AGENTS.md, syntax/feature specifications and implementation status. Do not infer readiness from an issue number or an approval label alone.

Start a fresh task in the existing environment with the issue URL. Use a dedicated branch such as `codex/issue-19-int-arithmetic`; if a task PR already exists, continue that task/branch rather than creating a competing implementation.

### B. Write the implementation plan

Create `docs/features/implementation/issue-N-plan.md` before editing implementation code. Include the issue URL, current base commit, relevant decisions, exact scope, expected behavior, implementation steps, files/interfaces affected and an AC-to-test map.

List unresolved language/API/protocol decisions explicitly. A design issue can propose alternatives; an implementation issue must not silently choose a new public contract. Routine internal implementation choices do not require a new decision gate.

### C. Implement the feature end to end

For compiler work: extend lexical/parser structures, bind and check semantics, then lower/emit C#. Add tests proving execution and tests proving rejection for the intended reason. Preserve public compiler entry points unless the issue explicitly approves changing them.

For runtime work: implement the smallest approved behavior through a real host/store boundary. Keep compiler validation, runtime validity and durable store guarantees distinct. Use deterministic clocks/barriers for concurrency tests.

Keep the scope narrow. Refactor only as needed to make this feature correct and maintainable. Do not write a complete parser, runtime framework or universal abstraction in anticipation of later issues.

### D. Verify and update the plan

Run the issue's actual checks, fix failures within scope and record outcomes. Update the plan with completed steps, deviations and any remaining limitation. Do not rewrite acceptance criteria to make an incomplete implementation pass.

Update `docs/features/DSharp_Implementation_Status.md` to describe implemented and tested behavior precisely. A parsed construct without working checking/emission is partial, not finished.

### E. Open one PR and review it

The PR contains the plan, implementation, focused tests and relevant example/status updates. Its description includes:

- The concrete behavior change and issue link.
- A link to the plan.
- Each acceptance criterion and its test/evidence.
- Actual commands/results and meaningful limitations.
- `Closes #N` only when this PR completes that implementation issue.

Open a draft while checks or review remain. Use a separate read-only Codex review task for critical ownership, concurrency, durability, auth or interop changes. Ask it to inspect the PR diff against the issue/plan and identify concrete correctness gaps. Apply fixes in the original task and rerun affected checks.

If direct PR publication is unavailable in a task, report it clearly and use the web task's available PR publishing action. Do not describe a local branch/diff as a created GitHub PR.

### F. Merge and advance

The user reviews and merges; implementation tasks do not auto-merge or start another feature. After merge, start the next dependent task from updated main and verify the prerequisite code is present.

The default handoff is explicit. No API-backed orchestrator or unattended dependency scheduler is assumed.

## 9. Per-issue plan template

Copy this structure into `docs/features/implementation/issue-N-plan.md`:

```markdown
# Implementation plan: #N — Title

## Scope and baseline
- Issue URL:
- Base commit:
- Prerequisites and merged PRs:
- Applicable specifications/decision records:
- In scope:
- Out of scope:

## Expected behavior
One small positive example and its observable result.
The main invalid examples and expected diagnostics/outcomes.

## Implementation steps
1. Relevant compiler/runtime structures to change.
2. Semantics and boundary checks to enforce.
3. Lowering/runtime integration.
4. Tests, examples and status documentation.

## Files and interfaces
List planned source/test/doc files and shared contracts.
State whether another in-flight task overlaps them.

## Acceptance evidence
| AC | Planned test or check | Actual result |
| --- | --- | --- |
| AC1 | Named behavior and test | Pending |

## Verification
Exact repository commands, plus focused feature/integration checks.

## Decisions and blockers
Record unresolved public contracts; do not hide assumptions.

## Progress and deviations
Update as work proceeds. Mark steps completed or blocked.

## PR
PR link, review fixes and final verification commit.
```

The template does not add a separate plan-only PR for ordinary implementation issues. The plan is part of that issue's implementation PR. Design/coordination issues naturally have a document-focused PR.

## 10. Verification requirements

The current repository instructs compiler work to run:

```bash
bash .github/ai/bootstrap.sh
bash .github/ai/verify.sh
bash scripts/verify-compiler.sh
```

The scripts are repository checks; using them does not mean launching the AI coordinate workflow.

Put new compiler feature tests in `tests/features/` under the existing mechanism. Preserve prior tests and the pinned SDK/dependency setup. Retain generated-C# execution tests for observable behavior, not only emitted-text snapshots. A negative test must fail for the intended type/ownership/contract rule, not an earlier unrelated parse error.

Runtime/optional project setup issues must define and verify their additional restore/build/test commands before feature work uses them. Add provider integration tests against the actual selected database. Do not claim those tests ran if the environment lacked the required service.

| Feature class | Required evidence |
| --- | --- |
| Language syntax/types | Valid parse/check/execute plus intended invalid diagnostics with source spans. |
| Nullable/Result/matching | Present/null/error cases, exhaustive coverage and correct propagation scope. |
| Builders/resources/interop | Escape and aliasing rejection, independent snapshots, lifetime/cleanup tests. |
| Worker runtime | Concurrent isolation, application error, fault, timeout/cancellation and late completion. |
| Durable queue/store | Commit-boundary crashes, restart, stale tokens, contention and supported atomicity. |
| Events/cron | Partial fan-out recovery, independent subscribers, timezone/DST/missed-run and duplicate prevention rules. |
| HTTP/auth | Invalid inputs, context preservation, explicit failure mapping, runtime permission checks and cancellation cleanup. |
| Tooling | Protocol/edit tests and agreement with compiler contracts on incomplete/invalid source. |

Passing CI plus a readable PR is the completion boundary for review; merge remains an explicit step.

## 11. Architectural guardrails

- Keep the existing C# implementation, generated-C# backend and pinned managed .NET toolchain.
- Use one shared semantic model for compiler diagnostics, registration metadata, inventories and editor assistance.
- Enforce D# semantics before emitting C#; successful CLR compilation alone does not prove immutability or legal agent invocation.
- Public-only source declarations do not mean runtime-owned handles are freely constructible/callable. Generated implementation details may use CLR mechanics without adding source-language visibility keywords.
- Data type fields and nested collections remain immutable. Builders are method-local; freeze snapshots cannot alias later mutation.
- Only workers provide awaited handler results. Queued handlers report outcomes to the supervisor; enqueue completion reports durable acceptance.
- Queue/event/scheduled payload roots are declared data types or Unit, with recursively immutable serializable data.
- RuntimeStore owns atomic runtime operations. Business databases are separate unless a specific approved transaction protocol connects them.
- Start with one SQLite runtime file per application and honest single-host guarantees.
- Typed auth envelopes carry context; runtime checks enforce access.
- Default interface bodies implement individual methods. Service selection remains explicit in application composition.
- gRPC, persistent actor/cache semantics, general streaming, Native AOT and blanket NuGet compatibility remain outside the current milestones.

## 12. Complete issue-to-phase and dependency index

Dependencies below are translated from the current issue bodies into actual GitHub issue numbers. They are not live edits to those issues. Parent issues additionally require the relevant child implementation/setup work described above.

| Phase | Issue | Feature | Recorded dependencies |
| --- | --- | --- | --- |
| P00 | [#8](https://github.com/gajo357/dsharplang/issues/8) · SET-01 | Reconcile planning documents with the implemented bootstrap | Bootstrap |
| P01 | [#7](https://github.com/gajo357/dsharplang/issues/7) · LANG-01 | Implement immutable local let bindings | Bootstrap |
| P01 | [#19](https://github.com/gajo357/dsharplang/issues/19) · LANG-02 | Implement int arithmetic and parentheses | [#7](https://github.com/gajo357/dsharplang/issues/7) |
| P01 | [#20](https://github.com/gajo357/dsharplang/issues/20) · LANG-03 | Implement typed parameters and same-module function calls | [#7](https://github.com/gajo357/dsharplang/issues/7) |
| P01 | [#21](https://github.com/gajo357/dsharplang/issues/21) · LANG-04 | Implement bool values, comparisons and short-circuit logic | [#19](https://github.com/gajo357/dsharplang/issues/19), [#20](https://github.com/gajo357/dsharplang/issues/20) |
| P01 | [#22](https://github.com/gajo357/dsharplang/issues/22) · LANG-05 | Implement if/else expressions and nested lexical scopes | [#21](https://github.com/gajo357/dsharplang/issues/21) |
| P01 | [#23](https://github.com/gajo357/dsharplang/issues/23) · LANG-06 | Implement string and char literals | [#20](https://github.com/gajo357/dsharplang/issues/20) |
| P01 | [#24](https://github.com/gajo357/dsharplang/issues/24) · LANG-07 | Implement Unit and explicit return | [#22](https://github.com/gajo357/dsharplang/issues/22) |
| P02 | [#12](https://github.com/gajo357/dsharplang/issues/12) · DEC-02 | Freeze advanced grammar and symbol-resolution rules | Bootstrap |
| P02 | [#55](https://github.com/gajo357/dsharplang/issues/55) · LANG-38 | Implement block comments and attached documentation comments | [#12](https://github.com/gajo357/dsharplang/issues/12) |
| P02 | [#25](https://github.com/gajo357/dsharplang/issues/25) · LANG-08 | Implement immutable record types and copy updates | [#23](https://github.com/gajo357/dsharplang/issues/23), [#24](https://github.com/gajo357/dsharplang/issues/24) |
| P02 | [#26](https://github.com/gajo357/dsharplang/issues/26) · LANG-09 | Implement discriminated unions and nominal domain wrappers | [#25](https://github.com/gajo357/dsharplang/issues/25) |
| P02 | [#27](https://github.com/gajo357/dsharplang/issues/27) · LANG-10 | Implement exhaustive match and guards | [#26](https://github.com/gajo357/dsharplang/issues/26), [#21](https://github.com/gajo357/dsharplang/issues/21) |
| P02 | [#28](https://github.com/gajo357/dsharplang/issues/28) · LANG-11 | Implement native nullable types and direct matching | [#27](https://github.com/gajo357/dsharplang/issues/27) |
| P02 | [#29](https://github.com/gajo357/dsharplang/issues/29) · LANG-12 | Implement nullable fallback and conditional field access | [#28](https://github.com/gajo357/dsharplang/issues/28) |
| P02 | [#30](https://github.com/gajo357/dsharplang/issues/30) · LANG-13 | Implement generic data and generic functions | [#26](https://github.com/gajo357/dsharplang/issues/26), [#20](https://github.com/gajo357/dsharplang/issues/20), [#12](https://github.com/gajo357/dsharplang/issues/12) |
| P02 | [#31](https://github.com/gajo357/dsharplang/issues/31) · LANG-14 | Implement Result and Success/Error constructors | [#30](https://github.com/gajo357/dsharplang/issues/30), [#28](https://github.com/gajo357/dsharplang/issues/28), [#24](https://github.com/gajo357/dsharplang/issues/24) |
| P02 | [#32](https://github.com/gajo357/dsharplang/issues/32) · LANG-15 | Implement postfix Result propagation | [#31](https://github.com/gajo357/dsharplang/issues/31), [#24](https://github.com/gajo357/dsharplang/issues/24) |
| P03 | [#11](https://github.com/gajo357/dsharplang/issues/11) · DEC-01 | Freeze extended primitives and standard-value semantics | Bootstrap |
| P03 | [#33](https://github.com/gajo357/dsharplang/issues/33) · LANG-16 | Implement function types and capture-free lambdas | [#20](https://github.com/gajo357/dsharplang/issues/20), [#24](https://github.com/gajo357/dsharplang/issues/24), [#12](https://github.com/gajo357/dsharplang/issues/12) |
| P03 | [#34](https://github.com/gajo357/dsharplang/issues/34) · LANG-17 | Implement immutable closure capture and returned closures | [#33](https://github.com/gajo357/dsharplang/issues/33), [#25](https://github.com/gajo357/dsharplang/issues/25) |
| P03 | [#35](https://github.com/gajo357/dsharplang/issues/35) · LANG-18 | Implement value piping and simple return-type inference | [#33](https://github.com/gajo357/dsharplang/issues/33), [#20](https://github.com/gajo357/dsharplang/issues/20), [#12](https://github.com/gajo357/dsharplang/issues/12) |
| P03 | [#36](https://github.com/gajo357/dsharplang/issues/36) · LANG-19 | Implement tuples and tuple destructuring | [#25](https://github.com/gajo357/dsharplang/issues/25), [#33](https://github.com/gajo357/dsharplang/issues/33) |
| P03 | [#37](https://github.com/gajo357/dsharplang/issues/37) · LANG-20 | Implement immutable List values and core operations | [#30](https://github.com/gajo357/dsharplang/issues/30), [#33](https://github.com/gajo357/dsharplang/issues/33), [#11](https://github.com/gajo357/dsharplang/issues/11) |
| P03 | [#38](https://github.com/gajo357/dsharplang/issues/38) · LANG-21 | Implement immutable Map and Set values | [#37](https://github.com/gajo357/dsharplang/issues/37), [#11](https://github.com/gajo357/dsharplang/issues/11) |
| P03 | [#39](https://github.com/gajo357/dsharplang/issues/39) · LANG-22 | Implement sequential for/in iteration | [#37](https://github.com/gajo357/dsharplang/issues/37), [#24](https://github.com/gajo357/dsharplang/issues/24) |
| P03 | [#40](https://github.com/gajo357/dsharplang/issues/40) · LANG-23 | Implement method-local mutable builders and escape checks | [#38](https://github.com/gajo357/dsharplang/issues/38), [#34](https://github.com/gajo357/dsharplang/issues/34), [#39](https://github.com/gajo357/dsharplang/issues/39) |
| P04 | [#17](https://github.com/gajo357/dsharplang/issues/17) · DEC-07 | Freeze resource, configuration and startup lifecycle | Bootstrap |
| P04 | [#41](https://github.com/gajo357/dsharplang/issues/41) · LANG-24 | Implement async functions, Task and await | [#32](https://github.com/gajo357/dsharplang/issues/32), [#20](https://github.com/gajo357/dsharplang/issues/20), [#12](https://github.com/gajo357/dsharplang/issues/12), [#40](https://github.com/gajo357/dsharplang/issues/40) |
| P04 | [#42](https://github.com/gajo357/dsharplang/issues/42) · LANG-25 | Implement async lambdas | [#41](https://github.com/gajo357/dsharplang/issues/41), [#34](https://github.com/gajo357/dsharplang/issues/34) |
| P04 | [#43](https://github.com/gajo357/dsharplang/issues/43) · LANG-26 | Implement interfaces and explicitly implemented services | [#30](https://github.com/gajo357/dsharplang/issues/30), [#41](https://github.com/gajo357/dsharplang/issues/41) |
| P04 | [#44](https://github.com/gajo357/dsharplang/issues/44) · LANG-27 | Implement default interface method bodies | [#43](https://github.com/gajo357/dsharplang/issues/43) |
| P04 | [#45](https://github.com/gajo357/dsharplang/issues/45) · LANG-28 | Implement scoped using and resource non-escape | [#41](https://github.com/gajo357/dsharplang/issues/41), [#43](https://github.com/gajo357/dsharplang/issues/43), [#17](https://github.com/gajo357/dsharplang/issues/17) |
| P04 | [#46](https://github.com/gajo357/dsharplang/issues/46) · LANG-29 | Implement multi-file modules and imports | [#20](https://github.com/gajo357/dsharplang/issues/20), [#12](https://github.com/gajo357/dsharplang/issues/12) |
| P05 | [#13](https://github.com/gajo357/dsharplang/issues/13) · DEC-03 | Freeze worker invocation and failure contracts | Bootstrap |
| P05 | [#14](https://github.com/gajo357/dsharplang/issues/14) · DEC-04 | Freeze durable queue and store protocols | Bootstrap |
| P05 | [#15](https://github.com/gajo357/dsharplang/issues/15) · DEC-05 | Freeze HTTP composition and authorization contracts | Bootstrap |
| P05 | [#16](https://github.com/gajo357/dsharplang/issues/16) · DEC-06 | Freeze safe CLR and NuGet interoperability | Bootstrap |
| P05 | [#18](https://github.com/gajo357/dsharplang/issues/18) · DEC-08 | Freeze scheduling and WebSocket lifecycle contracts | Bootstrap |
| P05 | [#9](https://github.com/gajo357/dsharplang/issues/9) · SET-02 | Enable bounded runtime projects and trusted runtime tests | [#13](https://github.com/gajo357/dsharplang/issues/13), [#14](https://github.com/gajo357/dsharplang/issues/14) |
| P06 | [#51](https://github.com/gajo357/dsharplang/issues/51) · LANG-34 | Implement worker declaration contracts | [#43](https://github.com/gajo357/dsharplang/issues/43), [#31](https://github.com/gajo357/dsharplang/issues/31), [#41](https://github.com/gajo357/dsharplang/issues/41), [#13](https://github.com/gajo357/dsharplang/issues/13) |
| P06 | [#56](https://github.com/gajo357/dsharplang/issues/56) · RUN-01 | Execute isolated callable worker invocations | [#9](https://github.com/gajo357/dsharplang/issues/9), [#51](https://github.com/gajo357/dsharplang/issues/51) |
| P06 | [#57](https://github.com/gajo357/dsharplang/issues/57) · RUN-02 | Handle worker faults, deadlines and cancellation | [#56](https://github.com/gajo357/dsharplang/issues/56), [#13](https://github.com/gajo357/dsharplang/issues/13) |
| P07 | [#52](https://github.com/gajo357/dsharplang/issues/52) · LANG-35 | Implement triggered-agent contracts and message restrictions | [#51](https://github.com/gajo357/dsharplang/issues/51), [#40](https://github.com/gajo357/dsharplang/issues/40), [#14](https://github.com/gajo357/dsharplang/issues/14) |
| P07 | [#53](https://github.com/gajo357/dsharplang/issues/53) · LANG-36 | Implement typed execution-policy declarations | [#52](https://github.com/gajo357/dsharplang/issues/52), [#14](https://github.com/gajo357/dsharplang/issues/14) |
| P07 | [#54](https://github.com/gajo357/dsharplang/issues/54) · LANG-37 | Implement code-based application composition | [#46](https://github.com/gajo357/dsharplang/issues/46), [#52](https://github.com/gajo357/dsharplang/issues/52), [#53](https://github.com/gajo357/dsharplang/issues/53), [#17](https://github.com/gajo357/dsharplang/issues/17) |
| P07 | [#58](https://github.com/gajo357/dsharplang/issues/58) · RUN-03 | Start and stop explicitly registered applications | [#9](https://github.com/gajo357/dsharplang/issues/9), [#54](https://github.com/gajo357/dsharplang/issues/54), [#17](https://github.com/gajo357/dsharplang/issues/17) |
| P07 | [#96](https://github.com/gajo357/dsharplang/issues/96) · TOOL-01 | Expose consistent compiler diagnostics and status | [#52](https://github.com/gajo357/dsharplang/issues/52) |
| P07 | [#100](https://github.com/gajo357/dsharplang/issues/100) · TOOL-05 | Generate application inventory from registration metadata | [#54](https://github.com/gajo357/dsharplang/issues/54), [#58](https://github.com/gajo357/dsharplang/issues/58) |
| P07 | [#10](https://github.com/gajo357/dsharplang/issues/10) · SET-03 | Enable optional package and language-server test projects | [#9](https://github.com/gajo357/dsharplang/issues/9) |
| P08 | [#59](https://github.com/gajo357/dsharplang/issues/59) · RUN-04 | Implement RuntimeStore contracts and conformance harness | [#9](https://github.com/gajo357/dsharplang/issues/9), [#14](https://github.com/gajo357/dsharplang/issues/14) |
| P08 | [#60](https://github.com/gajo357/dsharplang/issues/60) · RUN-05 | Implement SQLite runtime schema and migrations | [#59](https://github.com/gajo357/dsharplang/issues/59) |
| P08 | [#78](https://github.com/gajo357/dsharplang/issues/78) · LIB-01 | Implement typed JSON encoding and decoding | [#9](https://github.com/gajo357/dsharplang/issues/9), [#38](https://github.com/gajo357/dsharplang/issues/38), [#28](https://github.com/gajo357/dsharplang/issues/28), [#31](https://github.com/gajo357/dsharplang/issues/31), [#14](https://github.com/gajo357/dsharplang/issues/14) |
| P08 | [#61](https://github.com/gajo357/dsharplang/issues/61) · RUN-06 | Implement durable enqueue and acceptance receipts | [#60](https://github.com/gajo357/dsharplang/issues/60), [#78](https://github.com/gajo357/dsharplang/issues/78), [#52](https://github.com/gajo357/dsharplang/issues/52) |
| P08 | [#62](https://github.com/gajo357/dsharplang/issues/62) · RUN-07 | Implement atomic claims and expiring ownership | [#61](https://github.com/gajo357/dsharplang/issues/61) |
| P08 | [#63](https://github.com/gajo357/dsharplang/issues/63) · RUN-08 | Implement completion and stale-ack rejection | [#62](https://github.com/gajo357/dsharplang/issues/62) |
| P08 | [#64](https://github.com/gajo357/dsharplang/issues/64) · RUN-09 | Connect queue execution to typed handler outcomes | [#63](https://github.com/gajo357/dsharplang/issues/63), [#56](https://github.com/gajo357/dsharplang/issues/56), [#53](https://github.com/gajo357/dsharplang/issues/53) |
| P09 | [#65](https://github.com/gajo357/dsharplang/issues/65) · RUN-10 | Implement retries, delays and retained failures | [#64](https://github.com/gajo357/dsharplang/issues/64) |
| P09 | [#66](https://github.com/gajo357/dsharplang/issues/66) · RUN-11 | Implement per-key sequential ordering and concurrency | [#65](https://github.com/gajo357/dsharplang/issues/65) |
| P09 | [#67](https://github.com/gajo357/dsharplang/issues/67) · RUN-12 | Implement queue capacity and cooperative cancellation | [#66](https://github.com/gajo357/dsharplang/issues/66) |
| P09 | [#68](https://github.com/gajo357/dsharplang/issues/68) · RUN-13 | Implement atomic completion with follow-up enqueue | [#63](https://github.com/gajo357/dsharplang/issues/63) |
| P09 | [#71](https://github.com/gajo357/dsharplang/issues/71) · RUN-16 | Implement queue inspection and operator controls | [#67](https://github.com/gajo357/dsharplang/issues/67), [#15](https://github.com/gajo357/dsharplang/issues/15) |
| P10 | [#79](https://github.com/gajo357/dsharplang/issues/79) · LIB-02 | Implement standard immutable time, identity and binary values | [#9](https://github.com/gajo357/dsharplang/issues/9), [#11](https://github.com/gajo357/dsharplang/issues/11), [#78](https://github.com/gajo357/dsharplang/issues/78) |
| P10 | [#69](https://github.com/gajo357/dsharplang/issues/69) · RUN-14 | Implement recoverable event fan-out | [#64](https://github.com/gajo357/dsharplang/issues/64), [#68](https://github.com/gajo357/dsharplang/issues/68), [#52](https://github.com/gajo357/dsharplang/issues/52) |
| P10 | [#70](https://github.com/gajo357/dsharplang/issues/70) · RUN-15 | Implement durable cron occurrence creation | [#68](https://github.com/gajo357/dsharplang/issues/68), [#18](https://github.com/gajo357/dsharplang/issues/18), [#79](https://github.com/gajo357/dsharplang/issues/79) |
| P10 | [#74](https://github.com/gajo357/dsharplang/issues/74) · APP-01 | Implement typed configuration loading | [#58](https://github.com/gajo357/dsharplang/issues/58), [#78](https://github.com/gajo357/dsharplang/issues/78), [#17](https://github.com/gajo357/dsharplang/issues/17) |
| P10 | [#75](https://github.com/gajo357/dsharplang/issues/75) · APP-02 | Implement asynchronous secret-provider initialization | [#74](https://github.com/gajo357/dsharplang/issues/74), [#17](https://github.com/gajo357/dsharplang/issues/17) |
| P10 | [#76](https://github.com/gajo357/dsharplang/issues/76) · APP-03 | Implement lazy resource initialization and replacement | [#75](https://github.com/gajo357/dsharplang/issues/75) |
| P11 | [#81](https://github.com/gajo357/dsharplang/issues/81) · HTTP-01 | Host explicitly registered HTTP agents | [#10](https://github.com/gajo357/dsharplang/issues/10), [#58](https://github.com/gajo357/dsharplang/issues/58), [#52](https://github.com/gajo357/dsharplang/issues/52), [#15](https://github.com/gajo357/dsharplang/issues/15) |
| P11 | [#82](https://github.com/gajo357/dsharplang/issues/82) · HTTP-02 | Implement typed path, query and JSON body binding | [#81](https://github.com/gajo357/dsharplang/issues/81), [#78](https://github.com/gajo357/dsharplang/issues/78) |
| P11 | [#83](https://github.com/gajo357/dsharplang/issues/83) · HTTP-03 | Implement typed asynchronous transformation pipelines | [#82](https://github.com/gajo357/dsharplang/issues/82), [#33](https://github.com/gajo357/dsharplang/issues/33), [#43](https://github.com/gajo357/dsharplang/issues/43) |
| P11 | [#84](https://github.com/gajo357/dsharplang/issues/84) · HTTP-04 | Implement wrapping middleware and cleanup | [#83](https://github.com/gajo357/dsharplang/issues/83), [#45](https://github.com/gajo357/dsharplang/issues/45) |
| P11 | [#85](https://github.com/gajo357/dsharplang/issues/85) · HTTP-05 | Implement runtime authentication and authorization checks | [#84](https://github.com/gajo357/dsharplang/issues/84), [#15](https://github.com/gajo357/dsharplang/issues/15) |
| P11 | [#93](https://github.com/gajo357/dsharplang/issues/93) · OPS-03 | Implement health checks and bounded graceful draining | [#58](https://github.com/gajo357/dsharplang/issues/58), [#67](https://github.com/gajo357/dsharplang/issues/67), [#81](https://github.com/gajo357/dsharplang/issues/81) |
| P12 | [#91](https://github.com/gajo357/dsharplang/issues/91) · OPS-01 | Implement correlated structured logs, metrics and tracing | [#10](https://github.com/gajo357/dsharplang/issues/10), [#64](https://github.com/gajo357/dsharplang/issues/64), [#81](https://github.com/gajo357/dsharplang/issues/81) |
| P12 | [#94](https://github.com/gajo357/dsharplang/issues/94) · OPS-04 | Provide handler and application testing harnesses | [#10](https://github.com/gajo357/dsharplang/issues/10), [#64](https://github.com/gajo357/dsharplang/issues/64), [#83](https://github.com/gajo357/dsharplang/issues/83) |
| P12 | [#92](https://github.com/gajo357/dsharplang/issues/92) · OPS-02 | Implement protected runtime observer dashboard | [#71](https://github.com/gajo357/dsharplang/issues/71), [#85](https://github.com/gajo357/dsharplang/issues/85), [#91](https://github.com/gajo357/dsharplang/issues/91) |
| P13 | [#87](https://github.com/gajo357/dsharplang/issues/87) · INT-01 | Implement explicit NuGet reference and package resolution | [#10](https://github.com/gajo357/dsharplang/issues/10), [#46](https://github.com/gajo357/dsharplang/issues/46), [#16](https://github.com/gajo357/dsharplang/issues/16) |
| P13 | [#88](https://github.com/gajo357/dsharplang/issues/88) · INT-02 | Implement the safe C# value and method adapter subset | [#87](https://github.com/gajo357/dsharplang/issues/87), [#43](https://github.com/gajo357/dsharplang/issues/43), [#45](https://github.com/gajo357/dsharplang/issues/45) |
| P13 | [#89](https://github.com/gajo357/dsharplang/issues/89) · INT-03 | Implement CLR async, error and delegate adapters | [#88](https://github.com/gajo357/dsharplang/issues/88), [#42](https://github.com/gajo357/dsharplang/issues/42) |
| P13 | [#90](https://github.com/gajo357/dsharplang/issues/90) · INT-04 | Implement the initial F# NuGet adapter subset | [#89](https://github.com/gajo357/dsharplang/issues/89) |
| P13 | [#77](https://github.com/gajo357/dsharplang/issues/77) · APP-04 | Implement scoped database and filesystem capabilities | [#10](https://github.com/gajo357/dsharplang/issues/10), [#45](https://github.com/gajo357/dsharplang/issues/45), [#75](https://github.com/gajo357/dsharplang/issues/75), [#16](https://github.com/gajo357/dsharplang/issues/16) |
| P13 | [#80](https://github.com/gajo357/dsharplang/issues/80) · LIB-03 | Design and implement an outbound HTTP client adapter | [#10](https://github.com/gajo357/dsharplang/issues/10), [#16](https://github.com/gajo357/dsharplang/issues/16), [#17](https://github.com/gajo357/dsharplang/issues/17), [#45](https://github.com/gajo357/dsharplang/issues/45) |
| P13 | [#95](https://github.com/gajo357/dsharplang/issues/95) · OPS-05 | Design business-data transactions and outbox integration | [#14](https://github.com/gajo357/dsharplang/issues/14), [#17](https://github.com/gajo357/dsharplang/issues/17), [#16](https://github.com/gajo357/dsharplang/issues/16) |
| P14 | [#48](https://github.com/gajo357/dsharplang/issues/48) · LANG-31 | Implement integer widths and extended integer literals | [#19](https://github.com/gajo357/dsharplang/issues/19), [#11](https://github.com/gajo357/dsharplang/issues/11) |
| P14 | [#49](https://github.com/gajo357/dsharplang/issues/49) · LANG-32 | Implement float32, float and decimal | [#48](https://github.com/gajo357/dsharplang/issues/48), [#11](https://github.com/gajo357/dsharplang/issues/11) |
| P14 | [#50](https://github.com/gajo357/dsharplang/issues/50) · LANG-33 | Implement string interpolation | [#23](https://github.com/gajo357/dsharplang/issues/23), [#11](https://github.com/gajo357/dsharplang/issues/11) |
| P14 | [#47](https://github.com/gajo357/dsharplang/issues/47) · LANG-30 | Implement statically resolved extensions | [#46](https://github.com/gajo357/dsharplang/issues/46), [#43](https://github.com/gajo357/dsharplang/issues/43) |
| P14 | [#97](https://github.com/gajo357/dsharplang/issues/97) · TOOL-02 | Implement language-server document diagnostics | [#10](https://github.com/gajo357/dsharplang/issues/10), [#96](https://github.com/gajo357/dsharplang/issues/96) |
| P14 | [#98](https://github.com/gajo357/dsharplang/issues/98) · TOOL-03 | Implement semantic completion, hover and signature help | [#97](https://github.com/gajo357/dsharplang/issues/97), [#53](https://github.com/gajo357/dsharplang/issues/53), [#33](https://github.com/gajo357/dsharplang/issues/33) |
| P14 | [#99](https://github.com/gajo357/dsharplang/issues/99) · TOOL-04 | Implement required-members code actions | [#98](https://github.com/gajo357/dsharplang/issues/98) |
| P14 | [#86](https://github.com/gajo357/dsharplang/issues/86) · HTTP-06 | Implement typed WebSocket connection handling | [#81](https://github.com/gajo357/dsharplang/issues/81), [#18](https://github.com/gajo357/dsharplang/issues/18) |
| P14 | [#72](https://github.com/gajo357/dsharplang/issues/72) · RUN-17 | Implement PostgreSQL RuntimeStore provider | [#10](https://github.com/gajo357/dsharplang/issues/10), [#59](https://github.com/gajo357/dsharplang/issues/59), [#71](https://github.com/gajo357/dsharplang/issues/71) |
| P14 | [#73](https://github.com/gajo357/dsharplang/issues/73) · RUN-18 | Implement MySQL RuntimeStore provider | [#10](https://github.com/gajo357/dsharplang/issues/10), [#59](https://github.com/gajo357/dsharplang/issues/59), [#71](https://github.com/gajo357/dsharplang/issues/71) |

## 13. Reusable Codex web task prompt

Replace ISSUE_NUMBER and send this as a new task in the existing environment:

```text
Implement https://github.com/gajo357/dsharplang/issues/ISSUE_NUMBER.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-ISSUE_NUMBER-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #ISSUE_NUMBER only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

## 14. Source references

- [Current implementation status](https://github.com/gajo357/dsharplang/blob/main/docs/features/DSharp_Implementation_Status.md)
- [Current compiler](https://github.com/gajo357/dsharplang/blob/main/src/DSharp.Compiler/Compiler.cs)
- [Repository working instructions](https://github.com/gajo357/dsharplang/blob/main/AGENTS.md)
- [Syntax reference](https://github.com/gajo357/dsharplang/blob/main/docs/features/DSharp_Syntax_Reference.md)
- [Backend feature specification](https://github.com/gajo357/dsharplang/blob/main/docs/design/FEATURES.md)
- [GitHub issues](https://github.com/gajo357/dsharplang/issues)
- [Official Codex Cloud documentation](https://learn.chatgpt.com/docs/cloud)
- [Official cloud environment documentation](https://learn.chatgpt.com/docs/environments/cloud-environments)

This sequence is based on the repository/issue snapshot above. Reconcile it after each merged milestone. A closed issue or an old plan must not override the actual code, current acceptance criteria or a later approved decision.

