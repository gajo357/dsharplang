using System.Buffers;
using System.Text.Json;

namespace DSharp.Runtime;

// One-way internal conversion of already-typed finite IEEE values. This does
// not define float parsing, a public D# API or a durable message format.
internal static class JsonFiniteFloatMapping
{
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
