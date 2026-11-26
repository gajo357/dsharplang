using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonDecimalCollectionTests
{
    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);
    private static string Text(byte[] value) => Encoding.UTF8.GetString(value);
    private static JsonFieldValue Tree(string value) => JsonFieldValueCodec.Decode(Bytes(value)).Value;

    [Fact]
    public void DecimalCollectionsRoundTripThroughUnionTreeAndReference()
    {
        var fields = new JsonScalarRecordSchema([
            new("prices", JsonRecordFieldKind.DecimalArray),
            new("adjustments", JsonRecordFieldKind.DecimalNullableElementsArray),
            new("bySku", JsonRecordFieldKind.DecimalMap),
            new("optional", JsonRecordFieldKind.NullableDecimalNullableValuesMap, Optional: true)]);
        var schema = new JsonTaggedUnionSchema([new("Prices", fields)]);
        const string input = "{\"case\":\"Prices\",\"value\":{\"prices\":[9007199254740993.125,10e-29],\"adjustments\":[null,-2.5],\"bySku\":{\"A\":79228162514264337593543950335},\"optional\":{\"B\":null}}}";
        const string normalized = "{\"case\":\"Prices\",\"value\":{\"prices\":[9007199254740993.125,0.0000000000000000000000000001],\"adjustments\":[null,-2.5],\"bySku\":{\"A\":79228162514264337593543950335},\"optional\":{\"B\":null}}}";
        var decoded = JsonValidatedUnionPayload.Decode(schema, Bytes(input));
        Assert.True(decoded.IsSuccess);
        Assert.Equal(input, Text(decoded.Value.Bytes));
        var encoded = JsonValidatedUnionPayload.FromValue(decoded.Value.Value);
        Assert.Equal(normalized, Text(encoded.Bytes));
        var tree = JsonTypedFieldMapping.FromUnion(encoded.Value);
        Assert.True(tree.IsSuccess);
        var fromTree = JsonTypedFieldMapping.ToUnion(schema, tree.Value);
        Assert.True(fromTree.IsSuccess);
        Assert.Equal(normalized, Text(JsonTaggedUnionCodec.Encode(fromTree.Value)));

        // Metadata and orchestration are test-owned; the store remains opaque
        // byte storage and is not a durable admission implementation.
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        var request = new TargetedRequest("app", "submission", TargetedOperation.Invoke,
            "actor", encoded.Bytes, Bytes("test-schema-v1"), [7]);
        Assert.Equal(TargetedAcceptKind.Created, store.Accept(request, Guid.NewGuid(), "selected").Kind);
        var recovered = JsonValidatedUnionPayload.Decode(schema, store.Lookup("app", "submission")!.Request.Payload);
        Assert.True(recovered.IsSuccess);
        Assert.Equal(normalized, Text(recovered.Value.Bytes));
        Assert.False(store.IsDurable);
    }

    [Fact]
    public void CollectionPresenceAndNullEntriesAreIndependent()
    {
        var schema = new JsonScalarRecordSchema([
            new("array", JsonRecordFieldKind.NullableDecimalNullableElementsArray, Optional: true),
            new("map", JsonRecordFieldKind.NullableDecimalNullableValuesMap, Optional: true)]);
        foreach (var (json, present, isNull) in new[] {
            ("{}", false, true), ("{\"array\":null,\"map\":null}", true, true),
            ("{\"array\":[],\"map\":{}}", true, false),
            ("{\"array\":[null],\"map\":{\"a\":null}}", true, false) })
        {
            var result = JsonValidatedRecordPayload.Decode(schema, Bytes(json));
            Assert.True(result.IsSuccess);
            Assert.Equal(present, result.Value.Value.IsPresent(0));
            Assert.Equal(present, result.Value.Value.IsPresent(1));
            Assert.Equal(isNull, result.Value.Value.ValueAt(0) is null);
            Assert.Equal(isNull, result.Value.Value.ValueAt(1) is null);
            Assert.Equal(json, Text(JsonValidatedRecordPayload.FromValue(result.Value.Value).Bytes));
        }
    }

    [Fact]
    public void InvalidValuesReportIndexedRedactedErrors()
    {
        var schema = new JsonScalarRecordSchema([new("array", JsonRecordFieldKind.DecimalArray), new("map", JsonRecordFieldKind.DecimalMap)]);
        var cases = new[] {
            ("{\"array\":[1,1e-29],\"map\":{}}", new JsonScalarError(JsonScalarErrorCode.InvalidDecimal, "$[\"array\"][1]")),
            ("{\"array\":[null],\"map\":{}}", new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"array\"][0]")),
            ("{\"array\":[],\"map\":{\"private\":\"secret\"}}", new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"map\"][0]")),
            ("{\"array\":[],\"map\":{\"private\":79228162514264337593543950336}}", new JsonScalarError(JsonScalarErrorCode.InvalidDecimal, "$[\"map\"][0]"))
        };
        foreach (var (json, expected) in cases)
        {
            var result = JsonScalarRecordCodec.Decode(schema, Bytes(json));
            Assert.Equal(expected, result.Error);
            Assert.DoesNotContain("private", result.Error!.Value.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("secret", result.Error.Value.ToString(), StringComparison.Ordinal);
        }
        Assert.Throws<ArgumentException>(() => JsonRecordScalarMap.Create(JsonRecordFieldKind.Decimal, [new("x", null!)]));
        Assert.Throws<ArgumentException>(() => JsonRecordScalarMap.Create(JsonRecordFieldKind.NullableDecimal, [new("x", 1)]));
        Assert.Throws<ArgumentException>(() => JsonRecordScalarMap.Create(JsonRecordFieldKind.Decimal, [new("x", 1m), new("x", 2m)]));
    }

    [Fact]
    public void SnapshotsAndStandaloneMapTreeMappingsRetainDescriptorAndValues()
    {
        decimal[] items = [1.25m, decimal.MaxValue];
        var array = JsonRecordScalarArray.Create(items);
        var entries = new[] { new KeyValuePair<string, object>("a", 1.25m), new("b", null!) };
        var map = JsonRecordScalarMap.Create(JsonRecordFieldKind.NullableDecimal, entries);
        items[0] = 9m;
        entries[0] = new("changed", 9m);
        Assert.Equal(1.25m, array.At(0));
        var mapped = JsonTypedFieldMapping.FromScalarMap(map);
        Assert.True(mapped.IsSuccess);
        Assert.Equal("{\"a\":1.25,\"b\":null}", Text(JsonFieldValueCodec.Encode(mapped.Value)));
        var roundTrip = JsonTypedFieldMapping.ToScalarMap(JsonRecordFieldKind.NullableDecimal, mapped.Value);
        Assert.True(roundTrip.IsSuccess);
        Assert.Equal(JsonRecordFieldKind.NullableDecimal, roundTrip.Value.Kind);
        Assert.Equal(1.25m, roundTrip.Value.At(0).Value);
        Assert.Null(roundTrip.Value.At(1).Value);
        var mismatch = JsonTypedFieldMapping.ToScalarMap(JsonRecordFieldKind.Decimal, Tree("{\"private\":null}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[0]"), mismatch.Error);
        var required = JsonRecordScalarMap.Create(JsonRecordFieldKind.Decimal, [new("a", decimal.MaxValue)]);
        Assert.True(JsonTypedFieldMapping.ToScalarMap(JsonRecordFieldKind.Decimal, JsonTypedFieldMapping.FromScalarMap(required).Value).IsSuccess);
        var schema = new JsonScalarRecordSchema([new("prices", JsonRecordFieldKind.DecimalArray)]);
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(schema, [JsonRecordScalarArray.Create(new decimal?[] { 1m })], [true]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DecimalCollectionsContributeToTheCombinedDepthLimit(bool useMap)
    {
        var schema = new JsonScalarRecordSchema([new("items", useMap ? JsonRecordFieldKind.DecimalMap : JsonRecordFieldKind.DecimalArray)]);
        object collection = useMap
            ? JsonRecordScalarMap.Create(JsonRecordFieldKind.Decimal, [new("a", 1.25m)])
            : JsonRecordScalarArray.Create(new[] { 1.25m });
        var record = JsonScalarRecord.Create(schema, [collection], [true]);
        for (var index = 0; index < 62; index++)
        {
            var parent = new JsonScalarRecordSchema([new("child", JsonRecordFieldKind.Record, NestedSchema: schema)]);
            record = JsonScalarRecord.Create(parent, [record], [true]);
            schema = parent;
        }
        Assert.Equal(64, record.Depth);
        Assert.True(JsonValidatedRecordPayload.Decode(schema, JsonValidatedRecordPayload.FromValue(record).Bytes).IsSuccess);
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("child", JsonRecordFieldKind.Record, NestedSchema: schema)]));
    }
}
