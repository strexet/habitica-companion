using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Tests.Workspace;

public sealed class WorkspaceRestorationModelTests
{
    private static WorkspaceDocument Document(string name)
    {
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","todo-restoration-cycles-v1.json")));
        return WorkspaceJson.Deserialize(fixture.RootElement.GetProperty("cases").EnumerateArray().Single(value=>value.GetProperty("name").GetString()==name).GetProperty("document").GetRawText());
    }
    [Fact]
    public void Completing_a_restored_Todo_adds_a_new_event_and_preserves_prior_cycles()
    {
        var document=Document("all cycles restored and ready to complete again");var snapshot=new WorkspaceJournalSnapshot(document,document.Revision,[],[]);var task=document.Tasks[0];
        var command=new WorkspaceCommand(Guid.NewGuid(),document.Id,document.Revision,WorkspaceOperation.CompleteTask,task.Id);
        var result=WorkspaceCommandEngine.Accept(snapshot,command,DateTimeOffset.Parse("2026-10-18T12:00:00Z"));Assert.Equal(SubmissionDisposition.Accepted,result.Disposition);
        Assert.Equal(3,result.Snapshot!.Document.Completions.Count);Assert.Equal(document.Completions.Select(row=>row.Id),result.Snapshot.Document.Completions.Take(2).Select(row=>row.Id));
        Assert.Equal(document.Restorations,result.Snapshot.Document.Restorations);Assert.Equal(CompanionTaskState.Completed,result.Snapshot.Document.Tasks[0].State);Assert.Empty(result.Entry!.Effects);
        Assert.True(WorkspaceJournalValidation.IsValid(WorkspaceJournalJson.Deserialize(WorkspaceJournalJson.Serialize(result.Snapshot))));
        Assert.Equal(SubmissionDisposition.Duplicate,WorkspaceCommandEngine.Accept(result.Snapshot,command,DateTimeOffset.Parse("2026-10-18T13:00:00Z")).Disposition);
    }
    [Fact]
    public void History_exposes_explicit_restoration_metadata_without_changing_legacy_rows()
    {
        var document=Document("all cycles restored and ready to complete again");var page=WorkspaceCompletionHistory.Evaluate(new(document,document.Revision,[],[]),new());
        Assert.Equal(2,page.Items.Count);Assert.All(page.Items,item=>Assert.NotNull(item.Restoration));
        foreach(var item in page.Items){var restoration=document.Restorations!.Single(value=>value.CompletionId==item.EventId);Assert.Equal(restoration.Id,item.Restoration!.EventId);Assert.Equal(restoration.CommandId,item.Restoration.CommandId);Assert.Equal(restoration.RestoredAtUtc,item.Restoration.RestoredAtUtc);}
        var legacy=Document("legacy completion has no extension");var legacyItem=Assert.Single(WorkspaceCompletionHistory.Evaluate(new(legacy,legacy.Revision,[],[]),new()).Items);Assert.Null(legacyItem.Restoration);Assert.False(WorkspaceTaskApiJson.Element(legacyItem).TryGetProperty("restoration",out _));
    }
}
