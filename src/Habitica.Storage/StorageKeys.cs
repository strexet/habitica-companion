namespace Habitica.Storage;

internal static class StorageKeys
{
    public const string PersistentCredentials = "auth/persistentCredentials";
    public const string LatestTaskSnapshot = "tasks/latestSnapshot";
    public const string LatestUserSnapshot = "user/latestSnapshot";
    public const string LatestGearCatalog = "inventory/gearCatalog";
    public const string EquipmentPresets = "inventory/equipmentPresets";
    public const string LatestPartySnapshot = "party/latestSnapshot";
    public const string PartyCronHistory = "party/cronHistory";
    public const string DiagnosticsLogEntries = "diagnostics/logEntries";
}
