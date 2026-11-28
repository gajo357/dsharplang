using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

[CollectionDefinition("Tree allocation", DisableParallelization = true)]
public sealed class JsonTreeAllocationCollection { }

[Collection("Tree allocation")]
public sealed class JsonTreeDecodeAllocationTests
{
    [Fact]
    public void WideTreeDecodeAvoidsDuplicateArraySnapshot()
    {
        var bytes = Encoding.UTF8.GetBytes("[" + string.Concat(Enumerable.Repeat("true,", 199_999)) + "true]");
        JsonFieldValueCodec.Decode(bytes);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = JsonFieldValueCodec.Decode(bytes);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 17_800_000, $"Tree decoding allocated {allocated} bytes.");
        Assert.True(result.IsSuccess); Assert.Equal(200_000, result.Value.ListItems.Length);
        var failure = JsonFieldValueCodec.Decode("[{\"private\":{\"secret\":1,\"secret\":2}}]"u8.ToArray());
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.DuplicateField, "$[0][0][1]"), failure.Error);
        Assert.DoesNotContain("secret", failure.Error!.Value.ToString(), StringComparison.Ordinal);
    }
}
