using System.Globalization;
using Habitica.Api;
using Habitica.Domain.Auth;
using Habitica.Domain.Diagnostics;
using Habitica.Domain.Party;
using Habitica.Domain.Tasks;
using Habitica.Domain.User;
using Habitica.Storage;

namespace Habitica.Application.Diagnostics;

public sealed class LiveTestWorkflow
{
    private readonly IHabiticaSyncClient _habiticaSyncClient;
    private readonly IUserSnapshotStore _userSnapshotStore;
    private readonly ITaskSnapshotStore _taskSnapshotStore;
    private readonly IPartySnapshotStore _partySnapshotStore;
    private readonly DiagnosticsLogWriter _logWriter;
    private readonly TimeProvider _timeProvider;

    public LiveTestWorkflow(
        IHabiticaSyncClient habiticaSyncClient,
        IUserSnapshotStore userSnapshotStore,
        ITaskSnapshotStore taskSnapshotStore,
        IPartySnapshotStore partySnapshotStore,
        DiagnosticsLogWriter logWriter,
        TimeProvider timeProvider)
    {
        _habiticaSyncClient = habiticaSyncClient;
        _userSnapshotStore = userSnapshotStore;
        _taskSnapshotStore = taskSnapshotStore;
        _partySnapshotStore = partySnapshotStore;
        _logWriter = logWriter;
        _timeProvider = timeProvider;
    }

    public async Task<LiveTestSuiteResult> RunSafeLiveTestsAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
    {
        var startedAtUtc = _timeProvider.GetUtcNow();
        var results = new List<LiveTestResult>();
        var requestCount = 0;

        var userSnapshot = await _habiticaSyncClient.GetUserSnapshotAsync(credentials, cancellationToken);
        requestCount++;
        await _userSnapshotStore.SaveAsync(userSnapshot, cancellationToken);

        results.Add(new LiveTestResult(
            Id: "account-snapshot",
            Title: "Account snapshot",
            Status: LiveTestStatus.Passed,
            Risk: LiveTestRisk.Safe,
            RequestCount: 1,
            Message: $"{userSnapshot.DisplayName} level {userSnapshot.Level} account snapshot refreshed."));

        results.Add(new LiveTestResult(
            Id: "inventory-explorer",
            Title: "Inventory and equipment",
            Status: LiveTestStatus.Passed,
            Risk: LiveTestRisk.Safe,
            RequestCount: 0,
            Message: $"{userSnapshot.Inventory.OwnedGearKeys.Length} owned gear keys cached; battle weapon {userSnapshot.Equipment.Battle.Weapon ?? "none"}."));

        if (string.IsNullOrWhiteSpace(userSnapshot.PartyId))
        {
            await _partySnapshotStore.ClearAsync(cancellationToken);
            results.Add(new LiveTestResult(
                Id: "party-overview",
                Title: "Party overview",
                Status: LiveTestStatus.Skipped,
                Risk: LiveTestRisk.Safe,
                RequestCount: 0,
                Message: "Skipped because the account snapshot shows no active party."));
        }
        else
        {
            var partySnapshot = await _habiticaSyncClient.GetPartySnapshotAsync(credentials, cancellationToken);
            requestCount++;
            await _partySnapshotStore.SaveAsync(partySnapshot, cancellationToken);

            results.Add(new LiveTestResult(
                Id: "party-overview",
                Title: "Party overview",
                Status: LiveTestStatus.Passed,
                Risk: LiveTestRisk.Safe,
                RequestCount: 1,
                Message: $"{partySnapshot.Name} cached with {partySnapshot.MemberCount} members."));
        }

        var taskSnapshot = await _habiticaSyncClient.GetTasksAsync(credentials, cancellationToken);
        requestCount++;
        await _taskSnapshotStore.SaveAsync(taskSnapshot, cancellationToken);

        results.Add(new LiveTestResult(
            Id: "task-snapshot",
            Title: "Task snapshot",
            Status: LiveTestStatus.Passed,
            Risk: LiveTestRisk.Safe,
            RequestCount: 1,
            Message: $"{taskSnapshot.Items.Count} tasks refreshed from Habitica."));

        await _logWriter.WriteAsync(
            DiagnosticsFeatureArea.Diagnostics,
            "safe-live-tests",
            DiagnosticsSeverity.Success,
            DiagnosticsMode.LiveRead,
            "Completed the safe diagnostics suite.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["requestCount"] = requestCount.ToString(CultureInfo.InvariantCulture),
                ["resultCount"] = results.Count.ToString(CultureInfo.InvariantCulture)
            },
            cancellationToken);

        return new LiveTestSuiteResult(
            startedAtUtc,
            _timeProvider.GetUtcNow(),
            results);
    }

    public async Task<LiveTestSuiteResult> RunReversibleGearTestAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
    {
        var startedAtUtc = _timeProvider.GetUtcNow();
        var results = new List<LiveTestResult>();
        var requests = 0;

        var initialUserSnapshot = await _habiticaSyncClient.GetUserSnapshotAsync(credentials, cancellationToken);
        requests++;
        await _userSnapshotStore.SaveAsync(initialUserSnapshot, cancellationToken);

        var candidate = FindGearCandidate(initialUserSnapshot);
        if (candidate is null)
        {
            results.Add(new LiveTestResult(
                Id: "reversible-gear-roundtrip",
                Title: "Reversible gear roundtrip",
                Status: LiveTestStatus.Skipped,
                Risk: LiveTestRisk.ReversibleMutation,
                RequestCount: requests,
                Message: "Skipped because no alternate owned battle gear was found for a supported slot."));

            await _logWriter.WriteAsync(
                DiagnosticsFeatureArea.Diagnostics,
                "reversible-gear-roundtrip",
                DiagnosticsSeverity.Warning,
                DiagnosticsMode.ReversibleTest,
                "Skipped reversible gear test because no alternate owned battle gear was available.",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["requestCount"] = requests.ToString(CultureInfo.InvariantCulture)
                },
                cancellationToken);

            return new LiveTestSuiteResult(startedAtUtc, _timeProvider.GetUtcNow(), results);
        }

        UserSnapshot? restoredSnapshot = null;
        string? cleanupFailureMessage = null;
        bool restorationAttempted = false;

        try
        {
            await _habiticaSyncClient.EquipGearAsync(credentials, candidate.AlternateKey, cancellationToken);
            requests++;
            restorationAttempted = true;

            var changedSnapshot = await _habiticaSyncClient.GetUserSnapshotAsync(credentials, cancellationToken);
            requests++;
            await _userSnapshotStore.SaveAsync(changedSnapshot, cancellationToken);

            if (!string.Equals(GetBattleSlotValue(changedSnapshot, candidate.SlotTitle), candidate.AlternateKey, StringComparison.Ordinal))
            {
                results.Add(new LiveTestResult(
                    Id: "reversible-gear-roundtrip",
                    Title: "Reversible gear roundtrip",
                    Status: LiveTestStatus.Failed,
                    Risk: LiveTestRisk.ReversibleMutation,
                    RequestCount: requests,
                    Message: $"Equip verification failed for {candidate.SlotTitle.ToLowerInvariant()} slot after switching to {candidate.AlternateKey}."));
            }
            else
            {
                results.Add(new LiveTestResult(
                    Id: "reversible-gear-roundtrip",
                    Title: "Reversible gear roundtrip",
                    Status: LiveTestStatus.Passed,
                    Risk: LiveTestRisk.ReversibleMutation,
                    RequestCount: requests,
                    Message: $"Temporarily equipped {candidate.AlternateKey} and verified the change before restoring {candidate.OriginalKey}."));
            }
        }
        catch (Exception exception)
        {
            results.Add(new LiveTestResult(
                Id: "reversible-gear-roundtrip",
                Title: "Reversible gear roundtrip",
                Status: LiveTestStatus.Failed,
                Risk: LiveTestRisk.ReversibleMutation,
                RequestCount: requests,
                Message: $"Live gear roundtrip failed before verification: {exception.Message}"));
        }
        finally
        {
            if (restorationAttempted)
            {
                try
                {
                    await _habiticaSyncClient.EquipGearAsync(credentials, candidate.OriginalKey, cancellationToken);
                    requests++;
                    restoredSnapshot = await _habiticaSyncClient.GetUserSnapshotAsync(credentials, cancellationToken);
                    requests++;
                    await _userSnapshotStore.SaveAsync(restoredSnapshot, cancellationToken);
                }
                catch (Exception exception)
                {
                    cleanupFailureMessage = exception.Message;
                }
            }
        }

        var result = results[0];
        if (cleanupFailureMessage is not null)
        {
            results[0] = result with
            {
                Status = LiveTestStatus.Failed,
                RequestCount = requests,
                Message = $"{result.Message} Restore failed: {cleanupFailureMessage}"
            };
        }
        else if (restoredSnapshot is null || !string.Equals(GetBattleSlotValue(restoredSnapshot, candidate.SlotTitle), candidate.OriginalKey, StringComparison.Ordinal))
        {
            results[0] = result with
            {
                Status = LiveTestStatus.Failed,
                RequestCount = requests,
                Message = $"{result.Message} Restore verification did not confirm the original equipped item."
            };
        }
        else
        {
            results[0] = result with
            {
                RequestCount = requests,
                Message = $"{result.Message} Original gear restored successfully."
            };
        }

        await _logWriter.WriteAsync(
            DiagnosticsFeatureArea.Diagnostics,
            "reversible-gear-roundtrip",
            results[0].Status == LiveTestStatus.Passed ? DiagnosticsSeverity.Success : DiagnosticsSeverity.Error,
            DiagnosticsMode.ReversibleTest,
            results[0].Message,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["requestCount"] = requests.ToString(CultureInfo.InvariantCulture),
                ["slotTitle"] = candidate.SlotTitle
            },
            cancellationToken);

        return new LiveTestSuiteResult(startedAtUtc, _timeProvider.GetUtcNow(), results);
    }

    private static GearRoundtripCandidate? FindGearCandidate(UserSnapshot snapshot)
    {
        var supportedSlots = new[]
        {
            ("Head", snapshot.Equipment.Battle.Head),
            ("Armor", snapshot.Equipment.Battle.Armor),
            ("Weapon", snapshot.Equipment.Battle.Weapon),
            ("Shield", snapshot.Equipment.Battle.Shield),
            ("Back", snapshot.Equipment.Battle.Back)
        };

        foreach (var (slotTitle, originalKey) in supportedSlots)
        {
            if (string.IsNullOrWhiteSpace(originalKey))
            {
                continue;
            }

            var alternateKey = snapshot.Inventory.OwnedGearKeys
                .Where(key => string.Equals(ParseSlotTitle(key), slotTitle, StringComparison.Ordinal))
                .Where(key => !string.Equals(key, originalKey, StringComparison.Ordinal))
                .OrderBy(key => key, StringComparer.Ordinal)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(alternateKey))
            {
                return new GearRoundtripCandidate(slotTitle, originalKey, alternateKey);
            }
        }

        return null;
    }

    private static string ParseSlotTitle(string key)
    {
        return key.Split('_', 2, StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant() switch
        {
            "head" => "Head",
            "armor" => "Armor",
            "weapon" => "Weapon",
            "shield" => "Shield",
            "back" => "Back",
            _ => "Other"
        };
    }

    private static string? GetBattleSlotValue(UserSnapshot snapshot, string slotTitle)
    {
        return slotTitle switch
        {
            "Head" => snapshot.Equipment.Battle.Head,
            "Armor" => snapshot.Equipment.Battle.Armor,
            "Weapon" => snapshot.Equipment.Battle.Weapon,
            "Shield" => snapshot.Equipment.Battle.Shield,
            "Back" => snapshot.Equipment.Battle.Back,
            _ => null
        };
    }

    private sealed record GearRoundtripCandidate(
        string SlotTitle,
        string OriginalKey,
        string AlternateKey);
}

public sealed record LiveTestSuiteResult(
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    IReadOnlyList<LiveTestResult> Results)
{
    public int TotalRequests => Results.Sum(result => result.RequestCount);
}

public sealed record LiveTestResult(
    string Id,
    string Title,
    LiveTestStatus Status,
    LiveTestRisk Risk,
    int RequestCount,
    string Message);

public enum LiveTestStatus
{
    Passed,
    Failed,
    Skipped
}

public enum LiveTestRisk
{
    Safe,
    ReversibleMutation
}
