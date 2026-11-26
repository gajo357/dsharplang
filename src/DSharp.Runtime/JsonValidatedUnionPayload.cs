namespace DSharp.Runtime;

// An internal validation boundary only. It does not define a public D# API,
// durable wire format, version policy or JSON canonicalization.
internal sealed class JsonValidatedUnionPayload
{
    private readonly byte[] bytes;

    private JsonValidatedUnionPayload(byte[] bytes, JsonTaggedUnion value)
    {
        this.bytes = bytes;
        Value = value;
    }

    internal JsonTaggedUnion Value { get; }
    internal byte[] Bytes => (byte[])bytes.Clone();

    internal static JsonScalarResult<JsonValidatedUnionPayload> Decode(JsonTaggedUnionSchema schema, ReadOnlyMemory<byte> input)
    {
        ArgumentNullException.ThrowIfNull(schema);
        // Validate the owned snapshot, not mutable memory retained by a caller.
        var snapshot = input.ToArray();
        var decoded = JsonTaggedUnionCodec.Decode(schema, snapshot);
        return decoded.IsSuccess
            ? JsonScalarResult<JsonValidatedUnionPayload>.Success(new(snapshot, decoded.Value))
            : JsonScalarResult<JsonValidatedUnionPayload>.Failure(decoded.Error!.Value.Code, decoded.Error.Value.Path);
    }
}
