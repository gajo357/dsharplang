using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonStreamingUnionTests
{
    [Fact]
    public void UnionFieldsArraysAndMapsKeepExactBytesAndRoundTrip()
    {
        var payloadSchema = new JsonScalarRecordSchema([new("text", JsonRecordFieldKind.String)]);
        var cases = new JsonTaggedUnionSchema([new("Empty"), new("Message", payloadSchema)]);
        var empty = JsonTaggedUnion.Create(cases, 0);
        var message = JsonTaggedUnion.Create(cases, 1, JsonScalarRecord.Create(payloadSchema, ["hello"], [true]));
        var schema = new JsonScalarRecordSchema([
            new("single", JsonRecordFieldKind.Union, UnionSchema: cases),
            new("array", JsonRecordFieldKind.UnionArray, UnionSchema: cases),
            new("nullableArray", JsonRecordFieldKind.UnionNullableElementsArray, UnionSchema: cases),
            new("map", JsonRecordFieldKind.UnionMap, UnionSchema: cases),
            new("nullableMap", JsonRecordFieldKind.UnionNullableValuesMap, UnionSchema: cases)]);
        var value = JsonScalarRecord.Create(schema, [message, JsonUnionArray.Create(cases, [empty, message]),
            JsonNullableUnionArray.Create(cases, [null, message]),
            JsonUnionMap.Create(cases, [new("a", message)]), JsonNullableUnionMap.Create(cases, [new("a", null), new("b", empty)])],
            [true, true, true, true, true]);
        var bytes = JsonScalarRecordCodec.Encode(value);
        Assert.Equal("{\"single\":{\"case\":\"Message\",\"value\":{\"text\":\"hello\"}},\"array\":[{\"case\":\"Empty\"},{\"case\":\"Message\",\"value\":{\"text\":\"hello\"}}],\"nullableArray\":[null,{\"case\":\"Message\",\"value\":{\"text\":\"hello\"}}],\"map\":{\"a\":{\"case\":\"Message\",\"value\":{\"text\":\"hello\"}}},\"nullableMap\":{\"a\":null,\"b\":{\"case\":\"Empty\"}}}", Encoding.UTF8.GetString(bytes));
        var decoded = JsonScalarRecordCodec.Decode(schema, bytes);
        Assert.True(decoded.IsSuccess);
        Assert.Equal(bytes, JsonScalarRecordCodec.Encode(decoded.Value));
    }

    [Fact]
    public void StandaloneUnionPreservesEscapedNamesAndText()
    {
        var payload = new JsonScalarRecordSchema([new("text", JsonRecordFieldKind.String)]);
        var schema = new JsonTaggedUnionSchema([new("<", payload)]);
        var value = JsonTaggedUnion.Create(schema, 0, JsonScalarRecord.Create(payload, ["é>"], [true]));
        var bytes = JsonTaggedUnionCodec.Encode(value);
        Assert.Equal("{\"case\":\"\\u003C\",\"value\":{\"text\":\"\\u00E9\\u003E\"}}", Encoding.UTF8.GetString(bytes));
        var decoded = JsonTaggedUnionCodec.Decode(schema, bytes);
        Assert.True(decoded.IsSuccess); Assert.Equal("é>", decoded.Value.Payload!.ValueAt(0));
    }

    [Fact]
    public void DeepAlternatingValuesDoNotCopyLargePayloadAtEveryUnion()
    {
        const int depth = 25;
        var text = new string('x', 160_000);
        var schema = new JsonScalarRecordSchema([new("text", JsonRecordFieldKind.String)]);
        var record = JsonScalarRecord.Create(schema, [text], [true]);
        var expected = "{\"text\":\"" + text + "\"}";
        for (var i = 0; i < depth; i++)
        {
            var cases = new JsonTaggedUnionSchema([new("Nest", schema)]);
            var union = JsonTaggedUnion.Create(cases, 0, record);
            schema = new JsonScalarRecordSchema([new("next", JsonRecordFieldKind.Union, UnionSchema: cases)]);
            record = JsonScalarRecord.Create(schema, [union], [true]);
            expected = "{\"next\":{\"case\":\"Nest\",\"value\":" + expected + "}}";
        }
        JsonScalarRecordCodec.Encode(record); // Warm up before measuring this thread.
        var before = GC.GetAllocatedBytesForCurrentThread();
        var bytes = JsonScalarRecordCodec.Encode(record);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 8_000_000, $"Nested encoding allocated {allocated} bytes.");
        Assert.Equal(expected, Encoding.UTF8.GetString(bytes));
        Assert.True(JsonScalarRecordCodec.Decode(schema, bytes).IsSuccess);
    }
}
