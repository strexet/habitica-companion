using Habitica.Domain.Tasks;

namespace Habitica.Application.Tasks;

public sealed record TaskListItemViewModel(
    string Id,
    string Text,
    TaskType Type,
    bool IsCompleted,
    decimal Difficulty,
    string? Notes,
    DateTimeOffset? DueDate,
    decimal? Value,
    bool IsChallengeTask,
    bool? SupportsPositiveScore,
    bool? SupportsNegativeScore);
