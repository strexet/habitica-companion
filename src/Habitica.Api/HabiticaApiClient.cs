using System.Globalization;
using System.Text.Json;
using Habitica.Domain.Auth;
using Habitica.Domain.Party;
using Habitica.Domain.Tasks;
using Habitica.Domain.User;

namespace Habitica.Api;

public sealed class HabiticaApiClient : IHabiticaSyncClient
{
    private readonly HttpClient _httpClient;
    private readonly HabiticaApiClientOptions _options;

    public HabiticaApiClient(HttpClient httpClient, HabiticaApiClientOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<UserSummary> GetUserAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
    {
        var snapshot = await GetUserSnapshotAsync(credentials, cancellationToken);

        return new UserSummary(
            snapshot.DisplayName,
            snapshot.ClassName,
            snapshot.Level);
    }

    public async Task<UserSnapshot> GetUserSnapshotAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, "user", credentials);
        using var document = await SendForDocumentAsync(request, cancellationToken);
        var data = document.RootElement.GetProperty("data");
        var profile = data.GetProperty("profile");
        var stats = data.GetProperty("stats");
        var items = data.TryGetProperty("items", out var itemsProperty) ? itemsProperty : default;
        var gear = TryGetObject(items, "gear");
        var inventory = MapInventory(gear, items);

        return new UserSnapshot(
            RetrievedAtUtc: DateTimeOffset.UtcNow,
            DisplayName: profile.GetProperty("name").GetString() ?? "Unknown Habitica User",
            ClassName: GetOptionalString(stats, "class"),
            Level: GetOptionalInt32(stats, "lvl"),
            Health: GetOptionalDecimal(stats, "hp"),
            MaxHealth: GetOptionalDecimal(stats, "maxHealth"),
            Mana: GetOptionalDecimal(stats, "mp"),
            MaxMana: GetOptionalDecimal(stats, "maxMP"),
            Experience: GetOptionalDecimal(stats, "exp"),
            ToNextLevel: GetOptionalDecimal(stats, "toNextLevel"),
            Gold: GetOptionalDecimal(stats, "gp"),
            PartyId: TryGetObject(data, "party") is { ValueKind: JsonValueKind.Object } party ? GetOptionalString(party, "_id") : null,
            CurrentPetKey: GetOptionalString(items, "currentPet"),
            CurrentMountKey: GetOptionalString(items, "currentMount"),
            Equipment: new EquipmentSnapshot(
                Battle: MapGearSlots(TryGetObject(gear, "equipped")),
                Costume: MapGearSlots(TryGetObject(gear, "costume"))),
            Inventory: inventory);
    }

    public async Task<TaskCollectionSnapshot> GetTasksAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, "tasks/user", credentials);
        using var document = await SendForDocumentAsync(request, cancellationToken);
        var tasks = document.RootElement
            .GetProperty("data")
            .EnumerateArray()
            .Select(MapTask)
            .ToArray();

        return new TaskCollectionSnapshot(DateTimeOffset.UtcNow, tasks);
    }

    public async Task<PartySnapshot> GetPartySnapshotAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
    {
        var retrievedAtUtc = DateTimeOffset.UtcNow;
        using var request = CreateRequest(HttpMethod.Get, "groups/party", credentials);
        using var document = await SendForDocumentAsync(request, cancellationToken);
        var data = document.RootElement.GetProperty("data");
        var quest = TryGetObject(data, "quest");
        var members = await GetPartyMembersAsync(credentials, retrievedAtUtc, cancellationToken);
        var questSnapshot = MapPartyQuest(quest);
        if (questSnapshot?.BossHealthRemaining is not null)
        {
            var totalPendingDamage = SumPartyPendingQuestDamage(quest, members);
            if (totalPendingDamage is not null)
            {
                questSnapshot = questSnapshot with
                {
                    TotalPendingDamage = totalPendingDamage
                };
            }
        }

        if (questSnapshot is not null && questSnapshot.BossHealthRemaining is null)
        {
            var totalPendingCollectionItems = SumPartyPendingCollectionItems(quest, members);
            if (totalPendingCollectionItems is not null)
            {
                questSnapshot = questSnapshot with
                {
                    TotalPendingCollectionItems = totalPendingCollectionItems
                };
            }
        }

        if (questSnapshot?.BossHealthRemaining is not null && !string.IsNullOrWhiteSpace(questSnapshot.Key))
        {
            var totalBossHealth = await GetQuestBossHealthTotalAsync(credentials, questSnapshot.Key, cancellationToken);
            if (totalBossHealth is not null)
            {
                questSnapshot = questSnapshot with
                {
                    BossHealthTotal = totalBossHealth
                };
            }
        }

        return new PartySnapshot(
            retrievedAtUtc: retrievedAtUtc,
            partyId: GetOptionalString(data, "_id") ?? string.Empty,
            name: GetOptionalString(data, "name") ?? "Unnamed Party",
            summary: GetOptionalString(data, "summary") ?? GetOptionalString(data, "description"),
            memberCount: GetOptionalInt32(data, "memberCount"),
            quest: questSnapshot,
            members: members);
    }

    private async Task<decimal?> GetQuestBossHealthTotalAsync(
        HabiticaCredentials credentials,
        string questKey,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Get, "content?language=en", credentials);
            using var document = await SendForDocumentAsync(request, cancellationToken);
            var data = document.RootElement.GetProperty("data");
            var quest = TryGetObject(TryGetObject(data, "quests"), questKey);
            var boss = TryGetObject(quest, "boss");

            return TryGetDecimal(boss, "hp", out var bossHealth) ? bossHealth : null;
        }
        catch (HabiticaApiException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task EquipGearAsync(HabiticaCredentials credentials, string key, CancellationToken cancellationToken)
    {
        await EquipGearAsync(credentials, EquipmentSetKind.Battle, key, cancellationToken);
    }

    public async Task EquipGearAsync(HabiticaCredentials credentials, EquipmentSetKind kind, string key, CancellationToken cancellationToken)
    {
        var equipType = kind == EquipmentSetKind.Costume ? "costume" : "equipped";
        using var request = CreateRequest(HttpMethod.Post, $"user/equip/{equipType}/{Uri.EscapeDataString(key)}", credentials);
        using var _ = await SendForDocumentAsync(request, cancellationToken);
    }

    public async Task<GearCatalogSnapshot> GetContentCatalogAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, "content?language=en", credentials);
        using var document = await SendForDocumentAsync(request, cancellationToken);
        var flatGear = TryGetObject(TryGetObject(document.RootElement.GetProperty("data"), "gear"), "flat");
        var items = new Dictionary<string, GearCatalogItem>(StringComparer.Ordinal);

        if (flatGear.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in flatGear.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var key = GetOptionalString(property.Value, "key") ?? property.Name;
                items[key] = new GearCatalogItem(
                    Key: key,
                    Text: GetOptionalString(property.Value, "text") ?? key,
                    SlotTitle: ParseSlotTitle(GetOptionalString(property.Value, "type") ?? key),
                    ClassName: GetOptionalString(property.Value, "klass") ?? GetOptionalString(property.Value, "class"),
                    Notes: GetOptionalString(property.Value, "notes"),
                    Stats: new GearStatBlock(
                        GetOptionalDecimal(property.Value, "str"),
                        GetOptionalDecimal(property.Value, "int"),
                        GetOptionalDecimal(property.Value, "con"),
                        GetOptionalDecimal(property.Value, "per")),
                    TwoHanded: GetOptionalBoolean(property.Value, "twoHanded"));
            }
        }

        return new GearCatalogSnapshot(DateTimeOffset.UtcNow, items);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativePath, HabiticaCredentials credentials)
    {
        var request = new HttpRequestMessage(method, relativePath);
        var clientHeaderValue = string.IsNullOrWhiteSpace(_options.ClientHeaderValue)
            ? $"{credentials.UserId}-{_options.ApplicationName}"
            : _options.ClientHeaderValue;
        request.Headers.Add("x-api-user", credentials.UserId);
        request.Headers.Add("x-api-key", credentials.ApiToken);
        request.Headers.Add("x-client", clientHeaderValue);
        request.Headers.Accept.ParseAdd("application/json");
        return request;
    }

    private async Task<JsonDocument> SendForDocumentAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HabiticaApiException(response.StatusCode, ExtractErrorMessage(content, response.ReasonPhrase));
        }

        return JsonDocument.Parse(content, new JsonDocumentOptions
        {
            AllowTrailingCommas = true
        });
    }

    private async Task<IReadOnlyList<PartyMemberSnapshot>> GetPartyMembersAsync(
        HabiticaCredentials credentials,
        DateTimeOffset retrievedAtUtc,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, "groups/party/members?includeAllPublicFields=true", credentials);
        using var document = await SendForDocumentAsync(request, cancellationToken);
        return document.RootElement
            .GetProperty("data")
            .EnumerateArray()
            .Select(member => MapPartyMember(member, retrievedAtUtc))
            .Where(static member => !string.IsNullOrWhiteSpace(member.MemberId))
            .ToArray();
    }

    private static TaskSnapshot MapTask(JsonElement task)
    {
        return new TaskSnapshot(
            task.GetProperty("id").GetString() ?? string.Empty,
            task.GetProperty("text").GetString() ?? string.Empty,
            ParseTaskType(task.GetProperty("type").GetString()),
            task.TryGetProperty("completed", out var completedProperty) && completedProperty.GetBoolean(),
            task.TryGetProperty("priority", out var priorityProperty)
                ? priorityProperty.GetDecimal()
                : 1m,
            task.TryGetProperty("notes", out var notesProperty) ? notesProperty.GetString() : null,
            ParseNullableDate(task));
    }

    private static InventorySnapshot MapInventory(JsonElement gear, JsonElement items)
    {
        var ownedGearKeys = TryGetObject(gear, "owned") is { ValueKind: JsonValueKind.Object } ownedGear
            ? ownedGear.EnumerateObject()
                .Where(property => property.Value.ValueKind == JsonValueKind.True)
                .Select(property => property.Name)
                .OrderBy(static key => key, StringComparer.Ordinal)
                .ToArray()
            : Array.Empty<string>();

        return new InventorySnapshot(
            EggCount: CountPositiveEntries(TryGetObject(items, "eggs")),
            FoodCount: CountPositiveEntries(TryGetObject(items, "food")),
            HatchingPotionCount: CountPositiveEntries(TryGetObject(items, "hatchingPotions")),
            QuestCount: CountPositiveEntries(TryGetObject(items, "quests")),
            OwnedPetCount: CountPositiveEntries(TryGetObject(items, "pets")),
            OwnedMountCount: CountTrueEntries(TryGetObject(items, "mounts")),
            OwnedGearKeys: ownedGearKeys);
    }

    private static PartyMemberSnapshot MapPartyMember(JsonElement member, DateTimeOffset retrievedAtUtc)
    {
        var profile = TryGetObject(member, "profile");
        var preferences = TryGetObject(member, "preferences");
        var id = GetOptionalString(member, "_id") ?? GetOptionalString(member, "id") ?? string.Empty;
        var displayName = GetOptionalString(profile, "name")
            ?? GetOptionalString(member, "displayName")
            ?? GetOptionalString(member, "username")
            ?? "Unknown party member";
        var authTimestamps = TryGetObject(TryGetObject(member, "auth"), "timestamps");
        var partyQuestProgress = TryGetObject(TryGetObject(TryGetObject(member, "party"), "quest"), "progress");
        var input = new PartyMemberCronInput(
            id,
            displayName,
            ParseDateTimeOffset(GetOptionalString(member, "lastCron"))
                ?? ParseDateTimeOffset(GetOptionalString(authTimestamps, "loggedin")),
            GetOptionalNullableInt32(preferences, "dayStart"),
            GetOptionalNullableInt32(preferences, "timezoneOffset") ?? GetOptionalNullableInt32(preferences, "timezoneOffsetAtLastCron"));

        var snapshot = PartyCronCalculator.ClassifyMember(input, retrievedAtUtc, DateTimeOffset.UtcNow);
        return snapshot with
        {
            PendingQuestDamage = TryGetDecimal(partyQuestProgress, "up", out var pendingQuestDamage)
                ? pendingQuestDamage
                : null,
            PendingQuestItems = GetPendingQuestItems(partyQuestProgress)
        };
    }

    private static PartyQuestSnapshot? MapPartyQuest(JsonElement quest)
    {
        if (quest.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var progress = TryGetObject(quest, "progress");
        var questProgress = MapQuestProgress(progress);
        var pendingDamage = TryGetDecimal(progress, "up", out var up) ? up : (decimal?)null;
        var bossHealthRemaining = TryGetDecimal(progress, "hp", out var hp) ? hp : (decimal?)null;
        var pendingPartyDamage = TryGetDecimal(progress, "down", out var down) ? down : (decimal?)null;

        return new PartyQuestSnapshot(
            Key: GetOptionalString(quest, "key"),
            IsActive: GetOptionalBoolean(quest, "active"),
            ProgressUp: questProgress.Value,
            ProgressDown: pendingPartyDamage ?? 0m,
            ParticipantCount: CountTrueEntries(TryGetObject(quest, "members")),
            ProgressLabel: questProgress.Label,
            PendingDamage: pendingDamage,
            BossHealthRemaining: bossHealthRemaining,
            PendingPartyDamage: pendingPartyDamage);
    }

    private static decimal? GetPendingQuestItems(JsonElement progress)
    {
        if (TryGetDecimal(progress, "collectedItems", out var collectedItems))
        {
            return collectedItems;
        }

        return SumNumericObject(TryGetObject(progress, "collect"));
    }

    private static decimal? SumPartyPendingQuestDamage(JsonElement quest, IReadOnlyList<PartyMemberSnapshot> members)
    {
        if (quest.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var questMembers = TryGetObject(quest, "members");
        var isActive = GetOptionalBoolean(quest, "active");
        var includedMembers = members
            .Where(member => member.PendingQuestDamage is not null)
            .Where(member => ShouldIncludePendingQuestProgress(member, questMembers, isActive))
            .ToArray();
        if (includedMembers.Length == 0)
        {
            return null;
        }

        return includedMembers.Sum(static member => member.PendingQuestDamage!.Value);
    }

    private static decimal? SumPartyPendingCollectionItems(JsonElement quest, IReadOnlyList<PartyMemberSnapshot> members)
    {
        if (quest.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var questMembers = TryGetObject(quest, "members");
        var isActive = GetOptionalBoolean(quest, "active");
        var includedMembers = members
            .Where(member => member.PendingQuestItems is not null)
            .Where(member => ShouldIncludePendingQuestProgress(member, questMembers, isActive))
            .ToArray();
        if (includedMembers.Length == 0)
        {
            return null;
        }

        return includedMembers.Sum(static member => member.PendingQuestItems!.Value);
    }

    private static bool ShouldIncludePendingQuestProgress(
        PartyMemberSnapshot member,
        JsonElement questMembers,
        bool isActive)
    {
        if (!isActive || questMembers.ValueKind != JsonValueKind.Object)
        {
            return true;
        }

        return questMembers.TryGetProperty(member.MemberId, out var participation)
            && participation.ValueKind == JsonValueKind.True;
    }

    private static GearSlotsSnapshot MapGearSlots(JsonElement slots)
    {
        return new GearSlotsSnapshot(
            Head: GetOptionalString(slots, "head"),
            Armor: GetOptionalString(slots, "armor"),
            Weapon: GetOptionalString(slots, "weapon"),
            Shield: GetOptionalString(slots, "shield"),
            Back: GetOptionalString(slots, "back"));
    }

    private static int CountPositiveEntries(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        var count = 0;

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.True)
            {
                count++;
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Number && property.Value.GetDecimal() > 0m)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountTrueEntries(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        return element.EnumerateObject().Count(property => property.Value.ValueKind == JsonValueKind.True);
    }

    private static (decimal Value, string Label) MapQuestProgress(JsonElement progress)
    {
        if (TryGetDecimal(progress, "up", out var value))
        {
            return (value, "Pending damage");
        }

        if (TryGetDecimal(progress, "hp", out value))
        {
            return (value, "Boss HP remaining");
        }

        if (TryGetDecimal(progress, "collected", out value))
        {
            return (value, "Items collected");
        }

        if (TryGetDecimal(progress, "collectedItems", out value))
        {
            return (value, "Items collected");
        }

        return SumNumericObject(TryGetObject(progress, "collect")) is { } collectionTotal
            ? (collectionTotal, "Items collected")
            : (0m, "Progress");
    }

    private static decimal? SumNumericObject(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var hasValue = false;
        var total = 0m;
        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Number)
            {
                continue;
            }

            hasValue = true;
            total += property.Value.GetDecimal();
        }

        return hasValue ? total : null;
    }

    private static string ExtractErrorMessage(string responseBody, string? fallbackReasonPhrase)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody, new JsonDocumentOptions
            {
                AllowTrailingCommas = true
            });

            if (document.RootElement.TryGetProperty("message", out var messageProperty))
            {
                return messageProperty.GetString() ?? "Habitica API request failed.";
            }
        }
        catch (JsonException)
        {
        }

        return string.IsNullOrWhiteSpace(fallbackReasonPhrase)
            ? "Habitica API request failed."
            : fallbackReasonPhrase;
    }

    private static DateTimeOffset? ParseNullableDate(JsonElement task)
    {
        if (!task.TryGetProperty("date", out var dateProperty) || dateProperty.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (DateTimeOffset.TryParse(
                dateProperty.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static int GetOptionalInt32(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            ? property.GetInt32()
            : 0;
    }

    private static int? GetOptionalNullableInt32(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            ? property.GetInt32()
            : null;
    }

    private static bool GetOptionalBoolean(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind is JsonValueKind.True or JsonValueKind.False
            && property.GetBoolean();
    }

    private static decimal GetOptionalDecimal(JsonElement element, string propertyName)
    {
        return TryGetDecimal(element, propertyName, out var value) ? value : 0m;
    }

    private static bool TryGetDecimal(JsonElement element, string propertyName, out decimal value)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number)
        {
            value = property.GetDecimal();
            return true;
        }

        value = 0m;
        return false;
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static DateTimeOffset? ParseDateTimeOffset(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
    }

    private static JsonElement TryGetObject(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property)
            ? property
            : default;
    }

    private static string ParseSlotTitle(string value)
    {
        return value.Split('_', 2, StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant() switch
        {
            "head" => "Head",
            "armor" => "Armor",
            "weapon" => "Weapon",
            "shield" => "Shield",
            "back" => "Back",
            _ => "Other"
        };
    }

    private static TaskType ParseTaskType(string? taskType)
    {
        return taskType?.ToLowerInvariant() switch
        {
            "habit" => TaskType.Habit,
            "daily" => TaskType.Daily,
            "todo" => TaskType.Todo,
            "reward" => TaskType.Reward,
            _ => TaskType.Todo
        };
    }
}
