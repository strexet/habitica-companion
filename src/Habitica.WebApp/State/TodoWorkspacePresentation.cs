using Habitica.Application.Workspace;
using Habitica.Domain.Sync;
using Habitica.Domain.Workspace;
using Habitica.Storage;

namespace Habitica.WebApp.State;

public enum TodoWorkspaceSource { Browser, Cloud }
public enum TodoWorkspaceAvailability { SignedOut, NeedsSelection, Loading, Available, Unavailable }
public sealed record TodoWorkspaceSelection(TodoWorkspaceSource Source, Guid WorkspaceId);
public sealed record TodoTaskProvenance(Guid WorkspaceId, long WorkspaceRevision, Guid TaskId,
    TodoWorkspaceSource Source, ProjectionState MappingState, string? HabiticaTaskId);
public sealed record TodoHabiticaCacheContext(string Source, SnapshotFreshnessState Freshness, DateTimeOffset? RetrievedAtUtc);

/// <summary>Scoped selection and reads; WorkspaceTaskController retains all task command ownership.</summary>
public sealed class TodoWorkspacePresentation : IDisposable
{
    private readonly IAppSessionController _session;
    private readonly IWorkspaceCloudClient _cloud;
    private readonly IndexedDbWorkspaceDatabase _database;
    private readonly WorkspaceTaskController _commands;
    private string? _account;
    private long _generation;
    private bool _unavailable;
    private bool _loading;
    private WorkspaceJournalSnapshot? _validated;
    public TodoWorkspacePresentation(IAppSessionController session, IWorkspaceCloudClient cloud,
        IndexedDbWorkspaceDatabase database, WorkspaceTaskController commands)
    {
        _session = session; _cloud = cloud; _database = database; _commands = commands;
        _account = Account; AdoptLoadedSelection(); _session.Changed += SessionChanged;
    }

    public event Action? Changed;
    public TodoWorkspaceSelection? Selection { get; private set; }
    public IReadOnlyList<TodoWorkspaceSelection> Sources { get; private set; } = [];
    public bool? BrowserSourcesAvailable { get; private set; }
    public bool IsBusy { get; private set; }
    public string? Message { get; private set; }
    public bool CanSwitchSource => !IsBusy && !_commands.HasPendingIntent;
    public WorkspaceTaskController Commands => _commands;
    private string? Account => _session.State.IsAuthenticated && Guid.TryParse(_session.State.UserId, out var id) ? id.ToString("D") : null;
    public TodoWorkspaceAvailability Availability => Account is null ? TodoWorkspaceAvailability.SignedOut
        : _loading ? TodoWorkspaceAvailability.Loading : Selection is null ? BrowserSourcesAvailable == false && Sources.Count == 0 ? TodoWorkspaceAvailability.Unavailable : TodoWorkspaceAvailability.NeedsSelection
        : Snapshot is null ? TodoWorkspaceAvailability.Unavailable : TodoWorkspaceAvailability.Available;
    public WorkspaceJournalSnapshot? Snapshot
    {
        get
        {
            if (Account is null || Account != _account || _unavailable || _commands.RequiresReload || Selection is null) return null;
            var snapshot = _commands.Snapshot;
            if (snapshot is null || snapshot.Document.Id != Selection.WorkspaceId || _commands.IsCloud != (Selection.Source == TodoWorkspaceSource.Cloud)) return null;
            if (!ReferenceEquals(snapshot, _validated))
            {
                if (WorkspaceRules.Validate(snapshot.Document).Count != 0) return null;
                _validated = snapshot;
            }
            return snapshot;
        }
    }
    public long? Revision => Snapshot?.Document.Revision;
    public bool IsReadOnly => Snapshot is null || _commands.IsReadOnly;
    public TodoHabiticaCacheContext HabiticaCache => new("Separate Habitica cache; not merged into Companion tasks",
        Account is null ? SnapshotFreshnessState.Missing : _session.State.TaskFreshness,
        Account is null ? null : _session.State.TaskSnapshot?.RetrievedAtUtc);

    public Task RefreshSourcesAsync(CancellationToken token = default) => RunAsync(async () =>
    {
        var scope = Account!; var generation = _generation;
        IReadOnlyList<Guid> ids;
        var browserAvailable = true;
        try { ids = await _database.ListAsync(scope, token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch { ids = []; browserAvailable = false; }
        if (!Current(scope, generation)) return;
        token.ThrowIfCancellationRequested();
        BrowserSourcesAvailable = browserAvailable;
        Sources = ids.Distinct().Order().Select(id => new TodoWorkspaceSelection(TodoWorkspaceSource.Browser, id))
            .Concat(_cloud.WorkspaceId is { } cloudId ? [new(TodoWorkspaceSource.Cloud, cloudId)] : Array.Empty<TodoWorkspaceSelection>()).ToArray();
        AdoptLoadedSelection();
        Message = !browserAvailable ? "Browser workspaces could not be listed. Existing cloud access remains separate; retry browser storage when available."
            : Selection is null ? "Choose the exact workspace. Browser and cloud records remain independent." : _commands.Message;
    }, token, preserveUnavailable: true);

    public Task SelectAsync(TodoWorkspaceSelection selection, CancellationToken token = default)
    {
        if (!CanSwitchSource) throw new InvalidOperationException("Resolve the original command before switching workspace sources.");
        if (selection.WorkspaceId == Guid.Empty || !Enum.IsDefined(selection.Source) || !Sources.Contains(selection))
            throw new InvalidOperationException("Refresh sources and choose an available workspace.");
        Selection = selection;
        return LoadSelectedAsync(token);
    }

    public Task LoadSelectedAsync(CancellationToken token = default) => RunAsync(async () =>
    {
        AdoptLoadedSelection();
        var selected = Selection ?? throw new InvalidOperationException("Choose a workspace before loading Todo data.");
        if (selected.Source == TodoWorkspaceSource.Cloud && _cloud.WorkspaceId != selected.WorkspaceId)
            throw new InvalidOperationException("The selected cloud workspace is unavailable. Keep the original command reference and renew access in Settings.");
        await _commands.LoadAsync(selected.Source == TodoWorkspaceSource.Cloud, selected.Source == TodoWorkspaceSource.Browser ? selected.WorkspaceId : null, token);
    }, token, loading: true);

    public Task CreateBrowserAsync(CancellationToken token = default)
    {
        if (!CanSwitchSource) throw new InvalidOperationException("Resolve the original command before creating another workspace.");
        return RunAsync(async () => { await _commands.CreateLocalAsync(token); AdoptLoadedSelection(replace: true); }, token, loading: true);
    }

    public Task PrepareAsync(WorkspaceTaskInput input, CancellationToken token = default)
    {
        _ = RequireSnapshot();
        return RunAsync(() => _commands.PrepareAsync(input, token), token);
    }
    public Task ConfirmAsync(CancellationToken token = default) => RunAsync(() => _commands.ConfirmAsync(token), token);
    public Task RetryOriginalPreviewAsync(CancellationToken token = default) => RunAsync(() => _commands.RetryOriginalPreviewAsync(token), token);
    public Task CheckOriginalAsync(CancellationToken token = default) => RunAsync(() => _commands.CheckOriginalAsync(token), token);
    public void CancelPreview() { if (IsBusy) throw new InvalidOperationException("Wait for the current operation."); _commands.CancelPreview(); Changed?.Invoke(); }

    public WorkspaceTaskPage Tasks(WorkspaceTaskFilter filter) => WorkspaceTaskQuery.Evaluate(RequireSnapshot().Document, filter);
    public WorkspaceAgendaPage Agenda(WorkspaceAgendaFilter filter) => WorkspaceAgenda.Evaluate(RequireSnapshot().Document, filter);
    public WorkspaceReadPage<WorkspaceCompletionItem> History(WorkspaceHistoryFilter filter) => WorkspaceCompletionHistory.Evaluate(RequireSnapshot(), filter);
    public WorkspaceReadPage<WorkspaceCategory> Categories(WorkspaceReadFilter filter) => WorkspaceOrganization.Categories(RequireSnapshot().Document, filter);
    public WorkspaceReadPage<WorkspaceProject> Projects(WorkspaceReadFilter filter, Guid? categoryId = null) => WorkspaceOrganization.Projects(RequireSnapshot().Document, filter, categoryId);
    public WorkspaceReadPage<WorkspaceSection> Sections(WorkspaceReadFilter filter, Guid projectId) => WorkspaceOrganization.Sections(RequireSnapshot().Document, filter, projectId);
    public WorkspaceReadPage<WorkspaceLabel> Labels(WorkspaceReadFilter filter) => WorkspaceOrganization.Labels(RequireSnapshot().Document, filter);
    public WorkspaceReadPage<WorkspaceGoalSummary> Goals(WorkspaceReadFilter filter) => WorkspaceOrganization.Goals(RequireSnapshot().Document, filter);
    public WorkspaceTaskPage SavedView(Guid viewId, WorkspaceReadFilter filter) => WorkspaceSavedTaskViews.Evaluate(RequireSnapshot().Document, viewId, filter);
    public TodoTaskProvenance Provenance(Guid taskId)
    {
        var document = RequireSnapshot().Document;
        if (!document.Tasks.Any(task => task.Id == taskId)) throw new InvalidOperationException("Select an exact Companion task identity.");
        var mapping = document.Projections.SingleOrDefault(value => value.TaskId == taskId && !value.IsArchived);
        return new(document.Id, document.Revision, taskId, Selection!.Source, mapping?.State ?? ProjectionState.Unlinked, mapping?.HabiticaTaskId);
    }

    private WorkspaceJournalSnapshot RequireSnapshot() => Snapshot ?? throw new InvalidOperationException("Todo workspace data is unavailable. Select or reload the source.");
    private bool Current(string scope, long generation) => generation == _generation && scope == Account;
    private void AdoptLoadedSelection(bool replace = false)
    {
        if ((replace || Selection is null) && _commands.Snapshot is { } snapshot)
            Selection = new(_commands.IsCloud ? TodoWorkspaceSource.Cloud : TodoWorkspaceSource.Browser, snapshot.Document.Id);
    }
    private async Task RunAsync(Func<Task> action, CancellationToken token, bool loading = false, bool preserveUnavailable = false)
    {
        if (Account is null) throw new InvalidOperationException("Sign in before loading account-bound Todo data.");
        if (IsBusy) throw new InvalidOperationException("Wait for the current Todo operation.");
        var scope = Account!; var generation = _generation;
        IsBusy = true; _loading = loading; Message = null; Changed?.Invoke();
        try
        {
            token.ThrowIfCancellationRequested(); await action();
            if (!Current(scope, generation)) return;
            token.ThrowIfCancellationRequested();
            if (!preserveUnavailable) _unavailable = _commands.RequiresReload;
            if (!preserveUnavailable) Message = _commands.Message ?? Message;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        { if (Current(scope, generation)) { _unavailable = true; Message = "Todo loading was cancelled. Reload the selected source."; } throw; }
        catch
        { if (Current(scope, generation)) { _unavailable = true; Message = "Todo data is unavailable. Reload the selected source; original command references remain available."; } throw; }
        finally { if (Current(scope, generation)) { IsBusy = false; _loading = false; Changed?.Invoke(); } }
    }
    private void SessionChanged()
    {
        if (Account != _account)
        {
            _account = Account; _generation++; _commands.Forget(); Selection = null; Sources = []; BrowserSourcesAvailable = null;
            _validated = null; _unavailable = false; _loading = false; IsBusy = false; Message = null;
        }
        Changed?.Invoke();
    }
    public void Dispose() => _session.Changed -= SessionChanged;
}
