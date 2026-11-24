using DSharp.Runtime.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DSharp.Runtime.Sqlite.Tests.Features;

public sealed class ActorSqliteApplicationBindingTests
{
    private static void InDirectory(Action<string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"dsharp-bound-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try { action(directory); }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static object? Scalar(SqliteConnection connection, string sql, string? app = null)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (app is not null) command.Parameters.AddWithValue("$app", app);
        return command.ExecuteScalar();
    }

    private static void Execute(SqliteConnection connection, string sql, string? app = null)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (app is not null) command.Parameters.AddWithValue("$app", app);
        command.ExecuteNonQuery();
    }

    private static SqliteConnection Raw(string path, bool foreignKeys = true)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadWrite, Pooling = false,
            ForeignKeys = foreignKeys, DefaultTimeout = 5
        }.ToString());
        connection.Open();
        return connection;
    }

    private static void PopulateAll(SqliteConnection db, string app)
    {
        Execute(db, "INSERT INTO actor_submissions VALUES ($app,'sub','targeted',x'01',x'0011',x'')", app);
        Execute(db, "INSERT INTO actor_submissions VALUES ($app,'pubsub','publication',x'01',x'',x'')", app);
        Execute(db, """
            INSERT INTO actor_invocations (application_id,invocation_id,submission_id,requested_actor_id,selected_actor_id,operation)
            VALUES ($app,'inv','sub','requested','selected','invoke')
            """, app);
        Execute(db, "INSERT INTO actor_attempts VALUES ($app,'inv','attempt')", app);
        Execute(db, """
            INSERT INTO actor_publications(application_id,publication_id,submission_id) VALUES ($app,'pub','pubsub')
            """, app);
        Execute(db, "INSERT INTO actor_deliveries VALUES ($app,'pub','subscriber','requested','selected')", app);
        Execute(db, """
            INSERT INTO actor_attempt_exits(application_id,invocation_id,attempt_id,outcome_kind,payload,contract,report_present)
            VALUES ($app,'inv','attempt','reported_fault',x'22',x'01',1)
            """, app);
        Execute(db, """
            INSERT INTO actor_fault_observations(application_id,invocation_id,attempt_id,ordinal,classification,diagnostic,metadata)
            VALUES ($app,'inv','attempt',0,'cleanup',x'',x'')
            """, app);
        Execute(db, """
            INSERT INTO actor_invocation_lifecycle(application_id,invocation_id,state,eligibility_version,cancellation_requested)
            VALUES ($app,'inv','eligible',1,0)
            """, app);
        Execute(db, """
            INSERT INTO actor_attempt_recoveries VALUES ($app,'inv',0,'attempt','token',100,200,'reported_fault')
            """, app);
    }

    [Fact]
    public void PopulatedV3BindsAtomicallyAndReopensWithExactApplication()
    {
        InDirectory(directory =>
        {
            var path = Path.Combine(directory, "actor;safe.db");
            const string app = "App'); DROP TABLE actor_submissions; --";
            using (var db = ActorSqliteDatabase.Open(path, ActorSqliteSchemaVersion.V3))
            {
                PopulateAll(db, app);
                ActorSqliteApplicationBinding.BindVersion3(db, app);
                ActorSqliteApplicationBinding.BindVersion3(db, app);
                Assert.Equal(4L, Assert.IsType<long>(Scalar(db, "PRAGMA user_version")));
            }
            Assert.Throws<InvalidOperationException>(() => ActorSqliteDatabase.Open(path, ActorSqliteSchemaVersion.V3));
            Assert.Throws<InvalidOperationException>(() => ActorSqliteDatabase.OpenBound(path, app.ToLowerInvariant()));
            using var reopened = ActorSqliteDatabase.OpenBound(path, app);
            Assert.Equal(1L, Assert.IsType<long>(Scalar(reopened, "PRAGMA foreign_keys")));
            Assert.Equal(2L, Assert.IsType<long>(Scalar(reopened, "PRAGMA synchronous")));
            Assert.Equal(5000L, Assert.IsType<long>(Scalar(reopened, "PRAGMA busy_timeout")));
            Assert.Equal("delete", Assert.IsType<string>(Scalar(reopened, "PRAGMA journal_mode")), ignoreCase: true);
            Assert.Equal(1L, Assert.IsType<long>(Scalar(reopened, "SELECT count(*) FROM actor_application_binding WHERE application_id=$app", app)));
            Assert.Equal("0011", Assert.IsType<string>(Scalar(reopened, "SELECT hex(payload) FROM actor_submissions WHERE submission_id='sub'")));
            Assert.Equal("selected", Assert.IsType<string>(Scalar(reopened, "SELECT selected_actor_id FROM actor_invocations WHERE invocation_id='inv'")));
            Assert.Equal(1L, Assert.IsType<long>(Scalar(reopened, "SELECT count(*) FROM actor_fault_observations")));
            Assert.Equal(1L, Assert.IsType<long>(Scalar(reopened, "SELECT count(*) FROM actor_attempt_recoveries")));
        });
    }

    [Fact]
    public void WrongOrMixedStoredApplicationsAreRejectedWithoutChangingV3()
    {
        InDirectory(directory =>
        {
            foreach (var mixed in new[] { false, true })
            {
                var path = Path.Combine(directory, mixed ? "mixed.db" : "wrong.db");
                using var db = ActorSqliteDatabase.Open(path, ActorSqliteSchemaVersion.V3);
                PopulateAll(db, "App-A");
                if (mixed) Execute(db, "INSERT INTO actor_submissions VALUES ('App-B','another','targeted',x'01',x'',x'')");
                Assert.Throws<InvalidOperationException>(() => ActorSqliteApplicationBinding.BindVersion3(db, mixed ? "App-A" : "app-a"));
                Assert.Equal(3L, Assert.IsType<long>(Scalar(db, "PRAGMA user_version")));
                Assert.Equal(0L, Assert.IsType<long>(Scalar(db, "SELECT count(*) FROM sqlite_master WHERE name='actor_application_binding'")));
                Assert.Equal("0011", Assert.IsType<string>(Scalar(db, "SELECT hex(payload) FROM actor_submissions WHERE submission_id='sub'")));
            }
        });
    }

    [Fact]
    public void EmptyFileCanBindAndMissingOrInvalidInputsDoNotCreateAFile()
    {
        InDirectory(directory =>
        {
            var missing = Path.Combine(directory, "missing.db");
            Assert.Throws<SqliteException>(() => ActorSqliteDatabase.OpenBound(missing, "app"));
            Assert.False(File.Exists(missing));
            Assert.Throws<ArgumentException>(() => ActorSqliteDatabase.OpenBound(missing, "bad\0id"));
            Assert.Throws<ArgumentException>(() => ActorSqliteDatabase.OpenBound(missing, "bad\ud800"));
            Assert.False(File.Exists(missing));
            var path = Path.Combine(directory, "empty.db");
            using (var db = ActorSqliteDatabase.Open(path, ActorSqliteSchemaVersion.V3))
                ActorSqliteApplicationBinding.BindVersion3(db, "😀-app");
            using var reopened = ActorSqliteDatabase.OpenBound(path, "😀-app");
            Assert.Equal(4L, Assert.IsType<long>(Scalar(reopened, "PRAGMA user_version")));
        });
    }

    [Fact]
    public void GuardsRejectOtherApplicationsEvenWithoutForeignKeysAndBindingCannotBeReplaced()
    {
        InDirectory(directory =>
        {
            var path = Path.Combine(directory, "guard.db");
            using (var db = ActorSqliteDatabase.Open(path, ActorSqliteSchemaVersion.V3))
            {
                PopulateAll(db, "app");
                ActorSqliteApplicationBinding.BindVersion3(db, "app");
            }
            using var raw = Raw(path, foreignKeys: false);
            Assert.Equal(0L, Assert.IsType<long>(Scalar(raw, "PRAGMA foreign_keys")));
            var wrongInserts = new[]
            {
                "INSERT INTO actor_submissions VALUES ('other','s','targeted',x'01',x'',x'')",
                "INSERT INTO actor_invocations(application_id,invocation_id,submission_id,requested_actor_id,selected_actor_id,operation) VALUES ('other','i','s','r','s','invoke')",
                "INSERT INTO actor_attempts VALUES ('other','i','a')",
                "INSERT INTO actor_publications(application_id,publication_id,submission_id) VALUES ('other','p','s')",
                "INSERT INTO actor_deliveries VALUES ('other','p','s','r','i')",
                "INSERT INTO actor_attempt_exits(application_id,invocation_id,attempt_id,outcome_kind,payload,contract,report_present) VALUES ('other','i','a','normal',x'',x'01',0)",
                "INSERT INTO actor_fault_observations(application_id,invocation_id,attempt_id,ordinal,classification,diagnostic,metadata) VALUES ('other','i','a',0,'c',x'',x'')",
                "INSERT INTO actor_invocation_lifecycle(application_id,invocation_id,state,eligibility_version,cancellation_requested) VALUES ('other','i','eligible',0,0)",
                "INSERT INTO actor_attempt_recoveries VALUES ('other','i',0,'a','t',1,2,'lease_expired')"
            };
            foreach (var sql in wrongInserts)
                Assert.Contains("application binding mismatch", Assert.Throws<SqliteException>(() => Execute(raw, sql)).Message);
            var tables = new[] { "actor_submissions", "actor_invocations", "actor_attempts", "actor_publications", "actor_deliveries", "actor_attempt_exits", "actor_fault_observations", "actor_invocation_lifecycle", "actor_attempt_recoveries" };
            foreach (var table in tables)
                Assert.Contains("application binding mismatch", Assert.Throws<SqliteException>(() => Execute(raw, $"UPDATE {table} SET application_id='other'")).Message);
            Assert.Throws<SqliteException>(() => Execute(raw, "INSERT OR REPLACE INTO actor_application_binding VALUES (1,'other')"));
            Assert.Throws<SqliteException>(() => Execute(raw, "UPDATE actor_application_binding SET application_id='other'"));
            Assert.Throws<SqliteException>(() => Execute(raw, "DELETE FROM actor_application_binding"));
            Assert.Equal("app", Assert.IsType<string>(Scalar(raw, "SELECT application_id FROM actor_application_binding")));
        });
    }

    [Fact]
    public void ExistingForeignKeyViolationAndDdlConflictRollbackAllChanges()
    {
        InDirectory(directory =>
        {
            var corruptPath = Path.Combine(directory, "corrupt.db");
            using (var db = ActorSqliteDatabase.Open(corruptPath, ActorSqliteSchemaVersion.V3))
            {
                Execute(db, "PRAGMA foreign_keys=OFF");
                Execute(db, "INSERT INTO actor_attempts VALUES ('app','missing','attempt')");
                Execute(db, "PRAGMA foreign_keys=ON");
                Assert.Throws<InvalidOperationException>(() => ActorSqliteApplicationBinding.BindVersion3(db, "app"));
                Assert.Equal(3L, Assert.IsType<long>(Scalar(db, "PRAGMA user_version")));
                Assert.Equal(0L, Assert.IsType<long>(Scalar(db, "SELECT count(*) FROM sqlite_master WHERE name='actor_application_binding'")));
            }
            var conflictPath = Path.Combine(directory, "conflict.db");
            using var conflict = ActorSqliteDatabase.Open(conflictPath, ActorSqliteSchemaVersion.V3);
            PopulateAll(conflict, "app");
            Execute(conflict, "CREATE TRIGGER actor_attempts_application_guard_insert AFTER INSERT ON actor_attempts BEGIN SELECT 1; END");
            Assert.Throws<SqliteException>(() => ActorSqliteApplicationBinding.BindVersion3(conflict, "app"));
            Assert.Equal(3L, Assert.IsType<long>(Scalar(conflict, "PRAGMA user_version")));
            Assert.Equal(0L, Assert.IsType<long>(Scalar(conflict, "SELECT count(*) FROM sqlite_master WHERE name IN ('actor_application_binding','actor_submissions_application_guard_insert')")));
            Assert.Equal(1L, Assert.IsType<long>(Scalar(conflict, "SELECT count(*) FROM sqlite_master WHERE name='actor_attempts_application_guard_insert'")));
            Assert.Equal("0011", Assert.IsType<string>(Scalar(conflict, "SELECT hex(payload) FROM actor_submissions WHERE submission_id='sub'")));
        });
    }

    [Fact]
    public void MissingGuardFailsClosedOnReopenAndRebind()
    {
        InDirectory(directory =>
        {
            var path = Path.Combine(directory, "missing-guard.db");
            using (var db = ActorSqliteDatabase.Open(path, ActorSqliteSchemaVersion.V3))
                ActorSqliteApplicationBinding.BindVersion3(db, "app");
            using (var raw = Raw(path))
                Execute(raw, "DROP TRIGGER actor_attempts_application_guard_insert");
            Assert.Throws<InvalidOperationException>(() => ActorSqliteDatabase.OpenBound(path, "app"));
            using var reopened = Raw(path);
            Assert.Throws<InvalidOperationException>(() => ActorSqliteApplicationBinding.BindVersion3(reopened, "app"));
            Assert.Equal(4L, Assert.IsType<long>(Scalar(reopened, "PRAGMA user_version")));
            Assert.Equal("app", Assert.IsType<string>(Scalar(reopened, "SELECT application_id FROM actor_application_binding")));
        });
    }

    [Fact]
    public void OtherSchemaVersionsAreRejectedWithoutMutation()
    {
        InDirectory(directory =>
        {
            foreach (var version in new[] { ActorSqliteSchemaVersion.V1, ActorSqliteSchemaVersion.V2 })
            {
                var path = Path.Combine(directory, $"{version}.db");
                using var db = ActorSqliteDatabase.Open(path, version);
                Assert.Throws<InvalidOperationException>(() => ActorSqliteApplicationBinding.BindVersion3(db, "app"));
                Assert.Equal((long)version, Assert.IsType<long>(Scalar(db, "PRAGMA user_version")));
                Assert.Equal(0L, Assert.IsType<long>(Scalar(db, "SELECT count(*) FROM sqlite_master WHERE name='actor_application_binding'")));
            }
            var newerPath = Path.Combine(directory, "newer.db");
            using (var db = ActorSqliteDatabase.Open(newerPath, ActorSqliteSchemaVersion.V3))
            {
                Execute(db, "PRAGMA user_version=5");
                Assert.Throws<InvalidOperationException>(() => ActorSqliteApplicationBinding.BindVersion3(db, "app"));
                Assert.Equal(5L, Assert.IsType<long>(Scalar(db, "PRAGMA user_version")));
            }
            Assert.Throws<InvalidOperationException>(() => ActorSqliteDatabase.OpenBound(newerPath, "app"));
        });
    }

    [Fact]
    public void ExistingCallerTransactionRemainsUnderCallerControl()
    {
        InDirectory(directory =>
        {
            using var db = ActorSqliteDatabase.Open(Path.Combine(directory, "caller.db"), ActorSqliteSchemaVersion.V3);
            using var caller = db.BeginTransaction();
            Assert.Throws<InvalidOperationException>(() => ActorSqliteApplicationBinding.BindVersion3(db, "app"));
            using (var command = db.CreateCommand())
            {
                command.Transaction = caller;
                command.CommandText = "INSERT INTO actor_submissions VALUES ('app','caller','targeted',x'01',x'',x'')";
                Assert.Equal(1, command.ExecuteNonQuery());
            }
            caller.Commit();
            Assert.Equal(3L, Assert.IsType<long>(Scalar(db, "PRAGMA user_version")));
            Assert.Equal(1L, Assert.IsType<long>(Scalar(db, "SELECT count(*) FROM actor_submissions WHERE submission_id='caller'")));
        });
    }

    [Fact]
    public void UnsupportedConnectionSettingsAreRejectedWithoutChangingV3()
    {
        InDirectory(directory =>
        {
            var walPath = Path.Combine(directory, "wal.db");
            using (var db = ActorSqliteDatabase.Open(walPath, ActorSqliteSchemaVersion.V3))
            {
                PopulateAll(db, "app");
                Assert.Equal("wal", Assert.IsType<string>(Scalar(db, "PRAGMA journal_mode=WAL")), ignoreCase: true);
                Assert.Throws<InvalidOperationException>(() => ActorSqliteApplicationBinding.BindVersion3(db, "app"));
                Assert.Equal("wal", Assert.IsType<string>(Scalar(db, "PRAGMA journal_mode")), ignoreCase: true);
                Assert.Equal(3L, Assert.IsType<long>(Scalar(db, "PRAGMA user_version")));
                Assert.Equal("0011", Assert.IsType<string>(Scalar(db, "SELECT hex(payload) FROM actor_submissions WHERE submission_id='sub'")));
            }
            var normalPath = Path.Combine(directory, "normal.db");
            using var normal = ActorSqliteDatabase.Open(normalPath, ActorSqliteSchemaVersion.V3);
            Execute(normal, "PRAGMA synchronous=NORMAL");
            Assert.Throws<InvalidOperationException>(() => ActorSqliteApplicationBinding.BindVersion3(normal, "app"));
            Assert.Equal(1L, Assert.IsType<long>(Scalar(normal, "PRAGMA synchronous")));
            Assert.Equal(3L, Assert.IsType<long>(Scalar(normal, "PRAGMA user_version")));
            Execute(normal, "PRAGMA synchronous=FULL");
            Execute(normal, "PRAGMA foreign_keys=OFF");
            Assert.Throws<InvalidOperationException>(() => ActorSqliteApplicationBinding.BindVersion3(normal, "app"));
            Assert.Equal(0L, Assert.IsType<long>(Scalar(normal, "PRAGMA foreign_keys")));
            Assert.Equal(3L, Assert.IsType<long>(Scalar(normal, "PRAGMA user_version")));
        });
    }

    [Fact]
    public void CompetingFirstBindersSerializeAndOnlyOneApplicationWins()
    {
        InDirectory(directory =>
        {
            foreach (var same in new[] { false, true })
            {
                var path = Path.Combine(directory, same ? "same.db" : "different.db");
                using (var initial = ActorSqliteDatabase.Open(path, ActorSqliteSchemaVersion.V3)) { }
                using var first = ActorSqliteDatabase.Open(path, ActorSqliteSchemaVersion.V3);
                using var second = ActorSqliteDatabase.Open(path, ActorSqliteSchemaVersion.V3);
                using var start = new Barrier(2);
                Task<(string App, Exception? Error)> Run(SqliteConnection connection, string app) => Task.Run(() =>
                {
                    if (!start.SignalAndWait(TimeSpan.FromSeconds(10)))
                        throw new TimeoutException("Binders did not reach the barrier.");
                    try { ActorSqliteApplicationBinding.BindVersion3(connection, app); return (app, (Exception?)null); }
                    catch (Exception error) { return (app, error); }
                });
                var results = Task.WhenAll(Run(first, "app-A"), Run(second, same ? "app-A" : "app-B"))
                    .WaitAsync(TimeSpan.FromSeconds(20)).GetAwaiter().GetResult();
                if (same)
                {
                    Assert.All(results, result => Assert.Null(result.Error));
                    using var bound = ActorSqliteDatabase.OpenBound(path, "app-A");
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
}
