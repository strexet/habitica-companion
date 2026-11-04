using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Notes;
using Habitica.Domain.Workspace;
using Habitica.Storage;

namespace Habitica.WebApp.Tests.Integration;
public sealed class WorkspaceTransferTests
{
    private const string Account="00000000-0000-4000-8000-000000000011";
    private static readonly Guid Target=Guid.NewGuid();
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    private sealed class Browser:IBrowserWorkspaceTransfer
    {
        public BrowserTransferInventory Inventory=new([],null,null);public int Captures;public bool Acknowledged;public IReadOnlyList<BrowserTransferPlan>? LastPlans;
        public Task<BrowserTransferInventory> InventoryAsync(string account,string owner,CancellationToken token)=>Task.FromResult(Inventory);
        public Task<BrowserTransferState> FreezeAsync(string account,string owner,Guid id,Guid target,IReadOnlyList<BrowserTransferPlan> plans,CancellationToken token,string? expectedNotesJson=null)
        {
            Captures++;LastPlans=plans;var artifact=JsonSerializer.Serialize(new{version="companion-adoption-v1",transferId=id,ownerScope=owner,targetWorkspaceId=target,workspaces=plans.Where(plan=>!plan.RetainCloud).Select(plan=>new{workspaceId=WorkspaceJournalJson.Deserialize(plan.FrozenJson).Document.Id,archive=plan.FrozenJson}),notes=Inventory.NotesJson},Json);
            var state=new BrowserTransferState(artifact,false,null);Inventory=Inventory with{PendingJson=JsonSerializer.Serialize(state,Json)};return Task.FromResult(state);
        }
        public Task<bool> AcknowledgeAsync(string account,string owner,Guid id,string hash,string receipt,CancellationToken token){Acknowledged=true;return Task.FromResult(true);}
    }
    [Fact]public async Task Restart_capture_reuses_exact_original_intent_and_requires_exact_receipt()
    {
        var local=new Browser{Inventory=new([WorkspaceJournalJson.Serialize(new(new WorkspaceDocument{Id=Guid.NewGuid()},1,[]))],null,null)};
        var service=new WorkspaceTransferService(local);var first=await service.CaptureAsync(Account,Target,default);var second=await new WorkspaceTransferService(local).CaptureAsync(Account,Target,default);
        Assert.Equal(first.Artifact,second.Artifact);Assert.Equal(1,local.Captures);
        var(id,hash)=WorkspaceTransferService.Validate(first,Account,Target);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.AcknowledgeAsync(Account,Target,first,JsonSerializer.Serialize(new{state="accepted",transferId=Guid.NewGuid(),workspaceId=Target,artifactHash=hash}),default));Assert.False(local.Acknowledged);
        Assert.True(await service.AcknowledgeAsync(Account,Target,first,JsonSerializer.Serialize(new{state="accepted",transferId=id,workspaceId=Target,artifactHash=hash}),default));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.CaptureAsync(Account,Guid.NewGuid(),default));Assert.Equal(1,local.Captures);
    }
    [Fact]public async Task Legacy_frozen_authority_cannot_acquire_replacement_transfer_identity()
    {
        var local=new Browser{Inventory=new([WorkspaceJournalJson.Serialize(new(new WorkspaceDocument{Id=Guid.NewGuid()},1,[],Authority:new(WorkspaceAuthorityMode.Frozen,Guid.NewGuid())))],null,null)};
        await Assert.ThrowsAsync<InvalidOperationException>(()=>new WorkspaceTransferService(local).CaptureAsync(Account,Target,default));Assert.Equal(0,local.Captures);
    }
    [Fact]public async Task Already_owned_cloud_recovery_copy_is_retained_without_recapturing_tasks()
    {
        var cloud=new WorkspaceJournalSnapshot(new WorkspaceDocument{Id=Target},1,[],Authority:new(WorkspaceAuthorityMode.Cloud,Guid.NewGuid(),Target));
        var local=new Browser{Inventory=new([WorkspaceJournalJson.Serialize(cloud)],null,null)};
        var state=await new WorkspaceTransferService(local).CaptureAsync(Account,Target,default);
        using var json=JsonDocument.Parse(state.Artifact);Assert.Equal(0,json.RootElement.GetProperty("workspaces").GetArrayLength());
    }
    [Fact]public void Transfer_marker_preserves_real_note_revisions_without_fabricating_command_receipts()
    {
        var id=Guid.NewGuid();var owner=NotesModel.Scope(Account);var at=DateTimeOffset.Parse("2026-10-01T00:00:00Z");
        var note=new PersonalNote(id,new(2,"Заметка 😀","Retained body",false,at.AddHours(1)),at,[new(1,"Original","First body",false,at)]);
        var book=new NotesNotebook(1,owner,1,[note],[],[],[new(Guid.NewGuid(),1,[new(id,2)])]);
        NotesModel.Validate(book);var roundTrip=NotesJson.Deserialize(NotesJson.Serialize(book));Assert.Empty(roundTrip.Receipts);Assert.Equal(2,roundTrip.Notes[0].Current.Revision);
        var command=new NoteCommand(Guid.NewGuid(),id,1,2,NoteOperation.Update,Markdown:"Later edit");
        var saved=Habitica.Application.Notes.NotesEngine.Apply(book,command,at.AddHours(2));
        NotesModel.Validate(saved.Notebook!);Assert.Single(saved.Notebook!.Receipts);Assert.Equal(3,saved.Notebook.Notes[0].Current.Revision);
        Assert.Throws<JsonException>(()=>NotesModel.Validate(book with{Transfers=null}));
        Assert.Throws<JsonException>(()=>NotesModel.Validate(book with{Transfers=[new(Guid.NewGuid(),1,[new(id,1)])]}));
    }
    [Theory][InlineData("accepted-journal.json")][InlineData("corrected-journal.json")][InlineData("removed-journal.json")]
    public void Accepted_D1_journals_are_readable_by_the_application(string file)
    {
        var snapshot=WorkspaceJournalJson.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","workspace-transfer",file)));
        Assert.NotEmpty(snapshot.Document.SourceImports!);Assert.Empty(WorkspaceRules.Validate(snapshot.Document));
    }
    [Fact]public void Accepted_D1_Notes_keep_original_note_revisions_and_transfer_baselines()
    {
        var book=NotesJson.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","workspace-transfer","accepted-notes.json")));
        Assert.Single(book.Transfers!);Assert.Equal(2,book.Notes[0].Current.Revision);Assert.Empty(book.Receipts);
    }
    [Fact]public void Unsaved_browser_task_drafts_require_explicit_resolution_and_remain_exportable()
    {
        var fixture=new Habitica.WebApp.Tests.Workspace.WorkspaceTaskFixture();using var drafts=new Habitica.WebApp.State.TodoTaskDraftStore(fixture.Session);
        var source=new Habitica.WebApp.State.TodoWorkspaceSelection(Habitica.WebApp.State.TodoWorkspaceSource.Browser,Guid.NewGuid());
        var draft=drafts.Get(source,CompanionTaskKind.Todo,null);Assert.Equal(0,drafts.UnsavedBrowserDrafts);draft.Text="Retain private unsaved text";
        Assert.Equal(1,drafts.UnsavedBrowserDrafts);Assert.Contains("Retain private unsaved text",drafts.ExportUnsavedBrowserDrafts());
        drafts.DiscardBrowserDrafts();Assert.Equal(0,drafts.UnsavedBrowserDrafts);Assert.DoesNotContain("Retain private unsaved text",drafts.ExportUnsavedBrowserDrafts());
    }
    [Fact]public async Task Unsupported_or_foreign_folder_notebooks_refuse_before_freeze()
    {
        var owner=NotesModel.Scope(Account);var bad=new Browser{Inventory=new([],"{\"schemaVersion\":3}",null)};
        await Assert.ThrowsAsync<JsonException>(()=>new WorkspaceTransferService(bad).CaptureAsync(Account,Target,default));Assert.Equal(0,bad.Captures);
        var folder=new NoteFolder(Guid.NewGuid(),1,"Private folder","cloud",DateTimeOffset.Parse("2026-11-01T00:00:00Z"),DateTimeOffset.Parse("2026-11-01T00:00:00Z"));
        var book=new NotesNotebook(2,owner,1,[],[],[],null,[folder],[new(Guid.NewGuid(),new string('a',64),folder.Id,1,1)]);
        bad.Inventory=new([],NotesJson.Serialize(book),null);await Assert.ThrowsAsync<InvalidOperationException>(()=>new WorkspaceTransferService(bad).CaptureAsync(Account,Target,default));Assert.Equal(0,bad.Captures);
    }
    [Fact]public void Imported_folder_baselines_remain_causal_and_allow_later_rename_without_fabricated_receipts()
    {
        var owner=NotesModel.Scope(Account);var at=DateTimeOffset.Parse("2026-11-01T00:00:00Z");var folder=new NoteFolder(Guid.NewGuid(),2,"Imported","cloud",at,at);
        var marker=new NotesTransferMarker(Guid.NewGuid(),1,[],[new(folder.Id,2)]);var book=new NotesNotebook(2,owner,1,[],[],[],[marker],[folder],[]);
        NotesModel.Validate(book);Assert.Equal(book,NotesJson.Deserialize(NotesJson.Serialize(book)) with{Transfers=book.Transfers,Folders=book.Folders,FolderReceipts=book.FolderReceipts,Notes=book.Notes,Receipts=book.Receipts,Purged=book.Purged});
        Assert.Throws<JsonException>(()=>NotesModel.Validate(book with{Transfers=[marker with{Folders=null}]}));
        var after=book with{Revision=2,Folders=[folder with{Revision=3,Name="Renamed"}],FolderReceipts=[new(Guid.NewGuid(),new string('b',64),folder.Id,3,2)]};NotesModel.Validate(after);
    }

    [Fact]public void Actual_schema2_D1_adoption_preserves_folder_membership_and_original_revision_baselines()
    {
        var book=NotesJson.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","workspace-transfer","folder-adoption-accepted.json")));
        Assert.Equal(2,book.SchemaVersion);Assert.Single(book.Folders!);Assert.Empty(book.FolderReceipts!);Assert.Equal(book.Folders![0].Id,book.Notes[0].Current.Folder!.FolderId);Assert.Equal("cloud",book.Notes[0].Current.Folder!.Source);Assert.Single(book.Transfers![0].Folders!);
    }

    [Fact]public async Task Cross_runtime_capture_preserves_accepted_snapshot_and_event_timestamp_encodings()
    {
        var inventory=JsonSerializer.Deserialize<BrowserTransferInventory>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","workspace-transfer","folder-browser-inventory.json")),Json)!;
        var book=NotesJson.Deserialize(inventory.NotesJson!);var browser=new Browser{Inventory=inventory};
        // The fixture belongs to the exact synthetic account used by the local Worker/browser control.
        var account="00000000-0000-4000-8000-000000000011";Assert.Equal(NotesModel.Scope(account),book.OwnerScope);
        await new WorkspaceTransferService(browser).CaptureAsync(account,Target,default);
        using var before=JsonDocument.Parse(inventory.Journals[0]);using var after=JsonDocument.Parse(browser.LastPlans![0].FrozenJson);
        Assert.Equal(before.RootElement.GetProperty("commands")[0].GetProperty("taskSnapshot").GetProperty("createdAtUtc").GetString(),after.RootElement.GetProperty("commands")[0].GetProperty("taskSnapshot").GetProperty("createdAtUtc").GetString());
        Assert.Equal(before.RootElement.GetProperty("events")[0].GetProperty("atUtc").GetString(),after.RootElement.GetProperty("events")[0].GetProperty("atUtc").GetString());
    }

}
