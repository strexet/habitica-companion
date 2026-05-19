namespace Habitica.Application.Tasks;

public sealed record TaskListItemViewModel(
    string Id,
    string Text,
    bool IsCompleted,
    decimal Difficulty,
    string? Notes,
    DateTimeOffset? DueDate,
    decimal? Value);
