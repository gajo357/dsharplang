namespace DSharp.Runtime;

/// <summary>
/// An application- and invocation-scoped dispatcher supplied by the host.
/// This interface defines a typed seam; it does not implement dispatch or acceptance.
/// </summary>
public interface IActorDispatcher
{
    /// <summary>Invokes the public target and returns its declared result when it completes.</summary>
    Task<TResult> Invoke<TMessage, TResult>(ActorRoute<TMessage, TResult> route, TMessage message);

    /// <summary>
    /// Submits to the same public target used by <see cref="Invoke{TMessage, TResult}"/>.
    /// A production implementation completes the task after durable acceptance,
    /// without waiting for the target's Handle result.
    /// </summary>
    Task<InvocationReceipt> Dispatch<TMessage, TResult>(ActorRoute<TMessage, TResult> route, TMessage message);

    /// <summary>
    /// Publishes a message without selecting an actor target. A production implementation
    /// completes the task after durable acceptance of the publication and its
    /// delivery intents, without waiting for subscriber results.
    /// </summary>
    Task<PublicationReceipt> Publish<TMessage>(MessageContract<TMessage> contract, TMessage message);
}

/// <summary>
/// Identifies a public target and its message and result types within a host's scope.
/// The key does not define a durable or cross-deployment identity.
/// </summary>
public sealed class ActorRoute<TMessage, TResult>
{
    /// <summary>Creates a route descriptor with a nonblank host-scoped key.</summary>
    public ActorRoute(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key;
    }

    /// <summary>Gets the opaque host-scoped key.</summary>
    public string Key { get; }
}

/// <summary>
/// Identifies a message contract within a host's scope. Its key does not define
/// an encoding or durable schema identity.
/// </summary>
public sealed class MessageContract<TMessage>
{
    /// <summary>Creates a message contract descriptor with a nonblank host-scoped key.</summary>
    public MessageContract(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key;
    }

    /// <summary>Gets the opaque host-scoped key.</summary>
    public string Key { get; }
}

/// <summary>
/// A targeted acceptance token. Constructing a token does not itself establish acceptance.
/// </summary>
public sealed class InvocationReceipt
{
    /// <summary>Creates a token for a host or test implementation.</summary>
    public InvocationReceipt() { }
}

/// <summary>
/// A publication acceptance token. Constructing a token does not itself establish acceptance.
/// </summary>
public sealed class PublicationReceipt
{
    /// <summary>Creates a token for a host or test implementation.</summary>
    public PublicationReceipt() { }
}
