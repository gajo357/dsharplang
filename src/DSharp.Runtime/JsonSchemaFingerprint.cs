using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace DSharp.Runtime;

// Opt-in strict descriptor-shape fingerprint, not nominal identity, semantic
// compatibility, authenticity, a public wire format or automatic migration.
// Same-shaped independent contracts produce the same fingerprint; callers must
// supply their own logical namespace. The v1 framing is fixed; a future change
// requires a separate versioned algorithm.
internal static class JsonSchemaFingerprint
{
    internal static byte[] ForRecord(JsonScalarRecordSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        return new Walker().Record(schema);
    }

    internal static byte[] ForUnion(JsonTaggedUnionSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        return new Walker().Union(schema);
    }

    private sealed class Walker
    {
        private readonly Dictionary<JsonScalarRecordSchema, byte[]> records = new();
        private readonly Dictionary<JsonTaggedUnionSchema, byte[]> unions = new();

        internal byte[] Record(JsonScalarRecordSchema schema)
        {
            if (records.TryGetValue(schema, out var cached)) return cached;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            Prefix(hash, 1); Number(hash, schema.Count);
            for (var index = 0; index < schema.Count; index++)
            {
                var field = schema.FieldAt(index);
                Text(hash, field.Name); Text(hash, field.Kind.ToString()); Tag(hash, field.Optional ? (byte)1 : (byte)0);
                if (field.NestedSchema is { } record) { Tag(hash, 1); hash.AppendData(Record(record)); }
                else if (field.UnionSchema is { } union) { Tag(hash, 2); hash.AppendData(Union(union)); }
                else Tag(hash, 0);
            }
            var result = hash.GetHashAndReset(); records.Add(schema, result); return result;
        }

        internal byte[] Union(JsonTaggedUnionSchema schema)
        {
            if (unions.TryGetValue(schema, out var cached)) return cached;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            Prefix(hash, 2); Number(hash, schema.Count);
            for (var index = 0; index < schema.Count; index++)
            {
                var item = schema.CaseAt(index);
                Text(hash, item.Name);
                if (item.Payload is { } payload) { Tag(hash, 1); hash.AppendData(Record(payload)); }
                else Tag(hash, 0);
            }
            var result = hash.GetHashAndReset(); unions.Add(schema, result); return result;
        }

        private static void Prefix(IncrementalHash hash, byte kind)
        {
            hash.AppendData("DSharp.JsonSchemaFingerprint.v1"u8); Tag(hash, kind);
        }

        private static void Tag(IncrementalHash hash, byte value)
        {
            Span<byte> data = stackalloc byte[1]; data[0] = value; hash.AppendData(data);
        }

        private static void Number(IncrementalHash hash, int value)
        {
            Span<byte> data = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(data, value); hash.AppendData(data);
        }

        private static void Text(IncrementalHash hash, string value)
        {
            var data = Encoding.UTF8.GetBytes(value); Number(hash, data.Length); hash.AppendData(data);
        }
    }
}
