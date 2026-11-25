using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonRecordArrayCodecTests
{
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);
    private static JsonScalarRecordSchema Child() => new([new("name", JsonRecordFieldKind.String)]);
    private static JsonScalarRecordSchema Parent(JsonScalarRecordSchema child) => new([
        new("items", JsonRecordFieldKind.RecordArray, NestedSchema: child),
        new("optional", JsonRecordFieldKind.NullableRecordArray, Optional: true, NestedSchema: child),
        new("requiredNullable", JsonRecordFieldKind.NullableRecordArray, NestedSchema: child)
    ]);
    private static JsonScalarRecord Item(JsonScalarRecordSchema schema, string name) => JsonScalarRecord.Create(schema, [name], [true]);

    [Fact]
    public void RecordArraysRoundTripAndOwnTheirSnapshots()
    {
        var child = Child();
        var schema = Parent(child);
        var first = Item(child, "café");
        var source = new[] { first, Item(child, "two") };
        var array = JsonRecordArray.Create(child, source);
        source[0] = Item(child, "replacement");
        var original = JsonScalarRecord.Create(schema, [array, null, JsonRecordArray.Create(child, [])], [true, false, true]);
        var bytes = JsonScalarRecordCodec.Encode(original);
        var decoded = JsonScalarRecordCodec.Decode(schema, bytes);
        Assert.True(decoded.IsSuccess);
        var result = decoded.Value;
        var items = Assert.IsType<JsonRecordArray>(result.ValueAt(0));
        Assert.Equal(2, items.Count);
        Assert.Equal("café", items.At(0).ValueAt(0));
        Assert.Equal("two", items.At(1).ValueAt(0));
        Assert.False(result.IsPresent(1));
        Assert.Equal(0, Assert.IsType<JsonRecordArray>(result.ValueAt(2)).Count);
        Assert.Equal(bytes, JsonScalarRecordCodec.Encode(result));
        var explicitNull = JsonScalarRecordCodec.Decode(schema, Utf8("{\"items\":[],\"optional\":null,\"requiredNullable\":null}"));
        Assert.True(explicitNull.IsSuccess);
        Assert.True(explicitNull.Value.IsPresent(1));
        Assert.Null(explicitNull.Value.ValueAt(1));
        var populated = JsonScalarRecordCodec.Decode(schema, Utf8("{\"items\":[],\"optional\":[{\"name\":\"a\"}],\"requiredNullable\":null}"));
        Assert.True(populated.IsSuccess);
        Assert.Equal("a", Assert.IsType<JsonRecordArray>(populated.Value.ValueAt(1)).At(0).ValueAt(0));
    }

    [Fact]
    public void ArrayAndChildErrorsHaveRedactedIndexedPaths()
    {
        var schema = Parent(Child());
        Failure(schema, "{\"items\":null}", JsonScalarErrorCode.TypeMismatch, "$[\"items\"]");
        Failure(schema, "{\"items\":{}}", JsonScalarErrorCode.TypeMismatch, "$[\"items\"]");
        Failure(schema, "{\"items\":[null]}", JsonScalarErrorCode.TypeMismatch, "$[\"items\"][0]");
        Failure(schema, "{\"items\":[{\"name\":\"ok\"},{\"name\":123}]}", JsonScalarErrorCode.TypeMismatch, "$[\"items\"][1][\"name\"]");
        Failure(schema, "{\"items\":[{}]}", JsonScalarErrorCode.MissingField, "$[\"items\"][0][\"name\"]");
        Failure(schema, "{\"items\":[{\"name\":\"a\",\"name\":\"b\"}]}", JsonScalarErrorCode.DuplicateField, "$[\"items\"][0][\"name\"]");
        Failure(schema, "{\"items\":[{\"secret-name\":\"secret-value\"}]}", JsonScalarErrorCode.UnknownField, "$[\"items\"][0]");
        Failure(schema, "{\"items\":[", JsonScalarErrorCode.InvalidJson, "$");
        var result = JsonScalarRecordCodec.Decode(schema, Utf8("{\"items\":[{\"name\":{\"secret\":1}}]}"));
        Assert.DoesNotContain("secret", result.Error!.Value.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("secret", Assert.Throws<InvalidOperationException>(() => result.Value).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SnapshotsRejectWrongDescriptorsNullElementsAndForeignArrays()
    {
        var child = Child();
        var other = Child();
        var schema = Parent(child);
        Assert.Throws<ArgumentException>(() => JsonRecordArray.Create(child, [Item(other, "x")]));
        Assert.Throws<ArgumentException>(() => JsonRecordArray.Create(child, [null!]));
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(schema, [JsonRecordArray.Create(other, []), null, null], [true, false, true]));
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(schema, [new[] { Item(child, "x") }, null, null], [true, false, true]));
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("a", JsonRecordFieldKind.RecordArray)]));
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("a", JsonRecordFieldKind.RecordArray, Optional: true, NestedSchema: child)]));
    }

    [Fact]
    public void ArrayAndObjectBothCountTowardDepthLimit()
    {
        var child = new JsonScalarRecordSchema([]);
        var record = JsonScalarRecord.Create(child, [], []);
        for (var depth = 2; depth <= 62; depth++)
        {
            child = new JsonScalarRecordSchema([new("child", JsonRecordFieldKind.Record, NestedSchema: child)]);
            record = JsonScalarRecord.Create(child, [record], [true]);
        }
        var parent = new JsonScalarRecordSchema([new("items", JsonRecordFieldKind.RecordArray, NestedSchema: child)]);
        var snapshot = JsonScalarRecord.Create(parent, [JsonRecordArray.Create(child, [record])], [true]);
        Assert.Equal(64, parent.Depth);
        Assert.True(JsonScalarRecordCodec.Decode(parent, JsonScalarRecordCodec.Encode(snapshot)).IsSuccess);
        var tooDeep = new JsonScalarRecordSchema([new("child", JsonRecordFieldKind.Record, NestedSchema: child)]);
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("items", JsonRecordFieldKind.RecordArray, NestedSchema: tooDeep)]));
    }

    [Fact]
    public void EscapedArrayAndChildNamesRemainSafeInErrorPaths()
    {
        var child = new JsonScalarRecordSchema([new("name\n\"x", JsonRecordFieldKind.String)]);
        var schema = new JsonScalarRecordSchema([new("items\n\"x", JsonRecordFieldKind.RecordArray, NestedSchema: child)]);
        var bytes = JsonScalarRecordCodec.Encode(JsonScalarRecord.Create(schema, [JsonRecordArray.Create(child, [Item(child, "ok")])], [true]));
        Assert.True(JsonScalarRecordCodec.Decode(schema, bytes).IsSuccess);
        var result = JsonScalarRecordCodec.Decode(schema, Utf8("{\"items\\n\\\"x\":[{}]}"));
        Assert.False(result.IsSuccess);
        Assert.Equal(JsonScalarErrorCode.MissingField, result.Error!.Value.Code);
        Assert.Contains("[0]", result.Error.Value.Path, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", result.Error.Value.Path, StringComparison.Ordinal);
    }

    private static void Failure(JsonScalarRecordSchema schema, string input, JsonScalarErrorCode code, string path)
    {
        var result = JsonScalarRecordCodec.Decode(schema, Utf8(input));
        Assert.False(result.IsSuccess);
        Assert.Equal(new JsonScalarError(code, path), result.Error);
    }
}
