using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Tests.Workspace;

public sealed class WorkspaceCompletionHistoryTests
{
    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","completion-history-v1.json")));
    private static WorkspaceJournalSnapshot Snapshot(JsonDocument fixture) => new(WorkspaceJson.Deserialize(fixture.RootElement.GetProperty("document").GetRawText()),20,
        WorkspaceTaskApiJson.Deserialize<WorkspaceCommandEntry[]>(fixture.RootElement.GetProperty("commands").GetRawText()),[]);
    [Fact]
    public void Portable_history_preserves_details_archive_flags_and_precise_ordering_without_mutation()
    {
        using var fixture=Fixture();var snapshot=Snapshot(fixture);Assert.True(WorkspaceJournalValidation.IsValid(snapshot));var before=WorkspaceJournalJson.Serialize(snapshot);
        foreach(var value in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var filter=WorkspaceTaskApiJson.Deserialize<WorkspaceHistoryFilter>(value.GetProperty("filter").GetRawText());var page=WorkspaceCompletionHistory.Evaluate(snapshot,filter);
            Assert.Equal(WorkspaceTaskApiJson.Deserialize<WorkspaceCompletionItem[]>(value.GetProperty("items").GetRawText()),page.Items);
            Assert.Equal(value.GetProperty("matchedCount").GetInt32(),page.MatchedCount);Assert.Equal(value.GetProperty("offset").GetInt32(),page.Offset);Assert.Equal(value.GetProperty("truncated").GetBoolean(),page.Truncated);
            Assert.Equal(value.GetProperty("nextOffset").ValueKind==JsonValueKind.Null?(int?)null:value.GetProperty("nextOffset").GetInt32(),page.NextOffset);
        }
        Assert.Equal(before,WorkspaceJournalJson.Serialize(snapshot));
    }
    [Fact]
    public void Invalid_history_filters_stale_pages_and_inconsistent_snapshots_fail_explicitly()
    {
        using var fixture=Fixture();var snapshot=Snapshot(fixture);
        foreach(var filter in new WorkspaceHistoryFilter[]{new(Kind:CompanionTaskKind.Habit),new(TaskId:Guid.Empty),new(Limit:0),new(Limit:101),new(Offset:1),new(Offset:100001),new(ExpectedRevision:0),new(Query:new string('x',257)),new(CompletedFromUtc:DateTimeOffset.MinValue),new(CompletedFromUtc:DateTimeOffset.Parse("2026-10-18T00:00:00+03:00")),new(CompletedFromUtc:DateTimeOffset.Parse("2026-10-18T00:00:00Z"),CompletedThroughUtc:DateTimeOffset.Parse("2026-10-17T00:00:00Z"))})
            Assert.Throws<ArgumentException>(()=>WorkspaceCompletionHistory.Evaluate(snapshot,filter));
        Assert.Equal("stale_revision",Assert.Throws<InvalidOperationException>(()=>WorkspaceCompletionHistory.Evaluate(snapshot,new(ExpectedRevision:19))).Message);
        var entry=snapshot.Commands[0];
        foreach(var wrong in new[]{entry with { Command=entry.Command with { Operation=WorkspaceOperation.UpdateTask } },entry with { TaskSnapshot=entry.TaskSnapshot! with { Id=Guid.NewGuid() } }})
            Assert.Equal("history_unavailable",Assert.Throws<InvalidOperationException>(()=>WorkspaceCompletionHistory.Evaluate(snapshot with { Commands=[wrong] },new())).Message);
        var legacy=WorkspaceCompletionHistory.Evaluate(snapshot with { Commands=[] },new(TaskId:entry.Command.TaskId));Assert.Equal("currentTask",Assert.Single(legacy.Items).DetailSource);Assert.Equal("Renamed current task",legacy.Items[0].TaskText);
    }
    [Fact]
    public void Canonical_history_retains_more_than_the_legacy_recent_completed_limit()
    {
        using var fixture=Fixture();var snapshot=Snapshot(fixture);var task=snapshot.Document.Tasks.First(value=>value.Kind==CompanionTaskKind.Daily);var occurrences=snapshot.Document.Occurrences.ToList();var completions=snapshot.Document.Completions.ToList();
        for(var index=0;index<35;index++)
        {
            var date=new DateOnly(2026,8,1).AddDays(index);var occurrence=new TaskOccurrence(Guid.NewGuid(),20,task.Id,date,OccurrenceState.Completed);occurrences.Add(occurrence);
            completions.Add(new(Guid.NewGuid(),20,task.Id,Guid.NewGuid(),new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue),TimeSpan.Zero),occurrence.Id));
        }
        snapshot=snapshot with { Document=snapshot.Document with { Occurrences=occurrences,Completions=completions } };
        var all=WorkspaceCompletionHistory.Evaluate(snapshot,new(Limit:100));Assert.Equal(41,all.MatchedCount);Assert.Equal(41,all.Items.Count);
        var page=WorkspaceCompletionHistory.Evaluate(snapshot,new(Limit:10));Assert.Equal(41,page.MatchedCount);Assert.Equal(10,page.Items.Count);Assert.Equal(10,page.NextOffset);
    }
}
