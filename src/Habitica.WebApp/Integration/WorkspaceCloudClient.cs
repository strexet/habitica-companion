using System.Text.Json;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Auth;
using Habitica.Domain.Workspace;
using Habitica.Application.Diagnostics;

namespace Habitica.WebApp.Integration;

public sealed partial class WorkspaceCloudClient : IWorkspaceCloudClient
{
    private readonly HttpClient _http;
    private readonly ChatGptIntegrationClient _vault;
    private readonly Uri? _baseUri;
    private string? _token;
    private int _generation;
    private readonly bool _partyEnabled;
    private readonly DiagnosticsLogWriter? _diagnostics;
    public bool PartyPlanningAvailable => _partyEnabled && IsAvailable;
    public Guid? WorkspaceId { get; private set; }
    public bool ConnectionStatusVerified { get; private set; }
    public bool ExecutorEnabled { get; private set; }
    public bool ExecutorAvailable { get; private set; }
    public bool IsAvailable => _baseUri is not null && _vault.IsVaultAvailable;

    public WorkspaceCloudClient(HttpClient http, ChatGptIntegrationClient vault, string? baseUrl, bool partyEnabled = false, DiagnosticsLogWriter? diagnostics = null)
    {
        _http = http; _vault = vault; _partyEnabled = partyEnabled; _diagnostics = diagnostics;
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == "https" || uri.Scheme == "http" && uri.IsLoopback)
            && uri.AbsolutePath == "/" && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)) _baseUri = uri;
    }

    public async Task<WorkspaceActionResult> ConnectAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
    {
        if (!IsAvailable) return new(false, "Cloud workspace is unavailable on this app.");
        var generation = _generation;
        ConnectionStatusVerified = false;
        try
        {
            var enrolled = await _vault.EnrollProfileAsync(credentials, cancellationToken);
            if (!enrolled.Succeeded || generation != _generation) return new(false, "Cloud connection could not be confirmed. Revalidate before trying again.");
            using var request = _vault.WorkspaceHandshake(new Uri(_baseUri!, "connect"));
            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return new(false, "Cloud connection could not be confirmed. Revalidate before trying again.");
            var result = await response.Content.ReadFromJsonAsync<Connection>(cancellationToken);
            if (result is null || result.WorkspaceId == Guid.Empty || result.ConsentVersion != "workspace-v1" || result.ExpiresAtUtc <= DateTimeOffset.UtcNow
                || !System.Text.RegularExpressions.Regex.IsMatch(result.SessionToken ?? "", "^[A-Za-z0-9_-]{43}$") || generation != _generation) return new(false, "Cloud connection could not be confirmed.");
            _generation++; _linkPreparation = null; WorkspaceId = result.WorkspaceId; _token = result.SessionToken;
            ExecutorEnabled = result.ExecutorEnabled ?? false; ExecutorAvailable = result.ExecutorAvailable ?? false;
            ConnectionStatusVerified = result.ExecutorEnabled.HasValue && result.ExecutorAvailable.HasValue;
            return new(true, "Cloud workspace connected for this tab. Background execution is separately controlled. Browser data has not been uploaded.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(false, "Cloud connection result is unknown. Revalidate before managing cloud data."); }
    }

    public async Task<WorkspaceActionResult> ExportAsync(CancellationToken cancellationToken)
    {
        var generation = _generation; var id = WorkspaceId;
        if (id is null) return new(false, "Connect the cloud workspace first.");
        try
        {
            using var request = Request(HttpMethod.Get, "export");
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode) return new(false, "Cloud export is unavailable. Revalidate the connection or try later.");
            var json = await BoundedTextAsync(response.Content, cancellationToken);
            var snapshot = WorkspaceJournalJson.Deserialize(json);
            if (generation != _generation || snapshot.Document.Id != id) return new(false, "The account session changed; the export was discarded.");
            return new(true, "Cloud export includes readable task notes and command history. Keep the file private.", WorkspaceJournalJson.Serialize(snapshot));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(false, "Cloud export could not be validated. Existing data remains available."); }
    }

    public async Task<WorkspaceActionResult> DisconnectAsync(bool deleteWorkspace, CancellationToken cancellationToken)
    {
        if (WorkspaceId is null) return new(false, "Connect the cloud workspace first.");
        var generation = _generation;
        try
        {
            using var request = Request(deleteWorkspace ? HttpMethod.Delete : HttpMethod.Post, deleteWorkspace ? "workspace" : "disconnect");
            request.Content = deleteWorkspace ? JsonContent.Create(new { confirmWorkspaceId = WorkspaceId }) : JsonContent.Create(new { });
            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return new(false, "Cloud cleanup could not be confirmed. Reconnect to check its status.");
            var result = await response.Content.ReadFromJsonAsync<Cleanup>(cancellationToken);
            if (result is null || (deleteWorkspace ? !result.Deleted : !result.Disconnected)) return new(false, "Cloud cleanup could not be confirmed.");
            if (generation == _generation) ForgetSession();
            return new(true, deleteWorkspace ? "Cloud workspace and command history deleted. Browser copies and Habitica data are separate." : "Cloud workspace disconnected. Retained tasks and history remain on the server until deletion.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(false, "Cloud cleanup outcome is unknown. Reconnect to check its status."); }
    }

    public async Task<WorkspaceActionResult> SetExecutorAsync(bool enabled, CancellationToken cancellationToken)
    {
        if (WorkspaceId is null) return new(false, "Connect the cloud workspace first.");
        var generation = _generation;
        ConnectionStatusVerified = false;
        try
        {
            using var request = Request(HttpMethod.Post, "executor");
            request.Content = JsonContent.Create(new { enabled, consentVersion = enabled ? "executor-v1" : null });
            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return new(false, "Execution setting could not be confirmed. Reconnect to check its status.");
            var result = await response.Content.ReadFromJsonAsync<Executor>(cancellationToken);
            if (result is null || result.ExecutorEnabled != enabled || generation != _generation) return new(false, "Execution setting could not be confirmed.");
            ExecutorEnabled = result.ExecutorEnabled; ExecutorAvailable = result.ExecutorAvailable;
            ConnectionStatusVerified = true;
            return new(true, enabled ? "Background execution enabled. Accepted commands may affect Habitica while this tab is closed." : "Background execution paused. Already transmitted effects may still finish.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(false, "Execution setting outcome is unknown. Reconnect to check its status."); }
    }

    public async Task<WorkspaceActionResult> HandoffAsync(string archive, CancellationToken cancellationToken)
    {
        if (WorkspaceId is null) return new(false, "Connect the cloud workspace first.");
        var generation = _generation;
        try
        {
            var snapshot = WorkspaceJournalJson.Deserialize(archive);
            if (snapshot.Authority?.Mode != WorkspaceAuthorityMode.Frozen) return new(false, "Local ownership must be frozen before upload.");
            using var request = Request(HttpMethod.Post, "handoff");
            request.Content = JsonContent.Create(new { consentVersion = "workspace-handoff-v1", transferId = snapshot.Authority.TransferId, archive });
            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return new(false, "Transfer was not confirmed. The local copy remains frozen; retry the same transfer after checking cloud data.");
            var result = await response.Content.ReadFromJsonAsync<Transfer>(cancellationToken);
            if (result is null || !result.Accepted || result.WorkspaceId != snapshot.Document.Id || result.TransferId != snapshot.Authority.TransferId || generation != _generation)
                return new(false, "Transfer receipt could not be validated. The local copy remains frozen.");
            _generation++; _linkPreparation = null; WorkspaceId = result.WorkspaceId; ExecutorEnabled = result.ExecutorEnabled; ConnectionStatusVerified = false;
            return new(true, "Workspace transferred. The local copy cannot execute tasks. Background execution remains separately controlled.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(false, "Transfer outcome is unknown. The local copy remains frozen; retry the same transfer to recover its receipt."); }
    }

    public void ForgetSession() { _generation++; _linkPreparation = null; _token = null; WorkspaceId = null; ExecutorEnabled = false; ExecutorAvailable = false; ConnectionStatusVerified = false; }
    public async Task<WorkspaceActionResult> RefreshConnectionStatusAsync(CancellationToken cancellationToken)
    {
        if (WorkspaceId is not { } id || _token is null) return new(false, "Server connection status is unverified in this tab. Verify or renew access first.");
        var generation = _generation;
        ConnectionStatusVerified = false;
        try
        {
            using var request = Request(HttpMethod.Get, "workspace");
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (generation != _generation) return new(false, "The account session changed; status was discarded.");
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden) ForgetSession();
                return new(false, "Server connection status is unavailable. Verify or renew access; no command was retried.");
            }
            var payload = System.Text.Json.JsonSerializer.Deserialize<ConnectionStatus>(await BoundedTextAsync(response.Content, cancellationToken),
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
            if (generation != _generation || WorkspaceId != id || payload is null || payload.StoreVersion < 1
                || payload.Document.ValueKind != System.Text.Json.JsonValueKind.Object
                || !payload.Document.TryGetProperty("id", out var identity) || identity.ValueKind != System.Text.Json.JsonValueKind.String
                || !identity.TryGetGuid(out var returnedId) || returnedId != id)
                return new(false, "Server connection status could not be verified.");
            ExecutorEnabled = payload.ExecutorEnabled;
            ExecutorAvailable = payload.ExecutorAvailable;
            ConnectionStatusVerified = true;
            return new(true, "Server connection and background execution status verified. No command was retried.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(false, "Server connection status is unavailable. No command was retried."); }
    }

    public async Task<WorkspaceJournalSnapshot?> LoadWorkspaceAsync(CancellationToken cancellationToken)
    {
        var result = await ExportAsync(cancellationToken);
        return result.Succeeded && result.Json is not null ? WorkspaceJournalJson.Deserialize(result.Json) : null;
    }

    public async Task<WorkspaceTaskResponse<WorkspaceCommandPreview>> PrepareCommandAsync(Guid requestId, WorkspaceCommand intent, CancellationToken cancellationToken)
    {
        if (WorkspaceId != intent.WorkspaceId) return new(false, null, "The workspace connection changed.");
        var command = WorkspaceTaskApiJson.Element(intent);
        var args = new Dictionary<string, object?>
        {
            ["requestId"] = requestId, ["operation"] = command.GetProperty("operation"), ["patch"] = command.GetProperty("patch"),
            ["expectedRevision"] = intent.ExpectedRevision, ["direction"] = command.GetProperty("direction"), ["count"] = intent.Count,
            ["occurrenceDate"] = command.GetProperty("occurrenceDate"), ["projectToHabitica"] = intent.ProjectToHabitica
        };
        if (intent.Operation is not (WorkspaceOperation.CreateTodo or WorkspaceOperation.CreateDaily or WorkspaceOperation.CreateHabit)) args["taskId"] = intent.TaskId;
        var result = await CallTaskAsync<WorkspaceCommandPreview>("prepare_task_command", args, cancellationToken);
        if (result.Succeeded && result.Value is { } preview && (preview.Command.WorkspaceId != intent.WorkspaceId || preview.PreparedCommandId != preview.Command.Id || preview.Command.Operation != intent.Operation || preview.CorrelationId == Guid.Empty || preview.PreparedCommandId == Guid.Empty))
            return new(false, null, "The command preview could not be validated.", true);
        if (result.Value is { TagProjection: { } tags } && !tags.IsValid()) return new(false, null, "The tag metadata preview is invalid.", true);
        if (result.Succeeded && result.Value is {} recovery && intent.Operation is WorkspaceOperation.UnarchiveTask or WorkspaceOperation.CorrectOccurrence
            && (recovery.MayAffectHabitica || recovery.TagProjection?.RequestsMetadataChanges == true || WorkspaceCommandIdentity.Hash(recovery.Command with { Id = intent.Id }) != WorkspaceCommandIdentity.Hash(intent)))
            return new(false, null, "The native recovery preview did not match the requested command.", true);
        if (result.Succeeded && result.Value is { } plan && intent.Operation is WorkspaceOperation.PlanTask or WorkspaceOperation.RestoreTodo or WorkspaceOperation.RelateTodo or WorkspaceOperation.SetTodoState or WorkspaceOperation.SetTodoStage or WorkspaceOperation.ReviewTask
            && ((plan.MayAffectHabitica && plan.TagProjection?.RequestsMetadataChanges != true) || WorkspaceCommandIdentity.Hash(plan.Command with { Id = intent.Id }) != WorkspaceCommandIdentity.Hash(intent)))
            return new(false, null, "The native command preview did not match the requested command.", true);
        return result;
    }

    public async Task<WorkspaceTaskResponse<WorkspaceMutationReceipt>> AcceptCommandAsync(WorkspaceCommandPreview preview, CancellationToken cancellationToken)
    {
        if (WorkspaceId != preview.Command.WorkspaceId) return new(false, null, "The workspace connection changed.");
        var tool = preview.Command.Operation switch { WorkspaceOperation.CreateTodo => "create_todo", WorkspaceOperation.CreateDaily => "create_daily", WorkspaceOperation.CreateHabit => "create_habit", WorkspaceOperation.DeleteTask => "delete_task", WorkspaceOperation.UpdateTask => "update_task", WorkspaceOperation.CompleteTask => "complete_task", WorkspaceOperation.ScoreHabit => "score_habit", WorkspaceOperation.SkipOccurrence => "skip_occurrence", WorkspaceOperation.PauseTask => "pause_task", WorkspaceOperation.ResumeTask => "resume_task", WorkspaceOperation.PlanTask => "plan_task", WorkspaceOperation.UnarchiveTask => "unarchive_task", WorkspaceOperation.CorrectOccurrence => "correct_occurrence", WorkspaceOperation.RestoreTodo => "restore_todo", WorkspaceOperation.RelateTodo => "relate_todo", WorkspaceOperation.SetTodoState => "set_todo_state", WorkspaceOperation.SetTodoStage => "set_todo_stage", WorkspaceOperation.ReviewTask => "review_task", WorkspaceOperation.EnsureProjection => "ensure_task_projection", _ => throw new InvalidOperationException("Unsupported command.") };
        var result = await CallTaskAsync<WorkspaceMutationReceipt>(tool, new { commandId = preview.PreparedCommandId }, cancellationToken, preview.PreparedCommandId);
        if (result.Succeeded && result.Value is { } receipt && (receipt.TaskId != preview.Command.TaskId || receipt.Operation != System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(preview.Command.Operation.ToString())))
            return new(false, null, "The command receipt identity did not match. Check the original command before retrying.", true);
        return result;
    }

    public Task<WorkspaceTaskResponse<WorkspaceMutationReceipt>> ReadCommandAsync(Guid commandId, CancellationToken cancellationToken) =>
        CallTaskAsync<WorkspaceMutationReceipt>("get_task_command", new { commandId }, cancellationToken, commandId);

    private (Guid WorkspaceId, Guid TaskId, Guid NativeId, Guid RequestId)? _linkPreparation;

    public async Task<WorkspaceTaskResponse<WorkspaceProjectionPreview>> PreviewLinkAsync(Guid taskId, Guid habiticaTaskId, CancellationToken cancellationToken)
    {
        if (WorkspaceId is null) return new(false, null, "Connect the cloud workspace first.");
        var generation = _generation; var workspaceId = WorkspaceId;
        try
        {
            if (_linkPreparation is { } pending && pending.WorkspaceId == workspaceId && (pending.TaskId != taskId || pending.NativeId != habiticaTaskId))
                return new(false, null, "Recover the original exact link preview before choosing another record.", true);
            _linkPreparation ??= (workspaceId.Value, taskId, habiticaTaskId, Guid.NewGuid());
            using var request = Request(HttpMethod.Post, "adoption-api"); request.Content = JsonContent.Create(new { tool = "prepare_task_adoption", arguments = new { requestId = _linkPreparation.Value.RequestId, taskId, habiticaTaskId } });
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode is 400 or 401 or 403 or 404 or 409 or 422 or 429) _linkPreparation = null;
                return new(false, null, "The link preview is unavailable. Recover the original request before choosing another record.");
            }
            var payload = await BoundedTextAsync(response.Content, cancellationToken);
            using (var parsed = JsonDocument.Parse(payload))
            {
                var root = parsed.RootElement;
                if (root.TryGetProperty("state", out var state) && state.GetString() == "refused"
                    && root.GetProperty("requestId").GetGuid() == _linkPreparation.Value.RequestId && root.GetProperty("workspaceId").GetGuid() == workspaceId
                    && root.GetProperty("commandId").GetGuid() != Guid.Empty && root.GetProperty("savedInCompanion").ValueKind == JsonValueKind.False && root.GetProperty("outcomeUnknown").ValueKind == JsonValueKind.False)
                { _linkPreparation = null; return new(false, null, "The original link review expired or was refused. Request a fresh comparison."); }
            }
            var preview = WorkspaceTaskApiJson.Deserialize<WorkspaceProjectionPreview>(payload);
            if (generation != _generation || WorkspaceId != workspaceId || preview.WorkspaceId != workspaceId || preview.Task.Id != taskId || preview.External.Id != habiticaTaskId || preview.AccessEpoch == Guid.Empty || preview.ExpectedRevision < 1 || preview.ExpectedRevision >= WorkspaceDocument.MaxRevision || preview.PendingCommandCount < 0
                || preview.PreparedCommandId == Guid.Empty || preview.CommandId != preview.PreparedCommandId || preview.RequestId != _linkPreparation.Value.RequestId || preview.ConnectionId == Guid.Empty
                || preview.State != "prepared" || preview.SavedInCompanion || preview.OutcomeUnknown || preview.GameEffects != 0 || preview.NativeWrites != 0
                || !System.Text.RegularExpressions.Regex.IsMatch(preview.ReviewHash ?? "", "^[a-f0-9]{64}$") || preview.ExpiresAtUtc == default
                || preview.MappingAfter is not { } mapping || mapping.Id == Guid.Empty || mapping.State != ProjectionState.Linked || mapping.IsArchived || mapping.Fingerprint != preview.External.Fingerprint || mapping.TaskId != taskId || mapping.HabiticaTaskId != habiticaTaskId.ToString("D") || mapping.Revision != preview.ExpectedRevision + 1
                || preview.Task.Kind.ToString().ToLowerInvariant() != preview.External.Kind
                || preview.Task.Kind == CompanionTaskKind.Todo && (preview.Task.State == CompanionTaskState.Completed) != preview.External.Completed
                || preview.External.Schedule is { } schedule && (preview.External.Kind != "daily" || !schedule.IsValid())
                || !System.Text.RegularExpressions.Regex.IsMatch(preview.External.Fingerprint ?? "", "^[a-f0-9]{64}$")) return new(false, null, "The link preview could not be validated.");
            _linkPreparation = null;
            return new(true, preview, "Review the separate Companion and Habitica task states before linking.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(false, null, "The link preview could not be validated."); }
    }

    public async Task<WorkspaceActionResult> AdoptLinkAsync(WorkspaceProjectionPreview preview, CancellationToken cancellationToken)
    {
        if (WorkspaceId is null || preview.WorkspaceId != WorkspaceId || preview.PendingCommandCount != 0 || preview.PreparedCommandId == Guid.Empty || preview.ReviewHash is null)
            return new(false, "Resolve pending commands and obtain a durable exact link review before linking.");
        var generation = _generation;
        try
        {
            // Receipt recovery precedes any prospective acceptance. A later mapping revision is never used as proof.
            async Task<AdoptionCommand?> Call(string tool)
            {
                using var request = Request(HttpMethod.Post, "adoption-api");
                request.Content = JsonContent.Create(new { tool, arguments = tool == "get_task_adoption_command" ? (object)new { commandId = preview.PreparedCommandId } : new { commandId = preview.PreparedCommandId, reviewHash = preview.ReviewHash } });
                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!response.IsSuccessStatusCode) return null;
                return WorkspaceTaskApiJson.Deserialize<AdoptionCommand>(await BoundedTextAsync(response.Content, cancellationToken));
            }
            var result = await Call("get_task_adoption_command");
            bool Matches(AdoptionCommand? value) => value is not null && value.CommandId == preview.PreparedCommandId && value.RequestId == preview.RequestId
                && value.WorkspaceId == preview.WorkspaceId && value.ReviewHash == preview.ReviewHash && !value.OutcomeUnknown && value.GameEffects == 0 && value.NativeWrites == 0;
            if (!Matches(result)) return new(false, "The original link outcome is unknown. Retain this review and recover its original receipt.", OutcomeUnknown: true);
            if (result!.State == "prepared" && !result.SavedInCompanion) result = await Call("accept_task_adoption");
            if (!Matches(result) || generation != _generation) return new(false, "The original link receipt could not be validated. Keep the original review.", OutcomeUnknown: true);
            if (result!.State == "refused" && !result.SavedInCompanion && result.Receipt is null)
                return new(false, "The original review was not accepted. Cancel this review and request a fresh comparison.", KnownNotAccepted: true);
            var receipt = result.Receipt;
            if (result.State != "accepted" || !result.SavedInCompanion || receipt is null || !receipt.Adopted
                || receipt.CommandId != preview.PreparedCommandId || receipt.RequestId != preview.RequestId || receipt.ReviewHash != preview.ReviewHash
                || receipt.WorkspaceId != preview.WorkspaceId || receipt.AccessEpoch != preview.AccessEpoch || receipt.ConnectionId != preview.ConnectionId
                || receipt.TaskId != preview.Task.Id || receipt.HabiticaTaskId != preview.External.Id || receipt.MappingId != preview.MappingAfter?.Id
                || receipt.Revision != preview.ExpectedRevision + 1 || receipt.GameEffects != 0 || receipt.NativeWrites != 0 || receipt.AcceptedAtUtc == default)
                return new(false, "The link receipt could not be validated. Recover the original link before another review.", OutcomeUnknown: true);
            return new(true, "Tasks linked without changing their fields or Habitica state. Future accepted commands can request projection.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(false, "Link outcome is unknown. Recover the original command receipt before another review.", OutcomeUnknown: true); }
    }

    private async Task<WorkspaceTaskResponse<T>> CallTaskAsync<T>(string tool, object args, CancellationToken cancellationToken, Guid? expectedCommandId = null)
    {
        if (WorkspaceId is null) return new(false, default, "Connect the cloud workspace first.");
        var generation = _generation;
        try
        {
            using var request = Request(HttpMethod.Post, "task-api");
            request.Content = new StringContent(WorkspaceTaskApiJson.Serialize(new { tool, arguments = args }), System.Text.Encoding.UTF8, "application/json");
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode) return new(false, default, "The command was not confirmed. Check its original receipt; review current state before preparing another action.", (int)response.StatusCode is not (400 or 401 or 403 or 404 or 409 or 422 or 429));
            var value = WorkspaceTaskApiJson.Deserialize<T>(await BoundedTextAsync(response.Content, cancellationToken));
            if (generation != _generation) return new(false, default, "The account session changed; the response was discarded.", true);
            if (value is WorkspaceMutationReceipt receipt && (receipt.CommandId != expectedCommandId || receipt.TaskId == Guid.Empty || receipt.CorrelationId == Guid.Empty || !receipt.SavedInCompanion
                || receipt.ConfirmedProjectionCount < 0 || receipt.TotalProjectionCount is < 0 or > 11 || receipt.ConfirmedProjectionCount > receipt.TotalProjectionCount
                || receipt.AcceptedEventCount is < 0 or > 10 || receipt.FailurePosition is { } position && (position < 1 || position > receipt.TotalProjectionCount)
                || !new[] { "pending", "confirmed", "blocked", "UnknownOutcome", "notRequested", "cancelled" }.Contains(receipt.HabiticaProjectionStatus)
                || receipt.OutcomeUnknown != (receipt.HabiticaProjectionStatus == "UnknownOutcome")
                || receipt.HabiticaProjectionStatus == "confirmed" && receipt.ConfirmedProjectionCount != receipt.TotalProjectionCount))
                return new(false, default, "The command receipt could not be validated. Check the original command before retrying.", true);
            if (value is WorkspaceMutationReceipt lifecycle && lifecycle.Operation is "skipOccurrence" or "pauseTask" or "resumeTask" or "planTask" or "restoreTodo" or "relateTodo" or "setTodoState" or "setTodoStage" or "reviewTask" or "unarchiveTask" or "correctOccurrence"
                && (lifecycle.HabiticaProjectionStatus != "notRequested" || lifecycle.TotalProjectionCount != 0 || lifecycle.ConfirmedProjectionCount != 0
                    || lifecycle.AcceptedEventCount != (lifecycle.Operation is "skipOccurrence" or "restoreTodo" or "reviewTask" or "unarchiveTask" or "correctOccurrence" ? 1 : 0)))
                return new(false, default, "The Companion-only command receipt was inconsistent. Check the original command.", true);
            return new(true, value, "Workspace command state received.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(false, default, "Command outcome is unknown. Reuse the original request or command ID to check its receipt.", true); }
    }
    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, new Uri(_baseUri!, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token); return request;
    }
    private static async Task<string> BoundedTextAsync(HttpContent content, CancellationToken cancellationToken)
    {
        const int limit = 16 * 1024 * 1024;
        if (content.Headers.ContentLength > limit) throw new InvalidOperationException();
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream(); var buffer = new byte[8192]; int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0) { if (output.Length + read > limit) throw new InvalidOperationException(); await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken); }
        return System.Text.Encoding.UTF8.GetString(output.GetBuffer(), 0, (int)output.Length);
    }
    private sealed record Connection(Guid WorkspaceId, string? SessionToken, DateTimeOffset ExpiresAtUtc, string ConsentVersion, bool? ExecutorEnabled = null, bool? ExecutorAvailable = null);
    private sealed record ConnectionStatus(
        [property: System.Text.Json.Serialization.JsonRequired] System.Text.Json.JsonElement Document,
        [property: System.Text.Json.Serialization.JsonRequired] long StoreVersion,
        [property: System.Text.Json.Serialization.JsonRequired] bool ExecutorEnabled,
        [property: System.Text.Json.Serialization.JsonRequired] bool ExecutorAvailable);
    private sealed record Executor(
        [property: System.Text.Json.Serialization.JsonRequired] bool ExecutorEnabled,
        [property: System.Text.Json.Serialization.JsonRequired] bool ExecutorAvailable);
    private sealed record Transfer(bool Accepted, Guid TransferId, Guid WorkspaceId, bool ExecutorEnabled);
    private sealed record AdoptionReceipt(Guid CommandId, Guid RequestId, Guid WorkspaceId, Guid AccessEpoch, Guid ConnectionId, Guid TaskId, Guid HabiticaTaskId, Guid MappingId, string ReviewHash, long Revision, bool Adopted, DateTimeOffset AcceptedAtUtc, int GameEffects, int NativeWrites);
    private sealed record AdoptionCommand(Guid CommandId, Guid RequestId, Guid WorkspaceId, string ReviewHash, string State, bool SavedInCompanion, bool OutcomeUnknown, int GameEffects, int NativeWrites, AdoptionReceipt? Receipt = null, string? Error = null, Guid PreparedCommandId = default);
    private sealed record Cleanup(bool Deleted, bool Disconnected);
}
