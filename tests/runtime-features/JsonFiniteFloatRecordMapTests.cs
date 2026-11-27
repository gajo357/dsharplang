using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonFiniteFloatRecordMapTests
{
    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);
    private static KeyValuePair<string, object> Entry(string key, object? value) => new(key, value!);

    [Fact]
    public void TypedMapFieldsRetainBitsNullsAndSnapshots()
    {
        var schema = new JsonScalarRecordSchema([
            new("single", JsonRecordFieldKind.SingleMap), new("double", JsonRecordFieldKind.DoubleNullableValuesMap),
            new("maybeSingle", JsonRecordFieldKind.NullableSingleNullableValuesMap, Optional: true),
            new("maybeDouble", JsonRecordFieldKind.NullableDoubleMap, Optional: true)]);
        KeyValuePair<string, object>[] singles = [Entry("zero", -0f), Entry("min", float.Epsilon), Entry("max", float.MaxValue)];
        KeyValuePair<string, object>[] doubles = [Entry("none", null), Entry("zero", -0d), Entry("max", double.MaxValue)];
        var record = JsonScalarRecord.Create(schema, [JsonRecordScalarMap.Create(JsonRecordFieldKind.Single, singles), JsonRecordScalarMap.Create(JsonRecordFieldKind.NullableDouble, doubles), null, null], [true, true, false, true]);
        singles[0] = Entry("changed", 1f); doubles[1] = Entry("changed", 1d);
        var decoded = JsonScalarRecordCodec.Decode(schema, JsonScalarRecordCodec.Encode(record));
        Assert.True(decoded.IsSuccess);
        var a = (JsonRecordScalarMap)decoded.Value.ValueAt(0)!; var b = (JsonRecordScalarMap)decoded.Value.ValueAt(1)!;
        Assert.Equal("zero", a.At(0).Key); Assert.Equal(int.MinValue, BitConverter.SingleToInt32Bits((float)a.At(0).Value));
        Assert.Equal(float.Epsilon, a.At(1).Value); Assert.Equal(float.MaxValue, a.At(2).Value);
        Assert.Null(b.At(0).Value); Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits((double)b.At(1).Value)); Assert.Equal(double.MaxValue, b.At(2).Value);
        Assert.False(decoded.Value.IsPresent(2)); Assert.True(decoded.Value.IsPresent(3)); Assert.Null(decoded.Value.ValueAt(3));
        var explicitNull = JsonScalarRecordCodec.Decode(schema, Bytes("{\"single\":{},\"double\":{\"x\":-1e-9999},\"maybeSingle\":null}"));
        Assert.True(explicitNull.IsSuccess); Assert.True(explicitNull.Value.IsPresent(2)); Assert.Null(explicitNull.Value.ValueAt(2)); Assert.False(explicitNull.Value.IsPresent(3));
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits((double)((JsonRecordScalarMap)explicitNull.Value.ValueAt(1)!).At(0).Value));
    }

    [Fact]
    public void AllFieldVariantsRoundTripMatchingKinds()
    {
        var cases = new (JsonRecordFieldKind Field, JsonRecordFieldKind Element, object? Value)[] {
            (JsonRecordFieldKind.SingleMap, JsonRecordFieldKind.Single, -0f),
            (JsonRecordFieldKind.NullableSingleMap, JsonRecordFieldKind.Single, float.Epsilon),
            (JsonRecordFieldKind.SingleNullableValuesMap, JsonRecordFieldKind.NullableSingle, null),
            (JsonRecordFieldKind.NullableSingleNullableValuesMap, JsonRecordFieldKind.NullableSingle, float.MaxValue),
            (JsonRecordFieldKind.DoubleMap, JsonRecordFieldKind.Double, -0d),
            (JsonRecordFieldKind.NullableDoubleMap, JsonRecordFieldKind.Double, double.Epsilon),
            (JsonRecordFieldKind.DoubleNullableValuesMap, JsonRecordFieldKind.NullableDouble, null),
            (JsonRecordFieldKind.NullableDoubleNullableValuesMap, JsonRecordFieldKind.NullableDouble, double.MaxValue) };
        foreach (var (field, element, value) in cases)
        {
            var schema = new JsonScalarRecordSchema([new("v", field)]);
            var map = JsonRecordScalarMap.Create(element, [Entry("key", value)]);
            var result = JsonScalarRecordCodec.Decode(schema, JsonScalarRecordCodec.Encode(JsonScalarRecord.Create(schema, [map], [true])));
            Assert.True(result.IsSuccess); var output = (JsonRecordScalarMap)result.Value.ValueAt(0)!;
            Assert.Equal(element, output.Kind); Assert.Equal(value, output.At(0).Value);
        }
    }

    [Fact]
    public void NonfiniteInputsInvalidKeysAndDecodedValuesAreRejected()
    {
        foreach (var value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            Assert.Throws<ArgumentException>(() => JsonRecordScalarMap.Create(JsonRecordFieldKind.Single, [Entry("v", value)]));
            Assert.Throws<ArgumentException>(() => JsonRecordScalarMap.Create(JsonRecordFieldKind.NullableSingle, [Entry("v", value)]));
        }
        foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentException>(() => JsonRecordScalarMap.Create(JsonRecordFieldKind.Double, [Entry("v", value)]));
            Assert.Throws<ArgumentException>(() => JsonRecordScalarMap.Create(JsonRecordFieldKind.NullableDouble, [Entry("v", value)]));
        }
        Assert.Throws<ArgumentException>(() => JsonRecordScalarMap.Create(JsonRecordFieldKind.Single, [Entry("a", 1f), Entry("a", 2f)]));
        Assert.Throws<ArgumentException>(() => JsonRecordScalarMap.Create(JsonRecordFieldKind.Single, [Entry("\ud800", 1f)]));
        var schema = new JsonScalarRecordSchema([new("v", JsonRecordFieldKind.SingleNullableValuesMap)]);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidFloat, "$[\"v\"][1]"), JsonScalarRecordCodec.Decode(schema, Bytes("{\"v\":{\"ok\":null,\"secret-key\":1e9999,\"later\":\"secret-value\"}}")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"v\"][1]"), JsonScalarRecordCodec.Decode(schema, Bytes("{\"v\":{\"ok\":null,\"secret-key\":\"secret-value\",\"later\":1e9999}}")).Error);
        var required = new JsonScalarRecordSchema([new("v", JsonRecordFieldKind.DoubleMap)]);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"v\"][0]"), JsonScalarRecordCodec.Decode(required, Bytes("{\"v\":{\"secret-key\":null}}")).Error);
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(required, [JsonRecordScalarMap.Create(JsonRecordFieldKind.Single, [Entry("x", 1f)])], [true]));
    }

    [Fact]
    public void TreeBridgeAndNestedUnionPreserveValuesAndRedactedErrors()
    {
        var map = JsonRecordScalarMap.Create(JsonRecordFieldKind.NullableSingle, [Entry("n", null), Entry("z", -0f), Entry("m", float.MaxValue)]);
        var tree = JsonTypedFieldMapping.FromScalarMap(map).Value;
        var recovered = JsonTypedFieldMapping.ToScalarMap(JsonRecordFieldKind.NullableSingle, tree);
        Assert.True(recovered.IsSuccess); Assert.Null(recovered.Value.At(0).Value);
        Assert.Equal(int.MinValue, BitConverter.SingleToInt32Bits((float)recovered.Value.At(1).Value));
        var bad = JsonFieldValueCodec.Decode(Bytes("{\"a\":1,\"secret-key\":1e9999}")).Value;
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidFloat, "$[1]"), JsonTypedFieldMapping.ToScalarMap(JsonRecordFieldKind.Double, bad).Error);
        var schema = new JsonScalarRecordSchema([new("v", JsonRecordFieldKind.SingleNullableValuesMap)]);
        var unionSchema = new JsonTaggedUnionSchema([new("Metrics", schema)]);
        var union = JsonTaggedUnion.Create(unionSchema, 0, JsonScalarRecord.Create(schema, [map], [true]));
        var decoded = JsonTaggedUnionCodec.Decode(unionSchema, JsonTaggedUnionCodec.Encode(union));
        Assert.True(decoded.IsSuccess); Assert.Equal(float.MaxValue, ((JsonRecordScalarMap)decoded.Value.Payload!.ValueAt(0)!).At(2).Value);
    }
}
