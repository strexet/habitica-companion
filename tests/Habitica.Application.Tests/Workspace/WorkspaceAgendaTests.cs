using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Tests.Workspace;

public sealed class WorkspaceAgendaTests
{
    [Fact]
    public void Portable_agenda_preserves_unique_date_reasons_and_retained_history()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "agenda-v1.json")));
        var document = WorkspaceJson.Deserialize(fixture.RootElement.GetProperty("document").GetRawText());
        var before = WorkspaceJson.Serialize(document);
        foreach (var value in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var filter = WorkspaceTaskApiJson.Deserialize<WorkspaceAgendaFilter>(value.GetProperty("filter").GetRawText());
            var page = WorkspaceAgenda.Evaluate(document, filter);
            var expected = WorkspaceTaskApiJson.Deserialize<WorkspaceAgendaItem[]>(value.GetProperty("items").GetRawText());
            Assert.Equal(expected, page.Items);
            Assert.Equal(page.Items.Count, page.Items.Select(item => (item.TaskId,item.Date)).Distinct().Count());
            Assert.Equal(value.GetProperty("matchedCount").GetInt32(), page.MatchedCount);
            Assert.Equal(value.GetProperty("offset").GetInt32(), page.Offset);
            Assert.Equal(value.GetProperty("nextOffset").ValueKind == JsonValueKind.Null ? (int?)null : value.GetProperty("nextOffset").GetInt32(), page.NextOffset);
            Assert.Equal(value.GetProperty("truncated").GetBoolean(), page.Truncated);
        }
        Assert.Equal(before, WorkspaceJson.Serialize(document));
    }

    [Fact]
    public void Invalid_ranges_stale_pages_unavailable_rules_and_capacity_fail_without_partial_results()
    {
        var from = new DateOnly(2026,10,17); var document = new WorkspaceDocument { Id = Guid.NewGuid(), Revision = 10 };
        foreach (var filter in new WorkspaceAgendaFilter[] { new(default,from), new(from,from.AddDays(-1)), new(from,from.AddDays(366)),
            new(from,from,Limit:0), new(from,from,Limit:101), new(from,from,Offset:1), new(from,from,Offset:10001), new(from,from,ExpectedRevision:0) })
            Assert.Throws<ArgumentException>(() => WorkspaceAgenda.Evaluate(document,filter));
        Assert.Equal("stale_revision",Assert.Throws<InvalidOperationException>(() => WorkspaceAgenda.Evaluate(document,new(from,from,ExpectedRevision:9))).Message);
        var rule = new RecurrenceRule(RecurrenceFrequency.Daily,1,from,"UTC",[],[]);
        var task = new CompanionTask(Guid.NewGuid(),10,"Daily",CompanionTaskKind.Daily,CompanionTaskState.Active,CompanionPriority.Normal,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,Recurrence:rule);
        Assert.Equal("recurrence_unavailable",Assert.Throws<InvalidOperationException>(() => WorkspaceAgenda.Evaluate(document with { Tasks = [task with { Recurrence = rule with { TimeZoneId = "Unavailable/Zone" } }] },new(from,from))).Message);
        var many = document with { Tasks = Enumerable.Range(0,1001).Select(_ => task with { Id = Guid.NewGuid() }).ToArray() };
        Assert.Equal("agenda_capacity",Assert.Throws<InvalidOperationException>(() => WorkspaceAgenda.Evaluate(many,new(from,from.AddDays(365)))).Message);
        many = document with { Tasks = Enumerable.Range(0,28).Select(_ => task with { Id = Guid.NewGuid() }).ToArray() };
        Assert.Equal("agenda_capacity",Assert.Throws<InvalidOperationException>(() => WorkspaceAgenda.Evaluate(many,new(from,from.AddDays(365)))).Message);
        Assert.Equal(366,WorkspaceAgenda.Evaluate(document with { Tasks = [task] },new(from,from.AddDays(365))).MatchedCount);
    }
}
