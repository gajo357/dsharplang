using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonWideIntegerCollectionTests
{
    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);
    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    [Fact]
    public void AllCollectionDescriptorsRoundTripExactBoundaryValuesAndPresence()
    {
        var kinds = new[] {
            ("Int64Array", "[-9223372036854775808,9223372036854775807]"),
            ("NullableInt64Array", "[-9223372036854775808,9223372036854775807]"),
            ("Int64NullableElementsArray", "[null,-9223372036854775808]"),
            ("NullableInt64NullableElementsArray", "[null,9223372036854775807]"),
            ("UInt64Array", "[0,18446744073709551615]"),
            ("NullableUInt64Array", "[0,18446744073709551615]"),
            ("UInt64NullableElementsArray", "[null,18446744073709551615]"),
            ("NullableUInt64NullableElementsArray", "[null,18446744073709551615]"),
            ("Int64Map", "{\"a\":-9223372036854775808}"),
            ("NullableInt64Map", "{\"a\":9223372036854775807}"),
            ("Int64NullableValuesMap", "{\"a\":null,\"b\":-9223372036854775808}"),
            ("NullableInt64NullableValuesMap", "{\"a\":null,\"b\":9223372036854775807}"),
            ("UInt64Map", "{\"a\":18446744073709551615}"),
            ("NullableUInt64Map", "{\"a\":18446744073709551615}"),
            ("UInt64NullableValuesMap", "{\"a\":null,\"b\":18446744073709551615}"),
            ("NullableUInt64NullableValuesMap", "{\"a\":null,\"b\":18446744073709551615}") };
        foreach (var (name, value) in kinds)
        {
            var optional = name.StartsWith("Nullable", StringComparison.Ordinal);
            var schema = new JsonScalarRecordSchema([new("values", Enum.Parse<JsonRecordFieldKind>(name), Optional: optional)]);
            var json = "{\"values\":" + value + "}";
            var decoded = JsonValidatedRecordPayload.Decode(schema, Bytes(json));
            Assert.True(decoded.IsSuccess, name);
            Assert.Equal(json, Text(JsonValidatedRecordPayload.FromValue(decoded.Value.Value).Bytes));
            if (!optional) continue;
            var missing = JsonValidatedRecordPayload.Decode(schema, Bytes("{}"));
            var explicitNull = JsonValidatedRecordPayload.Decode(schema, Bytes("{\"values\":null}"));
            Assert.True(missing.IsSuccess);
            Assert.True(explicitNull.IsSuccess);
            Assert.False(missing.Value.Value.IsPresent(0));
            Assert.True(explicitNull.Value.Value.IsPresent(0));
            Assert.Null(explicitNull.Value.Value.ValueAt(0));
            var empty = name.Contains("Array", StringComparison.Ordinal) ? "[]" : "{}";
            Assert.True(JsonValidatedRecordPayload.Decode(schema, Bytes("{\"values\":" + empty + "}")).IsSuccess);
        }
    }

    [Fact]
    public void FullWidthValuesCrossNestedUnionTreeAndReferenceHandoff()
    {
        var fields = new JsonScalarRecordSchema([
            new("signed", JsonRecordFieldKind.Int64NullableElementsArray),
            new("unsigned", JsonRecordFieldKind.UInt64NullableValuesMap)]);
        var schema = new JsonTaggedUnionSchema([new("Batch", fields)]);
        const string json = "{\"case\":\"Batch\",\"value\":{\"signed\":[-9223372036854775808,null,9223372036854775807],\"unsigned\":{\"x\":18446744073709551615,\"y\":null}}}";
        var source = Bytes(json);
        var decoded = JsonValidatedUnionPayload.Decode(schema, source);
        Assert.True(decoded.IsSuccess);
        Array.Fill(source, (byte)'x');
        var payload = JsonValidatedUnionPayload.FromValue(decoded.Value.Value);
        Assert.Equal(json, Text(payload.Bytes));
        var tree = JsonTypedFieldMapping.FromUnion(payload.Value);
        Assert.True(tree.IsSuccess);
        var restored = JsonTypedFieldMapping.ToUnion(schema, tree.Value);
        Assert.True(restored.IsSuccess);
        Assert.Equal(json, Text(JsonTaggedUnionCodec.Encode(restored.Value)));
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        var bytes = payload.Bytes;
        var request = new TargetedRequest("app", "submission", TargetedOperation.Invoke, "actor", bytes, Bytes("test-schema-v1"), [1]);
        Assert.Equal(TargetedAcceptKind.Created, store.Accept(request, Guid.NewGuid(), "selected").Kind);
        Array.Fill(bytes, (byte)'y');
        var recovered = JsonValidatedUnionPayload.Decode(schema, store.Lookup("app", "submission")!.Request.Payload);
        Assert.True(recovered.IsSuccess);
        Assert.Equal(json, Text(recovered.Value.Bytes));
        Assert.False(store.IsDurable);
    }

    [Theory]
    [InlineData("Int64Array", "[9223372036854775808]", "InvalidInteger")]
    [InlineData("Int64NullableElementsArray", "[null,-9223372036854775809]", "InvalidInteger")]
    [InlineData("UInt64Array", "[-1]", "InvalidInteger")]
    [InlineData("UInt64Array", "[18446744073709551616]", "InvalidInteger")]
    [InlineData("UInt64Array", "[1.5]", "InvalidInteger")]
    [InlineData("UInt64Array", "[\"secret\"]", "TypeMismatch")]
    [InlineData("UInt64Map", "{\"private\":-1}", "InvalidInteger")]
    [InlineData("Int64NullableValuesMap", "{\"private\":\"secret\"}", "TypeMismatch")]
    public void InvalidEntriesHaveRedactedIndexedErrors(string kind, string value, string code)
    {
        var schema = new JsonScalarRecordSchema([new("values", Enum.Parse<JsonRecordFieldKind>(kind))]);
        var result = JsonScalarRecordCodec.Decode(schema, Bytes("{\"values\":" + value + "}"));
        var index = value.StartsWith("[null,", StringComparison.Ordinal) ? 1 : 0;
        Assert.Equal(new JsonScalarError(Enum.Parse<JsonScalarErrorCode>(code), "$[\"values\"][" + index + "]"), result.Error);
        Assert.DoesNotContain("private", result.Error!.Value.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("secret", result.Error.Value.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void SnapshotsAndScalarMapBridgeKeepTypesAndRejectMismatches()
    {
        long[] signed = [long.MinValue, long.MaxValue];
        ulong?[] unsigned = [null, ulong.MaxValue];
        var a = JsonRecordScalarArray.Create(signed);
        var b = JsonRecordScalarArray.Create(unsigned);
        signed[0] = 0;
        unsigned[1] = 0;
        Assert.Equal(long.MinValue, a.At(0));
        Assert.Equal(ulong.MaxValue, b.At(1));
        foreach (var (kind, value) in new[] {
            (JsonRecordFieldKind.Int64, (object)long.MinValue), (JsonRecordFieldKind.NullableInt64, (object)long.MaxValue),
            (JsonRecordFieldKind.UInt64, (object)ulong.MaxValue), (JsonRecordFieldKind.NullableUInt64, (object)ulong.MaxValue) })
        {
            var entries = new[] { new KeyValuePair<string, object>("key", value) };
            var map = JsonRecordScalarMap.Create(kind, entries);
            entries[0] = new("changed", 0);
            var tree = JsonTypedFieldMapping.FromScalarMap(map);
            Assert.True(tree.IsSuccess);
            var roundtrip = JsonTypedFieldMapping.ToScalarMap(kind, tree.Value);
            Assert.True(roundtrip.IsSuccess);
            Assert.Equal(kind, roundtrip.Value.Kind);
            Assert.Equal("key", roundtrip.Value.At(0).Key);
            Assert.Equal(value, roundtrip.Value.At(0).Value);
        }
        Assert.Throws<ArgumentException>(() => JsonRecordScalarMap.Create(JsonRecordFieldKind.UInt64, [new("x", -1L)]));
        Assert.Throws<ArgumentException>(() => JsonRecordScalarMap.Create(JsonRecordFieldKind.Int64, [new("x", ulong.MaxValue)]));
        var schema = new JsonScalarRecordSchema([new("values", JsonRecordFieldKind.UInt64Array)]);
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(schema, [a], [true]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WideIntegerContainersContributeToDepthLimit(bool map)
    {
        var schema = new JsonScalarRecordSchema([new("values", map ? JsonRecordFieldKind.UInt64Map : JsonRecordFieldKind.Int64Array)]);
        object value = map ? JsonRecordScalarMap.Create(JsonRecordFieldKind.UInt64, [new("x", ulong.MaxValue)]) : JsonRecordScalarArray.Create(new[] { long.MinValue });
        var record = JsonScalarRecord.Create(schema, [value], [true]);
        for (var i = 0; i < 62; i++)
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
