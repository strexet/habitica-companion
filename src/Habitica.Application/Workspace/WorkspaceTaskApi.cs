using System.Text.Json;
using System.Text.Json.Serialization;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Workspace;

public sealed record WorkspaceTaskResponse<T>(bool Succeeded, T? Value, string Message, bool OutcomeUnknown = false);
public sealed record WorkspaceCommandPreview(Guid PreparedCommandId, Guid CorrelationId, DateTimeOffset ExpiresAtUtc, bool Expired, WorkspaceCommand Command, bool MayAffectHabitica, bool SavedInCompanion, WorkspaceMutationReceipt? Receipt = null, WorkspaceTagChangePreview? TagProjection = null);
public sealed record WorkspaceMutationReceipt(string Operation, Guid TaskId, Guid CommandId, Guid CorrelationId, string TaskText, bool SavedInCompanion, string HabiticaProjectionStatus, int AcceptedEventCount, int ConfirmedProjectionCount, int TotalProjectionCount, int? FailurePosition, string Failure, DateTimeOffset? RetryAtUtc, bool RefreshSucceeded, bool OutcomeUnknown);
public sealed record WorkspaceExternalTask(Guid Id, string Kind, string Text, string Notes, string? Alias, double Priority, string Attribute, string? Date, IReadOnlyList<TaskChecklistItem> Checklist, bool Completed, bool Up, bool Down, bool? IsDue, string Fingerprint, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] WorkspaceExternalDailySchedule? Schedule = null);
public sealed record WorkspaceExternalDailySchedule(string Frequency, int EveryX, DateTimeOffset StartDate, IReadOnlyDictionary<string, bool> Repeat, IReadOnlyList<int> DaysOfMonth, IReadOnlyList<int> WeeksOfMonth)
{
    public bool IsValid() => Frequency is "daily" or "weekly" or "monthly" or "yearly" && EveryX is >= 0 and <= 9999
        && StartDate != default && StartDate.Offset == TimeSpan.Zero && Repeat is not null && Repeat.Count == 7
        && new[] { "su", "m", "t", "w", "th", "f", "s" }.All(Repeat.ContainsKey)
        && DaysOfMonth is not null && DaysOfMonth.All(day => day is >= 1 and <= 31) && DaysOfMonth.Distinct().Count() == DaysOfMonth.Count
        && WeeksOfMonth is not null && WeeksOfMonth.All(week => week is >= 1 and <= 5) && WeeksOfMonth.Distinct().Count() == WeeksOfMonth.Count;
}
public sealed record WorkspaceProjectionPreview(Guid WorkspaceId, Guid AccessEpoch, CompanionTask Task, WorkspaceExternalTask External, long ExpectedRevision, int PendingCommandCount,
    Guid PreparedCommandId = default, Guid RequestId = default, string? ReviewHash = null, DateTimeOffset ExpiresAtUtc = default, Guid ConnectionId = default,
    HabiticaProjection? MappingBefore = null, HabiticaProjection? MappingAfter = null, string? Disclosure = null,
    Guid CommandId = default, string? State = null, bool SavedInCompanion = false, bool OutcomeUnknown = false, int GameEffects = 0, int NativeWrites = 0, string? Error = null);

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
