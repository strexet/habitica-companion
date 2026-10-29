using Habitica.Domain.Workspace;

namespace Habitica.WebApp.State;

public sealed class TodoSavedListDraft
{
    public string Name { get; set; } = "";
    public string Source { get; set; } = "Tasks";
    public string Search { get; set; } = "";
    public string Kind { get; set; } = "";
    public string State { get; set; } = "";
    public string Priority { get; set; } = "";
    public string Stage { get; set; } = "";
    public string Projection { get; set; } = "";
    public Guid? Project { get; set; }
    public Guid? Category { get; set; }
    public Guid? Label { get; set; }
    public string PlannedFrom { get; set; } = "";
    public string PlannedTo { get; set; } = "";
    public string DeadlineFrom { get; set; } = "";
    public string DeadlineTo { get; set; } = "";
    private bool _emptySearchIsNull = true;

    public static TodoSavedListDraft From(string name, WorkspaceSavedTaskQuery? query)
    {
        var f = query?.Filter ?? new();
        return new() { Name = name, Source = query?.Source.ToString() ?? "Tasks", Search = f.Query ?? "", _emptySearchIsNull = f.Query is null,
            Kind = f.Kind?.ToString() ?? "", State = f.State?.ToString() ?? "", Priority = f.Priority?.ToString() ?? "", Stage = f.ExecutionStage?.ToString() ?? "", Projection = f.ProjectionState?.ToString() ?? "",
            Project = f.ProjectId, Category = f.CategoryId, Label = f.LabelId,
            PlannedFrom = Format(f.PlannedFrom), PlannedTo = Format(f.PlannedTo), DeadlineFrom = Format(f.DeadlineFrom), DeadlineTo = Format(f.DeadlineTo) };
    }
    private static string Format(DateOnly? date) => date?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? "";
    private static T? Choice<T>(string value) where T : struct, Enum => value.Length == 0 ? null
        : Enum.TryParse<T>(value, out var parsed) && Enum.IsDefined(parsed) ? parsed : throw new InvalidOperationException("Choose a supported saved-list filter.");
    public WorkspaceSavedTaskQuery Query()
    {
        var source = Choice<WorkspaceSavedTaskSource>(Source) ?? throw new InvalidOperationException("Choose a saved-list source.");
        var filter = new WorkspaceSavedTaskPredicates(Search.Length == 0 && _emptySearchIsNull ? null : Search,
            Choice<CompanionTaskKind>(Kind), Choice<CompanionTaskState>(State), Choice<CompanionPriority>(Priority), Project, Category, Label,
            TodoTaskDraft.Date(PlannedFrom), TodoTaskDraft.Date(PlannedTo), TodoTaskDraft.Date(DeadlineFrom), TodoTaskDraft.Date(DeadlineTo), Choice<ProjectionState>(Projection), Choice<TodoExecutionStage>(Stage));
        var query = new WorkspaceSavedTaskQuery(source, filter);
        if (!query.IsValid()) throw new InvalidOperationException("Use fixed ordered date ranges, up to 256 search characters and supported filters. Stage filters require active Todos. Unblocked Todos source requires empty type and state filters.");
        return query;
    }
}
