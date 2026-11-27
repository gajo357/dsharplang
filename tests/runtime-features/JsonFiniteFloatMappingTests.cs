using System.Text;
using System.Text.Json;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonFiniteFloatMappingTests
{
    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);
    private static JsonExactNumber Number(string text) => JsonExactNumberCodec.Decode(Bytes(text)).Value;

    private static void CheckSingle(float value)
    {
        var result = JsonFiniteFloatMapping.FromSingle(value);
        Assert.True(result.IsSuccess);
        Assert.Equal(JsonFieldValueKind.Number, result.Value.Kind);
        using var json = JsonDocument.Parse(JsonFieldValueCodec.Encode(result.Value));
        Assert.True(json.RootElement.TryGetSingle(out var decoded));
        Assert.Equal(BitConverter.SingleToInt32Bits(value), BitConverter.SingleToInt32Bits(decoded));
    }

    private static void CheckDouble(double value)
    {
        var result = JsonFiniteFloatMapping.FromDouble(value);
        Assert.True(result.IsSuccess);
        Assert.Equal(JsonFieldValueKind.Number, result.Value.Kind);
        using var json = JsonDocument.Parse(JsonFieldValueCodec.Encode(result.Value));
        Assert.True(json.RootElement.TryGetDouble(out var decoded));
        Assert.Equal(BitConverter.DoubleToInt64Bits(value), BitConverter.DoubleToInt64Bits(decoded));
    }

    [Fact]
    public void FiniteBoundariesAndSignedZerosRoundTripAsNumbers()
    {
        foreach (var value in new[] { 0f, BitConverter.Int32BitsToSingle(int.MinValue), float.Epsilon, -float.Epsilon,
            float.MaxValue, -float.MaxValue, 1.25f, 16777216f }) CheckSingle(value);
        foreach (var value in new[] { 0d, BitConverter.Int64BitsToDouble(long.MinValue), double.Epsilon, -double.Epsilon,
            double.MaxValue, -double.MaxValue, 1.25d, 9007199254740992d }) CheckDouble(value);
    }

    [Fact]
    public void DeterministicFiniteBitPatternsRoundTrip()
    {
        var random = new Random(225227);
        var bytes = new byte[8];
        for (var index = 0; index < 4096; index++)
        {
            random.NextBytes(bytes);
            var single = BitConverter.Int32BitsToSingle(BitConverter.ToInt32(bytes));
            var binary64 = BitConverter.Int64BitsToDouble(BitConverter.ToInt64(bytes));
            if (float.IsFinite(single)) CheckSingle(single);
            if (double.IsFinite(binary64)) CheckDouble(binary64);
        }
    }

    [Fact]
    public void NonFiniteValuesReturnStructuredErrors()
    {
        foreach (var value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity,
            BitConverter.Int32BitsToSingle(unchecked((int)0x7fa12345)) })
            Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidFloat, "$"), JsonFiniteFloatMapping.FromSingle(value).Error);
        foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity,
            BitConverter.Int64BitsToDouble(unchecked((long)0x7ff123456789abcd)) })
            Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidFloat, "$"), JsonFiniteFloatMapping.FromDouble(value).Error);
    }

    [Fact]
    public void MixedTreeSurvivesNestedUnionAndReferenceHandoff()
    {
        var negativeZero = BitConverter.Int32BitsToSingle(int.MinValue);
        var items = new[] {
            JsonFiniteFloatMapping.FromSingle(negativeZero).Value,
            JsonFiniteFloatMapping.FromDouble(double.MaxValue).Value,
            JsonFieldValue.Number(Number("9007199254740993.12500")) };
        var tree = JsonFieldValue.List(items);
        items[0] = JsonFieldValue.Null();
        var fields = new JsonScalarRecordSchema([new("values", JsonRecordFieldKind.FieldValue)]);
        var schema = new JsonTaggedUnionSchema([new("Metrics", fields)]);
        var record = JsonScalarRecord.Create(fields, [tree], [true]);
        var payload = JsonValidatedUnionPayload.FromValue(JsonTaggedUnion.Create(schema, 0, record));
        var bytes = payload.Bytes;
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        var request = new TargetedRequest("app", "submission", TargetedOperation.Invoke, "actor", bytes, Bytes("test-schema-v1"), [1]);
        Assert.Equal(TargetedAcceptKind.Created, store.Accept(request, Guid.NewGuid(), "selected").Kind);
        Array.Fill(bytes, (byte)'x');
        var recovered = JsonValidatedUnionPayload.Decode(schema, store.Lookup("app", "submission")!.Request.Payload);
        Assert.True(recovered.IsSuccess);
        var values = ((JsonFieldValue)recovered.Value.Value.Payload!.ValueAt(0)!).ListItems;
        using (var first = JsonDocument.Parse(JsonFieldValueCodec.Encode(values[0])))
            Assert.Equal(int.MinValue, BitConverter.SingleToInt32Bits(first.RootElement.GetSingle()));
        using (var second = JsonDocument.Parse(JsonFieldValueCodec.Encode(values[1])))
            Assert.Equal(BitConverter.DoubleToInt64Bits(double.MaxValue), BitConverter.DoubleToInt64Bits(second.RootElement.GetDouble()));
        Assert.Equal("9007199254740993.12500", Encoding.UTF8.GetString(JsonFieldValueCodec.Encode(values[2])));
        Assert.False(store.IsDurable);
    }
}
