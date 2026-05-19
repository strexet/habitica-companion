using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Habitica.Storage;

namespace Habitica.Application.Sync;

public sealed class LocalUserDataPortabilityService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IKeyValueStorage _keyValueStorage;
    private readonly TimeProvider _timeProvider;

    public LocalUserDataPortabilityService(IKeyValueStorage keyValueStorage, TimeProvider timeProvider)
    {
        _keyValueStorage = keyValueStorage;
        _timeProvider = timeProvider;
    }

    public async Task<LocalUserDataBundle> ExportAsync(string? userId, CancellationToken cancellationToken)
    {
        var records = new List<LocalUserDataRecord>();
        foreach (var key in StorageKeys.PortableDataKeys)
        {
            var json = await _keyValueStorage.GetRawJsonAsync(key, cancellationToken);
            if (!string.IsNullOrWhiteSpace(json))
            {
                records.Add(new LocalUserDataRecord(key, json));
            }
        }

        return new LocalUserDataBundle(
            SchemaVersion: 1,
            ExportedAtUtc: _timeProvider.GetUtcNow(),
            UserId: string.IsNullOrWhiteSpace(userId) ? null : userId,
            Records: records);
    }

    public async Task<LocalUserDataImportPreview> PreviewImportAsync(
        LocalUserDataBundle bundle,
        CancellationToken cancellationToken)
    {
        ValidateBundle(bundle);
        var localKeys = await GetLocalKeysAsync(cancellationToken);
        var incomingKeys = bundle.Records.Select(static record => record.Key).Distinct(StringComparer.Ordinal).ToArray();
        var conflicts = incomingKeys.Where(localKeys.Contains).Order(StringComparer.Ordinal).ToArray();

        return new LocalUserDataImportPreview(
            HasLocalData: localKeys.Count > 0,
            IncomingRecordCount: incomingKeys.Length,
            LocalRecordCount: localKeys.Count,
            ConflictingKeys: conflicts);
    }

    public async Task<LocalUserDataImportResult> ImportAsync(
        LocalUserDataBundle bundle,
        LocalDataImportMode mode,
        CancellationToken cancellationToken)
    {
        ValidateBundle(bundle);
        var records = bundle.Records
            .GroupBy(static record => record.Key, StringComparer.Ordinal)
            .Select(static group => group.Last())
            .ToDictionary(static record => record.Key, static record => record.JsonText, StringComparer.Ordinal);

        if (mode == LocalDataImportMode.Override)
        {
            foreach (var key in StorageKeys.PortableDataKeys.Where(key => !records.ContainsKey(key)))
            {
                await _keyValueStorage.RemoveAsync(key, cancellationToken);
            }
        }

        var importedCount = 0;
        foreach (var record in records)
        {
            var localJson = await _keyValueStorage.GetRawJsonAsync(record.Key, cancellationToken);
            var nextJson = mode == LocalDataImportMode.Merge && !string.IsNullOrWhiteSpace(localJson)
                ? MergeJson(record.Key, localJson!, record.Value)
                : record.Value;

            await _keyValueStorage.SetRawJsonAsync(record.Key, nextJson, cancellationToken);
            importedCount++;
        }

        return new LocalUserDataImportResult(
            true,
            mode == LocalDataImportMode.Override
                ? $"Imported {importedCount} data records and replaced existing app data."
                : $"Imported {importedCount} data records and merged with existing app data.",
            importedCount);
    }

    public async Task<LocalUserDataRecord?> ExportSectionAsync(string storageKey, CancellationToken cancellationToken)
    {
        var json = await _keyValueStorage.GetRawJsonAsync(storageKey, cancellationToken);
        return string.IsNullOrWhiteSpace(json) ? null : new LocalUserDataRecord(storageKey, json);
    }

    public async Task ImportSectionAsync(
        LocalUserDataRecord record,
        LocalDataImportMode mode,
        CancellationToken cancellationToken)
    {
        if (!StorageKeys.PortableDataKeys.Contains(record.Key, StringComparer.Ordinal))
        {
            return;
        }

        var localJson = await _keyValueStorage.GetRawJsonAsync(record.Key, cancellationToken);
        var nextJson = mode == LocalDataImportMode.Merge && !string.IsNullOrWhiteSpace(localJson)
            ? MergeJson(record.Key, localJson!, record.JsonText)
            : record.JsonText;

        await _keyValueStorage.SetRawJsonAsync(record.Key, nextJson, cancellationToken);
    }

    public string Serialize(LocalUserDataBundle bundle)
    {
        return JsonSerializer.Serialize(bundle, JsonOptions);
    }

    public LocalUserDataBundle Deserialize(string jsonText)
    {
        try
        {
            return JsonSerializer.Deserialize<LocalUserDataBundle>(jsonText, JsonOptions)
                ?? throw new InvalidOperationException("Import file is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"Import file is not valid Habitica Tool data: {exception.Message}", exception);
        }
    }

    private async Task<HashSet<string>> GetLocalKeysAsync(CancellationToken cancellationToken)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in StorageKeys.PortableDataKeys)
        {
            if (!string.IsNullOrWhiteSpace(await _keyValueStorage.GetRawJsonAsync(key, cancellationToken)))
            {
                keys.Add(key);
            }
        }

        return keys;
    }

    private static void ValidateBundle(LocalUserDataBundle bundle)
    {
        if (bundle.SchemaVersion != 1)
        {
            throw new InvalidOperationException($"Unsupported import schema version {bundle.SchemaVersion}.");
        }

        var invalidKey = bundle.Records.FirstOrDefault(record => !StorageKeys.PortableDataKeys.Contains(record.Key, StringComparer.Ordinal));
        if (invalidKey is not null)
        {
            throw new InvalidOperationException($"Import file contains unsupported data key '{invalidKey.Key}'.");
        }

        foreach (var record in bundle.Records)
        {
            if (string.IsNullOrWhiteSpace(record.JsonText))
            {
                throw new InvalidOperationException($"Import file contains an empty payload for '{record.Key}'.");
            }

            JsonNode.Parse(record.JsonText);
        }
    }

    private static string MergeJson(string key, string localJson, string incomingJson)
    {
        return key switch
        {
            StorageKeys.EquipmentPresets => MergeArrayByProperty(localJson, incomingJson, "id"),
            StorageKeys.DiagnosticsLogEntries => MergeArrayByProperty(localJson, incomingJson, "id"),
            StorageKeys.PartyCronHistory => MergePartyCronHistory(localJson, incomingJson),
            StorageKeys.LatestTaskSnapshot => PickNewerSnapshot(localJson, incomingJson, "retrievedAtUtc"),
            StorageKeys.LatestUserSnapshot => PickNewerSnapshot(localJson, incomingJson, "retrievedAtUtc"),
            StorageKeys.LatestPartySnapshot => PickNewerSnapshot(localJson, incomingJson, "retrievedAtUtc"),
            StorageKeys.LatestGearCatalog => PickNewerSnapshot(localJson, incomingJson, "retrievedAtUtc"),
            _ => localJson
        };
    }

    private static string MergeArrayByProperty(string localJson, string incomingJson, string propertyName)
    {
        var merged = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var item in ParseArray(localJson).Concat(ParseArray(incomingJson)))
        {
            var key = item?[propertyName]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(key))
            {
                merged[key] = item;
            }
        }

        return SerializeNode(new JsonArray(merged.Values.Select(static value => value?.DeepClone()).ToArray()));
    }

    private static string MergePartyCronHistory(string localJson, string incomingJson)
    {
        var local = JsonNode.Parse(localJson)?.AsObject() ?? new JsonObject();
        var incoming = JsonNode.Parse(incomingJson)?.AsObject() ?? new JsonObject();
        var events = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);

        foreach (var item in ParseObjectArray(local, "events").Concat(ParseObjectArray(incoming, "events")))
        {
            var key = string.Join(
                '|',
                item?["partyId"]?.GetValue<string>() ?? string.Empty,
                item?["memberId"]?.GetValue<string>() ?? string.Empty,
                item?["lastCronUtc"]?.GetValue<string>() ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(key))
            {
                events[key] = item;
            }
        }

        return SerializeNode(new JsonObject
        {
            ["events"] = new JsonArray(events.Values.Select(static value => value?.DeepClone()).ToArray())
        });
    }

    private static string PickNewerSnapshot(string localJson, string incomingJson, string timestampProperty)
    {
        var localTimestamp = ReadTimestamp(localJson, timestampProperty);
        var incomingTimestamp = ReadTimestamp(incomingJson, timestampProperty);

        return incomingTimestamp >= localTimestamp ? incomingJson : localJson;
    }

    private static DateTimeOffset ReadTimestamp(string jsonText, string propertyName)
    {
        var node = JsonNode.Parse(jsonText);
        var value = node?[propertyName]?.GetValue<string>();
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp)
            ? timestamp.ToUniversalTime()
            : DateTimeOffset.MinValue;
    }

    private static IEnumerable<JsonNode?> ParseArray(string jsonText)
    {
        return JsonNode.Parse(jsonText)?.AsArray().Select(static node => node?.DeepClone())
            ?? Array.Empty<JsonNode?>();
    }

    private static IEnumerable<JsonNode?> ParseObjectArray(JsonObject obj, string propertyName)
    {
        return obj[propertyName]?.AsArray().Select(static node => node?.DeepClone())
            ?? Array.Empty<JsonNode?>();
    }

    private static string SerializeNode(JsonNode node)
    {
        return node.ToJsonString(JsonOptions);
    }
}

public enum LocalDataImportMode
{
    Override,
    Merge
}

public sealed record LocalUserDataBundle(
    int SchemaVersion,
    DateTimeOffset ExportedAtUtc,
    string? UserId,
    IReadOnlyList<LocalUserDataRecord> Records);

public sealed record LocalUserDataRecord(
    string Key,
    string JsonText);

public sealed record LocalUserDataImportPreview(
    bool HasLocalData,
    int IncomingRecordCount,
    int LocalRecordCount,
    IReadOnlyList<string> ConflictingKeys);

public sealed record LocalUserDataImportResult(
    bool Succeeded,
    string Message,
    int ImportedRecordCount);
