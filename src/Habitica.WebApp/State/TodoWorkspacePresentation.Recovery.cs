using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
namespace Habitica.WebApp.State;

public sealed partial class TodoWorkspacePresentation
{
    private WorkspaceLinkedRecoveryRequest? _linkedRequest;
    public WorkspaceLinkedRecoveryView? LinkedRecovery { get; private set; }
    public string? RecoveryMessage { get; private set; }
    public bool RecoveryAcceptanceAttempted { get; private set; }
    public bool HasLinkedRecoveryIntent => _linkedRequest is not null;
    public Guid? RecoveryRequestId => _linkedRequest?.RequestId;
    public bool CanAcceptLinkedRecovery => !IsBusy && _linkedRequest is not null && !RecoveryAcceptanceAttempted && LinkedRecovery is {State:"ready",Expired:false} review
        && review.ExpiresAtUtc>DateTimeOffset.UtcNow && review.Revision==Revision && !_commands.IsReadOnly;
    private void RequireRecoveryResolved(){if(HasLinkedRecoveryIntent)throw new InvalidOperationException("Resolve the original linked recovery in Task history before another task or organization change.");}
    private void ClearLinkedRecovery(){_linkedRequest=null;LinkedRecovery=null;RecoveryMessage=null;RecoveryAcceptanceAttempted=false;}
    public Task StartLinkedRecoveryAsync(Guid taskId,Guid completionId,CancellationToken token=default)
    {
        if(IsReadOnly||_commands.HasPendingIntent||_commands.LinkPreview is not null)throw new InvalidOperationException("Resolve the original command or reload writable workspace data first.");
        var source=Selection??throw new InvalidOperationException("Select a workspace first.");
        if(source.Source!=TodoWorkspaceSource.Cloud||_cloud is not IWorkspaceLinkedRecoveryApi api)throw new InvalidOperationException("Habitica uncheck requires a connected cloud workspace and the reviewed recovery client.");
        var scope=Account!;var generation=_generation;
        return RunAsync(async()=>{
            ClearLinkedRecovery();
            var eligibility=await api.ReadLinkedRecoveryEligibilityAsync(taskId,completionId,token);
            if(!Current(scope,generation)||Selection!=source)return;
            if(!eligibility.Succeeded||eligibility.Value is not {} available){RecoveryMessage=eligibility.Message;return;}
            if(available.OriginalCommand is {} original){_linkedRequest=new(original.RequestId,taskId,completionId,original.Revision);LinkedRecovery=original;RecoveryAcceptanceAttempted=original.State is "executing" or "unknownOutcome" or "nativeConfirmed";RecoveryMessage="Original recovery found. Keep this identity; read its original receipt before another action.";return;}
            if(!available.Available){RecoveryMessage="Recovery unavailable: "+available.Reason+". No Habitica score was requested.";return;}
            _linkedRequest=new(Guid.NewGuid(),taskId,completionId,available.Revision);
            await PrepareOriginalRecoveryAsync(api,scope,generation,source,token);
        },token);
    }
    private async Task PrepareOriginalRecoveryAsync(IWorkspaceLinkedRecoveryApi api,string scope,long generation,TodoWorkspaceSelection source,CancellationToken token)
    {
        if(_linkedRequest is not {} request)return;
        var result=await api.PrepareLinkedRecoveryAsync(request,token);
        if(!Current(scope,generation)||Selection!=source)return;
        if(!result.Succeeded||result.Value is not {} view){RecoveryMessage=result.Message+" Keep the original request ID; retry that preview to recover its command.";return;}
        if(LinkedRecovery is {} prior&&view.CommandId!=prior.CommandId)throw new InvalidOperationException("Original recovery command identity changed.");
        await AdoptRecoveryAsync(view,source,token);
    }
    public Task RetryLinkedRecoveryPreviewAsync(CancellationToken token=default)
    {
        if(_linkedRequest is null||RecoveryAcceptanceAttempted||_cloud is not IWorkspaceLinkedRecoveryApi api)throw new InvalidOperationException("Only an unsubmitted original preview can be retried.");
        var scope=Account!;var generation=_generation;var source=Selection!;
        return RunAsync(()=>PrepareOriginalRecoveryAsync(api,scope,generation,source,token),token);
    }
    public Task AcceptLinkedRecoveryAsync(CancellationToken token=default)
    {
        if(!CanAcceptLinkedRecovery||_cloud is not IWorkspaceLinkedRecoveryApi api)throw new InvalidOperationException("A fresh exact recovery review is required.");
        var review=LinkedRecovery!;var scope=Account!;var generation=_generation;var source=Selection!;
        return RunAsync(async()=>{
            RecoveryAcceptanceAttempted=true;
            var result=await api.AcceptLinkedRecoveryAsync(review,token);
            if(!Current(scope,generation)||Selection!=source)return;
            if(!result.Succeeded||result.Value is not {} view){RecoveryMessage=result.Message+" Read the original command receipt; do not prepare a replacement.";return;}
            await AdoptRecoveryAsync(view,source,token);
        },token);
    }
    public Task ReadLinkedRecoveryByIdAsync(Guid commandId,CancellationToken token=default)
    {
        if(commandId==Guid.Empty||Selection is not {Source:TodoWorkspaceSource.Cloud} source||_cloud is not IWorkspaceLinkedRecoveryApi api)
            throw new InvalidOperationException("Select the cloud workspace and enter the exact original command ID.");
        if(HasLinkedRecoveryIntent&&LinkedRecovery?.CommandId!=commandId)throw new InvalidOperationException("Keep the current original recovery identity.");
        var scope=Account!;var generation=_generation;
        return RunAsync(async()=>{
            var result=await api.ReadLinkedRecoveryAsync(commandId,token);
            if(!Current(scope,generation)||Selection!=source)return;
            if(!result.Succeeded||result.Value is not {} view){RecoveryMessage=result.Message;return;}
            _linkedRequest??=new(view.RequestId,view.Task.Id,view.Completion.Id,view.Revision);
            await AdoptRecoveryAsync(view,source,token);
        },token);
    }
    public Task ReadLinkedRecoveryReceiptAsync(CancellationToken token=default)
    {
        if(LinkedRecovery is not {} original||_cloud is not IWorkspaceLinkedRecoveryApi api)throw new InvalidOperationException("Retry the original preview first to recover the command ID.");
        var scope=Account!;var generation=_generation;var source=Selection!;
        return RunAsync(async()=>{
            var result=await api.ReadLinkedRecoveryAsync(original.CommandId,token);
            if(!Current(scope,generation)||Selection!=source)return;
            if(!result.Succeeded||result.Value is not {} view){RecoveryMessage=result.Message+" Original identity remains retained.";return;}
            await AdoptRecoveryAsync(view,source,token);
        },token);
    }
    private async Task AdoptRecoveryAsync(WorkspaceLinkedRecoveryView view,TodoWorkspaceSelection source,CancellationToken token)
    {
        if(_linkedRequest is not {} request||view.RequestId!=request.RequestId||view.Task.Id!=request.TaskId||view.Completion.Id!=request.CompletionId||view.WorkspaceId!=source.WorkspaceId
            ||LinkedRecovery is {} prior&&prior.CommandId!=view.CommandId)throw new InvalidOperationException("Receipt does not match the original recovery intent.");
        LinkedRecovery=view;
        if(view.State=="ready")RecoveryAcceptanceAttempted=false;
        if(view.State=="confirmed"&&view.Receipt is not null){_linkedRequest=null;RecoveryMessage="Original Habitica receipt confirmed; causal Companion history saved. Exact resource refunds remain unknown.";await _commands.LoadAsync(true,cancellationToken:token);}
        else if(view.State=="refused"){_linkedRequest=null;RecoveryMessage="Original action refused before dispatch: "+view.Error+". History remains unchanged.";}
        else RecoveryMessage=view.OutcomeUnknown?"Original recovery remains uncertain. Check its original receipt; another score is forbidden.":"Review the exact task, completion, account day and unknown resource changes before unchecking in Habitica.";
    }
    public void CancelUnsubmittedLinkedRecovery()
    {
        if(IsBusy||RecoveryAcceptanceAttempted)throw new InvalidOperationException("An attempted recovery must retain its original receipt identity.");
        ClearLinkedRecovery();Changed?.Invoke();
    }
}
