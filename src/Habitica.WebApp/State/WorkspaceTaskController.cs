using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.Storage;

namespace Habitica.WebApp.State;

public sealed record WorkspaceTaskInput(WorkspaceOperation Operation, Guid? TaskId, string Text, string Notes, CompanionPriority Priority,
    DateOnly? Deadline, HabiticaTaskDetails? Details, bool ProjectToHabitica, HabitScoreDirection Direction, int Count, DateOnly? OccurrenceDate);

public sealed class WorkspaceTaskController(IAppSessionController session, IWorkspaceCloudClient cloud, IndexedDbWorkspaceDatabase database)
{
    private string? _scope;
    private WorkspaceJournalSnapshot? _snapshot;
    private WorkspaceCommand? _intent;
    private Guid _requestId;
    public WorkspaceJournalSnapshot? Snapshot => CurrentScope == _scope ? _snapshot : null;
    public bool IsCloud { get; private set; }
    public bool CloudConnected => cloud.WorkspaceId is not null;
    public IReadOnlyList<Guid> LocalIds { get; private set; } = [];
    public WorkspaceCommandPreview? Preview { get; private set; }
    public WorkspaceProjectionPreview? LinkPreview { get; private set; }
    public WorkspaceMutationReceipt? LastReceipt { get; private set; }
    public string? Message { get; private set; }
    public bool HasPendingIntent => _intent is not null;
    public bool AcceptanceUnknown { get; private set; }
    public bool RequiresReload { get; private set; }
    public bool IsReadOnly => RequiresReload || !IsCloud && Snapshot?.Authority?.Mode is WorkspaceAuthorityMode.Frozen or WorkspaceAuthorityMode.Cloud;
    private string? CurrentScope => session.State.IsAuthenticated && Guid.TryParse(session.State.UserId, out var id) ? id.ToString("D") : null;

    public void Forget()
    {
        _scope = null; _snapshot = null; _intent = null; Preview = null; LinkPreview = null; LastReceipt = null; LocalIds = []; AcceptanceUnknown = false; RequiresReload = false; Message = null;
    }
    private string Account() => CurrentScope ?? throw new InvalidOperationException("Sign in before managing Companion tasks.");
    private bool StillCurrent(string scope) { if (scope == CurrentScope) return true; Forget(); Message = "The account changed. Reload workspace data."; return false; }
    public async Task LoadAsync(bool cloudSource, Guid? localId = null, CancellationToken cancellationToken = default)
    {
        var scope = Account();
        if (_intent is not null && (cloudSource != IsCloud || localId is not null && localId != _snapshot?.Document.Id)) throw new InvalidOperationException("Resolve or cancel the current preview before switching workspaces.");
        LinkPreview = null;
        var ids = cloudSource ? LocalIds : await database.ListAsync(scope, cancellationToken);
        var id = localId ?? (Snapshot is not null && !IsCloud ? Snapshot.Document.Id : ids.FirstOrDefault());
        var snapshot = cloudSource ? await cloud.LoadWorkspaceAsync(cancellationToken) : id == Guid.Empty ? null : await database.ForScope(scope).LoadAsync(id, cancellationToken);
        if (!StillCurrent(scope)) return;
        _scope = scope; _snapshot = snapshot; LocalIds = ids; IsCloud = cloudSource; RequiresReload = snapshot is null;
        Message = snapshot is null ? cloudSource ? "Cloud data is unavailable. Reconnect in Settings or try again later." : "Create a local workspace to start saving Companion tasks." : null;
    }
    public async Task CreateLocalAsync(CancellationToken cancellationToken = default)
    {
        if (_intent is not null) throw new InvalidOperationException("Resolve the current preview first.");
        var scope = Account(); var snapshot = new WorkspaceJournalSnapshot(new WorkspaceDocument { Id = Guid.NewGuid() }, 1, [], []);
        if (!await database.ForScope(scope).TryCommitAsync(0, snapshot, cancellationToken)) throw new InvalidOperationException("Local workspace could not be created.");
        if (!StillCurrent(scope)) return;
        await LoadAsync(false, snapshot.Document.Id, cancellationToken);
    }
    private WorkspaceCommand Intent(WorkspaceTaskInput input)
    {
        var snapshot = Snapshot ?? throw new InvalidOperationException("Load a workspace first.");
        if (IsReadOnly) throw new InvalidOperationException("This browser copy is frozen or cloud-owned. Select the connected cloud workspace.");
        var current = snapshot.Document.Tasks.FirstOrDefault(task => task.Id == input.TaskId);
        WorkspaceTaskPatch? patch = null;
        if (input.Operation == WorkspaceOperation.CreateTodo) patch = new(input.Text, input.Notes.Length == 0 ? null : input.Notes, Priority: input.Priority, Deadline: input.Deadline, HabiticaDetails: input.Details);
        if (input.Operation == WorkspaceOperation.UpdateTask)
        {
            if (current is null) throw new InvalidOperationException("Select an exact Companion task.");
            patch = new(input.Text == current.Text ? null : input.Text, input.Notes == (current.Notes ?? "") || input.Notes.Length == 0 ? null : input.Notes,
                input.Notes.Length == 0 && !string.IsNullOrEmpty(current.Notes), input.Priority == current.Priority ? null : input.Priority,
                input.Deadline == current.Deadline ? null : input.Deadline, input.Deadline is null && current.Deadline is not null, input.Details);
        }
        return new(Guid.NewGuid(), snapshot.Document.Id, snapshot.Document.Revision, input.Operation, input.Operation == WorkspaceOperation.CreateTodo ? Guid.NewGuid() : input.TaskId ?? Guid.Empty,
            patch, input.Operation == WorkspaceOperation.ScoreHabit ? input.Direction : null, input.Operation == WorkspaceOperation.ScoreHabit ? input.Count : 1,
            input.Operation == WorkspaceOperation.CompleteTask ? input.OccurrenceDate : null, input.ProjectToHabitica);
    }
    public async Task PrepareAsync(WorkspaceTaskInput input, CancellationToken cancellationToken = default)
    {
        var scope = Account(); if (scope != _scope || Snapshot is null) throw new InvalidOperationException("Reload the current account workspace.");
        LinkPreview = null;
        if (_intent is null) { _intent = Intent(input); _requestId = Guid.NewGuid(); LastReceipt = null; }
        if (IsCloud)
        {
            var result = await cloud.PrepareCommandAsync(_requestId, _intent, cancellationToken);
            if (!StillCurrent(scope)) return;
            Message = result.Message;
            if (result.Succeeded && result.Value is { } preview)
            {
                Preview = preview;
                if (preview.SavedInCompanion && preview.Receipt is { } receipt) await SavedAsync(receipt, cancellationToken);
            }
        }
        else
        {
            var result = WorkspaceCommandEngine.Accept(Snapshot, _intent, DateTimeOffset.UtcNow);
            if (result.Disposition != SubmissionDisposition.Accepted) { Message = "The preview is invalid or stale. Check the task, field bounds and current revision."; _intent = null; return; }
            Preview = new(_intent.Id, _requestId, DateTimeOffset.UtcNow.AddMinutes(10), false, _intent, result.Entry!.Effects.Count > 0, false);
            Message = "Review this command before saving. Local projection effects wait for an explicit ownership transfer and enabled cloud executor.";
        }
    }
    public void CancelPreview()
    {
        if (AcceptanceUnknown) throw new InvalidOperationException("Check the original receipt before leaving an uncertain acceptance.");
        _intent = null; Preview = null; Message = null;
    }
    public async Task ConfirmAsync(CancellationToken cancellationToken = default)
    {
        var scope = Account(); var preview = Preview ?? throw new InvalidOperationException("Prepare a command first.");
        if (scope != _scope || preview.Command.WorkspaceId != Snapshot?.Document.Id) throw new InvalidOperationException("Reload the current workspace.");
        if (!AcceptanceUnknown && (preview.Expired || preview.ExpiresAtUtc <= DateTimeOffset.UtcNow)) { Message = "This preview expired. Cancel and review a new preview."; return; }
        var wasUnknown = AcceptanceUnknown; AcceptanceUnknown = true;
        if (IsCloud)
        {
            var result = await cloud.AcceptCommandAsync(preview, cancellationToken);
            if (!StillCurrent(scope)) return;
            Message = result.Message; AcceptanceUnknown = wasUnknown || result.OutcomeUnknown;
            if (result.Succeeded && result.Value is { } receipt) await SavedAsync(receipt, cancellationToken);
        }
        else
        {
            var result = await new WorkspaceCommandService(database.ForScope(scope)).SubmitAsync(preview.Command, cancellationToken);
            if (!StillCurrent(scope)) return;
            AcceptanceUnknown = wasUnknown || result?.Disposition == SubmissionDisposition.Unavailable;
            if (result?.SavedInCompanion == true && result.Entry is { } entry) await SavedAsync(Receipt(entry, preview.CorrelationId), cancellationToken);
            else Message = AcceptanceUnknown ? "Local acceptance is unknown. Check the original command receipt before retrying." : "The command was not saved. Reload current state and review a new preview.";
        }
    }
    public async Task CheckOriginalAsync(CancellationToken cancellationToken = default)
    {
        var scope = Account(); if (IsCloud && Preview is null) throw new InvalidOperationException("Recover the original preview before checking the command receipt."); var id = Preview?.PreparedCommandId ?? _intent?.Id ?? throw new InvalidOperationException("No original command is available.");
        if (IsCloud)
        {
            var result = await cloud.ReadCommandAsync(id, cancellationToken);
            if (!StillCurrent(scope)) return;
            Message = result.Message;
            if (result.Succeeded && result.Value is { } receipt) await SavedAsync(receipt, cancellationToken);
        }
        else
        {
            var current = await database.ForScope(scope).LoadAsync(_intent!.WorkspaceId, cancellationToken);
            if (!StillCurrent(scope)) return;
            var entry = current?.Commands.FirstOrDefault(entry => entry.Command.Id == id);
            if (entry is not null) await SavedAsync(Receipt(entry, Preview!.CorrelationId), cancellationToken);
            else Message = "No receipt is visible yet. Retry only the original command; do not create another action for the same intent.";
        }
    }
    private async Task SavedAsync(WorkspaceMutationReceipt receipt, CancellationToken cancellationToken)
    {
        LastReceipt = receipt; AcceptanceUnknown = false; _intent = null; Preview = null;
        var scope = _scope; var id = _snapshot?.Document.Id;
        var refreshed = false;
        try { await LoadAsync(IsCloud, IsCloud ? null : id, cancellationToken); refreshed = Snapshot is not null; }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { if (scope == CurrentScope) { RequiresReload = true; LastReceipt = receipt with { RefreshSucceeded = false }; Message = "The command was saved, but workspace refresh failed. Reload before another edit."; } }
        if (scope == CurrentScope && !refreshed) { RequiresReload = true; LastReceipt = receipt with { RefreshSucceeded = false }; Message = "The command was saved, but workspace refresh failed. Reload before another edit."; }
        if (scope == CurrentScope && refreshed) Message = $"Saved in Companion. Habitica projection: {receipt.HabiticaProjectionStatus}. Confirmed effects: {receipt.ConfirmedProjectionCount}/{receipt.TotalProjectionCount}.";
    }
    public async Task PreviewLinkAsync(Guid taskId, Guid externalId, CancellationToken cancellationToken = default)
    {
        var scope = Account();
        if (!IsCloud || Snapshot?.Document.Id != cloud.WorkspaceId || HasPendingIntent) throw new InvalidOperationException("Load the cloud workspace and resolve the current command before linking.");
        var result = await cloud.PreviewLinkAsync(taskId, externalId, cancellationToken);
        if (!StillCurrent(scope)) return;
        LinkPreview = result.Succeeded ? result.Value : null; Message = result.Message;
    }
    public async Task AdoptLinkAsync(CancellationToken cancellationToken = default)
    {
        var scope = Account(); var preview = LinkPreview ?? throw new InvalidOperationException("Review a link preview first.");
        if (!IsCloud || Snapshot?.Document.Id != preview.WorkspaceId || HasPendingIntent) throw new InvalidOperationException("Reload and review the current cloud workspace before linking.");
        LinkPreview = null;
        var result = await cloud.AdoptLinkAsync(preview, cancellationToken);
        if (!StillCurrent(scope)) return;
        if (result.Succeeded) await LoadAsync(true, cancellationToken: cancellationToken);
        if (StillCurrent(scope)) Message = result.Message;
    }
    public void CancelLinkPreview() => LinkPreview = null;
    public static WorkspaceMutationReceipt Receipt(WorkspaceCommandEntry entry, Guid correlationId) => new(
        System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(entry.Command.Operation.ToString()), entry.Command.TaskId, entry.Command.Id, correlationId, entry.TaskSnapshot?.Text ?? "", true,
        entry.ProjectionState == ProjectionCommandState.UnknownOutcome ? "UnknownOutcome" : entry.ProjectionState is ProjectionCommandState.Leased or ProjectionCommandState.Executing ? "pending" : System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(entry.ProjectionState.ToString()),
        entry.Command.Operation == WorkspaceOperation.ScoreHabit ? entry.Command.Count : entry.Command.Operation == WorkspaceOperation.CompleteTask ? 1 : 0,
        entry.ConfirmedEffects, entry.Effects.Count, entry.Failure == ProjectionFailure.None ? null : entry.ConfirmedEffects + 1, entry.Failure.ToString(), entry.RetryAtUtc, true, entry.ProjectionState == ProjectionCommandState.UnknownOutcome);
}
