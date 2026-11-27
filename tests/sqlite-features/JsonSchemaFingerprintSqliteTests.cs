using System.Diagnostics;
using System.Reflection;
using System.Text;
using DSharp.Runtime.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DSharp.Runtime.Sqlite.Tests.Features;

public sealed class JsonSchemaFingerprintSqliteTests
{
    private const string WriterDirectory = "DSHARP_FINGERPRINT_SQLITE_WRITER_DIRECTORY";
    private const string App = "fingerprint-json-restart";

    [Fact]
    public void ReconstructedSchemaSelectsPersistedPayloadAfterWriterExit()
    {
        var childDirectory = Environment.GetEnvironmentVariable(WriterDirectory);
        if (childDirectory is not null)
        {
            Write(Path.Combine(childDirectory, "actor.db"));
            File.WriteAllText(Path.Combine(childDirectory, "complete"), "ok");
            return;
        }
        var directory = Path.Combine(Path.GetTempPath(), $"dsharp-fingerprint-sqlite-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            RunWriter(directory);
            var path = Path.Combine(directory, "actor.db");
            var saved = Assert.IsType<ActorSqlitePinnedWork>(ActorSqlitePinnedWorkReader.Read(path, App, "good-inv"));
            var same = new Bridge(changed: false); var changed = new Bridge(changed: true);
            Assert.Equal(same.Identity, saved.Contract); Assert.NotEqual(changed.Identity, saved.Contract);
            var decoded = same.Decode(saved.Contract, saved.Payload);
            Assert.True(same.Success(decoded)); Assert.Equal(9007199254740993.125m, same.Amount(decoded));
            var wrongSchema = changed.Decode(saved.Contract, saved.Payload);
            Assert.False(changed.Success(wrongSchema)); Assert.Contains("ContractMismatch", changed.Error(wrongSchema), StringComparison.Ordinal);
            var corrupt = Assert.IsType<ActorSqlitePinnedWork>(ActorSqlitePinnedWorkReader.Read(path, App, "corrupt-inv"));
            var rejected = same.Decode(corrupt.Contract, corrupt.Payload);
            Assert.False(same.Success(rejected)); Assert.Contains("TypeMismatch", same.Error(rejected), StringComparison.Ordinal);
            Assert.DoesNotContain("secret", same.Error(rejected), StringComparison.Ordinal);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void RunWriter(string directory)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("vstest"); start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        start.ArgumentList.Add("--TestCaseFilter:FullyQualifiedName=DSharp.Runtime.Sqlite.Tests.Features.JsonSchemaFingerprintSqliteTests.ReconstructedSchemaSelectsPersistedPayloadAfterWriterExit");
        start.Environment[WriterDirectory] = directory;
        using var child = Process.Start(start) ?? throw new InvalidOperationException("Could not start writer.");
        var output = child.StandardOutput.ReadToEndAsync(); var error = child.StandardError.ReadToEndAsync();
        if (!child.WaitForExit(60000)) { child.Kill(entireProcessTree: true); child.WaitForExit(); throw new TimeoutException("Writer timed out."); }
        Assert.True(child.ExitCode == 0, output.GetAwaiter().GetResult() + error.GetAwaiter().GetResult());
        Assert.Equal("ok", File.ReadAllText(Path.Combine(directory, "complete")));
    }

    // Fixture SQL stores a catalog-encoded pair; admission and schema evolution
    // remain outside this test and no public durable format is adopted.
    private static void Write(string path)
    {
        var bridge = new Bridge(changed: false); var encoded = bridge.Encode();
        Assert.True(bridge.Success(bridge.Decode(encoded.Identity, encoded.Payload)));
        using var db = ActorSqliteDatabase.OpenAndBind(path, App); using var tx = db.BeginTransaction();
        Insert(db, tx, "good", encoded.Identity, encoded.Payload);
        Insert(db, tx, "corrupt", encoded.Identity, Encoding.UTF8.GetBytes("{\"name\":\"secret-value\",\"amount\":\"secret\"}"));
        tx.Commit();
    }

    private static void Insert(SqliteConnection db, SqliteTransaction tx, string id, byte[] identity, byte[] payload)
    {
        using var command = db.CreateCommand(); command.Transaction = tx;
        command.CommandText = "INSERT INTO actor_submissions VALUES ($app,$sub,'targeted',$contract,$payload,x''); INSERT INTO actor_invocations(application_id,invocation_id,submission_id,requested_actor_id,selected_actor_id,operation) VALUES ($app,$inv,$sub,'actor','selected','invoke')";
        command.Parameters.AddWithValue("$app", App); command.Parameters.AddWithValue("$sub", id + "-sub");
        command.Parameters.AddWithValue("$inv", id + "-inv"); command.Parameters.AddWithValue("$contract", identity); command.Parameters.AddWithValue("$payload", payload);
        command.ExecuteNonQuery();
    }

    private sealed class Bridge
    {
        private const BindingFlags HiddenInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags HiddenStatic = BindingFlags.Static | BindingFlags.NonPublic;
        private readonly object schema; private readonly object catalog; private readonly Type recordType;
        internal byte[] Identity { get; }
        internal Bridge(bool changed)
        {
            var runtime = Assembly.Load("DSharp.Runtime");
            Type Find(string name) => runtime.GetType($"DSharp.Runtime.{name}", throwOnError: true)!;
            object New(Type type, params object[] args) => Activator.CreateInstance(type, HiddenInstance, binder: null, args: args, culture: null)!;
            object Field(string name, string kind, bool optional = false) => Activator.CreateInstance(Find("JsonRecordField"), name, Enum.Parse(Find("JsonRecordFieldKind"), kind), optional, null, null)!;
            var fields = Array.CreateInstance(Find("JsonRecordField"), 2);
            fields.SetValue(Field("name", "String"), 0); fields.SetValue(changed ? Field("amount", "NullableDecimal", true) : Field("amount", "Decimal"), 1);
            schema = New(Find("JsonScalarRecordSchema"), fields); recordType = Find("JsonScalarRecord");
            var fingerprint = (byte[])Find("JsonSchemaFingerprint").GetMethod("ForRecord", HiddenStatic)!.Invoke(null, [schema])!;
            Identity = Encoding.UTF8.GetBytes("test-logical-contract-v1:").Concat(fingerprint).ToArray();
            var identity = New(Find("JsonContractIdentity"), Identity);
            var registration = Find("JsonContractRegistration").GetMethod("ForRecord", HiddenStatic)!.Invoke(null, [identity, schema])!;
            var registrations = Array.CreateInstance(Find("JsonContractRegistration"), 1); registrations.SetValue(registration, 0);
            catalog = New(Find("JsonContractCatalog"), registrations);
        }
        internal (byte[] Identity, byte[] Payload) Encode()
        {
            var record = recordType.GetMethod("Create", HiddenStatic)!.Invoke(null, [schema, new object?[] { "example", 9007199254740993.125m }, new[] { true, true }])!;
            var result = catalog.GetType().GetMethod("EncodeRecord", HiddenInstance)!.Invoke(catalog, [new ReadOnlyMemory<byte>(Identity), record])!;
            Assert.True(Success(result)); var pair = Property(result, "Value")!;
            return ((byte[])Property(pair, "Identity")!, (byte[])Property(pair, "Payload")!);
        }
        internal object Decode(byte[] identity, byte[] payload) => catalog.GetType().GetMethod("DecodeRecord", HiddenInstance)!.Invoke(catalog, [new ReadOnlyMemory<byte>(identity), new ReadOnlyMemory<byte>(payload)])!;
        private static object? Property(object value, string name) => value.GetType().GetProperty(name, HiddenInstance)!.GetValue(value);
        internal bool Success(object result) => (bool)Property(result, "IsSuccess")!;
        internal string Error(object result) => Property(result, "Error")!.ToString()!;
        internal object? Amount(object result)
        {
            var validated = Property(result, "Value")!; var record = Property(validated, "Value")!;
            return record.GetType().GetMethod("ValueAt", HiddenInstance)!.Invoke(record, [1]);
        }
    }
}
