using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonValidatedRecordPayloadTests
{
    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);
    private static string Text(byte[] value) => Encoding.UTF8.GetString(value);

    private static (JsonScalarRecordSchema Item, JsonScalarRecordSchema Root) Schemas()
    {
        var item = new JsonScalarRecordSchema([new("sku", JsonRecordFieldKind.String), new("count", JsonRecordFieldKind.Int64)]);
        var root = new JsonScalarRecordSchema([
            new("items", JsonRecordFieldKind.RecordArray, NestedSchema: item),
            new("amount", JsonRecordFieldKind.ExactNumber),
            new("note", JsonRecordFieldKind.NullableString, Optional: true),
            new("missing", JsonRecordFieldKind.NullableString, Optional: true)]);
        return (item, root);
    }

    [Fact]
    public void TypedRecordEncodesAndSurvivesTestOwnedReferenceHandoff()
    {
        var (itemSchema, schema) = Schemas();
        var item = JsonScalarRecord.Create(itemSchema, ["A", 9007199254740993L], [true, true]);
        var items = new[] { item };
        var number = JsonExactNumberCodec.Decode(Bytes("12345678901234567890.00e+2")).Value;
        object?[] values = [JsonRecordArray.Create(itemSchema, items), number, null, null];
        bool[] present = [true, true, true, false];
        var record = JsonScalarRecord.Create(schema, values, present);
        var payload = JsonValidatedRecordPayload.FromValue(record);
        Assert.Same(record, payload.Value);
        Assert.Same(schema, payload.Value.Schema);
        items[0] = JsonScalarRecord.Create(itemSchema, ["changed", 1L], [true, true]);
        values[0] = null;
        present[2] = false;
        const string expected = "{\"items\":[{\"sku\":\"A\",\"count\":9007199254740993}],\"amount\":12345678901234567890.00e+2,\"note\":null}";
        var exposed = payload.Bytes;
        Assert.Equal(expected, Text(exposed));
        exposed[0] = 0;
        Assert.Equal(expected, Text(payload.Bytes));

        // Fixture metadata and the handoff are test-owned; the store still
        // accepts opaque bytes and is not a durable admission implementation.
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        var request = new TargetedRequest("app", "submission", TargetedOperation.Invoke,
            "actor", payload.Bytes, Bytes("test-schema-v1"), [7]);
        Assert.Equal(TargetedAcceptKind.Created, store.Accept(request, Guid.NewGuid(), "selected").Kind);
        var saved = store.Lookup("app", "submission")!;
        Assert.Equal(expected, Text(saved.Request.Payload));
        var recovered = JsonValidatedRecordPayload.Decode(schema, saved.Request.Payload);
        Assert.True(recovered.IsSuccess);
        Assert.Same(schema, recovered.Value.Value.Schema);
        Assert.True(recovered.Value.Value.IsPresent(2));
        Assert.Null(recovered.Value.Value.ValueAt(2));
        Assert.False(recovered.Value.Value.IsPresent(3));
        var recoveredItems = Assert.IsType<JsonRecordArray>(recovered.Value.Value.ValueAt(0));
        Assert.Same(itemSchema, recoveredItems.Schema);
        Assert.Equal(9007199254740993L, recoveredItems.At(0).ValueAt(1));
        Assert.Equal(expected, Text(recovered.Value.Bytes));
        Assert.False(store.IsDurable);
    }

    [Fact]
    public void DecodePreservesRawBytesAndDoesNotRetainCallerMemory()
    {
        var (_, schema) = Schemas();
        const string json = " { \"amount\" : -0.00, \"items\" : [ { \"count\":1, \"sku\":\"A\" } ] } ";
        var input = Bytes(json);
        var result = JsonValidatedRecordPayload.Decode(schema, input);
        Assert.True(result.IsSuccess);
        Assert.Same(schema, result.Value.Value.Schema);
        Assert.False(result.Value.Value.IsPresent(2));
        Assert.False(result.Value.Value.IsPresent(3));
        Array.Fill(input, (byte)'x');
        result.Value.Bytes[0] = 0;
        Assert.Equal(json, Text(result.Value.Bytes));
        Assert.Equal("-0.00", Assert.IsType<JsonExactNumber>(result.Value.Value.ValueAt(1)).Token);
    }

    [Fact]
    public void InvalidNestedDataIsRedactedBeforeTestOwnedHandoff()
    {
        var (itemSchema, schema) = Schemas();
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        var input = Bytes("{\"items\":[{\"sku\":\"A\",\"count\":\"secret-value\"}],\"amount\":1}");
        var result = JsonValidatedRecordPayload.Decode(schema, input);
        if (result.IsSuccess)
        {
            var request = new TargetedRequest("app", "submission", TargetedOperation.Invoke,
                "actor", result.Value.Bytes, Bytes("test-schema-v1"), [7]);
            store.Accept(request, Guid.NewGuid(), "selected");
        }
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"items\"][0][\"count\"]"), result.Error);
        Assert.DoesNotContain("secret-value", result.Error!.Value.ToString(), StringComparison.Ordinal);
        Assert.Null(store.Lookup("app", "submission"));
        Assert.Throws<ArgumentNullException>(() => JsonValidatedRecordPayload.Decode(null!, Bytes("{}")));
        Assert.Throws<ArgumentNullException>(() => JsonValidatedRecordPayload.FromValue(null!));
        var foreignSchema = new JsonScalarRecordSchema([new("sku", JsonRecordFieldKind.String), new("count", JsonRecordFieldKind.Int64)]);
        var foreign = JsonRecordArray.Create(foreignSchema, [JsonScalarRecord.Create(foreignSchema, ["A", 1L], [true, true])]);
        var number = JsonExactNumberCodec.Decode(Bytes("1")).Value;
        Assert.NotSame(itemSchema, foreign.Schema);
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(schema, [foreign, number, null, null], [true, true, true, false]));
    }
}
