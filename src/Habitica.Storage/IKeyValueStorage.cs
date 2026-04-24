namespace Habitica.Storage;

public interface IKeyValueStorage
{
    Task<TValue?> GetAsync<TValue>(string key, CancellationToken cancellationToken);

    Task SetAsync<TValue>(string key, TValue value, CancellationToken cancellationToken);

    Task RemoveAsync(string key, CancellationToken cancellationToken);
}
