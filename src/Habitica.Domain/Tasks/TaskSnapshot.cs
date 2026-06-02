namespace Habitica.Domain.Tasks;

public sealed record TaskSnapshot(
    string Id,
    string Text,
    TaskType Type,
    bool IsCompleted,
    decimal Difficulty,
    string? Notes,
    DateTimeOffset? DueDate,
    decimal? Value = null,
    bool IsChallengeTask = false,
    bool? SupportsPositiveScore = null,
    bool? SupportsNegativeScore = null,
    IReadOnlyList<TaskHistoryPoint>? History = null,
    bool? IsDue = null)
{
    public IReadOnlyList<TaskHistoryPoint> HistoryPoints => History ?? Array.Empty<TaskHistoryPoint>();
}

public sealed record TaskHistoryPoint(
    DateTimeOffset Date,
    decimal Value);

public enum TaskScoreDirection
{
    Up,
    Down
}
