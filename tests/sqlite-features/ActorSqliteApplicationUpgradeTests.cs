using DSharp.Runtime.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DSharp.Runtime.Sqlite.Tests.Features;

public sealed class ActorSqliteApplicationUpgradeTests
{
    private static void InDirectory(Action<string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"dsharp-upgrade-bind-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try { action(directory); }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void Execute(SqliteConnection db, string sql, string? app = null)
    {
        using var command = db.CreateCommand();
        command.CommandText = sql;
        if (app is not null) command.Parameters.AddWithValue("$app", app);
        command.ExecuteNonQuery();
    }

    private static object? Scalar(SqliteConnection db, string sql)
    {
        using var command = db.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static void Populate(SqliteConnection db, string app, bool withV2)
    {
        Execute(db, "INSERT INTO actor_submissions VALUES ($app,'sub','targeted',x'01',x'00FF',x'')", app);
        Execute(db, """
            INSERT INTO actor_invocations(application_id,invocation_id,submission_id,requested_actor_id,selected_actor_id,operation)
            VALUES ($app,'inv','sub','requested','selected','invoke')
            """, app);
        Execute(db, "INSERT INTO actor_attempts VALUES ($app,'inv','attempt')", app);
        if (withV2)
            Execute(db, """
                INSERT INTO actor_attempt_exits(application_id,invocation_id,attempt_id,outcome_kind,payload,contract,report_present)
                VALUES ($app,'inv','attempt','normal',x'1234',x'01',0)
                """, app);
    }

    [Fact]
    public void FreshFileIsInitializedAndBoundAndInvalidIdCreatesNothing()
    {
        InDirectory(directory =>
        {
            var invalid = Path.Combine(directory, "invalid.db");
            Assert.Throws<ArgumentException>(() => ActorSqliteDatabase.OpenAndBind(invalid, "bad\0id"));
            Assert.False(File.Exists(invalid));
            var path = Path.Combine(directory, "fresh.db");
            using (var db = ActorSqliteDatabase.OpenAndBind(path, "app"))
            {
                Assert.Equal(4L, Assert.IsType<long>(Scalar(db, "PRAGMA user_version")));
                Assert.Equal("app", Assert.IsType<string>(Scalar(db, "SELECT application_id FROM actor_application_binding")));
            }
            using var reopened = ActorSqliteDatabase.OpenBound(path, "app");
            Assert.Equal(4L, Assert.IsType<long>(Scalar(reopened, "PRAGMA user_version")));
            Assert.Throws<InvalidOperationException>(() => ActorSqliteDatabase.OpenBound(path, "other"));
        });
    }

    [Fact]
    public void PopulatedV1AndV2UpgradeAndBindWithoutLosingRecords()
    {
        InDirectory(directory =>
        {
            foreach (var version in new[] { ActorSqliteSchemaVersion.V1, ActorSqliteSchemaVersion.V2 })
            {
                var path = Path.Combine(directory, $"{version}.db");
                using (var db = ActorSqliteDatabase.Open(path, version)) Populate(db, "App", version == ActorSqliteSchemaVersion.V2);
                using (var bound = ActorSqliteDatabase.OpenAndBind(path, "App"))
                {
                    Assert.Equal(4L, Assert.IsType<long>(Scalar(bound, "PRAGMA user_version")));
                    Assert.Equal("00FF", Assert.IsType<string>(Scalar(bound, "SELECT hex(payload) FROM actor_submissions WHERE submission_id='sub'")));
                    Assert.Equal("selected", Assert.IsType<string>(Scalar(bound, "SELECT selected_actor_id FROM actor_invocations WHERE invocation_id='inv'")));
                    Assert.Equal(version == ActorSqliteSchemaVersion.V2 ? 1L : 0L,
                        Assert.IsType<long>(Scalar(bound, "SELECT count(*) FROM actor_attempt_exits")));
                }
                using var reopened = ActorSqliteDatabase.OpenAndBind(path, "App");
                Assert.Equal(4L, Assert.IsType<long>(Scalar(reopened, "PRAGMA user_version")));
                Assert.Throws<InvalidOperationException>(() => ActorSqliteDatabase.OpenAndBind(path, "app"));
            }
        });
    }

    [Fact]
    public void WrongOrMixedOlderIdsDoNotPerformIntermediateUpgrade()
    {
        InDirectory(directory =>
        {
            foreach (var version in new[] { ActorSqliteSchemaVersion.V1, ActorSqliteSchemaVersion.V2 })
            foreach (var mixed in new[] { false, true })
            {
                var path = Path.Combine(directory, $"{version}-{mixed}.db");
                using (var db = ActorSqliteDatabase.Open(path, version))
                {
                    Populate(db, "App-A", version == ActorSqliteSchemaVersion.V2);
                    if (mixed) Execute(db, "INSERT INTO actor_submissions VALUES ('App-B','other','targeted',x'01',x'',x'')");
                }
                Assert.Throws<InvalidOperationException>(() => ActorSqliteDatabase.OpenAndBind(path, mixed ? "App-A" : "app-a"));
                using var raw = ActorSqliteDatabase.Open(path, version);
                Assert.Equal((long)version, Assert.IsType<long>(Scalar(raw, "PRAGMA user_version")));
                Assert.Equal(0L, Assert.IsType<long>(Scalar(raw, "SELECT count(*) FROM sqlite_master WHERE name IN ('actor_invocation_lifecycle','actor_application_binding')")));
                Assert.Equal("00FF", Assert.IsType<string>(Scalar(raw, "SELECT hex(payload) FROM actor_submissions WHERE submission_id='sub'")));
            }
        });
    }

    [Fact]
    public void LaterDdlConflictRollsBackIntermediateUpgradeAndBinding()
    {
        InDirectory(directory =>
        {
            var path = Path.Combine(directory, "conflict.db");
            using (var db = ActorSqliteDatabase.Open(path, ActorSqliteSchemaVersion.V1))
            {
                Populate(db, "app", false);
                Execute(db, "CREATE TABLE actor_fault_observations(marker TEXT NOT NULL)");
                Execute(db, "INSERT INTO actor_fault_observations VALUES ('kept')");
            }
            Assert.Throws<SqliteException>(() => ActorSqliteDatabase.OpenAndBind(path, "app"));
            using var raw = ActorSqliteDatabase.Open(path, ActorSqliteSchemaVersion.V1);
            Assert.Equal(1L, Assert.IsType<long>(Scalar(raw, "PRAGMA user_version")));
            Assert.Equal(0L, Assert.IsType<long>(Scalar(raw, "SELECT count(*) FROM sqlite_master WHERE name IN ('actor_attempt_exits','actor_invocation_lifecycle','actor_application_binding')")));
            Assert.Equal("kept", Assert.IsType<string>(Scalar(raw, "SELECT marker FROM actor_fault_observations")));
            Assert.Equal("00FF", Assert.IsType<string>(Scalar(raw, "SELECT hex(payload) FROM actor_submissions")));
        });
    }

    [Fact]
    public void UnversionedNonemptyNewerAndCorruptFilesFailWithoutMutation()
    {
        InDirectory(directory =>
        {
            foreach (var kind in new[] { "unversioned", "newer", "corrupt" })
            {
                var path = Path.Combine(directory, $"{kind}.db");
                if (kind == "corrupt")
                {
                    using var db = ActorSqliteDatabase.Open(path, ActorSqliteSchemaVersion.V1);
                    Execute(db, "PRAGMA foreign_keys=OFF");
                    Execute(db, "INSERT INTO actor_attempts VALUES ('app','missing','attempt')");
                }
                else
                {
                    using var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
                    db.Open();
                    Execute(db, "CREATE TABLE marker(value TEXT NOT NULL)");
                    Execute(db, "INSERT INTO marker VALUES ('kept')");
                    if (kind == "newer") Execute(db, "PRAGMA user_version=5");
                }
                Assert.Throws<InvalidOperationException>(() => ActorSqliteDatabase.OpenAndBind(path, "app"));
                using var raw = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
                raw.Open();
                Assert.Equal(kind == "newer" ? 5L : kind == "corrupt" ? 1L : 0L,
                    Assert.IsType<long>(Scalar(raw, "PRAGMA user_version")));
                Assert.Equal(0L, Assert.IsType<long>(Scalar(raw, "SELECT count(*) FROM sqlite_master WHERE name='actor_application_binding'")));
                if (kind != "corrupt") Assert.Equal("kept", Assert.IsType<string>(Scalar(raw, "SELECT value FROM marker")));
            }
        });
    }

    [Fact]
    public void CompetingOlderFileUpgradesBindOnlyOneApplication()
    {
        InDirectory(directory =>
        {
            foreach (var same in new[] { false, true })
            {
                var path = Path.Combine(directory, same ? "same.db" : "different.db");
                using (var db = ActorSqliteDatabase.Open(path, ActorSqliteSchemaVersion.V1)) { }
                using var start = new Barrier(2);
                Task<(string App, Exception? Error)> Run(string app) => Task.Run(() =>
                {
                    if (!start.SignalAndWait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Binders did not reach the barrier.");
                    try { using var db = ActorSqliteDatabase.OpenAndBind(path, app); return (app, (Exception?)null); }
                    catch (Exception error) { return (app, error); }
                });
                var results = Task.WhenAll(Run("App-A"), Run(same ? "App-A" : "App-B"))
                    .WaitAsync(TimeSpan.FromSeconds(20)).GetAwaiter().GetResult();
                if (same)
                {
                    Assert.All(results, result => Assert.Null(result.Error));
                    using var bound = ActorSqliteDatabase.OpenBound(path, "App-A");
                    Assert.Equal(4L, Assert.IsType<long>(Scalar(bound, "PRAGMA user_version")));
                }
                else
                {
                    var winner = Assert.Single(results, result => result.Error is null);
                    var loser = Assert.Single(results, result => result.Error is not null);
                    Assert.IsType<InvalidOperationException>(loser.Error);
                    using var bound = ActorSqliteDatabase.OpenBound(path, winner.App);
                    Assert.Throws<InvalidOperationException>(() => ActorSqliteDatabase.OpenBound(path, loser.App));
                    Assert.Equal(4L, Assert.IsType<long>(Scalar(bound, "PRAGMA user_version")));
                }
            }
        });
    }

    [Fact]
    public void CompletedTransactionCannotBeReusedForMigration()
    {
        InDirectory(directory =>
        {
            var path = Path.Combine(directory, "transaction.db");
            using var db = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path, Pooling = false, ForeignKeys = true
            }.ToString());
            db.Open();
            using var transaction = db.BeginTransaction();
            transaction.Commit();
            Assert.Throws<InvalidOperationException>(() =>
                ActorSqliteSchema.ApplyWithinTransaction(db, transaction, ActorSqliteSchemaVersion.V3));
            Assert.Equal(0L, Assert.IsType<long>(Scalar(db, "PRAGMA user_version")));
            Assert.Equal(0L, Assert.IsType<long>(Scalar(db, "SELECT count(*) FROM sqlite_master WHERE name NOT LIKE 'sqlite_%'")));
        });
    }
}
