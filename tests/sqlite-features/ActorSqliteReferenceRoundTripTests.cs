using System.Collections;
using System.Diagnostics;
using System.Reflection;
using DSharp.Runtime.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DSharp.Runtime.Sqlite.Tests.Features;

public sealed class ActorSqliteReferenceRoundTripTests
{
    private const string WriterDirectoryVariable = "DSHARP_SQLITE_REFERENCE_WRITER_DIRECTORY";
    private const string App = "reference-app";
    private static readonly Guid Invocation = Guid.Parse("f695da52-54f1-475d-8dd5-219117dc3022");
    private static readonly Guid Publication = Guid.Parse("13390b29-8cf8-42b6-a39a-b27650f65087");

    [Fact]
    public void ActualAcceptedReferenceRecordsCanBeStoredAndReadAfterWriterExit()
    {
        var childDirectory = Environment.GetEnvironmentVariable(WriterDirectoryVariable);
        if (childDirectory is not null)
        {
            WriteAcceptedRecords(Path.Combine(childDirectory, "actor.db"));
            File.WriteAllText(Path.Combine(childDirectory, "writer.complete"), "ok");
            return;
        }

        var directory = Path.Combine(Path.GetTempPath(), $"dsharp-reference-roundtrip-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            RunWriter(directory);
            var path = Path.Combine(directory, "actor.db");
            var targeted = Assert.IsType<ActorSqlitePinnedWork>(ActorSqlitePinnedWorkReader.Read(path, App, Invocation.ToString("D")));
            Assert.Equal("target-sub", targeted.SubmissionId);
            Assert.Equal("requested-actor", targeted.RequestedActorId);
            Assert.Equal("selected-implementation", targeted.SelectedActorId);
            Assert.Equal("invoke", targeted.Operation);
            Assert.Equal("parent-invocation", targeted.ParentInvocationId);
            Assert.Equal("parent-attempt", targeted.ParentAttemptId);
            Assert.Equal(new byte[] { 0, 255 }, targeted.Contract);
            Assert.Equal(new byte[] { 1, 2, 3 }, targeted.Payload);
            Assert.Equal(new byte[] { 4, 5 }, targeted.Options);
            Assert.Equal(new byte[] { 0, 9 }, targeted.TargetKey);

            var publicationId = Publication.ToString("D");
            var publication = Assert.IsType<ActorSqlitePublicationSnapshot>(ActorSqlitePublicationReader.ReadPublication(path, App, publicationId));
            Assert.Equal("publication-sub", publication.SubmissionId);
            Assert.Equal(new byte[] { 6, 7 }, publication.Contract);
            Assert.Equal(new byte[] { 8, 9 }, publication.Payload);
            Assert.Equal(new byte[] { 10 }, publication.Options);
            Assert.Equal(new ActorSqliteDeliverySnapshot(publicationId, "first", "public-one", "selected-one"),
                ActorSqlitePublicationReader.ReadDelivery(path, App, publicationId, "first"));
            Assert.Equal(new ActorSqliteDeliverySnapshot(publicationId, "second", "public-two", "selected-two"),
                ActorSqlitePublicationReader.ReadDelivery(path, App, publicationId, "second"));
            Assert.Null(ActorSqlitePublicationReader.ReadDelivery(path, App, publicationId, "missing"));
            Assert.Throws<InvalidOperationException>(() => ActorSqlitePinnedWorkReader.Read(path, "other-app", Invocation.ToString("D")));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void RunWriter(string directory)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add("vstest");
        start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        start.ArgumentList.Add("--TestCaseFilter:FullyQualifiedName=DSharp.Runtime.Sqlite.Tests.Features.ActorSqliteReferenceRoundTripTests.ActualAcceptedReferenceRecordsCanBeStoredAndReadAfterWriterExit");
        start.Environment[WriterDirectoryVariable] = directory;
        using var child = Process.Start(start) ?? throw new InvalidOperationException("Could not start the reference writer process.");
        var output = child.StandardOutput.ReadToEndAsync();
        var error = child.StandardError.ReadToEndAsync();
        if (!child.WaitForExit(60000))
        {
            child.Kill(entireProcessTree: true);
            child.WaitForExit();
            throw new TimeoutException("The reference writer process did not exit.");
        }
        var diagnostic = output.GetAwaiter().GetResult() + error.GetAwaiter().GetResult();
        Assert.True(child.ExitCode == 0, $"Reference writer process failed: {diagnostic}");
        var marker = Path.Combine(directory, "writer.complete");
        Assert.True(File.Exists(marker), $"Reference writer test did not run: {diagnostic}");
        Assert.Equal("ok", File.ReadAllText(marker));
    }

    // Only the test bridges the separate SQLite and internal reference-model
    // assemblies. This mapping is not a provider admission or deduplication API.
    private static void WriteAcceptedRecords(string path)
    {
        var runtime = Assembly.Load("DSharp.Runtime");
        Type Find(string name) => runtime.GetType($"DSharp.Runtime.{name}", throwOnError: true)!;
        var store = Activator.CreateInstance(Find("VolatileTargetedStore"),
            TimeSpan.FromMinutes(1), Enum.Parse(Find("VolatileStoreRequirements"), "None"))!;
        var targetedRequest = Activator.CreateInstance(Find("TargetedRequest"), App, "target-sub",
            Enum.Parse(Find("TargetedOperation"), "Invoke"), "requested-actor", new byte[] { 1, 2, 3 },
            new byte[] { 0, 255 }, new byte[] { 4, 5 }, "parent-invocation", "parent-attempt", new byte[] { 0, 9 })!;
        var targetedResult = store.GetType().GetMethod("Accept")!.Invoke(store, [targetedRequest, Invocation, "selected-implementation"])!;
        Assert.Equal("Created", Property(targetedResult, "Kind").ToString());
        var targeted = Property(targetedResult, "Record");
        var request = Property(targeted, "Request");
        var operation = Property(request, "Operation").ToString() switch
        {
            "Invoke" => "invoke",
            "Dispatch" => "dispatch",
            _ => throw new InvalidOperationException("Unexpected reference-model operation.")
        };

        var publicationRequest = Activator.CreateInstance(Find("PublicationRequest"), App, "publication-sub",
            new byte[] { 8, 9 }, new byte[] { 6, 7 }, new byte[] { 10 })!;
        var intentType = Find("PublicationIntent");
        var intents = Array.CreateInstance(intentType, 2);
        intents.SetValue(Activator.CreateInstance(intentType, "first", "public-one", "selected-one"), 0);
        intents.SetValue(Activator.CreateInstance(intentType, "second", "public-two", "selected-two"), 1);
        var publicationResult = store.GetType().GetMethod("AcceptPublication")!.Invoke(store, [publicationRequest, Publication, intents])!;
        Assert.Equal("Created", Property(publicationResult, "Kind").ToString());
        var publication = Property(publicationResult, "Record");
        var pubRequest = Property(publication, "Request");

        using var db = ActorSqliteDatabase.OpenAndBind(path, App);
        using var tx = db.BeginTransaction();
        Execute(db, tx, "INSERT INTO actor_submissions VALUES ($app,$sub,'targeted',$contract,$payload,$options)",
            ("$app", Property(request, "ApplicationId")), ("$sub", Property(request, "SubmissionId")),
            ("$contract", Property(request, "ContractMetadata")), ("$payload", Property(request, "Payload")), ("$options", Property(request, "BehaviorOptions")));
        Execute(db, tx, """
            INSERT INTO actor_invocations(application_id,invocation_id,submission_id,requested_actor_id,selected_actor_id,
              target_key,operation,parent_invocation_id,parent_attempt_id)
            VALUES ($app,$inv,$sub,$requested,$selected,$key,$operation,$parent,$attempt)
            """, ("$app", Property(request, "ApplicationId")), ("$inv", ((Guid)Property(targeted, "InvocationId")).ToString("D")),
            ("$sub", Property(request, "SubmissionId")), ("$requested", Property(request, "RequestedTarget")),
            ("$selected", Property(targeted, "SelectedImplementation")), ("$key", Property(request, "PublicTargetKey")),
            ("$operation", operation),
            ("$parent", Property(request, "ParentInvocationId")), ("$attempt", Property(request, "ParentAttemptId")));
        Execute(db, tx, "INSERT INTO actor_submissions VALUES ($app,$sub,'publication',$contract,$payload,$options)",
            ("$app", Property(pubRequest, "ApplicationId")), ("$sub", Property(pubRequest, "SubmissionId")),
            ("$contract", Property(pubRequest, "ContractMetadata")), ("$payload", Property(pubRequest, "Payload")), ("$options", Property(pubRequest, "BehaviorOptions")));
        Execute(db, tx, "INSERT INTO actor_publications(application_id,publication_id,submission_id) VALUES ($app,$pub,$sub)",
            ("$app", Property(pubRequest, "ApplicationId")), ("$pub", ((Guid)Property(publication, "PublicationId")).ToString("D")),
            ("$sub", Property(pubRequest, "SubmissionId")));
        foreach (var intent in (IEnumerable)Property(publication, "Intents"))
            Execute(db, tx, "INSERT INTO actor_deliveries VALUES ($app,$pub,$sub,$requested,$selected)",
                ("$app", Property(pubRequest, "ApplicationId")), ("$pub", ((Guid)Property(publication, "PublicationId")).ToString("D")),
                ("$sub", Property(intent!, "SubscriptionId")), ("$requested", Property(intent!, "PublicTarget")),
                ("$selected", Property(intent!, "SelectedImplementation")));
        tx.Commit();
    }

    private static object Property(object value, string name) =>
        value.GetType().GetProperty(name)!.GetValue(value) ?? throw new InvalidOperationException($"Missing reference-model {name}.");

    private static void Execute(SqliteConnection db, SqliteTransaction tx, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = db.CreateCommand();
        command.Transaction = tx;
        command.CommandText = sql;
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        command.ExecuteNonQuery();
    }
}
