using Habitica.Application.Diagnostics;
using Habitica.Domain.Diagnostics;
namespace Habitica.WebApp.State;

// Scoped to this browser tab. Navigation preserves a review; identity changes erase private state.
public sealed class DiagnosticSharingController:IDisposable
{
    private readonly IAppSessionController _session;
    private readonly IDiagnosticSharingApi _api;
    private readonly TimeProvider _clock;
    private string? _account;
    private Guid? _workspace;
    private string? _identity;
    public bool AcceptanceAttempted {get;private set;}
    private int _generation;
    public event Action? Changed;
    public DiagnosticSharingController(IAppSessionController session,IDiagnosticSharingApi api,TimeProvider clock)
    {_session=session;_api=api;_clock=clock;session.Changed+=SessionChanged;EnsureIdentity();}
    public DiagnosticsBundleFilters Filters {get;private set;}=new();
    public DiagnosticSharedBundle? Preview {get;private set;}
    public DiagnosticUploadView? Original {get;private set;}
    public Guid? RequestId {get;private set;}
    public DiagnosticSharingStatus? Status {get;private set;}
    public string? Message {get;private set;}
    public bool Busy {get;private set;}
    public bool Unresolved {get;private set;}
    public bool Available {get {EnsureIdentity();return _account is not null&&_api.DiagnosticSharingAvailable;}}
    public Guid? WorkspaceId=>_workspace;
    public bool CanReplace=>!Busy&&!Unresolved;
    public bool CanUpload=>!AcceptanceAttempted&&!Busy&&Status is {Enabled:true,CleanupPending:false}&&Preview is not null&&Original is {State:"prepared"}&&Original.AcceptByUtc>_clock.GetUtcNow();
    private void SessionChanged(){EnsureIdentity();Changed?.Invoke();}
    public void EnsureIdentity()
    {
        var account=_session.State.IsAuthenticated?_session.State.UserId:null;var workspace=_api.WorkspaceId;
        if(_account==account&&_workspace==workspace&&_identity==_api.DiagnosticIdentity)return;
        _identity=_api.DiagnosticIdentity;
        _generation++;_account=account;_workspace=workspace;Filters=new();Preview=null;Original=null;RequestId=null;Status=null;Message=null;Unresolved=false;Busy=false;AcceptanceAttempted=false;
    }
    private bool Current(int generation){EnsureIdentity();return generation==_generation;}
    public void SetFilters(DiagnosticsBundleFilters filters)
    {EnsureIdentity();if(!CanReplace)return;Filters=filters;Preview=null;Original=null;RequestId=null;AcceptanceAttempted=false;Message=null;Changed?.Invoke();}
    public void PreviewLocal()
    {
        EnsureIdentity();if(!CanReplace||!Available)return;
        Preview=DiagnosticSharing.Prepare(DiagnosticsBundleExport.Create(_session.State.DiagnosticsLogEntries??[],Filters,_clock.GetUtcNow()));Original=null;RequestId=null;AcceptanceAttempted=false;
        Message="Local preview only. No diagnostic content has been sent.";Changed?.Invoke();
    }
    public Task RefreshAsync()=>Run(async generation=>
    {
        var r=await _api.ReadDiagnosticSharingAsync(CancellationToken.None);if(!Current(generation))return;
        Status=r.Succeeded?r.Value:null;Message=r.Succeeded?"Sharing status received. No browser messages were uploaded.":"Remote status is unavailable. Manual copy/download remains available.";
    });
    public Task SetConsentAsync(bool enabled)=>Run(async generation=>
    {
        Status=null;var r=await _api.SetDiagnosticSharingAsync(enabled,CancellationToken.None);if(!Current(generation))return;
        Status=r.Succeeded?r.Value:null;
        if(!r.Succeeded){Message="Consent or cleanup could not be confirmed. Check sharing status; no new upload is enabled.";return;}
        if(!enabled){Preview=null;Original=null;RequestId=null;Unresolved=false;AcceptanceAttempted=false;Message=r.Value!.CleanupPending?"Access is revoked. Physical cleanup is pending or failed; deletion is not confirmed.":"Access is revoked and primary shared records were cleared. Provider recovery history is separate.";}
        else Message="Separate diagnostic consent is enabled. Review a local bundle before sending any messages.";
    });
    public Task PrepareAsync()=>Run(async generation=>
    {
        if(Preview is not {} bundle||Status is not {Enabled:true,CleanupPending:false}||Unresolved)return;
        RequestId??=DiagnosticSharing.NewRequestId(_clock.GetUtcNow());Unresolved=true;
        var r=await _api.PrepareDiagnosticUploadAsync(RequestId.Value,bundle,CancellationToken.None);if(!Current(generation))return;
        if(r.Succeeded&&r.Value is {} original){Original=original;Unresolved=original.State=="prepared";Message="Metadata-only preparation received. Review the exact content and confirm upload separately.";}
        else Message="Preparation could not be confirmed. Preserve this request and check the original upload before replacing it.";
    });
    public Task UploadAsync()=>Run(async generation=>
    {
        // Busy is set by Run; all other eligibility checks remain required here.
        if(AcceptanceAttempted||Status is not {Enabled:true,CleanupPending:false}||Preview is not {} bundle||Original is not {State:"prepared"} review||review.AcceptByUtc<=_clock.GetUtcNow())return;
        AcceptanceAttempted=true;Unresolved=true;var r=await _api.AcceptDiagnosticUploadAsync(review,bundle,CancellationToken.None);if(!Current(generation))return;
        ApplyOriginal(r);
    });
    public Task RecoverAsync()=>Run(async generation=>
    {
        if(RequestId is not {} id)return;var r=await _api.RecoverDiagnosticUploadAsync(id,CancellationToken.None);if(Current(generation))ApplyOriginal(r);
    });
    private void ApplyOriginal(DiagnosticCloudResult<DiagnosticUploadView> result)
    {
        if(result.Succeeded&&result.Value is {} value&&value.RequestId==RequestId)
        {Original=value;Unresolved=value.State=="prepared";Message=value.Saved==true?"Original upload receipt confirmed. Authorized agents can read this shared copy until expiry; no agent result is promised.":value.State=="expired"?"The original preparation is terminal and was not accepted. Create a fresh local review if needed.":"Original preparation remains open. No acceptance is confirmed.";}
        else {Unresolved=true;Message="Upload outcome remains unknown or unavailable. A missing receipt does not prove failure. Check the same original request; do not create a replacement.";}
    }
    private async Task Run(Func<int,Task> action)
    {
        EnsureIdentity();if(Busy||!Available)return;var generation=_generation;Busy=true;Changed?.Invoke();
        try{await action(generation);}catch{if(Current(generation))Message=Unresolved?"Outcome remains unknown. Check the same original upload.":"Remote operation is unavailable. Check sharing status before continuing.";}
        finally{if(Current(generation)){Busy=false;Changed?.Invoke();}}
    }
    public void Dispose()=>_session.Changed-=SessionChanged;
}
