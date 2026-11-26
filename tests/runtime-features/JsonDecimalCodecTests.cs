using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonDecimalCodecTests
{
    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);
    private static string Text(byte[] value) => Encoding.UTF8.GetString(value);

    [Fact]
    public void ExactTypedValuesRoundTripAcrossTheFullRange()
    {
        foreach (var value in new[] { decimal.MinValue, decimal.MaxValue, 0m, -0.0000000000000000000000000001m, 9007199254740993.125m, 123.4500m })
        {
            var encoded = JsonDecimalCodec.Encode(value);
            var decoded = JsonDecimalCodec.Decode(encoded);
            Assert.True(decoded.IsSuccess);
            Assert.Equal(value, decoded.Value);
            Assert.Equal(value, JsonDecimalCodec.DecodeNullable(encoded).Value);
        }
        Assert.Equal("123.4500", Text(JsonDecimalCodec.Encode(123.4500m)));
        Assert.Equal("null", Text(JsonDecimalCodec.EncodeNullable(null)));
        Assert.Null(JsonDecimalCodec.DecodeNullable(Bytes("null")).Value);
    }

    [Fact]
    public void DeterministicCoefficientAndScaleSamplesRoundTripWithoutLoss()
    {
        var random = new Random(2026);
        for (var index = 0; index < 512; index++)
        {
            var value = new decimal(random.Next(), random.Next(), random.Next(), index % 2 != 0, (byte)(index % 29));
            var decoded = JsonDecimalCodec.Decode(JsonDecimalCodec.Encode(value));
            Assert.True(decoded.IsSuccess);
            Assert.Equal(value, decoded.Value);
        }
    }

    [Theory]
    [InlineData("1.2300", "1.23")]
    [InlineData("1.00e+2", "100")]
    [InlineData("10e-29", "0.0000000000000000000000000001")]
    [InlineData("79228162514264337593543950335.0", "79228162514264337593543950335")]
    [InlineData("0e999999999999999999999999", "0")]
    [InlineData("-0.000e-999999999999999999999999", "0")]
    public void ExponentsAndRedundantZerosAreAcceptedOnlyWhenExact(string json, string encoded)
    {
        var decoded = JsonDecimalCodec.Decode(Bytes(json));
        Assert.True(decoded.IsSuccess);
        Assert.Equal(encoded, Text(JsonDecimalCodec.Encode(decoded.Value)));
    }

    [Theory]
    [InlineData("79228162514264337593543950336")]
    [InlineData("-79228162514264337593543950336")]
    [InlineData("1e-29")]
    [InlineData("1.00000000000000000000000000001")]
    [InlineData("79228162514264337593543950335.1")]
    [InlineData("1e999999999999999999999999")]
    [InlineData("1e-999999999999999999999999")]
    public void RejectsOverflowAndUnrepresentablePrecisionInsteadOfRounding(string json)
    {
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidDecimal, "$"), JsonDecimalCodec.Decode(Bytes(json)).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidDecimal, "$"), JsonDecimalCodec.DecodeNullable(Bytes(json)).Error);
    }

    [Fact]
    public void WrongTypesAndMalformedInputReturnStructuredErrors()
    {
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$"), JsonDecimalCodec.Decode(Bytes("\"secret\"")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$"), JsonDecimalCodec.Decode(Bytes("null")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidJson, "$"), JsonDecimalCodec.Decode(Bytes("1e")).Error);
    }

    [Fact]
    public void DecimalFieldsRoundTripThroughUnionAndValidatedPayload()
    {
        var fields = new JsonScalarRecordSchema([
            new("amount", JsonRecordFieldKind.Decimal),
            new("discount", JsonRecordFieldKind.NullableDecimal, Optional: true),
            new("missing", JsonRecordFieldKind.NullableDecimal, Optional: true)]);
        var schema = new JsonTaggedUnionSchema([new("Pay", fields)]);
        const string json = "{\"case\":\"Pay\",\"value\":{\"amount\":9007199254740993.125,\"discount\":null}}";
        var decoded = JsonValidatedUnionPayload.Decode(schema, Bytes(json));
        Assert.True(decoded.IsSuccess);
        Assert.Equal(9007199254740993.125m, decoded.Value.Value.Payload!.ValueAt(0));
        Assert.True(decoded.Value.Value.Payload.IsPresent(1));
        Assert.Null(decoded.Value.Value.Payload.ValueAt(1));
        Assert.False(decoded.Value.Value.Payload.IsPresent(2));
        Assert.Equal(json, Text(decoded.Value.Bytes));
        var encoded = JsonValidatedUnionPayload.FromValue(decoded.Value.Value);
        Assert.Equal(json, Text(encoded.Bytes));
        var tree = JsonTypedFieldMapping.FromUnion(encoded.Value);
        Assert.True(tree.IsSuccess);
        Assert.Equal(json, Text(JsonTaggedUnionCodec.Encode(JsonTypedFieldMapping.ToUnion(schema, tree.Value).Value)));
        var value = JsonScalarRecordCodec.Decode(fields, Bytes("{\"amount\":1,\"discount\":0.25}"));
        Assert.True(value.IsSuccess);
        Assert.Equal(0.25m, value.Value.ValueAt(1));
    }

    [Fact]
    public void NestedErrorsUseRedactedPathsAndConstructionChecksTypes()
    {
        var fields = new JsonScalarRecordSchema([new("amount", JsonRecordFieldKind.Decimal)]);
        var schema = new JsonTaggedUnionSchema([new("Pay", fields)]);
        var overflow = JsonTaggedUnionCodec.Decode(schema, Bytes("{\"case\":\"Pay\",\"value\":{\"amount\":1e-29}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidDecimal, "$[\"value\"][\"amount\"]"), overflow.Error);
        var secret = JsonTaggedUnionCodec.Decode(schema, Bytes("{\"case\":\"Pay\",\"value\":{\"amount\":\"secret-value\"}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"value\"][\"amount\"]"), secret.Error);
        Assert.DoesNotContain("secret-value", secret.Error!.Value.ToString(), StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(fields, [1], [true]));
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("amount", JsonRecordFieldKind.Decimal, Optional: true)]));
    }
}
