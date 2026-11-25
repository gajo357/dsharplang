using System.Buffers;
using System.Text.Json;

namespace DSharp.Runtime;

// Internal scalar building blocks only. These APIs do not define a public D#
// JSON surface, durable wire identity, or canonical semantic equality.
internal enum JsonScalarErrorCode { InvalidJson, TypeMismatch, InvalidInteger, InvalidUnicode }
internal readonly record struct JsonScalarError(JsonScalarErrorCode Code, string Path);

internal readonly struct JsonScalarResult<T>
{
    private readonly T value;

    private JsonScalarResult(bool isSuccess, T value, JsonScalarError? error)
    {
        IsSuccess = isSuccess;
        this.value = value;
        Error = error;
    }

    internal bool IsSuccess { get; }
    internal JsonScalarError? Error { get; }
    internal T Value => IsSuccess ? value : throw new InvalidOperationException("A failed decode has no value.");
    internal static JsonScalarResult<T> Success(T value) => new(true, value, null);
    internal static JsonScalarResult<T> Failure(JsonScalarErrorCode code, string path = "$") => new(false, default!, new(code, path));
}

internal static class JsonScalarCodec
{
    internal static byte[] EncodeBoolean(bool value) => Write(writer => writer.WriteBooleanValue(value));
    internal static byte[] EncodeInteger(int value) => Write(writer => writer.WriteNumberValue(value));

    internal static byte[] EncodeString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidateUtf16(value);
        return Write(writer => writer.WriteStringValue(value));
    }

    internal static byte[] EncodeNullableBoolean(bool? value) => value.HasValue ? EncodeBoolean(value.Value) : EncodeNull();
    internal static byte[] EncodeNullableInteger(int? value) => value.HasValue ? EncodeInteger(value.Value) : EncodeNull();
    internal static byte[] EncodeNullableString(string? value) => value is null ? EncodeNull() : EncodeString(value);

    internal static JsonScalarResult<bool> DecodeBoolean(ReadOnlyMemory<byte> input) => Decode(input, ReadBoolean);
    internal static JsonScalarResult<int> DecodeInteger(ReadOnlyMemory<byte> input) => Decode(input, ReadInteger);
    internal static JsonScalarResult<string> DecodeString(ReadOnlyMemory<byte> input) => Decode(input, ReadString);
    internal static JsonScalarResult<bool?> DecodeNullableBoolean(ReadOnlyMemory<byte> input) => Decode(input, ReadNullableBoolean);
    internal static JsonScalarResult<int?> DecodeNullableInteger(ReadOnlyMemory<byte> input) => Decode(input, ReadNullableInteger);
    internal static JsonScalarResult<string?> DecodeNullableString(ReadOnlyMemory<byte> input) => Decode(input, ReadNullableString);

    private static JsonScalarResult<T> Decode<T>(ReadOnlyMemory<byte> input, Func<JsonElement, JsonScalarResult<T>> read)
    {
        try
        {
            using var document = JsonDocument.Parse(input);
            return read(document.RootElement);
        }
        catch (JsonException)
        {
            // Never return parser text: it may contain untrusted input.
            return JsonScalarResult<T>.Failure(JsonScalarErrorCode.InvalidJson);
        }
    }

    internal static JsonScalarResult<bool> ReadBoolean(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True => JsonScalarResult<bool>.Success(true),
        JsonValueKind.False => JsonScalarResult<bool>.Success(false),
        _ => JsonScalarResult<bool>.Failure(JsonScalarErrorCode.TypeMismatch)
    };

    internal static JsonScalarResult<int> ReadInteger(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number)
            return JsonScalarResult<int>.Failure(JsonScalarErrorCode.TypeMismatch);
        return value.TryGetInt32(out var result)
            ? JsonScalarResult<int>.Success(result)
            : JsonScalarResult<int>.Failure(JsonScalarErrorCode.InvalidInteger);
    }

    internal static JsonScalarResult<string> ReadString(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
            return JsonScalarResult<string>.Failure(JsonScalarErrorCode.TypeMismatch);
        try
        {
            var text = value.GetString()!;
            return IsValidUtf16(text)
                ? JsonScalarResult<string>.Success(text)
                : JsonScalarResult<string>.Failure(JsonScalarErrorCode.InvalidUnicode);
        }
        catch (InvalidOperationException)
        {
            return JsonScalarResult<string>.Failure(JsonScalarErrorCode.InvalidUnicode);
        }
    }

    internal static JsonScalarResult<bool?> ReadNullableBoolean(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null) return JsonScalarResult<bool?>.Success(null);
        var result = ReadBoolean(value);
        return result.IsSuccess ? JsonScalarResult<bool?>.Success(result.Value) : JsonScalarResult<bool?>.Failure(result.Error!.Value.Code);
    }

    internal static JsonScalarResult<int?> ReadNullableInteger(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null) return JsonScalarResult<int?>.Success(null);
        var result = ReadInteger(value);
        return result.IsSuccess ? JsonScalarResult<int?>.Success(result.Value) : JsonScalarResult<int?>.Failure(result.Error!.Value.Code);
    }

    internal static JsonScalarResult<string?> ReadNullableString(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null) return JsonScalarResult<string?>.Success(null);
        var result = ReadString(value);
        return result.IsSuccess ? JsonScalarResult<string?>.Success(result.Value) : JsonScalarResult<string?>.Failure(result.Error!.Value.Code);
    }

    private static byte[] EncodeNull() => Write(writer => writer.WriteNullValue());

    private static byte[] Write(Action<Utf8JsonWriter> write)
    {
        var output = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(output))
        {
            write(writer);
            writer.Flush();
        }
        return output.WrittenSpan.ToArray();
    }

    internal static void ValidateUtf16(string value)
    {
        if (!IsValidUtf16(value)) throw new ArgumentException("The string contains invalid UTF-16.", nameof(value));
    }

    private static bool IsValidUtf16(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (char.IsHighSurrogate(value[index]))
            {
                if (++index >= value.Length || !char.IsLowSurrogate(value[index])) return false;
            }
            else if (char.IsLowSurrogate(value[index])) return false;
        }
        return true;
    }
}
