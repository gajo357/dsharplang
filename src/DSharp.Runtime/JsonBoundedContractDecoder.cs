namespace DSharp.Runtime;

// An opt-in byte-length guard, not a transport/admission limit or a bound on
// parser allocations. Existing catalog decoding is unchanged.
internal sealed class JsonBoundedContractDecoder
{
    private readonly JsonContractCatalog catalog;
    private readonly int maxMetadataBytes;
    private readonly int maxPayloadBytes;

    internal JsonBoundedContractDecoder(JsonContractCatalog catalog, int maxMetadataBytes, int maxPayloadBytes)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        if (maxMetadataBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxMetadataBytes));
        if (maxPayloadBytes < 0) throw new ArgumentOutOfRangeException(nameof(maxPayloadBytes));
        this.maxMetadataBytes = maxMetadataBytes;
        this.maxPayloadBytes = maxPayloadBytes;
    }

    internal JsonScalarResult<JsonValidatedRecordPayload> DecodeRecord(ReadOnlyMemory<byte> metadata, ReadOnlyMemory<byte> payload) =>
        TooLarge(metadata, payload) ? JsonScalarResult<JsonValidatedRecordPayload>.Failure(JsonScalarErrorCode.InputTooLarge)
            : catalog.DecodeRecord(metadata, payload);

    internal JsonScalarResult<JsonValidatedUnionPayload> DecodeUnion(ReadOnlyMemory<byte> metadata, ReadOnlyMemory<byte> payload) =>
        TooLarge(metadata, payload) ? JsonScalarResult<JsonValidatedUnionPayload>.Failure(JsonScalarErrorCode.InputTooLarge)
            : catalog.DecodeUnion(metadata, payload);

    private bool TooLarge(ReadOnlyMemory<byte> metadata, ReadOnlyMemory<byte> payload) =>
        metadata.Length > maxMetadataBytes || payload.Length > maxPayloadBytes;
}
