# Issue 45: Scoped resource ownership

**Status:** The maintainer directed D# to follow C# `using` cleanup behavior.
Direct construction and immediate function-factory transfer are implemented;
service/interface method factories are implemented for review. #45 remains open
for Result transfer.

[DEC-07](DEC-07.md) adopts bounded ownership and exactly-once cleanup. Its prior
rule for separately reporting a cleanup failure when another failure is already
in flight is superseded for this feature by the maintainer's explicit instruction
to follow C# behavior. A cleanup exception replaces the in-flight outcome. The
slice below is not an application host, resource provider, transaction or CLR
interop.

## D# scopes

A resource can be scoped to the rest of the enclosing block:

```text
resource Cursor(value: int) {
    read(): int { value }
    release(): Unit { () }
}

fn answer(): int {
    using cursor = Cursor(40)
    cursor.read() + 2
}
```

An explicit block provides a shorter scope:

```text
fn answer(): int {
    using cursor = Cursor(40) {
        cursor.read() + 2
    }
}
```

A `resource` declares operations and exactly one compiler-invoked `release`.
An initializer directly constructs a fresh resource or consumes a fully
applied checked owned-resource function or service/interface method factory; an async factory must be
immediately awaited. Constructor and factory arguments are evaluated once before
the handle is in scope. An `async release(): Unit`
is awaited and requires an async enclosing function or lambda. Async resource
operations must be immediately awaited in their scope. Resource dependencies
follow existing service rules: immutable data or explicit service/interface
capabilities. Operations return supported immutable data.

A handle may only be the direct receiver of a declared operation, and `release`
is reserved for compiler cleanup. It cannot be copied, passed, returned, stored,
placed in a data value, captured by a closure, used in a partial application or
left in an unawaited task. These checks apply through supported generic calls
and imports. Return inference follows the existing conservative function rules;
indirect method calls may require an explicit annotation. Async work must have
stopped using the handle when its operation completes; unchecked foreign
providers are not included.

## Cleanup behavior

Cleanup runs exactly once on every successfully acquired resource when its
scope ends, including normal completion, early return, propagated `Result.Error`,
fault and cooperative cancellation. It does not run for an acquisition that
fails. Nested or sequential resources release in reverse acquisition order,
including when an inner acquisition fails. Async cleanup is awaited.

When cleanup succeeds, the body's outcome remains unchanged. When cleanup
throws, its exception is surfaced even if the body was already returning a
value or typed error, throwing or cancelling. If multiple nested cleanups throw,
the last exception thrown during unwinding is surfaced. There is no special
cleanup reporter or implicit transaction rollback.

## Remaining #45 work

Explicit nongeneric top-level function factories can return fresh resources for
immediate consumption by `using`, including direct async calls under `await`.
They cannot be used as values or partially applied, and a resource factory may
not contain `using`: cleanup failure could otherwise lose a produced resource
before ownership transfers. See the [factory plan](issue-170-plan.md).

Checked service/interface methods and supported defaults may also return a fresh
resource for immediate use, with async methods directly awaited. Their bodies
follow the same non-escape and no-`using` rules as owned-returning functions.
See the [service factory plan](issue-172-plan.md).

Linear transfers through `Result` or stored `Task` values are not implemented. In particular,
`using connection = (await db.open())?` is still a target rather than supported
behavior. General borrowing, foreign
`IDisposable`/`IAsyncDisposable` mapping, database transactions, and implicit
application registration remain separate work. Closing #45 requires the
remaining ownership behavior and its acceptance tests; the
[plan](issue-45-plan.md) records test and verification evidence for this slice.
