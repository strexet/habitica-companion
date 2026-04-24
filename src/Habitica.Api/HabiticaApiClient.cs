using System.Globalization;
using System.Text.Json;
using Habitica.Domain.Auth;
using Habitica.Domain.Tasks;

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
        using var request = CreateRequest(HttpMethod.Get, "user", credentials);
        using var document = await SendForDocumentAsync(request, cancellationToken);
        var data = document.RootElement.GetProperty("data");
        var profile = data.GetProperty("profile");
        var stats = data.GetProperty("stats");

        return new UserSummary(
            profile.GetProperty("name").GetString() ?? "Unknown Habitica User",
            stats.TryGetProperty("class", out var classProperty) ? classProperty.GetString() : null,
            stats.TryGetProperty("lvl", out var levelProperty) ? levelProperty.GetInt32() : 0);
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
