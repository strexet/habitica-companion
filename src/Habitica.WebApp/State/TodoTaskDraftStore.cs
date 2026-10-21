using System.Globalization;
using Habitica.Domain.Workspace;

namespace Habitica.WebApp.State;

public sealed class TodoTaskDraft
{
    public DailyCreationDraft DailyCreation { get; } = new();
    public string Text { get; set; } = "";
    public string Notes { get; set; } = "";
    public CompanionPriority Priority { get; set; } = CompanionPriority.Normal;
    public string Deadline { get; set; } = "";
    public string PlannedDate { get; set; } = "";
    public string Difficulty { get; set; } = "";
    public string Attribute { get; set; } = "";
    public string Checklist { get; set; } = "";
    public bool ReplaceChecklist { get; set; }
    public bool HabitUp { get; set; } = true;
    public bool HabitDown { get; set; } = true;
    public long? TaskRevision { get; set; }

    public static TodoTaskDraft From(CompanionTask? task) => task is null ? new() : new()
    {
        Text = task.Text, Notes = task.Notes ?? "", Priority = task.Priority,
        Deadline = task.Deadline?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
        PlannedDate = task.PlannedDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
        Difficulty = task.HabiticaDetails?.Difficulty?.ToString(CultureInfo.InvariantCulture) ?? "",
        Attribute = task.HabiticaDetails?.Attribute?.ToString() ?? "",
        Checklist = string.Join('\n', task.HabiticaDetails?.Checklist?.Select(item => item.Text) ?? []), TaskRevision = task.Revision
    };
    public void RequireCurrent(CompanionTask? current)
    {
        if (TaskRevision is not null && (current is null || current.Revision != TaskRevision))
            throw new InvalidOperationException("The task changed after this draft opened. Reload the current task before replacing saved fields.");
    }
    public WorkspaceTaskInput Basic(CompanionTaskKind kind, CompanionTask? current)
    {
        RequireCurrent(current);
        if (string.IsNullOrWhiteSpace(Text) || Text.Length > 2048 || Notes.Length > 10000)
            throw new InvalidOperationException("Enter a task title and keep the title and notes within the displayed limits.");
        double? difficulty = Difficulty.Length == 0 ? null : double.TryParse(Difficulty, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number)
            && number is 0.1 or 1 or 1.5 or 2 ? number : throw new InvalidOperationException("Choose a supported Habitica difficulty.");
        HabiticaTaskAttribute? attribute = Attribute.Length == 0 ? null : Enum.TryParse<HabiticaTaskAttribute>(Attribute, out var parsed) && Enum.IsDefined(parsed)
            ? parsed : throw new InvalidOperationException("Choose a supported Habitica attribute.");
        IReadOnlyList<TaskChecklistItem>? checklist = null;
        if (ReplaceChecklist)
        {
            var prior = current?.HabiticaDetails?.Checklist ?? [];
            checklist = Checklist.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select((text, index) => index < prior.Count && prior[index].Text == text ? prior[index] : new TaskChecklistItem(Guid.NewGuid(), text)).ToArray();
        }
        var details = difficulty is null && attribute is null && checklist is null ? null : new HabiticaTaskDetails(difficulty, attribute, checklist);
        if (current is null && kind == CompanionTaskKind.Habit) details = new(difficulty, attribute, null, HabitUp, HabitDown);
        if (!HabiticaTaskDetails.IsValid(details)) throw new InvalidOperationException("Use at most 100 checklist items, each up to 256 characters.");
        var operation = current is not null ? WorkspaceOperation.UpdateTask : kind switch
        { CompanionTaskKind.Daily => WorkspaceOperation.CreateDaily, CompanionTaskKind.Habit => WorkspaceOperation.CreateHabit, _ => WorkspaceOperation.CreateTodo };
        return new(operation, current?.Id, Text, Notes, Priority, Date(Deadline), details,
            true, HabitScoreDirection.Up, 1, null, Recurrence: operation == WorkspaceOperation.CreateDaily ? DailyCreation.Rule() : null);
    }
    public static DateOnly? Date(string value) => value.Length == 0 ? null
        : DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) && date != DateOnly.MinValue
            ? date : throw new InvalidOperationException("Enter a valid date in year-month-day format.");
}

/// <summary>Memory-only drafts survive route changes and clear when the account changes.</summary>
public sealed class TodoTaskDraftStore : IDisposable
{
    private readonly IAppSessionController _session;
    private string _account;
    private readonly Dictionary<(TodoWorkspaceSelection Source, CompanionTaskKind Kind, Guid? TaskId), TodoTaskDraft> _drafts = [];
    private readonly Dictionary<Guid, (TodoTaskDraft Draft, bool Creating)> _prepared = [];
    private readonly Dictionary<(TodoWorkspaceSelection Source, CompanionTaskKind Kind), Guid?> _editing = [];
    private readonly Dictionary<(TodoWorkspaceSelection Source, Guid TaskId), string> _dates = [];
    public TodoTaskDraftStore(IAppSessionController session) { _session = session; _account = Account; _session.Changed += SessionChanged; }
    private string Account => _session.State.IsAuthenticated && Guid.TryParse(_session.State.UserId, out var id) ? id.ToString("D") : "";
    public Guid? Editing(TodoWorkspaceSelection source, CompanionTaskKind kind) => _editing.GetValueOrDefault((source, kind));
    public void Edit(TodoWorkspaceSelection source, CompanionTaskKind kind, Guid? taskId) => _editing[(source, kind)] = taskId;
    public string OccurrenceDate(TodoWorkspaceSelection source, Guid taskId) => _dates.GetValueOrDefault((source, taskId)) ?? "";
    public void OccurrenceDate(TodoWorkspaceSelection source, Guid taskId, string value) => _dates[(source, taskId)] = value;
    public TodoTaskDraft Get(TodoWorkspaceSelection source, CompanionTaskKind kind, CompanionTask? task)
    {
        var key = (source, kind, task?.Id);
        if (!_drafts.TryGetValue(key, out var draft)) _drafts[key] = draft = TodoTaskDraft.From(task);
        return draft;
    }
    public void Reload(TodoWorkspaceSelection source, CompanionTaskKind kind, CompanionTask? task) => _drafts[(source, kind, task?.Id)] = TodoTaskDraft.From(task);
    public void Prepared(Guid commandId, TodoTaskDraft draft, bool creating) => _prepared[commandId] = (draft, creating);
    public void Cancelled(Guid commandId) => _prepared.Remove(commandId);
    public void Saved(Guid commandId, CompanionTask? saved)
    {
        if (!_prepared.Remove(commandId, out var owner)) return;
        if (owner.Creating)
        {
            foreach (var key in _drafts.Where(pair => ReferenceEquals(pair.Value, owner.Draft)).Select(pair => pair.Key).ToArray()) _drafts.Remove(key);
        }
        else if (saved is not null) owner.Draft.TaskRevision = saved.Revision;
    }
    private void SessionChanged() { if (_account != Account) { _account = Account; _drafts.Clear(); _prepared.Clear(); _editing.Clear(); _dates.Clear(); } }
    public void Dispose() => _session.Changed -= SessionChanged;
}
