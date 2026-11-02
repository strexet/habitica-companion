using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Habitica.Domain.Workspace;
using Habitica.Domain.Notes;
using Habitica.Storage;
namespace Habitica.Application.Workspace;

public sealed class WorkspaceTransferService(IBrowserWorkspaceTransfer browser,TimeProvider? clock=null)
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    public async Task<BrowserTransferState> CaptureAsync(string account,Guid target,CancellationToken token)
    {
        account=Guid.Parse(account).ToString("D");if(target==Guid.Empty)throw new ArgumentException("Exact cloud Workspace required.");
        var owner=NotesModel.Scope(account);var inventory=await browser.InventoryAsync(account,owner,token);
        if(inventory.PendingJson is {} pending){var original=JsonSerializer.Deserialize<BrowserTransferState>(pending,Json)??throw new InvalidOperationException("Original transfer unavailable.");Validate(original,account,target);return original;}
        var id=Guid.NewGuid();var now=(clock??TimeProvider.System).GetUtcNow();var plans=new List<BrowserTransferPlan>();
        if(inventory.NotesJson is {} notes)_=NotesJson.Deserialize(notes);
        foreach(var text in inventory.Journals)
        {
            var before=WorkspaceJournalJson.Deserialize(text);
            if(before.Authority?.Mode==WorkspaceAuthorityMode.Cloud&&before.Authority.CloudWorkspaceId==target&&before.Document.Id==target)
            { plans.Add(new(text,text,true));continue; }
            if(before.Authority?.Mode is not (null or WorkspaceAuthorityMode.Local))throw new InvalidOperationException("Recover the original frozen transfer before preparing another transfer.");
            if(before.Commands.Any(entry=>entry.ProjectionState is ProjectionCommandState.Leased or ProjectionCommandState.Executing&&entry.Lease!.ExpiresAtUtc>now))throw new InvalidOperationException("Resolve the original active attempt before transfer.");
            var recovered=ProjectionJournal.RecoverExpired(before,now);
            var frozen=recovered with{StoreVersion=before.StoreVersion<WorkspaceDocument.MaxRevision?before.StoreVersion+1:throw new InvalidOperationException("Revision exhausted."),Authority=new(WorkspaceAuthorityMode.Frozen,id)};
            plans.Add(new(text,WorkspaceJournalJson.Serialize(frozen)));
        }
        var state=await browser.FreezeAsync(account,owner,id,target,plans,token);Validate(state,account,target);return state;
    }
    public static (Guid TransferId,string Hash) Validate(BrowserTransferState state,string account,Guid target)
    {
        if(Encoding.UTF8.GetByteCount(state.Artifact)>8*1024*1024)throw new InvalidOperationException("Transfer capacity exceeded.");
        using var doc=JsonDocument.Parse(state.Artifact);var value=doc.RootElement;
        if(value.GetProperty("version").GetString()!="companion-adoption-v1"||value.GetProperty("ownerScope").GetString()!=NotesModel.Scope(Guid.Parse(account).ToString("D"))||value.GetProperty("targetWorkspaceId").GetGuid()!=target)throw new InvalidOperationException("Original transfer source or target mismatch.");
        var id=value.GetProperty("transferId").GetGuid();if(id==Guid.Empty)throw new InvalidOperationException("Original transfer identity missing.");
        return(id,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(state.Artifact))).ToLowerInvariant());
    }
    public Task<bool> AcknowledgeAsync(string account,Guid target,BrowserTransferState state,string receipt,CancellationToken token)
    {
        var(id,hash)=Validate(state,account,target);using var doc=JsonDocument.Parse(receipt);var value=doc.RootElement;
        if(value.GetProperty("state").GetString()!="accepted"||value.GetProperty("transferId").GetGuid()!=id||value.GetProperty("workspaceId").GetGuid()!=target||value.GetProperty("artifactHash").GetString()!=hash)throw new InvalidOperationException("Original acceptance receipt mismatch.");
        return browser.AcknowledgeAsync(account,NotesModel.Scope(account),id,hash,receipt,token);
    }
}
