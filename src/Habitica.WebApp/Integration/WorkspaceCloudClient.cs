using System.Net.Http.Headers;
using System.Net.Http.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Auth;
using Habitica.Domain.Workspace;

namespace Habitica.WebApp.Integration;

public sealed partial class WorkspaceCloudClient : IWorkspaceCloudClient
{
    private readonly HttpClient _http;
    private readonly ChatGptIntegrationClient _vault;
    private readonly Uri? _baseUri;
    private string? _token;
    private int _generation;
    private readonly bool _partyEnabled;
    public bool PartyPlanningAvailable => _partyEnabled && IsAvailable;
    public Guid? WorkspaceId { get; private set; }
    public bool ExecutorEnabled { get; private set; }
    public bool ExecutorAvailable { get; private set; }
    public bool IsAvailable => _baseUri is not null && _vault.IsVaultAvailable;

    public WorkspaceCloudClient(HttpClient http, ChatGptIntegrationClient vault, string? baseUrl, bool partyEnabled = false)
    {
        _http = http; _vault = vault; _partyEnabled = partyEnabled;
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == "https" || uri.Scheme == "http" && uri.IsLoopback)
            && uri.AbsolutePath == "/" && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)) _baseUri = uri;
    }

    public async Task<WorkspaceActionResult> ConnectAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
    {
        if (!IsAvailable) return new(false, "Cloud workspace is unavailable on this app.");
        var generation = _generation;
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
            _generation++; WorkspaceId = result.WorkspaceId; _token = result.SessionToken;
            ExecutorEnabled = result.ExecutorEnabled; ExecutorAvailable = result.ExecutorAvailable;
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
        try
        {
            using var request = Request(HttpMethod.Post, "executor");
            request.Content = JsonContent.Create(new { enabled, consentVersion = enabled ? "executor-v1" : null });
            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return new(false, "Execution setting could not be confirmed. Reconnect to check its status.");
            var result = await response.Content.ReadFromJsonAsync<Executor>(cancellationToken);
            if (result is null || result.ExecutorEnabled != enabled || generation != _generation) return new(false, "Execution setting could not be confirmed.");
            ExecutorEnabled = result.ExecutorEnabled; ExecutorAvailable = result.ExecutorAvailable;
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
            _generation++; WorkspaceId = result.WorkspaceId; ExecutorEnabled = result.ExecutorEnabled;
            return new(true, "Workspace transferred. The local copy cannot execute tasks. Background execution remains separately controlled.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(false, "Transfer outcome is unknown. The local copy remains frozen; retry the same transfer to recover its receipt."); }
    }

    public void ForgetSession() { _generation++; _token = null; WorkspaceId = null; ExecutorEnabled = false; ExecutorAvailable = false; }
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
        return result;
    }

    public async Task<WorkspaceTaskResponse<WorkspaceMutationReceipt>> AcceptCommandAsync(WorkspaceCommandPreview preview, CancellationToken cancellationToken)
    {
        if (WorkspaceId != preview.Command.WorkspaceId) return new(false, null, "The workspace connection changed.");
        var tool = preview.Command.Operation switch { WorkspaceOperation.CreateTodo => "create_todo", WorkspaceOperation.CreateDaily => "create_daily", WorkspaceOperation.CreateHabit => "create_habit", WorkspaceOperation.DeleteTask => "delete_task", WorkspaceOperation.UpdateTask => "update_task", WorkspaceOperation.CompleteTask => "complete_task", WorkspaceOperation.ScoreHabit => "score_habit", WorkspaceOperation.SkipOccurrence => "skip_occurrence", WorkspaceOperation.PauseTask => "pause_task", WorkspaceOperation.ResumeTask => "resume_task", _ => throw new InvalidOperationException("Unsupported command.") };
        var result = await CallTaskAsync<WorkspaceMutationReceipt>(tool, new { commandId = preview.PreparedCommandId }, cancellationToken, preview.PreparedCommandId);
        if (result.Succeeded && result.Value is { } receipt && (receipt.TaskId != preview.Command.TaskId || receipt.Operation != System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(preview.Command.Operation.ToString())))
            return new(false, null, "The command receipt identity did not match. Check the original command before retrying.", true);
        return result;
    }

    public Task<WorkspaceTaskResponse<WorkspaceMutationReceipt>> ReadCommandAsync(Guid commandId, CancellationToken cancellationToken) =>
        CallTaskAsync<WorkspaceMutationReceipt>("get_task_command", new { commandId }, cancellationToken, commandId);

    public async Task<WorkspaceTaskResponse<WorkspaceProjectionPreview>> PreviewLinkAsync(Guid taskId, Guid habiticaTaskId, CancellationToken cancellationToken)
    {
        if (WorkspaceId is null) return new(false, null, "Connect the cloud workspace first.");
        var generation = _generation; var workspaceId = WorkspaceId;
        try
        {
            using var request = Request(HttpMethod.Post, "projection/preview"); request.Content = JsonContent.Create(new { taskId, habiticaTaskId });
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode) return new(false, null, "The link preview is unavailable. Check the task identity, connection and quota before trying again.");
            var preview = WorkspaceTaskApiJson.Deserialize<WorkspaceProjectionPreview>(await BoundedTextAsync(response.Content, cancellationToken));
            if (generation != _generation || WorkspaceId != workspaceId || preview.WorkspaceId != workspaceId || preview.Task.Id != taskId || preview.External.Id != habiticaTaskId || preview.AccessEpoch == Guid.Empty || preview.ExpectedRevision < 1 || preview.PendingCommandCount < 0
                || !System.Text.RegularExpressions.Regex.IsMatch(preview.External.Fingerprint ?? "", "^[a-f0-9]{64}$")) return new(false, null, "The link preview could not be validated.");
            return new(true, preview, "Review the separate Companion and Habitica task states before linking.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(false, null, "The link preview could not be validated."); }
    }

    public async Task<WorkspaceActionResult> AdoptLinkAsync(WorkspaceProjectionPreview preview, CancellationToken cancellationToken)
    {
        if (WorkspaceId is null || preview.WorkspaceId != WorkspaceId || preview.PendingCommandCount != 0) return new(false, "Resolve pending projected commands before linking.");
        var generation = _generation;
        try
        {
            using var request = Request(HttpMethod.Post, "projection/adopt"); request.Content = JsonContent.Create(new { taskId = preview.Task.Id, habiticaTaskId = preview.External.Id, expectedWorkspaceId = preview.WorkspaceId, expectedAccessEpoch = preview.AccessEpoch, expectedRevision = preview.ExpectedRevision, expectedFingerprint = preview.External.Fingerprint });
            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return new(false, "The link was not confirmed. Reload current task states and review a fresh preview.");
            var result = await response.Content.ReadFromJsonAsync<Adoption>(cancellationToken);
            if (result is null || !result.Adopted || result.TaskId != preview.Task.Id || result.HabiticaTaskId != preview.External.Id || result.Revision <= preview.ExpectedRevision || generation != _generation)
                return new(false, "The link receipt could not be validated. Reload before reviewing another link.");
            return new(true, "Tasks linked without changing their fields or Habitica state. Future accepted commands can request projection.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(false, "Link outcome is unknown. Reload and review current mapping before another confirmation."); }
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
            if (!response.IsSuccessStatusCode) return new(false, default, "The command was not confirmed. Check its original receipt; review current state before preparing another action.", (int)response.StatusCode >= 500);
            var value = WorkspaceTaskApiJson.Deserialize<T>(await BoundedTextAsync(response.Content, cancellationToken));
            if (generation != _generation) return new(false, default, "The account session changed; the response was discarded.", true);
            if (value is WorkspaceMutationReceipt receipt && (receipt.CommandId != expectedCommandId || receipt.TaskId == Guid.Empty || receipt.CorrelationId == Guid.Empty || !receipt.SavedInCompanion
                || receipt.ConfirmedProjectionCount < 0 || receipt.TotalProjectionCount is < 0 or > 11 || receipt.ConfirmedProjectionCount > receipt.TotalProjectionCount
                || receipt.AcceptedEventCount is < 0 or > 10 || receipt.FailurePosition is { } position && (position < 1 || position > receipt.TotalProjectionCount)
                || !new[] { "pending", "confirmed", "blocked", "UnknownOutcome", "notRequested", "cancelled" }.Contains(receipt.HabiticaProjectionStatus)
                || receipt.OutcomeUnknown != (receipt.HabiticaProjectionStatus == "UnknownOutcome")
                || receipt.HabiticaProjectionStatus == "confirmed" && receipt.ConfirmedProjectionCount != receipt.TotalProjectionCount))
                return new(false, default, "The command receipt could not be validated. Check the original command before retrying.", true);
            if (value is WorkspaceMutationReceipt lifecycle && lifecycle.Operation is "skipOccurrence" or "pauseTask" or "resumeTask"
                && (lifecycle.HabiticaProjectionStatus != "notRequested" || lifecycle.TotalProjectionCount != 0 || lifecycle.ConfirmedProjectionCount != 0
                    || lifecycle.AcceptedEventCount != (lifecycle.Operation == "skipOccurrence" ? 1 : 0)))
                return new(false, default, "The Companion-only lifecycle receipt was inconsistent. Check the original command.", true);
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
    private sealed record Connection(Guid WorkspaceId, string? SessionToken, DateTimeOffset ExpiresAtUtc, string ConsentVersion, bool ExecutorEnabled = false, bool ExecutorAvailable = false);
    private sealed record Executor(bool ExecutorEnabled, bool ExecutorAvailable);
    private sealed record Transfer(bool Accepted, Guid TransferId, Guid WorkspaceId, bool ExecutorEnabled);
    private sealed record Adoption(bool Adopted, Guid TaskId, Guid HabiticaTaskId, long Revision);
    private sealed record Cleanup(bool Deleted, bool Disconnected);
}
