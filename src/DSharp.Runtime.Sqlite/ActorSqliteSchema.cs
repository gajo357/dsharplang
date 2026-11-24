using System.Data;
using Microsoft.Data.Sqlite;

namespace DSharp.Runtime.Sqlite;

// Internal version-one identity substrate only. It does not perform actor
// admission, delivery, claims, codecs, or durable acknowledgement.
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

    internal static void Apply(SqliteConnection connection) => ApplyMigration(connection, InitialStatements);

    // Only trusted, transaction-compatible statements may be passed. This is an
    // internal migration primitive, not an application-provided SQL interface.
    internal static void ApplyMigration(SqliteConnection connection, IReadOnlyList<string> statements)
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
        new ActorSchemaMigrationCoordinator([new ActorSchemaMigration(1, statements)]).Apply(connection);
    }
}
