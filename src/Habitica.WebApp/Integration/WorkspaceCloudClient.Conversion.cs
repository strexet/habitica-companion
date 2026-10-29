using System.Text;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;

namespace Habitica.WebApp.Integration;

public sealed partial class WorkspaceCloudClient : IWorkspaceConversionApi
{
    public Task<WorkspaceTaskResponse<WorkspaceConversionStatus>> ReadConversionAsync(Guid sourceTaskId,CancellationToken token)=>ObserveResponseAsync("workspace-conversion-read",async()=>
    {
        var result=await CallConversionAsync<WorkspaceConversionStatus>("get_checklist_conversion",new {sourceTaskId},token);
        if(result.Value is not {} status)return result;
        if(sourceTaskId==Guid.Empty||status.Provenance!="companion-canonical"||status.WorkspaceId!=WorkspaceId||status.Revision<1||status.Source?.Id!=sourceTaskId||status.Source.Kind!=CompanionTaskKind.Todo||status.Conversion is null&&status.Source.HabiticaDetails?.Checklist is not {Count:>0 and <=100}
            ||status.NativeVerification!="notFreshlyVerified"||status.RetirementReady||status.Targets is null||status.Targets.Count>100||status.Disclosure is not {Length:>0 and <=4096}
            ||(status.Conversion is null?status.Targets.Count!=0||status.CanonicalComplete||status.StoredCorrespondenceComplete:
                status.Conversion.SourceTaskId!=sourceTaskId||!ValidConversion(status.Conversion)||status.Targets.Count!=status.Conversion.Items.Count
                ||!status.Targets.Select(target=>target.Binding.SourceItemId).SequenceEqual(status.Conversion.Items.Select(item=>item.SourceItemId))
                ||status.SourceUnchanged!=(status.Source.Revision==status.Conversion.SourceRevision))
            ||status.Targets.Any(target=>!ValidBinding(target.Binding,target.Task)||target.PendingCommandIds is null||target.PendingCommandIds.Any(id=>id==Guid.Empty)||target.PendingCommandIds.Distinct().Count()!=target.PendingCommandIds.Count
                ||target.StoredCorrespondence&&(!target.PlacementComplete||target.Mapping is not {State:ProjectionState.Linked,HabiticaTaskId:not null,Fingerprint:not null}||target.PendingCommandIds.Count!=0))
            ||status.Conversion is {} selected&&status.Targets.Any(target=>!Same(target.Binding,selected.Items.Single(item=>item.SourceItemId==target.Binding.SourceItemId))
                ||target.PlacementComplete!=(target.Task is {} task&&task.ProjectId==selected.ProjectId&&(target.Binding.DesiredState==ChecklistConversionState.Active?task.State==CompanionTaskState.Active&&!task.IsArchived:task.State==CompanionTaskState.Completed&&task.IsArchived)))
            ||status.CanonicalComplete!=(status.Conversion is not null&&status.SourceUnchanged&&status.Targets.All(target=>target.PlacementComplete))
            ||status.StoredCorrespondenceComplete&&(status.Conversion is null||status.Targets.Any(target=>!target.StoredCorrespondence)||status.ProjectTag?.MembershipState is not ("confirmed" or "retained")))return new(false,null,"Conversion status could not be validated. No native presence or retirement was inferred.");
        return result;
    });
    public Task<WorkspaceTaskResponse<WorkspaceConversionPreview>> PrepareConversionAsync(WorkspaceConversionRequest request,CancellationToken token)=>ObserveResponseAsync("workspace-conversion-prepare",async()=>
    {
        if(request.RequestId==Guid.Empty||request.SourceTaskId==Guid.Empty||request.Items is null)return new WorkspaceTaskResponse<WorkspaceConversionPreview>(false,null,"Choose an exact source and a complete mapping first.");
        var arguments=new Dictionary<string,object?> { ["requestId"]=request.RequestId,["sourceTaskId"]=request.SourceTaskId,["sourceRevision"]=request.SourceRevision,["expectedRevision"]=request.ExpectedRevision,["items"]=request.Items };
        if(request.ProjectId is not null)arguments["projectId"]=request.ProjectId;if(request.ProjectName is not null)arguments["projectName"]=request.ProjectName;
        var result=await CallConversionAsync<WorkspaceConversionPreview>("prepare_checklist_conversion",arguments,token);if(result.Value is not {} preview)return result;
        if(!ValidPreview(preview)||preview.Command.SourceTaskId!=request.SourceTaskId||preview.Command.SourceRevision!=request.SourceRevision||preview.Command.ExpectedRevision!=request.ExpectedRevision
            ||request.ExpectedConversionId is {} expected&&preview.Command.ConversionId!=expected||request.ProjectId is {} project&&preview.Command.ProjectId!=project
            ||request.ProjectName is not null&&preview.Command.ProjectName!=request.ProjectName||!MatchesItems(preview.Command.Items,request.Items))return new(false,null,"The conversion preview did not match the original request. Preserve the request ID.",true);
        return result;
    },correlationId:request.RequestId);
    public Task<WorkspaceTaskResponse<WorkspaceConversionReceipt>> AcceptConversionAsync(WorkspaceConversionPreview preview,CancellationToken token)=>ObserveResponseAsync("workspace-conversion-accept",async()=>
    {
        if(!ValidPreview(preview))return new WorkspaceTaskResponse<WorkspaceConversionReceipt>(false,null,"Review an exact conversion preview first.");
        // The server checks an original accepted receipt before expiry; retries keep the original identity.
        var result=await CallConversionAsync<WorkspaceConversionReceipt>("accept_checklist_conversion",new {commandId=preview.PreparedCommandId},token);
        if(result.Value is {} receipt&&(!ValidReceipt(receipt,preview.PreparedCommandId)||receipt.CorrelationId!=preview.CorrelationId||WorkspaceChecklistConversions.Hash(receipt.Receipt!.Command)!=WorkspaceChecklistConversions.Hash(preview.Command)))return new(false,null,"The original conversion receipt did not match. Keep the original command ID.",true);
        return result;
    },preview.PreparedCommandId,preview.CorrelationId);
    public Task<WorkspaceTaskResponse<WorkspaceConversionReceipt>> ReadConversionCommandAsync(Guid commandId,CancellationToken token)=>ObserveResponseAsync("workspace-conversion-recover",async()=>
    {
        var result=await CallConversionAsync<WorkspaceConversionReceipt>("get_checklist_conversion_command",new {commandId},token);
        return result.Value is {} receipt&&!ValidReceipt(receipt,commandId)?new(false,null,"No confirmed original conversion acceptance is available. Keep the original reference.",true):result;
    },commandId);
    private bool ValidPreview(WorkspaceConversionPreview preview)=>preview.PreparedCommandId!=Guid.Empty&&preview.CorrelationId!=Guid.Empty
        &&preview.Command is {} command&&WorkspaceChecklistConversions.ValidCommand(command)&&command.WorkspaceId==WorkspaceId&&command.Id==preview.PreparedCommandId
        &&preview.ExpiresAtUtc!=default&&preview.ExpiresAtUtc.Offset==TimeSpan.Zero&&preview.GameScores==0&&preview.NewCaptureCount is >=0 and <=10
        &&preview.Source?.Id==command.SourceTaskId&&preview.Source.Kind==CompanionTaskKind.Todo&&(preview.SavedInCompanion||preview.Source.Revision==command.SourceRevision)
        &&preview.Conversion is {} conversion&&ValidConversion(conversion)&&conversion.Id==command.ConversionId&&conversion.SourceTaskId==command.SourceTaskId&&conversion.SourceRevision==command.SourceRevision&&conversion.ProjectId==command.ProjectId
        &&preview.Targets is not null&&preview.Targets.Count==command.Items.Count&&preview.Targets.Select(target=>target.Binding.SourceItemId).SequenceEqual(command.Items.Select(item=>item.SourceItemId))
        &&preview.Targets.All(target=>ValidBinding(target.Binding,target.Task)&&Same(target.Binding,conversion.Items.Single(item=>item.SourceItemId==target.Binding.SourceItemId)))
        &&(preview.SavedInCompanion?conversion.Receipts.Any(receipt=>WorkspaceChecklistConversions.Hash(receipt.Command)==WorkspaceChecklistConversions.Hash(command)):WorkspaceChecklistConversions.Hash(conversion.Receipts[^1].Command)==WorkspaceChecklistConversions.Hash(command))
        &&preview.NewCaptureCount==(preview.SavedInCompanion?0:command.Items.Count(item=>item.Mode==ChecklistConversionMode.Create&&!conversion.Receipts.SkipLast(1).Any(receipt=>receipt.Command.Items.Any(prior=>prior.SourceItemId==item.SourceItemId&&prior.TargetTaskId is not null))))
        &&(preview.SavedInCompanion||Same(preview.Source,conversion.SourceSnapshot))&&preview.TagProjection is not null&&preview.TagProjection.IsValid()
        &&preview.SavedInCompanion==(preview.Receipt is not null)&&(preview.Receipt is null||ValidReceipt(preview.Receipt,command.Id));
    private bool ValidReceipt(WorkspaceConversionReceipt receipt,Guid id)=>id!=Guid.Empty&&receipt.CommandId==id&&receipt.SavedInCompanion==true&&receipt.OutcomeUnknown==false&&receipt.GameScores==0
        &&receipt.CorrelationId is {} correlation&&correlation!=Guid.Empty&&receipt.Receipt is {} original&&WorkspaceChecklistConversions.ValidCommand(original.Command)
        &&original.Command.Id==id&&original.Command.WorkspaceId==WorkspaceId&&original.Command.ConversionId==receipt.ConversionId&&original.Command.ProjectId==receipt.ProjectId&&original.Command.SourceTaskId==receipt.SourceTaskId
        &&original.PayloadHash==WorkspaceChecklistConversions.Hash(original.Command)&&original.AcceptedAtUtc!=default&&original.AcceptedAtUtc.Offset==TimeSpan.Zero&&original.ResultRevision>original.Command.ExpectedRevision;
    private bool ValidConversion(WorkspaceChecklistConversion conversion)=>conversion.Items is not null&&conversion.SourceSnapshot is not null&&conversion.Receipts is {Count:>0}
        &&WorkspaceId is {} workspace&&WorkspaceChecklistConversions.IsValid(new WorkspaceDocument {Id=workspace,Revision=conversion.Revision,
            Tasks=new[]{conversion.SourceSnapshot}.Concat(conversion.Items.Where(item=>item?.OriginalTarget is not null).Select(item=>item.OriginalTarget!)).ToArray(),
            Projects=[new(conversion.ProjectId,conversion.Revision,"Conversion destination")],ChecklistConversions=[conversion]});
    private static bool Same<T>(T left,T right)=>WorkspaceTaskApiJson.Serialize(left)==WorkspaceTaskApiJson.Serialize(right);
    private static bool ValidBinding(ChecklistConversionBinding binding,CompanionTask? task)=>binding.SourceItemId!=Guid.Empty&&Enum.IsDefined(binding.DesiredState)&&binding.TargetTaskId!=Guid.Empty
        &&binding.HistoricalTimeUncertain==(binding.DesiredState==ChecklistConversionState.CompletedArchived)
        &&(binding.TargetTaskId is null?task is null:task is {Kind:CompanionTaskKind.Todo}&&task.Id==binding.TargetTaskId&&binding.OriginalTarget?.Id==task.Id);
    private static bool MatchesItems(IReadOnlyList<ChecklistConversionItem> actual,IReadOnlyList<WorkspaceConversionItemInput> expected)=>actual.Count==expected.Count&&actual.Zip(expected).All(pair=>
        pair.First.SourceItemId==pair.Second.SourceItemId&&pair.First.Mode==pair.Second.Mode&&pair.First.DesiredState==pair.Second.DesiredState
        &&(pair.Second.Mode==ChecklistConversionMode.Create?pair.First.TargetTaskId is not null:pair.First.TargetTaskId==pair.Second.TargetTaskId)
        &&pair.First.Notes==pair.Second.Notes&&pair.First.GameDetails?.Difficulty==pair.Second.GameDetails?.Difficulty&&pair.First.GameDetails?.Attribute==pair.Second.GameDetails?.Attribute);
    private async Task<WorkspaceTaskResponse<T>> CallConversionAsync<T>(string tool,object arguments,CancellationToken token)
    {
        if(WorkspaceId is null||_token is null)return new(false,default,"Connect the cloud workspace before converting a checklist.");var generation=_generation;
        try
        {
            using var request=Request(HttpMethod.Post,"conversion-api");request.Content=new StringContent(WorkspaceTaskApiJson.Serialize(new {tool,arguments}),Encoding.UTF8,"application/json");
            using var response=await _http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);
            if(!response.IsSuccessStatusCode)return new(false,default,(int)response.StatusCode==403?"Task and organization permission or workspace consent is missing. Use the normal connection controls.":"Conversion state changed or is unavailable. Preserve the original request/command ID.",tool is "prepare_checklist_conversion" or "accept_checklist_conversion"&&(int)response.StatusCode>=500);
            var value=WorkspaceTaskApiJson.Deserialize<T>(await BoundedTextAsync(response.Content,token));return generation==_generation?new(true,value,"Conversion state received. Canonical save, native correspondence and rewards remain separate."):new(false,default,"The account changed; conversion state was discarded.",true);
        }
        catch(OperationCanceledException) when(token.IsCancellationRequested){throw;}
        catch{return new(false,default,"Conversion state is unavailable. Recover the original request or receipt.",tool is "prepare_checklist_conversion" or "accept_checklist_conversion");}
    }
}
