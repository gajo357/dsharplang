# Issue 38: Immutable D# maps and sets

**Base:** `e29e285b7d8317a6f27390a47b816c25fdfabe5f`.

## Scope and dependencies

Implement the Map and Set slice of the adopted [DEC-01](DEC-01.md), following
merged immutable List support in #37. Expose `Map.empty<K,V>()`, `set`, `get`,
`contains`, `count`, `Set.empty<T>()`, `Set.from(List<T>)`, `add`, `contains`
and `count`. A missing map key returns `None`, while a present nullable null is
`Some(null)`. Updates retain existing equal-key representatives and never expose
mutable backing storage. No builders, sorted collections or wire encoding.

## Steps and affected interfaces

Extend intrinsic type/name resolution, typed factories and instance operations;
enforce the adopted recursive key-eligibility rules without allowing unconstrained
generic substitutions to bypass them. Emit immutable managed storage with D#
structural equality, including order-independent Map/Set equality. A bounded
linear scan with D# structural hashes and equality is acceptable for this initial
implementation and avoids relying on foreign CLR reference equality; document its
cost without promising fixed hash values. Equal keys have equal hashes and Option
tags participate in hashing. Keep existing compiler APIs, tests, dependencies
and verifier intact.
Add actual D# examples and executable feature tests, then update status.

## Acceptance-to-test map

- Construction, duplicate policies, lookup/membership and count: execute D#
  programs covering missing keys, replacements, duplicate set members and
  preservation of the original value.
- Eligible keys and immutability: test structural records/unions, nullable and
  Option keys, insertion-order-independent equality, generic substitutions,
  and rejection of collection/tuple/function or otherwise ineligible keys.
- Nullable and Option values: exercise generic calls, nested options, missing
  versus present null and different option tags through generated C#.
- Verify evaluation order, multi-file lookup, diagnostics and invalid factory or
  method calls. Run `bash .github/ai/bootstrap.sh`,
  `bash scripts/verify-compiler.sh`, `bash .github/ai/verify.sh`, and check,
  compile and execute the D# example. Record actual results and GitHub CI.

## Results and limits

The D# example and generated-code tests cover nullable and Option keys/values,
recursive structural keys, collisions, first-representative retention,
order-independent equality, generic nullable values, diagnostics and multi-file
lookup. The initial storage scans entries linearly; repeated insertion, including
`Set.from`, can take quadratic time. Unconstrained generic key parameters are
rejected until eligibility can be established; generic values are supported.

Bootstrap, documentation checks and compiler verification passed: 18 baseline
and 369 feature tests. The D# example passed CLI check, compile and execution.
All 25 automation tests passed. The local isolated verifier could not download
its pinned Docker image (`Forbidden`); GitHub Actions runs the unchanged verifier.
Issue #30 remains open for its complete nullable-generic acceptance criteria.
