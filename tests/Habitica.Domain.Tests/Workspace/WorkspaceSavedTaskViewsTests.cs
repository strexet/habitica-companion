using System.Text.Json;
using System.Text.Json.Nodes;
using Habitica.Domain.Workspace;
namespace Habitica.Domain.Tests.Workspace;
public sealed class WorkspaceSavedTaskViewsTests
{
    [Fact]
    public void Portable_definitions_validate_exact_references_and_omit_unused_defaults()
    {
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","saved-task-views-v1.json")));
        var raw=fixture.RootElement.GetProperty("document").GetRawText();var document=WorkspaceJson.Deserialize(raw);var json=WorkspaceJson.Serialize(document);Assert.Equal(json,WorkspaceJson.Serialize(WorkspaceJson.Deserialize(json)));Assert.Equal(14,document.SavedTaskViews!.Count);
        foreach(var invalid in fixture.RootElement.GetProperty("invalid").EnumerateArray())
        {
            var altered=JsonNode.Parse(raw)!;altered["savedTaskViews"]=JsonNode.Parse(invalid.GetProperty("views").GetRawText());Assert.Throws<JsonException>(()=>WorkspaceJson.Deserialize(altered.ToJsonString()));
        }
        var empty=new WorkspaceDocument {Id=Guid.NewGuid()};Assert.DoesNotContain("savedTaskViews",WorkspaceJson.Serialize(empty));Assert.DoesNotContain("savedTaskViews",WorkspaceJson.Serialize(empty with {SavedTaskViews=null}));Assert.Contains("\"savedTaskViews\":[]",WorkspaceJson.Serialize(empty with {SavedTaskViews=[]}));
        Assert.Contains(WorkspaceRules.Validate(document with {SavedTaskViews=[document.SavedTaskViews[0] with {Query=new((WorkspaceSavedTaskSource)99,new())}]}),e=>e.Code=="invalid_saved_task_view");
    }
}
