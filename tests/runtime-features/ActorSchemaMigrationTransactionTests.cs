using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using DSharp.Runtime;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class ActorSchemaMigrationTransactionTests
{
    private static ActorSchemaMigrationCoordinator Coordinator() => new([
        new ActorSchemaMigration(1, ["first"]),
        new ActorSchemaMigration(2, ["second"]) ]);

    [Fact]
    public void CallerOwnsCommitRollbackAndDisposal()
    {
        using var connection = new FakeConnection();
        using var transaction = connection.BeginTransaction();
        Coordinator().ApplyWithinTransaction(connection, transaction);
        Assert.Equal(2, connection.StagedVersion);
        Assert.Equal(new[] { "first", "second" }, connection.Statements);
        Assert.Equal(0, connection.Commits);
        Assert.Equal(0, connection.Rollbacks);
        Assert.Equal(0, connection.TransactionDisposals);
        transaction.Rollback();
        Assert.Equal(1, connection.Rollbacks);
        Assert.Equal(0, connection.CommittedVersion);
        Assert.Equal(ConnectionState.Open, connection.State);
    }

    [Fact]
    public void CallerCanCommitThePreparedMigration()
    {
        using var connection = new FakeConnection();
        using var transaction = connection.BeginTransaction();
        Coordinator().ApplyWithinTransaction(connection, transaction);
        Assert.Equal(0, connection.CommittedVersion);
        Assert.Equal(0, connection.Commits);
        transaction.Commit();
        Assert.Equal(1, connection.Commits);
        Assert.Equal(2, connection.CommittedVersion);
        Assert.Equal(0, connection.Rollbacks);
    }

    [Fact]
    public void FailureLeavesRollbackToCallerAndMismatchRunsNothing()
    {
        using var connection = new FakeConnection { FailAt = "second" };
        using var transaction = connection.BeginTransaction();
        Assert.Throws<InvalidOperationException>(() => Coordinator().ApplyWithinTransaction(connection, transaction));
        Assert.Equal(0, connection.Commits);
        Assert.Equal(0, connection.Rollbacks);
        Assert.Equal(0, connection.TransactionDisposals);
        transaction.Rollback();
        Assert.Equal(1, connection.Rollbacks);
        Assert.Equal(0, connection.CommittedVersion);

        using var other = new FakeConnection();
        using var foreign = other.BeginTransaction();
        Assert.Throws<InvalidOperationException>(() => Coordinator().ApplyWithinTransaction(connection, foreign));
        Assert.Empty(other.Statements);
        Assert.Equal(0, other.Commits);
        Assert.Equal(0, other.Rollbacks);
        Assert.Equal(0, other.TransactionDisposals);

        foreign.Dispose();
        Assert.Throws<InvalidOperationException>(() => Coordinator().ApplyWithinTransaction(other, foreign));
        Assert.Empty(other.Statements);
    }

    private sealed class FakeConnection : DbConnection
    {
        private ConnectionState state = ConnectionState.Open;
        private string connectionString = "fake";
        internal int StagedVersion { get; set; }
        internal int CommittedVersion { get; set; }
        internal int Commits { get; set; }
        internal int Rollbacks { get; set; }
        internal int TransactionDisposals { get; set; }
        internal string? FailAt { get; init; }
        internal List<string> Statements { get; } = [];
        [AllowNull] public override string ConnectionString { get => connectionString; set => connectionString = value ?? string.Empty; }
        public override string Database => "fake";
        public override string DataSource => "fake";
        public override string ServerVersion => "fake";
        public override ConnectionState State => state;
        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();
        public override void Close() => state = ConnectionState.Closed;
        public override void Open() => state = ConnectionState.Open;
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => new FakeTransaction(this, isolationLevel);
        protected override DbCommand CreateDbCommand() => new FakeCommand(this);
    }

    private sealed class FakeTransaction(FakeConnection connection, IsolationLevel level) : DbTransaction
    {
        private bool disposed;
        public override IsolationLevel IsolationLevel => level;
        protected override DbConnection? DbConnection => disposed ? null : connection;
        public override void Commit() { connection.Commits++; connection.CommittedVersion = connection.StagedVersion; }
        public override void Rollback() { connection.Rollbacks++; connection.StagedVersion = connection.CommittedVersion; }
        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposed) { disposed = true; connection.TransactionDisposals++; }
            base.Dispose(disposing);
        }
    }

    private sealed class FakeCommand(FakeConnection connection) : DbCommand
    {
        private string text = string.Empty;
        private DbConnection? owner = connection;
        private DbTransaction? transaction;
        [AllowNull] public override string CommandText { get => text; set => text = value ?? string.Empty; }
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get => owner; set => owner = value; }
        protected override DbTransaction? DbTransaction { get => transaction; set => transaction = value; }
        protected override DbParameterCollection DbParameterCollection => throw new NotSupportedException();
        public override void Cancel() => throw new NotSupportedException();
        public override void Prepare() => throw new NotSupportedException();
        protected override DbParameter CreateDbParameter() => throw new NotSupportedException();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
        public override object ExecuteScalar()
        {
            RequireOwner();
            if (text == "PRAGMA user_version") return (long)connection.StagedVersion;
            if (text.Contains("sqlite_master", StringComparison.Ordinal)) return 0L;
            throw new InvalidOperationException("Unexpected scalar.");
        }
        public override int ExecuteNonQuery()
        {
            RequireOwner();
            if (text == connection.FailAt) throw new InvalidOperationException("Injected statement failure.");
            if (text.StartsWith("PRAGMA user_version = ", StringComparison.Ordinal))
                connection.StagedVersion = int.Parse(text[(text.IndexOf('=') + 1)..].Trim(), System.Globalization.CultureInfo.InvariantCulture);
            else connection.Statements.Add(text);
            return 1;
        }
        private void RequireOwner()
        {
            if (!ReferenceEquals(owner, connection) || !ReferenceEquals(transaction?.Connection, connection))
                throw new InvalidOperationException("Command must use the caller's transaction.");
        }
    }
}
