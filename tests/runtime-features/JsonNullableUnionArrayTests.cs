using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonNullableUnionArrayTests
{
    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    [Fact]
    public void NullableElementsPreserveCasesPayloadsAndSnapshot()
    {
        var payload = new JsonScalarRecordSchema([new("n", JsonRecordFieldKind.ExactNumber)]);
        var union = new JsonTaggedUnionSchema([new("Idle"), new("Data", payload)]);
        var number = JsonExactNumberCodec.Decode(Utf8("-12345678901234567890.00e+5")).Value;
        var record = JsonScalarRecord.Create(payload, [number], [true]);
        var input = new JsonTaggedUnion?[] { null, JsonTaggedUnion.Create(union, 0), JsonTaggedUnion.Create(union, 1, record) };
        var array = JsonNullableUnionArray.Create(union, input);
        input[0] = input[1]; input[2] = null;
        var schema = new JsonScalarRecordSchema([new("events", JsonRecordFieldKind.UnionNullableElementsArray, UnionSchema: union)]);
        var snapshot = JsonScalarRecord.Create(schema, [array], [true]);
        var encoded = JsonScalarRecordCodec.Encode(snapshot);
        Assert.Equal("{\"events\":[null,{\"case\":\"Idle\"},{\"case\":\"Data\",\"value\":{\"n\":-12345678901234567890.00e+5}}]}", Encoding.UTF8.GetString(encoded));
        var decoded = JsonScalarRecordCodec.Decode(schema, encoded);
        Assert.True(decoded.IsSuccess);
        Assert.Equal(encoded, JsonScalarRecordCodec.Encode(decoded.Value));
    }

    [Fact]
    public void MissingNullEmptyAndNullElementRemainDistinct()
    {
        var union = new JsonTaggedUnionSchema([new("Idle")]);
        var schema = new JsonScalarRecordSchema([new("events", JsonRecordFieldKind.NullableUnionNullableElementsArray, Optional: true, UnionSchema: union)]);
        foreach (var json in new[] { "{}", "{\"events\":null}", "{\"events\":[]}", "{\"events\":[null]}", "{\"events\":[{\"case\":\"Idle\"}]}" })
        {
            var result = JsonScalarRecordCodec.Decode(schema, Utf8(json));
            Assert.True(result.IsSuccess);
            Assert.Equal(json, Encoding.UTF8.GetString(JsonScalarRecordCodec.Encode(result.Value)));
        }
    }

    [Fact]
    public void RequiredAndNullableElementDescriptorsCannotBeInterchanged()
    {
        var union = new JsonTaggedUnionSchema([new("Idle")]);
        var foreign = new JsonTaggedUnionSchema([new("Idle")]);
        var nullable = new JsonScalarRecordSchema([new("events", JsonRecordFieldKind.UnionNullableElementsArray, UnionSchema: union)]);
        var required = new JsonScalarRecordSchema([new("events", JsonRecordFieldKind.UnionArray, UnionSchema: union)]);
        Assert.Throws<ArgumentException>(() => JsonNullableUnionArray.Create(union, [JsonTaggedUnion.Create(foreign, 0)]));
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(nullable, [JsonUnionArray.Create(union, [])], [true]));
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(required, [JsonNullableUnionArray.Create(union, [null])], [true]));
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(nullable, [JsonNullableUnionArray.Create(foreign, [null])], [true]));
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("events", JsonRecordFieldKind.UnionNullableElementsArray, Optional: true, UnionSchema: union)]));
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("events", JsonRecordFieldKind.UnionNullableElementsArray)]));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"events\"][0]"), JsonScalarRecordCodec.Decode(required, Utf8("{\"events\":[null]}")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"events\"]"), JsonScalarRecordCodec.Decode(nullable, Utf8("{\"events\":null}")).Error);
    }

    [Fact]
    public void NestedErrorPathsKeepNullIndicesAndRedactInput()
    {
        var payload = new JsonScalarRecordSchema([new("amount", JsonRecordFieldKind.Integer)]);
        var union = new JsonTaggedUnionSchema([new("Idle"), new("Data", payload)]);
        var inner = new JsonScalarRecordSchema([new("events", JsonRecordFieldKind.UnionNullableElementsArray, UnionSchema: union)]);
        var schema = new JsonScalarRecordSchema([new("entry", JsonRecordFieldKind.Record, NestedSchema: inner)]);
        var bad = JsonScalarRecordCodec.Decode(schema, Utf8("{\"entry\":{\"events\":[null,{\"case\":\"Data\",\"value\":{\"amount\":\"secret\"}}]}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"entry\"][\"events\"][1][\"value\"][\"amount\"]"), bad.Error);
        Assert.DoesNotContain("secret", bad.Error!.Value.ToString(), StringComparison.Ordinal);
        var unknown = JsonScalarRecordCodec.Decode(schema, Utf8("{\"entry\":{\"events\":[null,{\"case\":\"private-tag\"}]}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.UnknownCase, "$[\"entry\"][\"events\"][1][\"case\"]"), unknown.Error);
        Assert.DoesNotContain("private-tag", unknown.Error!.Value.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void NullableUnionElementsRespectCombinedDepthBound()
    {
        var payload = new JsonScalarRecordSchema([]);
        var record = JsonScalarRecord.Create(payload, [], []);
        for (var depth = 2; depth <= 61; depth++)
        {
            payload = new JsonScalarRecordSchema([new("child", JsonRecordFieldKind.Record, NestedSchema: payload)]);
            record = JsonScalarRecord.Create(payload, [record], [true]);
        }
        var union = new JsonTaggedUnionSchema([new("Deep", payload)]);
        var schema = new JsonScalarRecordSchema([new("events", JsonRecordFieldKind.UnionNullableElementsArray, UnionSchema: union)]);
        var snapshot = JsonScalarRecord.Create(schema, [JsonNullableUnionArray.Create(union, [null, JsonTaggedUnion.Create(union, 0, record)])], [true]);
        Assert.Equal(64, schema.Depth);
        Assert.True(JsonScalarRecordCodec.Decode(schema, JsonScalarRecordCodec.Encode(snapshot)).IsSuccess);
        var tooDeep = new JsonScalarRecordSchema([new("child", JsonRecordFieldKind.Record, NestedSchema: payload)]);
        var deeperUnion = new JsonTaggedUnionSchema([new("Deep", tooDeep)]);
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("events", JsonRecordFieldKind.UnionNullableElementsArray, UnionSchema: deeperUnion)]));
    }
}
