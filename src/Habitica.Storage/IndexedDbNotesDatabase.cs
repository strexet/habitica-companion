using Habitica.Domain.Notes;
using Microsoft.JSInterop;
namespace Habitica.Storage;

public interface INotesStore
{
    string OwnerScope {get;}
    Task<NotesNotebook?> LoadAsync(CancellationToken token);
    Task<bool> TryCommitAsync(long expectedRevision,NotesNotebook notebook,CancellationToken token);
}
public interface INotesProjectStore:INotesStore
{
    Task<bool> TryCommitProjectAsync(long expectedRevision,NotesNotebook notebook,Guid noteId,string workspaceAccountScope,long workspaceStoreVersion,CancellationToken token);
}
public interface INotesStoreFactory {INotesStore ForScope(string scope);}
public sealed class IndexedDbNotesDatabase(IJSRuntime runtime) : IAsyncDisposable,INotesStoreFactory
{
    private readonly Lazy<Task<IJSObjectReference>> _module=new(()=>runtime.InvokeAsync<IJSObjectReference>("import","./js/storage/indexedDbStorage.js").AsTask());
    public INotesStore ForScope(string scope)=>NotesModel.ValidScope(scope)?new Store(this,scope):throw new ArgumentException("Invalid notes owner scope.");
    public async Task ClearAsync(CancellationToken token)=>await (await _module.Value).InvokeVoidAsync("clearNotes",token);
    public async ValueTask DisposeAsync(){if(_module.IsValueCreated)await (await _module.Value).DisposeAsync();}
    private sealed class Store(IndexedDbNotesDatabase database,string scope):INotesProjectStore
    {
        public string OwnerScope=>scope;
        public async Task<NotesNotebook?> LoadAsync(CancellationToken token)
        {
            var json=await (await database._module.Value).InvokeAsync<string?>("getNotes",token,scope);
            if(json is null)return null;var book=NotesJson.Deserialize(json);
            return book.OwnerScope==scope?book:throw new InvalidOperationException("Notes account mismatch.");
        }
        public async Task<bool> TryCommitProjectAsync(long expectedRevision,NotesNotebook notebook,Guid noteId,string workspaceAccountScope,long workspaceStoreVersion,CancellationToken token)
        {
            var link=notebook.Notes.Single(n=>n.Id==noteId).Current.Project??throw new ArgumentException("Project link required.");
            if(notebook.OwnerScope!=scope||NotesModel.Scope(workspaceAccountScope)!=scope||link.Source!="browser"||!NotesModel.ValidProject(link,scope))throw new ArgumentException("Project owner/source mismatch.");
            return await (await database._module.Value).InvokeAsync<bool>("commitNotes",token,scope,expectedRevision,NotesJson.Serialize(notebook),new{accountScope=workspaceAccountScope,workspaceId=link.WorkspaceId.ToString("D"),projectId=link.ProjectId.ToString("D"),noteId=noteId.ToString("D"),workspaceStoreVersion});
        }
        public async Task<bool> TryCommitAsync(long expectedRevision,NotesNotebook notebook,CancellationToken token)
        {
            if(notebook.OwnerScope!=scope||expectedRevision<0||notebook.Revision<=expectedRevision||expectedRevision>0&&notebook.Revision!=expectedRevision+1)throw new ArgumentException("Notes revision/account mismatch.");
            var json=NotesJson.Serialize(notebook);
            return await (await database._module.Value).InvokeAsync<bool>("commitNotes",token,scope,expectedRevision,json);
        }
    }
}
