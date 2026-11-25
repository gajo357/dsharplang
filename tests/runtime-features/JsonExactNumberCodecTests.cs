using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonExactNumberCodecTests
{
    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
    private static JsonExactNumber Number(string text)
    {
        var result = JsonExactNumberCodec.Decode(Utf8(text));
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    [Fact]
    public void ExactNumbersRetainWrittenDigitsAndExponentWithoutFloatingPoint()
    {
        var samples = new[] {
            "0", "-0", "12345678901234567890123456789012345678901234567890",
            "-987654321098765432109876543210.000000000000000000123400",
            "1.2300e+1000", "-0.0000000000000000000000000001E-9999"
        };
        foreach (var sample in samples)
        {
            var input = Utf8(" \n" + sample + "\t");
            var result = JsonExactNumberCodec.Decode(input);
            Assert.True(result.IsSuccess);
            Array.Fill(input, (byte)'x');
            var encoded = JsonExactNumberCodec.Encode(result.Value);
            Assert.Equal(sample, Encoding.UTF8.GetString(encoded));
            encoded[0] = (byte)'x';
            Assert.Equal(sample, Encoding.UTF8.GetString(JsonExactNumberCodec.Encode(result.Value)));
        }
    }

    [Fact]
    public void MalformedAndNonNumberValuesAreRejectedWithoutInputText()
    {
        foreach (var sample in new[] { "", "01", "+1", "1.", "1e", "NaN", "Infinity", "1 2", "1 trailing", "１２" })
        {
            var result = JsonExactNumberCodec.Decode(Utf8(sample));
            Assert.False(result.IsSuccess);
            Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidJson, "$"), result.Error);
            Assert.DoesNotContain(sample.Length == 0 ? "not-present" : sample, result.Error!.Value.ToString(), StringComparison.Ordinal);
        }
        foreach (var sample in new[] { "null", "true", "\"123\"", "[]", "{}" })
        {
            var result = JsonExactNumberCodec.Decode(Utf8(sample));
            Assert.False(result.IsSuccess);
            Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$"), result.Error);
        }
        var invalidUtf8 = JsonExactNumberCodec.Decode(new byte[] { 0xff });
        Assert.False(invalidUtf8.IsSuccess);
        Assert.Equal(JsonScalarErrorCode.InvalidJson, invalidUtf8.Error!.Value.Code);
        Assert.Throws<ArgumentNullException>(() => JsonExactNumberCodec.Encode(null!));
    }

    [Fact]
    public void RecordFieldsKeepExactTokensAndNullablePresence()
    {
        var schema = new JsonScalarRecordSchema([
            new("number", JsonRecordFieldKind.ExactNumber),
            new("optional", JsonRecordFieldKind.NullableExactNumber, Optional: true),
            new("nullable", JsonRecordFieldKind.NullableExactNumber)
        ]);
        var value = JsonScalarRecord.Create(schema, [Number("123456789012345678901234567890.000100"), null, Number("-0.0e+9")], [true, false, true]);
        var bytes = JsonScalarRecordCodec.Encode(value);
        var result = JsonScalarRecordCodec.Decode(schema, bytes);
        Assert.True(result.IsSuccess);
        Assert.Equal("123456789012345678901234567890.000100", Assert.IsType<JsonExactNumber>(result.Value.ValueAt(0)).Token);
        Assert.False(result.Value.IsPresent(1));
        Assert.Equal("-0.0e+9", Assert.IsType<JsonExactNumber>(result.Value.ValueAt(2)).Token);
        Assert.Equal(bytes, JsonScalarRecordCodec.Encode(result.Value));
        var explicitNull = JsonScalarRecordCodec.Decode(schema, Utf8("{\"number\":1.20,\"optional\":null,\"nullable\":null}"));
        Assert.True(explicitNull.IsSuccess);
        Assert.True(explicitNull.Value.IsPresent(1)); Assert.Null(explicitNull.Value.ValueAt(1));
        Assert.True(explicitNull.Value.IsPresent(2)); Assert.Null(explicitNull.Value.ValueAt(2));
        Assert.Equal("1.20", Assert.IsType<JsonExactNumber>(explicitNull.Value.ValueAt(0)).Token);
    }

    [Fact]
    public void NestedRecordAndUnionErrorsReportOnlyDeclaredPaths()
    {
        var payload = new JsonScalarRecordSchema([new("amount\n\"x", JsonRecordFieldKind.ExactNumber)]);
        var union = new JsonTaggedUnionSchema([new("Data", payload)]);
        var schema = new JsonScalarRecordSchema([new("entry", JsonRecordFieldKind.Union, UnionSchema: union)]);
        var good = JsonScalarRecordCodec.Decode(schema, Utf8("{\"entry\":{\"case\":\"Data\",\"value\":{\"amount\\n\\\"x\":123456789012345678901234567890.0000}}}"));
        Assert.True(good.IsSuccess);
        Assert.Contains("123456789012345678901234567890.0000", Encoding.UTF8.GetString(JsonScalarRecordCodec.Encode(good.Value)), StringComparison.Ordinal);
        var bad = JsonScalarRecordCodec.Decode(schema, Utf8("{\"entry\":{\"case\":\"Data\",\"value\":{\"amount\\n\\\"x\":\"secret-value\"}}}"));
        Assert.False(bad.IsSuccess);
        Assert.Equal(JsonScalarErrorCode.TypeMismatch, bad.Error!.Value.Code);
        Assert.StartsWith("$[\"entry\"][\"value\"]", bad.Error.Value.Path, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", bad.Error.Value.Path, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value", bad.Error.Value.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value", Assert.Throws<InvalidOperationException>(() => bad.Value).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RecordConstructionRejectsForeignOrMismatchedValues()
    {
        var schema = new JsonScalarRecordSchema([new("number", JsonRecordFieldKind.ExactNumber)]);
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(schema, [1], [true]));
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(schema, ["123"], [true]));
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(schema, [null], [true]));
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("number", JsonRecordFieldKind.ExactNumber, Optional: true)]));
        var missing = JsonScalarRecordCodec.Decode(schema, Utf8("{}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.MissingField, "$[\"number\"]"), missing.Error);
        var bad = JsonScalarRecordCodec.Decode(schema, Utf8("{\"number\":null}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"number\"]"), bad.Error);
    }
}
