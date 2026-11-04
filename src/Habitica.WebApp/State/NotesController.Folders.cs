using Habitica.Application.Notes;
using Habitica.Domain.Notes;
namespace Habitica.WebApp.State;

public sealed record NotesFolderReview(NoteFolderCommand Command,NoteFolder? Before,NoteFolder After,string Context);
public sealed partial class NotesController
{
    private NoteFolderLink? _originalFolder;
    private NoteFolderCommand? _pendingFolder;
    private Guid? _pendingFolderNotebook;
    public NoteFolderLink? DraftFolder {get;private set;}
    public NotesFolderReview? FolderReview {get;private set;}
    public NoteFolderReceipt? LastFolderReceipt {get;private set;}
    public string ContextIdentity=>$"{_account}:{Source}:{_cloud.NotebookId}";
    public IReadOnlyList<NoteFolder> Folders=>Book?.Folders?.Where(f=>!f.IsRemoved).ToArray()??[];
    public bool CanManageFolders=>CanWrite&&!Dirty&&Review is null&&ProjectReview is null&&FolderReview is null;
    private void ClearFolderContext(){_pendingFolder=null;_pendingFolderNotebook=null;FolderReview=null;LastFolderReceipt=null;DraftFolder=_originalFolder=null;}
    public string FolderLabel(NoteFolderLink? link)
    {
        if(link is null)return "Without a folder";
        var folder=CanRead&&link.OwnerScope==Book?.OwnerScope&&link.Source==ProjectSource?Book?.Folders?.FirstOrDefault(f=>f.Id==link.FolderId):null;
        return $"{folder?.Name??"Retained folder"} · {(folder is null?"unavailable":folder.IsRemoved?"removed":"active")} · {link.FolderId:D}";
    }
    public bool FolderSelectable(Guid id)=>Folders.Any(f=>f.Id==id&&f.Source==ProjectSource&&(f.Project is null||EligibleProject(f.Project)));
    public bool ChooseFolder(Guid? id)
    {
        EnsureAccount();if(!CanWrite||HasConflict||FolderReview is not null||ProjectReview is not null||Selected?.Current.IsTrashed==true)return false;
        var folder=id is {} target?Folders.FirstOrDefault(f=>f.Id==target):null;
        if(id is not null&&(folder is null||!FolderSelectable(id.Value))){Say("The folder or project is unavailable for new placement. Existing note content stays unchanged.",true);return false;}
        DraftFolder=folder is null?null:new(folder.Id,Book!.OwnerScope,folder.Source);
        if(folder is not null)Draft.Project=folder.Project;
        Changed?.Invoke();return true;
    }
    public bool NewNoteInFolder(Guid id,bool discard=false)=>FolderSelectable(id)&&NewNote(discard)&&ChooseFolder(id);
    public bool PrepareFolder(NoteFolderOperation operation,Guid? folderId,string? name,Guid? projectId=null)
    {
        EnsureAccount();if(!CanManageFolders||Book is null)return false;
        var before=folderId is {} id?Book.Folders?.FirstOrDefault(f=>f.Id==id):null;
        if(operation!=NoteFolderOperation.Create&&before is null)return false;
        var project=projectId is {} p?ProjectLink(p):null;
        if(projectId is not null&&project is null){Say("Choose an active project in the matching Workspace before creating a project folder.",true);return false;}
        var command=new NoteFolderCommand(Guid.NewGuid(),operation==NoteFolderOperation.Create?Guid.NewGuid():folderId!.Value,Book.Revision,before?.Revision??0,operation,Book.OwnerScope,ProjectSource,name,project);
        var authority=project is not null&&_projects?.Snapshot is {} snapshot?new NotesProjectAuthority(Book.OwnerScope,ProjectSource,snapshot.Document):null;
        var preview=NotesFolders.Apply(Book,command,DateTimeOffset.UtcNow,authority);
        if(!preview.Accepted){Say(preview.Code=="folder_not_empty"?"Move every note out of the folder first, including notes in trash. No notes were deleted.":preview.Code.Contains("quota")?"Folder storage capacity is reached. Existing content and receipts remain stored.":"The folder could not be reviewed. Check the name, exact folder revision and project context.",true);return false;}
        FolderReview=new(command,before,preview.Notebook.Folders!.Single(f=>f.Id==command.FolderId),ContextIdentity);Changed?.Invoke();return true;
    }
    public void CancelFolderReview(){FolderReview=null;Changed?.Invoke();}
    public Task AcceptFolderAsync(CancellationToken token=default)=>Observe("notes-folder-save",async()=>
    {
        EnsureAccount();if(FolderReview is not {} review||!CanWrite||Dirty)return;
        if(review.Context!=ContextIdentity||Book!.Revision!=review.Command.ExpectedNotebookRevision){FolderReview=null;Say("The folder context changed. Refresh and review the original intent again.",true);return;}
        await _gate.WaitAsync(token);var generation=_generation;
        try
        {
            if(!CanWrite||review.Context!=ContextIdentity)return;
            Busy=true;_pendingFolder=review.Command;_pendingFolderNotebook=Source==NotesSource.Cloud?_cloud.NotebookId:null;Changed?.Invoke();
            NoteFolderReceipt? receipt;bool accepted;bool unknown;
            if(Source==NotesSource.Browser){var result=await new NotesService(_database.ForScope(LocalScope),projects:ResolveLocalProject).SubmitFolderAsync(review.Command,token);accepted=result.Accepted;unknown=result.Code=="acceptance_unknown";receipt=result.Receipt;}
            else {var result=await _cloud.SubmitFolderAsync(review.Command,token);accepted=result.Succeeded;unknown=result.OutcomeUnknown;receipt=result.Value;}
            if(!Current(generation))return;
            if(!accepted){if(!unknown){_pendingFolder=null;FolderReview=null;}Say(unknown?"Folder outcome is unknown. Check the original receipt before another write.":"The folder action was refused or the revision changed. Refresh before reviewing another action.",true);return;}
            await ConfirmFolderAsync(receipt!,generation,token);
        }
        catch{if(Current(generation))Say("Folder outcome is unknown. Check the original receipt; no replacement was sent.",true);}
        finally{Busy=false;_gate.Release();Changed?.Invoke();}
    });
    private async Task ConfirmFolderAsync(NoteFolderReceipt receipt,int generation,CancellationToken token)
    {
        if(!Current(generation))return;_pendingFolder=null;FolderReview=null;LastFolderReceipt=receipt;Book=null;
        await LoadAsync(token);if(Current(generation))Say(Book is null?"Folder saved. Original receipt confirmed; refresh is unavailable.":"Folder saved. Original command receipt confirmed.");
    }
    private async Task RecoverFolderCoreAsync(CancellationToken token)
    {
        EnsureAccount();if(_pendingFolder is not {} command||Busy)return;var generation=_generation;
        if(Source==NotesSource.Cloud&&_pendingFolderNotebook!=_cloud.NotebookId){Say("The original folder action belongs to another notebook. No command was replayed.",true);return;}
        try
        {
            NoteFolderReceipt? receipt;
            if(Source==NotesSource.Browser)receipt=(await _database.ForScope(LocalScope).LoadAsync(token))?.FolderReceipts?.FirstOrDefault(r=>r.CommandId==command.Id);
            else {var result=await _cloud.RecoverFolderAsync(command,token);receipt=result.Succeeded?result.Value:null;}
            if(!Current(generation))return;
            if(receipt is null||receipt.PayloadHash!=NotesJson.Hash(command)||receipt.FolderId!=command.FolderId||receipt.FolderRevision!=command.ExpectedFolderRevision+1||receipt.NotebookRevision!=command.ExpectedNotebookRevision+1){Say("The original folder receipt is unavailable. The action remains uncertain; no replacement was sent.",true);return;}
            await ConfirmFolderAsync(receipt,generation,token);
        }
        catch{if(Current(generation))Say("The original folder receipt is unavailable. No action was retried.",true);}
    }
}
