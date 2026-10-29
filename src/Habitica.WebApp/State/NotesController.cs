using Habitica.Application.Notes;
using Habitica.Domain.Notes;
using Habitica.Storage;
using Habitica.Application.Diagnostics;
using Habitica.Domain.Diagnostics;
namespace Habitica.WebApp.State;

public enum NotesSource {Browser,Cloud}
public sealed record NotesLifecycleReview(Guid NoteId,long NotebookRevision,long NoteRevision,NoteOperation Operation,string Title,int Versions);
public sealed partial class NotesController:IAsyncDisposable
{
    private readonly IAppSessionController _session;
    private readonly INotesStoreFactory _database;
    private readonly INotesCloudClient _cloud;
    private readonly IndexedDbWorkspaceDatabase? _projectDatabase;
    private readonly DiagnosticsLogWriter? _diagnostics;
    public NotesDraftSession Draft {get;}
    private string? _account,_originalTitle,_originalMarkdown;
    private int _generation;
    private NoteCommand? _pending;
    private long _editorNoteRevision;
    private Guid? _pendingNotebook;
    private readonly SemaphoreSlim _gate=new(1,1);
    public event Action? Changed;
    public NotesController(IAppSessionController session,INotesStoreFactory database,INotesCloudClient cloud,NotesDraftSession draft,DiagnosticsLogWriter? diagnostics=null,IndexedDbWorkspaceDatabase? projectDatabase=null,TodoWorkspacePresentation? projects=null)
    {_session=session;_database=database;_cloud=cloud;_projectDatabase=projectDatabase;_projects=projects;if(projects is not null)projects.Changed+=ProjectsChanged;_diagnostics=diagnostics;Draft=draft;session.Changed+=SessionChanged;EnsureAccount();}
    public NotesSource Source {get;private set;}
    public NotesNotebook? Book {get;private set;}
    public Guid? SelectedId {get;private set;}
    public NotesLifecycleReview? Review {get;private set;}
    public bool Busy {get;private set;}
    public bool HasPending=>_pending is not null;
    public Guid? PendingCommandId=>_pending?.Id;
    public bool CloudAvailable=>_cloud.IsAvailable;
    public bool CloudConnected=>_cloud.IsConnected;
    public Guid? CloudNotebookId=>_cloud.NotebookId;
    public string? Message {get;private set;}
    public bool IsError {get;private set;}
    public NoteReceipt? LastReceipt {get;private set;}
    public PersonalNote? Selected=>Book?.Notes.FirstOrDefault(note=>note.Id==SelectedId);
    public bool Dirty=>Draft.Title!=_originalTitle||Draft.Markdown!=_originalMarkdown||Draft.Project!=_originalProject;
    public bool HasConflict=>SelectedId is not null&&(Selected is null||Selected.Current.Revision!=_editorNoteRevision);
    public bool CanWrite=>_account is not null&&Book is not null&&(!Busy&&!HasPending)&&(Source==NotesSource.Browser||CloudConnected);
    private string LocalScope=>NotesModel.Scope(_account??throw new InvalidOperationException("Sign in first."));
    private string DraftScope=>NotesModel.Scope(LocalScope+":"+Source);
    private void SessionChanged(){EnsureAccount();Changed?.Invoke();}
    private void EnsureAccount()
    {
        var account=_session.State.IsAuthenticated&&Guid.TryParse(_session.State.UserId,out var id)?id.ToString("D"):null;
        if(account==_account){if(account is not null&&Draft.OwnerScope!=DraftScope){_generation++;Book=null;SelectedId=null;Review=null;_pending=null;LastReceipt=null;Draft.Bind(DraftScope);ResetDraft(null);}return;}
        _generation++;_account=account;Source=NotesSource.Browser;Book=null;SelectedId=null;Review=null;_pending=null;_pendingNotebook=null;LastReceipt=null;Message=null;ProjectReview=null;_originalProject=null;Draft.Clear();
        _originalTitle=_originalMarkdown=null;
        if(account is not null){Draft.Bind(DraftScope);ResetDraft(null);}
    }
    private bool Current(int generation)=>generation==_generation&&_account is not null&&_session.State.IsAuthenticated;
    private void Say(string message,bool error=false){Message=message;IsError=error;Changed?.Invoke();}
    private void ResetDraft(PersonalNote? note)
    {SelectedId=note?.Id;_editorNoteRevision=note?.Current.Revision??0;Draft.Title=_originalTitle=note?.Current.Title??"";Draft.Markdown=_originalMarkdown=note?.Current.Markdown??"";_originalProject=Draft.Project=note?.Current.Project;Review=null;ProjectReview=null;}
    public bool NewNote(bool discard=false)
    {EnsureAccount();if(HasPending){Say("Check the original save before starting another note.",true);return false;}if(Dirty&&!discard)return false;ResetDraft(null);Message=null;Changed?.Invoke();return true;}
    public bool Open(Guid id,bool discard=false)
    {
        EnsureAccount();if(HasPending||Dirty&&!discard)return false;var note=Book?.Notes.FirstOrDefault(note=>note.Id==id);if(note is null)return false;
        ResetDraft(note);LastReceipt=Book!.Receipts.LastOrDefault(receipt=>receipt.NoteId==id);Changed?.Invoke();return true;
    }
    public async Task<bool> SwitchAsync(NotesSource source,bool discard=false,CancellationToken token=default)
    {
        EnsureAccount();if(_account is null||HasPending||Dirty&&!discard)return false;
        _generation++;Source=source;Book=null;Review=null;LastReceipt=null;Draft.Bind(DraftScope);ResetDraft(null);await LoadAsync(token);return true;
    }
    private Task Observe(string operation,Func<Task> action)
    {
        IsError=false;if(_diagnostics is null)return action();var account=_account;var source=Source;
        return _diagnostics.ObserveAsync(DiagnosticsFeatureArea.Workspace,operation,async()=>{await action();return HasPending?DiagnosticsResult.Unknown:IsError?DiagnosticsResult.Unavailable:DiagnosticsResult.Success;},
            result=>new(result,CommandId:LastReceipt?.CommandId??_pending?.Id),()=>_account==account&&_session.State.IsAuthenticated,
            source==NotesSource.Browser?DiagnosticsMode.Local:operation is "notes-load" or "notes-recover"?DiagnosticsMode.LiveRead:DiagnosticsMode.LiveMutation);
    }
    public Task LoadAsync(CancellationToken token=default)=>Observe("notes-load",()=>LoadCoreAsync(token));
    private async Task LoadCoreAsync(CancellationToken token)
    {
        EnsureAccount();if(_account is null)return;var generation=_generation;
        try
        {
            NotesNotebook? book;
            if(Source==NotesSource.Browser)book=await _database.ForScope(LocalScope).LoadAsync(token)??NotesModel.Empty(LocalScope);
            else {if(!CloudConnected){Book=null;Say("Connect cloud Notes separately to read or save server notes.");return;}var loaded=await _cloud.LoadAsync(token);if(!Current(generation))return;if(!loaded.Succeeded||loaded.Value is null){Book=null;Say("Cloud Notes could not be loaded. Existing drafts were preserved.",true);return;}book=loaded.Value;}
            if(!Current(generation))return;NotesModel.Validate(book);Book=book;
            if(!Dirty&&SelectedId is {} id&&book.Notes.FirstOrDefault(note=>note.Id==id) is {} selected)ResetDraft(selected);
            Changed?.Invoke();
        }
        catch(OperationCanceledException) when(token.IsCancellationRequested){throw;}
        catch{if(Current(generation))Say("Notes are unavailable. Existing drafts were preserved; no save was retried.",true);}
    }
    public Task ConnectAsync(bool consent,CancellationToken token=default)=>Observe("notes-connect",()=>ConnectCoreAsync(consent,token));
    private async Task ConnectCoreAsync(bool consent,CancellationToken token)
    {
        EnsureAccount();if(!consent||_account is null){Say("Accept the separate cloud Notes disclosure before connecting.",true);return;}
        var generation=_generation;var result=await _session.ConnectNotesAsync(consent,token);if(!Current(generation))return;
        if(!result.Succeeded){Say("Cloud Notes connection could not be confirmed. Verify access before trying again.",true);return;}await LoadAsync(token);Say(Book is null?"Cloud connection confirmed, but Notes could not be loaded. Retry the read.":"Cloud Notes connected. Local browser notes were not uploaded.",Book is null);
    }
    public Task SaveAsync(CancellationToken token=default)=>Observe("notes-save",()=>SaveCoreAsync(token));
    private async Task SaveCoreAsync(CancellationToken token)
    {
        EnsureAccount();if(!CanWrite||ProjectReview is not null)return;
        if(HasConflict){Say("The stored note changed. Copy or export this draft, then explicitly reload the stored note before saving.",true);return;}
        if(!NotesModel.TextValid(Draft.Title,Draft.Markdown)){Say("Add a title of up to 256 characters. Markdown must be valid UTF-8 and at most 128 KiB.",true);return;}
        if(Selected?.Current.IsTrashed==true){Say("Restore the note before editing.",true);return;}
        if(SelectedId is not null&&!Dirty){Say("No changes to save.");return;}
        var selected=Selected;var command=new NoteCommand(Guid.NewGuid(),selected?.Id??Guid.NewGuid(),Book!.Revision,selected?.Current.Revision??0,selected is null?NoteOperation.Create:NoteOperation.Update,
            selected is null||Draft.Title!=_originalTitle?Draft.Title:null,selected is null||Draft.Markdown!=_originalMarkdown?Draft.Markdown:null,
            Draft.Project!=_originalProject?Draft.Project:null,selected is not null&&Draft.Project is null&&_originalProject is not null?true:null);
        if(Draft.Project!=_originalProject)
        {
            if(Draft.Project is not null&&!EligibleProject(Draft.Project)){Say("Choose an active project in the matching Notes source. The draft was preserved.",true);return;}
            ProjectReview=new(command,_originalProject,Draft.Project,ProjectLabel(_originalProject),ProjectLabel(Draft.Project));Changed?.Invoke();return;
        }
        await SubmitAsync(command,token);
    }
    public bool PrepareLifecycle(NoteOperation operation)
    {
        EnsureAccount();var note=Selected;if(!CanWrite||note is null||Dirty||operation is not (NoteOperation.Trash or NoteOperation.Restore or NoteOperation.Purge))return false;
        if(note.Current.IsTrashed?operation==NoteOperation.Trash:operation!=NoteOperation.Trash)return false;
        Review=new(note.Id,Book!.Revision,note.Current.Revision,operation,note.Current.Title,note.History.Count+1);Changed?.Invoke();return true;
    }
    public void CancelReview(){Review=null;ProjectReview=null;Changed?.Invoke();}
    public Task AcceptLifecycleAsync(CancellationToken token=default)=>Observe("notes-lifecycle",()=>AcceptLifecycleCoreAsync(token));
    private async Task AcceptLifecycleCoreAsync(CancellationToken token)
    {
        EnsureAccount();if(Review is not {} review||!CanWrite)return;
        if(Book!.Revision!=review.NotebookRevision||SelectedId!=review.NoteId||Selected?.Current.Revision!=review.NoteRevision){Review=null;Say("The note changed. Refresh and review the action again.",true);return;}
        await SubmitAsync(new(Guid.NewGuid(),review.NoteId,review.NotebookRevision,review.NoteRevision,review.Operation),token);
    }
    private async Task SubmitAsync(NoteCommand command,CancellationToken token)
    {
        await _gate.WaitAsync(token);var generation=_generation;
        try
        {
            if(!CanWrite)return;Busy=true;_pending=command;_pendingNotebook=Source==NotesSource.Cloud?_cloud.NotebookId:null;Changed?.Invoke();
            bool accepted;string code;NoteReceipt? receipt;
            if(Source==NotesSource.Browser){var result=await new NotesService(_database.ForScope(LocalScope),projects:ResolveLocalProject).SubmitAsync(command,token);accepted=result.Accepted;code=result.Code;receipt=result.Receipt;}
            else {var result=await _cloud.SubmitAsync(command,token);accepted=result.Succeeded;code=result.Code;receipt=result.Value;if(!accepted&&!result.OutcomeUnknown)_pending=null;}
            if(!Current(generation))return;
            if(!accepted){if(code!="acceptance_unknown"&&Source==NotesSource.Browser)_pending=null;Say(_pending is null?"The save was refused or the revision changed. Your draft is preserved; refresh before reviewing another save.":"Save outcome is unknown. Check the original save; do not create a replacement.",true);return;}
            await ConfirmAsync(command,receipt!,generation,token);
        }
        catch(OperationCanceledException) when(token.IsCancellationRequested){if(Current(generation))Say("Save outcome is unknown. Check the original save before trying another write.",true);}
        catch{if(Current(generation))Say("Save outcome is unknown. Check the original save before trying another write.",true);}
        finally{Busy=false;_gate.Release();Changed?.Invoke();}
    }
    private async Task<NotesProjectProof?> ResolveLocalProject(Habitica.Domain.Notes.NoteProjectLink link,CancellationToken token)
    {
        if(_projectDatabase is null||_account is null||Source!=NotesSource.Browser||link.Source!="browser"||link.OwnerScope!=LocalScope)return null;
        var account=_account;var snapshot=await _projectDatabase.ForScope(account).LoadAsync(link.WorkspaceId,token);
        if(account!=_account||snapshot is null||snapshot.Authority?.Mode is Habitica.Domain.Workspace.WorkspaceAuthorityMode.Frozen or Habitica.Domain.Workspace.WorkspaceAuthorityMode.Cloud)return null;
        return new(new(LocalScope,"browser",snapshot.Document),account,snapshot.StoreVersion);
    }
    private async Task ConfirmAsync(NoteCommand command,NoteReceipt receipt,int generation,CancellationToken token)
    {
        if(!Current(generation))return;_pending=null;LastReceipt=receipt;Review=null;
        // Accepted receipt stays confirmed even when the following read fails.
        if(command.Operation==NoteOperation.Purge)ResetDraft(null);else SelectedId=command.NoteId;
        _originalTitle=Draft.Title;_originalMarkdown=Draft.Markdown;_originalProject=Draft.Project;ProjectReview=null;Book=null;
        await LoadAsync(token);if(!Current(generation))return;
        var note=Book?.Notes.FirstOrDefault(note=>note.Id==command.NoteId);if(note is not null)ResetDraft(note);
        Say(command.Operation==NoteOperation.Purge?"Note and retained versions permanently deleted.":Book is null?"Saved. Original receipt confirmed; refreshing the note is unavailable.":note?.Current.Revision>receipt.NoteRevision?"Saved. Original receipt confirmed; the current note contains a later revision.":"Saved. Original command receipt confirmed.");
    }
    public Task RecoverAsync(CancellationToken token=default)=>Observe("notes-recover",()=>RecoverCoreAsync(token));
    private async Task RecoverCoreAsync(CancellationToken token)
    {
        EnsureAccount();if(_pending is not {} command||Busy)return;var generation=_generation;
        if(Source==NotesSource.Cloud&&_pendingNotebook!=_cloud.NotebookId){Say("The original save belongs to another notebook. No command was replayed.",true);return;}
        try
        {
            NoteReceipt? receipt;
            if(Source==NotesSource.Browser){var book=await _database.ForScope(LocalScope).LoadAsync(token);receipt=book?.Receipts.FirstOrDefault(receipt=>receipt.CommandId==command.Id);if(receipt?.PayloadHash!=NotesJson.Hash(command))receipt=null;}
            else {var result=await _cloud.RecoverAsync(command,token);receipt=result.Succeeded?result.Value:null;}
            if(!Current(generation))return;
            if(receipt is null){Say("The original receipt is unavailable. The save remains uncertain; no replacement was sent.",true);return;}await ConfirmAsync(command,receipt,generation,token);
        }
        catch{if(Current(generation))Say("The original receipt is unavailable. No save was retried.",true);}
    }
    public bool ImportMarkdown(string name,string markdown,bool discard=false)
    {
        EnsureAccount();if(HasPending||_account is null||Dirty&&!discard)return false;var title=Path.GetFileNameWithoutExtension(name);
        if(!NotesModel.TextValid(title,markdown)){Say("The Markdown file exceeds the note limits or contains invalid text.",true);return false;}
        ResetDraft(null);Draft.Title=title;Draft.Markdown=markdown;Say("File loaded into an unsaved draft. Review and save in the selected source.");return true;
    }
    public bool UseVersion(long revision,bool discard=false)
    {
        EnsureAccount();if(HasPending||Dirty&&!discard||Selected is not {} note)return false;
        var version=note.History.Append(note.Current).FirstOrDefault(version=>version.Revision==revision);if(version is null)return false;
        ResetDraft(note);Draft.Title=version.Title;Draft.Markdown=version.Markdown;Draft.Project=version.Project;Say("Stored revision loaded into a draft. Saving creates a new revision and retains current history.");return true;
    }
    public Task DisconnectCloudAsync(bool delete,CancellationToken token=default)=>Observe("notes-cloud-cleanup",()=>DisconnectCloudCoreAsync(delete,token));
    private async Task DisconnectCloudCoreAsync(bool delete,CancellationToken token)
    {
        EnsureAccount();if(_account is null||HasPending)return;var generation=_generation;
        var result=await _cloud.DisconnectAsync(delete,token);if(!Current(generation))return;
        if(!result.Succeeded){_cloud.ForgetSession();Book=null;ResetDraft(null);Review=null;Say("Cloud cleanup could not be confirmed. This tab was disconnected. Reconnect to inspect the original notebook before another action.",true);return;}
        _generation++;Book=null;SelectedId=null;Review=null;ResetDraft(null);LastReceipt=null;Say(delete?"Cloud Notes notebook deleted. Browser notes and downloaded files remain separate.":"Cloud Notes disconnected. Server notes remain until explicit deletion.");
    }
    public async ValueTask DisposeAsync(){_session.Changed-=SessionChanged;if(_projects is not null)_projects.Changed-=ProjectsChanged;await _gate.WaitAsync();_gate.Release();_gate.Dispose();}
}
