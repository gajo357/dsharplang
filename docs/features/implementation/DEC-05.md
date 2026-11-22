# DEC-05: HTTP composition and authorization contract

**Status:** Proposed decision; effective when reviewed and merged, not implemented.

**Issue:** [#15](https://github.com/gajo357/dsharplang/issues/15).

**Base:** `d6d2ad277caa717639971deb00c7e72aac98c289`.

## Work plan

Reconcile the existing HTTP, composition, authorization and logging direction;
propose typed binding, explicit mappings and wrapping/cleanup contracts with D#
illustrations and adversarial tests. Separate settled choices from proposals and
identify unresolved gates. This design-only slice changes this document and no
compiler or runtime behavior.

## Scope and existing decisions

This decision takes effect when reviewed and merged; it is not an implemented
API or evidence of runtime behavior. Names and signatures for HTTP, authentication,
authorization, binding, wrappers and mappings below are provisional. The D#
examples are illustrative target code, not tested current compiler support.

The [syntax reference](../DSharp_Syntax_Reference.md) §16 and
[pipes and logging specification](../DSharp_Pipes_and_Logging.md) §§4–8 settle
these constraints:

- An explicitly registered HTTP route targets an actor; `pipeline = expression`
  holds one ordinary typed callable. There is no `pipeline {}` step list. The host
  invokes preprocessing once per HTTP request, submits to the actor only on
  success, and otherwise directly binds the actor input when no preprocessing is declared.
- `|>` is ordinary value piping. `andThen` composes Result-returning steps with
  a common error type; different errors need explicit `mapError`. Its supported
  synchronous and asynchronous combinations preserve typed errors and invoke a
  subsequent callback at most once. Exceptions and cancellation propagate; Result
  helpers do not silently catch, flatten or retry them.
- The actor receives precisely the successful pipeline output type. HTTP
  error mappings are explicit. Wrapping middleware is distinct from preprocessing
  and must clean up on failure and cancellation.
- A public record named `Authorized` is an envelope, not proof of permission.
  Runtime authentication and authorization checks enforce access; sensitive
  operations enforce their own policy. Logging context is diagnostic, never
  authority.

The [actor invocation](DEC-03.md) and [durable acceptance](DEC-04.md) boundaries
govern dispatch. Route and query syntax, HTTP encoding, response customization,
evidence representation and wrapper API spelling belong to their bounded owners.

## Transport and typed composition

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
actor's exact input type. With preprocessing, the host supplies its declared
raw request input, awaits its result once and supplies only `Success(T)` to an
actor whose checked Handle input is exactly `T`; it never invents a default value for
failure. Without preprocessing, a declared direct binder supplies exactly the
actor input under the same error/mapping rules. Registration rejects a missing
binder, incompatible type, unsupported binding, conflicting method/route, or
missing policy before accepting traffic; report statically knowable errors at
compile time. Matching a route does not itself authenticate or authorize it.
Registration is explicit, and importing a package does not enable a route.
The host submits through the durable dispatcher after validation and policy
checks, never by constructing an actor or directly calling Handle. `Invoke`
awaits the exact Handle result; targeted `Dispatch` acknowledges durable
acceptance only. A receipt is not evidence that the actor completed. A single
accepted logical invocation can have several attempts; once-only HTTP
preprocessing does not promise once-only actor execution. On an uncertain
acknowledgement, use the same submission identity for authorized lookup or
deduplicated retry rather than submitting a new logical operation.

Each decoding, authentication, authorization and application error is retained
as a distinct tagged case in an explicitly chosen endpoint error union. Adapt a
step's `Result<A, SpecificError>` with `mapError` into that union **before**
`andThen`; avoid stringification, a catch-all universal error, implicit
conversion or treating `Error` as a successful payload. The same rules apply to
the Handle result: preserve its declared error until its explicit response
mapping. Each supported failure case must map to a response; registration rejects
an absent or ambiguous mapping. Separate exception and cancellation handling from
typed errors. An exception is not an application `Error`, and cancellation is
not an authentication or validation failure.

## Responses and failure boundaries

An Invoke endpoint explicitly maps each Handle value and business error; a
Dispatch endpoint maps the durable acceptance receipt and admission failures,
not a later Handle result. Both map their applicable pipeline, authentication,
authorization, binding and runtime failure categories to an `HttpResponse` with
a legal status, headers and body encoder. No mapping silently returns `default(T)`, relies on exception
text, or turns an unmapped case into success. The following are standard mappings
for the *named transport failure categories*, not inferred meanings for arbitrary
application errors; application cases require explicit mappings and the API
shape remains open:

| Condition | Response before headers commit |
| --- | --- |
| Missing or invalid authentication | `401` with the applicable authentication challenge |
| Authenticated principal denied permission | `403` |
| Malformed path/query/body or failed declared input validation | `400` |
| Body exceeds configured limit | `413` |
| Unsupported request content type | `415` |
| Unexpected fault | Sanitized `500`; record diagnostics without exposing secrets |

An unavailable or faulty authentication provider is a server failure, not proof
of invalid credentials and never a reason to grant access. Its exact availability
status mapping needs explicit configuration; it must not be reported
as `401` merely because the provider failed.

An application can deliberately map its own cases, such as a known missing
resource to `404` or a conflict to `409`; it cannot use mapping to bypass the
runtime authorization guard. An operation with no representation may explicitly
return `204`, with no body. Validate illegal status/body combinations, headers
and encoders before commit when possible. HEAD response handling must not write a
body. A 401 challenge must match the configured authentication scheme; this
proposal does not choose an identity provider, credential format or challenge.

The host writes at most one response. A typed failure or unexpected fault before
commit can be mapped as above. Once headers or body are committed, no second
status or replacement response can be sent: terminate/abort the response according
to the transport and report the fault to supervision. Client disconnect or
request cancellation normally aborts rather than inventing a response. Timeout or
disconnect of the HTTP wait does not itself cancel accepted actor work. Explicit
actor cancellation is a separate cooperative request and does not roll back
previously performed external effects. If the transport is still writable
and a separately adopted timeout policy wants a response, it must specify that
case explicitly; this proposal does not select a timeout status or deadline.

## Authentication and authorization evidence

Authentication verifies credentials through a configured trusted adapter and
establishes a principal or fails. Authorization checks a concrete action and,
where applicable, resource and tenant against policy for that principal.
Authorization may require loading the resource first; ensure loading does not
expose protected data. These are distinct checks. The ordinary
`Authorized<T, Permission>` envelope can carry decoded body and identity for
convenience, but anyone can construct or copy such a public record. Its fields,
type name, a log scope, caller-supplied claims or a fabricated principal are never
accepted as proof.

Proof is a runtime-owned opaque capability issued only after successful
trusted checks, backed by host-private state or an equivalent unforgeable
runtime validation mechanism, not by the visibility of a D# record or by a
caller-controlled boolean. Its validation binds the issuing host, active request
invocation, verified principal, tenant, action and applicable resource, policy
and validity period. After decoding, the host validates it immediately before
admission against the actual actor target and concrete action, tenant and
resource the message will request. A mismatch requires a fresh successful check;
proof for one operation cannot be carried over to another. Sensitive operations independently validate authority
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
runtime boundary, not a claim that D# visibility alone prevents hostile
code or that an in-process language sandbox exists. Trusted host/adapters and
security of their implementation remain required; exact storage, revocation,
policy and public API belong to their implementation owners.

The live proof and request, response, body stream or other scoped capability must
never be serialized into the durable actor message or replayed after request end.
Admission authorization establishes only that the route may submit the checked
message at that boundary; it does not promise that permission remains valid at a
later attempt or protected write. Every sensitive execution-time operation must
independently authorize its actual action, tenant and resource against current
policy and revocation. If the host cannot establish valid execution-time authority,
it fails closed; admission, public records, diagnostic correlation or a copied
subject ID alone do not grant authority. Trusted infrastructure may use a durable
delegation only when a separately specified integrity-protected, scoped, expiring
and revocable contract can establish that authority; this decision does not invent
such a credential, token format or mechanism. Where a policy change must be
atomic with a write, enforce it at that resource's transactional boundary; a
separate recheck cannot undo an already committed effect. Queue and scheduled
entry points use their own explicit policies, not replayed HTTP evidence.
Authorized status lookup and deduplicated retries must enforce the caller's
applicable policy before returning receipt or result; knowing a submission ID is
not authority to inspect it.

## Wrapping, async and context

A wrapper is a separate host-registered typed callable surrounding a continuation
for the rest of the request. It can perform work before and after awaiting that
continuation. Registration declares deterministic outer-to-inner order; entry
runs in that order and cleanup runs inner-to-outer. Each continuation is
single-use: a wrapper may invoke it once or deliberately short-circuit with an
explicit response, but a second invocation is an error and must not rerun the
actor submission. If it does not invoke the continuation, downstream wrappers,
preprocessing and actor submission are not invoked. A wrapper must not detach the
continuation, race it against itself, or return before its owned downstream work
has completed. Retrying business work is not implicit middleware behavior.
Calling a continuation after its owning wrapper scope ends is invalid.
Short-circuiting cannot bypass authorization: wrappers that produce protected
data or perform sensitive operations must satisfy the applicable policy at those
boundaries. A wrapper may return an unauthenticated challenge or another safe
failure without submitting protected work. Registration treats wrappers as
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
flow across awaited callbacks and wrapper boundaries within the HTTP request,
isolated by invocation and host. Transfer only explicitly allowed immutable
diagnostic context to durable actor work, not live request authority or scope.
Do not assume thread-local flow. Context is released at invocation end, including on
fault or cancellation. Logger remains the host-scoped system actor: submission is
non-waiting and transient, scope snapshots are diagnostic only, and output
failure cannot change the response, authorization or transaction result. Avoid
logging credentials, request bodies or capability material by default; select
safe fields explicitly. These proposals do not make logging a durable audit.

## Illustrative protected D# route

The registration, fault handling and adapter names below are sketches, not
implemented APIs or settled grammar. The HTTP route and actor are explicitly
registered. The route validates host-issued proof for admission; the actor
message contains immutable data, never the live proof or request object.

```text
type CreateError {
    | AuthenticationFailed(AuthError)
    | Forbidden(AuthorizationError)
    | InvalidRequest(BindingError)
    | InvoiceFailed(InvoiceError)
}

httpRoute CreateInvoiceRoute {
    post("/invoices")
    pipeline = async raw => {
        await (
            raw
            |> authenticate
            |> mapError(error => AuthenticationFailed(error))
            |> andThen(authorizeCreate)
            |> andThen(decodeCreateInvoice)
        )
    }
    target = InvoiceActor
}

actor InvoiceActor(invoices: InvoiceResource) {
    Handle(request: CreateInvoiceRequest): Result<Invoice, InvoiceError> {
        // The resource enforces its own execution-time policy.
        invoices.create(request)
    }
}
```

Conceptual adapter signatures explain the boundary:

```text
authenticate : HttpRequest -> Task<Result<Authenticated<HttpRequest>, AuthError>>
authorizeCreate : Authenticated<HttpRequest>
    -> Task<Result<Authorized<HttpRequest, CreateInvoices>, CreateError>>
decodeCreateInvoice : Authorized<HttpRequest, CreateInvoices>
    -> Result<CreateInvoiceRequest, CreateError>
```

`Authorized` is an ordinary envelope, not proof. The host retains and validates
its opaque request-scoped proof for the concrete admission immediately before
submitting the decoded, immutable `CreateInvoiceRequest`. The proof does not
enter that message. The trusted resource checks current authority for the actual
write using a separately specified execution authority; if unavailable or invalid,
it denies the operation. The runtime must not substitute a public envelope or
log context for that authority. A deployment requiring durable user delegation
must provide an approved mechanism before enabling the protected operation; this
decision does not claim such a mechanism already exists.

For this illustrative route, `Invoke` returns the exact declared
`Result<Invoice, InvoiceError>`, which maps explicitly to `201` or the chosen
mapping for each business error. An alternative targeted `Dispatch` route may
map a receipt to an acceptance response but cannot claim invoice creation. The
route maps missing/invalid credentials to `401` with the configured challenge,
policy denial to `403`, and binding failures to `400`, `413` or `415`
according to their tagged cases. Provider failures use an explicit server-failure
mapping; all remaining domain errors require explicit mappings. The host
registers the route, target, binder, policy, wrapper chain, encoders and mappings
before accepting traffic. Authorization, decoding and error conversion signatures
are illustrative; they do not settle D# actor or registration syntax.

## Required implementation evidence

Implementation must add D# source examples and executable tests, with generated
C# and live transport tests where relevant. At minimum:

1. Bind path, repeated query, headers and body, exercising invalid encodings,
   missing/duplicate values, malformed bodies, streamed size limits, unsupported
   content types and unapproved nullable/Option wire forms. Reject incompatible
   input/output types, missing direct binders, conflicting routes and incomplete
   error mappings before traffic.
2. Verify sync/async success and typed failures at every preprocessing step and
   Handle outcome, with each preprocessing callback invoked at most once and later steps skipped on
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
   for its bound active HTTP request and admission. Sensitive operations check
   separate execution authority, including after delay, retry, restart or policy
   revocation; without an approved delegation they fail closed. Exercise policy
   change between the initial check and admission, provider outage, and retry or
   status lookup by a different subject/action using the same submission ID;
   an untrusted identity alone cannot disclose status or a receipt. Changing body fields that affect
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

## Implementation boundaries

Implement the typed binding, mappings, runtime evidence, wrapper continuation
and lifetime rules above. Select exact public APIs and route/query syntax in
their owning issues. This decision does not choose an identity provider, a
wire encoding for Option, a durable delegation mechanism, HTTP timeout,
WebSockets or gRPC. A protected operation that needs unavailable delegation
must fail closed. See the issue plan for documentation and compiler verification;
those checks do not establish HTTP or authorization behavior.
