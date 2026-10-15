using System.Text.Json;
using System.Text.Json.Serialization;

namespace Habitica.Domain.Workspace;

public static class WorkspaceJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 32
    };

    public static string Serialize(WorkspaceDocument document)
    {
        RequireValid(document);
        return JsonSerializer.Serialize(document, Options);
    }

    public static WorkspaceDocument Deserialize(string json)
    {
        if (json.Length > 16 * 1024 * 1024) throw new JsonException("Workspace exceeds the import bound.");
        using var parsed = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
        var pending = new Stack<JsonElement>();
        pending.Push(parsed.RootElement);
        while (pending.TryPop(out var value))
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in value.EnumerateObject())
                {
                    if (!names.Add(property.Name)) throw new JsonException("Duplicate workspace property.");
                    pending.Push(property.Value);
                }
            }
            else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) pending.Push(item);
        }
        var document = parsed.Deserialize<WorkspaceDocument>(Options) ?? throw new JsonException("Workspace is required.");
        RequireValid(document);
        return document;
    }

    private static void RequireValid(WorkspaceDocument document)
    {
        if (WorkspaceRules.Validate(document).Count != 0) throw new JsonException("Workspace violates the schema or domain invariants.");
    }
}
