using System.Buffers;
using System.Text.Json;

namespace DSharp.Runtime;

// Explicit internal scalar descriptors, not reflection or a public D# schema.
internal enum JsonRecordFieldKind { Boolean, Integer, String, NullableBoolean, NullableInteger, NullableString, Record, NullableRecord }
internal sealed record JsonRecordField(string Name, JsonRecordFieldKind Kind, bool Optional = false, JsonScalarRecordSchema? NestedSchema = null);

internal sealed class JsonScalarRecordSchema
{
    private readonly JsonRecordField[] fields;
    private readonly Dictionary<string, int> indexes;

    internal JsonScalarRecordSchema(JsonRecordField[] fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        this.fields = (JsonRecordField[])fields.Clone();
        indexes = new(StringComparer.Ordinal);
        var depth = 1;
        for (var index = 0; index < this.fields.Length; index++)
        {
            var field = this.fields[index];
            if (field is null || string.IsNullOrEmpty(field.Name))
                throw new ArgumentException("Record fields require nonempty names.", nameof(fields));
            JsonScalarCodec.ValidateUtf16(field.Name);
            if (!Enum.IsDefined(field.Kind) || (field.Optional && !IsNullable(field.Kind)))
                throw new ArgumentException("The field descriptor is not supported.", nameof(fields));
            var isRecord = field.Kind is JsonRecordFieldKind.Record or JsonRecordFieldKind.NullableRecord;
            if (isRecord != (field.NestedSchema is not null))
                throw new ArgumentException("Nested fields require exactly one child descriptor.", nameof(fields));
            if (field.NestedSchema is { } nested)
                depth = Math.Max(depth, checked(nested.Depth + 1));
            if (depth > 64)
                throw new ArgumentException("Record descriptor depth exceeds the supported limit.", nameof(fields));
            if (!indexes.TryAdd(field.Name, index))
                throw new ArgumentException("Record field names must be distinct.", nameof(fields));
        }
        Depth = depth;
    }

    internal int Count => fields.Length;
    internal int Depth { get; }
    internal JsonRecordField FieldAt(int index) => fields[index];
    internal bool TryFind(string name, out int index) => indexes.TryGetValue(name, out index);
    internal static bool IsNullable(JsonRecordFieldKind kind) => kind is
        JsonRecordFieldKind.NullableBoolean or JsonRecordFieldKind.NullableInteger or JsonRecordFieldKind.NullableString or JsonRecordFieldKind.NullableRecord;

    // Only schema-owned names appear in paths, JSON-escaped to keep control
    // characters and punctuation safe. Unknown input names are never echoed.
    internal string PathAt(int index) => "$[" + JsonSerializer.Serialize(fields[index].Name) + "]";
}

internal sealed class JsonScalarRecord
{
    private readonly object?[] values;
    private readonly bool[] present;

    private JsonScalarRecord(JsonScalarRecordSchema schema, object?[] values, bool[] present)
    {
        Schema = schema;
        this.values = values;
        this.present = present;
    }

    internal JsonScalarRecordSchema Schema { get; }
    internal object? ValueAt(int index) => values[index];
    internal bool IsPresent(int index) => present[index];

    internal static JsonScalarRecord Create(JsonScalarRecordSchema schema, object?[] values, bool[] present)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(present);
        var copiedValues = (object?[])values.Clone();
        var copiedPresent = (bool[])present.Clone();
        if (copiedValues.Length != schema.Count || copiedPresent.Length != schema.Count)
            throw new ArgumentException("Record data must match its descriptor.");
        for (var index = 0; index < schema.Count; index++)
        {
            var field = schema.FieldAt(index);
            var value = copiedValues[index];
            if (!copiedPresent[index])
            {
                if (!field.Optional || value is not null)
                    throw new ArgumentException("Missing record data must be optional and empty.");
                continue;
            }
            if (!Matches(field, value))
                throw new ArgumentException("Record data does not match its descriptor.");
            if (value is string text) JsonScalarCodec.ValidateUtf16(text);
        }
        return new JsonScalarRecord(schema, copiedValues, copiedPresent);
    }

    private static bool Matches(JsonRecordField field, object? value) => field.Kind switch
    {
        JsonRecordFieldKind.Boolean => value is bool,
        JsonRecordFieldKind.Integer => value is int,
        JsonRecordFieldKind.String => value is string,
        JsonRecordFieldKind.NullableBoolean => value is null or bool,
        JsonRecordFieldKind.NullableInteger => value is null or int,
        JsonRecordFieldKind.NullableString => value is null or string,
        JsonRecordFieldKind.Record => value is JsonScalarRecord record && ReferenceEquals(record.Schema, field.NestedSchema),
        JsonRecordFieldKind.NullableRecord => value is null || value is JsonScalarRecord record && ReferenceEquals(record.Schema, field.NestedSchema),
        _ => false
    };
}

internal static class JsonScalarRecordCodec
{
    internal static byte[] Encode(JsonScalarRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var output = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(output))
        {
            WriteRecord(writer, record);
            writer.Flush();
        }
        return output.WrittenSpan.ToArray();
    }

    internal static JsonScalarResult<JsonScalarRecord> Decode(JsonScalarRecordSchema schema, ReadOnlyMemory<byte> input)
    {
        ArgumentNullException.ThrowIfNull(schema);
        try
        {
            using var document = JsonDocument.Parse(input);
            return ReadRecord(schema, document.RootElement, "$");
        }
        catch (JsonException)
        {
            return JsonScalarResult<JsonScalarRecord>.Failure(JsonScalarErrorCode.InvalidJson);
        }
    }

    private static JsonScalarResult<JsonScalarRecord> ReadRecord(JsonScalarRecordSchema schema, JsonElement element, string objectPath)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return JsonScalarResult<JsonScalarRecord>.Failure(JsonScalarErrorCode.TypeMismatch, objectPath);
        var values = new object?[schema.Count];
        var present = new bool[schema.Count];
        foreach (var property in element.EnumerateObject())
        {
            string name;
            try { name = property.Name; JsonScalarCodec.ValidateUtf16(name); }
            catch (InvalidOperationException) { return JsonScalarResult<JsonScalarRecord>.Failure(JsonScalarErrorCode.InvalidUnicode, objectPath); }
            catch (ArgumentException) { return JsonScalarResult<JsonScalarRecord>.Failure(JsonScalarErrorCode.InvalidUnicode, objectPath); }
            if (!schema.TryFind(name, out var index))
                return JsonScalarResult<JsonScalarRecord>.Failure(JsonScalarErrorCode.UnknownField, objectPath);
            var path = objectPath + schema.PathAt(index)[1..];
            if (present[index])
                return JsonScalarResult<JsonScalarRecord>.Failure(JsonScalarErrorCode.DuplicateField, path);
            var field = schema.FieldAt(index);
            JsonScalarResult<object?> value;
            if (field.Kind is JsonRecordFieldKind.Record or JsonRecordFieldKind.NullableRecord)
            {
                value = field.Kind == JsonRecordFieldKind.NullableRecord && property.Value.ValueKind == JsonValueKind.Null
                    ? JsonScalarResult<object?>.Success(null)
                    : Box(ReadRecord(field.NestedSchema!, property.Value, path));
            }
            else
            {
                var scalar = ReadValue(property.Value, field.Kind);
                value = scalar.IsSuccess ? scalar : JsonScalarResult<object?>.Failure(scalar.Error!.Value.Code, path);
            }
            if (!value.IsSuccess)
                return JsonScalarResult<JsonScalarRecord>.Failure(value.Error!.Value.Code, value.Error.Value.Path);
            values[index] = value.Value;
            present[index] = true;
        }
        for (var index = 0; index < schema.Count; index++)
            if (!present[index] && !schema.FieldAt(index).Optional)
                return JsonScalarResult<JsonScalarRecord>.Failure(JsonScalarErrorCode.MissingField, objectPath + schema.PathAt(index)[1..]);
        return JsonScalarResult<JsonScalarRecord>.Success(JsonScalarRecord.Create(schema, values, present));
    }

    private static JsonScalarResult<object?> ReadValue(JsonElement value, JsonRecordFieldKind kind) => kind switch
    {
        JsonRecordFieldKind.Boolean => Box(JsonScalarCodec.ReadBoolean(value)),
        JsonRecordFieldKind.Integer => Box(JsonScalarCodec.ReadInteger(value)),
        JsonRecordFieldKind.String => Box(JsonScalarCodec.ReadString(value)),
        JsonRecordFieldKind.NullableBoolean => Box(JsonScalarCodec.ReadNullableBoolean(value)),
        JsonRecordFieldKind.NullableInteger => Box(JsonScalarCodec.ReadNullableInteger(value)),
        JsonRecordFieldKind.NullableString => Box(JsonScalarCodec.ReadNullableString(value)),
        _ => throw new InvalidOperationException("Unsupported record descriptor.")
    };

    private static JsonScalarResult<object?> Box<T>(JsonScalarResult<T> value) => value.IsSuccess
        ? JsonScalarResult<object?>.Success(value.Value)
        : JsonScalarResult<object?>.Failure(value.Error!.Value.Code, value.Error.Value.Path);

    private static void WriteRecord(Utf8JsonWriter writer, JsonScalarRecord record)
    {
        writer.WriteStartObject();
        for (var index = 0; index < record.Schema.Count; index++)
        {
            if (!record.IsPresent(index)) continue;
            var field = record.Schema.FieldAt(index);
            writer.WritePropertyName(field.Name);
            WriteValue(writer, field.Kind, record.ValueAt(index));
        }
        writer.WriteEndObject();
    }

    private static void WriteValue(Utf8JsonWriter writer, JsonRecordFieldKind kind, object? value)
    {
        if (value is null) { writer.WriteNullValue(); return; }
        switch (kind)
        {
            case JsonRecordFieldKind.Boolean:
            case JsonRecordFieldKind.NullableBoolean: writer.WriteBooleanValue((bool)value); break;
            case JsonRecordFieldKind.Integer:
            case JsonRecordFieldKind.NullableInteger: writer.WriteNumberValue((int)value); break;
            case JsonRecordFieldKind.String:
            case JsonRecordFieldKind.NullableString: writer.WriteStringValue((string)value); break;
            case JsonRecordFieldKind.Record:
            case JsonRecordFieldKind.NullableRecord: WriteRecord(writer, (JsonScalarRecord)value); break;
            default: throw new InvalidOperationException("Unsupported record descriptor.");
        }
    }
}
