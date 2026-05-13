using Habitica.Domain.Diagnostics;
using Habitica.Domain.Party;
using Habitica.Domain.Sync;
using Habitica.Domain.Tasks;
using Habitica.Domain.User;

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
    IReadOnlyList<DiagnosticsLogEntry>? DiagnosticsLogEntries = null,
    string? UserId = null,
    GearCatalogSnapshot? GearCatalogSnapshot = null,
    IReadOnlyList<EquipmentPreset>? EquipmentPresets = null,
    SpellCastProgress? ActiveSpellCastProgress = null,
    EquipmentProgress? ActiveEquipmentProgress = null)
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
}

public sealed record SpellCastRequest(
    string SpellId,
    string? TargetTaskId,
    int Count,
    bool AutoEquipRecommendedGear = false,
    GearSlotsSnapshot? AutoEquipGearSlots = null);

public sealed record SpellCastProgress(
    string SpellId,
    int Completed,
    int Total);

public sealed record EquipmentProgress(
    string OperationId,
    string Label,
    int Completed,
    int Total);

public sealed record SpellActionResult(
    bool Succeeded,
    string Message)
{
    public static SpellActionResult Success(string message) => new(true, message);

    public static SpellActionResult Failure(string message) => new(false, message);
}
