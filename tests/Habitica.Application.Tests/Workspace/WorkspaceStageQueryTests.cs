using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Tests.Workspace;

public sealed class WorkspaceStageQueryTests
{
    [Fact]
    public void Neutral_stage_queries_and_candidates_retain_current_state_precedence_and_stable_pages()
    {
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","stage-query-v1.json")));
        var document=WorkspaceJson.Deserialize(fixture.RootElement.GetProperty("document").GetRawText());var before=WorkspaceJson.Serialize(document);
        foreach(var row in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var filter=WorkspaceTaskApiJson.Deserialize<WorkspaceTaskFilter>(row.GetProperty("filter").GetRawText());
            var page=row.GetProperty("tool").GetString()=="get_unblocked_todos"?WorkspaceUnblockedTodos.Evaluate(document,filter):WorkspaceTaskQuery.Evaluate(document,filter);
            Assert.Equal(row.GetProperty("taskIds").EnumerateArray().Select(id=>id.GetGuid()),page.Tasks.Select(task=>task.Id));
            Assert.Equal(row.GetProperty("matchedCount").GetInt32(),page.MatchedCount);Assert.Equal(row.GetProperty("offset").GetInt32(),page.Offset);Assert.Equal(row.GetProperty("truncated").GetBoolean(),page.Truncated);
            Assert.Equal(row.GetProperty("nextOffset").ValueKind==JsonValueKind.Null?(int?)null:row.GetProperty("nextOffset").GetInt32(),page.NextOffset);
            Assert.All(page.Tasks,task=>Assert.Same(document.Tasks.Single(source=>source.Id==task.Id),task));
        }
        Assert.Equal(before,WorkspaceJson.Serialize(document));Assert.DoesNotContain("executionStage",WorkspaceTaskApiJson.Serialize(new WorkspaceTaskFilter()));
    }
    [Fact]
    public void Invalid_or_incompatible_stage_and_stale_revision_fail_without_reinterpreting_filters()
    {
        var document=new WorkspaceDocument {Id=Guid.NewGuid(),Revision=20};
        foreach(var filter in new WorkspaceTaskFilter[] {new(ExecutionStage:(TodoExecutionStage)99),new(ExecutionStage:TodoExecutionStage.Next,Kind:CompanionTaskKind.Daily),new(ExecutionStage:TodoExecutionStage.Inbox,Kind:CompanionTaskKind.Habit),new(ExecutionStage:TodoExecutionStage.Next,State:CompanionTaskState.Waiting),new(ExecutionStage:TodoExecutionStage.Next,State:CompanionTaskState.Completed),new(ExecutionStage:TodoExecutionStage.Next,State:CompanionTaskState.Paused),new(ExecutionStage:TodoExecutionStage.Inbox,State:CompanionTaskState.Someday),new(ExecutionStage:TodoExecutionStage.Next,State:CompanionTaskState.Cancelled)})
            Assert.Throws<ArgumentException>(()=>WorkspaceTaskQuery.Evaluate(document,filter));
        Assert.Throws<JsonException>(()=>WorkspaceTaskApiJson.Deserialize<WorkspaceTaskFilter>("{\"executionStage\":\"Done\"}"));
        Assert.Equal("stale_revision",Assert.Throws<InvalidOperationException>(()=>WorkspaceTaskQuery.Evaluate(document,new(ExecutionStage:TodoExecutionStage.Next,ExpectedRevision:19))).Message);
        Assert.Equal("stale_revision",Assert.Throws<InvalidOperationException>(()=>WorkspaceUnblockedTodos.Evaluate(document,new(ExecutionStage:TodoExecutionStage.Inbox,Offset:1,ExpectedRevision:19))).Message);
    }
}
