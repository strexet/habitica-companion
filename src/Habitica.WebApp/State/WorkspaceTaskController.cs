using Habitica.Application.Workspace;
using Habitica.Application.Diagnostics;
using Habitica.Domain.Diagnostics;
using Habitica.Domain.Workspace;
using Habitica.Storage;

namespace Habitica.WebApp.State;

public sealed record WorkspaceTaskInput(WorkspaceOperation Operation, Guid? TaskId, string Text, string Notes, CompanionPriority Priority,
    DateOnly? Deadline, HabiticaTaskDetails? Details, bool ProjectToHabitica, HabitScoreDirection Direction, int Count, DateOnly? OccurrenceDate, WorkspaceTaskPlanning? Planning = null, WorkspaceTodoRestore? RestoreTodo = null, WorkspaceTaskRelationships? Relationships = null, WorkspaceTodoState? TodoState = null, WorkspaceTodoStage? TodoStage = null, RecurrenceRule? Recurrence = null, bool ClearRecurrence = false, WorkspaceTaskReview? Review = null, WorkspaceTaskUnarchive? Unarchive = null, WorkspaceOccurrenceCorrection? CorrectOccurrence = null);

public sealed class WorkspaceTaskController(IAppSessionController session, IWorkspaceCloudClient cloud, IndexedDbWorkspaceDatabase database, DiagnosticsLogWriter? diagnostics = null)
{
    private string? _scope;
    private WorkspaceJournalSnapshot? _snapshot;
    private WorkspaceCommand? _intent;
    private Guid _requestId;
    private long _generation;
    private long _loadVersion;
    public WorkspaceJournalSnapshot? Snapshot => CurrentScope == _scope ? _snapshot : null;
    public bool IsCloud { get; private set; }
    public bool CloudConnected => cloud.WorkspaceId is not null;
    public IReadOnlyList<Guid> LocalIds { get; private set; } = [];
    public WorkspaceCommandPreview? Preview { get; private set; }
    public WorkspaceProjectionPreview? LinkPreview { get; private set; }
    public WorkspaceMutationReceipt? LastReceipt { get; private set; }
    public string? Message { get; private set; }
    private bool _preparationUnknown;
    public bool PreparationRejected { get; private set; }
    public bool LinkAcceptanceUnknown { get; private set; }
    public bool PreparationUnknown => _intent is not null && Preview is null && _preparationUnknown;
    public bool HasPendingIntent => _intent is not null || LinkAcceptanceUnknown;
    public Guid? OriginalRequestId => CurrentScope == _scope && _requestId != Guid.Empty && (_intent is not null || LastReceipt is not null) ? _requestId : null;
    public bool AcceptanceUnknown { get; private set; }
    public bool RequiresReload { get; private set; }
    public bool IsReadOnly => RequiresReload || !IsCloud && Snapshot?.Authority?.Mode is WorkspaceAuthorityMode.Frozen or WorkspaceAuthorityMode.Cloud;
    private string? CurrentScope => session.State.IsAuthenticated && Guid.TryParse(session.State.UserId, out var id) ? id.ToString("D") : null;

    private Task ObserveAsync(string operation, Func<Task> action, bool? cloudSource = null)
    {
        if (diagnostics is null) return action();
        var scope = CurrentScope; var generation = _generation;
        var priorPreview = Preview;
        var mode = !(cloudSource ?? IsCloud) ? DiagnosticsMode.Local
            : operation is "workspace-load" or "workspace-task-recover" or "workspace-link-preview" ? DiagnosticsMode.LiveRead : DiagnosticsMode.LiveMutation;
        return diagnostics.ObserveAsync(DiagnosticsFeatureArea.Workspace, operation,
            async () => { await action(); return true; }, _ => Describe(), () => scope == CurrentScope && generation == _generation, mode);

        DiagnosticsContext Describe()
        {
            var receipt = operation is "workspace-task-accept" or "workspace-task-recover" or "workspace-task-prepare" ? LastReceipt : null;
            var result = AcceptanceUnknown || PreparationUnknown || LinkAcceptanceUnknown || operation == "workspace-task-recover" && receipt is null ? DiagnosticsResult.Unknown
                : receipt is not null ? DiagnosticsResult.Success
                : RequiresReload ? DiagnosticsResult.Unavailable
                : operation == "workspace-task-prepare" && Preview is null ? IsCloud && !PreparationRejected ? DiagnosticsResult.Unknown : DiagnosticsResult.Refused
                : operation == "workspace-task-accept" && LastReceipt is null ? DiagnosticsResult.Refused
                : operation == "workspace-task-prepare" && ReferenceEquals(priorPreview, Preview) ? DiagnosticsResult.Observed
                : operation == "workspace-link-preview" && LinkPreview is null ? DiagnosticsResult.Refused
                : operation == "workspace-link-adopt" ? LinkPreview is not null ? DiagnosticsResult.Refused : DiagnosticsResult.Observed
                : DiagnosticsResult.Success;
            return new(result, CorrelationId: receipt?.CorrelationId ?? Preview?.CorrelationId,
                CommandId: receipt?.CommandId ?? Preview?.PreparedCommandId ?? _intent?.Id,
                AcceptedCount: receipt?.AcceptedEventCount, ConfirmedCount: receipt?.ConfirmedProjectionCount, RequestedCount: receipt?.TotalProjectionCount);
        }
    }

    public void Forget()
    {
        _generation++; IsCloud = false; _scope = null; _snapshot = null; _intent = null; Preview = null; LinkPreview = null; LastReceipt = null; LocalIds = []; _preparationUnknown = PreparationRejected = LinkAcceptanceUnknown = false; AcceptanceUnknown = false; RequiresReload = false; Message = null;
    }
    private string Account() => CurrentScope ?? throw new InvalidOperationException("Sign in before managing Companion tasks.");
    private bool StillCurrent(string scope, long generation)
    {
        if (generation != _generation) return false;
        if (scope == CurrentScope) return true;
        Forget(); Message = "The account changed. Reload workspace data."; return false;
    }
    public Task LoadAsync(bool cloudSource, Guid? localId = null, CancellationToken cancellationToken = default) => ObserveAsync("workspace-load", () => LoadCoreAsync(cloudSource, localId, cancellationToken), cloudSource);
    private async Task LoadCoreAsync(bool cloudSource, Guid? localId = null, CancellationToken cancellationToken = default)
    {
        var scope = Account(); var generation = _generation;
        if (HasPendingIntent && (cloudSource != IsCloud || localId is not null && localId != _snapshot?.Document.Id)) throw new InvalidOperationException("Resolve or cancel the current preview before switching workspaces.");
        var loadVersion = ++_loadVersion;
        if (!LinkAcceptanceUnknown) LinkPreview = null;
        var ids = cloudSource ? LocalIds : await database.ListAsync(scope, cancellationToken);
        if (!StillCurrent(scope, generation) || loadVersion != _loadVersion) return;
        if (!cloudSource && localId is null && (Snapshot is null || IsCloud) && ids.Count > 1)
        {
            _scope = scope; _snapshot = null; LocalIds = ids; IsCloud = false; RequiresReload = true;
            Message = "Choose the exact browser workspace. Independent workspaces are never merged."; return;
        }
        var id = localId ?? (Snapshot is not null && !IsCloud ? Snapshot.Document.Id : ids.FirstOrDefault());
        var snapshot = cloudSource ? await cloud.LoadWorkspaceAsync(cancellationToken) : id == Guid.Empty ? null : await database.ForScope(scope).LoadAsync(id, cancellationToken);
        if (!StillCurrent(scope, generation) || loadVersion != _loadVersion) return;
        cancellationToken.ThrowIfCancellationRequested();
        if (HasPendingIntent && (cloudSource != IsCloud || snapshot is not null && snapshot.Document.Id != _snapshot?.Document.Id))
        {
            Message = "The original command now owns the selected workspace. Resolve the command before switching sources.";
            return;
        }
        var retainOriginal = snapshot is null && HasPendingIntent && _scope == scope && IsCloud == cloudSource;
        _scope = scope; _snapshot = retainOriginal ? _snapshot : snapshot; LocalIds = ids; IsCloud = cloudSource; RequiresReload = snapshot is null;
        Message = snapshot is null ? cloudSource ? "Cloud data is unavailable. Reconnect in Settings or try again later." : "Create a local workspace to start saving Companion tasks." : null;
        if (retainOriginal) Message = cloudSource ? "Workspace access is unavailable. The original command reference remains available; renew cloud access in Settings before checking its receipt." : "Browser storage is unavailable. The original command reference remains available; restore storage access before checking its receipt.";
    }
    public Task CreateLocalAsync(CancellationToken cancellationToken = default) => ObserveAsync("workspace-create-local", () => CreateLocalCoreAsync(cancellationToken));
    private async Task CreateLocalCoreAsync(CancellationToken cancellationToken = default)
    {
        if (HasPendingIntent) throw new InvalidOperationException("Resolve the current preview first.");
        var scope = Account(); var generation = _generation; var snapshot = new WorkspaceJournalSnapshot(new WorkspaceDocument { Id = Guid.NewGuid() }, 1, [], []);
        if (!await database.ForScope(scope).TryCommitAsync(0, snapshot, cancellationToken)) throw new InvalidOperationException("Local workspace could not be created.");
        if (!StillCurrent(scope, generation)) return;
        await LoadAsync(false, snapshot.Document.Id, cancellationToken);
    }
    private WorkspaceCommand Intent(WorkspaceTaskInput input)
    {
        var snapshot = Snapshot ?? throw new InvalidOperationException("Load a workspace first.");
        if (IsReadOnly) throw new InvalidOperationException("This browser copy is frozen or cloud-owned. Select the connected cloud workspace.");
        var current = snapshot.Document.Tasks.FirstOrDefault(task => task.Id == input.TaskId);
        if (input.Recurrence is not null || input.ClearRecurrence)
        {
            if (input.Operation != WorkspaceOperation.CreateDaily && (input.Operation != WorkspaceOperation.UpdateTask || current?.Kind != CompanionTaskKind.Daily)
                || input.ClearRecurrence && (input.Recurrence is not null || input.Operation == WorkspaceOperation.CreateDaily)
                || input.Recurrence is { } rule && !RecurrenceCalendar.Expand(rule, rule.StartDate, rule.StartDate).Succeeded)
                throw new InvalidOperationException("Choose a valid complete Daily recurrence replacement or explicit clearing on an existing Daily.");
        }
        WorkspaceTaskPatch? patch = null;
        var creating = input.Operation is WorkspaceOperation.CreateTodo or WorkspaceOperation.CreateDaily or WorkspaceOperation.CreateHabit;
        if (creating) patch = new(input.Text, input.Notes.Length == 0 ? null : input.Notes, Priority: input.Priority, Deadline: input.Deadline, HabiticaDetails: input.Details, Recurrence: input.Recurrence, ClearRecurrence: input.ClearRecurrence);
        if (input.Operation == WorkspaceOperation.UpdateTask)
        {
            if (current is null) throw new InvalidOperationException("Select an exact Companion task.");
            patch = new(input.Text == current.Text ? null : input.Text, input.Notes == (current.Notes ?? "") || input.Notes.Length == 0 ? null : input.Notes,
                input.Notes.Length == 0 && !string.IsNullOrEmpty(current.Notes), input.Priority == current.Priority ? null : input.Priority,
                input.Deadline == current.Deadline ? null : input.Deadline, input.Deadline is null && current.Deadline is not null, input.Details, input.Recurrence, input.ClearRecurrence);
        }
        if (input.Operation == WorkspaceOperation.UpdateTask && input.ProjectToHabitica && current is not null && patch is { } unchanged
            && (unchanged with { HabiticaDetails = null }) == new WorkspaceTaskPatch()
            && (input.Details is null || (input.Details.Difficulty is null || input.Details.Difficulty == current.HabiticaDetails?.Difficulty)
                && (input.Details.Attribute is null || input.Details.Attribute == current.HabiticaDetails?.Attribute)
                && (input.Details.Up is null || input.Details.Up == current.HabiticaDetails?.Up)
                && (input.Details.Down is null || input.Details.Down == current.HabiticaDetails?.Down)
                && (input.Details.Checklist is null || input.Details.Checklist.SequenceEqual(current.HabiticaDetails?.Checklist ?? []))))
        { input = input with { Operation = WorkspaceOperation.EnsureProjection }; patch = null; }
        if (input.Operation == WorkspaceOperation.PlanTask) patch = new(Planning: input.Planning);
        if (input.Operation == WorkspaceOperation.ReviewTask) patch = new(Review:input.Review);
        if (input.Operation == WorkspaceOperation.SetTodoStage) patch = new(TodoStage:input.TodoStage);
        if (input.Operation == WorkspaceOperation.SetTodoState) patch = new(TodoState: input.TodoState);
        if (input.Operation == WorkspaceOperation.RelateTodo) patch = new(Relationships: input.Relationships);
        if (input.Operation == WorkspaceOperation.UnarchiveTask) patch = new(Unarchive: input.Unarchive);
        if (input.Operation == WorkspaceOperation.CorrectOccurrence) patch = new(CorrectOccurrence: input.CorrectOccurrence);
        if (input.Operation == WorkspaceOperation.RestoreTodo) patch = new(RestoreTodo: input.RestoreTodo);
        return WorkspaceTodoProjectionPolicy.Prepare(snapshot.Document, new(Guid.NewGuid(), snapshot.Document.Id, snapshot.Document.Revision, input.Operation, creating ? Guid.NewGuid() : input.TaskId ?? Guid.Empty,
            patch, input.Operation == WorkspaceOperation.ScoreHabit ? input.Direction : null, input.Operation == WorkspaceOperation.ScoreHabit ? input.Count : 1,
            input.Operation is WorkspaceOperation.CompleteTask or WorkspaceOperation.SkipOccurrence ? input.OccurrenceDate : null, input.ProjectToHabitica));
    }
    public Task PrepareAsync(WorkspaceTaskInput input, CancellationToken cancellationToken = default) => ObserveAsync("workspace-task-prepare", () => PrepareCoreAsync(input, cancellationToken));
    public Task RetryOriginalPreviewAsync(CancellationToken cancellationToken = default)
    {
        if (_intent is null) throw new InvalidOperationException("No original preparation is available.");
        return ObserveAsync("workspace-task-prepare", () => PrepareCoreAsync(null, cancellationToken));
    }
    private async Task PrepareCoreAsync(WorkspaceTaskInput? input, CancellationToken cancellationToken = default)
    {
        var scope = Account(); var generation = _generation; if (scope != _scope || Snapshot is null) throw new InvalidOperationException("Reload the current account workspace.");
        if (LinkAcceptanceUnknown) throw new InvalidOperationException("Recover the original link before preparing another command.");
        LinkPreview = null;
        PreparationRejected = false;
        if (_intent is null) { _intent = Intent(input ?? throw new InvalidOperationException("No original preparation is available.")); _requestId = Guid.NewGuid(); LastReceipt = null; }
        if (IsCloud)
        {
            var wasUnknown = _preparationUnknown; _preparationUnknown = true;
            var result = await cloud.PrepareCommandAsync(_requestId, _intent, cancellationToken);
            if (!StillCurrent(scope, generation)) return;
            Message = result.Message;
            _preparationUnknown = wasUnknown || result.OutcomeUnknown;
            PreparationRejected = !result.Succeeded && !_preparationUnknown;
            if (result.Succeeded && result.Value is { } preview)
            {
                _preparationUnknown = PreparationRejected = false; Preview = preview;
                if (preview.SavedInCompanion && preview.Receipt is { } receipt) await SavedAsync(receipt, cancellationToken);
            }
        }
        else
        {
            if (WorkspaceTodoProjectionPolicy.Rejection(Snapshot.Document, _intent) is { } policyError)
            {
                Message = policyError == "historical_projection_requires_review"
                    ? "This completed Todo has no Habitica link. Historical integration needs a separate review; no task or reward was changed."
                    : $"Task integration is unavailable ({policyError}). Supply a supported Daily schedule or use Habitica link to review an existing record. No task or game effect was accepted.";
                _intent = null; return;
            }
            var result = WorkspaceCommandEngine.Accept(Snapshot, _intent, DateTimeOffset.UtcNow);
            if (result.Disposition != SubmissionDisposition.Accepted) { Message = _intent.Operation == WorkspaceOperation.EnsureProjection
                ? $"Counterpart creation is unavailable ({result.ErrorCode}). Recover an original pending create, or review an existing Habitica record for adoption; historical tasks must not be scored or reopened."
                : "The preview is invalid or stale. Check the task, field bounds and current revision."; _intent = null; return; }
            Preview = new(_intent.Id, _requestId, DateTimeOffset.UtcNow.AddMinutes(10), false, _intent, result.Entry!.Effects.Count > 0, false);
            Message = _intent.Operation == WorkspaceOperation.ReviewTask ? "Review the voluntary cue action and timezone. Only explicit review metadata and retained history change; no completion or game effect occurs." : _intent.Operation == WorkspaceOperation.SetTodoStage ? "Review the active Todo stage preference. Current state, dates, dependencies, history and accepted game commands remain unchanged." : _intent.Operation == WorkspaceOperation.SetTodoState ? "Review the native Todo planning state. Completion history, dependencies and accepted game commands remain unchanged; no new game effect occurs." : _intent.Operation == WorkspaceOperation.RelateTodo ? "Review the exact Todo parent and full dependency replacement. This command changes native relationships only." : _intent.Operation == WorkspaceOperation.RestoreTodo ? "Review the exact Todo completion to restore. Earlier completion history remains; no Habitica effect or reward reversal occurs." : _intent.Operation == WorkspaceOperation.PlanTask ? "Review native planned date and placement. This command changes Companion only." : "Review this command before saving. Local projection effects wait for an explicit ownership transfer and enabled cloud executor.";
        }
    }
    public void CancelPreview()
    {
        if (AcceptanceUnknown || PreparationUnknown) throw new InvalidOperationException("Recover the original request or receipt before leaving an uncertain command.");
        _intent = null; Preview = null; _preparationUnknown = PreparationRejected = false; Message = null;
    }
    public Task ConfirmAsync(CancellationToken cancellationToken = default) => ObserveAsync("workspace-task-accept", () => ConfirmCoreAsync(cancellationToken));
    private async Task ConfirmCoreAsync(CancellationToken cancellationToken = default)
    {
        var scope = Account(); var generation = _generation; var preview = Preview ?? throw new InvalidOperationException("Prepare a command first.");
        if (scope != _scope || preview.Command.WorkspaceId != Snapshot?.Document.Id) throw new InvalidOperationException("Reload the current workspace.");
        if (!AcceptanceUnknown && (preview.Expired || preview.ExpiresAtUtc <= DateTimeOffset.UtcNow)) { Message = "This preview expired. Cancel and review a new preview."; return; }
        var wasUnknown = AcceptanceUnknown; AcceptanceUnknown = true;
        if (IsCloud)
        {
            var result = await cloud.AcceptCommandAsync(preview, cancellationToken);
            if (!StillCurrent(scope, generation)) return;
            Message = result.Message; AcceptanceUnknown = wasUnknown || result.OutcomeUnknown;
            if (result.Succeeded && result.Value is { } receipt) await SavedAsync(receipt, cancellationToken);
        }
        else
        {
            var result = await new WorkspaceCommandService(database.ForScope(scope)).SubmitAsync(preview.Command, cancellationToken);
            if (!StillCurrent(scope, generation)) return;
            AcceptanceUnknown = wasUnknown || result?.Disposition == SubmissionDisposition.Unavailable;
            if (result?.SavedInCompanion == true && result.Entry is { } entry) await SavedAsync(Receipt(entry, preview.CorrelationId), cancellationToken);
            else Message = AcceptanceUnknown ? "Local acceptance is unknown. Check the original command receipt before retrying." : "The command was not saved. Reload current state and review a new preview.";
        }
    }
    public Task CheckOriginalAsync(CancellationToken cancellationToken = default) => ObserveAsync("workspace-task-recover", () => CheckOriginalCoreAsync(cancellationToken));
    private async Task CheckOriginalCoreAsync(CancellationToken cancellationToken = default)
    {
        var scope = Account(); var generation = _generation; if (IsCloud && Preview is null) throw new InvalidOperationException("Recover the original preview before checking the command receipt."); var id = Preview?.PreparedCommandId ?? _intent?.Id ?? throw new InvalidOperationException("No original command is available.");
        if (IsCloud)
        {
            var result = await cloud.ReadCommandAsync(id, cancellationToken);
            if (!StillCurrent(scope, generation)) return;
            Message = result.Message;
            if (result.Succeeded && result.Value is { } receipt) await SavedAsync(receipt, cancellationToken);
        }
        else
        {
            var current = await database.ForScope(scope).LoadAsync(_intent!.WorkspaceId, cancellationToken);
            if (!StillCurrent(scope, generation)) return;
            var entry = current?.Commands.FirstOrDefault(entry => entry.Command.Id == id);
            if (entry is not null) await SavedAsync(Receipt(entry, Preview!.CorrelationId), cancellationToken);
            else Message = "No receipt is visible yet. Retry only the original command; do not create another action for the same intent.";
        }
    }
    private async Task SavedAsync(WorkspaceMutationReceipt receipt, CancellationToken cancellationToken)
    {
        LastReceipt = receipt; AcceptanceUnknown = false; _preparationUnknown = PreparationRejected = false; _intent = null; Preview = null;
        var scope = _scope; var generation = _generation; var id = _snapshot?.Document.Id;
        var refreshed = false;
        try { await LoadAsync(IsCloud, IsCloud ? null : id, cancellationToken); refreshed = Snapshot is not null; }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { if (scope == CurrentScope && generation == _generation) { RequiresReload = true; LastReceipt = receipt with { RefreshSucceeded = false }; Message = "The command was saved, but workspace refresh failed. Reload before another edit."; } }
        if (scope == CurrentScope && generation == _generation && !refreshed) { RequiresReload = true; LastReceipt = receipt with { RefreshSucceeded = false }; Message = "The command was saved, but workspace refresh failed. Reload before another edit."; }
        if (scope == CurrentScope && generation == _generation && refreshed) Message = $"Saved in Companion. Habitica projection: {receipt.HabiticaProjectionStatus}. Confirmed effects: {receipt.ConfirmedProjectionCount}/{receipt.TotalProjectionCount}.";
    }
    public Task PreviewLinkAsync(Guid taskId, Guid externalId, CancellationToken cancellationToken = default) => ObserveAsync("workspace-link-preview", () => PreviewLinkCoreAsync(taskId, externalId, cancellationToken));
    private async Task PreviewLinkCoreAsync(Guid taskId, Guid externalId, CancellationToken cancellationToken = default)
    {
        var scope = Account(); var generation = _generation;
        if (!IsCloud || Snapshot?.Document.Id != cloud.WorkspaceId || HasPendingIntent) throw new InvalidOperationException("Load the cloud workspace and resolve the current command before linking.");
        var result = await cloud.PreviewLinkAsync(taskId, externalId, cancellationToken);
        if (!StillCurrent(scope, generation)) return;
        LinkPreview = result.Succeeded ? result.Value : null; Message = result.Message;
    }
    public Task AdoptLinkAsync(CancellationToken cancellationToken = default) => ObserveAsync("workspace-link-adopt", () => AdoptLinkCoreAsync(cancellationToken));
    private async Task AdoptLinkCoreAsync(CancellationToken cancellationToken = default)
    {
        var scope = Account(); var generation = _generation; var preview = LinkPreview ?? throw new InvalidOperationException("Review a link preview first.");
        if (!IsCloud || Snapshot?.Document.Id != preview.WorkspaceId || _intent is not null) throw new InvalidOperationException("Reload and review the current cloud workspace before linking.");
        var wasUnknown = LinkAcceptanceUnknown; LinkAcceptanceUnknown = true;
        var result = await cloud.AdoptLinkAsync(preview, cancellationToken);
        if (!StillCurrent(scope, generation)) return;
        LinkAcceptanceUnknown = !result.KnownNotAccepted && (wasUnknown || result.OutcomeUnknown);
        if (result.Succeeded)
        {
            LinkAcceptanceUnknown = false; LinkPreview = null; RequiresReload = true;
            try { await LoadAsync(true, cancellationToken: cancellationToken); }
            catch { if (StillCurrent(scope, generation)) Message = "The link is confirmed, but workspace refresh failed. Reload before another action; do not repeat adoption."; return; }
            if (StillCurrent(scope, generation)) Message = RequiresReload ? "The link is confirmed, but current workspace data is unavailable. Reload before another action." : result.Message;
            return;
        }
        if (StillCurrent(scope, generation)) Message = result.Message;
    }
    public void CancelLinkPreview()
    {
        if (LinkAcceptanceUnknown) throw new InvalidOperationException("Recover the original link receipt before cancelling.");
        LinkPreview = null;
    }
    public static WorkspaceMutationReceipt Receipt(WorkspaceCommandEntry entry, Guid correlationId) => new(
        System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(entry.Command.Operation.ToString()), entry.Command.TaskId, entry.Command.Id, correlationId, entry.TaskSnapshot?.Text ?? "", true,
        entry.ProjectionState == ProjectionCommandState.UnknownOutcome ? "UnknownOutcome" : entry.ProjectionState is ProjectionCommandState.Leased or ProjectionCommandState.Executing ? "pending" : System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(entry.ProjectionState.ToString()),
        entry.Command.Operation == WorkspaceOperation.ScoreHabit ? entry.Command.Count : entry.Command.Operation is WorkspaceOperation.CompleteTask or WorkspaceOperation.SkipOccurrence or WorkspaceOperation.RestoreTodo or WorkspaceOperation.ReviewTask or WorkspaceOperation.UnarchiveTask or WorkspaceOperation.CorrectOccurrence ? 1 : 0,
        entry.ConfirmedEffects, entry.Effects.Count, entry.Failure == ProjectionFailure.None ? null : entry.ConfirmedEffects + 1, entry.Failure.ToString(), entry.RetryAtUtc, true, entry.ProjectionState == ProjectionCommandState.UnknownOutcome);
}
