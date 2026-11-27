using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonContractCatalogEncodeTests
{
    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);

    [Fact]
    public void RecordEncodingUsesRegisteredIdentityAndOwnedBytes()
    {
        var sourceIdentity = Bytes("record-v1"); var id = new JsonContractIdentity(sourceIdentity);
        var schema = new JsonScalarRecordSchema([new("value", JsonRecordFieldKind.Decimal)]);
        var catalog = new JsonContractCatalog([JsonContractRegistration.ForRecord(id, schema)]);
        var record = JsonScalarRecord.Create(schema, [9007199254740993.125m], [true]);
        var metadata = Bytes("record-v1"); var encoded = catalog.EncodeRecord(metadata, record);
        Assert.True(encoded.IsSuccess);
        Array.Fill(metadata, (byte)'x'); Array.Fill(sourceIdentity, (byte)'x');
        Assert.Equal("record-v1", Encoding.UTF8.GetString(encoded.Value.Identity));
        var identityCopy = encoded.Value.Identity; var payloadCopy = encoded.Value.Payload;
        Array.Fill(identityCopy, (byte)'x'); Array.Fill(payloadCopy, (byte)'x');
        var decoded = catalog.DecodeRecord(encoded.Value.Identity, encoded.Value.Payload);
        Assert.True(decoded.IsSuccess); Assert.Equal(9007199254740993.125m, decoded.Value.Value.ValueAt(0));
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        var request = new TargetedRequest("app", "submission", TargetedOperation.Invoke, "actor", encoded.Value.Payload, encoded.Value.Identity, []);
        Assert.Equal(TargetedAcceptKind.Created, store.Accept(request, Guid.NewGuid(), "selected").Kind);
        var saved = store.Lookup("app", "submission")!.Request;
        Assert.True(catalog.DecodeRecord(saved.ContractMetadata, saved.Payload).IsSuccess);
        Assert.False(store.IsDurable);
    }

    [Fact]
    public void UnionEncodingAndDecodingUseTheSameCatalogBinding()
    {
        var fields = new JsonScalarRecordSchema([new("note", JsonRecordFieldKind.NullableString)]);
        var schema = new JsonTaggedUnionSchema([new("Message", fields)]); var id = new JsonContractIdentity(Bytes("union-v1"));
        var catalog = new JsonContractCatalog([JsonContractRegistration.ForUnion(id, schema)]);
        var union = JsonTaggedUnion.Create(schema, 0, JsonScalarRecord.Create(fields, [null], [true]));
        var encoded = catalog.EncodeUnion(Bytes("union-v1"), union);
        Assert.True(encoded.IsSuccess); Assert.Equal("union-v1", Encoding.UTF8.GetString(encoded.Value.Identity));
        var decoded = catalog.DecodeUnion(encoded.Value.Identity, encoded.Value.Payload);
        Assert.True(decoded.IsSuccess); Assert.Same(schema, decoded.Value.Value.Schema); Assert.Null(decoded.Value.Value.Payload!.ValueAt(0));
    }

    [Fact]
    public void UnknownWrongKindAndSameShapeWrongDescriptorFailClosed()
    {
        var first = new JsonScalarRecordSchema([new("value", JsonRecordFieldKind.String)]);
        var second = new JsonScalarRecordSchema([new("value", JsonRecordFieldKind.String)]);
        var firstUnion = new JsonTaggedUnionSchema([new("Message", first)]);
        var secondUnion = new JsonTaggedUnionSchema([new("Message", first)]);
        var catalog = new JsonContractCatalog([JsonContractRegistration.ForRecord(new JsonContractIdentity(Bytes("record")), first),
            JsonContractRegistration.ForUnion(new JsonContractIdentity(Bytes("union")), firstUnion)]);
        var value = JsonScalarRecord.Create(first, ["secret-value"], [true]);
        var otherValue = JsonScalarRecord.Create(second, ["secret-value"], [true]);
        var union = JsonTaggedUnion.Create(firstUnion, 0, value);
        var otherUnion = JsonTaggedUnion.Create(secondUnion, 0, value);
        var mismatch = new JsonScalarError(JsonScalarErrorCode.ContractMismatch, "$");
        var unknown = catalog.EncodeRecord(Bytes("secret-id"), value);
        Assert.Equal(mismatch, unknown.Error); Assert.DoesNotContain("secret", unknown.Error!.Value.ToString(), StringComparison.Ordinal);
        Assert.Equal(mismatch, catalog.EncodeRecord(Bytes("union"), value).Error);
        Assert.Equal(mismatch, catalog.EncodeRecord(Bytes("record"), otherValue).Error);
        Assert.Equal(mismatch, catalog.EncodeUnion(Bytes("record"), union).Error);
        Assert.Equal(mismatch, catalog.EncodeUnion(Bytes("union"), otherUnion).Error);
        Assert.Equal(mismatch, catalog.EncodeUnion(ReadOnlyMemory<byte>.Empty, union).Error);
        Assert.Throws<ArgumentNullException>(() => catalog.EncodeRecord(Bytes("record"), null!));
        Assert.Throws<ArgumentNullException>(() => catalog.EncodeUnion(Bytes("union"), null!));
    }

    [Fact]
    public void ConcurrentEncodingKeepsDistinctExplicitVersions()
    {
        var first = new JsonScalarRecordSchema([new("v", JsonRecordFieldKind.String)]);
        var second = new JsonScalarRecordSchema([new("v", JsonRecordFieldKind.String)]);
        var catalog = new JsonContractCatalog([JsonContractRegistration.ForRecord(new JsonContractIdentity(Bytes("v1")), first),
            JsonContractRegistration.ForRecord(new JsonContractIdentity(Bytes("v2")), second)]);
        var one = JsonScalarRecord.Create(first, ["one"], [true]); var two = JsonScalarRecord.Create(second, ["two"], [true]);
        Parallel.For(0, 128, i =>
        {
            var version = i % 2 == 0 ? "v1" : "v2"; var value = i % 2 == 0 ? one : two;
            var result = catalog.EncodeRecord(Bytes(version), value); Assert.True(result.IsSuccess);
            Assert.Equal(version, Encoding.UTF8.GetString(result.Value.Identity));
            var decoded = catalog.DecodeRecord(result.Value.Identity, result.Value.Payload);
            Assert.True(decoded.IsSuccess); Assert.Same(value.Schema, decoded.Value.Value.Schema);
        });
    }
}
