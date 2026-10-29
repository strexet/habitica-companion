using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Habitica.Application.Integration;

namespace Habitica.WebApp.Integration;

public sealed partial class ChatGptIntegrationClient
{
    public Task<IntegrationTagConsentResult> ReadTagConsentAsync(CancellationToken cancellationToken) => TagConsentAsync(null, cancellationToken);
    public Task<IntegrationTagConsentResult> SetTagConsentAsync(bool enabled, CancellationToken cancellationToken) => TagConsentAsync(enabled, cancellationToken);
    private async Task<IntegrationTagConsentResult> TagConsentAsync(bool? enabled, CancellationToken cancellationToken)
    {
        if (_baseUri is null || !HasConnection) return new(false, "Verify the account in Settings to manage tag access.");
        var generation = _sessionVersion;
        try
        {
            using var request = ManagementRequest(enabled is null ? HttpMethod.Get : HttpMethod.Post, "tag-consent");
            if (enabled is not null) request.Content = JsonContent.Create(new { consentVersion = "habitica-tags-v1", enabled = enabled.Value });
            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return new(false, "Tag access could not be verified. Check consent status before another change.");
            var result = await response.Content.ReadFromJsonAsync<TagConsentPayload>(cancellationToken);
            if (generation != _sessionVersion || result is null || result.ConsentVersion != "habitica-tags-v1" || enabled is not null && result.Enabled != enabled.Value
                || result.GrantedAtUtc is {} granted && (granted == default || granted.Offset != TimeSpan.Zero)) return new(false, "The account or tag consent response changed. Verify the account and check status.");
            return new(true, enabled is null ? "Tag consent status checked." : result.Enabled ? "Tag metadata access enabled. Agent access and background execution have separate controls." : "New tag operations paused. Native tags and original uncertain receipts remain.", result.Enabled, result.GrantedAtUtc);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(false, "Tag access result is unavailable. Check status before another change."); }
    }
    private sealed record TagConsentPayload([property:JsonRequired] bool Enabled, [property:JsonRequired] string ConsentVersion, DateTimeOffset? GrantedAtUtc);
}
