using System.Text.Json;
using Habitica.Api;
using Habitica.Domain.Auth;
using Habitica.Domain.Diagnostics;

namespace Habitica.Application.Diagnostics;

public sealed class DiagnosticsPresetWorkflow
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IHabiticaSyncClient _habiticaSyncClient;
    private readonly DiagnosticsLogWriter _logWriter;

    public DiagnosticsPresetWorkflow(
        IHabiticaSyncClient habiticaSyncClient,
        DiagnosticsLogWriter logWriter)
    {
        _habiticaSyncClient = habiticaSyncClient;
        _logWriter = logWriter;
    }

    public Task<DiagnosticsPresetRunResult> RunAsync(
        HabiticaCredentials credentials,
        DiagnosticsPreset preset,
        CancellationToken cancellationToken)
    {
        return preset switch
        {
            DiagnosticsPreset.UserAccount => RunUserAccountAsync(credentials, cancellationToken),
            DiagnosticsPreset.UserInventory => RunUserInventoryAsync(credentials, cancellationToken),
            DiagnosticsPreset.TasksUser => RunTasksAsync(credentials, cancellationToken),
            DiagnosticsPreset.Party => RunPartyAsync(credentials, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null)
        };
    }

    private async Task<DiagnosticsPresetRunResult> RunUserAccountAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
    {
        var snapshot = await _habiticaSyncClient.GetUserSnapshotAsync(credentials, cancellationToken);
        var preview = JsonSerializer.Serialize(new
        {
            displayName = snapshot.DisplayName,
            className = snapshot.ClassName,
            level = snapshot.Level,
            health = snapshot.Health,
            maxHealth = snapshot.MaxHealth,
            mana = snapshot.Mana,
            maxMana = snapshot.MaxMana,
            experience = snapshot.Experience,
            toNextLevel = snapshot.ToNextLevel,
            gold = snapshot.Gold
        }, JsonOptions);

        await WriteSuccessLogAsync("preset-user-account", DiagnosticsPreset.UserAccount, cancellationToken);

        return new DiagnosticsPresetRunResult(
            DiagnosticsPreset.UserAccount,
            true,
            1,
            $"{snapshot.DisplayName} level {snapshot.Level} account snapshot loaded.",
            preview);
    }

    private async Task<DiagnosticsPresetRunResult> RunUserInventoryAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
    {
        var snapshot = await _habiticaSyncClient.GetUserSnapshotAsync(credentials, cancellationToken);
        var preview = JsonSerializer.Serialize(new
        {
            currentPetKey = snapshot.CurrentPetKey,
            currentMountKey = snapshot.CurrentMountKey,
            ownedGearCount = snapshot.Inventory.OwnedGearKeys.Length,
            eggCount = snapshot.Inventory.EggCount,
            foodCount = snapshot.Inventory.FoodCount,
            hatchingPotionCount = snapshot.Inventory.HatchingPotionCount,
            questCount = snapshot.Inventory.QuestCount
        }, JsonOptions);

        await WriteSuccessLogAsync("preset-user-inventory", DiagnosticsPreset.UserInventory, cancellationToken);

        return new DiagnosticsPresetRunResult(
            DiagnosticsPreset.UserInventory,
            true,
            1,
            $"Inventory preset loaded with {snapshot.Inventory.OwnedGearKeys.Length} owned gear keys.",
            preview);
    }

    private async Task<DiagnosticsPresetRunResult> RunTasksAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
    {
        var snapshot = await _habiticaSyncClient.GetTasksAsync(credentials, cancellationToken);
        var preview = JsonSerializer.Serialize(new
        {
            total = snapshot.Items.Count,
            open = snapshot.Items.Count(task => !task.IsCompleted),
            completed = snapshot.Items.Count(task => task.IsCompleted),
            sample = snapshot.Items.Take(5).Select(task => new
            {
                id = task.Id,
                text = task.Text,
                type = task.Type.ToString(),
                isCompleted = task.IsCompleted
            })
        }, JsonOptions);

        await WriteSuccessLogAsync("preset-tasks-user", DiagnosticsPreset.TasksUser, cancellationToken);

        return new DiagnosticsPresetRunResult(
            DiagnosticsPreset.TasksUser,
            true,
            1,
            $"Tasks preset loaded {snapshot.Items.Count} tasks.",
            preview);
    }

    private async Task<DiagnosticsPresetRunResult> RunPartyAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
    {
        var snapshot = await _habiticaSyncClient.GetPartySnapshotAsync(credentials, cancellationToken);
        var preview = JsonSerializer.Serialize(new
        {
            name = snapshot.Name,
            memberCount = snapshot.MemberCount,
            summary = snapshot.Summary,
            quest = snapshot.Quest is null ? null : new
            {
                key = snapshot.Quest.Key,
                isActive = snapshot.Quest.IsActive,
                progressUp = snapshot.Quest.ProgressUp,
                progressDown = snapshot.Quest.ProgressDown,
                participantCount = snapshot.Quest.ParticipantCount
            }
        }, JsonOptions);

        await WriteSuccessLogAsync("preset-party", DiagnosticsPreset.Party, cancellationToken);

        return new DiagnosticsPresetRunResult(
            DiagnosticsPreset.Party,
            true,
            1,
            $"Party preset loaded {snapshot.Name}.",
            preview);
    }

    private Task WriteSuccessLogAsync(string operation, DiagnosticsPreset preset, CancellationToken cancellationToken)
    {
        return _logWriter.WriteAsync(
            DiagnosticsFeatureArea.Diagnostics,
            operation,
            DiagnosticsSeverity.Success,
            DiagnosticsMode.LiveRead,
            $"Fetched curated diagnostics preset {preset}.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["requestCount"] = "1",
                ["preset"] = preset.ToString()
            },
            cancellationToken);
    }
}
