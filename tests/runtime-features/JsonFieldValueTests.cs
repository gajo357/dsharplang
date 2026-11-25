using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonFieldValueTests
{
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);
    private static string Encode(JsonFieldValue value) => Encoding.UTF8.GetString(JsonFieldValueCodec.Encode(value));
    private static KeyValuePair<string, JsonFieldValue> Pair(string key, JsonFieldValue value) => new(key, value);

    [Fact]
    public void HeterogeneousNestedCollectionsPreserveNumbersAndNull()
    {
        const string json = "{\"secret-key\":[true,null,-12345678901234567890.000e+12,{\"a\":\"value\",\"empty\":[],\"object\":{}}],\"no\":false}";
        var decoded = JsonFieldValueCodec.Decode(Utf8(json));
        Assert.True(decoded.IsSuccess);
        Assert.Equal(json, Encode(decoded.Value));
        Assert.Equal(JsonFieldValueKind.Map, decoded.Value.Kind);
        Assert.Equal(4, decoded.Value.Depth);
    }

    [Fact]
    public void ContainersOwnInputAndReturnedArraysAndPreserveOrder()
    {
        var children = new[] { JsonFieldValue.String("original") };
        var list = JsonFieldValue.List(children);
        var pairs = new[] { Pair("first", list), Pair("second", JsonFieldValue.Null()) };
        var map = JsonFieldValue.Map(pairs);
        children[0] = JsonFieldValue.Boolean(false);
        pairs[0] = Pair("changed", JsonFieldValue.Null());
        list.ListItems[0] = JsonFieldValue.Null();
        map.MapItems[0] = Pair("changed", JsonFieldValue.Null());
        Assert.Throws<InvalidOperationException>(() => map.Scalar);
        Assert.Equal("{\"first\":[\"original\"],\"second\":null}", Encode(map));
    }

    [Fact]
    public void RejectsInvalidConstructionWithoutUserCallbacks()
    {
        Assert.Throws<ArgumentException>(() => JsonFieldValue.String("\ud800"));
        Assert.Throws<ArgumentException>(() => JsonFieldValue.List([null!]));
        Assert.Throws<ArgumentException>(() => JsonFieldValue.Map([Pair("a", JsonFieldValue.Null()), Pair("a", JsonFieldValue.Null())]));
        Assert.Throws<ArgumentException>(() => JsonFieldValue.Map([Pair("\ud800", JsonFieldValue.Null())]));
        Assert.Throws<ArgumentException>(() => JsonFieldValue.Map([Pair("a", null!)]));
        Assert.Throws<ArgumentNullException>(() => JsonFieldValue.Number(null!));
    }

    [Fact]
    public void FailuresUseIndexesAndNeverEchoKeysOrValues()
    {
        var duplicate = JsonFieldValueCodec.Decode(Utf8("{\"secret\":1,\"\\u0073ecret\":2}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.DuplicateField, "$[1]"), duplicate.Error);
        var invalidString = JsonFieldValueCodec.Decode(Utf8("{\"private\":[{\"hidden\":\"\\uD800\"}]}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidUnicode, "$[0][0][0]"), invalidString.Error);
        var invalidKey = JsonFieldValueCodec.Decode(Utf8("{\"private\":{\"\\uD800\":\"secret\"}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidUnicode, "$[0][0]"), invalidKey.Error);
        var malformed = JsonFieldValueCodec.Decode(Utf8("{\"private\": secret}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidJson, "$"), malformed.Error);
        foreach (var error in new[] { duplicate.Error, invalidString.Error, invalidKey.Error, malformed.Error })
        {
            Assert.DoesNotContain("secret", error!.Value.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("private", error.Value.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("hidden", error.Value.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CombinedDepthIsBoundedAt64Containers()
    {
        var value = JsonFieldValue.Null();
        for (var index = 0; index < 64; index++)
            value = index % 2 == 0 ? JsonFieldValue.List([value]) : JsonFieldValue.Map([Pair("k", value)]);
        Assert.Equal(64, value.Depth);
        var encoded = JsonFieldValueCodec.Encode(value);
        Assert.True(JsonFieldValueCodec.Decode(encoded).IsSuccess);
        Assert.Throws<ArgumentException>(() => JsonFieldValue.List([value]));
        Assert.Throws<ArgumentException>(() => JsonFieldValue.Map([Pair("k", value)]));
        Assert.Equal(JsonScalarErrorCode.InvalidJson, JsonFieldValueCodec.Decode(Utf8("[" + Encoding.UTF8.GetString(encoded) + "]")).Error!.Value.Code);
    }
}
