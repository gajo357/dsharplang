# DEC-02 — Advanced grammar and symbol resolution

**Status: adopted as proposed on 12 November 2026 by the project maintainer.**
**Issue:** [#12](https://github.com/gajo357/dsharplang/issues/12)
**Baseline inspected:** main `7d487df` (compiler through #24)
**Scope:** grammar, lexical boundaries and static name resolution only.

The project maintainer explicitly adopted this bounded contract as proposed on
12 November 2026. References to “proposed” in the historical sections below
refer to those adopted choices; their fixtures do not claim current compiler
implementation. Implementation remains incremental and scoped to individual
issues. Resolve any inconsistencies before dispatch and record accepted examples,
allowed paths and verification in each dependent issue.

## Sources and current boundary

- [Syntax reference revision 0.3](../DSharp_Syntax_Reference.md), especially
  §§1, 3, 7.3, 9, 18–19 and 21: selected requirements plus explicitly proposed
  spellings. [Implementation status](../DSharp_Implementation_Status.md) and
  [bootstrap](../../COMPILER_BOOTSTRAP.md) describe what is actually executable.
- [P02 sequence](DSharp_Implementation_Sequence.md) places this decision before
  [comments #55](https://github.com/gajo357/dsharplang/issues/55) and
  [generics #30](https://github.com/gajo357/dsharplang/issues/30).
- At adoption, main implemented int/bool/string/char/Unit functions, local bindings,
  same-module calls, arithmetic, comparisons, conditionals and explicit return.
  It accepted whitespace and `//` comments, but did not implement block/doc
  comments, imports, generics, lambdas, function types or extensions. Its
  binding-line handling was narrower than the adopted newline rules below.

Settled constraints retained here: public-only declarations; braces delimit
blocks; no required or accepted semicolon; immutable bindings with no general
reassignment; calls are positional and uncurried; an explicit return exits the
nearest function or lambda. Selected nullable/result/worker rules remain governed
by the syntax reference, not redecided here. No runtime, package-manifest or editor
contract is adopted.

## Proposed function, type and lambda grammar

The following is an intentionally bounded grammar sketch. `identifier`, `type`,
`expression` and `block` use the lexer and precedence rules below. `{ X }` means
zero or more repetitions in this sketch; quoted `"{"` and `"}"` are literal
braces. `?` after a production means optional; it is not the language's postfix
result operator.

```text
function     = ["async"] "fn" identifier [type-parameters]
               "(" [typed-parameter {"," typed-parameter}] ")"
               [":" type] block
parameter    = identifier [":" type]
typed-parameter = identifier ":" type
lambda       = ["async"] lambda-parameters "=>" (expression | block)
lambda-parameters = identifier | "(" [parameter {"," parameter}] ")"
type         = function-type | nullable-type
nullable-type = primary-type ["?"]
primary-type = type-name [type-arguments] | "(" type ")"
             | "(" type "," type {"," type} ")"
type-name    = identifier {"." identifier} | primitive-name
function-type = "(" [type {"," type}] ")" "->" type
extension-method = ["async"] "fn" identifier "(" "self" [":" type]
                   {"," typed-parameter} ")" ":" type block
type-parameters = "<" identifier {"," identifier} ">"
type-arguments  = "<" type {"," type} ">"
```

`self` is contextual as the first receiver parameter of an extension method. Its
type is the `extend` target; an explicit annotation, if present, must be that exact
type. A missing, misplaced or duplicate receiver is a diagnostic. Other parameters
are typed. This initial proposal requires an explicit extension-method result type;
`self` remains an ordinary identifier outside that receiver position.

A type argument is a syntactically complete type from this sketch, including
qualified names, nested type arguments, parenthesized/tuple types, function
types and one nullable suffix; semantic availability and nullable eligibility
are checked separately. `Unit` is a named type, while `()` in an expression is
the Unit value. Tuple types have at least two elements. A repeated nullable
suffix such as `T??` is not a nested nullable type.

Function types associate to the right: `(A) -> (B) -> C` is
`(A) -> ((B) -> C)`. Parenthesized multiple entries are separate arguments;
`((int, int)) -> int` has **one tuple argument**, `(int, int) -> int` has two,
and `() -> string` has none. Tuple syntax/type checking is separately scoped to
[#36](https://github.com/gajo357/dsharplang/issues/36); the examples fix the
proposed distinction, not tuple implementation.

`x => x`, `(x, y) => x + y`, `() => 1` and `(x: int) => x` are lambdas.
Parameters without annotations require a unique expected function type from the
context; absent or conflicting context is a diagnostic at the parameter. Annotated
and inferred parameters may coexist only when all inferred types are supplied by
that context. A lambda block's last expression gives its value, or Unit if no
value; `return` exits that lambda, never an enclosing caller. Capture, async and
lifetime rules remain subject to their separate issues; no new capability is
implied by grammar.

An omitted ordinary-function return type is proposed only for a non-public-contract
implementation whose result can be uniquely inferred from every reachable exit and
whose dependency component is acyclic. In this public-only language, *non-contract*
means an ordinary function not implementing an interface/handler signature; it
does not mean private visibility. Explicit types are required for interface and
handler contracts and every function in a self-recursive or mutually recursive
call-graph component. No inference from callers, cross-component guessing, or
silent widening is permitted. A recursive body without an annotation is a
source diagnostic; if an indirect/function-value call or unresolved dependency
prevents proving the required call graph acyclic, require an explicit result type; explicit signatures do not prove termination. Async inference
requires a separately adopted async contract; no inferred async result is frozen
here. Existing explicit int/bool/string/char/Unit signatures remain valid.

## Proposed generic-call disambiguation

In a type or declaration position `<...>` is parsed as type arguments or type
parameters. In an expression, after a name or member, parse a generic call only
if `<`, a complete nonempty comma-separated list of syntactically valid types,
`>`, and an immediately following `(` occur in the **token stream**. Whitespace
is irrelevant. Otherwise `<` and `>` remain comparison operators. Split adjacent
nested closers into individual `>` tokens in generic type contexts; shift
operators are outside this contract. Parsing does not consult the symbol table:
if the generic-call shape is recognized but the callee/type arguments are invalid,
report a semantic diagnostic rather than reinterpret the source. Parentheses can
make intended comparison grouping explicit. Ambiguous forms that look like a
generic call are therefore deliberately parsed as generic calls; this tradeoff
requires review. No overload ranking, generic constraints, variance, type aliases
or import aliases are introduced.

| Fixture | Proposed outcome |
| --- | --- |
| `identity<int>(42)` | Generic call with one type argument and one value argument. |
| `f<List<int>>(x)` | Generic call; nested `>` closers end separate lists. |
| `a < b` | Comparison. |
| `a < b > c` | Reject unparenthesized chained comparison. |
| `a < b > (c)` | Generic-call shape `a<b>(c)`; diagnose unresolved/invalid generic callee if necessary, without comparison fallback. |
| `let f: (int, int) -> int = (a, b) => a + b` | Two-parameter function type and lambda. |
| `let f: ((int, int)) -> int = pair => 1` | One tuple-parameter type; tuple implementation is separate. |
| `fn recurse(n: int) { recurse(n) }` | Reject omitted recursive return type. |
| `fn recurse(n: int): int { recurse(n) }` | Explicit signature meets this grammar gate; runtime termination is not promised. |
| `let f = value => value` | Reject if no expected type or parameter annotation supplies a unique type. |
| `fn applyIt(f: (int) -> int) { f(1) }` | Require an explicit result type when the indirect call prevents proving the dependency graph acyclic. |

A parser recognizes a parenthesized lambda head only when its matching `)` is
followed by `=>`; otherwise parse grouping, Unit `()`, or tuple syntax in its
own context. In a type context `->` identifies a function type. Reject malformed
parameter lists and trailing tokens at their D# spans rather than silently
reinterpreting them.

## Proposed modules and lookup

A file declares one exact dotted module name. Multiple files declaring the same
name contribute to a single module regardless of file order; paths do not create
implicit module names. Collect declarations from all contributing files before
binding bodies, enabling forward references. All declarations remain public.
Duplicate fully qualified declarations in the same namespace are errors with both
origin paths/spans; no overloads or order-dependent winner are introduced by this
record.

An `import A.B` is file-scoped, explicit, non-transitive and idempotent. It makes
that module's public direct members available unqualified and permits qualified
`A.B.name` access in that file. Repeating the same import does not duplicate a
candidate. It does not make imports of `A.B` visible, discover handlers or register
anything. An unimported external module cannot be accessed by merely spelling a
name; same-module declarations are available across contributing files. Package
loading, aliases and re-exports are separate decisions.

For an unqualified name, proposed precedence is: innermost lexical scope
(parameters/locals, honoring the no-shadow rule of the active slice), current
module, then direct imports. Search stops at the first level with candidates.
Two distinct candidates at that level are an ambiguity, independent of file/import
order; diagnose each origin and use an explicitly qualified name to resolve it.
Unresolved names are diagnostics at the use site. Type declarations occupy the type namespace. Functions and public record
constructors occupy the module value namespace; a type and its constructor may
share spelling across the two namespaces, but a function cannot collide with that
constructor. Union cases have canonical value names scoped to their declaring union:
`InvoiceError.NotFound` and `PaymentError.NotFound` may coexist, while duplicate
case names within one union are errors. Unqualified case names join the value
lookup candidates visible at that scope. Two matching cases, or a function and a
case with the same unqualified name at the same lookup level, are ambiguous rather
than order-selected;
qualify the case as `Union.Case` or the function as `Module.name`. An expected
type does not silently select one ambiguous short name. Extensions use their
separately described member-lookup candidate set; this proposal does not introduce
overloads.

For qualified expression lookup, a visible lexical value named by the first path
segment takes precedence over module qualification. If that shadows the imported
module prefix, diagnose the attempted member access if it cannot resolve; the
program must rename the local or restructure the import/use. Module qualification
otherwise matches the **whole** explicitly imported dotted module name. Importing
`A.B` does not implicitly import `A` or permit other `A` members. Type-position
qualification uses the type/module context rather than a value expression.

| Fixture | Proposed outcome |
| --- | --- |
| Two `module Billing` files, one calls a function defined in the other | One merged module; forward lookup succeeds. |
| Two `Billing` files declare the same function name | Duplicate diagnostic with both origins. |
| One file imports `A` and `B`, both export `format`; use `format(x)` | Ambiguity with both origins; order never chooses a winner. |
| Same imports, use `A.format(x)` | Resolve `A`'s public declaration. |
| Repeat `import A` | Idempotent; does not manufacture ambiguity. |
| `A` imports `B`; a different file imports only `A` and uses `B.name` | Reject: imports are not transitive. |
| A local and current module both define a value name | Lexical lookup wins, subject to applicable no-shadow restrictions. |
| `import A`, then local `A` and expression `A.name` | Local-member lookup wins; diagnose if invalid rather than silently qualifying the module. |
| `import A.B`, then `A.C.name` | Reject: the whole exact imported module prefix must match. |
| `type Invoice` with its constructor, plus `fn Invoice(...)` | Duplicate value-name diagnostic; the type/constructor pair itself is allowed across namespaces. |
| `InvoiceError.NotFound` and `PaymentError.NotFound`, used as bare `NotFound(...)` | Ambiguous; qualify the case with its union. |
| A function and a union case in the current module are both named `NotFound` | Bare use is ambiguous; qualify the intended function or union case. |

## Proposed extensions

Extensions are statically resolved from the receiver's declared type, not from
runtime type, reflection, discovery or a registry. `extend Invoice { fn
label(self): string { ... } }` declares a public extension with an ordinary
`self` receiver typed as `Invoice`; it adds no fields or inherited implementation.
An actual member wins only if applicable to the same name, arity and exact argument
types. If same-name real members exist but none is applicable, continue to extension
lookup. Collect applicable extension declarations from the current module and
direct imports as one set, deduplicate the same declaration,
and select the sole applicable candidate. The initial proposal requires exact
receiver/argument matching; generic applicability or conversion ranking must be
specified by a later adopted contract. Multiple applicable candidates—including one from the current module and one from
an import—are an ambiguity, with origin locations, regardless of import order; no default service
or silent winner is selected. Qualified extension-call spelling and overloads are
not chosen here.

| Fixture | Proposed outcome |
| --- | --- |
| A receiver has a matching real `label` and one visible extension | Resolve the real member. |
| No real member and one applicable directly imported extension | Resolve that extension statically. |
| Two imported modules define applicable `label` for the same receiver | Reject as ambiguous and identify both origins. |
| A same-name real member has incompatible arity, and one extension matches | Resolve the extension; no real member is applicable. |
| One current-module and one imported extension both match | Reject as ambiguous; neither scope silently wins. |
| An extension exists only via a transitive import | Reject as not visible. |

## Proposed precedence and lexical rules

From highest to lowest: member access, call and postfix propagation; prefix `-`,
`!`, `await`; `* / %`; `+ -`; `< <= > >=`; `== !=`; `&&`; `||`; `??`; `|>`.
Binary operators are left-associative except right-associative `??`. Parentheses
group. Unparenthesized repeated **or mixed** comparisons/equalities are rejected;
`(a < b) == true` is explicitly grouped. There is no `?:` ternary. `T?`, `?.`,
`??` and postfix `?` retain the distinct meanings described in the syntax
reference; longest-match lexing distinguishes them. Their type/lifetime behavior
is not adopted here.

Proposed reserved core words are `module import type interface service agent worker
fn async await let return if else match when for in with extend using app true
false null`; the primitive names `bool byte sbyte short ushort int uint long ulong float32
float decimal char string` are globally reserved and cannot be identifiers;
`public private protected internal` are rejected visibility modifiers.
`handle trigger pipeline policy retry bind` are contextual only at their defined
declaration positions; elsewhere they are identifiers. Names such as `concurrency`,
`scope`, `maxAttempts`, `Http`, `Result` and `Unit` remain ordinary or library/type
names as appropriate. `_` is the wildcard only in the pattern positions that
introduce it. This list is a **proposal**; current compiler reservation is broader
than implemented grammar and is not evidence of support.

Whitespace separates tokens. A newline ends a declaration/binding/statement unless
the grammar is inside matching `()`, `[]` or another construct explicitly requiring
continuation, or the preceding token is an operator requiring a right operand.
A leading `.` continues member access; a leading binary operator alone does not
continue the preceding completed expression. Braces delimit blocks; they do not by
themselves make every newline insignificant. Line comments end at the newline and
do not erase that boundary. An `if`/`else` block expression may span lines, and an
operator on the same line as its completed closing delimiter can continue it.
These future rules are not a claim that today's parser already accepts every case.

Strings/chars use the existing UTF-16 model: `\n`, `\r`, `\t`, `\\`, `\"`,
`\'` and `\uXXXX` (exactly four hex digits); a char decodes to one UTF-16 code unit.
Raw/multiline literals and interpolation are outside this decision. `//` starts a
line comment; `/* ... */` is a non-nested block comment. Encountering another
`/*` before its closing `*/`, or reaching EOF without closure, is a diagnostic.
Inside a string/char, comment markers are literal text. A contiguous group of
`///` lines immediately preceding a declaration attaches to that declaration;
an ordinary comment or blank line breaks attachment. An unattached documentation
comment is diagnosed rather than silently attached to a later declaration.
These adopted attachment details are implemented incrementally by #55.

| Fixture | Proposed outcome |
| --- | --- |
| `1 + 2 * 3` | `1 + (2 * 3)`. |
| `let int = 1` / `let policy = 1` | Reject reserved `int`; accept contextual `policy` as an ordinary local. |
| `(a < b) == true` | Valid explicit grouping; types must still match. |
| `a < b == true` | Reject unparenthesized chained comparison/equality. |
| `a ?? b ?? c` | `a ?? (b ?? c)`. |
| `let n = 1 +` followed by newline and `2` | Continue after the operator. |
| `call(` followed by newline-separated arguments and `)` | Continue inside delimiters. |
| `let n = 1` followed by newline and `+ 2` | Do not join the two lines; diagnose invalid following statement. |
| A completed expression followed by newline and `.member` | Continue member access. |
| `/* outer /* nested */` | Reject nested opener; do not infer nested comments. |
| `"/* not a comment */"` | One string literal. |
| `/// docs` immediately followed by `fn f...` | Attach docs to `f`. |
| `/// docs`, blank line, `fn f...` | Diagnose unattached docs; do not attach across the blank line. |

## Review and adoption gates

The maintainer explicitly adopted the reviewed decisions, including generic-call
ambiguity, import lookup and extension-conflict rules, newline continuation and
documentation attachment. If a choice changes, update the decision and its
fixtures before dispatch; do not
silently choose in an implementation PR. Review the exact slice in each issue:
[#55](https://github.com/gajo357/dsharplang/issues/55) comments,
[#30](https://github.com/gajo357/dsharplang/issues/30) generics,
[#33](https://github.com/gajo357/dsharplang/issues/33) function types/lambdas,
[#35](https://github.com/gajo357/dsharplang/issues/35) piping/inference,
[#41](https://github.com/gajo357/dsharplang/issues/41) async,
[#46](https://github.com/gajo357/dsharplang/issues/46) modules/imports and
[#47](https://github.com/gajo357/dsharplang/issues/47) extensions.
This proposal contains no proof of those features, runtime protocols, package
compatibility, editor support or readiness for autonomous implementation.
