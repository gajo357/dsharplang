using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonTypedFieldMappingTests
{
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);
    private static JsonFieldValue Tree(string json) => JsonFieldValueCodec.Decode(Utf8(json)).Value;
    private static string Encode(JsonFieldValue value) => Encoding.UTF8.GetString(JsonFieldValueCodec.Encode(value));

    [Fact]
    public void NestedTypedRecordRoundTripsThroughTreeWithPresenceAndPrecision()
    {
        var payload = new JsonScalarRecordSchema([
            new("amount", JsonRecordFieldKind.ExactNumber),
            new("note", JsonRecordFieldKind.NullableString, Optional: true),
            new("fields", JsonRecordFieldKind.NullableFieldValue, Optional: true)
        ]);
        var cases = new JsonTaggedUnionSchema([new("Empty"), new("Data", payload)]);
        var schema = new JsonScalarRecordSchema([
            new("event", JsonRecordFieldKind.Union, UnionSchema: cases),
            new("items", JsonRecordFieldKind.IntegerArray)
        ]);
        const string json = "{\"event\":{\"case\":\"Data\",\"value\":{\"amount\":12345678901234567890.000e+8,\"fields\":null}},\"items\":[1,2]}";
        var original = JsonScalarRecordCodec.Decode(schema, Utf8(json));
        Assert.True(original.IsSuccess);
        var mapped = JsonTypedFieldMapping.FromRecord(original.Value);
        Assert.True(mapped.IsSuccess);
        Assert.Equal(json, Encode(mapped.Value));
        var returned = JsonTypedFieldMapping.ToRecord(schema, mapped.Value);
        Assert.True(returned.IsSuccess);
        Assert.Same(schema, returned.Value.Schema);
        Assert.Equal(json, Encoding.UTF8.GetString(JsonScalarRecordCodec.Encode(returned.Value)));
        var union = Assert.IsType<JsonTaggedUnion>(returned.Value.ValueAt(0));
        Assert.Same(cases, union.Schema);
        Assert.False(union.Payload!.IsPresent(1));
        Assert.True(union.Payload.IsPresent(2));
        Assert.Null(union.Payload.ValueAt(2));
    }

    [Fact]
    public void EveryUnionCaseMapsExplicitlyAndKeepsDescriptorIdentity()
    {
        var payload = new JsonScalarRecordSchema([new("name", JsonRecordFieldKind.String)]);
        var schema = new JsonTaggedUnionSchema([new("Empty"), new("Named", payload)]);
        foreach (var json in new[] { "{\"case\":\"Empty\"}", "{\"case\":\"Named\",\"value\":{\"name\":\"Ada\"}}" })
        {
            var original = JsonTaggedUnionCodec.Decode(schema, Utf8(json));
            Assert.True(original.IsSuccess);
            var tree = JsonTypedFieldMapping.FromUnion(original.Value);
            Assert.True(tree.IsSuccess);
            Assert.Equal(json, Encode(tree.Value));
            var restored = JsonTypedFieldMapping.ToUnion(schema, tree.Value);
            Assert.True(restored.IsSuccess);
            Assert.Same(schema, restored.Value.Schema);
            Assert.Equal(original.Value.CaseIndex, restored.Value.CaseIndex);
        }
    }

    [Fact]
    public void TypedDecodeRejectsWrongShapesWithoutLeakingUnknownData()
    {
        var schema = new JsonScalarRecordSchema([new("count", JsonRecordFieldKind.Integer)]);
        var unknown = JsonTypedFieldMapping.ToRecord(schema, Tree("{\"secret-key\":\"secret-value\"}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.UnknownField, "$"), unknown.Error);
        Assert.DoesNotContain("secret", unknown.Error!.Value.ToString(), StringComparison.Ordinal);
        var wrong = JsonTypedFieldMapping.ToRecord(schema, Tree("{\"count\":\"secret\"}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"count\"]"), wrong.Error);
        Assert.DoesNotContain("secret", wrong.Error!.Value.ToString(), StringComparison.Ordinal);
        Assert.Equal(JsonScalarErrorCode.TypeMismatch, JsonTypedFieldMapping.ToRecord(schema, Tree("[]")).Error!.Value.Code);
        var cases = new JsonTaggedUnionSchema([new("Allowed")]);
        var unknownCase = JsonTypedFieldMapping.ToUnion(cases, Tree("{\"case\":\"secret-tag\"}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.UnknownCase, "$[\"case\"]"), unknownCase.Error);
        Assert.DoesNotContain("secret", unknownCase.Error!.Value.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void MappingOwnsContainersAndRequiresExplicitInputs()
    {
        var elements = new[] { JsonFieldValue.String("original") };
        var schema = new JsonScalarRecordSchema([new("fields", JsonRecordFieldKind.FieldValue)]);
        var original = JsonScalarRecord.Create(schema, [JsonFieldValue.List(elements)], [true]);
        var mapped = JsonTypedFieldMapping.FromRecord(original);
        Assert.True(mapped.IsSuccess);
        elements[0] = JsonFieldValue.Boolean(false);
        mapped.Value.MapItems[0] = new("changed", JsonFieldValue.Null());
        Assert.Equal("{\"fields\":[\"original\"]}", Encode(mapped.Value));
        Assert.Throws<ArgumentNullException>(() => JsonTypedFieldMapping.FromRecord(null!));
        Assert.Throws<ArgumentNullException>(() => JsonTypedFieldMapping.ToRecord(null!, mapped.Value));
        Assert.Throws<ArgumentNullException>(() => JsonTypedFieldMapping.ToRecord(schema, null!));
        Assert.Throws<ArgumentNullException>(() => JsonTypedFieldMapping.FromUnion(null!));
        Assert.Throws<ArgumentNullException>(() => JsonTypedFieldMapping.ToUnion(null!, mapped.Value));
    }

    [Fact]
    public void MappingRetainsTheCombinedDepthLimit()
    {
        var schema = new JsonScalarRecordSchema([new("data", JsonRecordFieldKind.FieldValue)]);
        var tree = JsonFieldValue.Null();
        for (var i = 0; i < 63; i++) tree = JsonFieldValue.List([tree]);
        var record = JsonScalarRecord.Create(schema, [tree], [true]);
        var mapped = JsonTypedFieldMapping.FromRecord(record);
        Assert.True(mapped.IsSuccess);
        Assert.Equal(64, mapped.Value.Depth);
        Assert.True(JsonTypedFieldMapping.ToRecord(schema, mapped.Value).IsSuccess);
        Assert.Throws<ArgumentException>(() => JsonFieldValue.List([mapped.Value]));
    }
}
