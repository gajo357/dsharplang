using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonRecordUnionCompositionTests
{
    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
    private static JsonScalarRecordSchema Payload() => new([new("message", JsonRecordFieldKind.String)]);
    private static JsonTaggedUnionSchema Union(JsonScalarRecordSchema payload) => new([new("Idle"), new("Ready", payload)]);
    private static JsonScalarRecordSchema Record(JsonTaggedUnionSchema union) => new([
        new("required", JsonRecordFieldKind.Union, UnionSchema: union),
        new("optional", JsonRecordFieldKind.NullableUnion, Optional: true, UnionSchema: union),
        new("nullable", JsonRecordFieldKind.NullableUnion, UnionSchema: union)
    ]);

    [Fact]
    public void UnionFieldsRoundTripDistinctCasesAndPresence()
    {
        var payload = Payload(); var union = Union(payload); var schema = Record(union);
        var payloadValue = JsonScalarRecord.Create(payload, ["café 😀"], [true]);
        var ready = JsonTaggedUnion.Create(union, 1, payloadValue);
        var unit = JsonTaggedUnion.Create(union, 0);
        var value = JsonScalarRecord.Create(schema, [ready, null, unit], [true, false, true]);
        var bytes = JsonScalarRecordCodec.Encode(value);
        var result = JsonScalarRecordCodec.Decode(schema, bytes);
        Assert.True(result.IsSuccess);
        var restored = result.Value;
        Assert.Same(union, Assert.IsType<JsonTaggedUnion>(restored.ValueAt(0)).Schema);
        Assert.Equal("café 😀", Assert.IsType<JsonTaggedUnion>(restored.ValueAt(0)).Payload!.ValueAt(0));
        Assert.False(restored.IsPresent(1));
        Assert.Equal(0, Assert.IsType<JsonTaggedUnion>(restored.ValueAt(2)).CaseIndex);
        Assert.Equal(bytes, JsonScalarRecordCodec.Encode(restored));
        var explicitNull = JsonScalarRecordCodec.Decode(schema, Utf8("{\"required\":{\"case\":\"Idle\"},\"optional\":null,\"nullable\":null}"));
        Assert.True(explicitNull.IsSuccess);
        Assert.True(explicitNull.Value.IsPresent(1)); Assert.Null(explicitNull.Value.ValueAt(1));
        Assert.True(explicitNull.Value.IsPresent(2)); Assert.Null(explicitNull.Value.ValueAt(2));
    }

    [Fact]
    public void UnionRecordArrayAndUnionPayloadComposeInBothDirections()
    {
        var leaf = Payload(); var nestedUnion = Union(leaf);
        var child = new JsonScalarRecordSchema([new("state", JsonRecordFieldKind.Union, UnionSchema: nestedUnion)]);
        var topPayload = new JsonScalarRecordSchema([new("children", JsonRecordFieldKind.RecordArray, NestedSchema: child)]);
        var top = new JsonTaggedUnionSchema([new("Top", topPayload)]);
        var first = JsonScalarRecord.Create(child, [JsonTaggedUnion.Create(nestedUnion, 0)], [true]);
        var second = JsonScalarRecord.Create(child, [JsonTaggedUnion.Create(nestedUnion, 1, JsonScalarRecord.Create(leaf, ["x"], [true]))], [true]);
        var value = JsonTaggedUnion.Create(top, 0, JsonScalarRecord.Create(topPayload, [JsonRecordArray.Create(child, [first, second])], [true]));
        var result = JsonTaggedUnionCodec.Decode(top, JsonTaggedUnionCodec.Encode(value));
        Assert.True(result.IsSuccess);
        var children = Assert.IsType<JsonRecordArray>(result.Value.Payload!.ValueAt(0));
        Assert.Equal(0, Assert.IsType<JsonTaggedUnion>(children.At(0).ValueAt(0)).CaseIndex);
        Assert.Equal("x", Assert.IsType<JsonTaggedUnion>(children.At(1).ValueAt(0)).Payload!.ValueAt(0));
        var invalid = JsonTaggedUnionCodec.Decode(top, Utf8("{\"case\":\"Top\",\"value\":{\"children\":[{\"state\":{\"case\":\"secret-tag\"}}]}}"));
        Assert.False(invalid.IsSuccess);
        Assert.Equal(JsonScalarErrorCode.UnknownCase, invalid.Error!.Value.Code);
        Assert.Equal("$[\"value\"][\"children\"][0][\"state\"][\"case\"]", invalid.Error.Value.Path);
        Assert.DoesNotContain("secret", invalid.Error.Value.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void UnionFieldsRequireExactDescriptorsAndSupportedPresence()
    {
        var leaf = Payload(); var first = Union(leaf); var second = Union(leaf); var schema = Record(first);
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(schema, [JsonTaggedUnion.Create(second, 0), null, null], [true, false, true]));
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(schema, [null, null, null], [true, false, true]));
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("x", JsonRecordFieldKind.Union)]));
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("x", JsonRecordFieldKind.Union, Optional: true, UnionSchema: first)]));
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("x", JsonRecordFieldKind.Integer, UnionSchema: first)]));
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("x", JsonRecordFieldKind.Union, NestedSchema: leaf, UnionSchema: first)]));
    }

    [Fact]
    public void NestedFailuresRetainEscapedSchemaPathsAndRedactInput()
    {
        var leaf = new JsonScalarRecordSchema([new("line\n\"x", JsonRecordFieldKind.String)]);
        var schema = new JsonScalarRecordSchema([new("state\n\"x", JsonRecordFieldKind.Union, UnionSchema: Union(leaf))]);
        var result = JsonScalarRecordCodec.Decode(schema, Utf8("{\"state\\n\\\"x\":{\"case\":\"Ready\",\"value\":{\"line\\n\\\"x\":{\"secret\":1}}}}"));
        Assert.False(result.IsSuccess);
        Assert.Equal(JsonScalarErrorCode.TypeMismatch, result.Error!.Value.Code);
        Assert.Contains("[\"value\"]", result.Error.Value.Path, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", result.Error.Value.Path, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", result.Error.Value.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("secret", Assert.Throws<InvalidOperationException>(() => result.Value).Message, StringComparison.Ordinal);
        var duplicate = JsonScalarRecordCodec.Decode(schema, Utf8("{\"state\\n\\\"x\":{\"case\":\"Idle\",\"case\":\"Idle\"}}"));
        Assert.False(duplicate.IsSuccess);
        Assert.Equal(JsonScalarErrorCode.DuplicateField, duplicate.Error!.Value.Code);
        Assert.EndsWith("[\"case\"]", duplicate.Error.Value.Path, StringComparison.Ordinal);
    }

    [Fact]
    public void CombinedDescriptorDepthIsBoundedAndRoundTripsAtLimit()
    {
        var schema = new JsonScalarRecordSchema([]);
        var record = JsonScalarRecord.Create(schema, [], []);
        for (var depth = 2; depth <= 62; depth++)
        {
            schema = new JsonScalarRecordSchema([new("child", JsonRecordFieldKind.Record, NestedSchema: schema)]);
            record = JsonScalarRecord.Create(schema, [record], [true]);
        }
        var union = new JsonTaggedUnionSchema([new("Deep", schema)]);
        Assert.Equal(63, union.Depth);
        var wrapper = new JsonScalarRecordSchema([new("state", JsonRecordFieldKind.Union, UnionSchema: union)]);
        Assert.Equal(64, wrapper.Depth);
        var value = JsonScalarRecord.Create(wrapper, [JsonTaggedUnion.Create(union, 0, record)], [true]);
        Assert.True(JsonScalarRecordCodec.Decode(wrapper, JsonScalarRecordCodec.Encode(value)).IsSuccess);
        Assert.Throws<ArgumentException>(() => new JsonTaggedUnionSchema([new("Deep", wrapper)]));
        var almostDeepest = new JsonScalarRecordSchema([new("child", JsonRecordFieldKind.Record, NestedSchema: schema)]);
        var tooDeepUnion = new JsonTaggedUnionSchema([new("Deep", almostDeepest)]);
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("state", JsonRecordFieldKind.Union, UnionSchema: tooDeepUnion)]));
    }
}
