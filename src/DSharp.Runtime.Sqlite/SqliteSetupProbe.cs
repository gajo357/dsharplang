using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;

[assembly: InternalsVisibleTo("DSS")]

namespace DSharp.Runtime.Sqlite;

// Setup-only probe for loading the explicitly pinned provider. It is not an
// actor RuntimeStore, schema, durable acknowledgement or public application API.
internal static class SqliteSetupProbe
{
    // Internal setup fixture: exercise the existing core coordinator against the
    // real provider without exposing a schema/provider API to applications.
    public static void ApplySmokeMigration(SqliteConnection connection)
    {
        var coordinator = new ActorSchemaMigrationCoordinator(
            [new ActorSchemaMigration(1, ["CREATE TABLE probe (id INTEGER PRIMARY KEY, value TEXT NOT NULL)"])]);
        coordinator.Apply(connection);
    }

    public static SqliteConnection OpenFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Pooling = false
        }.ToString());
        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }
}
