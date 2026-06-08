using Habitica.Domain.Tasks;

namespace Habitica.Application.Tasks;

public sealed record TaskListFilter(
    string? SearchText = null,
    bool IncludeCompleted = true,
    IReadOnlyCollection<TaskType>? SelectedTypes = null,
    TaskListSortMode SortMode = TaskListSortMode.Habitica);

public enum TaskListSortMode
{
    Habitica,
    Name,
    ValueHigh,
    ValueLow,
    DueSoon
}
