using DSharp.Runtime.Sqlite;
using Xunit;

namespace DSharp.Runtime.Sqlite.Tests;

public sealed class SqliteSetupTests
{
    [Fact]
    public void FileBackedProviderLoadsNativeLibraryRollsBackAndReopens()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"dsharp-sqlite-setup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "probe.db");
        const string value = "quoted ' value; DROP TABLE probe; --";
        try
        {
            using (var connection = SqliteSetupProbe.OpenFile(path))
            {
                using var version = connection.CreateCommand();
                version.CommandText = "SELECT sqlite_version()";
                Assert.False(string.IsNullOrWhiteSpace(Assert.IsType<string>(version.ExecuteScalar())));
                SqliteSetupProbe.ApplySmokeMigration(connection);
                SqliteSetupProbe.ApplySmokeMigration(connection);
                using var schemaVersion = connection.CreateCommand();
                schemaVersion.CommandText = "PRAGMA user_version";
                Assert.Equal(1L, Assert.IsType<long>(schemaVersion.ExecuteScalar()));
                using var insert = connection.CreateCommand();
                insert.CommandText = "INSERT INTO probe (id, value) VALUES ($id, $value)";
                insert.Parameters.AddWithValue("$id", 1);
                insert.Parameters.AddWithValue("$value", value);
                Assert.Equal(1, insert.ExecuteNonQuery());
                using var transaction = connection.BeginTransaction();
                using var rolledBack = connection.CreateCommand();
                rolledBack.Transaction = transaction;
                rolledBack.CommandText = "INSERT INTO probe (id, value) VALUES ($id, $value)";
                rolledBack.Parameters.AddWithValue("$id", 2);
                rolledBack.Parameters.AddWithValue("$value", "must roll back");
                Assert.Equal(1, rolledBack.ExecuteNonQuery());
                transaction.Rollback();
            }
            using (var reopened = SqliteSetupProbe.OpenFile(path))
            {
                using var schemaVersion = reopened.CreateCommand();
                schemaVersion.CommandText = "PRAGMA user_version";
                Assert.Equal(1L, Assert.IsType<long>(schemaVersion.ExecuteScalar()));
                using var read = reopened.CreateCommand();
                read.CommandText = "SELECT value FROM probe WHERE id = $id";
                read.Parameters.AddWithValue("$id", 1);
                Assert.Equal(value, Assert.IsType<string>(read.ExecuteScalar()));
                using var count = reopened.CreateCommand();
                count.CommandText = "SELECT COUNT(*) FROM probe";
                Assert.Equal(1L, Assert.IsType<long>(count.ExecuteScalar()));
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
