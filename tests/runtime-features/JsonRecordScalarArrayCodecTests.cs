using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonRecordScalarArrayCodecTests
{
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);
    private static JsonScalarRecordSchema Schema() => new([
        new("flags", JsonRecordFieldKind.BooleanArray),
        new("numbers", JsonRecordFieldKind.IntegerArray),
        new("names", JsonRecordFieldKind.StringArray),
        new("optional", JsonRecordFieldKind.NullableStringArray, Optional: true),
        new("nullable", JsonRecordFieldKind.NullableIntegerArray)
    ]);

    [Fact]
    public void TypedArraysRoundTripWithIndependentSnapshots()
    {
        var schema = Schema();
        var flags = new[] { true, false };
        var numbers = new[] { int.MinValue, 0, int.MaxValue };
        var names = new[] { "", "café 😀" };
        var record = JsonScalarRecord.Create(schema, [JsonRecordScalarArray.Create(flags), JsonRecordScalarArray.Create(numbers), JsonRecordScalarArray.Create(names), null, null], [true, true, true, false, true]);
        flags[0] = false; numbers[0] = 42; names[1] = "changed";
        var bytes = JsonScalarRecordCodec.Encode(record);
        var decoded = JsonScalarRecordCodec.Decode(schema, bytes);
        Assert.True(decoded.IsSuccess);
        var result = decoded.Value;
        Assert.Equal(true, Assert.IsType<JsonRecordScalarArray>(result.ValueAt(0)).At(0));
        Assert.Equal(int.MinValue, Assert.IsType<JsonRecordScalarArray>(result.ValueAt(1)).At(0));
        Assert.Equal("café 😀", Assert.IsType<JsonRecordScalarArray>(result.ValueAt(2)).At(1));
        Assert.False(result.IsPresent(3));
        Assert.True(result.IsPresent(4));
        Assert.Null(result.ValueAt(4));
        Assert.Equal(bytes, JsonScalarRecordCodec.Encode(result));
    }

    [Fact]
    public void NullableArrayPresenceAndEmptyArraysRemainDistinct()
    {
        var schema = Schema();
        var omitted = JsonScalarRecordCodec.Decode(schema, Utf8("{\"flags\":[],\"numbers\":[],\"names\":[],\"nullable\":[]}"));
        Assert.True(omitted.IsSuccess);
        Assert.False(omitted.Value.IsPresent(3));
        Assert.Equal(0, Assert.IsType<JsonRecordScalarArray>(omitted.Value.ValueAt(4)).Count);
        var explicitNull = JsonScalarRecordCodec.Decode(schema, Utf8("{\"flags\":[],\"numbers\":[],\"names\":[],\"optional\":null,\"nullable\":null}"));
        Assert.True(explicitNull.IsSuccess);
        Assert.True(explicitNull.Value.IsPresent(3));
        Assert.Null(explicitNull.Value.ValueAt(3));
        var present = JsonScalarRecordCodec.Decode(schema, Utf8("{\"flags\":[],\"numbers\":[],\"names\":[],\"optional\":[\"ok\"],\"nullable\":[1]}"));
        Assert.True(present.IsSuccess);
        Assert.Equal("ok", Assert.IsType<JsonRecordScalarArray>(present.Value.ValueAt(3)).At(0));
        Assert.Equal(1, Assert.IsType<JsonRecordScalarArray>(present.Value.ValueAt(4)).At(0));
        var nullableBool = new JsonScalarRecordSchema([new("b", JsonRecordFieldKind.NullableBooleanArray, Optional: true)]);
        Assert.True(JsonScalarRecordCodec.Decode(nullableBool, Utf8("{\"b\":[true]}" )).IsSuccess);
        Assert.True(JsonScalarRecordCodec.Decode(nullableBool, Utf8("{\"b\":null}" )).IsSuccess);
    }

    [Fact]
    public void InvalidElementsUseRedactedIndexedPaths()
    {
        var schema = Schema();
        Failure(schema, "{\"flags\":null}", JsonScalarErrorCode.TypeMismatch, "$[\"flags\"]");
        Failure(schema, "{\"flags\":{},\"numbers\":[]}", JsonScalarErrorCode.TypeMismatch, "$[\"flags\"]");
        Failure(schema, "{\"flags\":[true,null]}", JsonScalarErrorCode.TypeMismatch, "$[\"flags\"][1]");
        Failure(schema, "{\"flags\":[],\"numbers\":[2,1.5]}", JsonScalarErrorCode.InvalidInteger, "$[\"numbers\"][1]");
        Failure(schema, "{\"flags\":[],\"numbers\":[],\"names\":[null]}", JsonScalarErrorCode.TypeMismatch, "$[\"names\"][0]");
        var bad = JsonScalarRecordCodec.Decode(schema, Utf8("{\"flags\":[\"secret-value\"]}"));
        Assert.False(bad.IsSuccess);
        Assert.DoesNotContain("secret-value", bad.Error!.Value.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value", Assert.Throws<InvalidOperationException>(() => bad.Value).Message, StringComparison.Ordinal);
        var escaped = new JsonScalarRecordSchema([new("x\n\"q", JsonRecordFieldKind.StringArray)]);
        var invalid = JsonScalarRecordCodec.Decode(escaped, Utf8("{\"x\\n\\\"q\":[\"\\uD800\"]}"));
        Assert.False(invalid.IsSuccess);
        Assert.Contains("[0]", invalid.Error!.Value.Path, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", invalid.Error.Value.Path, StringComparison.Ordinal);
        Assert.Contains(invalid.Error.Value.Code, new[] { JsonScalarErrorCode.InvalidJson, JsonScalarErrorCode.InvalidUnicode });
    }

    [Fact]
    public void ConstructionRejectsForeignAndInvalidValues()
    {
        var schema = Schema();
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(schema, [JsonRecordScalarArray.Create(new[] { 1 }), null, null, null, null], [true, true, true, false, true]));
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(schema, [new[] { true }, null, null, null, null], [true, true, true, false, true]));
        Assert.Throws<ArgumentException>(() => JsonRecordScalarArray.Create(new string[] { null! }));
        Assert.Throws<ArgumentException>(() => JsonRecordScalarArray.Create(new[] { "\uD800" }));
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("a", JsonRecordFieldKind.IntegerArray, Optional: true)]));
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("a", JsonRecordFieldKind.IntegerArray, NestedSchema: schema)]));
    }

    [Fact]
    public void ScalarArrayAddsOneJsonDepthLevel()
    {
        var schema = new JsonScalarRecordSchema([new("values", JsonRecordFieldKind.IntegerArray)]);
        var record = JsonScalarRecord.Create(schema, [JsonRecordScalarArray.Create(new[] { 1 })], [true]);
        Assert.Equal(2, schema.Depth);
        for (var depth = 3; depth <= 64; depth++)
        {
            schema = new JsonScalarRecordSchema([new("child", JsonRecordFieldKind.Record, NestedSchema: schema)]);
            record = JsonScalarRecord.Create(schema, [record], [true]);
        }
        Assert.True(JsonScalarRecordCodec.Decode(schema, JsonScalarRecordCodec.Encode(record)).IsSuccess);
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("child", JsonRecordFieldKind.Record, NestedSchema: schema)]));
    }

    private static void Failure(JsonScalarRecordSchema schema, string input, JsonScalarErrorCode code, string path)
    {
        var result = JsonScalarRecordCodec.Decode(schema, Utf8(input));
        Assert.False(result.IsSuccess);
        Assert.Equal(new JsonScalarError(code, path), result.Error);
    }
}
