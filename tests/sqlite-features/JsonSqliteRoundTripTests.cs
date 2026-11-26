using System.Diagnostics;
using System.Reflection;
using System.Text;
using DSharp.Runtime.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DSharp.Runtime.Sqlite.Tests.Features;

public sealed class JsonSqliteRoundTripTests
{
    private const string WriterDirectory = "DSHARP_JSON_SQLITE_WRITER_DIRECTORY";
    private const string App = "json-snapshot-test";
    private const string Valid = "{\"case\":\"Price\",\"value\":{\"amount\":9007199254740993.125,\"original\":1e-29,\"items\":[1.25,null,-2.5],\"child\":{\"note\":null}}}";
    private const string Invalid = "{\"case\":\"Price\",\"value\":{\"amount\":1e-29,\"original\":1,\"items\":[],\"child\":{\"note\":\"secret\"}}}";
    private static readonly byte[] Contract = Encoding.UTF8.GetBytes("test-owned-contract-v1");

    [Fact]
    public void ValidatedNestedUnionSurvivesWriterExitAndCorruptInputIsRejected()
    {
        var childDirectory = Environment.GetEnvironmentVariable(WriterDirectory);
        if (childDirectory is not null)
        {
            Write(Path.Combine(childDirectory, "actor.db"));
            File.WriteAllText(Path.Combine(childDirectory, "complete"), "ok");
            return;
        }

        var directory = Path.Combine(Path.GetTempPath(), $"dsharp-json-sqlite-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            RunWriter(directory);
            var path = Path.Combine(directory, "actor.db");
            var saved = Assert.IsType<ActorSqlitePinnedWork>(ActorSqlitePinnedWorkReader.Read(path, App, "valid-inv"));
            Assert.Equal(Contract, saved.Contract);
            Assert.Equal(Valid, Encoding.UTF8.GetString(saved.Payload));
            var json = new JsonBridge();
            var decoded = json.Decode(saved.Payload);
            Assert.True(json.Success(decoded));
            Assert.Equal(Valid, Encoding.UTF8.GetString(json.Bytes(decoded)));
            json.AssertTypedValues(decoded);
            saved.Payload[0] = 0;
            Assert.Equal(Valid, Encoding.UTF8.GetString(ActorSqlitePinnedWorkReader.Read(path, App, "valid-inv")!.Payload));
            var corrupt = Assert.IsType<ActorSqlitePinnedWork>(ActorSqlitePinnedWorkReader.Read(path, App, "corrupt-inv"));
            var rejected = json.Decode(corrupt.Payload);
            Assert.False(json.Success(rejected));
            Assert.DoesNotContain("secret", json.Error(rejected), StringComparison.Ordinal);
            Assert.Null(ActorSqlitePinnedWorkReader.Read(path, App, "invalid-inv"));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void RunWriter(string directory)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("vstest");
        start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        start.ArgumentList.Add("--TestCaseFilter:FullyQualifiedName=DSharp.Runtime.Sqlite.Tests.Features.JsonSqliteRoundTripTests.ValidatedNestedUnionSurvivesWriterExitAndCorruptInputIsRejected");
        start.Environment[WriterDirectory] = directory;
        using var child = Process.Start(start) ?? throw new InvalidOperationException("Could not start JSON SQLite writer.");
        var output = child.StandardOutput.ReadToEndAsync();
        var error = child.StandardError.ReadToEndAsync();
        if (!child.WaitForExit(60000)) { child.Kill(entireProcessTree: true); child.WaitForExit(); throw new TimeoutException("JSON SQLite writer timed out."); }
        var diagnostic = output.GetAwaiter().GetResult() + error.GetAwaiter().GetResult();
        Assert.True(child.ExitCode == 0, $"JSON SQLite writer failed: {diagnostic}");
        Assert.Equal("ok", File.ReadAllText(Path.Combine(directory, "complete")));
    }

    // This test alone bridges internal codec, volatile reference and SQLite;
    // neither the opaque provider nor the reference store adopts a wire contract.
    private static void Write(string path)
    {
        var json = new JsonBridge();
        var bad = json.Decode(Encoding.UTF8.GetBytes(Invalid));
        Assert.False(json.Success(bad));
        Assert.Contains("InvalidDecimal", json.Error(bad), StringComparison.Ordinal);
        Assert.DoesNotContain("secret", json.Error(bad), StringComparison.Ordinal);
        var source = Encoding.UTF8.GetBytes(Valid);
        var result = json.Decode(source);
        Assert.True(json.Success(result));
        Array.Fill(source, (byte)'x');
        var bytes = json.Bytes(result);
        Assert.Equal(Valid, Encoding.UTF8.GetString(bytes));

        var runtime = Assembly.Load("DSharp.Runtime");
        Type Find(string name) => runtime.GetType($"DSharp.Runtime.{name}", throwOnError: true)!;
        var store = Activator.CreateInstance(Find("VolatileTargetedStore"), TimeSpan.FromMinutes(1), Enum.Parse(Find("VolatileStoreRequirements"), "None"))!;
        var request = Activator.CreateInstance(Find("TargetedRequest"), App, "valid-sub", Enum.Parse(Find("TargetedOperation"), "Invoke"), "actor", bytes, Contract, Array.Empty<byte>(), null, null, null)!;
        var accepted = store.GetType().GetMethod("Accept")!.Invoke(store, [request, Guid.NewGuid(), "selected"])!;
        Assert.Equal("Created", Public(accepted, "Kind").ToString());
        var captured = Public(Public(accepted, "Record"), "Request");
        Array.Fill(bytes, (byte)'y');
        var capturedBytes = Assert.IsType<byte[]>(Public(captured, "Payload"));
        Assert.Equal(Valid, Encoding.UTF8.GetString(capturedBytes));

        using var db = ActorSqliteDatabase.OpenAndBind(path, App);
        using var tx = db.BeginTransaction();
        Insert(db, tx, "valid", (byte[])Public(captured, "ContractMetadata"), capturedBytes);
        Insert(db, tx, "corrupt", Contract, Encoding.UTF8.GetBytes(Invalid));
        tx.Commit();
    }

    private static object Public(object value, string name) => value.GetType().GetProperty(name)!.GetValue(value)!;
    private static void Insert(SqliteConnection db, SqliteTransaction tx, string id, byte[] contract, byte[] payload)
    {
        using var command = db.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "INSERT INTO actor_submissions VALUES ($app,$sub,'targeted',$contract,$payload,x''); INSERT INTO actor_invocations(application_id,invocation_id,submission_id,requested_actor_id,selected_actor_id,operation) VALUES ($app,$inv,$sub,'actor','selected','invoke')";
        command.Parameters.AddWithValue("$app", App);
        command.Parameters.AddWithValue("$sub", id + "-sub");
        command.Parameters.AddWithValue("$inv", id + "-inv");
        command.Parameters.AddWithValue("$contract", contract);
        command.Parameters.AddWithValue("$payload", payload);
        command.ExecuteNonQuery();
    }

    private sealed class JsonBridge
    {
        private const BindingFlags HiddenInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags HiddenStatic = BindingFlags.Static | BindingFlags.NonPublic;
        private readonly Assembly runtime = Assembly.Load("DSharp.Runtime");
        private readonly object schema;
        private readonly MethodInfo decode;
        internal JsonBridge()
        {
            var child = Record(Field("note", "NullableString"));
            var payload = Record(Field("amount", "Decimal"), Field("original", "ExactNumber"), Field("items", "DecimalNullableElementsArray"), Field("child", "Record", nested: child), Field("optional", "NullableString", optional: true));
            var cases = Array.CreateInstance(Find("JsonTaggedUnionCase"), 1);
            cases.SetValue(Activator.CreateInstance(Find("JsonTaggedUnionCase"), "Price", payload)!, 0);
            schema = HiddenNew(Find("JsonTaggedUnionSchema"), cases);
            decode = Find("JsonValidatedUnionPayload").GetMethod("Decode", HiddenStatic)!;
        }
        private Type Find(string name) => runtime.GetType($"DSharp.Runtime.{name}", throwOnError: true)!;
        private static object HiddenNew(Type type, object argument) => Activator.CreateInstance(type, HiddenInstance, binder: null, args: [argument], culture: null)!;
        private object Field(string name, string kind, bool optional = false, object? nested = null) =>
            Activator.CreateInstance(Find("JsonRecordField"), name, Enum.Parse(Find("JsonRecordFieldKind"), kind), optional, nested, null)!;
        private object Record(params object[] fields)
        {
            var array = Array.CreateInstance(Find("JsonRecordField"), fields.Length);
            for (var i = 0; i < fields.Length; i++) array.SetValue(fields[i], i);
            return HiddenNew(Find("JsonScalarRecordSchema"), array);
        }
        internal object Decode(byte[] input) => decode.Invoke(null, [schema, new ReadOnlyMemory<byte>(input)])!;
        private static object? Property(object value, string name) => value.GetType().GetProperty(name, HiddenInstance)!.GetValue(value);
        internal bool Success(object value) => (bool)Property(value, "IsSuccess")!;
        internal string Error(object value) => Property(value, "Error")!.ToString()!;
        internal byte[] Bytes(object value) => (byte[])Property(Property(value, "Value")!, "Bytes")!;
        internal void AssertTypedValues(object result)
        {
            var union = Property(Property(result, "Value")!, "Value")!;
            var record = Property(union, "Payload")!;
            object? At(object item, string method, int index) => item.GetType().GetMethod(method, HiddenInstance)!.Invoke(item, [index]);
            Assert.Equal(9007199254740993.125m, At(record, "ValueAt", 0));
            var items = At(record, "ValueAt", 2)!;
            Assert.Equal(1.25m, At(items, "At", 0));
            Assert.Null(At(items, "At", 1));
            var child = At(record, "ValueAt", 3)!;
            Assert.True((bool)At(child, "IsPresent", 0)!);
            Assert.Null(At(child, "ValueAt", 0));
            Assert.False((bool)At(record, "IsPresent", 4)!);
        }
    }
}
