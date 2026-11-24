using System.Data;
using DSharp.Runtime.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DSharp.Runtime.Sqlite.Tests.Features;

public sealed class ActorSqliteWriteBatchTests
{
    private static void InDirectory(Action<string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"dsharp-write-batch-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try { action(directory); }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static object? Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static void ExecuteSql(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static ActorSqliteWriteParameter P(string name, object? value) => new(name, value);

    private static ActorSqliteWriteCommand C(string sql, int? expected, params ActorSqliteWriteParameter[] parameters)
        => new(sql, parameters, expected);

    private static ActorSqliteWriteBatch Batch(params ActorSqliteWriteCommand[] commands) => new(commands);

    private const string InsertSubmission = """
        INSERT INTO actor_submissions (application_id,submission_id,kind,contract,payload,options)
        VALUES ($app,$submission,'targeted',$contract,$payload,$options)
        """;

    private static ActorSqliteWriteCommand Submission(string app, string submission, byte[] payload)
        => C(InsertSubmission, 1, P("$app", app), P("$submission", submission), P("$contract", new byte[] { 1 }),
            P("$payload", payload), P("$options", Array.Empty<byte>()));

    [Fact]
    public void ParameterizedLinkedWritesCommitAndReopenWithoutCrossingFiles()
    {
        InDirectory(directory =>
        {
            var first = Path.Combine(directory, "first.db");
            var second = Path.Combine(directory, "second.db");
            const string requested = "actor'); DROP TABLE actor_invocations; --";
            using (var db = ActorSqliteDatabase.Open(first, ActorSqliteSchemaVersion.V3))
            {
                var batch = Batch(
                    Submission("app", "one", new byte[] { 0, 255, 17 }),
                    C("""
                      INSERT INTO actor_invocations
                        (application_id,invocation_id,submission_id,requested_actor_id,selected_actor_id,target_key,operation)
                      VALUES ($app,$inv,$sub,$requested,$selected,$key,'invoke')
                      """, 1, P("$app", "app"), P("$inv", "inv-one"), P("$sub", "one"), P("$requested", requested),
                        P("$selected", "selected-stable"), P("$key", null)),
                    C("INSERT INTO actor_attempts VALUES ($app,$inv,$attempt)", 1,
                        P("$app", "app"), P("$inv", "inv-one"), P("$attempt", "attempt-one")),
                    Submission("app", "two", Array.Empty<byte>()),
                    C("""
                      INSERT INTO actor_invocations
                        (application_id,invocation_id,submission_id,requested_actor_id,selected_actor_id,target_key,operation)
                      VALUES ($app,$inv,$sub,$requested,$selected,$key,'invoke')
                      """, 1, P("$app", "app"), P("$inv", "inv-two"), P("$sub", "two"), P("$requested", "requested"),
                        P("$selected", "selected-stable"), P("$key", Array.Empty<byte>())));
                Assert.Equal(new[] { 1, 1, 1, 1, 1 }, batch.Execute(db));
                Assert.Equal(ConnectionState.Open, db.State);
            }
            using (var other = ActorSqliteDatabase.Open(second, ActorSqliteSchemaVersion.V3))
                Assert.Equal(0L, Assert.IsType<long>(Scalar(other, "SELECT count(*) FROM actor_submissions")));
            using var reopened = ActorSqliteDatabase.Open(first, ActorSqliteSchemaVersion.V3);
            using var command = reopened.CreateCommand();
            command.CommandText = """
                SELECT s.payload,i.requested_actor_id,i.selected_actor_id,i.target_key
                FROM actor_submissions s JOIN actor_invocations i USING(application_id,submission_id)
                WHERE s.submission_id='one'
                """;
            using (var reader = command.ExecuteReader())
            {
                Assert.True(reader.Read());
                Assert.Equal(new byte[] { 0, 255, 17 }, Assert.IsType<byte[]>(reader.GetValue(0)));
                Assert.Equal(requested, reader.GetString(1));
                Assert.Equal("selected-stable", reader.GetString(2));
                Assert.True(reader.IsDBNull(3));
                Assert.False(reader.Read());
            }
            Assert.Equal(0L, Assert.IsType<long>(Scalar(reopened, "SELECT length(target_key) FROM actor_invocations WHERE invocation_id='inv-two'")));
            Assert.Equal(1L, Assert.IsType<long>(Scalar(reopened, "SELECT count(*) FROM actor_attempts WHERE attempt_id='attempt-one'")));
        });
    }

    [Fact]
    public void LaterConstraintFailureRollsBackEarlierWritesAndKeepsCallerConnectionUsable()
    {
        InDirectory(directory =>
        {
            using var db = ActorSqliteDatabase.Open(Path.Combine(directory, "actor.db"), ActorSqliteSchemaVersion.V3);
            var batch = Batch(Submission("app", "same", new byte[] { 1 }), Submission("app", "same", new byte[] { 2 }));
            Assert.Throws<SqliteException>(() => batch.Execute(db));
            Assert.Equal(ConnectionState.Open, db.State);
            Assert.Equal(0L, Assert.IsType<long>(Scalar(db, "SELECT count(*) FROM actor_submissions")));
            Assert.Equal(new[] { 1 }, Batch(Submission("app", "after", new byte[] { 3 })).Execute(db));
            Assert.Equal(1L, Assert.IsType<long>(Scalar(db, "SELECT count(*) FROM actor_submissions")));
        });
    }

    [Fact]
    public void AffectedRowMismatchRollsBackEarlierWrites()
    {
        InDirectory(directory =>
        {
            using var db = ActorSqliteDatabase.Open(Path.Combine(directory, "actor.db"), ActorSqliteSchemaVersion.V3);
            var batch = Batch(Submission("app", "before", new byte[] { 1 }),
                C("UPDATE actor_submissions SET payload=$payload WHERE submission_id=$missing", 1,
                    P("$payload", new byte[] { 2 }), P("$missing", "not-present")));
            Assert.Throws<InvalidOperationException>(() => batch.Execute(db));
            Assert.Equal(0L, Assert.IsType<long>(Scalar(db, "SELECT count(*) FROM actor_submissions")));
            Assert.Equal(ConnectionState.Open, db.State);
        });
    }

    [Fact]
    public void DeferredForeignKeyCommitFailureRollsBackAndLeavesConnectionUsable()
    {
        InDirectory(directory =>
        {
            using var db = ActorSqliteDatabase.Open(Path.Combine(directory, "actor.db"), ActorSqliteSchemaVersion.V3);
            ExecuteSql(db, "CREATE TABLE test_parent (id INTEGER NOT NULL PRIMARY KEY)");
            ExecuteSql(db, "CREATE TABLE test_child (id INTEGER NOT NULL PRIMARY KEY, parent_id INTEGER NOT NULL REFERENCES test_parent(id) DEFERRABLE INITIALLY DEFERRED)");
            var batch = Batch(C("INSERT INTO test_child VALUES ($id,$parent)", 1, P("$id", 1), P("$parent", 99L)));
            Assert.Throws<SqliteException>(() => batch.Execute(db));
            Assert.Equal(ConnectionState.Open, db.State);
            Assert.Equal(0L, Assert.IsType<long>(Scalar(db, "SELECT count(*) FROM test_child")));
            Assert.Equal(new[] { 1, 1 }, Batch(
                C("INSERT INTO test_parent VALUES ($id)", 1, P("$id", 99)),
                C("INSERT INTO test_child VALUES ($id,$parent)", 1, P("$id", 2), P("$parent", 99))).Execute(db));
            Assert.Equal(1L, Assert.IsType<long>(Scalar(db, "SELECT count(*) FROM test_child")));
        });
    }

    [Fact]
    public void BatchSnapshotsListsAndBlobsBeforeExecution()
    {
        InDirectory(directory =>
        {
            using var db = ActorSqliteDatabase.Open(Path.Combine(directory, "actor.db"), ActorSqliteSchemaVersion.V3);
            var payload = new byte[] { 0, 42, 255 };
            var parameters = new List<ActorSqliteWriteParameter>
            {
                P("$app", "app"), P("$submission", "snapshot"), P("$contract", new byte[] { 1 }),
                P("$payload", payload), P("$options", Array.Empty<byte>())
            };
            var command = new ActorSqliteWriteCommand(InsertSubmission, parameters, 1);
            var commands = new List<ActorSqliteWriteCommand> { command };
            var batch = new ActorSqliteWriteBatch(commands);
            payload[1] = 99;
            parameters.Clear();
            commands.Clear();
            Assert.Equal(new[] { 1 }, batch.Execute(db));
            Assert.Equal("002AFF", Assert.IsType<string>(Scalar(db, "SELECT hex(payload) FROM actor_submissions WHERE submission_id='snapshot'")));
        });
    }

    [Fact]
    public void CallerEnumerablesAreConsumedBeforeExecutionOnlyOnce()
    {
        InDirectory(directory =>
        {
            using var db = ActorSqliteDatabase.Open(Path.Combine(directory, "actor.db"), ActorSqliteSchemaVersion.V3);
            var parameterEnumerations = 0;
            var commandEnumerations = 0;
            IEnumerable<ActorSqliteWriteParameter> Parameters()
            {
                Assert.Equal(1, ++parameterEnumerations);
                yield return P("$app", "app");
                yield return P("$submission", "enumerated");
                yield return P("$contract", new byte[] { 1 });
                yield return P("$payload", Array.Empty<byte>());
                yield return P("$options", Array.Empty<byte>());
            }
            var command = new ActorSqliteWriteCommand(InsertSubmission, Parameters(), 1);
            IEnumerable<ActorSqliteWriteCommand> Commands()
            {
                Assert.Equal(1, ++commandEnumerations);
                yield return command;
            }
            var batch = new ActorSqliteWriteBatch(Commands());
            Assert.Equal(1, parameterEnumerations);
            Assert.Equal(1, commandEnumerations);
            Assert.Equal(new[] { 1 }, batch.Execute(db));
            Assert.Equal(1, parameterEnumerations);
            Assert.Equal(1, commandEnumerations);
        });
    }

    [Fact]
    public void RejectsInvalidInputsBeforeExecution()
    {
        Assert.ThrowsAny<ArgumentException>(() => P("name", 1));
        Assert.ThrowsAny<ArgumentException>(() => P("$invalid-name", 1));
        Assert.ThrowsAny<ArgumentException>(() => P("$value", new object()));
        Assert.ThrowsAny<ArgumentException>(() => C(" ", 1));
        Assert.ThrowsAny<ArgumentException>(() => C("SELECT 1", -1));
        Assert.ThrowsAny<ArgumentException>(() => C("INSERT INTO x VALUES ($x)", 1, P("$x", 1), P("$x", 2)));
        Assert.ThrowsAny<ArgumentException>(() => new ActorSqliteWriteBatch(Array.Empty<ActorSqliteWriteCommand>()));
    }

    [Fact]
    public void RejectsDisabledForeignKeysAndDoesNotDisturbCallerTransaction()
    {
        InDirectory(directory =>
        {
            var unsafePath = Path.Combine(directory, "foreign-keys-off.db");
            using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
                { DataSource = unsafePath, Pooling = false, ForeignKeys = false }.ToString()))
            {
                db.Open();
                ExecuteSql(db, "CREATE TABLE probe (id INTEGER NOT NULL)");
                Assert.Throws<InvalidOperationException>(() => Batch(C("INSERT INTO probe VALUES (1)", 1)).Execute(db));
                Assert.Equal(0L, Assert.IsType<long>(Scalar(db, "SELECT count(*) FROM probe")));
                Assert.Equal(ConnectionState.Open, db.State);
            }
            using var configured = ActorSqliteDatabase.Open(Path.Combine(directory, "configured.db"), ActorSqliteSchemaVersion.V3);
            using var callerTransaction = configured.BeginTransaction();
            Assert.Throws<InvalidOperationException>(() => Batch(Submission("app", "inner", new byte[] { 1 })).Execute(configured));
            using (var command = configured.CreateCommand())
            {
                command.Transaction = callerTransaction;
                command.CommandText = "INSERT INTO actor_submissions VALUES ('app','outer','targeted',x'01',x'',x'')";
                Assert.Equal(1, command.ExecuteNonQuery());
            }
            callerTransaction.Commit();
            Assert.Equal(1L, Assert.IsType<long>(Scalar(configured, "SELECT count(*) FROM actor_submissions WHERE submission_id='outer'")));
            Assert.Equal(0L, Assert.IsType<long>(Scalar(configured, "SELECT count(*) FROM actor_submissions WHERE submission_id='inner'")));
        });
    }
}
