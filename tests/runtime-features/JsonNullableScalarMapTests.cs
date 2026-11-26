using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonNullableScalarMapTests
{
    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);
    private static string Encode(JsonScalarRecord record) => Encoding.UTF8.GetString(JsonScalarRecordCodec.Encode(record));

    [Fact]
    public void NullableValuesOfEveryKindRoundTripThroughUnionAndTree()
    {
        var payload = new JsonScalarRecordSchema([
            new("flags", JsonRecordFieldKind.BooleanNullableValuesMap), new("counts", JsonRecordFieldKind.IntegerNullableValuesMap),
            new("names", JsonRecordFieldKind.StringNullableValuesMap), new("numbers", JsonRecordFieldKind.ExactNumberNullableValuesMap)]);
        var union = new JsonTaggedUnionSchema([new("Data", payload)]);
        var schema = new JsonScalarRecordSchema([new("event", JsonRecordFieldKind.Union, UnionSchema: union)]);
        const string json = "{\"event\":{\"case\":\"Data\",\"value\":{\"flags\":{\"a\":true,\"b\":null},\"counts\":{\"a\":-9,\"b\":null},\"names\":{\"a\":\"text\",\"b\":null},\"numbers\":{\"a\":12345678901234567890.00e+8,\"b\":null}}}}";
        var record = JsonScalarRecordCodec.Decode(schema, Bytes(json));
        Assert.True(record.IsSuccess);
        Assert.Equal(json, Encode(record.Value));
        var tree = JsonTypedFieldMapping.FromRecord(record.Value);
        Assert.True(tree.IsSuccess);
        Assert.Equal(json, Encode(JsonTypedFieldMapping.ToRecord(schema, tree.Value).Value));
    }

    [Fact]
    public void MissingNullEmptyAndNullEntryStayDistinct()
    {
        var schema = new JsonScalarRecordSchema([new("items", JsonRecordFieldKind.NullableStringNullableValuesMap, Optional: true)]);
        foreach (var (json, present, mapNull) in new[] { ("{}", false, true), ("{\"items\":null}", true, true), ("{\"items\":{}}", true, false), ("{\"items\":{\"a\":null}}", true, false) })
        {
            var result = JsonScalarRecordCodec.Decode(schema, Bytes(json));
            Assert.True(result.IsSuccess);
            Assert.Equal(present, result.Value.IsPresent(0));
            Assert.Equal(mapNull, result.Value.ValueAt(0) is null);
            Assert.Equal(json, Encode(result.Value));
        }
        var required = new JsonScalarRecordSchema([new("items", JsonRecordFieldKind.StringNullableValuesMap)]);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.MissingField, "$[\"items\"]"), JsonScalarRecordCodec.Decode(required, Bytes("{}")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"items\"]"), JsonScalarRecordCodec.Decode(required, Bytes("{\"items\":null}")).Error);
        Assert.True(JsonScalarRecordCodec.Decode(required, Bytes("{\"items\":{\"a\":null}}" )).IsSuccess);
    }

    [Fact]
    public void SnapshotAndDescriptorChecksDistinguishNullableValues()
    {
        var entries = new[] { new KeyValuePair<string, object>("a", null!), new KeyValuePair<string, object>("b", 3) };
        var map = JsonRecordScalarMap.Create(JsonRecordFieldKind.NullableInteger, entries);
        entries[0] = new("changed", 7);
        var schema = new JsonScalarRecordSchema([new("items", JsonRecordFieldKind.IntegerNullableValuesMap)]);
        Assert.Equal("{\"items\":{\"a\":null,\"b\":3}}", Encode(JsonScalarRecord.Create(schema, [map], [true])));
        Assert.Throws<ArgumentException>(() => JsonRecordScalarMap.Create(JsonRecordFieldKind.NullableInteger, [new("a", "bad")]));
        Assert.Throws<ArgumentException>(() => JsonRecordScalarMap.Create(JsonRecordFieldKind.NullableString, [new("a", "\ud800")]));
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(schema, [JsonRecordScalarMap.Create(JsonRecordFieldKind.Integer, [])], [true]));
        var nonNullable = new JsonScalarRecordSchema([new("items", JsonRecordFieldKind.IntegerMap)]);
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(nonNullable, [map], [true]));
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("items", JsonRecordFieldKind.IntegerNullableValuesMap, Optional: true)]));
    }

    [Fact]
    public void InvalidValuesAndDuplicateKeysAreRedacted()
    {
        var schema = new JsonScalarRecordSchema([new("items", JsonRecordFieldKind.IntegerNullableValuesMap)]);
        var wrong = JsonScalarRecordCodec.Decode(schema, Bytes("{\"items\":{\"private\":null,\"other\":\"secret\"}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"items\"][1]"), wrong.Error);
        var duplicate = JsonScalarRecordCodec.Decode(schema, Bytes("{\"items\":{\"private\":null,\"\\u0070rivate\":1}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.DuplicateField, "$[\"items\"][1]"), duplicate.Error);
        var invalid = JsonScalarRecordCodec.Decode(schema, Bytes("{\"items\":{\"\\uD800\":null}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidUnicode, "$[\"items\"][0]"), invalid.Error);
        foreach (var error in new[] { wrong.Error, duplicate.Error, invalid.Error })
        {
            Assert.DoesNotContain("private", error!.Value.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("secret", error.Value.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void NullableMapStillContributesToDepth()
    {
        var schema = new JsonScalarRecordSchema([new("items", JsonRecordFieldKind.BooleanNullableValuesMap)]);
        var map = JsonRecordScalarMap.Create(JsonRecordFieldKind.NullableBoolean, [new("a", null!)]);
        var record = JsonScalarRecord.Create(schema, [map], [true]);
        for (var i = 0; i < 62; i++)
        {
            var outer = new JsonScalarRecordSchema([new("child", JsonRecordFieldKind.Record, NestedSchema: schema)]);
            record = JsonScalarRecord.Create(outer, [record], [true]);
            schema = outer;
        }
        Assert.Equal(64, record.Depth);
        Assert.True(JsonScalarRecordCodec.Decode(schema, JsonScalarRecordCodec.Encode(record)).IsSuccess);
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("child", JsonRecordFieldKind.Record, NestedSchema: schema)]));
    }
}
