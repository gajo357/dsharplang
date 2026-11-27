using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonContractBindingTests
{
    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);

    [Fact]
    public void RecordContractSnapshotsIdentityAndPayloadAcrossReferenceStore()
    {
        var schema = new JsonScalarRecordSchema([new("name", JsonRecordFieldKind.String), new("value", JsonRecordFieldKind.Decimal)]);
        var identityBytes = Bytes("application-message-v1");
        var identity = new JsonContractIdentity(identityBytes);
        var contract = new JsonRecordContract(identity, schema);
        var record = JsonScalarRecord.Create(schema, ["item", 9007199254740993.125m], [true, true]);
        var encoded = contract.Encode(record);
        Array.Fill(identityBytes, (byte)'x');
        var metadata = encoded.Identity; var payload = encoded.Payload;
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        var request = new TargetedRequest("app", "submission", TargetedOperation.Invoke, "actor", payload, metadata, []);
        Assert.Equal(TargetedAcceptKind.Created, store.Accept(request, Guid.NewGuid(), "selected").Kind);
        Array.Fill(metadata, (byte)'x'); Array.Fill(payload, (byte)'x');
        var captured = store.Lookup("app", "submission")!.Request;
        var decoded = contract.Decode(captured.ContractMetadata, captured.Payload);
        Assert.True(decoded.IsSuccess);
        Assert.Equal("item", decoded.Value.Value.ValueAt(0));
        Assert.Equal(9007199254740993.125m, decoded.Value.Value.ValueAt(1));
        var exported = encoded.Identity; Array.Fill(exported, (byte)'x');
        var exportedPayload = encoded.Payload; Array.Fill(exportedPayload, (byte)'x');
        Assert.True(contract.Decode(encoded.Identity, encoded.Payload).IsSuccess);
        Assert.Equal("application-message-v1", Encoding.UTF8.GetString(identity.Bytes));
        var identityCopy = identity.Bytes; Array.Fill(identityCopy, (byte)'x');
        Assert.Equal("application-message-v1", Encoding.UTF8.GetString(identity.Bytes));
        Assert.False(store.IsDurable);
    }

    [Fact]
    public void SameShapeDifferentVersionIsRejectedBeforeParsing()
    {
        var a = new JsonScalarRecordSchema([new("name", JsonRecordFieldKind.String)]);
        var b = new JsonScalarRecordSchema([new("name", JsonRecordFieldKind.String)]);
        var first = new JsonRecordContract(new JsonContractIdentity(Bytes("message-v1")), a);
        var second = new JsonRecordContract(new JsonContractIdentity(Bytes("message-v2")), b);
        var encoded = first.Encode(JsonScalarRecord.Create(a, ["value"], [true]));
        Assert.True(first.Decode(encoded.Identity, encoded.Payload).IsSuccess);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.ContractMismatch, "$"), second.Decode(encoded.Identity, encoded.Payload).Error);
        var mismatch = second.Decode(Bytes("secret-contract"), Bytes("not valid JSON secret-value"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.ContractMismatch, "$"), mismatch.Error);
        Assert.DoesNotContain("secret", mismatch.Error!.Value.ToString(), StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => first.Encode(JsonScalarRecord.Create(b, ["value"], [true])));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"name\"]"), first.Decode(Bytes("message-v1"), Bytes("{\"name\":42}")).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidJson, "$"), first.Decode(Bytes("message-v1"), Bytes("{" )).Error);
    }

    [Fact]
    public void UnionContractEnforcesDescriptorAndIdentity()
    {
        var fields = new JsonScalarRecordSchema([new("note", JsonRecordFieldKind.NullableString)]);
        var schema = new JsonTaggedUnionSchema([new("Value", fields)]);
        var otherSchema = new JsonTaggedUnionSchema([new("Value", fields)]);
        var contract = new JsonUnionContract(new JsonContractIdentity(Bytes("union-v1")), schema);
        var union = JsonTaggedUnion.Create(schema, 0, JsonScalarRecord.Create(fields, [null], [true]));
        var encoded = contract.Encode(union);
        var decoded = contract.Decode(encoded.Identity, encoded.Payload);
        Assert.True(decoded.IsSuccess); Assert.Null(decoded.Value.Value.Payload!.ValueAt(0));
        Assert.Throws<ArgumentException>(() => contract.Encode(JsonTaggedUnion.Create(otherSchema, 0, JsonScalarRecord.Create(fields, [null], [true]))));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.ContractMismatch, "$"), contract.Decode(Bytes("union-v2"), encoded.Payload).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"value\"][\"note\"]"), contract.Decode(Bytes("union-v1"), Bytes("{\"case\":\"Value\",\"value\":{\"note\":1}}" )).Error);
    }

    [Fact]
    public void EmptyAndNullContractInputsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new JsonContractIdentity(null!));
        Assert.Throws<ArgumentException>(() => new JsonContractIdentity([]));
        var id = new JsonContractIdentity([1]);
        var record = new JsonScalarRecordSchema([]); var union = new JsonTaggedUnionSchema([new("Empty")]);
        Assert.Throws<ArgumentNullException>(() => new JsonRecordContract(null!, record));
        Assert.Throws<ArgumentNullException>(() => new JsonRecordContract(id, null!));
        Assert.Throws<ArgumentNullException>(() => new JsonUnionContract(null!, union));
        Assert.Throws<ArgumentNullException>(() => new JsonUnionContract(id, null!));
        Assert.Throws<ArgumentNullException>(() => new JsonRecordContract(id, record).Encode(null!));
        Assert.Throws<ArgumentNullException>(() => new JsonUnionContract(id, union).Encode(null!));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.ContractMismatch, "$"), new JsonRecordContract(id, record).Decode(ReadOnlyMemory<byte>.Empty, Bytes("{}" )).Error);
    }
}
