using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonNullableRecordArrayTests
{
    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);
    private static string Encode(JsonScalarRecord value) => Encoding.UTF8.GetString(JsonScalarRecordCodec.Encode(value));
    private static JsonFieldValue Tree(string json) => JsonFieldValueCodec.Decode(Bytes(json)).Value;

    [Fact]
    public void NullableRecordsRoundTripThroughUnionAndTree()
    {
        var item = new JsonScalarRecordSchema([new("amount", JsonRecordFieldKind.ExactNumber), new("note", JsonRecordFieldKind.NullableString, Optional: true)]);
        var payload = new JsonScalarRecordSchema([new("entries", JsonRecordFieldKind.RecordNullableElementsArray, NestedSchema: item)]);
        var union = new JsonTaggedUnionSchema([new("Data", payload)]);
        var schema = new JsonScalarRecordSchema([new("event", JsonRecordFieldKind.Union, UnionSchema: union)]);
        const string json = "{\"event\":{\"case\":\"Data\",\"value\":{\"entries\":[null,{\"amount\":12345678901234567890.00e+8},{\"amount\":-0.0,\"note\":null}]}}}";
        var record = JsonScalarRecordCodec.Decode(schema, Bytes(json));
        Assert.True(record.IsSuccess);
        Assert.Equal(json, Encode(record.Value));
        var tagged = (JsonTaggedUnion)record.Value.ValueAt(0)!;
        var items = Assert.IsType<JsonNullableRecordArray>(tagged.Payload!.ValueAt(0));
        Assert.Null(items.At(0));
        Assert.False(items.At(1)!.IsPresent(1));
        Assert.True(items.At(2)!.IsPresent(1));
        Assert.Equal(json, Encode(JsonTypedFieldMapping.ToRecord(schema, JsonTypedFieldMapping.FromRecord(record.Value).Value).Value));
    }

    [Fact]
    public void AbsentNullEmptyAndNullElementAreDistinct()
    {
        var item = new JsonScalarRecordSchema([]);
        var schema = new JsonScalarRecordSchema([new("items", JsonRecordFieldKind.NullableRecordNullableElementsArray, Optional: true, NestedSchema: item)]);
        foreach (var (json, present, isNull) in new[] { ("{}", false, true), ("{\"items\":null}", true, true), ("{\"items\":[]}", true, false), ("{\"items\":[null]}", true, false) })
        {
            var value = JsonScalarRecordCodec.Decode(schema, Bytes(json));
            Assert.True(value.IsSuccess);
            Assert.Equal(present, value.Value.IsPresent(0));
            Assert.Equal(isNull, value.Value.ValueAt(0) is null);
            Assert.Equal(json, Encode(value.Value));
        }
        var required = new JsonScalarRecordSchema([new("items", JsonRecordFieldKind.RecordNullableElementsArray, NestedSchema: item)]);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.MissingField, "$[\"items\"]"), JsonScalarRecordCodec.Decode(required, Bytes("{}")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"items\"]"), JsonScalarRecordCodec.Decode(required, Bytes("{\"items\":null}")).Error);
    }

    [Fact]
    public void StandaloneMappingPreservesSnapshotsAndSchemaIdentity()
    {
        var item = new JsonScalarRecordSchema([new("n", JsonRecordFieldKind.Integer)]);
        var other = new JsonScalarRecordSchema([new("n", JsonRecordFieldKind.Integer)]);
        var record = JsonScalarRecordCodec.Decode(item, Bytes("{\"n\":1}")).Value;
        var input = new JsonScalarRecord?[] { null, record };
        var array = JsonNullableRecordArray.Create(item, input);
        input[1] = null;
        var tree = JsonTypedFieldMapping.FromNullableRecords(array);
        Assert.True(tree.IsSuccess);
        Assert.Equal("[null,{\"n\":1}]", Encoding.UTF8.GetString(JsonFieldValueCodec.Encode(tree.Value)));
        var restored = JsonTypedFieldMapping.ToNullableRecords(item, tree.Value);
        Assert.True(restored.IsSuccess);
        Assert.Same(item, restored.Value.Schema);
        Assert.Null(restored.Value.At(0));
        Assert.NotNull(restored.Value.At(1));
        Assert.Throws<ArgumentException>(() => JsonNullableRecordArray.Create(item, [JsonScalarRecord.Create(other, [1], [true])]));
        var schema = new JsonScalarRecordSchema([new("items", JsonRecordFieldKind.RecordNullableElementsArray, NestedSchema: item)]);
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(schema, [JsonNullableRecordArray.Create(other, [])], [true]));
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("items", JsonRecordFieldKind.RecordNullableElementsArray)]));
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("items", JsonRecordFieldKind.RecordNullableElementsArray, Optional: true, NestedSchema: item)]));
    }

    [Fact]
    public void InvalidItemsUseRedactedIndexesInBothPaths()
    {
        var item = new JsonScalarRecordSchema([new("n", JsonRecordFieldKind.Integer)]);
        var schema = new JsonScalarRecordSchema([new("items", JsonRecordFieldKind.RecordNullableElementsArray, NestedSchema: item)]);
        var failed = JsonScalarRecordCodec.Decode(schema, Bytes("{\"items\":[null,{\"n\":\"secret\"}]}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"items\"][1][\"n\"]"), failed.Error);
        var mapped = JsonTypedFieldMapping.ToNullableRecords(item, Tree("[null,{\"private\":\"secret\"}]"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.UnknownField, "$[1]"), mapped.Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$"), JsonTypedFieldMapping.ToNullableRecords(item, Tree("{}")).Error);
        foreach (var error in new[] { failed.Error, mapped.Error })
        {
            Assert.DoesNotContain("private", error!.Value.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("secret", error.Value.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ArrayAndRecordShareDepthLimit()
    {
        var item = new JsonScalarRecordSchema([]);
        var record = JsonScalarRecord.Create(item, [], []);
        for (var depth = 2; depth <= 62; depth++)
        {
            item = new JsonScalarRecordSchema([new("child", JsonRecordFieldKind.Record, NestedSchema: item)]);
            record = JsonScalarRecord.Create(item, [record], [true]);
        }
        var array = JsonNullableRecordArray.Create(item, [null, record]);
        var schema = new JsonScalarRecordSchema([new("items", JsonRecordFieldKind.RecordNullableElementsArray, NestedSchema: item)]);
        var outer = JsonScalarRecord.Create(schema, [array], [true]);
        Assert.Equal(64, outer.Depth);
        Assert.True(JsonScalarRecordCodec.Decode(schema, JsonScalarRecordCodec.Encode(outer)).IsSuccess);
        Assert.True(JsonTypedFieldMapping.ToNullableRecords(item, JsonTypedFieldMapping.FromNullableRecords(array).Value).IsSuccess);
        var tooDeep = new JsonScalarRecordSchema([new("child", JsonRecordFieldKind.Record, NestedSchema: item)]);
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("items", JsonRecordFieldKind.RecordNullableElementsArray, NestedSchema: tooDeep)]));
    }
}
