# DEC-01 — Extended primitives and standard-value semantics

**Status: proposed for review; not adopted.**
**Issue:** [#11](https://github.com/gajo357/dsharplang/issues/11)
**Baseline:** main `81f71f7`, compiler through Result propagation.
**Scope:** a proposed value and collection contract, not an implementation.

The maintainer must explicitly adopt a reviewed version before dependent work
implements these choices. Creating or merging this document and passing checks
do not constitute adoption. All normative language in the sections marked
**Proposed** below describes the proposal, not current D# behavior. Examples are
contract fixtures, not claims that today's compiler parses or executes them.

## Sources, settled boundaries and intended slices

The [syntax reference](../DSharp_Syntax_Reference.md) §§2–3 and 8 supplies
illustrative names and APIs; exact extended numeric, standard-value and
collection rules there are provisional. [Implementation status](../DSharp_Implementation_Status.md)
is the source for what currently executes. [DEC-02](DEC-02.md) has been adopted
for grammar; it reserves the listed numeric names without implementing their
meaning. The [sequence](DSharp_Implementation_Sequence.md) places this
decision before collection work [#37](https://github.com/gajo357/dsharplang/issues/37)
and [#38](https://github.com/gajo357/dsharplang/issues/38); extended integers,
floating point/decimal and standard values are separately scoped in
[#48](https://github.com/gajo357/dsharplang/issues/48),
[#49](https://github.com/gajo357/dsharplang/issues/49) and
[#79](https://github.com/gajo357/dsharplang/issues/79). Those slices need not
land together.

Existing rules retained: `int` is signed 32-bit; int arithmetic is checked;
division truncates toward zero and remainder has the dividend's sign. Int
overflow, division or remainder by zero, and minimum-int divided or remaindered
by -1 are execution faults. There are no general implicit mixed-numeric
conversions. Strings and chars use UTF-16 code units with ordinal equality.
Ordinary data is immutable, records/unions are nominal and structurally
compared, and local builders cannot escape. `T?` is idempotently nullable;
`Option<T>` is a separate tagged choice, so `None` and `Some(null)` are
different. No JSON, HTTP, durable serialization or foreign interop contract is
settled here.

The current compiler implements only the existing int/bool/string/char/Unit
subset of these primitive types; aliases and native collections remain future
work. In particular it checks a nonnegative int token before unary minus, so
`-2147483648` is currently rejected. Representability of that value and an
eventual way to spell it are separate from claiming that spelling works today.

## Proposed integer types, literals and arithmetic

| Type | Bits | Inclusive value range |
| --- | ---: | --- |
| `sbyte` | 8 signed | -128 … 127 |
| `byte` | 8 unsigned | 0 … 255 |
| `short` | 16 signed | -32768 … 32767 |
| `ushort` | 16 unsigned | 0 … 65535 |
| `int` | 32 signed | -2147483648 … 2147483647 |
| `uint` | 32 unsigned | 0 … 4294967295 |
| `long` | 64 signed | -9223372036854775808 … 9223372036854775807 |
| `ulong` | 64 unsigned | 0 … 18446744073709551615 |

An integer token is a nonnegative mathematical magnitude, expressed as decimal,
`0x` hexadecimal or `0b` binary digits. An underscore may appear only between
two valid digits of that same token: no leading, trailing, repeated, prefix- or
suffix-adjacent separators. `-` is unary negation, not part of the token. An
unsuffixed token has a contextually expected integer type if its magnitude fits;
without such context it is `int` and must fit `int`. The suffix `L` fixes a
token as `long`; other integer suffixes are deferred. Hex and binary denote
nonnegative magnitudes, never implicit two's-complement bit reinterpretations.
Context does not convert an already typed numeric expression.

To preserve the existing int behavior, the magnitude must fit its selected type
*before* unary minus. Thus `-2147483648` remains rejected; the minimum int is
nevertheless a representable value, e.g. `(-2147483647 - 1)`. The same rule
applies to other signed widths. A special minimum-literal exception, if desired,
requires a separate explicit change and compatibility review. Numeric separators,
nondecimal bases, contextual widths and other integer types are not implemented
by this proposal.

Integer `+`, `-`, `*`, unary `-`, `/` and `%` require matching types and produce
that type. They are checked; negating an unsigned nonzero value or signed
minimum faults, as do overflow and zero divisors. Division truncates toward zero;
remainder satisfies `a = (a / b) * b + a % b` and has the dividend's sign.
The signed minimum / -1 and minimum % -1 both fault, preserving int behavior.
An otherwise well-typed constant expression that overflows faults when executed,
not as an extra compile-time range rule. Comparison operands also need matching
types; no implicit widening of variables or mixed-type arithmetic is introduced.

| Fixture | Proposed result |
| --- | --- |
| `let n: byte = 255` | Valid byte literal. |
| `let n: byte = 256` | Diagnostic: magnitude does not fit byte. |
| `let n: uint = 0xFFFF_FFFF` | Valid value 4294967295. |
| `let n: int = 0xFFFF_FFFF` | Diagnostic, not -1. |
| `let n = 8_000_000_000L` | Valid long literal. |
| `1__0`, `0x_FF`, `10_`, `0b102` | Invalid literal. |
| `-2147483648` / `(-2147483647 - 1)` | First is rejected; second evaluates to minimum int. |
| `-7 / 3`, `-7 % 3` | -2 and -1. |
| minimum `int` / -1 or % -1 | Execution fault. |

## Proposed floating point, decimal and numeric conversion

`float32` denotes IEEE 754 binary32; `float` denotes binary64. A fractional
literal without suffix is `float`; `f` selects `float32`, and `m` selects
`decimal`. In the following source grammar, `digits` is one or more decimal
digits with the between-digits underscore rule, and `exp` is `e` or `E`, an
optional `+` or `-`, then `digits`:

```text
fractional-token = digits "." digits [exp] ["f" | "m"]
                 | digits exp ["f" | "m"]
                 | digits ("f" | "m")
```

Thus `1e3`, `1f` and `1m` are valid; `.5` and `1.` are not. A suffix fixes the type.
Infinity or NaN are not ordinary numeric literal spellings; explicit standard
values and parsing can produce them. Extra suffixes and hexadecimal floating
literals are deferred. A finite token whose magnitude exceeds the chosen type's
finite range is a source diagnostic. Binary-float literals round to nearest,
ties to even; nonzero underflow can round to signed zero. Decimal literals must
be exactly representable after removing trailing fractional zeroes; a value
requiring scale greater than 28 or a coefficient outside 96 bits is diagnosed,
not silently rounded. Unary minus remains a separate operation.

Binary floating arithmetic and comparisons follow IEEE 754 with round-to-nearest,
ties-to-even for each operation. `%` is the truncated-quotient remainder as in
C#, not IEEE's nearest-quotient remainder: `5.0 % 3.0 == 2.0`. A zero divisor or
infinite dividend yields NaN; a finite dividend with an infinite divisor yields
the dividend, and a zero remainder retains the dividend's sign. Division by zero may yield infinities or NaN;
overflow may yield infinity. NaN compares unequal to every value including
itself; all ordered comparisons involving NaN are false. Positive and negative
zero compare equal. These operator rules also apply recursively inside list,
record and union structural equality: object identity or CLR `Equals` must not
turn a contained NaN into an equal value. Equality and hashing used for keys
must form an equivalence relation, so floats and values containing them are
initially excluded as map/set keys, rather than silently giving NaN a different
equality. A separately adopted total floating-key policy could relax this.

`decimal` is a signed decimal value with a 96-bit coefficient and scale 0..28,
matching the representable range and arithmetic of .NET `decimal` on the pinned
backend. It has no NaN or infinity. Numeric equality ignores trailing decimal
zeros and zero sign; `1.0m == 1.00m`. Arithmetic is checked; overflow and
division by zero fault. Inexact arithmetic rounds to the representable result
using nearest, ties to even; it does not define monetary rounding or currency.
Integer and decimal exceptional arithmetic is not changed by floating rules.

Numeric values of different types do not implicitly convert, including
binary-float/decimal mixtures. Proposed explicit conversions are typed operations
returning `Result<T, ValueError>`: integer-to-integer requires the destination
range; decimal/float-to-integer truncates toward zero and checks the destination
range; NaN and infinity fail integer/decimal conversion. Integer-to-float and
float narrowing round to nearest representable value, ties to even; a finite
input outside the destination's finite range fails before rounding rather than
silently producing infinity. Binary-float to binary-float conversion preserves
infinities and produces NaN from NaN, without promising a preserved NaN payload;
binary32 finite values widen exactly to binary64. For a
finite binary-float to decimal conversion, round the exact mathematical binary
value to the nearest representable decimal, ties to even; decimal to binary
float similarly rounds the exact decimal value to the nearest binary value. If
the exact input is outside the destination's finite range, conversion fails
before rounding. These are D# rules and
may require more than a direct CLR cast. No
unchecked cast, implicit precision loss or default value replaces a failure.
`ValueError` is a proposed typed error with `InvalidFormat`, `OutOfRange`, and
`NonFinite` cases; operations select the applicable case and never return an
untyped error string. Exact conversion entry-point names beyond the examples
below are a later library API slice.

Formatting and parsing are invariant, independent of process culture. Integer
formatting is base-10 with optional leading `-`, no grouping and no `+`.
Decimal formatting uses plain decimal digits with `.` where needed, removes
fractional trailing zeroes, and renders every zero as `0`. Floating formatting
uses the shortest significant-digit decimal that round-trips to the same binary
value, choosing the closest candidate and then an even last digit on a tie.
Construct both fixed and normalized scientific spellings from that candidate:
both have at least one digit on each side of `.`, and scientific notation has
one nonzero digit before `.`, lowercase `e`, and an exponent with no leading `+`
or redundant zeroes. Omit trailing fractional zeroes except the required single
digit after `.`. Choose the shorter spelling by character count, choosing fixed
on a tie. Preserve `-0.0` and format positive zero as `0.0`;
special values format as `NaN`, `Infinity`, and `-Infinity`. Float parsing
accepts those spellings and finite input of the form `-? digits ('.' digits)?
([eE] [+-]? digits)?`, with ASCII digits, no underscores and an optional leading
minus but no leading plus. Leading zeroes are accepted; `.1`, `1.`, whitespace,
locale separators and trailing text are rejected. A finite out-of-range input
returns `OutOfRange`; tiny binary values round to the nearest representable
subnormal or signed zero, ties to even. NaN payload bits are not preserved by
text.

Integer parsing accepts `-? digits`, with ASCII digits, no whitespace, base
prefix, separators, suffix or leading plus; a minus sign on unsigned input,
including `-0`, is out of range. Decimal parsing accepts `-? digits ('.' digits)?` with those
same restrictions and no exponent; it requires the exact value to be
representable after trailing fractional zeroes are removed. Leading zeroes are
accepted; signed integer and decimal parsing also accept negative zero. Formatting
canonicalizes them. These parse
grammars are distinct from source literals. Syntax failures return
`Error(ValueError.InvalidFormat)`; representable-range/precision failures return
`Error(ValueError.OutOfRange)`. These are value-format rules, not a wire format
or JSON non-finite-number policy.

| Fixture | Proposed result |
| --- | --- |
| `1.5f`, `1.5`, `1.5m` | `float32`, `float`, `decimal`, respectively. |
| `1.0m == 1.00m` | `true`. |
| `NaN == NaN`, `NaN < 1.0` | Both `false`. |
| `0.0 == -0.0` | `true`; their floating text forms retain the zero sign. |
| `1.0m + 1.0` | Diagnostic: distinct numeric types. |
| parse float `"1,5"` or `" 1.5"` | `Error(InvalidFormat)` regardless of culture. |
| explicit conversion of NaN to `int` | `Error(NonFinite)`. |

## Proposed standard values and minimum APIs

Each type below is immutable, compares by its specified value, and exposes
explicit `parse(text): Result<T, ValueError>` and `format(): string` operations
unless otherwise stated. Bad text yields `InvalidFormat`; a syntactically valid
but out-of-range value yields `OutOfRange`. APIs are first-slice targets, not
existing members. Parse input is exact, with no surrounding whitespace. No
clock, random source, timezone database or external resource is implicit.

| Type | Value representation and canonical format | Minimum construction and operations |
| --- | --- | --- |
| `uuid` | 128 bits; canonical lowercase 8-4-4-4-12 hexadecimal groups. | Parse exactly that shape, allowing upper/lower hex; format lowercase; equality by all bits. No implicit UUID byte order. |
| `Instant` | Signed 100 ns ticks since 1970-01-01T00:00:00Z, range -621355968000000000..2534023007999999999 (UTC years 0001–9999). Format `yyyy-MM-ddTHH:mm:ss.fffffffZ`. | `fromTicks(long): Result<Instant, ValueError>`, `ticks(): long`; parse only the canonical UTC shape, rejecting offsets and leap seconds. |
| `LocalDate` | Proleptic Gregorian year 0001–9999, month and day; format `yyyy-MM-dd`. | `fromParts(int,int,int): Result<LocalDate, ValueError>`, year/month/day accessors and exact parse; validate month lengths and leap years. |
| `LocalTime` | 0..863999999999 ticks of 100 ns after midnight; format `HH:mm:ss.fffffff`. | `fromTicks(long): Result<LocalTime, ValueError>`, `ticks(): long` and exact parse; reject 24:00 and leap seconds. No timezone. |
| `Duration` | Signed 64-bit count of 100 ns elapsed ticks, not months or calendar days. Format signed invariant decimal ticks followed by `tick`. | `fromTicks(long): Duration`, `ticks(): long`, addition, subtraction and negation; overflow faults. Parse the canonical formatted integer and suffix: no plus, leading zeroes or `-0tick`; zero is `0tick`. |
| `Bytes` | Immutable ordered byte sequence; equality compares every byte. Format lowercase hexadecimal, two digits per byte. | `Bytes.from(List<byte>)`, `length(): int`, `get(int): Option<byte>`; strict even-length hexadecimal parse accepts either hex case. Negative or out-of-range indexes yield None; lengths exceeding max `int` fault. Copy mutable foreign input and output; no mutable backing buffer escapes. |

UTC/offset conversions, timezone and DST policy, clocks, scheduling, calendar
periods, UUID generation and byte order, byte literals, UTF-8/base64 conversion,
and richer date arithmetic are later work. A value's format is not a promise of
JSON, HTTP or durable encoding. Interop must separately specify representation
and copies; a CLR `Guid` byte array, mutable array or collection must not silently
become an immutable value. Invalid numeric components or out-of-range ticks in
`fromParts`/`fromTicks` return `Error(ValueError.OutOfRange)`; they do not fault
or silently normalize. Every `long` tick count constructs a Duration.

| Fixture | Proposed result |
| --- | --- |
| parse uuid `"550E8400-E29B-41D4-A716-446655440000"` | Success; format is lowercase. |
| parse uuid with braces or missing hex digits | `Error(InvalidFormat)`. |
| parse Instant `"1970-01-01T00:00:00.0000000Z"` | Tick value 0. |
| parse Instant with `+01:00` or seconds `60` | Error; offsets and leap seconds are not this API. |
| `LocalDate` 2024-02-29 / 2025-02-29 | First valid, second fails. |
| `LocalTime` `24:00:00.0000000` | Error. |
| `Bytes` containing 0 and 255 | Formats `"00ff"`; parsing `"0ff"` fails. |
| `Duration` -1 tick | Formats `"-1tick"`; negating minimum ticks faults. |

## Proposed immutable collections

`List<T>`, `Map<K,V>` and `Set<T>` are immutable values. Each constructor and
update returns an independent value; sharing internal immutable nodes is allowed
when unobservable. Elements must be valid immutable data, including nested
nullable and Option values; builders, resources, closures and arbitrary mutable
foreign objects cannot be smuggled inside. Collection equality recursively uses
D# value equality, never reference identity.

The proposed first slices expose:

| Type | Construction and core operations |
| --- | --- |
| `List<T>` | `[a, b]`, context-typed `[]`; `length(): int`, `append(T): List<T>`, `get(int): Option<T>`, `map`, `filter` and left-to-right `fold`. |
| `Map<K,V>` | `Map.empty<K,V>()`, `set(K,V): Map<K,V>`, `get(K): Option<V>`, `contains(K): bool`, `count(): int`. |
| `Set<T>` | `Set.empty<T>()`, `Set.from(List<T>)`, `add(T): Set<T>`, `contains(T): bool`, `count(): int`. |

`map<U>((T) -> U): List<U>` applies its callback in order;
`filter((T) -> bool): List<T>` keeps matching elements in order; and
`fold<A>(initial: A, step: (A,T) -> A): A` visits left to right. Callback
effects and errors follow the adopted function/Result rules. Empty literals need
an expected element type. A list index outside `0 <= index < length` yields
`None`, including negative indexes. A map missing a key yields `None`; a present
key yields `Some(value)`, even when that value is null. The `T?`/`V?` lookup
examples in syntax reference §8 are provisional and would be superseded by
this proposal on adoption: idempotent nullability cannot distinguish an absent
entry from a present null. There is no implicit Option-to-nullable conversion.

`Map.set` inserts a missing key or replaces its value for an equal key, retaining
the existing key representative. `Set.add` and `Set.from` retain the first equal
representative and collapse duplicates. A map built from pairs, if added later,
must explicitly specify a duplicate policy; the first slice does not silently
choose one. Lists compare by length and ordered elements. Maps compare the same
key/value associations independently of insertion order; sets compare the same
members independently of insertion order. `count`/`length` overflow is a fault,
not truncation. The key eligibility rules below apply at type checking time.

Builders in [#40](https://github.com/gajo357/dsharplang/issues/40) are not
immutable collections: they remain invocation-local, cannot be passed to
ordinary helpers, captured, returned or hidden inside another value, and only
recognized local operations may mutate them. Mutation returns Unit and
`freeze()` creates an independent snapshot; later builder mutation cannot
change that snapshot. Borrowing and ownership transfer remain separate work.

### Proposed keys, equality and deterministic display

Initially eligible keys are bool, integer types, decimal, char, string, Unit,
uuid, Instant, LocalDate, LocalTime, Duration and Bytes; nullable and Option of
an eligible type; and immutable records/unions whose fields/payloads are all
eligible. Eligibility is recursive and rejects any type containing `float32`,
`float`, functions, builders, foreign mutable references or unsupported types;
collections and tuples as keys are deferred. `null` is an eligible key only for
a nullable eligible type. Record and union equality includes nominal type and
compares fields or the active case/payloads structurally. The same type-based
rule applies whether or not a particular value happens to contain NaN.

Equal eligible keys must have equal hashes. Hashes are implementation details:
no fixed hash, cross-process hash stability, or reliance on host randomization
is promised. The map/set algorithm must still use D# equality to resolve hash
collisions. User-supplied comparers and total floating-key equality are deferred.

Formatting is deterministic diagnostic display, not serialization. Lists render
`[element, element]` in list order; maps render `{key: value, key: value}` and
sets render `{element, element}` in the total order below. Primitive and standard
values use their invariant formats; bool is `true`/`false`, Unit is `()`, null is
`null`, Option is `None` or `Some(value)`. Strings use double quotes, chars single
quotes, escaping backslash, the enclosing quote, newline, carriage return and tab
as `\\`, `\"` or `\'`, `\n`, `\r`, `\t`; other C0/C1 control UTF-16 code
units and unpaired surrogate code units use uppercase `\uXXXX`. Valid surrogate
pairs are retained as their two original UTF-16 code units. Records render
`QualifiedType{field: value, ...}` in declared
field order; unions render `QualifiedType.Case` or `QualifiedType.Case(value, ...)`
in payload order. Qualified names are their canonical D# module/type names,
including concrete type arguments for generic types; the standard Result uses
`Result<T, E>.Success(value)` or `Result<T, E>.Error(value)` with the concrete
argument names substituted. User-declared unions use their own qualified names.

For a fixed eligible key type, ordering is total and independent of hash or
insertion: bool false before true; integers, decimal and time values numeric;
char and string ordinal UTF-16; uuid and Bytes lexicographic by unsigned value
bytes; Unit has one value; nullable null before present; Option None before Some;
records lexicographically by declared field order; unions by declaration case
order then payload order. Present/nested values use the corresponding recursive
order. UUID ordering uses the 16 bytes in canonical text hex order, not CLR
`Guid`'s mixed-endian byte-array representation. Bytes with a shared prefix sort
shorter first. No cross-type ordering or implicit
ordering of ineligible types is supplied. Collection iteration order itself is
not promised here; [#39](https://github.com/gajo357/dsharplang/issues/39) must
specify its contract independently. List transformations above remain ordered.

| Fixture | Proposed result |
| --- | --- |
| `[null].get(0)` for `List<int?>` | `Some(null)`; `.get(-1)` and `.get(1)` are `None`. |
| `Map.empty<string,string?>().set("x", null).get("x")` | `Some(null)`; lookup of `"missing"` is `None`. |
| `Map.empty<string,int>().set("x", 1).set("x", 2).get("x")` | `Some(2)`; prior maps are unchanged. |
| `Set.from([2, 2, 1]).count()` | 2; the first equal representative is retained. |
| Same map entries inserted in opposite orders | Equal and identical diagnostic display, regardless of hashes. |
| `Map<float,int>` or a record key containing `float` | Static diagnostic under the initial key policy. |
| `Option<Option<int>>` elements | `None`, `Some(None)` and `Some(Some(42))` stay distinct. |

## Review and implementation gates

Review especially the integer-minimum spelling, IEEE NaN/key exclusion,
decimal conversion and rounding, tick/format ranges, error case names, Option
lookup replacing the older illustrative `T?` API, key eligibility and display
order. These are concrete proposals for the maintainer to accept or change, not
decisions made by this PR. A later implementation issue must map each accepted
rule to generated execution/diagnostic tests, preserve existing supported int and
nullable/Option behavior, and identify any approved revision. Serialization,
interop, calendar/timezone scheduling, collections iteration and broader APIs
remain separate decisions. [#30](https://github.com/gajo357/dsharplang/issues/30)
and [#121](https://github.com/gajo357/dsharplang/issues/121) remain independently
tracked; this record does not itself complete their pending collection coverage.
