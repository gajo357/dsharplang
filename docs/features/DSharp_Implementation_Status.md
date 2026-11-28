# Compiler implementation status

> **Actor cutover:** The [21 November handoff](DSharp_Actor_Runtime_Instructions.md) is the target, not implemented behavior. The compiler currently executes interfaces/default methods, services and scoped resources. #240 owns removal and migration of the service-dependent fixtures after replacement actor/resource forms exist. The actor dispatcher, durable runtime, registration and observer remain planned. Existing examples classify as follows: `interfaces-services.ds` and `multiline_declarations.ds` demonstrate historical service syntax; `default-interface-methods.ds` demonstrates interface defaults through a service; `owned-service-factories.ds` and `owned-result-resources.ds` demonstrate scoped resource ownership through service factories. Business work moves to actors, external adapters to resources, and pure calculations to functions. Preserve their assertions and interface/cleanup coverage in the migration.

The #9 setup adds a managed runtime project and test-only manual clock and
scripted randomness harness. #267 adds a typed dispatcher interface, route and
message-contract descriptors, and distinct opaque receipt types. Runtime feature
tests exercise the interface using recording fakes. #59 adds a provisional,
single-process, volatile reference for targeted and publication identity and
lookup: tests cover byte-exact retries and conflicts, pinned selections and frozen
publication intents, and concurrent admission in the shared submission namespace.
Targeted requests also retain an optional copied opaque public-target key; this
does not define its encoding or implement ordering policy.
The reference also exercises a simulated-clock targeted claim, lease renewal and
explicit terminal lease-expiry failure with fenced owners, and stores copied
opaque normal-completion bytes only for a current unexpired owner. An explicitly
reported fault can be rescheduled with a caller-chosen delay; the reference keeps
prior attempt data and requires fresh ownership after simulated eligibility. A
caller can also select retry after an exactly observed expired lease; a tagged
ordered history distinguishes lease loss from a reported fault. A current
unexpired owner can explicitly record an opaque terminal reported fault and an
optional caller-supplied, unverified origin invocation ID. The volatile reference
can retain an optional caller-normalized structured fault report, separating a
primary observation from ordered cleanup observations and preserving a supplied
origin through fault retry, terminal and cancellation-inspection paths. It does not
normalize or redact diagnostics, verify origins or propagate actor fault chains.
The volatile reference
also records cancellation winner ordering and separately retains a still-fenced
attempt's observed normal or fault exit; it does not signal or stop execution.
Test-side reusable scenarios exercise this in-process subset and detect a
deliberately unfenced stale-owner commit. They are not provider certification or
evidence of canonical equality, persistence, restart or durable delivery.
Additional controlled-clock scenarios cover lease renewal, stale observations,
delayed retry eligibility and bounded ownership races. A negative control detects
an actual stale-deadline recovery in the reference store; it does not establish
real-clock or multi-host behavior.
Test-side outcome scenarios also cover cancellation ordering against normal and
reported-fault exits and controlled lease expiry, including a misrouted-request
negative control. They exercise bookkeeping rather than signalling execution.
An opt-in reference operation fences an owned parent attempt while recording an
awaited Invoke child. The existing raw acceptance path still does not validate
parent ownership; neither path implements host authorization or durable admission.
An internal provisional RuntimeStore interface exposes the tested admission,
ownership and outcome operations with explicit capability validation. Reusable
interface-driven scenarios exercise the volatile reference and detect a real
stale-deadline recovery; they do not certify a durable provider or finalize its API.
The volatile reference can retain eligible work as explicitly blocked when a
host reports its pinned implementation unavailable or incompatible. Versioned
observations and block IDs fence stale transitions; cancellation remains terminal.
The reference does not itself assess compatibility or select a replacement.
An explicit reference-only failure point and reusable scenarios exercise rollback
after publication ID reservation, and simulate a lost response after committing
the full frozen set; these are not crash or durable-transaction evidence.
This is not a canonical codec, production store or durable acceptance; execution,
result schema validation, retry policy, durable recovery and provider conformance
remain open.
No production dispatcher or application host is implemented. #269 adds the
bounded generated Invoke entry described below. See the
[runtime seam plan](implementation/issue-267-plan.md).

#60 first added an internal schema-version migration coordinator over a
caller-supplied connection, tested with a deterministic fake provider. That
coordinator alone does not establish physical rollback, persistence or restart.
The optional #10 SQLite setup exercises that coordinator against a real file,
including transaction rollback and reopening. It does not implement an actor
schema or establish crash durability.
The next #60 slice adds an internal version-one SQLite identity schema for
submissions, invocations, attempts, publications and delivery identities. Its
real-provider tests cover relational constraints and reopening. The tables alone
do not provide atomic admission, immutable acceptance or crash recovery.
An internal file opener now checks DELETE journal mode and configures foreign
keys, FULL synchronous mode and an initial busy timeout before applying that
schema. Tests check these settings and preserved records across reopen. The host
still owns local file selection and process ownership; callers control the
returned connection.
An opt-in version-two migration adds raw per-attempt normal/fault exit and
ordered fault-observation tables. Real SQLite tests exercise upgrades, rollback
and reopen while the default version-one path continues to reject newer files.
These tables do not implement a writer, ownership fencing, normalization or
typed validation of fault origins.
An opt-in version-three migration adds raw lifecycle and ordered recovery
records, including separate lease-expiry and reported-fault tags. It preserves
prior schema targets; it does not implement state transitions, fencing or host
availability checks.
An internal SQLite write-batch helper snapshots trusted statements and parameters
before executing them in one transaction. Real-provider tests cover commit,
rollback and a deferred-constraint commit failure; this is not an admission
protocol, ownership fence or durable acknowledgement.
An opt-in v3-to-v4 binding validates stored application IDs and installs ordinary
row guards in one SQLite transaction. Tests cover reopen and competing binders;
the host still owns path selection and existing-file provenance. The bound path
currently rejects IDs containing NUL or unpaired UTF-16 and does not defend
against deliberate changes to the trusted schema.
An explicit path now initializes a fresh file or upgrades a supported v1/v2
file and binds it in the same SQLite transaction. Tests cover preserved records,
mismatch rejection, competing binders and rollback of an intermediate upgrade.
An internal bound-file reader retrieves one invocation's stored requested and
selected IDs, opaque input, target and unverified ancestry after reopen, including
publication-derived work. It does not select implementations, decide availability,
claim work or execute actors.
An additional real SQLite test commits model-shaped records in one process and
reads them from a second process after the writer exits, including pinned
selection, encoded inputs, fault/lifecycle rows and separate application files.
This demonstrates normal committed-file persistence, not SQLite admission,
power-loss recovery or host availability reporting.
An internal bound-file reader can also retrieve a saved publication, including
one with no deliveries, and an individual delivery's frozen requested/selected
IDs after reopen. Separate reads are separate snapshots; this does not perform
publication admission, fanout or execution.
Another real SQLite test accepts targeted and publication records in the #59
volatile reference, maps them to the schema in a test-owned transaction, and
reads the saved input and pinned identities after that writer process exits.
That test mapping is not SQLite admission or provider conformance.
The [#60 completion audit](implementation/issue-60-plan.md) maps its schema,
transaction, migration and normal process-reopen criteria to the real-provider
tests. Actual durable admission remains #61; implementation availability and
execution remain #64. Neither is implied by the schema evidence.

The first #78 slice adds internal JSON scalar primitives for bool, int32 and
string, including explicit nullable variants and redacted root-path decode
errors. Tests cover type, range, Unicode and malformed-input rejection. This is
not an adopted public API, composite/Option encoding, canonical durable codec
or actor admission support; #78 remains open.
Internal signed and unsigned 64-bit scalar codecs and record fields, including
nullable and optional fields, also round-trip through nested union/tree bridges
without floating-point conversion; typed integer decoding rejects out-of-range
or fractional numbers and reports redacted paths.
Internal signed and unsigned 64-bit arrays and string-keyed maps also support
nullable elements and independently nullable or optional collections, with
full-width boundary values, copied snapshots and indexed redacted errors.
Internal decimal scalars and record fields decode only exactly representable
.NET decimal values, including exponent forms, and reject overflow or precision
loss with redacted errors. Typed decimal encoding follows decimal value/scale;
the separate exact-number token retains original JSON spelling when needed.
Decimal conversion scans long mantissas without making another full mantissa
copy; JSON parsing and the raw token still allocate with input size.
Decimal arrays and string-keyed maps now compose through internal record and
union fields, including nullable elements and optional or nullable collections,
with copied snapshots, redacted indexed errors and bounded nesting.
Test-owned integration also validates a nested typed union, transfers its copied
bytes through the volatile reference and real SQLite, and decodes them after the
writer exits. It checks null versus missing fields and rejects corrupt input with
redacted errors. SQLite remains opaque; this does not implement durable admission,
an adopted message format, or a schema-version negotiation policy.
The next internal slice handles arrays of these scalar and nullable element
types, including two-level int32 arrays, with generated indexed error paths and
read-only decoded collections. This array slice does not establish a D#
collection codec or define union or Option encodings.
An additional internal descriptor handles records of the supported scalar
kinds, preserving missing versus explicit-null optional fields and returning
redacted schema-field error paths. Its case, unknown-field and duplicate-field
policies are provisional; it does not construct application records or establish
a public record, Option or durable schema-evolution format.
The same internal descriptor now supports nested record snapshots up to depth
64, including nullable nested fields and escaped nested error paths. Internal
fields can also contain arrays of explicitly described records, with nullable
array presence and redacted indexed child paths. Elements must be non-null
snapshots of the exact child descriptor. This does not construct D# records or
define union/Option or durable encoding contracts.
Internal record fields also support non-null bool, int32 and string arrays,
including nullable array presence, with copied snapshots and redacted indexed
errors. Separate internal descriptors support nullable scalar elements as well,
independently of array nullability; public collection mappings remain future work.
An internal experimental tagged-union codec composes explicit case descriptors
with optional record payload snapshots and redacted path-aware failures. Its
object shape is provisional and does not adopt a public D# union, Option or
durable message format.
Internal record fields can compose that union descriptor in either direction,
including through record arrays, with exact descriptor identity, nullable
presence and redacted nested paths under the combined depth limit.
Nested tagged unions and record payloads now write to their containing JSON
writer directly, avoiding intermediate encoded byte arrays at each union layer;
feature tests preserve existing bytes, null entries and typed round trips.
Union and record composition now reads parsed JSON elements directly, so invalid
UTF-8 in nested union payload strings and keys returns a redacted indexed or
schema-field error rather than escaping as a transcoding exception.
An internal exact-number token codec and record field preserve large JSON
integers and decimal/exponent spellings without binary floating-point conversion.
This does not establish numeric comparison or a canonical durable format.
Internal record scalar arrays also support exact-number tokens, including nullable
elements and nullable or optional array fields, with indexed redacted errors.
Internal record fields can also contain arrays of one exact tagged-union
descriptor, with nullable or optional array presence and redacted indexed errors.
Separate internal union-array descriptors also support nullable elements while
preserving null-array, missing-field and union-case distinctions.
An independent internal JSON value tree supports heterogeneous nested scalar
lists and string-keyed maps, exact number spellings, owned snapshots, a bounded
depth and redacted indexed errors. It is not yet a public D# or logging API.
Record fields now stream these trees to their containing writer without an
intermediate encoded byte array or child-array clones during traversal, while
external snapshots remain owned and existing provisional bytes are preserved.
The tree reader constructs indexed error paths on failure rather than allocating
them for every successful element, retaining first-error ordering and redaction.
It also snapshots parsed list and map buffers once instead of cloning the same
new array twice; validation and owned snapshots remain unchanged.
Internal record descriptors also support required and nullable nested int32
arrays, with owned rows, optional nullable presence, typed tree/union round trips
and redacted indexed errors. This does not adopt a public JSON encoding contract.
An internal adapter encodes finite binary32/binary64 values as JSON number
nodes and converts number nodes back using invariant nearest-representable
rounding, retaining signed zero and rejecting overflow. It rejects non-number
nodes and non-finite inputs with structured errors. It also encodes arrays of
these values, including nullable elements, with owned snapshots and redacted
first-invalid indexed errors. It decodes required and nullable float arrays with
invariant rounding, returning fresh arrays or a redacted first-invalid indexed
error. Explicit internal record descriptors also support required and nullable
finite float fields, including optional presence and redacted field-path errors.
Typed record arrays likewise support finite floats, nullable arrays and elements,
owned snapshots, and redacted indexed errors, with a scalar-array tree bridge.
Typed record maps support finite floats and nullable maps and values with the
same snapshot and redacted indexed-error behavior and a scalar-map tree bridge.
An opt-in internal binding associates a caller-assigned opaque identity with
one record or union descriptor, requiring exact metadata equality before decode.
It owns identity and payload snapshots; a SQLite fixture verifies matching,
wrong-identity and corrupt-data behavior after a writer process exits. Callers
own identity uniqueness and versioning; this does not infer compatibility or
implement durable admission.
An opt-in codec-local catalog rejects duplicate identities across record and
union bindings and resolves exact identities to the requested kind before
encoding or decoding. Encoding also requires the bound descriptor and returns
an owned identity/payload pair. Unknown, wrong-kind or wrong-descriptor inputs
return a redacted mismatch; the catalog does not register actor routes or assign
versions.
An opt-in v1 fingerprint describes the exact ordered record/union descriptor
shape and is reproducible across independent processes. Callers must combine it
with a logical namespace: unrelated contracts with the same shape have the same
fingerprint. It does not establish semantic compatibility, authenticity, schema
evolution or durable admission.
An opt-in internal builder combines caller-supplied namespace bytes and that
shape fingerprint into a framed, owned identity for the existing codec catalog.
Callers remain responsible for namespace uniqueness and versioning.
A separate-process SQLite fixture stores identities from two logical namespaces
with the same record shape, then reconstructs catalogs after writer exit and
checks namespace and changed-shape rejection and redacted corrupt-data errors.
The fixture uses direct SQL and does not implement durable admission.
An opt-in internal contract decoder rejects metadata or payloads exceeding
caller-supplied byte limits before reading or copying either input. Within its
limits it delegates to the existing catalog. This does not bound all parser
allocations or implement transport or durable admission limits.
This does not define a public wire contract.
Explicit internal record fields can hold those value trees through nested records
and unions, with a combined 64-container bound and redacted nested errors.
Nullable tree fields can also be optional, retaining missing, explicit null and
present non-null values as distinct internal states.
Explicit internal adapters map typed record and union snapshots to JSON value
trees and back using a supplied descriptor and the existing provisional codecs.
Those adapters also map standalone record and union arrays, including nullable
union elements, with indexed redacted failures and bounded nesting. Nullable
record-array elements also round-trip as fields and standalone tree mappings.
Internal record fields can contain string-keyed maps of one record descriptor,
including nullable or optional map presence, with indexed redacted errors.
Standalone typed record maps also map to and from JSON value trees using an
explicit descriptor, retaining snapshots, exact values and indexed errors.
Nullable record-map values are supported in fields and standalone tree mappings,
independently of nullable or optional map presence.
Internal record fields also support boolean, integer, string and exact-number
string-keyed maps, including nullable or optional map presence, non-null typed
values, immutable snapshots and redacted indexed errors. Explicit variants also
support nullable scalar values, independently of nullable or optional maps.
Standalone typed scalar maps now also round-trip through JSON value trees using
an explicit scalar kind, with nullable entries, exact number spellings,
immutable snapshots and redacted indexed failures.
Standalone typed scalar arrays also map to and from JSON value trees using an
explicit scalar kind, retaining nullable entries, exact numbers, copied values,
depth limits and redacted first-error indexes.
Record fields can also hold string-keyed maps of one tagged-union descriptor,
with nullable or optional map presence and indexed redacted errors. Explicit
variants also allow null union values, and standalone union maps map to and
from value trees using a supplied descriptor. Runtime tests also exercise rejection
of delegate, Task, disposable and mutable foreign values at the internal record
and scalar-map boundaries without invoking sentinel callbacks or exposing contents.
Test-owned composition also round-trips nested JSON bytes through the volatile
targeted-submission reference, including copied metadata and byte-sensitive retry
conflicts. That reference accepts opaque bytes without JSON validation; this
evidence does not implement durable admission or an adopted message contract.
An internal validated union payload now snapshots bytes before decoding against
an explicit descriptor and returns redacted errors or a decoded immutable value
with defensive byte copies. A test-owned caller demonstrates validation before a
volatile-reference handoff; no store admission policy is implied.
The same internal payload can be created from an already descriptor-checked
union, encoded and recovered through that test-owned reference handoff while
preserving exact numbers, null/missing distinctions and immutable snapshots.
An equivalent internal record-root payload validates a copied byte snapshot or
encodes a descriptor-checked record; its tests cover nested record arrays and
the same test-owned handoff without changing store admission.

The original executable baseline is recorded in [bootstrap scope](../COMPILER_BOOTSTRAP.md).
The matrix below includes subsequent implemented feature slices.

The inventory application example uses the current multi-file compiler subset;
its verifier checks and compiles the actual sources and executes the generated
pure calculation, immutable stock copy update and multiple inventory inputs;
the application module also constructs and updates the imported stock record
and verifies the resulting balance across multiple inputs. It independently
inspects original and returned generated stock values for a separate sale, with
a clean-source negative control for that direct observation. Clean-source negative
controls reject constant implementations that would still return 42 for the
summary.
It does not demonstrate an actor runtime or an SDK scaffold.
An automation test also runs its documented command from a clean committed source
export after checking the application instructions and their local links.
The command builds a copy of the checkout compiler in a private temporary
directory; a read-only-source test verifies it does not write compiler outputs
back into the checkout.
Its quick reference reproduces those source files and is checked for drift and
unmarked D# examples.
The example and scaffold relative Claude imports were checked with Claude Code
2.1.332 against a local mock, including controls without imports.
This status describes tested implementation, not the full syntax draft.

The [pipes, partial application and logging specification](DSharp_Pipes_and_Logging.md)
is a target addition tracked in #129–#136. Issue #135 implements the ordinary
function partial-application slice below. Issue #136 adds synchronous Result
helpers; #219 adds task-input `map`, #221 adds the asynchronous `andThen`
combinations, and #223 adds task-input `mapError`, `tap` and `tapError`.
The #130 contract remains open: task-valued `map` payloads are not supported
in stored Result values. Callable HTTP preprocessing, Logger and the remaining
#129 integrations remain future work.
The #35 plain-pipe and #36 tuple milestones retain their tested scope.

Actor declarations now expose checked Handle input/result, dependency and source
metadata, with internal generated bodies. Tests execute those bodies through a
host-side harness for values, `Unit`, business `Result.Error`, await, helpers and
borrowed resources; D# code cannot construct or directly call an actor. Host-owned
dependencies cannot be released, aliased or forwarded as ordinary values, and
async borrowed operations must be immediately awaited. Registration, a production
dispatcher and durable execution remain unimplemented.

An explicit async top-level host entry can directly await a targeted
`Invoke<Actor>(message)`. The compiler checks the actor's Handle input and emits
the exact Handle result through a host-supplied `IActorDispatcher`; recording
tests execute generated value, Unit and business-error results and isolated
host contexts. A directly awaited targeted `Dispatch<Actor>(message)` can be
the final result of an unannotated async host entry; it yields an opaque runtime
invocation receipt and shares the target route with Invoke. Recording tests
exercise pending fake acceptance, exact receipt identity and isolated hosts.
Receipt construction, source annotations and storage are not supported. An
explicitly typed ordinary async function may directly await a fully applied
Invoke entry or another such function; generated calls forward its host-supplied
dispatcher, including across tested qualified and recursive chains. Recording
tests exercise interleaved hosts, pending calls and faults. An otherwise empty
inferred async module function can directly await and return either opaque
Dispatch receipt through a fully applied nongeneric relay, forwarding only data
parameters or Unit. Recording tests cover separate receipt kinds, interleaved
host scopes and faults. Receipt storage, captures, partials, unawaited calls and
dispatcher operations inside actor
helpers or lambdas remain unsupported. A resource-free actor Handle whose sole
result directly invokes a target can also use an explicit host-supplied
dispatcher. Its message may use the input, sequential proven pure immutable
locals, record fields/construction and primitive literal nodes; operators,
arbitrary calls and other statement forms remain unsupported. Checked actor
metadata identifies this body signature.
Recording tests cover isolated host contexts, exact results and faults. Other
actor-body shapes remain unimplemented. Direct or transitive actor operations
are rejected while a locally owned `using` scope is live; without transaction
classification this conservatively includes ordinary owned resources. Ordinary
resource awaits and actor operations after cleanup remain supported. This is not
a complete transaction or provider policy. A directly awaited targetless Dispatch host
entry accepts a conservative checked message subset and yields an opaque
publication receipt; recording tests exercise fake acceptance and host isolation.
Actual publication, subscriptions and delivery remain unimplemented. For affected CLI
compilations, `DSHARP_RUNTIME_ASSEMBLY` supplies the runtime DLL copied into the
generated project. This compiler seam does not implement dispatch or durability.

Actor queue bindings can be checked as source metadata against the resolved
Handle input. The compiler accepts a nonnullable declared record/union or Unit
root and checks a conservative nested data subset after generic substitution;
it rejects mismatched contracts, unsupported shapes and duplicate queue names.
The checked binding does not register a target, encode a message or deliver work.
The queue codec and general HTTP trigger surface remain unimplemented.

Actor event declarations now expose checked candidate message metadata using
the same structural data and Handle-input rules. They do not create a
subscription or activate delivery; explicit application registration and event
publication remain future work. An executed compiler integration test targets
the same Queue/Event-bound actor through both Invoke and targeted Dispatch,
preserving its business Error and separate fake acceptance receipt through the
shared typed route. It does not demonstrate actual admission or delivery.

Actor Cron declarations expose checked message and raw expression metadata
using those same structural rules. The expression is not parsed or validated;
schedule registration, calendars, payload provision and execution remain
future work.

The bounded HTTP candidates check a named direct binder and explicit response
mapper signatures against a plain, Unit or Result-valued actor Handle. They retain
inert compiler metadata only; Dispatch receipts, general callable preprocessing,
transport response validation and registration remain future work. The selected
response type is application data, not an HTTP API.

The Result helper bodies are authored as D# in
[`Result.ds`](../../src/DSharp.Compiler/Prelude/Result.ds), packaged with the
compiler and parsed and bound as the active implementation by #142.

The compiler targets the pinned .NET 11 SDK and C# 15. C# 15 provides
[union types](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/tutorials/unions).
The backend uses a private native union with a distinct typed wrapper for each
D# case. This retains case identity for equal payload types, keeps `None`,
`Some(null)` and nested options distinct, and continues using D# structural
equality. Factories construct valid cases; an invalid native default is rejected.
The generated CLR layout remains an implementation detail. No performance or
external interoperability guarantee follows from this lowering. See the
[D# example](../../examples/native_union_cases.ds) and
[implementation plan](implementation/issue-152-plan.md).

| Feature | Parse/type-check | Generated execution |
| --- | --- | --- |
| Checked actor declarations | One typed Handle, helpers, resource dependencies, source metadata and direct-call/escape rejection across supported source layouts | Internal body harness covers value, `Unit`, business error, await and host-owned resource use; no public dispatcher |
| Checked queue bindings | Explicit actor-local Queue metadata, matching Handle input, source locations, conservative recursive data checks and mismatch/duplicate diagnostics | Compiler and CLI checks only; no registration, serialization or queue delivery |
| Checked event candidates | Explicit actor-local Event metadata, matching Handle input, source locations and shared conservative data checks | Compiler and CLI checks only; no subscription registration or publication |
| Checked Cron candidates | Explicit actor-local name, opaque expression, matching Handle input and source locations | Compiler and CLI checks only; no cron validation or scheduling |
| Checked HTTP candidates | Named direct binder and typed response mappers for plain, Unit and Result-valued Handle; source locations and nominal identities | Compiler and CLI checks only; no HTTP host, registration or mapper execution |
| One module, parameterless int literal functions | Implemented and tested | Demo.answer returns 42 |
| Multiple source files and modules | Dotted module names, file-scoped imports, qualified lookup and duplicate/ambiguity diagnostics | Split modules and cross-module D# calls execute from multiple files |
| Immutable function-local int `let` bindings | Inferred/explicit int, earlier-local initializers and literal/local final expressions tested | Chained bindings return 42; independent functions retain distinct values |
| Typed int parameters and same-module calls | Explicit int parameter/return types, positional argument lists, forward and nested call binding | Generated functions accept int arguments and execute calls |
| UTF-16 string and char literals | Literals, basic escapes, parameters/locals/returns and ordinal equality | Unicode and exact UTF-16 code-unit round trips tested |
| Scalar string interpolation | `$"...{expression}..."`, ordinary escapes and `{{` / `}}`; non-nullable string, char, bool, Unit, integer, decimal and binary floating values | Expressions evaluate once from left to right; scalar formatting is invariant, including across async and early-exit paths |
| Unit and explicit return | Unit values, empty blocks, Unit statements and reachable-result checks | Nested early returns leave the nearest function without evaluating remaining expressions |
| Immutable records | Named construction, public fields, nested records, copy updates, structural equality and source diagnostics | Copies preserve the original; field expressions evaluate in source order |
| Discriminated unions and wrappers | Named cases with zero or more positional immutable payloads; qualified or unambiguous construction | Nominal values and nested record/union payloads compare structurally |
| Exhaustive match and guards | Nested union, bool, Unit and primitive patterns, lexical bindings, wildcard and bool guards; checked coverage | Scrutinee evaluates once; first matching arm and its guard execute in source order |
| Nullable values and direct matching | `T?` and `null` in supported signatures, locals, record fields and union payloads; present-value match narrows to `T` | Present and null values remain distinct for scalar, Unit and reference types |
| Nullable fallback and conditional access | Lazy, right-associative `??` and null-preserving `?.` for record fields and supported immutable-value calls; nullable results flatten | Receiver evaluates once; fallback and conditional call arguments evaluate only as needed |
| Generic data and functions | Type parameters, explicit calls, structural inference and idempotent nullable substitution | Generic nullable calls, returns, record and union fields, lists and maps preserve null and present values for value and reference types |
| Explicit Option values | Standard `Option<T>` with `None` and `Some(T)`; nullable and nested payloads remain distinct | Matching and structural equality preserve cases and payloads; no implicit conversion to or from nullable values |
| Result values | Standard `Result<T,E>`, typed `Success`/`Error`, Unit shorthand and exhaustive matching | Present and null successes remain distinct from errors; discarded Result statements are rejected |
| Synchronous Result composition | `andThen`, `map`, `mapError`, `tap` and `tapError` as ordinary generic functions | Applicable callbacks execute once; typed errors and nullable/Option payloads remain distinct |
| Asynchronous Result map | `map` with a synchronous callback and `Task<Result<A, E>>` input in direct calls, pipes and supported typed callable contexts | Awaits the source once; invokes the callback only on success; faults and cancellation propagate |
| Asynchronous Result andThen | `andThen` supports Result or Task<Result> inputs with synchronous Result-returning or async Task<Result>-returning callbacks | Awaits each task once, invokes the callback only on success, and preserves typed errors, evaluation order, faults and cancellation |
| Asynchronous Result error mapping and taps | `mapError`, `tap` and `tapError` accept Task<Result> input with synchronous callbacks | Await input once; invoke the applicable callback at most once and preserve values, errors, faults, cancellation and evaluation order |
| Result propagation | Postfix `?` requires a standard Result operand and an enclosing Result with the same error type | Success yields its payload; Error exits the nearest function without evaluating later expressions |
| Function types and immutable closures | Function-valued parameters, locals and returns; typed or contextually inferred lambda parameters; immutable data captures | Returned closures retain independent captured values; lambda `return` and Result `?` remain within the lambda |
| Value pipes and return inference | `value |> function`, plus inferred returns for ordinary acyclic functions and provably static partial calls | Pipes evaluate their left value once before the target and use normal call semantics |
| Ordinary partial application | Positional prefixes, grouped and empty-prefix calls, function values, generic inference and pipes | Bound values are captured once; the body executes only for completed calls |
| Extension partial application and values | Unique visible immutable-data extensions accept positional prefixes; empty prefixes and bare values need supported context, including a pipe input | Receiver and supplied values are captured once; the body runs only on invocation |
| Immutable Map.set partial application | `map.set(key)` produces a function taking the remaining value | The receiver and key are captured once in order; each invocation creates an independent updated map |
| Immutable List.fold partial application | `items.fold(initial)` produces a function taking the fold callback | Receiver and initial are captured once in order; each completed call folds the captured list left to right |
| Empty-prefix immutable collection methods | `List.append/get/filter`, `Map.get/contains/set` and `Set.add/contains` accept `()` as a partial call | Receiver is captured once; the intrinsic operation runs when its arguments are supplied |
| Generic List empty-prefix methods | `map<U>()` and `fold<A>()` accept explicit or contextually inferred types | Receiver is captured once; callbacks and iteration run only on completed calls |
| Immutable collection method values | An expected callable type selects supported bare List, Map and Set methods, including `length`, `count`, `map` and `fold` | Receiver is captured once; the operation and any callbacks run only when invoked |
| Tuples and destructuring | Tuple expressions and types with at least two elements; immutable nested `let` destructuring, including multiline parenthesized local binding headers | Checked arity, single evaluation, nested tuple values and the distinction between one tuple argument and multiple arguments |
| Immutable lists | Typed and inferred literals; `length`, `append`, `get`, `map`, `filter` and `fold` | Ordered transformations, structural equality, immutable updates and `Option` lookup preserve nullable and nested values |
| Immutable maps and sets | `Map.empty`, `set`, `get`, `contains`, `count`; `Set.empty`, `from`, `add`, `contains`, `count` | Structural keys, immutable updates, nullable/Option distinctions and insertion-order-independent equality |
| Sequential list iteration | `for item in list { ... }` requires a non-nullable `List<T>` and a Unit body; the loop yields Unit and async contexts can await inside it | Source evaluates once, iteration captures are independent, each iteration completes before the next, and return/Result propagation leave the enclosing function or lambda |
| Method-local mutable builders | `MutableList<T>()` and `MutableMap<K,V>()` with local aliases, Unit-valued `add`/`set`, and `freeze`; escape and lambda capture rejected | Frozen lists and structurally keyed maps remain unchanged by subsequent mutations; builders created inside a lambda belong to its invocation |
| Async functions and await | Explicit `async fn` with logical return annotation; `Task<T>` calls and `await` in async functions | Tasks suspend and resume without blocking; nested expressions, early return, Result propagation and same-invocation builders preserve their behavior |
| Async lambdas | Explicit `async` lambdas with typed or contextually inferred parameters, immutable captures and `Task<T>` results | Awaited closures suspend and resume; return and Result propagation remain local to the lambda |
| Interfaces and services | Explicit interfaces, services implementing one or more interfaces, checked methods and constructor dependencies | Interface-typed calls dispatch to the supplied service instance; async methods and explicit dependencies retain their usual behavior |
| Default interface methods | Checked method bodies alongside required signatures; explicit service methods resolve conflicts and take precedence | Default bodies call methods on the selected service instance, including async dispatch |
| Statically resolved extensions | `extend Target` methods require `self` first and an explicit result type; exact static receiver and argument types select visible candidates | Calls evaluate the receiver once before arguments; applicable real members take precedence, with async and directly imported extension calls supported |
| Directly constructed scoped resources | Fresh `resource` declarations, enclosing-block and braced `using`, receiver-only scoped handles | Sync/async release runs on tested exits with C#-style cleanup failure behavior |
| Owned resource function factories | Explicit non-generic functions return a fresh resource directly to `using`; async factories must be immediately awaited | Factory forwarding, async acquisition and cleanup execute without exposing an owned value as ordinary data |
| Owned resource service factories | Interface and service methods, including supported defaults, return fresh resources directly to `using` | Checked polymorphic dispatch, forwarding and immediate async acquisition preserve scoped ownership |
| Owned resources through Result | Checked factories return `Result<Resource,E>` only for direct forwarding or immediate `?` into `using` | Success transfers one scoped resource; Error propagates without acquiring it |
| Typed if/else and lexical scopes | Bool conditions, matching int/bool branches and block-local bindings | Both branches and nested conditionals execute lazily |
| Bool values and logic | Bool literals, types, int comparisons, int/bool equality, !, &&, || with type checks | Checked calls, precedence and short-circuit evaluation tested |
| Checked integer arithmetic | Eight integer widths, context-typed literals, decimal/hex/binary forms, separators and `L`; matching operand types required | Checked arithmetic, unsigned negation and zero/overflow faults execute at invocation |
| Decimal arithmetic | `decimal` values and exact `m`-suffixed literals; same-type unary, arithmetic and comparison operations | Decimal operations follow pinned .NET behavior; equality and collection keys ignore trailing scale |
| Binary32 arithmetic | `float32` values and `f`-suffixed literals; same-type unary, arithmetic and comparison operations | IEEE NaN, infinity and signed-zero behavior extends through supported structural equality; float-containing map/set keys are rejected |
| Binary64 arithmetic | `float` values and unsuffixed fractional or exponent literals; same-type unary, arithmetic and comparison operations | IEEE NaN, infinity and signed-zero behavior extends through supported structural equality; float-containing map/set keys are rejected |
| Comments and documentation | Single-line and non-nested block comments; contiguous `///` groups attach to module, function, record and union declarations | Comments preserve source locations and do not change execution |
| Source diagnostics and duplicate functions | Implemented and tested | Rejected before emission |
| CLI check/compile | Implemented, smoke-tested | SDK builds generated library |
| Remaining language syntax | Unsupported, explicit diagnostics | Not implemented |
| Workers/queues/runtime/interop/LSP | Not implemented | Not claimed |

Preserve syntax reference revision 0.3 selected behaviour and the adopted
[DEC-02 grammar contract](implementation/DEC-02.md); freeze other provisional
contracts before implementing them. Worker-call failure representation,
queue lifecycle, durable serialization, middleware, authorization capability
validity, remaining numeric library APIs and safe .NET interop remain decisions for
later small milestones. No agent isolation or durability follows from this bootstrap.

## Planned work

Follow the [Codex web implementation sequence](implementation/DSharp_Implementation_Sequence.md)
and [task queue](implementation/DSharp_Task_Queue.md). [Issue #7](https://github.com/gajo357/dsharplang/issues/7)
is implemented: immutable function-local bindings with optional type annotations,
no reassignment or shadowing. A binding begins a statement; its expression can
continue inside delimiters or after an operator.
[Issue #19](https://github.com/gajo357/dsharplang/issues/19) extends initializers and
final expressions with checked int arithmetic and parentheses. In that baseline,
unsuffixed literals default to int; unary minus is an operator, and
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
[Issue #48](https://github.com/gajo357/dsharplang/issues/48) adds `sbyte`, `byte`,
`short`, `ushort`, `uint`, `long` and `ulong` alongside `int`. Unsuffixed integer
literals use an expected integer type when available and otherwise default to
`int`; `L` selects `long`. Decimal, hexadecimal and binary magnitudes support
separators between digits. Magnitudes must fit before unary negation, including
for signed minimum values. Arithmetic and comparisons require matching types;
checked overflow and division faults occur at execution. Numeric values work in
the tested nullable, generic, collection, match and multi-file contexts. Explicit
conversion entry-point names remain a later library API decision. Existing value
identifiers such as `short` remain accepted for compatibility, despite DEC-02
reserving numeric names globally; that conflict is unresolved. #48 remains open. See the [D# example](../../examples/integer_widths.ds) and
[implementation plan](implementation/issue-48-plan.md).
[Issue #215](https://github.com/gajo357/dsharplang/issues/215) implements the
`decimal` type and exact `m`-suffixed literals, including the adopted exponent
and digit-separator syntax. Unrepresentable or overprecision literals are
rejected rather than silently rounded. Same-type unary, arithmetic and comparison
operations use the pinned .NET decimal semantics; overflow and division by zero
fault at runtime, and equality ignores trailing scale. Decimal values work in
supported nullable, generic and immutable data contexts, including map and set
keys. `decimal` remains allowed as a value or field identifier for backward
compatibility; DEC-02’s numeric name reservation remains unresolved, as in #48.
Conversions, general text-formatting APIs and currency semantics remain separate
work; decimal interpolation is covered by #217 and binary64 by #227. #49 remains open. See the
[D# example](../../examples/decimal_arithmetic.ds) and
[implementation plan](implementation/issue-215-plan.md).
[Issue #225](https://github.com/gajo357/dsharplang/issues/225) adds the `float32`
type and `f`-suffixed literals with invariant parsing, finite-range checks before
rounding and IEEE round-to-nearest-even behavior. Same-type arithmetic,
comparisons and remainder follow IEEE binary32 behavior, including infinity,
NaN and signed zero. NaN remains unequal to itself through supported nullable,
generic and nested immutable values, even when comparing a value with itself.
Map and set keys containing `float32` are rejected recursively; float values
remain supported in safe data positions. As with other numeric type names,
`float32` remains available as a value or field identifier for compatibility;
DEC-02's global numeric-name reservation remains unresolved. Binary64 `float` is
covered by #227; explicit conversions and general floating formatting remain
separate work, so #49 stays open. See the
[D# example](../../examples/float32_arithmetic.ds) and
[implementation plan](implementation/issue-225-plan.md).
[Issue #227](https://github.com/gajo357/dsharplang/issues/227) adds the D#
`float` type as IEEE binary64, generated as C# `double`, and unsuffixed
fractional or exponent literals with invariant parsing, finite-range checks
before rounding and round-to-nearest-even behavior. Same-type arithmetic,
comparisons and remainder preserve IEEE NaN, infinity and signed-zero behavior.
NaN remains unequal to itself through supported nullable, generic and nested
immutable values, even when comparing a value with itself. Map and set keys
containing `float` are rejected recursively; binary64 values remain supported
in safe data positions. Float32, decimal and integer types remain distinct; no
implicit mixed numeric conversions are added. Explicit conversions and general
floating formatting remain separate work, so #49 stays open. See the
[D# example](../../examples/float_arithmetic.ds) and
[implementation plan](implementation/issue-227-plan.md).
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
and [implementation plan](implementation/issue-23-plan.md). Raw/multiline strings
and general concatenation remain unsupported.
[Issue #50](https://github.com/gajo357/dsharplang/issues/50) adds a scalar
interpolation slice. Embedded expressions in `$"...{expression}..."` are checked
and evaluated once, left to right; `{{` and `}}` insert literal braces, and
ordinary string escapes apply. Strings and characters insert their text, booleans
render as `true` or `false`, Unit as `()`, and `sbyte`, `byte`, `short`, `ushort`,
`int`, `uint`, `long` and `ulong` use invariant decimal formatting. Nullable
and composite values cannot yet be interpolated, and #50 remains open. See the
[D# example](../../examples/interpolation.ds) and
[implementation plan](implementation/issue-50-plan.md).
[Issue #217](https://github.com/gajo357/dsharplang/issues/217) adds interpolation
of non-nullable `decimal` expressions. It renders the full supported decimal range
as plain invariant digits, removes fractional trailing zeros and writes all zero
values as `0`, including negative zero. It retains the existing interpolation
evaluation order and async and fault behavior. General formatting APIs and
nullable and composite interpolation remain separate work. See
the [D# example](../../examples/decimal_interpolation.ds) and
[implementation plan](implementation/issue-217-plan.md).
[Issue #229](https://github.com/gajo357/dsharplang/issues/229) adds non-nullable
`float32` and `float` interpolation. It formats shortest round-trip digits
invariantly, selects the shorter fixed or normalized scientific spelling, and
preserves signed zero, NaN and infinities. Evaluation order and async, early
return and fault behavior follow existing interpolation. General formatting
APIs and nullable and composite interpolation remain separate work; #49 and
#50 stay open. See the [D# example](../../examples/float_interpolation.ds) and
[implementation plan](implementation/issue-229-plan.md).
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
reads preserve absence. See [the example](../../examples/nullable-operators.ds) and
[implementation plan](implementation/issue-29-plan.md).
[Issue #193](https://github.com/gajo357/dsharplang/issues/193) adds conditional
calls to fully supplied synchronous immutable-value methods, including supported
collection operations and visible extensions. The receiver evaluates once; when
it is null, the arguments and method body are skipped. Results are nullable, with
already-nullable results flattened; `Option` values retain their own cases. The
normal member and extension resolution and lifetime restrictions apply. Conditional
partials, Task results, and service, interface, resource or builder dispatch remain
unsupported. Checked, acyclic calls with statically proven targets can infer an
omitted enclosing return as described in #195; unproven targets still require an
annotation. See the
[D# example](../../examples/conditional_calls.ds) and
[implementation plan](implementation/issue-193-plan.md).
[Issue #30](https://github.com/gajo357/dsharplang/issues/30) implements generic
records, unions and functions, including explicit type arguments and
unambiguous inference through supported argument and result types. Generic call
disambiguation follows DEC-02. Closed nullable type arguments such as
`Box<int?>` are supported. Nullable substitution is idempotent, including when
an unconstrained `T?` is instantiated with an already-nullable type. Generated
calls and data preserve null for value, reference and Unit arguments. The C#
backend supplies an associated nullable CLR type argument for an unconstrained
parameter when needed, propagating that requirement through generic calls and
data. These extra arguments are an implementation detail and external C# callers
must not assume the compiler enforces a relationship between arbitrary CLR type
arguments. Generated execution tests cover null and present values for `int`,
`int?`, `string` and `string?` through generic parameters and returns, record
fields, union payloads, lists and maps, including closed `Box<int?>`. Option cases
remain distinct from null. Generic type parameters have distinct lexical identities
even when their source names overlap other types. Generic constraints, variance
and type aliases remain unsupported; alias normalization applies if aliases are
added later. See the [D# matrix example](../../examples/generic-nullability-matrix.ds)
and [implementation plan](implementation/issue-30-plan.md).
[Issue #121](https://github.com/gajo357/dsharplang/issues/121) implemented standard
`Option<T>`, a distinct tagged choice with `None` and `Some(T)`. `Some(null)`
requires a nullable payload type; nested options retain each case. JSON and
HTTP mappings remain undecided. The nullable and Option changes are documented
in the [implementation plan](implementation/nullable-option-plan.md). The
[record example](../../examples/option_records.ds) exercises optional update
fields, nested options and generic records with nullable payloads. Its cases
are also covered by generated-code execution and diagnostic tests.

[Issue #37](https://github.com/gajo357/dsharplang/issues/37) adds immutable
`List<T>` values with contextual empty literals, ordered operations and structural
equality. `get` returns `Option<T>`, distinguishing an absent index from a
stored null. The [D# example](../../examples/list.ds) and
[implementation plan](implementation/issue-37-plan.md) show the supported
operations and test coverage.
[Issue #183](https://github.com/gajo357/dsharplang/issues/183) adds the
nonempty-prefix form `items.fold(initial)`, which returns a function taking the
fold callback. The receiver and initial accumulator are evaluated once in order
when the function is formed; each completed invocation folds the captured list
from left to right, and an empty list returns the captured initial. Existing
fully supplied `fold(initial, callback)` calls retain their behavior. Accumulator
types can be inferred or supplied explicitly or by a compatible expected function
type, including tested nullable, Option and generic values. Builder, resource
and service/interface capability captures remain rejected, including through
generic substitution. Omitted returns can be inferred for tested immediate calls
and pipes when the receiver and callback are statically known. Unknown callbacks
and unproven targets remain conservative. Other collection partials and
unsupported empty-prefix forms remain separate work under #129. See the
[D# example](../../examples/list_fold_partial.ds) and
[implementation plan](implementation/issue-183-plan.md).
`Map` and `Set` are implemented separately in
[#38](https://github.com/gajo357/dsharplang/issues/38); builders and
serialization remain separate work.

Maps and sets use private immutable snapshots and D# structural hashing plus
equality for collision resolution. The initial representation scans entries
linearly; repeated insertion, including `Set.from`, can take quadratic time.
Key eligibility is checked recursively. Because generic key constraints are not
yet supported, an unconstrained key type parameter is rejected; generic value
types remain supported. See the [D# example](../../examples/map_set.ds) and
[implementation plan](implementation/issue-38-plan.md).
[Issue #181](https://github.com/gajo357/dsharplang/issues/181) adds the
nonempty-prefix form `map.set(key)`, producing a function from a value to an
updated map. It evaluates the immutable receiver and key once in order when the
function is formed; each completed call independently updates the captured map,
leaving the original unchanged. Grouped calls and value pipes are supported,
including nullable and Option data and generic value types. Omitted returns can
be inferred for tested immediate calls and pipes when the receiver is statically
known; unproven targets remain conservative. Unconstrained generic map keys
remain unsupported. Other collection method partials remain separate work
under #129. See the
[D# example](../../examples/map_partial.ds) and
[implementation plan](implementation/issue-181-plan.md).
[Issue #187](https://github.com/gajo357/dsharplang/issues/187) adds empty-prefix
partial calls for immutable List `append`, `get`, `filter`; Map `get`, `contains`,
`set`; and Set `add`, `contains`. The receiver is evaluated once when forming
the function; the operation and any callback run only when supplied their
remaining arguments. Grouped calls and pipes retain immutable updates and
nullable/Option lookup distinctions. Zero-argument `length()` and `count()`
remain full calls. Existing applicable full zero-argument extension calls keep
their behavior; a compatible expected callable or pipe can select the intrinsic
partial when that full result is incompatible. Omitted returns can be inferred
through the statically proven immediate and staged calls described in #191;
unproven targets still require an annotation. Contextual empty extension calls
are described in #197; contextual bare extension values are described in #199.
Contextual bare collection method values are described in #201; other bare
method values and service or interface method partials remain separate work under
#129. See the
[D# example](../../examples/collection_empty_partials.ds) and
[implementation plan](implementation/issue-187-plan.md).
[Issue #189](https://github.com/gajo357/dsharplang/issues/189) adds empty-prefix
`List<T>.map<U>()` and `List<T>.fold<A>()`. Their result or accumulator type can
be supplied explicitly or established from a compatible expected callable or pipe
context. The list is captured once; callbacks and iteration run only when all
arguments are supplied. `map<U>` retains immutable-data requirements, and staged
`fold<A>` calls retain capability and ownership checks, including through generic
substitution. A call without sufficient type evidence is rejected unless an
existing applicable full extension supplies the call. Previously valid full
extension calls retain their behavior; compatible callable or pipe context can
select the intrinsic when the extension result is incompatible, and ambiguous
applicable extensions remain errors. Omitted returns can be inferred through
the statically proven immediate and staged calls described in #191; unproven
targets still require an annotation. Contextual empty extension calls are
described in #197; contextual bare extension values are described in #199.
Contextual bare collection method values are described in #201; other bare
method values and service/interface method partials remain separate work under
#129. See the
[D# example](../../examples/generic_list_empty_partials.ds) and
[implementation plan](implementation/issue-189-plan.md).
[Issue #43](https://github.com/gajo357/dsharplang/issues/43) adds interfaces and
explicitly constructed services, including multiple implemented interfaces,
signature conformance and direct polymorphic dispatch. Dependencies are supplied
at construction and remain private to each instance. Service/interface references
are execution capabilities: they cannot be hidden in immutable data, captured by
lambdas or retained by partial application. Application registration and
service/interface method partial application remain separate work.
See the [D# example](../../examples/interfaces-services.ds),
[adopted contract](implementation/issue-43-contract.md) and
[implementation plan](implementation/issue-43-plan.md).
[Issue #44](https://github.com/gajo357/dsharplang/issues/44) adds default
interface method bodies. A default can call another method on the same interface
and dispatches through the selected service; a matching service method overrides
it. Conflicting defaults require an explicit service method. A bodyless signature
also requires an explicit method, including when another implemented interface
provides a same-named default. See the [D# example](../../examples/default-interface-methods.ds)
and [implementation plan](implementation/issue-44-plan.md).
The first slice of [#45](https://github.com/gajo357/dsharplang/issues/45) adds
fresh, directly constructed D# resources with `using` declarations and braced
scopes. Cleanup runs once on tested exits, including async suspension, typed
errors, faults and cancellation. Following C#, a cleanup exception replaces any
in-flight outcome. Handles cannot escape; async operations are immediately awaited.
Resource dependencies follow service dependency restrictions, and existing
conservative return-inference rules apply. Provider-returned resources require
the factory rules described below; owned results have the restricted transfer
rules described below. See the [D# example](../../examples/scoped-resources.ds)
and [contract](implementation/issue-45-contract.md).
[Issue #170](https://github.com/gajo357/dsharplang/issues/170) adds directly
consumed owned-resource function factories, including async factories awaited
immediately by `using`. Factories and fresh resources cannot become ordinary
values, and factories containing `using` are rejected to avoid losing a resource
if cleanup interrupts transfer. See the
[D# example](../../examples/owned-resource-factories.ds) and
[implementation plan](implementation/issue-170-plan.md).
[Issue #172](https://github.com/gajo357/dsharplang/issues/172) extends owned
resource factories to interface and service methods, including supported default
methods. Immediate calls and awaited async calls retain checked dispatch and
receiver/argument evaluation order. Method values, ownership escapes and `using`
inside an owned-return body remain rejected. See the
[D# example](../../examples/owned-service-factories.ds) and
[implementation plan](implementation/issue-172-plan.md).
[Issue #174](https://github.com/gajo357/dsharplang/issues/174) supports immediate
transfer through the standard `Result<Resource,E>` from checked sync and async
function or service factories. `Success` transfers ownership to `using` through
`?`; `Error` propagates without acquiring a resource. These results cannot be
stored, copied, matched or passed as ordinary data. See the
[D# example](../../examples/owned-result-resources.ds) and
[implementation plan](implementation/issue-174-plan.md).
[Issue #42](https://github.com/gajo357/dsharplang/issues/42) adds async lambda
expressions and blocks. Their explicit `async` marker permits `await` in the
lambda itself, and the resulting function value returns a Task. Immutable
captures, contextual and body result inference, lambda-local returns and Result
propagation are checked. Builder captures remain rejected; builders created by
the lambda may survive its own await. Ordinary `List.map` does not implicitly
await callbacks, and task-valued lists remain unsupported by the existing
immutable-data rule, so map-of-tasks execution is not yet available. See the
[D# example](../../examples/async-lambdas.ds) and
[plan](implementation/issue-42-plan.md).
[Issue #41](https://github.com/gajo357/dsharplang/issues/41) adds explicitly
annotated async functions, task-valued calls and `await`. Async return inference
for ordinary functions and task storage in immutable data remain separate work;
awaits inside synchronous functions or lambdas are rejected. See the
[D# example](../../examples/async-functions.ds) and
[plan](implementation/issue-41-plan.md).
[Issue #40](https://github.com/gajo357/dsharplang/issues/40) adds local mutable
list and map builders. Only recognized operations and local aliases can use a
builder; returns, ordinary calls, data storage and lambda capture are rejected.
`freeze()` copies an independent immutable snapshot. Map keys follow the
existing structural-key rules. Builders may survive an await in their creating
invocation; borrowing remains separate work. See the [D# example](../../examples/local-builders.ds) and
[plan](implementation/issue-40-plan.md).
[Issue #39](https://github.com/gajo357/dsharplang/issues/39) adds sequential
`for/in` over immutable lists, including generic lists and nested loops. The
iteration binding follows the existing no-shadowing rule and is scoped to the
body. Empty lists do not execute it; a return exits the nearest function or
lambda. Map/Set iteration order remains separate work. See the
[D# example](../../examples/for-in.ds) and [plan](implementation/issue-39-plan.md).
[Issue #205](https://github.com/gajo357/dsharplang/issues/205) verifies `await`
inside the Unit body of an ordinary List `for` in an async context. Incomplete
operations suspend the current iteration before the next starts; failures and
cancellation stop later iterations. Tested paths include early return, Result
propagation, async lambdas and invocation-local builders across suspension.
This is sequential List iteration, not an async enumerable, `await foreach`,
or parallel iteration. See the
[D# example](../../examples/async_for.ds) and
[implementation plan](implementation/issue-205-plan.md).
[Issue #31](https://github.com/gajo357/dsharplang/issues/31) adds the standard
`Result<T,E>` union, `Success(value)`, `Error(error)` and `Success()` for Unit.
Nullable success values match directly as `Success(value)` or `Success(null)`;
both outcomes must be handled and a Result expression statement cannot be
discarded. Standard names are implicitly available after current-module names,
which retain precedence.
See [the example](../../examples/result.ds) and
[implementation plan](implementation/issue-31-plan.md).
[Issue #32](https://github.com/gajo357/dsharplang/issues/32) adds postfix `?`
for the standard Result. It evaluates the operand once, yields the Success
payload, or returns the same Error payload from the nearest enclosing function.
The enclosing function must return a non-nullable Result with the identical error
type; there is no automatic error conversion. Nullable, generic, Unit and Option
success payloads retain their types. Nullable values and user-defined types named
`Result` are not implicitly unwrapped. Lambda and async forms are implemented
in later slices described above.
See [the example](../../examples/result-propagation.ds) and
[implementation plan](implementation/issue-32-plan.md).
[Issue #33](https://github.com/gajo357/dsharplang/issues/33) adds function
types and capture-free lambdas with zero or more parameters, expression or block
bodies, and function-valued parameters, locals and returns. Arguments and results
are checked, including generic and nullable function types. Explicit return and
Result propagation leave the nearest lambda rather than its caller. Function
values cannot be stored in records or union payloads, including through generic
data constructors or helpers. Function-value equality is unsupported; nullable
functions may be compared with `null`. Async lambdas are described above. See
[the example](../../examples/capture-free-lambdas.ds) and
[implementation plan](implementation/issue-33-plan.md).
[Issue #34](https://github.com/gajo357/dsharplang/issues/34) adds capture of
immutable lexical data in returned and nested closures. Each creation retains
its invocation's values, including supported generic and nullable data; generated
capture fields are private and readonly. Function values are not data and cannot
be captured, including through generic calls. Mutable builders, resources and
capabilities remain outside this slice. See [the example](../../examples/immutable-closures.ds)
and [implementation plan](implementation/issue-34-plan.md).
[Issue #35](https://github.com/gajo357/dsharplang/issues/35) adds ordinary
value pipes and omitted return annotations for functions whose dependencies and
reachable exits satisfy the adopted inference rules. Pipes pass a value as one
argument; they do not implicitly unwrap Result values, await tasks or destructure
tuples. Recursive, indirect or unresolved inference and cyclic checked-type
dependencies require an annotation. Explicitly typed recursive functions remain
supported. See [the example](../../examples/pipes-inference.ds) and
[implementation plan](implementation/issue-35-plan.md).
[Issue #185](https://github.com/gajo357/dsharplang/issues/185) extends inference
for checked, acyclic immediate calls and pipes through statically resolved
`Map.set`, `List.fold` and supported extension partials. For fold, a callback
must be statically known as a function or lambda; unknown callable values remain
indirect. Inferred returns also require an annotation for reachable indirect
scoped-resource method dispatch, even when it resembles a collection method.
Recursive, ambiguous and unproven dependencies continue to be rejected. See the
[D# example](../../examples/partial_inference.ds) and
[implementation plan](implementation/issue-185-plan.md).
[Issue #191](https://github.com/gajo357/dsharplang/issues/191) extends omitted
return inference to checked, acyclic grouped calls and pipes through provably
intrinsic empty-prefix immutable List, Map and Set partials, including supported
generic `List.map` and `List.fold` and staged application. A callback supplied
to a completed operation must be a statically known function or lambda; unknown
callbacks, recursive or unresolved dependencies, and indirect service, interface
or resource dispatch still require an annotation. A possible full zero-argument
extension can also prevent inference, preserving normal extension precedence.
This does not make insufficiently typed generic empty prefixes valid or expand
other partial-application families. See the
[D# example](../../examples/empty_collection_inference.ds) and
[implementation plan](implementation/issue-191-plan.md).
[Issue #195](https://github.com/gajo357/dsharplang/issues/195) extends omitted
return inference through checked, acyclic conditional calls to provably intrinsic
immutable List, Map and Set methods and uniquely resolved visible extensions.
Supported conditional chains, generic and nullable values, Option results and
`??` retain their existing behavior. Inference follows possible receiver, argument,
callback and extension dependencies; unknown callbacks, indirect or unproven
dispatch, ambiguous targets and recursive dependencies still require annotations.
The conditional-call restrictions in #193 remain unchanged. See the
[D# example](../../examples/conditional_inference.ds) and
[implementation plan](implementation/issue-195-plan.md).
[Issue #207](https://github.com/gajo357/dsharplang/issues/207) extends omitted
return inference to checked, acyclic pipes through provably intrinsic bare List,
Map and Set methods, including supported staged calls. Invoked callbacks must be
statically known where their dependencies are needed. Unknown callbacks or method
values, ambiguous or unproven targets, service/interface or resource dispatch,
and recursive dependencies remain conservative. The existing collection method
and lifetime restrictions remain unchanged. See the
[D# example](../../examples/bare_collection_pipe_inference.ds) and
[implementation plan](implementation/issue-207-plan.md).
[Issue #135](https://github.com/gajo357/dsharplang/issues/135) adds positional
partial application of ordinary functions and function values. Supplied values
and dynamic receivers are evaluated once when a partial is formed; calls run
only when saturated. Pipes can supply the first remaining parameter. Further async
and service/interface/extension integrations remain tracked by #129. See the
[implementation plan](implementation/issue-135-plan.md).
[Issue #136](https://github.com/gajo357/dsharplang/issues/136) adds synchronous
callback-first Result helpers through the standard prelude. They use ordinary
calls and pipes, evaluate callback expressions eagerly, and invoke callbacks only
for the applicable outcome. `map` wraps rather than flattening nested results;
`tap` observers return Unit. See the
[implementation plan](implementation/issue-136-plan.md).
[Issue #219](https://github.com/gajo357/dsharplang/issues/219) adds the
task-input `map` form: a synchronous callback and `Task<Result<A, E>>` produce
`Task<Result<B, E>>`. Direct calls, pipes and typed callable contexts select the
task form when supported. The D# prelude helper awaits its input once and invokes
the callback only for success; errors, task faults and cancellation propagate.
Callback expressions retain ordinary eager evaluation, and the pipe does not
implicitly await or unwrap values. See the
[D# example](../../examples/async_result_map.ds) and
[implementation plan](implementation/issue-219-plan.md).
[Issue #221](https://github.com/gajo357/dsharplang/issues/221) adds the remaining
`andThen` combinations: the input may be `Result<A, E>` or `Task<Result<A, E>>`,
and the callback may return `Result<B, E>` or `Task<Result<B, E>>`. Async forms
return `Task<Result<B, E>>`. D# prelude helpers await the input and callback as
needed, invoke the callback only on success, and preserve typed errors, eager
callback expression evaluation, source order, faults and cancellation. Pipes do
not implicitly await or unwrap. See the
[D# example](../../examples/async_result_andthen.ds) and
[implementation plan](implementation/issue-221-plan.md).
[Issue #223](https://github.com/gajo357/dsharplang/issues/223) adds task-input
`mapError`, `tap` and `tapError` with synchronous callbacks. The D# prelude
awaits the source once and invokes only the callback applicable to the Result
case. `mapError` transforms the error; `tap` and `tapError` observe and
preserve the original outcome. Callback expressions evaluate eagerly in source
order, and exceptions and cancellation propagate without becoming errors. The
pipe does not implicitly await or unwrap, and nullable and Option payloads retain
their distinctions. Task-valued payloads inside Result remain unsupported, so
#130 stays open. See the
[D# example](../../examples/async_result_observers.ds) and
[implementation plan](implementation/issue-223-plan.md).
[Issue #36](https://github.com/gajo357/dsharplang/issues/36) adds immutable
tuple values and types, with checked, nested `let` destructuring. Tuple elements
are evaluated in order and a destructuring initializer is evaluated once. A
single tuple parameter stays distinct from multiple parameters; `()` remains
Unit. Tuple values can contain supported nullable and generic data and retain
structural equality. Function values remain excluded from stored data. See the
[example](../../examples/tuples.ds) and [implementation plan](implementation/issue-36-plan.md).
[Issue #213](https://github.com/gajo357/dsharplang/issues/213) supports local
`let` binding headers continued across lines inside matching parentheses,
including nested tuple patterns and tuple or function type annotations. A newline
after `let`, before `:` or `=`, or immediately after a completed closing
parenthesis remains invalid when it falls outside parentheses; comments preserve
these line boundaries. Initializer continuation and the end of a binding are
checked from the end of its header. The initializer is still evaluated once.
Other declaration headers are not included in this slice. See the
[D# example](../../examples/multiline_binding_headers.ds) and
[implementation plan](implementation/issue-213-plan.md).
[Issue #231](https://github.com/gajo357/dsharplang/issues/231) checks record
field and bodyless interface method boundaries against the end of a multiline
type. A following declaration must start on a later line; newlines inside
supported tuple, function and generic types remain valid. Comments preserve
the boundary. See the [D# example](../../examples/multiline_declarations.ds)
and [implementation plan](implementation/issue-231-plan.md).
[Issue #46](https://github.com/gajo357/dsharplang/issues/46) adds multi-file
module contributions and explicit, file-scoped imports. The compiler API and
CLI accept multiple sources while retaining single-file use. The
[D# example](../../examples/multifile/) demonstrates cross-file calls and
qualified names. Generated C# module-wrapper names are not a .NET interop
contract. General-purpose D# standard-library packaging remains separate work;
the synchronous and asynchronous Result helper slices use the embedded D# prelude.
[Issue #47](https://github.com/gajo357/dsharplang/issues/47) adds synchronous
and async extensions as statically resolved methods. The compiler searches the
current module and each file's direct imports; duplicate imports do not create
extra candidates, and transitive imports do not expose extensions. An applicable
real member wins; otherwise a unique extension with an exact receiver and
argument match is required, and ambiguity reports the candidate origins. The
receiver is evaluated once before the arguments. Extension targets cannot be
builders or resources, and existing capability and ownership restrictions apply.
Extensions cannot introduce fields or inheritance; generic extension methods
remain separate work. Contextual bare extension values are described in #199. See the
[D# example](../../examples/extensions.ds) and
[implementation plan](implementation/issue-47-plan.md).
[Issue #179](https://github.com/gajo357/dsharplang/issues/179) adds partial
application of a unique visible extension on an immutable-data receiver when at
least one positional argument is supplied and others remain. The receiver and
prefix are evaluated once, in order, at construction; the body runs on each
completed call. Expected callable types and pipes can resolve supported
overloads, direct-import visibility and applicable real-member precedence are
preserved, and async calls return tasks without starting the body early.
Capabilities, resources and builders cannot be captured. Contextual empty-prefix
extension calls are described in #197, and contextual bare extension values in
#199, and contextual bare collection method values in #201. Other method
values and service/interface method partials remain separate work. Tested pipes into statically resolved
extension partials can infer their enclosing return type, including names
overlapping built-in members;
unproven targets still require an annotation. See the
[D# example](../../examples/extension_partials.ds) and
[implementation plan](implementation/issue-179-plan.md).
[Issue #197](https://github.com/gajo357/dsharplang/issues/197) supports empty
argument prefixes for immutable-value extensions when an expected callable type,
pipe or immediate grouped invocation uniquely selects a compatible target. The
receiver is captured once; the extension body, including async work, runs only
when its remaining arguments are supplied. Applicable real members and previously
valid full zero-argument extension calls retain their precedence, including full
calls returning callable values. Direct-import visibility, ambiguity diagnostics,
and capability, resource and builder restrictions continue to apply. Untyped
empty extension partials remain unsupported; contextual bare extension values
are described in #199. Enclosing omitted-return functions may still require an
annotation. See the
[D# example](../../examples/extension_empty_partial.ds) and
[implementation plan](implementation/issue-197-plan.md).
[Issue #199](https://github.com/gajo357/dsharplang/issues/199) supports bare
immutable-value extension method values such as `let add: (int)->int = value.add`
when a non-nullable expected function type uniquely identifies a visible extension.
The receiver is captured once; the method body, including async work, runs only
when the value is invoked. Zero-argument methods are supported as `()->R` values.
Record fields and qualified union cases keep precedence. A compatible built-in
collection method value takes precedence as described in #201; an incompatible
same-name extension collision remains rejected conservatively. Direct-import visibility,
ambiguity diagnostics and capability, resource and builder restrictions remain
in force. An extension explicitly targeting a nullable immutable receiver
captures that receiver as-is; it does not unwrap null. Nullable field access and
selecting an extension on the non-nullable type still require explicit matching.
Bare extension-method pipes are described in #211. Uncontextualized bare methods
and service/interface method values remain unsupported; omitted enclosing returns
may still require an annotation. See the
[D# example](../../examples/extension_method_values.ds) and
[implementation plan](implementation/issue-199-plan.md).
[Issue #201](https://github.com/gajo357/dsharplang/issues/201) supports bare
immutable List, Map and Set method values when an expected non-nullable function
type exactly matches the operation. Supported List methods are `length`,
`append`, `get`, `filter`, `map` and `fold`; Map methods are `count`,
`get`, `contains` and `set`; Set methods are `count`, `contains` and
`add`. The expected function type supplies the generic `map` result or `fold`
accumulator type where supported. A receiver is captured once when the value is
formed; the operation and its callbacks run only on invocation. Nullable elements
and nested `Option` results retain their existing distinctions, and immutable
updates do not change the captured collection. Existing field/union precedence and
capability, resource and builder restrictions continue to apply. Uncontextualized
bare methods remain unsupported; supported collection-method pipes are described
in #203. An incompatible same-name extension does not replace a collection
method value. See the
[D# example](../../examples/collection_method_values.ds) and
[implementation plan](implementation/issue-201-plan.md).
[Issue #209](https://github.com/gajo357/dsharplang/issues/209) supports
immediately invoking a bare immutable collection or visible extension method,
for example `(items.get)(0)` or `(invoice.add)(2)`, when grouped argument types
and the expected result select a unique applicable target. Supported zero-argument
and staged calls use the existing method-value and partial-call rules. The receiver
is evaluated once before the grouped arguments, and the method body, including
async work, runs only when its remaining arguments are supplied. Record-field and
qualified-union precedence, built-in/extension resolution, import visibility,
ambiguity diagnostics, and capability, resource and builder restrictions remain
in force. Uncontextualized bare method values remain unsupported; bare
extension-method pipes are described in #211. Omitted returns may still require
an annotation. See the
[D# example](../../examples/grouped_method_values.ds) and
[implementation plan](implementation/issue-209-plan.md).
[Issue #203](https://github.com/gajo357/dsharplang/issues/203) permits a pipe to
supply the first argument to bare immutable List `get`, `append`, `filter`, `map`
and `fold`; Map `get`, `contains` and `set`; and Set `contains` and `add`. The
left value is evaluated once before the collection receiver; methods with
remaining arguments produce a partial function, and completed calls preserve the
existing immutable and nullable/Option behavior. List `map` needs a typed callback
or an expected result list type, while `fold` needs an accumulator type from the
input or the expected remaining callable. Zero-argument `length` and `count` are
not pipe targets. Ordinary field and union resolution,
existing capability, resource and builder restrictions, and conservative checks
for dynamic callbacks and recursive dependencies continue to apply. Bare
extension-method pipes are described in #211; uncontextualized bare methods
remain unsupported. Checked, acyclic omitted returns can be inferred in the proven cases described
in #207, while unproven targets still require an annotation. See the
[D# example](../../examples/collection_method_pipes.ds) and
[implementation plan](implementation/issue-203-plan.md).
[Issue #211](https://github.com/gajo357/dsharplang/issues/211) permits a pipe to
supply the first argument to a uniquely selected visible immutable extension
method, for example `fee |> invoice.add`. The flowing value is evaluated once
before the receiver; a method with remaining arguments produces a partial
function, and the body runs only when its arguments are supplied. Resolution
uses the pipe input and available output context while preserving field and
built-in precedence, import visibility, ambiguity diagnostics, and existing
capability, resource and builder restrictions. Async extension calls return tasks
without implicitly awaiting them. Untyped standalone method values remain
unsupported. See the
[D# example](../../examples/bare_extension_pipes.ds) and
[implementation plan](implementation/issue-211-plan.md).
[Issue #233](https://github.com/gajo357/dsharplang/issues/233) infers omitted
function returns through bare pipes to the only visible same-name extension on a
known immutable non-collection receiver, when it is synchronous and unary with
a known exact data input and data result. The analysis tracks the input,
receiver and selected extension;
recursive, ambiguous and indirect dependencies remain conservative. Collections,
overloaded extension families, nullable input conversions, staged applications,
callbacks and async extensions are outside this inference slice. See the
[D# example](../../examples/bare_extension_pipe_inference.ds) and
[implementation plan](implementation/issue-233-plan.md).
[Issue #235](https://github.com/gajo357/dsharplang/issues/235) extends that
inference to statically known non-nullable immutable List, Map and Set receivers
when the method name does not collide with a built-in collection operation.
The existing exact-input, unique-target, dependency and lifetime checks remain
in force. See the [D# example](../../examples/collection_extension_pipe_inference.ds)
and [implementation plan](implementation/issue-235-plan.md).
[Issue #237](https://github.com/gajo357/dsharplang/issues/237) extends that
inference to a proven unary async extension with an explicitly declared
immutable data result. An ordinary function can infer and return its `Task`
without implicitly awaiting it. Existing dependency, collection precedence and
task-storage restrictions remain in force. See the
[D# example](../../examples/async_extension_pipe_inference.ds) and
[implementation plan](implementation/issue-237-plan.md).
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
