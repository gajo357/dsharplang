using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonContractCatalogTests
{
    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);

    [Fact]
    public void CatalogRoutesExplicitVersionsAndKindsThroughReferenceStore()
    {
        var firstSchema = new JsonScalarRecordSchema([new("value", JsonRecordFieldKind.String)]);
        var secondSchema = new JsonScalarRecordSchema([new("value", JsonRecordFieldKind.String)]);
        var unionSchema = new JsonTaggedUnionSchema([new("Message", firstSchema)]);
        var firstId = new JsonContractIdentity(Bytes("record-v1"));
        var secondId = new JsonContractIdentity(Bytes("record-v2"));
        var unionId = new JsonContractIdentity(Bytes("union-v1"));
        var catalog = new JsonContractCatalog([
            JsonContractRegistration.ForRecord(firstId, firstSchema), JsonContractRegistration.ForRecord(secondId, secondSchema),
            JsonContractRegistration.ForUnion(unionId, unionSchema)]);
        var first = new JsonRecordContract(firstId, firstSchema).Encode(JsonScalarRecord.Create(firstSchema, ["first"], [true]));
        var second = new JsonRecordContract(secondId, secondSchema).Encode(JsonScalarRecord.Create(secondSchema, ["second"], [true]));
        var union = new JsonUnionContract(unionId, unionSchema).Encode(JsonTaggedUnion.Create(unionSchema, 0, JsonScalarRecord.Create(firstSchema, ["nested"], [true])));
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        var request = new TargetedRequest("app", "submission", TargetedOperation.Invoke, "actor", first.Payload, first.Identity, []);
        Assert.Equal(TargetedAcceptKind.Created, store.Accept(request, Guid.NewGuid(), "selected").Kind);
        var saved = store.Lookup("app", "submission")!.Request;
        var decoded = catalog.DecodeRecord(saved.ContractMetadata, saved.Payload);
        Assert.True(decoded.IsSuccess); Assert.Same(firstSchema, decoded.Value.Value.Schema); Assert.Equal("first", decoded.Value.Value.ValueAt(0));
        var secondDecoded = catalog.DecodeRecord(second.Identity, second.Payload);
        Assert.True(secondDecoded.IsSuccess); Assert.Same(secondSchema, secondDecoded.Value.Value.Schema);
        var unionDecoded = catalog.DecodeUnion(union.Identity, union.Payload);
        Assert.True(unionDecoded.IsSuccess); Assert.Equal("nested", unionDecoded.Value.Value.Payload!.ValueAt(0));
        Assert.False(store.IsDurable);
    }

    [Fact]
    public void DuplicateIdentitiesAreRejectedAcrossShapesAndKinds()
    {
        var first = new JsonContractIdentity(Bytes("same")); var duplicate = new JsonContractIdentity(Bytes("same"));
        var firstSchema = new JsonScalarRecordSchema([]); var secondSchema = new JsonScalarRecordSchema([new("v", JsonRecordFieldKind.Integer)]);
        var record = JsonContractRegistration.ForRecord(first, firstSchema);
        Assert.Throws<ArgumentException>(() => new JsonContractCatalog([record, record]));
        Assert.Throws<ArgumentException>(() => new JsonContractCatalog([record, JsonContractRegistration.ForRecord(duplicate, secondSchema)]));
        Assert.Throws<ArgumentException>(() => new JsonContractCatalog([record, JsonContractRegistration.ForUnion(duplicate, new JsonTaggedUnionSchema([new("Empty")]))]));
        Assert.Throws<ArgumentException>(() => new JsonContractCatalog([record, null!]));
        Assert.Throws<ArgumentNullException>(() => new JsonContractCatalog(null!));
        Assert.Throws<ArgumentNullException>(() => JsonContractRegistration.ForRecord(null!, firstSchema));
        Assert.Throws<ArgumentNullException>(() => JsonContractRegistration.ForUnion(first, null!));
    }

    [Fact]
    public void UnknownAndWrongKindFailBeforeParsingWithoutEchoingInput()
    {
        var recordId = new JsonContractIdentity(Bytes("record")); var unionId = new JsonContractIdentity(Bytes("union"));
        var schema = new JsonScalarRecordSchema([new("value", JsonRecordFieldKind.String)]);
        var catalog = new JsonContractCatalog([JsonContractRegistration.ForRecord(recordId, schema),
            JsonContractRegistration.ForUnion(unionId, new JsonTaggedUnionSchema([new("Empty")]))]);
        var malformed = Bytes("not-json secret-value");
        var unknown = catalog.DecodeRecord(Bytes("secret-id"), malformed);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.ContractMismatch, "$"), unknown.Error);
        Assert.DoesNotContain("secret", unknown.Error!.Value.ToString(), StringComparison.Ordinal);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.ContractMismatch, "$"), catalog.DecodeRecord(unionId.Bytes, malformed).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.ContractMismatch, "$"), catalog.DecodeUnion(recordId.Bytes, malformed).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.ContractMismatch, "$"), catalog.DecodeRecord(ReadOnlyMemory<byte>.Empty, malformed).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidJson, "$"), catalog.DecodeRecord(recordId.Bytes, malformed).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"value\"]"), catalog.DecodeRecord(recordId.Bytes, Bytes("{\"value\":1}")).Error);
    }

    [Fact]
    public void CatalogSnapshotsConfigurationAndSupportsConcurrentReads()
    {
        var bytes = Bytes("record-v1"); var id = new JsonContractIdentity(bytes);
        var schema = new JsonScalarRecordSchema([new("value", JsonRecordFieldKind.String)]);
        JsonContractRegistration[] entries = [JsonContractRegistration.ForRecord(id, schema)];
        var catalog = new JsonContractCatalog(entries);
        Array.Fill(bytes, (byte)'x'); entries[0] = JsonContractRegistration.ForRecord(new JsonContractIdentity(Bytes("other")), schema);
        var metadata = Bytes("record-v1"); var payload = Bytes("{\"value\":\"ok\"}");
        Parallel.For(0, 128, _ => { var result = catalog.DecodeRecord(metadata, payload); Assert.True(result.IsSuccess); Assert.Equal("ok", result.Value.Value.ValueAt(0)); });
        Array.Fill(metadata, (byte)'x'); Array.Fill(payload, (byte)'x');
        Assert.True(catalog.DecodeRecord(Bytes("record-v1"), Bytes("{\"value\":\"ok\"}")).IsSuccess);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.ContractMismatch, "$"), catalog.DecodeRecord(Bytes("other"), Bytes("{}" )).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.ContractMismatch, "$"), new JsonContractCatalog([]).DecodeRecord(Bytes("record-v1"), Bytes("{}" )).Error);
    }
}
