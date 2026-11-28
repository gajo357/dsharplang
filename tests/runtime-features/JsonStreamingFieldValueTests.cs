using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonStreamingFieldValueTests
{
    [Fact]
    public void NestedTreePreservesBytesAndSnapshotOwnership()
    {
        var number = JsonExactNumberCodec.Decode("1.2300e+4"u8.ToArray()).Value;
        JsonFieldValue[] elements = [JsonFieldValue.Number(number), JsonFieldValue.Null(), JsonFieldValue.String("é")];
        var list = JsonFieldValue.List(elements);
        KeyValuePair<string, JsonFieldValue>[] entries = [new("k", list)];
        var tree = JsonFieldValue.Map(entries);
        var payload = new JsonScalarRecordSchema([new("tree", JsonRecordFieldKind.FieldValue), new("optional", JsonRecordFieldKind.NullableFieldValue, Optional: true)]);
        var cases = new JsonTaggedUnionSchema([new("X", payload)]);
        var value = JsonTaggedUnion.Create(cases, 0, JsonScalarRecord.Create(payload, [tree, null], [true, false]));
        elements[0] = JsonFieldValue.Boolean(false); entries[0] = new("changed", JsonFieldValue.Null());
        var exposedList = list.ListItems; exposedList[0] = JsonFieldValue.Boolean(false);
        var exposedMap = tree.MapItems; exposedMap[0] = new("changed", JsonFieldValue.Null());
        var bytes = JsonTaggedUnionCodec.Encode(value);
        Assert.Equal("{\"case\":\"X\",\"value\":{\"tree\":{\"k\":[1.2300e+4,null,\"\\u00E9\"]}}}", Encoding.UTF8.GetString(bytes));
        var decoded = JsonTaggedUnionCodec.Decode(cases, bytes);
        Assert.True(decoded.IsSuccess); Assert.Equal(bytes, JsonTaggedUnionCodec.Encode(decoded.Value));
    }

    [Fact]
    public void WideFieldTreeAvoidsIntermediateBuffersAndContainerClones()
    {
        var element = JsonFieldValue.String(new string('x', 80));
        var tree = JsonFieldValue.List(Enumerable.Repeat(element, 30_000).ToArray());
        var schema = new JsonScalarRecordSchema([new("tree", JsonRecordFieldKind.FieldValue)]);
        var value = JsonScalarRecord.Create(schema, [tree], [true]);
        JsonScalarRecordCodec.Encode(value);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var bytes = JsonScalarRecordCodec.Encode(value);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 14_000_000, $"Field-tree encoding allocated {allocated} bytes.");
        var decoded = JsonScalarRecordCodec.Decode(schema, bytes);
        Assert.True(decoded.IsSuccess);
        Assert.Equal(30_000, Assert.IsType<JsonFieldValue>(decoded.Value.ValueAt(0)).ListItems.Length);
    }
}
