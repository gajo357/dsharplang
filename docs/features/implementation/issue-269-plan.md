# Issue #269 — targeted Invoke at a scoped host entry

Issue: https://github.com/gajo357/dsharplang/issues/269
Parent: https://github.com/gajo357/dsharplang/issues/241
Base: `33724ec0d9045c7ce19992dad15b39fc8a6faf98` (`origin/main`).

## Scope and decisions

Implement one directly awaited targeted Invoke in supported explicit async
top-level function bodies. #267 supplies the `DSharp.Runtime.IActorDispatcher`
ABI; #51 supplies checked Handle contracts. Keep #241 and #52 open. Dispatch,
receipts in D#, production runtime, durable acceptance, registration and broad
context propagation are out of scope. The operation must use a host-provided
dispatcher, never a static process-global instance or direct actor body call.

Recognize contextual `await Invoke<Actor>(message)` only after ordinary lexical
lookup: an existing local or function named Invoke retains its meaning. Resolve
the selected actor by its source-scope nominal identity, require the argument
to match its Handle input exactly, and derive the result from Handle. Targetless
Invoke, bad targets, mismatches and unobserved awaitables are errors. Restrict
effectful Invoke to directly awaited expressions in async top-level entry
functions. Until transitive context propagation exists, reject calling or
capturing those entries from D# and reject Invoke in actors, helpers or lambdas.
Retain existing entry signatures for functions that do not contain Invoke.

Emit one host-scoped `ActorRoute<TInput,TResult>` descriptor per canonical public
actor identity, shared across entry points. Route keys identify the source
module and actor for the host; they do not promise durable identity or schema
evolution. The effectful generated C# entry receives an explicit
`DSharp.Runtime.IActorDispatcher` argument and calls its generic Invoke once;
normal await yields the declared result, including generated Unit and business
Result. Argument evaluation stays once-only. Two hosts may supply different
dispatcher instances concurrently. Do not construct actors, use reflection,
invent a result type or claim any production execution behavior.

For CLI compilation with this feature, use the explicit environment variable
`DSHARP_RUNTIME_ASSEMBLY` naming the actual `DSharp.Runtime.dll`: validate the
file before output creation, copy it into the generated output directory, and
emit a project reference to that copied assembly. A missing or invalid path
must fail clearly without reporting a successful build. The ordinary no-Invoke
CLI project remains byte-for-byte unchanged. `check` needs no runtime assembly.
This avoids changing trusted project manifests; generated consumers supply the
same ABI assembly as the host. Compiler results should explicitly indicate
whether the runtime reference is needed, not infer it from generated text.

## Files and implementation steps

1. In `src/DSharp.Compiler/Compiler.cs` and `MultiFileResolver.cs`, recognize
   and bind the contextual operation, preserve actor identity and enforce the
   safe entry/call/escape restrictions with source diagnostics. Emit typed
   route descriptors, scoped dispatcher parameter and call. Preserve prior
   behavior and source names; do not modify existing tests.
2. In `src/DSharp.Cli/Program.cs`, use the compiler result's feature flag to
   validate and copy the explicit runtime assembly and reference it only for
   affected generated projects. Do not change project manifests or dependencies.
3. Add new `tests/features/ActorInvokeEntryTests.cs` and a D# example. Execute
   generated code against a recording implementation of the actual runtime ABI
   for value, Unit and business-error results, once-only evaluation, route
   identity and two interleaved host contexts. Test invalid targets/arguments,
   unobserved calls, shadowing, scope rejection, CLI check/compile and missing
   assembly input. Test nominal identities and imports where supported.
4. Update implementation status only for tested behavior. Run bootstrap, full
   verifier, standalone compiler and runtime verifiers, focused tests, CLI
   checks and `git diff --check`. Record actual outcomes, local image limitations
   and exact-head hosted evidence. Preserve existing tests and verification gates.

## Acceptance-to-test map

| Criterion | Evidence |
| --- | --- |
| One typed await and result | Executed generated value, Unit and business-error results against the real ABI with one recording call; no direct actor body call. |
| Correct scoping and route | Interleaved independent host dispatchers and same route identity across entries; once-only argument evaluation and multi-module identity. |
| Reject unsafe forms | Invalid target/input, targetless and unobserved operation, direct actor access, unsupported actor/helper/lambda/call paths, shadowed ordinary Invoke. |
| Deployable generated project | CLI check and compile with explicit runtime assembly, missing input fails; ordinary CLI output unchanged. |

## Results

- Bootstrap passed; documentation validation passed for 123 files and 308
  local links, and all 31 automation tests passed. The full local verifier
  could not pull the pinned MCR SDK image (`Forbidden`), so isolated compiler
  and runtime verification requires exact-head hosted evidence.
- The unchanged compiler verifier passed 18 baseline and 397 feature tests in
  a byte-checked short snapshot. The feature TRX was 511,162 bytes, below the
  unchanged 512,000-byte guard. Runtime verification passed 2 baseline and 5
  feature tests. The snapshot preceded a CLI error-handling correction and extra
  negative cases; the final focused Invoke integration test passed afterward.
- The focused generated-host test executed value, Unit and business-error
  results, once-only message creation, route identity and separate interleaved
  hosts. It also exercised CLI check and compilation with the actual runtime
  ABI, clear missing/invalid assembly failures, ordinary output and the scoped
  negative cases. Recording dispatchers do not prove durable acceptance.
