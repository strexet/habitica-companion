using System.Net.Http.Json;
using System.Text.Json;
using Habitica.Application.Integration;
using Habitica.Domain.Auth;

namespace Habitica.WebApp.Integration;

public sealed partial class ChatGptIntegrationClient
{
    public IntegrationRecoveryDescriptor? RecoveryDescriptor { get; private set; }

    public async Task<IntegrationRecoveryResult> RecoverAsync(HabiticaCredentials credentials,
        IntegrationRecoveryDescriptor? descriptor, bool bootstrapAcknowledged, CancellationToken cancellationToken)
    {
        if (_baseUri is null) return new(false, "recovery_unavailable");
        if (descriptor is null && !bootstrapAcknowledged) return new(false, "recovery_acknowledgement_required");
        if (descriptor is not null && !descriptor.IsValid) return new(false, "recovery_descriptor_stale");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        var requestedToken = cancellationToken; cancellationToken = deadline.Token;
        var version = _sessionVersion;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_baseUri, "recover"))
            {
                Content = JsonContent.Create(new { habiticaUserId = credentials.UserId, apiToken = credentials.ApiToken,
                    descriptor, bootstrapAcknowledged = bootstrapAcknowledged ? (bool?)true : null },
                    options: RecoveryRequestJson)
            };
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var text = await RecoveryHttp.Text(response, cancellationToken);
            if (version != _sessionVersion) return new(false, "connection_changed");
            if (!response.IsSuccessStatusCode) return new(false, RecoveryHttp.Error(text));
            var value = JsonSerializer.Deserialize<RecoveredSession>(text, RecoveryHttp.Json);
            if (value?.Recovered != true || value.Descriptor?.IsValid != true || value.ProfileId != value.Descriptor.ProfileId
                || descriptor is not null && descriptor != value.Descriptor
                || !System.Text.RegularExpressions.Regex.IsMatch(value.ManagementToken ?? "", "^[A-Za-z0-9_-]{43}$")
                || value.ExpiresAtUtc <= DateTimeOffset.UtcNow || value.ExpiresAtUtc > DateTimeOffset.UtcNow.AddHours(8).AddMinutes(1))
                return new(false, "invalid_recovery_response");
            _profileId = value.ProfileId; _managementToken = value.ManagementToken;
            RecoveryDescriptor = value.Descriptor; _sessionVersion++;
            return new(true, "recovered", value.Descriptor, value.ExpiresAtUtc);
        }
        catch (OperationCanceledException) when (requestedToken.IsCancellationRequested) { throw; }
        catch { return new(false, "recovery_unavailable"); }
    }

    private static readonly JsonSerializerOptions RecoveryRequestJson = new(JsonSerializerDefaults.Web)
    { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
    private sealed record RecoveredSession(string? ProfileId, string? ManagementToken, DateTimeOffset ExpiresAtUtc,
        IntegrationRecoveryDescriptor? Descriptor, bool Recovered);

    internal HttpRequestMessage RecoveryHandshake(Uri uri, object input)
    {
        if (!HasConnection) throw new InvalidOperationException("Existing account access is required.");
        var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _managementToken);
        request.Content = JsonContent.Create(input, options: RecoveryRequestJson);
        return request;
    }
}
