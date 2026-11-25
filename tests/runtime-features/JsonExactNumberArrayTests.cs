using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonExactNumberArrayTests
{
    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
    private static JsonExactNumber Number(string text) => JsonExactNumberCodec.Decode(Utf8(text)).Value;

    [Fact]
    public void ArraysPreserveSpellingAndOwnTheirInput()
    {
        var schema = new JsonScalarRecordSchema([
            new("values", JsonRecordFieldKind.ExactNumberArray),
            new("optional", JsonRecordFieldKind.NullableExactNumberNullableElementsArray, Optional: true)
        ]);
        var input = new[] { Number("-0.00"), Number("123456789012345678901234567890e+999") };
        var array = JsonRecordScalarArray.Create(input);
        input[0] = Number("7");
        var nullable = new JsonExactNumber?[] { null, Number("1.2300") };
        var record = JsonScalarRecord.Create(schema, [array, JsonRecordScalarArray.CreateNullableNumbers(nullable)], [true, true]);
        nullable[1] = null;
        var encoded = JsonScalarRecordCodec.Encode(record);
        Assert.Equal("{\"values\":[-0.00,123456789012345678901234567890e+999],\"optional\":[null,1.2300]}", Encoding.UTF8.GetString(encoded));
        var decoded = JsonScalarRecordCodec.Decode(schema, encoded);
        Assert.True(decoded.IsSuccess);
        Assert.Equal(encoded, JsonScalarRecordCodec.Encode(decoded.Value));
    }

    [Fact]
    public void MissingNullAndEmptyArraysRemainDistinct()
    {
        var schema = new JsonScalarRecordSchema([new("values", JsonRecordFieldKind.NullableExactNumberArray, Optional: true)]);
        foreach (var json in new[] { "{}", "{\"values\":null}", "{\"values\":[]}" })
        {
            var decoded = JsonScalarRecordCodec.Decode(schema, Utf8(json));
            Assert.True(decoded.IsSuccess);
            Assert.Equal(json, Encoding.UTF8.GetString(JsonScalarRecordCodec.Encode(decoded.Value)));
        }
    }

    [Fact]
    public void IndexedErrorsAreRedactedThroughNestedRecords()
    {
        var inner = new JsonScalarRecordSchema([new("numbers", JsonRecordFieldKind.ExactNumberNullableElementsArray)]);
        var outer = new JsonScalarRecordSchema([new("entry", JsonRecordFieldKind.Record, NestedSchema: inner)]);
        var bad = JsonScalarRecordCodec.Decode(outer, Utf8("{\"entry\":{\"numbers\":[null,1,\"secret\"]}}"));
        Assert.False(bad.IsSuccess);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"entry\"][\"numbers\"][2]"), bad.Error);
        Assert.DoesNotContain("secret", bad.Error!.Value.ToString(), StringComparison.Ordinal);
        var good = JsonScalarRecordCodec.Decode(outer, Utf8("{\"entry\":{\"numbers\":[null,1.00]}}"));
        Assert.True(good.IsSuccess);
        Assert.Equal("{\"entry\":{\"numbers\":[null,1.00]}}", Encoding.UTF8.GetString(JsonScalarRecordCodec.Encode(good.Value)));
    }

    [Fact]
    public void RequiredElementsAndDescriptorKindsAreEnforced()
    {
        var required = new JsonScalarRecordSchema([new("values", JsonRecordFieldKind.ExactNumberArray)]);
        Assert.Throws<ArgumentException>(() => JsonRecordScalarArray.Create(new JsonExactNumber[] { null! }));
        Assert.Throws<ArgumentException>(() => JsonScalarRecord.Create(required, [JsonRecordScalarArray.CreateNullableNumbers([Number("1")])], [true]));
        Assert.Throws<ArgumentException>(() => new JsonScalarRecordSchema([new("values", JsonRecordFieldKind.ExactNumberArray, Optional: true)]));
        var result = JsonScalarRecordCodec.Decode(required, Utf8("{\"values\":[1,null]}"));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.TypeMismatch, "$[\"values\"][1]"), result.Error);
    }
}
