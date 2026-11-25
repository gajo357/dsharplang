using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonRecordNullableElementArrayTests
{
    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
    private static JsonScalarRecordSchema Schema() => new([
        new("flags", JsonRecordFieldKind.BooleanNullableElementsArray),
        new("numbers", JsonRecordFieldKind.IntegerNullableElementsArray),
        new("names", JsonRecordFieldKind.StringNullableElementsArray),
        new("optional", JsonRecordFieldKind.NullableStringNullableElementsArray, Optional: true),
        new("nullable", JsonRecordFieldKind.NullableIntegerNullableElementsArray)
    ]);

    [Fact]
    public void NullableElementsRoundTripAndOwnTheirInputs()
    {
        var schema = Schema();
        bool?[] flags = [true, null, false];
        int?[] numbers = [null, int.MinValue, 0, int.MaxValue];
        string?[] names = ["", null, "café 😀"];
        var record = JsonScalarRecord.Create(schema,
            [JsonRecordScalarArray.Create(flags), JsonRecordScalarArray.Create(numbers), JsonRecordScalarArray.CreateNullableStrings(names), null, null],
            [true, true, true, false, true]);
        flags[0] = null; numbers[1] = 10; names[2] = "changed";
        var bytes = JsonScalarRecordCodec.Encode(record);
        var result = JsonScalarRecordCodec.Decode(schema, bytes);
        Assert.True(result.IsSuccess);
        var decoded = result.Value;
        var decodedFlags = Assert.IsType<JsonRecordScalarArray>(decoded.ValueAt(0));
        Assert.Equal(true, decodedFlags.At(0)); Assert.Null(decodedFlags.At(1)); Assert.Equal(false, decodedFlags.At(2));
        var decodedNumbers = Assert.IsType<JsonRecordScalarArray>(decoded.ValueAt(1));
        Assert.Null(decodedNumbers.At(0)); Assert.Equal(int.MinValue, decodedNumbers.At(1)); Assert.Equal(int.MaxValue, decodedNumbers.At(3));
        var decodedNames = Assert.IsType<JsonRecordScalarArray>(decoded.ValueAt(2));
        Assert.Equal("", decodedNames.At(0)); Assert.Null(decodedNames.At(1)); Assert.Equal("café 😀", decodedNames.At(2));
        Assert.False(decoded.IsPresent(3)); Assert.True(decoded.IsPresent(4)); Assert.Null(decoded.ValueAt(4));
        Assert.Equal(bytes, JsonScalarRecordCodec.Encode(decoded));
    }

    [Fact]
    public void MissingNullEmptyAndNullElementsRemainDistinct()
    {
        var schema = Schema();
        var missing = Decode(schema, "{\"flags\":[],\"numbers\":[],\"names\":[],\"nullable\":[]}");
        Assert.False(missing.IsPresent(3)); Assert.Equal(0, Assert.IsType<JsonRecordScalarArray>(missing.ValueAt(4)).Count);
        var nulls = Decode(schema, "{\"flags\":[],\"numbers\":[],\"names\":[],\"optional\":null,\"nullable\":null}");
        Assert.True(nulls.IsPresent(3)); Assert.Null(nulls.ValueAt(3)); Assert.Null(nulls.ValueAt(4));
        var present = Decode(schema, "{\"flags\":[null],\"numbers\":[null],\"names\":[null],\"optional\":[null],\"nullable\":[null]}");
        for (var index = 0; index < 5; index++) Assert.Null(Assert.IsType<JsonRecordScalarArray>(present.ValueAt(index)).At(0));
        var optionalBoolean = new JsonScalarRecordSchema([new("v", JsonRecordFieldKind.NullableBooleanNullableElementsArray, Optional: true)]);
        Assert.True(JsonScalarRecordCodec.Decode(optionalBoolean, Utf8("{\"v\":[true,null]}")).IsSuccess);
        Assert.True(JsonScalarRecordCodec.Decode(optionalBoolean, Utf8("{\"v\":null}")).IsSuccess);
    }

    [Fact]
    public void ArrayElementNullabilityIsAnExactDescriptorProperty()
    {
        var schema = new JsonScalarRecordSchema([new("v", JsonRecordFieldKind.IntegerNullableElementsArray)]);
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(schema, [JsonRecordScalarArray.Create(new[] { 1 })], [true]));
        var nonNullable = new JsonScalarRecordSchema([new("v", JsonRecordFieldKind.IntegerArray)]);
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(nonNullable, [JsonRecordScalarArray.Create(new int?[] { 1 })], [true]));
        Failure(nonNullable, "{\"v\":[null]}", JsonScalarErrorCode.TypeMismatch, "$[\"v\"][0]");
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("v", JsonRecordFieldKind.IntegerNullableElementsArray, Optional: true)]));
        Assert.Throws<ArgumentException>(() => JsonRecordScalarArray.CreateNullableStrings(["\uD800", null]));
    }

    [Fact]
    public void InvalidElementsAndNestedPathsAreRedacted()
    {
        var child = new JsonScalarRecordSchema([new("name\n\"x", JsonRecordFieldKind.StringNullableElementsArray)]);
        var parent = new JsonScalarRecordSchema([new("children", JsonRecordFieldKind.RecordArray, NestedSchema: child)]);
        var result = JsonScalarRecordCodec.Decode(parent, Utf8("{\"children\":[{\"name\\n\\\"x\":[null,\"ok\",{\"secret\":1}]}]}"));
        Assert.False(result.IsSuccess);
        Assert.Equal(JsonScalarErrorCode.TypeMismatch, result.Error!.Value.Code);
        Assert.StartsWith("$[\"children\"][0]", result.Error.Value.Path, StringComparison.Ordinal);
        Assert.EndsWith("[2]", result.Error.Value.Path, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", result.Error.Value.Path, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", result.Error.Value.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("secret", Assert.Throws<InvalidOperationException>(() => result.Value).Message, StringComparison.Ordinal);
        var schema = Schema();
        Failure(schema, "{\"flags\":[null,\"secret\"]}", JsonScalarErrorCode.TypeMismatch, "$[\"flags\"][1]");
        Failure(schema, "{\"flags\":[],\"numbers\":[null,1.5]}", JsonScalarErrorCode.InvalidInteger, "$[\"numbers\"][1]");
        var invalid = JsonScalarRecordCodec.Decode(schema, Utf8("{\"flags\":[],\"numbers\":[],\"names\":[null,\"\\uD800\"]}"));
        Assert.False(invalid.IsSuccess);
        Assert.Equal("$[\"names\"][1]", invalid.Error!.Value.Path);
        Assert.Contains(invalid.Error.Value.Code, new[] { JsonScalarErrorCode.InvalidJson, JsonScalarErrorCode.InvalidUnicode });
    }

    [Fact]
    public void NullableElementArraysRespectTheDepthBound()
    {
        var schema = new JsonScalarRecordSchema([new("v", JsonRecordFieldKind.BooleanNullableElementsArray)]);
        var record = JsonScalarRecord.Create(schema, [JsonRecordScalarArray.Create(new bool?[] { null, true })], [true]);
        Assert.Equal(2, schema.Depth);
        for (var depth = 3; depth <= 64; depth++)
        {
            schema = new JsonScalarRecordSchema([new("child", JsonRecordFieldKind.Record, NestedSchema: schema)]);
            record = JsonScalarRecord.Create(schema, [record], [true]);
        }
        Assert.True(JsonScalarRecordCodec.Decode(schema, JsonScalarRecordCodec.Encode(record)).IsSuccess);
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("child", JsonRecordFieldKind.Record, NestedSchema: schema)]));
    }

    private static JsonScalarRecord Decode(JsonScalarRecordSchema schema, string text)
    {
        var result = JsonScalarRecordCodec.Decode(schema, Utf8(text));
        Assert.True(result.IsSuccess);
        return result.Value;
    }
    private static void Failure(JsonScalarRecordSchema schema, string text, JsonScalarErrorCode code, string path)
    {
        var result = JsonScalarRecordCodec.Decode(schema, Utf8(text));
        Assert.False(result.IsSuccess);
        Assert.Equal(new JsonScalarError(code, path), result.Error);
    }
}
