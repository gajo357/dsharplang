# Compiler implementation status

The approved executable boundary is recorded in [bootstrap scope](../COMPILER_BOOTSTRAP.md).
This status describes tested implementation, not the full syntax draft.

| Feature | Parse/type-check | Generated execution |
| --- | --- | --- |
| One module, parameterless int literal functions | Implemented and tested | Demo.answer returns 42 |
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
tracks immutable locals and remains unimplemented on this documented baseline.
Instruction reconciliation in #8 does not add language behavior. Issue creation,
approval labels and planning PRs are not implementation evidence; update the matrix
only when the behavior and relevant generated execution/rejection tests pass.
