using Habitica.Domain.User;

namespace Habitica.Storage;

public interface IEquipmentPresetStore
{
    Task<IReadOnlyList<EquipmentPreset>> GetForUserAsync(string userId, CancellationToken cancellationToken);

    Task SaveAsync(EquipmentPreset preset, CancellationToken cancellationToken);

    Task RemoveAsync(string userId, string presetId, CancellationToken cancellationToken);

    Task ClearAsync(CancellationToken cancellationToken);
}
