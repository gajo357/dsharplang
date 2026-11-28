using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonNamespacedSchemaIdentityTests
{
    [Fact]
    public void IndependentlyConstructedSchemasAndNamespacesProduceOwnedStableIdentity()
    {
        var name = "orders/v1"u8.ToArray();
        var schema = new JsonScalarRecordSchema([new("count", JsonRecordFieldKind.Integer)]);
        var identity = JsonNamespacedSchemaIdentity.ForRecord(name, schema);
        var expected = identity.Bytes;
        Assert.Equal("4453686172702E4A736F6E4E616D65737061636564536368656D614964656E746974792E7631000000096F72646572732F763112D6989F7526AF3B336B32EEE1766E248E28FD0A2FC3C44FE677138F7B8301FA", Convert.ToHexString(expected));
        var independentlyConstructed = new JsonScalarRecordSchema([new("count", JsonRecordFieldKind.Integer)]);
        Assert.Equal(expected, JsonNamespacedSchemaIdentity.ForRecord("orders/v1"u8.ToArray(), independentlyConstructed).Bytes);
        Array.Fill(name, (byte)0); var returned = identity.Bytes; Array.Fill(returned, (byte)0);
        Assert.Equal(expected, identity.Bytes);
        Assert.Throws<ArgumentNullException>(() => JsonNamespacedSchemaIdentity.ForRecord(null!, schema));
        Assert.Throws<ArgumentException>(() => JsonNamespacedSchemaIdentity.ForRecord([], schema));
        Assert.Throws<ArgumentNullException>(() => JsonNamespacedSchemaIdentity.ForRecord("name"u8.ToArray(), null!));
        Assert.Throws<ArgumentException>(() => JsonNamespacedSchemaIdentity.ForUnion([], new JsonTaggedUnionSchema([new("Empty")])));
    }

    [Fact]
    public void NamespaceKindAndNestedShapeAreIndependentInputs()
    {
        JsonScalarRecordSchema Child(JsonRecordFieldKind kind) => new([new("value", kind)]);
        JsonScalarRecordSchema Root(JsonRecordFieldKind kind) => new([new("child", JsonRecordFieldKind.Record, NestedSchema: Child(kind))]);
        var name = "orders"u8.ToArray(); var root = Root(JsonRecordFieldKind.Integer);
        var identity = JsonNamespacedSchemaIdentity.ForRecord(name, root).Bytes;
        Assert.NotEqual(identity, JsonNamespacedSchemaIdentity.ForRecord("orders2"u8.ToArray(), root).Bytes);
        Assert.NotEqual(identity, JsonNamespacedSchemaIdentity.ForRecord(name, Root(JsonRecordFieldKind.String)).Bytes);
        Assert.NotEqual(identity, JsonNamespacedSchemaIdentity.ForUnion(name, new JsonTaggedUnionSchema([new("Empty")])).Bytes);
        Assert.Equal(JsonNamespacedSchemaIdentity.ForUnion(name, new JsonTaggedUnionSchema([new("Empty")])).Bytes,
            JsonNamespacedSchemaIdentity.ForUnion("orders"u8.ToArray(), new JsonTaggedUnionSchema([new("Empty")])).Bytes);
        Assert.NotEqual(JsonNamespacedSchemaIdentity.ForRecord("a"u8.ToArray(), root).Bytes,
            JsonNamespacedSchemaIdentity.ForRecord("a\0"u8.ToArray(), root).Bytes);
    }

    [Fact]
    public void CatalogRejectsDifferentNamespaceOrShapeBeforeParsing()
    {
        var schema = new JsonScalarRecordSchema([new("count", JsonRecordFieldKind.Integer)]);
        var identity = JsonNamespacedSchemaIdentity.ForRecord("orders/v1"u8.ToArray(), schema);
        var catalog = new JsonContractCatalog([JsonContractRegistration.ForRecord(identity, schema)]);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidJson, "$"), catalog.DecodeRecord(identity.Bytes, "{"u8.ToArray()).Error);
        var otherName = JsonNamespacedSchemaIdentity.ForRecord("other/v1"u8.ToArray(), schema);
        var changed = new JsonScalarRecordSchema([new("count", JsonRecordFieldKind.NullableInteger)]);
        var otherShape = JsonNamespacedSchemaIdentity.ForRecord("orders/v1"u8.ToArray(), changed);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.ContractMismatch, "$"), catalog.DecodeRecord(otherName.Bytes, "{secret"u8.ToArray()).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.ContractMismatch, "$"), catalog.DecodeRecord(otherShape.Bytes, "{secret"u8.ToArray()).Error);
    }
}
