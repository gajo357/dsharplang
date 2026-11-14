# Issue 152: C# 15 union lowering

**Base:** `98da3382e370cc373360904d8b470b7f7966f6ae`.

## Scope and dependencies

Evaluate and implement a native C# 15 union representation only if it preserves
the existing D# contract. The merged .NET 11 baseline supplies the C# 15
compiler. Keep source syntax, compiler APIs, existing tests, nullable and Option
semantics, and verification unchanged. Generated CLR shapes are implementation
details; no external interop contract or wire format is adopted.

## Steps and affected interfaces

Inspect current union construction, match lowering, nullable-generic handling
and recursive equality; design distinct per-case payloads for native unions and
handle C#'s extra default state. Add a D# example and feature tests, then change
only compiler lowering needed to preserve behavior. Update implementation status
with precisely tested behavior and record representation tradeoffs.

## Acceptance and verification

Execute generated D# for equal generic payload types, distinct tags, nullable
payloads (`None` versus `Some(null)`), nested options and records, structural
equality, multi-payload cases, matching/guards and evaluation order. Retain
negative diagnostics for nonexhaustive matches and invalid nulls. Inspect
generated C# for actual native unions and run the existing regression suite.
Run `bash .github/ai/bootstrap.sh`, `bash scripts/verify-compiler.sh`, and
`bash .github/ai/verify.sh`, the example CLI check/compile/execution, and record
actual results and any limitations. Keep CI fixes on this PR.

## Representation and evidence

Each generated D# union remains a nominal class whose private storage is a C# 15
union of distinct per-case reference types with typed payloads. Existing D# case
factories, matching accessors and structural equality use that storage. An
unreachable native default fails explicitly rather than becoming a D# variant.
This does not establish CLR interop compatibility or a performance improvement;
tag lookup now uses case-type dispatch and should be benchmarked separately.

The new D# example and generated-code test cover same-type `Success`/`Error`
and user cases, nullable `Option` and outer null, nested cases, multi-field and
single-case unions, structural equality, reserved-name collisions, invalid
default storage and negative diagnostics. Existing tests cover matching guards,
generic nullability, Result propagation and evaluation order. Bootstrap, the CLI
`check` and `compile` commands, and execution of the generated example passed.
Compiler verification passed 18 baseline and 367 feature tests. Documentation
and all 25 automation tests passed. The local isolated verifier could not pull
its pinned Docker image (`Forbidden`); GitHub Actions runs that verifier.
