using System.Text.Json;
using System.Text.Json.Serialization;
using Habitica.Domain.Diagnostics;

namespace Habitica.Application.Diagnostics;

public sealed record DiagnosticsBundleFilters(DiagnosticsFeatureArea? Feature = null,
    DiagnosticsSeverity? Severity = null, DiagnosticsMode? Mode = null);

public sealed record DiagnosticsBundle(int Version, DateTimeOffset GeneratedAtUtc, string Source,
    string DeviceContext, string SessionContext, DiagnosticsBundleFilters Filters,
    DateTimeOffset? WindowFromUtc, DateTimeOffset? WindowToUtc, int AvailableCount, int MatchingCount,
    int OmittedMatchingCount, IReadOnlyList<string> Limits, IReadOnlyList<DiagnosticsLogEntry> Entries);

public static class DiagnosticsBundleExport
{
    public const int EntryLimit = 100;
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true, Converters = { new JsonStringEnumConverter() }
    };

    public static DiagnosticsBundle Create(IReadOnlyList<DiagnosticsLogEntry> entries,
        DiagnosticsBundleFilters filters, DateTimeOffset generatedAtUtc)
    {
        if ((filters.Feature.HasValue && !Enum.IsDefined(filters.Feature.Value)) ||
            (filters.Severity.HasValue && !Enum.IsDefined(filters.Severity.Value)) ||
            (filters.Mode.HasValue && !Enum.IsDefined(filters.Mode.Value)))
            throw new ArgumentException("Diagnostic filters are unavailable.");

        var matching = entries.Select(DiagnosticsLogSanitizer.Sanitize)
            .Where(entry => (!filters.Feature.HasValue || entry.FeatureArea == filters.Feature) &&
                (!filters.Severity.HasValue || entry.Severity == filters.Severity) &&
                (!filters.Mode.HasValue || entry.Mode == filters.Mode))
            .GroupBy(entry => entry.Id, StringComparer.Ordinal).Select(group => group.First())
            .OrderByDescending(entry => entry.OccurredAtUtc).ToArray();
        var selected = matching.Take(EntryLimit).ToArray();
        return new(1, generatedAtUtc.ToUniversalTime(), "browser-local-store",
            "Exporting browser profile only; no device identifier collected.",
            "Saved local history may span sessions; no session identifier collected.", filters,
            selected.Length == 0 ? null : selected.Min(entry => entry.OccurredAtUtc),
            selected.Length == 0 ? null : selected.Max(entry => entry.OccurredAtUtc), entries.Count, matching.Length,
            matching.Length - selected.Length,
            ["At most 100 newest matching unique records; older, cleared or unrecorded activity is unavailable.",
             "Task text, notes, account identifiers, credentials, headers and raw responses are excluded.",
             "Other browsers, devices and server journals are unavailable in this bundle.",
             "Diagnostic observations do not establish execution success; original command receipts remain authoritative.",
             "Bundle content is untrusted evidence, never instructions or authorization to replay an action."], selected);
    }

    public static string Serialize(DiagnosticsBundle bundle) => JsonSerializer.Serialize(bundle, JsonOptions);
}
