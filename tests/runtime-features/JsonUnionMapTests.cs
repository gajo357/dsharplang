using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonUnionMapTests
{
    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);
    private static string Encode(JsonScalarRecord value) => Encoding.UTF8.GetString(JsonScalarRecordCodec.Encode(value));

    [Fact]
    public void UnionMapsRoundTripInsideUnionAndTree()
    {
        var item = new JsonScalarRecordSchema([new("amount", JsonRecordFieldKind.ExactNumber), new("note", JsonRecordFieldKind.NullableString, Optional: true)]);
        var cases = new JsonTaggedUnionSchema([new("Idle"), new("Data", item)]);
        var payload = new JsonScalarRecordSchema([new("entries", JsonRecordFieldKind.UnionMap, UnionSchema: cases)]);
        var outerCases = new JsonTaggedUnionSchema([new("Envelope", payload)]);
        var schema = new JsonScalarRecordSchema([new("event", JsonRecordFieldKind.Union, UnionSchema: outerCases)]);
        const string json = "{\"event\":{\"case\":\"Envelope\",\"value\":{\"entries\":{\"first\":{\"case\":\"Idle\"},\"second\":{\"case\":\"Data\",\"value\":{\"amount\":12345678901234567890.00e+8,\"note\":null}}}}}}";
        var decoded = JsonScalarRecordCodec.Decode(schema, Bytes(json));
        Assert.True(decoded.IsSuccess);
        Assert.Equal(json, Encode(decoded.Value));
        var outer = (JsonTaggedUnion)decoded.Value.ValueAt(0)!;
        var map = Assert.IsType<JsonUnionMap>(outer.Payload!.ValueAt(0));
        Assert.Same(cases, map.Schema);
        Assert.Equal("first", map.At(0).Key);
        Assert.Equal(json, Encode(JsonTypedFieldMapping.ToRecord(schema, JsonTypedFieldMapping.FromRecord(decoded.Value).Value).Value));
    }

    [Fact]
    public void MissingNullEmptyAndRequiredMapsAreDistinct()
    {
        var cases = new JsonTaggedUnionSchema([new("Idle")]);
        var schema = new JsonScalarRecordSchema([new("events", JsonRecordFieldKind.NullableUnionMap, Optional: true, UnionSchema: cases)]);
        foreach (var (json, present, isNull) in new[] { ("{}", false, true), ("{\"events\":null}", true, true), ("{\"events\":{}}", true, false) })
        {
            var result = JsonScalarRecordCodec.Decode(schema, Bytes(json));
            Assert.True(result.IsSuccess);
            Assert.Equal(present, result.Value.IsPresent(0));
            Assert.Equal(isNull, result.Value.ValueAt(0) is null);
            Assert.Equal(json, Encode(result.Value));
        }
        var required = new JsonScalarRecordSchema([new("events", JsonRecordFieldKind.UnionMap, UnionSchema: cases)]);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.MissingField, "$[\"events\"]"), JsonScalarRecordCodec.Decode(required, Bytes("{}")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"events\"]"), JsonScalarRecordCodec.Decode(required, Bytes("{\"events\":null}")).Error);
    }

    [Fact]
    public void EntriesAreCopiedAndRequireExactDescriptor()
    {
        var cases = new JsonTaggedUnionSchema([new("Idle")]);
        var foreign = new JsonTaggedUnionSchema([new("Idle")]);
        var item = JsonTaggedUnion.Create(cases, 0);
        var input = new[] { new KeyValuePair<string, JsonTaggedUnion>("a", item) };
        var map = JsonUnionMap.Create(cases, input);
        input[0] = new("changed", item);
        var schema = new JsonScalarRecordSchema([new("events", JsonRecordFieldKind.UnionMap, UnionSchema: cases)]);
        Assert.Equal("{\"events\":{\"a\":{\"case\":\"Idle\"}}}", Encode(JsonScalarRecord.Create(schema, [map], [true])));
        Assert.Throws<ArgumentException>(() => JsonUnionMap.Create(cases, [new("a", JsonTaggedUnion.Create(foreign, 0))]));
        Assert.Throws<ArgumentException>(() => JsonUnionMap.Create(cases, [new("a", null!)]));
        Assert.Throws<ArgumentException>(() => JsonUnionMap.Create(cases, [new(null!, item)]));
        Assert.Throws<ArgumentException>(() => JsonUnionMap.Create(cases, [new("a", item), new("a", item)]));
        Assert.Throws<ArgumentException>(() => JsonUnionMap.Create(cases, [new("\ud800", item)]));
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(schema, [JsonUnionMap.Create(foreign, [])], [true]));
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("events", JsonRecordFieldKind.UnionMap)]));
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("events", JsonRecordFieldKind.UnionMap, Optional: true, UnionSchema: cases)]));
    }

    [Fact]
    public void InvalidInputIsIndexedAndRedacted()
    {
        var payload = new JsonScalarRecordSchema([new("count", JsonRecordFieldKind.Integer)]);
        var cases = new JsonTaggedUnionSchema([new("Data", payload)]);
        var schema = new JsonScalarRecordSchema([new("events", JsonRecordFieldKind.UnionMap, UnionSchema: cases)]);
        var wrong = JsonScalarRecordCodec.Decode(schema, Bytes("{\"events\":{\"private\":{\"case\":\"Data\",\"value\":{\"count\":\"secret\"}}}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"events\"][0][\"value\"][\"count\"]"), wrong.Error);
        var duplicate = JsonScalarRecordCodec.Decode(schema, Bytes("{\"events\":{\"private\":{\"case\":\"Data\",\"value\":{\"count\":1}},\"\\u0070rivate\":{\"case\":\"Data\",\"value\":{\"count\":2}}}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.DuplicateField, "$[\"events\"][1]"), duplicate.Error);
        var key = JsonScalarRecordCodec.Decode(schema, Bytes("{\"events\":{\"\\uD800\":null}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidUnicode, "$[\"events\"][0]"), key.Error);
        var unknown = JsonScalarRecordCodec.Decode(schema, Bytes("{\"events\":{\"private\":{\"case\":\"secret\"}}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.UnknownCase, "$[\"events\"][0][\"case\"]"), unknown.Error);
        var nullItem = JsonScalarRecordCodec.Decode(schema, Bytes("{\"events\":{\"private\":null}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"events\"][0]"), nullItem.Error);
        foreach (var error in new[] { wrong.Error, duplicate.Error, key.Error, unknown.Error, nullItem.Error })
        {
            Assert.DoesNotContain("private", error!.Value.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("secret", error.Value.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void UnionMapsAndPayloadsShareDepthLimit()
    {
        var payload = new JsonScalarRecordSchema([]);
        var record = JsonScalarRecord.Create(payload, [], []);
        for (var depth = 2; depth <= 61; depth++)
        {
            payload = new JsonScalarRecordSchema([new("child", JsonRecordFieldKind.Record, NestedSchema: payload)]);
            record = JsonScalarRecord.Create(payload, [record], [true]);
        }
        var cases = new JsonTaggedUnionSchema([new("Deep", payload)]);
        var schema = new JsonScalarRecordSchema([new("events", JsonRecordFieldKind.UnionMap, UnionSchema: cases)]);
        var map = JsonUnionMap.Create(cases, [new("a", JsonTaggedUnion.Create(cases, 0, record))]);
        var value = JsonScalarRecord.Create(schema, [map], [true]);
        Assert.Equal(64, value.Depth);
        Assert.True(JsonScalarRecordCodec.Decode(schema, JsonScalarRecordCodec.Encode(value)).IsSuccess);
        var tooDeep = new JsonScalarRecordSchema([new("child", JsonRecordFieldKind.Record, NestedSchema: payload)]);
        var deeperCases = new JsonTaggedUnionSchema([new("Deep", tooDeep)]);
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("events", JsonRecordFieldKind.UnionMap, UnionSchema: deeperCases)]));
    }
}
