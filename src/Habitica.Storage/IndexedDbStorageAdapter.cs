using System.Text.Json;
using Microsoft.JSInterop;

namespace Habitica.Storage;

public sealed class IndexedDbStorageAdapter : IKeyValueStorage, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Lazy<Task<IJSObjectReference>> _moduleTask;

    public IndexedDbStorageAdapter(IJSRuntime jsRuntime)
    {
        _moduleTask = new Lazy<Task<IJSObjectReference>>(
            () => jsRuntime.InvokeAsync<IJSObjectReference>(
                "import",
                "./js/storage/indexedDbStorage.js").AsTask());
    }

    public async Task<TValue?> GetAsync<TValue>(string key, CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        var json = await module.InvokeAsync<string?>("getJson", cancellationToken, key);
        return json is null ? default : JsonSerializer.Deserialize<TValue>(json, JsonOptions);
    }

    public async Task SetAsync<TValue>(string key, TValue value, CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        var json = JsonSerializer.Serialize(value, JsonOptions);
        await module.InvokeVoidAsync("setJson", cancellationToken, key, json);
    }

    public async Task<string?> GetRawJsonAsync(string key, CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<string?>("getJson", cancellationToken, key);
    }

    public async Task SetRawJsonAsync(string key, string jsonText, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(jsonText))
        {
            throw new InvalidOperationException("Storage JSON payload is required.");
        }

        JsonDocument.Parse(jsonText);
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("setJson", cancellationToken, key, jsonText);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("remove", cancellationToken, key);
    }

    public async ValueTask DisposeAsync()
    {
        if (_moduleTask.IsValueCreated)
        {
            var module = await _moduleTask.Value;
            await module.DisposeAsync();
        }
    }
}
