using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonTreeBufferSnapshotTests
{
    [Fact]
    public void BufferFactoriesValidateAndOwnTheirSnapshots()
    {
        var listBuffer = new List<JsonFieldValue> { JsonFieldValue.Boolean(true) };
        var list = JsonFieldValue.ListFromBuffer(listBuffer);
        var mapBuffer = new List<KeyValuePair<string, JsonFieldValue>> { new("k", list) };
        var map = JsonFieldValue.MapFromBuffer(mapBuffer);
        listBuffer[0] = JsonFieldValue.Null(); mapBuffer[0] = new("changed", JsonFieldValue.Null());
        var exposed = list.ListItems; exposed[0] = JsonFieldValue.Null();
        Assert.Equal("{\"k\":[true]}", Encoding.UTF8.GetString(JsonFieldValueCodec.Encode(map)));
        Assert.Throws<ArgumentNullException>(() => JsonFieldValue.ListFromBuffer(null!));
        Assert.Throws<ArgumentNullException>(() => JsonFieldValue.MapFromBuffer(null!));
        Assert.Throws<ArgumentException>(() => JsonFieldValue.ListFromBuffer([null!]));
        Assert.Throws<ArgumentException>(() => JsonFieldValue.MapFromBuffer([new("same", JsonFieldValue.Null()), new("same", JsonFieldValue.Null())]));
        Assert.Throws<ArgumentException>(() => JsonFieldValue.MapFromBuffer([new("\uD800", JsonFieldValue.Null())]));
        var depth = JsonFieldValue.Null();
        for (var i = 0; i < 64; i++) depth = JsonFieldValue.ListFromBuffer([depth]);
        Assert.Throws<ArgumentException>(() => JsonFieldValue.ListFromBuffer([depth]));
    }

}
