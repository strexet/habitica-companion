using System.Globalization;
using Habitica.Application.Auth;
using Habitica.Application.Diagnostics;
using Habitica.Application.Inventory;
using Habitica.Application.Sync;
using Habitica.Api;
using Habitica.Domain.Auth;
using Habitica.Domain.Diagnostics;
using Habitica.Domain.Party;
using Habitica.Domain.Sync;
using Habitica.Domain.User;
using Habitica.Storage;

namespace Habitica.WebApp.State;

public sealed class AppSessionController : IAppSessionController
{
    private readonly ICredentialStore _credentialStore;
    private readonly IDiagnosticsLogStore _diagnosticsLogStore;
    private readonly DiagnosticsLogWriter _diagnosticsLogWriter;
    private readonly DiagnosticsPresetWorkflow _diagnosticsPresetWorkflow;
    private readonly IEquipmentPresetStore _equipmentPresetStore;
    private readonly IGearCatalogStore _gearCatalogStore;
    private readonly IHabiticaSyncClient _habiticaSyncClient;
    private readonly LoginWorkflow _loginWorkflow;
    private readonly LiveTestWorkflow _liveTestWorkflow;
    private readonly IPartyCronHistoryStore _partyCronHistoryStore;
    private readonly IPartySnapshotStore _partySnapshotStore;
    private readonly SnapshotFreshnessPolicy _snapshotFreshnessPolicy;
    private readonly ITaskSnapshotStore _taskSnapshotStore;
    private readonly IUserSnapshotStore _userSnapshotStore;
    private readonly TimeProvider _timeProvider;
    private HabiticaCredentials? _currentCredentials;
    private bool _initialized;
    private bool _persistLocally;

    public AppSessionController(
        LoginWorkflow loginWorkflow,
        IHabiticaSyncClient habiticaSyncClient,
        LiveTestWorkflow liveTestWorkflow,
        DiagnosticsPresetWorkflow diagnosticsPresetWorkflow,
        ICredentialStore credentialStore,
        IEquipmentPresetStore equipmentPresetStore,
        IGearCatalogStore gearCatalogStore,
        IPartyCronHistoryStore partyCronHistoryStore,
        IPartySnapshotStore partySnapshotStore,
        ITaskSnapshotStore taskSnapshotStore,
        IUserSnapshotStore userSnapshotStore,
        IDiagnosticsLogStore diagnosticsLogStore,
        DiagnosticsLogWriter diagnosticsLogWriter,
        SnapshotFreshnessPolicy snapshotFreshnessPolicy,
        TimeProvider timeProvider)
    {
        _loginWorkflow = loginWorkflow;
        _habiticaSyncClient = habiticaSyncClient;
        _liveTestWorkflow = liveTestWorkflow;
        _diagnosticsPresetWorkflow = diagnosticsPresetWorkflow;
        _credentialStore = credentialStore;
        _equipmentPresetStore = equipmentPresetStore;
        _gearCatalogStore = gearCatalogStore;
        _partyCronHistoryStore = partyCronHistoryStore;
        _partySnapshotStore = partySnapshotStore;
        _taskSnapshotStore = taskSnapshotStore;
        _userSnapshotStore = userSnapshotStore;
        _diagnosticsLogStore = diagnosticsLogStore;
        _diagnosticsLogWriter = diagnosticsLogWriter;
        _snapshotFreshnessPolicy = snapshotFreshnessPolicy;
        _timeProvider = timeProvider;
    }

    public event Action? Changed;

    public SessionViewModel State { get; private set; } = SessionViewModel.Empty;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await LoadCachedStateAsync(cancellationToken);

        var persistedCredentials = await _credentialStore.GetPersistentCredentialsAsync(cancellationToken);

        if (persistedCredentials is not null)
        {
            await SignInCoreAsync(
                new SignInRequest
                {
                    ApiToken = persistedCredentials.ApiToken,
                    PersistLocally = true,
                    UserId = persistedCredentials.UserId
                },
                cancellationToken);
        }
    }

    public async Task SignInAsync(SignInRequest request, CancellationToken cancellationToken = default)
    {
        await SignInCoreAsync(request, cancellationToken);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var credentials = await ResolveCredentialsAsync(cancellationToken);

        if (credentials is not null)
        {
            await SignInCoreAsync(
                new SignInRequest
                {
                    ApiToken = credentials.ApiToken,
                    PersistLocally = _persistLocally || _currentCredentials is null,
                    UserId = credentials.UserId
                },
                cancellationToken);

            return;
        }

        SetState(State with
        {
            ErrorMessage = "Sign in is required before refreshing."
        });
    }

    public async Task<LiveTestSuiteResult> RunSafeLiveTestsAsync(CancellationToken cancellationToken = default)
    {
        var credentials = await ResolveCredentialsAsync(cancellationToken);
        if (credentials is null)
        {
            var message = "Sign in is required before running live tests.";
            SetState(State with
            {
                ErrorMessage = message
            });

            return BuildFailureResult("safe-live-tests", "Safe live tests", message, LiveTestRisk.Safe);
        }

        SetState(State with
        {
            ErrorMessage = null,
            IsBusy = true
        });

        try
        {
            var result = await _liveTestWorkflow.RunSafeLiveTestsAsync(credentials, cancellationToken);
            await LoadCachedStateAsync(cancellationToken);

            SetState(State with
            {
                ErrorMessage = null,
                IsBusy = false
            });

            return result;
        }
        catch (Exception exception)
        {
            await LoadCachedStateAsync(cancellationToken);

            SetState(State with
            {
                ErrorMessage = exception.Message,
                IsBusy = false
            });

            return BuildFailureResult("safe-live-tests", "Safe live tests", exception.Message, LiveTestRisk.Safe);
        }
    }

    public async Task<LiveTestSuiteResult> RunReversibleGearTestAsync(CancellationToken cancellationToken = default)
    {
        var credentials = await ResolveCredentialsAsync(cancellationToken);
        if (credentials is null)
        {
            var message = "Sign in is required before running the reversible gear test.";
            SetState(State with
            {
                ErrorMessage = message
            });

            return BuildFailureResult("reversible-gear-roundtrip", "Reversible gear roundtrip", message, LiveTestRisk.ReversibleMutation);
        }

        SetState(State with
        {
            ErrorMessage = null,
            IsBusy = true
        });

        try
        {
            var result = await _liveTestWorkflow.RunReversibleGearTestAsync(credentials, cancellationToken);
            await LoadCachedStateAsync(cancellationToken);

            SetState(State with
            {
                ErrorMessage = null,
                IsBusy = false
            });

            return result;
        }
        catch (Exception exception)
        {
            await LoadCachedStateAsync(cancellationToken);

            SetState(State with
            {
                ErrorMessage = exception.Message,
                IsBusy = false
            });

            return BuildFailureResult("reversible-gear-roundtrip", "Reversible gear roundtrip", exception.Message, LiveTestRisk.ReversibleMutation);
        }
    }

    public async Task<DiagnosticsPresetRunResult> RunDiagnosticsPresetAsync(DiagnosticsPreset preset, CancellationToken cancellationToken = default)
    {
        var credentials = await ResolveCredentialsAsync(cancellationToken);
        if (credentials is null)
        {
            var message = "Sign in is required before running diagnostics presets.";
            SetState(State with
            {
                ErrorMessage = message
            });

            return new DiagnosticsPresetRunResult(preset, false, 0, message, "{}");
        }

        SetState(State with
        {
            ErrorMessage = null,
            IsBusy = true
        });

        try
        {
            var result = await _diagnosticsPresetWorkflow.RunAsync(credentials, preset, cancellationToken);
            await LoadCachedStateAsync(cancellationToken);

            SetState(State with
            {
                ErrorMessage = null,
                IsBusy = false
            });

            return result;
        }
        catch (Exception exception)
        {
            await _diagnosticsLogWriter.WriteAsync(
                DiagnosticsFeatureArea.Diagnostics,
                $"preset-{preset.ToString().ToLowerInvariant()}",
                DiagnosticsSeverity.Error,
                DiagnosticsMode.LiveRead,
                exception.Message,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["preset"] = preset.ToString()
                },
                cancellationToken);

            await LoadCachedStateAsync(cancellationToken);

            SetState(State with
            {
                ErrorMessage = exception.Message,
                IsBusy = false
            });

            return new DiagnosticsPresetRunResult(preset, false, 0, exception.Message, "{}");
        }
    }

    public async Task ClearDiagnosticsLogsAsync(CancellationToken cancellationToken = default)
    {
        await _diagnosticsLogStore.ClearAsync(cancellationToken);
        await LoadCachedStateAsync(cancellationToken);
    }

    public async Task<InventoryActionResult> SaveEquipmentPresetAsync(
        EquipmentSetKind kind,
        string name,
        CancellationToken cancellationToken = default)
    {
        var credentials = await ResolveCredentialsAsync(cancellationToken);
        if (credentials is null || State.UserSnapshot is null)
        {
            return await FailInventoryActionAsync("inventory-save-preset", "Sign in and refresh account data before saving equipment presets.", cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return await FailInventoryActionAsync("inventory-save-preset", "Preset name is required.", cancellationToken);
        }

        var preset = new EquipmentPreset(
            Guid.NewGuid().ToString("N"),
            credentials.UserId,
            kind,
            name.Trim(),
            _timeProvider.GetUtcNow(),
            NormalizeBaseSlots(kind == EquipmentSetKind.Battle ? State.UserSnapshot.Equipment.Battle : State.UserSnapshot.Equipment.Costume));

        try
        {
            await _equipmentPresetStore.SaveAsync(preset, cancellationToken);
            await _diagnosticsLogWriter.WriteAsync(
                DiagnosticsFeatureArea.Inventory,
                "inventory-save-preset",
                DiagnosticsSeverity.Success,
                DiagnosticsMode.Local,
                $"Saved {kind.ToString().ToLowerInvariant()} preset '{preset.Name}'.",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["presetId"] = preset.Id,
                    ["presetName"] = preset.Name,
                    ["presetKind"] = kind.ToString()
                },
                cancellationToken);
            await LoadCachedStateAsync(cancellationToken);
            return InventoryActionResult.Success($"Saved preset {preset.Name}.");
        }
        catch (Exception exception)
        {
            await LoadCachedStateAsync(cancellationToken);
            return await FailInventoryActionAsync(
                "inventory-save-preset",
                exception.Message,
                cancellationToken,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["presetName"] = name.Trim(),
                    ["presetKind"] = kind.ToString()
                });
        }
    }

    public async Task<InventoryActionResult> RemoveEquipmentPresetAsync(string presetId, CancellationToken cancellationToken = default)
    {
        var credentials = await ResolveCredentialsAsync(cancellationToken);
        if (credentials is null)
        {
            return await FailInventoryActionAsync("inventory-remove-preset", "Sign in before removing equipment presets.", cancellationToken);
        }

        var preset = State.Presets.FirstOrDefault(item => string.Equals(item.Id, presetId, StringComparison.Ordinal));
        await _equipmentPresetStore.RemoveAsync(credentials.UserId, presetId, cancellationToken);
        await _diagnosticsLogWriter.WriteAsync(
            DiagnosticsFeatureArea.Inventory,
            "inventory-remove-preset",
            DiagnosticsSeverity.Success,
            DiagnosticsMode.Local,
            preset is null ? "Removed equipment preset." : $"Removed preset '{preset.Name}'.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["presetId"] = presetId,
                ["presetName"] = preset?.Name ?? "",
                ["presetKind"] = preset?.Kind.ToString() ?? ""
            },
            cancellationToken);
        await LoadCachedStateAsync(cancellationToken);
        return InventoryActionResult.Success(preset is null ? "Removed preset." : $"Removed preset {preset.Name}.");
    }

    public async Task<InventoryActionResult> RenameEquipmentPresetAsync(
        string presetId,
        string name,
        CancellationToken cancellationToken = default)
    {
        var credentials = await ResolveCredentialsAsync(cancellationToken);
        if (credentials is null)
        {
            return await FailInventoryActionAsync("inventory-rename-preset", "Sign in before renaming equipment presets.", cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return await FailInventoryActionAsync(
                "inventory-rename-preset",
                "Preset name is required.",
                cancellationToken,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["presetId"] = presetId
                });
        }

        var preset = State.Presets.FirstOrDefault(item => string.Equals(item.Id, presetId, StringComparison.Ordinal));
        if (preset is null)
        {
            return await FailInventoryActionAsync(
                "inventory-rename-preset",
                "Equipment preset was not found.",
                cancellationToken,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["presetId"] = presetId
                });
        }

        var renamedPreset = preset with { Name = name.Trim() };
        try
        {
            await _equipmentPresetStore.SaveAsync(renamedPreset, cancellationToken);
            await _diagnosticsLogWriter.WriteAsync(
                DiagnosticsFeatureArea.Inventory,
                "inventory-rename-preset",
                DiagnosticsSeverity.Success,
                DiagnosticsMode.Local,
                $"Renamed preset '{preset.Name}' to '{renamedPreset.Name}'.",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["presetId"] = preset.Id,
                    ["presetName"] = renamedPreset.Name,
                    ["previousPresetName"] = preset.Name,
                    ["presetKind"] = preset.Kind.ToString()
                },
                cancellationToken);
            await LoadCachedStateAsync(cancellationToken);
            return InventoryActionResult.Success($"Renamed preset {renamedPreset.Name}.");
        }
        catch (Exception exception)
        {
            await LoadCachedStateAsync(cancellationToken);
            return await FailInventoryActionAsync(
                "inventory-rename-preset",
                exception.Message,
                cancellationToken,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["presetId"] = preset.Id,
                    ["presetName"] = renamedPreset.Name,
                    ["presetKind"] = preset.Kind.ToString()
                });
        }
    }

    public async Task<InventoryActionResult> EquipInventoryItemAsync(
        EquipmentSetKind kind,
        string key,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateInventoryMutationAsync("inventory-equip-item", cancellationToken);
        if (validation.Result is not null)
        {
            return validation.Result;
        }

        var snapshot = validation.Snapshot!;
        if (IsUnequippedBaseKey(key))
        {
            return await FailInventoryActionAsync(
                "inventory-equip-item",
                $"{key} is an unequipped slot marker and cannot be sent to Habitica as gear.",
                cancellationToken,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["itemKey"] = key,
                    ["equipmentKind"] = kind.ToString()
                });
        }

        if (!CanUseGearKey(snapshot, kind, key))
        {
            return await FailInventoryActionAsync(
                "inventory-equip-item",
                $"Cannot equip {key} because it is not in the cached owned or equipped gear list.",
                cancellationToken,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["itemKey"] = key,
                    ["equipmentKind"] = kind.ToString()
                });
        }

        SetState(State with { ErrorMessage = null, IsBusy = true });

        try
        {
            await _habiticaSyncClient.EquipGearAsync(validation.Credentials!, kind, key, cancellationToken);
            var refreshedSnapshot = await _habiticaSyncClient.GetUserSnapshotAsync(validation.Credentials!, cancellationToken);
            await _userSnapshotStore.SaveAsync(refreshedSnapshot, cancellationToken);
            await _diagnosticsLogWriter.WriteAsync(
                DiagnosticsFeatureArea.Inventory,
                "inventory-equip-item",
                DiagnosticsSeverity.Success,
                DiagnosticsMode.LiveMutation,
                $"Changed {kind.ToString().ToLowerInvariant()} equipment to {key}.",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["itemKey"] = key,
                    ["equipmentKind"] = kind.ToString(),
                    ["requestCount"] = "2"
                },
                cancellationToken);
            await LoadCachedStateAsync(cancellationToken);
            SetState(State with { ErrorMessage = null, IsBusy = false });
            return InventoryActionResult.Success($"Equipment changed to {ResolveGearName(key)}.");
        }
        catch (Exception exception)
        {
            await LoadCachedStateAsync(cancellationToken);
            SetState(State with { ErrorMessage = exception.Message, IsBusy = false });
            return await FailInventoryActionAsync(
                "inventory-equip-item",
                exception.Message,
                cancellationToken,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["itemKey"] = key,
                    ["equipmentKind"] = kind.ToString()
                });
        }
    }

    public async Task<InventoryActionResult> EquipEquipmentPresetAsync(string presetId, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateInventoryMutationAsync("inventory-equip-preset", cancellationToken);
        if (validation.Result is not null)
        {
            return validation.Result;
        }

        var preset = State.Presets.FirstOrDefault(item => string.Equals(item.Id, presetId, StringComparison.Ordinal));
        if (preset is null)
        {
            return await FailInventoryActionAsync(
                "inventory-equip-preset",
                "Equipment preset was not found.",
                cancellationToken,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["presetId"] = presetId
                });
        }

        var desiredSlots = EnumerateSlots(NormalizeBaseSlots(preset.Slots)).ToArray();
        foreach (var slot in desiredSlots.Where(slot => !string.IsNullOrWhiteSpace(slot.Key)))
        {
            if (!CanUseGearKey(validation.Snapshot!, preset.Kind, slot.Key!))
            {
                return await FailInventoryActionAsync(
                    "inventory-equip-preset",
                    $"Cannot equip preset '{preset.Name}' because {slot.Key} is not owned.",
                    cancellationToken,
                    PresetMetadata(preset, failedSlot: slot.SlotTitle, itemKey: slot.Key));
            }
        }

        var currentSlots = preset.Kind == EquipmentSetKind.Battle
            ? validation.Snapshot!.Equipment.Battle
            : validation.Snapshot!.Equipment.Costume;
        var changedSlots = desiredSlots
            .Where(slot => !string.Equals(NormalizeGearKey(GetSlotValue(currentSlots, slot.SlotTitle)), slot.Key, StringComparison.Ordinal))
            .ToArray();

        SetState(State with { ErrorMessage = null, IsBusy = true });

        var requestCount = 0;
        try
        {
            foreach (var slot in changedSlots)
            {
                var keyToToggle = slot.Key ?? NormalizeGearKey(GetSlotValue(currentSlots, slot.SlotTitle));
                if (string.IsNullOrWhiteSpace(keyToToggle))
                {
                    continue;
                }

                await _habiticaSyncClient.EquipGearAsync(validation.Credentials!, preset.Kind, keyToToggle, cancellationToken);
                requestCount++;
            }

            if (changedSlots.Length > 0)
            {
                var refreshedSnapshot = await _habiticaSyncClient.GetUserSnapshotAsync(validation.Credentials!, cancellationToken);
                requestCount++;
                await _userSnapshotStore.SaveAsync(refreshedSnapshot, cancellationToken);
            }

            await _diagnosticsLogWriter.WriteAsync(
                DiagnosticsFeatureArea.Inventory,
                "inventory-equip-preset",
                DiagnosticsSeverity.Success,
                changedSlots.Length == 0 ? DiagnosticsMode.Local : DiagnosticsMode.LiveMutation,
                changedSlots.Length == 0
                    ? $"Preset '{preset.Name}' was already equipped."
                    : $"Equipped preset '{preset.Name}'.",
                PresetMetadata(preset, changedSlots.Length, desiredSlots.Length - changedSlots.Length, requestCount),
                cancellationToken);
            await LoadCachedStateAsync(cancellationToken);
            SetState(State with { ErrorMessage = null, IsBusy = false });
            return InventoryActionResult.Success(changedSlots.Length == 0 ? $"Preset {preset.Name} was already equipped." : $"Equipped preset {preset.Name}.");
        }
        catch (Exception exception)
        {
            await LoadCachedStateAsync(cancellationToken);
            SetState(State with { ErrorMessage = exception.Message, IsBusy = false });
            return await FailInventoryActionAsync(
                "inventory-equip-preset",
                exception.Message,
                cancellationToken,
                PresetMetadata(preset, changedSlots.Length, desiredSlots.Length - changedSlots.Length, requestCount));
        }
    }

    public Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        _currentCredentials = null;
        var cachedUserSnapshot = State.UserSnapshot;

        SetState(State with
        {
            DisplayName = cachedUserSnapshot?.DisplayName,
            ClassName = cachedUserSnapshot?.ClassName,
            ErrorMessage = null,
            IsAuthenticated = false,
            Level = cachedUserSnapshot?.Level
        });

        return Task.CompletedTask;
    }

    public async Task ClearLocalDataAsync(CancellationToken cancellationToken = default)
    {
        _currentCredentials = null;
        _persistLocally = false;

        await _credentialStore.ClearPersistentCredentialsAsync(cancellationToken);
        await _diagnosticsLogStore.ClearAsync(cancellationToken);
        await _equipmentPresetStore.ClearAsync(cancellationToken);
        await _gearCatalogStore.ClearAsync(cancellationToken);
        await _partyCronHistoryStore.ClearAsync(cancellationToken);
        await _partySnapshotStore.ClearAsync(cancellationToken);
        await _taskSnapshotStore.ClearAsync(cancellationToken);
        await _userSnapshotStore.ClearAsync(cancellationToken);

        SetState(SessionViewModel.Empty);
    }

    private async Task LoadCachedStateAsync(CancellationToken cancellationToken)
    {
        var diagnosticsLogEntries = await _diagnosticsLogStore.GetRecentAsync(cancellationToken);
        var gearCatalog = await _gearCatalogStore.GetLatestAsync(cancellationToken);
        var taskSnapshot = await _taskSnapshotStore.GetLatestAsync(cancellationToken);
        var userSnapshot = await _userSnapshotStore.GetLatestAsync(cancellationToken);
        var partySnapshot = await _partySnapshotStore.GetLatestAsync(cancellationToken);
        var userId = State.UserId ?? _currentCredentials?.UserId;
        var equipmentPresets = string.IsNullOrWhiteSpace(userId)
            ? Array.Empty<EquipmentPreset>()
            : await _equipmentPresetStore.GetForUserAsync(userId, cancellationToken);

        SetState(State with
        {
            ClassName = userSnapshot?.ClassName ?? State.ClassName,
            DisplayName = userSnapshot?.DisplayName ?? State.DisplayName,
            LastSyncedAtUtc = GetLatestSyncTimestamp(taskSnapshot, userSnapshot, partySnapshot),
            Level = userSnapshot?.Level ?? State.Level,
            PartyFreshness = ClassifyFreshness(partySnapshot),
            PartySnapshot = partySnapshot,
            TaskFreshness = ClassifyFreshness(taskSnapshot),
            TaskSnapshot = taskSnapshot,
            DiagnosticsLogEntries = diagnosticsLogEntries,
            GearCatalogSnapshot = gearCatalog,
            EquipmentPresets = equipmentPresets,
            UserId = userId,
            UserFreshness = ClassifyFreshness(userSnapshot),
            UserSnapshot = userSnapshot
        });
    }

    private async Task SignInCoreAsync(SignInRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.ApiToken))
        {
            SetState(State with
            {
                ErrorMessage = "Habitica User ID and API Token are required."
            });
            return;
        }

        SetState(State with
        {
            ErrorMessage = null,
            IsBusy = true
        });

        try
        {
            var loginResult = await _loginWorkflow.AuthenticateAndSyncAsync(
                new LoginCommand(request.UserId.Trim(), request.ApiToken.Trim(), request.PersistLocally),
                cancellationToken);
            var partySnapshot = await _partySnapshotStore.GetLatestAsync(cancellationToken);
            var taskSnapshot = await _taskSnapshotStore.GetLatestAsync(cancellationToken);
            var userSnapshot = await _userSnapshotStore.GetLatestAsync(cancellationToken);
            var gearCatalog = await RefreshGearCatalogAsync(new HabiticaCredentials(request.UserId.Trim(), request.ApiToken.Trim()), cancellationToken);

            _currentCredentials = new HabiticaCredentials(request.UserId.Trim(), request.ApiToken.Trim());
            _persistLocally = request.PersistLocally;
            var equipmentPresets = await _equipmentPresetStore.GetForUserAsync(_currentCredentials.UserId, cancellationToken);

            SetState(new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: loginResult.DisplayName,
                ErrorMessage: null,
                LastSyncedAtUtc: loginResult.RetrievedAtUtc,
                PartyFreshness: ClassifyFreshness(partySnapshot),
                PartySnapshot: partySnapshot,
                TaskFreshness: ClassifyFreshness(taskSnapshot),
                TaskSnapshot: taskSnapshot,
                DiagnosticsLogEntries: await _diagnosticsLogStore.GetRecentAsync(cancellationToken),
                ClassName: loginResult.ClassName,
                Level: loginResult.Level,
                UserSnapshot: userSnapshot,
                UserFreshness: ClassifyFreshness(userSnapshot),
                UserId: _currentCredentials.UserId,
                GearCatalogSnapshot: gearCatalog,
                EquipmentPresets: equipmentPresets));
        }
        catch (Exception exception)
        {
            var partySnapshot = await _partySnapshotStore.GetLatestAsync(cancellationToken);
            var taskSnapshot = await _taskSnapshotStore.GetLatestAsync(cancellationToken);
            var userSnapshot = await _userSnapshotStore.GetLatestAsync(cancellationToken);

            SetState(State with
            {
                ErrorMessage = exception.Message,
                IsBusy = false,
                LastSyncedAtUtc = GetLatestSyncTimestamp(taskSnapshot ?? State.TaskSnapshot, userSnapshot ?? State.UserSnapshot, partySnapshot ?? State.PartySnapshot) ?? State.LastSyncedAtUtc,
                PartyFreshness = ClassifyFreshness(partySnapshot ?? State.PartySnapshot),
                PartySnapshot = partySnapshot ?? State.PartySnapshot,
                TaskFreshness = ClassifyFreshness(taskSnapshot ?? State.TaskSnapshot),
                TaskSnapshot = taskSnapshot ?? State.TaskSnapshot,
                UserFreshness = ClassifyFreshness(userSnapshot ?? State.UserSnapshot),
                UserSnapshot = userSnapshot ?? State.UserSnapshot
            });
        }
    }

    private async Task<GearCatalogSnapshot?> RefreshGearCatalogAsync(
        HabiticaCredentials credentials,
        CancellationToken cancellationToken)
    {
        try
        {
            var catalog = await _habiticaSyncClient.GetContentCatalogAsync(credentials, cancellationToken);
            await _gearCatalogStore.SaveAsync(catalog, cancellationToken);
            await _diagnosticsLogWriter.WriteAsync(
                DiagnosticsFeatureArea.Inventory,
                "inventory-refresh-catalog",
                DiagnosticsSeverity.Success,
                DiagnosticsMode.LiveRead,
                "Refreshed gear content catalog.",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["itemCount"] = catalog.Items.Count.ToString(CultureInfo.InvariantCulture),
                    ["requestCount"] = "1"
                },
                cancellationToken);
            return catalog;
        }
        catch (Exception exception)
        {
            await _diagnosticsLogWriter.WriteAsync(
                DiagnosticsFeatureArea.Inventory,
                "inventory-refresh-catalog",
                DiagnosticsSeverity.Warning,
                DiagnosticsMode.LiveRead,
                exception.Message,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["requestCount"] = "1"
                },
                cancellationToken);
            return await _gearCatalogStore.GetLatestAsync(cancellationToken);
        }
    }

    private async Task<InventoryMutationValidation> ValidateInventoryMutationAsync(
        string operation,
        CancellationToken cancellationToken)
    {
        var credentials = await ResolveCredentialsAsync(cancellationToken);
        if (credentials is null)
        {
            return new InventoryMutationValidation(null, null, await FailInventoryActionAsync(operation, "Sign in is required before changing equipment.", cancellationToken));
        }

        if (State.UserSnapshot is null)
        {
            return new InventoryMutationValidation(credentials, null, await FailInventoryActionAsync(operation, "Refresh account data before changing equipment.", cancellationToken));
        }

        if (State.UserFreshness != SnapshotFreshnessState.Fresh)
        {
            return new InventoryMutationValidation(credentials, State.UserSnapshot, await FailInventoryActionAsync(operation, "Fresh account data is required before changing equipment.", cancellationToken));
        }

        return new InventoryMutationValidation(credentials, State.UserSnapshot, null);
    }

    private async Task<InventoryActionResult> FailInventoryActionAsync(
        string operation,
        string message,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        await _diagnosticsLogWriter.WriteAsync(
            DiagnosticsFeatureArea.Inventory,
            operation,
            DiagnosticsSeverity.Error,
            operation.Contains("equip", StringComparison.Ordinal) ? DiagnosticsMode.LiveMutation : DiagnosticsMode.Local,
            message,
            metadata,
            cancellationToken);
        var diagnosticsLogEntries = await _diagnosticsLogStore.GetRecentAsync(cancellationToken);

        SetState(State with
        {
            DiagnosticsLogEntries = diagnosticsLogEntries,
            ErrorMessage = message,
            IsBusy = false
        });

        return InventoryActionResult.Failure(message);
    }

    private string ResolveGearName(string key)
    {
        return State.GearCatalogSnapshot?.Items.TryGetValue(key, out var item) == true
            ? item.Text
            : key;
    }

    private static bool CanUseGearKey(UserSnapshot snapshot, EquipmentSetKind kind, string key)
    {
        if (IsUnequippedBaseKey(key))
        {
            return false;
        }

        return snapshot.Inventory.OwnedGearKeys.Contains(key, StringComparer.Ordinal)
            || EnumerateSlots(kind == EquipmentSetKind.Battle ? snapshot.Equipment.Battle : snapshot.Equipment.Costume)
                .Any(slot => string.Equals(slot.Key, key, StringComparison.Ordinal));
    }

    private static GearSlotsSnapshot NormalizeBaseSlots(GearSlotsSnapshot slots)
    {
        return new GearSlotsSnapshot(
            NormalizeGearKey(slots.Head),
            NormalizeGearKey(slots.Armor),
            NormalizeGearKey(slots.Weapon),
            NormalizeGearKey(slots.Shield),
            NormalizeGearKey(slots.Back));
    }

    private static string? NormalizeGearKey(string? key)
    {
        return string.IsNullOrWhiteSpace(key) || IsUnequippedBaseKey(key) ? null : key;
    }

    private static bool IsUnequippedBaseKey(string key)
    {
        return key.EndsWith("_base_0", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, string> PresetMetadata(
        EquipmentPreset preset,
        int changedSlotCount = 0,
        int skippedSlotCount = 0,
        int requestCount = 0,
        string? failedSlot = null,
        string? itemKey = null)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["presetId"] = preset.Id,
            ["presetName"] = preset.Name,
            ["presetKind"] = preset.Kind.ToString(),
            ["changedSlotCount"] = changedSlotCount.ToString(CultureInfo.InvariantCulture),
            ["skippedSlotCount"] = skippedSlotCount.ToString(CultureInfo.InvariantCulture),
            ["requestCount"] = requestCount.ToString(CultureInfo.InvariantCulture)
        };

        if (!string.IsNullOrWhiteSpace(failedSlot))
        {
            metadata["failedSlot"] = failedSlot;
        }

        if (!string.IsNullOrWhiteSpace(itemKey))
        {
            metadata["itemKey"] = itemKey;
        }

        return metadata;
    }

    private static IEnumerable<(string SlotTitle, string? Key)> EnumerateSlots(GearSlotsSnapshot slots)
    {
        yield return ("Head", slots.Head);
        yield return ("Armor", slots.Armor);
        yield return ("Weapon", slots.Weapon);
        yield return ("Shield", slots.Shield);
        yield return ("Back", slots.Back);
    }

    private static string? GetSlotValue(GearSlotsSnapshot slots, string slotTitle)
    {
        return slotTitle switch
        {
            "Head" => slots.Head,
            "Armor" => slots.Armor,
            "Weapon" => slots.Weapon,
            "Shield" => slots.Shield,
            "Back" => slots.Back,
            _ => null
        };
    }

    private sealed record InventoryMutationValidation(
        HabiticaCredentials? Credentials,
        UserSnapshot? Snapshot,
        InventoryActionResult? Result);

    private SnapshotFreshnessState ClassifyFreshness(Habitica.Domain.Tasks.TaskCollectionSnapshot? snapshot)
    {
        return _snapshotFreshnessPolicy.Classify(
            SnapshotCategory.VolatileGameplayState,
            snapshot?.RetrievedAtUtc,
            _timeProvider.GetUtcNow());
    }

    private SnapshotFreshnessState ClassifyFreshness(UserSnapshot? snapshot)
    {
        return _snapshotFreshnessPolicy.Classify(
            SnapshotCategory.VolatileGameplayState,
            snapshot?.RetrievedAtUtc,
            _timeProvider.GetUtcNow());
    }

    private SnapshotFreshnessState ClassifyFreshness(PartySnapshot? snapshot)
    {
        return _snapshotFreshnessPolicy.Classify(
            SnapshotCategory.VolatileGameplayState,
            snapshot?.RetrievedAtUtc,
            _timeProvider.GetUtcNow());
    }

    private static DateTimeOffset? GetLatestSyncTimestamp(
        Habitica.Domain.Tasks.TaskCollectionSnapshot? taskSnapshot,
        UserSnapshot? userSnapshot,
        PartySnapshot? partySnapshot)
    {
        return new[]
        {
            taskSnapshot?.RetrievedAtUtc,
            userSnapshot?.RetrievedAtUtc,
            partySnapshot?.RetrievedAtUtc
        }.Max();
    }

    private void SetState(SessionViewModel nextState)
    {
        State = nextState;
        Changed?.Invoke();
    }

    private async Task<HabiticaCredentials?> ResolveCredentialsAsync(CancellationToken cancellationToken)
    {
        if (_currentCredentials is not null)
        {
            return _currentCredentials;
        }

        var persistedCredentials = await _credentialStore.GetPersistentCredentialsAsync(cancellationToken);
        if (persistedCredentials is not null)
        {
            _persistLocally = true;
        }

        return persistedCredentials;
    }

    private LiveTestSuiteResult BuildFailureResult(string id, string title, string message, LiveTestRisk risk)
    {
        var now = _timeProvider.GetUtcNow();
        return new LiveTestSuiteResult(
            now,
            now,
            new[]
            {
                new LiveTestResult(id, title, LiveTestStatus.Failed, risk, 0, message)
            });
    }
}
