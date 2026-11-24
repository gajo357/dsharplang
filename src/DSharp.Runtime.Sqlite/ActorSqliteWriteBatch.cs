using System.Data;
using System.Runtime.ExceptionServices;
using Microsoft.Data.Sqlite;

namespace DSharp.Runtime.Sqlite;

// These definitions accept trusted, transaction-compatible, single SQL
// statements from internal runtime code only. They do not parse SQL and are
// not an interface for application-supplied statements. Transaction control,
// ATTACH, transaction-changing PRAGMAs and external effects (including through
// triggers or registered SQLite functions) are not permitted.
internal sealed class ActorSqliteWriteParameter
{
    private readonly object? value;

    internal ActorSqliteWriteParameter(string name, object? value)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!IsValidName(name))
            throw new ArgumentException("A named SQLite parameter is required.", nameof(name));

        Name = name;
        this.value = value switch
        {
            null => null,
            string text => text,
            int number => number,
            long number => number,
            byte[] bytes => (byte[])bytes.Clone(),
            _ => throw new ArgumentException("Unsupported SQLite parameter value.", nameof(value))
        };
    }

    internal string Name { get; }

    // Do not expose the stored BLOB even to another internal command owner.
    internal void Bind(SqliteCommand command) => command.Parameters.AddWithValue(Name,
        value is byte[] bytes ? (byte[])bytes.Clone() : value ?? DBNull.Value);

    private static bool IsValidName(string name)
    {
        if (name.Length < 2 || name[0] is not ('@' or '$' or ':') || !IsIdentifierStart(name[1]))
            return false;
        for (var index = 2; index < name.Length; index++)
            if (!IsIdentifierStart(name[index]) && name[index] is not (>= '0' and <= '9'))
                return false;
        return true;
    }

    private static bool IsIdentifierStart(char value) =>
        value is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_';
}

internal sealed class ActorSqliteWriteCommand
{
    private readonly ActorSqliteWriteParameter[] parameters;

    internal ActorSqliteWriteCommand(string sql, IEnumerable<ActorSqliteWriteParameter> parameters,
        int? expectedAffectedRows = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentNullException.ThrowIfNull(parameters);
        if (sql.Contains('\0'))
            throw new ArgumentException("SQL cannot contain a NUL character.", nameof(sql));
        if (expectedAffectedRows < 0)
            throw new ArgumentOutOfRangeException(nameof(expectedAffectedRows));

        var copied = new List<ActorSqliteWriteParameter>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in parameters)
        {
            if (parameter is null)
                throw new ArgumentException("Parameters cannot contain null entries.", nameof(parameters));
            if (!names.Add(parameter.Name))
                throw new ArgumentException("Parameter names must be unique within a command.", nameof(parameters));
            copied.Add(parameter);
        }
        Sql = sql;
        ExpectedAffectedRows = expectedAffectedRows;
        this.parameters = copied.ToArray();
    }

    internal string Sql { get; }
    internal int? ExpectedAffectedRows { get; }

    internal void Bind(SqliteCommand command)
    {
        foreach (var parameter in parameters)
            parameter.Bind(command);
    }
}

// A write-only primitive, not admission, fencing or durable acknowledgement.
// Constructors finish all caller enumeration and copy all mutable inputs before
// Execute can begin a transaction. The caller retains ownership of the open
// connection and must not use or reconfigure it concurrently with this batch.
internal sealed class ActorSqliteWriteBatch
{
    private readonly ActorSqliteWriteCommand[] commands;

    internal ActorSqliteWriteBatch(IEnumerable<ActorSqliteWriteCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        var copied = new List<ActorSqliteWriteCommand>();
        foreach (var command in commands)
        {
            if (command is null)
                throw new ArgumentException("Commands cannot contain null entries.", nameof(commands));
            copied.Add(command);
        }
        if (copied.Count == 0)
            throw new ArgumentException("A batch must contain at least one command.", nameof(commands));
        this.commands = copied.ToArray();
    }

    internal IReadOnlyList<int> Execute(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (connection.State != ConnectionState.Open)
            throw new InvalidOperationException("The caller must supply an open connection.");
        EnsureForeignKeys(connection);

        var counts = new int[commands.Length];
        var result = Array.AsReadOnly(counts);
        // BeginTransaction rejects an existing caller transaction; if it fails,
        // this helper has no transaction to roll back or dispose. Allocate the
        // result buffer first so every post-begin operation is cleanup-protected.
        var transaction = connection.BeginTransaction();
        Exception? failure = null;
        Exception? rollbackFailure = null;
        Exception? disposeFailure = null;
        try
        {
            for (var index = 0; index < commands.Length; index++)
            {
                counts[index] = ExecuteCommand(connection, transaction, commands[index]);
                if (commands[index].ExpectedAffectedRows is { } expected && counts[index] != expected)
                    throw new InvalidOperationException("The affected-row count did not match the expected count.");
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
            // Always attempt cleanup before allocating any aggregate exception.
            try { transaction.Dispose(); }
            catch (Exception disposeError) { disposeFailure = disposeError; }
        }
        if (failure is not null && rollbackFailure is null && disposeFailure is null)
            ExceptionDispatchInfo.Capture(failure).Throw();
        if (failure is null && disposeFailure is not null)
            ExceptionDispatchInfo.Capture(disposeFailure).Throw();
        if (failure is not null)
        {
            var errors = new List<Exception> { failure };
            if (rollbackFailure is not null) errors.Add(rollbackFailure);
            if (disposeFailure is not null) errors.Add(disposeFailure);
            throw new AggregateException("Batch failed; rollback or transaction disposal also failed.", errors);
        }
        return result;
    }

    private static void EnsureForeignKeys(SqliteConnection connection)
    {
        var command = connection.CreateCommand();
        Exception? failure = null;
        try
        {
            command.CommandText = "PRAGMA foreign_keys";
            if (command.ExecuteScalar() is not long value || value != 1)
                throw new InvalidOperationException("SQLite foreign key enforcement must be enabled on this connection.");
        }
        catch (Exception error) { failure = error; }
        try { command.Dispose(); }
        catch (Exception disposeError)
        {
            failure = failure is null ? disposeError :
                new AggregateException("Foreign-key check and command disposal failed.", failure, disposeError);
        }
        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static int ExecuteCommand(SqliteConnection connection, SqliteTransaction transaction,
        ActorSqliteWriteCommand statement)
    {
        var command = connection.CreateCommand();
        var count = 0;
        Exception? failure = null;
        try
        {
            command.Transaction = transaction;
            command.CommandText = statement.Sql;
            statement.Bind(command);
            count = command.ExecuteNonQuery();
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
        return count;
    }
}
