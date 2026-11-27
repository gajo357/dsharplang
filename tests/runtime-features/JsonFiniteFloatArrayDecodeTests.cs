using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonFiniteFloatArrayDecodeTests
{
    private static JsonFieldValue Tree(string json) => JsonFieldValueCodec.Decode(Encoding.UTF8.GetBytes(json)).Value;

    [Fact]
    public void RequiredArraysRoundTripBitsAndReturnIndependentArrays()
    {
        float[] singles = [-0f, float.Epsilon, -float.MaxValue, 1.25f];
        double[] doubles = [-0d, double.Epsilon, double.MaxValue, -1.25d];
        var singleTree = JsonFiniteFloatMapping.FromSingles(singles).Value;
        var doubleTree = JsonFiniteFloatMapping.FromDoubles(doubles).Value;
        var a = JsonFiniteFloatMapping.ToSingles(singleTree).Value;
        var b = JsonFiniteFloatMapping.ToDoubles(doubleTree).Value;
        for (var i = 0; i < singles.Length; i++) Assert.Equal(BitConverter.SingleToInt32Bits(singles[i]), BitConverter.SingleToInt32Bits(a[i]));
        for (var i = 0; i < doubles.Length; i++) Assert.Equal(BitConverter.DoubleToInt64Bits(doubles[i]), BitConverter.DoubleToInt64Bits(b[i]));
        a[0] = 3; b[0] = 3;
        Assert.Equal(int.MinValue, BitConverter.SingleToInt32Bits(JsonFiniteFloatMapping.ToSingles(singleTree).Value[0]));
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(JsonFiniteFloatMapping.ToDoubles(doubleTree).Value[0]));
        Assert.Empty(JsonFiniteFloatMapping.ToSingles(Tree("[]")).Value);
        Assert.Empty(JsonFiniteFloatMapping.ToDoubles(Tree("[]")).Value);
    }

    [Fact]
    public void NullableArraysRetainNullZeroAndRounding()
    {
        var tree = Tree("[null,-0,1.000000059604644775390625,-1e-9999]");
        var singles = JsonFiniteFloatMapping.ToNullableSingles(tree).Value;
        var doubles = JsonFiniteFloatMapping.ToNullableDoubles(tree).Value;
        Assert.Null(singles[0]); Assert.Null(doubles[0]);
        Assert.Equal(int.MinValue, BitConverter.SingleToInt32Bits(singles[1]!.Value));
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(doubles[1]!.Value));
        Assert.Equal(1f, singles[2]);
        Assert.Equal(int.MinValue, BitConverter.SingleToInt32Bits(singles[3]!.Value));
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(doubles[3]!.Value));
        Assert.Empty(JsonFiniteFloatMapping.ToNullableSingles(Tree("[]")).Value);
        Assert.Empty(JsonFiniteFloatMapping.ToNullableDoubles(Tree("[]")).Value);
    }

    [Fact]
    public void RootAndFirstInvalidElementAreRedacted()
    {
        var root = new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$");
        Assert.Equal(root, JsonFiniteFloatMapping.ToSingles(Tree("null")).Error);
        Assert.Equal(root, JsonFiniteFloatMapping.ToNullableDoubles(Tree("{} ")).Error);
        var wrong = new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[1]");
        Assert.Equal(wrong, JsonFiniteFloatMapping.ToSingles(Tree("[1,null,1e9999]")).Error);
        Assert.Equal(wrong, JsonFiniteFloatMapping.ToDoubles(Tree("[1,\"secret\",1e9999]")).Error);
        Assert.Equal(wrong, JsonFiniteFloatMapping.ToNullableSingles(Tree("[null,\"secret\",1e9999]")).Error);
        Assert.Equal(wrong, JsonFiniteFloatMapping.ToNullableDoubles(Tree("[null,{},1e9999]")).Error);
        var overflow = new JsonScalarError(JsonScalarErrorCode.InvalidFloat, "$[1]");
        Assert.Equal(overflow, JsonFiniteFloatMapping.ToSingles(Tree("[1,1e9999,\"secret\"]")).Error);
        Assert.Equal(overflow, JsonFiniteFloatMapping.ToDoubles(Tree("[1,1e9999,\"secret\"]")).Error);
        Assert.Equal(overflow, JsonFiniteFloatMapping.ToNullableSingles(Tree("[null,1e9999]")).Error);
        Assert.Equal(overflow, JsonFiniteFloatMapping.ToNullableDoubles(Tree("[null,1e9999]")).Error);
        Assert.Throws<ArgumentNullException>(() => JsonFiniteFloatMapping.ToSingles(null!));
        Assert.Throws<ArgumentNullException>(() => JsonFiniteFloatMapping.ToNullableDoubles(null!));
    }

    [Fact]
    public void NestedTreeEncodingRetainsTypedValues()
    {
        var nested = JsonFieldValue.List([JsonFiniteFloatMapping.FromNullableSingles([null, -0f, float.MaxValue]).Value]);
        var decoded = JsonFieldValueCodec.Decode(JsonFieldValueCodec.Encode(nested)).Value;
        var values = JsonFiniteFloatMapping.ToNullableSingles(decoded.ListItems[0]).Value;
        Assert.Null(values[0]);
        Assert.Equal(int.MinValue, BitConverter.SingleToInt32Bits(values[1]!.Value));
        Assert.Equal(float.MaxValue, values[2]);
    }
}
