using Habitica.Application.Diagnostics;
using Habitica.Application.Inventory;
using Habitica.Application.Sync;
using Habitica.Domain.User;

namespace Habitica.WebApp.State;

public interface IAppSessionController
{
    event Action? Changed;

    SessionViewModel State { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task SignInAsync(SignInRequest request, CancellationToken cancellationToken = default);

    Task RefreshAsync(CancellationToken cancellationToken = default);

    Task RefreshForPageAsync(string pageRoute, CancellationToken cancellationToken = default);

    Task<LiveTestSuiteResult> RunSafeLiveTestsAsync(CancellationToken cancellationToken = default);

    Task<LiveTestSuiteResult> RunReversibleGearTestAsync(CancellationToken cancellationToken = default);

    Task<DiagnosticsPresetRunResult> RunDiagnosticsPresetAsync(DiagnosticsPreset preset, CancellationToken cancellationToken = default);

    Task ClearDiagnosticsLogsAsync(CancellationToken cancellationToken = default);

    Task<InventoryActionResult> SaveEquipmentPresetAsync(EquipmentSetKind kind, string name, CancellationToken cancellationToken = default);

    Task<InventoryActionResult> RemoveEquipmentPresetAsync(string presetId, CancellationToken cancellationToken = default);

    Task<InventoryActionResult> RenameEquipmentPresetAsync(string presetId, string name, CancellationToken cancellationToken = default);

    Task<InventoryActionResult> EquipInventoryItemAsync(EquipmentSetKind kind, string key, CancellationToken cancellationToken = default);

    Task<InventoryActionResult> EquipEquipmentPresetAsync(string presetId, CancellationToken cancellationToken = default);

    Task<InventoryActionResult> EquipGearSlotsAsync(
        EquipmentSetKind kind,
        GearSlotsSnapshot slots,
        string operationId,
        string label,
        CancellationToken cancellationToken = default);

    Task<SpellActionResult> CastSpellAsync(SpellCastRequest request, CancellationToken cancellationToken = default);

    Task<SpellActionResult> AllocateStatsAsync(StatAllocation allocation, CancellationToken cancellationToken = default);

    Task<InventoryActionResult> BuyArmoireAsync(int count, CancellationToken cancellationToken = default);

    Task LogoutAsync(CancellationToken cancellationToken = default);

    Task ClearLocalDataAsync(CancellationToken cancellationToken = default);

    Task<LocalDataActionResult> ExportLocalDataAsync(CancellationToken cancellationToken = default);

    Task<LocalDataActionResult> PreviewImportLocalDataAsync(string jsonText, CancellationToken cancellationToken = default);

    Task<LocalDataActionResult> ImportLocalDataAsync(
        string jsonText,
        LocalDataImportMode mode,
        CancellationToken cancellationToken = default);

    Task<LocalDataActionResult> PushCloudSyncAsync(CancellationToken cancellationToken = default);

    Task<LocalDataActionResult> DownloadCloudSyncAsync(CancellationToken cancellationToken = default);

    Task SetIncludeStalePartyMembersAsync(bool include, CancellationToken cancellationToken = default);

    Task<PartyQuestActionResult> RefreshPartyQuestStateAsync(CancellationToken cancellationToken = default);

    Task<PartyQuestActionResult> AddPartyQuestToQueueAsync(string questKey, CancellationToken cancellationToken = default);

    Task<PartyQuestActionResult> TogglePartyQuestVoteAsync(string queueItemId, CancellationToken cancellationToken = default);

    Task<PartyQuestActionResult> RemovePartyQuestQueueItemAsync(string queueItemId, int version, CancellationToken cancellationToken = default);

    Task<PartyQuestActionResult> MarkPartyQuestCompletedAsync(string queueItemId, int version, CancellationToken cancellationToken = default);
}
