# DEC-05: HTTP composition and authorization contract

**Status:** Draft proposal for review; not adopted or implemented.

**Issue:** [#15](https://github.com/gajo357/dsharplang/issues/15).

**Base:** `0041139ab2e2c4fc7660ac9f33fb291f7f1efe53`.

## Work plan

Reconcile the existing HTTP, composition, authorization and logging direction;
propose typed binding, explicit mappings and wrapping/cleanup contracts with D#
illustrations and adversarial tests. Separate settled choices from proposals and
identify unresolved gates. This design-only slice changes this document and no
compiler or runtime behavior.

## Scope and existing decisions

This is a **proposal for review**, not adoption, an implemented API, or permission
for dependent implementation. Merging it does not adopt it. A maintainer must
explicitly adopt a reviewed version before #83/#84 or other work depending on
this contract implements it. Names and signatures for HTTP, authentication,
authorization, binding, wrappers and mappings below are provisional. The D#
examples are illustrative target code, not tested current compiler support.

The [syntax reference](../DSharp_Syntax_Reference.md) §16 and
[pipes and logging specification](../DSharp_Pipes_and_Logging.md) §§4–8 settle
these constraints:

- An explicitly registered HTTP agent declares its route; `pipeline = expression`
  holds one ordinary typed callable. There is no `pipeline {}` step list. The host
  invokes preprocessing once, invokes the handler only on success, and otherwise
  directly binds the handler input when no preprocessing is declared.
- `|>` is ordinary value piping. `andThen` composes Result-returning steps with
  a common error type; different errors need explicit `mapError`. Its supported
  synchronous and asynchronous combinations preserve typed errors and invoke a
  subsequent callback at most once. Exceptions and cancellation propagate; Result
  helpers do not silently catch, flatten or retry them.
- The handler receives precisely the successful pipeline output type. HTTP
  error mappings are explicit. Wrapping middleware is distinct from preprocessing
  and must clean up on failure and cancellation.
- A public record named `Authorized` is an envelope, not proof of permission.
  Runtime authentication and authorization checks enforce access; sensitive
  operations enforce their own policy. Logging context is diagnostic, never
  authority.

The [worker](DEC-03.md) and [queue](DEC-04.md) proposals are independently
unadopted; this document does not adopt their contracts or defaults. Route and
query syntax, HTTP encoding, response customization, evidence representation and
wrapper APIs need review here or in their separately gated work.

## Proposed transport and typed composition

`HttpRequest`, `RouteValues`, `QueryValues`, `Headers`, `HttpResponse`, and the
error names below describe contracts, not finalized D# library declarations.
A host supplies one request per matched method and route with path parameters,
query values, headers, body stream, cancellation signal and request identity.
The request identity identifies an invocation, not an authenticated principal;
credential material in the request is untrusted until a trusted adapter verifies it.
Route and query collections retain repeated values; decoding must state whether a
field is required, optional or repeated and reject missing, duplicate or malformed
values when the declared binding does not permit them. Path matching and percent
and character decoding must reject invalid or ambiguous encodings rather than
silently reinterpret them. Headers and body are untrusted input. Set explicit
body size and supported content-type policies; enforce limits even for streamed
or missing-length bodies. JSON field presence, null, `Option<T>` and nested
options have no wire encoding selected by this proposal. An adapter may only
support them after a separate explicit encoding is specified; it must not
collapse missing, null, `None` or `Some(null)` by assumption.

Binding declarations identify the source of each field (path, query, header or
body); equal names in different sources do not overwrite one another or acquire
an implicit precedence. An absent field differs from a present null whenever a
separately approved decoder supports both. The route registry rejects ambiguous
overlapping dynamic routes before listening, rather than dispatching according to
registration order. Response selection must not disclose protected resource
details before the applicable policy check; exact route specificity, decoder
syntax and detailed public error bodies remain review decisions.

A binding/decoder is a typed function from the request or an intermediate value
to `Result<T, E>` or `Task<Result<T, E>>`. A route declares a binding to the
handler's exact input type. With preprocessing, the host supplies its declared
raw request input, awaits its result once and supplies only `Success(T)` to a
handler whose parameter is exactly `T`; it never invents a default value for
failure. Without preprocessing, a declared direct binder supplies exactly the
handler input under the same error/mapping rules. Registration rejects a missing
binder, incompatible type, unsupported binding, conflicting method/route, or
missing policy before accepting traffic; report statically knowable errors at
compile time. Matching a route does not itself authenticate or authorize it.
Registration is explicit, and importing a package does not enable a route.

Each decoding, authentication, authorization and application error is retained
as a distinct tagged case in an explicitly chosen endpoint error union. Adapt a
step's `Result<A, SpecificError>` with `mapError` into that union **before**
`andThen`; avoid stringification, a catch-all universal error, implicit
conversion or treating `Error` as a successful payload. The same rules apply to
the handler's result: preserve its declared error until its explicit response
mapping. Each supported failure case must map to a response; registration rejects
an absent or ambiguous mapping. Separate exception and cancellation handling from
typed errors. An exception is not an application `Error`, and cancellation is
not an authentication or validation failure.

## Proposed responses and failure boundaries

An endpoint explicitly maps each successful handler result and each declared
pipeline/handler error case to an `HttpResponse` with a legal status, headers and
body encoder. No mapping silently returns `default(T)`, relies on exception
text, or turns an unmapped case into success. The following are proposed defaults
for the *named transport failure categories*, not inferred meanings for arbitrary
application errors; application cases require explicit mappings and the API
shape remains open:

| Condition | Proposed response before headers commit |
| --- | --- |
| Missing or invalid authentication | `401` with the applicable authentication challenge |
| Authenticated principal denied permission | `403` |
| Malformed path/query/body or failed declared input validation | `400` |
| Body exceeds configured limit | `413` |
| Unsupported request content type | `415` |
| Unexpected fault | Sanitized `500`; record diagnostics without exposing secrets |

An unavailable or faulty authentication provider is a server failure, not proof
of invalid credentials and never a reason to grant access. Its exact availability
status mapping needs explicit configuration and review; it must not be reported
as `401` merely because the provider failed.

An application can deliberately map its own cases, such as a known missing
resource to `404` or a conflict to `409`; it cannot use mapping to bypass the
runtime authorization guard. A handler with no representation may explicitly
return `204`, with no body. Validate illegal status/body combinations, headers
and encoders before commit when possible. HEAD response handling must not write a
body. A 401 challenge must match the configured authentication scheme; this
proposal does not choose an identity provider, credential format or challenge.

The host writes at most one response. A typed failure or unexpected fault before
commit can be mapped as above. Once headers or body are committed, no second
status or replacement response can be sent: terminate/abort the response according
to the transport and report the fault to supervision. Client disconnect or
request cancellation signals cooperative cancellation and normally aborts rather
than inventing a response. Cancellation does not guarantee that previously
performed external effects were rolled back. If the transport is still writable
and a separately adopted timeout policy wants a response, it must specify that
case explicitly; this proposal does not select a timeout status or deadline.

## Proposed authentication and authorization evidence

Authentication verifies credentials through a configured trusted adapter and
establishes a principal or fails. Authorization checks a concrete action and,
where applicable, resource and tenant against policy for that principal.
Authorization may require loading the resource first; ensure loading does not
expose protected data. These are distinct checks. The ordinary
`Authorized<T, Permission>` envelope can carry decoded body and identity for
convenience, but anyone can construct or copy such a public record. Its fields,
type name, a log scope, caller-supplied claims or a fabricated principal are never
accepted as proof.

Proposed proof is a runtime-owned opaque capability issued only after successful
trusted checks, backed by host-private state or an equivalent unforgeable
runtime validation mechanism, not by the visibility of a D# record or by a
caller-controlled boolean. Its validation binds the issuing host, active request
invocation, verified principal, tenant, action and applicable resource, policy
and validity period. The host validates it immediately before dispatch to a
protected handler; sensitive service operations independently validate authority
for their actual action and resource, including current policy/revocation as
required by that operation. A request check for one resource must not authorize
another; a check on a resource before a change must not automatically authorize a
later different operation. Revalidate at the protected operation's boundary to
avoid using stale checks across async work; systems that need atomicity with a
resource update must arrange it at that boundary. The host denies invalid,
expired, revoked, wrong-host, wrong-request, wrong-principal, wrong-tenant,
wrong-action and wrong-resource capabilities. It never treats an inability to
validate as permission.

Capability data must not be serialized into HTTP input, reconstructed from
public values, or derived from Logger context. Copying an opaque reference within
the same active invocation does not create additional authority; it remains
subject to the same validation and expires when that invocation ends. It cannot
be replayed into another invocation or host. The runtime controls its issuance,
validation and lifetime even when all user declarations are public. This is a
proposed runtime boundary, not a claim that D# visibility alone prevents hostile
code or that an in-process language sandbox exists. Trusted host/adapters and
security of their implementation remain required; exact storage, revocation,
policy and public API need explicit review.

Queue and scheduled entry points have their own authorization policies; an HTTP
principal or capability stored with a message is not permanent authority and
must not be replayed as a live HTTP capability. Passing a diagnostic correlation
identifier across such a boundary does not authenticate the later invocation.

## Proposed wrapping, async and context

A wrapper is a separate host-registered typed callable surrounding a continuation
for the rest of the request. It can perform work before and after awaiting that
continuation. Registration declares deterministic outer-to-inner order; entry
runs in that order and cleanup runs inner-to-outer. Each continuation is
single-use: a wrapper may invoke it once or deliberately short-circuit with an
explicit response, but a second invocation is an error and must not rerun the
handler. If it does not invoke the continuation, downstream wrappers,
preprocessing and the handler are not invoked. A wrapper must not detach the
continuation, race it against itself, or return before its owned downstream work
has completed. Retrying business work is not implicit middleware behavior.
Calling a continuation after its owning wrapper scope ends is invalid.
Short-circuiting cannot bypass authorization: wrappers that produce protected
data or perform sensitive operations must satisfy the applicable policy at those
boundaries. A wrapper may return an unauthenticated challenge or another safe
failure without running the protected handler. Registration treats wrappers as
trusted host components, not as a means for arbitrary code to waive policy.

Every acquired wrapper resource is released exactly once in a `finally`/scope
exit on success, typed failure, short circuit, exception and cooperative
cancellation, including when an inner wrapper fails during entry. Release only
successfully acquired resources. For streaming responses, lifetime extends until
body completion, failure or abort, not merely until headers or a body producer
are returned; cancellation or disconnect still triggers cleanup. A cleanup fault
must be reported without hiding an earlier primary failure or attempting a
second response; if it is the first fault before commit, use the unexpected-fault
boundary. Cleanup must not rely on a cancellation signal that prevents release.
Wrappers may observe a typed failure, but do not erase it or impersonate a valid
capability. Transaction wrappers make no general promise of rolling back effects
outside their explicitly controlled resource.

Request cancellation, host-owned authority and immutable diagnostic correlation
flow across awaited callbacks and wrapper boundaries, isolated by invocation and
host. Explicitly transfer allowed context at runtime boundaries rather than
assuming thread-local flow. Context is released at invocation end, including on
fault or cancellation. Logger remains the host-scoped system actor: submission is
non-waiting and transient, scope snapshots are diagnostic only, and output
failure cannot change the response, authorization or transaction result. Avoid
logging credentials, request bodies or capability material by default; select
safe fields explicitly. These proposals do not make logging a durable audit.

## Illustrative protected D# route

The names, constructor forms, type signatures, registration and mapping syntax
below are design sketches; neither the syntax nor these APIs are implemented by
this proposal. The functions `authenticate`, `authorizeCreate`, and `decode`
are adapter/application functions that explicitly map their distinct failures
into the same tagged `CreateError`. The opaque capability shown as `proof` is
issued and checked by the trusted runtime, never constructed by application code.

```text
type CreateError {
    | AuthenticationFailed(AuthError)
    | Forbidden(AuthorizationError)
    | InvalidRequest(BindingError)
    | InvoiceFailed(InvoiceError)
}

agent CreateInvoice(invoices: InvoiceService) {
    trigger Http.post("/invoices")

    pipeline = async raw => {
        await (
            raw
            |> authenticate
            |> mapError(error => AuthenticationFailed(error))
            |> andThen(authorizeCreate)
            |> andThen(decodeCreateInvoice)
        )
    }

    handle(request: Authorized<CreateInvoiceRequest, CreateInvoices>)
        : Task<Result<Invoice, CreateError>> {
        await (
            invoices.create(request.body, request.identity, request.proof)
            |> mapError(error => InvoiceFailed(error))
        )
    }
}
```

Conceptual signatures make the error conversions and exact boundary visible:

```text
authenticate : HttpRequest -> Task<Result<Authenticated<HttpRequest>, AuthError>>
authorizeCreate : Authenticated<HttpRequest>
    -> Task<Result<Authorized<HttpRequest, CreateInvoices>, CreateError>>
decodeCreateInvoice : Authorized<HttpRequest, CreateInvoices>
    -> Result<Authorized<CreateInvoiceRequest, CreateInvoices>, CreateError>
```

The authorization and decode adapters explicitly wrap their own errors as
`Forbidden(...)` and `InvalidRequest(...)`. The protected handler guard validates
the runtime proof on dispatch, and `invoices.create` checks it again for the
actual operation/resource. The application cannot turn a manually constructed
envelope into valid proof. In this illustrative endpoint an explicit mapping
returns `201` with an encoded invoice on success and maps `AuthenticationFailed`
for missing or invalid credentials to `401` plus challenge, authentication
provider failures to an explicitly configured server failure, `Forbidden` to
`403`, and `InvalidRequest` to `400`,
`413` or `415` by tagged binding case. Every `InvoiceFailed` case has an
explicit declared mapping; the mapping mechanism and the domain's particular
error cases are intentionally not invented here. The host configuration must
explicitly register `CreateInvoice` and its binder, policy, wrapper chain,
encoders and mappings before traffic is accepted.

## Evidence required after adoption

Implementation must add D# source examples and executable tests, with generated
C# and live transport tests where relevant. At minimum:

1. Bind path, repeated query, headers and body, exercising invalid encodings,
   missing/duplicate values, malformed bodies, streamed size limits, unsupported
   content types and unapproved nullable/Option wire forms. Reject incompatible
   input/output types, missing direct binders, conflicting routes and incomplete
   error mappings before traffic.
2. Verify sync/async success and typed failures at every preprocessing step and
   handler, with each callback invoked at most once and later steps skipped on
   failure. Mixed errors require `mapError`; erased or unmapped errors fail
   validation. Exceptions and cancellation propagate separately from `Error`.
3. Verify exact status, challenge, safe headers and body for every mapped case;
   distinguish `401` from `403`, `400`, `413` and `415`, `204` without a body,
   HEAD without a body and explicit application `404`/`409`. Fault before commit
   produces sanitized `500`; fault after commit aborts without a second response.
4. Attempt public `Authorized` construction, altered identity/proof,
   replayed/expired/revoked and cross-host/request/tenant/action/resource proof,
   forged claims, and log-scope impersonation. All must fail authorization
   where invalid, including after an await or a resource/policy change. A copied
   reference retains only its original validity; a valid capability works only
   for its bound active invocation and checks at both the
   handler and sensitive operation boundaries. Changing body fields that affect
   the authorized tenant, resource or action must fail validation or require a
   fresh check; an unrelated body change does not invalidate an action-scoped
   capability unless policy explicitly binds it to that field.
5. Test wrapper order and exactly-once LIFO cleanup on success, typed failure,
   deliberate short circuit, inner acquisition failure, fault, client disconnect,
   cancellation and streaming completion/abort. A second continuation call is
   rejected without rerunning work. Cleanup faults preserve the primary fault;
   no detached work or second response survives the invocation. A wrapper's
   short circuit cannot return protected data or perform a sensitive operation
   without satisfying that operation's policy.
6. Run concurrent hosts and requests with interleaved awaits; verify isolation
   of authority, cancellation, correlation and scopes. A full or failed Logger
   output cannot block or alter the route's response; no credentials or proof
   are automatically logged. Verify operation side effects are not claimed to
   roll back merely because the client cancelled.

## Decisions and adoption gate

Review the proposed binding and encoding boundaries, explicit response mappings,
capability issuance/validation/revocation and service enforcement, wrapper
registration and single-use continuation, streaming lifetime, cancellation and
fault boundaries. Select the actual public APIs and route/query syntax separately;
this proposal does not choose a provider, wire encoding for Option, HTTP timeout,
WebSockets or gRPC. An explicitly adopted contract is required before dependent
implementation. Passing documentation checks or merging this file does not adopt
it or establish that any HTTP behavior exists.

## Verification of this proposal

Bootstrap, documentation and link checks, and compiler verification passed
(18 baseline and 362 feature tests). All 23 automation tests passed. The local
isolated verifier could not download its pinned Docker image (`Forbidden`);
GitHub Actions runs the unchanged verifier. These checks validate the repository,
not HTTP runtime behavior or adoption.
