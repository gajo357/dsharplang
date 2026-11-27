namespace DSharp.Runtime;

// Explicit codec bindings only; this does not register actors, assign versions,
// infer compatibility, or define a durable/public serialization protocol.
internal sealed class JsonContractRegistration
{
    private JsonContractRegistration(JsonContractIdentity identity, JsonRecordContract? record, JsonUnionContract? union)
    {
        Identity = identity;
        Record = record;
        Union = union;
    }

    internal JsonContractIdentity Identity { get; }
    internal JsonRecordContract? Record { get; }
    internal JsonUnionContract? Union { get; }

    internal static JsonContractRegistration ForRecord(JsonContractIdentity identity, JsonScalarRecordSchema schema)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return new(identity, new JsonRecordContract(identity, schema), null);
    }

    internal static JsonContractRegistration ForUnion(JsonContractIdentity identity, JsonTaggedUnionSchema schema)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return new(identity, null, new JsonUnionContract(identity, schema));
    }
}

internal sealed class JsonContractCatalog
{
    private readonly Dictionary<string, JsonContractRegistration> entries;

    internal JsonContractCatalog(IEnumerable<JsonContractRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        var copied = new Dictionary<string, JsonContractRegistration>(StringComparer.Ordinal);
        foreach (var registration in registrations)
        {
            if (registration is null) throw new ArgumentException("Contract registrations cannot be null.", nameof(registrations));
            if (!copied.TryAdd(Key(registration.Identity.Bytes), registration))
                throw new ArgumentException("Contract identities must be unique.", nameof(registrations));
        }
        entries = copied;
    }

    internal JsonScalarResult<JsonValidatedRecordPayload> DecodeRecord(ReadOnlyMemory<byte> metadata, ReadOnlyMemory<byte> payload)
    {
        var snapshot = metadata.ToArray();
        return entries.TryGetValue(Key(snapshot), out var entry) && entry.Record is { } contract
            ? contract.Decode(snapshot, payload)
            : JsonScalarResult<JsonValidatedRecordPayload>.Failure(JsonScalarErrorCode.ContractMismatch);
    }

    internal JsonScalarResult<JsonValidatedUnionPayload> DecodeUnion(ReadOnlyMemory<byte> metadata, ReadOnlyMemory<byte> payload)
    {
        var snapshot = metadata.ToArray();
        return entries.TryGetValue(Key(snapshot), out var entry) && entry.Union is { } contract
            ? contract.Decode(snapshot, payload)
            : JsonScalarResult<JsonValidatedUnionPayload>.Failure(JsonScalarErrorCode.ContractMismatch);
    }

    private static string Key(byte[] identity) => Convert.ToBase64String(identity);
}
