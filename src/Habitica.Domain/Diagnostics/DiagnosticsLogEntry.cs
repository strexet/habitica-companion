namespace Habitica.Domain.Diagnostics;

public sealed record DiagnosticsLogEntry(
    string Id,
    DateTimeOffset OccurredAtUtc,
    DiagnosticsFeatureArea FeatureArea,
    string Operation,
    DiagnosticsSeverity Severity,
    DiagnosticsMode Mode,
    string Message,
    IReadOnlyDictionary<string, string> Metadata);

public enum DiagnosticsFeatureArea
{
    Auth,
    Sync,
    Tasks,
    Inventory,
    Party,
    Diagnostics,
    Equipment,
    Skills
}

public enum DiagnosticsSeverity
{
    Info,
    Success,
    Warning,
    Error
}

public enum DiagnosticsMode
{
    Local,
    LiveRead,
    LiveMutation,
    ReversibleTest
}
