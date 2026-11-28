using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonNestedNumberTests
{
    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);

    [Fact]
    public void NumericLeaves()
    {
        var signed = JsonCollectionShape.Map(JsonCollectionShape.List(JsonCollectionShape.Nullable(JsonCollectionShape.Int64())));
        const string signedJson = "{\"k\":[-9223372036854775808,null,9223372036854775807]}";
        var result = JsonCollectionCodec.Decode(signed, Bytes(signedJson));
        Assert.True(result.IsSuccess); Assert.Equal(signedJson, Encoding.UTF8.GetString(JsonFieldValueCodec.Encode(result.Value)));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidInteger, "$[0][0]"), JsonCollectionCodec.Decode(signed, Bytes("{\"x\":[9223372036854775808]}")).Error);
        var unsigned = JsonCollectionShape.List(JsonCollectionShape.UInt64());
        Assert.True(JsonCollectionCodec.Decode(unsigned, Bytes("[0,18446744073709551615]")).IsSuccess);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidInteger, "$[1]"), JsonCollectionCodec.Decode(unsigned, Bytes("[0,18446744073709551616]")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidInteger, "$[0]"), JsonCollectionCodec.Decode(unsigned, Bytes("[-1]")).Error);
        var decimals = JsonCollectionShape.List(JsonCollectionShape.Map(JsonCollectionShape.Decimal()));
        const string precise = "[{\"amount\":9007199254740993.125,\"scale\":1.2300}]";
        var exactDecimal = JsonCollectionCodec.Decode(decimals, Bytes(precise));
        Assert.True(exactDecimal.IsSuccess); Assert.Equal(precise, Encoding.UTF8.GetString(JsonFieldValueCodec.Encode(exactDecimal.Value)));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidDecimal, "$[0][0]"), JsonCollectionCodec.Decode(decimals, Bytes("[{\"secret\":1e-29}]")).Error);
        var single = JsonCollectionShape.List(JsonCollectionShape.Single());
        var singleValue = JsonCollectionCodec.Decode(single, Bytes("[-0.0,1.40129846e-45]"));
        Assert.True(singleValue.IsSuccess);
        Assert.Equal(int.MinValue, BitConverter.SingleToInt32Bits(JsonFiniteFloatMapping.ToSingle(singleValue.Value.ListAt(0)).Value));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidFloat, "$[0]"), JsonCollectionCodec.Decode(single, Bytes("[1e9999]")).Error);
        var doubles = JsonCollectionShape.Map(JsonCollectionShape.Double());
        var finiteDouble = JsonCollectionCodec.Decode(doubles, Bytes("{\"x\":-0.0,\"max\":1.7976931348623157e308}"));
        Assert.True(finiteDouble.IsSuccess);
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(JsonFiniteFloatMapping.ToDouble(finiteDouble.Value.MapAt(0).Value).Value));
        Assert.Equal(double.MaxValue, JsonFiniteFloatMapping.ToDouble(finiteDouble.Value.MapAt(1).Value).Value);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidFloat, "$[0]"), JsonCollectionCodec.Decode(doubles, Bytes("{\"secret\":1e9999}")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[0]"), JsonCollectionCodec.Decode(doubles, Bytes("{\"secret\":\"secret-value\"}")).Error);
        var raw = JsonCollectionShape.List(JsonCollectionShape.ExactNumber());
        const string rawJson = "[1e9999,900719925474099312345678901234567890]";
        var exact = JsonCollectionCodec.Decode(raw, Bytes(rawJson));
        Assert.True(exact.IsSuccess); Assert.Equal(rawJson, Encoding.UTF8.GetString(JsonFieldValueCodec.Encode(exact.Value)));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[0]"), JsonCollectionCodec.Decode(raw, Bytes("[null]")).Error);
        Assert.DoesNotContain("secret", JsonCollectionCodec.Decode(doubles, Bytes("{\"secret\":\"secret-value\"}")).Error.ToString());
    }
}
