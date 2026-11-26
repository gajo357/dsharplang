using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonNullableUnionMapTests
{
    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);
    private static string Encode(JsonScalarRecord value) => Encoding.UTF8.GetString(JsonScalarRecordCodec.Encode(value));
    private static JsonFieldValue Tree(string json) => JsonFieldValueCodec.Decode(Bytes(json)).Value;
    private static string Json(JsonFieldValue value) => Encoding.UTF8.GetString(JsonFieldValueCodec.Encode(value));

    [Fact]
    public void NullableUnionMapRoundTripsInsideRecordUnionAndTree()
    {
        var item = new JsonScalarRecordSchema([new("amount", JsonRecordFieldKind.ExactNumber), new("note", JsonRecordFieldKind.NullableString, Optional: true)]);
        var cases = new JsonTaggedUnionSchema([new("Idle"), new("Data", item)]);
        var payload = new JsonScalarRecordSchema([new("entries", JsonRecordFieldKind.UnionNullableValuesMap, UnionSchema: cases)]);
        var outerCases = new JsonTaggedUnionSchema([new("Envelope", payload)]);
        var schema = new JsonScalarRecordSchema([new("event", JsonRecordFieldKind.Union, UnionSchema: outerCases)]);
        const string json = "{\"event\":{\"case\":\"Envelope\",\"value\":{\"entries\":{\"a\":null,\"b\":{\"case\":\"Idle\"},\"c\":{\"case\":\"Data\",\"value\":{\"amount\":12345678901234567890.00e+8,\"note\":null}}}}}}";
        var record = JsonScalarRecordCodec.Decode(schema, Bytes(json));
        Assert.True(record.IsSuccess);
        Assert.Equal(json, Encode(record.Value));
        var outer = (JsonTaggedUnion)record.Value.ValueAt(0)!;
        var map = Assert.IsType<JsonNullableUnionMap>(outer.Payload!.ValueAt(0));
        Assert.Same(cases, map.Schema);
        Assert.Null(map.At(0).Value);
        Assert.Equal(json, Encode(JsonTypedFieldMapping.ToRecord(schema, JsonTypedFieldMapping.FromRecord(record.Value).Value).Value));
    }

    [Fact]
    public void MissingNullEmptyAndNullEntryStayDistinct()
    {
        var cases = new JsonTaggedUnionSchema([new("Idle")]);
        var schema = new JsonScalarRecordSchema([new("events", JsonRecordFieldKind.NullableUnionNullableValuesMap, Optional: true, UnionSchema: cases)]);
        foreach (var (json, present, isNull) in new[] { ("{}", false, true), ("{\"events\":null}", true, true), ("{\"events\":{}}", true, false), ("{\"events\":{\"a\":null}}", true, false) })
        {
            var result = JsonScalarRecordCodec.Decode(schema, Bytes(json));
            Assert.True(result.IsSuccess);
            Assert.Equal(present, result.Value.IsPresent(0));
            Assert.Equal(isNull, result.Value.ValueAt(0) is null);
            Assert.Equal(json, Encode(result.Value));
        }
        var required = new JsonScalarRecordSchema([new("events", JsonRecordFieldKind.UnionNullableValuesMap, UnionSchema: cases)]);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.MissingField, "$[\"events\"]"), JsonScalarRecordCodec.Decode(required, Bytes("{}")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"events\"]"), JsonScalarRecordCodec.Decode(required, Bytes("{\"events\":null}")).Error);
    }

    [Fact]
    public void StandaloneMappingsCopyAndRequireDescriptors()
    {
        var cases = new JsonTaggedUnionSchema([new("Idle")]);
        var foreign = new JsonTaggedUnionSchema([new("Idle")]);
        var value = JsonTaggedUnion.Create(cases, 0);
        var entries = new[] { new KeyValuePair<string, JsonTaggedUnion?>("a", null), new KeyValuePair<string, JsonTaggedUnion?>("b", value) };
        var map = JsonNullableUnionMap.Create(cases, entries);
        entries[1] = new("changed", null);
        var tree = JsonTypedFieldMapping.FromNullableUnionMap(map).Value;
        Assert.Equal("{\"a\":null,\"b\":{\"case\":\"Idle\"}}", Json(tree));
        var roundTrip = JsonTypedFieldMapping.ToNullableUnionMap(cases, tree);
        Assert.True(roundTrip.IsSuccess);
        Assert.Same(cases, roundTrip.Value.Schema);
        Assert.Null(roundTrip.Value.At(0).Value);
        Assert.NotNull(roundTrip.Value.At(1).Value);
        var required = JsonUnionMap.Create(cases, [new("b", value)]);
        Assert.Equal("{\"b\":{\"case\":\"Idle\"}}", Json(JsonTypedFieldMapping.FromUnionMap(required).Value));
        Assert.True(JsonTypedFieldMapping.ToUnionMap(cases, JsonTypedFieldMapping.FromUnionMap(required).Value).IsSuccess);
        Assert.Throws<ArgumentException>(() => JsonNullableUnionMap.Create(cases, [new("a", JsonTaggedUnion.Create(foreign, 0))]));
        Assert.Throws<ArgumentException>(() => JsonNullableUnionMap.Create(cases, [new("a", null), new("a", value)]));
        Assert.Throws<ArgumentException>(() => JsonNullableUnionMap.Create(cases, [new("\ud800", null)]));
        var schema = new JsonScalarRecordSchema([new("events", JsonRecordFieldKind.UnionNullableValuesMap, UnionSchema: cases)]);
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(schema, [JsonNullableUnionMap.Create(foreign, [])], [true]));
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("events", JsonRecordFieldKind.UnionNullableValuesMap)]));
    }

    [Fact]
    public void ErrorsAreIndexedAndRedactedInBothPaths()
    {
        var payload = new JsonScalarRecordSchema([new("n", JsonRecordFieldKind.Integer)]);
        var cases = new JsonTaggedUnionSchema([new("Data", payload)]);
        var schema = new JsonScalarRecordSchema([new("events", JsonRecordFieldKind.UnionNullableValuesMap, UnionSchema: cases)]);
        var wrong = JsonScalarRecordCodec.Decode(schema, Bytes("{\"events\":{\"private\":null,\"other\":{\"case\":\"Data\",\"value\":{\"n\":\"secret\"}}}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"events\"][1][\"value\"][\"n\"]"), wrong.Error);
        var duplicate = JsonScalarRecordCodec.Decode(schema, Bytes("{\"events\":{\"private\":null,\"\\u0070rivate\":null}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.DuplicateField, "$[\"events\"][1]"), duplicate.Error);
        var invalidKey = JsonScalarRecordCodec.Decode(schema, Bytes("{\"events\":{\"\\uD800\":null}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidUnicode, "$[\"events\"][0]"), invalidKey.Error);
        var mapped = JsonTypedFieldMapping.ToNullableUnionMap(cases, Tree("{\"private\":null,\"other\":{\"case\":\"secret\"}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.UnknownCase, "$[1][\"case\"]"), mapped.Error);
        var required = JsonTypedFieldMapping.ToUnionMap(cases, Tree("{\"private\":null}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[0]"), required.Error);
        foreach (var error in new[] { wrong.Error, duplicate.Error, invalidKey.Error, mapped.Error, required.Error })
        {
            Assert.DoesNotContain("private", error!.Value.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("secret", error.Value.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MapAndUnionShareDepthLimit()
    {
        var payload = new JsonScalarRecordSchema([]);
        var record = JsonScalarRecord.Create(payload, [], []);
        for (var depth = 2; depth <= 61; depth++)
        {
            payload = new JsonScalarRecordSchema([new("child", JsonRecordFieldKind.Record, NestedSchema: payload)]);
            record = JsonScalarRecord.Create(payload, [record], [true]);
        }
        var cases = new JsonTaggedUnionSchema([new("Deep", payload)]);
        var map = JsonNullableUnionMap.Create(cases, [new("empty", null), new("deep", JsonTaggedUnion.Create(cases, 0, record))]);
        var schema = new JsonScalarRecordSchema([new("events", JsonRecordFieldKind.UnionNullableValuesMap, UnionSchema: cases)]);
        var outer = JsonScalarRecord.Create(schema, [map], [true]);
        Assert.Equal(64, outer.Depth);
        Assert.True(JsonScalarRecordCodec.Decode(schema, JsonScalarRecordCodec.Encode(outer)).IsSuccess);
        Assert.True(JsonTypedFieldMapping.ToNullableUnionMap(cases, JsonTypedFieldMapping.FromNullableUnionMap(map).Value).IsSuccess);
        var tooDeep = new JsonScalarRecordSchema([new("child", JsonRecordFieldKind.Record, NestedSchema: payload)]);
        var deeperCases = new JsonTaggedUnionSchema([new("Deep", tooDeep)]);
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("events", JsonRecordFieldKind.UnionNullableValuesMap, UnionSchema: deeperCases)]));
        var deepRecord = JsonScalarRecord.Create(tooDeep, [record], [true]);
        var deepestSchema = new JsonScalarRecordSchema([new("child", JsonRecordFieldKind.Record, NestedSchema: tooDeep)]);
        var deepestRecord = JsonScalarRecord.Create(deepestSchema, [deepRecord], [true]);
        var deepestCases = new JsonTaggedUnionSchema([new("Deep", deepestSchema)]);
        var tooDeepMap = JsonNullableUnionMap.Create(deepestCases, [new("deep", JsonTaggedUnion.Create(deepestCases, 0, deepestRecord))]);
        Assert.Throws<ArgumentException>(() => JsonTypedFieldMapping.FromNullableUnionMap(tooDeepMap));
    }
}
