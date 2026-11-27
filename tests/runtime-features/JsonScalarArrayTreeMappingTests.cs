using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonScalarArrayTreeMappingTests
{
    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);
    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);
    private static JsonFieldValue Tree(string text) => JsonFieldValueCodec.Decode(Bytes(text)).Value;
    private static JsonExactNumber Number(string text) => JsonExactNumberCodec.Decode(Bytes(text)).Value;

    [Fact]
    public void AllScalarKindsRoundTripAsTreeRoots()
    {
        var cases = new (JsonRecordScalarArray Array, string Json)[] {
            (JsonRecordScalarArray.Create(new[] { true, false }), "[true,false]"),
            (JsonRecordScalarArray.Create(new[] { int.MinValue, int.MaxValue }), "[-2147483648,2147483647]"),
            (JsonRecordScalarArray.Create(new[] { "a", "λ" }), "[\"a\",\"\\u03BB\"]"),
            (JsonRecordScalarArray.Create(new[] { Number("1.2300"), Number("1e+999") }), "[1.2300,1e+999]"),
            (JsonRecordScalarArray.Create(new[] { decimal.MaxValue, 0.125m }), "[79228162514264337593543950335,0.125]"),
            (JsonRecordScalarArray.Create(new[] { long.MinValue, long.MaxValue }), "[-9223372036854775808,9223372036854775807]"),
            (JsonRecordScalarArray.Create(new[] { 0UL, ulong.MaxValue }), "[0,18446744073709551615]"),
            (JsonRecordScalarArray.Create(new bool?[] { null, true }), "[null,true]"),
            (JsonRecordScalarArray.Create(new int?[] { null, 42 }), "[null,42]"),
            (JsonRecordScalarArray.CreateNullableStrings([null, "a"]), "[null,\"a\"]"),
            (JsonRecordScalarArray.CreateNullableNumbers([null, Number("-0.00")]), "[null,-0.00]"),
            (JsonRecordScalarArray.Create(new decimal?[] { null, 1.25m }), "[null,1.25]"),
            (JsonRecordScalarArray.Create(new long?[] { null, long.MinValue }), "[null,-9223372036854775808]"),
            (JsonRecordScalarArray.Create(new ulong?[] { null, ulong.MaxValue }), "[null,18446744073709551615]") };
        foreach (var (array, json) in cases)
        {
            var tree = JsonTypedFieldMapping.FromScalarArray(array);
            Assert.True(tree.IsSuccess, array.Kind.ToString());
            Assert.Equal(json, Text(JsonFieldValueCodec.Encode(tree.Value)));
            var decoded = JsonTypedFieldMapping.ToScalarArray(array.Kind, tree.Value);
            Assert.True(decoded.IsSuccess, array.Kind.ToString());
            Assert.Equal(array.Kind, decoded.Value.Kind);
            var encodedAgain = JsonTypedFieldMapping.FromScalarArray(decoded.Value);
            Assert.True(encodedAgain.IsSuccess);
            Assert.Equal(json, Text(JsonFieldValueCodec.Encode(encodedAgain.Value)));
        }
    }

    [Fact]
    public void TreeComposesWithValidatedUnionAndReferenceSnapshot()
    {
        long?[] source = [long.MinValue, null, long.MaxValue];
        var array = JsonRecordScalarArray.Create(source);
        source[0] = 0;
        var tree = JsonTypedFieldMapping.FromScalarArray(array);
        Assert.True(tree.IsSuccess);
        var fields = new JsonScalarRecordSchema([new("numbers", JsonRecordFieldKind.FieldValue)]);
        var record = JsonScalarRecord.Create(fields, [tree.Value], [true]);
        var schema = new JsonTaggedUnionSchema([new("Values", fields)]);
        var union = JsonTaggedUnion.Create(schema, 0, record);
        var payload = JsonValidatedUnionPayload.FromValue(union);
        var store = new VolatileTargetedStore(TimeSpan.FromMinutes(1));
        var bytes = payload.Bytes;
        var request = new TargetedRequest("app", "submission", TargetedOperation.Invoke, "actor", bytes, Bytes("test-schema-v1"), [1]);
        Assert.Equal(TargetedAcceptKind.Created, store.Accept(request, Guid.NewGuid(), "selected").Kind);
        Array.Fill(bytes, (byte)'x');
        var recovered = JsonValidatedUnionPayload.Decode(schema, store.Lookup("app", "submission")!.Request.Payload);
        Assert.True(recovered.IsSuccess);
        var recoveredTree = (JsonFieldValue)recovered.Value.Value.Payload!.ValueAt(0)!;
        var result = JsonTypedFieldMapping.ToScalarArray(JsonRecordScalarArrayKind.NullableInt64, recoveredTree);
        Assert.True(result.IsSuccess);
        Assert.Equal(long.MinValue, result.Value.At(0));
        Assert.Null(result.Value.At(1));
        Assert.Equal(long.MaxValue, result.Value.At(2));
        Assert.False(store.IsDurable);
    }

    [Theory]
    [InlineData("Integer", "[1,\"secret\"]", "TypeMismatch", "$[1]")]
    [InlineData("UInt64", "[-1]", "InvalidInteger", "$[0]")]
    [InlineData("Int64", "[9223372036854775808]", "InvalidInteger", "$[0]")]
    [InlineData("Decimal", "[1e-29]", "InvalidDecimal", "$[0]")]
    [InlineData("Boolean", "[null]", "TypeMismatch", "$[0]")]
    [InlineData("Integer", "{\"private\":1}", "TypeMismatch", "$")]
    public void InvalidValuesReturnRedactedIndexedErrors(string kind, string json, string code, string path)
    {
        var result = JsonTypedFieldMapping.ToScalarArray(Enum.Parse<JsonRecordScalarArrayKind>(kind), Tree(json));
        Assert.Equal(new JsonScalarError(Enum.Parse<JsonScalarErrorCode>(code), path), result.Error);
        Assert.DoesNotContain("secret", result.Error!.Value.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("private", result.Error.Value.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void DepthBoundaryReportsFirstInvalidItemWithoutWrappingOverflow()
    {
        var deep = JsonFieldValue.Null();
        for (var i = 0; i < 63; i++) deep = JsonFieldValue.List([deep]);
        var invalidFirst = JsonFieldValue.List([JsonFieldValue.String("secret"), deep]);
        var nestedSecond = JsonFieldValue.List([JsonFieldValue.Number(Number("42")), deep]);
        Assert.Equal(64, invalidFirst.Depth);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[0]"),
            JsonTypedFieldMapping.ToScalarArray(JsonRecordScalarArrayKind.Integer, invalidFirst).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[1]"),
            JsonTypedFieldMapping.ToScalarArray(JsonRecordScalarArrayKind.Integer, nestedSecond).Error);
    }

    [Fact]
    public void NullsUnknownKindsAndSnapshotsAreHandledExplicitly()
    {
        Assert.Throws<ArgumentNullException>(() => JsonTypedFieldMapping.FromScalarArray(null!));
        Assert.Throws<ArgumentNullException>(() => JsonTypedFieldMapping.ToScalarArray(JsonRecordScalarArrayKind.Integer, null!));
        Assert.Throws<ArgumentException>(() => JsonTypedFieldMapping.ToScalarArray((JsonRecordScalarArrayKind)999, Tree("[]")));
        var input = new[] { "a", "b" };
        var array = JsonRecordScalarArray.Create(input);
        input[0] = "changed";
        var tree = JsonTypedFieldMapping.FromScalarArray(array).Value;
        var items = tree.ListItems;
        items[0] = JsonFieldValue.String("changed");
        Assert.Equal("[\"a\",\"b\"]", Text(JsonFieldValueCodec.Encode(tree)));
        Assert.True(JsonTypedFieldMapping.ToScalarArray(JsonRecordScalarArrayKind.Integer, Tree("[]")).IsSuccess);
    }
}
