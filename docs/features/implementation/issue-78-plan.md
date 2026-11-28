# Issue #78: typed JSON encoding and decoding

## Fifty-third slice: namespaced identities across SQLite reopen

Base `8fe3957117b07a66a561feacc9349674d7b5de59` adds the opt-in
namespaced schema identity builder but has no storage/restart evidence for it.
Add a SQLite feature test in which a separate writer process reconstructs one
record shape for two caller namespaces and stores catalog-encoded identity and
payload pairs. After writer exit, independently reconstruct the descriptors and
catalogs, verify each namespace selects its payload, cross-namespace and changed
shape fail before parsing, and corrupt matching payloads yield redacted errors.
Use fixture SQL only; no claim of durable admission, automatic evolution, public
wire format or namespace authority. Preserve all existing tests.

AC1 gains restart evidence for stable explicit identities; AC2/AC4 gain
cross-namespace and changed-shape rejection and redacted malformed-data evidence;
AC3 exercises typed payload round trips. Merged #9, #14, #59, #60 and #372–#376
provide prerequisites. Update tested status and this plan. Run bootstrap, full
verifier, runtime, SQLite, compiler and exact-base isolated SQLite verifier;
use fresh short snapshots and unchanged report guards. Keep #78 open.

Results: the focused separate-process SQLite test passed. Bootstrap passed;
documentation checked 137 files and 309 links, and all 57 automation tests
passed. Full verification restored but the pinned MCR image pull returned
`Forbidden` before the container ran. Runtime passed 2 baseline and 367 feature
tests; SQLite passed 1 baseline and 56 feature tests. Compiler passed 18 baseline
and 406 feature tests in a fresh short snapshot; the unchanged report guard
accepted 510,084 bytes. The exact-base isolated SQLite verifier restored but hit
the same image pull failure before its container ran. Hosted exact-head checks
and the trusted failure/clean pilot remain required.

## Fifty-second slice: explicitly namespaced shape identities

Base `0eec9669c64732b1b35c4812ef3a4eedd84ecc61` fingerprints schema shapes
but leaves each caller to combine its logical namespace and fingerprint. Add an
opt-in internal versioned identity builder that requires nonempty caller-supplied
namespace bytes and combines them with the record or union shape fingerprint
using domain-separated length framing. Return the existing owned opaque identity;
do not alter existing caller-assigned identities, codec behavior, admission,
compatibility policy or public/durable wire contracts. Namespace ownership and
uniqueness remain the caller's responsibility; identical namespace and shape
produce identical identities.

Add runtime feature tests for deterministic independent reconstruction, distinct
namespaces and kinds, changed nested shape, input/output mutation, empty input
and catalog-bound decode mismatch before parsing. AC1 gains reproducible explicit
namespace and shape identity; AC2/AC4 gain fail-closed mismatch evidence. Merged
#9, #14, #59 and #372–#375 provide prerequisites. Update tested status and this
plan. Run bootstrap, full verifier, runtime, SQLite, compiler and exact-base
isolated runtime verification with fresh short snapshots and unchanged guards;
record actual results. Keep #78 open for remaining acceptance.

Results: the three new runtime feature tests passed as part of the full runtime
suite, including the fixed framing vector. Bootstrap passed; documentation
checked 137 files and 309 links, and all 57 automation tests passed. Full
verification restored but the pinned MCR image pull returned `Forbidden` before
the container ran. Runtime passed 2 baseline and 367 feature tests; SQLite
passed 1 baseline and 55 feature tests. Compiler passed 18 baseline and 406
feature tests in a fresh short snapshot; the unchanged report guard accepted
510,084 bytes. The exact-base isolated runtime verifier restored but hit the
same image pull failure before its container ran. Hosted exact-head checks and
the trusted failure/clean pilot remain required.

## Fifty-first slice: opt-in strict schema fingerprints

Base `ef2f12312ad9e8445ebe2e27afb0e02b893bcb71` requires caller-assigned
opaque codec identities, but reusing an identity for a changed descriptor in
another process can silently select a shape-compatible codec. Add an opt-in,
versioned SHA-256 fingerprint of the complete explicit record/union descriptor
graph, including ordered field/case names, kinds, optionality and nested shapes.
Use domain-separated length-prefixed framing and memoize shared descriptor nodes;
the returned bytes may be combined with a caller-owned logical namespace as an
identity. Same-shaped unrelated contracts have the same fingerprint. It detects
strict shape changes, not nominal identity, semantic compatibility, authenticity,
automatic migration, public schema evolution, durable admission or a wire format.
Do not replace caller-assigned identities or alter existing codec behavior.

Add an internal fingerprint helper, runtime feature tests and a separate-process
SQLite fixture that reconstructs descriptors independently and checks persisted
fingerprint/payload selection and changed-shape rejection. Update status and this
plan. AC1 gains reproducible explicit schema identity; AC2/AC4 gain fail-closed
changed-shape and redacted mismatch evidence; AC3 gains nested typed round trips.
Exercise equivalent separately constructed descriptors, scalar/nullable/optional
kinds, nested records/unions and collections, ordering, Unicode/framing,
max-depth/shared subgraphs and a stable golden vector. Merged #9, #14, #59,
#372–#374 supply prerequisites. Run bootstrap, full verifier, runtime, SQLite,
compiler and exact-base isolated runtime and SQLite checks with fresh short
snapshots and unchanged guards. Keep #78 open for remaining acceptance.

Results: all four focused runtime tests and the focused SQLite test passed.
Bootstrap passed; documentation checked 137 files and 309 links, and all 57
automation tests passed. Full verification restored but the pinned MCR image
pull returned `Forbidden` before the container ran. Runtime passed 2 baseline
and 364 feature tests; SQLite passed 1 baseline and 55 feature tests. Compiler
passed 18 baseline and 406 feature tests in a fresh short snapshot; the
unchanged report guard accepted 510,084 bytes. The exact-base isolated runtime
and SQLite verifiers restored but hit the same image pull failure before their
containers ran. Hosted exact-head checks and the trusted failure/clean pilots
remain required.

## Fiftieth slice: encode through the collision-checked contract catalog

Base `28f2621480a1616a8ab47da370663519421db058` resolves explicit metadata
through the codec catalog only on decode. Encoding can still bypass that catalog
with a separately constructed binding for the same metadata and another
shape-compatible descriptor. Add catalog-bound record and union encoding that
resolves the caller-supplied exact identity, checks the value's descriptor and
returns an owned metadata/payload pair. Unknown identity, wrong kind and wrong
descriptor return a redacted contract mismatch before serialization. Snapshot
the lookup bytes. This opt-in boundary does not assign identities, register
actors, infer compatibility, change admission or define a public wire format.

Update the internal contract/catalog and status and add runtime feature tests.
AC1 gains collision-checked identity on encoding as well as decoding; AC3
gains catalog-bound record and union round trips through the reference store;
AC4 gains redacted rejection of unknown/wrong-kind/wrong-descriptor inputs.
Exercise same-shape different descriptors, all rejection paths, input and output
mutation and concurrent encoding. Merged #9, #14, #59 and #373 supply
prerequisites. Run bootstrap, full verifier, runtime, SQLite, compiler and
exact-base isolated runtime checks with fresh short snapshots and unchanged
guards. Keep #78 open for remaining acceptance.

Results: all four focused tests passed. Bootstrap passed; documentation checked
137 files and 309 links, and all 57 automation tests passed. Full verification
restored but the pinned MCR image pull returned `Forbidden` before the container
ran. Runtime passed 2 baseline and 360 feature tests; SQLite passed 1 baseline
and 54 feature tests. Compiler passed 18 baseline and 406 feature tests in a
fresh short snapshot; the unchanged report guard accepted 510,084 bytes. The
exact-base isolated runtime verifier restored but hit the same image pull
failure before its container ran. Hosted exact-head checks and the trusted
failure/clean pilot remain required.

## Forty-ninth slice: fail-closed codec contract catalog

Base `3f36c0145d197176897d3c4fb6aa72e8b5fb1eee` binds one caller-assigned
opaque identity to a record or union codec, but permits two bindings with the
same identity and has no safe multi-contract lookup. Add an explicit immutable
codec-local catalog that rejects duplicate identities, including across record
and union kinds, and resolves a supplied identity to the intended kind before
decoding. Unknown or wrong-kind identities fail with a redacted mismatch before
parsing. Copy all configuration and lookup bytes; permit concurrent readers.
This catalog is opt-in, does not register actor routes or change admission, and
does not assign identities, infer compatibility or establish a public wire format.

Add the catalog and runtime feature tests; update this plan and status. AC1
gains collision-safe dispatch across explicitly supplied contract versions;
AC2/AC4 gain fail-closed, redacted mismatch handling; AC3 covers typed record
and union payloads through the volatile reference. Test duplicate identical
and cross-kind identities, unknown/wrong kind, same-shape distinct versions,
mutation of inputs, malformed payload ordering, and concurrent lookup. Merged
#9, #14, #59 and #372 provide prerequisites. Run bootstrap, full verifier,
runtime, SQLite, compiler and exact-base isolated runtime checks with fresh short
snapshots and unchanged guards. Keep #78 open for remaining acceptance.

Results: all four focused tests passed. Bootstrap passed; documentation checked
137 files and 309 links, and all 57 automation tests passed. Full verification
restored but the pinned MCR image pull returned `Forbidden` before the container
ran. Runtime passed 2 baseline and 356 feature tests; SQLite passed 1 baseline
and 54 feature tests. Compiler passed 18 baseline and 406 feature tests in a
fresh short snapshot; the unchanged report guard accepted 510,084 bytes. The
exact-base isolated runtime verifier restored but hit the same image pull
failure before its container ran. Hosted exact-head checks and the trusted
failure/clean pilot remain required.

## Forty-eighth slice: bind validated JSON to an explicit contract identity

Base `834df31ed1d1cd5635e0264dc3dee9b88d209447` validates record and union
payload bytes, but callers can decode a shape-compatible payload using the
wrong descriptor without checking the opaque contract metadata already stored
with targeted work. Add an internal opt-in binding from an explicitly supplied
nonempty byte identity to one record or union descriptor. Snapshot identity and
payload, require exact identity equality before decoding, and reject a value
whose descriptor is not the bound descriptor before encoding. Expose only
redacted mismatch errors. Callers own globally unique identities and version
assignment; this is exact identity checking, not schema inference, structural
compatibility, canonicalization, migration, public API or an adopted wire format.
Admission and host registry remain with #61/#58; do not change their behavior.

Add an internal contract-bound codec, error code and runtime feature tests;
update this plan and status. AC1 gains explicit stored contract identity binding;
AC2/AC4 gain fail-closed mismatch and redacted malformed-payload evidence;
AC3 gains typed record/union round-trip through the existing reference store.
Exercise same-shape distinct versions, source and result buffer mutations,
descriptor identity, null/empty identities, mismatch before parse and nested
errors. A SQLite feature test uses a separate writer process and direct fixture
inserts to check the supplied identity and payload after reopen, including wrong
identity and corrupt data; it does not claim durable admission. Merged #9, #14,
#59 and prior #78 slices provide prerequisites.
Run bootstrap, full verifier, runtime, SQLite, compiler and exact-base isolated
runtime checks with fresh short snapshots and unchanged guards. Keep #78 open.

Results: four focused runtime tests and the focused separate-process SQLite
restart test passed. Bootstrap passed; documentation checked 137 files and
309 links, and all 57 automation tests passed. Full verification restored but
the pinned MCR image pull returned `Forbidden` before the container ran.
Runtime passed 2 baseline and 352 feature tests; SQLite passed 1 baseline and
54 feature tests. Compiler passed 18 baseline and 406 feature tests in a fresh
short snapshot, with the unchanged guard accepting 510,084 bytes. Exact-base
isolated runtime and SQLite verifiers restored but hit the same image pull
failure before containers ran. Hosted exact-head checks and failure/clean
pilots remain required.

## Forty-seventh slice: finite float maps in typed records

Base `cbbe1626582cab92533ed6491560db804353a71d` supports finite float
record fields and arrays, but not typed scalar maps. Add internal binary32 and
binary64 map descriptors with nullable map and nullable value variants, plus
the existing standalone scalar-map/tree bridge. Snapshot entries, require
unique valid keys and finite values, preserve signed zero, invariant rounding
and optional missing/null distinctions; report invalid input using redacted
indexed paths. No public D# API, Option mapping or durable wire schema is adopted.

Update the scalar-map descriptor, record codec, tree bridge, tests and status.
AC1/AC3 gain typed map round trips with nested union records; AC2/AC4 gain
missing/null and indexed error evidence. Exercise all eight descriptor variants,
boundaries, snapshots, duplicate/invalid keys, non-finite construction, overflow,
wrong types, first-error order and tree mapping. Merged #9, #14, #225, #227,
#369 and #370 provide prerequisites. Run bootstrap, full verifier, runtime,
SQLite, compiler and exact-base isolated runtime checks with fresh short snapshots
and unchanged guards. Keep #78 open for remaining acceptance.

Results: all four focused tests passed. Bootstrap passed; documentation checked
137 files and 309 links, and all 57 automation tests passed. Full verification
restored but the pinned MCR image pull returned `Forbidden` before the container
ran. Runtime passed 2 baseline and 348 feature tests; SQLite passed 1 baseline
and 53 feature tests. Compiler passed 18 baseline and 406 feature tests in a
fresh short snapshot; the unchanged report guard accepted 510,084 bytes. The
exact-base isolated runtime verifier also restored but hit the same image pull
failure before its container ran. Hosted exact-head checks and the trusted
failure/clean pilot remain required.

## Forty-sixth slice: finite float arrays in typed records

Base `266afe5f8109ac440a05ade96e60d66014dec55a` supports finite float
record scalars and separate tree arrays, but typed record array descriptors
cannot hold them. Add explicit binary32/binary64 arrays with nullable array and
nullable element variants. Snapshot and validate input before construction;
retain signed zero, invariant rounding and optional missing/null distinctions.
Return redacted first-invalid indexed paths on decoding. Integrate the existing
standalone scalar-array/tree bridge. No float map descriptor, public D# API,
Option representation or durable wire schema is adopted.

Change the internal scalar-array descriptor, record codec and typed tree bridge;
add runtime feature tests and update status. AC1/AC3 gain typed collection
round trips, including nested union records; AC2/AC4 gain missing/null and
indexed error evidence. Test empty/nullable arrays, boundaries, snapshots,
non-finite construction, overflow, type mismatch, first-error order and tree
mapping. Merged #9, #14, #225, #227, #368 and #369 provide prerequisites.
Run bootstrap, full verifier, runtime, SQLite and compiler checks, and exact-base
isolated runtime verification in fresh short snapshots with unchanged guards.
Keep #78 open for remaining acceptance.

Results: four focused tests passed; the full runtime suite passed the later
optional-presence assertion, with 2 baseline and 344 feature tests. Bootstrap
passed; documentation checked 137 files and 309 links and all 57 automation
tests passed. Full verification restored but the pinned MCR image pull returned
`Forbidden` before the container ran. SQLite passed 1 baseline and 53 feature
tests. Compiler passed 18 baseline and 406 feature tests in a fresh short
snapshot; its unchanged guard accepted 510,084 bytes. The exact-base isolated
runtime verifier restored but encountered the same image pull failure before
the container ran. Hosted exact-head checks and the trusted failure/clean pilot
remain required.

## Forty-fifth slice: finite float fields in typed records

Base `0616c7a50c5ecc86659fa08d481f7c7d83dd050c` round-trips finite float
scalars and arrays through trees, but explicit typed record descriptors cannot
hold binary floats. Add required and nullable binary32/binary64 scalar fields.
Reuse invariant nearest-representable conversion, preserving signed zero and
finite underflow while rejecting overflow and non-finite record construction.
Optional nullable fields retain missing versus explicit null. No float array or
map descriptor, public API, Option representation or durable wire schema is
adopted by this slice.

Change the internal float conversion and typed record codec, add runtime feature
tests and update status. AC1/AC3 gain typed nested field round trips; AC2/AC4
gain missing/null and redacted field-path errors. Exercise extrema, subnormals,
signed zero, rounding, overflow, wrong types, non-finite construction, optional
presence, and a nested union/record. Merged #9, #14, #225, #227 and #367 provide
the prerequisites. Run bootstrap, full verifier, runtime, SQLite and compiler
checks plus exact-base isolated runtime verification using fresh short snapshots
and unchanged guards. Keep #78 open for remaining acceptance.

Results: four focused tests passed, and the full runtime suite passed the later
nested-path assertion: 2 baseline and 340 feature tests. Bootstrap passed;
documentation checked 137 files and 309 links, and all 57 automation tests
passed. Full verification restored successfully after granting its NuGet HTTP
cache write access, but the pinned MCR image pull returned `Forbidden` before
the container ran. SQLite passed 1 baseline and 53 feature tests. Compiler
passed 18 baseline and 406 feature tests in a fresh short snapshot, with the
unchanged guard accepting 510,084 bytes. The exact-base isolated runtime verifier
also restored, then hit the same image pull failure before its container ran.
Exact-head hosted checks and the trusted failure/clean pilot remain required.

## Forty-fourth slice: decode finite float arrays from JSON trees

Base `e1a32dfe0ede13d35475b199718f28694a40fc78` supports finite float
scalar decoding and array encoding but has no typed array decoding. Add internal
conversions for required and nullable binary32/binary64 elements. Preserve
order, bit patterns, nulls and invariant rounding; fail atomically at the first
invalid element with a redacted indexed error. Return a fresh array on each call.

Update `JsonFiniteFloatMapping`, add runtime feature tests and update status.
AC1/AC3 gain collection round trips; AC2/AC4 gain root and indexed failure
coverage. Test empty, nullable and boundary values, signed zero, overflow,
wrong kinds, first-error order and independence of returned arrays, including
a nested tree encode/decode. Merged #9, #14, #225, #227 and #367 supply prerequisites.
No public API, Option mapping, durable wire schema or logging contract is decided.
Run bootstrap, full verifier, runtime, SQLite and compiler checks, plus exact-base
isolated runtime verification in fresh short snapshots with unchanged guards.
Keep #78 open for remaining acceptance.

Results: four focused tests passed. Bootstrap passed; full verification passed
documentation (137 files, 309 links) and all 57 automation tests, then its
trusted host restore failed with NU1900 because the NuGet HTTP cache under
`/home/agent` is read-only. The trusted process omits the writable HTTP-cache
override. Runtime passed 2 baseline and 336 feature tests; SQLite passed 1
baseline and 53 feature tests. Compiler passed 18 baseline and 406 feature tests
in a fresh short snapshot; the unchanged guard accepted 510,084 bytes. An
exact-base isolated runtime verifier also stopped at the same host restore
failure, before its container ran. Hosted exact-head checks and the trusted
runtime failure/clean pilot remain required.

## Forty-third slice: finite binary floats round-trip through JSON trees

Base `1da7e24697ea83177766343bf8df45a38d47626f` encodes finite binary32 and
binary64 values as exact-number tree nodes but cannot recover the typed binary
value. Add explicit internal conversions from number nodes to each binary type.
Use the pinned .NET invariant nearest-representable conversion, preserve signed
zero, permit finite underflow and reject overflow to infinity. Non-number nodes
and out-of-range values yield structured, redacted errors. This internal choice
does not determine source-literal range checks, exact decimal conversion, a
public D# API, Option encoding, durable wire identity or logger delivery.

Change the internal finite-float adapter, add runtime feature tests, and update
this plan and status. Test bit-exact round trips over boundaries and deterministic
finite patterns, midpoint rounding, overflow/underflow, wrong types, and a nested
record/union tree with a test-owned reference handoff. AC1/AC3 gain a bidirectional
primitive path; AC2/AC4 cover redacted failures. Merged #9, #14, #225, #227 and
earlier #78 slices provide prerequisites. Run bootstrap, full verifier, runtime,
SQLite, compiler and exact-base isolated runtime checks using fresh short
snapshots and unchanged guards. Keep #78 open for its remaining acceptance.

Results: four focused tests passed, including 4,096 deterministic bit patterns
per type (excluding non-finite values), boundary values, nearest-even midpoints,
negative underflow, invalid kinds and overflow. Bootstrap passed; documentation
checked 137 files and 309 links, and all 57 automation tests passed. The full
verifier then hit `Forbidden` pulling the pinned MCR image. Runtime passed 2
baseline and 332 feature tests; SQLite passed 1 baseline and 53 feature tests.
Compiler passed 18 baseline and 406 feature tests in a fresh short snapshot,
with the unchanged report guard accepting 508,460 bytes. The exact-base isolated
runtime verifier restored, but the same MCR pull returned `Forbidden` before its
container ran. Exact-head hosted checks and the trusted failure/clean pilot
remain required.

## Forty-second slice: encode finite float arrays into JSON trees

Base `60909fba5e790c37ba7a8966776c404954ae847e` encodes finite IEEE
scalars into JSON trees but has no corresponding array adapter. Add internal
one-way adapters for binary32 and binary64 arrays, including nullable elements.
Snapshot the input, preserve order and nulls, and return the first non-finite
element as a structured indexed error without producing a partial tree.
Existing exact numeric tokens stay independent of binary floating point.

Update the internal adapter, add runtime feature tests and update this plan and
status. Test boundaries, signed zero, snapshots, nulls, indexed failures and
nested record/union tree handoff. AC1/AC3 gain collection encoding; AC2/AC4
gain redacted failure coverage. Merged #9, #14, #225, #227 and earlier #78
slices provide prerequisites. Run bootstrap, full verifier, runtime, SQLite,
compiler and exact-base isolated runtime checks with fresh short snapshots and
unchanged guards. No float decoding policy, public API, logging implementation,
Option encoding or durable wire format is adopted. Keep #78 open.

Results: four focused tests passed. Bootstrap passed; documentation checked 137
files and 309 links, and all 57 automation tests passed. The full verifier then
hit `Forbidden` pulling the pinned MCR image. Runtime passed 2 baseline and 328
feature tests; SQLite passed 1 baseline and 53 feature tests. Compiler passed 18
baseline and 406 feature tests in a fresh short snapshot, and the unchanged
report guard accepted 508,460 bytes. The exact-base isolated runtime verifier
restored but the pinned MCR image pull returned `Forbidden` before its container
ran. Exact-head hosted checks and the trusted failure/clean pilot remain needed.

## Forty-first slice: encode finite binary floats into JSON trees

Base `9f3ac35d3ce583bf2cbc2220da3dcedc10778031` has exact-number trees but
no explicit path from supported `float32`/`float` values into them. Merged
#225/#227 implement those IEEE types, and the logging target explicitly rejects
non-finite values. Add an internal one-way adapter from finite binary32/binary64
values to exact-number tree nodes, using the pinned JSON writer's round-trip
formatting. Return a structured root error for NaN and infinities; never turn
them into strings or null. Existing decimal and arbitrary numeric tokens remain
separate and never pass through binary floating point. No public D# JSON API,
floating JSON decoding policy, logging implementation, Option encoding or
durable wire format is adopted by this bounded slice.

Change the internal error enum and add an internal finite-float tree adapter and
runtime feature tests; update this plan/status. Exercise binary32/binary64
boundaries, subnormal values, signed zeros, deterministic finite bit patterns,
non-finite rejection and nested record/union/tree with a test-owned volatile
handoff. AC1/AC3 gain an explicit supported primitive encoding path; AC2/AC4
cover redacted structured non-finite failures. Merged #9, #14, #225, #227 and
prior #78 slices supply setup and numeric semantics. Run bootstrap, full verifier,
runtime, SQLite, compiler and exact-base isolated runtime checks with fresh
short snapshots and unchanged report guards. Keep #78 open.

Results: all four focused tests passed, including 4,096 deterministic binary
bit patterns per type (non-finite patterns excluded), signed zero, extremes and
subnormals. Bootstrap passed. The full verifier passed documentation (137 files,
309 links) and all 57 automation tests, then the pinned MCR pull returned
`Forbidden`. Runtime passed 2 baseline and 324 feature tests; SQLite passed 1
baseline and 53 feature tests. Compiler passed 18 baseline and 406 feature tests
in a fresh short source snapshot with the unchanged guard accepting 508,460 bytes.
The exact-base isolated runtime verifier restored successfully but the same pinned
MCR pull returned `Forbidden` before its container ran. Hosted exact-head checks
and the trusted runtime failure/clean pilot remain required.

## Fortieth slice: avoid redundant large decimal token copies

Base `e001d75557541747abcdec2a955920838aef87ab` validates exact decimal
representability without binary floating point, but constructs an intermediate
string containing every mantissa digit before trimming. A local probe of the
private conversion on a 100,010-character valid token allocated 203,360 bytes
after the input string already existed. Replace those intermediate proportional
copies with a bounded significant-digit scan while retaining exact zero,
exponent, precision, overflow and redacted-error semantics. The JSON parser and
its raw-token string still allocate proportionally to the supplied input; this
is not a claim of bounded total memory or a new input-size policy.

Change only the internal decimal codec, add runtime feature tests and update
this plan/status. Exercise long leading/trailing zero runs, giant exponents,
decimal boundaries, unrepresentable precision, nested union/array and tree
composition, plus an independent allocation probe of the conversion itself.
Map exact value and nested round trips to AC1/AC3 and invalid/redacted paths to
AC2/AC4. Merged #9, #14, #25, #28, #31, #38, #216 and prior #78 slices provide
the setup, primitive semantics and boundary. Preserve existing tests, checks,
dependencies and report guards. Run bootstrap, full verifier, runtime, SQLite,
compiler and exact-base isolated runtime checks. No public D# API, Option,
durable admission or wire policy is adopted; #78 remains open.

Results: four focused tests passed, including 500 deterministic comparisons
with independently constructed decimal values and nested redacted failures.
The same local private-conversion probe measured 3,304 allocated bytes after
the 100,010-character input string existed, versus 203,360 before the change;
the test enforces an 8,192-byte bound on that conversion only. Bootstrap passed.
The full verifier passed documentation (137 files/309 links) and all 57
automation tests, then the pinned MCR image pull returned `Forbidden`. Runtime
passed 2 baseline and 320 feature tests; SQLite passed 1 baseline and 53 feature
tests. Compiler passed 18 baseline and 406 feature tests in a fresh short source
snapshot, with the unchanged report guard accepting 508,460 bytes. The exact-base
isolated runtime verifier restored successfully but the pinned MCR pull returned
`Forbidden` before its container ran. Hosted exact-head checks and the trusted
runtime failure/clean pilot remain required.

## Thirty-ninth slice: standalone typed scalar arrays and JSON trees

Base `9ccbb7567ef11181b5bec3ed583d71f171f8ddf1` bridges typed record/union
arrays and scalar maps to JSON trees, but scalar arrays have no standalone
typed/tree conversion. Add the corresponding explicit internal bridge for all
supported scalar array kinds, including nullable entries and exact full-width
numbers. Reuse record-field codecs and their descriptor validation, preserving
snapshots and indexed redacted first errors. Handle a maximum-depth tree without
trying to wrap it past the existing depth bound, and preserve deterministic first
invalid-element ordering. Test root conversions, nested validated payload and
test-owned reference handoff, null/type/range failures, depth and ownership.

Change `JsonTypedFieldMapping.cs`, add runtime feature tests, and update this
plan/status. Merged #9, #14, #25, #28, #31, #38, #216 and prior #78 slices supply
setup, contracts and existing codecs. This advances AC1/AC3 standalone
collection composition and AC2/AC4 invalid and redacted path behavior. It does
not adopt public D# APIs, Option representation, durable admission or wire
policy. Run bootstrap, full verifier, runtime, SQLite, compiler and exact-base
isolated runtime checks; use fresh unique short snapshots and keep report guards.
Keep #78 open.

Results: all 10 focused cases passed. Bootstrap passed; the full verifier passed
documentation (137 files/309 links) and all 57 automation tests, then the
pinned MCR image pull returned `Forbidden`. Runtime passed 2 baseline and 316
feature tests; SQLite passed 1 baseline and 53 feature tests. Compiler passed
18 baseline and 406 feature tests in a fresh short source snapshot; the unchanged
report guard accepted 508,460 bytes. The exact-base isolated runtime verifier
restored successfully but the same pinned image pull returned `Forbidden` before
its container ran. Hosted exact-head checks and the trusted runtime failure/clean
pilot remain required.

## Thirty-eighth slice: full-width integer collections

Base `85730de09a8bd2aef5a1851ee02b1771818ccd3a` supports signed and unsigned
64-bit scalar fields and exact-number tokens, but typed arrays and string-keyed
maps cannot carry these primitive values. Extend existing scalar array and map
descriptors for `long` and `ulong`, nullable elements, and independently
optional or nullable collections, using the existing exact integer readers.
Extend the standalone scalar-map/tree bridge. Round-trip full-width boundary
values without floating point through a nested validated union, explicit tree
mapping and a test-owned volatile reference handoff. Check input snapshots,
missing/null/empty distinctions, schema identity, redacted indexed failures
for negative unsigned, overflow, fractional and wrong-type values, and combined
depth limits. Preserve existing tests and security boundaries.

Change internal scalar-record/tree-mapping sources, add runtime feature tests,
and update plan/status. Merged #9, #14, #25, #28, #31, #38 and #216 provide
setup, boundaries and supported types. AC1/AC3 gain full-width nested typed
collections; AC2/AC4 cover invalid and nullable cases. No public D# API, Option
encoding, durable admission or wire policy is adopted. Run bootstrap, full
verifier, runtime, SQLite, compiler and exact-base isolated runtime checks;
use fresh unique short snapshots and retain all report guards. Keep #78 open.

Results: the 13 focused cases passed. Bootstrap passed; the full verifier passed
documentation (137 files/309 links) and all 56 automation tests before the
pinned MCR image pull returned `Forbidden`. Runtime passed 2 baseline and 306
feature tests; SQLite passed 1 baseline and 53 feature tests. Compiler regression
in a fresh short candidate snapshot produced 18 passing baseline and 406 passing
feature results; the unchanged checker accepted the 508,460-byte feature TRX.
The command session was lost while the feature run was active, so the original
shell's final exit was not observed; the run completed and its report was
checked independently with the same unchanged `check-test-results.mjs` guard.
The exact-base isolated runtime verifier restored successfully but the same
pinned MCR image pull returned `Forbidden` before the container ran. Hosted
exact-head checks and the trusted runtime failure/clean pilot remain required.

## Thirty-seventh slice: reject malformed UTF-8 in nested unions

Base `83b38396bdee83a230e3155c7f3a46f7b51a1d79` composes typed unions by
converting nested `JsonElement` values to raw text and reparsing. A direct
System.Text.Json probe shows that `GetRawText()` throws `InvalidOperationException`
for invalid UTF-8 inside an object; the current nested union paths do not catch it.
This can escape the promised structured, redacted decode errors. Reuse the
already parsed elements when reading a union payload and unions inside record
fields, arrays and maps, so existing scalar and property-name validation reports
invalid Unicode with the correct schema/index path. Preserve existing schema
identity, depth limits, duplicate handling and successful encodings; do not
introduce replacement decoding, a public API or a wire contract.

Change the internal record/union codec readers, add a new runtime feature test
covering malformed UTF-8 in nested values and keys, union fields, arrays and maps,
and update this plan/status. Merged #9, #14, #25, #28, #31, #38 and earlier #78
slices provide setup and descriptor behavior. AC2/AC4 map malformed bytes to
structured, path-aware, redacted failures; AC3 retains nested round trips and
depth behavior through the existing regression suite. Run bootstrap, full
verification, runtime, SQLite, compiler and exact-base isolated runtime checks,
preserving all trusted checks and the report guard. Keep #78 open.

Results: before the change, all seven new focused cases failed with uncaught
`InvalidOperationException` from `GetRawText()`; after it, all seven passed
with redacted paths. `bash .github/ai/bootstrap.sh` passed. The full
`bash .github/ai/verify.sh` passed documentation (137 files, 309 links) and all
56 automation tests, then could not pull the pinned MCR image (`Forbidden`).
`bash scripts/verify-runtime.sh` passed 2 baseline and 293 feature tests;
`bash scripts/verify-sqlite.sh` passed 1 baseline and 53 feature tests.
`bash scripts/verify-compiler.sh` passed 18 baseline and 406 feature tests in
a fresh very short source snapshot with the unchanged report guard accepting
508,460 bytes. The exact-base isolated runtime verifier restored successfully,
then the same pinned MCR image pull returned `Forbidden` before its container
ran. Hosted exact-head checks and their trusted runtime pilot remain required.

## Thirty-sixth slice: verified JSON bytes through SQLite reopen

Base `29a288a883c486d1f2173e505f8f29d86f4e3889` has internal typed JSON
snapshots and independent SQLite pinned-work storage. Verify their composition
end to end using a test-owned bridge: validate nested typed union JSON, submit
it through the volatile reference model, write its captured bytes and explicitly
test-owned contract metadata into real SQLite, exit the writer process, reopen
and decode the stored bytes with the original descriptors. Check exact large
numbers, decimal values, null/missing fields, copied inputs and redacted failure
before the test bridge writes anything. Also verify that corrupt stored bytes
fail validation on read. SQLite retains opaque bytes and must not become a JSON
admission or version policy; these tests do not claim public D# or durable actor
admission, crash recovery, schema evolution or an adopted wire format.

Change only a new SQLite feature test and this plan/status. Merged #9, #10,
#14, #60 and the earlier #78 slices supply test setup, storage, and codecs.
AC1/AC3 gain real provider reopen evidence for nested eligible values; AC2/AC4
cover presence, invalid and corrupt data with redacted errors. Option's wire
representation and actual durable contract/version negotiation remain open.
Run bootstrap, full verifier, runtime, SQLite, compiler, and exact-base isolated
runtime verification; use fresh unique snapshots, preserve report guards, and
record all results. Keep #78 open.

Results: bootstrap passed; the full verifier passed documentation (137 files,
309 links) and all 56 automation tests before its pinned MCR image pull returned
Forbidden. Runtime passed 2 baseline and 286 feature tests, and SQLite passed 1
baseline and 53 feature tests including this new process-exit/reopen scenario.
The compiler check passed 18 baseline and 406 features in a fresh short snapshot;
the unchanged report guard accepted the 508,460-byte feature TRX. An earlier
longer snapshot ran all 406 successfully but its 514,144-byte report failed that
guard, so it was not counted as a passing verifier. A fresh exact-base isolated
SQLite verifier restored successfully but the same pinned MCR pull returned
Forbidden before the container ran. Hosted exact-head checks remain required.
The first sandboxed SQLite attempt could not open the test runner's loopback
socket; managed network permission enabled the successful retry. A runtime
restore also needed a fresh writable NuGet HTTP cache before passing.

## Thirty-fifth slice: exact decimal collections through typed records

Base `ade292d8fb086e1d4f736e4b6c6f4c7f7ab93756` supports exact typed decimal
scalars, but not decimal list or string-keyed map fields. Extend the existing
immutable scalar-array and scalar-map descriptors to decimal values, including
nullable elements independently of nullable or optional collection presence.
Reuse the exact decimal reader so unrepresentable precision and overflow fail
at redacted indexed paths; do not round or use binary floating point. Extend the
existing standalone scalar-map/tree bridge using the same explicit descriptors.
Typed decimal encoding follows .NET value/scale; original arbitrary number
spelling remains the responsibility of the exact-number token type.

Change the internal scalar-record and tree-mapping sources, add runtime feature
tests, and update this plan and status. Merged #9, #14, #25, #28, #31, #38 and
#216 supply setup, contract boundaries and tested types. AC1/AC3: round-trip
decimal arrays and maps through nested union/validated-record/tree and a
test-owned volatile handoff. AC2/AC4: distinguish missing, null, empty and null
entries, reject type/range/precision errors with redacted indexed paths, and
test descriptor identity and copied inputs. Preserve existing tests, pinned
dependencies and trusted verification. Run bootstrap, full verifier, runtime,
SQLite, compiler and exact-base isolated runtime verification; record results.
No public D# API, Option policy, durable admission or versioned wire contract is
adopted; #78 remains open.

### Thirty-fifth-slice verification

- `bash .github/ai/bootstrap.sh` passed with .NET 11.0.100. The full verifier
  rerun passed documentation (137 files, 309 links) and all 56 automation tests,
  then the pinned MCR image pull failed `Forbidden`. Its first run hit the
  previously observed intermittent application build failure without diagnostics;
  automation passed on rerun. The exact-base isolated runtime verifier restored
  and hit the same MCR image-pull failure. Hosted exact-head checks and the real
  intentional-failure/clean runtime pilot remain required.
- `bash scripts/verify-runtime.sh` passed 2 baseline and 286 feature cases;
  `bash scripts/verify-sqlite.sh` passed 1 baseline and 52 feature cases.
- `bash scripts/verify-compiler.sh` passed 18 baseline and 406 feature tests in
  a fresh unique short source snapshot; the unchanged TRX guard accepted
  508,460 bytes.

## Thirty-fourth slice: exact typed decimal JSON values

Base `781e60bab227e523c298d049416e77268b0cca09` preserves arbitrary JSON
number tokens but cannot decode a typed D# `decimal` value. Merged #216 supplies
checked D# decimal arithmetic and exact literal semantics. Add internal decimal
scalar and nullable record-field codecs using an exact bounded coefficient and
scale check, never binary floating point or silent rounding. Permit exponent and
trailing-zero forms only when their value is exactly representable by .NET
decimal; reject overflows and unrepresentable precision with a structured error.
Typed decimal encoding follows .NET decimal value/scale; callers needing original
token spelling continue to use the separate exact-number token type.

Change the internal scalar and record codecs, add runtime feature tests and
update this plan and status. Merged #9, #14, #25, #28, #31, #38 and #216
provide setup, contract boundaries and the tested type. AC1/AC3: round-trip
boundary values, exponent inputs, nullable fields and nested records/unions via
the validated payload. AC2/AC4: reject overflow, fractional precision loss,
wrong types and invalid inputs with redacted paths; test null/missing fields and
descriptor matching. Preserve prior tests, dependencies and trusted verification.
Run bootstrap, full verifier, runtime, SQLite, compiler and exact-base isolated
runtime verification; record results. Public D# JSON APIs, durable admission,
Option, numeric comparison and versioned contracts remain outside this slice;
#78 remains open.

### Thirty-fourth-slice verification

- `bash .github/ai/bootstrap.sh` passed with .NET 11.0.100. The full verifier
  rerun passed documentation (137 files, 309 links) and all 56 automation tests,
  then the pinned MCR image pull failed `Forbidden`. Its first run encountered
  the previously observed intermittent application compiler build failure with
  no diagnostic; the automation rerun passed. The exact-base isolated runtime
  verifier restored and hit the same MCR image-pull failure. Hosted exact-head
  checks and the real intentional-failure/clean runtime pilot remain required.
- `bash scripts/verify-runtime.sh` passed 2 baseline and 280 feature cases;
  `bash scripts/verify-sqlite.sh` passed 1 baseline and 52 feature cases.
- `bash scripts/verify-compiler.sh` passed 18 baseline and 406 feature tests in
  a fresh unique short source snapshot; the unchanged TRX guard accepted
  508,460 bytes.

## Thirty-third slice: validated record payloads for typed handoff

Base `b54f92973f04156f0719c35d7d435cc461bd3489` supports validated union
payloads in both directions, but the common record message root has no equivalent
boundary. Add an internal validated record payload: copy input bytes before
descriptor-based decoding, retain the immutable record and defensive byte copies,
and encode an already descriptor-checked record directly. Preserve original bytes
on decode; encoding follows the existing provisional record codec. Do not change
the volatile store or adopt canonicalization, public APIs or a durable wire format.

Add a runtime source file, new runtime feature tests, this plan and status update.
Merged #9, #14, #25, #28, #31, #38 and #59 supply setup, contract boundaries,
records, supported types and the volatile reference. AC1/AC3: exercise a nested
record array, exact number and nullable/missing fields through typed encode,
test-owned volatile handoff, retrieval and validation. AC2/AC4: test raw-byte
snapshot ownership, bad nested values and redacted paths before test-owned
handoff. Preserve all existing tests and avoid callbacks or reflection. Run
bootstrap, full verifier, runtime, SQLite, compiler and exact-base isolated
runtime verification; record results. Public D# integration, durable admission,
Option and versioned schema evolution remain open; #78 stays open.

### Thirty-third-slice verification

- `bash .github/ai/bootstrap.sh` passed with .NET 11.0.100. Full
  `bash .github/ai/verify.sh` passed documentation (137 files, 309 links) and
  all 56 automation tests, then the pinned MCR image pull failed `Forbidden`.
  The exact-base isolated runtime verifier restored and hit the same image-pull
  failure. Hosted exact-head checks and the real intentional-failure/clean
  runtime pilot remain required.
- `bash scripts/verify-runtime.sh` passed 2 baseline and 262 feature cases;
  `bash scripts/verify-sqlite.sh` passed 1 baseline and 52 feature cases.
- `bash scripts/verify-compiler.sh` passed 18 baseline and 406 feature tests in
  a fresh unique short source snapshot; the unchanged TRX guard accepted
  508,460 bytes.

## Thirty-second slice: hand off encoded typed union snapshots

Base `3abfc4727878700b02f1638ecb745aa8d75868d7` validates and snapshots
incoming union JSON, but callers starting with an immutable typed union must
encode and decode it again to obtain the same validated payload. Add an internal
factory that encodes an existing, descriptor-checked union snapshot using the
existing codec and retains defensive bytes and the immutable typed value. Do not
adopt a new wire format or canonical equality rule. The existing constructors
already enforce descriptor identity, eligible fields and depth; no application
callbacks, schema discovery or reflection are introduced.

Change `src/DSharp.Runtime/JsonValidatedUnionPayload.cs`, add a new runtime
feature test and update this plan and the implementation status. Merged #9, #14,
#28, #31, #38 and #59 supply setup, contract boundaries, supported types and the
volatile reference. AC1/AC3: construct nested typed data, encode and hand off
its defensive snapshot through a test-owned volatile caller, retrieve and decode
it with the same explicit descriptor, including exact numbers, null and missing
fields. AC2/AC4: exercise invalid descriptor construction, null input, redacted
decode failures and mutation isolation. Fixture metadata and orchestration are
test-owned. Run bootstrap, full verifier, runtime, SQLite, compiler and exact-base
isolated runtime verification; record results. Public D# APIs, durable
admission, Option, canonicalization and versioned contracts remain open; #78
stays open.

### Thirty-second-slice verification

- `bash .github/ai/bootstrap.sh` passed with .NET 11.0.100. Full
  `bash .github/ai/verify.sh` passed documentation (137 files, 309 links) and
  all 56 automation tests, then the pinned MCR image pull failed `Forbidden`.
  The exact-base isolated runtime verifier restored and hit the same image-pull
  failure. Hosted exact-head checks and the real intentional-failure/clean
  runtime pilot remain required.
- `bash scripts/verify-runtime.sh` passed 2 baseline and 259 feature cases;
  `bash scripts/verify-sqlite.sh` passed 1 baseline and 52 feature cases.
- `bash scripts/verify-compiler.sh` passed 18 baseline and 406 feature tests in
  a fresh unique short source snapshot; the unchanged TRX guard accepted
  508,460 bytes.

## Thirty-first slice: validate and snapshot typed union bytes before handoff

Base `7a3c8617bc6d48143355f83d58982758fd1daee1` demonstrates that the
volatile reference stores opaque bytes without validating them. Add an internal
validated union payload that copies the supplied bytes *before* decoding with
an explicit existing union descriptor, returns the existing redacted structured
failure if decoding fails, and exposes only defensive byte copies with the
validated immutable union snapshot. Preserve the caller's exact JSON bytes,
including numeric spelling, whitespace and property order; this is not a
canonicalization or versioning policy. A test-owned caller can hand those bytes
to the volatile reference only after validation succeeds. No provider or store
implementation is changed and the reference continues to accept opaque bytes.

Affected files: a new internal `src/DSharp.Runtime/` implementation, new
`tests/runtime-features/` tests, this plan and implementation status. The
merged #9, #14, #28, #31, #38 and #59 supply setup, contract boundaries,
supported types and the volatile reference. AC1/AC3: validate, snapshot, hand
off, retrieve and decode nested typed data while preserving bytes and descriptor
identity. AC2/AC4: reject malformed, unknown or invalid input with redacted
paths before test-owned handoff; exercise caller/output mutations and no
application constructors. The test uses opaque fixture metadata, not a wire
contract. Run bootstrap, full verifier, runtime, SQLite, compiler and exact-base
isolated runtime verification; record results. Public D# APIs, durable admission,
Option and versioned schema evolution remain outside this slice; #78 stays open.

### Thirty-first-slice verification

- `bash .github/ai/bootstrap.sh` passed with .NET 11.0.100. Full
  `bash .github/ai/verify.sh` passed documentation (137 files, 309 links) and
  all 56 automation tests, then the pinned MCR image pull failed `Forbidden`.
  The exact-base isolated runtime verifier restored and hit the same image-pull
  failure. Hosted exact-head checks and the real intentional-failure/clean
  runtime pilot remain required.
- `bash scripts/verify-runtime.sh` passed 2 baseline and 257 feature cases;
  `bash scripts/verify-sqlite.sh` passed 1 baseline and 52 feature cases.
- `bash scripts/verify-compiler.sh` passed 18 baseline and 406 feature tests in
  a fresh unique short source snapshot; the unchanged TRX guard accepted
  508,460 bytes.

## Thirtieth slice: exercise JSON bytes through the volatile reference store

Base `c5636897aaf144c7d32bbca91c4870eabd224e01` has internal JSON value,
record and union codecs and the #59 volatile targeted-submission reference, but
their composed behavior has not been exercised. Add runtime feature tests that
encode a nested typed union with an exact-number token, explicit null and JSON
tree, store its bytes with test-owned opaque metadata, retrieve and decode the
stored snapshot, and verify matching retries versus changed payload or metadata.
Test that different numeric spellings remain distinct bytes in this reference,
without claiming canonical semantic equality. Exercise malformed stored input
and redacted paths, explicitly confirming that this store is byte-oriented and
does not validate JSON on acceptance. These tests use no reflection or callbacks.

Affected paths are a new `tests/runtime-features/` test, this plan and the
implementation status. Merged #9, #14, #28, #31, #38 and #59 supply setup,
the existing contract boundaries, types and volatile reference. Map nested
round-trip and capability-boundary evidence to AC1/AC3 and redacted invalid
input to AC2/AC4. Metadata values and orchestration belong solely to the test:
this does not implement durable storage, admission validation, a public D# API,
Option, logging or a versioned wire contract. Run bootstrap, full verifier,
runtime, SQLite, compiler and exact-base isolated runtime verification; record
results. Keep #78 open for those outstanding integration and contract criteria.

### Thirtieth-slice verification

- `bash .github/ai/bootstrap.sh` passed with .NET 11.0.100. Full
  `bash .github/ai/verify.sh` passed documentation (137 files, 309 links) and
  all 56 automation tests, then the pinned MCR image pull failed `Forbidden`.
  The exact-base isolated runtime verifier restored and hit the same image-pull
  failure. Hosted exact-head checks and the real intentional-failure/clean
  runtime pilot remain required.
- `bash scripts/verify-runtime.sh` passed 2 baseline and 254 feature cases;
  `bash scripts/verify-sqlite.sh` passed 1 baseline and 52 feature cases.
- `bash scripts/verify-compiler.sh` passed 18 baseline and 406 feature tests in
  a fresh unique short source snapshot; the unchanged TRX guard accepted
  508,460 bytes.

## Twenty-ninth slice: signed and unsigned 64-bit integer fields

Base `847198624af94149d1c4b02d85376cbcc78b192e` preserves arbitrary JSON
number tokens but only has an int32 typed scalar. D# already executes `long`
and `ulong` in the merged #177 integer-width slice. Add explicit internal
signed/unsigned 64-bit scalar codecs and record-field descriptors, including
nullable/optional presence. These reuse the existing record/union/tree bridge
and never pass through binary floating point. Numeric spelling is normalized
when converting to an integer; the existing exact-number type remains available
when token spelling must be retained. Do not claim that all numeric widths,
collections, public D# bindings or durable encoding are implemented.

Change the scalar and record codecs, add runtime feature tests, and update this
plan and implementation status. Merged #9, #38, #28, #31 and #14 are the
issue's verified prerequisites; #177 supplies executable `long`/`ulong`
behavior without requiring closure of parent #48. AC1/AC3: exercise root
scalars and nested record/union/tree round trips at both width boundaries,
including nullable fields. AC2/AC4: verify absent, explicit null, required
absence, type/range errors and redacted paths, without application callbacks.
Run bootstrap, full verifier, runtime, SQLite, compiler and exact-base isolated
runtime verifier; record actual results. Public JSON APIs, Option policy,
logging envelope and durable contract/version adoption remain outside this
internal slice, so #78 remains open.

### Twenty-ninth-slice verification

- `bash .github/ai/bootstrap.sh` passed with .NET 11.0.100. Full
  `bash .github/ai/verify.sh` passed documentation (137 files, 309 links) and
  all 54 automation tests before the pinned MCR image pull failed `Forbidden`.
  The exact-base isolated runtime verifier restored, then hit the same image
  failure. Hosted exact-head checks and the trusted runtime intentional-failure
  and clean pilot remain required.
- `bash scripts/verify-runtime.sh` passed 2 baseline and 251 feature cases;
  `bash scripts/verify-sqlite.sh` passed 1 baseline and 52 features.
- `bash scripts/verify-compiler.sh` passed 18 baseline and 406 feature cases in
  a fresh unique short source snapshot; its unchanged TRX guard accepted
  508,460 bytes.

## Twenty-eighth slice: standalone typed scalar-map tree bridge

Base `84a0fc5168494e0b2e0f60f6abf36e412fbb2ec8` supports scalar maps as
record fields, but unlike record and union maps it cannot bridge a standalone
typed scalar map to the JSON value tree and back. Add that internal bridge for
boolean, integer, string and exact-number values, including nullable values.
Reuse the existing scalar decoder so integer validation and error codes agree
with record fields; retain exact number spelling and immutable ownership.

Affected files: `src/DSharp.Runtime/JsonTypedFieldMapping.cs`, the existing
scalar record codec where needed for reuse, new `tests/runtime-features/`
coverage, this plan and the implementation status. Merged prerequisites #9,
#38, #28, #31 and #14 are documented below. AC1/AC3: round-trip each map type
through encoded JSON and the heterogeneous tree, including null entries and
large exact numbers. AC2/AC4: verify wrong root, wrong entry type, null in a
required map, invalid integers and redacted indexed errors. Verify empty maps,
snapshot ownership and invalid descriptors. Run bootstrap, full verifier,
runtime, SQLite, compiler and exact-base isolated runtime verifier; record
actual results. This internal slice does not adopt a public D# API, durable
wire contract, Option representation or logging envelope. Those decisions and
full #78 acceptance remain open.

### Twenty-eighth-slice verification

- `bash .github/ai/bootstrap.sh` passed with .NET 11.0.100. The full
  `bash .github/ai/verify.sh` passed documentation (137 files, 309 links) and
  54 automation tests, then failed at the pinned MCR image pull (`Forbidden`).
  The exact-base isolated runtime verifier restored successfully but hit the
  same image-pull failure. Hosted exact-head checks and the trusted runtime
  deliberate-failure/clean pilot are still required.
- `bash scripts/verify-runtime.sh` passed 2 baseline and 242 feature cases.
  `bash scripts/verify-sqlite.sh` passed 1 baseline and 52 features.
- `bash scripts/verify-compiler.sh` passed 18 baseline and 406 features in a
  fresh short-path source snapshot; the unchanged TRX guard accepted 508,460
  bytes. An earlier longer-path run passed the test cases but failed the guard
  with a 514,144-byte report.

## Twenty-seventh slice: reject live capabilities at JSON boundaries

Base `e543d6ef0d6e369f6f8048eb3feb7d990f7bcd3e` has internal typed
record, union, list, map and tree codecs, but their acceptance evidence does not
exercise the issue's named rejection categories together. Add focused runtime
feature tests that attempt to insert a delegate, Task, disposable resource and
mutable foreign object into the actual object-valued record and scalar-map
construction boundaries, including nested records and unions. Use controlled
sentinel values whose `ToString`, enumeration and disposal expose unintended
application callbacks. Assert rejection without invoking them or leaking their
secret data, while a valid immutable nested record/union/collection still round
trips through bytes and the tree bridge. Preserve the existing API and codecs
unless the tests expose an in-scope defect.

Map unsupported capability rejection and nested round trips to AC3, fixed
redacted failures and no application callbacks to AC4. Tests use #9's enabled
runtime paths; no public D#, Option, logging or durable contract is adopted.
Preserve existing tests, dependencies and trusted verification. Run bootstrap,
full verifier, runtime, SQLite, compiler and exact-base isolated runtime checks
where available; record results. Keep #78 open for public integration and the
remaining contract decisions.

### Twenty-seventh-slice verification

- `bash .github/ai/bootstrap.sh` passed with .NET 11.0.100. Full
  `bash .github/ai/verify.sh` passed documentation (137 files, 309 links) and
  54 automation tests; its isolated compiler stage could not pull the pinned MCR
  image (`Forbidden`). The exact-base isolated runtime verifier restored but
  hit the same image-pull failure. Hosted exact-head checks and the trusted runtime
  deliberate-failure/clean pilot remain necessary.
- `bash scripts/verify-runtime.sh` passed 2 baseline and 231 feature tests,
  including three capability-rejection tests. `bash scripts/verify-sqlite.sh`
  passed 1 baseline and 52 feature tests.
- `bash scripts/verify-compiler.sh` passed 18 baseline and 406 feature tests
  in a short-path source snapshot; the unchanged TRX guard accepted 511,708 bytes.

## Twenty-sixth slice: nullable record map values and tree mappings

Base `b41d3a0a9f896cb60ffad3c3bda0813283cd5786` supports record maps with
required entries and standalone required-record-map tree mapping, but cannot
represent null record values. Add explicit nullable record-map entries,
independently of required, nullable or optional map presence, and standalone
bidirectional tree mapping with an explicit record descriptor. Preserve absent,
null and empty maps versus null entries, immutable snapshots, descriptor identity,
exact number spellings, Unicode and duplicate-key rejection, redacted indexed
errors and the combined 64-container limit through nested records and unions.

Update the private record codec and tree adapter, with runtime feature tests for
nested record/union/tree and standalone round trips (AC1/AC3), map and entry
presence and invalid inputs (AC2), schema identity, snapshots, redaction and
depth (AC3/AC4). Preserve existing tests, dependencies and trusted verification.
No public D#, Option, logging or durable wire semantics are adopted. Run bootstrap,
full verifier, runtime, SQLite and compiler regression, and exact-base isolated
runtime verifier where available; record results and keep #78 open.

### Twenty-sixth-slice verification

- `bash .github/ai/bootstrap.sh` passed with .NET 11.0.100. Full
  `bash .github/ai/verify.sh` passed documentation (137 files, 309 links) and
  53 automation tests; its isolated compiler stage could not pull the pinned MCR
  image (`Forbidden`). The exact-base isolated runtime verifier restored but
  hit the same image-pull failure. Hosted exact-head checks and the trusted runtime
  deliberate-failure/clean pilot remain necessary.
- `bash scripts/verify-runtime.sh` passed 2 baseline and 228 feature tests,
  including five nullable-record-map tests. `bash scripts/verify-sqlite.sh`
  passed 1 baseline and 52 feature tests.
- `bash scripts/verify-compiler.sh` passed 18 baseline and 406 feature tests
  in a short-path source snapshot; the unchanged TRX guard accepted 511,708 bytes.

## Twenty-fifth slice: nullable union map values and tree mappings

Base `805aed3fa7229eebe89b85292f7f107c0e6fb931` supports record fields with
union maps but rejects null union entries and lacks standalone union-map/tree
adapters. Add explicit nullable union values independently of required, nullable
or optional map presence. Add bidirectional standalone mappings for both required
and nullable union maps to the existing heterogeneous tree, using an explicitly
supplied union descriptor on decode. Preserve absent/null/empty maps and null
entries as distinct, exact descriptor identity, snapshot ownership, precise numbers,
redacted indexed failures, duplicate/Unicode rejection and shared depth bounds.

Update the private record codec and tree adapter, with runtime feature tests
covering nested union/record/tree round trips and standalone conversions (AC1/AC3),
presence and invalid values (AC2), schema identity, snapshots, redaction and depth
(AC3/AC4). Preserve existing tests, dependencies and trusted verification. No
public D#, Option, logging or durable wire semantics are adopted. Run bootstrap,
full verifier, runtime, SQLite and compiler regression, and exact-base isolated
runtime verifier where available; record results and keep #78 open.

### Twenty-fifth-slice verification

- `bash .github/ai/bootstrap.sh` passed with .NET 11.0.100. Full
  `bash .github/ai/verify.sh` passed documentation (137 files, 309 links) and
  53 automation tests; its isolated compiler stage could not pull the pinned MCR
  image (`Forbidden`). The exact-base isolated runtime verifier restored but
  hit the same image-pull failure. Hosted exact-head checks and the trusted runtime
  deliberate-failure/clean pilot remain necessary.
- `bash scripts/verify-runtime.sh` passed 2 baseline and 223 feature tests,
  including five nullable-union-map tests. `bash scripts/verify-sqlite.sh`
  passed 1 baseline and 52 feature tests.
- `bash scripts/verify-compiler.sh` passed 18 baseline and 406 feature tests
  in a short-path source snapshot; the unchanged TRX guard accepted 511,708 bytes.

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
