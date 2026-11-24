using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

// This fake checks coordinator calls and transaction association. It is not a
// SQLite engine and cannot establish SQL validity, physical rollback or durability.
public sealed class ActorSchemaMigrationCoordinatorTests
{
    private static ActorSchemaMigrationCoordinator Coordinator() => new([
        new ActorSchemaMigration(1, ["migration-1-a", "migration-1-b"]),
        new ActorSchemaMigration(2, ["migration-2-a"]) ]);

    [Fact]
    public void FreshUpgradeAndCurrentVersionUseOneTransactionAndOnlyNeededStatements()
    {
        using var fresh = new FakeConnection();
        Coordinator().Apply(fresh);
        Assert.Equal(2, fresh.CommittedVersion);
        Assert.Equal(new[] { "migration-1-a", "migration-1-b", "migration-2-a" }, fresh.CommittedStatements);
        Assert.Equal(new[] { "begin", "read-version", "read-objects", "ddl:migration-1-a", "ddl:migration-1-b", "ddl:migration-2-a", "version:2", "commit" }, fresh.ActivityLog);
        Assert.Equal(new[] { "PRAGMA user_version", "SELECT COUNT(*) FROM sqlite_master WHERE substr(lower(name), 1, 7) <> 'sqlite_'" }, fresh.ScalarStatements);
        Assert.True(fresh.AllCommandsDisposed);
        Assert.True(fresh.LastTransaction!.Disposed);
        Assert.False(fresh.ConnectionDisposed);
        Assert.Equal(ConnectionState.Open, fresh.State);

        using var upgrade = new FakeConnection(version: 1);
        Coordinator().Apply(upgrade);
        Assert.Equal(2, upgrade.CommittedVersion);
        Assert.Equal(new[] { "migration-2-a" }, upgrade.CommittedStatements);
        Assert.Equal(new[] { "begin", "read-version", "ddl:migration-2-a", "version:2", "commit" }, upgrade.ActivityLog);
        using var current = new FakeConnection(version: 2);
        Coordinator().Apply(current);
        Assert.Equal(new[] { "begin", "read-version", "commit" }, current.ActivityLog);
        Assert.Equal(2, current.CommittedVersion);
        Assert.Empty(current.CommittedStatements);
    }

    [Fact]
    public void UnsupportedCorruptAndNonemptyUnversionedDatabasesFailBeforeWrites()
    {
        foreach (var connection in new[]
        {
            new FakeConnection(version: -1), new FakeConnection(version: 3),
            new FakeConnection(version: 0, nonInternalObjects: 1),
            new FakeConnection(versionValue: "not-a-version")
        })
        {
            using (connection)
            {
                var before = connection.CommittedVersion;
                Assert.Throws<InvalidOperationException>(() => Coordinator().Apply(connection));
                Assert.Equal(before, connection.CommittedVersion);
                Assert.Empty(connection.CommittedStatements);
                Assert.DoesNotContain(connection.ActivityLog, item => item.StartsWith("ddl:", StringComparison.Ordinal) || item.StartsWith("version:", StringComparison.Ordinal));
                Assert.Contains("rollback", connection.ActivityLog);
                Assert.DoesNotContain("commit", connection.ActivityLog);
                Assert.True(connection.AllCommandsDisposed);
                Assert.True(connection.LastTransaction!.Disposed);
                Assert.False(connection.ConnectionDisposed);
            }
        }
    }

    [Fact]
    public void ReadStatementVersionAndCommitFailuresRollbackAndDoNotReturnSuccess()
    {
        foreach (var stage in new[] { "read-version", "read-objects", "ddl:migration-1-b", "version:2", "commit" })
        {
            using var connection = new FakeConnection { FailureAt = stage };
            var error = Assert.Throws<InjectedDatabaseFailure>(() => Coordinator().Apply(connection));
            Assert.Equal(stage, error.Stage);
            Assert.Equal(0, connection.CommittedVersion);
            Assert.Empty(connection.CommittedStatements);
            Assert.Contains("rollback", connection.ActivityLog);
            Assert.True(connection.AllCommandsDisposed);
            Assert.True(connection.LastTransaction!.Disposed);
            Assert.False(connection.ConnectionDisposed);
            Assert.Equal(ConnectionState.Open, connection.State);
        }
    }

    [Fact]
    public void PrimaryAndRollbackFailuresRemainSeparatelyObservable()
    {
        using var connection = new FakeConnection { FailureAt = "ddl:migration-1-b", FailRollback = true };
        var error = Assert.Throws<AggregateException>(() => Coordinator().Apply(connection));
        Assert.Equal(2, error.InnerExceptions.Count);
        Assert.Equal("ddl:migration-1-b", Assert.IsType<InjectedDatabaseFailure>(error.InnerExceptions[0]).Stage);
        Assert.Equal("rollback", Assert.IsType<InjectedDatabaseFailure>(error.InnerExceptions[1]).Stage);
        Assert.Equal(0, connection.CommittedVersion);
        Assert.Empty(connection.CommittedStatements);
        Assert.True(connection.AllCommandsDisposed);
        Assert.True(connection.LastTransaction!.Disposed);
        Assert.False(connection.ConnectionDisposed);
    }

    [Fact]
    public void PostCommitFailuresAreUncertainAndDoNotEraseCommittedState()
    {
        using var lostAcknowledgement = new FakeConnection { FailAfterCommit = true };
        var lost = Assert.Throws<InjectedDatabaseFailure>(() => Coordinator().Apply(lostAcknowledgement));
        Assert.Equal("commit-after", lost.Stage);
        Assert.Equal(2, lostAcknowledgement.CommittedVersion);
        Assert.Equal(new[] { "migration-1-a", "migration-1-b", "migration-2-a" }, lostAcknowledgement.CommittedStatements);
        Assert.Contains("rollback", lostAcknowledgement.ActivityLog);
        lostAcknowledgement.FailAfterCommit = false;
        Coordinator().Apply(lostAcknowledgement);
        Assert.Equal(2, lostAcknowledgement.CommittedVersion);
        Assert.Equal(new[] { "migration-1-a", "migration-1-b", "migration-2-a" }, lostAcknowledgement.CommittedStatements);

        using var disposeFailure = new FakeConnection { FailTransactionDispose = true };
        var disposed = Assert.Throws<InjectedDatabaseFailure>(() => Coordinator().Apply(disposeFailure));
        Assert.Equal("transaction-dispose", disposed.Stage);
        Assert.Equal(2, disposeFailure.CommittedVersion);
        Assert.Equal(new[] { "migration-1-a", "migration-1-b", "migration-2-a" }, disposeFailure.CommittedStatements);
        Assert.DoesNotContain("rollback", disposeFailure.ActivityLog);
        Assert.False(disposeFailure.ConnectionDisposed);
    }

    [Fact]
    public void CommandDisposalFailureIsPreservedAlongsideExecutionFailure()
    {
        using var connection = new FakeConnection
        {
            FailureAt = "ddl:migration-1-b",
            FailCommandDisposeAt = "migration-1-b"
        };
        var error = Assert.Throws<AggregateException>(() => Coordinator().Apply(connection));
        Assert.Equal(2, error.InnerExceptions.Count);
        Assert.Equal("ddl:migration-1-b", Assert.IsType<InjectedDatabaseFailure>(error.InnerExceptions[0]).Stage);
        Assert.Equal("command-dispose", Assert.IsType<InjectedDatabaseFailure>(error.InnerExceptions[1]).Stage);
        Assert.Contains("rollback", connection.ActivityLog);
        Assert.Equal(0, connection.CommittedVersion);
        Assert.Empty(connection.CommittedStatements);
    }

    [Fact]
    public void MigrationDescriptionsAreValidatedAndSnapshottedBeforeRunning()
    {
        Assert.ThrowsAny<ArgumentException>(() => new ActorSchemaMigration(0, ["statement"]));
        Assert.ThrowsAny<ArgumentException>(() => new ActorSchemaMigration(1, []));
        Assert.ThrowsAny<ArgumentException>(() => new ActorSchemaMigration(1, [" "]));
        Assert.Throws<ArgumentNullException>(() => new ActorSchemaMigration(1, null!));
        Assert.ThrowsAny<ArgumentException>(() => new ActorSchemaMigrationCoordinator([]));
        Assert.ThrowsAny<ArgumentException>(() => new ActorSchemaMigrationCoordinator([new ActorSchemaMigration(2, ["statement"])]));
        Assert.ThrowsAny<ArgumentException>(() => new ActorSchemaMigrationCoordinator([
            new ActorSchemaMigration(1, ["first"]), new ActorSchemaMigration(1, ["duplicate"]) ]));
        Assert.ThrowsAny<ArgumentException>(() => new ActorSchemaMigrationCoordinator([
            new ActorSchemaMigration(1, ["first"]), new ActorSchemaMigration(3, ["gap"]) ]));
        Assert.Throws<ArgumentNullException>(() => new ActorSchemaMigrationCoordinator(null!));
        string[] statements = ["original-a", "original-b"];
        var descriptions = new List<ActorSchemaMigration> { new(1, statements) };
        var coordinator = new ActorSchemaMigrationCoordinator(descriptions);
        statements[0] = "changed";
        descriptions.Clear();
        using var connection = new FakeConnection();
        coordinator.Apply(connection);
        Assert.Equal(new[] { "original-a", "original-b" }, connection.CommittedStatements);
        Assert.Equal(1, connection.CommittedVersion);
    }

    [Fact]
    public void RequiresAlreadyOpenCallerOwnedConnection()
    {
        Assert.Throws<ArgumentNullException>(() => Coordinator().Apply(null!));
        using var closed = new FakeConnection(open: false);
        Assert.Throws<InvalidOperationException>(() => Coordinator().Apply(closed));
        Assert.Empty(closed.ActivityLog);
        Assert.False(closed.ConnectionDisposed);
        using var open = new FakeConnection();
        Coordinator().Apply(open);
        Assert.False(open.ConnectionDisposed);
        Assert.Equal(ConnectionState.Open, open.State);
    }

    private sealed class InjectedDatabaseFailure(string stage) : Exception(stage)
    {
        public string Stage { get; } = stage;
    }

    private sealed class FakeConnection : DbConnection
    {
        private ConnectionState state;
        private readonly object? versionValue;
        private readonly long nonInternalObjects;
        private readonly List<FakeCommand> commands = [];
        private string connectionString = "fake";
        public int CommittedVersion { get; private set; }
        public List<string> CommittedStatements { get; } = [];
        public List<string> ActivityLog { get; } = [];
        public List<string> ScalarStatements { get; } = [];
        public string? FailureAt { get; init; }
        public bool FailRollback { get; init; }
        public bool FailAfterCommit { get; set; }
        public bool FailTransactionDispose { get; init; }
        public string? FailCommandDisposeAt { get; init; }
        public bool ConnectionDisposed { get; private set; }
        public FakeTransaction? LastTransaction { get; private set; }
        public bool AllCommandsDisposed => commands.All(command => command.CommandDisposed);

        public FakeConnection(int version = 0, long nonInternalObjects = 0, bool open = true, object? versionValue = null)
        {
            CommittedVersion = version;
            this.nonInternalObjects = nonInternalObjects;
            this.versionValue = versionValue;
            state = open ? ConnectionState.Open : ConnectionState.Closed;
        }

        [AllowNull] public override string ConnectionString { get => connectionString; set => connectionString = value ?? string.Empty; }
        public override string Database => "fake";
        public override string DataSource => "fake";
        public override string ServerVersion => "fake";
        public override ConnectionState State => state;
        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();
        public override void Close() => state = ConnectionState.Closed;
        public override void Open() => state = ConnectionState.Open;
        protected override void Dispose(bool disposing) { if (disposing) ConnectionDisposed = true; base.Dispose(disposing); }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            if (state != ConnectionState.Open) throw new InvalidOperationException("Connection must be open.");
            ActivityLog.Add("begin");
            LastTransaction = new FakeTransaction(this, isolationLevel, CommittedVersion);
            return LastTransaction;
        }

        protected override DbCommand CreateDbCommand()
        {
            var result = new FakeCommand(this);
            commands.Add(result);
            return result;
        }

        public object Read(FakeCommand command)
        {
            var transaction = RequireTransaction(command);
            ScalarStatements.Add(command.CommandText);
            if (command.CommandText.Contains("sqlite_master", StringComparison.OrdinalIgnoreCase))
            {
                Record("read-objects");
                return nonInternalObjects;
            }
            if (command.CommandText.Trim().Equals("PRAGMA user_version", StringComparison.OrdinalIgnoreCase))
            {
                Record("read-version");
                return versionValue ?? (long)transaction.StagedVersion;
            }
            throw new InvalidOperationException($"Unexpected scalar: {command.CommandText}");
        }

        public int Execute(FakeCommand command)
        {
            var transaction = RequireTransaction(command);
            var sql = command.CommandText.Trim();
            if (sql.StartsWith("PRAGMA user_version = ", StringComparison.OrdinalIgnoreCase))
            {
                var value = int.Parse(sql[(sql.IndexOf('=') + 1)..].Trim().TrimEnd(';'), System.Globalization.CultureInfo.InvariantCulture);
                Record($"version:{value}");
                transaction.StagedVersion = value;
            }
            else
            {
                Record($"ddl:{sql}");
                transaction.StagedStatements.Add(sql);
            }
            return 1;
        }

        private FakeTransaction RequireTransaction(FakeCommand command)
        {
            if (!ReferenceEquals(command.ConnectionValue, this) || LastTransaction is null ||
                !ReferenceEquals(command.TransactionValue, LastTransaction) || LastTransaction.Disposed)
                throw new InvalidOperationException("Every command must use the active caller connection and transaction.");
            return LastTransaction;
        }

        public void Record(string stage)
        {
            ActivityLog.Add(stage);
            if (FailureAt == stage || (stage == "rollback" && FailRollback)) throw new InjectedDatabaseFailure(stage);
        }

        public void Commit(FakeTransaction transaction)
        {
            Record("commit");
            CommittedVersion = transaction.StagedVersion;
            CommittedStatements.AddRange(transaction.StagedStatements);
            if (FailAfterCommit) throw new InjectedDatabaseFailure("commit-after");
        }

        public void DisposeTransaction()
        {
            if (FailTransactionDispose) throw new InjectedDatabaseFailure("transaction-dispose");
        }

        public void DisposeCommand(string sql)
        {
            if (FailCommandDisposeAt == sql) throw new InjectedDatabaseFailure("command-dispose");
        }
    }

    private sealed class FakeTransaction(FakeConnection connection, IsolationLevel isolationLevel, int version) : DbTransaction
    {
        public int StagedVersion { get; set; } = version;
        public List<string> StagedStatements { get; } = [];
        public bool Disposed { get; private set; }
        public override IsolationLevel IsolationLevel => isolationLevel;
        protected override DbConnection DbConnection => connection;
        public override void Commit() => connection.Commit(this);
        public override void Rollback() => connection.Record("rollback");
        protected override void Dispose(bool disposing)
        {
            if (disposing) { Disposed = true; connection.DisposeTransaction(); }
            base.Dispose(disposing);
        }
    }

    private sealed class FakeCommand(FakeConnection connection) : DbCommand
    {
        private string commandText = string.Empty;
        public bool CommandDisposed { get; private set; }
        public DbConnection? ConnectionValue { get; private set; } = connection;
        public DbTransaction? TransactionValue { get; private set; }
        [AllowNull] public override string CommandText { get => commandText; set => commandText = value ?? string.Empty; }
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get => ConnectionValue; set => ConnectionValue = value; }
        protected override DbTransaction? DbTransaction { get => TransactionValue; set => TransactionValue = value; }
        protected override DbParameterCollection DbParameterCollection => throw new NotSupportedException();
        public override void Cancel() => throw new NotSupportedException();
        public override int ExecuteNonQuery() => connection.Execute(this);
        public override object ExecuteScalar() => connection.Read(this);
        public override void Prepare() => throw new NotSupportedException();
        protected override DbParameter CreateDbParameter() => throw new NotSupportedException();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing) { CommandDisposed = true; connection.DisposeCommand(CommandText); }
            base.Dispose(disposing);
        }
    }
}
