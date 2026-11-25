# Issue #78: typed JSON encoding and decoding

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
