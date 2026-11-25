using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonNestedRecordCodecTests
{
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    private static JsonScalarRecordSchema Child() => new([
        new("value", JsonRecordFieldKind.Integer),
        new("note", JsonRecordFieldKind.NullableString, Optional: true)
    ]);

    private static JsonScalarRecordSchema Parent(JsonScalarRecordSchema child) => new([
        new("child", JsonRecordFieldKind.Record, NestedSchema: child),
        new("optional", JsonRecordFieldKind.NullableRecord, Optional: true, NestedSchema: child),
        new("requiredNullable", JsonRecordFieldKind.NullableRecord, NestedSchema: child)
    ]);

    [Fact]
    public void NestedRecordsRoundTripAndPreserveOptionalPresence()
    {
        var childSchema = Child();
        var schema = Parent(childSchema);
        var childValues = new object?[] { 42, "café 😀" };
        var child = JsonScalarRecord.Create(childSchema, childValues, [true, true]);
        var values = new object?[] { child, null, null };
        var present = new[] { true, false, true };
        var original = JsonScalarRecord.Create(schema, values, present);
        childValues[0] = 99;
        values[0] = null;
        present[2] = false;

        var bytes = JsonScalarRecordCodec.Encode(original);
        Assert.DoesNotContain("\"optional\"", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
        var decoded = JsonScalarRecordCodec.Decode(schema, bytes);
        Assert.True(decoded.IsSuccess);
        var result = decoded.Value;
        var nested = Assert.IsType<JsonScalarRecord>(result.ValueAt(0));
        Assert.Equal(42, nested.ValueAt(0));
        Assert.Equal("café 😀", nested.ValueAt(1));
        Assert.True(nested.IsPresent(1));
        Assert.False(result.IsPresent(1));
        Assert.Null(result.ValueAt(1));
        Assert.True(result.IsPresent(2));
        Assert.Null(result.ValueAt(2));
        Assert.Equal(Encoding.UTF8.GetString(bytes), Encoding.UTF8.GetString(JsonScalarRecordCodec.Encode(result)));

        var explicitNull = JsonScalarRecordCodec.Decode(schema, Utf8("{\"child\":{\"value\":1},\"optional\":null,\"requiredNullable\":null}"));
        Assert.True(explicitNull.IsSuccess);
        Assert.True(explicitNull.Value.IsPresent(1));
        Assert.Null(explicitNull.Value.ValueAt(1));
        var omittedChildNote = Assert.IsType<JsonScalarRecord>(explicitNull.Value.ValueAt(0));
        Assert.False(omittedChildNote.IsPresent(1));
        var nestedValues = JsonScalarRecordCodec.Decode(schema, Utf8("{\"child\":{\"value\":1},\"optional\":{\"value\":2},\"requiredNullable\":{\"value\":3}}"));
        Assert.True(nestedValues.IsSuccess);
        Assert.Equal(2, Assert.IsType<JsonScalarRecord>(nestedValues.Value.ValueAt(1)).ValueAt(0));
        Assert.Equal(3, Assert.IsType<JsonScalarRecord>(nestedValues.Value.ValueAt(2)).ValueAt(0));
        Assert.True(JsonScalarRecordCodec.Decode(schema, JsonScalarRecordCodec.Encode(nestedValues.Value)).IsSuccess);
    }

    [Fact]
    public void NestedFailuresUseEscapedDeclaredPathsWithoutInputValues()
    {
        var schema = Parent(Child());
        Failure(schema, "{\"child\":{\"value\":\"secret-value\"}}", JsonScalarErrorCode.TypeMismatch, "$[\"child\"][\"value\"]");
        Failure(schema, "{\"child\":{}}", JsonScalarErrorCode.MissingField, "$[\"child\"][\"value\"]");
        Failure(schema, "{\"child\":{\"value\":1,\"value\":2}}", JsonScalarErrorCode.DuplicateField, "$[\"child\"][\"value\"]");
        Failure(schema, "{\"child\":{\"secret-name\":1}}", JsonScalarErrorCode.UnknownField, "$[\"child\"]");
        Failure(schema, "{\"child\":null}", JsonScalarErrorCode.TypeMismatch, "$[\"child\"]");
        Failure(schema, "{\"child\":[]}", JsonScalarErrorCode.TypeMismatch, "$[\"child\"]");
        Failure(schema, "{\"child\":{\"value\":1},\"requiredNullable\":{\"value\":1.5}}",
            JsonScalarErrorCode.InvalidInteger, "$[\"requiredNullable\"][\"value\"]");
        var invalidValue = JsonScalarRecordCodec.Decode(schema, Utf8("{\"child\":{\"value\":1,\"note\":\"\\uD800\"}}"));
        Assert.False(invalidValue.IsSuccess);
        Assert.Equal("$[\"child\"][\"note\"]", invalidValue.Error!.Value.Path);
        Assert.Contains(invalidValue.Error.Value.Code, new[] { JsonScalarErrorCode.InvalidJson, JsonScalarErrorCode.InvalidUnicode });
        var result = JsonScalarRecordCodec.Decode(schema, Utf8("{\"child\":{\"value\":\"secret-value\"}}"));
        Assert.DoesNotContain("secret-value", result.Error!.Value.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value", Assert.Throws<InvalidOperationException>(() => result.Value).Message, StringComparison.Ordinal);
        Failure(schema, "{\"child\":{\"value\":1", JsonScalarErrorCode.InvalidJson, "$");
    }

    [Fact]
    public void ChildDescriptorsAndValuesMustMatchExactly()
    {
        var first = Child();
        var second = Child();
        var parent = Parent(first);
        var foreignChild = JsonScalarRecord.Create(second, [1, null], [true, false]);
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(parent, [foreignChild, null, null], [true, false, true]));
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(parent, [null, null, null], [true, false, true]));
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("child", JsonRecordFieldKind.Record)]));
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("child", JsonRecordFieldKind.Integer, NestedSchema: first)]));
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("child", JsonRecordFieldKind.Record, Optional: true, NestedSchema: first)]));
    }

    [Fact]
    public void DescriptorDepthIsBoundedAndLargestSupportedRecordRoundTrips()
    {
        var schema = new JsonScalarRecordSchema([]);
        var record = JsonScalarRecord.Create(schema, [], []);
        for (var depth = 2; depth <= 64; depth++)
        {
            schema = new JsonScalarRecordSchema([new("child", JsonRecordFieldKind.Record, NestedSchema: schema)]);
            record = JsonScalarRecord.Create(schema, [record], [true]);
        }
        Assert.Equal(64, schema.Depth);
        Assert.True(JsonScalarRecordCodec.Decode(schema, JsonScalarRecordCodec.Encode(record)).IsSuccess);
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("child", JsonRecordFieldKind.Record, NestedSchema: schema)]));
    }

    [Fact]
    public void EscapedNamesAndInvalidUnicodeStayWithinTheNestedPath()
    {
        var child = new JsonScalarRecordSchema([new("line\n\"name", JsonRecordFieldKind.String)]);
        var schema = new JsonScalarRecordSchema([new("outer", JsonRecordFieldKind.Record, NestedSchema: child)]);
        var missing = JsonScalarRecordCodec.Decode(schema, Utf8("{\"outer\":{}}"));
        Assert.False(missing.IsSuccess);
        Assert.Equal(JsonScalarErrorCode.MissingField, missing.Error!.Value.Code);
        Assert.StartsWith("$[\"outer\"][\"", missing.Error.Value.Path, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", missing.Error.Value.Path, StringComparison.Ordinal);
        var invalid = JsonScalarRecordCodec.Decode(schema, Utf8("{\"outer\":{\"\\uD800\":1}}"));
        Assert.False(invalid.IsSuccess);
        Assert.Equal("$[\"outer\"]", invalid.Error!.Value.Path);
        Assert.Contains(invalid.Error.Value.Code, new[] { JsonScalarErrorCode.InvalidJson, JsonScalarErrorCode.InvalidUnicode });
    }

    private static void Failure(JsonScalarRecordSchema schema, string input, JsonScalarErrorCode code, string path)
    {
        var result = JsonScalarRecordCodec.Decode(schema, Utf8(input));
        Assert.False(result.IsSuccess);
        Assert.Equal(new JsonScalarError(code, path), result.Error);
    }
}
