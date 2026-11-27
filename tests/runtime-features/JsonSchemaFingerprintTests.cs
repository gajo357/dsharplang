using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonSchemaFingerprintTests
{
    [Fact]
    public void GoldenVectorAndIndependentDescriptorsAreStable()
    {
        var first = new JsonScalarRecordSchema([new("value", JsonRecordFieldKind.Integer)]);
        var second = new JsonScalarRecordSchema([new("value", JsonRecordFieldKind.Integer)]);
        var fingerprint = JsonSchemaFingerprint.ForRecord(first);
        Assert.Equal("8D03212193C9326EFB87B097F34227D2FE23C969DDF1D67CCAF19988C83BD655", Convert.ToHexString(fingerprint));
        Assert.Equal(fingerprint, JsonSchemaFingerprint.ForRecord(second));
        Array.Fill(fingerprint, (byte)0);
        Assert.Equal("8D03212193C9326EFB87B097F34227D2FE23C969DDF1D67CCAF19988C83BD655", Convert.ToHexString(JsonSchemaFingerprint.ForRecord(first)));
        Assert.Throws<ArgumentNullException>(() => JsonSchemaFingerprint.ForRecord(null!));
        Assert.Throws<ArgumentNullException>(() => JsonSchemaFingerprint.ForUnion(null!));
    }

    [Fact]
    public void EveryDescriptorDimensionAffectsStrictFingerprint()
    {
        byte[] Hash(params JsonRecordField[] fields) => JsonSchemaFingerprint.ForRecord(new JsonScalarRecordSchema(fields));
        var original = Hash(new("a", JsonRecordFieldKind.NullableString), new("b", JsonRecordFieldKind.Integer));
        Assert.NotEqual(original, Hash(new("a", JsonRecordFieldKind.NullableString, Optional: true), new("b", JsonRecordFieldKind.Integer)));
        Assert.NotEqual(original, Hash(new("a", JsonRecordFieldKind.String), new("b", JsonRecordFieldKind.Integer)));
        Assert.NotEqual(original, Hash(new("b", JsonRecordFieldKind.Integer), new("a", JsonRecordFieldKind.NullableString)));
        Assert.NotEqual(original, Hash(new("a", JsonRecordFieldKind.NullableString), new("c", JsonRecordFieldKind.Integer)));
        Assert.NotEqual(Hash(new("ab", JsonRecordFieldKind.Integer), new("c", JsonRecordFieldKind.String)),
            Hash(new("a", JsonRecordFieldKind.Integer), new("bc", JsonRecordFieldKind.String)));
        Assert.NotEqual(Hash(new JsonRecordField("é", JsonRecordFieldKind.String)), Hash(new JsonRecordField("e\u0301", JsonRecordFieldKind.String)));
        Assert.NotEqual(Hash(), JsonSchemaFingerprint.ForUnion(new JsonTaggedUnionSchema([new("Empty")])));
    }

    [Fact]
    public void NestedRecordUnionAndCollectionChangesAreDetected()
    {
        JsonScalarRecordSchema Child(JsonRecordFieldKind kind) => new([new("amount", kind)]);
        JsonTaggedUnionSchema Choice(JsonScalarRecordSchema payload) => new([new("Empty"), new("Value", payload)]);
        JsonScalarRecordSchema Root(JsonTaggedUnionSchema choice) => new([new("items", JsonRecordFieldKind.UnionNullableValuesMap, UnionSchema: choice)]);
        var left = Root(Choice(Child(JsonRecordFieldKind.DecimalArray)));
        var equal = Root(Choice(Child(JsonRecordFieldKind.DecimalArray)));
        var changed = Root(Choice(Child(JsonRecordFieldKind.NullableDecimalArray)));
        Assert.Equal(JsonSchemaFingerprint.ForRecord(left), JsonSchemaFingerprint.ForRecord(equal));
        Assert.NotEqual(JsonSchemaFingerprint.ForRecord(left), JsonSchemaFingerprint.ForRecord(changed));
        var cases = Choice(Child(JsonRecordFieldKind.Decimal));
        var reordered = new JsonTaggedUnionSchema([new("Value", Child(JsonRecordFieldKind.Decimal)), new("Empty")]);
        Assert.NotEqual(JsonSchemaFingerprint.ForUnion(cases), JsonSchemaFingerprint.ForUnion(reordered));
        Assert.NotEqual(JsonSchemaFingerprint.ForUnion(cases), JsonSchemaFingerprint.ForUnion(new JsonTaggedUnionSchema([new("Empty"), new("Value")])));
    }

    [Fact]
    public void DeepSharedDescriptorsAreMemoizedAndCanBindCatalog()
    {
        var child = new JsonScalarRecordSchema([new("value", JsonRecordFieldKind.Integer)]);
        for (var depth = 0; depth < 60; depth++)
            child = new JsonScalarRecordSchema([new("left", JsonRecordFieldKind.Record, NestedSchema: child), new("right", JsonRecordFieldKind.Record, NestedSchema: child)]);
        Assert.Equal(61, child.Depth);
        var bytes = JsonSchemaFingerprint.ForRecord(child); Assert.Equal(32, bytes.Length);
        var metadata = "test-logical-contract-v1:"u8.ToArray().Concat(bytes).ToArray();
        var identity = new JsonContractIdentity(metadata);
        var catalog = new JsonContractCatalog([JsonContractRegistration.ForRecord(identity, child)]);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidJson, "$"), catalog.DecodeRecord(metadata, "{"u8.ToArray()).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.ContractMismatch, "$"), catalog.DecodeRecord(new byte[32], "{"u8.ToArray()).Error);
    }
}
