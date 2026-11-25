using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonScalarCodecTests
{
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    [Fact]
    public void BooleanAndIntegerValuesRoundTripWithoutCoercion()
    {
        foreach (var value in new[] { false, true })
        {
            var result = JsonScalarCodec.DecodeBoolean(JsonScalarCodec.EncodeBoolean(value));
            Assert.True(result.IsSuccess);
            Assert.Null(result.Error);
            Assert.Equal(value, result.Value);
        }
        foreach (var value in new[] { int.MinValue, -1, 0, 1, int.MaxValue })
        {
            var result = JsonScalarCodec.DecodeInteger(JsonScalarCodec.EncodeInteger(value));
            Assert.True(result.IsSuccess);
            Assert.Null(result.Error);
            Assert.Equal(value, result.Value);
        }
        Assert.Equal("-2147483648", Encoding.UTF8.GetString(JsonScalarCodec.EncodeInteger(int.MinValue)));
        Assert.Equal("true", Encoding.UTF8.GetString(JsonScalarCodec.EncodeBoolean(true)));
    }

    [Fact]
    public void StringsPreserveEmptyUnicodeControlsAndNonBmpScalars()
    {
        foreach (var value in new[] { "", "plain", "quote \" slash \\ newline\n", "café 漢字 😀", "\0" })
        {
            var result = JsonScalarCodec.DecodeString(JsonScalarCodec.EncodeString(value));
            Assert.True(result.IsSuccess);
            Assert.Equal(value, result.Value);
        }
        Assert.Equal("A😀", JsonScalarCodec.DecodeString(Utf8("\"A\\uD83D\\uDE00\"")).Value);
    }

    [Fact]
    public void NullableScalarsDistinguishNullFromFalseZeroAndEmpty()
    {
        Assert.Null(JsonScalarCodec.DecodeNullableBoolean(JsonScalarCodec.EncodeNullableBoolean(null)).Value);
        Assert.False(JsonScalarCodec.DecodeNullableBoolean(JsonScalarCodec.EncodeNullableBoolean(false)).Value);
        Assert.Null(JsonScalarCodec.DecodeNullableInteger(JsonScalarCodec.EncodeNullableInteger(null)).Value);
        Assert.Equal(0, JsonScalarCodec.DecodeNullableInteger(JsonScalarCodec.EncodeNullableInteger(0)).Value);
        Assert.Null(JsonScalarCodec.DecodeNullableString(JsonScalarCodec.EncodeNullableString(null)).Value);
        Assert.Equal("", JsonScalarCodec.DecodeNullableString(JsonScalarCodec.EncodeNullableString("")).Value);
        Assert.Equal("null", Encoding.UTF8.GetString(JsonScalarCodec.EncodeNullableString(null)));
    }

    [Fact]
    public void NonNullableValuesAndWrongJsonTypesAreRejected()
    {
        Failure(JsonScalarCodec.DecodeBoolean(Utf8("null")), JsonScalarErrorCode.TypeMismatch);
        Failure(JsonScalarCodec.DecodeInteger(Utf8("null")), JsonScalarErrorCode.TypeMismatch);
        Failure(JsonScalarCodec.DecodeString(Utf8("null")), JsonScalarErrorCode.TypeMismatch);
        Failure(JsonScalarCodec.DecodeBoolean(Utf8("1")), JsonScalarErrorCode.TypeMismatch);
        Failure(JsonScalarCodec.DecodeInteger(Utf8("\"1\"")), JsonScalarErrorCode.TypeMismatch);
        Failure(JsonScalarCodec.DecodeString(Utf8("false")), JsonScalarErrorCode.TypeMismatch);
        Failure(JsonScalarCodec.DecodeNullableBoolean(Utf8("[]")), JsonScalarErrorCode.TypeMismatch);
        Failure(JsonScalarCodec.DecodeNullableInteger(Utf8("{}")), JsonScalarErrorCode.TypeMismatch);
        Failure(JsonScalarCodec.DecodeNullableString(Utf8("42")), JsonScalarErrorCode.TypeMismatch);
        Assert.True(JsonScalarCodec.DecodeBoolean(Utf8("  true \n")).Value);
    }

    [Fact]
    public void FractionalExponentAndOutOfRangeIntegersAreRejected()
    {
        foreach (var number in new[] { "2147483648", "-2147483649", "1.0", "1e0", "1e100" })
            Failure(JsonScalarCodec.DecodeInteger(Utf8(number)), JsonScalarErrorCode.InvalidInteger);
        Failure(JsonScalarCodec.DecodeNullableInteger(Utf8("1.5")), JsonScalarErrorCode.InvalidInteger);
    }

    [Fact]
    public void MalformedAndTrailingJsonProduceRedactedRootErrors()
    {
        const string secret = "secret-value-8e6e";
        foreach (var input in new[] { "", "true false", $"{{\"{secret}\":", "/*comment*/ true", "01" })
        {
            var result = JsonScalarCodec.DecodeBoolean(Utf8(input));
            Failure(result, JsonScalarErrorCode.InvalidJson);
            Assert.DoesNotContain(secret, result.Error!.Value.ToString(), StringComparison.Ordinal);
            var error = Assert.Throws<InvalidOperationException>(() => result.Value);
            Assert.DoesNotContain(secret, error.Message, StringComparison.Ordinal);
        }
        var wrongType = JsonScalarCodec.DecodeInteger(Utf8($"\"{secret}\""));
        Failure(wrongType, JsonScalarErrorCode.TypeMismatch);
        Assert.DoesNotContain(secret, wrongType.Error!.Value.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidUnicodeIsRejectedWithoutLeakingInput()
    {
        var loneHigh = new string('\uD800', 1);
        var loneLow = new string('\uDC00', 1);
        Assert.Throws<ArgumentException>(() => JsonScalarCodec.EncodeString(loneHigh));
        Assert.Throws<ArgumentException>(() => JsonScalarCodec.EncodeNullableString(loneLow));
        var encodeError = Assert.Throws<ArgumentException>(() => JsonScalarCodec.EncodeString("secret-value-8e6e" + loneHigh));
        Assert.DoesNotContain("secret-value-8e6e", encodeError.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentNullException>(() => JsonScalarCodec.EncodeString(null!));
        foreach (var input in new[] { Utf8("\"\\uD800\""), Utf8("\"\\uDC00\""), new byte[] { 0x22, 0xff, 0x22 } })
        {
            var result = JsonScalarCodec.DecodeString(input);
            Assert.False(result.IsSuccess);
            Assert.NotNull(result.Error);
            Assert.Equal("$", result.Error.Value.Path);
            Assert.Contains(result.Error.Value.Code, new[] { JsonScalarErrorCode.InvalidJson, JsonScalarErrorCode.InvalidUnicode });
        }
    }

    private static void Failure<T>(JsonScalarResult<T> result, JsonScalarErrorCode expected)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(new JsonScalarError(expected, "$"), result.Error);
    }
}
