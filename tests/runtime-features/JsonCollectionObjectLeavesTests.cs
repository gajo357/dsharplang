using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonCollectionObjectLeavesTests
{
    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);

    [Fact]
    public void TypedLeavesComposeAndRetainValidation()
    {
        var recordSchema = new JsonScalarRecordSchema([new("name", JsonRecordFieldKind.String), new("age", JsonRecordFieldKind.NullableInteger, Optional: true)]);
        var recordShape = JsonCollectionShape.Map(JsonCollectionShape.List(JsonCollectionShape.Nullable(JsonCollectionShape.Record(recordSchema))));
        var records = JsonCollectionCodec.Decode(recordShape, Bytes("{\"group\":[{\"name\":\"A\"},null,{\"name\":\"B\",\"age\":3}]}"));
        Assert.True(records.IsSuccess);
        var items = records.Value.MapAt(0).Value;
        Assert.Equal(JsonFieldValueKind.Null, items.ListAt(1).Kind);
        var recovered = JsonTypedFieldMapping.ToRecord(recordSchema, items.ListAt(2));
        Assert.True(recovered.IsSuccess); Assert.Equal("B", recovered.Value.ValueAt(0)); Assert.Equal(3, recovered.Value.ValueAt(1));
        Assert.True(JsonCollectionCodec.Decode(recordShape, JsonFieldValueCodec.Encode(records.Value)).IsSuccess);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.MissingField, "$[0][0][\"name\"]"), JsonCollectionCodec.Decode(recordShape, Bytes("{\"secret\":[{}]}" )).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[0][0][\"name\"]"), JsonCollectionCodec.Decode(recordShape, Bytes("{\"secret\":[{\"name\":9,\"name\":\"ok\"}]}" )).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.DuplicateField, "$[0][0][\"name\"]"), JsonCollectionCodec.Decode(recordShape, Bytes("{\"secret\":[{\"name\":\"ok\",\"name\":\"secret\"}]}" )).Error);
        var unionSchema = new JsonTaggedUnionSchema([new("One", recordSchema), new("Empty")]);
        var unionShape = JsonCollectionShape.List(JsonCollectionShape.Map(JsonCollectionShape.Union(unionSchema)));
        var unions = JsonCollectionCodec.Decode(unionShape, Bytes("[{\"x\":{\"case\":\"One\",\"value\":{\"name\":\"A\"}},\"y\":{\"case\":\"Empty\"}}]"));
        Assert.True(unions.IsSuccess);
        var recoveredUnion = JsonTypedFieldMapping.ToUnion(unionSchema, unions.Value.ListAt(0).MapAt(0).Value);
        Assert.True(recoveredUnion.IsSuccess); Assert.Equal("A", recoveredUnion.Value.Payload!.ValueAt(0));
        Assert.True(JsonCollectionCodec.Decode(unionShape, JsonFieldValueCodec.Encode(unions.Value)).IsSuccess);
        var failure = JsonCollectionCodec.Decode(unionShape, Bytes("[{\"secret\":{\"case\":\"One\",\"value\":{\"name\":7}}}]"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[0][0][\"value\"][\"name\"]"), failure.Error);
        Assert.DoesNotContain("secret", failure.Error.ToString());
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.UnknownCase, "$[0][0][\"case\"]"), JsonCollectionCodec.Decode(unionShape, Bytes("[{\"x\":{\"case\":\"secret\"}}]")).Error);
        Assert.Throws<ArgumentNullException>(() => JsonCollectionShape.Record(null!));
        Assert.Throws<ArgumentNullException>(() => JsonCollectionShape.Union(null!));
        var deep = new JsonScalarRecordSchema([new("v", JsonRecordFieldKind.Integer)]);
        for (var i = 0; i < 63; i++) deep = new JsonScalarRecordSchema([new("v", JsonRecordFieldKind.Record, NestedSchema: deep)]);
        Assert.Equal(64, JsonCollectionShape.Record(deep).Height);
        Assert.Throws<ArgumentException>(() => JsonCollectionShape.List(JsonCollectionShape.Record(deep)));
    }
}
