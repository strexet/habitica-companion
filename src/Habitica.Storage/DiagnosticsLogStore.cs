using Habitica.Domain.Diagnostics;

namespace Habitica.Storage;

public sealed class DiagnosticsLogStore : IDiagnosticsLogStore
{
    private readonly IKeyValueStorage _keyValueStorage;
    private readonly int _maxEntries;
    private readonly SemaphoreSlim _mutationGate = new(1, 1);

    public DiagnosticsLogStore(IKeyValueStorage keyValueStorage, int maxEntries = 250)
    {
        _keyValueStorage = keyValueStorage;
        _maxEntries = maxEntries;
    }

    public async Task<IReadOnlyList<DiagnosticsLogEntry>> GetRecentAsync(CancellationToken cancellationToken)
    {
        return await _keyValueStorage.GetAsync<DiagnosticsLogEntry[]>(StorageKeys.DiagnosticsLogEntries, cancellationToken)
            ?? Array.Empty<DiagnosticsLogEntry>();
    }

    public async Task AppendAsync(DiagnosticsLogEntry entry, CancellationToken cancellationToken)
    {
        await _mutationGate.WaitAsync(cancellationToken);
        try
        {
            var entries = (await GetRecentAsync(cancellationToken)).ToList();
            entries.Insert(0, entry);

            if (entries.Count > _maxEntries)
            {
                entries.RemoveRange(_maxEntries, entries.Count - _maxEntries);
            }

            await _keyValueStorage.SetAsync(StorageKeys.DiagnosticsLogEntries, entries.ToArray(), cancellationToken);
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken)
    {
        await _mutationGate.WaitAsync(cancellationToken);
        try
        {
            await _keyValueStorage.RemoveAsync(StorageKeys.DiagnosticsLogEntries, cancellationToken);
        }
        finally
        {
            _mutationGate.Release();
        }
    }
}
