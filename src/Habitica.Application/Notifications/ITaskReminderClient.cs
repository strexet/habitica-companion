using System.Text.Json.Serialization;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Notifications;

public sealed record ReminderTarget(Guid DeviceId, long Revision, Guid SubscriptionId);
public sealed record ReminderRepeat(RecurrenceRule Rule, string Time, DateOnly Through, string Gap, string Overlap);
public sealed record ReminderIntent(string Operation, Guid? ScheduleId, long ExpectedRevision, Guid TaskId, DateTimeOffset? ReviewAfterUtc, string TimeZoneId,
    ReminderRepeat? Repeat, IReadOnlyList<ReminderTarget> Targets, string ConsentVersion = "scheduled-reminders-v1");
public sealed record ReminderPreparation(Guid RequestId, long ExpectedWorkspaceRevision, ReminderIntent Intent);
public sealed record ReminderReceipt([property: JsonRequired] Guid CommandId, [property: JsonRequired] Guid RequestId, [property: JsonRequired] Guid WorkspaceId,
    [property: JsonRequired] Guid ScheduleId, [property: JsonRequired] long Revision, [property: JsonRequired] Guid TaskId, [property: JsonRequired] string Operation,
    [property: JsonRequired] string State, [property: JsonRequired] bool Saved, [property: JsonRequired] bool Scheduled, [property: JsonRequired] bool Dispatched,
    [property: JsonRequired] bool AcceptedByPushService, [property: JsonRequired] bool? Shown, [property: JsonRequired] bool? Read, [property: JsonRequired] ReminderIntent Intent);
public sealed record ReminderPreview([property: JsonRequired] Guid CommandId, [property: JsonRequired] Guid RequestId, [property: JsonRequired] bool Accepted,
    string? InputHash = null, string? AcceptToken = null, DateTimeOffset? ExpiresAtUtc = null, Guid? WorkspaceId = null, long? ExpectedWorkspaceRevision = null,
    ReminderIntent? Intent = null, Guid? ScheduleId = null, string? Warning = null, ReminderReceipt? Receipt = null)
{
    public override string ToString() => $"Reminder preview {CommandId:D}; acceptance token redacted";
}
public sealed record ReminderCommandStatus([property: JsonRequired] Guid CommandId, [property: JsonRequired] bool? Accepted, Guid? RequestId = null, ReminderReceipt? Receipt = null, DateTimeOffset? ExpiresAtUtc = null, bool? OutcomeUnknown = null);
public sealed record ReminderSchedule([property: JsonRequired] Guid ScheduleId, [property: JsonRequired] long Revision, [property: JsonRequired] Guid TaskId,
    [property: JsonRequired] string State, [property: JsonRequired] string Reason, [property: JsonRequired] ReminderIntent Intent,
    [property: JsonRequired] DateTimeOffset? NextAtUtc, [property: JsonRequired] DateOnly? NextCivilDate);
public sealed record ReminderAttempt(int Attempt, Guid RequestId, string State, DateTimeOffset AtUtc, DateTimeOffset? FinishedAtUtc = null,
    string? Code = null, int? HttpStatus = null, long? RetryAfterMs = null, bool? AcceptedByPushService = null, bool? Shown = null, bool? Read = null);
public sealed record ReminderDelivery([property: JsonRequired] Guid OccurrenceId, [property: JsonRequired] Guid ScheduleId, [property: JsonRequired] long ScheduleRevision,
    [property: JsonRequired] DateOnly CivilDate, [property: JsonRequired] DateTimeOffset AtUtc, [property: JsonRequired] Guid DeviceId, [property: JsonRequired] string State,
    [property: JsonRequired] string Code, [property: JsonRequired] int Attempt, [property: JsonRequired] long? RetryAfterMs, [property: JsonRequired] int? HttpStatus,
    [property: JsonRequired] ReminderAttempt[] Attempts);
public sealed record ReminderPushReadiness([property: JsonRequired] string ProfileId, [property: JsonRequired] Guid ConnectionId, [property: JsonRequired] bool Available,
    [property: JsonRequired] bool DispatchAvailable, [property: JsonRequired] string ConsentVersion, [property: JsonRequired] Guid? ConsentEpoch,
    [property: JsonRequired] bool Enabled, [property: JsonRequired] string? ApplicationServerKey, [property: JsonRequired] PushDevice[] Devices);
public sealed record TaskReminderState([property: JsonRequired] Guid WorkspaceId, [property: JsonRequired] long WorkspaceRevision, [property: JsonRequired] Guid TaskId,
    [property: JsonRequired] bool Available, [property: JsonRequired] ReminderSchedule[] Schedules, [property: JsonRequired] ReminderDelivery[] Deliveries,
    [property: JsonRequired] int Offset, [property: JsonRequired] int? NextOffset, [property: JsonRequired] ReminderPushReadiness? Push,
    [property: JsonRequired] bool? Shown, [property: JsonRequired] bool? Read);

public interface ITaskReminderClient
{
    long ContextVersion { get; }
    Task<PushResult<TaskReminderState>> ReadTaskRemindersAsync(Guid taskId, int offset, CancellationToken token);
    Task<PushResult<ReminderPreview>> PrepareReminderAsync(ReminderPreparation preparation, CancellationToken token);
    Task<PushResult<ReminderCommandStatus>> AcceptReminderAsync(ReminderPreview preview, CancellationToken token);
    Task<PushResult<ReminderCommandStatus>> RecoverReminderAsync(Guid commandId, CancellationToken token);
}
