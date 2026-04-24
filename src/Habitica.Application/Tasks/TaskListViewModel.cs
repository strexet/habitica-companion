namespace Habitica.Application.Tasks;

public sealed record TaskListViewModel(
    IReadOnlyList<TaskGroupViewModel> Groups,
    int TotalVisibleTasks);
