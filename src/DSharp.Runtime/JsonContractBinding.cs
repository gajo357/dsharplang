namespace DSharp.Runtime;

// Caller-assigned opaque identity: exact matching only, not schema inference,
// compatibility, canonicalization or an adopted public/durable wire format.
internal sealed class JsonContractIdentity
{
    private readonly byte[] bytes;

    internal JsonContractIdentity(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length == 0) throw new ArgumentException("Contract identity must not be empty.", nameof(bytes));
        this.bytes = (byte[])bytes.Clone();
    }

    internal byte[] Bytes => (byte[])bytes.Clone();
    internal bool Matches(ReadOnlySpan<byte> candidate) => candidate.SequenceEqual(bytes);
}

// Metadata and JSON bytes are owned separately, matching the store boundary.
internal sealed class JsonContractPayload
{
    private readonly byte[] identity;
    private readonly byte[] payload;

    internal JsonContractPayload(JsonContractIdentity identity, byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(payload);
        this.identity = identity.Bytes;
        this.payload = (byte[])payload.Clone();
    }

    internal byte[] Identity => (byte[])identity.Clone();
    internal byte[] Payload => (byte[])payload.Clone();
}

internal sealed class JsonRecordContract
{
    private readonly JsonContractIdentity identity;
    private readonly JsonScalarRecordSchema schema;

    internal JsonRecordContract(JsonContractIdentity identity, JsonScalarRecordSchema schema)
    {
        this.identity = identity ?? throw new ArgumentNullException(nameof(identity));
        this.schema = schema ?? throw new ArgumentNullException(nameof(schema));
    }

    internal JsonContractPayload Encode(JsonScalarRecord value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!Accepts(value)) throw new ArgumentException("Record descriptor does not match the contract.", nameof(value));
        return new(identity, JsonScalarRecordCodec.Encode(value));
    }

    internal bool Accepts(JsonScalarRecord value) => ReferenceEquals(value.Schema, schema);

    internal JsonScalarResult<JsonValidatedRecordPayload> Decode(ReadOnlyMemory<byte> metadata, ReadOnlyMemory<byte> payload)
    {
        var metadataSnapshot = metadata.ToArray();
        if (!identity.Matches(metadataSnapshot)) return JsonScalarResult<JsonValidatedRecordPayload>.Failure(JsonScalarErrorCode.ContractMismatch);
        return JsonValidatedRecordPayload.Decode(schema, payload);
    }
}

internal sealed class JsonUnionContract
{
    private readonly JsonContractIdentity identity;
    private readonly JsonTaggedUnionSchema schema;

    internal JsonUnionContract(JsonContractIdentity identity, JsonTaggedUnionSchema schema)
    {
        this.identity = identity ?? throw new ArgumentNullException(nameof(identity));
        this.schema = schema ?? throw new ArgumentNullException(nameof(schema));
    }

    internal JsonContractPayload Encode(JsonTaggedUnion value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!Accepts(value)) throw new ArgumentException("Union descriptor does not match the contract.", nameof(value));
        return new(identity, JsonTaggedUnionCodec.Encode(value));
    }

    internal bool Accepts(JsonTaggedUnion value) => ReferenceEquals(value.Schema, schema);

    internal JsonScalarResult<JsonValidatedUnionPayload> Decode(ReadOnlyMemory<byte> metadata, ReadOnlyMemory<byte> payload)
    {
        var metadataSnapshot = metadata.ToArray();
        if (!identity.Matches(metadataSnapshot)) return JsonScalarResult<JsonValidatedUnionPayload>.Failure(JsonScalarErrorCode.ContractMismatch);
        return JsonValidatedUnionPayload.Decode(schema, payload);
    }
}
