using System.Globalization;
using System.Text.Json;

namespace DSharp.Runtime;

// Internal, explicitly supplied shapes; not a public schema or wire contract.
internal enum JsonCollectionShapeKind { Boolean, Integer, String, Nullable, List, Map, Record, Union }

internal sealed class JsonCollectionShape
{
    private JsonCollectionShape(JsonCollectionShapeKind kind, JsonCollectionShape? child,
        JsonScalarRecordSchema? record = null, JsonTaggedUnionSchema? union = null)
    {
        Kind = kind;
        Child = child;
        RecordSchema = record;
        UnionSchema = union;
        Height = child is null ? record?.Depth ?? union?.Depth ?? 1 : checked(child.Height + 1);
        if (Height > 64) throw new ArgumentException("Collection descriptor exceeds the supported depth.", nameof(child));
    }

    internal JsonCollectionShapeKind Kind { get; }
    internal JsonCollectionShape? Child { get; }
    internal JsonScalarRecordSchema? RecordSchema { get; }
    internal JsonTaggedUnionSchema? UnionSchema { get; }
    internal int Height { get; }
    internal static JsonCollectionShape Boolean() => new(JsonCollectionShapeKind.Boolean, null);
    internal static JsonCollectionShape Integer() => new(JsonCollectionShapeKind.Integer, null);
    internal static JsonCollectionShape String() => new(JsonCollectionShapeKind.String, null);
    internal static JsonCollectionShape Record(JsonScalarRecordSchema schema) => new(JsonCollectionShapeKind.Record, null, record: schema ?? throw new ArgumentNullException(nameof(schema)));
    internal static JsonCollectionShape Union(JsonTaggedUnionSchema schema) => new(JsonCollectionShapeKind.Union, null, union: schema ?? throw new ArgumentNullException(nameof(schema)));
    internal static JsonCollectionShape Nullable(JsonCollectionShape child) => new(JsonCollectionShapeKind.Nullable, child ?? throw new ArgumentNullException(nameof(child)));
    internal static JsonCollectionShape List(JsonCollectionShape child) => new(JsonCollectionShapeKind.List, child ?? throw new ArgumentNullException(nameof(child)));
    internal static JsonCollectionShape Map(JsonCollectionShape child) => new(JsonCollectionShapeKind.Map, child ?? throw new ArgumentNullException(nameof(child)));
}

internal static class JsonCollectionCodec
{
    internal static JsonScalarResult<JsonFieldValue> Decode(JsonCollectionShape shape, ReadOnlyMemory<byte> input)
    {
        ArgumentNullException.ThrowIfNull(shape);
        try
        {
            using var document = JsonDocument.Parse(input);
            return Read(shape, document.RootElement);
        }
        catch (JsonException) { return JsonScalarResult<JsonFieldValue>.Failure(JsonScalarErrorCode.InvalidJson); }
    }

    private static JsonScalarResult<JsonFieldValue> Read(JsonCollectionShape shape, JsonElement element)
    {
        switch (shape.Kind)
        {
            case JsonCollectionShapeKind.Nullable:
                return element.ValueKind == JsonValueKind.Null
                    ? JsonScalarResult<JsonFieldValue>.Success(JsonFieldValue.Null()) : Read(shape.Child!, element);
            case JsonCollectionShapeKind.Boolean:
                var boolean = JsonScalarCodec.ReadBoolean(element);
                return boolean.IsSuccess ? JsonScalarResult<JsonFieldValue>.Success(JsonFieldValue.Boolean(boolean.Value))
                    : JsonScalarResult<JsonFieldValue>.Failure(boolean.Error!.Value.Code);
            case JsonCollectionShapeKind.Integer:
                var integer = JsonScalarCodec.ReadInteger(element);
                return integer.IsSuccess ? JsonScalarResult<JsonFieldValue>.Success(JsonFieldValue.Number(JsonExactNumber.Read(element).Value))
                    : JsonScalarResult<JsonFieldValue>.Failure(integer.Error!.Value.Code);
            case JsonCollectionShapeKind.String:
                var text = JsonScalarCodec.ReadString(element);
                return text.IsSuccess ? JsonScalarResult<JsonFieldValue>.Success(JsonFieldValue.String(text.Value))
                    : JsonScalarResult<JsonFieldValue>.Failure(text.Error!.Value.Code);
            case JsonCollectionShapeKind.Record:
                var record = JsonScalarRecordCodec.ReadRecord(shape.RecordSchema!, element, "$");
                return record.IsSuccess ? JsonTypedFieldMapping.FromRecord(record.Value)
                    : JsonScalarResult<JsonFieldValue>.Failure(record.Error!.Value.Code, record.Error.Value.Path);
            case JsonCollectionShapeKind.Union:
                var union = JsonTaggedUnionCodec.Read(shape.UnionSchema!, element);
                return union.IsSuccess ? JsonTypedFieldMapping.FromUnion(union.Value)
                    : JsonScalarResult<JsonFieldValue>.Failure(union.Error!.Value.Code, union.Error.Value.Path);
            case JsonCollectionShapeKind.List:
                if (element.ValueKind != JsonValueKind.Array) return JsonScalarResult<JsonFieldValue>.Failure(JsonScalarErrorCode.TypeMismatch);
                var items = new List<JsonFieldValue>();
                foreach (var child in element.EnumerateArray())
                {
                    var result = Read(shape.Child!, child);
                    if (!result.IsSuccess) return Prefix(result.Error!.Value, items.Count);
                    items.Add(result.Value);
                }
                return JsonScalarResult<JsonFieldValue>.Success(JsonFieldValue.ListFromBuffer(items));
            case JsonCollectionShapeKind.Map:
                if (element.ValueKind != JsonValueKind.Object) return JsonScalarResult<JsonFieldValue>.Failure(JsonScalarErrorCode.TypeMismatch);
                var pairs = new List<KeyValuePair<string, JsonFieldValue>>();
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                {
                    string name;
                    try { name = property.Name; JsonScalarCodec.ValidateUtf16(name); }
                    catch (InvalidOperationException) { return At(JsonScalarErrorCode.InvalidUnicode, pairs.Count); }
                    catch (ArgumentException) { return At(JsonScalarErrorCode.InvalidUnicode, pairs.Count); }
                    if (!names.Add(name)) return At(JsonScalarErrorCode.DuplicateField, pairs.Count);
                    var result = Read(shape.Child!, property.Value);
                    if (!result.IsSuccess) return Prefix(result.Error!.Value, pairs.Count);
                    pairs.Add(new(name, result.Value));
                }
                return JsonScalarResult<JsonFieldValue>.Success(JsonFieldValue.MapFromBuffer(pairs));
            default: throw new InvalidOperationException("Unsupported collection descriptor.");
        }
    }

    private static JsonScalarResult<JsonFieldValue> At(JsonScalarErrorCode code, int index) =>
        JsonScalarResult<JsonFieldValue>.Failure(code, Index(index));
    private static JsonScalarResult<JsonFieldValue> Prefix(JsonScalarError error, int index) =>
        JsonScalarResult<JsonFieldValue>.Failure(error.Code, Index(index) + error.Path[1..]);
    private static string Index(int index) => "$[" + index.ToString(CultureInfo.InvariantCulture) + "]";
}
