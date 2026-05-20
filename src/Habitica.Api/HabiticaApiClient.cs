using System.Globalization;
using System.Net.Http.Json;
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
        var buffs = TryGetObject(stats, "buffs");
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
            Inventory: inventory,
            UnallocatedStatPoints: GetOptionalInt32(stats, "points"),
            Stats: MapCharacterStats(stats),
            Buffs: MapCharacterStats(buffs),
            BuffFlags: new BuffFlagsSnapshot(
                ChillingFrost: GetOptionalBoolean(buffs, "streaks"),
                Stealth: GetOptionalInt32(buffs, "stealth")));
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
        var questMembers = TryGetObject(quest, "members");
        var members = await GetPartyMembersAsync(credentials, retrievedAtUtc, cancellationToken);
        members = ApplyQuestParticipation(members, questMembers);
        var questSnapshot = MapPartyQuest(quest, members);
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

        if (questSnapshot is not null && !string.IsNullOrWhiteSpace(questSnapshot.Key))
        {
            var questMetadata = await GetQuestContentMetadataAsync(credentials, questSnapshot.Key, cancellationToken);
            if (questMetadata.HasAnyValue)
            {
                questSnapshot = questSnapshot with
                {
                    BossHealthTotal = questMetadata.BossHealthTotal ?? questSnapshot.BossHealthTotal,
                    CollectionItemsTotal = questMetadata.CollectionItemsTotal ?? questSnapshot.CollectionItemsTotal,
                    Name = questMetadata.Name ?? questSnapshot.Name,
                    Description = questMetadata.Description ?? questSnapshot.Description,
                    RewardSummary = questMetadata.RewardSummary.Count > 0 ? questMetadata.RewardSummary : questSnapshot.RewardSummary
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

    private async Task<PartyQuestContentMetadata> GetQuestContentMetadataAsync(
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
            var collect = TryGetObject(quest, "collect");

            return new PartyQuestContentMetadata(
                BossHealthTotal: TryGetDecimal(boss, "hp", out var bossHealth) ? bossHealth : null,
                CollectionItemsTotal: SumCollectionRequirements(collect),
                Name: GetOptionalString(quest, "text") ?? GetOptionalString(quest, "name"),
                Description: GetOptionalString(quest, "notes"),
                RewardSummary: BuildQuestRewardSummary(quest));
        }
        catch (HabiticaApiException)
        {
            return PartyQuestContentMetadata.Empty;
        }
        catch (JsonException)
        {
            return PartyQuestContentMetadata.Empty;
        }
        catch (HttpRequestException)
        {
            return PartyQuestContentMetadata.Empty;
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

    public async Task CastSpellAsync(
        HabiticaCredentials credentials,
        string spellId,
        string? targetId,
        CancellationToken cancellationToken)
    {
        var path = $"user/class/cast/{Uri.EscapeDataString(spellId)}";
        if (!string.IsNullOrWhiteSpace(targetId))
        {
            path += $"?targetId={Uri.EscapeDataString(targetId)}";
        }

        using var request = CreateRequest(HttpMethod.Post, path, credentials);
        using var _ = await SendForDocumentAsync(request, cancellationToken);
    }

    public async Task AllocateStatsAsync(
        HabiticaCredentials credentials,
        StatAllocation allocation,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, "user/allocate-bulk", credentials);
        request.Content = JsonContent.Create(new
        {
            stats = new
            {
                str = allocation.Strength,
                @int = allocation.Intelligence,
                con = allocation.Constitution,
                per = allocation.Perception
            }
        });
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

        var questCatalog = MapQuestCatalog(TryGetObject(document.RootElement.GetProperty("data"), "quests"));

        return new GearCatalogSnapshot(DateTimeOffset.UtcNow, items, questCatalog);
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

    private static IReadOnlyList<PartyMemberSnapshot> ApplyQuestParticipation(
        IReadOnlyList<PartyMemberSnapshot> members,
        JsonElement questMembers)
    {
        if (questMembers.ValueKind != JsonValueKind.Object)
        {
            return members;
        }

        return members
            .Select(member =>
            {
                var status = questMembers.TryGetProperty(member.MemberId, out var participation)
                    ? participation.ValueKind switch
                    {
                        JsonValueKind.True => PartyQuestParticipationStatus.Accepted,
                        JsonValueKind.False => PartyQuestParticipationStatus.Rejected,
                        JsonValueKind.Null => PartyQuestParticipationStatus.Pending,
                        _ => PartyQuestParticipationStatus.Unknown
                    }
                    : PartyQuestParticipationStatus.Unknown;
                return member with
                {
                    ParticipationStatus = status
                };
            })
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
            ParseNullableDate(task),
            TryGetDecimal(task, "value", out var value) ? value : null,
            IsChallengeTask(task));
    }

    private static bool IsChallengeTask(JsonElement task)
    {
        var challenge = TryGetObject(task, "challenge");
        return challenge.ValueKind == JsonValueKind.Object
            && !string.IsNullOrWhiteSpace(GetOptionalString(challenge, "id"));
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
            OwnedGearKeys: ownedGearKeys,
            OwnedQuestScrolls: CountEntries(TryGetObject(items, "quests")));
    }

    private static PartyMemberSnapshot MapPartyMember(JsonElement member, DateTimeOffset retrievedAtUtc)
    {
        var profile = TryGetObject(member, "profile");
        var preferences = TryGetObject(member, "preferences");
        var stats = TryGetObject(member, "stats");
        var id = GetOptionalString(member, "_id") ?? GetOptionalString(member, "id") ?? string.Empty;
        var displayName = GetOptionalString(profile, "name")
            ?? GetOptionalString(member, "displayName")
            ?? GetOptionalString(member, "username")
            ?? "Unknown party member";
        var authTimestamps = TryGetObject(TryGetObject(member, "auth"), "timestamps");
        var partyQuestProgress = TryGetObject(TryGetObject(TryGetObject(member, "party"), "quest"), "progress");
        var isInInn = GetOptionalBoolean(preferences, "sleep");
        var input = new PartyMemberCronInput(
            id,
            displayName,
            ParseDateTimeOffset(GetOptionalString(member, "lastCron"))
                ?? ParseDateTimeOffset(GetOptionalString(authTimestamps, "loggedin")),
            GetOptionalNullableInt32(preferences, "dayStart"),
            GetOptionalNullableInt32(preferences, "timezoneOffset") ?? GetOptionalNullableInt32(preferences, "timezoneOffsetAtLastCron"),
            isInInn);

        var snapshot = PartyCronCalculator.ClassifyMember(input, retrievedAtUtc, DateTimeOffset.UtcNow);
        return snapshot with
        {
            ClassName = GetOptionalString(stats, "class"),
            Level = TryGetOptionalInt32(stats, "lvl"),
            IsInInn = isInInn,
            PendingQuestDamage = TryGetDecimal(partyQuestProgress, "up", out var pendingQuestDamage)
                ? pendingQuestDamage
                : null,
            PendingQuestItems = GetPendingQuestItems(partyQuestProgress),
            Stats = MapPartyMemberStats(stats),
            CreatedAtUtc = ParseDateTimeOffset(GetOptionalString(authTimestamps, "created")),
            LastLoggedInUtc = ParseDateTimeOffset(GetOptionalString(authTimestamps, "loggedin"))
        };
    }

    private static PartyQuestSnapshot? MapPartyQuest(JsonElement quest, IReadOnlyList<PartyMemberSnapshot> members)
    {
        if (quest.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var questKey = GetOptionalString(quest, "key");
        if (string.IsNullOrWhiteSpace(questKey))
        {
            return null;
        }

        var progress = TryGetObject(quest, "progress");
        var questProgress = MapQuestProgress(progress);
        var pendingDamage = TryGetDecimal(progress, "up", out var up) ? up : (decimal?)null;
        var bossHealthRemaining = TryGetDecimal(progress, "hp", out var hp) ? hp : (decimal?)null;
        var pendingPartyDamage = TryGetDecimal(progress, "down", out var down) ? down : (decimal?)null;

        return new PartyQuestSnapshot(
            Key: questKey,
            IsActive: GetOptionalBoolean(quest, "active"),
            ProgressUp: questProgress.Value,
            ProgressDown: pendingPartyDamage ?? 0m,
            ParticipantCount: members.Count(static member => member.ParticipationStatus == PartyQuestParticipationStatus.Accepted),
            ProgressLabel: questProgress.Label,
            PendingDamage: pendingDamage,
            BossHealthRemaining: bossHealthRemaining,
            PendingPartyDamage: pendingPartyDamage,
            QuestType: bossHealthRemaining is not null ? PartyQuestType.Boss : PartyQuestType.Collection);
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
        if (member.IsInInn)
        {
            return false;
        }

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

    private static CharacterStatsSnapshot MapCharacterStats(JsonElement stats)
    {
        return new CharacterStatsSnapshot(
            Strength: GetOptionalDecimal(stats, "str"),
            Intelligence: GetOptionalDecimal(stats, "int"),
            Constitution: GetOptionalDecimal(stats, "con"),
            Perception: GetOptionalDecimal(stats, "per"));
    }

    private static PartyMemberStatBreakdownSnapshot? MapPartyMemberStats(JsonElement stats)
    {
        if (stats.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var training = TryGetObject(stats, "training");
        var allocated = TryGetObject(stats, "allocated");
        var baseAllocated = PartyStatSectionSnapshot.FromCharacterStats(MapCharacterStats(
            training.ValueKind == JsonValueKind.Object
                ? training
                : allocated.ValueKind == JsonValueKind.Object
                    ? allocated
                    : stats));
        var gear = PartyStatSectionSnapshot.FromCharacterStats(MapCharacterStats(TryGetObject(stats, "gear")));
        var buffs = PartyStatSectionSnapshot.FromCharacterStats(MapCharacterStats(TryGetObject(stats, "buffs")));
        var totalStats = TryGetObject(stats, "total");
        var effectiveStats = TryGetObject(stats, "effective");
        var total = totalStats.ValueKind == JsonValueKind.Object
            ? PartyStatSectionSnapshot.FromCharacterStats(MapCharacterStats(totalStats))
            : effectiveStats.ValueKind == JsonValueKind.Object
                ? PartyStatSectionSnapshot.FromCharacterStats(MapCharacterStats(effectiveStats))
            : null;

        var breakdown = new PartyMemberStatBreakdownSnapshot(baseAllocated, gear, buffs, total);
        return breakdown.HasAnySection ? breakdown : null;
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

    private static IReadOnlyDictionary<string, int> CountEntries(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }

        var entries = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            var count = property.Value.ValueKind switch
            {
                JsonValueKind.Number => property.Value.GetInt32(),
                JsonValueKind.True => 1,
                _ => 0
            };

            if (count > 0)
            {
                entries[property.Name] = count;
            }
        }

        return entries;
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

    private static decimal? SumCollectionRequirements(JsonElement collect)
    {
        if (collect.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var total = 0m;
        var hasValue = false;
        foreach (var property in collect.EnumerateObject())
        {
            var item = property.Value;
            decimal requiredValue;
            if (item.ValueKind == JsonValueKind.Number)
            {
                requiredValue = item.GetDecimal();
            }
            else if (TryGetDecimal(item, "count", out var count))
            {
                requiredValue = count;
            }
            else if (TryGetDecimal(item, "required", out var required))
            {
                requiredValue = required;
            }
            else
            {
                continue;
            }

            if (requiredValue <= 0m)
            {
                continue;
            }

            hasValue = true;
            total += requiredValue;
        }

        return hasValue ? total : null;
    }

    private static IReadOnlyDictionary<string, QuestCatalogItem> MapQuestCatalog(JsonElement quests)
    {
        if (quests.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, QuestCatalogItem>(StringComparer.Ordinal);
        }

        var catalog = new Dictionary<string, QuestCatalogItem>(StringComparer.Ordinal);
        foreach (var property in quests.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var quest = property.Value;
            var key = GetOptionalString(quest, "key") ?? property.Name;
            catalog[key] = new QuestCatalogItem(
                Key: key,
                Text: GetOptionalString(quest, "text") ?? GetOptionalString(quest, "name") ?? key,
                Notes: GetOptionalString(quest, "notes"),
                Category: GetOptionalString(quest, "category") ?? "Quest",
                QuestType: ResolveQuestType(quest),
                RewardSummary: BuildQuestRewardSummary(quest));
        }

        return catalog;
    }

    private static string ResolveQuestType(JsonElement quest)
    {
        return TryGetObject(quest, "boss").ValueKind == JsonValueKind.Object
            ? "Boss"
            : TryGetObject(quest, "collect").ValueKind == JsonValueKind.Object
                ? "Collection"
                : "Quest";
    }

    private static IReadOnlyList<string> BuildQuestRewardSummary(JsonElement quest)
    {
        var rewards = new List<string>();
        var rewardElement = TryGetObject(quest, "rewards");
        var dropElement = TryGetObject(quest, "drop");
        AddRewardCurrency(rewards, rewardElement);
        AddRewardCurrency(rewards, dropElement);
        AddRewardItems(rewards, TryGetObject(rewardElement, "items"));
        AddRewardItems(rewards, TryGetObject(dropElement, "items"));
        AddRewardItems(rewards, TryGetObject(rewardElement, "unlock"));
        AddRewardItems(rewards, TryGetObject(dropElement, "unlock"));
        AddRewardItems(rewards, TryGetObject(quest, "unlock"));
        return rewards;
    }

    private static void AddRewardCurrency(List<string> rewards, JsonElement value)
    {
        if (TryGetDecimal(value, "gp", out var gold) && gold > 0m)
        {
            AddRewardLabel(rewards, $"{gold:0.##} Gold");
        }

        if (TryGetDecimal(value, "exp", out var experience) && experience > 0m)
        {
            AddRewardLabel(rewards, $"{experience:0.##} XP");
        }
    }

    private static void AddRewardItems(List<string> rewards, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray())
                {
                    AddRewardItems(rewards, item);
                }

                break;
            case JsonValueKind.Object:
                if (TryAddRewardItem(rewards, value))
                {
                    break;
                }

                foreach (var property in value.EnumerateObject())
                {
                    if (property.Value.ValueKind is JsonValueKind.Number or JsonValueKind.True)
                    {
                        AddRewardLabel(rewards, property.Name);
                    }
                    else
                    {
                        AddRewardItems(rewards, property.Value);
                    }
                }

                break;
            case JsonValueKind.String:
                AddRewardLabel(rewards, value.GetString());
                break;
            case JsonValueKind.Number:
            case JsonValueKind.True:
                break;
        }
    }

    private static bool TryAddRewardItem(List<string> rewards, JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var name = GetOptionalString(item, "text")
            ?? GetOptionalString(item, "name")
            ?? GetOptionalString(item, "key");
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        AddRewardLabel(rewards, name);
        return true;
    }

    private static void AddRewardLabel(List<string> rewards, string? label)
    {
        if (!string.IsNullOrWhiteSpace(label) && !rewards.Contains(label, StringComparer.OrdinalIgnoreCase))
        {
            rewards.Add(label);
        }
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

    private static int? TryGetOptionalInt32(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            ? property.GetInt32()
            : null;
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

    private sealed record PartyQuestContentMetadata(
        decimal? BossHealthTotal,
        decimal? CollectionItemsTotal,
        string? Name,
        string? Description,
        IReadOnlyList<string> RewardSummary)
    {
        public bool HasAnyValue =>
            BossHealthTotal is not null
            || CollectionItemsTotal is not null
            || !string.IsNullOrWhiteSpace(Name)
            || !string.IsNullOrWhiteSpace(Description)
            || RewardSummary.Count > 0;

        public static PartyQuestContentMetadata Empty { get; } = new(null, null, null, null, Array.Empty<string>());
    }
}
