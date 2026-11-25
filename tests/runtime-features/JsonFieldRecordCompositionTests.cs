using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonFieldRecordCompositionTests
{
    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
    private static string Encode(JsonScalarRecord value) => Encoding.UTF8.GetString(JsonScalarRecordCodec.Encode(value));
    private static JsonFieldValue Nested(int depth)
    {
        var result = JsonFieldValue.Null();
        for (var i = 0; i < depth; i++) result = JsonFieldValue.List([result]);
        return result;
    }

    [Fact]
    public void TreeRoundTripsThroughRecordAndUnionWithoutLosingPrecision()
    {
        var payload = new JsonScalarRecordSchema([new("fields", JsonRecordFieldKind.FieldValue)]);
        var cases = new JsonTaggedUnionSchema([new("Data", payload)]);
        var outer = new JsonScalarRecordSchema([new("event", JsonRecordFieldKind.Union, UnionSchema: cases)]);
        const string json = "{\"event\":{\"case\":\"Data\",\"value\":{\"fields\":{\"amount\":[123456789012345678901234567890.000e+8,null,true]}}}}";
        var decoded = JsonScalarRecordCodec.Decode(outer, Utf8(json));
        Assert.True(decoded.IsSuccess);
        Assert.Equal(json, Encode(decoded.Value));
        var union = Assert.IsType<JsonTaggedUnion>(decoded.Value.ValueAt(0));
        var tree = Assert.IsType<JsonFieldValue>(union.Payload!.ValueAt(0));
        Assert.Equal(JsonFieldValueKind.Map, tree.Kind);
        var pairs = tree.MapItems;
        pairs[0] = new("changed", JsonFieldValue.Null());
        Assert.Equal(json, Encode(decoded.Value));
    }

    [Fact]
    public void ExplicitNullIsAValueAndMissingOrForeignValuesAreRejected()
    {
        var schema = new JsonScalarRecordSchema([new("data", JsonRecordFieldKind.FieldValue)]);
        var decoded = JsonScalarRecordCodec.Decode(schema, Utf8("{\"data\":null}"));
        Assert.True(decoded.IsSuccess);
        Assert.Equal(JsonFieldValueKind.Null, Assert.IsType<JsonFieldValue>(decoded.Value.ValueAt(0)).Kind);
        Assert.Equal("{\"data\":null}", Encode(decoded.Value));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.MissingField, "$[\"data\"]"), JsonScalarRecordCodec.Decode(schema, Utf8("{}")).Error);
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(schema, [null], [true]));
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(schema, [new object()], [true]));
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("data", JsonRecordFieldKind.FieldValue, Optional: true)]));
    }

    [Fact]
    public void NestedErrorsContainOnlyDeclaredNamesAndIndexes()
    {
        var payload = new JsonScalarRecordSchema([new("fields", JsonRecordFieldKind.FieldValue)]);
        var cases = new JsonTaggedUnionSchema([new("Data", payload)]);
        var outer = new JsonScalarRecordSchema([new("event", JsonRecordFieldKind.Union, UnionSchema: cases)]);
        var duplicate = JsonScalarRecordCodec.Decode(outer, Utf8("{\"event\":{\"case\":\"Data\",\"value\":{\"fields\":{\"private\":[0,{\"secret\":1,\"secret\":2}]}}}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.DuplicateField, "$[\"event\"][\"value\"][\"fields\"][0][1][1]"), duplicate.Error);
        Assert.DoesNotContain("secret", duplicate.Error!.Value.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("private", duplicate.Error.Value.ToString(), StringComparison.Ordinal);
        var invalid = JsonScalarRecordCodec.Decode(outer, Utf8("{\"event\":{\"case\":\"Data\",\"value\":{\"fields\":[\"\\uD800\"]}}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidUnicode, "$[\"event\"][\"value\"][\"fields\"][0]"), invalid.Error);
    }

    [Fact]
    public void CombinedTreeRecordUnionArrayDepthIsBounded()
    {
        var payload = new JsonScalarRecordSchema([new("fields", JsonRecordFieldKind.FieldValue)]);
        var cases = new JsonTaggedUnionSchema([new("Data", payload)]);
        var outer = new JsonScalarRecordSchema([new("events", JsonRecordFieldKind.UnionArray, UnionSchema: cases)]);
        JsonTaggedUnion MakeUnion(int treeDepth) => JsonTaggedUnion.Create(cases, 0, JsonScalarRecord.Create(payload, [Nested(treeDepth)], [true]));
        var union = MakeUnion(60);
        var record = JsonScalarRecord.Create(outer, [JsonUnionArray.Create(cases, [union])], [true]);
        Assert.Equal(64, record.Depth);
        var encoded = JsonScalarRecordCodec.Encode(record);
        Assert.True(JsonScalarRecordCodec.Decode(outer, encoded).IsSuccess);
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(outer, [JsonUnionArray.Create(cases, [MakeUnion(61)])], [true]));
        var tooDeep = JsonScalarRecord.Create(payload, [Nested(63)], [true]);
        Assert.Throws<ArgumentException>(() => JsonTaggedUnion.Create(cases, 0, tooDeep));
        var invalidJsonDepth = "{\"events\":[{\"case\":\"Data\",\"value\":{\"fields\":" + new string('[', 61) + "null" + new string(']', 61) + "}}]}";
        Assert.Equal(JsonScalarErrorCode.InvalidJson, JsonScalarRecordCodec.Decode(outer, Utf8(invalidJsonDepth)).Error!.Value.Code);
    }

    [Fact]
    public void DepthPropagatesThroughRecordAndNullableUnionArrays()
    {
        var payload = new JsonScalarRecordSchema([new("fields", JsonRecordFieldKind.FieldValue)]);
        var records = new JsonScalarRecordSchema([new("items", JsonRecordFieldKind.RecordArray, NestedSchema: payload)]);
        var cases = new JsonTaggedUnionSchema([new("Data", payload)]);
        var unions = new JsonScalarRecordSchema([new("items", JsonRecordFieldKind.UnionNullableElementsArray, UnionSchema: cases)]);
        JsonScalarRecord MakePayload(int depth) => JsonScalarRecord.Create(payload, [Nested(depth)], [true]);

        var recordArray = JsonRecordArray.Create(payload, [MakePayload(61)]);
        var record = JsonScalarRecord.Create(records, [recordArray], [true]);
        Assert.Equal(64, record.Depth);
        Assert.True(JsonScalarRecordCodec.Decode(records, JsonScalarRecordCodec.Encode(record)).IsSuccess);
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(records, [JsonRecordArray.Create(payload, [MakePayload(62)])], [true]));

        var union = JsonTaggedUnion.Create(cases, 0, MakePayload(60));
        var array = JsonNullableUnionArray.Create(cases, [null, union]);
        var outer = JsonScalarRecord.Create(unions, [array], [true]);
        Assert.Equal(64, outer.Depth);
        Assert.True(JsonScalarRecordCodec.Decode(unions, JsonScalarRecordCodec.Encode(outer)).IsSuccess);
        var tooDeep = JsonTaggedUnion.Create(cases, 0, MakePayload(61));
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(unions, [JsonNullableUnionArray.Create(cases, [tooDeep])], [true]));
    }
}
