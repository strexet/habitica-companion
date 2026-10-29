using Habitica.Application.Notes;
using Habitica.Domain.Notes;
using Habitica.Storage;
using System.Text.Json;
namespace Habitica.Application.Tests.Notes;

public sealed class NotesTests
{
    private static readonly DateTimeOffset Now=DateTimeOffset.Parse("2026-10-23T00:00:00Z");
    private sealed class Clock:TimeProvider {public override DateTimeOffset GetUtcNow()=>Now;}
    private static NotesNotebook Empty()=>NotesModel.Empty(NotesModel.Scope("synthetic-owner"));
    private static NoteCommand Create(string body="line\r\n```md\n# 🧥 Размер\n```\n")=>new(Guid.NewGuid(),Guid.NewGuid(),0,0,NoteOperation.Create,"Личная заметка",body);
    private static NoteSubmission Change(NotesNotebook book,Guid id,NoteOperation operation,string? title=null,string? body=null)=>NotesEngine.Apply(book,new(Guid.NewGuid(),id,book.Revision,book.Notes.Single(note=>note.Id==id).Current.Revision,operation,title,body),Now.AddMinutes(book.Revision));
    [Fact]
    public void Exact_Unicode_line_endings_and_code_fences_survive_partial_update_and_append()
    {
        var command=Create();var result=NotesEngine.Apply(Empty(),command,Now);Assert.True(result.Accepted);
        var renamed=Change(result.Notebook,command.NoteId,NoteOperation.Update,title:"Новое имя");Assert.Equal(command.Markdown,renamed.Notebook.Notes[0].Current.Markdown);
        var appended=Change(renamed.Notebook,command.NoteId,NoteOperation.Append,body:"\r\n尾\n");Assert.True(appended.Accepted);
        var copy=NotesJson.Deserialize(NotesJson.Serialize(appended.Notebook));Assert.Equal(command.Markdown+"\r\n尾\n",copy.Notes[0].Current.Markdown);Assert.Equal(command.Markdown,copy.Notes[0].History[0].Markdown);
    }
    [Fact]
    public void Duplicates_recover_original_receipt_and_conflicting_id_or_revision_refuse()
    {
        var command=Create();var first=NotesEngine.Apply(Empty(),command,Now);var updated=Change(first.Notebook,command.NoteId,NoteOperation.Append,body:"suffix");
        var duplicate=NotesEngine.Apply(updated.Notebook,command,Now);Assert.True(duplicate.Accepted);Assert.Equal("duplicate",duplicate.Code);Assert.Equal(first.Receipt,duplicate.Receipt);Assert.Equal(updated.Notebook,duplicate.Notebook);
        Assert.Equal("command_id_reused",NotesEngine.Apply(updated.Notebook,command with {Markdown="different"},Now).Code);
        Assert.Equal("stale_notebook",NotesEngine.Apply(first.Notebook,Create(),Now).Code);
    }
    [Fact]
    public void Trash_is_recoverable_and_purge_removes_all_content_without_reusing_identity()
    {
        var command=Create("unique private fixture");var first=NotesEngine.Apply(Empty(),command,Now);
        var trash=Change(first.Notebook,command.NoteId,NoteOperation.Trash);Assert.Empty(NotesEngine.List(trash.Notebook));Assert.Single(NotesEngine.List(trash.Notebook,true));
        var restored=Change(trash.Notebook,command.NoteId,NoteOperation.Restore);Assert.Single(NotesEngine.List(restored.Notebook));
        Assert.False(Change(restored.Notebook,command.NoteId,NoteOperation.Purge).Accepted);
        var trash2=Change(restored.Notebook,command.NoteId,NoteOperation.Trash);var purged=Change(trash2.Notebook,command.NoteId,NoteOperation.Purge);Assert.True(purged.Accepted);
        Assert.DoesNotContain("unique private fixture",NotesJson.Serialize(purged.Notebook));Assert.Empty(purged.Notebook.Notes);
        Assert.Equal("note_purged",NotesEngine.Apply(purged.Notebook,command with {Id=Guid.NewGuid(),ExpectedNotebookRevision=purged.Notebook.Revision},Now).Code);
    }
    [Fact]
    public void History_is_bounded_contiguous_and_bad_import_revisions_or_duplicate_fields_refuse()
    {
        var command=Create();var book=NotesEngine.Apply(Empty(),command,Now).Notebook;
        for(var i=0;i<20;i++)book=Change(book,command.NoteId,NoteOperation.Append,body:"x").Notebook;
        Assert.Equal(NotesModel.HistoryLimit,book.Notes[0].History.Count);Assert.Equal(5,book.Notes[0].History[0].Revision);NotesModel.Validate(book);
        Assert.Throws<JsonException>(()=>NotesModel.Validate(book with {Receipts=book.Receipts.Skip(1).ToArray()}));
        var json=NotesJson.Serialize(book);Assert.Throws<JsonException>(()=>NotesJson.Deserialize(json.Replace("\"schemaVersion\":1","\"schemaVersion\":1,\"schemaVersion\":1")));
    }
    [Fact]
    public void Limits_refuse_oversize_and_invalid_Unicode_without_truncating_accepted_source()
    {
        Assert.True(NotesEngine.Apply(Empty(),Create(new string('x',NotesModel.BodyBytes)),Now).Accepted);
        Assert.False(NotesEngine.Apply(Empty(),Create(new string('x',NotesModel.BodyBytes+1)),Now).Accepted);
        Assert.False(NotesEngine.Apply(Empty(),Create("\ud800"),Now).Accepted);
        Assert.False(NotesEngine.Apply(Empty(),Create() with {Title=new string('x',257)},Now).Accepted);
        Assert.True(NotesEngine.Apply(Empty(),Create() with {Title=new string('x',256)},Now).Accepted);
    }
    [Fact]
    public async Task Lossless_backup_restores_only_matching_account_without_overwriting_conflicts()
    {
        var store=new MemoryStore(Empty().OwnerScope);var service=new NotesService(store,new Clock());var command=Create();Assert.True((await service.SubmitAsync(command,default)).Accepted);
        var exported=await service.ExportAsync(default);var target=new NotesService(new MemoryStore(store.OwnerScope));Assert.Equal("restored",await target.ImportAsync(exported,default));Assert.Equal(exported,await target.ExportAsync(default));Assert.Equal("unchanged",await target.ImportAsync(exported,default));
        var other=new NotesService(new MemoryStore(NotesModel.Scope("other")));Assert.Equal("account_mismatch",await other.ImportAsync(exported,default));
        var json=NotesJson.Deserialize(exported);var changed=Change(json,command.NoteId,NoteOperation.Append,body:"new");Assert.Equal("conflict",await target.ImportAsync(NotesJson.Serialize(changed.Notebook),default));Assert.Equal(exported,await target.ExportAsync(default));
    }
    [Fact]
    public void Account_changes_and_signout_clear_private_drafts()
    {
        var draft=new NotesDraftSession();draft.Bind(Empty().OwnerScope);draft.Title="private";draft.Markdown="body";draft.Bind(Empty().OwnerScope);Assert.Equal("body",draft.Markdown);draft.Bind(NotesModel.Scope("other"));Assert.Null(draft.Markdown);draft.Markdown="again";draft.Clear();Assert.Null(draft.Markdown);Assert.Null(draft.OwnerScope);
    }
    [Fact]
    public void Full_command_quota_reserves_explicit_trash_and_purge_capacity()
    {
        var id=Guid.NewGuid();var limit=NotesModel.MaxReceipts;
        var note=new PersonalNote(id,new(limit,"note","body",false,Now),Now,Enumerable.Range(limit-NotesModel.HistoryLimit,NotesModel.HistoryLimit).Select(i=>new NoteVersion(i,"note","body",false,Now)).ToArray());
        var receipts=Enumerable.Range(1,limit).Select(i=>new NoteReceipt(Guid.NewGuid(),new string('a',64),id,i,i)).ToArray();
        var book=Empty() with {Revision=limit,Notes=[note],Receipts=receipts};NotesModel.Validate(book);
        Assert.Equal("notebook_quota",Change(book,id,NoteOperation.Append,body:"x").Code);
        var trash=Change(book,id,NoteOperation.Trash);Assert.True(trash.Accepted);var purge=Change(trash.Notebook,id,NoteOperation.Purge);Assert.True(purge.Accepted);Assert.Empty(purge.Notebook.Notes);
    }

    [Fact]
    public async Task Lost_storage_acknowledgement_recovers_original_command_without_appending_twice()
    {
        var store=new MemoryStore(Empty().OwnerScope){LoseAcknowledgement=true};var service=new NotesService(store,new Clock());var command=Create();
        var unknown=await service.SubmitAsync(command,default);Assert.False(unknown.Accepted);Assert.Equal("acceptance_unknown",unknown.Code);
        var recovered=await service.SubmitAsync(command,default);Assert.True(recovered.Accepted);Assert.Equal("duplicate",recovered.Code);Assert.Single(recovered.Notebook.Notes);Assert.Single(recovered.Notebook.Receipts);
        var changed=await service.SubmitAsync(command with {Id=Guid.NewGuid()},default);Assert.False(changed.Accepted);Assert.Equal("stale_notebook",changed.Code);
    }

    private sealed class MemoryStore(string scope):INotesStore
    {
        public string OwnerScope=>scope;private NotesNotebook? _book;public bool LoseAcknowledgement {get;init;}
        public Task<NotesNotebook?> LoadAsync(CancellationToken token)=>Task.FromResult(_book);
        public Task<bool> TryCommitAsync(long expectedRevision,NotesNotebook book,CancellationToken token){if((_book?.Revision??0)!=expectedRevision)return Task.FromResult(false);_book=book;if(LoseAcknowledgement)throw new IOException("Synthetic storage response loss.");return Task.FromResult(true);}
    }
}
