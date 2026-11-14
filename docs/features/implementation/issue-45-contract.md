# Issue 45: Proposed scoped resource contract

**Status: proposal for review; not adopted or implemented.**
**Issue:** [#45](https://github.com/gajo357/dsharplang/issues/45) remains open.
**Prerequisites:** [#41](https://github.com/gajo357/dsharplang/issues/41),
[#43](https://github.com/gajo357/dsharplang/issues/43), and the adopted
[DEC-07](DEC-07.md).

DEC-07 adopts bounded ownership and exactly-once cleanup without masking an
earlier primary failure. Its names, signatures and syntax are illustrative;
it expressly leaves public APIs and lifetime enforcement to separately scoped
work. The [syntax reference §11.2](../DSharp_Syntax_Reference.md#112-scoped-resources)
labels `using` syntax proposed, and DEC-02 reserves the word without settling
resource types. In particular neither D# resource eligibility nor the
representation of simultaneous primary and cleanup failures has been adopted.
This document makes those choices reviewable; it does not claim behavior exists.

## Proposed surface and ownership model

The following D# illustrates the intended calling shape. `open` and the resource
and failure types here are placeholders until the ownership protocol below is
approved; this example is not a working API:

```text
async fn lookup(db: Database, id: int): Result<int, LookupError> {
    using cursor = (await db.open(id))? {
        let value = await cursor.read()
        Success(value)
    }
}
```

The recommended grammar is `using name = acquisition { body }`. Acquisition
is evaluated exactly once before the binding exists. The block yields its body
value; normal scope, no-shadowing, `return`, `?` and async rules apply. An
asynchronous acquisition requires `await` in an async context; asynchronous
cleanup also requires an async context and is awaited. A non-resource, nullable
resource, or unsupported acquisition is a compile-time error.

A resource needs an **owned token**, distinct from an ordinary service reference.
Merely adding a `dispose`-shaped method to an existing service is insufficient:
there could already be aliases to that service outside the scope. The proposed
model has an explicitly marked resource provider produce a compiler-recognized
single-use owned token. Its payload exposes only its declared operations inside
the `using` body; no normal service conversion exposes or duplicates the token.
Resource providers use an explicit D# sync or async release operation rather than
implicit discovery or a global host. The exact declaration spelling and token
signature are **not yet selected**; the following alternatives need a decision:

1. **Recommended direction:** a dedicated nominal resource declaration identifies
   operations and sync or async cleanup. The compiler generates the owned-token
   boundary. This makes ownership explicit but introduces a new declaration form.
2. A service explicitly implements a compiler-recognized sync or async resource
   contract, and an explicit ownership-producing operation creates the token.
   Ordinary service values must never be converted to tokens: doing so would leave
   existing aliases live. This reuses service methods but requires a separately
   specified, non-forgeable ownership-producing operation.

A possible first slice of option 1 creates only a fresh resource directly in
the `using` initializer:

```text
interface Tracker { closed(): Unit }

resource Cursor(tracker: Tracker) {
    read(): int { 42 }
    release(): Unit { tracker.closed() }
}

fn answer(tracker: Tracker): int {
    using cursor = Cursor(tracker) { cursor.read() }
}
```

Here `resource` is a contextual declaration marker and `release` is reserved
for compiler-invoked cleanup; source code cannot invoke it. An async release
would be written `async release(): Unit { ... }` and requires an async caller.
This is a bounded design sketch, not adopted grammar. Requiring a fresh direct
constructor avoids aliases, but does not yet support provider-returned resources
such as `(await db.open())?`. The complete feature still needs a specified
linear factory/transfer contract for those forms, and #45 must remain open
until its accepted scope and tests are complete.

Whichever alternative is adopted, these are requirements, not optional checks:

- Acquisition can cross an async or typed-failure boundary only as a linear
  ownership transfer to the immediately enclosing `using`; it cannot be copied,
  put in a local, stored in data, passed to an arbitrary function or used twice.
  A `Task` or `Result` carrying the token is likewise transient and cannot be
  stored, duplicated, or returned as ordinary data. Factory returns transfer an
  *unconsumed token*, not a scoped handle; returning a handle bound by `using`
  remains an error. If this cannot be proved, reject the program.
- A successful acquisition establishes exactly one owner. A failing, cancelled,
  or uncompleted acquisition establishes none, so `using` performs no release for
  it; a provider must clean any partial acquisition it owns. Cleanup begins only
  after acquisition completes successfully.
- The handle and all aliases are valid only inside that scope. Reject return,
  storage in fields or immutable containers, service dependencies, lambda or
  callback capture, partial application, task escape, and agent messages. There
  is no borrowed helper parameter in this issue. A resource cannot be explicitly
  released or entered into another `using` by user code; cleanup is owned by the
  scope. Calls on a handle are allowed only under rules that prevent it escaping.
- An async resource operation must be immediately awaited inside its scope; its
  task cannot be returned or retained. Its result must be immutable data, not a
  capability or function. Completion, fault or cancellation must mean the
  operation has stopped using the handle. The compiler must reject uses it cannot
  prove safe, and unchecked foreign providers await a separate interop contract.
- Release is exactly once per successfully acquired token, including normal exit,
  early return, Result propagation, fault and cooperative cancellation. Nested
  scopes release in reverse acquisition order. Async release is awaited and is
  not skipped merely because the body's cancellation is already signalled.

These rules do not define CLR `IDisposable`/`IAsyncDisposable` interoperability,
database transactions, a provider, a runtime host or automatic registration.
Those remain separate work; no CLR adapter can be inferred from this proposal.

## Failure reporting: decision required

A successful cleanup preserves the body's outcome, including an ordinary
`Result.Error`, a thrown fault or cancellation. If the body succeeds but cleanup
fails, surface the cleanup failure. A failed acquisition does not trigger scope
cleanup. Multiple nested cleanup failures must all remain available in cleanup
order. DEC-07 requires a cleanup failure to be reported without masking an earlier
primary failure, but it does not choose a reporting mechanism. An ordinary
`Result<T,E>` has nowhere to put a cleanup fault, so the following options are
mutually exclusive and require explicit review:

**A. Combined fault.** A combined
scope fault contains a tagged primary outcome and every cleanup fault in order.
For an escaping typed `Error(E)`, it retains the exact typed error payload; for
an exception or cancellation it retains the original exception/cancellation as
the primary. An `Error(E)` plus a cleanup fault now *faults* instead of returning
`Error(E)`; cancellation plus a cleanup fault also faults, while retaining the
cancellation as primary. This is observable and may conflict with an
interpretation of DEC-07 requiring the original typed/cancelled outcome itself
to be returned. Adoption must explicitly reconcile that distinction. The exact
D# inspection and foreign exception representation remain separate APIs; the
combined fault must still be surfaced with its constituent failures intact.

**B. Explicit typed scope outcome.** Require a new typed outcome containing the
primary and cleanup failures. This makes both inspectable as values, but changes
function signatures and needs a concrete treatment for exceptions and
cancellation. It is not a drop-in implementation of the existing `Result<T,E>`.

**C. Explicit scoped cleanup reporter (recommended for review).** Require an explicitly supplied reporting
capability and return the original `Error(E)` or cancellation when the reporter
successfully records the cleanup failure. This preserves those outcomes in the
successful-report case, but introduces a reporter API and its own lifetime and
reliability requirements. If reporting faults, it cannot silently discard either
the primary or cleanup failure: surface a combined failure containing all three.
There is no implicit global reporter or assumed durable delivery. This best
preserves the adopted DEC-07 primary outcome when reporting succeeds; the
reporter declaration, propagation, and combined-fault fallback still need a
concrete contract before implementation. For example, a possible explicit
spelling is:

```text
fn answer(reporter: CleanupReporter): int {
    using cursor = Cursor(40) report reporter { cursor.read() + 2 }
}
```

This is a design sketch, not a settled API: it still requires an inspectable,
redacted cleanup-failure representation and precise reporter fault behavior.

Any selected design must define what counts as the scope's primary outcome. An
`Error` that leaves the scope via `?`, `return`, or its final result expression
is different from an error merely stored, matched, recovered or nested as data;
cleanup must run in both cases, but handled data must not be misclassified as an
escaping failure. This requires a precise rule for nested scopes and their exits,
not inspection of arbitrary payload objects after the fact.

## Acceptance evidence before closing #45

Use real D# programs plus generated-code execution, with controlled release
counters, async barriers, faults and cancellation. Test sync and async release on
success, early return, propagated typed failure, exception and cancellation;
failed/partial acquisition, nested reverse-order release and repeated invocation;
and every adopted primary/cleanup and reporter-failure combination. Exercise
resource calls across suspension without disposing while work is in flight.
Negative tests must cover aliases before and after a scope, duplicate or forged
tokens, return/field/container/capture/partial/task/message escapes, and explicit
release. Run the existing compiler and repository verification. Documentation
approval or a proposal PR alone does not implement or close #45.
