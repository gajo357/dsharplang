using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonScalarRecordCodecTests
{
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    private static JsonScalarRecordSchema Schema() => new([
        new("enabled", JsonRecordFieldKind.Boolean),
        new("count", JsonRecordFieldKind.Integer),
        new("title", JsonRecordFieldKind.String),
        new("flag", JsonRecordFieldKind.NullableBoolean, Optional: true),
        new("maybeCount", JsonRecordFieldKind.NullableInteger, Optional: true),
        new("note", JsonRecordFieldKind.NullableString, Optional: true),
        new("requiredNullable", JsonRecordFieldKind.NullableString)
    ]);

    [Fact]
    public void ScalarRecordRoundTripsAndPreservesMissingVersusExplicitNull()
    {
        var schema = Schema();
        var values = new object?[] { true, int.MinValue, "café 😀", null, 0, null, null };
        var present = new[] { true, true, true, false, true, true, true };
        var original = JsonScalarRecord.Create(schema, values, present);
        values[0] = false;
        present[1] = false;
        var json = JsonScalarRecordCodec.Encode(original);
        Assert.DoesNotContain("\"flag\"", Encoding.UTF8.GetString(json), StringComparison.Ordinal);
        Assert.Contains("\"note\":null", Encoding.UTF8.GetString(json), StringComparison.Ordinal);
        var decoded = JsonScalarRecordCodec.Decode(schema, json);
        Assert.True(decoded.IsSuccess);
        var record = decoded.Value;
        Assert.Equal(true, record.ValueAt(0));
        Assert.Equal(int.MinValue, record.ValueAt(1));
        Assert.Equal("café 😀", record.ValueAt(2));
        Assert.False(record.IsPresent(3));
        Assert.Null(record.ValueAt(3));
        Assert.True(record.IsPresent(4));
        Assert.Equal(0, record.ValueAt(4));
        Assert.True(record.IsPresent(5));
        Assert.Null(record.ValueAt(5));
        Assert.True(record.IsPresent(6));
        Assert.Null(record.ValueAt(6));
        Assert.Equal(Encoding.UTF8.GetString(json), Encoding.UTF8.GetString(JsonScalarRecordCodec.Encode(record)));

        var empty = new JsonScalarRecordSchema([]);
        Assert.True(JsonScalarRecordCodec.Decode(empty, Utf8("{}" )).IsSuccess);
        Assert.Equal("{}", Encoding.UTF8.GetString(JsonScalarRecordCodec.Encode(JsonScalarRecord.Create(empty, [], []))));
    }

    [Fact]
    public void MissingRequiredAndInvalidValuesReportDeclaredPaths()
    {
        var schema = Schema();
        Failure(JsonScalarRecordCodec.Decode(schema, Utf8("{\"enabled\":true,\"title\":\"x\",\"requiredNullable\":null}")),
            JsonScalarErrorCode.MissingField, "$[\"count\"]");
        Failure(JsonScalarRecordCodec.Decode(schema, Utf8("{\"enabled\":true,\"count\":1,\"title\":\"x\"}")),
            JsonScalarErrorCode.MissingField, "$[\"requiredNullable\"]");
        Failure(JsonScalarRecordCodec.Decode(schema, Utf8("{\"enabled\":null}")),
            JsonScalarErrorCode.TypeMismatch, "$[\"enabled\"]");
        Failure(JsonScalarRecordCodec.Decode(schema, Utf8("{\"count\":2147483648}")),
            JsonScalarErrorCode.InvalidInteger, "$[\"count\"]");
        Failure(JsonScalarRecordCodec.Decode(schema, Utf8("{\"maybeCount\":1.5}")),
            JsonScalarErrorCode.InvalidInteger, "$[\"maybeCount\"]");
        var invalidUnicode = JsonScalarRecordCodec.Decode(schema, Utf8("{\"title\":\"\\uD800\"}"));
        Assert.False(invalidUnicode.IsSuccess);
        Assert.Equal("$[\"title\"]", invalidUnicode.Error!.Value.Path);
        Assert.Contains(invalidUnicode.Error.Value.Code, new[] { JsonScalarErrorCode.InvalidJson, JsonScalarErrorCode.InvalidUnicode });
        var invalidUtf8 = JsonScalarRecordCodec.Decode(schema,
            new byte[] { 0x7b, 0x22, 0x74, 0x69, 0x74, 0x6c, 0x65, 0x22, 0x3a, 0x22, 0xff, 0x22, 0x7d });
        Assert.False(invalidUtf8.IsSuccess);
        Assert.Equal("$[\"title\"]", invalidUtf8.Error!.Value.Path);
        Assert.Contains(invalidUtf8.Error.Value.Code, new[] { JsonScalarErrorCode.InvalidJson, JsonScalarErrorCode.InvalidUnicode });
    }

    [Fact]
    public void UnknownDuplicateAndCaseMismatchedFieldsAreRejectedWithoutEchoingInput()
    {
        var schema = Schema();
        const string secret = "secret-value-37";
        var unknown = JsonScalarRecordCodec.Decode(schema, Utf8($"{{\"{secret}\":7}}"));
        Failure(unknown, JsonScalarErrorCode.UnknownField, "$");
        Assert.DoesNotContain(secret, unknown.Error!.Value.ToString(), StringComparison.Ordinal);
        Failure(JsonScalarRecordCodec.Decode(schema, Utf8("{\"Enabled\":true}")), JsonScalarErrorCode.UnknownField, "$");
        Failure(JsonScalarRecordCodec.Decode(schema, Utf8("{\"count\":1,\"count\":2}")),
            JsonScalarErrorCode.DuplicateField, "$[\"count\"]");
        var malformed = JsonScalarRecordCodec.Decode(schema, Utf8($"{{\"enabled\":true,\"{secret}\":"));
        Failure(malformed, JsonScalarErrorCode.InvalidJson, "$");
        Assert.DoesNotContain(secret, malformed.Error!.Value.ToString(), StringComparison.Ordinal);
        var exception = Assert.Throws<InvalidOperationException>(() => malformed.Value);
        Assert.DoesNotContain(secret, exception.Message, StringComparison.Ordinal);
        Failure(JsonScalarRecordCodec.Decode(schema, Utf8("[]")), JsonScalarErrorCode.TypeMismatch, "$");
    }

    [Fact]
    public void DeclaredPathsEscapeControlsAndInputUnicodeErrorsAreRedacted()
    {
        var schema = new JsonScalarRecordSchema([new("line\n\"name", JsonRecordFieldKind.Integer)]);
        var result = JsonScalarRecordCodec.Decode(schema, Utf8("{}"));
        Assert.False(result.IsSuccess);
        Assert.Equal(JsonScalarErrorCode.MissingField, result.Error!.Value.Code);
        Assert.StartsWith("$[\"", result.Error.Value.Path, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", result.Error.Value.Path, StringComparison.Ordinal);
        Assert.Contains("line", result.Error.Value.Path, StringComparison.Ordinal);
        var unicode = JsonScalarRecordCodec.Decode(schema, Utf8("{\"\\uD800\":1}"));
        Assert.False(unicode.IsSuccess);
        Assert.Equal("$", unicode.Error!.Value.Path);
        Assert.Contains(unicode.Error.Value.Code, new[] { JsonScalarErrorCode.InvalidJson, JsonScalarErrorCode.InvalidUnicode });
    }

    [Fact]
    public void DescriptorAndRecordValidationRejectUnsupportedInputsWithoutCallingThem()
    {
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("x", JsonRecordFieldKind.Integer), new("x", JsonRecordFieldKind.String)]));
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("x", JsonRecordFieldKind.Integer, Optional: true)]));
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("x", (JsonRecordFieldKind)999)]));
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new(new string('\uD800', 1), JsonRecordFieldKind.String)]));
        var schema = new JsonScalarRecordSchema([new("value", JsonRecordFieldKind.String)]);
        var foreign = new ForeignObject();
        var error = Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(schema, [foreign], [true]));
        Assert.False(foreign.WasCalled);
        Assert.DoesNotContain("secret-value-37", error.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(schema, [null], [true]));
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(schema, [null], [false]));
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(schema, [], []));
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(schema, [new string('\uD800', 1)], [true]));
    }

    [Fact]
    public void DescriptorSnapshotsItsFieldsAndOptionalNullableKindsRemainDistinct()
    {
        var fields = new[] { new JsonRecordField("a", JsonRecordFieldKind.NullableBoolean, true), new JsonRecordField("b", JsonRecordFieldKind.NullableInteger, true) };
        var schema = new JsonScalarRecordSchema(fields);
        fields[0] = new("changed", JsonRecordFieldKind.String);
        var result = JsonScalarRecordCodec.Decode(schema, Utf8("{\"a\":false,\"b\":null}"));
        Assert.True(result.IsSuccess);
        Assert.Equal(false, result.Value.ValueAt(0));
        Assert.True(result.Value.IsPresent(1));
        Assert.Null(result.Value.ValueAt(1));
        Assert.Equal("{\"a\":false,\"b\":null}", Encoding.UTF8.GetString(JsonScalarRecordCodec.Encode(result.Value)));
    }

    private static void Failure(JsonScalarResult<JsonScalarRecord> result, JsonScalarErrorCode expected, string path)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(new JsonScalarError(expected, path), result.Error);
    }

    private sealed class ForeignObject
    {
        internal bool WasCalled { get; private set; }
        public override string ToString() { WasCalled = true; return "secret-value-37"; }
    }
}
