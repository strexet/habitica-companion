namespace Habitica.Application.Notifications;

public sealed record ForegroundReminderContext(string AccountId, Guid WorkspaceId, string Source);
public sealed record ForegroundReminderSchedule(Guid TaskId, string ReviewAfterUtc, long ScheduleRevision);
public sealed record ForegroundReminderState(long BindingVersion, string? ContextKey, bool InApp, bool Browser, string Permission, string Capability, string Storage, string Delivery, long NowMs, bool ClockChanged = false);
public interface IForegroundReminderBrowser
{
    event Action<ForegroundReminderState>? Changed;
    Task PrepareAsync(CancellationToken token = default);
    Task<ForegroundReminderState> BindAsync(ForegroundReminderContext? context, IReadOnlyList<ForegroundReminderSchedule> cues, long version, CancellationToken token = default);
    Task<ForegroundReminderState> ConfigureAsync(string kind, bool enabled, long version, CancellationToken token = default);
    Task<ForegroundReminderState> RequestPermissionAsync(long version, CancellationToken token = default);
    Task ClearAsync(CancellationToken token = default);
}
