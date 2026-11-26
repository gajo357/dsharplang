using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonScalarMapTests
{
    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);
    private static string Encode(JsonScalarRecord value) => Encoding.UTF8.GetString(JsonScalarRecordCodec.Encode(value));
    private static JsonScalarRecordSchema Schema() => new([
        new("flags", JsonRecordFieldKind.BooleanMap), new("counts", JsonRecordFieldKind.IntegerMap),
        new("names", JsonRecordFieldKind.StringMap), new("prices", JsonRecordFieldKind.ExactNumberMap)]);

    [Fact]
    public void ScalarMapsRoundTripInUnionAndTreeWithoutLosingNumericSpelling()
    {
        var payload = Schema();
        var union = new JsonTaggedUnionSchema([new("Data", payload)]);
        var schema = new JsonScalarRecordSchema([new("event", JsonRecordFieldKind.Union, UnionSchema: union)]);
        const string json = "{\"event\":{\"case\":\"Data\",\"value\":{\"flags\":{\"ok\":true},\"counts\":{\"a\":-42},\"names\":{\"x\":\"snow \\u2603\"},\"prices\":{\"p\":12345678901234567890.00e+8,\"z\":-0.0}}}}";
        var decoded = JsonScalarRecordCodec.Decode(schema, Bytes(json));
        Assert.True(decoded.IsSuccess);
        Assert.Equal(json, Encode(decoded.Value));
        var tree = JsonTypedFieldMapping.FromRecord(decoded.Value);
        Assert.True(tree.IsSuccess);
        Assert.Equal(json, Encode(JsonTypedFieldMapping.ToRecord(schema, tree.Value).Value));
    }

    [Fact]
    public void NullableMapsDistinguishMissingNullAndEmpty()
    {
        var schema = new JsonScalarRecordSchema([new("items", JsonRecordFieldKind.NullableIntegerMap, Optional: true)]);
        foreach (var (json, present, isNull) in new[] { ("{}", false, true), ("{\"items\":null}", true, true), ("{\"items\":{}}", true, false) })
        {
            var result = JsonScalarRecordCodec.Decode(schema, Bytes(json));
            Assert.True(result.IsSuccess);
            Assert.Equal(present, result.Value.IsPresent(0));
            Assert.Equal(isNull, result.Value.ValueAt(0) is null);
            Assert.Equal(json, Encode(result.Value));
        }
        var required = new JsonScalarRecordSchema([new("items", JsonRecordFieldKind.IntegerMap)]);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.MissingField, "$[\"items\"]"), JsonScalarRecordCodec.Decode(required, Bytes("{}")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"items\"]"), JsonScalarRecordCodec.Decode(required, Bytes("{\"items\":null}")).Error);
    }

    [Fact]
    public void ConstructionCopiesAndChecksKindUnicodeAndDuplicates()
    {
        var entries = new[] { new KeyValuePair<string, object>("key", "value") };
        var map = JsonRecordScalarMap.Create(JsonRecordFieldKind.String, entries);
        entries[0] = new("changed", "changed");
        var schema = new JsonScalarRecordSchema([new("items", JsonRecordFieldKind.StringMap)]);
        Assert.Equal("{\"items\":{\"key\":\"value\"}}", Encode(JsonScalarRecord.Create(schema, [map], [true])));
        Assert.Throws<ArgumentException>(() => JsonRecordScalarMap.Create(JsonRecordFieldKind.String, [new("x", null!)]));
        Assert.Throws<ArgumentException>(() => JsonRecordScalarMap.Create(JsonRecordFieldKind.Integer, [new("x", true)]));
        Assert.Throws<ArgumentException>(() => JsonRecordScalarMap.Create(JsonRecordFieldKind.String, [new(null!, "v")]));
        Assert.Throws<ArgumentException>(() => JsonRecordScalarMap.Create(JsonRecordFieldKind.String, [new("x", "a"), new("x", "b")]));
        Assert.Throws<ArgumentException>(() => JsonRecordScalarMap.Create(JsonRecordFieldKind.String, [new("\ud800", "v")]));
        Assert.Throws<ArgumentException>(() => JsonRecordScalarMap.Create(JsonRecordFieldKind.String, [new("x", "\ud800")]));
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(schema, [JsonRecordScalarMap.Create(JsonRecordFieldKind.Integer, [])], [true]));
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("items", JsonRecordFieldKind.StringMap, Optional: true)]));
    }

    [Fact]
    public void DecodeRejectsInvalidValuesKeysAndDuplicatesWithoutDisclosingThem()
    {
        var schema = new JsonScalarRecordSchema([new("items", JsonRecordFieldKind.IntegerMap)]);
        var bad = JsonScalarRecordCodec.Decode(schema, Bytes("{\"items\":{\"private\":\"secret\"}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"items\"][0]"), bad.Error);
        var duplicate = JsonScalarRecordCodec.Decode(schema, Bytes("{\"items\":{\"private\":1,\"\\u0070rivate\":2}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.DuplicateField, "$[\"items\"][1]"), duplicate.Error);
        var unicode = JsonScalarRecordCodec.Decode(schema, Bytes("{\"items\":{\"\\uD800\":1}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidUnicode, "$[\"items\"][0]"), unicode.Error);
        var nullValue = JsonScalarRecordCodec.Decode(schema, Bytes("{\"items\":{\"private\":null}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"items\"][0]"), nullValue.Error);
        var overflow = JsonScalarRecordCodec.Decode(schema, Bytes("{\"items\":{\"private\":2147483648}}"));
        Assert.False(overflow.IsSuccess);
        foreach (var error in new[] { bad.Error, duplicate.Error, unicode.Error, nullValue.Error, overflow.Error })
        {
            Assert.DoesNotContain("private", error!.Value.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("secret", error.Value.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ScalarMapContributesToCombinedDepthLimit()
    {
        var schema = new JsonScalarRecordSchema([new("items", JsonRecordFieldKind.BooleanMap)]);
        var record = JsonScalarRecord.Create(schema, [JsonRecordScalarMap.Create(JsonRecordFieldKind.Boolean, [new("x", true)])], [true]);
        Assert.Equal(2, record.Depth);
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
