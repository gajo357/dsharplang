using DSharp.Runtime.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DSharp.Runtime.Sqlite.Tests.Features;

public sealed class ActorSqlitePinnedWorkReaderTests
{
    [Fact]
    public void ReopenReadsOriginalPinnedTargetAndPublicationInputsWithoutReselection()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"dsharp-pinned-read-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "actor.db");
            const string app = "app'one";
            using (var db = ActorSqliteDatabase.OpenAndBind(path, app))
            {
                Execute(db, "INSERT INTO actor_submissions VALUES ($app,'target-sub','targeted',x'00FF',x'1122',x'33')", app);
                Execute(db, """
                    INSERT INTO actor_invocations(application_id,invocation_id,submission_id,requested_actor_id,selected_actor_id,
                      target_key,operation,parent_invocation_id,parent_attempt_id)
                    VALUES ($app,'target''id','target-sub','requested','selected',x'0042','invoke','parent','attempt')
                    """, app);
                Execute(db, "INSERT INTO actor_submissions VALUES ($app,'dispatch-sub','targeted',x'02',x'',x'')", app);
                Execute(db, """
                    INSERT INTO actor_invocations(application_id,invocation_id,submission_id,requested_actor_id,selected_actor_id,
                      target_key,operation) VALUES ($app,'dispatch','dispatch-sub','public','pinned',x'','dispatch')
                    """, app);
                Execute(db, "INSERT INTO actor_submissions VALUES ($app,'pub-sub','publication',x'44',x'5566',x'')", app);
                Execute(db, "INSERT INTO actor_publications(application_id,publication_id,submission_id) VALUES ($app,'pub','pub-sub')", app);
                Execute(db, "INSERT INTO actor_deliveries VALUES ($app,'pub','subscription','public','subscriber')", app);
                Execute(db, """
                    INSERT INTO actor_invocations(application_id,invocation_id,source_publication_id,source_subscription_id,
                      requested_actor_id,selected_actor_id,operation)
                    VALUES ($app,'delivery','pub','subscription','public','subscriber','invoke')
                    """, app);
                Execute(db, "INSERT INTO actor_submissions VALUES ($app,'empty-sub','publication',x'01',x'',x'')", app);
                Execute(db, "INSERT INTO actor_publications(application_id,publication_id,submission_id) VALUES ($app,'empty-pub','empty-sub')", app);
            }

            var targeted = Assert.IsType<ActorSqlitePinnedWork>(ActorSqlitePinnedWorkReader.Read(path, app, "target'id"));
            Assert.Equal("target'id", targeted.InvocationId);
            Assert.Equal("target-sub", targeted.SubmissionId);
            Assert.Null(targeted.PublicationId);
            Assert.Null(targeted.SubscriptionId);
            Assert.Equal("requested", targeted.RequestedActorId);
            Assert.Equal("selected", targeted.SelectedActorId);
            Assert.Equal("invoke", targeted.Operation);
            Assert.Equal("parent", targeted.ParentInvocationId);
            Assert.Equal("attempt", targeted.ParentAttemptId);
            Assert.Equal(new byte[] { 0, 255 }, targeted.Contract);
            Assert.Equal(new byte[] { 17, 34 }, targeted.Payload);
            Assert.Equal(new byte[] { 51 }, targeted.Options);
            Assert.Equal(new byte[] { 0, 66 }, targeted.TargetKey);
            var dispatch = Assert.IsType<ActorSqlitePinnedWork>(ActorSqlitePinnedWorkReader.Read(path, app, "dispatch"));
            Assert.Equal("dispatch", dispatch.Operation);
            Assert.Equal("pinned", dispatch.SelectedActorId);
            Assert.NotNull(dispatch.TargetKey);
            Assert.Empty(dispatch.TargetKey);
            Assert.Null(dispatch.ParentInvocationId);
            targeted.Contract[0] = 9;
            targeted.Payload[0] = 9;
            targeted.Options[0] = 9;
            targeted.TargetKey![0] = 9;
            Assert.Equal(new byte[] { 0, 255 }, targeted.Contract);
            Assert.Equal(new byte[] { 17, 34 }, targeted.Payload);
            Assert.Equal(new byte[] { 51 }, targeted.Options);
            Assert.Equal(new byte[] { 0, 66 }, targeted.TargetKey);
            Assert.Equal(new byte[] { 0, 255 }, ActorSqlitePinnedWorkReader.Read(path, app, "target'id")!.Contract);

            var delivery = Assert.IsType<ActorSqlitePinnedWork>(ActorSqlitePinnedWorkReader.Read(path, app, "delivery"));
            Assert.Equal("pub-sub", delivery.SubmissionId);
            Assert.Equal("pub", delivery.PublicationId);
            Assert.Equal("subscription", delivery.SubscriptionId);
            Assert.Equal("public", delivery.RequestedActorId);
            Assert.Equal("subscriber", delivery.SelectedActorId);
            Assert.Null(delivery.ParentInvocationId);
            Assert.Null(delivery.ParentAttemptId);
            Assert.Null(delivery.TargetKey);
            Assert.Equal(new byte[] { 68 }, delivery.Contract);
            Assert.Equal(new byte[] { 85, 102 }, delivery.Payload);
            Assert.Empty(delivery.Options);
            Assert.Null(ActorSqlitePinnedWorkReader.Read(path, app, "empty-pub"));
            Assert.Null(ActorSqlitePinnedWorkReader.Read(path, app, "missing' OR 1=1 --"));
            Assert.Throws<InvalidOperationException>(() => ActorSqlitePinnedWorkReader.Read(path, "App'one", "delivery"));
            var missingPath = Path.Combine(directory, "missing.db");
            Assert.Throws<SqliteException>(() => ActorSqlitePinnedWorkReader.Read(missingPath, app, "delivery"));
            Assert.False(File.Exists(missingPath));
            Assert.Throws<ArgumentException>(() => ActorSqlitePinnedWorkReader.Read(path, app, ""));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void Execute(SqliteConnection db, string sql, string applicationId)
    {
        using var command = db.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$app", applicationId);
        command.ExecuteNonQuery();
    }
}
