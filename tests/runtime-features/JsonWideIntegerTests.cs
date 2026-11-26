using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonWideIntegerTests
{
    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);
    private static string Text(byte[] value) => Encoding.UTF8.GetString(value);

    [Theory]
    [InlineData(long.MinValue, "-9223372036854775808")]
    [InlineData(long.MaxValue, "9223372036854775807")]
    [InlineData(9007199254740993L, "9007199254740993")]
    public void SignedRootValuesRoundTripExactly(long value, string json)
    {
        Assert.Equal(json, Text(JsonScalarCodec.EncodeInt64(value)));
        Assert.Equal(value, JsonScalarCodec.DecodeInt64(Bytes(json)).Value);
        Assert.Equal(value, JsonScalarCodec.DecodeNullableInt64(Bytes(json)).Value);
        Assert.Equal(json, Text(JsonScalarCodec.EncodeNullableInt64(value)));
    }

    [Theory]
    [InlineData(0UL, "0")]
    [InlineData(9007199254740993UL, "9007199254740993")]
    [InlineData(ulong.MaxValue, "18446744073709551615")]
    public void UnsignedRootValuesRoundTripExactly(ulong value, string json)
    {
        Assert.Equal(json, Text(JsonScalarCodec.EncodeUInt64(value)));
        Assert.Equal(value, JsonScalarCodec.DecodeUInt64(Bytes(json)).Value);
        Assert.Equal(value, JsonScalarCodec.DecodeNullableUInt64(Bytes(json)).Value);
        Assert.Equal(json, Text(JsonScalarCodec.EncodeNullableUInt64(value)));
    }

    [Fact]
    public void RootErrorsRejectOverflowWrongTypesAndFractionalNumbers()
    {
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidInteger, "$"), JsonScalarCodec.DecodeInt64(Bytes("9223372036854775808")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidInteger, "$"), JsonScalarCodec.DecodeInt64(Bytes("-9223372036854775809")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidInteger, "$"), JsonScalarCodec.DecodeUInt64(Bytes("18446744073709551616")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidInteger, "$"), JsonScalarCodec.DecodeUInt64(Bytes("-1")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidInteger, "$"), JsonScalarCodec.DecodeInt64(Bytes("1.5")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$"), JsonScalarCodec.DecodeUInt64(Bytes("\"secret\"")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidJson, "$"), JsonScalarCodec.DecodeInt64(Bytes("[")).Error);
        Assert.Null(JsonScalarCodec.DecodeNullableInt64(Bytes("null")).Value);
        Assert.Null(JsonScalarCodec.DecodeNullableUInt64(Bytes("null")).Value);
        Assert.Equal("null", Text(JsonScalarCodec.EncodeNullableInt64(null)));
        Assert.Equal("null", Text(JsonScalarCodec.EncodeNullableUInt64(null)));
    }

    [Fact]
    public void WideFieldsRoundTripThroughNestedUnionsAndTreeWithoutRounding()
    {
        var item = new JsonScalarRecordSchema([
            new("signed", JsonRecordFieldKind.Int64), new("unsigned", JsonRecordFieldKind.UInt64),
            new("maybe", JsonRecordFieldKind.NullableInt64, Optional: true),
            new("optional", JsonRecordFieldKind.NullableUInt64, Optional: true)]);
        var union = new JsonTaggedUnionSchema([new("Data", item)]);
        const string json = "{\"case\":\"Data\",\"value\":{\"signed\":-9223372036854775808,\"unsigned\":18446744073709551615,\"maybe\":null}}";
        var decoded = JsonTaggedUnionCodec.Decode(union, Bytes(json));
        Assert.True(decoded.IsSuccess);
        var data = decoded.Value.Payload!;
        Assert.True(data.IsPresent(2));
        Assert.Null(data.ValueAt(2));
        Assert.False(data.IsPresent(3));
        Assert.Equal(json, Text(JsonTaggedUnionCodec.Encode(decoded.Value)));
        var tree = JsonTypedFieldMapping.FromUnion(decoded.Value);
        Assert.True(tree.IsSuccess);
        var restored = JsonTypedFieldMapping.ToUnion(union, tree.Value);
        Assert.True(restored.IsSuccess);
        Assert.Equal(json, Text(JsonTaggedUnionCodec.Encode(restored.Value)));
        const string present = "{\"case\":\"Data\",\"value\":{\"signed\":1,\"unsigned\":2,\"maybe\":-3,\"optional\":18446744073709551615}}";
        var withValues = JsonTaggedUnionCodec.Decode(union, Bytes(present));
        Assert.True(withValues.IsSuccess);
        Assert.True(withValues.Value.Payload!.IsPresent(2));
        Assert.Equal(-3L, withValues.Value.Payload.ValueAt(2));
        Assert.True(withValues.Value.Payload.IsPresent(3));
        Assert.Equal(ulong.MaxValue, withValues.Value.Payload.ValueAt(3));
        Assert.Equal(present, Text(JsonTaggedUnionCodec.Encode(JsonTypedFieldMapping.ToUnion(union, JsonTypedFieldMapping.FromUnion(withValues.Value).Value).Value)));
    }

    [Fact]
    public void RecordFieldsRejectWrongTypesAndKeepErrorsRedacted()
    {
        var schema = new JsonScalarRecordSchema([new("signed", JsonRecordFieldKind.Int64), new("unsigned", JsonRecordFieldKind.UInt64)]);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.MissingField, "$[\"unsigned\"]"), JsonScalarRecordCodec.Decode(schema, Bytes("{\"signed\":1}")).Error);
        var bad = JsonScalarRecordCodec.Decode(schema, Bytes("{\"signed\":1,\"unsigned\":\"secret-value\"}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"unsigned\"]"), bad.Error);
        Assert.DoesNotContain("secret-value", bad.Error!.Value.ToString(), StringComparison.Ordinal);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidInteger, "$[\"unsigned\"]"), JsonScalarRecordCodec.Decode(schema, Bytes("{\"signed\":1,\"unsigned\":18446744073709551616}")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidInteger, "$[\"unsigned\"]"), JsonScalarRecordCodec.Decode(schema, Bytes("{\"signed\":1,\"unsigned\":-1}")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"signed\"]"), JsonScalarRecordCodec.Decode(schema, Bytes("{\"signed\":null,\"unsigned\":1}")).Error);
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(schema, [1, 1UL], [true, true]));
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(schema, [1L, 1L], [true, true]));
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("signed", JsonRecordFieldKind.Int64, Optional: true)]));
    }
}
