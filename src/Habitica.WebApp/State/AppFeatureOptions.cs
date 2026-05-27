namespace Habitica.WebApp.State;

public sealed record AppFeatureOptions
{
    public bool PartySyncEnabled { get; init; } = true;

    // Extra fixed delay added by the controller between Habitica calls. Now that
    // HabiticaApiClient does adaptive token-bucket throttling (burst when budget is
    // healthy, ramp down as the 30 req/60s window drains, honor Retry-After), this
    // blunt per-call delay is redundant and defaults to 0. Set > 0 only to force an
    // additional floor on top of the client's own pacing.
    public int HabiticaRequestDelayMilliseconds { get; init; }

    public IReadOnlyList<string> CloudSyncExcludedSections { get; init; } = new[] { "diagnostics" };

    public IReadOnlyList<string> AdminUserIds { get; init; } = Array.Empty<string>();

    public bool IsAdmin(string? userId)
    {
        return !string.IsNullOrWhiteSpace(userId)
            && AdminUserIds.Contains(userId, StringComparer.OrdinalIgnoreCase);
    }
}
