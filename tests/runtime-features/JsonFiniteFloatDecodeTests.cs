using System.Globalization;
using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonFiniteFloatDecodeTests
{
    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);
    private static JsonFieldValue Number(string text) => JsonFieldValue.Number(JsonExactNumberCodec.Decode(Bytes(text)).Value);

    [Fact]
    public void FiniteBitsRoundTripAcrossEncodedJsonTrees()
    {
        var random = new Random(78366);
        var bytes = new byte[8];
        for (var i = 0; i < 4096; i++)
        {
            random.NextBytes(bytes);
            var single = BitConverter.Int32BitsToSingle(BitConverter.ToInt32(bytes));
            var binary64 = BitConverter.Int64BitsToDouble(BitConverter.ToInt64(bytes));
            if (float.IsFinite(single)) CheckSingle(single);
            if (double.IsFinite(binary64)) CheckDouble(binary64);
        }
        foreach (var value in new[] { 0f, -0f, float.Epsilon, -float.Epsilon, float.MaxValue, -float.MaxValue }) CheckSingle(value);
        foreach (var value in new[] { 0d, -0d, double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue }) CheckDouble(value);
    }

    private static void CheckSingle(float value)
    {
        var encoded = JsonFieldValueCodec.Encode(JsonFiniteFloatMapping.FromSingle(value).Value);
        var result = JsonFiniteFloatMapping.ToSingle(JsonFieldValueCodec.Decode(encoded).Value);
        Assert.True(result.IsSuccess);
        Assert.Equal(BitConverter.SingleToInt32Bits(value), BitConverter.SingleToInt32Bits(result.Value));
    }

    private static void CheckDouble(double value)
    {
        var encoded = JsonFieldValueCodec.Encode(JsonFiniteFloatMapping.FromDouble(value).Value);
        var result = JsonFiniteFloatMapping.ToDouble(JsonFieldValueCodec.Decode(encoded).Value);
        Assert.True(result.IsSuccess);
        Assert.Equal(BitConverter.DoubleToInt64Bits(value), BitConverter.DoubleToInt64Bits(result.Value));
    }

    [Fact]
    public void RoundingAndUnderflowUseInvariantNearestFiniteValues()
    {
        var prior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal(1f, JsonFiniteFloatMapping.ToSingle(Number("1.000000059604644775390625")).Value);
            Assert.Equal(BitConverter.Int32BitsToSingle(0x3f800002), JsonFiniteFloatMapping.ToSingle(Number("1.000000178813934326171875")).Value);
            Assert.Equal(1d, JsonFiniteFloatMapping.ToDouble(Number("1.00000000000000011102230246251565404236316680908203125")).Value);
            Assert.Equal(int.MinValue, BitConverter.SingleToInt32Bits(JsonFiniteFloatMapping.ToSingle(Number("-1e-9999")).Value));
            Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(JsonFiniteFloatMapping.ToDouble(Number("-1e-9999")).Value));
            Assert.Equal(1.5f, JsonFiniteFloatMapping.ToSingle(Number("1.5")).Value);
        }
        finally { CultureInfo.CurrentCulture = prior; }
    }

    [Fact]
    public void WrongKindsAndOverflowReturnRedactedErrors()
    {
        foreach (var value in new[] { JsonFieldValue.Null(), JsonFieldValue.String("secret-value"), JsonFieldValue.Boolean(true), JsonFieldValue.List([]) })
        {
            Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$"), JsonFiniteFloatMapping.ToSingle(value).Error);
            Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$"), JsonFiniteFloatMapping.ToDouble(value).Error);
        }
        foreach (var token in new[] { "1e9999", "-1e9999" })
        {
            Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidFloat, "$"), JsonFiniteFloatMapping.ToSingle(Number(token)).Error);
            Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidFloat, "$"), JsonFiniteFloatMapping.ToDouble(Number(token)).Error);
        }
        Assert.Throws<ArgumentNullException>(() => JsonFiniteFloatMapping.ToSingle(null!));
        Assert.Throws<ArgumentNullException>(() => JsonFiniteFloatMapping.ToDouble(null!));
    }

    [Fact]
    public void NestedTypedPayloadHandoffRetainsFloatBitsAndExactNeighbor()
    {
        var tree = JsonFieldValue.List([JsonFiniteFloatMapping.FromSingle(-0f).Value,
            JsonFiniteFloatMapping.FromDouble(double.MaxValue).Value, Number("9007199254740993.12500")]);
        var fields = new JsonScalarRecordSchema([new("values", JsonRecordFieldKind.FieldValue)]);
        var schema = new JsonTaggedUnionSchema([new("Metrics", fields)]);
        var payload = JsonValidatedUnionPayload.FromValue(JsonTaggedUnion.Create(schema, 0, JsonScalarRecord.Create(fields, [tree], [true])));
        var bytes = payload.Bytes;
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        var request = new TargetedRequest("app", "submission", TargetedOperation.Invoke, "actor", bytes, Bytes("test-schema-v1"), [1]);
        Assert.Equal(TargetedAcceptKind.Created, store.Accept(request, Guid.NewGuid(), "selected").Kind);
        Array.Fill(bytes, (byte)'x');
        var recovered = JsonValidatedUnionPayload.Decode(schema, store.Lookup("app", "submission")!.Request.Payload);
        Assert.True(recovered.IsSuccess);
        var items = ((JsonFieldValue)recovered.Value.Value.Payload!.ValueAt(0)!).ListItems;
        Assert.Equal(int.MinValue, BitConverter.SingleToInt32Bits(JsonFiniteFloatMapping.ToSingle(items[0]).Value));
        Assert.Equal(BitConverter.DoubleToInt64Bits(double.MaxValue), BitConverter.DoubleToInt64Bits(JsonFiniteFloatMapping.ToDouble(items[1]).Value));
        Assert.Equal("9007199254740993.12500", Encoding.UTF8.GetString(JsonFieldValueCodec.Encode(items[2])));
        Assert.False(store.IsDurable);
    }
}
