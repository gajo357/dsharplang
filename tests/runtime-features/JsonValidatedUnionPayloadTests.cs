using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonValidatedUnionPayloadTests
{
    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);
    private static string Text(byte[] value) => Encoding.UTF8.GetString(value);
    private static JsonTaggedUnionSchema Schema() => new([new("Place", new JsonScalarRecordSchema([
        new("amount", JsonRecordFieldKind.ExactNumber),
        new("owner", JsonRecordFieldKind.NullableString, Optional: true),
        new("data", JsonRecordFieldKind.FieldValue)]))]);

    // This caller and its metadata are test-owned. The volatile reference does
    // not itself validate or adopt a durable schema or admission contract.
    private static JsonScalarResult<JsonValidatedUnionPayload> ValidateAndStore(
        VolatileTargetedStore store, JsonTaggedUnionSchema schema, byte[] input)
    {
        var result = JsonValidatedUnionPayload.Decode(schema, input);
        if (result.IsSuccess)
        {
            var request = new TargetedRequest("app", "submission", TargetedOperation.Invoke,
                "actor", result.Value.Bytes, Bytes("test-schema-v1"), [7]);
            Assert.Equal(TargetedAcceptKind.Created, store.Accept(request, Guid.NewGuid(), "selected").Kind);
        }
        return result;
    }

    [Fact]
    public void ValidationOwnsTheExactBytesAndBindsTheDecodedDescriptor()
    {
        const string json = " { \"value\" : { \"data\":{\"items\":[true,null]}, \"owner\":null, \"amount\":9007199254740993.00e+2 }, \"case\":\"Place\" } ";
        var schema = Schema();
        var input = Bytes(json);
        var result = JsonValidatedUnionPayload.Decode(schema, input);
        Assert.True(result.IsSuccess);
        Assert.Same(schema, result.Value.Value.Schema);
        Assert.True(result.Value.Value.Payload!.IsPresent(1));
        Assert.Null(result.Value.Value.Payload.ValueAt(1));
        Assert.Equal("9007199254740993.00e+2", Assert.IsType<JsonExactNumber>(result.Value.Value.Payload.ValueAt(0)).Token);
        Array.Fill(input, (byte)'x');
        var exposed = result.Value.Bytes;
        exposed[0] = 0;
        Assert.Equal(json, Text(result.Value.Bytes));
        var decodedAgain = JsonTaggedUnionCodec.Decode(schema, result.Value.Bytes);
        Assert.True(decodedAgain.IsSuccess);
        Assert.Equal("9007199254740993.00e+2", Assert.IsType<JsonExactNumber>(decodedAgain.Value.Payload!.ValueAt(0)).Token);
    }

    [Fact]
    public void ValidatedSnapshotCanBeHandedToTheReferenceAndReadBack()
    {
        const string json = "{\"case\":\"Place\",\"value\":{\"amount\":-0.00,\"data\":[1,true]}}";
        var schema = Schema();
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        var input = Bytes(json);
        var result = ValidateAndStore(store, schema, input);
        Assert.True(result.IsSuccess);
        Array.Fill(input, (byte)'x');
        result.Value.Bytes[0] = 0;
        var saved = store.Lookup("app", "submission")!;
        Assert.Equal(json, Text(saved.Request.Payload));
        Assert.Equal("test-schema-v1", Text(saved.Request.ContractMetadata));
        var recovered = JsonValidatedUnionPayload.Decode(schema, saved.Request.Payload);
        Assert.True(recovered.IsSuccess);
        Assert.Same(schema, recovered.Value.Value.Schema);
        Assert.False(recovered.Value.Value.Payload!.IsPresent(1));
        Assert.Equal("-0.00", Assert.IsType<JsonExactNumber>(recovered.Value.Value.Payload.ValueAt(0)).Token);
        Assert.False(store.IsDurable);
    }

    [Fact]
    public void InvalidInputsNeverReachTheTestOwnedHandoffAndErrorsStayRedacted()
    {
        var schema = Schema();
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        var invalids = new[] {
            ("{", new JsonScalarError(JsonScalarErrorCode.InvalidJson, "$")),
            ("{\"case\":\"Place\",\"value\":{\"amount\":\"secret-value\",\"data\":null}}", new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"value\"][\"amount\"]")),
            ("{\"case\":\"Place\",\"value\":{\"secret-key\":1,\"amount\":1,\"data\":null}}", new JsonScalarError(JsonScalarErrorCode.UnknownField, "$[\"value\"]")),
            ("{\"case\":\"Place\",\"value\":{\"data\":null}}", new JsonScalarError(JsonScalarErrorCode.MissingField, "$[\"value\"][\"amount\"]"))
        };
        foreach (var (json, expected) in invalids)
        {
            var result = ValidateAndStore(store, schema, Bytes(json));
            Assert.False(result.IsSuccess);
            Assert.Equal(expected, result.Error);
            Assert.DoesNotContain("secret", result.Error!.Value.ToString(), StringComparison.Ordinal);
            Assert.Null(store.Lookup("app", "submission"));
        }
        Assert.Throws<ArgumentNullException>(() => JsonValidatedUnionPayload.Decode(null!, Bytes("{}")));
    }
}
