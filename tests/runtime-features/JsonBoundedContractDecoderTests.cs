using System.Buffers;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class JsonBoundedContractDecoderTests
{
    [Fact]
    public void ExactLimitsPreserveRecordAndUnionDecoding()
    {
        var recordSchema = new JsonScalarRecordSchema([new("value", JsonRecordFieldKind.String)]);
        var unionSchema = new JsonTaggedUnionSchema([new("Message", recordSchema)]);
        var recordId = new JsonContractIdentity("record"u8.ToArray());
        var unionId = new JsonContractIdentity("union"u8.ToArray());
        var catalog = new JsonContractCatalog([JsonContractRegistration.ForRecord(recordId, recordSchema), JsonContractRegistration.ForUnion(unionId, unionSchema)]);
        var record = JsonScalarRecord.Create(recordSchema, ["value"], [true]);
        var encodedRecord = new JsonRecordContract(recordId, recordSchema).Encode(record);
        var encodedUnion = new JsonUnionContract(unionId, unionSchema).Encode(JsonTaggedUnion.Create(unionSchema, 0, record));
        var boundedRecord = new JsonBoundedContractDecoder(catalog, encodedRecord.Identity.Length, encodedRecord.Payload.Length);
        var boundedUnion = new JsonBoundedContractDecoder(catalog, encodedUnion.Identity.Length, encodedUnion.Payload.Length);
        var result = boundedRecord.DecodeRecord(encodedRecord.Identity, encodedRecord.Payload);
        Assert.True(result.IsSuccess); Assert.Equal("value", result.Value.Value.ValueAt(0));
        var union = boundedUnion.DecodeUnion(encodedUnion.Identity, encodedUnion.Payload);
        Assert.True(union.IsSuccess); Assert.Equal("value", union.Value.Value.Payload!.ValueAt(0));
    }

    [Fact]
    public void OversizedInputIsRejectedBeforeEitherMemoryIsRead()
    {
        var decoder = new JsonBoundedContractDecoder(new JsonContractCatalog([]), 3, 4);
        using var oversizedMetadata = new TrapMemory(4);
        using var boundedMetadata = new TrapMemory(3);
        using var oversizedPayload = new TrapMemory(5);
        using var boundedPayload = new TrapMemory(4);
        var expected = new JsonScalarError(JsonScalarErrorCode.InputTooLarge, "$");
        Assert.Equal(expected, decoder.DecodeRecord(oversizedMetadata.ReadOnly, boundedPayload.ReadOnly).Error);
        Assert.Equal(expected, decoder.DecodeRecord(boundedMetadata.ReadOnly, oversizedPayload.ReadOnly).Error);
        Assert.Equal(expected, decoder.DecodeUnion(oversizedMetadata.ReadOnly, boundedPayload.ReadOnly).Error);
        Assert.Equal(expected, decoder.DecodeUnion(boundedMetadata.ReadOnly, oversizedPayload.ReadOnly).Error);
        Assert.Equal(0, oversizedMetadata.Reads + boundedMetadata.Reads + oversizedPayload.Reads + boundedPayload.Reads);
    }

    [Fact]
    public void InvalidLimitsFailAndWithinLimitsPreserveRedactedErrors()
    {
        var schema = new JsonScalarRecordSchema([]); var id = new JsonContractIdentity("id"u8.ToArray());
        var catalog = new JsonContractCatalog([JsonContractRegistration.ForRecord(id, schema)]);
        Assert.Throws<ArgumentNullException>(() => new JsonBoundedContractDecoder(null!, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new JsonBoundedContractDecoder(catalog, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new JsonBoundedContractDecoder(catalog, 1, -1));
        var bounded = new JsonBoundedContractDecoder(catalog, 2, 8);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.ContractMismatch, "$"), bounded.DecodeRecord("xx"u8.ToArray(), "{secret"u8.ToArray()).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidJson, "$"), bounded.DecodeRecord(id.Bytes, "{secret"u8.ToArray()).Error);
        var zero = new JsonBoundedContractDecoder(catalog, 2, 0);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InputTooLarge, "$"), zero.DecodeRecord(id.Bytes, "{"u8.ToArray()).Error);
        Assert.Equal(new JsonScalarError(JsonScalarErrorCode.InvalidJson, "$"), zero.DecodeRecord(id.Bytes, ReadOnlyMemory<byte>.Empty).Error);
    }

    private sealed class TrapMemory(int length) : MemoryManager<byte>
    {
        internal int Reads { get; private set; }
        internal ReadOnlyMemory<byte> ReadOnly => CreateMemory(length);
        public override Span<byte> GetSpan() { Reads++; throw new InvalidOperationException("Input was read."); }
        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();
        public override void Unpin() { }
        protected override void Dispose(bool disposing) { }
    }
}
