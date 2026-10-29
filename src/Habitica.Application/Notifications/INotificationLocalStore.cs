namespace Habitica.Application.Notifications;

public sealed record PushBrowserBinding(Guid DeviceId, Guid SubscriptionId, long Revision, string Fingerprint);
// Original identities only. Subscription endpoint/keys and acceptance tokens never enter local storage.
public sealed record PendingPushAction(Guid RequestId, string Operation, Guid? ExpectedConsentEpoch, bool? Enabled, Guid? DeviceId = null,
    long? ExpectedRevision = null, Guid? ConsentEpoch = null, Guid? SubscriptionId = null, string? Fingerprint = null);
public sealed record PushBrowserLocalState(long Revision, PushBrowserBinding? Binding, PendingPushAction? Pending);
public sealed record PendingReminderAction(Guid WorkspaceId, ReminderPreparation Preparation, Guid? CommandId, bool AcceptanceAttempted = false);
public sealed record ReminderLocalState(long Revision, PendingReminderAction? Pending);
public interface INotificationLocalStore
{
    Task<PushBrowserLocalState> ReadDeviceAsync(string accountId,PushContext context,CancellationToken token=default);
    Task<bool> WriteDeviceAsync(string accountId,PushContext context,long expectedRevision,PushBrowserLocalState state,CancellationToken token=default);
    Task<ReminderLocalState> ReadReminderAsync(string accountId,PushContext context,Guid workspaceId,CancellationToken token=default);
    Task<bool> WriteReminderAsync(string accountId,PushContext context,Guid workspaceId,long expectedRevision,ReminderLocalState state,CancellationToken token=default);
    Task ClearAsync(CancellationToken token=default);
}
