using DSharp.Runtime.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DSharp.Runtime.Sqlite.Tests.Features;

public sealed class ActorSqliteLifecycleTests
{
    private static void InDirectory(Action<string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"dsharp-actor-lifecycle-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try { action(directory); }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static int Execute(SqliteConnection db, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = db.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return command.ExecuteNonQuery();
    }

    private static object? Scalar(SqliteConnection db, string sql)
    {
        using var command = db.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static void AddInvocation(SqliteConnection db, string app, string suffix)
    {
        Execute(db, "INSERT INTO actor_submissions VALUES ($app,$sub,'targeted',x'00FF',x'001122',x'FE')",
            ("$app", app), ("$sub", $"submission-{suffix}"));
        Execute(db, """
            INSERT INTO actor_invocations
              (application_id,invocation_id,submission_id,requested_actor_id,selected_actor_id,target_key,operation)
            VALUES ($app,$inv,$sub,'requested-stable','selected-stable',x'AB00','invoke')
            """, ("$app", app), ("$inv", $"invocation-{suffix}"), ("$sub", $"submission-{suffix}"));
        Execute(db, "INSERT INTO actor_attempts VALUES ($app,$inv,$attempt)",
            ("$app", app), ("$inv", $"invocation-{suffix}"), ("$attempt", $"attempt-{suffix}"));
    }

    [Fact]
    public void FreshV3AndPopulatedV1V2UpgradePreserveExistingRecords()
    {
        InDirectory(directory =>
        {
            var fresh = Path.Combine(directory, "fresh.db");
            using (var db = ActorSqliteDatabase.Open(fresh, ActorSqliteSchemaVersion.V3))
            {
                Assert.Equal(3L, Assert.IsType<long>(Scalar(db, "PRAGMA user_version")));
                Assert.Equal(1L, Assert.IsType<long>(Scalar(db, "PRAGMA foreign_keys")));
                ActorSqliteSchema.Apply(db, ActorSqliteSchemaVersion.V3);
                Assert.Equal(2L, Assert.IsType<long>(Scalar(db, "SELECT count(*) FROM sqlite_master WHERE name IN ('actor_invocation_lifecycle','actor_attempt_recoveries')")));
            }
            using (var reopened = ActorSqliteDatabase.Open(fresh, ActorSqliteSchemaVersion.V3))
                Assert.Equal(3L, Assert.IsType<long>(Scalar(reopened, "PRAGMA user_version")));

            var fromV1 = Path.Combine(directory, "from-v1.db");
            using (var db = ActorSqliteDatabase.Open(fromV1)) AddInvocation(db, "app", "v1");
            using (var db = ActorSqliteDatabase.Open(fromV1, ActorSqliteSchemaVersion.V3))
            {
                Assert.Equal(3L, Assert.IsType<long>(Scalar(db, "PRAGMA user_version")));
                Assert.Equal("selected-stable", Assert.IsType<string>(Scalar(db, "SELECT selected_actor_id FROM actor_invocations WHERE invocation_id='invocation-v1'")));
                Assert.Equal("001122", Assert.IsType<string>(Scalar(db, "SELECT hex(payload) FROM actor_submissions WHERE submission_id='submission-v1'")));
                Assert.Equal(0L, Assert.IsType<long>(Scalar(db, "SELECT count(*) FROM actor_attempt_exits")));
            }

            var fromV2 = Path.Combine(directory, "from-v2.db");
            using (var db = ActorSqliteDatabase.Open(fromV2, ActorSqliteSchemaVersion.V2))
            {
                AddInvocation(db, "app", "v2");
                Execute(db, """
                    INSERT INTO actor_attempt_exits
                      (application_id,invocation_id,attempt_id,outcome_kind,payload,contract,report_present)
                    VALUES ('app','invocation-v2','attempt-v2','normal',x'0001FF',x'CAFE',0)
                    """);
            }
            using (var upgraded = ActorSqliteDatabase.Open(fromV2, ActorSqliteSchemaVersion.V3))
            {
                Assert.Equal(3L, Assert.IsType<long>(Scalar(upgraded, "PRAGMA user_version")));
                Assert.Equal("selected-stable", Assert.IsType<string>(Scalar(upgraded, "SELECT selected_actor_id FROM actor_invocations WHERE invocation_id='invocation-v2'")));
                Assert.Equal("0001FF", Assert.IsType<string>(Scalar(upgraded, "SELECT hex(payload) FROM actor_attempt_exits WHERE attempt_id='attempt-v2'")));
                Assert.Equal("CAFE", Assert.IsType<string>(Scalar(upgraded, "SELECT hex(contract) FROM actor_attempt_exits WHERE attempt_id='attempt-v2'")));
            }
            Assert.Throws<InvalidOperationException>(() => ActorSqliteDatabase.Open(fromV2));
            Assert.Throws<InvalidOperationException>(() => ActorSqliteDatabase.Open(fromV2, ActorSqliteSchemaVersion.V2));
            using var final = ActorSqliteDatabase.Open(fromV2, ActorSqliteSchemaVersion.V3);
            Assert.Equal("0001FF", Assert.IsType<string>(Scalar(final, "SELECT hex(payload) FROM actor_attempt_exits WHERE attempt_id='attempt-v2'")));
        });
    }

    [Fact]
    public void InvalidTargetDoesNotCreateFileAndNewerFileFailsClosed()
    {
        InDirectory(directory =>
        {
            var invalid = Path.Combine(directory, "invalid.db");
            Assert.Throws<ArgumentOutOfRangeException>(() => ActorSqliteDatabase.Open(invalid, (ActorSqliteSchemaVersion)99));
            Assert.False(File.Exists(invalid));
            var path = Path.Combine(directory, "newer.db");
            using (var db = ActorSqliteDatabase.Open(path, ActorSqliteSchemaVersion.V3))
            {
                AddInvocation(db, "app", "newer");
                Execute(db, "PRAGMA user_version = 4");
            }
            Assert.Throws<InvalidOperationException>(() => ActorSqliteDatabase.Open(path, ActorSqliteSchemaVersion.V3));
            using var raw = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
            raw.Open();
            Assert.Equal(4L, Assert.IsType<long>(Scalar(raw, "PRAGMA user_version")));
            Assert.Equal("selected-stable", Assert.IsType<string>(Scalar(raw, "SELECT selected_actor_id FROM actor_invocations WHERE invocation_id='invocation-newer'")));
        });
    }

    [Fact]
    public void LifecycleRetainsCancellationAndTerminalOwnershipShapes()
    {
        InDirectory(directory =>
        {
            var path = Path.Combine(directory, "lifecycle.db");
            using (var db = ActorSqliteDatabase.Open(path, ActorSqliteSchemaVersion.V3))
            {
                foreach (var suffix in new[] { "before", "after", "blocked", "completed", "faulted", "expired", "running", "blocked-live", "eligible" }) AddInvocation(db, "app", suffix);
                Execute(db, """
                    INSERT INTO actor_invocation_lifecycle
                      (application_id,invocation_id,state,eligibility_version,eligible_at_utc_ticks,cancellation_requested)
                    VALUES ('app','invocation-before','cancelled',1,100,1)
                    """);
                Execute(db, """
                    INSERT INTO actor_invocation_lifecycle
                      (application_id,invocation_id,state,eligibility_version,cancellation_requested,attempt_id,token_id,lease_until_utc_ticks)
                    VALUES ('app','invocation-after','cancelled',2,1,'attempt-after','token-after',300)
                    """);
                Execute(db, """
                    INSERT INTO actor_invocation_lifecycle
                      (application_id,invocation_id,state,eligibility_version,eligible_at_utc_ticks,cancellation_requested,block_id,block_reason)
                    VALUES ('app','invocation-blocked','cancelled',3,400,1,'block-1','incompatible')
                    """);
                Execute(db, """
                    INSERT INTO actor_invocation_lifecycle
                      (application_id,invocation_id,state,eligibility_version,cancellation_requested,attempt_id,token_id,lease_until_utc_ticks)
                    VALUES ('app','invocation-completed','completed',0,0,'attempt-completed','token-completed',500),
                           ('app','invocation-faulted','failed_reported_fault',0,0,'attempt-faulted','token-faulted',600),
                           ('app','invocation-expired','failed_lease_expired',0,0,'attempt-expired','token-expired',700),
                           ('app','invocation-running','running',1,1,'attempt-running','token-running',3155378975999999999)
                    """);
                Execute(db, """
                    INSERT INTO actor_invocation_lifecycle
                      (application_id,invocation_id,state,eligibility_version,eligible_at_utc_ticks,cancellation_requested,block_id,block_reason)
                    VALUES ('app','invocation-blocked-live','blocked',1,0,0,'block-live','implementation_unavailable')
                    """);
                Execute(db, """
                    INSERT INTO actor_invocation_lifecycle
                      (application_id,invocation_id,state,eligibility_version,eligible_at_utc_ticks,cancellation_requested)
                    VALUES ('app','invocation-eligible','eligible',2,3155378975999999999,0)
                    """);
            }
            using var reopened = ActorSqliteDatabase.Open(path, ActorSqliteSchemaVersion.V3);
            Assert.Equal(1L, Assert.IsType<long>(Scalar(reopened, "SELECT count(*) FROM actor_invocation_lifecycle WHERE invocation_id='invocation-before' AND state='cancelled' AND attempt_id IS NULL AND eligible_at_utc_ticks=100")));
            Assert.Equal(1L, Assert.IsType<long>(Scalar(reopened, "SELECT count(*) FROM actor_invocation_lifecycle WHERE invocation_id='invocation-after' AND state='cancelled' AND attempt_id='attempt-after' AND token_id='token-after' AND lease_until_utc_ticks=300")));
            Assert.Equal(1L, Assert.IsType<long>(Scalar(reopened, "SELECT count(*) FROM actor_invocation_lifecycle WHERE invocation_id='invocation-blocked' AND state='cancelled' AND block_id='block-1' AND block_reason='incompatible' AND eligible_at_utc_ticks=400")));
            Assert.Equal(3L, Assert.IsType<long>(Scalar(reopened, "SELECT count(*) FROM actor_invocation_lifecycle WHERE state IN ('completed','failed_reported_fault','failed_lease_expired') AND attempt_id IS NOT NULL AND token_id IS NOT NULL AND lease_until_utc_ticks IS NOT NULL")));
            Assert.Equal(1L, Assert.IsType<long>(Scalar(reopened, "SELECT count(*) FROM actor_invocation_lifecycle WHERE invocation_id='invocation-running' AND state='running' AND cancellation_requested=1 AND lease_until_utc_ticks=3155378975999999999")));
            Assert.Equal(1L, Assert.IsType<long>(Scalar(reopened, "SELECT count(*) FROM actor_invocation_lifecycle WHERE invocation_id='invocation-blocked-live' AND state='blocked' AND eligible_at_utc_ticks=0 AND block_reason='implementation_unavailable'")));
            Assert.Equal(1L, Assert.IsType<long>(Scalar(reopened, "SELECT count(*) FROM actor_invocation_lifecycle WHERE invocation_id='invocation-eligible' AND state='eligible' AND eligible_at_utc_ticks=3155378975999999999")));
            Assert.Equal(0L, Assert.IsType<long>(Scalar(reopened, "SELECT count(*) FROM actor_attempt_exits WHERE invocation_id='invocation-expired'")));
        });
    }

    [Fact]
    public void RecoveryHistoryKeepsFaultAndLeaseLossDistinctAndOrdered()
    {
        InDirectory(directory =>
        {
            var path = Path.Combine(directory, "recoveries.db");
            using (var db = ActorSqliteDatabase.Open(path, ActorSqliteSchemaVersion.V3))
            {
                AddInvocation(db, "app", "history");
                Execute(db, "INSERT INTO actor_attempts VALUES ('app','invocation-history','attempt-second')");
                Execute(db, """
                    INSERT INTO actor_attempt_recoveries
                      (application_id,invocation_id,ordinal,attempt_id,token_id,lease_until_utc_ticks,eligible_at_utc_ticks,kind)
                    VALUES ('app','invocation-history',1,'attempt-second','token-second',200,3155378975999999999,'lease_expired'),
                           ('app','invocation-history',0,'attempt-history','token-first',100,150,'reported_fault')
                    """);
                Assert.Throws<SqliteException>(() => Execute(db, "INSERT INTO actor_attempt_recoveries VALUES ('app','invocation-history',0,'attempt-history','other',100,150,'lease_expired')"));
            }
            using var reopened = ActorSqliteDatabase.Open(path, ActorSqliteSchemaVersion.V3);
            using var command = reopened.CreateCommand();
            command.CommandText = "SELECT ordinal,attempt_id,token_id,lease_until_utc_ticks,eligible_at_utc_ticks,kind FROM actor_attempt_recoveries ORDER BY ordinal";
            using var reader = command.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal(0L, reader.GetInt64(0));
            Assert.Equal("attempt-history", reader.GetString(1));
            Assert.Equal("token-first", reader.GetString(2));
            Assert.Equal(100L, reader.GetInt64(3));
            Assert.Equal(150L, reader.GetInt64(4));
            Assert.Equal("reported_fault", reader.GetString(5));
            Assert.True(reader.Read());
            Assert.Equal(1L, reader.GetInt64(0));
            Assert.Equal("attempt-second", reader.GetString(1));
            Assert.Equal(200L, reader.GetInt64(3));
            Assert.Equal(3155378975999999999L, reader.GetInt64(4));
            Assert.Equal("lease_expired", reader.GetString(5));
            Assert.False(reader.Read());
            reader.Close();
            Assert.Equal(0L, Assert.IsType<long>(Scalar(reopened, "SELECT count(*) FROM actor_attempt_exits")));
        });
    }

    [Fact]
    public void InvalidStructuralShapesAndCrossInvocationAttemptsAreRejected()
    {
        InDirectory(directory =>
        {
            using var db = ActorSqliteDatabase.Open(Path.Combine(directory, "constraints.db"), ActorSqliteSchemaVersion.V3);
            AddInvocation(db, "app", "a");
            AddInvocation(db, "app", "b");
            AddInvocation(db, "other-app", "other");
            const string columns = "(application_id,invocation_id,state,eligibility_version,eligible_at_utc_ticks,cancellation_requested,attempt_id,token_id,lease_until_utc_ticks,block_id,block_reason)";
            Assert.Throws<SqliteException>(() => Execute(db, $"INSERT INTO actor_invocation_lifecycle {columns} VALUES ('app','invocation-a','unknown',0,NULL,0,NULL,NULL,NULL,NULL,NULL)"));
            Assert.Throws<SqliteException>(() => Execute(db, $"INSERT INTO actor_invocation_lifecycle {columns} VALUES ('app','invocation-a','eligible',-1,NULL,0,NULL,NULL,NULL,NULL,NULL)"));
            Assert.Throws<SqliteException>(() => Execute(db, $"INSERT INTO actor_invocation_lifecycle {columns} VALUES ('app','invocation-a','eligible',0,-1,0,NULL,NULL,NULL,NULL,NULL)"));
            Assert.Throws<SqliteException>(() => Execute(db, $"INSERT INTO actor_invocation_lifecycle {columns} VALUES ('app','invocation-a','eligible',0,3155378976000000000,0,NULL,NULL,NULL,NULL,NULL)"));
            Assert.Throws<SqliteException>(() => Execute(db, $"INSERT INTO actor_invocation_lifecycle {columns} VALUES ('app','invocation-a','eligible',0,NULL,2,NULL,NULL,NULL,NULL,NULL)"));
            Assert.Throws<SqliteException>(() => Execute(db, $"INSERT INTO actor_invocation_lifecycle {columns} VALUES ('app','invocation-a','running',0,NULL,0,'attempt-a',NULL,100,NULL,NULL)"));
            Assert.Throws<SqliteException>(() => Execute(db, $"INSERT INTO actor_invocation_lifecycle {columns} VALUES ('app','invocation-a','blocked',0,NULL,0,NULL,NULL,NULL,'block',NULL)"));
            Assert.Throws<SqliteException>(() => Execute(db, $"INSERT INTO actor_invocation_lifecycle {columns} VALUES ('app','invocation-a','blocked',0,NULL,0,NULL,NULL,NULL,'block','other')"));
            Assert.Throws<SqliteException>(() => Execute(db, $"INSERT INTO actor_invocation_lifecycle {columns} VALUES ('app','invocation-a','running',0,NULL,0,'attempt-b','token',100,NULL,NULL)"));
            Assert.Throws<SqliteException>(() => Execute(db, $"INSERT INTO actor_invocation_lifecycle {columns} VALUES ('app','invocation-a','running',0,NULL,0,'attempt-other','token',100,NULL,NULL)"));
            Assert.Throws<SqliteException>(() => Execute(db, $"INSERT INTO actor_invocation_lifecycle {columns} VALUES ('app','missing','eligible',0,NULL,0,NULL,NULL,NULL,NULL,NULL)"));
            Assert.Throws<SqliteException>(() => Execute(db, "INSERT INTO actor_attempt_recoveries VALUES ('app','invocation-a',-1,'attempt-a','token',10,20,'lease_expired')"));
            Assert.Throws<SqliteException>(() => Execute(db, "INSERT INTO actor_attempt_recoveries VALUES ('app','invocation-a',0,'attempt-b','token',10,20,'lease_expired')"));
            Assert.Throws<SqliteException>(() => Execute(db, "INSERT INTO actor_attempt_recoveries VALUES ('app','invocation-a',0,'attempt-a','token',10,20,'other')"));
            Assert.Throws<SqliteException>(() => Execute(db, "INSERT INTO actor_attempt_recoveries VALUES ('app','invocation-a',0,'attempt-a','token',10,3155378976000000000,'lease_expired')"));
        });
    }

    [Fact]
    public void FailedSecondV3StatementRollsBackWithoutLosingV2Data()
    {
        InDirectory(directory =>
        {
            var path = Path.Combine(directory, "rollback.db");
            using (var db = ActorSqliteDatabase.Open(path, ActorSqliteSchemaVersion.V2))
            {
                AddInvocation(db, "app", "existing");
                Execute(db, "INSERT INTO actor_attempt_exits (application_id,invocation_id,attempt_id,outcome_kind,payload,contract,report_present) VALUES ('app','invocation-existing','attempt-existing','normal',x'99',x'02',0)");
                Execute(db, "CREATE TABLE actor_attempt_recoveries (marker TEXT NOT NULL)");
                Execute(db, "INSERT INTO actor_attempt_recoveries VALUES ('original')");
            }
            Assert.Throws<SqliteException>(() => ActorSqliteDatabase.Open(path, ActorSqliteSchemaVersion.V3));
            using var reopened = ActorSqliteDatabase.Open(path, ActorSqliteSchemaVersion.V2);
            Assert.Equal(2L, Assert.IsType<long>(Scalar(reopened, "PRAGMA user_version")));
            Assert.Equal(0L, Assert.IsType<long>(Scalar(reopened, "SELECT count(*) FROM sqlite_master WHERE name='actor_invocation_lifecycle'")));
            Assert.Equal("original", Assert.IsType<string>(Scalar(reopened, "SELECT marker FROM actor_attempt_recoveries")));
            Assert.Equal("selected-stable", Assert.IsType<string>(Scalar(reopened, "SELECT selected_actor_id FROM actor_invocations WHERE invocation_id='invocation-existing'")));
            Assert.Equal("99", Assert.IsType<string>(Scalar(reopened, "SELECT hex(payload) FROM actor_attempt_exits WHERE attempt_id='attempt-existing'")));
        });
    }
}
