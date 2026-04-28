using Habitica.Domain.User;

namespace Habitica.Storage;

public sealed class EquipmentPresetStore : IEquipmentPresetStore
{
    private readonly IKeyValueStorage _keyValueStorage;

    public EquipmentPresetStore(IKeyValueStorage keyValueStorage)
    {
        _keyValueStorage = keyValueStorage;
    }

    public async Task<IReadOnlyList<EquipmentPreset>> GetForUserAsync(string userId, CancellationToken cancellationToken)
    {
        var presets = await GetAllAsync(cancellationToken);
        return presets
            .Where(preset => string.Equals(preset.UserId, userId, StringComparison.Ordinal))
            .OrderBy(preset => preset.Kind)
            .ThenBy(preset => preset.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task SaveAsync(EquipmentPreset preset, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(preset.UserId))
        {
            throw new InvalidOperationException("Preset user id is required.");
        }

        if (string.IsNullOrWhiteSpace(preset.Name))
        {
            throw new InvalidOperationException("Preset name is required.");
        }

        var presets = (await GetAllAsync(cancellationToken)).ToList();
        var duplicate = presets.Any(existing =>
            !string.Equals(existing.Id, preset.Id, StringComparison.Ordinal)
            && string.Equals(existing.UserId, preset.UserId, StringComparison.Ordinal)
            && existing.Kind == preset.Kind
            && string.Equals(existing.Name, preset.Name, StringComparison.OrdinalIgnoreCase));

        if (duplicate)
        {
            throw new InvalidOperationException("A preset with this name already exists.");
        }

        presets.RemoveAll(existing =>
            string.Equals(existing.UserId, preset.UserId, StringComparison.Ordinal)
            && string.Equals(existing.Id, preset.Id, StringComparison.Ordinal));
        presets.Add(preset);

        await _keyValueStorage.SetAsync(StorageKeys.EquipmentPresets, presets.ToArray(), cancellationToken);
    }

    public async Task RemoveAsync(string userId, string presetId, CancellationToken cancellationToken)
    {
        var presets = (await GetAllAsync(cancellationToken)).ToList();
        presets.RemoveAll(preset =>
            string.Equals(preset.UserId, userId, StringComparison.Ordinal)
            && string.Equals(preset.Id, presetId, StringComparison.Ordinal));

        await _keyValueStorage.SetAsync(StorageKeys.EquipmentPresets, presets.ToArray(), cancellationToken);
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        return _keyValueStorage.RemoveAsync(StorageKeys.EquipmentPresets, cancellationToken);
    }

    private async Task<IReadOnlyList<EquipmentPreset>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _keyValueStorage.GetAsync<EquipmentPreset[]>(StorageKeys.EquipmentPresets, cancellationToken)
            ?? Array.Empty<EquipmentPreset>();
    }
}
