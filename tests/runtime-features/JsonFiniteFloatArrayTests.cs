using System.Text;
using System.Text.Json;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonFiniteFloatArrayTests
{
    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);

    [Fact]
    public void RequiredArraysPreserveBitsAndSnapshotInputs()
    {
        var singles = new[] { -0f, float.Epsilon, -float.MaxValue, 1.25f };
        var doubles = new[] { -0d, double.Epsilon, double.MaxValue, -1.25d };
        var singleTree = JsonFiniteFloatMapping.FromSingles(singles).Value;
        var doubleTree = JsonFiniteFloatMapping.FromDoubles(doubles).Value;
        singles[0] = 7f;
        doubles[0] = 7d;
        using var singleJson = JsonDocument.Parse(JsonFieldValueCodec.Encode(singleTree));
        using var doubleJson = JsonDocument.Parse(JsonFieldValueCodec.Encode(doubleTree));
        var expectedSingles = new[] { -0f, float.Epsilon, -float.MaxValue, 1.25f };
        var expectedDoubles = new[] { -0d, double.Epsilon, double.MaxValue, -1.25d };
        for (var i = 0; i < expectedSingles.Length; i++)
            Assert.Equal(BitConverter.SingleToInt32Bits(expectedSingles[i]), BitConverter.SingleToInt32Bits(singleJson.RootElement[i].GetSingle()));
        for (var i = 0; i < expectedDoubles.Length; i++)
            Assert.Equal(BitConverter.DoubleToInt64Bits(expectedDoubles[i]), BitConverter.DoubleToInt64Bits(doubleJson.RootElement[i].GetDouble()));
        Assert.Equal("[]", Encoding.UTF8.GetString(JsonFieldValueCodec.Encode(JsonFiniteFloatMapping.FromSingles([]).Value)));
        Assert.Equal("[]", Encoding.UTF8.GetString(JsonFieldValueCodec.Encode(JsonFiniteFloatMapping.FromDoubles([]).Value)));
        Assert.Throws<ArgumentNullException>(() => JsonFiniteFloatMapping.FromSingles(null!));
        Assert.Throws<ArgumentNullException>(() => JsonFiniteFloatMapping.FromDoubles(null!));
    }

    [Fact]
    public void NullableArraysPreserveNullsAndSignedZero()
    {
        float?[] singles = [null, -0f, float.MaxValue];
        double?[] doubles = [double.MinValue, null, -0d];
        var a = JsonFiniteFloatMapping.FromNullableSingles(singles).Value;
        var b = JsonFiniteFloatMapping.FromNullableDoubles(doubles).Value;
        singles[0] = 42;
        doubles[1] = 42;
        using var singleJson = JsonDocument.Parse(JsonFieldValueCodec.Encode(a));
        using var doubleJson = JsonDocument.Parse(JsonFieldValueCodec.Encode(b));
        Assert.Equal(JsonValueKind.Null, singleJson.RootElement[0].ValueKind);
        Assert.Equal(int.MinValue, BitConverter.SingleToInt32Bits(singleJson.RootElement[1].GetSingle()));
        Assert.Equal(float.MaxValue, singleJson.RootElement[2].GetSingle());
        Assert.Equal(double.MinValue, doubleJson.RootElement[0].GetDouble());
        Assert.Equal(JsonValueKind.Null, doubleJson.RootElement[1].ValueKind);
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(doubleJson.RootElement[2].GetDouble()));
        Assert.Throws<ArgumentNullException>(() => JsonFiniteFloatMapping.FromNullableSingles(null!));
        Assert.Throws<ArgumentNullException>(() => JsonFiniteFloatMapping.FromNullableDoubles(null!));
    }

    [Fact]
    public void FirstNonFiniteElementReturnsOnlyAnIndexedError()
    {
        var expected = new JsonScalarError(JsonScalarErrorCode.InvalidFloat, "$[1]");
        Assert.Equal(expected, JsonFiniteFloatMapping.FromSingles([1f, float.NaN, float.PositiveInfinity]).Error);
        Assert.Equal(expected, JsonFiniteFloatMapping.FromDoubles([1d, double.NegativeInfinity, double.NaN]).Error);
        Assert.Equal(expected, JsonFiniteFloatMapping.FromNullableSingles([null, float.PositiveInfinity, float.NaN]).Error);
        Assert.Equal(expected, JsonFiniteFloatMapping.FromNullableDoubles([null, double.NaN, double.PositiveInfinity]).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidFloat, "$[0]"), JsonFiniteFloatMapping.FromSingles([float.NegativeInfinity]).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidFloat, "$[2]"), JsonFiniteFloatMapping.FromDoubles([1d, 2d, double.PositiveInfinity]).Error);
    }

    [Fact]
    public void ArrayTreeSurvivesUnionAndReferenceHandoff()
    {
        var source = new float?[] { -0f, null, float.MaxValue };
        var array = JsonFiniteFloatMapping.FromNullableSingles(source).Value;
        source[0] = 1f;
        var exact = JsonExactNumberCodec.Decode(Bytes("9007199254740993.12500")).Value;
        var tree = JsonFieldValue.List([array, JsonFieldValue.Number(exact)]);
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
        var items = ((JsonFieldValue)recovered.Value.Value.Payload!.ValueAt(0)!).ListItems;
        using var json = JsonDocument.Parse(JsonFieldValueCodec.Encode(items[0]));
        Assert.Equal(int.MinValue, BitConverter.SingleToInt32Bits(json.RootElement[0].GetSingle()));
        Assert.Equal(JsonValueKind.Null, json.RootElement[1].ValueKind);
        Assert.Equal(float.MaxValue, json.RootElement[2].GetSingle());
        Assert.Equal("9007199254740993.12500", Encoding.UTF8.GetString(JsonFieldValueCodec.Encode(items[1])));
        Assert.False(store.IsDurable);
    }
}
