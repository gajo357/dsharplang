using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonUnionArrayTests
{
    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    [Fact]
    public void UnionArraysRoundTripCasesAndSnapshotInputs()
    {
        var payload = new JsonScalarRecordSchema([new("number", JsonRecordFieldKind.ExactNumber)]);
        var union = new JsonTaggedUnionSchema([new("Idle"), new("Data", payload)]);
        var number = JsonExactNumberCodec.Decode(Utf8("12345678901234567890.000")).Value;
        var value = JsonScalarRecord.Create(payload, [number], [true]);
        var input = new[] { JsonTaggedUnion.Create(union, 0), JsonTaggedUnion.Create(union, 1, value) };
        var array = JsonUnionArray.Create(union, input);
        input[1] = input[0];
        var schema = new JsonScalarRecordSchema([new("events", JsonRecordFieldKind.UnionArray, UnionSchema: union)]);
        var record = JsonScalarRecord.Create(schema, [array], [true]);
        var encoded = JsonScalarRecordCodec.Encode(record);
        Assert.Equal("{\"events\":[{\"case\":\"Idle\"},{\"case\":\"Data\",\"value\":{\"number\":12345678901234567890.000}}]}", Encoding.UTF8.GetString(encoded));
        var decoded = JsonScalarRecordCodec.Decode(schema, encoded);
        Assert.True(decoded.IsSuccess);
        Assert.Equal(encoded, JsonScalarRecordCodec.Encode(decoded.Value));
    }

    [Fact]
    public void MissingNullAndEmptyArraysRemainDistinct()
    {
        var union = new JsonTaggedUnionSchema([new("Idle")]);
        var schema = new JsonScalarRecordSchema([new("events", JsonRecordFieldKind.NullableUnionArray, Optional: true, UnionSchema: union)]);
        foreach (var json in new[] { "{}", "{\"events\":null}", "{\"events\":[]}" })
        {
            var decoded = JsonScalarRecordCodec.Decode(schema, Utf8(json));
            Assert.True(decoded.IsSuccess);
            Assert.Equal(json, Encoding.UTF8.GetString(JsonScalarRecordCodec.Encode(decoded.Value)));
        }
    }

    [Fact]
    public void SchemaIdentityAndRequiredElementsAreEnforced()
    {
        var union = new JsonTaggedUnionSchema([new("Idle")]);
        var foreign = new JsonTaggedUnionSchema([new("Idle")]);
        var schema = new JsonScalarRecordSchema([new("events", JsonRecordFieldKind.UnionArray, UnionSchema: union)]);
        Assert.Throws<ArgumentException>(() => JsonUnionArray.Create(union, [null!]));
        Assert.Throws<ArgumentException>(() => JsonUnionArray.Create(union, [JsonTaggedUnion.Create(foreign, 0)]));
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(schema, [JsonUnionArray.Create(foreign, [])], [true]));
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("events", JsonRecordFieldKind.UnionArray)]));
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("events", JsonRecordFieldKind.UnionArray, Optional: true, UnionSchema: union)]));
        var nullItem = JsonScalarRecordCodec.Decode(schema, Utf8("{\"events\":[null]}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"events\"][0]"), nullItem.Error);
        var missing = JsonScalarRecordCodec.Decode(schema, Utf8("{}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.MissingField, "$[\"events\"]"), missing.Error);
    }

    [Fact]
    public void NestedFailuresIncludeIndexAndDeclaredPathsWithoutSecrets()
    {
        var payload = new JsonScalarRecordSchema([new("amount", JsonRecordFieldKind.Integer)]);
        var union = new JsonTaggedUnionSchema([new("Idle"), new("Data", payload)]);
        var inner = new JsonScalarRecordSchema([new("events", JsonRecordFieldKind.UnionArray, UnionSchema: union)]);
        var schema = new JsonScalarRecordSchema([new("entry", JsonRecordFieldKind.Record, NestedSchema: inner)]);
        var result = JsonScalarRecordCodec.Decode(schema, Utf8("{\"entry\":{\"events\":[{\"case\":\"Idle\"},{\"case\":\"Data\",\"value\":{\"amount\":\"secret\"}}]}}"));
        Assert.False(result.IsSuccess);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"entry\"][\"events\"][1][\"value\"][\"amount\"]"), result.Error);
        Assert.DoesNotContain("secret", result.Error!.Value.ToString(), StringComparison.Ordinal);
        var unknown = JsonScalarRecordCodec.Decode(schema, Utf8("{\"entry\":{\"events\":[{\"case\":\"secret-tag\"}]}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.UnknownCase, "$[\"entry\"][\"events\"][0][\"case\"]"), unknown.Error);
        Assert.DoesNotContain("secret-tag", unknown.Error!.Value.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void UnionArrayAndPayloadRespectDepthLimit()
    {
        var payload = new JsonScalarRecordSchema([]);
        var record = JsonScalarRecord.Create(payload, [], []);
        for (var depth = 2; depth <= 61; depth++)
        {
            payload = new JsonScalarRecordSchema([new("child", JsonRecordFieldKind.Record, NestedSchema: payload)]);
            record = JsonScalarRecord.Create(payload, [record], [true]);
        }
        var union = new JsonTaggedUnionSchema([new("Deep", payload)]);
        var schema = new JsonScalarRecordSchema([new("events", JsonRecordFieldKind.UnionArray, UnionSchema: union)]);
        var snapshot = JsonScalarRecord.Create(schema, [JsonUnionArray.Create(union, [JsonTaggedUnion.Create(union, 0, record)])], [true]);
        Assert.Equal(64, schema.Depth);
        Assert.True(JsonScalarRecordCodec.Decode(schema, JsonScalarRecordCodec.Encode(snapshot)).IsSuccess);
        var tooDeep = new JsonScalarRecordSchema([new("child", JsonRecordFieldKind.Record, NestedSchema: payload)]);
        var deeperUnion = new JsonTaggedUnionSchema([new("Deep", tooDeep)]);
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("events", JsonRecordFieldKind.UnionArray, UnionSchema: deeperUnion)]));
    }
}
