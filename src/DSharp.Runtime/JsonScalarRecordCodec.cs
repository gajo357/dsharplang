using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace DSharp.Runtime;

// Explicit internal scalar descriptors, not reflection or a public D# schema.
internal enum JsonRecordFieldKind { Boolean, Integer, String, NullableBoolean, NullableInteger, NullableString, Record, NullableRecord, RecordArray, NullableRecordArray, BooleanArray, IntegerArray, StringArray, NullableBooleanArray, NullableIntegerArray, NullableStringArray, BooleanNullableElementsArray, IntegerNullableElementsArray, StringNullableElementsArray, NullableBooleanNullableElementsArray, NullableIntegerNullableElementsArray, NullableStringNullableElementsArray, Union, NullableUnion, ExactNumber, NullableExactNumber, ExactNumberArray, NullableExactNumberArray, ExactNumberNullableElementsArray, NullableExactNumberNullableElementsArray, UnionArray, NullableUnionArray, UnionNullableElementsArray, NullableUnionNullableElementsArray, FieldValue, NullableFieldValue, RecordMap, NullableRecordMap, BooleanMap, IntegerMap, StringMap, ExactNumberMap, NullableBooleanMap, NullableIntegerMap, NullableStringMap, NullableExactNumberMap, BooleanNullableValuesMap, IntegerNullableValuesMap, StringNullableValuesMap, ExactNumberNullableValuesMap, NullableBooleanNullableValuesMap, NullableIntegerNullableValuesMap, NullableStringNullableValuesMap, NullableExactNumberNullableValuesMap, UnionMap, NullableUnionMap }
internal sealed record JsonRecordField(string Name, JsonRecordFieldKind Kind, bool Optional = false, JsonScalarRecordSchema? NestedSchema = null, JsonTaggedUnionSchema? UnionSchema = null);

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
            var isArray = field.Kind is JsonRecordFieldKind.RecordArray or JsonRecordFieldKind.NullableRecordArray;
            var isMap = field.Kind is JsonRecordFieldKind.RecordMap or JsonRecordFieldKind.NullableRecordMap;
            var isRecord = isArray || isMap || field.Kind is JsonRecordFieldKind.Record or JsonRecordFieldKind.NullableRecord;
            var isScalarArray = JsonRecordScalarArray.ElementKind(field.Kind) is not null;
            var isScalarMap = JsonRecordScalarMap.ElementKind(field.Kind) is not null;
            var isUnionArray = field.Kind is JsonRecordFieldKind.UnionArray or JsonRecordFieldKind.NullableUnionArray or
                JsonRecordFieldKind.UnionNullableElementsArray or JsonRecordFieldKind.NullableUnionNullableElementsArray;
            var isUnionMap = field.Kind is JsonRecordFieldKind.UnionMap or JsonRecordFieldKind.NullableUnionMap;
            var isUnion = isUnionArray || isUnionMap || field.Kind is JsonRecordFieldKind.Union or JsonRecordFieldKind.NullableUnion;
            if (isRecord != (field.NestedSchema is not null))
                throw new ArgumentException("Nested fields require exactly one child descriptor.", nameof(fields));
            if (isUnion != (field.UnionSchema is not null))
                throw new ArgumentException("Union fields require exactly one union descriptor.", nameof(fields));
            if (field.NestedSchema is { } nested)
                depth = Math.Max(depth, checked(nested.Depth + (isArray || isMap ? 2 : 1)));
            else if (field.UnionSchema is { } union)
                depth = Math.Max(depth, checked(union.Depth + (isUnionArray || isUnionMap ? 2 : 1)));
            else if (isScalarArray || isScalarMap)
                depth = Math.Max(depth, 2);
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
        JsonRecordFieldKind.NullableBoolean or JsonRecordFieldKind.NullableInteger or JsonRecordFieldKind.NullableString or JsonRecordFieldKind.NullableRecord or JsonRecordFieldKind.NullableRecordArray or
        JsonRecordFieldKind.NullableBooleanArray or JsonRecordFieldKind.NullableIntegerArray or JsonRecordFieldKind.NullableStringArray or
        JsonRecordFieldKind.NullableBooleanNullableElementsArray or JsonRecordFieldKind.NullableIntegerNullableElementsArray or JsonRecordFieldKind.NullableStringNullableElementsArray or JsonRecordFieldKind.NullableUnion or JsonRecordFieldKind.NullableExactNumber or JsonRecordFieldKind.NullableExactNumberArray or JsonRecordFieldKind.NullableExactNumberNullableElementsArray or JsonRecordFieldKind.NullableUnionArray or JsonRecordFieldKind.NullableUnionNullableElementsArray or JsonRecordFieldKind.NullableFieldValue or JsonRecordFieldKind.NullableRecordMap or
        JsonRecordFieldKind.NullableBooleanMap or JsonRecordFieldKind.NullableIntegerMap or JsonRecordFieldKind.NullableStringMap or JsonRecordFieldKind.NullableExactNumberMap or
        JsonRecordFieldKind.NullableBooleanNullableValuesMap or JsonRecordFieldKind.NullableIntegerNullableValuesMap or JsonRecordFieldKind.NullableStringNullableValuesMap or JsonRecordFieldKind.NullableExactNumberNullableValuesMap or JsonRecordFieldKind.NullableUnionMap;

    // Only schema-owned names appear in paths, JSON-escaped to keep control
    // characters and punctuation safe. Unknown input names are never echoed.
    internal string PathAt(int index) => "$[" + JsonSerializer.Serialize(fields[index].Name) + "]";
}

internal sealed class JsonScalarRecord
{
    private readonly object?[] values;
    private readonly bool[] present;

    private JsonScalarRecord(JsonScalarRecordSchema schema, object?[] values, bool[] present, int depth)
    {
        Schema = schema;
        this.values = values;
        this.present = present;
        Depth = depth;
    }

    internal JsonScalarRecordSchema Schema { get; }
    internal int Depth { get; }
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
        var depth = 1;
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
            depth = Math.Max(depth, 1 + ValueDepth(value));
        }
        if (depth > 64) throw new ArgumentException("JSON nesting exceeds 64 containers.");
        return new JsonScalarRecord(schema, copiedValues, copiedPresent, depth);
    }

    private static int ValueDepth(object? value) => value switch
    {
        JsonFieldValue tree => tree.Depth,
        JsonScalarRecord record => record.Depth,
        JsonTaggedUnion union => union.Depth,
        JsonRecordArray array => array.Depth,
        JsonRecordMap map => map.Depth,
        JsonUnionMap map => map.Depth,
        JsonUnionArray array => array.Depth,
        JsonNullableUnionArray array => array.Depth,
        JsonRecordScalarArray => 1,
        JsonRecordScalarMap => 1,
        _ => 0
    };

    private static bool Matches(JsonRecordField field, object? value) => field.Kind switch
    {
        JsonRecordFieldKind.Boolean => value is bool,
        JsonRecordFieldKind.Integer => value is int,
        JsonRecordFieldKind.String => value is string,
        JsonRecordFieldKind.NullableBoolean => value is null or bool,
        JsonRecordFieldKind.NullableInteger => value is null or int,
        JsonRecordFieldKind.NullableString => value is null or string,
        JsonRecordFieldKind.FieldValue => value is JsonFieldValue,
        JsonRecordFieldKind.NullableFieldValue => value is null || value is JsonFieldValue { Kind: not JsonFieldValueKind.Null },
        JsonRecordFieldKind.ExactNumber => value is JsonExactNumber,
        JsonRecordFieldKind.NullableExactNumber => value is null or JsonExactNumber,
        JsonRecordFieldKind.Record => value is JsonScalarRecord record && ReferenceEquals(record.Schema, field.NestedSchema),
        JsonRecordFieldKind.NullableRecord => value is null || value is JsonScalarRecord record && ReferenceEquals(record.Schema, field.NestedSchema),
        JsonRecordFieldKind.RecordArray => value is JsonRecordArray array && ReferenceEquals(array.Schema, field.NestedSchema),
        JsonRecordFieldKind.NullableRecordArray => value is null || value is JsonRecordArray array && ReferenceEquals(array.Schema, field.NestedSchema),
        JsonRecordFieldKind.RecordMap => value is JsonRecordMap map && ReferenceEquals(map.Schema, field.NestedSchema),
        JsonRecordFieldKind.NullableRecordMap => value is null || value is JsonRecordMap map && ReferenceEquals(map.Schema, field.NestedSchema),
        JsonRecordFieldKind.Union => value is JsonTaggedUnion union && ReferenceEquals(union.Schema, field.UnionSchema),
        JsonRecordFieldKind.NullableUnion => value is null || value is JsonTaggedUnion union && ReferenceEquals(union.Schema, field.UnionSchema),
        JsonRecordFieldKind.UnionMap => value is JsonUnionMap map && ReferenceEquals(map.Schema, field.UnionSchema),
        JsonRecordFieldKind.NullableUnionMap => value is null || value is JsonUnionMap map && ReferenceEquals(map.Schema, field.UnionSchema),
        JsonRecordFieldKind.UnionArray => value is JsonUnionArray unionArray && ReferenceEquals(unionArray.Schema, field.UnionSchema),
        JsonRecordFieldKind.NullableUnionArray => value is null || value is JsonUnionArray unionArray && ReferenceEquals(unionArray.Schema, field.UnionSchema),
        JsonRecordFieldKind.UnionNullableElementsArray => value is JsonNullableUnionArray nullableArray && ReferenceEquals(nullableArray.Schema, field.UnionSchema),
        JsonRecordFieldKind.NullableUnionNullableElementsArray => value is null || value is JsonNullableUnionArray nullableArray && ReferenceEquals(nullableArray.Schema, field.UnionSchema),
        JsonRecordFieldKind.BooleanArray or JsonRecordFieldKind.IntegerArray or JsonRecordFieldKind.StringArray or
        JsonRecordFieldKind.BooleanNullableElementsArray or JsonRecordFieldKind.IntegerNullableElementsArray or JsonRecordFieldKind.StringNullableElementsArray or
        JsonRecordFieldKind.ExactNumberArray or JsonRecordFieldKind.ExactNumberNullableElementsArray =>
            value is JsonRecordScalarArray scalarArray && scalarArray.Kind == JsonRecordScalarArray.ElementKind(field.Kind),
        JsonRecordFieldKind.NullableBooleanArray or JsonRecordFieldKind.NullableIntegerArray or JsonRecordFieldKind.NullableStringArray or
        JsonRecordFieldKind.NullableBooleanNullableElementsArray or JsonRecordFieldKind.NullableIntegerNullableElementsArray or JsonRecordFieldKind.NullableStringNullableElementsArray or
        JsonRecordFieldKind.NullableExactNumberArray or JsonRecordFieldKind.NullableExactNumberNullableElementsArray =>
            value is null || value is JsonRecordScalarArray scalarArray && scalarArray.Kind == JsonRecordScalarArray.ElementKind(field.Kind),
        JsonRecordFieldKind.BooleanMap or JsonRecordFieldKind.IntegerMap or JsonRecordFieldKind.StringMap or JsonRecordFieldKind.ExactNumberMap or
        JsonRecordFieldKind.BooleanNullableValuesMap or JsonRecordFieldKind.IntegerNullableValuesMap or JsonRecordFieldKind.StringNullableValuesMap or JsonRecordFieldKind.ExactNumberNullableValuesMap =>
            value is JsonRecordScalarMap scalarMap && scalarMap.Kind == JsonRecordScalarMap.ElementKind(field.Kind),
        JsonRecordFieldKind.NullableBooleanMap or JsonRecordFieldKind.NullableIntegerMap or JsonRecordFieldKind.NullableStringMap or JsonRecordFieldKind.NullableExactNumberMap or
        JsonRecordFieldKind.NullableBooleanNullableValuesMap or JsonRecordFieldKind.NullableIntegerNullableValuesMap or JsonRecordFieldKind.NullableStringNullableValuesMap or JsonRecordFieldKind.NullableExactNumberNullableValuesMap =>
            value is null || value is JsonRecordScalarMap scalarMap && scalarMap.Kind == JsonRecordScalarMap.ElementKind(field.Kind),
        _ => false
    };
}

internal enum JsonRecordScalarArrayKind { Boolean, Integer, String, NullableBoolean, NullableInteger, NullableString, ExactNumber, NullableExactNumber }

// The only accepted elements are immutable primitive values copied from typed arrays.
internal sealed class JsonRecordScalarArray
{
    private readonly Array values;
    private JsonRecordScalarArray(JsonRecordScalarArrayKind kind, Array values) { Kind = kind; this.values = values; }
    internal JsonRecordScalarArrayKind Kind { get; }
    internal int Count => values.Length;
    internal object? At(int index) => values.GetValue(index);
    internal static JsonRecordScalarArray Create(bool[] values) => new(JsonRecordScalarArrayKind.Boolean, Copy(values));
    internal static JsonRecordScalarArray Create(int[] values) => new(JsonRecordScalarArrayKind.Integer, Copy(values));
    internal static JsonRecordScalarArray Create(bool?[] values) => new(JsonRecordScalarArrayKind.NullableBoolean, Copy(values));
    internal static JsonRecordScalarArray Create(int?[] values) => new(JsonRecordScalarArrayKind.NullableInteger, Copy(values));
    internal static JsonRecordScalarArray Create(JsonExactNumber[] values)
    {
        var snapshot = Copy(values);
        foreach (var item in snapshot)
            if (item is null) throw new ArgumentException("Number array elements cannot be null.", nameof(values));
        return new JsonRecordScalarArray(JsonRecordScalarArrayKind.ExactNumber, snapshot);
    }
    internal static JsonRecordScalarArray CreateNullableNumbers(JsonExactNumber?[] values) =>
        new(JsonRecordScalarArrayKind.NullableExactNumber, Copy(values));
    internal static JsonRecordScalarArray CreateNullableStrings(string?[] values)
    {
        var snapshot = Copy(values);
        foreach (var item in snapshot)
            if (item is not null) JsonScalarCodec.ValidateUtf16(item);
        return new JsonRecordScalarArray(JsonRecordScalarArrayKind.NullableString, snapshot);
    }
    internal static JsonRecordScalarArray Create(string[] values)
    {
        var snapshot = Copy(values);
        foreach (var item in snapshot)
        {
            if (item is null) throw new ArgumentException("String array elements cannot be null.", nameof(values));
            JsonScalarCodec.ValidateUtf16(item);
        }
        return new JsonRecordScalarArray(JsonRecordScalarArrayKind.String, snapshot);
    }
    private static T[] Copy<T>(T[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return (T[])values.Clone();
    }
    internal static JsonRecordScalarArrayKind? ElementKind(JsonRecordFieldKind kind) => kind switch
    {
        JsonRecordFieldKind.BooleanArray or JsonRecordFieldKind.NullableBooleanArray => JsonRecordScalarArrayKind.Boolean,
        JsonRecordFieldKind.IntegerArray or JsonRecordFieldKind.NullableIntegerArray => JsonRecordScalarArrayKind.Integer,
        JsonRecordFieldKind.StringArray or JsonRecordFieldKind.NullableStringArray => JsonRecordScalarArrayKind.String,
        JsonRecordFieldKind.BooleanNullableElementsArray or JsonRecordFieldKind.NullableBooleanNullableElementsArray => JsonRecordScalarArrayKind.NullableBoolean,
        JsonRecordFieldKind.IntegerNullableElementsArray or JsonRecordFieldKind.NullableIntegerNullableElementsArray => JsonRecordScalarArrayKind.NullableInteger,
        JsonRecordFieldKind.StringNullableElementsArray or JsonRecordFieldKind.NullableStringNullableElementsArray => JsonRecordScalarArrayKind.NullableString,
        JsonRecordFieldKind.ExactNumberArray or JsonRecordFieldKind.NullableExactNumberArray => JsonRecordScalarArrayKind.ExactNumber,
        JsonRecordFieldKind.ExactNumberNullableElementsArray or JsonRecordFieldKind.NullableExactNumberNullableElementsArray => JsonRecordScalarArrayKind.NullableExactNumber,
        _ => null
    };
}

// Explicit homogeneous scalar maps own their entries; keys and values are immutable.
internal sealed class JsonRecordScalarMap
{
    private readonly KeyValuePair<string, object>[] values;
    private JsonRecordScalarMap(JsonRecordFieldKind kind, KeyValuePair<string, object>[] values) { Kind = kind; this.values = values; }
    internal JsonRecordFieldKind Kind { get; }
    internal int Count => values.Length;
    internal KeyValuePair<string, object> At(int index) => values[index];
    internal static JsonRecordFieldKind? ElementKind(JsonRecordFieldKind field) => field switch
    {
        JsonRecordFieldKind.BooleanMap or JsonRecordFieldKind.NullableBooleanMap => JsonRecordFieldKind.Boolean,
        JsonRecordFieldKind.IntegerMap or JsonRecordFieldKind.NullableIntegerMap => JsonRecordFieldKind.Integer,
        JsonRecordFieldKind.StringMap or JsonRecordFieldKind.NullableStringMap => JsonRecordFieldKind.String,
        JsonRecordFieldKind.ExactNumberMap or JsonRecordFieldKind.NullableExactNumberMap => JsonRecordFieldKind.ExactNumber,
        JsonRecordFieldKind.BooleanNullableValuesMap or JsonRecordFieldKind.NullableBooleanNullableValuesMap => JsonRecordFieldKind.NullableBoolean,
        JsonRecordFieldKind.IntegerNullableValuesMap or JsonRecordFieldKind.NullableIntegerNullableValuesMap => JsonRecordFieldKind.NullableInteger,
        JsonRecordFieldKind.StringNullableValuesMap or JsonRecordFieldKind.NullableStringNullableValuesMap => JsonRecordFieldKind.NullableString,
        JsonRecordFieldKind.ExactNumberNullableValuesMap or JsonRecordFieldKind.NullableExactNumberNullableValuesMap => JsonRecordFieldKind.NullableExactNumber,
        _ => null
    };
    internal static JsonRecordScalarMap Create(JsonRecordFieldKind kind, KeyValuePair<string, object>[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (kind is not (JsonRecordFieldKind.Boolean or JsonRecordFieldKind.Integer or JsonRecordFieldKind.String or JsonRecordFieldKind.ExactNumber or
            JsonRecordFieldKind.NullableBoolean or JsonRecordFieldKind.NullableInteger or JsonRecordFieldKind.NullableString or JsonRecordFieldKind.NullableExactNumber))
            throw new ArgumentException("Unsupported scalar map descriptor.", nameof(kind));
        var copy = (KeyValuePair<string, object>[])values.Clone();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pair in copy)
        {
            if (pair.Key is null || !names.Add(pair.Key)) throw new ArgumentException("Map keys must be unique and non-null.", nameof(values));
            JsonScalarCodec.ValidateUtf16(pair.Key);
            var matches = kind switch
            {
                JsonRecordFieldKind.Boolean => pair.Value is bool,
                JsonRecordFieldKind.Integer => pair.Value is int,
                JsonRecordFieldKind.String => pair.Value is string,
                JsonRecordFieldKind.ExactNumber => pair.Value is JsonExactNumber,
                JsonRecordFieldKind.NullableBoolean => pair.Value is null or bool,
                JsonRecordFieldKind.NullableInteger => pair.Value is null or int,
                JsonRecordFieldKind.NullableString => pair.Value is null or string,
                JsonRecordFieldKind.NullableExactNumber => pair.Value is null or JsonExactNumber,
                _ => false
            };
            if (!matches) throw new ArgumentException("Map values must match their descriptor.", nameof(values));
            if (pair.Value is string text) JsonScalarCodec.ValidateUtf16(text);
        }
        return new(kind, copy);
    }
}

// Own the entries; keys and record snapshots are immutable.
internal sealed class JsonRecordMap
{
    private readonly KeyValuePair<string, JsonScalarRecord>[] values;
    private JsonRecordMap(JsonScalarRecordSchema schema, KeyValuePair<string, JsonScalarRecord>[] values) { Schema = schema; this.values = values; }
    internal JsonScalarRecordSchema Schema { get; }
    internal int Count => values.Length;
    internal int Depth => 1 + values.Select(value => value.Value.Depth).DefaultIfEmpty(0).Max();
    internal KeyValuePair<string, JsonScalarRecord> At(int index) => values[index];

    internal static JsonRecordMap Create(JsonScalarRecordSchema schema, KeyValuePair<string, JsonScalarRecord>[] values)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(values);
        var copied = (KeyValuePair<string, JsonScalarRecord>[])values.Clone();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pair in copied)
        {
            if (pair.Key is null || pair.Value is null || !ReferenceEquals(pair.Value.Schema, schema))
                throw new ArgumentException("Map entries must have keys and records matching their descriptor.", nameof(values));
            JsonScalarCodec.ValidateUtf16(pair.Key);
            if (!names.Add(pair.Key)) throw new ArgumentException("Map keys must be unique.", nameof(values));
        }
        return new JsonRecordMap(schema, copied);
    }
}

// Own the entries; keys and tagged-union snapshots are immutable.
internal sealed class JsonUnionMap
{
    private readonly KeyValuePair<string, JsonTaggedUnion>[] values;
    private JsonUnionMap(JsonTaggedUnionSchema schema, KeyValuePair<string, JsonTaggedUnion>[] values) { Schema = schema; this.values = values; }
    internal JsonTaggedUnionSchema Schema { get; }
    internal int Count => values.Length;
    internal int Depth => 1 + values.Select(value => value.Value.Depth).DefaultIfEmpty(0).Max();
    internal KeyValuePair<string, JsonTaggedUnion> At(int index) => values[index];
    internal static JsonUnionMap Create(JsonTaggedUnionSchema schema, KeyValuePair<string, JsonTaggedUnion>[] values)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(values);
        var copy = (KeyValuePair<string, JsonTaggedUnion>[])values.Clone();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pair in copy)
        {
            if (pair.Key is null || pair.Value is null || !ReferenceEquals(pair.Value.Schema, schema))
                throw new ArgumentException("Map entries must have keys and unions matching their descriptor.", nameof(values));
            JsonScalarCodec.ValidateUtf16(pair.Key);
            if (!names.Add(pair.Key)) throw new ArgumentException("Map keys must be unique.", nameof(values));
        }
        return new JsonUnionMap(schema, copy);
    }
}

// Own the array; its elements are already immutable record snapshots.
internal sealed class JsonRecordArray
{
    private readonly JsonScalarRecord[] values;
    private JsonRecordArray(JsonScalarRecordSchema schema, JsonScalarRecord[] values) { Schema = schema; this.values = values; }
    internal JsonScalarRecordSchema Schema { get; }
    internal int Count => values.Length;
    internal int Depth => 1 + values.Select(value => value.Depth).DefaultIfEmpty(0).Max();
    internal JsonScalarRecord At(int index) => values[index];

    internal static JsonRecordArray Create(JsonScalarRecordSchema schema, JsonScalarRecord[] values)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(values);
        var copied = (JsonScalarRecord[])values.Clone();
        foreach (var value in copied)
            if (value is null || !ReferenceEquals(value.Schema, schema))
                throw new ArgumentException("Array records must match their descriptor.", nameof(values));
        return new JsonRecordArray(schema, copied);
    }
}

// Own the array; every element is an immutable value of the exact descriptor.
internal sealed class JsonUnionArray
{
    private readonly JsonTaggedUnion[] values;
    private JsonUnionArray(JsonTaggedUnionSchema schema, JsonTaggedUnion[] values) { Schema = schema; this.values = values; }
    internal JsonTaggedUnionSchema Schema { get; }
    internal int Count => values.Length;
    internal int Depth => 1 + values.Select(value => value.Depth).DefaultIfEmpty(0).Max();
    internal JsonTaggedUnion At(int index) => values[index];

    internal static JsonUnionArray Create(JsonTaggedUnionSchema schema, JsonTaggedUnion[] values)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(values);
        var copied = (JsonTaggedUnion[])values.Clone();
        foreach (var value in copied)
            if (value is null || !ReferenceEquals(value.Schema, schema))
                throw new ArgumentException("Array unions must match their descriptor.", nameof(values));
        return new JsonUnionArray(schema, copied);
    }
}

// Own nullable elements without weakening required-element union arrays.
internal sealed class JsonNullableUnionArray
{
    private readonly JsonTaggedUnion?[] values;
    private JsonNullableUnionArray(JsonTaggedUnionSchema schema, JsonTaggedUnion?[] values) { Schema = schema; this.values = values; }
    internal JsonTaggedUnionSchema Schema { get; }
    internal int Count => values.Length;
    internal int Depth => 1 + values.Select(value => value?.Depth ?? 0).DefaultIfEmpty(0).Max();
    internal JsonTaggedUnion? At(int index) => values[index];

    internal static JsonNullableUnionArray Create(JsonTaggedUnionSchema schema, JsonTaggedUnion?[] values)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(values);
        var copied = (JsonTaggedUnion?[])values.Clone();
        foreach (var value in copied)
            if (value is not null && !ReferenceEquals(value.Schema, schema))
                throw new ArgumentException("Array unions must match their descriptor.", nameof(values));
        return new JsonNullableUnionArray(schema, copied);
    }
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
            else if (field.Kind is JsonRecordFieldKind.RecordArray or JsonRecordFieldKind.NullableRecordArray)
            {
                value = field.Kind == JsonRecordFieldKind.NullableRecordArray && property.Value.ValueKind == JsonValueKind.Null
                    ? JsonScalarResult<object?>.Success(null)
                    : Box(ReadRecordArray(field.NestedSchema!, property.Value, path));
            }
            else if (field.Kind is JsonRecordFieldKind.RecordMap or JsonRecordFieldKind.NullableRecordMap)
            {
                value = field.Kind == JsonRecordFieldKind.NullableRecordMap && property.Value.ValueKind == JsonValueKind.Null
                    ? JsonScalarResult<object?>.Success(null)
                    : Box(ReadRecordMap(field.NestedSchema!, property.Value, path));
            }
            else if (field.Kind is JsonRecordFieldKind.UnionMap or JsonRecordFieldKind.NullableUnionMap)
            {
                value = field.Kind == JsonRecordFieldKind.NullableUnionMap && property.Value.ValueKind == JsonValueKind.Null
                    ? JsonScalarResult<object?>.Success(null)
                    : Box(ReadUnionMap(field.UnionSchema!, property.Value, path));
            }
            else if (field.Kind is JsonRecordFieldKind.UnionNullableElementsArray or JsonRecordFieldKind.NullableUnionNullableElementsArray)
            {
                value = field.Kind == JsonRecordFieldKind.NullableUnionNullableElementsArray && property.Value.ValueKind == JsonValueKind.Null
                    ? JsonScalarResult<object?>.Success(null)
                    : Box(ReadNullableUnionArray(field.UnionSchema!, property.Value, path));
            }
            else if (field.Kind is JsonRecordFieldKind.UnionArray or JsonRecordFieldKind.NullableUnionArray)
            {
                value = field.Kind == JsonRecordFieldKind.NullableUnionArray && property.Value.ValueKind == JsonValueKind.Null
                    ? JsonScalarResult<object?>.Success(null)
                    : Box(ReadUnionArray(field.UnionSchema!, property.Value, path));
            }
            else if (field.Kind is JsonRecordFieldKind.Union or JsonRecordFieldKind.NullableUnion)
            {
                if (field.Kind == JsonRecordFieldKind.NullableUnion && property.Value.ValueKind == JsonValueKind.Null)
                    value = JsonScalarResult<object?>.Success(null);
                else
                {
                    var union = JsonTaggedUnionCodec.Decode(field.UnionSchema!, Encoding.UTF8.GetBytes(property.Value.GetRawText()));
                    value = union.IsSuccess
                        ? JsonScalarResult<object?>.Success(union.Value)
                        : JsonScalarResult<object?>.Failure(union.Error!.Value.Code, path + union.Error.Value.Path[1..]);
                }
            }
            else if (JsonRecordScalarMap.ElementKind(field.Kind) is { } mapKind)
            {
                value = JsonScalarRecordSchema.IsNullable(field.Kind) && property.Value.ValueKind == JsonValueKind.Null
                    ? JsonScalarResult<object?>.Success(null)
                    : Box(ReadScalarMap(mapKind, property.Value, path));
            }
            else if (JsonRecordScalarArray.ElementKind(field.Kind) is { } scalarKind)
            {
                value = JsonScalarRecordSchema.IsNullable(field.Kind) && property.Value.ValueKind == JsonValueKind.Null
                    ? JsonScalarResult<object?>.Success(null)
                    : Box(ReadScalarArray(scalarKind, property.Value, path));
            }
            else
            {
                var scalar = ReadValue(property.Value, field.Kind);
                value = scalar.IsSuccess ? scalar : JsonScalarResult<object?>.Failure(scalar.Error!.Value.Code, path + scalar.Error.Value.Path[1..]);
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

    private static JsonScalarResult<JsonRecordScalarMap> ReadScalarMap(JsonRecordFieldKind kind, JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return JsonScalarResult<JsonRecordScalarMap>.Failure(JsonScalarErrorCode.TypeMismatch, path);
        var values = new List<KeyValuePair<string, object>>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            var itemPath = path + "[" + values.Count.ToString(CultureInfo.InvariantCulture) + "]";
            string name;
            try { name = property.Name; JsonScalarCodec.ValidateUtf16(name); }
            catch (InvalidOperationException) { return JsonScalarResult<JsonRecordScalarMap>.Failure(JsonScalarErrorCode.InvalidUnicode, itemPath); }
            catch (ArgumentException) { return JsonScalarResult<JsonRecordScalarMap>.Failure(JsonScalarErrorCode.InvalidUnicode, itemPath); }
            if (!names.Add(name)) return JsonScalarResult<JsonRecordScalarMap>.Failure(JsonScalarErrorCode.DuplicateField, itemPath);
            var item = ReadValue(property.Value, kind);
            if (!item.IsSuccess) return JsonScalarResult<JsonRecordScalarMap>.Failure(item.Error!.Value.Code, itemPath);
            values.Add(new(name, item.Value!));
        }
        return JsonScalarResult<JsonRecordScalarMap>.Success(JsonRecordScalarMap.Create(kind, values.ToArray()));
    }

    private static JsonScalarResult<JsonRecordScalarArray> ReadScalarArray(JsonRecordScalarArrayKind kind, JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Array)
            return JsonScalarResult<JsonRecordScalarArray>.Failure(JsonScalarErrorCode.TypeMismatch, path);
        return kind switch
        {
            JsonRecordScalarArrayKind.Boolean => ReadScalarItems(element, path, JsonScalarCodec.ReadBoolean, JsonRecordScalarArray.Create),
            JsonRecordScalarArrayKind.Integer => ReadScalarItems(element, path, JsonScalarCodec.ReadInteger, JsonRecordScalarArray.Create),
            JsonRecordScalarArrayKind.String => ReadScalarItems(element, path, JsonScalarCodec.ReadString, JsonRecordScalarArray.Create),
            JsonRecordScalarArrayKind.NullableBoolean => ReadScalarItems(element, path, JsonScalarCodec.ReadNullableBoolean, JsonRecordScalarArray.Create),
            JsonRecordScalarArrayKind.NullableInteger => ReadScalarItems(element, path, JsonScalarCodec.ReadNullableInteger, JsonRecordScalarArray.Create),
            JsonRecordScalarArrayKind.NullableString => ReadScalarItems(element, path, JsonScalarCodec.ReadNullableString, JsonRecordScalarArray.CreateNullableStrings),
            JsonRecordScalarArrayKind.ExactNumber => ReadScalarItems(element, path, JsonExactNumber.Read, JsonRecordScalarArray.Create),
            JsonRecordScalarArrayKind.NullableExactNumber => ReadScalarItems(element, path, ReadNullableExactNumber, JsonRecordScalarArray.CreateNullableNumbers),
            _ => throw new InvalidOperationException("Unsupported scalar array descriptor.")
        };
    }

    private static JsonScalarResult<JsonExactNumber?> ReadNullableExactNumber(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null) return JsonScalarResult<JsonExactNumber?>.Success(null);
        var number = JsonExactNumber.Read(value);
        return number.IsSuccess ? JsonScalarResult<JsonExactNumber?>.Success(number.Value)
            : JsonScalarResult<JsonExactNumber?>.Failure(number.Error!.Value.Code);
    }

    private static JsonScalarResult<JsonRecordScalarArray> ReadScalarItems<T>(JsonElement element, string path,
        Func<JsonElement, JsonScalarResult<T>> read, Func<T[], JsonRecordScalarArray> create)
    {
        var values = new List<T>();
        foreach (var item in element.EnumerateArray())
        {
            var result = read(item);
            if (!result.IsSuccess)
                return JsonScalarResult<JsonRecordScalarArray>.Failure(result.Error!.Value.Code,
                    path + "[" + values.Count.ToString(CultureInfo.InvariantCulture) + "]");
            values.Add(result.Value);
        }
        return JsonScalarResult<JsonRecordScalarArray>.Success(create(values.ToArray()));
    }

    private static JsonScalarResult<JsonNullableUnionArray> ReadNullableUnionArray(JsonTaggedUnionSchema schema, JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Array)
            return JsonScalarResult<JsonNullableUnionArray>.Failure(JsonScalarErrorCode.TypeMismatch, path);
        var values = new List<JsonTaggedUnion?>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Null) { values.Add(null); continue; }
            var itemPath = path + "[" + values.Count.ToString(CultureInfo.InvariantCulture) + "]";
            var result = JsonTaggedUnionCodec.Decode(schema, Encoding.UTF8.GetBytes(item.GetRawText()));
            if (!result.IsSuccess)
                return JsonScalarResult<JsonNullableUnionArray>.Failure(result.Error!.Value.Code, itemPath + result.Error.Value.Path[1..]);
            values.Add(result.Value);
        }
        return JsonScalarResult<JsonNullableUnionArray>.Success(JsonNullableUnionArray.Create(schema, values.ToArray()));
    }

    private static JsonScalarResult<JsonUnionArray> ReadUnionArray(JsonTaggedUnionSchema schema, JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Array)
            return JsonScalarResult<JsonUnionArray>.Failure(JsonScalarErrorCode.TypeMismatch, path);
        var values = new List<JsonTaggedUnion>();
        foreach (var item in element.EnumerateArray())
        {
            var itemPath = path + "[" + values.Count.ToString(CultureInfo.InvariantCulture) + "]";
            var result = JsonTaggedUnionCodec.Decode(schema, Encoding.UTF8.GetBytes(item.GetRawText()));
            if (!result.IsSuccess)
                return JsonScalarResult<JsonUnionArray>.Failure(result.Error!.Value.Code, itemPath + result.Error.Value.Path[1..]);
            values.Add(result.Value);
        }
        return JsonScalarResult<JsonUnionArray>.Success(JsonUnionArray.Create(schema, values.ToArray()));
    }

    private static JsonScalarResult<JsonUnionMap> ReadUnionMap(JsonTaggedUnionSchema schema, JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return JsonScalarResult<JsonUnionMap>.Failure(JsonScalarErrorCode.TypeMismatch, path);
        var values = new List<KeyValuePair<string, JsonTaggedUnion>>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            var itemPath = path + "[" + values.Count.ToString(CultureInfo.InvariantCulture) + "]";
            string name;
            try { name = property.Name; JsonScalarCodec.ValidateUtf16(name); }
            catch (InvalidOperationException) { return JsonScalarResult<JsonUnionMap>.Failure(JsonScalarErrorCode.InvalidUnicode, itemPath); }
            catch (ArgumentException) { return JsonScalarResult<JsonUnionMap>.Failure(JsonScalarErrorCode.InvalidUnicode, itemPath); }
            if (!names.Add(name)) return JsonScalarResult<JsonUnionMap>.Failure(JsonScalarErrorCode.DuplicateField, itemPath);
            var union = JsonTaggedUnionCodec.Decode(schema, Encoding.UTF8.GetBytes(property.Value.GetRawText()));
            if (!union.IsSuccess) return JsonScalarResult<JsonUnionMap>.Failure(union.Error!.Value.Code, itemPath + union.Error.Value.Path[1..]);
            values.Add(new(name, union.Value));
        }
        return JsonScalarResult<JsonUnionMap>.Success(JsonUnionMap.Create(schema, values.ToArray()));
    }

    private static JsonScalarResult<JsonRecordMap> ReadRecordMap(JsonScalarRecordSchema schema, JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return JsonScalarResult<JsonRecordMap>.Failure(JsonScalarErrorCode.TypeMismatch, path);
        var values = new List<KeyValuePair<string, JsonScalarRecord>>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            var itemPath = path + "[" + values.Count.ToString(CultureInfo.InvariantCulture) + "]";
            string name;
            try { name = property.Name; JsonScalarCodec.ValidateUtf16(name); }
            catch (InvalidOperationException) { return JsonScalarResult<JsonRecordMap>.Failure(JsonScalarErrorCode.InvalidUnicode, itemPath); }
            catch (ArgumentException) { return JsonScalarResult<JsonRecordMap>.Failure(JsonScalarErrorCode.InvalidUnicode, itemPath); }
            if (!names.Add(name)) return JsonScalarResult<JsonRecordMap>.Failure(JsonScalarErrorCode.DuplicateField, itemPath);
            var value = ReadRecord(schema, property.Value, itemPath);
            if (!value.IsSuccess)
                return JsonScalarResult<JsonRecordMap>.Failure(value.Error!.Value.Code, value.Error.Value.Path);
            values.Add(new(name, value.Value));
        }
        return JsonScalarResult<JsonRecordMap>.Success(JsonRecordMap.Create(schema, values.ToArray()));
    }

    private static JsonScalarResult<JsonRecordArray> ReadRecordArray(JsonScalarRecordSchema schema, JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Array)
            return JsonScalarResult<JsonRecordArray>.Failure(JsonScalarErrorCode.TypeMismatch, path);
        var values = new List<JsonScalarRecord>();
        foreach (var item in element.EnumerateArray())
        {
            var itemPath = path + "[" + values.Count.ToString(CultureInfo.InvariantCulture) + "]";
            var value = ReadRecord(schema, item, itemPath);
            if (!value.IsSuccess)
                return JsonScalarResult<JsonRecordArray>.Failure(value.Error!.Value.Code, value.Error.Value.Path);
            values.Add(value.Value);
        }
        return JsonScalarResult<JsonRecordArray>.Success(JsonRecordArray.Create(schema, values.ToArray()));
    }

    private static JsonScalarResult<object?> ReadValue(JsonElement value, JsonRecordFieldKind kind) => kind switch
    {
        JsonRecordFieldKind.Boolean => Box(JsonScalarCodec.ReadBoolean(value)),
        JsonRecordFieldKind.Integer => Box(JsonScalarCodec.ReadInteger(value)),
        JsonRecordFieldKind.String => Box(JsonScalarCodec.ReadString(value)),
        JsonRecordFieldKind.NullableBoolean => Box(JsonScalarCodec.ReadNullableBoolean(value)),
        JsonRecordFieldKind.NullableInteger => Box(JsonScalarCodec.ReadNullableInteger(value)),
        JsonRecordFieldKind.NullableString => Box(JsonScalarCodec.ReadNullableString(value)),
        JsonRecordFieldKind.FieldValue => Box(JsonFieldValueCodec.Read(value, "$")),
        JsonRecordFieldKind.NullableFieldValue => value.ValueKind == JsonValueKind.Null
            ? JsonScalarResult<object?>.Success(null) : Box(JsonFieldValueCodec.Read(value, "$")),
        JsonRecordFieldKind.ExactNumber => Box(JsonExactNumber.Read(value)),
        JsonRecordFieldKind.NullableExactNumber => value.ValueKind == JsonValueKind.Null
            ? JsonScalarResult<object?>.Success(null) : Box(JsonExactNumber.Read(value)),
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
            case JsonRecordFieldKind.FieldValue:
            case JsonRecordFieldKind.NullableFieldValue: writer.WriteRawValue(JsonFieldValueCodec.Encode((JsonFieldValue)value)); break;
            case JsonRecordFieldKind.ExactNumber:
            case JsonRecordFieldKind.NullableExactNumber: writer.WriteRawValue(((JsonExactNumber)value).Token); break;
            case JsonRecordFieldKind.Record:
            case JsonRecordFieldKind.NullableRecord: WriteRecord(writer, (JsonScalarRecord)value); break;
            case JsonRecordFieldKind.Union:
            case JsonRecordFieldKind.NullableUnion: writer.WriteRawValue(JsonTaggedUnionCodec.Encode((JsonTaggedUnion)value)); break;
            case JsonRecordFieldKind.UnionNullableElementsArray:
            case JsonRecordFieldKind.NullableUnionNullableElementsArray:
                var nullableUnions = (JsonNullableUnionArray)value;
                writer.WriteStartArray();
                for (var index = 0; index < nullableUnions.Count; index++)
                {
                    var item = nullableUnions.At(index);
                    if (item is null) writer.WriteNullValue();
                    else writer.WriteRawValue(JsonTaggedUnionCodec.Encode(item));
                }
                writer.WriteEndArray();
                break;
            case JsonRecordFieldKind.UnionArray:
            case JsonRecordFieldKind.NullableUnionArray:
                var unions = (JsonUnionArray)value;
                writer.WriteStartArray();
                for (var index = 0; index < unions.Count; index++) writer.WriteRawValue(JsonTaggedUnionCodec.Encode(unions.At(index)));
                writer.WriteEndArray();
                break;
            case JsonRecordFieldKind.RecordArray:
            case JsonRecordFieldKind.NullableRecordArray:
                var array = (JsonRecordArray)value;
                writer.WriteStartArray();
                for (var index = 0; index < array.Count; index++) WriteRecord(writer, array.At(index));
                writer.WriteEndArray();
                break;
            case JsonRecordFieldKind.UnionMap:
            case JsonRecordFieldKind.NullableUnionMap:
                var unionMap = (JsonUnionMap)value;
                writer.WriteStartObject();
                for (var index = 0; index < unionMap.Count; index++)
                {
                    var entry = unionMap.At(index);
                    writer.WritePropertyName(entry.Key);
                    writer.WriteRawValue(JsonTaggedUnionCodec.Encode(entry.Value));
                }
                writer.WriteEndObject();
                break;
            case JsonRecordFieldKind.RecordMap:
            case JsonRecordFieldKind.NullableRecordMap:
                var map = (JsonRecordMap)value;
                writer.WriteStartObject();
                for (var index = 0; index < map.Count; index++)
                {
                    var entry = map.At(index);
                    writer.WritePropertyName(entry.Key);
                    WriteRecord(writer, entry.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonRecordFieldKind.BooleanMap:
            case JsonRecordFieldKind.IntegerMap:
            case JsonRecordFieldKind.StringMap:
            case JsonRecordFieldKind.ExactNumberMap:
            case JsonRecordFieldKind.NullableBooleanMap:
            case JsonRecordFieldKind.NullableIntegerMap:
            case JsonRecordFieldKind.NullableStringMap:
            case JsonRecordFieldKind.NullableExactNumberMap:
            case JsonRecordFieldKind.BooleanNullableValuesMap:
            case JsonRecordFieldKind.IntegerNullableValuesMap:
            case JsonRecordFieldKind.StringNullableValuesMap:
            case JsonRecordFieldKind.ExactNumberNullableValuesMap:
            case JsonRecordFieldKind.NullableBooleanNullableValuesMap:
            case JsonRecordFieldKind.NullableIntegerNullableValuesMap:
            case JsonRecordFieldKind.NullableStringNullableValuesMap:
            case JsonRecordFieldKind.NullableExactNumberNullableValuesMap:
                var scalarMap = (JsonRecordScalarMap)value;
                writer.WriteStartObject();
                for (var index = 0; index < scalarMap.Count; index++)
                {
                    var entry = scalarMap.At(index);
                    writer.WritePropertyName(entry.Key);
                    WriteValue(writer, scalarMap.Kind, entry.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonRecordFieldKind.BooleanArray:
            case JsonRecordFieldKind.IntegerArray:
            case JsonRecordFieldKind.StringArray:
            case JsonRecordFieldKind.NullableBooleanArray:
            case JsonRecordFieldKind.NullableIntegerArray:
            case JsonRecordFieldKind.NullableStringArray:
            case JsonRecordFieldKind.BooleanNullableElementsArray:
            case JsonRecordFieldKind.IntegerNullableElementsArray:
            case JsonRecordFieldKind.StringNullableElementsArray:
            case JsonRecordFieldKind.NullableBooleanNullableElementsArray:
            case JsonRecordFieldKind.NullableIntegerNullableElementsArray:
            case JsonRecordFieldKind.NullableStringNullableElementsArray:
            case JsonRecordFieldKind.ExactNumberArray:
            case JsonRecordFieldKind.NullableExactNumberArray:
            case JsonRecordFieldKind.ExactNumberNullableElementsArray:
            case JsonRecordFieldKind.NullableExactNumberNullableElementsArray:
                var scalars = (JsonRecordScalarArray)value;
                writer.WriteStartArray();
                for (var index = 0; index < scalars.Count; index++)
                {
                    var item = scalars.At(index);
                    if (item is null) { writer.WriteNullValue(); continue; }
                    switch (scalars.Kind)
                    {
                        case JsonRecordScalarArrayKind.Boolean:
                        case JsonRecordScalarArrayKind.NullableBoolean: writer.WriteBooleanValue((bool)item); break;
                        case JsonRecordScalarArrayKind.Integer:
                        case JsonRecordScalarArrayKind.NullableInteger: writer.WriteNumberValue((int)item); break;
                        case JsonRecordScalarArrayKind.String:
                        case JsonRecordScalarArrayKind.NullableString: writer.WriteStringValue((string)item); break;
                        case JsonRecordScalarArrayKind.ExactNumber:
                        case JsonRecordScalarArrayKind.NullableExactNumber: writer.WriteRawValue(((JsonExactNumber)item).Token); break;
                        default: throw new InvalidOperationException("Unsupported scalar array descriptor.");
                    }
                }
                writer.WriteEndArray();
                break;
            default: throw new InvalidOperationException("Unsupported record descriptor.");
        }
    }
}
