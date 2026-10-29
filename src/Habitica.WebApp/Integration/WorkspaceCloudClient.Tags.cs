using System.Text;
using Habitica.Application.Workspace;
using Habitica.Application.Integration;
using Habitica.Domain.Diagnostics;
using Habitica.Domain.Workspace;

namespace Habitica.WebApp.Integration;

public sealed partial class WorkspaceCloudClient : IWorkspaceTagApi
{
    public Task<IntegrationTagConsentResult> ReadTagConsentAsync(CancellationToken cancellationToken) => ObserveTagConsentAsync(null,cancellationToken);
    public Task<IntegrationTagConsentResult> SetTagConsentAsync(bool enabled,CancellationToken cancellationToken) => ObserveTagConsentAsync(enabled,cancellationToken);
    private Task<IntegrationTagConsentResult> ObserveTagConsentAsync(bool? enabled,CancellationToken cancellationToken)
    {
        Task<IntegrationTagConsentResult> Action()=>enabled is null?_vault.ReadTagConsentAsync(cancellationToken):_vault.SetTagConsentAsync(enabled.Value,cancellationToken);
        if(_diagnostics is null)return Action();var generation=_generation;
        return _diagnostics.ObserveAsync(DiagnosticsFeatureArea.Workspace,enabled is null?"workspace-tag-consent-read":"workspace-tag-consent-set",Action,
            result=>new(result.Succeeded?DiagnosticsResult.Success:DiagnosticsResult.Unavailable),()=>generation==_generation,
            enabled is null?DiagnosticsMode.LiveRead:DiagnosticsMode.LiveMutation);
    }
    public Task<WorkspaceTaskResponse<WorkspaceTagStatusPage>> ReadGroupTagsAsync(int offset,long? expectedRevision,CancellationToken cancellationToken)
    {
        var args=new Dictionary<string,object?> { ["offset"]=offset,["limit"]=20 };
        if(expectedRevision is not null)args["expectedRevision"]=expectedRevision;
        return ObserveResponseAsync("workspace-tag-status",async()=>
        {
            if(offset<0)return new WorkspaceTaskResponse<WorkspaceTagStatusPage>(false,null,"The tag page is invalid.");
            var result=await CallTagAsync<WorkspaceTagStatusPage>("get_group_tag_status",args,cancellationToken);
            if(result.Value is not {} page)return result;
            var t=page.TaskCoverage;var g=page.GroupCoverage;
            if(page.WorkspaceId!=WorkspaceId||page.Provenance!="stored-canonical-evidence"||page.Revision<1||page.RetiredSourceContainers is <0 or >1000||expectedRevision is not null&&page.Revision!=expectedRevision
                ||t is null||g is null||!Counts(t.Total,t.Linked,t.Pending,t.Missing,t.NeedsReview,t.Archived)||t.Linked+t.Pending+t.Missing+t.NeedsReview!=t.Total||t.Archived>t.Total
                ||!Counts(g.Total,g.Confirmed,g.Pending,g.NeedsReview,g.Missing)||g.Confirmed+g.Pending+g.NeedsReview+g.Missing!=g.Total
                ||page.Groups is null||page.Groups.Count>20||page.MatchedCount<0||page.MatchedCount>g.Total||page.Groups.Count>page.MatchedCount
                ||page.NextOffset is {} next&&(next!=offset+page.Groups.Count||next>=page.MatchedCount)
                ||page.Groups.Select(row=>row.EntityId).Distinct().Count()!=page.Groups.Count
                ||page.Groups.Any(row=>row.EntityId==Guid.Empty||!Enum.IsDefined(row.Kind)||!Name(row.Name)||!CoverageStates.Contains(row.IdentityState)||!MembershipStates.Contains(row.MembershipState)
                    ||(row.UnknownWorkIds is {} ids && (ids.Count>100000||ids.Any(id=>id==Guid.Empty)||ids.Distinct().Count()!=ids.Count))
                    ||!Remote(row.HabiticaTagId)||!page.AccountVerified&&row.HabiticaTagId is not null||!Counts(row.DesiredTaskCount,row.ConfirmedMemberCount))
                ||page.IntegrationComplete&&(!page.AccountVerified||!page.TagConsentEnabled||!page.TagScopeAllowed||t.Linked!=t.Total||g.Confirmed!=g.Total))return new(false,null,"Tag coverage could not be validated.");
            return result;
        });
    }
    public Task<WorkspaceTaskResponse<WorkspaceTagCandidatePage>> InspectTagCandidatesAsync(WorkspaceOrganizationKind kind,Guid entityId,int offset,string? expectedFingerprint,CancellationToken cancellationToken)
    {
        var args=new Dictionary<string,object?> { ["kind"]=kind,["entityId"]=entityId,["offset"]=offset,["limit"]=20 };
        if(expectedFingerprint is not null)args["expectedFingerprint"]=expectedFingerprint;
        return ObserveResponseAsync("workspace-tag-candidates",async()=>
        {
            if(entityId==Guid.Empty||!Enum.IsDefined(kind)||offset<0)return new WorkspaceTaskResponse<WorkspaceTagCandidatePage>(false,null,"Select an exact group first.");
            var result=await CallTagAsync<WorkspaceTagCandidatePage>("inspect_group_tag_candidates",args,cancellationToken);
            if(result.Value is not {} page)return result;
            if(page.WorkspaceId!=WorkspaceId||page.EntityId!=entityId||page.Kind!=kind||page.Revision<1||!Name(page.Name)||!Fingerprint(page.Fingerprint)
                ||expectedFingerprint is not null&&page.Fingerprint!=expectedFingerprint||page.Candidates is null||page.Candidates.Count>20||!Counts(page.MatchedCount)
                ||page.Candidates.Any(tag=>tag.Id==Guid.Empty||!Name(tag.Name))||page.Candidates.Select(tag=>tag.Id).Distinct().Count()!=page.Candidates.Count
                ||page.NextOffset is {} next&&(next!=offset+page.Candidates.Count||next>=page.MatchedCount))return new(false,null,"The native tag candidates changed or could not be validated.");
            return result;
        });
    }
    public Task<WorkspaceTaskResponse<WorkspaceTagReview>> PrepareTagReviewAsync(WorkspaceTagRequest intent,CancellationToken cancellationToken)
    {
        var args=new Dictionary<string,object?> { ["requestId"]=intent.RequestId,["operation"]=intent.Operation };
        if(intent.Kind is not null)args["kind"]=intent.Kind;if(intent.EntityId is not null)args["entityId"]=intent.EntityId;
        if(intent.HabiticaTagId is not null)args["habiticaTagId"]=intent.HabiticaTagId;if(intent.WorkId is not null)args["workId"]=intent.WorkId;
        return ObserveResponseAsync("workspace-tag-prepare",async()=>
        {
            if(intent.RequestId==Guid.Empty||intent.Operation is not ("reconcile" or "adopt" or "recover"))return new WorkspaceTaskResponse<WorkspaceTagReview>(false,null,"The tag review request is invalid.");
            var result=await CallTagAsync<WorkspaceTagReview>("prepare_group_tag_command",args,cancellationToken);
            if(result.Value is not {} review)return result;
            if(review.Receipt is { SavedInCompanion:false } terminal&&ValidTagReceipt(terminal,review.CommandId??Guid.Empty)
                &&terminal.RequestId==intent.RequestId&&terminal.Operation==intent.Operation&&review.RequestId==intent.RequestId&&review.WorkspaceId==WorkspaceId&&!review.CanAccept)return result;
            if(!ValidTagReview(review)||review.RequestId!=intent.RequestId||review.Operation!=intent.Operation
                ||(intent.Operation=="recover"?review.Details?.OriginalWorkId!=intent.WorkId:review.Details?.EntityId!=intent.EntityId||review.Details?.Kind!=intent.Kind)
                ||intent.HabiticaTagId is {} tag&&review.Details?.HabiticaTagId!=tag.ToString("D"))return new(false,null,"The tag preview did not match the original request. Preserve the request ID.",true);
            return result;
        },correlationId:intent.RequestId);
    }
    public Task<WorkspaceTaskResponse<WorkspaceTagReceipt>> AcceptTagReviewAsync(WorkspaceTagReview review,CancellationToken cancellationToken)=>ObserveResponseAsync("workspace-tag-accept",async()=>
    {
        if(!ValidTagReview(review))return new WorkspaceTaskResponse<WorkspaceTagReceipt>(false,null,"Review a current exact tag preview first.");
        var result=await CallTagAsync<WorkspaceTagReceipt>("accept_group_tag_command",new { commandId=review.CommandId },cancellationToken);
        return result.Value is {} receipt&&(!ValidTagReceipt(receipt,review.CommandId!.Value)||receipt.Operation!=review.Operation||receipt.RequestId!=review.RequestId)?new(false,null,"The original tag receipt could not be validated.",true):result;
    },review.CommandId,review.RequestId);
    public Task<WorkspaceTaskResponse<WorkspaceTagReceipt>> ReadTagReviewAsync(Guid commandId,CancellationToken cancellationToken)=>ObserveResponseAsync("workspace-tag-recover",async()=>
    {
        var result=await CallTagAsync<WorkspaceTagReceipt>("get_group_tag_command",new { commandId },cancellationToken);
        return result.Value is {} receipt&&!ValidTagReceipt(receipt,commandId)?new(false,null,"No confirmed original tag acceptance is available. Keep the original review.",true):result;
    },commandId);
    public Task<WorkspaceTaskResponse<WorkspaceTagOriginal>> RecoverTagReceiptAsync(Guid workId,CancellationToken cancellationToken)=>ObserveResponseAsync("workspace-tag-original-receipt",async()=>
    {
        var result=await CallTagAsync<WorkspaceTagOriginal>("recover_group_tag_receipt",new { workId },cancellationToken);
        return result.Value is {} original&&(!ValidOriginal(original)||original.WorkId!=workId||original.GameEffects!=0)?new(false,null,"The original tag result is unavailable; no mutation was retried.",true):result;
    },workId);
    private bool ValidTagReview(WorkspaceTagReview review)=>review.WorkspaceId==WorkspaceId&&review.CommandId is {} id&&id!=Guid.Empty&&review.RequestId is {} request&&request!=Guid.Empty
        &&review.CanAccept&&review.GameEffects==0&&review.ExpectedRevision>=1&&review.ExpiresAtUtc!=default&&review.ExpiresAtUtc.Offset==TimeSpan.Zero
        &&review.Operation is "reconcile" or "adopt" or "recover"&&review.Details is {} details&&details.EntityId!=Guid.Empty&&Enum.IsDefined(details.Kind)&&Name(details.Name)
        &&Remote(details.HabiticaTagId)&&details.Disclosure is { Length: >0 and <=4096 }&&details.TaskChanges is not null&&details.TaskChanges.Count<=100000
        &&details.TaskChanges.All(row=>row.TaskId!=Guid.Empty&&Remote(row.HabiticaTaskId)&&row.Text is not null&&row.Text.Length<=2048&&MemberStates.Contains(row.State))
        &&details.TaskChanges.Select(row=>row.TaskId).Distinct().Count()==details.TaskChanges.Count
        &&(review.Operation!="recover"||details.OriginalWorkId is {} work&&work!=Guid.Empty&&details.OriginalAttemptId is {} attempt&&attempt!=Guid.Empty)
        &&(review.Operation!="adopt"||details.ObservedTag is {} tag&&tag.Id!=Guid.Empty&&tag.Id.ToString("D")==details.HabiticaTagId&&Name(tag.Name));
    private bool ValidTagReceipt(WorkspaceTagReceipt receipt,Guid commandId)=>receipt.CommandId==commandId&&commandId!=Guid.Empty
        &&receipt.WorkspaceId==WorkspaceId&&receipt.RequestId is {} request&&request!=Guid.Empty&&receipt.Operation is "reconcile" or "adopt" or "recover"&&receipt.GameEffects==0
        &&receipt.WorkIds is not null&&receipt.WorkIds.All(id=>id!=Guid.Empty)&&receipt.WorkIds.Distinct().Count()==receipt.WorkIds.Count
        &&(receipt.Original is null||ValidOriginal(receipt.Original))
        &&(receipt.SavedInCompanion==true
            ?receipt.ReviewOutcome is null||(receipt.ReviewOutcome=="accepted-unknown")==receipt.OutcomeUnknown&&receipt.ReviewOutcome is "accepted" or "accepted-unknown"
            :receipt.SavedInCompanion==false
                ?receipt.ReviewOutcome=="terminal-unaccepted"&&!receipt.OutcomeUnknown&&receipt.WorkIds.Count==0&&receipt.Original is null
                    &&receipt.Reason is "tag_preview_expired" or "tag_preview_changed" or "tag_connection_changed" or "tag_authority_changed"
                    &&receipt.ClosedAtUtc is {} closed&&closed!=default&&closed.Offset==TimeSpan.Zero
                :receipt.ReviewOutcome=="prepared"&&receipt.Prepared==true&&receipt.OutcomeUnknown&&receipt.WorkIds.Count==0&&receipt.Original is null);
    private static bool ValidOriginal(WorkspaceTagOriginal original)=>original.WorkId!=Guid.Empty&&original.CommandId!=Guid.Empty&&original.SourceRevision>=1
        &&original.EntityId!=Guid.Empty&&Enum.IsDefined(original.Kind)&&(MemberStates.Contains(original.State)||original.State=="executing")&&original.Operation is "ensureTag" or "setMembership";
    private static readonly string[] CoverageStates=["missing","pending","linked","retained","needsReview"];
    private static readonly string[] MembershipStates=["missing","pending","confirmed","retained","needsReview"];
    private static readonly string[] MemberStates=["pending","confirmed","unknownOutcome","needsReview"];
    private static bool Counts(params int[] values)=>values.All(value=>value is >=0 and <=100000);
    private static bool Name(string? value)=>!string.IsNullOrWhiteSpace(value)&&value.Length<=256;
    private static bool Remote(string? value)=>value is null||Guid.TryParseExact(value,"D",out var id)&&id!=Guid.Empty;
    private static bool Fingerprint(string? value)=>value is not null&&System.Text.RegularExpressions.Regex.IsMatch(value,"^[a-f0-9]{64}$");
    private async Task<WorkspaceTaskResponse<T>> CallTagAsync<T>(string tool,object args,CancellationToken cancellationToken)
    {
        if(WorkspaceId is null||_token is null)return new(false,default,"Connect the cloud workspace first.");var generation=_generation;
        try
        {
            using var request=Request(HttpMethod.Post,"tag-api");request.Content=new StringContent(WorkspaceTaskApiJson.Serialize(new { tool,arguments=args }),Encoding.UTF8,"application/json");
            using var response=await _http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancellationToken);
            if(!response.IsSuccessStatusCode)return new(false,default,(int)response.StatusCode==403?"Separate tag consent and agent tag permission are required. Verify Settings and the connection.":"Tag state is unavailable or changed. Preserve the original request/command ID.",tool is "prepare_group_tag_command" or "accept_group_tag_command"&&(int)response.StatusCode>=500);
            var result=WorkspaceTaskApiJson.Deserialize<T>(await BoundedTextAsync(response.Content,cancellationToken));
            return generation==_generation?new(true,result,"Tag metadata state received. No game effect was requested."):new(false,default,"The account changed; tag state was discarded.",true);
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested){throw;}
        catch{return new(false,default,"Tag state is unavailable. Check the original receipt before another action.",tool is "prepare_group_tag_command" or "accept_group_tag_command");}
    }
}
