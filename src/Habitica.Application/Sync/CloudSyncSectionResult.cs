namespace Habitica.Application.Sync;

public enum CloudSyncDirection
{
    Upload,
    Download,
    Metadata
}

public enum CloudSyncSectionStatusKind
{
    Pending,
    Succeeded,
    Failed,
    Skipped,
    Excluded,
    Conflict
}

public enum CloudSyncSectionImportDecision
{
    KeepLocal,
    Merge,
    UseRemote
}

public sealed record CloudSyncSectionResult(
    CloudSyncSection Section,
    string SectionKey,
    bool Succeeded,
    string? ErrorMessage = null,
    int? PayloadBytes = null,
    CloudSyncSectionStatusKind Status = CloudSyncSectionStatusKind.Succeeded);

public sealed record CloudSyncSectionStatus(
    CloudSyncSection Section,
    string SectionKey,
    CloudSyncDirection Direction,
    CloudSyncSectionStatusKind Status,
    DateTimeOffset UpdatedAtUtc,
    int? PayloadBytes = null,
    string? Message = null);

public sealed record CloudSyncUploadReport(
    IReadOnlyList<CloudSyncSectionResult> SectionResults,
    bool MergedRemoteData,
    int SucceededCount,
    int FailedCount)
{
    public bool IsPartial => FailedCount > 0 && SucceededCount > 0;

    public bool IsFullSuccess => FailedCount == 0 && SucceededCount > 0;
}

public sealed record CloudSyncMetadata(
    int SchemaVersion,
    DateTimeOffset UploadedAtUtc,
    IReadOnlyList<string> UploadedSections,
    IReadOnlyList<string> FailedSections);
