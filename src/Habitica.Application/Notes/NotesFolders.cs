using System.Text;
using System.Text.Json;
using Habitica.Domain.Notes;
namespace Habitica.Application.Notes;

public sealed record NoteFolderSubmission(bool Accepted,string Code,NotesNotebook Notebook,NoteFolderReceipt? Receipt=null);
public sealed record NotesFolderFilter(Guid? FolderId=null,string? Source=null,bool NoFolder=false)
{
    public bool Valid=>NoFolder?FolderId is null&&Source is null:FolderId is null?Source is null:FolderId!=Guid.Empty&&Source is "browser" or "cloud";
    public bool Matches(NoteFolderLink? folder)=>NoFolder?folder is null:FolderId is null||folder is not null&&folder.FolderId==FolderId&&folder.Source==Source;
}
public static class NotesFolders
{
    public static NoteFolderSubmission Apply(NotesNotebook book,NoteFolderCommand command,DateTimeOffset now,NotesProjectAuthority? authority=null)
    {
        NotesModel.Validate(book);
        NoteFolderSubmission Reject(string code)=>new(false,code,book);
        if(command.Id==Guid.Empty||command.FolderId==Guid.Empty||command.OwnerScope!=book.OwnerScope||command.Source is not ("browser" or "cloud")||!Enum.IsDefined(command.Operation)
            ||command.ExpectedNotebookRevision<0||command.ExpectedFolderRevision<0||now==default||now.Offset!=TimeSpan.Zero)return Reject("invalid_folder_command");
        var hash=NotesJson.Hash(command);var old=(book.FolderReceipts??[]).FirstOrDefault(r=>r.CommandId==command.Id);
        if(old is not null)return old.PayloadHash==hash?new(true,"duplicate",book,old):Reject("command_id_reused");
        if(book.Receipts.Any(r=>r.CommandId==command.Id))return Reject("command_id_reused");
        if(command.ExpectedNotebookRevision!=book.Revision)return Reject("stale_notebook");
        var folders=book.Folders??[];var folder=folders.SingleOrDefault(f=>f.Id==command.FolderId);var create=command.Operation==NoteFolderOperation.Create;
        if(create?folder is not null||command.ExpectedFolderRevision!=0:folder is null||folder.Revision!=command.ExpectedFolderRevision)return Reject("stale_folder");
        if(folder?.IsRemoved==true)return Reject("folder_removed");
        if(folder is not null&&(folder.Source!=command.Source||now<folder.UpdatedAtUtc||folder.Revision>=NotesModel.MaxRevision))return Reject("invalid_folder_identity");
        if(folders.Any(f=>f.Source!=command.Source))return Reject("folder_source_mismatch");
        if(!NotesModel.ValidProject(command.Project,book.OwnerScope)||command.Project is not null&&(command.Operation!=NoteFolderOperation.Create||command.Project.Source!=command.Source))return Reject("invalid_folder_project");
        if(command.Project is {} project&&(authority is null||!authority.Authorizes(project)))return Reject("project_unavailable");
        if(command.Operation==NoteFolderOperation.Remove?command.Name is not null:!NotesModel.TextValid(command.Name,""))return Reject("invalid_folder_name");
        if(create&&folders.Count>=NotesModel.MaxFolders)return Reject("folders_quota");
        if(command.Operation==NoteFolderOperation.Remove&&book.Notes.Any(n=>n.Current.Folder?.FolderId==command.FolderId))return Reject("folder_not_empty");
        var count=book.Receipts.Count+(book.FolderReceipts?.Count??0);
        if(book.Revision>=NotesModel.MaxRevision||count>=NotesModel.MaxReceipts+2*NotesModel.MaxNotes+NotesModel.MaxFolders||count>=NotesModel.MaxReceipts&&command.Operation!=NoteFolderOperation.Remove)return Reject("notebook_quota");
        var changed=create?new NoteFolder(command.FolderId,1,command.Name!,command.Source,now,now,Project:command.Project)
            :folder! with {Revision=folder.Revision+1,Name=command.Name??folder.Name,UpdatedAtUtc=now,IsRemoved=command.Operation==NoteFolderOperation.Remove};
        var receipt=new NoteFolderReceipt(command.Id,hash,changed.Id,changed.Revision,book.Revision+1);
        var next=book with {SchemaVersion=2,Revision=book.Revision+1,Folders=create?folders.Append(changed).ToArray():folders.Select(f=>f.Id==changed.Id?changed:f).ToArray(),FolderReceipts=(book.FolderReceipts??[]).Append(receipt).ToArray()};
        try{var json=NotesJson.Serialize(next);if(command.Operation!=NoteFolderOperation.Remove&&Encoding.UTF8.GetByteCount(json)>NotesModel.NotebookBytes-2*1024*1024)return Reject("notebook_quota");}catch(JsonException){return Reject("notebook_quota");}
        return new(true,"accepted",next,receipt);
    }
    public static IReadOnlyList<NoteFolder> List(NotesNotebook book,NotesProjectFilter? filter=null,bool includeRemoved=false)
    {
        NotesModel.Validate(book);filter??=new();if(!filter.Valid)throw new ArgumentException("Invalid folder search.");
        return (book.Folders??[]).Where(f=>(includeRemoved||!f.IsRemoved)&&filter.Matches(f.Project)).ToArray();
    }
}
