using System.Globalization;
using System.Reflection;
using System.Text;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonDecimalLongTokenTests
{
    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);

    [Fact]
    public void LongZeroRunsAndExponentsRetainExactValues()
    {
        const int count = 100000;
        var zeros = new string('0', count);
        var cases = new (string Token, decimal Value)[] {
            ("0." + zeros + "1e" + (count + 1).ToString(CultureInfo.InvariantCulture), 1m),
            ("1" + zeros + "e-" + count.ToString(CultureInfo.InvariantCulture), 1m),
            ("1" + zeros + "e-" + (count + 28).ToString(CultureInfo.InvariantCulture), 0.0000000000000000000000000001m),
            ("-0." + zeros + "79228162514264337593543950335e" + (count + 29).ToString(CultureInfo.InvariantCulture), decimal.MinValue),
            ("0e" + new string('9', 2000), 0m),
            ("0e-" + new string('9', 2000), 0m) };
        foreach (var (token, expected) in cases)
        {
            var result = JsonDecimalCodec.Decode(Bytes(token));
            Assert.True(result.IsSuccess);
            Assert.Equal(expected, result.Value);
        }
        foreach (var token in new[] {
            "1" + zeros + "e-" + (count + 29).ToString(CultureInfo.InvariantCulture),
            "79228162514264337593543950336" + zeros + "e-" + count.ToString(CultureInfo.InvariantCulture),
            "1e" + new string('9', 2000), "1e-" + new string('9', 2000) })
            Assert.Equal(JsonScalarErrorCode.InvalidDecimal, JsonDecimalCodec.Decode(Bytes(token)).Error!.Value.Code);
    }

    [Fact]
    public void VariedZeroPlacementMatchesAnIndependentDecimalValue()
    {
        var random = new Random(2048);
        for (var i = 0; i < 500; i++)
        {
            var digits = random.NextInt64(1, 9_000_000_000_000).ToString(CultureInfo.InvariantCulture);
            var leading = random.Next(0, 25);
            var trailing = random.Next(0, 25);
            var scale = random.Next(0, 13);
            var sign = random.Next(2) == 0 ? "" : "-";
            var exponent = leading + digits.Length - scale;
            var token = sign + "0." + new string('0', leading) + digits + new string('0', trailing) +
                "e" + exponent.ToString(CultureInfo.InvariantCulture);
            var expected = decimal.Parse(sign + digits + "e-" + scale.ToString(CultureInfo.InvariantCulture),
                NumberStyles.Float, CultureInfo.InvariantCulture);
            var actual = JsonDecimalCodec.Decode(Bytes(token));
            Assert.True(actual.IsSuccess, $"Case {i} did not decode.");
            Assert.Equal(expected, actual.Value);
        }
    }

    [Fact]
    public void LongNestedValuesStayExactAndInvalidPathsStayRedacted()
    {
        const int count = 50000;
        var token = "0." + new string('0', count) + "125e" + (count + 1).ToString(CultureInfo.InvariantCulture);
        var fields = new JsonScalarRecordSchema([new("values", JsonRecordFieldKind.DecimalNullableElementsArray)]);
        var schema = new JsonTaggedUnionSchema([new("Prices", fields)]);
        var input = "{\"case\":\"Prices\",\"value\":{\"values\":[null," + token + "]}}";
        var decoded = JsonValidatedUnionPayload.Decode(schema, Bytes(input));
        Assert.True(decoded.IsSuccess);
        var payload = JsonValidatedUnionPayload.FromValue(decoded.Value.Value);
        Assert.Equal("{\"case\":\"Prices\",\"value\":{\"values\":[null,1.25]}}", Encoding.UTF8.GetString(payload.Bytes));
        var tree = JsonTypedFieldMapping.FromUnion(payload.Value);
        Assert.True(tree.IsSuccess);
        Assert.True(JsonTypedFieldMapping.ToUnion(schema, tree.Value).IsSuccess);
        var invalid = "{\"case\":\"Prices\",\"value\":{\"values\":[null,1e-" + new string('9', 2000) + "]}}";
        var result = JsonValidatedUnionPayload.Decode(schema, Bytes(invalid));
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidDecimal, "$[\"value\"][\"values\"][1]"), result.Error);
        Assert.DoesNotContain("9999", result.Error!.Value.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ConversionDoesNotCopyTheFullMantissaAgain()
    {
        var method = typeof(JsonDecimalCodec).GetMethod("TryExact", BindingFlags.NonPublic | BindingFlags.Static)!;
        var token = "0." + new string('0', 100000) + "1e100001";
        object[] arguments = [token, 0m];
        Assert.True((bool)method.Invoke(null, arguments)!);
        Assert.True((bool)method.Invoke(null, arguments)!);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var accepted = (bool)method.Invoke(null, arguments)!;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(accepted);
        Assert.Equal(1m, arguments[1]);
        Assert.True(allocated < 8192, $"The conversion allocated {allocated} bytes after the token already existed.");
    }
}
