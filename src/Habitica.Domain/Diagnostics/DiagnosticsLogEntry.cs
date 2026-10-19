namespace Habitica.Domain.Diagnostics;

public sealed record DiagnosticsLogEntry(
    string Id,
    DateTimeOffset OccurredAtUtc,
    DiagnosticsFeatureArea FeatureArea,
    string Operation,
    DiagnosticsSeverity Severity,
    DiagnosticsMode Mode,
    string Message,
    IReadOnlyDictionary<string, string> Metadata,
    DiagnosticsContext? Context = null);

public enum DiagnosticsResult { Success, Refused, Unavailable, Unknown, Cancelled, AccountChanged, Observed }

public sealed record DiagnosticsContext(DiagnosticsResult Result, long DurationMs = 0, Guid? CorrelationId = null,
    Guid? CommandId = null, int? AcceptedCount = null, int? ConfirmedCount = null, int? RequestedCount = null);

public enum DiagnosticsFeatureArea
{
    Auth,
    Sync,
    Tasks,
    Inventory,
    Party,
    Diagnostics,
    Equipment,
    Skills,
    Workspace
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
