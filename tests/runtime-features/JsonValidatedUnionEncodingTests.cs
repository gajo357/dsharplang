using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonValidatedUnionEncodingTests
{
    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);
    private static string Text(byte[] value) => Encoding.UTF8.GetString(value);

    [Fact]
    public void TypedUnionEncodesAndSurvivesTestOwnedReferenceHandoff()
    {
        var fields = new JsonScalarRecordSchema([
            new("amount", JsonRecordFieldKind.ExactNumber),
            new("note", JsonRecordFieldKind.NullableString, Optional: true),
            new("data", JsonRecordFieldKind.FieldValue),
            new("missing", JsonRecordFieldKind.NullableString, Optional: true)]);
        var schema = new JsonTaggedUnionSchema([new("Place", fields)]);
        var number = JsonExactNumberCodec.Decode(Bytes("9007199254740993.00e+2")).Value;
        var treeItems = new[] { JsonFieldValue.Boolean(true), JsonFieldValue.Null() };
        var mapItems = new[] { new KeyValuePair<string, JsonFieldValue>("items", JsonFieldValue.List(treeItems)) };
        object?[] values = [number, null, JsonFieldValue.Map(mapItems), null];
        bool[] present = [true, true, true, false];
        var record = JsonScalarRecord.Create(fields, values, present);
        var union = JsonTaggedUnion.Create(schema, 0, record);
        var payload = JsonValidatedUnionPayload.FromValue(union);
        Assert.Same(union, payload.Value);
        Assert.Same(schema, payload.Value.Schema);
        treeItems[0] = JsonFieldValue.Boolean(false);
        mapItems[0] = new("changed", JsonFieldValue.Null());
        values[0] = null;
        present[1] = false;

        const string expected = "{\"case\":\"Place\",\"value\":{\"amount\":9007199254740993.00e+2,\"note\":null,\"data\":{\"items\":[true,null]}}}";
        var exposed = payload.Bytes;
        Assert.Equal(expected, Text(exposed));
        exposed[0] = 0;
        Assert.Equal(expected, Text(payload.Bytes));
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        var metadata = Bytes("test-schema-v1");
        var request = new TargetedRequest("app", "submission", TargetedOperation.Invoke,
            "actor", payload.Bytes, metadata, [7]);
        Array.Fill(metadata, (byte)'x');
        var id = Guid.NewGuid();
        Assert.Equal(TargetedAcceptKind.Created, store.Accept(request, id, "selected-v1").Kind);
        Assert.Equal(TargetedAcceptKind.Existing, store.Accept(new TargetedRequest("app", "submission", TargetedOperation.Invoke,
            "actor", payload.Bytes, Bytes("test-schema-v1"), [7]), Guid.NewGuid(), "other").Kind);
        var saved = store.Lookup("app", "submission")!;
        Assert.Equal(id, saved.InvocationId);
        Assert.Equal(expected, Text(saved.Request.Payload));
        Assert.Equal("test-schema-v1", Text(saved.Request.ContractMetadata));
        var recovered = JsonValidatedUnionPayload.Decode(schema, saved.Request.Payload);
        Assert.True(recovered.IsSuccess);
        Assert.Same(schema, recovered.Value.Value.Schema);
        Assert.True(recovered.Value.Value.Payload!.IsPresent(1));
        Assert.Null(recovered.Value.Value.Payload.ValueAt(1));
        Assert.False(recovered.Value.Value.Payload.IsPresent(3));
        Assert.Equal("9007199254740993.00e+2", Assert.IsType<JsonExactNumber>(recovered.Value.Value.Payload.ValueAt(0)).Token);
        Assert.Equal(expected, Text(recovered.Value.Bytes));
        Assert.False(store.IsDurable);
    }

    [Fact]
    public void EncodingRejectsInvalidConstructionAndDecodeErrorsStayRedacted()
    {
        Assert.Throws<ArgumentNullException>(() => JsonValidatedUnionPayload.FromValue(null!));
        var fields = new JsonScalarRecordSchema([new("number", JsonRecordFieldKind.Integer)]);
        var other = new JsonScalarRecordSchema([new("number", JsonRecordFieldKind.Integer)]);
        var schema = new JsonTaggedUnionSchema([new("Data", fields)]);
        var wrongRecord = JsonScalarRecord.Create(other, [1], [true]);
        Assert.Throws<ArgumentException>(() => JsonTaggedUnion.Create(schema, 0, wrongRecord));
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(fields, ["secret-value"], [true]));
        var decoded = JsonValidatedUnionPayload.Decode(schema, Bytes("{\"case\":\"Data\",\"value\":{\"number\":\"secret-value\"}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"value\"][\"number\"]"), decoded.Error);
        Assert.DoesNotContain("secret-value", decoded.Error!.Value.ToString(), StringComparison.Ordinal);
    }
}
