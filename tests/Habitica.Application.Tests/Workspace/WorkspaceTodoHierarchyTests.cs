using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Tests.Workspace;

public sealed class WorkspaceTodoHierarchyTests
{
    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","todo-hierarchy-v1.json")));
    private static WorkspaceDocument Document(JsonDocument fixture) => WorkspaceJson.Deserialize(fixture.RootElement.GetProperty("document").GetRawText());
    [Fact]
    public void Portable_retained_hierarchy_pages_preserve_order_depth_parent_context_and_every_record()
    {
        using var fixture=Fixture();var document=Document(fixture);var before=WorkspaceJson.Serialize(document);
        foreach(var row in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var filter=WorkspaceTaskApiJson.Deserialize<WorkspaceHierarchyFilter>(row.GetProperty("arguments").GetRawText());var page=WorkspaceTodoHierarchy.Evaluate(document,filter);
            var compact=page.Items.Select(node=>new object?[]{node.Task.Id,node.Depth,node.ChildCount,WorkspaceTaskApiJson.Element(node.ParentStatus).GetString(),WorkspaceTaskApiJson.Element(node.ParentCoverage).GetString(),node.ParentOffset}).ToArray();
            Assert.Equal(row.GetProperty("rows").GetRawText().Replace(" ","").Replace("\n","").Replace("\r",""),WorkspaceTaskApiJson.Serialize(compact));
            Assert.Equal(6,page.TotalTodoCount);Assert.Equal(row.GetProperty("matchedCount").GetInt32(),page.MatchedCount);Assert.Equal(row.GetProperty("offset").GetInt32(),page.Offset);
            Assert.Equal(row.GetProperty("nextOffset").ValueKind==JsonValueKind.Null?(int?)null:row.GetProperty("nextOffset").GetInt32(),page.NextOffset);Assert.Equal(row.GetProperty("truncated").GetBoolean(),page.Truncated);
            Assert.Equal(filter.TaskId,page.ContextTask?.Id);Assert.Equal(document.Revision,page.Revision);
        }
        Assert.Equal(before,WorkspaceJson.Serialize(document));
    }
    [Fact]
    public void More_than_two_thousand_parent_levels_page_and_reparent_without_recursion_or_project_cascade()
    {
        using var fixture=Fixture();var source=Document(fixture);var recipe=fixture.RootElement.GetProperty("deepChain");var count=recipe.GetProperty("count").GetInt32();var prefix=recipe.GetProperty("idPrefix").GetString();
        Guid Id(int index)=>Guid.Parse(prefix+(index+1).ToString("D12"));
        var tasks=Enumerable.Range(0,count).Select(index=>source.Tasks[0] with {Id=Id(index),Text=recipe.GetProperty("sameName").GetString()!,ParentTaskId=index==0?null:Id(index-1),DependsOn=[],LabelIds=[],ProjectId=source.Projects[index%2].Id,Notes=null}).Reverse().ToArray();
        var document=source with {Tasks=tasks,Completions=[],Projections=[],Goals=[]};Assert.Empty(WorkspaceRules.Validate(document));
        var all=new List<WorkspaceHierarchyNode>();int offset=0;
        do
        {
            var page=WorkspaceTodoHierarchy.Evaluate(document,new(Limit:100,Offset:offset,ExpectedRevision:10));all.AddRange(page.Items);Assert.True(page.Items.Count<=100);Assert.Equal(count,page.MatchedCount);
            if(page.NextOffset is not {} next)break;offset=next;
        }while(true);
        Assert.Equal(count,all.Count);Assert.Equal(Enumerable.Range(0,count),all.Select(node=>node.Depth));Assert.Equal(Id(count-1),all[^1].Task.Id);Assert.Equal(2000,all[^1].Depth);
        var ancestors=WorkspaceTodoHierarchy.Evaluate(document,new(Id(count-1),WorkspaceHierarchyMode.Ancestors,Offset:1900,ExpectedRevision:10));Assert.Equal(2000,ancestors.MatchedCount);Assert.Equal(1899,ancestors.Items[0].ParentOffset);Assert.Equal(WorkspaceHierarchyParentCoverage.EarlierPage,ancestors.Items[0].ParentCoverage);
        var state=new WorkspaceJournalSnapshot(document,10,[],[]);var before=WorkspaceTaskApiJson.Serialize(document.Tasks);
        WorkspaceCommand Command(WorkspaceTaskRelationships patch)=>new(Guid.NewGuid(),document.Id,10,WorkspaceOperation.RelateTodo,Id(count-1),new(Relationships:patch));
        var move=Command(new(ParentTaskId:Id(0)));var accepted=WorkspaceCommandEngine.Accept(state,move,DateTimeOffset.Parse("2026-10-20T12:00:00Z"));Assert.Equal(SubmissionDisposition.Accepted,accepted.Disposition);Assert.Empty(accepted.Entry!.Effects);
        var changed=accepted.Snapshot!.Document.Tasks.Single(task=>task.Id==move.TaskId);Assert.Equal(tasks[0].ProjectId,changed.ProjectId);Assert.Equal(Id(0),changed.ParentTaskId);Assert.Equal(document.Completions,accepted.Snapshot.Document.Completions);
        Assert.Equal(SubmissionDisposition.Duplicate,WorkspaceCommandEngine.Accept(accepted.Snapshot,move,DateTimeOffset.Parse("2026-10-20T12:01:00Z")).Disposition);
        var detached=WorkspaceCommandEngine.Accept(state,Command(new(ClearParentTask:true)),DateTimeOffset.Parse("2026-10-20T12:00:00Z"));Assert.Null(detached.Snapshot!.Document.Tasks.Single(task=>task.Id==Id(count-1)).ParentTaskId);Assert.Empty(detached.Entry!.Effects);
        var cycle=move with {Id=Guid.NewGuid(),TaskId=Id(0),Patch=new(Relationships:new(ParentTaskId:Id(count-1)))};Assert.Equal(SubmissionDisposition.Invalid,WorkspaceCommandEngine.Accept(state,cycle,DateTimeOffset.Parse("2026-10-20T12:00:00Z")).Disposition);
        Assert.Equal(before,WorkspaceTaskApiJson.Serialize(document.Tasks));
    }
    [Fact]
    public void Invalid_scope_missing_or_nonTodo_targets_and_broken_graphs_never_become_partial_trees()
    {
        using var fixture=Fixture();var document=Document(fixture);
        foreach(var filter in new WorkspaceHierarchyFilter[]{new(Mode:WorkspaceHierarchyMode.Ancestors),new(TaskId:Guid.Empty),new(Mode:(WorkspaceHierarchyMode)99),new(Limit:0),new(Limit:101),new(Offset:1),new(Offset:-1),new(Offset:100001,ExpectedRevision:10),new(ExpectedRevision:0)})Assert.Throws<ArgumentException>(()=>WorkspaceTodoHierarchy.Evaluate(document,filter));
        Assert.Equal("stale_revision",Assert.Throws<InvalidOperationException>(()=>WorkspaceTodoHierarchy.Evaluate(document,new(ExpectedRevision:9))).Message);
        foreach(var id in new[]{Guid.NewGuid(),document.Tasks[2].Id,document.Tasks[3].Id})Assert.Equal("task_unavailable",Assert.Throws<InvalidOperationException>(()=>WorkspaceTodoHierarchy.Evaluate(document,new(id))).Message);
        foreach(var parent in new[]{Guid.NewGuid(),document.Tasks[0].Id,document.Tasks[4].Id})
        {var broken=document with {Tasks=document.Tasks.Select((task,index)=>index==0?task with {ParentTaskId=parent}:task).ToArray()};Assert.Equal("invalid_workspace",Assert.Throws<InvalidOperationException>(()=>WorkspaceTodoHierarchy.Evaluate(broken,new())).Message);}
    }
}
