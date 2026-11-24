using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Runtime.ExceptionServices;

namespace DSharp.Runtime;

// Trusted internal migration definitions, not an application SQL interface.
// Each statement must be a single transaction-compatible statement, with no
// transaction control, user_version changes, ATTACH, VACUUM or external effects.
// This type does not parse SQL or enforce that precondition. Statements and
// enumerables are materialized before any transaction starts.
internal sealed class ActorSchemaMigration
{
    public ActorSchemaMigration(int version, IEnumerable<string> statements)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);
        ArgumentNullException.ThrowIfNull(statements);
        var copied = new List<string>();
        foreach (var statement in statements)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(statement);
            copied.Add(statement);
        }
        if (copied.Count == 0)
            throw new ArgumentException("A migration must contain at least one statement.", nameof(statements));
        Version = version;
        Statements = Array.AsReadOnly(copied.ToArray());
    }

    public int Version { get; }
    public IReadOnlyList<string> Statements { get; }
}

/// <summary>
/// Coordinates trusted schema migrations on an already-open caller-owned
/// connection. The provider supplies transaction behavior; this class does not
/// establish SQLite locking, durability, crash recovery or schema compatibility.
/// A commit exception or later disposal failure leaves the outcome potentially
/// uncertain; rollback after a commit exception is best effort.
/// </summary>
internal sealed class ActorSchemaMigrationCoordinator
{
    internal const string ReadVersionSql = "PRAGMA user_version";
    internal const string CountUserObjectsSql = "SELECT COUNT(*) FROM sqlite_master WHERE substr(lower(name), 1, 7) <> 'sqlite_'";
    private readonly IReadOnlyList<ActorSchemaMigration> migrations;

    public ActorSchemaMigrationCoordinator(IEnumerable<ActorSchemaMigration> migrations)
    {
        ArgumentNullException.ThrowIfNull(migrations);
        var copied = new List<ActorSchemaMigration>();
        foreach (var migration in migrations)
        {
            if (migration is null)
                throw new ArgumentException("Migration entries cannot be null.", nameof(migrations));
            if (migration.Version != (long)copied.Count + 1)
                throw new ArgumentException("Migration versions must be contiguous and start at one.", nameof(migrations));
            copied.Add(migration);
        }
        if (copied.Count == 0)
            throw new ArgumentException("At least one migration is required.", nameof(migrations));
        this.migrations = Array.AsReadOnly(copied.ToArray());
    }

    public void Apply(DbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (connection.State != ConnectionState.Open)
            throw new InvalidOperationException("The caller must supply an open connection.");

        var transaction = connection.BeginTransaction();
        Exception? failure = null;
        try
        {
            var storedVersion = ReadNonnegativeInteger(connection, transaction, ReadVersionSql, "schema version");
            if (storedVersion > migrations.Count)
                throw new InvalidOperationException("The stored schema version is newer than this coordinator supports.");
            if (storedVersion == 0 && ReadNonnegativeInteger(connection, transaction, CountUserObjectsSql, "schema object count") != 0)
                throw new InvalidOperationException("A nonempty unversioned database cannot be initialized.");

            if (storedVersion < migrations.Count)
            {
                for (var index = (int)storedVersion; index < migrations.Count; index++)
                    foreach (var statement in migrations[index].Statements)
                        Execute(connection, transaction, statement, command => command.ExecuteNonQuery());

                var finalVersion = migrations.Count.ToString(CultureInfo.InvariantCulture);
                Execute(connection, transaction, $"PRAGMA user_version = {finalVersion}", command => command.ExecuteNonQuery());
            }
            transaction.Commit();
        }
        catch (Exception error)
        {
            failure = error;
            try { transaction.Rollback(); }
            catch (Exception rollbackError)
            {
                failure = new AggregateException("Migration failed and rollback also failed.", error, rollbackError);
            }
        }

        try { transaction.Dispose(); }
        catch (Exception disposeError)
        {
            failure = failure is null ? disposeError :
                new AggregateException("Migration and transaction disposal failed.", failure, disposeError);
        }
        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static long ReadNonnegativeInteger(DbConnection connection, DbTransaction transaction, string sql, string description)
    {
        var value = Execute(connection, transaction, sql, command => command.ExecuteScalar());
        long result = value switch
        {
            byte number => number,
            sbyte number => number,
            short number => number,
            ushort number => number,
            int number => number,
            uint number => number,
            long number => number,
            ulong number when number <= long.MaxValue => (long)number,
            _ => throw new InvalidOperationException($"Invalid {description}.")
        };
        if (result < 0)
            throw new InvalidOperationException($"Invalid {description}.");
        return result;
    }

    private static T Execute<T>(DbConnection connection, DbTransaction transaction, string sql, Func<DbCommand, T> operation)
    {
        var command = connection.CreateCommand();
        T result = default!;
        Exception? failure = null;
        try
        {
            command.CommandText = sql;
            command.Transaction = transaction;
            result = operation(command);
        }
        catch (Exception error) { failure = error; }

        try { command.Dispose(); }
        catch (Exception disposeError)
        {
            failure = failure is null ? disposeError :
                new AggregateException("Command execution and disposal failed.", failure, disposeError);
        }
        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
        return result;
    }
}
