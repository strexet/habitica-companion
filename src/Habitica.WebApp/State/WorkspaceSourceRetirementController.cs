using Habitica.Application.Workspace;
namespace Habitica.WebApp.State;

/// <summary>Account-scoped original cleanup identity. Unknown dispatch never enables another DELETE.</summary>
public sealed class WorkspaceSourceRetirementController : IDisposable
{
    private readonly IAppSessionController _session;private readonly IWorkspaceCloudClient _cloud;
    private string? _account;private Guid? _workspace;private long _generation;private bool _disposed;
    private readonly HashSet<int> _pages=[];
    public WorkspaceSourceRetirementController(IAppSessionController session,IWorkspaceCloudClient cloud){_session=session;_cloud=cloud;_account=Account;_workspace=cloud.WorkspaceId;session.Changed+=SessionChanged;}
    private IWorkspaceSourceRetirementApi? Api=>_cloud as IWorkspaceSourceRetirementApi;
    private string? Account=>_session.State.IsAuthenticated&&Guid.TryParse(_session.State.UserId,out var id)?id.ToString("D"):null;
    public event Action? Changed;
    public WorkspaceSourceRetirementRequest? Request {get;private set;}
    public WorkspaceSourceRetirementView? View {get;private set;}
    public Guid? OriginalCommandId {get;private set;}
    public bool IsBusy {get;private set;}
    public bool Unknown {get;private set;}
    public string? Message {get;private set;}
    public int ReviewOffset {get;private set;}
    public bool IrreversibleApproved {get;private set;}
    public bool Available=>Account is not null&&_cloud.IsAvailable&&_cloud.WorkspaceId is not null&&Api is not null;
    public bool PageReviewed=>_pages.Contains(ReviewOffset);
    public bool CanAccept=>!IsBusy&&!Unknown&&View is {State:"ready",Expired:false,VerifiedAtUtc:{} verified} view&&verified.AddMinutes(5)>DateTimeOffset.UtcNow&&view.ExpiresAtUtc>DateTimeOffset.UtcNow
        &&IrreversibleApproved&&Enumerable.Range(0,(view.Targets.Count+9)/10).All(page=>_pages.Contains(page*10));
    public void AcknowledgePage(bool value){CheckScope();if(IsBusy||View?.State!="ready")return;if(value)_pages.Add(ReviewOffset);else _pages.Remove(ReviewOffset);Changed?.Invoke();}
    public void ApproveIrreversible(bool value){CheckScope();if(IsBusy||View?.State!="ready")return;IrreversibleApproved=value;Changed?.Invoke();}
    public void ReviewPage(int offset){if(IsBusy||View is null||offset<0||offset>=View.Targets.Count||offset%10!=0)return;ReviewOffset=offset;Changed?.Invoke();}
    public Task PrepareAsync(Guid conversionId,long revision,CancellationToken token=default)=>RunAsync(async generation=>
    {
        if(Request is not null||OriginalCommandId is not null){Message="Recover the original review before preparing another cleanup.";return;}
        Request=new(Guid.NewGuid(),conversionId,revision);await PrepareOriginal(generation,token);
    },token);
    public Task RetryPreparationAsync(CancellationToken token=default)=>RunAsync(generation=>PrepareOriginal(generation,token),token);
    private async Task PrepareOriginal(long generation,CancellationToken token){if(Request is null)return;Apply(await Api!.PrepareSourceRetirementAsync(Request,token),generation);}
    public Task VerifyAsync(CancellationToken token=default)=>RunAsync(async generation=>
    {
        if(View is not {State:"verifying"} view||Unknown)return;var deadline=DateTimeOffset.UtcNow.AddSeconds(50);
        for(var step=0;step<102&&DateTimeOffset.UtcNow<deadline;step++)
        {
            var before=view.CompletedSteps;var result=await Api!.VerifySourceRetirementAsync(view.CommandId,token);if(!Current(generation))return;Apply(result,generation);
            if(!result.Succeeded||View is not {} next||next.State!="verifying"||next.CompletedSteps<=before)return;view=next;
        }
        Message="Verification paused at its time limit. Continue the same original review; no mutation has been sent.";
    },token);
    public Task AcceptAsync(CancellationToken token=default)
    {
        CheckScope();if(!CanAccept)return Task.CompletedTask;var approved=View!;
        return RunAsync(async generation=>{Unknown=true;IrreversibleApproved=false;Apply(await Api!.AcceptSourceRetirementAsync(approved,token),generation);},token);
    }
    public Task RecoverAsync(Guid? commandId=null,bool reconcile=false,CancellationToken token=default)=>RunAsync(async generation=>
    {
        var id=OriginalCommandId??commandId;if(id is null||id==Guid.Empty)return;if(OriginalCommandId is {} original&&commandId is {} requested&&original!=requested){Message="Keep the original command ID.";return;}
        OriginalCommandId=id;Apply(await(reconcile?Api!.ReconcileSourceRetirementAsync(id.Value,token):Api!.ReadSourceRetirementAsync(id.Value,token)),generation);
    },token);
    private void Apply(WorkspaceTaskResponse<WorkspaceSourceRetirementView> result,long generation)
    {
        if(!Current(generation))return;Message=result.Message;
        if(!result.Succeeded||result.Value is not {} value){Unknown|=result.OutcomeUnknown;return;}
        if(OriginalCommandId is {} id&&id!=value.CommandId){Unknown=true;Message="Original retirement identity changed; result discarded.";return;}
        OriginalCommandId=value.CommandId;View=value;Unknown=value.OutcomeUnknown||value.State=="nativeConfirmed";
        if(value.State=="confirmed")Message="Source cleanup confirmed. Full source history remains archived; replacement tasks and rewards are unchanged.";
        if(value.State=="ready")Unknown=false;
    }
    public void ClearUnsentReview()
    {
        CheckScope();if(IsBusy||Unknown||View?.State is "executing" or "unknownOutcome" or "nativeConfirmed")return;
        Request=null;View=null;OriginalCommandId=null;ReviewOffset=0;IrreversibleApproved=false;_pages.Clear();Message=null;Changed?.Invoke();
    }
    private void SessionChanged(){CheckScope();Changed?.Invoke();}
    public void CheckScope(){if(_account==Account&&_workspace==_cloud.WorkspaceId)return;_account=Account;_workspace=_cloud.WorkspaceId;_generation++;Request=null;View=null;OriginalCommandId=null;IsBusy=Unknown=IrreversibleApproved=false;ReviewOffset=0;_pages.Clear();Message=null;}
    private bool Current(long generation)=>!_disposed&&generation==_generation&&Account==_account&&_workspace==_cloud.WorkspaceId;
    private async Task RunAsync(Func<long,Task> action,CancellationToken token)
    {
        CheckScope();if(IsBusy||!Available||_disposed)return;var generation=_generation;IsBusy=true;Changed?.Invoke();
        try{await action(generation);}
        catch(OperationCanceledException) when(token.IsCancellationRequested){if(Current(generation))Message="Interrupted. Recover the same original review or command; no automatic retry.";}
        catch{if(Current(generation)){Unknown=true;Message="Retirement outcome unavailable. Keep the original ID and inspect its receipt.";}}
        finally{if(Current(generation)){IsBusy=false;Changed?.Invoke();}}
    }
    public void Dispose(){_disposed=true;_generation++;_session.Changed-=SessionChanged;}
}
