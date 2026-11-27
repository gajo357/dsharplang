using System.Diagnostics;
using System.Reflection;
using System.Text;
using DSharp.Runtime.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DSharp.Runtime.Sqlite.Tests.Features;

public sealed class JsonContractSqliteRestartTests
{
    private const string WriterDirectory = "DSHARP_CONTRACT_SQLITE_WRITER_DIRECTORY";
    private const string App = "contract-json-restart";
    private static readonly byte[] Identity = Encoding.UTF8.GetBytes("test-message-v1");
    private static readonly byte[] Payload = Encoding.UTF8.GetBytes("{\"name\":\"example\",\"amount\":9007199254740993.125}");

    [Fact]
    public void StoredIdentityAndValidatedRecordSurviveWriterExit()
    {
        var childDirectory = Environment.GetEnvironmentVariable(WriterDirectory);
        if (childDirectory is not null)
        {
            Write(Path.Combine(childDirectory, "actor.db"));
            File.WriteAllText(Path.Combine(childDirectory, "complete"), "ok");
            return;
        }
        var directory = Path.Combine(Path.GetTempPath(), $"dsharp-contract-sqlite-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            RunWriter(directory);
            var path = Path.Combine(directory, "actor.db"); var bridge = new Bridge();
            var good = Assert.IsType<ActorSqlitePinnedWork>(ActorSqlitePinnedWorkReader.Read(path, App, "good-inv"));
            Assert.Equal(Identity, good.Contract); Assert.Equal(Payload, good.Payload);
            Assert.True(bridge.Success(bridge.Decode(good.Contract, good.Payload)));
            var wrong = Assert.IsType<ActorSqlitePinnedWork>(ActorSqlitePinnedWorkReader.Read(path, App, "wrong-inv"));
            var mismatch = bridge.Decode(wrong.Contract, wrong.Payload);
            Assert.False(bridge.Success(mismatch)); Assert.Contains("ContractMismatch", bridge.Error(mismatch), StringComparison.Ordinal);
            Assert.DoesNotContain("secret", bridge.Error(mismatch), StringComparison.Ordinal);
            var corrupt = Assert.IsType<ActorSqlitePinnedWork>(ActorSqlitePinnedWorkReader.Read(path, App, "corrupt-inv"));
            var rejected = bridge.Decode(corrupt.Contract, corrupt.Payload);
            Assert.False(bridge.Success(rejected)); Assert.Contains("TypeMismatch", bridge.Error(rejected), StringComparison.Ordinal);
            Assert.DoesNotContain("secret", bridge.Error(rejected), StringComparison.Ordinal);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void RunWriter(string directory)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("vstest"); start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        start.ArgumentList.Add("--TestCaseFilter:FullyQualifiedName=DSharp.Runtime.Sqlite.Tests.Features.JsonContractSqliteRestartTests.StoredIdentityAndValidatedRecordSurviveWriterExit");
        start.Environment[WriterDirectory] = directory;
        using var child = Process.Start(start) ?? throw new InvalidOperationException("Could not start writer.");
        var output = child.StandardOutput.ReadToEndAsync(); var error = child.StandardError.ReadToEndAsync();
        if (!child.WaitForExit(60000)) { child.Kill(entireProcessTree: true); child.WaitForExit(); throw new TimeoutException("Writer timed out."); }
        Assert.True(child.ExitCode == 0, output.GetAwaiter().GetResult() + error.GetAwaiter().GetResult());
        Assert.Equal("ok", File.ReadAllText(Path.Combine(directory, "complete")));
    }

    // The fixture supplies metadata and inserts rows directly; it does not claim
    // durable admission, schema evolution or an adopted application wire format.
    private static void Write(string path)
    {
        var bridge = new Bridge(); Assert.True(bridge.Success(bridge.Decode(Identity, Payload)));
        using var db = ActorSqliteDatabase.OpenAndBind(path, App); using var tx = db.BeginTransaction();
        Insert(db, tx, "good", Identity, Payload);
        Insert(db, tx, "wrong", Encoding.UTF8.GetBytes("secret-other-version"), Payload);
        Insert(db, tx, "corrupt", Identity, Encoding.UTF8.GetBytes("{\"name\":\"secret-value\",\"amount\":\"secret\"}"));
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
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly object contract;
        private readonly MethodInfo decode;
        internal Bridge()
        {
            var runtime = Assembly.Load("DSharp.Runtime");
            Type Find(string name) => runtime.GetType($"DSharp.Runtime.{name}", throwOnError: true)!;
            object New(Type type, params object[] args) => Activator.CreateInstance(type, Hidden, binder: null, args: args, culture: null)!;
            object Field(string name, string kind) => Activator.CreateInstance(Find("JsonRecordField"), name, Enum.Parse(Find("JsonRecordFieldKind"), kind), false, null, null)!;
            var fields = Array.CreateInstance(Find("JsonRecordField"), 2);
            fields.SetValue(Field("name", "String"), 0); fields.SetValue(Field("amount", "Decimal"), 1);
            var schema = New(Find("JsonScalarRecordSchema"), fields); var identity = New(Find("JsonContractIdentity"), Identity);
            contract = New(Find("JsonRecordContract"), identity, schema);
            decode = contract.GetType().GetMethod("Decode", Hidden)!;
        }
        internal object Decode(byte[] identity, byte[] payload) => decode.Invoke(contract, [new ReadOnlyMemory<byte>(identity), new ReadOnlyMemory<byte>(payload)])!;
        private static object? Property(object value, string name) => value.GetType().GetProperty(name, Hidden)!.GetValue(value);
        internal bool Success(object result) => (bool)Property(result, "IsSuccess")!;
        internal string Error(object result) => Property(result, "Error")!.ToString()!;
    }
}
