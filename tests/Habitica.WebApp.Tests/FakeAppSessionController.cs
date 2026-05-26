using Habitica.WebApp.State;

using Habitica.Application.Diagnostics;
using Habitica.Application.Inventory;
using Habitica.Application.Sync;
using Habitica.Domain.Diagnostics;
using Habitica.Domain.Party;
using Habitica.Domain.User;

namespace Habitica.WebApp.Tests;

internal sealed class FakeAppSessionController : IAppSessionController
{
    private bool _initialized;

    public FakeAppSessionController(SessionViewModel state)
    {
        State = state;
    }

    public event Action? Changed;

    public int DiagnosticsPresetCalls { get; private set; }

    public DiagnosticsPresetRunResult? DiagnosticsPresetResult { get; set; }

    public SignInRequest? LastSignInRequest { get; private set; }

    public int ReversibleGearTestCalls { get; private set; }

    public LiveTestSuiteResult? ReversibleGearTestResult { get; set; }

    public int SafeLiveTestCalls { get; private set; }

    public LiveTestSuiteResult? SafeLiveTestResult { get; set; }

    public List<(EquipmentSetKind Kind, string Key)> EquipItemCalls { get; } = new();

    public List<string> EquipPresetCalls { get; } = new();

    public List<(EquipmentSetKind Kind, GearSlotsSnapshot Slots, string OperationId, string Label)> EquipGearSlotsCalls { get; } = new();

    public List<SpellCastRequest> CastSpellCalls { get; } = new();

    public List<TaskScoreRequest> ScoreTaskCalls { get; } = new();

    public List<(string UserId, string DisplayName)> AssignPartyOwnerCalls { get; } = new();

    public List<(string QueueItemId, int Version)> InvitePartyQuestCalls { get; } = new();

    public List<(string QueueItemId, int Version, bool Pinned)> PinPartyQuestQueueCalls { get; } = new();

    public List<(string QueueItemId, int Version)> SelectPartyQuestQueueCalls { get; } = new();

    public List<(string QueueItemId, int Version)> SkipPartyQuestQueueCalls { get; } = new();

    public List<(string QueueItemId, int Version)> ExpirePartyQuestQueueCalls { get; } = new();

    public List<(string QueueItemId, int Version)> RequeuePartyQuestQueueCalls { get; } = new();

    public List<string> StartSelectedPartyQuestCalls { get; } = new();

    public int AcceptPartyQuestInvitationCalls { get; private set; }

    public int RejectPartyQuestInvitationCalls { get; private set; }

    public List<(string QuestKey, DateTimeOffset CompletedAtUtc)> RemoveRecentlyCompletedQuestCalls { get; } = new();

    public int StartNewDayCalls { get; private set; }

    public List<StatAllocation> StatAllocationCalls { get; } = new();

    public int BuyHealthPotionCalls { get; private set; }

    public List<string> RemovePresetCalls { get; } = new();

    public List<(string PresetId, string Name)> RenamePresetCalls { get; } = new();

    public List<(EquipmentSetKind Kind, string Name)> SavePresetCalls { get; } = new();

    public SessionViewModel State { get; private set; }

    public SessionViewModel? StateAfterInitialize { get; set; }

    public int InitializeCalls { get; private set; }

    public LocalDataActionResult LocalDataResult { get; set; } =
        LocalDataActionResult.Success("Local data operation completed.", "{}");

    public PartyQuestActionResult StartSelectedPartyQuestResult { get; set; } =
        PartyQuestActionResult.Success("Quest started.");

    public PartyQuestActionResult InvitePartyQuestResult { get; set; } =
        PartyQuestActionResult.Success("Party invited to quest.");

    public Task ClearLocalDataAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task ClearDiagnosticsLogsAsync(CancellationToken cancellationToken = default)
    {
        State = State with
        {
            DiagnosticsLogEntries = Array.Empty<DiagnosticsLogEntry>()
        };
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public Task<InventoryActionResult> EquipEquipmentPresetAsync(string presetId, CancellationToken cancellationToken = default)
    {
        EquipPresetCalls.Add(presetId);
        return Task.FromResult(InventoryActionResult.Success("Preset equipped."));
    }

    public Task<InventoryActionResult> EquipGearSlotsAsync(
        EquipmentSetKind kind,
        GearSlotsSnapshot slots,
        string operationId,
        string label,
        CancellationToken cancellationToken = default)
    {
        EquipGearSlotsCalls.Add((kind, slots, operationId, label));
        return Task.FromResult(InventoryActionResult.Success("Gear equipped."));
    }

    public Task<InventoryActionResult> EquipInventoryItemAsync(EquipmentSetKind kind, string key, CancellationToken cancellationToken = default)
    {
        EquipItemCalls.Add((kind, key));
        return Task.FromResult(InventoryActionResult.Success("Equipment changed."));
    }

    public Task<LocalDataActionResult> ExportLocalDataAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(LocalDataResult);
    }

    public Task<SpellActionResult> CastSpellAsync(SpellCastRequest request, CancellationToken cancellationToken = default)
    {
        CastSpellCalls.Add(request);
        return Task.FromResult(SpellActionResult.Success("Spell cast."));
    }

    public Task<TaskActionResult> ScoreTaskAsync(TaskScoreRequest request, CancellationToken cancellationToken = default)
    {
        ScoreTaskCalls.Add(request);
        return Task.FromResult(TaskActionResult.Success("Task scored."));
    }

    public Task<SpellActionResult> StartNewDayAsync(CancellationToken cancellationToken = default)
    {
        StartNewDayCalls++;
        return Task.FromResult(SpellActionResult.Success("Started a new Habitica day."));
    }

    public Task<SpellActionResult> AllocateStatsAsync(StatAllocation allocation, CancellationToken cancellationToken = default)
    {
        StatAllocationCalls.Add(allocation);
        return Task.FromResult(SpellActionResult.Success("Stats allocated."));
    }

    public Task<InventoryActionResult> BuyArmoireAsync(int count, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(InventoryActionResult.Success("Armoire opened."));
    }

    public Task<InventoryActionResult> BuyHealthPotionAsync(CancellationToken cancellationToken = default)
    {
        BuyHealthPotionCalls++;
        return Task.FromResult(InventoryActionResult.Success("Health potion bought."));
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return Task.CompletedTask;
        }

        _initialized = true;
        InitializeCalls++;

        if (StateAfterInitialize is not null)
        {
            State = StateAfterInitialize;
            Changed?.Invoke();
        }

        return Task.CompletedTask;
    }

    public Task<LocalDataActionResult> ImportLocalDataAsync(
        string jsonText,
        LocalDataImportMode mode,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(LocalDataResult with { JsonText = jsonText });
    }

    public Task<LocalDataActionResult> ImportCloudSyncSectionsAsync(
        string jsonText,
        IReadOnlyDictionary<string, CloudSyncSectionImportDecision> sectionDecisions,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(LocalDataResult with { JsonText = jsonText });
    }

    public Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<InventoryActionResult> RemoveEquipmentPresetAsync(string presetId, CancellationToken cancellationToken = default)
    {
        RemovePresetCalls.Add(presetId);
        State = State with
        {
            EquipmentPresets = State.Presets.Where(preset => !string.Equals(preset.Id, presetId, StringComparison.Ordinal)).ToArray()
        };
        Changed?.Invoke();
        return Task.FromResult(InventoryActionResult.Success("Preset removed."));
    }

    public Task<InventoryActionResult> RenameEquipmentPresetAsync(string presetId, string name, CancellationToken cancellationToken = default)
    {
        RenamePresetCalls.Add((presetId, name));
        State = State with
        {
            EquipmentPresets = State.Presets
                .Select(preset => string.Equals(preset.Id, presetId, StringComparison.Ordinal) ? preset with { Name = name } : preset)
                .ToArray()
        };
        Changed?.Invoke();
        return Task.FromResult(InventoryActionResult.Success("Preset renamed."));
    }

    public Task<DiagnosticsPresetRunResult> RunDiagnosticsPresetAsync(DiagnosticsPreset preset, CancellationToken cancellationToken = default)
    {
        DiagnosticsPresetCalls++;
        return Task.FromResult(DiagnosticsPresetResult ?? new DiagnosticsPresetRunResult(preset, true, 0, string.Empty, "{}"));
    }

    public Task<LiveTestSuiteResult> RunReversibleGearTestAsync(CancellationToken cancellationToken = default)
    {
        ReversibleGearTestCalls++;
        return Task.FromResult(ReversibleGearTestResult ?? EmptyResult());
    }

    public Task<LiveTestSuiteResult> RunSafeLiveTestsAsync(CancellationToken cancellationToken = default)
    {
        SafeLiveTestCalls++;
        return Task.FromResult(SafeLiveTestResult ?? EmptyResult());
    }

    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task RefreshForPageAsync(string pageRoute, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<LocalDataActionResult> PreviewImportLocalDataAsync(string jsonText, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(LocalDataResult with { JsonText = jsonText });
    }

    public Task<LocalDataActionResult> PushCloudSyncAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(LocalDataResult);
    }

    public Task<LocalDataActionResult> DownloadCloudSyncAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(LocalDataResult);
    }

    public Task SetCloudSyncSectionExcludedAsync(CloudSyncSection section, bool isExcluded, CancellationToken cancellationToken = default)
    {
        State = State with
        {
            CloudSyncExcludedSections = isExcluded
                ? State.ExcludedCloudSyncSections.Concat(new[] { section }).Distinct().ToArray()
                : State.ExcludedCloudSyncSections.Where(item => item != section).ToArray()
        };
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public Task SetIncludeStalePartyMembersAsync(bool include, CancellationToken cancellationToken = default)
    {
        State = State with
        {
            IncludeStalePartyMembersInQuestForecasts = include
        };
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public Task<PartyQuestActionResult> RefreshPartyQuestStateAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(PartyQuestActionResult.Success("Shared quest state refreshed."));
    }

    public Task<PartyQuestActionResult> AddPartyQuestToQueueAsync(string questKey, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(PartyQuestActionResult.Success("Quest queued."));
    }

    public Task<PartyQuestActionResult> TogglePartyQuestVoteAsync(string queueItemId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(PartyQuestActionResult.Success("Vote updated."));
    }

    public Task<PartyQuestActionResult> RemovePartyQuestQueueItemAsync(string queueItemId, int version, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(PartyQuestActionResult.Success("Queue item removed."));
    }

    public Task<PartyQuestActionResult> PinPartyQuestQueueItemAsync(string queueItemId, int version, bool pinned, CancellationToken cancellationToken = default)
    {
        PinPartyQuestQueueCalls.Add((queueItemId, version, pinned));
        return Task.FromResult(PartyQuestActionResult.Success(pinned ? "Quest pinned." : "Quest unpinned."));
    }

    public Task<PartyQuestActionResult> SelectPartyQuestQueueItemAsync(string queueItemId, int version, CancellationToken cancellationToken = default)
    {
        SelectPartyQuestQueueCalls.Add((queueItemId, version));
        return Task.FromResult(PartyQuestActionResult.Success("Next quest selected."));
    }

    public Task<PartyQuestActionResult> SkipPartyQuestQueueItemAsync(string queueItemId, int version, CancellationToken cancellationToken = default)
    {
        SkipPartyQuestQueueCalls.Add((queueItemId, version));
        return Task.FromResult(PartyQuestActionResult.Success("Quest skipped."));
    }

    public Task<PartyQuestActionResult> ExpirePartyQuestQueueItemAsync(string queueItemId, int version, CancellationToken cancellationToken = default)
    {
        ExpirePartyQuestQueueCalls.Add((queueItemId, version));
        return Task.FromResult(PartyQuestActionResult.Success("Quest expired."));
    }

    public Task<PartyQuestActionResult> RequeuePartyQuestQueueItemAsync(string queueItemId, int version, CancellationToken cancellationToken = default)
    {
        RequeuePartyQuestQueueCalls.Add((queueItemId, version));
        return Task.FromResult(PartyQuestActionResult.Success("Quest returned to queue."));
    }

    public Task<PartyQuestActionResult> MarkPartyQuestCompletedAsync(string queueItemId, int version, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(PartyQuestActionResult.Success("Quest marked completed."));
    }

    public Task<PartyQuestActionResult> InvitePartyToQuestAsync(string queueItemId, int version, CancellationToken cancellationToken = default)
    {
        InvitePartyQuestCalls.Add((queueItemId, version));
        return Task.FromResult(InvitePartyQuestResult);
    }

    public Task<PartyQuestActionResult> StartSelectedPartyQuestAsync(string queueItemId, CancellationToken cancellationToken = default)
    {
        StartSelectedPartyQuestCalls.Add(queueItemId);
        return Task.FromResult(StartSelectedPartyQuestResult);
    }

    public Task<PartyQuestActionResult> AcceptPartyQuestInvitationAsync(CancellationToken cancellationToken = default)
    {
        AcceptPartyQuestInvitationCalls++;
        return Task.FromResult(PartyQuestActionResult.Success("Quest invitation accepted."));
    }

    public Task<PartyQuestActionResult> RejectPartyQuestInvitationAsync(CancellationToken cancellationToken = default)
    {
        RejectPartyQuestInvitationCalls++;
        return Task.FromResult(PartyQuestActionResult.Success("Quest invitation rejected."));
    }

    public Task<PartyQuestActionResult> AssignPartySyncOfficerAsync(string userId, string displayName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(PartyQuestActionResult.Success("Officer assigned."));
    }

    public Task<PartyQuestActionResult> AssignPartySyncOwnerAsync(string userId, string displayName, CancellationToken cancellationToken = default)
    {
        AssignPartyOwnerCalls.Add((userId, displayName));
        return Task.FromResult(PartyQuestActionResult.Success("Party owner assigned."));
    }

    public Task<PartyQuestActionResult> RemovePartySyncOfficerAsync(string userId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(PartyQuestActionResult.Success("Officer removed."));
    }

    public Task<PartyQuestActionResult> KickPartySyncMemberAsync(string userId, string displayName, string? reason, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(PartyQuestActionResult.Success("Member removed from party sync."));
    }

    public Task<PartyQuestActionResult> UnkickPartySyncMemberAsync(string userId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(PartyQuestActionResult.Success("Member restored to party sync."));
    }

    public Task<PartyQuestActionResult> UpdatePartySyncSettingsAsync(PartySyncSettings settings, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(PartyQuestActionResult.Success("Party sync settings updated."));
    }

    public Task<PartyQuestActionResult> RemovePartyRecentlyCompletedQuestAsync(string questKey, DateTimeOffset completedAtUtc, CancellationToken cancellationToken = default)
    {
        RemoveRecentlyCompletedQuestCalls.Add((questKey, completedAtUtc));
        return Task.FromResult(PartyQuestActionResult.Success("Completed quest removed."));
    }

    public Task SignInAsync(SignInRequest request, CancellationToken cancellationToken = default)
    {
        LastSignInRequest = request;
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public Task<InventoryActionResult> SaveEquipmentPresetAsync(EquipmentSetKind kind, string name, CancellationToken cancellationToken = default)
    {
        SavePresetCalls.Add((kind, name));
        return Task.FromResult(InventoryActionResult.Success("Preset saved."));
    }

    private static LiveTestSuiteResult EmptyResult()
    {
        return new LiveTestSuiteResult(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, Array.Empty<LiveTestResult>());
    }
}
