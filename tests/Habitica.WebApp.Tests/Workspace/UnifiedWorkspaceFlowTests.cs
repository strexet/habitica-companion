using System.Text.Json;
using System.Text.Json.Nodes;
using Bunit;
using Habitica.Application.Integration;
using Habitica.Application.Notes;
using Habitica.Application.Workspace;
using Habitica.Domain.Auth;
using Habitica.Domain.Notes;
using Habitica.Domain.Workspace;
using Habitica.Storage;
using Habitica.WebApp.Components;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Workspace;

public sealed class UnifiedWorkspaceFlowTests:BunitContext
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    private static string Fixture(string name)=>File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","workspace-transfer",name));
    private sealed class Integration:IChatGptIntegrationClient
    {
        public int Recoveries;public bool IsAvailable=>true;public bool HasConnection{get;private set;}=true;
        public IntegrationRecoveryDescriptor? RecoveryDescriptor=>new("integration-recovery-v1","prf_"+new string('a',43),Guid.Parse("30000000-0000-4000-8000-000000000001"));
        public Task<IntegrationRecoveryResult> RecoverAsync(HabiticaCredentials credentials,IntegrationRecoveryDescriptor? descriptor,bool acknowledged,CancellationToken token){Recoveries++;HasConnection=true;return Task.FromResult(new IntegrationRecoveryResult(true,"recovered",RecoveryDescriptor));}
        public Task<IntegrationStatusResult> GetStatusAsync(CancellationToken token)=>Task.FromResult(new IntegrationStatusResult(true,"Existing access verified","Synthetic account",["tasks:read","notes.read"],"last_verified"));
        public Task<IntegrationLinkResult> EnrollAsync(HabiticaCredentials c,CancellationToken t)=>throw new InvalidOperationException("Unexpected enrollment");
        public Task<IntegrationLinkResult> CreateLinkAsync(CancellationToken t)=>throw new InvalidOperationException();
        public Task<IntegrationLinkResult> DisconnectAsync(bool d,CancellationToken t)=>throw new InvalidOperationException();
        public Task<IntegrationLinkResult> RevokeGrantsAsync(CancellationToken t)=>throw new InvalidOperationException();
        public Task<IntegrationLinkResult> RotateManagementAsync(CancellationToken t)=>throw new InvalidOperationException();
        public Task<IntegrationLinkResult> RevalidateAsync(HabiticaCredentials c,CancellationToken t)=>throw new InvalidOperationException("Unexpected credential replacement");
        public void ForgetSession()=>HasConnection=false;
    }
    private sealed class Browser(WorkspaceTaskFixture tasks,Habitica.WebApp.Tests.Notes.NotesFixture.MemoryFactory notes):IBrowserWorkspaceTransfer,IBrowserWorkspaceSource
    {
        public string? Pending;public int Captures;public bool LoseAcknowledgement;public TaskCompletionSource<BrowserTransferState>? CaptureDelay;public BrowserTransferState? Captured;
        public Task<BrowserWorkspaceSelection> EnsureDefaultAsync(string account,CancellationToken token)
        {
            var values=tasks.Js.Rows.Where(pair=>pair.Key.Scope==account).Select(pair=>WorkspaceJournalJson.Deserialize(pair.Value)).ToArray();
            return Task.FromResult(values.Any(value=>value.Authority?.Mode is WorkspaceAuthorityMode.Frozen or WorkspaceAuthorityMode.Cloud)?new BrowserWorkspaceSelection(null,values.Length>1,true):new(values.Length==1?values[0].Document.Id:null,values.Length>1,false));
        }
        public Task<BrowserTransferInventory> InventoryAsync(string account,string scope,CancellationToken token)=>Task.FromResult(new BrowserTransferInventory(tasks.Js.Rows.Where(pair=>pair.Key.Scope==account).Select(pair=>pair.Value).ToArray(),notes.Stores.GetValueOrDefault(scope)?.Book is {} book?NotesJson.Serialize(book):null,Pending));
        public Task<BrowserTransferState> FreezeAsync(string account,string scope,Guid id,Guid target,IReadOnlyList<BrowserTransferPlan> plans,CancellationToken token)
        {
            if(Pending is {} existing)return Task.FromResult(JsonSerializer.Deserialize<BrowserTransferState>(existing,Json)!);Captures++;
            foreach(var plan in plans){var value=WorkspaceJournalJson.Deserialize(plan.FrozenJson);tasks.Js.Rows[(account,value.Document.Id.ToString("D"))]=plan.FrozenJson;}
            var artifact=JsonSerializer.Serialize(new{version="companion-adoption-v1",transferId=id,ownerScope=scope,targetWorkspaceId=target,workspaces=plans.Where(plan=>!plan.RetainCloud).Select(plan=>new{workspaceId=WorkspaceJournalJson.Deserialize(plan.FrozenJson).Document.Id,archive=plan.FrozenJson}),notes=NotesJson.Serialize(notes.Stores[scope].Book!)},Json);
            var state=new BrowserTransferState(artifact,false,null);Pending=JsonSerializer.Serialize(state,Json);Captured=state;return CaptureDelay?.Task??Task.FromResult(state);
        }
        public Task<bool> AcknowledgeAsync(string account,string scope,Guid id,string hash,string receipt,CancellationToken token)
        {
            var state=JsonSerializer.Deserialize<BrowserTransferState>(Pending!,Json)!;Pending=JsonSerializer.Serialize(state with{Acknowledged=true,Receipt=receipt},Json);
            if(LoseAcknowledgement){LoseAcknowledgement=false;return Task.FromResult(false);}return Task.FromResult(true);
        }
    }
    private sealed class Remote(WorkspaceTaskFixture tasks,Habitica.WebApp.Tests.Notes.NotesFixture.Cloud notes):IWorkspaceTransferCloudClient
    {
        private string? _artifact,_review,_receipt;public int Accepts;public bool LoseReply,Conflict;
        public Task<WorkspaceActionResult> StageAsync(string artifact,CancellationToken token){_artifact??=artifact;Assert.Equal(_artifact,artifact);var state=JsonNode.Parse(artifact)!;return Result(new{transferId=state["transferId"]!.GetValue<Guid>(),artifactHash=Hash(artifact),state="staged",staged=true});}
        private static string Hash(string text)=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        private static Task<WorkspaceActionResult> Result(object value)=>Task.FromResult(new WorkspaceActionResult(true,"Synthetic D1-backed fixture",JsonSerializer.Serialize(value,Json)));
        public Task<WorkspaceActionResult> PreviewTransferAsync(Guid id,bool archiveConflicts,CancellationToken token)
        {
            if(_receipt is not null)return Task.FromResult(new WorkspaceActionResult(true,"Original accepted receipt",_receipt));
            var review=JsonNode.Parse(Fixture("review.json"))!;review["transferId"]=id;review["artifactHash"]=Hash(_artifact!);review["ready"]=!Conflict||archiveConflicts;review["archiveConflicts"]=archiveConflicts;review["expiresAtUtc"]=DateTimeOffset.UtcNow.AddMinutes(10);review["reviewHash"]=null;
            if(Conflict)review["conflicts"]=JsonSerializer.SerializeToNode(new[]{new{kind="tasks",id=Guid.NewGuid(),sourceWorkspaceId="synthetic-source",retainedSource="cloud"}});
            review["reviewHash"]=Hash(review.ToJsonString());_review=review.ToJsonString();return Task.FromResult(new WorkspaceActionResult(true,"Synthetic exact review",_review));
        }
        public Task<WorkspaceActionResult> AcceptTransferAsync(Guid id,string reviewHash,CancellationToken token)
        {
            if(_receipt is not null)return Task.FromResult(new WorkspaceActionResult(true,"Original receipt",_receipt));
            Assert.Equal(reviewHash,JsonNode.Parse(_review!)!["reviewHash"]!.GetValue<string>());Accepts++;
            var accepted=WorkspaceJournalJson.Deserialize(Fixture("accepted-journal.json"));tasks.Client.State=accepted with{Document=accepted.Document with{SourceImports=accepted.Document.SourceImports!.Select(origin=>origin with{TransferId=id}).ToArray()}};
            var book=NotesJson.Deserialize(Fixture("accepted-notes.json"));notes.Book=book with{Transfers=book.Transfers!.Select(marker=>marker with{TransferId=id}).ToArray()};
            var receipt=JsonNode.Parse(Fixture("accepted-receipt.json"))!;receipt["transferId"]=id;receipt["artifactHash"]=Hash(_artifact!);receipt["reviewHash"]=reviewHash;_receipt=receipt.ToJsonString();
            return Task.FromResult(LoseReply?new WorkspaceActionResult(false,"Synthetic lost acknowledgement",OutcomeUnknown:true):new WorkspaceActionResult(true,"Original acceptance",_receipt));
        }
        public Task<WorkspaceActionResult> ReadTransferReceiptAsync(Guid id,CancellationToken token)=>Task.FromResult(new WorkspaceActionResult(true,"Original receipt",_receipt??JsonSerializer.Serialize(new{transferId=id,state="staged",outcomeUnknown=true},Json)));
        public Task<WorkspaceActionResult> ExportTransferAsync(Guid id,CancellationToken token)=>Result(new{artifact=_artifact,receipt=_receipt});
    }
    private sealed record Flow(WorkspaceTaskFixture Tasks,Habitica.WebApp.Tests.Notes.NotesFixture.MemoryFactory Database,Habitica.WebApp.Tests.Notes.NotesFixture.Cloud CloudNotes,CompanionConnectionCoordinator Connections,CompanionPreferencesStore Preferences,InMemoryKeyValueStorage Storage,Integration Integration,Browser Browser,Remote Remote,TodoWorkspacePresentation Todo,NotesController Notes,WorkspaceTransferController Transfer);
    private async Task<Flow> Setup()
    {
        JSInterop.Mode=JSRuntimeMode.Loose;Services.AddMudServices();var tasks=new WorkspaceTaskFixture();tasks.Client.Connected=true;tasks.Client.State=WorkspaceJournalJson.Deserialize(Fixture("target-before-journal.json"));
        using var artifact=JsonDocument.Parse(Fixture("source-artifact.json"));var source=WorkspaceJournalJson.Deserialize(artifact.RootElement.GetProperty("workspaces")[0].GetProperty("archive").GetString()!);source=source with{Authority=null};tasks.Js.Rows[(WorkspaceTaskFixture.User,source.Document.Id.ToString("D"))]=WorkspaceJournalJson.Serialize(source);
        var db=new Habitica.WebApp.Tests.Notes.NotesFixture.MemoryFactory();var local=NotesJson.Deserialize(artifact.RootElement.GetProperty("notes").GetString()!);((Habitica.WebApp.Tests.Notes.NotesFixture.MemoryStore)db.ForScope(local.OwnerScope)).Book=local;
        var cloudNotes=new Habitica.WebApp.Tests.Notes.NotesFixture.Cloud{Connected=true,Book=NotesModel.Empty(NotesJson.Deserialize(Fixture("accepted-notes.json")).OwnerScope)};using(var receipt=JsonDocument.Parse(Fixture("accepted-receipt.json")))cloudNotes.Id=receipt.RootElement.GetProperty("notebookId").GetGuid();
        var storage=new InMemoryKeyValueStorage();var preferences=new CompanionPreferencesStore(storage,new());var browser=new Browser(tasks,db);var integration=new Integration();var connections=new CompanionConnectionCoordinator(preferences,browser,integration,tasks.Client,cloudNotes);connections.BindAccount(WorkspaceTaskFixture.User);await connections.RefreshSelectionAsync(WorkspaceTaskFixture.User,default);
        var todo=new TodoWorkspacePresentation(tasks.Session,tasks.Client,tasks.Database,tasks.Controller,connections:connections);await todo.RefreshSourcesAsync();
        var notes=new NotesController(tasks.Session,db,cloudNotes,new NotesDraftSession(),connections:connections);await notes.LoadAsync();var remote=new Remote(tasks,cloudNotes);var transfer=new WorkspaceTransferController(tasks.Session,tasks.Client,remote,new WorkspaceTransferService(browser),browser,connections,notes,todo,new TodoTaskDraftStore(tasks.Session));
        tasks.Session.ExistingRecoveryAction=async()=>{await connections.BootstrapAsync(new(WorkspaceTaskFixture.User,"fixture-only"),true,true,default);return new(true,"Existing access restored without enrollment.");};
        Services.AddSingleton<IAppSessionController>(tasks.Session);Services.AddSingleton<IWorkspaceCloudClient>(tasks.Client);Services.AddSingleton(tasks.Database);Services.AddSingleton(tasks.Controller);Services.AddSingleton(todo);Services.AddSingleton(notes);Services.AddSingleton(connections);Services.AddSingleton(transfer);Services.AddSingleton(new WorkspaceDataController(tasks.Session,tasks.Client,tasks.Database,connections:connections));Services.AddSingleton<TodoTaskDraftStore>();
        return new(tasks,db,cloudNotes,connections,preferences,storage,integration,browser,remote,todo,notes,transfer);
    }
    private static void Evidence(string name,string markup,string state,string test)=>Qualification.PageLayoutEvidence.Save(name,markup,"/todo/sync",state,["form","actions","confirmation","disabled-controls","error"],test);
    private static void ChooseAndReview(IRenderedComponent<WorkspaceTransferPanel> cut)
    {
        cut.Find("[data-testid=workspace-choose-cloud]").Click();Assert.True(cut.Find("[data-testid=workspace-move-continue]").HasAttribute("disabled"));cut.Find("[data-testid=workspace-move-task-consent]").Change(true);cut.Find("[data-testid=workspace-move-notes-consent]").Change(true);cut.Find("[data-testid=workspace-move-continue]").Click();cut.WaitForAssertion(()=>Assert.Single(cut.FindAll("[data-testid=workspace-transfer-review]")));
    }
    [Fact]public async Task Reviewed_move_preserves_populated_target_and_Notes_and_shares_one_source_without_enrollment()
    {
        var f=await Setup();var cut=Render<WorkspaceTransferPanel>();Assert.Equal(TodoWorkspaceSource.Browser,f.Todo.Selection!.Source);Assert.Equal(NotesSource.Browser,f.Notes.Source);ChooseAndReview(cut);Assert.False(f.Connections.CloudSelected);Assert.False(f.Notes.CanWrite);Assert.True(cut.Find("[data-testid=workspace-transfer-accept]").HasAttribute("disabled"));Evidence("workspace-unified-review",cut.Markup,"exact-source-review",nameof(Reviewed_move_preserves_populated_target_and_Notes_and_shares_one_source_without_enrollment));
        cut.Find("[data-testid=workspace-transfer-accept-check]").Change(true);cut.Find("[data-testid=workspace-transfer-accept]").Click();cut.WaitForAssertion(()=>Assert.Single(cut.FindAll("[data-testid=workspace-transfer-confirmed]")));
        await f.Todo.RefreshSourcesAsync();await f.Notes.LoadAsync();Assert.Equal(TodoWorkspaceSource.Cloud,f.Todo.Selection!.Source);Assert.Equal(NotesSource.Cloud,f.Notes.Source);Assert.Equal(2,f.Todo.Snapshot!.Document.Tasks.Count);Assert.Equal("Later 😀",Assert.Single(f.Notes.Book!.Notes).Current.Markdown);Assert.Equal(1,f.Remote.Accepts);Assert.Equal(0,f.Tasks.Session.WorkspaceConnectCalls);Assert.Empty(f.Tasks.Client.AcceptedIds);Assert.Empty(cut.FindAll("select"));
        Evidence("workspace-unified-accepted",cut.Markup,"accepted-one-cloud-source",nameof(Reviewed_move_preserves_populated_target_and_Notes_and_shares_one_source_without_enrollment));
    }
    [Theory][InlineData(false)][InlineData(true)]public async Task Lost_acceptance_or_local_acknowledgement_recovers_the_original_transfer(bool localAcknowledgement)
    {
        var f=await Setup();f.Remote.LoseReply=!localAcknowledgement;f.Browser.LoseAcknowledgement=localAcknowledgement;var cut=Render<WorkspaceTransferPanel>();ChooseAndReview(cut);var id=f.Transfer.TransferId;cut.Find("[data-testid=workspace-transfer-accept-check]").Change(true);cut.Find("[data-testid=workspace-transfer-accept]").Click();cut.WaitForAssertion(()=>Assert.True(f.Transfer.IsError));Assert.False(f.Connections.CloudSelected);Assert.False(f.Transfer.Accepted);
        await f.Transfer.RefreshAsync();cut.Find("[data-testid=workspace-transfer-recover]").Click();cut.WaitForAssertion(()=>Assert.True(f.Transfer.Accepted));Assert.Equal(id,f.Transfer.TransferId);Assert.Equal(1,f.Remote.Accepts);Assert.Equal(1,f.Browser.Captures);Evidence("workspace-unified-recovered-"+localAcknowledgement,cut.Markup,"original-receipt-recovery",nameof(Lost_acceptance_or_local_acknowledgement_recovers_the_original_transfer));
    }
    [Fact]public async Task Conflict_and_dirty_draft_require_explicit_resolution_before_acceptance()
    {
        var f=await Setup();f.Notes.Draft.Markdown="Private unsaved Notes";var cut=Render<WorkspaceTransferPanel>();cut.Find("[data-testid=workspace-choose-cloud]").Click();cut.Find("[data-testid=workspace-move-task-consent]").Change(true);cut.Find("[data-testid=workspace-move-notes-consent]").Change(true);Assert.True(cut.Find("[data-testid=workspace-move-continue]").HasAttribute("disabled"));Assert.Equal(0,f.Browser.Captures);Assert.Equal("Private unsaved Notes",f.Notes.Draft.Markdown);Evidence("workspace-unified-draft",cut.Markup,"private-draft-blocks-capture",nameof(Conflict_and_dirty_draft_require_explicit_resolution_before_acceptance));
        Assert.True(f.Notes.NewNote(discard:true));f.Remote.Conflict=true;cut.Find("[data-testid=workspace-move-continue]").Click();cut.WaitForAssertion(()=>Assert.Single(cut.FindAll("[data-testid=workspace-conflict-consent]")));Assert.Empty(cut.FindAll("[data-testid=workspace-transfer-accept]"));cut.Find("[data-testid=workspace-conflict-consent]").Change(true);cut.Find("[data-testid=workspace-review-conflicts]").Click();cut.WaitForAssertion(()=>Assert.Single(cut.FindAll("[data-testid=workspace-transfer-accept]")));Assert.True(cut.Find("[data-testid=workspace-transfer-accept]").HasAttribute("disabled"));Evidence("workspace-unified-conflicts",cut.Markup,"explicit-conflict-archival-review",nameof(Conflict_and_dirty_draft_require_explicit_resolution_before_acceptance));
    }
    [Fact]public async Task Offline_cloud_keeps_the_same_source_and_removes_ordinary_browser_choices()
    {
        var f=await Setup();await f.Connections.RememberConnectionAsync(WorkspaceTaskFixture.User,true,true,default,activateWorkspace:true);f.Tasks.Client.ConnectionStatusVerified=false;await f.Connections.RefreshSelectionAsync(WorkspaceTaskFixture.User,default);
        var cut=Render<WorkspaceTransferPanel>();Assert.Equal(CompanionConnectionState.CloudUnavailable,f.Connections.State);Assert.Contains("Cloud remains selected",cut.Markup);Assert.Empty(cut.FindAll("select"));Evidence("workspace-unified-offline",cut.Markup,"cloud-unavailable-no-fork",nameof(Offline_cloud_keeps_the_same_source_and_removes_ordinary_browser_choices));
        var notes=Render<NotesPanel>();Assert.Empty(notes.FindAll("[data-testid=note-source]"));Assert.Equal(NotesSource.Cloud,f.Notes.Source);Qualification.PageLayoutEvidence.Save("notes-unified-cloud",notes.Markup,"/notes","shared-cloud-source",["form","disabled-controls"],nameof(Offline_cloud_keeps_the_same_source_and_removes_ordinary_browser_choices));
    }
    [Fact]public async Task Account_change_discards_private_capture_that_finishes_after_switch()
    {
        var f=await Setup();f.Browser.CaptureDelay=new(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending=f.Transfer.PreviewAsync();Assert.NotNull(f.Browser.Captured);
        f.Tasks.Session.SetState(SessionViewModel.Empty with{IsAuthenticated=true,UserId=Guid.NewGuid().ToString()});
        f.Browser.CaptureDelay.SetResult(f.Browser.Captured!);await pending;
        Assert.Null(f.Transfer.Local);Assert.Null(f.Transfer.TransferId);Assert.Null(f.Transfer.ReviewJson);Assert.Equal(0,f.Remote.Accepts);
    }
    [Fact]public async Task New_session_restores_both_exact_cloud_stores_from_non_authorizing_preferences()
    {
        var f=await Setup();await f.Transfer.RefreshAsync();await f.Transfer.PreviewAsync();await f.Transfer.AcceptAsync();Assert.True(f.Transfer.Accepted);
        var expectedWorkspace=f.Connections.ActiveWorkspaceId;var expectedNotebook=f.CloudNotes.NotebookId;var copies=f.Tasks.Js.Rows.Count;
        f.Tasks.Session.SetState(SessionViewModel.Empty);f.Connections.BindAccount(null);f.Tasks.Client.ForgetSession();f.CloudNotes.ForgetSession();f.Integration.ForgetSession();
        f.Tasks.Session.SetState(SessionViewModel.Empty with{IsAuthenticated=true,UserId=WorkspaceTaskFixture.User});
        var restored=new CompanionConnectionCoordinator(f.Preferences,f.Browser,f.Integration,f.Tasks.Client,f.CloudNotes);restored.BindAccount(WorkspaceTaskFixture.User);restored.BeginStartup(WorkspaceTaskFixture.User);
        await restored.BootstrapAsync(new(WorkspaceTaskFixture.User,"fixture-only"),false,false,default);await restored.CompleteStartupAsync(WorkspaceTaskFixture.User,default);
        Assert.Equal(CompanionConnectionState.Cloud,restored.State);Assert.Equal(expectedWorkspace,restored.ActiveWorkspaceId);Assert.Equal(expectedNotebook,f.CloudNotes.NotebookId);Assert.True(restored.NotesAccessVerified);Assert.Equal(1,f.Integration.Recoveries);Assert.Equal(1,f.Tasks.Client.ConnectionRecoveries);Assert.Equal(1,f.CloudNotes.ConnectionRecoveries);Assert.Equal(copies,f.Tasks.Js.Rows.Count);Assert.Equal(0,f.Tasks.Session.WorkspaceConnectCalls);Assert.Empty(f.Tasks.Client.AcceptedIds);
    }
}
