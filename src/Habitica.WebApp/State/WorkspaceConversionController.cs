using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;

namespace Habitica.WebApp.State;

public sealed class ChecklistConversionDraft(Guid sourceItemId)
{
    public Guid SourceItemId {get;}=sourceItemId;
    public ChecklistConversionMode? Mode {get;set;}
    public ChecklistConversionState? DesiredState {get;set;}
    public Guid? TargetTaskId {get;set;}
    public string Notes {get;set;}="";
    public bool Bound {get;set;}
}

/// <summary>Account-scoped, memory-only original request/review ownership survives route changes.</summary>
public sealed class WorkspaceConversionController : IDisposable
{
    private readonly IAppSessionController _session;private readonly IWorkspaceCloudClient _cloud;
    private string? _account;private Guid? _workspace;private long _generation;private bool _disposed;
    private readonly List<ChecklistConversionDraft> _drafts=[];private readonly HashSet<int> _reviewedPages=[];
    private Guid _reviewId;private bool _tagsReviewed,_approved;
    public WorkspaceConversionController(IAppSessionController session,IWorkspaceCloudClient cloud){_session=session;_cloud=cloud;_account=Account;_workspace=cloud.WorkspaceId;session.Changed+=SessionChanged;}
    private IWorkspaceConversionApi? Api=>_cloud as IWorkspaceConversionApi;
    private string? Account=>_session.State.IsAuthenticated&&Guid.TryParse(_session.State.UserId,out var user)?user.ToString("D"):null;
    public event Action? Changed;
    public bool Available=>Account is not null&&_cloud.IsAvailable&&_cloud.WorkspaceId is not null&&Api is not null;
    public Guid? SourceTaskId {get;private set;}
    public WorkspaceConversionStatus? Status {get;private set;}
    public WorkspaceConversionPreview? Preview {get;private set;}
    public WorkspaceConversionReceipt? LastReceipt {get;private set;}
    public WorkspaceConversionRequest? Request {get;private set;}
    public IReadOnlyList<ChecklistConversionDraft> Drafts=>_drafts;
    public bool IsBusy {get;private set;}
    public bool AcceptanceUnknown {get;private set;}
    public bool PreparationRejected {get;private set;}
    public bool HasPendingIntent=>Request is not null||Preview is not null;
    public string? Message {get;private set;}
    public Guid? ProjectId {get;set;}
    public string ProjectName {get;set;}="";
    public double? Difficulty {get;set;}
    public HabiticaTaskAttribute? Attribute {get;set;}
    public int ReviewOffset {get;private set;}
    public const int PageSize=10;
    public bool CanConfirm=>Preview is {} preview&&!IsBusy&&_reviewId==preview.PreparedCommandId
        &&(AcceptanceUnknown&&_approved||!preview.Expired&&preview.ExpiresAtUtc>DateTimeOffset.UtcNow&&_tagsReviewed
            &&Enumerable.Range(0,(preview.Targets.Count+PageSize-1)/PageSize).All(page=>_reviewedPages.Contains(page*PageSize)));
    public bool PageReviewed=>_reviewedPages.Contains(ReviewOffset);
    public void AcknowledgePage(bool value){if(IsBusy||Preview is null)return;if(value)_reviewedPages.Add(ReviewOffset);else _reviewedPages.Remove(ReviewOffset);Changed?.Invoke();}
    public void TagsReviewed(Guid id,bool value){if(Preview?.PreparedCommandId!=id)return;_tagsReviewed=value;Changed?.Invoke();}
    public void ReviewPage(int offset){if(IsBusy||Preview is null||offset<0||offset>=Preview.Targets.Count||offset%PageSize!=0)return;ReviewOffset=offset;Changed?.Invoke();}
    public void ClassifyUnassignedActive(){if(IsBusy||HasPendingIntent)return;foreach(var draft in _drafts.Where(row=>!row.Bound&&row.DesiredState is null))draft.DesiredState=ChecklistConversionState.Active;Changed?.Invoke();}
    public void DeferUnassigned(){if(IsBusy||HasPendingIntent)return;foreach(var draft in _drafts.Where(row=>!row.Bound&&row.Mode is null)){draft.Mode=ChecklistConversionMode.Defer;draft.DesiredState??=ChecklistConversionState.Active;}Changed?.Invoke();}
    public Task LoadAsync(Guid sourceTaskId,CancellationToken token=default)=>RunAsync(async generation=>
    {
        if(HasPendingIntent&&SourceTaskId!=sourceTaskId){Message="Resolve the original conversion review before choosing another source.";return;}
        if(sourceTaskId==Guid.Empty){Message="Choose an exact source Todo.";return;}
        var result=await Api!.ReadConversionAsync(sourceTaskId,token);if(!Current(generation))return;Message=result.Message;
        if(!result.Succeeded||result.Value is not {} status)return;SourceTaskId=sourceTaskId;Status=status;
        if(!HasPendingIntent)ResetDrafts(status);
    },token);
    private void ResetDrafts(WorkspaceConversionStatus status)
    {
        _drafts.Clear();var conversion=status.Conversion;
        foreach(var item in status.Source.HabiticaDetails?.Checklist??[])
        {
            var binding=conversion?.Items.FirstOrDefault(row=>row.SourceItemId==item.Id);
            _drafts.Add(new(item.Id){Mode=binding?.TargetTaskId is not null?ChecklistConversionMode.Reuse:binding is not null?ChecklistConversionMode.Defer:null,
                DesiredState=binding?.DesiredState,TargetTaskId=binding?.TargetTaskId,Bound=binding?.TargetTaskId is not null});
        }
        ProjectId=conversion?.ProjectId;ProjectName=status.ProjectTag?.Name??status.Source.Text;Difficulty=null;Attribute=null;
    }
    public Task PrepareAsync(CancellationToken token=default)=>RunAsync(async generation=>
    {
        if(HasPendingIntent){Message="Recover the original request or receipt before another conversion.";return;}
        if(Status is not {} status||SourceTaskId!=status.Source.Id){Message="Read the current source before preparing a conversion.";return;}
        if(!status.SourceUnchanged){Message="The source changed after conversion began. Review the current and retained originals; existing targets remain bound and source amendment requires separate support.";return;}
        var rows=new List<WorkspaceConversionItemInput>();
        foreach(var draft in _drafts)
        {
            if(draft.Mode is not {} mode||draft.DesiredState is not {} desired)throw new DraftException("Choose an explicit action and lifecycle for every checklist item. Checkmarks do not choose completion.");
            if(mode==ChecklistConversionMode.Create&&(desired!=ChecklistConversionState.Active||Difficulty is null||Attribute is null))throw new DraftException("New captures require Active plus explicit game difficulty and attribute. Historical work must reuse a completed task or stay deferred.");
            if(mode==ChecklistConversionMode.Reuse&&draft.TargetTaskId is null)throw new DraftException("Choose an exact existing Todo for every reuse action.");
            rows.Add(new(draft.SourceItemId,mode,desired,mode==ChecklistConversionMode.Reuse?draft.TargetTaskId:null,mode==ChecklistConversionMode.Create&&draft.Notes.Length>0?draft.Notes:null,mode==ChecklistConversionMode.Create?new(Difficulty,Attribute):null));
        }
        if(rows.Count(row=>row.Mode==ChecklistConversionMode.Create)>10)throw new DraftException("Capture at most ten new active tasks per review. Defer remaining items and resume the same conversion afterward.");
        Request=new(Guid.NewGuid(),status.Source.Id,status.Source.Revision,status.Revision,rows,ProjectId,ProjectId is null?ProjectName:null,status.Conversion?.Id);LastReceipt=null;PreparationRejected=false;
        await PrepareOriginalAsync(generation,token);
    },token);
    public Task RetryPreparationAsync(CancellationToken token=default)=>RunAsync(generation=>PrepareOriginalAsync(generation,token),token);
    private async Task PrepareOriginalAsync(long generation,CancellationToken token)
    {
        if(Request is null)return;var result=await Api!.PrepareConversionAsync(Request,token);if(!Current(generation))return;Message=result.Message;
        PreparationRejected=!result.Succeeded&&!result.OutcomeUnknown;
        if(result.Succeeded&&result.Value is {} preview)
        {
            if(_reviewId!=preview.PreparedCommandId){_reviewId=preview.PreparedCommandId;_reviewedPages.Clear();ReviewOffset=0;_tagsReviewed=!preview.TagProjection.RequestsMetadataChanges;_approved=false;}
            Preview=preview;PreparationRejected=false;
            if(preview.SavedInCompanion&&preview.Receipt is {} receipt)await SavedAsync(receipt,generation,token);
        }
    }
    public Task ConfirmAsync(CancellationToken token=default)=>RunAsync(async generation=>
    {
        // RunAsync sets IsBusy before the action; evaluate the same complete review without the busy guard.
        if(Preview is not {} preview||!ReviewReady())throw new DraftException("Review every source/target and tag page before confirming.");
        var wasUnknown=AcceptanceUnknown;_approved=true;AcceptanceUnknown=true;
        var result=await Api!.AcceptConversionAsync(preview,token);if(!Current(generation))return;Message=result.Message;
        if(result.Succeeded&&result.Value is {} receipt)await SavedAsync(receipt,generation,token);
        else if(!result.OutcomeUnknown&&!wasUnknown){AcceptanceUnknown=false;PreparationRejected=true;}
    },token);
    private bool ReviewReady()=>Preview is {} preview&&_reviewId==preview.PreparedCommandId&&(AcceptanceUnknown&&_approved
        ||!preview.Expired&&preview.ExpiresAtUtc>DateTimeOffset.UtcNow&&_tagsReviewed&&Enumerable.Range(0,(preview.Targets.Count+PageSize-1)/PageSize).All(page=>_reviewedPages.Contains(page*PageSize)));
    public Task RecoverAsync(CancellationToken token=default)=>RunAsync(async generation=>
    {
        if(Preview is not {} preview)return;var result=await Api!.ReadConversionCommandAsync(preview.PreparedCommandId,token);if(!Current(generation))return;Message=result.Message;
        if(result.Succeeded&&result.Value is {} receipt)await SavedAsync(receipt,generation,token);
    },token);
    private async Task SavedAsync(WorkspaceConversionReceipt receipt,long generation,CancellationToken token)
    {
        LastReceipt=receipt;AcceptanceUnknown=false;PreparationRejected=false;Request=null;Preview=null;_approved=false;_reviewedPages.Clear();_reviewId=Guid.Empty;ReviewOffset=0;Status=null;
        var result=await Api!.ReadConversionAsync(receipt.SourceTaskId!.Value,token);if(!Current(generation))return;
        if(result.Succeeded&&result.Value is {} status){Status=status;SourceTaskId=status.Source.Id;ResetDrafts(status);Message="Conversion saved. Inspect every remaining counterpart/tag gap; the original source remains retained.";}
        else {Status=null;Message="Conversion was saved, but current status is unavailable. Keep the original receipt and reload before another review.";}
    }
    public void CancelReview()
    {
        CheckScope();if(IsBusy||AcceptanceUnknown)return;
        if(Preview is null&&Request is not null&&!PreparationRejected){Message="Preparation is unresolved. Recover the original request first.";Changed?.Invoke();return;}
        Request=null;Preview=null;PreparationRejected=false;_approved=false;_reviewId=Guid.Empty;_reviewedPages.Clear();ReviewOffset=0;Message="Unaccepted review cancelled. Read current source data before another review.";Changed?.Invoke();
    }
    private void SessionChanged(){CheckScope();Changed?.Invoke();}
    public void CheckScope(){if(_account==Account&&_workspace==_cloud.WorkspaceId)return;_account=Account;_workspace=_cloud.WorkspaceId;_generation++;IsBusy=AcceptanceUnknown=PreparationRejected=false;SourceTaskId=null;Status=null;Preview=null;Request=null;LastReceipt=null;_drafts.Clear();_reviewedPages.Clear();_reviewId=Guid.Empty;_approved=_tagsReviewed=false;Message=null;ProjectId=null;ProjectName="";Difficulty=null;Attribute=null;}
    private bool Current(long generation)=>!_disposed&&generation==_generation&&Account==_account&&_cloud.WorkspaceId==_workspace;
    private sealed class DraftException(string message):Exception(message);
    private async Task RunAsync(Func<long,Task> action,CancellationToken token)
    {
        CheckScope();if(IsBusy||!Available||_disposed)return;var generation=_generation;IsBusy=true;Changed?.Invoke();
        try{await action(generation);}
        catch(OperationCanceledException) when(token.IsCancellationRequested){if(Current(generation))Message="The operation was interrupted. Preserve the original request/review and recover its result.";}
        catch(DraftException error){if(Current(generation))Message=error.Message;}
        catch{if(Current(generation))Message="Conversion result is unavailable. Preserve the original request/review and check the original receipt.";}
        finally{if(Current(generation)){IsBusy=false;Changed?.Invoke();}}
    }
    public void Dispose(){_disposed=true;_generation++;_session.Changed-=SessionChanged;}
}
