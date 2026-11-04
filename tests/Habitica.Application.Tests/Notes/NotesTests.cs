using Habitica.Application.Notes;
using Habitica.Domain.Notes;
using Habitica.Storage;
using System.Text.Json;
namespace Habitica.Application.Tests.Notes;

public sealed class NotesFolderCompatibilityTests
{
    private static readonly DateTimeOffset Now=DateTimeOffset.Parse("2026-11-02T00:00:00Z");
    private static readonly string Scope=NotesModel.Scope("synthetic-folder-owner");
    private static readonly Guid Workspace=Guid.Parse("00000000-0000-4000-8000-000000000080"),Project=Guid.Parse("00000000-0000-4000-8000-000000000081");
    private static NoteProjectLink ProjectLink=>new(Project,Workspace,"browser",Scope);
    private static NotesProjectAuthority Authority=>new(Scope,"browser",new Habitica.Domain.Workspace.WorkspaceDocument{Id=Workspace,Projects=[new(Project,1,"Trip")]});
    private static NoteFolderCommand Folder(NotesNotebook book,Guid id,NoteFolderOperation operation,string? name=null,NoteProjectLink? project=null)=>new(Guid.NewGuid(),id,book.Revision,(book.Folders??[]).FirstOrDefault(f=>f.Id==id)?.Revision??0,operation,Scope,"browser",name,project);
    private static NotesNotebook Accept(NotesNotebook book,NoteFolderCommand command,NotesProjectAuthority? authority=null)
    {var result=NotesFolders.Apply(book,command,Now.AddSeconds(book.Revision),authority);Assert.True(result.Accepted,result.Code);return result.Notebook;}
    private static NotesNotebook Accept(NotesNotebook book,NoteCommand command,NotesProjectAuthority? authority=null)
    {var result=NotesEngine.Apply(book,command,Now.AddSeconds(book.Revision),authority);Assert.True(result.Accepted,result.Code);return result.Notebook;}
    private static NoteCommand Change(NotesNotebook book,Guid id,NoteOperation operation,NoteFolderLink? folder=null,bool? clearFolder=null,NoteProjectLink? project=null,bool? clearProject=null,string? markdown=null)=>new(Guid.NewGuid(),id,book.Revision,book.Notes.Single(n=>n.Id==id).Current.Revision,operation,Markdown:markdown,Project:project,ClearProject:clearProject,Folder:folder,ClearFolder:clearFolder);
    [Fact]
    public void Version_two_backup_retains_a_folder_only_revision_without_inventing_a_note()
    {
        var scope=NotesModel.Scope("synthetic-folder-owner");
        var json="""
        {"schemaVersion":2,"ownerScope":"SCOPE","revision":1,"notes":[],"receipts":[],"purged":[],"folders":[{"id":"00000000-0000-4000-8000-000000000091","revision":1,"name":"Hotels","source":"browser","createdAtUtc":"2026-11-02T00:00:00+00:00","updatedAtUtc":"2026-11-02T00:00:00+00:00","isRemoved":false}],"folderReceipts":[{"commandId":"00000000-0000-4000-8000-000000000092","payloadHash":"HASH","folderId":"00000000-0000-4000-8000-000000000091","folderRevision":1,"notebookRevision":1}]}
        """.Replace("SCOPE",scope).Replace("HASH",new string('a',64));
        var book=NotesJson.Deserialize(json);
        Assert.Equal(2,book.SchemaVersion);Assert.Empty(book.Notes);Assert.Empty(book.Receipts);
        Assert.Contains("Hotels",NotesJson.Serialize(book));
    }
    [Fact]
    public void Folder_lifecycle_preserves_identity_and_original_receipts_after_rename_and_removal()
    {
        var book=NotesModel.Empty(Scope);var id=Guid.NewGuid();var create=Folder(book,id,NoteFolderOperation.Create,"Hotels");book=Accept(book,create);var original=book.FolderReceipts![0];
        var second=Guid.NewGuid();book=Accept(book,Folder(book,second,NoteFolderOperation.Create,"Hotels"));Assert.Equal(2,NotesFolders.List(book).Count);
        book=Accept(book,Folder(book,id,NoteFolderOperation.Rename,"Interesting places"));var remove=Folder(book,id,NoteFolderOperation.Remove);book=Accept(book,remove);
        Assert.Single(NotesFolders.List(book));Assert.Equal(2,NotesFolders.List(book,includeRemoved:true).Count);Assert.Equal(original,NotesFolders.Apply(book,create,Now).Receipt);
        Assert.Equal("command_id_reused",NotesFolders.Apply(book,create with {Name="Replacement"},Now).Code);
        Assert.False(NotesFolders.Apply(book,Folder(book,id,NoteFolderOperation.Create,"Replacement"),Now).Accepted);
        Assert.Equal("folder_removed",NotesFolders.Apply(book,Folder(book,id,NoteFolderOperation.Rename,"Replacement"),Now).Code);
        Assert.Equal(NotesJson.Serialize(book),NotesJson.Serialize(NotesJson.Deserialize(NotesJson.Serialize(book))));Assert.Empty(book.Notes);
    }
    [Fact]
    public void Placement_moves_require_explicit_matching_project_change_and_preserve_exact_content_and_history()
    {
        var book=NotesModel.Empty(Scope);var standalone=Guid.NewGuid();var scoped=Guid.NewGuid();var note=Guid.NewGuid();
        book=Accept(book,Folder(book,standalone,NoteFolderOperation.Create,"Hotels"));book=Accept(book,Folder(book,scoped,NoteFolderOperation.Create,"Hotels",ProjectLink),Authority);
        var body="# Detailed hotel\r\n🧥 <>& \"\\\n";var source=new NoteCommand(Guid.NewGuid(),note,book.Revision,0,NoteOperation.Create,"Exact",body,Folder:new(standalone,Scope,"browser"));book=Accept(book,source);
        var move=Change(book,note,NoteOperation.Update,folder:new(scoped,Scope,"browser"));Assert.Equal("folder_scope_mismatch",NotesEngine.Apply(book,move,Now.AddMinutes(1)).Code);
        move=move with {Project=ProjectLink};Assert.Equal("project_unavailable",NotesEngine.Apply(book,move,Now.AddMinutes(1)).Code);book=Accept(book,move,Authority);var receipt=book.Receipts[^1];
        var edit=Change(book,note,NoteOperation.Update,markdown:body+"edited");book=Accept(book,edit);Assert.Equal(scoped,book.Notes[0].Current.Folder!.FolderId);
        book=Accept(book,Change(book,note,NoteOperation.Update,clearFolder:true));Assert.Null(book.Notes[0].Current.Folder);Assert.Equal(ProjectLink,book.Notes[0].Current.Project);
        var back=Change(book,note,NoteOperation.Update,folder:new(standalone,Scope,"browser"));Assert.False(NotesEngine.Apply(book,back,Now).Accepted);book=Accept(book,back with {ClearProject=true});
        Assert.Equal(note,book.Notes[0].Id);Assert.Equal(body+"edited",book.Notes[0].Current.Markdown);Assert.Equal(body,book.Notes[0].History[0].Markdown);Assert.Equal(receipt,NotesEngine.Apply(book,move,Now).Receipt);
        book=Accept(book,Folder(book,scoped,NoteFolderOperation.Remove));Assert.True(book.Folders!.Single(f=>f.Id==scoped).IsRemoved);NotesModel.Validate(book);
    }
    [Fact]
    public void Trashed_members_prevent_empty_removal_and_foreign_stale_scope_and_invalid_patches_leave_original_unchanged()
    {
        var book=NotesModel.Empty(Scope);var folder=Guid.NewGuid();var note=Guid.NewGuid();book=Accept(book,Folder(book,folder,NoteFolderOperation.Create,"Hotels"));
        book=Accept(book,new NoteCommand(Guid.NewGuid(),note,book.Revision,0,NoteOperation.Create,"Exact","body",Folder:new(folder,Scope,"browser")));book=Accept(book,Change(book,note,NoteOperation.Trash));
        var before=NotesJson.Serialize(book);Assert.Equal("folder_not_empty",NotesFolders.Apply(book,Folder(book,folder,NoteFolderOperation.Remove),Now.AddMinutes(1)).Code);
        var restore=Change(book,note,NoteOperation.Restore);Assert.Equal("invalid_folder_patch",NotesEngine.Apply(book,restore with {ClearFolder=true},Now.AddMinutes(1)).Code);book=Accept(book,restore);
        foreach(var link in new[]{new NoteFolderLink(folder,NotesModel.Scope("foreign"),"browser"),new(folder,Scope,"cloud"),new(Guid.NewGuid(),Scope,"browser")})Assert.False(NotesEngine.Apply(book,Change(book,note,NoteOperation.Update,folder:link),Now.AddMinutes(1)).Accepted);
        Assert.False(NotesFolders.Apply(book,Folder(book,folder,NoteFolderOperation.Rename,"Changed") with {ExpectedNotebookRevision=0},Now).Accepted);
        var empty=NotesModel.Empty(Scope);foreach(var link in new[]{ProjectLink with {OwnerScope=NotesModel.Scope("foreign")},ProjectLink with {Source="cloud"},ProjectLink with {WorkspaceId=Guid.NewGuid()}})Assert.False(NotesFolders.Apply(empty,Folder(empty,Guid.NewGuid(),NoteFolderOperation.Create,"Private",link),Now,Authority).Accepted);
        var archived=Authority with {Document=Authority.Document with {Projects=[new(Project,2,"Trip",IsArchived:true)]}};Assert.False(NotesFolders.Apply(empty,Folder(empty,Guid.NewGuid(),NoteFolderOperation.Create,"Private",ProjectLink),Now,archived).Accepted);
        Assert.Equal(before,NotesJson.Serialize(NotesJson.Deserialize(before)));
    }
    [Fact]
    public void Folder_filters_and_project_filters_are_independent_before_revision_bound_paging()
    {
        var book=NotesModel.Empty(Scope);var folder=Guid.NewGuid();book=Accept(book,Folder(book,folder,NoteFolderOperation.Create,"Hotels",ProjectLink),Authority);
        for(var i=0;i<30;i++)book=Accept(book,new NoteCommand(Guid.NewGuid(),Guid.NewGuid(),book.Revision,0,NoteOperation.Create,"Hotel "+i,"MATCH",Project:ProjectLink,Folder:i<25?new(folder,Scope,"browser"):null),Authority);
        var first=NotesQueries.Search(book,"match",false,0,null,new(Project,Workspace,"browser"),new(folder,"browser"));var next=NotesQueries.Search(book,"match",false,20,book.Revision,new(Project,Workspace,"browser"),new(folder,"browser"));
        Assert.Equal(25,first.Total);Assert.Equal(20,first.Results.Count);Assert.Equal(5,next.Results.Count);Assert.All(first.Results,n=>Assert.Equal(folder,n.Folder!.FolderId));Assert.Equal(5,NotesQueries.Search(book,"",false,0,null,folderFilter:new(NoFolder:true)).Total);Assert.Equal(0,NotesQueries.Search(book,"",false,0,null,new(Unassigned:true)).Total);
        Assert.Empty(NotesFolders.List(book,new(Unassigned:true)));Assert.Throws<InvalidOperationException>(()=>NotesQueries.Search(book,"",false,20,null,folderFilter:new(folder,"browser")));Assert.Throws<ArgumentException>(()=>NotesQueries.Search(book,"",false,0,null,folderFilter:new(folder,"browser",true)));
    }
    [Fact]
    public void Backups_reject_missing_folder_receipts_downgrades_scope_changes_and_removed_members()
    {
        var book=NotesModel.Empty(Scope);var id=Guid.NewGuid();book=Accept(book,Folder(book,id,NoteFolderOperation.Create,"Hotels"));
        Assert.Throws<JsonException>(()=>NotesJson.Serialize(book with {SchemaVersion=1}));Assert.Throws<JsonException>(()=>NotesJson.Serialize(book with {FolderReceipts=[]}));
        Assert.Throws<JsonException>(()=>NotesJson.Serialize(book with {Folders=[book.Folders![0] with {Source="cloud",Project=ProjectLink}]}));
        var note=Guid.NewGuid();book=Accept(book,new NoteCommand(Guid.NewGuid(),note,book.Revision,0,NoteOperation.Create,"Exact","body",Folder:new(id,Scope,"browser")));
        Assert.Throws<JsonException>(()=>NotesJson.Serialize(book with {Folders=[book.Folders![0] with {IsRemoved=true,Revision=2}]}));
        Assert.Throws<JsonException>(()=>NotesJson.Deserialize(NotesJson.Serialize(book).Replace("\"schemaVersion\":2","\"schemaVersion\":3")));
    }
    [Fact]
    public async Task Folder_service_recovers_original_receipt_after_lost_ack_and_refuses_cross_source_imports()
    {
        var store=new FolderStore();var service=new NotesService(store);var command=Folder(NotesModel.Empty(Scope),Guid.NewGuid(),NoteFolderOperation.Create,"Hotels");
        store.Lose=true;Assert.Equal("acceptance_unknown",(await service.SubmitFolderAsync(command,default)).Code);store.Lose=false;
        var recovered=await service.SubmitFolderAsync(command,default);Assert.True(recovered.Accepted);Assert.Equal("duplicate",recovered.Code);Assert.Equal(1,store.Writes);
        var rename=Folder(recovered.Notebook,command.FolderId,NoteFolderOperation.Rename,"Renamed");Assert.True((await service.SubmitFolderAsync(rename,default)).Accepted);Assert.Equal(recovered.Receipt,(await service.SubmitFolderAsync(command,default)).Receipt);
        var json=await service.ExportAsync(default);var copy=new NotesService(new FolderStore());Assert.Equal("restored",await copy.ImportAsync(json,default));Assert.Equal(json,await copy.ExportAsync(default));
        Assert.Equal("folder_source_mismatch",await new NotesService(new FolderStore()).ImportAsync(json.Replace("\"browser\"","\"cloud\""),default));
        Assert.Equal("folder_source_mismatch",(await service.SubmitFolderAsync(command with {Id=Guid.NewGuid(),Source="cloud"},default)).Code);
        var projectCreate=Folder(recovered.Notebook,Guid.NewGuid(),NoteFolderOperation.Create,"Trip",ProjectLink);Assert.Equal("project_validation_unavailable",(await service.SubmitFolderAsync(projectCreate,default)).Code);
    }
    private sealed class FolderStore:INotesStore
    {
        public string OwnerScope=>Scope;private NotesNotebook? book;public bool Lose;public int Writes;
        public Task<NotesNotebook?> LoadAsync(CancellationToken token)=>Task.FromResult(book);
        public Task<bool> TryCommitAsync(long expected,NotesNotebook next,CancellationToken token){if((book?.Revision??0)!=expected)return Task.FromResult(false);book=next;Writes++;if(Lose)throw new IOException("Synthetic lost acknowledgment");return Task.FromResult(true);}
    }
    [Fact]
    public async Task Project_folder_and_new_placement_require_atomic_current_authority_even_when_note_project_is_unchanged()
    {
        var store=new FolderProjectStore();var proof=new NotesProjectProof(Authority,"synthetic-folder-owner",7);var service=new NotesService(store,projects:(_,_)=>Task.FromResult<NotesProjectProof?>(proof));
        var id=Guid.NewGuid();var create=Folder(NotesModel.Empty(Scope),id,NoteFolderOperation.Create,"Trip",ProjectLink);store.Refuse=true;
        Assert.Equal("stale_notebook",(await service.SubmitFolderAsync(create,default)).Code);Assert.Null(await store.LoadAsync(default));store.Refuse=false;store.Lose=true;
        Assert.Equal("acceptance_unknown",(await service.SubmitFolderAsync(create,default)).Code);store.Lose=false;var recovered=await new NotesService(store).SubmitFolderAsync(create,default);Assert.Equal("duplicate",recovered.Code);Assert.Equal(2,store.GuardedFolders);
        var note=Guid.NewGuid();var command=new NoteCommand(Guid.NewGuid(),note,recovered.Notebook.Revision,0,NoteOperation.Create,"Note","body",Project:ProjectLink);var first=await service.SubmitAsync(command,default);Assert.True(first.Accepted,first.Code);
        var placement=Change(first.Notebook,note,NoteOperation.Update,folder:new(id,Scope,"browser"));Assert.Equal("project_validation_unavailable",(await new NotesService(store).SubmitAsync(placement,default)).Code);
        store.Refuse=true;Assert.Equal("stale_notebook",(await service.SubmitAsync(placement,default)).Code);store.Refuse=false;Assert.True((await service.SubmitAsync(placement,default)).Accepted);Assert.Equal(3,store.GuardedNotes);
    }
    private sealed class FolderProjectStore:INotesProjectStore
    {
        public string OwnerScope=>Scope;private NotesNotebook? book;public bool Refuse,Lose;public int GuardedFolders,GuardedNotes;
        public Task<NotesNotebook?> LoadAsync(CancellationToken token)=>Task.FromResult(book);
        public Task<bool> TryCommitAsync(long expected,NotesNotebook next,CancellationToken token){if((book?.Revision??0)!=expected)return Task.FromResult(false);book=next;if(Lose)throw new IOException("Synthetic lost acknowledgment");return Task.FromResult(true);}
        public Task<bool> TryCommitProjectAsync(long expected,NotesNotebook next,Guid note,string account,long version,CancellationToken token){Assert.Equal("synthetic-folder-owner",account);Assert.Equal(7,version);GuardedNotes++;return Refuse?Task.FromResult(false):TryCommitAsync(expected,next,token);}
        public Task<bool> TryCommitFolderProjectAsync(long expected,NotesNotebook next,Guid folder,string account,long version,CancellationToken token){Assert.Equal("synthetic-folder-owner",account);Assert.Equal(7,version);GuardedFolders++;return Refuse?Task.FromResult(false):TryCommitAsync(expected,next,token);}
    }
    [Fact]
    public void Folder_capacity_and_shared_receipt_quota_preserve_lifecycle_reserve()
    {
        var folder=Guid.NewGuid();var note=Guid.NewGuid();var count=NotesModel.MaxReceipts-1;
        var folders=new[]{new NoteFolder(folder,1,"Hotels","browser",Now,Now)};var folderReceipt=new NoteFolderReceipt(Guid.NewGuid(),new string('b',64),folder,1,1);
        var current=new NoteVersion(count,"Exact","body",false,Now);var history=Enumerable.Range(count-NotesModel.HistoryLimit,NotesModel.HistoryLimit).Select(r=>current with {Revision=r}).ToArray();
        var book=new NotesNotebook(2,Scope,NotesModel.MaxReceipts,[new(note,current,Now,history)],Enumerable.Range(1,count).Select(r=>new NoteReceipt(Guid.NewGuid(),new string('a',64),note,r,r+1)).ToArray(),[],Folders:folders,FolderReceipts:[folderReceipt]);NotesModel.Validate(book);
        Assert.Equal("notebook_quota",NotesFolders.Apply(book,Folder(book,folder,NoteFolderOperation.Rename,"New"),Now).Code);Assert.Equal("notebook_quota",NotesEngine.Apply(book,Change(book,note,NoteOperation.Update,markdown:"new"),Now).Code);
        book=Accept(book,Folder(book,folder,NoteFolderOperation.Remove));book=Accept(book,Change(book,note,NoteOperation.Trash));book=Accept(book,Change(book,note,NoteOperation.Purge));Assert.Empty(book.Notes);Assert.Single(book.Purged);Assert.True(book.Folders![0].IsRemoved);
        book=NotesModel.Empty(Scope);for(var i=0;i<NotesModel.MaxFolders;i++)book=Accept(book,Folder(book,Guid.NewGuid(),NoteFolderOperation.Create,"Same"));Assert.Equal("folders_quota",NotesFolders.Apply(book,Folder(book,Guid.NewGuid(),NoteFolderOperation.Create,"Extra"),Now).Code);
    }
}

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
