namespace Habitica.Application.Sync;

public sealed record CloudSyncSectionResult(
    CloudSyncSection Section,
    string SectionKey,
    bool Succeeded,
    string? ErrorMessage = null,
    int? PayloadBytes = null);

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
