using System.Text.Json;
using Habitica.Storage;

namespace Habitica.WebApp.Tests;

internal sealed class InMemoryKeyValueStorage : IKeyValueStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public Task<TValue?> GetAsync<TValue>(string key, CancellationToken cancellationToken)
    {
        return Task.FromResult(_values.TryGetValue(key, out var json)
            ? JsonSerializer.Deserialize<TValue>(json, JsonOptions)
            : default);
    }

    public Task<string?> GetRawJsonAsync(string key, CancellationToken cancellationToken)
    {
        return Task.FromResult(_values.GetValueOrDefault(key));
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        _values.Remove(key);
        return Task.CompletedTask;
    }

    public Task SetAsync<TValue>(string key, TValue value, CancellationToken cancellationToken)
    {
        _values[key] = JsonSerializer.Serialize(value, JsonOptions);
        return Task.CompletedTask;
    }

    public Task SetRawJsonAsync(string key, string jsonText, CancellationToken cancellationToken)
    {
        _values[key] = jsonText;
        return Task.CompletedTask;
    }
}
