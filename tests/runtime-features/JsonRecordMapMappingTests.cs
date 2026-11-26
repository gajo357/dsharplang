using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonRecordMapMappingTests
{
    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);
    private static string Json(JsonFieldValue value) => Encoding.UTF8.GetString(JsonFieldValueCodec.Encode(value));
    private static JsonScalarRecord Record(JsonScalarRecordSchema schema, string json) => JsonScalarRecordCodec.Decode(schema, Bytes(json)).Value;
    private static JsonFieldValue Tree(string json) => JsonFieldValueCodec.Decode(Bytes(json)).Value;
    private static JsonFieldValue Nested(int count)
    {
        var value = JsonFieldValue.Null();
        for (var index = 0; index < count; index++) value = JsonFieldValue.List([value]);
        return value;
    }

    [Fact]
    public void MapRoundTripsNestedTypedValuesAndExactNumbers()
    {
        var payload = new JsonScalarRecordSchema([new("amount", JsonRecordFieldKind.ExactNumber), new("note", JsonRecordFieldKind.NullableString, Optional: true)]);
        var choice = new JsonTaggedUnionSchema([new("Data", payload)]);
        var schema = new JsonScalarRecordSchema([new("event", JsonRecordFieldKind.Union, UnionSchema: choice)]);
        const string json = "{\"first\":{\"event\":{\"case\":\"Data\",\"value\":{\"amount\":12345678901234567890.00e+8}}},\"second\":{\"event\":{\"case\":\"Data\",\"value\":{\"amount\":-0.0,\"note\":null}}}}";
        var mapped = JsonTypedFieldMapping.ToRecordMap(schema, Tree(json));
        Assert.True(mapped.IsSuccess);
        Assert.Same(schema, mapped.Value.Schema);
        Assert.Equal("first", mapped.Value.At(0).Key);
        Assert.Equal(json, Json(JsonTypedFieldMapping.FromRecordMap(mapped.Value).Value));
        var first = (JsonTaggedUnion)mapped.Value.At(0).Value.ValueAt(0)!;
        var second = (JsonTaggedUnion)mapped.Value.At(1).Value.ValueAt(0)!;
        Assert.False(first.Payload!.IsPresent(1));
        Assert.True(second.Payload!.IsPresent(1));
        Assert.Null(second.Payload.ValueAt(1));
    }

    [Fact]
    public void EmptyAndSnapshotsPreserveExplicitDescriptor()
    {
        var schema = new JsonScalarRecordSchema([new("n", JsonRecordFieldKind.Integer)]);
        var foreign = new JsonScalarRecordSchema([new("n", JsonRecordFieldKind.Integer)]);
        var entries = new[] { new KeyValuePair<string, JsonScalarRecord>("k", Record(schema, "{\"n\":1}")) };
        var map = JsonRecordMap.Create(schema, entries);
        entries[0] = new("changed", Record(schema, "{\"n\":2}"));
        var tree = JsonTypedFieldMapping.FromRecordMap(map).Value;
        Assert.Equal("{\"k\":{\"n\":1}}", Json(tree));
        var copy = tree.MapItems;
        copy[0] = new("changed", Tree("{}"));
        Assert.Equal("{\"k\":{\"n\":1}}", Json(tree));
        Assert.Same(foreign, JsonTypedFieldMapping.ToRecordMap(foreign, tree).Value.Schema);
        var empty = JsonTypedFieldMapping.ToRecordMap(schema, Tree("{}"));
        Assert.True(empty.IsSuccess);
        Assert.Equal(0, empty.Value.Count);
        Assert.Equal("{}", Json(JsonTypedFieldMapping.FromRecordMap(empty.Value).Value));
    }

    [Fact]
    public void BadShapesAndValuesReturnRedactedIndexedErrors()
    {
        var schema = new JsonScalarRecordSchema([new("n", JsonRecordFieldKind.Integer)]);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$"), JsonTypedFieldMapping.ToRecordMap(schema, Tree("[]")).Error);
        var bad = JsonTypedFieldMapping.ToRecordMap(schema, Tree("{\"private\":{\"n\":\"secret\"}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[0][\"n\"]"), bad.Error);
        Assert.DoesNotContain("private", bad.Error!.Value.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("secret", bad.Error.Value.ToString(), StringComparison.Ordinal);
        var unknown = JsonTypedFieldMapping.ToRecordMap(schema, Tree("{\"private\":{\"n\":1,\"hidden\":2}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.UnknownField, "$[0]"), unknown.Error);
        var nullValue = JsonTypedFieldMapping.ToRecordMap(schema, Tree("{\"private\":null}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[0]"), nullValue.Error);
    }

    [Fact]
    public void MapAndRecordShareContainerDepthLimit()
    {
        var schema = new JsonScalarRecordSchema([new("v", JsonRecordFieldKind.FieldValue)]);
        JsonRecordMap Make(int count) => JsonRecordMap.Create(schema,
            [new("k", JsonScalarRecord.Create(schema, [Nested(count)], [true]))]);
        var valid = Make(62);
        Assert.Equal(64, valid.Depth);
        var tree = JsonTypedFieldMapping.FromRecordMap(valid).Value;
        Assert.Equal(64, tree.Depth);
        Assert.True(JsonTypedFieldMapping.ToRecordMap(schema, tree).IsSuccess);
        Assert.Throws<ArgumentException>(() => JsonTypedFieldMapping.FromRecordMap(Make(63)));
    }
}
