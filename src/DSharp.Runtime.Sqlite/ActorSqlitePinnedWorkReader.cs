namespace DSharp.Runtime.Sqlite;

// A snapshot of stored identity and encoded input, not a claim or a decision
// that the selected implementation is currently available or compatible.
internal sealed class ActorSqlitePinnedWork
{
    private readonly byte[] contract;
    private readonly byte[] payload;
    private readonly byte[] options;
    private readonly byte[]? targetKey;

    internal ActorSqlitePinnedWork(string invocationId, string submissionId, string? publicationId,
        string? subscriptionId, string requestedActorId, string selectedActorId, string operation,
        string? parentInvocationId, string? parentAttemptId, byte[] contract, byte[] payload,
        byte[] options, byte[]? targetKey)
    {
        InvocationId = invocationId;
        SubmissionId = submissionId;
        PublicationId = publicationId;
        SubscriptionId = subscriptionId;
        RequestedActorId = requestedActorId;
        SelectedActorId = selectedActorId;
        Operation = operation;
        ParentInvocationId = parentInvocationId;
        ParentAttemptId = parentAttemptId;
        this.contract = (byte[])contract.Clone();
        this.payload = (byte[])payload.Clone();
        this.options = (byte[])options.Clone();
        this.targetKey = targetKey is null ? null : (byte[])targetKey.Clone();
    }

    internal string InvocationId { get; }
    internal string SubmissionId { get; }
    internal string? PublicationId { get; }
    internal string? SubscriptionId { get; }
    internal string RequestedActorId { get; }
    internal string SelectedActorId { get; }
    internal string Operation { get; }
    // These are persisted values; validating or following ancestry is outside
    // this reader's scope.
    internal string? ParentInvocationId { get; }
    internal string? ParentAttemptId { get; }
    internal byte[] Contract => (byte[])contract.Clone();
    internal byte[] Payload => (byte[])payload.Clone();
    internal byte[] Options => (byte[])options.Clone();
    internal byte[]? TargetKey => targetKey is null ? null : (byte[])targetKey.Clone();
}

internal static class ActorSqlitePinnedWorkReader
{
    // One query observes one SQLite snapshot. The bound opener does not create
    // a missing file; callers own host path selection and provenance.
    internal static ActorSqlitePinnedWork? Read(string path, string applicationId, string invocationId)
    {
        ArgumentException.ThrowIfNullOrEmpty(invocationId);
        using var connection = ActorSqliteDatabase.OpenBound(path, applicationId);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT i.invocation_id, s.submission_id, i.source_publication_id, i.source_subscription_id,
                   i.requested_actor_id, i.selected_actor_id, i.operation, i.parent_invocation_id,
                   i.parent_attempt_id, s.contract, s.payload, s.options, i.target_key
            FROM actor_invocations AS i
            LEFT JOIN actor_publications AS p
              ON p.application_id = i.application_id AND p.publication_id = i.source_publication_id
            JOIN actor_submissions AS s
              ON s.application_id = i.application_id AND s.submission_id = COALESCE(i.submission_id, p.submission_id)
            WHERE i.application_id = $application AND i.invocation_id = $invocation
            """;
        command.Parameters.AddWithValue("$application", applicationId);
        command.Parameters.AddWithValue("$invocation", invocationId);
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;
        return new ActorSqlitePinnedWork(reader.GetString(0), reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetString(4), reader.GetString(5), reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.GetFieldValue<byte[]>(9), reader.GetFieldValue<byte[]>(10), reader.GetFieldValue<byte[]>(11),
            reader.IsDBNull(12) ? null : reader.GetFieldValue<byte[]>(12));
    }
}
