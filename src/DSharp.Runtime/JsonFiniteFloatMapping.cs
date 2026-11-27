using System.Buffers;
using System.Globalization;
using System.Text.Json;

namespace DSharp.Runtime;

// Internal finite IEEE/tree conversion, not a public D# API, source-literal
// parsing rule or durable message format.
internal static class JsonFiniteFloatMapping
{
    internal static JsonScalarResult<float> ToSingle(JsonFieldValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Kind != JsonFieldValueKind.Number) return JsonScalarResult<float>.Failure(JsonScalarErrorCode.TypeMismatch);
        return ParseSingle(((JsonExactNumber)value.Scalar!).Token);
    }

    private static JsonScalarResult<float> ParseSingle(string token)
    {
        return float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) && float.IsFinite(result)
            ? JsonScalarResult<float>.Success(result) : JsonScalarResult<float>.Failure(JsonScalarErrorCode.InvalidFloat);
    }

    internal static JsonScalarResult<double> ToDouble(JsonFieldValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Kind != JsonFieldValueKind.Number) return JsonScalarResult<double>.Failure(JsonScalarErrorCode.TypeMismatch);
        return ParseDouble(((JsonExactNumber)value.Scalar!).Token);
    }

    private static JsonScalarResult<double> ParseDouble(string token)
    {
        return double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) && double.IsFinite(result)
            ? JsonScalarResult<double>.Success(result) : JsonScalarResult<double>.Failure(JsonScalarErrorCode.InvalidFloat);
    }

    internal static JsonScalarResult<float> ReadSingle(JsonElement value) => value.ValueKind == JsonValueKind.Number
        ? ParseSingle(value.GetRawText()) : JsonScalarResult<float>.Failure(JsonScalarErrorCode.TypeMismatch);
    internal static JsonScalarResult<double> ReadDouble(JsonElement value) => value.ValueKind == JsonValueKind.Number
        ? ParseDouble(value.GetRawText()) : JsonScalarResult<double>.Failure(JsonScalarErrorCode.TypeMismatch);
    internal static JsonScalarResult<float?> ReadNullableSingle(JsonElement value) => value.ValueKind == JsonValueKind.Null
        ? JsonScalarResult<float?>.Success(null) : Nullable(ReadSingle(value));
    internal static JsonScalarResult<double?> ReadNullableDouble(JsonElement value) => value.ValueKind == JsonValueKind.Null
        ? JsonScalarResult<double?>.Success(null) : Nullable(ReadDouble(value));

    internal static JsonScalarResult<float[]> ToSingles(JsonFieldValue value) => ToArray(value, ToSingle);
    internal static JsonScalarResult<double[]> ToDoubles(JsonFieldValue value) => ToArray(value, ToDouble);
    internal static JsonScalarResult<float?[]> ToNullableSingles(JsonFieldValue value) => ToArray<float?>(value,
        item => item.Kind == JsonFieldValueKind.Null ? JsonScalarResult<float?>.Success(null) : Nullable(ToSingle(item)));
    internal static JsonScalarResult<double?[]> ToNullableDoubles(JsonFieldValue value) => ToArray<double?>(value,
        item => item.Kind == JsonFieldValueKind.Null ? JsonScalarResult<double?>.Success(null) : Nullable(ToDouble(item)));

    private static JsonScalarResult<T?> Nullable<T>(JsonScalarResult<T> result) where T : struct =>
        result.IsSuccess ? JsonScalarResult<T?>.Success(result.Value)
            : JsonScalarResult<T?>.Failure(result.Error!.Value.Code, result.Error.Value.Path);

    private static JsonScalarResult<T[]> ToArray<T>(JsonFieldValue value, Func<JsonFieldValue, JsonScalarResult<T>> convert)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Kind != JsonFieldValueKind.List) return JsonScalarResult<T[]>.Failure(JsonScalarErrorCode.TypeMismatch);
        var items = value.ListItems;
        var result = new T[items.Length];
        for (var i = 0; i < result.Length; i++)
        {
            var item = convert(items[i]);
            if (!item.IsSuccess)
                return JsonScalarResult<T[]>.Failure(item.Error!.Value.Code, "$[" + i.ToString(CultureInfo.InvariantCulture) + "]");
            result[i] = item.Value;
        }
        return JsonScalarResult<T[]>.Success(result);
    }

    internal static JsonScalarResult<JsonFieldValue> FromSingles(float[] values) => FromArray(values, FromSingle);
    internal static JsonScalarResult<JsonFieldValue> FromDoubles(double[] values) => FromArray(values, FromDouble);
    internal static JsonScalarResult<JsonFieldValue> FromNullableSingles(float?[] values) =>
        FromArray(values, value => value.HasValue ? FromSingle(value.Value) : JsonScalarResult<JsonFieldValue>.Success(JsonFieldValue.Null()));
    internal static JsonScalarResult<JsonFieldValue> FromNullableDoubles(double?[] values) =>
        FromArray(values, value => value.HasValue ? FromDouble(value.Value) : JsonScalarResult<JsonFieldValue>.Success(JsonFieldValue.Null()));

    private static JsonScalarResult<JsonFieldValue> FromArray<T>(T[] values, Func<T, JsonScalarResult<JsonFieldValue>> convert)
    {
        ArgumentNullException.ThrowIfNull(values);
        var snapshot = (T[])values.Clone();
        var items = new JsonFieldValue[snapshot.Length];
        for (var i = 0; i < snapshot.Length; i++)
        {
            var item = convert(snapshot[i]);
            if (!item.IsSuccess)
                return JsonScalarResult<JsonFieldValue>.Failure(item.Error!.Value.Code, "$[" + i.ToString(CultureInfo.InvariantCulture) + "]");
            items[i] = item.Value;
        }
        return JsonScalarResult<JsonFieldValue>.Success(JsonFieldValue.List(items));
    }

    internal static JsonScalarResult<JsonFieldValue> FromSingle(float value)
    {
        if (!float.IsFinite(value)) return JsonScalarResult<JsonFieldValue>.Failure(JsonScalarErrorCode.InvalidFloat);
        var output = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(output)) { writer.WriteNumberValue(value); writer.Flush(); }
        return FromNumber(output.WrittenMemory);
    }

    internal static JsonScalarResult<JsonFieldValue> FromDouble(double value)
    {
        if (!double.IsFinite(value)) return JsonScalarResult<JsonFieldValue>.Failure(JsonScalarErrorCode.InvalidFloat);
        var output = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(output)) { writer.WriteNumberValue(value); writer.Flush(); }
        return FromNumber(output.WrittenMemory);
    }

    private static JsonScalarResult<JsonFieldValue> FromNumber(ReadOnlyMemory<byte> input)
    {
        var number = JsonExactNumberCodec.Decode(input);
        return number.IsSuccess ? JsonScalarResult<JsonFieldValue>.Success(JsonFieldValue.Number(number.Value))
            : JsonScalarResult<JsonFieldValue>.Failure(number.Error!.Value.Code, number.Error.Value.Path);
    }
}
