using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonScalarArrayCodecTests
{
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    [Fact]
    public void ScalarArraysRoundTripAndDecodedValuesAreReadOnly()
    {
        Assert.Equal(new[] { true, false }, JsonScalarArrayCodec.DecodeBooleans(
            JsonScalarArrayCodec.EncodeBooleans([true, false])).Value);
        var input = new[] { int.MinValue, 0, int.MaxValue };
        var bytes = JsonScalarArrayCodec.EncodeIntegers(input);
        input[1] = 10;
        var integers = JsonScalarArrayCodec.DecodeIntegers(bytes).Value;
        Assert.Equal(new[] { int.MinValue, 0, int.MaxValue }, integers);
        Assert.Throws<NotSupportedException>(() => ((IList<int>)integers)[0] = 1);
        Assert.Equal(new[] { "", "café 😀", "quote\"\n" }, JsonScalarArrayCodec.DecodeStrings(
            JsonScalarArrayCodec.EncodeStrings(["", "café 😀", "quote\"\n"])).Value);
        Assert.Empty(JsonScalarArrayCodec.DecodeIntegers(JsonScalarArrayCodec.EncodeIntegers([])).Value);
    }

    [Fact]
    public void NullableArraysKeepNullDistinctFromFalseZeroAndEmpty()
    {
        Assert.Equal(new bool?[] { null, false, true }, JsonScalarArrayCodec.DecodeNullableBooleans(
            JsonScalarArrayCodec.EncodeNullableBooleans([null, false, true])).Value);
        Assert.Equal(new int?[] { null, 0, -1 }, JsonScalarArrayCodec.DecodeNullableIntegers(
            JsonScalarArrayCodec.EncodeNullableIntegers([null, 0, -1])).Value);
        Assert.Equal(new string?[] { null, "", "x" }, JsonScalarArrayCodec.DecodeNullableStrings(
            JsonScalarArrayCodec.EncodeNullableStrings([null, "", "x"])).Value);
    }

    [Fact]
    public void NestedIntegerArraysRoundTripAsIndependentReadOnlyCollections()
    {
        int[][] input = [[1, 2], [], [int.MinValue]];
        var bytes = JsonScalarArrayCodec.EncodeNestedIntegers(input);
        input[0][0] = 99;
        input[2] = [42];
        var result = JsonScalarArrayCodec.DecodeNestedIntegers(bytes).Value;
        Assert.Equal(new[] { 1, 2 }, result[0]);
        Assert.Empty(result[1]);
        Assert.Equal(new[] { int.MinValue }, result[2]);
        Assert.Throws<NotSupportedException>(() => ((IList<IReadOnlyList<int>>)result)[0] = Array.Empty<int>());
        Assert.Throws<NotSupportedException>(() => ((IList<int>)result[0])[0] = 5);
    }

    [Fact]
    public void ElementFailuresIncludeOnlyGeneratedIndexPaths()
    {
        Failure(JsonScalarArrayCodec.DecodeIntegers(Utf8("[0,1.5]")), JsonScalarErrorCode.InvalidInteger, "$[1]");
        Failure(JsonScalarArrayCodec.DecodeIntegers(Utf8("[2147483648]")), JsonScalarErrorCode.InvalidInteger, "$[0]");
        Failure(JsonScalarArrayCodec.DecodeBooleans(Utf8("[true,null]")), JsonScalarErrorCode.TypeMismatch, "$[1]");
        Failure(JsonScalarArrayCodec.DecodeStrings(Utf8("[\"ok\",false]")), JsonScalarErrorCode.TypeMismatch, "$[1]");
        Failure(JsonScalarArrayCodec.DecodeNestedIntegers(Utf8("[[1], [2,3,\"secret-value\"]]")), JsonScalarErrorCode.TypeMismatch, "$[1][2]");
        Failure(JsonScalarArrayCodec.DecodeNestedIntegers(Utf8("[[1],null]")), JsonScalarErrorCode.TypeMismatch, "$[1]");
        Failure(JsonScalarArrayCodec.DecodeIntegers(Utf8("[0,0,0,0,0,0,0,0,0,0,false]")), JsonScalarErrorCode.TypeMismatch, "$[10]");
    }

    [Fact]
    public void MalformedInputAndWrongRootFailWithoutPartialValuesOrSecrets()
    {
        Failure(JsonScalarArrayCodec.DecodeIntegers(Utf8("null")), JsonScalarErrorCode.TypeMismatch, "$");
        Failure(JsonScalarArrayCodec.DecodeNestedIntegers(Utf8("[1] trailing")), JsonScalarErrorCode.InvalidJson, "$");
        var result = JsonScalarArrayCodec.DecodeIntegers(Utf8("[1,\"secret-value\"]"));
        Failure(result, JsonScalarErrorCode.TypeMismatch, "$[1]");
        Assert.DoesNotContain("secret-value", result.Error!.Value.ToString(), StringComparison.Ordinal);
        var error = Assert.Throws<InvalidOperationException>(() => result.Value);
        Assert.DoesNotContain("secret-value", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnicodeAndInvalidArrayInputsAreRejected()
    {
        var invalid = new string('\uD800', 1);
        Assert.Throws<ArgumentNullException>(() => JsonScalarArrayCodec.EncodeIntegers(null!));
        Assert.Throws<ArgumentException>(() => JsonScalarArrayCodec.EncodeStrings(["ok", null!]));
        var error = Assert.Throws<ArgumentException>(() => JsonScalarArrayCodec.EncodeStrings(["secret-value" + invalid]));
        Assert.DoesNotContain("secret-value", error.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => JsonScalarArrayCodec.EncodeNestedIntegers([[1], null!]));
        var decoded = JsonScalarArrayCodec.DecodeStrings(Utf8("[\"ok\",\"\\uD800\"]"));
        Assert.False(decoded.IsSuccess);
        Assert.NotNull(decoded.Error);
        Assert.Equal("$[1]", decoded.Error.Value.Path);
        Assert.Contains(decoded.Error.Value.Code, new[] { JsonScalarErrorCode.InvalidJson, JsonScalarErrorCode.InvalidUnicode });
        var invalidUtf8 = JsonScalarArrayCodec.DecodeStrings(new byte[] { 0x5b, 0x22, 0x6f, 0x6b, 0x22, 0x2c, 0x22, 0xff, 0x22, 0x5d });
        Assert.False(invalidUtf8.IsSuccess);
        Assert.NotNull(invalidUtf8.Error);
        Assert.Equal("$[1]", invalidUtf8.Error.Value.Path);
        Assert.Contains(invalidUtf8.Error.Value.Code, new[] { JsonScalarErrorCode.InvalidJson, JsonScalarErrorCode.InvalidUnicode });
    }

    private static void Failure<T>(JsonScalarResult<T> result, JsonScalarErrorCode expected, string path)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(new JsonScalarError(expected, path), result.Error);
    }
}
