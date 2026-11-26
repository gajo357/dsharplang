# Issue #78: typed JSON encoding and decoding

## Twenty-fourth slice: nullable record array elements

Base `572c311fd43669fd6ba9bdfb0ab9256cbdc74e9b` supports record arrays with
required elements and nullable union arrays, but cannot represent an array of
nullable records. Add explicit required or nullable/optional record-array fields
whose elements may be null, plus standalone conversion to and from the existing
heterogeneous JSON tree. Keep null-array, absent-field, empty-array and null-item
states distinct. Copy snapshots, require exact schema identity for non-null
items, and preserve exact values, redacted indexed errors and the combined
64-container limit through nested records and unions.

Update the private record codec and tree adapter; add runtime feature tests for
nested union/tree round trips and standalone mapping (AC1/AC3), presence and
invalid data (AC2), schema identity, snapshot isolation, error redaction and
depth (AC3/AC4). No Option, public D# or durable wire semantics are adopted.
Preserve existing tests, dependencies and trusted verification; run bootstrap,
full verifier, runtime, SQLite and compiler regression, and the exact-base
isolated runtime verifier where available. Record actual results and keep #78 open.

### Twenty-fourth-slice verification

- `bash .github/ai/bootstrap.sh` passed with .NET 11.0.100. Full
  `bash .github/ai/verify.sh` passed documentation (137 files, 309 links) and
  53 automation tests; its isolated compiler stage could not pull the pinned MCR
  image (`Forbidden`). The exact-base isolated runtime verifier restored but
  hit the same image-pull failure. Hosted exact-head checks and the trusted runtime
  deliberate-failure/clean pilot remain necessary.
- `bash scripts/verify-runtime.sh` passed 2 baseline and 218 feature tests,
  including five nullable-record-array tests. `bash scripts/verify-sqlite.sh`
  passed 1 baseline and 52 feature tests.
- `bash scripts/verify-compiler.sh` passed 18 baseline and 406 feature tests
  in a short-path source snapshot; the unchanged TRX guard accepted 511,708 bytes.

## Twenty-third slice: typed union maps in record fields

Base `b24db7577e5768fecc048fc40bed8fc45c0c0dd1` supports maps of records
and scalars but not maps of tagged unions. Add explicit string-keyed maps of one
union descriptor as required or nullable/optional record fields. Copy entries,
require exact descriptor identity and non-null union values, reject invalid
Unicode/duplicate keys, and preserve order, combined depth and redacted indexed
errors. Integrate through nested records and unions and the existing JSON tree
bridge; do not define public D#, Option or durable wire semantics.

Modify the private record codec and add runtime feature tests for nested
union-map/tree round trips (AC1/AC3), missing/null/empty and required distinctions
(AC2), foreign/null values, snapshots and malformed/duplicate keys, redacted
paths and depth limits (AC2–AC4). Preserve existing tests, dependencies and trusted
verification. Run bootstrap, full verifier, runtime, SQLite and compiler regression
and exact-base isolated runtime verification where available. Record actual
results; #78 remains open for public integration and remaining contract decisions.

### Twenty-third-slice verification

- `bash .github/ai/bootstrap.sh` passed with .NET 11.0.100. Full
  `bash .github/ai/verify.sh` passed documentation (137 files, 309 links) and
  53 automation tests; its isolated compiler stage could not pull the pinned MCR
  image (`Forbidden`). The exact-base isolated runtime verifier restored but
  hit the same image-pull failure. Hosted exact-head checks and the trusted runtime
  deliberate-failure/clean pilot remain necessary.
- `bash scripts/verify-runtime.sh` passed 2 baseline and 213 feature tests,
  including five union-map tests. `bash scripts/verify-sqlite.sh` passed
  1 baseline and 52 feature tests.
- `bash scripts/verify-compiler.sh` passed 18 baseline and 406 feature tests
  in a short-path source snapshot; the unchanged TRX guard accepted 511,708 bytes.

## Twenty-second slice: nullable scalar map values

Base `ed531ef6b761a6cd5475b58d5d9f362fc4afc3b8` supports typed scalar map
fields but rejects null values within maps. Add explicit boolean, integer,
string and exact-number maps with nullable values, independently supporting
required, nullable and optional map presence. Preserve distinct absent map,
null map, empty map and null entry states; reuse typed immutable snapshots,
exact numbers, ordinal duplicate and Unicode rejection, redacted indexed errors
and the combined 64-container bound. This extends records, union payloads and
the existing heterogeneous tree bridge without defining Option, public D# or
durable wire semantics.

Update the internal record codec and add runtime feature tests for all four
nullable element kinds and nested union/tree round trips (AC1/AC3), missing,
null, empty and invalid distinctions (AC2), descriptor mismatch, snapshots,
redaction and depth (AC3/AC4). Preserve existing tests, dependencies and trusted
verification. Run bootstrap, full verifier, runtime, SQLite and compiler checks
and exact-base isolated runtime verification where available; record results.
#78 remains open for public integration and remaining contract decisions.

### Twenty-second-slice verification

- `bash .github/ai/bootstrap.sh` passed with .NET 11.0.100. Full
  `bash .github/ai/verify.sh` passed documentation (137 files, 309 links) and
  53 automation tests; its isolated compiler stage could not pull the pinned MCR
  image (`Forbidden`). The exact-base isolated runtime verifier initially hit
  a restore error; a retry of locked restore passed, then the verifier hit the
  same MCR image-pull failure. Hosted exact-head checks and the trusted runtime
  deliberate-failure/clean pilot remain necessary.
- `bash scripts/verify-runtime.sh` passed 2 baseline and 208 feature tests,
  including five nullable scalar-map tests. `bash scripts/verify-sqlite.sh`
  passed 1 baseline and 52 feature tests.
- `bash scripts/verify-compiler.sh` passed 18 baseline and 406 feature tests
  in a short-path source snapshot; the unchanged TRX guard accepted 511,708 bytes.

## Twenty-first slice: typed scalar maps in record fields

Base `9e9db08c55ec9d51e0fbfec7a5d064205160bdd0` supports maps of typed
records but not typed string-keyed maps of primitive values. Add explicit
boolean, integer, string and exact-number map field kinds, plus nullable/optional
map presence, to the existing private record codec. Map values are required
non-null scalars; nullable values and public or durable wire choices remain
outside this slice. Use copied immutable entries and reject duplicate or invalid
Unicode keys, null or wrong-kind values. Preserve insertion order, exact numeric
spelling, descriptor-kind matching and the shared 64-container depth bound.
Encode and decode inside records, nested unions and the existing JSON tree bridge,
with redacted indexed errors.

Modify the private runtime codec and add new runtime feature tests for primitive
map round trips including nested union/tree integration (AC1/AC3), missing,
null, required and invalid values (AC2), snapshots, kind matching, duplicate and
Unicode rejection and redacted paths (AC3/AC4). Preserve existing tests,
dependencies and trusted verification. Run bootstrap, full verifier, runtime,
SQLite and compiler regression and exact-base isolated runtime verifier when
available; record results. #78 remains open for public integration, Option,
collection variants and durable/version contract decisions.

### Twenty-first-slice verification

- `bash .github/ai/bootstrap.sh` passed with .NET 11.0.100. Full
  `bash .github/ai/verify.sh` passed documentation (137 files, 309 links) and
  53 automation tests; its isolated compiler stage could not pull the pinned MCR
  image (`Forbidden`). The exact-base isolated runtime verifier restored but
  hit the same image-pull failure. Hosted exact-head checks and the trusted
  runtime deliberate-failure/clean pilot remain necessary.
- `bash scripts/verify-runtime.sh` passed 2 baseline and 203 features,
  including five new scalar map tests. The initial new Unicode round-trip test
  expected an unescaped character; its expected JSON was corrected to the
  serializer's valid escape and the full runtime suite passed on rerun.
  `bash scripts/verify-sqlite.sh` passed 1 baseline and 52 feature tests.
- `bash scripts/verify-compiler.sh` passed 18 baseline and 406 feature tests
  in a short-path source snapshot; the unchanged TRX guard accepted 511,708 bytes.

## Twentieth slice: standalone typed record-map tree mapping

Base `bb3ed50a75803863512d1d113539c3af4f9371bf` integrates typed record maps
inside records and unions, but the existing tree adapter supports standalone
arrays only. Add explicit bidirectional conversion between a standalone
`JsonRecordMap` and a heterogeneous JSON map using an explicit value schema.
Preserve insertion order, exact numbers, missing/null distinctions, immutable
snapshots, schema identity and the shared 64-container depth bound. Reject
non-map roots, malformed entries and unknown fields with redacted indexed paths;
never infer a schema or invoke application constructors.

Update the private `JsonTypedFieldMapping` adapter and add runtime feature tests
for map/tree round trips including nested unions and records (AC1/AC3), empty,
missing and null cases and invalid values (AC2), schema identity, snapshots,
depth and redacted paths (AC3/AC4). Public D#, Option, logging and durable
format choices remain outside this slice and #78 remains open. Preserve existing
coverage, dependencies and trusted verification. Run bootstrap, full verifier,
runtime, SQLite and compiler regression and exact-base runtime verification where
available, and record results.

### Twentieth-slice verification

- `bash .github/ai/bootstrap.sh` passed with .NET 11.0.100. The full
  `bash .github/ai/verify.sh` passed documentation (137 files, 309 links) and
  all 53 automation tests; its isolated compiler stage could not pull the pinned
  MCR image (`Forbidden`). The exact-base isolated runtime verifier restored
  successfully but hit the same image-pull failure. Hosted exact-head checks and
  the trusted runtime deliberate-failure/clean pilot remain necessary.
- `bash scripts/verify-runtime.sh` passed 2 baseline and 198 feature tests,
  including four new map/tree mapping tests. `bash scripts/verify-sqlite.sh`
  passed 1 baseline and 52 feature tests.
- `bash scripts/verify-compiler.sh` passed 18 baseline and 406 feature tests
  in a short-path source snapshot; the unchanged TRX guard accepted 511,708 bytes.

## Nineteenth slice: typed record maps in record fields

Base `059272b51d819cf42f6c146d306cec5ed7087e37` provides typed record and
union snapshots, heterogeneous JSON trees and arrays of typed records. Add an
explicit string-keyed map of one record descriptor as a required or nullable/
optional record field. Use an immutable copied container, reject invalid Unicode,
duplicate keys, null or foreign records, and preserve descriptor identity and
entry order. Encode and decode through the existing record codec so typed maps
work in nested records, arrays and union payloads. Report untrusted map keys only
as indexes and apply the combined 64-container depth bound.

Update the private record codec and add runtime feature tests for nested map
round trips and exact numbers (AC1/AC3), absent/null/empty and required fields
(AC2), invalid keys/values and duplicate input (AC2/AC3), indexed redaction and
depth boundaries (AC4). Preserve existing tests, projects, dependencies and
trusted verification. This does not adopt public D# or Option semantics, a
logging API or a durable wire contract. Run bootstrap, full verifier, runtime,
SQLite and compiler regressions and exact-base isolated runtime verification
where available; record actual results. #78 remains open.

### Nineteenth-slice verification

- `bash .github/ai/bootstrap.sh` passed with the pinned SDK. Full
  `bash .github/ai/verify.sh` passed documentation (137 files, 309 links) and
  53 automation tests; the isolated compiler stage could not pull the pinned
  MCR image (`Forbidden`). The exact-base isolated runtime verifier hit the
  same image-pull failure. Hosted exact-head checks and the trusted runtime
  deliberate-failure/clean pilot remain necessary.
- `bash scripts/verify-runtime.sh` passed 2 baseline and 194 feature tests,
  including five new record-map tests. `bash scripts/verify-sqlite.sh` passed
  1 baseline and 52 features.
- `bash scripts/verify-compiler.sh` passed 18 baseline and 406 feature tests
  in a short-path snapshot; the unchanged TRX guard accepted 509,272 bytes.

## Eighteenth slice: typed collection/tree mapping

Base `d9f5c89df427df6d7f1ed8b87394b518c9590591` maps individual private
records and tagged unions to and from JSON trees. Extend the explicit mapping to
standalone arrays of records and unions, including nullable union elements.
The reverse mapping requires an exact supplied element descriptor; preserve
order, immutable ownership, exact number spelling and null elements, and prefix
redacted failures with the array index. Reject non-array roots, incompatible
elements and values exceeding the existing 64-container limit. This makes typed
collections usable end to end through the existing tree without inferring a
schema or adopting public D#, Option, logging or durable wire semantics.

Update the private mapping and add runtime feature tests for typed record and
union collection round trips (AC1/AC3), empty arrays, nullable elements, invalid
and unknown content (AC2), indexed redaction (AC4), descriptor identity,
snapshot ownership and depth boundaries. Preserve existing tests, dependencies,
project files and trusted verification. Run bootstrap, full verifier, runtime,
SQLite and compiler checks and the exact-base isolated runtime verifier where
available; record actual results. #78 remains open for its broader acceptance.

### Eighteenth-slice verification

- `bash .github/ai/bootstrap.sh` passed with the pinned SDK. Full
  `bash .github/ai/verify.sh` passed documentation (137 files, 309 links) and
  52 automation tests; the isolated compiler stage could not pull the pinned
  MCR image (`Forbidden`). The exact-base isolated runtime verifier hit the
  same image-pull failure. Hosted exact-head checks and the trusted runtime
  deliberate-failure/clean pilot remain necessary.
- `bash scripts/verify-runtime.sh` passed 2 baseline and 189 feature tests,
  including five collection mapping tests. `bash scripts/verify-sqlite.sh`
  passed 1 baseline and 52 features.
- `bash scripts/verify-compiler.sh` passed 18 baseline and 406 feature tests
  in a short-path snapshot; the unchanged TRX guard accepted 509,272 bytes.

## Seventeenth slice: explicit typed-value tree mapping

Base `f245cc9a3890e45151aadc06d18c7a0663fa6209` has private typed
record/union codecs and heterogeneous immutable JSON trees. Add explicit
internal adapters in both directions between a typed record or tagged union and
a tree, using the existing provisional codec representations and an explicitly
supplied descriptor on the reverse path. This connects those codecs end to end
without reflection, application constructors or an inferred schema. Retain exact
numbers, nullable/missing distinctions, descriptor identity, bounded depth and
redacted typed decode errors. Do not silently map Option or adopt a public or
durable format; the current union shape remains experimental.

Add a private mapping source and runtime feature tests. Cover nested record and
union round trips (AC1/AC3), missing and null fields (AC2), wrong shape, unknown
fields/tags and redaction (AC2/AC4), immutable snapshots and depth boundaries.
Preserve prior tests, SDK, dependencies, project files and trusted verification.
Run bootstrap, full verifier, runtime, SQLite and compiler checks and the
exact-base isolated runtime verifier where available; record results. #78 remains
open for public typed integration, approved Option and durable/version contracts.

### Seventeenth-slice verification

- `bash .github/ai/bootstrap.sh` passed with the pinned SDK. Full
  `bash .github/ai/verify.sh` passed documentation (137 files, 309 links) and
  52 automation tests; the isolated compiler stage could not pull the pinned
  MCR image (`Forbidden`). The exact-base isolated runtime verifier hit the
  same image-pull failure. Hosted exact-head checks and the trusted runtime
  deliberate-failure/clean pilot remain necessary.
- `bash scripts/verify-runtime.sh` passed 2 baseline and 184 feature tests,
  including five typed mapping tests. `bash scripts/verify-sqlite.sh` passed
  1 baseline and 52 features.
- `bash scripts/verify-compiler.sh` passed 18 baseline and 406 feature tests
  in a short-path snapshot; the unchanged TRX guard accepted 509,272 bytes.

## Sixteenth slice: optional nullable tree fields

Base `2384177339bd6903b8afc84254d447a79e615a60` integrates private JSON
value trees with records and unions but requires tree fields to be present. Add
an explicit nullable tree field kind that may also be optional. Preserve three
distinct states: absent, present JSON null, and present non-null tree. As with
other nullable fields, represent present null as CLR null; reject a separate
`JsonFieldValue.Null()` wrapper in this kind so one decoded null has one internal
representation. The existing required `FieldValue` kind remains unchanged.

Update the private record codec and add runtime feature tests for missing/null/
empty/nested values, non-null precision, type mismatch and duplicate/redacted
errors, construction snapshots and combined depth bounds through unions. This
advances #78 AC1–AC4, especially nullable presence (AC2), without adopting
Option, public D# APIs, logging or durable schema semantics. Preserve existing
tests, dependencies, projects and trusted verification. Run bootstrap, full
verifier, runtime, SQLite and compiler regressions and exact-base isolated runtime
verification where available; record actual results. #78 remains open.

### Sixteenth-slice verification

- `bash .github/ai/bootstrap.sh` passed using the pinned SDK. Full
  `bash .github/ai/verify.sh` passed documentation (137 files, 309 links) and
  52 automation tests; the isolated compiler stage could not pull the pinned
  MCR image (`Forbidden`). The exact-base isolated runtime verifier hit the
  same image-pull failure. Hosted exact-head checks and the trusted runtime
  deliberate-failure/clean pilot remain necessary.
- `bash scripts/verify-runtime.sh` passed 2 baseline and 179 feature tests,
  including five new presence/composition tests. `bash scripts/verify-sqlite.sh`
  passed 1 baseline and 52 features.
- `bash scripts/verify-compiler.sh` passed 18 baseline and 406 feature tests in
  a short-path snapshot; the unchanged TRX guard accepted 509,272 bytes.

## Fifteenth slice: compose value trees with records and unions

Base `9ecb11044950c9199943fa01bcb0f322dff7ec92` includes the private
heterogeneous value tree and typed record/union snapshots. Allow an explicit
required record field to hold a tree, including its explicit JSON null, so a
typed record or union payload can carry nested field data. Preserve exact number
tokens, immutable snapshots and redacted indexed paths. Track actual container
depth through records, unions and their arrays so programmatic construction and
decoding enforce the same 64-container limit across both representations.

Modify the existing private record/union and tree codecs; add runtime feature
tests for nested record/union round trips, exact numbers, invalid/duplicate and
redacted paths, null and foreign values, and mixed depth boundaries (partial
AC1–AC4). Preserve prior tests, dependencies, project files and trusted
verification. This does not define optional tree fields, public D# APIs, Option
wire semantics, logging delivery or durable schemas; #78 remains open. Run
bootstrap, full verifier, runtime, SQLite and compiler regressions and the
exact-base isolated runtime verifier where available; record actual results.

### Fifteenth-slice verification

- `bash .github/ai/bootstrap.sh` passed using the pinned SDK. Full
  `bash .github/ai/verify.sh` passed documentation (137 files, 309 links) and
  52 automation tests; the isolated compiler stage could not pull the pinned
  MCR image (`Forbidden`). The exact-base isolated runtime verifier hit the
  same image-pull failure. Hosted exact-head checks and the trusted runtime
  deliberate-failure/clean pilot remain necessary.
- `bash scripts/verify-runtime.sh` passed 2 baseline and 174 feature tests,
  including five composition tests. `bash scripts/verify-sqlite.sh` passed
  1 baseline and 52 features.
- `bash scripts/verify-compiler.sh` passed 18 baseline and 406 feature tests
  in a short-path snapshot; the unchanged TRX guard accepted 509,272 bytes.

## Fourteenth slice: heterogeneous scalar lists and maps

Base `a79ac681b202df0654bb484b09a9a7545561ec2a` contains the internal
record, union, array and exact-number building blocks. Implement a separate
internal immutable JSON value tree for nested heterogeneous lists and
string-keyed maps of null, booleans, strings and exact number tokens. This
advances the logging target's explicitly described field shapes and #78 AC1/AC3;
it does not implement logging, UUID/time conversion, record/union/Option mapping,
a public D# API or a durable schema. Preserve exact numeric spelling, copied
container ownership, valid Unicode and the existing 64-container nesting bound.
Reject duplicate map keys, malformed values and foreign data without invoking
application code; errors use generated indexes rather than untrusted map keys,
and never contain input values (AC2/AC4). Test nested round trips, copies,
malformed and duplicate input, Unicode, redaction, and the depth boundary.

Add runtime source and feature tests in the existing #9 paths, update status,
and preserve dependencies, prior tests and trusted verification. Run bootstrap,
full verifier, runtime, SQLite and compiler regressions and exact-base isolated
runtime verification where available. Record actual evidence before publishing
the draft. #78 remains open for the adopted full codec and integration.

### Fourteenth-slice verification

- Bootstrap passed with the pinned SDK on `PATH`. Full verification passed
  documentation (137 files, 309 links) and all 52 automation tests, then the
  pinned MCR image pull returned `Forbidden` in the isolated compiler stage.
  Exact-base isolated runtime verification reached the same image-pull failure;
  hosted exact-head checks and the trusted runtime failure/clean pilot remain
  necessary. A first invocation of the exact-base harness lacked its locked
  Node dependencies; after installing them the harness reached the image pull.
- Runtime passed 2 baseline and 169 feature tests; SQLite passed 1 baseline
  and 52 feature tests. A sandbox proxy connection failure during the first
  runtime restore was resolved by the managed network retry.
- Compiler passed 18 baseline and 406 feature tests in a short-path snapshot;
  the unchanged report guard accepted 508,460 bytes. An initial invocation
  stopped silently after restore; its ordinary retry passed.

## Thirteenth slice: nullable elements in internal union arrays

Base `f796e4c1df2cf78223dbf782c0360f04f36d84cf` includes #328's
internal union arrays. Extend record fields with required and nullable/optional
arrays whose union elements may be null. Keep a distinct immutable array type
with exact union descriptor identity, copied inputs and no nullable-element
coercion into existing required-element arrays. Preserve missing, null-array,
empty-array, null-element and unit-case distinctions; propagate redacted indexed
errors and retain the combined 64-level bound.

Change `src/DSharp.Runtime/JsonScalarRecordCodec.cs`, add
`tests/runtime-features/JsonNullableUnionArrayTests.cs`, and update status.
Map round trips and nested compositions to AC1/AC3; missing/null/empty and
foreign values to AC2/AC3; indexed redaction and depth boundaries to AC4.
This remains provisional internal work, not an adopted Option, public D# API,
schema evolution policy or durable wire format. #78 remains open. Preserve
existing tests, projects, dependencies and trusted verification. Run bootstrap,
full verifier, runtime, SQLite and compiler regressions and the exact-base
isolated runtime verifier where available; record results in this plan.

### Thirteenth-slice verification

- `bash .github/ai/bootstrap.sh` passed with the pinned SDK on `PATH`.
- `bash .github/ai/verify.sh` passed documentation (137 files, 309 links) and
  all 52 automation tests, then the isolated compiler stage could not pull the
  pinned MCR image (`Forbidden`). The exact-base isolated runtime verifier hit
  the same image-pull failure. Hosted exact-head checks and the trusted runtime
  intentional-failure/clean pilot remain necessary.
- `bash scripts/verify-runtime.sh` passed 2 baseline and 164 feature tests;
  `bash scripts/verify-sqlite.sh` passed 1 baseline and 52 feature tests.
- `bash scripts/verify-compiler.sh` passed 18 baseline and 406 feature tests in
  a short-path snapshot; the unchanged report guard accepted 508,460 bytes.

## Twelfth slice: internal arrays of tagged unions

Base `84503abaa229aca9ece32ed7bafe1b70fc496dc1` contains the internal
record, union and array building blocks from merged #323–#325. Compose an
immutable array of values from one exact union descriptor into required and
nullable/optional record fields. Preserve array ownership, reject null or
foreign elements and descriptor mismatches, retain missing/null/empty
distinctions, redact indexed nested failures, and count array, union and record
containers under the existing 64-level bound.

Change `src/DSharp.Runtime/JsonScalarRecordCodec.cs`, add
`tests/runtime-features/JsonUnionArrayTests.cs`, and update implementation
status. Test unit/payload cases and nested round trips (AC1/AC3), nullable and
missing fields plus malformed/foreign values (AC2/AC3), indexed redacted paths
and depth limits (AC4). This only advances provisional internals: public D#
APIs, nullable union elements, Option, schema versioning and durable format
adoption remain out of scope; #78 remains open. Preserve existing tests,
dependencies, projects and trusted verifier. Run bootstrap, full verifier,
runtime, SQLite and compiler checks and exact-base isolated runtime verifier
where available; record results before publishing the draft.

### Twelfth-slice verification

- `bash .github/ai/bootstrap.sh` passed with the pinned SDK on `PATH`.
- `bash .github/ai/verify.sh` passed documentation (137 files, 309 links) and
  all 47 automation tests, then its isolated compiler stage could not pull the
  pinned MCR image (`Forbidden`). The exact-base isolated runtime verifier hit
  the same image-pull failure. Hosted exact-head checks, including the trusted
  runtime intentional-failure/clean pilot, remain necessary.
- `bash scripts/verify-runtime.sh` passed 2 baseline and 159 feature tests. An
  initial test incorrectly expected `InvalidInteger` for a JSON string; after
  correcting it to `TypeMismatch`, the full runtime suite passed.
- `bash scripts/verify-sqlite.sh` passed 1 baseline and 52 feature tests.
- `bash scripts/verify-compiler.sh` passed 18 baseline and 406 feature tests in
  a short-path snapshot; the unchanged report guard accepted 508,460 bytes.

## Eleventh slice: exact-number arrays in internal records

Base `de1031f0417132f45c022912061f80ca2332b2ae` adds exact-number
tokens and record fields. Extend the existing immutable scalar-array descriptor
to support exact-number elements, nullable elements, and nullable/optional array
fields. Preserve token spelling and snapshot ownership, reject mismatched types
and null required elements, and return redacted indexed paths for invalid input.
The existing record/union nesting and 64-level depth bound remain in force.

Change `JsonScalarRecordCodec.cs` and add runtime feature tests for round trips,
missing/null/empty distinctions, input array mutation, wrong descriptors, indexed
errors and nested records. Update implementation status with tested scope. This
partially advances AC1–AC4; public D# collection APIs, Option, numeric comparison,
schema evolution and durable wire adoption remain outside the slice. No new
dependency, project or verifier changes. Run bootstrap, full verifier, runtime,
SQLite and compiler regressions, and record actual results. #78 stays open.

### Eleventh-slice verification

- `bash .github/ai/bootstrap.sh` passed with the pinned SDK on `PATH`.
- `bash .github/ai/verify.sh` passed documentation (136 files, 309 links) and
  all 46 automation tests, then could not run its isolated compiler verifier:
  the pinned MCR image pull returned `Forbidden`. The exact-base isolated
  runtime verifier reached the same image pull failure. Hosted exact-head checks
  and the trusted runtime deliberate-failure/clean pilot remain required.
- `bash scripts/verify-runtime.sh` passed 2 baseline and 154 feature tests;
  `bash scripts/verify-sqlite.sh` passed 1 baseline and 52 feature tests.
- `bash scripts/verify-compiler.sh` passed 18 baseline and 406 feature tests in
  a short-path snapshot; its unchanged report guard passed (510,084 bytes).

## Tenth slice: lossless internal JSON number tokens

Base `7be57e920e8674c770a0b4e00a5cda1862549a26` contains the provisional
internal record/union codecs. Add a separate immutable exact JSON number token
building block and explicit nullable/non-null internal record field kinds. Parse
with the existing strict JSON parser, retain the valid number spelling without a
binary-floating-point or bounded-decimal conversion, and write it as a JSON
number. Copy input into immutable storage, reject malformed/trailing input,
non-numbers and foreign values, and preserve redacted field errors. This supports
large integer/decimal precision required by the logging target without claiming
numeric comparison, arbitrary D# numeric types or a canonical durable format.

Test large integers and decimal/exponent spellings including trailing zeros and
negative zero; malformed, non-number and Unicode/trailing input; input ownership;
nullable/missing record presence; nested redacted paths; descriptor type safety;
and repeatable round trips. This is partial #78 AC1–AC4 evidence only; public
D# APIs, Option, schema evolution and durable wire adoption remain open. Use
#9's existing runtime source/tests and pinned framework; preserve prior tests,
dependencies, projects and trusted verifiers. Run bootstrap, full verifier,
runtime, SQLite regression, compiler checks and exact-base isolated runtime
verification where available, and record actual results. #78 remains open.

### Tenth-slice verification

- Bootstrap and documentation checks (136 files, 309 local links) passed, as
  did all 46 automation tests. No dependency, project or trusted verifier
  changed.
- Runtime passed 2 baseline and 150 feature tests, including five new exact
  number tests; SQLite passed 1 baseline and 52 feature tests.
- Compiler passed 18 baseline and 406 feature tests from a private short-path
  snapshot; the unchanged report guard passed (506,836-byte feature TRX).
- Full isolated verification and the exact-base isolated runtime verifier could
  not complete locally because the pinned MCR image pull returned `Forbidden`.
  Hosted exact-head checks, including the runtime failure/clean pilot, remain
  required. #78 remains open and no durable numeric format is adopted.

## Ninth slice: compose internal records and tagged unions

Base `e5cea4a10b8cadefe30c0faf904cc0e75cdd4da6` includes the experimental
internal tagged-union codec. Extend internal record fields to hold a union of
one exact union descriptor, including nullable and optional fields, and allow
that record to be an existing union's payload or a record-array element. Both
directions retain immutable snapshot/schema identity and distinguish missing,
null and a unit-like union case. Reject foreign descriptors and malformed,
unknown, duplicate or invalid nested content without exposing input values.
Combine record, array and union depths under the existing 64-level limit.

Add new runtime feature tests for nested union/record composition, case and
payload round trips, nullable presence, schema mismatch, escaped nested/indexed
error paths and redaction, and the exact supported depth boundary. Use #9's
existing runtime paths and pinned framework; preserve all existing tests,
dependencies, projects and trusted verifiers. This is partial #78 AC1–AC4
evidence for provisional internals only, not an adopted public D# union,
Option, schema evolution or durable wire contract; #78 remains open. Run
bootstrap, full verifier, runtime, SQLite regression, compiler checks and the
exact-base isolated runtime verifier where available; record actual evidence.

### Ninth-slice verification

- Bootstrap and documentation checks (136 files, 309 local links) passed, as
  did all 46 automation tests. No dependency, project or trusted verifier
  changed.
- Runtime passed 2 baseline and 145 feature tests, including five new nested
  union composition tests; SQLite passed 1 baseline and 52 feature tests.
- Compiler passed 18 baseline and 406 feature tests from a private short-path
  snapshot; the unchanged report guard passed (506,836-byte feature TRX).
- Full isolated verification and the exact-base isolated runtime verifier could
  not complete locally because the pinned MCR image pull returned `Forbidden`.
  Hosted exact-head checks, including the runtime failure/clean pilot, remain
  required. The union shape is experimental and #78 remains open.

## Eighth slice: provisional internal tagged union codec

Base `da39d198fb1ab0a05e597a3b40c4af9f0911dce5` supplies internal record
snapshots. Add a separate internal tagged-union codec with an explicitly supplied
immutable set of distinct case names and either no payload or an exact internal
record payload descriptor. For this private experiment only, encode an object
with `case` and, for a record case, `value`; require exactly those fields on
decode. Preserve case-sensitive identity, reject unknown/duplicate/missing fields
and unknown tags without echoing input, and prepend the fixed payload path to
record errors. No application constructors, reflection or external converters
run. A union and its record payload together obey the existing 64-level bound.

Test every case and both property orders, null/missing/wrong payloads, unknown
and duplicate names/tags, invalid Unicode and malformed input, snapshot/schema
identity, nested redacted paths, and the exact depth boundary. This is a
provisional internal representation and partial #78 AC1–AC4 evidence: it does
not establish an adopted D# union, Option, public JSON or durable wire contract
or schema evolution policy. Keep #78 open. Preserve previous tests, dependencies,
projects and trusted verifiers; use #9's existing runtime paths. Run bootstrap,
full verification, runtime, SQLite regression, compiler checks and the exact-base
isolated runtime verifier when available, and record actual results.

### Eighth-slice verification

- Bootstrap and documentation checks (136 files, 309 local links) passed, as
  did all 46 automation tests. No dependency, project or trusted verifier
  changed.
- Runtime passed 2 baseline and 140 feature tests, including five new tagged
  union tests; SQLite passed 1 baseline and 52 feature tests.
- Compiler passed 18 baseline and 406 feature tests from a private short-path
  snapshot; the unchanged report guard passed (506,836-byte feature TRX).
- Full isolated verification and the exact-base isolated runtime verifier could
  not complete locally because the pinned MCR image pull returned `Forbidden`.
  Hosted exact-head checks, including the runtime failure/clean pilot, remain
  required. The tagged shape is experimental and #78 remains open.

## Seventh slice: nullable elements in internal scalar array fields

Base `d70abdb08782a2131c98efa9fd0b01d12b92fd05` contains non-null scalar
array fields and nullable array presence. Add separate internal field kinds for
bool?, int32? and string? elements, with independently selectable nullable
array presence. Preserve previous non-null field behavior and immutable tests;
copy typed input arrays and reject mismatched element kinds and invalid Unicode.
No application callbacks, arbitrary CLR values or new dependencies are allowed.

Exercise null versus empty versus missing arrays, null versus non-null elements,
all three types and numeric bounds, owned snapshots, descriptor mismatch, redacted
nested indexed failures, Unicode, and depth limits. This is partial #78 AC1–AC4
evidence only: it does not adopt Option, union, public D# JSON APIs, schema
evolution or a canonical durable representation. Keep #78 open. Use #9's exact
existing runtime paths, pinned framework and trusted checks; preserve previous
tests. Run bootstrap, full verifier, runtime, SQLite and compiler checks plus
the exact-base isolated runtime verifier when available; record actual results.

### Seventh-slice verification

- Bootstrap and documentation checks (136 files, 309 local links) passed, as
  did all 46 automation tests. No dependency, project or trusted verifier
  changed.
- Runtime passed 2 baseline and 135 feature tests, including five new nullable
  element array tests; SQLite passed 1 baseline and 52 feature tests.
- Compiler passed 18 baseline and 406 feature tests in a private short-path
  snapshot; the unchanged report-size guard passed (506,836-byte feature TRX).
- Full isolated verification and the exact-base isolated runtime verifier could
  not complete locally because the pinned MCR image pull returned `Forbidden`.
  Hosted exact-head checks, including the runtime failure/clean pilot, remain
  required. #78 remains open for the full codec and integration criteria.

## Sixth slice: scalar array fields in internal records

Base `0c0924cad9cbfe063fad5efc9f8100998373acc5` contains scalar and record
array building blocks. Extend the existing internal record descriptor to hold
arrays of non-null bool, int32 or string elements, with explicit nullable array
fields and optional missing versus null. Typed array inputs become copied,
read-only snapshots; strings retain the existing UTF-16 validation and foreign
objects or null elements are rejected. Preserve existing tests, paths, pinned
dependencies and trusted verifiers from #9.

Add runtime feature tests covering each kind, nullable presence, empty arrays,
snapshot ownership, wrong types, invalid Unicode, escaped field/index error
paths, redaction, and the JSON depth boundary. Account for each array level in
the 64-level descriptor bound. This is partial #78 AC1–AC4 evidence, not a
public D# codec, nullable elements, union/Option, durable wire contract or
schema-evolution policy; #78 remains open. Run bootstrap, full verification,
runtime, SQLite regression, compiler checks and the exact-base isolated runtime
verifier when available, and record actual results.

### Sixth-slice verification

- Bootstrap and documentation checks (136 files, 309 local links) passed, as
  did all 46 automation tests. No dependency, project or trusted verifier
  changed.
- Runtime passed 2 baseline and 130 feature tests, including five new scalar
  array field tests; SQLite passed 1 baseline and 52 feature tests.
- Compiler passed 18 baseline and 406 feature tests in a private short-path
  snapshot; the unchanged report guard passed (506,836-byte feature TRX).
- Full isolated verification and the exact-base isolated runtime verifier could
  not complete locally because the pinned MCR image pull returned `Forbidden`.
  Hosted exact-head checks, including the runtime failure/clean pilot, remain
  required. #78 stays open for its complete codec and integration criteria.

## Fifth slice: internal arrays of records

Base `fa968f88bf87b867d3d390846e317e274e6577a5` includes nested record
descriptors. Extend the existing internal record codec to describe fields holding
arrays of records of one exact child descriptor, including a nullable array with
optional presence. Elements are non-null independently owned record snapshots;
copy the typed input array and reject descriptor mismatches. No external
constructors, converters or mutable foreign objects run. Preserve all existing
tests and reuse #9's enabled runtime paths and pinned framework without changes
to dependencies, projects or trusted verifiers.

Decode nested arrays with schema-owned, escaped field paths and generated array
indexes. Reject wrong types, null elements, missing, duplicate and unknown child
fields with redacted errors and no partial result. Account for both array and
object levels in the existing 64-level parser bound. Tests cover empty and
multiple arrays, optional missing versus explicit null, array snapshots and
descriptor identity, nested failures and the exact supported depth boundary.
This is partial #78 AC1–AC4 evidence, not a public D# collection/record API,
union or Option representation, schema evolution or canonical durable format.
#78 remains open. Run bootstrap, full verification, runtime, SQLite regression,
compiler checks and the exact-base isolated runtime verifier when available;
record actual evidence and update status only for tested behavior.

### Fifth-slice verification

- Bootstrap and documentation checks (136 files, 309 local links) passed, as
  did 46 automation tests. Dependencies, projects and trusted verifiers did not
  change.
- Runtime passed 2 baseline and 125 feature tests, including five new record
  array tests. SQLite passed 1 baseline and 52 feature tests.
- Compiler passed 18 baseline and 406 feature tests from a private short-path
  snapshot; the unchanged report-size guard passed (506,836-byte feature TRX).
- The full isolated verifier and the exact-base isolated runtime verifier could
  not complete locally: the pinned MCR image pull returned `Forbidden`.
  Hosted exact-head checks and the trusted runtime failure/clean pilot remain
  required. This is a bounded internal slice; #78 remains open.

## Fourth slice: nested internal record descriptors

Base `6411186f94b9e373aed9f90961cb2a5d6074ba9a` includes the provisional
scalar-record descriptor. Continue #78 on its owning branch, extending only the
internal descriptor to contain explicitly described nested records and nullable
nested records. Reuse the existing #9 runtime source/test paths and pinned
net11.0 framework; change no package, manifest or trusted verifier. Preserve
all existing scalar, array and record tests and their policies.

In `src/DSharp.Runtime/JsonScalarRecordCodec.cs`, nested fields carry a specific
immutable child descriptor, and values must be independently owned record
snapshots of that exact descriptor. Nullable optional nested fields retain the
distinction between missing and explicit null. A bounded descriptor depth (64,
matching the existing `JsonDocument` default) prevents unbounded recursion.
Decoding must preserve generated schema-field paths through nested records;
unknown names report the containing object path without echoing input, while
wrong values, duplicate names and missing required fields report the escaped
declared child path. No application constructors, setters or converters execute.

Add `tests/runtime-features/JsonNestedRecordCodecTests.cs` for nested round
trips, optional presence, descriptor mismatch, unsupported inputs/depth,
redacted nested paths, missing/duplicate/unknown fields, wrong types and
malformed JSON. This is partial #78 AC1–AC4 evidence only: it is not a public
D# record API, arbitrary reflection, record-array integration, Option or union
encoding, canonical durable wire contract or schema-evolution policy. #78 stays
open. Run bootstrap, full verification, runtime, SQLite regression and compiler
scripts, plus the exact-base isolated runtime verifier where the pinned image is
available; record actual results and update status only for tested behavior.

### Fourth-slice verification

- Bootstrap and documentation checks (136 files, 309 local links) passed, as
  did all 46 automation tests. No package, project or trusted verifier changed.
- Runtime passed 2 baseline and 120 feature tests, including five new nested
  record tests and all prior scalar, array and record tests. The real SQLite
  regression suite passed 1 baseline and 52 feature tests.
- Compiler passed 18 baseline and 406 feature tests from a private short-path
  snapshot, with the unchanged report guard enforced (506,836-byte feature TRX).
- The full isolated verifier and exact-base isolated runtime verifier could not
  complete locally because the pinned MCR image pull returned `Forbidden`.
  Hosted exact-head checks, including the runtime failure/clean pilot, remain
  required. #78 remains open for full codec and integration acceptance.

## Third slice: explicit internal scalar-record descriptors

Base `47d4b04606ebe28a377c713499df62f09967b480` includes scalar and bounded
array primitives. Continue #78 on its existing branch using #9's already enabled
runtime and feature-test paths and the pinned net11.0 framework. Add an internal
explicit descriptor for records containing only bool, int32, string and their
nullable variants. Encode/decode immutable scalar snapshots without invoking
application constructors, setters, converters or arbitrary object methods.
Reject foreign/mutable objects by exact supported type rather than reflecting
over them; copy input arrays and do not promise a coherent snapshot if callers
mutate those arrays concurrently.

The provisional descriptor uses ordinal case-sensitive declared field names.
Required fields must be present; optional fields must be nullable and preserve
the distinction between an omitted field and an explicit null. Unknown or
duplicate input fields are rejected; unknown names are not echoed. Wrong types,
integer/Unicode failures and missing/duplicate known fields carry a safely
escaped path based only on the declared schema name. Malformed JSON and unknown
names report `$`, without input values or parser text. A successful decode
returns independently owned immutable scalar data, never a partial result.

Add `src/DSharp.Runtime/JsonScalarRecordCodec.cs` and new tests in
`tests/runtime-features/JsonScalarRecordCodecTests.cs`. Extend the internal
error-code vocabulary for these record failures without changing existing
scalar/array behavior or tests. Exercise round trips, supported scalar kinds,
explicit null versus missing, required/duplicate/unknown fields, case and
escaping, redaction, unsupported objects, invalid descriptors and snapshots.
This is a proposed bounded internal schema policy, not an adopted D# record API,
general reflection deserializer, Option or union format, durable canonical wire
contract, schema evolution mechanism, HTTP integration or actor admission.
#78 remains open. Preserve existing tests, dependencies and trusted verifiers.
Run bootstrap, full verification, runtime, SQLite regression and compiler
scripts, plus the exact-base isolated runtime verifier where its pinned image is
available; record actual results.

### Third-slice verification

- Bootstrap and documentation checks (136 files, 309 local links) passed, as
  did all 46 automation tests. No package, project or trusted verifier changed.
- Runtime passed 2 baseline and 115 feature tests, including six new record
  tests and all prior scalar/array tests. The real SQLite regression suite passed
  1 baseline and 52 feature tests.
- Compiler passed 18 baseline and 406 feature tests from a private short-path
  snapshot, with the unchanged report guard enforced (506,836-byte feature TRX).
- The full isolated verifier and exact-base isolated runtime verifier could not
  complete locally because the pinned MCR image pull returned `Forbidden`.
  Hosted exact-head checks, including the runtime failure/clean pilot, remain
  required. #78 remains open for full codec and integration acceptance.

## Second slice: internal scalar arrays and indexed decode errors

Base `2a0a966d4e4ed16a5d8111edd1d41bac7883bcc2` includes #315's internal
scalar codec. Continue #78 on its owning branch. Using the same #9 runtime source
and trusted feature-test paths and pinned net11.0 framework, add internal JSON
array building blocks for bool, int32 and string, nullable scalar elements, and
two-level int32 arrays. Preserve all existing scalar tests and behavior. Accept
only typed CLR arrays for encoding; copy them before writing, reject null arrays
and invalid string elements, and do not promise a coherent snapshot if callers
mutate an input array concurrently. Return read-only, independently owned arrays
from decoding. No caller-supplied constructors or converters run.

Add `src/DSharp.Runtime/JsonScalarArrayCodec.cs` and new tests in
`tests/runtime-features/JsonScalarArrayCodecTests.cs`. Make the existing scalar
element readers reusable internally and allow their fixed error code to carry a
generated numeric-index path. Malformed JSON or a non-array root fails at `$`;
an invalid element fails at `$[index]` or `$[outer][inner]`, with no element
value or parser text in the error. Reject wrong types, nonnullable null,
fractional/out-of-range ints and invalid Unicode; nullable element null and
empty strings remain distinct. Errors never expose partially decoded arrays.

This is a bounded internal representation and partial AC1–AC4 evidence, not an
adopted D# collection API, an Option or record/union encoding, canonical durable
representation, schema/version policy, HTTP integration or actor admission.
#78 remains open. Preserve compiler and verifier protections, manifests and
existing tests; add no package. Run bootstrap, full verification, runtime,
SQLite regression and compiler scripts plus the exact-base isolated runtime
verifier where its pinned image is available; record actual results.

### Second-slice verification

- Bootstrap and documentation checks (136 files, 309 local links) passed, as
  did all 46 automation tests. No project, package or trusted verifier changed.
- Runtime passed 2 baseline and 109 feature tests, including six new array
  tests and all prior scalar tests. The real SQLite regression suite passed
  1 baseline and 52 feature tests.
- Compiler passed 18 baseline and 406 feature tests from a private short-path
  snapshot, with the unchanged report guard enforced (506,836-byte feature TRX).
- The full isolated verifier and exact-base isolated runtime verifier could not
  complete locally because the pinned MCR image pull returned `Forbidden`.
  Hosted exact-head checks, including the runtime failure/clean pilot, remain
  required. #78 remains open for its full codec and integration acceptance.

## First slice: internal JSON scalar primitives

Base `5bbbbbb5f12bf4db9fab214f3fc6ef68379b09c3` contains merged #60. The
issue's recorded dependencies are merged: #9 (SET-02 runtime/test setup), #38
(LANG-21), #28 (LANG-11), #31 (LANG-14) and #14 (DEC-04). #9 enables the exact
existing source path `src/DSharp.Runtime/**/*.cs`, candidate tests under
`tests/runtime-features/**/*.cs`, and the trusted `scripts/verify-runtime.sh`
and base-owned isolated runtime verifier. This bounded slice uses the pinned
`net11.0` SDK's `System.Text.Json` framework assembly, adds no package, manifest,
project, trusted test or verifier change, and does not require optional provider
setup under #10. The setup, paths and dependencies are recorded here before
implementation; #78 remains open for its full acceptance and later integrations.

Add an internal scalar-only codec in `src/DSharp.Runtime/JsonScalarCodec.cs`
and new feature tests in `tests/runtime-features/JsonScalarCodecTests.cs`. The
internal slice encodes and decodes only bool, signed 32-bit integer and string,
plus explicit nullable variants, using ordinary JSON scalars. Encoding rejects
invalid UTF-16 rather than silently replacing it; decoding requires one complete
valid UTF-8 JSON value, the requested scalar type and an in-range integral int.
Failures have a fixed error code and root path `$`, never the input value or the
parser exception text. Null is accepted only by nullable entry points. Returned
values are immutable CLR scalars; no user-supplied converters or constructors
execute. Preserve exact string content and distinguish null from empty string.

These are provisional internal building blocks, not an adopted public D# API,
durable message wire format, canonical semantic comparison or schema/versioning
contract. Records, unions, collections, missing/unknown fields, Option, `Unit`,
other numeric types, arbitrary CLR values, compiler integration, HTTP and actor
admission remain outside this slice. In particular no Option representation is
selected and no JSON object construction or external callbacks are supported.

Acceptance mapping: exercise supported bool/int/string and nullable round trips
including Unicode and int bounds (partial AC1/AC3); reject wrong types, explicit
null for nonnullable targets, invalid/fractional/overflow numbers, malformed or
trailing JSON and invalid Unicode with fixed redacted root-path errors (partial
AC2/AC4); verify no unsupported arbitrary object API and leave AC5 for a reviewed
Option encoding decision. Preserve all existing tests and compiler safety rules.
Run bootstrap, full verification, runtime and compiler scripts, the exact-base
isolated runtime verifier where its pinned image is available, and `git diff
--check`; record actual results and precisely tested status in this PR.

## First-slice verification

- Bootstrap and documentation checks (136 files, 309 local links) passed, as
  did all 46 automation tests. No manifests, dependencies or trusted tests were
  changed.
- Runtime passed 2 baseline and 103 feature tests, including seven new scalar
  codec tests; the real SQLite regression suite passed 1 baseline and 52 features.
- Compiler passed 18 baseline and 406 feature tests from a private short-path
  snapshot, with the unchanged report guard enforced (506,836-byte feature TRX).
- The full isolated verifier and exact-base isolated runtime verifier could not
  complete locally because the pinned MCR image pull returned `Forbidden`.
  Hosted exact-head isolated checks, including the runtime failure/clean pilot,
  remain required. These results do not establish a public D# codec, composite
  encoding or canonical durable message representation; #78 remains open.
