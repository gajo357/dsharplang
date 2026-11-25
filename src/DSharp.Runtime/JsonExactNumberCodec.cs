using System.Text;
using System.Text.Json;

namespace DSharp.Runtime;

// An immutable validated JSON-number spelling, not a D# numeric type, numeric
// comparison policy, canonical form, or durable message wire contract.
internal sealed class JsonExactNumber
{
    private JsonExactNumber(string token) { Token = token; }
    internal string Token { get; }

    internal static JsonScalarResult<JsonExactNumber> Read(JsonElement value) => value.ValueKind == JsonValueKind.Number
        ? JsonScalarResult<JsonExactNumber>.Success(new JsonExactNumber(value.GetRawText()))
        : JsonScalarResult<JsonExactNumber>.Failure(JsonScalarErrorCode.TypeMismatch);
}

internal static class JsonExactNumberCodec
{
    internal static byte[] Encode(JsonExactNumber value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Encoding.UTF8.GetBytes(value.Token);
    }

    internal static JsonScalarResult<JsonExactNumber> Decode(ReadOnlyMemory<byte> input)
    {
        try
        {
            using var document = JsonDocument.Parse(input);
            return JsonExactNumber.Read(document.RootElement);
        }
        catch (JsonException)
        {
            return JsonScalarResult<JsonExactNumber>.Failure(JsonScalarErrorCode.InvalidJson);
        }
    }
}
