using Habitica.Application.Notes;
using Habitica.Domain.Auth;
using Habitica.Domain.Notes;
using Habitica.Storage;
using Habitica.WebApp.State;
namespace Habitica.WebApp.Tests.Notes;
internal sealed class NotesFixture
{
    public FakeAppSessionController Session {get;}
    public MemoryFactory Database {get;}=new();
    public Cloud Client {get;}=new();
    public NotesController Controller {get;}
    public NotesFixture(TodoWorkspacePresentation? projects=null,IndexedDbWorkspaceDatabase? projectDatabase=null,FakeAppSessionController? session=null){Session=session??new(SessionViewModel.Empty with{IsAuthenticated=true,UserId="00000000-0000-4000-8000-000000000011"});Controller=new(Session,Database,Client,new NotesDraftSession(),projectDatabase:projectDatabase,projects:projects);Session.NotesConnectAction=()=>{Client.Connected=true;return Task.FromResult(new NotesCloudResult<bool>(true,true,"connected"));};}
    public sealed class MemoryFactory:INotesStoreFactory
    {
        public Dictionary<string,MemoryStore> Stores {get;}=[];
        public INotesStore ForScope(string scope){if(!Stores.TryGetValue(scope,out var store))Stores[scope]=store=new(scope);return store;}
        public MemoryStore Current=>Stores.Values.First();
    }
    public sealed class MemoryStore(string scope):INotesProjectStore
    {
        public string OwnerScope=>scope; public NotesNotebook? Book {get;set;}
        public int ProjectCommits {get;private set;}
        public Task<bool> TryCommitProjectAsync(long expected,NotesNotebook value,Guid noteId,string account,long version,CancellationToken token){ProjectCommits++;return TryCommitAsync(expected,value,token);}
        public bool LoseResponse {get;set;}public bool Unavailable {get;set;}
        public TaskCompletionSource? Entered {get;set;}public TaskCompletionSource? Release {get;set;}
        public async Task<NotesNotebook?> LoadAsync(CancellationToken token){Entered?.TrySetResult();if(Release is not null)await Release.Task;if(Unavailable)throw new IOException();return Book;}
        public Task<bool> TryCommitAsync(long expected,NotesNotebook book,CancellationToken token){if((Book?.Revision??0)!=expected)return Task.FromResult(false);Book=book;if(LoseResponse){LoseResponse=false;throw new IOException();}return Task.FromResult(true);}
    }
    public sealed class Cloud:INotesCloudClient
    {
        public NotesProjectAuthority? ProjectAuthority {get;set;}
        public bool Connected {get;set;}
        public bool IsAvailable {get;set;}=true;public bool IsConnected=>Connected;
        public Guid? NotebookId=>Connected?Id:null;public Guid Id {get;set;}=Guid.NewGuid();public string? OwnerScope=>Book.OwnerScope;
        public NotesNotebook Book {get;set;}=NotesModel.Empty(NotesModel.Scope("synthetic-profile"));
        public bool LoseResponse {get;set;}public bool FailLoads {get;set;}public int Submits {get;private set;}public int Recoveries {get;private set;}public int CleanupCalls {get;private set;}public int Forgotten {get;private set;}
        public Task<NotesCloudResult<bool>> ConnectAsync(HabiticaCredentials credentials,CancellationToken token){Connected=true;return Task.FromResult(new NotesCloudResult<bool>(true,true,"connected"));}
        public Task<NotesCloudResult<NotesNotebook>> LoadAsync(CancellationToken token)=>Task.FromResult(new NotesCloudResult<NotesNotebook>(!FailLoads,FailLoads?null:Book,"read"));
        public Task<NotesCloudResult<NoteReceipt>> SubmitAsync(NoteCommand command,CancellationToken token){Submits++;var result=NotesEngine.Apply(Book,command,DateTimeOffset.UtcNow,ProjectAuthority);Book=result.Notebook;if(LoseResponse){LoseResponse=false;return Task.FromResult(new NotesCloudResult<NoteReceipt>(false,null,"acceptance_unknown",true));}return Task.FromResult(new NotesCloudResult<NoteReceipt>(result.Accepted,result.Receipt,result.Code));}
        public Task<NotesCloudResult<NoteReceipt>> RecoverAsync(NoteCommand command,CancellationToken token){Recoveries++;var receipt=Book.Receipts.FirstOrDefault(r=>r.CommandId==command.Id&&r.PayloadHash==NotesJson.Hash(command));return Task.FromResult(new NotesCloudResult<NoteReceipt>(receipt is not null,receipt,"read"));}
        public Task<NotesCloudResult<NotesSearchPage>> SearchAsync(string query,bool trash,int offset,long? expectedRevision,CancellationToken token)=>throw new NotSupportedException();
        public Task<NotesCloudResult<CloudNote>> ReadAsync(Guid id,CancellationToken token)=>throw new NotSupportedException();
        public Task<NotesCloudResult<bool>> DisconnectAsync(bool delete,CancellationToken token){CleanupCalls++;Connected=false;if(delete)Book=NotesModel.Empty(Book.OwnerScope);return Task.FromResult(new NotesCloudResult<bool>(true,true,"done"));}
        public void ForgetSession(){Forgotten++;Connected=false;}
    }
}
