using System.Buffers;
using System.Globalization;
using System.Text.Json;

namespace DSharp.Runtime;

// Internal typed arrays only: not a public collection API or durable format.
internal static class JsonScalarArrayCodec
{
    internal static byte[] EncodeBooleans(bool[] values) => Encode(values, (writer, value) => writer.WriteBooleanValue(value));
    internal static byte[] EncodeIntegers(int[] values) => Encode(values, (writer, value) => writer.WriteNumberValue(value));
    internal static byte[] EncodeStrings(string[] values) => Encode(values, WriteRequiredString);
    internal static byte[] EncodeNullableBooleans(bool?[] values) => Encode(values, (writer, value) =>
    {
        if (value.HasValue) writer.WriteBooleanValue(value.Value); else writer.WriteNullValue();
    });
    internal static byte[] EncodeNullableIntegers(int?[] values) => Encode(values, (writer, value) =>
    {
        if (value.HasValue) writer.WriteNumberValue(value.Value); else writer.WriteNullValue();
    });
    internal static byte[] EncodeNullableStrings(string?[] values) => Encode(values, (writer, value) =>
    {
        if (value is null) writer.WriteNullValue(); else WriteRequiredString(writer, value);
    });

    internal static byte[] EncodeNestedIntegers(int[][] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var outer = (int[][])values.Clone();
        for (var index = 0; index < outer.Length; index++)
        {
            if (outer[index] is null) throw new ArgumentException("Nested arrays cannot contain null.", nameof(values));
            outer[index] = (int[])outer[index].Clone();
        }
        return Write(writer =>
        {
            writer.WriteStartArray();
            foreach (var inner in outer)
            {
                writer.WriteStartArray();
                foreach (var value in inner) writer.WriteNumberValue(value);
                writer.WriteEndArray();
            }
            writer.WriteEndArray();
        });
    }

    internal static JsonScalarResult<IReadOnlyList<bool>> DecodeBooleans(ReadOnlyMemory<byte> input) =>
        Decode(input, (element, path) => ReadArray(element, path, ReadBoolean));
    internal static JsonScalarResult<IReadOnlyList<int>> DecodeIntegers(ReadOnlyMemory<byte> input) =>
        Decode(input, (element, path) => ReadArray(element, path, ReadInteger));
    internal static JsonScalarResult<IReadOnlyList<string>> DecodeStrings(ReadOnlyMemory<byte> input) =>
        Decode(input, (element, path) => ReadArray(element, path, ReadString));
    internal static JsonScalarResult<IReadOnlyList<bool?>> DecodeNullableBooleans(ReadOnlyMemory<byte> input) =>
        Decode(input, (element, path) => ReadArray(element, path, ReadNullableBoolean));
    internal static JsonScalarResult<IReadOnlyList<int?>> DecodeNullableIntegers(ReadOnlyMemory<byte> input) =>
        Decode(input, (element, path) => ReadArray(element, path, ReadNullableInteger));
    internal static JsonScalarResult<IReadOnlyList<string?>> DecodeNullableStrings(ReadOnlyMemory<byte> input) =>
        Decode(input, (element, path) => ReadArray(element, path, ReadNullableString));
    internal static JsonScalarResult<IReadOnlyList<IReadOnlyList<int>>> DecodeNestedIntegers(ReadOnlyMemory<byte> input) =>
        Decode(input, (element, path) => ReadArray(element, path,
            (inner, innerPath) => ReadArray(inner, innerPath, ReadInteger)));

    private static byte[] Encode<T>(T[] values, Action<Utf8JsonWriter, T> writeValue)
    {
        ArgumentNullException.ThrowIfNull(values);
        var snapshot = (T[])values.Clone();
        return Write(writer =>
        {
            writer.WriteStartArray();
            foreach (var value in snapshot) writeValue(writer, value);
            writer.WriteEndArray();
        });
    }

    private static void WriteRequiredString(Utf8JsonWriter writer, string value)
    {
        if (value is null) throw new ArgumentException("String arrays cannot contain null.", nameof(value));
        JsonScalarCodec.ValidateUtf16(value);
        writer.WriteStringValue(value);
    }

    private static byte[] Write(Action<Utf8JsonWriter> write)
    {
        var output = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(output))
        {
            write(writer);
            writer.Flush();
        }
        return output.WrittenSpan.ToArray();
    }

    private static JsonScalarResult<T> Decode<T>(ReadOnlyMemory<byte> input, Func<JsonElement, string, JsonScalarResult<T>> read)
    {
        try
        {
            using var document = JsonDocument.Parse(input);
            return read(document.RootElement, "$");
        }
        catch (JsonException)
        {
            return JsonScalarResult<T>.Failure(JsonScalarErrorCode.InvalidJson);
        }
    }

    private static JsonScalarResult<IReadOnlyList<T>> ReadArray<T>(JsonElement element, string path,
        Func<JsonElement, string, JsonScalarResult<T>> readElement)
    {
        if (element.ValueKind != JsonValueKind.Array)
            return JsonScalarResult<IReadOnlyList<T>>.Failure(JsonScalarErrorCode.TypeMismatch, path);
        var result = new List<T>();
        var index = 0;
        foreach (var child in element.EnumerateArray())
        {
            var childPath = path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
            var item = readElement(child, childPath);
            if (!item.IsSuccess)
                return JsonScalarResult<IReadOnlyList<T>>.Failure(item.Error!.Value.Code, item.Error.Value.Path);
            result.Add(item.Value);
            index++;
        }
        return JsonScalarResult<IReadOnlyList<T>>.Success(Array.AsReadOnly(result.ToArray()));
    }

    private static JsonScalarResult<T> At<T>(JsonScalarResult<T> result, string path) =>
        result.IsSuccess ? result : JsonScalarResult<T>.Failure(result.Error!.Value.Code, path);
    private static JsonScalarResult<bool> ReadBoolean(JsonElement value, string path) => At(JsonScalarCodec.ReadBoolean(value), path);
    private static JsonScalarResult<int> ReadInteger(JsonElement value, string path) => At(JsonScalarCodec.ReadInteger(value), path);
    private static JsonScalarResult<string> ReadString(JsonElement value, string path) => At(JsonScalarCodec.ReadString(value), path);
    private static JsonScalarResult<bool?> ReadNullableBoolean(JsonElement value, string path) => At(JsonScalarCodec.ReadNullableBoolean(value), path);
    private static JsonScalarResult<int?> ReadNullableInteger(JsonElement value, string path) => At(JsonScalarCodec.ReadNullableInteger(value), path);
    private static JsonScalarResult<string?> ReadNullableString(JsonElement value, string path) => At(JsonScalarCodec.ReadNullableString(value), path);
}
