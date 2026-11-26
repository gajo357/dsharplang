using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonScalarMapMappingTests
{
    private static JsonFieldValue Tree(string json) => JsonFieldValueCodec.Decode(Encoding.UTF8.GetBytes(json)).Value;
    private static string Json(JsonFieldValue value) => Encoding.UTF8.GetString(JsonFieldValueCodec.Encode(value));

    [Theory]
    [InlineData((int)JsonRecordFieldKind.Boolean, "{\"a\":true,\"b\":false}")]
    [InlineData((int)JsonRecordFieldKind.Integer, "{\"a\":-42,\"b\":2147483647}")]
    [InlineData((int)JsonRecordFieldKind.String, "{\"a\":\"text\",\"b\":\"\"}")]
    [InlineData((int)JsonRecordFieldKind.ExactNumber, "{\"a\":12345678901234567890.00e+8,\"b\":-0.0}")]
    [InlineData((int)JsonRecordFieldKind.NullableBoolean, "{\"a\":null,\"b\":true}")]
    [InlineData((int)JsonRecordFieldKind.NullableInteger, "{\"a\":null,\"b\":-42}")]
    [InlineData((int)JsonRecordFieldKind.NullableString, "{\"a\":null,\"b\":\"text\"}")]
    [InlineData((int)JsonRecordFieldKind.NullableExactNumber, "{\"a\":null,\"b\":1.00e-20}")]
    public void StandaloneMapsRoundTripThroughJsonAndTree(int kindValue, string json)
    {
        var kind = (JsonRecordFieldKind)kindValue;
        var mapped = JsonTypedFieldMapping.ToScalarMap(kind, Tree(json));
        Assert.True(mapped.IsSuccess);
        Assert.Equal(kind, mapped.Value.Kind);
        Assert.Equal(json, Json(JsonTypedFieldMapping.FromScalarMap(mapped.Value).Value));
        var empty = JsonTypedFieldMapping.ToScalarMap(kind, Tree("{}"));
        Assert.True(empty.IsSuccess);
        Assert.Equal(kind, empty.Value.Kind);
        Assert.Equal("{}", Json(JsonTypedFieldMapping.FromScalarMap(empty.Value).Value));
    }

    [Fact]
    public void SnapshotsRetainValuesAndKinds()
    {
        var entries = new[] { new KeyValuePair<string, object>("a", "old") };
        var map = JsonRecordScalarMap.Create(JsonRecordFieldKind.String, entries);
        var tree = JsonTypedFieldMapping.FromScalarMap(map).Value;
        entries[0] = new("changed", "new");
        var copied = tree.MapItems;
        copied[0] = new("changed", JsonFieldValue.Null());
        Assert.Equal("{\"a\":\"old\"}", Json(tree));
        Assert.Equal("old", JsonTypedFieldMapping.ToScalarMap(JsonRecordFieldKind.String, tree).Value.At(0).Value);
        Assert.Throws<ArgumentException>(() => JsonTypedFieldMapping.ToScalarMap(JsonRecordFieldKind.Record, tree));
    }

    [Fact]
    public void InvalidValuesProduceRedactedIndexedErrors()
    {
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$"), JsonTypedFieldMapping.ToScalarMap(JsonRecordFieldKind.Integer, Tree("[]")).Error);
        var bad = JsonTypedFieldMapping.ToScalarMap(JsonRecordFieldKind.Integer, Tree("{\"first\":1,\"private\":\"secret\"}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[1]"), bad.Error);
        Assert.DoesNotContain("private", bad.Error!.Value.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("secret", bad.Error.Value.ToString(), StringComparison.Ordinal);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[0]"), JsonTypedFieldMapping.ToScalarMap(JsonRecordFieldKind.String, Tree("{\"x\":null}")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidInteger, "$[0]"), JsonTypedFieldMapping.ToScalarMap(JsonRecordFieldKind.Integer, Tree("{\"x\":2147483648}")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidInteger, "$[0]"), JsonTypedFieldMapping.ToScalarMap(JsonRecordFieldKind.Integer, Tree("{\"x\":1.5}")).Error);
    }

    [Fact]
    public void MaximumDepthIncompatibleValueDoesNotHideEarlierErrors()
    {
        var deep = JsonFieldValue.Null();
        for (var i = 0; i < 63; i++) deep = JsonFieldValue.List([deep]);
        var map = JsonFieldValue.Map([new("bad", JsonFieldValue.String("secret")), new("deep", deep)]);
        Assert.Equal(64, map.Depth);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[0]"), JsonTypedFieldMapping.ToScalarMap(JsonRecordFieldKind.Integer, map).Error);
        var onlyDeep = JsonFieldValue.Map([new("deep", deep)]);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[0]"), JsonTypedFieldMapping.ToScalarMap(JsonRecordFieldKind.Integer, onlyDeep).Error);
    }
}
