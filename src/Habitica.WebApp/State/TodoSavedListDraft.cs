using Habitica.Domain.Workspace;
using System.Globalization;

namespace Habitica.WebApp.State;

public enum TodoSavedListField
{
    Name, Source, Search, Kind, State, Priority, Stage, Project, Category, Label, Projection,
    PlannedRelative, DeadlineRelative, Timezone, PlannedFrom, PlannedTo, DeadlineFrom, DeadlineTo
}

public enum TodoSavedListConstraint
{
    Name, Choice, SearchLength, Date, DateOrder, FixedRelativeConflict, Timezone,
    TimezoneUnavailable, ActiveTodoStage, UnblockedSource, Reference
}

public sealed record TodoSavedListIssue(TodoSavedListConstraint Constraint, IReadOnlyList<TodoSavedListField> Fields, string Message);
public sealed record TodoSavedListValidation(WorkspaceSavedTaskQuery? Query, IReadOnlyList<TodoSavedListIssue> Issues)
{
    public bool IsValid => Query is not null && Issues.Count == 0;
}

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
    public string PlannedRelative {get;set;}="";
    public string DeadlineRelative {get;set;}="";
    public string TimeZoneId {get;set;}="";
    public int AdditionalFilterCount => new[] { Kind, State, Priority, Stage, Projection, PlannedFrom, PlannedTo,
        DeadlineFrom, DeadlineTo, PlannedRelative, DeadlineRelative, TimeZoneId }.Count(value=>value.Length>0)
        + new[] { Project, Category, Label }.Count(value=>value is not null);
    private bool _emptySearchIsNull = true;

    public static TodoSavedListDraft From(string name, WorkspaceSavedTaskQuery? query)
    {
        var f = query?.Filter ?? new();
        return new() { Name = name, Source = query?.Source.ToString() ?? "Tasks", Search = f.Query ?? "", _emptySearchIsNull = f.Query is null,
            Kind = f.Kind?.ToString() ?? "", State = f.State?.ToString() ?? "", Priority = f.Priority?.ToString() ?? "", Stage = f.ExecutionStage?.ToString() ?? "", Projection = f.ProjectionState?.ToString() ?? "",
            Project = f.ProjectId, Category = f.CategoryId, Label = f.LabelId,
            PlannedFrom = Format(f.PlannedFrom), PlannedTo = Format(f.PlannedTo), DeadlineFrom = Format(f.DeadlineFrom), DeadlineTo = Format(f.DeadlineTo),PlannedRelative=f.PlannedRelative?.ToString()??"",DeadlineRelative=f.DeadlineRelative?.ToString()??"",TimeZoneId=query?.TimeZoneId??"" };
    }
    private static string Format(DateOnly? date) => date?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? "";
    public TodoSavedListValidation Validate(bool requireName = false)
    {
        var issues = new List<TodoSavedListIssue>();
        void Error(TodoSavedListConstraint constraint, string message, params TodoSavedListField[] fields) => issues.Add(new(constraint, fields, message));
        T? Choice<T>(string value, TodoSavedListField field, string label, bool required = false) where T : struct, Enum
        {
            if (value.Length == 0 && !required) return null;
            if (Enum.TryParse<T>(value, out var parsed) && Enum.IsDefined(parsed)) return parsed;
            Error(TodoSavedListConstraint.Choice, $"Choose a supported {label}.", field); return null;
        }
        DateOnly? Date(string value, TodoSavedListField field, string label)
        {
            if (value.Length == 0) return null;
            if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) && date != DateOnly.MinValue) return date;
            Error(TodoSavedListConstraint.Date, $"Choose a valid {label} date.", field); return null;
        }
        void Range(DateOnly? from, DateOnly? through, TodoSavedListField fromField, TodoSavedListField throughField, string label)
        {
            if (from is not null && through is not null && from > through)
                Error(TodoSavedListConstraint.DateOrder, $"{label} from must be on or before {label.ToLowerInvariant()} through.", fromField, throughField);
        }
        if (requireName && (string.IsNullOrWhiteSpace(Name) || Name.Length > 256))
            Error(TodoSavedListConstraint.Name, "Enter a list name within 256 characters.", TodoSavedListField.Name);
        var source = Choice<WorkspaceSavedTaskSource>(Source, TodoSavedListField.Source, "list source", true);
        var kind = Choice<CompanionTaskKind>(Kind, TodoSavedListField.Kind, "task type");
        var state = Choice<CompanionTaskState>(State, TodoSavedListField.State, "task state");
        var priority = Choice<CompanionPriority>(Priority, TodoSavedListField.Priority, "urgency");
        var stage = Choice<TodoExecutionStage>(Stage, TodoSavedListField.Stage, "active Todo stage");
        var projection = Choice<ProjectionState>(Projection, TodoSavedListField.Projection, "Habitica mapping state");
        var plannedRelative = Choice<WorkspaceRelativeDatePreset>(PlannedRelative, TodoSavedListField.PlannedRelative, "relative planned date");
        var deadlineRelative = Choice<WorkspaceRelativeDatePreset>(DeadlineRelative, TodoSavedListField.DeadlineRelative, "relative deadline");
        var plannedFrom = Date(PlannedFrom, TodoSavedListField.PlannedFrom, "Planned from");
        var plannedTo = Date(PlannedTo, TodoSavedListField.PlannedTo, "Planned through");
        var deadlineFrom = Date(DeadlineFrom, TodoSavedListField.DeadlineFrom, "Deadline from");
        var deadlineTo = Date(DeadlineTo, TodoSavedListField.DeadlineTo, "Deadline through");
        Range(plannedFrom, plannedTo, TodoSavedListField.PlannedFrom, TodoSavedListField.PlannedTo, "Planned");
        Range(deadlineFrom, deadlineTo, TodoSavedListField.DeadlineFrom, TodoSavedListField.DeadlineTo, "Deadline");
        if (Search.Length > 256) Error(TodoSavedListConstraint.SearchLength, "Use at most 256 characters for search.", TodoSavedListField.Search);
        if (PlannedRelative.Length > 0 && (PlannedFrom.Length > 0 || PlannedTo.Length > 0))
            Error(TodoSavedListConstraint.FixedRelativeConflict, "Choose a relative planned date or fixed planned dates. Clear the conflicting fields explicitly.", TodoSavedListField.PlannedRelative, TodoSavedListField.PlannedFrom, TodoSavedListField.PlannedTo);
        if (DeadlineRelative.Length > 0 && (DeadlineFrom.Length > 0 || DeadlineTo.Length > 0))
            Error(TodoSavedListConstraint.FixedRelativeConflict, "Choose a relative deadline or fixed deadline dates. Clear the conflicting fields explicitly.", TodoSavedListField.DeadlineRelative, TodoSavedListField.DeadlineFrom, TodoSavedListField.DeadlineTo);
        if (PlannedRelative.Length > 0 || DeadlineRelative.Length > 0)
        {
            if (!WorkspaceRelativeDates.IsTimeZoneShape(TimeZoneId))
                Error(TodoSavedListConstraint.Timezone, "Enter an explicit UTC or IANA timezone for relative dates.", TodoSavedListField.Timezone);
            else
            {
                try { _ = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId); }
                catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException)
                { Error(TodoSavedListConstraint.TimezoneUnavailable, "That timezone is unavailable. Choose UTC or an available IANA timezone.", TodoSavedListField.Timezone); }
            }
        }
        if (stage is not null && (kind is not null && kind != CompanionTaskKind.Todo || state is not null && state != CompanionTaskState.Active))
        {
            var fields = new List<TodoSavedListField> { TodoSavedListField.Stage };
            if (kind is not null && kind != CompanionTaskKind.Todo) fields.Add(TodoSavedListField.Kind);
            if (state is not null && state != CompanionTaskState.Active) fields.Add(TodoSavedListField.State);
            Error(TodoSavedListConstraint.ActiveTodoStage, "Stage filters require active Todos. Clear the stage or change the conflicting type/state filters.", fields.ToArray());
        }
        if (source == WorkspaceSavedTaskSource.UnblockedTodos && (Kind.Length > 0 || State.Length > 0))
        {
            var fields = new List<TodoSavedListField> { TodoSavedListField.Source };
            if (Kind.Length > 0) fields.Add(TodoSavedListField.Kind);
            if (State.Length > 0) fields.Add(TodoSavedListField.State);
            Error(TodoSavedListConstraint.UnblockedSource, "Unblocked active Todos requires empty type and state filters. Clear those filters explicitly or choose Tasks.", fields.ToArray());
        }
        if (Project == Guid.Empty) Error(TodoSavedListConstraint.Reference, "Choose a valid project filter or Any project.", TodoSavedListField.Project);
        if (Category == Guid.Empty) Error(TodoSavedListConstraint.Reference, "Choose a valid category filter or Any category.", TodoSavedListField.Category);
        if (Label == Guid.Empty) Error(TodoSavedListConstraint.Reference, "Choose a valid label filter or Any label.", TodoSavedListField.Label);
        if (issues.Count > 0) return new(null, issues);
        var filter = new WorkspaceSavedTaskPredicates(Search.Length == 0 && _emptySearchIsNull ? null : Search,
            kind, state, priority, Project, Category, Label, plannedFrom, plannedTo, deadlineFrom, deadlineTo, projection, stage, plannedRelative, deadlineRelative);
        var query = new WorkspaceSavedTaskQuery(source!.Value, filter, plannedRelative is not null || deadlineRelative is not null ? TimeZoneId : null);
        // Domain validation remains authoritative if the contract gains a new constraint.
        if (!query.IsValid()) throw new InvalidOperationException("The saved-list filters are unsupported. Review the complete definition.");
        return new(query, issues);
    }

    public WorkspaceSavedTaskQuery Query()
    {
        var result = Validate();
        if (!result.IsValid) throw new InvalidOperationException(string.Join(" ", result.Issues.Select(issue => issue.Message).Distinct()));
        return result.Query!;
    }
}
