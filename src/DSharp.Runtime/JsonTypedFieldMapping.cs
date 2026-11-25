namespace DSharp.Runtime;

// Explicit internal bridge using the existing provisional codecs. It does not
// discover schemas, execute application constructors, or define an Option or
// durable-message representation.
internal static class JsonTypedFieldMapping
{
    internal static JsonScalarResult<JsonFieldValue> FromRecord(JsonScalarRecord value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return JsonFieldValueCodec.Decode(JsonScalarRecordCodec.Encode(value));
    }

    internal static JsonScalarResult<JsonScalarRecord> ToRecord(JsonScalarRecordSchema schema, JsonFieldValue value)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(value);
        return JsonScalarRecordCodec.Decode(schema, JsonFieldValueCodec.Encode(value));
    }

    internal static JsonScalarResult<JsonFieldValue> FromUnion(JsonTaggedUnion value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return JsonFieldValueCodec.Decode(JsonTaggedUnionCodec.Encode(value));
    }

    internal static JsonScalarResult<JsonTaggedUnion> ToUnion(JsonTaggedUnionSchema schema, JsonFieldValue value)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(value);
        return JsonTaggedUnionCodec.Decode(schema, JsonFieldValueCodec.Encode(value));
    }
}
