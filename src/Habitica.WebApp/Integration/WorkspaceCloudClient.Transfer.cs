using System.Net.Http.Json;
using System.Text.Json;
using Habitica.Application.Workspace;
namespace Habitica.WebApp.Integration;
public sealed partial class WorkspaceCloudClient:IWorkspaceTransferCloudClient
{
    public Task<WorkspaceActionResult> StageAsync(string artifact,CancellationToken token)=>TransferRequestAsync("workspace-transfer/stage",new{artifact},token);
    public Task<WorkspaceActionResult> PreviewTransferAsync(Guid transferId,bool archiveConflicts,CancellationToken token)=>TransferRequestAsync("workspace-transfer/api",new{tool="preview_workspace_transfer",arguments=new{transferId,archiveConflicts}},token);
    public Task<WorkspaceActionResult> AcceptTransferAsync(Guid transferId,string reviewHash,CancellationToken token)=>TransferRequestAsync("workspace-transfer/api",new{tool="accept_workspace_transfer",arguments=new{transferId,reviewHash}},token);
    public Task<WorkspaceActionResult> ReadTransferReceiptAsync(Guid transferId,CancellationToken token)=>TransferRequestAsync("workspace-transfer/api",new{tool="get_workspace_transfer_receipt",arguments=new{transferId}},token);
    public Task<WorkspaceActionResult> ExportTransferAsync(Guid transferId,CancellationToken token)=>TransferRequestAsync("workspace-transfer/export/"+transferId.ToString("D"),null,token);
    private async Task<WorkspaceActionResult> TransferRequestAsync(string path,object? body,CancellationToken token)
    {
        var generation=_generation;var workspaceId=WorkspaceId;
        if(workspaceId is null||!ConnectionStatusVerified)return new(false,"Restore verified access to the exact cloud Workspace first.",KnownNotAccepted:true);
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            using var request=Request(body is null?HttpMethod.Get:HttpMethod.Post,path);if(body is not null)request.Content=JsonContent.Create(body);
            using var response=await _http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,deadline.Token);
            var text=await BoundedTextAsync(response.Content,deadline.Token);
            if(generation!=_generation||WorkspaceId!=workspaceId)return new(false,"The account changed. Recover the original transfer after restoring access.",OutcomeUnknown:true);
            if(!response.IsSuccessStatusCode)
            {
                string code="transfer_unavailable";try{using var error=JsonDocument.Parse(text);var value=error.RootElement.GetProperty("error").GetString();if(value is {Length:>0 and <80}&&value.All(c=>c is >= 'a' and <= 'z' or '_'))code=value;}catch{}
                return new(false,code,OutcomeUnknown:(int)response.StatusCode>=500,KnownNotAccepted:(int)response.StatusCode<500);
            }
            using var document=JsonDocument.Parse(text);
            if(document.RootElement.ValueKind!=JsonValueKind.Object||document.RootElement.TryGetProperty("workspaceId",out var id)&&id.GetGuid()!=workspaceId)throw new JsonException("Transfer target mismatch.");
            return new(true,"Original transfer response verified.",text);
        }
        catch(OperationCanceledException)when(token.IsCancellationRequested){throw;}
        catch{return new(false,"Transfer outcome is unknown. Read the original receipt before another acceptance attempt.",OutcomeUnknown:true);}
    }
}
