using System.Collections;
using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonCapabilityRejectionTests
{
    private sealed class Foreign : IEnumerable<object>, IDisposable
    {
        internal int Calls { get; private set; }
        public IEnumerator<object> GetEnumerator() { Calls++; throw new InvalidOperationException("secret-enumerator"); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void Dispose() { Calls++; throw new InvalidOperationException("secret-resource"); }
        public override string ToString() { Calls++; return "secret-foreign"; }
    }

    [Fact]
    public void ObjectValuedBoundariesRejectCapabilitiesWithoutExecutingThem()
    {
        var foreign = new Foreign();
        var calls = 0;
        Func<int> callback = () => { calls++; throw new InvalidOperationException("secret-callback"); };
        var task = new TaskCompletionSource<int>().Task;
        object[] unsafeValues = [callback, task, foreign, new List<int> { 1, 2 }];
        var field = new JsonScalarRecordSchema([new("data", JsonRecordFieldKind.FieldValue)]);
        foreach (var unsafeValue in unsafeValues)
        {
            var recordError = Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(field, [unsafeValue], [true]));
            var mapError = Assert.Throws<ArgumentException>(() => JsonRecordScalarMap.Create(JsonRecordFieldKind.String, [new("key", unsafeValue)]));
            Assert.DoesNotContain("secret", recordError.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("secret", mapError.ToString(), StringComparison.Ordinal);
        }
        Assert.Equal(0, calls);
        Assert.Equal(0, foreign.Calls);
        Assert.False(task.IsCompleted);
    }

    [Fact]
    public void NestedRecordAndUnionBoundariesRejectForeignObjectsWithoutCallbacks()
    {
        var foreign = new Foreign();
        var item = new JsonScalarRecordSchema([new("count", JsonRecordFieldKind.Integer)]);
        var union = new JsonTaggedUnionSchema([new("Data", item)]);
        var outer = new JsonScalarRecordSchema([
            new("record", JsonRecordFieldKind.Record, NestedSchema: item),
            new("event", JsonRecordFieldKind.Union, UnionSchema: union),
            new("array", JsonRecordFieldKind.RecordNullableElementsArray, NestedSchema: item)]);
        var validRecord = JsonScalarRecord.Create(item, [1], [true]);
        var validUnion = JsonTaggedUnion.Create(union, 0, validRecord);
        var validArray = JsonNullableRecordArray.Create(item, [null, validRecord]);
        var errors = new[] {
            Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(outer, [foreign, validUnion, validArray], [true, true, true])),
            Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(outer, [validRecord, foreign, validArray], [true, true, true])),
            Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(outer, [validRecord, validUnion, foreign], [true, true, true]))
        };
        foreach (var error in errors) Assert.DoesNotContain("secret", error.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, foreign.Calls);
    }

    [Fact]
    public void EligibleNestedDataRoundTripsAndMalformedValuesStayRedacted()
    {
        var item = new JsonScalarRecordSchema([new("n", JsonRecordFieldKind.Integer)]);
        var cases = new JsonTaggedUnionSchema([new("Data", item)]);
        var payload = new JsonScalarRecordSchema([
            new("records", JsonRecordFieldKind.RecordNullableElementsArray, NestedSchema: item),
            new("events", JsonRecordFieldKind.UnionNullableValuesMap, UnionSchema: cases)]);
        var root = new JsonTaggedUnionSchema([new("Batch", payload)]);
        const string json = "{\"case\":\"Batch\",\"value\":{\"records\":[null,{\"n\":1}],\"events\":{\"a\":null,\"b\":{\"case\":\"Data\",\"value\":{\"n\":2}}}}}";
        var decoded = JsonTaggedUnionCodec.Decode(root, Encoding.UTF8.GetBytes(json));
        Assert.True(decoded.IsSuccess);
        Assert.Equal(json, Encoding.UTF8.GetString(JsonTaggedUnionCodec.Encode(decoded.Value)));
        var tree = JsonTypedFieldMapping.FromUnion(decoded.Value);
        Assert.True(tree.IsSuccess);
        var restored = JsonTypedFieldMapping.ToUnion(root, tree.Value);
        Assert.True(restored.IsSuccess);
        Assert.Equal(json, Encoding.UTF8.GetString(JsonTaggedUnionCodec.Encode(restored.Value)));
        var bad = JsonTaggedUnionCodec.Decode(root, Encoding.UTF8.GetBytes("{\"case\":\"Batch\",\"value\":{\"records\":[{\"n\":\"secret-value\"}],\"events\":{}}}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"value\"][\"records\"][0][\"n\"]"), bad.Error);
        Assert.DoesNotContain("secret-value", bad.Error!.Value.ToString(), StringComparison.Ordinal);
    }
}
