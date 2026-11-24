using System.Globalization;
using Microsoft.Data.Sqlite;

namespace DSharp.Runtime.Sqlite;

// This internal opener configures one caller-selected file. The host owns the
// mapping from application to local path and any process ownership policy.
internal static class ActorSqliteDatabase
{
    internal static SqliteConnection Open(string path) => Open(path, ActorSqliteSchemaVersion.V1);

    internal static SqliteConnection Open(string path, ActorSqliteSchemaVersion version)
    {
        // Reject an unsupported target before opening or creating a file.
        ActorSqliteSchema.ValidateVersion(version);
        var connection = OpenConfigured(path, SqliteOpenMode.ReadWriteCreate);
        try
        {
            ActorSqliteSchema.Apply(connection, version);
            return connection;
        }
        catch (Exception error)
        {
            DisposeAfterFailure(connection, error);
            throw;
        }
    }

    // Opening a bound file never creates it. The caller still owns host path
    // selection and must exclusively use the returned connection.
    internal static SqliteConnection OpenBound(string path, string applicationId)
    {
        ActorSqliteApplicationBinding.ValidateApplicationId(applicationId);
        var connection = OpenConfigured(path, SqliteOpenMode.ReadWrite);
        try
        {
            ActorSqliteApplicationBinding.ValidateBound(connection, applicationId);
            return connection;
        }
        catch (Exception error)
        {
            DisposeAfterFailure(connection, error);
            throw;
        }
    }

    // Explicitly initialize or upgrade an older supported file and bind it in
    // one transaction. Invalid application IDs are rejected before opening.
    internal static SqliteConnection OpenAndBind(string path, string applicationId)
    {
        ActorSqliteApplicationBinding.ValidateApplicationId(applicationId);
        var connection = OpenConfigured(path, SqliteOpenMode.ReadWriteCreate);
        try
        {
            ActorSqliteApplicationBinding.BindWithUpgrade(connection, applicationId);
            return connection;
        }
        catch (Exception error)
        {
            DisposeAfterFailure(connection, error);
            throw;
        }
    }

    private static SqliteConnection OpenConfigured(string path, SqliteOpenMode mode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Path.IsPathFullyQualified(path) || path.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ||
            Path.EndsInDirectorySeparator(path))
            throw new ArgumentException("An absolute SQLite file path is required.", nameof(path));

        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = mode,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
            ForeignKeys = true,
            DefaultTimeout = 5
        }.ToString());
        try
        {
            connection.Open();
            // Journal mode is persistent. Do not convert an unsupported or
            // newer database just by attempting to initialize it.
            var journal = Convert.ToString(Scalar(connection, "PRAGMA journal_mode"), CultureInfo.InvariantCulture);
            if (!string.Equals(journal, "delete", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("SQLite journal mode must be DELETE.");

            Execute(connection, "PRAGMA synchronous = FULL");
            Execute(connection, "PRAGMA busy_timeout = 5000");
            if (Convert.ToInt64(Scalar(connection, "PRAGMA foreign_keys"), CultureInfo.InvariantCulture) != 1 ||
                Convert.ToInt64(Scalar(connection, "PRAGMA synchronous"), CultureInfo.InvariantCulture) != 2 ||
                Convert.ToInt64(Scalar(connection, "PRAGMA busy_timeout"), CultureInfo.InvariantCulture) != 5000)
                throw new InvalidOperationException("Required SQLite connection settings are unavailable.");

            return connection;
        }
        catch (Exception error)
        {
            DisposeAfterFailure(connection, error);
            throw;
        }
    }

    private static void DisposeAfterFailure(SqliteConnection connection, Exception failure)
    {
        try { connection.Dispose(); }
        catch (Exception disposeError)
        {
            throw new AggregateException("Opening and connection disposal failed.", failure, disposeError);
        }
    }

    private static object? Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
