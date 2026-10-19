using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;

namespace Habitica.WebApp.Integration;

public sealed partial class WorkspaceCloudClient : IWorkspaceOrganizationApi
{
    public Task<WorkspaceTaskResponse<WorkspaceOrganizationPreview>> PrepareOrganizationAsync(Guid requestId,WorkspaceOrganizationCommand intent,CancellationToken cancellationToken) => ObserveResponseAsync("workspace-organization-prepare", () => PrepareOrganizationCoreAsync(requestId,intent,cancellationToken), correlationId: requestId);
    private async Task<WorkspaceTaskResponse<WorkspaceOrganizationPreview>> PrepareOrganizationCoreAsync(Guid requestId,WorkspaceOrganizationCommand intent,CancellationToken cancellationToken)
    {
        if(requestId==Guid.Empty||WorkspaceId!=intent.WorkspaceId||!WorkspaceOrganizationCommands.IsValid(intent))return new(false,null,"The organization intent is invalid or the connection changed.");
        var value=WorkspaceTaskApiJson.Element(intent);var args=new Dictionary<string,object?> { ["requestId"]=requestId,["kind"]=value.GetProperty("kind"),["operation"]=value.GetProperty("operation"),["expectedRevision"]=intent.ExpectedRevision,["patch"]=intent.Patch };
        if(intent.Operation!=WorkspaceOrganizationOperation.Create)args["entityId"]=intent.EntityId;
        var result=await CallOrganizationAsync<WorkspaceOrganizationPreview>("prepare_organization_command",args,cancellationToken);
        if(!result.Succeeded||result.Value is not {} preview)return result;
        var command=preview.Command;
        if(command is null||preview.Snapshot is null||!WorkspaceOrganizationCommands.IsValid(command)||preview.PreparedCommandId!=command.Id||preview.CorrelationId==Guid.Empty
            ||preview.ExpiresAtUtc==default||preview.ExpiresAtUtc.Offset!=TimeSpan.Zero||preview.MayAffectHabitica||!ValidSnapshot(command,preview.Snapshot)
            ||WorkspaceOrganizationCommands.Hash(command with { Id=intent.Id,EntityId=intent.Operation==WorkspaceOrganizationOperation.Create?intent.EntityId:command.EntityId })!=WorkspaceOrganizationCommands.Hash(intent)
            ||preview.SavedInCompanion!=(preview.Receipt is not null)||preview.Receipt is {} receipt&&(!ValidReceipt(receipt,command.Id)||receipt.CorrelationId!=preview.CorrelationId||WorkspaceOrganizationCommands.Hash(receipt.Entry.Command)!=WorkspaceOrganizationCommands.Hash(command)||!receipt.Entry.Snapshot.Equivalent(preview.Snapshot)))
            return new(false,null,"The native organization preview did not match the requested intent. Keep the original request ID.",true);
        return result;
    }
    public Task<WorkspaceTaskResponse<WorkspaceOrganizationReceipt>> AcceptOrganizationAsync(WorkspaceOrganizationPreview preview,CancellationToken cancellationToken) => ObserveResponseAsync("workspace-organization-accept", () => AcceptOrganizationCoreAsync(preview,cancellationToken), preview.PreparedCommandId, preview.CorrelationId);
    private async Task<WorkspaceTaskResponse<WorkspaceOrganizationReceipt>> AcceptOrganizationCoreAsync(WorkspaceOrganizationPreview preview,CancellationToken cancellationToken)
    {
        if(preview.Command is null||WorkspaceId!=preview.Command.WorkspaceId||preview.PreparedCommandId!=preview.Command.Id||preview.MayAffectHabitica
            ||preview.CorrelationId==Guid.Empty||!WorkspaceOrganizationCommands.IsValid(preview.Command)||!ValidSnapshot(preview.Command,preview.Snapshot))return new(false,null,"Review an exact organization preview first.");
        if(!preview.SavedInCompanion&&(preview.Expired||preview.ExpiresAtUtc<=DateTimeOffset.UtcNow))return new(false,null,"The organization preview expired. Check the original receipt before preparing another action.");
        var result=await CallOrganizationAsync<WorkspaceOrganizationReceipt>("accept_organization_command",new { commandId=preview.PreparedCommandId },cancellationToken,preview.PreparedCommandId);
        if(result.Succeeded&&result.Value is {} receipt&&(receipt.CorrelationId!=preview.CorrelationId||WorkspaceOrganizationCommands.Hash(receipt.Entry.Command)!=WorkspaceOrganizationCommands.Hash(preview.Command)||!receipt.Entry.Snapshot.Equivalent(preview.Snapshot)))
            return new(false,null,"The original organization receipt did not match the reviewed intent.",true);
        return result;
    }
    public Task<WorkspaceTaskResponse<WorkspaceOrganizationReceipt>> ReadOrganizationAsync(Guid commandId,CancellationToken cancellationToken)=>ObserveResponseAsync("workspace-organization-recover", () => CallOrganizationAsync<WorkspaceOrganizationReceipt>("get_organization_command",new { commandId },cancellationToken,commandId), commandId);
    private bool ValidReceipt(WorkspaceOrganizationReceipt receipt,Guid? expectedId)
    {
        var entry=receipt.Entry;var command=entry?.Command;
        return receipt.CommandId==expectedId&&receipt.CorrelationId!=Guid.Empty&&receipt.WorkspaceId==WorkspaceId&&receipt.SavedInCompanion
            &&!receipt.MayAffectHabitica&&receipt.AcceptedEventCount==0&&receipt.ConfirmedEffectCount==0&&!receipt.OutcomeUnknown
            &&command is not null&&WorkspaceOrganizationCommands.IsValid(command)&&command.Id==receipt.CommandId&&command.WorkspaceId==receipt.WorkspaceId
            &&command.EntityId==receipt.EntityId&&command.Kind==receipt.Kind&&command.Operation==receipt.Operation
            &&entry!.AcceptedAtUtc!=default&&entry.AcceptedAtUtc.Offset==TimeSpan.Zero&&entry.PayloadHash==WorkspaceOrganizationCommands.Hash(command)&&ValidSnapshot(command,entry.Snapshot);
    }
    private static bool ValidSnapshot(WorkspaceOrganizationCommand command,WorkspaceOrganizationSnapshot? snapshot)
    {
        if(snapshot is null||snapshot.Id!=command.EntityId||snapshot.Kind!=command.Kind||snapshot.Revision!=command.ExpectedRevision+1
            ||string.IsNullOrWhiteSpace(snapshot.Name)||snapshot.Name.Length>256||snapshot.IsArchived!=(command.Operation==WorkspaceOrganizationOperation.Archive)
            ||snapshot.Kind!=WorkspaceOrganizationKind.Project&&snapshot.CategoryId is not null||snapshot.CategoryId==Guid.Empty
            ||snapshot.Kind!=WorkspaceOrganizationKind.Section&&(snapshot.ProjectId is not null||snapshot.Position!=0)
            ||snapshot.Kind==WorkspaceOrganizationKind.Section&&(snapshot.ProjectId is null||snapshot.ProjectId==Guid.Empty||snapshot.Position<0)
            ||snapshot.Kind!=WorkspaceOrganizationKind.SavedTaskView&&snapshot.Query is not null
            ||snapshot.Kind==WorkspaceOrganizationKind.SavedTaskView&&(snapshot.Query is null||!snapshot.Query.IsValid())
            ||snapshot.Kind!=WorkspaceOrganizationKind.Goal&&snapshot.TaskIds is not null
            ||snapshot.Kind==WorkspaceOrganizationKind.Goal&&(snapshot.TaskIds is null||snapshot.TaskIds.Count>100000||snapshot.TaskIds.Any(id=>id==Guid.Empty)||snapshot.TaskIds.Distinct().Count()!=snapshot.TaskIds.Count))return false;
        if(command.Operation==WorkspaceOrganizationOperation.Create)return WorkspaceOrganizationCommands.Apply(command,null).Equivalent(snapshot);
        var patch=command.Patch;return patch is null||
            (patch.Query is null||patch.Query==snapshot.Query)&&(patch.Name is null||patch.Name==snapshot.Name)&&(patch.CategoryId is null||patch.CategoryId==snapshot.CategoryId)&&(!patch.ClearCategory||snapshot.CategoryId is null)
            &&(patch.Position is null||patch.Position==snapshot.Position)&&(patch.TaskIds is null||snapshot.TaskIds is not null&&patch.TaskIds.SequenceEqual(snapshot.TaskIds));
    }
    private async Task<WorkspaceTaskResponse<T>> CallOrganizationAsync<T>(string tool,object args,CancellationToken cancellationToken,Guid? expectedId=null)
    {
        if(WorkspaceId is null)return new(false,default,"Connect the cloud workspace first.");var generation=_generation;
        try
        {
            using var request=Request(HttpMethod.Post,"organization-api");request.Content=new StringContent(WorkspaceTaskApiJson.Serialize(new { tool,arguments=args }),System.Text.Encoding.UTF8,"application/json");
            using var response=await _http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancellationToken);
            if(!response.IsSuccessStatusCode)return new(false,default,"Organization state is unavailable. Preserve the original request or command ID.",tool!="get_organization_command"&&(int)response.StatusCode>=500);
            var value=WorkspaceTaskApiJson.Deserialize<T>(await BoundedTextAsync(response.Content,cancellationToken));
            if(generation!=_generation)return new(false,default,"The connection changed; organization state was discarded.",tool!="get_organization_command");
            if(value is WorkspaceOrganizationReceipt receipt&&!ValidReceipt(receipt,expectedId))return new(false,default,"No valid original organization receipt is available. Preserve the original command ID.",tool!="get_organization_command");
            return new(true,value,"Native organization state received.");
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested){throw;}
        catch{return new(false,default,"Organization state is unavailable. Preserve the original request or command ID.",tool!="get_organization_command");}
    }
}
