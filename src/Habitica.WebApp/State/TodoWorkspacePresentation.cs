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
public sealed partial class TodoWorkspacePresentation : IDisposable
{
    private readonly IAppSessionController _session;
    private readonly IWorkspaceCloudClient _cloud;
    private readonly IndexedDbWorkspaceDatabase _database;
    private readonly WorkspaceTaskController _commands;
    private readonly WorkspaceOrganizationController? _organization;
    private string? _account;
    private long _generation;
    private bool _unavailable;
    private bool _loading;
    private WorkspaceJournalSnapshot? _validated;
    private WorkspaceDocument? _coverageDocument;
    private WorkspaceHabiticaCoverage? _coverage;
    public TodoWorkspacePresentation(IAppSessionController session, IWorkspaceCloudClient cloud,
        IndexedDbWorkspaceDatabase database, WorkspaceTaskController commands, WorkspaceOrganizationController? organization = null)
    {
        _session = session; _cloud = cloud; _database = database; _commands = commands; _organization = organization;
        if (_organization is not null) _organization.Changed += OrganizationChanged;
        _account = Account; AdoptLoadedSelection(); _session.Changed += SessionChanged;
    }

    public event Action? Changed;
    public TodoWorkspaceSelection? Selection { get; private set; }
    public IReadOnlyList<TodoWorkspaceSelection> Sources { get; private set; } = [];
    public bool? BrowserSourcesAvailable { get; private set; }
    public bool IsBusy { get; private set; }
    public string? Message { get; private set; }
    public bool CanSwitchSource => !HasLinkedRecoveryIntent && !IsBusy && !_commands.HasPendingIntent && _commands.LinkPreview is null && _organization?.HasPendingIntent != true && _organization?.IsBusy != true;
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
    public WorkspaceHabiticaCoverage? HabiticaCoverage
    {
        get
        {
            var document = Snapshot?.Document;
            if (document is null) { _coverageDocument = null; _coverage = null; return null; }
            if (!ReferenceEquals(document, _coverageDocument))
            { _coverageDocument = document; _coverage = WorkspaceHabiticaCoveragePolicy.Audit(document,Guid.TryParse(Account,out var accountId)?accountId:null); }
            return _coverage;
        }
    }
    public string? OrganizationBlockReason => _organization?.HasPendingIntent == true
        ? "Resolve the original organization command in Projects before editing tasks."
        : _organization?.IsBusy == true ? "Wait for the current organization operation before editing tasks."
        : Selection is { } source && _organization?.LastReceipt is { SavedInCompanion: true } receipt
            && receipt.WorkspaceId == source.WorkspaceId && _organization.IsCloud == (source.Source == TodoWorkspaceSource.Cloud)
            && Revision < receipt.Entry.Snapshot.Revision ? "Organization changes were saved. Reload the selected workspace before editing tasks." : null;
    public bool IsReadOnly => HasLinkedRecoveryIntent || Snapshot is null || _commands.IsReadOnly || OrganizationBlockReason is not null;
    public string IntegrationReadiness => Selection?.Source != TodoWorkspaceSource.Cloud
        ? "Saved browser tasks request Habitica changes, but execution waits for cloud transfer, separate account consent and an enabled executor."
        : !_cloud.ConnectionStatusVerified ? "Cloud connection status is unverified. Check Todo Sync before relying on game effects."
        : !_cloud.ExecutorAvailable ? "Cloud task execution is unavailable. Canonical saves do not confirm Habitica presence or rewards."
        : !_cloud.ExecutorEnabled ? "Cloud execution is paused. Accepted Habitica changes remain pending until execution is separately enabled."
        : "Cloud execution is enabled at the last connection check. Only original confirmed receipts establish Habitica effects; pending or unknown remains unconfirmed.";
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
        RequireRecoveryResolved();
        if (OrganizationBlockReason is { } reason) throw new InvalidOperationException(reason);
        return RunAsync(() => _commands.PrepareAsync(input, token), token);
    }
    public Task PreviewLinkAsync(Guid taskId, Guid remoteId, CancellationToken token = default)
    { _ = RequireSnapshot(); RequireRecoveryResolved(); return RunAsync(() => _commands.PreviewLinkAsync(taskId, remoteId, token), token); }
    public Task AdoptLinkAsync(CancellationToken token = default) => RunAsync(() => _commands.AdoptLinkAsync(token), token);
    public void CancelLinkPreview() { if (IsBusy) throw new InvalidOperationException("Wait for the current operation."); _commands.CancelLinkPreview(); Changed?.Invoke(); }
    public Task ConfirmAsync(CancellationToken token = default) => RunAsync(() => _commands.ConfirmAsync(token), token);
    public Task RetryOriginalPreviewAsync(CancellationToken token = default) => RunAsync(() => _commands.RetryOriginalPreviewAsync(token), token);
    public Task CheckOriginalAsync(CancellationToken token = default) => RunAsync(() => _commands.CheckOriginalAsync(token), token);
    public void CancelPreview() { if (IsBusy) throw new InvalidOperationException("Wait for the current operation."); _commands.CancelPreview(); Changed?.Invoke(); }

    public Task LoadOrganizationAsync(CancellationToken token = default) => RunOrganizationAsync(async organization =>
    {
        var source = Selection ?? throw new InvalidOperationException("Select the workspace before loading organization data.");
        await organization.LoadAsync(source.Source == TodoWorkspaceSource.Cloud, source.Source == TodoWorkspaceSource.Browser ? source.WorkspaceId : null, token);
    }, token);
    public Task PrepareOrganizationAsync(WorkspaceOrganizationInput input, CancellationToken token = default) => RunOrganizationAsync(async organization =>
    {
        _ = RequireSnapshot();
        if (organization.HasPendingIntent) throw new InvalidOperationException("Resolve the original organization command before preparing another change.");
        var source = Selection!; var scope = Account!; var generation = _generation; var revision = Revision;
        await organization.LoadAsync(source.Source == TodoWorkspaceSource.Cloud, source.Source == TodoWorkspaceSource.Browser ? source.WorkspaceId : null, token);
        if (!Current(scope, generation) || source != Selection) return;
        if (organization.Snapshot?.Document.Revision != revision)
            throw new InvalidOperationException("Workspace data changed. Reload the selected workspace and editor context before reviewing.");
        await organization.PrepareAsync(input, token);
    }, token);
    public Task ConfirmOrganizationAsync(CancellationToken token = default) => RunOrganizationAsync(organization => organization.ConfirmAsync(token), token, true);
    public Task RetryOrganizationPreviewAsync(CancellationToken token = default) => RunOrganizationAsync(organization => organization.RetryOriginalPreviewAsync(token), token, true);
    public Task RecoverOrganizationAsync(CancellationToken token = default) => RunOrganizationAsync(organization => organization.CheckOriginalAsync(token), token, true);
    public void CancelOrganizationPreview()
    {
        if (IsBusy || _commands.HasPendingIntent) throw new InvalidOperationException("Wait for the original task operation.");
        (_organization ?? throw new InvalidOperationException("Organization management is unavailable.")).CancelPreview();
    }
    private Task RunOrganizationAsync(Func<WorkspaceOrganizationController, Task> action, CancellationToken token, bool refresh = false)
    {
        RequireRecoveryResolved();
        var organization = _organization ?? throw new InvalidOperationException("Organization management is unavailable.");
        if (_commands.HasPendingIntent) throw new InvalidOperationException("Resolve the original task command before changing organization.");
        var scope = Account; var generation = _generation; var selected = Selection;
        return RunAsync(async () =>
        {
            if (selected is null) throw new InvalidOperationException("Select a workspace before changing organization.");
            if (organization.HasPendingIntent && (organization.WorkspaceId != selected.WorkspaceId || organization.IsCloud != (selected.Source == TodoWorkspaceSource.Cloud)))
                throw new InvalidOperationException("Recover the original organization source before changing another workspace.");
            await action(organization);
            if (!refresh || scope is null || !Current(scope, generation) || selected != Selection) return;
            if (organization.LastReceipt is { SavedInCompanion: true } receipt && receipt.WorkspaceId == selected.WorkspaceId)
                await _commands.LoadAsync(selected.Source == TodoWorkspaceSource.Cloud, selected.Source == TodoWorkspaceSource.Browser ? selected.WorkspaceId : null, token);
        }, token);
    }
    private void OrganizationChanged() { if (Account == _account) AdoptLoadedSelection(); Changed?.Invoke(); }

    public WorkspaceHierarchyPage Hierarchy(WorkspaceHierarchyFilter filter) => WorkspaceTodoHierarchy.Evaluate(RequireSnapshot().Document, filter);
    public WorkspaceTaskPage Tasks(WorkspaceTaskFilter filter) => WorkspaceTaskQuery.Evaluate(RequireSnapshot().Document, filter);
    public WorkspaceTaskPage Planning(WorkspacePlanningList list, DateOnly day, int offset = 0) => WorkspacePlanningQuery.Evaluate(RequireSnapshot().Document, list, day, offset: offset, expectedRevision: Revision);
    public WorkspaceAgendaPage Agenda(WorkspaceAgendaFilter filter) => WorkspaceAgenda.Evaluate(RequireSnapshot().Document, filter);
    public WorkspaceReadPage<WorkspaceCompletionItem> History(WorkspaceHistoryFilter filter) => WorkspaceCompletionHistory.Evaluate(RequireSnapshot(), filter);
    public WorkspaceReadPage<WorkspaceHabitHistoryItem> HabitHistory(WorkspaceHabitHistoryFilter filter) => WorkspaceHabitHistory.Evaluate(RequireSnapshot(), filter);
    public WorkspaceReadPage<WorkspaceCategory> Categories(WorkspaceReadFilter filter) => WorkspaceOrganization.Categories(RequireSnapshot().Document, filter);
    public WorkspaceReadPage<WorkspaceProject> Projects(WorkspaceReadFilter filter, Guid? categoryId = null) => WorkspaceOrganization.Projects(RequireSnapshot().Document, filter, categoryId);
    public WorkspaceReadPage<WorkspaceSection> Sections(WorkspaceReadFilter filter, Guid projectId) => WorkspaceOrganization.Sections(RequireSnapshot().Document, filter, projectId);
    public WorkspaceProjectRemovalImpact ProjectRemoval(Guid projectId, long? expectedRevision = null) => WorkspaceProjectRemovalQuery.Evaluate(RequireSnapshot().Document, projectId, expectedRevision);
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
        if (Selection is null && _organization is { HasPendingIntent: true, WorkspaceId: { } originalId })
            Selection = new(_organization.IsCloud ? TodoWorkspaceSource.Cloud : TodoWorkspaceSource.Browser, originalId);
        else if ((replace || Selection is null) && _commands.Snapshot is { } snapshot)
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
            ClearLinkedRecovery();
            _account = Account; _generation++; _commands.Forget(); Selection = null; Sources = []; BrowserSourcesAvailable = null;
            _validated = null; _coverageDocument = null; _coverage = null;
            _unavailable = false; _loading = false; IsBusy = false; Message = null;
            // Consumers clear account-private drafts before an original source can be restored, even for reused workspace IDs.
            Changed?.Invoke(); AdoptLoadedSelection();
        }
        Changed?.Invoke();
    }
    public void Dispose() { _session.Changed -= SessionChanged; if (_organization is not null) _organization.Changed -= OrganizationChanged; }
}
