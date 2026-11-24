using DSharp.Runtime.Sqlite;
using Xunit;

namespace DSharp.Runtime.Sqlite.Tests.Features;

public sealed class SqliteProviderFeatureTests
{
    [Fact]
    public void IndependentFilesRemainSeparateAfterReopen()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"dsharp-sqlite-feature-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var firstPath = Path.Combine(directory, "first.db");
        var secondPath = Path.Combine(directory, "second.db");
        try
        {
            foreach (var (path, value) in new[] { (firstPath, "first"), (secondPath, "second") })
            {
                using var connection = SqliteSetupProbe.OpenFile(path);
                using var create = connection.CreateCommand();
                create.CommandText = "CREATE TABLE probe (value TEXT NOT NULL)";
                create.ExecuteNonQuery();
                using var insert = connection.CreateCommand();
                insert.CommandText = "INSERT INTO probe (value) VALUES ($value)";
                insert.Parameters.AddWithValue("$value", value);
                Assert.Equal(1, insert.ExecuteNonQuery());
            }
            foreach (var (path, expected) in new[] { (firstPath, "first"), (secondPath, "second") })
            {
                using var reopened = SqliteSetupProbe.OpenFile(path);
                using var read = reopened.CreateCommand();
                read.CommandText = "SELECT value FROM probe";
                Assert.Equal(expected, Assert.IsType<string>(read.ExecuteScalar()));
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
