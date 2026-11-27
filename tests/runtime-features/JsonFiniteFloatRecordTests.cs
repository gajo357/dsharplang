using System.Globalization;
using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonFiniteFloatRecordTests
{
    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);
    private static readonly JsonScalarRecordSchema Schema = new([
        new("single", JsonRecordFieldKind.Single), new("double", JsonRecordFieldKind.Double),
        new("maybeSingle", JsonRecordFieldKind.NullableSingle, Optional: true),
        new("maybeDouble", JsonRecordFieldKind.NullableDouble, Optional: true)]);

    [Fact]
    public void TypedRecordRoundTripsFiniteBitsAndOptionalPresence()
    {
        var record = JsonScalarRecord.Create(Schema, [-0f, double.Epsilon, null, -double.MaxValue], [true, true, false, true]);
        var decoded = JsonScalarRecordCodec.Decode(Schema, JsonScalarRecordCodec.Encode(record));
        Assert.True(decoded.IsSuccess);
        Assert.Equal(int.MinValue, BitConverter.SingleToInt32Bits((float)decoded.Value.ValueAt(0)!));
        Assert.Equal(BitConverter.DoubleToInt64Bits(double.Epsilon), BitConverter.DoubleToInt64Bits((double)decoded.Value.ValueAt(1)!));
        Assert.False(decoded.Value.IsPresent(2));
        Assert.True(decoded.Value.IsPresent(3));
        Assert.Equal(-double.MaxValue, decoded.Value.ValueAt(3));
        var explicitNull = JsonScalarRecordCodec.Decode(Schema, Bytes("{\"single\":1,\"double\":-0,\"maybeSingle\":null,\"maybeDouble\":null}"));
        Assert.True(explicitNull.IsSuccess);
        Assert.True(explicitNull.Value.IsPresent(2)); Assert.Null(explicitNull.Value.ValueAt(2));
        Assert.True(explicitNull.Value.IsPresent(3)); Assert.Null(explicitNull.Value.ValueAt(3));
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits((double)explicitNull.Value.ValueAt(1)!));
    }

    [Fact]
    public void InvariantRoundingUnderflowAndExtremesWorkInFields()
    {
        var prior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var result = JsonScalarRecordCodec.Decode(Schema, Bytes("{\"single\":1.000000059604644775390625,\"double\":-1e-9999,\"maybeSingle\":1.401298464324817e-45,\"maybeDouble\":1.7976931348623157e308}"));
            Assert.True(result.IsSuccess);
            Assert.Equal(1f, result.Value.ValueAt(0));
            Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits((double)result.Value.ValueAt(1)!));
            Assert.Equal(float.Epsilon, result.Value.ValueAt(2));
            Assert.Equal(double.MaxValue, result.Value.ValueAt(3));
        }
        finally { CultureInfo.CurrentCulture = prior; }
    }

    [Fact]
    public void ConstructionAndDecodeRejectNonfiniteAndWrongTypes()
    {
        foreach (var value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(Schema, [value, 1d, null, null], [true, true, false, false]));
            Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(Schema, [1f, 1d, value, null], [true, true, true, false]));
        }
        foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(Schema, [1f, value, null, null], [true, true, false, false]));
            Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(Schema, [1f, 1d, null, value], [true, true, false, true]));
        }
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidFloat, "$[\"single\"]"), JsonScalarRecordCodec.Decode(Schema, Bytes("{\"single\":1e9999,\"double\":1}")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidFloat, "$[\"maybeDouble\"]"), JsonScalarRecordCodec.Decode(Schema, Bytes("{\"single\":1,\"double\":1,\"maybeDouble\":-1e9999}")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"maybeSingle\"]"), JsonScalarRecordCodec.Decode(Schema, Bytes("{\"single\":1,\"double\":1,\"maybeSingle\":\"secret\"}")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"single\"]"), JsonScalarRecordCodec.Decode(Schema, Bytes("{\"single\":null,\"double\":1}")).Error);
    }

    [Fact]
    public void NestedUnionPreservesFieldsAndRedactsErrorPath()
    {
        var unionSchema = new JsonTaggedUnionSchema([new("Metrics", Schema)]);
        var record = JsonScalarRecord.Create(Schema, [float.MaxValue, -0d, float.Epsilon, null], [true, true, true, true]);
        var union = JsonTaggedUnion.Create(unionSchema, 0, record);
        var decoded = JsonTaggedUnionCodec.Decode(unionSchema, JsonTaggedUnionCodec.Encode(union));
        Assert.True(decoded.IsSuccess);
        Assert.Equal(float.MaxValue, decoded.Value.Payload!.ValueAt(0));
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits((double)decoded.Value.Payload.ValueAt(1)!));
        var tree = JsonTypedFieldMapping.FromUnion(union).Value;
        Assert.True(JsonTypedFieldMapping.ToUnion(unionSchema, tree).IsSuccess);
        var outer = new JsonScalarRecordSchema([new("inner", JsonRecordFieldKind.Record, NestedSchema: Schema)]);
        var bad = JsonScalarRecordCodec.Decode(outer, Bytes("{\"inner\":{\"single\":1,\"double\":1,\"maybeDouble\":\"secret\"}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"inner\"][\"maybeDouble\"]"), bad.Error);
    }
}
