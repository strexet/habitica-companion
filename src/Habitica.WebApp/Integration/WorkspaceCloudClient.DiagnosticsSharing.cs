using System.Text.Json;
using Habitica.Application.Diagnostics;
namespace Habitica.WebApp.Integration;
public sealed partial class WorkspaceCloudClient:IDiagnosticSharingApi
{
    public string DiagnosticIdentity=>$"{WorkspaceId}:{_generation}:{_vault.SessionVersion}";
    public bool DiagnosticSharingAvailable=>IsAvailable&&WorkspaceId is not null&&_vault.HasConnection;
    public Task<DiagnosticCloudResult<DiagnosticSharingStatus>> ReadDiagnosticSharingAsync(CancellationToken token)=>DiagnosticCall<DiagnosticSharingStatus>("status",null,ValidDiagnosticStatus,token);
    public Task<DiagnosticCloudResult<DiagnosticSharingStatus>> SetDiagnosticSharingAsync(bool enabled,CancellationToken token)=>DiagnosticCall<DiagnosticSharingStatus>("consent",new{expectedWorkspaceId=WorkspaceId,consentVersion="diagnostics-v1",enabled},ValidDiagnosticStatus,token,true);
    public Task<DiagnosticCloudResult<DiagnosticUploadView>> PrepareDiagnosticUploadAsync(Guid requestId,DiagnosticSharedBundle bundle,CancellationToken token)
    {
        if(!DiagnosticSharing.ValidRequestId(requestId)||!DiagnosticSharing.Valid(bundle))return Task.FromResult(new DiagnosticCloudResult<DiagnosticUploadView>(false,null,"invalid_diagnostic_review"));
        var hash=DiagnosticSharing.Hash(bundle);var size=DiagnosticSharing.Bytes(bundle);
        return DiagnosticCall<DiagnosticUploadView>("prepare",new{expectedWorkspaceId=WorkspaceId,requestId,bundleHash=hash,payloadBytes=size,eventCount=bundle.Entries.Count},view=>ValidDiagnosticUpload(view)&&view.RequestId==requestId&&view.BundleHash==hash&&view.PayloadBytes==size&&view.EventCount==bundle.Entries.Count,token,true);
    }
    public Task<DiagnosticCloudResult<DiagnosticUploadView>> AcceptDiagnosticUploadAsync(DiagnosticUploadView review,DiagnosticSharedBundle bundle,CancellationToken token)
    {
        if(!ValidDiagnosticUpload(review)||!DiagnosticSharing.Valid(bundle)||review.BundleHash!=DiagnosticSharing.Hash(bundle)||review.PayloadBytes!=DiagnosticSharing.Bytes(bundle)||review.EventCount!=bundle.Entries.Count)return Task.FromResult(new DiagnosticCloudResult<DiagnosticUploadView>(false,null,"diagnostic_review_changed"));
        return DiagnosticCall<DiagnosticUploadView>("accept",new{expectedWorkspaceId=WorkspaceId,uploadId=review.UploadId,reviewedHash=review.BundleHash,bundle},view=>ValidDiagnosticUpload(view)&&view.RequestId==review.RequestId&&view.UploadId==review.UploadId&&view.BundleHash==review.BundleHash&&view.ExpiresAtUtc==review.ExpiresAtUtc,token,true);
    }
    public Task<DiagnosticCloudResult<DiagnosticUploadView>> RecoverDiagnosticUploadAsync(Guid requestId,CancellationToken token)=>DiagnosticCall<DiagnosticUploadView>("original",new{expectedWorkspaceId=WorkspaceId,requestId},view=>ValidDiagnosticUpload(view)&&view.RequestId==requestId,token);
    public Task<DiagnosticCloudResult<DiagnosticReadBundle>> ReadDiagnosticBundleAsync(Guid uploadId,CancellationToken token)=>DiagnosticCall<DiagnosticReadBundle>("bundle",new{expectedWorkspaceId=WorkspaceId,uploadId},view=>view.WorkspaceId==WorkspaceId&&view.UploadId==uploadId&&view.Source=="explicit-browser-upload"&&!view.BrowserTimesTrusted&&!view.CompleteDeviceHistory&&view.Bundle is not null&&DiagnosticSharing.Valid(view.Bundle)&&view.BundleHash==DiagnosticSharing.Hash(view.Bundle)&&view.EventCount==view.Bundle.Entries.Count&&view.PayloadBytes==DiagnosticSharing.Bytes(view.Bundle)&&ValidDiagnosticUpload(new(view.WorkspaceId,view.RequestId,view.Saved,view.OutcomeUnknown,view.UploadId,view.BundleHash,view.State,view.EventCount,view.PayloadBytes,view.CreatedAtUtc,view.AcceptByUtc,view.ExpiresAtUtc,view.Receipt)),token);
    public Task<DiagnosticCloudResult<DiagnosticReadPage>> QueryDiagnosticsAsync(DiagnosticReadQuery query,CancellationToken token)
    {
        if(query.FromUtc.Offset!=TimeSpan.Zero||query.ToUtc.Offset!=TimeSpan.Zero||query.FromUtc>query.ToUtc||query.Offset<0||query.Offset>100000||query.Offset>0&&(query.ExpectedRevision is null||query.ExpectedWorkspaceVersion is null||query.AsOfUtc is null||query.ValidUntilUtc is null))return Task.FromResult(new DiagnosticCloudResult<DiagnosticReadPage>(false,null,"invalid_diagnostic_query"));
        var args=new Dictionary<string,object?>{{"expectedWorkspaceId",WorkspaceId},{"fromUtc",query.FromUtc},{"toUtc",query.ToUtc},{"offset",query.Offset}};
        void Add(string key,object? value){if(value is not null)args[key]=value;}
        Add("feature",query.Feature);Add("operation",query.Operation);Add("severity",query.Severity);Add("commandId",query.CommandId);Add("correlationId",query.CorrelationId);Add("source",query.Source);Add("expectedRevision",query.ExpectedRevision);Add("expectedWorkspaceVersion",query.ExpectedWorkspaceVersion);Add("asOfUtc",query.AsOfUtc);Add("validUntilUtc",query.ValidUntilUtc);
        return DiagnosticCall<DiagnosticReadPage>("query",args,page=>ValidDiagnosticPage(page,query),token);
    }
    private bool ValidDiagnosticStatus(DiagnosticSharingStatus s)=>s.WorkspaceId==WorkspaceId&&s.ConsentVersion=="diagnostics-v1"&&s.Revision>=0&&(!s.Enabled||s.ConsentEpoch is {} epoch&&epoch!=Guid.Empty&&s.Revision>=1)&&s.EventCount is >=0 and <=DiagnosticSharing.MaxEvents&&s.ChargedBytes is >=0 and <=DiagnosticSharing.MaxBytes
        &&s.Limits is {EventRows:DiagnosticSharing.MaxEvents,ChargedBytes:DiagnosticSharing.MaxBytes,EntriesPerBundle:DiagnosticSharing.MaxBundleEntries,RetentionDays:7}&&s.Sources is not null&&s.Omitted is {Count:<=16};
    private bool ValidDiagnosticUpload(DiagnosticUploadView v)=>v.WorkspaceId==WorkspaceId&&DiagnosticSharing.ValidRequestId(v.RequestId)&&v.UploadId is {} id&&id!=Guid.Empty&&v.BundleHash is {Length:64} hash&&hash.All(Uri.IsHexDigit)&&v.State is "prepared" or "accepted" or "expired"
        &&v.Saved==(v.State=="accepted")&&v.OutcomeUnknown==(v.State=="prepared")&&v.EventCount is >=0 and <=100&&v.PayloadBytes is >0 and <=DiagnosticSharing.MaxBytes&&v.CreatedAtUtc!=default&&v.CreatedAtUtc.Offset==TimeSpan.Zero&&v.AcceptByUtc>v.CreatedAtUtc&&v.AcceptByUtc<=v.CreatedAtUtc.AddMinutes(5).AddSeconds(1)&&v.ExpiresAtUtc>v.CreatedAtUtc&&v.ExpiresAtUtc<=v.CreatedAtUtc.AddDays(7).AddSeconds(1)
        &&(v.State!="accepted"?v.Receipt is null:v.Receipt is {} r&&r.WorkspaceId==v.WorkspaceId&&r.UploadId==v.UploadId&&r.RequestId==v.RequestId&&r.BundleHash==v.BundleHash&&r.EventCount==v.EventCount&&r.PayloadBytes==v.PayloadBytes&&r.Saved&&r.AcceptedAtUtc>=v.CreatedAtUtc&&r.AcceptedAtUtc<=v.AcceptByUtc&&r.ExpiresAtUtc==v.ExpiresAtUtc);
    private bool ValidDiagnosticPage(DiagnosticReadPage p,DiagnosticReadQuery q)=>p.WorkspaceId==WorkspaceId&&p.Revision>=1&&p.WorkspaceVersion>=1&&(q.ExpectedRevision is null||q.ExpectedRevision==p.Revision)&&(q.ExpectedWorkspaceVersion is null||q.ExpectedWorkspaceVersion==p.WorkspaceVersion)&&(q.AsOfUtc is null||q.AsOfUtc==p.AsOfUtc)&&(q.ValidUntilUtc is null||q.ValidUntilUtc==p.ValidUntilUtc)
        &&p.AsOfUtc.Offset==TimeSpan.Zero&&p.ValidUntilUtc>p.AsOfUtc&&p.Items is {Count:<=20}&&p.MatchedCount is >=0 and <=2600&&p.Items.Count<=p.MatchedCount&&(p.NextOffset is null||p.NextOffset==q.Offset+p.Items.Count&&p.NextOffset<p.MatchedCount)&&p.Items.Select(i=>i.Id).Distinct().Count()==p.Items.Count&&p.Items.All(ValidDiagnosticItem)&&p.Coverage is {ServerPerKindLimit:200,OmittedServerReceiptLowerBound:>=0,Unavailable:not null};
    private static bool ValidDiagnosticItem(DiagnosticReadItem i)
    {
        if(i.Source=="browser-shared")return i.Entry is {} e&&i.Receipt is null&&i.UploadId is {} upload&&upload!=Guid.Empty&&i.Id==e.Id&&i.OccurredAtUtc==e.OccurredAtUtc&&i.ReceivedAtUtc is not null&&i.TimeBasis=="untrusted-browser-occurrence"&&DiagnosticSharing.Valid(new(1,i.OccurredAtUtc,"browser-local-store",new(null,null,null),1,1,0,[e]));
        if(i.Entry is not null||i.UploadId is not null||i.Receipt is not {} r||r.CommandId==Guid.Empty||r.LiveVerification||r.Operation is not ("workspace-task-accept" or "workspace-organization-accept" or "workspace-tag-accept" or "workspace-tag-recover"))return false;
        var prefix=i.Source switch {"server-task-receipt"=>"server-task-","server-organization-receipt"=>"server-organization-","server-tag-receipt"=>"server-tag-",_=>null};
        return prefix is not null&&i.Id==prefix+r.CommandId.ToString("D")&&i.TimeBasis is "server-acceptance" or "server-terminal-closure" or "review-creation-acceptance-time-unavailable"&&r.GameEffects is null or 0&&r.RecordedConfirmedCount is null or >=0 and <=11&&r.RecordedRequestedCount is null or >=0 and <=11&&r.RecordedProjectionState is null or "notRequested" or "pending" or "leased" or "executing" or "confirmed" or "blocked" or "unknownOutcome" or "cancelled" or "unavailable";
    }
    private async Task<DiagnosticCloudResult<T>> DiagnosticCall<T>(string operation,object? args,Func<T,bool> valid,CancellationToken token,bool mutation=false)
    {
        if(!DiagnosticSharingAvailable)return new(false,default,"diagnostic_owner_access_unavailable");var generation=_generation;var vaultVersion=_vault.SessionVersion;var workspace=WorkspaceId;
        try
        {
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromSeconds(30));var operationToken=deadline.Token;
            using var request=_vault.DiagnosticOwnerRequest(new(_baseUri!,"diagnostics/"+operation),args is null?HttpMethod.Get:HttpMethod.Post,args);
            using var response=await _http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,operationToken);
            if(!response.IsSuccessStatusCode)return new(false,default,(int)response.StatusCode==403?"diagnostic_consent_or_authority_required":(int)response.StatusCode==409?"diagnostic_review_or_quota_changed":"diagnostic_unavailable",operation=="original"||mutation&&(int)response.StatusCode>=500);
            var text=await DiagnosticTextAsync(response.Content,operationToken);if(System.Text.Encoding.UTF8.GetByteCount(text)>DiagnosticSharing.MaxBytes+65536)return new(false,default,"diagnostic_response_oversized",mutation);
            var value=JsonSerializer.Deserialize<T>(text,DiagnosticSharing.Json);
            if(generation!=_generation||vaultVersion!=_vault.SessionVersion||workspace!=WorkspaceId)return new(false,default,"diagnostic_account_changed",true);
            return value is not null&&valid(value)?new(true,value,"diagnostic_state_received"):new(false,default,"diagnostic_response_invalid",true);
        }
        catch(OperationCanceledException) when(token.IsCancellationRequested){throw;}
        catch{return new(false,default,"diagnostic_unavailable",mutation||operation=="original");}
    }
    private static async Task<string> DiagnosticTextAsync(HttpContent content,CancellationToken token)
    {
        const int limit=DiagnosticSharing.MaxBytes+65536;
        if(content.Headers.ContentLength>limit)throw new InvalidOperationException();await using var input=await content.ReadAsStreamAsync(token);using var output=new MemoryStream();var bytes=new byte[8192];int read;
        while((read=await input.ReadAsync(bytes,token))>0){if(output.Length+read>limit)throw new InvalidOperationException();await output.WriteAsync(bytes.AsMemory(0,read),token);}return new System.Text.UTF8Encoding(false,true).GetString(output.GetBuffer(),0,(int)output.Length);
    }

}
