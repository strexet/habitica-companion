using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Habitica.Domain.Auth;
using Habitica.Domain.Party;
using Habitica.Domain.Tasks;
using Habitica.Domain.User;

namespace Habitica.Api;

public sealed class HabiticaApiClient : IHabiticaSyncClient
{
    // Habitica's documented contract: 30 requests / 60s per user, regenerating
    // per second. These are fallbacks until the server's X-RateLimit-* headers
    // tell us the live numbers, after which we trust the headers.
    private const double DefaultRateLimit = 30d;
    private const double RateLimitWindowSeconds = 60d;

    // Token-bucket tuning. While the estimated budget stays above
    // BurstHeadroomFraction of the limit, requests fire with only the base
    // spacing (a free burst). Below it, the delay ramps up smoothly toward the
    // steady refill cadence. ReserveTokens keeps a safety margin so estimate
    // lag never drives us into an actual 429.
    private const double BurstHeadroomFraction = 0.5d;
    private const double RampSteepness = 3.0d;
    private const double ReserveTokens = 2.0d;

    private readonly HttpClient _httpClient;
    private readonly HabiticaApiClientOptions _options;
    private readonly TimeSpan _minRequestSpacing;

    // Adaptive throttle state. This client is used sequentially within a user
    // session (each request is awaited before the next), matching the existing
    // lock-free handling of _rateLimitPauseUntilUtc.
    private double _rateLimit = DefaultRateLimit;
    private double _estimatedTokens = DefaultRateLimit; // optimistic: assume a full bucket
    private DateTimeOffset _tokensUpdatedUtc = DateTimeOffset.UtcNow;
    private DateTimeOffset _lastRequestUtc = DateTimeOffset.MinValue;
    private DateTimeOffset? _rateLimitPauseUntilUtc;

    public HabiticaApiClient(HttpClient httpClient, HabiticaApiClientOptions options)
    {
        _httpClient = httpClient;
        _options = options;
        _minRequestSpacing = TimeSpan.FromMilliseconds(Math.Max(0, options.MinRequestSpacingMilliseconds));
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
        var retrievedAtUtc = DateTimeOffset.UtcNow;
        using var request = CreateRequest(HttpMethod.Get, "user", credentials);
        using var document = await SendForDocumentAsync(request, cancellationToken);
        var data = document.RootElement.GetProperty("data");
        var profile = data.GetProperty("profile");
        var stats = data.GetProperty("stats");
        var buffs = TryGetObject(stats, "buffs");
        var preferences = TryGetObject(data, "preferences");
        var flags = TryGetObject(data, "flags");
        var purchased = TryGetObject(data, "purchased");
        var purchasePlan = TryGetObject(purchased, "plan");
        var items = data.TryGetProperty("items", out var itemsProperty) ? itemsProperty : default;
        var gear = TryGetObject(items, "gear");
        var inventory = MapInventory(gear, items);
        var lastCronUtc = ParseDateTimeOffset(GetOptionalString(data, "lastCron"));
        var dayStartHour = GetOptionalNullableInt32(preferences, "dayStart");
        var timezoneOffsetMinutes = GetOptionalNullableInt32(preferences, "timezoneOffset")
            ?? GetOptionalNullableInt32(preferences, "timezoneOffsetAtLastCron");
        var currentHabiticaDayStartUtc = dayStartHour is not null && timezoneOffsetMinutes is not null
            ? HabiticaDayCalculator.ComputeCurrentDayStartUtc(retrievedAtUtc, dayStartHour.Value, timezoneOffsetMinutes.Value)
            : (DateTimeOffset?)null;
        var currentHabiticaDayKey = dayStartHour is not null && timezoneOffsetMinutes is not null
            ? HabiticaDayCalculator.ComputeDayKey(retrievedAtUtc, dayStartHour.Value, timezoneOffsetMinutes.Value)
            : null;
        var needsCron = GetOptionalNullableBoolean(flags, "needsCron")
            ?? HabiticaDayCalculator.NeedsCron(lastCronUtc, currentHabiticaDayStartUtc);

        return new UserSnapshot(
            RetrievedAtUtc: retrievedAtUtc,
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
                Stealth: GetOptionalInt32(buffs, "stealth")),
            LastCronUtc: lastCronUtc,
            DayStartHour: dayStartHour,
            TimezoneOffsetMinutes: timezoneOffsetMinutes,
            CurrentHabiticaDayKey: currentHabiticaDayKey,
            CurrentHabiticaDayStartUtc: currentHabiticaDayStartUtc,
            NeedsCron: needsCron,
            GemBalance: TryGetDecimal(data, "balance", out var gemBalance) ? gemBalance : null,
            CanBuyGemsForGold: GetCanBuyGemsForGold(purchasePlan),
            RemainingGemPurchases: GetRemainingGemPurchases(purchasePlan));
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

    public async Task ScoreTaskAsync(
        HabiticaCredentials credentials,
        string taskId,
        TaskScoreDirection direction,
        CancellationToken cancellationToken)
    {
        var directionSegment = direction == TaskScoreDirection.Down ? "down" : "up";
        using var request = CreateRequest(
            HttpMethod.Post,
            $"tasks/{Uri.EscapeDataString(taskId)}/score/{directionSegment}",
            credentials);
        using var _ = await SendForDocumentAsync(request, cancellationToken);
    }

    public async Task StartPartyQuestAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, "groups/party/quests/force-start", credentials);
        using var _ = await SendForDocumentAsync(request, cancellationToken);
    }

    public async Task InvitePartyToQuestAsync(HabiticaCredentials credentials, string questKey, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(
            HttpMethod.Post,
            $"groups/party/quests/invite/{Uri.EscapeDataString(questKey)}",
            credentials);
        using var _ = await SendForDocumentAsync(request, cancellationToken);
    }

    public async Task AcceptPartyQuestAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, "groups/party/quests/accept", credentials);
        using var _ = await SendForDocumentAsync(request, cancellationToken);
    }

    public async Task RejectPartyQuestAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, "groups/party/quests/reject", credentials);
        using var _ = await SendForDocumentAsync(request, cancellationToken);
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

        var contentMetadata = await GetPartyContentMetadataAsync(credentials, questSnapshot?.Key, cancellationToken);
        if (contentMetadata.Gear.Count > 0)
        {
            members = EnrichPartyMemberStatsWithGear(members, contentMetadata.Gear);
        }

        if (questSnapshot is not null && contentMetadata.Quest.HasAnyValue)
        {
            var questMetadata = contentMetadata.Quest;
            questSnapshot = questSnapshot with
            {
                BossHealthTotal = questMetadata.BossHealthTotal ?? questSnapshot.BossHealthTotal,
                CollectionItemsTotal = questMetadata.CollectionItemsTotal ?? questSnapshot.CollectionItemsTotal,
                Name = questMetadata.Name ?? questSnapshot.Name,
                Description = questMetadata.Description ?? questSnapshot.Description,
                RewardSummary = questMetadata.RewardSummary.Count > 0 ? questMetadata.RewardSummary : questSnapshot.RewardSummary
            };
        }

        var recentChatMessages = questSnapshot?.IsActive == true
            ? Array.Empty<PartyChatMessageSnapshot>()
            : await GetPartyChatMessagesAsync(credentials, cancellationToken);

        return new PartySnapshot(
            retrievedAtUtc: retrievedAtUtc,
            partyId: GetOptionalString(data, "_id") ?? string.Empty,
            name: GetOptionalString(data, "name") ?? "Unnamed Party",
            summary: GetOptionalString(data, "description") ?? GetOptionalString(data, "summary"),
            memberCount: GetOptionalInt32(data, "memberCount"),
            quest: questSnapshot,
            members: members,
            leaderId: GetOptionalString(data, "leader"),
            recentChatMessages: recentChatMessages);
    }

    private async Task<IReadOnlyList<PartyChatMessageSnapshot>> GetPartyChatMessagesAsync(
        HabiticaCredentials credentials,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Get, "groups/party/chat", credentials);
            using var document = await SendForDocumentAsync(request, cancellationToken);
            var data = document.RootElement.TryGetProperty("data", out var dataElement) ? dataElement : default;
            if (data.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<PartyChatMessageSnapshot>();
            }

            return data
                .EnumerateArray()
                .Select(MapPartyChatMessage)
                .ToArray();
        }
        catch (HabiticaApiException)
        {
            return Array.Empty<PartyChatMessageSnapshot>();
        }
        catch (JsonException)
        {
            return Array.Empty<PartyChatMessageSnapshot>();
        }
        catch (HttpRequestException)
        {
            return Array.Empty<PartyChatMessageSnapshot>();
        }
    }

    private async Task<PartyContentMetadata> GetPartyContentMetadataAsync(
        HabiticaCredentials credentials,
        string? questKey,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Get, "content?language=en", credentials);
            using var document = await SendForDocumentAsync(request, cancellationToken);
            var data = document.RootElement.GetProperty("data");
            var quest = string.IsNullOrWhiteSpace(questKey) ? default : TryGetObject(TryGetObject(data, "quests"), questKey);
            var questMetadata = PartyQuestContentMetadata.Empty;
            if (quest.ValueKind == JsonValueKind.Object)
            {
                var boss = TryGetObject(quest, "boss");
                var collect = TryGetObject(quest, "collect");
                questMetadata = new PartyQuestContentMetadata(
                    BossHealthTotal: TryGetDecimal(boss, "hp", out var bossHealth) ? bossHealth : null,
                    CollectionItemsTotal: SumCollectionRequirements(collect),
                    Name: GetOptionalString(quest, "text") ?? GetOptionalString(quest, "name"),
                    Description: GetOptionalString(quest, "notes"),
                    RewardSummary: BuildQuestRewardSummary(quest));
            }

            return new PartyContentMetadata(questMetadata, MapPartyGearCatalog(TryGetObject(TryGetObject(data, "gear"), "flat")));
        }
        catch (HabiticaApiException)
        {
            return PartyContentMetadata.Empty;
        }
        catch (JsonException)
        {
            return PartyContentMetadata.Empty;
        }
        catch (HttpRequestException)
        {
            return PartyContentMetadata.Empty;
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

    public async Task RunCronAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, "cron", credentials);
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

    public async Task<ArmoirePurchaseSnapshot> BuyArmoireAsync(
        HabiticaCredentials credentials,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, "user/buy-armoire", credentials);
        using var document = await SendForDocumentAsync(request, cancellationToken);
        var data = document.RootElement.TryGetProperty("data", out var dataElement) ? dataElement : default;
        var armoire = TryGetObject(data, "armoire");
        return new ArmoirePurchaseSnapshot(
            DropType: GetOptionalString(armoire, "type") ?? "reward",
            DropKey: GetOptionalString(armoire, "dropKey"),
            DropText: GetOptionalString(armoire, "dropText"),
            Experience: TryGetDecimal(armoire, "value", out var experience) ? experience : null,
            Message: GetOptionalString(document.RootElement, "message") ?? "Armoire opened.");
    }

    public async Task BuyHealthPotionAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, "user/buy/potion", credentials);
        using var _ = await SendForDocumentAsync(request, cancellationToken);
    }

    public async Task PurchaseGemsForGoldAsync(HabiticaCredentials credentials, int quantity, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, "user/purchase/gems/gem", credentials);
        request.Content = JsonContent.Create(new
        {
            quantity = Math.Max(1, quantity)
        });
        using var _ = await SendForDocumentAsync(request, cancellationToken);
    }

    public async Task FeedPetAsync(
        HabiticaCredentials credentials,
        string petKey,
        string foodKey,
        int amount,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(
            HttpMethod.Post,
            $"user/feed/{Uri.EscapeDataString(petKey)}/{Uri.EscapeDataString(foodKey)}?amount={Math.Max(1, amount).ToString(CultureInfo.InvariantCulture)}",
            credentials);
        using var _ = await SendForDocumentAsync(request, cancellationToken);
    }

    public async Task EquipPetAsync(HabiticaCredentials credentials, string key, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, $"user/equip/pet/{Uri.EscapeDataString(key)}", credentials);
        using var _ = await SendForDocumentAsync(request, cancellationToken);
    }

    public async Task EquipMountAsync(HabiticaCredentials credentials, string key, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, $"user/equip/mount/{Uri.EscapeDataString(key)}", credentials);
        using var _ = await SendForDocumentAsync(request, cancellationToken);
    }

    public async Task HatchPetAsync(
        HabiticaCredentials credentials,
        string eggKey,
        string hatchingPotionKey,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(
            HttpMethod.Post,
            $"user/hatch/{Uri.EscapeDataString(eggKey)}/{Uri.EscapeDataString(hatchingPotionKey)}",
            credentials);
        using var _ = await SendForDocumentAsync(request, cancellationToken);
    }

    public async Task SellInventoryItemAsync(
        HabiticaCredentials credentials,
        InventorySellItemType type,
        string key,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(
            HttpMethod.Post,
            $"user/sell/{GetSellItemTypePath(type)}/{Uri.EscapeDataString(key)}",
            credentials);
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
        await ThrottleAsync(cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var rateLimit = ExtractRateLimitInfo(response);
        ReconcileRateLimit(rateLimit);

        if (!response.IsSuccessStatusCode)
        {
            throw CreateApiException(response, content, rateLimit);
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
                    ? MapQuestParticipationStatus(participation)
                    : PartyQuestParticipationStatus.Unknown;
                return member with
                {
                    ParticipationStatus = status
                };
            })
            .ToArray();
    }

    private static PartyQuestParticipationStatus MapQuestParticipationStatus(JsonElement participation)
    {
        return participation.ValueKind switch
        {
            JsonValueKind.True => PartyQuestParticipationStatus.Accepted,
            JsonValueKind.False => PartyQuestParticipationStatus.Rejected,
            JsonValueKind.Null => PartyQuestParticipationStatus.Pending,
            JsonValueKind.String when string.Equals(participation.GetString(), "true", StringComparison.OrdinalIgnoreCase) => PartyQuestParticipationStatus.Accepted,
            JsonValueKind.String when string.Equals(participation.GetString(), "false", StringComparison.OrdinalIgnoreCase) => PartyQuestParticipationStatus.Rejected,
            JsonValueKind.String when string.Equals(participation.GetString(), "null", StringComparison.OrdinalIgnoreCase) => PartyQuestParticipationStatus.Pending,
            _ => PartyQuestParticipationStatus.Unknown
        };
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
            IsChallengeTask(task),
            GetOptionalNullableBoolean(task, "up"),
            GetOptionalNullableBoolean(task, "down"),
            ParseTaskHistory(task),
            GetOptionalNullableBoolean(task, "isDue"));
    }

    private static bool IsChallengeTask(JsonElement task)
    {
        var challenge = TryGetObject(task, "challenge");
        return challenge.ValueKind == JsonValueKind.Object
            && !string.IsNullOrWhiteSpace(GetOptionalString(challenge, "id"));
    }

    private static IReadOnlyList<TaskHistoryPoint> ParseTaskHistory(JsonElement task)
    {
        if (!task.TryGetProperty("history", out var history) || history.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<TaskHistoryPoint>();
        }

        var points = new List<TaskHistoryPoint>();
        foreach (var item in history.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || !TryParseTaskHistoryDate(item, out var date)
                || !TryGetDecimal(item, "value", out var value))
            {
                continue;
            }

            points.Add(new TaskHistoryPoint(date, value));
        }

        return points
            .OrderBy(static point => point.Date)
            .ToArray();
    }

    private static bool TryParseTaskHistoryDate(JsonElement item, out DateTimeOffset date)
    {
        if (!item.TryGetProperty("date", out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            date = default;
            return false;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var milliseconds))
        {
            date = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
            return true;
        }

        if (property.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(
                property.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            date = parsed;
            return true;
        }

        date = default;
        return false;
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
            OwnedPetCount: CountNonNegativeNumericEntries(TryGetObject(items, "pets")),
            OwnedMountCount: CountTrueEntries(TryGetObject(items, "mounts")),
            OwnedGearKeys: ownedGearKeys,
            OwnedQuestScrolls: CountEntries(TryGetObject(items, "quests")),
            OwnedEggs: CountEntries(TryGetObject(items, "eggs")),
            OwnedFood: CountEntries(TryGetObject(items, "food")),
            OwnedHatchingPotions: CountEntries(TryGetObject(items, "hatchingPotions")),
            OwnedPets: CountNonNegativeEntries(TryGetObject(items, "pets")),
            OwnedMounts: BooleanEntries(TryGetObject(items, "mounts")));
    }

    private static PartyMemberSnapshot MapPartyMember(JsonElement member, DateTimeOffset retrievedAtUtc)
    {
        var profile = TryGetObject(member, "profile");
        var preferences = TryGetObject(member, "preferences");
        var stats = TryGetObject(member, "stats");
        var items = TryGetObject(member, "items");
        var gear = TryGetObject(items, "gear");
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
            LastLoggedInUtc = ParseDateTimeOffset(GetOptionalString(authTimestamps, "loggedin")),
            TotalLogins = GetOptionalNullableInt32(member, "loginIncentives")
                ?? GetOptionalNullableInt32(TryGetObject(member, "flags"), "cronCount"),
            EquippedGearKeys = GetEquippedGearKeys(TryGetObject(gear, "equipped")),
            Health = GetOptionalDecimal(stats, "hp"),
            MaxHealth = GetOptionalDecimal(stats, "maxHealth"),
            Mana = GetOptionalDecimal(stats, "mp"),
            MaxMana = GetOptionalDecimal(stats, "maxMP")
        };
    }

    private static PartyChatMessageSnapshot MapPartyChatMessage(JsonElement message)
    {
        var info = TryGetObject(message, "info");
        return new PartyChatMessageSnapshot(
            MessageId: GetOptionalString(message, "id") ?? GetOptionalString(message, "_id"),
            SentAtUtc: ParseChatTimestamp(message),
            Text: GetOptionalString(message, "text") ?? GetOptionalString(message, "unformattedText"),
            Info: info.ValueKind == JsonValueKind.Object
                ? new PartyChatMessageInfoSnapshot(
                    GetOptionalString(info, "type"),
                    GetOptionalString(info, "quest"))
                : null);
    }

    private static DateTimeOffset? ParseChatTimestamp(JsonElement message)
    {
        var timestamp = TryGetObject(message, "timestamp");
        if (timestamp.ValueKind == JsonValueKind.Number && timestamp.TryGetInt64(out var milliseconds))
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
        }

        return GetOptionalString(message, "timestamp") is { } timestampText
            ? ParseDateTimeOffset(timestampText)
            : null;
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
            Back: GetOptionalString(slots, "back"),
            HeadAccessory: GetOptionalString(slots, "headAccessory"),
            Eyewear: GetOptionalString(slots, "eyewear"),
            Body: GetOptionalString(slots, "body"));
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
        var baseAllocated = MapPartyStatSection(
            training.ValueKind == JsonValueKind.Object
                ? training
                : allocated.ValueKind == JsonValueKind.Object
                    ? allocated
                    : stats);
        var gear = MapPartyStatSection(TryGetObject(stats, "gear"));
        var buffs = MapPartyStatSection(TryGetObject(stats, "buffs"));
        var totalStats = TryGetObject(stats, "total");
        var effectiveStats = TryGetObject(stats, "effective");
        var total = totalStats.ValueKind == JsonValueKind.Object
            ? MapPartyStatSection(totalStats)
            : effectiveStats.ValueKind == JsonValueKind.Object
                ? MapPartyStatSection(effectiveStats)
            : null;

        var breakdown = new PartyMemberStatBreakdownSnapshot(baseAllocated, gear, buffs, total);
        return breakdown.HasAnySection ? breakdown : null;
    }

    private static IReadOnlyList<PartyMemberSnapshot> EnrichPartyMemberStatsWithGear(
        IReadOnlyList<PartyMemberSnapshot> members,
        IReadOnlyDictionary<string, PartyGearCatalogItem> gearCatalog)
    {
        return members
            .Select(member => member.EquippedGearKeys is { Count: > 0 } equippedGearKeys
                ? EnrichPartyMemberStatsWithGear(member, equippedGearKeys, gearCatalog)
                : member)
            .ToArray();
    }

    private static PartyMemberSnapshot EnrichPartyMemberStatsWithGear(
        PartyMemberSnapshot member,
        IReadOnlyList<string> equippedGearKeys,
        IReadOnlyDictionary<string, PartyGearCatalogItem> gearCatalog)
    {
        var gear = ComputeEquipmentStats(equippedGearKeys, gearCatalog, member.ClassName);
        var levelBonus = ComputeLevelBonus(member.Level);
        var current = member.Stats ?? new PartyMemberStatBreakdownSnapshot(null, null, null, null);
        var next = current with
        {
            Gear = gear ?? current.Gear,
            LevelBonus = levelBonus,
            Total = current.Total ?? SumPartyStatSections(levelBonus, current.BaseAllocated, gear ?? current.Gear, current.Buffs)
        };

        return next.HasAnySection
            ? member with { Stats = next }
            : member;
    }

    private static PartyStatSectionSnapshot? ComputeLevelBonus(int? level)
    {
        if (level is null)
        {
            return null;
        }

        var levelBonus = Math.Floor(Math.Min(level.Value, 100) / 2m);
        return new PartyStatSectionSnapshot(levelBonus, levelBonus, levelBonus, levelBonus);
    }

    private static PartyStatSectionSnapshot? ComputeEquipmentStats(
        IReadOnlyList<string> equippedGearKeys,
        IReadOnlyDictionary<string, PartyGearCatalogItem> gearCatalog,
        string? className)
    {
        var hasAnyGear = false;
        var strength = 0m;
        var intelligence = 0m;
        var constitution = 0m;
        var perception = 0m;

        foreach (var key in equippedGearKeys)
        {
            if (!gearCatalog.TryGetValue(key, out var item))
            {
                continue;
            }

            hasAnyGear = true;
            var multiplier = IsClassGear(item, className) ? 1.5m : 1m;
            strength += item.Strength * multiplier;
            intelligence += item.Intelligence * multiplier;
            constitution += item.Constitution * multiplier;
            perception += item.Perception * multiplier;
        }

        return hasAnyGear
            ? new PartyStatSectionSnapshot(strength, intelligence, constitution, perception)
            : null;
    }

    private static bool IsClassGear(PartyGearCatalogItem item, string? className)
    {
        return !string.IsNullOrWhiteSpace(className)
            && (string.Equals(item.ClassName, className, StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.SpecialClassName, className, StringComparison.OrdinalIgnoreCase));
    }

    private static PartyStatSectionSnapshot? SumPartyStatSections(params PartyStatSectionSnapshot?[] sections)
    {
        if (sections.Any(static section => section is null))
        {
            return null;
        }

        return new PartyStatSectionSnapshot(
            sections.Sum(static section => section!.Strength ?? 0m),
            sections.Sum(static section => section!.Intelligence ?? 0m),
            sections.Sum(static section => section!.Constitution ?? 0m),
            sections.Sum(static section => section!.Perception ?? 0m));
    }

    private static PartyStatSectionSnapshot? MapPartyStatSection(JsonElement stats)
    {
        if (stats.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var section = new PartyStatSectionSnapshot(
            TryGetDecimal(stats, "str", out var strength) ? strength : null,
            TryGetDecimal(stats, "int", out var intelligence) ? intelligence : null,
            TryGetDecimal(stats, "con", out var constitution) ? constitution : null,
            TryGetDecimal(stats, "per", out var perception) ? perception : null);

        return section.HasAnyValue ? section : null;
    }

    private static IReadOnlyList<string> GetEquippedGearKeys(JsonElement equipped)
    {
        if (equipped.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<string>();
        }

        return equipped
            .EnumerateObject()
            .Select(static property => property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null)
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .Select(static key => key!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, PartyGearCatalogItem> MapPartyGearCatalog(JsonElement flatGear)
    {
        if (flatGear.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, PartyGearCatalogItem>(StringComparer.Ordinal);
        }

        var gear = new Dictionary<string, PartyGearCatalogItem>(StringComparer.Ordinal);
        foreach (var property in flatGear.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var key = GetOptionalString(property.Value, "key") ?? property.Name;
            gear[key] = new PartyGearCatalogItem(
                key,
                GetOptionalString(property.Value, "klass") ?? GetOptionalString(property.Value, "class"),
                GetOptionalString(property.Value, "specialClass"),
                GetOptionalDecimal(property.Value, "str"),
                GetOptionalDecimal(property.Value, "int"),
                GetOptionalDecimal(property.Value, "con"),
                GetOptionalDecimal(property.Value, "per"));
        }

        return gear;
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

    private static int CountNonNegativeNumericEntries(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Object
            ? element.EnumerateObject().Count(static property =>
                property.Value.ValueKind == JsonValueKind.Number
                && property.Value.GetDecimal() >= 0m)
            : 0;
    }

    private static IReadOnlyDictionary<string, int> CountNonNegativeEntries(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }

        return element.EnumerateObject()
            .Where(static property =>
                property.Value.ValueKind == JsonValueKind.Number
                && property.Value.GetDecimal() >= 0m)
            .ToDictionary(
                static property => property.Name,
                static property => property.Value.GetInt32(),
                StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, bool> BooleanEntries(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, bool>(StringComparer.Ordinal);
        }

        return element.EnumerateObject()
            .Where(static property => property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            .ToDictionary(
                static property => property.Name,
                static property => property.Value.GetBoolean(),
                StringComparer.Ordinal);
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

    private static HabiticaApiException CreateApiException(
        HttpResponseMessage response,
        string responseBody,
        HabiticaRateLimitInfo? rateLimit)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return new HabiticaApiException(
                response.StatusCode,
                BuildRateLimitMessage(rateLimit),
                rateLimit);
        }

        return new HabiticaApiException(
            response.StatusCode,
            ExtractErrorMessage(responseBody, response.ReasonPhrase),
            rateLimit);
    }

    private static HabiticaRateLimitInfo? ExtractRateLimitInfo(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter?.Delta
            ?? (response.Headers.RetryAfter?.Date is { } retryAfterDate
                ? retryAfterDate - DateTimeOffset.UtcNow
                : null);
        if (retryAfter is { } retryAfterValue && retryAfterValue < TimeSpan.Zero)
        {
            retryAfter = TimeSpan.Zero;
        }

        var limit = TryGetHeaderInt32(response, "X-RateLimit-Limit");
        var remaining = TryGetHeaderInt32(response, "X-RateLimit-Remaining");
        var resetAtUtc = TryGetHeaderDateTimeOffset(response, "X-RateLimit-Reset");

        return retryAfter is null && limit is null && remaining is null && resetAtUtc is null
            ? null
            : new HabiticaRateLimitInfo(retryAfter, limit, remaining, resetAtUtc);
    }

    // Adaptive throttle run before every request. Combines a polite base-spacing
    // floor with a token-bucket estimate so callers can burst while budget is
    // healthy and slow down smoothly as the rate-limit window drains, instead of
    // sleeping a flat amount on every call or slamming into a full-window wall.
    private async Task ThrottleAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        // A prior 429 (or a window we drained to zero) wins over everything else.
        if (_rateLimitPauseUntilUtc is { } pauseUntilUtc)
        {
            if (pauseUntilUtc > now)
            {
                await Task.Delay(pauseUntilUtc - now, cancellationToken);
                now = DateTimeOffset.UtcNow;
            }

            _rateLimitPauseUntilUtc = null;
        }

        var refillPerSecond = _rateLimit / RateLimitWindowSeconds;
        RefillTokens(now, refillPerSecond);

        var delay = ComputeAdaptiveDelay(now, refillPerSecond);
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken);
            now = DateTimeOffset.UtcNow;
            RefillTokens(now, refillPerSecond); // credit the refill that happened while waiting
        }

        // Spend the token for the request we are about to send.
        _estimatedTokens = Math.Max(0d, _estimatedTokens - 1d);
        _lastRequestUtc = now;
    }

    private void RefillTokens(DateTimeOffset now, double refillPerSecond)
    {
        var elapsedSeconds = (now - _tokensUpdatedUtc).TotalSeconds;
        if (elapsedSeconds <= 0d)
        {
            return;
        }

        _estimatedTokens = Math.Min(_rateLimit, _estimatedTokens + elapsedSeconds * refillPerSecond);
        _tokensUpdatedUtc = now;
    }

    private TimeSpan ComputeAdaptiveDelay(DateTimeOffset now, double refillPerSecond)
    {
        // Base floor: never fire faster than the configured minimum spacing.
        var floorWait = _minRequestSpacing - (now - _lastRequestUtc);
        if (floorWait < TimeSpan.Zero)
        {
            floorWait = TimeSpan.Zero;
        }

        // Adaptive ramp: zero while we still have burst headroom, then a smooth
        // exponential climb toward the steady refill cadence as the bucket nears
        // the reserve. f(x) = (e^(k·x) − 1) / (e^k − 1) maps x∈[0,1] to [0,1],
        // staying near zero until x (depletion) is large, then rising sharply.
        var bucketWait = TimeSpan.Zero;
        if (_estimatedTokens - ReserveTokens < 1d && refillPerSecond > 0d && _rateLimit > 0d)
        {
            var secondsPerToken = 1d / refillPerSecond; // ~2s at 30 req / 60s
            var fraction = _estimatedTokens / _rateLimit;
            var depletion = Math.Clamp((BurstHeadroomFraction - fraction) / BurstHeadroomFraction, 0d, 1d);
            var ramp = (Math.Exp(RampSteepness * depletion) - 1d) / (Math.Exp(RampSteepness) - 1d);
            bucketWait = TimeSpan.FromSeconds(secondsPerToken * ramp);
        }

        return floorWait > bucketWait ? floorWait : bucketWait;
    }

    // Reconcile the local token estimate with the server's authoritative headers
    // and remember any hard pause the server asked for (429 Retry-After, or a
    // fully drained window).
    private void ReconcileRateLimit(HabiticaRateLimitInfo? rateLimit)
    {
        if (rateLimit is null)
        {
            return;
        }

        if (rateLimit.Limit is { } limit && limit > 0)
        {
            _rateLimit = limit;
        }

        if (rateLimit.Remaining is { } remaining)
        {
            _estimatedTokens = Math.Clamp(remaining, 0d, _rateLimit);
            _tokensUpdatedUtc = DateTimeOffset.UtcNow;
        }

        DateTimeOffset? pauseUntilUtc = null;
        if (rateLimit.RetryAfter is { } retryAfter)
        {
            pauseUntilUtc = DateTimeOffset.UtcNow + retryAfter;
        }
        else if (rateLimit.Remaining == 0 && rateLimit.ResetAtUtc is { } resetAtUtc)
        {
            pauseUntilUtc = resetAtUtc;
        }

        if (pauseUntilUtc is null)
        {
            return;
        }

        _rateLimitPauseUntilUtc = _rateLimitPauseUntilUtc is { } existing && existing > pauseUntilUtc
            ? existing
            : pauseUntilUtc;
    }

    private static string BuildRateLimitMessage(HabiticaRateLimitInfo? rateLimit)
    {
        if (rateLimit?.RetryAfter is { } retryAfter)
        {
            var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
            return $"Habitica is rate limiting requests. Wait {FormatDuration(seconds)} before trying again.";
        }

        if (rateLimit?.ResetAtUtc is { } resetAtUtc)
        {
            var wait = resetAtUtc - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                var seconds = Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds));
                return $"Habitica is rate limiting requests. Wait {FormatDuration(seconds)} before trying again.";
            }
        }

        return "Habitica is rate limiting requests. Wait before trying again.";
    }

    private static string FormatDuration(int seconds)
    {
        return seconds < 60
            ? $"{seconds} second{(seconds == 1 ? string.Empty : "s")}"
            : $"{(int)Math.Ceiling(seconds / 60d)} minute{(seconds <= 60 ? string.Empty : "s")}";
    }

    private static int? TryGetHeaderInt32(HttpResponseMessage response, string headerName)
    {
        return response.Headers.TryGetValues(headerName, out var values)
            && int.TryParse(values.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static DateTimeOffset? TryGetHeaderDateTimeOffset(HttpResponseMessage response, string headerName)
    {
        if (!response.Headers.TryGetValues(headerName, out var values))
        {
            return null;
        }

        var value = values.FirstOrDefault();
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericValue))
        {
            return numericValue < 1_000_000_000
                ? DateTimeOffset.UtcNow.AddSeconds(Math.Max(0, numericValue))
                : DateTimeOffset.FromUnixTimeSeconds(numericValue);
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed.ToUniversalTime();
        }

        return null;
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

    private static string GetSellItemTypePath(InventorySellItemType type)
    {
        return type switch
        {
            InventorySellItemType.Egg => "eggs",
            InventorySellItemType.Food => "food",
            InventorySellItemType.HatchingPotion => "hatchingPotions",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported sell item type.")
        };
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

    private static bool? GetOptionalNullableBoolean(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? property.GetBoolean()
            : null;
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

    private static bool? GetCanBuyGemsForGold(JsonElement plan)
    {
        if (plan.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (GetOptionalNullableBoolean(plan, "canBuyGems") is { } explicitFlag)
        {
            return explicitFlag;
        }

        if (GetOptionalNullableBoolean(plan, "canBuyGemsForGold") is { } explicitGoldFlag)
        {
            return explicitGoldFlag;
        }

        var hasTermination = !string.IsNullOrWhiteSpace(GetOptionalString(plan, "dateTerminated"))
            || !string.IsNullOrWhiteSpace(GetOptionalString(plan, "dateCanceled"));
        if (hasTermination)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(GetOptionalString(plan, "customerId"))
            || !string.IsNullOrWhiteSpace(GetOptionalString(plan, "subscriptionId"))
            || !string.IsNullOrWhiteSpace(GetOptionalString(plan, "planId"))
            || TryGetDecimal(plan, "quantity", out var quantity) && quantity > 0m;
    }

    private static int? GetRemainingGemPurchases(JsonElement plan)
    {
        if (plan.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (GetOptionalNullableInt32(plan, "remainingGemPurchases") is { } explicitRemaining)
        {
            return Math.Max(0, explicitRemaining);
        }

        if (GetOptionalNullableInt32(plan, "gemsRemaining") is { } gemsRemaining)
        {
            return Math.Max(0, gemsRemaining);
        }

        var cap = GetOptionalNullableInt32(plan, "gemsTotal")
            ?? GetOptionalNullableInt32(plan, "gemLimit")
            ?? GetOptionalNullableInt32(plan, "monthlyGemCap")
            ?? GetOptionalNullableInt32(plan, "maxGemPurchases");
        var bought = GetOptionalNullableInt32(plan, "gemsBought");
        return cap is null || bought is null ? null : Math.Max(0, cap.Value - bought.Value);
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
        var normalized = value.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

        if (normalized.StartsWith("headaccessory", StringComparison.Ordinal))
        {
            return "Head Accessory";
        }

        return value.Split('_', 2, StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant() switch
        {
            "head" => "Head",
            "armor" => "Armor",
            "weapon" => "Weapon",
            "shield" => "Shield",
            "back" => "Back",
            "eyewear" => "Eyewear",
            "body" => "Body",
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

    private sealed record PartyContentMetadata(
        PartyQuestContentMetadata Quest,
        IReadOnlyDictionary<string, PartyGearCatalogItem> Gear)
    {
        public static PartyContentMetadata Empty { get; } = new(
            PartyQuestContentMetadata.Empty,
            new Dictionary<string, PartyGearCatalogItem>(StringComparer.Ordinal));
    }

    private sealed record PartyGearCatalogItem(
        string Key,
        string? ClassName,
        string? SpecialClassName,
        decimal Strength,
        decimal Intelligence,
        decimal Constitution,
        decimal Perception);
}
