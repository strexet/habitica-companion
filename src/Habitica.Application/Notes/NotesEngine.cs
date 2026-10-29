using Habitica.Domain.Notes;
using System.Text.Json;
namespace Habitica.Application.Notes;

public sealed record NoteSubmission(bool Accepted, string Code, NotesNotebook Notebook, NoteReceipt? Receipt = null);
public static class NotesEngine
{
    public static NoteSubmission Apply(NotesNotebook book,NoteCommand command,DateTimeOffset now,NotesProjectAuthority? projectAuthority=null)
    {
        NotesModel.Validate(book);
        NoteSubmission Reject(string code)=>new(false,code,book);
        if(command.Id==Guid.Empty||command.NoteId==Guid.Empty||!Enum.IsDefined(command.Operation)||now==default||now.Offset!=TimeSpan.Zero
            ||command.ExpectedNotebookRevision<0||command.ExpectedNoteRevision<0)return Reject("invalid_command");
        if(command.Title is not null&&!NotesModel.TextValid(command.Title,"")||command.Markdown is not null&&!NotesModel.TextValid("note",command.Markdown))return Reject("note_content_limit");
        var hash=NotesJson.Hash(command);var old=book.Receipts.FirstOrDefault(row=>row.CommandId==command.Id);
        if(old is not null)return old.PayloadHash==hash?new(true,"duplicate",book,old):Reject("command_id_reused");
        if(command.ExpectedNotebookRevision!=book.Revision)return Reject("stale_notebook");
        if(book.Revision>=NotesModel.MaxRevision||book.Receipts.Count>=NotesModel.MaxReceipts+2*NotesModel.MaxNotes
            ||book.Receipts.Count>=NotesModel.MaxReceipts&&command.Operation is not (NoteOperation.Trash or NoteOperation.Purge))return Reject("notebook_quota");
        var note=book.Notes.SingleOrDefault(row=>row.Id==command.NoteId);var operation=command.Operation;
        if(book.Purged.Any(row=>row.Id==command.NoteId))return Reject("note_purged");
        if(operation==NoteOperation.Create ? note is not null||command.ExpectedNoteRevision!=0 : note is null||command.ExpectedNoteRevision!=note.Current.Revision)return Reject("stale_note");
        if(note is not null&&(note.Current.Revision>=NotesModel.MaxRevision||now<note.Current.AtUtc))return Reject("invalid_revision_time");
        if(command.ClearProject==false||command.Project is not null&&command.ClearProject==true||!NotesModel.ValidProject(command.Project,book.OwnerScope)
            ||(command.Project is not null||command.ClearProject is not null)&&operation is not (NoteOperation.Create or NoteOperation.Update)
            ||operation==NoteOperation.Create&&command.ClearProject is not null)return Reject("invalid_project_patch");
        if(command.Project is {} link&&link!=note?.Current.Project&&(projectAuthority is null||!projectAuthority.Authorizes(link)))return Reject("project_unavailable");
        if(operation is NoteOperation.Trash or NoteOperation.Restore or NoteOperation.Purge && (command.Title is not null||command.Markdown is not null))return Reject("invalid_patch");
        if(operation==NoteOperation.Append&&(command.Title is not null||string.IsNullOrEmpty(command.Markdown)))return Reject("invalid_patch");
        if(operation==NoteOperation.Update&&command.Title is null&&command.Markdown is null&&command.Project is null&&command.ClearProject is null)return Reject("empty_patch");
        if(note is not null && (note.Current.IsTrashed ? operation is not (NoteOperation.Restore or NoteOperation.Purge) : operation is NoteOperation.Restore or NoteOperation.Purge))return Reject("invalid_lifecycle");
        var revision=(note?.Current.Revision??0)+1;var receipt=new NoteReceipt(command.Id,hash,command.NoteId,revision,book.Revision+1);
        NotesNotebook next;
        if(operation==NoteOperation.Purge)
            next=book with {Notes=book.Notes.Where(row=>row.Id!=command.NoteId).ToArray(),Purged=book.Purged.Append(new(command.NoteId,revision)).ToArray()};
        else
        {
            if(operation==NoteOperation.Create&&book.Notes.Count>=NotesModel.MaxNotes)return Reject("notes_quota");
            var title=command.Title??note?.Current.Title;var body=operation==NoteOperation.Append?note!.Current.Markdown+command.Markdown:command.Markdown??note?.Current.Markdown;
            if(!NotesModel.TextValid(title,body))return Reject("note_content_limit");
            var current=new NoteVersion(revision,title!,body!,operation==NoteOperation.Trash,now,command.ClearProject==true?null:command.Project??note?.Current.Project);
            var changed=new PersonalNote(command.NoteId,current,note?.CreatedAtUtc??now,note is null?[]:note.History.Append(note.Current).TakeLast(NotesModel.HistoryLimit).ToArray());
            next=book with {Notes=note is null?book.Notes.Append(changed).ToArray():book.Notes.Select(row=>row.Id==changed.Id?changed:row).ToArray()};
        }
        next=next with {Revision=book.Revision+1,Receipts=book.Receipts.Append(receipt).ToArray()};
        try{var json=NotesJson.Serialize(next);if(operation is not (NoteOperation.Trash or NoteOperation.Purge)&&System.Text.Encoding.UTF8.GetByteCount(json)>NotesModel.NotebookBytes-2*1024*1024)return Reject("notebook_quota");}catch(JsonException){return Reject("notebook_quota");}
        return new(true,"accepted",next,receipt);
    }
    public static IReadOnlyList<PersonalNote> List(NotesNotebook book,bool trash=false)=>book.Notes.Where(note=>note.Current.IsTrashed==trash).ToArray();
}
