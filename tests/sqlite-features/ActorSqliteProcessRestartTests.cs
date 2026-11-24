using System.Diagnostics;
using System.Reflection;
using DSharp.Runtime.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DSharp.Runtime.Sqlite.Tests.Features;

public sealed class ActorSqliteProcessRestartTests
{
    private const string PhaseVariable = "DSHARP_SQLITE_RESTART_TEST_PHASE";
    private const string DirectoryVariable = "DSHARP_SQLITE_RESTART_TEST_DIRECTORY";

    [Fact]
    public void CommittedActorRecordsSurviveWriterProcessExit()
    {
        var phase = Environment.GetEnvironmentVariable(PhaseVariable);
        if (phase is not null)
        {
            var childDirectory = Environment.GetEnvironmentVariable(DirectoryVariable);
            Assert.False(string.IsNullOrEmpty(childDirectory));
            if (phase == "write") WriteFiles(childDirectory!);
            else if (phase == "read") VerifyFiles(childDirectory!);
            else throw new InvalidOperationException("Unknown process-test phase.");
            File.WriteAllText(Path.Combine(childDirectory!, $"{phase}.complete"), phase);
            return;
        }

        var directory = Path.Combine(Path.GetTempPath(), $"dsharp-process-reopen-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            RunChild(directory, "write");
            RunChild(directory, "read");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void RunChild(string directory, string phase)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add("vstest");
        start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        start.ArgumentList.Add("--TestCaseFilter:FullyQualifiedName=DSharp.Runtime.Sqlite.Tests.Features.ActorSqliteProcessRestartTests.CommittedActorRecordsSurviveWriterProcessExit");
        start.Environment[PhaseVariable] = phase;
        start.Environment[DirectoryVariable] = directory;
        using var child = Process.Start(start) ?? throw new InvalidOperationException("Could not start the SQLite test process.");
        var output = child.StandardOutput.ReadToEndAsync();
        var error = child.StandardError.ReadToEndAsync();
        if (!child.WaitForExit(60000))
        {
            child.Kill(entireProcessTree: true);
            child.WaitForExit();
            throw new TimeoutException($"SQLite {phase} process did not exit.");
        }
        var diagnostic = output.GetAwaiter().GetResult() + error.GetAwaiter().GetResult();
        Assert.True(child.ExitCode == 0, $"SQLite {phase} process failed: {diagnostic}");
        var marker = Path.Combine(directory, $"{phase}.complete");
        Assert.True(File.Exists(marker), $"SQLite {phase} test did not run: {diagnostic}");
        Assert.Equal(phase, File.ReadAllText(marker));
    }

    private static void WriteFiles(string directory)
    {
        WriteFile(Path.Combine(directory, "first.db"), "app-a", "selection-a", "AA");
        WriteFile(Path.Combine(directory, "second.db"), "app-b", "selection-b", "BB");
    }

    private static void WriteFile(string path, string app, string selected, string payloadHex)
    {
        using var db = ActorSqliteDatabase.OpenAndBind(path, app);
        using var transaction = db.BeginTransaction();
        Execute(db, transaction, $"INSERT INTO actor_submissions VALUES ($app,'target','targeted',x'0102',x'{payloadHex}',x'FF')", app);
        Execute(db, transaction, """
            INSERT INTO actor_invocations(application_id,invocation_id,submission_id,requested_actor_id,selected_actor_id,
              target_key,operation,parent_invocation_id,parent_attempt_id)
            VALUES ($app,'inv','target','requested',$selected,x'00','invoke','parent','parent-attempt')
            """, app, selected);
        Execute(db, transaction, "INSERT INTO actor_attempts VALUES ($app,'inv','attempt')", app);
        Execute(db, transaction, """
            INSERT INTO actor_attempt_exits(application_id,invocation_id,attempt_id,outcome_kind,payload,contract,report_present)
            VALUES ($app,'inv','attempt','reported_fault',x'CAFE',x'01',1)
            """, app);
        Execute(db, transaction, """
            INSERT INTO actor_fault_observations(application_id,invocation_id,attempt_id,ordinal,classification,diagnostic,metadata)
            VALUES ($app,'inv','attempt',-1,'primary',x'11',x'22')
            """, app);
        Execute(db, transaction, """
            INSERT INTO actor_invocation_lifecycle(application_id,invocation_id,state,eligibility_version,cancellation_requested,block_id,block_reason)
            VALUES ($app,'inv','blocked',2,0,'block','implementation_unavailable')
            """, app);
        Execute(db, transaction, "INSERT INTO actor_submissions VALUES ($app,'pub-sub','publication',x'03',x'04',x'')", app);
        Execute(db, transaction, "INSERT INTO actor_publications(application_id,publication_id,submission_id) VALUES ($app,'pub','pub-sub')", app);
        Execute(db, transaction, "INSERT INTO actor_deliveries VALUES ($app,'pub','sub','public',$selected)", app, selected);
        Execute(db, transaction, """
            INSERT INTO actor_invocations(application_id,invocation_id,source_publication_id,source_subscription_id,
              requested_actor_id,selected_actor_id,operation) VALUES ($app,'delivery','pub','sub','public',$selected,'invoke')
            """, app, selected);
        Execute(db, transaction, "INSERT INTO actor_submissions VALUES ($app,'empty-sub','publication',x'05',x'',x'')", app);
        Execute(db, transaction, "INSERT INTO actor_publications(application_id,publication_id,submission_id) VALUES ($app,'empty','empty-sub')", app);
        transaction.Commit();
    }

    private static void VerifyFiles(string directory)
    {
        VerifyFile(Path.Combine(directory, "first.db"), "app-a", "app-b", "selection-a", 0xAA);
        VerifyFile(Path.Combine(directory, "second.db"), "app-b", "app-a", "selection-b", 0xBB);
    }

    private static void VerifyFile(string path, string app, string otherApp, string selected, byte payload)
    {
        var pinned = Assert.IsType<ActorSqlitePinnedWork>(ActorSqlitePinnedWorkReader.Read(path, app, "inv"));
        Assert.Equal("requested", pinned.RequestedActorId);
        Assert.Equal(selected, pinned.SelectedActorId);
        Assert.Equal(new byte[] { 1, 2 }, pinned.Contract);
        Assert.Equal(new[] { payload }, pinned.Payload);
        Assert.Equal(new byte[] { 255 }, pinned.Options);
        Assert.Equal(new byte[] { 0 }, pinned.TargetKey);
        Assert.Equal("parent", pinned.ParentInvocationId);
        Assert.Equal("parent-attempt", pinned.ParentAttemptId);
        var delivery = Assert.IsType<ActorSqlitePinnedWork>(ActorSqlitePinnedWorkReader.Read(path, app, "delivery"));
        Assert.Equal("pub", delivery.PublicationId);
        Assert.Equal("sub", delivery.SubscriptionId);
        Assert.Equal(selected, delivery.SelectedActorId);
        Assert.Equal(new byte[] { 4 }, delivery.Payload);
        Assert.Throws<InvalidOperationException>(() => ActorSqlitePinnedWorkReader.Read(path, otherApp, "inv"));

        using var db = ActorSqliteDatabase.OpenBound(path, app);
        Assert.Equal("delete", Scalar(db, "PRAGMA journal_mode"));
        Assert.Equal(1L, Scalar(db, "PRAGMA foreign_keys"));
        Assert.Equal(2L, Scalar(db, "PRAGMA synchronous"));
        Assert.Equal(1L, Scalar(db, "SELECT count(*) FROM actor_attempts WHERE invocation_id='inv' AND attempt_id='attempt'"));
        Assert.Equal("CAFE", Scalar(db, "SELECT hex(payload) FROM actor_attempt_exits WHERE invocation_id='inv' AND attempt_id='attempt'"));
        Assert.Equal("primary", Scalar(db, "SELECT classification FROM actor_fault_observations WHERE invocation_id='inv' AND ordinal=-1"));
        Assert.Equal("implementation_unavailable", Scalar(db, "SELECT block_reason FROM actor_invocation_lifecycle WHERE invocation_id='inv'"));
        Assert.Equal(1L, Scalar(db, "SELECT count(*) FROM actor_publications WHERE publication_id='empty'"));
        Assert.Equal(0L, Scalar(db, "SELECT count(*) FROM actor_deliveries WHERE publication_id='empty'"));
        Assert.Equal(0L, Scalar(db, "SELECT count(*) FROM actor_publications WHERE publication_id='missing'"));
    }

    private static void Execute(SqliteConnection db, SqliteTransaction transaction, string sql, string app, string? selected = null)
    {
        using var command = db.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.AddWithValue("$app", app);
        if (selected is not null) command.Parameters.AddWithValue("$selected", selected);
        command.ExecuteNonQuery();
    }

    private static object? Scalar(SqliteConnection db, string sql)
    {
        using var command = db.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }
}
