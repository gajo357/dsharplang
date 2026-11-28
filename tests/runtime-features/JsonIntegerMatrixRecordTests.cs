using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonIntegerMatrixRecordTests
{
    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);

    [Fact]
    public void MatrixFieldsOwnRowsAndComposeWithTreesAndUnions()
    {
        var schema = new JsonScalarRecordSchema([new("m", JsonRecordFieldKind.IntegerMatrix),
            new("optional", JsonRecordFieldKind.NullableIntegerMatrix, Optional: true)]);
        int[][] input = [[1, -2], [], [int.MaxValue]];
        var matrix = JsonIntegerMatrix.Create(input);
        var record = JsonScalarRecord.Create(schema, [matrix, null], [true, false]);
        input[0][0] = 99; input[2] = [0];
        Assert.Equal(3, schema.Depth);
        Assert.Equal("{\"m\":[[1,-2],[],[2147483647]]}", Encoding.UTF8.GetString(JsonScalarRecordCodec.Encode(record)));
        var decoded = JsonScalarRecordCodec.Decode(schema, JsonScalarRecordCodec.Encode(record));
        Assert.True(decoded.IsSuccess); Assert.False(decoded.Value.IsPresent(1));
        var result = (JsonIntegerMatrix)decoded.Value.ValueAt(0)!;
        Assert.Equal(3, result.Count); Assert.Equal(0, result.RowLength(1)); Assert.Equal(int.MaxValue, result.At(2, 0));
        var explicitNull = JsonScalarRecordCodec.Decode(schema, Bytes("{\"m\":[],\"optional\":null}"));
        Assert.True(explicitNull.IsSuccess); Assert.True(explicitNull.Value.IsPresent(1)); Assert.Null(explicitNull.Value.ValueAt(1));
        var tree = JsonTypedFieldMapping.FromRecord(record);
        Assert.True(tree.IsSuccess); Assert.True(JsonTypedFieldMapping.ToRecord(schema, tree.Value).IsSuccess);
        var unionSchema = new JsonTaggedUnionSchema([new("Case", schema)]);
        var union = JsonTaggedUnion.Create(unionSchema, 0, record);
        Assert.True(JsonTaggedUnionCodec.Decode(unionSchema, JsonTaggedUnionCodec.Encode(union)).IsSuccess);
        Assert.Throws<ArgumentException>(() => JsonIntegerMatrix.Create([null!]));
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(schema, [new int[][] { [1] }, null], [true, false]));
    }

    [Fact]
    public void InvalidNestedValuesReportFirstRedactedIndexedError()
    {
        var schema = new JsonScalarRecordSchema([new("m", JsonRecordFieldKind.IntegerMatrix)]);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.MissingField, "$[\"m\"]"), JsonScalarRecordCodec.Decode(schema, Bytes("{}")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"m\"]"), JsonScalarRecordCodec.Decode(schema, Bytes("{\"m\":null}")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"m\"][1]"), JsonScalarRecordCodec.Decode(schema, Bytes("{\"m\":[[],null]}")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidInteger, "$[\"m\"][0][1]"), JsonScalarRecordCodec.Decode(schema, Bytes("{\"m\":[[1,2147483648,\"secret\"]]}")).Error);
        var failure = JsonScalarRecordCodec.Decode(schema, Bytes("{\"m\":[[1,\"secret\"],null]}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"m\"][0][1]"), failure.Error);
        Assert.DoesNotContain("secret", failure.Error.ToString());
    }
}
