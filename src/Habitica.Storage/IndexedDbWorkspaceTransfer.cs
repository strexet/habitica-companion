using Habitica.Domain.Workspace;
using Microsoft.JSInterop;
namespace Habitica.Storage;

public sealed record BrowserTransferInventory(IReadOnlyList<string> Journals,string? NotesJson,string? PendingJson);
public sealed record BrowserTransferPlan(string BeforeJson,string FrozenJson,bool RetainCloud=false);
public sealed record BrowserTransferState(string Artifact,bool Acknowledged,string? Receipt);
public interface IBrowserWorkspaceTransfer
{
    Task<BrowserTransferInventory> InventoryAsync(string account,string ownerScope,CancellationToken token);
    Task<BrowserTransferState> FreezeAsync(string account,string ownerScope,Guid transferId,Guid target,IReadOnlyList<BrowserTransferPlan> plans,CancellationToken token,string? expectedNotesJson=null);
    Task<bool> AcknowledgeAsync(string account,string ownerScope,Guid transferId,string hash,string receipt,CancellationToken token);
}
public sealed class IndexedDbWorkspaceTransfer(IJSRuntime runtime):IBrowserWorkspaceTransfer,IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _module=new(()=>runtime.InvokeAsync<IJSObjectReference>("import","./js/storage/indexedDbStorage.js").AsTask());
    private static readonly System.Text.Json.JsonSerializerOptions Json=new(System.Text.Json.JsonSerializerDefaults.Web);
    public async Task<BrowserTransferInventory> InventoryAsync(string account,string ownerScope,CancellationToken token)=>await(await _module.Value).InvokeAsync<BrowserTransferInventory>("getWorkspaceTransferInventory",token,account,ownerScope);
    public async Task<BrowserTransferState> FreezeAsync(string account,string ownerScope,Guid transferId,Guid target,IReadOnlyList<BrowserTransferPlan> plans,CancellationToken token,string? expectedNotesJson=null)
    {
        var json=await(await _module.Value).InvokeAsync<string>("freezeWorkspaceTransfer",token,account,ownerScope,transferId.ToString("D"),target.ToString("D"),plans,expectedNotesJson);
        return System.Text.Json.JsonSerializer.Deserialize<BrowserTransferState>(json,Json)??throw new InvalidOperationException("Transfer state unavailable.");
    }
    public async Task<bool> AcknowledgeAsync(string account,string ownerScope,Guid transferId,string hash,string receipt,CancellationToken token)=>await(await _module.Value).InvokeAsync<bool>("acknowledgeWorkspaceTransfer",token,account,ownerScope,transferId.ToString("D"),hash,receipt);
    public async ValueTask DisposeAsync(){if(_module.IsValueCreated)await(await _module.Value).DisposeAsync();}
}
