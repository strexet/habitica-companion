using System.Text.Json;
using System.Text.Json.Serialization;

namespace Habitica.WebApp.Integration;

internal static class RecoveryHttp
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    internal static async Task<string> Text(HttpResponseMessage response, CancellationToken token)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var buffer = new MemoryStream();
        var chunk = new byte[1024];
        int count;
        while ((count = await stream.ReadAsync(chunk, token)) > 0)
        {
            if (buffer.Length + count > 8192) throw new JsonException("Recovery response exceeds the limit.");
            buffer.Write(chunk, 0, count);
        }
        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    internal static string Error(string text)
    {
        using var json = JsonDocument.Parse(text);
        var code = json.RootElement.TryGetProperty("error", out var error) ? error.GetString() : null;
        // Never display untrusted error bodies or diagnostic detail.
        return code is "existing_connection_missing" or "existing_connection_revoked" or "integration_consent_unsupported"
            or "recovery_descriptor_stale" or "recovery_acknowledgement_required" or "credential_repair_required"
            or "workspace_missing" or "workspace_disconnected" or "workspace_connection_changed" or "workspace_consent_unsupported"
            or "notes_missing" or "notes_disconnected" or "notes_connection_changed" or "notes_consent_unsupported"
            or "workspace_mismatch" or "notebook_mismatch" or "connection_changed" or "unauthorized"
            or "try_later" or "invalid_credentials" or "habitica_unavailable" ? code : "recovery_unavailable";
    }
}
