using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
namespace Habitica.Application.Tests.Workspace;
public sealed class WorkspaceSavedTaskViewTests
{
    private static JsonDocument Fixture()=>JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","saved-task-views-v1.json")));
    [Fact]
    public void Named_queries_reuse_current_predicates_and_guarded_pages_without_mutation()
    {
        using var fixture=Fixture();var document=WorkspaceJson.Deserialize(fixture.RootElement.GetProperty("document").GetRawText());var before=WorkspaceJson.Serialize(document);
        foreach(var row in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var page=WorkspaceSavedTaskViews.Evaluate(document,row.GetProperty("viewId").GetGuid(),WorkspaceTaskApiJson.Deserialize<WorkspaceReadFilter>(row.GetProperty("page").GetRawText()));
            Assert.Equal(row.GetProperty("taskIds").EnumerateArray().Select(v=>v.GetGuid()),page.Tasks.Select(t=>t.Id));Assert.Equal(row.GetProperty("matchedCount").GetInt32(),page.MatchedCount);Assert.Equal(row.GetProperty("truncated").GetBoolean(),page.Truncated);Assert.Equal(row.GetProperty("offset").GetInt32(),page.Offset);Assert.Equal(row.GetProperty("nextOffset").ValueKind==JsonValueKind.Null?(int?)null:row.GetProperty("nextOffset").GetInt32(),page.NextOffset);
        }
        Assert.Equal(before,WorkspaceJson.Serialize(document));
        Assert.Equal("saved_view_not_found",Assert.Throws<InvalidOperationException>(()=>WorkspaceSavedTaskViews.Evaluate(document,Guid.NewGuid(),new())).Message);Assert.Equal("saved_view_not_found",Assert.Throws<InvalidOperationException>(()=>WorkspaceSavedTaskViews.Evaluate(document,document.SavedTaskViews!.Last().Id,new())).Message);
        Assert.Equal("stale_revision",Assert.Throws<InvalidOperationException>(()=>WorkspaceSavedTaskViews.Evaluate(document,document.SavedTaskViews![0].Id,new(Offset:1,ExpectedRevision:19))).Message);Assert.Throws<ArgumentException>(()=>WorkspaceSavedTaskViews.Evaluate(document,document.SavedTaskViews![0].Id,new(Offset:1)));Assert.Throws<ArgumentException>(()=>WorkspaceSavedTaskViews.Evaluate(document,Guid.Empty,new()));
    }
    [Fact]
    public void Saved_queries_read_current_native_state_instead_of_retaining_old_results()
    {
        using var fixture=Fixture();var document=WorkspaceJson.Deserialize(fixture.RootElement.GetProperty("document").GetRawText());var view=document.SavedTaskViews!.Single(v=>v.Name=="Next cannot bypass incomplete prerequisites");var before=Assert.Single(WorkspaceSavedTaskViews.Evaluate(document,view.Id,new()).Tasks);
        var command=new WorkspaceCommand(Guid.NewGuid(),document.Id,document.Revision,WorkspaceOperation.SetTodoState,before.Id,new(TodoState:new(CompanionTaskState.Waiting)));var result=WorkspaceCommandEngine.Accept(new(document,document.Revision,[],[]),command,DateTimeOffset.Parse("2026-10-18T21:00:00Z"));Assert.Equal(SubmissionDisposition.Accepted,result.Disposition);Assert.Empty(WorkspaceSavedTaskViews.Evaluate(result.Snapshot!.Document,view.Id,new()).Tasks);Assert.Equal(document.SavedTaskViews,result.Snapshot.Document.SavedTaskViews);Assert.Empty(result.Entry!.Effects);
        Assert.Equal("stale_revision",Assert.Throws<InvalidOperationException>(()=>WorkspaceSavedTaskViews.Evaluate(result.Snapshot.Document,view.Id,new(ExpectedRevision:20))).Message);
    }
}
