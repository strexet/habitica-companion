using System.Globalization;
using System.Text.Json.Serialization;

namespace Habitica.Domain.Workspace;

public enum TaskReviewAction { Set, Snooze, Clear, MarkReviewed, SetStale, ClearStale }
public sealed record WorkspaceTaskReview([property: JsonRequired] TaskReviewAction Action, [property: JsonRequired] string TimeZoneId, DateTimeOffset? ReviewAfterUtc = null, DateTimeOffset? StaleSinceUtc = null)
{
    public bool IsValid()
    {
        if (!Enum.IsDefined(Action) || !ValidZone(TimeZoneId)) return false;
        static bool Instant(DateTimeOffset? value) => value is {} time && time != default && time.Offset == TimeSpan.Zero;
        return Action is TaskReviewAction.Set or TaskReviewAction.Snooze ? Instant(ReviewAfterUtc) && StaleSinceUtc is null
            : Action == TaskReviewAction.SetStale ? Instant(StaleSinceUtc) && ReviewAfterUtc is null : ReviewAfterUtc is null && StaleSinceUtc is null;
    }
    public static bool ValidZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || id != "UTC" && !id.Contains('/')) return false;
        try { TimeZoneInfo.FindSystemTimeZoneById(id); return true; }
        catch (TimeZoneNotFoundException) { return false; }
        catch (InvalidTimeZoneException) { return false; }
    }
    public bool CanApply(CompanionTask task, DateTimeOffset now) => IsValid() && !task.IsArchived
        && (Action != TaskReviewAction.Snooze || task.ReviewAfterUtc is {} current && ReviewAfterUtc > current && ReviewAfterUtc > now)
        && (Action != TaskReviewAction.SetStale || StaleSinceUtc <= now);
    public CompanionTask Apply(CompanionTask task) => Action switch
    {
        TaskReviewAction.Set or TaskReviewAction.Snooze => task with { ReviewAfterUtc = ReviewAfterUtc },
        TaskReviewAction.Clear or TaskReviewAction.MarkReviewed => task with { ReviewAfterUtc = null },
        TaskReviewAction.SetStale => task with { StaleSinceUtc = StaleSinceUtc },
        TaskReviewAction.ClearStale => task with { StaleSinceUtc = null },
        _ => throw new InvalidOperationException("Invalid review action.")
    };
    public static string? CanonicalUtc(DateTimeOffset? value) => value?.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);
}
public sealed record TaskReviewEvent(Guid Id, long Revision, Guid TaskId, Guid CommandId, [property: JsonRequired] TaskReviewAction Action, [property: JsonRequired] DateTimeOffset RecordedAtUtc, [property: JsonRequired] string TimeZoneId, DateTimeOffset? ReviewAfterUtc = null, DateTimeOffset? StaleSinceUtc = null, bool IsArchived = false) : WorkspaceEntity(Id, Revision, IsArchived);
