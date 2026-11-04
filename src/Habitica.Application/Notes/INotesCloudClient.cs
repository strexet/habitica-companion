using Habitica.Domain.Auth;
using Habitica.Domain.Notes;
using System.Text.Json.Serialization;
namespace Habitica.Application.Notes;

public sealed record NotesCloudResult<T>(bool Succeeded,T? Value,string Code,bool OutcomeUnknown=false);
public sealed record NotesSearchItem(Guid Id,string Title,long NoteRevision,bool IsTrashed,DateTimeOffset AtUtc,string Snippet,bool ContentTruncated,[property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] NoteProjectLink? Project=null,[property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] NoteFolderLink? Folder=null);
public sealed record NotesSearchPage(Guid NotebookId,string OwnerScope,long Revision,IReadOnlyList<NotesSearchItem> Results,int Total,int? NextOffset,bool FullContentIncluded);
public sealed record CloudNote(Guid NotebookId,string OwnerScope,long Revision,PersonalNote Note,string ContentHash,int HistoryLimit,bool HistoryTruncated);
public sealed record NotesFolderPage(Guid NotebookId,string OwnerScope,long Revision,IReadOnlyList<NoteFolder> Folders,int Total,int? NextOffset);
public sealed record CloudNoteFolder(Guid NotebookId,string OwnerScope,long Revision,NoteFolder Folder);
public interface INotesCloudClient
{
    Task<NotesCloudResult<bool>> RecoverConnectionAsync(Guid? expectedNotebookId,CancellationToken token)
        =>Task.FromResult(new NotesCloudResult<bool>(false,false,"recovery_unavailable"));
    bool IsAvailable {get;}
    bool IsConnected {get;}
    Guid? NotebookId {get;}
    string? OwnerScope {get;}
    Task<NotesCloudResult<bool>> ConnectAsync(HabiticaCredentials credentials,CancellationToken token);
    Task<NotesCloudResult<NotesNotebook>> LoadAsync(CancellationToken token);
    Task<NotesCloudResult<NoteReceipt>> SubmitAsync(NoteCommand command,CancellationToken token);
    Task<NotesCloudResult<NoteReceipt>> RecoverAsync(NoteCommand command,CancellationToken token);
    Task<NotesCloudResult<NotesSearchPage>> SearchAsync(string query,bool trash,int offset,long? expectedRevision,CancellationToken token);
    Task<NotesCloudResult<NotesSearchPage>> SearchAsync(string query,bool trash,int offset,long? expectedRevision,NotesProjectFilter filter,CancellationToken token)
        =>filter==new NotesProjectFilter()?SearchAsync(query,trash,offset,expectedRevision,token):Task.FromResult(new NotesCloudResult<NotesSearchPage>(false,null,"project_search_unavailable"));
    Task<NotesCloudResult<CloudNote>> ReadAsync(Guid noteId,CancellationToken token);
    Task<NotesCloudResult<NotesSearchPage>> SearchAsync(string query,bool trash,int offset,long? expectedRevision,NotesProjectFilter projects,NotesFolderFilter folders,CancellationToken token)
        =>folders==new NotesFolderFilter()?SearchAsync(query,trash,offset,expectedRevision,projects,token):Task.FromResult(new NotesCloudResult<NotesSearchPage>(false,null,"folder_search_unavailable"));
    Task<NotesCloudResult<NotesFolderPage>> SearchFoldersAsync(string query,int offset,long? expectedRevision,NotesProjectFilter projects,bool includeRemoved,CancellationToken token)
        =>Task.FromResult(new NotesCloudResult<NotesFolderPage>(false,null,"folders_unavailable"));
    Task<NotesCloudResult<CloudNoteFolder>> ReadFolderAsync(Guid folderId,CancellationToken token)=>Task.FromResult(new NotesCloudResult<CloudNoteFolder>(false,null,"folders_unavailable"));
    Task<NotesCloudResult<NoteFolderReceipt>> SubmitFolderAsync(NoteFolderCommand command,CancellationToken token)=>Task.FromResult(new NotesCloudResult<NoteFolderReceipt>(false,null,"folders_unavailable"));
    Task<NotesCloudResult<NoteFolderReceipt>> RecoverFolderAsync(NoteFolderCommand command,CancellationToken token)=>Task.FromResult(new NotesCloudResult<NoteFolderReceipt>(false,null,"folders_unavailable"));
    Task<NotesCloudResult<bool>> DisconnectAsync(bool deleteNotebook,CancellationToken token);
    void ForgetSession();
}
