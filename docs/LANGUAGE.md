# Type system and syntax

**Source precedence:** the selected requirements in the current syntax reference,
[section 21.2](features/DSharp_Syntax_Reference.md), and the handoff's section 4
supersede older conflicting examples here. In particular, declarations are public,
interfaces may supply method defaults, `T?` matches present values or null,
`Success(value)` constructs results, local builders cannot escape, and callable
workers differ from enqueue-only agents. No default-service selection is implied.
The [pipes and logging target](features/DSharp_Pipes_and_Logging.md) supersedes
older composition/logging details: positional-prefix partial application,
ordinary Result helpers, callable HTTP preprocessing and host-scoped Logger.
Remaining syntax/API proposals stay provisional. See the
[implementation status](features/DSharp_Implementation_Status.md) for currently
executable behavior; importing a target specification does not implement it.

This document separates established language semantics from syntax that has not
been designed. It is based on [Feature specification v0.2](design/FEATURES.md),
sections 1–6 and 11–13. See [Architecture](ARCHITECTURE.md) for runtime behavior.
The older conceptual examples below are not compiler fixtures; see the precedence note above.

**Grammar beyond the approved bootstrap and selected revision 0.3 requirements remains open.** The notation below is
pseudocode for reviewing contracts, not a proposed executable grammar. It does
not select declaration keywords, punctuation, primitive type names, or generic
syntax. This document must not be treated as a finalized parser specification.

## Established type-system direction

| Area | Established semantics | Details still open |
| --- | --- | --- |
| Typing | Static, strong typing. | Inference, coercion, subtyping, and numeric rules. |
| Data | Immutable record-like values with value equality; nested collections are immutable. Updates create new values. | Declaration/update syntax, equality details, collection APIs. |
| Unions | Discriminated alternatives and exhaustive pattern matching. | Variant syntax, representation, matching grammar. |
| Results | Typed success and failure. | Exact result types, propagation, and interaction with other failure channels. |
| Contracts | Interfaces have no inherited implementation or state; services can implement multiple interfaces, and several services can implement one interface. | Interface/service declarations and resolution syntax. |
| Composition | Functions and service methods connect through typed inputs and outputs, accounting for asynchronous completion and typed failures. | Operators, combinators, context representation, wrapping APIs. |
| JSON | Built-in serialization and typed decoding with structured errors. | Naming rules, schema evolution, decoding APIs, custom converters. |
| Resources | Scoped ownership for connections, streams, and transaction handles. | Compiler/runtime enforcement mechanism and syntax. |

Application code has no implementation inheritance. Reuse comes through
composition, service interfaces, and extension methods. Optional values,
generics, and explicit error propagation are proposed supporting features;
their precise rules are not settled. No default-nullability, implicit-conversion,
or generic-variance rule is established by the source.

## Immutable data

A record-like value groups typed fields and uses value equality. Nested
collections remain immutable; copying only the outer record while sharing a
mutable list would violate the design. An update produces a new value.

Conceptual notation:

```text
OrderRequest contains:
  customer identifier
  immutable collection of order lines

revised request = a new OrderRequest with an updated collection
original request remains unchanged
```

Mutation of a field or a nested collection in place is incompatible with this
model. The source does not yet define record construction, field access, update
expressions, primitive types, collection literals, or the equality algorithm for
all supported values. Foreign mutable objects cannot silently become immutable
merely by appearing inside a record.

## Unions and exhaustive matching

Discriminated unions describe known alternatives, each potentially carrying data.
Pattern matching must cover every alternative. Adding a variant exposes
previously exhaustive matches that now need another case at compile time.

```text
illustrative union OrderOutcome:
  Accepted carrying a receipt
  Rejected carrying a reason

match outcome:
  Accepted -> handle the receipt
  Rejected -> handle the reason
```

A match that covers only `Accepted` is invalid for this two-variant example.
Adding another variant requires handling it in matches that would otherwise
become incomplete. Exact pattern forms and rules for catch-all patterns remain
unspecified; this example establishes no grammar.

## Results, optional values, and failure

Typed results distinguish success from failure and let composition account for
both paths. A conceptual handler might produce either a receipt or a domain
failure. The specification does not choose names such as `Result`, `Ok`, or
`Error`, nor does it establish a propagation operator.

Optional values, generics, and explicit error propagation remain proposals.
Their representation and rules must be designed before examples can claim to be
valid source code. There is no settled equivalence between optional values,
foreign nulls, results, exceptions, cancellation, and timeouts.

Ordinary handler failures are contained by the host and callers receive failure
or timeout. That runtime guarantee does not define how those outcomes are
represented in the language's type system. Fatal CLR/native failures may still
terminate the process. HTTP response mappings likewise belong to the boundary
and are not yet specified as exact status-code rules.

## Interfaces, services, and dependencies

Interfaces declare contracts, without inherited implementation or state. Several
services may implement one interface, and one service may implement several
interfaces. Application composition explicitly supplies dependencies, allowing
alternative implementations and test doubles without a global service locator.

Service references expose contract operations without exposing private state.
Invocations normally own fresh private state per trigger. Typed messages carry
immutable data between services; services cannot directly access one another's
private state. Persistent actors remain unresolved and must not be inferred from
sequential queue policies.

```text
illustrative contract ReceiptSender:
  input: immutable receipt message
  outcome: typed success or delivery failure

application supplies a ReceiptSender implementation to a handler
handler calls through the contract, without accessing implementation state
```

No service declaration keyword, call syntax, dispatch syntax, or lifetime
annotation is selected by this notation.

## Typed composition and middleware

A step accepts known input and produces the input required by its successor.
Composition checks must account for asynchronous completion and typed failures.
Ordinary functions and service methods both participate; service calls retain
isolation and ownership rules.

The arrows below describe data flow, not an operator:

```text
request
  -> authenticate
request plus verified identity
  -> check permission
request plus required identity/permission context
  -> decode body while preserving required context
body plus required identity/permission context
  -> business handler
response or typed failure
```

These are conceptual shapes, not selected identity/evidence types. A pipeline
that drops identity before connecting to a handler requiring it is incompatible.
A decoder whose successful output cannot satisfy the next step is incompatible.
A typed failure can stop the pipeline and must be handled by its boundary.
No casting, untyped property bag, or global request context is required.

Authorization may follow resource loading instead of preceding body decoding.
If authorization evidence is represented as a type, arbitrary application values
must not be able to impersonate it. Its construction must be controlled and its
scope appropriate to the resource/operation. Type checking verifies the contract;
it does not evaluate credentials or policies at compile time.

Wrapping middleware also needs a way to invoke downstream execution and release
scoped resources on completion, failure, or cancellation. Input/output
transformations alone do not express this requirement. The exact wrapper API,
context mechanics, composition syntax, identity types, and evidence representation
remain open.

## Resource and interop boundaries

| Value | Permitted use |
| --- | --- |
| Immutable data | Share without mutation. |
| Service reference | Invoke contract operations, without accessing private state. |
| Managed resource capability | Perform safe operations on runtime-managed infrastructure. |
| Connection or stream | Exclusive use within its bounded scope. |
| Transaction handle | Own one connection in scope; cannot escape or be sent to another actor. |

An example that sends a transaction handle to another actor violates the design.
The source does not select linear types, borrow checking, special scope keywords,
or another particular enforcement technique. Both compiler checks and runtime
boundaries will need explicit design.

.NET interoperability must preserve these contracts. Mutable foreign objects
need conversion or wrapping at isolation boundaries. Foreign nulls and exceptions
need explicit mapping into the eventual optional/error models. Tasks,
cancellation, deterministic disposal, CLR APIs, and F# values need supported
adapters or compiler/build integration. Compatibility with every NuGet package
is not implied by .NET targeting. Safe escape hatches remain unresolved.

## Handler declarations and application syntax

The selected direction is strongly typed code-based registration, not application
YAML. Each handler declares its trigger beside its typed implementation; the
central application explicitly enables handlers and supplies dependencies and
stores. Trigger metadata is inspectable before execution.

| Handler kind | Required declaration information |
| --- | --- |
| HTTP | Method, route, pipeline, typed handler contract. |
| Queue | Stable queue identity, payload contract, default execution policies. |
| Event | Event contract, stable subscription identity, default delivery policies. |
| Schedule | Stable schedule identity, cron expression, timezone, missed-run policy. |

Installing a package must not start handlers or discover them automatically.
Explicit registration groups are permitted. Compiler-generated versus
library-defined declaration/registration APIs remain open. JSON and environment
variables can still supply configuration values independently of this wiring.

Static checks cover known pipeline connections, signatures, and referenced
contracts. Startup checks cover duplicate routes/identities, missing dependencies,
incompatible registrations, and unsupported store policies where runtime settings
are needed. Runtime checks still decode untrusted input, evaluate permissions,
and handle external failures.

## Syntax work remaining

The supplied specification explicitly delegates exact syntax to a later design.
The following must be settled before this document can become a complete language
reference or support a parser:

- Lexical rules: identifiers, comments, whitespace, literals, strings, and keywords.
- Modules, imports, visibility, and package references.
- Primitive types, collection types, records, construction, field access, and updates.
- Union declarations, patterns, and exhaustive-match grammar.
- Function declarations, calls, expressions, control flow, precedence, and associativity.
- Interface, service, extension-method, and handler declarations.
- Result, optional-value, generic, async, cancellation, and propagation rules.
- Composition, context preservation, wrapping middleware, and evidence construction.
- Dependency wiring, trigger metadata, scoped resources, and foreign API access.

No executable syntax or EBNF is supplied because choosing it would introduce new
language decisions beyond the source. These open items do not block documenting
the established semantic baseline; they do block claiming a finished syntax
specification or implementation.
