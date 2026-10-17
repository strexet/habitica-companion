using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Tests.Workspace;

public sealed class WorkspaceTaskQueryTests
{
    [Fact]
    public void Portable_filters_and_pages_match_neutral_fixtures_without_mutation()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "task-query-v1.json")));
        var document = WorkspaceJson.Deserialize(fixture.RootElement.GetProperty("document").GetRawText());
        var before = WorkspaceJson.Serialize(document);
        foreach (var value in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var filter = WorkspaceTaskApiJson.Deserialize<WorkspaceTaskFilter>(value.GetProperty("filter").GetRawText());
            var page = WorkspaceTaskQuery.Evaluate(document, filter);
            Assert.Equal(value.GetProperty("taskIds").EnumerateArray().Select(id => id.GetGuid()), page.Tasks.Select(task => task.Id));
            Assert.Equal(value.GetProperty("matchedCount").GetInt32(), page.MatchedCount);
            Assert.Equal(value.GetProperty("offset").GetInt32(), page.Offset);
            Assert.Equal(value.GetProperty("nextOffset").ValueKind == JsonValueKind.Null ? (int?)null : value.GetProperty("nextOffset").GetInt32(), page.NextOffset);
            Assert.Equal(value.GetProperty("truncated").GetBoolean(), page.Truncated);
            Assert.Equal(document.Id, page.WorkspaceId); Assert.Equal(document.Revision, page.Revision);
        }
        Assert.Equal(before, WorkspaceJson.Serialize(document));
    }

    [Fact]
    public void Invalid_filters_and_stale_pages_cannot_silently_change_results()
    {
        var document = new WorkspaceDocument { Id = Guid.NewGuid(), Revision = 10 };
        foreach (var filter in new WorkspaceTaskFilter[] { new(Limit: 0), new(Limit: 101), new(Offset: 1), new(Offset: -1), new(Offset: 100001),
            new(ExpectedRevision: 0), new(ExpectedRevision: WorkspaceDocument.MaxRevision + 1), new(ProjectId: Guid.Empty), new(CategoryId: Guid.Empty), new(LabelId: Guid.Empty),
            new(Kind: (CompanionTaskKind)99), new(State: (CompanionTaskState)99), new(Priority: (CompanionPriority)99), new(ProjectionState: (ProjectionState)99),
            new(Query: new string('x',257)), new(PlannedFrom: DateOnly.MinValue), new(DeadlineTo: DateOnly.MinValue), new(PlannedFrom: new(2026,10,20), PlannedTo: new(2026,10,19)) })
            Assert.Throws<ArgumentException>(() => WorkspaceTaskQuery.Evaluate(document, filter));
        Assert.Equal("stale_revision", Assert.Throws<InvalidOperationException>(() => WorkspaceTaskQuery.Evaluate(document, new(ExpectedRevision: 9))).Message);
        Assert.Equal("stale_revision", Assert.Throws<InvalidOperationException>(() => WorkspaceTaskQuery.Evaluate(document, new(Offset: 2, ExpectedRevision: 9))).Message);
        Assert.Throws<JsonException>(() => WorkspaceTaskApiJson.Deserialize<WorkspaceTaskFilter>("{\"accountId\":\"other\"}"));
    }
}
