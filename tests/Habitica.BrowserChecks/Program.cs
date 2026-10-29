using System.Reflection;
using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<BrowserChecks>("#app");
await builder.Build().RunAsync();

public sealed class BrowserChecks : ComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var passed = 0;
        string result;
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("task-query-v1.json")!;
            using var fixture = JsonDocument.Parse(stream);
            var document = WorkspaceJson.Deserialize(fixture.RootElement.GetProperty("document").GetRawText());
            var before = WorkspaceJson.Serialize(document);
            foreach (var row in fixture.RootElement.GetProperty("cases").EnumerateArray())
            {
                var filter = WorkspaceTaskApiJson.Deserialize<WorkspaceTaskFilter>(row.GetProperty("filter").GetRawText());
                var page = WorkspaceTaskQuery.Evaluate(document, filter);
                var ids = row.GetProperty("taskIds").EnumerateArray().Select(value => value.GetGuid());
                var next = row.GetProperty("nextOffset");
                if (!ids.SequenceEqual(page.Tasks.Select(task => task.Id))
                    || page.MatchedCount != row.GetProperty("matchedCount").GetInt32()
                    || page.Offset != row.GetProperty("offset").GetInt32()
                    || page.NextOffset != (next.ValueKind == JsonValueKind.Null ? (int?)null : next.GetInt32())
                    || page.Truncated != row.GetProperty("truncated").GetBoolean()
                    || before != WorkspaceJson.Serialize(document))
                    throw new InvalidOperationException($"Fixture mismatch at case {passed + 1}");
                passed++;
            }
            result = $"PASS: {passed} task-query cases in browser WebAssembly. Original document unchanged.";
        }
        catch (Exception error)
        {
            result = $"FAIL after {passed} cases: {error}";
        }
        builder.OpenElement(0, "h1");
        builder.AddContent(1, "Workspace browser checks");
        builder.CloseElement();
        builder.OpenElement(2, "pre");
        builder.AddAttribute(3, "data-testid", "browser-check-result");
        builder.AddContent(4, result);
        builder.CloseElement();
    }
}
