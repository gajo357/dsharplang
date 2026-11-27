using System.Buffers;
using System.Globalization;
using System.Text.Json;

namespace DSharp.Runtime;

// Exact typed decimal values only, not a spelling-preserving number token or
// an adopted public D# API, canonical form or durable encoding.
internal static class JsonDecimalCodec
{
    internal static byte[] Encode(decimal value)
    {
        var output = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(output)) { writer.WriteNumberValue(value); writer.Flush(); }
        return output.WrittenSpan.ToArray();
    }

    internal static byte[] EncodeNullable(decimal? value) => value.HasValue ? Encode(value.Value) : "null"u8.ToArray();
    internal static JsonScalarResult<decimal> Decode(ReadOnlyMemory<byte> input) => DecodeValue(input, Read);
    internal static JsonScalarResult<decimal?> DecodeNullable(ReadOnlyMemory<byte> input) => DecodeValue(input, ReadNullable);

    private static JsonScalarResult<T> DecodeValue<T>(ReadOnlyMemory<byte> input, Func<JsonElement, JsonScalarResult<T>> read)
    {
        try { using var document = JsonDocument.Parse(input); return read(document.RootElement); }
        catch (JsonException) { return JsonScalarResult<T>.Failure(JsonScalarErrorCode.InvalidJson); }
    }

    internal static JsonScalarResult<decimal> Read(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number) return JsonScalarResult<decimal>.Failure(JsonScalarErrorCode.TypeMismatch);
        return TryExact(value.GetRawText(), out var number) ? JsonScalarResult<decimal>.Success(number)
            : JsonScalarResult<decimal>.Failure(JsonScalarErrorCode.InvalidDecimal);
    }

    internal static JsonScalarResult<decimal?> ReadNullable(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null) return JsonScalarResult<decimal?>.Success(null);
        var number = Read(value);
        return number.IsSuccess ? JsonScalarResult<decimal?>.Success(number.Value)
            : JsonScalarResult<decimal?>.Failure(number.Error!.Value.Code);
    }

    // JsonDocument has already validated the token grammar. Bound exponent
    // arithmetic before appending zeros, and remove only exact trailing zeros.
    private static bool TryExact(string token, out decimal result)
    {
        result = decimal.Zero;
        var negative = token[0] == '-';
        var start = negative ? 1 : 0;
        var exponentAt = token.IndexOfAny(['e', 'E']);
        var mantissaEnd = exponentAt < 0 ? token.Length : exponentAt;
        var mantissa = token.AsSpan(start, mantissaEnd - start);
        var dot = mantissa.IndexOf('.');
        var fractionLength = dot < 0 ? 0 : mantissa.Length - dot - 1;
        var first = -1;
        var digits = 0;
        var trailingZeros = 0;
        for (var index = 0; index < mantissa.Length; index++)
        {
            var digit = mantissa[index];
            if (digit == '.') continue;
            if (first < 0)
            {
                if (digit == '0') continue;
                first = index;
            }
            digits++;
            trailingZeros = digit == '0' ? trailingZeros + 1 : 0;
        }
        if (first < 0) return true;

        long exponent = 0;
        if (exponentAt >= 0)
        {
            var index = exponentAt + 1;
            var negativeExponent = token[index] == '-';
            if (token[index] is '+' or '-') index++;
            const long limit = (long)int.MaxValue + 32;
            for (; index < token.Length; index++) exponent = Math.Min(limit, exponent * 10 + token[index] - '0');
            if (negativeExponent) exponent = -exponent;
        }
        long scale = (long)fractionLength - exponent;
        var trim = (int)Math.Min(Math.Max(scale, 0), trailingZeros);
        digits -= trim;
        scale -= trim;
        if (scale > 28) return false;
        var appendedZeros = 0;
        if (scale < 0)
        {
            if (-scale > 29 || digits - scale > 29) return false;
            appendedZeros = (int)-scale;
            scale = 0;
        }
        if (digits > 29) return false;
        Span<char> coefficient = stackalloc char[29];
        var written = 0;
        for (var index = first; written < digits; index++)
            if (mantissa[index] != '.') coefficient[written++] = mantissa[index];
        coefficient.Slice(written, appendedZeros).Fill('0');
        written += appendedZeros;
        if (!decimal.TryParse(coefficient[..written], NumberStyles.None,
            CultureInfo.InvariantCulture, out var magnitude)) return false;
        var bits = decimal.GetBits(magnitude);
        result = new decimal(bits[0], bits[1], bits[2], negative, (byte)scale);
        return true;
    }
}
