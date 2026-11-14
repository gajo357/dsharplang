# DEC-06: Safe CLR and NuGet interoperability

**Status:** Draft proposal for review; not adopted or implemented.

**Issue:** [#16](https://github.com/gajo357/dsharplang/issues/16).

**Base:** `f22efb53fdedbb17339d798d61c9d25577497cd8` (the merged [HTTP proposal](DEC-05.md)); the compiler bootstrap is recorded in [COMPILER_BOOTSTRAP.md](../../COMPILER_BOOTSTRAP.md).

## Work plan and scope

Reconcile the existing immutable-value, option, resource and package boundaries;
propose a deliberately small, explicit C#/F# adapter surface, ownership and
lifetime rules, build resolution, and checks for unsafe or unsupported cases.
The only changed file is this decision proposal. The following sections and test
matrix cover issue #16's four acceptance criteria. No compiler, package, runtime,
dependency or verifier changes are included.

This is a **proposal for review**, not an implemented API or an adopted contract.
Merging the proposal does not adopt it. A maintainer must explicitly adopt the
reviewed version before dependent implementation proceeds. D# adapter names and
code below are illustrative, not existing syntax or compiler support. The exact
package declaration, adapter registration and generated C# API remain to be
reviewed; this document specifies the first supported behaviors, not a package
manifest grammar. [DEC-01](DEC-01.md) was subsequently adopted; [DEC-03](DEC-03.md),
[DEC-04](DEC-04.md), and [DEC-05](DEC-05.md) remain separately gated proposals.
This document does not adopt or depend on their provisional representations.

The settled requirements are that D# data is immutable, local builders do not
cross foreign calls, and agents retain their isolation and message restrictions.
`T?` means null or a value and is idempotent; the tagged `Option<T>` has distinct
`None` and `Some(T)` cases, including `Some(null)` only when `T` admits null and
without flattening nested options. No implicit conversion may erase these
distinctions. Those requirements and their implementation status are described
in the [implementation instructions](../DSharp_Implementation_Instructions.md)
and [syntax reference](../DSharp_Syntax_Reference.md). Generated C# details do
not themselves define D# semantics. Serialization formats, resource lifetimes,
generic constraints and agent protocols remain subject to their own decisions.

## Proposed explicit boundary and trust model

A project explicitly references a pinned package and explicitly selects an
adapter for each exposed foreign type or operation. A successful NuGet restore,
CLR signature, import or generated C# compilation is not proof an API is safe.
The adapter contract lists the exact assembly/type/member, closed generic
arguments, input and output mappings, nullability, declared failure mapping,
lifetime, and whether the call may run asynchronously or retain callbacks. A
missing, ambiguous, open-generic or unsupported mapping fails with a diagnostic;
it is not exposed by reflection or a guessed conversion. Importing a package
never registers a handler, starts an actor, opens a resource or executes code for
editor metadata. Metadata inspection must be bounded and must not load or run
package code. Type names and adapter syntax are separate design choices.

The first slice permits explicitly reviewed public static methods and public
constructors/instance methods through approved adapters with supported argument
and result shapes, plus explicitly selected read-only properties as operations.
Generic APIs are allowed only when the complete closed signature and every
recursive mapping are supported; reject open, unconstrained or unsupported
instantiations. A property getter is executable foreign code, not a D# immutable
field. Do not automatically expose setters, indexers, events, operators,
reflection, dynamic dispatch, `object`/arbitrary foreign references, pointers,
`ref`/`out`/`in`, ref-like values, mutable spans, unsafe or unmanaged callbacks.
This is a conservative initial subset, not a claim that these APIs are
intrinsically unsafe in every later adapter. Overload selection must identify a
single supported member after type checking; no CLR overload or nullability
heuristic silently widens the selected contract.

Foreign managed code executes with its host process's authority. This proposal
is **not a sandbox**, does not make a malicious package harmless, and cannot
prove immutability or absence of effects merely from CLR metadata. The host and
application owner must trust and review the package, adapters and configured
sources. Adapters enforce D# value/lifetime boundaries for supported code, but
cannot prevent arbitrary trusted CLR code from using process resources, static
state or threads. No permission claim is implied by calling such code. Use a
separately designed isolation boundary for untrusted code.

## Proposed values, nulls, errors and ownership

Eligible primitive values are the currently supported D# scalar types with an
explicit exact CLR mapping, `Unit`, and recursively mapped immutable D# records,
unions and tuples when an adapter provides a complete representation. No
additional DEC-01 type is implemented by this proposal. A checked conversion must
reject overflow or unsupported values, not truncate or fall back to `object`.
Declared non-null references are checked on both sides: an unexpected foreign
null is a boundary failure, not `default(T)`, `None`, or success. A foreign null
in an explicitly nullable reference/value mapping becomes D# `T?` null; a
non-null payload is converted to `T`. Nullability annotations inform adapter
review, but metadata or C# generic `T?` is not trusted as enforcement. In
particular, a D# unconstrained nullable value type must retain real nullability
after generic instantiation, including `int`, rather than becoming `0`.

Adapters map a named, reviewed set of foreign exceptions to declared tagged
D# error cases through an explicit result-returning boundary. An unmapped
exception remains a fault for the surrounding host/supervisor, with diagnostic
identity and cause retained; never return a success or silently coerce every
exception to a single domain error. Synchronous throws before a task is returned
and asynchronous task faults follow the same specified mapping; cancellation is
handled separately below. Fatal runtime failures are not promised recoverable.
Avoid exposing exception strings, stack traces or secrets as public responses;
that does not prevent preserving them in appropriately protected diagnostics.
No automatic retry or rollback is implied.

Foreign arrays, lists, dictionaries, mutable DTOs and their nested objects are
never reclassified as immutable because the outer reference is read-only. The
first approved data adapters make independent recursive snapshots in both
directions, including nested mutable elements; after conversion neither side can
mutate the other's D# value. Preserve element order and declared duplicate-key
behavior; reject unsupported cyclic graphs, repeated mutable aliases when the
mapping cannot preserve its documented semantics, concurrent mutation during
snapshot and unsupported collection shapes instead of returning a partial
value. Adapters define finite depth and size limits and fail rather than return
a truncated copy. An adapter must establish stable ownership or synchronization before
copying; a check alone cannot make a concurrent foreign mutation safe. A foreign
object with no complete, safe copy contract is not eligible as D# immutable data
or an agent/queue message.

A reviewed service/resource adapter may instead keep a foreign reference as a
host-owned opaque capability. It exposes only declared operations, is never
stored as immutable message data, and observes the owning scope, host and
concurrency policy. It cannot be turned into a public record to bypass checks.
No foreign reference to a local builder, its view or backing storage may pass a
foreign call; `freeze()` first creates an independent immutable snapshot. Scoped
connections, transactions and resource handles likewise cannot escape in foreign
objects, callbacks, tasks or data. These rules also apply through generics and
nested collections. A trusted adapter cannot claim to permit a captured scoped
value merely by labeling an API safe.

## Proposed tasks, cancellation, disposal and callbacks

An explicitly selected `Task<T>` adapter maps completion once to the declared
D# async result after applying the value and error mapping above; a void-returning
`Task` maps to `Unit`. Foreign `ValueTask`, arbitrary awaitables, async streams,
custom schedulers and fire-and-forget methods are excluded from the first slice.
If a foreign method throws before returning its Task, handle it at the same
boundary as a faulted Task. A null Task is an interop fault, not a completed
operation. A foreign task must not retain D# local builders or scoped resources;
the adapter cannot pass them in the first place. Task completion is not proof
that external effects can be rolled back.

Cancellation is cooperative. When an explicitly selected operation accepts a
`CancellationToken`, its adapter supplies the owning invocation's token; it does
not fabricate a cancelled result for an ordinary error. A cancellation exception
associated with the signalled owning token maps to cancellation, not a domain
`Error`. A cancelled Task alone does not establish token provenance: an unrelated
or unsignalled cancellation exception is a fault unless an explicitly reviewed
adapter contract establishes the operation's cancellation provenance and the
owning token is signalled. A mismatched token is never treated as the owning
invocation's cancellation merely because it was observed while awaiting a Task.
The host still observes the final task outcome and owns any
resource until the foreign operation has actually stopped; a cancellation request
alone does not prove that work or side effects stopped. There is no implicit
retry, timeout, transaction rollback or exactly-once claim.

A resource returned by a selected `IDisposable`/`IAsyncDisposable` factory may
only be used inside a host-controlled bounded scope, never as message data. The
adapter records exclusive/shared ownership and prevents use after scope exit.
Release exactly once after all owned work completes on success, mapped failure,
exception and cooperative cancellation; await asynchronous disposal. If cleanup
fails while another failure is active, preserve the primary failure and report
the cleanup fault separately. A first disposal fault is surfaced as a fault, not
ignored; do not let a cancelled operation's token prevent cleanup. If a foreign
operation will not terminate, bounded shutdown and escalation remain host policy,
not proof of disposal or forced cancellation. Exact startup/shutdown policy
belongs to the separately gated resource decision.

Initially accept only explicit delegate adapters with immutable, independently
owned captures and a lifetime the host can enforce. A synchronous callback may be
invoked only during its declared foreign call; after return it is invalid. For an
explicit asynchronous callback contract, its registration is owned by a bounded
scope, and disposal prevents new callback entry and waits for in-flight callbacks
before releasing captures. A late invocation is rejected without executing D#
code; invocation, threading, reentrancy and exception behavior are declared per
adapter. Foreign code may retain a CLR delegate reference, so the host-owned
trampoline checks scope validity at every invocation. Local builders, scoped
resources, transaction handles and mutable foreign references cannot be captured
or transferred into a callback. If invalidation or draining cannot be enforced,
reject the callback API. Disposal requested from inside an in-flight callback
must not synchronously wait for itself; defer draining until that callback exits.
No unmanaged callback, implicit background task or
unbounded event subscription is admitted by this first subset.

## Proposed NuGet resolution and build boundary

The first supported input names explicit package IDs and exact versions; there
are no floating versions or version ranges in the declared roots. Restore resolves
transitive versions once into a checked-in lock recording exact dependency graph,
content hashes, target framework and relevant source identity. Repeated builds use
locked restore and verify the graph and package content hashes; a changed graph,
missing lock, hash mismatch, unsupported target framework or unavailable locked
package produces a deterministic failure rather than silently updating or falling
back to another version. Intentional updates require an explicit lock regeneration
and review. Credentials and machine-local absolute paths must not be committed;
source mapping and authentication configuration are controlled by the host.
Hashes establish repeatable bytes, not package trust or vulnerability status.
The exact file/CLI syntax, source configuration and diagnostics remain open for
[#87](https://github.com/gajo357/dsharplang/issues/87).

Target the repository's pinned managed SDK and target framework. The proposed
[.NET 11 upgrade](https://github.com/gajo357/dsharplang/issues/151) changes that
baseline; select only NuGet-compatible managed assets for the actual pinned
target. A package targeting another
framework is accepted only when the SDK's compatibility rules select a usable
managed asset, not merely because restore reports success. Diagnose incompatible
frameworks and unsupported required runtime assets before generated code is run.
The initial slice excludes native or platform-specific runtime assets required by
the selected path, native libraries/PInvoke, COM, source generators, analyzers,
MSBuild tasks, package-supplied build/buildTransitive props and targets, package
tools and install scripts. A non-executing staged inspection of package contents
and dependency metadata must reject forbidden assets before any generated NuGet
props or targets can be imported. Restore and build run in a trusted configuration
that prevents package-provided imports and hooks from executing, including on a
rejected package path; if that cannot be enforced, fail before build. No package
hook is executed during inspection, restore or build as part of this contract.
Fail closed when required support cannot be established; do not silently
omit an asset required for correct execution. Restoring packages and running the
SDK still uses trusted host tools and feeds; this is not a claim that external
build infrastructure is a sandbox. Trimming and Native AOT remain deferred.
Ordinary runtime libraries with unsafe behavior are still trusted code, not proven
safe because build hooks are excluded. No transitive package automatically adds
an application route, runtime registration or editor-executed plugin.

## Proposed F# adapters

F# support is explicit per selected, closed, supported API; the package being
written in F# does not authorize automatic adaptation of arbitrary F# types. Use
F# case-aware adapter logic against the selected FSharp.Core version rather than
inferring a union tag from raw CLR nulls, reference equality or reflection layout.
Adapters must validate the whole declared F# shape and payload mapping, including
null representation and nested types. Both directions preserve each case or
fail; unsupported union shapes are rejected before use.

| F# value | Proposed D# result for explicitly selected mapping |
| --- | --- |
| `option<string>` `None` | `Option<string?>` `None` |
| `option<string>` `Some "Alice"` | `Option<string?>` `Some("Alice")` |
| `option<string>` `Some null` | `Option<string?>` `Some(null)` |
| `option<option<int>>` `None` | `Option<Option<int>>` `None` |
| `option<option<int>>` `Some None` | `Option<Option<int>>` `Some(None)` |
| `option<option<int>>` `Some (Some 42)` | `Option<Option<int>>` `Some(Some(42))` |

For `option<string>` whose payload contract is not proven non-null, use nullable
D# payload `Option<string?>`; `Option<string>` is eligible only when its adapter
checks and rejects a `Some null` payload as a boundary failure. Ordinary F#
`None` may itself use a null runtime representation; this is a union case and
must not become a D# nullable null. Conversely, a CLR reference returned as
plain nullable `string` maps only to `string?`, not implicitly to an option.
Adapters must not collapse `None`, `Some(null)` or nested options, nor invent a
`Some(default(T))` when the payload is absent. Conversion in either direction
must preserve the tagged distinction. The first subset supports F# `option<T>`;
`voption<T>` is excluded until separately selected and tested. F# nullable
values require their own selected nullable mapping, not option conversion. If an
additional nullable outer option can collapse into the same CLR null used for
`None`, reject that boundary unless an explicit wrapper preserves both states;
do not infer a distinction from the single null representation.

Selected, closed F# discriminated unions map through a declared one-to-one
case/payload mapping to an exhaustive D# union; unknown cases, unsupported payloads
or invalid nulls fail rather than silently map to a default. This includes unions
whose runtime representation uses null for a particular case. Do not derive a
public wire encoding or permit unreviewed recursive graphs through this mapping.
A selected F# function value maps to an explicit D# callable only when its entire
argument/result conversion, curried invocation, effects and lifetime are declared;
returned functions must not capture a local builder or scoped resource and must
not outlive their owned capability. In the opposite direction a D# callable uses
the bounded delegate rules above. F# `Async<T>` is not a `Task<T>` by identity:
a selected adapter starts it once under the invocation's declared cancellation
scope, awaits completion and applies the same mapping for results, cancellation
and faults as a selected task. Creating a computation alone does not start it;
no implicit retry or repeated execution is permitted. Additional F# computation
expressions, arbitrary active patterns, quotations and provider-generated types
remain outside this subset.

## Illustrative D# use after adoption

The foreign adapter, package selection and mappings below are descriptive
placeholders, not accepted D# declarations or an implemented library. Given a
reviewed adapter `findName` that converts F# `option<string>` to
`Option<string?>` and maps its selected exceptions to `LookupError`, ordinary D#
code can keep all three states explicit:

```text
fn describeName(name: Option<string?>): string {
    match name {
        None => "not supplied"
        Some(null) => "clear name"
        Some(value) => value
    }
}

fn describeLookup(id: int): Result<string, LookupError> {
    match findName(id) {
        Success(name) => Success(describeName(name))
        Error(error) => Error(error)
    }
}
```

The adapter must distinguish the three F# cases before invoking this code; the
example does not grant permission to map the option to `string?`, turn `None`
into `null`, or send a live foreign reference as immutable data.

## Acceptance evidence required after adoption

The following is a test plan, not evidence of implemented behavior. Execute D#
source and generated code through trusted tests where applicable; use pinned
managed C# and F# fixture packages, deterministic barriers for asynchronous races,
and real restore in an isolated test feed. Setup and trusted tests must be
explicitly enabled by their own scoped issues first.

1. **AC1 — signatures, nulls, failures, ownership:** execute supported closed
   constructors/methods and read-only getters; reject unsupported overloads,
   arbitrary CLR objects, `ref`/`out`, open generics, and null into non-nullable
   payloads. Test value/reference generic nullable substitution, nested data,
   mutable array/list/DTO copy in both directions, mutation after conversion,
   cyclic/unsupported graphs and claimed concurrency. Verify mapped exceptions,
   unmapped faults, and diagnostic preservation, with no default-on-null.
2. **AC2 — lifetime:** exercise sync throws, successful/faulted/cancelled Tasks,
   matching, mismatched, unrelated and unsignalled cancellation, cancelled Tasks
   with unknown provenance, cancellation before and during work,
   null Task, one disposal on every exit, asynchronous cleanup fault plus primary
   fault, and late completion. Attempt builder/resource escape via args, nested
   data, captured and returned functions, and foreign callbacks. Use barriers to
   verify callback invalidation, in-flight draining and rejection of calls after
   scope exit; cancellation alone must not release a resource still in use.
3. **AC3 — package/build:** restore twice with a pinned pure-managed package and
   transitive graph from a controlled feed; reject changed/missing locks, altered
   package content, floating roots, unsupported frameworks and required native or
   build-time assets. Prove unsupported analyzer, generator, props/targets and
   MSBuild-task fixtures cannot execute, including while a package is rejected;
   metadata inspection cannot execute package code. Verify dependency update is
   explicit and lock diffs are reviewable.
4. **AC4 — F# values:** test F# `None`, `Some(null)`, `Some(value)`, nested
   options in both directions, rejection of null in `Option<string>`, true
   nullable values separately, union cases including null-represented cases,
   unknown/unsupported payloads, returned/retained functions and async success,
   synchronous/asynchronous faults and cancellation. Each case and payload must
   remain distinguishable; never implicitly convert to nullable or flatten.

## Review and adoption gate

Review the selected signature/exception subset, nullable and F# case mapping,
mutable snapshots versus scoped capabilities, callback and cancellation lifetime,
and locked restore/build asset exclusions. Concrete public syntax, package lock
format, isolation mechanism and precise host shutdown policy need separate review;
no issue depending on this decision is implemented here. After explicit adoption,
[#87](https://github.com/gajo357/dsharplang/issues/87),
[#88](https://github.com/gajo357/dsharplang/issues/88),
[#89](https://github.com/gajo357/dsharplang/issues/89) and
[#90](https://github.com/gajo357/dsharplang/issues/90) remain separately gated
by setup and their other prerequisites. A documentation check or merge does not
adopt this proposal or demonstrate safe foreign execution.

## Verification of this proposal

Bootstrap, documentation and link checks, and compiler verification passed
(18 baseline and 362 feature tests). All 23 automation tests passed. The local
isolated verifier could not download its pinned Docker image (`Forbidden`);
GitHub Actions runs the unchanged verifier. These checks validate the repository,
not foreign execution or adoption.
