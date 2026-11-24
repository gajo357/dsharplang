using DSharp.Runtime.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DSharp.Runtime.Sqlite.Tests.Features;

public sealed class ActorSqliteOutcomeTests
{
    private static void InDirectory(Action<string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"dsharp-actor-outcome-{Guid.NewGuid():N}");
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

    private static void AddInvocation(SqliteConnection db)
    {
        Execute(db, "INSERT INTO actor_submissions VALUES ('app','submission','targeted',x'00FF',x'001122',x'FE')");
        Execute(db, """
            INSERT INTO actor_invocations
              (application_id,invocation_id,submission_id,requested_actor_id,selected_actor_id,target_key,operation)
            VALUES ('app','invocation','submission','requested-stable','selected-stable',x'AB00','invoke')
            """);
        Execute(db, "INSERT INTO actor_attempts VALUES ('app','invocation','attempt-1')");
        Execute(db, "INSERT INTO actor_attempts VALUES ('app','invocation','attempt-2')");
        Execute(db, "INSERT INTO actor_attempts VALUES ('app','invocation','attempt-3')");
    }

    [Fact]
    public void FreshV2AndExplicitV1AreIndependentAndRejectUnsupportedTargets()
    {
        InDirectory(directory =>
        {
            var invalid = Path.Combine(directory, "invalid.db");
            Assert.Throws<ArgumentOutOfRangeException>(() => ActorSqliteDatabase.Open(invalid, (ActorSqliteSchemaVersion)99));
            Assert.False(File.Exists(invalid));

            var v1 = Path.Combine(directory, "v1.db");
            using (var db = ActorSqliteDatabase.Open(v1, ActorSqliteSchemaVersion.V1))
            {
                Assert.Equal(1L, Assert.IsType<long>(Scalar(db, "PRAGMA user_version")));
                ActorSqliteSchema.Apply(db, ActorSqliteSchemaVersion.V1);
                Assert.Equal(0L, Assert.IsType<long>(Scalar(db, "SELECT count(*) FROM sqlite_master WHERE name='actor_attempt_exits'")));
            }

            var v2 = Path.Combine(directory, "v2.db");
            using (var db = ActorSqliteDatabase.Open(v2, ActorSqliteSchemaVersion.V2))
            {
                Assert.Equal(2L, Assert.IsType<long>(Scalar(db, "PRAGMA user_version")));
                Assert.Equal(1L, Assert.IsType<long>(Scalar(db, "PRAGMA foreign_keys")));
                Assert.Equal(2L, Assert.IsType<long>(Scalar(db, "PRAGMA synchronous")));
                ActorSqliteSchema.Apply(db, ActorSqliteSchemaVersion.V2);
                Assert.Equal(2L, Assert.IsType<long>(Scalar(db, "SELECT count(*) FROM sqlite_master WHERE name IN ('actor_attempt_exits','actor_fault_observations')")));
            }
            using (var reopened = ActorSqliteDatabase.Open(v2, ActorSqliteSchemaVersion.V2))
                Assert.Equal(2L, Assert.IsType<long>(Scalar(reopened, "PRAGMA user_version")));
            Assert.Throws<InvalidOperationException>(() => ActorSqliteDatabase.Open(v2));
            using (var reopened = ActorSqliteDatabase.Open(v2, ActorSqliteSchemaVersion.V2))
                Assert.Equal(2L, Assert.IsType<long>(Scalar(reopened, "PRAGMA user_version")));

            var v3 = Path.Combine(directory, "v3.db");
            using (var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = v3, Pooling = false }.ToString()))
            {
                db.Open();
                Execute(db, "PRAGMA user_version = 3");
            }
            Assert.Throws<InvalidOperationException>(() => ActorSqliteDatabase.Open(v3, ActorSqliteSchemaVersion.V2));
            using var raw = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = v3, Pooling = false }.ToString());
            raw.Open();
            Assert.Equal(3L, Assert.IsType<long>(Scalar(raw, "PRAGMA user_version")));
            Assert.Equal(0L, Assert.IsType<long>(Scalar(raw, "SELECT count(*) FROM sqlite_master WHERE name NOT LIKE 'sqlite_%'")));
        });
    }

    [Fact]
    public void UpgradePreservesV1IdentitiesAndOpaqueDataAcrossReopen()
    {
        InDirectory(directory =>
        {
            var path = Path.Combine(directory, "upgrade.db");
            using (var db = ActorSqliteDatabase.Open(path)) AddInvocation(db);
            using (var upgraded = ActorSqliteDatabase.Open(path, ActorSqliteSchemaVersion.V2))
            {
                Assert.Equal(2L, Assert.IsType<long>(Scalar(upgraded, "PRAGMA user_version")));
                Assert.Equal(3L, Assert.IsType<long>(Scalar(upgraded, "SELECT count(*) FROM actor_attempts WHERE application_id='app' AND invocation_id='invocation'")));
                using var command = upgraded.CreateCommand();
                command.CommandText = """
                    SELECT s.contract,s.payload,s.options,i.requested_actor_id,i.selected_actor_id,i.target_key
                    FROM actor_submissions s JOIN actor_invocations i USING(application_id,submission_id)
                    """;
                using var reader = command.ExecuteReader();
                Assert.True(reader.Read());
                Assert.Equal(new byte[] { 0, 255 }, Assert.IsType<byte[]>(reader.GetValue(0)));
                Assert.Equal(new byte[] { 0, 17, 34 }, Assert.IsType<byte[]>(reader.GetValue(1)));
                Assert.Equal(new byte[] { 254 }, Assert.IsType<byte[]>(reader.GetValue(2)));
                Assert.Equal("requested-stable", reader.GetString(3));
                Assert.Equal("selected-stable", reader.GetString(4));
                Assert.Equal(new byte[] { 171, 0 }, Assert.IsType<byte[]>(reader.GetValue(5)));
                Assert.False(reader.Read());
            }
            using var reopened = ActorSqliteDatabase.Open(path, ActorSqliteSchemaVersion.V2);
            Assert.Equal(2L, Assert.IsType<long>(Scalar(reopened, "PRAGMA user_version")));
            Assert.Equal(3L, Assert.IsType<long>(Scalar(reopened, "SELECT count(*) FROM actor_attempts")));
        });
    }

    [Fact]
    public void AttemptExitsPreserveNormalFaultAndDistinctOriginsWithOrderedObservations()
    {
        InDirectory(directory =>
        {
            var path = Path.Combine(directory, "outcomes.db");
            using (var db = ActorSqliteDatabase.Open(path, ActorSqliteSchemaVersion.V2))
            {
                AddInvocation(db);
                Execute(db, "INSERT INTO actor_attempts VALUES ('app','invocation','attempt-4')");
                Execute(db, "INSERT INTO actor_attempts VALUES ('app','invocation','attempt-5')");
                Execute(db, """
                    INSERT INTO actor_attempt_exits
                    (application_id,invocation_id,attempt_id,outcome_kind,payload,contract,report_present)
                    VALUES ('app','invocation','attempt-1','normal',x'',x'01',0)
                    """);
                Execute(db, """
                    INSERT INTO actor_attempt_exits
                    (application_id,invocation_id,attempt_id,outcome_kind,payload,contract,origin_invocation_id,report_present,report_origin_invocation_id)
                    VALUES ('app','invocation','attempt-2','reported_fault',x'00FF',x'AA','abcdefab-cdef-abcd-efab-cdefabcdefab',1,'ABCDEFAB-CDEF-ABCD-EFAB-CDEFABCDEFAB')
                    """);
                Execute(db, """
                    INSERT INTO actor_attempt_exits
                    (application_id,invocation_id,attempt_id,outcome_kind,payload,contract,report_present,report_origin_invocation_id)
                    VALUES ('app','invocation','attempt-3','reported_fault',x'',x'02',1,'report-only-origin')
                    """);
                Execute(db, """
                    INSERT INTO actor_attempt_exits
                    (application_id,invocation_id,attempt_id,outcome_kind,payload,contract,origin_invocation_id,report_present)
                    VALUES ('app','invocation','attempt-4','reported_fault',x'',x'03','exit-only-origin',0)
                    """);
                // The storage layer preserves both unverified values as supplied;
                // the future typed writer must validate that both Guids agree.
                Execute(db, """
                    INSERT INTO actor_attempt_exits
                    (application_id,invocation_id,attempt_id,outcome_kind,payload,contract,origin_invocation_id,report_present,report_origin_invocation_id)
                    VALUES ('app','invocation','attempt-5','reported_fault',x'',x'04','first-unverified',1,'second-unverified')
                    """);
                Execute(db, """
                    INSERT INTO actor_fault_observations
                    (application_id,invocation_id,attempt_id,ordinal,classification,diagnostic,metadata)
                    VALUES ('app','invocation','attempt-2',-1,'primary',x'',x'00')
                    """);
                Execute(db, """
                    INSERT INTO actor_fault_observations
                    (application_id,invocation_id,attempt_id,ordinal,classification,diagnostic,metadata)
                    VALUES ('app','invocation','attempt-2',1,'cleanup-second',x'FF',x'')
                    """);
                Execute(db, """
                    INSERT INTO actor_fault_observations
                    (application_id,invocation_id,attempt_id,ordinal,classification,diagnostic,metadata)
                    VALUES ('app','invocation','attempt-2',0,'cleanup-first',x'01',x'02')
                    """);
                Execute(db, """
                    INSERT INTO actor_fault_observations
                    (application_id,invocation_id,attempt_id,ordinal,classification,diagnostic,metadata)
                    VALUES ('app','invocation','attempt-3',0,'cleanup-only',x'',x'')
                    """);
            }
            using var reopened = ActorSqliteDatabase.Open(path, ActorSqliteSchemaVersion.V2);
            Assert.Equal("normal", Assert.IsType<string>(Scalar(reopened, "SELECT outcome_kind FROM actor_attempt_exits WHERE attempt_id='attempt-1'")));
            Assert.Equal(0L, Assert.IsType<long>(Scalar(reopened, "SELECT length(payload) FROM actor_attempt_exits WHERE attempt_id='attempt-1'")));
            using (var command = reopened.CreateCommand())
            {
                command.CommandText = "SELECT payload,contract,origin_invocation_id,report_origin_invocation_id FROM actor_attempt_exits WHERE attempt_id='attempt-2'";
                using var reader = command.ExecuteReader();
                Assert.True(reader.Read());
                Assert.Equal(new byte[] { 0, 255 }, Assert.IsType<byte[]>(reader.GetValue(0)));
                Assert.Equal(new byte[] { 170 }, Assert.IsType<byte[]>(reader.GetValue(1)));
                Assert.Equal("abcdefab-cdef-abcd-efab-cdefabcdefab", reader.GetString(2));
                Assert.Equal("ABCDEFAB-CDEF-ABCD-EFAB-CDEFABCDEFAB", reader.GetString(3));
            }
            using (var command = reopened.CreateCommand())
            {
                command.CommandText = "SELECT ordinal,classification,diagnostic,metadata FROM actor_fault_observations WHERE attempt_id='attempt-2' ORDER BY ordinal";
                using var reader = command.ExecuteReader();
                Assert.True(reader.Read());
                Assert.Equal(-1L, reader.GetInt64(0));
                Assert.Equal("primary", reader.GetString(1));
                Assert.Empty(Assert.IsType<byte[]>(reader.GetValue(2)));
                Assert.Equal(new byte[] { 0 }, Assert.IsType<byte[]>(reader.GetValue(3)));
                Assert.True(reader.Read());
                Assert.Equal(0L, reader.GetInt64(0));
                Assert.Equal("cleanup-first", reader.GetString(1));
                Assert.True(reader.Read());
                Assert.Equal(1L, reader.GetInt64(0));
                Assert.Equal("cleanup-second", reader.GetString(1));
                Assert.False(reader.Read());
            }
            Assert.Equal(1L, Assert.IsType<long>(Scalar(reopened, "SELECT count(*) FROM actor_fault_observations WHERE attempt_id='attempt-3' AND ordinal=0 AND classification='cleanup-only'")));
            Assert.Equal("report-only-origin", Assert.IsType<string>(Scalar(reopened, "SELECT report_origin_invocation_id FROM actor_attempt_exits WHERE attempt_id='attempt-3' AND origin_invocation_id IS NULL")));
            Assert.Equal("exit-only-origin", Assert.IsType<string>(Scalar(reopened, "SELECT origin_invocation_id FROM actor_attempt_exits WHERE attempt_id='attempt-4' AND report_origin_invocation_id IS NULL AND report_present=0")));
            Assert.Equal(1L, Assert.IsType<long>(Scalar(reopened, "SELECT count(*) FROM actor_attempt_exits WHERE attempt_id='attempt-5' AND origin_invocation_id='first-unverified' AND report_origin_invocation_id='second-unverified'")));
        });
    }

    [Fact]
    public void MalformedExitsAndInvalidObservationRelationshipsAreRejected()
    {
        InDirectory(directory =>
        {
            using var db = ActorSqliteDatabase.Open(Path.Combine(directory, "constraints.db"), ActorSqliteSchemaVersion.V2);
            AddInvocation(db);
            const string columns = "(application_id,invocation_id,attempt_id,outcome_kind,payload,contract,origin_invocation_id,report_present,report_origin_invocation_id)";
            Assert.Throws<SqliteException>(() => Execute(db, $"INSERT INTO actor_attempt_exits {columns} VALUES ('app','invocation','missing','normal',x'',x'01',NULL,0,NULL)"));
            Assert.Throws<SqliteException>(() => Execute(db, $"INSERT INTO actor_attempt_exits {columns} VALUES ('app','invocation','attempt-1','normal',x'',x'',NULL,0,NULL)"));
            Assert.Throws<SqliteException>(() => Execute(db, $"INSERT INTO actor_attempt_exits {columns} VALUES ('app','invocation','attempt-1','other',x'',x'01',NULL,0,NULL)"));
            Assert.Throws<SqliteException>(() => Execute(db, $"INSERT INTO actor_attempt_exits {columns} VALUES ('app','invocation','attempt-1','normal',x'',x'01','origin',0,NULL)"));
            Assert.Throws<SqliteException>(() => Execute(db, $"INSERT INTO actor_attempt_exits {columns} VALUES ('app','invocation','attempt-1','normal',x'',x'01',NULL,1,NULL)"));
            Assert.Throws<SqliteException>(() => Execute(db, $"INSERT INTO actor_attempt_exits {columns} VALUES ('app','invocation','attempt-1','reported_fault',x'',x'01',NULL,0,'origin')"));
            Assert.Throws<SqliteException>(() => Execute(db, $"INSERT INTO actor_attempt_exits {columns} VALUES ('app','invocation','attempt-1','reported_fault',x'',x'01','',0,NULL)"));
            Assert.Throws<SqliteException>(() => Execute(db, $"INSERT INTO actor_attempt_exits {columns} VALUES ('app','invocation','attempt-1','reported_fault',x'',x'01',x'01',0,NULL)"));
            Execute(db, $"INSERT INTO actor_attempt_exits {columns} VALUES ('app','invocation','attempt-1','normal',x'',x'01',NULL,0,NULL)");
            Execute(db, $"INSERT INTO actor_attempt_exits {columns} VALUES ('app','invocation','attempt-2','reported_fault',x'',x'01',NULL,0,NULL)");
            Execute(db, $"INSERT INTO actor_attempt_exits {columns} VALUES ('app','invocation','attempt-3','reported_fault',x'',x'01',NULL,1,NULL)");
            const string observation = "(application_id,invocation_id,attempt_id,ordinal,classification,diagnostic,metadata)";
            Assert.Throws<SqliteException>(() => Execute(db, $"INSERT INTO actor_fault_observations {observation} VALUES ('app','invocation','attempt-1',0,'x',x'',x'')"));
            Assert.Throws<SqliteException>(() => Execute(db, $"INSERT INTO actor_fault_observations {observation} VALUES ('app','invocation','attempt-2',0,'x',x'',x'')"));
            Assert.Throws<SqliteException>(() => Execute(db, $"INSERT INTO actor_fault_observations {observation} VALUES ('app','invocation','missing',0,'x',x'',x'')"));
            Assert.Throws<SqliteException>(() => Execute(db, $"INSERT INTO actor_fault_observations {observation} VALUES ('app','invocation','attempt-3',-2,'x',x'',x'')"));
            Assert.Throws<SqliteException>(() => Execute(db, $"INSERT INTO actor_fault_observations {observation} VALUES ('app','invocation','attempt-3',0,'',x'',x'')"));
            Assert.Throws<SqliteException>(() => Execute(db, $"INSERT INTO actor_fault_observations {observation} VALUES ('app','invocation','attempt-3',0,x'01',x'',x'')"));
            Execute(db, $"INSERT INTO actor_fault_observations {observation} VALUES ('app','invocation','attempt-3',-1,'primary',x'',x'')");
            Assert.Throws<SqliteException>(() => Execute(db, $"INSERT INTO actor_fault_observations {observation} VALUES ('app','invocation','attempt-3',-1,'another-primary',x'',x'')"));
        });
    }

    [Fact]
    public void FailedSecondV2StatementRollsBackMigrationAndKeepsV1Records()
    {
        InDirectory(directory =>
        {
            var path = Path.Combine(directory, "rollback.db");
            using (var db = ActorSqliteDatabase.Open(path))
            {
                AddInvocation(db);
                Execute(db, "CREATE TABLE actor_fault_observations (marker TEXT NOT NULL)");
                Execute(db, "INSERT INTO actor_fault_observations VALUES ('original')");
            }
            Assert.Throws<SqliteException>(() => ActorSqliteDatabase.Open(path, ActorSqliteSchemaVersion.V2));
            using var reopened = ActorSqliteDatabase.Open(path);
            Assert.Equal(1L, Assert.IsType<long>(Scalar(reopened, "PRAGMA user_version")));
            Assert.Equal(0L, Assert.IsType<long>(Scalar(reopened, "SELECT count(*) FROM sqlite_master WHERE name='actor_attempt_exits'")));
            Assert.Equal("original", Assert.IsType<string>(Scalar(reopened, "SELECT marker FROM actor_fault_observations")));
            Assert.Equal(3L, Assert.IsType<long>(Scalar(reopened, "SELECT count(*) FROM actor_attempts")));
            Assert.Equal("001122", Assert.IsType<string>(Scalar(reopened, "SELECT hex(payload) FROM actor_submissions WHERE submission_id='submission'")));
            Assert.Equal("selected-stable", Assert.IsType<string>(Scalar(reopened, "SELECT selected_actor_id FROM actor_invocations WHERE invocation_id='invocation'")));
        });
    }
}
