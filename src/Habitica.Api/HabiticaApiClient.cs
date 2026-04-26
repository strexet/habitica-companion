using System.Globalization;
using System.Text.Json;
using Habitica.Domain.Auth;
using Habitica.Domain.Tasks;
using Habitica.Domain.User;

namespace Habitica.Api;

public sealed class HabiticaApiClient : IHabiticaSyncClient
{
    private const string UserSnapshotFields = "profile,stats,party,items.currentPet,items.currentMount,items.gear.equipped,items.gear.costume,items.gear.owned,items.eggs,items.food,items.hatchingPotions,items.quests,items.pets,items.mounts";
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
        using var request = CreateRequest(HttpMethod.Get, $"user?userFields={UserSnapshotFields}", credentials);
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

    private static decimal GetOptionalDecimal(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            ? property.GetDecimal()
            : 0m;
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static JsonElement TryGetObject(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property)
            ? property
            : default;
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
