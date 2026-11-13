# D# — Distributed Sharp: Syntax and Type Reference

**Version:** 0.4 — implementation handoff draft
**Date:** 13 November 2026
**Companion:** Backend Language Feature Specification v0.2  
**Status:** Language design and implementation handoff. See
[implementation status](DSharp_Implementation_Status.md) for the tested compiler
subset and [bootstrap scope](../COMPILER_BOOTSTRAP.md) for its starting point.
Target examples below do not establish implemented language/runtime support.
**Implementation brief:** [DSharp_Implementation_Instructions.md](DSharp_Implementation_Instructions.md).
**Composition and logging target:** [Pipes, partial application and logging](DSharp_Pipes_and_Logging.md).

**Decision boundary:** The user-selected behaviour listed in section 21.2 is authoritative design input. Exact syntax and library APIs marked proposed, illustrative or provisional must be frozen for the relevant milestone before implementation. A handoff document does not override repository automation permissions.

The composition and logging companion specifies the initial targets for partial
application, Result helpers, callable HTTP preprocessing and Logger. It supersedes
older `pipeline {}` composition blocks, calls requiring all arguments, `Log.write`
naming and default database logging. Incorporating these targets is not evidence
that their compiler or runtime implementation is complete.

D# combines F#-inspired inference, expressions and discriminated unions with C#-style braces, calls, comments and familiar operators. Its execution model centres on isolated agents, typed results and explicit application composition.

The established direction is: immutable values; explicit missing values; typed success/error outcomes; services with interfaces; agents whose handlers always report an outcome; and code-based registration. This document proposes concrete spellings and fills gaps needed for a readable reference. Numeric defaults, async declarations and runtime-call error types remain reviewable. This revision incorporates public-only declarations, default interface methods, method-local mutable builders, native nullable matching, and distinct callable workers versus queued agents. Where it differs from the companion feature document, these newer decisions take precedence.

“Agent” is the source-language term used here for an actor/handler definition. It does not imply an AI agent. Syntax examples demonstrate a proposed design; external library APIs shown are illustrative contracts, not claims that packages already exist.

## Contents

1. General syntax and naming
2. Primitive and standard types
3. Literals and conversions
4. Bindings and immutability
5. Nullable values: `T?`
6. Results and error propagation
7. Records, unions and generics
8. Collections and tuples
9. Functions, lambdas and extensions
10. Expressions, matching and control flow
11. Asynchronous operations and resources
12. Interfaces and default methods
13. Workers, agents and handler outcomes
14. Worker calls, queue submission and supervision
15. Triggers and execution policies
16. Typed HTTP pipelines
17. Application composition
18. Modules and public declarations
19. Keywords and operators
20. Complete worked example
21. Diagnostics and open decisions
22. Discoverable agent contracts and developer tooling

## 1. General syntax and naming

- Braces group declarations, blocks and record values. Indentation improves readability but does not define scope.
- Semicolons are not required and are omitted in this draft. Write one binding or statement per line. Expressions continue across lines inside delimiters or after an operator; a newline before `|>` also continues the expression.
- Use parentheses for calls and comma-separated argument lists.
- Use `name: Type` for type annotations. Local types are normally inferred.
- The final expression in a block is its value. `return` is available for an explicit early exit.
- A block with no final value has type `Unit`; this never implicitly becomes `Success()`.
- Names are case-sensitive. Prefer PascalCase for types, union cases, services and agents; camelCase for functions, methods, parameters and bindings.
- No class/service implementation inheritance, `new` keyword, implicit truthiness or null-forgiving operator is proposed. Interfaces may provide default method bodies.
- All declarations and members are public. No visibility modifiers are required or provided.

```text
// A single-line comment

/* A block comment.
   Nested block comments are not proposed in v0.1. */

/// Documentation for a declaration.
fn add(left: int, right: int): int {
    left + right
}

let answer = add(20, 22)
```

Documentation comments attach to the next declaration. Multiline expression parsing must use a defined newline rule; a full lexical grammar is a later compiler deliverable.

## 2. Primitive and standard types

### 2.1 Built-in scalar types

All of these are non-nullable unless suffixed with `?`.

| Type | Meaning | Example |
| --- | --- | --- |
| `bool` | Boolean value | `true` |
| `byte` | Unsigned 8-bit integer | `let flags: byte = 3` |
| `sbyte` | Signed 8-bit integer | `let offset: sbyte = -3` |
| `short` | Signed 16-bit integer | `let portOffset: short = 100` |
| `ushort` | Unsigned 16-bit integer | `let port: ushort = 443` |
| `int` | Signed 32-bit integer; default integer type | `42` |
| `uint` | Unsigned 32-bit integer | `let count: uint = 42` |
| `long` | Signed 64-bit integer | `42L` |
| `ulong` | Unsigned 64-bit integer | `let size: ulong = 42` |
| `float32` | IEEE 754 binary32 floating point | `1.5f` |
| `float` | IEEE 754 binary64 floating point | `1.5` |
| `decimal` | .NET-style decimal arithmetic; useful for monetary values | `125.50m` |
| `char` | One UTF-16 code unit | `'A'` |
| `string` | Immutable Unicode text represented as UTF-16 | `"hello"` |
| `Unit` | Exactly one value, `()`; no returned payload | `()` |

`float` deliberately means binary64 in this proposal, unlike C#'s `float`. This naming choice remains provisional. A `char` is not necessarily a complete Unicode character; Unicode scalar/grapheme operations belong in text libraries.

`decimal` still requires explicit business rules for currency, scale and rounding. It is not a currency type.

### 2.2 Standard types, not primitive keywords

| Type | Purpose |
| --- | --- |
| `uuid` | UUID/GUID value. |
| `Instant` | An unambiguous point in time. |
| `LocalDate` | A calendar date without a timezone. |
| `LocalTime` | A time of day without a date or timezone. |
| `Duration` | Elapsed time. |
| `Bytes` | Immutable binary data. |
| `List<T>` | Immutable ordered collection. |
| `Map<K, V>` | Immutable key/value collection. |
| `Set<T>` | Immutable distinct values. |
| `MutableList<T>` | Method-local mutable builder; cannot escape its creating method. |
| `MutableMap<K, V>` | Method-local mutable dictionary builder; same restriction. |
| `Result<T, E>` | Operation success or a typed error. |
| `Task<T>` | Awaitable asynchronous completion. |

Date/time library choices, exact APIs and CLR representations remain open. Avoid an ambiguous generic `DateTime` primitive. Mutable CLR arrays or collections must not be exposed as immutable D# values without an interop boundary.

## 3. Literals and conversions

```text
let enabled = true
let attempts = 5
let population = 8_000_000_000L
let mask = 0xFF
let ratio = 0.75
let amount = 125.50m
let separator = '\n'
let name = "Gavrilo"
let greeting = $"Hello, {name}"
let completed = ()
```

Proposed rules:

- Unsuffixed integer literals default to `int`; an expected integer type may select another width if the literal fits. An out-of-range literal is a compile error.
- Unsuffixed fractional literals default to `float`. Use `m` for decimal and `f` for float32. These suffixes fix the literal type.
- Numeric separators `_` are allowed between digits. Decimal, hexadecimal (`0x`) and binary (`0b`) integers are supported.
- Ordinary escapes include `\n`, `\r`, `\t`, `\\`, `\"` and `\uXXXX`. In interpolated strings, `{{` and `}}` represent literal braces.
- Mixed numeric variable types require explicit conversion; do not silently mix decimal and binary floating point.
- Integer arithmetic is checked by default. Overflow is an execution fault unless an explicit checked-operation API returns it as a typed error.
- Conversion and parsing APIs return `Result` where failure is possible. Exact API names are a library decision.
- Multiline/raw strings, byte literals and additional numeric suffixes are deferred.

## 4. Bindings and immutability

```text
let count = 3
let total: decimal = 125.50m
let title: string? = null
```

`let` creates a binding that cannot be reassigned. Records and ordinary collections are recursively immutable. Explicit mutable builders may change their contents within the creating method, while their binding remains fixed; see section 8.

```text
let invoice = Invoice {
    id = InvoiceId(uuidValue)
    total = 125.50m
    note = null
}

let revised = invoice with {
    total = 150.00m
}
```

`with` constructs a new record and preserves unspecified fields. No `var` or mutable data fields are included. Method-local `MutableList` and `MutableMap` builders are permitted under strict non-escape rules; immutable snapshots may leave the method.

Results must not be silently discarded. A function returning `Unit` may be used as a statement, but a `Result` must be matched, propagated, returned or passed to another explicitly typed consumer.

## 5. Nullable values: `T?`

**Missing data is a normal state, not automatically a failure.** Its possibility appears directly in the type and must be handled before accessing the value.

```text
let requiredName: string = "Gavrilo"
let nickname: string? = null
let age: int? = 40
```

`T?` means a value of `T` or `null`. The notation applies equally to scalars and records. It is distinct from the explicit `Option<T>` choice described below.

### 5.1 Construction

```text
let first: string? = "Gavrilo"
let second: string? = null
```

A `T` may be assigned to `T?`; the reverse requires explicit handling. `null` cannot be assigned to a non-nullable type. A bare `let value = null` is rejected when no contained type can be inferred. Literal repeated shorthand such as `string??` is excluded. Semantically, nullability is idempotent: making an already-nullable type nullable leaves it unchanged, including after generic substitution and eventual alias resolution.

For unconstrained `T`, `T?` genuinely admits null when instantiated with a value type as well as a reference type: `T = int` or `int?` yields `int?`, and `T = string` or `string?` yields `string?`. Generated C# must preserve this behavior in generic calls, returns and stored data; directly emitting unconstrained C# `T?` is not sufficient by itself. Existing closed nullable arguments such as `Box<int?>` remain valid. This behavior is tracked in [#30](https://github.com/gajo357/dsharplang/issues/30); accepting the syntax alone does not complete it. Alias declarations and collections remain future work where not implemented, and must preserve the same normalization and values when introduced.

### 5.2 Value-or-null matching

```text
fn displayName(name: string?): string {
    match name {
        null => "Anonymous"
        value => value
    }
}
```

For a nullable scrutinee, a named value-binding arm matches only a present value and binds it as non-nullable `T`. The `null` arm matches absence. Both branches are required unless an explicit wildcard covers the remaining cases. Branch order may be reversed; the value arm does not absorb null. This nullable-specific narrowing rule also applies inside a nested result pattern.

```text
match lookup {
    Success(null) => "No invoice found"
    Success(invoice) => $"Total: {invoice.total}"
    Error(error) => $"Lookup failed: {error}"
}
```

Here `lookup` has type `Result<Invoice?, StoreError>`. In the second arm, `invoice` is an `Invoice`. A named binding against a non-nullable scrutinee keeps ordinary binding semantics. `_` remains an explicit catch-all, including null where applicable. The initial design uses patterns rather than relying on flow narrowing after `name != null`.

### 5.3 Fallback and conditional access

```text
let label = nickname ?? "Anonymous"
let length: int? = nickname?.length
let visibleLength = nickname?.length ?? 0
```

- `??` explicitly supplies a fallback for absence; its right side is evaluated only when needed.
- `?.` accesses a member only when the receiver is present and otherwise preserves absence.
- Conditional access flattens an already-nullable member to one nullable layer.
- Direct access such as `nickname.length` is rejected.
- There is no postfix `!` to suppress checks.
- Returning, storing or forwarding `T?` is valid because absence remains explicit in the next contract.

### 5.4 Nullable fields and decoding

```text
type Customer {
    name: string
    nickname: string? = null
}
```

A field without a declared default must be supplied at construction, even if nullable. A default of `null` explicitly permits omission.

`Option<string?>` can express an update in which `None` leaves a name unchanged, `Some(null)` clears it and `Some("Alice")` replaces it. This is a value-level example; JSON serialization and HTTP binding rules are not decided and future integrations must preserve these distinctions explicitly.

### 5.4.1 Explicit options

`Option<T>` is a tagged union with cases `None` and `Some(T)`, tracked separately in [#121](https://github.com/gajo357/dsharplang/issues/121). `None` means no value was supplied. `Some(value)` means a value was supplied; `Some(null)` is valid only when `T` allows null. Construction, exhaustive matching and equality distinguish `None` from `Some(null)`. Access requires matching or an explicit operation; `None` must never silently become `default(T)`.

Options are not nullable types and there is no implicit conversion that loses their distinctions. Nested options do not flatten: `None`, `Some(None)` and `Some(Some(42))` are different `Option<Option<int>>` values. When collections are implemented, elements must retain all of these distinctions.

### 5.5 Question-mark forms

| Form | Meaning |
| --- | --- |
| `T?` | Nullable type. |
| `value?.member` | Conditional access, preserving absence. |
| `value ?? fallback` | Explicit fallback for absence. |
| `result?` | Unwrap success or propagate a typed error. |

Standalone postfix `?` is not an unchecked nullable unwrap and does not propagate null in this draft. Use matching, conditional access or a fallback for nullable values.

## 6. Results and error propagation

```text
type Result<T, E> {
    | Success(T)
    | Error(E)
}
```

`Result`, `Success` and `Error` are standard types/cases, not general-purpose reserved keywords. The compiler recognizes the standard result type for propagation and handler checking.

```text
fn validateAmount(amount: decimal): Result<decimal, InvoiceError> {
    if amount <= 0m {
        Error(InvalidAmount(amount))
    } else {
        Success(amount)
    }
}
```

Every branch must produce a compatible type. Ambiguous union constructors require qualification, such as `InvoiceError.NotFound(id)`.

### 6.1 Success without data

```text
fn validateName(name: string): Result<Unit, ValidationError> {
    if name == "" {
        Error(NameRequired)
    } else {
        Success()
    }
}
```

`Success()` is shorthand for `Success(())`, valid for a `Unit` success payload. It is different from `Success(null)`, which reports success with an absent optional payload.

### 6.2 Propagation with postfix `?`

```text
fn createInvoice(id: InvoiceId, amount: decimal)
    : Result<Invoice, InvoiceError> {
    let validAmount = validateAmount(amount)?

    Success(Invoice {
        id = id
        total = validAmount
        note = null
    })
}
```

`expression?` evaluates a result once. `Success(value)` yields `value`; `Error(error)` exits the nearest enclosing function or handler with `Error(error)`.

- The enclosing return type must be `Result<U, E>` with a compatible error type.
- It does not return through an enclosing caller or through a lambda boundary.
- Different error types require explicit mapping; there is no silent conversion to strings or generic exceptions.
- It does not automatically retry, log or supervise anything.
- For an awaited result, write `(await operation())?` to make grouping explicit.

### 6.3 Optional successful data

```text
Result<Invoice?, StoreError>
```

This type has three meaningful outcomes:

| Outcome | Meaning |
| --- | --- |
| `Success(invoice)` | Lookup succeeded and found an invoice. |
| `Success(null)` | Lookup succeeded but found no invoice. |
| `Error(error)` | Lookup itself failed. |

`Success(invoice)` is the normal syntax. After `result?`, the value remains `Invoice?`; absence must still be handled before accessing it.

## 7. Records, unions and generics

### 7.1 Records

```text
type Invoice {
    id: InvoiceId
    total: decimal
    note: string?
}
```

Records use named fields, structural value equality and immutable construction. Field declarations use `:`; field initializers use `=`. A field's type alone does not validate domain rules such as a positive amount; use validation functions and enforce domain rules where operations are performed. Public construction alone cannot guarantee validation.

### 7.2 Discriminated unions

```text
type InvoiceError {
    | NotFound(InvoiceId)
    | InvalidAmount(decimal)
    | StorageUnavailable
}
```

A leading `|` identifies a union case. Cases may have zero, one or several positional payloads. Use a record payload when several fields need descriptive names. There are no inherited implementations.

A single-case union provides a distinct domain type:

```text
type InvoiceId {
    | InvoiceId(uuid)
}
```

An `InvoiceId` cannot be passed where a `CustomerId` is expected merely because both wrap a UUID.

### 7.3 Generic types

```text
type Page<T> {
    items: List<T>
    nextCursor: string?
}

fn identity<T>(value: T): T {
    value
}
```

Type arguments may be inferred when unambiguous. Generic constraints, variance and type aliases are deferred; do not assume C# or F# rules for them yet.

## 8. Collections and tuples

```text
let numbers = [1, 2, 3]
let empty: List<int> = []
let extended = numbers.append(4)
let first: int? = numbers.get(0)

let pair = ("invoice", 42)
let (kind, count) = pair
```

List literals infer `List<T>`. Empty collections need an expected element type. `append` returns a new list; `get` returns an optional value for a possibly missing index.

Use ordinary library factories for maps and sets initially:

```text
let counts = Map.empty<string, int>()
    .set("open", 3)
    .set("paid", 8)
let statuses = Set.from(["open", "paid"])
let openCount: int? = counts.get("open")
```

`Map.empty` constructs an empty map, and `set` returns a new map with the key inserted or its previous value replaced. `Set.from` collapses duplicate values. A leading member-access dot continues the preceding expression across a newline. These API names are provisional; key equality/hash constraints must be specified before implementation.

Tuples are immutable product values. Prefer named records for public messages and durable contracts. Native immutable collections can cross agent boundaries; resource handles and arbitrary mutable foreign collections cannot.

### 8.1 Method-local mutable builders

```text
fn positiveValues(values: List<int>): List<int> {
    let result = MutableList<int>()

    for value in values {
        if value > 0 {
            result.add(value)
        }
    }

    result.freeze()
}
```

`MutableMap<K, V>` follows the same rules, with `set(key, value)` and `freeze()`. Mutation methods such as `add` and `set` return `Unit`. `freeze()` produces an independent immutable `List` or `Map`; later builder mutation cannot affect that snapshot. Copy elimination is a future optimization only when safety is proven.

A builder belongs to the function/method/handler or lambda invocation that creates it. It cannot:

- Be a property of a `type`, service or agent, directly or through a wrapper.
- Be returned, enqueued or sent across an agent boundary.
- Be passed to another ordinary function, service, worker or foreign API under the initial strict rule.
- Be captured by a lambda, including a callback intended to run immediately; borrowed capture is deferred.
- Escape through a lazy view, iterator, tuple, generic wrapper or background task.

Compiler-recognized builder operations are permitted on their local receiver; they do not expose a general retention loophole. Suspension at an `await` may retain a builder in that same invocation, but must not share it with concurrent work.

All fields and nested elements of ordinary data remain immutable. `List<MutableList<int>>` is invalid as a data value. A read-only interface over a still-mutable collection is insufficient. The initial builders accept immutable data elements, so freezing the container cannot leave mutable contents behind. Borrowed helper parameters and optimized ownership transfer remain future work.

## 9. Functions, lambdas and extensions

### 9.1 Functions

```text
fn formatTotal(total: decimal): string {
    $"Total: {total}"
}

fn double(value: int) {
    value * 2
}
```

`fn` declares a function. Return types may be inferred for simple helpers; interface and handler contracts declare them explicitly. Calls use parentheses and preserve ordinary multi-parameter function types: `add(2, 3)`. The partial-application target in section 9.8 lets a positional prefix produce a function for the remaining parameters without rewriting every function type into curried form.

### 9.2 Lambdas are first-class function values

Lambdas may be assigned, passed as arguments and returned. They use `=>` and do not require `fn`.

```text
let doubled = numbers.map(value => value * 2)
let sum = numbers.fold(0, (total, value) => total + value)
let greeting = () => "Hello"
let square = (value: int) => value * value
let calculate = (price: decimal, quantity: int) => {
    let subtotal = price * decimal.fromInt(quantity)
    subtotal
}
```

`decimal.fromInt` is an illustrative explicit conversion. Parameters are inferred from the expected function type where possible; otherwise annotate them. Multi-expression bodies use braces and return the last expression. `return` exits the lambda, not the surrounding method.

### 9.3 Function types and higher-order functions

Proposed function-type syntax uses `->`, distinct from a lambda body's `=>`:

```text
let double: (int) -> int = value => value * 2
let add: (int, int) -> int = (left, right) => left + right
let greet: () -> string = () => "Hello"

fn apply<T, U>(value: T, transform: (T) -> U): U {
    transform(value)
}

let answer = apply(21, value => value * 2)
```

Multiple function arguments are separate parameters. A single tuple argument needs an extra pair of parentheses, for example `((int, int)) -> int`.

### 9.4 Captures and returned lambdas

```text
fn multiplier(factor: int): (int) -> int {
    value => value * factor
}

let triple = multiplier(3)
let result = triple(7)
```

The lambda captures the immutable `factor`. Capturing ordinary immutable values is allowed. Capturing method-local mutable builders or scoped resources is rejected under the initial rules. Service and worker capabilities may be captured only where their lifetimes permit; such closures are execution objects, not durable data or queue payloads.

Function values cannot be fields or union payloads in immutable data `type` declarations in this draft. They remain usable as local values, method parameters and function return values. This avoids disguising captured capabilities as serializable data.

### 9.5 Async and result-returning lambdas

```text
let validate = (amount: decimal) => {
    if amount > 0m {
        Success(amount)
    } else {
        Error(InvalidAmount(amount))
    }
}

let load: (InvoiceId) -> Task<Result<Invoice?, StoreError>> =
    async id => {
        await store.find(id)
    }
```

`async` explicitly creates an asynchronous lambda. The last expression becomes the task's result. Postfix `?` may propagate from a lambda only if that lambda itself returns a compatible result. It never returns from the outer method.

Using an async lambda with ordinary `map` produces a list of tasks if the types permit it; it does not implicitly await them or define a concurrency limit. Async collection execution needs an explicit library operation and policy.

### 9.6 Value piping

```text
let label = amount |> formatTotal
let adjusted = amount |> (value => value + 10m)
```

`value |> function` means `function(value)`. Chains associate left to right, and
call/member access binds more tightly. Evaluate the left value once, then the
right callable expression once, then apply it. The pipe supplies the first
remaining parameter under the partial-application target; remaining parameters
produce another function. A newline before `|>` continues the expression.

Pipes do not implicitly await, unwrap Result or Option, remove null, destructure
tuples, retry, parallelize or invoke a queue handler. Piping a function passes it
as a value; reusable composition uses an explicit lambda. Result-aware behavior
comes from ordinary helpers in section 9.9, including in HTTP preprocessing.

### 9.7 Extensions

```text
extend Invoice {
    fn label(self): string {
        $"Invoice {self.id}"
    }
}
```

`self` is an ordinary receiver parameter typed by the extension target. Extensions add statically resolved functions, not state. All extension methods are public. Conflicting extension resolution remains a compiler-design topic.

### 9.8 Automatic partial application

For `(A, B, C) -> R`, `f(a)` has type `(B, C) -> R`, `f(a, b)` has type
`(C) -> R`, and `f(a, b, c)`, `f(a)(b, c)` or `f(a)(b)(c)` executes the body.
Evaluate the receiver and supplied arguments once in source order when forming
the partial function; the body executes only when the remaining arguments arrive.
Prefer configuration first and flowing data last, such as `take(count, text)`.

```text
let preview = text |> trim |> replace("old", "new") |> take(80)
```

`f()` invokes a zero-argument function; for a function needing arguments it binds
an empty prefix and returns the callable without executing it. Too many arguments
are an error, and discarding a function-valued expression as a statement is an
error. Middle/final argument binding uses a lambda rather than holes.

The target includes D# functions, function values, service/interface methods and
supported extensions. Captured receivers retain dispatch and lifetime rules;
partial application cannot let builders or scoped resources escape. An async
partial starts no task until its arguments are complete and preserves `Task<R>`.
Infer generics from supplied arguments, expected function type and the remainder
of the pipe; ambiguity requires an annotation. Resolve a unique callable first.
Partially supplied named calls require a lambda. Defaults, variadic/overloaded
foreign calls and their partial application require separate interop rules.

### 9.9 Result composition helpers

These are standard library functions with callback first and flowing value last,
not keywords or special pipe behavior:

| Helper | Success behavior | Error behavior |
| --- | --- | --- |
| `andThen(next, result)` | Call the Result-returning next step | Preserve error, skip callback |
| `map(transform, result)` | Wrap transformed value in Success | Preserve error |
| `mapError(convert, result)` | Preserve success | Transform error |
| `tap(observe, result)` | Call Unit observer, preserve success | Preserve error |
| `tapError(observe, result)` | Preserve success | Call Unit observer, preserve error |

`andThen` supports Result or Task-of-Result inputs with callbacks returning Result
or Task-of-Result; any task makes the combined output a Task-of-Result. The other
four helpers accept either input form but their callbacks stay synchronous. Await
the input once and invoke the applicable callback at most once. `map` never
flattens Result, Option or Task. Error types must agree or be explicitly mapped.

Callback expressions are created eagerly even when the helper skips invoking
them. Faults/cancellation follow the normal contract, without automatic conversion
to Error, retries, parallelism or durability. `Success(null)` and `Success(None)`
remain successes. See the [exact overload and acceptance contract](DSharp_Pipes_and_Logging.md#4-result-composition-through-ordinary-functions).

`call(worker)` adapts a runtime-owned worker reference while preserving dispatch,
concurrency, deadlines and worker-call errors. `enqueue(agent)` adapts a queued
reference to its asynchronous durable-acceptance Result, whose success is a
receipt, not a handler result. `call(queueAgent)` and direct `.handle` dispatch
remain invalid.

## 10. Expressions, matching and control flow

### 10.1 Conditional expressions

```text
let description = if amount > 0m {
    "Payable"
} else {
    "Empty"
}
```

Conditions must be `bool`. An `if` used as a value requires `else`. An `if` without `else` is allowed only in a `Unit` context.

### 10.2 Exhaustive matching

```text
let message = match outcome {
    Success(invoice) => invoice.label()
    Error(NotFound(id)) => $"Invoice {id} was not found"
    Error(InvalidAmount(amount)) => $"Invalid amount: {amount}"
    Error(StorageUnavailable) => "Storage is unavailable"
}
```

Use `when` for a guard:

```text
match amount {
    value when value > 0m => Success(value)
    _ => Error(InvalidAmount(amount))
}
```

`_` is a wildcard. It also deliberately ignores a bound payload, as in `Success(_)`. Exhaustiveness checking covers unions, optionals and booleans. Open domains such as integers require a catch-all when guards do not prove coverage.

A wildcard intentionally absorbs future cases; adding a union case only forces updates where the match does not already cover it.

### 10.3 Early exit and iteration

```text
if amount <= 0m {
    return Error(InvalidAmount(amount))
}

for invoice in invoices {
    (await mail.send(invoice))?
}

Success()
```

`return` exits the nearest function/handler. `for ... in` visits values sequentially and evaluates to `Unit`; the body must have a `Unit` result. The snippet assumes `mail.send` returns `Result<Unit, E>` after awaiting, and the enclosing handler returns `Result<Unit, E>`.

Parallel iteration must use an explicit runtime/library API with bounded concurrency. Mutable `while` loops, `break` and `continue` are outside the initial syntax proposal.

## 11. Asynchronous operations and resources

### 11.1 Explicit asynchronous service/function contracts

```text
async fn fetchInvoice(id: InvoiceId): Result<Invoice?, StoreError> {
    await client.fetch(id)
}
```

The annotation is the logical result. Calling an `async` function produces `Task<Result<Invoice?, StoreError>>`; `await` obtains that result. Awaiting does not imply a new agent invocation or occupy a blocked worker thread while waiting for asynchronous I/O.

`async` must appear in interface signatures where asynchronous completion is part of the contract. An implementation can inherit that modifier from the interface.

Agent handlers are implicitly asynchronous execution boundaries, so their declarations do not require `async`. Ordinary functions containing `await` do. This makes asynchronous behaviour explicit at ordinary API boundaries without repeating it on every agent handler.

### 11.2 Scoped resources

Proposed resource syntax:

```text
using connection = (await db.open())? {
    await connection.execute(command)
}
```

The `using` block disposes its acquired resource on normal completion, early return and failure. If disposal is asynchronous, the containing context must be asynchronous and cleanup is awaited.

Resource values cannot escape their scope or cross agent boundaries. Disposal does not imply transaction rollback or undo external effects. Transaction helpers must define commit and rollback explicitly; their exact syntax is deferred.

## 12. Interfaces and default methods

Interfaces may define a default implementation for individual methods:

```text
interface InvoiceStore {
    // Required implementation
    async find(id: InvoiceId): Result<Invoice?, StoreError>

    // Default implementation
    async exists(id: InvoiceId): Result<bool, StoreError> {
        let invoice = (await find(id))?

        match invoice {
            null => Success(false)
            value => Success(true)
        }
    }
}

service SqlInvoiceStore(db: Database): InvoiceStore {
    find(id) {
        await db.findInvoice(id)
    }
}
```

The service implements `find` and uses the default `exists`. Calls to `find` from the default body dispatch to that service's implementation. A service may replace `exists` by defining a matching method, for example using a database existence query.

Rules:

- A signature without a body requires implementation; a body supplies a default.
- A matching service method takes precedence without an `override` keyword.
- Implementations can inherit parameter/return types and async markers from the interface signature.
- Default methods can call other methods on the same interface.
- Interfaces have no instance fields or constructor dependencies.
- All methods are public. There is no class/service implementation inheritance.
- Conflicting default bodies from multiple implemented interfaces require an explicit service implementation; do not silently choose one.

The body itself declares the default. There is no `default service` mechanism or `default` keyword.

Service constructor parameters declare dependencies. A service is ordinary dependency-backed code, not an automatically scheduled agent. Calling it does not enqueue work or invoke a supervisor. Runtime resource services may encapsulate pools under the ownership rules.

Select the implementation explicitly during application composition:

```text
app {
    bind InvoiceStore = MemoryInvoiceStore()
}
```

Providing default method bodies does not select a default service implementation or discover registrations automatically.

## 13. Workers, agents and handler outcomes

A **worker** is an agent that can be called and awaited. A **queued agent** accepts messages and reports each attempt's outcome to its supervisor. These are different reference types and invocation contracts.

| Declaration | Input | Handler outcome | How another agent interacts |
| --- | --- | --- | --- |
| `worker` | Typed request or `Unit` | `Result<T, E>` | Call and await a result. |
| `agent` with a queue trigger | Declared data `type` or `Unit` | `Result<Unit, E>` in this draft | Enqueue only; no awaited handler result. |
| `agent` with event/cron trigger | Declared event `type` or `Unit` | `Result<Unit, E>` in this draft | Publish an event or let the schedule enqueue work. |
| `agent` with HTTP trigger | Typed pipeline input | `Result<T, E>` | HTTP host dispatches; internal code calls a separate worker. |

Using `worker Name` for a callable declaration is the proposed spelling of the agreed distinction. Queue handlers use `Unit` for their success payload because the supervisor needs completion status, not a direct reply. Result reporting through events is explicit application behaviour.

```text
worker GetInvoice(store: InvoiceStore) {
    handle(id: InvoiceId): Result<Invoice?, StoreError> {
        await store.find(id)
    }
}
```

Both workers and triggered agents have one `handle` entry point, explicit dependencies and a result on every normal completion path. They may use `await` without an `async` modifier. A handler cannot fall through and implicitly succeed.

```text
agent SendInvoice(mail: MailService) {
    trigger Queue<Invoice>("invoice-delivery")

    handle(invoice: Invoice): Result<Unit, DeliveryError> {
        await mail.send(invoice)
    }
}
```

An agent reference is a runtime capability, not an ordinary instance exposing a directly callable `handle` method. Public visibility does not allow bypassing dispatch, queues or supervision. The compiler rejects calling a queued agent, taking its `handle` as a callback, or casting it to a callable worker.

The runtime may reuse infrastructure, but correctness must not depend on retained per-request data. The current baseline uses isolated invocations; persistent state and temporary-cache syntax remain separate decisions.

## 14. Worker calls, queue submission and supervision

### 14.1 Queue outcomes belong to the supervisor

A queued handler reports one attempt's result. The supervisor completes the message, applies eligible retries or retains a terminal failure according to its policy. The producer cannot await that handler's business result, including through a completion task returned by enqueueing.

| Attempt outcome | Meaning |
| --- | --- |
| `Success()` | Queued work completed successfully. |
| `Error(error)` | Declared application failure; policy decides what to do. |
| Unexpected fault | Execution failure outside the declared error union. |
| Cancellation/deadline | Execution-control outcome requiring a defined runtime policy. |

Retrying does not roll back external effects. Queue retries must not be imported implicitly into direct worker-call semantics.

### 14.2 Calling a worker

```text
worker InvoiceSummary(getInvoice: GetInvoice) {
    handle(id: InvoiceId): Result<string, CallError<StoreError>> {
        let invoice = (await getInvoice(id))?

        match invoice {
            null => Success("No invoice found")
            value => Success($"Invoice total: {value.total}")
        }
    }
}
```

Only worker references support the direct runtime call syntax `await worker(input)`. A queued agent may call a worker, and a worker may enqueue work, but the reverse interaction does not turn a queue into request/reply. Ordinary async service calls and I/O remain awaitable too; the restriction concerns waiting for an agent's handler result.

### 14.3 Worker-call failures — still provisional

To distinguish application errors from dispatch failures, timeouts and faults, this draft retains the proposed runtime wrapper:

```text
type CallError<E> {
    | Application(E)
    | Unavailable
    | TimedOut
    | Cancelled
    | Faulted(FaultId)
}
```

Calling a worker whose handler returns `Result<T, E>` would produce `Task<Result<T, CallError<E>>>`. This wrapper is not a finalized decision. A caller may map it into its own error type. `FaultId` points to diagnostic details without exposing them as a public error payload.

A timeout does not prove execution stopped or side effects did not occur. Automatic retries are not implied for worker calls; they need explicit policy and idempotency rules if introduced.

### 14.4 Enqueue acceptance, never handler completion

Proposed queue-reference API:

```text
let acceptance = await sendInvoice.enqueue(invoice)

match acceptance {
    Success(receipt) => Success(receipt.messageId)
    Error(error) => Error(error)
}
```

`enqueue` returns `Task<Result<EnqueueReceipt, EnqueueError>>`. Awaiting it waits for the configured durable acceptance guarantee, not processing. The receipt identifies the message; it contains no handler-result task. These library type/API names remain provisional.

```text
// Compile errors: a queued agent cannot be called as a worker
let result = await sendInvoice(invoice)
let result = await sendInvoice.handle(invoice)
```

Event publication follows the same acceptance-versus-processing distinction. Runtime inspection may expose job status operationally, but is not an agent request/reply contract. If business logic needs an eventual response, model a separate completion event or application workflow explicitly.

Rescheduling is not a business error. Its control API still needs to be reconciled with mandatory `Result` handler returns; no untyped third result case is implied.

## 15. Triggers and execution policies

Triggers and policies are inspectable declarations, not executable handler code. The names of factories such as `Queue`, `Event` and `Cron` are library-level names.

### 15.1 Queue

```text
agent SendInvoice(mail: MailService) {
    trigger Queue<Invoice>("invoice-delivery")

    policy {
        concurrency = 4
        scope = PerHost

        retry TemporaryFailure {
            maxAttempts = 5
            backoff = exponential
        }
    }

    handle(invoice: Invoice): Result<Unit, DeliveryError> {
        await mail.send(invoice)
    }
}
```

`TemporaryFailure` must identify a variant in `DeliveryError`. `maxAttempts` includes the first attempt. The declaration enables retry only for that variant, not every error or runtime fault. Runtime-fault retry policy is separate and remains to be designed. Backoff parameters and jitter defaults must be specified before this API is final.

Queue payload and handler input must match. Concurrency scope, sequential ordering, per-key partitioning and store capabilities retain the semantics of the feature specification; a numeric concurrency limit alone does not promise deployment-wide ordering.

### Queue message constraints

The top-level queue message must be a record or union declared with `type`, or `Unit`. A bare primitive, list, tuple, nullable root, function, service reference or worker reference is not a queue message; wrap data in an explicit named contract.

```text
type SendInvoices {
    invoiceIds: List<InvoiceId>
    note: string?
}

// Valid contracts
Queue<SendInvoices>("send-invoices")
Queue<Unit>("refresh-cache")

// Invalid roots
Queue<string>("send-invoices")
Queue<List<InvoiceId>>("send-invoices")
```

Data fields and union payloads are recursively immutable. Durable queue validation also requires serializable fields: callbacks, tasks, live connections, service/worker references and other runtime capabilities are not message data even if a reference cannot be reassigned. Immutable primitives, nullable data, nested data types and immutable collections of eligible elements are supported. Generic contracts are checked after substitution.

`Unit` is sent as `()`; a no-argument `handle()` is equivalent to receiving that no-payload input. Stable contract identities and payload evolution remain necessary for persisted messages. Event deliveries and scheduled queue payloads obey the same constraints.

### 15.2 Event subscription

```text
agent NotifyDoctor(notifications: NotificationService) {
    trigger Event<InvoiceCreated>(subscription: "notify-doctor")

    handle(event: InvoiceCreated): Result<Unit, NotificationError> {
        await notifications.invoiceCreated(event)
    }
}
```

The subscription gets its own queued delivery and independent outcome. Different subscriptions are not competing consumers of a single delivery.

### 15.3 Scheduled execution

```text
agent CheckOverdueInvoices(invoices: InvoiceService) {
    trigger Cron(
        name: "check-overdue-invoices",
        schedule: "0 8 * * *",
        timezone: "Europe/Sarajevo",
        missed: RunOnce
    )

    handle(): Result<Unit, InvoiceError> {
        await invoices.checkOverdue()
    }
}
```

`handle()` is a no-payload entry point and corresponds to `Unit` input. The cron expression dialect, daylight-saving behaviour and occurrence metadata contract still need specification. Stable names must survive source-symbol renaming where persisted work exists.

## 16. Typed HTTP pipelines

```text
agent CreateInvoice(invoices: InvoiceService) {
    trigger Http.post("/invoices")

    pipeline = async raw => {
        await (
            raw
            |> authenticateRequest
            |> andThen(authorizeCreateInvoice)
            |> andThen(decodeCreateInvoice)
        )
    }

    handle(request: Authorized<CreateInvoiceRequest, CreateInvoices>)
        : Result<Invoice, InvoiceError> {
        await invoices.create(request.body, request.identity)
    }
}
```

The contextual `pipeline` member holds an ordinary typed callable expression.
There is no general `pipeline {}` expression or list-of-steps composition block.
The example uses application/adaptor functions with explicitly compatible errors;
their transport types remain part of DEC-05. One typed envelope supplies the
handler input and carries the validated body and authorization context explicitly.

For this example, the library step contracts conceptually connect as follows, with each step able to fail:

| Step | Input | Successful output |
| --- | --- | --- |
| Authenticate | `HttpRequest` | `Authenticated<HttpRequest>` |
| Require permission | `Authenticated<HttpRequest>` | `Authorized<HttpRequest, CreateInvoices>` |
| Decode body | `Authorized<HttpRequest, CreateInvoices>` | `Authorized<CreateInvoiceRequest, CreateInvoices>` |
| Handle | The decoded authorized envelope | `Invoice` |

`CreateInvoices` is a permission-marker type. With all declarations public, a user-constructible record named `Authorized` is not unforgeable authorization evidence. The type describes the pipeline input; runtime authentication and permission checks enforce access. Sensitive operations must enforce their policy, and any future runtime-owned capability design must specify validity independently of visibility modifiers. Do not treat a type name alone as proof that a check happened.

The host invokes the preprocessing callable once and invokes `handle` only on
success. The compiler checks its input, successful output against `handle`, and
explicit HTTP error mappings. Without preprocessing, the binding contract directly
supplies the handler input. Ordinary pipes and section 9.9's Result helpers provide
composition; `|>` itself stays unaware of Result and Task. Different failure types
require explicit mapping. Exact transport/error types and HTTP status mapping APIs
remain DEC-05 work.

Route and query decoding, typed path parameters and response customization are follow-up syntax work. Request-specific authorization can be placed after loading a resource.

Wrapping middleware for tracing, logging or transaction scopes must be supported, including cleanup on failure/cancellation. This draft does not pretend a list of transformations provides wrapping automatically; the wrapper/continuation declaration remains open.

## 17. Application composition

```text
app {
    let database = SqlDatabase.fromConfig(databaseConfig)
    bind InvoiceStore = SqlInvoiceStore(database)

    workers.register(GetInvoice, InvoiceSummary)
    api.register(CreateInvoice)
    agents.register(SendInvoice, NotifyDoctor, CheckOverdueInvoices)
}
```

This is a composition sketch; other dependencies referenced by enabled handlers must also be bound. Resource initialization must still follow the startup lifecycle rather than opening connections merely by declaring metadata.

- `bind Contract = expression` chooses an implementation or resource recipe explicitly.
- `workers.register` enables callable worker definitions.
- `api.register` enables HTTP-triggered agent definitions.
- `agents.register` enables queue, event and scheduled agent definitions and validates their triggers.
- Registration takes typed definition references, not handler-name strings.
- Registration does not execute a handler or accept work before startup completes.
- The API router dispatches concurrent invocations; central registration is not a global sequential actor.
- Duplicate routes/identities, missing dependencies and invalid policies are rejected before accepting work. Statically known errors should be compile-time errors.

There is no preferred/default-service selection mechanism. Interface method defaults apply within the explicitly selected implementation.

Configuration and secret-source APIs remain open. Values may come from JSON/environment variables while application wiring stays in code. No application YAML grammar is introduced here.

### 17.1 Logger and output configuration

The [logging target](DSharp_Pipes_and_Logging.md#6-what-logger-is) defines `Logger`
as a runtime-owned system actor, registered once per application host. Its
`Logger.log(level, template, field: value, ...)` API synchronously captures an
immutable event and attempts non-waiting submission to a bounded transient
mailbox, then returns Unit. Callers do not await delivery or construct queued
messages. Database output persists after processing; it does not make admission
durable. Business queue acknowledgement rules are unchanged.

```text
using scope = Logger.scope(tenantId: request.tenantId) {
    Logger.log(Info, "Invoice {invoiceId} created", invoiceId: invoice.id)
}
```

Levels are standard `LogLevel` cases: Trace, Debug, Info, Warning, Error and
Critical. Templates are compile-time literals/constants with `{fieldName}`
placeholders and escaped `{{`/`}}`; each placeholder requires an event field,
duplicate fields are rejected and repeated placeholders evaluate their field once.
Extra fields are allowed. Interpolated strings are not structured templates.
Arguments are eagerly evaluated once in source order even for disabled levels;
`Logger.isEnabled(level)` permits an explicit expensive-work guard.

Fields accept immutable JSON-compatible scalars, nullable scalars, lists and
string-keyed maps. Records, unions and Option require explicit selection/mapping;
None is not silently null. Capture preserves numeric/boolean/null values and
numeric precision. Oversize/deep values and nonfinite floats are dropped with an
independent diagnostic/counter; a fault in a caller's field expression retains
ordinary expression behavior.

`Logger.scope` is a synchronous scoped caller-context helper, not an actor
message. It follows `using` cleanup on every exit and flows through awaited calls
without leaking between concurrent invocations. Merge outer, inner and event
fields in that order and capture the final snapshot at submission. Runtime
metadata is separate. Workers/queues transfer allowed correlation explicitly;
diagnostic context never grants authorization.

```text
app Billing {
    logging {
        minimumLevel = Info
        retention = 7d
        outputs = [ConsoleLogOutput(), DatabaseLogOutput(store: runtimeStore)]
    }
}
```

No configuration means Info minimum and one console output, with no database,
exporter or retention job. An explicit output list replaces that default; an empty
list disables delivery. Output recipes resolve before traffic, are host-scoped,
and remain fixed for that host lifetime. Bootstrap console events are not replayed.

`LogOutput` is the service contract
`async write(events: List<LogEvent>): Result<Unit, LogOutputError>`; success means
the whole immutable batch met that output's delivery guarantee. There is no
partial-batch acknowledgement or untracked background-success path. The actor
bounds admission and each output's backlog, retries only the failed output, never
overlaps a timed-out write with its retry, and lets healthy outputs progress.
Delivery failure diagnostics bypass Logger and are rate-limited. Shutdown drains
producers, then Logger, then outputs before closing a shared RuntimeStore.

The opt-in database output uses that same RuntimeStore with separate versioned
logging tables. Batches commit atomically and deduplicate stable event IDs.
Retention defaults to seven days from database receive time, with explicit
keep-forever and an initial 256-MiB logical retained-data budget. Cleanup never
removes queue rows or participates in business transactions. Third-party outputs
own/document their retention mechanism; the minimal write interface promises no
remote retention control.

The separate read-only `LogReader` provides typed, parameterized, bounded queries
and stable cursor pagination for the authenticated observer. Missing fields and
explicit null differ, numeric comparisons preserve numeric meaning, and timeouts
are failures. History is unavailable when no query-capable output is configured;
console output is not an indefinite searchable buffer. The companion specifies
the complete [delivery budgets](DSharp_Pipes_and_Logging.md#10-what-the-actor-does),
[event schema](DSharp_Pipes_and_Logging.md#11-event-data-and-json),
[retention](DSharp_Pipes_and_Logging.md#12-database-storage-and-retention) and
[query contract](DSharp_Pipes_and_Logging.md#13-searching-stored-logs).

## 18. Modules and public declarations

```text
module Billing

import DSharp.Http
import DSharp.Messaging

type Invoice {
    id: InvoiceId
    total: decimal
    note: string?
}

fn normalizeNote(note: string?): string {
    note ?? ""
}
```

**All declarations and members are public.** There are no `public`, `private`, `protected` or `internal` modifiers. Lexical scope still applies: a local binding is not addressable outside its function simply because declarations have public visibility.

A file declares one module; files may contribute to the same module. Modules organize names. Imports make named modules available without discovering or registering handlers. Public visibility does not change mutability, resource lifetimes or worker/queue invocation restrictions.

Data constructors are public. Validation must occur at the operation/boundary where required rather than depending on private constructors. Execution-owned state and runtime handles still follow their own ownership and validity rules.

Package manifests, import aliases and F#/C# interop syntax remain deferred.

## 19. Keywords and operators

### 19.1 Proposed core keywords

| Keyword | Purpose |
| --- | --- |
| `module` | Declare a module. |
| `import` | Make a module available. |
| `type` | Declare a record or discriminated union. |
| `interface` | Declare a service contract. |
| `service` | Declare dependency-backed implementation code. |
| `agent` | Declare a triggered agent, including message-only queue agents. |
| `worker` | Declare a callable agent whose result may be awaited. |
| `fn` | Declare an ordinary function. |
| `async` | Declare an asynchronous ordinary function or service method. |
| `await` | Await asynchronous completion. |
| `let` | Bind an immutable value or destructure a tuple. |
| `return` | Explicit early return. |
| `if`, `else` | Conditional expression or Unit-valued conditional. |
| `match`, `when` | Pattern matching and guards. |
| `for`, `in` | Sequential collection iteration. |
| `with` | Copy a record with specified field changes. |
| `extend` | Declare extension functions. |
| `using` | Scope a resource and its disposal. |
| `app` | Declare application composition. |
| `true`, `false`, `null` | Boolean and absence literals. |

### 19.2 Contextual declaration words

| Word | Meaning in its declaration context |
| --- | --- |
| `handle` | Required agent entry point. |
| `trigger` | Trigger declaration inside an agent. |
| `pipeline` | Contextual HTTP preprocessing member whose value is an ordinary typed callable. |
| `policy` | Execution-policy declaration. |
| `retry` | Retry rule inside a policy. |
| `bind` | Dependency binding inside application composition. |

Words such as `concurrency`, `scope`, `maxAttempts` and `backoff` are policy field names, not globally reserved keywords. `Http`, `Queue`, `Event`, `Cron`, `Result`, `Success`, `Error`, `Unit` and `CallError` are types, cases or library names. Primitive names are built-in type names. `_` is special wildcard syntax.

### 19.3 Operators and punctuation

| Form | Meaning |
| --- | --- |
| `=` | Binding, field initialization or declarative setting; not reassignment. |
| `==`, `!=` | Equality and inequality for types supporting equality. |
| `<`, `<=`, `>`, `>=` | Ordered comparisons. |
| `+`, `-`, `*`, `/`, `%` | Numeric arithmetic; `/` on integers truncates toward zero. |
| `!` | Prefix Boolean negation only. |
| `&&`, `\|\|` | Short-circuit Boolean operators. |
| `T?`, `?.`, `??`, postfix `?` | Optional types/access/fallback and result propagation, as defined above. |
| `\|>` | Pass a value to a function. |
| `=>` | Lambda body or match-arm expression. |
| `->` | Function-type return separator, as in `(int) -> int`. |
| `:` | Type annotation or implemented interface. |
| `\|` | Union-case declaration marker. |
| `.` | Member access or qualification. |
| `<T, E>` | Generic parameters/arguments in a type or generic-call context. |
| `()`, `{}`, `[]` | Calls/tuples/Unit; blocks/records; list literals. |

Proposed precedence, highest to lowest: member access/calls/postfix propagation; prefix operators including `await`; multiplicative arithmetic; additive arithmetic; comparisons; equality; `&&`; `||`; `??`; `|>`. Use `(await call())?` for propagation after awaiting. Binary operators are left-associative except `??`, which is right-associative. Chained comparisons are rejected; write `a < b && b < c`.

Generic brackets and type suffixes are parsed in type/generic contexts, not as arithmetic operators. Full parser disambiguation rules remain an implementation task. There is no ternary `?:`; `if` already returns a value.

## 20. Complete worked example

This example uses an in-memory implementation, a default interface method, a lambda and nullable matching. No external database API is assumed.

```text
module BillingExample

type InvoiceId {
    | InvoiceId(int)
}

type Invoice {
    id: InvoiceId
    total: decimal
    note: string?
}

type StoreError {
    | Unavailable
}

interface InvoiceStore {
    async find(id: InvoiceId): Result<Invoice?, StoreError>

    async exists(id: InvoiceId): Result<bool, StoreError> {
        let invoice = (await find(id))?

        match invoice {
            null => Success(false)
            value => Success(true)
        }
    }
}

service DemoInvoiceStore: InvoiceStore {
    find(id) {
        match id {
            InvoiceId(1) => Success(Invoice {
                id = id
                total = 125.50m
                note = null
            })

            _ => Success(null)
        }
    }
}

worker GetInvoice(store: InvoiceStore) {
    handle(id: InvoiceId): Result<Invoice?, StoreError> {
        await store.find(id)
    }
}

worker InvoiceSummary(getInvoice: GetInvoice) {
    handle(id: InvoiceId): Result<string, CallError<StoreError>> {
        let found = (await getInvoice(id))?
        let describe = (invoice: Invoice) => {
            let note = invoice.note ?? "No note"
            $"Total: {invoice.total}. {note}"
        }

        match found {
            null => Success("No invoice found")
            invoice => Success(describe(invoice))
        }
    }
}

app {
    bind InvoiceStore = DemoInvoiceStore()
    workers.register(GetInvoice, InvoiceSummary)
}
```

The service inherits the default `exists` body and the `find` signature's async contract. `GetInvoice` returns missing data as `Success(null)`. `InvoiceSummary` matches either null or the non-null invoice, then passes that value to a lambda.

Only workers are directly callable here. Queue submission uses the separate API in section 14. The provisional `CallError` wrapper can become nested across worker layers; applications may map it to a stable domain error instead. Registration enables definitions but does not invoke them.

## 21. Diagnostics and open decisions

### 21.1 Examples of required diagnostics

| Invalid code or declaration | Expected diagnostic |
| --- | --- |
| `let name: string = null` | Absence requires `string?`. |
| `let name = null` without context | Cannot infer the nullable value type. |
| `optionalName.length` | Handle null or use conditional access. |
| `optionalName?` | Postfix propagation expects a result, not a nullable value. |
| A nullable match with only a named value arm | Handle null as well. |
| `result?` in a function returning `string` | A compatible result return type is required. |
| Propagating an incompatible error union | Map the error or change the contract. |
| Empty `handle(): Result<Unit, E> {}` | Return an explicit result. |
| Ignoring a returned `Result` | Handle, propagate, return or forward it. |
| Discarding a function-valued expression | Bind, return or pass the function; check for missing arguments. |
| Incompatible pipeline steps | Output does not satisfy the next input contract. |
| `Logger.log` template without a matching event field | Supply the named field explicitly; hidden scope fields do not satisfy placeholders. |
| Duplicate logging fields or an interpolated structured template | Use unique named fields and a literal/constant placeholder template. |
| Missing required interface method | Implement the method or provide an interface default body. |
| Conflicting interface defaults | Resolve with an explicit service implementation. |
| A mutable builder field, return value or capture | Builder cannot escape its creating method. |
| A live connection in a queued message | Only immutable serializable data may be queued. |
| `Queue<string>(...)` | Use a declared data type or Unit as the message contract. |
| `await queueAgent(message)` | Queued agents accept messages only; call a worker to await a handler result. |
| `queueAgent.handle(message)` or a reference to that entry point | Runtime-only entry; dispatch cannot be bypassed. |
| Visibility modifiers | All declarations are public; remove the modifier. |

### 21.2 Established choices in this revision

- Public-only declarations, without visibility keywords.
- Native `T?` with direct present-value/null matching and `Success(value)` construction; explicit `Option<T>` with distinct `None` and `Some(T)` cases.
- Default bodies for individual interface methods, not default service selection.
- Immutable data properties and method-local mutable collection builders with non-escape restrictions.
- Callable workers separated from queued agents; queue acceptance does not expose handler completion.
- Queue roots restricted to declared data types or Unit, with recursively immutable and serializable contents.
- First-class lambdas, including typed signatures, captures, returned functions and async bodies.
- Positional-prefix partial application while preserving multi-parameter function types and capture/lifetime rules.
- Plain value pipes, ordinary Result composition helpers and callable `pipeline = ...` HTTP preprocessing.
- Host-scoped transient Logger, typed structured fields/scopes, console-only defaults and opt-in shared-RuntimeStore logging/search.

### 21.3 Remaining design work

1. Finalize worker-call failure reporting and the provisional `CallError` wrapper.
2. Confirm exact function-type and async spellings, parser precedence and generic disambiguation.
3. Specify wrapping middleware, typed path/query binding and HTTP transport/error/response mappings; callable preprocessing and Result helper composition are specified in the companion.
4. Define authorization enforcement and runtime capability validity under public-only declarations.
5. Reconcile result-returning queued handlers with rescheduling and other supervisor-control operations.
6. Finalize queue acceptance guarantees, receipt API, contract versioning and serialization constraints.
7. Finalize numeric names, collection APIs, equality constraints, time types and JSON encoding details.
8. Define safe .NET interop without mutable-builder escapes or unchecked foreign nulls/exceptions.
9. Define warm-instance/cache rules without weakening invocation isolation.
10. Produce a formal grammar and compiler tests. These examples are design examples, not compiled programs.

Borrowed mutable-builder parameters/captures, general local-variable reassignment, advanced generic constraints, raw strings, package manifests, filename extensions and CLI syntax remain deferred. gRPC remains deferred.

## 22. Discoverable agent contracts and developer tooling

### 22.1 Required and available members

Programmers should discover agent requirements through compiler diagnostics, editor completion and generated contract documentation. A new agent declaration is not an unstructured collection of specially named methods.

| Declaration | Required | Optional | Invalid |
| --- | --- | --- | --- |
| `worker` | Exactly one typed `handle` | Helper methods; explicitly supported worker policies | Queue/event/cron/HTTP trigger; queue retry policy |
| Queue `agent` | Exactly one queue `trigger` and one `handle` | Queue `policy`, helper methods | Direct invocation or awaited handler completion |
| Event `agent` | Exactly one event `trigger` and one `handle` | Subscription delivery policies, helper methods | Direct invocation or awaited handler completion |
| Scheduled `agent` | Exactly one schedule `trigger` and one `handle` | Schedule/queue policies, helper methods | Direct invocation or awaited handler completion |
| HTTP `agent` | Exactly one HTTP `trigger` and one `handle` | Typed `pipeline = callable`, supported HTTP policies, helper methods | Inheriting queue retry rules implicitly |

Exactly one trigger per `agent` is the proposed initial grammar constraint; sharing logic is done through services/workers. The compiler rejects unsupported declaration members and duplicate reserved members. It must not silently ignore a misspelled `handle`, `trigger` or policy field.

`handle`, `trigger`, `pipeline` and `policy` are contextual language members. Trigger factory names and policy values are typed library symbols. These contracts do not require the programmer to repeat an extra agent interface declaration.

### 22.2 Deriving the handler signature

```text
agent SendInvoice {
    trigger Queue<SendInvoiceMessage>("invoice-delivery")
}
```

Once the trigger is resolved, the compiler knows the message type, valid handler shape and supported policy category. For this example it must report a missing handler with the expected shape:

```text
SendInvoice requires:
handle(message: SendInvoiceMessage): Result<Unit, E>

Choose the application error type E and implement the handler.
```

The trigger does not invent the error type. A code action may leave an editor placeholder for it; that placeholder is not valid completed source. Before a trigger is present, completion shows available trigger categories and the need for `handle` without guessing a message type.

Queue/event message types must satisfy section 15. Scheduled handlers use Unit input unless a later approved contract defines an immutable occurrence message. A worker's explicit handler signature supplies its input/output/error types. HTTP's trigger and pipeline supply its final input contract.

### 22.3 One shared semantic contract

The compiler, registration generator, documentation generator and language server should use the same resolved contract model. It should describe:

- Declaration kind, source location and required/optional members.
- Input, success and application-error types.
- Invocation capability: callable worker, enqueue-only reference, event subscription, schedule or HTTP dispatch.
- Supported typed trigger and policy definitions, including defaults and restrictions.
- Dependencies, stable runtime identities and startup validation requirements.
- Documentation and diagnostic identifiers attached to those definitions.

Avoid separate hard-coded signature lists in the editor and compiler. Packages can provide typed trigger/policy metadata through a bounded supported extension mechanism; installing a package must not run code during completion or discover application handlers automatically. The exact metadata API is not yet frozen.

### 22.4 Editor behaviour

The initial tooling target should provide:

1. Diagnostics on incomplete declarations, with source ranges and expected signatures.
2. Completion for `handle`, `trigger`, `pipeline`, `policy` and ordinary helper methods in the correct declaration context.
3. Completion and documentation for valid fields inside the selected policy type.
4. Hover text showing the resolved message, result and invocation contract.
5. “Implement required members” to insert the missing signature, preserving existing code.
6. Go-to-definition for the message type, handler and referenced service/worker contracts.
7. Signature help and inferred parameter types for lambdas, service methods and worker calls.

The CLI must produce the same semantic errors without an editor. An initial compiler milestone may deliver CLI diagnostics first, with language-server features in a later milestone. Syntax highlighting alone is not contract-aware completion.

### 22.5 Required validation examples

| Case | Expected outcome |
| --- | --- |
| Queue trigger present, handler absent | Missing-handler diagnostic with the trigger's message type. |
| Handler present, triggered agent has no trigger | Missing-trigger diagnostic. |
| Queue message differs from handler parameter | Input mismatch diagnostic before code generation. |
| Queue handler returns a business payload | Reject under the initial `Result<Unit, E>` queue contract. |
| Worker declares a queue trigger | Reject incompatible declaration kind. |
| Misspelled policy field | Unknown-field diagnostic, never a silently ignored setting. |
| Editor adds a required handler | Correct signature inserted without deleting existing members. |
| Queue reference used as a function or callback | Reject bypass of runtime-owned queue dispatch. |

These checks are language/type constraints, independent of all-public visibility. Some policy capabilities and registration conflicts depend on the configured store or environment and therefore must also be validated at startup.
