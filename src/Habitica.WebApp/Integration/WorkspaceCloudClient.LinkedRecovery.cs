using System.Text;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
namespace Habitica.WebApp.Integration;

public sealed partial class WorkspaceCloudClient : IWorkspaceLinkedRecoveryApi
{
    public Task<WorkspaceTaskResponse<WorkspaceLinkedRecoveryEligibility>> ReadLinkedRecoveryEligibilityAsync(Guid taskId,Guid completionId,CancellationToken token) =>
        LinkedRecoveryAsync<WorkspaceLinkedRecoveryEligibility>("get_linked_task_recovery",new {taskId,completionId},view => view.WorkspaceId==WorkspaceId&&view.Revision>0&&view.TaskId==taskId&&view.CompletionId==completionId&&!string.IsNullOrWhiteSpace(view.Disclosure)
            &&(view.Available ? view.Reason is null&&view.Plan is {} plan&&plan.WorkspaceId==view.WorkspaceId&&plan.ReviewedRevision==view.Revision&&plan.Task.Id==taskId&&plan.Completion.Id==completionId&&ValidLinkedPlan(plan) : !string.IsNullOrWhiteSpace(view.Reason)&&view.Plan is null)
            &&(view.OriginalCommand is null||!view.Available&&view.Reason=="original_recovery_pending"&&ValidLinkedRecovery(view.OriginalCommand)&&view.OriginalCommand.Task.Id==taskId&&view.OriginalCommand.Completion.Id==completionId),token);
    public Task<WorkspaceTaskResponse<WorkspaceLinkedRecoveryView>> PrepareLinkedRecoveryAsync(WorkspaceLinkedRecoveryRequest request,CancellationToken token) =>
        LinkedRecoveryAsync<WorkspaceLinkedRecoveryView>("prepare_linked_task_recovery",request,view=>ValidLinkedRecovery(view)&&view.RequestId==request.RequestId&&view.Task.Id==request.TaskId&&view.Completion.Id==request.CompletionId&&view.Revision==request.ExpectedRevision,token);
    public Task<WorkspaceTaskResponse<WorkspaceLinkedRecoveryView>> AcceptLinkedRecoveryAsync(WorkspaceLinkedRecoveryView reviewed,CancellationToken token)
    {
        if(!ValidLinkedRecovery(reviewed)||reviewed.State!="ready"||reviewed.Expired||reviewed.ExpiresAtUtc<=DateTimeOffset.UtcNow)
            return Task.FromResult(new WorkspaceTaskResponse<WorkspaceLinkedRecoveryView>(false,null,"A fresh exact recovery review is required."));
        return LinkedRecoveryAsync<WorkspaceLinkedRecoveryView>("accept_linked_task_recovery",new {commandId=reviewed.CommandId,reviewed.ContextHash,reviewHash=reviewed.Review.ReviewHash},
            view=>ValidLinkedRecovery(view)&&view.CommandId==reviewed.CommandId&&view.ContextHash==reviewed.ContextHash&&view.Review.ReviewHash==reviewed.Review.ReviewHash,token);
    }
    public Task<WorkspaceTaskResponse<WorkspaceLinkedRecoveryView>> ReadLinkedRecoveryAsync(Guid commandId,CancellationToken token) =>
        LinkedRecoveryAsync<WorkspaceLinkedRecoveryView>("get_linked_task_recovery_command",new {commandId},view=>ValidLinkedRecovery(view)&&view.CommandId==commandId,token);
    private Task<WorkspaceTaskResponse<T>> LinkedRecoveryAsync<T>(string tool,object args,Func<T,bool> matches,CancellationToken token)
    {
        async Task<WorkspaceTaskResponse<T>> Action()
        {
            if(WorkspaceId is null||_token is null)return new(false,default,"Connect the cloud workspace first.");var generation=_generation;
            try
            {
                using var request=Request(HttpMethod.Post,"linked-recovery-api");request.Content=new StringContent(WorkspaceTaskApiJson.Serialize(new {tool,arguments=args}),Encoding.UTF8,"application/json");
                using var response=await _http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);
                if(!response.IsSuccessStatusCode)
                {
                    string? code=null;
                    try
                    {
                        using var error=System.Text.Json.JsonDocument.Parse(await BoundedTextAsync(response.Content,token));
                        if(error.RootElement.TryGetProperty("error",out var value)&&value.ValueKind==System.Text.Json.JsonValueKind.String
                            &&value.GetString() is {} candidate&&System.Text.RegularExpressions.Regex.IsMatch(candidate,"^[a-z][a-z0-9_]{0,95}$"))code=candidate;
                    }
                    catch(System.Text.Json.JsonException){ }
                    return new(false,default,(code is null?"Recovery was not confirmed.":"Recovery unavailable: "+code+".")+" Keep the original request or command ID; inspect its receipt. Changed state, consent, permission, pending effects or expired proof can block recovery.",tool=="accept_linked_task_recovery"&&(int)response.StatusCode>=500);
                }
                var view=WorkspaceTaskApiJson.Deserialize<T>(await BoundedTextAsync(response.Content,token));
                if(generation!=_generation)return new(false,default,"The account changed; recovery data was discarded.",true);
                if(view is null||!matches(view))return new(false,default,"Invalid recovery response. Keep the original ID; no successful score or refund was inferred.",true);
                return new(true,view,"Recovery state received. Only the original confirmed receipt establishes causal Companion recovery.");
            }
            catch(OperationCanceledException) when(token.IsCancellationRequested){throw;}
            catch{return new(false,default,"Recovery response unavailable. Keep the original ID; never send a replacement score.",true);}
        }
        return ObserveResponseAsync(tool.StartsWith("get_",StringComparison.Ordinal)?"workspace-linked-recovery-read":"workspace-linked-recovery-write",Action);
    }
    private static LinkedTaskRecovery ContextRow(WorkspaceLinkedRecoveryPlan plan) => new(Guid.Empty,plan.ReviewedRevision+1,plan.Task.Id,plan.Completion.Id,plan.SourceAttemptId,Guid.Empty,Guid.Parse(plan.Mapping.HabiticaTaskId!),plan.AccountId,
        plan.ReviewedRevision,plan.Task.Revision,plan.Mapping.Id,plan.Mapping.Revision,plan.Mapping.Fingerprint!,plan.ContextHash,new string('a',64),plan.SourceContext,DateTimeOffset.UtcNow);
    private static bool ValidLinkedPlan(WorkspaceLinkedRecoveryPlan plan)
    {
        if(plan.WorkspaceId==Guid.Empty||plan.ReviewedRevision<1||plan.Task is not {IsArchived:false} task||task.Kind is not (CompanionTaskKind.Todo or CompanionTaskKind.Daily)
            ||task.Kind==CompanionTaskKind.Todo&&task.State!=CompanionTaskState.Completed||task.Kind==CompanionTaskKind.Daily&&task.State!=CompanionTaskState.Active||task.Revision<1||task.Revision>plan.ReviewedRevision
            ||plan.Completion is not {IsArchived:false} completion||completion.TaskId!=task.Id||completion.Revision>plan.ReviewedRevision
            ||plan.Mapping is not {IsArchived:false,State:ProjectionState.Linked} mapping||mapping.TaskId!=task.Id||mapping.Revision<1||mapping.Revision>plan.ReviewedRevision||mapping.Id==Guid.Empty
            ||!Guid.TryParse(mapping.HabiticaTaskId,out var native)||native==Guid.Empty||!WorkspaceLinkedRecovery.ValidHash(mapping.Fingerprint)
            ||plan.SourceContext is not {} source||!source.IsValid()||source.WorkspaceId!=plan.WorkspaceId||source.TaskId!=task.Id||source.CompletionId!=completion.Id||source.CommandId!=completion.CommandId||source.TaskKind!=task.Kind
            ||plan.SourceAttemptId==Guid.Empty||plan.AccountId==Guid.Empty||!WorkspaceLinkedRecovery.ValidHash(plan.ContextHash))return false;
        return WorkspaceLinkedRecovery.ContextHash(plan.WorkspaceId,ContextRow(plan),completion)==plan.ContextHash;
    }
    private bool ValidLinkedRecovery(WorkspaceLinkedRecoveryView view)
    {
        if(view.WorkspaceId!=WorkspaceId||view.CommandId==Guid.Empty||view.RequestId==Guid.Empty||view.Task is null||view.Mapping is null||view.Completion is null||view.SourceContext is null)return false;
        var plan=new WorkspaceLinkedRecoveryPlan(view.WorkspaceId,view.Revision,view.Task,view.Mapping,view.Completion,view.SourceContext,view.SourceAttemptId,view.AccountId,view.ContextHash);
        if(!ValidLinkedPlan(plan)||view.CommandId==view.SourceAttemptId||view.CommandId==view.SourceContext.CommandId||view.Review is not {} review||review.Contract!="own-task-recovery-v1"||review.ContextHash!=view.ContextHash||review.TaskId.ToString("D")!=view.Mapping.HabiticaTaskId
            ||review.TaskKind!=view.Task.Kind||review.Fingerprint!=view.Mapping.Fingerprint||review.OccurrenceDate!=view.SourceContext.OccurrenceDate||review.AccountDay==default||!WorkspaceLinkedRecovery.ValidHash(review.ReviewHash)
            ||review.ResourceChanges is not {Hp:"unknown",Mp:"unknown",Gp:"unknown",Xp:"unknown",Streak:"unknown"}||view.ExpiresAtUtc==default||view.ExpiresAtUtc.Offset!=TimeSpan.Zero||string.IsNullOrWhiteSpace(view.Disclosure)
            ||view.State is not ("ready" or "executing" or "unknownOutcome" or "nativeConfirmed" or "refused" or "confirmed")||view.OutcomeUnknown!=(view.State is "executing" or "unknownOutcome" or "nativeConfirmed")
            ||view.SavedInCompanion!=(view.Receipt is null?null:true)||view.State=="confirmed"&&view.Receipt is null)return false;
        if(view.Receipt is not {} receipt)return true;
        var expected=ContextRow(plan) with {Id=view.CommandId,RecoveryAttemptId=view.CommandId,UpstreamReviewHash=review.ReviewHash,RecoveredAtUtc=receipt.RecoveredAtUtc};
        return view.State=="confirmed"&&receipt==expected&&receipt.RecoveredAtUtc>=view.Completion.CompletedAtUtc&&receipt.RecoveredAtUtc.Offset==TimeSpan.Zero;
    }
}
