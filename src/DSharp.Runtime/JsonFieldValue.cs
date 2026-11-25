using System.Buffers;
using System.Globalization;
using System.Text.Json;

namespace DSharp.Runtime;

// Private building block for JSON-compatible logging fields, not a public API
// or a record, union, Option, or durable-message encoding contract.
internal enum JsonFieldValueKind { Null, Boolean, String, Number, List, Map }
internal sealed class JsonFieldValue
{
    private readonly object? value;
    private JsonFieldValue(JsonFieldValueKind kind, object? value, int depth = 0) { Kind = kind; this.value = value; Depth = depth; }
    internal JsonFieldValueKind Kind { get; }
    internal int Depth { get; }
    internal static JsonFieldValue Null() => new(JsonFieldValueKind.Null, null);
    internal static JsonFieldValue Boolean(bool value) => new(JsonFieldValueKind.Boolean, value);
    internal static JsonFieldValue String(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        JsonScalarCodec.ValidateUtf16(value);
        return new(JsonFieldValueKind.String, value);
    }
    internal static JsonFieldValue Number(JsonExactNumber value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(JsonFieldValueKind.Number, value);
    }
    internal static JsonFieldValue List(JsonFieldValue[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var copy = (JsonFieldValue[])values.Clone();
        var depth = 1;
        foreach (var item in copy)
        {
            if (item is null) throw new ArgumentException("List items require explicit values.", nameof(values));
            depth = Math.Max(depth, 1 + item.Depth);
        }
        if (depth > 64) throw new ArgumentException("JSON nesting exceeds 64 containers.", nameof(values));
        return new(JsonFieldValueKind.List, copy, depth);
    }
    internal static JsonFieldValue Map(KeyValuePair<string, JsonFieldValue>[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var copy = (KeyValuePair<string, JsonFieldValue>[])values.Clone();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var depth = 1;
        foreach (var pair in copy)
        {
            if (pair.Key is null || pair.Value is null) throw new ArgumentException("Map entries require a key and an explicit value.", nameof(values));
            JsonScalarCodec.ValidateUtf16(pair.Key);
            if (!names.Add(pair.Key)) throw new ArgumentException("Map keys must be unique.", nameof(values));
            depth = Math.Max(depth, 1 + pair.Value.Depth);
        }
        if (depth > 64) throw new ArgumentException("JSON nesting exceeds 64 containers.", nameof(values));
        return new(JsonFieldValueKind.Map, copy, depth);
    }
    internal object? Scalar => Kind is JsonFieldValueKind.List or JsonFieldValueKind.Map
        ? throw new InvalidOperationException("A container has no scalar value.") : value;
    internal JsonFieldValue[] ListItems => (JsonFieldValue[])((JsonFieldValue[])value!).Clone();
    internal KeyValuePair<string, JsonFieldValue>[] MapItems => (KeyValuePair<string, JsonFieldValue>[])((KeyValuePair<string, JsonFieldValue>[])value!).Clone();
}

internal static class JsonFieldValueCodec
{
    internal static byte[] Encode(JsonFieldValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var output = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(output)) { Write(writer, value); writer.Flush(); }
        return output.WrittenSpan.ToArray();
    }
    private static void Write(Utf8JsonWriter writer, JsonFieldValue value)
    {
        switch (value.Kind)
        {
            case JsonFieldValueKind.Null: writer.WriteNullValue(); break;
            case JsonFieldValueKind.Boolean: writer.WriteBooleanValue((bool)value.Scalar!); break;
            case JsonFieldValueKind.String: writer.WriteStringValue((string)value.Scalar!); break;
            case JsonFieldValueKind.Number: writer.WriteRawValue(((JsonExactNumber)value.Scalar!).Token); break;
            case JsonFieldValueKind.List:
                writer.WriteStartArray();
                foreach (var item in value.ListItems) Write(writer, item);
                writer.WriteEndArray(); break;
            case JsonFieldValueKind.Map:
                writer.WriteStartObject();
                foreach (var pair in value.MapItems) { writer.WritePropertyName(pair.Key); Write(writer, pair.Value); }
                writer.WriteEndObject(); break;
            default: throw new InvalidOperationException("Unsupported JSON field value.");
        }
    }
    internal static JsonScalarResult<JsonFieldValue> Decode(ReadOnlyMemory<byte> input)
    {
        try
        {
            using var document = JsonDocument.Parse(input);
            return Read(document.RootElement, "$");
        }
        catch (JsonException) { return JsonScalarResult<JsonFieldValue>.Failure(JsonScalarErrorCode.InvalidJson); }
    }
    private static JsonScalarResult<JsonFieldValue> Read(JsonElement element, string path)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null: return JsonScalarResult<JsonFieldValue>.Success(JsonFieldValue.Null());
            case JsonValueKind.True: return JsonScalarResult<JsonFieldValue>.Success(JsonFieldValue.Boolean(true));
            case JsonValueKind.False: return JsonScalarResult<JsonFieldValue>.Success(JsonFieldValue.Boolean(false));
            case JsonValueKind.Number: return JsonScalarResult<JsonFieldValue>.Success(JsonFieldValue.Number(JsonExactNumber.Read(element).Value));
            case JsonValueKind.String:
                var text = JsonScalarCodec.ReadString(element);
                return text.IsSuccess ? JsonScalarResult<JsonFieldValue>.Success(JsonFieldValue.String(text.Value))
                    : JsonScalarResult<JsonFieldValue>.Failure(text.Error!.Value.Code, path);
            case JsonValueKind.Array:
                var items = new List<JsonFieldValue>();
                foreach (var item in element.EnumerateArray())
                {
                    var result = Read(item, Index(path, items.Count));
                    if (!result.IsSuccess) return result;
                    items.Add(result.Value);
                }
                return JsonScalarResult<JsonFieldValue>.Success(JsonFieldValue.List(items.ToArray()));
            case JsonValueKind.Object:
                var pairs = new List<KeyValuePair<string, JsonFieldValue>>();
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                {
                    var entryPath = Index(path, pairs.Count);
                    string name;
                    try { name = property.Name; JsonScalarCodec.ValidateUtf16(name); }
                    catch (InvalidOperationException) { return JsonScalarResult<JsonFieldValue>.Failure(JsonScalarErrorCode.InvalidUnicode, entryPath); }
                    catch (ArgumentException) { return JsonScalarResult<JsonFieldValue>.Failure(JsonScalarErrorCode.InvalidUnicode, entryPath); }
                    if (!names.Add(name)) return JsonScalarResult<JsonFieldValue>.Failure(JsonScalarErrorCode.DuplicateField, entryPath);
                    var result = Read(property.Value, entryPath);
                    if (!result.IsSuccess) return result;
                    pairs.Add(new(name, result.Value));
                }
                return JsonScalarResult<JsonFieldValue>.Success(JsonFieldValue.Map(pairs.ToArray()));
            default: return JsonScalarResult<JsonFieldValue>.Failure(JsonScalarErrorCode.TypeMismatch, path);
        }
    }
    private static string Index(string path, int index) => path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
}
