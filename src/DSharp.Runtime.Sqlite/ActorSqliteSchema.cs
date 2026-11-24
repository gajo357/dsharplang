using System.Data;
using Microsoft.Data.Sqlite;

namespace DSharp.Runtime.Sqlite;

// A target version is explicitly selected by each internal caller. V1 remains
// the compatibility default; newer files are rejected by a V1 caller.
internal enum ActorSqliteSchemaVersion { V1 = 1, V2 = 2, V3 = 3 }

// Internal schema substrate only. It does not perform actor admission,
// delivery, claims, codecs, fault normalization, or durable acknowledgement.
internal static class ActorSqliteSchema
{
    private static readonly string[] InitialStatements =
    [
        """
        CREATE TABLE actor_submissions (
          application_id TEXT NOT NULL CHECK (length(application_id) > 0),
          submission_id TEXT NOT NULL CHECK (length(submission_id) > 0),
          kind TEXT NOT NULL CHECK (kind IN ('targeted', 'publication')),
          contract BLOB NOT NULL CHECK (typeof(contract) = 'blob' AND length(contract) > 0),
          payload BLOB NOT NULL CHECK (typeof(payload) = 'blob'),
          options BLOB NOT NULL CHECK (typeof(options) = 'blob'),
          PRIMARY KEY (application_id, submission_id),
          UNIQUE (application_id, submission_id, kind)
        )
        """,
        """
        CREATE TABLE actor_invocations (
          application_id TEXT NOT NULL,
          invocation_id TEXT NOT NULL CHECK (length(invocation_id) > 0),
          submission_id TEXT,
          submission_kind TEXT NOT NULL DEFAULT 'targeted' CHECK (submission_kind = 'targeted'),
          source_publication_id TEXT,
          source_subscription_id TEXT,
          requested_actor_id TEXT NOT NULL CHECK (length(requested_actor_id) > 0),
          selected_actor_id TEXT NOT NULL CHECK (length(selected_actor_id) > 0),
          target_key BLOB CHECK (target_key IS NULL OR typeof(target_key) = 'blob'),
          operation TEXT NOT NULL CHECK (operation IN ('invoke', 'dispatch')),
          parent_invocation_id TEXT,
          parent_attempt_id TEXT,
          CHECK ((parent_invocation_id IS NULL) = (parent_attempt_id IS NULL)),
          CHECK (operation <> 'dispatch' OR parent_invocation_id IS NULL),
          CHECK (source_publication_id IS NULL OR parent_invocation_id IS NULL),
          CHECK ((submission_id IS NOT NULL AND source_publication_id IS NULL AND source_subscription_id IS NULL)
              OR (submission_id IS NULL AND source_publication_id IS NOT NULL AND source_subscription_id IS NOT NULL)),
          PRIMARY KEY (application_id, invocation_id),
          UNIQUE (application_id, submission_id),
          UNIQUE (application_id, source_publication_id, source_subscription_id),
          FOREIGN KEY (application_id, submission_id, submission_kind)
            REFERENCES actor_submissions (application_id, submission_id, kind),
          FOREIGN KEY (application_id, source_publication_id, source_subscription_id, requested_actor_id, selected_actor_id)
            REFERENCES actor_deliveries (application_id, publication_id, subscription_id, requested_actor_id, selected_actor_id)
        )
        """,
        """
        CREATE TABLE actor_attempts (
          application_id TEXT NOT NULL,
          invocation_id TEXT NOT NULL,
          attempt_id TEXT NOT NULL CHECK (length(attempt_id) > 0),
          PRIMARY KEY (application_id, attempt_id),
          UNIQUE (application_id, invocation_id, attempt_id),
          FOREIGN KEY (application_id, invocation_id)
            REFERENCES actor_invocations (application_id, invocation_id)
        )
        """,
        """
        CREATE TABLE actor_publications (
          application_id TEXT NOT NULL,
          publication_id TEXT NOT NULL CHECK (length(publication_id) > 0),
          submission_id TEXT NOT NULL,
          submission_kind TEXT NOT NULL DEFAULT 'publication' CHECK (submission_kind = 'publication'),
          PRIMARY KEY (application_id, publication_id),
          UNIQUE (application_id, submission_id),
          FOREIGN KEY (application_id, submission_id, submission_kind)
            REFERENCES actor_submissions (application_id, submission_id, kind)
        )
        """,
        """
        CREATE TABLE actor_deliveries (
          application_id TEXT NOT NULL,
          publication_id TEXT NOT NULL,
          subscription_id TEXT NOT NULL CHECK (length(subscription_id) > 0),
          requested_actor_id TEXT NOT NULL CHECK (length(requested_actor_id) > 0),
          selected_actor_id TEXT NOT NULL CHECK (length(selected_actor_id) > 0),
          PRIMARY KEY (application_id, publication_id, subscription_id),
          UNIQUE (application_id, publication_id, subscription_id, requested_actor_id, selected_actor_id),
          FOREIGN KEY (application_id, publication_id)
            REFERENCES actor_publications (application_id, publication_id)
        )
        """
    ];

    private static readonly string[] AttemptExitStatements =
    [
        """
        CREATE TABLE actor_attempt_exits (
          application_id TEXT NOT NULL,
          invocation_id TEXT NOT NULL,
          attempt_id TEXT NOT NULL,
          outcome_kind TEXT NOT NULL CHECK (outcome_kind IN ('normal', 'reported_fault')),
          payload BLOB NOT NULL CHECK (typeof(payload) = 'blob'),
          contract BLOB NOT NULL CHECK (typeof(contract) = 'blob' AND length(contract) > 0),
          origin_invocation_id TEXT CHECK (origin_invocation_id IS NULL OR (typeof(origin_invocation_id) = 'text' AND length(origin_invocation_id) > 0)),
          report_present INTEGER NOT NULL CHECK (typeof(report_present) = 'integer' AND report_present IN (0, 1)),
          report_origin_invocation_id TEXT CHECK (report_origin_invocation_id IS NULL OR (typeof(report_origin_invocation_id) = 'text' AND length(report_origin_invocation_id) > 0)),
          CHECK (outcome_kind = 'reported_fault' OR (origin_invocation_id IS NULL AND report_present = 0 AND report_origin_invocation_id IS NULL)),
          CHECK (report_present = 1 OR report_origin_invocation_id IS NULL),
          PRIMARY KEY (application_id, invocation_id, attempt_id),
          UNIQUE (application_id, invocation_id, attempt_id, outcome_kind, report_present),
          FOREIGN KEY (application_id, invocation_id, attempt_id)
            REFERENCES actor_attempts (application_id, invocation_id, attempt_id)
        )
        """,
        """
        CREATE TABLE actor_fault_observations (
          application_id TEXT NOT NULL,
          invocation_id TEXT NOT NULL,
          attempt_id TEXT NOT NULL,
          outcome_kind TEXT NOT NULL DEFAULT 'reported_fault' CHECK (outcome_kind = 'reported_fault'),
          report_present INTEGER NOT NULL DEFAULT 1 CHECK (typeof(report_present) = 'integer' AND report_present = 1),
          ordinal INTEGER NOT NULL CHECK (typeof(ordinal) = 'integer' AND ordinal >= -1),
          classification TEXT NOT NULL CHECK (typeof(classification) = 'text' AND length(classification) > 0),
          diagnostic BLOB NOT NULL CHECK (typeof(diagnostic) = 'blob'),
          metadata BLOB NOT NULL CHECK (typeof(metadata) = 'blob'),
          PRIMARY KEY (application_id, invocation_id, attempt_id, ordinal),
          FOREIGN KEY (application_id, invocation_id, attempt_id, outcome_kind, report_present)
            REFERENCES actor_attempt_exits (application_id, invocation_id, attempt_id, outcome_kind, report_present)
        )
        """
    ];

    // UTC timestamps use DateTimeOffset UTC ticks in this internal SQLite
    // representation. These rows do not validate a clock or execute transitions.
    private static readonly string[] LifecycleStatements =
    [
        """
        CREATE TABLE actor_invocation_lifecycle (
          application_id TEXT NOT NULL,
          invocation_id TEXT NOT NULL,
          state TEXT NOT NULL CHECK (state IN ('eligible', 'running', 'blocked', 'completed', 'failed_reported_fault', 'failed_lease_expired', 'cancelled')),
          eligibility_version INTEGER NOT NULL CHECK (typeof(eligibility_version) = 'integer' AND eligibility_version >= 0),
          eligible_at_utc_ticks INTEGER CHECK (eligible_at_utc_ticks IS NULL OR (typeof(eligible_at_utc_ticks) = 'integer' AND eligible_at_utc_ticks BETWEEN 0 AND 3155378975999999999)),
          cancellation_requested INTEGER NOT NULL CHECK (typeof(cancellation_requested) = 'integer' AND cancellation_requested IN (0, 1)),
          attempt_id TEXT CHECK (attempt_id IS NULL OR (typeof(attempt_id) = 'text' AND length(attempt_id) > 0)),
          token_id TEXT CHECK (token_id IS NULL OR (typeof(token_id) = 'text' AND length(token_id) > 0)),
          lease_until_utc_ticks INTEGER CHECK (lease_until_utc_ticks IS NULL OR (typeof(lease_until_utc_ticks) = 'integer' AND lease_until_utc_ticks BETWEEN 0 AND 3155378975999999999)),
          block_id TEXT CHECK (block_id IS NULL OR (typeof(block_id) = 'text' AND length(block_id) > 0)),
          block_reason TEXT CHECK (block_reason IS NULL OR block_reason IN ('implementation_unavailable', 'incompatible')),
          CHECK ((attempt_id IS NULL AND token_id IS NULL AND lease_until_utc_ticks IS NULL)
              OR (attempt_id IS NOT NULL AND token_id IS NOT NULL AND lease_until_utc_ticks IS NOT NULL)),
          CHECK ((block_id IS NULL AND block_reason IS NULL) OR (block_id IS NOT NULL AND block_reason IS NOT NULL)),
          PRIMARY KEY (application_id, invocation_id),
          FOREIGN KEY (application_id, invocation_id)
            REFERENCES actor_invocations (application_id, invocation_id),
          FOREIGN KEY (application_id, invocation_id, attempt_id)
            REFERENCES actor_attempts (application_id, invocation_id, attempt_id)
        )
        """,
        """
        CREATE TABLE actor_attempt_recoveries (
          application_id TEXT NOT NULL,
          invocation_id TEXT NOT NULL,
          ordinal INTEGER NOT NULL CHECK (typeof(ordinal) = 'integer' AND ordinal >= 0),
          attempt_id TEXT NOT NULL CHECK (typeof(attempt_id) = 'text' AND length(attempt_id) > 0),
          token_id TEXT NOT NULL CHECK (typeof(token_id) = 'text' AND length(token_id) > 0),
          lease_until_utc_ticks INTEGER NOT NULL CHECK (typeof(lease_until_utc_ticks) = 'integer' AND lease_until_utc_ticks BETWEEN 0 AND 3155378975999999999),
          eligible_at_utc_ticks INTEGER NOT NULL CHECK (typeof(eligible_at_utc_ticks) = 'integer' AND eligible_at_utc_ticks BETWEEN 0 AND 3155378975999999999),
          kind TEXT NOT NULL CHECK (kind IN ('reported_fault', 'lease_expired')),
          PRIMARY KEY (application_id, invocation_id, ordinal),
          FOREIGN KEY (application_id, invocation_id, attempt_id)
            REFERENCES actor_attempts (application_id, invocation_id, attempt_id)
        )
        """
    ];

    internal static void ValidateVersion(ActorSqliteSchemaVersion version)
    {
        if (version is not ActorSqliteSchemaVersion.V1 and not ActorSqliteSchemaVersion.V2 and not ActorSqliteSchemaVersion.V3)
            throw new ArgumentOutOfRangeException(nameof(version), version, "Unsupported SQLite schema target.");
    }

    internal static void Apply(SqliteConnection connection) => Apply(connection, ActorSqliteSchemaVersion.V1);

    internal static void Apply(SqliteConnection connection, ActorSqliteSchemaVersion version)
    {
        ValidateVersion(version);
        EnsureReady(connection);
        var migrations = version switch
        {
            ActorSqliteSchemaVersion.V1 => new[] { new ActorSchemaMigration(1, InitialStatements) },
            ActorSqliteSchemaVersion.V2 => new[]
            {
                new ActorSchemaMigration(1, InitialStatements),
                new ActorSchemaMigration(2, AttemptExitStatements)
            },
            ActorSqliteSchemaVersion.V3 => new[]
            {
                new ActorSchemaMigration(1, InitialStatements),
                new ActorSchemaMigration(2, AttemptExitStatements),
                new ActorSchemaMigration(3, LifecycleStatements)
            },
            _ => throw new ArgumentOutOfRangeException(nameof(version))
        };
        new ActorSchemaMigrationCoordinator(migrations).Apply(connection);
    }

    // Only trusted, transaction-compatible statements may be passed. This is an
    // internal migration primitive, not an application-provided SQL interface.
    internal static void ApplyMigration(SqliteConnection connection, IReadOnlyList<string> statements)
    {
        EnsureReady(connection);
        new ActorSchemaMigrationCoordinator([new ActorSchemaMigration(1, statements)]).Apply(connection);
    }

    private static void EnsureReady(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (connection.State != ConnectionState.Open)
            throw new InvalidOperationException("The caller must supply an open connection.");
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA foreign_keys";
            if (Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 1)
                throw new InvalidOperationException("SQLite foreign key enforcement must be enabled on this connection.");
        }
    }
}
