using Habitica.Domain.Diagnostics;
using Habitica.Domain.Party;
using Habitica.Domain.Sync;
using Habitica.Domain.Tasks;
using Habitica.Domain.User;
using Habitica.Application.Sync;

namespace Habitica.WebApp.State;

public sealed record SessionViewModel(
    bool IsBusy,
    bool IsAuthenticated,
    string? DisplayName,
    string? ErrorMessage,
    DateTimeOffset? LastSyncedAtUtc,
    SnapshotFreshnessState TaskFreshness,
    TaskCollectionSnapshot? TaskSnapshot,
    string? ClassName = null,
    int? Level = null,
    UserSnapshot? UserSnapshot = null,
    SnapshotFreshnessState UserFreshness = SnapshotFreshnessState.Missing,
    PartySnapshot? PartySnapshot = null,
    SnapshotFreshnessState PartyFreshness = SnapshotFreshnessState.Missing,
    PartyQuestQueueSnapshot? PartyQuestQueue = null,
    IReadOnlyList<DiagnosticsLogEntry>? DiagnosticsLogEntries = null,
    string? UserId = null,
    GearCatalogSnapshot? GearCatalogSnapshot = null,
    IReadOnlyList<EquipmentPreset>? EquipmentPresets = null,
    SpellCastProgress? ActiveSpellCastProgress = null,
    TaskMutationProgress? ActiveTaskMutationProgress = null,
    EquipmentProgress? ActiveEquipmentProgress = null,
    bool IncludeStalePartyMembersInQuestForecasts = false,
    IReadOnlyDictionary<Habitica.Application.Sync.RefreshDomain, Habitica.Application.Sync.DomainRefreshState>? DomainStates = null,
    bool IsAdmin = false,
    bool IsPartySyncEnabled = true,
    IReadOnlyList<CloudSyncSectionStatus>? CloudSyncSectionStatuses = null,
    IReadOnlyList<CloudSyncSection>? CloudSyncExcludedSections = null,
    PetsMountsQueueProgress? ActivePetsMountsQueueProgress = null)
{
    public static SessionViewModel Empty { get; } = new(
        IsBusy: false,
        IsAuthenticated: false,
        DisplayName: null,
        ErrorMessage: null,
        LastSyncedAtUtc: null,
        TaskFreshness: SnapshotFreshnessState.Missing,
        TaskSnapshot: null,
        DiagnosticsLogEntries: Array.Empty<DiagnosticsLogEntry>());

    public bool HasCachedTasks => TaskSnapshot?.Items.Count > 0;

    public bool HasCachedUserSnapshot => UserSnapshot is not null;

    public bool HasCachedPartySnapshot => PartySnapshot is not null;

    public bool HasDiagnosticsHistory => DiagnosticsLogEntries is { Count: > 0 };

    public IReadOnlyList<EquipmentPreset> Presets => EquipmentPresets ?? Array.Empty<EquipmentPreset>();

    public int DiagnosticsWarningCount =>
        DiagnosticsLogEntries?.Count(entry => entry.Severity is DiagnosticsSeverity.Warning or DiagnosticsSeverity.Error) ?? 0;

    public IReadOnlyList<CloudSyncSectionStatus> CloudSyncStatuses => CloudSyncSectionStatuses ?? Array.Empty<CloudSyncSectionStatus>();

    public IReadOnlyList<CloudSyncSection> ExcludedCloudSyncSections => CloudSyncExcludedSections ?? Array.Empty<CloudSyncSection>();

    public bool IsRefreshing => DomainStates?.Values.Any(static state =>
        state.IsFetching && state.Domain is not RefreshDomain.CloudSync and not RefreshDomain.PartySync) == true;

    public bool IsCloudSyncing => DomainStates?.TryGetValue(RefreshDomain.CloudSync, out var state) == true && state.IsFetching;

    public bool HasSyncFailure => DomainStates?.Values.Any(static state => !string.IsNullOrWhiteSpace(state.LastError)) == true;

    public bool HasStaleSync =>
        IsStale(TaskFreshness) ||
        IsStale(UserFreshness) ||
        IsStale(PartyFreshness);

    private static bool IsStale(SnapshotFreshnessState freshness) =>
        freshness is SnapshotFreshnessState.Stale or SnapshotFreshnessState.Expired;
}

public sealed record SpellCastRequest(
    string SpellId,
    string? TargetTaskId,
    int Count,
    bool AutoEquipRecommendedGear = false,
    GearSlotsSnapshot? AutoEquipGearSlots = null);

public sealed record TaskScoreRequest(
    string TaskId,
    Habitica.Domain.Tasks.TaskScoreDirection Direction,
    int Count = 1);

public sealed record SpellCastProgress(
    string SpellId,
    int Completed,
    int Total);

public sealed record TaskMutationProgress(
    string TaskId,
    Habitica.Domain.Tasks.TaskScoreDirection Direction,
    int Completed,
    int Total);

public sealed record EquipmentProgress(
    string OperationId,
    string Label,
    int Completed,
    int Total);

public enum PetsMountsQueueOperation
{
    Feed,
    Hatch
}

public sealed record PetsMountsQueueProgress(
    PetsMountsQueueOperation Operation,
    int Completed,
    int Total);

public sealed record SpellActionResult(
    bool Succeeded,
    string Message)
{
    public static SpellActionResult Success(string message) => new(true, message);

    public static SpellActionResult Failure(string message) => new(false, message);
}

public sealed record PartyQuestActionResult(
    bool Succeeded,
    string Message)
{
    public static PartyQuestActionResult Success(string message) => new(true, message);

    public static PartyQuestActionResult Failure(string message) => new(false, message);
}

public sealed record TaskActionResult(
    bool Succeeded,
    string Message)
{
    public static TaskActionResult Success(string message) => new(true, message);

    public static TaskActionResult Failure(string message) => new(false, message);
}

public sealed record LocalDataActionResult(
    bool Succeeded,
    string Message,
    string? JsonText = null,
    LocalUserDataImportPreview? ImportPreview = null)
{
    public static LocalDataActionResult Success(
        string message,
        string? jsonText = null,
        LocalUserDataImportPreview? importPreview = null)
    {
        return new LocalDataActionResult(true, message, jsonText, importPreview);
    }

    public static LocalDataActionResult Failure(string message)
    {
        return new LocalDataActionResult(false, message);
    }
}
