using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Tests.Workspace;

public sealed class WorkspaceHabitHistoryTests
{
    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","habit-history-v1.json")));
    private static WorkspaceJournalSnapshot Seed(JsonElement fixture) => new(WorkspaceJson.Deserialize(fixture.GetProperty("document").GetRawText()),20,WorkspaceTaskApiJson.Deserialize<WorkspaceCommandEntry[]>(fixture.GetProperty("commands").GetRawText()),[]);
    [Fact]
    public void Neutral_filters_history_provenance_and_precise_paging_match_expected_events()
    {
        using var fixture=Fixture();var state=Seed(fixture.RootElement);Assert.True(WorkspaceJournalValidation.IsValid(state));var before=WorkspaceJournalJson.Serialize(state);
        foreach(var item in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var page=WorkspaceHabitHistory.Evaluate(state,WorkspaceTaskApiJson.Deserialize<WorkspaceHabitHistoryFilter>(item.GetProperty("filter").GetRawText()));
            var expected=WorkspaceTaskApiJson.Deserialize<WorkspaceHabitHistoryItem[]>(item.GetProperty("items").GetRawText());Assert.Equal(expected,page.Items);
            Assert.Equal(item.GetProperty("matchedCount").GetInt32(),page.MatchedCount);Assert.Equal(item.GetProperty("offset").GetInt32(),page.Offset);Assert.Equal(item.GetProperty("truncated").GetBoolean(),page.Truncated);
            Assert.Equal(item.GetProperty("nextOffset").ValueKind==JsonValueKind.Null?(int?)null:item.GetProperty("nextOffset").GetInt32(),page.NextOffset);
        }
        Assert.Equal(before,WorkspaceJournalJson.Serialize(state));
    }
    [Fact]
    public void Invalid_filters_stale_pages_and_wrong_snapshots_fail_explicitly()
    {
        using var fixture=Fixture();var state=Seed(fixture.RootElement);
        foreach(var item in fixture.RootElement.GetProperty("invalid").EnumerateArray())Assert.ThrowsAny<Exception>(()=>WorkspaceHabitHistory.Evaluate(state,WorkspaceTaskApiJson.Deserialize<WorkspaceHabitHistoryFilter>(item.GetRawText())));
        Assert.Throws<InvalidOperationException>(()=>WorkspaceHabitHistory.Evaluate(state,new(ExpectedRevision:19)));
        var first=state.Commands[0];
        foreach(var entry in new[]{first with { Command=first.Command with { Operation=WorkspaceOperation.UpdateTask } },first with { Command=first.Command with { Direction=HabitScoreDirection.Down } },first with { Command=first.Command with { TaskId=Guid.NewGuid() } },first with { TaskSnapshot=first.TaskSnapshot! with { Id=Guid.NewGuid() } },first with { TaskSnapshot=first.TaskSnapshot! with { Kind=CompanionTaskKind.Todo } }})Assert.Throws<InvalidOperationException>(()=>WorkspaceHabitHistory.Evaluate(state with { Commands=[entry] },new()));
        var fallback=WorkspaceHabitHistory.Evaluate(state with { Commands=[] },new(Query:"current-only"));Assert.Equal(5,fallback.Items.Count);Assert.All(fallback.Items,row=>Assert.Equal("currentTask",row.DetailSource));
    }
    [Fact]
    public void Batched_events_remain_distinct_beyond_legacy_history_limits()
    {
        using var fixture=Fixture();var state=Seed(fixture.RootElement);var task=state.Document.Tasks[0];
        var extra=Enumerable.Range(0,40).Select(index=>new HabitScoreEvent(Guid.NewGuid(),20,task.Id,Guid.NewGuid(),DateTimeOffset.Parse("2026-10-18T00:00:00Z"),HabitScoreDirection.Up));
        state=state with { Document=state.Document with { HabitScores=state.Document.HabitScores.Concat(extra).ToArray() } };
        var page=WorkspaceHabitHistory.Evaluate(state,new(Limit:100));Assert.Equal(47,page.MatchedCount);Assert.Equal(47,page.Items.Count);Assert.Equal(47,page.Items.Select(row=>row.EventId).Distinct().Count());
    }
}
