using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonTypedCollectionMappingTests
{
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);
    private static JsonFieldValue Tree(string json) => JsonFieldValueCodec.Decode(Utf8(json)).Value;
    private static string Encode(JsonFieldValue value) => Encoding.UTF8.GetString(JsonFieldValueCodec.Encode(value));
    private static JsonScalarRecord Record(JsonScalarRecordSchema schema, string json) => JsonScalarRecordCodec.Decode(schema, Utf8(json)).Value;
    private static JsonTaggedUnion Union(JsonTaggedUnionSchema schema, string json) => JsonTaggedUnionCodec.Decode(schema, Utf8(json)).Value;

    [Fact]
    public void RecordsRoundTripWithExactNumbersPresenceAndDescriptor()
    {
        var schema = new JsonScalarRecordSchema([
            new("amount", JsonRecordFieldKind.ExactNumber),
            new("note", JsonRecordFieldKind.NullableString, Optional: true)
        ]);
        var source = new[] { Record(schema, "{\"amount\":12345678901234567890.000e+8}"), Record(schema, "{\"amount\":-0.0,\"note\":null}") };
        var array = JsonRecordArray.Create(schema, source);
        source[0] = source[1];
        var mapped = JsonTypedFieldMapping.FromRecords(array);
        Assert.True(mapped.IsSuccess);
        const string expected = "[{\"amount\":12345678901234567890.000e+8},{\"amount\":-0.0,\"note\":null}]";
        Assert.Equal(expected, Encode(mapped.Value));
        var restored = JsonTypedFieldMapping.ToRecords(schema, mapped.Value);
        Assert.True(restored.IsSuccess);
        Assert.Same(schema, restored.Value.Schema);
        Assert.Same(schema, restored.Value.At(0).Schema);
        Assert.False(restored.Value.At(0).IsPresent(1));
        Assert.True(restored.Value.At(1).IsPresent(1));
        Assert.Null(restored.Value.At(1).ValueAt(1));
        Assert.Equal(expected, Encode(JsonTypedFieldMapping.FromRecords(restored.Value).Value));
    }

    [Fact]
    public void UnionArraysPreserveCasesAndNullableElements()
    {
        var payload = new JsonScalarRecordSchema([new("name", JsonRecordFieldKind.String)]);
        var schema = new JsonTaggedUnionSchema([new("Empty"), new("Named", payload)]);
        var one = Union(schema, "{\"case\":\"Empty\"}");
        var two = Union(schema, "{\"case\":\"Named\",\"value\":{\"name\":\"Ada\"}}");
        var source = new[] { one, two };
        var array = JsonUnionArray.Create(schema, source);
        source[0] = two;
        var tree = JsonTypedFieldMapping.FromUnions(array).Value;
        const string expected = "[{\"case\":\"Empty\"},{\"case\":\"Named\",\"value\":{\"name\":\"Ada\"}}]";
        Assert.Equal(expected, Encode(tree));
        var restored = JsonTypedFieldMapping.ToUnions(schema, tree);
        Assert.True(restored.IsSuccess);
        Assert.Same(schema, restored.Value.Schema);
        Assert.Equal(0, restored.Value.At(0).CaseIndex);
        Assert.Equal(1, restored.Value.At(1).CaseIndex);
        var nullable = JsonNullableUnionArray.Create(schema, [null, two, one]);
        var mapped = JsonTypedFieldMapping.FromNullableUnions(nullable).Value;
        Assert.Equal("[null,{\"case\":\"Named\",\"value\":{\"name\":\"Ada\"}},{\"case\":\"Empty\"}]", Encode(mapped));
        var nullableBack = JsonTypedFieldMapping.ToNullableUnions(schema, mapped);
        Assert.True(nullableBack.IsSuccess);
        Assert.Same(schema, nullableBack.Value.Schema);
        Assert.Null(nullableBack.Value.At(0));
        Assert.Equal(1, nullableBack.Value.At(1)!.CaseIndex);
    }

    [Fact]
    public void EmptyArraysAndExplicitInputsAreHandled()
    {
        var records = new JsonScalarRecordSchema([]);
        var unions = new JsonTaggedUnionSchema([new("Empty")]);
        Assert.Equal("[]", Encode(JsonTypedFieldMapping.FromRecords(JsonRecordArray.Create(records, [])).Value));
        Assert.Equal(0, JsonTypedFieldMapping.ToRecords(records, Tree("[]")).Value.Count);
        Assert.Equal(0, JsonTypedFieldMapping.ToUnions(unions, Tree("[]")).Value.Count);
        Assert.Equal(0, JsonTypedFieldMapping.ToNullableUnions(unions, Tree("[]")).Value.Count);
        Assert.Throws<ArgumentNullException>(() => JsonTypedFieldMapping.FromRecords(null!));
        Assert.Throws<ArgumentNullException>(() => JsonTypedFieldMapping.FromUnions(null!));
        Assert.Throws<ArgumentNullException>(() => JsonTypedFieldMapping.FromNullableUnions(null!));
        Assert.Throws<ArgumentNullException>(() => JsonTypedFieldMapping.ToRecords(null!, Tree("[]")));
        Assert.Throws<ArgumentNullException>(() => JsonTypedFieldMapping.ToUnions(unions, null!));
        Assert.Throws<ArgumentNullException>(() => JsonTypedFieldMapping.ToNullableUnions(unions, null!));
    }

    [Fact]
    public void InvalidElementsReportRedactedIndexedPaths()
    {
        var records = new JsonScalarRecordSchema([new("count", JsonRecordFieldKind.Integer)]);
        var unions = new JsonTaggedUnionSchema([new("Allowed")]);
        var root = JsonTypedFieldMapping.ToRecords(records, Tree("{}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$"), root.Error);
        var wrong = JsonTypedFieldMapping.ToRecords(records, Tree("[{\"count\":1},{\"count\":\"secret\"}]"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[1][\"count\"]"), wrong.Error);
        var unknown = JsonTypedFieldMapping.ToRecords(records, Tree("[{\"private\":\"secret\"}]"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.UnknownField, "$[0]"), unknown.Error);
        var badCase = JsonTypedFieldMapping.ToNullableUnions(unions, Tree("[null,{\"case\":\"secret\"}]"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.UnknownCase, "$[1][\"case\"]"), badCase.Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[0]"), JsonTypedFieldMapping.ToUnions(unions, Tree("[null]")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[0]"), JsonTypedFieldMapping.ToRecords(records, Tree("[null]")).Error);
        foreach (var error in new[] { wrong.Error, unknown.Error, badCase.Error })
        {
            Assert.DoesNotContain("private", error!.Value.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("secret", error.Value.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CollectionMappingEnforcesCombinedDepth()
    {
        var schema = new JsonScalarRecordSchema([new("data", JsonRecordFieldKind.FieldValue)]);
        JsonScalarRecord Make(int depth)
        {
            var tree = JsonFieldValue.Null();
            for (var i = 0; i < depth; i++) tree = JsonFieldValue.List([tree]);
            return JsonScalarRecord.Create(schema, [tree], [true]);
        }
        var valid = JsonRecordArray.Create(schema, [Make(62)]);
        var mapped = JsonTypedFieldMapping.FromRecords(valid);
        Assert.True(mapped.IsSuccess);
        Assert.Equal(64, mapped.Value.Depth);
        Assert.True(JsonTypedFieldMapping.ToRecords(schema, mapped.Value).IsSuccess);
        var invalid = JsonRecordArray.Create(schema, [Make(63)]);
        Assert.Throws<ArgumentException>(() => JsonTypedFieldMapping.FromRecords(invalid));

        var cases = new JsonTaggedUnionSchema([new("Data", schema)]);
        var union = JsonTaggedUnion.Create(cases, 0, Make(61));
        var unionTree = JsonTypedFieldMapping.FromUnions(JsonUnionArray.Create(cases, [union]));
        Assert.True(unionTree.IsSuccess);
        Assert.Equal(64, unionTree.Value.Depth);
        Assert.True(JsonTypedFieldMapping.ToUnions(cases, unionTree.Value).IsSuccess);
        var tooDeep = JsonTaggedUnion.Create(cases, 0, Make(62));
        Assert.Throws<ArgumentException>(() => JsonTypedFieldMapping.FromUnions(JsonUnionArray.Create(cases, [tooDeep])));
        Assert.Throws<ArgumentException>(() => JsonTypedFieldMapping.FromNullableUnions(JsonNullableUnionArray.Create(cases, [null, tooDeep])));
    }
}
