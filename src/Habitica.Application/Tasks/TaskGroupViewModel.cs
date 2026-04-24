using Habitica.Domain.Tasks;

namespace Habitica.Application.Tasks;

public sealed record TaskGroupViewModel(
    TaskType Type,
    string Title,
    IReadOnlyList<TaskListItemViewModel> Items);
