namespace DSharp.Runtime;

// Internal validation and ownership only; no public D# API, canonical form,
// durable wire format or version policy is implied.
internal sealed class JsonValidatedRecordPayload
{
    private readonly byte[] bytes;

    private JsonValidatedRecordPayload(byte[] bytes, JsonScalarRecord value)
    {
        this.bytes = bytes;
        Value = value;
    }

    internal JsonScalarRecord Value { get; }
    internal byte[] Bytes => (byte[])bytes.Clone();

    internal static JsonValidatedRecordPayload FromValue(JsonScalarRecord value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(JsonScalarRecordCodec.Encode(value), value);
    }

    internal static JsonScalarResult<JsonValidatedRecordPayload> Decode(JsonScalarRecordSchema schema, ReadOnlyMemory<byte> input)
    {
        ArgumentNullException.ThrowIfNull(schema);
        var snapshot = input.ToArray();
        var decoded = JsonScalarRecordCodec.Decode(schema, snapshot);
        return decoded.IsSuccess
            ? JsonScalarResult<JsonValidatedRecordPayload>.Success(new(snapshot, decoded.Value))
            : JsonScalarResult<JsonValidatedRecordPayload>.Failure(decoded.Error!.Value.Code, decoded.Error.Value.Path);
    }
}
