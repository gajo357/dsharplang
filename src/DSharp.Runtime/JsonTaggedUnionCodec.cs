using System.Buffers;
using System.Text;
using System.Text.Json;

namespace DSharp.Runtime;

// A private experimental shape, not an adopted D# union or durable wire format.
internal sealed record JsonTaggedUnionCase(string Name, JsonScalarRecordSchema? Payload = null);

internal sealed class JsonTaggedUnionSchema
{
    private readonly JsonTaggedUnionCase[] cases;
    private readonly Dictionary<string, int> indexes;

    internal JsonTaggedUnionSchema(JsonTaggedUnionCase[] cases)
    {
        ArgumentNullException.ThrowIfNull(cases);
        this.cases = (JsonTaggedUnionCase[])cases.Clone();
        if (this.cases.Length == 0) throw new ArgumentException("At least one case is required.", nameof(cases));
        indexes = new(StringComparer.Ordinal);
        var depth = 1;
        for (var index = 0; index < this.cases.Length; index++)
        {
            var item = this.cases[index];
            if (item is null || string.IsNullOrEmpty(item.Name))
                throw new ArgumentException("Cases require nonempty names.", nameof(cases));
            JsonScalarCodec.ValidateUtf16(item.Name);
            if (item.Payload is { Depth: > 63 })
                throw new ArgumentException("Union descriptor depth exceeds the supported limit.", nameof(cases));
            if (item.Payload is { } payload) depth = Math.Max(depth, checked(payload.Depth + 1));
            if (!indexes.TryAdd(item.Name, index))
                throw new ArgumentException("Case names must be distinct.", nameof(cases));
        }
        Depth = depth;
    }

    internal int Count => cases.Length;
    internal int Depth { get; }
    internal JsonTaggedUnionCase CaseAt(int index) => cases[index];
    internal bool TryFind(string name, out int index) => indexes.TryGetValue(name, out index);
}

internal sealed class JsonTaggedUnion
{
    private JsonTaggedUnion(JsonTaggedUnionSchema schema, int caseIndex, JsonScalarRecord? payload)
    {
        Schema = schema;
        CaseIndex = caseIndex;
        Payload = payload;
    }

    internal JsonTaggedUnionSchema Schema { get; }
    internal int CaseIndex { get; }
    internal JsonScalarRecord? Payload { get; }

    internal static JsonTaggedUnion Create(JsonTaggedUnionSchema schema, int caseIndex, JsonScalarRecord? payload = null)
    {
        ArgumentNullException.ThrowIfNull(schema);
        if ((uint)caseIndex >= (uint)schema.Count) throw new ArgumentOutOfRangeException(nameof(caseIndex));
        var descriptor = schema.CaseAt(caseIndex).Payload;
        if (descriptor is null ? payload is not null : payload is null || !ReferenceEquals(payload.Schema, descriptor))
            throw new ArgumentException("Union payload must match its case descriptor.", nameof(payload));
        return new JsonTaggedUnion(schema, caseIndex, payload);
    }
}

internal static class JsonTaggedUnionCodec
{
    private const string CasePath = "$[\"case\"]";
    private const string ValuePath = "$[\"value\"]";

    internal static byte[] Encode(JsonTaggedUnion value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var output = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject();
            writer.WriteString("case", value.Schema.CaseAt(value.CaseIndex).Name);
            if (value.Payload is { } payload)
            {
                writer.WritePropertyName("value");
                writer.WriteRawValue(JsonScalarRecordCodec.Encode(payload));
            }
            writer.WriteEndObject();
            writer.Flush();
        }
        return output.WrittenSpan.ToArray();
    }

    internal static JsonScalarResult<JsonTaggedUnion> Decode(JsonTaggedUnionSchema schema, ReadOnlyMemory<byte> input)
    {
        ArgumentNullException.ThrowIfNull(schema);
        try
        {
            using var document = JsonDocument.Parse(input);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return JsonScalarResult<JsonTaggedUnion>.Failure(JsonScalarErrorCode.TypeMismatch);
            JsonElement caseElement = default, valueElement = default;
            var hasCase = false;
            var hasValue = false;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                string name;
                try { name = property.Name; JsonScalarCodec.ValidateUtf16(name); }
                catch (InvalidOperationException) { return JsonScalarResult<JsonTaggedUnion>.Failure(JsonScalarErrorCode.InvalidUnicode); }
                catch (ArgumentException) { return JsonScalarResult<JsonTaggedUnion>.Failure(JsonScalarErrorCode.InvalidUnicode); }
                switch (name)
                {
                    case "case":
                        if (hasCase) return JsonScalarResult<JsonTaggedUnion>.Failure(JsonScalarErrorCode.DuplicateField, CasePath);
                        hasCase = true; caseElement = property.Value; break;
                    case "value":
                        if (hasValue) return JsonScalarResult<JsonTaggedUnion>.Failure(JsonScalarErrorCode.DuplicateField, ValuePath);
                        hasValue = true; valueElement = property.Value; break;
                    default: return JsonScalarResult<JsonTaggedUnion>.Failure(JsonScalarErrorCode.UnknownField);
                }
            }
            if (!hasCase) return JsonScalarResult<JsonTaggedUnion>.Failure(JsonScalarErrorCode.MissingField, CasePath);
            var tag = JsonScalarCodec.ReadString(caseElement);
            if (!tag.IsSuccess) return JsonScalarResult<JsonTaggedUnion>.Failure(tag.Error!.Value.Code, CasePath);
            if (!schema.TryFind(tag.Value, out var caseIndex))
                return JsonScalarResult<JsonTaggedUnion>.Failure(JsonScalarErrorCode.UnknownCase, CasePath);
            var descriptor = schema.CaseAt(caseIndex).Payload;
            if (descriptor is null)
            {
                if (hasValue) return JsonScalarResult<JsonTaggedUnion>.Failure(JsonScalarErrorCode.TypeMismatch, ValuePath);
                return JsonScalarResult<JsonTaggedUnion>.Success(JsonTaggedUnion.Create(schema, caseIndex));
            }
            if (!hasValue) return JsonScalarResult<JsonTaggedUnion>.Failure(JsonScalarErrorCode.MissingField, ValuePath);
            var payload = JsonScalarRecordCodec.Decode(descriptor, Encoding.UTF8.GetBytes(valueElement.GetRawText()));
            if (!payload.IsSuccess)
                return JsonScalarResult<JsonTaggedUnion>.Failure(payload.Error!.Value.Code, ValuePath + payload.Error.Value.Path[1..]);
            return JsonScalarResult<JsonTaggedUnion>.Success(JsonTaggedUnion.Create(schema, caseIndex, payload.Value));
        }
        catch (JsonException)
        {
            return JsonScalarResult<JsonTaggedUnion>.Failure(JsonScalarErrorCode.InvalidJson);
        }
    }
}
