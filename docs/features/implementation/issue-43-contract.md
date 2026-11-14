# Issue 43: Proposed interface and service contract

**Status: proposal for maintainer review; not adopted or implemented.**
**Issue:** [#43](https://github.com/gajo357/dsharplang/issues/43).
**Base:** `103a146bc1a4a6d7a69d4f675e4cc76131bcd48f`.

This is a bounded proposal for the remaining declaration, construction and call
rules needed by #43. The [syntax reference](../DSharp_Syntax_Reference.md#12-interfaces-and-default-methods)
illustrates one implemented interface but does not settle the multi-interface
form, collisions or construction. Merging this proposal does not adopt it. The
existing compiler does not yet implement these examples. Explicit maintainer
adoption is required before implementing the proposed contract.

## Illustrative D#

```text
module Pricing

interface Price {
    calculate(value: int): int
}

interface CanQuote {
    eligible(value: int): bool
}

// A service's explicitly supplied dependency is available in its methods.
service Fixed(amount: int): Price, CanQuote {
    calculate(value) { amount }
    eligible(value) { value > 0 }
}

service Doubled(amount: int): Price {
    calculate(value) { amount * 2 }
}

// An interface-typed dependency is also explicitly provided.
service WithFee(underlying: Price, fee: int): Price {
    calculate(value) { underlying.calculate(value) + fee }
}

fn use(pricer: Price): int {
    pricer.calculate(0)
}

fn answer(): int {
    let first: Price = Fixed(20)
    let second: Price = Doubled(11)
    let quote: CanQuote = Fixed(20)
    if quote.eligible(1) { use(first) + use(second) }
    else { 0 }
}

fn injected(): int { use(WithFee(Fixed(20), 2)) }
```

If adopted and implemented, `answer()` should return 42 and `injected()` should
return 22. `Fixed` implements both interfaces; `Doubled` is an alternative
`Price`. The caller explicitly constructs and passes the intended instance.
Nothing discovers or registers a default service. These names are examples, not
standard APIs.

## Proposed declarations and matching

- `interface Name { ... }` declares a nominal interface with named method
  signatures. A signature uses `name(typedParameters): ReturnType`, or
  `async name(typedParameters): LogicalResult`. Parameters and return types are
  explicit. Braces do not contain fields or constructor dependencies. Interface
  inheritance and generic interface declarations are outside this first slice;
  supported generic data types may still occur in method signatures.
- `service Name(optionalTypedDependencies): Interface1, Interface2 { ... }`
  declares a nominal implementation. The parenthesized dependency list may be
  omitted only when empty. The comma-separated interface list has at least one
  entry. Duplicate interface names, unknown/non-interface entries and attempts to
  extend a service are errors; services do not inherit implementation.
- A service method body uses `name(parameters) { ... }`, following the syntax
  reference's inherited-signature example. Its parameter count must match its
  interface method. Parameter annotations, return annotation and `async` marker
  may be omitted and are inherited from the unique matching contract. If supplied,
  each must agree exactly with that contract. Parameter names may differ because
  calls are positional. The body is checked under the resolved signature, including
  Unit, nullability, Result propagation and the async await boundary.
- Method names are unique within an interface and within a service; there is no
  overload selection in this slice. When multiple implemented interfaces declare
  a same-named method, one service method satisfies them only if their complete
  parameter types, logical result and async marker are identical. Conflicting
  contracts, duplicate method bodies, and missing required methods are errors.
  To keep the initial surface bounded, a service method must implement a declared
  contract method; standalone service methods are deferred.
- All declarations and contract methods are public. Interface methods with bodies,
  default-method conflict rules and dispatch through default bodies belong to #44;
  #43 rejects a body in an interface instead of ignoring it.

## Proposed construction, typing and dispatch

- `Name(arguments)` constructs a service explicitly. Arity and dependency types
  are checked as for ordinary calls; argument expressions evaluate once, left to
  right. The constructed service retains its supplied dependencies for its
  lifetime. Its dependency names are immutable lexical bindings visible to its
  methods, not externally accessible fields; duplicate dependency names, a
  dependency with the same name as a service method, and method parameters or
  locals that shadow dependencies are errors. Construction performs no implicit lookup,
  application registration, resource acquisition or user-defined startup action.
  This first slice permits immutable data and non-null service/interface references
  as dependency types; Task or function values, builders and scoped resources are
  rejected as dependencies pending their lifetime contracts.
- A service value is usable at its concrete service type and may be passed,
  bound or returned as an interface it explicitly implements. There is no
  conversion between unrelated interfaces, no implicit service selection, and
  no unchecked cast. Calling an interface-typed value dispatches to the supplied
  implementation; calling a concrete service value invokes its checked method.
  Multiple service instances retain their own supplied dependencies. Where an
  interface type is explicitly expected, branches may independently supply an
  implementing service; without an expected type, do not guess a common interface
  for two concrete implementations.
- Methods are ordinary calls on the receiver, not worker invocations, queue
  submissions or supervised work. Receivers and arguments evaluate once in source
  order. An `async` method returns `Task<LogicalResult>` and is awaited explicitly,
  just like an async function. Typed failures and thrown faults keep their existing
  behavior; the call adds no retry, scheduling or implicit await. Unqualified
  calls in a method resolve ordinary functions using existing lexical/module rules;
  this slice does not introduce `self` or unqualified calls to other methods on
  the same service. Calls through an explicitly supplied interface dependency are
  supported, as shown by `WithFee` above.
- Service and interface references are execution capabilities, not immutable data.
  They may be used as checked function/method parameters, returns, locals and
  constructor dependencies, but cannot be placed in record/union payloads or
  immutable collections, captured by lambdas, or compared for equality in this
  initial slice. Preserve recursive generic data checks so a `Box`, `Option`,
  `List`, `Map` key/value or generic forwarding function cannot hide a capability
  in data; builder, task, and scoped-resource lifetime rules continue to apply.
  Generic functions may pass a capability as an execution value where their body
  does not require immutable data. An async function or method may return an
  interface reference as its logical result, producing `Task<Interface>`; neither
  that task nor its capability becomes immutable or serializable data.
  Nullable capability types, capability borrowing and lifetime
  transfer, resource APIs and external .NET interop remain separate decisions.
- Reuse DEC-02's module/import visibility, duplicate/ambiguity diagnostics and
  value/type namespace rules. A service's constructor occupies the value namespace
  like a record constructor; no overload, import-order choice or implicit DI
  selection is introduced. Generic service declarations and service/interface
  extension interactions remain separate work. Method-valued expressions and
  partial application of methods are deferred to [#129](https://github.com/gajo357/dsharplang/issues/129),
  which retains its receiver-capture and async-deferral requirements.

## Evidence required if adopted

| Acceptance criterion | Required evidence |
| --- | --- |
| AC1 | Compile and execute two implementations of one interface, a single service implementing two interfaces, direct calls and checked interface-typed calls; reject implementation inheritance and unknown/duplicate interfaces. |
| AC2 | Exercise inherited and explicitly matching synchronous/asynchronous signatures; reject missing methods, mismatches, duplicates, conflicting same-name contracts, and interface bodies reserved for #44. |
| AC3 | Execute two independently constructed instances and injected interface dependencies, proving left-to-right, once-only evaluation and dynamic dispatch; reject arity/type mismatches, unauthorized data/capture escapes and ambiguous imports. |

Use actual D# examples and generated C# execution tests, including async
suspension and faults. Preserve existing compiler tests, generated-code behavior,
the .NET SDK, dependencies and trusted verification. Run the required bootstrap,
compiler verification and full verifier. Application composition, default methods,
worker/runtime behavior and resource lifetimes are not implemented by this slice.
