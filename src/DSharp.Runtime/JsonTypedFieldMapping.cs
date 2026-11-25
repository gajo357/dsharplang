using System.Globalization;

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
    internal static JsonScalarResult<JsonFieldValue> FromRecords(JsonRecordArray value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return FromItems(value.Depth, value.Count, index => FromRecord(value.At(index)));
    }

    internal static JsonScalarResult<JsonFieldValue> FromUnions(JsonUnionArray value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return FromItems(value.Depth, value.Count, index => FromUnion(value.At(index)));
    }

    internal static JsonScalarResult<JsonFieldValue> FromNullableUnions(JsonNullableUnionArray value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return FromItems(value.Depth, value.Count, index => value.At(index) is { } item
            ? FromUnion(item) : JsonScalarResult<JsonFieldValue>.Success(JsonFieldValue.Null()));
    }

    internal static JsonScalarResult<JsonRecordArray> ToRecords(JsonScalarRecordSchema schema, JsonFieldValue value)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(value);
        var items = ToItems(value, item => ToRecord(schema, item));
        return items.IsSuccess ? JsonScalarResult<JsonRecordArray>.Success(JsonRecordArray.Create(schema, items.Value))
            : JsonScalarResult<JsonRecordArray>.Failure(items.Error!.Value.Code, items.Error.Value.Path);
    }

    internal static JsonScalarResult<JsonUnionArray> ToUnions(JsonTaggedUnionSchema schema, JsonFieldValue value)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(value);
        var items = ToItems(value, item => ToUnion(schema, item));
        return items.IsSuccess ? JsonScalarResult<JsonUnionArray>.Success(JsonUnionArray.Create(schema, items.Value))
            : JsonScalarResult<JsonUnionArray>.Failure(items.Error!.Value.Code, items.Error.Value.Path);
    }

    internal static JsonScalarResult<JsonNullableUnionArray> ToNullableUnions(JsonTaggedUnionSchema schema, JsonFieldValue value)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(value);
        var items = ToItems(value, item => ReadNullableUnion(schema, item));
        return items.IsSuccess ? JsonScalarResult<JsonNullableUnionArray>.Success(JsonNullableUnionArray.Create(schema, items.Value))
            : JsonScalarResult<JsonNullableUnionArray>.Failure(items.Error!.Value.Code, items.Error.Value.Path);
    }

    private static JsonScalarResult<JsonTaggedUnion?> ReadNullableUnion(JsonTaggedUnionSchema schema, JsonFieldValue item)
    {
        if (item.Kind == JsonFieldValueKind.Null) return JsonScalarResult<JsonTaggedUnion?>.Success(null);
        var result = ToUnion(schema, item);
        return result.IsSuccess ? JsonScalarResult<JsonTaggedUnion?>.Success(result.Value)
            : JsonScalarResult<JsonTaggedUnion?>.Failure(result.Error!.Value.Code, result.Error.Value.Path);
    }

    private static JsonScalarResult<JsonFieldValue> FromItems(int depth, int count, Func<int, JsonScalarResult<JsonFieldValue>> read)
    {
        if (depth > 64) throw new ArgumentException("JSON nesting exceeds 64 containers.");
        var items = new JsonFieldValue[count];
        for (var index = 0; index < count; index++)
        {
            var item = read(index);
            if (!item.IsSuccess)
                return JsonScalarResult<JsonFieldValue>.Failure(item.Error!.Value.Code, Index(index, item.Error.Value.Path));
            items[index] = item.Value;
        }
        return JsonScalarResult<JsonFieldValue>.Success(JsonFieldValue.List(items));
    }

    private static JsonScalarResult<T[]> ToItems<T>(JsonFieldValue value, Func<JsonFieldValue, JsonScalarResult<T>> read)
    {
        if (value.Kind != JsonFieldValueKind.List) return JsonScalarResult<T[]>.Failure(JsonScalarErrorCode.TypeMismatch);
        var source = value.ListItems;
        var items = new T[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            var item = read(source[index]);
            if (!item.IsSuccess)
                return JsonScalarResult<T[]>.Failure(item.Error!.Value.Code, Index(index, item.Error.Value.Path));
            items[index] = item.Value;
        }
        return JsonScalarResult<T[]>.Success(items);
    }

    private static string Index(int index, string path) =>
        "$[" + index.ToString(CultureInfo.InvariantCulture) + "]" + path[1..];

}
