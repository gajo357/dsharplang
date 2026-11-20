using System;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace DSharp.Compiler;

// Check the exact decimal magnitude before CLR rounding: an out-of-range
// literal may otherwise round down to the largest finite binary64 value.
internal static class Float64Literals
{
    // The largest finite binary64 is exactly 2^1024 - 2^971.
    private static readonly string Maximum = ((BigInteger.One << 1024) - (BigInteger.One << 971))
        .ToString(CultureInfo.InvariantCulture);
    // Above the maximum possible source-token length and bounded within Int64.
    private const long ExponentLimit = 10_000_000_000L;

    internal static bool TryParse(string text, out ulong bits)
    {
        bits = 0;
        var end = text.Length;
        if (end < 2) return false;
        var index = 0;
        var digits = new StringBuilder();

        bool ReadDigits(StringBuilder output, out int count)
        {
            count = 0;
            var previousDigit = false;
            while (index < end && (char.IsAsciiDigit(text[index]) || text[index] == '_'))
            {
                var character = text[index++];
                if (character == '_')
                {
                    if (!previousDigit || index == end || !char.IsAsciiDigit(text[index])) return false;
                    previousDigit = false;
                }
                else
                {
                    output.Append(character);
                    count++;
                    previousDigit = true;
                }
            }
            return previousDigit;
        }

        if (!ReadDigits(digits, out _)) return false;
        var fractionDigits = 0;
        var hasFraction = false;
        if (index < end && text[index] == '.')
        {
            hasFraction = true;
            index++;
            if (!ReadDigits(digits, out fractionDigits)) return false;
        }

        long exponent = 0;
        var hasExponent = false;
        if (index < end && text[index] is 'e' or 'E')
        {
            hasExponent = true;
            index++;
            var negative = index < end && text[index] == '-';
            if (index < end && text[index] is '+' or '-') index++;
            var exponentDigits = new StringBuilder();
            if (!ReadDigits(exponentDigits, out _)) return false;
            foreach (var digit in exponentDigits.ToString())
                exponent = Math.Min(ExponentLimit, exponent * 10 + digit - '0');
            if (negative) exponent = -exponent;
        }
        if (index != end || !(hasFraction || hasExponent)) return false;

        var significant = digits.ToString().TrimStart('0');
        // Zero remains representable even with an arbitrarily large exponent.
        if (significant.Length == 0) return true;

        var integerDigits = (long)significant.Length + exponent - fractionDigits;
        if (integerDigits > Maximum.Length) return false;
        if (integerDigits == Maximum.Length)
        {
            // Compare to the exact integer maximum without allocating an
            // exponent-sized integer. Fractional nonzero digits exceed a tie.
            for (var i = 0; i < Maximum.Length; i++)
            {
                var digit = i < significant.Length ? significant[i] : '0';
                if (digit > Maximum[i]) return false;
                if (digit < Maximum[i]) goto WithinRange;
            }
            for (var i = Maximum.Length; i < significant.Length; i++)
                if (significant[i] != '0') return false;
        }

    WithinRange:
        // Any positive value below 10^-324 is less than half the smallest
        // subnormal; avoid passing enormous negative exponents to the parser.
        if (integerDigits < -323) return true;
        var invariant = text.Replace("_", "", StringComparison.Ordinal);
        if (!double.TryParse(invariant, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || double.IsInfinity(value))
            return false;
        bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
        return true;
    }
}
