using System.Buffers.Binary;

namespace DSharp.Runtime;

// An opt-in internal composition, not a public wire format or compatibility
// policy. Callers own namespace uniqueness and versioning.
internal static class JsonNamespacedSchemaIdentity
{
    internal static JsonContractIdentity ForRecord(byte[] logicalNamespace, JsonScalarRecordSchema schema)
    {
        var name = Snapshot(logicalNamespace);
        return Compose(name, JsonSchemaFingerprint.ForRecord(schema));
    }

    internal static JsonContractIdentity ForUnion(byte[] logicalNamespace, JsonTaggedUnionSchema schema)
    {
        var name = Snapshot(logicalNamespace);
        return Compose(name, JsonSchemaFingerprint.ForUnion(schema));
    }

    private static byte[] Snapshot(byte[] logicalNamespace)
    {
        ArgumentNullException.ThrowIfNull(logicalNamespace);
        if (logicalNamespace.Length == 0) throw new ArgumentException("Logical namespace must not be empty.", nameof(logicalNamespace));
        return (byte[])logicalNamespace.Clone();
    }

    private static JsonContractIdentity Compose(byte[] logicalNamespace, byte[] fingerprint)
    {
        ReadOnlySpan<byte> prefix = "DSharp.JsonNamespacedSchemaIdentity.v1"u8;
        var bytes = new byte[checked(prefix.Length + 4 + logicalNamespace.Length + fingerprint.Length)];
        prefix.CopyTo(bytes);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(prefix.Length, 4), logicalNamespace.Length);
        logicalNamespace.CopyTo(bytes.AsSpan(prefix.Length + 4));
        fingerprint.CopyTo(bytes.AsSpan(prefix.Length + 4 + logicalNamespace.Length));
        return new JsonContractIdentity(bytes);
    }
}
