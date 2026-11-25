using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonTaggedUnionCodecTests
{
    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
    private static JsonScalarRecordSchema Payload() => new([
        new("text", JsonRecordFieldKind.String),
        new("items", JsonRecordFieldKind.NullableIntegerNullableElementsArray, Optional: true)
    ]);
    private static JsonTaggedUnionSchema Schema(JsonScalarRecordSchema payload) => new([
        new("Idle"), new("Ready", payload), new("Other", payload)
    ]);

    [Fact]
    public void UnitAndRecordCasesRoundTripAndPreservePayloadSnapshots()
    {
        var payloadSchema = Payload();
        var schema = Schema(payloadSchema);
        var original = new object?[] { "café 😀", JsonRecordScalarArray.Create(new int?[] { 2, null, 4 }) };
        var record = JsonScalarRecord.Create(payloadSchema, original, [true, true]);
        original[0] = "mutated";
        var values = new[] { JsonTaggedUnion.Create(schema, 0), JsonTaggedUnion.Create(schema, 1, record), JsonTaggedUnion.Create(schema, 2, record) };
        foreach (var value in values)
        {
            var bytes = JsonTaggedUnionCodec.Encode(value);
            var decoded = JsonTaggedUnionCodec.Decode(schema, bytes);
            Assert.True(decoded.IsSuccess);
            Assert.Equal(value.CaseIndex, decoded.Value.CaseIndex);
            Assert.Same(schema, decoded.Value.Schema);
            Assert.Equal(bytes, JsonTaggedUnionCodec.Encode(decoded.Value));
            if (value.Payload is null) Assert.Null(decoded.Value.Payload);
            else
            {
                Assert.Same(payloadSchema, decoded.Value.Payload!.Schema);
                Assert.Equal("café 😀", decoded.Value.Payload.ValueAt(0));
                Assert.Null(Assert.IsType<JsonRecordScalarArray>(decoded.Value.Payload.ValueAt(1)).At(1));
            }
        }
        var reversed = JsonTaggedUnionCodec.Decode(schema, Utf8("{\"value\":{\"text\":\"x\"},\"case\":\"Ready\"}"));
        Assert.True(reversed.IsSuccess);
        Assert.False(reversed.Value.Payload!.IsPresent(1));
    }

    [Fact]
    public void ConstructorRejectsUntrustedOrMismatchedDescriptors()
    {
        var first = Payload(); var other = Payload(); var schema = Schema(first);
        var foreign = JsonScalarRecord.Create(other, ["x", null], [true, false]);
        var owned = JsonScalarRecord.Create(first, ["x", null], [true, false]);
        Assert.Throws<ArgumentException>(() => JsonTaggedUnion.Create(schema, 0, owned));
        Assert.Throws<ArgumentException>(() => JsonTaggedUnion.Create(schema, 1));
        Assert.Throws<ArgumentException>(() => JsonTaggedUnion.Create(schema, 1, foreign));
        Assert.Throws<ArgumentOutOfRangeException>(() => JsonTaggedUnion.Create(schema, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => JsonTaggedUnion.Create(schema, 3));
        Assert.Throws<ArgumentException>(() => new JsonTaggedUnionSchema([]));
        Assert.Throws<ArgumentException>(() => new JsonTaggedUnionSchema([new("x"), new("x")]));
        Assert.Throws<ArgumentException>(() => new JsonTaggedUnionSchema([new("")]));
        Assert.Throws<ArgumentException>(() => new JsonTaggedUnionSchema([new("\uD800")]));
        var cases = new[] { new JsonTaggedUnionCase("original") };
        var copied = new JsonTaggedUnionSchema(cases); cases[0] = new("changed");
        Assert.Equal("original", copied.CaseAt(0).Name);
    }

    [Fact]
    public void MissingDuplicateUnknownAndWrongTypesFailAtSafePaths()
    {
        var schema = Schema(Payload());
        Failure(schema, "[]", JsonScalarErrorCode.TypeMismatch, "$");
        Failure(schema, "{}", JsonScalarErrorCode.MissingField, "$[\"case\"]");
        Failure(schema, "{\"case\":null}", JsonScalarErrorCode.TypeMismatch, "$[\"case\"]");
        Failure(schema, "{\"case\":\"secret-tag\"}", JsonScalarErrorCode.UnknownCase, "$[\"case\"]");
        Failure(schema, "{\"case\":\"idle\"}", JsonScalarErrorCode.UnknownCase, "$[\"case\"]");
        Failure(schema, "{\"case\":\"Idle\",\"case\":\"Idle\"}", JsonScalarErrorCode.DuplicateField, "$[\"case\"]");
        Failure(schema, "{\"case\":\"Ready\",\"value\":{},\"value\":{}}", JsonScalarErrorCode.DuplicateField, "$[\"value\"]");
        Failure(schema, "{\"case\":\"Idle\",\"secret-name\":1}", JsonScalarErrorCode.UnknownField, "$");
        Failure(schema, "{\"case\":\"Idle\",\"value\":null}", JsonScalarErrorCode.TypeMismatch, "$[\"value\"]");
        Failure(schema, "{\"case\":\"Ready\"}", JsonScalarErrorCode.MissingField, "$[\"value\"]");
        Failure(schema, "{\"case\":\"Ready\",\"value\":null}", JsonScalarErrorCode.TypeMismatch, "$[\"value\"]");
        Failure(schema, "{\"case\":", JsonScalarErrorCode.InvalidJson, "$");
        var result = JsonTaggedUnionCodec.Decode(schema, Utf8("{\"case\":\"secret-tag\"}"));
        Assert.DoesNotContain("secret", result.Error!.Value.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("secret", Assert.Throws<InvalidOperationException>(() => result.Value).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PayloadErrorsKeepOnlyDeclaredNestedPaths()
    {
        var child = new JsonScalarRecordSchema([new("text\n\"x", JsonRecordFieldKind.IntegerNullableElementsArray)]);
        var schema = new JsonTaggedUnionSchema([new("Known", child)]);
        var result = JsonTaggedUnionCodec.Decode(schema, Utf8("{\"case\":\"Known\",\"value\":{\"text\\n\\\"x\":[null,1.5]}}"));
        Assert.False(result.IsSuccess);
        Assert.Equal(JsonScalarErrorCode.InvalidInteger, result.Error!.Value.Code);
        Assert.StartsWith("$[\"value\"][", result.Error.Value.Path, StringComparison.Ordinal);
        Assert.EndsWith("[1]", result.Error.Value.Path, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", result.Error.Value.Path, StringComparison.Ordinal);
        Failure(schema, "{\"case\":\"Known\",\"value\":{\"secret-name\":7}}", JsonScalarErrorCode.UnknownField, "$[\"value\"]");
        var unicode = JsonTaggedUnionCodec.Decode(schema, Utf8("{\"case\":\"\\uD800\"}"));
        Assert.False(unicode.IsSuccess);
        Assert.Equal("$[\"case\"]", unicode.Error!.Value.Path);
        Assert.Contains(unicode.Error.Value.Code, new[] { JsonScalarErrorCode.InvalidUnicode, JsonScalarErrorCode.InvalidJson });
    }

    [Fact]
    public void UnionAndPayloadTogetherRespectDepthLimit()
    {
        var payload = new JsonScalarRecordSchema([]);
        var record = JsonScalarRecord.Create(payload, [], []);
        for (var depth = 2; depth <= 63; depth++)
        {
            payload = new JsonScalarRecordSchema([new("child", JsonRecordFieldKind.Record, NestedSchema: payload)]);
            record = JsonScalarRecord.Create(payload, [record], [true]);
        }
        var union = new JsonTaggedUnionSchema([new("Deep", payload)]);
        Assert.True(JsonTaggedUnionCodec.Decode(union, JsonTaggedUnionCodec.Encode(JsonTaggedUnion.Create(union, 0, record))).IsSuccess);
        var tooDeep = new JsonScalarRecordSchema([new("child", JsonRecordFieldKind.Record, NestedSchema: payload)]);
        Assert.Throws<ArgumentException>(() => new JsonTaggedUnionSchema([new("Deep", tooDeep)]));
    }

    private static void Failure(JsonTaggedUnionSchema schema, string text, JsonScalarErrorCode code, string path)
    {
        var result = JsonTaggedUnionCodec.Decode(schema, Utf8(text));
        Assert.False(result.IsSuccess);
        Assert.Equal(new JsonScalarError(code, path), result.Error);
    }
}
