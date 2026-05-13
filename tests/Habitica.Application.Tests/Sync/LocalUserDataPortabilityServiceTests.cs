using System.Text.Json;
using Habitica.Application.Sync;
using Habitica.Domain.Auth;
using Habitica.Domain.Party;
using Habitica.Domain.User;
using Habitica.Storage;

namespace Habitica.Application.Tests.Sync;

public sealed class LocalUserDataPortabilityServiceTests
{
    [Fact]
    public async Task ExportAsync_excludes_persistent_credentials_and_includes_portable_records()
    {
        var storage = new InMemoryKeyValueStorage();
        var service = new LocalUserDataPortabilityService(storage, TimeProvider.System);
        await storage.SetAsync(StorageKeys.PersistentCredentials, new HabiticaCredentials("user-id", "api-token"), CancellationToken.None);
        await storage.SetAsync(
            StorageKeys.EquipmentPresets,
            new[]
            {
                new EquipmentPreset("preset-1", "user-id", EquipmentSetKind.Battle, "Casting", DateTimeOffset.Parse("2026-05-13T02:00:00Z"), new GearSlotsSnapshot(null, null, "weapon_wizard_5", null, null))
            },
            CancellationToken.None);

        var bundle = await service.ExportAsync("user-id", CancellationToken.None);

        Assert.Equal("user-id", bundle.UserId);
        Assert.Contains(bundle.Records, record => record.Key == StorageKeys.EquipmentPresets);
        Assert.DoesNotContain(bundle.Records, record => record.Key == StorageKeys.PersistentCredentials);
        Assert.DoesNotContain(service.Serialize(bundle), "api-token", StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreviewImportAsync_reports_conflicting_local_data()
    {
        var storage = new InMemoryKeyValueStorage();
        var service = new LocalUserDataPortabilityService(storage, TimeProvider.System);
        await storage.SetRawJsonAsync(StorageKeys.LatestPartySnapshot, """{"retrievedAtUtc":"2026-05-13T01:00:00Z"}""", CancellationToken.None);
        var bundle = new LocalUserDataBundle(
            1,
            DateTimeOffset.Parse("2026-05-13T02:00:00Z"),
            "user-id",
            new[]
            {
                new LocalUserDataRecord(StorageKeys.LatestPartySnapshot, """{"retrievedAtUtc":"2026-05-13T02:00:00Z"}""")
            });

        var preview = await service.PreviewImportAsync(bundle, CancellationToken.None);

        Assert.True(preview.HasLocalData);
        Assert.Equal(1, preview.IncomingRecordCount);
        Assert.Equal(new[] { StorageKeys.LatestPartySnapshot }, preview.ConflictingKeys);
    }

    [Fact]
    public async Task ImportAsync_merges_equipment_presets_and_party_cron_history()
    {
        var storage = new InMemoryKeyValueStorage();
        var service = new LocalUserDataPortabilityService(storage, TimeProvider.System);
        await storage.SetAsync(
            StorageKeys.EquipmentPresets,
            new[]
            {
                new EquipmentPreset("local", "user-id", EquipmentSetKind.Battle, "Local", DateTimeOffset.Parse("2026-05-13T01:00:00Z"), new GearSlotsSnapshot("head_local", null, null, null, null))
            },
            CancellationToken.None);
        await storage.SetAsync(
            StorageKeys.PartyCronHistory,
            new PartyCronHistorySnapshot(new[]
            {
                CreateCronEvent("member-1", "2026-05-12T06:00:00Z")
            }),
            CancellationToken.None);
        var incomingPresets = JsonSerializer.Serialize(
            new[]
            {
                new EquipmentPreset("remote", "user-id", EquipmentSetKind.Battle, "Remote", DateTimeOffset.Parse("2026-05-13T02:00:00Z"), new GearSlotsSnapshot("head_remote", null, null, null, null))
            },
            InMemoryKeyValueStorage.JsonOptions);
        var incomingHistory = JsonSerializer.Serialize(
            new PartyCronHistorySnapshot(new[]
            {
                CreateCronEvent("member-2", "2026-05-13T06:00:00Z")
            }),
            InMemoryKeyValueStorage.JsonOptions);
        var bundle = new LocalUserDataBundle(
            1,
            DateTimeOffset.Parse("2026-05-13T03:00:00Z"),
            "user-id",
            new[]
            {
                new LocalUserDataRecord(StorageKeys.EquipmentPresets, incomingPresets),
                new LocalUserDataRecord(StorageKeys.PartyCronHistory, incomingHistory)
            });

        await service.ImportAsync(bundle, LocalDataImportMode.Merge, CancellationToken.None);

        var mergedPresets = await storage.GetAsync<EquipmentPreset[]>(StorageKeys.EquipmentPresets, CancellationToken.None);
        var mergedHistory = await storage.GetAsync<PartyCronHistorySnapshot>(StorageKeys.PartyCronHistory, CancellationToken.None);

        Assert.NotNull(mergedPresets);
        Assert.Contains(mergedPresets!, preset => preset.Id == "local");
        Assert.Contains(mergedPresets!, preset => preset.Id == "remote");
        Assert.NotNull(mergedHistory);
        Assert.Contains(mergedHistory!.Events, entry => entry.MemberId == "member-1");
        Assert.Contains(mergedHistory.Events, entry => entry.MemberId == "member-2");
    }

    private static PartyCronHistoryEvent CreateCronEvent(string memberId, string lastCronUtc)
    {
        return new PartyCronHistoryEvent(
            "party-id",
            memberId,
            "Member",
            DateTimeOffset.Parse(lastCronUtc),
            "2026-05-13",
            DateTimeOffset.Parse(lastCronUtc).AddMinutes(5),
            PartyCronEventConfidence.High);
    }

    private sealed class InMemoryKeyValueStorage : IKeyValueStorage
    {
        public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public Task<TValue?> GetAsync<TValue>(string key, CancellationToken cancellationToken)
        {
            return Task.FromResult(_values.TryGetValue(key, out var value)
                ? JsonSerializer.Deserialize<TValue>(value, JsonOptions)
                : default);
        }

        public Task<string?> GetRawJsonAsync(string key, CancellationToken cancellationToken)
        {
            return Task.FromResult(_values.GetValueOrDefault(key));
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken)
        {
            _values.Remove(key);
            return Task.CompletedTask;
        }

        public Task SetAsync<TValue>(string key, TValue value, CancellationToken cancellationToken)
        {
            _values[key] = JsonSerializer.Serialize(value, JsonOptions);
            return Task.CompletedTask;
        }

        public Task SetRawJsonAsync(string key, string jsonText, CancellationToken cancellationToken)
        {
            _values[key] = jsonText;
            return Task.CompletedTask;
        }
    }
}
