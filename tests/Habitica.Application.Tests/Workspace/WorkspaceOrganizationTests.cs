using System.Text.Json;
using System.Text.Json.Nodes;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Tests.Workspace;

public sealed class WorkspaceOrganizationTests
{
    [Fact]
    public void Portable_lists_preserve_metadata_order_and_Todo_progress_without_mutation()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","organization-v1.json")));
        var document = WorkspaceJson.Deserialize(fixture.RootElement.GetProperty("document").GetRawText()); var before = WorkspaceJson.Serialize(document);
        foreach (var value in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var args = value.GetProperty("arguments");
            var filter = new WorkspaceReadFilter(args.TryGetProperty("limit",out var limit) ? limit.GetInt32() : 50,args.TryGetProperty("offset",out var offset) ? offset.GetInt32() : 0,args.TryGetProperty("expectedRevision",out var revision) ? revision.GetInt64() : null);
            void Check<T>(WorkspaceReadPage<T> page)
            {
                Assert.True(JsonNode.DeepEquals(JsonNode.Parse(value.GetProperty("items").GetRawText()),JsonNode.Parse(WorkspaceTaskApiJson.Serialize(page.Items))),value.GetProperty("name").GetString());
                Assert.Equal(value.GetProperty("matchedCount").GetInt32(),page.MatchedCount); Assert.Equal(value.GetProperty("offset").GetInt32(),page.Offset);
                Assert.Equal(value.GetProperty("truncated").GetBoolean(),page.Truncated);
                Assert.Equal(value.GetProperty("nextOffset").ValueKind == JsonValueKind.Null ? (int?)null : value.GetProperty("nextOffset").GetInt32(),page.NextOffset);
                Assert.Equal(document.Id,page.WorkspaceId); Assert.Equal(document.Revision,page.Revision);
            }
            switch(value.GetProperty("tool").GetString())
            {
                case "list_categories": Check(WorkspaceOrganization.Categories(document,filter)); break;
                case "list_projects": Check(WorkspaceOrganization.Projects(document,filter,args.TryGetProperty("categoryId",out var category) ? category.GetGuid() : null)); break;
                case "list_project_sections": Check(WorkspaceOrganization.Sections(document,filter,args.GetProperty("projectId").GetGuid())); break;
                case "list_labels": Check(WorkspaceOrganization.Labels(document,filter)); break;
                case "list_goals": Check(WorkspaceOrganization.Goals(document,filter)); break;
                default: throw new InvalidOperationException("Unknown fixture tool");
            }
        }
        Assert.Equal(before,WorkspaceJson.Serialize(document));
    }
    [Fact]
    public void Invalid_page_identity_and_stale_revision_fail_explicitly()
    {
        var document = new WorkspaceDocument { Id = Guid.NewGuid(),Revision = 10 };
        foreach(var filter in new WorkspaceReadFilter[] { new(Limit:0),new(Limit:101),new(Offset:1),new(Offset:100001),new(ExpectedRevision:0) })
            Assert.Throws<ArgumentException>(() => WorkspaceOrganization.Categories(document,filter));
        Assert.Equal("stale_revision",Assert.Throws<InvalidOperationException>(() => WorkspaceOrganization.Goals(document,new(ExpectedRevision:9))).Message);
        Assert.Throws<ArgumentException>(() => WorkspaceOrganization.Projects(document,new(),Guid.Empty));
        Assert.Throws<ArgumentException>(() => WorkspaceOrganization.Sections(document,new(),Guid.Empty));
    }
}
