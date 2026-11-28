using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonIntegerArrayMapTests
{
    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);

    [Fact]
    public void NestedMapRoundTripsOwnedValuesAndRejectsInvalidInputs()
    {
        var schema = new JsonScalarRecordSchema([new("m", JsonRecordFieldKind.IntegerArrayMap),
            new("optional", JsonRecordFieldKind.NullableIntegerArrayMap, Optional: true)]);
        int[] input = [1, -2];
        var first = JsonRecordScalarArray.Create(input);
        KeyValuePair<string, JsonRecordScalarArray>[] entries = [new("a", first), new("empty", JsonRecordScalarArray.Create(Array.Empty<int>()))];
        var map = JsonIntegerArrayMap.Create(entries);
        input[0] = 99; entries[0] = new("changed", first);
        var record = JsonScalarRecord.Create(schema, [map, null], [true, false]);
        Assert.Equal(3, schema.Depth);
        Assert.Equal("{\"m\":{\"a\":[1,-2],\"empty\":[]}}", Encoding.UTF8.GetString(JsonScalarRecordCodec.Encode(record)));
        var decoded = JsonScalarRecordCodec.Decode(schema, JsonScalarRecordCodec.Encode(record));
        Assert.True(decoded.IsSuccess); Assert.False(decoded.Value.IsPresent(1));
        var owned = (JsonIntegerArrayMap)decoded.Value.ValueAt(0)!;
        Assert.Equal("a", owned.At(0).Key); Assert.Equal(1, owned.At(0).Value.At(0)); Assert.Equal(0, owned.At(1).Value.Count);
        var present = JsonScalarRecordCodec.Decode(schema, Bytes("{\"m\":{},\"optional\":null}"));
        Assert.True(present.IsSuccess); Assert.True(present.Value.IsPresent(1)); Assert.Null(present.Value.ValueAt(1));
        var optionalValue = JsonScalarRecordCodec.Decode(schema, Bytes("{\"m\":{},\"optional\":{\"k\":[4]}}"));
        Assert.True(optionalValue.IsSuccess); Assert.Equal(4, ((JsonIntegerArrayMap)optionalValue.Value.ValueAt(1)!).At(0).Value.At(0));
        var tree = JsonTypedFieldMapping.FromRecord(record);
        Assert.True(tree.IsSuccess); Assert.True(JsonTypedFieldMapping.ToRecord(schema, tree.Value).IsSuccess);
        var unionSchema = new JsonTaggedUnionSchema([new("Case", schema)]);
        var union = JsonTaggedUnion.Create(unionSchema, 0, record);
        Assert.True(JsonTaggedUnionCodec.Decode(unionSchema, JsonTaggedUnionCodec.Encode(union)).IsSuccess);
        Assert.Throws<ArgumentException>(() => JsonIntegerArrayMap.Create([new("x", first), new("x", first)]));
        Assert.Throws<ArgumentException>(() => JsonIntegerArrayMap.Create([new("\uD800", first)]));
        Assert.Throws<ArgumentException>(() => JsonIntegerArrayMap.Create([new("x", JsonRecordScalarArray.Create(new[] { true }))]));
        Assert.Throws<ArgumentException>(() => JsonIntegerArrayMap.Create([new("x", null!)]));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.MissingField, "$[\"m\"]"), JsonScalarRecordCodec.Decode(schema, Bytes("{}")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"m\"]"), JsonScalarRecordCodec.Decode(schema, Bytes("{\"m\":null}")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.DuplicateField, "$[\"m\"][1]"), JsonScalarRecordCodec.Decode(schema, Bytes("{\"m\":{\"secret\":[],\"secret\":[]}}")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"m\"][0]"), JsonScalarRecordCodec.Decode(schema, Bytes("{\"m\":{\"secret\":null}}")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidUnicode, "$[\"m\"][0]"), JsonScalarRecordCodec.Decode(schema, Bytes("{\"m\":{\"\\uD800\":[]}}")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidInteger, "$[\"m\"][0][0]"), JsonScalarRecordCodec.Decode(schema, Bytes("{\"m\":{\"x\":[2147483648]}}")).Error);
        var failure = JsonScalarRecordCodec.Decode(schema, Bytes("{\"m\":{\"secret\":[1,\"secret-value\"]}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"m\"][0][1]"), failure.Error);
        Assert.DoesNotContain("secret", failure.Error.ToString());
    }
}
