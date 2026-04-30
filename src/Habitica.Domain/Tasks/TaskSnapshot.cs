namespace Habitica.Domain.Tasks;

public sealed record TaskSnapshot(
    string Id,
    string Text,
    TaskType Type,
    bool IsCompleted,
    decimal Difficulty,
    string? Notes,
    DateTimeOffset? DueDate,
    decimal? Value = null);
