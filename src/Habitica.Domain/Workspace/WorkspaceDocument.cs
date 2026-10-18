using System.Text.Json.Serialization;

namespace Habitica.Domain.Workspace;

public enum CompanionTaskKind { Todo, Daily, Habit }
public enum CompanionTaskState { Active, Completed, Waiting, Someday, Paused, Cancelled }
public enum CompanionPriority { Low, Normal, High, Urgent }
public enum RecurrenceFrequency { Daily, Weekly, Monthly }
public enum OccurrenceState { Pending, Completed, Skipped }
public enum HabitScoreDirection { Up, Down }
public enum ProjectionState { Unlinked, Pending, Linked, ExternalConflict, UnknownOutcome }
public enum HabiticaTaskAttribute { Str, Int, Per, Con }
public sealed record TaskChecklistItem(Guid Id, string Text, bool Completed = false);
public sealed record HabiticaTaskDetails(double? Difficulty = null, HabiticaTaskAttribute? Attribute = null, IReadOnlyList<TaskChecklistItem>? Checklist = null)
{
    public static bool IsValid(HabiticaTaskDetails? value) => value is null ||
        (value.Difficulty is null or 0.1 or 1 or 1.5 or 2 && (value.Attribute is null || Enum.IsDefined(value.Attribute.Value))
        && (value.Checklist is null || value.Checklist.Count <= 100 && value.Checklist.All(item => item is not null && item.Id != Guid.Empty && !string.IsNullOrWhiteSpace(item.Text) && item.Text.Length <= 256)
            && value.Checklist.Select(item => item.Id).Distinct().Count() == value.Checklist.Count));

    public static HabiticaTaskDetails? Merge(HabiticaTaskDetails? current, HabiticaTaskDetails? patch) => patch is null ? current :
        new(patch.Difficulty ?? current?.Difficulty, patch.Attribute ?? current?.Attribute, patch.Checklist ?? current?.Checklist);
}

public abstract record WorkspaceEntity(Guid Id, long Revision, bool IsArchived);
public sealed record WorkspaceCategory(Guid Id, long Revision, string Name, bool IsArchived = false) : WorkspaceEntity(Id, Revision, IsArchived);
public sealed record WorkspaceProject(Guid Id, long Revision, string Name, Guid? CategoryId = null, bool IsArchived = false) : WorkspaceEntity(Id, Revision, IsArchived);
public sealed record WorkspaceSection(Guid Id, long Revision, string Name, Guid ProjectId, int Position = 0, bool IsArchived = false) : WorkspaceEntity(Id, Revision, IsArchived);
public sealed record WorkspaceLabel(Guid Id, long Revision, string Name, bool IsArchived = false) : WorkspaceEntity(Id, Revision, IsArchived);
public sealed record WorkspaceGoal(Guid Id, long Revision, string Name, IReadOnlyList<Guid> TaskIds, bool IsArchived = false) : WorkspaceEntity(Id, Revision, IsArchived);

public sealed record RecurrenceRule(
    [property: JsonRequired] RecurrenceFrequency Frequency,
    int Interval,
    DateOnly StartDate,
    string TimeZoneId,
    IReadOnlyList<DayOfWeek> Weekdays,
    IReadOnlyList<int> DaysOfMonth);

public sealed record CompanionTask(
    Guid Id,
    long Revision,
    [property: JsonRequired] string Text,
    [property: JsonRequired] CompanionTaskKind Kind,
    [property: JsonRequired] CompanionTaskState State,
    [property: JsonRequired] CompanionPriority Priority,
    [property: JsonRequired] DateTimeOffset CreatedAtUtc,
    [property: JsonRequired] DateTimeOffset UpdatedAtUtc,
    string? Notes = null,
    Guid? CategoryId = null,
    Guid? ProjectId = null,
    Guid? SectionId = null,
    Guid? ParentTaskId = null,
    IReadOnlyList<Guid>? LabelIds = null,
    IReadOnlyList<Guid>? DependsOn = null,
    DateOnly? PlannedDate = null,
    DateOnly? Deadline = null,
    DateTimeOffset? ReviewAfterUtc = null,
    DateTimeOffset? StaleSinceUtc = null,
    RecurrenceRule? Recurrence = null,
    bool IsArchived = false,
    HabiticaTaskDetails? HabiticaDetails = null) : WorkspaceEntity(Id, Revision, IsArchived);

public sealed record TaskOccurrence(Guid Id, long Revision, Guid TaskId, DateOnly Date, OccurrenceState State, bool IsArchived = false) : WorkspaceEntity(Id, Revision, IsArchived);
public sealed record CompletionEvent(Guid Id, long Revision, Guid TaskId, Guid CommandId, DateTimeOffset CompletedAtUtc, Guid? OccurrenceId = null, bool IsArchived = false) : WorkspaceEntity(Id, Revision, IsArchived);
public sealed record TaskRestoration(Guid Id, long Revision, Guid TaskId, Guid CompletionId, Guid CommandId, DateTimeOffset RestoredAtUtc, bool IsArchived = false) : WorkspaceEntity(Id, Revision, IsArchived);
public sealed record HabitScoreEvent(Guid Id, long Revision, Guid TaskId, Guid CommandId, DateTimeOffset RecordedAtUtc, HabitScoreDirection Direction, bool IsArchived = false) : WorkspaceEntity(Id, Revision, IsArchived);
public sealed record HabiticaProjection(Guid Id, long Revision, Guid TaskId, ProjectionState State, string? HabiticaTaskId = null, bool IsArchived = false, string? Fingerprint = null, string? CorrelationAlias = null) : WorkspaceEntity(Id, Revision, IsArchived);

public sealed record WorkspaceDocument
{
    public const int CurrentSchemaVersion = 1;
    public const long MaxRevision = 9007199254740991;
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required Guid Id { get; init; }
    public long Revision { get; init; } = 1;
    public IReadOnlyList<WorkspaceCategory> Categories { get; init; } = Array.Empty<WorkspaceCategory>();
    public IReadOnlyList<WorkspaceProject> Projects { get; init; } = Array.Empty<WorkspaceProject>();
    public IReadOnlyList<WorkspaceSection> Sections { get; init; } = Array.Empty<WorkspaceSection>();
    public IReadOnlyList<WorkspaceLabel> Labels { get; init; } = Array.Empty<WorkspaceLabel>();
    public IReadOnlyList<WorkspaceGoal> Goals { get; init; } = Array.Empty<WorkspaceGoal>();
    public IReadOnlyList<CompanionTask> Tasks { get; init; } = Array.Empty<CompanionTask>();
    public IReadOnlyList<TaskOccurrence> Occurrences { get; init; } = Array.Empty<TaskOccurrence>();
    public IReadOnlyList<CompletionEvent> Completions { get; init; } = Array.Empty<CompletionEvent>();
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<TaskRestoration>? Restorations { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<WorkspaceOrganizationEntry>? OrganizationHistory { get; init; }
    public IReadOnlyList<HabitScoreEvent> HabitScores { get; init; } = Array.Empty<HabitScoreEvent>();
    public IReadOnlyList<HabiticaProjection> Projections { get; init; } = Array.Empty<HabiticaProjection>();
}
