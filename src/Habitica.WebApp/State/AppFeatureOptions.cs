namespace Habitica.WebApp.State;

public sealed record AppFeatureOptions
{
    public bool PartySyncEnabled { get; init; } = true;

    public int HabiticaRequestDelayMilliseconds { get; init; } = 1000;

    public IReadOnlyList<string> CloudSyncExcludedSections { get; init; } = new[] { "diagnostics" };

    public IReadOnlyList<string> AdminUserIds { get; init; } = Array.Empty<string>();

    public bool IsAdmin(string? userId)
    {
        return !string.IsNullOrWhiteSpace(userId)
            && AdminUserIds.Contains(userId, StringComparer.OrdinalIgnoreCase);
    }
}
