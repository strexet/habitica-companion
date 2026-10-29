using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Habitica.Domain.Diagnostics;

public static class DiagnosticsLogSanitizer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string SanitizeJson(string json)
    {
        try
        {
            var entries = JsonSerializer.Deserialize<DiagnosticsLogEntry?[]>(json, JsonOptions)
                ?? throw new JsonException();
            return JsonSerializer.Serialize(entries.Where(entry => entry is not null).Select(entry => Sanitize(entry!))
                .GroupBy(entry => entry.Id, StringComparer.Ordinal).Select(group => group.First()).Take(250), JsonOptions);
        }
        catch (JsonException) { throw new InvalidOperationException("Diagnostic history could not be safely exported."); }
    }

    private static readonly HashSet<string> Operations = new(StringComparer.Ordinal)
    {
        "sign-in", "cloud-sync", "cloud-sync-upload", "cloud-sync-download", "cloud-sync-metadata", "party-sync", "sign-in-background-refresh",
        "integration-enroll", "integration-link", "integration-revalidate", "integration-disconnect", "integration-status", "integration-revoke", "integration-management",
        "appearance-select", "appearance-save", "appearance-delete", "appearance-import",
        "inventory-refresh-catalog", "inventory-remove-preset", "inventory-rename-preset", "inventory-equip-item",
        "inventory-save-preset", "inventory-apply-preset", "inventory-bulk-sell", "armoire-bulk-buy", "health-potion-buy", "gems-for-gold-buy",
        "spell-cast", "spell-cast-party-refresh", "spell-cast-cancel-refresh", "task-score", "stats-allocate",
        "cron-party-refresh", "cron-start-new-day", "party-quest-invite-sync", "party-quest-invite", "party-quest-start-reconcile", "party-quest-start",
        "quest-lifecycle-complete", "quest-lifecycle-activate", "quest-lifecycle-detect-complete", "safe-live-tests", "reversible-gear-roundtrip",
        "preset-useraccount", "preset-userinventory", "preset-tasksuser", "preset-party",
        "preset-user-account", "preset-user-inventory", "preset-tasks-user", "inventory-save-recommendation",
        "inventory-equip-preset", "inventory-equip-slots", "pets-feed", "pets-equip", "pets-hatch", "mounts-equip",
        "party-quest-accept", "party-quest-reject", "refresh-userprofile", "refresh-gearcatalog", "refresh-cloudsync", "refresh-partysync",
        "refresh-user", "refresh-tasks", "refresh-party", "refresh-inventory", "refresh-content",
        "workspace-load", "workspace-create-local", "workspace-task-prepare", "workspace-task-accept", "workspace-task-recover",
        "workspace-tag-consent-read", "workspace-tag-consent-set", "workspace-tag-status", "workspace-tag-candidates",
        "workspace-tag-prepare", "workspace-tag-accept", "workspace-tag-recover", "workspace-tag-original-receipt",
        "workspace-link-preview", "workspace-link-adopt", "workspace-connect", "workspace-status", "workspace-operations",
        "workspace-executor", "workspace-export-cloud", "workspace-disconnect", "workspace-delete", "workspace-transfer",
        "workspace-export-local", "workspace-restore-local", "workspace-organization-prepare", "workspace-organization-accept",
        "workspace-organization-recover", "workspace-saved-list", "workspace-saved-query", "workspace-party-planning",
        "workspace-retirement-read", "workspace-retirement-prepare", "workspace-retirement-verify", "workspace-retirement-accept", "workspace-retirement-reconcile",
        "workspace-conversion-read", "workspace-conversion-prepare", "workspace-conversion-accept", "workspace-conversion-recover",
        "notes-load", "notes-connect", "notes-save", "notes-lifecycle", "notes-recover", "notes-cloud-cleanup"
    };
    private static readonly HashSet<string> Counts = new(StringComparer.Ordinal)
    {
        "httpStatus", "cooldownSeconds", "requestCount", "taskCount", "resultCount", "itemCount", "count", "completed", "requested", "requestedCount", "completedCount",
        "payloadBytes", "maxBytes", "metadataPayloadBytes", "gearRequestCount", "restoreGearRequestCount", "requestedPotionCount", "completedPurchaseCount", "str", "int", "con", "per"
    };
    private static readonly HashSet<string> Measurements = new(StringComparer.Ordinal)
        { "healthBefore", "healthAfter", "goldBefore", "goldAfter", "gemBalanceBefore", "gemBalanceAfter" };
    private static readonly Dictionary<string, string[]> Choices = new(StringComparer.Ordinal)
    {
        ["stage"] = ["auto-equip", "cast", "restore-gear", "refresh-user", "refresh-tasks", "refresh-party", "load-cache", "completed", "preparation", "casting", "refresh", "restore", "refreshing", "restoring"],
        ["cleanupStatus"] = ["NotNeeded", "Pending", "Completed", "Failed", "Unknown", "Skipped"],
        ["refreshStatus"] = ["NotNeeded", "Pending", "Completed", "Failed", "Unknown", "Skipped"],
        ["gearGoal"] = ["INT for mana", "CON for defense"],
        ["endpoint"] = ["POST /user/buy/potion"],
        ["itemType"] = ["Egg", "HatchingPotion", "Food", "Quest"],
        ["taskType"] = ["Habit", "Daily", "Todo", "habit", "daily", "todo"],
        ["direction"] = ["Up", "Down", "up", "down", "Upload", "Download", "Metadata"],
        ["presetKind"] = ["Battle", "Costume"],
        ["provider"] = ["Cloudflare", "cloudflare"]
    };
    private static readonly HashSet<string> Flags = new(StringComparer.Ordinal)
        { "castOutcomeUnknown", "equipmentOutcomeUnknown", "automatic", "autoEquip", "cronStarted", "cronCompleted", "restoreStarted", "restoreCompleted", "recommendedForCronRisk", "mergedRemoteData" };
    private static readonly HashSet<string> Sections = new(StringComparer.Ordinal)
        { "user-profile", "tasks-current", "task-order-preferences", "inventory-current", "inventory-catalog", "saved-presets", "party-current", "party-cron-history", "diagnostics", "color-schemes", "sync-metadata", "settings" };
    private static readonly HashSet<string> Statuses = new(StringComparer.Ordinal)
        { "Pending", "Success", "Succeeded", "Failed", "Skipped", "Excluded", "Unavailable", "Conflict" };
    private static readonly HashSet<string> SectionLists = new(StringComparer.Ordinal)
        { "succeededSections", "failedSections", "skippedSections", "excludedSections", "conflictSections" };

    public static DiagnosticsLogEntry Sanitize(DiagnosticsLogEntry entry)
    {
        var operation = Operations.Contains(entry.Operation ?? "") ? entry.Operation : "app-operation";
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in entry.Metadata ?? new Dictionary<string, string>())
        {
            if (Counts.Contains(pair.Key) && long.TryParse(pair.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var count) && count is >= 0 and <= 1_000_000_000)
                metadata[pair.Key] = count.ToString(CultureInfo.InvariantCulture);
            else if (Measurements.Contains(pair.Key) && decimal.TryParse(pair.Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var measurement) && measurement is >= 0 and <= 1_000_000_000)
                metadata[pair.Key] = measurement.ToString(CultureInfo.InvariantCulture);
            else if (Flags.Contains(pair.Key) && bool.TryParse(pair.Value, out var flag)) metadata[pair.Key] = flag.ToString();
            else if (Choices.TryGetValue(pair.Key, out var options) && options.Contains(pair.Value, StringComparer.Ordinal)) metadata[pair.Key] = pair.Value;
            else if (pair.Key == "metadataStatus" && Statuses.Contains(pair.Value)) metadata[pair.Key] = pair.Value;
            else if (SectionMetadata(pair.Key, pair.Value) is {} sections) metadata[pair.Key] = sections;
        }
        var context = entry.Context;
        if (context is not null)
            context = context with { Result = Enum.IsDefined(context.Result) ? context.Result : DiagnosticsResult.Unavailable,
                DurationMs = Math.Clamp(context.DurationMs, 0, 3_600_000), CorrelationId = context.CorrelationId == Guid.Empty ? null : context.CorrelationId,
                CommandId = context.CommandId == Guid.Empty ? null : context.CommandId,
                AcceptedCount = Bounded(context.AcceptedCount), ConfirmedCount = Bounded(context.ConfirmedCount), RequestedCount = Bounded(context.RequestedCount) };
        var severity = Enum.IsDefined(entry.Severity) ? entry.Severity : DiagnosticsSeverity.Warning;
        var id = entry.Id ?? "";
        if (!id.StartsWith("log-", StringComparison.Ordinal) || id.Length != 68 || !id.AsSpan(4).ContainsOnlyHex())
            id = "log-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id))).ToLowerInvariant();
        return entry with { Id = id, Operation = operation!, Message = operation!.Replace('-', ' ') + ": " + (context?.Result.ToString() ?? severity.ToString()) + ".",
            OccurredAtUtc = entry.OccurredAtUtc.ToUniversalTime(),
            Severity = severity, FeatureArea = Enum.IsDefined(entry.FeatureArea) ? entry.FeatureArea : DiagnosticsFeatureArea.Diagnostics,
            Mode = Enum.IsDefined(entry.Mode) ? entry.Mode : DiagnosticsMode.Local, Metadata = metadata, Context = context };
    }

    private static int? Bounded(int? value) => value is >= 0 and <= 1_000_000_000 ? value : null;
    private static string? SectionMetadata(string key, string? value)
    {
        if (value is null || value.Length > 1024) return null;
        var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (SectionLists.Contains(key)) return parts.All(Sections.Contains) ? string.Join(", ", parts) : null;
        if (key == "sectionStatuses" && parts.All(part => part.Split(':') is [var section, var status] && Sections.Contains(section) && Statuses.Contains(status)))
            return string.Join(", ", parts);
        if (key == "sectionPayloadBytes" && parts.All(part => part.Split(':') is [var section, var count] && Sections.Contains(section)
                && long.TryParse(count, NumberStyles.None, CultureInfo.InvariantCulture, out var bytes) && bytes is >= 0 and <= 1_000_000_000))
            return string.Join(", ", parts);
        return null;
    }
    private static bool ContainsOnlyHex(this ReadOnlySpan<char> value)
    { foreach (var c in value) if (!char.IsAsciiHexDigit(c)) return false; return true; }
}
