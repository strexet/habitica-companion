using Habitica.Application.Diagnostics;
using Habitica.Application.Inventory;
using Habitica.Application.Sync;
using Habitica.Domain.Party;
using Habitica.Domain.User;
using Habitica.WebApp.Sync;

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

    Task<InventoryActionResult> SaveEquipmentPresetAsync(
        EquipmentSetKind kind,
        string name,
        GearSlotsSnapshot slots,
        CancellationToken cancellationToken = default);

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

    Task<SpellActionResult> StartNewDayAsync(CancellationToken cancellationToken = default);

    Task<SpellActionResult> StartNewDayAsync(StartNewDayRequest request, CancellationToken cancellationToken = default);

    Task<TaskActionResult> ScoreTaskAsync(TaskScoreRequest request, CancellationToken cancellationToken = default);

    Task<SpellActionResult> AllocateStatsAsync(StatAllocation allocation, CancellationToken cancellationToken = default);

    Task<InventoryActionResult> BuyArmoireAsync(int count, CancellationToken cancellationToken = default);

    Task<InventoryActionResult> BuyHealthPotionAsync(CancellationToken cancellationToken = default);

    Task<InventoryActionResult> FeedPetAsync(
        IReadOnlyList<PetFeedQueueItem> queue,
        CancellationToken cancellationToken = default);

    Task<InventoryActionResult> EquipPetAsync(string key, CancellationToken cancellationToken = default);

    Task<InventoryActionResult> EquipMountAsync(string key, CancellationToken cancellationToken = default);

    Task<InventoryActionResult> HatchPetAsync(
        string eggKey,
        string hatchingPotionKey,
        CancellationToken cancellationToken = default);

    Task<InventoryActionResult> SellInventoryItemAsync(
        InventorySellItemType type,
        string key,
        int count,
        CancellationToken cancellationToken = default);

    Task LogoutAsync(CancellationToken cancellationToken = default);

    Task ClearLocalDataAsync(CancellationToken cancellationToken = default);

    Task<LocalDataActionResult> ExportLocalDataAsync(CancellationToken cancellationToken = default);

    Task<LocalDataActionResult> PreviewImportLocalDataAsync(string jsonText, CancellationToken cancellationToken = default);

    Task<LocalDataActionResult> ImportLocalDataAsync(
        string jsonText,
        LocalDataImportMode mode,
        CancellationToken cancellationToken = default);

    Task<LocalDataActionResult> ImportCloudSyncSectionsAsync(
        string jsonText,
        IReadOnlyDictionary<string, CloudSyncSectionImportDecision> sectionDecisions,
        CancellationToken cancellationToken = default);

    Task<LocalDataActionResult> PushCloudSyncAsync(CancellationToken cancellationToken = default);

    Task<LocalDataActionResult> DownloadCloudSyncAsync(CancellationToken cancellationToken = default);

    Task SetCloudSyncSectionExcludedAsync(CloudSyncSection section, bool isExcluded, CancellationToken cancellationToken = default);

    Task SetIncludeStalePartyMembersAsync(bool include, CancellationToken cancellationToken = default);

    Task<PartyQuestActionResult> RefreshPartyQuestStateAsync(CancellationToken cancellationToken = default);

    Task<PartyQuestActionResult> AddPartyQuestToQueueAsync(string questKey, CancellationToken cancellationToken = default);

    Task<PartyQuestActionResult> TogglePartyQuestVoteAsync(string queueItemId, CancellationToken cancellationToken = default);

    Task<PartyQuestActionResult> RemovePartyQuestQueueItemAsync(string queueItemId, int version, CancellationToken cancellationToken = default);

    Task<PartyQuestActionResult> PinPartyQuestQueueItemAsync(string queueItemId, int version, bool pinned, CancellationToken cancellationToken = default);

    Task<PartyQuestActionResult> SelectPartyQuestQueueItemAsync(string queueItemId, int version, CancellationToken cancellationToken = default);

    Task<PartyQuestActionResult> SkipPartyQuestQueueItemAsync(string queueItemId, int version, CancellationToken cancellationToken = default);

    Task<PartyQuestActionResult> ExpirePartyQuestQueueItemAsync(string queueItemId, int version, CancellationToken cancellationToken = default);

    Task<PartyQuestActionResult> RequeuePartyQuestQueueItemAsync(string queueItemId, int version, CancellationToken cancellationToken = default);

    Task<PartyQuestActionResult> MarkPartyQuestCompletedAsync(string queueItemId, int version, CancellationToken cancellationToken = default);

    Task<PartyQuestActionResult> InvitePartyToQuestAsync(string queueItemId, int version, CancellationToken cancellationToken = default);

    Task<PartyQuestActionResult> AcceptPartyQuestInvitationAsync(CancellationToken cancellationToken = default);

    Task<PartyQuestActionResult> RejectPartyQuestInvitationAsync(CancellationToken cancellationToken = default);

    Task<PartyQuestActionResult> StartSelectedPartyQuestAsync(string queueItemId, CancellationToken cancellationToken = default);

    Task<PartyQuestActionResult> AssignPartySyncOfficerAsync(string userId, string displayName, CancellationToken cancellationToken = default);

    Task<PartyQuestActionResult> AssignPartySyncOwnerAsync(string userId, string displayName, CancellationToken cancellationToken = default);

    Task<PartyQuestActionResult> RemovePartySyncOfficerAsync(string userId, CancellationToken cancellationToken = default);

    Task<PartyQuestActionResult> KickPartySyncMemberAsync(string userId, string displayName, string? reason, CancellationToken cancellationToken = default);

    Task<PartyQuestActionResult> UnkickPartySyncMemberAsync(string userId, CancellationToken cancellationToken = default);

    Task<PartyQuestActionResult> UpdatePartySyncSettingsAsync(PartySyncSettings settings, CancellationToken cancellationToken = default);

    Task<PartySyncInviteProofActionResult> CreatePartySyncInviteProofAsync(string label, DateTimeOffset? expiresAtUtc = null, CancellationToken cancellationToken = default);

    Task<PartySyncInviteProofActionResult> RevokePartySyncInviteProofAsync(string proofId, CancellationToken cancellationToken = default);

    Task<PartySyncInviteProofActionResult> RotatePartySyncInviteProofAsync(string proofId, CancellationToken cancellationToken = default);

    Task<PartySyncInviteProofActionResult> RemovePartySyncInviteProofAsync(string proofId, CancellationToken cancellationToken = default);

    Task<PartySyncInviteProofActionResult> SetPartySyncInviteProofModeAsync(bool enabled, CancellationToken cancellationToken = default);

    Task<PartySyncInviteProofActionResult> ActivatePartySyncInviteProofAsync(string proofId, string token, CancellationToken cancellationToken = default);

    Task<PartySyncInviteProofActionResult> ClearPartySyncInviteProofAsync(CancellationToken cancellationToken = default);

    Task<PartyQuestActionResult> RemovePartyRecentlyCompletedQuestAsync(string questKey, DateTimeOffset completedAtUtc, CancellationToken cancellationToken = default);
}

public sealed record StartNewDayRequest(
    bool AutoEquipRecommendedGear = false,
    GearSlotsSnapshot? AutoEquipGearSlots = null,
    string? GearOptimizationGoalLabel = null);

public sealed record PartySyncInviteProofActionResult(
    bool Succeeded,
    string Message,
    PartySyncIssuedInviteProof? IssuedInviteProof = null)
{
    public static PartySyncInviteProofActionResult Success(string message, PartySyncIssuedInviteProof? issuedInviteProof = null)
        => new(true, message, issuedInviteProof);

    public static PartySyncInviteProofActionResult Failure(string message)
        => new(false, message);
}
