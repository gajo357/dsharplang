using DSharp.Runtime.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DSharp.Runtime.Sqlite.Tests.Features;

public sealed class ActorSqliteSchemaTests
{
    private static SqliteConnection Open(string path, bool foreignKeys = true)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Pooling = false, ForeignKeys = foreignKeys
        }.ToString());
        connection.Open();
        return connection;
    }

    private static int Execute(SqliteConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return command.ExecuteNonQuery();
    }

    private static object? Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static void InDirectory(Action<string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"dsharp-actor-schema-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try { action(directory); }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void Submission(SqliteConnection db, string app, string id, string kind)
        => Execute(db, "INSERT INTO actor_submissions VALUES ($app,$id,$kind,$contract,$payload,$options)",
            ("$app", app), ("$id", id), ("$kind", kind), ("$contract", new byte[] { 0, 255, 17 }), ("$payload", Array.Empty<byte>()), ("$options", new byte[] { 255, 0 }));

    private const string Targeted = """
        INSERT INTO actor_invocations
        (application_id,invocation_id,submission_id,requested_actor_id,selected_actor_id,target_key,operation,parent_invocation_id,parent_attempt_id)
        VALUES ($app,$inv,$sub,'requested-id','selected-id',$key,$operation,$parent,$attempt)
        """;

    [Fact]
    public void SchemaIsIdempotentAndPreservesOpaqueBytesAndSelectionOnReopen()
    {
        InDirectory(directory =>
        {
            var path = Path.Combine(directory, "actor.db");
            using (var db = Open(path))
            {
                ActorSqliteSchema.Apply(db);
                ActorSqliteSchema.Apply(db);
                Assert.Equal(1L, Assert.IsType<long>(Scalar(db, "PRAGMA user_version")));
                Submission(db, "app", "one", "targeted");
                Execute(db, Targeted, ("$app", "app"), ("$inv", "inv-1"), ("$sub", "one"),
                    ("$key", Array.Empty<byte>()), ("$operation", "invoke"), ("$parent", "unverified-parent"), ("$attempt", "unverified-attempt"));
            }
            using var reopened = Open(path);
            ActorSqliteSchema.Apply(reopened);
            using var command = reopened.CreateCommand();
            command.CommandText = "SELECT s.contract,s.payload,i.requested_actor_id,i.selected_actor_id,i.target_key,i.parent_invocation_id,i.parent_attempt_id,s.options FROM actor_submissions s JOIN actor_invocations i USING(application_id,submission_id)";
            using var reader = command.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal(new byte[] { 0, 255, 17 }, (byte[])reader.GetValue(0));
            Assert.Empty((byte[])reader.GetValue(1));
            Assert.Equal("requested-id", reader.GetString(2));
            Assert.Equal("selected-id", reader.GetString(3));
            Assert.Empty((byte[])reader.GetValue(4));
            Assert.Equal("unverified-parent", reader.GetString(5));
            Assert.Equal("unverified-attempt", reader.GetString(6));
            Assert.Equal(new byte[] { 255, 0 }, (byte[])reader.GetValue(7));
            Assert.False(reader.Read());
        });
    }

    [Fact]
    public void SharedCaseSensitiveKeysAndKindsEnforceOwnership()
    {
        InDirectory(directory =>
        {
            using var db = Open(Path.Combine(directory, "actor.db"));
            ActorSqliteSchema.Apply(db);
            Submission(db, "app", "same", "targeted");
            Assert.Throws<SqliteException>(() => Submission(db, "app", "same", "publication"));
            Submission(db, "app", "Same", "publication");
            Submission(db, "App", "same", "targeted");
            Assert.Equal(3L, Assert.IsType<long>(Scalar(db, "SELECT count(*) FROM actor_submissions")));
            Assert.Throws<SqliteException>(() => Execute(db, "INSERT INTO actor_submissions VALUES ('app','bad','other',x'01',x'',x'')"));
            Assert.Throws<SqliteException>(() => Execute(db, "INSERT INTO actor_submissions VALUES ('app','empty','targeted',x'',x'',x'')"));
            Execute(db, "INSERT INTO actor_submissions VALUES ('app','empty-options','targeted',x'01',x'',x'')");
            Assert.Equal(0L, Assert.IsType<long>(Scalar(db, "SELECT length(options) FROM actor_submissions WHERE submission_id='empty-options'")));
            Assert.Throws<SqliteException>(() => Execute(db, "INSERT INTO actor_publications(application_id,publication_id,submission_id) VALUES ('app','p','same')"));
            Assert.Throws<SqliteException>(() => Execute(db, Targeted, ("$app", "app"), ("$inv", "wrong"), ("$sub", "Same"),
                ("$key", DBNull.Value), ("$operation", "invoke"), ("$parent", DBNull.Value), ("$attempt", DBNull.Value)));
        });
    }

    [Fact]
    public void AttemptsAncestryAndFrozenDeliveryRelationshipsRemainDistinct()
    {
        InDirectory(directory =>
        {
            using var db = Open(Path.Combine(directory, "actor.db"));
            ActorSqliteSchema.Apply(db);
            Submission(db, "app", "one", "targeted");
            Execute(db, Targeted, ("$app", "app"), ("$inv", "root"), ("$sub", "one"), ("$key", DBNull.Value),
                ("$operation", "invoke"), ("$parent", DBNull.Value), ("$attempt", DBNull.Value));
            Assert.Equal(1L, Assert.IsType<long>(Scalar(db, "SELECT target_key IS NULL FROM actor_invocations WHERE invocation_id='root'")));
            Execute(db, "INSERT INTO actor_attempts VALUES ('app','root','attempt-1')");
            Execute(db, "INSERT INTO actor_attempts VALUES ('app','root','attempt-2')");
            Assert.Throws<SqliteException>(() => Execute(db, "INSERT INTO actor_attempts VALUES ('app','root','attempt-1')"));
            Assert.Throws<SqliteException>(() => Execute(db, "INSERT INTO actor_attempts VALUES ('app','missing','attempt-3')"));
            Submission(db, "app", "pub", "publication");
            Execute(db, "INSERT INTO actor_publications(application_id,publication_id,submission_id) VALUES ('app','p','pub')");
            Assert.Equal(0L, Assert.IsType<long>(Scalar(db, "SELECT count(*) FROM actor_deliveries")));
            Execute(db, "INSERT INTO actor_deliveries VALUES ('app','p','sub','public-id','selected-id')");
            Execute(db, "INSERT INTO actor_deliveries VALUES ('app','p','another','public-id','selected-id')");
            Assert.Throws<SqliteException>(() => Execute(db, "INSERT INTO actor_deliveries VALUES ('app','p','sub','other','other')"));
            Assert.Throws<SqliteException>(() => Execute(db, "INSERT INTO actor_deliveries VALUES ('app','missing','sub','public-id','selected-id')"));
            Execute(db, "INSERT INTO actor_invocations(application_id,invocation_id,source_publication_id,source_subscription_id,requested_actor_id,selected_actor_id,operation) VALUES ('app','subscriber','p','sub','public-id','selected-id','invoke')");
            Assert.Throws<SqliteException>(() => Execute(db, "INSERT INTO actor_invocations(application_id,invocation_id,source_publication_id,source_subscription_id,requested_actor_id,selected_actor_id,operation) VALUES ('app','again','p','sub','public-id','selected-id','invoke')"));
            Assert.Throws<SqliteException>(() => Execute(db, "INSERT INTO actor_invocations(application_id,invocation_id,source_publication_id,source_subscription_id,requested_actor_id,selected_actor_id,operation) VALUES ('app','wrong-request','p','another','wrong','selected-id','invoke')"));
            Assert.Throws<SqliteException>(() => Execute(db, "INSERT INTO actor_invocations(application_id,invocation_id,source_publication_id,source_subscription_id,requested_actor_id,selected_actor_id,operation) VALUES ('app','wrong-selected','p','another','public-id','wrong','invoke')"));
            Assert.Throws<SqliteException>(() => Execute(db, "INSERT INTO actor_invocations(application_id,invocation_id,source_publication_id,source_subscription_id,requested_actor_id,selected_actor_id,operation,parent_invocation_id,parent_attempt_id) VALUES ('app','child','p','another','public-id','selected-id','invoke','parent','attempt')"));
            Execute(db, "INSERT INTO actor_invocations(application_id,invocation_id,source_publication_id,source_subscription_id,requested_actor_id,selected_actor_id,operation) VALUES ('app','another-root','p','another','public-id','selected-id','invoke')");
            Assert.Throws<SqliteException>(() => Execute(db, "INSERT INTO actor_invocations(application_id,invocation_id,source_publication_id,source_subscription_id,requested_actor_id,selected_actor_id,operation) VALUES ('app','orphan','p','missing','public-id','selected-id','invoke')"));
            Submission(db, "app", "two", "targeted");
            Assert.Throws<SqliteException>(() => Execute(db, Targeted, ("$app", "app"), ("$inv", "bad-parent"), ("$sub", "two"), ("$key", DBNull.Value),
                ("$operation", "invoke"), ("$parent", "parent"), ("$attempt", DBNull.Value)));
            Assert.Throws<SqliteException>(() => Execute(db, Targeted, ("$app", "app"), ("$inv", "bad-dispatch"), ("$sub", "two"), ("$key", DBNull.Value),
                ("$operation", "dispatch"), ("$parent", "parent"), ("$attempt", "attempt")));
        });
    }

    [Fact]
    public void FailedMigrationRollsBackAndNewerVersionIsRejectedWithoutMutation()
    {
        InDirectory(directory =>
        {
            using (var db = Open(Path.Combine(directory, "rollback.db")))
            {
                Assert.Throws<SqliteException>(() => ActorSqliteSchema.ApplyMigration(db,
                    ["CREATE TABLE first (id INTEGER)", "CREATE TABLE broken (invalid syntax here"]));
                Assert.Equal(0L, Assert.IsType<long>(Scalar(db, "PRAGMA user_version")));
                Assert.Equal(0L, Assert.IsType<long>(Scalar(db, "SELECT count(*) FROM sqlite_master WHERE name='first'")));
            }
            using (var db = Open(Path.Combine(directory, "newer.db")))
            {
                Execute(db, "PRAGMA user_version = 2");
                Assert.Throws<InvalidOperationException>(() => ActorSqliteSchema.Apply(db));
                Assert.Equal(2L, Assert.IsType<long>(Scalar(db, "PRAGMA user_version")));
                Assert.Equal(0L, Assert.IsType<long>(Scalar(db, "SELECT count(*) FROM sqlite_master WHERE name NOT LIKE 'sqlite_%'")));
            }
            using (var db = Open(Path.Combine(directory, "unversioned.db")))
            {
                Execute(db, "CREATE TABLE existing (id INTEGER)");
                Assert.Throws<InvalidOperationException>(() => ActorSqliteSchema.Apply(db));
                Assert.Equal(0L, Assert.IsType<long>(Scalar(db, "PRAGMA user_version")));
                Assert.Equal(1L, Assert.IsType<long>(Scalar(db, "SELECT count(*) FROM sqlite_master WHERE name='existing'")));
            }
        });
    }

    [Fact]
    public void IndependentApplicationFilesAndRequiredForeignKeySetting()
    {
        InDirectory(directory =>
        {
            var first = Path.Combine(directory, "first.db");
            var second = Path.Combine(directory, "second.db");
            using (var disabled = Open(first, false))
            {
                Assert.Throws<InvalidOperationException>(() => ActorSqliteSchema.Apply(disabled));
                Assert.Equal(0L, Assert.IsType<long>(Scalar(disabled, "PRAGMA user_version")));
            }
            using (var db = Open(first)) { ActorSqliteSchema.Apply(db); Submission(db, "app", "one", "targeted"); }
            using (var db = Open(second)) { ActorSqliteSchema.Apply(db); Assert.Equal(0L, Assert.IsType<long>(Scalar(db, "SELECT count(*) FROM actor_submissions"))); }
            using (var db = Open(first)) Assert.Equal(1L, Assert.IsType<long>(Scalar(db, "SELECT count(*) FROM actor_submissions")));
        });
    }
}
