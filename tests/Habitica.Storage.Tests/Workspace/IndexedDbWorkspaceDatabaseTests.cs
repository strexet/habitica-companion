using System.Text.Json;
using Habitica.Domain.Workspace;
using Microsoft.JSInterop;

namespace Habitica.Storage.Tests.Workspace;

public sealed class IndexedDbWorkspaceDatabaseTests
{
    private static WorkspaceJournalSnapshot Empty() => new(new WorkspaceDocument { Id = Guid.NewGuid() }, 1, Array.Empty<WorkspaceCommandEntry>());

    [Fact]
    public async Task ScopedAdapterSerializesValidatedJournalAndRejectsStaleWrites()
    {
        var js = new Runtime(); await using var database = new IndexedDbWorkspaceDatabase(js);
        var a = database.ForScope("fixture_a"); var b = database.ForScope("fixture_b"); var snapshot = Empty();
        Assert.True(await a.TryCommitAsync(0, snapshot, default));
        Assert.False(await a.TryCommitAsync(0, snapshot, default));
        var loaded = await a.LoadAsync(snapshot.Document.Id, default);
        Assert.Equal(WorkspaceJournalJson.Serialize(snapshot), WorkspaceJournalJson.Serialize(loaded!));
        Assert.Null(await b.LoadAsync(snapshot.Document.Id, default));
        Assert.Equal(1, js.ImportCount);
        Assert.Equal(new[] { snapshot.Document.Id }, await database.ListAsync("fixture_a", default));
        Assert.Empty(await database.ListAsync("fixture_b", default));
        await Assert.ThrowsAsync<ArgumentException>(() => a.TryCommitAsync(1, snapshot with { StoreVersion = 3 }, default));
        await Assert.ThrowsAsync<JsonException>(() => a.TryCommitAsync(1, snapshot with { StoreVersion = 2, Document = snapshot.Document with { SchemaVersion = 99 } }, default));
    }

    [Fact]
    public async Task SavedTaskViewDefinitionsSurviveScopedStorageAndPortableBackup()
    {
        var js=new Runtime();await using var database=new IndexedDbWorkspaceDatabase(js);var scope=database.ForScope("fixture_saved_views");var snapshot=Empty();
        snapshot=snapshot with {Document=snapshot.Document with {SavedTaskViews=[new(Guid.NewGuid(),1,"Active Inbox",new(WorkspaceSavedTaskSource.Tasks,new(ExecutionStage:TodoExecutionStage.Inbox)))]}};
        Assert.True(await scope.TryCommitAsync(0,snapshot,default));var loaded=await scope.LoadAsync(snapshot.Document.Id,default);
        Assert.Equal(WorkspaceJournalJson.Serialize(snapshot),WorkspaceJournalJson.Serialize(loaded!));Assert.Equal(snapshot.Document.SavedTaskViews![0],Assert.Single(loaded!.Document.SavedTaskViews!));
        Assert.Null(await database.ForScope("fixture_other").LoadAsync(snapshot.Document.Id,default));
    }

    [Fact]
    public async Task DatabaseClearRemovesCanonicalRowsAndDisposalReleasesTheModule()
    {
        var js = new Runtime(); var database = new IndexedDbWorkspaceDatabase(js);
        var scope = database.ForScope("fixture_a"); var snapshot = Empty();
        Assert.True(await scope.TryCommitAsync(0, snapshot, default));
        await database.ClearAsync(default); Assert.Null(await scope.LoadAsync(snapshot.Document.Id, default));
        await database.DisposeAsync(); Assert.True(js.Module.Disposed);
    }

    [Fact]
    public async Task MalformedAndMismatchedReadResponsesFailClosed()
    {
        var js = new Runtime(); await using var database = new IndexedDbWorkspaceDatabase(js); var scope = database.ForScope("fixture_a");
        js.Module.ReadOverride = "{\"document\":null,\"storeVersion\":1,\"commands\":[]}";
        await Assert.ThrowsAsync<JsonException>(() => scope.LoadAsync(Guid.NewGuid(), default));
        js.Module.ReadOverride = WorkspaceJournalJson.Serialize(Empty());
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.LoadAsync(Guid.NewGuid(), default));
        Assert.Throws<ArgumentException>(() => database.ForScope("unsafe/scope"));
        Assert.Throws<ArgumentException>(() => database.ForScope(""));
    }

    [Fact]
    public void JournalSerializationNormalizesEmptyHistoryAndRejectsInvalidImports()
    {
        var snapshot = Empty(); var json = WorkspaceJournalJson.Serialize(snapshot);
        Assert.Contains("\"events\":[]", json);
        Assert.Equal(json, WorkspaceJournalJson.Serialize(WorkspaceJournalJson.Deserialize(json)));
        Assert.Throws<JsonException>(() => WorkspaceJournalJson.Deserialize(json.Replace("\"storeVersion\":1", "\"storeVersion\":0")));
        Assert.Throws<JsonException>(() => WorkspaceJournalJson.Deserialize(json.Replace("\"storeVersion\":1", "\"storeVersion\":1,\"storeVersion\":2")));
        Assert.Throws<JsonException>(() => WorkspaceJournalJson.Deserialize(json.Replace("\"storeVersion\":1", "\"userId\":\"other\",\"storeVersion\":1")));
    }

    private sealed class Runtime : IJSRuntime
    {
        public Module Module { get; } = new();
        public int ImportCount { get; private set; }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, default, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken token, object?[]? args)
        {
            token.ThrowIfCancellationRequested(); Assert.Equal("import", identifier); Assert.Equal("./js/storage/indexedDbStorage.js", args![0]); ImportCount++;
            return ValueTask.FromResult((TValue)(object)Module);
        }
    }

    private sealed class Module : IJSObjectReference
    {
        private readonly Dictionary<(string Scope, string Id), string> _rows = new();
        public bool Disposed { get; private set; }
        public string? ReadOverride { get; set; }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, default, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken token, object?[]? args)
        {
            token.ThrowIfCancellationRequested(); object? result = null;
            if (identifier == "getWorkspace") result = ReadOverride ?? _rows.GetValueOrDefault(((string)args![0]!, (string)args[1]!));
            else if (identifier == "listWorkspaces") result = _rows.Keys.Where(key => key.Scope == (string)args![0]!).Select(key => key.Id).ToArray();
            else if (identifier == "clearWorkspaces") _rows.Clear();
            else if (identifier == "commitWorkspace")
            {
                var scope = (string)args![0]!; var expected = (long)args[1]!; var json = (string)args[2]!;
                var snapshot = WorkspaceJournalJson.Deserialize(json); var key = (scope, snapshot.Document.Id.ToString("D"));
                var version = _rows.TryGetValue(key, out var old) ? WorkspaceJournalJson.Deserialize(old).StoreVersion : 0;
                result = version == expected;
                if ((bool)result) _rows[key] = json;
            }
            else throw new InvalidOperationException("Unexpected fixture invocation.");
            return ValueTask.FromResult(result is null ? default! : (TValue)result);
        }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }
}
