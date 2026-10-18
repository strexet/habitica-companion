using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Tests.Workspace;

public sealed class WorkspaceUnblockedTodosTests
{
    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","unblocked-todos-v1.json")));
    [Fact]
    public void Neutral_candidate_cases_preserve_dependency_state_filters_order_and_pages()
    {
        using var fixture=Fixture();var document=WorkspaceJson.Deserialize(fixture.RootElement.GetProperty("document").GetRawText());var before=WorkspaceJson.Serialize(document);
        foreach(var item in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var page=WorkspaceUnblockedTodos.Evaluate(document,WorkspaceTaskApiJson.Deserialize<WorkspaceTaskFilter>(item.GetProperty("filter").GetRawText()));
            var ids=WorkspaceTaskApiJson.Deserialize<Guid[]>(item.GetProperty("taskIds").GetRawText());Assert.Equal(ids,page.Tasks.Select(task=>task.Id));Assert.Equal(ids.Select(id=>document.Tasks.Single(task=>task.Id==id)),page.Tasks);
            Assert.Equal(item.GetProperty("matchedCount").GetInt32(),page.MatchedCount);Assert.Equal(item.GetProperty("offset").GetInt32(),page.Offset);Assert.Equal(item.GetProperty("truncated").GetBoolean(),page.Truncated);Assert.Equal(item.GetProperty("nextOffset").ValueKind==JsonValueKind.Null?(int?)null:item.GetProperty("nextOffset").GetInt32(),page.NextOffset);
        }
        Assert.Equal(before,WorkspaceJson.Serialize(document));
    }
    [Fact]
    public void Invalid_filters_stale_revisions_and_damaged_dependency_graphs_fail_explicitly()
    {
        using var fixture=Fixture();var document=WorkspaceJson.Deserialize(fixture.RootElement.GetProperty("document").GetRawText());
        foreach(var filter in fixture.RootElement.GetProperty("invalid").EnumerateArray())Assert.ThrowsAny<Exception>(()=>WorkspaceUnblockedTodos.Evaluate(document,WorkspaceTaskApiJson.Deserialize<WorkspaceTaskFilter>(filter.GetRawText())));
        Assert.Throws<InvalidOperationException>(()=>WorkspaceUnblockedTodos.Evaluate(document,new(ExpectedRevision:19)));
        Assert.Throws<InvalidOperationException>(()=>WorkspaceUnblockedTodos.Evaluate(document with { Tasks=document.Tasks.Select((task,index)=>index==0?task with { DependsOn=[Guid.NewGuid()] }:task).ToArray() },new()));
    }
    [Fact]
    public void Restoring_and_recompleting_a_dependency_changes_candidates_without_scoring_dependants()
    {
        using var fixture=Fixture();var document=WorkspaceJson.Deserialize(fixture.RootElement.GetProperty("document").GetRawText());var state=new WorkspaceJournalSnapshot(document,20,[],[]);var completion=document.Completions[0];var target=Guid.Parse("60000000-0000-4000-8000-000000000201");
        Assert.Contains(WorkspaceUnblockedTodos.Evaluate(document,new()).Tasks,task=>task.Id==target);
        var restore=new WorkspaceCommand(Guid.NewGuid(),document.Id,20,WorkspaceOperation.RestoreTodo,completion.TaskId,new(RestoreTodo:new(completion.Id)));
        state=WorkspaceCommandEngine.Accept(state,restore,DateTimeOffset.Parse("2026-10-18T12:00:00Z")).Snapshot!;
        Assert.DoesNotContain(WorkspaceUnblockedTodos.Evaluate(state.Document,new()).Tasks,task=>task.Id==target);
        var complete=new WorkspaceCommand(Guid.NewGuid(),document.Id,state.Document.Revision,WorkspaceOperation.CompleteTask,completion.TaskId);
        state=WorkspaceCommandEngine.Accept(state,complete,DateTimeOffset.Parse("2026-10-18T12:01:00Z")).Snapshot!;Assert.Contains(WorkspaceUnblockedTodos.Evaluate(state.Document,new()).Tasks,task=>task.Id==target);
        Assert.Equal(2,state.Commands.Count);Assert.All(state.Commands,entry=>Assert.Empty(entry.Effects));Assert.Empty(state.Document.HabitScores);
    }
}
