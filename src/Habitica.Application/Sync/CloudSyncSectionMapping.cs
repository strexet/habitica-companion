using Habitica.Storage;

namespace Habitica.Application.Sync;

public static class CloudSyncSectionMapping
{
    public static IReadOnlyList<CloudSyncSection> AllSections { get; } = new[]
    {
        CloudSyncSection.UserProfile,
        CloudSyncSection.SavedPresets,
        CloudSyncSection.TasksCurrent,
        CloudSyncSection.TaskOrderPreferences,
        CloudSyncSection.InventoryCatalog,
        CloudSyncSection.PartyCurrent,
        CloudSyncSection.PartyCronHistory,
        CloudSyncSection.ColorSchemes,
        CloudSyncSection.Diagnostics,
        CloudSyncSection.SyncMetadata
    };

    public static IReadOnlyList<CloudSyncSection> CriticalSections { get; } = new[]
    {
        CloudSyncSection.UserProfile,
        CloudSyncSection.SavedPresets,
        CloudSyncSection.SyncMetadata
    };

    public const int MaxSectionPayloadBytes = 2 * 1024 * 1024;

    public static string? StorageKeyFor(CloudSyncSection section)
    {
        return section switch
        {
            CloudSyncSection.UserProfile => StorageKeys.LatestUserSnapshot,
            CloudSyncSection.TasksCurrent => StorageKeys.LatestTaskSnapshot,
            CloudSyncSection.TaskOrderPreferences => StorageKeys.TaskOrderPreferences,
            CloudSyncSection.InventoryCatalog => StorageKeys.LatestGearCatalog,
            CloudSyncSection.SavedPresets => StorageKeys.EquipmentPresets,
            CloudSyncSection.PartyCurrent => StorageKeys.LatestPartySnapshot,
            CloudSyncSection.PartyCronHistory => StorageKeys.PartyCronHistory,
            CloudSyncSection.ColorSchemes => StorageKeys.ColorSchemePreferences,
            CloudSyncSection.Diagnostics => StorageKeys.DiagnosticsLogEntries,
            CloudSyncSection.SyncMetadata => null,
            _ => null
        };
    }

    public static CloudSyncSection? SectionForStorageKey(string storageKey)
    {
        return storageKey switch
        {
            StorageKeys.LatestUserSnapshot => CloudSyncSection.UserProfile,
            StorageKeys.LatestTaskSnapshot => CloudSyncSection.TasksCurrent,
            StorageKeys.TaskOrderPreferences => CloudSyncSection.TaskOrderPreferences,
            StorageKeys.LatestGearCatalog => CloudSyncSection.InventoryCatalog,
            StorageKeys.EquipmentPresets => CloudSyncSection.SavedPresets,
            StorageKeys.LatestPartySnapshot => CloudSyncSection.PartyCurrent,
            StorageKeys.PartyCronHistory => CloudSyncSection.PartyCronHistory,
            StorageKeys.ColorSchemePreferences => CloudSyncSection.ColorSchemes,
            StorageKeys.DiagnosticsLogEntries => CloudSyncSection.Diagnostics,
            _ => null
        };
    }

    public static string KvSuffix(CloudSyncSection section)
    {
        return section switch
        {
            CloudSyncSection.UserProfile => "user-profile",
            CloudSyncSection.TasksCurrent => "tasks-current",
            CloudSyncSection.TaskOrderPreferences => "task-order-preferences",
            CloudSyncSection.InventoryCatalog => "inventory-catalog",
            CloudSyncSection.SavedPresets => "saved-presets",
            CloudSyncSection.PartyCurrent => "party-current",
            CloudSyncSection.PartyCronHistory => "party-cron-history",
            CloudSyncSection.ColorSchemes => "color-schemes",
            CloudSyncSection.Diagnostics => "diagnostics",
            CloudSyncSection.SyncMetadata => "sync-metadata",
            _ => section.ToString().ToLowerInvariant()
        };
    }

    public static bool IsCritical(CloudSyncSection section)
    {
        return section is CloudSyncSection.UserProfile
            or CloudSyncSection.SavedPresets
            or CloudSyncSection.SyncMetadata;
    }
}
