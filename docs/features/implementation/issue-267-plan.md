# Issue #267 — typed actor dispatcher runtime seam

Issue: https://github.com/gajo357/dsharplang/issues/267
Parent: https://github.com/gajo357/dsharplang/issues/241
Base: `44b8107ebfd17f602588b61cfd7dd7c82a967a53` (`origin/main`).

## Scope, dependencies and decisions

Implement only the runtime ABI child of the merged #241 plan. #9 enables the
runtime assembly, tests and verifier; #13/#14 and #51 supply the checked actor
and dispatch semantics. #241 and #52 stay open. There is no compiler lowering,
application host, registration, production dispatcher, durable acceptance,
execution, subscriber fan-out, codec, lookup, error taxonomy or wait protocol.
The host must complete receipt tasks only after the actual acceptance promised
by the operation; a fake receipt proves no such acceptance.

Add `src/DSharp.Runtime/ActorDispatcher.cs` with these public CLR shapes:

```csharp
public interface IActorDispatcher {
    Task<TResult> Invoke<TMessage, TResult>(ActorRoute<TMessage, TResult> route, TMessage message);
    Task<InvocationReceipt> Dispatch<TMessage, TResult>(ActorRoute<TMessage, TResult> route, TMessage message);
    Task<PublicationReceipt> Publish<TMessage>(MessageContract<TMessage> contract, TMessage message);
}
public sealed class ActorRoute<TMessage, TResult> { public ActorRoute(string key); public string Key { get; } }
public sealed class MessageContract<TMessage> { public MessageContract(string key); public string Key { get; } }
public sealed class InvocationReceipt { public InvocationReceipt(); }
public sealed class PublicationReceipt { public PublicationReceipt(); }
```

Route and message keys reject null or whitespace and remain immutable,
host-scoped opaque identities. Equality, encoding, durable identity, and
cross-deployment compatibility are not specified. Receipt classes expose no ID,
fields or storage protocol; CLR hosts and test fakes can construct tokens but
this does not add constructors to D#. They are distinct nominal types. Passing
the same typed route to targeted methods preserves a common public target; no
dispatch method invokes Handle or manufactures acceptance on its own.

The future compiler child must reference the existing runtime assembly from
its generated project and receive an `IActorDispatcher` from the application
and invocation host at its generated entry boundary. It must propagate that
specific instance through supported functions, actor helpers and closures,
including across awaits; reject unsupported paths rather than use a global.
Its plan owns the concrete generated-project reference and entry signature.
The generic result can carry generated Unit without a shared Unit CLR type.
The future compiler must make receipts recognizable without granting D# direct
construction. This child does not modify project references or manifests.

## Steps and acceptance mapping

1. Implement only the described types and XML documentation on behavior and
   non-claims. Existing runtime source, tests, manifests and verification stay
   intact. Add `tests/runtime-features/ActorDispatcherContractTests.cs`.
2. A recording implementation must test shared route/message forwarding,
   typed value, Unit-like and business-error results, distinct receipt types,
   publication without a target with zero and multiple simulated subscribers,
   independently controlled pending tasks, fault propagation and interleaved
   isolated dispatcher instances. Validate immutable nonempty keys. These
   exercise the ABI and fake, not real acceptance, delivery or scheduling.
3. Update implementation status for the seam actually tested. Run focused
   runtime features plus bootstrap, full verifier, standalone compiler and
   runtime checks and `git diff --check`. Record actual results, including the
   local image-pull limitation; exact-head hosted checks are required to merge.

## Results

- Bootstrap passed. Documentation validation passed for 122 files and 308
  local links; all 31 automation tests passed. The full local verifier could
  not pull its pinned MCR SDK image (`Forbidden`), so isolated compiler/runtime
  verification requires the hosted exact-head workflows.
- The four focused runtime contract tests passed. After adding a successful
  zero-subscriber publication case, standalone runtime verification passed
  2 baseline and 5 feature tests, including all four contract tests.
- The unchanged compiler verifier passed 18 baseline and 396 feature tests
  from a byte-checked short source snapshot; its 509,900-byte feature TRX
  remained below the unchanged 512,000-byte guard. The runtime test's final
  zero-subscriber case and these results were added after the snapshot;
  standalone runtime verification above exercised that final case.
