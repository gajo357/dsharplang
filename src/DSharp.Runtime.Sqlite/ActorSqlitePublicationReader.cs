namespace DSharp.Runtime.Sqlite;

// Stored publication data only; this does not accept or deliver a publication.
internal sealed class ActorSqlitePublicationSnapshot
{
    private readonly byte[] contract;
    private readonly byte[] payload;
    private readonly byte[] options;

    internal ActorSqlitePublicationSnapshot(string publicationId, string submissionId,
        byte[] contract, byte[] payload, byte[] options)
    {
        PublicationId = publicationId;
        SubmissionId = submissionId;
        this.contract = (byte[])contract.Clone();
        this.payload = (byte[])payload.Clone();
        this.options = (byte[])options.Clone();
    }

    internal string PublicationId { get; }
    internal string SubmissionId { get; }
    internal byte[] Contract => (byte[])contract.Clone();
    internal byte[] Payload => (byte[])payload.Clone();
    internal byte[] Options => (byte[])options.Clone();
}

// The selected identity is the persisted choice, not a fresh registry lookup.
internal sealed record ActorSqliteDeliverySnapshot(string PublicationId, string SubscriptionId,
    string RequestedActorId, string SelectedActorId);

internal static class ActorSqlitePublicationReader
{
    // Both methods open an existing application-bound file. Each call has its
    // own SQLite snapshot; separate calls do not form one atomic view.
    internal static ActorSqlitePublicationSnapshot? ReadPublication(string path, string applicationId, string publicationId)
    {
        ArgumentException.ThrowIfNullOrEmpty(publicationId);
        using var connection = ActorSqliteDatabase.OpenBound(path, applicationId);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.publication_id, s.submission_id, s.contract, s.payload, s.options
            FROM actor_publications AS p
            JOIN actor_submissions AS s ON s.application_id = p.application_id AND s.submission_id = p.submission_id
            WHERE p.application_id = $application AND p.publication_id = $publication
            """;
        command.Parameters.AddWithValue("$application", applicationId);
        command.Parameters.AddWithValue("$publication", publicationId);
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;
        return new ActorSqlitePublicationSnapshot(reader.GetString(0), reader.GetString(1),
            reader.GetFieldValue<byte[]>(2), reader.GetFieldValue<byte[]>(3), reader.GetFieldValue<byte[]>(4));
    }

    internal static ActorSqliteDeliverySnapshot? ReadDelivery(string path, string applicationId,
        string publicationId, string subscriptionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(publicationId);
        ArgumentException.ThrowIfNullOrEmpty(subscriptionId);
        using var connection = ActorSqliteDatabase.OpenBound(path, applicationId);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT publication_id, subscription_id, requested_actor_id, selected_actor_id
            FROM actor_deliveries
            WHERE application_id = $application AND publication_id = $publication AND subscription_id = $subscription
            """;
        command.Parameters.AddWithValue("$application", applicationId);
        command.Parameters.AddWithValue("$publication", publicationId);
        command.Parameters.AddWithValue("$subscription", subscriptionId);
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;
        return new ActorSqliteDeliverySnapshot(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3));
    }
}
