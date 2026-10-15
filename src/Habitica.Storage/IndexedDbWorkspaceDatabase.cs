using System.Text.RegularExpressions;
using Habitica.Domain.Workspace;
using Microsoft.JSInterop;

namespace Habitica.Storage;

public sealed class IndexedDbWorkspaceDatabase : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _module;

    public IndexedDbWorkspaceDatabase(IJSRuntime runtime)
    {
        _module = new(() => runtime.InvokeAsync<IJSObjectReference>("import", "./js/storage/indexedDbStorage.js").AsTask());
    }

    public ICompanionWorkspaceStore ForScope(string scope)
    {
        if (!Regex.IsMatch(scope, "^[A-Za-z0-9_-]{1,128}$")) throw new ArgumentException("A local account scope is required.", nameof(scope));
        return new ScopedStore(this, scope);
    }

    public async Task ClearAsync(CancellationToken cancellationToken)
    {
        var module = await _module.Value;
        await module.InvokeVoidAsync("clearWorkspaces", cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module.IsValueCreated) await (await _module.Value).DisposeAsync();
    }

    private sealed class ScopedStore(IndexedDbWorkspaceDatabase database, string scope) : ICompanionWorkspaceStore
    {
        public async Task<WorkspaceJournalSnapshot?> LoadAsync(Guid workspaceId, CancellationToken cancellationToken)
        {
            var module = await database._module.Value;
            var json = await module.InvokeAsync<string?>("getWorkspace", cancellationToken, scope, workspaceId.ToString("D"));
            if (json is null) return null;
            var snapshot = WorkspaceJournalJson.Deserialize(json);
            if (snapshot.Document.Id != workspaceId) throw new InvalidOperationException("Workspace identity mismatch.");
            return snapshot;
        }

        public async Task<bool> TryCommitAsync(long expectedStoreVersion, WorkspaceJournalSnapshot snapshot, CancellationToken cancellationToken)
        {
            if (expectedStoreVersion < 0 || expectedStoreVersion > WorkspaceDocument.MaxRevision
                || snapshot.StoreVersion <= expectedStoreVersion || expectedStoreVersion > 0 && snapshot.StoreVersion != expectedStoreVersion + 1)
                throw new ArgumentException("Invalid workspace store revision.");
            var json = WorkspaceJournalJson.Serialize(snapshot);
            var module = await database._module.Value;
            return await module.InvokeAsync<bool>("commitWorkspace", cancellationToken, scope, expectedStoreVersion, json);
        }
    }
}
