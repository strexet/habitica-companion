using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Notes;
using Habitica.Storage;
namespace Habitica.WebApp.State;

public sealed class WorkspaceTransferController(IAppSessionController session,IWorkspaceCloudClient cloud,IWorkspaceTransferCloudClient remote,
    WorkspaceTransferService transfers,IBrowserWorkspaceTransfer browser,CompanionConnectionCoordinator connections,NotesController notes,TodoWorkspacePresentation todo,TodoTaskDraftStore drafts):IDisposable
{
    private string? _account;
    private long _generation;
    private bool _subscribed,_acceptanceUnknown;
    public event Action? Changed;
    public bool Busy{get;private set;}
    public string? Message{get;private set;}
    public bool IsError{get;private set;}
    public BrowserTransferState? Local{get;private set;}
    public string? ReviewJson{get;private set;}
    public string? ReceiptJson{get;private set;}
    public bool InventoryVerified {get;private set;}
    public int BrowserWorkspaceCount{get;private set;}
    public int BrowserNoteCount{get;private set;}
    public bool HasOriginalTransfer=>Local is not null;
    public bool Accepted { get { if(Local?.Acknowledged!=true||!connections.CloudSelected)return false;using var document=JsonDocument.Parse(Local.Artifact);return connections.ActiveWorkspaceId==document.RootElement.GetProperty("targetWorkspaceId").GetGuid(); } }
    public int UnsavedTaskDrafts=>drafts.UnsavedBrowserDrafts;
    public string ExportTaskDrafts()=>drafts.ExportUnsavedBrowserDrafts();
    public void DiscardTaskDrafts(){if(Busy)throw new InvalidOperationException("Wait for the transfer operation.");drafts.DiscardBrowserDrafts();Changed?.Invoke();}
    public bool CanCapture=>!Busy&&session.State.IsAuthenticated&&cloud.WorkspaceId is not null&&cloud.ConnectionStatusVerified&&!notes.Dirty&&!notes.HasPending&&!notes.Busy&&todo.CanSwitchSource&&drafts.UnsavedBrowserDrafts==0;
    public Guid? TransferId{get;private set;}
    public bool CanAccept { get { if(Busy||_acceptanceUnknown||ReviewJson is null)return false;using var doc=JsonDocument.Parse(ReviewJson);return doc.RootElement.TryGetProperty("ready",out var ready)&&ready.GetBoolean(); } }
    private void SessionChanged(){Bind();Changed?.Invoke();}
    public void Bind()
    {
        if(!_subscribed){session.Changed+=SessionChanged;_subscribed=true;}
        var account=session.State.IsAuthenticated&&Guid.TryParse(session.State.UserId,out var id)?id.ToString("D"):null;
        if(account==_account)return;_account=account;_generation++;_acceptanceUnknown=false;Local=null;ReviewJson=ReceiptJson=null;TransferId=null;Message=null;InventoryVerified=false;BrowserWorkspaceCount=BrowserNoteCount=0;
    }
    private bool Current(string account,long generation)=>generation==_generation&&_account==account&&session.State.IsAuthenticated&&Guid.TryParse(session.State.UserId,out var id)&&id.ToString("D")==account;
    public async Task RefreshAsync(CancellationToken token=default)
    {
        Bind();if(_account is not {} account)return;var generation=_generation;
        try{var inventory=await browser.InventoryAsync(account,NotesModel.Scope(account),token);if(!Current(account,generation))return;
            InventoryVerified=true;BrowserWorkspaceCount=inventory.Journals.Count;BrowserNoteCount=inventory.NotesJson is {} text?NotesJson.Deserialize(text).Notes.Count:0;
            if(inventory.PendingJson is {} pending){Local=JsonSerializer.Deserialize<BrowserTransferState>(pending,new JsonSerializerOptions(JsonSerializerDefaults.Web));using var doc=JsonDocument.Parse(Local!.Artifact);TransferId=doc.RootElement.GetProperty("transferId").GetGuid();ReceiptJson=Local.Receipt;}
            Changed?.Invoke();}
        catch(OperationCanceledException)when(token.IsCancellationRequested){throw;}
        catch{InventoryVerified=false;Say("Browser inventory is unavailable. Existing records remain preserved.",true);}
    }
    public Task PreviewAsync(bool archiveConflicts=false,CancellationToken token=default)=>Run(async(account,generation)=>
    {
        if(!cloud.ConnectionStatusVerified||cloud.WorkspaceId is not {} target)throw new InvalidOperationException("Restore verified cloud access first.");
        if(notes.Dirty||notes.HasPending||notes.Busy||!todo.CanSwitchSource||drafts.UnsavedBrowserDrafts!=0)throw new InvalidOperationException("Save, export or explicitly discard drafts and resolve original commands before transfer.");
        var captured=await transfers.CaptureAsync(account,target,token);if(!Current(account,generation))return;Local=captured;
        var(id,hash)=WorkspaceTransferService.Validate(Local,account,target);TransferId=id;
        await connections.RefreshSelectionAsync(account,token);if(!Current(account,generation))return;
        var staged=await remote.StageAsync(Local.Artifact,token);if(!Current(account,generation))return;
        if(!staged.Succeeded){Say(staged.Message,true);return;}
        using(var doc=JsonDocument.Parse(staged.Json!))if(doc.RootElement.GetProperty("transferId").GetGuid()!=id||doc.RootElement.GetProperty("artifactHash").GetString()!=hash)throw new InvalidOperationException("Staged identity mismatch.");
        var original=await remote.ReadTransferReceiptAsync(id,token);if(!Current(account,generation))return;
        if(!original.Succeeded){Say(original.Message,true);return;}
        if(original.Json is {} originalJson){using var originalDoc=JsonDocument.Parse(originalJson);if(originalDoc.RootElement.GetProperty("state").GetString()=="accepted"){await FinishAsync(account,generation,target,originalJson,token);return;}}
        var result=await remote.PreviewTransferAsync(id,archiveConflicts,token);if(!Current(account,generation))return;
        if(!result.Succeeded){Say(result.Message,true);return;}
        using var review=JsonDocument.Parse(result.Json!);if(review.RootElement.TryGetProperty("state",out var state)&&state.GetString()=="accepted"){await FinishAsync(account,generation,target,result.Json!,token);return;}
        if(review.RootElement.GetProperty("transferId").GetGuid()!=id||review.RootElement.GetProperty("artifactHash").GetString()!=hash||review.RootElement.GetProperty("workspaceId").GetGuid()!=target)throw new InvalidOperationException("Review identity mismatch.");
        _acceptanceUnknown=false;ReviewJson=result.Json;Say("Review exact sources, counts and conflicts. Original source archives remain readable; migration triggers no game effects.");
    },token);
    public Task AcceptAsync(CancellationToken token=default)=>Run(async(account,generation)=>
    {
        if(_acceptanceUnknown)throw new InvalidOperationException("Recover the original receipt before another acceptance. A fresh review of the same transfer fences the prior unaccepted review.");
        if(Local is null||ReviewJson is null||TransferId is not {} id||cloud.WorkspaceId is not {} target)throw new InvalidOperationException("Review the original staged transfer first.");
        using var review=JsonDocument.Parse(ReviewJson);var value=review.RootElement;
        if(!value.GetProperty("ready").GetBoolean())throw new InvalidOperationException("Resolve the reviewed identity conflicts first.");
        _acceptanceUnknown=true;
        var result=await remote.AcceptTransferAsync(id,value.GetProperty("reviewHash").GetString()!,token);if(!Current(account,generation))return;
        if(result.Succeeded)await FinishAsync(account,generation,target,result.Json!,token);else {if(result.KnownNotAccepted)_acceptanceUnknown=false;Say(result.Message+" Preserve the original transfer ID; recover the original receipt.",true);}
    },token);
    public async Task<WorkspaceActionResult> ExportOriginalAsync(CancellationToken token=default)
    {
        Bind();if(TransferId is not {} id||_account is not {} account)return new(false,"Original transfer is unavailable.");var generation=_generation;
        var result=await remote.ExportTransferAsync(id,token);return Current(account,generation)?result:new(false,"The account changed; private archive response discarded.");
    }
    public Task RecoverAsync(CancellationToken token=default)=>Run(async(account,generation)=>
    {
        if(Local is null||TransferId is not {} id||cloud.WorkspaceId is not {} target)throw new InvalidOperationException("Load the original browser transfer and restore exact cloud access first.");
        var result=await remote.ReadTransferReceiptAsync(id,token);if(!Current(account,generation))return;
        if(result.Succeeded&&result.Json is {} json){using var doc=JsonDocument.Parse(json);if(doc.RootElement.GetProperty("state").GetString()=="accepted"){await FinishAsync(account,generation,target,json,token);return;}}
        Say("No accepted original receipt is visible. Sources remain frozen. Review the same transfer after restoring access.",true);
    },token);
    private async Task FinishAsync(string account,long generation,Guid target,string receipt,CancellationToken token)
    {
        if(!Current(account,generation)||Local is null)return;
        using(var source=JsonDocument.Parse(Local.Artifact))
        using(var accepted=JsonDocument.Parse(receipt))
            if(source.RootElement.GetProperty("notes").ValueKind!=JsonValueKind.Null
                &&accepted.RootElement.GetProperty("notebookId").GetGuid()!=notes.CloudNotebookId)
                throw new InvalidOperationException("The original Notes target differs from current access. Preserve the original receipt and source archive for review.");
        if(!await transfers.AcknowledgeAsync(account,target,Local,receipt,token))throw new InvalidOperationException("Browser acknowledgement changed. Read the original receipt again.");
        if(!Current(account,generation))return;
        _acceptanceUnknown=false;ReceiptJson=receipt;Local=Local with{Acknowledged=true,Receipt=receipt};ReviewJson=null;
        await connections.RememberConnectionAsync(account,true,notes.CloudConnected,token,activateWorkspace:true);if(!Current(account,generation))return;
        await session.SyncAppDataSectionAsync(Habitica.Application.Sync.CloudSyncSection.CompanionPreferences,token);
        if(Current(account,generation))Say("Transfer confirmed. Tasks and Notes use cloud. Frozen browser copies retain original recovery evidence; imported pending effects remain paused.");
    }
    private async Task Run(Func<string,long,Task> action,CancellationToken token)
    {
        Bind();if(Busy||_account is not {} account)return;var generation=_generation;Busy=true;IsError=false;Changed?.Invoke();
        try{await action(account,generation);}
        catch(OperationCanceledException)when(token.IsCancellationRequested){throw;}
        catch(InvalidOperationException error){if(Current(account,generation))Say(error.Message,true);}
        catch{if(Current(account,generation))Say("Transfer did not finish. Original frozen records and transfer identity remain available for recovery.",true);}
        finally{Busy=false;Changed?.Invoke();}
    }
    private void Say(string message,bool error=false){Message=message;IsError=error;Changed?.Invoke();}
    public void Dispose(){if(_subscribed)session.Changed-=SessionChanged;_generation++;_acceptanceUnknown=false;Local=null;ReviewJson=ReceiptJson=null;}
}
