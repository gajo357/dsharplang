using DSharp.Runtime.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DSharp.Runtime.Sqlite.Tests.Features;

public sealed class ActorSqliteDatabaseTests
{
    private static void InDirectory(Action<string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"dsharp-actor-open-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try { action(directory); }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static SqliteConnection Raw(string path)
    {
        var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        db.Open();
        return db;
    }

    private static object? Scalar(SqliteConnection db, string sql)
    {
        using var command = db.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static void Execute(SqliteConnection db, string sql)
    {
        using var command = db.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void AssertSettings(SqliteConnection db)
    {
        Assert.Equal("delete", Assert.IsType<string>(Scalar(db, "PRAGMA journal_mode")), ignoreCase: true);
        Assert.Equal(1L, Assert.IsType<long>(Scalar(db, "PRAGMA foreign_keys")));
        Assert.Equal(2L, Assert.IsType<long>(Scalar(db, "PRAGMA synchronous")));
        Assert.Equal(5000L, Assert.IsType<long>(Scalar(db, "PRAGMA busy_timeout")));
        Assert.Equal(1L, Assert.IsType<long>(Scalar(db, "PRAGMA user_version")));
    }

    [Fact]
    public void OpensAndReopensConfiguredFileWithItsRecordsAndForeignKeys()
    {
        InDirectory(directory =>
        {
            var path = Path.Combine(directory, "actor.db");
            using (var db = ActorSqliteDatabase.Open(path))
            {
                AssertSettings(db);
                Execute(db, "INSERT INTO actor_submissions VALUES ('app','one','targeted',x'01',x'0001',x'')");
                Assert.Throws<SqliteException>(() => Execute(db, "INSERT INTO actor_attempts VALUES ('app','missing','attempt')"));
            }
            Assert.True(File.Exists(path));
            using var reopened = ActorSqliteDatabase.Open(path);
            AssertSettings(reopened);
            Assert.Equal(1L, Assert.IsType<long>(Scalar(reopened, "SELECT count(*) FROM actor_submissions WHERE application_id='app' AND submission_id='one' AND hex(payload)='0001'")));
            Assert.Throws<SqliteException>(() => Execute(reopened, "INSERT INTO actor_attempts VALUES ('app','missing','attempt')"));
        });
    }

    [Fact]
    public void SeparateCallerSelectedFilesRemainIndependent()
    {
        InDirectory(directory =>
        {
            var first = Path.Combine(directory, "first.db");
            var second = Path.Combine(directory, "second.db");
            using (var db = ActorSqliteDatabase.Open(first))
                Execute(db, "INSERT INTO actor_submissions VALUES ('app','one','targeted',x'01',x'',x'')");
            using (var db = ActorSqliteDatabase.Open(second))
            {
                AssertSettings(db);
                Assert.Equal(0L, Assert.IsType<long>(Scalar(db, "SELECT count(*) FROM actor_submissions")));
            }
            using var reopened = ActorSqliteDatabase.Open(first);
            Assert.Equal(1L, Assert.IsType<long>(Scalar(reopened, "SELECT count(*) FROM actor_submissions")));
        });
    }

    [Fact]
    public void RejectsMemoryUriRelativeAndDirectoryPaths()
    {
        InDirectory(directory =>
        {
            foreach (var path in new[] { "", ":memory:", "file:actor.db?mode=memory", "relative.db", directory + Path.DirectorySeparatorChar })
                Assert.Throws<ArgumentException>(() => ActorSqliteDatabase.Open(path));
            Assert.Empty(Directory.GetFileSystemEntries(directory));
        });
    }

    [Fact]
    public void FilenameContainingConnectionStringSyntaxRemainsAnOrdinaryFile()
    {
        InDirectory(directory =>
        {
            var path = Path.Combine(directory, "actor;Mode=Memory.db");
            using (var db = ActorSqliteDatabase.Open(path))
            {
                AssertSettings(db);
                Execute(db, "INSERT INTO actor_submissions VALUES ('app','one','targeted',x'01',x'',x'')");
            }
            Assert.True(File.Exists(path));
            using var reopened = ActorSqliteDatabase.Open(path);
            AssertSettings(reopened);
            Assert.Equal(1L, Assert.IsType<long>(Scalar(reopened, "SELECT count(*) FROM actor_submissions")));
        });
    }

    [Fact]
    public void UnsupportedJournalModeIsNotConvertedOrDeleted()
    {
        InDirectory(directory =>
        {
            var path = Path.Combine(directory, "wal.db");
            using (var db = Raw(path))
            {
                Assert.Equal("wal", Assert.IsType<string>(Scalar(db, "PRAGMA journal_mode=WAL")), ignoreCase: true);
                Execute(db, "CREATE TABLE marker (value TEXT NOT NULL)");
                Execute(db, "INSERT INTO marker VALUES ('kept')");
            }
            Assert.Throws<InvalidOperationException>(() => ActorSqliteDatabase.Open(path));
            Assert.True(File.Exists(path));
            using var reopened = Raw(path);
            Assert.Equal("wal", Assert.IsType<string>(Scalar(reopened, "PRAGMA journal_mode")), ignoreCase: true);
            Assert.Equal("kept", Assert.IsType<string>(Scalar(reopened, "SELECT value FROM marker")));
            Assert.Equal(0L, Assert.IsType<long>(Scalar(reopened, "PRAGMA user_version")));
        });
    }

    [Fact]
    public void NewerSchemaFailurePreservesFileAndAllowsSubsequentExclusiveTransaction()
    {
        InDirectory(directory =>
        {
            var path = Path.Combine(directory, "newer.db");
            using (var db = Raw(path))
            {
                Execute(db, "CREATE TABLE marker (value TEXT NOT NULL)");
                Execute(db, "INSERT INTO marker VALUES ('kept')");
                Execute(db, "PRAGMA user_version = 2");
            }
            Assert.Throws<InvalidOperationException>(() => ActorSqliteDatabase.Open(path));
            Assert.True(File.Exists(path));
            using var reopened = Raw(path);
            Assert.Equal("delete", Assert.IsType<string>(Scalar(reopened, "PRAGMA journal_mode")), ignoreCase: true);
            Assert.Equal(2L, Assert.IsType<long>(Scalar(reopened, "PRAGMA user_version")));
            Assert.Equal("kept", Assert.IsType<string>(Scalar(reopened, "SELECT value FROM marker")));
            Execute(reopened, "BEGIN EXCLUSIVE");
            Execute(reopened, "ROLLBACK");
        });
    }
}
