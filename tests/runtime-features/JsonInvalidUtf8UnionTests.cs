using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonInvalidUtf8UnionTests
{
    private static readonly JsonScalarRecordSchema Payload = new([new("text", JsonRecordFieldKind.String)]);
    private static readonly JsonTaggedUnionSchema Union = new([new("Message", Payload)]);
    private static byte[] BadUtf8(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var index = Array.IndexOf(bytes, (byte)'~');
        Assert.True(index >= 0);
        bytes[index] = 0xff;
        return bytes;
    }

    [Theory]
    [InlineData("{\"case\":\"Message\",\"value\":{\"text\":\"secret~\"}}", "$[\"value\"][\"text\"]")]
    [InlineData("{\"case\":\"Message\",\"value\":{\"secret~\":1}}", "$[\"value\"]")]
    public void RootUnionPayloadReportsInvalidUtf8WithoutThrowing(string json, string path)
    {
        var decoded = JsonValidatedUnionPayload.Decode(Union, BadUtf8(json));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidUnicode, path), decoded.Error);
        Assert.DoesNotContain("secret", decoded.Error!.Value.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Union", "{\"item\":{\"case\":\"Message\",\"value\":{\"text\":\"secret~\"}}}", "$[\"item\"][\"value\"][\"text\"]")]
    [InlineData("UnionArray", "{\"item\":[{\"case\":\"Message\",\"value\":{\"text\":\"secret~\"}}]}", "$[\"item\"][0][\"value\"][\"text\"]")]
    [InlineData("UnionNullableElementsArray", "{\"item\":[null,{\"case\":\"Message\",\"value\":{\"text\":\"secret~\"}}]}", "$[\"item\"][1][\"value\"][\"text\"]")]
    [InlineData("UnionMap", "{\"item\":{\"private\":{\"case\":\"Message\",\"value\":{\"text\":\"secret~\"}}}}", "$[\"item\"][0][\"value\"][\"text\"]")]
    [InlineData("UnionNullableValuesMap", "{\"item\":{\"a\":null,\"private\":{\"case\":\"Message\",\"value\":{\"secret~\":1}}}}", "$[\"item\"][1][\"value\"]")]
    public void NestedUnionsReportRedactedPaths(string kind, string json, string path)
    {
        var schema = new JsonScalarRecordSchema([new("item", Enum.Parse<JsonRecordFieldKind>(kind), UnionSchema: Union)]);
        var decoded = JsonValidatedRecordPayload.Decode(schema, BadUtf8(json));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidUnicode, path), decoded.Error);
        Assert.DoesNotContain("secret", decoded.Error!.Value.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("private", decoded.Error.Value.ToString(), StringComparison.Ordinal);
    }
}
