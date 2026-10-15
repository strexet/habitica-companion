using System.Text.Json;
using System.Text.Json.Serialization;

namespace Habitica.Domain.Workspace;

public static class WorkspaceJournalJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 32
    };

    public static string Serialize(WorkspaceJournalSnapshot snapshot)
    {
        if (!WorkspaceJournalValidation.IsValid(snapshot)) throw new JsonException("Workspace journal is invalid.");
        var json = JsonSerializer.Serialize(snapshot with { Events = snapshot.Events ?? Array.Empty<WorkspaceJournalEvent>() }, Options);
        if (json.Length > 16 * 1024 * 1024) throw new JsonException("Workspace journal exceeds the local storage bound.");
        return json;
    }

    public static WorkspaceJournalSnapshot Deserialize(string json)
    {
        using var parsed = WorkspaceJson.ParseStrict(json);
        var snapshot = parsed.Deserialize<WorkspaceJournalSnapshot>(Options) ?? throw new JsonException("Workspace journal is required.");
        if (!WorkspaceJournalValidation.IsValid(snapshot)) throw new JsonException("Workspace journal is invalid.");
        return snapshot with { Events = snapshot.Events ?? Array.Empty<WorkspaceJournalEvent>() };
    }
}
