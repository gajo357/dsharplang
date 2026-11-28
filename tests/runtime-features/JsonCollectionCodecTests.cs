using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonCollectionCodecTests
{
    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);

    [Fact]
    public void ComposedCollectionsPreserveTypedValuesAndRejectInvalidInput()
    {
        var shape = JsonCollectionShape.Map(JsonCollectionShape.List(JsonCollectionShape.Nullable(JsonCollectionShape.Integer())));
        var decoded = JsonCollectionCodec.Decode(shape, Bytes("{\"a\":[1,null,-2],\"empty\":[]}"));
        Assert.True(decoded.IsSuccess);
        Assert.Equal("{\"a\":[1,null,-2],\"empty\":[]}", Encoding.UTF8.GetString(JsonFieldValueCodec.Encode(decoded.Value)));
        var exposed = decoded.Value.MapItems; exposed[0] = new("changed", JsonFieldValue.Null());
        Assert.Equal("a", decoded.Value.MapAt(0).Key);
        Assert.Equal(JsonFieldValueKind.Null, decoded.Value.MapAt(0).Value.ListAt(1).Kind);
        var nested = JsonCollectionShape.List(JsonCollectionShape.Map(JsonCollectionShape.List(JsonCollectionShape.String())));
        Assert.True(JsonCollectionCodec.Decode(nested, Bytes("[{\"x\":[\"ok\"]},{}]" )).IsSuccess);
        Assert.True(JsonCollectionCodec.Decode(JsonCollectionShape.Nullable(JsonCollectionShape.List(JsonCollectionShape.Boolean())), Bytes("null")).IsSuccess);
        Assert.True(JsonCollectionCodec.Decode(JsonCollectionShape.List(JsonCollectionShape.Boolean()), Bytes("[true,false]")).IsSuccess);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[0][1]"), JsonCollectionCodec.Decode(shape, Bytes("{\"secret\":[1,\"secret-value\"]}")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidInteger, "$[0][0]"), JsonCollectionCodec.Decode(shape, Bytes("{\"x\":[2147483648]}")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.DuplicateField, "$[1]"), JsonCollectionCodec.Decode(shape, Bytes("{\"x\":[],\"x\":[]}")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[0][0]"), JsonCollectionCodec.Decode(shape, Bytes("{\"x\":[false],\"x\":[]}")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidUnicode, "$[0]"), JsonCollectionCodec.Decode(shape, Bytes("{\"\\uD800\":[]}")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidUnicode, "$[0][0][0]"), JsonCollectionCodec.Decode(nested, Bytes("[{\"x\":[\"\\uD800\"]}]")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidJson, "$"), JsonCollectionCodec.Decode(shape, Bytes("{} trailing")).Error);
        Assert.DoesNotContain("secret", JsonCollectionCodec.Decode(shape, Bytes("{\"secret\":[\"secret-value\"]}")).Error.ToString());
        Assert.Throws<ArgumentNullException>(() => JsonCollectionShape.List(null!));
        Assert.Throws<ArgumentNullException>(() => JsonCollectionCodec.Decode(null!, Bytes("null")));
        var limit = JsonCollectionShape.Integer();
        for (var i = 0; i < 63; i++) limit = JsonCollectionShape.List(limit);
        Assert.Equal(64, limit.Height);
        Assert.Throws<ArgumentException>(() => JsonCollectionShape.List(limit));
    }
}
