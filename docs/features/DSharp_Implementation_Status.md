# Compiler implementation status

The original executable baseline is recorded in [bootstrap scope](../COMPILER_BOOTSTRAP.md).
The matrix below includes subsequent implemented feature slices.
This status describes tested implementation, not the full syntax draft.

| Feature | Parse/type-check | Generated execution |
| --- | --- | --- |
| One module, parameterless int literal functions | Implemented and tested | Demo.answer returns 42 |
| Immutable function-local int `let` bindings | Inferred/explicit int, earlier-local initializers and literal/local final expressions tested | Chained bindings return 42; independent functions retain distinct values |
| Typed int parameters and same-module calls | Explicit int parameter/return types, positional argument lists, forward and nested call binding | Generated functions accept int arguments and execute calls |
| UTF-16 string and char literals | Literals, basic escapes, parameters/locals/returns and ordinal equality | Unicode and exact UTF-16 code-unit round trips tested |
| Unit and explicit return | Unit values, empty blocks, Unit statements and reachable-result checks | Nested early returns leave the nearest function without evaluating remaining expressions |
| Immutable records | Named construction, public fields, nested records, copy updates, structural equality and source diagnostics | Copies preserve the original; field expressions evaluate in source order |
| Discriminated unions and wrappers | Named cases with zero or more positional immutable payloads; qualified or unambiguous construction | Nominal values and nested record/union payloads compare structurally |
| Exhaustive match and guards | Nested union, bool, Unit and primitive patterns, lexical bindings, wildcard and bool guards; checked coverage | Scrutinee evaluates once; first matching arm and its guard execute in source order |
| Nullable values and direct matching | `T?` and `null` in supported signatures, locals, record fields and union payloads; present-value match narrows to `T` | Present and null values remain distinct for scalar, Unit and reference types |
| Nullable fallback and field access | Lazy, right-associative `??` and null-preserving `?.` for record fields; nullable fields flatten | Fallback and receiver evaluate only as needed; nested chains preserve absence |
| Generic data and functions (partial) | Type parameters, explicit calls, structural inference and nested record/union substitution; open `T?` unsupported | Generic functions and data execute with nominal types and structural data equality |
| Typed if/else and lexical scopes | Bool conditions, matching int/bool branches and block-local bindings | Both branches and nested conditionals execute lazily |
| Bool values and logic | Bool literals, types, int comparisons, int/bool equality, !, &&, || with type checks | Checked calls, precedence and short-circuit evaluation tested |
| Checked int arithmetic | Unary minus, +, -, *, /, %, precedence and parentheses; int-only operands checked | Precedence/signed division execute; overflow and zero division fault at invocation |
| Comments and documentation | Single-line and non-nested block comments; contiguous `///` groups attach to module, function, record and union declarations | Comments preserve source locations and do not change execution |
| Source diagnostics and duplicate functions | Implemented and tested | Rejected before emission |
| CLI check/compile | Implemented, smoke-tested | SDK builds generated library |
| Remaining language syntax | Unsupported, explicit diagnostics | Not implemented |
| Workers/queues/runtime/interop/LSP | Not implemented | Not claimed |

Preserve syntax reference revision 0.3 selected behaviour and the adopted
[DEC-02 grammar contract](implementation/DEC-02.md); freeze other provisional
contracts before implementing them. Worker-call failure representation,
queue lifecycle, durable serialization, middleware, authorization capability
validity, numeric extensions and safe .NET interop remain decisions for later
small milestones. No agent isolation or durability follows from this bootstrap.

## Planned work

Follow the [Codex web implementation sequence](implementation/DSharp_Implementation_Sequence.md)
and [task queue](implementation/DSharp_Task_Queue.md). [Issue #7](https://github.com/gajo357/dsharplang/issues/7)
is implemented: immutable function-local bindings with optional type annotations,
no reassignment or shadowing. A binding begins a statement; its expression can
continue inside delimiters or after an operator.
[Issue #19](https://github.com/gajo357/dsharplang/issues/19) extends initializers and
final expressions with checked int arithmetic and parentheses. Numeric tokens
remain nonnegative literals in 0..2147483647; unary minus is an operator, and
`-2147483648` is still rejected. Binary operators are left-associative with
multiplicative precedence. Operands evaluate left to right. Division truncates
toward zero; remainder follows the dividend sign. Overflow (including the CLR
minimum-int divided/remainder by -1 cases) and division/remainder by zero are
execution faults, even for constant-looking expressions. No arithmetic is folded
at binding time. A completed binding ends at the newline unless its expression
requires continuation inside delimiters or after an operator.
Bindings become visible after their initializer and never cross function boundaries.
Reassignment (DS2005), duplicate locals (DS2006), unknown/self/forward/cross-function
references (DS2007), unsupported local annotations (DS2008) and out-of-range int literals
(DS2004) are rejected with D# source spans. Malformed declarations and unsupported
expressions continue to produce source diagnostics.

See [the example](../../examples/let-bindings.ds) and
[implementation plan and evidence](implementation/issue-7-plan.md).
Also see [the arithmetic example](../../examples/int-arithmetic.ds) and
[arithmetic plan/evidence](implementation/issue-19-plan.md).
[Issue #20](https://github.com/gajo357/dsharplang/issues/20) adds explicit int
parameters and positional calls in the same module, resolved regardless of
declaration order. Parameter names cannot be duplicated, reassigned or redeclared
as locals; unknown functions, wrong arity and unsupported types are rejected.
Calls and arguments evaluate in source order. See [the example](../../examples/function-calls.ds)
and [plan](implementation/issue-20-plan.md).
[Issue #21](https://github.com/gajo357/dsharplang/issues/21) adds `bool` values
and annotations in functions, parameters and locals, int comparisons, int/bool
equality, and `!`, `&&`, `||` with short-circuit evaluation. Operands and call/
return types must match; unparenthesized chained comparisons are rejected. There
is no truthiness. See [the example](../../examples/bool-logic.ds) and
[implementation plan](implementation/issue-21-plan.md).
[Issue #22](https://github.com/gajo357/dsharplang/issues/22) adds value-producing
`if`/`else` expressions with bool conditions, matching branch types, and nested
block-local bindings. Branch names do not escape; redeclaring a visible local
or parameter is rejected, while sibling branches may reuse a name. Only the
selected branch executes. Value-producing conditionals require `else`; Unit-valued conditionals may omit it.
See [the example](../../examples/if-scopes.ds) and
[implementation plan](implementation/issue-22-plan.md).
[Issue #23](https://github.com/gajo357/dsharplang/issues/23) adds immutable
UTF-16 `string` and `char` values, quoted literals and basic escapes. A `char`
is exactly one UTF-16 code unit; malformed literals and unsupported escapes are
rejected. String equality is ordinal. See [the example](../../examples/text-literals.ds)
and [implementation plan](implementation/issue-23-plan.md). Interpolation,
raw/multiline strings and concatenation remain unsupported.
[Issue #24](https://github.com/gajo357/dsharplang/issues/24) adds the distinct
`Unit` value `()`, empty Unit blocks, Unit-valued statements and explicit
`return`. An `if` without `else` is permitted only when its completing branch
is Unit. Returns target the nearest function, including from nested conditionals;
reachable non-Unit paths still require a matching value. Unit never becomes an
implicit `Success()`. See [the example](../../examples/unit-return.ds) and
[implementation plan](implementation/issue-24-plan.md).
[Issue #25](https://github.com/gajo357/dsharplang/issues/25) adds immutable
record declarations, named construction and field access, including nested record
and supported scalar fields. Copy updates preserve unspecified fields and leave
the original unchanged. Equality compares fields structurally, including nested
records and ordinal strings. Missing, duplicate, unknown or incompatible fields
and attempted field reassignment produce source diagnostics. Record documentation
attaches to type declarations. The generated CLR layout and its external equality
API are implementation details; the tested equality contract is D# `==`/`!=`.
See [the example](../../examples/records.ds) and
[implementation plan](implementation/issue-25-plan.md).
[Issue #26](https://github.com/gajo357/dsharplang/issues/26) adds immutable
union cases, positional scalar, Unit, record and union payloads, and distinct
single-case wrapper types. Construction checks payload types and arity; equality
compares the active case and its payloads structurally. Bare case names must be
unambiguous even when an expected type is known; `Union.Case` qualifies a case.
If a case and record constructor share a name, both are ambiguous unqualified;
the current subset has no module-qualified record construction, so that record
constructor needs a different name until module qualification is implemented.
See [the example](../../examples/unions.ds)
and [implementation plan](implementation/issue-26-plan.md).
[Issue #27](https://github.com/gajo357/dsharplang/issues/27) adds match
expressions with nested case patterns, literal patterns, bindings, `_` and bool
guards. Exhaustiveness covers union and bool combinations, including nested
payloads; open primitive domains need an unguarded catch-all. Guarded arms do
not establish coverage, and `_` intentionally covers future cases. The matched
value is evaluated once and the first pattern with a successful guard selects
the result. Result-specific semantics remain later work.
See [the example](../../examples/match.ds) and
[implementation plan](implementation/issue-27-plan.md).
[Issue #28](https://github.com/gajo357/dsharplang/issues/28) adds nullable
values and direct `null`/present matching. A named pattern for `T?` matches only
a present value and binds it as `T`; `_` also covers null. Coverage handles
nested nullable union and record payloads. Direct nullable field access remains
invalid, and comparing with `null` does not narrow. Nullable defaults and Result
remain separate work. See [the example](../../examples/nullable.ds)
and [implementation plan](implementation/issue-28-plan.md).
[Issue #29](https://github.com/gajo357/dsharplang/issues/29) adds lazy,
right-associative `??` and conditional record-field access via `?.`. A conditional
field read returns a nullable value, flattening an already-nullable field; chained
reads preserve absence. Conditional method calls and postfix Result propagation
remain unsupported. See [the example](../../examples/nullable-operators.ds) and
[implementation plan](implementation/issue-29-plan.md).
[Issue #30](https://github.com/gajo357/dsharplang/issues/30) is partially implemented with generic
records, unions and functions, including explicit type arguments and
unambiguous inference through supported argument and result types. Generic call
disambiguation follows DEC-02. Closed nullable type arguments such as
`Box<int?>` are supported. An unconstrained open `T?` and operations that would
synthesize it are diagnosed because this slice does not yet represent that
combination safely for both value and reference types. Generic type parameters
have distinct lexical identities even when their source names overlap other
types. Generic constraints, variance and type aliases remain unsupported. See
[the example](../../examples/generics.ds) and
[implementation plan](implementation/issue-30-plan.md).
[Issue #55](https://github.com/gajo357/dsharplang/issues/55) adds non-nested
block comments and contiguous `///` documentation attached to module and function
declarations. Unterminated or nested block comments and unattached documentation
are diagnosed with source spans. Comments do not change execution; comment markers
inside literals remain text. See [the example](../../examples/comments.ds) and
[implementation plan](implementation/issue-55-plan.md). Newline support is
incremental: declaration-header handling is not yet fully checked against DEC-02;
supported record field access accepts a leading dot.
Additional primitives and runtime features remain unsupported.
Instruction reconciliation in #8 does not add language behavior. Issue creation,
approval labels and planning PRs are not implementation evidence; update the matrix
only when the behavior and relevant generated execution/rejection tests pass.
