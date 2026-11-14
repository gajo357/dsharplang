# Issue 45: Scoped resource ownership

**Status:** The maintainer approved owned resources and explicit cleanup
reporting. The direct-construction slice is implemented for review; #45 remains
open for provider-returned ownership and its integration tests.

[DEC-07](DEC-07.md) adopts bounded ownership and exactly-once cleanup, without
masking an earlier primary failure. The implementation below is a bounded
compiler slice of [#45](https://github.com/gajo357/dsharplang/issues/45), not
an application host, a resource provider, a transaction or CLR interop.

## D# scope

```text
resource Cursor(value: int) {
    read(): int { value }
    release(): Unit { () }
}

fn answer(onCleanup: (string) -> Unit): int {
    using cursor = Cursor(40) report onCleanup {
        cursor.read() + 2
    }
}
```

A `resource` declares operations and exactly one compiler-invoked `release`.
The initializer directly constructs a fresh resource. The reporter is an
existing non-null `(string) -> Unit` parameter or local, named by identifier;
it is not an arbitrary expression. Dependencies follow the existing service
rules: immutable data or explicit service/interface capabilities. An
`async release(): Unit` is awaited and requires an async enclosing function or
lambda. Async resource operations must be immediately awaited in their scope.
Resource operations return supported immutable data. The scope evaluates
constructor arguments once and releases successful acquisitions exactly once;
a failed acquisition is not released.

A handle may only be the direct receiver of a declared operation, and `release`
is reserved for compiler cleanup. It cannot be copied, passed, returned, stored,
placed in a data value, captured by a closure, used in a partial application or
left in an unawaited task. These checks apply through supported generic calls
and imports. The scope and its enclosing function or lambda require a concrete
result type: unconstrained generic top-level results and omitted enclosing return
annotations are not supported in this slice. Async work must have stopped using
the handle when its operation completes; unchecked foreign providers are not
included.

## Cleanup and failure reporting

Cleanup runs on normal completion, early return, propagated `Result.Error`, fault
and cooperative cancellation. Nested scopes release inner first. Successful
cleanup preserves the body's outcome. If there was no prior failure, a cleanup
fault is surfaced. If a top-level `Result.Error` leaves the scope, or an exception
or cancellation is already in flight, a cleanup fault is sent once to the
explicit reporter, and the original outcome continues if reporting succeeds.
An error already handled inside the scope, or an error nested inside another
value, is not treated as the scope's primary failure.

The reporter receives the source resource name as a redacted diagnostic string,
not the original exception details or secret-bearing messages. A successful
callback is not a durability guarantee. If the callback faults, generated C#
surfaces a combined fault with the original typed result or exception, cleanup
exception and reporter exception available for inspection. Nested combined
faults retain their constituent failures recursively. This fallback changes the
outer outcome to a fault; it does not silently discard the original failure.
Raw exception inspection and richer diagnostics are separate interop work.

## Remaining #45 work

Provider-returned owned tokens and linear transfers through `Task` or `Result`
are not implemented. In particular, `using connection = (await db.open())?`
is still a target rather than supported behavior. General borrowing, async or
durable reporters, foreign `IDisposable`/`IAsyncDisposable` mapping, database
transactions, and implicit application registration remain separate work.
Closing #45 requires the remaining ownership behavior and its acceptance tests;
the [plan](issue-45-plan.md) records test and verification evidence for this
slice.
