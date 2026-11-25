# Issue #78: typed JSON encoding and decoding

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
