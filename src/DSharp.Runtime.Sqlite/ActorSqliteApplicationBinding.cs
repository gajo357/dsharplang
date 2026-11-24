using System.Data;
using System.Globalization;
using System.Runtime.ExceptionServices;
using Microsoft.Data.Sqlite;

namespace DSharp.Runtime.Sqlite;

// This is an opt-in storage boundary for trusted schema and connections, not
// authentication, a host registry, or a defense against schema tampering.
internal static class ActorSqliteApplicationBinding
{
    private static readonly string[] ActorTables =
    [
        "actor_submissions", "actor_invocations", "actor_attempts",
        "actor_publications", "actor_deliveries", "actor_attempt_exits",
        "actor_fault_observations", "actor_invocation_lifecycle", "actor_attempt_recoveries"
    ];

    internal static void ValidateApplicationId(string applicationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationId);
        // SQLite TEXT cannot faithfully represent NUL or unpaired UTF-16 for
        // our ordinal comparison. Fail closed rather than silently rebind them.
        for (var i = 0; i < applicationId.Length; i++)
        {
            var ch = applicationId[i];
            if (ch == '\0' || char.IsLowSurrogate(ch))
                throw new ArgumentException("The application ID must be well-formed text without NUL.", nameof(applicationId));
            if (char.IsHighSurrogate(ch))
            {
                if (++i >= applicationId.Length || !char.IsLowSurrogate(applicationId[i]))
                    throw new ArgumentException("The application ID must be well-formed text without NUL.", nameof(applicationId));
            }
        }
    }

    // Bind an already-version-three database, or revalidate a version-four
    // binding. The caller owns the open connection and must use it exclusively.
    // All validation and schema changes run under the same immediate lock.
    internal static void BindVersion3(SqliteConnection connection, string applicationId)
    {
        ValidateApplicationId(applicationId);
        EnsureReady(connection);
        var transaction = connection.BeginTransaction(deferred: false);
        Exception? failure = null, rollbackFailure = null, disposeFailure = null;
        try
        {
            // Journal mode is persistent and another connection could have
            // changed it after the initial preflight, before this lock.
            EnsureSettings(connection, transaction);
            var version = ReadLong(connection, transaction, "PRAGMA user_version");
            if (version is not (3 or 4))
                throw new InvalidOperationException("Only a version-three database can be bound.");
            if (version == 4)
            {
                CheckBound(connection, transaction, applicationId);
            }
            else
            {
                CheckExistingRows(connection, transaction, applicationId);
                Execute(connection, transaction, """
                    CREATE TABLE actor_application_binding (
                      singleton INTEGER NOT NULL PRIMARY KEY CHECK (singleton = 1),
                      application_id TEXT NOT NULL CHECK (typeof(application_id) = 'text' AND length(application_id) > 0)
                    )
                    """);
                Execute(connection, transaction, """
                    CREATE TRIGGER actor_binding_no_second_insert BEFORE INSERT ON actor_application_binding
                    WHEN EXISTS (SELECT 1 FROM actor_application_binding)
                    BEGIN SELECT RAISE(ABORT, 'application binding is immutable'); END
                    """);
                Execute(connection, transaction, """
                    CREATE TRIGGER actor_binding_no_update BEFORE UPDATE ON actor_application_binding
                    BEGIN SELECT RAISE(ABORT, 'application binding is immutable'); END
                    """);
                Execute(connection, transaction, """
                    CREATE TRIGGER actor_binding_no_delete BEFORE DELETE ON actor_application_binding
                    BEGIN SELECT RAISE(ABORT, 'application binding is immutable'); END
                    """);
                foreach (var table in ActorTables)
                {
                    // Table names are a fixed internal list, never caller input.
                    foreach (var operation in new[] { "INSERT", "UPDATE" })
                        Execute(connection, transaction, $"""
                            CREATE TRIGGER {table}_application_guard_{operation.ToLowerInvariant()}
                            BEFORE {operation} ON {table}
                            WHEN NOT EXISTS (SELECT 1 FROM actor_application_binding
                              WHERE singleton = 1 AND application_id = NEW.application_id COLLATE BINARY)
                            BEGIN SELECT RAISE(ABORT, 'application binding mismatch'); END
                            """);
                }
                Execute(connection, transaction,
                    "INSERT INTO actor_application_binding (singleton, application_id) VALUES (1, $application)", applicationId);
                Execute(connection, transaction, "PRAGMA user_version = 4");
            }
            transaction.Commit();
        }
        catch (Exception error)
        {
            failure = error;
            try { transaction.Rollback(); }
            catch (Exception rollbackError) { rollbackFailure = rollbackError; }
        }
        finally
        {
            try { transaction.Dispose(); }
            catch (Exception disposeError) { disposeFailure = disposeError; }
        }
        ThrowFailures(failure, rollbackFailure, disposeFailure);
    }

    // Validate an existing v4 file without rebinding it. Schema and triggers
    // are trusted: this does not defend against deliberate schema replacement.
    internal static void ValidateBound(SqliteConnection connection, string applicationId)
    {
        ValidateApplicationId(applicationId);
        EnsureReady(connection);
        // Hold the same immediate lock while checking settings, guards and data;
        // an unrelated connection must not change journal mode between checks.
        var transaction = connection.BeginTransaction(deferred: false);
        Exception? failure = null, rollbackFailure = null, disposeFailure = null;
        try
        {
            EnsureSettings(connection, transaction);
            if (ReadLong(connection, transaction, "PRAGMA user_version") != 4)
                throw new InvalidOperationException("A version-four bound database is required.");
            CheckBound(connection, transaction, applicationId);
            transaction.Commit();
        }
        catch (Exception error)
        {
            failure = error;
            try { transaction.Rollback(); }
            catch (Exception rollbackError) { rollbackFailure = rollbackError; }
        }
        finally
        {
            try { transaction.Dispose(); }
            catch (Exception disposeError) { disposeFailure = disposeError; }
        }
        ThrowFailures(failure, rollbackFailure, disposeFailure);
    }

    private static void CheckBound(SqliteConnection connection, SqliteTransaction transaction, string applicationId)
    {
        if (ReadLong(connection, transaction, """
            SELECT COUNT(*) FROM actor_application_binding
            WHERE singleton = 1 AND typeof(application_id) = 'text' AND application_id = $application COLLATE BINARY
            """, applicationId) != 1 ||
            ReadLong(connection, transaction, "SELECT COUNT(*) FROM actor_application_binding") != 1)
            throw new InvalidOperationException("The application binding does not match.");
        // A missing guard must not silently turn a supported v4 reopen into an
        // unguarded connection. Names are fixed; schema replacement remains
        // outside the trusted-schema boundary.
        foreach (var name in new[] { "actor_binding_no_second_insert", "actor_binding_no_update", "actor_binding_no_delete" })
            RequireGuard(connection, transaction, name);
        foreach (var table in ActorTables)
        {
            RequireGuard(connection, transaction, $"{table}_application_guard_insert");
            RequireGuard(connection, transaction, $"{table}_application_guard_update");
        }
        CheckExistingRows(connection, transaction, applicationId);
    }

    private static void RequireGuard(SqliteConnection connection, SqliteTransaction transaction, string name)
    {
        if (ReadLong(connection, transaction,
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'trigger' AND name = $application COLLATE BINARY", name) != 1)
            throw new InvalidOperationException("An application binding guard is missing.");
    }

    private static void CheckExistingRows(SqliteConnection connection, SqliteTransaction transaction, string applicationId)
    {
        foreach (var table in ActorTables)
            if (ReadLong(connection, transaction, $"""
                SELECT COUNT(*) FROM {table}
                WHERE typeof(application_id) <> 'text' OR application_id IS NOT $application COLLATE BINARY
                """, applicationId) != 0)
                throw new InvalidOperationException("Existing actor records belong to another application.");
        if (ReadLong(connection, transaction, "SELECT COUNT(*) FROM pragma_foreign_key_check") != 0)
            throw new InvalidOperationException("Existing actor records violate foreign keys.");
    }

    private static void EnsureReady(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (connection.State != ConnectionState.Open)
            throw new InvalidOperationException("The caller must supply an open connection.");
        EnsureSettings(connection, null);
    }

    private static void EnsureSettings(SqliteConnection connection, SqliteTransaction? transaction)
    {
        if (ReadLong(connection, transaction, "PRAGMA foreign_keys") != 1)
            throw new InvalidOperationException("SQLite foreign key enforcement must be enabled.");
        var journal = Command(connection, transaction, "PRAGMA journal_mode", null, command => command.ExecuteScalar());
        if (!string.Equals(Convert.ToString(journal, CultureInfo.InvariantCulture), "delete", StringComparison.OrdinalIgnoreCase) ||
            ReadLong(connection, transaction, "PRAGMA synchronous") != 2)
            throw new InvalidOperationException("SQLite DELETE journal and FULL synchronous settings are required.");
    }

    private static long ReadLong(SqliteConnection connection, SqliteTransaction? transaction, string sql, string? applicationId = null)
    {
        var result = Command(connection, transaction, sql, applicationId, command => command.ExecuteScalar());
        return result is long value ? value : throw new InvalidOperationException("Unexpected SQLite result.");
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql, string? applicationId = null) =>
        Command(connection, transaction, sql, applicationId, command => command.ExecuteNonQuery());

    private static T Command<T>(SqliteConnection connection, SqliteTransaction? transaction, string sql,
        string? applicationId, Func<SqliteCommand, T> action)
    {
        var command = connection.CreateCommand();
        T result = default!;
        Exception? failure = null;
        try
        {
            command.Transaction = transaction;
            command.CommandText = sql;
            if (applicationId is not null) command.Parameters.AddWithValue("$application", applicationId);
            result = action(command);
        }
        catch (Exception error) { failure = error; }
        try { command.Dispose(); }
        catch (Exception disposeError)
        {
            failure = failure is null ? disposeError : new AggregateException("Command and disposal failed.", failure, disposeError);
        }
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        return result;
    }

    private static void ThrowFailures(Exception? failure, Exception? rollbackFailure, Exception? disposeFailure)
    {
        if (failure is not null && rollbackFailure is null && disposeFailure is null)
            ExceptionDispatchInfo.Capture(failure).Throw();
        if (failure is null && disposeFailure is not null)
            ExceptionDispatchInfo.Capture(disposeFailure).Throw();
        if (failure is not null)
        {
            var errors = new List<Exception> { failure };
            if (rollbackFailure is not null) errors.Add(rollbackFailure);
            if (disposeFailure is not null) errors.Add(disposeFailure);
            throw new AggregateException("Binding failed; rollback or disposal also failed.", errors);
        }
    }
}
