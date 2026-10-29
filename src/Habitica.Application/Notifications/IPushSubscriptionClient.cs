using System.Text.Json.Serialization;

namespace Habitica.Application.Notifications;

public sealed record PushResult<T>(bool Succeeded, string Message, T? Value = default);
public sealed record PushContext(string ProfileId, Guid ConnectionId);
public sealed record PushDevice([property: JsonRequired] Guid DeviceId, [property: JsonRequired] long Revision, [property: JsonRequired] Guid SubscriptionId, [property: JsonRequired] string State, [property: JsonRequired] long? ExpiresAtMs);
public sealed record PushStatus([property: JsonRequired] string ProfileId, [property: JsonRequired] Guid ConnectionId, [property: JsonRequired] bool Available, [property: JsonRequired] string ConsentVersion, [property: JsonRequired] Guid? ConsentEpoch, [property: JsonRequired] bool Enabled, [property: JsonRequired] string? ApplicationServerKey, [property: JsonRequired] PushDevice[] Devices);
public sealed record PushKeys(string P256dh, string Auth)
{
    public override string ToString() => "[redacted push keys]";
}
public sealed record PushMaterial(string Endpoint, PushKeys Keys, long? ExpirationTime)
{
    public override string ToString() => "[redacted push subscription]";
}
public sealed record PushConsentCommand(PushContext Context, Guid RequestId, Guid? ExpectedConsentEpoch, bool Enabled);
public sealed record PushSubscribeCommand(PushContext Context, Guid RequestId, Guid DeviceId, long ExpectedRevision, Guid ConsentEpoch, PushMaterial Subscription);
public sealed record PushUnsubscribeCommand(PushContext Context, Guid RequestId, Guid DeviceId, long ExpectedRevision, Guid ConsentEpoch, Guid SubscriptionId);
public sealed record PushReceipt([property: JsonRequired] string ProfileId, [property: JsonRequired] Guid ConnectionId, [property: JsonRequired] Guid RequestId, [property: JsonRequired] string Operation, [property: JsonRequired] bool Saved, Guid? ConsentEpoch, bool? Enabled, Guid? DeviceId, Guid? SubscriptionId, long? Revision, string? State);

public interface IPushSubscriptionClient
{
    long ContextVersion => 0;
    Task<PushResult<PushStatus>> ReadPushStatusAsync(CancellationToken cancellationToken);
    Task<PushResult<PushReceipt>> SetPushConsentAsync(PushConsentCommand command, CancellationToken cancellationToken);
    Task<PushResult<PushReceipt>> SubscribePushAsync(PushSubscribeCommand command, CancellationToken cancellationToken);
    Task<PushResult<PushReceipt>> UnsubscribePushAsync(PushUnsubscribeCommand command, CancellationToken cancellationToken);
    Task<PushResult<PushReceipt>> RecoverPushReceiptAsync(Guid requestId, CancellationToken cancellationToken);
}

public sealed record BrowserPushCapability(string State, string Permission);
public sealed record BrowserPushCurrent(string State, string Permission, bool Exists, string? Fingerprint);
public sealed record BrowserPushResult(string State, string? Fingerprint = null, PushMaterial? Subscription = null);
public interface IPushBrowser
{
    Task<BrowserPushCapability> PrepareAsync(CancellationToken cancellationToken);
    Task<BrowserPushCurrent> ReadCurrentAsync(CancellationToken cancellationToken);
    Task<BrowserPushResult> SubscribeAsync(string applicationServerKey, bool reminderConsent, CancellationToken cancellationToken);
    Task<BrowserPushResult> UnsubscribeAsync(string expectedFingerprint, CancellationToken cancellationToken);
}
