using Habitica.Application.Diagnostics;
namespace Habitica.WebApp.Tests;
internal sealed class DiagnosticSharingFake:IDiagnosticSharingApi
{
    public bool DiagnosticSharingAvailable {get;set;}=true;
    public Guid? WorkspaceId {get;set;}=Guid.Parse("7813bac6-9350-4320-8624-41122c2d2e00");
    public string DiagnosticIdentity {get;set;}="fixture-connection-1";
    public int Preparations,Uploads,Reads,ConsentCalls;
    public bool Enabled,CleanupPending,LoseAck,Missing,Refuse,ThrowConsent;
    public DiagnosticSharedBundle? Uploaded;
    public DiagnosticUploadView? Original;
    public TaskCompletionSource<DiagnosticCloudResult<DiagnosticUploadView>>? Held;
    private DiagnosticSharingStatus Status=>new(WorkspaceId!.Value,"diagnostics-v1",Enabled,Enabled?Guid.NewGuid():null,1,CleanupPending,Uploaded?.Entries.Count??0,0,new(2000,1048576,100,7),new("Explicit browser copies","Reduced receipts"),[]);
    public Task<DiagnosticCloudResult<DiagnosticSharingStatus>> ReadDiagnosticSharingAsync(CancellationToken t){Reads++;return Task.FromResult(new DiagnosticCloudResult<DiagnosticSharingStatus>(true,Status,"ok"));}
    public Task<DiagnosticCloudResult<DiagnosticSharingStatus>> SetDiagnosticSharingAsync(bool enabled,CancellationToken t){ConsentCalls++;if(ThrowConsent)throw new HttpRequestException();Enabled=enabled;if(!enabled&&!CleanupPending)Uploaded=null;return Task.FromResult(new DiagnosticCloudResult<DiagnosticSharingStatus>(true,Status,"ok"));}
    public Task<DiagnosticCloudResult<DiagnosticUploadView>> PrepareDiagnosticUploadAsync(Guid request,DiagnosticSharedBundle bundle,CancellationToken t)
    {Preparations++;var now=DateTimeOffset.UtcNow;Original=new(WorkspaceId!.Value,request,false,true,Guid.NewGuid(),DiagnosticSharing.Hash(bundle),"prepared",bundle.Entries.Count,DiagnosticSharing.Bytes(bundle),now,now.AddMinutes(5),now.AddDays(7));return Task.FromResult(new DiagnosticCloudResult<DiagnosticUploadView>(true,Original,"ok"));}
    public Task<DiagnosticCloudResult<DiagnosticUploadView>> AcceptDiagnosticUploadAsync(DiagnosticUploadView review,DiagnosticSharedBundle bundle,CancellationToken t)
    {Uploads++;if(Held is not null)return Held.Task;if(Refuse)return Task.FromResult(new DiagnosticCloudResult<DiagnosticUploadView>(false,null,"quota"));Uploaded=bundle;Original=review with {State="accepted",Saved=true,OutcomeUnknown=false,Receipt=new(review.WorkspaceId,review.UploadId!.Value,review.RequestId,review.BundleHash!,review.EventCount,review.PayloadBytes,DateTimeOffset.UtcNow,review.ExpiresAtUtc,true)};return Task.FromResult(LoseAck?new DiagnosticCloudResult<DiagnosticUploadView>(false,null,"unavailable",true):new(true,Original,"ok"));}
    public Task<DiagnosticCloudResult<DiagnosticUploadView>> RecoverDiagnosticUploadAsync(Guid request,CancellationToken t)=>Task.FromResult(Missing?new DiagnosticCloudResult<DiagnosticUploadView>(false,null,"unavailable",true):new(true,Original,"ok"));
    public Task<DiagnosticCloudResult<DiagnosticReadBundle>> ReadDiagnosticBundleAsync(Guid id,CancellationToken t)=>throw new NotSupportedException();
    public Task<DiagnosticCloudResult<DiagnosticReadPage>> QueryDiagnosticsAsync(DiagnosticReadQuery query,CancellationToken t)=>throw new NotSupportedException();
}
