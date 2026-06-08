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
            if (IsEmptyTaskOrderPreferences(record.Key, record.Value))
            {
                await _keyValueStorage.RemoveAsync(record.Key, cancellationToken);
                importedCount++;
                continue;
            }

            var localJson = await _keyValueStorage.GetRawJsonAsync(record.Key, cancellationToken);
            var nextJson = mode == LocalDataImportMode.Merge && !string.IsNullOrWhiteSpace(localJson)
                ? MergeJson(record.Key, localJson!, record.Value)
                : record.Value;

            if (IsEmptyTaskOrderPreferences(record.Key, nextJson))
            {
                await _keyValueStorage.RemoveAsync(record.Key, cancellationToken);
                importedCount++;
                continue;
            }

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

        if (IsEmptyTaskOrderPreferences(record.Key, record.JsonText))
        {
            await _keyValueStorage.RemoveAsync(record.Key, cancellationToken);
            return;
        }

        var localJson = await _keyValueStorage.GetRawJsonAsync(record.Key, cancellationToken);
        var nextJson = mode == LocalDataImportMode.Merge && !string.IsNullOrWhiteSpace(localJson)
            ? MergeJson(record.Key, localJson!, record.JsonText)
            : record.JsonText;

        if (IsEmptyTaskOrderPreferences(record.Key, nextJson))
        {
            await _keyValueStorage.RemoveAsync(record.Key, cancellationToken);
            return;
        }

        await _keyValueStorage.SetRawJsonAsync(record.Key, nextJson, cancellationToken);
    }

    public async Task ClearSectionAsync(string storageKey, CancellationToken cancellationToken)
    {
        await _keyValueStorage.RemoveAsync(storageKey, cancellationToken);
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
            StorageKeys.TaskOrderPreferences => MergeTaskOrderPreferences(localJson, incomingJson),
            StorageKeys.ColorSchemePreferences => MergeColorSchemes(localJson, incomingJson),
            StorageKeys.LatestTaskSnapshot => PickNewerSnapshot(localJson, incomingJson, "retrievedAtUtc"),
            StorageKeys.LatestUserSnapshot => PickNewerSnapshot(localJson, incomingJson, "retrievedAtUtc"),
            StorageKeys.LatestPartySnapshot => PickNewerSnapshot(localJson, incomingJson, "retrievedAtUtc"),
            StorageKeys.LatestGearCatalog => PickNewerSnapshot(localJson, incomingJson, "retrievedAtUtc"),
            _ => localJson
        };
    }

    // Sync logic mirrors the data shape: a built-in active scheme is just an id (selectedSchemeId
    // is small) while custom schemes ship their full token bundles. Custom schemes union by id,
    // newer updatedAtUtc wins. The selected scheme follows the device whose selectedAtUtc is newer,
    // falling back to local if neither side stamped one. Matches the snapshot/array merge style
    // used by other sections so all sync uses the same shape: LWW per item with id-based union.
    private static string MergeColorSchemes(string localJson, string incomingJson)
    {
        var local = JsonNode.Parse(localJson)?.AsObject() ?? new JsonObject();
        var incoming = JsonNode.Parse(incomingJson)?.AsObject() ?? new JsonObject();

        var merged = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var item in ParseObjectArray(local, "customSchemes").Concat(ParseObjectArray(incoming, "customSchemes")))
        {
            var id = TryGetString(item, "id");
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (!merged.TryGetValue(id, out var existing))
            {
                merged[id] = item;
                continue;
            }

            var existingTimestamp = ReadOptionalTimestamp(existing, "updatedAtUtc");
            var candidateTimestamp = ReadOptionalTimestamp(item, "updatedAtUtc");
            // Strict `>`: ties keep whichever side was inserted first (the local pass above). This
            // is stable and avoids flipping on missing-timestamp legacy data, matching how
            // MergeArrayByProperty deterministically prefers later insertions.
            if (candidateTimestamp > existingTimestamp)
            {
                merged[id] = item;
            }
        }

        var localSelectedTimestamp = ReadOptionalTimestamp(local, "selectedAtUtc");
        var incomingSelectedTimestamp = ReadOptionalTimestamp(incoming, "selectedAtUtc");
        // Tie or neither stamped → keep local selection (consistent with `_ => localJson` default).
        var preferIncomingSelection = incomingSelectedTimestamp > localSelectedTimestamp;
        var selectedSchemeId = TryGetString(preferIncomingSelection ? incoming : local, "selectedSchemeId")
            ?? TryGetString(local, "selectedSchemeId");
        var selectedAtNode = (preferIncomingSelection ? incoming : local)["selectedAtUtc"]?.DeepClone();

        var result = new JsonObject
        {
            ["selectedSchemeId"] = selectedSchemeId,
            ["customSchemes"] = new JsonArray(merged.Values.Select(static value => value?.DeepClone()).ToArray()),
            ["selectedAtUtc"] = selectedAtNode,
            ["schemaVersion"] = Math.Max(ReadOptionalInt(local, "schemaVersion"), ReadOptionalInt(incoming, "schemaVersion"))
        };
        return SerializeNode(result);
    }

    private static string? TryGetString(JsonNode? node, string propertyName)
    {
        var property = node?[propertyName];
        if (property is null)
        {
            return null;
        }

        try
        {
            return property.GetValue<string>();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static DateTimeOffset ReadOptionalTimestamp(JsonNode? node, string propertyName)
    {
        var raw = TryGetString(node, propertyName);
        return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp)
            ? timestamp.ToUniversalTime()
            : DateTimeOffset.MinValue;
    }

    private static int ReadOptionalInt(JsonNode? node, string propertyName)
    {
        try
        {
            return node?[propertyName]?.GetValue<int>() ?? 0;
        }
        catch (InvalidOperationException)
        {
            return 0;
        }
        catch (FormatException)
        {
            return 0;
        }
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

    private static string MergeTaskOrderPreferences(string localJson, string incomingJson)
    {
        var local = JsonNode.Parse(localJson)?["ordersByType"]?.AsObject() ?? new JsonObject();
        var incoming = JsonNode.Parse(incomingJson)?["ordersByType"]?.AsObject() ?? new JsonObject();
        var merged = new JsonObject();

        foreach (var key in incoming.Select(static pair => pair.Key).Concat(local.Select(static pair => pair.Key)).Distinct(StringComparer.Ordinal))
        {
            var localIds = ReadStringArray(local[key]);
            var incomingIds = ReadStringArray(incoming[key]);
            merged[key] = new JsonArray(MergeTaskOrderIds(localIds, incomingIds).Select<string, JsonNode?>(static id => JsonValue.Create(id)).ToArray());
        }

        return SerializeNode(new JsonObject
        {
            ["ordersByType"] = merged
        });
    }

    private static bool IsEmptyTaskOrderPreferences(string storageKey, string jsonText)
    {
        if (!string.Equals(storageKey, StorageKeys.TaskOrderPreferences, StringComparison.Ordinal))
        {
            return false;
        }

        var orders = JsonNode.Parse(jsonText)?["ordersByType"]?.AsObject();
        return orders is null || orders.Count == 0 || orders.All(static pair => ReadStringArray(pair.Value).Count == 0);
    }

    private static IReadOnlyList<string> MergeTaskOrderIds(IReadOnlyList<string> localIds, IReadOnlyList<string> incomingIds)
    {
        var localSet = localIds.ToHashSet(StringComparer.Ordinal);
        var incomingSet = incomingIds.ToHashSet(StringComparer.Ordinal);
        var result = new List<string>();

        result.AddRange(incomingIds.Where(localSet.Contains));
        result.AddRange(localIds.Where(id => !incomingSet.Contains(id)));
        result.AddRange(incomingIds.Where(id => !localSet.Contains(id)));

        return result.Distinct(StringComparer.Ordinal).ToArray();
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

    private static IReadOnlyList<string> ReadStringArray(JsonNode? node)
    {
        if (node is not JsonArray array)
        {
            return Array.Empty<string>();
        }

        var values = new List<string>();
        foreach (var value in array)
        {
            var id = value?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(id))
            {
                values.Add(id);
            }
        }

        return values.Distinct(StringComparer.Ordinal).ToArray();
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
