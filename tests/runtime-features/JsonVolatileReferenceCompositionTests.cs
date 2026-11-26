using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

// The metadata and orchestration here belong to the test. This is a volatile
// byte store, not a durable provider or an adopted JSON message contract.
public sealed class JsonVolatileReferenceCompositionTests
{
    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);
    private static string Text(byte[] value) => Encoding.UTF8.GetString(value);
    private static JsonTaggedUnionSchema Schema() => new([new("Place", new JsonScalarRecordSchema([
        new("amount", JsonRecordFieldKind.ExactNumber),
        new("owner", JsonRecordFieldKind.NullableString, Optional: true),
        new("data", JsonRecordFieldKind.FieldValue)]))]);
    private static TargetedRequest Request(byte[] payload, byte[] metadata) =>
        new("app", "submission", TargetedOperation.Invoke, "actor", payload, metadata, [7]);

    [Fact]
    public void EncodedNestedDataSurvivesReferenceSnapshotAndMatchingRetry()
    {
        const string json = "{\"case\":\"Place\",\"value\":{\"amount\":9007199254740993.00e+2,\"owner\":null,\"data\":{\"private\":[true,null,{\"nested\":-0.00}]}}}";
        var schema = Schema();
        var parsed = JsonTaggedUnionCodec.Decode(schema, Bytes(json));
        Assert.True(parsed.IsSuccess);
        var payload = JsonTaggedUnionCodec.Encode(parsed.Value);
        var metadata = Bytes("test-schema-v1");
        var request = Request(payload, metadata);
        Array.Fill(payload, (byte)'x');
        Array.Fill(metadata, (byte)'x');
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        var id = Guid.NewGuid();
        var accepted = store.Accept(request, id, "selected-v1");
        Assert.Equal(TargetedAcceptKind.Created, accepted.Kind);
        Assert.False(store.IsDurable);

        var retry = store.Accept(Request(Bytes(json), Bytes("test-schema-v1")), Guid.NewGuid(), "selected-v2");
        Assert.Equal(TargetedAcceptKind.Existing, retry.Kind);
        Assert.Equal(id, retry.Record!.InvocationId);
        Assert.Equal("selected-v1", retry.Record.SelectedImplementation);
        var stored = store.Lookup("app", "submission")!;
        Assert.Equal(json, Text(stored.Request.Payload));
        Assert.Equal("test-schema-v1", Text(stored.Request.ContractMetadata));
        stored.Request.Payload[0] = 0;
        stored.Request.ContractMetadata[0] = 0;
        var decoded = JsonTaggedUnionCodec.Decode(schema, store.Lookup("app", "submission")!.Request.Payload);
        Assert.True(decoded.IsSuccess);
        Assert.True(decoded.Value.Payload!.IsPresent(1));
        Assert.Null(decoded.Value.Payload.ValueAt(1));
        Assert.Equal(json, Text(JsonTaggedUnionCodec.Encode(decoded.Value)));
        var tree = JsonTypedFieldMapping.FromUnion(decoded.Value);
        Assert.True(tree.IsSuccess);
        Assert.Equal(json, Text(JsonTaggedUnionCodec.Encode(JsonTypedFieldMapping.ToUnion(schema, tree.Value).Value)));
    }

    [Fact]
    public void ReferenceComparesPayloadAndOpaqueMetadataBytesRatherThanNumericMeaning()
    {
        const string first = "{\"case\":\"Place\",\"value\":{\"amount\":1.00,\"data\":null}}";
        const string alternateSpelling = "{\"case\":\"Place\",\"value\":{\"amount\":1.0,\"data\":null}}";
        var schema = Schema();
        Assert.True(JsonTaggedUnionCodec.Decode(schema, Bytes(first)).IsSuccess);
        Assert.True(JsonTaggedUnionCodec.Decode(schema, Bytes(alternateSpelling)).IsSuccess);
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        var id = Guid.NewGuid();
        Assert.Equal(TargetedAcceptKind.Created, store.Accept(Request(Bytes(first), Bytes("test-schema-v1")), id, "selected").Kind);
        Assert.Equal(TargetedAcceptKind.Conflict, store.Accept(Request(Bytes(alternateSpelling), Bytes("test-schema-v1")), Guid.NewGuid(), "other").Kind);
        Assert.Equal(TargetedAcceptKind.Conflict, store.Accept(Request(Bytes(first), Bytes("test-schema-v2")), Guid.NewGuid(), "other").Kind);
        var stored = store.Lookup("app", "submission")!;
        Assert.Equal(id, stored.InvocationId);
        Assert.Equal(first, Text(stored.Request.Payload));
        Assert.False(JsonTaggedUnionCodec.Decode(schema, stored.Request.Payload).Value.Payload!.IsPresent(1));
    }

    [Fact]
    public void ByteStoreDoesNotValidateJsonAndLaterDecodeIsRedacted()
    {
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        var invalid = Bytes("{\"case\":\"Place\",\"value\":{\"amount\":\"secret-value\",\"data\":null}}");
        Assert.Equal(TargetedAcceptKind.Created, store.Accept(Request(invalid, Bytes("test-schema-v1")), Guid.NewGuid(), "selected").Kind);
        var decoded = JsonTaggedUnionCodec.Decode(Schema(), store.Lookup("app", "submission")!.Request.Payload);
        Assert.False(decoded.IsSuccess);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"value\"][\"amount\"]"), decoded.Error);
        Assert.DoesNotContain("secret-value", decoded.Error!.Value.ToString(), StringComparison.Ordinal);
    }
}
