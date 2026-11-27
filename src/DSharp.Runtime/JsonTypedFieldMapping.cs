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

    internal static JsonScalarResult<JsonFieldValue> FromNullableRecords(JsonNullableRecordArray value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return FromItems(value.Depth, value.Count, index => value.At(index) is { } item
            ? FromRecord(item) : JsonScalarResult<JsonFieldValue>.Success(JsonFieldValue.Null()));
    }

    internal static JsonScalarResult<JsonNullableRecordArray> ToNullableRecords(JsonScalarRecordSchema schema, JsonFieldValue value)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(value);
        var items = ToItems(value, item => ReadNullableRecord(schema, item));
        return items.IsSuccess ? JsonScalarResult<JsonNullableRecordArray>.Success(JsonNullableRecordArray.Create(schema, items.Value))
            : JsonScalarResult<JsonNullableRecordArray>.Failure(items.Error!.Value.Code, items.Error.Value.Path);
    }

    private static JsonScalarResult<JsonScalarRecord?> ReadNullableRecord(JsonScalarRecordSchema schema, JsonFieldValue item)
    {
        if (item.Kind == JsonFieldValueKind.Null) return JsonScalarResult<JsonScalarRecord?>.Success(null);
        var result = ToRecord(schema, item);
        return result.IsSuccess ? JsonScalarResult<JsonScalarRecord?>.Success(result.Value)
            : JsonScalarResult<JsonScalarRecord?>.Failure(result.Error!.Value.Code, result.Error.Value.Path);
    }

    internal static JsonScalarResult<JsonFieldValue> FromUnionMap(JsonUnionMap value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return FromMap(value.Depth, value.Count, index => (value.At(index).Key, FromUnion(value.At(index).Value)));
    }

    // As for scalar maps, use the existing field codec in a temporary wrapper
    // and expose only the array as the tree root.
    internal static JsonScalarResult<JsonFieldValue> FromScalarArray(JsonRecordScalarArray value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var schema = ScalarArraySchema(value.Kind);
        var record = JsonScalarRecord.Create(schema, [value], [true]);
        var tree = FromRecord(record);
        return tree.IsSuccess ? JsonScalarResult<JsonFieldValue>.Success(tree.Value.MapItems[0].Value)
            : JsonScalarResult<JsonFieldValue>.Failure(tree.Error!.Value.Code, ScalarValuePath(tree.Error.Value.Path));
    }

    internal static JsonScalarResult<JsonRecordScalarArray> ToScalarArray(JsonRecordScalarArrayKind kind, JsonFieldValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var schema = ScalarArraySchema(kind);
        if (value.Kind != JsonFieldValueKind.List)
            return JsonScalarResult<JsonRecordScalarArray>.Failure(JsonScalarErrorCode.TypeMismatch);
        // A depth-64 child cannot be wrapped; validate preceding values first
        // so a prior invalid scalar remains the first reported error.
        if (value.Depth == 64)
        {
            var items = value.ListItems;
            var firstContainer = Array.FindIndex(items, item => item.Kind is JsonFieldValueKind.List or JsonFieldValueKind.Map);
            var prefix = ToScalarArray(kind, JsonFieldValue.List(items[..firstContainer]));
            return prefix.IsSuccess
                ? JsonScalarResult<JsonRecordScalarArray>.Failure(JsonScalarErrorCode.TypeMismatch, Index(firstContainer, "$"))
                : prefix;
        }
        var wrapper = JsonFieldValue.Map([new("value", value)]);
        var record = ToRecord(schema, wrapper);
        return record.IsSuccess ? JsonScalarResult<JsonRecordScalarArray>.Success((JsonRecordScalarArray)record.Value.ValueAt(0)!)
            : JsonScalarResult<JsonRecordScalarArray>.Failure(record.Error!.Value.Code, ScalarValuePath(record.Error.Value.Path));
    }

    private static JsonScalarRecordSchema ScalarArraySchema(JsonRecordScalarArrayKind kind) => new([new("value", kind switch
    {
        JsonRecordScalarArrayKind.Boolean => JsonRecordFieldKind.BooleanArray,
        JsonRecordScalarArrayKind.Integer => JsonRecordFieldKind.IntegerArray,
        JsonRecordScalarArrayKind.String => JsonRecordFieldKind.StringArray,
        JsonRecordScalarArrayKind.ExactNumber => JsonRecordFieldKind.ExactNumberArray,
        JsonRecordScalarArrayKind.Decimal => JsonRecordFieldKind.DecimalArray,
        JsonRecordScalarArrayKind.Int64 => JsonRecordFieldKind.Int64Array,
        JsonRecordScalarArrayKind.UInt64 => JsonRecordFieldKind.UInt64Array,
        JsonRecordScalarArrayKind.NullableBoolean => JsonRecordFieldKind.BooleanNullableElementsArray,
        JsonRecordScalarArrayKind.NullableInteger => JsonRecordFieldKind.IntegerNullableElementsArray,
        JsonRecordScalarArrayKind.NullableString => JsonRecordFieldKind.StringNullableElementsArray,
        JsonRecordScalarArrayKind.NullableExactNumber => JsonRecordFieldKind.ExactNumberNullableElementsArray,
        JsonRecordScalarArrayKind.NullableDecimal => JsonRecordFieldKind.DecimalNullableElementsArray,
        JsonRecordScalarArrayKind.NullableInt64 => JsonRecordFieldKind.Int64NullableElementsArray,
        JsonRecordScalarArrayKind.NullableUInt64 => JsonRecordFieldKind.UInt64NullableElementsArray,
        JsonRecordScalarArrayKind.Single => JsonRecordFieldKind.SingleArray,
        JsonRecordScalarArrayKind.NullableSingle => JsonRecordFieldKind.SingleNullableElementsArray,
        JsonRecordScalarArrayKind.Double => JsonRecordFieldKind.DoubleArray,
        JsonRecordScalarArrayKind.NullableDouble => JsonRecordFieldKind.DoubleNullableElementsArray,
        _ => throw new ArgumentException("Unsupported scalar array descriptor.", nameof(kind))
    })]);

    // Use the same scalar-map codec as record fields, while exposing the map
    // itself as the tree root. The wrapper is strictly internal and temporary.
    internal static JsonScalarResult<JsonFieldValue> FromScalarMap(JsonRecordScalarMap value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var schema = ScalarMapSchema(value.Kind);
        var record = JsonScalarRecord.Create(schema, [value], [true]);
        var tree = FromRecord(record);
        return tree.IsSuccess ? JsonScalarResult<JsonFieldValue>.Success(tree.Value.MapItems[0].Value)
            : JsonScalarResult<JsonFieldValue>.Failure(tree.Error!.Value.Code, ScalarValuePath(tree.Error.Value.Path));
    }

    internal static JsonScalarResult<JsonRecordScalarMap> ToScalarMap(JsonRecordFieldKind kind, JsonFieldValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var schema = ScalarMapSchema(kind);
        if (value.Kind != JsonFieldValueKind.Map)
            return JsonScalarResult<JsonRecordScalarMap>.Failure(JsonScalarErrorCode.TypeMismatch);
        // An incompatible maximum-depth child cannot be wrapped. Check its
        // preceding entries first so the first error remains deterministic.
        if (value.Depth == 64)
        {
            var entries = value.MapItems;
            var firstContainer = Array.FindIndex(entries, entry => entry.Value.Kind is JsonFieldValueKind.List or JsonFieldValueKind.Map);
            var prefix = ToScalarMap(kind, JsonFieldValue.Map(entries[..firstContainer]));
            return prefix.IsSuccess
                ? JsonScalarResult<JsonRecordScalarMap>.Failure(JsonScalarErrorCode.TypeMismatch, Index(firstContainer, "$"))
                : prefix;
        }
        var wrapper = JsonFieldValue.Map([new("value", value)]);
        var record = ToRecord(schema, wrapper);
        return record.IsSuccess ? JsonScalarResult<JsonRecordScalarMap>.Success((JsonRecordScalarMap)record.Value.ValueAt(0)!)
            : JsonScalarResult<JsonRecordScalarMap>.Failure(record.Error!.Value.Code, ScalarValuePath(record.Error.Value.Path));
    }

    private static JsonScalarRecordSchema ScalarMapSchema(JsonRecordFieldKind kind) => new([new("value", kind switch
    {
        JsonRecordFieldKind.Boolean => JsonRecordFieldKind.BooleanMap,
        JsonRecordFieldKind.Integer => JsonRecordFieldKind.IntegerMap,
        JsonRecordFieldKind.String => JsonRecordFieldKind.StringMap,
        JsonRecordFieldKind.ExactNumber => JsonRecordFieldKind.ExactNumberMap,
        JsonRecordFieldKind.Decimal => JsonRecordFieldKind.DecimalMap,
        JsonRecordFieldKind.Int64 => JsonRecordFieldKind.Int64Map,
        JsonRecordFieldKind.UInt64 => JsonRecordFieldKind.UInt64Map,
        JsonRecordFieldKind.NullableBoolean => JsonRecordFieldKind.BooleanNullableValuesMap,
        JsonRecordFieldKind.NullableInteger => JsonRecordFieldKind.IntegerNullableValuesMap,
        JsonRecordFieldKind.NullableString => JsonRecordFieldKind.StringNullableValuesMap,
        JsonRecordFieldKind.NullableExactNumber => JsonRecordFieldKind.ExactNumberNullableValuesMap,
        JsonRecordFieldKind.NullableDecimal => JsonRecordFieldKind.DecimalNullableValuesMap,
        JsonRecordFieldKind.NullableInt64 => JsonRecordFieldKind.Int64NullableValuesMap,
        JsonRecordFieldKind.NullableUInt64 => JsonRecordFieldKind.UInt64NullableValuesMap,
        JsonRecordFieldKind.Single => JsonRecordFieldKind.SingleMap,
        JsonRecordFieldKind.NullableSingle => JsonRecordFieldKind.SingleNullableValuesMap,
        JsonRecordFieldKind.Double => JsonRecordFieldKind.DoubleMap,
        JsonRecordFieldKind.NullableDouble => JsonRecordFieldKind.DoubleNullableValuesMap,
        _ => throw new ArgumentException("Unsupported scalar map descriptor.", nameof(kind))
    })]);

    private static string ScalarValuePath(string path)
    {
        const string prefix = "$[\"value\"]";
        return path.StartsWith(prefix, StringComparison.Ordinal) ? "$" + path[prefix.Length..] : path;
    }

    internal static JsonScalarResult<JsonFieldValue> FromNullableUnionMap(JsonNullableUnionMap value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return FromMap(value.Depth, value.Count, index =>
        {
            var entry = value.At(index);
            return (entry.Key, entry.Value is null ? JsonScalarResult<JsonFieldValue>.Success(JsonFieldValue.Null()) : FromUnion(entry.Value));
        });
    }

    internal static JsonScalarResult<JsonUnionMap> ToUnionMap(JsonTaggedUnionSchema schema, JsonFieldValue value)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(value);
        var items = ToMap(value, item => ToUnion(schema, item));
        return items.IsSuccess ? JsonScalarResult<JsonUnionMap>.Success(JsonUnionMap.Create(schema, items.Value))
            : JsonScalarResult<JsonUnionMap>.Failure(items.Error!.Value.Code, items.Error.Value.Path);
    }

    internal static JsonScalarResult<JsonNullableUnionMap> ToNullableUnionMap(JsonTaggedUnionSchema schema, JsonFieldValue value)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(value);
        var items = ToMap(value, item => ReadNullableUnion(schema, item));
        return items.IsSuccess ? JsonScalarResult<JsonNullableUnionMap>.Success(JsonNullableUnionMap.Create(schema, items.Value))
            : JsonScalarResult<JsonNullableUnionMap>.Failure(items.Error!.Value.Code, items.Error.Value.Path);
    }

    private static JsonScalarResult<JsonFieldValue> FromMap(int depth, int count, Func<int, (string Key, JsonScalarResult<JsonFieldValue> Value)> read)
    {
        if (depth > 64) throw new ArgumentException("JSON nesting exceeds 64 containers.");
        var entries = new KeyValuePair<string, JsonFieldValue>[count];
        for (var index = 0; index < count; index++)
        {
            var (key, item) = read(index);
            if (!item.IsSuccess) return JsonScalarResult<JsonFieldValue>.Failure(item.Error!.Value.Code, Index(index, item.Error.Value.Path));
            entries[index] = new(key, item.Value);
        }
        return JsonScalarResult<JsonFieldValue>.Success(JsonFieldValue.Map(entries));
    }

    private static JsonScalarResult<KeyValuePair<string, T>[]> ToMap<T>(JsonFieldValue value, Func<JsonFieldValue, JsonScalarResult<T>> read)
    {
        if (value.Kind != JsonFieldValueKind.Map) return JsonScalarResult<KeyValuePair<string, T>[]>.Failure(JsonScalarErrorCode.TypeMismatch);
        var source = value.MapItems;
        var entries = new KeyValuePair<string, T>[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            var item = read(source[index].Value);
            if (!item.IsSuccess) return JsonScalarResult<KeyValuePair<string, T>[]>.Failure(item.Error!.Value.Code, Index(index, item.Error.Value.Path));
            entries[index] = new(source[index].Key, item.Value);
        }
        return JsonScalarResult<KeyValuePair<string, T>[]>.Success(entries);
    }

    internal static JsonScalarResult<JsonFieldValue> FromNullableRecordMap(JsonNullableRecordMap value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return FromMap(value.Depth, value.Count, index =>
        {
            var entry = value.At(index);
            return (entry.Key, entry.Value is null ? JsonScalarResult<JsonFieldValue>.Success(JsonFieldValue.Null()) : FromRecord(entry.Value));
        });
    }

    internal static JsonScalarResult<JsonNullableRecordMap> ToNullableRecordMap(JsonScalarRecordSchema schema, JsonFieldValue value)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(value);
        var items = ToMap(value, item => ReadNullableRecord(schema, item));
        return items.IsSuccess ? JsonScalarResult<JsonNullableRecordMap>.Success(JsonNullableRecordMap.Create(schema, items.Value))
            : JsonScalarResult<JsonNullableRecordMap>.Failure(items.Error!.Value.Code, items.Error.Value.Path);
    }

    internal static JsonScalarResult<JsonFieldValue> FromRecordMap(JsonRecordMap value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Depth > 64) throw new ArgumentException("JSON nesting exceeds 64 containers.", nameof(value));
        var entries = new KeyValuePair<string, JsonFieldValue>[value.Count];
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = value.At(index);
            var item = FromRecord(entry.Value);
            if (!item.IsSuccess)
                return JsonScalarResult<JsonFieldValue>.Failure(item.Error!.Value.Code, Index(index, item.Error.Value.Path));
            entries[index] = new(entry.Key, item.Value);
        }
        return JsonScalarResult<JsonFieldValue>.Success(JsonFieldValue.Map(entries));
    }

    internal static JsonScalarResult<JsonRecordMap> ToRecordMap(JsonScalarRecordSchema schema, JsonFieldValue value)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(value);
        if (value.Kind != JsonFieldValueKind.Map) return JsonScalarResult<JsonRecordMap>.Failure(JsonScalarErrorCode.TypeMismatch);
        var source = value.MapItems;
        var entries = new KeyValuePair<string, JsonScalarRecord>[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            var item = ToRecord(schema, source[index].Value);
            if (!item.IsSuccess)
                return JsonScalarResult<JsonRecordMap>.Failure(item.Error!.Value.Code, Index(index, item.Error.Value.Path));
            entries[index] = new(source[index].Key, item.Value);
        }
        return JsonScalarResult<JsonRecordMap>.Success(JsonRecordMap.Create(schema, entries));
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
