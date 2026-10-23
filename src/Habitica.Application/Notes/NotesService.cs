using Habitica.Domain.Notes;
using Habitica.Storage;
namespace Habitica.Application.Notes;

public sealed class NotesService(INotesStore store,TimeProvider? clock=null)
{
    public async Task<NoteSubmission> SubmitAsync(NoteCommand command,CancellationToken token)
    {
        var current=await store.LoadAsync(token)??NotesModel.Empty(store.OwnerScope);
        var result=NotesEngine.Apply(current,command,(clock??TimeProvider.System).GetUtcNow());
        if(!result.Accepted||result.Code=="duplicate")return result;
        try
        {
            if(await store.TryCommitAsync(current.Revision,result.Notebook,token))return result;
            var latest=await store.LoadAsync(token)??NotesModel.Empty(store.OwnerScope);
            var recovered=NotesEngine.Apply(latest,command,(clock??TimeProvider.System).GetUtcNow());
            return recovered.Code=="duplicate"?recovered:new(false,"stale_notebook",latest);
        }
        catch { return new(false,"acceptance_unknown",current); }
    }
    public async Task<string> ExportAsync(CancellationToken token)=>NotesJson.Serialize(await store.LoadAsync(token)??NotesModel.Empty(store.OwnerScope));
    public async Task<string> ImportAsync(string json,CancellationToken token)
    {
        var incoming=NotesJson.Deserialize(json);
        if(incoming.OwnerScope!=store.OwnerScope)return "account_mismatch";
        var current=await store.LoadAsync(token);
        if(current is not null)return NotesJson.Serialize(current)==NotesJson.Serialize(incoming)?"unchanged":"conflict";
        if(incoming.Revision==0)return "unchanged";
        return await store.TryCommitAsync(0,incoming,token)?"restored":"conflict";
    }
}
// Private draft ownership is volatile and never enters exports, generic sync or command receipts.
public sealed class NotesDraftSession
{
    public string? OwnerScope {get;private set;}
    public string? Title {get;set;}
    public string? Markdown {get;set;}
    public void Bind(string? scope){if(scope is not null&&!NotesModel.ValidScope(scope))throw new ArgumentException("Invalid draft scope.");if(scope!=OwnerScope){Title=Markdown=null;OwnerScope=scope;}}
    public void Clear(){Title=Markdown=null;OwnerScope=null;}
}
