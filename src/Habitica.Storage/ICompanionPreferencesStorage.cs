namespace Habitica.Storage;

public interface ICompanionPreferencesStorage
{
    Task<bool> TryCommitCompanionPreferencesAsync(string? expectedJson, string json, CancellationToken token);
}

public sealed record BrowserWorkspaceSelection(Guid? WorkspaceId, bool RequiresConsolidation, bool RequiresRecovery);

public interface IBrowserWorkspaceSource
{
    Task<BrowserWorkspaceSelection> EnsureDefaultAsync(string account, CancellationToken token);
}
