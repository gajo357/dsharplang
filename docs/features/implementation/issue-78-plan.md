# Issue #78: typed JSON encoding and decoding

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
