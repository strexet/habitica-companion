using Habitica.Application.Workspace;
using Habitica.Domain.Auth;
using Habitica.Domain.Workspace;
using Habitica.Storage;
using Habitica.WebApp.State;
using Microsoft.JSInterop;

namespace Habitica.WebApp.Tests.Workspace;

internal sealed class WorkspaceTaskFixture
{
    public const string User = "00000000-0000-4000-8000-000000000011";
    public FakeAppSessionController Session { get; } = new(SessionViewModel.Empty with { IsAuthenticated = true, UserId = User });
    public Runtime Js { get; } = new();
    public Cloud Client { get; } = new();
    public IndexedDbWorkspaceDatabase Database { get; }
    public WorkspaceTaskController Controller { get; }
    public WorkspaceTaskFixture(Habitica.Application.Diagnostics.DiagnosticsLogWriter? diagnostics = null) { Database = new(Js); Controller = new(Session, Client, Database, diagnostics); }
    public static WorkspaceTaskInput Todo(string text = "Synthetic Companion task") => new(WorkspaceOperation.CreateTodo, null, text, "Private synthetic notes", CompanionPriority.High, null, null, false, HabitScoreDirection.Up, 1, null);
    internal sealed class Runtime : IJSRuntime, IJSObjectReference
    {
        public Dictionary<(string Scope, string Id), string> Rows { get; } = new();
        public bool LoseCommitAcknowledgement { get; set; }
        public bool FailReadAfterCommit { get; set; }
        public bool FailReads { get; set; }
        public bool FailLists { get; set; }
        public ValueTask<T> InvokeAsync<T>(string name, object?[]? args) => InvokeAsync<T>(name, default, args);
        public ValueTask<T> InvokeAsync<T>(string name, CancellationToken token, object?[]? args)
        {
            token.ThrowIfCancellationRequested(); object? result = null;
            if (name == "import") result = this;
            else if (name == "listWorkspaces") { if (FailLists) throw new JSException("Synthetic list failure"); result = Rows.Keys.Where(key => key.Scope == (string)args![0]!).Select(key => key.Id).ToArray(); }
            else if (name == "getWorkspace")
            {
                if (FailReads) throw new JSException("Synthetic read failure");
                result = Rows.GetValueOrDefault(((string)args![0]!, (string)args[1]!));
            }
            else if (name == "commitWorkspace")
            {
                var scope = (string)args![0]!; var expected = (long)args[1]!; var json = (string)args[2]!;
                var snapshot = WorkspaceJournalJson.Deserialize(json); var key = (scope, snapshot.Document.Id.ToString("D"));
                var version = Rows.TryGetValue(key, out var old) ? WorkspaceJournalJson.Deserialize(old).StoreVersion : 0;
                result = expected == version;
                if ((bool)result)
                {
                    Rows[key] = json;
                    if (snapshot.Commands.Count > 0 && FailReadAfterCommit) FailReads = true;
                    if (snapshot.Commands.Count > 0 && LoseCommitAcknowledgement) { LoseCommitAcknowledgement = false; throw new JSException("Synthetic acknowledgement loss"); }
                }
            }
            return ValueTask.FromResult(result is null ? default! : (T)result);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    internal sealed class Cloud : IWorkspaceCloudClient
    {
        public WorkspaceJournalSnapshot State { get; set; } = new(new WorkspaceDocument { Id = Guid.NewGuid() }, 1, [], []);
        public bool Connected { get; set; }
        public bool PartyPlanningAvailable { get; set; }
        public List<PartyPlanningRequest> PartyRequests { get; } = [];
        public Func<PartyPlanningRequest, Task<PartyPlanningResult>> PartyAction { get; set; } = _ => Task.FromResult(new PartyPlanningResult(false, "Unavailable"));
        public Task<PartyPlanningResult> ManagePartyAsync(PartyPlanningRequest request, CancellationToken token) { PartyRequests.Add(request); return PartyAction(request); }
        public int StatusReads { get; private set; }
        public Func<Task<WorkspaceStatusResult>> StatusAction { get; set; } = () => Task.FromResult(new WorkspaceStatusResult(false, "Unavailable"));
        public Task<WorkspaceStatusResult> ReadOperationalStatusAsync(CancellationToken token) { StatusReads++; return StatusAction(); }
        public bool LoseAcceptance { get; set; }
        public bool FailLoads { get; set; }
        public TaskCompletionSource? LoadGate { get; set; }
        public TaskCompletionSource? LoadEntered { get; set; }
        public Func<CancellationToken, Task<WorkspaceJournalSnapshot?>>? LoadAction { get; set; }
        public List<Guid> PreparationIds { get; } = [];
        public List<Guid> AcceptedIds { get; } = [];
        public bool IsAvailable => true;
        public Guid? WorkspaceId => Connected ? State.Document.Id : null;
        public bool ConnectionStatusVerified { get; set; } = true;
        public int ConnectionStatusReads { get; private set; }
        public Func<Task<WorkspaceActionResult>>? ConnectionStatusAction { get; set; }
        public Task<WorkspaceActionResult> RefreshConnectionStatusAsync(CancellationToken token)
        { ConnectionStatusReads++; return ConnectionStatusAction?.Invoke() ?? Task.FromResult(new WorkspaceActionResult(true, "Synthetic server status verified.")); }
        public bool ExecutorEnabled { get; set; }
        public bool ExecutorAvailable { get; set; }
        public async Task<WorkspaceJournalSnapshot?> LoadWorkspaceAsync(CancellationToken token) { if (LoadAction is not null) return await LoadAction(token); LoadEntered?.TrySetResult(); if (LoadGate is not null) await LoadGate.Task; return FailLoads ? null : State; }
        public Task<WorkspaceTaskResponse<WorkspaceCommandPreview>> PrepareCommandAsync(Guid requestId, WorkspaceCommand intent, CancellationToken token)
        {
            PreparationIds.Add(requestId); var command = intent with { Id = Guid.NewGuid(), TaskId = intent.Operation == WorkspaceOperation.CreateTodo ? Guid.NewGuid() : intent.TaskId };
            var preview = new WorkspaceCommandPreview(command.Id, requestId, DateTimeOffset.UtcNow.AddMinutes(10), false, command, true, false);
            return Task.FromResult(new WorkspaceTaskResponse<WorkspaceCommandPreview>(true, preview, "Synthetic preview"));
        }
        public Task<WorkspaceTaskResponse<WorkspaceMutationReceipt>> AcceptCommandAsync(WorkspaceCommandPreview preview, CancellationToken token)
        {
            AcceptedIds.Add(preview.PreparedCommandId); var result = WorkspaceCommandEngine.Accept(State with { Authority = null }, preview.Command, DateTimeOffset.UtcNow); State = result.Snapshot!;
            if (LoseAcceptance) { LoseAcceptance = false; return Task.FromResult(new WorkspaceTaskResponse<WorkspaceMutationReceipt>(false, null, "Synthetic lost response", true)); }
            return Task.FromResult(new WorkspaceTaskResponse<WorkspaceMutationReceipt>(true, WorkspaceTaskController.Receipt(result.Entry!, preview.CorrelationId), "Synthetic receipt"));
        }
        public Task<WorkspaceTaskResponse<WorkspaceMutationReceipt>> ReadCommandAsync(Guid id, CancellationToken token)
        {
            var entry = State.Commands.SingleOrDefault(entry => entry.Command.Id == id);
            return Task.FromResult(new WorkspaceTaskResponse<WorkspaceMutationReceipt>(entry is not null, entry is null ? null : WorkspaceTaskController.Receipt(entry, id), "Synthetic receipt lookup"));
        }
        public Task<WorkspaceActionResult> ConnectAsync(HabiticaCredentials credentials, CancellationToken token) => throw new NotSupportedException();
        public Task<WorkspaceActionResult> ExportAsync(CancellationToken token) => throw new NotSupportedException();
        public Task<WorkspaceActionResult> SetExecutorAsync(bool enabled, CancellationToken token) => throw new NotSupportedException();
        public Task<WorkspaceActionResult> HandoffAsync(string json, CancellationToken token) => throw new NotSupportedException();
        public Task<WorkspaceActionResult> DisconnectAsync(bool delete, CancellationToken token) => throw new NotSupportedException();
        public Task<WorkspaceTaskResponse<WorkspaceProjectionPreview>> PreviewLinkAsync(Guid taskId, Guid externalId, CancellationToken token) => throw new NotSupportedException();
        public Task<WorkspaceActionResult> AdoptLinkAsync(WorkspaceProjectionPreview preview, CancellationToken token) => throw new NotSupportedException();
        public void ForgetSession() => Connected = false;
    }
}
