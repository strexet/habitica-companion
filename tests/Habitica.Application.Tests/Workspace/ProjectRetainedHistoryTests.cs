using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
namespace Habitica.Application.Tests.Workspace;
public sealed class ProjectRetainedHistoryTests
{
    [Fact]public void Exact_project_history_uses_current_assignment_while_preserving_accepted_event_details()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","completion-history-v1.json")));var doc=WorkspaceJson.Deserialize(data.RootElement.GetProperty("document").GetRawText());var taskId=Guid.Parse("60000000-0000-4000-8000-000000000105");var project=doc.Projects[1];doc=doc with{Tasks=doc.Tasks.Select(t=>t.Id==taskId?t with{ProjectId=project.Id}:t).ToArray()};Assert.Empty(WorkspaceRules.Validate(doc));var commands=WorkspaceTaskApiJson.Deserialize<WorkspaceCommandEntry[]>(data.RootElement.GetProperty("commands").GetRawText());var state=new WorkspaceJournalSnapshot(doc,doc.Revision,commands,[]);var filter=WorkspaceTaskApiJson.Deserialize<WorkspaceHistoryFilter>("{\"projectId\":\""+project.Id.ToString("D")+"\"}");var page=WorkspaceCompletionHistory.Evaluate(state,filter);Assert.Equal(taskId,Assert.Single(page.Items).TaskId);Assert.Equal("commandSnapshot",page.Items[0].DetailSource);Assert.NotEqual(doc.Tasks.Single(t=>t.Id==taskId).Text,page.Items[0].TaskText);
    }

    [Fact]public void Independent_project_history_examples_match_current_retained_placement_precise_pages_and_unchanged_event_provenance()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","project-retained-history-v1.json")));foreach(var section in data.RootElement.GetProperty("sections").EnumerateArray()){
            var raw=WorkspaceTaskApiJson.Deserialize<WorkspaceDocument>(section.GetProperty("document").GetRawText());Assert.True(WorkspaceRules.Validate(raw).Count==0,string.Join(";",WorkspaceRules.Validate(raw).Select(e=>e.Code+":"+e.EntityId)));var doc=WorkspaceJson.Deserialize(section.GetProperty("document").GetRawText());var state=new WorkspaceJournalSnapshot(doc,30,WorkspaceTaskApiJson.Deserialize<WorkspaceCommandEntry[]>(section.GetProperty("commands").GetRawText()),[]);var before=WorkspaceJournalJson.Serialize(state);
            foreach(var row in section.GetProperty("cases").EnumerateArray()){
                if(section.GetProperty("kind").GetString()=="completion"){var page=WorkspaceCompletionHistory.Evaluate(state,WorkspaceTaskApiJson.Deserialize<WorkspaceHistoryFilter>(row.GetProperty("filter").GetRawText()));Assert.Equal(WorkspaceTaskApiJson.Deserialize<WorkspaceCompletionItem[]>(row.GetProperty("items").GetRawText()),page.Items);Assert.Equal(row.GetProperty("matchedCount").GetInt32(),page.MatchedCount);Assert.Equal(row.GetProperty("truncated").GetBoolean(),page.Truncated);Assert.Equal(row.GetProperty("offset").GetInt32(),page.Offset);Assert.Equal(row.GetProperty("nextOffset").ValueKind==JsonValueKind.Null?(int?)null:row.GetProperty("nextOffset").GetInt32(),page.NextOffset);}
                else{var page=WorkspaceHabitHistory.Evaluate(state,WorkspaceTaskApiJson.Deserialize<WorkspaceHabitHistoryFilter>(row.GetProperty("filter").GetRawText()));Assert.Equal(WorkspaceTaskApiJson.Deserialize<WorkspaceHabitHistoryItem[]>(row.GetProperty("items").GetRawText()),page.Items);Assert.Equal(row.GetProperty("matchedCount").GetInt32(),page.MatchedCount);Assert.Equal(row.GetProperty("truncated").GetBoolean(),page.Truncated);Assert.Equal(row.GetProperty("offset").GetInt32(),page.Offset);Assert.Equal(row.GetProperty("nextOffset").ValueKind==JsonValueKind.Null?(int?)null:row.GetProperty("nextOffset").GetInt32(),page.NextOffset);}
            }Assert.Equal(before,WorkspaceJournalJson.Serialize(state));
            Assert.Throws<ArgumentException>(()=>WorkspaceCompletionHistory.Evaluate(state,new(ProjectId:Guid.NewGuid(),Unassigned:true)));Assert.Throws<ArgumentException>(()=>WorkspaceHabitHistory.Evaluate(state,new(ProjectId:Guid.Empty)));Assert.Equal("history_project_not_found",Assert.Throws<InvalidOperationException>(()=>WorkspaceCompletionHistory.Evaluate(state,new(ProjectId:Guid.NewGuid()))).Message);Assert.Equal("stale_revision",Assert.Throws<InvalidOperationException>(()=>WorkspaceHabitHistory.Evaluate(state,new(ProjectId:doc.Projects[1].Id,ExpectedRevision:29))).Message);
        }
    }

    [Fact]public void Retained_project_discovery_preserves_default_active_reads_and_supports_exact_archived_or_removed_history_choices()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","project-retained-history-v1.json")));var section=data.RootElement.GetProperty("sections")[0];var doc=WorkspaceJson.Deserialize(section.GetProperty("document").GetRawText());Assert.Equal(2,WorkspaceOrganization.Projects(doc,new()).MatchedCount);var page=WorkspaceOrganization.Projects(doc,new(Limit:3),includeRetained:true);Assert.Equal(4,page.MatchedCount);Assert.Equal(3,page.Items.Count);Assert.True(page.Truncated);var next=WorkspaceOrganization.Projects(doc,new(Limit:3,Offset:3,ExpectedRevision:30),includeRetained:true);Assert.True(Assert.Single(next.Items).IsRemoved);Assert.Equal(doc.Projects.Select(p=>p.Id),page.Items.Concat(next.Items).Select(p=>p.Id));
    }
}
