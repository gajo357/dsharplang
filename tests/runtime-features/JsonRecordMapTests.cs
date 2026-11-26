using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonRecordMapTests
{
    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
    private static string Encode(JsonScalarRecord value) => Encoding.UTF8.GetString(JsonScalarRecordCodec.Encode(value));
    private static JsonScalarRecord Record(JsonScalarRecordSchema schema, string json) => JsonScalarRecordCodec.Decode(schema, Utf8(json)).Value;
    private static KeyValuePair<string, JsonScalarRecord> Pair(string key, JsonScalarRecord value) => new(key, value);
    private static JsonFieldValue Nested(int depth)
    {
        var value = JsonFieldValue.Null();
        for (var i = 0; i < depth; i++) value = JsonFieldValue.List([value]);
        return value;
    }

    [Fact]
    public void MapsRoundTripInsideUnionWithExactNumbersAndPresence()
    {
        var item = new JsonScalarRecordSchema([
            new("amount", JsonRecordFieldKind.ExactNumber),
            new("note", JsonRecordFieldKind.NullableString, Optional: true)
        ]);
        var payload = new JsonScalarRecordSchema([new("entries", JsonRecordFieldKind.RecordMap, NestedSchema: item)]);
        var cases = new JsonTaggedUnionSchema([new("Data", payload)]);
        var outer = new JsonScalarRecordSchema([new("event", JsonRecordFieldKind.Union, UnionSchema: cases)]);
        const string json = "{\"event\":{\"case\":\"Data\",\"value\":{\"entries\":{\"first\":{\"amount\":12345678901234567890.000e+8},\"second\":{\"amount\":-0.0,\"note\":null}}}}}";
        var decoded = JsonScalarRecordCodec.Decode(outer, Utf8(json));
        Assert.True(decoded.IsSuccess);
        Assert.Equal(json, Encode(decoded.Value));
        var union = Assert.IsType<JsonTaggedUnion>(decoded.Value.ValueAt(0));
        var map = Assert.IsType<JsonRecordMap>(union.Payload!.ValueAt(0));
        Assert.Same(item, map.Schema);
        Assert.False(map.At(0).Value.IsPresent(1));
        Assert.True(map.At(1).Value.IsPresent(1));
        Assert.Null(map.At(1).Value.ValueAt(1));
        var tree = JsonTypedFieldMapping.FromRecord(decoded.Value);
        Assert.True(tree.IsSuccess);
        Assert.Equal(json, Encode(JsonTypedFieldMapping.ToRecord(outer, tree.Value).Value));
    }

    [Fact]
    public void NullableMapsPreserveMissingNullAndEmpty()
    {
        var item = new JsonScalarRecordSchema([]);
        var schema = new JsonScalarRecordSchema([new("entries", JsonRecordFieldKind.NullableRecordMap, Optional: true, NestedSchema: item)]);
        foreach (var (json, present, isNull) in new[] { ("{}", false, true), ("{\"entries\":null}", true, true), ("{\"entries\":{}}", true, false) })
        {
            var value = JsonScalarRecordCodec.Decode(schema, Utf8(json));
            Assert.True(value.IsSuccess);
            Assert.Equal(present, value.Value.IsPresent(0));
            Assert.Equal(isNull, value.Value.ValueAt(0) is null);
            Assert.Equal(json, Encode(value.Value));
        }
        var required = new JsonScalarRecordSchema([new("entries", JsonRecordFieldKind.RecordMap, NestedSchema: item)]);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.MissingField, "$[\"entries\"]"), JsonScalarRecordCodec.Decode(required, Utf8("{}")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"entries\"]"), JsonScalarRecordCodec.Decode(required, Utf8("{\"entries\":null}")).Error);
    }

    [Fact]
    public void ConstructionCopiesEntriesAndRequiresExactDescriptor()
    {
        var schema = new JsonScalarRecordSchema([new("value", JsonRecordFieldKind.Integer)]);
        var other = new JsonScalarRecordSchema([new("value", JsonRecordFieldKind.Integer)]);
        var original = Record(schema, "{\"value\":1}");
        var input = new[] { Pair("a", original) };
        var map = JsonRecordMap.Create(schema, input);
        input[0] = Pair("changed", original);
        var outer = new JsonScalarRecordSchema([new("entries", JsonRecordFieldKind.RecordMap, NestedSchema: schema)]);
        var record = JsonScalarRecord.Create(outer, [map], [true]);
        Assert.Equal("{\"entries\":{\"a\":{\"value\":1}}}", Encode(record));
        Assert.Throws<ArgumentException>(() => JsonRecordMap.Create(schema, [Pair("a", Record(other, "{\"value\":1}"))]));
        Assert.Throws<ArgumentException>(() => JsonRecordMap.Create(schema, [Pair("a", null!)]));
        Assert.Throws<ArgumentException>(() => JsonRecordMap.Create(schema, [Pair(null!, original)]));
        Assert.Throws<ArgumentException>(() => JsonRecordMap.Create(schema, [Pair("a", original), Pair("a", original)]));
        Assert.Throws<ArgumentException>(() => JsonRecordMap.Create(schema, [Pair("\ud800", original)]));
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(outer, [JsonRecordMap.Create(other, [])], [true]));
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("entries", JsonRecordFieldKind.RecordMap)]));
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("entries", JsonRecordFieldKind.RecordMap, Optional: true, NestedSchema: schema)]));
    }

    [Fact]
    public void InvalidMapDataReportsOnlyIndexesAndDeclaredFields()
    {
        var item = new JsonScalarRecordSchema([new("count", JsonRecordFieldKind.Integer)]);
        var schema = new JsonScalarRecordSchema([new("entries", JsonRecordFieldKind.RecordMap, NestedSchema: item)]);
        var duplicate = JsonScalarRecordCodec.Decode(schema, Utf8("{\"entries\":{\"private\":{\"count\":1},\"\\u0070rivate\":{\"count\":2}}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.DuplicateField, "$[\"entries\"][1]"), duplicate.Error);
        var wrong = JsonScalarRecordCodec.Decode(schema, Utf8("{\"entries\":{\"private\":{\"count\":\"secret\"}}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"entries\"][0][\"count\"]"), wrong.Error);
        var key = JsonScalarRecordCodec.Decode(schema, Utf8("{\"entries\":{\"\\uD800\":{\"count\":1}}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidUnicode, "$[\"entries\"][0]"), key.Error);
        var wrongRoot = JsonScalarRecordCodec.Decode(schema, Utf8("{\"entries\":[]}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"entries\"]"), wrongRoot.Error);
        var nullItem = JsonScalarRecordCodec.Decode(schema, Utf8("{\"entries\":{\"private\":null}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"entries\"][0]"), nullItem.Error);
        foreach (var error in new[] { duplicate.Error, wrong.Error, key.Error, nullItem.Error })
        {
            Assert.DoesNotContain("private", error!.Value.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("secret", error.Value.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MixedUnionMapRecordTreeDepthIsBounded()
    {
        var item = new JsonScalarRecordSchema([new("data", JsonRecordFieldKind.FieldValue)]);
        var payload = new JsonScalarRecordSchema([new("entries", JsonRecordFieldKind.RecordMap, NestedSchema: item)]);
        var cases = new JsonTaggedUnionSchema([new("Data", payload)]);
        JsonTaggedUnion Make(int depth)
        {
            var child = JsonScalarRecord.Create(item, [Nested(depth)], [true]);
            var map = JsonRecordMap.Create(item, [Pair("k", child)]);
            var record = JsonScalarRecord.Create(payload, [map], [true]);
            return JsonTaggedUnion.Create(cases, 0, record);
        }
        var valid = Make(60);
        Assert.Equal(64, valid.Depth);
        var encoded = JsonTaggedUnionCodec.Encode(valid);
        Assert.True(JsonTaggedUnionCodec.Decode(cases, encoded).IsSuccess);
        Assert.Throws<ArgumentException>(() => Make(61));
        var invalid = "{\"case\":\"Data\",\"value\":{\"entries\":{\"k\":{\"data\":" + new string('[', 61) + "null" + new string(']', 61) + "}}}}";
        Assert.Equal(JsonScalarErrorCode.InvalidJson, JsonTaggedUnionCodec.Decode(cases, Utf8(invalid)).Error!.Value.Code);
    }
}
