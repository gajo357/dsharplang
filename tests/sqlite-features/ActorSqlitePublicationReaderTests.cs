using DSharp.Runtime.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DSharp.Runtime.Sqlite.Tests.Features;

public sealed class ActorSqlitePublicationReaderTests
{
    [Fact]
    public void ReopenRetainsEmptyPublicationAndFrozenDeliveryChoices()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"dsharp-publication-read-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var first = Path.Combine(directory, "first.db");
            var second = Path.Combine(directory, "second.db");
            using (var db = ActorSqliteDatabase.OpenAndBind(first, "app'a"))
            {
                Execute(db, "INSERT INTO actor_submissions VALUES ($app,'input','publication',x'00FF',x'1122',x'33')", "app'a");
                Execute(db, "INSERT INTO actor_publications(application_id,publication_id,submission_id) VALUES ($app,'pub''one','input')", "app'a");
                Execute(db, "INSERT INTO actor_deliveries VALUES ($app,'pub''one','sub''one','public-a','selected-a')", "app'a");
                Execute(db, "INSERT INTO actor_deliveries VALUES ($app,'pub''one','sub-two','public-b','selected-b')", "app'a");
                Execute(db, "INSERT INTO actor_submissions VALUES ($app,'empty-input','publication',x'01',x'',x'')", "app'a");
                Execute(db, "INSERT INTO actor_publications(application_id,publication_id,submission_id) VALUES ($app,'empty','empty-input')", "app'a");
            }
            using (var db = ActorSqliteDatabase.OpenAndBind(second, "app-b"))
            {
                Execute(db, "INSERT INTO actor_submissions VALUES ($app,'input','publication',x'02',x'77',x'')", "app-b");
                Execute(db, "INSERT INTO actor_publications(application_id,publication_id,submission_id) VALUES ($app,'pub''one','input')", "app-b");
                Execute(db, "INSERT INTO actor_deliveries VALUES ($app,'pub''one','sub''one','other-public','other-selection')", "app-b");
            }

            var publication = Assert.IsType<ActorSqlitePublicationSnapshot>(
                ActorSqlitePublicationReader.ReadPublication(first, "app'a", "pub'one"));
            Assert.Equal("pub'one", publication.PublicationId);
            Assert.Equal("input", publication.SubmissionId);
            Assert.Equal(new byte[] { 0, 255 }, publication.Contract);
            Assert.Equal(new byte[] { 17, 34 }, publication.Payload);
            Assert.Equal(new byte[] { 51 }, publication.Options);
            publication.Contract[0] = 9;
            publication.Payload[0] = 9;
            publication.Options[0] = 9;
            Assert.Equal(new byte[] { 0, 255 }, publication.Contract);
            Assert.Equal(new byte[] { 17, 34 }, publication.Payload);
            Assert.Equal(new byte[] { 51 }, publication.Options);
            Assert.Equal(new byte[] { 0, 255 }, ActorSqlitePublicationReader.ReadPublication(first, "app'a", "pub'one")!.Contract);

            var firstDelivery = Assert.IsType<ActorSqliteDeliverySnapshot>(
                ActorSqlitePublicationReader.ReadDelivery(first, "app'a", "pub'one", "sub'one"));
            Assert.Equal(new ActorSqliteDeliverySnapshot("pub'one", "sub'one", "public-a", "selected-a"), firstDelivery);
            var otherDelivery = Assert.IsType<ActorSqliteDeliverySnapshot>(
                ActorSqlitePublicationReader.ReadDelivery(first, "app'a", "pub'one", "sub-two"));
            Assert.Equal("public-b", otherDelivery.RequestedActorId);
            Assert.Equal("selected-b", otherDelivery.SelectedActorId);
            var secondDelivery = Assert.IsType<ActorSqliteDeliverySnapshot>(
                ActorSqlitePublicationReader.ReadDelivery(second, "app-b", "pub'one", "sub'one"));
            Assert.Equal("other-selection", secondDelivery.SelectedActorId);
            Assert.Equal(new byte[] { 119 }, ActorSqlitePublicationReader.ReadPublication(second, "app-b", "pub'one")!.Payload);

            var empty = Assert.IsType<ActorSqlitePublicationSnapshot>(
                ActorSqlitePublicationReader.ReadPublication(first, "app'a", "empty"));
            Assert.Equal("empty-input", empty.SubmissionId);
            Assert.Empty(empty.Payload);
            Assert.Empty(empty.Options);
            Assert.Null(ActorSqlitePublicationReader.ReadDelivery(first, "app'a", "empty", "sub'one"));
            Assert.Null(ActorSqlitePublicationReader.ReadPublication(first, "app'a", "missing"));
            Assert.Null(ActorSqlitePublicationReader.ReadPublication(first, "app'a", "missing' OR 1=1 --"));
            Assert.Null(ActorSqlitePublicationReader.ReadDelivery(first, "app'a", "pub'one", "missing' OR 1=1 --"));
            Assert.Throws<InvalidOperationException>(() => ActorSqlitePublicationReader.ReadPublication(first, "app-b", "pub'one"));
            Assert.Throws<InvalidOperationException>(() => ActorSqlitePublicationReader.ReadDelivery(first, "app-b", "pub'one", "sub'one"));
            Assert.Throws<ArgumentException>(() => ActorSqlitePublicationReader.ReadPublication(first, "app'a", ""));
            Assert.Throws<ArgumentException>(() => ActorSqlitePublicationReader.ReadDelivery(first, "app'a", "pub'one", ""));
            var absent = Path.Combine(directory, "absent.db");
            Assert.Throws<SqliteException>(() => ActorSqlitePublicationReader.ReadPublication(absent, "app'a", "pub'one"));
            Assert.False(File.Exists(absent));
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
