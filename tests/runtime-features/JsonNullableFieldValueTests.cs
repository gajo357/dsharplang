using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonNullableFieldValueTests
{
    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
    private static string Encode(JsonScalarRecord record) => Encoding.UTF8.GetString(JsonScalarRecordCodec.Encode(record));
    private static JsonFieldValue Nested(int depth)
    {
        var value = JsonFieldValue.Boolean(true);
        for (var i = 0; i < depth; i++) value = JsonFieldValue.List([value]);
        return value;
    }

    [Fact]
    public void MissingExplicitNullAndValuesRemainDistinct()
    {
        var schema = new JsonScalarRecordSchema([new("data", JsonRecordFieldKind.NullableFieldValue, Optional: true)]);
        foreach (var (json, present, isNull) in new[] {
            ("{}", false, true), ("{\"data\":null}", true, true),
            ("{\"data\":[]}", true, false), ("{\"data\":{}}", true, false),
            ("{\"data\":12345678901234567890.000e+8}", true, false) })
        {
            var decoded = JsonScalarRecordCodec.Decode(schema, Utf8(json));
            Assert.True(decoded.IsSuccess);
            Assert.Equal(present, decoded.Value.IsPresent(0));
            Assert.Equal(isNull, decoded.Value.ValueAt(0) is null);
            Assert.Equal(json, Encode(decoded.Value));
        }
    }

    [Fact]
    public void RequiredNullableFieldMustBePresent()
    {
        var schema = new JsonScalarRecordSchema([new("data", JsonRecordFieldKind.NullableFieldValue)]);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.MissingField, "$[\"data\"]"), JsonScalarRecordCodec.Decode(schema, Utf8("{}")).Error);
        Assert.True(JsonScalarRecordCodec.Decode(schema, Utf8("{\"data\":null}")).IsSuccess);
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(schema, [null], [false]));
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(schema, [new object()], [true]));
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(schema, [JsonFieldValue.Null()], [true]));
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(schema, [JsonFieldValue.Boolean(true)], [false]));
    }

    [Fact]
    public void ConstructionOwnsPresenceAndTreeInputs()
    {
        var schema = new JsonScalarRecordSchema([new("data", JsonRecordFieldKind.NullableFieldValue, Optional: true)]);
        var items = new[] { JsonFieldValue.String("original") };
        var values = new object?[] { JsonFieldValue.List(items) };
        var presence = new[] { true };
        var record = JsonScalarRecord.Create(schema, values, presence);
        items[0] = JsonFieldValue.Boolean(false); values[0] = null; presence[0] = false;
        Assert.Equal("{\"data\":[\"original\"]}", Encode(record));
        Assert.True(JsonScalarRecordCodec.Decode(schema, Utf8(Encode(record))).IsSuccess);
    }

    [Fact]
    public void NestedErrorsAreIndexedAndRedacted()
    {
        var payload = new JsonScalarRecordSchema([new("data", JsonRecordFieldKind.NullableFieldValue, Optional: true)]);
        var cases = new JsonTaggedUnionSchema([new("Data", payload)]);
        var schema = new JsonScalarRecordSchema([new("event", JsonRecordFieldKind.Union, UnionSchema: cases)]);
        var bad = JsonScalarRecordCodec.Decode(schema, Utf8("{\"event\":{\"case\":\"Data\",\"value\":{\"data\":{\"private\":[{\"secret\":1,\"secret\":2}]}}}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.DuplicateField, "$[\"event\"][\"value\"][\"data\"][0][0][1]"), bad.Error);
        Assert.DoesNotContain("private", bad.Error!.Value.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("secret", bad.Error.Value.ToString(), StringComparison.Ordinal);
        var invalid = JsonScalarRecordCodec.Decode(schema, Utf8("{\"event\":{\"case\":\"Data\",\"value\":{\"data\":[\"\\uD800\"]}}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidUnicode, "$[\"event\"][\"value\"][\"data\"][0]"), invalid.Error);
    }

    [Fact]
    public void MixedUnionDepthRemainsBounded()
    {
        var payload = new JsonScalarRecordSchema([new("data", JsonRecordFieldKind.NullableFieldValue, Optional: true)]);
        var cases = new JsonTaggedUnionSchema([new("Data", payload)]);
        var outer = new JsonScalarRecordSchema([new("event", JsonRecordFieldKind.Union, UnionSchema: cases)]);
        JsonScalarRecord Make(int depth) => JsonScalarRecord.Create(payload, [Nested(depth)], [true]);
        var value = JsonScalarRecord.Create(outer, [JsonTaggedUnion.Create(cases, 0, Make(61))], [true]);
        Assert.Equal(64, value.Depth);
        Assert.True(JsonScalarRecordCodec.Decode(outer, JsonScalarRecordCodec.Encode(value)).IsSuccess);
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(outer, [JsonTaggedUnion.Create(cases, 0, Make(62))], [true]));
    }
}
