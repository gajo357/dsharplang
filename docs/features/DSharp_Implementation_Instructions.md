# D# implementation instructions

**Updated:** 11 November 2026
**Repository:** https://github.com/gajo357/dsharplang

## Current source of truth

The compiler bootstrap and compiler-code automation are implemented. The old
version 0.1 handoff described the earlier documentation-only state; its bootstrap
and automation-enablement briefs are historical, not tasks to repeat. That version
remains available in Git history at commit
[567fbf1](https://github.com/gajo357/dsharplang/blob/567fbf1c88d8a329339f1e11a92e4ee71bf90e03/docs/features/DSharp_Implementation_Instructions.md).

Read [AGENTS.md](../../AGENTS.md), [bootstrap scope](../COMPILER_BOOTSTRAP.md),
[implementation status](DSharp_Implementation_Status.md) and the
[syntax reference](DSharp_Syntax_Reference.md), revision 0.3. Selected requirements
below and in syntax section 21.2 supersede conflicting older examples. Proposed
syntax and APIs outside the implemented subset still need their relevant contract
decision; this instruction update does not approve the entire draft.

The compiler is C#, with a hand-written lexer/parser, typed binding and generated
C# on .NET SDK 10.0.100. Only the literal-function bootstrap is reported implemented.
[Issue #7](https://github.com/gajo357/dsharplang/issues/7) tracks immutable local
bindings; its open issue/approval label does not establish integrated implementation.

## Codex web lifecycle

Follow the [implementation sequence](implementation/DSharp_Implementation_Sequence.md)
and [task queue](implementation/DSharp_Task_Queue.md), starting with #8 then #7.
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
