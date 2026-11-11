# D# Codex web task queue

Source: [Implementation sequence](https://github.com/gajo357/dsharplang/blob/main/docs/features/implementation/DSharp_Implementation_Sequence.md).

These 94 task briefs reuse existing GitHub issues. They are prepared prompts, not launched Codex tasks. Launch one foundational implementation task at a time. Check current main, existing tasks/PRs and merged prerequisites before each launch. Follow the source sequence for dependency gates and parent issue splits. The default next candidate is #8; no open PRs were found when preparing this queue. A task or unmerged implementation may still exist outside GitHub.

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

## P00 — [#8](https://github.com/gajo357/dsharplang/issues/8) · SET-01

## P01 — [#7](https://github.com/gajo357/dsharplang/issues/7) · LANG-01

## P01 — [#19](https://github.com/gajo357/dsharplang/issues/19) · LANG-02

## P01 — [#20](https://github.com/gajo357/dsharplang/issues/20) · LANG-03

## P01 — [#21](https://github.com/gajo357/dsharplang/issues/21) · LANG-04

## P01 — [#22](https://github.com/gajo357/dsharplang/issues/22) · LANG-05

## P01 — [#23](https://github.com/gajo357/dsharplang/issues/23) · LANG-06

## P01 — [#24](https://github.com/gajo357/dsharplang/issues/24) · LANG-07

## P02 — [#12](https://github.com/gajo357/dsharplang/issues/12) · DEC-02

## P02 — [#55](https://github.com/gajo357/dsharplang/issues/55) · LANG-38

## P02 — [#25](https://github.com/gajo357/dsharplang/issues/25) · LANG-08

## P02 — [#26](https://github.com/gajo357/dsharplang/issues/26) · LANG-09

## P02 — [#27](https://github.com/gajo357/dsharplang/issues/27) · LANG-10

## P02 — [#28](https://github.com/gajo357/dsharplang/issues/28) · LANG-11

## P02 — [#29](https://github.com/gajo357/dsharplang/issues/29) · LANG-12

## P02 — [#30](https://github.com/gajo357/dsharplang/issues/30) · LANG-13

## P02 — [#31](https://github.com/gajo357/dsharplang/issues/31) · LANG-14

## P02 — [#32](https://github.com/gajo357/dsharplang/issues/32) · LANG-15

## P03 — [#11](https://github.com/gajo357/dsharplang/issues/11) · DEC-01

## P03 — [#33](https://github.com/gajo357/dsharplang/issues/33) · LANG-16

## P03 — [#34](https://github.com/gajo357/dsharplang/issues/34) · LANG-17

## P03 — [#35](https://github.com/gajo357/dsharplang/issues/35) · LANG-18

## P03 — [#36](https://github.com/gajo357/dsharplang/issues/36) · LANG-19

## P03 — [#37](https://github.com/gajo357/dsharplang/issues/37) · LANG-20

## P03 — [#38](https://github.com/gajo357/dsharplang/issues/38) · LANG-21

## P03 — [#39](https://github.com/gajo357/dsharplang/issues/39) · LANG-22

## P03 — [#40](https://github.com/gajo357/dsharplang/issues/40) · LANG-23

## P04 — [#17](https://github.com/gajo357/dsharplang/issues/17) · DEC-07

## P04 — [#41](https://github.com/gajo357/dsharplang/issues/41) · LANG-24

## P04 — [#42](https://github.com/gajo357/dsharplang/issues/42) · LANG-25

## P04 — [#43](https://github.com/gajo357/dsharplang/issues/43) · LANG-26

## P04 — [#44](https://github.com/gajo357/dsharplang/issues/44) · LANG-27

## P04 — [#45](https://github.com/gajo357/dsharplang/issues/45) · LANG-28

## P04 — [#46](https://github.com/gajo357/dsharplang/issues/46) · LANG-29

## P05 — [#13](https://github.com/gajo357/dsharplang/issues/13) · DEC-03

## P05 — [#14](https://github.com/gajo357/dsharplang/issues/14) · DEC-04

## P05 — [#15](https://github.com/gajo357/dsharplang/issues/15) · DEC-05

## P05 — [#16](https://github.com/gajo357/dsharplang/issues/16) · DEC-06

## P05 — [#18](https://github.com/gajo357/dsharplang/issues/18) · DEC-08

## P05 — [#9](https://github.com/gajo357/dsharplang/issues/9) · SET-02

## P06 — [#51](https://github.com/gajo357/dsharplang/issues/51) · LANG-34

## P06 — [#56](https://github.com/gajo357/dsharplang/issues/56) · RUN-01

## P06 — [#57](https://github.com/gajo357/dsharplang/issues/57) · RUN-02

## P07 — [#52](https://github.com/gajo357/dsharplang/issues/52) · LANG-35

## P07 — [#53](https://github.com/gajo357/dsharplang/issues/53) · LANG-36

## P07 — [#54](https://github.com/gajo357/dsharplang/issues/54) · LANG-37

## P07 — [#58](https://github.com/gajo357/dsharplang/issues/58) · RUN-03

## P07 — [#96](https://github.com/gajo357/dsharplang/issues/96) · TOOL-01

## P07 — [#100](https://github.com/gajo357/dsharplang/issues/100) · TOOL-05

## P07 — [#10](https://github.com/gajo357/dsharplang/issues/10) · SET-03

## P08 — [#59](https://github.com/gajo357/dsharplang/issues/59) · RUN-04

## P08 — [#60](https://github.com/gajo357/dsharplang/issues/60) · RUN-05

## P08 — [#78](https://github.com/gajo357/dsharplang/issues/78) · LIB-01

## P08 — [#61](https://github.com/gajo357/dsharplang/issues/61) · RUN-06

## P08 — [#62](https://github.com/gajo357/dsharplang/issues/62) · RUN-07

## P08 — [#63](https://github.com/gajo357/dsharplang/issues/63) · RUN-08

## P08 — [#64](https://github.com/gajo357/dsharplang/issues/64) · RUN-09

## P09 — [#65](https://github.com/gajo357/dsharplang/issues/65) · RUN-10

## P09 — [#66](https://github.com/gajo357/dsharplang/issues/66) · RUN-11

## P09 — [#67](https://github.com/gajo357/dsharplang/issues/67) · RUN-12

## P09 — [#68](https://github.com/gajo357/dsharplang/issues/68) · RUN-13

## P09 — [#71](https://github.com/gajo357/dsharplang/issues/71) · RUN-16

## P10 — [#79](https://github.com/gajo357/dsharplang/issues/79) · LIB-02

## P10 — [#69](https://github.com/gajo357/dsharplang/issues/69) · RUN-14

## P10 — [#70](https://github.com/gajo357/dsharplang/issues/70) · RUN-15

## P10 — [#74](https://github.com/gajo357/dsharplang/issues/74) · APP-01

## P10 — [#75](https://github.com/gajo357/dsharplang/issues/75) · APP-02

## P10 — [#76](https://github.com/gajo357/dsharplang/issues/76) · APP-03

## P11 — [#81](https://github.com/gajo357/dsharplang/issues/81) · HTTP-01

## P11 — [#82](https://github.com/gajo357/dsharplang/issues/82) · HTTP-02

## P11 — [#83](https://github.com/gajo357/dsharplang/issues/83) · HTTP-03

## P11 — [#84](https://github.com/gajo357/dsharplang/issues/84) · HTTP-04

## P11 — [#85](https://github.com/gajo357/dsharplang/issues/85) · HTTP-05

## P11 — [#93](https://github.com/gajo357/dsharplang/issues/93) · OPS-03

## P12 — [#91](https://github.com/gajo357/dsharplang/issues/91) · OPS-01

## P12 — [#94](https://github.com/gajo357/dsharplang/issues/94) · OPS-04

## P12 — [#92](https://github.com/gajo357/dsharplang/issues/92) · OPS-02

## P13 — [#87](https://github.com/gajo357/dsharplang/issues/87) · INT-01

## P13 — [#88](https://github.com/gajo357/dsharplang/issues/88) · INT-02

## P13 — [#89](https://github.com/gajo357/dsharplang/issues/89) · INT-03

## P13 — [#90](https://github.com/gajo357/dsharplang/issues/90) · INT-04

## P13 — [#77](https://github.com/gajo357/dsharplang/issues/77) · APP-04

## P13 — [#80](https://github.com/gajo357/dsharplang/issues/80) · LIB-03

## P13 — [#95](https://github.com/gajo357/dsharplang/issues/95) · OPS-05

## P14 — [#48](https://github.com/gajo357/dsharplang/issues/48) · LANG-31

## P14 — [#49](https://github.com/gajo357/dsharplang/issues/49) · LANG-32

## P14 — [#50](https://github.com/gajo357/dsharplang/issues/50) · LANG-33

## P14 — [#47](https://github.com/gajo357/dsharplang/issues/47) · LANG-30

## P14 — [#97](https://github.com/gajo357/dsharplang/issues/97) · TOOL-02

## P14 — [#98](https://github.com/gajo357/dsharplang/issues/98) · TOOL-03

## P14 — [#99](https://github.com/gajo357/dsharplang/issues/99) · TOOL-04

## P14 — [#86](https://github.com/gajo357/dsharplang/issues/86) · HTTP-06

## P14 — [#72](https://github.com/gajo357/dsharplang/issues/72) · RUN-17

## P14 — [#73](https://github.com/gajo357/dsharplang/issues/73) · RUN-18

