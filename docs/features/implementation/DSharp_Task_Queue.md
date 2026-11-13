# D# Codex web task queue

Source: [Implementation sequence](https://github.com/gajo357/dsharplang/blob/main/docs/features/implementation/DSharp_Implementation_Sequence.md).

These task briefs are prepared prompts, not launched Codex tasks. Launch one foundational implementation task at a time. Check current main, existing tasks/PRs and merged prerequisites before each launch; do not use the original snapshot's next candidate as a current dispatch decision. Follow the source sequence for dependency gates and parent issue splits. [#121](https://github.com/gajo357/dsharplang/issues/121) tracks explicit Option<T> separately from [#30](https://github.com/gajo357/dsharplang/issues/30); syntax acceptance alone does not satisfy nullable-generic execution criteria.

The [pipes, partial application and logging specification](../DSharp_Pipes_and_Logging.md) extends the target contracts. The new tasks at the end of this queue track that work; their presence does not claim implementation. Preserve completed #35's historical plain-pipe scope. Runtime setup and the remaining design dependencies still apply.

## P00 — Align the repository with Codex web

### #8 — SET-01: Reconcile planning documents with the implemented bootstrap

Issue: https://github.com/gajo357/dsharplang/issues/8

Plan: `docs/features/implementation/issue-8-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/8.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-8-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #8 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

## P01 — Basic executable language

### #7 — [spec] Implement immutable local let bindings

Issue: https://github.com/gajo357/dsharplang/issues/7

Plan: `docs/features/implementation/issue-7-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/7.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-7-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #7 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #19 — LANG-02: Implement int arithmetic and parentheses

Issue: https://github.com/gajo357/dsharplang/issues/19

Plan: `docs/features/implementation/issue-19-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/19.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-19-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #19 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #20 — LANG-03: Implement typed parameters and same-module function calls

Issue: https://github.com/gajo357/dsharplang/issues/20

Plan: `docs/features/implementation/issue-20-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/20.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-20-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #20 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #21 — LANG-04: Implement bool values, comparisons and short-circuit logic

Issue: https://github.com/gajo357/dsharplang/issues/21

Plan: `docs/features/implementation/issue-21-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/21.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-21-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #21 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #22 — LANG-05: Implement if/else expressions and nested lexical scopes

Issue: https://github.com/gajo357/dsharplang/issues/22

Plan: `docs/features/implementation/issue-22-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/22.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-22-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #22 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #23 — LANG-06: Implement string and char literals

Issue: https://github.com/gajo357/dsharplang/issues/23

Plan: `docs/features/implementation/issue-23-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/23.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-23-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #23 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #24 — LANG-07: Implement Unit and explicit return

Issue: https://github.com/gajo357/dsharplang/issues/24

Plan: `docs/features/implementation/issue-24-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/24.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-24-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #24 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

## P02 — Typed immutable data and explicit outcomes

### #12 — DEC-02: Freeze advanced grammar and symbol-resolution rules

Issue: https://github.com/gajo357/dsharplang/issues/12

Plan: `docs/features/implementation/issue-12-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/12.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-12-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #12 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #55 — LANG-38: Implement block comments and attached documentation comments

Issue: https://github.com/gajo357/dsharplang/issues/55

Plan: `docs/features/implementation/issue-55-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/55.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-55-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #55 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #25 — LANG-08: Implement immutable record types and copy updates

Issue: https://github.com/gajo357/dsharplang/issues/25

Plan: `docs/features/implementation/issue-25-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/25.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-25-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #25 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #26 — LANG-09: Implement discriminated unions and nominal domain wrappers

Issue: https://github.com/gajo357/dsharplang/issues/26

Plan: `docs/features/implementation/issue-26-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/26.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-26-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #26 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #27 — LANG-10: Implement exhaustive match and guards

Issue: https://github.com/gajo357/dsharplang/issues/27

Plan: `docs/features/implementation/issue-27-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/27.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-27-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #27 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #28 — LANG-11: Implement native nullable types and direct matching

Issue: https://github.com/gajo357/dsharplang/issues/28

Plan: `docs/features/implementation/issue-28-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/28.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-28-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #28 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #29 — LANG-12: Implement nullable fallback and conditional field access

Issue: https://github.com/gajo357/dsharplang/issues/29

Plan: `docs/features/implementation/issue-29-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/29.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-29-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #29 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #30 — LANG-13: Implement generic data and generic functions

Issue: https://github.com/gajo357/dsharplang/issues/30

Plan: `docs/features/implementation/issue-30-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/30.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-30-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #30 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #121 — Explicit Option<T>

Issue: https://github.com/gajo357/dsharplang/issues/121

Implement `None` and `Some(T)` as distinct tagged cases, with exhaustive matching and equality. Reject `Some(null)` for non-nullable `T`; preserve `Some(null)` for nullable `T` and preserve nested options. Do not implicitly convert between `Option<T>` and `T?`, or expose `None` as a default value. Verify calls and stored data; alias declarations and collection support are future work where not implemented. JSON serialization and HTTP binding are not decided.

### #31 — LANG-14: Implement Result and Success/Error constructors

Issue: https://github.com/gajo357/dsharplang/issues/31

Plan: `docs/features/implementation/issue-31-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/31.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-31-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #31 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #32 — LANG-15: Implement postfix Result propagation

Issue: https://github.com/gajo357/dsharplang/issues/32

Plan: `docs/features/implementation/issue-32-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/32.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-32-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #32 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

## P03 — Functions, collections and local builders

### #11 — DEC-01: Freeze extended primitives and standard-value semantics

Issue: https://github.com/gajo357/dsharplang/issues/11

Plan: `docs/features/implementation/issue-11-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/11.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-11-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #11 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #33 — LANG-16: Implement function types and capture-free lambdas

Issue: https://github.com/gajo357/dsharplang/issues/33

Plan: `docs/features/implementation/issue-33-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/33.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-33-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #33 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #34 — LANG-17: Implement immutable closure capture and returned closures

Issue: https://github.com/gajo357/dsharplang/issues/34

Plan: `docs/features/implementation/issue-34-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/34.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-34-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #34 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #35 — LANG-18: Implement value piping and simple return-type inference

Issue: https://github.com/gajo357/dsharplang/issues/35

Plan: `docs/features/implementation/issue-35-plan.md`

Historical completed plain-pipe scope. Do not reopen this task for partial application or Result helpers; use #129/#135 and #130/#136. Retain the no-implicit-await, unwrap or tuple-unpacking behavior.

```text
Implement https://github.com/gajo357/dsharplang/issues/35.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-35-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #35 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #36 — LANG-19: Implement tuples and tuple destructuring

Issue: https://github.com/gajo357/dsharplang/issues/36

Plan: `docs/features/implementation/issue-36-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/36.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-36-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #36 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #37 — LANG-20: Implement immutable List values and core operations

Issue: https://github.com/gajo357/dsharplang/issues/37

Plan: `docs/features/implementation/issue-37-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/37.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-37-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #37 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #38 — LANG-21: Implement immutable Map and Set values

Issue: https://github.com/gajo357/dsharplang/issues/38

Plan: `docs/features/implementation/issue-38-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/38.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-38-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #38 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #39 — LANG-22: Implement sequential for/in iteration

Issue: https://github.com/gajo357/dsharplang/issues/39

Plan: `docs/features/implementation/issue-39-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/39.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-39-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #39 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #40 — LANG-23: Implement method-local mutable builders and escape checks

Issue: https://github.com/gajo357/dsharplang/issues/40

Plan: `docs/features/implementation/issue-40-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/40.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-40-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #40 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

## P04 — Async, services and scoped resources

### #17 — DEC-07: Freeze resource, configuration and startup lifecycle

Issue: https://github.com/gajo357/dsharplang/issues/17

Plan: `docs/features/implementation/issue-17-plan.md`

Include the host-scoped Logger lifecycle: bootstrap console during initialization, configured outputs before admission, and Logger teardown after producers drain but before the shared RuntimeStore closes. Arbitrary package handlers still require explicit registration.

```text
Implement https://github.com/gajo357/dsharplang/issues/17.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-17-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #17 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #41 — LANG-24: Implement async functions, Task and await

Issue: https://github.com/gajo357/dsharplang/issues/41

Plan: `docs/features/implementation/issue-41-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/41.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-41-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #41 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #42 — LANG-25: Implement async lambdas

Issue: https://github.com/gajo357/dsharplang/issues/42

Plan: `docs/features/implementation/issue-42-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/42.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-42-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #42 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #43 — LANG-26: Implement interfaces and explicitly implemented services

Issue: https://github.com/gajo357/dsharplang/issues/43

Plan: `docs/features/implementation/issue-43-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/43.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-43-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #43 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #44 — LANG-27: Implement default interface method bodies

Issue: https://github.com/gajo357/dsharplang/issues/44

Plan: `docs/features/implementation/issue-44-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/44.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-44-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #44 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #45 — LANG-28: Implement scoped using and resource non-escape

Issue: https://github.com/gajo357/dsharplang/issues/45

Plan: `docs/features/implementation/issue-45-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/45.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-45-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #45 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #46 — LANG-29: Implement multi-file modules and imports

Issue: https://github.com/gajo357/dsharplang/issues/46

Plan: `docs/features/implementation/issue-46-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/46.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-46-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #46 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

## P05 — Freeze runtime boundaries and add runtime projects

### #13 — DEC-03: Freeze worker invocation and failure contracts

Issue: https://github.com/gajo357/dsharplang/issues/13

Plan: `docs/features/implementation/issue-13-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/13.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-13-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #13 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #14 — DEC-04: Freeze durable queue and store protocols

Issue: https://github.com/gajo357/dsharplang/issues/14

Plan: `docs/features/implementation/issue-14-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/14.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-14-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #14 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #15 — DEC-05: Freeze HTTP composition and authorization contracts

Issue: https://github.com/gajo357/dsharplang/issues/15

Plan: `docs/features/implementation/issue-15-plan.md`

Reconcile the remaining transport and authorization contracts with `pipeline = callable` and ordinary Result helpers. The new specification fixes composition syntax; it does not finalize HTTP types, status mappings or authorization evidence.

```text
Implement https://github.com/gajo357/dsharplang/issues/15.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-15-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #15 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #16 — DEC-06: Freeze safe CLR and NuGet interoperability

Issue: https://github.com/gajo357/dsharplang/issues/16

Plan: `docs/features/implementation/issue-16-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/16.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-16-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #16 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #18 — DEC-08: Freeze scheduling and WebSocket lifecycle contracts

Issue: https://github.com/gajo357/dsharplang/issues/18

Plan: `docs/features/implementation/issue-18-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/18.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-18-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #18 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #9 — SET-02: Enable bounded runtime projects and trusted runtime tests

Issue: https://github.com/gajo357/dsharplang/issues/9

Plan: `docs/features/implementation/issue-9-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/9.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-9-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #9 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

## P06 — First callable worker

### #51 — LANG-34: Implement worker declaration contracts

Issue: https://github.com/gajo357/dsharplang/issues/51

Plan: `docs/features/implementation/issue-51-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/51.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-51-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #51 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #56 — RUN-01: Execute isolated callable worker invocations

Issue: https://github.com/gajo357/dsharplang/issues/56

Plan: `docs/features/implementation/issue-56-plan.md`

Track `call(worker)` as a callable adapter preserving runtime dispatch, deadlines, concurrency and the complete worker-call error contract. No direct `.handle` dispatch or queued-agent request/reply shortcut.

```text
Implement https://github.com/gajo357/dsharplang/issues/56.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-56-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #56 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #57 — RUN-02: Handle worker faults, deadlines and cancellation

Issue: https://github.com/gajo357/dsharplang/issues/57

Plan: `docs/features/implementation/issue-57-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/57.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-57-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #57 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

## P07 — Agent contracts and explicit application hosting

### #52 — LANG-35: Implement triggered-agent contracts and message restrictions

Issue: https://github.com/gajo357/dsharplang/issues/52

Plan: `docs/features/implementation/issue-52-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/52.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-52-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #52 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #53 — LANG-36: Implement typed execution-policy declarations

Issue: https://github.com/gajo357/dsharplang/issues/53

Plan: `docs/features/implementation/issue-53-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/53.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-53-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #53 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #54 — LANG-37: Implement code-based application composition

Issue: https://github.com/gajo357/dsharplang/issues/54

Plan: `docs/features/implementation/issue-54-plan.md`

Include the explicit standard-system-actor exception for Logger and startup logging recipes. This does not enable automatic registration of application or package handlers.

```text
Implement https://github.com/gajo357/dsharplang/issues/54.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-54-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #54 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #58 — RUN-03: Start and stop explicitly registered applications

Issue: https://github.com/gajo357/dsharplang/issues/58

Plan: `docs/features/implementation/issue-58-plan.md`

Register Logger once per host using the actor runtime. No configuration means Info plus console only; outputs and scopes are host-isolated. Follow #132 for bounded delivery and lifecycle details.

```text
Implement https://github.com/gajo357/dsharplang/issues/58.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-58-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #58 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #96 — TOOL-01: Expose consistent compiler diagnostics and status

Issue: https://github.com/gajo357/dsharplang/issues/96

Plan: `docs/features/implementation/issue-96-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/96.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-96-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #96 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #100 — TOOL-05: Generate application inventory from registration metadata

Issue: https://github.com/gajo357/dsharplang/issues/100

Plan: `docs/features/implementation/issue-100-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/100.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-100-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #100 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #10 — SET-03: Enable optional package and language-server test projects

Issue: https://github.com/gajo357/dsharplang/issues/10

Plan: `docs/features/implementation/issue-10-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/10.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-10-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #10 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

## P08 — First durable SQLite queue

### #59 — RUN-04: Implement RuntimeStore contracts and conformance harness

Issue: https://github.com/gajo357/dsharplang/issues/59

Plan: `docs/features/implementation/issue-59-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/59.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-59-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #59 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #60 — RUN-05: Implement SQLite runtime schema and migrations

Issue: https://github.com/gajo357/dsharplang/issues/60

Plan: `docs/features/implementation/issue-60-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/60.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-60-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #60 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #78 — LIB-01: Implement typed JSON encoding and decoding

Issue: https://github.com/gajo357/dsharplang/issues/78

Plan: `docs/features/implementation/issue-78-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/78.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-78-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #78 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #61 — RUN-06: Implement durable enqueue and acceptance receipts

Issue: https://github.com/gajo357/dsharplang/issues/61

Plan: `docs/features/implementation/issue-61-plan.md`

Track `enqueue(agent)` as a callable adapter returning the existing asynchronous durable acceptance Result and receipt, never the handler result. Awaiting success still waits for the durable commit.

```text
Implement https://github.com/gajo357/dsharplang/issues/61.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-61-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #61 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #62 — RUN-07: Implement atomic claims and expiring ownership

Issue: https://github.com/gajo357/dsharplang/issues/62

Plan: `docs/features/implementation/issue-62-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/62.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-62-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #62 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #63 — RUN-08: Implement completion and stale-ack rejection

Issue: https://github.com/gajo357/dsharplang/issues/63

Plan: `docs/features/implementation/issue-63-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/63.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-63-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #63 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #64 — RUN-09: Connect queue execution to typed handler outcomes

Issue: https://github.com/gajo357/dsharplang/issues/64

Plan: `docs/features/implementation/issue-64-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/64.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-64-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #64 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

## P09 — Reliable queue policies and operations

### #65 — RUN-10: Implement retries, delays and retained failures

Issue: https://github.com/gajo357/dsharplang/issues/65

Plan: `docs/features/implementation/issue-65-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/65.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-65-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #65 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #66 — RUN-11: Implement per-key sequential ordering and concurrency

Issue: https://github.com/gajo357/dsharplang/issues/66

Plan: `docs/features/implementation/issue-66-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/66.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-66-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #66 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #67 — RUN-12: Implement queue capacity and cooperative cancellation

Issue: https://github.com/gajo357/dsharplang/issues/67

Plan: `docs/features/implementation/issue-67-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/67.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-67-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #67 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #68 — RUN-13: Implement atomic completion with follow-up enqueue

Issue: https://github.com/gajo357/dsharplang/issues/68

Plan: `docs/features/implementation/issue-68-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/68.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-68-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #68 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #71 — RUN-16: Implement queue inspection and operator controls

Issue: https://github.com/gajo357/dsharplang/issues/71

Plan: `docs/features/implementation/issue-71-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/71.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-71-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #71 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

## P10 — Events, schedules and application configuration

### #79 — LIB-02: Implement standard immutable time, identity and binary values

Issue: https://github.com/gajo357/dsharplang/issues/79

Plan: `docs/features/implementation/issue-79-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/79.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-79-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #79 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #69 — RUN-14: Implement recoverable event fan-out

Issue: https://github.com/gajo357/dsharplang/issues/69

Plan: `docs/features/implementation/issue-69-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/69.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-69-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #69 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #70 — RUN-15: Implement durable cron occurrence creation

Issue: https://github.com/gajo357/dsharplang/issues/70

Plan: `docs/features/implementation/issue-70-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/70.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-70-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #70 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #74 — APP-01: Implement typed configuration loading

Issue: https://github.com/gajo357/dsharplang/issues/74

Plan: `docs/features/implementation/issue-74-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/74.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-74-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #74 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #75 — APP-02: Implement asynchronous secret-provider initialization

Issue: https://github.com/gajo357/dsharplang/issues/75

Plan: `docs/features/implementation/issue-75-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/75.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-75-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #75 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #76 — APP-03: Implement lazy resource initialization and replacement

Issue: https://github.com/gajo357/dsharplang/issues/76

Plan: `docs/features/implementation/issue-76-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/76.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-76-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #76 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

## P11 — Typed HTTP and authentication

### #81 — HTTP-01: Host explicitly registered HTTP agents

Issue: https://github.com/gajo357/dsharplang/issues/81

Plan: `docs/features/implementation/issue-81-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/81.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-81-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #81 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #82 — HTTP-02: Implement typed path, query and JSON body binding

Issue: https://github.com/gajo357/dsharplang/issues/82

Plan: `docs/features/implementation/issue-82-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/82.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-82-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #82 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #83 — HTTP-03: Implement typed asynchronous transformation pipelines

Issue: https://github.com/gajo357/dsharplang/issues/83

Plan: `docs/features/implementation/issue-83-plan.md`

Implement `pipeline = ordinaryCallable`, not a list-of-steps block or a new general pipeline expression. Reuse #130/#136 Result helpers for explicit failure-aware composition; validate handler input and HTTP error mappings. Wrapping middleware remains #84.

```text
Implement https://github.com/gajo357/dsharplang/issues/83.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-83-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #83 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #84 — HTTP-04: Implement wrapping middleware and cleanup

Issue: https://github.com/gajo357/dsharplang/issues/84

Plan: `docs/features/implementation/issue-84-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/84.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-84-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #84 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #85 — HTTP-05: Implement runtime authentication and authorization checks

Issue: https://github.com/gajo357/dsharplang/issues/85

Plan: `docs/features/implementation/issue-85-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/85.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-85-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #85 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #93 — OPS-03: Implement health checks and bounded graceful draining

Issue: https://github.com/gajo357/dsharplang/issues/93

Plan: `docs/features/implementation/issue-93-plan.md`

Integrate the #132 shutdown order: drain producers, stop Logger admission and finish bounded output delivery, release outputs, then close shared RuntimeStore. Non-cooperating outputs must not cause overlapping retries or unbounded host shutdown.

```text
Implement https://github.com/gajo357/dsharplang/issues/93.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-93-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #93 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

## P12 — Observability, testing and the observer

### #91 — OPS-01: Implement correlated structured logs, metrics and tracing

Issue: https://github.com/gajo357/dsharplang/issues/91

Plan: `docs/features/implementation/issue-91-plan.md`

Reuse #131/#132 for captured LogEvents, Logger and output delivery. Own runtime instrumentation, metrics, tracing, redaction and boundary correlation; do not build a second logging subsystem or recurse through failed outputs.

```text
Implement https://github.com/gajo357/dsharplang/issues/91.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-91-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #91 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #94 — OPS-04: Provide handler and application testing harnesses

Issue: https://github.com/gajo357/dsharplang/issues/94

Plan: `docs/features/implementation/issue-94-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/94.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-94-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #94 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #92 — OPS-02: Implement protected runtime observer dashboard

Issue: https://github.com/gajo357/dsharplang/issues/92

Plan: `docs/features/implementation/issue-92-plan.md`

Provide observer core and its read authorization independently of #134; the later LOG-04 task adds stored-log search through LogReader. With no query-capable output, history is unavailable. User fields such as tenantId are filters, not access control.

```text
Implement https://github.com/gajo357/dsharplang/issues/92.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-92-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #92 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

## P13 — .NET/F# interop and external resource adapters

### #87 — INT-01: Implement explicit NuGet reference and package resolution

Issue: https://github.com/gajo357/dsharplang/issues/87

Plan: `docs/features/implementation/issue-87-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/87.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-87-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #87 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #88 — INT-02: Implement the safe C# value and method adapter subset

Issue: https://github.com/gajo357/dsharplang/issues/88

Plan: `docs/features/implementation/issue-88-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/88.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-88-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #88 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #89 — INT-03: Implement CLR async, error and delegate adapters

Issue: https://github.com/gajo357/dsharplang/issues/89

Plan: `docs/features/implementation/issue-89-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/89.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-89-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #89 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #90 — INT-04: Implement the initial F# NuGet adapter subset

Issue: https://github.com/gajo357/dsharplang/issues/90

Plan: `docs/features/implementation/issue-90-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/90.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-90-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #90 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #77 — APP-04: Implement scoped database and filesystem capabilities

Issue: https://github.com/gajo357/dsharplang/issues/77

Plan: `docs/features/implementation/issue-77-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/77.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-77-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #77 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #80 — LIB-03: Design and implement an outbound HTTP client adapter

Issue: https://github.com/gajo357/dsharplang/issues/80

Plan: `docs/features/implementation/issue-80-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/80.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-80-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #80 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #95 — OPS-05: Design business-data transactions and outbox integration

Issue: https://github.com/gajo357/dsharplang/issues/95

Plan: `docs/features/implementation/issue-95-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/95.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-95-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #95 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

## P14 — Additional language features, tooling and providers

### #48 — LANG-31: Implement integer widths and extended integer literals

Issue: https://github.com/gajo357/dsharplang/issues/48

Plan: `docs/features/implementation/issue-48-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/48.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-48-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #48 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #49 — LANG-32: Implement float32, float and decimal

Issue: https://github.com/gajo357/dsharplang/issues/49

Plan: `docs/features/implementation/issue-49-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/49.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-49-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #49 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #50 — LANG-33: Implement string interpolation

Issue: https://github.com/gajo357/dsharplang/issues/50

Plan: `docs/features/implementation/issue-50-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/50.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-50-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #50 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #47 — LANG-30: Implement statically resolved extensions

Issue: https://github.com/gajo357/dsharplang/issues/47

Plan: `docs/features/implementation/issue-47-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/47.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-47-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #47 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #97 — TOOL-02: Implement language-server document diagnostics

Issue: https://github.com/gajo357/dsharplang/issues/97

Plan: `docs/features/implementation/issue-97-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/97.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-97-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #97 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #98 — TOOL-03: Implement semantic completion, hover and signature help

Issue: https://github.com/gajo357/dsharplang/issues/98

Plan: `docs/features/implementation/issue-98-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/98.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-98-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #98 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #99 — TOOL-04: Implement required-members code actions

Issue: https://github.com/gajo357/dsharplang/issues/99

Plan: `docs/features/implementation/issue-99-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/99.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-99-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #99 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #86 — HTTP-06: Implement typed WebSocket connection handling

Issue: https://github.com/gajo357/dsharplang/issues/86

Plan: `docs/features/implementation/issue-86-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/86.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-86-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #86 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #72 — RUN-17: Implement PostgreSQL RuntimeStore provider

Issue: https://github.com/gajo357/dsharplang/issues/72

Plan: `docs/features/implementation/issue-72-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/72.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-72-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #72 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```

### #73 — RUN-18: Implement MySQL RuntimeStore provider

Issue: https://github.com/gajo357/dsharplang/issues/73

Plan: `docs/features/implementation/issue-73-plan.md`

```text
Implement https://github.com/gajo357/dsharplang/issues/73.

Follow the implementation sequence and applicable AGENTS.md instructions.
Inspect current main, existing issue/PR state and merged prerequisites.
If this is a parent issue, implement only its approved planning/contract slice;
do not combine all children into one implementation.

Use one dedicated branch and one PR for this issue.
Before implementation, write:
docs/features/implementation/issue-73-plan.md

Include scope, implementation steps, affected files/interfaces, dependencies,
AC-to-test mapping, exact verification commands and unresolved decisions.

Proceed from the plan through implementation and tests without routine pauses.
If a required dependency or public contract is unresolved, report the blocker.
Do not silently invent language syntax, runtime semantics or new dependencies.

Implement only this issue. Preserve existing tests and safety invariants.
Run the required checks, fix in-scope failures, update the plan and status,
and open one draft PR with the plan, code, tests and acceptance evidence.
Use Closes #73 only if that PR completes the implementation issue.
If publishing the PR is unavailable, say so explicitly.

Do not merge, deploy or start another issue.
```


## Pipes, partial application and logging additions

These target tasks follow the [new specification](../DSharp_Pipes_and_Logging.md) and current [implementation sequence](DSharp_Implementation_Sequence.md). Parent numbers are tracking boundaries, not permission to combine all children into one implementation. Check live issue bodies before dispatch; none of these entries claims completed behavior.

### #129 — LANG-41: Automatic partial application across supported callables

Issue: https://github.com/gajo357/dsharplang/issues/129

Plan: `docs/features/implementation/issue-129-plan.md`

Dependencies: Core: #30, #33, #34, #35 and child #135. Later integrations wait for #41, #43, #44, #47, #40 and #45; foreign behavior remains governed by #16/#88/#89.

Parent tracking task. Dispatch #135 for ordinary functions first. Keep this parent open until the async, method/default/extension and lifetime matrix is complete. Preserve declared multi-argument types, evaluation order and no implicit pipe awaiting/unwrapping.

```text
Plan the remaining bounded implementation slices for https://github.com/gajo357/dsharplang/issues/129.

Follow AGENTS.md, the implementation sequence and DSharp_Pipes_and_Logging.md.
Inspect current main, existing issue/PR state and merged prerequisites.
Write docs/features/implementation/issue-129-plan.md before implementation.
Use one dedicated branch and one draft PR for the selected bounded slice.

Respect the scope and dependency notes above. If a runtime/library setup or
public contract remains unresolved, report the gate and continue only independent
scoped work. Preserve unrelated tests, the pinned SDK/dependencies and trusted
verification. Run the required bootstrap/compiler/full-verifier commands and
applicable approved runtime/provider tests; map acceptance criteria to evidence.
Update implementation status only for tested behavior.

Do not close this parent for planning or a partial implementation.
Do not merge, deploy or start another issue.
```

### #130 — LIB-04: Result composition helpers and async overloads

Issue: https://github.com/gajo357/dsharplang/issues/130

Plan: `docs/features/implementation/issue-130-plan.md`

Dependencies: #31 and #135; synchronous child #136 first. The async matrix waits for #41/#42 and approved library setup.

Parent tracking task. Provide callback-first/data-last andThen, map, mapError, tap and tapError as ordinary symbols. Track all Result/Task combinations without retry, flattening by map, implicit Option/null unwrapping or error erasure. Worker/queue adapters remain #56/#61.

```text
Plan the remaining bounded implementation slices for https://github.com/gajo357/dsharplang/issues/130.

Follow AGENTS.md, the implementation sequence and DSharp_Pipes_and_Logging.md.
Inspect current main, existing issue/PR state and merged prerequisites.
Write docs/features/implementation/issue-130-plan.md before implementation.
Use one dedicated branch and one draft PR for the selected bounded slice.

Respect the scope and dependency notes above. If a runtime/library setup or
public contract remains unresolved, report the gate and continue only independent
scoped work. Preserve unrelated tests, the pinned SDK/dependencies and trusted
verification. Run the required bootstrap/compiler/full-verifier commands and
applicable approved runtime/provider tests; map acceptance criteria to evidence.
Update implementation status only for tested behavior.

Do not close this parent for planning or a partial implementation.
Do not merge, deploy or start another issue.
```

### #131 — LOG-01: Typed LogEvent, Logger templates and scope capture

Issue: https://github.com/gajo357/dsharplang/issues/131

Plan: `docs/features/implementation/issue-131-plan.md`

Dependencies: Approved #9/#10 setup as applicable, #37/#38, #41/#45, #79 and #78. Boundary-specific context integration remains #91.

Freeze the minimal immutable event/error schema and named-field lowering before dependent delivery work. Implement eager once-only field capture, checked templates, numeric/null precision and host-isolated scope snapshots with cleanup. Do not silently define a general variadic-call feature or Option wire encoding.

```text
Implement the bounded slice in https://github.com/gajo357/dsharplang/issues/131.

Follow AGENTS.md, the implementation sequence and DSharp_Pipes_and_Logging.md.
Inspect current main, existing issue/PR state and merged prerequisites.
Write docs/features/implementation/issue-131-plan.md before implementation.
Use one dedicated branch and one draft PR for the selected bounded slice.

Respect the scope and dependency notes above. If a runtime/library setup or
public contract remains unresolved, report the gate and continue only independent
scoped work. Preserve unrelated tests, the pinned SDK/dependencies and trusted
verification. Run the required bootstrap/compiler/full-verifier commands and
applicable approved runtime/provider tests; map acceptance criteria to evidence.
Update implementation status only for tested behavior.

Use Closes #131 only if this PR completes the bounded issue.
Do not merge, deploy or start another issue.
```

### #132 — LOG-02: Host-owned Logger actor, console and output lifecycle

Issue: https://github.com/gajo357/dsharplang/issues/132

Plan: `docs/features/implementation/issue-132-plan.md`

Dependencies: #131, #56/#58, #17 and approved runtime setup. Do not depend on #91, which consumes Logger.

Use the actor runtime for one bounded transient Logger per host. Default to Info and console only; validate explicit startup outputs. Prove nonwaiting producer admission, independent failed-output retries, quarantine of timed-out writes, bounded backlogs and ordered shutdown. No durable-audit or exactly-once guarantee.

```text
Implement the bounded slice in https://github.com/gajo357/dsharplang/issues/132.

Follow AGENTS.md, the implementation sequence and DSharp_Pipes_and_Logging.md.
Inspect current main, existing issue/PR state and merged prerequisites.
Write docs/features/implementation/issue-132-plan.md before implementation.
Use one dedicated branch and one draft PR for the selected bounded slice.

Respect the scope and dependency notes above. If a runtime/library setup or
public contract remains unresolved, report the gate and continue only independent
scoped work. Preserve unrelated tests, the pinned SDK/dependencies and trusted
verification. Run the required bootstrap/compiler/full-verifier commands and
applicable approved runtime/provider tests; map acceptance criteria to evidence.
Update implementation status only for tested behavior.

Use Closes #132 only if this PR completes the bounded issue.
Do not merge, deploy or start another issue.
```

### #133 — LOG-03: DatabaseLogOutput and bounded retention

Issue: https://github.com/gajo357/dsharplang/issues/133

Plan: `docs/features/implementation/issue-133-plan.md`

Dependencies: #132, #59/#60, #64, #78 and approved provider setup.

Use the existing RuntimeStore with separate logging migrations. Require atomic committed batches, event-ID deduplication, received-at retention and bounded capacity eviction. Logging remains outside business transactions. Use real-provider tests, including queue coexistence.

```text
Implement the bounded slice in https://github.com/gajo357/dsharplang/issues/133.

Follow AGENTS.md, the implementation sequence and DSharp_Pipes_and_Logging.md.
Inspect current main, existing issue/PR state and merged prerequisites.
Write docs/features/implementation/issue-133-plan.md before implementation.
Use one dedicated branch and one draft PR for the selected bounded slice.

Respect the scope and dependency notes above. If a runtime/library setup or
public contract remains unresolved, report the gate and continue only independent
scoped work. Preserve unrelated tests, the pinned SDK/dependencies and trusted
verification. Run the required bootstrap/compiler/full-verifier commands and
applicable approved runtime/provider tests; map acceptance criteria to evidence.
Update implementation status only for tested behavior.

Use Closes #133 only if this PR completes the bounded issue.
Do not merge, deploy or start another issue.
```

### #134 — LOG-04: Typed LogReader and protected observer search

Issue: https://github.com/gajo357/dsharplang/issues/134

Plan: `docs/features/implementation/issue-134-plan.md`

Dependencies: #133, #92 observer core and #85 read authorization. #92 must not depend on this extension.

Implement a separate read-only LogReader and observer search with typed parameterized filters, precise null/missing/numeric semantics, bounded queries and high-water cursors. Preserve unavailable-history states and existing read authorization; user filters are not access control. Use real-provider tests.

```text
Implement the bounded slice in https://github.com/gajo357/dsharplang/issues/134.

Follow AGENTS.md, the implementation sequence and DSharp_Pipes_and_Logging.md.
Inspect current main, existing issue/PR state and merged prerequisites.
Write docs/features/implementation/issue-134-plan.md before implementation.
Use one dedicated branch and one draft PR for the selected bounded slice.

Respect the scope and dependency notes above. If a runtime/library setup or
public contract remains unresolved, report the gate and continue only independent
scoped work. Preserve unrelated tests, the pinned SDK/dependencies and trusted
verification. Run the required bootstrap/compiler/full-verifier commands and
applicable approved runtime/provider tests; map acceptance criteria to evidence.
Update implementation status only for tested behavior.

Use Closes #134 only if this PR completes the bounded issue.
Do not merge, deploy or start another issue.
```

### #135 — LANG-41a: Partial application for ordinary functions and value pipes

Issue: https://github.com/gajo357/dsharplang/issues/135

Plan: `docs/features/implementation/issue-135-plan.md`

Dependencies: #30, #33, #34, #35 and #36. Parent #129 tracks the wider matrix.

Before implementation dispatch, inventory the exact old under-application rejection assertions, including 1 |> add(2), and prepare a narrowly reviewed baseline migration. Preserve regression intent and trusted verification; do not silently bypass old tests. Scope any required trusted-harness migration separately. Implement only ordinary positional-prefix calls, grouped invocation, empty-prefix behavior, generic context and ordinary pipes; methods/async/resource integrations remain with #129.

```text
Implement the bounded slice in https://github.com/gajo357/dsharplang/issues/135.

Follow AGENTS.md, the implementation sequence and DSharp_Pipes_and_Logging.md.
Inspect current main, existing issue/PR state and merged prerequisites.
Write docs/features/implementation/issue-135-plan.md before implementation.
Use one dedicated branch and one draft PR for the selected bounded slice.

Respect the scope and dependency notes above. If a runtime/library setup or
public contract remains unresolved, report the gate and continue only independent
scoped work. Preserve unrelated tests, the pinned SDK/dependencies and trusted
verification. Run the required bootstrap/compiler/full-verifier commands and
applicable approved runtime/provider tests; map acceptance criteria to evidence.
Update implementation status only for tested behavior.

Use Closes #135 only if this PR completes the bounded issue.
Do not merge, deploy or start another issue.
```

### #136 — LIB-04a: Synchronous Result composition helpers

Issue: https://github.com/gajo357/dsharplang/issues/136

Plan: `docs/features/implementation/issue-136-plan.md`

Dependencies: #31 and merged #135, plus the approved library setup boundary. Parent #130 retains async #41/#42 work.

Implement only the synchronous helper matrix with precise generic signatures, once-only applicable callback invocation, eager callback-expression creation and explicit typed errors. Preserve Success(null), Success(None), Some(null), and nested Result/Option distinctions; do not add Task behavior or close #130.

```text
Implement the bounded slice in https://github.com/gajo357/dsharplang/issues/136.

Follow AGENTS.md, the implementation sequence and DSharp_Pipes_and_Logging.md.
Inspect current main, existing issue/PR state and merged prerequisites.
Write docs/features/implementation/issue-136-plan.md before implementation.
Use one dedicated branch and one draft PR for the selected bounded slice.

Respect the scope and dependency notes above. If a runtime/library setup or
public contract remains unresolved, report the gate and continue only independent
scoped work. Preserve unrelated tests, the pinned SDK/dependencies and trusted
verification. Run the required bootstrap/compiler/full-verifier commands and
applicable approved runtime/provider tests; map acceptance criteria to evidence.
Update implementation status only for tested behavior.

Use Closes #136 only if this PR completes the bounded issue.
Do not merge, deploy or start another issue.
```
