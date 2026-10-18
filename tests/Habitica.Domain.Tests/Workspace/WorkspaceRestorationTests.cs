using System.Text.Json;
using Habitica.Domain.Workspace;

namespace Habitica.Domain.Tests.Workspace;

public sealed class WorkspaceRestorationTests
{
    [Fact]
    public void Portable_cycle_validation_and_roundtrip_preserve_legacy_defaults_and_Todo_progress()
    {
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","todo-restoration-cycles-v1.json")));
        foreach(var value in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var raw=value.GetProperty("document").GetRawText();
            if(!value.GetProperty("valid").GetBoolean()){Assert.Throws<JsonException>(()=>WorkspaceJson.Deserialize(raw));continue;}
            var document=WorkspaceJson.Deserialize(raw);var encoded=WorkspaceJson.Serialize(document);var restored=WorkspaceJson.Deserialize(encoded);Assert.Equal(encoded,WorkspaceJson.Serialize(restored));
            Assert.Equal(document.Completions.Select(row=>row.Id),restored.Completions.Select(row=>row.Id));Assert.Equal((document.Restorations??[]).Select(row=>row.Id),(restored.Restorations??[]).Select(row=>row.Id));
            var progress=WorkspaceRules.Progress(document.Goals[0],document.Tasks);Assert.Equal(value.GetProperty("completedTodos").GetInt32(),progress.CompletedTodos);Assert.Equal(value.GetProperty("eligibleTodos").GetInt32(),progress.EligibleTodos);
            using var json=JsonDocument.Parse(encoded);var hasExtension=value.GetProperty("document").TryGetProperty("restorations",out var property)&&property.ValueKind!=JsonValueKind.Null;Assert.Equal(hasExtension,json.RootElement.TryGetProperty("restorations",out _));
        }
    }
}
