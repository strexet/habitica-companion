using System.Net.Http.Json;
using System.Text.Json;
using Habitica.Application.Notifications;

namespace Habitica.WebApp.Integration;

public sealed partial class ChatGptIntegrationClient : IPushSubscriptionClient
{
    public long ContextVersion => _sessionVersion;
    private static bool ValidGuid(Guid value) => value != Guid.Empty;
    private bool ValidContext(PushContext context) => HasConnection && context.ProfileId == _profileId && ValidGuid(context.ConnectionId);
    private static bool ValidRevision(long value) => value >= 0 && value < 9007199254740991;
    private static bool ValidReceipt(PushReceipt value) => value.Saved && ValidGuid(value.RequestId) && ValidGuid(value.ConnectionId)
        && (value.Operation == "consent" ? value.ConsentEpoch is {} epoch && ValidGuid(epoch) && value.Enabled is not null && value.DeviceId is null && value.SubscriptionId is null && value.Revision is null && value.State is null
            : value.Operation is "subscribe" or "unsubscribe" && value.DeviceId is {} device && ValidGuid(device) && value.SubscriptionId is {} subscription && ValidGuid(subscription)
                && value.Revision is > 0 and <= 9007199254740991 && value.State == (value.Operation == "subscribe" ? "active" : "unsubscribed"));
    private static PushResult<T> PushUnavailable<T>() => new(false, "Push result is unavailable. Check status or recover the original request before another change.");
    private static readonly JsonSerializerOptions PushJson = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };

    public Task<PushResult<PushStatus>> ReadPushStatusAsync(CancellationToken cancellationToken) => PushRequestAsync<PushStatus>("", null, value =>
        value.ProfileId == _profileId && ValidGuid(value.ConnectionId) && value.ConsentVersion == "web-push-v1"
        && (value.ConsentEpoch is null ? !value.Enabled : ValidGuid(value.ConsentEpoch.Value))
        && value.Available == (value.ApplicationServerKey is not null) && (value.ApplicationServerKey is null || System.Text.RegularExpressions.Regex.IsMatch(value.ApplicationServerKey, "^[A-Za-z0-9_-]{87}$"))
        && value.Devices is { Length: <= 64 } && value.Devices.All(d => d is not null && ValidGuid(d.DeviceId) && ValidGuid(d.SubscriptionId) && d.Revision is > 0 and <= 9007199254740991
            && d.State is "active" or "unsubscribed" or "revoked" or "expired" && (d.ExpiresAtMs is null or >= 0 and <= 8640000000000000))
        && value.Devices.Select(d => d.DeviceId).Distinct().Count() == value.Devices.Length, cancellationToken);

    public Task<PushResult<PushReceipt>> SetPushConsentAsync(PushConsentCommand command, CancellationToken cancellationToken)
    {
        if (!ValidContext(command.Context) || !ValidGuid(command.RequestId) || command.ExpectedConsentEpoch == Guid.Empty) return Task.FromResult(PushUnavailable<PushReceipt>());
        return WritePushAsync("consent", command.Context, command.RequestId, new { command.RequestId, command.Context.ConnectionId, command.ExpectedConsentEpoch, consentVersion = "web-push-v1", command.Enabled },
            value => value.Operation == "consent" && value.Enabled == command.Enabled, cancellationToken);
    }
    public Task<PushResult<PushReceipt>> SubscribePushAsync(PushSubscribeCommand command, CancellationToken cancellationToken)
    {
        if (!ValidContext(command.Context) || !ValidGuid(command.RequestId) || !ValidGuid(command.DeviceId) || !ValidGuid(command.ConsentEpoch) || !ValidRevision(command.ExpectedRevision)
            || command.Subscription is null || command.Subscription.Endpoint?.Length is not (> 0 and <= 2048) || command.Subscription.Keys?.P256dh?.Length != 87 || command.Subscription.Keys.Auth?.Length != 22)
            return Task.FromResult(PushUnavailable<PushReceipt>());
        return WritePushAsync("subscribe", command.Context, command.RequestId, new { command.RequestId, command.Context.ConnectionId, command.DeviceId, command.ExpectedRevision, command.ConsentEpoch, command.Subscription, userVisibleOnly = true },
            value => value.Operation == "subscribe" && value.DeviceId == command.DeviceId && value.Revision == command.ExpectedRevision + 1, cancellationToken);
    }
    public Task<PushResult<PushReceipt>> UnsubscribePushAsync(PushUnsubscribeCommand command, CancellationToken cancellationToken)
    {
        if (!ValidContext(command.Context) || !ValidGuid(command.RequestId) || !ValidGuid(command.DeviceId) || !ValidGuid(command.ConsentEpoch) || !ValidGuid(command.SubscriptionId) || !ValidRevision(command.ExpectedRevision))
            return Task.FromResult(PushUnavailable<PushReceipt>());
        return WritePushAsync("unsubscribe", command.Context, command.RequestId, new { command.RequestId, command.Context.ConnectionId, command.DeviceId, command.ExpectedRevision, command.ConsentEpoch, command.SubscriptionId },
            value => value.Operation == "unsubscribe" && value.DeviceId == command.DeviceId && value.SubscriptionId == command.SubscriptionId && value.Revision == command.ExpectedRevision + 1, cancellationToken);
    }
    private Task<PushResult<PushReceipt>> WritePushAsync(string operation, PushContext context, Guid requestId, object body, Func<PushReceipt, bool> matches, CancellationToken cancellationToken) =>
        PushRequestAsync<PushReceipt>("/" + operation, body, value => ValidReceipt(value) && value.ProfileId == context.ProfileId && value.ConnectionId == context.ConnectionId && value.RequestId == requestId && matches(value), cancellationToken);

    public async Task<PushResult<PushReceipt>> RecoverPushReceiptAsync(Guid requestId, CancellationToken cancellationToken)
    {
        if (!ValidGuid(requestId)) return PushUnavailable<PushReceipt>();
        var result = await PushRequestAsync<PushReceiptEnvelope>("/commands/" + requestId.ToString("D"), null,
            value => value.ProfileId == _profileId && value.RequestId == requestId && (value.Receipt is null || ValidReceipt(value.Receipt) && value.Receipt.ProfileId == _profileId && value.Receipt.RequestId == requestId), cancellationToken);
        return result.Succeeded ? new(true, result.Value!.Receipt is null ? "Original receipt is absent or expired. Check current status; do not infer that an uncertain change failed." : "Original push receipt recovered.", result.Value!.Receipt) : PushUnavailable<PushReceipt>();
    }
    private sealed record PushReceiptEnvelope([property: System.Text.Json.Serialization.JsonRequired] string ProfileId, [property: System.Text.Json.Serialization.JsonRequired] Guid RequestId, [property: System.Text.Json.Serialization.JsonRequired] PushReceipt? Receipt);
    private async Task<PushResult<T>> PushRequestAsync<T>(string suffix, object? body, Func<T, bool> validate, CancellationToken cancellationToken) where T : class
    {
        if (_baseUri is null || !HasConnection) return PushUnavailable<T>();
        var generation = _sessionVersion;
        try
        {
            using var request = ManagementRequest(body is null ? HttpMethod.Get : HttpMethod.Post, "push" + suffix);
            if (body is not null) request.Content = JsonContent.Create(body);
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode) return PushUnavailable<T>();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var buffer = new byte[32769];
            var count = await stream.ReadAtLeastAsync(buffer, buffer.Length, false, cancellationToken);
            if (count > 32768) return PushUnavailable<T>();
            var value = JsonSerializer.Deserialize<T>(buffer.AsSpan(0, count), PushJson);
            if (generation != _sessionVersion || value is null || !validate(value)) return PushUnavailable<T>();
            return new(true, "Push status or original result checked.", value);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return PushUnavailable<T>(); }
    }
}
