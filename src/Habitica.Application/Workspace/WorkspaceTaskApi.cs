using System.Text.Json;
using System.Text.Json.Serialization;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Workspace;

public sealed record WorkspaceTaskResponse<T>(bool Succeeded, T? Value, string Message, bool OutcomeUnknown = false);
public sealed record WorkspaceCommandPreview(Guid PreparedCommandId, Guid CorrelationId, DateTimeOffset ExpiresAtUtc, bool Expired, WorkspaceCommand Command, bool MayAffectHabitica, bool SavedInCompanion, WorkspaceMutationReceipt? Receipt = null);
public sealed record WorkspaceMutationReceipt(string Operation, Guid TaskId, Guid CommandId, Guid CorrelationId, string TaskText, bool SavedInCompanion, string HabiticaProjectionStatus, int AcceptedEventCount, int ConfirmedProjectionCount, int TotalProjectionCount, int? FailurePosition, string Failure, DateTimeOffset? RetryAtUtc, bool RefreshSucceeded, bool OutcomeUnknown);
public sealed record WorkspaceExternalTask(Guid Id, string Kind, string Text, string Notes, string? Alias, double Priority, string Attribute, string? Date, IReadOnlyList<TaskChecklistItem> Checklist, bool Completed, bool Up, bool Down, bool? IsDue, string Fingerprint);
public sealed record WorkspaceProjectionPreview(Guid WorkspaceId, Guid AccessEpoch, CompanionTask Task, WorkspaceExternalTask External, long ExpectedRevision, int PendingCommandCount);

public static class WorkspaceTaskApiJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 32
    };
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options) ?? throw new JsonException("Workspace response is required.");
    public static JsonElement Element<T>(T value) => JsonSerializer.SerializeToElement(value, Options);
}
