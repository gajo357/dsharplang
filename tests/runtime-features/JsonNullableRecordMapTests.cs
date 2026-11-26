using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonNullableRecordMapTests
{
    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);
    private static string Encode(JsonScalarRecord value) => Encoding.UTF8.GetString(JsonScalarRecordCodec.Encode(value));
    private static JsonFieldValue Tree(string json) => JsonFieldValueCodec.Decode(Bytes(json)).Value;
    private static string Json(JsonFieldValue value) => Encoding.UTF8.GetString(JsonFieldValueCodec.Encode(value));

    [Fact]
    public void NullableRecordMapsRoundTripInsideUnionAndTree()
    {
        var item = new JsonScalarRecordSchema([new("amount", JsonRecordFieldKind.ExactNumber), new("note", JsonRecordFieldKind.NullableString, Optional: true)]);
        var payload = new JsonScalarRecordSchema([new("entries", JsonRecordFieldKind.RecordNullableValuesMap, NestedSchema: item)]);
        var cases = new JsonTaggedUnionSchema([new("Envelope", payload)]);
        var schema = new JsonScalarRecordSchema([new("event", JsonRecordFieldKind.Union, UnionSchema: cases)]);
        const string json = "{\"event\":{\"case\":\"Envelope\",\"value\":{\"entries\":{\"a\":null,\"b\":{\"amount\":12345678901234567890.00e+8},\"c\":{\"amount\":-0.0,\"note\":null}}}}}";
        var decoded = JsonScalarRecordCodec.Decode(schema, Bytes(json));
        Assert.True(decoded.IsSuccess);
        Assert.Equal(json, Encode(decoded.Value));
        var union = (JsonTaggedUnion)decoded.Value.ValueAt(0)!;
        var map = Assert.IsType<JsonNullableRecordMap>(union.Payload!.ValueAt(0));
        Assert.Same(item, map.Schema);
        Assert.Null(map.At(0).Value);
        Assert.False(map.At(1).Value!.IsPresent(1));
        Assert.True(map.At(2).Value!.IsPresent(1));
        Assert.Equal(json, Encode(JsonTypedFieldMapping.ToRecord(schema, JsonTypedFieldMapping.FromRecord(decoded.Value).Value).Value));
    }

    [Fact]
    public void MissingNullEmptyAndNullEntryStayDistinct()
    {
        var item = new JsonScalarRecordSchema([]);
        var schema = new JsonScalarRecordSchema([new("items", JsonRecordFieldKind.NullableRecordNullableValuesMap, Optional: true, NestedSchema: item)]);
        foreach (var (json, present, isNull) in new[] { ("{}", false, true), ("{\"items\":null}", true, true), ("{\"items\":{}}", true, false), ("{\"items\":{\"a\":null}}", true, false) })
        {
            var result = JsonScalarRecordCodec.Decode(schema, Bytes(json));
            Assert.True(result.IsSuccess);
            Assert.Equal(present, result.Value.IsPresent(0));
            Assert.Equal(isNull, result.Value.ValueAt(0) is null);
            Assert.Equal(json, Encode(result.Value));
        }
        var required = new JsonScalarRecordSchema([new("items", JsonRecordFieldKind.RecordNullableValuesMap, NestedSchema: item)]);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.MissingField, "$[\"items\"]"), JsonScalarRecordCodec.Decode(required, Bytes("{}")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"items\"]"), JsonScalarRecordCodec.Decode(required, Bytes("{\"items\":null}")).Error);
    }

    [Fact]
    public void StandaloneMappingPreservesSnapshotsAndDescriptors()
    {
        var item = new JsonScalarRecordSchema([new("n", JsonRecordFieldKind.Integer)]);
        var foreign = new JsonScalarRecordSchema([new("n", JsonRecordFieldKind.Integer)]);
        var record = JsonScalarRecord.Create(item, [1], [true]);
        var entries = new[] { new KeyValuePair<string, JsonScalarRecord?>("a", null), new KeyValuePair<string, JsonScalarRecord?>("b", record) };
        var map = JsonNullableRecordMap.Create(item, entries);
        entries[1] = new("changed", null);
        var tree = JsonTypedFieldMapping.FromNullableRecordMap(map).Value;
        Assert.Equal("{\"a\":null,\"b\":{\"n\":1}}", Json(tree));
        var roundTrip = JsonTypedFieldMapping.ToNullableRecordMap(item, tree);
        Assert.True(roundTrip.IsSuccess);
        Assert.Same(item, roundTrip.Value.Schema);
        Assert.Null(roundTrip.Value.At(0).Value);
        Assert.NotNull(roundTrip.Value.At(1).Value);
        Assert.Throws<ArgumentException>(() => JsonNullableRecordMap.Create(item, [new("a", JsonScalarRecord.Create(foreign, [1], [true]))]));
        Assert.Throws<ArgumentException>(() => JsonNullableRecordMap.Create(item, [new("a", null), new("a", record)]));
        Assert.Throws<ArgumentException>(() => JsonNullableRecordMap.Create(item, [new("\ud800", null)]));
        var schema = new JsonScalarRecordSchema([new("items", JsonRecordFieldKind.RecordNullableValuesMap, NestedSchema: item)]);
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(schema, [JsonNullableRecordMap.Create(foreign, [])], [true]));
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("items", JsonRecordFieldKind.RecordNullableValuesMap)]));
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("items", JsonRecordFieldKind.RecordNullableValuesMap, Optional: true, NestedSchema: item)]));
    }

    [Fact]
    public void InvalidDataIsIndexedAndRedactedInBothPaths()
    {
        var item = new JsonScalarRecordSchema([new("n", JsonRecordFieldKind.Integer)]);
        var schema = new JsonScalarRecordSchema([new("items", JsonRecordFieldKind.RecordNullableValuesMap, NestedSchema: item)]);
        var wrong = JsonScalarRecordCodec.Decode(schema, Bytes("{\"items\":{\"private\":null,\"other\":{\"n\":\"secret\"}}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"items\"][1][\"n\"]"), wrong.Error);
        var duplicate = JsonScalarRecordCodec.Decode(schema, Bytes("{\"items\":{\"private\":null,\"\\u0070rivate\":null}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.DuplicateField, "$[\"items\"][1]"), duplicate.Error);
        var key = JsonScalarRecordCodec.Decode(schema, Bytes("{\"items\":{\"\\uD800\":null}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidUnicode, "$[\"items\"][0]"), key.Error);
        var mapped = JsonTypedFieldMapping.ToNullableRecordMap(item, Tree("{\"private\":null,\"other\":{\"hidden\":\"secret\"}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.UnknownField, "$[1]"), mapped.Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$"), JsonTypedFieldMapping.ToNullableRecordMap(item, Tree("[]")).Error);
        foreach (var error in new[] { wrong.Error, duplicate.Error, key.Error, mapped.Error })
        {
            Assert.DoesNotContain("private", error!.Value.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("secret", error.Value.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MapAndRecordShareDepthLimit()
    {
        var item = new JsonScalarRecordSchema([]);
        var record = JsonScalarRecord.Create(item, [], []);
        for (var depth = 2; depth <= 62; depth++)
        {
            item = new JsonScalarRecordSchema([new("child", JsonRecordFieldKind.Record, NestedSchema: item)]);
            record = JsonScalarRecord.Create(item, [record], [true]);
        }
        var map = JsonNullableRecordMap.Create(item, [new("empty", null), new("deep", record)]);
        var schema = new JsonScalarRecordSchema([new("items", JsonRecordFieldKind.RecordNullableValuesMap, NestedSchema: item)]);
        var outer = JsonScalarRecord.Create(schema, [map], [true]);
        Assert.Equal(64, outer.Depth);
        Assert.True(JsonScalarRecordCodec.Decode(schema, JsonScalarRecordCodec.Encode(outer)).IsSuccess);
        Assert.True(JsonTypedFieldMapping.ToNullableRecordMap(item, JsonTypedFieldMapping.FromNullableRecordMap(map).Value).IsSuccess);
        var tooDeep = new JsonScalarRecordSchema([new("child", JsonRecordFieldKind.Record, NestedSchema: item)]);
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("items", JsonRecordFieldKind.RecordNullableValuesMap, NestedSchema: tooDeep)]));
        var deeper = JsonScalarRecord.Create(tooDeep, [record], [true]);
        var deepestSchema = new JsonScalarRecordSchema([new("child", JsonRecordFieldKind.Record, NestedSchema: tooDeep)]);
        var deepest = JsonScalarRecord.Create(deepestSchema, [deeper], [true]);
        var tooDeepMap = JsonNullableRecordMap.Create(deepestSchema, [new("deep", deepest)]);
        Assert.Throws<ArgumentException>(() => JsonTypedFieldMapping.FromNullableRecordMap(tooDeepMap));
    }
}
