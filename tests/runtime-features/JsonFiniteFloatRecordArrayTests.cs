using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonFiniteFloatRecordArrayTests
{
    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    [Fact]
    public void TypedArrayFieldsRoundTripBitsNullsAndOwnedSnapshots()
    {
        var schema = new JsonScalarRecordSchema([
            new("single", JsonRecordFieldKind.SingleArray), new("double", JsonRecordFieldKind.DoubleNullableElementsArray),
            new("optionalSingle", JsonRecordFieldKind.NullableSingleNullableElementsArray, Optional: true),
            new("optionalDouble", JsonRecordFieldKind.NullableDoubleArray, Optional: true)]);
        float[] singles = [-0f, float.Epsilon, float.MaxValue];
        double?[] doubles = [null, -0d, double.MaxValue];
        var record = JsonScalarRecord.Create(schema, [JsonRecordScalarArray.Create(singles), JsonRecordScalarArray.Create(doubles), null, null], [true, true, false, true]);
        singles[0] = 1f; doubles[1] = 1d;
        var decoded = JsonScalarRecordCodec.Decode(schema, JsonScalarRecordCodec.Encode(record));
        Assert.True(decoded.IsSuccess);
        var a = (JsonRecordScalarArray)decoded.Value.ValueAt(0)!;
        var b = (JsonRecordScalarArray)decoded.Value.ValueAt(1)!;
        Assert.Equal(int.MinValue, BitConverter.SingleToInt32Bits((float)a.At(0)!));
        Assert.Equal(float.Epsilon, a.At(1)); Assert.Equal(float.MaxValue, a.At(2));
        Assert.Null(b.At(0)); Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits((double)b.At(1)!)); Assert.Equal(double.MaxValue, b.At(2));
        Assert.False(decoded.Value.IsPresent(2)); Assert.True(decoded.Value.IsPresent(3)); Assert.Null(decoded.Value.ValueAt(3));
        var explicitNull = JsonScalarRecordCodec.Decode(schema, Bytes("{\"single\":[],\"double\":[null,-1e-9999],\"optionalSingle\":null}"));
        Assert.True(explicitNull.IsSuccess); Assert.True(explicitNull.Value.IsPresent(2)); Assert.Null(explicitNull.Value.ValueAt(2));
        Assert.False(explicitNull.Value.IsPresent(3));
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits((double)((JsonRecordScalarArray)explicitNull.Value.ValueAt(1)!).At(1)!));
    }

    [Fact]
    public void EveryFieldVariantAcceptsItsMatchingArrayKind()
    {
        var cases = new (JsonRecordFieldKind Field, JsonRecordScalarArray Value)[] {
            (JsonRecordFieldKind.SingleArray, JsonRecordScalarArray.Create(Array.Empty<float>())),
            (JsonRecordFieldKind.NullableSingleArray, JsonRecordScalarArray.Create(new float[] { -0f })),
            (JsonRecordFieldKind.SingleNullableElementsArray, JsonRecordScalarArray.Create(new float?[] { null, float.Epsilon })),
            (JsonRecordFieldKind.NullableSingleNullableElementsArray, JsonRecordScalarArray.Create(new float?[] { null })),
            (JsonRecordFieldKind.DoubleArray, JsonRecordScalarArray.Create(Array.Empty<double>())),
            (JsonRecordFieldKind.NullableDoubleArray, JsonRecordScalarArray.Create(new double[] { -0d })),
            (JsonRecordFieldKind.DoubleNullableElementsArray, JsonRecordScalarArray.Create(new double?[] { null, double.Epsilon })),
            (JsonRecordFieldKind.NullableDoubleNullableElementsArray, JsonRecordScalarArray.Create(new double?[] { null })) };
        foreach (var (field, value) in cases)
        {
            var schema = new JsonScalarRecordSchema([new("v", field)]);
            var decoded = JsonScalarRecordCodec.Decode(schema, JsonScalarRecordCodec.Encode(JsonScalarRecord.Create(schema, [value], [true])));
            Assert.True(decoded.IsSuccess); Assert.Equal(value.Kind, ((JsonRecordScalarArray)decoded.Value.ValueAt(0)!).Kind);
            Assert.Equal(value.Count, ((JsonRecordScalarArray)decoded.Value.ValueAt(0)!).Count);
        }
    }

    [Fact]
    public void NonfiniteInputsAndInvalidElementsAreRejected()
    {
        foreach (var value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            Assert.Throws<ArgumentException>(() => JsonRecordScalarArray.Create(new[] { 1f, value }));
            Assert.Throws<ArgumentException>(() => JsonRecordScalarArray.Create(new float?[] { null, value }));
        }
        foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentException>(() => JsonRecordScalarArray.Create(new[] { 1d, value }));
            Assert.Throws<ArgumentException>(() => JsonRecordScalarArray.Create(new double?[] { null, value }));
        }
        var schema = new JsonScalarRecordSchema([new("v", JsonRecordFieldKind.SingleNullableElementsArray)]);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidFloat, "$[\"v\"][1]"), JsonScalarRecordCodec.Decode(schema, Bytes("{\"v\":[null,1e9999,\"secret\"]}")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"v\"][1]"), JsonScalarRecordCodec.Decode(schema, Bytes("{\"v\":[null,\"secret\",1e9999]}")).Error);
        var required = new JsonScalarRecordSchema([new("v", JsonRecordFieldKind.DoubleArray)]);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"v\"][0]"), JsonScalarRecordCodec.Decode(required, Bytes("{\"v\":[null]}")).Error);
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(required, [JsonRecordScalarArray.Create(new float[] { 1f })], [true]));
    }

    [Fact]
    public void TreeBridgeAndNestedUnionKeepFiniteValuesAndErrors()
    {
        var scalar = JsonRecordScalarArray.Create(new float?[] { null, -0f, float.MaxValue });
        var tree = JsonTypedFieldMapping.FromScalarArray(scalar).Value;
        var recovered = JsonTypedFieldMapping.ToScalarArray(JsonRecordScalarArrayKind.NullableSingle, tree);
        Assert.True(recovered.IsSuccess); Assert.Null(recovered.Value.At(0));
        Assert.Equal(int.MinValue, BitConverter.SingleToInt32Bits((float)recovered.Value.At(1)!));
        var bad = JsonFieldValueCodec.Decode(Bytes("[1,1e9999,\"secret\"]")).Value;
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidFloat, "$[1]"), JsonTypedFieldMapping.ToScalarArray(JsonRecordScalarArrayKind.Double, bad).Error);
        var schema = new JsonScalarRecordSchema([new("v", JsonRecordFieldKind.SingleNullableElementsArray)]);
        var unionSchema = new JsonTaggedUnionSchema([new("Metrics", schema)]);
        var union = JsonTaggedUnion.Create(unionSchema, 0, JsonScalarRecord.Create(schema, [scalar], [true]));
        var decoded = JsonTaggedUnionCodec.Decode(unionSchema, JsonTaggedUnionCodec.Encode(union));
        Assert.True(decoded.IsSuccess);
        Assert.Equal(float.MaxValue, ((JsonRecordScalarArray)decoded.Value.Payload!.ValueAt(0)!).At(2));
    }
}
