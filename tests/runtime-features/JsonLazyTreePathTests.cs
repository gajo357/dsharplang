using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonLazyTreePathTests
{
    [Fact]
    public void NestedFailuresPreserveFirstIndexedPathWithoutSecretNames()
    {
        var payload = new JsonScalarRecordSchema([new("fields", JsonRecordFieldKind.FieldValue)]);
        var cases = new JsonTaggedUnionSchema([new("Data", payload)]);
        var root = new JsonScalarRecordSchema([new("event", JsonRecordFieldKind.Union, UnionSchema: cases)]);
        var duplicate = JsonScalarRecordCodec.Decode(root, Encoding.UTF8.GetBytes("{\"event\":{\"case\":\"Data\",\"value\":{\"fields\":{\"private\":[{\"secret\":{\"x\":1,\"x\":2}}]}}}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.DuplicateField, "$[\"event\"][\"value\"][\"fields\"][0][0][0][1]"), duplicate.Error);
        Assert.DoesNotContain("secret", duplicate.Error!.Value.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("private", duplicate.Error.Value.ToString(), StringComparison.Ordinal);
        var first = JsonFieldValueCodec.Decode(Encoding.UTF8.GetBytes("[\"\\uD800\",{\"secret\":1,\"secret\":2}]"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidUnicode, "$[0]"), first.Error);
        var valid = JsonFieldValueCodec.Decode("[{\"x\":[true,null]}]"u8.ToArray());
        Assert.True(valid.IsSuccess);
        Assert.Equal("[{\"x\":[true,null]}]", Encoding.UTF8.GetString(JsonFieldValueCodec.Encode(valid.Value)));
    }

    [Fact]
    public void WideValidArrayDoesNotAllocateAPathPerElement()
    {
        var bytes = Encoding.UTF8.GetBytes("[" + string.Concat(Enumerable.Repeat("true,", 49_999)) + "true]");
        JsonFieldValueCodec.Decode(bytes);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = JsonFieldValueCodec.Decode(bytes);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 6_500_000, $"Tree decoding allocated {allocated} bytes.");
        Assert.True(result.IsSuccess); Assert.Equal(50_000, result.Value.ListItems.Length);
    }
}
