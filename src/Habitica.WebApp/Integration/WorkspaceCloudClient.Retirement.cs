using System.Text;
using Habitica.Application.Workspace;
using Habitica.Domain.Diagnostics;
using Habitica.Domain.Workspace;
namespace Habitica.WebApp.Integration;

public sealed partial class WorkspaceCloudClient : IWorkspaceSourceRetirementApi
{
    public Task<WorkspaceTaskResponse<WorkspaceSourceRetirementView>> PrepareSourceRetirementAsync(WorkspaceSourceRetirementRequest request,CancellationToken token)=>
        RetirementAsync("prepare_source_retirement",request,view=>view.RequestId==request.RequestId&&view.ConversionId==request.ConversionId&&view.Revision==request.ExpectedRevision,token);
    public Task<WorkspaceTaskResponse<WorkspaceSourceRetirementView>> VerifySourceRetirementAsync(Guid commandId,CancellationToken token)=>
        RetirementAsync("verify_source_retirement",new {commandId},view=>view.CommandId==commandId,token);
    public Task<WorkspaceTaskResponse<WorkspaceSourceRetirementView>> AcceptSourceRetirementAsync(WorkspaceSourceRetirementView reviewed,CancellationToken token)=>
        RetirementAsync("accept_source_retirement",new {commandId=reviewed.CommandId,reviewed.ContextHash,irreversibleApproved=true},view=>view.CommandId==reviewed.CommandId&&view.ContextHash==reviewed.ContextHash,token);
    public Task<WorkspaceTaskResponse<WorkspaceSourceRetirementView>> ReadSourceRetirementAsync(Guid commandId,CancellationToken token)=>
        RetirementAsync("get_source_retirement_command",new {commandId},view=>view.CommandId==commandId,token);
    public Task<WorkspaceTaskResponse<WorkspaceSourceRetirementView>> ReconcileSourceRetirementAsync(Guid commandId,CancellationToken token)=>
        RetirementAsync("reconcile_source_retirement",new {commandId},view=>view.CommandId==commandId,token);
    private Task<WorkspaceTaskResponse<WorkspaceSourceRetirementView>> RetirementAsync(string tool,object args,Func<WorkspaceSourceRetirementView,bool> matches,CancellationToken token)
    {
        async Task<WorkspaceTaskResponse<WorkspaceSourceRetirementView>> Action()
        {
            if(WorkspaceId is null||_token is null)return new(false,null,"Connect the cloud workspace first.");var generation=_generation;
            try
            {
                using var request=Request(HttpMethod.Post,"retirement-api");request.Content=new StringContent(WorkspaceTaskApiJson.Serialize(new {tool,arguments=args}),Encoding.UTF8,"application/json");
                using var response=await _http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);
                if(!response.IsSuccessStatusCode)return new(false,null,"Retirement could not be confirmed. Keep the original ID; inspect its receipt. Changed content, consent, permissions, unresolved work, rate limits or an expired review can block cleanup.",tool=="accept_source_retirement"&&(int)response.StatusCode>=500);
                var view=WorkspaceTaskApiJson.Deserialize<WorkspaceSourceRetirementView>(await BoundedTextAsync(response.Content,token));
                if(generation!=_generation)return new(false,null,"The account changed; retirement data was discarded.",true);
                if(!ValidRetirement(view)||!matches(view))return new(false,null,"Invalid retirement response. Keep the original command ID and inspect its receipt.",true);
                return new(true,view,"Retirement state received. Only a confirmed receipt establishes archived cleanup.");
            }
            catch(OperationCanceledException) when(token.IsCancellationRequested){throw;}
            catch{return new(false,null,"Retirement response unavailable. Keep the original request or command ID; do not send another deletion.",true);}
        }
        return ObserveResponseAsync(tool switch {"get_source_retirement_command"=>"workspace-retirement-read","prepare_source_retirement"=>"workspace-retirement-prepare","verify_source_retirement"=>"workspace-retirement-verify","accept_source_retirement"=>"workspace-retirement-accept",_=>"workspace-retirement-reconcile"},Action);
    }
    private bool ValidRetirement(WorkspaceSourceRetirementView view)
    {
        if(view.WorkspaceId!=WorkspaceId||view.CommandId==Guid.Empty||view.RequestId==Guid.Empty||view.ConversionId==Guid.Empty||view.Revision<1||view.Source is not {Kind:CompanionTaskKind.Todo,IsArchived:false}
            ||view.SourceMapping is not {State:ProjectionState.Linked,IsArchived:false}||view.SourceMapping.TaskId!=view.Source.Id||view.SourceMapping.HabiticaTaskId!=view.HabiticaTaskId
            ||!Guid.TryParse(view.HabiticaTaskId,out var native)||native==Guid.Empty||!Guid.TryParse(view.HabiticaProjectTagId,out var tag)||tag==Guid.Empty||view.Project is not {IsArchived:false,IsRemoved:false}
            ||view.Targets is not {Count:>0 and <=100}||view.Targets.Any(target=>target?.Task is null||target.Mapping is null||target.Task.ProjectId!=view.Project.Id||target.Mapping.TaskId!=target.Task.Id||target.Mapping.State!=ProjectionState.Linked)
            ||view.Targets.Select(target=>target.Task.Id).Distinct().Count()!=view.Targets.Count||view.VerificationSteps!=view.Targets.Count+2||view.CompletedSteps<0||view.CompletedSteps>view.VerificationSteps
            ||view.VerifiedTargets!=Math.Clamp(view.CompletedSteps-1,0,view.Targets.Count)||view.GameScores!=0||view.ExpiresAtUtc==default||view.ExpiresAtUtc.Offset!=TimeSpan.Zero||string.IsNullOrWhiteSpace(view.Disclosure)
            ||!new[]{"verifying","ready","executing","unknownOutcome","nativeConfirmed","refused","confirmed"}.Contains(view.State)||view.OutcomeUnknown!=(view.State is "executing" or "unknownOutcome")
            ||view.SavedInCompanion!=(view.Receipt is null?null:true))return false;
        var plan=new ConversionRetirementPlan(view.WorkspaceId,view.Revision,view.ConversionId,view.Source,view.SourceMapping,view.Project,tag,view.Targets,view.ContextHash);
        if(view.ContextHash!=WorkspaceConversionRetirementPolicy.Hash(plan)||view.State=="ready"&&(view.CompletedSteps!=view.VerificationSteps||view.VerifiedAtUtc is null))return false;
        return view.Receipt is not {} receipt||view.State=="confirmed"&&receipt.Id==view.CommandId&&receipt.ConversionId==view.ConversionId&&receipt.SourceTaskId==view.Source.Id&&receipt.ContextHash==view.ContextHash&&receipt.HabiticaTaskId==view.HabiticaTaskId&&receipt.ReviewedRevision==view.Revision&&receipt.Revision>view.Revision&&Enum.IsDefined(receipt.Outcome)&&receipt.RetiredAtUtc>=receipt.VerifiedAtUtc
            &&(receipt.Outcome==SourceRetirementOutcome.AlreadyAbsent?receipt.NativeAttemptId is null:receipt.NativeAttemptId is {} attempt&&attempt!=Guid.Empty);
    }
}
