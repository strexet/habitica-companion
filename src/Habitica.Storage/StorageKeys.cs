namespace Habitica.Storage;

public static class StorageKeys
{
    public const string PersistentCredentials = "auth/persistentCredentials";
    public const string LatestTaskSnapshot = "tasks/latestSnapshot";
    public const string LatestUserSnapshot = "user/latestSnapshot";
    public const string LatestGearCatalog = "inventory/gearCatalog";
    public const string EquipmentPresets = "inventory/equipmentPresets";
    public const string LatestPartySnapshot = "party/latestSnapshot";
    public const string PartyCronHistory = "party/cronHistory";
    public const string DiagnosticsLogEntries = "diagnostics/logEntries";
    public const string TasksPagePreferences = "preferences/tasksPage";

    public static IReadOnlyList<string> PortableDataKeys { get; } =
        new[]
        {
            LatestTaskSnapshot,
            LatestUserSnapshot,
            LatestGearCatalog,
            EquipmentPresets,
            LatestPartySnapshot,
            PartyCronHistory,
            DiagnosticsLogEntries
        };
}
