using System.Text.Json;
using Habitica.Application.Notes;
using Habitica.Domain.Notes;
namespace Habitica.WebApp.Integration;

public sealed partial class NotesCloudClient
{
    private bool ValidFolder(NoteFolder? folder)=>folder is not null&&folder.Id!=Guid.Empty&&folder.Revision is >0 and <=NotesModel.MaxRevision&&NotesModel.TextValid(folder.Name,"")
        &&folder.Source=="cloud"&&folder.CreatedAtUtc!=default&&folder.CreatedAtUtc.Offset==TimeSpan.Zero&&folder.UpdatedAtUtc.Offset==TimeSpan.Zero&&folder.UpdatedAtUtc>=folder.CreatedAtUtc
        &&(!folder.IsRemoved||folder.Revision>=2)&&NotesModel.ValidProject(folder.Project,OwnerScope!)&&(folder.Project is null||folder.Project.Source=="cloud");
    public async Task<NotesCloudResult<NotesFolderPage>> SearchFoldersAsync(string query,int offset,long? expectedRevision,NotesProjectFilter projects,bool includeRemoved,CancellationToken token)
    {
        if(!projects.Valid||projects.ProjectId is not null&&projects.Source!="cloud"||query.Length>256||!NotesModel.TextValid("search",query)||offset<0||offset>NotesModel.MaxFolders)return new(false,null,"invalid_folder_search");
        var input=new Dictionary<string,object?>{{"query",query},{"offset",offset},{"includeRemoved",includeRemoved}};
        if(expectedRevision is {} revision)input["expectedRevision"]=revision;
        if(projects.ProjectId is {} projectId){input["projectId"]=projectId;input["projectWorkspaceId"]=projects.WorkspaceId;}
        if(projects.Unassigned)input["unassigned"]=true;
        var result=await Call(HttpMethod.Post,"notes/folders",input,256*1024,text=>JsonSerializer.Deserialize<NotesFolderPage>(text,Json)??throw new JsonException(),token);
        if(result.Succeeded&&result.Value is {} page&&(page.NotebookId!=NotebookId||page.OwnerScope!=OwnerScope||page.Revision is <0 or >NotesModel.MaxRevision||page.Folders is null||page.Folders.Count>20||page.Total<page.Folders.Count||page.Total>NotesModel.MaxFolders
            ||page.Folders.Any(f=>!ValidFolder(f)||!includeRemoved&&f.IsRemoved||!projects.Matches(f.Project))||page.Folders.Select(f=>f.Id).Distinct().Count()!=page.Folders.Count
            ||page.NextOffset is {} next&&(next!=offset+page.Folders.Count||next>=page.Total)||expectedRevision is not null&&page.Revision!=expectedRevision||offset>0&&expectedRevision!=page.Revision))return new(false,null,"invalid_folder_search");
        return result;
    }
    public async Task<NotesCloudResult<CloudNoteFolder>> ReadFolderAsync(Guid folderId,CancellationToken token)
    {
        var result=await Call(HttpMethod.Post,"notes/folder",new{folderId},8192,text=>JsonSerializer.Deserialize<CloudNoteFolder>(text,Json)??throw new JsonException(),token);
        if(result.Succeeded&&result.Value is {} value&&(value.NotebookId!=NotebookId||value.OwnerScope!=OwnerScope||value.Revision is <1 or >NotesModel.MaxRevision||!ValidFolder(value.Folder)||value.Folder.Id!=folderId||value.Revision<value.Folder.Revision))return new(false,null,"invalid_folder");
        return result;
    }
    public Task<NotesCloudResult<NoteFolderReceipt>> SubmitFolderAsync(NoteFolderCommand command,CancellationToken token)=>FolderReceipt("notes/folder-command",new{expectedNotebookId=NotebookId,command},command,token);
    public Task<NotesCloudResult<NoteFolderReceipt>> RecoverFolderAsync(NoteFolderCommand command,CancellationToken token)=>FolderReceipt("notes/folder-receipt",new{expectedNotebookId=NotebookId,commandId=command.Id},command,token);
    private async Task<NotesCloudResult<NoteFolderReceipt>> FolderReceipt(string path,object input,NoteFolderCommand command,CancellationToken token)
    {
        if(command.OwnerScope!=OwnerScope||command.Source!="cloud")return new(false,null,"folder_owner_or_source_mismatch");
        var result=await Call(HttpMethod.Post,path,input,4096,text=>JsonSerializer.Deserialize<FolderReceiptPayload>(text,Json)??throw new JsonException(),token,path=="notes/folder-command");
        if(!result.Succeeded||result.Value is not {} value)return new(false,null,result.Code,result.OutcomeUnknown);
        var receipt=value.Receipt;
        if(!value.Saved||value.NotebookId!=NotebookId||value.OwnerScope!=OwnerScope||receipt is null||receipt.CommandId!=command.Id||receipt.FolderId!=command.FolderId||receipt.PayloadHash!=NotesJson.Hash(command)
            ||receipt.FolderRevision!=command.ExpectedFolderRevision+1||receipt.NotebookRevision!=command.ExpectedNotebookRevision+1||receipt.FolderRevision is <1 or >NotesModel.MaxRevision||receipt.NotebookRevision is <1 or >NotesModel.MaxRevision)return new(false,null,"invalid_folder_receipt",true);
        return new(true,receipt,value.Disposition??"recovered");
    }
    private sealed record FolderReceiptPayload(Guid NotebookId,string OwnerScope,NoteFolderReceipt Receipt,bool Saved,string? Disposition=null,long? Revision=null);
}
