using System;
using System.Globalization;
using System.Text;

namespace DSharp.Compiler;

// Validate the mathematical decimal magnitude before using the runtime's
// correctly-rounded binary32 parser. Parsing alone is insufficient: a number
// just above float.MaxValue can round down to float.MaxValue.
internal static class Float32Literals
{
    private const string Maximum = "340282346638528859811704183484516925440";
    // Larger than any possible source token length, while keeping all
    // exponent arithmetic comfortably inside Int64.
    private const long ExponentLimit = 10_000_000_000L;

    internal static bool TryParse(string text, out uint bits)
    {
        bits = 0;
        if (text.Length < 2 || text[^1] != 'f') return false;
        var end = text.Length - 1;
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
        if (index < end && text[index] == '.')
        {
            index++;
            if (!ReadDigits(digits, out fractionDigits)) return false;
        }

        long exponent = 0;
        if (index < end && text[index] is 'e' or 'E')
        {
            index++;
            var negative = index < end && text[index] == '-';
            if (index < end && text[index] is '+' or '-') index++;
            var exponentDigits = new StringBuilder();
            if (!ReadDigits(exponentDigits, out _)) return false;
            foreach (var digit in exponentDigits.ToString())
                exponent = Math.Min(ExponentLimit, exponent * 10 + digit - '0');
            if (negative) exponent = -exponent;
        }
        if (index != end) return false;

        var significant = digits.ToString().TrimStart('0');
        // Every spelling of mathematical zero is representable, including an
        // exponent too large for the runtime parser to consume.
        if (significant.Length == 0) return true;

        var integerDigits = (long)significant.Length + exponent - fractionDigits;
        if (integerDigits > Maximum.Length) return false;
        if (integerDigits == Maximum.Length)
        {
            // The decimal point lies just after the maximum's final digit.
            // Compare those digits without constructing an unbounded power of
            // ten; any extra nonzero fractional digits exceed an exact tie.
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
        // Any positive value below 10^-46 is less than half the smallest
        // positive subnormal and therefore rounds to +0. This also avoids
        // passing adversarially large negative exponents to the CLR parser.
        if (integerDigits < -45) return true;
        var invariant = text[..end].Replace("_", "", StringComparison.Ordinal);
        if (!float.TryParse(invariant, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || float.IsInfinity(value))
            return false;
        bits = unchecked((uint)BitConverter.SingleToInt32Bits(value));
        return true;
    }
}
