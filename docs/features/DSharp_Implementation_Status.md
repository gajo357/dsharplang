# Compiler implementation status

The original executable baseline is recorded in [bootstrap scope](../COMPILER_BOOTSTRAP.md).
The matrix below includes subsequent implemented feature slices.
This status describes tested implementation, not the full syntax draft.

| Feature | Parse/type-check | Generated execution |
| --- | --- | --- |
| One module, parameterless int literal functions | Implemented and tested | Demo.answer returns 42 |
| Immutable function-local int `let` bindings | Inferred/explicit int, earlier-local initializers and literal/local final expressions tested | Chained bindings return 42; independent functions retain distinct values |
| Whitespace and single-line comments | Implemented and tested | Included in source pipeline |
| Source diagnostics and duplicate functions | Implemented and tested | Rejected before emission |
| CLI check/compile | Implemented, smoke-tested | SDK builds generated library |
| Remaining language syntax | Unsupported, explicit diagnostics | Not implemented |
| Workers/queues/runtime/interop/LSP | Not implemented | Not claimed |

Preserve syntax reference revision 0.3 selected behaviour, while freezing each
provisional contract before implementing it. Worker-call failure representation,
queue lifecycle, durable serialization, middleware, authorization capability
validity, numeric extensions and safe .NET interop remain decisions for later
small milestones. No agent isolation or durability follows from this bootstrap.

## Planned work

Follow the [Codex web implementation sequence](implementation/DSharp_Implementation_Sequence.md)
and [task queue](implementation/DSharp_Task_Queue.md). [Issue #7](https://github.com/gajo357/dsharplang/issues/7)
is implemented in this slice: one binding per line, only nonnegative int literals
or earlier locals as initializers, optional `: int`, no reassignment or shadowing.
Bindings become visible after their initializer and never cross function boundaries.
Reassignment (DS2005), duplicate locals (DS2006), unknown/self/forward/cross-function
references (DS2007), unsupported local annotations (DS2008) and int overflow
(DS2004) are rejected with D# source spans. Malformed declarations and unsupported
expressions continue to produce source diagnostics.

See [the example](../../examples/let-bindings.ds) and
[implementation plan and evidence](implementation/issue-7-plan.md). Arithmetic,
parameters/calls, nested scopes, additional primitives and runtime features remain
unsupported.
Instruction reconciliation in #8 does not add language behavior. Issue creation,
approval labels and planning PRs are not implementation evidence; update the matrix
only when the behavior and relevant generated execution/rejection tests pass.
