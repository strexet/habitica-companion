using Habitica.Domain.Auth;
using Habitica.Domain.Notes;
using System.Text.Json.Serialization;
namespace Habitica.Application.Notes;

public sealed record NotesCloudResult<T>(bool Succeeded,T? Value,string Code,bool OutcomeUnknown=false);
public sealed record NotesSearchItem(Guid Id,string Title,long NoteRevision,bool IsTrashed,DateTimeOffset AtUtc,string Snippet,bool ContentTruncated,[property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] NoteProjectLink? Project=null);
public sealed record NotesSearchPage(Guid NotebookId,string OwnerScope,long Revision,IReadOnlyList<NotesSearchItem> Results,int Total,int? NextOffset,bool FullContentIncluded);
public sealed record CloudNote(Guid NotebookId,string OwnerScope,long Revision,PersonalNote Note,string ContentHash,int HistoryLimit,bool HistoryTruncated);
public interface INotesCloudClient
{
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
    Task<NotesCloudResult<bool>> DisconnectAsync(bool deleteNotebook,CancellationToken token);
    void ForgetSession();
}
